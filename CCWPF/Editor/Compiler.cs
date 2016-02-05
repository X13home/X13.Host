﻿using NiL.JS;
using NiL.JS.Core;
using NiL.JS.Expressions;
using NiL.JS.Statements;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace X13.CC {
  internal partial class DP_Compiler : Visitor<DP_Compiler> {
    private static SortedList<string, DP_Type> _predefs;
    private static DVar<bool> _verbose;

    static DP_Compiler() {
      _predefs = new SortedList<string, DP_Type>();
      _predefs["Op"] = DP_Type.OUTPUT;
      _predefs["On"] = DP_Type.OUTPUT;
      _predefs["Pp"] = DP_Type.OUTPUT;
      _predefs["Pn"] = DP_Type.OUTPUT;
      _predefs["Ip"] = DP_Type.INPUT;
      _predefs["In"] = DP_Type.INPUT;
      _predefs["Av"] = DP_Type.INPUT;
      _predefs["Ai"] = DP_Type.INPUT;
      _verbose = Topic.root.Get<bool>("/etc/MQTT-SN/PLC/verbose");
    }

    private Stack<DP_Inst> _sp;
    private List<DP_Merker> _memory;
    private List<DP_Scope> _programm;
    private Stack<DP_Scope> _scope;
    private SortedSet<DP_MemBlock> _memBlocks;
    private DP_Scope cur;

    public SortedList<string, string> varList;
    public List<string> ioList;
    public event CompilerMessageCallback CMsg;
    public long StackBottom { get; private set; }
    public SortedList<uint, PLC.ByteArray> Hex;

    public DP_Compiler() {
      Hex = new SortedList<uint, PLC.ByteArray>();
    }

    public bool Parse(string code) {
      bool success = false;
      _memory = new List<DP_Merker>();
      _scope = new Stack<DP_Scope>();
      _programm = new List<DP_Scope>();
      _sp = new Stack<DP_Inst>();
      _memBlocks = new SortedSet<DP_MemBlock>();
      _memBlocks.Add(new DP_MemBlock(0, 16384));
      uint addr;
      string vName;
      try {
        ScopePush("");

        var module = new Module(code, CompilerMessageCallback, Options.SuppressConstantPropogation);
        module.Root.Visit(this);

        cur = _programm[0];
        if(cur.code.Count == 0 || cur.code[cur.code.Count - 1]._code.Length != 1 || cur.code[cur.code.Count - 1]._code[0] != (byte)DP_InstCode.RET) {
          cur.code.Add(new DP_Inst(DP_InstCode.RET));
        }
        varList = new SortedList<string, string>();
        ioList = new List<string>();
        uint mLen;

        foreach(var m in _memory) {
          switch(m.type) {
          case DP_Type.BOOL:
            vName = "Mz";
            mLen = 1;
            break;
          case DP_Type.SINT8:
            vName = "Mb";
            mLen = 8;
            break;
          case DP_Type.SINT16:
            vName = "Mw";
            mLen = 16;
            break;
          case DP_Type.SINT32:
            vName = "Md";
            mLen = 32;
            break;
          case DP_Type.UINT8:
            vName = "MB";
            mLen = 8;
            break;
          case DP_Type.UINT16:
            vName = "MW";
            mLen = 16;
            break;
          case DP_Type.INPUT:
          case DP_Type.OUTPUT:
            ioList.Add(m.vd.Name);
            continue;
          default:
            continue;
          }
          if(m.Addr == uint.MaxValue) {
            m.Addr = AllocateMemory(uint.MaxValue, mLen) / mLen;
          }
          varList[m.vd.Name] = vName + m.Addr.ToString();
        }

        addr = 0;
        SortedList<uint, PLC.ByteArray> HexN = new SortedList<uint, PLC.ByteArray>();

        foreach(var p in _programm) {
          addr += (32 - (addr % 32)) % 32;
          if(p.entryPoint != null) {
            p.entryPoint.Addr = addr;
          }
          foreach(var c in p.code) {
            c.addr = addr;
            addr += (uint)c._code.Length;
          }
        }
        List<byte> bytes = new List<byte>();
        foreach(var p in _programm) {
          foreach(var c in p.code) {
            c.Link();
            if(c._code.Length > 0) {
              bytes.AddRange(c._code);
            }
          }
          HexN[p.code.First().addr] = new PLC.ByteArray(bytes.ToArray()); // { Titel = p.name };
          if(_verbose.value) {
            Log.Debug("{0}", p.ToString());
          }
          bytes.Clear();
        }
        Hex = HexN;
        StackBottom=_memBlocks.Last().start / 8;
        Log.Info("Used ROM: {0} bytes, RAM: {1} bytes", Hex.Select(z=>z.Key+z.Value.GetBytes().Length).Max(), StackBottom);
        success = true;
      }
      catch(JSException ex) {
        var syntaxError = ex.Error.Value as NiL.JS.BaseLibrary.SyntaxError;
        if(syntaxError != null) {
          Log.Error("{0}", syntaxError.message);
        } else {
          Log.Error("Compile - {0}: {1}", ex.GetType().Name, ex.Message);
        }
      }
      catch(Exception ex) {
        Log.Error("Compile - {0}: {1}", ex.GetType().Name, ex.Message);
      }
      _scope = null;
      _programm = null;
      _sp = null;

      return success;
    }

    private uint AllocateMemory(uint addr, uint length) {
      DP_MemBlock fb;
      uint start, end;

      if(addr == uint.MaxValue) {
        int o;
        if(length >= 16) {
          o = 32;
        } else if(length > 8) {
          o = 16;
        } else if(length > 1) {
          o = 8;
        } else {
          o = 1;
        }

        fb = _memBlocks.FirstOrDefault(z => z.Check(length, o));
        if(fb == null) {
          throw new ArgumentOutOfRangeException("Not enough memory");
        }
        _memBlocks.Remove(fb);
        start = (uint)(fb.start + (o - (fb.start % o)) % o);
        end = start + length - 1;
        if(fb.start < start) {
          _memBlocks.Add(new DP_MemBlock(fb.start, start - 1));
        }
        if(fb.end > end) {
          _memBlocks.Add(new DP_MemBlock(end + 1, fb.end));
        }
      } else {
        start = addr;
        end = addr + length - 1;
        do {
          fb = _memBlocks.FirstOrDefault(z => z.start <= end && z.end >= start);
          if(fb == null) {
            break;
          }
          _memBlocks.Remove(fb);
          if(fb.start < start) {
            _memBlocks.Add(new DP_MemBlock(fb.start, start - 1));
          }
          if(fb.end > end) {
            _memBlocks.Add(new DP_MemBlock(end + 1, fb.end));
          }
        } while(fb != null);
      }
      //{
      //  StringBuilder sb = new StringBuilder();
      //  sb.AppendFormat("AllocateMemory({0:X4}{2}, {1:X2})\n", start, length, addr==uint.MaxValue?"*":"");
      //  foreach(var m in _memBlocks) {
      //    sb.AppendFormat("  {0:X4}:{1:X4}\n", m.start, m.end);
      //  }
      //  Log.Info("{0}", sb.ToString());
      //}
      return start;
    }

    private void CompilerMessageCallback(MessageLevel level, CodeCoordinates coords, string message) {
      var msg = string.Format("[{0}, {1}] {2}", coords.Line, coords.Column, message);
      switch(level) {
      case MessageLevel.Error:
      case MessageLevel.CriticalWarning:
        Log.Error("{0}", msg);
        break;
      case MessageLevel.Warning:
        Log.Warning("{0}", msg);
        break;
      case MessageLevel.Recomendation:
        Log.Info("{0}", msg);
        break;
      default:
        Log.Debug("{0}", msg);
        break;
      }
      if(CMsg != null) {
        CMsg(level, coords, message);
      }
    }
    private void ScopePush(string name) {
      cur = new DP_Scope(name);
      _scope.Push(cur);
      _programm.Add(cur);
    }
    private void ScopePop() {
      _scope.Pop();
      cur = _scope.Peek();
    }
    private void LoadConstant(CodeNode node, int v) {
      DP_InstCode c;
      if(v == 0) {
        c = DP_InstCode.LDI_0;
      } else if(v > 0) {
        if(v < 256) {
          if(v == 1) {
            c = DP_InstCode.LDI_1;
          } else {
            c = DP_InstCode.LDI_U1;
          }
        } else {
          if(v < 65536) {
            c = DP_InstCode.LDI_U2;
          } else {
            c = DP_InstCode.LDI_S4;
          }
        }
      } else {
        if(v > -128) {
          if(v == -1) {
            c = DP_InstCode.LDI_M1;
          } else {
            c = DP_InstCode.LDI_S1;
          }
        } else {
          if(v > -32768) {
            c = DP_InstCode.LDI_S2;
          } else {
            c = DP_InstCode.LDI_S4;
          }
        }
      }
      var d = new DP_Inst(c, null, node);
      cur.code.Add(d);
      _sp.Push(d);
    }
    private void Store(CodeNode node, Expression e) {
      GetVariable a = e as GetVariable;
      if(a == null) {
        AssignmentOperatorCache a2 = e as AssignmentOperatorCache;
        if(a2 != null) {
          a = a2.Source as GetVariable;
        }
      }
      if(a != null) {
        var m = GetMerker(a.Descriptor);
        switch(m.type) {
        case DP_Type.BOOL:
          cur.code.Add(new DP_Inst(DP_InstCode.STM_B1_C16, m, node));
          break;
        case DP_Type.UINT8:
        case DP_Type.SINT8:
          cur.code.Add(new DP_Inst(DP_InstCode.STM_S1_C16, m, node));
          break;
        case DP_Type.UINT16:
        case DP_Type.SINT16:
          cur.code.Add(new DP_Inst(DP_InstCode.STM_S2_C16, m, node));
          break;
        case DP_Type.SINT32:
          cur.code.Add(new DP_Inst(DP_InstCode.STM_S4_C16, m, node));
          break;
        case DP_Type.PARAMETER:
          cur.code.Add(new DP_Inst((DP_InstCode)(DP_InstCode.ST_P0 + (byte)m.Addr), null, node));
          break;
        case DP_Type.LOCAL:
          cur.code.Add(new DP_Inst((DP_InstCode)(DP_InstCode.ST_L0 + (byte)m.Addr), null, node));
          break;
        case DP_Type.OUTPUT:
          cur.code.Add(new DP_Inst(DP_InstCode.OUT, m, node));
          break;
        default:
          throw new NotImplementedException(node.ToString());
        }
        _sp.Pop();
      } else {
        throw new NotImplementedException(node.ToString());
      }
    }
    private void AddCommon(CodeNode node, Expression a, Expression b) {
      var c1 = a as Constant;
      var c2 = b as Constant;
      DP_Inst d = null;
      if(c1 != null && c2 != null) {
        LoadConstant(node, (int)(c1.Value.Value) + (int)(c2.Value.Value));
      } else if(c1 != null && (int)(c1.Value.Value) == 1) {
        b.Visit(this);
        _sp.Pop();
        d = new DP_Inst(DP_InstCode.INC);
      } else if(c1 != null && (int)(c1.Value.Value) == -1) {
        b.Visit(this);
        _sp.Pop();
        d = new DP_Inst(DP_InstCode.DEC);
      } else if(c2 != null && (int)(c2.Value.Value) == 1) {
        a.Visit(this);
        cur.code.Add(new DP_Inst(DP_InstCode.INC));
      } else if(c2 != null && (int)(c2.Value.Value) == -1) {
        a.Visit(this);
        _sp.Pop();
        d = new DP_Inst(DP_InstCode.DEC, null, node);
      } else {
        b.Visit(this);
        a.Visit(this);
        _sp.Pop();
        _sp.Pop();
        d = new DP_Inst(DP_InstCode.ADD);
      }
      if(d != null) {
        cur.code.Add(d);
        _sp.Push(d);
      }
    }
    private void Arg2Op(Expression node, DP_InstCode c) {
      DP_Inst d;
      node.SecondOperand.Visit(this);
      node.FirstOperand.Visit(this);
      _sp.Pop();
      _sp.Pop();
      d = new DP_Inst(c);
      cur.code.Add(d);
      _sp.Push(d);
    }
    private DP_Merker GetMerker(VariableDescriptor v) {
      DP_Merker m = null;
      m = cur.memory.FirstOrDefault(z => z.vd == v);
      if(m == null) {
        m = _memory.FirstOrDefault(z => z.vd == v);
      }
      return m;
    }
    private void SafeCodeBlock(CodeNode node, int sp=-1) {
      if(node is CodeBlock) {
        node.Visit(this);
      } else {
        if(sp < 0) {
          sp = _sp.Count;
        }
        node.Visit(this);
        while(_sp.Count > sp) {
          var d = _sp.Pop();
          if(!d.canOptimized || !cur.code.Remove(d)) {
            cur.code.Add(new DP_Inst(DP_InstCode.DROP));
          }
        }
      }
    }
  }
  internal class DP_Merker {
    public uint Addr;
    public DP_Type type;
    public VariableDescriptor vd;
    public Expression init;
    public DP_Scope scope;
    public bool initialized;
  }
  internal class DP_Scope {
    public string name;
    public List<DP_Inst> code;
    public List<DP_Merker> memory;
    public DP_Merker entryPoint;
    public Stack<DP_Loop> loops;

    public DP_Scope(string name) {
      this.name = name;
      code = new List<DP_Inst>();
      memory = new List<DP_Merker>();
      loops = new Stack<DP_Loop>();
    }
    public override string ToString() {
      var sb = new StringBuilder();
      int ls = 0;
      sb.Append(this.name).Append("\n");
      byte[] hex;
      int j;
      for(int i = 0; i < code.Count; i++) {
        var c = code[i];
        sb.Append(c.addr.ToString("X4"));
        sb.Append(" ");
        hex = c._code;
        for(j = 0; j < 5; j++) {
          if(j < hex.Length) {
            sb.Append(hex[j].ToString("X2"));
            sb.Append(" ");
          } else {
            sb.Append("   ");
          }
        }
        sb.Append("| ").Append(c.ToString());
        if(c._cn != null) {
          while((sb.Length - ls) < 46) {
            sb.Append(" ");
          }
          sb.Append("; ").Append(c._cn.ToString());
        }
        sb.Append("\r\n");
        ls = sb.Length;
      }
      return sb.ToString();
    }
  }
  internal class DP_Loop {
    public DP_Loop(int sp1, ICollection<string> labels) {
      this.sp1 = sp1;
      this.labels = labels;
      L1 = new DP_Inst(DP_InstCode.LABEL);
      L2 = new DP_Inst(DP_InstCode.LABEL);
      L3 = new DP_Inst(DP_InstCode.LABEL);
    }
    public DP_Inst L1, L2, L3;
    public int sp1, sp2;
    public ICollection<string> labels;
  }
  internal class DP_Inst {
    internal uint addr;
    internal byte[] _code;
    internal DP_Merker _param;
    internal CodeNode _cn;
    internal DP_Inst _ref;

    public bool canOptimized;

    public DP_Inst(DP_InstCode cmd, DP_Merker param = null, CodeNode cn = null) {
      _param = param;
      _cn = cn;
      Prepare(cmd);
    }
    public bool Link() {
      if(_code.Length <= 1 || (_param == null && _ref == null)) {
        return true;
      }
      Prepare((DP_InstCode)_code[0]);
      return false;
    }
    private void Prepare(DP_InstCode cmd) {
      int tmp_d;
      uint tmp_D;
      switch(cmd) {
      case DP_InstCode.LABEL:
        if(_code == null || _code.Length != 0) {
          _code = new byte[0];
        }
        break;
      case DP_InstCode.NOP:
      case DP_InstCode.DUP:
      case DP_InstCode.DROP:
      case DP_InstCode.NIP:
      case DP_InstCode.SWAP:
      case DP_InstCode.OVER:
      case DP_InstCode.ROT:
      case DP_InstCode.NOT:
      case DP_InstCode.AND:
      case DP_InstCode.OR:
      case DP_InstCode.XOR:
      case DP_InstCode.ADD:
      case DP_InstCode.SUB:
      case DP_InstCode.MUL:
      case DP_InstCode.DIV:
      case DP_InstCode.MOD:
      case DP_InstCode.INC:
      case DP_InstCode.DEC:
      case DP_InstCode.NEG:
      case DP_InstCode.CEQ:
      case DP_InstCode.CNE:
      case DP_InstCode.CGT:
      case DP_InstCode.CGE:
      case DP_InstCode.CLT:
      case DP_InstCode.CLE:
      case DP_InstCode.NOT_L:
      case DP_InstCode.AND_L:
      case DP_InstCode.OR_L:
      case DP_InstCode.XOR_L:
      case DP_InstCode.LDI_0:
      case DP_InstCode.LDI_1:
      case DP_InstCode.LDI_M1:
      case DP_InstCode.LD_P0:
      case DP_InstCode.LD_P1:
      case DP_InstCode.LD_P2:
      case DP_InstCode.LD_P3:
      case DP_InstCode.LD_P4:
      case DP_InstCode.LD_P5:
      case DP_InstCode.LD_P6:
      case DP_InstCode.LD_P7:
      case DP_InstCode.LD_P8:
      case DP_InstCode.LD_P9:
      case DP_InstCode.LD_PA:
      case DP_InstCode.LD_PB:
      case DP_InstCode.LD_PC:
      case DP_InstCode.LD_PD:
      case DP_InstCode.LD_PE:
      case DP_InstCode.LD_PF:
      case DP_InstCode.LD_L0:
      case DP_InstCode.LD_L1:
      case DP_InstCode.LD_L2:
      case DP_InstCode.LD_L3:
      case DP_InstCode.LD_L4:
      case DP_InstCode.LD_L5:
      case DP_InstCode.LD_L6:
      case DP_InstCode.LD_L7:
      case DP_InstCode.LD_L8:
      case DP_InstCode.LD_L9:
      case DP_InstCode.LD_LA:
      case DP_InstCode.LD_LB:
      case DP_InstCode.LD_LC:
      case DP_InstCode.LD_LD:
      case DP_InstCode.LD_LE:
      case DP_InstCode.LD_LF:
      case DP_InstCode.ST_P0:
      case DP_InstCode.ST_P1:
      case DP_InstCode.ST_P2:
      case DP_InstCode.ST_P3:
      case DP_InstCode.ST_P4:
      case DP_InstCode.ST_P5:
      case DP_InstCode.ST_P6:
      case DP_InstCode.ST_P7:
      case DP_InstCode.ST_P8:
      case DP_InstCode.ST_P9:
      case DP_InstCode.ST_PA:
      case DP_InstCode.ST_PB:
      case DP_InstCode.ST_PC:
      case DP_InstCode.ST_PD:
      case DP_InstCode.ST_PE:
      case DP_InstCode.ST_PF:
      case DP_InstCode.ST_L0:
      case DP_InstCode.ST_L1:
      case DP_InstCode.ST_L2:
      case DP_InstCode.ST_L3:
      case DP_InstCode.ST_L4:
      case DP_InstCode.ST_L5:
      case DP_InstCode.ST_L6:
      case DP_InstCode.ST_L7:
      case DP_InstCode.ST_L8:
      case DP_InstCode.ST_L9:
      case DP_InstCode.ST_LA:
      case DP_InstCode.ST_LB:
      case DP_InstCode.ST_LC:
      case DP_InstCode.ST_LD:
      case DP_InstCode.ST_LE:
      case DP_InstCode.ST_LF:
      case DP_InstCode.LDM_B1_S:
      case DP_InstCode.LDM_S1_S:
      case DP_InstCode.LDM_S2_S:
      case DP_InstCode.LDM_S4_S:
      case DP_InstCode.LDM_U1_S:
      case DP_InstCode.LDM_U2_S:
      case DP_InstCode.STM_B1_S:
      case DP_InstCode.STM_S1_S:
      case DP_InstCode.STM_S2_S:
      case DP_InstCode.STM_S4_S:
      case DP_InstCode.SJMP:
      case DP_InstCode.SCALL:
      case DP_InstCode.RET:
        if(_code == null || _code.Length != 1) {
          _code = new byte[1];
        }
        _code[0] = (byte)cmd;
        break;
      case DP_InstCode.LSL:
      case DP_InstCode.LSR:
      case DP_InstCode.ASR:
        if(_code == null || _code.Length != 2) {
          _code = new byte[2];
        }
        _code[0] = (byte)cmd;
        _code[1] = (byte)((int)((Constant)_cn).Value & 0x1F);
        break;
      case DP_InstCode.STM_B1_C16:
      case DP_InstCode.STM_S1_C16:
      case DP_InstCode.STM_S2_C16:
      case DP_InstCode.STM_S4_C16:
      case DP_InstCode.LDM_B1_C16:
      case DP_InstCode.LDM_S1_C16:
      case DP_InstCode.LDM_S2_C16:
      case DP_InstCode.LDM_S4_C16:
      case DP_InstCode.LDM_U1_C16:
      case DP_InstCode.LDM_U2_C16:
      case DP_InstCode.CALL:
        if(_code == null || _code.Length != 3) {
          _code = new byte[3];
        }
        _code[0] = (byte)cmd;
        _code[1] = (byte)_param.Addr;
        _code[2] = (byte)(_param.Addr >> 8);
        break;
      case DP_InstCode.LDI_S1:
      case DP_InstCode.LDI_U1:
        if(_code == null || _code.Length != 2) {
          _code = new byte[2];
        }
        _code[0] = (byte)cmd;
        _code[1] = (byte)((Constant)_cn).Value;
        break;
      case DP_InstCode.LDI_S2:
      case DP_InstCode.LDI_U2:
        tmp_d = (int)((Constant)_cn).Value;
        if(_code == null || _code.Length != 3) {
          _code = new byte[3];
        }
        _code[0] = (byte)cmd;
        _code[1] = (byte)tmp_d;
        _code[2] = (byte)(tmp_d >> 8);
        break;
      case DP_InstCode.LDI_S4:
        tmp_d = (int)((Constant)_cn).Value;
        if(_code == null || _code.Length != 5) {
          _code = new byte[5];
        }
        _code[0] = (byte)cmd;
        _code[1] = (byte)tmp_d;
        _code[2] = (byte)(tmp_d >> 8);
        _code[3] = (byte)(tmp_d >> 16);
        _code[4] = (byte)(tmp_d >> 24);
        break;
      case DP_InstCode.OUT:
      case DP_InstCode.IN:
        if(_code == null || _code.Length != 5) {
          _code = new byte[5];
        }
        _code[0] = (byte)cmd;
        _code[1] = (byte)_param.Addr;
        _code[2] = (byte)(_param.Addr >> 8);
        _code[3] = (byte)(_param.Addr >> 16);
        _code[4] = (byte)(_param.Addr >> 24);
        break;

      case DP_InstCode.JZ:
      case DP_InstCode.JNZ:
      case DP_InstCode.JMP:
        tmp_D = _ref == null ? uint.MaxValue : _ref.addr;
        if(_code == null || _code.Length != 3) {
          _code = new byte[3];
        }
        _code[0] = (byte)cmd;
        _code[1] = (byte)tmp_D;
        _code[2] = (byte)(tmp_D >> 8);
        break;
      default:
        throw new NotImplementedException(this.ToString());
      }
    }
    public override string ToString() {
      if(_code.Length > 0) {
        StringBuilder sb = new StringBuilder();
        sb.Append(((DP_InstCode)_code[0]).ToString());
        if(_code.Length > 1) {
          while(sb.Length < 12) {
            sb.Append(" ");
          }
          sb.Append("0x");
          for(int i = _code.Length - 1; i > 0; i--) {
            sb.Append(_code[i].ToString("X2"));
          }
        }
        return sb.ToString();
      } else {
        return null;
      }
    }
  }
  internal class DP_MemBlock : IComparable<DP_MemBlock> {
    public readonly uint start;
    public readonly uint end;

    public DP_MemBlock(uint start, uint end) {
      this.start = start;
      this.end = end;
    }
    public int CompareTo(DP_MemBlock other) {
      return other == null ? int.MaxValue : this.start.CompareTo(other.start);
    }
    public bool Check(uint length, int o) {
      return (length + (o - (start % o)) % o) <= end - start+1;
    }
  }
}
