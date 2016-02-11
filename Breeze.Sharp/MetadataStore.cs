using Breeze.Sharp.Core;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace Breeze.Sharp {

  /// <summary>
  /// An instance of the MetadataStore contains all of the metadata about a collection of <see cref="EntityType"/>s 
  /// and <see cref="ComplexType"/>s.
  /// </summary>
  [DebuggerDisplay("StoreID: {StoreID}")]
  public class MetadataStore : IJsonSerializable {


     // Explicit static constructor to tell C# compiler
    // not to mark type as beforefieldinit
    static MetadataStore() {
      
    }

    public MetadataStore() {
      
      lock (__lock) {
        StoreID = __nextStoreID++;
      }
    }

    #region Public properties

    public int StoreID { get; private set; }

    public static String MetadataVersion = "1.0.3";

    public static MetadataStore Detached {
      get { return __detached; }
      set { __detached = value ?? new MetadataStore(); }
    }

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
      get { return _namingConvention; }
      set { _namingConvention = value; } 
    }

    /// <summary>
    /// Allowed types of metadata mismatches.
    /// </summary>
    public MetadataMismatchTypes AllowedMetadataMismatchTypes {
      get; set;
    }

    /// <summary>
    /// Fired whenever an entity's state is changing in any significant manner.
    /// </summary>
    public event EventHandler<MetadataMismatchEventArgs> MetadataMismatch;

    internal Message OnMetadataMismatch(String entityTypeName, String propertyName, MetadataMismatchTypes mmType, String detail = null) {
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
      allow = allow && (MetadataMismatchTypes.NotAllowable & mmType) == 0;
      return AddMessage(args.Message, allow ? MessageType.Message : MessageType.Error, false);
    }

    internal Message AddMessage(String message, MessageType messageType, bool throwOnError = false) {
      
      var msg = new Message() { Text = message, MessageType = messageType };
      _messages.Add(msg);
      if (messageType == MessageType.Error && throwOnError) {
        throw new Exception(message);
      }
      return msg;
      
    }

    public IEnumerable<String> GetMessages(MessageType messageType = MessageType.All) {
      return _messages.ToList().Where(m => (m.MessageType & messageType) > 0).Select(m => m.Text);
    }

    #endregion

    #region Public methods

    
    /// <summary>
    /// Fetches the metadata for a specified 'service'. This method is automatically called 
    /// internally by an EntityManager before its first query against a new service.
    /// </summary>
    /// <param name="dataService"></param>
    /// <returns></returns>
    public async Task<DataService> FetchMetadata(DataService dataService)
    {
        return await FetchMetadata(dataService, CancellationToken.None);
    }

    /// <summary>
    /// Fetches the metadata for a specified 'service'. This method is automatically called 
    /// internally by an EntityManager before its first query against a new service.
    /// </summary>
    /// <param name="dataService"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    public async Task<DataService> FetchMetadata(DataService dataService, CancellationToken cancellationToken) {
      String serviceName;

      serviceName = dataService.ServiceName;
      var ds = GetDataService(serviceName);
      if (ds != null) return dataService;

      await _asyncSemaphore.WaitAsync();

      cancellationToken.ThrowIfCancellationRequested();

      String metadata;
      try {
        ds = GetDataService(serviceName);
        if (ds != null) return dataService;

        metadata = await dataService.GetAsync("Metadata", cancellationToken);

        cancellationToken.ThrowIfCancellationRequested();
      }
      catch (Exception e)
      {
          if (!(e is TaskCanceledException))
            throw new Exception("Unable to locate metadata resource for: " + dataService.ServiceName, e);
          throw;
      } finally {
        _asyncSemaphore.Release();
      }
      metadata = metadata.Trim();
      // this section is needed if metadata is returned as an escaped string - ( not common but does happen ... ).
      if (metadata.Substring(0, 1) == "\"" && metadata.Substring(metadata.Length - 1, 1) == "\"") {
        metadata = Regex.Unescape(metadata.Substring(1, metadata.Length - 2));
      }

      var json = (JObject)JsonConvert.DeserializeObject(metadata);
      var schema = json["schema"];
      if (schema != null) {
        // metadata returned in CSDL format
        var metadataProcessor = new CsdlMetadataProcessor();
        metadataProcessor.ProcessMetadata(this, json);
      } else {
        // metadata returned in breeze native format
        this.ImportMetadata(metadata, true);
        
      }
      var errorMessages = GetMessages(MessageType.Error).ToList();
      if (errorMessages.Any()) {
        throw new Exception("Metadata errors encountered: \n" + errorMessages.ToAggregateString("\n"));
      }

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
    /// <returns></returns>
    public EntityType GetEntityType(Type clrEntityType) {
      return GetStructuralType<EntityType>(clrEntityType);
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
    /// <returns></returns>
    public ComplexType GetComplexType(Type clrComplexType ) {
      return GetStructuralType<ComplexType>(clrComplexType);
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

    // T is ComplexType or EntityType
    internal T GetStructuralType<T>(Type clrType) where T : class {
      var st = GetStructuralType(clrType);
      var result = st as T;
      if (result == null) {
        throw new Exception("A type by this name exists but is not a " + typeof(T).FullName);
      } 
      return result; 
    }

    // T is either <ComplexType> or <EntityType>
    private T GetStructuralType<T>(String typeName, bool okIfNotFound = false) where T : class {
      var st = GetStructuralTypeCore(typeName);
      if (st != null) {
        var result = st as T;
        if (result == null) {
          throw new Exception("A type by this name exists but is not a " + typeof(T).FullName);
        }
        return result;
      } else if (okIfNotFound) {
        return (T)null;
      } else {
        throw MissingTypeException(typeName);
      }
    }

    /// <summary>
    /// Returns an StructuralType (EntityType or ComplexType) given its CLR type.
    /// </summary>
    /// <param name="clrType"></param>
    /// <returns></returns>
    public StructuralType GetStructuralType(Type clrType) {
      if (!IsStructuralType(clrType)) {
        throw new ArgumentOutOfRangeException("clrType", "This type does not implement either IEntity or IComplexObject");
      }
      // not need for okIfNotFound because we will create a type if one isn't found.
      var stName = TypeNameInfo.FromClrType(clrType).StructuralTypeName;
      lock (_structuralTypes) {
        var st = _structuralTypes[stName];
        if (st == null) {
          var stb = new StructuralTypeBuilder(this);
          st = stb.CreateStructuralType(clrType);
          _structuralTypes[stName] = st;
        }
        return st;
      }
    }

    private StructuralType GetStructuralTypeCore(String stName) {
      lock (_structuralTypes) {
        if (!TypeNameInfo.IsQualifiedTypeName(stName)) {
          String fullStName;
          if (_shortNameMap.TryGetValue(stName, out fullStName)) {
            stName = fullStName;
          }
        }
        var st = _structuralTypes[stName];
        if (st == null) {
          var clrType = Configuration.Instance.GetClrType(stName);
          if (clrType == null) return null;
          var stb = new StructuralTypeBuilder(this);
          st = stb.CreateStructuralType(clrType);
          _structuralTypes[stName] = st;
        }
        return st;
      }
    }

    private StructuralType CreateStructuralType(Type clrType) {
      var stb = new StructuralTypeBuilder(this);
      var st = stb.CreateStructuralType(clrType);
      return st;
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
    /// <param name="isFromServer"></param>
    public void ImportMetadata(String metadata, bool isFromServer = false) {
      var jNode = JNode.DeserializeFrom(metadata);
      ImportMetadata(jNode, isFromServer);
    }

    /// <summary>
    /// Imports metadata via a TextReader.
    /// </summary>
    /// <param name="textReader"></param>
    /// <param name="isFromServer"></param>
    public void ImportMetadata(TextReader textReader, bool isFromServer = false) {
      var jNode = JNode.DeserializeFrom(textReader);
      ImportMetadata(jNode, isFromServer);
    }

    internal void ImportMetadata(JNode jNode, bool isFromServer ) {
      DeserializeFrom(jNode, isFromServer);
      EntityTypes.ForEach(et => {
        // cross entity/complex type fixup.
        et.UpdateNavigationProperties();
        et.ComplexProperties
          .Where(cp => cp.ComplexType == null)
          .ForEach(cp => cp.ComplexType = GetComplexType(cp.ComplexType.Name));
      });
     
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

    private void DeserializeFrom(JNode jNode, bool isFromServer) {
      MetadataVersion = jNode.Get<String>("metadataVersion");
      // may be more than just a name
      
      var ncNode = jNode.GetJNode("namingConvention");
      if (ncNode != null) {
        var nc = Configuration.Instance.FindOrCreateNamingConvention(ncNode);
        if (nc == null) {
          OnMetadataMismatch(null, null, MetadataMismatchTypes.MissingCLRNamingConvention, ncNode.ToString());
        } else {
          // keep any preexisting ClientServerNamespaceMap info
          NamingConvention = nc.WithClientServerNamespaceMapping(this.NamingConvention.ClientServerNamespaceMap);
        }
      }

      // localQueryComparisonOptions
      jNode.GetJNodeArray("dataServices").Select(jn => new DataService(jn)).ForEach(ds => {
        if (GetDataService(ds.ServiceName) == null) {
          AddDataService(ds);
        }
      });
      jNode.GetJNodeArray("structuralTypes")
        .ForEach(jn => UpdateStructuralTypeFromJNode(jn, isFromServer));

      jNode.GetMap<String>("resourceEntityTypeMap").ForEach(kvp => {
        var stName = kvp.Value;
        if (isFromServer) {
            stName = TypeNameInfo.FromStructuralTypeName(stName).ToClient(this).StructuralTypeName;
        }
        // okIfNotFound because metadata may contain refs to types that were already excluded earlier in
        // UpdateStructuralTypeFromJNode
        var et = GetEntityType(stName, true);
        if (et != null) {
          SetResourceName(kvp.Key, et);
        }
      });
    }

    private void UpdateStructuralTypeFromJNode(JNode jNode, bool isFromServer) {
      var name = GetStructuralTypeNameFromJNode(jNode, isFromServer);
      var stype = GetStructuralTypeCore(name);
      if (stype == null) {
        var isComplexType = jNode.Get<bool>("isComplexType", false);
        OnMetadataMismatch(name, null, isComplexType ? 
          MetadataMismatchTypes.MissingCLRComplexType : MetadataMismatchTypes.MissingCLREntityType);
        return;
      }
      
      stype.UpdateFromJNode(jNode, isFromServer);
    }

    internal String GetStructuralTypeNameFromJNode(JNode jNode, String key, bool isFromServer) {
      var stName = jNode.Get<String>(key);
      if (stName != null && isFromServer) {
        stName = TypeNameInfo.FromStructuralTypeName(stName).ToClient(this).StructuralTypeName;
      }
      return stName;
    }

    internal String GetStructuralTypeNameFromJNode(JNode jNode, bool isFromServer) {
      var shortName = jNode.Get<String>("shortName");
      var ns = jNode.Get<String>("namespace");
      String stName;
      if (isFromServer) {
        stName = new TypeNameInfo(shortName, ns).ToClient(this).StructuralTypeName;
      } else {
        stName = TypeNameInfo.ToStructuralTypeName(shortName, ns);
      }
      return stName;
    }

    #endregion

    #region Internal and Private

    internal static Exception MissingTypeException(String typeName) {
      return new Exception("Unable to locate a CLR type corresponding to: " + typeName
          + ".  Consider calling Configuration.Instance.ProbeAssemblies with the assembly containing this " +
          "type when your application starts up.  In addition, if your namespaces are different between server and client " +
          "then you may need to call the MetadataStore.NamingConvention.WithClientServerNamespaceMapping method to create a " +
          "new NamingConvention that understands how to map between the two. Another alternative, if this type is not going " +
          "to be defined on the client, is to set the MetadataStore.AllowMetadataMismatchTypes property" +
          "or to use the MetadataStore.MetadataMismatch event.");
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

        _structuralTypes.Add(stType);
        _shortNameMap[stType.ShortName] = stType.Name;

      }
    }
    
    #endregion

    #region Inner classes 

    // inner class
 

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

    private static Object __lock = new Object();
    private static int __nextStoreID = 1;
    // Note: the two lines above need to appear before this next one 
    private static MetadataStore __detached = new MetadataStore();
    

    private NamingConvention _namingConvention = new NamingConvention();

    private readonly AsyncSemaphore _asyncSemaphore = new AsyncSemaphore(1);
    private readonly Object _lock = new Object();

    // lock using _dataServiceMap
    private readonly Dictionary<String, DataService> _dataServiceMap = new Dictionary<String, DataService>();
      
    
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
