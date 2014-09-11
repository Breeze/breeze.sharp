
using System.Linq;
using System.Threading;
using Breeze.Sharp.Core;
using System;
using System.Linq.Expressions;
using System.Reflection;

namespace Breeze.Sharp {

  /// <summary>
  /// Base class that provides a fluent interface for configuring the MetadataStore.
  /// </summary>
  public class StructuralTypeBuilder {

    public StructuralTypeBuilder(MetadataStore metadataStore) {
      MetadataStore = metadataStore;
    }

    public MetadataStore MetadataStore { get; private set; }


    internal StructuralType CreateStructuralType(Type clrType) {
      if (typeof(IEntity).IsAssignableFrom(clrType)) {
        return CreateEntityType(clrType);
      } else {
        return CreateComplexType(clrType);
      }
    }

    internal EntityType CreateEntityType(Type clrType ) {
      var typeInfo = clrType.GetTypeInfo();
      var baseEntityType = (typeInfo.BaseType == typeof(Object)) ? null : MetadataStore.GetEntityType(typeInfo.BaseType);
      
      var entityType = new EntityType(MetadataStore) {
        ClrType = clrType,
        BaseEntityType = baseEntityType
      };

      MetadataStore.AddEntityType(entityType);

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

    private ComplexType CreateComplexType(Type clrType) {
      var typeInfo = clrType.GetTypeInfo();
      var complexType = new ComplexType(MetadataStore) {
        ClrType = clrType
      };

      MetadataStore.AddComplexType(complexType);

      var baseComplexType = (typeInfo.BaseType == typeof(Object)) ? null : MetadataStore.GetComplexType(typeInfo.BaseType);
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

    private DataProperty CreateDataProperty(StructuralType structuralType, PropertyInfo pInfo) {
      var propType = pInfo.PropertyType;
      var dp = new DataProperty(pInfo.Name);

      // TODO: handle isScalar
      if (typeof(IComplexObject).IsAssignableFrom(propType)) {
        dp.ComplexType = MetadataStore.GetComplexType(propType);
        dp.IsNullable = false;
        // complex Objects do not have defaultValues currently
      } else {
        var nnType = TypeFns.GetNonNullableType(propType);
        dp.ClrType = propType;

        dp.DataType = DataType.FromClrType(nnType);
        dp.IsNullable = TypeFns.IsNullableType(propType);
        dp.DefaultValue = dp.IsNullable ? null : dp.DataType.DefaultValue;
        var isEnumType = nnType.GetTypeInfo().IsEnum;
        if (isEnumType) {
          dp.EnumTypeName = nnType.FullName;
        }
      }

      structuralType.AddDataProperty(dp);
      return dp;
    }

    private NavigationProperty CreateNavigationProperty(EntityType entityType, PropertyInfo pInfo ) {
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
      np.EntityType = MetadataStore.GetEntityType(targetType);
      // may change later
      np.AssociationName = entityType.Name + "_" + np.Name;

      entityType.AddNavigationProperty(np);
      return np;
    }

    

  }

  /// <summary>
  /// Provides a fluent interface for configuring an EntityType within the MetadataStore.
  /// </summary>
  public class EntityTypeBuilder<TEntity> : StructuralTypeBuilder where TEntity:IEntity  {

    // TODO: also need a ComplexTypeBuilder;
    public EntityTypeBuilder(MetadataStore metadataStore, bool checkOnly = false) : base(metadataStore) {
      EntityType = MetadataStore.GetEntityType(typeof (TEntity));
      CheckOnly = checkOnly;
    }

    public EntityType EntityType {
      get;
      protected set;
    }

    public bool CheckOnly {
      get; set;
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
        throw new ArgumentException("Unable to locate a DataProperty named: " + pInfo.Name);
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
        throw new ArgumentException("Unable to locate a NavigationProperty named: " + pInfo.Name);
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

  /// <summary>
  /// Provides a fluent interface for configuring an DataProperty within the MetadataStore.
  /// </summary>
  public class DataPropertyBuilder {
    internal DataPropertyBuilder(DataProperty dp ) {
      DataProperty = dp;
    }
    
    /// <summary>
    /// Used to defined that this DataProperty is nullable.
    /// </summary>
    /// <returns></returns>
    public DataPropertyBuilder IsNullable() {
      DataProperty.IsNullable = true;
      return this;
    }

    /// <summary>
    /// Used to defined that this DataProperty is required. ( The opposite of IsNullable. )
    /// </summary>
    /// <returns></returns>
    public DataPropertyBuilder IsRequired() {
      DataProperty.IsNullable = false;
      return this;
    }

    /// <summary>
    /// Used to defined that this DataProperty is part of this Entity's EntityKey.
    /// </summary>
    /// <param name="isPartOfKey"></param>
    /// <returns></returns>
    public DataPropertyBuilder IsPartOfKey(bool isPartOfKey = true) {
      DataProperty.IsPartOfKey = isPartOfKey;
      return this;
    }

    /// <summary>
    /// Used to defined that this DataProperty is autoIncrementing.
    /// </summary>
    /// <param name="isAutoIncrementing"></param>
    /// <returns></returns>
    public DataPropertyBuilder IsAutoIncrementing(bool isAutoIncrementing = true) {
      DataProperty.IsAutoIncrementing = isAutoIncrementing;
      return this;
    }

    /// <summary>
    /// Used to defined the default value for this DataProperty.
    /// </summary>
    /// <param name="defaultValue"></param>
    /// <returns></returns>
    public DataPropertyBuilder DefaultValue(Object defaultValue) {
      DataProperty.DefaultValue = defaultValue;
      return this;
    }

    /// <summary>
    /// Used to defined the ConcurrencyMode for this DataProperty.
    /// </summary>
    /// <param name="mode"></param>
    /// <returns></returns>
    public DataPropertyBuilder ConcurrencyMode(ConcurrencyMode mode) {
      DataProperty.ConcurrencyMode = mode;
      return this;
    }

    /// <summary>
    /// Used to defined the maximum string length for this DataProperty.
    /// </summary>
    /// <param name="maxLength"></param>
    /// <returns></returns>
    public DataPropertyBuilder MaxLength(int? maxLength) {
      DataProperty.MaxLength = maxLength;
      return this;
    }

    /// <summary>
    /// The DataProperty associated with this builder.
    /// </summary>
    public DataProperty DataProperty { get; private set; }

  }

  /// <summary>
  /// Provides a fluent interface for configuring an NavigatiomProperty within the MetadataStore.
  /// </summary>
  /// <typeparam name="TEntity"></typeparam>
  /// <typeparam name="TTarget"></typeparam>
  public class NavigationPropertyBuilder<TEntity, TTarget> where TEntity: IEntity where TTarget: IEntity {

    internal NavigationPropertyBuilder(EntityTypeBuilder<TEntity> etb, NavigationProperty np) {
      _etb = etb;
      NavigationProperty = np;
    }

    /// <summary>
    /// Used to define the foreign key property associated with this NavigationProperty.
    /// </summary>
    /// <typeparam name="TValue"></typeparam>
    /// <param name="propExpr"></param>
    /// <returns></returns>
    public NavigationPropertyBuilder<TEntity, TTarget> HasForeignKey<TValue>(Expression<Func<TEntity, TValue>> propExpr) {
      if (!NavigationProperty.IsScalar) {
        throw new Exception("Can only call 'WithForeignKey' on a scalar NavigationProperty");
      }
      var dpb = _etb.DataProperty(propExpr);
      var fkProp = dpb.DataProperty;

      fkProp.RelatedNavigationProperty = NavigationProperty;
      return this;
    }

    
    /// <summary>
    /// Used to define the 'inverse' foreign key property associated with this NavigationProperty. This method will only
    /// be needed in unusual cases.
    /// </summary>
    /// <typeparam name="TValue"></typeparam>
    /// <param name="propExpr"></param>
    /// <returns></returns>
    public NavigationPropertyBuilder<TEntity, TTarget> HasInverseForeignKey<TValue>(Expression<Func<TTarget, TValue>> propExpr) {
      var invEtb = new EntityTypeBuilder<TTarget>(NavigationProperty.EntityType.MetadataStore);
      var invDpBuilder = invEtb.DataProperty(propExpr);
      var invFkProp = invDpBuilder.DataProperty;
      
      invFkProp.InverseNavigationProperty = NavigationProperty;
      return this;
    }

    /// <summary>
    /// Used to define the scalar Inverse navigation property associated with this NavigationProperty.
    /// </summary>
    /// <param name="propExpr"></param>
    /// <returns></returns>
    public NavigationPropertyBuilder<TEntity, TTarget> HasInverse(Expression<Func<TTarget, TEntity>> propExpr) {
      var invEtb = new EntityTypeBuilder<TTarget>(NavigationProperty.EntityType.MetadataStore);
      var invNp = invEtb.NavigationProperty(propExpr).NavigationProperty;
      return HasInverseCore(invNp);
    }

    /// <summary>
    /// Used to define the nonscalar Inverse navigation property associated with this NavigationProperty.
    /// </summary>
    /// <param name="propExpr"></param>
    /// <returns></returns>
    public NavigationPropertyBuilder<TEntity, TTarget> HasInverse(Expression<Func<TTarget, NavigationSet<TEntity>>> propExpr) {
      var invEtb = new EntityTypeBuilder<TTarget>(NavigationProperty.EntityType.MetadataStore);
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

    /// <summary>
    /// The NavigationProperty associated with this builder.
    /// </summary>
    public NavigationProperty NavigationProperty { get; private set; }
  }
}
