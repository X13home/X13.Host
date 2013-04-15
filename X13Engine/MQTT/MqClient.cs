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
using X13.PLC;
using System.Diagnostics;

namespace X13.MQTT {
  public class MqClient {
    private string addr;
    private int port;
    private DVar<MqClient> _owner;
    private MqStreamer _stream;
    private static Topic _mq;

    private bool _waitPingResp;
    private bool _connected;
    private Timer _tOut;
    private Timer _tLoaded;
    private int _keepAliveMS=89950;  // 90 sec
    private Action<bool> _statusDelegate;

    public ushort KeepAlive {
      get { return (ushort)(_keepAliveMS>0?(_keepAliveMS+50)/1000:0); }
      set {
        if(!_connected) {             // can not inform the broker about the changing
          _keepAliveMS=value>0?value*1000-50:Timeout.Infinite;
        }
      }
    }
    private MqConnect ConnInfo;
    public string BrokerName { get; private set; }
    public string UserName { get { return ConnInfo.userName; } set { ConnInfo.userName=value; } }
    public string UserPass { get { return ConnInfo.userPassword; } set { ConnInfo.userPassword=value; } }

    public MqClient(Action<bool> statusDelegate) {
      _waitPingResp=false;
      _mq=Topic.root.Get("/local/MQ");
      _statusDelegate=statusDelegate;
      ConnInfo=new MqConnect();
      ConnInfo.cleanSession=true;
      ConnInfo.keepAlive=this.KeepAlive;

    }
    public void Connect(string connectionstring) {
      if(string.IsNullOrEmpty(connectionstring)) {
        connectionstring="localhost";
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
      Topic.paused=true;
      TcpClient _tcp=new TcpClient();
      _tcp.SendTimeout=900;
      _tcp.ReceiveTimeout=0;
      _tcp.BeginConnect(addr, port, new AsyncCallback(ConnectCB), _tcp);

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
        _tOut=new Timer(new TimerCallback(TimeOut));
        _tLoaded=new Timer(new TimerCallback(LoadedCB));
        _connected=false;
        ConnInfo.clientId=string.Format("{0}@{1}_{2:X4}", Environment.UserName, Environment.MachineName, System.Diagnostics.Process.GetCurrentProcess().Id);
        this.Send(ConnInfo);
        _owner.Subscribe("/#", OwnerChanged);
        _tOut.Change(3000, _keepAliveMS);       // more often than not
        _tLoaded.Change(6500, 0);
      }
      catch(Exception ex) {
        if(_tLoaded!=null) {
          _tLoaded.Change(Timeout.Infinite, Timeout.Infinite);
          Topic.paused=false;
        }
        Log.Error("Connect to {0}:{1} failed, {2}", addr, port, ex.Message);
        if(_statusDelegate!=null) {
          _statusDelegate(false);
        }
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
    public void Disconnect() {
       if(_connected) {
        _connected=false;
        if(_stream!=null) {
          _stream.Close();
          _stream=null;
        }
        _owner.Unsubscribe("/#", OwnerChanged);
        _owner.Remove();
        _tOut.Change(Timeout.Infinite, Timeout.Infinite);
        if(_statusDelegate!=null) {
          _statusDelegate(_connected);
        }
        Log.Info("{0} Disconnected", BrokerName);
      }
    }
    private void TimeOut(object o) {
      if(!_connected) {
        Log.Warning("ConnAck timeout");
        this.Disconnect();                  //TODO: reconnect
      } else if(_waitPingResp) {
        Log.Warning("PingResponse timeout");
        this.Disconnect();                  //TODO: reconnect
      } else {
        _waitPingResp=true;
        _stream.Send(new MqPingReq());
      }
    }
    private void LoadedCB(object o) {
      _tLoaded.Change(Timeout.Infinite, Timeout.Infinite);
      Topic.paused=false;
    }
    private void Received(MqMessage msg) {
      //Log.Debug("R {0}", msg);

      switch(msg.MsgType) {
      case MessageType.CONNACK: {
          MqConnack cm=msg as MqConnack;
          if(cm.Response!=MqConnack.MqttConnectionResponse.Accepted) {
            _connected=false;
            _tOut.Change(Timeout.Infinite, Timeout.Infinite);
            _tLoaded.Change(Timeout.Infinite, Timeout.Infinite);
            if(_stream!=null) {
              _stream.Close();
              _stream=null;
            }
            Log.Error("Connection to {0}:{1} failed. error={2}", addr, port, cm.Response.ToString());
          } else {
            _connected=true;
          }
          if(_statusDelegate!=null) {
            _statusDelegate(_connected);
          }
        }
        break;
      case MessageType.DISCONNECT:
        this.Disconnect();
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
      if(pm.Path.Equals(_mq.path)) {
        LoadedCB(null);
        Log.Info("MQTT Loaded");
        return;
      }
      Topic cur;
      if(!string.IsNullOrEmpty(pm.Payload)) {         // Publish
        if(!Topic.root.Exist(pm.Path, out cur)) {
          Type vt=X13.WOUM.ExConverter.Json2Type(pm.Payload);
          cur=Topic.GetP(pm.Path, vt, _owner);
        }
        cur.saved=pm.Retained;
        if(cur.valueType!=null) {
          cur.FromJson(pm.Payload, _owner);
        }
      } else if(Topic.root.Exist(pm.Path, out cur)) {                      // Remove
        cur.Remove(_owner);
      }
    }

    private void Send(MqMessage msg) {
      _stream.Send(msg);
    }
    private void SendIdle() {
      _tOut.Change(!_connected?2900:_keepAliveMS, _keepAliveMS);
    }
    private void OwnerChanged(Topic sender, TopicChanged param) {
      if(sender.parent==null || sender.path.StartsWith("/local") || param.Visited(_mq, false) || param.Visited(_owner, false)) {
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
