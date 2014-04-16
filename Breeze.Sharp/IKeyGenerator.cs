using System;

namespace Breeze.Sharp {

  /// <summary>
  /// Interface that generates, describes and keeps track of all of the temporary keys associated 
  /// with a single EntityManager.
  /// </summary>
  public interface IKeyGenerator {


    /// <summary>
    /// Generates a new temporary ID for a specified EntityProperty.  
    /// </summary>
    /// <param name="property">Property for which a new ID should be generated</param>
    /// <returns>A new temporary ID</returns>
    /// <remarks>The definition of a "temporary" ID is user-defined.  In the sample code for a "LongIdGenerator"
    /// negative integers are used as temporary IDs. 
    /// <para>This method should also store the temporary IDs generated in a <see cref="UniqueIdCollection"/>.
    /// </para>
    /// </remarks>
    object GetNextTempId(DataProperty property);

    /// <summary>
    /// Determines whether a given ID is temporary.
    /// </summary>
    /// <param name="uniqueId">ID to be analyzed</param>
    /// <returns>true if the ID is temporary; otherwise false</returns>
    /// <remarks>The <see cref="UniqueId.Value"/> contains the ID to be tested.
    /// You can use the <see cref="StructuralProperty.EntityType"/> property of the <see cref="UniqueId.Property"/>
    /// to determine the <see cref="IEntity"/> type.
    /// </remarks>
    bool IsTempId(UniqueId uniqueId);

    /// <summary>
    /// Returns the temporary IDs generated since instantiation of this class or the last <see cref="Reset"/>.
    /// </summary>
    UniqueIdCollection TempIds { get; }

    /// <summary>
    /// Reset temporary ID generation back to its initial state. 
    /// </summary>
    /// <remarks>Called by the <see cref="EntityManager"/> after Id fixup
    /// during <see cref="EntityManager.SaveChanges"/> processing.
    /// </remarks>
    void Reset();

  }

  /// <summary>
  /// Default implementation of IKeyGenerator that automatically generates temporary ids for most common datatypes.
  /// </summary>
  public class DefaultKeyGenerator : IKeyGenerator {

    /// <summary>
    /// Ctor.
    /// </summary>
    public DefaultKeyGenerator() {
      TempIds = new UniqueIdCollection();
    }

    /// <inheritdoc />
    public virtual object GetNextTempId(DataProperty property) {
      var nextValue = property.DataType.GetNextTempValue();
      if (nextValue == null) {
        throw new Exception("Unable to generate a temporary id for this property: " + property.Name);
      }
      TempIds.Add(new UniqueId(property, nextValue));
      return nextValue;
    }

    /// <inheritdoc />
    public bool IsTempId(UniqueId uniqueId) {
      return TempIds.Contains(uniqueId);
    }

    /// <inheritdoc />
    public UniqueIdCollection TempIds {
      get; private set; 
    }

    /// <inheritdoc />
    public void Reset() {
      TempIds.Clear();
    }
  }


}
