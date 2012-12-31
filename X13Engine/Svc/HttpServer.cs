#region license
//Copyright (c) 2011-2013 <comparator@gmx.de>; Wassili Hense

//This file is part of the X13.Home project.
//https://github.com/X13home

//BSD License
//See LICENSE.txt file for license details.
#endregion license

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Net;
using System.IO;
using System.Reflection;
using X13.PLC;

namespace X13.Svc {
  internal class HttpServer {
    //private static HttpServer _http;

    //static void Main(string[] args) {
    //  Log.Write+=new Action<LogLevel, DateTime, string>(Log_Write);

    //  _http=new HttpServer();
    //  _http.Start();
    //  Console.ReadKey();
    //  _http.Stop();
    //}

    //if(System.Environment.OSVersion.Version.Major >= 6) {
    //  ProcessStartInfo info = new ProcessStartInfo("netsh") { Arguments=@"http add urlacl url=http://+:80/ user=NetworkService" };
    //  Process p = Process.Start(info);
    //}

    private HttpListener _listener;
    private string _htPath;

    public void Start() {
      string startPath=Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
      _htPath=startPath.Substring(0, startPath.LastIndexOf('\\'))+"\\htdocs";
      _listener = new HttpListener();
      _listener.Prefixes.Add(@"http://+:80/");
      _listener.Start();
      _listener.AuthenticationSchemes=AuthenticationSchemes.Basic;
      _listener.BeginGetContext(new AsyncCallback(ContextReady), null);
    }
    public void Stop() {
      _listener.Close();
    }
    private void ContextReady(IAsyncResult ar) {
      bool userPassWrong=true;
      try {
        var context = _listener.EndGetContext(ar);

        // Obtain a response object.
        Log.Debug("{0} {1}", context.Request.HttpMethod, context.Request.RawUrl);
        if(context.User!=null) {
          var id=context.User.Identity as HttpListenerBasicIdentity;
          userPassWrong=!MQTT.MqBroker.CheckAuth(id.Name, id.Password);
        } else {
          userPassWrong=false;
        }
        using(HttpListenerResponse response = context.Response) {
          if(userPassWrong) {
            response.StatusCode=401;
          } else if(context.Request.HttpMethod=="GET") {
            try {
              string responseString=string.Empty;
              if(context.Request.RawUrl==@"/") {
                responseString=File.ReadAllText(Path.Combine(_htPath, "index.html"));
              } else if(context.Request.RawUrl.StartsWith(@"/MQTT/")) {
                string mqPath=context.Request.RawUrl.Substring(5);
                Topic cur;
                if(Topic.root.Exist(mqPath, out cur)) {
                  if(MQTT.MqBroker.CheckAcl(context.User.Identity.Name, cur, TopicAcl.Subscribe)) {
                    responseString=cur.ToJson();
                    response.ContentType="application/json";
                  } else {
                    response.StatusCode=403;
                    responseString="Forbidden";
                  }
                } else {
                  responseString="null";
                }
              } else {
                string path=Path.Combine(_htPath, context.Request.RawUrl.Substring(1));
                if(Path.GetFullPath(path).StartsWith(_htPath)) {
                  responseString=File.ReadAllText(path);
                } else {
                  response.StatusCode=403;
                  responseString="Forbidden";
                }
              }

              byte[] buffer = System.Text.Encoding.UTF8.GetBytes(responseString);
              // Get a response stream and write the response to it.
              response.ContentLength64 = buffer.Length;
              System.IO.Stream output = response.OutputStream;
              output.Write(buffer, 0, buffer.Length);
              // You must close the output stream.
              output.Close();
            }
            catch(Exception) {
              response.StatusCode=404;
            }

          } else {
            response.StatusCode=405;
          }
        }

        _listener.BeginGetContext(new AsyncCallback(ContextReady), null);
      }
      catch(ObjectDisposedException) {
      }
      catch(Exception ex) {
        Log.Error("ContextReady Exception={0}", ex);
      }
    }
  }
}
