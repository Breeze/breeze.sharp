using Breeze.Sharp.Core;
using System;
using System.Diagnostics;
using System.Linq;

namespace Breeze.Sharp {

  /// <summary>
  /// 
  /// </summary>
  public class DataPropertyCollection : MapCollection<String, DataProperty> {
    protected override String GetKeyForItem(DataProperty item) {
      return item.Name;
    }
  }

  /// <summary>
  /// 
  /// </summary>
  [DebuggerDisplay("{Name} - {ParentType.Name}")]
  public class DataProperty : StructuralProperty, IJsonSerializable {
    public DataProperty() {
      IsScalar = true;
    }

    public DataProperty(DataProperty dp)
      : base(dp) {

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

    public DataProperty(JNode jNode) {
      Name = jNode.Get<String>("name");
      ComplexTypeName = jNode.Get<String>("complexTypeName");
      if (ComplexTypeName == null) {
        DataType = DataType.FromName(jNode.Get<String>("dataType"));
      } else {
        // may fail because ComplexType has not yet been defined - this will be resolved later in the 
        // MetadataStore.ResolveComplexTypeRefs method
        ComplexType = MetadataStore.Instance.GetComplexType(ComplexTypeName, true);
      }
      IsNullable = jNode.Get<bool>("isNullable", true);
      if (DataType != null) {
        DefaultValue = jNode.Get("defaultValue", DataType.ClrType);
      }
      IsPartOfKey = jNode.Get<bool>("isPartOfKey", false);
      IsUnmapped = jNode.Get<bool>("isUnmapped", false);
      IsAutoIncrementing = jNode.Get<bool>("isAutoIncrementing", false);
      ConcurrencyMode = (ConcurrencyMode)Enum.Parse(typeof(ConcurrencyMode), jNode.Get<String>("conncurrencyMode", ConcurrencyMode.None.ToString()));
      MaxLength = jNode.Get<int?>("maxLength");
      IsScalar = jNode.Get<bool>("isScalar", true);
      _validators = new ValidatorCollection(jNode.GetJNodeArray("validators"));
      EnumTypeName = jNode.Get<String>("enumType");

    }

    JNode IJsonSerializable.ToJNode(Object config) {
      var jn = new JNode();
      jn.AddPrimitive("name", this.Name);
      jn.AddPrimitive("dataType", this.DataType != null ? this.DataType.Name : null); 
      jn.AddPrimitive("complexTypeName", this.ComplexType != null ? this.ComplexType.Name : null );
      jn.AddPrimitive("isNullable", this.IsNullable, true);
      jn.AddPrimitive("defaultValue", this.DefaultValue );
      jn.AddPrimitive("isPartOfKey", this.IsPartOfKey, false);
      jn.AddPrimitive("isUnmapped", this.IsUnmapped, false);
      jn.AddPrimitive("isAutoIncrementing", this.IsAutoIncrementing, false);
      jn.AddPrimitive("concurrencyMode", this.ConcurrencyMode == ConcurrencyMode.None ? null : this.ConcurrencyMode.ToString());
      jn.AddPrimitive("maxLength", this.MaxLength);
      jn.AddPrimitive("isScalar", this.IsScalar, true);
      jn.AddArray("validators", this.Validators);
      jn.AddPrimitive("enumType", this.EnumTypeName);
      // jo.AddProperty("custom", this.Custom.ToJObject)
      return jn;
    }

    public DataType DataType { get; internal set; }
    public ComplexType ComplexType { get; internal set; }
    // only used during deserialization of JNodes
    internal String ComplexTypeName { get; set; }


    public override Type ClrType {
      get {
        if (_clrType == null && ( DataType != null || ComplexType != null)) {
          if (ComplexType != null) {
            _clrType = ComplexType.ClrType;
          } else if (DataType != null) {
            var rawClrType = DataType.ClrType;
            _clrType = IsNullable ? TypeFns.GetNullableType(rawClrType) : rawClrType;
          }
        }
        return _clrType;
      }
    }

    public bool IsNullable { get; internal set; }

    public bool IsAutoIncrementing { get; internal set; }

    public bool IsPartOfKey { get; internal set; }

    public Object DefaultValue { get; internal set; }

    public ConcurrencyMode ConcurrencyMode { get; internal set; }

    public Int64? MaxLength { get; internal set; }


    public String EnumTypeName { get; internal set; }
    public String RawTypeName { get; internal set; }

    public NavigationProperty InverseNavigationProperty { get; internal set; }
    public NavigationProperty RelatedNavigationProperty { get; internal set; } // only set if fk
    public bool IsForeignKey { get; internal set; } // may be set even if no RelatedNavigationProperty ( if unidirectional nav)

    public bool IsComplexProperty { get { return ComplexTypeName != null;}}
    public bool IsConcurrencyProperty { get { return ConcurrencyMode != ConcurrencyMode.None; } }
    public override bool IsDataProperty { get { return true; } }
    public override bool IsNavigationProperty { get { return false; } }

    private Type _clrType;
    
  }

  public enum ConcurrencyMode {
    None = 0,
    Fixed = 1
  }


}
