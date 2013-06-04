using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;

namespace X13.HttpServer {
  [Export(typeof(IPlugModul))]
  [ExportMetadata("priority", 20)]
  [ExportMetadata("name", "HttpServer")]
  public class HttpServPl : IPlugModul {
    private HttpListener _listener;
    private DVar<bool> _verbose;
    private string _htPath;

    public HttpServPl() {
    }

    public void Init() {
      Topic.root.Subscribe("/etc/Broker/security/#", L_dummy);
      Topic.root.Subscribe("/etc/HttpServer/#", L_dummy);
    }
    public void Start() {
      bool ad2=false;
      _verbose=Topic.root.Get<bool>("/etc/HttpServer/_verbose");
      var urlD=Topic.root.Get<string>("/local/cfg/HttpServer/_url");

      _htPath=Path.GetFullPath("../htdocs");

      if(string.IsNullOrEmpty(urlD.value)) {
        urlD.saved=true;
        urlD.value=@"http://+:80/";
      }
    reconnect:
      _listener = new HttpListener();
      _listener.Prefixes.Add(urlD.value);
      try {
        _listener.Start();
      }
      catch(HttpListenerException ex) {
        if(ex.ErrorCode==5 && !ad2 && Environment.OSVersion.Platform==PlatformID.Win32NT && System.Environment.OSVersion.Version.Major >= 6) {   // Access denied
          ad2=true;
          ProcessStartInfo info = new ProcessStartInfo("netsh");
          info.Arguments=string.Format("http add urlacl url={0} user={1}", urlD.value, Environment.UserName);
          info.Verb = "runas";
          Process p = Process.Start(info);
          p.WaitForExit();
          goto reconnect;
        } else {
          Log.Error("HttpServer.Start failed - code={0}, {1}", ex.ErrorCode, ex.Message);
          return;
        }
      }
      _listener.AuthenticationSchemes=AuthenticationSchemes.Basic | AuthenticationSchemes.Anonymous;
      _listener.BeginGetContext(new AsyncCallback(ContextReady), null);
    }
    public void Stop() {
      Session.CloseAll();
      _listener.Close();
    }

    private void L_dummy(Topic sender, TopicChanged arg) {
      return;
    }

    private void ContextReady(IAsyncResult ar) {
      bool userPassWrong=true;
      string userName;
      Session ses;
      HttpListenerContext ctx=null;
      string RemoteEP=string.Empty;

      try {
        ctx = _listener.EndGetContext(ar);
        RemoteEP=ctx.Request.RemoteEndPoint.ToString();
        if(ctx.User!=null) {
          var id=ctx.User.Identity as HttpListenerBasicIdentity;
          userPassWrong=!MQTT.MqBroker.CheckAuth(id.Name, id.Password);
          userName=id.Name;
        } else {
          userPassWrong=false;
          userName=string.Empty;
        }
        if(_verbose.value) {
          Log.Debug("{0}({1}) [{2}] {3}", userName, userPassWrong?"fail":"pass", ctx.Request.HttpMethod, ctx.Request.RawUrl);
        }
        if(!userPassWrong && ctx.Request.HttpMethod=="GET" && ctx.Request.RawUrl==@"/data?read") {
          ses=Session.Get(ctx.Request.Cookies["session"], userName);
          ses.Enqueue(ctx);
        } else {
          using(HttpListenerResponse response = ctx.Response) {
            string responseString=string.Empty;
            if(userPassWrong) {
              response.StatusCode=401;
              responseString="401 Unauthorized";
            } else if(ctx.Request.HttpMethod=="GET") {
              try {
                if(ctx.Request.RawUrl==@"/") {
                  responseString=File.ReadAllText(Path.Combine(_htPath, "index.html"));
                  response.ContentType="text/html";
                } else
                  if(ctx.Request.RawUrl.StartsWith(@"/data/")) {
                    string mqPath=ctx.Request.RawUrl.Substring(5);
                    Topic cur;
                    if(Topic.root.Exist(mqPath, out cur)) {
                      if(MQTT.MqBroker.CheckAcl(userName, cur, TopicAcl.Subscribe)) {
                        responseString=cur.ToJson();
                        response.ContentType="application/json";
                      } else {
                        response.StatusCode=403;
                        responseString="403 Forbidden";
                      }
                    } else {
                      responseString="null";
                    }
                  } else {
                    string path=Path.GetFullPath(Path.Combine(_htPath, ctx.Request.RawUrl.Substring(1)));
                    if(path.StartsWith(_htPath) && File.Exists(path)) {
                      responseString=File.ReadAllText(path);
                      response.ContentType=Ext2ContentType(Path.GetExtension(path));
                    } else {
                      response.StatusCode=403;
                      responseString="403 Forbidden";
                    }
                  }

              }
              catch(Exception) {
                response.StatusCode=404;
              }
            } else if(ctx.Request.HttpMethod=="POST" && ctx.Request.HasEntityBody) {
              if(ctx.Request.RawUrl==@"/data?subscribe") {
                var sco=ctx.Request.Cookies["session"];
                ses=Session.Get(sco, userName);
                ctx.Response.SetCookie(ses.id);
                string sub=(new StreamReader(ctx.Request.InputStream, ctx.Request.ContentEncoding)).ReadToEnd();
                ses.Subscribe(sub);
              } else if(ctx.Request.RawUrl.StartsWith(@"/data/")) {
                string json=(new StreamReader(ctx.Request.InputStream, ctx.Request.ContentEncoding)).ReadToEnd();
                response.StatusCode=ProcessPublish(ctx.Request.RawUrl.Substring(5), json, userName);
              } else {
                response.StatusCode=400;
                responseString="400 Bad Request";
              }
            } else {
              response.StatusCode=405;
              if(ctx.Request.HttpMethod!="HEAD") {
                responseString="405 Method Not Allowed";
              }
            }
            if(!string.IsNullOrEmpty(responseString)) {
              byte[] buffer = System.Text.Encoding.UTF8.GetBytes(responseString);
              // Get a response stream and write the response to it.
              response.ContentLength64 = buffer.Length;
              System.IO.Stream output = response.OutputStream;
              output.Write(buffer, 0, buffer.Length);
              // You must close the output stream.
              output.Close();
            }
          }
        }
      }
      catch(ObjectDisposedException) {
      }
      catch(Exception ex) {
        Log.Error("ContextReady[{1}] Exception={0}", ex, RemoteEP);
      }
      if(_listener!=null && _listener.IsListening) {
        _listener.BeginGetContext(new AsyncCallback(ContextReady), null);
      }
    }

    private int ProcessPublish(string path, string json, string user) {
      Topic cur=Topic.root;
      Type vt=null;

      string[] pt=path.Split(new char[] { '/' }, StringSplitOptions.RemoveEmptyEntries);
      if(pt.Length>0 && pt[0]=="local") {
        return 400;
      }
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

    private string Ext2ContentType(string ext) {
      switch(ext) {
      case ".jpg":
      case ".jpeg":
        return "image/jpeg";
      case ".png":
        return "image/png";
      case ".htm":
      case ".html":
        return "text/html";
      case ".js":
        return "application/javascript";
      }
      return string.Empty;
    }

  }
}
