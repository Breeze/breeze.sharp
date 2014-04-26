using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Threading.Tasks;
using System.Linq;
using Breeze.Sharp.Core;
using Breeze.Sharp;
using System.Collections.Generic;

using Model.Inheritance.Produce;
using System.Collections;

namespace Breeze.Sharp.Tests {

  [TestClass]
  public class InheritanceProduceTests {

    private String _serviceName;

    //    test("Skipping inherit produce tests - DB not yet avail", function () {
    //        ok(true, "Skipped tests - Mongo");
    //    });

    [TestInitialize]
    public void TestInitializeMethod() {
      MetadataStore.Instance.ProbeAssemblies(typeof(Apple).Assembly);
      MetadataStore.Instance.NamingConvention.AddClientServerNamespaceMapping("Model.Inheritance.Produce", "ProduceTPH");
      _serviceName = "http://localhost:7150/breeze/ProduceTPH/";
    }

    [TestCleanup]
    public void TearDown() {
      
    }

    [TestMethod]
    public async Task FetchByKey() {
      var em1 = await TestFns.NewEm(_serviceName);
      var q = new EntityQuery<Fruit>().From("Fruits");
      var r0 = await em1.ExecuteQuery(q);
      Assert.IsTrue(r0.Count() > 0);
      var fruit1 = r0.First();
      var id = fruit1.Id;
      var ek = new EntityKey(typeof(Fruit), id);
      var r1 = await em1.ExecuteQuery(ek.ToQuery());
      var fruits = r1.Cast<Fruit>();
      Assert.IsTrue(fruits.First() == fruit1);
      
    }

    [TestMethod]
    public async Task FetchByKeyWithDefaultResource() {
      var em1 = await TestFns.NewEm(_serviceName);
      var rn = MetadataStore.Instance.GetDefaultResourceName(typeof(Fruit));
      Assert.IsTrue(rn != "Fruits");
      MetadataStore.Instance.SetResourceName("Fruits", typeof(Fruit), true);
      var rn2 = MetadataStore.Instance.GetDefaultResourceName(typeof(Fruit));
      Assert.IsTrue(rn2 == "Fruits");
      var q = new EntityQuery<Fruit>();
      var r0 = await em1.ExecuteQuery(q);
      Assert.IsTrue(r0.Count() > 0);
      var fruit1 = r0.First();
      var id = fruit1.Id;
      var ek = new EntityKey(typeof(Fruit), id);
      var r1 = await em1.ExecuteQuery(ek.ToQuery());
      var fruits = r1.Cast<Fruit>();
      Assert.IsTrue(fruits.First() == fruit1);

    }

    [TestMethod]
    public async Task QueryAbstractWithOr() {
      var em1 = await TestFns.NewEm(_serviceName);
      var q = new EntityQuery<Fruit>().From("Fruits").Where(f => f.Name == "Apple" || f.Name == "Foo" || f.Name == "Papa");

      var r0 = await em1.ExecuteQuery(q);
      Assert.IsTrue(r0.Count() > 0);
      var fruit1 = r0.First();
      Assert.IsTrue(fruit1 is Apple, "fruit should be an Apple");

    }

    [TestMethod]
    public async Task FetchByAbstractEntityKey() {
      var em1 = await TestFns.NewEm(_serviceName);
      var q = new EntityQuery<ItemOfProduce>().From("ItemsOfProduce");
      var r0 = await em1.ExecuteQuery(q);
      Assert.IsTrue(r0.Count() > 0);
      var iop1 = r0.First();
      var id = iop1.Id;
      var ek = new EntityKey(typeof(ItemOfProduce), id);
      var r1 = await em1.ExecuteQuery(ek.ToQuery());
      var fruits = r1.Cast<ItemOfProduce>();
      Assert.IsTrue(fruits.First() == iop1);

    }

    [TestMethod]
    public async Task LocalQueryFruits() {
      var em1 = await TestFns.NewEm(_serviceName);
      var q = new EntityQuery<Fruit>().From("Fruits");
      var r0 = await em1.ExecuteQuery(q);
      Assert.IsTrue(r0.Count() > 0);

      var r1 = em1.ExecuteQueryLocally(q);
      Assert.IsTrue(r0.Count() == r1.Count());
      Assert.IsTrue(r0.All(r => r1.Contains(r)));

    }

    [TestMethod]
    public async Task FetchEntityByKeyItemOfProduce() {
      var em1 = await TestFns.NewEm(_serviceName);
      var appleId = new Guid("13f1c9f5-3189-45fa-ba6e-13314fafaa92");
      
      // var appleId = new Guid("D35E9669-2BAE-4D69-A27A-252B31800B74");

      var ek = new EntityKey(typeof(ItemOfProduce), appleId);
      var fr = await em1.FetchEntityByKey(ek);
      Assert.IsTrue(fr.Entity != null && fr.Entity is Apple && !fr.FromCache);
      // and again
      var r0 = await em1.ExecuteQuery(ek.ToQuery<ItemOfProduce>());
      Assert.IsTrue(r0.Count() > 0);
      Assert.IsTrue(r0.First().Id == appleId);
    }

    [TestMethod]
    public async Task FetchEntityByKeyLocalCache() {
      var em1 = await TestFns.NewEm(_serviceName);
      var appleId = new Guid("13f1c9f5-3189-45fa-ba6e-13314fafaa92");
      // var appleId = new Guid("D35E9669-2BAE-4D69-A27A-252B31800B74");

      var ek = new EntityKey(typeof(ItemOfProduce), appleId);
      var q = new EntityQuery<ItemOfProduce>();
      var r0 = await em1.ExecuteQuery(q);
      Assert.IsTrue(r0.Count() > 30);
      var fr = await em1.FetchEntityByKey(ek, true);
      // should now be fromCache
      Assert.IsTrue(fr.Entity != null && fr.Entity is Apple && fr.FromCache);

    }

    [TestMethod]
    public async Task QueryAndModify() {
      var em1 = await TestFns.NewEm(_serviceName);

      var q = new EntityQuery<ItemOfProduce>().Take(2);
      var r0 = await em1.ExecuteQuery(q);
      Assert.IsTrue(r0.Count() == 2);
      Assert.IsTrue(r0.First().QuantityPerUnit != null);
      Assert.IsTrue(r0.ElementAt(1).QuantityPerUnit != null);
      r0.First().QuantityPerUnit = "ZZZ";
      Assert.IsTrue(r0.First().QuantityPerUnit == "ZZZ");


    }

    [TestMethod]
    public async Task QueryCheckUnique() {
      var em1 = await TestFns.NewEm(_serviceName);

      var q = new EntityQuery<ItemOfProduce>();
      var r0 = await em1.ExecuteQuery(q);
      var hs = r0.Select(r => r.QuantityPerUnit).ToHashSet();
      Assert.IsTrue(hs.Count() > 2, "should be more than 2 unique values");

    }

    [TestMethod]
    public async Task InitializeTest() {
      var em1 = await TestFns.NewEm(_serviceName);

      var q = new EntityQuery<Fruit>().From("Fruits");
      var r0 = await em1.ExecuteQuery(q);
      var apple = r0.First(r => r is Apple);
      Assert.IsTrue(apple.IsFruit);
      Assert.IsTrue(apple.InitializedTypes.Count == 3);
      Assert.IsTrue(apple.InitializedTypes[0] == "ItemOfProduce");
      Assert.IsTrue(apple.InitializedTypes[1] == "Fruit");
      Assert.IsTrue(apple.InitializedTypes[2] == "Apple");
    }

    //test("query Fruits w/client ofType", function () {
    //    var em = newEmX();
    //    ok(false, "Expected failure - OfType operator not yet supported - will be added later");
    //    return;

    //    var q = EntityQuery.from("ItemsOfProduce")
    //        .where(null, FilterQueryOp.IsTypeOf, "Fruit")
    //        .using(em);
    //    stop();
    //    var fruitType = em.metadataStore.getEntityType("Fruit");
    //    q.execute().then(function (data) {
    //        var r = data.results;
    //        ok(r.length > 0, "should have found some 'Fruits'");
    //        ok(r.every(function (f) {
    //            return f.entityType.isSubtypeOf(fruitType);
    //        }));

    //    }).fail(testFns.handleFail).fin(start);

    //});



  }
}

  
