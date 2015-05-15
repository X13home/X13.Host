using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using WebSocketSharp;
using WebSocketSharp.Net;
using WebSocketSharp.Server;

namespace X13.Plugins {
  internal class ApiV03 : WebSocketBehavior {
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
        X13.Log.Debug("{0} connect webSocket", _ses.owner.name);
      }
    }
    protected override void OnMessage(MessageEventArgs e) {
      string[] sa;
      if(e.Type==Opcode.Text && !string.IsNullOrEmpty(e.Data) && (sa=e.Data.Split('\t'))!=null && sa.Length>0) {
        if(_verbose.value) {
          X13.Log.Debug("ws.msg({0})", string.Join(", ", sa));
        }
        if(sa[0]=="C" && sa.Length==3) {  // Connect, username, password
          if((sa[1]!="local" || _ses.ip.IsLocal()) && MQTT.MqBroker.CheckAuth(sa[1], sa[2])) {
            _ses.userName=sa[1];
            Send("C\ttrue");
            if(_verbose.value) {
              X13.Log.Info("{0} logon as {1} success", _ses.owner.name, _ses.ToString());
            }
          } else {
            Send("C\tfalse");
            if(_verbose.value) {
              X13.Log.Warning("{0}@{2} logon  as {1} fail", _ses.owner.name, sa[1], _ses.owner.value);
            }
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
        if(_verbose.value) {
          X13.Log.Debug("ws.snd({0}) - access denied", t.path);
        }
        return;
      }
      if(a.Art==TopicChanged.ChangeArt.Remove) {
        Send(string.Concat("P\t", t.path, "\tnull"));
        if(_verbose.value) {
          X13.Log.Debug("ws.snd({0}) - remove", t.path);
        }
      } else if(a.Art==TopicChanged.ChangeArt.Value) {
        Send(string.Concat("P\t", t.path, "\t", t.ToJson()));
        if(_verbose.value) {
          X13.Log.Debug("ws.snd({0}, {1})", t.path, t.ToJson());
        }
      } else {
        return;
      }
    }
    protected override void OnClose(CloseEventArgs e) {
      if(_ses!=null) {
        _ses.Close();
        if(_verbose.value) {
          X13.Log.Info("{0} Disconnect: [{1}]{2}", _ses.owner.name, e.Code, e.Reason);
        }
        _ses=null;
      }
      foreach(var s in _subscriptions) {
        Topic.root.Unsubscribe(s.path, s.func);
      }
    }
  }
  internal class Session : IDisposable {
    private static List<WeakReference> sessions;
    private static DVar<bool> _verbose;

    static Session() {
      sessions=new List<WeakReference>();
      _verbose=Topic.root.Get<bool>("/etc/HttpServer/_verbose");
    }
    public static Session Get(string sid, System.Net.IPEndPoint ep, bool create=true) {
      Session s;
      if(string.IsNullOrEmpty(sid) || (s=sessions.Where(z => z.IsAlive).Select(z => z.Target as Session).FirstOrDefault(z => z!=null && z.id==sid && z.ip.Equals(ep.Address)))==null) {
        if(create) {
          s=new Session(ep);
          sessions.Add(new WeakReference(s));
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
      string pre=ip.ToString();
      while(r.Exist(pre+i.ToString())) {
        i++;
      }
      _owner=r.Get<string>(pre+i.ToString());
      owner.saved=false;
      try {
        var he=System.Net.Dns.GetHostEntry(this.ip);
        _host=string.Format("{0}[{1}]", he.HostName, this.ip.ToString());
        var tmp=he.HostName.Split('.');
        if(tmp!=null && tmp.Length>0 && !string.IsNullOrEmpty(tmp[0])) {
          i=1;
          while(r.Exist(tmp[0]+"-"+i.ToString())) {
            i++;
          }
          _owner.Move(r, tmp[0]+"-"+i.ToString());
        }
      }
      catch(Exception) {
        _host=string.Format("[{0}]", this.ip.ToString());
      }
      this.owner.value=_host;
      if(_verbose.value) {
        Log.Info("{0} session[{2}] - {1}", owner.name, this._host, this.id);
      }
    }
    private string _host;
    private DVar<string> _owner;
    public readonly string id;
    public readonly System.Net.IPAddress ip;
    public string userName;
    public DVar<string> owner { get { return _owner; } }
    public void Close() {
      sessions.RemoveAll(z => !z.IsAlive || z.Target==this);
      Dispose();
    }
    public override string ToString() {
      return (string.IsNullOrEmpty(userName)?"anonymus":userName)+"@"+_host;
    }
    public void Dispose() {
      var o=Interlocked.Exchange(ref _owner, null);
      if(o!=null && !o.disposed) {
        o.Remove();
      }
    }
  }
}
