
using System.Linq;
using System.ServiceModel.Channels;
using System.Threading;
using Breeze.Sharp.Core;
using System;
using System.Linq.Expressions;
using System.Reflection;

namespace Breeze.Sharp {

  public class StructuralTypeBuilder {

    public static StructuralType GetStructuralType(Type clrType) {
      if (typeof (IEntity).IsAssignableFrom(clrType)) {
        return GetEntityType(clrType);
      } else {
        return GetComplexType(clrType);
      }
    }

    public static EntityType GetEntityType(Type clrType) {
      var entityType = MetadataStore.Instance.GetEntityType(clrType, true);
      return entityType ?? CreateEntityType(clrType);
    }

    public static ComplexType GetComplexType(Type clrType) {
      var complexType = MetadataStore.Instance.GetComplexType(clrType, true);
      return complexType ?? CreateComplexType(clrType);
    }

    private static EntityType CreateEntityType(Type clrType ) {
      var typeInfo = clrType.GetTypeInfo();
      var baseEntityType = (typeInfo.BaseType == typeof(Object)) ? null : GetEntityType(typeInfo.BaseType);
      
      var entityType = new EntityType() {
        ClrType = clrType,
        BaseEntityType = baseEntityType
      };

      MetadataStore.Instance.AddEntityType(entityType);

      if (baseEntityType != null) {
        if (typeof (IEntity).IsAssignableFrom(typeInfo.BaseType) && typeInfo.BaseType != typeof (BaseEntity)) {
          UpdateBaseProperties(entityType, baseEntityType);
        }
      }

      foreach (var pi in typeInfo.DeclaredProperties) {
        if (!pi.GetMethod.IsPublic) continue;
        if (pi.Name == "EntityAspect") continue;

        if (pi.PropertyType.GenericTypeArguments.Any() &&
            pi.PropertyType.GetGenericTypeDefinition() == typeof (NavigationSet<>)) {
          CreateNavigationProperty(entityType, pi);
        } else if (typeof (IEntity).IsAssignableFrom(pi.PropertyType)) {
          CreateNavigationProperty(entityType, pi);
        } else {
          CreateDataProperty(entityType, pi);
        }
      }

      
      return entityType;
    }

    private static ComplexType CreateComplexType(Type clrType) {
      var typeInfo = clrType.GetTypeInfo();
      var complexType = new ComplexType() {
        ClrType = clrType
      };

      MetadataStore.Instance.AddComplexType(complexType);

      var baseComplexType = (typeInfo.BaseType == typeof(Object)) ? null : GetComplexType(typeInfo.BaseType);
      if (baseComplexType != null)
      if (typeof(IComplexObject).IsAssignableFrom(typeInfo.BaseType) && typeInfo.BaseType != typeof(BaseComplexObject)) {
        UpdateBaseProperties(complexType, baseComplexType);
      }

      foreach (var pi in typeInfo.DeclaredProperties) {
        if (!pi.GetMethod.IsPublic) {
          continue;
        }
        CreateDataProperty(complexType, pi);
      }
      
      return complexType;
    }

    private static void UpdateBaseProperties(StructuralType structuralType, StructuralType baseStructuralType) {
      baseStructuralType.DataProperties.ForEach(dp => {
        var newDp = new DataProperty(dp);
        structuralType.AddDataProperty(newDp);
      });
      if (baseStructuralType.IsEntityType) {
        var entityType = (EntityType) structuralType;
        var baseEntityType = (EntityType) baseStructuralType;
        baseEntityType.NavigationProperties.ForEach(np => {
          var newNp = new NavigationProperty(np);
          entityType.AddNavigationProperty(newNp);
        });
      }
    }

    protected static DataProperty CreateDataProperty(StructuralType structuralType, PropertyInfo pInfo) {
      var propType = pInfo.PropertyType;
      var dp = new DataProperty(pInfo.Name);

      // TODO: handle isScalar
      if (typeof(IComplexObject).IsAssignableFrom(propType)) {
        dp.ComplexType = GetComplexType(propType);
        dp.IsNullable = false;
        // complex Objects do not have defaultValues currently
      } else {
        dp.ClrType = propType;
        dp.DataType = DataType.FromClrType(TypeFns.GetNonNullableType(propType));
        dp.IsNullable = TypeFns.IsNullableType(propType);
        dp.DefaultValue = dp.IsNullable ? null : dp.DataType.DefaultValue;
      }

      structuralType.AddDataProperty(dp);
      return dp;
    }

    protected static NavigationProperty CreateNavigationProperty(EntityType entityType, PropertyInfo pInfo ) {
      Type targetType;
      bool isScalar;
      if (pInfo.PropertyType.GenericTypeArguments.Any()) {
        targetType = pInfo.PropertyType.GenericTypeArguments[0];
        isScalar = false;
      } else {
        targetType = pInfo.PropertyType;
        isScalar = true;
      }

      var np = new NavigationProperty(pInfo.Name);
      
      np.IsScalar = isScalar;
      // np.EntityTypeName = TypeNameInfo.FromClrTypeName(targetType.FullName).Name;
      np.EntityType = GetEntityType(targetType);
      // may change later
      np.AssociationName = entityType.Name + "_" + np.Name;

      entityType.AddNavigationProperty(np);
      return np;
    }
  
  }

  /// <summary>
  /// 
  /// </summary>
  public class EntityTypeBuilder<TEntity> : StructuralTypeBuilder where TEntity:IEntity  {

    // TODO: also need a ComplexTypeBuilder;
    public EntityTypeBuilder() {
      EntityType = GetEntityType(typeof (TEntity));
    }

    public EntityType EntityType {
      get;
      protected set;
    }

    /// <summary>
    /// Returns null if not found
    /// </summary>
    /// <typeparam name="TValue"></typeparam>
    /// <param name="propExpr"></param>
    /// <returns></returns>

    public DataPropertyBuilder DataProperty<TValue>(Expression<Func<TEntity, TValue>> propExpr) {
      var pInfo = GetPropertyInfo(propExpr);
      var dp = EntityType.GetDataProperty(pInfo.Name);
      if (dp == null) {
        dp = CreateDataProperty(EntityType, pInfo);
      } 
      return new DataPropertyBuilder(dp);
    }


    public NavigationPropertyBuilder<TEntity, TTarget> NavigationProperty<TTarget>(
      Expression<Func<TEntity, TTarget>> propExpr) where TTarget:IEntity {
      var pInfo = GetPropertyInfo(propExpr);
      return GetNavPropBuilder<TTarget>(pInfo);
    }

    public NavigationPropertyBuilder<TEntity, TTarget> NavigationProperty<TTarget>(
      Expression<Func<TEntity, NavigationSet<TTarget>>> propExpr) where TTarget: IEntity {
      var pInfo = GetPropertyInfo(propExpr);
      return GetNavPropBuilder<TTarget>(pInfo);
    }

    private NavigationPropertyBuilder<TEntity, TTarget> GetNavPropBuilder<TTarget>(PropertyInfo pInfo) where TTarget : IEntity {
      var np = EntityType.GetNavigationProperty(pInfo.Name);
      if (np == null) {
        np = CreateNavigationProperty(EntityType, pInfo);
      }
      return new NavigationPropertyBuilder<TEntity, TTarget>(this, np);
    }

    internal PropertyInfo GetPropertyInfo<TValue>(Expression<Func<TEntity, TValue>> propExpr) {
      var lambda = propExpr as LambdaExpression;
      if (lambda == null) throw new ArgumentNullException("propExpr");
      var memberExpr = lambda.Body as MemberExpression;
      if (memberExpr == null) {
        throw new Exception("Unable to resolve property for: " + propExpr);
      }
      var pInfo = memberExpr.Member as PropertyInfo;
      if (pInfo == null) {
        throw new Exception("Unable to resolve " + propExpr + " as a property");
      }
      return pInfo;
    }
  }

  public class DataPropertyBuilder {
    public DataPropertyBuilder(DataProperty dp) {
      DataProperty = dp;
    }

    public DataPropertyBuilder IsNullable() {
      DataProperty.IsNullable = true;
      return this;
    }
    public DataPropertyBuilder IsRequired() {
      DataProperty.IsNullable = false;
      return this;
    }

    public DataPropertyBuilder IsPartOfKey(bool isPartOfKey = true) {
      DataProperty.IsPartOfKey = isPartOfKey;
      return this;
    }

    public DataPropertyBuilder IsAutoIncrementing(bool isAutoIncrementing = true) {
      DataProperty.IsAutoIncrementing = isAutoIncrementing;
      return this;
    }

    public DataPropertyBuilder DefaultValue(Object defaultValue) {
      DataProperty.DefaultValue = defaultValue;
      return this;
    }

    public DataPropertyBuilder ConcurrencyMode(ConcurrencyMode mode) {
      DataProperty.ConcurrencyMode = mode;
      return this;
    }

    public DataPropertyBuilder MaxLength(int? maxLength) {
      DataProperty.MaxLength = maxLength;
      return this;
    }

    public DataPropertyBuilder IsScalar(bool isScalar) {
      DataProperty.IsScalar = isScalar;
      return this;
    }

    public DataProperty DataProperty { get; private set; }
    }


  public class NavigationPropertyBuilder<TEntity, TTarget> where TEntity: IEntity where TTarget: IEntity {

    public NavigationPropertyBuilder(EntityTypeBuilder<TEntity> etb, NavigationProperty np) {
      _etb = etb;
      NavigationProperty = np;
    }

    public NavigationPropertyBuilder<TEntity, TTarget> HasForeignKey<TValue>(Expression<Func<TEntity, TValue>> propExpr) {
      if (!NavigationProperty.IsScalar) {
        throw new Exception("Can only call 'WithForeignKey' on a scalar NavigationProperty");
      }
      var dpb = _etb.DataProperty(propExpr);
      var fkProp = dpb.DataProperty;

      fkProp.RelatedNavigationProperty = NavigationProperty;
      return this;
    }

    // only needed in unusual cases.
    public NavigationPropertyBuilder<TEntity, TTarget> HasInverseForeignKey<TValue>(Expression<Func<TTarget, TValue>> propExpr) {
      var invEtb = new EntityTypeBuilder<TTarget>();
      var invDpBuilder = invEtb.DataProperty(propExpr);
      var invFkProp = invDpBuilder.DataProperty;
      
      invFkProp.InverseNavigationProperty = NavigationProperty;
      return this;
    }

    public NavigationPropertyBuilder<TEntity, TTarget> HasInverse(Expression<Func<TTarget, TEntity>> propExpr) {
      var invEtb = new EntityTypeBuilder<TTarget>();
      var invNp = invEtb.NavigationProperty(propExpr).NavigationProperty;
      return HasInverseCore(invNp);
      
    }

    public NavigationPropertyBuilder<TEntity, TTarget> HasInverse(Expression<Func<TTarget, NavigationSet<TEntity>>> propExpr) {
      var invEtb = new EntityTypeBuilder<TTarget>();
      var invNp = invEtb.NavigationProperty(propExpr).NavigationProperty;
      return HasInverseCore(invNp);
    }

    private NavigationPropertyBuilder<TEntity, TTarget> HasInverseCore(NavigationProperty invNp) {
      NavigationProperty.Inverse = invNp;
      invNp.Inverse = NavigationProperty;
      invNp.AssociationName = NavigationProperty.AssociationName;
      return this;
    }

    private readonly EntityTypeBuilder<TEntity> _etb;
    public NavigationProperty NavigationProperty { get; private set; }
  }
}
