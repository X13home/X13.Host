#region license
//Copyright (c) 2011-2013 <comparator@gmx.de>; Wassili Hense

//This file is part of the X13.Home project.
//https://github.com/X13home

//BSD License
//See LICENSE.txt file for license details.
#endregion license

using System;
using System.Collections.Generic;
using System.IO.Ports;
using System.Linq;
using System.Text;
using System.Threading;
using X13.PLC;

namespace X13.MQTT {
  [Newtonsoft.Json.JsonObject(Newtonsoft.Json.MemberSerialization.OptIn)]
  public class MsDevSer : MsDevice {
    private static DVar<bool> _verbose;

    static MsDevSer() {
      _verbose=Topic.root.Get<bool>("/system/MQTTS/verboseLog");
      _verbose.value=true;
    }
    public static void Open() {
      Topic dev=Topic.root.Get("/dev");

      ThreadPool.QueueUserWorkItem(ScanPorts);
    }
    private static void ScanPorts(object o) {
      byte[] buf=new byte[64];
      byte addr;
      bool escChar;
      int cnt;
      SerialPort port=null;

      byte[] curAddr;

      string[] usedPorts;
      List<MsDevSer> _ifs=new List<MsDevSer>();
      Topic dev=Topic.root.Get("/dev");
      lock(dev) {
        _ifs.AddRange(dev.children.Where(z => z.valueType==typeof(MsDevSer)).Cast<DVar<MsDevSer>>().Select(z => z.value));
        usedPorts=_ifs.Where(z => z!=null && !string.IsNullOrEmpty(z.portName) && z.state==MsDeviceState.Connected).Select(z => z.portName).ToArray();
      }
      string[] pns=SerialPort.GetPortNames();
      for(int i=0; i<pns.Length; i++) {
        if(usedPorts.Any(z => string.Equals(z, pns[i]))) {
          continue;
        }
        try {
          port=new SerialPort(pns[i], 38400, Parity.None, 8, StopBits.One);
          port.ReadBufferSize=300;
          port.WriteBufferSize=300;
          port.Open();
          port.DiscardInBuffer();
          SendRaw(port, 0, new byte[] {0x03, 0x01, 0x00}); // Send SearchGW
          Thread.Sleep(50);
          cnt=-1;
          escChar=false;
          curAddr=null;
          while(GetPacket(port, out addr, buf, ref cnt, ref escChar)) {
            if(_verbose)
              Log.Debug("{0} r {1:X2}:{2}", pns[i], addr, BitConverter.ToString(buf, 0, cnt));
            if(cnt==3 && buf[1]==0x02) {   // Received GWInfo
              curAddr=new byte[] { buf[2] };
              break;
            }
          }
          if(curAddr==null) {
            port.Close();
            continue;
          }
          SendRaw(port, curAddr[0], new byte[] { 0x02, 0x18 }); // Send Disconnect
          Thread.Sleep(500);
          cnt=-1;
          escChar=false;
          while(GetPacket(port, out addr, buf, ref cnt, ref escChar)) {
            if(_verbose)
              Log.Debug("{0} r {1:X2}:{2}", pns[i], addr, BitConverter.ToString(buf, 0, cnt));
            if(addr==curAddr[0] && cnt>1 && buf[1]==0x04) {   // Received Connect
              var conn=new MsConnect(buf.Take(cnt).ToArray());
              conn.Addr=curAddr;
              var ds=dev.Get<MsDevSer>(conn.ClientId);
              if(ds.value==null) {
                ds.value=new MsDevSer();
              }
              if(ds.value._port!=null && ds.value._port.IsOpen) {
                ds.value._port.Close();
              }
              ds.value._port=port;
              ds.value.Connect(conn);
              curAddr=null;
              break;
            }
          }
          if(curAddr!=null) {
            port.Close();
          }
        }
        catch(Exception) {
          if(port!=null && port.IsOpen) {
            port.Close();
          }
        }
        port=null;
      }
    }
    private static bool GetPacket(SerialPort port, out byte addr, byte[] buf, ref int cnt, ref bool escChar) {
      int b;
      addr=0xFF;
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
            Log.Warning("size mismatch: {0}", BitConverter.ToString(buf, 0, cnt));
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
      if(_verbose) {
        Log.Debug("{0} s {1:X2}:{2}", port.PortName, addr, BitConverter.ToString(buf, 0, buf.Length));
      }
    }

    private SerialPort _port;

    internal string portName {
      get { return Owner!=null?Owner.Get<string>("portName").value:string.Empty; }
      set {
        if(Owner!=null) {
          var t=Owner.Get<string>("portName");
          t.saved=true;
          t.value=value;
        }
      }
    }

    internal override void SendIF(MsMessage msg) {
      SendRaw(_port, msg.Addr[0], msg.GetBytes());
    }
    /*
        private Queue<MsMessage> _sendQueue;
        private Timer _sendPoolTimer;
        private byte _gwAddr;
        private Timer _advTimer;

        public MsDevSer(){
          if(Topic.brokerMode) {
            _sendQueue=new Queue<MsMessage>();
            _sendPoolTimer=new Timer(new TimerCallback(SendPool), null, Timeout.Infinite, Timeout.Infinite);
          }
        }

        private byte[] _inputBuf=new byte[256];
        private byte[] _inputAddr=new byte[1];
        private bool _escChar=false;
        private int _inputCnt=-1;

        private void RecvThread(object o) {
          try {
            _inputCnt=0;
            _escChar=false;
            while(_port!=null && _port.IsOpen) {
              if(GetPacket()) {
                byte[] rezBuf=new byte[_inputCnt];
                byte[] rezAddr=new byte[] { _inputAddr[0] };
                Array.Copy(_inputBuf, rezBuf, _inputCnt);
                _inputCnt=0;
                ParseInPacket(rezAddr, rezBuf);
              } else {
                Thread.Sleep(15);
              }
            }
          }
          catch(Exception ex) {
            Log.Error("MsGateway.RecvThread ex={0}", ex.Message);
          }
          _sendPoolTimer.Change(15000, Timeout.Infinite);
          if(_port!=null) {
            try {
              _port.Dispose();
            }
            catch(Exception) {
            }
            _port=null;
          }
          if(SerialPortName!="offline") {
            ThreadPool.QueueUserWorkItem(new WaitCallback(OpenPort));
          }
        }

        private void PortNameChanged(Topic sender, TopicChanged param) {
          if(param.Art==TopicChanged.ChangeArt.Value) {
            if(portName!="offline") {
              ThreadPool.QueueUserWorkItem(new WaitCallback(OpenPort));
            } else {
              _sendPoolTimer.Change(Timeout.Infinite, Timeout.Infinite);
              if(_port!=null) {
                if(_port.IsOpen) {
                  _port.Close();
                }
                _port=null;
              }
            }
            return;
          }
        }

        internal void Send(MsMessage msg) {
          lock(_sendQueue) {
            _sendQueue.Enqueue(msg);
          }
        }
        private void SendIntern(MsMessage msg) {
          byte[] buf=msg.GetBytes();
          Queue<byte> sBuf=new Queue<byte>(buf.Length*3/2);
          for(int i=0; i<buf.Length; i++) {
            if(buf[i]==0xC0 || buf[i]==0xDB) {
              sBuf.Enqueue(0xDB);
              sBuf.Enqueue((byte)(buf[i] ^ 0x20));
            } else {
              sBuf.Enqueue(buf[i]);
            }
          }
          sBuf.Enqueue(0xC0);
          _port.Write(sBuf.ToArray(), 0, sBuf.Count);
          var dev=GetDeviceByAddr(msg.Addr);
          if(_debug)
            Log.Debug("s {0} {1} [{2}]", dev!=null?dev.path:msg.Addr.ToString(), msg.ToString(), BitConverter.ToString(buf));
        }
        private void OpenPort(object o) {
          lock(_inputBuf) {
            if(_port!=null) {
              if(_port.PortName==portName) {
                return;
              }
              _sendPoolTimer.Change(Timeout.Infinite, Timeout.Infinite);
              _port.Close();
              _port=null;
            }
            List<string> ports=new List<string>();
            string spMin=null;
            _gwAddr=0xFF;
            if(!string.IsNullOrWhiteSpace(portName)) {
              ports.Add(portName);
            } else {
              ports.AddRange(SerialPort.GetPortNames());
            }
            foreach(string pn in ports) {

                try {
                  _port=new SerialPort(pn, 38400, Parity.None, 8, StopBits.One);
                  _port.ReadBufferSize=300;
                  _port.WriteBufferSize=300;
                  _port.Open();
                  _port.DiscardInBuffer();
                  _inputCnt=0;
                  byte[] bufO=new byte[] { 0xC0, 0x00, 0x03, 0x01, 0x00, 0xC0 };
                  _port.Write(bufO, 0, bufO.Length);   // Send SearchGW
                  if(_debug)
                    Log.Debug("{0} s {1}", pn, BitConverter.ToString(bufO));
                  Thread.Sleep(50);
                  while(GetPacket()) {

                    if(_debug) 
                      Log.Debug("{0} r {1}", pn, BitConverter.ToString(_inputBuf, 0, _inputCnt));
                    if(_inputCnt==4 && _inputBuf[2]==0x02) {   // Received GWInfo
                      if(_inputBuf[3]<_gwAddr) {
                        _gwAddr=_inputBuf[3];
                        spMin=pn;
                      }
                      _port.Close();
                      break;
                    }
                    Thread.Sleep(50);
                  }
                  _port.Close();
                }
                catch(Exception) {
                  if(_port!=null && _port.IsOpen) {
                    _port.Close();
                  }
                }
                _port=null;
          
            }
            if(spMin!=null) {
              try {
                _port=new SerialPort(spMin, 38400, Parity.None, 8, StopBits.One);
                //_port.ReadTimeout=Timeout.Infinite;
                //_port.WriteTimeout=30;
                _port.ReadBufferSize=300;
                _port.WriteBufferSize=300;
                _port.Open();
                _port.DiscardInBuffer();
                _inputCnt=0;
                portName=spMin;
              }
              catch(Exception) {
                _port=null;
              }
            }
            if(_port==null) {
              portName=null;
              _sendPoolTimer.Change(15000, Timeout.Infinite);
              return;
            }
          }
          Log.Info("found MQTTS Gataway on {0}", portName);
          _sendPoolTimer.Change(600, 14);
          ThreadPool.QueueUserWorkItem(RecvThread);
          this.Send(new MsDisconnect() {Addr=new byte[]{0} });
          if(_advTimer!=null) {
            _advTimer.Dispose();
          }
          _advTimer=new Timer(new TimerCallback(SendAdvMessage), new MsAdvertise(_gwAddr, 900), 5000, 900000);
        }
        private void SendAdvMessage(object o) {
          this.Send((MsAdvertise)o);
        }
        private void ParseInPacket(byte[] addr, byte[] buf) {
          try {
            var msgTyp=(MsMessageType)(buf[1]>0?buf[2]:buf[4]);
            DVar<MsDevice> dev=null;
            if(msgTyp!=MsMessageType.CONNECT && msgTyp!=MsMessageType.SEARCHGW) {
              dev=GetDeviceByAddr(addr);
              if(dev==null || dev.value==null || dev.value.state==MsDeviceState.Disconnected || dev.value.state==MsDeviceState.Lost) {
                if(_debug) {
                  if(dev==null || dev.value==null) {
                    Log.Debug("unknown device: [{0}]", BitConverter.ToString(buf));
                  } else {
                    Log.Debug("inactive device: [{0}]", BitConverter.ToString(buf));
                  }
                }
                if(addr[0]!=0) {             // broadcast addr
                  this.Send(new MsDisconnect() {Addr=addr });
                }
                return;
              }
            }
            switch(msgTyp) {
            case MsMessageType.SEARCHGW:
              PrintPacket(null, new MsSearchGW(buf), buf);
              this.Send(new MsGwInfo(_gwAddr));
              break;
            case MsMessageType.CONNECT: {
                var msg=new MsConnect(buf);
                if(msg.Addr[0]==0xFF) {
                  PrintPacket(null, msg, buf);
                  this.Send(new MsConnack(MsReturnCode.Accepted) { Addr=msg.Addr });
                  byte[] nAddr=new byte[1];
                  var r=new Random(DateTime.Now.Millisecond);
                  do {
                    nAddr[0]=(byte)(8+r.Next(0xF6));  //0x08 .. 0xFE
                  } while(Owner.children.Where(z => z is DVar<MsDevice>).Cast<DVar<MsDevice>>().Any(z => z.value!=null && z.value.Addr==nAddr));
                  Log.Info("{0} new addr={1}", msg.ClientId, nAddr);
                  var pm=new MsPublish(null, (ushort)PredefinedTopics._DeviceAddr, QoS.AtLeastOnce) { Addr=msg.Addr, MessageId=1 };
                  pm.Data=nAddr;
                  this.Send(pm);
                } else {
                  dev=Owner.Get<MsDevice>(msg.ClientId);
                  if(dev.value==null) {
                    if(!msg.CleanSession) {
                      this.Send(new MsConnack(MsReturnCode.InvalidTopicId) { Addr=msg.Addr });
                      PrintPacket(dev, msg, buf);
                      return;
                    }
                    dev.value=new MsDevRf();
                  } else {
                    if(!msg.CleanSession && (dev.value.Addr!=msg.Addr || dev.value.state==MsDeviceState.Disconnected || dev.value.state==MsDeviceState.Lost)) {
                      this.Send(new MsConnack(MsReturnCode.InvalidTopicId) { Addr=msg.Addr });
                      PrintPacket(dev, msg, buf);
                      return;
                    }
                    if(dev.value.Addr!=msg.Addr) {
                      dev.value.Addr=msg.Addr;
                    }
                  }
                  PrintPacket(dev, msg, buf);
                  foreach(DVar<MsDevice> dead in Owner.children.Where(z => z is DVar<MsDevice> && z!=dev).Cast<DVar<MsDevice>>().Where(z => z.value!=null && z.value.Addr==msg.Addr).ToArray()) {
                    dead.value.Disconnect();
                    dead.value.Addr=null;
                    dead.Publish(dead, TopicChanged.ChangeArt.Value, dev);
                  }
                  dev.value.Connect(msg);
                }
              }
              break;
            case MsMessageType.WILLTOPIC: {
                var msg=new MsWillTopic(buf);
                PrintPacket(dev, msg, buf);
                dev.value.WillTopic(msg);
              }
              break;
            case MsMessageType.WILLMSG: {
                var msg=new MsWillMsg(buf);
                PrintPacket(dev, msg, buf);
                dev.value.WillMsg(msg);
              }
              break;
            case MsMessageType.SUBSCRIBE: {
                var msg=new MsSubscribe(buf);
                PrintPacket(dev, msg, buf);
                dev.value.Subscibe(msg);
              }
              break;
            case MsMessageType.REGISTER: {
                var msg=new MsRegister(buf);
                PrintPacket(dev, msg, buf);
                dev.value.Register(msg);
              }
              break;
            case MsMessageType.REGACK: {
                var msg=new MsRegAck(buf);
                PrintPacket(dev, msg, buf);
                dev.value.RegAck(msg);
              }
              break;
            case MsMessageType.PUBLISH: {
                var msg=new MsPublish(buf);
                PrintPacket(dev, msg, buf);
                dev.value.Publish(msg);
              }
              break;
            case MsMessageType.PUBACK: {
                var msg=new MsPubAck(buf);
                PrintPacket(dev, msg, buf);
                dev.value.PubAck(msg);
              }
              break;
            case MsMessageType.PINGREQ: {
                var msg=new MsPingReq(buf);
                PrintPacket(dev, msg, buf);
                dev.value.PingReq(msg);
              }
              break;
            case MsMessageType.DISCONNECT: {
                var msg=new MsDisconnect(buf);
                PrintPacket(dev, msg, buf);
                dev.value.Disconnect(msg.Duration);
              }
              break;
            default:
              Log.Warning("{0} unknown packet: {1}", dev.path, BitConverter.ToString(buf));
              break;
            }
          }
          catch(ArgumentException) {
            Log.Warning("incorrect packet on {0} ({1})", Owner.name, BitConverter.ToString(buf));
            _port.DiscardInBuffer();
            _inputCnt=0;
            _port.Write(new byte[] { 0xC0, 0x00, 0x03, 0x01, 0x00, 0xC0 }, 0, 6);   // Send SearchGW
          }
          catch(Exception ex) {
            Log.Error("{0} ({1}) {2}", Owner.name, BitConverter.ToString(buf), ex.ToString());
          }
        }
        private void SendPool(object o) {
          try {
            MsMessage msg=null;
            lock(_sendQueue) {
              if(_sendQueue.Count>0) {
                msg=_sendQueue.Dequeue();
              }
            }
            if(msg!=null) {
              SendIntern(msg);
            }
          }
          catch(Exception ex) {
            _sendPoolTimer.Change(15000, Timeout.Infinite);
            if(_port!=null) {
              try {
                _port.Dispose();
              }
              catch(Exception) {
              }
              _port=null;
              Log.Error("MsGateway.CtsTimeout {0}", ex.Message);
            }
            if(portName!="offline") {
              ThreadPool.QueueUserWorkItem(new WaitCallback(OpenPort));
            }
          }
        }
        private void PrintPacket(DVar<MsDevice> dev, MsMessage msg, byte[] buf=null) {
          if(_debug) {
            Log.Debug("r {0} {1} [{2}]", dev!=null?dev.path:BitConverter.ToString(msg.Addr), msg.ToString(), BitConverter.ToString(buf??msg.GetBytes()));
          }
        }
        private DVar<MsDevice> GetDeviceByAddr(byte[] addr) {
          return Owner.children.Where(tp => tp is DVar<MsDevice>).Cast<DVar<MsDevice>>().FirstOrDefault(tp => tp.value!=null && addr.SequenceEqual(tp.value.Addr));
        }
        public override string ToString() {
          return string.Format("{1}@{0}", portName, Owner.name);
        }

        [Newtonsoft.Json.JsonProperty]
        internal string declarer { get { return Owner.Get<string>("_declarer").value; } set { } }
        */
  }
}
