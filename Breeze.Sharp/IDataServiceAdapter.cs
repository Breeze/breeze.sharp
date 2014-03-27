﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Breeze.Sharp {

  /// <summary>
  /// 
  /// </summary>
  public interface IDataServiceAdapter {
    String Name { get; }
    Task<SaveResult> SaveChanges(IEnumerable<IEntity> entitiesToSave, SaveOptions saveOptions);
  }
}