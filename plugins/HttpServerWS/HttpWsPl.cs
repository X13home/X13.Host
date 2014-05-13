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
using WebSocketSharp;
using WebSocketSharp.Net;
using WebSocketSharp.Server;

namespace X13.Plugins {
  [Export(typeof(IPlugModul))]
  [ExportMetadata("priority", 20)]
  [ExportMetadata("name", "HttpServer")]
  public class HttpWsPl : IPlugModul {
    public static int ProcessPublish(string path, string json, string user) {
      Topic cur=Topic.root;
      Type vt=null;

      string[] pt=path.Split(new char[] { '/' }, StringSplitOptions.RemoveEmptyEntries);
      int i=0;
      while(i<pt.Length && cur.Exist(pt[i])) {
        cur=cur.Get(pt[i++]);
      }
      if(string.IsNullOrEmpty(json) || json=="null") {                      // Remove
        if(i==pt.Length && MQTT.MqBroker.CheckAcl(user, cur, TopicAcl.Delete)) {
          cur.Remove();
        }
        return 200;
      }
      if(i<pt.Length || cur.valueType==null) {                             // path not exist
        vt=X13.WOUM.ExConverter.Json2Type(json);
        if(!MQTT.MqBroker.CheckAcl(user, cur, TopicAcl.Create)) {
          return 403;
        }
        cur=Topic.GetP(path, vt);        // Create
      }

      if(cur.valueType!=null) {                 // Publish
        if(MQTT.MqBroker.CheckAcl(user, cur, TopicAcl.Change)) {
          cur.FromJson(json);
        }
      }
      return 200;
    }

    private const long _version=271;
    private DVar<bool> _verbose;
    private HttpServer _sv;

    public void Init() {
      Topic.root.Subscribe("/etc/Broker/security/#", L_dummy);
      Topic.root.Subscribe("/etc/HttpServer/#", L_dummy);
      Topic.root.Subscribe("/etc/declarers/ui/#", L_dummy);
      Topic.root.Subscribe("/export/#", L_dummy);
    }

    public void Start() {
      var ver=Topic.root.Get<long>("/etc/HttpServer/version");
      if(ver.value<_version) {
        ver.saved=true;
        ver.value=_version;
        Log.Info("Load HttpServer declarers");
        var st=System.Reflection.Assembly.GetExecutingAssembly().GetManifestResourceStream("X13.HttpServerWS.ui.xst");
        if(st!=null) {
          using(var sr=new StreamReader(st)) {
            Topic.Import(sr, null);
          }
        }
      }
      if(!Topic.root.Exist("/export/_declarer")) {
        var exp=Topic.root.Get("/export");
        Topic.root.Get<long>("/etc/Broker/security/acls/export").value=0x1F000001;
        exp.Get<string>("_declarer").value="ui_root";
      }

      _verbose=Topic.root.Get<bool>("/etc/HttpServer/_verbose");
      var portD=Topic.root.Get<long>("/local/cfg/HttpServer/_port");

      if(portD.value==0) {
        portD.value=Engine.IsLinux?8080:80;
      }
      _sv = new HttpServer((int)portD.value);
      _sv.Log.Output=WsLog;
      _sv.RootPath=Path.GetFullPath(Path.GetFullPath("../htdocs"));
      _sv.OnGet+=OnGet;
      _sv.AddWebSocketService<ApiV03>("/api/v03");
      _sv.Start();
      if(_sv.IsListening) {
        Log.Info("HttpServer started on {0}:{1} ", Environment.MachineName, _sv.Port.ToString());
      } else {
        Log.Error("HttpServer start failed");
      }
    }

    public void Stop() {
      _sv.Stop();
    }

    private void WsLog(LogData d, string f) {
      Log.Debug("WS({0}) - {1}", d.Level ,d.Message);
    }
    private void L_dummy(Topic sender, TopicChanged arg) {
      return;
    }
    private byte[] getContent(string path) {
      if(path == "/")
        path += "idx_ws.html";

      return _sv.GetFile(path);
    }

    private void OnGet(object sender, HttpRequestEventArgs e) {
      var req = e.Request;
      var res = e.Response;
      var content = getContent(req.RawUrl);
      if(content != null) {
        res.WriteContent(content);
      } else {
        res.StatusCode = (int)HttpStatusCode.NotFound;
      }
      if(_verbose.value) {
        Log.Debug("{0}[{1}]{2} - {3}", req.RemoteEndPoint.Address.ToString(), req.HttpMethod, req.RawUrl, ((HttpStatusCode)res.StatusCode).ToString());
      }
    }
  }
}
