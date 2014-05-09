using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using WebSocketSharp;
using WebSocketSharp.Net;
using WebSocketSharp.Server;

namespace X13.Plugins {
  internal class ApiV03 : WebSocketService {
    private DVar<string> _owner;
    protected override void OnOpen() {
      Topic r=Topic.root.Get("/var/WsClients");
      int i=1;
      while(r.Exist("guest "+i.ToString("X2"))) {
        i++;
      }
      _owner=r.Get<string>("guest "+i.ToString("X2"));
      _owner.saved=false;
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
      // Subscribe, mask
      // Publish, path, value

      // e.Data

      // Send(msg);
      Send(e.Data);
    }
    protected override void OnClose(CloseEventArgs e) {
      if(_owner!=null) {
        X13.Log.Debug("{0} Disconnect: [{1}]{2}", _owner.name, e.Code, e.Reason);
        _owner.Remove();
        _owner=null;
      }
    }
  }
}
