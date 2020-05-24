using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Serialization;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Text;

namespace Breeze.Sharp.Json {
  public class JsonQueryExpressionVisitor : ExpressionVisitor {

    public int? Skip { get; private set; } = null;
    public int? Take { get; private set; } = null;
    public bool? InlineCount { get; private set; } = null;
    public List<string> OrderBy { get; private set; } = null;
    [JsonConverter(typeof(PlainJsonStringConverter))]
    public string Where { get; private set; } = null;
    public List<string> Select { get; private set; } = null;
    public List<string> Expand { get; private set; } = null;
    public Dictionary<string, string> Parameters { get; private set; } = null;

    /// <summary> for building Where clause </summary>
    private StringBuilder sb;
    private ListExpressionVisitor selectVisitor;
    private ListExpressionVisitor expandVisitor;

    /// <summary> Translate the EntityQuery expression into a JSON string </summary>
    public static string Translate(Expression expression) {

      var visitor = new JsonQueryExpressionVisitor();
      visitor.VisitRoot(expression);

      var jsonSettings = new JsonSerializerSettings {
        ReferenceLoopHandling = ReferenceLoopHandling.Ignore,
        ContractResolver = new CamelCasePropertyNamesContractResolver(),
        NullValueHandling = NullValueHandling.Ignore
        //ContractResolver = Json.QueryContractResolver.Instance
      };

      var json = JsonConvert.SerializeObject(visitor, Formatting.None, jsonSettings);
      return json;
    }

    private JsonQueryExpressionVisitor() { }

    /// <summary> Populate this visitor's properties from the expression </summary>
    protected void VisitRoot(Expression expression) {
      this.sb = new StringBuilder();
      this.selectVisitor = new ListExpressionVisitor();
      this.expandVisitor = new ListExpressionVisitor();

      this.Visit(expression);
      if (sb.Length > 2) {
        this.Where = sb.ToString();
      }
      if (this.selectVisitor.list.Count > 0) {
        this.Select = this.selectVisitor.list;
      }
      if (this.expandVisitor.list.Count > 0) {
        this.Expand = this.expandVisitor.list;
      }
    }

    private static Expression StripQuotes(Expression e) {
      while (e.NodeType == ExpressionType.Quote) {
        e = ((UnaryExpression)e).Operand;
      }
      return e;
    }

    private static bool IsResourceSetExpression(Expression expr) {
      return (int)expr.NodeType == 10000;
    }


    protected override Expression VisitMethodCall(MethodCallExpression m) {
      var methodName = m.Method.Name;
      if (m.Method.DeclaringType == typeof(Queryable) && methodName == "Where") {
        if (!IsResourceSetExpression(m.Arguments[0])) {
          this.Visit(m.Arguments[0]);
          LambdaExpression lambda = (LambdaExpression)StripQuotes(m.Arguments[1]);
          this.Visit(lambda.Body);
        }
        return m;
      } else if (methodName == "Select") {
        this.Visit(m.Arguments[0]);
        LambdaExpression lambda = (LambdaExpression)StripQuotes(m.Arguments[1]);
        this.selectVisitor.Visit(lambda.Body);
        return m;
      } else if (methodName == "Expand") {
        var inner = StripQuotes(m.Arguments[0]);
        if (inner is LambdaExpression lambda) {
          this.expandVisitor.Visit(lambda.Body);
        } else {
          this.expandVisitor.Visit(inner);
        }
        var operand = ((UnaryExpression)m.Object).Operand;
        this.Visit(operand);
        return m;
      } else if (methodName == "Take") {
        if (this.ParseTakeExpression(m)) {
          return this.Visit(m.Arguments[0]);
        }
      } else if (methodName == "Skip") {
        if (this.ParseSkipExpression(m)) {
          return this.Visit(m.Arguments[0]);
        }
      } else if (methodName == "IncludeTotalCount") {
        this.InlineCount = true;
        var operand = ((UnaryExpression)m.Object).Operand;
        return this.Visit(operand);
      } else if (methodName == "OrderBy") {
        if (this.ParseOrderByExpression(m, null)) {
          return this.Visit(m.Arguments[0]);
        }
      } else if (methodName == "ThenBy") {
        if (this.ParseOrderByExpression(m, null)) {
          return this.Visit(m.Arguments[0]);
        }
      } else if (methodName == "OrderByDescending") {
        if (this.ParseOrderByExpression(m, "DESC")) {
          return this.Visit(m.Arguments[0]);
        }
      }

      throw new NotSupportedException(string.Format("The method '{0}' is not supported", methodName));
    }

    protected override Expression VisitUnary(UnaryExpression u) {
      switch (u.NodeType) {
        case ExpressionType.Not:
          sb.Append(" NOT ");
          this.Visit(u.Operand);
          break;
        case ExpressionType.Convert:
          this.Visit(u.Operand);
          break;
        default:
          throw new NotSupportedException(string.Format("The unary operator '{0}' is not supported", u.NodeType));
      }
      return u;
    }


    /// <summary>
    /// 
    /// </summary>
    /// <param name="b"></param>
    /// <returns></returns>
    protected override Expression VisitBinary(BinaryExpression b) {

      switch (b.NodeType) {
        case ExpressionType.And:
        case ExpressionType.AndAlso:
          return VisitBinaryAndOr(b, "and");

        case ExpressionType.Or:
        case ExpressionType.OrElse:
          return VisitBinaryAndOr(b, "or");

        case ExpressionType.Equal:
          return VisitBinaryComparison(b, "eq");

        case ExpressionType.NotEqual:
          return VisitBinaryComparison(b, "ne");

        case ExpressionType.LessThan:
          return VisitBinaryComparison(b, "lt");

        case ExpressionType.LessThanOrEqual:
          return VisitBinaryComparison(b, "le");

        case ExpressionType.GreaterThan:
          return VisitBinaryComparison(b, "gt");

        case ExpressionType.GreaterThanOrEqual:
          return VisitBinaryComparison(b, "ge");

        default:
          throw new NotSupportedException(string.Format("The binary operator '{0}' is not supported", b.NodeType));
      }
    }

    private Expression VisitBinaryAndOr(BinaryExpression b, string op) {
      sb.Append("{\"").Append(op).Append("\":[");
      this.Visit(b.Left);
      sb.Append(",");
      this.Visit(b.Right);
      sb.Append("]}");
      return b;
    }

    private Expression VisitBinaryComparison(BinaryExpression b, string op) {
      sb.Append('{');
      this.Visit(b.Left);
      if (op == "eq") {
        sb.Append(":");
        this.Visit(b.Right);
      } else {
        sb.Append(":{\"").Append(op).Append("\": ");
        this.Visit(b.Right);
        sb.Append("}");
      }
      sb.Append('}');
      return b;
    }

    protected override Expression VisitConstant(ConstantExpression c) {
      if (c.Value == null) {
        sb.Append("null");
      } else {
        switch (Type.GetTypeCode(c.Value.GetType())) {
          case TypeCode.Boolean:
            sb.Append(((bool)c.Value) ? "true" : "false");
            break;

          case TypeCode.String:
            sb.Append('"').Append(c.Value).Append('"');
            break;

          case TypeCode.DateTime:
            sb.Append('"').Append(c.Value).Append('"');
            break;

          case TypeCode.Object:
            return base.VisitConstant(c);

          default:
            sb.Append(c.Value);
            break;
        }
      }

      return c;
    }

    protected override Expression VisitMember(MemberExpression m) {
      if (m.Expression != null && m.Expression.NodeType == ExpressionType.Parameter) {
        sb.Append('"').Append(m.Member.Name).Append('"');
        return m;
      } else if (m.Expression != null) {
        var ne = Visit(m.Expression);
        if (ne is ConstantExpression ce) {
          return VisitMemberInfo(m.Member, ce.Value);
        }
      } else {
        return VisitMemberInfo(m.Member, null);
      }

      throw new NotSupportedException(string.Format("The member '{0}' is not supported", m.Member.Name));
    }

    private Expression VisitMemberInfo(MemberInfo memberInfo, object container) {
      if (memberInfo is FieldInfo fi) {
        object value = fi.GetValue(container);
        return Visit(Expression.Constant(value));
      } else if (memberInfo is PropertyInfo pi) {
        object value = pi.GetValue(container, null);
        return Visit(Expression.Constant(value));
      }
      throw new NotSupportedException(string.Format("The MemberInfo '{0}' is not supported", memberInfo));
    }

    protected bool IsNullConstant(Expression exp) {
      return (exp.NodeType == ExpressionType.Constant && ((ConstantExpression)exp).Value == null);
    }

    private bool ParseOrderByExpression(MethodCallExpression expression, string order = null) {
      UnaryExpression unary = (UnaryExpression)expression.Arguments[1];
      LambdaExpression lambdaExpression = (LambdaExpression)unary.Operand;
      if (!string.IsNullOrEmpty(order)) {
        order = " " + order.Trim();
      }

      //lambdaExpression = (LambdaExpression)Evaluator.PartialEval(lambdaExpression);

      MemberExpression body = ((UnaryExpression)lambdaExpression.Body).Operand as MemberExpression;
      if (body != null) {
        if (OrderBy == null)
          OrderBy = new List<string>();

        OrderBy.Add(string.Format("{0}{1}", body.Member.Name, order));

        return true;
      }

      return false;
    }

    private bool ParseTakeExpression(MethodCallExpression expression) {
      return ParseIntExpression(expression, i => Take = i);
    }

    private bool ParseSkipExpression(MethodCallExpression expression) {
      return ParseIntExpression(expression, i => Skip = i);
    }

    private bool ParseIntExpression(MethodCallExpression expression, Action<int> action) {
      ConstantExpression intExpression = (ConstantExpression)expression.Arguments[1];
      int size;
      if (int.TryParse(intExpression.Value.ToString(), out size)) {
        action(size);
        return true;
      }
      return false;
    }
  }

  /// <summary> Convert strings to JSON without quotes </summary>
  public class PlainJsonStringConverter : JsonConverter {
    public override bool CanConvert(Type objectType) {
      return objectType == typeof(string);
    }
    public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer) {
      return reader.Value;
    }
    public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer) {
      writer.WriteRawValue((string)value);
    }
  }
}
