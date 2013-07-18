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
using System.Threading;
using System.Xml.Linq;
using System.Globalization;
using System.IO;
using System.Net;
using SoftCircuits;
using System.ComponentModel.Composition;

namespace X13.PLC {
  public class BiultInStatements {
    public static DVar<T> AddPin<T>(DVar<PiStatement> model, string name) {
      DVar<T> pin=model.Get<T>(name);
      pin.saved=true;
      pin.Publish(pin, TopicChanged.ChangeArt.Value, null);
      return pin;
    }

    [Export(typeof(IStatement))]
    [ExportMetadata("declarer", "NOT")]
    private class bNot : IStatement {
      public void Load() {
        var m=Topic.root.Get<string>("/etc/declarers/func/NOT");
        m.value="pack://application:,,/CC;component/Images/bi_not.png";
        m.Get<string>("_description").value="a Logical negation";
        m.Get<string>("A").value="Az";
        m.Get<string>("Q").value="az";
        m.Get<string>("rename").value="|R";
        m.Get<string>("remove").value="}D";
      }

      public void Init(DVar<PiStatement> model) {
        AddPin<bool>(model, "A");
        AddPin<bool>(model, "Q");
      }

      public void Calculate(DVar<PiStatement> model, Topic source) {
        DVar<bool> op=model.Get<bool>("Q");
        op.saved=true;
        op.value=!model.Get<bool>("A").value;
      }

      public void DeInit() {
      }
    }

    [Export(typeof(IStatement))]
    [ExportMetadata("declarer", "DTriger")]
    private class DTriger : IStatement {
      public void Load() {
        var m=Topic.root.Get<string>("/etc/declarers/func/DTriger");
        m.value="pack://application:,,/CC;component/Images/bi_triger.png";
        m.Get<string>("_description").value="b RS-DC Triger";
        m.Get<string>("S").value="Az";
        m.Get<string>("D").value="Bz";
        m.Get<string>("C").value="Cz";
        m.Get<string>("R").value="Dz";
        m.Get<string>("Q").value="az";
        m.Get<string>("!Q").value="bz";
        m.Get<string>("rename").value="|R";
        m.Get<string>("remove").value="}D";
      }

      public void Init(DVar<PiStatement> model) {
        AddPin<bool>(model, "S");
        AddPin<bool>(model, "C");
        AddPin<bool>(model, "D");
        AddPin<bool>(model, "R");
        AddPin<bool>(model, "Q");
        AddPin<bool>(model, "!Q");
        model.Get<bool>("!Q").value=!model.Get<bool>("Q").value;
      }

      public void Calculate(DVar<PiStatement> model, Topic source) {
        bool ret=model.Get<bool>("Q");
        if(model.Get<bool>("R")) {
          ret=false;
        } else if(model.Get<bool>("S")) {
          ret=true;
        } else if(source.name=="C" && model.Get<bool>("C")) {
          ret=model.Get<bool>("D");
        } else {
          return;
        }

        DVar<bool> op=model.Get<bool>("Q");
        op.saved=true;
        op.value=ret;
        op=model.Get<bool>("!Q");
        op.saved=true;
        op.value=!ret;
      }

      public void DeInit() {
      }
    }

    [Export(typeof(IStatement))]
    [ExportMetadata("declarer", "ANDI")]
    private class AndI : IStatement {
      public void Load() {
        var m=Topic.root.Get<string>("/etc/declarers/func/ANDI");
        m.value="pack://application:,,/CC;component/Images/bi_and.png";
        m.Get<string>("_description").value="c1Bitwise AND";
        m.Get<string>("A").value="Ai";
        m.Get<string>("B").value="Bi";
        m.Get<string>("C").value="Ci";
        m.Get<string>("D").value="Di";
        m.Get<string>("E").value="Ei";
        m.Get<string>("F").value="Fi";
        m.Get<string>("G").value="Gi";
        m.Get<string>("H").value="Hi";
        m.Get<string>("Q").value="ai";
        m.Get<string>("rename").value="|R";
        m.Get<string>("remove").value="}D";
      }

      public void Init(DVar<PiStatement> model) {
        AddPin<long>(model, "A");
        AddPin<long>(model, "B");
        AddPin<long>(model, "Q");
      }

      public void Calculate(DVar<PiStatement> model, Topic source) {
        long ret=-1;  // 0xFFFFFFFF
        foreach(DVar<long> pin in model.children.Where(z => (z.name.Length==1 && z.valueType==typeof(long) && z.name[0]>='A' && z.name[0]<='H')).Cast<DVar<long>>()) {
          ret&=pin.value;
        }
        model.Get<long>("Q").value=ret;
      }

      public void DeInit() {
      }
    }

    [Export(typeof(IStatement))]
    [ExportMetadata("declarer", "ORI")]
    private class OrI : IStatement {
      public void Load() {
        var m=Topic.root.Get<string>("/etc/declarers/func/ORI");
        m.value="pack://application:,,/CC;component/Images/bi_or.png";
        m.Get<string>("_description").value="c2Bitwise OR";
        m.Get<string>("A").value="Ai";
        m.Get<string>("B").value="Bi";
        m.Get<string>("C").value="Ci";
        m.Get<string>("D").value="Di";
        m.Get<string>("E").value="Ei";
        m.Get<string>("F").value="Fi";
        m.Get<string>("G").value="Gi";
        m.Get<string>("H").value="Hi";
        m.Get<string>("Q").value="ai";
        m.Get<string>("rename").value="|R";
        m.Get<string>("remove").value="}D";
      }

      public void Init(DVar<PiStatement> model) {
        AddPin<long>(model, "A");
        AddPin<long>(model, "B");
        AddPin<long>(model, "Q");
      }

      public void Calculate(DVar<PiStatement> model, Topic source) {
        long ret=0;
        foreach(DVar<long> pin in model.children.Where(z => (z.name.Length==1 && z.valueType==typeof(long) && z.name[0]>='A' && z.name[0]<='H')).Cast<DVar<long>>()) {
          ret|=pin;
        }
        model.Get<long>("Q").value=ret;
      }

      public void DeInit() {
      }
    }

    [Export(typeof(IStatement))]
    [ExportMetadata("declarer", "XORI")]
    private class XorI : IStatement {
      public void Load() {
        var m=Topic.root.Get<string>("/etc/declarers/func/XORI");
        m.value="pack://application:,,/CC;component/Images/bi_xor.png";
        m.Get<string>("_description").value="c3Bitwise XOR";
        m.Get<string>("A").value="Ai";
        m.Get<string>("B").value="Bi";
        m.Get<string>("C").value="Ci";
        m.Get<string>("D").value="Di";
        m.Get<string>("E").value="Ei";
        m.Get<string>("F").value="Fi";
        m.Get<string>("G").value="Gi";
        m.Get<string>("H").value="Hi";
        m.Get<string>("Q").value="ai";
        m.Get<string>("rename").value="|R";
        m.Get<string>("remove").value="}D";
      }

      public void Init(DVar<PiStatement> model) {
        AddPin<long>(model, "A");
        AddPin<long>(model, "B");
        AddPin<long>(model, "Q");
      }

      public void Calculate(DVar<PiStatement> model, Topic source) {
        long ret=0;
        foreach(DVar<long> pin in model.children.Where(z => (z.name.Length==1 && z.valueType==typeof(long) && z.name[0]>='A' && z.name[0]<='H')).Cast<DVar<long>>()) {
          ret^=pin;
        }
        model.Get<long>("Q").value=ret;
      }

      public void DeInit() {
      }
    }

    [Export(typeof(IStatement))]
    [ExportMetadata("declarer", "SHL")]
    private class Shl : IStatement {
      public void Load() {
        var m=Topic.root.Get<string>("/etc/declarers/func/SHL");
        m.value="pack://application:,,/CC;component/Images/bi_shl.png";
        m.Get<string>("_description").value="c4Bitwise left shift";
        m.Get<string>("A").value="Ai";
        m.Get<string>("B").value="Bi";
        m.Get<string>("Q").value="ai";
        m.Get<string>("rename").value="|R";
        m.Get<string>("remove").value="}D";
      }

      public void Init(DVar<PiStatement> model) {
        AddPin<long>(model, "A");
        AddPin<long>(model, "B");
        AddPin<long>(model, "Q");
      }

      public void Calculate(DVar<PiStatement> model, Topic source) {
        model.Get<long>("Q").value=model.Get<long>("A").value<<(int)model.Get<long>("B").value;
      }

      public void DeInit() {
      }
    }

    [Export(typeof(IStatement))]
    [ExportMetadata("declarer", "SHR")]
    private class Shr : IStatement {
      public void Load() {
        var m=Topic.root.Get<string>("/etc/declarers/func/SHR");
        m.value="pack://application:,,/CC;component/Images/bi_shr.png";
        m.Get<string>("_description").value="c5Bitwise right shift";
        m.Get<string>("A").value="Ai";
        m.Get<string>("B").value="Bi";
        m.Get<string>("Q").value="ai";
        m.Get<string>("rename").value="|R";
        m.Get<string>("remove").value="}D";
      }
      public void Init(DVar<PiStatement> model) {
        AddPin<long>(model, "A");
        AddPin<long>(model, "B");
        AddPin<long>(model, "Q");
      }

      public void Calculate(DVar<PiStatement> model, Topic source) {
        model.Get<long>("Q").value=model.Get<long>("A").value>>(int)model.Get<long>("B").value;
      }

      public void DeInit() {
      }
    }

    [Export(typeof(IStatement))]
    [ExportMetadata("declarer", "Counter")]
    private class Counter : IStatement {
      private DVar<bool> _inc, _set, _reset, _dec;
      private DVar<long> _val, _out;

      public void Load() {
        var m=Topic.root.Get<string>("/etc/declarers/func/Counter");
        m.value="pack://application:,,/CC;component/Images/bi_counter.png";
        m.Get<string>("_description").value="d Counter";
        m.Get<string>("+1").value="Az";
        m.Get<string>("Set").value="Bz";
        m.Get<string>("Value").value="Ci";
        m.Get<string>("Reset").value="Dz";
        m.Get<string>("-1").value="Ez";
        m.Get<string>("Out").value="ci";
        m.Get<string>("rename").value="|R";
        m.Get<string>("remove").value="}D";
      }

      public void Init(DVar<PiStatement> model) {
        _inc=AddPin<bool>(model, "+1");
        _set=AddPin<bool>(model, "Set");
        _val=AddPin<long>(model, "Value");
        _reset=AddPin<bool>(model, "Reset");
        _dec=AddPin<bool>(model, "-1");
        _out=AddPin<long>(model, "Out");
      }
      public void Calculate(DVar<PiStatement> model, Topic source) {
        _out.saved=true;
        if(source==_reset && _reset.value) {
          _out.value=0;
        } else if((source==_set || source==_val) && _set.value) {
          _out.value=_val.value;
        } else if(source==_inc && _inc.value) {
          _out.value++;
        } else if(source==_dec && _dec.value) {
          _out.value--;
        }
      }
      public void DeInit() {
      }
    }

    [Export(typeof(IStatement))]
    [ExportMetadata("declarer", "comp_eq")]
    private class ar_comp_eq : IStatement {
      private DVar<double> _a;
      private DVar<double> _b;

      public void Load() {
        var t1=Topic.root.Get<string>("/etc/declarers/func/comp_eq");
        t1.value="pack://application:,,/CC;component/Images/ar_eq.png";
        t1.Get<string>("A").value="Ag";
        t1.Get<string>("B").value="Bb";
        t1.Get<string>("Q").value="az";
        t1.Get<string>("!Q").value="bz";

        t1.Get<string>("_description").value="f Equal";

        t1.Get<string>("rename").value="|R";
        t1.Get<string>("remove").value="}D";
      }
      public void Init(DVar<PiStatement> model) {
        _a=AddPin<double>(model, "A");
        _b=AddPin<double>(model, "B");
        AddPin<bool>(model, "Q");
        AddPin<bool>(model, "!Q");
        Calculate(model, _a);
      }
      public void Calculate(DVar<PiStatement> model, Topic source) {
        model.Get<bool>("Q").value=_a.value==_b.value;
        model.Get<bool>("!Q").value=!model.Get<bool>("Q").value;
      }
      public void DeInit() {
      }
    }

    [Export(typeof(IStatement))]
    [ExportMetadata("declarer", "comp_gr")]
    private class ar_comp_gr : IStatement {
      private DVar<double> _a;
      private DVar<double> _b;

      public void Load() {
        var t1=Topic.root.Get<string>("/etc/declarers/func/comp_gr");
        t1.value="pack://application:,,/CC;component/Images/ar_comp_gr.png";
        t1.Get<string>("A").value="Ag";
        t1.Get<string>("B").value="Bb";
        t1.Get<string>("Q").value="az";
        t1.Get<string>("!Q").value="bz";

        t1.Get<string>("_description").value="f Greater";

        t1.Get<string>("rename").value="|R";
        t1.Get<string>("remove").value="}D";
      }
      public void Init(DVar<PiStatement> model) {
        _a=AddPin<double>(model, "A");
        _b=AddPin<double>(model, "B");
        AddPin<bool>(model, "Q");
        AddPin<bool>(model, "!Q");
        Calculate(model, _a);
      }
      public void Calculate(DVar<PiStatement> model, Topic source) {
        model.Get<bool>("Q").value=_a.value>_b.value;
        model.Get<bool>("!Q").value=!model.Get<bool>("Q").value;
      }
      public void DeInit() {
      }
    }

    [Export(typeof(IStatement))]
    [ExportMetadata("declarer", "comp_le")]
    private class ar_comp_le : IStatement {
      private DVar<double> _a;
      private DVar<double> _b;

      public void Load() {
        var t1=Topic.root.Get<string>("/etc/declarers/func/comp_le");
        t1.value="pack://application:,,/CC;component/Images/ar_comp_le.png";
        t1.Get<string>("A").value="Ag";
        t1.Get<string>("B").value="Bb";
        t1.Get<string>("Q").value="az";
        t1.Get<string>("!Q").value="bz";

        t1.Get<string>("_description").value="f Less";

        t1.Get<string>("rename").value="|R";
        t1.Get<string>("remove").value="}D";
      }
      public void Init(DVar<PiStatement> model) {
        _a=AddPin<double>(model, "A");
        _b=AddPin<double>(model, "B");
        AddPin<bool>(model, "Q");
        AddPin<bool>(model, "!Q");
        Calculate(model, _a);
      }
      public void Calculate(DVar<PiStatement> model, Topic source) {
        model.Get<bool>("Q").value=_a.value<_b.value;
        model.Get<bool>("!Q").value=!model.Get<bool>("Q").value;
      }
      public void DeInit() {
      }
    }

    [Export(typeof(IStatement))]
    [ExportMetadata("declarer", "MathExpr")]
    private class MathExpr : IStatement {
      private DVar<PiStatement> _model;
      private Eval eval;
      private DVar<string> _dFunc;
      private DVar<double> _dOut;

      public void Load() {
        var m=Topic.root.Get<string>("/etc/declarers/func/MathExpr");
        m.value="pack://application:,,/CC;component/Images/ty_func.png";
        m.Get<string>("_description").value="f1Math expression";
        m.Get<string>("A").value="Ag";
        m.Get<string>("B").value="Bg";
        m.Get<string>("C").value="Cg";
        m.Get<string>("D").value="Dg";
        m.Get<string>("E").value="Eg";
        m.Get<string>("F").value="Fg";
        m.Get<string>("G").value="Gg";
        m.Get<string>("H").value="Hg";
        m.Get<string>("Out").value="ag";
        m.Get<string>("rename").value="|R";
        m.Get<string>("remove").value="}D";
      }

      public void Init(DVar<PiStatement> model) {
        _model=model;
        _dFunc=AddPin<string>(model, "_func");
        _dOut=AddPin<double>(model, "Out");
        AddPin<double>(model, "A");
        eval = new Eval();
        eval.ProcessSymbol += ProcessSymbol;
        eval.ProcessFunction += ProcessFunction;
      }

      public void Calculate(DVar<PiStatement> model, Topic source) {
        try {
          // Evaluate the current expression
          _dOut.saved=false;
          _dOut.SetValue(eval.Execute(_dFunc.value), new TopicChanged(TopicChanged.ChangeArt.Value, model));

        }
        catch(Exception ex) {
          Log.Warning("{0}.calculate - {1}", model.path, ex.Message);
        }
      }

      public void DeInit() {
      }

      // Implement expression symbols
      private void ProcessSymbol(object sender, SymbolEventArgs e) {
        switch(e.Name.ToLower()) {
        case "pi":
          e.Result = Math.PI;
          break;
        case "a":
        case "b":
        case "c":
        case "d":
        case "e":
        case "f":
        case "g":
        case "h":
          e.Result=_model.Get<double>(e.Name.ToUpper()).value;
          break;
        default:
          e.Status = SymbolStatus.UndefinedSymbol;
          break;
        }
      }

      // Implement expression functions
      private void ProcessFunction(object sender, FunctionEventArgs e) {
        switch(e.Name.ToLower()) {
        case "abs":
          if(e.Parameters.Count == 1)
            e.Result = Math.Abs(e.Parameters[0]);
          else
            e.Status = FunctionStatus.WrongParameterCount;
          break;
        case "pow":
          if(e.Parameters.Count == 2)
            e.Result = Math.Pow(e.Parameters[0], e.Parameters[1]);
          else
            e.Status = FunctionStatus.WrongParameterCount;
          break;
        case "round":
          if(e.Parameters.Count == 1)
            e.Result = Math.Round(e.Parameters[0]);
          else if(e.Parameters.Count == 2) {
            e.Result = Math.Round(e.Parameters[0], (int)e.Parameters[1]);
          } else
            e.Status = FunctionStatus.WrongParameterCount;
          break;
        case "sqrt":
          if(e.Parameters.Count == 1)
            e.Result = Math.Sqrt(e.Parameters[0]);
          else
            e.Status = FunctionStatus.WrongParameterCount;
          break;
        case "acos":
          if(e.Parameters.Count == 1)
            e.Result = Math.Acos(e.Parameters[0]);
          else
            e.Status = FunctionStatus.WrongParameterCount;
          break;
        case "asin":
          if(e.Parameters.Count == 1)
            e.Result = Math.Asin(e.Parameters[0]);
          else
            e.Status = FunctionStatus.WrongParameterCount;
          break;
        case "atan":
          if(e.Parameters.Count == 1)
            e.Result = Math.Atan(e.Parameters[0]);
          else if(e.Parameters.Count==2)
            e.Result = Math.Atan2(e.Parameters[0], (int)e.Parameters[1]);
          else
            e.Status = FunctionStatus.WrongParameterCount;
          break;
        case "cos":
          if(e.Parameters.Count == 1)
            e.Result = Math.Cos(e.Parameters[0]);
          else
            e.Status = FunctionStatus.WrongParameterCount;
          break;
        case "sin":
          if(e.Parameters.Count == 1)
            e.Result = Math.Sin(e.Parameters[0]);
          else
            e.Status = FunctionStatus.WrongParameterCount;
          break;
        case "tan":
          if(e.Parameters.Count == 1)
            e.Result = Math.Tan(e.Parameters[0]);
          else
            e.Status = FunctionStatus.WrongParameterCount;
          break;
        case "sign":
          if(e.Parameters.Count == 1)
            e.Result = Math.Sign(e.Parameters[0]);
          else
            e.Status = FunctionStatus.WrongParameterCount;
          break;
        case "log":
          if(e.Parameters.Count == 1)
            e.Result = Math.Log10(e.Parameters[0]);
          else if(e.Parameters.Count==2)
            e.Result = Math.Log(e.Parameters[0], (int)e.Parameters[1]);
          else
            e.Status = FunctionStatus.WrongParameterCount;
          break;
        default: // Unknown function name
          e.Status = FunctionStatus.UndefinedFunction;
          break;
        }
      }
    }

    [Export(typeof(IStatement))]
    [ExportMetadata("declarer", "Switch")]
    private class Switch : IStatement {
      public void Load() {
        var m=Topic.root.Get<string>("/etc/declarers/func/Switch");
        m.value="pack://application:,,/CC;component/Images/ar_switch.png";
        m.Get<string>("_description").value="f2Multiplexer";
        m.Get<string>("Sel").value="Ai";
        m.Get<string>("0").value="Bg";
        m.Get<string>("1").value="Cg";
        m.Get<string>("2").value="Dg";
        m.Get<string>("3").value="Eg";
        m.Get<string>("4").value="Fg";
        m.Get<string>("5").value="Gg";
        m.Get<string>("6").value="Hg";
        m.Get<string>("Out").value="bg";
        m.Get<string>("rename").value="|R";
        m.Get<string>("remove").value="}D";
      }

      public void Init(DVar<PiStatement> model) {
        AddPin<long>(model, "Sel");
        AddPin<double>(model, "0");
        AddPin<double>(model, "1");
        AddPin<double>(model, "Out");
      }

      public void Calculate(DVar<PiStatement> model, Topic source) {
        string sel=model.Get<long>("Sel").value.ToString();
        Topic inp;
        if(model.Exist(sel, out inp) && inp.valueType==typeof(double)) {
          model.Get<double>("Out").value=(inp as DVar<double>).value;
        }
      }

      public void DeInit() {
      }
    }

    [Export(typeof(IStatement))]
    [ExportMetadata("declarer", "ZBuffer")]
    private class ZBuffer : IStatement {
      private DVar<bool> _latch;
      private DVar<bool> _oe;
      private DVar<double> _in;
      private DVar<double> _out;
      private DVar<double> _val;

      public void Load() {
        var m=Topic.root.Get<string>("/etc/declarers/func/ZBuffer");
        m.value="pack://application:,,/CC;component/Images/ar_zbuffer.png";
        m.Get<string>("_description").value="f3Buffer with output enable";
        m.Get<string>("In").value="Ag";
        m.Get<string>("Latch").value="Bz";
        m.Get<string>("OE").value="Cz";
        m.Get<string>("Out").value="ag";
        m.Get<string>("rename").value="|R";
        m.Get<string>("remove").value="}D";
      }

      public void Init(DVar<PiStatement> model) {
        _latch=AddPin<bool>(model, "Latch");
        _oe=AddPin<bool>(model, "OE");
        _in=AddPin<double>(model, "In");
        _out=AddPin<double>(model, "Out");
        _val=AddPin<double>(model, "_val");
      }

      public void Calculate(DVar<PiStatement> model, Topic source) {
        if(source==_val) {
          return;
        }
        if((source==_in && _latch.value) || (source==_latch)) {
          _val.value=_in.value;
        }
        if(_oe.value) {
          _out.value=_val.value;
        }
      }

      public void DeInit() {
      }

    }

    [Export(typeof(IStatement))]
    [ExportMetadata("declarer", "Average")]
    private class Average : IStatement {
      private DVar<double> _in, _out;
      private DVar<bool> _strobe;
      private DVar<long> _capacity;
      private Queue<double> _buf;

      public void Load() {
        var m=Topic.root.Get<string>("/etc/declarers/func/Average");
        m.value="pack://application:,,/CC;component/Images/ar_avr.png";
        m.Get<string>("_description").value="g Average";
        m.Get<string>("In").value="Ag";
        m.Get<string>("Stb").value="Bz";
        m.Get<string>("Out").value="ag";
        m.Get<string>("rename").value="|R";
        m.Get<string>("remove").value="}D";
      }

      public void Init(DVar<PiStatement> model) {
        _in=AddPin<double>(model, "In");
        _strobe=AddPin<bool>(model, "Stb");
        _capacity=AddPin<long>(model, "_period");
        _out=AddPin<double>(model, "Out");
        _out.value=_in.value;
        _buf=new Queue<double>();
      }
      public void Calculate(DVar<PiStatement> model, Topic source) {
        if(source==_strobe && _strobe.value) {
          _buf.Enqueue(_in.value);
          while(_buf.Count>(_capacity.value==0?1:_capacity.value)) {
            _buf.Dequeue();
          }
          _out.value=_buf.Average();
        }
      }
      public void DeInit() {
      }
    }

    [Export(typeof(IStatement))]
    [ExportMetadata("declarer", "Sum")]
    private class MathOpSum : IStatement {
      public void Load() {
        var m=Topic.root.Get<string>("/etc/declarers/func/Sum");
        m.value="pack://application:,,/CC;component/Images/ar_sum.png";
        m.Get<string>("_description").value="g1Addition";
        m.Get<string>("A").value="Ag";
        m.Get<string>("B").value="Bg";
        m.Get<string>("C").value="Cg";
        m.Get<string>("D").value="Dg";
        m.Get<string>("E").value="Eg";
        m.Get<string>("F").value="Fg";
        m.Get<string>("G").value="Gg";
        m.Get<string>("H").value="Hg";
        m.Get<string>("Q").value="ag";
        m.Get<string>("rename").value="|R";
        m.Get<string>("remove").value="}D";
      }
      public void Init(DVar<PiStatement> model) {
        AddPin<double>(model, "A");
        AddPin<double>(model, "B");
        AddPin<double>(model, "Q");
      }
      public void Calculate(DVar<PiStatement> model, Topic source) {
        double ret=model.Get<double>("A");
        foreach(DVar<double> pin in model.children.Where(z => (z.name.Length==1 && z.valueType==typeof(double) && z.name[0]>'A' && z.name[0]<='H')).Cast<DVar<double>>()) {
          ret+=pin.value;
          //ret-=pin.value;
          //ret*=pin.value;
          //if(pin.value!=0) {
          //  ret/=pin.value;
          //} else {
          //  ret=double.MaxValue;
          //}
          //if(pin.value!=0) {
          //  ret%=pin.value;
          //}
        }
        DVar<double> outp=model.Get<double>("Q");
        outp.value=ret;
      }
      public void DeInit() {
      }
    }

    [Export(typeof(IStatement))]
    [ExportMetadata("declarer", "Sub")]
    private class MathOpSub : IStatement {
      public void Load() {
        var m=Topic.root.Get<string>("/etc/declarers/func/Sub");
        m.value="pack://application:,,/CC;component/Images/ar_sub.png";
        m.Get<string>("_description").value="g2Subtraction";
        m.Get<string>("A").value="Ag";
        m.Get<string>("B").value="Bg";
        m.Get<string>("C").value="Cg";
        m.Get<string>("D").value="Dg";
        m.Get<string>("E").value="Eg";
        m.Get<string>("F").value="Fg";
        m.Get<string>("G").value="Gg";
        m.Get<string>("H").value="Hg";
        m.Get<string>("Q").value="ag";
        m.Get<string>("rename").value="|R";
        m.Get<string>("remove").value="}D";
      }
      public void Init(DVar<PiStatement> model) {
        AddPin<double>(model, "A");
        AddPin<double>(model, "B");
        AddPin<double>(model, "Q");
      }
      public void Calculate(DVar<PiStatement> model, Topic source) {
        double ret=model.Get<double>("A");
        foreach(DVar<double> pin in model.children.Where(z => (z.name.Length==1 && z.valueType==typeof(double) && z.name[0]>'A' && z.name[0]<='H')).Cast<DVar<double>>()) {
          ret-=pin.value;
        }
        DVar<double> outp=model.Get<double>("Q");
        outp.value=ret;
      }
      public void DeInit() {
      }
    }

    [Export(typeof(IStatement))]
    [ExportMetadata("declarer", "Mul")]
    private class MathOpMul : IStatement {
      public void Load() {
        var m=Topic.root.Get<string>("/etc/declarers/func/Mul");
        m.value="pack://application:,,/CC;component/Images/ar_mul.png";
        m.Get<string>("_description").value="g3Multiplication";
        m.Get<string>("A").value="Ag";
        m.Get<string>("B").value="Bg";
        m.Get<string>("C").value="Cg";
        m.Get<string>("D").value="Dg";
        m.Get<string>("E").value="Eg";
        m.Get<string>("F").value="Fg";
        m.Get<string>("G").value="Gg";
        m.Get<string>("H").value="Hg";
        m.Get<string>("Q").value="ag";
        m.Get<string>("rename").value="|R";
        m.Get<string>("remove").value="}D";
      }
      public void Init(DVar<PiStatement> model) {
        AddPin<double>(model, "A");
        AddPin<double>(model, "B");
        AddPin<double>(model, "Q");
      }
      public void Calculate(DVar<PiStatement> model, Topic source) {
        double ret=model.Get<double>("A");
        foreach(DVar<double> pin in model.children.Where(z => (z.name.Length==1 && z.valueType==typeof(double) && z.name[0]>'A' && z.name[0]<='H')).Cast<DVar<double>>()) {
          ret*=pin.value;
        }
        DVar<double> outp=model.Get<double>("Q");
        outp.value=ret;
      }
      public void DeInit() {
      }
    }

    [Export(typeof(IStatement))]
    [ExportMetadata("declarer", "Div")]
    private class MathOpDiv : IStatement {
      public void Load() {
        var m=Topic.root.Get<string>("/etc/declarers/func/Div");
        m.value="pack://application:,,/CC;component/Images/ar_div.png";
        m.Get<string>("_description").value="g4Division";
        m.Get<string>("A").value="Ag";
        m.Get<string>("B").value="Bg";
        m.Get<string>("C").value="Cg";
        m.Get<string>("D").value="Dg";
        m.Get<string>("E").value="Eg";
        m.Get<string>("F").value="Fg";
        m.Get<string>("G").value="Gg";
        m.Get<string>("H").value="Hg";
        m.Get<string>("Q").value="ag";
        m.Get<string>("rename").value="|R";
        m.Get<string>("remove").value="}D";
      }
      public void Init(DVar<PiStatement> model) {
        AddPin<double>(model, "A");
        AddPin<double>(model, "B");
        AddPin<double>(model, "Q");
      }
      public void Calculate(DVar<PiStatement> model, Topic source) {
        double ret=model.Get<double>("A");
        foreach(DVar<double> pin in model.children.Where(z => (z.name.Length==1 && z.valueType==typeof(double) && z.name[0]>'A' && z.name[0]<='H')).Cast<DVar<double>>()) {
          if(pin.value!=0) {
            ret/=pin.value;
          }
        }
        DVar<double> outp=model.Get<double>("Q");
        outp.value=ret;
      }
      public void DeInit() {
      }
    }

    [Export(typeof(IStatement))]
    [ExportMetadata("declarer", "Remainder")]
    private class MathOpMod : IStatement {
      public void Load() {
        var m=Topic.root.Get<string>("/etc/declarers/func/Remainder");
        m.value="pack://application:,,/CC;component/Images/ar_mod.png";
        m.Get<string>("_description").value="g5Modulo";
        m.Get<string>("A").value="Ag";
        m.Get<string>("B").value="Bg";
        m.Get<string>("C").value="Cg";
        m.Get<string>("D").value="Dg";
        m.Get<string>("E").value="Eg";
        m.Get<string>("F").value="Fg";
        m.Get<string>("G").value="Gg";
        m.Get<string>("H").value="Hg";
        m.Get<string>("Q").value="ag";
        m.Get<string>("rename").value="|R";
        m.Get<string>("remove").value="}D";
      }
      public void Init(DVar<PiStatement> model) {
        AddPin<double>(model, "A");
        AddPin<double>(model, "B");
        AddPin<double>(model, "Q");
      }
      public void Calculate(DVar<PiStatement> model, Topic source) {
        double ret=model.Get<double>("A");
        foreach(DVar<double> pin in model.children.Where(z => (z.name.Length==1 && z.valueType==typeof(double) && z.name[0]>'A' && z.name[0]<='H')).Cast<DVar<double>>()) {
          if(pin.value!=0) {
            ret%=pin.value;
          }
        }
        DVar<double> outp=model.Get<double>("Q");
        outp.value=ret;
      }
      public void DeInit() {
      }
    }

    [Export(typeof(IStatement))]
    [ExportMetadata("declarer", "BreakerO")]
    private class Breaker : IStatement {
      DVar<object> _in;
      DVar<object> _out;
      DVar<bool> _oe;

      public void Load() {
        var m=Topic.root.Get<string>("/etc/declarers/func/BreakerO");
        m.value="pack://application:,,/CC;component/Images/ar_breaker.png";
        m.Get<string>("_description").value="o Switch";
        m.Get<string>("In").value="Ao";
        m.Get<string>("OE").value="Bz";
        m.Get<string>("Out").value="ao";
        m.Get<string>("rename").value="|R";
        m.Get<string>("remove").value="}D";
      }

      public void Init(DVar<PiStatement> model) {
        _in=AddPin<object>(model, "In");
        _oe=AddPin<bool>(model, "OE");
        _out=AddPin<object>(model, "Out");
      }

      public void Calculate(DVar<PiStatement> model, Topic source) {
        if(_oe.value) {
          _out.SetValue(_in.value, new TopicChanged(TopicChanged.ChangeArt.Value, model));
        }
      }

      public void DeInit() {
      }
    }

    [Export(typeof(IStatement))]
    [ExportMetadata("declarer", "StrFormat")]
    private class StrFormat : IStatement {
      private DVar<string> _dFmt;
      private DVar<string> _out;

      public void Load() {
        var m=Topic.root.Get<string>("/etc/declarers/func/StrFormat");
        m.value="pack://application:,,/CC;component/Images/st_fmt.png";
        m.Get<string>("_description").value="s string.Format";
        m.Get<string>("0").value="Ao";
        m.Get<string>("1").value="Bo";
        m.Get<string>("2").value="Co";
        m.Get<string>("3").value="Do";
        m.Get<string>("4").value="Eo";
        m.Get<string>("5").value="Fo";
        m.Get<string>("6").value="Go";
        m.Get<string>("7").value="Ho";
        m.Get<string>("O").value="as";
        m.Get<string>("rename").value="|R";
        m.Get<string>("remove").value="}D";
      }

      public void Init(DVar<PiStatement> model) {
        _dFmt=AddPin<string>(model, "_format");
        AddPin<object>(model, "0");
        _out=AddPin<string>(model, "O");
      }

      public void Calculate(DVar<PiStatement> model, Topic source) {
        if(string.IsNullOrEmpty(_dFmt.value)) {
          return;
        }
        object[] inp=new object[8];
        for(int i=0; i<8; i++) {
          inp[i]=null;
        }
        foreach(DVar<object> pin in model.children.Where(z => (z.name.Length==1 && z.valueType==typeof(object) && z.name[0]>='0' && z.name[0]<='7')).Cast<DVar<object>>()) {
          inp[(int)(pin.name[0]-'0')]=pin.value;
        }
        string fmt=Newtonsoft.Json.JsonConvert.DeserializeObject<string>(string.Format("\"{0}\"", _dFmt.value));
        _out.value=string.Format(fmt, inp[0], inp[1], inp[2], inp[3], inp[4], inp[5], inp[6], inp[7]);
      }

      public void DeInit() {
      }
    }

    [Export(typeof(IStatement))]
    [ExportMetadata("declarer", "Pulse")]
    private class Impuls : IStatement {
      private DVar<bool> _input;
      private DVar<bool> _reset;
      private DVar<bool> _output;
      private DVar<bool> _iOutput;
      private DVar<long> _onDelay;
      private DVar<long> _offDelay;
      private int _state;
      private Timer _timer;

      public void Load() {
        var t1=Topic.root.Get<string>("/etc/declarers/func/Pulse");
        t1.value="pack://application:,,/CC;component/Images/bi_impulse.png";
        t1.Get<string>("Stb").value="Az";
        t1.Get<string>("Reset").value="Bz";
        t1.Get<string>("Q").value="az";
        t1.Get<string>("!Q").value="bz";

        t1.Get<string>("_description").value="t Univibrator";

        t1.Get<string>("rename").value="|R";
        t1.Get<string>("remove").value="}D";
      }

      public void Init(DVar<PiStatement> model) {
        _input=AddPin<bool>(model, "Stb");
        _reset=AddPin<bool>(model, "Reset");
        _output=AddPin<bool>(model, "Q");
        _iOutput=AddPin<bool>(model, "!Q");
        _onDelay=AddPin<long>(model, "_onDelay");
        _offDelay=AddPin<long>(model, "_offDelay");
        model.Get<bool>("!Q").value=!model.Get<bool>("Q").value;
        _state=0;
        _timer=new Timer((o) => process(), null, Timeout.Infinite, Timeout.Infinite);
      }
      public void Calculate(DVar<PiStatement> model, Topic source) {
        if(_reset.value) {
          _state=0;
          process();
        }
        if(_input==source && _input.value) {
          if(_onDelay.value>0) {
            _state=1;
          } else {
            _state=2;
          }
          process();
        }
      }
      public void DeInit() {
        if(_timer!=null) {
          _timer.Change(Timeout.Infinite, Timeout.Infinite);
          _timer=null;
        }
      }
      private void process() {
        bool rez=_output.value;
        switch(_state) {
        case 1:
          _timer.Change(_onDelay.value, Timeout.Infinite);
          _state++;
          break;
        case 2:
          rez=true;
          _timer.Change(_offDelay.value==0?1:_offDelay.value, Timeout.Infinite);
          _state++;
          break;
        case 3:
          rez=false;
          _state=0;
          break;
        default:
          _state=0;
          rez=false;
          _timer.Change(Timeout.Infinite, Timeout.Infinite);
          break;
        }
        _output.value=rez;
        _iOutput.value=!rez;
      }
    }

    [Export(typeof(IStatement))]
    [ExportMetadata("declarer", "SqPulse")]
    private class PulseGenerator : IStatement {
      private DVar<bool> _enable;
      private DVar<bool> _output;
      private DVar<bool> _iOutput;
      private DVar<long> _offDelay;
      private DVar<long> _period;
      private Timer _timerOn;
      private Timer _timerOff;

      public void Load() {
        var t1=Topic.root.Get<string>("/etc/declarers/func/SqPulse");
        t1.value="pack://application:,,/CC;component/Images/bi_oscillator.png";
        t1.Get<string>("En").value="Az";
        t1.Get<string>("Q").value="az";
        t1.Get<string>("!Q").value="bz";

        t1.Get<string>("_description").value="t2Period oscilator";

        t1.Get<string>("rename").value="|R";
        t1.Get<string>("remove").value="}D";
      }

      public void Init(DVar<PiStatement> model) {
        _enable=AddPin<bool>(model, "En");
        _output=AddPin<bool>(model, "Q");
        _iOutput=AddPin<bool>(model, "!Q");
        _offDelay=AddPin<long>(model, "_offDelay");
        _period=AddPin<long>(model, "_period");
        _iOutput.value=!_output.value;
        _timerOn=new Timer(new TimerCallback(SwitchOn));
        _timerOff=new Timer(new TimerCallback(SwitchOff));
        Calculate(model, _enable);
      }
      private void SwitchOn(object o) {
        _timerOff.Change(_offDelay, _period.value);
        _output.value=true;
        _iOutput.value=false;
      }
      private void SwitchOff(object o) {
        _output.value=false;
        _iOutput.value=true;
      }
      public void Calculate(DVar<PiStatement> model, Topic source) {
        if(source==_output || source==_iOutput) {
          return;
        }
        if(!_enable.value || _offDelay.value==0) {
          _output.value=false;
          _iOutput.value=true;
          _timerOn.Change(Timeout.Infinite, Timeout.Infinite);
          _timerOff.Change(Timeout.Infinite, Timeout.Infinite);
          return;
        }
        _output.value=true;
        _iOutput.value=false;
        if(_offDelay.value>_period.value) {
          _timerOn.Change(Timeout.Infinite, Timeout.Infinite);
          _timerOff.Change(Timeout.Infinite, Timeout.Infinite);
          return;
        }
        _timerOff.Change(_offDelay, _period.value);
        _timerOn.Change(0, _period.value);
      }
      public void DeInit() {
        if(_timerOn!=null) {
          _timerOn.Change(Timeout.Infinite, Timeout.Infinite);
          _timerOn=null;
        }
        if(_timerOff!=null) {
          _timerOff.Change(Timeout.Infinite, Timeout.Infinite);
          _timerOff=null;
        }
      }
    }

    [Export(typeof(IStatement))]
    [ExportMetadata("declarer", "Cosm")]
    private class Cosm : IStatement {
      private DVar<bool> _push;
      private DVar<string> _feed;
      private DVar<string> _key;

      public void Load() {
        var m=Topic.root.Get<string>("/etc/declarers/func/Cosm");
        m.value="pack://application:,,/CC;component/Images/fu_cosm.png";
        m.Get<string>("_description").value="v Export to cosm.com";
        m.Get<string>("Push").value="Az";
        m.Get<string>("A").value="Bg";
        m.Get<string>("B").value="Cg";
        m.Get<string>("C").value="Dg";
        m.Get<string>("D").value="Eg";
        m.Get<string>("E").value="Fg";
        m.Get<string>("F").value="Gg";
        m.Get<string>("G").value="Hg";
        m.Get<string>("rename").value="|R";
        m.Get<string>("remove").value="}D";
      }

      public void Init(DVar<PiStatement> model) {
        AddPin<double>(model, "A");
        _push=AddPin<bool>(model, "Push");
        _feed=AddPin<string>(model, "_feed");
        _key=AddPin<string>(model, "_key");
      }

      public void Calculate(DVar<PiStatement> model, Topic source) {
        if(string.IsNullOrEmpty(_feed.value) || string.IsNullOrEmpty(_key.value) || !_push.value) {
          return;
        }
        if(source==_push) {
          StringBuilder sb=new StringBuilder();
          foreach(var inp in model.children.Where(z => z is DVar<double> && z.name.Length==1 && z.name[0]>='A' && z.name[0]<='G').Cast<DVar<double>>()) {
            string valS=inp.value.ToString(CultureInfo.InvariantCulture);
            {
              int i=Math.Max(valS.IndexOf('.'), 6);
              if(i<valS.Length) {
                valS=valS.Substring(0, i);
              }
            }
            sb.AppendFormat("{0},{1}\n", inp.name, valS);
          }
          ThreadPool.QueueUserWorkItem((o) => Send(_key.value, _feed.value, sb.ToString()));
        } else if(source.valueType==typeof(double) && source.name.Length==1 && source.name[0]>='A' && source.name[0]<='G') {
          string p=_feed.value+"/datastreams/"+source.name;
          string v=(source as DVar<double>).value.ToString(CultureInfo.InvariantCulture);
          ThreadPool.QueueUserWorkItem((o) => Send(_key.value, p, v));
        }
      }
      private void Send(string apiKey, string feedId, string sample) {
        try {
          byte[] buffer = Encoding.UTF8.GetBytes(sample);

          var request = (HttpWebRequest)WebRequest.Create("https://api.xively.com/v2/feeds/" + feedId + ".csv");

          // request line
          request.Method = "PUT";

          // request headers
          request.ContentLength = buffer.Length;
          request.ContentType = "text/csv";
          request.Headers.Add("X-ApiKey", apiKey);

          // request body
          using(Stream stream = request.GetRequestStream()) {
            stream.Write(buffer, 0, buffer.Length);
          }

          request.Timeout = 5000;     // 5 seconds
          // send request and receive response
          using(var response =(HttpWebResponse)request.GetResponse()) {
            if(response.StatusCode!=HttpStatusCode.OK) {
              Log.Warning("Cosm({0}) - {1}", feedId, response.StatusCode);
            }
          }
          request=null;
        }
        catch(Exception ex) {
          Log.Warning("Cosm({0}) - {1}", feedId, ex.Message);
        }
      }

      public void DeInit() {
      }
    }

    [Export(typeof(IStatement))]
    [ExportMetadata("declarer", "Pile")]
    private class Pile : IStatement {
      public void Load() {
        var m=Topic.root.Get<string>("/etc/declarers/func/Pile");
        m.value="pack://application:,,/CC;component/Images/ar_pile.png";
        m.Get<string>("_description").value="v Export to .CSV";
        m.Get<string>("Push").value="Az";
        m.Get<string>("A").value="Bg";
        m.Get<string>("B").value="Cg";
        m.Get<string>("C").value="Dg";
        m.Get<string>("D").value="Eg";
        m.Get<string>("E").value="Fg";
        m.Get<string>("F").value="Gg";
        m.Get<string>("G").value="Hg";
        m.Get<string>("rename").value="|R";
        m.Get<string>("remove").value="}D";
      }

      public void Init(DVar<PiStatement> model) {
        AddPin<double>(model, "A");
        AddPin<bool>(model, "Push");
        AddPin<string>(model, "_id_A");
        AddPin<string>(model, "_id_B");
        AddPin<string>(model, "_id_C");
        AddPin<string>(model, "_id_D");
        AddPin<string>(model, "_id_E");
        AddPin<string>(model, "_id_F");
        AddPin<string>(model, "_id_G");
        AddPin<string>(model, "_fileName");
        AddPin<long>(model, "_capacity");
        var xFmt=AddPin<string>(model, "_XFormat");
        if(string.IsNullOrEmpty(xFmt.value)) {
          xFmt.value="yyyy-MM-dd HH:mm:ss";
        }
      }

      public void Calculate(DVar<PiStatement> model, Topic source) {
        var push=model.Get<bool>("Push");
        if(source==push && push.value) {
          string path=model.Get<string>("_fileName").value;
          string[] old=null;
          if(File.Exists(path)) {
            try {
              old=File.ReadAllLines(path);
            }
            catch(Exception) {
            }
          }
          if(old==null) {
            old=new string[] { string.Empty };
          }

          long cap=model.Get<long>("_capacity").value;
          string header="DT";
          string cur=DateTime.Now.ToString(model.Get<string>("_XFormat").value);
          string valS;
          foreach(var inp in model.children.Where(z => z is DVar<double> && z.name.Length==1 && z.name[0]>='A' && z.name[0]<='G').Cast<DVar<double>>()) {
            var id=model.Get<string>(string.Format("_id_{0}", inp.name)).value;
            if(string.IsNullOrEmpty(id)) {
              id=inp.name;
            }
            header=header+","+id;
            valS=inp.value.ToString(CultureInfo.InvariantCulture);
            {
              int i=Math.Max(valS.IndexOf('.'), 6);
              if(i<valS.Length) {
                valS=valS.Substring(0, i);
              }
            }
            cur=cur+","+valS;
          }
          using(StreamWriter file = new StreamWriter(path, false)) {
            file.WriteLine(header);
            int stIndex=old.Length-(int)cap+1;
            if(cap<1 || stIndex<1) {
              stIndex=1;
            }
            foreach(var l in old.Skip(stIndex)) {
              file.WriteLine(l);
            }
            file.WriteLine(cur);
          }
        }
      }

      public void DeInit() {
      }
    }

    [Export(typeof(IStatement))]
    [ExportMetadata("declarer", "Execute")]
    private class Execute : IStatement {
      private DVar<string> _proc;
      private DVar<string> _args;
      private DVar<bool> _start;

      public void Load() {
        var m=Topic.root.Get<string>("/etc/declarers/func/Execute");
        m.value="pack://application:,,/CC;component/Images/fu_exec.png";
        m.Get<string>("_description").value="v Execute process";
        m.Get<string>("process").value="As";
        m.Get<string>("arguments").value="Bs";
        m.Get<string>("start").value="Cz";
        m.Get<string>("rename").value="|R";
        m.Get<string>("remove").value="}D";
      }

      public void Init(DVar<PiStatement> model) {
        _proc=AddPin<string>(model, "process");
        _args=AddPin<string>(model, "arguments");
        _start=AddPin<bool>(model, "start");
      }

      public void Calculate(DVar<PiStatement> model, Topic source) {
        if(source!=_start || !_start.value || string.IsNullOrEmpty(_proc.value)) {
          return;
        }
        Thread objThread = new Thread(new ThreadStart(StartProcess));
        objThread.IsBackground = true;
        objThread.Priority = ThreadPriority.BelowNormal;
        objThread.Start();
      }

      private void StartProcess() {
        try {
          System.Diagnostics.Process.Start(_proc.value, _args.value);
          Log.Info("Execute({0}, {1})", _proc.value, _args.value);
        }
        catch(Exception ex) {
          Log.Warning("Execute({0}, {1}) - {2}", _proc.value, _args.value, ex.Message);
        }
      }

      public void DeInit() {
      }
    }
  }
}
