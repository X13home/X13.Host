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
    private Dictionary<string, Action<string, string>> _subs;

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
      _verbose=false;
      _subs=new Dictionary<string, Action<string, string>>();
      Connect();
    }
    public void Subscribe(string path, Action<string, string> func) {
      _subs[path]=func;
      if(_st==State.Ready) {
        Send("S\t"+path);
      }
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
      if(_verbose.Value) {
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
      if(_verbose.Value) {
        Log.Warning("client - " +e.Message);
      }
      _reconn.Change(_rccnt*15000, _rccnt*30000);
    }
    private void WsLog(LogData d, string f) {
      if(_verbose.Value) {
        Log.Debug("client({0}) - {1}", d.Level, d.Message);
      }
    }
    private void _ws_OnMessage(object sender, MessageEventArgs e) {
      string[] sa;
      if(e.Type==Opcode.Text && !string.IsNullOrEmpty(e.Data) && (sa=e.Data.Split('\t'))!=null && sa.Length>0) {
        if(_verbose.Value) {
          Log.Debug("R {0}", e.Data);
        }
        if(sa[0]=="P" && sa.Length==3) {
          Parse(sa[1], sa[2]);
        } else if(sa[0]=="C" && sa.Length==2) {  // Connect, username, password
          if(sa[1]=="true") {
            _st=State.Ready;
            SendSubs();
          } else {
            Log.Warning("client: wrong username or password");
            _st=State.BadAuth;
            _ws.Close(CloseStatusCode.Normal);
            _reconn.Change(-1, -1);
          }
        } else if(sa[0]=="I" && sa.Length==3) {
          _clientId=sa[1];
          if(sa[2]=="true" || (sa[2]=="null" && string.IsNullOrEmpty(_uName))) {
            _st=State.Ready;
            SendSubs();
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
      if(_verbose.Value) {
        Log.Info("client connected to {0}://{1}", _ws.Url.Scheme, _ws.Url.DnsSafeHost);
      }
    }
    private void Send(string msg) {
      if(_ws!=null && _ws.ReadyState==WebSocketState.Open) {
        _ws.Send(msg);
        if(_verbose.Value) {
          Log.Debug("S {0}", msg);
        }
      }
    }
    private void Parse(string rp, string json) {
      Action<string, string> f;
      if(_subs.TryGetValue(rp, out f) && f!=null) {
        try {
          f(rp, json);
        }
        catch(Exception ex) {
          Log.Warning("Parse({0}, {1}) - {2}", rp, json, ex.ToString());
        }
      }
    }
    private void SendSubs(){
      foreach(string key in _subs.Keys){
        Send("S\t"+key);
      }
    }
  }
}
