using System;
using System.Collections.Generic;

using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Breeze.Sharp {

  /// <summary>
  /// 
  /// </summary>
  public interface IEntity : IStructuralObject, INotifyDataErrorInfo, INotifyPropertyChanged, IEditableObject, 
    IChangeTracking, IRevertibleChangeTracking, IComparable {
    EntityAspect EntityAspect { get; set; }
  }

  /// <summary>
  /// Interface implemented by any ComplexObject.
  /// </summary>
  public interface IComplexObject : IStructuralObject, INotifyDataErrorInfo, IComparable {
    /// <summary>
    /// Returns the ComplexAspect associated with this ComplexObject.
    /// </summary>
    ComplexAspect ComplexAspect { get; set; }
  }

  /// <summary>
  /// Interface implemented by entities and complex types. 
  /// </summary>
  public interface IStructuralObject {
    /// <summary>
    /// For internal use only.
    /// </summary>
    void Initialize();
  }

  /// <summary>
  /// Extension methods that operate on any IStructuralObject. i.e. Entities and ComplexObjects
  /// </summary>
  public static class IStructuralObjectExtns {

    /// <summary>
    /// Returns the StructuralAspect associated with this StructuralObject i.e. return either an EntityAspect or a ComplexAspect.
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
