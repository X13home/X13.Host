#region license
//Copyright (c) 2011-2013 <comparator@gmx.de>; Wassili Hense

//This file is part of the X13.Home project.
//https://github.com/X13home

//BSD License
//See LICENSE.txt file for license details.
#endregion license

using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;

namespace X13.HttpSync {
  [Export(typeof(IPlugModul))]
  [ExportMetadata("priority", 19)]
  [ExportMetadata("name", "HttpSync")]
  public class HttpSyncPl : IPlugModul {
    private DVar<bool> _verbose;
    private Topic _hsRoot;
    private List<HttpSyncItem> _items;

    public HttpSyncPl() {
      _items=new List<HttpSyncItem>();
    }
    
    public void Init() {
      _hsRoot=Topic.root.Get("/etc/HttpSync");
    }
    public void Start() {
      _verbose=_hsRoot.Get<bool>("_verbose");
      _hsRoot.Subscribe("+", HsrChanged);
    }
    public void Stop() {
      for(int i=0; i<_items.Count; i++) {
        _items[i].Dispose();
      }
      _items.Clear();
    }

    private void HsrChanged(Topic sender, TopicChanged arg) {
      DVar<string> dv=sender as DVar<string>;
      if(dv==null || sender==_verbose) {
        return;
      }
      if(arg.Art==TopicChanged.ChangeArt.Remove) {
        _items.RemoveAll(z => z.name==dv.name);
      } else if(!string.IsNullOrWhiteSpace(dv.value)) {
        Uri u;
        try {
          u=new Uri(dv.value);
        }
        catch(UriFormatException ex) {
          Log.Warning("{0}=\"{1}\" - {2}", dv.path, dv.value, ex.Message);
          return;
        }
        HttpSyncItem it=_items.FirstOrDefault(z => z.name==dv.name);
        if(it==null) {
          it=new HttpSyncItem(dv.name, u);
          _items.Add(it);
        } else {
          it.ChangeUri(u);
        }
      }
    }
  }
}
