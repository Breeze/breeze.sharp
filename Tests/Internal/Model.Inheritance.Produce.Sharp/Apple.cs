using System;
using System.Collections.Generic;

namespace Model.Inheritance.Produce {

  public partial class Apple : Fruit {
    public string Variety {
      get { return GetValue<string>(); }
      set { SetValue(value); }
    }
    public string Description {
      get { return GetValue<string>(); }
      set { SetValue(value); }
    }
    public byte[] Photo {
      get { return GetValue<byte[]>(); }
      set { SetValue(value); }
    }
  }
}
