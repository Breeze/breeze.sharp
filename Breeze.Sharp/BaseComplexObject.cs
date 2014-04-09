using System;
using System.Collections;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Breeze.Sharp {

  /// <summary>
  /// Default base class implementation of IComplexObject. 
  /// </summary>
  public abstract class BaseComplexObject : IComplexObject {

    /// <summary>
    /// 
    /// </summary>
    protected BaseComplexObject() {
      ComplexAspect = new ComplexAspect(this, null);
    }

    public virtual void Initialize() {
      // nnop;
    }

    #region syntactic sugar helper methods 

    protected T GetValue<T>([CallerMemberName] string propertyName = "") {
      return ComplexAspect.GetValue<T>(propertyName);
    }

    protected void SetValue(Object value, [CallerMemberName] string propertyName = "") {
      ComplexAspect.SetValue(propertyName, value);
    }

    /// <summary>
    /// Compares this ComplexObject with another and determines if they represent the same data.
    /// </summary>
    /// <param name="other"></param>
    /// <returns></returns>
    public bool StructuralEquals(BaseComplexObject other) {
      return this.ComplexAspect.StructuralEquals(other.ComplexAspect);
    }

    #endregion

    /// <summary>
    /// Returns the ComplexAspect associated with this ComplexObject.
    /// </summary>
    public ComplexAspect ComplexAspect {
      get;
      set;
    }

    int IComparable.CompareTo(object obj) {
      throw new NotImplementedException();
    }

    #region INotifyDataErrorInfo 

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

    private INotifyDataErrorInfo INotifyDataErrorInfoImpl {
      get {
        return (INotifyDataErrorInfo)ComplexAspect;
      }
    }
  }
    #endregion

}