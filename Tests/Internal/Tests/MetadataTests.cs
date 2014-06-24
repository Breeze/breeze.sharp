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
using System.Net.Http;

namespace Breeze.Sharp.Tests {

  [TestClass]
  public class MetadataTests {

    

    private String _serviceName;

    [TestInitialize]
    public void TestInitializeMethod() {
      Configuration.Instance.ProbeAssemblies(typeof(Customer).Assembly);
      _serviceName = "http://localhost:7150/breeze/NorthwindIBModel/";
      
    }

    [TestCleanup]
    public void TearDown() {

    }

    

    [TestMethod]
    public async Task SelfAndSubtypes() {
      var em = await TestFns.NewEm(_serviceName);
      var allOrders = em.GetEntities(typeof(Order), typeof(InternationalOrder));
      var orderEntityType = em.MetadataStore.GetEntityType(typeof (Order));
      var allOrders2 = em.GetEntities(orderEntityType.SelfAndSubEntityTypes.Select(et => et.ClrType));
      Assert.IsTrue(allOrders.Count() == allOrders2.Count());
      
    }


    [TestMethod]
    public async Task HttpClientHandler() {
      var  httpClient = new HttpClient(new HttpClientHandler() {
        UseDefaultCredentials = true
      });
      var ds = new DataService(_serviceName, httpClient);
      var em = new EntityManager(ds);
      em.MetadataStore.AllowedMetadataMismatchTypes = MetadataMismatchType.MissingCLREntityType;
      var md = await em.FetchMetadata(ds);
      Assert.IsTrue(md != null);

    }

    // Creates a convention that removes underscores from server property names
    // Remembers them in a private dictionary so it can restore them
    // when going from client to server
    // Warning: use only with metadata loaded directly from server
    public class UnderscoreRemovallNamingConvention : NamingConvention {

      private Dictionary<String, String> _clientServerPropNameMap = new Dictionary<string, string>();

      public override String ServerPropertyNameToClient(String serverPropertyName, StructuralType parentType) {
        if (serverPropertyName.IndexOf("_", StringComparison.InvariantCulture) != -1) {
          var clientPropertyName = serverPropertyName.Replace("_", "");
          _clientServerPropNameMap[clientPropertyName] = serverPropertyName;
          return clientPropertyName;
        } else {
          return base.ServerPropertyNameToClient(serverPropertyName, parentType);
        }
      }

      public override string ClientPropertyNameToServer(string clientPropertyName, StructuralType parentType) {
        String serverPropertyName;
        if (_clientServerPropNameMap.TryGetValue(clientPropertyName, out serverPropertyName)) {
          serverPropertyName = clientPropertyName;
        }
        return serverPropertyName;

      }
    }
  }

   
  


}
