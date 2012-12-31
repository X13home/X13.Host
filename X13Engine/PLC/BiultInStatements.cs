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

namespace X13.PLC {
  public class BiultInStatements {
    public static void Initialize() {
      PiStatement.AddStatemen("AND", typeof(And));
      PiStatement.AddStatemen("ANDI", typeof(AndI));
      PiStatement.AddStatemen("OR", typeof(Or));
      PiStatement.AddStatemen("ORI", typeof(OrI));
      PiStatement.AddStatemen("XOR", typeof(Xor));
      PiStatement.AddStatemen("XORI", typeof(XorI));
      PiStatement.AddStatemen("NOT", typeof(Not));
      PiStatement.AddStatemen("SHL", typeof(Shl));
      PiStatement.AddStatemen("SHR", typeof(Shr));
      PiStatement.AddStatemen("DTriger", typeof(DTriger));
      PiStatement.AddStatemen("Pulse", typeof(Impuls));
      PiStatement.AddStatemen("SqPulse", typeof(PulseGenerator));
      PiStatement.AddStatemen("Comparer", typeof(Comparer));
      PiStatement.AddStatemen("comp_gr", typeof(Comparer));
      PiStatement.AddStatemen("comp_eq", typeof(Comparer));
      PiStatement.AddStatemen("comp_le", typeof(Comparer));
      PiStatement.AddStatemen("Counter", typeof(Counter));
      PiStatement.AddStatemen("Average", typeof(Average));
      PiStatement.AddStatemen("Switch", typeof(Switch));
      PiStatement.AddStatemen("Sum", typeof(MathOp));
      PiStatement.AddStatemen("Sub", typeof(MathOp));
      PiStatement.AddStatemen("Mul", typeof(MathOp));
      PiStatement.AddStatemen("Div", typeof(MathOp));
      PiStatement.AddStatemen("Remainder", typeof(MathOp));
      PiStatement.AddStatemen("Pile", typeof(Pile));
      PiStatement.AddStatemen("Cosm", typeof(Cosm));
      PiStatement.AddStatemen("Sun", typeof(Sun));
      PiStatement.AddStatemen("StrFormat", typeof(StrFormat));
      PiStatement.AddStatemen("MathExpr", typeof(MathExpr));
      PiStatement.AddStatemen("Execute", typeof(Execute));
      PiStatement.AddStatemen("Breaker", typeof(Breaker));
      PiStatement.AddStatemen("BreakerO", typeof(BreakerO));
    }

    public static DVar<T> AddPin<T>(DVar<PiStatement> model, string name) {
      DVar<T> pin=model.Get<T>(name);
      pin.saved=true;
      pin.Publish(pin, TopicChanged.ChangeArt.Value, null);
      return pin;
    }

    private class And : IStatement {
      static And() {
        var t1=Topic.root.Get<string>("/system/declarers/AND");
        t1.value="/CC;component/Images/bi_and.png";
        t1.Get<string>("A").value="Az";
        t1.Get<string>("B").value="Bz";
        t1.Get<string>("C").value="Cz";
        t1.Get<string>("D").value="Dz";
        t1.Get<string>("E").value="Ez";
        t1.Get<string>("F").value="Fz";
        t1.Get<string>("G").value="Gz";
        t1.Get<string>("H").value="Hz";
        t1.Get<string>("Q").value="az";
        t1.Get<string>("rename").value="|R";
        t1.Get<string>("remove").value="}D";
      }
      public void Init(DVar<PiStatement> model) {
        AddPin<bool>(model, "A");
        AddPin<bool>(model, "B");
        AddPin<bool>(model, "Q");
      }

      public void Calculate(DVar<PiStatement> model, Topic source) {
        bool ret=true;
        foreach(DVar<bool> pin in model.children.Where(z => (z.name.Length==1 && z.valueType==typeof(bool) && z.name[0]>='A' && z.name[0]<='H')).Cast<DVar<bool>>()) {
          ret&=pin;
        }
        DVar<bool> op=model.Get<bool>("Q");
        op.saved=true;
        op.value=ret;
      }

      public void DeInit() {
      }
    }
    private class AndI : IStatement {
      static AndI() {
        var t1=Topic.root.Get<string>("/system/declarers/ANDI");
        t1.value="/CC;component/Images/bi_and.png";
        t1.Get<string>("A").value="Ai";
        t1.Get<string>("B").value="Bi";
        t1.Get<string>("C").value="Ci";
        t1.Get<string>("D").value="Di";
        t1.Get<string>("E").value="Ei";
        t1.Get<string>("F").value="Fi";
        t1.Get<string>("G").value="Gi";
        t1.Get<string>("H").value="Hi";
        t1.Get<string>("Q").value="ai";
        t1.Get<string>("rename").value="|R";
        t1.Get<string>("remove").value="}D";
      }

      public void Init(DVar<PiStatement> model) {
        AddPin<int>(model, "A");
        AddPin<int>(model, "B");
        AddPin<int>(model, "Q");
      }

      public void Calculate(DVar<PiStatement> model, Topic source) {
        int ret=-1;  // 0xFFFFFFFF
        foreach(DVar<int> pin in model.children.Where(z => (z.name.Length==1 && z.valueType==typeof(int) && z.name[0]>='A' && z.name[0]<='H')).Cast<DVar<int>>()) {
          ret&=pin.value;
        }
        model.Get<int>("Q").value=ret;
      }

      public void DeInit() {
      }
    }

    private class Or : IStatement {
      static Or() {
        var t1=Topic.root.Get<string>("/system/declarers/OR");
        t1.value="/CC;component/Images/bi_or.png";
        t1.Get<string>("A").value="Az";
        t1.Get<string>("B").value="Bz";
        t1.Get<string>("C").value="Cz";
        t1.Get<string>("D").value="Dz";
        t1.Get<string>("E").value="Ez";
        t1.Get<string>("F").value="Fz";
        t1.Get<string>("G").value="Gz";
        t1.Get<string>("H").value="Hz";
        t1.Get<string>("Q").value="az";
        t1.Get<string>("rename").value="|R";
        t1.Get<string>("remove").value="}D";
      }

      public void Init(DVar<PiStatement> model) {
        AddPin<bool>(model, "A");
        AddPin<bool>(model, "B");
        AddPin<bool>(model, "Q");
      }

      public void Calculate(DVar<PiStatement> model, Topic source) {
        bool ret=false;
        foreach(DVar<bool> pin in model.children.Where(z => (z.name.Length==1 && z.valueType==typeof(bool) && z.name[0]>='A' && z.name[0]<='H')).Cast<DVar<bool>>()) {
          ret|=pin;
        }
        DVar<bool> op=model.Get<bool>("Q");
        op.saved=true;
        op.value=ret;
      }

      public void DeInit() {
      }
    }
    private class OrI : IStatement {
      static OrI() {
        var t1=Topic.root.Get<string>("/system/declarers/ORI");
        t1.value="/CC;component/Images/bi_or.png";
        t1.Get<string>("A").value="Ai";
        t1.Get<string>("B").value="Bi";
        t1.Get<string>("C").value="Ci";
        t1.Get<string>("D").value="Di";
        t1.Get<string>("E").value="Ei";
        t1.Get<string>("F").value="Fi";
        t1.Get<string>("G").value="Gi";
        t1.Get<string>("H").value="Hi";
        t1.Get<string>("Q").value="ai";
        t1.Get<string>("rename").value="|R";
        t1.Get<string>("remove").value="}D";
      }

      public void Init(DVar<PiStatement> model) {
        AddPin<int>(model, "A");
        AddPin<int>(model, "B");
        AddPin<int>(model, "Q");
      }

      public void Calculate(DVar<PiStatement> model, Topic source) {
        int ret=0;
        foreach(DVar<int> pin in model.children.Where(z => (z.name.Length==1 && z.valueType==typeof(int) && z.name[0]>='A' && z.name[0]<='H')).Cast<DVar<int>>()) {
          ret|=pin;
        }
        model.Get<int>("Q").value=ret;
      }

      public void DeInit() {
      }
    }

    private class Xor : IStatement {
      static Xor() {
        var t1=Topic.root.Get<string>("/system/declarers/XOR");
        t1.value="/CC;component/Images/bi_xor.png";
        t1.Get<string>("A").value="Az";
        t1.Get<string>("B").value="Bz";
        t1.Get<string>("C").value="Cz";
        t1.Get<string>("D").value="Dz";
        t1.Get<string>("E").value="Ez";
        t1.Get<string>("F").value="Fz";
        t1.Get<string>("G").value="Gz";
        t1.Get<string>("H").value="Hz";
        t1.Get<string>("Q").value="az";
        t1.Get<string>("rename").value="|R";
        t1.Get<string>("remove").value="}D";
      }

      public void Init(DVar<PiStatement> model) {
        AddPin<bool>(model, "A");
        AddPin<bool>(model, "B");
        AddPin<bool>(model, "Q");
      }

      public void Calculate(DVar<PiStatement> model, Topic source) {
        bool ret=false;
        foreach(DVar<bool> pin in model.children.Where(z => (z.name.Length==1 && z.valueType==typeof(bool) && z.name[0]>='A' && z.name[0]<='H')).Cast<DVar<bool>>()) {
          ret^=pin;
        }
        DVar<bool> op=model.Get<bool>("Q");
        op.saved=true;
        op.value=ret;
      }

      public void DeInit() {
      }
    }
    private class XorI : IStatement {
      static XorI() {
        var t1=Topic.root.Get<string>("/system/declarers/XORI");
        t1.value="/CC;component/Images/bi_xor.png";
        t1.Get<string>("A").value="Ai";
        t1.Get<string>("B").value="Bi";
        t1.Get<string>("C").value="Ci";
        t1.Get<string>("D").value="Di";
        t1.Get<string>("E").value="Ei";
        t1.Get<string>("F").value="Fi";
        t1.Get<string>("G").value="Gi";
        t1.Get<string>("H").value="Hi";
        t1.Get<string>("Q").value="ai";
        t1.Get<string>("rename").value="|R";
        t1.Get<string>("remove").value="}D";
      }

      public void Init(DVar<PiStatement> model) {
        AddPin<int>(model, "A");
        AddPin<int>(model, "B");
        AddPin<int>(model, "Q");
      }

      public void Calculate(DVar<PiStatement> model, Topic source) {
        int ret=0;
        foreach(DVar<int> pin in model.children.Where(z => (z.name.Length==1 && z.valueType==typeof(int) && z.name[0]>='A' && z.name[0]<='H')).Cast<DVar<int>>()) {
          ret^=pin;
        }
        model.Get<int>("Q").value=ret;
      }

      public void DeInit() {
      }
    }

    private class Not : IStatement {
      static Not() {
        var t1=Topic.root.Get<string>("/system/declarers/NOT");
        t1.value="/CC;component/Images/bi_not.png";
        t1.Get<string>("A").value="Az";
        t1.Get<string>("Q").value="az";
        t1.Get<string>("rename").value="|R";
        t1.Get<string>("remove").value="}D";
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

    private class Shl : IStatement {
      static Shl() {
        var t1=Topic.root.Get<string>("/system/declarers/SHL");
        t1.value="/CC;component/Images/bi_shl.png";
        t1.Get<string>("A").value="Ai";
        t1.Get<string>("B").value="Bi";
        t1.Get<string>("Q").value="ai";
        t1.Get<string>("rename").value="|R";
        t1.Get<string>("remove").value="}D";
      }

      public void Init(DVar<PiStatement> model) {
        AddPin<int>(model, "A");
        AddPin<int>(model, "B");
        AddPin<int>(model, "Q");
      }

      public void Calculate(DVar<PiStatement> model, Topic source) {
        model.Get<int>("Q").value=model.Get<int>("A").value<<model.Get<int>("B").value;
      }

      public void DeInit() {
      }
    }
    private class Shr : IStatement {
      static Shr() {
        var t1=Topic.root.Get<string>("/system/declarers/SHR");
        t1.value="/CC;component/Images/bi_shr.png";
        t1.Get<string>("A").value="Ai";
        t1.Get<string>("B").value="Bi";
        t1.Get<string>("Q").value="ai";
        t1.Get<string>("rename").value="|R";
        t1.Get<string>("remove").value="}D";
      }

      public void Init(DVar<PiStatement> model) {
        AddPin<int>(model, "A");
        AddPin<int>(model, "B");
        AddPin<int>(model, "Q");
      }

      public void Calculate(DVar<PiStatement> model, Topic source) {
        model.Get<int>("Q").value=model.Get<int>("A").value>>model.Get<int>("B").value;
      }

      public void DeInit() {
      }
    }

    private class DTriger : IStatement {
      static DTriger() {
        var t1=Topic.root.Get<string>("/system/declarers/DTriger");
        t1.value="/CC;component/Images/bi_triger.png";
        t1.Get<string>("S").value="Az";
        t1.Get<string>("D").value="Bz";
        t1.Get<string>("C").value="Cz";
        t1.Get<string>("R").value="Dz";
        t1.Get<string>("Q").value="az";
        t1.Get<string>("!Q").value="bz";
        t1.Get<string>("rename").value="|R";
        t1.Get<string>("remove").value="}D";
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

    private class Impuls : IStatement {
      static Impuls() {
        var t1=Topic.root.Get<string>("/system/declarers/Pulse");
        t1.value="/CC;component/Images/bi_impulse.png";
        t1.Get<string>("Stb").value="Az";
        t1.Get<string>("Q").value="az";
        t1.Get<string>("!Q").value="bz";
        t1.Get<string>("rename").value="|R";
        t1.Get<string>("remove").value="}D";
      }

      private DVar<bool> _input;
      private DVar<bool> _output;
      private DVar<bool> _iOutput;
      private DVar<uint> _onDelay;
      private DVar<uint> _offDelay;
      private int _state;
      private Timer _timer;

      public void Init(DVar<PiStatement> model) {
        _input=AddPin<bool>(model, "Stb");
        _output=AddPin<bool>(model, "Q");
        _iOutput=AddPin<bool>(model, "!Q");
        _onDelay=AddPin<uint>(model, "_onDelay");
        _offDelay=AddPin<uint>(model, "_offDelay");
        model.Get<bool>("!Q").value=!model.Get<bool>("Q").value;
        _state=0;
        _timer=new Timer((o) => process(), null, Timeout.Infinite, Timeout.Infinite);
      }
      public void Calculate(DVar<PiStatement> model, Topic source) {
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

    private class PulseGenerator : IStatement {
      static PulseGenerator() {
        var t1=Topic.root.Get<string>("/system/declarers/SqPulse");
        t1.value="/CC;component/Images/bi_oscillator.png";
        t1.Get<string>("En").value="Az";
        t1.Get<string>("Q").value="az";
        t1.Get<string>("!Q").value="bz";
        t1.Get<string>("rename").value="|R";
        t1.Get<string>("remove").value="}D";

      }
      private DVar<bool> _enable;
      private DVar<bool> _output;
      private DVar<bool> _iOutput;
      private DVar<uint> _offDelay;
      private DVar<uint> _period;
      private Timer _timerOn;
      private Timer _timerOff;

      public void Init(DVar<PiStatement> model) {
        _enable=AddPin<bool>(model, "En");
        _output=AddPin<bool>(model, "Q");
        _iOutput=AddPin<bool>(model, "!Q");
        _offDelay=AddPin<uint>(model, "_offDelay");
        _period=AddPin<uint>(model, "_period");
        _iOutput.value=!_output.value;
        _timerOn=new Timer(new TimerCallback(SwitchOn));
        _timerOff=new Timer(new TimerCallback(SwitchOff));
        Calculate(model, _enable);
      }
      private void SwitchOn(object o) {
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

    private class Comparer : IStatement {
      static Comparer() {
        var t1=Topic.root.Get<string>("/system/declarers/Comparer");
        t1.value="/CC;component/Images/ty_func.png";
        t1.Get<string>("A").value="Ac";
        t1.Get<string>("B").value="Cc";
        t1.Get<string>("<").value="az";
        t1.Get<string>("=").value="bz";
        t1.Get<string>(">").value="cz";
        t1.Get<string>("rename").value="|R";
        t1.Get<string>("remove").value="}D";
        FillDecl(Topic.root.Get<string>("/system/declarers/comp_gr"), "ar_comp_gr");
        FillDecl(Topic.root.Get<string>("/system/declarers/comp_eq"), "ar_eq");
        FillDecl(Topic.root.Get<string>("/system/declarers/comp_le"), "ar_comp_le");
      }
      private static void FillDecl(DVar<string> t1, string icon) {
        t1.value="/CC;component/Images/"+icon+".png";
        t1.Get<string>("A").value="Ac";
        t1.Get<string>("B").value="Bc";
        t1.Get<string>("Q").value="az";
        t1.Get<string>("!Q").value="bz";
        t1.Get<string>("rename").value="|R";
        t1.Get<string>("remove").value="}D";
      }

      private DVar<string> declarer;
      private DVar<decimal> _a;
      private DVar<decimal> _b;

      public void Init(DVar<PiStatement> model) {
        _a=AddPin<Decimal>(model, "A");
        _b=AddPin<Decimal>(model, "B");
        declarer=model.Get<string>("_declarer");
        if(declarer.value=="Comparer") {
          AddPin<bool>(model, ">");
          AddPin<bool>(model, "=");
          AddPin<bool>(model, "<");
        } else {
          AddPin<bool>(model, "Q");
          AddPin<bool>(model, "!Q");
        }
        Calculate(model, _a);
      }
      public void Calculate(DVar<PiStatement> model, Topic source) {
        if(source==_a || source==_b) {
          switch(declarer.value) {
          case "Comparer":
            model.Get<bool>(">").value=_a.value>_b.value;
            model.Get<bool>("=").value=_a.value==_b.value;
            model.Get<bool>("<").value=_a.value<_b.value;
            break;
          case "comp_gr":
            model.Get<bool>("Q").value=_a.value>_b.value;
            model.Get<bool>("!Q").value=!model.Get<bool>("Q").value;
            break;
          case "comp_le":
            model.Get<bool>("Q").value=_a.value<_b.value;
            model.Get<bool>("!Q").value=!model.Get<bool>("Q").value;
            break;
          case "comp_eq":
            model.Get<bool>("Q").value=_a.value==_b.value;
            model.Get<bool>("!Q").value=!model.Get<bool>("Q").value;
            break;
          }
        }
      }
      public void DeInit() {
      }
    }

    private class Counter : IStatement {
      static Counter() {
        var t1=Topic.root.Get<string>("/system/declarers/Counter");
        t1.value="/CC;component/Images/bi_counter.png";
        t1.Get<string>("+1").value="Az";
        t1.Get<string>("Set").value="Bz";
        t1.Get<string>("Value").value="Ci";
        t1.Get<string>("Reset").value="Dz";
        t1.Get<string>("-1").value="Ez";
        t1.Get<string>("Out").value="ci";
        t1.Get<string>("rename").value="|R";
        t1.Get<string>("remove").value="}D";
      }

      private DVar<bool> _inc, _set, _reset, _dec;
      private DVar<int> _val, _out;
      public void Init(DVar<PiStatement> model) {
        _inc=AddPin<bool>(model, "+1");
        _set=AddPin<bool>(model, "Set");
        _val=AddPin<int>(model, "Value");
        _reset=AddPin<bool>(model, "Reset");
        _dec=AddPin<bool>(model, "-1");
        _out=AddPin<int>(model, "Out");
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

    private class Average : IStatement {
      static Average() {
        var t1=Topic.root.Get<string>("/system/declarers/Average");
        t1.value="/CC;component/Images/ar_avr.png";
        t1.Get<string>("In").value="Ac";
        t1.Get<string>("Stb").value="Bz";
        t1.Get<string>("Out").value="az";
        t1.Get<string>("rename").value="|R";
        t1.Get<string>("remove").value="}D";
      }

      private DVar<Decimal> _in, _out;
      private DVar<bool> _strobe;
      private DVar<uint> _capacity;
      private Queue<Decimal> _buf;
      public void Init(DVar<PiStatement> model) {
        _in=AddPin<Decimal>(model, "In");
        _strobe=AddPin<bool>(model, "Stb");
        _capacity=AddPin<uint>(model, "_period");
        _out=AddPin<Decimal>(model, "Out");
        _out.value=_in.value;
        _buf=new Queue<decimal>();
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

    private class Switch : IStatement {
      static Switch() {
        var t1=Topic.root.Get<string>("/system/declarers/Switch");
        t1.value="/CC;component/Images/ar_switch.png";
        t1.Get<string>("Sel").value="Ai";
        t1.Get<string>("0").value="Bc";
        t1.Get<string>("1").value="Cc";
        t1.Get<string>("2").value="Dc";
        t1.Get<string>("3").value="Ec";
        t1.Get<string>("4").value="Fc";
        t1.Get<string>("5").value="Gc";
        t1.Get<string>("6").value="Hc";
        t1.Get<string>("Out").value="bc";
        t1.Get<string>("rename").value="|R";
        t1.Get<string>("remove").value="}D";
      }

      public void Init(DVar<PiStatement> model) {
        AddPin<byte>(model, "Sel");
        AddPin<Decimal>(model, "0");
        AddPin<decimal>(model, "1");
        AddPin<decimal>(model, "Out");
      }

      public void Calculate(DVar<PiStatement> model, Topic source) {
        string sel=model.Get<byte>("Sel").value.ToString();
        DVar<decimal> inp;
        if(model.Exist(sel) && (inp=model.Get<Decimal>(sel))!=null) {
          model.Get<decimal>("Out").value=inp.value;
        }
      }

      public void DeInit() {
      }
    }
    private class Pile : IStatement {
      static Pile() {
        var t1=Topic.root.Get<string>("/system/declarers/Pile");
        t1.value="/CC;component/Images/ar_pile.png";
        t1.Get<string>("Push").value="Az";
        t1.Get<string>("A").value="Bc";
        t1.Get<string>("B").value="Cc";
        t1.Get<string>("C").value="Dc";
        t1.Get<string>("D").value="Ec";
        t1.Get<string>("E").value="Fc";
        t1.Get<string>("F").value="Gc";
        t1.Get<string>("G").value="Hc";
        t1.Get<string>("rename").value="|R";
        t1.Get<string>("remove").value="}D";
      }
      public void Init(DVar<PiStatement> model) {
        AddPin<Decimal>(model, "A");
        AddPin<bool>(model, "Push");
        AddPin<string>(model, "_id_A");
        AddPin<string>(model, "_id_B");
        AddPin<string>(model, "_id_C");
        AddPin<string>(model, "_id_D");
        AddPin<string>(model, "_id_E");
        AddPin<string>(model, "_id_F");
        AddPin<string>(model, "_id_G");
        AddPin<string>(model, "_fileName");
        AddPin<uint>(model, "_capacity");
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

          uint cap=model.Get<uint>("_capacity").value;
          string header="DT";
          string cur=DateTime.Now.ToString(model.Get<string>("_XFormat").value);
          string valS;
          foreach(var inp in model.children.Where(z => z is DVar<decimal> && z.name.Length==1 && z.name[0]>='A' && z.name[0]<='G').Cast<DVar<Decimal>>()) {
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
    private class Cosm : IStatement {
      static Cosm() {
        var t1=Topic.root.Get<string>("/system/declarers/Cosm");
        t1.value="/CC;component/Images/fu_cosm.png";
        t1.Get<string>("Push").value="Az";
        t1.Get<string>("A").value="Bc";
        t1.Get<string>("B").value="Cc";
        t1.Get<string>("C").value="Dc";
        t1.Get<string>("D").value="Ec";
        t1.Get<string>("E").value="Fc";
        t1.Get<string>("F").value="Gc";
        t1.Get<string>("G").value="Hc";
        t1.Get<string>("rename").value="|R";
        t1.Get<string>("remove").value="}D";
      }

      private DVar<bool> _push;
      private DVar<string> _feed;
      private DVar<string> _key;
      public void Init(DVar<PiStatement> model) {
        AddPin<Decimal>(model, "A");
        _push=AddPin<bool>(model, "Push");
        _feed=AddPin<string>(model, "_feed");
        _key=AddPin<string>(model, "_key");
      }

      public void Calculate(DVar<PiStatement> model, Topic source) {
        if(!string.IsNullOrEmpty(_feed.value) && !string.IsNullOrEmpty(_key.value) && source==_push && _push.value) {
          StringBuilder sb=new StringBuilder();
          foreach(var inp in model.children.Where(z => z is DVar<decimal> && z.name.Length==1 && z.name[0]>='A' && z.name[0]<='G').Cast<DVar<Decimal>>()) {
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
        }
      }
      private void Send(string apiKey, string feedId, string sample) {
        try {
          byte[] buffer = Encoding.UTF8.GetBytes(sample);

          var request = (HttpWebRequest)WebRequest.Create("http://api.cosm.com/v2/feeds/" + feedId + ".csv");

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

    private class Sun : IStatement {
      static Sun() {
        var t1=Topic.root.Get<string>("/system/declarers/Sun");
        t1.value="/CC;component/Images/fu_sun.png";
        t1.Get<string>("Lat").value="Ac";
        t1.Get<string>("Lon").value="Bc";
        t1.Get<string>("Sunrise").value="ac";
        t1.Get<string>("Out").value="bz";
        t1.Get<string>("Sunset").value="cc";
        t1.Get<string>("rename").value="|R";
        t1.Get<string>("remove").value="}D";
      }

      private DVar<Decimal> _dLat;
      private DVar<Decimal> _dLon;
      private DVar<bool> _dOut;
      private DVar<DateTime> _dSunrise;
      private DVar<DateTime> _dSunset;

      private Timer _evnt;

      public void Init(DVar<PiStatement> model) {
        _dLat=AddPin<Decimal>(model, "Lat");
        _dLon=AddPin<Decimal>(model, "Lon");
        _dSunrise=AddPin<DateTime>(model, "Sunrise");
        _dSunset=AddPin<DateTime>(model, "Sunset");
        _dOut=AddPin<bool>(model, "Out");
        _evnt=new Timer((o) => Calculate(null, null));
      }

      public void Calculate(DVar<PiStatement> model, Topic source) {
        var DR = Math.PI / 180;
        var RD = 1 / DR;
        var B5 = (double)_dLat.value;
        var L5 = (double)_dLon.value;
        var Now = DateTime.Now;
        var H =  TimeZone.CurrentTimeZone.IsDaylightSavingTime(Now)?0:1;
        B5 = DR * B5;
        var N = (Int32)(275 * Now.Month / 9) - 2 * (Int32)((Now.Month + 9) / 12) + Now.Day - 30;
        var L0 = 4.8771 + .0172 * (N + .5 - L5 / 360);
        var C = .03342 * Math.Sin(L0 + 1.345);
        var C2 = RD * (Math.Atan(Math.Tan(L0 + C)) - Math.Atan(.9175 * Math.Tan(L0 + C)) - C);
        var SD = .3978 * Math.Sin(L0 + C);
        var CD = Math.Sqrt(1 - SD * SD);
        var SC = (SD * Math.Sin(B5) + .0145) / (Math.Cos(B5) * CD);

        if(Math.Abs(SC) <= 1) {
          // calculate sunrise 
          var C3 = RD * Math.Atan(SC / Math.Sqrt(1 - SC * SC));
          var R1 = 7 - ((L5%15) + C2 + C3) / 15;
          _dSunrise.value  = TimeZone.CurrentTimeZone.ToLocalTime(Now.Date.AddHours(R1));
          // calculate sunset
          var S1 = 19 - ((L5%15) + C2 - C3) / 15;
          _dSunset.value = TimeZone.CurrentTimeZone.ToLocalTime(Now.Date.AddHours(S1));
        } else {
          if(SC > 1) {
            // sun is up all day ...
            // Set Sunset to be in the future ...
            _dSunset.value = Now.Date.AddDays(1);
            // Set Sunrise to be in the past ...
            _dSunrise.value = Now.Date;
          }
          if(SC < -1) {
            // sun is down all day ...
            // Set Sunrise and Sunset to be in the future ...
            _dSunrise.value = Now.Date.AddDays(1);
            _dSunset.value = Now.AddDays(2);
          }
        }
        if(Now<_dSunrise.value) {
          _dOut.value=false;
          _evnt.Change(_dSunrise.value.AddSeconds(2)-Now, TimeSpan.FromDays(1));
        } else if(Now<_dSunset.value) {
          _dOut.value=true;
          _evnt.Change(_dSunset.value.AddSeconds(2)-Now, TimeSpan.FromDays(1));
        } else {
          _dOut.value=false;
          _evnt.Change(_dSunrise.value.AddHours(23)-Now, TimeSpan.FromDays(1));
        }
      }

      public void DeInit() {
        _evnt.Change(Timeout.Infinite, Timeout.Infinite);
      }

    }

    private class MathOp : IStatement {
      static MathOp() {
        FillDecl(Topic.root.Get<string>("/system/declarers/Sum"), "ar_sum");
        FillDecl(Topic.root.Get<string>("/system/declarers/Sub"), "ar_sub");
        FillDecl(Topic.root.Get<string>("/system/declarers/Mul"), "ar_mul");
        FillDecl(Topic.root.Get<string>("/system/declarers/Div"), "ar_div");
        FillDecl(Topic.root.Get<string>("/system/declarers/Remainder"), "ar_mod");
      }
      private static void FillDecl(DVar<string> t1, string icon) {
        t1.value="/CC;component/Images/"+icon+".png";
        t1.Get<string>("A").value="Ac";
        t1.Get<string>("B").value="Bc";
        t1.Get<string>("C").value="Cc";
        t1.Get<string>("D").value="Dc";
        t1.Get<string>("E").value="Ec";
        t1.Get<string>("F").value="Fc";
        t1.Get<string>("G").value="Gc";
        t1.Get<string>("H").value="Hc";
        t1.Get<string>("Q").value="ac";
        t1.Get<string>("rename").value="|R";
        t1.Get<string>("remove").value="}D";
      }

      public void Init(DVar<PiStatement> model) {
        AddPin<decimal>(model, "A");
        AddPin<decimal>(model, "B");
        AddPin<decimal>(model, "Q");
      }

      public void Calculate(DVar<PiStatement> model, Topic source) {
        decimal ret=model.Get<decimal>("A");
        string op=model.Get<string>("_declarer");
        foreach(DVar<decimal> pin in model.children.Where(z => (z.name.Length==1 && z.valueType==typeof(decimal) && z.name[0]>'A' && z.name[0]<='H')).Cast<DVar<decimal>>()) {
          switch(op) {
          case "Sum":
            ret+=pin.value;
            break;
          case "Sub":
            ret-=pin.value;
            break;
          case "Mul":
            ret*=pin.value;
            break;
          case "Div":
            if(pin.value!=0) {
              ret/=pin.value;
            } else {
              ret=decimal.MaxValue;
            }
            break;
          case "Remainder":
            if(pin.value!=0) {
              ret%=pin.value;
            }
            break;
          }
        }
        DVar<decimal> outp=model.Get<decimal>("Q");
        outp.saved=true;
        outp.value=ret;
      }

      public void DeInit() {
      }
    }

    private class StrFormat : IStatement {
      static StrFormat() {
        var t1=Topic.root.Get<string>("/system/declarers/StrFormat");
        t1.value="/CC;component/Images/st_fmt.png";
        t1.Get<string>("0").value="Ao";
        t1.Get<string>("1").value="Bo";
        t1.Get<string>("2").value="Co";
        t1.Get<string>("3").value="Do";
        t1.Get<string>("4").value="Eo";
        t1.Get<string>("5").value="Fo";
        t1.Get<string>("6").value="Go";
        t1.Get<string>("7").value="Ho";
        t1.Get<string>("O").value="ao";
        t1.Get<string>("rename").value="|R";
        t1.Get<string>("remove").value="}D";
      }
      private DVar<string> _dFmt;
      private DVar<string> _out;

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

    private class MathExpr : IStatement {
      static MathExpr() {
        var t1=Topic.root.Get<string>("/system/declarers/MathExpr");
        t1.value="/CC;component/Images/ty_func.png";
        t1.Get<string>("A").value="Ag";
        t1.Get<string>("B").value="Bg";
        t1.Get<string>("C").value="Cg";
        t1.Get<string>("D").value="Dg";
        t1.Get<string>("E").value="Eg";
        t1.Get<string>("F").value="Fg";
        t1.Get<string>("G").value="Gg";
        t1.Get<string>("H").value="Hg";
        t1.Get<string>("Out").value="ag";
        t1.Get<string>("rename").value="|R";
        t1.Get<string>("remove").value="}D";
      }
      private DVar<PiStatement> _model;
      private Eval eval;
      private DVar<string> _dFunc;
      private DVar<double> _dOut;

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

    private class Execute : IStatement {
      static Execute() {
        var t1=Topic.root.Get<string>("/system/declarers/Execute");
        t1.value="/CC;component/Images/fu_exec.png";
        t1.Get<string>("process").value="As";
        t1.Get<string>("arguments").value="Bs";
        t1.Get<string>("start").value="Cz";
        t1.Get<string>("rename").value="|R";
        t1.Get<string>("remove").value="}D";
      }

      private DVar<string> _proc;
      private DVar<string> _args;
      private DVar<bool> _start;
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
          var proc=System.Diagnostics.Process.Start(_proc.value, _args.value);
          Log.Info("Execute({0}, {1})", _proc.value, _args.value);
        }
        catch(Exception ex) {
          Log.Warning("Execute({0}, {1}) - {2}", _proc.value, _args.value, ex.Message);
        }
      }

      public void DeInit() {
      }
    }
    private class Breaker : IStatement {
      static Breaker() {
        var t1=Topic.root.Get<string>("/system/declarers/Breaker");
        t1.value="/CC;component/Images/ar_breaker.png";
        t1.Get<string>("In").value="Ac";
        t1.Get<string>("OE").value="Bz";
        t1.Get<string>("Out").value="ac";
        t1.Get<string>("rename").value="|R";
        t1.Get<string>("remove").value="}D";
      }
      DVar<Decimal> _in;
      DVar<Decimal> _out;
      DVar<bool> _oe;
      public void Init(DVar<PiStatement> model) {
        _in=AddPin<decimal>(model, "In");
        _oe=AddPin<bool>(model, "OE");
        _out=AddPin<decimal>(model, "Out");
      }

      public void Calculate(DVar<PiStatement> model, Topic source) {
        if(_oe.value) {
          _out.SetValue(_in.value, new TopicChanged(TopicChanged.ChangeArt.Value, model));
        }
      }

      public void DeInit() {
      }
    }
    private class BreakerO : IStatement {
      static BreakerO() {
        var t1=Topic.root.Get<string>("/system/declarers/BreakerO");
        t1.value="/CC;component/Images/ar_breaker.png";
        t1.Get<string>("In").value="Ao";
        t1.Get<string>("OE").value="Bz";
        t1.Get<string>("Out").value="ao";
        t1.Get<string>("rename").value="|R";
        t1.Get<string>("remove").value="}D";
      }
      DVar<object> _in;
      DVar<object> _out;
      DVar<bool> _oe;
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
  }
}
