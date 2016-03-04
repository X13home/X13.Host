using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;
using X13.Agent2;

namespace X13.Agent3 {
  internal class TopicSrc : INotifyPropertyChanged {
    private static Client _cl;
    private static Dictionary<string, TopicSrc> _rep;
    static TopicSrc() {
      _rep = new Dictionary<string, TopicSrc>();
      try {
        TopicSrc.Import("../data/Agent3.xst");
      }
      catch(Exception ex) {
        Log.Error(ex.ToString());
      }

      var urlT = TopicSrc.Get("/local/cfg/Client/URL");
      string url;
      if(urlT == null || (url = urlT.value as string) == null) {
        url = "ws://local@localhost/";
      }
      _cl = new Client(new Uri(url));
    }
    public static void Close() {
      if(_cl != null) {
        _cl.Close();
        _cl = null;
      }
    }
    private static void Import(string fileName) {
      try {
        XDocument doc = XDocument.Load(fileName);
        string path, value;
        if(doc.Root.Attribute("head") != null) {
          path = doc.Root.Attribute("head").Value;
        } else {
          path = string.Empty;
        }
        value = doc.Root.Attribute("value") != null ? doc.Root.Attribute("value").Value : null;
        var cur = new TopicSrc(path, value);
        foreach(var xNext in doc.Root.Elements("item")) {
          Import(xNext, path + "/");
        }
      }
      catch(Exception ex) {
        Log.Error("TopicSrc.Import({0}) - {1}", fileName, ex.Message);
      }
    }
    private static void Import(XElement xElement, string oPath) {
      if(xElement == null || xElement.Attribute("name") == null) {
        return;
      }
      string path = oPath + xElement.Attribute("name").Value;
      string value = xElement.Attribute("value") != null ? xElement.Attribute("value").Value : null;
      var cur = new TopicSrc(path, value);
      foreach(var xNext in xElement.Elements("item")) {
        Import(xNext, path + "/");
      }
    }
    private static void Write(TopicSrc t) {
      int i;
      try {
        string json = Newtonsoft.Json.JsonConvert.SerializeObject(t.value);
        Log.Debug("TopicSrc.Write({0}, {1})", t.path, json);
        var ns = t.path.Split(new char[] { '/' }, StringSplitOptions.RemoveEmptyEntries);

        XDocument doc = XDocument.Load("../data/Agent3.xst");
        var cur = doc.Root;
        XElement next;
        var hs = cur.Attribute("head").Value.Split(new char[] { '/' }, StringSplitOptions.RemoveEmptyEntries);
        for(i = 0; i < hs.Length; i++) {
          if(ns.Length <= i || ns[i]!=hs[i]) {
            return;
          }
        }
        for(; i < ns.Length; i++) {
          next = cur.Elements("item").FirstOrDefault(z => z.Attribute("name")!=null && z.Attribute("name").Value == ns[i]);
          if(next == null) {
            next=new XElement("item", new XAttribute("name", ns[i]));
            var ch = cur.Elements("item").LastOrDefault(z => z.Attribute("name") != null && string.Compare(z.Attribute("name").Value, ns[i]) < 0);
            if(ch == null) {
              cur.Add(next);
            } else {
              ch.AddAfterSelf(next);
            }
          }
          if(i == ns.Length - 1) {
            next.SetAttributeValue("value", json);
          }
          cur=next;
        }
        using(StreamWriter sw = File.CreateText("../data/Agent3.xst")) {
          using(var writer = new System.Xml.XmlTextWriter(sw)) {
            writer.Formatting = System.Xml.Formatting.Indented;
            writer.QuoteChar = '\'';
            writer.WriteNode(doc.CreateReader(), false);
            writer.Flush();
          }
        }
      }
      catch(Exception ex) {
        Log.Debug("TopicSrc.Write({0}, {1}) - {3}", t.path, t.value, ex);
      }
    }

    public static TopicSrc Get(string path, bool create = false) {
      TopicSrc r;
      if(!_rep.TryGetValue(path, out r)) {
        if(create) {
          r = new TopicSrc(path, null);
        } else {
          r = null;
        }
      }
      return r;
    }

    private TopicSrc _parent;
    private object _value;
    private bool _local;
    private SortedList<string, TopicSrc> _children;

    public readonly string name;
    public readonly string path;
    public bool saved;

    public TopicSrc(string path)
      : this(path, string.IsNullOrEmpty(path) || path.StartsWith("/local")) {
    }

    private TopicSrc(string path, bool local) {
      _local = local;
      if(string.IsNullOrEmpty(path)) {
        name = string.Empty;
        this.path = "/";
        _parent = null;
      } else {
        int idx = path.LastIndexOf('/');
        if(idx >= 0 && path.Length > idx + 1) {
          name = path.Substring(idx + 1);
          if(idx > 0) {
            _rep.TryGetValue(path.Substring(0, idx), out _parent);
            if(_parent != null) {
              _parent._children[name] = this;
            }
          }
        } else {
          name = string.Empty;
          _parent = null;
        }
        this.path = path;
      }
      _children = new SortedList<string, TopicSrc>();
      if(!_local && _cl != null) {
        _cl.Subscribe(this.path, OnChange);
      }
      _rep[path] = this;
      foreach(var kv in _rep.Where(z => z.Key != null && z.Key.StartsWith(path) && z.Key.LastIndexOf('/') == path.Length)) {
        _children[kv.Value.name] = kv.Value;
        kv.Value._parent = this;
      }
    }
    private TopicSrc(string path, string json)
      : this(path, true) {
      if(!string.IsNullOrWhiteSpace(json)) {
        try {
          _value = Newtonsoft.Json.JsonConvert.DeserializeObject(json);
        }
        catch(Exception ex) {
          _value = null;
          Log.Warning("TopicSrc({0}, {1})+Deserialize - {2}", path, json, ex.Message);
        }
      } else {
        _value = null;
      }
    }

    public object value {
      get {
        return _value;
      }
      set {
        _value = value;

        if(!_local) {
          //_cl.Send(_path, Newtonsoft.Json.JsonConvert.SerializeObject(_value);
        } else if(saved) {
          Write(this);
        }

        if(PropertyChanged != null) {
          PropertyChanged(this, new PropertyChangedEventArgs("value"));
        }
      }
    }

    public IEnumerable<TopicSrc> children { get { return _children.Values; } }
    private void OnChange(string path, string value) {
      if(!string.IsNullOrWhiteSpace(value)) {
        try {
          _value = Newtonsoft.Json.JsonConvert.DeserializeObject(value);
        }
        catch(Exception ex) {
          _value = null;
          Log.Warning("TopicSrc.OnChange({0}, {1})+Deserialize - {2}", path, value, ex.Message);
        }
        if(PropertyChanged != null) {
          PropertyChanged(this, new PropertyChangedEventArgs("value"));
        }
      } else {
        _value = null;
      }
    }

    public event PropertyChangedEventHandler PropertyChanged;
  }
}
