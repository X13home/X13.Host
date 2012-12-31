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
using X13.MQTT;

namespace X13.PLC {
  [Newtonsoft.Json.JsonObject(Newtonsoft.Json.MemberSerialization.OptIn)]
  public class PiWire : ITopicOwned {
    static PiWire() {
      if(Topic.brokerMode) {
        var t1=Topic.root.Get<string>("/system/declarers/Wire");
        t1.value="/CC;component/Images/ty_wire.png";
        t1.Get<string>("remove").value="1D";
      }
    }

    [Newtonsoft.Json.JsonProperty]
    private uint _dummy;
    private DVar<PiWire> _owner;
    private Topic _a;
    private Topic _b;
    private DVar<Topic> _aAlias;
    private DVar<Topic> _bAlias;
    private DVar<byte> _direction;

    public PiWire() {
      _dummy=1000;
    }

    public Topic A {
      get { return _aAlias??_a; }
      set {
        if(_owner!=null) {
          var z=_owner.Get<Topic>("A");
          z.saved=true;
          z.value=value;
        }
      }
    }
    public Topic B {
      get { return _bAlias??_b; }
      set {
        if(_owner!=null) {
          var z=_owner.Get<Topic>("B");
          z.saved=true;
          z.value=value;
        }
      }
    }
    /// <summary>0 - A<=>B, 1 - A=>B, 2 - B=>A </summary>
    public byte Direction {
      get {
        if(_direction==null) {
          if(_owner==null) {
            return 0;
          }
          _direction=_owner.Get<byte>("direction");
        }
        return _direction.value;
      }
      set {
        if(_direction==null) {
          if(_owner==null) {
            return;
          }
          _direction=_owner.Get<byte>("direction");
          _direction.saved=true;
        }
        _direction.value=value;
      }
    }

    #region ITopicOwned Members
    void ITopicOwned.SetOwner(Topic owner) {
      if(_owner!=owner) {
        if(_owner!=null) {
          _owner.Unsubscribe("+", _owner_changed);
          _dummy--;
          if(_aAlias!=null) {
            _aAlias.changed-=_aAlias_changed;
          }
          if(_bAlias!=null) {
            _bAlias.changed-=_bAlias_changed;
          }
          if(_a!=null) {
            _a.changed-=_a_changed;
          }
          if(_b!=null) {
            _b.changed-=_b_changed;
          }
        }
        _owner=owner as DVar<PiWire>;
        if(_owner!=null) {
          _dummy++;
          _owner.Subscribe("+", _owner_changed);
          _owner.saved=true;
          var dc=_owner.Get<string>("_declarer");
          dc.saved=true;
          dc.value="Wire";

          foreach(Topic tp in _owner.children.Where(t => t.valueType==typeof(Topic))) {
            var p=tp as DVar<Topic>;
            if(p.value!=null) {
              ChangedPin(p);
            }
          }
        }
      }
    }
    #endregion ITopicOwned Members

    private void _owner_changed(Topic sender, TopicChanged param) {
      DVar<Topic> p=param.Source as DVar<Topic>;
      if(p!=null && p.parent==_owner && (p.name=="A" || p.name=="B")) {
        if(param.Art==TopicChanged.ChangeArt.Value) {
          ChangedPin(p);
        } else if(Topic.brokerMode && param.Art==TopicChanged.ChangeArt.Remove) {          // Remove
          if(_a!=null) {
            _a.changed-=_a_changed;
          }
          if(_b!=null) {
            _b.changed-=_b_changed;
          }
          if(_owner!=null) {
            _owner.Remove();
          }
        }
      }
    }

    private void ChangedPin(DVar<Topic> p) {
      if(p.value==null || p.value.valueType!=typeof(Topic)) {
        if(p.name=="A" && p.value!=_a) {
          Change_A(p);
        } else if(p.name=="B" && p.value!=_b) {
          Change_B(p);
        }
      }
      if(p.value==null || p.value.valueType==typeof(Topic)) {
        if(p.name=="A" && p.value!=_aAlias) {
          if(Topic.brokerMode && _aAlias!=null) {
            _aAlias.changed-=_aAlias_changed;
          }
          _aAlias=p.value as DVar<Topic>;
          if(Topic.brokerMode && _aAlias!=null) {
            _aAlias.changed+=_aAlias_changed;
          }
          Change_A(_aAlias);
        } else if(p.name=="B" && p.value!=_bAlias) {
          if(Topic.brokerMode && _bAlias!=null) {
            _bAlias.changed-=_bAlias_changed;
          }
          _bAlias=p.value as DVar<Topic>;
          if(Topic.brokerMode && _bAlias!=null) {
            _bAlias.changed+=_bAlias_changed;
          }
          Change_B(_bAlias);
        }

      }
      if(Topic.brokerMode && _a!=null && _b!=null) {
        if(_b.saved && !_a.saved) {
          _b.Publish(_b, TopicChanged.ChangeArt.Value);
        } else {
          _a.Publish(_a, TopicChanged.ChangeArt.Value);
        }
      }
    }

    private void Change_A(DVar<Topic> p) {
      if(Topic.brokerMode && _a!=null) {
        _a.changed-=_a_changed;
      }
      _a=p.value;
      if(Topic.brokerMode && _a!=null) {
        _a.changed+=_a_changed;
      }
    }

    private void Change_B(DVar<Topic> p) {
      if(Topic.brokerMode && _b!=null) {
        _b.changed-=_b_changed;
      }
      _b=p.value;
      if(Topic.brokerMode && _b!=null) {
        _b.changed+=_b_changed;
      }
    }

    private void _aAlias_changed(Topic sender, TopicChanged param) {
      if(param.Art==TopicChanged.ChangeArt.Value && param.Source==_aAlias) {
        Change_A(_aAlias);
      }
    }

    private void _bAlias_changed(Topic sender, TopicChanged param) {
      if(param.Art==TopicChanged.ChangeArt.Value && param.Source==_bAlias) {
        Change_B(_bAlias);
      }
    }

    private void _a_changed(Topic sender, TopicChanged param) {
      if(param.Art==TopicChanged.ChangeArt.Value && _b!=null && !param.Visited(_b, true)) {
        if(Direction==0 || Direction==1) {
          _b.SetValue(_a.GetValue(), new TopicChanged(param));
        } else {
          _a.SetValue(_b.GetValue(), new TopicChanged(TopicChanged.ChangeArt.Value, _b));
        }
      }
    }
    private void _b_changed(Topic sender, TopicChanged param) {
      if(param.Art==TopicChanged.ChangeArt.Value && _a!=null && !param.Visited(_a, true)) {
        if(Direction==0 || Direction==2) {
          _a.SetValue(_b.GetValue(), new TopicChanged(param));
        } else {
          _b.SetValue(_a.GetValue(), new TopicChanged(TopicChanged.ChangeArt.Value, _a));
        }
      }
    }

    public override string ToString() {
      StringBuilder sb=new StringBuilder();
      if(this.A==null) {
        sb.Append("NC - ");
      } else {
        if(this.A.parent!=null) {
          sb.AppendFormat("{0}.", this.A.parent.name);
        }
        sb.AppendFormat("{0} - ", this.A.name);
      }
      if(this.B==null) {
        sb.Append("NC");
      } else {
        if(this.B.parent!=null) {
          sb.AppendFormat("{0}.", this.B.parent.name);
        }
        sb.AppendFormat("{0}", this.B.name);
      }
      return sb.ToString();
    }
  }
}
