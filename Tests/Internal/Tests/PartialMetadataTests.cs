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

  public class MorphedClassNamingConvention : Breeze.Sharp.NamingConvention {

    public MorphedClassNamingConvention() {
      this.AddClientServerNamespaceMapping("PartialFoo", "Foo" );
    }
    
  }

  [TestClass]
  public class PartialMetadataTests {

    private String _serviceName;

    [TestInitialize]
    public void TestInitializeMethod() {
      MetadataStore.__Reset();
      MetadataStore.Instance.NamingConvention = new MorphedClassNamingConvention();
      MetadataStore.Instance.ProbeAssemblies(typeof(PartialFoo.Customer).Assembly);
      
    }

    [TestCleanup]
    public void TearDown() {
      
    }

    [TestMethod]
    public async Task MetadataMissingClrProperty() {
      try {
        MetadataStore.Instance.AllowedMetadataMismatchTypes = MetadataMismatchType.AllAllowable;
        var serviceName = "http://localhost:7150/breeze/NorthwindIBModel/";

        var em = new EntityManager(serviceName);
        var mmargs = new List<MetadataMismatchEventArgs>();
        MetadataStore.Instance.MetadataMismatch += (s, e) => {
          mmargs.Add(e);
        };
        var x = await em.FetchMetadata();
        Assert.IsTrue(mmargs.Count > 4, "should be more than 4 mismatches, but found: " + mmargs.Count);
        var errors = MetadataStore.Instance.GetMessages(MessageType.Error);
        Assert.IsTrue(errors.Count() == 0, "should be 0 errors: " + errors.ToAggregateString("..."));
        Assert.IsTrue(MetadataStore.Instance.GetMessages().Count() >= 4, "should be more than 4 message");

      } finally {
        MetadataStore.__Reset();
      }
    }

    [TestMethod]
    public async Task MetadataMissingClrPropertyQuery() {
      try {
        MetadataStore.Instance.AllowedMetadataMismatchTypes = MetadataMismatchType.AllAllowable;
        var serviceName = "http://localhost:7150/breeze/NorthwindIBModel/";

        var em = new EntityManager(serviceName);
        var q = new EntityQuery<PartialFoo.Customer>().Where(c => c.CompanyName.StartsWith("B"));
        var r0 = await em.ExecuteQuery(q);
        Assert.IsTrue(r0.Count() > 0);

      } finally {
        MetadataStore.__Reset();
      }
    }

    [TestMethod]
    public async Task MetadataMissingClrType() {
      try {
        //MetadataStore.Instance.AllowedMetadataMismatchTypes = MetadataMismatchType.AllAllowable;
        var serviceName = "http://sampleservice.breezejs.com/api/todos/";

        var em = new EntityManager(serviceName);
        var mmargs = new List<MetadataMismatchEventArgs>();
        MetadataStore.Instance.MetadataMismatch += (s, e) => {
          mmargs.Add(e);
          Assert.IsTrue(e.StructuralTypeName.ToUpper().Contains("TODO"), "entityTypeName should be TODO");
          Assert.IsTrue(e.PropertyName == null, "propertyName should be null");
          Assert.IsTrue(e.Allow == false, "allow should be false");
          e.Allow = (e.MetadataMismatchType == MetadataMismatchType.MissingCLREntityType);
        };
        var x = await em.FetchMetadata();
        Assert.IsTrue(mmargs.Count == 1, "should be only one mismatch, but found: " + mmargs.Count);
        var errors = MetadataStore.Instance.GetMessages(MessageType.Error);
        Assert.IsTrue(errors.Count() == 0, "should be 0 errors: " + errors.ToAggregateString("..."));
        Assert.IsTrue(MetadataStore.Instance.GetMessages().Count() == 1, "should be 1 message");
      }
      finally {
        MetadataStore.__Reset();
      }
    }
      
    
    [TestMethod]
    public async Task MetadataWithEmbeddedQuotes() {
      try {
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
      finally {
        MetadataStore.__Reset();
      }
    }




  }
}
