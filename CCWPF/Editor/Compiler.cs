using NiL.JS;
using NiL.JS.Core;
using NiL.JS.Expressions;
using NiL.JS.Statements;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace X13.CC {
  internal partial class EP_Compiler {
    private static SortedList<string, EP_Type> _predefs;
    private static DVar<bool> _verbose;

    static EP_Compiler() {
      _predefs = new SortedList<string, EP_Type>();
      _predefs["Op"] = EP_Type.OUTPUT;
      _predefs["On"] = EP_Type.OUTPUT;
      _predefs["Pp"] = EP_Type.OUTPUT;
      _predefs["Pn"] = EP_Type.OUTPUT;
      _predefs["Ip"] = EP_Type.INPUT;
      _predefs["In"] = EP_Type.INPUT;
      _predefs["Av"] = EP_Type.INPUT;
      _predefs["Ai"] = EP_Type.INPUT;

      _verbose = Topic.root.Get<bool>("/etc/MQTT-SN/PLC/verbose");
    }

    internal Stack<Instruction> _sp;
    internal List<Scope> _programm;
    internal Stack<Scope> _scope;
    public Scope global, cur, initBlock;

    public SortedList<string, string> varList;
    public List<string> ioList;
    public event CompilerMessageCallback CMsg;
    public long StackBottom { get; private set; }
    public SortedList<uint, PLC.ByteArray> Hex;

    public EP_Compiler() {
      Hex = new SortedList<uint, PLC.ByteArray>();
    }

    public bool Parse(string code) {
      bool success = false;
      _scope = new Stack<Scope>();
      _programm = new List<Scope>();
      _sp = new Stack<Instruction>();
      uint addr;
      string vName;
      Instruction ri;

      try {
        global = ScopePush(null);
        initBlock = new Scope(this, null);
        _programm.Add(initBlock);

        initBlock.AddInst(ri = new Instruction(EP_InstCode.LABEL));
        global.AddInst(new Instruction(EP_InstCode.JMP) { _ref = ri });
        global.AddInst(EP_InstCode.NOP);

        var module = new Module(code, CompilerMessageCallback, Options.SuppressConstantPropogation);  //  | Options.SuppressUselessExpressionsElimination

        var p1 = new EP_VP1(this);
        module.Root.Visit(p1);

        _sp.Clear();
        var p2 = new EP_VP2(this);
        module.Root.Visit(p2);

        if(global.code.Count == 0 || (ri = global.code[global.code.Count - 1])._code.Length != 1 || ri._code[0] != (byte)EP_InstCode.RET) {
          global.AddInst(EP_InstCode.RET);
        }
        if(initBlock.code.Count == 0 || (ri = initBlock.code[initBlock.code.Count - 1])._code.Length != 1 || ri._code[0] != (byte)EP_InstCode.RET) {
          initBlock.AddInst(EP_InstCode.RET);
        }
        varList = new SortedList<string, string>();
        ioList = new List<string>();
        uint mLen;

        addr = 0;
        SortedList<uint, PLC.ByteArray> HexN = new SortedList<uint, PLC.ByteArray>();

        foreach(var p in _programm) {
          foreach(var m in p.memory.OrderBy(z => z.type)) {
            switch(m.type) {
            case EP_Type.BOOL:
              vName = "Mz";
              mLen = 1;
              break;
            case EP_Type.SINT8:
              vName = "Mb";
              mLen = 8;
              break;
            case EP_Type.SINT16:
              vName = "Mw";
              mLen = 16;
              break;
            case EP_Type.SINT32:
              vName = "Md";
              mLen = 32;
              break;
            case EP_Type.UINT8:
              vName = "MB";
              mLen = 8;
              break;
            case EP_Type.UINT16:
              vName = "MW";
              mLen = 16;
              break;
            case EP_Type.REFERENCE:
              vName = null;
              mLen = (uint)m.pOut * 32;
              break;
            case EP_Type.INPUT:
            case EP_Type.OUTPUT:
              ioList.Add(m.vd.Name);
              continue;
            default:
              continue;
            }
            if(m.Addr == uint.MaxValue) {
              m.Addr = global.AllocateMemory(uint.MaxValue, mLen) / (mLen >= 32 ? 32 : mLen);
            }
            Log.Debug("{0}={1:X4}:{2:X4}", m.vd.Name, m.Addr * (mLen >= 32 ? 32 : mLen), mLen);
            if(p == global && vName != null) {
              varList[m.vd.Name] = vName + m.Addr.ToString();
            }
          }

          addr += (32 - (addr % 32)) % 32;
          if(p.fm != null) {
            p.fm.Addr = addr;
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
          if(bytes.Count > 0) {
            HexN[p.code.First().addr] = new PLC.ByteArray(bytes.ToArray());
            if(_verbose.value) {
              Log.Debug("{0}", p.ToString());
            }
          }
          bytes.Clear();
        }
        Hex = HexN;
        StackBottom = (global.memBlocks.Last().start + 7) / 8;
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

    internal Scope ScopePush(Merker fm) {
      cur = _programm.FirstOrDefault(z => z.fm == fm);
      if(cur == null) {
        cur = new Scope(this, fm);
        _programm.Add(cur);
      }
      _scope.Push(cur);
      return cur;
    }
    internal void ScopePop() {
      _scope.Pop();
      cur = _scope.Peek();
    }
    internal Merker DefineMerker(VariableDescriptor v, EP_Type type = EP_Type.NONE) {
      Merker m = null;
      uint addr;
      EP_Type ioType;

      m = cur.memory.FirstOrDefault(z => z.vd == v);
      if(m == null) {
        m = global.memory.FirstOrDefault(z => z.vd == v);
      }
      if(m == null) {
        addr = uint.MaxValue;
        if(v.Name.Length > 2 && _predefs.TryGetValue(v.Name.Substring(0, 2), out ioType) && UInt32.TryParse(v.Name.Substring(2), out addr)) {
          addr = (uint)((uint)(((byte)v.Name[0]) << 24) | (uint)(((byte)v.Name[1]) << 16) | addr & 0xFFFF);
          type = ioType;
        } else if(type == EP_Type.NONE) {
          if(v.Initializer != null && v.Initializer is FunctionDefinition) {
            type = EP_Type.FUNCTION;
          } else if(v.LexicalScope) {
            type = EP_Type.LOCAL;
            addr = (uint)cur.memory.Where(z => z.type == EP_Type.LOCAL).Count();
            if(addr > 15) {
              throw new ArgumentOutOfRangeException("Too many local variables: " + v.Name + cur.fm == null ? string.Empty : ("in " + cur.fm.ToString()));
            }
          } else {
            type = EP_Type.SINT32;
            addr = uint.MaxValue;
          }

        }
        m = new Merker() { type = type, vd = v, pName=v.Name, Addr = addr, init = v.Initializer };

        if(type == EP_Type.API || type == EP_Type.INPUT || type == EP_Type.OUTPUT) {
          global.memory.Add(m);
          m.fName = v.Name;
        } else {
          cur.memory.Add(m);
          m.fName = (cur == global ? v.Name : cur.fm.fName + (cur.fm.type==EP_Type.FUNCTION?"+":".") + v.Name);
        }
        
      } else if(m.type != type && m.type == EP_Type.NONE) {
        m.type = type;
      }
      return m;
    }
    internal Merker GetMerker(VariableDescriptor v) {
      Merker m = null;

      m = cur.memory.FirstOrDefault(z => z.vd == v);
      if(m == null) {
        m = global.memory.FirstOrDefault(z => z.vd == v);
      }
      if(m == null) {
        m = LoadNativeFunctions(v);
      }

      return m;
    }

    private Merker LoadNativeFunctions(VariableDescriptor v) {
      Merker m;
      switch(v.Name) {
      case "TwiControl":
        m = new Merker() { type = EP_Type.API, Addr = 1, vd = v, pIn = 1 };
        break;
      case "TwiStatus":
        m = new Merker() { type = EP_Type.API, Addr = 2, vd = v, pOut = 1 };
        break;
      case "TwiPutByte":
        m = new Merker() { type = EP_Type.API, Addr = 3, vd = v, pIn = 1 };
        break;
      case "TwiGetByte":
        m = new Merker() { type = EP_Type.API, Addr = 4, vd = v, pOut = 1 };
        break;
      case "NodeStatus":
        m = new Merker() { type = EP_Type.API, Addr = 5, vd = v, pOut = 1 };
        break;
      case "getMilliseconds":
        m = new Merker() { type = EP_Type.API, Addr = 6, vd = v, pOut = 1 };
        break;
      case "getSeconds":
        m = new Merker() { type = EP_Type.API, Addr = 7, vd = v, pOut = 1 };
        break;
      case "Random":
        m = new Merker() { type = EP_Type.API, Addr = 8, vd = v, pOut = 1 };
        break;
      default:
        return null;
      }
      global.memory.Add(m);
      return m;
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
    internal class Merker {
      public uint Addr;
      public EP_Type type;
      public VariableDescriptor vd;
      public Expression init;
      public Scope scope;
      public bool initialized;
      public int pIn;
      public int pOut;
      public string pName;
      public string fName;
      public override string ToString() {
        return fName;
      }
    }
    internal class Scope {
      private EP_Compiler _compiler;
      public List<Instruction> code;
      public List<Merker> memory;
      public Stack<EP_VP2.Loop> loops;
      public Merker fm;
      public SortedSet<DP_MemBlock> memBlocks;


      public Scope(EP_Compiler c, Merker fm) {
        _compiler = c;
        this.fm = fm;
        memBlocks = new SortedSet<DP_MemBlock>();
        memBlocks.Add(new DP_MemBlock(0, 16384));
        code = new List<Instruction>();
        memory = new List<Merker>();
        loops = new Stack<EP_VP2.Loop>();
      }
      public void AddInst(Instruction inst, int pop = 0, int push = 0) {
        int i;
        for(i = 0; i < pop; i++) {
          _compiler._sp.Pop();
        }
        code.Add(inst);
        for(i = 0; i < push; i++) {
          _compiler._sp.Push(inst);
        }
      }
      public void AddInst(EP_InstCode ic, int pop = 0, int push = 0) {
        int i;
        Instruction inst;
        for(i = 0; i < pop; i++) {
          _compiler._sp.Pop();
        }
        code.Add(inst = new Instruction(ic));
        for(i = 0; i < push; i++) {
          _compiler._sp.Push(inst);
        }
      }
      public uint AllocateMemory(uint addr, uint length) {
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

          fb = memBlocks.FirstOrDefault(z => z.Check(length, o));
          if(fb == null) {
            throw new ArgumentOutOfRangeException("Not enough memory");
          }
          memBlocks.Remove(fb);
          start = (uint)(fb.start + (o - (fb.start % o)) % o);
          end = start + length - 1;
          if(fb.start < start) {
            memBlocks.Add(new DP_MemBlock(fb.start, start - 1));
          }
          if(fb.end > end) {
            memBlocks.Add(new DP_MemBlock(end + 1, fb.end));
          }
        } else {
          start = addr;
          end = addr + length - 1;
          do {
            fb = memBlocks.FirstOrDefault(z => z.start <= end && z.end >= start);
            if(fb == null) {
              break;
            }
            memBlocks.Remove(fb);
            if(fb.start < start) {
              memBlocks.Add(new DP_MemBlock(fb.start, start - 1));
            }
            if(fb.end > end) {
              memBlocks.Add(new DP_MemBlock(end + 1, fb.end));
            }
          } while(fb != null);
        }
        //{
        //  StringBuilder sb = new StringBuilder();
        //  sb.AppendFormat("AllocateMemory({0:X4}{2}, {1:X2})\n", start, length, addr==uint.MaxValue?"*":"");
        //  foreach(var m in global.memBlocks) {
        //    sb.AppendFormat("  {0:X4}:{1:X4}\n", m.start, m.end);
        //  }
        //  Log.Info("{0}", sb.ToString());
        //}
        return start;
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
      public Merker GetProperty(string name, EP_Type type= EP_Type.NONE) {
        Merker m;
        string fName = fm.fName + "." + name;
        m = memory.FirstOrDefault(z => z.pName == name);
        if(type!=EP_Type.NONE && m == null && !string.IsNullOrEmpty(name)) {
          m = new Merker() { fName = fName, pName=name, type = type };
          memory.Add(m);
        }
        return m;
      }
      public void AllocatFields() {
        uint mLen;
        foreach(var m in memory.OrderBy(z => z.type)) {
          switch(m.type) {
          case EP_Type.PropB1:
            mLen = 1;
            break;
          case EP_Type.PropU1:
          case EP_Type.PropS1:
            mLen = 8;
            break;
          case EP_Type.PropU2:
          case EP_Type.PropS2:
            mLen = 16;
            break;
          case EP_Type.PropS4:
            mLen = 32;
            break;
          default:
            continue;
          }
          m.Addr = AllocateMemory(uint.MaxValue, mLen) / (mLen >= 32 ? 32 : mLen);
          Log.Debug("{0}= +{1:X4}:{2:X4}", m.fName, m.Addr * (mLen >= 32 ? 32 : mLen), mLen);
        }
      }
    }
    internal class Instruction {
      internal uint addr;
      internal byte[] _code;
      internal Merker _param;
      internal CodeNode _cn;
      internal Instruction _ref;

      public bool canOptimized;

      public Instruction(EP_InstCode cmd, Merker param = null, CodeNode cn = null) {
        _param = param;
        _cn = cn;
        Prepare(cmd);
      }
      public bool Link() {
        if(_param == null && _ref == null) {
          return true;
        }
        Prepare((EP_InstCode)_code[0]);
        return false;
      }
      private void Prepare(EP_InstCode cmd) {
        int tmp_d;
        uint tmp_D;
        switch(cmd) {
        case EP_InstCode.LABEL:
          if(_code == null || _code.Length != 0) {
            _code = new byte[0];
          }
          break;
        case EP_InstCode.NOP:
        case EP_InstCode.DUP:
        case EP_InstCode.DROP:
        case EP_InstCode.NIP:
        case EP_InstCode.SWAP:
        case EP_InstCode.OVER:
        case EP_InstCode.ROT:
        case EP_InstCode.NOT:
        case EP_InstCode.AND:
        case EP_InstCode.OR:
        case EP_InstCode.XOR:
        case EP_InstCode.ADD:
        case EP_InstCode.SUB:
        case EP_InstCode.MUL:
        case EP_InstCode.DIV:
        case EP_InstCode.MOD:
        case EP_InstCode.INC:
        case EP_InstCode.DEC:
        case EP_InstCode.NEG:
        case EP_InstCode.CEQ:
        case EP_InstCode.CNE:
        case EP_InstCode.CGT:
        case EP_InstCode.CGE:
        case EP_InstCode.CLT:
        case EP_InstCode.CLE:
        case EP_InstCode.NOT_L:
        case EP_InstCode.AND_L:
        case EP_InstCode.OR_L:
        case EP_InstCode.XOR_L:
        case EP_InstCode.LD_P0:
        case EP_InstCode.LD_P1:
        case EP_InstCode.LD_P2:
        case EP_InstCode.LD_P3:
        case EP_InstCode.LD_P4:
        case EP_InstCode.LD_P5:
        case EP_InstCode.LD_P6:
        case EP_InstCode.LD_P7:
        case EP_InstCode.LD_P8:
        case EP_InstCode.LD_P9:
        case EP_InstCode.LD_PA:
        case EP_InstCode.LD_PB:
        case EP_InstCode.LD_PC:
        case EP_InstCode.LD_PD:
        case EP_InstCode.LD_PE:
        case EP_InstCode.LD_PF:
        case EP_InstCode.LD_L0:
        case EP_InstCode.LD_L1:
        case EP_InstCode.LD_L2:
        case EP_InstCode.LD_L3:
        case EP_InstCode.LD_L4:
        case EP_InstCode.LD_L5:
        case EP_InstCode.LD_L6:
        case EP_InstCode.LD_L7:
        case EP_InstCode.LD_L8:
        case EP_InstCode.LD_L9:
        case EP_InstCode.LD_LA:
        case EP_InstCode.LD_LB:
        case EP_InstCode.LD_LC:
        case EP_InstCode.LD_LD:
        case EP_InstCode.LD_LE:
        case EP_InstCode.LD_LF:
        case EP_InstCode.ST_P0:
        case EP_InstCode.ST_P1:
        case EP_InstCode.ST_P2:
        case EP_InstCode.ST_P3:
        case EP_InstCode.ST_P4:
        case EP_InstCode.ST_P5:
        case EP_InstCode.ST_P6:
        case EP_InstCode.ST_P7:
        case EP_InstCode.ST_P8:
        case EP_InstCode.ST_P9:
        case EP_InstCode.ST_PA:
        case EP_InstCode.ST_PB:
        case EP_InstCode.ST_PC:
        case EP_InstCode.ST_PD:
        case EP_InstCode.ST_PE:
        case EP_InstCode.ST_PF:
        case EP_InstCode.ST_L0:
        case EP_InstCode.ST_L1:
        case EP_InstCode.ST_L2:
        case EP_InstCode.ST_L3:
        case EP_InstCode.ST_L4:
        case EP_InstCode.ST_L5:
        case EP_InstCode.ST_L6:
        case EP_InstCode.ST_L7:
        case EP_InstCode.ST_L8:
        case EP_InstCode.ST_L9:
        case EP_InstCode.ST_LA:
        case EP_InstCode.ST_LB:
        case EP_InstCode.ST_LC:
        case EP_InstCode.ST_LD:
        case EP_InstCode.ST_LE:
        case EP_InstCode.ST_LF:
        case EP_InstCode.LDM_B1_S:
        case EP_InstCode.LDM_S1_S:
        case EP_InstCode.LDM_S2_S:
        case EP_InstCode.LDM_S4_S:
        case EP_InstCode.LDM_U1_S:
        case EP_InstCode.LDM_U2_S:
        case EP_InstCode.STM_B1_S:
        case EP_InstCode.STM_S1_S:
        case EP_InstCode.STM_S2_S:
        case EP_InstCode.STM_S4_S:
        case EP_InstCode.SJMP:
        case EP_InstCode.SCALL:
        case EP_InstCode.RET:
          if(_code == null || _code.Length != 1) {
            _code = new byte[1];
          }
          _code[0] = (byte)cmd;
          break;
        case EP_InstCode.LSL:
        case EP_InstCode.LSR:
        case EP_InstCode.ASR:
          if(_code == null || _code.Length != 2) {
            _code = new byte[2];
          }
          _code[0] = (byte)cmd;
          _code[1] = (byte)((int)((Constant)_cn).Value & 0x1F);
          break;
        case EP_InstCode.LDM_B1_CS8:
        case EP_InstCode.STM_B1_CS8:

        case EP_InstCode.LDM_S1_CS8:
        case EP_InstCode.STM_S1_CS8:
        case EP_InstCode.LDM_U1_CS8:

        case EP_InstCode.LDM_S2_CS8:
        case EP_InstCode.STM_S2_CS8:
        case EP_InstCode.LDM_U2_CS8:

        case EP_InstCode.LDM_S4_CS8:
        case EP_InstCode.STM_S4_CS8:
          if(_code == null || _code.Length != 2) {
            _code = new byte[2];
          }
          _code[0] = (byte)cmd;
          _code[1] = (byte)_param.Addr;
          break;
        case EP_InstCode.LDM_B1_C16:
        case EP_InstCode.LDM_B1_CS16:
        case EP_InstCode.STM_B1_C16:
        case EP_InstCode.STM_B1_CS16:

        case EP_InstCode.LDM_S1_C16:
        case EP_InstCode.LDM_S1_CS16:
        case EP_InstCode.STM_S1_C16:
        case EP_InstCode.STM_S1_CS16:

        case EP_InstCode.LDM_S2_C16:
        case EP_InstCode.LDM_S2_CS16:
        case EP_InstCode.STM_S2_C16:
        case EP_InstCode.STM_S2_CS16:

        case EP_InstCode.LDM_S4_C16:
        case EP_InstCode.LDM_S4_CS16:
        case EP_InstCode.STM_S4_C16:
        case EP_InstCode.STM_S4_CS16:

        case EP_InstCode.LDM_U1_C16:
        case EP_InstCode.LDM_U1_CS16:

        case EP_InstCode.LDM_U2_C16:
        case EP_InstCode.LDM_U2_CS16:

        case EP_InstCode.CALL:
        case EP_InstCode.API:
          if(_code == null || _code.Length != 3) {
            _code = new byte[3];
          }
          _code[0] = (byte)cmd;
          _code[1] = (byte)_param.Addr;
          _code[2] = (byte)(_param.Addr >> 8);
          break;
        case EP_InstCode.LDI_0:
        case EP_InstCode.LDI_1:
        case EP_InstCode.LDI_M1:
        case EP_InstCode.LDI_S1:
        case EP_InstCode.LDI_U1:
        case EP_InstCode.LDI_S2:
        case EP_InstCode.LDI_U2:
        case EP_InstCode.LDI_S4:
          if(_param != null && (_param.type == EP_Type.REFERENCE || _param.type == EP_Type.FUNCTION)) {
            tmp_d = (int)_param.Addr;
          } else if((_cn as Constant) != null) {
            tmp_d = (int)((Constant)_cn).Value;
          } else {
            tmp_d = 0;
          }

          if(tmp_d == 0) {
            if(_code == null || _code.Length != 1) {
              _code = new byte[1];
            }
            _code[0]=(byte)EP_InstCode.LDI_0;
          } else if(tmp_d == 1) {
            if(_code == null || _code.Length != 1) {
              _code = new byte[1];
            }
            _code[0] = (byte)EP_InstCode.LDI_1;
          } else if(tmp_d == -1) {
            if(_code == null || _code.Length != 1) {
              _code = new byte[1];
            }
            _code[0] = (byte)EP_InstCode.LDI_M1;
          } else if(tmp_d > -128 && tmp_d < 256) {
            if(_code == null || _code.Length != 2) {
              _code = new byte[2];
            }
            _code[0] = (byte)(tmp_d < 0 ? EP_InstCode.LDI_S1 : EP_InstCode.LDI_U1);
            _code[1] = (byte)tmp_d;
          } else if(tmp_d > -32768 && tmp_d < 65536) {
            if(_code == null || _code.Length != 3) {
              _code = new byte[3];
            }
            _code[0] = (byte)(tmp_d < 0 ? EP_InstCode.LDI_S2 : EP_InstCode.LDI_U2);
            _code[1] = (byte)tmp_d;
            _code[2] = (byte)(tmp_d >> 8);

          } else {
            if(_code == null || _code.Length != 5) {
              _code = new byte[5];
            }
            _code[0] = (byte)EP_InstCode.LDI_S4;
            _code[1] = (byte)tmp_d;
            _code[2] = (byte)(tmp_d >> 8);
            _code[3] = (byte)(tmp_d >> 16);
            _code[4] = (byte)(tmp_d >> 24);
          }
          break;
        case EP_InstCode.OUT:
        case EP_InstCode.IN:
          if(_code == null || _code.Length != 5) {
            _code = new byte[5];
          }
          _code[0] = (byte)cmd;
          _code[1] = (byte)_param.Addr;
          _code[2] = (byte)(_param.Addr >> 8);
          _code[3] = (byte)(_param.Addr >> 16);
          _code[4] = (byte)(_param.Addr >> 24);
          break;

        case EP_InstCode.JZ:
        case EP_InstCode.JNZ:
        case EP_InstCode.JMP:
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
          sb.Append(((EP_InstCode)_code[0]).ToString());
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

  }
}
