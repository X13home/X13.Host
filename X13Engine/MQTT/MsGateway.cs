﻿#region license
//Copyright (c) 2011-2013 <comparator@gmx.de>; Wassili Hense

//This file is part of the X13.Home project.
//https://github.com/X13home

//BSD License
//See LICENSE.txt file for license details.
#endregion license

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using X13.PLC;
using System.IO.Ports;
using System.Threading;

namespace X13.MQTT {
  [Newtonsoft.Json.JsonObject(Newtonsoft.Json.MemberSerialization.OptIn)]
  public class MsGateway : ITopicOwned {
    private const string P_N="_SerialPortName";

    static MsGateway() {
      if(Topic.brokerMode) {
        var t1=Topic.root.Get<string>("/system/declarers/MsGateway");
        t1.value="/CC;component/Images/ty_gateway.png";
        t1.Get<string>("remove").value="1D";
      }
    }

    private SerialPort _port;
    private Queue<MsMessage> _sendQueue;
    private bool _escChar=false;
    private Queue<byte> _InputQueue=new Queue<byte>(128);
    private Timer _sendPoolTimer;
    private byte _gwAddr;
    private MsAdvertise _advMsg;
    private Timer _advTimer;

    [Newtonsoft.Json.JsonIgnore]
    public DVar<MsGateway> Owner { get; private set; }
    public string SerialPortName {
      get { return Owner!=null?Owner.Get<string>(P_N).value:string.Empty; }
      set {
        if(Owner!=null) {
          var t=Owner.Get<string>(P_N);
          t.saved=true;
          t.value=value;
        }
      }
    }

    public MsGateway() {
      if(Topic.brokerMode) {
        _sendQueue=new Queue<MsMessage>();
        _sendPoolTimer=new Timer(new TimerCallback(SendPool), null, Timeout.Infinite, Timeout.Infinite);
      }
    }

    private void PortNameChanged(Topic sender, TopicChanged param) {
      if(param.Art==TopicChanged.ChangeArt.Value) {
        if(SerialPortName!="offline") {
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
      Log.Debug("s {0} {1} [{2}]", dev!=null?dev.path:msg.Addr.ToString(), msg.ToString(), BitConverter.ToString(buf));
    }
    private void OpenPort(object o) {
      lock(_InputQueue) {
        if(_port!=null) {
          if(_port.PortName==SerialPortName) {
            return;
          }
          _sendPoolTimer.Change(Timeout.Infinite, Timeout.Infinite);
          _port.Close();
          _port=null;
        }
        List<string> ports=new List<string>();
        string spMin=null;
        _gwAddr=0xFF;
        if(!string.IsNullOrWhiteSpace(SerialPortName)) {
          ports.Add(SerialPortName);
        }
        ports.AddRange(SerialPort.GetPortNames());
        foreach(string pn in ports) {
          for(int i=2; i>0; i--) {
            try {
              _port=new SerialPort(pn, 38400, Parity.None, 8, StopBits.One);
              _port.ReadTimeout=30;
              _port.ReceivedBytesThreshold=5;
              _port.Open();
              _port.DiscardInBuffer();
              byte[] bufO=new byte[] { 0xC0, 0x00, 0x03, 0x01, 0x00, 0xC0 };
              _port.Write(bufO, 0, bufO.Length);   // Send SearchGW
              Log.Debug("{0} s {1}", pn, BitConverter.ToString(bufO));
              byte[] inBuf=new byte[5];
              Thread.Sleep(34);
              int j=_port.Read(inBuf, 0, 5);
              Log.Debug("{0} r {1}", pn, BitConverter.ToString(inBuf));
              if(j==5 && inBuf[2]==0x02) {   // Received GWInfo
                if(inBuf[3]<_gwAddr) {
                  _gwAddr=inBuf[3];
                  spMin=pn;
                }
                _port.Close();
                break;
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
        }
        if(spMin!=null) {
          try {
            _port=new SerialPort(spMin, 38400, Parity.None, 8, StopBits.One);
            _port.ReadTimeout=5;
            _port.ReceivedBytesThreshold=1;
            _port.Open();
            _port.DiscardInBuffer();

            SerialPortName=spMin;
          }
          catch(Exception) {
            _port=null;
          }
        }
        if(_port==null) {
          SerialPortName=null;
          _sendPoolTimer.Change(15000, Timeout.Infinite);
          return;
        }
      }
      Log.Info("found MQTTS Gataway on {0}", SerialPortName);
      _port.DiscardInBuffer();
      _port.DataReceived+=new SerialDataReceivedEventHandler(DataReceived);
      _sendPoolTimer.Change(600, 14);
      this.Send(new MsDisconnect() { Addr=0 });
      if(_advTimer!=null){
        _advTimer.Dispose();
      }
      _advTimer=new Timer(new TimerCallback(SendAdvMessage), new MsAdvertise(_gwAddr, 900), 5000, 900000);
    }
    private void SendAdvMessage(object o) {
      this.Send((MsAdvertise)o);
    }
    private void DataReceived(object sender, SerialDataReceivedEventArgs e) {
      try {
        while(_port!=null && _port.BytesToRead>0) {
          int b=_port.ReadByte();
          if(b<0) {
            break;
          }
          if(b==0xC0) {
            byte[] rezBuf=_InputQueue.ToArray();
            _InputQueue.Clear();
            _escChar=false;
            if(rezBuf.Length>1 && rezBuf.Length==rezBuf[1]+1) {
              ParseInPacket(rezBuf);
            } else {
              Log.Warning("size mismatch: {0}", BitConverter.ToString(rezBuf));
            }
            continue;
          }
          if(b==0xDB) {
            _escChar=true;
            continue;
          }
          if(_escChar) {
            b^=0x20;
            _escChar=false;
          }
          _InputQueue.Enqueue((byte)b);
        }
      }
      catch(Exception ex) {
        _sendPoolTimer.Change(15000, Timeout.Infinite);
        try {
          _port.Dispose();
        }
        catch(Exception) {
        }
        _port=null;
        Log.Error("MsGateway.DataReceived {0}", ex.Message);
        if(SerialPortName!="offline") {
          ThreadPool.QueueUserWorkItem(new WaitCallback(OpenPort));
        }
      }
    }
    private void ParseInPacket(byte[] buf) {
      try {
        var msgTyp=(MsMessageType)(buf[1]>0?buf[2]:buf[4]);
        DVar<MsDevice> dev=null;
        if(msgTyp!=MsMessageType.CONNECT && msgTyp!=MsMessageType.SEARCHGW) {
          dev=GetDeviceByAddr(buf[0]);
          if(dev==null || dev.value==null || dev.value.state==MsDeviceState.Disconnected || dev.value.state==MsDeviceState.Lost) {
            if(dev==null || dev.value==null) {
              Log.Debug("unknown device: [{0}]", BitConverter.ToString(buf));
            } else {
              Log.Debug("inactive device: [{0}]", BitConverter.ToString(buf));
            }
            if(buf[0]!=0) {             // broadcast addr
              this.Send(new MsDisconnect() { Addr=buf[0] });
            }
            return;
          }
        }
        switch(msgTyp) {
        case MsMessageType.SEARCHGW:
          PrintPacket(null, new MsSearchGW(buf), buf);
          this.Send(new MsGwInfo(0, _gwAddr));
          break;
        case MsMessageType.CONNECT: {
            var msg=new MsConnect(buf);
            if(msg.Addr==0xFF) {
              PrintPacket(null, msg, buf);
              this.Send(new MsConnack(MsReturnCode.Accepted) { Addr=msg.Addr });
              byte nAddr;
              var r=new Random(DateTime.Now.Millisecond);
              do {
                nAddr=(byte)(8+r.Next(0xF6));  //0x08 .. 0xFE
              } while(Owner.children.Where(z => z is DVar<MsDevice>).Cast<DVar<MsDevice>>().Any(z => z.value!=null && z.value.Addr==nAddr));
              Log.Info("{0} new addr={1}", msg.ClientId, nAddr);
              var pm=new MsPublish(null, (ushort)PredefinedTopics._DeviceAddr, QoS.AtLeastOnce) { Addr=msg.Addr, MessageId=1 };
              pm.Data=new byte[] { nAddr };
              this.Send(pm);
            } else {
              dev=Owner.Get<MsDevice>(msg.ClientId);
              if(dev.value==null) {
                if(!msg.CleanSession) {
                  this.Send(new MsConnack(MsReturnCode.InvalidTopicId) { Addr=msg.Addr });
                  PrintPacket(dev, msg, buf);
                  return;
                }
                dev.value=new MsDevice();
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
                dead.value.Addr=0;
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
        _InputQueue.Clear();
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
        if(SerialPortName!="offline") {
          ThreadPool.QueueUserWorkItem(new WaitCallback(OpenPort));
        }
      }
    }
    private void PrintPacket(DVar<MsDevice> dev, MsMessage msg, byte[] buf=null) {
      Log.Debug("r {0} {1} [{2}]", dev!=null?dev.path:msg.Addr.ToString("X2"), msg.ToString(), BitConverter.ToString(buf??msg.GetBytes()));
    }
    private DVar<MsDevice> GetDeviceByAddr(byte addr) {
      return Owner.children.Where(tp => tp is DVar<MsDevice>).Cast<DVar<MsDevice>>().FirstOrDefault(tp => tp.value!=null && tp.value.Addr==addr);
    }
    public override string ToString() {
      return string.Format("{1}@{0}", SerialPortName, Owner.name);
    }

    #region ITopicOwned Members
    void ITopicOwned.SetOwner(Topic owner) {
      if(Owner!=owner) {
        if(Topic.brokerMode && Owner!=null) {
          Owner.Unsubscribe(P_N, PortNameChanged);
        }
        Owner=owner as DVar<MsGateway>;
        if(Topic.brokerMode && Owner!=null) {
          Owner.Subscribe(P_N, PortNameChanged);
          Owner.saved=true;
          Owner.Get<string>(P_N).saved=true;
          var dc=Owner.Get<string>("_declarer");
          dc.saved=true;
          dc.value="MsGateway";
        }
      }
    }
    #endregion ITopicOwned Members

    [Newtonsoft.Json.JsonProperty]
    internal string declarer { get { return Owner.Get<string>("_declarer").value; } set { } }
  }
}
