
using Breeze.Sharp.Core;
using System;
using System.Linq.Expressions;
using System.Reflection;

namespace Breeze.Sharp {

  /// <summary>
  /// 
  /// </summary>
  public class EntityTypeReflector  {

    public EntityTypeReflector(Type clrType) {
      if (!typeof (IEntity).IsAssignableFrom(clrType)) {
        throw new ArgumentException("The 'clrType' parameter must implement the IEntity interface");
      }

    }
    
  }
}
