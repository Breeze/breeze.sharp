using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Breeze.Sharp {

  /// <summary>
  /// Client side interface that describes the interaction between a DataService and a remote data service.
  /// </summary>
  public interface IDataServiceAdapter {
    String Name { get; }
    IJsonResultsAdapter JsonResultsAdapter { get;  }
    Task<SaveResult> SaveChanges(IEnumerable<IEntity> entitiesToSave, SaveOptions saveOptions);
  }
}
