
using System;
using System.Collections.Generic;

namespace Model.Inheritance.Produce {


  public partial class Tomato : Vegetable {
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
    public Nullable<bool> Determinate {
      get { return GetValue<Nullable<bool>>(); }
      set { SetValue(value); }
    }
  }
}
