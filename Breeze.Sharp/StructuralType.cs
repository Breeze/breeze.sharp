using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

using System.Reflection;
using Breeze.Core;
using System.Diagnostics;

namespace Breeze.Sharp {

  public class StructuralTypeCollection : MapCollection<String, StructuralType> {
    protected override String GetKeyForItem(StructuralType item) {
      return item.ShortName + ":#" + item.Namespace;
    }
  }

  [DebuggerDisplay("{Name}")]
  public abstract class StructuralType {
    public StructuralType() {
      Warnings = new List<string>();
      MetadataStore = MetadataStore.Instance;
    }

    // TODO: will be needed later when we have complexType inheritance 
    // public abstract StructuralType BaseStructuralType { get; }



    public MetadataStore MetadataStore { get; internal set; }

    public String Name { 
      get { return TypeNameInfo.QualifyTypeName(ShortName, Namespace); }
      internal set {
        var parts = TypeNameInfo.FromEntityTypeName(value);
        ShortName = parts.ShortName;
        Namespace = parts.Namespace;
        _nameOnServer = parts.ToServer().Name;
      }
    }

    public String NameOnServer {
      get {
        return _nameOnServer;
      }
      internal set {
        _nameOnServer = value;
        Name = TypeNameInfo.FromEntityTypeName(value).ToClient().Name;
      }
    }

    public Type ClrType {
      get {
        if (_clrType == null) {
          _clrType = MetadataStore.GetClrTypeFor(this);
          if (_clrType == null) {
            throw new Exception("Unable to locate a CLR type corresponding to: " + this.Name
              + ".  Consider calling MetadataStore.Instance.ProbeAssemblies with the assembly containing this " +
              "type when your application starts up.  In addition, if your namespaces are different between server and client " +
              "then you may need to call MetadataStore.Instance.NamingConvention.AddClientServerNamespaceMapping to " +
              "tell Breeze how to map between the two.");
          }
        }
        return _clrType;
      }
      internal set {
        _clrType = value;
      }  
    }
    private Type _clrType;
    public String ShortName { get; private set; }
    public String Namespace { get; private set;}
    public dynamic Custom { get; set; }
    public bool IsAbstract { get; internal set; }
    // TODO: determine if this is  still needed;
    public bool IsAnonymous { get; internal set; }
    public List<String> Warnings { get; internal set; }
    public abstract bool IsEntityType { get;  }
    
    public virtual  IEnumerable<StructuralProperty> Properties {
      get { return _dataProperties.Cast<StructuralProperty>(); }
    }

    public ICollection<DataProperty> DataProperties {
      get { return _dataProperties.ReadOnlyValues; }
    }

    public DataProperty GetDataProperty(String dpName) {
      return _dataProperties[dpName];
    }

    public virtual StructuralProperty GetProperty(String propName) {
      return _dataProperties[propName];
    }

    internal virtual DataProperty AddDataProperty(DataProperty dp) {
      dp.ParentType = this;
      UpdateClientServerName(dp);
      _dataProperties.Add(dp);

      if (dp.IsComplexProperty) {
        _complexProperties.Add(dp);
      }

      if (dp.IsUnmapped) {
        _unmappedProperties.Add(dp);
      }

      return dp;
    } 


    public ReadOnlyCollection<DataProperty> ComplexProperties {
      get { return _complexProperties.ReadOnlyValues; }
    }

    public ReadOnlyCollection<DataProperty> UnmappedProperties {
      get { return _unmappedProperties.ReadOnlyValues;  }
    }

    public ICollection<Validator> Validators {
      get { return _validators; }
    }

    internal void UpdateClientServerName(StructuralProperty property) {
      var nc = MetadataStore.NamingConvention;
      if (!String.IsNullOrEmpty(property.Name)) {
        property.NameOnServer = nc.TestPropertyName(property.Name, true);
      } else {
        property.Name = nc.TestPropertyName(property.NameOnServer, false);
      }
    }
      
    internal void UpdateClientServerFkNames(StructuralProperty property) {
      // TODO: add check for name roundtriping ( to see if ok)
      var nc = MetadataStore.NamingConvention;
      var navProp = property as NavigationProperty;
      if (navProp != null) {
        if (navProp._foreignKeyNames.Count > 0) {
          navProp._foreignKeyNamesOnServer = navProp._foreignKeyNames.Select(fkn => nc.TestPropertyName(fkn, true)).ToSafeList();
        } else {
          navProp._foreignKeyNames = navProp._foreignKeyNamesOnServer.Select(fkn => nc.TestPropertyName(fkn, false)).ToSafeList();
        }

        if (navProp._invForeignKeyNames.Count > 0) {
          navProp._invForeignKeyNamesOnServer = navProp._invForeignKeyNames.Select(fkn => nc.TestPropertyName(fkn, true)).ToSafeList();
        } else {
          navProp._invForeignKeyNames = navProp._invForeignKeyNamesOnServer.Select(fkn => nc.TestPropertyName(fkn, false)).ToSafeList();
        }
      }
    }

    protected String _nameOnServer;
    protected DataPropertyCollection _dataProperties = new DataPropertyCollection();
    protected SafeList<DataProperty> _complexProperties = new SafeList<DataProperty>();
    protected SafeList<DataProperty> _unmappedProperties = new SafeList<DataProperty>();
    protected ValidatorCollection _validators = new ValidatorCollection();

  }

  


}
