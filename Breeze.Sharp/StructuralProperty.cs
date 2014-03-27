using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace Breeze.Sharp {

  /// <summary>
  /// 
  /// </summary>
  public class PropertyCollection : KeyedCollection<String, StructuralProperty> {
    protected override String GetKeyForItem(StructuralProperty item) {
      return item.Name;
    }
  }

  /// <summary>
  /// 
  /// </summary>
  public abstract class StructuralProperty  {
    protected StructuralProperty(String name) {
      Name = name;
      UpdateClientServerNames();
    }

    protected StructuralProperty(StructuralProperty prop) {
      this.IsInherited = true;
      this.Name = prop.Name;
      this.NameOnServer = prop.NameOnServer;
      this.Custom = prop.Custom;
      this.IsInherited = prop.IsInherited;
      this.IsScalar = prop.IsScalar;
      this.IsUnmapped = prop.IsUnmapped;
      this._validators = new ValidatorCollection(prop.Validators);
    }

    public StructuralType ParentType { get; internal set; }
    public abstract Type ClrType { get; internal set; }
    public String Name { get; internal set; }
    public String NameOnServer { get; internal set; }
    public bool IsScalar { get; internal set; }
    public bool IsInherited { get; internal set; }

    public bool IsUnmapped {
      get { return _isUnmapped; }
      set {
        if (_isUnmapped == value) return;
        if (this.IsNavigationProperty) {
          if (value) { 
            throw new Exception("Cannot set IsUnmapped on a NavigationProperty");
          }
          return;
        }
        _isUnmapped = value;
        ParentType.UpdateUnmappedProperties((DataProperty) this);
      }
    }

    public ICollection<Validator> Validators {
      get { return _validators; }
    }


    internal void Check(Object v1, Object v2, String name) {
      if (v1 == null && v2 == null) return;
      if (Object.Equals(v1, v2)) return;
      throw new Exception("StructuralType metadata mismatch. StructuralType: "  + this.ParentType.Name 
        + ".  StructuralProperty: " + this.Name 
        + ".  Property: " + name);
    }

    private void UpdateClientServerNames() {
      var nc = MetadataStore.Instance.NamingConvention;
      if (!String.IsNullOrEmpty(Name)) {
        NameOnServer = nc.TestPropertyName(Name, true);
      } else {
        Name = nc.TestPropertyName(NameOnServer, false);
      }
    }

    // TODO: enhance this later with DisplayName property and localization
    public String DisplayName {
      get { return this.Name; }
    }
    public Object Custom { get; internal set; }

    public abstract bool IsDataProperty { get;  }
    public abstract bool IsNavigationProperty { get; }

    private bool _isUnmapped = false;
    internal ValidatorCollection _validators = new ValidatorCollection();

  }


}
