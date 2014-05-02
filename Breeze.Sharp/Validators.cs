using System;
using System.Collections.Generic;
using System.Globalization;
using System.Resources;
using System.Text.RegularExpressions;

using Microsoft.Data.OData.Query.SemanticAst;

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
      TreatEmptyStringAsNull = treatEmptyStringAsNull.HasValue
        ? treatEmptyStringAsNull.Value
        : DefaultTreatEmptyStringAsNull;
    }

    public static bool DefaultTreatEmptyStringAsNull {
      get { return __defaultTreatEmptyStringAsNull; }
      set { __defaultTreatEmptyStringAsNull = value; }
    }

    /// <inheritdoc />
    protected override bool ValidateCore(ValidationContext context) {
      var value = context.PropertyValue;
      if (value == null) return false;
      if (value.GetType() == typeof (String) && String.IsNullOrEmpty((String) value) && TreatEmptyStringAsNull) {
        return false;
      }
      return true;
    }

    /// <inheritdoc />
    public override String GetErrorMessage(ValidationContext context) {
      return LocalizedMessage.Format(context.DisplayName);
    }

    public bool TreatEmptyStringAsNull { get; private set; }

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
      LocalizedMessage = new LocalizedMessage(LocalizedKey, (ResourceManager) null);
      MaxLength = maxLength;
    }

    /// <inheritdoc />
    protected override bool ValidateCore(ValidationContext context) {
      var value = context.PropertyValue;
      if (value == null) return true;
      return ((String) value).Length <= MaxLength;
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
      LocalizedMessage = new LocalizedMessage(LocalizedKey, (ResourceManager) null);
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
  public class RangeValidator<T> : Validator where T : struct {
    public RangeValidator(T min, T max, bool includeMinEndpoint = true, bool includeMaxEndpoint = true) : base() {
      LocalizedMessage = new LocalizedMessage(LocalizedKey, (ResourceManager) null);
      Min = min;
      Max = max;
      IncludeMinEndpoint = includeMinEndpoint;
      IncludeMaxEndpoint = includeMinEndpoint;
    }

    /// <inheritdoc />
    protected override bool ValidateCore(ValidationContext context) {
      var val = context.PropertyValue;
      T value = (T) Convert.ChangeType(val, typeof (T), CultureInfo.CurrentCulture);

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

    public T Min { get; private set; }

    public bool IncludeMinEndpoint { get; private set; }

    public T Max { get; private set; }

    public bool IncludeMaxEndpoint { get; private set; }

  }

  /// <summary>
  /// Specialization of the <see cref="RangeValidator{T}"/> for Int32's.
  /// </summary>
  public class Int32RangeValidator : RangeValidator<Int32> {
    public Int32RangeValidator(Int32 min, Int32 max, bool includeMinEndpoint = true, bool includeMaxEndpoint = true)
      : base(min, max, includeMinEndpoint, includeMaxEndpoint) {
    }
  }

  /// <summary>
  /// Validator implementation that determines if a given property matches a
  /// predefined regular expression pattern.
  /// </summary>
  public class RegexValidator : Validator {
    /// <summary>
    /// Ctor.
    /// </summary>
    /// <param name="pattern"></param>
    /// <param name="patternName"></param>
    public RegexValidator(string pattern, string patternName = null)
      : this(new Regex(pattern), patternName) {
    }

    /// <summary>
    /// Ctor
    /// </summary>
    /// <param name="regex"></param>
    /// <param name="patternName"></param>
    public RegexValidator(Regex regex, string patternName = null) {
      if (patternName != null) {
        LocalizedKey = "Val_regexNamed";
      }
      LocalizedMessage = new LocalizedMessage(LocalizedKey, (ResourceManager) null);

      Regex = regex;
      PatternName = patternName;
    }

    /// <inheritdoc />
    protected override bool ValidateCore(ValidationContext context) {
      var value = context.PropertyValue;
      return value == null || Regex.IsMatch((string) value);
    }

    /// <inheritdoc />
    public override String GetErrorMessage(ValidationContext context) {
      // '{0}' must be a valid {1} pattern
      return LocalizedMessage.Format(context.DisplayName, this.PatternName ?? this.Regex.ToString());
    }

    /// <summary>
    /// Display name for this pattern in any errorMessage.
    /// </summary>
    public String PatternName;

    /// <summary>
    /// The Regex1 pattern to match.
    /// </summary>
    public Regex Regex { get; private set; }


  }

  public class EmailValidator : RegexValidator {

    // Regex from: https://github.com/srkirkland/DataAnnotationsExtensions/blob/master/DataAnnotationsExtensions

    public EmailValidator(String patternName = null) :
      base(Email, patternName) {
    }

    private static Regex Email =
      new Regex(
        @"^((([a-z]|\d|[!#\$%&'\*\+\-\/=\?\^_`{\|}~]|[\u00A0-\uD7FF\uF900-\uFDCF\uFDF0-\uFFEF])+(\.([a-z]|\d|[!#\$%&'\*\+\-\/=\?\^_`{\|}~]|[\u00A0-\uD7FF\uF900-\uFDCF\uFDF0-\uFFEF])+)*)|((\x22)((((\x20|\x09)*(\x0d\x0a))?(\x20|\x09)+)?(([\x01-\x08\x0b\x0c\x0e-\x1f\x7f]|\x21|[\x23-\x5b]|[\x5d-\x7e]|[\u00A0-\uD7FF\uF900-\uFDCF\uFDF0-\uFFEF])|(\\([\x01-\x09\x0b\x0c\x0d-\x7f]|[\u00A0-\uD7FF\uF900-\uFDCF\uFDF0-\uFFEF]))))*(((\x20|\x09)*(\x0d\x0a))?(\x20|\x09)+)?(\x22)))@((([a-z]|\d|[\u00A0-\uD7FF\uF900-\uFDCF\uFDF0-\uFFEF])|(([a-z]|\d|[\u00A0-\uD7FF\uF900-\uFDCF\uFDF0-\uFFEF])([a-z]|\d|-|\.|_|~|[\u00A0-\uD7FF\uF900-\uFDCF\uFDF0-\uFFEF])*([a-z]|\d|[\u00A0-\uD7FF\uF900-\uFDCF\uFDF0-\uFFEF])))\.)+(([a-z]|[\u00A0-\uD7FF\uF900-\uFDCF\uFDF0-\uFFEF])|(([a-z]|[\u00A0-\uD7FF\uF900-\uFDCF\uFDF0-\uFFEF])([a-z]|\d|-|\.|_|~|[\u00A0-\uD7FF\uF900-\uFDCF\uFDF0-\uFFEF])*([a-z]|[\u00A0-\uD7FF\uF900-\uFDCF\uFDF0-\uFFEF])))\.?$",
        RegexOptions.IgnoreCase);
  }

  public class PhoneNumberValidator : RegexValidator {

    // Regex from: https://github.com/srkirkland/DataAnnotationsExtensions/blob/master/DataAnnotationsExtensions

    public PhoneNumberValidator(String patternName = null) :
      base(new Regex(PhoneNumber), patternName) {
    }

    /// <summary>
    /// Matches:
    ///   International dialing prefix: {{}, +, 0, 0000} (with or without a trailing break character, if not '+': [-/. ])
    ///     > ((\+)|(0(\d+)?[-/.\s]))
    ///   Country code: {{}, 1, ..., 999} (with or without a trailing break character: [-/. ])
    ///     > [1-9]\d{,2}[-/.\s]?
    ///   Area code: {(0), ..., (000000), 0, ..., 000000} (with or without a trailing break character: [-/. ])
    ///     > ((\(\d{1,6}\)|\d{1,6})[-/.\s]?)?
    ///   Local: {0, ...}+ (with or without a trailing break character: [-/. ])
    ///     > (\d+[-/.\s]?)+\d+
    /// </summary>
    /// <remarks>
    /// This regular expression is not complete for identifying the numerous variations that exist in phone numbers.
    /// It provides basic assertions on the format and will help to eliminate most nonsense input but does not
    /// guarantee validity of the value entered for any specific geography. If greater value checking is required
    /// then consider: http://nuget.org/packages/libphonenumber-csharp.
    /// </remarks>
    private const string PhoneNumber =
      @"^((\+|(0(\d+)?[-/.\s]?))[1-9]\d{0,2}[-/.\s]?)?((\(\d{1,6}\)|\d{1,6})[-/.\s]?)?(\d+[-/.\s]?)+\d+$";

  }

  public class UrlValidator : RegexValidator {

    // Regex from: https://github.com/srkirkland/DataAnnotationsExtensions/blob/master/DataAnnotationsExtensions

    public UrlValidator(String patternName = null, UrlOptions urlOptions = UrlOptions.OptionalProtocol) :
      base(GetRegex(urlOptions), patternName) {
    }

    /// <summary>
    /// The base URL regular expression.
    /// </summary>
    /// <remarks>
    /// RFC-952 describes basic name standards: http://www.ietf.org/rfc/rfc952.txt
    /// KB 909264 describes Windows name standards: http://support.microsoft.com/kb/909264
    /// </remarks>
    private const string BaseUrlExpression =
      @"(((([a-zA-Z]|\d|-|\.|_|~|[\u00A0-\uD7FF\uF900-\uFDCF\uFDF0-\uFFEF])|(%[\da-fA-F]{2})|[!\$&'\(\)\*\+,;=]|:)*@)?(((\d|[1-9]\d|1\d\d|2[0-4]\d|25[0-5])\.(\d|[1-9]\d|1\d\d|2[0-4]\d|25[0-5])\.(\d|[1-9]\d|1\d\d|2[0-4]\d|25[0-5])\.(\d|[1-9]\d|1\d\d|2[0-4]\d|25[0-5]))|([a-zA-Z][\-a-zA-Z0-9]*)|((([a-zA-Z]|\d|[\u00A0-\uD7FF\uF900-\uFDCF\uFDF0-\uFFEF])|(([a-zA-Z]|\d|[\u00A0-\uD7FF\uF900-\uFDCF\uFDF0-\uFFEF])([a-zA-Z]|\d|-|\.|_|~|[\u00A0-\uD7FF\uF900-\uFDCF\uFDF0-\uFFEF])*([a-zA-Z]|\d|[\u00A0-\uD7FF\uF900-\uFDCF\uFDF0-\uFFEF])))\.)+(([a-zA-Z]|[\u00A0-\uD7FF\uF900-\uFDCF\uFDF0-\uFFEF])|(([a-zA-Z]|[\u00A0-\uD7FF\uF900-\uFDCF\uFDF0-\uFFEF])([a-zA-Z]|\d|-|\.|_|~|[\u00A0-\uD7FF\uF900-\uFDCF\uFDF0-\uFFEF])*([a-zA-Z]|[\u00A0-\uD7FF\uF900-\uFDCF\uFDF0-\uFFEF])))\.?)(:\d*)?)(\/((([a-zA-Z]|\d|-|\.|_|~|[\u00A0-\uD7FF\uF900-\uFDCF\uFDF0-\uFFEF])|(%[\da-fA-F]{2})|[!\$&'\(\)\*\+,;=]|:|@)+(\/(([a-zA-Z]|\d|-|\.|_|~|[\u00A0-\uD7FF\uF900-\uFDCF\uFDF0-\uFFEF])|(%[\da-fA-F]{2})|[!\$&'\(\)\*\+,;=]|:|@)*)*)?)?(\?((([a-zA-Z]|\d|-|\.|_|~|[\u00A0-\uD7FF\uF900-\uFDCF\uFDF0-\uFFEF])|(%[\da-fA-F]{2})|[!\$&'\(\)\*\+,;=]|:|@)|[\uE000-\uF8FF]|\/|\?)*)?(\#((([a-zA-Z]|\d|-|\.|_|~|[\u00A0-\uD7FF\uF900-\uFDCF\uFDF0-\uFFEF])|(%[\da-fA-F]{2})|[!\$&'\(\)\*\+,;=]|:|@)|\/|\?)*)?";

    /// <summary>
    /// The base protocol regular expression.
    /// </summary>
    private const string BaseProtocolExpression = @"(https?|ftp):\/\/";

    private static Regex GetRegex(UrlOptions urlOptions) {
      String regex;
      switch (urlOptions) {
        case UrlOptions.RequireProtocol:
          regex = @"^" + BaseProtocolExpression + BaseUrlExpression + @"$";
          break;
        case UrlOptions.OptionalProtocol:
          regex = @"^(" + BaseProtocolExpression + @")?" + BaseUrlExpression + @"$";
          break;
        case UrlOptions.DisallowProtocol:
          regex = @"^" + BaseUrlExpression + @"$";
          break;
        default:
          throw new ArgumentOutOfRangeException("urlOptions");
      }
      return new Regex(regex);
    }

  }

  public enum UrlOptions {
    RequireProtocol,
    OptionalProtocol,
    DisallowProtocol
  }
}