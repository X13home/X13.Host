#region license
//Copyright (c) 2011-2013 <comparator@gmx.de>; Wassili Hense

//This file is part of the X13.Home project.
//https://github.com/X13home

//BSD License
//See LICENSE.txt file for license details.
#endregion license

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Threading;

namespace X13.HttpSync {
  internal class HttpSyncItem : IDisposable {
    private static Topic _hsVal;
    private static DVar<bool> _verbose;

    static HttpSyncItem() {
      _hsVal=Topic.root.Get("/var/HttpSync");
      _verbose=Topic.root.Get<bool>("/etc/HttpSync/_verbose");
    }
    private Uri _uri;
    private HttpWebRequest _req;
    private string _host;
    private string _remotePath;
    private string _remoteBase;
    private Topic _local;
    private Cookie _cookie;
    private CredentialCache _cred;
    private Timer _timeout;
    private DVar<string> _statusStr;
    private HttpStatusCode _status;

    public readonly string name;
    public Uri uri { get { return _uri; } }
    public HttpSyncItem(string name, Uri uri) {
      this.name=name;
      _uri=uri;
      _timeout=new Timer(TRefresh);
      System.Threading.ThreadPool.QueueUserWorkItem(Connect);
      _statusStr=_hsVal.Get<string>(name+"/_status");
      _statusStr.saved=false;
      _status=HttpStatusCode.NoContent;
    }
    public void ChangeUri(Uri uri) {
      if(_req!=null) {
        _req.Abort();
        _req=null;
      }
      _uri=uri;
      System.Threading.ThreadPool.QueueUserWorkItem(Connect);
    }
    public void Dispose() {
      if(_timeout!=null) {
        _timeout.Change(Timeout.Infinite, Timeout.Infinite);
        _timeout=null;
      }
      if(_local!=null) {
        _local.Unsubscribe("#", _local_changed);
      }
      if(_req!=null) {
        _req.Abort();
        _req=null;
      }
    }

    private void Connect(object o) {
      if(_uri.IsDefaultPort) {
        _host=string.Concat(_uri.Scheme, "://", _uri.DnsSafeHost);
      } else {
        _host=string.Concat(_uri.Scheme, "://", _uri.DnsSafeHost, ":", _uri.Port.ToString());
      }
      _remotePath=_uri.AbsolutePath;
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
      if(!string.IsNullOrEmpty(_uri.UserInfo)) {
        var up=Uri.UnescapeDataString(_uri.UserInfo).Split(':');
        if(up.Length==2) {
          _cred = new CredentialCache();
          _cred.Add(new Uri(_host), "Basic", new NetworkCredential(up[0], up[1]));
        } else if(up.Length==1) {
          _cred = new CredentialCache();
          _cred.Add(new Uri(_host), "Basic", new NetworkCredential(up[0], string.Empty));
        }
      }
      _cookie=new Cookie("session", Guid.NewGuid().ToString());

      HttpWebResponse resp=null;
      try {
        _req=(HttpWebRequest)WebRequest.Create(string.Concat(_host, "/export?subscribe"));
        _req.CookieContainer=new CookieContainer(1);
        _req.CookieContainer.Add(new Uri(_host), _cookie);
        _req.Method="POST";
        _req.UserAgent="HttpSync v."+System.Reflection.Assembly.GetExecutingAssembly().GetName().Version.ToString(4);
        if(_cred!=null) {
          _req.UseDefaultCredentials=false;
        }
        _req.Credentials=_cred;

        byte[] buf=Encoding.UTF8.GetBytes(Uri.EscapeDataString(_remotePath));
        _req.ContentLength=buf.Length;
        using(Stream s=_req.GetRequestStream()) {
          s.Write(buf, 0, buf.Length);
        }
        using(resp=_req.GetResponse() as HttpWebResponse) {
          resp.Close();
          if(resp.StatusCode!=HttpStatusCode.OK) {
            if(_status!=resp.StatusCode) {
              Log.Warning("subscribe {1} as /var/HttpSync/{0} - {2}", name, _uri.OriginalString, resp.StatusDescription);
              _status=resp.StatusCode;
              _statusStr.value=_status.ToString();
            }
            _timeout.Change(180000, 150000);
            _req=null;
            return;
          }
        }
      }
      catch(WebException ex) {
        HttpStatusCode st=resp==null?HttpStatusCode.RequestTimeout:resp.StatusCode;
        if(_status!=st) {
          Log.Warning("subscribe {1} as /var/HttpSync/{0} - {2}", name, _uri.OriginalString, ex.Message);
          _status=st;
          if(resp!=null) {
            _statusStr.value=_status.ToString();
          } else {
            _statusStr.value=ex.Status.ToString();
          }
        }
        _timeout.Change(180000, 150000);
        _req=null;
        return;
      }
      try {
        _req=(HttpWebRequest)WebRequest.Create(string.Concat(_host, "/export?read"));
        _req.CookieContainer=new CookieContainer(1);
        _req.CookieContainer.Add(new Uri(_host), _cookie);
        _req.Method="GET";
        _req.UserAgent="HttpSync v."+System.Reflection.Assembly.GetExecutingAssembly().GetName().Version.ToString(4);
        if(_cred!=null) {
          _req.UseDefaultCredentials=false;
        }
        _req.Credentials=_cred;
        _req.BeginGetResponse(RespCallback, null);
        _timeout.Change(180000, 150000);
      }
      catch(WebException ex) {
        Log.Warning("Pool {1} as /var/HttpSync/{0} - {2}", name, _uri.OriginalString, ex.Message);
      }
    }
    private void RespCallback(IAsyncResult asynchronousResult) {
      HttpWebResponse resp=null;
      try {
        using(resp=_req.EndGetResponse(asynchronousResult) as HttpWebResponse) {
          if(resp.StatusCode!=HttpStatusCode.OK) {
            if(_status!=resp.StatusCode) {
              _status=resp.StatusCode;
              _statusStr.value=_status.ToString();
              Log.Warning("Pool({1}) as /var/HttpSync/{0} - {2}", name, _uri.OriginalString, resp.StatusDescription);
            }
          } else {
            _status=resp.StatusCode;
            _statusStr.value=_status.ToString();
            using(var s=resp.GetResponseStream()) {
              if(s.CanRead) {
                using(var sr=new StreamReader(s, Encoding.UTF8)) {
                  string content=sr.ReadToEnd();
                  var sa=content.Substring(1, content.Length-2).Split(new string[] { "}\r\n{" }, StringSplitOptions.RemoveEmptyEntries);
                  for(int n=0; n<sa.Length-1; n++) { // last is /local/mq
                    int idx=sa[n].IndexOf("\":");
                    if(idx<0) {
                      continue;
                    }
                    string relPath=sa[n].Substring(1, idx-1);
                    if(relPath.StartsWith(_remoteBase)) {
                      if(relPath.Length==_remoteBase.Length) {
                        relPath=name;
                      } else {
                        relPath=name+relPath.Substring(_remoteBase.Length);
                      }
                    } else {
                      continue;
                    }
                    string json=sa[n].Substring(idx+3);
                    Parse(relPath, json);
                  }
                  if(_verbose.value) {
                    Log.Info("/var/HttpSync/{0} <<< {1}", name, content);
                  }
                }
              }
            }
          }
        }

        _req=(HttpWebRequest)WebRequest.Create(string.Concat(_host, "/export?read"));
        _req.CookieContainer=new CookieContainer(1);
        _req.CookieContainer.Add(new Uri(_host), _cookie);
        _req.Method="GET";
        _req.UserAgent="HttpSync v."+System.Reflection.Assembly.GetExecutingAssembly().GetName().Version.ToString(4);
        if(_cred!=null) {
          _req.UseDefaultCredentials=false;
        }
        _req.Credentials=_cred;
        _req.BeginGetResponse(RespCallback, null);
        _timeout.Change(180000, 150000);
      }
      catch(NullReferenceException) {
        return;
      }
      catch(WebException ex) {
        if(ex.Status!=WebExceptionStatus.RequestCanceled) {
          Log.Warning("Pool {1} as /var/HttpSync/{0} - {2}", name, _uri.OriginalString, ex.Message);
        }
      }
    }
    private void Parse(string rp, string json) {
      Topic cur;
      if(!string.IsNullOrEmpty(json)) {         // Publish
        if(!_hsVal.Exist(rp, out cur) || cur.valueType==null) {
          Type vt=X13.WOUM.ExConverter.Json2Type(json);
          cur=Topic.GetP(_hsVal.path+"/"+rp, vt, _hsVal);
        }
        if(_local==null) {
          _local=_hsVal.Get(name);
        }
        cur.saved=false;
        if(cur.valueType!=null) {
          cur.FromJson(json, _local);
        }
      } else if(_hsVal.Exist(rp, out cur)) {                      // Remove
        cur.Remove(_local);
      }
    }
    private void _local_changed(Topic sender, TopicChanged p) {
    }
    private void TRefresh(object o) {
      if(_req!=null) {
        _req.Abort();
        _req=null;
      }
      System.Threading.ThreadPool.QueueUserWorkItem(Connect);
    }
  }
}
