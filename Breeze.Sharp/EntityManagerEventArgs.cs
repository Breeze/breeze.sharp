
namespace Breeze.Sharp {
  /// <summary>
  /// Arguments to the <see cref="E:EntityManager.EntityManagerCreated"/> event.
  /// </summary>
  public class EntityManagerCreatedEventArgs : System.EventArgs {

    /// <summary>
    /// Initialize a new instance of this class.
    /// </summary>
    public EntityManagerCreatedEventArgs(EntityManager entityManager) {
      _entityManager = entityManager;
    }

    /// <summary>
    /// The EntityManager involved in this event.
    /// </summary>
    public EntityManager EntityManager {
      get { return _entityManager; }
    }

    private readonly EntityManager _entityManager;

  }

  /// <summary>
  /// Arguments to the EntityManager's <see cref="E:EntityManager.HasChangesChanged"/> event.
  /// </summary>
  public class EntityManagerHasChangesChangedEventArgs : System.EventArgs {

    /// <summary>
    /// Initialize a new instance of this class.
    /// </summary>
    public EntityManagerHasChangesChangedEventArgs(EntityManager entityManager) {
      EntityManager = entityManager;
      
    }

    /// <summary>
    /// The EntityManager involved in this event.
    /// </summary>
    public EntityManager EntityManager {
      get;
      private set;
    }

    public bool HasChanges {
      get { return EntityManager.HasChanges(); }
    }

    

  }
}
