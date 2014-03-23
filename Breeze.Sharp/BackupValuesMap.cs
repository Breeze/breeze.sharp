using Breeze.Sharp.Core;
using System;
using System.Collections.Generic;

namespace Breeze.Sharp {

  public  class BackupValuesMap : SafeDictionary<String, Object> {
    public BackupValuesMap() : base() {}
    public BackupValuesMap(Dictionary<String, Object> map) : base(map) {
    }

    public static BackupValuesMap Empty = new BackupValuesMap(new Dictionary<String, Object>());
  }

 
}
