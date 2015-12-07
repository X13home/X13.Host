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
using System.Collections;

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
	  DVar<bool> _a, _q;
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
		_a=AddPin<bool>(model, "A");
		_q=AddPin<bool>(model, "Q");
	  }

	  public void Calculate(DVar<PiStatement> model, Topic source) {
		_q.saved=false;
		_q.value=!_a.value;
	  }

	  public void DeInit() {
	  }
	}

	[Export(typeof(IStatement))]
	[ExportMetadata("declarer", "DTriger")]
	private class DTriger : IStatement {
	  private bool _st;
	  private DVar<bool> _s, _r, _c, _d, _q, _nq;
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
		_s=AddPin<bool>(model, "S");
		_c=AddPin<bool>(model, "C");
		_d=AddPin<bool>(model, "D");
		_r=AddPin<bool>(model, "R");
		_q=AddPin<bool>(model, "Q");
		_nq=AddPin<bool>(model, "!Q");
		_nq.value=!_q.value;
	  }

	  public void Calculate(DVar<PiStatement> model, Topic source) {
		if(_r.value) {
		  _st=false;
		} else if(_s.value) {
		  _st=true;
		} else if(source==_c && _c.value) {
		  _st=_d.value;
		}
		_q.saved=true;
		_q.value=_st;
		_nq.value=!_st;
	  }

	  public void DeInit() {
	  }
	}

	[Export(typeof(IStatement))]
	[ExportMetadata("declarer", "ANDI")]
	private class AndI : IStatement {
	  DVar<long> _q;
	  DVar<bool> _nq;
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
		m.Get<string>("N").value="bz";
		m.Get<string>("rename").value="|R";
		m.Get<string>("remove").value="}D";
	  }

	  public void Init(DVar<PiStatement> model) {
		AddPin<long>(model, "A");
		AddPin<long>(model, "B");
		_q=AddPin<long>(model, "Q");
		_nq=AddPin<bool>(model, "N");
	  }

	  public void Calculate(DVar<PiStatement> model, Topic source) {
		long ret=-1;  // 0xFFFFFFFF
		foreach(DVar<long> pin in model.children.Where(z => (z.name.Length==1 && z.valueType==typeof(long) && z.name[0]>='A' && z.name[0]<='H')).Cast<DVar<long>>()) {
		  ret&=pin.value;
		}
		_q.value=ret;
		_nq.value=(ret & 1)==0;
	  }

	  public void DeInit() {
	  }
	}

	[Export(typeof(IStatement))]
	[ExportMetadata("declarer", "ORI")]
	private class OrI : IStatement {
	  DVar<long> _q;
	  DVar<bool> _nq;
	  public void Load() {
		var m=Topic.root.Get<string>("/etc/declarers/func/ORI");
		m.value="pack://application:,,/CC;component/Images/bi_or.png";
		m.Get<string>("_description").value="c2Bitwise OR";
		m.Get<string>("_proto").value="ANDI";
	  }

	  public void Init(DVar<PiStatement> model) {
		AddPin<long>(model, "A");
		AddPin<long>(model, "B");
		_q=AddPin<long>(model, "Q");
		_nq=AddPin<bool>(model, "N");
	  }

	  public void Calculate(DVar<PiStatement> model, Topic source) {
		long ret=0;
		foreach(DVar<long> pin in model.children.Where(z => (z.name.Length==1 && z.valueType==typeof(long) && z.name[0]>='A' && z.name[0]<='H')).Cast<DVar<long>>()) {
		  ret|=pin;
		}
		_q.value=ret;
		_nq.value=(ret & 1)==0;
	  }

	  public void DeInit() {
	  }
	}

	[Export(typeof(IStatement))]
	[ExportMetadata("declarer", "XORI")]
	private class XorI : IStatement {
	  DVar<long> _q;
	  DVar<bool> _nq;
	  public void Load() {
		var m=Topic.root.Get<string>("/etc/declarers/func/XORI");
		m.value="pack://application:,,/CC;component/Images/bi_xor.png";
		m.Get<string>("_description").value="c3Bitwise XOR";
		m.Get<string>("_proto").value="ANDI";
	  }

	  public void Init(DVar<PiStatement> model) {
		AddPin<long>(model, "A");
		AddPin<long>(model, "B");
		_q=AddPin<long>(model, "Q");
		_nq=AddPin<bool>(model, "N");
	  }

	  public void Calculate(DVar<PiStatement> model, Topic source) {
		long ret=0;
		foreach(DVar<long> pin in model.children.Where(z => (z.name.Length==1 && z.valueType==typeof(long) && z.name[0]>='A' && z.name[0]<='H')).Cast<DVar<long>>()) {
		  ret^=pin;
		}
		_q.value=ret;
		_nq.value=(ret & 1)==0;
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
		m.Get<string>("_proto").value="SHL";
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
		if(_reset.value) {
		  _out.value=0;
		} else if(_set.value) {
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
		t1.Get<string>("_description").value="f Greater";
		t1.Get<string>("_proto").value="comp_eq";
	  }
	  public void Init(DVar<PiStatement> model) {
		_a=AddPin<double>(model, "A");
		_b=AddPin<double>(model, "B");
		AddPin<bool>(model, "Q");
		AddPin<bool>(model, "!Q");
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
		t1.Get<string>("_description").value="f Less";
		t1.Get<string>("_proto").value="comp_eq";
	  }
	  public void Init(DVar<PiStatement> model) {
		_a=AddPin<double>(model, "A");
		_b=AddPin<double>(model, "B");
		AddPin<bool>(model, "Q");
		AddPin<bool>(model, "!Q");
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
		catch(NullReferenceException ex) {
		  Log.Debug("{0}.calculate - {1}", model.path, ex.Message);
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
	[ExportMetadata("declarer", "PID")]
	private class PID : IStatement {
	  private DVar<double> _pv, _sp, _kp, _ki, _kd, _ui, _u, _uMax, _uMin;
	  private DVar<bool> _auto;
	  private DVar<long> _t;
	  private double _prev;
	  private double[] _hist;
	  private Timer _ct;
	  private DateTime _pt;
	  private DateTime _ptI;
	  private int _cnt;
	  private int _cntMax;
	  private bool _gt;    // top
	  private bool _gb;    // bottom

	  public void Load() {
		var m=Topic.root.Get<string>("/etc/declarers/func/PID");
		m.value="pack://application:,,/CC;component/Images/ar_pid.png";
		m.Get<string>("_description").value="g PID controller";
		m.Get<string>("PV").value="Ag";
		m.Get<string>("SP").value="Bg";
		m.Get<string>("Top").value="Cz";
		m.Get<string>("Bottom").value="Dz";
		m.Get<string>("U").value="ag";
		m.Get<string>("rename").value="|R";
		m.Get<string>("remove").value="}D";
	  }

	  public void Init(DVar<PiStatement> model) {
		_hist=new double[9];
		_pv=AddPin<double>(model, "PV");
		_sp=AddPin<double>(model, "SP");
		_ui=AddPin<double>(model, "_iSum");
		_ui.saved=true;
		_u=AddPin<double>(model, "U");
		_u.saved=false;
		_kp=AddPin<double>(model, "_Kp");
		_ki=AddPin<double>(model, "_Ki");
		_kd=AddPin<double>(model, "_Kd");
		bool t_p=model.Exist("_t");
		_t=AddPin<long>(model, "_t");
		if(!t_p) {
		  _t.value=1000;
		}
		t_p=model.Exist("_uMax");
		_uMax=AddPin<double>(model, "_uMax");
		if(!t_p) {
		  _uMax.value=255.0;
		}
		t_p=model.Exist("_uMin");
		_uMin=AddPin<double>(model, "_uMin");
		if(!t_p) {
		  _uMin.value=-255.0;
		}
		_auto=AddPin<bool>(model, "_auto");
		_ct=new Timer(Pool);
		if(_t.value>0) {
		  int t=(int)Math.Sqrt(_t.value/250);
		  _cntMax=t<2?1:t;
		  _ct.Change(_t.value, _t.value/_cntMax);
		}
		_pt=DateTime.Now;
		_ptI=_pt;
		_prev=_pv.value;
		for(int i=0;i<9;i++) {
		  _hist[i]=_pv.value;
		}
	  }

	  public void Calculate(DVar<PiStatement> model, Topic source) {
		if(source==_u  || source==_pv) {
		  return;
		} else if(source==_t) {
		  if(_t.value>0) {
			int t=(int)Math.Sqrt(_t.value/250);
			_cntMax=t<2?1:t;
			_ct.Change(50, _t.value/_cntMax);
		  } else {
			_ct.Change(Timeout.Infinite, Timeout.Infinite);
		  }
		} else if(_t.value>0 && _cntMax>0 && (source==_sp || source==_kp || source==_ki || source==_kd)) {
		  _ct.Change(1, _t.value/_cntMax);
		  _cnt=_cntMax;
		} else if(source==_auto && _auto.value==true) {

		} else {
		  Topic tt;
		  DVar<bool> bt;
		  _gt=model.Exist("Top", out tt) && (bt=tt as DVar<bool>)!=null && bt.value;
		  _gb=model.Exist("Bottom", out tt) && (bt=tt as DVar<bool>)!=null && bt.value;
		}
	  }
	  public void DeInit() {
		_ct.Change(Timeout.Infinite, Timeout.Infinite);
	  }
	  private void Pool(object o) {
		_cnt++;
		var now=DateTime.Now;
		var e=_sp.value-_pv.value;
		var dtI=(now-_ptI).TotalSeconds;
		double uIntegral=_ui.value;
		if(_ki.value!=0) {
		  var ic=e*dtI*_ki.value;
		  if((ic>0 && _gt) || (ic<0 && _gb)) {
			ic=0;
		  } else {
			uIntegral+=ic;
			if(uIntegral>_uMax.value) {
			  uIntegral=_uMax.value;
			} else if(uIntegral<_uMin.value) {
			  uIntegral=_uMin.value;
			}
		  }
		} else {
		  uIntegral=0;
		}
		_ui.value=uIntegral;
		for(int i=1;i<9;i++) {
		  _hist[i]=_hist[i-1];
		}
		_hist[0]=_pv.value;
		_ptI=now;
		if(_cnt>=_cntMax) {
		  _cnt=0;
		  var dt=(now-_pt).TotalSeconds;
		  double d=(_hist[0]+_hist[1]+_hist[2])/2 - (_hist[3]+_hist[4]+_hist[5])/4.5 + (_hist[6]+_hist[7]+_hist[8])/18;
		  e=_sp.value-d;
		  double rez=_kp.value*e+uIntegral+_kd.value*(_prev-d)/dt;
		  if(rez>_uMax.value) {
			rez=_uMax.value;
		  } else if(rez<_uMin.value) {
			rez=_uMin.value;
		  }
		  _prev=(d+_prev)/2;
		  _u.value=rez;
		  _pt=now;
		}
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
		m.Get<string>("_proto").value="Sum";
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
		m.Get<string>("_proto").value="Sum";
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
		m.Get<string>("_proto").value="Sum";
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
		m.Get<string>("_proto").value="Sum";
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
		for(int i=0;i<8;i++) {
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
	[ExportMetadata("declarer", "OnOffDelay")]
	private class OnOffDelay : IStatement {
	  private DVar<bool> _input;
	  private DVar<bool> _output;
	  private DVar<bool> _iOutput;
	  private DVar<long> _onDelay;
	  private DVar<long> _offDelay;
	  private int _state;
	  private Timer _timer;

	  public void Load() {
		var t1=Topic.root.Get<string>("/etc/declarers/func/OnOffDelay");
		t1.value="pack://application:,,/CC;component/Images/bi_delay.png";
		t1.Get<string>("In").value="Az";
		t1.Get<string>("Q").value="az";
		t1.Get<string>("!Q").value="bz";

		t1.Get<string>("_description").value="t1OnOffDelay";

		t1.Get<string>("rename").value="|R";
		t1.Get<string>("remove").value="}D";
	  }

	  public void Init(DVar<PiStatement> model) {
		_input=AddPin<bool>(model, "In");
		_output=AddPin<bool>(model, "Q");
		_iOutput=AddPin<bool>(model, "!Q");
		_onDelay=AddPin<long>(model, "_onDelay");
		_offDelay=AddPin<long>(model, "_offDelay");
		model.Get<bool>("!Q").value=!model.Get<bool>("Q").value;
		_state=0;
		_timer=new Timer((o) => {
		  if(_state==1 || _state==3) {
			_state++;
		  }
		  process();
		}, null, Timeout.Infinite, Timeout.Infinite);
	  }
	  public void Calculate(DVar<PiStatement> model, Topic source) {
		if(_input==source) {
		  if(_input.value) {
			if(_onDelay.value>0) {
			  _state=1;
			} else {
			  _state=2;
			}
		  } else {
			if(_state==2 && _offDelay.value>0) {
			  _state=3;
			} else {
			  _state=0;
			}
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
		  rez=false;
		  _timer.Change(_onDelay.value, Timeout.Infinite);
		  break;
		case 2:
		  rez=true;
		  _timer.Change(Timeout.Infinite, Timeout.Infinite);
		  break;
		case 3:
		  rez=true;
		  _timer.Change(_offDelay.value==0?1:_offDelay.value, Timeout.Infinite);
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
	  private bool _st;

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
		_st=true;
		_output.value=_st;
		_iOutput.value=!_st;
	  }
	  private void SwitchOff(object o) {
		_st=false;
		_output.value=_st;
		_iOutput.value=!_st;
	  }
	  public void Calculate(DVar<PiStatement> model, Topic source) {
		if(source==_output || source==_iOutput) {
		  _output.value=_st;
		  _iOutput.value=!_st;
		  return;
		}
		if(!_enable.value || _offDelay.value==0) {
		  _st=false;
		  _output.value=_st;
		  _iOutput.value=!_st;
		  _timerOn.Change(Timeout.Infinite, Timeout.Infinite);
		  _timerOff.Change(Timeout.Infinite, Timeout.Infinite);
		  return;
		}
		_st=true;
		_output.value=_st;
		_iOutput.value=!_st;
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
		m.Get<string>("_description").value="v Export to xively.com";
		m.Get<string>("_proto").value="Pile";

	  }

	  public void Init(DVar<PiStatement> model) {
#pragma warning disable 0618
		if(Engine.IsLinux && ServicePointManager.CertificatePolicy==null) { // mono doesn't ship with any trusted root
		  ServicePointManager.CertificatePolicy=new AllowApi();
		}
#pragma warning restore 0618

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
	  private class AllowApi : ICertificatePolicy {
		public bool CheckValidationResult(ServicePoint srvPoint, System.Security.Cryptography.X509Certificates.X509Certificate certificate, WebRequest request, int error) {
		  if(error == 0)
			return true;
		  // only ask for trust failure (you may want to handle more cases)
		  if(error != -2146762486)
			return false;
		  if(request!=null && request.RequestUri.Host=="api.xively.com") {
			return true;
		  }
		  return false;
		}
	  }
	}

	[Export(typeof(IStatement))]
	[ExportMetadata("declarer", "NarodMon")]
	private class NarodMon : IStatement {
	  private DVar<bool> _push;
	  private DVar<string> _mac;
	  private DateTime _prev;
	  private byte _fl;
	  System.Net.Sockets.UdpClient _udp;

	  public void Load() {
		var m=Topic.root.Get<string>("/etc/declarers/func/NarodMon");
		m.value="pack://application:,,/CC;component/Images/fu_NarodMon.png";
		m.Get<string>("_description").value="v Export to narodmon.ru";
		m.Get<string>("_proto").value="Pile";
	  }

	  public void Init(DVar<PiStatement> model) {
		_fl=0;
		AddPin<double>(model, "A");
		_push=AddPin<bool>(model, "Push");
		_mac=AddPin<string>(model, "_feed");
		if(string.IsNullOrEmpty(_mac.value)) {
		  string s1=Topic.root.Get<string>("/etc/PLC/default").value.ToUpper();
		  string s2=_mac.parent.parent.name.ToUpper();
		  string s3=_mac.parent.name.ToUpper();
		  if(s3.Length>2) {
			s3=s3.Substring(s3.Length-2);
		  }
		  if(s1.Length<6) {
			s1=s1+"0";
		  }
		  s1=s1+s2;
		  if(s1.Length<12) {
			s1=s1+"0123456789AB".Substring(0, 12-s1.Length);
		  } else if(s1.Length>12) {
			s1=s1.Substring(0, 12);
		  }

		  _mac.value=s1+s3;
		}
		_udp=new System.Net.Sockets.UdpClient("narodmon.ru", 8283);
		_prev=DateTime.Now.AddMinutes(-6);
	  }

	  public void Calculate(DVar<PiStatement> model, Topic source) {
		if(source==null || !_push.value) {
		  return;
		}
		if(source==_push) {
		  _fl=0xFF;
		} else if(source.name!=null && source.name.Length==1 && source.name[0]>='A' && source.name[0]<='G') {
		  _fl|=(byte)(1<<(int)(source.name[0]-'A'));
		}
		if(_fl==0 || (DateTime.Now-_prev).TotalSeconds<299) {
		  return;
		}
		_prev=DateTime.Now;
		//#MAC[#NAME][#LAT][#LNG][#ELE]\n
		//#mac1#value1[#time1][#name1]\n
		//...
		//#macN#valueN[#timeN][#nameN]\n
		//##
		StringBuilder sb=new StringBuilder();
		sb.Append("#");
		sb.Append(_mac.value);
		foreach(var inp in model.children.Where(z => z is DVar<double> && z.name.Length==1 && z.name[0]>='A' && z.name[0]<='G').Cast<DVar<double>>()) {
		  if((_fl & (1<<(int)(inp.name[0]-'A')))!=0) {
			sb.AppendFormat("\n#{0}{1}#{2}", _mac.value, inp.name, inp.value.ToString(CultureInfo.InvariantCulture));
		  }
		}
		sb.Append("\n##");
		_fl=0;
		ThreadPool.QueueUserWorkItem((o) => Send(sb.ToString()));
	  }
	  private void Send(string sample) {
		try {
		  byte[] buffer = Encoding.UTF8.GetBytes(sample);
		  _udp.Send(buffer, buffer.Length);
		}
		catch(Exception ex) {
		  Log.Debug("NarodMon({0}) - {1}", _mac.parent.path, ex.Message);
		}
	  }

	  public void DeInit() {
		_udp=null;
	  }
	}

	[Export(typeof(IStatement))]
	[ExportMetadata("declarer", "Pile")]
	private class Pile : IStatement {
	  private Timer _saveT;
	  private DVar<PiStatement> _model;
	  public Pile() {
		_saveT=new Timer(Save);
	  }
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
		_model=model;
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
		if(push.value && _saveT!=null) {
		  _saveT.Change(100, -1);
		}
	  }

	  private void Save(object o) {
		string path=_model.Get<string>("_fileName").value;
		string[] old=null;
		if(File.Exists(path)) {
		  try {
			old=File.ReadAllLines(path);
		  }
		  catch(Exception) {
			_saveT.Change(100, -1);
			return;
		  }
		}
		if(old==null) {
		  old=new string[] { string.Empty };
		}

		long cap=_model.Get<long>("_capacity").value;
		string header="DT";
		string cur=DateTime.Now.ToString(_model.Get<string>("_XFormat").value);
		string valS;
		foreach(var inp in _model.children.Where(z => z is DVar<double> && z.name.Length==1 && z.name[0]>='A' && z.name[0]<='G').Cast<DVar<double>>()) {
		  var id=_model.Get<string>(string.Format("_id_{0}", inp.name)).value;
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
		try {
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
		catch(Exception) {
		  _saveT.Change(100, -1);
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

	[Export(typeof(IStatement))]
	[ExportMetadata("declarer", "BAInsertL")]
	private class BAInsert : IStatement {
	  private DVar<ByteArray> _in;
	  private DVar<ByteArray> _out;
	  private DVar<long> _val;
	  private DVar<long> _pos;
	  private DVar<long> _len;
	  private DVar<bool> _msbFirst;

	  public void Load() {
		var t1=Topic.root.Get<string>("/etc/declarers/func/BAInsertL");
		t1.value="pack://application:,,/CC;component/Images/fu_BAInsertL.png";
		t1.Get<string>("in").value="Ao";
		t1.Get<string>("val").value="Bi";
		t1.Get<string>("pos").value="Ci";
		t1.Get<string>("len").value="Di";
		t1.Get<string>("out").value="ao";
		t1.Get<string>("_description").value="paInsert long to byteArray";
		t1.Get<string>("rename").value="|R";
		t1.Get<string>("remove").value="}D";
	  }

	  public void Init(DVar<PiStatement> model) {
		_in=BiultInStatements.AddPin<ByteArray>(model, "in");
		_out=BiultInStatements.AddPin<ByteArray>(model, "out");
		_out.saved=false;
		_val=BiultInStatements.AddPin<long>(model, "val");
		_pos=BiultInStatements.AddPin<long>(model, "pos");
		_len=BiultInStatements.AddPin<long>(model, "len");
		_msbFirst=BiultInStatements.AddPin<bool>(model, "_msbFirst");
	  }

	  public void Calculate(DVar<PiStatement> model, Topic source) {
		if(_in==null || _out==null || _val==null || _pos==null || _len==null || _msbFirst==null || source==_out) {
		  return;
		}
		int cnt=(int)_len.value;
		if(cnt<1 || cnt>8) {
		  cnt=8;
		}
		int pos=(int)_pos.value;
		byte[] s2=BitConverter.GetBytes(_val.value).Take(cnt).ToArray();
		if(_msbFirst.value) {
		  s2=s2.Reverse().ToArray();
		}
		_out.value=new ByteArray(_in.value, s2, pos);
	  }

	  public void DeInit() {
	  }
	}
	[Export(typeof(IStatement))]
	[ExportMetadata("declarer", "BAInsertS")]
	private class BAInsertS : IStatement {
	  private DVar<ByteArray> _in;
	  private DVar<ByteArray> _out;
	  private DVar<string> _val;
	  private DVar<long> _pos;
	  private DVar<long> _len;

	  public void Load() {
		var t1=Topic.root.Get<string>("/etc/declarers/func/BAInsertS");
		t1.value="pack://application:,,/CC;component/Images/fu_BAInsertS.png";
		t1.Get<string>("_description").value="pcInsert string to byteArray";
		t1.Get<string>("_proto").value="BAInsertL";
		t1.Get<string>("val").value="Bs";
	  }

	  public void Init(DVar<PiStatement> model) {
		_in=BiultInStatements.AddPin<ByteArray>(model, "in");
		_out=BiultInStatements.AddPin<ByteArray>(model, "out");
		_out.saved=false;
		_val=BiultInStatements.AddPin<string>(model, "val");
		_pos=BiultInStatements.AddPin<long>(model, "pos");
		_len=BiultInStatements.AddPin<long>(model, "len");
	  }

	  public void Calculate(DVar<PiStatement> model, Topic source) {
		if(_in==null || _out==null || _pos==null || _len==null || source==_out) {
		  return;
		}
		int cnt=(int)_len.value;
		byte[] data=(_val==null || _val.value==null)?new byte[0]:Encoding.Default.GetBytes(_val.value);
		if(cnt<1) {
		  cnt=data.Length;
		} else if(cnt>data.Length) {
		  byte[] d2=new byte[cnt];
		  Buffer.BlockCopy(data, 0, d2, 0, data.Length);
		  data=d2;
		} else {
		  data=data.Take(cnt).ToArray();
		}
		_out.value=new ByteArray(_in.value, data, (int)_pos.value);
	  }

	  public void DeInit() {
	  }
	}

	[Export(typeof(IStatement))]
	[ExportMetadata("declarer", "BAGetL")]
	private class BAGetL : IStatement {
	  private DVar<ByteArray> _in;
	  private DVar<long> _pos;
	  private DVar<long> _len;
	  private DVar<bool> _msbFirst;
	  private DVar<long> _out;

	  public void Load() {
		var t1=Topic.root.Get<string>("/etc/declarers/func/BAGetL");
		t1.value="pack://application:,,/CC;component/Images/fu_BAGetL.png";
		t1.Get<string>("in").value="Ao";
		t1.Get<string>("pos").value="Bi";
		t1.Get<string>("len").value="Ci";
		t1.Get<string>("out").value="ai";
		t1.Get<string>("_description").value="pbGet long from byteArray";
		t1.Get<string>("rename").value="|R";
		t1.Get<string>("remove").value="}D";
	  }

	  public void Init(DVar<PiStatement> model) {
		_in=BiultInStatements.AddPin<ByteArray>(model, "in");
		_out=BiultInStatements.AddPin<long>(model, "out");
		_out.saved=false;
		_pos=BiultInStatements.AddPin<long>(model, "pos");
		_len=BiultInStatements.AddPin<long>(model, "len");
		_msbFirst=BiultInStatements.AddPin<bool>(model, "_msbFirst");
	  }

	  public void Calculate(DVar<PiStatement> model, Topic source) {
		if(_in==null || _in.value==null || _out==null || _pos==null || _len==null || _msbFirst==null || source==_out) {
		  return;
		}
		byte[] src=_in.value.GetBytes();
		int srcB=(int)_pos.value, dstB=0, cnt=(int)_len.value;

		if(cnt<1 || cnt>8) {
		  cnt=8;
		}
		byte[] dst=new byte[8];
		if(srcB<0) {
		  srcB=src.Length+srcB;
		}
		if(srcB<0) {
		  cnt+=srcB;
		  dstB-=srcB;
		  srcB=0;
		}
		if(srcB+cnt>src.Length) {
		  cnt=src.Length-srcB;
		}
		if(dstB>=0 && cnt>0 && dstB+cnt<=8) {
		  if(_msbFirst.value && cnt<8) {
			dstB+=8-cnt;
		  }
		  Buffer.BlockCopy(src, srcB, dst, dstB, cnt);
		}
		if(_msbFirst.value) {
		  dst=dst.Reverse().ToArray();
		}
		_out.value=BitConverter.ToInt64(dst, 0);
	  }

	  public void DeInit() {
	  }
	}

	[Export(typeof(IStatement))]
	[ExportMetadata("declarer", "BAGetS")]
	private class BAGetS : IStatement {
	  private DVar<ByteArray> _in;
	  private DVar<long> _pos;
	  private DVar<long> _len;
	  private DVar<string> _out;

	  public void Load() {
		var t1=Topic.root.Get<string>("/etc/declarers/func/BAGetS");
		t1.value="pack://application:,,/CC;component/Images/fu_BAGetS.png";
		t1.Get<string>("_description").value="pdGet string from byteArray";
		t1.Get<string>("_proto").value="BAGetL";
		t1.Get<string>("out").value="as";
	  }

	  public void Init(DVar<PiStatement> model) {
		_in=BiultInStatements.AddPin<ByteArray>(model, "in");
		_out=BiultInStatements.AddPin<string>(model, "out");
		_out.saved=false;
		_pos=BiultInStatements.AddPin<long>(model, "pos");
		_len=BiultInStatements.AddPin<long>(model, "len");
	  }

	  public void Calculate(DVar<PiStatement> model, Topic source) {
		if(_out==null || _pos==null || _len==null || source==_out) {
		  return;
		}
		if(_in==null || _in.value==null) {
		  _out.value=string.Empty;
		  return;
		}
		byte[] src=_in.value.GetBytes();
		int srcB=(int)_pos.value, cnt=(int)_len.value;

		if(srcB<0) {
		  srcB=src.Length+srcB;
		}

		if(cnt<1 || cnt>src.Length-srcB) {
		  cnt=src.Length-srcB;
		}
		if(srcB<0) {
		  cnt+=srcB;
		  srcB=0;
		}
		_out.value=Encoding.Default.GetString(src, srcB, cnt);
	  }

	  public void DeInit() {
	  }
	}

	[Export(typeof(IStatement))]
	[ExportMetadata("declarer", "BAGetLength")]
	private class BAGetLength : IStatement {
	  private DVar<ByteArray> _in;
	  private DVar<long> _out;

	  public void Load() {
		var t1=Topic.root.Get<string>("/etc/declarers/func/BAGetLength");
		t1.value="pack://application:,,/CC;component/Images/fu_BALength.png";
		t1.Get<string>("in").value="Ao";
		t1.Get<string>("len").value="ai";
		t1.Get<string>("_description").value="pzLength of byteArray";
		t1.Get<string>("rename").value="|R";
		t1.Get<string>("remove").value="}D";
	  }

	  public void Init(DVar<PiStatement> model) {
		_in=BiultInStatements.AddPin<ByteArray>(model, "in");
		_out=BiultInStatements.AddPin<long>(model, "len");
		_out.saved=false;
	  }

	  public void Calculate(DVar<PiStatement> model, Topic source) {
		if(_out==null || source==_out) {
		  return;
		}
		long len=0;
		if(_in!=null && _in.value!=null && _in.value.GetBytes()!=null) {
		  len=_in.value.GetBytes().Length;
		}
		_out.value=len;
	  }

	  public void DeInit() {
	  }
	}
  }
}
