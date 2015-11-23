#region license
//Copyright (c) 2011-2014 <comparator@gmx.de>; Wassili Hense

//This file is part of the X13.Home project.
//https://github.com/X13home

//BSD License
//See LICENSE.txt file for license details.
#endregion license

#define UART_RAW_MQTTSN

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
  [ExportMetadata("name", "MQTT-SN.Serial")]
  public class MQTTSGate : IPlugModul {
    public void Init() {
      Topic old;
      if(Topic.root.Exist("/local/cfg/MQTTS.Gate/enable", out old) && old.valueType==typeof(bool)) {
        (old as DVar<bool>).value=false;
      }

      Topic.root.Subscribe("/etc/MQTT-SN/#", Dummy);
      Topic.root.Subscribe("/etc/declarers/dev/#", Dummy);
    }
    public void Start() {
      using(var sr=new StreamReader(Assembly.GetExecutingAssembly().GetManifestResourceStream("X13.Periphery.MQTTSRf.xst"))) {
        Topic.Import(sr, null);
      }
      TWIDriver.Load();
      MsDevice.MsGSerial.Open();
    }

    void Dummy(Topic src, TopicChanged arg) {
    }

    public void Stop() {
      Topic.root.Unsubscribe("/etc/MQTT-SN/#", Dummy);
      Topic.root.Unsubscribe("/etc/declarers/#", Dummy);

      MsDevice.MsGSerial.Close();
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
        Log.Info("Search for MQTT-SN.serial devices");
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
      public static void Close() {
        if(_gates!=null) {
          lock(_gates) {
            var gates=_gates.Select(z => z as MsGSerial).Where(z => z!=null).ToArray();
            for(int i=0; i<gates.Length; i++) {
              gates[i].Stop();
            }
          }
        }

      }

      private static void ScanPorts(object o, bool b) {
        if(Interlocked.Exchange(ref _scanBusy, 1)!=0) {
          return;
        }

        byte[] buf=new byte[64];
        byte[] tmpBuf=new byte[64];
        byte[] disconnectAll=(new MsDisconnect()).GetBytes();
        bool escChar;
        int cnt=0, tryCnt;
        SerialPort port=null;
        int length;
        bool found;

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
        Topic tmp;
        if(Topic.root.Exist("/local/cfg/MQTT-SN.Serial/whitelist", out tmp)) {
          var whl=tmp as DVar<string>;
          if(whl!=null && !string.IsNullOrEmpty(whl.value)) {
            var wps=whl.value.Split(';', ',');
            if(wps!=null && wps.Length>0) {
              pns=pns.Intersect(wps).ToList();
            }
          }
        }
        if(Topic.root.Exist("/local/cfg/MQTT-SN.Serial/blacklist", out tmp)) {
          var bll=tmp as DVar<string>;
          if(bll!=null && !string.IsNullOrEmpty(bll.value)) {
            var bps=bll.value.Split(';', ',');
            if(bps!=null && bps.Length>0) {
              pns=pns.Except(bps).ToList();
            }
          }
        }
        for(int i=0; i<pns.Count; i++) {
          if(_gates.Exists(z => z.name==pns[i])) {
            continue;
          }

          try {
            port=new SerialPort(pns[i], 38400, Parity.None, 8, StopBits.One);
            port.ReadBufferSize=300;
            port.WriteBufferSize=300;
            port.Open();
            port.DiscardInBuffer();
            SendRaw(port, disconnectAll, tmpBuf); // Send Disconnect
            Thread.Sleep(500);
            cnt=-1;
            tryCnt=30;
            escChar=false;
            length=-1;
            found=false;
            while(--tryCnt>0) {
              if(GetPacket(port, ref length, buf, ref cnt, ref escChar)) {
                var msgTyp=(MsMessageType)(buf[0]>1?buf[1]:buf[3]);
                if(msgTyp==MsMessageType.SEARCHGW || msgTyp==MsMessageType.DHCP_REQ) {   // Received Ack
                  found=true;
                  MsGSerial gw;
                  lock(_gates) {
                    gw=new MsGSerial(port);
                    _gates.Add(gw);
                  }
                  MsDevice.ProcessInPacket(gw, gw._gateAddr, buf, 0, cnt);
                  break;
                } else if(_verbose.value) {
                  Log.Debug("r {0}: {1}  {2}", pns[i], BitConverter.ToString(buf, 0, cnt), msgTyp);
                }
                SendRaw(port, disconnectAll, tmpBuf); // Send Disconnect
              }
              Thread.Sleep(90);
            }
            if(!found) {
              port.Close();
              continue;
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
      private static bool GetPacket(SerialPort port, ref int length, byte[] buf, ref int cnt, ref bool escChar) {
        int b;
        if(port==null || !port.IsOpen) {
          return false;
        }
        while(port.BytesToRead>0) {
          b=port.ReadByte();
          if(b<0) {
            break;
          }
#if !UART_RAW_MQTTSN
          if(b==0xC0) {
            escChar=false;
            if(cnt>1 && cnt==length) {
              return true;
            } else {
              if(cnt>1) {
                Log.Warning("r  {0}: {1}  size mismatch: {2}/{3}", port.PortName, BitConverter.ToString(buf, 0, cnt), cnt, length);
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
#endif
          if(cnt>=0) {
            buf[cnt++]=(byte)b;
#if UART_RAW_MQTTSN
            if(cnt==length) {
              return true;
            }
#endif
          } else {
#if UART_RAW_MQTTSN
            if(b<2 && b>MsMessage.MSG_MAX_LENGTH) {
              if(_verbose.value) {
                Log.Warning("r 0x{0:X2} wrong length of the packet: {1}", port.PortName, b);
              }
              cnt=-1;
              port.DiscardInBuffer();
              return false;
            }
#endif
            length=b;
            cnt++;
          }
        }
        return false;
      }
      private static void SendRaw(SerialPort port, byte[] buf, byte[] tmp) {
        if(port==null || !port.IsOpen) {
          return;
        }
        int i, j=0;
        byte b;
        b=(byte)buf.Length;
#if UART_RAW_MQTTSN
        tmp[j++]=b;
        for(i=0; i<buf.Length; i++) {
          tmp[j++]=buf[i];
        }
#else
        tmp[j++]=0xC0;
        if(b==0xC0 || b==0xDB) {
          tmp[j++]=0xDB;
          tmp[j++]=(byte)(b ^ 0x20);
        } else {
          tmp[j++]=b;
        }
        for(i=0; i<buf.Length; i++) {
          if(buf[i]==0xC0 || buf[i]==0xDB) {
            tmp[j++]=0xDB;
            tmp[j++]=(byte)(buf[i] ^ 0x20);
          } else {
            tmp[j++]=buf[i];
          }
        }
        tmp[j++]=0xC0;
#endif
        port.Write(tmp, 0, j);
        if(_verbose.value) {
          Log.Debug("s  {0}: {1}  {2}", port.PortName, BitConverter.ToString(buf, 0, buf.Length), MsMessage.Parse(buf, 0, buf.Length));
        }
      }
      private static void SendRaw(MsGSerial g, MsMessage msg, byte[] tmp) {
        if(g==null || g._port==null || !g._port.IsOpen || msg==null) {
          return;
        }
        byte[] buf=msg.GetBytes();
        int i, j=0;
        byte b;
        b=(byte)buf.Length;
#if UART_RAW_MQTTSN
        tmp[j++]=b;
        for(i=0; i<buf.Length; i++) {
          tmp[j++]=buf[i];
        }
#else
        tmp[j++]=0xC0;
        if(b==0xC0 || b==0xDB) {
          tmp[j++]=0xDB;
          tmp[j++]=(byte)(b ^ 0x20);
        } else {
          tmp[j++]=b;
        }
        for(i=0; i<buf.Length; i++) {
          if(buf[i]==0xC0 || buf[i]==0xDB) {
            tmp[j++]=0xDB;
            tmp[j++]=(byte)(buf[i] ^ 0x20);
          } else {
            tmp[j++]=buf[i];
          }
        }
        tmp[j++]=0xC0;
#endif
        g._port.Write(tmp, 0, j);

        if(_verbose.value) {
          Log.Debug("s {0}: {1}  {2}", g._port.PortName, BitConverter.ToString(buf), msg.ToString());
        }
      }
      #endregion static

      #region instance
      private SerialPort _port;
      private Queue<MsMessage> _sendQueue;
      private DVar<MsDevice> _gwTopic;
      private byte[] _sndBuf;
      private byte[] _gateAddr;
      private DateTime _advTick;
      private List<MsDevice> _nodes;

      public MsGSerial(SerialPort port) {
        _nodes=new List<MsDevice>();
        _port=port;
        byte i=1;
        foreach(var g in _gates) {
          i=g.gwIdx>=i?(byte)(g.gwIdx+1):i;
        }
        gwIdx=i;
        int tmpAddr;
        if(!int.TryParse(new string(_port.PortName.Where(z => char.IsDigit(z)).ToArray()), out tmpAddr) || tmpAddr==0 || tmpAddr>254) {
          tmpAddr=(byte)(new Random()).Next(1, 254);
        }
        _gateAddr=new byte[] { gwIdx, (byte)tmpAddr };
        _sendQueue=new Queue<MsMessage>();
        _sndBuf=new byte[384];
        _advTick=DateTime.Now.AddSeconds(31.3);
        ThreadPool.QueueUserWorkItem(CommThread);
      }
      public void SendGw(byte[] addr, MsMessage msg) {
        msg.GetBytes();
        lock(_sendQueue) {
          _sendQueue.Enqueue(msg);
        }
      }
      public void SendGw(MsDevice dev, MsMessage msg) {
        msg.GetBytes();
        lock(_sendQueue) {
          _sendQueue.Enqueue(msg);
        }
      }
      public byte gwIdx { get; private set; }
      public string name { get { return _port!=null?_port.PortName:string.Empty; } }
      public string Addr2If(byte[] addr) {
        return _port!=null?_port.PortName:string.Empty;
      }
      public void AddNode(MsDevice dev) {
        _nodes.Add(dev);
      }
      public void RemoveNode(MsDevice dev) {
        if(_nodes!=null) {
          _nodes.Remove(dev);
        }
      }
      public void Stop() {
        try {
          if(_port!=null && _port.IsOpen) {
            var nodes=_nodes.ToArray();
            for(int i=0; i<nodes.Length; i++) {
              nodes[i].Stop();
            }
            _port.Close();
            _port=null;
          }
        }
        catch(Exception ex) {
          Log.Error("MsGSerial.Close({0}) - {1}", gwIdx, ex.ToString());
        }
      }

      private void CommThread(object o) {
        byte[] buf=new byte[256];
        bool escChar=false;
        int cnt=-1;
        int len=-1;
        MsMessage msg;
        DateTime busyTime=DateTime.Now;
        try {
          while(_port!=null && _port.IsOpen) {
            if(GetPacket(_port, ref len, buf, ref cnt, ref escChar)) {
              if(len==5 && buf[1]==(byte)MsMessageType.SUBSCRIBE) {
                _advTick=DateTime.Now.AddMilliseconds(100);   // Send Advertise
              }
              MsDevice.ProcessInPacket(this, _gateAddr, buf, 0, len);
              cnt=-1;
              msg=null;
              continue;
            }
            msg=null;
            if(busyTime>DateTime.Now) {
              Thread.Sleep(0);
              continue;
            }
            lock(_sendQueue) {
              if(_sendQueue.Count>0) {
                msg=_sendQueue.Dequeue();
              }
            }
            if(msg!=null) {
              SendRaw(this, msg, _sndBuf);
              busyTime=DateTime.Now.AddMilliseconds(msg.IsRequest?20:5);
              continue;
            }
            if(_gwTopic!=null && _gwTopic.value!=null && (_gwTopic.value.state==State.Disconnected || _gwTopic.value.state==State.Lost)) {
              _gwTopic=null;
              Thread.Sleep(500);
              this.Dispose();
              Thread.Sleep(1000);
              _startScan.Set();
              return;
            }
            if(_advTick<DateTime.Now) {
              SendRaw(this, new MsAdvertise(gwIdx, 900), _sndBuf);
              _advTick=DateTime.Now.AddMinutes(15);
            }
            Thread.Sleep(15);
          }
        }
        catch(IOException ex) {
          if(_verbose.value) {
            Log.Error("MsGSerial({0}).CommThread() - {1}", gwIdx, ex.ToString());
          }
        }
        catch(Exception ex) {
          Log.Error("MsGSerial({0}).CommThread() - {1}", gwIdx, ex.ToString());
        }
        if(_verbose.value) {
          Log.Debug("MsGSerial({0}).CommThread - exit", gwIdx);
        }
        this.Dispose();
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
      }
      #endregion instance
    }
  }
}
