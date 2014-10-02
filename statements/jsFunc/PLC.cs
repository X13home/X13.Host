using Jurassic;
using Jurassic.Library;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace X13.PLC {
  public class PLC {
    public static PLC instance { get; private set; }
    private ScriptEngine Engine;

    public PLC() {
      Engine = new ScriptEngine();
      instance=this;
    }
    public object Parse(Topic dst, string json) {
      object o=JSONObject.Parse(Engine, json);
      ObjectInstance jo=o as ObjectInstance;
      string jo_class;
      if(jo!=null && (jo_class=jo.GetPropertyValue("class") as string)!=null) {
        switch(jo_class) {
        #region declarer
        case "declarer": {
            DeclInstance di=new DeclInstance();
            di._name=dst.name;
            foreach(var pr in jo.Properties) {
              switch(pr.Name) {
              case "Init": {
                  string body=pr.Value as string;
                  if(!string.IsNullOrEmpty(body)) {
                    di.InitFunc=Engine.Function.Construct("path", body);
                  }
                }
                break;
              case "Calc": {
                  string body=pr.Value as string;
                  if(!string.IsNullOrEmpty(body)) {
                    di.CalcFunc=Engine.Function.Construct("sender", body);
                  }
                }
                break;
              case "Deinit": {
                  string body=pr.Value as string;
                  if(!string.IsNullOrEmpty(body)) {
                    di.DeinitFunc=Engine.Function.Construct(body);
                  }
                }
                break;
              case "class":
                break;
              default: {
                  ObjectInstance p=pr.Value as ObjectInstance;
                  if(p!=null && p.HasProperty("type")) {
                    var pin =new DeclInstance.PinDecl();
                    pin.name=pr.Name;
                    if(p!=null) {
                      foreach(var pi in p.Properties) {
                        switch(pi.Name) {
                        case "pos":
                          pin.pos=Convert.ToInt32(pi.Value);
                          break;
                        case "type":
                          pin.flags=(DeclInstance.Flags)Convert.ToInt32(pi.Value);
                          break;
                        }
                      }
                    }
                    di._pins.Add(pin);
                  }
                }
                break;
              }
            }
            DeclInstance.funcs[di._name]=di;
            return di;
          }
        #endregion declarer
        #region function
        case "function": {
            string jo_decl;
            if((jo_decl=jo.GetPropertyValue("declarer") as string)!=null) {
              DeclInstance decl;
              if(DeclInstance.funcs.TryGetValue(jo_decl, out decl) && decl!=null){
                return new FuncInst(Engine, decl);
              } else {
                Log.Warning("{0}[{1}] - unknown declarer", dst.path, jo_decl);
              }
            }
          }
          break;
        #endregion function
        }
      }
      return o;
    }
    public string ToJson(Topic t) {
      IToJson tj;
      if((tj=t.value as IToJson)!=null) {
        return tj.ToJson();
      }
      return JSONObject.Stringify(Engine, t.value);
    }
    public void Test() {
      string add_json="{"
        +"\"class\":\"declarer\","
        +"\"Calc\":\"var r=this.A+this.B; if(this.C!=null){r+=this.C; } this.Q=r; \","
        +"\"A\":{ \"type\":1, \"pos\":1},"
        +"\"B\":{ \"type\":1, \"pos\":2},"
        +"\"C\":{ \"type\":5, \"pos\":3},"
        +"\"Q\":{ \"type\":2, \"pos\":1}"
        +"}";
      Topic.root.Get("/var/test").SetJson("1");
      Topic.root.Get("/etc/declarer/func/Add").SetJson(add_json);
      var test=Topic.root.Get("/plc/test/A01");
      string a01_json="{"
        +"\"class\":\"function\","
        +"\"declarer\":\"Add\""
        +"}";
      test.SetJson(a01_json);

      Topic.Process();
      test.Get("A").value=1;
      test.Get("B").value=3;
      Topic.Process();
      test.Get("C").value=9;
      Topic.Process();

      string json=Topic.root.Get("/etc/declarer/func/Add").GetJson();
      Log.Debug("{0}", json);
      Topic.root.Get("/etc/declarer/func/Add1").SetJson(json);
      json=test.GetJson();
      Log.Debug("{0}", json);
      var a02=Topic.root.Get("/plc/test/A02");
      a02.SetJson(json);

      Topic.Process();
      a02.Get("A").value=4;
      a02.Get("B").value=12;
      Topic.Process();
      a02.Get("C").value=26;
      Topic.Process();

    }
  }
  public class DeclInstance : IToJson {
    public static readonly Dictionary<string, DeclInstance> funcs=new Dictionary<string, DeclInstance>();
    public List<PinDecl> _pins;
    public string _name;

    public DeclInstance() {
      _pins=new List<PinDecl>();
    }

    public FunctionInstance InitFunc { get; set; }
    public FunctionInstance CalcFunc { get; set; }
    public FunctionInstance DeinitFunc { get; set; }
    [Flags]
    public enum Flags {
      input=1,
      output=2,
      optional=4,
    }
    public struct PinDecl {
      public string name;
      public Flags flags;
      public int pos;
    }
    public string ToJson() {
      StringBuilder sb=new StringBuilder();
      sb.Append("{");
      sb.Append("\"class\":\"declarer\"");
      if(InitFunc!=null) {
        sb.Append(",\"Init\":\""+(InitFunc as UserDefinedFunction).BodyText+"\"");
      }
      if(CalcFunc!=null) {
        sb.Append(",\"Calc\":\""+(CalcFunc as UserDefinedFunction).BodyText+"\"");
      }
      if(DeinitFunc!=null) {
        sb.Append(",\"Deinit\":\""+(DeinitFunc as UserDefinedFunction).BodyText+"\"");
      }
      for(int i=0; i<_pins.Count; i++) {
        sb.Append(",\""+_pins[i].name+"\":{");
        sb.Append("\"type\":"+((int)_pins[i].flags).ToString());
        if(_pins[i].pos>0) {
          sb.Append(",\"pos\":"+_pins[i].pos.ToString());
        }
        sb.Append("}");
      }
      sb.Append("}");
      return sb.ToString();
    }
  }

  public class FuncInst : ObjectInstance, ITenant, IToJson {
    private Topic _owner;
    private DeclInstance _decl;

    public FuncInst(ScriptEngine engine, DeclInstance decl)
      : base(engine) {
      _decl=decl;
      base.DefineProperty("class", new PropertyDescriptor("function", PropertyAttributes.Enumerable), true);
      this.PopulateFunctions();
    }
    [JSProperty(Name = "declarer")]
    public string declarer { get { return _decl._name; } }
    public Topic owner { get { return _owner; } set { SetOwner(value); } }
    private void SetOwner(Topic owner) {
      if(_owner!=owner) {
        if(_owner!=null) {
          //TODO: something
        }
        _owner=owner;
        if(_owner!=null) {
          for(int i=_decl._pins.Count-1; i>=0; i--) {
            var ts=new TopicSetter(Engine, this, _decl._pins[i]);
            if((_decl._pins[i].flags & DeclInstance.Flags.input)==DeclInstance.Flags.input) {
              base.DefineProperty(_decl._pins[i].name, new PropertyDescriptor(ts, null,
                PropertyAttributes.IsAccessorProperty | PropertyAttributes.NonEnumerable), true);
            } else {
              base.DefineProperty(_decl._pins[i].name, new PropertyDescriptor(ts, ts,
                PropertyAttributes.IsAccessorProperty | PropertyAttributes.NonEnumerable | PropertyAttributes.Writable), true);
            }
          }
          if(_decl.InitFunc!=null) {
            _decl.InitFunc.Call(this, owner.path);
          }
          _owner.children.changed+=children_changed;
          if(_decl.CalcFunc!=null) {
            _decl.CalcFunc.Call(this, _owner.name);
          }
        }
      }
    }
    private void children_changed(Topic t, Topic.TopicArgs a) {
      if(_decl.CalcFunc!=null) {
        _decl.CalcFunc.Call(this, t.name);
      }
    }
    private class TopicSetter : FunctionInstance {
      private   FuncInst _owner;
      private   DeclInstance.PinDecl _decl;
      private   Topic _ref;

      public TopicSetter(ScriptEngine engine, FuncInst funcInst, DeclInstance.PinDecl pinDecl)
        : base(engine) {
        _owner = funcInst;
        _decl = pinDecl;

        if((_decl.flags & DeclInstance.Flags.optional)!=DeclInstance.Flags.optional) {
          _ref=_owner._owner.Get(_decl.name);
        } else {
          _owner._owner.Exist(_decl.name, out _ref);
        }

      }
      public override object CallLateBound(object thisObject, params object[] argumentValues) {
        if(argumentValues.Length==1) {
          if(_ref==null) {
            _ref=_owner._owner.Get(_decl.name);
          }
          _ref.value=argumentValues[0];
          Log.Debug("{0}={1}", _ref.path, argumentValues[0]);
        } else if(argumentValues.Length==0) {
          if(_ref!=null || _owner._owner.Exist(_decl.name, out _ref)) {
            return _ref.value;
          }
        }
        return Undefined.Value;
      }
    }

    public string ToJson() {
      return string.Concat("{\"class\":\"function\", \"declarer\":\"", _decl._name, "\"}");
    }
  }
}
