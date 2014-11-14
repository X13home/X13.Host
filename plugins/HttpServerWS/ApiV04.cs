using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using WebSocketSharp;
using WebSocketSharp.Net;
using WebSocketSharp.Server;
using X13.Plugins;

namespace X13.Server{
  internal class ApiV04 : WebSocketBehavior {
    private static DVar<bool> _verbose;
    private static Timer _pingTimer;
    private static WebSocketSessionManager _wsMan;
    private static string _hostName;

    static ApiV04() {
      _verbose=Topic.root.Get<bool>("/etc/Server/verbose");
      _pingTimer=new Timer(PingF, null, 270000, 300000);
      _hostName=Environment.MachineName;
    }
    private static void PingF(object o) {
      if(_wsMan!=null) {
        _wsMan.Broadping();
      }
    }
    private Session _ses;
    private System.Net.IPEndPoint _remoteEndPoint;

    protected override void OnOpen() {
      if(_wsMan==null) {
        _wsMan=Sessions;
      }
      string sid=null;
      if(Context.CookieCollection["sessionId"]!=null) {
        sid=Context.CookieCollection["sessionId"].Value;
      }
      _remoteEndPoint = Context.UserEndPoint;
      {
        System.Net.IPAddress remIP;
        if(Context.Headers.Contains("X-Real-IP") && System.Net.IPAddress.TryParse(Context.Headers["X-Real-IP"], out remIP)) {
          _remoteEndPoint=new System.Net.IPEndPoint(remIP, _remoteEndPoint.Port);
        }
      }
      _ses=Session.Get(sid, _remoteEndPoint, false);
      if(_ses!=null) {
        Send((new MessageV04(MessageV04.Cmd.Ack, 0, _ses.id)).ToString());
      } else {
        Send((new MessageV04(MessageV04.Cmd.Info, 0, _hostName)).ToString());
      }

    }
    protected override void OnMessage(MessageEventArgs e) {
      MessageV04 msg;
      if(e.Type==Opcode.Text) {
        msg=MessageV04.Parse(e.Data);
      } else {
        return;
      }
      switch(msg.cmd) {
      case MessageV04.Cmd.Connect:
        if(_ses!=null) {
          _ses.Close();
        }
        string un, up;
        if(msg.payload==null || msg.payload.Length<1) {
          un=string.Empty;
          up=string.Empty;
        } else {
          un=msg.payload[0];
          up=msg.payload.Length>1?msg.payload[1]:string.Empty;
        }
        if(!CheckAuth(un, up, _remoteEndPoint.Address.IsLocal())) {
          Send((new MessageV04(MessageV04.Cmd.Nack, msg.mid, "Bad username or password")).ToString());
          X13.Log.Warning("{0} logon as {1} failed", _remoteEndPoint.Address, un);
          Sessions.CloseSession(base.ID);
          break;
        }
        _ses=Session.Get(null, _remoteEndPoint, true);
        _ses.userName=un;
        Send((new MessageV04(MessageV04.Cmd.Ack, msg.mid, _ses.id)).ToString());
        break;
      }
    }

    protected override void OnError(ErrorEventArgs e) {
      X13.Log.Error("ApiV04 - ", e.Message);

    }
    protected override void OnClose(CloseEventArgs e) {
      X13.Log.Info("ApiV04.closed {0}", e.Code);
    }

    private bool CheckAuth(string un, string up, bool local) {
      return true;
    }

  }
}
