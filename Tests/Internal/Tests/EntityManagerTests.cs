using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Breeze.Sharp;
using Breeze.Sharp.Core;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Foo;
using System.Collections.Specialized;
using System.ComponentModel;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json;

namespace Breeze.Sharp.Tests {

  [TestClass]
  public class EntityManagerTests {

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
    public async Task EntityKeyNoMetadata() {

      try {
        var ek = new EntityKey(typeof(FooEntity), MetadataStore.Detached, 7);
        Assert.Fail("should not get here");
      } catch (Exception e) {
        Assert.IsTrue(e.Message.Contains("FooEntity") && e.Message.Contains("FetchMetadata"));
      }

    }

    public class FooEntity : BaseEntity {

    }


    [TestMethod]
    public async Task BadURL() {

      var serviceName = "http://localhost:7150/breeze/xxxFoo";
      var ds = new DataService(serviceName);
      try {
        var ms = new MetadataStore();
        await ms.FetchMetadata(ds);
      } catch (Exception e) {
        Assert.IsTrue(e.Message.Contains("metadata resource"));
      }

      try {
        var em = new EntityManager(ds);
        await em.FetchMetadata();
      } catch (Exception e) {
        Assert.IsTrue(e.Message.Contains("metadata resource"));
      }
    }


    [TestMethod]
    public async Task GetChanges() {
      var em1 = await TestFns.NewEm(_serviceName);

      var customer = new Customer();
      var q = new EntityQuery<Customer>().Take(2);
      var custs = await q.Execute(em1);
      custs.First().City = "XXX";
      var q2 = new EntityQuery<Employee>().Take(3);
      var emps = await q2.Execute(em1);
      emps.Take(2).ForEach(emp => emp.LastName = "XXX");
      var newCust1 = em1.CreateEntity<Customer>();
      var newEmp1 = em1.CreateEntity<Employee>();
      var changedEntities = em1.GetChanges();
      Assert.IsTrue(changedEntities.Count() == 5, "should be 5 changes");
      var changedCusts = em1.GetChanges(typeof(Customer));
      Assert.IsTrue(changedCusts.Count() == 2, "should be 2 changed custs");
      var changedEmps = em1.GetChanges(typeof(Employee));
      Assert.IsTrue(changedEmps.Count() == 3, "should be 3 changed emps");
      var changedEntities2 = em1.GetChanges(typeof(Employee), typeof(Customer));
      Assert.IsTrue(changedEntities2.Count() == 5, "should be 5 changes");


    }

    [TestMethod]
    public async Task HasChangesChangedAfterSave() {
      var em1 = await TestFns.NewEm(_serviceName);

      var hccArgs = new List<EntityManagerHasChangesChangedEventArgs>();
      em1.HasChangesChanged += (s, e) => {
        hccArgs.Add(e);
      };

      var emp = new Employee() { FirstName = "Test_Fn", LastName = "Test_Ln" };

      em1.AddEntity(emp);
      Assert.IsTrue(hccArgs.Count == 1);
      Assert.IsTrue(hccArgs.Last().HasChanges == true);
      Assert.IsTrue(em1.HasChanges());
      var sr = await em1.SaveChanges();
      Assert.IsTrue(sr.Entities.Count == 1);
      Assert.IsTrue(hccArgs.Count == 2);
      Assert.IsTrue(hccArgs.Last().HasChanges == false);
      Assert.IsTrue(em1.HasChanges() == false);
    }

    [TestMethod]
    public async Task LoadNavigationPropertyNonscalar() {
      var em1 = await TestFns.NewEm(_serviceName);
      TestFns.RunInWpfSyncContext( async () =>  {
        var q0 = new EntityQuery<Customer>().Where(c => c.Orders.Any()).Take(3);
        var r0 = await q0.Execute(em1);
        var arr = r0.ToArray();
        arr[0].City = "Modified City";
        // Task.WaitAll(r0.Select(c => c.EntityAspect.LoadNavigationProperty("Orders")).ToArray());
        await Task.WhenAll(r0.Select(c => c.EntityAspect.LoadNavigationProperty("Orders")));
        Assert.IsTrue(r0.All(c => c.Orders.Count() > 0));
      });
    }

    [TestMethod]
    public async Task LoadNavigationPropertyScalar() {
      var em1 = await TestFns.NewEm(_serviceName);
      TestFns.RunInWpfSyncContext(async () => {
        var q0 = new EntityQuery<Order>().Where(o => o.Customer != null).Take(3);
        var r0 = await q0.Execute(em1);
        var arr = r0.ToArray();
        arr[1].ShipCity = "Modified City";
        // Task.WaitAll(r0.Select(o => o.EntityAspect.LoadNavigationProperty("Customer")).ToArray());
        await Task.WhenAll(r0.Select(o => o.EntityAspect.LoadNavigationProperty("Customer")));
        Assert.IsTrue(r0.All(o => o.Customer != null));
      });
    }
    

  }
}
