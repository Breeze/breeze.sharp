using Breeze.Sharp.Core;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace Breeze.Sharp {

  /// <summary>
  /// SaveExceptions are thrown whenever an <see cref="EntityManager.SaveChanges(SaveOptions)"/> call fails for any reason.
  /// </summary>
  public class SaveException : Exception {

	private static readonly ReadOnlyCollection<EntityError> emptyErrors = new ReadOnlyCollection<EntityError>(new EntityError[0]); 

    public SaveException(EntityManager em, String json) : 
      this(em, JNode.DeserializeFrom(json)) {
    }
    
    internal SaveException(EntityManager em, JNode jn) 
      : base() {
      _message = jn.Get<String>("ExceptionMessage") ?? "see EntityErrors";
      var entityErrors = jn.GetArray<EntityError>("Errors", "errors", "EntityErrors", "entityErrors");
      entityErrors = entityErrors.Select(ee => ee.Resolve(em));
      _entityErrors = new SafeList<EntityError>(entityErrors);
      IsServerError = true;
    }


    public SaveException(String message, Exception innerException) : base(message, innerException) {
      _message = message;
    }

    public SaveException(IEnumerable<ValidationError> validationErrors) : base() {
      _message = "ValidationErrors encountered - see the ValidationErrors property";
      _validationErrors = validationErrors;
      IsServerError = false;
    }

    public override string Message {
	    get { return _message; }
    }

    public ReadOnlyCollection<EntityError> EntityErrors {
	    get
	    {
		    if (_entityErrors == null)
			    return emptyErrors;
		    return _entityErrors.ReadOnlyValues;
	    }
    }

    public IEnumerable<ValidationError> ValidationErrors {
      get { return _validationErrors; }
    }

    public bool IsServerError { get; private set; }

    private string _message;
    private SafeList<EntityError> _entityErrors;
    private IEnumerable<ValidationError> _validationErrors;
  }

  /// <summary>
  /// Information about any errors encountered on a specific entity during the course of a 
  /// EntityManager.SaveChanges operation.  A collection of EntityError instances is part
  /// of every <see cref="SaveException"/>
  /// </summary>
  public class EntityError {

    public String ErrorName;
    public String EntityTypeName;
    public Object[] KeyValues;
    public String PropertyName;
    public string ErrorMessage;

    // set by Resolve
    public IEntity Entity;
    public bool IsServerError;
    public StructuralProperty Property;

    public EntityError Resolve(EntityManager em) {
      IsServerError = true;
      try {
        EntityType entityType = null;
        if (EntityTypeName != null) {
          var stName = TypeNameInfo.FromClrTypeName(EntityTypeName).ToClient(em.MetadataStore).StructuralTypeName;
          entityType = em.MetadataStore.GetEntityType(stName);
          var ek = new EntityKey(entityType, KeyValues);
          Entity = em.GetEntityByKey(ek);
        }

        
        if (entityType != null) {
          if (PropertyName != null) {
            Property = entityType.Properties.FirstOrDefault(p => p.NameOnServer == PropertyName);
            if (Property != null) {
              PropertyName = Property.Name;
            }
          }
          
          var vc = new ValidationContext(this.Entity);
          vc.Property = Property;
          var veKey = (ErrorName ?? ErrorMessage) + (PropertyName ?? "");
          var ve = new ValidationError(null, vc, ErrorMessage, veKey);
          ve.IsServerError = true;
          this.Entity.EntityAspect.ValidationErrors.Add(ve);
        }
      } catch (Exception e) {
        ErrorMessage = ( ErrorMessage ?? "") + ":  Unable to Resolve this error: " + e.Message;
      }
      return this;
    }
 }
 
}
