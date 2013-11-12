using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;

namespace X13.HttpServer {
  internal class Session {
    private static List<Session> _sList;
    private const int RespTO=120000;
    private static DVar<bool> _verbose;

    static Session() {
      _sList=new List<Session>();
      _verbose=Topic.root.Get<bool>("/etc/HttpServer/_verbose");
    }

    public static Session Get(Cookie cookie, string userName) {
      Session s=null;
      if(cookie!=null && !string.IsNullOrEmpty(cookie.Value)) {
        lock(_sList) {
          s=_sList.FirstOrDefault(z => z.id.Value==cookie.Value);
        }
      }
      if(s==null) {
        s=new Session(cookie!=null?cookie.Value:string.Empty, userName);
        lock(_sList) {
          _sList.Add(s);
        }
      }
      return s;
    }
    public static void CloseAll() {
      for(int i=_sList.Count-1; i>=0; i--) {
        _sList[i].Close();
      }
    }

    private List<Topic.Subscription> _subscriptions;
    private HttpListenerContext _ctx;
    private StringBuilder _SendBuf;
    private Timer _to;

    public Session(string p, string user) {
      this.user=user;
      if(string.IsNullOrEmpty(p)) {
        id=new Cookie("session", Guid.NewGuid().ToString());
      } else {
        id=new Cookie("session", p);
      }
      _subscriptions=new List<Topic.Subscription>();
      _SendBuf=new StringBuilder();
      _SendBuf.Clear();
      _to=new Timer(TimeOut, null, RespTO, RespTO);
    }

    public readonly Cookie id;
    public readonly string user;

    public void Enqueue(HttpListenerContext ctx) {
      lock(_SendBuf) {
        if(_ctx!=null) {
          _ctx.Response.Abort();
        }
        _ctx=ctx;
        if(_verbose.value) {
          Log.Debug("{0}.Enq({1})", id, ctx.Request.RemoteEndPoint);
        }
        if(_SendBuf.Length>3) {
          try {
            SendResponse();
          }
          catch(Exception) {
            Close();
            return;
          }
        }
        _to.Change(RespTO, RespTO);
      }
    }
    public int Subscribe(string sub) {
      if(string.IsNullOrEmpty(sub) || !sub.StartsWith("/export")) {
        return 400;
      }

      Topic cur=Topic.root, next;
      string[] lvls=sub.Split(new char[] { '/' }, StringSplitOptions.RemoveEmptyEntries);
      int i=0;
      for(; i<lvls.Length; i++) {
        if(lvls[i]=="+" || lvls[i]=="#") {
          break;
        }
        if(!cur.Exist(lvls[i], out next)) {
          if(!MQTT.MqBroker.CheckAcl(user, cur, TopicAcl.Create)) {
            return 403;
          }
          next=cur.Get(lvls[i]);
        }
        if(next==null) {
          return 500;
        }
        cur=next;
      }

      var s=cur.Subscribe(string.Join("/", lvls.Skip(i)), OwnerChanged);
      _subscriptions.Add(s);
      return 200;
    }

    private void TimeOut(object o) {
      lock(_SendBuf) {
        if(_ctx!=null) {
          try {
            SendResponse();
            return;
          }
          catch(Exception) {
          }
        }
      }
      Close();
    }
    private void Close() {
      lock(_sList) {
        _sList.Remove(this);
      }
      _to.Dispose();
      if(_ctx!=null) {
        _ctx.Response.Abort();
      }
      _ctx=null;
      foreach(var s in _subscriptions) {
        Topic.root.Unsubscribe(s.path, s.func);
      }
    }
    private void OwnerChanged(Topic snd, TopicChanged arg) {
      if(snd.path.StartsWith("/local") || !MQTT.MqBroker.CheckAcl(user, snd, TopicAcl.Subscribe)) {
        return;
      }
      if(_verbose.value) {
        Log.Debug("{0}.Chg({1})", id, snd.path);
      }
      lock(_SendBuf) {
        if(arg.Art==TopicChanged.ChangeArt.Remove) {
          _SendBuf.AppendFormat("{{\"{0}\": null}}\r\n", snd.path);
        } else if(arg.Art==TopicChanged.ChangeArt.Value) {
          _SendBuf.AppendFormat("{{\"{0}\": {1}}}\r\n", snd.path, snd.ToJson());
        } else {
          return;
        }
        if(_ctx!=null) {
          try {
            SendResponse();
          }
          catch(Exception) {
            Close();
          }
        }
      }
    }
    private void SendResponse() {
      using(HttpListenerResponse response = _ctx.Response) {
        _SendBuf.Append("{\"/local/mq\": null}");
        string responseString=_SendBuf.ToString();
        _SendBuf.Clear();
        response.StatusCode=200;
        response.ContentType="application/json";
        byte[] buffer = System.Text.Encoding.UTF8.GetBytes(responseString);
        // Get a response stream and write the response to it.
        response.ContentLength64 = buffer.Length;
        System.IO.Stream output = response.OutputStream;
        output.Write(buffer, 0, buffer.Length);
        // You must close the output stream.
        output.Close();
        response.SetCookie(id);
        if(_verbose.value) {
          Log.Debug("{0}.Send({1})", id, responseString);
        }
      }
      _ctx=null;
    }
  }
}
