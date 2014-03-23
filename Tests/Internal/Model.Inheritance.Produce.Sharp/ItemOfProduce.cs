
using Breeze.Sharp;
using System;
using System.Collections.Generic;

namespace Model.Inheritance.Produce {

  public abstract partial class ItemOfProduce : BaseEntity {
    public ItemOfProduce() {
      this.RowVersion = 0;
    }

    public override void Initialize() {
      base.Initialize();
      InitializedTypes = new List<string>();
      InitializedTypes.Add("ItemOfProduce");
    }

    public List<String> InitializedTypes {
      get;
      set;
    }

    public System.Guid Id {
      get { return GetValue<Guid>(); }
      set { SetValue(value); }
    }
    public string ItemNumber {
      get { return GetValue<string>(); }
      set { SetValue(value); }
    }
    public Nullable<decimal> UnitPrice {
      get { return GetValue<decimal>(); }
      set { SetValue(value); }
    }
    public string QuantityPerUnit {
      get { return GetValue<string>(); }
      set { SetValue(value); }
    }
    public Nullable<short> UnitsInStock {
      get { return GetValue<short>(); }
      set { SetValue(value); }
    }
    public short UnitsOnOrder {
      get { return GetValue<short>(); }
      set { SetValue(value); }
    }
    public Nullable<int> RowVersion {
      get { return GetValue<Nullable<int>>(); }
      set { SetValue(value); }
    }
  }
}
