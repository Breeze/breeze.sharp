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
using Newtonsoft.Json.Linq;
using System.Dynamic;
using System.Net.Http;

namespace Breeze.Sharp.Tests {

  [TestClass]
  public class NamedQueryTests {

    private String _serviceName;

    [TestInitialize]
    public void TestInitializeMethod() {
      MetadataStore.Instance.ProbeAssemblies(typeof(Customer).Assembly);
      _serviceName = "http://localhost:7150/breeze/NorthwindIBModel/";
    }

    [TestCleanup]
    public void TearDown() {
      
    }

    
    [TestMethod]
    public async Task CustomersStartingWith() {
      var em1 = await TestFns.NewEm(_serviceName);

      var q = new EntityQuery<Customer>("CustomersStartingWith").WithParameter("companyName", "A");
      var rp = q.GetResourcePath();
      var customers = await q.Execute(em1);
      Assert.IsTrue(customers.Count() > 0, "should be some results");
      Assert.IsTrue(customers.All(c => c.CompanyName.StartsWith("A")));

    }

    [TestMethod]
    public async Task LookupsSimple() {
      var entityManager = await TestFns.NewEm(_serviceName);
      var query = EntityQuery.From("Lookup1Array", new {
        regions = new List<Region>()
      });

      var r0 = await query.Execute(entityManager);
      Assert.IsTrue(r0.Any());
    }

    [TestMethod]
    public async Task LookupsScalar() {
      
      var entityManager = await TestFns.NewEm(_serviceName);

      var query = EntityQuery.From("Lookups", new {
        Regions = Enumerable.Empty<Region>(),
        Territories = Enumerable.Empty<Territory>(),
        Categories = Enumerable.Empty<Category>(),
      });
      //var query = EntityQuery.From("Lookups", new {
      //  regions = new List<Region>(),
      //  territories = new List<Territory>(),
      //  categories = new List<Category>()
      //});

      var data = await query.Execute(entityManager);
      Assert.IsTrue(data.Count() == 1, "Lookups query should return single item");
      var lookups = data.First();

    }



    [TestMethod]
    public async Task LookupsEnumerableAnon() {
      // Not yet reviewed - JJT

      var entityManager = await TestFns.NewEm(_serviceName);

      //var query = EntityQuery.From("Lookups", new
      //{
      //    Regions = Enumerable.Empty<Region>(),
      //    Territories = Enumerable.Empty<Territory>(),
      //    Categories = Enumerable.Empty<Category>(),
      //});
      var query = EntityQuery.From("LookupsEnumerableAnon", new {
        regions = new List<Region>(),
        territories = new List<Territory>(),
        categories = new List<Category>()
      });

      var data = await query.Execute(entityManager);
      Assert.IsTrue(data.Count() == 1, "Lookups query should return single item");
      var lookups = data.First();

    }


    [TestMethod]
    public async Task CompanyNames() {
      var em1 = await TestFns.NewEm(_serviceName);

      var q = new EntityQuery<String>("CompanyNames");
      var rp = q.GetResourcePath();
      var companyNames = await q.Execute(em1);
      Assert.IsTrue(companyNames.Count() > 0, "should be some results");

      var q2 = new EntityQuery<Object>("CompanyNames");
      rp = q2.GetResourcePath();
      var companyNames2 = await q2.Execute(em1);
      Assert.IsTrue(companyNames2.Count() > 0, "should be some results");
      Assert.IsTrue(companyNames.SequenceEqual(companyNames2.Cast<String>()));
    }

    [TestMethod]
    public async Task CompanyNamesAndIds() {
      var em1 = await TestFns.NewEm(_serviceName);
      var q = new EntityQuery<Object>("CompanyNamesAndIds");
      var rp = q.GetResourcePath();
      var companyNamesAndIds = await q.Execute(em1);
      Assert.IsTrue(companyNamesAndIds.Count() > 0, "should be some results");
      // each item is a JObject
      var companyNamesAndIdObjects = companyNamesAndIds.Cast<JObject>();
      var item = companyNamesAndIdObjects.First();
      var companyName = item["CompanyName"].ToObject<String>();
      var id = item["CustomerID"].ToObject<Guid>();
    }

    [TestMethod]
    public async Task CompanyNamesAndIds2() {
      var em1 = await TestFns.NewEm(_serviceName);
      var q = EntityQuery.From("CompanyNamesAndIds", new { CompanyName = "fsdfsfd", CustomerID = new Guid() });
      var rp = q.GetResourcePath();
      var companyNamesAndIds = await q.Execute(em1);
      Assert.IsTrue(companyNamesAndIds.Count() > 0, "should be some results");
      Assert.IsTrue(companyNamesAndIds.All(x => {
        return x.CompanyName.Length > 0 && x.CustomerID != null;
      }));
      
    }

    [TestMethod]
    public async Task CustomersWithBigOrders() {
      var em1 = await TestFns.NewEm(_serviceName);

      var q = EntityQuery.From("CustomersWithBigOrders", new { Customer = (Customer)null, BigOrders = new List<Order>() });
      var rp = q.GetResourcePath();
      var results = await q.Execute(em1);
      Assert.IsTrue(results.Count() > 0, "should be some results");
      
      Assert.IsTrue(em1.GetEntities<Customer>().Count() > 0, "should have some customers");
      Assert.IsTrue(em1.GetEntities<Order>().Count() > 0, "should have some orders");
      Assert.IsTrue(results.All(r => 
        r.Customer.GetType() == typeof(Customer) && r.BigOrders.GetType() == typeof(List<Order>)
      ));
    }

    [TestMethod]
    public async Task CustomersWithBigOrders2() {
      var em1 = await TestFns.NewEm(_serviceName);
      
      var q = new EntityQuery<CustomerAndBigOrders>("CustomersWithBigOrders");
      var rp = q.GetResourcePath();
      var results = await q.Execute(em1);
      Assert.IsTrue(results.Count() > 0, "should be some results");

      Assert.IsTrue(em1.GetEntities<Customer>().Count() > 0, "should have some customers");
      Assert.IsTrue(em1.GetEntities<Order>().Count() > 0, "should have some orders");
      Assert.IsTrue(results.All(r =>
        r.Customer.GetType() == typeof(Customer) && r.BigOrders.GetType() == typeof(List<Order>)
      ));

    }

    public class CustomerAndBigOrders {
      public Customer Customer { get; set; }
      public IEnumerable<Order> BigOrders { get; set; }
    }

    [TestMethod]
    public async Task CustomersAndOrders() {
      var em1 = await TestFns.NewEm(_serviceName);

      var q = new EntityQuery<Customer>("CustomersAndOrders").Where(c => c.CompanyName.StartsWith("A"));
      var rp = q.GetResourcePath();
      var results = await q.Execute(em1);
      Assert.IsTrue(results.Count() > 0, "should be some results");

      Assert.IsTrue(em1.GetEntities<Customer>().Count() > 0, "should have some customers");
      Assert.IsTrue(em1.GetEntities<Order>().Count() > 0, "should have some orders");
      Assert.IsTrue(results.All(c => c.CompanyName.StartsWith("A")));

    }

    [TestMethod]
    public async Task SearchEmployees() {
      var em1 = await TestFns.NewEm(_serviceName);


      var q = EntityQuery.From<Employee>("SearchEmployees")
        .WithParameter("employeeIds", new int[] {1, 4});
        // .WithParameter("employeeIds", 1)
        // .WithParameter("employeeIds", 4);
      var rp = q.GetResourcePath();
      var results = await q.Execute(em1);
      Assert.IsTrue(results.Any());
      Assert.IsTrue(results.All(r => r.EmployeeID == 1 || r.EmployeeID == 4));

    }

    [TestMethod]
    public async Task EmployeesFilteredByCountryAndBirthDate() {
      var em1 = await TestFns.NewEm(_serviceName);

      var q = new EntityQuery<Employee>().From("EmployeesFilteredByCountryAndBirthdate")
        .WithParameter("birthDate", "1/1/1960")
        .WithParameter("country", "USA");
      
      var rp = q.GetResourcePath();
      var results = await q.Execute(em1);
      Assert.IsTrue(results.Any());
      

    }

      


    [TestMethod]
    public async Task SearchCustomers() {
      var em1 = await TestFns.NewEm(_serviceName);

      //var query = EntityQuery.from("SearchCustomers")
      //      .withParameters( { CompanyName: "A", ContactNames: ["B", "C"] , City: "Los Angeles"  } );
      var q = new EntityQuery<Customer>("SearchCustomers")
        .WithParameter("CompanyName", "A")
        .WithParameter("ContactNames", new String[] { "B", "C" })
        .WithParameter("City", "LosAngeles");
      var rp = q.GetResourcePath();
      var results = await q.Execute(em1);
      Assert.IsTrue(results.Count() == 3, "should be 3 results");

      Assert.IsTrue(em1.GetEntities<Customer>().Count() > 0, "should have some customers");
      
      

    }

    [TestMethod]
    public async Task SearchCustomersWithParameters() {
      var em1 = await TestFns.NewEm(_serviceName);

      //var query = EntityQuery.from("SearchCustomers")
      //      .withParameters( { CompanyName: "A", ContactNames: ["B", "C"] , City: "Los Angeles"  } );
      var q = new EntityQuery<Customer>("SearchCustomers")
        .WithParameters(new Dictionary<String, Object> {
          { "CompanyName", "A" }, 
          { "ContactNames", new String[] { "B", "C" }},
          { "City", "LosAngeles" }
      });
      var rp = q.GetResourcePath();
      var results = await q.Execute(em1);
      Assert.IsTrue(results.Count() == 3, "should be 3 results");

      Assert.IsTrue(em1.GetEntities<Customer>().Count() > 0, "should have some customers");

    }

    [TestMethod]
    public async Task CustomersWithHttpError() {
      var em1 = await TestFns.NewEm(_serviceName);

      var q = new EntityQuery<Customer>("CustomersWithHttpError");
        
      var rp = q.GetResourcePath();
      try {
        var results = await q.Execute(em1);
        Assert.Fail("shouldn't get here");
      } catch (HttpRequestException e) {
        Assert.IsTrue(e.Message.Contains("Custom error message"));
        

      }
      
    }
    
    
  }
}
