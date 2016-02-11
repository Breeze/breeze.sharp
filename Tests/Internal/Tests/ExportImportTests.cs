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
using System.IO;
using System.Reflection;

namespace Breeze.Sharp.Tests {

  [TestClass]
  public class ExportImportTests {

    // TODO: need Exp/Imp tests with Complex type changes.

    private String _serviceName;

    [TestInitialize]
    public void TestInitializeMethod()
    {
      Configuration.Instance.ProbeAssemblies(typeof(Customer).Assembly);
      _serviceName = TestFns.serviceName;
    }

    [TestCleanup]
    public void TearDown() {

    }


    // [TestMethod]
    // This test can only be run standalone because of the __Reset call
    public async Task ExpMetadata() {
      var em1 = await TestFns.NewEm(_serviceName);

      var metadata = em1.MetadataStore.ExportMetadata();
      File.WriteAllText("c:/temp/metadata.txt", metadata);

      var ms = Configuration.Instance;

      
      Assert.IsTrue(ms != Configuration.Instance);
      var ms2 = new MetadataStore();
      ms2.ImportMetadata(metadata);
      var metadata2 = ms2.ExportMetadata();

      File.WriteAllText("c:/temp/metadata2.txt", metadata2);
      Assert.IsTrue(metadata == metadata2, "metadata should match between export and import");
    }

    [TestMethod]
    public async Task ExpEntities() {
      var em1 = await TestFns.NewEm(_serviceName);

      var q = new EntityQuery<Foo.Customer>("Customers").Take(5);

      var results = await q.Execute(em1);

      Assert.IsTrue(results.Count() > 0);
      var exportedEntities = em1.ExportEntities();

      File.WriteAllText("c:/temp/emExport.txt", exportedEntities);

    }

	[TestMethod]
	public async Task ExpImpEntitiesWithEnum()
	{
      var em1 = await TestFns.NewEm(_serviceName);

      var q = new EntityQuery<Foo.Role>("Roles").Take(5);

      var results = await q.Execute(em1);

      Assert.IsTrue(results.Any());
      var exportedEntities = em1.ExportEntities();

      File.WriteAllText("c:/temp/emExportWithEnum.txt", exportedEntities);

      var em2 = new EntityManager(em1);

      em2.ImportEntities(exportedEntities);
      var roleTypes = em2.GetEntities<Role>().Select(x => x.RoleType).ToList();

	  Assert.IsTrue(roleTypes.Count == 5, "should have imported 5 entities");
	}

    [TestMethod]
    public async Task ExpEntitiesWithChanges() {
      var em1 = await TestFns.NewEm(_serviceName);

      var q = new EntityQuery<Foo.Customer>("Customers").Take(5);

      var results = await q.Execute(em1);

      Assert.IsTrue(results.Count() > 0);
      var custs = results.Take(2);
      custs.ForEach(c => c.City = "Paris");
      var emp1 = em1.CreateEntity<Employee>();

      var exportedEntities = em1.ExportEntities();

      File.WriteAllText("c:/temp/emExportWithChanges.txt", exportedEntities);

    }

    [TestMethod]
    public async Task ExpSelectedEntitiesWithChanges() {
      var em1 = await TestFns.NewEm(_serviceName);

      var q = new EntityQuery<Foo.Customer>("Customers").Take(5);

      var results = await q.Execute(em1);

      Assert.IsTrue(results.Count() > 0);
      var custs = results.Take(2).ToList();
      custs.ForEach(c => c.City = "Paris");
      var emp1 = em1.CreateEntity<Employee>();

      var exportedEntities = em1.ExportEntities(new IEntity[] { custs[0], custs[1], emp1 }, false);

      File.WriteAllText("c:/temp/emExportWithChanges.txt", exportedEntities);

    }

    [TestMethod]
    public async Task ExpImpSelectedEntitiesWithChanges() {
      var em1 = await TestFns.NewEm(_serviceName);

      var q = new EntityQuery<Foo.Customer>("Customers").Take(5);

      var results = await q.Execute(em1);

      Assert.IsTrue(results.Count() > 0);
      var custs = results.Take(2).ToList();
      custs.ForEach(c => c.City = "Paris");
      var emp1 = em1.CreateEntity<Employee>();
      var emp2 = em1.CreateEntity<Employee>();

      var exportedEntities = em1.ExportEntities(new IEntity[] { custs[0], custs[1], emp1, emp2 }, false);

      var em2 = new EntityManager(em1);
      var impResult = em2.ImportEntities(exportedEntities);
      var allEntities = em2.GetEntities();

      Assert.IsTrue(impResult.ImportedEntities.Count == 4, "should have imported 4 entities");
      Assert.IsTrue(impResult.TempKeyMap.Count == 2);
      Assert.IsTrue(impResult.TempKeyMap.All(kvp => kvp.Key == kvp.Value), "imported entities should have same key values");
      Assert.IsTrue(allEntities.Count() == 4, "should have 4 entities in cache");
      Assert.IsTrue(em2.GetEntities<Customer>().All(c => c.EntityAspect.EntityState.IsModified()));
      Assert.IsTrue(em2.GetEntities<Employee>().All(c => c.EntityAspect.EntityState.IsAdded()));

    }

    [TestMethod]
    public async Task ExpImpTempKeyCollision() {
      var em1 = await TestFns.NewEm(_serviceName);

      var q = new EntityQuery<Foo.Customer>("Customers").Take(5);

      var results = await q.Execute(em1);

      Assert.IsTrue(results.Count() > 0);
      var custs = results.Take(2).ToList();
      custs.ForEach(c => c.City = "Paris");
      var emp1 = em1.CreateEntity<Employee>();
      var emp2 = em1.CreateEntity<Employee>();

      var exportedEntities = em1.ExportEntities(new IEntity[] { custs[0], custs[1], emp1, emp2 }, false);

      custs.ForEach(c => c.City = "London");
      // custs1 and 2 shouldn't be imported because of default preserveChanges
      // emps1 and 2 should cause the creation of NEW emps with new temp ids;
      // tempKeys should cause creation of new entities;
      var impResult = em1.ImportEntities(exportedEntities);
      var allEntities = em1.GetEntities();

      Assert.IsTrue(allEntities.Count() == 9, "should have 9 entities in the cache");
      Assert.IsTrue(allEntities.OfType<Customer>().Count() == 5, "should only be the original 5 custs");
      Assert.IsTrue(allEntities.OfType<Employee>().Count() == 4, "should be 4 emps (2 + 2) ");
      Assert.IsTrue(allEntities.OfType<Customer>().Count(c => c.EntityAspect.EntityState.IsModified()) == 2, "should only be 2 modified customers");
      Assert.IsTrue(allEntities.OfType<Employee>().All(c => c.EntityAspect.EntityState.IsAdded()));
      Assert.IsTrue(impResult.ImportedEntities.Count == 2, "should have only imported 2 entities");
      Assert.IsTrue(custs.All(c => c.City == "London"), "city should still be London after import");
      Assert.IsTrue(custs.All(c => ((String)c.EntityAspect.OriginalValuesMap["City"]) != "London"), "original city should not be London");
      Assert.IsTrue(impResult.TempKeyMap.All(kvp => kvp.Key != kvp.Value), "imported entities should not have same key values");
    }


    [TestMethod]
    public async Task ExpImpTempKeyCollisionOverwrite() {
      var em1 = await TestFns.NewEm(_serviceName);

      var q = new EntityQuery<Foo.Customer>("Customers").Take(5);

      var results = await q.Execute(em1);

      Assert.IsTrue(results.Count() > 0);
      var custs = results.Take(2).ToList();
      custs.ForEach(c => c.City = "Paris");
      var emp1 = em1.CreateEntity<Employee>();
      var emp2 = em1.CreateEntity<Employee>();

      var exportedEntities = em1.ExportEntities(new IEntity[] { custs[0], custs[1], emp1, emp2 }, false);

      custs.ForEach(c => c.City = "London");

      // custs1 and 2 shouldn't be imported because of default preserveChanges
      // emps1 and 2 should cause the creation of NEW emps with new temp ids;
      // tempKeys should cause creation of new entities;
      var impResult = em1.ImportEntities(exportedEntities, new ImportOptions(MergeStrategy.OverwriteChanges));
      var allEntities = em1.GetEntities();

      Assert.IsTrue(allEntities.Count() == 9, "should have 9 entities in the cache");

      Assert.IsTrue(custs.All(c => c.City == "Paris"), "city should be Paris after import");
      Assert.IsTrue(custs.All(c => ((String)c.EntityAspect.OriginalValuesMap["City"]) != "Paris"), "original city should not be Paris");
      Assert.IsTrue(allEntities.OfType<Customer>().Count() == 5, "should only be the original 5 custs");
      Assert.IsTrue(allEntities.OfType<Employee>().Count() == 4, "should be 4 emps (2 + 2) ");
      Assert.IsTrue(allEntities.OfType<Customer>().Count(c => c.EntityAspect.EntityState.IsModified()) == 2, "should only be 2 modified customers");
      Assert.IsTrue(allEntities.OfType<Employee>().All(c => c.EntityAspect.EntityState.IsAdded()));
      Assert.IsTrue(impResult.ImportedEntities.Count == 4, "should have only imported 4 entities");
      Assert.IsTrue(impResult.TempKeyMap.All(kvp => kvp.Key != kvp.Value), "imported entities should not have same key values");
    }

    [TestMethod]
    public async Task ExpImpTempKeyFixup1() {
      var em1 = await TestFns.NewEm(_serviceName);

      var q = new EntityQuery<Foo.Employee>("Employees").Take(3);

      var results = await q.Execute(em1);

      Assert.IsTrue(results.Count() > 0);
      var emp1 = new Employee();
      var order1 = new Order();
      var order2 = new Order();
      em1.AddEntity(emp1);
      emp1.Orders.Add(order1);
      emp1.Orders.Add(order2);

      var exportedEntities = em1.ExportEntities(null, false);

      // custs1 and 2 shouldn't be imported because of default preserveChanges
      // emps1 and 2 should cause the creation of NEW emps with new temp ids;
      // tempKeys should cause creation of new entities;
      var impResult = em1.ImportEntities(exportedEntities);
      var allEntities = em1.GetEntities();

      Assert.IsTrue(allEntities.Count() == 9, "should have 9 (3 orig, 3 added, 3 imported (new) entities in the cache");

      Assert.IsTrue(allEntities.OfType<Order>().Count() == 4, "should be 4 orders (2 + 2)");
      Assert.IsTrue(allEntities.OfType<Employee>().Count() == 5, "should be 5 emps (3 + 1 + 1) ");
      Assert.IsTrue(allEntities.OfType<Employee>().Count(c => c.EntityAspect.EntityState.IsAdded()) == 2, "should only be 2 added emps");
      Assert.IsTrue(allEntities.OfType<Order>().All(c => c.EntityAspect.EntityState.IsAdded()));
      Assert.IsTrue(impResult.ImportedEntities.Count == 6, "should have imported 6 entities - 3 orig + 3 new");
      Assert.IsTrue(impResult.ImportedEntities.OfType<Order>().Count() == 2, "should have imported 2 orders");
      Assert.IsTrue(impResult.ImportedEntities.OfType<Employee>().Count(e => e.EntityAspect.EntityState.IsAdded()) == 1, "should have imported 1 added emp");
      Assert.IsTrue(impResult.ImportedEntities.OfType<Employee>().Count(e => e.EntityAspect.EntityState.IsUnchanged()) == 3, "should have imported 3 unchanged emps");
      Assert.IsTrue(impResult.TempKeyMap.Count == 3, "tempKeyMap should be of length 3");
      Assert.IsTrue(impResult.TempKeyMap.All(kvp => kvp.Key != kvp.Value), "imported entities should not have same key values");
      var newOrders = impResult.ImportedEntities.OfType<Order>();
      var newEmp = impResult.ImportedEntities.OfType<Employee>().First(e => e.EntityAspect.EntityState.IsAdded());
      Assert.IsTrue(newOrders.All(no => no.EmployeeID == newEmp.EmployeeID), "should have updated order empId refs");

    }

    [TestMethod]
    public async Task ExpImpTempKeyFixup2() {
      var em1 = await TestFns.NewEm(_serviceName);

      var q = new EntityQuery<Foo.Supplier>("Suppliers").Where(s => s.CompanyName.StartsWith("P"));

      var suppliers = await q.Execute(em1);

      Assert.IsTrue(suppliers.Count() > 0, "should be some suppliers");
      var orderIdProp = em1.MetadataStore.GetEntityType(typeof(Order)).KeyProperties[0];
      em1.KeyGenerator.GetNextTempId(orderIdProp);

      var order1 = new Order();
      var emp1 = new Employee();
      em1.AddEntity(order1); em1.AddEntity(emp1);
      emp1.LastName = "bar";
      var cust1 = new Customer() { CompanyName = "Foo" };
      order1.Employee = emp1;
      order1.Customer = cust1;
      var exportedEm = em1.ExportEntities(null, false);

      var em2 = new EntityManager(em1);
      em2.ImportEntities(exportedEm);

      var suppliers2 = em2.GetEntities<Supplier>().ToList();
      Assert.IsTrue(suppliers.Count() == suppliers2.Count, "should be the same number of suppliers");
      var addedOrders = em2.GetEntities<Order>(EntityState.Added);
      Assert.IsTrue(addedOrders.Count() == 1, "should be 1 added order");
      var order1x = addedOrders.First();
      var cust1x = order1x.Customer;
      Assert.IsTrue(cust1x.CompanyName == "Foo", "customer company name should be 'Foo'");
      var emp1x = order1x.Employee;
      Assert.IsTrue(emp1x.LastName == "bar", "lastName should be 'bar'");

    }

    [TestMethod]
    public async Task ExpImpComplexType() {
      var em1 = await TestFns.NewEm(_serviceName);

      var q = new EntityQuery<Foo.Supplier>("Suppliers").Where(s => s.CompanyName.StartsWith("P"));

      var suppliers = await q.Execute(em1);
      suppliers.ForEach((s, i) => s.Location.Address = "Foo:" + i.ToString());
      Assert.IsTrue(suppliers.All(s => s.EntityAspect.EntityState.IsModified()));
      var exportedEm = em1.ExportEntities();
      var em2 = new EntityManager(em1);
      var impResult = em2.ImportEntities(exportedEm);
      Assert.IsTrue(impResult.ImportedEntities.Count == suppliers.Count());
      impResult.ImportedEntities.Cast<Supplier>().ForEach(s => {
        Assert.IsTrue(s.EntityAspect.OriginalValuesMap.Count == 0, "supplierOriginalValuesMap should be empty");
        var location = s.Location;
        Assert.IsTrue(location.Address.StartsWith("Foo"), "address should start with 'Foo'");
        Assert.IsTrue(location.ComplexAspect.OriginalValuesMap.ContainsKey("Address"), "ComplexAspect originalValues should contain address");

      });
    }


    [TestMethod]
    public async Task ExpImpWithNulls() {
      var em1 = await TestFns.NewEm(_serviceName);

      var queryOptions = new QueryOptions(FetchStrategy.FromServer, MergeStrategy.OverwriteChanges);
      var q0 = new EntityQuery<Customer>().Where(c => c.CompanyName != null && c.City != null)
         .With(MergeStrategy.OverwriteChanges);
      var r0 = (await em1.ExecuteQuery(q0)).ToList();
      Assert.IsTrue(r0.Count > 2);
      r0[0].CompanyName = null;
      r0[1].City = null;
      var exportedEntities = em1.ExportEntities(null, false);
      var em2 = new EntityManager(em1);
      em2.ImportEntities(exportedEntities);
      var ek0 = r0[0].EntityAspect.EntityKey;
      var ek1 = r0[1].EntityAspect.EntityKey;
      var e0 = em2.GetEntityByKey<Customer>(ek0);
      Assert.IsTrue(e0.CompanyName == null, "company name should be null");
      Assert.IsTrue(e0.EntityAspect.EntityState.IsModified());
      var e1 = em2.GetEntityByKey<Customer>(ek1);
      Assert.IsTrue(e1.City == null, "city should be null");
      Assert.IsTrue(e1.EntityAspect.EntityState.IsModified());
      em2.AcceptChanges();
      var exportedEntities2 = em2.ExportEntities(null, false);
      em1.ImportEntities(exportedEntities2, new ImportOptions(MergeStrategy.OverwriteChanges));
      Assert.IsTrue(em1.GetChanges().Count() == 0);

    }

    [TestMethod]
    public async Task ExpImpDeleted() {
      var em1 = await TestFns.NewEm(_serviceName);

      var c1 = new Customer() { CompanyName = "Test_1", City = "Oakland", RowVersion = 13, Fax = "510 999-9999" };
      var c2 = new Customer() { CompanyName = "Test_2", City = "Oakland", RowVersion = 13, Fax = "510 999-9999" };
      em1.AddEntity(c1);
      em1.AddEntity(c2);
      var sr = await em1.SaveChanges();
      Assert.IsTrue(sr.Entities.Count == 2);
      c1.EntityAspect.Delete();
      c2.CompanyName = TestFns.MorphString(c2.CompanyName);
      var exportedEntities = em1.ExportEntities(null, false);
      var em2 = new EntityManager(em1);
      em2.ImportEntities(exportedEntities);
      var c1x = em2.GetEntityByKey<Customer>(c1.EntityAspect.EntityKey);
      Assert.IsTrue(c1x.EntityAspect.EntityState.IsDeleted(), "should be deleted");
      var c2x = em2.GetEntityByKey<Customer>(c2.EntityAspect.EntityKey);
      Assert.IsTrue(c2x.CompanyName == c2.CompanyName, "company names should match");
    }

    private void ResetTempKeyGeneratorSeed()
    {
      // A relaunch of the client would reset the temporary key generator
      // Simulate that for test purposes ONLY with an internal seed reset
      // that no one should know about or ever use.
      // SHHHHHHHH!
      // NEVER DO THIS IN YOUR PRODUCTION CODE
      Breeze.Sharp.DataType.NextNumber = -1;
    }

    [TestMethod]
    public async Task TemporaryKeyNotPreservedOnImport()
    {
      // If an earlier test has already created an entity, a new entity will not be assigned a key value of -1
      // Simulate initial launch of the client app
      ResetTempKeyGeneratorSeed();

      var manager1 = new EntityManager(_serviceName);
      manager1.MetadataStore.AllowedMetadataMismatchTypes = MetadataMismatchType.MissingCLREntityType;
      await manager1.FetchMetadata(); // Metadata must be fetched before CreateEntity() can be called

      // Create a new Order. The Order key is store-generated.
      // Until saved, the new Order has a temporary key such as '-1'.
      var acme1 = manager1.CreateEntity<Order>(new { ShipName = "Acme" });
      Assert.AreEqual(-1, acme1.OrderID, "Initial entity not assigned temp key -1");

      // export without metadata
      var exported = manager1.ExportEntities(new IEntity[] { acme1 }, false);

      // ... much time passes 
      // ... the client app is re-launched
      // ... the seed for the temporary id generator was reset
      ResetTempKeyGeneratorSeed();

      // Create a new manager2 with metadata
      var manager2 = new EntityManager(manager1);

      // Add a new order to manager2
      // This new order has a temporary key.
      // That key could be '-1' ... the same key as acme1!!!
      var beta = (Order)manager2.CreateEntity(typeof(Order), new { ShipName = "Beta" });

      // Its key will be '-1' ... the same key as acme1!!!
      Assert.AreEqual(-1, beta.OrderID);

      // Import the the exported acme1 from manager1
      // and get the newly merged instance from manager2
      var imported = manager2.ImportEntities(exported);
      var acme2 = imported.ImportedEntities.Cast<Order>().First();

      // compare the "same" order as it is in managers #1 and #2  
      var isSameName = acme1.ShipName == acme2.ShipName; // true
      Assert.IsTrue(isSameName, "ShipNames should be the same");

      // breeze had to update the acme key in manager2 because 'beta' already has ID==-1   
      var isSameId = acme1.OrderID == acme2.OrderID; // false; temporary keys are different
      Assert.IsFalse(isSameId, "OrderIDs (temporary keys) should be different");
    }

    [TestMethod]
    public async Task TemporaryKeyGeneratedAfterImport()
    {
      // If an earlier test has already created an entity, a new entity will not be assigned a key value of -1
      // Simulate initial launch of the client app
      ResetTempKeyGeneratorSeed();

      var manager1 = new EntityManager(_serviceName);
      manager1.MetadataStore.AllowedMetadataMismatchTypes = MetadataMismatchType.MissingCLREntityType;
      await manager1.FetchMetadata(); // Metadata must be fetched before CreateEntity() can be called

      // Create a new Order. The Order key is store-generated.
      // Until saved, the new Order has a temporary key such as '-1'.
      var acme1 = manager1.CreateEntity<Order>(new { ShipName = "Acme" });
      Assert.AreEqual(-1, acme1.OrderID, "Initial entity not assigned temp key -1");

      // export without metadata
      var exported = manager1.ExportEntities(new IEntity[] { acme1 }, false);

      // ... much time passes 
      // ... the client app is re-launched
      // ... the seed for the temporary id generator was reset
      ResetTempKeyGeneratorSeed();

      // Create a new manager2 with metadata
      var manager2 = new EntityManager(manager1);

      // Import the the exported acme1 from manager1
      // and get the newly merged instance from manager2
      var imported = manager2.ImportEntities(exported);
      var acme2 = imported.ImportedEntities.Cast<Order>().First();

      // compare the "same" order as it is in managers #1 and #2  
      var isSameName = acme1.ShipName == acme2.ShipName; // true
      Assert.IsTrue(isSameName, "ShipNames should be the same");

      // Add a new order to manager2
      // This new order has a temporary key.
      // That key could be '-1' ... the same key as acme1!!!
      var beta = (Order)manager2.CreateEntity(typeof(Order), new { ShipName = "Beta" });

      var isSameId = beta.OrderID == acme2.OrderID;
      Assert.IsFalse(isSameId, "OrderIDs (temporary keys) should not be the same");
    }
    
  }
}
