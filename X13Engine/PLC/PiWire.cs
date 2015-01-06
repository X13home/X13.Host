#region license
//Copyright (c) 2011-2015 <comparator@gmx.de>; Wassili Hense

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
using X13.MQTT;
using System.Runtime.Serialization;

namespace X13.PLC {
  [Newtonsoft.Json.JsonObject(Newtonsoft.Json.MemberSerialization.OptIn)]
  public class PiWire : ITopicOwned {
    private DVar<PiWire> _owner;
    private Topic _a;
    private Topic _b;
    [Newtonsoft.Json.JsonProperty]
    private string _pa, _ta, _pb, _tb;
    [Newtonsoft.Json.JsonProperty]
    private byte _dir;
    private PiLogram _parent;

    public PiWire(Topic tA, Topic tB, byte tDir) {
      _a = tA;
      _b = tB;
      _dir = tDir;
    }

    public Topic A { get { return _a; } }
    public Topic B { get { return _b; } }
    /// <summary>0 - A<=>B, 1 - A=>B, 2 - B=>A </summary>
    public byte Direction { get { return _dir; } }

    #region ITopicOwned Members
    void ITopicOwned.SetOwner(Topic owner) {
      if (_owner != owner) {
        if (_owner != null) {
          _owner.Unsubscribe("+", _owner_changed);
          Change_A(null);
          Change_B(null);
          _parent = null;
        }
        _owner = owner as DVar<PiWire>;
        if (_owner != null) {
          _owner.saved = true;
          var dc = _owner.Get<string>("_declarer");
          dc.saved = true;
          dc.value = "Wire";
          if (_owner.parent != null && _owner.parent.valueType == typeof(PiLogram)) {
            _parent = (_owner.parent as DVar<PiLogram>).value;
          }
          _owner.Subscribe("+", _owner_changed);
          if (exec) {
            Change_A(_a);
            Change_B(_b);
          }
        }
      }
    }
    #endregion ITopicOwned Members

    [OnSerializing]
    internal void OnSerializingMethod(StreamingContext context) {
      if (_a != null) {
        _pa = _a.path;
        _ta = _a.valueType == null ? string.Empty : WOUM.ExConverter.Type2Name(_a.valueType);
      } else {
        _pa = string.Empty;
        _ta = string.Empty;
      }
      if (_b != null) {
        _pb = _b.path;
        _tb = _b.valueType == null ? string.Empty : WOUM.ExConverter.Type2Name(_b.valueType);
      } else {
        _pb = string.Empty;
        _tb = string.Empty;
      }
    }
    [OnDeserialized]
    internal void OnDeserializedMethod(StreamingContext context) {
      if (!string.IsNullOrEmpty(_pa) && !Topic.root.Exist(_pa, out _a)) {
        _a = Topic.GetP(_pa, string.IsNullOrEmpty(_ta) ? null : WOUM.ExConverter.FullName2Type(_ta));
      }
      if (!string.IsNullOrEmpty(_pb) && !Topic.root.Exist(_pb, out _b)) {
        _b = Topic.GetP(_pb, string.IsNullOrEmpty(_tb) ? null : WOUM.ExConverter.FullName2Type(_tb));
      }
    }

    private bool exec {
      get {
        return _parent != null && _parent.exec;
      }
    }

    private void _owner_changed(Topic sender, TopicChanged param) {
      DVar<Topic> p = param.Source as DVar<Topic>;
      if (p != null && p.parent == _owner && p.value != null) {
        if (p.name == "A") {
          Change_A(p.value);
        } else if (p.name == "B") {
          Change_B(p.value);
        }
      } else if (sender.name == "direction" && sender.valueType == typeof(long)) {
        _dir = (byte)((sender as DVar<long>).value);
      }
      if (exec && _a != null && _b != null) {
        Topic a = _a.valueType == typeof(Topic) ? (_a as DVar<Topic>).value : _a;
        Topic b = _b.valueType == typeof(Topic) ? (_b as DVar<Topic>).value : _b;
        if (a != null && b != null) {
          if (Direction == 0 || Direction == 1) {
            b.SetValue(a.GetValue(), new TopicChanged(TopicChanged.ChangeArt.Value, _owner));
          } else {
            a.SetValue(b.GetValue(), new TopicChanged(TopicChanged.ChangeArt.Value, _owner));
          }
        }
      }
    }

    private void Change_A(Topic p) {
      if (exec && _a != null) {
        _a.changed -= _a_changed;
        Topic a;
        if (_a.valueType == typeof(Topic) && (a=(_a as DVar<Topic>).value)!=null) {
          a.changed -= _a_changed;
        }
      }
      _a = p;
      if (exec && _a != null) {
        _a.changed += _a_changed;
        Topic a;
        if (_a.valueType == typeof(Topic) && (a = (_a as DVar<Topic>).value) != null) {
          a.changed += _a_changed;
        }
      }
    }

    private void Change_B(Topic p) {
      if (exec && _b != null) {
        _b.changed -= _b_changed;
        Topic b;
        if (_b.valueType == typeof(Topic) && (b = (_b as DVar<Topic>).value) != null) {
          b.changed -= _b_changed;
        }
      }
      _b = p;
      if (exec && _b != null) {
        _b.changed += _b_changed;
        Topic b;
        if (_b.valueType == typeof(Topic) && (b = (_b as DVar<Topic>).value) != null) {
          b.changed += _b_changed;
        }
      }
    }

    private void _a_changed(Topic sender, TopicChanged param) {
      if (exec) {
        if (param.Art == TopicChanged.ChangeArt.Value && _b != null && !param.Visited(_b, true) && (Direction == 0 || Direction == 1)) {
          Topic a = _a.valueType == typeof(Topic) ? (_a as DVar<Topic>).value : _a;
          Topic b = _b.valueType == typeof(Topic) ? (_b as DVar<Topic>).value : _b;
          if (a != null && b != null) {
            b.SetValue(a.GetValue(), new TopicChanged(param));
          }
        } else if (_a.disposed) {
          DVar<PiWire> o = System.Threading.Interlocked.Exchange(ref _owner, null);
          if (o != null) {
            Change_A(null);
            Change_B(null);
            o.Remove();
          }
        }
      }
    }
    private void _b_changed(Topic sender, TopicChanged param) {
      if (exec) {
        if (param.Art == TopicChanged.ChangeArt.Value && _a != null && !param.Visited(_a, true) && (Direction == 0 || Direction == 2)) {
          Topic a = _a.valueType == typeof(Topic) ? (_a as DVar<Topic>).value : _a;
          Topic b = _b.valueType == typeof(Topic) ? (_b as DVar<Topic>).value : _b;
          if (a != null && b != null) {
            a.SetValue(b.GetValue(), new TopicChanged(param));
          }
        } else if (_b.disposed) {
          DVar<PiWire> o = System.Threading.Interlocked.Exchange(ref _owner, null);
          if (o != null) {
            Change_A(null);
            Change_B(null);
            o.Remove();
          }
        }
      }
    }

    public override string ToString() {
      StringBuilder sb = new StringBuilder();
      if (this.A == null) {
        sb.Append("NC - ");
      } else {
        if (this.A.parent != null) {
          sb.AppendFormat("{0}.", this.A.parent.name);
        }
        sb.AppendFormat("{0} - ", this.A.name);
      }
      if (this.B == null) {
        sb.Append("NC");
      } else {
        if (this.B.parent != null) {
          sb.AppendFormat("{0}.", this.B.parent.name);
        }
        sb.AppendFormat("{0}", this.B.name);
      }
      return sb.ToString();
    }
  }
}
