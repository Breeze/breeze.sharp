using System;
using System.Collections.Generic;

using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Breeze.Sharp {

  /// <summary>
  /// Interface that describes an Entity.
  /// </summary>
  public interface IEntity : IStructuralObject, INotifyDataErrorInfo, INotifyPropertyChanged, IEditableObject, 
    IChangeTracking, IRevertibleChangeTracking, IComparable {
    /// <summary>
    /// Returns the <see cref="EntityAspect"/> associated with this IEntity.
    /// </summary>
    EntityAspect EntityAspect { get; set; }
  }

  /// <summary>
  /// Interface that describes a ComplexObject
  /// </summary>
  public interface IComplexObject : IStructuralObject, INotifyDataErrorInfo, IComparable {
    /// <summary>
    /// Returns the <see cref="ComplexAspect"/> associated with this IComplexObject.
    /// </summary>
    ComplexAspect ComplexAspect { get; set; }
  }

  /// <summary>
  /// Interface implemented by by <see cref="IEntity"/> and <see cref="IComplexObject"/>. 
  /// </summary>
  public interface IStructuralObject {
    /// <summary>
    ///  Method that is automatically called after the materialization of any 
    /// <see cref="IEntity"/> or <see cref="IComplexObject"/> after being retrieved from a remote data service.
    /// </summary>
    void Initialize();
  }

  /// <summary>
  /// Extension methods for any <see cref="IStructuralObject"/>.
  /// </summary>
  public static class StructuralObjectExtensions {

    /// <summary>
    /// Returns either a EntityAspect or ComplexAspect for the associated IStructuralObject.
    /// </summary>
    /// <param name="so"></param>
    /// <returns></returns>
    public static StructuralAspect GetStructuralAspect(this IStructuralObject so) {
      var entity = so as IEntity;
      if (entity != null) {
        return entity.EntityAspect;
      } else {
        return ((IComplexObject)so).ComplexAspect;
      }
    }
  }

}
