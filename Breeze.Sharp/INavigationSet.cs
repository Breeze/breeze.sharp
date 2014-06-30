using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Breeze.Sharp.Core;
using System.ComponentModel;
using System.Collections.Specialized;
using System.Collections.ObjectModel;

namespace Breeze.Sharp {

  /// <summary>
  /// The interface that describes the type for any nonscalar navigation property on an Entity.
  /// </summary>
  public interface INavigationSet : IEnumerable, INotifyPropertyChanged, INotifyCollectionChanged {
    /// <summary>
    /// The parent <see cref="IEntity"/> associated with this collection.
    /// </summary>
    IEntity ParentEntity { get; set; }

    /// <summary>
    /// The <see cref="NavigationProperty"/> associated with this collection;
    /// </summary>
    NavigationProperty NavigationProperty { get; set; }
    /// <summary>
    /// Adds an IEntity to this collection - if an entity is added that is already in the collection, the add is ignored.
    /// </summary>
    /// <param name="entity"></param>
    void Add(IEntity entity);
    /// <summary>
    /// Removes an IEntity from this collection.
    /// </summary>
    /// <param name="entity"></param>
    void Remove(IEntity entity);
    /// <summary>
    /// Returns whether an IEntity is part of this collection.
    /// </summary>
    /// <param name="entity"></param>
    /// <returns></returns>
    bool Contains(IEntity entity);
    /// <summary>
    /// Clears this collection.
    /// </summary>
    void Clear();

    /// <inheritdoc />
    int Count { get; }


  }

  /// <summary>
  /// Concrete strongly typed implementation of the INavigationSet interface.  Used, by default, for
  /// as the return type for all nonscalar navigation properties.
  /// </summary>
  /// <typeparam name="T"></typeparam>
  public class NavigationSet<T> : NotifiableCollection<T>, INavigationSet where T:IEntity {

    /// <summary>
    /// Ctor.
    /// </summary>
    public NavigationSet() {
    }   


    #region Public props

    public IEntity ParentEntity {
      get;
      set;
    }

    public NavigationProperty NavigationProperty {
      get {
        if (ParentEntity.EntityAspect.IsAttached != _isAttached) {
          _navigationProperty = ParentEntity.EntityAspect.EntityType.GetNavigationProperty(_navigationProperty.Name);
          _isAttached = ParentEntity.EntityAspect.IsAttached;
        }
        return _navigationProperty;
      }
      set {
        _navigationProperty = value;
      }
    }

    #endregion

    #region INavigationSet imp

    void INavigationSet.Add(IEntity entity) {
      Add((T) entity);
    }


    void INavigationSet.Remove(IEntity entity) {
      Remove((T)entity);
    }

    bool INavigationSet.Contains(IEntity entity) {
      return Contains((T)entity);
    }

    #endregion

    #region Overrides

    protected override void InsertItem(int index, T entity) {
      // contains in next line is needed
      if (_inProcess || this.Contains(entity)) return;
      
      var parentAspect = ParentEntity == null ? null : ParentEntity.EntityAspect;
      if (parentAspect == null
        || parentAspect.IsDetached
        || parentAspect.EntityManager.IsLoadingEntity) {
        base.InsertItem(index, entity);
        return;
      }
      using (new BooleanUsingBlock(b => _inProcess = b)) {
        if (entity.EntityAspect.IsDetached) {
          entity.EntityAspect.Attach(EntityState.Added, parentAspect.EntityManager);
        }
        base.InsertItem(index, entity);
        ConnectRelated(entity);
      }

    }

    protected override void RemoveItem(int index) {
      if (_inProcess) return;
      var parentAspect = ParentEntity == null ? null : ParentEntity.EntityAspect;
      if (parentAspect == null
        || parentAspect.IsDetached
        || parentAspect.EntityManager.IsLoadingEntity) {
          base.RemoveItem(index);
        return;
      }
      using (new BooleanUsingBlock(b => _inProcess = b)) {
        var entity = Items[index];
        base.RemoveItem(index);
        DisconnectRelated(entity);
      }
    }

    protected override void ClearItems() {
      // TODO: need to resolve this. - when is it called
      base.ClearItems();
    }

    protected override void SetItem(int index, T item) {
      // TODO: need to resolve this.
      base.SetItem(index, item);
    }

    #endregion

    #region Other private

    private bool _inProcess = false;
    private NavigationProperty _navigationProperty;
    private bool _isAttached = false;

    private void ConnectRelated(IEntity entity) {

      var aspect = entity.EntityAspect;
      var parentAspect = ParentEntity.EntityAspect;
      var np = this.NavigationProperty;
      var invNp = np.Inverse;
      if (invNp != null) {
        aspect.SetNpValue(invNp, ParentEntity);
      } else {
        // This occurs with a unidirectional 1-n navigation - in this case
        // we need to update the fks instead of the navProp
        var pks = parentAspect.EntityType.KeyProperties;
        np.InvForeignKeyProperties.ForEach((fkp, i) => {
          entity.EntityAspect.SetDpValue(fkp, parentAspect.GetValue(pks[i]));
        });
      }
    }

    private void DisconnectRelated(T entity) {
      var aspect = entity.EntityAspect;
      var parentAspect = ParentEntity.EntityAspect;
      var invNp = NavigationProperty.Inverse;
      if (invNp != null) {
        if (invNp.IsScalar) {
          entity.EntityAspect.SetNpValue(invNp, null);
        } else {
          throw new Exception("Many-many relations not yet supported");
        }
      } else {
        // This occurs with a unidirectional 1-n navigation - in this case
        // we need to update the fks instead of the navProp
        var pks = parentAspect.EntityType.KeyProperties;
        this.NavigationProperty.InvForeignKeyProperties.ForEach((fkp, i) => {
          // TODO: write a test to see what happens if this fails
          aspect.SetDpValue(fkp, fkp.DefaultValue);
        });
      }
    }


    #endregion


    
  }
}

