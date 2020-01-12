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


// Edmunds link is no longer any good - api has changed.

//namespace Breeze.Sharp.Tests {

//  [TestClass]
//  public class EdmundsTests {

//    // TODO: need Exp/Imp tests with Complex type changes.

//    private DataService _dataService;

//    [TestInitialize]
//    public void TestInitializeMethod() {
      
//      Configuration.Instance.ProbeAssemblies(typeof(Make).Assembly);
      
//      var serviceName = "http://api.edmunds.com/v1/api/"; // edmunds
//      _dataService = new DataService(serviceName) {HasServerMetadata = false};
      
//    }

//    [TestCleanup]
//    public void TearDown() {
      
      
//    }




//    [TestMethod]
//    public async Task SimpleCall() {

//      //return;
//      var em1 = new EntityManager(_dataService);
//      em1.MetadataStore.NamingConvention = new EdmundsNamingConvention();
//      Model.Edmunds.Config.Initialize(em1.MetadataStore);
//      var initParameters = InitialParameters();
//      var q = new EntityQuery<Make>().From("vehicle/makerepository/findall")
//        .WithParameters(initParameters).With(new EdmundsJsonResultsAdapter());
//      var r = await em1.ExecuteQuery(q);
//      Assert.IsTrue(r.Any());

//    }

//    private Dictionary<String, Object> InitialParameters() {
//      var map = new Dictionary<String, Object>();
//      map["fmt"]  = "json";
//      map["api_key"] = "z35zpey2s8sbj4d3g3fxsqdx";
//      return map;
//    }

    
//  }


    
//}
