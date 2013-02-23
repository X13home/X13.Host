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

namespace X13.MQTT {
  [Newtonsoft.Json.JsonObject(Newtonsoft.Json.MemberSerialization.OptIn)]
  public class MsDevice : ITopicOwned {
    private const int ACK_TIMEOUT=550;

    private int _duration=3000;
    private DVar<MsDeviceState> _stateVar;

    public MsDeviceState state {
      get { return _stateVar!=null?_stateVar.value:MsDeviceState.Disconnected; }
      private set {
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
            _present.value=(state==MsDeviceState.Connected || state==MsDeviceState.ASleep || state==MsDeviceState.AWake);
          }
          catch(ObjectDisposedException) {
            _present=null;
          }
        }
      }
    }
    private string _willPath;
    private byte[] _wilMsg;
    private bool _willRetain;
    private Timer _activeTimer;
    // TODO: Save/Restore _topics & _subsscriptions
    private List<TopicInfo> _topics;
    private List<Topic.Subscription> _subsscriptions;
    private Queue<MsMessage> _sendQueue;
    private int _tryCounter;
    private int _topicIdGen=0;
    private int _messageIdGen=0;
    private byte _addr;
    private DVar<bool> _present;

    internal MsDevice() {
      if(Topic.brokerMode) {
        _activeTimer=new Timer(new TimerCallback(TimeOut));
        _topics=new List<TopicInfo>(16);
        _subsscriptions=new List<Topic.Subscription>(4);
        _sendQueue=new Queue<MsMessage>();
      }
    }

    public byte Addr {
      get { return _addr; }
      set {
        _addr=value;
      }
    }

    public DVar<MsDevice> Owner { get; private set; }

    internal void Connect(MsConnect msg) {
      Addr=msg.Addr;
      _topicIdGen=0;
      if(msg.CleanSession) {
        foreach(var s in _subsscriptions) {
          Owner.Unsubscribe(s.path, s.func);
        }
        _subsscriptions.Clear();
        _topics.Clear();
        lock(_sendQueue) {
          _sendQueue.Clear();
        }
      } else {
        try {
          _topicIdGen=_topics.Where(z => z.it==TopicIdType.Normal).Max(z => z.TopicId);
        }
        catch(InvalidOperationException) {
          _topicIdGen=1;
        }
      }
      _duration=msg.Duration*1100;
      ResetTimer();
      if(msg.Will) {
        state=MsDeviceState.WillTopic;
        _willPath=string.Empty;
        _wilMsg=null;
        Send(new MsMessage(MsMessageType.WILLTOPICREQ));
      } else {
        if(state==MsDeviceState.Disconnected || state==MsDeviceState.Lost) {
          Log.Info("{0}.state={1}->connected", Owner.path, state);
        }
        state=MsDeviceState.Connected;
        Send(new MsConnack(MsReturnCode.Accepted));
      }
    }
    internal void WillTopic(MsWillTopic msg) {
      if(state==MsDeviceState.WillTopic) {
        _willPath=msg.Path;
        _willRetain=msg.Retain;
        state=MsDeviceState.WillMsg;
        ProccessAcknoledge(msg);
      }
    }
    internal void WillMsg(MsWillMsg msg) {
      if(state==MsDeviceState.WillMsg) {
        _wilMsg=msg.Payload;
        state=MsDeviceState.Connected;
        ProccessAcknoledge(msg);
        Send(new MsConnack(MsReturnCode.Accepted));
        Log.Info("{0} connected", Owner.path);
      }
    }
    internal void Register(MsRegister msg) {
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
    internal void RegAck(MsRegAck msg) {
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
    internal void Subscibe(MsSubscribe msg) {
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
      //if(s!=null) {
        Send(new MsSuback(msg.qualityOfService, topicId, msg.MessageId, MsReturnCode.Accepted));
        s=Owner.Subscribe(msg.path, PublishTopic, msg.qualityOfService);
        _subsscriptions.Add(s);
      //} else {
      //  Send(new MsSuback(QoS.AtMostOnce, topicId, msg.MessageId, MsReturnCode.InvalidTopicId));
      //}
    }
    internal void Publish(MsPublish msg) {
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
    //TODO: Unsubscribe
    private void SetValue(TopicInfo ti, byte[] msgData) {
      if(ti!=null) {
        object val;
        switch(Type.GetTypeCode(ti.topic.valueType)) {
        case TypeCode.Boolean:
          val=(msgData[0]!=0);
          break;
        case TypeCode.Int64: {
          long rv=0;
          for(int i=msgData.Length-1;i>=0;i--){
          //for(int i=0;i<msgData.Length;i++){
            rv<<=8;
            rv|=msgData[i];
          }
          val=rv;
          Log.Debug("{0}={1}, {2}", ti.path, rv, Convert.ToString(msgData));
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
    internal void PubAck(MsPubAck msg) {
      ProccessAcknoledge(msg);
    }
    internal void PingReq(MsPingReq msg) {
      if(state==MsDeviceState.ASleep) {
        if(string.IsNullOrEmpty(msg.ClientId) || msg.ClientId==Owner.name) {
          state=MsDeviceState.AWake;
          ProccessAcknoledge(msg);    // resume send proccess
        } else {
          Send(new MsDisconnect());
          state=MsDeviceState.Lost;
          Log.Warning("{0} PingReq from unknown device: {1}", Owner.path, msg.ClientId);
        }
      } else {
        ResetTimer();
        Send(new MsMessage(MsMessageType.PINGRESP));
      }
    }
    internal void Disconnect(ushort duration=0) {
      if(!string.IsNullOrEmpty(_willPath)) {
        TopicInfo ti = GetTopicInfo(_willPath, false);
        SetValue(ti, _wilMsg);
      }
      if(duration>0) {
        ResetTimer(duration*1550);
        this.Send(new MsDisconnect());
        _tryCounter=0;
        state=MsDeviceState.ASleep;
        var st=Owner.Get<long>(PredefinedTopics._WSleepTime.ToString(), Owner);
        st.saved=true;
        st.SetValue((short)duration, new TopicChanged(TopicChanged.ChangeArt.Value, Owner){ Source=st });
      } else if(state!=MsDeviceState.Lost) {
        state=MsDeviceState.Disconnected;
        if(Owner!=null) {
          Log.Info("{0} Disconnected", Owner.path);
        }
        _activeTimer.Dispose();
        _activeTimer=null;
      }
    }
    private void OwnerChanged(Topic topic, TopicChanged param) {
      if(param.Art==TopicChanged.ChangeArt.Remove) {
        Send(new MsDisconnect());
        state=MsDeviceState.Disconnected;
        return;
      }
    }

    internal void PublishTopic(Topic topic, TopicChanged param) {
      if(param.Art==TopicChanged.ChangeArt.Add) {
        var ti=GetTopicInfo(topic);
        return;
      }
      if(state==MsDeviceState.Disconnected || state==MsDeviceState.Lost || param.Visited(Owner, true)) {
        return;
      }
      TopicInfo rez=_topics.FirstOrDefault(ti => ti.path==topic.path);
      if(rez==null && param.Art==TopicChanged.ChangeArt.Value) {
        rez=GetTopicInfo(topic, true);
      }
      if(rez==null || rez.TopicId>=0xFF00 || rez.TopicId==0xFE00 || !rez.registred) {
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
        PredefinedTopics rtId;
        if(Enum.TryParse(tpc, false, out rtId)) {
          rez.TopicId=(ushort)rtId;
          rez.it=TopicIdType.PreDefined;
          rez.registred=true;
        } else {
          rez.TopicId=(ushort)Interlocked.Increment(ref _topicIdGen);
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
          PredefinedTopics a=(PredefinedTopics)topicId;
          if(Enum.IsDefined(typeof(PredefinedTopics), a)) {
            string cPath=Enum.GetName(typeof(PredefinedTopics), a);
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
      if(msg!=null || state==MsDeviceState.AWake) {
        if(msg!=null && msg.IsRequest) {
          _tryCounter=2;
        }
        SendIntern(msg);
      }
    }
    private void Send(MsMessage msg) {
      if((state!=MsDeviceState.Disconnected && state!=MsDeviceState.Lost) || (msg.MsgTyp==MsMessageType.DISCONNECT || msg.MsgTyp==MsMessageType.PINGRESP)) {
        msg.Addr=this.Addr;
        bool send=true;
        if(msg.MessageId==0 && (msg.MsgTyp==MsMessageType.PUBLISH?(msg as MsPublish).qualityOfService!=QoS.AtMostOnce:msg.IsRequest)) {
          msg.MessageId=NextMsgId();
          lock(_sendQueue) {
            if(_sendQueue.Count>0 || state==MsDeviceState.ASleep) {
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
      MsGateway g;
      if(Owner==null || (g=(Owner.parent as DVar<MsGateway>).value)==null) {
        return;
      }
      while((msg!=null ||state==MsDeviceState.AWake) && (state!=MsDeviceState.ASleep || (msg.MsgTyp==MsMessageType.DISCONNECT || msg.MsgTyp==MsMessageType.PINGRESP))) {
        if(msg!=null) {
          g.Send(msg);
        }
        if(msg!=null && msg.IsRequest) {
          ResetTimer(ACK_TIMEOUT);
          break;
        } else {
          msg=null;
          lock(_sendQueue) {
            if(_sendQueue.Count==0 && state==MsDeviceState.AWake) {
              g.Send(new MsMessage(MsMessageType.PINGRESP) { Addr=this.Addr });
              state=MsDeviceState.ASleep;
              break;
            }
            if(_sendQueue.Count>0 && !(msg=_sendQueue.Peek()).IsRequest) {
              _sendQueue.Dequeue();
            //} else if(msg!=null && msg.MsgTyp==MsMessageType.PUBLISH && (msg as MsPublish).Dup) {
            //  break;
            }
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
      state=MsDeviceState.Lost;
      if(Owner!=null) {
        Disconnect();
        Log.Warning("{0} Lost", Owner.path);
      }
      lock(_sendQueue) {
        _sendQueue.Clear();
      }
      SendIntern(new MsDisconnect() { Addr=this.Addr });
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
        Owner=owner as DVar<MsDevice>;
        if(Topic.brokerMode && Owner!=null) {
          Owner.saved=true;
          _stateVar=Owner.Get<MsDeviceState>(PredefinedTopics._state.ToString());
          var dc=Owner.Get<string>(PredefinedTopics._declarer.ToString(), Owner);
          dc.saved=true;
          dc.value=_declarer;
          var st=Owner.Get<long>(PredefinedTopics._WSleepTime.ToString(), Owner);
          st.saved=true;
          _present=Owner.Get<bool>(PredefinedTopics.present.ToString(), Owner);
          _present.value=(state==MsDeviceState.Connected || state==MsDeviceState.ASleep || state==MsDeviceState.AWake);

          if(!string.IsNullOrEmpty(backName) && backName!=Owner.name && Owner.parent.Exist(backName)) {   // Device renamed
            var old=Owner.parent.Get<MsDevice>(backName);
            if(old!=null && old.value!=null) {
              _addr=old.value._addr;
              _stateVar.value=old.value._stateVar.value;
              Send(new MsPublish(null, (ushort)PredefinedTopics._sName, QoS.AtLeastOnce) { Data=Encoding.UTF8.GetBytes(Owner.name.Substring(0, Owner.name.Length)) });
              Send(new MsDisconnect());
            }
          }
          backName=Owner.name;
        }
      }
    }
    #endregion ITopicOwned Members

    private string _declarer="RF12_Default";

    [Newtonsoft.Json.JsonProperty]
    private string backName { get; set; }

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
                                          new NTRecord("_B", typeof(long)),   //uint8
                                          new NTRecord("Pp", typeof(long)),   //uint8 PWM positive[29, 30]
                                          new NTRecord("Pn", typeof(long)),   //uint8 PWM negative[29, 30]
                                          new NTRecord("_W", typeof(long)),   //uint16
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
                                          new NTRecord(PredefinedTopics._declarer.ToString(), typeof(string)),
                                          new NTRecord(PredefinedTopics.present.ToString(), typeof(bool)),
                                        };
    private struct NTRecord {
      public NTRecord(string name, Type type) {
        this.name=name;
        this.type=type;
      }
      public readonly string name;
      public readonly Type type;
    }
  }

  internal enum TopicIdType {
    Normal=0,
    PreDefined=1,
    ShortName=2
  }
  internal enum PredefinedTopics : ushort {
    _declarer=0xFE00,
    _DeviceAddr=0xFE01,
    _WGroupID=0xFE02,
    _BChannel=0xFE03,
    _sName=0xFE04,
    _WSleepTime=0xFE05,
    _BRSSI=0xFE08,
    _state=0xFF01,
    present=0xFF02,
  }
  public enum MsDeviceState {
    Disconnected=0,
    WillTopic,
    WillMsg,
    Connected,
    ASleep,
    AWake,
    Lost,
  }
}
