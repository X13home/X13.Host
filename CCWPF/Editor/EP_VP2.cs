using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using NiL.JS.Core;
using NiL.JS.Expressions;
using NiL.JS.Statements;


namespace X13.CC {
  internal partial class EP_VP2 : Visitor<EP_VP2> {
    private readonly EP_Compiler _compiler;

    public EP_VP2(EP_Compiler compiler) {
      _compiler = compiler;
    }
    protected override EP_VP2 Visit(CodeNode node) {
      throw new NotSupportedException("Visit(<" + node.GetType().Name + ">" + node.ToString() + ")");
    }
    protected override EP_VP2 Visit(Addition node) {
      AddCommon(node, node.FirstOperand, node.SecondOperand);
      return this;
    }
    protected override EP_VP2 Visit(BitwiseConjunction node) {
      Arg2Op(node, EP_InstCode.AND);
      return this;
    }
    protected override EP_VP2 Visit(ArrayDefinition node) {
      return Visit(node as Expression);
    }
    protected override EP_VP2 Visit(Assignment node) {
      EP_Compiler.Instruction d2 = new EP_Compiler.Instruction(EP_InstCode.DUP) { canOptimized = true };
      _compiler._sp.Push(d2);
      node.SecondOperand.Visit(this);
      _compiler.cur.AddInst(d2);
      Store(node, node.FirstOperand);
      return this;
    }
    protected override EP_VP2 Visit(Call node) {
      Property p;
      GetVariable f;
      EP_Compiler.Merker m;
      Constant c;
      EP_Compiler.Instruction d;
      EP_Compiler.Scope sc;

      if((p = node.FirstOperand as Property) != null
        && (((f = p.Source as GetVariable) != null && (m = _compiler.GetMerker(f.Descriptor)) != null && (sc = m.scope) != null) || (p.Source is This && (sc = _compiler.cur) != null))
        && (c = p.FieldName as Constant) != null && c.Value != null && c.Value.ValueType == JSValueType.String) {
        EP_Compiler.Merker mf;
        if((mf = sc.GetProperty(c.Value.ToString())) != null) {
          CallFunction(node, mf, p.Source as Expression);
          return this;
        }
      }
      f = node.FirstOperand as GetVariable;
      if(f != null) {
        if(node.CallMode == CallMode.Regular) {
          m = _compiler.GetMerker(f.Descriptor);
          if(m == null || (m.type != EP_Type.FUNCTION && m.type != EP_Type.API)) {
            throw new ArgumentException("Unknown function: " + f.Descriptor.Name);
          }
          if(m.type == EP_Type.API) {
            int i;
            for(i = m.pIn - 1; i >= 0; i--) {
              if(i < node.Arguments.Length) {
                node.Arguments[i].Visit(this);
                _compiler._sp.Pop();
              } else {
                _compiler.cur.AddInst(EP_InstCode.LDI_0);
              }
            }
            _compiler.cur.AddInst(new EP_Compiler.Instruction(EP_InstCode.API, m, node), 0, m.pOut);
            return this;
          } else if(m.type == EP_Type.FUNCTION) {
            if(CallFunction(node, m, null)) {
              return this;
            }
          } else {
            throw new ApplicationException(m.vd.Name + " is not function");
          }
        } else {
          throw new NotSupportedException("Call(" + node.FirstOperand.ToString() + ") Mode: " + node.CallMode.ToString());
        }
      }

      for(int i = node.Arguments.Length - 1; i >= 0; i--) {
        node.Arguments[i].Visit(this);
      }
      _compiler.cur.AddInst(EP_InstCode.LDI_0, 0, 1);
      node.FirstOperand.Visit(this);
      _compiler.cur.AddInst(EP_InstCode.SCALL, 1);

      for(int i = node.Arguments.Length - 1; i >= 0; i--) {
        _compiler.cur.AddInst(EP_InstCode.NIP);
        d = _compiler._sp.Pop();
        _compiler._sp.Pop();
        _compiler._sp.Push(d);
      }
      return this;
    }
    protected override EP_VP2 Visit(ClassDefinition node) {
      var mc = _compiler.GetMerker(node.Reference.Descriptor);
      if(mc == null || mc.type != EP_Type.CLASS) {
        throw new ApplicationException("Unknown merker in pass 2: " + node.Reference.Descriptor.Name);
      }
      mc.scope = _compiler.ScopePush(mc);
      var ctor = _compiler.GetMerker(node.Constructor.Reference.Descriptor);
      if(ctor == null || ctor.type != EP_Type.FUNCTION) {
        throw new ApplicationException("Unknown merker in pass 2: " + node.Constructor.Reference.Descriptor.Name);
      }
      DefineFunction(node.Constructor, ctor);
      foreach(var fv in node.Members.Where(z => z.Value is FunctionDefinition && z.Name is Constant)) {
        var fm = mc.scope.GetProperty((fv.Name as Constant).Value.ToString(), EP_Type.FUNCTION);
        if(fm == null || fm.type != EP_Type.FUNCTION) {
          throw new ApplicationException("Unknown merker in pass 2: " + mc.fName + "." + (fv.Name as Constant).Value.ToString());
        }
        DefineFunction(fv.Value as FunctionDefinition, fm);
      }
      _compiler.ScopePop();
      return this;
    }
    protected override EP_VP2 Visit(Constant node) {
      int v = node.Value == null ? 0 : (int)node.Value;
      LoadConstant(node, v);
      return this;
    }
    protected override EP_VP2 Visit(Decrement node) {
      Expression a;
      EP_Compiler.Instruction d2;
      node.FirstOperand.Visit(this);
      _compiler._sp.Pop();
      if(node.Type == DecrimentType.Predecriment) {
        d2 = new EP_Compiler.Instruction(EP_InstCode.DUP) { canOptimized = true };
        _compiler._sp.Push(d2);
        _compiler.cur.AddInst(EP_InstCode.DEC, 0, 1);
        _compiler.cur.AddInst(d2);
      } else {
        _compiler.cur.AddInst(new EP_Compiler.Instruction(EP_InstCode.DUP) { canOptimized = true }, 0, 1);
        _compiler.cur.AddInst(EP_InstCode.DEC, 0, 1);
      }

      if((a = node.FirstOperand as Expression) != null) {
        Store(node, a);
      } else {
        throw new NotImplementedException();
      }
      return this;
    }
    protected override EP_VP2 Visit(Delete node) {
      return Visit(node as Expression);
    }
    protected override EP_VP2 Visit(DeleteProperty node) {
      return Visit(node as Expression);
    }
    protected override EP_VP2 Visit(Division node) {
      Arg2Op(node, EP_InstCode.DIV);
      return this;
    }
    protected override EP_VP2 Visit(Equal node) {
      Arg2Op(node, EP_InstCode.CEQ);
      return this;
    }
    protected override EP_VP2 Visit(Expression node) {
      var v = node as AssignmentOperatorCache;
      if(v != null) {
        return v.Source.Visit(this);
      }
      var t = node as This;
      if(t != null) {
        _compiler.cur.AddInst(EP_InstCode.LD_P0, 0, 1);
        return this;
      }
      return Visit(node as CodeNode);
    }
    protected override EP_VP2 Visit(FunctionDefinition node) {
      var fm = _compiler.GetMerker(node.Reference.Descriptor);
      if(fm == null || fm.type != EP_Type.FUNCTION) {
        throw new ApplicationException("Unknown merker in pass 2: " + node.Reference.Descriptor.Name);
      }

      DefineFunction(node, fm);
      return this;
    }
    protected override EP_VP2 Visit(Property node) {
      GetVariable f;
      EP_Compiler.Merker m;
      Constant c;
      EP_Compiler.Scope sc;

      if((f = node.Source as GetVariable) != null && (m = _compiler.GetMerker(f.Descriptor)) != null) {
        switch(m.type) {
        case EP_Type.REFERENCE:  //TODO: LDM_xx_C16  (m.addr+offset)
          _compiler.cur.AddInst(new EP_Compiler.Instruction(EP_InstCode.LDI_S4, m), 0, 1);
          sc = m.scope;
          break;
        case EP_Type.U8_CARR:
          node.FieldName.Visit(this);
          _compiler.cur.AddInst(new EP_Compiler.Instruction(EP_InstCode.CHECK_IDX, m), 0, 0);
          _compiler.cur.AddInst(new EP_Compiler.Instruction(EP_InstCode.LPM_U1, m, node), 1, 1);
          return this;
        case EP_Type.I8_CARR:
          node.FieldName.Visit(this);
          _compiler.cur.AddInst(new EP_Compiler.Instruction(EP_InstCode.CHECK_IDX, m), 0, 0);
          _compiler.cur.AddInst(new EP_Compiler.Instruction(EP_InstCode.LPM_S1, m, node), 1, 1);
          return this;
        case EP_Type.U16_CARR:
          node.FieldName.Visit(this);
          _compiler.cur.AddInst(new EP_Compiler.Instruction(EP_InstCode.CHECK_IDX, m), 0, 0);
          _compiler.cur.AddInst(new EP_Compiler.Instruction(EP_InstCode.LPM_U2, m, node), 1, 1);
          return this;
        case EP_Type.I16_CARR:
          node.FieldName.Visit(this);
          _compiler.cur.AddInst(new EP_Compiler.Instruction(EP_InstCode.CHECK_IDX, m), 0, 0);
          _compiler.cur.AddInst(new EP_Compiler.Instruction(EP_InstCode.LPM_S2, m, node), 1, 1);
          return this;
        case EP_Type.I32_CARR:
          node.FieldName.Visit(this);
          _compiler.cur.AddInst(new EP_Compiler.Instruction(EP_InstCode.CHECK_IDX, m), 0, 0);
          _compiler.cur.AddInst(new EP_Compiler.Instruction(EP_InstCode.LPM_S4, m, node), 1, 1);
          return this;
        default:
          throw new NotSupportedException("P2: " + node.ToString());
        }
      } else if(node.Source is This) {
        _compiler.cur.AddInst(EP_InstCode.LD_P0, 0, 1);
        sc = _compiler.cur;
      } else {
        throw new NotSupportedException("P2: " + node.ToString());
      }
      if((c = node.FieldName as Constant) != null && c.Value != null && c.Value.ValueType == JSValueType.String) {
        string pn = c.Value.ToString();
        m = sc.GetProperty(pn);
        if(m == null) {
          sc = _compiler._scope.Skip(1).FirstOrDefault();
          if(sc != null) {
            m = sc.GetProperty(pn);
          }
        }
        if(m != null) {
          EP_InstCode cmd;
          switch(m.type) {
          case EP_Type.PropB1:
            cmd = m.Addr < 256 ? EP_InstCode.LDM_B1_CS8 : EP_InstCode.LDM_B1_CS16;
            break;
          case EP_Type.PropS1:
            cmd = m.Addr < 256 ? EP_InstCode.LDM_S1_CS8 : EP_InstCode.LDM_S1_CS16;
            break;
          case EP_Type.PropU1:
            cmd = m.Addr < 256 ? EP_InstCode.LDM_U1_CS8 : EP_InstCode.LDM_U1_CS16;
            break;
          case EP_Type.PropS2:
            cmd = m.Addr < 256 ? EP_InstCode.LDM_S2_CS8 : EP_InstCode.LDM_S2_CS16;
            break;
          case EP_Type.PropU2:
            cmd = m.Addr < 256 ? EP_InstCode.LDM_U2_CS8 : EP_InstCode.LDM_U2_CS16;
            break;
          case EP_Type.PropS4:
            cmd = m.Addr < 256 ? EP_InstCode.LDM_S4_CS8 : EP_InstCode.LDM_S4_CS16;
            break;
          default:
            throw new ApplicationException("Merker " + m.ToString() + " is not property");
          }
          _compiler.cur.AddInst(new EP_Compiler.Instruction(cmd, m), 1, 1);
          return this;
        }
      }
      throw new NotSupportedException("Field name in " + node.ToString());
    }
    protected override EP_VP2 Visit(GetVariable node) {
      EP_Compiler.Merker m = _compiler.GetMerker(node.Descriptor);
      if(m == null) {
        throw new ArgumentException("undefined variable " + node.Name);
      }
      EP_Compiler.Instruction d;
      switch(m.type) {
      case EP_Type.BOOL:
        d = new EP_Compiler.Instruction(EP_InstCode.LDM_B1_C16, m, node);
        break;
      case EP_Type.UINT8:
        d = new EP_Compiler.Instruction(EP_InstCode.LDM_U1_C16, m, node);
        break;
      case EP_Type.SINT8:
        d = new EP_Compiler.Instruction(EP_InstCode.LDM_S1_C16, m, node);
        break;
      case EP_Type.UINT16:
        d = new EP_Compiler.Instruction(EP_InstCode.LDM_U2_C16, m, node);
        break;
      case EP_Type.SINT16:
        d = new EP_Compiler.Instruction(EP_InstCode.LDM_S2_C16, m, node);
        break;
      case EP_Type.SINT32:
        d = new EP_Compiler.Instruction(EP_InstCode.LDM_S4_C16, m, node);
        break;
      case EP_Type.PARAMETER:
        d = new EP_Compiler.Instruction((EP_InstCode)(EP_InstCode.LD_P0 + (byte)m.Addr));
        break;
      case EP_Type.INPUT:
      case EP_Type.OUTPUT:
        d = new EP_Compiler.Instruction(EP_InstCode.IN, m, node);
        break;
      case EP_Type.LOCAL:
        d = new EP_Compiler.Instruction((EP_InstCode)(EP_InstCode.LD_L0 + (byte)m.Addr));
        break;
      case EP_Type.FUNCTION:
        d = new EP_Compiler.Instruction(EP_InstCode.LDI_U2, m, node);
        break;
      default:
        throw new NotImplementedException(node.ToString());
      }
      _compiler.cur.AddInst(d, 0, 1);
      return this;
    }
    protected override EP_VP2 Visit(VariableReference node) {
      return Visit(node as Expression);
    }
    protected override EP_VP2 Visit(In node) {
      return Visit(node as Expression);
    }
    protected override EP_VP2 Visit(Increment node) {
      Expression a;
      EP_Compiler.Instruction d2;
      node.FirstOperand.Visit(this);
      _compiler._sp.Pop();
      if(node.Type == IncrimentType.Preincriment) {
        d2 = new EP_Compiler.Instruction(EP_InstCode.DUP) { canOptimized = true };
        _compiler._sp.Push(d2);
        _compiler.cur.AddInst(EP_InstCode.INC, 0, 1);
        _compiler.cur.AddInst(d2);
      } else {
        _compiler.cur.AddInst(new EP_Compiler.Instruction(EP_InstCode.DUP) { canOptimized = true }, 0, 1);
        _compiler.cur.AddInst(EP_InstCode.INC, 0, 1);
      }

      if((a = node.FirstOperand as Expression) != null) {
        Store(node, a);
      } else {
        throw new NotImplementedException();
      }
      return this;
    }
    protected override EP_VP2 Visit(InstanceOf node) {
      return Visit(node as Expression);
    }
    protected override EP_VP2 Visit(NiL.JS.Expressions.ObjectDefinition node) {
      return Visit(node as Expression);
    }
    protected override EP_VP2 Visit(Less node) {
      Arg2Op(node, EP_InstCode.CLT);
      return this;
    }
    protected override EP_VP2 Visit(LessOrEqual node) {
      Arg2Op(node, EP_InstCode.CLE);
      return this;
    }
    protected override EP_VP2 Visit(LogicalConjunction node) {
      EP_Compiler.Instruction j1, j2;
      node.FirstOperand.Visit(this);
      _compiler.cur.AddInst(EP_InstCode.DUP, 0, 1);
      _compiler.cur.AddInst(j1 = new EP_Compiler.Instruction(EP_InstCode.JZ), 1);
      _compiler.cur.AddInst(EP_InstCode.DROP, 1, 0);
      node.SecondOperand.Visit(this);
      _compiler.cur.AddInst(j2 = new EP_Compiler.Instruction(EP_InstCode.LABEL));
      j1._ref = j2;
      return this;
    }
    protected override EP_VP2 Visit(LogicalNegation node) {
      node.FirstOperand.Visit(this);
      _compiler.cur.AddInst(EP_InstCode.CZE, 1, 1);
      return this;

    }
    protected override EP_VP2 Visit(LogicalDisjunction node) {
      EP_Compiler.Instruction j1, j2;
      node.FirstOperand.Visit(this);
      _compiler.cur.AddInst(EP_InstCode.DUP, 0, 1);
      _compiler.cur.AddInst(j1 = new EP_Compiler.Instruction(EP_InstCode.JNZ), 1, 0);
      _compiler.cur.AddInst(EP_InstCode.DROP, 1, 0);
      node.SecondOperand.Visit(this);
      _compiler.cur.AddInst(j2 = new EP_Compiler.Instruction(EP_InstCode.LABEL));
      j1._ref = j2;
      return this;
    }
    protected override EP_VP2 Visit(Modulo node) {
      Arg2Op(node, EP_InstCode.MOD);
      return this;
    }
    protected override EP_VP2 Visit(More node) {
      Arg2Op(node, EP_InstCode.CGT);
      return this;
    }
    protected override EP_VP2 Visit(MoreOrEqual node) {
      Arg2Op(node, EP_InstCode.CGE);
      return this;
    }
    protected override EP_VP2 Visit(Multiplication node) {
      Arg2Op(node, EP_InstCode.MUL);
      return this;
    }
    protected override EP_VP2 Visit(Negation node) {
      node.FirstOperand.Visit(this);
      _compiler.cur.AddInst(EP_InstCode.NEG, 1, 1);
      return this;
    }
    protected override EP_VP2 Visit(New node) {
      return Visit(node as Expression);
    }
    protected override EP_VP2 Visit(Comma node) {
      return Visit(node as Expression);
    }
    protected override EP_VP2 Visit(BitwiseNegation node) {
      node.FirstOperand.Visit(this);
      _compiler.cur.AddInst(EP_InstCode.NOT, 1, 1);
      return this;
    }
    protected override EP_VP2 Visit(NotEqual node) {
      Arg2Op(node, EP_InstCode.CNE);
      return this;
    }
    protected override EP_VP2 Visit(NumberAddition node) {
      AddCommon(node, node.FirstOperand, node.SecondOperand);
      return this;
    }
    protected override EP_VP2 Visit(NumberLess node) {
      Arg2Op(node, EP_InstCode.CLT);
      return this;
    }
    protected override EP_VP2 Visit(NumberLessOrEqual node) {
      Arg2Op(node, EP_InstCode.CLE);
      return this;
    }
    protected override EP_VP2 Visit(NumberMore node) {
      Arg2Op(node, EP_InstCode.CGT);
      return this;
    }
    protected override EP_VP2 Visit(NumberMoreOrEqual node) {
      Arg2Op(node, EP_InstCode.CGE);
      return this;
    }
    protected override EP_VP2 Visit(BitwiseDisjunction node) {
      Arg2Op(node, EP_InstCode.OR);
      return this;
    }
    protected override EP_VP2 Visit(RegExpExpression node) {
      return Visit(node as Expression);
    }
    protected override EP_VP2 Visit(SetProperty node) {
      FunctionDefinition fd;
      GetVariable f;
      EP_Compiler.Merker m;
      Call ca;

      if((fd = node.Value as FunctionDefinition) == null) {
        EP_Compiler.Instruction d2 = new EP_Compiler.Instruction(EP_InstCode.DUP) { canOptimized = true };
        _compiler._sp.Push(d2);
        if((ca = node.Value as Call) != null && (f = ca.FirstOperand as GetVariable) != null && (new string[] { "Boolean", "Int8", "UInt8", "Int16", "UInt16", "Int32" }).Any(z => z == f.Name)) {
          if(ca.Arguments.Length > 0) {
            ca.Arguments[0].Visit(this);
          } else {
            _compiler._sp.Pop();
            return this;
          }
        } else {
          node.Value.Visit(this);
        }
        _compiler.cur.AddInst(d2);

        StoreProperty(node, node.Source, node.FieldName);
      } else {
        Constant c;
        if(node.Source is This && (c = node.FieldName as Constant) != null && c.Value != null && c.Value.ValueType == JSValueType.String) {
          m = _compiler.cur.GetProperty(c.Value.ToString());
          DefineFunction(fd, m);
        }
      }
      return this;
    }
    protected override EP_VP2 Visit(SignedShiftLeft node) {
      var c = node.SecondOperand as Constant;
      if(c == null) {
        throw new NotImplementedException(node.ToString());
      } else {
        node.FirstOperand.Visit(this);
        _compiler.cur.AddInst(new EP_Compiler.Instruction(EP_InstCode.LSL, null, c), 1, 1);
      }
      return this;
    }
    protected override EP_VP2 Visit(SignedShiftRight node) {
      var c = node.SecondOperand as Constant;
      if(c == null) {
        throw new NotImplementedException(node.ToString());
      } else {
        node.FirstOperand.Visit(this);
        _compiler.cur.AddInst(new EP_Compiler.Instruction(EP_InstCode.ASR, null, c), 1, 1);
      }
      return this;
    }
    protected override EP_VP2 Visit(StrictEqual node) {
      Arg2Op(node, EP_InstCode.CEQ);
      return this;
    }
    protected override EP_VP2 Visit(StrictNotEqual node) {
      Arg2Op(node, EP_InstCode.CNE);
      return this;
    }
    protected override EP_VP2 Visit(StringConcatenation node) {
      return Visit(node as Expression);
    }
    protected override EP_VP2 Visit(Substract node) {
      Arg2Op(node, EP_InstCode.SUB);
      return this;
    }
    protected override EP_VP2 Visit(Conditional node) {
      EP_Compiler.Instruction j1, j2, j3;
      node.FirstOperand.Visit(this);
      j1 = new EP_Compiler.Instruction(EP_InstCode.JZ, null, node.FirstOperand);
      _compiler.cur.AddInst(j1);
      _compiler._sp.Pop();
      node.Threads[0].Visit(this);
      if(node.Threads.Count > 1) {
        j2 = new EP_Compiler.Instruction(EP_InstCode.JMP);
        _compiler.cur.AddInst(j2);
        j3 = new EP_Compiler.Instruction(EP_InstCode.LABEL);
        j1._ref = j3;
        _compiler.cur.AddInst(j3);
        node.Threads[1].Visit(this);
        _compiler._sp.Pop();
        j3 = new EP_Compiler.Instruction(EP_InstCode.LABEL);
        j2._ref = j3;
        _compiler.cur.AddInst(j3);
      } else {
        j3 = new EP_Compiler.Instruction(EP_InstCode.LABEL);
        j1._ref = j3;
        _compiler.cur.AddInst(j3);
      }
      return this;
    }
    protected override EP_VP2 Visit(ConvertToBoolean node) {
      return this;
    }
    protected override EP_VP2 Visit(ConvertToInteger node) {
      return this;
    }
    protected override EP_VP2 Visit(ConvertToNumber node) {
      return Visit(node as Expression);
    }
    protected override EP_VP2 Visit(ConvertToString node) {
      return Visit(node as Expression);
    }
    protected override EP_VP2 Visit(ConvertToUnsignedInteger node) {
      return Visit(node as Expression);
    }
    protected override EP_VP2 Visit(TypeOf node) {
      return Visit(node as Expression);
    }
    protected override EP_VP2 Visit(UnsignedShiftRight node) {
      var c = node.SecondOperand as Constant;
      if(c == null) {
        throw new NotImplementedException(node.ToString());
      } else {
        node.FirstOperand.Visit(this);
        _compiler.cur.AddInst(new EP_Compiler.Instruction(EP_InstCode.LSR, null, c), 1, 1);
      }
      return this;
    }
    protected override EP_VP2 Visit(BitwiseExclusiveDisjunction node) {
      Arg2Op(node, EP_InstCode.XOR);
      return this;
    }
    protected override EP_VP2 Visit(Yield node) {
      return Visit(node as Expression);
    }
    protected override EP_VP2 Visit(Break node) {
      Loop cl;
      if(node.Label != null) {
        var l = node.Label.ToString();
        cl = _compiler.cur.loops.FirstOrDefault(z => z.labels.Any(y => y == l));
        if(cl == null) {
          cl = _compiler.cur.loops.Peek();
        }
      } else {
        cl = _compiler.cur.loops.Peek();
      }
      int tmp = _compiler._sp.Count;
      while(tmp > cl.sp1) {
        tmp--;
        _compiler.cur.AddInst(EP_InstCode.DROP);
      }
      _compiler.cur.AddInst(new EP_Compiler.Instruction(EP_InstCode.JMP) { _ref = cl.L3 });
      return this;
    }
    protected override EP_VP2 Visit(CodeBlock node) {
      EP_Compiler.Merker m;
      int sp2 = _compiler._sp.Count;

      List<Assignment> inList = new List<Assignment>();
      foreach(var vd in node.Body.Select(z => z as VariableDefinition).Where(z => z != null)) {
        inList.AddRange(vd.Initializers.Select(z => z as Assignment).Where(z => z != null));
      }

      foreach(var v in node.Variables) {
        if(v.Initializer is ClassDefinition) {
          continue;
        }
        m = _compiler.GetMerker(v);
        if(m == null) {
          throw new ApplicationException("Unknown Merker in Pass2: " + v.Name);
        }
        if(m.vd.Initializer != null) {
          m.vd.Initializer.Visit(this);
        } else if(m.type == EP_Type.LOCAL) {
          var a2 = inList.FirstOrDefault(z => (z.FirstOperand as GetVariable) != null && (z.FirstOperand as GetVariable).Descriptor == m.vd);
          if(a2 != null) {
            a2.SecondOperand.Visit(this);
            m.initialized = true;
            _compiler.cur.AddInst(new EP_Compiler.Instruction(EP_InstCode.LABEL, null, a2));
          } else {
            _compiler.cur.AddInst(EP_InstCode.LDI_0, 0, 1);
          }
        }
      }

      int sp = _compiler._sp.Count;
      for(var i = 0; i < node.Body.Length; i++) {
        SafeCodeBlock(node.Body[i], sp);
      }
      while(_compiler._sp.Count > sp2) {
        _compiler.cur.AddInst(EP_InstCode.DROP, 1, 0);
        uint idx = (uint)_compiler.cur.memory.Where(z => z.type == EP_Type.LOCAL).Count();
        if(idx == 0 || _compiler.cur.memory.RemoveAll(z => z.type == EP_Type.LOCAL && z.Addr == idx - 1) != 1) {
          throw new ApplicationException("Stack error in " + node.ToString());
        }
      }

      return this;
    }
    protected override EP_VP2 Visit(Continue node) {
      Loop cl;
      if(node.Label != null) {
        var l = node.Label.ToString();
        cl = _compiler.cur.loops.FirstOrDefault(z => z.labels.Any(y => y == l));
        if(cl == null) {
          cl = _compiler.cur.loops.Peek();
        }
      } else {
        cl = _compiler.cur.loops.Peek();
      }
      int tmp = _compiler._sp.Count;
      while(tmp > cl.sp2) {
        tmp--;
        _compiler.cur.AddInst(EP_InstCode.DROP);
      }
      _compiler.cur.AddInst(new EP_Compiler.Instruction(EP_InstCode.JMP) { _ref = cl.L2 });
      return this;
    }
    protected override EP_VP2 Visit(Debugger node) {
      return Visit(node as CodeNode);
    }
    protected override EP_VP2 Visit(DoWhile node) {
      var cl = new Loop(_compiler._sp.Count, node.Labels);
      _compiler.cur.loops.Push(cl);

      _compiler.cur.AddInst(cl.L1);

      cl.sp2 = _compiler._sp.Count;
      SafeCodeBlock(node.Body);

      _compiler.cur.AddInst(cl.L2);
      node.Condition.Visit(this);
      _compiler.cur.AddInst(new EP_Compiler.Instruction(EP_InstCode.JNZ, null, node.Condition) { _ref = cl.L1 });
      _compiler.cur.AddInst(cl.L3);
      _compiler._sp.Pop();
      _compiler.cur.loops.Pop();

      return this;
    }
    protected override EP_VP2 Visit(Empty node) {
      return Visit(node as CodeNode);
    }
    protected override EP_VP2 Visit(ForIn node) {
      return Visit(node as CodeNode);
    }
    protected override EP_VP2 Visit(ForOf node) {
      return Visit(node as CodeNode);
    }
    protected override EP_VP2 Visit(For node) {
      var cl = new Loop(_compiler._sp.Count, node.Labels.ToArray());
      _compiler.cur.loops.Push(cl);

      if(node.Initializer != null) {
        node.Initializer.Visit(this);
      }
      _compiler.cur.AddInst(cl.L1);
      node.Condition.Visit(this);
      _compiler.cur.AddInst(new EP_Compiler.Instruction(EP_InstCode.JZ, null, node.Condition) { _ref = cl.L3 });
      _compiler._sp.Pop();
      cl.sp2 = _compiler._sp.Count;
      SafeCodeBlock(node.Body);

      _compiler.cur.AddInst(cl.L2);
      if(node.Post != null) {
        node.Post.Visit(this);
      }
      _compiler.cur.AddInst(new EP_Compiler.Instruction(EP_InstCode.JMP) { _ref = cl.L1 });
      _compiler.cur.AddInst(cl.L3);

      _compiler.cur.loops.Pop();
      return this;
    }
    protected override EP_VP2 Visit(IfElse node) {
      EP_Compiler.Instruction j1, j2, j3;
      node.Condition.Visit(this);

      j1 = new EP_Compiler.Instruction(EP_InstCode.JZ, null, node.Condition);
      _compiler.cur.AddInst(j1, 1);
      SafeCodeBlock(node.Then);
      if(node.Else != null) {
        j2 = new EP_Compiler.Instruction(EP_InstCode.JMP);
        _compiler.cur.AddInst(j2);
        j3 = new EP_Compiler.Instruction(EP_InstCode.LABEL);
        j1._ref = j3;
        _compiler.cur.AddInst(j3);
        SafeCodeBlock(node.Else);
        j3 = new EP_Compiler.Instruction(EP_InstCode.LABEL);
        j2._ref = j3;
        _compiler.cur.AddInst(j3);
      } else {
        j3 = new EP_Compiler.Instruction(EP_InstCode.LABEL);
        j1._ref = j3;
        _compiler.cur.AddInst(j3);
      }
      return this;
    }
    protected override EP_VP2 Visit(InfinityLoop node) {
      var cl = new Loop(_compiler._sp.Count, node.Labels);
      _compiler.cur.loops.Push(cl);

      _compiler.cur.AddInst(cl.L1);
      _compiler.cur.AddInst(cl.L2);

      cl.sp2 = _compiler._sp.Count;
      SafeCodeBlock(node.Body);

      _compiler.cur.AddInst(new EP_Compiler.Instruction(EP_InstCode.JMP) { _ref = cl.L1 });
      _compiler.cur.AddInst(cl.L3);
      _compiler.cur.loops.Pop();
      return this;
    }
    protected override EP_VP2 Visit(LabeledStatement node) {
      return Visit(node as CodeNode);
    }
    protected override EP_VP2 Visit(Return node) {
      if(node.Value != null) {
        node.Value.Visit(this);
        _compiler.cur.AddInst(new EP_Compiler.Instruction(EP_InstCode.ST_P0, null, node), 1);
      }
      _compiler.cur.AddInst(EP_InstCode.RET);
      return this;
    }
    protected override EP_VP2 Visit(Switch node) {
      int i, j;
      var labels = new EP_Compiler.Instruction[node.Cases.Length];
      var cvs = node.Cases.Where(z => z.Statement != null).OrderBy(z => z.Index).Union(node.Cases.Where(z => z.Statement == null)).ToArray();
      node.Image.Visit(this);
      for(j = 0; j < cvs.Length; j++) {
        labels[j] = new EP_Compiler.Instruction(EP_InstCode.LABEL);
        if(cvs[j].Statement != null) {
          if(j < cvs.Length - 2) {
            _compiler.cur.AddInst(EP_InstCode.DUP);
          }
          cvs[j].Statement.Visit(this);
          _compiler.cur.AddInst(EP_InstCode.CEQ);
          _compiler.cur.AddInst(new EP_Compiler.Instruction(EP_InstCode.JNZ, null, cvs[j].Statement) { _ref = labels[j] }, 1);
        } else {
          _compiler.cur.AddInst(new EP_Compiler.Instruction(EP_InstCode.JMP) { _ref = labels[j] });
        }
      }
      _compiler._sp.Pop();

      var cl = new Loop(_compiler._sp.Count, null);
      _compiler.cur.loops.Push(cl);

      j = 0;
      for(i = 0; i < node.Body.Length; i++) {
        if(j < cvs.Length && cvs[j].Index == i) {
          _compiler.cur.AddInst(labels[j]);
          j++;
        }
        SafeCodeBlock(node.Body[i]);
      }
      while(j < labels.Length) {
        _compiler.cur.AddInst(labels[j++]);
      }
      _compiler.cur.AddInst(cl.L3);
      _compiler.cur.loops.Pop();
      return this;
    }
    protected override EP_VP2 Visit(Throw node) {
      return Visit(node as CodeNode);
    }
    protected override EP_VP2 Visit(TryCatch node) {
      return Visit(node as CodeNode);
    }
    protected override EP_VP2 Visit(VariableDefinition node) {
      int i;
      Assignment a1;
      EP_Compiler.Scope tmp;
      EP_Compiler.Merker m, mf;
      List<EP_Compiler.Merker> tmp2;
      GetVariable v;
      for(i = 0; i < node.Initializers.Length; i++) {
        if(node.Initializers[i] is GetVariable) {
          continue;
        }
        if((a1 = node.Initializers[i] as Assignment) != null && (v = a1.FirstOperand as GetVariable) != null) {
          Call ca;
          GetVariable f;
          m = _compiler.GetMerker(v.Descriptor);
          if(m == null) {
            throw new ApplicationException("Unknown merker in pass 2: " + v.Descriptor.Name);
          }
          if(m.initialized) {
            continue;
          }
          if((ca = a1.SecondOperand as Call) != null && ca.CallMode == CallMode.Construct && (f = ca.FirstOperand as GetVariable) != null) {
            if((new string[] { "Boolean", "Int8", "UInt8", "Int16", "UInt16", "Int32" }).Any(z => z == f.Name)) {
              mf = null;
            } else if((new string[] { "Uint8Array", "Uint16Array", "Int8Array", "Int16Array", "Int32Array"}).Any(z => z == f.Name)) {
              continue;
            } else {
              mf = _compiler.GetMerker(f.Descriptor);
              if(mf == null || (mf.type != EP_Type.FUNCTION && mf.type != EP_Type.CLASS)) {
                throw new ApplicationException("Unknown merker in pass 2: " + f.Descriptor.Name);
              }
              m.scope = mf.scope;
              if(mf.type == EP_Type.CLASS) {
                var mc = mf.scope.GetProperty("constructor");
                if(mc == null || mc.type != EP_Type.FUNCTION) {
                  throw new ApplicationException("Unknown merker in pass 2: " + f.Descriptor.Name + ".constructor");
                }
                mf = mc;
              }
              m.pOut = (int)(m.scope.memBlocks.Last().start + 31) / 32;
            }

            tmp = _compiler.cur;
            _compiler.cur = _compiler.initBlock;
            tmp2 = _compiler.cur.memory;
            _compiler.cur.memory = tmp.memory;
            if(mf != null) {
              CallFunction(ca, mf, v);    // Call in INIT section
              _compiler.cur.AddInst(EP_InstCode.DROP, 1, 0);
            } else {
              if(ca.Arguments.Length > 0) {
                ca.Arguments[0].Visit(this);
                Store(a1, v);
              }
            }
            _compiler.cur.memory = tmp2;
            _compiler.cur = tmp;

            continue;
          } else {
            if(m.type != EP_Type.LOCAL && m.type != EP_Type.NONE) {
              tmp = _compiler.cur;
              _compiler.cur = _compiler.initBlock;
              tmp2 = _compiler.cur.memory;
              _compiler.cur.memory = tmp.memory;
              SafeCodeBlock(node.Initializers[i]);
              _compiler.cur.memory = tmp2;
              _compiler.cur = tmp;
              continue;
            }
          }
        }
        SafeCodeBlock(node.Initializers[i]);
      }
      return this;
    }
    protected override EP_VP2 Visit(While node) {
      var cl = new Loop(_compiler._sp.Count, node.Labels);
      _compiler.cur.loops.Push(cl);

      _compiler.cur.AddInst(cl.L1);
      _compiler.cur.AddInst(cl.L2);
      node.Condition.Visit(this);
      _compiler.cur.AddInst(new EP_Compiler.Instruction(EP_InstCode.JZ, null, node.Condition) { _ref = cl.L3 });
      _compiler._sp.Pop();

      cl.sp2 = _compiler._sp.Count;
      SafeCodeBlock(node.Body, cl.sp2);

      _compiler.cur.AddInst(new EP_Compiler.Instruction(EP_InstCode.JMP) { _ref = cl.L1 });
      _compiler.cur.AddInst(cl.L3);
      _compiler.cur.loops.Pop();
      return this;
    }
    protected override EP_VP2 Visit(With node) {
      return Visit(node as CodeNode);
    }

    private void LoadConstant(CodeNode node, int v) {
      if(v == 0) {
        _compiler.cur.AddInst(new EP_Compiler.Instruction(EP_InstCode.LDI_0, null, node), 0, 1);
      } else if(v > 0) {
        if(v < 256) {
          if(v == 1) {
            _compiler.cur.AddInst(new EP_Compiler.Instruction(EP_InstCode.LDI_1, null, node), 0, 1);
          } else {
            _compiler.cur.AddInst(new EP_Compiler.Instruction(EP_InstCode.LDI_U1, null, node), 0, 1);
          }
        } else {
          if(v < 65536) {
            _compiler.cur.AddInst(new EP_Compiler.Instruction(EP_InstCode.LDI_U2, null, node), 0, 1);
          } else {
            _compiler.cur.AddInst(new EP_Compiler.Instruction(EP_InstCode.LDI_S4, null, node), 0, 1);
          }
        }
      } else {
        if(v > -128) {
          if(v == -1) {
            _compiler.cur.AddInst(new EP_Compiler.Instruction(EP_InstCode.LDI_M1, null, node), 0, 1);
          } else {
            _compiler.cur.AddInst(new EP_Compiler.Instruction(EP_InstCode.LDI_S1, null, node), 0, 1);
          }
        } else {
          if(v > -32768) {
            _compiler.cur.AddInst(new EP_Compiler.Instruction(EP_InstCode.LDI_S2, null, node), 0, 1);
          } else {
            _compiler.cur.AddInst(new EP_Compiler.Instruction(EP_InstCode.LDI_S4, null, node), 0, 1);
          }
        }
      }
    }
    private void Store(CodeNode node, Expression e) {
      GetVariable a;
      AssignmentOperatorCache a2;
      Property p;
      if((a = e as GetVariable) != null || ((a2 = e as AssignmentOperatorCache) != null && (a = a2.Source as GetVariable) != null)) {
        var m = _compiler.GetMerker(a.Descriptor);
        if(m == null) {
          throw new ApplicationException("Unknown variable: " + a.Descriptor.Name);
        }
        switch(m.type) {
        case EP_Type.BOOL:
          _compiler.cur.AddInst(new EP_Compiler.Instruction(EP_InstCode.STM_B1_C16, m, node), 1, 0);
          break;
        case EP_Type.UINT8:
        case EP_Type.SINT8:
          _compiler.cur.AddInst(new EP_Compiler.Instruction(EP_InstCode.STM_S1_C16, m, node), 1, 0);
          break;
        case EP_Type.UINT16:
        case EP_Type.SINT16:
          _compiler.cur.AddInst(new EP_Compiler.Instruction(EP_InstCode.STM_S2_C16, m, node), 1, 0);
          break;
        case EP_Type.SINT32:
          _compiler.cur.AddInst(new EP_Compiler.Instruction(EP_InstCode.STM_S4_C16, m, node), 1, 0);
          break;
        case EP_Type.PARAMETER:
          _compiler.cur.AddInst(new EP_Compiler.Instruction((EP_InstCode)(EP_InstCode.ST_P0 + (byte)m.Addr), null, node), 1, 0);
          break;
        case EP_Type.LOCAL:
          _compiler.cur.AddInst(new EP_Compiler.Instruction((EP_InstCode)(EP_InstCode.ST_L0 + (byte)m.Addr), null, node), 1, 0);
          break;
        case EP_Type.OUTPUT:
          _compiler.cur.AddInst(new EP_Compiler.Instruction(EP_InstCode.OUT, m, node), 1, 0);
          break;
        default:
          throw new NotSupportedException(node.ToString());
        }
      } else if((p = e as Property) != null) {
        StoreProperty(node, p.Source as Expression, p.FieldName as Expression);
      } else {
        throw new NotImplementedException(node.ToString());
      }
    }
    private void StoreProperty(CodeNode node, Expression src, Expression name) {
      GetVariable f;
      EP_Compiler.Merker m;
      Constant c;
      EP_Compiler.Scope sc;

      if((f = src as GetVariable) != null && (m = _compiler.GetMerker(f.Descriptor)).type == EP_Type.REFERENCE) {
        _compiler.cur.AddInst(new EP_Compiler.Instruction(EP_InstCode.LDI_S4, m), 0, 1);
        sc = m.scope;
        //TODO: STM_xx_C16  (m.addr*size+offset)
      } else if(src is This) {
        _compiler.cur.AddInst(EP_InstCode.LD_P0, 0, 1);
        sc = _compiler.cur;
      } else {
        throw new NotSupportedException(src.ToString() + " as object");
      }
      if((c = name as Constant) != null && c.Value != null && c.Value.ValueType == JSValueType.String) {
        string pn = c.Value.ToString();
        m = sc.GetProperty(pn);
        if(m == null) {
          sc = _compiler._scope.Skip(1).FirstOrDefault();
          if(sc != null) {
            m = sc.GetProperty(pn);
          }
        }
        if(m != null) {
          EP_InstCode cmd;
          switch(m.type) {
          case EP_Type.PropB1:
            cmd = m.Addr < 256 ? EP_InstCode.STM_B1_CS8 : EP_InstCode.STM_B1_CS16;
            break;
          case EP_Type.PropS1:
          case EP_Type.PropU1:
            cmd = m.Addr < 256 ? EP_InstCode.STM_S1_CS8 : EP_InstCode.STM_S1_CS16;
            break;
          case EP_Type.PropS2:
          case EP_Type.PropU2:
            cmd = m.Addr < 256 ? EP_InstCode.STM_S2_CS8 : EP_InstCode.STM_S2_CS16;
            break;
          case EP_Type.PropS4:
            cmd = m.Addr < 256 ? EP_InstCode.STM_S4_CS8 : EP_InstCode.STM_S4_CS16;
            break;
          default:
            throw new ApplicationException("Merker " + m.ToString() + " is not property");
          }
          _compiler.cur.AddInst(new EP_Compiler.Instruction(cmd, m, node), 2, 0);
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
        _compiler.cur.AddInst(EP_InstCode.INC, 1, 1);
      } else if(c1 != null && (int)(c1.Value.Value) == -1) {
        b.Visit(this);
        _compiler.cur.AddInst(EP_InstCode.DEC, 1, 1);
      } else if(c2 != null && (int)(c2.Value.Value) == 1) {
        a.Visit(this);
        _compiler.cur.AddInst(EP_InstCode.INC, 1, 1);
      } else if(c2 != null && (int)(c2.Value.Value) == -1) {
        a.Visit(this);
        _compiler.cur.AddInst(EP_InstCode.DEC, 1, 1);
      } else {
        a.Visit(this);
        b.Visit(this);
        _compiler.cur.AddInst(EP_InstCode.ADD, 2, 1);
      }
    }
    private bool CallFunction(Call node, EP_Compiler.Merker m, Expression This) {
      EP_Compiler.Instruction d;
      EP_Compiler.Merker mt;
      GetVariable v;

      if(m.scope != null) {
        var al = m.scope.memory.Where(z => z.type == EP_Type.PARAMETER).OrderBy(z => z.Addr).ToArray();
        for(int i = al.Length - 1; i >= 0; i--) {
          if(i < node.Arguments.Length) {
            node.Arguments[i].Visit(this);
          } else if(al[i].init != null) {  //TODO: check function(a, b=7)
            al[i].init.Visit(this);
          } else {
            _compiler.cur.AddInst(EP_InstCode.LDI_0, 0, 1);
          }
        }
        if(This == null) {
          _compiler.cur.AddInst(EP_InstCode.LDI_MIN, 0, 1);
        } else if((v = This as GetVariable) != null && (mt = _compiler.GetMerker(v.Descriptor)).type == EP_Type.REFERENCE) {
          _compiler.cur.AddInst(new EP_Compiler.Instruction(EP_InstCode.LDI_S4, mt), 0, 1);
        } else if(This is This) {
          _compiler.cur.AddInst(EP_InstCode.LD_P0, 0, 1);
        } else {
          throw new NotSupportedException(This.ToString() + " as this");
        }
        _compiler.cur.AddInst(new EP_Compiler.Instruction(EP_InstCode.CALL, m, node));
        for(int i = al.Length - 1; i >= 0; i--) {
          _compiler.cur.AddInst(EP_InstCode.NIP);
          d = _compiler._sp.Pop();
          _compiler._sp.Pop();
          _compiler._sp.Push(d);
        }
        return true;
      } else {
        throw new ApplicationException("undefined function: " + m.vd.Name);
      }
    }
    private void Arg2Op(Expression node, EP_InstCode c) {
      node.FirstOperand.Visit(this);
      node.SecondOperand.Visit(this);
      _compiler.cur.AddInst(c, 2, 1);
    }
    private void SafeCodeBlock(CodeNode node, int sp = -1) {
      if(node is CodeBlock) {
        node.Visit(this);
      } else {
        if(sp < 0) {
          sp = _compiler._sp.Count;
        }
        node.Visit(this);
        while(_compiler._sp.Count > sp) {
          var d = _compiler._sp.Pop();
          if(d == null || !d.canOptimized || !_compiler.cur.code.Remove(d)) {
            _compiler.cur.AddInst(EP_InstCode.DROP);
          }
        }
      }
    }
    private void DefineFunction(FunctionDefinition node, EP_Compiler.Merker fm) {
      fm.scope = _compiler.ScopePush(fm);
      for(int i = 0; i < node.Parameters.Count; i++) {
        if(i > 15) {
          throw new IndexOutOfRangeException(node.Reference.Descriptor.Name + "(.., " + node.Parameters[i].Name + " ..)" + " too many parameters");
        }
        var m = _compiler.GetMerker(node.Parameters[i]);
        if(m == null || m.type != EP_Type.PARAMETER || m.Addr != (uint)i + 1) {
          throw new ApplicationException("Unknown merker in pass 2: " + node.Reference.Descriptor.Name);
        }
      }
      node.Body.Visit(this);
      if(_compiler.cur.code.Count == 0 || _compiler.cur.code[_compiler.cur.code.Count - 1]._code.Length != 1 || _compiler.cur.code[_compiler.cur.code.Count - 1]._code[0] != (byte)EP_InstCode.RET) {
        _compiler.cur.AddInst(EP_InstCode.RET);
      }
      _compiler.ScopePop();
    }

    internal class Loop {
      public Loop(int sp1, ICollection<string> labels) {
        this.sp1 = sp1;
        this.labels = labels;
        L1 = new EP_Compiler.Instruction(EP_InstCode.LABEL);
        L2 = new EP_Compiler.Instruction(EP_InstCode.LABEL);
        L3 = new EP_Compiler.Instruction(EP_InstCode.LABEL);
      }
      public EP_Compiler.Instruction L1, L2, L3;
      public int sp1, sp2;
      public ICollection<string> labels;
    }
  }
}
