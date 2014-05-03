using Breeze.Sharp.Core;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Breeze.Sharp {

  /// <summary>
  /// An instance of the MetadataStore contains all of the metadata about a collection of <see cref="EntityType"/>s 
  /// and <see cref="ComplexType"/>s.
  /// The MetadataStore.Instance property returns a singleton MetadataStore that is shared by all EntityManagers.
  /// This class is threadsafe meaning that single MetadataStore.Instance value may be shared across multiple threads. 
  /// This is NOT true of most other instances of classes within Breeze.
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
      RegisterTypeDiscoveryActionCore(typeof(NamingConvention), (t) => RegisterNamingConvention(t), true);
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

    /// <summary>
    /// Returns a list of all of the <see cref="EntityType"/>s within this store.
    /// </summary>
    public ICollection<EntityType> EntityTypes {
      get {
        lock (_structuralTypes) {
          return _structuralTypes.OfType<EntityType>().ToList();
        }
      }
    }

    /// <summary>
    /// Returns a list of all of the <see cref="ComplexType"/>s within this store.
    /// </summary>
    public ICollection<ComplexType> ComplexTypes {
      get {
        lock (_structuralTypes) {
          return _structuralTypes.OfType<ComplexType>().ToList();
        }
      }
    }

    /// <summary>
    /// The NamingConvention associated with this MetadataStore.
    /// </summary>
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

    /// <summary>
    /// Allowed types of metadata mismatches.
    /// </summary>
    public MetadataMismatchType AllowedMetadataMismatchTypes {
      get; set;
    }

    /// <summary>
    /// Fired whenever an entity's state is changing in any significant manner.
    /// </summary>
    public event EventHandler<MetadataMismatchEventArgs> MetadataMismatch;

    internal Message OnMetadataMismatch(String entityTypeName, String propertyName, MetadataMismatchType mmType, String detail = null) {
      EventHandler<MetadataMismatchEventArgs> handler = MetadataMismatch;
      var allow = (AllowedMetadataMismatchTypes & mmType) > 0;
      var args = new MetadataMismatchEventArgs() {
        StructuralTypeName = entityTypeName,
        PropertyName = propertyName,
        MetadataMismatchType = mmType,
        Detail = detail,
        Allow = allow
      };
      if (handler != null) {
        try {
          handler(this, args);
          allow = args.Allow;
        }
        catch {
          // Eat any handler exceptions but throw later. 
          allow = false;
        }
      }
      // don't allow NotAllowable thru no matter what the dev says.
      allow = allow && (MetadataMismatchType.NotAllowable & mmType) == 0;
      return AddMessage(args.Message, allow ? MessageType.Message : MessageType.Error, false);
    }

    internal Message AddMessage(String message, MessageType messageType, bool throwOnError = false) {
      lock (_lock) {
        var msg = new Message() { Text = message, MessageType = messageType };
        _messages.Add(msg);
        if (messageType == MessageType.Error && throwOnError) {
          throw new Exception(message);
        }
        return msg;
      }
    }

    public IEnumerable<String> GetMessages(MessageType messageType = MessageType.All) {
      lock (_lock) {
        return _messages.ToList().Where(m => (m.MessageType & messageType) > 0).Select(m => m.Text);
      }
    }

    #endregion

    #region Public methods

    public static void __Reset() {
      lock (__lock) {
        var x = __instance._probedAssemblies;
        __instance = new MetadataStore();
        // __instance.ProbeAssemblies(x.ToArray());
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

    /// <summary>
    /// Fetches the metadata for a specified 'service'. This method is automatically called 
    /// internally by an EntityManager before its first query against a new service.
    /// </summary>
    /// <param name="dataService"></param>
    /// <returns></returns>
    public async Task<DataService> FetchMetadata(DataService dataService) {
      String serviceName;
      
      serviceName = dataService.ServiceName;
      var ds = GetDataService(serviceName);
      if (ds != null) return dataService;

      await _asyncSemaphore.WaitAsync();
      String metadata;
      try {
        ds = GetDataService(serviceName);
        if (ds != null) return dataService;

        metadata = await dataService.GetAsync("Metadata");

      } catch (Exception e) {
        throw new Exception("Unable to locate metadata resource for: " + dataService.ServiceName, e);
      } finally {
        _asyncSemaphore.Release();
      }
      metadata = metadata.Trim();
      if (metadata.Substring(0, 1) == "\"" && metadata.Substring(metadata.Length - 1, 1) == "\"") {
        metadata = Regex.Unescape(metadata.Substring(1, metadata.Length - 2));
      }
      var metadataProcessor = new CsdlMetadataProcessor();
      metadataProcessor.ProcessMetadata(this, metadata);
      dataService.ServerMetadata = metadata;
      AddDataService(dataService);
      return dataService;

    }

    /// <summary>
    /// Returns the DataService for a specified service name or null.
    /// </summary>
    /// <param name="serviceName"></param>
    /// <returns></returns>
    public DataService GetDataService(String serviceName) {
      lock (_dataServiceMap) {
        if (_dataServiceMap.ContainsKey(serviceName)) {
          return _dataServiceMap[serviceName];
        } else {
          return null;
        }
      }
    }

    /// <summary>
    /// Adds a DataService to this MetadataStore. If a DataService with the same serviceName 
    /// is already in the MetadataStore an exception will be thrown unless the 'shouldOverwrite'
    /// parameter is set to 'true'.
    /// </summary>
    /// <param name="dataService"></param>
    /// <param name="shouldOverwrite"></param>
    public void AddDataService(DataService dataService, bool shouldOverwrite = false) {
      lock (_dataServiceMap) {
        if (_dataServiceMap.ContainsKey(dataService.ServiceName) && !shouldOverwrite) {
          throw new Exception("A dataService with this name '" + dataService.ServiceName + "' already exists in this MetadataStore");
        }
        _dataServiceMap[dataService.ServiceName] = dataService;
      }
    }

    /// <summary>
    /// Returns an EntityType given its CLR type.
    /// </summary>
    /// <param name="clrEntityType"></param>
    /// <param name="okIfNotFound"></param>
    /// <returns></returns>
    public EntityType GetEntityType(Type clrEntityType, bool okIfNotFound = false) {
      return GetStructuralType<EntityType>(clrEntityType, okIfNotFound);
    }

    /// <summary>
    /// Returns an EntityType given its name.
    /// </summary>
    /// <param name="etName"></param>
    /// <param name="okIfNotFound"></param>
    /// <returns></returns>
    public EntityType GetEntityType(String etName, bool okIfNotFound = false) {
      return GetStructuralType<EntityType>(etName, okIfNotFound);
    }

    /// <summary>
    /// Returns an ComplexType given its CLR type.
    /// </summary>
    /// <param name="clrComplexType"></param>
    /// <param name="okIfNotFound"></param>
    /// <returns></returns>
    public ComplexType GetComplexType(Type clrComplexType, bool okIfNotFound = false) {
      return GetStructuralType<ComplexType>(clrComplexType, okIfNotFound);
    }

    /// <summary>
    /// Returns an EntityType given its name.
    /// </summary>
    /// <param name="ctName"></param>
    /// <param name="okIfNotFound"></param>
    /// <returns></returns>
    public ComplexType GetComplexType(String ctName, bool okIfNotFound = false) {
      return GetStructuralType<ComplexType>(ctName, okIfNotFound);
    }


    internal T GetStructuralType<T>(Type clrType, bool okIfNotFound = false) where T : class {
      var stype = GetStructuralType(clrType, okIfNotFound);
      var ttype = stype as T;
      if (ttype != null) {
        return ttype;
      } else {
        if (okIfNotFound) return null;
        throw new Exception("Unable to find a matching " + typeof(T).Name + " for " + clrType.Name);
      }
    }

    /// <summary>
    /// Returns an StructuralType (EntityType or ComplexType) given its CLR type.
    /// </summary>
    /// <param name="clrType"></param>
    /// <param name="okIfNotFound"></param>
    /// <returns></returns>
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
        var msg = String.Format("Unable to find a matching EntityType or ComplexType for type: '{0}'",clrType.Name);
        throw new Exception(msg);
      }
    }

    
    /// <summary>
    /// Sets a resourceName for a specified clrType.
    /// </summary>
    /// <param name="resourceName"></param>
    /// <param name="clrType"></param>
    /// <param name="isDefault"></param>
    public void SetResourceName(String resourceName, Type clrType, bool isDefault = false) {
      var entityType = GetEntityType(clrType);
      SetResourceName(resourceName, entityType, isDefault);
    }


    internal void SetResourceName(String resourceName, EntityType entityType, bool isDefault = false) {
      lock (_defaultResourceNameMap) {
        _resourceNameEntityTypeMap[resourceName] = entityType;
        if (isDefault) {
          _defaultResourceNameMap[entityType] = resourceName;
        } else if (!_defaultResourceNameMap.ContainsKey(entityType)) {
          // isDefault ( by default) if no other resourceName is set for this entityType
          _defaultResourceNameMap[entityType] = resourceName;
        }
      }
    }

    /// <summary>
    /// Returns the EntityType for a specified resourceName.
    /// </summary>
    /// <param name="resourceName"></param>
    /// <param name="okIfNotFound"></param>
    /// <returns></returns>
    public EntityType GetEntityTypeForResourceName(String resourceName, bool okIfNotFound) {
      // by convention locking the _defaultResourceMap is a surrogate for also locking the _resourceNameEntityTypeMap;
      EntityType et;
      lock (_defaultResourceNameMap) {
        if (_resourceNameEntityTypeMap.TryGetValue(resourceName, out et)) {
          return et;
        } else if (okIfNotFound) {
          return null;
        } else {
          throw new Exception("Unable to locate a resource named: " + resourceName);
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
        // jo.AddPrimitive("namingConvention", this.NamingConvention.Name);
        jo.AddJNode("namingConvention", this.NamingConvention);
        // jo.AddProperty("localQueryComparisonOptions", this.LocalQueryComparisonOptions);
        jo.AddArray("dataServices", this._dataServiceMap.Values);
        jo.AddArray("structuralTypes", this._structuralTypes);
        jo.AddMap("resourceEntityTypeMap", this._resourceNameEntityTypeMap.ToDictionary(kvp => kvp.Key, kvp => kvp.Value.Name));
        return jo;
      }
    }

    private void DeserializeFrom(JNode jNode) {
      MetadataVersion = jNode.Get<String>("metadataVersion");
      // may be more than just a name
      
      var ncNode = jNode.GetJNode("namingConvention");
      var nc = FindOrCreateNamingConvention(ncNode);
      if (nc == null) {
        OnMetadataMismatch(null, null, MetadataMismatchType.MissingCLRNamingConvention, ncNode.ToString());
      } else {
        NamingConvention = nc;
      }
      
      // localQueryComparisonOptions
      jNode.GetJNodeArray("dataServices").Select(jn => new DataService(jn)).ForEach(ds => {
        if (GetDataService(ds.ServiceName) == null) {
          AddDataService(ds);
        }
      });
      jNode.GetJNodeArray("structuralTypes")
        .ForEach(UpdateStructuralTypeFromJNode);

      jNode.GetMap<String>("resourceEntityTypeMap").ForEach(kvp => {
        var et = GetEntityType(kvp.Value);
        SetResourceName(kvp.Key, et);
      });
    }

    private void UpdateStructuralTypeFromJNode(JNode jNode) {
      var shortName = jNode.Get<String>("shortName");
      var ns = jNode.Get<String>("namespace");
      var name = TypeNameInfo.ToStructuralTypeName(shortName, ns);
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
      return _validatorCache.FindOrCreate(jNode);
    }

    internal NamingConvention FindOrCreateNamingConvention(JNode jNode) {
      return _namingConventionCache.FindOrCreate(jNode);
    }

    internal Validator InternValidator(Validator validator) {
      return _validatorCache.Intern(validator);
    }

    internal NamingConvention InternNamingConvention(NamingConvention nc) {
      return _namingConventionCache.Intern(nc);
    }
    
    private void RegisterValidator(Type validatorType) {
      _validatorCache.Register(validatorType, Validator.Suffix);
    }

    private void RegisterNamingConvention(Type namingConventionType) {
      _namingConventionCache.Register(namingConventionType, NamingConvention.Suffix);
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
          throw MissingTypeException(typeName);
        }
      }
    }

    internal static Exception MissingTypeException(String typeName) {
      return new Exception("Unable to locate a CLR type corresponding to: " + typeName
          + ".  Consider calling MetadataStore.Instance.ProbeAssemblies with the assembly containing this " +
          "type when your application starts up.  In addition, if your namespaces are different between server and client " +
          "then you may need to call MetadataStore.Instance.NamingConvention.WithClientServerNamespaceMapping to create a" +
          "new NamingConvention that understands how to map between the two.");
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

    private class InternCache<T> where T : Internable {
      public readonly Dictionary<String, Type> TypeMap = new Dictionary<string, Type>();
      public readonly Dictionary<JNode, T> JNodeMap = new Dictionary<JNode, T>();

      internal T FindOrCreate(JNode jNode) {
        try {
          lock (TypeMap) {
            T internable;

            if (JNodeMap.TryGetValue(jNode, out internable)) {
              return internable;
            }

            internable = InternableFromJNode(jNode);
            JNodeMap[jNode] = internable;
            return internable;
          }
        } catch (Exception e) {
          throw new Exception("Unable to deserialize type: " + typeof(T).Name + " item: " + jNode);
        }
      }

      public T Intern(T internable) {
        if (internable.IsInterned) return internable;
        var jNode = internable.ToJNode();

        lock (TypeMap) {
          if (!TypeMap.ContainsKey(internable.Name)) {
            TypeMap[internable.Name] = internable.GetType();
          }
          T cachedInternable;
          if (JNodeMap.TryGetValue(jNode, out cachedInternable)) {
            cachedInternable.IsInterned = true;
            return (T)cachedInternable;
          } else {
            JNodeMap[jNode] = internable;
            internable.IsInterned = true;
            return internable;
          }
        }
      }

      public void Register(Type internableType, String defaultSuffix) {
        var ti = internableType.GetTypeInfo();
        if (ti.IsAbstract) return;
        if (ti.GenericTypeParameters.Length != 0) return;
        var key = UtilFns.TypeToSerializationName(internableType, defaultSuffix);
        lock (TypeMap) {
          TypeMap[key] = internableType;
        }
      }

      private T InternableFromJNode(JNode jNode) {
        var name = jNode.Get<String>("name");
        Type type;
        if (!TypeMap.TryGetValue(name, out type)) {
          return null;
        }
        // Deserialize the object
        var vr = (T)jNode.ToObject(type, true);
        return vr;
      }


    }


    #endregion

    #region Internal vars;

    internal static String ANONTYPE_PREFIX = "_IB_";

    #endregion

    #region Private vars

    private NamingConvention _namingConvention = new NamingConvention();

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

    private InternCache<Validator> _validatorCache = new InternCache<Validator>();
    private InternCache<NamingConvention> _namingConventionCache = new InternCache<NamingConvention>();

 
    private readonly Dictionary<String, Type> _namingConventionMap = new Dictionary<string, Type>();
    private readonly Dictionary<JNode, NamingConvention> _namingConventionJNodeCache = new Dictionary<JNode, NamingConvention>();

    private readonly List<Message> _messages = new List<Message>();


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

  internal class Message {
    public MessageType MessageType { get; set; }
    public String Text { get; set; }
    public bool IsError {
      get { return MessageType == MessageType.Error; }
    }
  }

  [Flags]
  public enum MessageType {
    Message = 1,
    Warning = 2,
    Error = 4,
    All = Message | Warning | Error
  }




}
