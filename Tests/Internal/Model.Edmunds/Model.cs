using System.ComponentModel;
using Breeze.Sharp;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

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