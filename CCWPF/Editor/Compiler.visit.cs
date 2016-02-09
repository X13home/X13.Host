using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using NiL.JS.Core;
using NiL.JS.Expressions;
using NiL.JS.Statements;


namespace X13.CC {
  internal partial class DP_Compiler : Visitor<DP_Compiler> {
    protected override DP_Compiler Visit(CodeNode node) {
      throw new NotSupportedException("Visit(" + node.GetType().Name + " " + node.ToString() + ")");
    }
    protected override DP_Compiler Visit(Addition node) {
      AddCommon(node, node.FirstOperand, node.SecondOperand);
      return this;
    }
    protected override DP_Compiler Visit(BitwiseConjunction node) {
      Arg2Op(node, DP_InstCode.AND);
      return this;
    }
    protected override DP_Compiler Visit(ArrayDefinition node) {
      return Visit(node as Expression);
    }
    protected override DP_Compiler Visit(Assignment node) {
      node.SecondOperand.Visit(this);
      Store(node, node.FirstOperand);
      return this;
    }
    protected override DP_Compiler Visit(Call node) {
      if(node.CallMode != CallMode.Regular) {
        throw new NotSupportedException(node.FirstOperand.ToString() + " Mode: " + node.CallMode.ToString());
      }
      GetVariable f = node.FirstOperand as GetVariable;
      DP_Inst d;
      if(f != null) {
        var m = GetMerker(f.Descriptor, DP_Type.FUNCTION);
        if(m == null) {
          throw new ArgumentException("Unknown function: " + f.Descriptor.Name);
        }
        if(m.type == DP_Type.API) {
          int i;
          for(i = m.pIn - 1; i >= 0; i--) {
            if(i < node.Arguments.Length) {
              node.Arguments[i].Visit(this);
              _sp.Pop();
            } else {
              cur.AddInst(DP_InstCode.LDI_0);
            }
          }
          cur.AddInst(new DP_Inst(DP_InstCode.API, m, node), 0, m.pOut);
          return this;
        } else if(m.type == DP_Type.FUNCTION) {
          if(m.scope != null) {
            var al = m.scope.memory.Where(z => z.type == DP_Type.PARAMETER).OrderBy(z => z.Addr).ToArray();
            if(al.Length == 0) {
              d = new DP_Inst(DP_InstCode.LDI_0);
              cur.AddInst(d);
              _sp.Push(d);
            } else {
              for(int i = al.Length - 1; i >= 0; i--) {
                if(i < node.Arguments.Length) {
                  node.Arguments[i].Visit(this);
                } else if(al[i].init != null) {  //TODO: check function(a, b=7)
                  al[i].init.Visit(this);
                } else {
                  cur.AddInst(DP_InstCode.LDI_0, 0, 1);
                }
              }
            }
            cur.AddInst(new DP_Inst(DP_InstCode.CALL, m));
            for(int i = al.Length - 1; i > 0; i--) {
              cur.AddInst(DP_InstCode.NIP);
              d = _sp.Pop();
              _sp.Pop();
              _sp.Push(d);
            }
            return this;
          } else if(_final) {
            throw new ApplicationException("undefined function: " + m.vd.Name);
          }
        } else {
          throw new ApplicationException(m.vd.Name + " is not function");
        }
      }

      if(node.Arguments.Length == 0) {
        cur.AddInst(DP_InstCode.LDI_0, 0, 1);
      } else {
        for(int i = node.Arguments.Length - 1; i >= 0; i--) {
          node.Arguments[i].Visit(this);
        }
      }

      node.FirstOperand.Visit(this);
      cur.AddInst(DP_InstCode.SCALL, 1);

      for(int i = node.Arguments.Length - 1; i > 0; i--) {
        cur.AddInst(DP_InstCode.NIP);
        d = _sp.Pop();
        _sp.Pop();
        _sp.Push(d);
      }
      return this;
    }
    protected override DP_Compiler Visit(ClassDefinition node) {
      return Visit(node as Expression);
    }
    protected override DP_Compiler Visit(Constant node) {
      int v = node.Value == null ? 0 : (int)node.Value;
      LoadConstant(node, v);
      return this;
    }
    protected override DP_Compiler Visit(Decrement node) {
      var a = node.FirstOperand as GetVariable;
      DP_Inst d2;
      if(a != null) {
        a.Visit(this);
        if(node.Type == DecrimentType.Predecriment) {
          _sp.Push(d2 = new DP_Inst(DP_InstCode.DUP) { canOptimized = true });
          cur.AddInst(DP_InstCode.DEC, 1, 1);
          cur.AddInst(d2);
        } else {
          cur.AddInst(new DP_Inst(DP_InstCode.DUP) { canOptimized = true }, 1, 1);
          cur.AddInst(DP_InstCode.DEC, 0, 1);
        }
        Store(node, a);
      } else {
        throw new NotImplementedException();
      }
      return this;
    }
    protected override DP_Compiler Visit(Delete node) {
      return Visit(node as Expression);
    }
    protected override DP_Compiler Visit(DeleteProperty node) {
      return Visit(node as Expression);
    }
    protected override DP_Compiler Visit(Division node) {
      Arg2Op(node, DP_InstCode.DIV);
      return this;
    }
    protected override DP_Compiler Visit(Equal node) {
      Arg2Op(node, DP_InstCode.CEQ);
      return this;
    }
    protected override DP_Compiler Visit(Expression node) {
      var v = node as AssignmentOperatorCache;
      if(v != null) {
        return v.Source.Visit(this);
      }
      return Visit(node as CodeNode);
    }
    protected override DP_Compiler Visit(FunctionDefinition node) {
      var fm = GetMerker(node.Reference.Descriptor, DP_Type.FUNCTION);
      ScopePush(fm);
      fm.scope = cur;
      fm.scope.entryPoint = fm;
      for(int i = 0; i < node.Parameters.Count; i++) {
        var m = new DP_Merker() { Addr = (uint)i, type = DP_Type.PARAMETER, vd = node.Parameters[i], init = node.Parameters[i].Initializer };
        cur.memory.Add(m);
      }
      node.Body.Visit(this);
      if(cur.code.Count == 0 || cur.code[cur.code.Count - 1]._code.Length != 1 || cur.code[cur.code.Count - 1]._code[0] != (byte)DP_InstCode.RET) {
        cur.AddInst(DP_InstCode.RET);
      }
      ScopePop();
      return this;
    }
    protected override DP_Compiler Visit(Property node) {
      return Visit(node as Expression);
    }
    protected override DP_Compiler Visit(GetVariable node) {
      DP_Merker m = GetMerker(node.Descriptor);
      if(m == null) {
        throw new ArgumentException("undefined variable " + node.Name);
      }
      DP_Inst d;
      switch(m.type) {
      case DP_Type.BOOL:
        d = new DP_Inst(DP_InstCode.LDM_B1_C16, m, node);
        break;
      case DP_Type.UINT8:
        d = new DP_Inst(DP_InstCode.LDM_U1_C16, m, node);
        break;
      case DP_Type.SINT8:
        d = new DP_Inst(DP_InstCode.LDM_S1_C16, m, node);
        break;
      case DP_Type.UINT16:
        d = new DP_Inst(DP_InstCode.LDM_U2_C16, m, node);
        break;
      case DP_Type.SINT16:
        d = new DP_Inst(DP_InstCode.LDM_S2_C16, m, node);
        break;
      case DP_Type.SINT32:
        d = new DP_Inst(DP_InstCode.LDM_S4_C16, m, node);
        break;
      case DP_Type.PARAMETER:
        d = new DP_Inst((DP_InstCode)(DP_InstCode.LD_P0 + (byte)m.Addr));
        break;
      case DP_Type.INPUT:
      case DP_Type.OUTPUT:
        d = new DP_Inst(DP_InstCode.IN, m, node);
        break;
      case DP_Type.LOCAL:
        d = new DP_Inst((DP_InstCode)(DP_InstCode.LD_L0 + (byte)m.Addr));
        break;
      case DP_Type.FUNCTION:
        d = new DP_Inst(DP_InstCode.LDI_U2, m, node);
        break;
      default:
        throw new NotImplementedException(node.ToString());
      }
      cur.AddInst(d, 0, 1);
      return this;
    }
    protected override DP_Compiler Visit(VariableReference node) {
      return Visit(node as Expression);
    }
    protected override DP_Compiler Visit(In node) {
      return Visit(node as Expression);
    }
    protected override DP_Compiler Visit(Increment node) {
      var a = node.FirstOperand as GetVariable;
      DP_Inst d2;
      if(a != null) {
        a.Visit(this);
        _sp.Pop();
        if(node.Type == IncrimentType.Preincriment) {
          d2 = new DP_Inst(DP_InstCode.DUP) { canOptimized = true };
          _sp.Push(d2);
          cur.AddInst(DP_InstCode.INC, 0, 1);
          cur.AddInst(d2);
        } else {
          cur.AddInst(new DP_Inst(DP_InstCode.DUP) { canOptimized = true }, 0, 1);
          cur.AddInst(DP_InstCode.INC, 0, 1);
        }
        Store(node, a);
      } else {
        throw new NotImplementedException();
      }
      return this;
    }
    protected override DP_Compiler Visit(InstanceOf node) {
      return Visit(node as Expression);
    }
    protected override DP_Compiler Visit(NiL.JS.Expressions.ObjectDefinition node) {
      return Visit(node as Expression);
    }
    protected override DP_Compiler Visit(Less node) {
      Arg2Op(node, DP_InstCode.CLT);
      return this;
    }
    protected override DP_Compiler Visit(LessOrEqual node) {
      Arg2Op(node, DP_InstCode.CLE);
      return this;
    }
    protected override DP_Compiler Visit(LogicalConjunction node) {
      DP_Inst j1, j2;
      node.FirstOperand.Visit(this);
      cur.AddInst(DP_InstCode.DUP, 0, 1);
      cur.AddInst(j1 = new DP_Inst(DP_InstCode.JZ));
      node.SecondOperand.Visit(this);
      cur.AddInst(DP_InstCode.AND_L, 2, 1);
      cur.AddInst(j2 = new DP_Inst(DP_InstCode.LABEL));
      j1._ref = j2;
      return this;
    }
    protected override DP_Compiler Visit(LogicalNegation node) {
      node.FirstOperand.Visit(this);
      cur.AddInst(DP_InstCode.NOT_L, 1, 1);
      return this;

    }
    protected override DP_Compiler Visit(LogicalDisjunction node) {
      DP_Inst j1, j2;
      node.FirstOperand.Visit(this);
      cur.AddInst(DP_InstCode.DUP, 0, 1);
      cur.AddInst(j1=new DP_Inst(DP_InstCode.JNZ), 1, 0);
      node.SecondOperand.Visit(this);
      cur.AddInst(DP_InstCode.OR_L, 2, 1);
      cur.AddInst(j2 = new DP_Inst(DP_InstCode.LABEL));
      j1._ref = j2;
      return this;
    }
    protected override DP_Compiler Visit(Modulo node) {
      Arg2Op(node, DP_InstCode.MOD);
      return this;
    }
    protected override DP_Compiler Visit(More node) {
      Arg2Op(node, DP_InstCode.CGT);
      return this;
    }
    protected override DP_Compiler Visit(MoreOrEqual node) {
      Arg2Op(node, DP_InstCode.CGE);
      return this;
    }
    protected override DP_Compiler Visit(Multiplication node) {
      Arg2Op(node, DP_InstCode.MUL);
      return this;
    }
    protected override DP_Compiler Visit(Negation node) {
      node.FirstOperand.Visit(this);
      cur.AddInst(DP_InstCode.NEG, 1, 1);
      return this;
    }
    protected override DP_Compiler Visit(New node) {
      return Visit(node as Expression);
    }
    protected override DP_Compiler Visit(Comma node) {
      return Visit(node as Expression);
    }
    protected override DP_Compiler Visit(BitwiseNegation node) {
      node.FirstOperand.Visit(this);
      cur.AddInst(DP_InstCode.NOT, 1, 1);
      return this;
    }
    protected override DP_Compiler Visit(NotEqual node) {
      Arg2Op(node, DP_InstCode.CNE);
      return this;
    }
    protected override DP_Compiler Visit(NumberAddition node) {
      AddCommon(node, node.FirstOperand, node.SecondOperand);
      return this;
    }
    protected override DP_Compiler Visit(NumberLess node) {
      Arg2Op(node, DP_InstCode.CLT);
      return this;
    }
    protected override DP_Compiler Visit(NumberLessOrEqual node) {
      Arg2Op(node, DP_InstCode.CLE);
      return this;
    }
    protected override DP_Compiler Visit(NumberMore node) {
      Arg2Op(node, DP_InstCode.CGT);
      return this;
    }
    protected override DP_Compiler Visit(NumberMoreOrEqual node) {
      Arg2Op(node, DP_InstCode.CGE);
      return this;
    }
    protected override DP_Compiler Visit(BitwiseDisjunction node) {
      Arg2Op(node, DP_InstCode.OR);
      return this;
    }
    protected override DP_Compiler Visit(RegExpExpression node) {
      return Visit(node as Expression);
    }
    protected override DP_Compiler Visit(SetProperty node) {
      return Visit(node as Expression);
    }
    protected override DP_Compiler Visit(SignedShiftLeft node) {
      var c = node.SecondOperand as Constant;
      if(c == null) {
        throw new NotImplementedException(node.ToString());
      } else {
        node.FirstOperand.Visit(this);
        cur.AddInst(new DP_Inst(DP_InstCode.LSL, null, c), 1, 1);
      }
      return this;
    }
    protected override DP_Compiler Visit(SignedShiftRight node) {
      var c = node.SecondOperand as Constant;
      if(c == null) {
        throw new NotImplementedException(node.ToString());
      } else {
        node.FirstOperand.Visit(this);
        cur.AddInst(new DP_Inst(DP_InstCode.ASR, null, c), 1, 1);
      }
      return this;
    }
    protected override DP_Compiler Visit(StrictEqual node) {
      Arg2Op(node, DP_InstCode.CEQ);
      return this;
    }
    protected override DP_Compiler Visit(StrictNotEqual node) {
      Arg2Op(node, DP_InstCode.CNE);
      return this;
    }
    protected override DP_Compiler Visit(StringConcatenation node) {
      return Visit(node as Expression);
    }
    protected override DP_Compiler Visit(Substract node) {
      Arg2Op(node, DP_InstCode.SUB);
      return this;
    }
    protected override DP_Compiler Visit(Conditional node) {
      DP_Inst j1, j2, j3;
      node.FirstOperand.Visit(this);
      j1 = new DP_Inst(DP_InstCode.JZ, null, node.FirstOperand);
      cur.AddInst(j1);
      _sp.Pop();
      node.Threads[0].Visit(this);
      if(node.Threads.Count > 1) {
        j2 = new DP_Inst(DP_InstCode.JMP);
        cur.AddInst(j2);
        j3 = new DP_Inst(DP_InstCode.LABEL);
        j1._ref = j3;
        cur.AddInst(j3);
        node.Threads[1].Visit(this);
        _sp.Pop();
        j3 = new DP_Inst(DP_InstCode.LABEL);
        j2._ref = j3;
        cur.AddInst(j3);
      } else {
        j3 = new DP_Inst(DP_InstCode.LABEL);
        j1._ref = j3;
        cur.AddInst(j3);
      }
      return this;
    }
    protected override DP_Compiler Visit(ConvertToBoolean node) {
      return Visit(node as Expression);
    }
    protected override DP_Compiler Visit(ConvertToInteger node) {
      return Visit(node as Expression);
    }
    protected override DP_Compiler Visit(ConvertToNumber node) {
      return Visit(node as Expression);
    }
    protected override DP_Compiler Visit(ConvertToString node) {
      return Visit(node as Expression);
    }
    protected override DP_Compiler Visit(ConvertToUnsignedInteger node) {
      return Visit(node as Expression);
    }
    protected override DP_Compiler Visit(TypeOf node) {
      return Visit(node as Expression);
    }
    protected override DP_Compiler Visit(UnsignedShiftRight node) {
      var c = node.SecondOperand as Constant;
      if(c == null) {
        throw new NotImplementedException(node.ToString());
      } else {
        node.FirstOperand.Visit(this);
        cur.AddInst(new DP_Inst(DP_InstCode.LSR, null, c), 1, 1);
      }
      return this;
    }
    protected override DP_Compiler Visit(BitwiseExclusiveDisjunction node) {
      Arg2Op(node, DP_InstCode.XOR);
      return this;
    }
    protected override DP_Compiler Visit(Yield node) {
      return Visit(node as Expression);
    }
    protected override DP_Compiler Visit(Break node) {
      DP_Loop cl;
      if(node.Label != null) {
        var l = node.Label.ToString();
        cl = cur.loops.FirstOrDefault(z => z.labels.Any(y => y == l));
        if(cl == null) {
          cl = cur.loops.Peek();
        }
      } else {
        cl = cur.loops.Peek();
      }
      int tmp = _sp.Count;
      while(tmp > cl.sp1) {
        tmp--;
        cur.AddInst(DP_InstCode.DROP);
      }
      cur.AddInst(new DP_Inst(DP_InstCode.JMP) { _ref = cl.L3 });
      return this;
    }
    protected override DP_Compiler Visit(CodeBlock node) {
      DP_Merker m;
      uint addr;
      DP_Type type;
      int sp2 = _sp.Count;

      List<Assignment> inList = new List<Assignment>();
      foreach(var vd in node.Body.Select(z => z as VariableDefinition).Where(z => z != null)) {
        inList.AddRange(vd.Initializers.Select(z => z as Assignment).Where(z => z != null));
      }

      foreach(var v in node.Variables) {
        m = null;
        addr = uint.MaxValue;
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
          addr = (uint)cur.memory.Where(z => z.type == DP_Type.LOCAL).Count();
          if(addr < 16) {
            type = DP_Type.LOCAL;
          } else {
            throw new ArgumentOutOfRangeException("Too many local variables: " + v.Name + "in \n" + v.Owner.ToString());
          }
        } else {
          type = DP_Type.SINT32;
          addr = uint.MaxValue;
        }
        m = GetMerker(v, type);
        m.Addr = addr;

        if(m.vd.Initializer != null) {
          m.vd.Initializer.Visit(this);
        } else if(m.type == DP_Type.LOCAL) {
          var a2 = inList.FirstOrDefault(z => (z.FirstOperand as GetVariable) != null && (z.FirstOperand as GetVariable).Descriptor == m.vd);
          if(a2 != null) {
            a2.SecondOperand.Visit(this);
            m.initialized = true;
          } else {
            cur.AddInst(DP_InstCode.LDI_0, 0, 1);
          }
        }
      }

      int sp = _sp.Count;
      for(var i = 0; i < node.Body.Length; i++) {
        SafeCodeBlock(node.Body[i], sp);
      }
      while(_sp.Count > sp2) {
        cur.AddInst(DP_InstCode.DROP, 1, 0);
        uint idx = (uint)cur.memory.Where(z => z.type == DP_Type.LOCAL).Count();
        if(idx == 0 || cur.memory.RemoveAll(z => z.type == DP_Type.LOCAL && z.Addr == idx - 1) != 1) {
          throw new ApplicationException("Stack error in " + node.ToString());
        }
      }

      return this;
    }
    protected override DP_Compiler Visit(Continue node) {
      DP_Loop cl;
      if(node.Label != null) {
        var l = node.Label.ToString();
        cl = cur.loops.FirstOrDefault(z => z.labels.Any(y => y == l));
        if(cl == null) {
          cl = cur.loops.Peek();
        }
      } else {
        cl = cur.loops.Peek();
      }
      int tmp = _sp.Count;
      while(tmp > cl.sp2) {
        tmp--;
        cur.AddInst(DP_InstCode.DROP);
      }
      cur.AddInst(new DP_Inst(DP_InstCode.JMP) { _ref = cl.L2 });
      return this;
    }
    protected override DP_Compiler Visit(Debugger node) {
      return Visit(node as CodeNode);
    }
    protected override DP_Compiler Visit(DoWhile node) {
      var cl = new DP_Loop(_sp.Count, node.Labels);
      cur.loops.Push(cl);

      cur.AddInst(cl.L1);

      cl.sp2 = _sp.Count;
      SafeCodeBlock(node.Body);

      cur.AddInst(cl.L2);
      node.Condition.Visit(this);
      cur.AddInst(new DP_Inst(DP_InstCode.JNZ, null, node.Condition) { _ref = cl.L1 });
      cur.AddInst(cl.L3);
      _sp.Pop();
      cur.loops.Pop();

      return this;
    }
    protected override DP_Compiler Visit(Empty node) {
      return Visit(node as CodeNode);
    }
    protected override DP_Compiler Visit(ForIn node) {
      return Visit(node as CodeNode);
    }
    protected override DP_Compiler Visit(ForOf node) {
      return Visit(node as CodeNode);
    }
    protected override DP_Compiler Visit(For node) {
      var cl = new DP_Loop(_sp.Count, node.Labels.ToArray());
      cur.loops.Push(cl);

      if(node.Initializator != null) {
        node.Initializator.Visit(this);
      }
      cur.AddInst(cl.L1);
      node.Condition.Visit(this);
      cur.AddInst(new DP_Inst(DP_InstCode.JZ, null, node.Condition) { _ref = cl.L3 });
      _sp.Pop();
      cl.sp2 = _sp.Count;
      SafeCodeBlock(node.Body);

      cur.AddInst(cl.L2);
      if(node.Post != null) {
        node.Post.Visit(this);
      }
      cur.AddInst(new DP_Inst(DP_InstCode.JMP) { _ref = cl.L1 });
      cur.AddInst(cl.L3);

      cur.loops.Pop();
      return this;
    }
    protected override DP_Compiler Visit(IfElse node) {
      DP_Inst j1, j2, j3;
      node.Condition.Visit(this);

      j1 = new DP_Inst(DP_InstCode.JZ, null, node.Condition);
      cur.AddInst(j1);
      _sp.Pop();
      SafeCodeBlock(node.Then);
      if(node.Else != null) {
        j2 = new DP_Inst(DP_InstCode.JMP);
        cur.AddInst(j2);
        j3 = new DP_Inst(DP_InstCode.LABEL);
        j1._ref = j3;
        cur.AddInst(j3);
        SafeCodeBlock(node.Else);
        j3 = new DP_Inst(DP_InstCode.LABEL);
        j2._ref = j3;
        cur.AddInst(j3);
      } else {
        j3 = new DP_Inst(DP_InstCode.LABEL);
        j1._ref = j3;
        cur.AddInst(j3);
      }
      return this;
    }
    protected override DP_Compiler Visit(InfinityLoop node) {
      var cl = new DP_Loop(_sp.Count, node.Labels);
      cur.loops.Push(cl);

      cur.AddInst(cl.L1);
      cur.AddInst(cl.L2);

      cl.sp2 = _sp.Count;
      SafeCodeBlock(node.Body);

      cur.AddInst(new DP_Inst(DP_InstCode.JMP) { _ref = cl.L1 });
      cur.AddInst(cl.L3);
      cur.loops.Pop();
      return this;
    }
    protected override DP_Compiler Visit(LabeledStatement node) {
      return Visit(node as CodeNode);
    }
    protected override DP_Compiler Visit(Return node) {
      if(node.Value != null) {
        node.Value.Visit(this);
        cur.AddInst(new DP_Inst(DP_InstCode.ST_P0, null, node), 1);
      }
      cur.AddInst(DP_InstCode.RET);
      return this;
    }
    protected override DP_Compiler Visit(Switch node) {
      int i, j;
      var labels = new DP_Inst[node.Cases.Length];
      var cvs = node.Cases.Where(z => z.Statement != null).OrderBy(z => z.Index).Union(node.Cases.Where(z => z.Statement == null)).ToArray();
      node.Image.Visit(this);
      for(j = 0; j < cvs.Length; j++) {
        labels[j] = new DP_Inst(DP_InstCode.LABEL);
        if(cvs[j].Statement != null) {
          if(j < cvs.Length - 2) {
            cur.AddInst(DP_InstCode.DUP);
          }
          cvs[j].Statement.Visit(this);
          cur.AddInst(DP_InstCode.CEQ);
          cur.AddInst(new DP_Inst(DP_InstCode.JNZ, null, cvs[j].Statement) { _ref = labels[j] }, 1);
        } else {
          cur.AddInst(new DP_Inst(DP_InstCode.JMP) { _ref = labels[j] });
        }
      }
      _sp.Pop();

      var cl = new DP_Loop(_sp.Count, null);
      cur.loops.Push(cl);

      j = 0;
      for(i = 0; i < node.Body.Length; i++) {
        if(j < cvs.Length && cvs[j].Index == i) {
          cur.AddInst(labels[j]);
          j++;
        }
        SafeCodeBlock(node.Body[i]);
      }
      while(j < labels.Length) {
        cur.AddInst(labels[j++]);
      }
      cur.AddInst(cl.L3);
      cur.loops.Pop();
      return this;
    }
    protected override DP_Compiler Visit(Throw node) {
      return Visit(node as CodeNode);
    }
    protected override DP_Compiler Visit(TryCatch node) {
      return Visit(node as CodeNode);
    }
    protected override DP_Compiler Visit(VariableDefinition node) {
      int i;
      Assignment a1;
      for(i = 0; i < node.Initializers.Length; i++) {
        if(node.Initializers[i] is GetVariable) {
          continue;
        } else if((a1 = node.Initializers[i] as Assignment) != null) {
          var m = GetMerker((a1.FirstOperand as GetVariable).Descriptor);
          if(m != null && m.initialized) {
            continue;
          }
        }
        node.Initializers[i].Visit(this);
      }
      return this;
    }
    protected override DP_Compiler Visit(While node) {
      var cl = new DP_Loop(_sp.Count, node.Labels);
      cur.loops.Push(cl);

      cur.AddInst(cl.L1);
      cur.AddInst(cl.L2);
      node.Condition.Visit(this);
      cur.AddInst(new DP_Inst(DP_InstCode.JZ, null, node.Condition) { _ref = cl.L3 });
      _sp.Pop();

      cl.sp2 = _sp.Count;
      SafeCodeBlock(node.Body, cl.sp2);

      cur.AddInst(new DP_Inst(DP_InstCode.JMP) { _ref = cl.L1 });
      cur.AddInst(cl.L3);
      cur.loops.Pop();
      return this;
    }
    protected override DP_Compiler Visit(With node) {
      return Visit(node as CodeNode);
    }
  }
}
