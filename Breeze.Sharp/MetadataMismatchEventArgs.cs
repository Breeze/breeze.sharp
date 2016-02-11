using Breeze.Sharp.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace Breeze.Sharp {

  public class MetadataMismatchEventArgs {
    public String StructuralTypeName { get; internal set; }

    public TypeNameInfo StructuralTypeInfo {
      get { return TypeNameInfo.FromStructuralTypeName(StructuralTypeName); } 
    }

    public String PropertyName { get; internal set; }
    public MetadataMismatchTypes MetadataMismatchType { get; set; }
    public String Detail { get; internal set; }
    public bool Allow { get; set; }
    public String Message {
      get {
        var mismatchDescr = SplitCamelCase(MetadataMismatchType.ToString()).Replace("C L R", "CLR");
        if (String.IsNullOrEmpty(PropertyName)) {
          return String.Format("Metadata mismatch classification: '{0}' - for StructuralType: '{1}'.  {2}",
            mismatchDescr, StructuralTypeName, Detail);
        } else {
          return String.Format("Metadata mismatch classification: '{0}' - for StructuralType: '{1}' Property: '{2}'.  {3}",
            mismatchDescr, StructuralTypeName, PropertyName, Detail);
        }
      }
    }

    private static string SplitCamelCase(string input) {
      // From: http://weblogs.asp.net/jgalloway/archive/2005/09/27/426087.aspx
      return Regex.Replace(input, "([A-Z])", " $1").Trim();
      // Handle sequential uppercase chars as a single word.
      // return Regex.Replace(input, "([A-Z][A-Z]*)", " $1").Trim();
    }
  }

  [Flags]
  public enum MetadataMismatchTypes {
    /// <summary>
    /// Server has metadata for a type that is not found on the client.
    /// </summary>
    MissingCLREntityType = 1,
    /// <summary>
    /// Server has metadata for a type that is not found on the client.
    /// </summary>
    MissingCLRComplexType = 2,
    /// <summary>
    /// Server has metadata for a DataProperty that does not exist on the client.
    /// </summary>
    MissingCLRDataProperty = 4,
    /// <summary>
    /// Server has metadata for a NavigationProperty that does not exist on the client.
    /// </summary>
    MissingCLRNavigationProperty = 8,
    /// <summary>
    /// Some fundamental part of this property does not match between client and server. 
    /// Mismatches of this type will always throw an exception.
    /// </summary>
    InconsistentCLRPropertyDefinition = 16,
    /// <summary>
    /// Some fundamental part of this CLRType does not match between client and server. 
    /// Mismatches of this type will always throw an exception.
    /// </summary>
    InconsistentCLRTypeDefinition = 32,
    MissingCLRNamingConvention = 64,
    /// <summary>
    /// 
    /// </summary>
    AllAllowable = MissingCLREntityType | MissingCLRComplexType | MissingCLRDataProperty | MissingCLRNavigationProperty,
    NotAllowable = InconsistentCLRTypeDefinition | InconsistentCLRTypeDefinition,
  }
}
