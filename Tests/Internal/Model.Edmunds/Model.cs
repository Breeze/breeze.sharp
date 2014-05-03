using System.ComponentModel;
using Breeze.Sharp;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Breeze.Sharp.Core;
using Newtonsoft.Json.Linq;

namespace Model.Edmunds {

  public static class Config {
    public static void Initialize() {
      

      var makeBuilder = new EntityTypeBuilder<Make>();
      makeBuilder.DataProperty(make => make.Id).IsPartOfKey();
      makeBuilder.DataProperty(make => make.Name).MaxLength(40);
      // makeBuilder.NavigationProperty(make => make.Models).HasInverse(model => model.Make);
      
      var modelBuilder = new EntityTypeBuilder<Model>();
      modelBuilder.DataProperty(model => model.Id).IsPartOfKey();
      modelBuilder.NavigationProperty(model => model.Make)
        .HasForeignKey(model => model.MakeId)
        .HasInverse(make => make.Models);
       
      
    }
  }

  public class AltNamingConvention : NamingConvention {
    public AltNamingConvention() {
      
    }

    public override string ClientPropertyNameToServer(string serverName, StructuralType parentType) {
      if (parentType.Namespace == "Model.Edmunds") {
        return serverName.Substring(0, 1).ToLower() + serverName.Substring(1);
      } else {
        return base.ClientPropertyNameToServer(serverName, parentType);
      }
    }

    public override String ServerPropertyNameToClient(String serverName, StructuralType parentType) {
      if (parentType.Namespace == "Model.Edmunds") {
        return serverName.Substring(0, 1).ToUpper() + serverName.Substring(1);
      } else {
        return base.ServerPropertyNameToClient(serverName, parentType);
      }}
    
  }

  public class EdmundsJsonResultsAdapter : IJsonResultsAdapter {

    public string Name {
      get { return "Edmunds"; }
    }

    public JToken ExtractResults(JToken node) {
      JToken token;
      var rootNode = (JObject) node;
      if (rootNode.TryGetValue("makeHolder", out token)) {
        return token;
      }

      if (rootNode.TryGetValue("modelHolder", out token)) {
        return token;
      }
      return node;
    }

    public JsonNodeInfo VisitNode(JObject node, MappingContext mappingContext, NodeContext nodeContext) {
      var result = new JsonNodeInfo();
      var idProp = node.Property("id");
      var modelsProp = node.Property("models");
      if (idProp != null && modelsProp != null) {
        node.Add("modelLinks", modelsProp.Value);
        modelsProp.Value = new JArray();
        result.ServerTypeNameInfo = new TypeNameInfo("Make", "");
        return result;
      }

      var makeProp = node.Property("make");
      if (idProp != null && makeProp != null) {
        node.Add("makeLink", makeProp.Value);
        makeProp.Value = null;
        var catsProp = node.Property("categories");
        if (catsProp != null) {
          var styleProps = catsProp["Vehicle Style"];
          var styles = styleProps.Select(t => t.Value<String>()).ToAggregateString(", ");
          node.Add("vehicleStyles", new JValue(styles));
        }
        result.ServerTypeNameInfo = new TypeNameInfo("Model", "");
        return result;
      }
      return result;
      //// Model parser
      //else if (node.id && node.makeId) {
      //    // move 'node.make' link so 'make' can be null reference
      //    node.makeLink = node.make;
      //    node.make = null;

      //    // flatten styles and sizes as comma-separated strings
      //    var styles = node.categories && node.categories["Vehicle Style"];
      //    node.vehicleStyles = styles && styles.join(", ");
      //    var sizes = node.categories && node.categories["Vehicle Size"];
      //    node.vehicleSizes = sizes && sizes.join(", ");

      //    return { entityType: "Model" };
      //}
    }
  }

  public class Make : BaseEntity {
    public long Id {
      get { return GetValue<long>(); }
      set { SetValue(value); }
    }

    public String Name {
      get { return GetValue<String>(); }
      set { SetValue(value);}
    }

    public String NiceName {
      get { return GetValue<String>(); }
      set { SetValue(value); }
    }

    public Object ModelLinks {
      get { return GetValue<Object>(); }
      set { SetValue(value); }
    }

    public NavigationSet<Model> Models {
      get { return GetValue<NavigationSet<Model>>(); }
      set { SetValue(value); }
    }
  }

  public class Model : BaseEntity {
    public long Id {
      get { return GetValue<long>(); }
      set { SetValue(value); }
    }

    public long MakeId {
      get { return GetValue<long>(); }
      set { SetValue(value); }
    }

    public String MakeName {
      get { return GetValue<String>(); }
      set { SetValue(value); }
    }
    public String MakeNiceName {
      get { return GetValue<String>(); }
      set { SetValue(value); }
    }
    public String Name {
      get { return GetValue<String>(); }
      set { SetValue(value); }
    }
    public String NiceName {
      get { return GetValue<String>(); }
      set { SetValue(value); }
    }
    public String VehicleStyles {
      get { return GetValue<String>(); }
      set { SetValue(value); }
    }
    public String VehicleSizes {
      get { return GetValue<String>(); }
      set { SetValue(value); }
    }
    public Object Categories {
      get { return GetValue<Object>(); }
      set { SetValue(value); }
    }

    public Make Make {
      get { return GetValue<Make>(); }
      set { SetValue(value);}
    }
  }
}


//function initialize(metadataStore) {
//       metadataStore.addEntityType({
//           shortName: "Make",
//           namespace: "Edmunds",
//           dataProperties: {
//               id:         { dataType: DT.Int64, isPartOfKey: true },
//               name:       { dataType: DT.String },
//               niceName:   { dataType: DT.String },
//               modelLinks: { dataType: DT.Undefined }
//           },
//           navigationProperties: {
//               models: {
//                   entityTypeName:  "Model:#Edmunds", isScalar: false,
//                   associationName: "Make_Models"
//               }
//           }
//       });

//       metadataStore.addEntityType({
//           shortName: "Model",
//           namespace: "Edmunds",
//           dataProperties: {
//               id:            { dataType: "String", isPartOfKey: true },
//               makeId:        { dataType: "Int64" },
//               makeName:      { dataType: "String" },
//               makeNiceName:  { dataType: "String" },
//               name:          { dataType: "String" },
//               niceName:      { dataType: "String" },
//               vehicleStyles: { dataType: "String" },
//               vehicleSizes:  { dataType: "String" },
//               categories:    { dataType: "Undefined" }
//           },
//           navigationProperties: {
//               make: {
//                   entityTypeName:  "Make:#Edmunds", isScalar: true,
//                   associationName: "Make_Models",  foreignKeyNames: ["makeId"]
//               }
//           }
//       });
//   }