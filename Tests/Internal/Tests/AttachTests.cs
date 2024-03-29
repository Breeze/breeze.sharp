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
using System.ComponentModel.Design;

namespace Breeze.Sharp.Tests {

  [TestClass]
  public class AttachTests {


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
    public async Task CreateEntityWithInitializer() {
      var em1 = await TestFns.NewEm(_serviceName);

      var cust = (Customer) em1.CreateEntity(typeof (Customer), new {CompanyName = "Testzzz", City = "Los Angeles", RowVersion = 3});
      Assert.IsTrue(cust.EntityAspect.EntityState.IsAdded());
      Assert.IsTrue(cust.CompanyName == "Testzzz");
      Assert.IsTrue(cust.City == "Los Angeles");
      Assert.IsTrue(cust.RowVersion == 3);
    }

    [TestMethod]
    public async Task CreateEntityWithInitializer2() {
      var em1 = await TestFns.NewEm(_serviceName);
      var order = new Order();
      var cust = (Customer)em1.CreateEntity(typeof(Customer), new { CompanyName = "Testzzz", Orders = new Order[] {order}, RowVersion = 3 });
      Assert.IsTrue(cust.EntityAspect.EntityState.IsAdded());
      Assert.IsTrue(cust.CompanyName == "Testzzz");
      Assert.IsTrue(cust.Orders.Count == 1 && cust.Orders.First() == order);
      Assert.IsTrue(cust.RowVersion == 3);
      Assert.IsTrue(order.EntityAspect.EntityState.IsAdded());
      Assert.IsTrue(order.Customer == cust);
      Assert.IsTrue(order.CustomerID == cust.CustomerID);
    }

    [TestMethod]
    public async Task RejectChanges() {
      var em1 = await TestFns.NewEm(_serviceName);


      var prod1 = new Product();
      prod1.Discontinued = true;
      em1.AttachEntity(prod1, EntityState.Unchanged);
      prod1.Discontinued = false;
      prod1.EntityAspect.RejectChanges();
      
      Assert.IsTrue(prod1.Discontinued == true, "should have returned to initial state");
    }

    [TestMethod]
    public async Task RejectChanges2() {
      var em1 = await TestFns.NewEm(_serviceName);


      var prod1 = new Product();
      prod1.Discontinued = true;
      em1.AddEntity(prod1);
      em1.RejectChanges();


      Assert.IsTrue(prod1.EntityAspect.EntityState.IsDetached());
    }

    [TestMethod]
    public async Task RejectChangesFkFixup() {
      var em1 = await TestFns.NewEm(_serviceName);

      var emp1 = new Employee();
      em1.AttachEntity(emp1);
      var emp2 = em1.CreateEntity<Employee>();
      
      var order1 = new Order();
      emp1.Orders.Add(order1);
      Assert.IsTrue(order1.EntityAspect.EntityState.IsAdded());
      Assert.IsTrue(order1.Employee == emp1);
      order1.EntityAspect.AcceptChanges();
      // move the order;
      emp2.Orders.Add(order1);
      Assert.IsTrue(emp1.Orders.Count == 0);
      Assert.IsTrue(emp2.Orders.Count == 1);
      Assert.IsTrue(order1.Employee == emp2);
      Assert.IsTrue(order1.EntityAspect.EntityState.IsModified());
      order1.EntityAspect.RejectChanges();
      Assert.IsTrue(order1.Employee == emp1);
      Assert.IsTrue(emp1.Orders.Count == 1);
      Assert.IsTrue(emp2.Orders.Count == 0);
    }

    [TestMethod]
    public async Task RejectChangesValidationFixup() {
      var em1 = await TestFns.NewEm(_serviceName);

      var cust1 = new Customer();
      // cust1.RowVersion = 0;
      em1.AttachEntity(cust1);
      Assert.IsTrue(cust1.EntityAspect.ValidationErrors.Count == 1);
      cust1.CompanyName = "valid name";
      Assert.IsTrue(cust1.EntityAspect.ValidationErrors.Count == 0);
      cust1.EntityAspect.RejectChanges();
      Assert.IsTrue(cust1.EntityAspect.ValidationErrors.Count == 1);
    }


    [TestMethod]
    public async Task NullForeignKey() {
      var em1 = await TestFns.NewEm(_serviceName);
      var prod1 = new Product();

      em1.AttachEntity(prod1);
      prod1.ProductName = "Test";
      prod1.SupplierID = null;

      EntityQuery<Product> q0;
      if (Configuration.Instance.QueryUriStyle == QueryUriStyle.JSON) {
        // server doesn't like the "p.Supplier != null" expression
        q0 = new EntityQuery<Product>().Where(p => p.Supplier.SupplierID != 0).Take(2).Expand(p => p.Supplier);
      } else {
        q0 = new EntityQuery<Product>().Where(p => p.Supplier != null).Take(2).Expand(p => p.Supplier);
      }
      var r0 = (await q0.Execute(em1)).ToList();
      Assert.IsTrue(r0.Count() == 2);
      Assert.IsTrue(r0.All(p => p.Supplier != null));
      var p0 = r0[0];
      var p1 = r0[1];
      var s0 = p0.Supplier;
      var s1 = p1.Supplier;

      Assert.IsTrue(s0.Products.Contains(p0));
      p0.Supplier = null;
      Assert.IsTrue(p0.SupplierID == null);
      Assert.IsTrue(!s0.Products.Contains(p0));
      
      Assert.IsTrue(s1.Products.Contains(p1));
      p1.SupplierID = null;
      Assert.IsTrue(p1.Supplier == null);
      Assert.IsTrue(!s1.Products.Contains(p1));
    }
   

    [TestMethod]
    public async Task CreateEntity() {
      var em1 = await TestFns.NewEm(_serviceName);

      var emp1 = em1.CreateEntity(typeof(Employee));
      Assert.IsTrue(emp1.EntityAspect.EntityState == EntityState.Added);
      var emp3 = em1.CreateEntity<Employee>(null, EntityState.Detached);
      emp3.LastName = "Smith";
      Assert.IsTrue(emp3.EntityAspect.EntityState.IsDetached());
      Assert.IsTrue(emp3.LastName == "Smith");
    }

    [TestMethod]
    public async Task AttachEntityDefaultValues() {
      // Default values when entity is first created.
      var em1 = await TestFns.NewEm(_serviceName);

      var employeeType = em1.MetadataStore.GetEntityType(typeof(Employee));
      var empIdProp = employeeType.GetDataProperty(TestFns.EmployeeKeyName);

      var emp = em1.CreateEntity<Employee>(entityState: EntityState.Unchanged);
      Assert.IsTrue(emp.EmployeeID == 0, "id should be zero at creation");
      var agkType = employeeType.AutoGeneratedKeyType;
      emp.EntityAspect.Detach();
      Assert.IsTrue(emp.EntityAspect.IsDetached);
      // manager should NOT replace '0' with generated temp id 
      em1.AttachEntity(emp, EntityState.Unchanged);
      Assert.IsTrue(emp.EmployeeID == 0, "should still be 0");
      emp.EntityAspect.Detach();
      em1.AttachEntity(emp, EntityState.Added);
      Assert.IsTrue(emp.EmployeeID != 0, "should not be be 0");
    }

    [TestMethod]
    public async Task AddToNavSet() {
        var em1 = await TestFns.NewEm(_serviceName);
        
        var customer = new Customer();
        var order = new Order();
        em1.AttachEntity(customer);
        var origOrders = customer.Orders;
        Assert.IsTrue(origOrders.Count == 0);
        origOrders.Add(order);
        // next line won't compile - this is good!
        // customer.Orders = new NavigationSet<Order>();
        Assert.IsTrue(origOrders.Count == 1);
        Assert.IsTrue(customer.Orders == origOrders, "should be same collection");
        Assert.IsTrue(order.EntityAspect.IsAttached, "should be attached");
        Assert.IsTrue(order.Customer == customer, "order.Customer should = customer");
    }
    

     // can attach a detached entity to a different manager via attach/detach
    [TestMethod]
    public async Task AttachToDifferentManager() {
      var em1 = await TestFns.NewEm(_serviceName);

      var cust = em1.CreateEntity<Customer>(null, EntityState.Unchanged);
      var order = em1.CreateEntity<Order>(entityState: EntityState.Unchanged);
      cust.Orders.Add(order);
      Assert.IsTrue(cust.Orders.Count == 1);
      var em2 = new EntityManager(em1);
      try {
        em2.AttachEntity(cust);
        Assert.Fail("should not get here");
      } catch {
        // expected
      }

      em1.DetachEntity(cust);
      Assert.IsTrue(order.Customer == null);
      Assert.IsTrue(cust.Orders.Count == 0);
      em2.AttachEntity(cust);
      Assert.IsTrue(cust.EntityAspect.EntityManager == em2);

    }
    
    // can attach a detached entity to a different manager via clear
    [TestMethod]
    public async Task AttachEmViaDetach() {
      var em1 = await TestFns.NewEm(_serviceName);

        var cust = new Customer();
        cust.EntityAspect.SetValue(TestFns.CustomerKeyName, Guid.NewGuid());
        Assert.IsTrue(cust.EntityAspect.IsDetached, "should be detached");
        em1.AttachEntity(cust);
        Assert.IsTrue(cust.EntityAspect.IsAttached, "should be attached");
        em1.Clear(); // will detach cust

        Assert.IsTrue(cust.EntityAspect.IsDetached, "should be detached - again");
        Assert.IsTrue(cust.EntityAspect.EntityManager == em1, "should still be associated with em1");
        // therefore this should be ok
        var em2 = new EntityManager(em1);
        em2.AttachEntity(cust);
        Assert.IsTrue(cust.EntityAspect.EntityManager == em2, "should be on em2");
    }

    
    // setting child's parent entity null removes it from old parent
    [TestMethod]
    public async Task SetParentEntityToNull() {
      var em1 = await TestFns.NewEm(_serviceName);

      var cust = em1.CreateEntity<Customer>(null, EntityState.Unchanged);
      var order = em1.CreateEntity<Order>();
      order.Customer = cust;
      Assert.IsTrue(order.Customer == cust, "should be set");
      Assert.IsTrue(cust.Orders.First() == order, "should be part of collection" );
      order.Customer = null;
      Assert.IsTrue(order.Customer == null, "should not be set");
      Assert.IsTrue(cust.Orders.Count == 0, "should not be part of collection" );

    }

    // unidirectional attach - n->1
    [TestMethod]
    public async Task UnidirectionalAttachFk() {
      var em1 = await TestFns.NewEm(_serviceName);
      if (TestFns.DEBUG_MONGO) {
        Assert.Inconclusive("NA for Mongo - Order/OrderDetail");
        return;
      }

      var od1 = new OrderDetail();
      var prod1 = new Product();
      od1.ProductID = -99;
      em1.AttachEntity(od1);
      em1.AttachEntity(prod1);
      Assert.IsTrue(od1.Product == null, "Product should be null");
      prod1.ProductID = 2;
      od1.ProductID = 2;
      Assert.IsTrue(od1.Product == prod1, "should now point to product");

      var od2 = new OrderDetail();
      var prod2 = new Product();
      od2.ProductID = -88;
      em1.AttachEntity(od2);
      em1.AttachEntity(prod2);
      Assert.IsTrue(od2.Product == null, "Product should be null - again");
      // same as above but different order
      od2.ProductID = 3;
      // should now have an unresolved parent.
      prod2.ProductID = 3;
      
      Assert.IsTrue(od2.Product == prod2, "should now point to product - again");
    }


    // unidirectional attach - 1->n
    [TestMethod]
    public async Task UnidirectionalAttach1ToN() {
      var em1 = await TestFns.NewEm(_serviceName);

      if (TestFns.DEBUG_MONGO || TestFns.DEBUG_ODATA) {
        Assert.Inconclusive( "NA for Mongo or OData - TimeList and Timegroup not yet added");
      }
      
      var tl1 = em1.CreateEntity<TimeLimit>();
      var tl2 = em1.CreateEntity<TimeLimit>();
      var tg1 = em1.CreateEntity<TimeGroup>();
      var id1 = tg1.Id;
      tl1.TimeGroupId = id1;
      Assert.IsTrue(tg1.TimeLimits.Count == 1 
        && tg1.TimeLimits.Contains(tl1), "should be connected");
      tl2.TimeGroupId = id1;
      Assert.IsTrue(tg1.TimeLimits.Count == 2 
        && tg1.TimeLimits.Contains(tl2), "another should be connected");

    
    }

    // unidirectional attach - 1->n
    [TestMethod]
    public async Task UnidirectionalAttach1ToN_v2() {
      var em1 = await TestFns.NewEm(_serviceName);

      if (TestFns.DEBUG_MONGO || TestFns.DEBUG_ODATA) {
        Assert.Inconclusive( "NA for Mongo or OData - TimeList and Timegroup not yet added");
      }
      
      var tl1 = em1.CreateEntity<TimeLimit>();
      var tl2 = em1.CreateEntity<TimeLimit>();
      var tg1 = em1.CreateEntity<TimeGroup>();
      Assert.IsTrue(tg1.TimeLimits.Count == 0, "should be no Timelimits");
      tg1.TimeLimits.Add(tl1);
      Assert.IsTrue(tg1.TimeLimits.Count == 1 
        && tg1.TimeLimits.Contains(tl1), "should be connected");
      tg1.TimeLimits.Add(tl2);
      Assert.IsTrue(tg1.TimeLimits.Count == 2 
        && tg1.TimeLimits.Contains(tl2), "another should be connected");

      // add 1 that is already there
      tg1.TimeLimits.Add(tl1);
      Assert.IsTrue(tg1.TimeLimits.Count == 2 
        && tg1.TimeLimits.Contains(tl1), "length should not change when adding a dup");
    }

    // primary key fixup
    [TestMethod]
    public async Task PrimaryKeyFixup() {
      var em1 = await TestFns.NewEm(_serviceName);

      var prod = new Product();
      em1.AttachEntity(prod);
      var origProdId = prod.ProductID;
      var ek = prod.EntityAspect.EntityKey;
      var sameProd = em1.GetEntityByKey(ek);
      Assert.IsTrue(prod == sameProd, "should be the same product");
      var sameProd2 = em1.GetEntityByKey<Product>(origProdId);
      Assert.IsTrue(prod == sameProd2, "should be the same product-again");
      prod.ProductID = 7;
      var notSameProd = em1.GetEntityByKey(ek);
      Assert.IsTrue(notSameProd == null);
      var sameProd3 = em1.GetEntityByKey(prod.EntityAspect.EntityKey);
      Assert.IsTrue(prod == sameProd2, "should be the same product-again 2");
    }
    
    // changing FK to null removes it from old parent
    [TestMethod]
    public async Task FkSetToNull() {
      var em1 = await TestFns.NewEm(_serviceName);

      var cust = em1.CreateEntity<Customer>(entityState: EntityState.Unchanged);
      var order1 = em1.CreateEntity<Order>();
      order1.Customer = cust;
      Assert.IsTrue(order1.Customer == cust, "should be customer");
      Assert.IsTrue(cust.Orders.Contains(order1), "should contain order1");

      var order2 = new Order();
      order2.Customer = cust;
      Assert.IsTrue(order2.EntityAspect.IsAttached && order2.EntityAspect.EntityState.IsAdded());
      Assert.IsTrue(order2.Customer == cust, "should be customer - again");
      Assert.IsTrue(cust.Orders.Contains(order2), "should contain order2");

      order1.CustomerID = null;
      Assert.IsTrue(order1.Customer == null, "should be null");
      Assert.IsTrue(!cust.Orders.Contains(order1), "should not contain order1");

    }

    
    // add, detach and readd
    [TestMethod]
    public async Task AddDetachReadd() {
      var em1 = await TestFns.NewEm(_serviceName);

      var order = em1.CreateEntity<Order>();
      Assert.IsTrue(order.EntityAspect.IsAttached && order.EntityAspect.EntityState.IsAdded());
      em1.DetachEntity(order);
      Assert.IsTrue(order.EntityAspect.IsDetached);
      em1.AttachEntity(order, EntityState.Added);
      Assert.IsTrue(order.EntityAspect.IsAttached && order.EntityAspect.EntityState.IsAdded());
    }

    // attach, detach and reattach
    [TestMethod]
    public async Task AttachDetachReattach() {
      var em1 = await TestFns.NewEm(_serviceName);

      var order = em1.CreateEntity<Order>(null, EntityState.Unchanged);
      Assert.IsTrue(order.EntityAspect.IsAttached && order.EntityAspect.EntityState.IsUnchanged());
      em1.DetachEntity(order);
      Assert.IsTrue(order.EntityAspect.IsDetached);
      em1.AttachEntity(order);
      Assert.IsTrue(order.EntityAspect.IsAttached && order.EntityAspect.EntityState.IsUnchanged());
    }

    // attach, detach and reattach
    [TestMethod]
    public async Task AttachDetachReattachNavProps() {
      var em1 = await TestFns.NewEm(_serviceName);

      var cust1 = new Customer() {  CustomerID = Guid.NewGuid() };
      em1.AttachEntity(cust1);
      var cust2 = new Customer() {  CustomerID =  Guid.NewGuid() };
      em1.AttachEntity(cust2); 
      var order = em1.CreateEntity<Order>(null, EntityState.Unchanged);
      Assert.IsTrue(order.EntityAspect.IsAttached && order.EntityAspect.EntityState.IsUnchanged());
      cust1.Orders.Add(order);
      Assert.IsTrue(order.Customer == cust1);
      Assert.IsTrue(order.EntityAspect.IsAttached && order.EntityAspect.EntityState.IsModified());
      em1.DetachEntity(order);
      Assert.IsTrue(order.EntityAspect.IsDetached);
      Assert.IsTrue(order.Customer == null);
      em1.AttachEntity(order);
      Assert.IsTrue(order.Customer == cust1);
      Assert.IsTrue(order.EntityAspect.IsAttached && order.EntityAspect.EntityState.IsUnchanged());

    }

    // attach, detach and reattach
    [TestMethod]
    public async Task AttachDetachOriginalValues() {
      var em1 = await TestFns.NewEm(_serviceName);

      var cust1 = new Customer() { CustomerID = Guid.NewGuid() };
      em1.AttachEntity(cust1);
      cust1.ContactName = "original contact name";
      cust1.EntityAspect.AcceptChanges();
      Assert.IsTrue(cust1.EntityAspect.IsAttached && cust1.EntityAspect.EntityState.IsUnchanged());

      cust1.ContactName = "new contact name";
      Assert.IsTrue(cust1.EntityAspect.IsAttached && cust1.EntityAspect.EntityState.IsModified());
      em1.DetachEntity(cust1);
      Assert.IsTrue(cust1.EntityAspect.IsDetached);
      Assert.IsTrue(cust1.ContactName == "new contact name");
      em1.AttachEntity(cust1, EntityState.Modified);
      Assert.IsTrue(cust1.ContactName == "new contact name");
      Assert.IsTrue(cust1.EntityAspect.IsAttached && cust1.EntityAspect.EntityState.IsModified());
      cust1.EntityAspect.RejectChanges();
      Assert.IsTrue(cust1.EntityAspect.IsAttached && cust1.EntityAspect.EntityState.IsUnchanged());
      Assert.IsTrue(cust1.ContactName == "original contact name");

      cust1.ContactName = "new foo";
      Assert.IsTrue(cust1.EntityAspect.IsAttached && cust1.EntityAspect.EntityState.IsModified());
      em1.DetachEntity(cust1);
      Assert.IsTrue(cust1.EntityAspect.IsDetached);
      Assert.IsTrue(cust1.ContactName == "new foo");
      
      em1.AttachEntity(cust1);
      Assert.IsTrue(cust1.ContactName == "new foo");
      Assert.IsTrue(cust1.EntityAspect.IsAttached && cust1.EntityAspect.EntityState.IsUnchanged());
      Assert.IsTrue(cust1.EntityAspect.OriginalValuesMap.Count == 0);
      cust1.EntityAspect.RejectChanges();
      Assert.IsTrue(cust1.ContactName == "new foo");
      Assert.IsTrue(cust1.EntityAspect.IsAttached && cust1.EntityAspect.EntityState.IsUnchanged());
    }
    
    // exception if set nav to entity with different manager
    [TestMethod]
    public async Task ErrorOnNavAttach() {
      var em1 = await TestFns.NewEm(_serviceName);
    
      var order = em1.CreateEntity<Order>(null, EntityState.Unchanged);
    
      var em2 = new EntityManager(em1);
      var cust = em2.CreateEntity<Customer>(null, EntityState.Unchanged);
      Assert.IsTrue(order.EntityAspect.EntityManager != cust.EntityAspect.EntityManager, "should not be the same manager");
      try {
        order.Customer = cust;
        Assert.Fail("should not get here");
      } catch (Exception e) {
        Assert.IsTrue(e.Message.Contains("EntityManager"), "message should mention 'EntityManager'");
      }
      cust.EntityAspect.Detach();
      order.Customer = cust;
      Assert.IsTrue(order.EntityAspect.EntityManager == cust.EntityAspect.EntityManager, "should be the same manager");
      Assert.IsTrue(cust.Orders.Contains(order) && order.Customer == cust, "should be properly connected");
    }

    // exception if set nav to entity with different manager
    [TestMethod]
    public async Task ErrorOnAttachMultiple() {
      var em1 = await TestFns.NewEm(_serviceName);
    
      var order = em1.CreateEntity<Order>(null, EntityState.Unchanged);
      var em2 = new EntityManager(em1);
      try {
        em2.AttachEntity(order);
        Assert.Fail("should not get here");
      } catch (Exception e) {
        Assert.IsTrue(e.Message.Contains("EntityManager"), "message should mention 'EntityManager'");
      }
    }
      
       
    // rejectChanges on added entity
    [TestMethod]
    public async Task RejectChangesOnAdd() {
      var em1 = await TestFns.NewEm(_serviceName);
      var order = em1.CreateEntity<Order>();
      Assert.IsTrue(order.EntityAspect.EntityState.IsAdded(), "should be in Added state");
      Assert.IsTrue(em1.HasChanges(), "should have some changes");
      var ents = em1.GetEntities();
      Assert.IsTrue(ents.Count() == 1);
      order.EntityAspect.RejectChanges();
      Assert.IsTrue(order.EntityAspect.IsDetached);
      Assert.IsTrue(!em1.HasChanges(), "should not have any changes");
      ents = em1.GetEntities();
      Assert.IsTrue(ents.Count() == 0);

    }
    
    // delete added entity
    [TestMethod]
    public async Task DeleteAdded() {
      var em1 = await TestFns.NewEm(_serviceName);
      var order = em1.CreateEntity<Order>();
      Assert.IsTrue(order.EntityAspect.EntityState.IsAdded(), "should be in Added state");
      Assert.IsTrue(em1.HasChanges(), "should have some changes");
      order.EntityAspect.Delete();
      Assert.IsTrue(order.EntityAspect.IsDetached);
      Assert.IsTrue(!em1.HasChanges(), "should not have any changes");
      var ents = em1.GetEntities();
      Assert.IsTrue(ents.Count() == 0);
    }

    // add entity - no key
    [TestMethod]
    public async Task AddEntityNoOrPartialKey() {
      var em1 = await TestFns.NewEm(_serviceName);

      if (TestFns.DEBUG_MONGO) {
        Assert.Inconclusive("NA for Mongo - OrderDetail");
        return;
      }
      var od = new OrderDetail();
      try {
        em1.AttachEntity(od, EntityState.Added);
        Assert.Fail("should not get here");
      } catch (Exception e) {
        Assert.IsTrue(e.Message.Contains("key"), "error message should contain 'key'");
      }

      // only need to set part of the key
      od.OrderID = 999;
      em1.AttachEntity(od, EntityState.Added);
      Assert.IsTrue(od.EntityAspect.EntityState.IsAdded());

      try {
        var od2 =em1.CreateEntity<OrderDetail>(EntityState.Added);
        Assert.Fail("should not get here");
      } catch (Exception e) {
        Assert.IsTrue(e.Message.Contains("key"), "error message should contain 'key'");
      }
    }

    // add child
    [TestMethod]
    public async Task AddToNavCollection() {
      var em1 = await TestFns.NewEm(_serviceName);

      var cust1 = new Customer();
      var order1 = new Order();
      em1.AttachEntity(cust1, EntityState.Added);
      Assert.IsTrue(cust1.EntityAspect.HasTemporaryKey, "should have a temp key");
      var orders = cust1.Orders;
      Assert.IsTrue(orders.ParentEntity == cust1, "ParentEntity should be set");
      Assert.IsTrue(orders.NavigationProperty == cust1.EntityAspect.EntityType.GetNavigationProperty("Orders"), "NavProperty should be set");

      NotifyCollectionChangedEventArgs changeArgs = null;
      orders.CollectionChanged += (s, e) => {
        changeArgs = e;
      };
      orders.Add(order1);
      Assert.IsTrue(order1.EntityAspect.EntityState.IsAdded(), "should be added");
      Assert.IsTrue(orders.Contains(order1), "should contain order");
      Assert.IsTrue(order1.Customer == cust1, "should be connected");
      Assert.IsTrue(changeArgs != null, "changeArgs should not be null");
      Assert.IsTrue(changeArgs.Action == NotifyCollectionChangedAction.Add);
      Assert.IsTrue(changeArgs.NewItems.Contains(order1), "change should mention order1");

    }

    [TestMethod]
    public async Task EntityAndPropertyChangedEvents() {
      var em1 = await TestFns.NewEm(_serviceName);

      var q = new EntityQuery<Supplier>().Where(s => s.CompanyName.StartsWith("P")).Take(2);

      var suppliers = await em1.ExecuteQuery(q);
      Assert.IsTrue(suppliers.Count() > 0, "should have returned some suppliers");

      var supp0 = suppliers.First();
      List<EntityChangedEventArgs> entityChangedList = new List<EntityChangedEventArgs>();
      List<PropertyChangedEventArgs> propChangedList = new List<PropertyChangedEventArgs>();
      List<PropertyChangedEventArgs> aspectPropChangedList = new List<PropertyChangedEventArgs>();
      em1.EntityChanged += (s, e) => {
        entityChangedList.Add(e);
      };
      ((INotifyPropertyChanged)supp0).PropertyChanged += (s, e) => {
        propChangedList.Add(e);
      };
      supp0.EntityAspect.PropertyChanged += (s, e) => {
        
        aspectPropChangedList.Add(e);
      };

      supp0.CompanyName = "xxx";
      var lastEc = entityChangedList.Last();
      var ecPcArg = entityChangedList.FirstOrDefault(ecArg => ecArg.Action == EntityAction.PropertyChange);
      Assert.IsTrue(ecPcArg != null, "should find a propertyChange event");
      var pcArgs = ecPcArg.ActionEventArgs as PropertyChangedEventArgs;
      Assert.IsTrue(pcArgs.PropertyName == "CompanyName", "ActionEventArgs should be set");
      Assert.IsTrue(lastEc.EntityAspect == supp0.EntityAspect, "ec should have been fired");
      
      Assert.IsTrue(entityChangedList[0].Action == EntityAction.PropertyChange && entityChangedList[0].Entity == supp0);
      Assert.IsTrue(entityChangedList[1].Action == EntityAction.EntityStateChange && entityChangedList[1].Entity == supp0);
      
      Assert.IsTrue(aspectPropChangedList.Count == 2, "2 aspects should have changed"); // isChanged and EntityState.

      Assert.IsTrue(propChangedList[0].PropertyName == "CompanyName");
      entityChangedList.Clear();
      propChangedList.Clear();
      aspectPropChangedList.Clear();
      supp0.HomePage = "xxxxx";
      supp0.Phone = "eeeeee";
      Assert.IsTrue(entityChangedList.Count == 2, "should be 2 entity changed events");
      Assert.IsTrue(propChangedList.Count == 2, "should be 2 propChanged events");
      Assert.IsTrue(aspectPropChangedList.Count == 0, "no more EntityAspect changes");
    }


    // detach child
    [TestMethod]
    public async Task RemoveFromNavCollection() {
      var em1 = await TestFns.NewEm(_serviceName);

      var cust1 = new Customer();
      var order1 = new Order();
      var order2 = new Order();
      em1.AddEntity(cust1);
      var orders = cust1.Orders;
      orders.Add(order1);
      orders.Add(order2);
      
      var collectionChangedList = new List<NotifyCollectionChangedEventArgs>();
      orders.CollectionChanged += (s, e) => {
        collectionChangedList.Add(e);
      };
      var propChangedList = new List<PropertyChangedEventArgs>();
      ((INotifyPropertyChanged)order1).PropertyChanged += (s, e) => {
        propChangedList.Add(e);
      };
      orders.Remove(order1);
      Assert.IsTrue(collectionChangedList.Last().Action == NotifyCollectionChangedAction.Remove);
      Assert.IsTrue(collectionChangedList.Last().OldItems.Contains(order1), "change event should contain order1");
      
      Assert.IsTrue(propChangedList.Any(args => args.PropertyName == "Customer"), "propChange should mention Customer");
      Assert.IsTrue(propChangedList.Any(args => args.PropertyName == "CustomerID"), "propChange should mention CustomerID");

      Assert.IsTrue(!orders.Contains(order1), "order1 should have been removed");
      Assert.IsTrue(order1.Customer == null, "Customer should be null");
      Assert.IsTrue(order1.CustomerID == null, "CustomerID should be null"); // null because not required.
      Assert.IsTrue(order1.EntityAspect.EntityState.IsAdded());
      Assert.IsTrue(orders.Count == 1, "count should be 1");

      collectionChangedList.Clear();
      propChangedList.Clear();
      order1.Customer = cust1;
      Assert.IsTrue(collectionChangedList.Last().Action == NotifyCollectionChangedAction.Add);
      Assert.IsTrue(collectionChangedList.Last().NewItems.Contains(order1), "change event should contain order1");
      Assert.IsTrue(propChangedList.Any(args => args.PropertyName == "Customer"), "propChange should mention Customer");
      Assert.IsTrue(propChangedList.Any(args => args.PropertyName == "CustomerID"), "propChange should mention CustomerID");

      Assert.IsTrue(orders.Contains(order1), "order1 should be back");
      Assert.IsTrue(order1.Customer == cust1, "Customer should be back");
      Assert.IsTrue(order1.CustomerID == cust1.CustomerID, "CustomerID should be back"); // null because not required.
      Assert.IsTrue(order1.EntityAspect.EntityState.IsAdded());
      Assert.IsTrue(orders.Count == 2, "count should be 2");

    }

    [TestMethod]
    public async Task DetachFromNavCollection() {
      var em1 = await TestFns.NewEm(_serviceName);

      var cust1 = new Customer();
      var order1 = new Order();
      var order2 = new Order();
      em1.AddEntity(cust1);
      var orders = cust1.Orders;
      orders.Add(order1);
      orders.Add(order2);

      var collectionChangedList = new List<NotifyCollectionChangedEventArgs>();
      orders.CollectionChanged += (s, e) => {
        collectionChangedList.Add(e);
      };
      var propChangedList = new List<PropertyChangedEventArgs>();
      ((INotifyPropertyChanged)order1).PropertyChanged += (s, e) => {
        propChangedList.Add(e);
      };
      var entityChangedList = new List<EntityChangedEventArgs>();
      em1.EntityChanged += (s, e) => {
        entityChangedList.Add(e);
      };
      order1.EntityAspect.Detach();
      
      Assert.IsTrue(collectionChangedList.Last().Action == NotifyCollectionChangedAction.Remove);
      Assert.IsTrue(collectionChangedList.Last().OldItems.Contains(order1), "change event should contain order1");

      Assert.IsTrue(propChangedList.Count == 0, "Detaching an entity will not create a propertyChange event");

      Assert.IsTrue(!orders.Contains(order1), "order1 should have been removed");
      Assert.IsTrue(order1.Customer == null, "Customer should be null");
      Assert.IsTrue(order1.CustomerID == cust1.CustomerID, "customerID should NOT be cleared when detached - just the Customer");
      Assert.IsTrue(order1.EntityAspect.EntityState.IsDetached());
      Assert.IsTrue(orders.Count == 1, "count should be 1");

      collectionChangedList.Clear();
      propChangedList.Clear();
      order1.Customer = cust1;
      Assert.IsTrue(collectionChangedList.Last().Action == NotifyCollectionChangedAction.Add);
      Assert.IsTrue(collectionChangedList.Last().NewItems.Contains(order1), "change event should contain order1");
      Assert.IsTrue(propChangedList.Any(args => args.PropertyName == "Customer"), "propChange should mention Customer");
      // Not needed because CustomerID is not cleared.
      // Assert.IsTrue(propChangedList.Any(args => args.PropertyName == "CustomerID"), "propChange should mention CustomerID");

      Assert.IsTrue(orders.Contains(order1), "order1 should be back");
      Assert.IsTrue(order1.Customer == cust1, "Customer should be back");
      Assert.IsTrue(order1.CustomerID == cust1.CustomerID, "CustomerID should not have changed"); // null because not required.
      Assert.IsTrue(order1.EntityAspect.EntityState.IsAdded());
      Assert.IsTrue(orders.Count == 2, "count should be 2");

    }

    [TestMethod]
    public async Task DeleteFromNavCollection()
    {
      var em1 = await TestFns.NewEm(_serviceName);

      var cust1 = new Customer();
      var order1 = new Order();
      var order2 = new Order();
      em1.AddEntity(cust1);
      var orders = cust1.Orders;
      orders.Add(order1);
      orders.Add(order2);
      em1.AcceptChanges();

      var collectionChangedList = new List<NotifyCollectionChangedEventArgs>();
      orders.CollectionChanged += (s, e) =>
      {
        collectionChangedList.Add(e);
      };
      var propChangedList = new List<PropertyChangedEventArgs>();
      ((INotifyPropertyChanged)order1).PropertyChanged += (s, e) =>
      {
        propChangedList.Add(e);
      };
      var entityChangedList = new List<EntityChangedEventArgs>();
      em1.EntityChanged += (s, e) =>
      {
        entityChangedList.Add(e);
      };
      order1.EntityAspect.Delete();

      Assert.IsTrue(collectionChangedList.Last().Action == NotifyCollectionChangedAction.Remove, "should have removed from collection");
      Assert.IsTrue(collectionChangedList.Last().OldItems.Contains(order1), "change event should contain order1");

      //Assert.IsTrue(propChangedList.Count == 0, "Deleting an entity will not create a propertyChange event");

      Assert.IsTrue(!orders.Contains(order1), "order1 should have been removed");
      Assert.IsTrue(order1.Customer == null, "Customer should be null");
      Assert.IsTrue(order1.CustomerID == cust1.CustomerID, "customerID should NOT be cleared when detached - just the Customer");
      Assert.IsTrue(order1.EntityAspect.EntityState.IsDeleted(), "state should be deleted");
      Assert.IsTrue(orders.Count == 1, "count should be 1");

      collectionChangedList.Clear();
      propChangedList.Clear();
      order1.EntityAspect.RejectChanges();
      Assert.IsTrue(collectionChangedList.Last().Action == NotifyCollectionChangedAction.Add, "should have added to collection");
      Assert.IsTrue(collectionChangedList.Last().NewItems.Contains(order1), "change event should contain order1");
      Assert.IsTrue(propChangedList.Any(args => args.PropertyName == "Customer"), "propChange should mention Customer");
      // Not needed because CustomerID is not cleared.
      // Assert.IsTrue(propChangedList.Any(args => args.PropertyName == "CustomerID"), "propChange should mention CustomerID");

      Assert.IsTrue(orders.Contains(order1), "order1 should be back");
      Assert.IsTrue(order1.Customer == cust1, "Customer should be back");
      Assert.IsTrue(order1.CustomerID == cust1.CustomerID, "CustomerID should not have changed"); // null because not required.
      Assert.IsTrue(order1.EntityAspect.EntityState.IsUnchanged(), "State should be unchanged but is " + order1.EntityAspect.EntityState);
      Assert.IsTrue(orders.Count == 2, "count should be 2");

    }

    [TestMethod]
    public async Task DeleteFromNavCollection2()
    {
      var em1 = await TestFns.NewEm(_serviceName);

      var emp1 = new Employee();
      var et1 = new EmployeeTerritory();
      var et2 = new EmployeeTerritory();
      var ter1 = new Territory() { TerritoryID = 11 };
      var ter2 = new Territory() { TerritoryID = 22 };

      em1.AddEntity(emp1);
      //em1.AddEntity(ter1);
      //em1.AddEntity(ter2);
      //em1.AddEntity(et1);
      //em1.AddEntity(et2);

      et1.Territory = ter1;
      et2.Territory = ter2;
      var empterrs = emp1.EmployeeTerritories;
      empterrs.Add(et1);
      empterrs.Add(et2);
      em1.AcceptChanges();
      Assert.IsTrue(et1.Employee == emp1, "Employee is attached to EmployeeTerritory");
      Assert.IsTrue(emp1.EmployeeTerritories.Count == 2, "Employee has EmployeeTerritories");
      Assert.IsTrue(et1.Territory == ter1, "Territory is attached to EmployeeTerritory");
      Assert.IsTrue(ter1.EmployeeTerritories.Count == 1, "Territory has EmployeeTerritories");

      var collectionChangedList = new List<NotifyCollectionChangedEventArgs>();
      empterrs.CollectionChanged += (s, e) =>
      {
        collectionChangedList.Add(e);
      };
      var propChangedList = new List<PropertyChangedEventArgs>();
      ((INotifyPropertyChanged)et1).PropertyChanged += (s, e) =>
      {
        propChangedList.Add(e);
      };
      var entityChangedList = new List<EntityChangedEventArgs>();
      em1.EntityChanged += (s, e) =>
      {
        entityChangedList.Add(e);
      };
      et1.EntityAspect.Delete();

      Assert.IsTrue(collectionChangedList.Last().Action == NotifyCollectionChangedAction.Remove, "should have removed from collection");
      Assert.IsTrue(collectionChangedList.Last().OldItems.Contains(et1), "change event should contain et1");

      //Assert.IsTrue(propChangedList.Count == 0, "Deleting an entity will not create a propertyChange event");

      Assert.IsTrue(!empterrs.Contains(et1), "et1 should have been removed");
      Assert.IsTrue(et1.Employee == null, "Employee should be null");
      Assert.IsTrue(et1.Territory == null, "Territory should be null");
      Assert.IsTrue(et1.EmployeeID == emp1.EmployeeID, "EmployeeID should NOT be cleared when detached - just the Employee");
      Assert.IsTrue(et1.TerritoryID == ter1.TerritoryID, "TerritoryID should NOT be cleared when detached - just the Territory");
      Assert.IsTrue(et1.EntityAspect.EntityState.IsDeleted(), "state should be deleted");
      Assert.IsTrue(empterrs.Count == 1, "count should be 1");

      collectionChangedList.Clear();
      propChangedList.Clear();
      et1.EntityAspect.RejectChanges();
      Assert.IsTrue(collectionChangedList.Last().Action == NotifyCollectionChangedAction.Add, "should have added to collection");
      Assert.IsTrue(collectionChangedList.Last().NewItems.Contains(et1), "change event should contain employeeTerritory");
      Assert.IsTrue(propChangedList.Any(args => args.PropertyName == "Employee"), "propChange should mention Employee");
      Assert.IsTrue(propChangedList.Any(args => args.PropertyName == "Territory"), "propChange should mention Territory");
      // Not needed because CustomerID is not cleared.
      // Assert.IsTrue(propChangedList.Any(args => args.PropertyName == "CustomerID"), "propChange should mention CustomerID");
      
      Assert.IsTrue(empterrs.Contains(et1), "et1 should be back");
      Assert.IsTrue(et1.Employee == emp1, "Employee should be back");
      Assert.IsTrue(et1.Territory == ter1, "Territory should be back");
      Assert.IsTrue(et1.EmployeeID == emp1.EmployeeID, "EmployeeID should not have changed"); // null because not required.
      Assert.IsTrue(et1.EntityAspect.EntityState.IsUnchanged(), "State should be unchanged but is " + et1.EntityAspect.EntityState);
      Assert.IsTrue(empterrs.Count == 2, "count should be 2");

    }

    [TestMethod]
    public async Task ChangeParent1ToN() {
      var em1 = await TestFns.NewEm(_serviceName);

      var cust1 = new Customer();
      var cust2 = new Customer();
      
      var order1 = new Order();
      var order2 = new Order();
      em1.AddEntity(cust1);
      em1.AddEntity(cust2);
      
      cust1.Orders.Add(order1);
      cust1.Orders.Add(order2);

      var cust1CollChangedList = new List<NotifyCollectionChangedEventArgs>();
      cust1.Orders.CollectionChanged += (s, e) => {
        cust1CollChangedList.Add(e);
      };
      var cust2CollChangedList = new List<NotifyCollectionChangedEventArgs>();
      cust2.Orders.CollectionChanged += (s, e) => {
        cust2CollChangedList.Add(e);
      };

      var propChangedList = new List<PropertyChangedEventArgs>();
      ((INotifyPropertyChanged)order1).PropertyChanged += (s, e) => {
        propChangedList.Add(e);
      };
      // move order
      cust2.Orders.Add(order1);

      Assert.IsTrue(cust1CollChangedList.Last().Action == NotifyCollectionChangedAction.Remove);
      Assert.IsTrue(cust1CollChangedList.Last().OldItems.Contains(order1), "change event should contain order1");

      Assert.IsTrue(cust2CollChangedList.Last().Action == NotifyCollectionChangedAction.Add);
      Assert.IsTrue(cust2CollChangedList.Last().NewItems.Contains(order1), "change event should contain order1");
      
      Assert.IsTrue(propChangedList.Any(args => args.PropertyName == "Customer"), "propChange should mention Customer");
      Assert.IsTrue(propChangedList.Any(args => args.PropertyName == "CustomerID"), "propChange should mention CustomerID");

      Assert.IsTrue(!cust1.Orders.Contains(order1), "order1 should have been removed");
      Assert.IsTrue(cust2.Orders.Contains(order1), "order1 should have been removed");
      Assert.IsTrue(order1.Customer == cust2, "Customer should be cust2");
      Assert.IsTrue(order1.CustomerID == cust2.CustomerID, "CustomerID should be cust2's id");
      Assert.IsTrue(order1.EntityAspect.EntityState.IsAdded());
      Assert.IsTrue(cust1.Orders.Count == cust2.Orders.Count, "count should be 1");

      cust1CollChangedList.Clear();
      cust2CollChangedList.Clear();
      propChangedList.Clear();
      order1.Customer = cust1;
      Assert.IsTrue(cust1CollChangedList.Last().Action == NotifyCollectionChangedAction.Add);
      Assert.IsTrue(cust1CollChangedList.Last().NewItems.Contains(order1), "change event should contain order1");
      Assert.IsTrue(propChangedList.Any(args => args.PropertyName == "Customer"), "propChange should mention Customer");
      Assert.IsTrue(propChangedList.Any(args => args.PropertyName == "CustomerID"), "propChange should mention CustomerID");
      
      Assert.IsTrue(cust1.Orders.Count == 2, "count should be 2");

    }


    // graph attach (1-n) - setProperties child, attach child
    [TestMethod]
    public async Task GraphAttachChild() {
      var em1 = await TestFns.NewEm(_serviceName);

      var cust1 = new Customer();
      // this test will fail if we don't give the customer a new Guid 
      cust1.CustomerID = Guid.NewGuid();
      var order1 = new Order();
      order1.Customer = cust1;
      
      em1.AddEntity(order1);

      Assert.IsTrue(order1.EntityAspect.EntityState.IsAdded());
      
      Assert.IsTrue(cust1.EntityAspect.EntityState.IsAdded());
      var orders = cust1.Orders;
      Assert.IsTrue(orders.Contains(order1) , "should contain order1");
      Assert.IsTrue(order1.Customer == cust1, "Customer should be cust2");
      Assert.IsTrue(order1.CustomerID == cust1.CustomerID, "CustomerID should be cust2's id");
      

    }

    // graph attach (1-n) - setProperties child, attach child
    [TestMethod]
    public async Task GraphAttachParent() {
      var em1 = await TestFns.NewEm(_serviceName);

      var cust1 = new Customer();
      var order1 = new Order();
      cust1.Orders.Add(order1);

      em1.AddEntity(cust1);

      Assert.IsTrue(order1.EntityAspect.EntityState.IsAdded());
      Assert.IsTrue(cust1.EntityAspect.EntityState.IsAdded());
      var orders = cust1.Orders;
      Assert.IsTrue(orders.Contains(order1), "should contain both orders");
      Assert.IsTrue(order1.Customer == cust1, "Customer should be cust2");
      Assert.IsTrue(order1.CustomerID == cust1.CustomerID, "CustomerID should be cust2's id");


    }

    
    [TestMethod]
    public async Task GraphAttachMultipartKey() {
      var em1 = await TestFns.NewEm(_serviceName);

      if (TestFns.DEBUG_MONGO) {
        Assert.Inconclusive("NA for Mongo - OrderDetail");
        return;
      }

      var order = new Order();
      order.OrderID = 999;
      for (int i = 0; i < 3; i++) {
        var od = new OrderDetail();
        od.ProductID = i;
        order.OrderDetails.Add(od);
      }
      em1.AttachEntity(order);
      Assert.IsTrue(order.EntityAspect.EntityState.IsUnchanged(), "order should be unchanged");
      Assert.IsTrue(order.OrderDetails.All(od => od.EntityAspect.EntityState.IsUnchanged()), "ods should all be unchanged");
      Assert.IsTrue(order.OrderDetails.Count == 3, "should be 3 ods");
      Assert.IsTrue(order.OrderDetails.All(od => od.Order == order), "should all point to order");
      Assert.IsTrue(order.OrderDetails.All(od => od.OrderID == 999), "should all have correct orderId");
      
    }

    [TestMethod]
    public async Task UnattachedChildrenMultipartkey() {
      var em1 = await TestFns.NewEm(_serviceName);

      if (TestFns.DEBUG_MONGO) {
        Assert.Inconclusive("NA for Mongo - OrderDetail");
        return;
      }

      var order = new Order();
      order.OrderID = 999;
      for (int i = 0; i < 3; i++) {
        var od = new OrderDetail();
        od.ProductID = i;
        od.OrderID = order.OrderID;
        em1.AttachEntity(od);
      }
      em1.AttachEntity(order);
      Assert.IsTrue(order.EntityAspect.EntityState.IsUnchanged(), "order should be unchanged");
      Assert.IsTrue(order.OrderDetails.All(od => od.EntityAspect.EntityState.IsUnchanged()), "ods should all be unchanged");
      Assert.IsTrue(order.OrderDetails.Count == 3, "should be 3 ods");
      Assert.IsTrue(order.OrderDetails.All(od => od.Order == order), "should all point to order");
      Assert.IsTrue(order.OrderDetails.All(od => od.OrderID == order.OrderID), "should all have correct orderId");

    }
    
    [TestMethod]
    public async Task DuplicateKeysError() {
      var em1 = await TestFns.NewEm(_serviceName);

      var cust1 = new Customer();
      var cust2 = new Customer();
      em1.AttachEntity(cust1);
      try {
        cust2.CustomerID = cust1.CustomerID;
        em1.AttachEntity(cust2);
        Assert.Fail("should not get here");
      } catch (Exception e) {
        Assert.IsTrue(e.Message.Contains("key"), "message should mention 'key'");
      }
    }
    
    // fk fixup - fk to nav - attached"
    [TestMethod]
    public async Task FkFixup() {
      var em1 = await TestFns.NewEm(_serviceName);

      var cust1 = em1.CreateEntity<Customer>(EntityState.Unchanged);
      var order1 = em1.CreateEntity<Order>(EntityState.Unchanged);
      order1.CustomerID = cust1.CustomerID;
      Assert.IsTrue(cust1.Orders.Contains(order1), "should contain order1");
      Assert.IsTrue(order1.Customer == cust1, "customer should be attached");
    }

    // fk fixup - unattached children
    [TestMethod]
    public async Task UnattachedChildren() {
      var em1 = await TestFns.NewEm(_serviceName);

      var cust1 = new Customer();
      var cust2 = new Customer();
      var order1 = new Order();
      cust1.CustomerID = Guid.NewGuid();
      em1.AttachEntity(order1);
      Assert.IsTrue(order1.EntityAspect.EntityState.IsUnchanged());
      order1.CustomerID = cust1.CustomerID;
      Assert.IsTrue(order1.EntityAspect.EntityState.IsModified());
      Assert.IsTrue(order1.Customer == null, "customer should be null");
      order1.EntityAspect.AcceptChanges();
      Assert.IsTrue(order1.EntityAspect.EntityState.IsUnchanged());
      em1.AttachEntity(cust1);
      Assert.IsTrue(order1.Customer == cust1, "customer should now be set");
      Assert.IsTrue(order1.EntityAspect.EntityState.IsUnchanged(), "fixup should not change entityState");
    }

    

    // recursive navigation fixup
    [TestMethod]
    public async Task AttachRecursive() {
      var em1 = await TestFns.NewEm(_serviceName);
      using (TestFns.ShareWithDetached(em1.MetadataStore)) {
        var emp1 = new Employee();
        var emp2 = new Employee();
        var emp3 = new Employee();

        emp2.Manager = emp1;
        emp3.Manager = emp2;
        em1.AddEntity(emp3);
        Assert.IsTrue(emp3.EntityAspect.IsAttached);
        Assert.IsTrue(emp2.EntityAspect.IsAttached);
        Assert.IsTrue(emp1.EntityAspect.IsAttached);
        Assert.IsTrue(emp1.DirectReports.Contains(emp2), "emp1 manages emp2");
        Assert.IsTrue(emp2.DirectReports.Contains(emp3), "emp2 manages emp3");
        Assert.IsTrue(emp2.Manager == emp1, "emp2 manager is emp1");
        Assert.IsTrue(emp3.Manager == emp2, "emp3 manager is emp2");
      }
    }
  }
}
