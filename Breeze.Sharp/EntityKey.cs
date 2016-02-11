using Breeze.Sharp.Core;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace Breeze.Sharp {
  /// <summary>
  /// Represents the primary key for an <see cref="IEntity"/>.
  /// </summary>
  public class EntityKey : IComparable, IJsonSerializable  {

    // used by EntityKey.Create
    private EntityKey() {

    }

    /// <summary>
    /// Constructs a new EntityKey. Each entity within an EntityManager will have a unique EntityKey.
    /// </summary>
    public EntityKey(Type clrType, MetadataStore metadataStore, params Object[] values) {
      ClrType = clrType;
      EntityType = metadataStore.GetEntityType(ClrType);
      InitializeValues(values, true);
    }

    /// <summary>
    /// Constructs a new EntityKey. Each entity within an EntityManager will have a unique EntityKey.
    /// </summary>
    /// <param name="entityType">The Entity type</param>
    /// <param name="values">The values of the primary key properties</param>
    public EntityKey(EntityType entityType, params Object[] values) {
      EntityType = entityType;
      ClrType = EntityType.ClrType;
      InitializeValues(values, true);
    }

    internal EntityKey(JNode jn, MetadataStore metadataStore) {
      var etName = jn.Get<String>("entityType");
      EntityType = metadataStore.GetEntityType(etName);
      ClrType = EntityType.ClrType;
      // coerce the incoming data
      Values = jn.GetArray("values", EntityType.KeyProperties.Select(kp => kp.ClrType)).ToArray();
    }

    // should be called internally when we don't need to go thru coercion.
    internal static EntityKey Create(EntityType entityType, params Object[] values) {
      var ek = new EntityKey();
      ek.EntityType = entityType;
      ek.ClrType = entityType.ClrType;
      ek.InitializeValues(values, false);
      return ek;
    }

    private void InitializeValues(Object[] values, bool shouldCoerce = true) {
      if (!EntityType.KeyProperties.Any()) {
        var msg = String.Format("There are no KeyProperties yet defined on EntityType: '{0}'.  "
                                + "Please insure that the metadata for this type is complete either by calling FetchMetadata or "
                                + "by explicitly updating the KeyProperties before creating an EntityKey for this type.",
          EntityType.Name);
        throw new Exception(msg);
      }

      if (values.Length == 1 && values[0] is Array) {
        Values = ((IEnumerable)values[0]).Cast<Object>().ToArray();
      } else {
        Values = values;
      }

      if (shouldCoerce) {
        Coerce();
      }
    }

    

     JNode IJsonSerializable.ToJNode(object config) {
      var jn = new JNode();
      jn.AddPrimitive("entityType", this.EntityType.Name);
      jn.AddArray("values", this.Values);

      return jn;
    }
   
    public Type ClrType {
      get;
      private set;
    }

    /// <summary>
    /// The <see cref="IEntity"/> type associated with this primary key.
    /// </summary>
    public EntityType EntityType {
      get;
      private set;
    }

    /// <summary>
    /// An array of values associated with individual properties of the key.
    /// </summary>
    public Object[] Values {
      get;
      internal set;
    }

    /// <summary>
    /// Constructs an untyped EntityQuery for a single entity based on this EntityKey.
    /// </summary>
    /// <returns></returns>
    public EntityQuery ToQuery() {
      return EntityQueryBuilder.BuildQuery(this);
    }

    /// <summary>
    /// Constructs a typed EntityQuery for a single entity based on this EntityKey.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <returns></returns>
    public EntityQuery<T> ToQuery<T>() {
      return (EntityQuery<T>) EntityQueryBuilder.BuildQuery(this);
    }

    internal bool IsEmpty() {
      return Values == null || Values.Length == 0 || Values.Any(v => v==null) ;
    }

    private void Coerce() {
      for (int i = 0; i < Values.Length; i++) {
        var clrType = EntityType.KeyProperties[i].ClrType;
        var val = Values[i];
        if (val == null) continue;
        if (clrType != val.GetType()) {
          Values[i] = TypeFns.ConvertType(val, clrType, true);
        }
      }
    }

    /// <summary>
    /// Determines whether two primary keys refer to the same entity.
    /// </summary>
    /// <param name="obj"></param>
    /// <returns></returns>
    public override Boolean Equals(Object obj) {
      if ((Object)this == obj) return true;
      var other = obj as EntityKey;
      if (other == null) return false;
      if (!ClrType.Equals(other.ClrType)) return false;
      if (!Values.SequenceEqual(other.Values)) return false;
      return true;
    }

    /// <summary>
    /// See <see cref="IComparable.CompareTo"/>.
    /// </summary>
    /// <param name="obj"></param>
    /// <returns></returns>
    public virtual int CompareTo(Object obj) {
      if ((Object)this == obj) return 0;
      var other = obj as EntityKey;
      if (other == null) return -1;
      int result = -1;
      for (int i = 0; i < this.Values.GetLength(0); i++) {
        result = this.Values[i].ToString().CompareTo(other.Values[i].ToString());
        if (result != 0) return result;
      }
      return result;
    }

    /// <summary>
    /// See <see cref="Object.GetHashCode"/>.
    /// </summary>
    /// <returns></returns>
    public override int GetHashCode() {
      int hashCode = ClrType.GetHashCode();
      foreach (Object item in Values) {
        if (item == null) continue;
        hashCode ^= item.GetHashCode();
      }
      return hashCode;
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="a"></param>
    /// <param name="b"></param>
    /// <returns></returns>
    public static bool operator ==(EntityKey a, EntityKey b) {
      // If both are null, or both are same instance, return true.

      if (System.Object.ReferenceEquals(a, b)) {
        return true;
      }

      // If one is null, but not both, return false.
      if (((object)a == null) || ((object)b == null)) {
        return false;
      }

      return a.Equals(b);
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="a"></param>
    /// <param name="b"></param>
    /// <returns></returns>
    public static bool operator !=(EntityKey a, EntityKey b) {
      return !(a == b);
    }

    /// <summary>
    /// Returns a human readable representation of this Primary Key.
    /// </summary>
    /// <returns></returns>
    public override String ToString() {
      return ClrType.Name + ": " + Values.ToAggregateString(",");
    }


    //// do not need to serialize this.
    //internal EntityKey BasemostEntityKey {
    //  get {
    //    if (_baseMostEntityKey == null) {
    //      _baseMostEntityKey = GetBasemostEntityKey(this);
    //    }
    //    return _baseMostEntityKey;
    //  }
    //}
    //private EntityKey _baseMostEntityKey;

    //private EntityKey GetBasemostEntityKey(EntityKey parentEntityKey) {
    //  var baseType = EntityMetadata.GetBaseEntitySubtype(parentEntityKey.EntityType);
    //  if (baseType != parentEntityKey.EntityType) {
    //    parentEntityKey = new EntityKey(baseType, parentEntityKey.Values);
    //  }
    //  return parentEntityKey;
    //}

   

  
  }

}
