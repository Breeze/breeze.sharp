using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Breeze.Sharp;
using Breeze.Sharp.Core;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PartialFoo;
using System.Collections.Specialized;
using System.ComponentModel;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json;

namespace Breeze.Sharp.Tests {

  

  [TestClass]
  public class PartialMetadataTests {

    private String _serviceName = "http://localhost:7150/breeze/NorthwindIBModel/";

    [TestInitialize]
    public void TestInitializeMethod() {
      Configuration.Instance.ProbeAssemblies(typeof(PartialFoo.Customer).Assembly);
    }

    [TestCleanup]
    public void TearDown() {
      
    }

    [TestMethod]
    public async Task NamingConventionSerialization() {
      
      var nc = new MorphedClassNamingConvention();
      var nc2 = new MorphedClassNamingConvention();
      Assert.IsTrue(nc.Equals(nc2));
      var nc3 = nc.WithClientServerNamespaceMapping("PartialBar", "Bar");
      var jn = ((IJsonSerializable) nc3).ToJNode(null);
      var nc4 = jn.ToObject(nc3.GetType(), true);
      Assert.IsTrue(nc3.Equals(nc4));
    }

    public class MorphedClassNamingConvention : Breeze.Sharp.NamingConvention {
      public MorphedClassNamingConvention() {
        this.AddClientServerNamespaceMapping("PartialFoo", "Foo");
      }
    }

    [TestMethod]
    public async Task MetadataMissingClrProperty() {
      
      var ms = new MetadataStore();
      ms.NamingConvention = new MorphedClassNamingConvention();
      var em = new EntityManager(_serviceName, ms);

      em.MetadataStore.AllowedMetadataMismatchTypes = MetadataMismatchType.AllAllowable;
        
      var mmargs = new List<MetadataMismatchEventArgs>();
      em.MetadataStore.MetadataMismatch += (s, e) => {
        mmargs.Add(e);
      };
      var x = await em.FetchMetadata();
      Assert.IsTrue(mmargs.Count > 4, "should be more than 4 mismatches, but found: " + mmargs.Count);
      var errors = em.MetadataStore.GetMessages(MessageType.Error);
      Assert.IsTrue(errors.Count() == 0, "should be 0 errors: " + errors.ToAggregateString("..."));
      Assert.IsTrue(em.MetadataStore.GetMessages().Count() >= 4, "should be more than 4 message");
    }

    [TestMethod]
    public async Task MetadataMissingClrPropertyQuery() {

      var em = new EntityManager(_serviceName);
      em.MetadataStore.NamingConvention = new MorphedClassNamingConvention();
      em.MetadataStore.AllowedMetadataMismatchTypes = MetadataMismatchType.AllAllowable;
      var q = new EntityQuery<PartialFoo.Customer>().Where(c => c.CompanyName.StartsWith("B"));
      var r0 = await em.ExecuteQuery(q);
      Assert.IsTrue(r0.Count() > 0);

    }

    [TestMethod]
    public async Task MetadataMissingClrType() {
      
      //Configuration.Instance.AllowedMetadataMismatchTypes = MetadataMismatchType.AllAllowable;
      var serviceName = "http://sampleservice.breezejs.com/api/todos/";

      var em = new EntityManager(serviceName);
      var mmargs = new List<MetadataMismatchEventArgs>();
      em.MetadataStore.MetadataMismatch += (s, e) => {
        mmargs.Add(e);
        Assert.IsTrue(e.StructuralTypeName.ToUpper().Contains("TODO"), "entityTypeName should be TODO");
        Assert.IsTrue(e.PropertyName == null, "propertyName should be null");
        Assert.IsTrue(e.Allow == false, "allow should be false");
        e.Allow = (e.MetadataMismatchType == MetadataMismatchType.MissingCLREntityType);
      };
      var x = await em.FetchMetadata();
      Assert.IsTrue(mmargs.Count == 1, "should be only one mismatch, but found: " + mmargs.Count);
      var errors = em.MetadataStore.GetMessages(MessageType.Error);
      Assert.IsTrue(errors.Count() == 0, "should be 0 errors: " + errors.ToAggregateString("..."));
      Assert.IsTrue(em.MetadataStore.GetMessages().Count() == 1, "should be 1 message");
      
      
    }
      
    
    [TestMethod]
    public async Task MetadataWithEmbeddedQuotes() {
      
      // this is a legacy service that has quoted metadata.
      var serviceName = "http://sampleservice.breezejs.com/api/todos/";
      var ds = new DataService(serviceName);
          
      var metadata = await ds.GetAsync("Metadata");

      var metadata2 = System.Text.RegularExpressions.Regex.Unescape(metadata).Trim('"');
      var jo = (JObject) JsonConvert.DeserializeObject(metadata2);
      var em = new EntityManager(ds);
      try {
        var x = await em.FetchMetadata();
        Assert.Fail("should not get here - CLR types for this metadata are not available");
      }
      catch (Exception e) {
        Assert.IsTrue(e.Message.Contains("CLR Entity"));
      }
      
    }




  }
}
