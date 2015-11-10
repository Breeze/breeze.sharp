using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Threading.Tasks;
using System.Linq;
using Breeze.Sharp.Core;
using Breeze.Sharp;
using System.Collections.Generic;
using Foo;
using System.Threading;
using System.Windows.Threading;

namespace Breeze.Sharp.Tests {

  [TestClass]
  public class PredicateQueryTests {

    private String _serviceName;

    [TestInitialize]
    public void TestInitializeMethod() {
      Configuration.Instance.ProbeAssemblies(typeof(Customer).Assembly);
      _serviceName = TestFns.serviceName;
    }

    [TestCleanup]
    public void TearDown() {
      
    }

    

    [TestMethod]
    public async Task SimplePred() {
      var entityManager = await TestFns.NewEm(_serviceName);

      // Orders with freight cost over 300.
      var pred = PredicateBuilder.Create<Order>(o => o.Freight > 300);
      var query = new EntityQuery<Order>().Where(pred);
      var orders300 = await entityManager.ExecuteQuery(query);
      Assert.IsTrue(orders300.Any(), "There should be orders with freight cost > 300");
      
    }

    [TestMethod]
    public async Task CompositePred() {
      var entityManager = await TestFns.NewEm(_serviceName);

      // Start with a base query for all Orders
      var baseQuery = new EntityQuery<Order>();

      // A Predicate is a condition that is true or false
      // Combine two predicates with '.And' to
      // query for orders with freight cost over $100
      // that were ordered after April 1, 1998
      var p1 = PredicateBuilder.Create<Order>(o => o.Freight > 100);
      var p2 = PredicateBuilder.Create<Order>(o => o.OrderDate > new DateTime(1998, 3, 1));
      var pred = p1.And(p2);
      var query = baseQuery.Where(pred);
      var orders = await entityManager.ExecuteQuery(query);
      Assert.IsTrue(orders.Any(), "There should be orders");
      Assert.IsTrue(orders.All(o => o.Freight > 100 && o.OrderDate > new DateTime(1998,3,1)), 
        "There should be the right orders");

      // Yet another way to ask the same question
      pred = PredicateBuilder.Create<Order>(o => o.Freight > 100)
          .And(PredicateBuilder.Create<Order>(o => o.OrderDate > new DateTime(1998, 3, 1)));
      var query2 = baseQuery.Where(pred);
      var orders2 = await entityManager.ExecuteQuery(query2);
      Assert.IsTrue(orders2.Count() == orders.Count());

      // Yet another way to ask the same question
      pred = PredicateBuilder.Create<Order>(o => o.Freight > 100)
          .Or(PredicateBuilder.Create<Order>(o => o.OrderDate > new DateTime(1998, 3, 1)));
      var query3 = baseQuery.Where(pred);

    }

    [TestMethod]
    public async Task CompositeNotPred() {
      var entityManager = await TestFns.NewEm(_serviceName);

      // Start with a base query for all Orders
      var baseQuery = new EntityQuery<Order>();

      var p1 = PredicateBuilder.Create<Order>(o => o.Freight > 100);

      var pred = p1.Not();
      var query = baseQuery.Where(pred);
      var orders = await entityManager.ExecuteQuery(query);
      Assert.IsTrue(orders.Any(), "There should be orders");
      pred = pred.Not();
      var query2 = baseQuery.Where(pred);
    }


    [TestMethod]
    public async Task PredAny() {
      var em1 = await TestFns.NewEm(_serviceName);
      
      var pred = PredicateBuilder.Create<Customer>(emp => emp.Orders.Any(order => order.ShipName != null));
      var q = new EntityQuery<Customer>().Where(pred);
      var r = await q.Execute(em1);
      Assert.IsTrue(r.Any());

      var q2 = new EntityQuery<Employee>()
          .Where(emp => emp.Orders.Any(order => order.Customer.CompanyName.StartsWith("Lazy")))
          .Expand("Orders.Customer");
      var r2 = await q2.Execute(em1);
      Assert.IsTrue(r2.Any());
    }

    

    // Need more work on PredicateBuilder for this to work.
    //[TestMethod]
    //public async Task PredAny() {
    //  var em1 = await TestFns.NewEm(_serviceName);
    //  var pred = PredicateBuilder.Create<Order>(order => order.Freight > 10);
    //  var pred2 = PredicateBuilder.Create<Employee>(emp => emp.Orders.Any(pred.Compile()));
    //  var q = new EntityQuery<Employee>()
    //    .Where(pred2);
    //  var results = await q.Execute(em1);
    //  Assert.IsTrue(results.Any());

    //}
  }
}

  
