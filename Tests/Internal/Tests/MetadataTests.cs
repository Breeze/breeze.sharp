using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
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
  public class MetadataTests {

    

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
    public async Task SelfAndSubtypes() {
      var em = await TestFns.NewEm(_serviceName);
      var allOrders = em.GetEntities(typeof(Order), typeof(InternationalOrder));
      var orderEntityType = MetadataStore.Instance.GetEntityType(typeof (Order));
      var allOrders2 = em.GetEntities(orderEntityType.SelfAndSubEntityTypes.Select(et => et.ClrType));
      Assert.IsTrue(allOrders.Count() == allOrders2.Count());
      
    }

    
  }


}
