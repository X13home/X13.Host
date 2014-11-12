#region license
//Copyright (c) 2011-2014 <comparator@gmx.de>; Wassili Hense

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
  [ExportMetadata("name", "MQTT-SN.udp")]
  public class MQTTSUdp : IPlugModul {

    public void Init() {
      Topic old;
      if(Topic.root.Exist("/local/cfg/MQTTS.udp/enable", out old) && old.valueType==typeof(bool)) {
        (old as DVar<bool>).value=false;
      }

      Topic.root.Subscribe("/etc/MQTT-SN/#", Dummy);
      Topic.root.Subscribe("/etc/declarers/dev/#", Dummy);
    }
    public void Start() {
      using(var sr=new StreamReader(Assembly.GetExecutingAssembly().GetManifestResourceStream("X13.Periphery.MQTTSUdp.xst"))) {
        Topic.Import(sr, null);
      }
      TWIDriver.Load();
      MsDevice.MsGUdp.Open();
    }

    void Dummy(Topic src, TopicChanged arg) {
    }

    public void Stop() {
      Topic.root.Unsubscribe("/etc/MQTT-SN/#", Dummy);
      Topic.root.Unsubscribe("/etc/declarers/#", Dummy);
      //TODO: Close
    }
  }

  public partial class MsDevice : ITopicOwned {
    internal class MsGUdp : IMsGate {

      #region static
      private static byte[][] _myIps;
      private static IPAddress[] _bcIps;

      public static void Open() {
        _myIps=Dns.GetHostEntry(Dns.GetHostName()).AddressList.Where(z => z.AddressFamily==AddressFamily.InterNetwork).Union(new IPAddress[] { IPAddress.Loopback }).Select(z => z.GetAddressBytes()).ToArray();
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
      private Timer _advTick;

      private MsGUdp() {
        try {
          _udp=new UdpClient(1883);
          _udp.EnableBroadcast=true;
          _udp.BeginReceive(new AsyncCallback(ReceiveCallback), null);
          _gates.Insert(0, this);
          _advTick=new Timer(SendAdv, null, 4500, 900000);
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
          byte[] addr=re.Address.GetAddressBytes();
          if(!_myIps.Any(z => addr.SequenceEqual(z))) {
            if(buf.Length>1) {
              MsDevice.ProcessInPacket(this, addr, buf, 0, buf.Length);
            }
          }
        }
        catch(Exception ex) {
          Log.Error("ReceiveCallback({0}, {1}) - {2}", re, BitConverter.ToString(buf), ex.ToString());
        }
        if(_udp!=null) {
          _udp.BeginReceive(new AsyncCallback(ReceiveCallback), null);
        }
      }
      private void SendAdv(object o) {
        SendGw(null, new MsAdvertise(0, 900));
      }
      public void SendGw(MsDevice dev, MsMessage msg) {
        if(_udp==null || msg==null) {
          return;
        }

        byte[] buf=msg.GetBytes();
        IPAddress addr;
        if(dev==null) {
          addr=IPAddress.Broadcast;
          foreach(var bc in _bcIps) {
            _udp.Send(buf, buf.Length, new IPEndPoint(bc, 1883));
          }
        } else if(dev.Addr!=null && dev.Addr.Length==4) {
          addr=new IPAddress(dev.Addr);
          _udp.Send(buf, buf.Length, new IPEndPoint(addr, 1883));
        } else {
          return;
        }
        if(_verbose.value) {
          Log.Debug("s  {0}: {1}  {2}", addr, BitConverter.ToString(buf), msg.ToString());
        }
      }
      public string name { get { return "UDP"; } }
      public string Addr2If(byte[] addr) {
        return (new IPAddress(addr)).ToString();
      }
      public byte gwIdx { get { return 0; } }
      #endregion instance
    }
  }
}