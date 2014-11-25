using Jurassic;
using Jurassic.Library;
using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Linq;
using System.Text;
using WebSocketSharp;

namespace X13.WAMP {
  internal class WampClient {
    private Uri _uri;
    private WebSocket _ws;
    private int _st;
    private ScriptEngine _engine;
    private long _sessionId;
    private long _reqId;
    private ConcurrentDictionary<long, IWampCommand> _requests;

    public WampClient(string uri){
      _uri=new Uri(uri);
      _requests=new ConcurrentDictionary<long, IWampCommand>();
    }
    public void Open(){
      string _host;
      if(_uri.IsDefaultPort) {
        _host=string.Concat(_uri.Scheme, "://", _uri.DnsSafeHost);
      } else {
        _host=string.Concat(_uri.Scheme, "://", _uri.DnsSafeHost, ":", _uri.Port.ToString());
      }
      if(_engine==null) {
        _engine=new ScriptEngine();
      }

      _ws=new WebSocket(_host+"/api/v04", "wamp.2.json");
      _ws.Log.Output=WsLog;
      _ws.OnOpen+=_ws_OnOpen;
      _ws.OnMessage+=_ws_OnMessage;
      _ws.OnError+=_ws_OnError;
      _ws.OnClose+=_ws_OnClose;
      _ws.ConnectAsync();
    }
    public void Close() {
      if(_ws!=null) {
        if(_ws.ReadyState==WebSocketState.Open) {
          _ws.CloseAsync(CloseStatusCode.Normal);
        }
      }
    }

    public void Subscribe(string path, AllOptions options=AllOptions.None) {
      long reqId=RequestId();
      var sub=new WampSubscribe(path, reqId, options);
      _requests[reqId]=sub;
      string json=sub.ToString();
      _ws.Send(json);
    }

    private void _ws_OnOpen(object sender, EventArgs e) {
      _st=0;
      var roles=_engine.Object.Construct();
      roles["publisher"]=_engine.Object.Construct();
      roles["subscriber"]=_engine.Object.Construct();
      roles["caller"]=_engine.Object.Construct();
      var details=_engine.Object.Construct();
      details["roles"]= roles;
      var msg=_engine.Array.New(new object[] { (int)WampMessageType.Hello, Environment.MachineName, details });
      var json=JSONObject.Stringify(_engine, msg); //[1, "somerealm", { "roles": { "publisher": {}, "subscriber": {}, "caller": {} } }]
      _ws.Send(json);
      
    }
    private void _ws_OnMessage(object sender, MessageEventArgs e) {
      if(e.Type!=Opcode.Text) {
        return;
      }
      try {
        var arr=JSONObject.Parse(_engine, e.Data) as Jurassic.Library.ArrayInstance;
        if(arr==null || arr.Length==0) {
          return;
        }
        WampMessageType cmd=(WampMessageType)Convert.ToUInt16(arr[0]);
        switch(cmd) {
        case WampMessageType.Welcome:
          if(_st==0) {
            _st=1;
            _reqId=0;
            _sessionId=Convert.ToInt64(arr[1]);
          } else {
            //TODO: Send Error
          }
          break;
        }
      }
      catch(Exception ex) {
        X13.lib.Log.Warning("OnMessage - ", ex.Message);
      }
    }
    private void _ws_OnClose(object sender, CloseEventArgs e) {
      var tmp=e;
    }
    private void _ws_OnError(object sender, ErrorEventArgs e) {
      X13.lib.Log.Warning("WS# - {0}", e.Message);
    }
    private void WsLog(LogData d, string f) {
      X13.lib.Log.Debug("({0}) - {1}", d.Level, d.Message);
    }

    private long RequestId() {
      long id=System.Threading.Interlocked.Increment(ref _reqId);
      return id;
    }
  }
}
