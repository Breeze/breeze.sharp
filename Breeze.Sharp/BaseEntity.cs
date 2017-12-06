using System;
using System.Collections;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace Breeze.Sharp {

  /// <summary>
  /// Default base class implementation of <see cref="IEntity"/>.
  /// All entities should inherit from this class either directly or indirectly.
  /// </summary>
  [DebuggerDisplay("{EntityAspect.EntityKey} - {EntityAspect.EntityState}")]
  public abstract class BaseEntity : IEntity {

    /// <summary>
    /// Protected empty ctor.
    /// </summary>
    protected BaseEntity() {
      EntityAspect = new EntityAspect(this);
    }

    /// <inheritdoc />
    public virtual void Initialize() {
      // noop
    }

    

    #region syntactic sugar helper methods 

    protected T GetValue<T>([CallerMemberName] string propertyName = "") {
      return EntityAspect.GetValue<T>(propertyName);
    }

    protected void SetValue(Object value, [CallerMemberName] string propertyName = "") {
      EntityAspect.SetValue(propertyName, value);
    }

    #endregion

    /// <inheritdoc />
    public EntityAspect EntityAspect {
      get;
      set;
    }

    void IEditableObject.BeginEdit() {
      ((IEditableObject)EntityAspect).BeginEdit();
    }

    void IEditableObject.CancelEdit() {
      ((IEditableObject)EntityAspect).CancelEdit();
    }

    void IEditableObject.EndEdit() {
      ((IEditableObject)EntityAspect).EndEdit();
    }

    void IChangeTracking.AcceptChanges() {
      ((IChangeTracking)EntityAspect).AcceptChanges();
    }

    bool IChangeTracking.IsChanged {
      get { return ((IChangeTracking)EntityAspect).IsChanged; }
    }

    void IRevertibleChangeTracking.RejectChanges() {
      ((IRevertibleChangeTracking)EntityAspect).RejectChanges();
    }

    event PropertyChangedEventHandler INotifyPropertyChanged.PropertyChanged {
      // EntityAspect.PropertyChanged is a different event that tracks changes to the EntityAspect itself.
      add { EntityAspect.EntityPropertyChanged += value; }
      remove { EntityAspect.EntityPropertyChanged -= value; ; }
    }

    event EventHandler<DataErrorsChangedEventArgs> INotifyDataErrorInfo.ErrorsChanged {
      add { INotifyDataErrorInfoImpl.ErrorsChanged += value; }
      remove { INotifyDataErrorInfoImpl.ErrorsChanged -= value; }
    }

    IEnumerable INotifyDataErrorInfo.GetErrors(string propertyName) {
      return INotifyDataErrorInfoImpl.GetErrors(propertyName);
    }

    bool INotifyDataErrorInfo.HasErrors {
      get { return INotifyDataErrorInfoImpl.HasErrors; }
    }

    int IComparable.CompareTo(object obj) {
      throw new NotImplementedException();
    }

    private INotifyDataErrorInfo INotifyDataErrorInfoImpl {
      get {
        return (INotifyDataErrorInfo)EntityAspect;
      }
    }
  }

  
}