#region license
//Copyright (c) 2014 Wassili Hense

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

namespace X13.PLC.jsFunc {
  [Export(typeof(IStatement))]
  [ExportMetadata("declarer", "jsFunc")]
  public class JsFunc : IStatement {
    private static Lazy<ScriptEngine> _engine;

    static JsFunc() {
      _engine=new Lazy<ScriptEngine>(() => new ScriptEngine());
    }

    private DVar<string> _dCode;
    private FunctionInstance _func;
    private TInst _this;
    public void Load() {
      using(var sr=new StreamReader(Assembly.GetExecutingAssembly().GetManifestResourceStream("jsFunc.jsFunc.xst"))) {
        Topic.Import(sr, null);
      }
      //_engine.Value.ForceStrictMode=true;
    }

    public void Init(DVar<PiStatement> model) {
      _dCode=BiultInStatements.AddPin<string>(model, "_code");
      if(!model.Exist("A")) {
        BiultInStatements.AddPin<double>(model, "A");
      }
      if(!model.Exist("Q")) {
        BiultInStatements.AddPin<double>(model, "Q");
      }
      _this=new TInst(_engine.Value, model);
      Compile(model);
    }

    public void Calculate(DVar<PiStatement> model, Topic source) {
      if(source==_dCode) {
        Compile(model);
      }
      if(_func!=null) {
        object r=_func.Call(_this, source.name);
        Topic Q;
        if(r!=null && model.Exist("Q", out Q)) {
          Q.SetValue(r, new TopicChanged(TopicChanged.ChangeArt.Value, model));
        }
      }
    }
    public void DeInit() {
    }

    private void Compile(DVar<PiStatement> model) {
      if(string.IsNullOrWhiteSpace(_dCode.value)) {
        _func=null;
      } else {
        try {
          _func=_engine.Value.Function.Construct("sender", _dCode.value);
        }
        catch(Exception ex) {
          Log.Warning("{0} - {1}", model.path, ex.Message);
          _func=null;
        }
      }
    }

    private class TInst: ObjectInstance {
      public readonly DVar<PiStatement> _owner;

      public TInst(ScriptEngine engine, DVar<PiStatement> owner) :base(engine) {
        _owner = owner;
      }
      protected override bool AddProperty(string propertyName, object value, Jurassic.Library.PropertyAttributes attributes, bool throwOnError) {
        if(propertyName.Length==1 && ((propertyName[0]>='A' && propertyName[0]<='H') || (propertyName[0]>='Q' && propertyName[0]<='T'))) {
          Topic p;
          if(_owner!=null && _owner.Exist(propertyName, out p)) {
            var ts=TopicSetter.Get(Engine, propertyName);
            var d=new PropertyDescriptor(ts, ts.ro?null:ts, Jurassic.Library.PropertyAttributes.IsAccessorProperty);
            p.SetValue(value, new TopicChanged(TopicChanged.ChangeArt.Value, _owner));
            return base.AddProperty(propertyName, d.Value, d.Attributes, throwOnError);
          } else {
            return false;
          }
        }
        return base.AddProperty(propertyName, value, attributes, throwOnError);
      }
      protected override object GetMissingPropertyValue(string propertyName) {
        if(propertyName.Length==1 && ((propertyName[0]>='A' && propertyName[0]<='H') || (propertyName[0]>='Q' && propertyName[0]<='T'))) {
          Topic p;
          if(_owner!=null && _owner.Exist(propertyName, out p)) {
            var ts=TopicSetter.Get(Engine, propertyName);
            base.DefineProperty(propertyName, new PropertyDescriptor(ts, ts.ro?null:ts, Jurassic.Library.PropertyAttributes.IsAccessorProperty), true);
            return p.GetValue();
          }
        }
        return base.GetMissingPropertyValue(propertyName);
      }
      private void tmp() {
        //base.DefineProperty(_decl._pins[i].name, new PropertyDescriptor(ts, ts, Jurassic.Library.PropertyAttributes.IsAccessorProperty | Jurassic.Library.PropertyAttributes.NonEnumerable | Jurassic.Library.PropertyAttributes.Writable), true);
        
      }
    }
    private class TopicSetter: FunctionInstance {
      private static SortedList<string, TopicSetter> _accs;
      static TopicSetter() {
        _accs=new SortedList<string, TopicSetter>(16);
      }
      public static TopicSetter Get(ScriptEngine engine, string name) {
        TopicSetter r;
        if(_accs.TryGetValue(name, out r)) {
          return r;
        }
        r=new TopicSetter(engine, name);
        _accs[name]=r;
        return r;
      }

      private string _name;
      public readonly bool ro;

      public TopicSetter(ScriptEngine engine, string name)
        : base(engine) {
        _name=name;
        ro=name[0]>='A' && name[0]<='H';
      }
      public override object CallLateBound(object thisObject, params object[] argumentValues) {
        var t=thisObject as TInst;
        object v;
        Topic o, r;
        if(t!=null && (o=t._owner)!=null) {
          if(!ro && argumentValues.Length==1) {
            v=argumentValues[0];
            if(v is ConcatenatedString) {
              v=v.ToString();
            }
            r=o.Get(_name);
            r.SetValue(v, new TopicChanged(TopicChanged.ChangeArt.Value, o));
          } else if(argumentValues.Length==0) {
            if(o.Exist(_name, out r)) {
              return r.GetValue();
            }
          }
        }
        return Undefined.Value;
      }
    }

  }
}
