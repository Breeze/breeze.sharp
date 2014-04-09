using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Breeze.Sharp {

  /// <summary>
  /// 
  /// </summary>
  public interface IDataServiceAdapter {
    /// <summary>
    /// The name of this adapter.
    /// </summary>
    String Name { get; }
    
    /// <summary>
    /// The IJsonResultsAdapter associated with this adapter.
    /// </summary>
    IJsonResultsAdapter JsonResultsAdapter { get;  }

    /// <summary>
    /// SaveChanges to the backend dataService asynchronously.
    /// </summary>
    /// <param name="entitiesToSave"></param>
    /// <param name="saveOptions"></param>
    /// <returns></returns>
    Task<SaveResult> SaveChanges(IEnumerable<IEntity> entitiesToSave, SaveOptions saveOptions);
  }
}
