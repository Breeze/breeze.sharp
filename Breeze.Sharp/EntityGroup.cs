
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Breeze.Sharp.Core;

namespace Breeze.Sharp {

  #region EntityGroup
  
  internal class EntityGroup : IGrouping<Type, EntityAspect>  {

    #region ctors

    protected EntityGroup(Type clrEntityType) {
      ClrType = clrEntityType;
    }

    /// <summary>
    /// Creates an instance of an EntityGroup for a specific entity type.
    /// </summary>
    /// <param name="clrEntityType"></param>
    /// <returns></returns>
    internal static EntityGroup Create(Type clrEntityType, EntityManager em) {
      var entityGroup = new EntityGroup(clrEntityType);
      entityGroup.Initialize(em);
      return entityGroup;
    }

    private void Initialize(EntityManager em) {
      _entityAspects = new EntityCollection();
      _entityKeyMap = new Dictionary<EntityKey, EntityAspect>();
      EntityManager = em;
      EntityType = em.MetadataStore.GetEntityType(ClrType);
      // insure that any added table can watch for change events
      ChangeNotificationEnabled = em.ChangeNotificationEnabled;      
    }


    #endregion

    #region Public properties

    public Type ClrType {
      get;
      private set;
    }

    /// <summary>
    /// The type of Entity contained within this group.
    /// </summary>
    public EntityType EntityType {
      get;
      internal set; 
    }

    /// <summary>
    /// The <see cref="T:Breeze.Sharp.EntityManager"/> which manages this EntityGroup.
    /// </summary>
    public EntityManager EntityManager {
      get;
      internal set;
    }

    /// <summary>
    /// The type being queried. (same as EntityType for an EntityGroup)
    /// </summary>
    public Type QueryableType {
      get { return EntityType.ClrType; }
    }

    /// <summary>
    /// The name of this group.
    /// </summary>
    public String Name {
      get { return ClrType.FullName; }
    }

    /// <summary>
    /// Used to suppress change events during the modification of entities within this group.
    /// </summary>
    public bool ChangeNotificationEnabled {
      get;
      set;
    }

    /// <summary>
    /// Returns a list of groups for this entity type and all sub-types.
    /// </summary>
    public ReadOnlyCollection<EntityGroup> SelfAndSubtypeGroups {
      get {
        if (_selfAndSubtypeGroups == null) {
          _selfAndSubtypeGroups = EntityType.SelfAndSubEntityTypes
            .Select(et => EntityManager.GetEntityGroup(et.ClrType))
            .ToSafeList();
        }
        return _selfAndSubtypeGroups.ReadOnlyValues;
      }
    }

    /// <summary>
    /// Returns a collection of entities of given entity type and sub-types.
    /// </summary>
    public IEnumerable<IEntity> Entities {
      get {
        return EntityAspects.Select(w => w.Entity);
      }
    }

    /// <summary>
    /// Returns the currently live (i.e not deleted or detached) entities for the given entity type and its subtypes.
    /// </summary>
    public IEnumerable<IEntity> CurrentEntities {
      get {
        return EntityAspects
          .Where(e => !e.EntityState.IsDeletedOrDetached())
          .Select(w => w.Entity);
      }
    }

    internal IEnumerable<EntityAspect> EntityAspects {
      get {
        return SelfAndSubtypeGroups
            .SelectMany(f => f.LocalEntityAspects);
      }
    }


    internal IEnumerable<EntityAspect> LocalEntityAspects {
      get { return _entityAspects; }
    }

    #endregion

    #region Misc public methods

    internal void Clear() {
      // do not call detach on each entityAspect - very slow and not needed 
      // all we really need to do is set each _entityaspect.EntityState to detached
      _entityAspects.ForEach(ea => ea.DetachOnClear());
      if (_selfAndSubtypeGroups != null) _selfAndSubtypeGroups.Clear();
      _entityAspects.Clear();
      _entityKeyMap.Clear();
    }

    /// <summary>
    /// Returns the EntityGroup name corresponding to any <see cref="IEntity"/> subtype.
    /// </summary>
    /// <param name="entityType"></param>
    /// <returns></returns>
    public static String GetNameFor(Type entityType) {
      return entityType.FullName;
    }

    #endregion

    #region Get/Accept/Reject changes methods

    /// <summary>
    /// Returns all of the entities within this group with the specified state or states.
    /// </summary>
    /// <param name="state"></param>
    /// <returns></returns>
    public IEnumerable<IEntity> GetChanges(EntityState state) {
      return LocalEntityAspects.Where(ea => (ea.EntityState & state) > 0).Select(ew => ew.Entity);
    }

    /// <summary>
    /// Calls <see cref="EntityAspect.AcceptChanges"/> on all entities in this group.
    /// </summary>
    public void AcceptChanges() {
      ChangedAspects.ToList().ForEach(ea => ea.AcceptChanges());
    }

    /// <summary>
    /// Calls <see cref="EntityAspect.RejectChanges"/> on all entities in this group.
    /// </summary>
    public void RejectChanges() {
      ChangedAspects.ToList().ForEach(ea => ea.RejectChanges());
    }

    /// <summary>
    /// Determines whether any entity in this group has pending changes.
    /// </summary>
    /// <returns></returns>
    public bool HasChanges() {
      return ChangedAspects.Any();
    }

    private IEnumerable<EntityAspect> ChangedAspects {
      get { return LocalEntityAspects.Where(ea => ea.HasChanges()); }
    }

    #endregion

    #region Internal props/methods 


    internal EntityAspect FindEntityAspect(EntityKey entityKey, bool includeDeleted) {
      EntityAspect result;
      // this can occur when we are trying to find say EntityKey(Order, 3)
      // in a collection of InternationalOrder keys
      if (entityKey.EntityType != this.EntityType) {
        entityKey = new EntityKey(this.EntityType, entityKey.Values);
      }
      if (_entityKeyMap.TryGetValue(entityKey, out result)) {
        if (result.EntityState.IsDeleted()) {
          return includeDeleted ? result : null;
        } else {
          return result;
        }
      } else {
        return null;
      }
    }

    internal EntityAspect AttachEntityAspect(EntityAspect entityAspect, EntityState entityState) {
      entityAspect.EntityGroup = this;
      AddToKeyMap(entityAspect);
      _entityAspects.Add(entityAspect);
      entityAspect.SetEntityStateCore(entityState);
      return entityAspect;
    }

    internal void DetachEntityAspect(EntityAspect aspect) {
      _entityAspects.Remove(aspect);
      RemoveFromKeyMap(aspect);
    }

    internal void ReplaceKey(EntityAspect entityAspect, EntityKey oldKey, EntityKey newKey) {
      _entityKeyMap.Remove(oldKey);  // it may not exist if this object was just Imported or Queried.
      _entityKeyMap.Add(newKey, entityAspect);
    }

    internal void UpdateFkVal(DataProperty fkProp, Object oldValue, Object newValue) {
      var fkPropName = fkProp.Name;
      _entityAspects.ForEach(ea => {
        if (Equals(ea.GetValue(fkPropName), oldValue)) {
          ea.SetValue(fkPropName, newValue);
        }
      });
    }

    internal bool KeyMapContains(EntityKey key) {
      EntityAspect val;
      return _entityKeyMap.TryGetValue(key, out val);
    }
    #endregion

    #region private and protected

    private void AddToKeyMap(EntityAspect aspect) {
      try {
        _entityKeyMap.Add(aspect.EntityKey, aspect);
      } catch (ArgumentException) {
        throw new InvalidOperationException("An entity with this key: " + aspect.EntityKey.ToString() + " already exists in this EntityManager");
      }
    }

    private void RemoveFromKeyMap(EntityAspect aspect) {
      _entityKeyMap.Remove(aspect.EntityKey);
    }

   
    #endregion

 

    #region explict interfaces

    Type IGrouping<Type, EntityAspect>.Key {
      get { return this.EntityType.ClrType; }
    }

    IEnumerator<EntityAspect> IEnumerable<EntityAspect>.GetEnumerator() {
      return EntityAspects.GetEnumerator();
    }

    IEnumerator IEnumerable.GetEnumerator() {
      return EntityAspects.GetEnumerator();
    }

    #endregion

    #region Fields

    
    // this member will only exist on EntityCache's sent from the server to the client
    // it should always be null on persistent client side entity sets
    private EntityCollection _entityAspects;
    private Dictionary<EntityKey, EntityAspect> _entityKeyMap;
    private SafeList<EntityGroup> _selfAndSubtypeGroups;


    #endregion

  }

  #endregion

  #region EntityGroupCollection and EntityCollection

  internal class EntityGroupCollection : MapCollection<Type, EntityGroup> {
    protected override Type GetKeyForItem(EntityGroup item) {
      return item.ClrType;
    }
  }

  internal class EntityCollection : IEnumerable<EntityAspect> {

    internal EntityCollection() {
      _innerList = new List<EntityAspect>();
      _emptyIndexes = new List<int>();
    }

    internal void Add(EntityAspect aspect) {
      var indexCount = _emptyIndexes.Count;
      if (indexCount == 0) {
        var index = _innerList.Count;
        _innerList.Add(aspect);
        aspect.IndexInEntityGroup = index;
      } else {
        var newIndex = _emptyIndexes[indexCount - 1];
        _innerList[newIndex] = aspect;
        aspect.IndexInEntityGroup = newIndex;
        _emptyIndexes.RemoveAt(indexCount - 1);
      }
    }

    internal void Remove(EntityAspect aspect) {
      var index = aspect.IndexInEntityGroup;
      if (aspect != _innerList[index]) {
        throw new Exception("Error in EntityCollection removall logic");
      }
      aspect.IndexInEntityGroup = -1;
      _innerList[index] = null;
      _emptyIndexes.Add(index);
    }

    internal void Clear() {
      this.ForEach(r => r.IndexInEntityGroup = -1);
      _innerList.Clear();
      _emptyIndexes.Clear();
    }

    public int Count {
      get {
        return _innerList.Count - _emptyIndexes.Count;
      }
    }

    #region IEnumerable<EntityAspect> Members

    public IEnumerator<EntityAspect> GetEnumerator() {
      foreach (var item in _innerList) {
        if (item != null) {
          yield return item;
        }
      }
    }

    #endregion

    #region IEnumerable Members

    System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() {
      return GetEnumerator();
    }

    #endregion

    private List<EntityAspect> _innerList;
    private List<int> _emptyIndexes;
  }


  #endregion

  #region NullEntityGroup - not currently used. 
  //public class NullEntityGroup : EntityGroup {

  //  public NullEntityGroup(Type clrType, EntityType entityType) 
  //    : base(clrType, entityType) {
  //  }

  //  public override bool ChangeNotificationEnabled {
  //    get {
  //      return false; 
  //    }
  //    set {
  //      if (value == true) {
  //        throw new Exception("ChangeNotificationEnabled cannot be set on a null EntityGroup");
  //      }
  //    }
  //  }

  //  public override bool IsNullGroup { get { return true;  }   }
  //  public override bool IsDetached  {
  //    get { return true; } 
  //    protected set { throw new Exception("IsDetached cannot be set on a null EntityGroup");}
  //  }
  //  protected override IEnumerable<EntityAspect> LocalEntityAspects {
  //    get {
  //      return Enumerable.Empty<EntityAspect>();
  //    }
  //  }
  //  public override ReadOnlyCollection<EntityGroup> SelfAndSubtypeGroups {
  //    get { return _selfAndSubtypeGroups.ReadOnlyValues;  }
  //  }


  //}
  #endregion
}
