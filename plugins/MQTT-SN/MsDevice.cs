#region license
//Copyright (c) 2011-2014 <comparator@gmx.de>; Wassili Hense

//This file is part of the X13.Home project.
//https://github.com/X13home

//BSD License
//See LICENSE.txt file for license details.
#endregion license

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;

namespace X13.Periphery {
  [Newtonsoft.Json.JsonObject(Newtonsoft.Json.MemberSerialization.OptIn)]
  public partial class MsDevice : ITopicOwned, IMsGate {
    private const int ACK_TIMEOUT=600;
    private const ushort LOG_D_ID=0xFFE0;
    private const ushort LOG_I_ID=0xFFE1;
    private const ushort LOG_W_ID=0xFFE2;
    private const ushort LOG_E_ID=0xFFE3;
    private static DVar<bool> _verbose;
    private static DVar<bool> _statistic;
    private static List<IMsGate> _gates;
    private static Random _rand;

    static MsDevice() {
      _verbose=Topic.root.Get<bool>("/etc/MQTT-SN/verbose");
      _statistic=Topic.root.Get<bool>("/etc/MQTT-SN/statistic");
      _gates=new List<IMsGate>();
      _rand=new Random((int)DateTime.Now.Ticks);
    }
    internal static void Open() {
      //MsGUdp.Open();
    }
    internal static byte[] Serialize(Topic t) {
      List<byte> ret=new List<byte>();
      switch(Type.GetTypeCode(t.valueType)) {
      case TypeCode.Boolean:
        ret.Add((byte)((t as DVar<bool>).value?1:0));
        break;
      case TypeCode.Int64: {
          long vo=(t as DVar<long>).value;
          long v=vo;
          do {
            ret.Add((byte)v);
            v=v>>8;
          } while(vo<0?(v<-1 || (ret[ret.Count-1]&0x80)==0):(v>0 || (ret[ret.Count-1]&0x80)!=0));
        }
        break;
      //case TypeCode.Double:
      case TypeCode.String: {
          string v=(string)t.GetValue();
          if(!string.IsNullOrEmpty(v)) {
            ret.AddRange(Encoding.Default.GetBytes(v));
          }
        }
        break;
      case TypeCode.Object:
        if(t.valueType==typeof(PLC.ByteArray) && t.GetValue()!=null) {
          ret.AddRange(((PLC.ByteArray)t.GetValue()).GetBytes());
        }
        break;
      }
      return ret.ToArray();
    }
    public static void ProcessInPacket(IMsGate gate, byte[] addr, byte[] buf, int start, int end) {
      var msg=MsMessage.Parse(buf, start, end);
      if(msg==null) {
        if(_verbose.value) {
          Log.Warning("r {0}: {1}  bad message", gate.Addr2If(addr), BitConverter.ToString(buf, start, end-start));
        }
        return;
      }
      if(msg.MsgTyp==MsMessageType.ADVERTISE || msg.MsgTyp==MsMessageType.GWINFO) {
        return;
      }
      if(_verbose.value) {
        Log.Debug("r {0}: {1}  {2}", gate.Addr2If(addr), BitConverter.ToString(buf, start, end-start), msg.ToString());
      }
      if(msg.MsgTyp==MsMessageType.SEARCHGW) {
        if((msg as MsSearchGW).radius==0 || (msg as MsSearchGW).radius==gate.gwRadius) {
          gate.SendGw((MsDevice)null, new MsGwInfo(gate.gwIdx));
        }
        return;
      }
      Topic devR=Topic.root.Get("/dev");
      if(msg.MsgTyp==MsMessageType.DHCP_REQ) {
        var dr=msg as MsDhcpReq;
        if((dr.radius==0 || dr.radius==1)) {
          List<byte> ackAddr=new List<byte>();
          byte[] respPrev=null;
          foreach(byte hLen in dr.hLen) {
            if(hLen==0) {
              continue;
            } else if(hLen<=8) {
              byte[] resp;
              if(respPrev!=null && respPrev.Length==hLen) {
                resp=respPrev;
              } else {
                resp=new byte[hLen];
                for(int i=0; i<5; i++) {
                  for(int j=0; j<resp.Length; j++) {
                    resp[j]=(byte)_rand.Next(j==0?4:0, (i<3 && hLen==1)?31:(j==0?254:255));
                  }
                  if(devR.children.Select(z => z as DVar<MsDevice>).Where(z => z!=null && z.value!=null).All(z => !z.value.CheckAddr(resp))) {
                    break;
                  } else if(i==4) {
                    for(int j=0; j<resp.Length; j++) {
                      resp[j]=0xFF;
                    }
                  }
                }
                respPrev=resp;
              }
              ackAddr.AddRange(resp);
            } else {
              if(_verbose.value) {
                Log.Warning("r {0}: {1}  DhcpReq.hLen is too high", gate.Addr2If(addr), BitConverter.ToString(buf, start, end-start));
              }
              ackAddr=null;
              break;
            }
          }
          if(ackAddr!=null) {
            gate.SendGw((MsDevice)null, new MsDhcpAck(gate.gwIdx, dr.xId, ackAddr.ToArray()));
          }
        }
        return;
      }
      if(msg.MsgTyp==MsMessageType.CONNECT) {
        var cm=msg as MsConnect;
        DVar<MsDevice> dDev=devR.Get<MsDevice>(cm.ClientId);
        if(dDev.value==null) {
          dDev.value=new MsDevice(gate, addr);
          Thread.Sleep(0);
          dDev.value.Owner=dDev;
        } else {
          gate.RemoveNode(dDev.value);
          dDev.value._gate=gate;
          dDev.value.Addr=addr;
        }
        gate.AddNode(dDev.value);
        dDev.value.Connect(cm);
        foreach(var dub in devR.children.Select(z => z.GetValue() as MsDevice).Where(z => z!=null && z!=dDev.value && z.Addr!=null && z.Addr.SequenceEqual(addr) && z._gate==gate).ToArray()) {
          dub.Addr=null;
          dub._gate=null;
          dub.state=State.Disconnected;
        }
      } else {
        MsDevice dev=devR.children.Select(z => z.GetValue() as MsDevice).FirstOrDefault(z => z!=null && z.Addr!=null && z.Addr.SequenceEqual(addr) && z._gate==gate);
        if(dev!=null && ((dev.state!=State.Disconnected && dev.state!=State.Lost) || msg.MsgTyp==MsMessageType.CONNECT)) {
          dev.ProcessInPacket(msg);
        } else {
          if(dev==null || dev.Owner==null) {
            Log.Debug("{0} unknown device", gate.Addr2If(addr));
            gate.SendGw(addr, new MsDisconnect());
          } else {
            Log.Debug("{0} inactive device: {1}", gate.Addr2If(addr), dev.Owner.path);
            gate.SendGw(dev, new MsDisconnect());
          }
        }
      }
    }

    private int _duration=3000;
    private DVar<State> _stateVar;

    private string _willPath;
    private byte[] _wilMsg;
    private bool _willRetain;
    private Timer _activeTimer;
    // TODO: Save/Restore _topics & _subsscriptions
    private List<TopicInfo> _topics;
    private List<Topic.Subscription> _subsscriptions;
    private Queue<MsMessage> _sendQueue;
    private string _declarer="MQTTS";
    private int _tryCounter;
    private int _messageIdGen=0;
    private DVar<bool> _present;
    private IMsGate _gate;
    private bool _waitAck;
    private List<MsDevice> _nodes;
    private Timer _poolTimer;
    private MsPublish _lastInPub;

    private MsDevice() {
      if(Topic.brokerMode) {
        _activeTimer=new Timer(new TimerCallback(TimeOut));
        _poolTimer=new Timer(ReisePool);
        _topics=new List<TopicInfo>(16);
        _subsscriptions=new List<Topic.Subscription>(4);
        _sendQueue=new Queue<MsMessage>();
      }
    }

    private void ReisePool(object state) {
      if(Pool!=null) {
        try {
          Pool();
        }
        catch(Exception ex) {
          Log.Warning("{0}.ReisePool - {1}", Owner, ex.ToString());
        }
      }
    }

    internal MsDevice(IMsGate gate, byte[] addr)
      : this() {
      _gate=gate;
      Addr=addr;
    }

    public State state {
      get { return _stateVar!=null?_stateVar.value:State.Disconnected; }
      protected set {
        State oldState=State.Disconnected;
        if(_stateVar!=null) {
          try {
            _stateVar.saved=false;
            oldState=_stateVar.value;
            _stateVar.value=value;
          }
          catch(ObjectDisposedException) {
            _stateVar=null;
          }
        }
        if(_present!=null) {
          try {
            _present.saved=false;
            _present.value=(state==State.Connected || state==State.ASleep || state==State.AWake);
          }
          catch(ObjectDisposedException) {
            _present=null;
          }
        }
        if(value==State.Connected) {
          _poolTimer.Change(300, 100);
        } else {
          _poolTimer.Change(-1, -1);
          if(!_present.value) {
            foreach(var t in Owner.children.Where(z => z.valueType==typeof(TWIDriver)).Select(z => z.GetValue() as TWIDriver).Where(z => z!=null)) {
              t.Reset();
            }
          }
        }
      }
    }
    public Topic Owner { get; private set; }
    public string name { get { return Owner!=null?Owner.name:string.Empty; } }
    public string Addr2If(byte[] addr) {
      return _gate!=null?_gate.Addr2If(addr):string.Concat(BitConverter.ToString(addr), " via ", this.name);
    }
    private string via {
      get { return Owner!=null?Owner.Get<string>(".cfg/_via").value:string.Empty; }
      set {
        if(Owner!=null) {
          var t=Owner.Get<string>(".cfg/_via");
          t.saved=true;
          t.value=value;
        }
      }
    }
    [Newtonsoft.Json.JsonProperty]
    private byte[] Addr { get; set; }
    [Newtonsoft.Json.JsonProperty]
    private string backName { get; set; }
	public SortedList<string, string> varMapping;

	public void AddNode(MsDevice dev) {
      if(_nodes==null) {
        _nodes=new List<MsDevice>();
      }
      _nodes.Add(dev);
    }
    public void RemoveNode(MsDevice dev) {
      if(_nodes!=null) {
        _nodes.Remove(dev);
      }
    }
    public void Stop() {
      if(_nodes==null || !_nodes.Any()) {
        return;
      }
      var nodes=_nodes.ToArray();
      for(int i=0; i<nodes.Length; i++) {
        nodes[i].Stop();
      }
      if(_gate!=null) {
        _gate.SendGw(this, new MsDisconnect());
        Stat(true, MsMessageType.DISCONNECT, false);
      }
      state=State.Disconnected;
    }

    private void Stat(bool send, MsMessageType t, bool dub=false) {
      string n2;
      switch(t) {
      case MsMessageType.CONNECT:
        n2="_Connect";
        break;
      case MsMessageType.GWINFO:
        n2="_Lost";
        break;
      case MsMessageType.PUBLISH:
        if(send) {
          n2=dub?"e_sPublishDup":"d_sPublish";
        } else {
          n2=dub?"b_rPublishDup":"a_rPublish";
        }
        break;
      case MsMessageType.PUBACK:
        n2=send?"c_sPubAck":"f_rPubAck";
        break;
      case MsMessageType.PINGREQ:
        n2="g_PingReq";
        break;
      case MsMessageType.PINGRESP:
        n2="h_PingResp";
        break;
      case MsMessageType.EncapsulatedMessage:
        return;
      default:
        n2=send?"o_sOther":"o_rOther";
        break;
      }
      if(Owner==null) {
        return;
      }
      string p=string.Concat("/var/stat/MQTT-SN/", Owner.name);
      Topic pa=Topic.root.Get(p);
      pa.saved=false;
      DVar<long> d=pa.Get<long>(n2);
      d.saved=false;
      d.value++;
    }

    internal void ProcessInPacket(MsMessage msg) {
      if(_statistic.value && msg.MsgTyp!=MsMessageType.EncapsulatedMessage && msg.MsgTyp!=MsMessageType.PUBLISH) {
        Stat(false, msg.MsgTyp);
      }
      switch(msg.MsgTyp) {
      case MsMessageType.WILLTOPIC: {
          var tmp=msg as MsWillTopic;
          if(state==State.WillTopic) {
            _willPath=tmp.Path;
            _willRetain=tmp.Retain;
            state=State.WillMsg;
            ProccessAcknoledge(msg);
          }
        }
        break;
      case MsMessageType.WILLMSG: {
          var tmp=msg as MsWillMsg;
          if(state==State.WillMsg) {
            _wilMsg=tmp.Payload;
            Log.Info("{0}.state {1} => WILLTOPICREQ", Owner.path, state);
            state=State.PreConnect;
            ProccessAcknoledge(msg);
            Send(new MsConnack(MsReturnCode.Accepted));
          }
        }
        break;
      case MsMessageType.SUBSCRIBE: {
          var tmp=msg as MsSubscribe;

          SyncMsgId(msg.MessageId);
          Topic.Subscription s=null;
          ushort topicId=tmp.topicId;
          if(tmp.topicIdType!=TopicIdType.Normal || tmp.path.IndexOfAny(new[] { '+', '#' })<0) {
            TopicInfo ti=null;
            if(tmp.topicIdType==TopicIdType.Normal) {
              ti=GetTopicInfo(tmp.path, false);
            } else {
              ti=GetTopicInfo(tmp.topicId, tmp.topicIdType);
            }
            topicId=ti.TopicId;
          }
          Send(new MsSuback(tmp.qualityOfService, topicId, msg.MessageId, MsReturnCode.Accepted));
          if(state==State.PreConnect) {
            state=State.Connected;
          }
          s=Owner.Subscribe(tmp.path, PublishTopic, tmp.qualityOfService);
          _subsscriptions.Add(s);
        }
        break;
      case MsMessageType.REGISTER: {
          var  tmp=msg as MsRegister;
          ResetTimer();
          try {
            TopicInfo ti = GetTopicInfo(tmp.TopicPath, false);
            if(ti.topic!=null) {
              if(ti.topic.valueType==typeof(SmartTwi)) {
                if(ti.topic.GetValue()==null) {
                  ti.topic.SetValue(new SmartTwi(ti.topic), new TopicChanged(TopicChanged.ChangeArt.Value, Owner));
                } else {
                  (ti.topic as DVar<SmartTwi>).value.Reset();
                }
              } else if(ti.topic.valueType==typeof(TWIDriver)) {
                if(ti.topic.GetValue()==null) {
                  ti.topic.SetValue(new TWIDriver(ti.topic), new TopicChanged(TopicChanged.ChangeArt.Value, Owner));
                } else {
                  (ti.topic as DVar<TWIDriver>).value.Reset();
                }
              } else if(ti.topic.valueType==typeof(DevicePLC)) {
                if(ti.topic.GetValue()==null) {
                  ti.topic.SetValue(new DevicePLC(ti.topic), new TopicChanged(TopicChanged.ChangeArt.Value, Owner));
                } else {
                  (ti.topic as DVar<DevicePLC>).value.Reset();
                }

              }
            }
            Send(new MsRegAck(ti.TopicId, tmp.MessageId, MsReturnCode.Accepted));
          }
          catch(Exception) {
            Send(new MsRegAck(0, tmp.MessageId, MsReturnCode.NotSupportes));
            Log.Warning("Unknown variable type by register {0}, {1}", Owner.path, tmp.TopicPath);
          }
        }
        break;
      case MsMessageType.REGACK: {
          var  tmp=msg as MsRegAck;
          ProccessAcknoledge(tmp);
          TopicInfo ti=_topics.FirstOrDefault(z => z.TopicId==tmp.TopicId);
          if(ti==null) {
            if(tmp.TopicId!=0xFFFF) { // 0xFFFF - remove variable
              Log.Warning("{0} RegAck({1:X4}) for unknown variable", Owner.path, tmp.TopicId);
            }
            return;
          }
          if(tmp.RetCode==MsReturnCode.Accepted) {
            ti.registred=true;
            if(ti.it!=TopicIdType.PreDefined) {
              Send(new MsPublish(ti.topic, ti.TopicId, QoS.AtLeastOnce));
            }
          } else {
            Log.Warning("{0} registred failed: {1}", ti.path, tmp.RetCode.ToString());
            _topics.Remove(ti);
            ti.topic.Remove();
          }
        }
        break;
      case MsMessageType.PUBLISH: {
          var tmp=msg as MsPublish;
          if(_statistic.value) {
            Stat(false, msg.MsgTyp, tmp.Dup);
          }
          TopicInfo ti=_topics.Find(z => z.TopicId==tmp.TopicId && z.it==tmp.topicIdType);
          if(ti==null && tmp.topicIdType!=TopicIdType.Normal) {
            ti=GetTopicInfo(tmp.TopicId, tmp.topicIdType, false);
          }
          if(tmp.qualityOfService==QoS.AtMostOnce || (tmp.qualityOfService==QoS.MinusOne && (tmp.topicIdType==TopicIdType.PreDefined || tmp.topicIdType==TopicIdType.ShortName))) {
            ResetTimer();
          } else if(tmp.qualityOfService==QoS.AtLeastOnce) {
            SyncMsgId(tmp.MessageId);
            Send(new MsPubAck(tmp.TopicId, tmp.MessageId, ti!=null?MsReturnCode.Accepted:MsReturnCode.InvalidTopicId));
          } else if(tmp.qualityOfService==QoS.ExactlyOnce) {
            SyncMsgId(tmp.MessageId);
            // QoS2 not supported, use QoS1
            Send(new MsPubAck(tmp.TopicId, tmp.MessageId, ti!=null?MsReturnCode.Accepted:MsReturnCode.InvalidTopicId));
          } else {
            throw new NotSupportedException("QoS -1 not supported "+Owner.path);
          }
          if(tmp.topicIdType==TopicIdType.PreDefined && tmp.TopicId>=LOG_D_ID && tmp.TopicId<=LOG_E_ID) {
            string str=string.Format("{0} msgId={2:X4}  msg={1}", this.Owner.name, tmp.Data==null?"null":(BitConverter.ToString(tmp.Data)+"["+ Encoding.ASCII.GetString(tmp.Data.Select(z => (z<0x20 || z>0x7E)?(byte)'.':z).ToArray())+"]"), tmp.MessageId);
            switch(tmp.TopicId) {
            case LOG_D_ID:
              Log.Debug("{0}", str);
              break;
            case LOG_I_ID:
              Log.Info("{0}", str);
              break;
            case LOG_W_ID:
              Log.Warning("{0}", str);
              break;
            case LOG_E_ID:
              Log.Error("{0}", str);
              break;
            }
          } else if(ti!=null) {
            if(tmp.Dup && _lastInPub!=null && tmp.MessageId==_lastInPub.MessageId) {  // arready recieved
            } else {
              SetValue(ti, tmp.Data, tmp.Retained);
            }
            _lastInPub=tmp;
          }
        }
        break;
      case MsMessageType.PUBACK: {
          ProccessAcknoledge(msg);
        }
        break;
      case MsMessageType.PINGREQ: {
          var tmp=msg as MsPingReq;
          if(state==State.ASleep) {
            if(string.IsNullOrEmpty(tmp.ClientId) || tmp.ClientId==Owner.name) {
              state=State.AWake;
              ProccessAcknoledge(msg);    // resume send proccess
            } else {
              SendGw(this, new MsDisconnect());
              state=State.Lost;
              Log.Warning("{0} PingReq from unknown device: {1}", Owner.path, tmp.ClientId);
            }
          } else {
            ResetTimer();
            if(_gate!=null) {
              _gate.SendGw(this, new MsMessage(MsMessageType.PINGRESP));
              if(_statistic.value) {
                Stat(true, MsMessageType.PINGRESP, false);
              }
            }
          }
        }
        break;
      case MsMessageType.DISCONNECT:
        Disconnect((msg as MsDisconnect).Duration);
        break;
      case MsMessageType.CONNECT:
        Connect(msg as MsConnect);
        break;
      case MsMessageType.EncapsulatedMessage: {
          Topic devR=Topic.root.Get("/dev");
          var fm=msg as MsForward;
          if(fm.msg==null) {
            if(_verbose.value) {
              Log.Warning("bad message {0}:{1}", _gate, fm.ToString());
            }
            return;
          }
          if(fm.msg.MsgTyp==MsMessageType.SEARCHGW) {
            _gate.SendGw(this, new MsGwInfo(gwIdx));
          } else if(fm.msg.MsgTyp==MsMessageType.DHCP_REQ) {
            var dr=fm.msg as MsDhcpReq;
            //******************************
            List<byte> ackAddr=new List<byte>();
            byte[] respPrev=null;

            foreach(byte hLen in dr.hLen) {
              if(hLen==0) {
                continue;
              } else if(hLen<=8) {
                byte[] resp;
                if(respPrev!=null && respPrev.Length==hLen) {
                  resp=respPrev;
                } else {
                  resp=new byte[hLen];

                  for(int i=0; i<5; i++) {
                    for(int j=0; j<resp.Length; j++) {
                      resp[j]=(byte)_rand.Next((i<3 && hLen==1)?32:1, (i<3 && hLen==1)?126:(j==0?254:255));
                    }
                    if(devR.children.Select(z => z as DVar<MsDevice>).Where(z => z!=null && z.value!=null).All(z => !z.value.CheckAddr(resp))) {
                      break;
                    } else if(i==4) {
                      for(int j=0; j<resp.Length; j++) {
                        resp[j]=0xFF;
                      }
                    }
                  }
                  respPrev=resp;
                }
                ackAddr.AddRange(resp);
              } else {
                if(_verbose.value) {
                  Log.Warning("{0}:{1} DhcpReq.hLen is too high", BitConverter.ToString(fm.addr), fm.msg.ToString());
                }
                ackAddr=null;
                break;
              }
            }
            if(ackAddr!=null) {
              _gate.SendGw(this, new MsForward(fm.addr, new MsDhcpAck(gwIdx, dr.xId, ackAddr.ToArray())));
            }
            //******************************
          } else {
            if(fm.msg.MsgTyp==MsMessageType.CONNECT) {
              var cm=fm.msg as MsConnect;
              if(fm.addr!=null && fm.addr.Length==2 && fm.addr[1]==0xFF) {    // DHCP V<0.3
                _gate.SendGw(this, new MsForward(fm.addr, new MsConnack(MsReturnCode.Accepted)));

                byte[] nAddr=new byte[1];
                do {
                  nAddr[0]=(byte)(_rand.Next(32, 254));
                } while(!devR.children.Select(z => z as DVar<MsDevice>).Where(z => z!=null && z.value!=null).All(z => !z.value.CheckAddr(nAddr)));
                Log.Info("{0} new addr={1:X2}", cm.ClientId, nAddr[0]);
                _gate.SendGw(this, new MsForward(fm.addr, new MsPublish(null, PredefinedTopics[".cfg/XD_DeviceAddr"], QoS.AtLeastOnce) { MessageId=1, Data=nAddr }));
              } else {
                DVar<MsDevice> dDev=devR.Get<MsDevice>(cm.ClientId);
                if(dDev.value==null) {
                  dDev.value=new MsDevice(this, fm.addr);
                  Thread.Sleep(0);
                  dDev.value.Owner=dDev;
                } else {
                  this.RemoveNode(dDev.value);
                  dDev.value._gate=this;
                  dDev.value.Addr=fm.addr;
                }
                this.AddNode(dDev.value);
                dDev.value.Connect(cm);
                foreach(var dub in devR.children.Select(z => z.GetValue() as MsDevice).Where(z => z!=null && z!=dDev.value && z.Addr!=null && z.Addr.SequenceEqual(fm.addr) && z._gate==this).ToArray()) {
                  dub.Addr=null;
                  dub._gate=null;
                  dub.state=State.Disconnected;
                }
              }
            } else {
              MsDevice dev=devR.children.Select(z => z.GetValue() as MsDevice).FirstOrDefault(z => z!=null && z.Addr!=null && z.Addr.SequenceEqual(fm.addr) && z._gate==this);
              if(dev!=null 
                && ((dev.state!=State.Disconnected && dev.state!=State.Lost) 
                  || fm.msg.MsgTyp==MsMessageType.CONNECT 
                  || (fm.msg.MsgTyp==MsMessageType.PUBLISH && (fm.msg as MsPublish).qualityOfService==QoS.MinusOne))) {
                dev.ProcessInPacket(fm.msg);
              } else if(fm.msg.MsgTyp==MsMessageType.PUBLISH && (fm.msg as MsPublish).qualityOfService==QoS.MinusOne) {
                var tmp=fm.msg as MsPublish;
                if(tmp.topicIdType==TopicIdType.PreDefined && tmp.TopicId>=LOG_D_ID && tmp.TopicId<=LOG_E_ID) {
                  string str=string.Format("{0}: msgId={2:X4} msg={1}", BitConverter.ToString(this.Addr), tmp.Data==null?"null":(BitConverter.ToString(tmp.Data)+"["+ Encoding.ASCII.GetString(tmp.Data.Select(z => (z<0x20 || z>0x7E)?(byte)'.':z).ToArray())+"]"), tmp.MessageId);
                  switch(tmp.TopicId) {
                  case LOG_D_ID:
                    Log.Debug(str);
                    break;
                  case LOG_I_ID:
                    Log.Info(str);
                    break;
                  case LOG_W_ID:
                    Log.Warning(str);
                    break;
                  case LOG_E_ID:
                    Log.Error(str);
                    break;
                  }
                }
              } else {
                if(dev==null || dev.Owner==null) {
                  if(_verbose.value) {
                    Log.Debug("{0} via {1} unknown device", BitConverter.ToString(fm.addr), this.name);
                  }
                } else {
                  if(_verbose.value) {
                    Log.Debug("{0} via {1} inactive", dev.Owner.name, this.name);
                  }
                }
                _gate.SendGw(this, new MsForward(fm.addr, new MsDisconnect()));
              }
            }
          }
        }
        break;
      }
    }

    private void Connect(MsConnect msg) {
      if(Owner==null) {
        Thread.Sleep(30);
      }
      if(msg.CleanSession) {
        foreach(var s in _subsscriptions) {
          Owner.Unsubscribe(s.path, s.func);
        }
        _subsscriptions.Clear();
        _topics.Clear();
        lock(_sendQueue) {
          _sendQueue.Clear();
        }
        _waitAck=false;
      }
      _duration=msg.Duration*1100;
      ResetTimer();
      if(msg.Will) {
        _willPath=string.Empty;
        _wilMsg=null;
        if(msg.CleanSession) {
          Log.Info("{0}.state {1} => WILLTOPICREQ", Owner.path, state);
        }
        state=State.WillTopic;
        Send(new MsMessage(MsMessageType.WILLTOPICREQ));
      } else {
        if(msg.CleanSession) {
          Log.Info("{0}.state {1} => PreConnect", Owner.path, state);
          state=State.PreConnect;
        } else {
          state=State.Connected;
        }
        Send(new MsConnack(MsReturnCode.Accepted));
      }
      via=_gate.name;
      if(_statistic.value) {
        Stat(false, MsMessageType.CONNECT, msg.CleanSession);
      }
    }
    //TODO: Unsubscribe
    private void SetValue(TopicInfo ti, byte[] msgData, bool retained) {
      if(ti!=null) {
        if(!ti.path.StartsWith(Owner.path)) {
          return;     // not allow publish
        }
        object val;
        switch(Type.GetTypeCode(ti.topic.valueType)) {
        case TypeCode.Boolean:
          val=(msgData[0]!=0);
          break;
        case TypeCode.Int64: {
            long rv=(msgData[msgData.Length-1]&0x80)==0?0:-1;
            for(int i=msgData.Length-1; i>=0; i--) {
              rv<<=8;
              rv|=msgData[i];
            }
            val=rv;
          }
          break;
        case TypeCode.String:
          val=Encoding.Default.GetString(msgData);
          break;
        case TypeCode.Object:
          if(ti.topic.valueType==typeof(PLC.ByteArray)) {
            val=new PLC.ByteArray(msgData);
            break;
          } else if(ti.topic.valueType==typeof(SmartTwi)) {
            var sa=(ti.topic.GetValue() as SmartTwi);
            if(sa==null) {
              sa=new SmartTwi(ti.topic);
              sa.Recv(msgData);
              val=sa;
            } else {
              sa.Recv(msgData);
              return;
            }
            break;
          } else if(ti.topic.valueType==typeof(TWIDriver)) {
            var twi=(ti.topic.GetValue() as TWIDriver);
            if(twi==null) {
              twi=new TWIDriver(ti.topic);
              twi.Recv(msgData);
              val=twi;
            } else {
              twi.Recv(msgData);
              return;
            }
            break;
          } else if(ti.topic.valueType==typeof(DevicePLC)) {
            var plc=(ti.topic.GetValue() as DevicePLC);
            if(plc==null) {
              plc=new DevicePLC(ti.topic);
              plc.Recv(msgData);
              val=plc;
            } else {
              plc.Recv(msgData);
              return;
            }
            break;
          } else {
            return;
          }
        default:
          return;
        }
        ti.topic.saved=retained;
        ti.topic.SetValue(val, new TopicChanged(TopicChanged.ChangeArt.Value, Owner));
      }
    }
    private void Disconnect(ushort duration=0) {
      if(duration==0 && !string.IsNullOrEmpty(_willPath)) {
        TopicInfo ti = GetTopicInfo(_willPath, false);
        SetValue(ti, _wilMsg, false);
      }
      if(duration>0) {
        if(state==State.ASleep) {
          state=State.AWake;
        }
        ResetTimer(3100+duration*1550);  // t_wakeup
        this.Send(new MsDisconnect());
        _tryCounter=0;
        state=State.ASleep;
        var st=Owner.Get<long>(".cfg/XD_SleepTime", Owner);
        st.saved=true;
        st.SetValue((short)duration, new TopicChanged(TopicChanged.ChangeArt.Value, Owner) { Source=st });
      } else {
        _activeTimer.Change(Timeout.Infinite, Timeout.Infinite);
        this._gate=null;
        if(state!=State.Lost) {
          state=State.Disconnected;
          if(Owner!=null) {
            Log.Info("{0} Disconnected", Owner.path);
          }
        }
      }
      _waitAck=false;
    }
    private void OwnerChanged(Topic topic, TopicChanged param) {
      if(param.Art==TopicChanged.ChangeArt.Remove) {
        Send(new MsDisconnect());
        state=State.Disconnected;
        return;
      }
    }
    private void PublishTopic(Topic topic, TopicChanged param) {
      if(topic.valueType==null || topic==Owner) {
        return;
      }
      if(param.Art==TopicChanged.ChangeArt.Add) {
        if(topic.valueType==typeof(SmartTwi)) { // || (topic.parent!=null && topic.parent.valueType==typeof(SmartTwi))
          return;   // processed from SmartTwi
        }
        if(topic.valueType==typeof(TWIDriver)) {  //  || (topic.parent!=null && topic.parent.valueType==typeof(TWIDriver))
          return;   // processed from TWIDriver
        }
        if(topic.valueType==typeof(DevicePLC)) {
          return;   // processed from DevicePLC
        }
        GetTopicInfo(topic);
        return;
      }
      if(topic.name=="_via") {
        if(_gate==null) {
          if(string.IsNullOrEmpty(via)) {
            MsGSerial.Rescan();
          }
        }
      }
      if(!(state==State.Connected || state==State.ASleep || state==State.AWake) || param.Visited(Owner, true)) {
        return;
      }
      if(topic.valueType==typeof(SmartTwi)) { // || (topic.parent!=null && topic.parent.valueType==typeof(SmartTwi))
        return;   // processed from SmartTwi
      }
      if(topic.valueType==typeof(TWIDriver)) {  //  || (topic.parent!=null && topic.parent.valueType==typeof(TWIDriver))
        return;   // processed from TWIDriver
      }
      if(topic.valueType==typeof(DevicePLC)) {
        return;   // processed from DevicePLC
      }
      TopicInfo rez=null;
      for(int i=_topics.Count-1; i>=0; i--) {
        if(_topics[i].path==topic.path) {
          rez=_topics[i];
          break;
        }
      }
      if(rez==null && param.Art==TopicChanged.ChangeArt.Value) {
        rez=GetTopicInfo(topic, true);
      }
      if(rez==null || rez.TopicId>=0xFFC0 || !rez.registred) {
        return;
      }
      if(param.Art==TopicChanged.ChangeArt.Value) {
        Send(new MsPublish(rez.topic, rez.TopicId, param.Subscription.qos));
      } else {          // Remove by device
        if(rez.it==TopicIdType.Normal) {
          string tpc, tpc_n;
          if(rez.path.StartsWith(Owner.path)) {
            tpc = rez.path.Substring(Owner.path.Length + 1);
            if(varMapping != null && varMapping.TryGetValue(tpc, out tpc_n)) {
              tpc = tpc_n;
            }
          } else {
            tpc = rez.path;
          }
          Send(new MsRegister(0xFFFF, tpc));
        }
        _topics.Remove(rez);
      }
    }
    internal void PublishWithPayload(Topic t, byte[] payload) {
      if(state==State.Disconnected || state==State.Lost || _topics==null) {
        return;
      }
      TopicInfo rez=null;
      for(int i=_topics.Count-1; i>=0; i--) {
        if(_topics[i].path==t.path) {
          rez=_topics[i];
          break;
        }
      }
      if(rez==null) {
        return;
      }
      //if(_verbose.value) {
      //  Log.Debug("{0}.Snd {1}", t.name, BitConverter.ToString(payload));
      //}
      Send(new MsPublish(rez.topic, rez.TopicId, QoS.AtLeastOnce) { Data=payload });
    }

    /// <summary>Find or create TopicInfo by Topic</summary>
    /// <param name="tp">Topic as key</param>
    /// <param name="sendRegister">Send MsRegister for new TopicInfo</param>
    /// <returns>found TopicInfo or null</returns>
    private TopicInfo GetTopicInfo(Topic tp, bool sendRegister=true) {
      if(tp==null) {
        return null;
      }
      TopicInfo rez=null;
      for(int i=_topics.Count-1; i>=0; i--) {
        if(_topics[i].path==tp.path) {
          rez=_topics[i];
          break;
        }
      }
      string tpc=(tp.path.StartsWith(Owner.path))?tp.path.Substring(Owner.path.Length+1):tp.path;
      if(rez==null) {
        rez=new TopicInfo();
        rez.topic=tp;
        rez.path=tp.path;
        ushort rtId;
        if(PredefinedTopics.TryGetValue(tpc, out rtId)) {
          rez.TopicId=rtId;
          rez.it=TopicIdType.PreDefined;
          rez.registred=true;
        } else {
          Topic tmp=tp.parent;
          bool ignory=false;
          while(tmp!=null && tmp.valueType!=typeof(MsDevice)) {
            if(tmp.valueType==typeof(SmartTwi) || tmp.valueType==typeof(TWIDriver) || tmp.valueType==typeof(DevicePLC)) {
              ignory=true;
              break;
            }
            tmp=tmp.parent;
          }
          if(ignory) {
            rez.TopicId=0xFFFF;
            rez.it=TopicIdType.PreDefined;
            rez.registred=true;
          } else {
            rez.TopicId=CalculateTopicId(rez.path);
            rez.it=TopicIdType.Normal;
          }
        }
        _topics.Add(rez);
      }
      if(!rez.registred) {
        if(sendRegister) {
		  string tpc_n;
		  if(varMapping!=null && varMapping.TryGetValue(tpc, out tpc_n)) {
            Log.Debug("{0}.register {1} as {2}", Owner.path, tpc, tpc_n);
            tpc = tpc_n;
		  }
          Send(new MsRegister(rez.TopicId, tpc));
        } else {
          rez.registred=true;
        }
      }
      return rez;
    }
    private ushort CalculateTopicId(string path) {
      ushort id;
      byte[] buf=Encoding.UTF8.GetBytes(path);
      id=Crc16.ComputeChecksum(buf);
      while(id==0 || id==0xF000 || id==0xFFFF || _topics.Any(z => z.it==TopicIdType.Normal && z.TopicId==id)) {
        id=Crc16.UpdateChecksum(id, (byte)_rand.Next(0, 255));
      }
      return id;
    }
    private TopicInfo GetTopicInfo(string path, bool sendRegister=true) {
      Topic cur=null;
      int idx=path.LastIndexOf('/');
      string cName=path.Substring(idx+1);

      var rec=_NTTable.FirstOrDefault(z => cName.StartsWith(z.name));
      TopicInfo ret;
      if(rec.name!=null && !path.StartsWith("/local")) {
		KeyValuePair<string, string> kv;
		if(varMapping!=null && (kv=varMapping.FirstOrDefault(z=>z.Value==cName)).Value==cName){
		  if(idx>0) {
			path=path.Substring(0, idx)+kv.Key;
		  } else {
			path=kv.Key;
		  }
		}
        cur=Topic.GetP(path, rec.type, Owner, Owner);
        ret=GetTopicInfo(cur, sendRegister);
      } else {
        ret=null;
      }
      return ret;
    }
    private TopicInfo GetTopicInfo(ushort topicId, TopicIdType topicIdType, bool sendRegister=true) {
      TopicInfo rez=_topics.Find(z => z.it==topicIdType && z.TopicId==topicId);
      if(rez==null) {
        if(topicIdType==TopicIdType.PreDefined) {
          var cPath=PredefinedTopics.FirstOrDefault(z => z.Value==topicId).Key;
          if(cPath!=null) {
            rez=GetTopicInfo(cPath, sendRegister);
          }
        } else if(topicIdType==TopicIdType.ShortName) {
          rez=GetTopicInfo(string.Format("{0}{1}", (char)(topicId>>8), (char)(topicId & 0xFF)), sendRegister);
        }
        if(rez!=null) {
          rez.it=topicIdType;
        }
      }
      return rez;
    }
    private ushort NextMsgId() {
      int rez=Interlocked.Increment(ref _messageIdGen);
      Interlocked.CompareExchange(ref _messageIdGen, 1, 0xFFFF);
      //Log.Debug("{0}.MsgId={1:X4}", Owner.name, _sendBuf);
      return (ushort)rez;
    }
    private void SyncMsgId(ushort p) {
      ResetTimer();
      int nid=p;
      if(nid==0xFFFE) {
        nid++;
        nid++;
      }
      if(nid>(int)_messageIdGen || (nid<0x0100 && _messageIdGen>0xFF00)) {
        _messageIdGen=(ushort)nid;      // synchronize messageId
      }
      //Log.Debug("{0}.MsgIdGen={1:X4}, p={2:X4}", Owner.name, _messageIdGen, p);
    }
    private bool CheckAddr(byte[] addr) {
      Topic ta;
      if(addr==null) {
        return false;
      }
      if(this.Addr!=null && this.Addr.Length-1==addr.Length && this.Addr.Skip(1).SequenceEqual(addr)) {
        return true;
      }
      if(Owner!=null) {
        for(int i=0; i<3; i++) {
          if(Owner.Exist(string.Format(".cfg/_a_phy{0}", i), out ta) && ta.valueType==typeof(PLC.ByteArray)) {
            var act=(ta as DVar<PLC.ByteArray>).value;
            if(act!=null && act.GetBytes().Length==addr.Length && act.GetBytes().SequenceEqual(addr)) {
              return true;
            }
          }
        }
      }
      return false;
    }

    private void ProccessAcknoledge(MsMessage rMsg) {
      MsMessage msg=null;
      lock(_sendQueue) {
        MsMessage reqMsg;
        if(_sendQueue.Count>0 && (reqMsg=_sendQueue.Peek()).MsgTyp==rMsg.ReqTyp && reqMsg.MessageId==rMsg.MessageId) {
          _sendQueue.Dequeue();
          _waitAck=false;
          if(_sendQueue.Count>0 && !(msg=_sendQueue.Peek()).IsRequest) {
            _sendQueue.Dequeue();
          }
        }
      }
      if(msg==null && !_waitAck && state==State.AWake) {
        ReisePool(null);
        if(_waitAck) {
          return; // sended from pool
        }
      }
      if(msg!=null || state==State.AWake) {
        if(msg!=null && msg.IsRequest) {
          _tryCounter=2;
        }
        SendIntern(msg);
      } else if(!_waitAck) {
        ResetTimer();
      }
    }
    private void Send(MsMessage msg) {
      if(state!=State.Disconnected && state!=State.Lost) {
        bool send=true;
        if(msg.MessageId==0 && msg.IsRequest) {
          msg.MessageId=NextMsgId();
          lock(_sendQueue) {
            if(_sendQueue.Count>0 || state==State.ASleep) {
              send=false;
            }
            _sendQueue.Enqueue(msg);
          }
        }
        if(send) {
          if(msg.IsRequest) {
            _tryCounter=2;
          }
          SendIntern(msg);
        }
      }
    }
    private void SendIntern(MsMessage msg) {
      while(state==State.AWake || (msg!=null && (state!=State.ASleep || msg.MsgTyp==MsMessageType.DISCONNECT))) {
        if(msg!=null) {
          if(_gate!=null) {
            if(_statistic.value) {
              Stat(true, msg.MsgTyp, ((msg is MsPublish && (msg as MsPublish).Dup) || (msg is MsSubscribe && (msg as MsSubscribe).dup)));
            }
            try {
              _gate.SendGw(this, msg);
            }
            catch(ArgumentOutOfRangeException ex) {
              Log.Warning("{0} - {1}", this.name, ex.Message);
              if(msg.IsRequest) {
                lock(_sendQueue) {
                  if(_sendQueue.Count>0 && _sendQueue.Peek()==msg) {
                    _sendQueue.Dequeue();
                    _waitAck=false;
                  }
                }
              }
              msg=null;
            }
          }
          if(msg!=null && msg.IsRequest) {
            ResetTimer(_rand.Next(ACK_TIMEOUT, ACK_TIMEOUT*5/3)/(_tryCounter+1));  // 600, 1000
            _waitAck=true;
            break;
          }
          if(_waitAck) {
            break;
          }
        }
        msg=null;
        lock(_sendQueue) {
          if(_sendQueue.Count==0 && state==State.AWake) {
            if(_gate!=null) {
              _gate.SendGw(this, new MsMessage(MsMessageType.PINGRESP));
              if(_statistic.value) {
                Stat(true, MsMessageType.PINGRESP, false);
              }
            }
            var st=Owner.Get<long>(".cfg/XD_SleepTime", Owner);
            ResetTimer(st.value>0?(3100+(int)st.value*1550):_duration);  // t_wakeup
            state=State.ASleep;
            break;
          }
          if(_sendQueue.Count>0 && !(msg=_sendQueue.Peek()).IsRequest) {
            _sendQueue.Dequeue();
          }
        }
      }
    }
    private void ResetTimer(int period=0) {
      if(period==0) {
        if(_waitAck) {
          return;
        }
        if(_sendQueue.Count>0) {
          period=_rand.Next(ACK_TIMEOUT*3/4, ACK_TIMEOUT);  // 450, 600
        } else if(_duration>0) {
          period=_duration;
          _tryCounter=1;
        }
      }
      //Log.Debug("$ {0}._activeTimer={1}", Owner.name, period);
      _activeTimer.Change(period, Timeout.Infinite);
    }
    private void TimeOut(object o) {
      //Log.Debug("$ {0}.TimeOut _tryCounter={1}", Owner.name, _tryCounter);
      if(_tryCounter>0) {
        MsMessage msg=null;
        lock(_sendQueue) {
          if(_sendQueue.Count>0) {
            msg=_sendQueue.Peek();
          }
        }
        _waitAck=false;
        if(msg!=null) {
          _tryCounter--;
          SendIntern(msg);
        } else {
          ResetTimer();
          _tryCounter=0;
        }
        return;
      }
      state=State.Lost;
      if(Owner!=null) {
        Disconnect();
        if(_statistic.value) {
          Stat(false, MsMessageType.GWINFO);
        }
        Log.Warning("{0} Lost", Owner.path);
      }
      lock(_sendQueue) {
        _sendQueue.Clear();
      }
      if(_gate!=null) {
        _gate.SendGw(this, new MsDisconnect());
        if(_statistic.value) {
          Stat(true, MsMessageType.DISCONNECT, false);
        }
      }
    }

    internal event Action Pool;

    #region ITopicOwned Members
    void ITopicOwned.SetOwner(Topic owner) {
      if(Owner!=owner) {
        if(Owner!=null) {
          if(_subsscriptions!=null) {
            foreach(var s in _subsscriptions) {
              Owner.Unsubscribe(s.path, s.func);
            }
            _subsscriptions.Clear();
          }
          _stateVar=null;
          if(_activeTimer!=null) {
            _activeTimer.Change(Timeout.Infinite, Timeout.Infinite);
          }
        }
        Owner=owner;
        if(Owner!=null) {
          _stateVar=Owner.Get<State>(".cfg/_state");
          if(Topic.brokerMode) {
            Owner.saved=true;
            Owner.Get<string>(".cfg/_declarer").value="mqtts_cfg";
            var dc=Owner.Get<string>("_declarer", Owner);
            dc.saved=true;
            dc.value=_declarer;
            _present=Owner.Get<bool>("present", Owner);
            _present.saved=false;
            _present.value=(state==State.Connected || state==State.ASleep || state==State.AWake);

            Topic oldT;
            if(!string.IsNullOrEmpty(backName) && backName!=Owner.name && Owner.parent.Exist(backName, out oldT) && oldT.valueType==typeof(MsDevice)) {   // Device renamed
              MsDevice old=(oldT as DVar<MsDevice>).value;
              if(old!=null) {
                Addr=old.Addr;
                old._gate.SendGw(this, new MsPublish(null, PredefinedTopics["_sName"], QoS.AtLeastOnce) { MessageId=old.NextMsgId(), Data=Encoding.UTF8.GetBytes(Owner.name.Substring(0, Owner.name.Length)) });
                this.state=State.Disconnected;
              }
            }
            backName=Owner.name;
          }
        }
      }
    }
    #endregion ITopicOwned Members

    #region IMsGate Members
    public void SendGw(byte[] addr, MsMessage msg) {
      if(_gate!=null && addr!=null) {
        _gate.SendGw(this, new MsForward(addr, msg));
      }
    }
    public void SendGw(MsDevice dev, MsMessage msg) {
      if(_gate!=null) {
        _gate.SendGw(this, new MsForward(dev.Addr, msg));
      }
    }
    public byte gwIdx { get { return (byte)(_gate==null?0xFF:_gate.gwIdx); } }
    public byte gwRadius { get { return 0; } }
    #endregion  IMsGate Members

    public override string ToString() {
      if(Owner!=null) {
        if(state==State.Disconnected || state==State.Lost) {
          return state.ToString();
        } else {
          return string.Format("{0} via {1}", Addr==null?"N/A":BitConverter.ToString(Addr), via);
        }
      }
      return _declarer;
    }
    private class TopicInfo {
      public Topic topic;
      public ushort TopicId;
      public TopicIdType it;
      public bool registred;
      public string path;
    }
    private static NTRecord[] _NTTable= new NTRecord[]{ 
      new NTRecord("In", typeof(bool)),
      new NTRecord("Ip", typeof(bool)),
      new NTRecord("Op", typeof(bool)),
      new NTRecord("On", typeof(bool)),
      new NTRecord("OA", typeof(bool)),   // output high if active
      new NTRecord("Oa", typeof(bool)),   // output low if active
      new NTRecord("Ai", typeof(long)),   //uint16 Analog ref
      new NTRecord("AI", typeof(long)),   //uint16 Analog ref2
      new NTRecord("Av", typeof(long)),   //uint16
      new NTRecord("Ae", typeof(long)),   //uint16
      new NTRecord("Pp", typeof(long)),   //uint8 PWM positive[29, 30]
      new NTRecord("Pn", typeof(long)),   //uint8 PWM negative[29, 30]
      new NTRecord("_B", typeof(long)),   //uint8
      new NTRecord("_W", typeof(long)),   //uint16
      new NTRecord("_D", typeof(long)),   //uint32
      new NTRecord("_q", typeof(long)),   //int64
      new NTRecord("_s", typeof(string)),
      new NTRecord("_a", typeof(PLC.ByteArray)),
      new NTRecord("St", typeof(PLC.ByteArray)),  // Serial port transmit
      new NTRecord("Sr", typeof(PLC.ByteArray)),  // Serial port recieve
      new NTRecord("Tz", typeof(bool)),
      new NTRecord("Tb", typeof(long)),   //int8
      new NTRecord("TB", typeof(long)),   //uint8
      new NTRecord("Tw", typeof(long)),   //int16
      new NTRecord("TW", typeof(long)),   //uint16
      new NTRecord("Td", typeof(long)),   //int32
      new NTRecord("TD", typeof(long)),   //uint32
      new NTRecord("Tq", typeof(long)),   //int64
      new NTRecord("Ts", typeof(string)),
      new NTRecord("Ta", typeof(TWIDriver)),   // TWI >= ver 2.7
      new NTRecord("sa", typeof(SmartTwi)),    // Smart TWI
      new NTRecord("Xz", typeof(bool)),   // user defined
      new NTRecord("Xb", typeof(long)),   //int8
      new NTRecord("XB", typeof(long)),   //uint8
      new NTRecord("Xw", typeof(long)),   //int16
      new NTRecord("XW", typeof(long)),   //uint16
      new NTRecord("Xd", typeof(long)),   //int32
      new NTRecord("XD", typeof(long)),   //uint32
      new NTRecord("Xq", typeof(long)),   //int64
      new NTRecord("Xs", typeof(string)),
      new NTRecord("Xa", typeof(PLC.ByteArray)),
      new NTRecord("Mz", typeof(bool)),   // user defined
      new NTRecord("Mb", typeof(long)),   //int8
      new NTRecord("MB", typeof(long)),   //uint8
      new NTRecord("Mw", typeof(long)),   //int16
      new NTRecord("MW", typeof(long)),   //uint16
      new NTRecord("Md", typeof(long)),   //int32
      new NTRecord("MD", typeof(long)),   //uint32
      new NTRecord("Mq", typeof(long)),   //int64
      new NTRecord("Ms", typeof(string)),
      new NTRecord("Ma", typeof(PLC.ByteArray)),  // Merkers
      new NTRecord("pa", typeof(DevicePLC)),    // Program
      new NTRecord("_declarer", typeof(string)),
      new NTRecord("present", typeof(bool)),
    };
    internal static Dictionary<string, ushort> PredefinedTopics=new Dictionary<string, ushort>(){
      {"_sName",             0xFF00},
      {".cfg/XD_SleepTime",  0xFF01},
      {".cfg/XD_ADCintegrate",   0xFF08},

      {".cfg/XD_DeviceAddr", 0xFF10},
      {".cfg/XD_GroupID",    0xFF11},
      {".cfg/XD_Channel",    0xFF12},
      {".cfg/XD_GateId",     0xFF14},

      {".cfg/Xa_MACAddr",    0xFF20},
      {".cfg/Xa_IPAddr",     0xFF21},
      {".cfg/Xa_IPMask",     0xFF22},
      {".cfg/Xa_IPRouter",   0xFF23},
      {".cfg/Xa_IPBroker",   0xFF24},

      {"pa0/XD_StackBottom", 0xFF50},

      {"_declarer",          0xFFC0},
      {".cfg/_a_phy1",       0xFFC1},
      {".cfg/_a_phy2",       0xFFC2},
      {".cfg/_a_phy3",       0xFFC3},
      {".cfg/_a_phy4",       0xFFC4},
      {".cfg/XD_RSSI",       0xFFC8},

      {".cfg/_declarer",     0xFFD0},
      {".cfg/_state",        0xFFD1},
      {"present",            0xFFD2},
      {".cfg/_via",          0xFFD3},
      {"pa0/_declarer",     0xFFD8},

      {"_logD",              LOG_D_ID},
      {"_logI",              LOG_I_ID},
      {"_logW",              LOG_W_ID},
      {"_logE",              LOG_E_ID},
    };
    private struct NTRecord {
      public NTRecord(string name, Type type) {
        this.name=name;
        this.type=type;
      }
      public readonly string name;
      public readonly Type type;
    }


    public enum State {
      Disconnected=0,
      WillTopic,
      WillMsg,
      Connected,
      ASleep,
      AWake,
      Lost,
      PreConnect,
    }
  }
  public interface IMsGate {
    void SendGw(byte[] addr, MsMessage msg);
    void SendGw(MsDevice dev, MsMessage msg);
    byte gwIdx { get; }
    byte gwRadius { get; }
    string name { get; }
    string Addr2If(byte[] addr);
    void AddNode(MsDevice dev);
    void RemoveNode(MsDevice dev);
    void Stop();
  }
}
