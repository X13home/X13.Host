#region license
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
using System.Threading;
using X13.PLC;
using X13.MQTT;
using X13.WOUM;

namespace X13.MQTT {
  [Newtonsoft.Json.JsonObject(Newtonsoft.Json.MemberSerialization.OptIn)]
  public partial class MsDevice : ITopicOwned {
    private const int ACK_TIMEOUT=550;
    private static DVar<bool> _verbose;
    private static List<IMsGate> _gates;

    static MsDevice() {
      _verbose=Topic.root.Get<bool>("/system/Broker/MQTTS/verboseLog");
      _verbose.value=true;
      _gates=new List<IMsGate>();
    }
    internal static void Open() {
      MsGUdp.Open();
      MsGSerial.Open();
    }
    private static void PrintPacket(MsDevice dev, MsMessage msg, byte[] buf) {
      if(_verbose) {
        Log.Debug("r {4:X2}:{0}:{1} \t{2}:{3}", BitConverter.ToString(msg.Addr??new byte[0]), BitConverter.ToString(buf??new byte[0]), (dev!=null && dev.Owner!=null)?dev.Owner.name:string.Empty, msg.ToString(), (dev!=null && dev._gate!=null)?dev._gate.gwIdx:0xFF);
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
    private string _declarer="RF12_Default";
    private int _tryCounter;
    private int _messageIdGen=0;
    private DVar<bool> _present;
    private IMsGate _gate;

    private MsDevice() {
      if(Topic.brokerMode) {
        _activeTimer=new Timer(new TimerCallback(TimeOut));
        _topics=new List<TopicInfo>(16);
        _subsscriptions=new List<Topic.Subscription>(4);
        _sendQueue=new Queue<MsMessage>();
      }
    }

    public State state {
      get { return _stateVar!=null?_stateVar.value:State.Disconnected; }
      protected set {
        if(_stateVar!=null) {
          try {
            _stateVar.value=value;
          }
          catch(ObjectDisposedException) {
            _stateVar=null;
          }
        }
        if(_present!=null) {
          try {
            _present.value=(state==State.Connected || state==State.ASleep || state==State.AWake);
          }
          catch(ObjectDisposedException) {
            _present=null;
          }
        }
      }
    }
    public Topic Owner { get; private set; }

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


    private void ParseInPacket(byte[] buf) {
      var msgTyp=(MsMessageType)(buf[0]>1?buf[1]:buf[2]);
      switch(msgTyp) {
      case MsMessageType.WILLTOPIC: {
          var msg=new MsWillTopic(buf) { Addr=this.Addr };
          PrintPacket(this, msg, buf);
          if(state==State.WillTopic) {
            _willPath=msg.Path;
            _willRetain=msg.Retain;
            state=State.WillMsg;
            ProccessAcknoledge(msg);
          }
        }
        break;
      case MsMessageType.WILLMSG: {
          var msg=new MsWillMsg(buf) { Addr=this.Addr };
          PrintPacket(this, msg, buf);
          if(state==State.WillMsg) {
            _wilMsg=msg.Payload;
            state=State.Connected;
            ProccessAcknoledge(msg);
            Send(new MsConnack(MsReturnCode.Accepted));
            Log.Info("{0} connected", Owner.path);
          }
        }
        break;
      case MsMessageType.SUBSCRIBE: {
          var msg=new MsSubscribe(buf) { Addr=this.Addr };
          PrintPacket(this, msg, buf);

          SyncMsgId(msg.MessageId);
          Topic.Subscription s=null;
          ushort topicId=msg.topicId;
          if(msg.topicIdType!=TopicIdType.Normal || msg.path.IndexOfAny(new[] { '+', '#' })<0) {
            TopicInfo ti=null;
            if(msg.topicIdType==TopicIdType.Normal) {
              ti=GetTopicInfo(msg.path, false);
            } else {
              ti=GetTopicInfo(msg.topicId, msg.topicIdType);
            }
            topicId=ti.TopicId;
          }
          Send(new MsSuback(msg.qualityOfService, topicId, msg.MessageId, MsReturnCode.Accepted));
          s=Owner.Subscribe(msg.path, PublishTopic, msg.qualityOfService);
          _subsscriptions.Add(s);
        }
        break;
      case MsMessageType.REGISTER: {
          var msg=new MsRegister(buf) { Addr=this.Addr };
          PrintPacket(this, msg, buf);
          ResetTimer();
          try {
            TopicInfo ti = GetTopicInfo(msg.TopicPath, false);
            Send(new MsRegAck(ti.TopicId, msg.MessageId, MsReturnCode.Accepted));
          }
          catch(Exception) {
            Send(new MsRegAck(0, msg.MessageId, MsReturnCode.NotSupportes));
            Log.Warning("Unknown variable type by register {0}, {1}", Owner.path, msg.TopicPath);
          }
        }
        break;
      case MsMessageType.REGACK: {
          var msg=new MsRegAck(buf) { Addr=this.Addr };
          PrintPacket(this, msg, buf);
          ProccessAcknoledge(msg);
          TopicInfo ti=_topics.FirstOrDefault(z => z.TopicId==msg.TopicId);
          if(ti==null) {
            if(msg.TopicId!=0) {
              Log.Warning("{0} RegAck({1:X4}) for unknown variable", Owner.path, msg.TopicId);
            }
            return;
          }
          if(msg.RetCode==MsReturnCode.Accepted) {
            ti.registred=true;
            if(ti.it!=TopicIdType.PreDefined) {
              Send(new MsPublish(ti.topic, ti.TopicId, QoS.AtLeastOnce));
            }
          } else {
            Log.Warning("{0} registred failed: {1}", ti.path, msg.RetCode.ToString());
            _topics.Remove(ti);
            ti.topic.Remove();
          }
        }
        break;
      case MsMessageType.PUBLISH: {
          var msg=new MsPublish(buf) { Addr=this.Addr };
          PrintPacket(this, msg, buf);
          TopicInfo ti=_topics.Find(z => z.TopicId==msg.TopicId && z.it==msg.topicIdType);
          if(ti==null && msg.topicIdType!=TopicIdType.Normal) {
            ti=GetTopicInfo(msg.TopicId, msg.topicIdType, false);
          }
          if(msg.qualityOfService==QoS.AtMostOnce) {
            ResetTimer();
          } else if(msg.qualityOfService==QoS.AtLeastOnce) {
            SyncMsgId(msg.MessageId);
            Send(new MsPubAck(msg.TopicId, msg.MessageId, ti!=null?MsReturnCode.Accepted:MsReturnCode.InvalidTopicId));
          } else if(msg.qualityOfService==QoS.ExactlyOnce) {
            SyncMsgId(msg.MessageId);
            // QoS2 not supported, use QoS1
            Send(new MsPubAck(msg.TopicId, msg.MessageId, ti!=null?MsReturnCode.Accepted:MsReturnCode.InvalidTopicId));
          } else {
            throw new NotSupportedException("QoS -1 not supported "+Owner.path);
          }
          SetValue(ti, msg.Data);
        }
        break;
      case MsMessageType.PUBACK: {
          var msg=new MsPubAck(buf) { Addr=this.Addr };
          PrintPacket(this, msg, buf);
          ProccessAcknoledge(msg);
        }
        break;
      case MsMessageType.PINGREQ: {
          var msg=new MsPingReq(buf) { Addr=this.Addr };
          PrintPacket(this, msg, buf);
          if(state==State.ASleep) {
            if(string.IsNullOrEmpty(msg.ClientId) || msg.ClientId==Owner.name) {
              state=State.AWake;
              ProccessAcknoledge(msg);    // resume send proccess
            } else {
              Send(new MsDisconnect());
              state=State.Lost;
              Log.Warning("{0} PingReq from unknown device: {1}", Owner.path, msg.ClientId);
            }
          } else {
            ResetTimer();
            if(_gate!=null) {
              _gate.Send(new MsMessage(MsMessageType.PINGRESP) { Addr=this.Addr });
            }
          }
        }
        break;
      case MsMessageType.DISCONNECT: {
          var msg=new MsDisconnect(buf) { Addr=this.Addr };
          PrintPacket(this, msg, buf);
          Disconnect(msg.Duration);
        }
        break;
      default:
        Log.Warning("{0} unknown packet: {1}", Owner!=null?Owner.path:"null", BitConverter.ToString(buf));
        break;
      }
    }

    private void Connect(MsConnect msg) {
      Addr=msg.Addr;
      if(msg.CleanSession) {
        foreach(var s in _subsscriptions) {
          Owner.Unsubscribe(s.path, s.func);
        }
        _subsscriptions.Clear();
        _topics.Clear();
        lock(_sendQueue) {
          _sendQueue.Clear();
        }
      }
      _duration=msg.Duration*1100;
      ResetTimer();
      if(msg.Will) {
        _willPath=string.Empty;
        _wilMsg=null;
        if(state!=State.ASleep) {
          Log.Info("{0}.state {1} => WILLTOPICREQ", Owner.path, state);
        }
        state=State.WillTopic;
        Send(new MsMessage(MsMessageType.WILLTOPICREQ));
      } else {
        if(state!=State.ASleep) {
          Log.Info("{0}.state {1} => Connected", Owner.path, state);
        }
        state=State.Connected;
        Send(new MsConnack(MsReturnCode.Accepted));
      }
    }
    //TODO: Unsubscribe
    private void SetValue(TopicInfo ti, byte[] msgData) {
      if(ti!=null) {
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
            //Log.Debug("{0}={1}, {2}", ti.path, rv, BitConverter.ToString(msgData));
          }
          break;
        case TypeCode.String:
          val=Encoding.Default.GetString(msgData);
          break;
        case TypeCode.Object:
          if(ti.topic.valueType==typeof(byte[])) {
            val=msgData;
            break;
          } else {
            return;
          }
        default:
          return;
        }
        ti.topic.SetValue(val, new TopicChanged(TopicChanged.ChangeArt.Value, Owner));
      }
    }
    private void Disconnect(ushort duration=0) {
      if(duration==0 && !string.IsNullOrEmpty(_willPath)) {
        TopicInfo ti = GetTopicInfo(_willPath, false);
        SetValue(ti, _wilMsg);
      }
      if(duration>0) {
        ResetTimer(duration*1550);
        this.Send(new MsDisconnect());
        _tryCounter=0;
        state=State.ASleep;
        var st=Owner.Get<long>(".cfg/XD_SleepTime", Owner);
        st.saved=true;
        st.SetValue((short)duration, new TopicChanged(TopicChanged.ChangeArt.Value, Owner) { Source=st });
      } else if(state!=State.Lost) {
        state=State.Disconnected;
        if(Owner!=null) {
          Log.Info("{0} Disconnected", Owner.path);
        }
        _activeTimer.Change(Timeout.Infinite, Timeout.Infinite);
      }
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
      if(state==State.Disconnected || state==State.Lost || param.Visited(Owner, true)) {
        return;
      }
      TopicInfo rez=_topics.FirstOrDefault(ti => ti.path==topic.path);
      if(rez==null && param.Art==TopicChanged.ChangeArt.Value) {
        rez=GetTopicInfo(topic, true);
      }
      if(rez==null || rez.TopicId>=0xFFC0 || !rez.registred) {
        return;
      }
      if(param.Art==TopicChanged.ChangeArt.Value) {
        Send(new MsPublish(rez.topic, rez.TopicId, param.Subscription.qos));
      } else {          // Remove by device
        Send(new MsRegister(0, rez.path.StartsWith(Owner.path)?rez.path.Remove(0, Owner.path.Length+1):rez.path));
        _topics.Remove(rez);
      }
    }

    /// <summary>Find or create TopicInfo by Topic</summary>
    /// <param name="tp">Topic as key</param>
    /// <param name="sendRegister">Send MsRegister for new TopicInfo</param>
    /// <returns>found TopicInfo or null</returns>
    private TopicInfo GetTopicInfo(Topic tp, bool sendRegister=true) {
      if(tp==null) {
        return null;
      }
      TopicInfo rez=_topics.FirstOrDefault(ti => ti.path==tp.path);
      string tpc=(tp.path.StartsWith(Owner.path))?tp.path.Remove(0, Owner.path.Length+1):tp.path;
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
          rez.TopicId=CalculateTopicId(rez.path);
          rez.it=TopicIdType.Normal;
        }
        _topics.Add(rez);
      }
      if(!rez.registred) {
        if(sendRegister) {
          Send(new MsRegister(rez.TopicId, tpc));
        } else {
          rez.registred=true;
        }
      }
      return rez;
    }
    private ushort CalculateTopicId(string path) {
      byte[] buf=Encoding.UTF8.GetBytes(path);
      ushort id=Crc16.ComputeChecksum(buf);
      while(id==0 || id==0xF000 || _topics.Any(z => z.it==TopicIdType.Normal && z.TopicId==id)) {
        if(id==0 || id==0xF000) {
          Log.Warning("{0} restrickted id={1:X4}", path, id);
        } else {
          var dup=_topics.Find(z => z.it==TopicIdType.Normal && z.TopicId==id);
          Log.Warning("{0} id {1:X4} already used as {2}", path, id, dup.path);
        }
        id=Crc16.UpdateChecksum(id, 0);
      }
      return id;
    }
    private TopicInfo GetTopicInfo(string path, bool sendRegister=true) {
      Topic cur=null;
      int idx=path.LastIndexOf('/');
      string cName=path.Substring(idx+1);

      var rec=_NTTable.FirstOrDefault(z => cName.StartsWith(z.name));
      TopicInfo ret;
      if(rec.name!=null) {
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
      //Log.Debug("{0}.MsgId={1:X4}", Owner.name, rez);
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

    private void ProccessAcknoledge(MsMessage rMsg) {
      ResetTimer();
      MsMessage msg=null;
      lock(_sendQueue) {
        MsMessage reqMsg;
        if(_sendQueue.Count>0 && (reqMsg=_sendQueue.Peek()).MsgTyp==rMsg.ReqTyp && reqMsg.MessageId==rMsg.MessageId) {
          _sendQueue.Dequeue();
        }
        if(_sendQueue.Count>0 && !(msg=_sendQueue.Peek()).IsRequest) {
          _sendQueue.Dequeue();
        }
      }
      if(msg!=null || state==State.AWake) {
        if(msg!=null && msg.IsRequest) {
          _tryCounter=2;
        }
        SendIntern(msg);
      }
    }
    private void Send(MsMessage msg) {
      if(state!=State.Disconnected && state!=State.Lost) {
        msg.Addr=this.Addr;
        bool send=true;
        if(msg.MessageId==0 && (msg.MsgTyp==MsMessageType.PUBLISH?(msg as MsPublish).qualityOfService!=QoS.AtMostOnce:msg.IsRequest)) {
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
      while((msg!=null || state==State.AWake) && state!=State.ASleep) {
        if(msg!=null) {
          if(_gate!=null) {
            _gate.Send(msg);
          }
          if(msg.IsRequest) {
            ResetTimer(ACK_TIMEOUT);
            break;
          }
        }
        msg=null;
        lock(_sendQueue) {
          if(_sendQueue.Count==0 && state==State.AWake) {
            if(_gate!=null) {
              _gate.Send(new MsMessage(MsMessageType.PINGRESP) { Addr=this.Addr });
            }
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
        if(_sendQueue.Count>0) {
          period=ACK_TIMEOUT;
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
        if(msg!=null) {
          SendIntern(msg);
          _tryCounter--;
        } else {
          ResetTimer();
          _tryCounter=0;
        }
        return;
      }
      state=State.Lost;
      if(Owner!=null) {
        Disconnect();
        Log.Warning("{0} Lost", Owner.path);
      }
      lock(_sendQueue) {
        _sendQueue.Clear();
      }
      if(_gate!=null) {
        _gate.Send(new MsDisconnect() { Addr=this.Addr });
      }
    }

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
            _present.value=(state==State.Connected || state==State.ASleep || state==State.AWake);

            Topic oldT;
            if(!string.IsNullOrEmpty(backName) && backName!=Owner.name && Owner.parent.Exist(backName, out oldT) && oldT.valueType==typeof(MsDevice)) {   // Device renamed
              MsDevice old=(oldT as DVar<MsDevice>).value;
              if(old!=null) {
                Addr=old.Addr;
                old.Send(new MsPublish(null, PredefinedTopics["_sName"], QoS.AtLeastOnce) { Data=Encoding.UTF8.GetBytes(Owner.name.Substring(0, Owner.name.Length)) });
                old.Send(new MsDisconnect());
              }
            }
            backName=Owner.name;
          }
        }
      }
    }
    #endregion ITopicOwned Members
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
      new NTRecord("Ai", typeof(long)),   //uint16
      new NTRecord("Av", typeof(long)),   //uint16
      new NTRecord("Ae", typeof(long)),   //uint16
      new NTRecord("Pp", typeof(long)),   //uint8 PWM positive[29, 30]
      new NTRecord("Pn", typeof(long)),   //uint8 PWM negative[29, 30]
      new NTRecord("_B", typeof(long)),   //uint8
      new NTRecord("_W", typeof(long)),   //uint16
      new NTRecord("_D", typeof(long)),   //uint32
      new NTRecord("_s", typeof(string)),
      new NTRecord("St", typeof(string)),  // Serial port transmit
      new NTRecord("Sr", typeof(string)),  // Serial port recieve
      new NTRecord("Tz", typeof(bool)),
      new NTRecord("Tb", typeof(long)),   //int8
      new NTRecord("TB", typeof(long)),   //uint8
      new NTRecord("Tw", typeof(long)),   //int16
      new NTRecord("TW", typeof(long)),   //uint16
      new NTRecord("Td", typeof(long)),   //int32
      new NTRecord("TD", typeof(long)),   //uint32
      new NTRecord("Ts", typeof(string)),
      new NTRecord("Ta", typeof(byte[])),
      new NTRecord("Xz", typeof(bool)),   // user defined
      new NTRecord("Xb", typeof(long)),   //int8
      new NTRecord("XB", typeof(long)),   //uint8
      new NTRecord("Xw", typeof(long)),   //int16
      new NTRecord("XW", typeof(long)),   //uint16
      new NTRecord("Xd", typeof(long)),   //int32
      new NTRecord("XD", typeof(long)),   //uint32
      new NTRecord("Xs", typeof(string)),
      new NTRecord("Xa", typeof(byte[])),
      new NTRecord("_declarer", typeof(string)),
      new NTRecord("present", typeof(bool)),
    };
    internal static Dictionary<string, ushort> PredefinedTopics=new Dictionary<string, ushort>(){
      {"_sName",            0xFF00},
      {".cfg/XD_SleepTime",  0xFF01},

      {".cfg/XD_DeviceAddr", 0xFF10},
      {".cfg/XD_GroupID",    0xFF11},
      {".cfg/XD_Channel",    0xFF12},
      {".cfg/XD_RSSI",       0xFF13},

      {".cfg/XD_MACAddr",    0xFF20},
      {".cfg/XD_IPAddr",     0xFE21},
      {".cfg/XD_IPMask",     0xFE22},
      {".cfg/XD_IPRouter",   0xFE23},
      //{".cfg/XD_IPBroker",    0xFE24},

      {"_declarer",         0xFFC0},
      {".cfg/_state",        0xFFC1},
      {"present",           0xFFC2},
      {".cfg/_via",          0xFFC3},
      {".cfg/_declarer",     0xFFD0},
    };

    private struct NTRecord {
      public NTRecord(string name, Type type) {
        this.name=name;
        this.type=type;
      }
      public readonly string name;
      public readonly Type type;
    }

    private interface IMsGate {
      void Send(MsMessage msg);
      byte gwIdx { get; }
      //TODO: Stop
    }
    public enum State {
      Disconnected=0,
      WillTopic,
      WillMsg,
      Connected,
      ASleep,
      AWake,
      Lost,
    }
  }
}
