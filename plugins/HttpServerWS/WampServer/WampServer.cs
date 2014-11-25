using Jurassic;
using Jurassic.Library;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using WebSocketSharp;
using WebSocketSharp.Net;
using WebSocketSharp.Server;

namespace X13.WAMP {
  internal class WampServer : WebSocketBehavior {
    private static DVar<bool> _verbose;
    private static Timer _pingTimer;
    private static WebSocketSessionManager _wsMan;
    private static ScriptEngine _engine;
    private static Random _rand;

    static WampServer() {
      _verbose=Topic.root.Get<bool>("/etc/WampServer/_verbose");
      _pingTimer=new Timer(PingF, null, 270000, 300000);
      _engine=new ScriptEngine();
      _rand=new Random((int)DateTime.Now.Ticks);
    }
    private static void PingF(object o) {
      if(_wsMan!=null) {
        _wsMan.Broadping();
      }
    }
    public WampServer() {
      if(_wsMan==null) {
        _wsMan=Sessions;
      }
      base.Protocol="wamp.2.json";
    }
    protected override void OnOpen() {
      System.Net.IPEndPoint remoteEndPoint = Context.UserEndPoint;
      {
        System.Net.IPAddress remIP;
        if(Context.Headers.Contains("X-Real-IP") && System.Net.IPAddress.TryParse(Context.Headers["X-Real-IP"], out remIP)) {
          remoteEndPoint=new System.Net.IPEndPoint(remIP, remoteEndPoint.Port);
        }
      }
      if(base.Context.SecWebSocketProtocols!=null && base.Context.SecWebSocketProtocols.Any(z => z=="wamp.2.json")) {
        X13.Log.Debug("WampServer.Open({0})", remoteEndPoint.Address);
      } else {
        X13.Log.Warning("WampServer.Open({0}) subprotocol!=\"wamp.2.json\"", remoteEndPoint.Address, base.Protocol);
      }
    }
    protected override void OnMessage(MessageEventArgs e) {
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
        case WampMessageType.Hello: {
            // [2, 9129137332, {"roles": {"broker": {}, "dealer": {} } } ]
            var roles=_engine.Object.Construct();
            roles["broker"]=_engine.Object.Construct();
            roles["dealer"]=_engine.Object.Construct();
            var details=_engine.Object.Construct();
            details["roles"]= roles;
            var msg=_engine.Array.New(new object[] { (int)WampMessageType.Welcome, IdGenerator.Generate() , details });
            var json=JSONObject.Stringify(_engine, msg);
            Send(json);
          }
          break;
        }
      }
      catch(Exception ex) {
        X13.Log.Warning("OnMessage({0}) - {1}", e.Data, ex.Message);
      }
    }
    protected override void OnClose(CloseEventArgs e) {
    }
    protected override void OnError(ErrorEventArgs e) {
      X13.Log.Warning("WS - {0}", e.Message);
    }

  }
}
