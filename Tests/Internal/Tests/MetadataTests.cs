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
  public class MetadataTests {

    private String _serviceName;

    [TestInitialize]
    public void TestInitializeMethod() {
      MetadataStore.Instance.ProbeAssemblies(typeof(Customer).Assembly);
      
    }

    [TestCleanup]
    public void TearDown() {
      
    }

    [TestMethod]
    public async Task MetadataMissingClrType() {
      try {
        var serviceName = "http://sampleservice.breezejs.com/api/todos/";

        var em = new EntityManager(serviceName);
        List<MetadataMismatchEventArgs> mmargs = new List<MetadataMismatchEventArgs>();
        MetadataStore.Instance.MetadataMismatch += (s, e) => {
          mmargs.Add(e);
          Assert.IsTrue(e.EntityTypeName.ToUpper().Contains("TODO"), "entityTypeName should be TODO");
          Assert.IsTrue(e.PropertyName == null, "propertyName should be null");
          Assert.IsTrue(e.Allow == false, "allow should be false");
          e.Allow = (e.MetadataMismatchType == MetadataMismatchType.MissingCLREntityType);
        };
        var x = await em.FetchMetadata();
        Assert.IsTrue(mmargs.Count == 1);
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
