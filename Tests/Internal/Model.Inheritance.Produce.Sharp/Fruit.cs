using System;
using System.Collections.Generic;

namespace Model.Inheritance.Produce {

  public partial class Fruit : ItemOfProduce {
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
