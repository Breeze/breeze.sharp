using Breeze.Sharp.Core;
using Newtonsoft.Json;     // need because of JsonIgnore attribute
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Resources;

namespace Breeze.Sharp {

  // TODO: need to figure out how to correctly serialize/deserialize any changes to the default LocalizedMessage
  // right now these changes will be lost thru serialization.


  /// <summary>
  /// Subclassed instances of the Validator class provide the logic to validate another object 
  /// and provide a description of any errors encountered during the validation process. 
  /// They are typically associated with a 'validators' property on the following types: 
  /// EntityType, DataProperty or NavigationProperty.
  /// A number of property level validators are registered automatically, i.e added to each DataProperty.Validators 
  /// property based on DataProperty metadata. For example,
  ///     DataProperty.MaxLength -> MaxLengthValidator
  ///     DataProperty.IsNullable -> RequiredValidator (if not nullable)
  /// </summary>
  /// <remarks>
  /// Validators are by convention immutable - if this convention is violated your app WILL break.
  /// You can use 'With' methods to create new Validators based on an existing validator.  For example,
  /// to change the message you might do the following: 
  ///     var newValidator = new RequiredValidator().With(new LocalizedMessage("foo"));
  /// </remarks>
  public abstract class Validator : Internable {

    public const String Suffix = "Validator";

    static Validator() {
      Configuration.Instance.ProbeAssemblies(typeof(Validator).GetTypeInfo().Assembly);
    }

    protected Validator() {
      Name = UtilFns.TypeToSerializationName(this.GetType(), Suffix);
      LocalizedMessage = new LocalizedMessage(LocalizedKey);
    }

    [JsonIgnore]
    public String LocalizedKey {
      get {
        return _localizedKey ?? "Val_" + Name;
      }
      set {
        _localizedKey = value;
      }
    }

    [JsonIgnore]
    public LocalizedMessage LocalizedMessage {
      get;
      internal protected set;
    }

    /// <summary>
    /// Performs validation given a specified context.
    /// </summary>
    /// <param name="context"></param>
    /// <returns></returns>
    public virtual ValidationError Validate(ValidationContext context) {
      return (ValidateCore(context)) ? null : new ValidationError(this, context);
    }

    /// <summary>
    /// Provides the core validation logic to this validator. 
    /// </summary>
    /// <param name="context"></param>
    /// <returns>true for success or false for failure</returns>
    protected abstract bool ValidateCore(ValidationContext context);

    /// <summary>
    /// Provides the error message associated with a failure of this validator.
    /// </summary>
    /// <param name="validationContext"></param>
    /// <returns></returns>
    public abstract String GetErrorMessage(ValidationContext validationContext);

    /// <summary>
    /// For internal use only - used during validator deserialization.
    /// </summary>
    /// <param name="jNode"></param>
    /// <returns></returns>
    public static Validator FindOrCreate(JNode jNode) {
      return Configuration.Instance.FindOrCreateValidator(jNode);
    }

    private string _localizedKey;
    

    private static Object __lock = new Object();


    private static readonly IEnumerable<ValidationError> EmptyErrors = Enumerable.Empty<ValidationError>();

    #region Not currently used 

    //public static T FindOrCreate<T>(params Object[] parameters) where T : Validator {
    //  return (T)FindOrCreate(typeof(T), parameters);
    //}

    //public static Validator FindOrCreate(Type type, params Object[] parameters) {
    //  var key = new ParamsWrapper(parameters);
    //  Validator vr;
    //  lock (__validatorParamsCache) {
    //    if (__validatorParamsCache.TryGetValue(key, out vr)) {
    //      return vr;
    //    }
    //    try {
    //      vr = (Validator)Activator.CreateInstance(type, parameters);
    //    } catch (Exception e) {
    //      throw new Exception("Unabled to create " + type.Name + " with the provided parameters", e);
    //    }
    //    __validatorParamsCache[key] = vr;
    //    return vr;
    //  }
    //}

    //private class ParamsWrapper {
    //  public ParamsWrapper(params Object[] values) {
    //    _values = values;
    //  }

    //  public override bool Equals(object obj) {
    //    if (obj == this) return true;
    //    var other = obj as ParamsWrapper;
    //    if (other == null) return false;
    //    return _values.SequenceEqual(other._values);
    //  }

    //  public override int GetHashCode() {
    //    return _values.GetAggregateHashCode();
    //  }

    //  private Object[] _values;
    //}

    //private static Dictionary<ParamsWrapper, Validator> __validatorParamsCache = new Dictionary<ParamsWrapper, Validator>();

    #endregion
  }

  /// <summary>
  /// Collection of extension methods for use with any <seealso cref="Validator"/>.
  /// </summary>
  public static class ValidatorExtensions {

    public static T WithMessage<T>(this T validator, String message) where T: Validator {
      return WithMessage(validator, new LocalizedMessage(message));
    }

    public static T WithMessage<T>(this T validator, Type resourceType) where T:Validator {
      return WithMessage(validator, new LocalizedMessage(key: validator.LocalizedKey, resourceType: resourceType));
    }

    public static T WithMessage<T>(this T validator, String baseName, Assembly assembly) where T : Validator {
      return WithMessage(validator, new LocalizedMessage(key: validator.LocalizedKey, baseName: baseName, assembly: assembly));
    }

    // returns a new Validator cloned from the original with a new localizedMessage;
    public static T WithMessage<T>(this T validator, LocalizedMessage localizedMessage) where T:Validator {
      // Deserialize the object - poor mans clone;
      var vr = (Validator) validator.ToJNode().ToObject(validator.GetType(), true);
      vr.LocalizedMessage = localizedMessage;
      return (T) vr;
    }

    public static T Intern<T>(this T validator) where T : Validator {
      return (T) Configuration.Instance.InternValidator(validator);
    }
  }

  internal class ValidatorCollection : SetCollection<Validator> {

    public ValidatorCollection() : base() { }
    public ValidatorCollection(IEnumerable<Validator> validators) : base(validators) { }
    public ValidatorCollection(IEnumerable<JNode> jNodes) {
      jNodes.Select(jn => Validator.FindOrCreate(jn))
        .Where(v => v != null)
        .ForEach(v => this.Add(v));
    }

    public override void Add(Validator item) {
      item = item.Intern();
      base.Add(item);
    }

    public override bool Remove(Validator item) {
      item = item.Intern();
      return base.Remove(item);
    }

    public override bool Contains(Validator item) {
      item = item.Intern();
      return base.Contains(item);
    }

  }
}
