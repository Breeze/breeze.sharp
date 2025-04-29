using System;
using System.Collections.Generic;
using System.Linq.Expressions;

namespace Breeze.Sharp.Json {
  /// <summary> Add expression values to the list </summary>
  class ListExpressionVisitor : ExpressionVisitor {
    public List<string> list { get; private set; } = new List<string>();

    protected override Expression VisitMember(MemberExpression m) {
      if (m.Expression != null && m.Expression.NodeType == ExpressionType.Parameter) {
        list.Add(m.Member.Name);
        return m;
      } else if (m.Expression != null && m.Expression is MemberExpression mex) {
        var exp = new List<string> { m.Member.Name };
        while (m.Expression is MemberExpression) {
          m = (MemberExpression)m.Expression;
          exp.Insert(0, m.Member.Name);
        }
        list.Add(string.Join(".", exp));
        return m;
      }

      throw new NotSupportedException(string.Format("The member '{0}' is not supported", m.Member.Name));
    }

    protected override Expression VisitConstant(ConstantExpression c) {
      if (c.Value is string stringValue) {
        list.Add(stringValue);
        return c;
      }
      throw new NotSupportedException(string.Format("Constant expression '{0}' is not supported", c.Value));
    }
  }
}
