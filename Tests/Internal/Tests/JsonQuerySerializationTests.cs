using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Foo;
using Breeze.Sharp.Json;

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
    }

    // TODO somehow compare JSON by structure instead of string, so whitespace changes won't matter
    private void Check(EntityQuery query, string expectedJson) {
      var json = JsonQueryExpressionVisitor.Translate(query.Expression);
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
      Check(q, "{\"orderBy\":\"ShipCountry\"}");

      q = ord.OrderByDescending(o => o.ShipCountry);
      Check(q, "{\"orderBy\":\"ShipCountry DESC\"}");

      q = ord.OrderBy(o => o.ShipCountry).OrderBy(o => o.ShipCity);
      Check(q, "{\"orderBy\":\"ShipCountry, ShipCity\"}");

      q = ord.OrderBy(o => o.ShipCountry).ThenBy(o => o.ShipCity);
      Check(q, "{\"orderBy\":\"ShipCountry, ShipCity\"}");

      q = ord.OrderByDescending(o => o.ShipCountry).ThenBy(o => o.ShipCity);
      Check(q, "{\"orderBy\":\"ShipCountry DESC, ShipCity\"}");

      q = ord.OrderBy(o => o.ShipCountry).ThenByDescending(o => o.ShipCity);
      Check(q, "{\"orderBy\":\"ShipCountry, ShipCity DESC\"}");

      q = ord.OrderByDescending(o => o.ShipCountry).ThenByDescending(o => o.ShipCity);
      Check(q, "{\"orderBy\":\"ShipCountry DESC, ShipCity DESC\"}");
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

      q = ord.Where(o => o.Freight > 100M);
      Check(q, "{\"where\":{\"Freight\":{\"gt\": 100}}}");
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
  }
}
