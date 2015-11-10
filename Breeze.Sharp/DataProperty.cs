using System.Reflection;
using Breeze.Sharp.Core;
using System;
using System.Diagnostics;
using System.Linq;
using System.Collections.Generic;

using Microsoft.Data.Edm.Library;

namespace Breeze.Sharp {

  /// <summary>
  /// For internal use only. Unique collection of DataProperties.
  /// </summary>
  internal class DataPropertyCollection : MapCollection<String, DataProperty> {
    protected override String GetKeyForItem(DataProperty item) {
      return item.Name;
    }
  }

  /// <summary>
  /// A DataProperty describes the metadata for a single property of an <see cref="EntityType"/> that contains simple data.
  /// Instances of the DataProperty class are constructed automatically during assembly probing and 
  /// and then updated via Metadata retrieval from an entity server. Itt is also possible to 
  /// update/extend them directly on the client.
  /// </summary>
  [DebuggerDisplay("{Name} - {ParentType.Name}")]
  public class DataProperty : StructuralProperty, IJsonSerializable {

    internal DataProperty(String name)
      : base(name) {
      IsScalar = true;

    }

    // only used to create an inherited property
    internal DataProperty(DataProperty dp)
      : base(dp) {

      this._clrType = dp.ClrType;
      this.DataType = dp.DataType;
      this.DefaultValue = dp.DefaultValue;
      this.IsNullable = dp.IsNullable;
      this.IsPartOfKey = dp.IsPartOfKey;
      this.IsForeignKey = dp.IsForeignKey;
      this.ConcurrencyMode = dp.ConcurrencyMode;
      this.IsUnmapped = dp.IsUnmapped;
      this.IsAutoIncrementing = dp.IsAutoIncrementing;
      this.IsScalar = dp.IsScalar;
      this.ComplexType = dp.ComplexType;
      this.MaxLength = dp.MaxLength;
      this.EnumTypeName = dp.EnumTypeName;
      this.RawTypeName = dp.RawTypeName;

    }

    internal void UpdateFromJNode(JNode jNode, bool isFromServer) {
      var complexTypeName = MetadataStore.GetStructuralTypeNameFromJNode(jNode, "complexTypeName", isFromServer);
      if (complexTypeName == null) {
        Check(DataType, DataType.FromName(jNode.Get<String>("dataType")), "DataType");
      } else {
        Check(ComplexType.Name, complexTypeName, "ComplexTypeName");
      }
      Check(this.IsScalar, jNode.Get<bool>("isScalar", true), "IsScalar");

      IsNullable = jNode.Get<bool>("isNullable", true);
      if (DataType != null) {
        DefaultValue = jNode.Get("defaultValue", DataType.ClrType);
        if (DefaultValue == null && !IsNullable) {
          DefaultValue = DataType.DefaultValue;
        }
      }
      IsPartOfKey = jNode.Get<bool>("isPartOfKey", false);
      IsUnmapped = jNode.Get<bool>("isUnmapped", false);
      IsAutoIncrementing = jNode.Get<bool>("isAutoIncrementing", false);
      ConcurrencyMode = (ConcurrencyMode)Enum.Parse(typeof(ConcurrencyMode), jNode.Get<String>("concurrencyMode", ConcurrencyMode.None.ToString()));
      MaxLength = jNode.Get<int?>("maxLength");
      // EnumType is best determined on the client and not from the server
      // so for now just ignore the 'enumType'
      // var enumTypeName = jNode.Get<String>("enumType");
      
      _validators = new ValidatorCollection(jNode.GetJNodeArray("validators"));
    }

    JNode IJsonSerializable.ToJNode(Object config) {
      var jn = new JNode();
      jn.AddPrimitive("name", this.Name);
      jn.AddPrimitive("dataType", this.DataType != null ? this.DataType.Name : null);
      jn.AddPrimitive("complexTypeName", this.ComplexType != null ? this.ComplexType.Name : null);
      jn.AddPrimitive("isNullable", this.IsNullable, true);
      jn.AddPrimitive("defaultValue", this.DefaultValue);
      jn.AddPrimitive("isPartOfKey", this.IsPartOfKey, false);
      jn.AddPrimitive("isUnmapped", this.IsUnmapped, false);
      jn.AddPrimitive("isAutoIncrementing", this.IsAutoIncrementing, false);
      jn.AddPrimitive("concurrencyMode", this.ConcurrencyMode == ConcurrencyMode.None ? null : this.ConcurrencyMode.ToString());
      jn.AddPrimitive("maxLength", this.MaxLength);
      jn.AddPrimitive("isScalar", this.IsScalar, true);
      jn.AddArray("validators", this.Validators);
      jn.AddPrimitive("enumType", this.EnumTypeName);
      // jo.AddProperty("rawTypeName").isOptional()
      // jo.AddProperty("custom", this.Custom.ToJObject)
      return jn;           
    }

    /// <summary>
    /// The DataType for this property. This will be null for a ComplexType DataProperty.
    /// </summary>
    public DataType DataType { get; internal set; }

    /// <summary>
    /// The ComplexType for this property. This will be null for a simple DataProprty.
    /// </summary>
    public ComplexType ComplexType {
      get { return _complexType; }
      set {
        if (_complexType == value) return;
        if (ParentType != null) {
          throw new Exception("The 'ComplexType' property must be set before a DataProperty is added to its parent.");
        }
        _complexType = value;
        if (value != null) {
          ClrType = value.ClrType;
        }
      }
    }

    /// <summary>
    /// The CLR type for this property.
    /// </summary>
    public override Type ClrType {
      get {
        return _clrType;
      }
      internal set {
        if (_clrType == value) return;
        if (ParentType != null) {
          throw new Exception("The 'ClrType' property must be set before a DataProperty is added to its parent.");
        }
        _clrType = value;
        if (typeof(IComplexObject).IsAssignableFrom(_clrType)) {
          DataType = null;
        }
      }
    }

    /// <summary>
    /// Whether this property is nullable.
    /// </summary>
    public bool IsNullable {
      get { return _isNullable; }
      set {
        if (value == _isNullable) return;
        if (this.ParentType == null || this.ParentType is ComplexType) {
          _isNullable = value;
          return;
        }

        if (value) {
          // Per discussion with Steve - NH allows complex objects to be null.
          // So Breeze will still keep them nonnullable but will allow null
          // values to be sent from the server which will then get absorbed
          // into the existing complex instance with all null values.
          if (ComplexType != null) {
            return;
          }
          //if (ComplexType != null) {
          //  throw new Exception("Metadata mismatch: A ComplexProperty cannot be made nullable: " +
          //    this.ParentType.Name + "." + this.Name);
          //}
          if (ClrType.GetTypeInfo().IsValueType && !TypeFns.IsNullableType(ClrType)) {
            throw new Exception("Metadata mismatch: This property cannot be made nullable because it has a nonnullable clr type: " +
              this.ParentType.Name + "." + this.Name);
          }
        }

        SelfAndSubtypeDps.ForEach(dp => {
          dp._isNullable = value;
        });
      }
    }

    /// <summary>
    /// Whether this property is autoincrementing.
    /// </summary>
    public bool IsAutoIncrementing {
      get {
        return _isAutoIncrementing;
      }
      set {
        if (value == _isAutoIncrementing) return;
        if (this.ParentType == null) {
          _isAutoIncrementing = value;
          return;
        }
        InsureEntityType("IsAutoIncrementing");

        SelfAndSubtypeDps.ForEach(dp => {
          dp._isAutoIncrementing = value;
          if (dp.EntityType.KeyProperties.Count() == 1) {
            if (!value) dp.EntityType.AutoGeneratedKeyType = AutoGeneratedKeyType.None;
            else if (dp.EntityType.AutoGeneratedKeyType == AutoGeneratedKeyType.None)
              dp.EntityType.AutoGeneratedKeyType = AutoGeneratedKeyType.Identity;
          }
        });

      }
    }

    /// <summary>
    /// Whether this property is part of this entity type's key.
    /// </summary>
    public bool IsPartOfKey {
      get { return _isPartOfKey; }
      set {
        if (value == _isPartOfKey) return;
        if (this.ParentType == null) {
          _isPartOfKey = value;
          return;
        }

        InsureEntityType("IsPartOfKey");

        SelfAndSubtypeDps.ForEach(dp => {
          dp._isPartOfKey = value;
          dp.EntityType.UpdateKeyProperties(dp);
        });

        IsNullable = false;
      }
    }

    /// <summary>
    /// Whether this property is an Enum type.
    /// </summary>
    public bool IsEnumType {
      get { return !String.IsNullOrEmpty(EnumTypeName); }
    }

    public Object DefaultValue {
      get { return _defaultValue; }
      internal set {
        // TODO: check if valid;
        if (value == _defaultValue) return;
        if (this.ParentType == null || this.ParentType is ComplexType) {
          _defaultValue = value;
          return;
        }

        if (this.IsComplexProperty) {
          throw new Exception("Cannot set a DefaultValue on a ComplexProperty");
        }

        SelfAndSubtypeDps.ForEach(dp => {
          dp._defaultValue = value;
        });

      }
    }

    // may be set even if no RelatedNavigationProperty ( if unidirectional nav)
    public bool IsForeignKey {
      get { return _isForeignKey; }
      internal set {
        if (value == _isForeignKey) return;
        if (this.ParentType == null) {
          _isForeignKey = value;
          return;
        }

        InsureEntityType("IsForeignKey");

        SelfAndSubtypeDps.ForEach(dp => {
          dp._isForeignKey = value;
          dp.EntityType.UpdateForeignKeyProperties(dp);
        });
      }
    }

    public ConcurrencyMode ConcurrencyMode {
      get { return _concurrencyMode; }
      internal set {
        if (value == _concurrencyMode) return;
        if (this.ParentType == null) {
          _concurrencyMode = value;
          return;
        }

        InsureEntityType("ConcurrencyMode");

        SelfAndSubtypeDps.ForEach(dp => {
          dp._concurrencyMode = value;
          dp.EntityType.UpdateConcurrencyProperties(dp);
        });
      }
    }

    public Int64? MaxLength { get; internal set; }

    public String EnumTypeName { get; internal set; }
    public String RawTypeName { get; internal set; }

    public NavigationProperty InverseNavigationProperty {
      get { return _inverseNavigationProperty; }
      internal set {
        _inverseNavigationProperty = value;
        this.IsForeignKey = true;

        value.AddInvFkName(this.Name);
      }
    }

    // only set if fk
    public NavigationProperty RelatedNavigationProperty {
      get { return _relatedNavigationProperty; }
      set {
        if (_relatedNavigationProperty == value) return;
        if (_relatedNavigationProperty != null) {
          throw new Exception("Cannot reset a RelatedNavigationProperty once its set");
        }

        _relatedNavigationProperty = value;
        this.IsForeignKey = true;
        value.EntityType.UpdateInverseForeignKeyProperties(this);
        value.UpdateWithRelatedDataProperty(this);

      }
    }

    private EntityType InsureEntityType(String propertyName) {
      var et = this.EntityType;
      if (et == null) {
        var msg = String.Format("Cannot set '{0}' on a property of a ComplexType", propertyName);
        throw new Exception(msg);
      }
      return et;
    }

    private EntityType EntityType {
      get { return this.ParentType as EntityType; }
    }

    private IEnumerable<DataProperty> SelfAndSubtypeDps {
      get {
        var entityType = this.ParentType as EntityType;
        if (entityType != null) {
          return new DataProperty[] { this }.Concat(entityType.SelfAndSubEntityTypes.Skip(1).Select(st => st.GetDataProperty(this.Name)));
        } else {
          // TODO: update this once we support inherited complexTypes
          return new DataProperty[] { this };
        }
      }
    }

    public bool IsComplexProperty { get { return ComplexType != null; } }
    public bool IsConcurrencyProperty { get { return ConcurrencyMode != ConcurrencyMode.None; } }
    public override bool IsDataProperty { get { return true; } }
    public override bool IsNavigationProperty { get { return false; } }

    private Type _clrType;
    private ComplexType _complexType;
    private ConcurrencyMode _concurrencyMode = ConcurrencyMode.None;
    private bool _isPartOfKey = false;
    private bool _isNullable = true;
    private bool _isAutoIncrementing = false;
    private bool _isForeignKey = false;
    private NavigationProperty _relatedNavigationProperty = null;
    private NavigationProperty _inverseNavigationProperty = null;
    private Object _defaultValue;
  }

  /// <summary>
  /// 
  /// </summary>
  public enum ConcurrencyMode {
    None = 0,
    Fixed = 1
  }


}
