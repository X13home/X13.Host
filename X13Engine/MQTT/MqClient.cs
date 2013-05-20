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
using System.Net.Sockets;
using System.Net;
using System.IO;
using System.Threading;
using System.Diagnostics;
using System.ComponentModel.Composition;
using Newtonsoft.Json;

namespace X13.MQTT {
  [Export(typeof(IPlugModul))]
  [ExportMetadata("priority", 2)]
  [ExportMetadata("name", "Client")]
  public class MqClient : IPlugModul {
    private static JsonConverter[] _jcs=new JsonConverter[] { new Newtonsoft.Json.Converters.JavaScriptDateTimeConverter() };

    private string addr;
    private int port;
    private DVar<MqClient> _owner;
    private MqStreamer _stream;
    private static Topic _mq;
    private Topic _settings;
    private DVar<DateTime> _now;
    private DVar<long> _nowOffset;

    private bool _waitPingResp;
    private bool _connected;
    private Timer _tOut;
    private Timer _tLoaded;
    private int _keepAliveMS=89950;  // 90 sec
    private MqConnect ConnInfo;
    private List<Topic.Subscription> _subs;
    private DVar<bool> _verbose;
    private AutoResetEvent _eLoaded;

    public ushort KeepAlive {
      get { return (ushort)(_keepAliveMS>0?(_keepAliveMS+50)/1000:0); }
      set {
        if(!_connected) {             // can not inform the broker only befor connect
          _keepAliveMS=value>0?value*1000-50:Timeout.Infinite;
        }
      }
    }
    public string BrokerName { get; private set; }
    public event Action<bool> StatusChg;

    public MqClient() {
      _waitPingResp=false;
      _mq=Topic.root.Get("/local/MQ");
      ConnInfo=new MqConnect();
      ConnInfo.cleanSession=true;
      ConnInfo.keepAlive=this.KeepAlive;
      _tOut=new Timer(new TimerCallback(TimeOut));
      _settings=Topic.root.Get("/local/cfg/Client");
      _subs=new List<Topic.Subscription>();
      _now=Topic.root.Get<DateTime>("/var/now");
      _nowOffset=_settings.Get<long>("TimeOffset");
      _eLoaded=new AutoResetEvent(false);
    }
    public void Start() {
      if(!Reconnect()) {
        _settings.Get<bool>("enable").value=false;
        return;
      }
      Topic.SubscriptionsChg+=Topic_SubscriptionsChg;
      _verbose=_settings.Get<bool>("verbose");
      Topic.root.Subscribe("/etc/system/#", PLC.PLCPlugin.L_dummy);
      Topic.root.Subscribe("/etc/repository/#", PLC.PLCPlugin.L_dummy);
      Topic.root.Subscribe("/etc/declarers/+", PLC.PLCPlugin.L_dummy);
      Topic.root.Subscribe("/etc/declarers/type/#", PLC.PLCPlugin.L_dummy);
      Topic.root.Subscribe("/var/now", PLC.PLCPlugin.L_dummy);
      _eLoaded.Reset();
      _eLoaded.WaitOne(7000);
    }

    private bool Reconnect(bool slow=false) {
      if(_stream!=null) {
        _tLoaded.Change(Timeout.Infinite, Timeout.Infinite);
        if(_connected) {
          _connected=false;
          if(StatusChg!=null) {
            StatusChg(_connected);
          }
          _tOut.Change(_keepAliveMS*2, Timeout.Infinite);
        } else {
          _tOut.Change(_keepAliveMS*(slow?10:5), Timeout.Infinite);
        }
        _stream.Close();
        _stream=null;
        return false;
      }
      if(slow) {
        _tOut.Change(_keepAliveMS*5, Timeout.Infinite);
        return false;
      }
      string connectionstring=_settings.Get<string>("_URL").value;
      _settings.Get<string>("_URL").saved=true;
      _settings.Get<string>("_username").saved=true;
      _settings.Get<string>("_password").saved=true;
      if(string.IsNullOrEmpty(connectionstring)) {
        return false;
      }
      if(connectionstring=="#local") {
        connectionstring="localhost";
      }

      if(connectionstring.IndexOf(':')>0) {
        addr=connectionstring.Substring(0, connectionstring.IndexOf(':'));
        port=int.Parse(connectionstring.Substring(connectionstring.IndexOf(':')+1));
      } else {
        addr=connectionstring;
        port=1883;
      }
      TcpClient _tcp=new TcpClient();
      _tcp.SendTimeout=900;
      _tcp.ReceiveTimeout=0;
      _tcp.BeginConnect(addr, port, new AsyncCallback(ConnectCB), _tcp);
      return true;
    }

    private void Dummy(Topic src, TopicChanged arg) {
    }

    private void Topic_SubscriptionsChg(Topic.Subscription s, bool added) {
      if(_verbose.value && s!=null) {
        Log.Debug("{0} {3} {1}.{2}", s.path, s.func.Method.DeclaringType.Name, s.func.Method.Name, added?"+=":"-=");
      }
      if((s!=null && s.path.StartsWith("/local")) || !_connected) {
        return;
      }
      if(!added) {
        if(!_subs.Exists(z => z==s)) {
          return;
        } else {
          _subs.Remove(s);
          Unsubscribe(s.path);
        }
      }
      var sAll=Topic.root.subscriptions.Where(z=>!z.path.StartsWith("/local")).ToArray();
      if(_verbose.value) {
        Log.Debug("SUBS={0}", string.Join("\n", sAll.Select(z => z.path)));
      }
      foreach(var sb in _subs.Except(sAll).ToArray()) {
        _subs.Remove(sb);
        Unsubscribe(sb.path);
      }
      foreach(var sb in sAll.Except(_subs).ToArray()) {
        _subs.Add(sb);
        Subscribe(sb.path, QoS.AtMostOnce);
      }
    }

    private void ConnectCB(IAsyncResult rez) {
      var _tcp=rez.AsyncState as TcpClient;
      try {
        _tcp.EndConnect(rez);
        _stream=new MqStreamer(_tcp, Received, SendIdle);
        var re=((IPEndPoint)_stream.Socket.Client.RemoteEndPoint);
        try {
          BrokerName=Dns.GetHostEntry(re.Address).HostName;
        }
        catch(SocketException) {
          BrokerName=re.Address.ToString();
        }
        _owner=_mq.Get<MqClient>(BrokerName);
        _owner.value=this;
        _tLoaded=new Timer(new TimerCallback(LoadedCB));
        _connected=false;
        string id=Topic.root.Get<string>("/local/cfg/id").value;
        if(string.IsNullOrEmpty(id)) {
          id=string.Format("{0}@{1}_{2:X4}", Environment.UserName, Environment.MachineName, System.Diagnostics.Process.GetCurrentProcess().Id);
        }
        ConnInfo.clientId=id;
        ConnInfo.userName=_settings.Get<string>("_username");
        _settings.Get<string>("_username").saved=true;
        ConnInfo.userPassword=_settings.Get<string>("_password");
        _settings.Get<string>("_password").saved=true;
        if(string.IsNullOrEmpty(ConnInfo.userName) && addr=="localhost") {
          ConnInfo.userName="local";
          ConnInfo.userPassword=string.Empty;
        }

        this.Send(ConnInfo);
        _owner.Subscribe("/#", OwnerChanged);
        _tOut.Change(3000, _keepAliveMS);       // more often than not
      }
      catch(Exception ex) {
        if(_tLoaded!=null) {
          _tLoaded.Change(Timeout.Infinite, Timeout.Infinite);
        }
        Log.Error("Connect to {0}:{1} failed, {2}", addr, port, ex.Message);
        if(StatusChg!=null) {
          StatusChg(false);
        }
        _tOut.Change(_keepAliveMS*5, Timeout.Infinite);
      }
    }

    public void Subscribe(string topic, QoS sQoS) {
      MqSubscribe msg=new MqSubscribe();
      msg.Add(topic, sQoS);
      Send(msg);
    }
    public void Unsubscribe(string path) {
      MqUnsubscribe msg=new MqUnsubscribe();
      msg.Add(path);
      Send(msg);
    }
    public void Stop() {
      if(_stream!=null) {
        if(_connected) {
          _connected=false;
          _owner.Unsubscribe("/#", OwnerChanged);
          _owner.Remove();
          _tOut.Change(Timeout.Infinite, Timeout.Infinite);
          if(StatusChg!=null) {
            StatusChg(_connected);
          }
          _stream.Close();
          _stream=null;
          Log.Info("{0} Disconnected", BrokerName);
        }
      }
    }
    private void TimeOut(object o) {
      if(_stream==null) {
        Reconnect();
      } else if(!_connected) {
        Log.Warning("ConnAck timeout");
        Reconnect();
      } else if(_waitPingResp) {
        Log.Warning("PingResponse timeout");
        Reconnect();
      } else {
        _waitPingResp=true;
        _stream.Send(new MqPingReq());
      }
    }
    private void LoadedCB(object o) {
      _tLoaded.Change(Timeout.Infinite, Timeout.Infinite);
      _eLoaded.Set();
    }
    private void Received(MqMessage msg) {
      if(_verbose.value) {
        Log.Debug("R {0}", msg);
      }
      switch(msg.MsgType) {
      case MessageType.CONNACK: {
          MqConnack cm=msg as MqConnack;
          if(cm.Response!=MqConnack.MqttConnectionResponse.Accepted) {
            Reconnect(true);
            Log.Error("Connection to {0}:{1} failed. error={2}", addr, port, cm.Response.ToString());
          } else {
            _connected=true;
            _subs.Clear();
            Topic_SubscriptionsChg(null, true);
          }
          if(StatusChg!=null) {
            StatusChg(_connected);
          }
        }
        break;
      case MessageType.DISCONNECT:
        Reconnect();
        break;
      case MessageType.PINGRESP:
        _waitPingResp=false;
        break;
      case MessageType.PUBLISH: {
          MqPublish pm=msg as MqPublish;
          if(msg.MessageID!=0) {
            if(msg.QualityOfService==QoS.AtLeastOnce) {
              this.Send(new MqMsgAck(MessageType.PUBACK, msg.MessageID));
            } else if(msg.QualityOfService==QoS.ExactlyOnce) {
              this.Send(new MqMsgAck(MessageType.PUBREC, msg.MessageID));
            }
          }
          ProccessPublishMsg(pm);
        }
        break;
      case MessageType.PUBACK:
        break;
      case MessageType.PUBREC:
        if(msg.MessageID!=0) {
          this.Send(new MqMsgAck(MessageType.PUBREL, msg.MessageID));
        }
        break;
      case MessageType.PUBREL:
        if(msg.MessageID!=0) {
          this.Send(new MqMsgAck(MessageType.PUBCOMP, msg.MessageID));
        }
        break;
      case MessageType.PUBCOMP:
        break;
      default:
        break;
      }
      if(_waitPingResp) {
        _tOut.Change(_keepAliveMS, _keepAliveMS);
      }
    }
    private void ProccessPublishMsg(MqPublish pm) {
      _tLoaded.Change(100, 0);

      Topic cur;
      if(!string.IsNullOrEmpty(pm.Payload)) {         // Publish
        if(!Topic.root.Exist(pm.Path, out cur) || cur.valueType==null) {
          Type vt=X13.WOUM.ExConverter.Json2Type(pm.Payload);
          cur=Topic.GetP(pm.Path, vt, _owner);
        }
        cur.saved=pm.Retained;
        if(cur.valueType!=null) {
          if(cur==_now) {
            try {
              _nowOffset.value=JsonConvert.DeserializeObject<DateTime>(pm.Payload, _jcs).ToLocalTime().Ticks-DateTime.Now.Ticks;
            }
            catch(Exception) {
              return;
            }
          } else if(cur.parent!=_now) {
            cur.FromJson(pm.Payload, _owner);
          }
        }
      } else if(Topic.root.Exist(pm.Path, out cur)) {                      // Remove
        cur.Remove(_owner);
      }
    }

    private void Send(MqMessage msg) {
      _stream.Send(msg);
      if(_verbose.value) {
        Log.Debug("S {0}", msg);
      }
    }
    private void SendIdle() {
      if(_connected) {
        _tOut.Change(_keepAliveMS, _keepAliveMS);
      }
    }
    private void OwnerChanged(Topic sender, TopicChanged param) {
      if(!_connected || sender.parent==null || sender.path.StartsWith("/local") || sender.path.StartsWith("/var/now") || sender.path.StartsWith("/var/log") || param.Visited(_mq, false) || param.Visited(_owner, false)) {
        return;
      }
      switch(param.Art) {
      case TopicChanged.ChangeArt.Add: {
          MqPublish pm=new MqPublish(sender);
          if(sender.valueType!=null && sender.valueType!=typeof(string) && !sender.valueType.IsEnum && !sender.valueType.IsPrimitive) {
            pm.Payload=(new Newtonsoft.Json.Linq.JObject(new Newtonsoft.Json.Linq.JProperty("+", sender.valueType.FullName))).ToString();
          }
          this.Send(pm);
        }
        break;
      case TopicChanged.ChangeArt.Value: {
          MqPublish pm=new MqPublish(sender);
          this.Send(pm);
        }
        break;
      case TopicChanged.ChangeArt.Remove: {
          MqPublish pm=new MqPublish(sender);
          pm.Payload=string.Empty;
          this.Send(pm);
        }
        break;
      }
    }
  }
}
