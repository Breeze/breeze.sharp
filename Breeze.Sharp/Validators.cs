using System;
using System.Collections.Generic;
using System.Globalization;
using System.Resources;

namespace Breeze.Sharp {

  /// <summary>
  /// Validator implementation to check that a property is not set to null.
  /// Special handling for empty strings is supported.
  /// </summary>
  public class RequiredValidator : Validator {
    /// <summary>
    /// Ctor.
    /// </summary>
    /// <param name="treatEmptyStringAsNull">Whether to treat empty string as null.  If not set, then the static 
    /// <see cref="RequiredValidator.DefaultTreatEmptyStringAsNull" /> is used instead.</param>
    public RequiredValidator(bool? treatEmptyStringAsNull = null) : base() {
      LocalizedMessage = new LocalizedMessage(LocalizedKey, (ResourceManager) null);
      TreatEmptyStringAsNull = treatEmptyStringAsNull.HasValue ? treatEmptyStringAsNull.Value : DefaultTreatEmptyStringAsNull;
    }

    public static bool DefaultTreatEmptyStringAsNull {
      get {
        return __defaultTreatEmptyStringAsNull;
      }
      set {
        __defaultTreatEmptyStringAsNull = value;
      }
    }

    /// <inheritdoc />
    protected override bool ValidateCore(ValidationContext context) {
      var value = context.PropertyValue;
      if (value == null) return false;
      if (value.GetType() == typeof(String) && String.IsNullOrEmpty((String)value) && TreatEmptyStringAsNull) {
        return false;
      }
      return true;
    }

    /// <inheritdoc />
    public override String GetErrorMessage(ValidationContext context) {
      return LocalizedMessage.Format(context.DisplayName);
    }

    public bool TreatEmptyStringAsNull {
      get;
      private set;
    }

    private static bool __defaultTreatEmptyStringAsNull = true;
  }

  /// <summary>
  /// Validator implementation that checks that a string does not exceed a maximum length.
  /// </summary>
  public class MaxLengthValidator : Validator {
    /// <summary>
    /// Ctor.
    /// </summary>
    /// <param name="maxLength"></param>
    public MaxLengthValidator(int maxLength) : base() {
      LocalizedMessage = new LocalizedMessage(LocalizedKey, (ResourceManager)null);
      MaxLength = maxLength;
    }

    /// <inheritdoc />
    protected override bool ValidateCore(ValidationContext context) {
      var value = context.PropertyValue;
      if (value == null) return true;
      return ((String)value).Length <= MaxLength;
    }

    /// <inheritdoc />
    public override String GetErrorMessage(ValidationContext context) {
      // '{0}' must be {1} character(s) or less
      return LocalizedMessage.Format(context.DisplayName, MaxLength);
    }

    public int MaxLength { get; private set; }
  }

  /// <summary>
  /// Validator implementation that checks that a string's length is within a specified range.
  /// </summary>
  public class StringLengthValidator : Validator {
    public StringLengthValidator(int minLength, int maxLength) : base() {
      LocalizedMessage = new LocalizedMessage(LocalizedKey, (ResourceManager)null);
      MinLength = minLength;
      MaxLength = maxLength;
    }
    /// <inheritdoc />
    protected override bool ValidateCore(ValidationContext context) {
      var value = context.PropertyValue;
      if (value == null) return true;
      var length = ((String) value).Length;
      return length <= MaxLength & length >= MinLength;
    }
    /// <inheritdoc />
    public override String GetErrorMessage(ValidationContext context) {
      // '{0}' must be between {1} and {2} character(s)
      return LocalizedMessage.Format(context.DisplayName, MinLength, MaxLength);
    }

    public int MinLength { get; private set; }
    public int MaxLength { get; private set; }
  }

  /// <summary>
  /// Validator implementation that checks that a number falls within a specified range..
  /// </summary>
  /// <typeparam name="T"></typeparam>
  public class RangeValidator<T> : Validator where T:struct  {
    public RangeValidator(T min, T max, bool includeMinEndpoint = true, bool includeMaxEndpoint = true) : base() {
      LocalizedMessage = new LocalizedMessage(LocalizedKey, (ResourceManager)null);
      Min = min;
      Max = max;
      IncludeMinEndpoint = includeMinEndpoint;
      IncludeMaxEndpoint = includeMinEndpoint;
    }
    /// <inheritdoc />
    protected override bool ValidateCore(ValidationContext context) {
      var val = context.PropertyValue;
      T value = (T)Convert.ChangeType(val, typeof(T), CultureInfo.CurrentCulture);

      bool ok = true;

      if (IncludeMinEndpoint) {
        ok = Comparer<T>.Default.Compare(value, Min) >= 0;
      } else {
        ok = Comparer<T>.Default.Compare(value, Min) > 0;
      }
      
      if (!ok) return false;

      if (IncludeMaxEndpoint) {
        ok = (Comparer<T>.Default.Compare(value, Max) <= 0);
      } else {
        ok = (Comparer<T>.Default.Compare(value, Max) < 0);
      }

      return ok;
    }
    /// <inheritdoc />
    public override String GetErrorMessage(ValidationContext context) {
      // '{0}' must be {1} {2} and {3} {4}"
      var minPhrase = IncludeMinEndpoint ? ">=" : ">";
      var maxPhrase = IncludeMaxEndpoint ? "<=" : "<";
      return LocalizedMessage.Format(context.DisplayName, minPhrase, Min, maxPhrase, Max);
    }

    public T Min {
      get;
      private set;
    }

    public bool IncludeMinEndpoint {
      get;
      private set;
    }

    public T Max {
      get;
      private set;
    }

    public bool IncludeMaxEndpoint {
      get;
      private set;
    }

  }

  /// <summary>
  /// Specialization of the <see cref="RangeValidator{T}"/> for Int32's.
  /// </summary>
  public class Int32RangeValidator : RangeValidator<Int32> {
     public Int32RangeValidator(Int32 min, Int32 max, bool includeMinEndpoint = true, bool includeMaxEndpoint = true) 
       :base(min, max, includeMinEndpoint, includeMaxEndpoint) {
    }
  }

  #region Unused Validators
  //public class PrimitiveTypeValidator<T> : Validator {
  //  public PrimitiveTypeValidator(Type type) {
  //    ValidationType = type;
  //  }

  //  protected override bool ValidateCore(ValidationContext context) {
  //    var value = context.PropertyValue;
  //    if (value == null) return true;
  //    if (value.GetType() == ValidationType) return true;
  //    return true;
  //  }

  //  public override String GetErrorMessage(ValidationContext context) {
  //    return FormatMessage("'{0}' value is not of type: {1}.", context.DisplayName, ValidationType.Name);
  //  }

  //  public Type ValidationType { get; private set; }
  //}
  #endregion
}
