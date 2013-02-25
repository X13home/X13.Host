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
using System.Reflection;
using X13.WOUM;

namespace X13.PLC {
  /// <summary>Generic class of data storage in Message Queue Telemetry Library</summary>
  /// <typeparam name="T">type of stored data</typeparam>
  public class DVar<T> : Topic {
    public static implicit operator T(DVar<T> it) {
      return it._value;
    }

    private T _value;

    public DVar()
      : base(typeof(T)) {
      if(valueType==null || valueType.IsGenericType) {
        throw new ArgumentException();
      }
      var tc=Type.GetTypeCode(valueType);
      _tcObject=tc==TypeCode.Object;
    }

    [Category("Content"), DisplayName("Value"), Browsable(true), ReadOnly(false)]
    public virtual T value {
      get {
        return _value;
      }
      set {
        SetValue(value, new TopicChanged(TopicChanged.ChangeArt.Value));
      }
    }

    public override object GetValue() {
      return value;
    }
    public override void SetValue(object value, TopicChanged param) {
      try {
        if(valueType==typeof(object)) {
          _tcObject=(value==null || Type.GetTypeCode(value.GetType())==TypeCode.Object);
        }
        T tmp=(T)(!_tcObject?Convert.ChangeType(value, this.valueType):value);
        SetValue(tmp, param);
      }
      catch(FormatException ex) {
        Log.Warning("{0}.SetValue({1}, ) - {2}", this.path, value, ex.Message);
      }
      catch(OverflowException ex) {
        Log.Warning("{0}.SetValue({1}, ) - {2}", this.path, value, ex.Message);
      }
      catch(InvalidCastException ex) {
        Log.Warning("{0}.SetValue({1}, ) - {2}", this.path, value, ex.Message);
      }
    }
    private void SetValue(T value, TopicChanged param) {
      if(_disposed>0) {
        Log.Warning("Object dispossed: {0}", this.path);
        return;
      }
      if(!object.Equals(_value, value)) {
        //if(param.Source==null) {
        //  param=new TopicChanged(TopicChanged.ChangeArt.Value);
        //}
        param.Source=this;
        if(_tcObject) {
          if(_tcObject && (_value as ITopicOwned)!=null) {
            (_value as ITopicOwned).SetOwner(null);
          }
          if(valueType==typeof(Topic) && _value!=null) {
            (_value as Topic).Unsubscribe("", DVar_changed);
          }
        }
        _value=value;
        base._json=null;
        if(_tcObject) {
          if((_value as ITopicOwned)!=null) {
            (_value as ITopicOwned).SetOwner(this);
          }
          if(valueType==typeof(Topic) && _value!=null) {
            (_value as Topic).Subscribe("", DVar_changed);
          }
        }
        Publish(this, param);
      }
    }
    protected override void onChange(Topic sender, TopicChanged param) {
      if(valueType==typeof(Topic) && sender.Equals(_value)) {
        if(param.Art==TopicChanged.ChangeArt.Add) {
          base._json=null;
          Publish(this, new TopicChanged(param) { Art=TopicChanged.ChangeArt.Value, Source=this });
        } else if(param.Art==TopicChanged.ChangeArt.Remove) {
          this.Remove();
        }
      } else {
        base.onChange(sender, param);
      }
    }
  }
}
