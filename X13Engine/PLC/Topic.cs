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
using System.ComponentModel;
using System.IO;
using System.Threading;
using System.Xml.Linq;

namespace X13.PLC {
  /// <summary> Basis class in Message Queue Telemetry Library</summary>
  public partial class Topic : IComparable<Topic> {

    public static readonly Topic root;
    public static bool brokerMode { get; internal set; }
    public static bool Import(string fileName, string path=null) {
      if(string.IsNullOrEmpty(fileName) || !File.Exists(fileName)) {
        return false;
      }
      using(StreamReader reader = File.OpenText(fileName)) {
        Import(reader, path);
      }
      return true;
    }

    public static void Export(string filename, Topic head) {
      if(filename==null || head==null) {
        throw new ArgumentNullException();
      }
      XDocument doc=new XDocument(new XElement("root", new XAttribute("head", head.path)));
      if(head.valueType!=null) {
        doc.Root.Add(new XAttribute("value", head.ToJson()));
        if(head.saved) {
          doc.Root.Add(new XAttribute("saved", bool.TrueString));
        }
        doc.Root.Add(new XAttribute("type", head.valueType.FullName));
      }
      foreach(Topic t in head.children) {
        Export(doc.Root, t);
      }
      using(StreamWriter writer = File.CreateText(filename)) {
        doc.Save(writer);
      }
    }

    [Browsable(false)]
    public Topic parent { get; protected set; }

    [Category("Location"), DisplayName("Name")]
    public string name {
      get {
        return _name;
      }
      set {
        this.Move(this.parent, value);
      }
    }

    [Category("Location"), DisplayName("Path"), ReadOnly(true)]
    public string path { get; protected set; }

    [Category("Content"), DisplayName("Type"), ReadOnly(true)]
    public Type valueType { get; protected set; }

    [Category("Content"), DisplayName("Saved"), ReadOnly(false)]
    public bool saved { get; set; }

    [Browsable(false)]
    public IEnumerable<Topic> all { get { return new TopicEnumerator(this, true); } }

    [Browsable(false)]
    public IEnumerable<Topic> children { get { return new TopicEnumerator(this, false); } }

    public event Action<Topic, TopicChanged> changed {
      add { 
        Subscribe("", value);

      }
      remove { 
        Unsubscribe("", value);
      }
    }
    public bool Exist(string path) {
      Topic tmp;
      return Exist(path, out tmp);
    }
    public bool Exist(string path, out Topic topic) {
      if(string.IsNullOrEmpty(path)) {
        topic=null;
        return false;
      }
      string[] pe=path.Split(new char[] { '/' }, StringSplitOptions.RemoveEmptyEntries);
      Topic cur;
      Topic next=null;
      if(!string.IsNullOrEmpty(path) && path.StartsWith("/")) {
        cur=root;
      } else {
        cur=this;
      }
      for(int i=0; i<pe.Length; i++, cur=next) {
        if(cur==null || cur._childNodes==null || !cur._childNodes.ContainsKey(pe[i])) {
          topic=null;
          return false;
        }
        next=cur._childNodes[pe[i]];
      }
      topic=cur;
      return cur!=null;
    }
    public Topic Get(string path, Topic initiator=null) {
      return GetP(path, null, initiator, this);
    }
    public DVar<T> Get<T>(string path, Topic initiator=null) {
      return Topic.GetP(path, typeof(T), initiator, this) as DVar<T>;
    }

    public Subscription Subscribe(string path, Action<Topic, TopicChanged> func, QoS qos=QoS.AtMostOnce) {
      Topic cur;
      string[] lvls;
      int i=0;
      if(string.IsNullOrWhiteSpace(path)) {
        cur=this;
        lvls=new string[0];
      } else {
        lvls=path.Split(_delmiter, StringSplitOptions.RemoveEmptyEntries);
        cur=path[0]=='/'?root:this;
        for(; i<lvls.Length; i++) {
          if(lvls[i]=="+" || lvls[i]=="#") {
            break;
          }
          cur=cur.Get(lvls[i]);
        }
      }
      Subscription s=null;
      lock(cur._subs) {
        foreach(var st in cur._subs.Where(z => z.func==func)) {
          if(st.lvls.Length!=lvls.Length-i) {
            continue;
          }
          for(int j=0; j<st.lvls.Length; j++) {
            if(st.lvls[j]!=lvls[j+i]) {
              goto l1;
            }
          }
          s=st;
          break;
l1: { }
        }
      }
      if(s==null) {
        s=new Subscription(lvls.Skip(i).ToArray(), func) { path=path, qos=qos };
        lock(cur._subs) {
          cur._subs.Add(s);
        }
      }

      // publish
      if(Topic.brokerMode) {
        var ts=(new TopicEnumerator(s.lvls, 0, cur)).GetEnumerator();
        if(ts.MoveNext()) {
          _publishQueue.Enqueue(new TopicChanged(TopicChanged.ChangeArt.Value) { Sender=cur, Subscription=s, Task=ts });
        }
      }
      return s;
    }
    public void Unsubscribe(string sPath, Action<Topic, TopicChanged> func) {
      Topic cur;
      string[] lvls;
      int i=0;
      if(string.IsNullOrWhiteSpace(sPath)) {
        cur=this;
        lvls=new string[0];
      } else {
        lvls=sPath.Split(_delmiter, StringSplitOptions.RemoveEmptyEntries);
        cur=sPath[0]=='/'?root:this;
        for(; i<lvls.Length; i++) {
          if(lvls[i]=="+" || lvls[i]=="#") {
            break;
          }
          if(!cur.Exist(lvls[i], out cur)) {
            return;   // node not exist
          }
        }
      }
      lock(cur._subs) {
        foreach(var s in cur._subs.Where(z => z.func==func)) {
          if(s.lvls.Length!=lvls.Length-i) {
            continue;
          }
          for(int j=0; j<s.lvls.Length; j++) {
            if(s.lvls[j]!=lvls[j+i]) {
              goto l1;
            }
          }
          cur._subs.Remove(s);
          break;
l1: { }
        }
      }
    }

    public virtual object GetValue() {
      return null;
    }
    public virtual void SetValue(object value, TopicChanged param) {
      Log.Error("SetValue({0}, {1}", value, this.path);
    }

    public void Move(Topic nParent, string name) {
      if(name.Contains('/') || name=="+" || name=="#") {
        throw new ArgumentException(string.Format("{0}.Move( , {1}) - name is not valid", this.path, name));
      }
      if(nParent==this.parent && this.name==name) {
        return;
      }
      if(nParent._childNodes==null) {
        nParent._childNodes=new SortedList<string, Topic>();
      }
      if(nParent._childNodes.ContainsKey(name)) {
        throw new ArgumentException(string.Format("{0} allready exist in {1}", name, nParent.path));
      }
      this.parent._childNodes.Remove(this.name);

      this.parent=nParent;
      this.parent._childNodes.Add(name, this);
      this._name=name;
      var fake=Topic.GetP(this.path, this.valueType, root);
      UpdateMovedTopicsDeep();
      fake.Remove();    // Remove with correct path
    }

    public void Remove(Topic initiator=null) {
      if(System.Threading.Interlocked.CompareExchange(ref _disposed, 1, 0)==0) {
        if(this._childNodes!=null) {
          for(int i=_childNodes.Count-1; i>=0; i--) {
            _childNodes.Values[i].Remove(_mq);
          }
        }
        this.Publish(this, TopicChanged.ChangeArt.Remove, initiator);
        this.parent._childNodes.Remove(this.name);
        if(GetValue()!=null && GetValue() is ITopicOwned) {
          (GetValue() as ITopicOwned).SetOwner(null);
        }
        if(GetValue()!=null && GetValue() is IDisposable) {
          (GetValue() as IDisposable).Dispose();
        }
        if(this.parent._childNodes.Count==0 && this.parent.valueType==null && this.parent!=_mq && this.parent!=root) {
          this.parent.Remove(_mq);
        }
        _disposed=2;
      }
    }

    public override string ToString() {
      return this.path;
    }

    #region IComparable<Topic> Members
    public int CompareTo(Topic other) {
      return this.path.CompareTo(other.path);
    }
    #endregion

    public class Subscription {
      public readonly string[] lvls;
      public readonly Action<Topic, TopicChanged> func;
      public string path;
      public QoS qos;

      public Subscription(string[] lvls, Action<Topic, TopicChanged> func) {
        this.lvls=lvls;
        this.func=func;
      }
    }

  }
  public struct TopicChanged {
    public enum ChangeArt {
      Value,
      Add,
      Remove
    }
    private List<Topic> _route;
    internal IEnumerator<Topic> Task;
    internal Topic Source;
    internal Topic Sender;
    public Topic.Subscription Subscription;
    public  ChangeArt Art;

    public TopicChanged(ChangeArt art, Topic initiator=null) {
      _route=new List<Topic>();
      Art=art;
      Task=null;
      Source=null;
      Sender=null;
      Subscription=null;
      if(initiator!=null) {
        _route.Add(initiator);
      }
    }
    public TopicChanged(TopicChanged old) {
      _route=old._route.Where(t => !t.path.StartsWith("/local/MQ") && t.valueType!=typeof(X13.MQTT.MsDevice)).ToList();
      Art=old.Art;
      Source=old.Source;
      Sender=old.Sender;
      Subscription=old.Subscription;
      Task=old.Task;
    }
    public bool Visited(Topic it, bool save) {
      if(it==null) {
        return false;
      }
      if(_route.Contains(it)) {
        return true;
      } else {
        if(save) {
          _route.Add(it);
        }
        return false;
      }
    }
    public Topic Initiator {
      get {
        return (_route.Count>0 && _route[0]!=Source)?_route[0]:null;
      }
    }
    public override string ToString() {
      StringBuilder sb=new StringBuilder();
      sb.AppendFormat("{0} [{1}] snd={2}", Source, Art, Sender);
      if(Initiator!=null) {
        sb.AppendFormat(" ini={0}", Initiator);
      }
      if(Subscription!=null) {
        sb.AppendFormat(" func={0}", Subscription.func.Method.Name);
      }
      return sb.ToString();
    }
  }
  public interface ITopicOwned {
    void SetOwner(Topic owner);
  }

  /// <summary>Quality of service levels</summary>
  public enum QoS : byte {
    ExactlyOnce = 2,
    AtLeastOnce = 1,
    AtMostOnce  = 0
  }

}
