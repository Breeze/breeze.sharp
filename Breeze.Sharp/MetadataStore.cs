using Breeze.Sharp.Core;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;

namespace Breeze.Sharp {

  /// <summary>
  /// This class is ThreadSafe
  /// </summary>
  public class MetadataStore : IJsonSerializable {

    #region Ctor related 

    private MetadataStore() {
      _clrTypeMap = new ClrTypeMap(this);
      RegisterTypeDiscoveryActionCore(typeof(IEntity), (t) => {
        StructuralTypeBuilder.GetEntityType(t);
        _clrTypeMap.GetStructuralType(t);
      }, false);
      RegisterTypeDiscoveryActionCore(typeof(IComplexObject), (t) => _clrTypeMap.GetStructuralType(t), false);
      RegisterTypeDiscoveryActionCore(typeof(Validator), (t) => RegisterValidator(t), true);
    }


     // Explicit static constructor to tell C# compiler
    // not to mark type as beforefieldinit
    static MetadataStore() {     }

    public static MetadataStore Instance {
      get {
        return __instance;
      }
    }

    #endregion

    #region Public properties

    public static String MetadataVersion = "1.0.3";

    public List<EntityType> EntityTypes {
      get {
        lock (_structuralTypes) {
          return _structuralTypes.OfType<EntityType>().ToList();
        }
      }
    }

    public List<ComplexType> ComplexTypes {
      get {
        lock (_structuralTypes) {
          return _structuralTypes.OfType<ComplexType>().ToList();
        }
      }
    }

    public NamingConvention NamingConvention {
      get { 
        lock( _lock) {
          return _namingConvention;
        }
      }
      set {
        lock (_lock) {
          _namingConvention = value;
        }
      } 
    }

    public IEnumerable<Exception> Errors {
      get { return _errors.ToList(); }
    }

    #endregion

    #region Public methods

    public static void __Reset() {
      lock (__lock) {
        var x = __instance._probedAssemblies;
        __instance = new MetadataStore();
        __instance.ProbeAssemblies(x.ToArray());
      }
    }

    public bool ProbeAssemblies(params Assembly[] assembliesToProbe) {
      lock (_structuralTypes) {
        var assemblies = assembliesToProbe.Except(_probedAssemblies).ToList();
        if (assemblies.Any()) {
          assemblies.ForEach(asm => {
            _probedAssemblies.Add(asm);
            _typeDiscoveryActions.Where(tpl => tpl.Item3 == null || tpl.Item3(asm))
              .ForEach(tpl => {
                var type = tpl.Item1;
                var action = tpl.Item2;
                TypeFns.GetTypesImplementing(type, asm).ForEach(action);
              });
          });
          return true;
        } else {
          return false;
        }
      }
    }

    public void RegisterTypeDiscoveryAction(Type type, Action<Type> action) {
      RegisterTypeDiscoveryActionCore(type, action, false);
    }

    private void RegisterTypeDiscoveryActionCore(Type type, Action<Type> action, bool includeThisAssembly) {
      Func<Assembly, bool> shouldProcessAssembly = (a) => {
        return includeThisAssembly ? true : a != this.GetType().GetTypeInfo().Assembly;
      };
      _typeDiscoveryActions.Add(Tuple.Create(type, action, shouldProcessAssembly));
    }

    public async Task<DataService> FetchMetadata(DataService dataService) {
      String serviceName;
      
      serviceName = dataService.ServiceName;
      var ds = GetDataService(serviceName);
      if (ds != null) return dataService;

      await _asyncSemaphore.WaitAsync();

      try {
        ds = GetDataService(serviceName);
        if (ds != null) return dataService;

        var metadata = await dataService.GetAsync("Metadata");
        dataService.ServerMetadata = metadata;
        lock (_dataServiceMap) {
          _dataServiceMap[serviceName] = dataService;
        }
        var metadataProcessor = new CsdlMetadataProcessor();
        metadataProcessor.ProcessMetadata(this, metadata);

        return dataService;

      } finally {
        _asyncSemaphore.Release();
      }

    }

    public DataService GetDataService(String serviceName) {
      lock (_dataServiceMap) {
        if (_dataServiceMap.ContainsKey(serviceName)) {
          return _dataServiceMap[serviceName];
        } else {
          return null;
        }
      }
    }

    public EntityType GetEntityType(Type clrEntityType, bool okIfNotFound = false) {
      return GetStructuralType<EntityType>(clrEntityType, okIfNotFound);
    }

    public EntityType GetEntityType(String etName, bool okIfNotFound = false) {
      return GetStructuralType<EntityType>(etName, okIfNotFound);
    }

    public ComplexType GetComplexType(Type clrComplexType, bool okIfNotFound = false) {
      return GetStructuralType<ComplexType>(clrComplexType, okIfNotFound);
    }

    public ComplexType GetComplexType(String ctName, bool okIfNotFound = false) {
      return GetStructuralType<ComplexType>(ctName, okIfNotFound);
    }

    public T GetStructuralType<T>(Type clrType, bool okIfNotFound = false) where T : class {
      var stype = GetStructuralType(clrType, okIfNotFound);
      var ttype = stype as T;
      if (ttype != null) {
        return ttype;
      } else {
        if (okIfNotFound) return null;
        throw new Exception("Unable to find a matching " + typeof(T).Name + " for " + clrType.Name);
      }
    }

    public StructuralType GetStructuralType(Type clrType, bool okIfNotFound = false) {
      lock (_structuralTypes) {
        if (IsStructuralType(clrType)) {
          var stType = _clrTypeMap.GetStructuralType(clrType);
          if (stType != null) return stType;

          // Not sure if this is needed.
          if (ProbeAssemblies(new Assembly[] { clrType.GetTypeInfo().Assembly })) {
            stType = _clrTypeMap.GetStructuralType(clrType);
            if (stType != null) return stType;
          }
        }

        if (okIfNotFound) return null;
        throw new Exception("Unable to find a matching EntityType or ComplexType for " + clrType.Name);
      }
    }

    // TODO: think about name
    public void AddResourceName(String resourceName, Type clrType, bool isDefault = false) {
      var entityType = GetEntityType(clrType);
      AddResourceName(resourceName, entityType, isDefault);
    }

    internal void AddResourceName(String resourceName, EntityType entityType, bool isDefault = false) {
      lock (_defaultResourceNameMap) {
        _resourceNameEntityTypeMap[resourceName] = entityType;
        if (isDefault) {
          _defaultResourceNameMap[entityType] = resourceName;
        }
      }
    }

    /// <summary>
    /// Returns the default resource name for the specified CLR type.
    /// </summary>
    /// <param name="clrType"></param>
    /// <returns></returns>
    public String GetDefaultResourceName(Type clrType) {
      var entityType = GetEntityType(clrType);
      return GetDefaultResourceName(entityType);
    }

    /// <summary>
    /// Returns the default resource name for the specified EntityType type.
    /// </summary>
    /// <param name="entityType"></param>
    /// <returns></returns>
    public  string GetDefaultResourceName(EntityType entityType) {
      lock (_defaultResourceNameMap) {
        String resourceName = null;
        // give the type it's base's resource name if it doesn't have its own.
        if (!_defaultResourceNameMap.TryGetValue(entityType, out resourceName)) {
          var baseEntityType = entityType.BaseEntityType;
          if (baseEntityType != null) {
            return GetDefaultResourceName(baseEntityType);
          }
        }
        return resourceName;
      }
    }

    /// <summary>
    /// Returns whether the specified CLR type is either an IEntity or a IComplexObject.
    /// </summary>
    /// <param name="clrType"></param>
    /// <returns></returns>
    public static bool IsStructuralType(Type clrType) {
      return typeof(IStructuralObject).IsAssignableFrom(clrType);
    }

    #endregion

    #region Import/Export metadata

    /// <summary>
    /// Exports metadata as a string.
    /// </summary>
    /// <returns></returns>
    public String ExportMetadata() {
      return ((IJsonSerializable)this).ToJNode(null).Serialize();
    }

    /// <summary>
    /// Exports metadata via a TextWriter.
    /// </summary>
    /// <param name="textWriter"></param>
    /// <returns></returns>
    public TextWriter ExportMetadata(TextWriter textWriter) {
      return ((IJsonSerializable)this).ToJNode(null).SerializeTo(textWriter);
    }

    /// <summary>
    /// Imports metadata from a string.
    /// </summary>
    /// <param name="metadata"></param>
    public void ImportMetadata(String metadata) {
      var jNode = JNode.DeserializeFrom(metadata);
      ImportMetadata(jNode);
    }

    /// <summary>
    /// Imports metadata via a TextReader.
    /// </summary>
    /// <param name="textReader"></param>
    public void ImportMetadata(TextReader textReader) {
      var jNode = JNode.DeserializeFrom(textReader);
      ImportMetadata(jNode);
    }

    internal void ImportMetadata(JNode jNode ) {

      DeserializeFrom(jNode);
      EntityTypes.ForEach(et => ResolveComplexTypeRefs(et));
    }

    private void ResolveComplexTypeRefs(EntityType et) {
      et.ComplexProperties.Where(cp => cp.ComplexType == null)
        .ForEach(cp => cp.ComplexType = GetComplexType(cp.ComplexType.Name));
    }

    JNode IJsonSerializable.ToJNode(Object config) {
      lock (_structuralTypes) {
        var jo = new JNode();
        jo.AddPrimitive("metadataVersion", MetadataVersion);
        // jo.Add("name", this.Name);
        jo.AddPrimitive("namingConvention", this.NamingConvention.Name);
        // jo.AddProperty("localQueryComparisonOptions", this.LocalQueryComparisonOptions);
        jo.AddArray("dataServices", this._dataServiceMap.Values);
        jo.AddArray("structuralTypes", this._structuralTypes);
        jo.AddMap("resourceEntityTypeMap", this._resourceNameEntityTypeMap.ToDictionary(kvp => kvp.Key, kvp => kvp.Value.Name));
        return jo;
      }
    }

    private void DeserializeFrom(JNode jNode) {
      MetadataVersion = jNode.Get<String>("metadataVersion");
      // Name
      NamingConvention = NamingConvention.FromName(jNode.Get<String>("namingConvention"));
      // localQueryComparisonOptions
      jNode.GetJNodeArray("dataServices").Select(jn => new DataService(jn)).ForEach(ds => {
        if (!_dataServiceMap.ContainsKey(ds.ServiceName)) {
          _dataServiceMap.Add(ds.ServiceName, ds);
        }
      });
      jNode.GetJNodeArray("structuralTypes")
        .ForEach(UpdateStructuralTypeFromJNode);

      jNode.GetMap<String>("resourceEntityTypeMap").ForEach(kvp => {
        var et = GetEntityType(kvp.Value);
        AddResourceName(kvp.Key, et);
      });
    }

    private void UpdateStructuralTypeFromJNode(JNode jNode) {
      var shortName = jNode.Get<String>("shortName");
      var ns = jNode.Get<String>("namespace");
      var name = TypeNameInfo.QualifyTypeName(shortName, ns);
      var isComplexType = jNode.Get<bool>("isComplexType", false);
      if (isComplexType) {
        var ct = MetadataStore.Instance.GetComplexType(name);
        ct.UpdateFromJNode(jNode);
      } else {
        var et = MetadataStore.Instance.GetEntityType(name);
        et.UpdateFromJNode(jNode);
      }
    }

    #endregion

    #region Validator methods

    internal Validator FindOrCreateValidator(JNode jNode) {
      lock (_validatorMap) {
        Validator vr;

        if (_validatorJNodeCache.TryGetValue(jNode, out vr)) {
          return vr;
        }

        vr = ValidatorFromJNode(jNode);
        _validatorJNodeCache[jNode] = vr;
        return vr;
      }
    }

    internal T InternValidator<T>(T validator) where T : Validator {
      if (validator.IsInterned) return validator;
      var jNode = validator.ToJNode();

      lock (_validatorMap) {
        if (!_validatorMap.ContainsKey(validator.Name)) {
          _validatorMap[validator.Name] = validator.GetType();
        }
        Validator cachedValidator;
        if (_validatorJNodeCache.TryGetValue(jNode, out cachedValidator)) {
          cachedValidator.IsInterned = true;
          return (T)cachedValidator;
        } else {
          _validatorJNodeCache[jNode] = validator;
          validator.IsInterned = true;
          return validator;
        }
      }
    }

    private void RegisterValidator(Type validatorType) {
      var ti = validatorType.GetTypeInfo();
      if (ti.IsAbstract) return;
      if (ti.GenericTypeParameters.Length != 0) return;
      var key = Validator.TypeToValidatorName(validatorType);
      lock (_validatorMap) {
        _validatorMap[key] = validatorType;
      }
    }

    private Validator ValidatorFromJNode(JNode jNode) {
      var vrName = jNode.Get<String>("name");
      Type vrType;
      if (!_validatorMap.TryGetValue(vrName, out vrType)) {
        var e = new Exception("Unable to create a validator for " + vrName);
        _errors.Add(e);
        return null;
      }
      // Deserialize the object
      var vr = (Validator)jNode.ToObject(vrType, true);
      return vr;
    }

    #endregion

    #region Internal and Private

    internal Type GetClrTypeFor(StructuralType stType) {
      lock (_structuralTypes) {
        return _clrTypeMap.GetClrType(stType);
      }
    }

    // T is either <ComplexType> or <EntityType>
    private T GetStructuralType<T>(String typeName, bool okIfNotFound = false) where T : class {
      lock (_structuralTypes) {
        var t = _structuralTypes[typeName];
        if (t == null) {
          // locate by short name if not found by full name;
          t = _structuralTypes.FirstOrDefault(st => st.ShortName == typeName);
        }
        if (t != null) {
          var result = t as T;
          if (result == null) {
            throw new Exception("A type by this name exists but is not a " + typeof(T).Name);
          }
          return result;
        }  else if (okIfNotFound) {
          return (T)null;
        } else {
          throw new Exception("Unable to locate Type: " + typeName);
        }
      }
    }

    internal EntityType AddEntityType(EntityType entityType) {
      AddStructuralType(entityType);
      return entityType;
    }

    internal ComplexType AddComplexType(ComplexType complexType) {
      AddStructuralType(complexType);
      return complexType;
    }

    private void AddStructuralType(StructuralType stType, bool allowMerge = true) {
      // don't register anon types
      if (stType.IsAnonymous) return;

      lock (_structuralTypes) {
        if (_structuralTypes.ContainsKey(stType.Name)) {
          throw new Exception("Type " + stType.Name + " already exists in this MetadataStore.");
        }

        _clrTypeMap.GetClrType(stType);
        _structuralTypes.Add(stType);
        _shortNameMap[stType.ShortName] = stType.Name;

      }
    }
    
    #endregion

    #region Inner classes 

    // inner class
    internal class ClrTypeMap {
      public ClrTypeMap(MetadataStore metadataStore) {
        _metadataStore = metadataStore;
      }

      public StructuralType GetStructuralType(Type clrType) {
        var stName = TypeNameInfo.FromClrTypeName(clrType.FullName).Name;
        TypePair tp;
        if (_map.TryGetValue(stName, out tp)) {
          return tp.StructuralType;
        } else {
          _map.Add(stName, new TypePair() { ClrType = clrType });
          return null;
        }
      }

      public Type GetClrType(StructuralType stType) {
        TypePair tp;
        if (_map.TryGetValue(stType.Name, out tp)) {
          stType.ClrType = tp.ClrType;
          if (tp.StructuralType == null) {
            tp.StructuralType = stType;
          }
          return tp.ClrType;
        } else {
          _map.Add(stType.Name, new TypePair() { StructuralType = stType });
          return null;
        }
      }

      private readonly MetadataStore _metadataStore;
      private readonly Dictionary<String, TypePair> _map = new Dictionary<String, TypePair>();

      [DebuggerDisplay("{ClrType.FullName} - {StructuralType.Name}")]
      private class TypePair {
        public Type ClrType;
        public StructuralType StructuralType;
      }
    }

    #endregion

    #region Internal vars;

    internal static String ANONTYPE_PREFIX = "_IB_";

    #endregion

    #region Private vars

    private NamingConvention _namingConvention = NamingConvention.Default;

    private static MetadataStore __instance = new MetadataStore();
    private static readonly Object __lock = new Object();
    
    private readonly AsyncSemaphore _asyncSemaphore = new AsyncSemaphore(1);
    private readonly Object _lock = new Object();

    // lock using _dataServiceMap
    private readonly Dictionary<String, DataService> _dataServiceMap = new Dictionary<String, DataService>();
    
    // locked using _structuralTypes
    private readonly ClrTypeMap _clrTypeMap;
    private readonly HashSet<Assembly> _probedAssemblies = new HashSet<Assembly>();
    private readonly List<Tuple<Type, Action<Type>, Func<Assembly, bool>>> _typeDiscoveryActions = new List<Tuple<Type, Action<Type>, Func<Assembly, bool>>>();

    private readonly StructuralTypeCollection _structuralTypes = new StructuralTypeCollection();
    private readonly Dictionary<String, String> _shortNameMap = new Dictionary<string, string>();
    

    // locked using _resourceNameEntityTypeMap
    private readonly Dictionary<EntityType, String> _defaultResourceNameMap = new Dictionary<EntityType, string>();
    private readonly Dictionary<String, EntityType> _resourceNameEntityTypeMap = new Dictionary<string, EntityType>();

    // validator related. - both locked using _validatorMap
    private readonly Dictionary<String, Type> _validatorMap = new Dictionary<string, Type>();
    private readonly Dictionary<JNode, Validator> _validatorJNodeCache = new Dictionary<JNode, Validator>();
    

    private readonly List<Exception> _errors = new List<Exception>();

    #endregion

    #region Unfinished or removed ideas

    //// Not needed because of Initialize() method on each Entity/ComplexObject
    //public void RegisterTypeInitializer(Type type, Action<Object> action) {
    //  lock (_typeInitializerMap) {
    //    if (action!=null) {
    //      _typeInitializerMap[type] = action;
    //    } else {
    //      if (_typeInitializerMap.ContainsKey(type)) {
    //        _typeInitializerMap.Remove(type);
    //      }
    //    }
    //    var st = GetStructuralType(type, true);
    //    if (st != null) {
    //      st.InitializerAction = action;
    //    }
    //  }
    //}

    // private Dictionary<Type, Action<Object>> _typeInitializerMap = new Dictionary<Type, Action<object>>();

    #endregion
  }







}
