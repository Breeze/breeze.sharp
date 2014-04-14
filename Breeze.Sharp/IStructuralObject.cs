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
    EntityAspect EntityAspect { get; set; }
  }

  /// <summary>
  /// Interface that describes a ComplexObject
  /// </summary>
  public interface IComplexObject : IStructuralObject, INotifyDataErrorInfo, IComparable {
    ComplexAspect ComplexAspect { get; set; }
  }

  /// <summary>
  /// Interface implemented by by IEntity and IComplexObject. 
  /// </summary>
  public interface IStructuralObject {
    /// <summary>
    ///  Method that is automatically called after the materialization of any 
    /// IEntity or IComplexObject after being retrieved from a remote data service.
    /// </summary>
    void Initialize();
  }

  /// <summary>
  /// Extension methods for any IStructuralObject.
  /// </summary>
  public static class IStructuralObjectExtns {

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
