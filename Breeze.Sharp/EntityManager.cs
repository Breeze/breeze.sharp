using Breeze.Sharp.Core;
using Newtonsoft.Json;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using System.Threading;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Converters;
using System.Text.RegularExpressions;

namespace Breeze.Sharp {

  /// <summary>
  /// Instances of the EntityManager contain and manage collections of entities, 
  /// either retrieved from a backend datastore or created on the client. 
  /// </summary>
  public class EntityManager  {

    #region Ctor 

    /// <summary>
    /// Constructs an empty EntityManager with a specified data service name. 
    /// </summary>
    /// <param name="serviceName"></param>
    /// <remarks><code>
    ///     // Example: 
    ///     var em = new EntityManager("http://localhost:7150/breeze/NorthwindIBModel/")
    /// </code></remarks>
    public EntityManager(String serviceName, MetadataStore metadataStore = null) 
      : this(new DataService(serviceName), metadataStore) {
    
    }

    /// <summary>
    /// /// <summary>
    /// Constructs an empty EntityManager with a specified DataService. 
    /// </summary>
    /// </summary>
    /// <param name="dataService"></param>
    public EntityManager(DataService dataService, MetadataStore metadataStore = null) {
      DataService = dataService;
      DefaultQueryOptions = QueryOptions.Default;
      CacheQueryOptions = CacheQueryOptions.Default;
      ValidationOptions = ValidationOptions.Default;
      MetadataStore = metadataStore ?? new MetadataStore();
      KeyGenerator = new DefaultKeyGenerator();
      Initialize();
    }

    /// <summary>
    /// Creates a new EntityManager with the same configuration as another EntityManager but without any entities.
    /// </summary>
    /// <param name="em"></param>
    public EntityManager(EntityManager em) {
      DataService = em.DataService;
      DefaultQueryOptions = em.DefaultQueryOptions;
      CacheQueryOptions = em.CacheQueryOptions;
      ValidationOptions = em.ValidationOptions;
      MetadataStore = em.MetadataStore;
      KeyGenerator = em.KeyGenerator; // TODO: review whether we should clone instead.
      Initialize();
    }

    private void Initialize() {
      var x = AuthorizedThreadId;
      EntityGroups = new EntityGroupCollection();
      UnattachedChildrenMap = new UnattachedChildrenMap();
      TempIds = new HashSet<UniqueId>();
    }

  
    #endregion

    #region Thread checking

    public Int32 CurrentThreadId {
      get {

        if (__threadLocalId == null) {
          __threadLocalId = new ThreadLocal<int>();
        }
        if (__threadLocalId.Value == 0) {
          __threadLocalId.Value = __nextThreadId++;
        }

        return __threadLocalId.Value;
      }
    }

    public Int32 AuthorizedThreadId {
      get {
        if (_authorizedThreadId == 0) {
          _authorizedThreadId = CurrentThreadId;
        }
        return _authorizedThreadId;
      }
    }

    public void CheckAuthorizedThreadId() {
      return;

      //if (CurrentThreadId != AuthorizedThreadId) {
      //  String msg = "An EntityManager can only execute on a single thread. This EntityManager is authorized to execute on the thread with id=’{0}’; ";
      //  msg += "the requested operation came from the thread with Id=‘{1}’.\n\n";
      //  msg += "You may have to disable this cross-thread checking for specific reasons such as automated testing. ";
      //  msg += "Please review our documentation on multi-threading issues and the EntityManager.AuthorizedThreadId property.";
      //  msg = String.Format(msg, AuthorizedThreadId, CurrentThreadId);
      //  throw new Exception(msg);
      //}
    }

    [ThreadStatic]
    private static ThreadLocal<Int32> __threadLocalId;
    private static Int32 __nextThreadId = 34;
    private Int32 _authorizedThreadId;

    #endregion

    #region Public props

    public MetadataStore MetadataStore {
      get; private set;
    }

    /// <summary>
    /// The default DataService for this EntityManager.
    /// </summary>
    public DataService DataService {
      get { return _dataService; }
      set { _dataService = InsureNotNull(value, "DataService"); }
    }

    /// <summary>
    /// Default QueryOptions for this EntityManager. These may be overriden for any specific query.
    /// </summary>
    public QueryOptions DefaultQueryOptions {
      get { return _defaultQueryOptions; }
      set { _defaultQueryOptions = InsureNotNull(value, "DefaultQueryOptions"); }
    }

    /// <summary>
    /// Default SaveOptions for this EntityManager. These may be overriden for any specific save.
    /// </summary>
    public SaveOptions DefaultSaveOptions {
      get { return _defaultSaveOptions; }
      set { _defaultSaveOptions = InsureNotNull(value, "DefaultSaveOptions"); }
    }

    /// <summary>
    /// The CacheQueryOptions to be used when querying the local cache.
    /// </summary>
    public CacheQueryOptions CacheQueryOptions {
      get { return _cacheQueryOptions; }
      set { _cacheQueryOptions = InsureNotNull(value, "CacheQueryOptions"); }
    }

    /// <summary>
    /// Validation options.
    /// </summary>
    public ValidationOptions ValidationOptions {
      get { return _validationOptions; }
      set { _validationOptions = InsureNotNull(value, "ValidationOptions"); }
    }

    public IKeyGenerator KeyGenerator {
      get { return _keyGenerator; }
      set { _keyGenerator = InsureNotNull(value, "KeyGenerator"); }
    }

    /// <summary>
    /// Whether or not change notification events will be fired. This includes both EntityChange and PropertyChange events.
    /// </summary>
    public bool ChangeNotificationEnabled {
      get { return _changeNotificationEnabled; }
      set {
        _changeNotificationEnabled = value;
        this.EntityGroups.ForEach(eg => eg.ChangeNotificationEnabled = value);
      }
    }
    
    #endregion

    #region async methods

    /// <summary>
    /// Fetches the metadata associated with the EntityManager's current 'serviceName'. 
    /// This call also occurs internally before the first query to any service if the metadata hasn't already been loaded.
    /// </summary>
    /// <param name="dataService"></param>
    /// <returns></returns>
    public async Task<DataService> FetchMetadata(DataService dataService = null)
    {
        return await FetchMetadata(CancellationToken.None, dataService);
    }
    /// <summary>
    /// Fetches the metadata associated with the EntityManager's current 'serviceName'. 
    /// This call also occurs internally before the first query to any service if the metadata hasn't already been loaded.
    /// </summary>
    /// <param name="dataService"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    public async Task<DataService> FetchMetadata(CancellationToken cancellationToken, DataService dataService = null) {
      dataService = dataService != null ? dataService : this.DataService;
      if (!dataService.HasServerMetadata) {
        throw new Exception("This DataService does not provide metadata: " + dataService.ServiceName);
      }
      return await MetadataStore.FetchMetadata(dataService, cancellationToken);
    }

    /// <summary>
    /// Performs an asynchronous query and that returns a typed result.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="query"></param>
    /// <returns></returns>
    public async Task<IEnumerable<T>> ExecuteQuery<T>(EntityQuery<T> query)
    {
        var result = await ExecuteQuery((EntityQuery)query, CancellationToken.None);
        return (IEnumerable<T>)result;
    }

    /// <summary>
    /// Performs an asynchronous query and that returns a typed result.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="query"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    public async Task<IEnumerable<T>> ExecuteQuery<T>(EntityQuery<T> query, CancellationToken cancellationToken) {
      var result = await ExecuteQuery((EntityQuery) query, cancellationToken);
      return (IEnumerable<T>)result;
    }

    /// <summary>
    /// Performs an asynchronous query and that returns an untyped result.
    /// </summary>
    /// <param name="query"></param>
    /// <returns></returns>
    public async Task<IEnumerable> ExecuteQuery(EntityQuery query)
    {
        return await ExecuteQuery(query, CancellationToken.None);
    }

    /// <summary>
    /// Performs an asynchronous query and that returns an untyped result.
    /// </summary>
    /// <param name="query"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    public async Task<IEnumerable> ExecuteQuery(EntityQuery query, CancellationToken cancellationToken)
    {
      cancellationToken.ThrowIfCancellationRequested();

      if (query.ElementType == null) {
        throw new Exception("Cannot execute a query with a null TargetType");
      }
      var fetchStrategy = query.QueryOptions.FetchStrategy ?? this.DefaultQueryOptions.FetchStrategy ?? QueryOptions.Default.FetchStrategy;
      if (fetchStrategy == FetchStrategy.FromLocalCache) {
        return query.ExecuteLocally(query.EntityManager ?? this);
        //var tcs = new TaskCompletionSource<IEnumerable>();
        //tcs.SetResult(query.ExecuteLocally());
        //return tcs.Task;
      }
      var dataService = query.DataService ?? this.DataService;
      if (dataService.HasServerMetadata) {
        await FetchMetadata(dataService);
        CheckAuthorizedThreadId();
      }
      var resourcePath = query.GetResourcePath(this.MetadataStore);
      // HACK
      resourcePath = resourcePath.Replace("/*", "");
      
      var result = await dataService.GetAsync(resourcePath, cancellationToken);

      cancellationToken.ThrowIfCancellationRequested();
      
      CheckAuthorizedThreadId();
      var mergeStrategy = query.QueryOptions.MergeStrategy ?? this.DefaultQueryOptions.MergeStrategy ?? QueryOptions.Default.MergeStrategy;

      var mappingContext = new MappingContext() {
        EntityManager = this,
        MergeStrategy = mergeStrategy.Value,
        LoadingOperation = LoadingOperation.Query,
        JsonResultsAdapter = query.JsonResultsAdapter ?? dataService.JsonResultsAdapter
      };
      // cannot reuse a jsonConverter - internal mappingContext is one instance/query
      var jsonConverter = new JsonEntityConverter(mappingContext);
      var serializer = new JsonSerializer();
      serializer.Converters.Add(jsonConverter);
      // serializer.Converters.Add(new StringEnumConverter());
      Type rType;
      
      using (NewIsLoadingBlock()) {
        var jt = JToken.Parse(result);
        jt = mappingContext.JsonResultsAdapter.ExtractResults(jt);
        if (resourcePath.Contains("inlinecount")) {
          rType = typeof (QueryResult<>).MakeGenericType(query.ElementType);
          return (IEnumerable)serializer.Deserialize(new JTokenReader(jt), rType);
        } else if (jt is JArray) {
          rType = typeof(IEnumerable<>).MakeGenericType(query.ElementType);
          return (IEnumerable)serializer.Deserialize(new JTokenReader(jt), rType);
        } else {
          rType = query.ElementType;
          var list= (IList) Activator.CreateInstance(typeof(List<>).MakeGenericType(query.ElementType));
          var item = serializer.Deserialize(new JTokenReader(jt), rType);
          list.Add(item);
          return list;
        }
        
      }
    }

    

    /// <summary>
    /// Performs an asynchronous saves of all pending changes within this EntityManager.
    /// </summary>
    /// <param name="saveOptions"></param>
    /// <returns></returns>
    public async Task<SaveResult> SaveChanges(SaveOptions saveOptions) {
      return await SaveChanges(null, saveOptions);
    }

    /// <summary>
    /// Performs an asynchronous save of just the specified entities within this EntityManager.
    /// </summary>
    /// <param name="entities"></param>
    /// <param name="saveOptions"></param>
    /// <returns></returns>
    public async Task<SaveResult> SaveChanges(IEnumerable<IEntity> entities = null, SaveOptions saveOptions = null) {
      List<IEntity> entitiesToSave;
      if (entities == null) {
        entitiesToSave = this.GetChanges().ToList();
      } else {
        entitiesToSave =
          entities.Where(e => !e.EntityAspect.IsDetached && e.EntityAspect.EntityManager == this).ToList();
      }
      if (entitiesToSave.Count == 0) {
        return SaveResult.Empty;
      }

      if ((this.ValidationOptions.ValidationApplicability & ValidationApplicability.OnSave) > 0) {
        var errs = entitiesToSave.SelectMany(ent => {
          var errors = ent.EntityAspect.ValidationErrors;
          // updates errors
          ent.EntityAspect.Validate();
          if (errors.Any()) {
            return errors.ToList().Where(err => {
              if (err.IsServerError) {
                errors.Remove(err);
              }
              return !err.IsServerError;
            });
          } else {
            return errors;
          }
        });
       if (errs.Any()) {
         throw new SaveException(errs);
       };
      }
      saveOptions = new SaveOptions(saveOptions ?? this.DefaultSaveOptions ?? SaveOptions.Default);
      if (saveOptions.ResourceName == null) saveOptions.ResourceName = "SaveChanges";
      if (saveOptions.DataService == null) saveOptions.DataService = this.DataService;
      var dataServiceAdapter = saveOptions.DataService.Adapter;
      var saveResult = await dataServiceAdapter.SaveChanges(entitiesToSave, saveOptions);
      if (entities == null) {
        SetHasChanges(false);
      } else {
        SetHasChanges(null);
      }
      return saveResult;
    }

    /// <summary>
    /// Performs an asynchronous query that optionally checks the local cache first.
    /// </summary>
    /// <param name="entityKey"></param>
    /// <param name="checkLocalCacheFirst"></param>
    /// <returns></returns>
    public async Task<EntityKeyFetchResult> FetchEntityByKey(EntityKey entityKey, bool checkLocalCacheFirst = false)
    {
        return await FetchEntityByKey(entityKey, CancellationToken.None, checkLocalCacheFirst);
    }

    /// <summary>
    /// Performs an asynchronous query that optionally checks the local cache first.
    /// </summary>
    /// <param name="entityKey"></param>
    /// <param name="cancellationToken"></param>
    /// <param name="checkLocalCacheFirst"></param>
    /// <returns></returns>
    public async Task<EntityKeyFetchResult> FetchEntityByKey(EntityKey entityKey, CancellationToken cancellationToken, bool checkLocalCacheFirst = false) {
      IEntity entity;
      
      cancellationToken.ThrowIfCancellationRequested();

      if (checkLocalCacheFirst) {
        entity = GetEntityByKey(entityKey);
        if (entity != null) return new EntityKeyFetchResult(entity, true);
      }
      var results = await ExecuteQuery(entityKey.ToQuery(), cancellationToken);
      entity = results.Cast<IEntity>().FirstOrDefault();
      return new EntityKeyFetchResult(entity, false);
    }

    /// <summary>
    /// The async result of a FetchEntityByKey call.
    /// </summary>
    public class EntityKeyFetchResult {
      public EntityKeyFetchResult(IEntity entity, bool fromCache) {
        Entity = entity;
        FromCache = fromCache;
      }

      public IEntity Entity { get; private set; }
      public bool FromCache { get; private set; }
    }

    #endregion

    #region Event methods

    /// <summary>
    /// Fired whenever an entity's state is changing in any significant manner.
    /// </summary>
    public event EventHandler<EntityChangingEventArgs> EntityChanging;

    /// <summary>
    /// Fired whenever an entity's state has changed in any significant manner.
    /// </summary>
    public event EventHandler<EntityChangedEventArgs> EntityChanged;

    public event EventHandler<EntityManagerHasChangesChangedEventArgs> HasChangesChanged;


    internal bool OnEntityChanging(IEntity entity, EntityAction action, EventArgs actionEventArgs = null) {
      EventHandler<EntityChangingEventArgs> handler = EntityChanging;
      if (handler != null) {
        var args = new EntityChangingEventArgs(entity, action, actionEventArgs);
        try {
          handler(this, args);
          return !args.Cancel;
        } catch {
          // Eat any handler exceptions during load (query or import) - throw for all others. 
          if (!(args.Action == EntityAction.AttachOnQuery || args.Action == EntityAction.AttachOnImport)) throw;
        }
      }
      return true;
    }

    internal void OnEntityChanged(IEntity entity, EntityAction entityAction, EventArgs actionEventArgs = null) {
      EventHandler<EntityChangedEventArgs> handler = EntityChanged;
      if (handler != null) {
        var args = new EntityChangedEventArgs(entity, entityAction, actionEventArgs);
        try {
          handler(this, args);
        } catch {
          // Eat any handler exceptions during load (query or import) - throw for all others. 
          if (!(args.Action == EntityAction.AttachOnQuery || args.Action == EntityAction.AttachOnImport)) throw;
        }
      }
    }

    internal void OnHasChangesChanged() {
      var handler = HasChangesChanged;
      if (handler != null) {
        var args = new EntityManagerHasChangesChangedEventArgs(this);
        handler(this, args);
      }
    }

    internal void FireQueuedEvents() {
      // IsLoadingEntity will still be true when this occurs.
      if (! _queuedEvents.Any()) return;
      var events = _queuedEvents.ToList();
      _queuedEvents.Clear();
      events.ForEach(a => a());

      // in case any of the previously queued events spawned other events.
      FireQueuedEvents();

    }

    internal List<Action> QueuedEvents {
      get { return _queuedEvents; }
    }

    #endregion

    #region Export/Import entities

    /// <summary>
    /// Exports a specified list of entities as a string, with the option to include metadata
    /// </summary>
    /// <param name="entities"></param>
    /// <param name="includeMetadata">Default is 'true'</param>
    /// <returns></returns>
    public String ExportEntities(IEnumerable<IEntity> entities = null, bool includeMetadata = true) {
      var jn = ExportToJNode(entities, includeMetadata);
      return jn.Serialize();
    }

    /// <summary>
    /// Exports a specified list of entities via a TextWriter, with the option to include metadata
    /// </summary>
    /// <param name="entities"></param>
    /// <param name="includeMetadata"></param>
    /// <param name="textWriter"></param>
    /// <returns></returns>
    public TextWriter ExportEntities(IEnumerable<IEntity> entities, bool includeMetadata, TextWriter textWriter) {
      var jn = ExportToJNode(entities, includeMetadata);
      jn.SerializeTo(textWriter);
      return textWriter;
    }

    /// <summary>
    /// Imports the string result of a previously executed ExportEntities call.
    /// </summary>
    /// <param name="exportedString"></param>
    /// <param name="importOptions"></param>
    /// <returns></returns>
    public ImportResult ImportEntities(String exportedString, ImportOptions importOptions = null) {
      var jn = JNode.DeserializeFrom(exportedString);
      return ImportEntities(jn, importOptions);
    }

    /// <summary>
    /// Imports the result of a previously executed ExportEntities call via a TextReader.
    /// </summary>
    /// <param name="textReader"></param>
    /// <param name="importOptions"></param>
    /// <returns></returns>
    public ImportResult ImportEntities(TextReader textReader, ImportOptions importOptions = null) {
      var jn = JNode.DeserializeFrom(textReader);
      return ImportEntities(jn, importOptions);
    }

    private ImportResult ImportEntities(JNode jn, ImportOptions importOptions) {
      importOptions = importOptions ?? ImportOptions.Default;
      if (importOptions.ShouldMergeMetadata) {
        var msNode = jn.GetJNode("metadataStore");
        if (msNode != null) {
          MetadataStore.ImportMetadata(msNode, false);
          var dsJn = jn.GetJNode("dataService");
          if (dsJn != null) DataService = new DataService(dsJn);
          var qoJn = jn.GetJNode("queryOptions");
          if (qoJn != null) DefaultQueryOptions = new QueryOptions(qoJn);
        }
      }
      var entityGroupNodesMap = jn.GetJNodeArrayMap("entityGroupMap");
      // tempKeyMap will have a new values where collisions will occur
      var tempKeyMap = jn.GetJNodeArray("tempKeys").Select(jnEk => new EntityKey(jnEk, this.MetadataStore)).ToDictionary(
        ek => ek, 
        ek => this.GetEntityByKey(ek) == null ? ek : EntityKey.Create(ek.EntityType, KeyGenerator.GetNextTempId(ek.EntityType.KeyProperties.First())) 
      );
      
      var mergeStrategy = (importOptions.MergeStrategy ?? this.DefaultQueryOptions.MergeStrategy ?? QueryOptions.Default.MergeStrategy).Value;
      var importedEntities = new List<IEntity>();
      using (NewIsLoadingBlock()) {
        entityGroupNodesMap.ForEach(kvp => {
          var entityTypeName = kvp.Key;
          var entityNodes = kvp.Value;
          var entityType = MetadataStore.GetEntityType(entityTypeName);
          var entities = ImportEntityGroup(entityNodes, entityType, tempKeyMap, mergeStrategy);
          importedEntities.AddRange(entities);
        });
      }
      return new ImportResult(importedEntities, tempKeyMap);
    }

    private List<IEntity> ImportEntityGroup(IEnumerable<JNode> entityNodes, EntityType entityType, Dictionary<EntityKey, EntityKey> tempKeyMap, MergeStrategy mergeStrategy) {
      var importedEntities = new List<IEntity>();
      foreach (var entityNode in entityNodes) {
        var ek = ExtractEntityKey(entityType, entityNode);
        var entityAspectNode = entityNode.GetJNode("entityAspect");
        // var entityState = (EntityState)Enum.Parse(typeof(EntityState), entityAspectNode.Get<String>("entityState"));
        var entityState = entityAspectNode.GetEnum<EntityState>("entityState");
        IEntity targetEntity = null;
        bool hasCollision = false;
        // allow merge of added records with non temp keys
        if (entityState.IsAdded() && tempKeyMap.ContainsKey(ek)) {
          hasCollision = tempKeyMap[ek] != ek;
        } else {
          targetEntity = GetEntityByKey(ek);
        }
        if (targetEntity != null) {
          var targetAspect = targetEntity.EntityAspect;
          if (mergeStrategy == MergeStrategy.Disallowed) continue;
          if (mergeStrategy == MergeStrategy.PreserveChanges && targetAspect.EntityState != EntityState.Unchanged) continue;
          PopulateImportedEntity(targetEntity, entityNode);
          UpdateTempFks(targetEntity, entityAspectNode, tempKeyMap);
          if (targetAspect.EntityState != entityState) {
            targetAspect.EntityState = entityState;
          }
          OnEntityChanged(targetEntity, EntityAction.MergeOnImport);
        } else {
          targetEntity = (IEntity)Activator.CreateInstance(entityType.ClrType);
          targetEntity.EntityAspect.EntityType = entityType;
          PopulateImportedEntity(targetEntity, entityNode);
          if (hasCollision) {
            var origEk = targetEntity.EntityAspect.EntityKey;
            var newEk = tempKeyMap[origEk];
            targetEntity.EntityAspect.SetDpValue(entityType.KeyProperties[0], newEk.Values[0]);
          }
          UpdateTempFks(targetEntity, entityAspectNode, tempKeyMap);
          AttachImportedEntity(targetEntity, entityType, entityState);
        }       

        importedEntities.Add(targetEntity);
      };
      return importedEntities;
    }

    private EntityKey ExtractEntityKey(EntityType entityType, JNode jn) {
      var keyValues = entityType.KeyProperties
         .Select(p => jn.Get(p.Name, p.ClrType))
         .ToArray();
      var entityKey = EntityKey.Create(entityType, keyValues);
      return entityKey;
    }

    private void PopulateImportedEntity(IEntity targetEntity, JNode jn) {
      var targetAspect = targetEntity.EntityAspect;
      var backingStore = targetAspect.BackingStore;

      var entityType = targetAspect.EntityType;
      entityType.DataProperties.ForEach(dp => {
        var propName = dp.Name;
        if (dp.IsComplexProperty) {
          var coNode = jn.GetJNode(propName);
          var newCo = (IComplexObject)coNode.ToObject(dp.ClrType);
          var targetCo = (IComplexObject)backingStore[propName];
          var targetCoAspect = targetCo.ComplexAspect;
          var coBacking = targetCoAspect.BackingStore;
          newCo.ComplexAspect.BackingStore.ForEach(kvp2 => {
            coBacking[kvp2.Key] = kvp2.Value;
          });
          UpdateOriginalValues(targetCoAspect, coNode);
        } else {
          var val = jn.Get(propName, dp.ClrType);
          backingStore[propName] = val;
        }
      });
      UpdateOriginalValues(targetAspect, jn);
    }

    private static void UpdateOriginalValues(StructuralAspect targetAspect, JNode jNode) {
      var stType = targetAspect.StructuralType;
      var aspectNode = jNode.GetJNode(stType.IsEntityType ? "entityAspect" : "complexAspect");
      if (aspectNode == null) return; // aspect node can be null in a complexAspect with no originalValues
      var originalValuesMap = aspectNode.GetMap("originalValuesMap", pn => stType.GetDataProperty(pn).ClrType);
      if (originalValuesMap != null) {
        targetAspect._originalValuesMap = new BackupValuesMap(originalValuesMap);
      }
    }

    private void UpdateTempFks(IEntity targetEntity, JNode entityAspectNode, Dictionary<EntityKey, EntityKey> tempKeyMap) {

      var tempNavPropNames = entityAspectNode.GetArray<String>("tempNavPropNames");
      if (!tempNavPropNames.Any()) return;
      var targetAspect = targetEntity.EntityAspect;
      var entityType = targetAspect.EntityType;

      tempNavPropNames.ForEach(npName => {
        var np = entityType.GetNavigationProperty(npName);
        var fkProp = np.RelatedDataProperties[0];
        var oldFkValue = targetAspect.GetValue(fkProp);
        var oldFk = new EntityKey(np.EntityType, oldFkValue);
        var newFk = tempKeyMap[oldFk];
        targetAspect.SetValue(fkProp, newFk.Values[0]);

      });
    }

    private JNode ExportToJNode(IEnumerable<IEntity> entities, bool includeMetadata) {
      var jn = ExportEntityGroupsAndTempKeys(entities);

      if (includeMetadata) {
        jn.AddJNode("dataService", this.DataService);
        jn.AddJNode("queryOptions", this.DefaultQueryOptions);
        // jo.AddObject("saveOptions", this.SaveOptions);
        // jo.AddObject("validationOptions", this.ValidationOptions);
        jn.AddJNode("metadataStore", ((IJsonSerializable)this.MetadataStore).ToJNode(null));
      }
      return jn;
    }

    private JNode ExportEntityGroupsAndTempKeys(IEnumerable<IEntity> entities) {
      Dictionary<String, IEnumerable<JNode>> map;
      IEnumerable<EntityAspect> aspects;

      if (entities != null) {
        aspects = entities.Select(e => e.EntityAspect);
        map = aspects.GroupBy(ea => ea.EntityGroup.EntityType).ToDictionary(grp => grp.Key.Name, grp => ExportAspects(grp, grp.Key));
      } else {
        aspects = this.EntityGroups.SelectMany(eg => eg.EntityAspects);
        map = this.EntityGroups.ToDictionary(eg => eg.EntityType.Name, eg => ExportAspects(eg, eg.EntityType));
      }

      var tempKeys = aspects.Where(ea => ea.HasTemporaryKey).Select(ea => ea.EntityKey);

      var jn = new JNode();
      // entityGroup map is map of entityTypeName: array of serialized entities;
      jn.AddMap("entityGroupMap", map);
      jn.AddArray("tempKeys", tempKeys);
      return jn;
    }

    private IEnumerable<JNode> ExportAspects(IEnumerable<EntityAspect> aspects, EntityType et) {
      var dps = et.DataProperties;
      var nodes = aspects.Select(aspect => ExportAspect(aspect, dps));
      return nodes;
    }

    private JNode ExportAspect(StructuralAspect aspect, IEnumerable<DataProperty> dps) {
      var jn = new JNode();
      dps.ForEach(dp => {
        var propName = dp.Name;
        var value = aspect.GetRawValue(propName);
        var co = value as IComplexObject;
        if (co != null) {
          var complexAspect = co.ComplexAspect;
          jn.AddJNode(propName, ExportAspect(complexAspect, complexAspect.ComplexType.DataProperties));
        } else {
          if (value != dp.DefaultValue) {
            jn.AddPrimitive(propName, value);
          }
        }
      });
      if (aspect is ComplexAspect) {
        var complexAspectNode = ExportComplexAspectInfo((ComplexAspect)aspect);
        jn.AddJNode("complexAspect", complexAspectNode);
      } else {
        var entityAspectNode = ExportEntityAspectInfo((EntityAspect)aspect);
        jn.AddJNode("entityAspect", entityAspectNode);
      }
      return jn;
    }

    private JNode ExportEntityAspectInfo(EntityAspect entityAspect) {
      var jn = new JNode();
      var es = entityAspect.EntityState;
      jn.AddEnum("entityState", entityAspect.EntityState);
      jn.AddArray("tempNavPropNames", GetTempNavPropNames(entityAspect));
      if (es.IsModified() || es.IsDeleted()) {
        jn.AddMap("originalValuesMap", entityAspect._originalValuesMap);
      }
      return jn;
    }

    private JNode ExportComplexAspectInfo(ComplexAspect complexAspect) {
      var jn = new JNode();
      jn.AddMap("originalValuesMap", complexAspect._originalValuesMap);
      return jn;
    }

    private IEnumerable<String> GetTempNavPropNames(EntityAspect entityAspect) {
      var npNames = entityAspect.EntityType.NavigationProperties.Where(np => {
        if (!np.IsScalar) return false;
        var val = (IEntity)entityAspect.GetRawValue(np.Name);
        return (val != null && val.EntityAspect.HasTemporaryKey);
      }).Select(np => np.Name);
      return npNames;
    }

    #endregion

    #region Misc public methods

    /// <summary>
    /// Turns on or off change notification events for a specific entity type.
    /// </summary>
    /// <param name="clrEntityType"></param>
    /// <param name="enabled"></param>
    public void EnableChangeNotification(Type clrEntityType, bool enabled = true) {
      CheckEntityType(clrEntityType);
      var eg = GetEntityGroup(clrEntityType);
      eg.ChangeNotificationEnabled = enabled;
    }

    /// <summary>
    /// Whether change notification events are enabled for a specific entity type.
    /// </summary>
    /// <param name="clrEntityType"></param>
    /// <returns></returns>
    public bool IsChangeNotificationEnabled(Type clrEntityType) {
      CheckEntityType(clrEntityType);
      var eg = GetEntityGroup(clrEntityType);
      return eg.ChangeNotificationEnabled;
    }

    /// <summary>
    /// Executes the specified query against the local cache and returns a typed result. 
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="query"></param>
    /// <returns></returns>
    public IEnumerable<T> ExecuteQueryLocally<T>(EntityQuery<T> query) {
      return query.ExecuteLocally(query.EntityManager ?? this);
    }

    /// <summary>
    /// Executes the specified query against the local cache and returns an untyped result. 
    /// </summary>
    /// <param name="query"></param>
    /// <returns></returns>
    public IEnumerable ExecuteQueryLocally(EntityQuery query) {
      return query.ExecuteLocally(query.EntityManager ?? this);
    }

    /// <summary>
    /// Calls AcceptChanges on each Added, Deleted or Modified entity within the cache. All pending changes will be considered complete and after this call
    /// the EntityManager will have no pending changes.
    /// </summary>
    public void AcceptChanges() {
      var entities = this.GetChanges().ToList();
      using (NewIsLoadingBlock(false)) {
        entities.ForEach(e => e.AcceptChanges());
      }
      SetHasChanges(false);
    }

    /// <summary>
    /// Calls RejectChanges on each Added, Deleted or Modified entity within the cache.  All pending changes will be lost and after this call
    /// the EntityManager will have no pending changes.
    /// </summary>
    public void RejectChanges() {
      var entities = this.GetChanges().ToList();
      using (NewIsLoadingBlock(false)) {
        entities.ForEach(e => e.RejectChanges());
      }
      SetHasChanges(false);
    }

    /// <summary>
    /// Removes all entities from this EntityManager.
    /// </summary>
    public void Clear() {
      EntityGroups.ForEach(eg => eg.Clear());
      SetHasChanges(false);
      Initialize();
      OnEntityChanged(null, EntityAction.Clear);
    }

    /// <summary>
    /// Find all entities in cache having the specified entity state(s).
    /// </summary>
    /// <param name="entityState">EntityState(s) of entities to return</param>
    /// <returns></returns>
    /// <remarks>
    /// As the <see cref="EntityState"/> is a flags enumeration, you can supply multiple 
    /// OR'ed values to search for multiple entity states.
    /// </remarks>
    public IEnumerable<IEntity> GetEntities(EntityState entityState = EntityState.AllButDetached) {
      return GetEntities(typeof(IEntity), entityState);
    }

    /// <summary>
    /// Retrieves all entities of a specified type with the specified entity state(s) from cache as a typed enumerable.
    /// </summary>
    /// <typeparam name="T">The type of Entity to retrieve</typeparam>
    /// <param name="entityState">EntityState(s) of entities to return</param>
    /// <returns>A collection of Entities</returns>
    /// <remarks>
    /// As the <see cref="EntityState"/> is a flags enumeration, you can supply multiple 
    /// OR'ed values to search for multiple entity states.
    /// </remarks>
    public IEnumerable<T> GetEntities<T>(EntityState entityState = EntityState.AllButDetached) {
      return GetEntities(typeof(T), entityState).Cast<T>();
    }

    /// <summary>
    /// Retrieves all entities of the specified types from cache.
    /// </summary>
    /// <param name="entityTypes"></param>
    /// <returns></returns>
    public IEnumerable<IEntity> GetEntities(params Type[] entityTypes) {
      return GetEntities((IEnumerable<Type>)entityTypes);
    }


    /// <summary>
    /// Retrieves all entities of the specified types with the specified entity state(s) from cache.
    /// </summary>
    /// <param name="types"></param>
    /// <param name="entityState">defaults to AllButDetached</param>
    /// <returns></returns>
    public IEnumerable<IEntity> GetEntities(IEnumerable<Type> types, EntityState entityState = EntityState.AllButDetached) {
      return types.SelectMany(t => GetEntities(t, entityState));
    }

    /// <summary>
    /// Retrieves all entities of a specified type with the specified entity state(s) from cache.
    /// </summary>
    /// <param name="type">The type of Entity to retrieve</param>
    /// <param name="entityState">EntityState(s) of entities to return - defaults to AllButDetached</param>
    /// <returns>A collection of Entities</returns>
    /// <remarks>
    /// As the <see cref="EntityState"/> is a flags enumeration, you can supply multiple 
    /// OR'ed values to search for multiple entity states.
    /// </remarks>
    public IEnumerable<IEntity> GetEntities(Type type, EntityState entityState) {
      if (type.GetTypeInfo().IsAbstract) {
        var groups = type == typeof(IEntity) 
          ? this.EntityGroups
          : this.EntityGroups.Where(eg => type.IsAssignableFrom(eg.ClrType));
        return groups.SelectMany(f => f.LocalEntityAspects)
          .Where(ea => ((ea.EntityState & entityState) > 0))
          .Select(ea => ea.Entity);
      } else {
        var group = GetEntityGroup(type);
        return group.EntityAspects
          .Where(ea => ((ea.EntityState & entityState) > 0))
          .Select(ea => ea.Entity);
      }
    }

    /// <summary>
    /// Returns a collection of all of the pending changes within this EntityManager.
    /// </summary>
    /// <param name="entityTypes"></param>
    /// <returns></returns>
    public IEnumerable<IEntity> GetChanges(params Type[] entityTypes) {
      return GetChanges((IEnumerable<Type>) entityTypes);
    }

    /// <summary>
    /// Returns a collection of all of the pending changes within this EntityManager for a specified list of entity types.
    /// </summary>
    /// <param name="entityTypes"></param>
    /// <returns></returns>
    public IEnumerable<IEntity> GetChanges(IEnumerable<Type> entityTypes = null) {
      if (entityTypes == null) {
        return GetEntities(EntityState.AnyAddedModifiedOrDeleted);
      }
      var groups = entityTypes.SelectMany(et => this.EntityGroups.Where(eg => et.IsAssignableFrom(eg.ClrType)));
      return groups.SelectMany(f => f.LocalEntityAspects)
        .Where(ea => ((ea.EntityState & EntityState.AnyAddedModifiedOrDeleted) > 0))
        .Select(ea => ea.Entity);
    }

    /// <summary>
    /// Attempts to return a specific entity within the local cache by its key and returns a null if not found.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="entityKey"></param>
    /// <returns></returns>
    public T GetEntityByKey<T>(EntityKey entityKey) {
      return (T)GetEntityByKey(entityKey);
    }

    /// <summary>
    /// Attempts to return a specific entity within the local cache by its key and returns a null if not found.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="values"></param>
    /// <returns></returns>
    public T GetEntityByKey<T>(params Object[] values) where T : IEntity {
      var ek = new EntityKey(typeof(T), this.MetadataStore, values);
      return (T)GetEntityByKey(ek);
    }

    /// <summary>
    /// Attempts to return a specific entity within the local cache by its key and returns a null if not found.
    /// </summary>
    /// <param name="entityKey"></param>
    /// <returns></returns>
    public IEntity GetEntityByKey(EntityKey entityKey) {
      var eg = this.GetEntityGroup(entityKey.ClrType);
      var ea = (eg == null) ? null : eg.FindEntityAspect(entityKey, true);
      if (ea != null) return ea.Entity;

      var subtypes = entityKey.EntityType.SelfAndSubEntityTypes;
      if (subtypes.Count > 1) {
        ea = subtypes.Skip(1).Select(st => {
          eg = this.GetEntityGroup(st.ClrType);
          return (eg == null) ? null : eg.FindEntityAspect(entityKey, true);
        }).FirstOrDefault(a => a != null);
      }
      
      return ea == null ? null : ea.Entity;
    }


    #endregion

    #region Create/Attach/Detach entity methods

    /// <summary>
    /// Creates a new entity and sets its EntityState. Unless this state is Detached, the entity
    /// is also added to the EntityManager.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="entityState"></param>
    /// <returns></returns>
    public T CreateEntity<T>(EntityState entityState) {
      return (T)CreateEntity(typeof(T), null, entityState);
    }

    /// <summary>
    /// Creates a new entity and sets its EntityState. Unless this state is Detached, the entity
    /// is also added to the EntityManager.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="initialValues"></param>
    /// <param name="entityState">Default is 'Added'</param>
    /// <returns></returns>
    public T CreateEntity<T>(Object initialValues=null, EntityState entityState = EntityState.Added) {
      return (T)CreateEntity(typeof(T), initialValues, entityState);
    }

    /// <summary>
    /// Creates a new entity and sets its EntityState. Unless this state is Detached, the entity
    /// is also added to the EntityManager.
    /// </summary>
    /// <param name="entityType"></param>
    /// <param name="initialValues"></param>
    /// <param name="entityState"></param>
    /// <returns></returns>
    public IEntity CreateEntity(EntityType entityType, Object initialValues=null, EntityState entityState = EntityState.Added) {
      return CreateEntity(entityType.ClrType, initialValues, entityState);
    }

    /// <summary>
    /// Creates a new entity and sets its EntityState. Unless this state is Detached, the entity
    /// is also added to the EntityManager.
    /// </summary>
    /// <param name="clrType"></param>
    /// <param name="initialValues"></param>
    /// <param name="entityState"></param>
    /// <returns></returns>
    public IEntity CreateEntity(Type clrType, Object initialValues = null, EntityState entityState = EntityState.Added) {
      var entity = (IEntity)Activator.CreateInstance(clrType);

      var et = MetadataStore.GetEntityType(clrType);

      InitializeEnumProps(clrType, entity, et);

      entity.EntityAspect.EntityType = et;
      InitializeEntity(entity, initialValues, et);
      if (entityState == EntityState.Detached) {
        PrepareForAttach(entity);
      } else {
        AttachEntity(entity, entityState);
      }
      return entity;
    }

    private static void InitializeEnumProps(Type clrType, IEntity entity, EntityType et) {
      // Needed because Activator.CreateInstance does not know how to handle nonnullable enums.
      var enumProps = clrType.GetTypeInfo().DeclaredProperties.Where(p => p.PropertyType.GetTypeInfo().IsEnum);
      enumProps.ForEach(pi => {
        var enumType = pi.PropertyType.GetTypeInfo();
        var firstEnum = Enum.GetValues(pi.PropertyType).GetValue(0);
        var prop = et.GetProperty(pi.Name);
        entity.EntityAspect.SetValue(prop, firstEnum);
      });
    }

    private static void InitializeEntity(IEntity entity, object initialValues, EntityType et) {
      if (initialValues == null) return;
      initialValues.GetType().GetTypeInfo().DeclaredProperties.ForEach(pi => {
        try {
          var prop = et.GetProperty(pi.Name);
          var val = pi.GetValue(initialValues);

          if (prop.IsDataProperty) {
            var type = TypeFns.GetNonNullableType(prop.ClrType);
            var cvtVal = TypeFns.ConvertType(val, type, true);
            entity.EntityAspect.SetValue(prop, cvtVal);
          } else {
            if (prop.IsScalar) {
              entity.EntityAspect.SetValue(prop, val);
            } else {
              var navSet = (INavigationSet) entity.EntityAspect.GetValue(prop);
              (val as IEnumerable).Cast<IEntity>().ForEach(e => navSet.Add(e));
            }
          }
        } catch(Exception e) {
          var msg = "Unable to create entity of type '{0}'.  Initialization failed on property: '{1}'";
          throw new Exception(String.Format(msg, et.Name, pi.Name), e);
        }
      });
    }

    /// <summary>
    /// Attaches a detached entity to this EntityManager and sets its EntityState to 'Added'.
    /// </summary>
    /// <param name="entity"></param>
    /// <returns></returns>
    public IEntity AddEntity(IEntity entity) {
      return AttachEntity(entity, EntityState.Added);
    }

    /// <summary>
    /// Attaches a detached entity to this EntityManager and sets its EntityState to a specified state.
    /// </summary>
    /// <param name="entity"></param>
    /// <param name="entityState">Defaults to 'Unchanged'</param>
    /// <param name="mergeStrategy"></param>
    /// <returns></returns>
    public IEntity AttachEntity(IEntity entity, EntityState entityState = EntityState.Unchanged, MergeStrategy mergeStrategy = MergeStrategy.Disallowed) {
      var aspect = PrepareForAttach(entity);
      if (aspect.IsAttached) return entity;
      using (NewIsLoadingBlock()) {
        // don't fire EntityChanging because there is no entity to recieve the event until it is attached.

        if (entityState.IsAdded()) {
          InitializeEntityKey(aspect);
        }

        // TODO: handle mergeStrategy here
        
        AttachEntityAspect(aspect, entityState);
        
        aspect.EntityType.NavigationProperties.ForEach(np => {
          aspect.ProcessNpValue(np, e => AttachEntity(e, entityState, mergeStrategy));
        });

        if ((this.ValidationOptions.ValidationApplicability & ValidationApplicability.OnAttach) > 0) {
          aspect.ValidateInternal();
        }


        if (!entityState.IsUnchanged()) {
          NotifyStateChange(aspect, true);
        }
        aspect.OnEntityChanged(EntityAction.Attach);
        return aspect.Entity;

      }
    }

    /// <summary>
    /// Removes a specified entity from this EntityManager. Note this is NOT a deletion.  It simply causes 
    /// the EntityManager to 'forget' about this entity.
    /// </summary>
    /// <param name="entity"></param>
    /// <returns></returns>
    public bool DetachEntity(IEntity entity) {
      return entity.EntityAspect.Detach();
    }

    internal EntityAspect AttachQueriedEntity(IEntity entity, EntityType entityType) {
      var aspect = entity.EntityAspect;
      aspect.EntityType = entityType;
      AttachEntityAspect(aspect, EntityState.Unchanged); 

      if ((this.ValidationOptions.ValidationApplicability & ValidationApplicability.OnQuery) > 0) {
        aspect.ValidateInternal();
      }

      aspect.OnEntityChanged(EntityAction.AttachOnQuery);
      return aspect;
    }

    internal EntityAspect AttachImportedEntity(IEntity entity, EntityType entityType, EntityState entityState) {
      var aspect = entity.EntityAspect;
      aspect.EntityType = entityType;
      AttachEntityAspect(aspect, entityState);
      
      aspect.OnEntityChanged(EntityAction.AttachOnImport);
      return aspect;
    }


    private EntityAspect PrepareForAttach(IEntity entity) {
      var aspect = entity.EntityAspect;
      if (aspect.EntityType.MetadataStore == MetadataStore.Detached) {
        aspect.EntityType = this.MetadataStore.GetEntityType(entity.GetType());
        aspect.EntityKey = null;
      } else if (aspect.EntityType.MetadataStore != this.MetadataStore) {
        throw new Exception("Cannot attach this entity because the EntityType (" + aspect.EntityType.Name + ") and MetadataStore associated with this entity does not match this EntityManager's MetadataStore.");
      }

      // check if already attached
      if (!aspect.IsDetached) {
        if (aspect.EntityManager != this) {
          throw new Exception("This entity already belongs to another EntityManager");
        }
      }
      aspect.Initialize();

      return aspect;
    }

    private EntityAspect AttachEntityAspect(EntityAspect entityAspect, EntityState entityState) {
      var group = GetEntityGroup(entityAspect.EntityType.ClrType);
      group.AttachEntityAspect(entityAspect, entityState);
      if (entityState != EntityState.Modified) {
        // don't clear this if modified because it came from an earlier incarnation.
        entityAspect._originalValuesMap = null;
      }
      entityAspect.LinkRelatedEntities();
      return entityAspect;
    }

    private void InitializeEntityKey(EntityAspect aspect) {
      var ek = aspect.EntityKey;
      // return properties that are = to defaultValues
      var keyProps = aspect.EntityType.KeyProperties;
      var keyPropsWithDefaultValues = keyProps
        .Zip(ek.Values, (kp, kv) => Object.Equals(kp.DefaultValue,kv) ? kp : null)
        .Where(kp => kp != null);

      if (keyPropsWithDefaultValues.Any()) {
        if (aspect.EntityType.AutoGeneratedKeyType != AutoGeneratedKeyType.None) {
          GenerateId(aspect.Entity, keyPropsWithDefaultValues.First(p => p.IsAutoIncrementing));
        } else {
          // we will allow attaches of entities where only part of the key is set.
          if (keyPropsWithDefaultValues.Count() == ek.Values.Length) {
            throw new Exception("Cannot attach an object of type  (" + aspect.EntityType.Name +
              ") to an EntityManager without first setting its key or setting its entityType 'AutoGeneratedKeyType' property to something other than 'None'");
          }
        }
      }
    }

    #endregion

    #region EntityGroup methods

    /// <summary>
    /// Returns the EntityGroup associated with a specific Entity subtype.
    /// </summary>
    /// <param name="clrEntityType">An <see cref="IEntity"/> subtype</param>
    /// <returns>The <see cref="EntityGroup"/> associated with the specified Entity subtype</returns>
    /// <exception cref="ArgumentException">Bad entity type</exception>
    /// <exception cref="EntityServerException"/>
    internal EntityGroup GetEntityGroup(Type clrEntityType) {
      var eg = this.EntityGroups[clrEntityType];
      if (eg != null) {
        return eg;
      }
      
      lock (this.EntityGroups) {
        // check again just in case another thread got in.
        eg = this.EntityGroups[clrEntityType];
        if (eg != null) {
          return eg;
        }

        eg = EntityGroup.Create(clrEntityType, this);
        this.EntityGroups.Add(eg);
        // ensure that any entities placed into the table on initialization are 
        // marked so as not to be saved again
        eg.AcceptChanges();

        return eg;

      }
    }

    internal EntityGroup GetEntityGroup<T>() where T : IEntity {
      return GetEntityGroup(typeof(T));
    }


    #endregion

    #region KeyGenerator methods

    /// <summary>
    /// Generates a temporary ID for an <see cref="IEntity"/>.  The temporary ID will be mapped to a real ID when
    /// <see cref="SaveChanges"/> is called.
    /// <seealso cref="IKeyGenerator"/>
    /// </summary>
    /// <param name="entity">The Entity object for which the new ID will be generated</param>
    /// <param name="entityProperty">The EntityProperty in which the new ID will be set </param>
    /// <remarks>
    /// You must implement the <see cref="IKeyGenerator"/> interface to use ID generation.  See the
    /// <b>DevForce Developer's Guide</b> for more information on custom ID generation.
    /// <para>
    /// If you are using a SQL Server <b>Identity</b> property you do not need to call <b>GenerateId</b>
    /// for the property.
    /// </para>
    /// </remarks>
    /// <exception cref="ArgumentException">Incorrect entity type/property</exception>
    /// <exception cref="IdeaBladeException">IdGenerator not found</exception>
    public UniqueId GenerateId(IEntity entity, DataProperty entityProperty) {

      var aspect = entity.EntityAspect;
      var entityType = entity.GetType();

      if (entityProperty.IsForeignKey) {
        String msg = String.Format(
          "Cannot call GenerateId on '{0}.{1}'. GenerateId cannot be called on ForeignKey properties ( even if they are also part of a PrimaryKey).  Call GenerateId instead on the 'source' primary key.",
          entityProperty.ParentType.Name, entityProperty.Name);
        throw new ArgumentException("entityProperty", msg);
      }
      if (!entityProperty.ParentType.ClrType.IsAssignableFrom(entityType)) {
        String msg = String.Format("The EntityType '{0}' for Property '{1}' must be of EntityType '{2}' or one of its subtypes", entityProperty.ParentType, entityProperty.Name, entityType);
        throw new ArgumentException("entityProperty", msg);
      }
      // Associate this entity with this EntityManager if it previously wasn't
      // - so that it cannot later be used in another EntityManager
      if (aspect.EntityGroup == null) {
        aspect.EntityGroup = GetEntityGroup(entityType);
      }
      
      if (KeyGenerator == null) {
        throw new Exception("Unable to locate a KeyGenerator");
      }

      object nextTempId = KeyGenerator.GetNextTempId(entityProperty);

      // check for collision with any existing temporary key (could happen as a result of import)
      var nextTempKey = new EntityKey(aspect.EntityType, new object[1] { nextTempId });
      while (aspect.EntityGroup.KeyMapContains(nextTempKey)) {
        nextTempId = KeyGenerator.GetNextTempId(entityProperty);
        nextTempKey.Values[0] = nextTempId;
      }

      aspect.SetDpValue(entityProperty, nextTempId);
      var aUniqueId = new UniqueId(entityProperty, nextTempId);
      // don't add to tempId's collection until the entity itself is added.
      if (aspect.EntityState != EntityState.Detached) {
        AddToTempIds(aUniqueId);
      }
      return aUniqueId;

    }

    /// <summary>
    /// Insures that a temporary pk is set if necessary
    /// </summary>
    /// <param name="aspect"></param>
    internal void UpdatePkIfNeeded(EntityAspect aspect) {
      if (KeyGenerator == null) return;
      var keyProperties = aspect.EntityType.KeyProperties;
      
      foreach (var aProperty in keyProperties) {

        var val = aspect.GetValue(aProperty.Name);
        var aUniqueId = new UniqueId(aProperty, val);
        
        // determine if a temp pk is needed.
        if (aProperty.IsAutoIncrementing) {
          if (!KeyGenerator.IsTempId(aUniqueId)) {
            // generate an id if it wasn't already generated
            aUniqueId = GenerateId(aspect.Entity, aProperty);
          }
          AddToTempIds(aUniqueId);
        } else if (aProperty.DefaultValue == val) {
          // do not call GenerateId unless the developer is explicit or the key is autoincrementing.
        } else {
          // this occurs if GenerateId was called before Attach - it won't have been added to tempIds in this case.
          if (KeyGenerator.IsTempId(aUniqueId)) {
            AddToTempIds(aUniqueId);
          }
        }
      }
    }

    internal void MarkTempIdAsMapped(EntityAspect aspect, bool isMapped) {
      var keyProperties = aspect.EntityType.KeyProperties;
      foreach (var aProperty in keyProperties) {
        UniqueId aUniqueId = new UniqueId(aProperty, aspect.GetValue(aProperty.Name));
        if (isMapped) {
          TempIds.Remove(aUniqueId);
        } else {
          if (KeyGenerator == null) return;
          if (KeyGenerator.IsTempId(aUniqueId)) {
            TempIds.Add(aUniqueId);
          }
        }
      }
    }

    internal void AddToTempIds(UniqueId uniqueId) {
      if (uniqueId != null) {
        TempIds.Add(uniqueId);
      }
    }

    private void RemoveMappedTempIds(UniqueIdMap idMap) {
      foreach (UniqueId uniqueId in idMap.Keys) {
        TempIds.Remove(uniqueId);
      }
    }

    internal bool AnyStoreGeneratedTempIds {
      get {
        return TempIds != null && TempIds.Any(id => id.Property.IsAutoIncrementing);
      }
    }

    private bool AnyTempIds {
      get {
        return TempIds != null && TempIds.Any();
      }
    }

    internal HashSet<UniqueId> TempIds {
      get;
      private set;
    }

    #endregion

    #region HasChanges/StateChange methods

    /// <summary>
    /// Returns whether there are any pending changes for this EntityManager or for selected types within this EntityManager.
    /// </summary>
    /// <param name="entityTypes"></param>
    /// <returns></returns>
    public bool HasChanges(IEnumerable<Type> entityTypes = null) {
      if (!this._hasChanges) return false;
      if (entityTypes == null) return this._hasChanges;
      return this.HasChangesCore(entityTypes);
    }

    // backdoor the "really" check for changes.
    private bool HasChangesCore(IEnumerable<Type> entityTypes) {
      var entityGroups = (entityTypes == null) 
        ? this.EntityGroups 
        : entityTypes.Select(et => GetEntityGroup(et));
      return entityGroups.Any(eg => eg != null && eg.HasChanges());
    }

    internal void CheckStateChange(EntityAspect entityAspect, bool wasUnchanged, bool isUnchanged) {
      if (wasUnchanged) {
        if (!isUnchanged) {
          this.NotifyStateChange(entityAspect, true);
        }
      } else {
        if (isUnchanged) {
          this.NotifyStateChange(entityAspect, false);
        }
      }
    }

    internal void NotifyStateChange(EntityAspect entityAspect, bool needsSave) {
      entityAspect.OnEntityChanged(EntityAction.EntityStateChange);

      if (needsSave) {
        SetHasChanges(true);
      } else {
        // called when rejecting a change or merging an unchanged record.
        // NOTE: this can be slow with lots of entities in the cache.
        if (this._hasChanges) {
          if (this.IsLoadingEntity) {
            this.HasChangesAction = this.HasChangesAction ?? (() => SetHasChanges(null));
          } else {
            SetHasChanges(null);
          }
        }
      }
    }

    internal void SetHasChanges(bool? value) {
      var hasChanges = value.HasValue ? value.Value : this.HasChangesCore(null);
      if (hasChanges != this._hasChanges) {
        this._hasChanges = hasChanges;
        OnHasChangesChanged();
      }
      HasChangesAction = null;
    }

    internal Action HasChangesAction { get; set; }

    #endregion

    #region Other internal 

    internal void UpdateFkVal(DataProperty fkProp, Object oldValue, Object newValue) {
      var eg = this.EntityGroups[fkProp.ParentType.ClrType];
      if (eg == null) return;
        
      eg.UpdateFkVal(fkProp, oldValue, newValue);
    }

    internal static void CheckEntityType(Type clrEntityType) {
      var etInfo = clrEntityType.GetTypeInfo();
      if ( typeof(IEntity).GetTypeInfo().IsAssignableFrom(etInfo) && !etInfo.IsAbstract) return;
      throw new ArgumentException("This operation requires a nonabstract type that implements the IEntity interface");
    }

    internal LoadingBlock NewIsLoadingBlock(bool allowHasChangesAction=true) {
      return new LoadingBlock(this, allowHasChangesAction);
    }

    internal class LoadingBlock : IDisposable {
      public LoadingBlock(EntityManager entityManager, bool allowHasChangesAction) {
        _entityManager = entityManager;
        _wasLoadingEntity = _entityManager.IsLoadingEntity;
        if (!allowHasChangesAction) {
          // makes the HasChangesAction a noop if not already set;
          _entityManager.HasChangesAction = entityManager.HasChangesAction ?? (() => { return; });
        }
        entityManager.IsLoadingEntity = true;
      }

      public void Dispose() {
        if (!_wasLoadingEntity) {
          _entityManager.FireQueuedEvents();
          if (_entityManager.HasChangesAction != null) {
            _entityManager.HasChangesAction();
            _entityManager.HasChangesAction = null;
          }
        }
        _entityManager.IsLoadingEntity = _wasLoadingEntity;
      }

      private EntityManager _entityManager;
      private bool _wasLoadingEntity;
    }

    internal bool IsLoadingEntity { get; set;  }
    internal bool IsRejectingChanges { get; set;  }
    internal UnattachedChildrenMap UnattachedChildrenMap { get; private set; }

    #endregion

    #region other private 

    private T InsureNotNull<T>(T val, String argumentName) {
      if (val == null) {
        throw new ArgumentNullException(argumentName);
      }
      return val;
    }

    private EntityGroupCollection EntityGroups { get; set; }
    private List<Action> _queuedEvents = new List<Action>();
    private bool _changeNotificationEnabled = true;
    private DataService _dataService;
    private QueryOptions _defaultQueryOptions;
    private SaveOptions _defaultSaveOptions;
    private CacheQueryOptions _cacheQueryOptions;
    private ValidationOptions _validationOptions;
    private IKeyGenerator _keyGenerator;
    private bool _hasChanges;
    #endregion
  }

  


}



