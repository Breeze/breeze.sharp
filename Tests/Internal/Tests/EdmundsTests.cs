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
  public class EdmunsTests {

    // TODO: need Exp/Imp tests with Complex type changes.

    private String _serviceName;

    [TestInitialize]
    public void TestInitializeMethod() {
      MetadataStore.Instance.ProbeAssemblies(typeof(Customer).Assembly);
      // _serviceName = "http://localhost:7150/breeze/NorthwindIBModel/";
      _serviceName = "http://api.edmunds.com/v1/api/"; // edmunds
    }

    [TestCleanup]
    public void TearDown() {

    }


    [TestMethod]
    
    public async Task SimpleCall() {
      var em1 = await TestFns.NewEm(_serviceName);
      var initParameters = InitialParameters();
      var q = new EntityQuery<>().From("vehicle/makerepository/findall")
          .WithParameters(initParameters);
            
    
    }

    private Dictionary<String, Object> InitialParameters() {
      var map = new Dictionary<String, Object>();
      map["fmt"]  = "json";
      map["api_key"] = "z35zpey2s8sbj4d3g3fxsqdx";
      return map;
    }

    
  }

  //function initialize(metadataStore) {
  //      metadataStore.addEntityType({
  //          shortName: "Make",
  //          namespace: "Edmunds",
  //          dataProperties: {
  //              id:         { dataType: DT.Int64, isPartOfKey: true },
  //              name:       { dataType: DT.String },
  //              niceName:   { dataType: DT.String },
  //              modelLinks: { dataType: DT.Undefined }
  //          },
  //          navigationProperties: {
  //              models: {
  //                  entityTypeName:  "Model:#Edmunds", isScalar: false,
  //                  associationName: "Make_Models"
  //              }
  //          }
  //      });

  //      metadataStore.addEntityType({
  //          shortName: "Model",
  //          namespace: "Edmunds",
  //          dataProperties: {
  //              id:            { dataType: "String", isPartOfKey: true },
  //              makeId:        { dataType: "Int64" },
  //              makeName:      { dataType: "String" },
  //              makeNiceName:  { dataType: "String" },
  //              name:          { dataType: "String" },
  //              niceName:      { dataType: "String" },
  //              vehicleStyles: { dataType: "String" },
  //              vehicleSizes:  { dataType: "String" },
  //              categories:    { dataType: "Undefined" }
  //          },
  //          navigationProperties: {
  //              make: {
  //                  entityTypeName:  "Make:#Edmunds", isScalar: true,
  //                  associationName: "Make_Models",  foreignKeyNames: ["makeId"]
  //              }
  //          }
  //      });
    
}
