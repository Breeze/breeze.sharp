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
  public class MetadataByHandTests {

    // TODO: need Exp/Imp tests with Complex type changes.

    //private String _serviceName;

    [TestInitialize]
    public void TestInitializeMethod() {
      Configuration.Instance.ProbeAssemblies(typeof(Customer).Assembly);
      // _serviceName = "http://localhost:7150/breeze/NorthwindIBModel/";
      // _serviceName = "http://api.edmunds.com/v1/api/"; // edmunds
    }

    [TestCleanup]
    public void TearDown() {

    }


    // [TestMethod]
    
    public void SimpleCall() {
      var metadataStore = new MetadataStore();
      var orderBuilder = new EntityTypeBuilder<Order>(metadataStore);
      var dp = orderBuilder.DataProperty(o => o.ShipAddress).MaxLength(40);
      orderBuilder.DataProperty(o => o.OrderID).IsPartOfKey();
      orderBuilder.NavigationProperty(o => o.Customer).HasInverse(c => c.Orders).HasForeignKey(o => o.CustomerID);
      orderBuilder.NavigationProperty(o => o.Employee).HasInverse(emp => emp.Orders).HasForeignKey(o => o.EmployeeID);
      orderBuilder.NavigationProperty(o => o.OrderDetails).HasInverse(od => od.Order);

      var odBuilder = new EntityTypeBuilder<OrderDetail>(metadataStore);
      odBuilder.DataProperty(od => od.OrderID).IsPartOfKey();
      odBuilder.DataProperty(od => od.ProductID).IsPartOfKey();
      odBuilder.NavigationProperty(od => od.Order).HasInverse(o => o.OrderDetails).HasForeignKey(od => od.OrderID);
      odBuilder.NavigationProperty(od => od.Product).HasForeignKey(o => o.ProductID);

      var empBuilder = new EntityTypeBuilder<Employee>(metadataStore);
      empBuilder.DataProperty(emp => emp.EmployeeID).IsPartOfKey();
      empBuilder.NavigationProperty(emp => emp.Orders).HasInverse(o => o.Employee);
      empBuilder.NavigationProperty(emp => emp.Manager).HasInverse(emp => emp.DirectReports).HasForeignKey(emp => emp.ReportsToEmployeeID);

      var prodBuilder = new EntityTypeBuilder<Product>(metadataStore);
      prodBuilder.DataProperty(p => p.ProductID).IsPartOfKey();
      prodBuilder.NavigationProperty(prod => prod.Category).HasForeignKey(prod => prod.CategoryID);


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
