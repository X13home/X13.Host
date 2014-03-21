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

namespace X13 {
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
        doc.Root.Add(new XAttribute("type", WOUM.ExConverter.Type2Name(head.valueType)));
      }
      foreach(Topic t in head.children) {
        Export(doc.Root, t);
      }
      using(StreamWriter writer = File.CreateText(filename)) {
        doc.Save(writer);
      }
    }

    internal static event Action<Subscription, bool> SubscriptionsChg;

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
    public bool disposed { get { return _disposed>0; } }

    [Browsable(false)]
    public IEnumerable<Topic> all { get { return new TopicEnumerator(this, true); } }

    [Browsable(false)]
    public IEnumerable<Topic> children { get { return new TopicEnumerator(this, false); } }

    [Browsable(false)]
    public IEnumerable<Subscription> subscriptions { get { return new SubscriptionEnumerator(this); } }

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
    public IEnumerable<Topic> Find(string mask) {
      return new TopicEnumerator(mask.Split(_delmiter, StringSplitOptions.RemoveEmptyEntries), 0, this);
    }

    public Subscription Subscribe(string path, Action<Topic, TopicChanged> func, QoS qos=QoS.AtMostOnce) {
      Topic cur;
      string[] lvls;
      int i=0;
      if(string.IsNullOrEmpty(path)) {
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
        s=new Subscription(lvls.Skip(i).ToArray(), func, cur) { qos=qos };
        lock(cur._subs) {
          cur._subs.Add(s);
        }
      }
      if(SubscriptionsChg!=null) {
        SubscriptionsChg(s, true);
      }
      // publish
      if(func.Method.DeclaringType!=typeof(MQTT.MqClient)) {
        foreach(var t in new TopicEnumerator(s.lvls, 0, cur)) {
          t.PublishSubs(new TopicChanged(TopicChanged.ChangeArt.Value) {Source=t, Sender=t, Subscription=s }, func);
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
          if(SubscriptionsChg!=null) {
            SubscriptionsChg(s, false);
          }
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
      var fake=Topic.GetP(this.path, this.valueType, _mq);
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
      public readonly Topic owner;
      public string path { get { return (owner==Topic.root?string.Empty:owner.path)+(lvls.Length>0?("/"+string.Join("/", lvls)):string.Empty); } }
      public QoS qos;

      public Subscription(string[] lvls, Action<Topic, TopicChanged> func, Topic owner) {
        this.lvls=lvls;
        this.func=func;
        this.owner=owner;
      }
      public override string ToString() {
        return path;
      }
    }
    private class SubscriptionEnumerator : IEnumerable<Subscription> {
      private Topic _topic;
      public SubscriptionEnumerator(Topic cur) {
        this._topic=cur;
      }

      IEnumerator<Subscription> IEnumerable<Subscription>.GetEnumerator() {
        if(_topic._subs!=null) {
          foreach(var s in _topic._subs.ToArray()) {
            if(s.func.Method.DeclaringType!=typeof(MQTT.MqClient) && s.lvls.Length==1 && s.lvls[0]=="#") {
              yield return s;
              yield break;
            }
          }
          foreach(var s in _topic._subs.ToArray()) {
            if(s.func.Method.DeclaringType!=typeof(MQTT.MqClient)) {
              yield return s;
            }
          }
        }
        if(_topic._childNodes!=null) {
          foreach(var t in _topic._childNodes.ToArray()) {
            foreach(var s in new SubscriptionEnumerator(t.Value)) {
              yield return s;
            }
          }
        }
      }

      System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() {
        throw new NotImplementedException();
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
    private Topic _initiator;
    public Topic Source;
    internal Topic Sender;
    public Topic.Subscription Subscription;
    public  ChangeArt Art;

    public TopicChanged(ChangeArt art, Topic initiator=null) {
      _route=new List<Topic>();
      Art=art;
      Source=null;
      Sender=null;
      Subscription=null;
      _initiator=initiator;
    }
    public TopicChanged(TopicChanged old) {
      _route=old._route.Where(t => !t.path.StartsWith("/local/MQ")).ToList();
      Art=old.Art;
      Source=old.Source;
      Sender=old.Sender;
      Subscription=old.Subscription;
      if(old._initiator==null || old._initiator.path.StartsWith("/local/MQ")) {
        _initiator=null;
      } else {
        _initiator=old._initiator;
      }
    }
    public bool Visited(Topic it, bool save) {
      if(it==null) {
        return false;
      }
      if(it==Initiator || _route.Contains(it)) {
        return true;
      } else {
        if(save) {
          _route.Add(it);
        }
        return false;
      }
    }
    public Topic Initiator { get { return _initiator; } }
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
