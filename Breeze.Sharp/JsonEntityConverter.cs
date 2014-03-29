
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Linq;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;

using Breeze.Sharp.Core;

namespace Breeze.Sharp {

  public class MappingContext {
    public MappingContext() {
      RefMap = new Dictionary<string, object>();
      Entities = new List<IEntity>();
    }
    public EntityManager EntityManager;
    public MergeStrategy MergeStrategy;
    public LoadingOperation LoadingOperation;
    // AllEntities is a list of all deserialized entities not just the top level ones.
    public List<IEntity> Entities { get; private set; }
    public Dictionary<String, Object> RefMap { get; private set; }
  }

  public enum LoadingOperation {
    Query,
    Save
    // Import - not yet needed
    // Attach - not yet needed
  }

  public class JsonEntityConverter : JsonConverter {
  
    // currently the normalizeTypeNmFn is only needed during saves, not during queries. 
    public JsonEntityConverter(MappingContext mappingContext) {
      _mappingContext = mappingContext;
    }

    
    public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer) {
      if (reader.TokenType != JsonToken.Null) {
        // Load JObject from stream
        var jObject = JObject.Load(reader);

        var jsonContext = new JsonContext { JObject = jObject, ObjectType = objectType, Serializer = serializer };
        // Create target object based on JObject
        var target = CreateAndPopulate( jsonContext);
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


    protected virtual Object CreateAndPopulate(JsonContext jsonContext) {
      var jObject = jsonContext.JObject;

      JToken refToken = null;
      if (jObject.TryGetValue("$ref", out refToken)) {
        return _mappingContext.RefMap[refToken.Value<String>()];
      }

      EntityType entityType;
      Type objectType;
      JToken typeToken = null;
      if (jObject.TryGetValue("$type", out typeToken)) {
        var clrTypeName = typeToken.Value<String>();
        var serverTypeInfo = TypeNameInfo.FromClrTypeName(clrTypeName);
        var clientEntityTypeName = serverTypeInfo.ToClient().Name; 
        entityType = MetadataStore.Instance.GetEntityType(clientEntityTypeName);
        objectType = entityType.ClrType;
        if (!jsonContext.ObjectType.IsAssignableFrom(objectType)) {
          throw new Exception("Unable to convert returned type: " + objectType.Name + " into type: " + jsonContext.ObjectType.Name);
        }
        jsonContext.ObjectType = objectType;
      } else {
        objectType = jsonContext.ObjectType;
        entityType =  MetadataStore.Instance.GetEntityType(objectType);
      }

      // an entity type
      jsonContext.StructuralType = entityType;
      var keyValues = entityType.KeyProperties
        .Select(p => jObject[p.Name].ToObject(p.ClrType))
        .ToArray();
      var entityKey = EntityKey.Create(entityType, keyValues);
      var entity = _mappingContext.EntityManager.GetEntityByKey(entityKey);
      if (entity == null) {
        entity = (IEntity)Activator.CreateInstance(objectType);
      }
      // must be called before populate
      UpdateRefMap(jObject, entity);
      _mappingContext.Entities.Add(entity);
      return PopulateEntity(jsonContext, entity);

    }

    private void UpdateRefMap(JObject jObject, Object target) {
      JToken idToken = null;
      if (jObject.TryGetValue("$id", out idToken)) {
        _mappingContext.RefMap[idToken.Value<String>()] = target;
      }
    }

    protected virtual Object PopulateEntity(JsonContext jsonContext, IEntity entity) {
      
      var aspect = entity.EntityAspect;
      if (aspect.EntityManager == null) {
        // new to this entityManager
        ParseObject(jsonContext, aspect);
        aspect.Initialize();
        // TODO: This is a nit.  Wierd case where a save adds a new entity will show up with
        // a AttachOnQuery operation instead of AttachOnSave
        _mappingContext.EntityManager.AttachQueriedEntity(entity, (EntityType) jsonContext.StructuralType);
      } else if (_mappingContext.MergeStrategy == MergeStrategy.OverwriteChanges || aspect.EntityState == EntityState.Unchanged) {
        // overwrite existing entityManager
        ParseObject(jsonContext, aspect);
        aspect.Initialize();
        aspect.OnEntityChanged(_mappingContext.LoadingOperation == LoadingOperation.Query ? EntityAction.MergeOnQuery : EntityAction.MergeOnSave);
      } else {
        // preserveChanges handling - we still want to handle expands.
        ParseObject(jsonContext, null );
      }

      return entity;
    }

    


    private void ParseObject(JsonContext jsonContext, EntityAspect targetAspect) {
      // backingStore will be null if not allowed to overwrite the entity.
      var backingStore = (targetAspect == null) ? null : targetAspect.BackingStore;
      var dict = (IDictionary<String, JToken>) jsonContext.JObject;
      var structuralType = jsonContext.StructuralType;
      dict.ForEach(kvp => {
        var key = kvp.Key;
        var prop = structuralType.GetProperty(key);
        if (prop != null) {         
          if (prop.IsDataProperty) {
            if (backingStore != null) {
              var dp = (DataProperty)prop;
              if (dp.IsComplexProperty) {
                var newCo = (IComplexObject) kvp.Value.ToObject(dp.ClrType);
                var co = (IComplexObject)backingStore[key];
                var coBacking = co.ComplexAspect.BackingStore;
                newCo.ComplexAspect.BackingStore.ForEach(kvp2 => {
                  coBacking[kvp2.Key] = kvp2.Value;
                });
              } else {
                backingStore[key] = kvp.Value.ToObject(dp.ClrType);
              }
            }
          } else {
            // prop is a ComplexObject
            var np = (NavigationProperty)prop;
            
            if (kvp.Value.HasValues) {
              JsonContext newContext;
              if (np.IsScalar) {
                var nestedOb = (JObject)kvp.Value;
                newContext = new JsonContext() { JObject = nestedOb, ObjectType = prop.ClrType, Serializer = jsonContext.Serializer }; 
                var entity = (IEntity)CreateAndPopulate(newContext);
                if (backingStore != null) backingStore[key] = entity;
              } else {
                var nestedArray = (JArray)kvp.Value;
                var navSet = (INavigationSet) TypeFns.CreateGenericInstance(typeof(NavigationSet<>), prop.ClrType);
                
                nestedArray.Cast<JObject>().ForEach(jo => {
                  newContext = new JsonContext() { JObject=jo, ObjectType = prop.ClrType, Serializer = jsonContext.Serializer };
                  var entity = (IEntity)CreateAndPopulate(newContext);
                  navSet.Add(entity);
                });
                // add to existing nav set if there is one otherwise just set it. 
                object tmp;
                if (backingStore.TryGetValue(key, out tmp)) {
                  var backingNavSet = (INavigationSet) tmp;
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

    protected class JsonContext {
      public JObject JObject;
      public Type ObjectType;
      public StructuralType StructuralType;
      public JsonSerializer Serializer;
    }

    private MappingContext _mappingContext;
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

