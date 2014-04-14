using System.Linq;
using Breeze.Sharp.Core;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace Breeze.Sharp {
  

  /// <summary>
  /// The result of an  <see cref="EntityManager.SaveChanges(SaveOptions)"/>  call
  /// </summary>
  public class SaveResult {

    internal SaveResult(IEnumerable<IEntity> entities, Dictionary<EntityKey, EntityKey> keyMappings) {
      _savedEntities = new SafeList<IEntity>(entities);
      _keyMappings = new SafeDictionary<EntityKey,EntityKey>(keyMappings);
    }

    /// <summary>
    /// The saved entities - with any temporary keys converted into 'real' keys.
    /// These entities are actually references to entities in the EntityManager 
    /// cache that have been updated as a result of the save.
    /// </summary>
    public ReadOnlyCollection<IEntity> Entities {
      get { return _savedEntities.ReadOnlyValues;  }
    }

    /// <summary>
    /// Dictionary that maps presave EntityKeys to postSave entity keys for all temporary keys where the server 
    /// will have generated the new key.
    /// </summary>
    public ReadOnlyDictionary<EntityKey, EntityKey> KeyMappings {
      get { return _keyMappings.ReadOnlyDictionary; }
    }

    private SafeList<IEntity> _savedEntities;
    private SafeDictionary<EntityKey, EntityKey> _keyMappings;

    public static SaveResult Empty = new SaveResult(Enumerable.Empty<IEntity>(), new Dictionary<EntityKey, EntityKey>() );
    
  }


}
