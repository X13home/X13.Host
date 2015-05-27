#region license
//Copyright (c) 2015 Wassili Hense

//This file is part of the X13.Home project.
//https://github.com/X13home

//BSD License
//See LICENSE.txt file for license details.
#endregion license

using Jurassic;
using Jurassic.Library;
using System;
using System.ComponentModel.Composition;
using System.IO;
using System.Linq;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Net;

namespace X13.PLC.jsFunc {
  //this.S=false; WebClient.GetString("http://www-app3.gfz-potsdam.de/kp_index/qlyymm.wdc", this, function(o){ var idx=o.rez.lastIndexOf("\n", o.rez.length-2);  var ll=o.rez.substring(idx+1, o.rez.length-2); this.R=ll; var i=idx+57; for(i=57; i>33; i-=3){ if(ll.charCodeAt(i)>=0x30 && ll.charCodeAt(i)<=0x39){ this.Q=ll.charCodeAt(i)-0x30; this.S=true; break; } } });
  public sealed class WebClientJS : ObjectInstance {
    private static ScriptEngine _ge;

    public WebClientJS(ScriptEngine engine)
      : base(engine) {
        _ge=engine;
      this.PopulateFunctions();
    }
    [JSFunction(Name = "GetString")]
    public static void GetString(string url, ObjectInstance Self, FunctionInstance cb) {
      JsFunc.TInst This=Self as JsFunc.TInst;
      if(This==null || cb==null) {
        return;
      }
      Uri u;
      if(!Uri.TryCreate(url, UriKind.Absolute, out u) || !u.IsAbsoluteUri) {
        Log.Warning("{0} WebClient.GetString({1}) bad uri", This._owner, url);
        return;
      }
      var wc=new WebClient();
      wc.DownloadStringCompleted+=wc_DownloadStringCompleted;
      wc.DownloadStringAsync(u, new Action<ObjectInstance>(o => cb.Call(This, o)));
    }

    static void wc_DownloadStringCompleted(object sender, DownloadStringCompletedEventArgs e) {
      try {
        ObjectInstance rez=_ge.Object.Construct();
        Action<ObjectInstance> f=e.UserState as Action<ObjectInstance>;
        if(f==null) {
          return;
        }
        if(e.Error!=null) {
          rez["error"]=e.Error.Message;
        }
        rez["rez"]=e.Result;
        f(rez);
      }
      catch(Exception ex) {
        Log.Warning("{0}", ex.Message);
      }
    }
  }
}
