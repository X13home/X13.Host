#region license
//Copyright (c) 2011-2013 <comparator@gmx.de>; Wassili Hense

//This file is part of the X13.Home project.
//https://github.com/X13home

//BSD License
//See LICENSE.txt file for license details.
#endregion license

using System;
using System.ComponentModel.Composition;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Threading;
using System.Net.NetworkInformation;
using System.Collections.Generic;

namespace X13.Periphery {
  [Export(typeof(IPlugModul))]
  [ExportMetadata("priority", 5)]
  [ExportMetadata("name", "MQTTS.udp")]
  public class MQTTSUdp : IPlugModul {
    private const long _version=301;

    public void Init() {
      Topic.root.Subscribe("/etc/MQTTS/#", Dummy);
      Topic.root.Subscribe("/etc/declarers/dev/#", Dummy);
    }
    public void Start() {
      var ver=Topic.root.Get<long>("/etc/MQTTS/udp/version");
      if(ver.value<_version) {
        ver.saved=true;
        ver.value=_version;
        Log.Info("Load MQTTS.udp declarers");
        var st=Assembly.GetExecutingAssembly().GetManifestResourceStream("X13.Periphery.MQTTSUdp.xst");
        if(st!=null) {
          using(var sr=new StreamReader(st)) {
            Topic.Import(sr, null);
          }
        }

      }
      MsDevice.MsGUdp.Open();
    }

    void Dummy(Topic src, TopicChanged arg) {
    }

    public void Stop() {
      Topic.root.Unsubscribe("/etc/MQTTS/#", Dummy);
      Topic.root.Unsubscribe("/etc/declarers/#", Dummy);
      //TODO: Close
    }
  }

  public partial class MsDevice : ITopicOwned {
    internal class MsGUdp : IMsGate {

      #region static
      private static IPAddress[] _myIps;
      private static IPAddress[] _bcIps;

      public static void Open() {
        _myIps=Dns.GetHostEntry(Dns.GetHostName()).AddressList.Where(z => z.AddressFamily==AddressFamily.InterNetwork).ToArray();
        List<IPAddress> bc=new List<IPAddress>();
        try {
          foreach(var nic in NetworkInterface.GetAllNetworkInterfaces()) {
            var ipProps = nic.GetIPProperties();
            var ipv4Addrs = ipProps.UnicastAddresses.Where(addr => addr.Address.AddressFamily == AddressFamily.InterNetwork);
            foreach(var addr in ipv4Addrs) {
              if(addr.IPv4Mask == null)
                continue;
              var ip = addr.Address.GetAddressBytes();
              var mask = addr.IPv4Mask.GetAddressBytes();
              var result = new Byte[4];
              for(int i = 0; i < 4; ++i) {
                result[i] = (Byte)(ip[i] | (mask[i]^255));
              }
              bc.Add(new IPAddress(result));
            }
          }
        }
        catch(Exception) {    // MONO: NotImplementedException
        }
        if(bc.Count==0) {
          bc.Add(new IPAddress(new byte[] { 255, 255, 255, 255 }));
        }
        _bcIps=bc.ToArray();

        MsGUdp ret;
        if(_gates!=null) {
          lock(_gates) {
            if(_gates.Count==0 || (ret=(_gates[0] as MsGUdp))==null) {
              ret=new MsGUdp();
            }
          }
        }
      }
      #endregion static

      #region instance
      private UdpClient _udp;

      private MsGUdp() {
        try {
          _udp=new UdpClient(1883);
          _udp.EnableBroadcast=true;
          _udp.BeginReceive(new AsyncCallback(ReceiveCallback), null);
          _gates.Insert(0, this);
        }
        catch(Exception ex) {
          Log.Error("MsGUdp.ctor() {0}", ex.Message);
        }
      }
      private void ReceiveCallback(IAsyncResult ar) {
        IPEndPoint re=new IPEndPoint(IPAddress.Any, 0);
        Byte[] buf=null;
        try {
          buf = _udp.EndReceive(ar, ref re);
          if(!_myIps.Any(z => re.Address==z)) {
            ParseInPacket(buf, re.Address.GetAddressBytes());
          }
        }
        catch(Exception ex) {
          Log.Error("ReceiveCallback({0}, {1}) - {2}", re, BitConverter.ToString(buf), ex.ToString());
        }
        if(_udp!=null) {
          _udp.BeginReceive(new AsyncCallback(ReceiveCallback), null);
        }
      }
      private void ParseInPacket(byte[] buf, byte[] addr) {
        Topic devR=Topic.root.Get("/dev");

        var msgTyp=(MsMessageType)(buf[0]>1?buf[1]:buf[3]);
        if(msgTyp==MsMessageType.GWINFO || msgTyp==MsMessageType.ADVERTISE) {
          return;
        } else if(msgTyp==MsMessageType.SEARCHGW) {
          PrintPacket(null, new MsSearchGW(buf) { Addr=addr }, buf);
          this.Send(new MsGwInfo(gwIdx) { Addr=IPAddress.Broadcast.GetAddressBytes() });
        } else if(msgTyp==MsMessageType.CONNECT) {
          var msg=new MsConnect(buf) { Addr=addr };
          DVar<MsDevice> dev=devR.Get<MsDevice>(msg.ClientId);
          if(!msg.CleanSession && (dev.value==null || dev.value.Addr!=msg.Addr || dev.value.state==State.Disconnected || dev.value.state==State.Lost)) {
            PrintPacket(dev, msg, buf);
            Send(new MsConnack(MsReturnCode.InvalidTopicId) { Addr=msg.Addr });
            return;
          }
          if(dev.value==null) {
            dev.value=new MsDevice();
          }
          dev.value._gate=this;
          if(dev.value.Addr==null || !msg.Addr.SequenceEqual(dev.value.Addr)) {
            dev.value.Addr=msg.Addr;
          }
          PrintPacket(dev, msg, buf);
          Thread.Sleep(0);
          dev.value.Connect(msg);
          dev.value.via="UDP";
        } else { // msgType==Connect
          MsDevice dev=devR.children.Select(z => z.GetValue() as MsDevice).FirstOrDefault(z => z!=null && z.Addr!=null && addr.SequenceEqual(z.Addr) && z._gate==this);
          if(dev!=null && dev.state!=State.Disconnected && dev.state!=State.Lost) {
            dev.ParseInPacket(buf);
          } else {
            if(dev==null || dev.Owner==null) {
              Log.Debug("unknown device: {0}:{1}", BitConverter.ToString(addr), BitConverter.ToString(buf));
            } else {
              Log.Debug("inactive device: [{0}] {1}:{2}", dev.Owner.name, BitConverter.ToString(addr), BitConverter.ToString(buf));
            }
            Send(new MsDisconnect() { Addr=addr });
          }
        }
      }

      public void Send(MsMessage msg) {
        if(_udp==null || msg==null || msg.Addr==null || msg.Addr.Length!=4) {
          return;
        }

        byte[] buf=msg.GetBytes();

        if(IPAddress.Broadcast.GetAddressBytes().SequenceEqual(msg.Addr)) {
          foreach(var bc in _bcIps) {
            _udp.Send(buf, buf.Length, new IPEndPoint(bc, 1883));
          }
        } else {
          _udp.Send(buf, buf.Length, new IPEndPoint(new IPAddress(msg.Addr), 1883));
        }
        if(_verbose.value) {
          Log.Debug("s {0:X2}:{1}:{2} \t{3}", gwIdx, BitConverter.ToString(msg.Addr), BitConverter.ToString(buf), msg.ToString());
        }
      }
      public byte gwIdx { get { return 0; } }

      #endregion instance

    }
  }
}