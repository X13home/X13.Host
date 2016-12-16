using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using NiL.JS.Core;
using NiL.JS.Expressions;
using NiL.JS.Statements;

namespace X13.CC {
  internal class EP_VP1 : Visitor<EP_VP1> {
    private readonly EP_Compiler _compiler;

    public EP_VP1(EP_Compiler compiler) {
      _compiler = compiler;
    }

    protected override EP_VP1 Visit(CodeNode node) {
      return this;
    }
    protected override EP_VP1 Visit(Addition node) {
      node.SecondOperand.Visit(this);
      node.FirstOperand.Visit(this);
      return this;
    }
    protected override EP_VP1 Visit(BitwiseConjunction node) {
      node.SecondOperand.Visit(this);
      node.FirstOperand.Visit(this);
      return this;
    }
    protected override EP_VP1 Visit(ArrayDefinition node) {
      return Visit(node as Expression);
    }
    protected override EP_VP1 Visit(Assignment node) {
      node.SecondOperand.Visit(this);
      node.FirstOperand.Visit(this);
      return this;
    }
    protected override EP_VP1 Visit(Call node) {
      for(int i = node.Arguments.Length - 1; i >= 0; i--) {
        node.Arguments[i].Visit(this);
      }
      node.FirstOperand.Visit(this);
      return this;
    }
    protected override EP_VP1 Visit(ClassDefinition node) {
      var mc = _compiler.DefineMerker(node.Reference.Descriptor, EP_Type.CLASS);
      mc.scope = _compiler.ScopePush(mc);
      var ctor = _compiler.DefineMerker(node.Constructor.Reference.Descriptor, EP_Type.FUNCTION);
      DefineFunction(node.Constructor, ctor, mc.scope.memory);
      foreach(var m in ctor.scope.memory.Where(z=>IsProperty(z.type))){
        int idx=m.fName.IndexOf(".constructor");
        m.fName=m.fName.Remove(idx, 12);
      }
      mc.scope.memory.AddRange(ctor.scope.memory.Where(z => IsProperty(z.type)));
      mc.scope.AllocatFields();
      
      foreach(var fv in node.Members.Where(z => z.Value is FunctionDefinition && z.Name is Constant)) {
        var fm = mc.scope.GetProperty((fv.Name as Constant).Value.ToString(), EP_Type.FUNCTION);
        DefineFunction(fv.Value as FunctionDefinition, fm, mc.scope.memory);
      }
      _compiler.ScopePop();
      return this;
    }
    protected override EP_VP1 Visit(Constant node) {
      return this;
    }
    protected override EP_VP1 Visit(Decrement node) {
      node.FirstOperand.Visit(this);
      return this;
    }
    protected override EP_VP1 Visit(Delete node) {
      return Visit(node as Expression);
    }
    protected override EP_VP1 Visit(DeleteProperty node) {
      return Visit(node as Expression);
    }
    protected override EP_VP1 Visit(Division node) {
      node.SecondOperand.Visit(this);
      node.FirstOperand.Visit(this);
      return this;
    }
    protected override EP_VP1 Visit(Equal node) {
      node.SecondOperand.Visit(this);
      node.FirstOperand.Visit(this);
      return this;
    }
    protected override EP_VP1 Visit(Expression node) {
      var v = node as AssignmentOperatorCache;
      if(v != null) {
        return v.Source.Visit(this);
      }
      if(node is This) {
        return this;
      }
      return Visit(node as CodeNode);
    }
    protected override EP_VP1 Visit(FunctionDefinition node) {
      var fm = _compiler.DefineMerker(node.Reference.Descriptor, EP_Type.FUNCTION);
      DefineFunction(node, fm, null);
      return this;
    }
    protected override EP_VP1 Visit(Property node) {
      node.Source.Visit(this);
      //node.FieldName.Visit(this);
      //Constant c;
      //if(node.FieldName is This && (c = node.FieldName as Constant) != null && c.Value != null && c.Value.ValueType == JSValueType.String) {
      //  _compiler.cur.GetProperty(c.Value.ToString());
      //}
      return this;
    }
    protected override EP_VP1 Visit(GetVariable node) {
      return this;
    }
    protected override EP_VP1 Visit(VariableReference node) {
      return Visit(node as Expression);
    }
    protected override EP_VP1 Visit(In node) {
      return Visit(node as Expression);
    }
    protected override EP_VP1 Visit(Increment node) {
      node.FirstOperand.Visit(this);
      return this;
    }
    protected override EP_VP1 Visit(InstanceOf node) {
      return Visit(node as Expression);
    }
    protected override EP_VP1 Visit(NiL.JS.Expressions.ObjectDefinition node) {
      return Visit(node as Expression);
    }
    protected override EP_VP1 Visit(Less node) {
      node.SecondOperand.Visit(this);
      node.FirstOperand.Visit(this);
      return this;
    }
    protected override EP_VP1 Visit(LessOrEqual node) {
      node.SecondOperand.Visit(this);
      node.FirstOperand.Visit(this);
      return this;
    }
    protected override EP_VP1 Visit(LogicalConjunction node) {
      node.SecondOperand.Visit(this);
      node.FirstOperand.Visit(this);
      return this;
    }
    protected override EP_VP1 Visit(LogicalNegation node) {
      node.FirstOperand.Visit(this);
      return this;
    }
    protected override EP_VP1 Visit(LogicalDisjunction node) {
      node.SecondOperand.Visit(this);
      node.FirstOperand.Visit(this);
      return this;
    }
    protected override EP_VP1 Visit(Modulo node) {
      node.SecondOperand.Visit(this);
      node.FirstOperand.Visit(this);
      return this;
    }
    protected override EP_VP1 Visit(More node) {
      node.SecondOperand.Visit(this);
      node.FirstOperand.Visit(this);
      return this;
    }
    protected override EP_VP1 Visit(MoreOrEqual node) {
      node.SecondOperand.Visit(this);
      node.FirstOperand.Visit(this);
      return this;
    }
    protected override EP_VP1 Visit(Multiplication node) {
      node.SecondOperand.Visit(this);
      node.FirstOperand.Visit(this);
      return this;
    }
    protected override EP_VP1 Visit(Negation node) {
      node.FirstOperand.Visit(this);
      return this;
    }
    protected override EP_VP1 Visit(New node) {
      return Visit(node as Expression);
    }
    protected override EP_VP1 Visit(Comma node) {
      return Visit(node as Expression);
    }
    protected override EP_VP1 Visit(BitwiseNegation node) {
      node.FirstOperand.Visit(this);
      return this;
    }
    protected override EP_VP1 Visit(NotEqual node) {
      node.SecondOperand.Visit(this);
      node.FirstOperand.Visit(this);
      return this;
    }
    protected override EP_VP1 Visit(NumberAddition node) {
      node.SecondOperand.Visit(this);
      node.FirstOperand.Visit(this);
      return this;
    }
    protected override EP_VP1 Visit(NumberLess node) {
      node.SecondOperand.Visit(this);
      node.FirstOperand.Visit(this);
      return this;
    }
    protected override EP_VP1 Visit(NumberLessOrEqual node) {
      node.SecondOperand.Visit(this);
      node.FirstOperand.Visit(this);
      return this;
    }
    protected override EP_VP1 Visit(NumberMore node) {
      node.SecondOperand.Visit(this);
      node.FirstOperand.Visit(this);
      return this;
    }
    protected override EP_VP1 Visit(NumberMoreOrEqual node) {
      node.SecondOperand.Visit(this);
      node.FirstOperand.Visit(this);
      return this;
    }
    protected override EP_VP1 Visit(BitwiseDisjunction node) {
      node.SecondOperand.Visit(this);
      node.FirstOperand.Visit(this);
      return this;
    }
    protected override EP_VP1 Visit(RegExpExpression node) {
      return Visit(node as Expression);
    }
    protected override EP_VP1 Visit(SetProperty node) {
      Constant c, c1;
      FunctionDefinition fd;
      GetVariable f;
      EP_Compiler.Merker m;
      EP_Compiler.Scope sc;
      Call ca;

      if((f = node.Source as GetVariable) != null && (m = _compiler.GetMerker(f.Descriptor)).type == EP_Type.REFERENCE) {
        sc = m.scope;
      } else if(node.Source is This) {
        sc = _compiler.cur;
      } else {
        throw new NotSupportedException(node.Source.ToString() + " as object");
      }
      if((c = node.FieldName as Constant) == null || c.Value == null || c.Value.ValueType != JSValueType.String) {
        throw new NotSupportedException(node.Source.ToString() + "." + node.FieldName.ToString() + " as FieldName");
      }

      if((ca = node.Value as Call) != null && (f = ca.FirstOperand as GetVariable) != null) {
        EP_Type t;
        switch(f.Name) {
        case "Boolean":
          t = EP_Type.PropB1;
          break;
        case "Int8":
          t = EP_Type.PropS1;
          break;
        case "UInt8":
          t = EP_Type.PropU1;
          break;
        case "Int16":
          t = EP_Type.PropS2;
          break;
        case "UInt16":
          t = EP_Type.PropU2;
          break;
        case "Int32":
          t = EP_Type.PropS4;
          break;
        default:
          t = EP_Type.NONE;
          break;
        }
        if(t == EP_Type.NONE && ca.CallMode == CallMode.Construct) {
          throw new NotSupportedException("enclosed constructor"+ node.ToString());
        } else {
          if(t == EP_Type.NONE) {
            t = EP_Type.PropS4;
          }
          sc.GetProperty(c.Value.ToString(), t);
        }
      } else if((c1 = node.Value as Constant) != null && c1.Value != null && c1.Value.ValueType == JSValueType.Boolean) {
        sc.GetProperty(c.Value.ToString(), EP_Type.PropB1);
      } else if((fd = node.Value as FunctionDefinition) != null) {
        m=sc.GetProperty(c.Value.ToString(), EP_Type.FUNCTION);
        DefineFunction(fd, m, sc.memory);
      } else {
        node.Value.Visit(this);
        sc.GetProperty(c.Value.ToString(), EP_Type.PropS4);
      }
      return this;
    }
    protected override EP_VP1 Visit(SignedShiftLeft node) {
      node.SecondOperand.Visit(this);
      node.FirstOperand.Visit(this);
      return this;
    }
    protected override EP_VP1 Visit(SignedShiftRight node) {
      node.SecondOperand.Visit(this);
      node.FirstOperand.Visit(this);
      return this;
    }
    protected override EP_VP1 Visit(StrictEqual node) {
      node.SecondOperand.Visit(this);
      node.FirstOperand.Visit(this);
      return this;
    }
    protected override EP_VP1 Visit(StrictNotEqual node) {
      node.SecondOperand.Visit(this);
      node.FirstOperand.Visit(this);
      return this;
    }
    protected override EP_VP1 Visit(StringConcatenation node) {
      return Visit(node as Expression);
    }
    protected override EP_VP1 Visit(Substract node) {
      node.SecondOperand.Visit(this);
      node.FirstOperand.Visit(this);
      return this;
    }
    protected override EP_VP1 Visit(Conditional node) {
      node.FirstOperand.Visit(this);
      node.Threads[0].Visit(this);
      node.Threads[1].Visit(this);
      return this;
    }
    protected override EP_VP1 Visit(ConvertToBoolean node) {
      return this;
    }
    protected override EP_VP1 Visit(ConvertToInteger node) {
      return this;
    }
    protected override EP_VP1 Visit(ConvertToNumber node) {
      return Visit(node as Expression);
    }
    protected override EP_VP1 Visit(ConvertToString node) {
      return Visit(node as Expression);
    }
    protected override EP_VP1 Visit(ConvertToUnsignedInteger node) {
      return Visit(node as Expression);
    }
    protected override EP_VP1 Visit(TypeOf node) {
      return Visit(node as Expression);
    }
    protected override EP_VP1 Visit(UnsignedShiftRight node) {
      node.SecondOperand.Visit(this);
      node.FirstOperand.Visit(this);
      return this;
    }
    protected override EP_VP1 Visit(BitwiseExclusiveDisjunction node) {
      node.SecondOperand.Visit(this);
      node.FirstOperand.Visit(this);
      return this;
    }
    protected override EP_VP1 Visit(Yield node) {
      return Visit(node as Expression);
    }
    protected override EP_VP1 Visit(Break node) {
      return this;
    }
    protected override EP_VP1 Visit(CodeBlock node) {
      //EP_Compiler.Merker m;
      FunctionDefinition fd;

      foreach(var v in node.Variables) {
        if((fd = v.Initializer as FunctionDefinition) != null) {
          var fm = _compiler.DefineMerker(v, EP_Type.FUNCTION);
          DefineFunction(fd, fm, null);
        }
        //m = _compiler.DefineMerker(v);
        //if(m.vd.Initializer != null) {
        //  m.vd.Initializer.Visit(this);
        //}
      }

      for(var i = 0; i < node.Body.Length; i++) {
        node.Body[i].Visit(this);
      }
      return this;
    }
    protected override EP_VP1 Visit(Continue node) {
      return this;
    }
    protected override EP_VP1 Visit(Debugger node) {
      return Visit(node as CodeNode);
    }
    protected override EP_VP1 Visit(DoWhile node) {
      node.Body.Visit(this);
      node.Condition.Visit(this);
      return this;
    }
    protected override EP_VP1 Visit(Empty node) {
      return Visit(node as CodeNode);
    }
    protected override EP_VP1 Visit(ForIn node) {
      return Visit(node as CodeNode);
    }
    protected override EP_VP1 Visit(ForOf node) {
      return Visit(node as CodeNode);
    }
    protected override EP_VP1 Visit(For node) {
      if(node.Initializer != null) {
        node.Initializer.Visit(this);
      }
      if(node.Condition != null) {
        node.Condition.Visit(this);
      }
      node.Body.Visit(this);

      if(node.Post != null) {
        node.Post.Visit(this);
      }
      return this;
    }
    protected override EP_VP1 Visit(IfElse node) {
      node.Condition.Visit(this);
      node.Then.Visit(this);
      if(node.Else != null) {
        node.Else.Visit(this);
      }
      return this;
    }
    protected override EP_VP1 Visit(InfinityLoop node) {
      node.Body.Visit(this);
      return this;
    }
    protected override EP_VP1 Visit(LabeledStatement node) {
      return Visit(node as CodeNode);
    }
    protected override EP_VP1 Visit(Return node) {
      if(node.Value != null) {
        node.Value.Visit(this);
      }
      return this;
    }
    protected override EP_VP1 Visit(Switch node) {
      int i, j;
      var cvs = node.Cases.Where(z => z.Statement != null).OrderBy(z => z.Index).Union(node.Cases.Where(z => z.Statement == null)).ToArray();
      node.Image.Visit(this);
      for(j = 0; j < cvs.Length; j++) {
        if(cvs[j].Statement != null) {
          cvs[j].Statement.Visit(this);
        }
      }
      j = 0;
      for(i = 0; i < node.Body.Length; i++) {
        node.Body[i].Visit(this);
      }
      return this;
    }
    protected override EP_VP1 Visit(Throw node) {
      return Visit(node as CodeNode);
    }
    protected override EP_VP1 Visit(TryCatch node) {
      return Visit(node as CodeNode);
    }
    protected override EP_VP1 Visit(VariableDefinition node) {
      Assignment a1;
      EP_Compiler.Merker m;
      GetVariable v;

      for(int i = 0; i < node.Initializers.Length; i++) {
        if((v=node.Initializers[i] as GetVariable)!=null) {
          m = _compiler.DefineMerker(v.Descriptor);
          continue;
        }
        if((a1 = node.Initializers[i] as Assignment) != null && (v = a1.FirstOperand as GetVariable) != null) {
          if(v.Descriptor.LexicalScope) {
            _compiler.DefineMerker(v.Descriptor);  // Local
            a1.SecondOperand.Visit(this);
            continue;
          } else {
            Call ca;
            GetVariable f;
            Constant c;
            if((ca = a1.SecondOperand as Call) != null && (f = ca.FirstOperand as GetVariable) != null) {
              EP_Type t;
              switch(f.Name) {
              case "Boolean":
                t = EP_Type.BOOL;
                break;
              case "Int8":
                t = EP_Type.SINT8;
                break;
              case "UInt8":
                t = EP_Type.UINT8;
                break;
              case "Int16":
                t = EP_Type.SINT16;
                break;
              case "UInt16":
                t = EP_Type.UINT16;
                break;
              case "Int32":
                t = EP_Type.SINT32;
                break;
              case "Uint8Array":
              case "Uint16Array":
              case "Int8Array":
              case "Int16Array":
              case "Int32Array":
                if(ca.Arguments.Count() == 1 && ca.Arguments[0] is ArrayDefinition) {
                  List<byte> narr=new List<byte>();
                  int cnt = 0;
                  t = EP_Type.NONE;
                  switch(f.Name) {
                  case "Uint8Array":
                    t = EP_Type.U8_CARR;
                    foreach(var el in (ca.Arguments[0] as ArrayDefinition).Elements) {
                      narr.Add((byte)el.Evaluate(null));
                      cnt++;
                    }
                    break;
                  case "Int8Array":
                    t = EP_Type.I8_CARR;
                    foreach(var el in (ca.Arguments[0] as ArrayDefinition).Elements) {
                      narr.Add((byte)el.Evaluate(null));
                      cnt++;
                    }
                    break;
                  case "Uint16Array":
                    t = EP_Type.U16_CARR;
                    foreach(var el in (ca.Arguments[0] as ArrayDefinition).Elements) {
                      narr.AddRange(BitConverter.GetBytes((ushort)el.Evaluate(null)));
                      cnt++;
                    }
                    break;
                  case "Int16Array":
                    t = EP_Type.I16_CARR;
                    foreach(var el in (ca.Arguments[0] as ArrayDefinition).Elements) {
                      narr.AddRange(BitConverter.GetBytes((ushort)el.Evaluate(null)));
                      cnt++;
                    }
                    break;
                  case "Int32Array":
                    t = EP_Type.I32_CARR;
                    foreach(var el in (ca.Arguments[0] as ArrayDefinition).Elements) {
                      narr.AddRange(BitConverter.GetBytes((int)el.Evaluate(null)));
                      cnt++;
                    }
                    break;
                  }
                  EP_Compiler.Instruction i1=new EP_Compiler.Instruction(narr.ToArray());
                  _compiler.dataBlock.AddInst(i1, 0, 0);
                  m = _compiler.DefineMerker(v.Descriptor, t);
                  i1._param = m;
                  i1._cn = v;
                  m.init = ca.Arguments[0];
                  m.pOut = cnt;
                  continue;
                } else {
                  throw new NotSupportedException("P1: " + ca.ToString());
                }
              default:
                t = EP_Type.NONE;
                break;
              }
              if(t == EP_Type.NONE && ca.CallMode == CallMode.Construct) {
                m = _compiler.DefineMerker(v.Descriptor, EP_Type.REFERENCE);
                m.type = EP_Type.REFERENCE;
                continue;
              } else {
                m = _compiler.DefineMerker(v.Descriptor, t);
              }
            } else if((c = a1.SecondOperand as Constant) != null && c.Value != null && c.Value.ValueType == JSValueType.Boolean) {
              m = _compiler.DefineMerker(v.Descriptor, EP_Type.BOOL);
            } else {
              m = _compiler.DefineMerker(v.Descriptor, EP_Type.SINT32);
            }
          }
        }

        node.Initializers[i].Visit(this);
      }
      return this;
    }
    protected override EP_VP1 Visit(While node) {
      node.Condition.Visit(this);
      node.Body.Visit(this);
      return this;
    }
    protected override EP_VP1 Visit(With node) {
      return Visit(node as CodeNode);
    }

    private void DefineFunction(FunctionDefinition node, EP_Compiler.Merker fm, List<EP_Compiler.Merker> parentMemory) {
      fm.scope = _compiler.ScopePush(fm);
      if(parentMemory != null) {
        fm.scope.memory.AddRange(parentMemory.Where(z => IsProperty(z.type)));
      }

      for(int i = 0; i < node.Parameters.Count; i++) {
        if(i > 15) {
          throw new IndexOutOfRangeException(node.Reference.Descriptor.Name + "(.., " + node.Parameters[i].Name + " ..)" + " too many parameters");
        }
        var m = _compiler.DefineMerker(node.Parameters[i], EP_Type.PARAMETER);
        m.Addr = (uint)i + 1;
      }
      node.Body.Visit(this);
      if(parentMemory==null) {
        fm.scope.AllocatFields();
      }
      _compiler.ScopePop();
    }
    private static bool IsProperty(EP_Type type) {
      return type == EP_Type.PropB1 || type == EP_Type.PropS1 || type == EP_Type.PropS2 || type == EP_Type.PropS4 || type == EP_Type.PropU1 || type == EP_Type.PropU2;
    }

  }
}
