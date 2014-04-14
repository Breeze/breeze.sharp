using Breeze.Sharp.Core;
using System;
using System.Collections.Generic;

namespace Breeze.Sharp {

  /// <summary>
  /// Class that is used to track property changes on an Entity or ComplexObject.  Instances of this type are used 
  /// for both the EntityAspect.OriginalValuesMap as well as to track IEditableObject changes on an entity.
  /// </summary>
  public  class BackupValuesMap : SafeDictionary<String, Object> {
    public BackupValuesMap() : base() {}
    public BackupValuesMap(Dictionary<String, Object> map) : base(map) {
    }

    public static BackupValuesMap Empty = new BackupValuesMap(new Dictionary<String, Object>());
  }

 
}
