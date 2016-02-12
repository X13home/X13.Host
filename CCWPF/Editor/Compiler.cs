using NiL.JS;
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
      _predefs["Mz"] = DP_Type.BOOL;
      _predefs["Mb"] = DP_Type.SINT8;
      _predefs["MB"] = DP_Type.UINT8;
      _predefs["Mw"] = DP_Type.SINT16;
      _predefs["MW"] = DP_Type.UINT16;
      _predefs["Md"] = DP_Type.SINT32;

      _verbose = Topic.root.Get<bool>("/etc/MQTT-SN/PLC/verbose");
    }

    private Stack<DP_Inst> _sp;
    private List<DP_Scope> _programm;
    private Stack<DP_Scope> _scope;
    private SortedSet<DP_MemBlock> _memBlocks;
    private DP_Scope global, cur, initBlock;

    private bool _final;

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
      _scope = new Stack<DP_Scope>();
      _programm = new List<DP_Scope>();
      _sp = new Stack<DP_Inst>();
      _memBlocks = new SortedSet<DP_MemBlock>();
      _memBlocks.Add(new DP_MemBlock(0, 16384));
      uint addr;
      string vName;
      DP_Inst ri;

      try {
        global = ScopePush(null);
        initBlock = new DP_Scope(this, null);
        _programm.Add(initBlock);

        _final = true;

        global.AddInst(new DP_Inst(DP_InstCode.API, new DP_Merker() { type = DP_Type.API, Addr = 5 }), 0, 1);
        initBlock.AddInst(ri = new DP_Inst(DP_InstCode.LABEL), 0, 0);
        global.AddInst(DP_InstCode.LDI_M1, 0, 1);
        global.AddInst(DP_InstCode.CEQ);
        global.AddInst(new DP_Inst(DP_InstCode.JNZ) { _ref = ri }, 1, 0);

        var module = new Module(code, CompilerMessageCallback, Options.SuppressConstantPropogation | Options.SuppressUselessExpressionsElimination);

        _final = false;
        module.Root.Visit(this);

        _final = true;
        _sp.Clear();
        module.Root.Visit(this);

        if(global.code.Count == 0 || (ri = global.code[global.code.Count - 1])._code.Length != 1 || ri._code[0] != (byte)DP_InstCode.RET) {
          global.AddInst(DP_InstCode.RET);
        }
        if(initBlock.code.Count == 0 || (ri = initBlock.code[initBlock.code.Count - 1])._code.Length != 1 || ri._code[0] != (byte)DP_InstCode.RET) {
          initBlock.AddInst(DP_InstCode.RET);
        }
        varList = new SortedList<string, string>();
        ioList = new List<string>();
        uint mLen;

        addr = 0;
        SortedList<uint, PLC.ByteArray> HexN = new SortedList<uint, PLC.ByteArray>();

        foreach(var p in _programm) {
          foreach(var m in p.memory.OrderBy(z => z.type)) {
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
            case DP_Type.REFERENCE:
              vName = null;
              mLen = (uint)m.pOut * 32;
              break;
            case DP_Type.INPUT:
            case DP_Type.OUTPUT:
              ioList.Add(m.vd.Name);
              continue;
            default:
              continue;
            }
            if(m.Addr == uint.MaxValue) {
              m.Addr = AllocateMemory(uint.MaxValue, mLen) / (mLen>=32?32:mLen);
            }
            Log.Debug("{0}={1:X4}:{2:X4}", m.vd.Name, m.Addr, mLen);
            if(p == global && vName != null) {
              varList[m.vd.Name] = vName + m.Addr.ToString();
            }
          }

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
          HexN[p.code.First().addr] = new PLC.ByteArray(bytes.ToArray()); // { Titel = p.fm==null?null:p.fm.ToString() };
          if(_verbose.value) {
            Log.Debug("{0}", p.ToString());
          }
          bytes.Clear();
        }
        Hex = HexN;
        StackBottom = (_memBlocks.Last().start + 7) / 8;
        Log.Info("Used ROM: {0} bytes, RAM: {1} bytes", Hex.Select(z => z.Key + z.Value.GetBytes().Length).Max(), StackBottom);
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
        if(length > 16) {
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
    private DP_Scope ScopePush(DP_Merker fm) {
      cur = _programm.FirstOrDefault(z => z.fm == fm);
      if(cur == null) {
        cur = new DP_Scope(this, fm);
        _programm.Add(cur);
      }
      _scope.Push(cur);
      return cur;
    }
    private void ScopePop() {
      _scope.Pop();
      cur = _scope.Peek();
    }
    private void LoadConstant(CodeNode node, int v) {
      if(v == 0) {
        cur.AddInst(DP_InstCode.LDI_0, 0, 1);
      } else if(v > 0) {
        if(v < 256) {
          if(v == 1) {
            cur.AddInst(DP_InstCode.LDI_1, 0, 1);
          } else {
            cur.AddInst(new DP_Inst(DP_InstCode.LDI_U1, null, node), 0, 1);
          }
        } else {
          if(v < 65536) {
            cur.AddInst(new DP_Inst(DP_InstCode.LDI_U2, null, node), 0, 1);
          } else {
            cur.AddInst(new DP_Inst(DP_InstCode.LDI_S4, null, node), 0, 1);
          }
        }
      } else {
        if(v > -128) {
          if(v == -1) {
            cur.AddInst(DP_InstCode.LDI_M1, 0, 1);
          } else {
            cur.AddInst(new DP_Inst(DP_InstCode.LDI_S1, null, node), 0, 1);
          }
        } else {
          if(v > -32768) {
            cur.AddInst(new DP_Inst(DP_InstCode.LDI_S2, null, node), 0, 1);
          } else {
            cur.AddInst(new DP_Inst(DP_InstCode.LDI_S4, null, node), 0, 1);
          }
        }
      }
    }
    private void Store(CodeNode node, Expression e) {
      GetVariable a ;
      AssignmentOperatorCache a2;
      Property p;
      if((a= e as GetVariable) != null || ((a2 = e as AssignmentOperatorCache)!=null && (a = a2.Source as GetVariable)!=null) ) {
        var m = GetMerker(a.Descriptor);
        switch(m.type) {
        case DP_Type.BOOL:
          cur.AddInst(new DP_Inst(DP_InstCode.STM_B1_C16, m, node), 1, 0);
          break;
        case DP_Type.UINT8:
        case DP_Type.SINT8:
          cur.AddInst(new DP_Inst(DP_InstCode.STM_S1_C16, m, node), 1, 0);
          break;
        case DP_Type.UINT16:
        case DP_Type.SINT16:
          cur.AddInst(new DP_Inst(DP_InstCode.STM_S2_C16, m, node), 1, 0);
          break;
        case DP_Type.SINT32:
          cur.AddInst(new DP_Inst(DP_InstCode.STM_S4_C16, m, node), 1, 0);
          break;
        case DP_Type.PARAMETER:
          cur.AddInst(new DP_Inst((DP_InstCode)(DP_InstCode.ST_P0 + (byte)m.Addr), null, node), 1, 0);
          break;
        case DP_Type.LOCAL:
          cur.AddInst(new DP_Inst((DP_InstCode)(DP_InstCode.ST_L0 + (byte)m.Addr), null, node), 1, 0);
          break;
        case DP_Type.OUTPUT:
          cur.AddInst(new DP_Inst(DP_InstCode.OUT, m, node), 1, 0);
          break;
        default:
          throw new NotSupportedException(node.ToString());
        }
      } else if((p=e as Property)!=null){
        StoreProperty(node, p.Source as Expression, p.FieldName as Expression);
      } else {
        throw new NotImplementedException(node.ToString());
      }
    }
    private void StoreProperty(CodeNode node, Expression src, Expression name) {
      GetVariable f;
      DP_Merker m;
      Constant c;
      int len;

      if((f = src as GetVariable) != null && (m = GetMerker(f.Descriptor)).type == DP_Type.REFERENCE) {
        cur.AddInst(new DP_Inst(DP_InstCode.LDI_S4, m), 0, 1);
        len = m.pOut;
        //TODO: STM_xx_C16  (m.addr+offset)
      } else if(src is This) {
        cur.AddInst(DP_InstCode.LD_P0, 0, 1);
        len = int.MaxValue;
      } else {
        throw new NotSupportedException(src.ToString() + " as object");
      }
      if((c = name as Constant) != null && c.Value != null && c.Value.ValueType == JSValueType.String) {
        string pn = c.Value.ToString();
        UInt16 addr;
        int size;
        int idx;
        if(pn.Length > 1 && (idx = "zbBwWd".IndexOf(pn[0])) >= 0 && UInt16.TryParse(pn.Substring(1), out addr)) {
          DP_InstCode cmd;
          switch(idx * 2 + (addr > 255 ? 1 : 0)) {
          case 0:
            cmd = DP_InstCode.STM_B1_CS8;
            size = 1;
            break;
          case 1:
            cmd = DP_InstCode.STM_B1_CS16;
            size = 1;
            break;
          case 2:
          case 4:
            cmd = DP_InstCode.STM_S1_CS8;
            size = 8;
            break;
          case 3:
          case 5:
            cmd = DP_InstCode.STM_S1_CS16;
            size = 8;
            break;
          case 6:
          case 8:
            cmd = DP_InstCode.STM_S2_CS8;
            size = 16;
            break;
          case 7:
          case 9:
            cmd = DP_InstCode.LDM_S2_CS16;
            size = 16;
            break;
          case 10:
            cmd = DP_InstCode.STM_S4_CS8;
            size = 32;
            break;
          case 11:
            cmd = DP_InstCode.STM_S4_CS16;
            size = 32;
            break;
          default:
            throw new ApplicationException("SetProperty(" + node.ToString() + ") bad index");
          }
          if(((addr + 1) * size - 1) / 32 >= len) {
            throw new IndexOutOfRangeException(node.ToString());
          }
          cur.AddInst(new DP_Inst(cmd, new DP_Merker() { Addr = addr }, node), 2, 0);
          return;
        }
      }
      throw new NotSupportedException("Field name in " + node.ToString());
    }
    private void AddCommon(CodeNode node, Expression a, Expression b) {
      var c1 = a as Constant;
      var c2 = b as Constant;
      if(c1 != null && c2 != null) {
        LoadConstant(node, (int)(c1.Value.Value) + (int)(c2.Value.Value));
      } else if(c1 != null && (int)(c1.Value.Value) == 1) {
        b.Visit(this);
        cur.AddInst(DP_InstCode.INC, 1, 1);
      } else if(c1 != null && (int)(c1.Value.Value) == -1) {
        b.Visit(this);
        cur.AddInst(DP_InstCode.DEC, 1, 1);
      } else if(c2 != null && (int)(c2.Value.Value) == 1) {
        a.Visit(this);
        cur.AddInst(DP_InstCode.INC, 1, 1);
      } else if(c2 != null && (int)(c2.Value.Value) == -1) {
        a.Visit(this);
        cur.AddInst(DP_InstCode.DEC, 1, 1);
      } else {
        b.Visit(this);
        a.Visit(this);
        cur.AddInst(DP_InstCode.ADD, 2, 1);
      }
    }
    private bool CallFunction(Call node, DP_Merker m, Expression This) {
      DP_Inst d;
      DP_Merker mt;
      GetVariable v;

      if(m.scope != null) {
        var al = m.scope.memory.Where(z => z.type == DP_Type.PARAMETER).OrderBy(z => z.Addr).ToArray();
        for(int i = al.Length - 1; i >= 0; i--) {
          if(i < node.Arguments.Length) {
            node.Arguments[i].Visit(this);
          } else if(al[i].init != null) {  //TODO: check function(a, b=7)
            al[i].init.Visit(this);
          } else {
            cur.AddInst(DP_InstCode.LDI_0, 0, 1);
          }
        }
        if(This == null) {
          cur.AddInst(DP_InstCode.LDI_M1, 0, 1);
        } else if((v = This as GetVariable) != null && (mt = GetMerker(v.Descriptor)).type == DP_Type.REFERENCE) {
          cur.AddInst(new DP_Inst(DP_InstCode.LDI_S4, mt), 0, 1);
        } else if(This is This) {
          cur.AddInst(DP_InstCode.LD_P0, 0, 1);
        } else {
          throw new NotSupportedException(This.ToString() + " as this");
        }
        cur.AddInst(new DP_Inst(DP_InstCode.CALL, m));
        for(int i = al.Length - 1; i >= 0; i--) {
          cur.AddInst(DP_InstCode.NIP);
          d = _sp.Pop();
          _sp.Pop();
          _sp.Push(d);
        }
        return true;
      } else if(_final) {
        throw new ApplicationException("undefined function: " + m.vd.Name);
      }
      return false;
    }
    private void Arg2Op(Expression node, DP_InstCode c) {
      node.SecondOperand.Visit(this);
      node.FirstOperand.Visit(this);
      cur.AddInst(new DP_Inst(c), 2, 1);
    }
    private DP_Merker GetMerker(VariableDescriptor v, DP_Type type = DP_Type.NONE) {
      DP_Merker m = null;
      uint addr;

      m = cur.memory.FirstOrDefault(z => z.vd == v);
      if(m == null) {
        m = global.memory.FirstOrDefault(z => z.vd == v);
      }
      if(m == null) {
        m = LoadNativeFunctions(v);
      }
      if(m == null) {
        addr = uint.MaxValue;
        if(type == DP_Type.NONE) {
          if(v.Initializer != null && v.Initializer is FunctionDefinition) {
            type = DP_Type.FUNCTION;
          } else if(v.Name.Length > 2 && _predefs.TryGetValue(v.Name.Substring(0, 2), out type)) {
            uint mLen;
            switch(type) {
            case DP_Type.BOOL:
              mLen = 1;
              break;
            case DP_Type.SINT8:
            case DP_Type.UINT8:
              mLen = 8;
              break;
            case DP_Type.SINT16:
            case DP_Type.UINT16:
              mLen = 16;
              break;
            case DP_Type.SINT32:
              mLen = 32;
              break;
            default:
              mLen = 0;
              break;
            }
            if(UInt32.TryParse(v.Name.Substring(2), out addr)) {
              addr &= 0xFFFF;
              if(type == DP_Type.INPUT || type == DP_Type.OUTPUT) {
                addr = (uint)((uint)(((byte)v.Name[0]) << 24) | (uint)(((byte)v.Name[1]) << 16) | addr);
              } else if(mLen > 0) {
                AllocateMemory(addr * mLen, mLen);
              }
            } else {
              addr = uint.MaxValue;
            }
          } else if(v.LexicalScope) {
            type = DP_Type.LOCAL;
            addr = (uint)cur.memory.Where(z => z.type == DP_Type.LOCAL).Count();
            if(addr > 15) {
              throw new ArgumentOutOfRangeException("Too many local variables: " + v.Name + cur.fm == null ? string.Empty : ("in " + cur.fm.ToString()));
            }
          } else {
            type = DP_Type.SINT32;
            addr = uint.MaxValue;
          }

        }
        m = new DP_Merker() { type = type, vd = v, Addr = addr, init = v.Initializer };

        if(type == DP_Type.FUNCTION || type == DP_Type.API || type == DP_Type.INPUT || type == DP_Type.OUTPUT) {
          global.memory.Add(m);
        } else {
          cur.memory.Add(m);
        }
      } else if(m.type != type && m.type == DP_Type.NONE) {
        m.type = type;
      }
      return m;
    }

    private DP_Merker LoadNativeFunctions(VariableDescriptor v) {
      DP_Merker m;
      switch(v.Name) {
      case "TwiControl":
        m = new DP_Merker() { type = DP_Type.API, Addr = 1, vd = v, pIn = 1 };
        break;
      case "TwiStatus":
        m = new DP_Merker() { type = DP_Type.API, Addr = 2, vd = v, pOut = 1 };
        break;
      case "TwiPutByte":
        m = new DP_Merker() { type = DP_Type.API, Addr = 3, vd = v, pIn = 1 };
        break;
      case "TwiGetByte":
        m = new DP_Merker() { type = DP_Type.API, Addr = 4, vd = v, pOut = 1 };
        break;
      case "NodeStatus":
        m = new DP_Merker() { type = DP_Type.API, Addr = 5, vd = v, pOut = 1 };
        break;
      case "getMilliseconds":
        m = new DP_Merker() { type = DP_Type.API, Addr = 6, vd = v, pOut = 1 };
        break;
      case "getSeconds":
        m = new DP_Merker() { type = DP_Type.API, Addr = 7, vd = v, pOut = 1 };
        break;
      case "Random":
        m = new DP_Merker() { type = DP_Type.API, Addr = 8, vd = v, pOut = 1 };
        break;
      default:
        return null;
      }
      global.memory.Add(m);
      return m;
    }

    private void SafeCodeBlock(CodeNode node, int sp = -1) {
      if(node is CodeBlock) {
        node.Visit(this);
      } else {
        if(sp < 0) {
          sp = _sp.Count;
        }
        node.Visit(this);
        while(_sp.Count > sp) {
          var d = _sp.Pop();
          if(d == null || !d.canOptimized || !cur.code.Remove(d)) {
            cur.AddInst(DP_InstCode.DROP);
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
      public int pIn;
      public int pOut;
      public override string ToString() {
        //return scope == null || scope.fm == null ? vd.Name : scope.fm.ToString() + "." + vd.ToString();
        return vd.Name;
      }
    }
    internal class DP_Scope {
      private DP_Compiler _compiler;
      public List<DP_Inst> code;
      public List<DP_Merker> memory;
      public DP_Merker entryPoint;
      public Stack<DP_Loop> loops;
      public DP_Merker fm;

      public DP_Scope(DP_Compiler c, DP_Merker fm) {
        _compiler = c;
        this.fm = fm;
        code = new List<DP_Inst>();
        memory = new List<DP_Merker>();
        loops = new Stack<DP_Loop>();
      }
      public void AddInst(DP_Inst inst, int pop = 0, int push = 0) {
        int i;
        for(i = 0; i < pop; i++) {
          _compiler._sp.Pop();
        }
        if(_compiler._final) {
          code.Add(inst);
        }
        for(i = 0; i < push; i++) {
          _compiler._sp.Push(inst);
        }
      }
      public void AddInst(DP_InstCode ic, int pop = 0, int push = 0) {
        int i;
        DP_Inst inst;
        for(i = 0; i < pop; i++) {
          _compiler._sp.Pop();
        }
        if(_compiler._final) {
          code.Add(inst = new DP_Inst(ic));
        } else {
          inst = null;
        }
        for(i = 0; i < push; i++) {
          _compiler._sp.Push(inst);
        }
      }

      public override string ToString() {
        var sb = new StringBuilder();
        int ls = 0;
        if(fm != null) {
          sb.Append(fm.ToString());
        }
        sb.Append("\n");
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
        case DP_InstCode.LDM_B1_CS8:
        case DP_InstCode.STM_B1_CS8:

        case DP_InstCode.LDM_S1_CS8:
        case DP_InstCode.STM_S1_CS8:
        case DP_InstCode.LDM_U1_CS8:

        case DP_InstCode.LDM_S2_CS8:
        case DP_InstCode.STM_S2_CS8:
        case DP_InstCode.LDM_U2_CS8:

        case DP_InstCode.LDM_S4_CS8:
        case DP_InstCode.STM_S4_CS8:
          if(_code == null || _code.Length != 2) {
            _code = new byte[2];
          }
          _code[0] = (byte)cmd;
          _code[1] = (byte)_param.Addr;
          break;
        case DP_InstCode.LDM_B1_C16:
        case DP_InstCode.LDM_B1_CS16:
        case DP_InstCode.STM_B1_C16:
        case DP_InstCode.STM_B1_CS16:

        case DP_InstCode.LDM_S1_C16:
        case DP_InstCode.LDM_S1_CS16:
        case DP_InstCode.STM_S1_C16:
        case DP_InstCode.STM_S1_CS16:

        case DP_InstCode.LDM_S2_C16:
        case DP_InstCode.LDM_S2_CS16:
        case DP_InstCode.STM_S2_C16:
        case DP_InstCode.STM_S2_CS16:

        case DP_InstCode.LDM_S4_C16:
        case DP_InstCode.LDM_S4_CS16:
        case DP_InstCode.STM_S4_C16:
        case DP_InstCode.STM_S4_CS16:

        case DP_InstCode.LDM_U1_C16:
        case DP_InstCode.LDM_U1_CS16:

        case DP_InstCode.LDM_U2_C16:
        case DP_InstCode.LDM_U2_CS16:

        case DP_InstCode.CALL:
        case DP_InstCode.API:
          if(_code == null || _code.Length != 3) {
            _code = new byte[3];
          }
          _code[0] = (byte)cmd;
          _code[1] = (byte)_param.Addr;
          _code[2] = (byte)(_param.Addr >> 8);
          break;
        case DP_InstCode.LDI_S1:
        case DP_InstCode.LDI_U1:
          if(_param != null && (_param.type == DP_Type.REFERENCE || _param.type == DP_Type.FUNCTION)) {
            if(_param.Addr > 65535) {
              cmd = DP_InstCode.LDI_S4;
              goto case DP_InstCode.LDI_S4;
            } else if(_param.Addr > 255) {
              cmd = DP_InstCode.LDI_U2;
              goto case DP_InstCode.LDI_U2;
            }
            tmp_d = (int)_param.Addr;
          } else if((_cn as Constant) != null) {
            tmp_d = (int)((Constant)_cn).Value;
          } else {
            tmp_d = 0;
          }
          if(_code == null || _code.Length != 2) {
            _code = new byte[2];
          }
          _code[0] = (byte)cmd;
          _code[1] = (byte)tmp_d;
          break;
        case DP_InstCode.LDI_S2:
        case DP_InstCode.LDI_U2:
          if(_param != null && (_param.type == DP_Type.REFERENCE || _param.type == DP_Type.FUNCTION)) {
            if(_param.Addr < 256) {
              cmd = DP_InstCode.LDI_U1;
              goto case DP_InstCode.LDI_U1;
            } else if(_param.Addr > 65535) {
              cmd = DP_InstCode.LDI_S4;
              goto case DP_InstCode.LDI_S4;
            }
            tmp_d = (int)_param.Addr;
          } else if((_cn as Constant) != null) {
            tmp_d = (int)((Constant)_cn).Value;
          } else {
            tmp_d = 0;
          }

          if(_code == null || _code.Length != 3) {
            _code = new byte[3];
          }
          _code[0] = (byte)cmd;
          _code[1] = (byte)tmp_d;
          _code[2] = (byte)(tmp_d >> 8);
          break;
        case DP_InstCode.LDI_S4:
          if(_param != null && (_param.type == DP_Type.REFERENCE || _param.type == DP_Type.FUNCTION)) {
            if(_param.Addr < 256) {
              cmd = DP_InstCode.LDI_U1;
              goto case DP_InstCode.LDI_U1;
            } else if(_param.Addr < 65536) {
              cmd = DP_InstCode.LDI_U2;
              goto case DP_InstCode.LDI_U2;
            }
            tmp_d = (int)_param.Addr;
          } else if((_cn as Constant) != null) {
            tmp_d = (int)((Constant)_cn).Value;
          } else {
            tmp_d = 0;
          }
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
        return (length + (o - (start % o)) % o) <= end - start + 1;
      }
    }
  }
}
