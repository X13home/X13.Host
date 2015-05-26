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
using System.Reflection;
using System.Text;
using WebSocketSharp;
using WebSocketSharp.Net;
using WebSocketSharp.Server;

namespace X13.Plugins {
  [Export(typeof(IPlugModul))]
  [ExportMetadata("priority", 20)]
  [ExportMetadata("name", "HttpServer")]
  public class HttpWsPl : IPlugModul {
    internal static int ProcessPublish(string path, string json, Session ses) {
      Topic cur=Topic.root;
      Type vt=null;

      string[] pt=path.Split(new char[] { '/' }, StringSplitOptions.RemoveEmptyEntries);
      int i=0;
      while(i<pt.Length && cur.Exist(pt[i])) {
        cur=cur.Get(pt[i++]);
      }
      if(string.IsNullOrEmpty(json) || json=="null") {                      // Remove
        if(i==pt.Length && MQTT.MqBroker.CheckAcl(ses==null?string.Empty:ses.userName, cur, TopicAcl.Delete)) {
          cur.Remove();
        }
        return 200;
      }
      if(i<pt.Length || cur.valueType==null) {                             // path not exist
        vt=X13.WOUM.ExConverter.Json2Type(json);
        if(!MQTT.MqBroker.CheckAcl(ses==null?string.Empty:ses.userName, cur, TopicAcl.Create)) {
          return 403;
        }
        cur=Topic.GetP(path, vt, ses==null?null:ses.owner);        // Create
      }

      if(cur.valueType!=null) {                 // Publish
        if(MQTT.MqBroker.CheckAcl(ses==null?string.Empty:ses.userName, cur, TopicAcl.Change)) {
          cur.FromJson(json, ses==null?null:ses.owner);
        }
      }
      return 200;
    }

    private DVar<bool> _verbose;
    private DVar<bool> _disAnonym;
    private HttpServer _sv;
    private SortedList<string, Tuple<Stream, string>> _resources;

    public void Init() {
      Topic.root.Subscribe("/etc/Broker/security/#", L_dummy);
      Topic.root.Subscribe("/etc/HttpServer/#", L_dummy);
      Topic.root.Subscribe("/etc/declarers/ui/#", L_dummy);
      Topic.root.Subscribe("/export/#", L_dummy);
    }

    public void Start() {
      using(var sr=new StreamReader(System.Reflection.Assembly.GetExecutingAssembly().GetManifestResourceStream("X13.Plugins.ui.xst"))) {
        Topic.Import(sr, null);
      }
      if(!Topic.root.Exist("/export/_declarer")) {
        var exp=Topic.root.Get("/export");
        Topic.root.Get<long>("/etc/Broker/security/acls/export").value=0x1F000001;
        exp.Get<string>("_declarer").value="ui_root";
      }
      {
        var assembly = Assembly.GetExecutingAssembly();

        var etf=assembly.GetName().Version.ToString(4).GetHashCode().ToString("X8")+"-";
        _resources=new SortedList<string, Tuple<Stream, string>>();
        foreach(var resourceName in assembly.GetManifestResourceNames().Where(z => z.StartsWith("X13.Plugins.www."))) {
          var stream=assembly.GetManifestResourceStream(resourceName);
          string eTag=etf+stream.Length.ToString("X4");
          _resources[resourceName.Substring(16)]=new Tuple<Stream, string>(stream, eTag);
        }
      }

      _verbose=Topic.root.Get<bool>("/etc/HttpServer/_verbose");
      _disAnonym=Topic.root.Get<bool>("/etc/HttpServer/DisableAnonymus");
      var portD=Topic.root.Get<long>("/local/cfg/HttpServer/_port");

      if(portD.value==0) {
        portD.value=Engine.IsLinux?8080:80;
      }

      _sv = new HttpServer((int)portD.value);
      _sv.Log.Output=WsLog;
#if DEBUG
      _sv.Log.Level=WebSocketSharp.LogLevel.Trace;
#endif
      _sv.RootPath=Path.GetFullPath(Path.GetFullPath("../htdocs"));
      if(!Directory.Exists(_sv.RootPath)) {
        Directory.CreateDirectory(_sv.RootPath);
      }
      _sv.OnGet+=OnGet;
      _sv.AddWebSocketService<ApiV03>("/api/v03");
      _sv.AddWebSocketService<X13.Server.ApiV04>("/api/v04");
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
      if(_verbose.value) {
        Log.Debug("WS({0}) - {1}", d.Level, d.Message);
      }
    }
    private void L_dummy(Topic sender, TopicChanged arg) {
      return;
    }

    private void OnGet(object sender, HttpRequestEventArgs e) {
      var req = e.Request;
      var res = e.Response;
      if(req.RemoteEndPoint==null) {
        res.StatusCode=(int)HttpStatusCode.NotAcceptable;
        return;
      }
      System.Net.IPEndPoint remoteEndPoint = req.RemoteEndPoint;
      {
        System.Net.IPAddress remIP;
        if(req.Headers.Contains("X-Real-IP") && System.Net.IPAddress.TryParse(req.Headers["X-Real-IP"], out remIP)) {
          remoteEndPoint=new System.Net.IPEndPoint(remIP, remoteEndPoint.Port);
        }
      }
      string path=req.RawUrl=="/"?"/index.html":req.RawUrl;
      string client;
      Session ses;
      if(req.Cookies["sessionId"]!=null) {
        ses=Session.Get(req.Cookies["sessionId"].Value, remoteEndPoint, false);
      } else {
        ses=null;
      }

      if(ses!=null && ses.owner!=null) {
        client=ses.owner.name;
      } else {
        client=remoteEndPoint.Address.ToString();
      }

      try {
        Tuple<Stream, string> rsc;
        HttpStatusCode statusCode;
        if(_resources.TryGetValue(path.Substring(1), out rsc)) {
          string et;
          if(req.Headers.Contains("If-None-Match") && (et=req.Headers["If-None-Match"])==rsc.Item2) {
            res.Headers.Add("ETag", rsc.Item2);
            statusCode=HttpStatusCode.NotModified;
            res.StatusCode=(int)statusCode;
            res.WriteContent(Encoding.UTF8.GetBytes("Not Modified"));
          } else {
            res.Headers.Add("ETag", rsc.Item2);
            res.ContentType=Ext2ContentType(Path.GetExtension(path));
            rsc.Item1.Position=0;
            rsc.Item1.CopyTo(res.OutputStream);
            statusCode=HttpStatusCode.OK;
          }
        } else {
          FileInfo f = new FileInfo(Path.Combine(_sv.RootPath, path.Substring(1)));
          if(f.Exists) {
            string eTag=f.LastWriteTimeUtc.Ticks.ToString("X8")+"-"+f.Length.ToString("X4");
            string et;
            if(req.Headers.Contains("If-None-Match") && (et=req.Headers["If-None-Match"])==eTag) {
              res.Headers.Add("ETag", eTag);
              statusCode=HttpStatusCode.NotModified;
              res.StatusCode=(int)statusCode;
              res.WriteContent(Encoding.UTF8.GetBytes("Not Modified"));
            } else {
              res.Headers.Add("ETag", eTag);
              res.ContentType=Ext2ContentType(f.Extension);
              using(var fs=f.OpenRead()) {
                fs.CopyTo(res.OutputStream);
              }
              statusCode=HttpStatusCode.OK;
            }
          } else {
            statusCode=HttpStatusCode.NotFound;
            res.StatusCode = (int)statusCode;
            res.WriteContent(Encoding.UTF8.GetBytes("404 Not found"));
          }
        }
        if(_verbose.value) {
          Log.Debug("{0} [{1}]{2} - {3}", client, req.HttpMethod, req.RawUrl, statusCode.ToString());
        }
      }
      catch(Exception ex) {
        if(_verbose.value) {
          Log.Debug("{0} [{1}]{2} - {3}", client, req.HttpMethod, req.RawUrl, ex.Message);
        }
      }
    }
    private string Ext2ContentType(string ext) {
      switch(ext) {
      case ".jpg":
      case ".jpeg":
        return "image/jpeg";
      case ".png":
        return "image/png";
      case ".css":
        return "text/css";
      case ".csv":
        return "text/csv";
      case ".htm":
      case ".html":
        return "text/html";
      case ".js":
        return "application/javascript";
      }
      return "application/octet-stream";
    }

  }
}
