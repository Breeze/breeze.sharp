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
    //public List<string> OrderBy { get; private set; } = null;
    [JsonConverter(typeof(PlainJsonStringConverter))]
    public string Where { get; private set; } = null;

    public List<string> Select { get; private set; } = null;
    public List<string> Expand { get; private set; } = null;
    public Stack<string> OrderBy { get; private set; } = null;
    public Dictionary<string, object> Parameters { get; private set; } = null;

    /// <summary> Holds the place of an ordering by the projected value until the Select is known. </summary>
    private const string IdentityOrderBy = "\0identity";
    /// <summary> for building Where clause </summary>
    private StringBuilder sb;

    private ListExpressionVisitor selectVisitor;
    private ListExpressionVisitor expandVisitor;
    private ListExpressionVisitor orderByVisitor;

    /// <summary> Translate the EntityQuery expression into a JSON string </summary>
    public static string Translate(Expression expression, out string parameters) {

      var visitor = new JsonQueryExpressionVisitor();
      visitor.VisitRoot(expression);

      var jsonSettings = new JsonSerializerSettings {
        ReferenceLoopHandling = ReferenceLoopHandling.Ignore,
        ContractResolver = new CamelCasePropertyNamesContractResolver(),
        NullValueHandling = NullValueHandling.Ignore
        //ContractResolver = Json.QueryContractResolver.Instance
      };

      var json = JsonConvert.SerializeObject(visitor, Formatting.None, jsonSettings);

      //without a server-side custom model binder for 'Customer?{"parameters":{"companyName":"C"}}' I cannot have parameters with right values,
      //so I have to use this hack
      if (visitor.Parameters?.Count > 0)
        parameters = string.Join("&", visitor.Parameters.Select(kvp => string.Format("{0}={1}", kvp.Key, Uri.EscapeDataString(kvp.Value.ToString()))));
      else
        parameters = null;

      return json;
    }

    private JsonQueryExpressionVisitor() { }

    /// <summary> Populate this visitor's properties from the expression </summary>
    protected void VisitRoot(Expression expression) {
      this.sb = new StringBuilder();
      this.selectVisitor = new ListExpressionVisitor();
      this.expandVisitor = new ListExpressionVisitor();
      this.orderByVisitor = new ListExpressionVisitor();

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
      ResolveIdentityOrderBy();
      if (this.orderByVisitor.list.Count > 0) {
        this.OrderBy = new Stack<string>(this.orderByVisitor.list);
      }
    }

    protected override Expression VisitMethodCall(MethodCallExpression m) {
      var methodName = m.Method.Name;
      if (m.Method.DeclaringType == typeof(Queryable) && methodName == "Where") {
        if (!IsResourceSetExpression(m.Arguments[0])) {
          this.Visit(m.Arguments[0]);
          LambdaExpression lambda = (LambdaExpression)StripQuotes(m.Arguments[1]);
          var savedSb = sb;
          sb = new StringBuilder();

          this.VisitPredicate(lambda.Body);
          var clause = sb.ToString();
          if (!string.IsNullOrWhiteSpace(clause)) {
            if (this.Where == null) {
              this.Where = clause;
            } else {
              this.Where = $"{{\"and\":[{this.Where},{clause}]}}";
            }
          }
          this.sb = savedSb;
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
        if (this.ParseOrderByExpression(m, "desc")) {
          return this.Visit(m.Arguments[0]);
        }
      } else if (methodName == "ThenByDescending") {
        if (this.ParseOrderByExpression(m, "desc")) {
          return this.Visit(m.Arguments[0]);
        }
      } else if (methodName == "Contains") {
        return this.VisitStringMethod(m, methodName);
      } else if (methodName == "StartsWith") {
        return this.VisitStringMethod(m, methodName);
      } else if (methodName == "EndsWith") {
        return this.VisitStringMethod(m, methodName);
      } else if (methodName == "Any") {
        return this.VisitAnyAll(m, methodName);
      } else if (methodName == "All") {
        return this.VisitAnyAll(m, methodName);
      } else if (methodName == "AddQueryOption") {
        if (this.Parameters == null) {
          this.Parameters = new Dictionary<string, object>();
        }
        this.Parameters.Add(GetValue(m.Arguments[0]).ToString(), GetValue(m.Arguments[1]));
        return this.Visit(m.Object);
      } else if (m.Object is MemberExpression) {
        object result = Expression.Lambda(m).Compile().DynamicInvoke();
        return this.VisitConstant(Expression.Constant(result));
        //} else if (methodName == "AddQueryOption") {
        //  if (this.ParseAddQueryOptionExpression(m)) {
        //    var operand = ((UnaryExpression)m.Object).Operand;
        //    this.Visit(operand);
        //    return m;
        //  }
      }

      throw new NotSupportedException(string.Format("The method '{0}' is not supported", methodName));
    }

    protected object GetValue(Expression node) {
      if (node.NodeType == ExpressionType.Constant) {
        return ((ConstantExpression)node).Value;
      } else {
        return Expression.Lambda(node).Compile().DynamicInvoke();
      }
    }

    protected override Expression VisitNew(NewExpression node) {
      object result = Expression.Lambda(node).Compile().DynamicInvoke();
      return this.VisitConstant(Expression.Constant(result));
    }

    protected override Expression VisitUnary(UnaryExpression u) {
      switch (u.NodeType) {
        case ExpressionType.Not:
          sb.Append("{\"not\":");
          this.VisitPredicate(u.Operand);
          sb.Append("}");
          break;

        case ExpressionType.Convert:
          this.Visit(u.Operand);
          break;

        default:
          throw new NotSupportedException(string.Format("The unary operator '{0}' is not supported", u.NodeType));
      }
      return u;
    }

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

    protected override Expression VisitConstant(ConstantExpression c) {
      if (c.Value == null) {
        sb.Append("null");
      } else {
        Type type = c.Value.GetType();

        switch (Type.GetTypeCode(type)) {
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
            if (type == typeof(Guid)) {
              sb.Append('"').Append(c.Value).Append('"');
              break;
            }

            return base.VisitConstant(c);

          default:
            if (type.IsEnum) {
              sb.Append('"').Append(c.Value).Append('"');
            } else {
              sb.Append(c.Value);
            }
            break;
        }

      }

      return c;
    }

    protected bool IsNullConstant(Expression exp) {
      return (exp.NodeType == ExpressionType.Constant && ((ConstantExpression)exp).Value == null);
    }

    protected override Expression VisitMember(MemberExpression m) {
      if (m.Expression != null && (m.Expression.NodeType == ExpressionType.Parameter || m.Expression.NodeType == ExpressionType.Convert)) {
        //Base case: simple property like o.City
        sb.Append('"').Append(m.Member.Name).Append('"');
        return m;
      } else if (m.Expression != null) {
        if (m.Expression is MemberExpression me && me.Expression.NodeType == ExpressionType.Parameter) {
          //1-level nested property like o.Location.City
          sb.Append('"').Append(me.Member.Name).Append('.').Append(m.Member.Name).Append('"');
          return m;
        } else if (m.Expression is MemberExpression mme) {
          //NEW: Handles deeper nesting like o.Supplier.Location.City
          var parts = new Stack<string>();
          Expression current = m;
          while (current is MemberExpression mm) {
            parts.Push(mm.Member.Name);
            current = mm.Expression;
          }
          if (current is ParameterExpression || current is UnaryExpression) {
            sb.Append('"').Append(string.Join(".", parts)).Append('"');
            return m;
          }
        }

        var ne = Visit(m.Expression);
        if (ne is ConstantExpression ce) {
          return VisitMemberInfo(m.Member, ce.Value);
        }
      } else {
        return VisitMemberInfo(m.Member, null);
      }

      throw new NotSupportedException($"The member '{m}' is not supported");
    }

    private Expression VisitBinaryAndOr(BinaryExpression b, string op) {
      sb.Append("{\"").Append(op).Append("\":[");
      this.VisitPredicate(b.Left);
      sb.Append(",");
      this.VisitPredicate(b.Right);
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

    /// <summary>
    /// Visits an expression sitting where the query expects a predicate: the body of a Where or
    /// Any/All lambda, either side of an and/or, or the operand of a not.
    /// </summary>
    /// <remarks>
    /// A boolean property used on its own is already a complete predicate - Where(o => o.Discontinued)
    /// means o.Discontinued == true - but VisitMember writes only the property name, leaving the
    /// clause as the JSON string "Discontinued" where the server expects an object. Writing it as
    /// {"Discontinued":true} is the same query in the form the server understands.
    /// </remarks>
    private Expression VisitPredicate(Expression node) {
      if (StripConverts(node) is MemberExpression member && IsBooleanPropertyPath(member)) {
        sb.Append('{');
        this.Visit(member);
        sb.Append(":true}");
        return node;
      }
      return this.Visit(node);
    }

    /// <summary>
    /// True when the member names a boolean property reached from the query's parameter, as
    /// opposed to a captured variable whose value is resolved while translating.
    /// </summary>
    private static bool IsBooleanPropertyPath(MemberExpression member) {
      if (member.Type != typeof(bool)) {
        return false;
      }
      Expression current = member;
      while (current is MemberExpression m) {
        current = StripConverts(m.Expression);
      }
      return current is ParameterExpression;
    }

    private static Expression StripConverts(Expression e) {
      while (e is UnaryExpression u &&
             (u.NodeType == ExpressionType.Convert || u.NodeType == ExpressionType.ConvertChecked)) {
        e = u.Operand;
      }
      return e;
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

    private bool ParseOrderByExpression(MethodCallExpression expression, string order = null) {
      if (expression.Arguments.Count < 2) {
        return false;
      }
      UnaryExpression unary = (UnaryExpression)expression.Arguments[1];
      LambdaExpression lambdaExpression = (LambdaExpression)unary.Operand;

      Expression body = lambdaExpression.Body is MemberExpression mex ? mex :
        lambdaExpression.Body is UnaryExpression uex ? uex.Operand :
        lambdaExpression.Body;

      if (body is ParameterExpression) {
        // Ordering by the value itself, as in Select(o => o.OrderDate).OrderByDescending(d => d).
        // What that value is only becomes known once the Select has been visited, which happens
        // later - the tree is walked from the outermost call inwards - so hold a place in the
        // list and fill it in at the end.
        orderByVisitor.list.Add(IdentityOrderBy + OrderSuffix(order));
        return true;
      }

      if (body != null) {
        // resolve expression into string and add to list
        var len = orderByVisitor.list.Count;
        orderByVisitor.Visit(body);
        // if order specified, update the newly-added string to include it
        if (!string.IsNullOrEmpty(order) && orderByVisitor.list.Count > len) {
          orderByVisitor.list[len] += OrderSuffix(order);
        }
        return true;
      }

      return false;
    }

    private static string OrderSuffix(string order) {
      return string.IsNullOrEmpty(order) ? string.Empty : $" {order.Trim()}";
    }

    /// <summary>
    /// Replaces each held place from an ordering by the projected value with the member that was
    /// projected, dropping it when there is no single member to name.
    /// </summary>
    /// <remarks>
    /// Ordering a projection by its own value is ordering the source by the member the projection
    /// selected, so Select(o => o.OrderDate).OrderByDescending(d => d) is "OrderDate desc". A
    /// projection to several members, or an ordering of entities by themselves, names nothing the
    /// server can sort on, so the clause is dropped rather than guessed at.
    /// </remarks>
    private void ResolveIdentityOrderBy() {
      var list = orderByVisitor.list;
      var projected = selectVisitor.list.Count == 1 ? selectVisitor.list[0] : null;

      for (var i = list.Count - 1; i >= 0; i--) {
        if (!list[i].StartsWith(IdentityOrderBy, StringComparison.Ordinal)) {
          continue;
        }
        if (projected == null) {
          list.RemoveAt(i);
        } else {
          list[i] = projected + list[i].Substring(IdentityOrderBy.Length);
        }
      }
    }

    //private bool ParseAddQueryOptionExpression(MethodCallExpression expression) {
    //  if (Parameters == null)
    //    Parameters = new Dictionary<string, object>();

    //  Parameters.Add(((ConstantExpression)expression.Arguments[0]).Value.ToString(), ((ConstantExpression)expression.Arguments[1]).Value.ToString());

    //  return true;
    //}

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

    /// <summary>
    /// Handles Contains/StartsWith/EndsWith
    /// Note: Doesn't yet handle characters i.e string.Contains('c') will fail, string.Contains("C") will succeed
    /// </summary>
    /// <param name="m">Current Expression</param>
    /// <param name="methodName">Method Name</param>
    /// <returns>Passes through the MethodCallExpression</returns>
    private MethodCallExpression VisitStringMethod(MethodCallExpression m, string methodName) {
      sb.Append("{");
      this.Visit(m.Object);
      sb.Append(":{\"").Append(methodName).Append("\":");
      this.Visit(m.Arguments[0]);
      sb.Append("}}");
      return m;
    }

    /// <summary>
    /// Handles Any/All
    /// {"orders":{"any":{"ShipCity":{"startswith":"F"}}}}
    /// </summary>
    /// <param name="m">Current Expression</param>
    /// <param name="methodName">Method Name</param>
    /// <returns>Passes through the MethodCallExpression</returns>
    private MethodCallExpression VisitAnyAll(MethodCallExpression m, string methodName) {
      sb.Append("{");
      this.Visit(m.Arguments[0]);
      sb.Append(":{\"").Append(methodName).Append("\":");
      var inner = StripQuotes(m.Arguments[1]);
      if (inner is LambdaExpression lambda) {
        this.VisitPredicate(lambda.Body);
      } else {
        this.Visit(inner);
      }
      sb.Append("}}");
      return m;
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
