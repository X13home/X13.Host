#region license
//Copyright (c) 2011-2014 <comparator@gmx.de>; Wassili Hense

//This file is part of the X13.Home project.
//https://github.com/X13home

//BSD License
//See LICENSE.txt file for license details.
#endregion license

using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using WebSocketSharp;

namespace X13.Plugins {
  [Export(typeof(IPlugModul))]
  [ExportMetadata("priority", 26)]
  [ExportMetadata("name", "WsSync")]
  public class WsSyncPl : IPlugModul {
    private List<WsSyncItem> _items;
    private DVar<bool> _verbose;
    private Topic _cfg;
    public void Init() {
      _items=new List<WsSyncItem>();
      _cfg=Topic.root.Get("/etc/WsSync");
    }
    public void Start() {
      _verbose=_cfg.Get<bool>("_verbose");
      _cfg.Subscribe("+", CfgChanged);
    }
    public void Stop() {
      _cfg.Unsubscribe("+", CfgChanged);
      for(int i=0; i<_items.Count; i++) {
        _items[i].Dispose();
      }
      _items.Clear();
    }
    private void CfgChanged(Topic sender, TopicChanged arg) {
      DVar<string> dv=sender as DVar<string>;
      if(dv==null || sender==_verbose) {
        return;
      }
      if(arg.Art==TopicChanged.ChangeArt.Remove) {
        foreach(var i in _items.Where(z => z.name==dv.name).ToArray()) {
          i.Dispose();
          _items.Remove(i);
        }
      } else if(!string.IsNullOrWhiteSpace(dv.value)) {
        Uri u;
        try {
          u=new Uri(dv.value);
        }
        catch(UriFormatException ex) {
          Log.Warning("{0}=\"{1}\" - {2}", dv.path, dv.value, ex.Message);
          return;
        }
        if(string.IsNullOrEmpty(u.AbsolutePath)) {
          return;
        }
        WsSyncItem it=_items.FirstOrDefault(z => z.name==dv.name);
        if(it==null) {
          it=new WsSyncItem(dv.name, u);
          _items.Add(it);
        } else {
          it.ChangeUri(u);
        }
      }
    }
  }
  internal class WsSyncItem : IDisposable {
    private static Topic _var;
    private static DVar<bool> _verbose;

    static WsSyncItem() {
      _var=Topic.root.Get("/var/WsSync");
      _verbose=Topic.root.Get<bool>("/etc/WsSync/_verbose");
    }
    public readonly string name;
    private   Uri _uri;
    private string _host;
    private string _uName;
    private string _uPass;
    private string _remotePath;
    private string _remoteBase;
    private WebSocket _ws;
    private string _clientId;
    private Topic _val;
    private DVar<bool> _present;
    private enum State {
      Connecting,
      Ready,
      NoAnswer,
      BadAuth,
      Dispose,
    }
    private State _st;
    private Timer _reconn;
    private int _rccnt;

    public WsSyncItem(string name, Uri uri) {
      this.name = name;
      this._uri = uri;
      _st=State.Connecting;
      _val=_var.Get(name);
      _present=_val.Get<bool>("_present");
      _present.value=false;
      Connect();
    }
    public void ChangeUri(Uri uri) {
      _st=State.Connecting;
      this._uri = uri;
      Connect();
    }
    public void Dispose() {
      _st=State.Dispose;
      _var.Unsubscribe(_remotePath.Substring(_remoteBase.Length), _local_changed);
      if(_ws!=null) {
        if(_ws.ReadyState==WebSocketState.Open) {
          _ws.CloseAsync(CloseStatusCode.Normal);
        }
      }
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
      _remotePath=_uri.AbsolutePath+_uri.Fragment;
      {
        int i;
        i=_remotePath.IndexOf("/#");
        if(i<0) {
          i=_remotePath.IndexOf("/+");
        }
        if(i>0) {
          _remoteBase=_remotePath.Substring(0, i);
        } else {
          _remoteBase=_remotePath;
        }
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
    }
    private void _ws_OnClose(object sender, CloseEventArgs e) {
      if(_verbose.value) {
        Log.Debug("{0}.disconnected - {1}", name, e.Code);
      }
      _present.value=false;
      if(_st==State.Ready) {
        _reconn=new Timer(o => {
          if(_st==State.NoAnswer) {
            if(_rccnt<120) {
              _rccnt++;
            }
            Connect();
          }
        }, null, _rccnt*15000, -1);
      } else if(_st==State.Dispose) {
        _ws=null;
      }
    }
    private void _ws_OnError(object sender, WebSocketSharp.ErrorEventArgs e) {
      _st=State.NoAnswer;
      if(_verbose.value) {
        Log.Debug(name + " - " +e.Message);
      }
      _reconn=new Timer(o => {
        if(_st==State.NoAnswer) {
          if(_rccnt<120) {
            _rccnt++;
          }
          Connect();
        }
      }, null, _rccnt*15000, -1);
    }
    private void WsLog(LogData d, string f) {
      if(_verbose.value) {
        Log.Debug("WsSync/{2}({0}) - {1}", d.Level, d.Message, name);
      }
    }
    private void _ws_OnMessage(object sender, MessageEventArgs e) {
      string[] sa;
      if(e.Type==Opcode.Text && !string.IsNullOrEmpty(e.Data) && (sa=e.Data.Split('\t'))!=null && sa.Length>0) {
        if(_verbose.value) {
          Log.Debug("WsSync/{0} << {1}", name, e.Data);
        }
        if(sa[0]=="P" && sa.Length==3) {
          Parse(sa[1], sa[2]);
        } else if(sa[0]=="C" && sa.Length==2) {  // Connect, username, password
          if(sa[1]=="true") {
            _ws.Send("S\t"+_remotePath);
            _val.Subscribe("#", _local_changed);
            _st=State.Ready;
            _present.value=true;
          } else {
            Log.Warning("WsSync/"+name+" wrong username or password");
            _st=State.BadAuth;
            _ws.Close(CloseStatusCode.Normal);
          }
        } else if(sa[0]=="I" && sa.Length==3) {
          _clientId=sa[1];
          if(sa[2]=="true" || (sa[2]=="null" && string.IsNullOrEmpty(_uName))) {
            _ws.Send("S\t"+_remotePath);
            _val.Subscribe("#", _local_changed);
            _st=State.Ready;
            _present.value=true;
          } else if(!string.IsNullOrEmpty(_uName)) {
            _ws.Send("C\t"+_uName+"\t"+_uPass);
          } else {
            Log.Warning("WsSync/"+name+" anonymous user is disabled");
            _st=State.BadAuth;
            _ws.Close(CloseStatusCode.Normal);
          }
        }
      }
    }
    private void _ws_OnOpen(object sender, EventArgs e) {
      if(_verbose.value) {
        Log.Debug("WsSync/"+name+" connected");
      }
      _rccnt=0;
    }
    private void _local_changed(Topic sender, TopicChanged p) {
      if(_val==null || sender==null || sender==_present || _val==null || p.Initiator==_val || sender.path==null || !sender.path.StartsWith(_val.path) || p.Art==TopicChanged.ChangeArt.Add) {
        return;
      }
      string path;
      if(sender==_val) {
        path=_remoteBase;
      } else {
        path=_remoteBase+sender.path.Substring(_val.path.Length);
      }
      string content;
      if(p.Art==TopicChanged.ChangeArt.Value) {
        content=sender.ToJson();
      } else {
        content="null";
      }
      if(_ws!=null && _ws.ReadyState==WebSocketState.Open) {
        _ws.Send("P\t"+path+"\t"+content);
      }
    }
    private void Parse(string rp, string json) {
      if(rp.StartsWith(_remoteBase)) {
        if(rp.Length==_remoteBase.Length) {
          rp=name;
        } else {
          rp=name+rp.Substring(_remoteBase.Length);
        }
      } else {
        return;
      }

      Topic cur;
      if(!string.IsNullOrEmpty(json) && json!="null") {         // Publish
        if(!_var.Exist(rp, out cur) || cur.valueType==null) {
          Type vt=X13.WOUM.ExConverter.Json2Type(json);
          cur=Topic.GetP(_var.path+"/"+rp, vt, _var);
        }
        if(_val==null) {
          _val=_var.Get(name);
          _val.Subscribe("#", _local_changed);
        }
        cur.saved=false;
        if(cur.valueType!=null) {
          cur.FromJson(json, _val);
        }
      } else if(_var.Exist(rp, out cur)) {                      // Remove
        cur.Remove(_val);
      }
    }
  }
}
