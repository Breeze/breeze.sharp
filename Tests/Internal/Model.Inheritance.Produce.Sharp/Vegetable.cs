using System;
using System.Collections.Generic;

namespace Model.Inheritance.Produce {
  using System;
  using System.Collections.Generic;

  public partial class Vegetable : ItemOfProduce {
    public string Name {
      get { return GetValue<string>(); }
      set { SetValue(value); }
    }
    public string USDACategory {
      get { return GetValue<string>(); }
      set { SetValue(value); }
    }
    public Nullable<bool> AboveGround {
      get { return GetValue<Nullable<bool>>(); }
      set { SetValue(value); }
    }
  }
}
