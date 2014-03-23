using System;
using System.Collections.Generic;

namespace Model.Inheritance.Produce {

  public partial class Fruit : ItemOfProduce {
    public override void Initialize() {
      base.Initialize();
      InitializedTypes.Add("Fruit");
      IsFruit = true;
    }

    public bool IsFruit {
      get;
      set;
    }
    public string Name {
      get { return GetValue<string>(); }
      set { SetValue(value); }
    }
    public string USDACategory {
      get { return GetValue<string>(); }
      set { SetValue(value); }
    }
  }
}
