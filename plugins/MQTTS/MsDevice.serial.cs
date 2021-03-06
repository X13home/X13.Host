﻿#region license
//Copyright (c) 2011-2014 <comparator@gmx.de>; Wassili Hense

//This file is part of the X13.Home project.
//https://github.com/X13home

//BSD License
//See LICENSE.txt file for license details.
#endregion license

using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.IO;
using System.IO.Ports;
using System.Linq;
using System.Reflection;
using System.Threading;


namespace X13.Periphery {
  [Export(typeof(IPlugModul))]
  [ExportMetadata("priority", 5)]
  [ExportMetadata("name", "MQTTS.Gate")]
  public class MQTTSGate : IPlugModul {

    public void Init() {
      Topic.root.Subscribe("/etc/MQTTS/#", Dummy);
      Topic.root.Subscribe("/etc/declarers/dev/#", Dummy);
    }
    public void Start() {
      using(var sr=new StreamReader(Assembly.GetExecutingAssembly().GetManifestResourceStream("X13.Periphery.MQTTSRf.xst"))) {
        Topic.Import(sr, null);
      }
      MsDevice.MsGSerial.Open();
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

    internal class MsGSerial : IMsGate {

      #region static
      private static AutoResetEvent _startScan;
      private static bool _scanAllPorts;
      private static int _scanBusy;

      static MsGSerial() {
        _startScan=new AutoResetEvent(false);
        _scanBusy=0;
        ThreadPool.RegisterWaitForSingleObject(_startScan, ScanPorts, null, 45000, false);
      }

      public static void Open() {
        Log.Info("Search for MQTTS.serial devices");
        Topic dev=Topic.root.Get("/dev");
        dev.Get<string>("_declarer").value="DevFolder";
        _scanAllPorts=true;
        ScanPorts(null, false);
      }
      public static void Rescan() {
        if(_scanBusy==0) {
          _startScan.Set();
        }
      }

      private static void ScanPorts(object o, bool b) {
        if(Interlocked.Exchange(ref _scanBusy, 1)!=0) {
          return;
        }

        byte[] buf=new byte[64];
        byte[] searchGW=new byte[] { 0x03, 0x01, 0x00 };
        byte addr=0xFF;
        bool escChar;
        int cnt=0, tryCnt;
        SerialPort port=null;

        byte[] curAddr;

        List<string> pns=new List<string>();
        Topic dev=Topic.root.Get("/dev");
        lock(dev) {
          var ifs=dev.children.Where(z => z.valueType==typeof(MsDevice)).Cast<DVar<MsDevice>>().Where(z => z.value!=null).Select(z => z.value).ToArray();
          foreach(var devSer in ifs) {
            cnt++;
            if(devSer.state==State.Connected) {
              continue;
            }
            if(string.IsNullOrWhiteSpace(devSer.via)) {
              _scanAllPorts=true;
              break;
            }
            string via=devSer.via;
            if(via!="offline" && !pns.Exists(z => string.Equals(z, via, StringComparison.InvariantCultureIgnoreCase))) {
              pns.Add(via);
            }
          }
        }
        if(_scanAllPorts || cnt==0) {
          _scanAllPorts=false;
          pns.Clear();
          pns.AddRange(SerialPort.GetPortNames());
        } else {
          pns=pns.Intersect(SerialPort.GetPortNames()).ToList();
        }
        for(int i=0; i<pns.Count; i++) {
          try {
            port=new SerialPort(pns[i], 38400, Parity.None, 8, StopBits.One);
            port.ReadBufferSize=300;
            port.WriteBufferSize=300;
            port.Open();
            port.DiscardInBuffer();
            SendRaw(port, 0, searchGW); // Send SearchGW
            Thread.Sleep(70);
            cnt=-1;
            tryCnt=5;
            escChar=false;
            curAddr=null;
            while(--tryCnt>0) {
              if(GetPacket(port, ref addr, buf, ref cnt, ref escChar)) {
                if(_verbose.value)
                  Log.Debug("{0} r {1:X2}:{2}", pns[i], addr, BitConverter.ToString(buf, 0, cnt));
                if(cnt==3 && buf[1]==0x02) {   // Received GWInfo
                  curAddr=new byte[] { buf[2] }; // addr
                  break;
                }
                SendRaw(port, 0, searchGW); // Send SearchGW
              }
              Thread.Sleep(50);
            }
            if(curAddr==null) {
              port.Close();
              continue;
            }
            lock(_gates) {
              var gw=new MsGSerial(port, curAddr[0]);
              _gates.Add(gw);
            }
          }
          catch(Exception ex) {
            if(_verbose.value) {
              Log.Debug("MQTTS.Serial search on {0} - {1}", pns[i], ex.Message);
            }
            try {
              if(port!=null) {
                if(port!=null && port.IsOpen) {
                  port.Close();
                }
                port.Dispose();
              }
            }
            catch(Exception) {
            }
          }
          port=null;
        }
        _scanBusy=0;
      }
      private static bool GetPacket(SerialPort port, ref byte addr, byte[] buf, ref int cnt, ref bool escChar) {
        int b;
        if(port==null || !port.IsOpen) {
          return false;
        }
        while(port.BytesToRead>0) {
          b=port.ReadByte();
          if(b<0) {
            break;
          }
          if(b==0xC0) {
            escChar=false;
            if(cnt>1 && cnt==buf[0]) {
              return true;
            } else {
              if(cnt>1) {
                Log.Warning("size mismatch: {0}", cnt>0?BitConverter.ToString(buf, 0, cnt):"[0]");
              }
              cnt=-1;
            }
            continue;
          }
          if(b==0xDB) {
            escChar=true;
            continue;
          }
          if(escChar) {
            b^=0x20;
            escChar=false;
          }
          if(cnt==0x100) {
            cnt=-1;
            continue;
          }
          if(cnt>=0) {
            buf[cnt++]=(byte)b;
          } else {
            addr=(byte)b;
            cnt++;
          }
        }
        return false;
      }
      private static void SendRaw(SerialPort port, byte addr, byte[] buf) {
        if(port==null || !port.IsOpen) {
          return;
        }
        int i;
        byte[] b=new byte[1];
        b[0]=0xC0;
        port.Write(b, 0, 1);
        if(addr==0xC0 || addr==0xDB) {
          b[0]=0xDB;
          port.Write(b, 0, 1);
          b[0]=(byte)(addr ^ 0x20);
        } else {
          b[0]=addr;
        }
        port.Write(b, 0, 1);
        for(i=0; i<buf.Length; i++) {
          if(buf[i]==0xC0 || buf[i]==0xDB) {
            b[0]=0xDB;
            port.Write(b, 0, 1);
            b[0]=(byte)(buf[i] ^ 0x20);
          } else {
            b[0]=buf[i];
          }
          port.Write(b, 0, 1);
        }
        b[0]=0xC0;
        port.Write(b, 0, 1);
        if(_verbose.value) {
          Log.Debug("{0} s {1:X2}:{2}", port.PortName, addr, BitConverter.ToString(buf, 0, buf.Length));
        }
      }
      private static void SendRaw(MsGSerial g, MsMessage msg) {
        if(g==null || g._port==null || !g._port.IsOpen || msg==null || msg.Addr==null || msg.Addr.Length!=1) {
          return;
        }
        int i;
        byte[] b=new byte[1];
        b[0]=0xC0;
        g._port.Write(b, 0, 1);
        if(msg.Addr[0]==0xC0 || msg.Addr[0]==0xDB) {
          b[0]=0xDB;
          g._port.Write(b, 0, 1);
          b[0]=(byte)(msg.Addr[0] ^ 0x20);
        } else {
          b[0]=msg.Addr[0];
        }
        g._port.Write(b, 0, 1);
        byte[] buf=msg.GetBytes();
        for(i=0; i<buf.Length; i++) {
          if(buf[i]==0xC0 || buf[i]==0xDB) {
            b[0]=0xDB;
            g._port.Write(b, 0, 1);
            b[0]=(byte)(buf[i] ^ 0x20);
          } else {
            b[0]=buf[i];
          }
          g._port.Write(b, 0, 1);
        }
        b[0]=0xC0;
        g._port.Write(b, 0, 1);
        if(_verbose.value) {
          Log.Debug("s {0:X2}:{1:X2}:{2} \t{3}", g.gwIdx, msg.Addr[0], BitConverter.ToString(buf), msg.ToString());
        }
      }
      #endregion static

      #region instance
      private SerialPort _port;
      private byte _gwAddr;
      private Queue<MsMessage> _sendQueue;
      private DVar<MsDevice> _gwTopic;
      private DateTime _advTick;

      public MsGSerial(SerialPort port, byte addr) {
        _port=port;
        _gwAddr=addr;
        byte i=1;
        foreach(var g in _gates) {
          i=g.gwIdx>=i?(byte)(g.gwIdx+1):i;
        }
        gwIdx=i;
        _sendQueue=new Queue<MsMessage>();
        ThreadPool.QueueUserWorkItem(CommThread);
        Send(new MsDisconnect() { Addr=new byte[] { addr } });
        _advTick=DateTime.Now.AddSeconds(2.6);
      }
      public void Send(MsMessage msg) {
        lock(_sendQueue) {
          _sendQueue.Enqueue(msg);
        }
      }
      public byte gwIdx { get; private set; }

      private void CommThread(object o) {
        byte[] buf=new byte[256];
        bool escChar=false;
        int cnt=-1;
        byte addr=0xFF;
        MsMessage msg;
        try {
          while(_port!=null && _port.IsOpen) {
            if(GetPacket(_port, ref addr, buf, ref cnt, ref escChar)) {
              byte[] rezBuf=new byte[cnt];
              byte[] rezAddr=new byte[] { addr };
              Array.Copy(buf, rezBuf, cnt);
              cnt=-1;
              ParseInPacket(rezAddr, rezBuf);
              continue;
            }
            msg=null;
            lock(_sendQueue) {
              if(_sendQueue.Count>0) {
                msg=_sendQueue.Dequeue();
              }
            }
            SendRaw(this, msg);

            if(msg==null && _advTick<DateTime.Now) {
              SendRaw(this, new MsAdvertise(gwIdx, 900) { Addr=new byte[] { 0 } });
              _advTick=DateTime.Now.AddMinutes(15);
            }
            Thread.Sleep(15);
            if(msg==null && _gwTopic!=null && _gwTopic.value!=null && (_gwTopic.value.state==State.Disconnected || _gwTopic.value.state==State.Lost)) {
              _gwTopic=null;
              ThreadPool.QueueUserWorkItem(obj => {
                Thread.Sleep(500);
                if(_port!=null && _port.IsOpen) {
                  _port.Close();
                }
                Thread.Sleep(1000);
                _startScan.Set();
              });
            }
          }
        }
        catch(IOException) {
        }
        catch(Exception ex) {
          Log.Error("MsGSerial({0}).CommThread() - {1}", gwIdx, ex.ToString());
        }
        if(_verbose.value) {
          Log.Debug("MsGSerial({0}).CommThread - exit", gwIdx);
        }
        this.Dispose();
      }
      private void ParseInPacket(byte[] addr, byte[] buf) {
        if(addr[0]==0) {
          Log.Warning("Packet with broadcast address via {0}:{1:X2}:{2}", _port.PortName, BitConverter.ToString(addr), BitConverter.ToString(buf));
          return;
        }

        Topic devR=Topic.root.Get("/dev");
        var msgTyp=(MsMessageType)(buf[0]>1?buf[1]:buf[3]);
        if(msgTyp==MsMessageType.SEARCHGW) {
          PrintPacket(null, new MsSearchGW(buf) { Addr=addr }, buf);
          this.Send(new MsGwInfo(_gwAddr) { Addr=new byte[] { 0 } });
        } else if(msgTyp==MsMessageType.CONNECT) {
          var msg=new MsConnect(buf) { Addr=addr };
          if(addr[0]==_gwAddr) {
            _advTick=DateTime.Now.AddSeconds(2.6);  // Send Advertise in 2.6 sec.
          }
          if(addr[0]==0xFF) {
            PrintPacket(null, msg, buf);
            Send(new MsConnack(MsReturnCode.Accepted) { Addr=msg.Addr });
            byte[] nAddr=new byte[1];
            var r=new Random(DateTime.Now.Millisecond);
            do {
              nAddr[0]=(byte)(8+r.Next(0xF6));  //0x08 .. 0xFE
            } while(devR.children.Select(z => z.GetValue() as MsDevice).Any(z => z!=null && nAddr.SequenceEqual(z.Addr)));
            Log.Info("{0} new addr={1}", msg.ClientId, BitConverter.ToString(nAddr));
            var pm=new MsPublish(null, PredefinedTopics[".cfg/XD_DeviceAddr"], QoS.AtLeastOnce) { Addr=msg.Addr, MessageId=1, Data=nAddr };
            Send(pm);
          } else { // msg.Addr!=0xFF
            DVar<MsDevice> dev=devR.Get<MsDevice>(msg.ClientId); // 
            if(!msg.CleanSession && (dev.value==null || !dev.value.Addr.SequenceEqual(msg.Addr) || dev.value.state==State.Disconnected || dev.value.state==State.Lost)) {
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
            if(msg.Addr[0]==_gwAddr) {
              dev.value.via=_port.PortName;
              _gwTopic=dev;
            } else {
              dev.value.via= _gwTopic==null?string.Empty:_gwTopic.name;
            }
          }  // msg.Addr!=0xFF
        } else { // msgType==Connect
          MsDevice dev=devR.children.Select(z => z.GetValue() as MsDevice).FirstOrDefault(z => z!=null && z.Addr!=null && addr.SequenceEqual(z.Addr) && z._gate==this);
          if(dev!=null && dev.state!=State.Disconnected && dev.state!=State.Lost) {
            dev.ParseInPacket(buf);
          } else {
            if(dev==null || dev.Owner==null) {
              Log.Debug("unknown device: [{0:X2}:{1}]", addr[0], BitConverter.ToString(buf));
            } else {
              Log.Debug("inactive device: [{0}:{1}]", dev.Owner.name, BitConverter.ToString(buf));
            }
            Send(new MsDisconnect() { Addr=addr });
          }
        }
      }
      private void Dispose() {
        try {
          if(_port!=null && _port.IsOpen) {
            _port.Close();
          }
        }
        catch(Exception) {
        }
        _port=null;
        _gates.Remove(this);
        byte[] gwAddr=new byte[] { _gwAddr };
        Topic dev=Topic.root.Get("/dev");
        lock(dev) {
          var ifs=dev.children.Where(z => z.valueType==typeof(MsDevice)).Cast<DVar<MsDevice>>().Where(z => z.value!=null && z.value._gate==this).Select(z => z.value).ToArray();
          foreach(var t in ifs) {
            t._gate=null;
            t.Disconnect();
          }
        }
      }
      #endregion instance
    }
  }
}