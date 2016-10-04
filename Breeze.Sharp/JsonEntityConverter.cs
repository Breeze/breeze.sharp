
using System.Reflection;

using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Linq;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;

using Breeze.Sharp.Core;
using System.Xml;

namespace Breeze.Sharp {

  /// <summary>
  /// Used by the <see cref="JsonResultsAdapter"/> to provide information regarding the overall context 
  /// of the currently executing operation.
  /// </summary>
  public class MappingContext {
    public MappingContext() {
      RefMap = new Dictionary<string, object>();
      Entities = new List<IEntity>();
    }
    public EntityManager EntityManager;
    public MergeStrategy MergeStrategy;
    public LoadingOperation LoadingOperation;
    public IJsonResultsAdapter JsonResultsAdapter;
    // AllEntities is a list of all deserialized entities not just the top level ones.
    public List<IEntity> Entities { get; private set; }
    public JsonSerializer Serializer { get; internal set; }
    public Dictionary<String, Object> RefMap { get; private set; }

    public MetadataStore MetadataStore {
      get { return EntityManager.MetadataStore; }
    }

    public JsonNodeInfo VisitNode(NodeContext nodeContext) {
      return JsonResultsAdapter.VisitNode(nodeContext.Node, this, nodeContext);
    }
  }

  /// <summary>
  /// Used by the <see cref="IJsonResultsAdapter"/> to provide information about the current node being processed. 
  /// </summary>
  public class NodeContext {
    public JObject Node;
    public Type ObjectType;
    public StructuralType StructuralType;
    public StructuralProperty StructuralProperty;
  }

  /// <summary>
  /// Enum that is used to describe the current operation being performed while
  /// a JsonResultsAdapter is executing.  Referenced by <see cref="MappingContext"/>
  /// </summary>
  public enum LoadingOperation {
    Query,
    Save
    // Import - not yet needed
    // Attach - not yet needed
  }

  public class TimeSpanConverter : JsonConverter {
    public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer) {
      var ts = (TimeSpan)value;
      var tsString = XmlConvert.ToString(ts);
      serializer.Serialize(writer, tsString);
    }

    public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer) {
      if (reader.TokenType == JsonToken.Null) {
        return null;
      }

      var value = serializer.Deserialize<String>(reader);
      return XmlConvert.ToTimeSpan(value);
    }

    public override bool CanConvert(Type objectType) {
      return objectType == typeof(TimeSpan) || objectType == typeof(TimeSpan?);
    }
  }

  /// <summary>
  /// For internal use only.
  /// </summary>
  public class JsonEntityConverter : JsonConverter {

    // currently the normalizeTypeNmFn is only needed during saves, not during queries. 
    public JsonEntityConverter(MappingContext mappingContext) {
      _mappingContext = mappingContext;
      _customSerializer = new JsonSerializer();
      _customSerializer.Converters.Add(new StringEnumConverter());
      _customSerializer.Converters.Add(new TimeSpanConverter());
    }

    public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer) {
      if (reader.TokenType != JsonToken.Null) {
        // Load JObject from stream
        var node = JObject.Load(reader);

        var nodeContext = new NodeContext { Node = node, ObjectType = objectType };
        // Create target object based on JObject
        var target = CreateAndPopulate(nodeContext);
        return target;
      } else {
        return null;
      }
    }

    public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer) {
      throw new NotImplementedException();
    }

    public override bool CanConvert(Type objectType) {
      return MetadataStore.IsStructuralType(objectType);
    }

    protected virtual Object CreateAndPopulate(NodeContext nodeContext) {
      var node = nodeContext.Node;

      var nodeInfo = _mappingContext.VisitNode(nodeContext);
      if (nodeInfo.Ignore) return null;

      node = nodeInfo.Node ?? node;

      if (nodeInfo.NodeRefId != null) {
        return _mappingContext.RefMap[nodeInfo.NodeRefId];
      }
      var metadataStore = _mappingContext.MetadataStore;
      EntityType entityType;
      Type objectType;
      if (nodeInfo.ServerTypeNameInfo != null) {
        var clientEntityTypeName = nodeInfo.ServerTypeNameInfo.ToClient(metadataStore).StructuralTypeName;
        entityType = metadataStore.GetEntityType(clientEntityTypeName);
        objectType = entityType.ClrType;
        if (!nodeContext.ObjectType.IsAssignableFrom(objectType)) {
          throw new Exception("Unable to convert returned type: " + objectType.Name + " into type: " +
                              nodeContext.ObjectType.Name);
        }
        nodeContext.ObjectType = objectType;
      } else {
        objectType = nodeContext.ObjectType;
        entityType = metadataStore.GetEntityType(objectType);
      }

      // an entity type
      nodeContext.StructuralType = entityType;
      var keyValues = entityType.KeyProperties
        .Select(p => node[p.NameOnServer].ToObject(p.ClrType))
        .ToArray();
      var entityKey = EntityKey.Create(entityType, keyValues);
      var entity = _mappingContext.EntityManager.GetEntityByKey(entityKey);
      if (entity == null) {
        entity = (IEntity)Activator.CreateInstance(objectType);
        entity.EntityAspect.EntityType = entityType;
      }
      // must be called before populate
      if (nodeInfo.NodeId != null) {
        _mappingContext.RefMap[nodeInfo.NodeId] = entity;
      }

      _mappingContext.Entities.Add(entity);
      return PopulateEntity(nodeContext, entity);
    }


    protected virtual Object PopulateEntity(NodeContext nodeContext, IEntity entity) {

      var aspect = entity.EntityAspect;
      if (aspect.EntityManager == null) {
        // new to this entityManager
        ParseObject(nodeContext, aspect);
        aspect.Initialize();
        // TODO: This is a nit.  Wierd case where a save adds a new entity will show up with
        // a AttachOnQuery operation instead of AttachOnSave
        _mappingContext.EntityManager.AttachQueriedEntity(entity, (EntityType)nodeContext.StructuralType);
      } else if (_mappingContext.MergeStrategy == MergeStrategy.OverwriteChanges || aspect.EntityState == EntityState.Unchanged) {
        // overwrite existing entityManager
        ParseObject(nodeContext, aspect);
        aspect.Initialize();
        aspect.OnEntityChanged(_mappingContext.LoadingOperation == LoadingOperation.Query ? EntityAction.MergeOnQuery : EntityAction.MergeOnSave);
      } else {
        // preserveChanges handling - we still want to handle expands.
        ParseObject(nodeContext, null);
      }

      return entity;
    }

    private void ParseObject(NodeContext nodeContext, EntityAspect targetAspect) {
      // backingStore will be null if not allowed to overwrite the entity.
      var backingStore = (targetAspect == null) ? null : targetAspect.BackingStore;
      var dict = (IDictionary<String, JToken>)nodeContext.Node;
      var structuralType = nodeContext.StructuralType;
      // needs to be the current namingConvention
      var nc = _mappingContext.EntityManager.MetadataStore.NamingConvention;
      dict.ForEach(kvp => {
        var key = nc.ServerPropertyNameToClient(kvp.Key, structuralType);
        var prop = structuralType.GetProperty(key);
        if (prop != null) {
          if (prop.IsDataProperty) {
            if (backingStore != null) {
              var dp = (DataProperty)prop;
              if (dp.IsComplexProperty) {
                var newCo = (IComplexObject)kvp.Value.ToObject(dp.ClrType);
                var co = (IComplexObject)backingStore[key];
                var coBacking = co.ComplexAspect.BackingStore;
                newCo.ComplexAspect.BackingStore.ForEach(kvp2 => {
                  coBacking[kvp2.Key] = kvp2.Value;
                });
              } else {
                var val = kvp.Value;
                if (val.Type == JTokenType.Null && dp.ClrType != typeof(String) && !TypeFns.IsNullableType(dp.ClrType)) {
                  // this can only happen if the client is nonnullable but the server is nullable.
                  backingStore[key] = dp.DefaultValue;
                } else if (dp.IsEnumType || (dp.DataType.ClrType == typeof(TimeSpan))) {
                  backingStore[key] = val.ToObject(dp.ClrType, _customSerializer);
                } else {
                  var newValue = val.ToObject(dp.ClrType);
                  if (dp.IsForeignKey && targetAspect != null) {
                    var oldValue = targetAspect.GetValue(key);
                    backingStore[key] = newValue;
                    targetAspect.UpdateRelated(dp, newValue, oldValue);
                    // Above is like next line but with fewer side effects
                    // targetAspect.SetValue(key, val.ToObject(dp.ClrType));
                  } else {
                    backingStore[key] = newValue;
                  }
                }
              }
            }
          } else {
            // prop is a ComplexObject
            var np = (NavigationProperty)prop;

            if (kvp.Value.HasValues) {
              NodeContext newContext;
              if (np.IsScalar) {
                var nestedOb = (JObject)kvp.Value;
                newContext = new NodeContext() { Node = nestedOb, ObjectType = prop.ClrType, StructuralProperty = np };
                var entity = (IEntity)CreateAndPopulate(newContext);
                if (backingStore != null) backingStore[key] = entity;
              } else {
                var nestedArray = (JArray)kvp.Value;
                var navSet = (INavigationSet)TypeFns.CreateGenericInstance(typeof(NavigationSet<>), prop.ClrType);

                nestedArray.Cast<JObject>().ForEach(jo => {
                  newContext = new NodeContext() { Node = jo, ObjectType = prop.ClrType, StructuralProperty = np };
                  var entity = (IEntity)CreateAndPopulate(newContext);
                  navSet.Add(entity);
                });
                // add to existing nav set if there is one otherwise just set it. 
                object tmp;
                if (backingStore.TryGetValue(key, out tmp)) {
                  var backingNavSet = (INavigationSet)tmp;
                  navSet.Cast<IEntity>().ForEach(e => backingNavSet.Add(e));
                } else {
                  navSet.NavigationProperty = np;
                  navSet.ParentEntity = targetAspect.Entity;
                  backingStore[key] = navSet;
                }
              }
            } else {
              // do nothing
              //if (!np.IsScalar) {
              //  return TypeFns.ConstructGenericInstance(typeof(NavigationSet<>), prop.ClrType);
              //} else {
              //  return null;
              //}
            }
          }
        } else {
          if (backingStore != null) backingStore[key] = kvp.Value.ToObject<Object>();
        }
      });

    }



    private MappingContext _mappingContext;
    private JsonSerializer _customSerializer;
  }



  //public static class JsonFns {

  //  public static JsonSerializerSettings SerializerSettings {
  //    get {
  //      var settings = new JsonSerializerSettings() {

  //        NullValueHandling = NullValueHandling.Include,
  //        PreserveReferencesHandling = PreserveReferencesHandling.Objects,
  //        ReferenceLoopHandling = ReferenceLoopHandling.Serialize,
  //        TypeNameHandling = TypeNameHandling.Objects,
  //        TypeNameAssemblyFormat = FormatterAssemblyStyle.Simple,
  //      };
  //      settings.Converters.Add(new IsoDateTimeConverter());
  //      settings.Converters.Add(new JsonEntityConverter(em));
  //      return settings;
  //    }
  //  }
  //}

}

