using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using WebSocketSharp;
using WebSocketSharp.Net;
using WebSocketSharp.Server;

namespace X13.Plugins {
  internal class ApiV03: WebSocketService {
    private static DVar<bool> _verbose;

    static ApiV03() {
      _verbose=Topic.root.Get<bool>("/etc/HttpServer/_verbose");
    }

    private DVar<string> _owner;
    private string user;
    private List<Topic.Subscription> _subscriptions;

    protected override void OnOpen() {
      Topic r=Topic.root.Get("/etc/HttpServer/clients");
      int i=1;
      while(r.Exist("guest "+i.ToString("X2"))) {
        i++;
      }
      _owner=r.Get<string>("guest "+i.ToString("X2"));
      _owner.saved=false;
      _subscriptions=new List<Topic.Subscription>();
      user=string.Empty;
      System.Threading.ThreadPool.QueueUserWorkItem(o => {
        string host=Context.UserEndPoint.Address.ToString();
        try {
          var he=System.Net.Dns.GetHostEntry(Context.UserEndPoint.Address);
          host=he.HostName;
        }
        catch(Exception) {
        }
        _owner.value=string.Format("{0}:{1}", host, Context.UserEndPoint.Port);
      });
      X13.Log.Debug("{0} connect from {1}", _owner.name, Context.UserEndPoint.Address.ToString());
    }
    protected override void OnMessage(MessageEventArgs e) {
      string[] sa;
      if(e.Type==Opcode.Text && !string.IsNullOrEmpty(e.Data) && (sa=e.Data.Split('\t'))!=null && sa.Length>0) {
        if(sa[0]=="P" && sa.Length==3) {
          HttpWsPl.ProcessPublish(sa[1], sa[2], user);
        } else if(sa[0]=="S" && sa.Length==2) {
          _subscriptions.Add(Topic.root.Subscribe(sa[1], SubChanged));
        }
      }
    }

    private void SubChanged(Topic t, TopicChanged a) {
      if(t.path.StartsWith("/local") || !MQTT.MqBroker.CheckAcl(user, t, TopicAcl.Subscribe)) {
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
      if(_owner!=null) {
        X13.Log.Debug("{0} Disconnect: [{1}]{2}", _owner.name, e.Code, e.Reason);
        _owner.Remove();
        _owner=null;
      }
      foreach(var s in _subscriptions) {
        Topic.root.Unsubscribe(s.path, s.func);
      }
    }
  }
}
