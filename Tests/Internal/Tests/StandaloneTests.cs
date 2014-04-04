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
using Model.Edmunds;

namespace Breeze.Sharp.Tests {

  [TestClass]
  public class StandaloneTests {

    

    [TestInitialize]
    public void TestInitializeMethod() {
      
      
    }

    [TestCleanup]
    public void TearDown() {

    }


    [TestMethod]
    public async Task NoClrTypes() {
      
      var serviceName = "http://localhost:7150/breeze/NorthwindIBModel";
      var ds = new DataService(serviceName);
      try {
        await MetadataStore.Instance.FetchMetadata(ds);
      } catch (Exception e) {
        Assert.IsTrue(e.Message.Contains("MetadataStore.Instance"));
      }

      try {
        var em = new EntityManager(ds);
        await em.FetchMetadata();
      }
      catch (Exception e) {
        Assert.IsTrue(e.Message.Contains("MetadataStore.Instance"));
      }
    }

    

    
  }


    
}
