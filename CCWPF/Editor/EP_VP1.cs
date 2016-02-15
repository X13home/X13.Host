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
      return Visit(node as Expression);
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
      fm.scope = _compiler.ScopePush(fm);
      fm.scope.entryPoint = fm;
      for(int i = 0; i < node.Parameters.Count; i++) {
        if(i > 15) {
          throw new IndexOutOfRangeException(node.Reference.Descriptor.Name + "(.., " + node.Parameters[i].Name + " ..)" + " too many parameters");
        }
        var m = _compiler.DefineMerker(node.Parameters[i], EP_Type.PARAMETER);
        m.Addr = (uint)i + 1;
      }
      node.Body.Visit(this);
      _compiler.ScopePop();
      return this;
    }
    protected override EP_VP1 Visit(Property node) {
      node.Source.Visit(this);
      node.FieldName.Visit(this);
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
      node.Value.Visit(this);
      node.Source.Visit(this);
      node.FieldName.Visit(this);
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
      return Visit(node as Expression);
    }
    protected override EP_VP1 Visit(ConvertToInteger node) {
      return Visit(node as Expression);
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
      EP_Compiler.Merker m;

      foreach(var v in node.Variables) {
        m = _compiler.DefineMerker(v);

        if(m.vd.Initializer != null) {
          m.vd.Initializer.Visit(this);
        }
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
      if(node.Initializator != null) {
        node.Initializator.Visit(this);
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
      int i;
      Assignment a1;
      EP_Compiler.Merker m;
      GetVariable v;

      for(i = 0; i < node.Initializers.Length; i++) {
        if(node.Initializers[i] is GetVariable) {
          continue;
        }
        if((a1 = node.Initializers[i] as Assignment) != null && (v = a1.FirstOperand as GetVariable) != null) {
          Call ca;
          GetVariable f;
          if((ca = a1.SecondOperand as Call) != null && ca.CallMode == CallMode.Construct && (f = ca.FirstOperand as GetVariable) != null && f.Descriptor.Name == "Int32Array") {
            Constant len;
            if(ca.Arguments.Length != 1 || (len = ca.Arguments[0] as Constant) == null || !len.Value.IsNumber) {
              throw new NotSupportedException("supported only new Int32Array(constant length)");
            }
            m = _compiler.DefineMerker(v.Descriptor, EP_Type.REFERENCE);
            m.type = EP_Type.REFERENCE;
            m.pOut = (int)len.Value;
            continue;
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
  }
}
