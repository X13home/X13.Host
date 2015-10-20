using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using WebSocketSharp;


namespace X13.Agent2 {
  internal class Client {
    private   Uri _uri;
    private string _host;
    private string _uName;
    private string _uPass;
    private string _clientId;
    private WebSocket _ws;
    private State _st;
    private Timer _reconn;
    private int _rccnt;
    private bool? _verbose;


    private enum State {
      Connecting,
      Ready,
      NoAnswer,
      BadAuth,
      Dispose,
    }

    public Client(Uri uri) {
      this._uri = uri;
      _st=State.Connecting;
      _reconn = new Timer(CheckState);
      _rccnt = 1;
      _verbose=true;
      Connect();
    }
    private void CheckState(object o) {
      if(_st == State.Ready && (_ws == null || _ws.ReadyState != WebSocketState.Open)) {
        _rccnt = 1;
      } else if(_st == State.NoAnswer) {
        if(_rccnt < 120) {
          _rccnt++;
        }
      } else {
        _rccnt = 1;
        return;
      }
      Connect();
    }
    private void Connect() {
      if(_ws!=null) {
        if(_ws.IsAlive) {
          _ws.Close(CloseStatusCode.Normal);
        }
        _ws=null;
      }
      if(_st==State.BadAuth) {
        return;
      }
      if(_uri.IsDefaultPort) {
        _host=string.Concat(_uri.Scheme, "://", _uri.DnsSafeHost);
      } else {
        _host=string.Concat(_uri.Scheme, "://", _uri.DnsSafeHost, ":", _uri.Port.ToString());
      }
      var up=Uri.UnescapeDataString(_uri.UserInfo).Split(':');
      _uName=up.Length>0?up[0]:string.Empty;
      _uPass=up.Length==2?up[1]:string.Empty;
      _ws=new WebSocket(_host+"/api/v03");
      _ws.Log.Output=WsLog;
      _ws.OnOpen+=_ws_OnOpen;
      _ws.OnMessage+=_ws_OnMessage;
      _ws.OnError+=_ws_OnError;
      _ws.OnClose+=_ws_OnClose;
      _ws.ConnectAsync();
      _reconn.Change(_rccnt * 15000, _rccnt * 30000);
    }
    private void _ws_OnClose(object sender, CloseEventArgs e) {
      if(_verbose.value) {
        if(e.Code==1000) {
          Log.Info("Client - disconnected[{0}]", e.Code);
        } else {
          Log.Warning("Client - disconnected[{0}]", e.Code);
        }
      }
      if(_st==State.Dispose) {
        _reconn.Change(-1, -1);
        _ws=null;
      }
    }
    private void _ws_OnError(object sender, WebSocketSharp.ErrorEventArgs e) {
      _st=State.NoAnswer;
      if(_verbose.value) {
        Log.Warning("client - " +e.Message);
      }
      _reconn.Change(_rccnt*15000, _rccnt*30000);
    }
    private void WsLog(LogData d, string f) {
      if(_verbose.value) {
        Log.Debug("client({0}) - {1}", d.Level, d.Message);
      }
    }
    private void _ws_OnMessage(object sender, MessageEventArgs e) {
      string[] sa;
      if(e.Type==Opcode.Text && !string.IsNullOrEmpty(e.Data) && (sa=e.Data.Split('\t'))!=null && sa.Length>0) {
        if(_verbose.value) {
          Log.Debug("R client {0}", e.Data);
        }
        if(sa[0]=="P" && sa.Length==3) {
          Parse(sa[1], sa[2]);
        } else if(sa[0]=="C" && sa.Length==2) {  // Connect, username, password
          if(sa[1]=="true") {
            //Send("S\t"+_remotePath);
            _st=State.Ready;
          } else {
            Log.Warning("client: wrong username or password");
            _st=State.BadAuth;
            _ws.Close(CloseStatusCode.Normal);
            _reconn.Change(-1, -1);
          }
        } else if(sa[0]=="I" && sa.Length==3) {
          _clientId=sa[1];
          if(sa[2]=="true" || (sa[2]=="null" && string.IsNullOrEmpty(_uName))) {
            //Send("S\t"+_remotePath);
            _st=State.Ready;
          } else if(!string.IsNullOrEmpty(_uName)) {
            Send("C\t"+_uName+"\t"+_uPass);
          } else {
            Log.Warning("client: anonymous user is disabled");
            _st=State.BadAuth;
            _ws.Close(CloseStatusCode.Normal);
          }
        }
      }
    }
    private void _ws_OnOpen(object sender, EventArgs e) {
      if(_verbose.value) {
        Log.Info("client connected to {0}://{1}", _ws.Url.Scheme, _ws.Url.DnsSafeHost);
      }
    }
    private void Send(string msg) {
      if(_ws!=null && _ws.ReadyState==WebSocketState.Open) {
        if(_verbose.value) {
          Log.Debug("S client {1}", msg);
        }
        _ws.Send(msg);
      }
    }
    private void Parse(string rp, string json) {
    }

  }
}
