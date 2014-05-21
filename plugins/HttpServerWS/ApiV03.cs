using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using WebSocketSharp;
using WebSocketSharp.Net;
using WebSocketSharp.Server;

namespace X13.Plugins {
  internal class ApiV03: WebSocketService {
    private static DVar<bool> _verbose;
    private static DVar<bool> _disAnonym;
    private static Timer _pingTimer;
    private static WebSocketSessionManager _wsMan;

    static ApiV03() {
      _verbose=Topic.root.Get<bool>("/etc/HttpServer/_verbose");
      _disAnonym=Topic.root.Get<bool>("/etc/HttpServer/DisableAnonymus");
      _pingTimer=new Timer(PingF, null, 270000, 300000);
    }

    private static void PingF(object o) {
      if(_wsMan!=null) {
        _wsMan.Broadping();
      }
    }

    private List<Topic.Subscription> _subscriptions;
    private Session _ses;

    protected override void OnOpen() {
      if(_wsMan==null) {
        _wsMan=Sessions;
      }
      string sid=null;
      if(Context.CookieCollection["sessionId"]!=null) {
        sid=Context.CookieCollection["sessionId"].Value;
      }
      System.Net.IPEndPoint remoteEndPoint = Context.UserEndPoint;
      {
        System.Net.IPAddress remIP;
        if(Context.Headers.Contains("X-Real-IP") && System.Net.IPAddress.TryParse(Context.Headers["X-Real-IP"], out remIP)) {
          remoteEndPoint=new System.Net.IPEndPoint(remIP, remoteEndPoint.Port);
        }
      }
      _ses=Session.Get(sid, remoteEndPoint);
      _subscriptions=new List<Topic.Subscription>();
      Send(string.Concat("I\t", _ses.id, "\t", (string.IsNullOrEmpty(_ses.userName)?(_disAnonym.value?"false":"null"):"true")));
      if(_verbose.value) {
        X13.Log.Debug("{0} connect from {1}", _ses.owner.name, _ses.ip.ToString());
      }
    }
    protected override void OnMessage(MessageEventArgs e) {
      string[] sa;
      if(e.Type==Opcode.Text && !string.IsNullOrEmpty(e.Data) && (sa=e.Data.Split('\t'))!=null && sa.Length>0) {
        if(sa[0]=="C" && sa.Length==3) {  // Connect, username, password
          if((sa[1]!="local" || _ses.ip.IsLocal()) && MQTT.MqBroker.CheckAuth(sa[1], sa[2])) {
            _ses.userName=sa[1];
            Send("C\ttrue");
            X13.Log.Debug("Connect {0} success", _ses.ToString());
          } else {
            Send("C\tfalse");
            X13.Log.Warning("Connect {0}@{1} fail", sa[1], _ses.owner.value);
            Sessions.CloseSession(base.ID);
          }
        } else if(!_disAnonym.value || (_ses!=null && !string.IsNullOrEmpty(_ses.userName))) {
          if(sa[0]=="P" && sa.Length==3) {
            HttpWsPl.ProcessPublish(sa[1], sa[2], _ses);
          } else if(sa[0]=="S" && sa.Length==2) {
            _subscriptions.Add(Topic.root.Subscribe(sa[1], SubChanged));
          }
        }
      }
    }

    private void SubChanged(Topic t, TopicChanged a) {
      if(t.path.StartsWith("/local") || a.Visited(_ses.owner, true) || !MQTT.MqBroker.CheckAcl(_ses.userName, t, TopicAcl.Subscribe)) {
        return;
      }
      if(a.Art==TopicChanged.ChangeArt.Remove) {
        Send(string.Concat("P\t", t.path, "\tnull"));
      } else if(a.Art==TopicChanged.ChangeArt.Value) {
        Send(string.Concat("P\t", t.path, "\t", t.ToJson()));
      } else {
        return;
      }
    }
    protected override void OnClose(CloseEventArgs e) {
      if(_ses!=null) {
        if(_verbose.value) {
          X13.Log.Debug("{0} Disconnect: [{1}]{2}", _ses.owner.name, e.Code, e.Reason);
        }
        _ses.Close();
        _ses=null;
      }
      foreach(var s in _subscriptions) {
        Topic.root.Unsubscribe(s.path, s.func);
      }
    }
  }
  internal class Session {
    private static List<Session> sessions;
    static Session() {
      sessions=new List<Session>();

    }
    public static Session Get(string sid, System.Net.IPEndPoint ep, bool create=true) {
      Session s;
      if(string.IsNullOrEmpty(sid) || (s=sessions.FirstOrDefault(z => z.id==sid && z.ip==ep.Address))==null) {
        if(create) {
          s=new Session(ep);
        } else {
          s=null;
        }
      }
      return s;
    }

    private Session(System.Net.IPEndPoint ep) {
      Topic r=Topic.root.Get("/etc/HttpServer/clients");
      this.id = Guid.NewGuid().ToString();
      this.ip = ep.Address;
      int i=1;
      while(r.Exist("guest "+i.ToString("X2"))) {
        i++;
      }
      owner=r.Get<string>("guest "+i.ToString("X2"));
      owner.saved=false;
      try {
        var he=System.Net.Dns.GetHostEntry(this.ip);
        _host=string.Format("{0}[{1}]:{2}", he.HostName, this.ip.ToString(), ep.Port);
      }
      catch(Exception) {
        _host=string.Format("[{0}]:{1}", this.ip.ToString(), ep.Port);
      }
      this.owner.value=_host;

      Log.Debug("Ses({0}) - {1}", this.ip.ToString(), this.id);
    }
    private string _host;
    public readonly string id;
    public readonly System.Net.IPAddress ip;
    public string userName;
    public DVar<string> owner { get; private set; }
    public void Close() {
      sessions.Remove(this);
      owner.Remove();
    }
    public override string ToString() {
      return (string.IsNullOrEmpty(userName)?"anonymus":userName)+"@"+_host;
    }

  }
}
