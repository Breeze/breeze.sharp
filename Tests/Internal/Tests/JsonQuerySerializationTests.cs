using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Foo;
using Breeze.Sharp.Json;
using Breeze.Sharp.Core;
using System.Linq;

namespace Breeze.Sharp.Tests {

  /// <summary> Test whether EntityQuery is serialized to JSON correctly </summary>
  [TestClass]
  public class JsonQuerySerializationTests {
    private String _serviceName;

    [TestInitialize]
    public void TestInitializeMethod() {
      Configuration.Instance.QueryUriStyle = QueryUriStyle.JSON;
      Configuration.Instance.ProbeAssemblies(typeof(Customer).Assembly);
      _serviceName = TestFns.serviceName;
    }

    [TestCleanup]
    public void TearDown() {
      Configuration.Instance.QueryUriStyle = TestFns.queryUriStyle;
    }

    // TODO somehow compare JSON by structure instead of string, so whitespace changes won't matter
    private void Check(EntityQuery query, string expectedJson) {
      var json = JsonQueryExpressionVisitor.Translate(query.Expression, out string parameters);
      Assert.AreEqual(expectedJson, json);
    }

    [TestMethod]
    public void InlineCount() {
      var ord = EntityQuery.From<Order>();
      Check(ord, "{}");

      var q = ord.InlineCount();
      Check(q, "{\"inlineCount\":true}");
    }

    [TestMethod]
    public void SkipAndTake() {
      var ord = EntityQuery.From<Order>();
      Check(ord, "{}");

      var q = ord.Skip(2);
      Check(q, "{\"skip\":2}");

      q = ord.Take(5);
      Check(q, "{\"take\":5}");

      q = ord.Skip(2).Take(5);
      Check(q, "{\"skip\":2,\"take\":5}");
    }

    [TestMethod]
    public void OrderBy() {
      var ord = EntityQuery.From<Order>();

      var q = ord.OrderBy(o => o.ShipCountry);
      Check(q, "{\"orderBy\":[\"ShipCountry\"]}");

      q = ord.OrderByDescending(o => o.ShipCountry);
      Check(q, "{\"orderBy\":[\"ShipCountry desc\"]}");

      q = ord.OrderBy(o => o.ShipCountry).OrderBy(o => o.ShipCity);
      Check(q, "{\"orderBy\":[\"ShipCountry\",\"ShipCity\"]}");

      q = ord.OrderBy(o => o.ShipCountry).ThenBy(o => o.ShipCity);
      Check(q, "{\"orderBy\":[\"ShipCountry\",\"ShipCity\"]}");

      q = ord.OrderByDescending(o => o.ShipCountry).ThenBy(o => o.ShipCity);
      Check(q, "{\"orderBy\":[\"ShipCountry desc\",\"ShipCity\"]}");

      q = ord.OrderBy(o => o.ShipCountry).ThenByDescending(o => o.ShipCity);
      Check(q, "{\"orderBy\":[\"ShipCountry\",\"ShipCity desc\"]}");

      q = ord.OrderByDescending(o => o.ShipCountry).ThenByDescending(o => o.ShipCity);
      Check(q, "{\"orderBy\":[\"ShipCountry desc\",\"ShipCity desc\"]}");

      q = ord.OrderBy(o => o.Customer.CompanyName);
      Check(q, "{\"orderBy\":[\"Customer.CompanyName\"]}");

      q = ord.OrderBy(o => o.Employee.Manager.Manager.FirstName);
      Check(q, "{\"orderBy\":[\"Employee.Manager.Manager.FirstName\"]}");

      q = ord.OrderByDescending(o => o.Employee.Manager.Manager.FirstName);
      Check(q, "{\"orderBy\":[\"Employee.Manager.Manager.FirstName desc\"]}");

      q = ord.OrderBy(o => o.Employee.HireDate);
      Check(q, "{\"orderBy\":[\"Employee.HireDate\"]}");

      q = ord.OrderByDescending(o => o.Employee.HireDate);
      Check(q, "{\"orderBy\":[\"Employee.HireDate desc\"]}");


      //q = ord.OrderBy(o => o.Customer.CompanyName.Substring(1));
      //Check(q, "{\"orderBy\":[\"Not yet supported\"]}");
    }

    /// <summary>
    /// Ordering by the value itself rather than by a named member.
    /// </summary>
    /// <remarks>
    /// Over a projection to a single member this is ordering the source by that member, so it can
    /// be translated. Ordering entities by themselves, or a projection to several members, names
    /// nothing the server can sort on, and the clause is dropped.
    /// </remarks>
    [TestMethod]
    public void OrderBy2() {
      var ord = EntityQuery.From<Order>();

      var q = ord.OrderBy(o => o);
      Check(q, "{}");

      var q2 = ord.Select(o => o.Employee.HireDate);
      q2 = q2.OrderBy(y => y);
      Check(q2, "{\"select\":[\"Employee.HireDate\"],\"orderBy\":[\"Employee.HireDate\"]}");

      var q3 = ord.Select(o => o.Employee.HireDate);
      q3 = q3.OrderByDescending(y => y);
      Check(q3, "{\"select\":[\"Employee.HireDate\"],\"orderBy\":[\"Employee.HireDate desc\"]}");

      // The projected value still orders the source, so a Where alongside it is unaffected.
      var q4 = ord.Where(o => o.Freight > 100).Select(o => o.OrderDate);
      q4 = q4.OrderByDescending(y => y);
      Check(q4, "{\"where\":{\"Freight\":{\"gt\": 100}},\"select\":[\"OrderDate\"],\"orderBy\":[\"OrderDate desc\"]}");

      // Several members leave nothing to name, so the ordering goes rather than being guessed at.
      var q5 = ord.Select(o => new { o.Freight, o.ShipCity });
      q5 = q5.OrderBy(y => y.Freight).ThenBy(y => y.ShipCity);
      Check(q5, "{\"select\":[\"Freight\",\"ShipCity\"],\"orderBy\":[\"Freight\",\"ShipCity\"]}");
    }

    [TestMethod]
    public void Expand() {
      var ord = EntityQuery.From<Order>();
      Check(ord, "{}");

      var q = ord.Expand(o => o.Customer);
      Check(q, "{\"expand\":[\"Customer\"]}");

      q = ord.Expand(o => o.OrderDetails);
      Check(q, "{\"expand\":[\"OrderDetails\"]}");

      q = ord.Expand("OrderDetails");
      Check(q, "{\"expand\":[\"OrderDetails\"]}");

      q = ord.Expand("OrderDetails.Product");
      Check(q, "{\"expand\":[\"OrderDetails/Product\"]}");

      q = ord.Expand(o => o.OrderDetails).Expand(o => o.Customer);
      Check(q, "{\"expand\":[\"Customer\",\"OrderDetails\"]}");

      q = ord.Expand("OrderDetails").Expand("Customer");
      Check(q, "{\"expand\":[\"Customer\",\"OrderDetails\"]}");
    }

    [TestMethod]
    public void Select() {
      var ord = EntityQuery.From<Order>();

      var q = ord.Select(o => o.ShipCity);
      Check(q, "{\"select\":[\"ShipCity\"]}");

      var q2 = ord.Select(o => new { o.ShipCity, o.ShipCountry });
      Check(q2, "{\"select\":[\"ShipCity\",\"ShipCountry\"]}");

      var q3 = ord.Select(o => o.Customer.City);
      Check(q3, "{\"select\":[\"Customer.City\"]}");
    }


    [TestMethod]
    public void WhereConstant() {
      var ord = EntityQuery.From<Order>();
      Check(ord, "{}");

      var q = ord.Where(o => o.ShipCountry == "England");
      Check(q, "{\"where\":{\"ShipCountry\":\"England\"}}");

      var p1 = PredicateBuilder.Create<Order>(o => o.Freight > 100);
      var pred = p1.Not();
      q = ord.Where(pred);
      Check(q, "{\"where\":{\"not\":{\"Freight\":{\"gt\": 100}}}}");
    }

    [TestMethod]
    public void WhereVariable() {
      var ord = EntityQuery.From<Order>();

      var country = "England";
      var q = ord.Where(o => o.ShipCountry == country);
      Check(q, "{\"where\":{\"ShipCountry\":\"England\"}}");

      var date = new DateTime(2019, 5, 30, 10, 11, 12);
      q = ord.Where(o => o.OrderDate < date);
      Check(q, "{\"where\":{\"OrderDate\":{\"lt\": \"5/30/2019 10:11:12 AM\"}}}");

      var anon = new { Country = "England" };
      q = ord.Where(o => o.ShipCountry == anon.Country);
      Check(q, "{\"where\":{\"ShipCountry\":\"England\"}}");
    }

    [TestMethod]
    public void WhereDateConstructor() {
      var ord = EntityQuery.From<Order>();
      var q = ord.Where(o => o.OrderDate < new DateTime(2019, 5, 30, 10, 11, 12));
      Check(q, "{\"where\":{\"OrderDate\":{\"lt\": \"5/30/2019 10:11:12 AM\"}}}");
    }

    [TestMethod]
    public void WhereDictionary() {
      var ord = EntityQuery.From<Order>();
      var d = new Dictionary<string, string> { { "country", "England" } };
      var q = ord.Where(o => o.ShipCountry == d["country"]);
      Check(q, "{\"where\":{\"ShipCountry\":\"England\"}}");
    }

    [TestMethod]
    public void WhereConstantGuid() {
      var q = EntityQuery.From<Customer>();
      var guid = new Guid("81E6E4C0-E608-4191-A717-3372B2FAC343");
      q = q.Where(c => c.CustomerID == guid);

      // parser lowercases the guid so
      string lowerCaseGuid = guid.ToString().ToLower();
      Check(q, $"{{\"where\":{{\"CustomerID\":\"{lowerCaseGuid}\"}}}}");
    }

    [TestMethod]
    public void WhereNestedProperty() {
      var q = EntityQuery.From<Order>();
      q = q.Where(o => o.Customer.Country == "England");
      Check(q, "{\"where\":{\"Customer.Country\":\"England\"}}");
    }

    [TestMethod]
    public void WhereNestedPropertyAsString() {
      var q = EntityQuery.From<Order>();
      var country = "England";
      q = q.Where(o => "Customer.Country" == country);
      Check(q, "{\"where\":{\"Customer.Country\":\"England\"}}");
    }

    [TestMethod]
    public void WhereStringContains() {
      var q = EntityQuery.From<Customer>();
      q = q.Where(o => o.City.Contains("C"));
      Check(q, "{\"where\":{\"City\":{\"Contains\":\"C\"}}}");
    }

    [TestMethod]
    public void WhereStringStartsWith() {
      var q = EntityQuery.From<Customer>();
      q = q.Where(o => o.City.StartsWith("C"));
      Check(q, "{\"where\":{\"City\":{\"StartsWith\":\"C\"}}}");
    }

    [TestMethod]
    public void WhereStringEndsWith() {
      var q = EntityQuery.From<Customer>();
      q = q.Where(o => o.City.EndsWith("C"));
      Check(q, "{\"where\":{\"City\":{\"EndsWith\":\"C\"}}}");
    }

    [TestMethod]
    public void WhereAny() {
      var q = EntityQuery.From<Customer>();
      q = q.Where(c => c.Orders.Any(o => o.Freight > 100));
      Check(q, "{\"where\":{\"Orders\":{\"Any\":{\"Freight\":{\"gt\": 100}}}}}");
    }

    [TestMethod]
    public void WhereAll() {
      var q = EntityQuery.From<Order>();
      q = q.Where(o => o.OrderDetails.All(d => d.Discount == 0));
      Check(q, "{\"where\":{\"OrderDetails\":{\"All\":{\"Discount\":0}}}}");
    }

    /// <summary>
    /// A boolean property used on its own is a complete predicate. It has to be written as
    /// {"Discontinued":true}; a bare "Discontinued" is a JSON string, which the server rejects.
    /// </summary>
    [TestMethod]
    public void WhereBoolean() {
      var prod = EntityQuery.From<Product>();

      var q = prod.Where(p => p.Discontinued);
      Check(q, "{\"where\":{\"Discontinued\":true}}");

      // The explicit comparison must keep producing the same thing.
      q = prod.Where(p => p.Discontinued == true);
      Check(q, "{\"where\":{\"Discontinued\":true}}");

      q = prod.Where(p => !p.Discontinued);
      Check(q, "{\"where\":{\"not\":{\"Discontinued\":true}}}");

      q = prod.Where(p => p.Discontinued == false);
      Check(q, "{\"where\":{\"Discontinued\":false}}");
    }

    [TestMethod]
    public void WhereBooleanCombined() {
      var prod = EntityQuery.From<Product>();

      var q = prod.Where(p => p.Discontinued && p.ProductName == "Chai");
      Check(q, "{\"where\":{\"and\":[{\"Discontinued\":true},{\"ProductName\":\"Chai\"}]}}");

      q = prod.Where(p => p.ProductName == "Chai" || p.Discontinued);
      Check(q, "{\"where\":{\"or\":[{\"ProductName\":\"Chai\"},{\"Discontinued\":true}]}}");

      q = prod.Where(p => p.Discontinued && p.Category.CategoryName == "Beverages");
      Check(q, "{\"where\":{\"and\":[{\"Discontinued\":true},{\"Category.CategoryName\":\"Beverages\"}]}}");
    }

    [TestMethod]
    public void WhereBooleanInAnyAll() {
      var cat = EntityQuery.From<Category>();

      var q = cat.Where(c => c.Products.Any(p => p.Discontinued));
      Check(q, "{\"where\":{\"Products\":{\"Any\":{\"Discontinued\":true}}}}");

      q = cat.Where(c => c.Products.All(p => p.Discontinued));
      Check(q, "{\"where\":{\"Products\":{\"All\":{\"Discontinued\":true}}}}");
    }

    [TestMethod]
    public void WithParameters() {
      var q = EntityQuery.From<Employee>("SearchEmployees")
        .WithParameter("employeeIds", new int[] { 1, 4 });

      //Check(q, "{\"from\":\"SearchEmployees\",\"parameters\":{\"employeeIds\":[1,4]}}");
      Check(q, "{\"parameters\":{\"employeeIds\":[1,4]}}");
    }

    [TestMethod]
    public void WherePropertyPathLevel1() {
      var q = EntityQuery.From<Supplier>();
      q = q.Where(o => o.Location.City == "New York");
      Check(q, "{\"where\":{\"Location.City\":\"New York\"}}");
    }

    [TestMethod]
    public void WherePropertyPathLevel2() {
      var q = EntityQuery.From<Product>();
      q = q.Where(o => o.Supplier.Location.City == "New York");
      Check(q, "{\"where\":{\"Supplier.Location.City\":\"New York\"}}");
    }

    [TestMethod]
    public void WherePropertyPathLevel2Customer() {
      var q = EntityQuery.From<OrderDetail>();
      q = q.Where(o => o.Order.Customer.City == "New York");
      Check(q, "{\"where\":{\"Order.Customer.City\":\"New York\"}}");
    }

    [TestMethod]
    public void WherePropertyPathLevel3() {
      var q = EntityQuery.From<OrderDetail>();
      q = q.Where(o => o.Product.Supplier.Location.City == "New York");
      Check(q, "{\"where\":{\"Product.Supplier.Location.City\":\"New York\"}}");
    }

    [TestMethod]
    public void WhereChainedQueries() {
      var q = EntityQuery.From<Customer>();
      q = q.Where(o => o.City == ("New York"));
      q = q.Where(o => o.Country == "USA");
      Check(q, "{\"where\":{\"and\":[{\"City\":\"New York\"},{\"Country\":\"USA\"}]}}");
    }

  }
}
