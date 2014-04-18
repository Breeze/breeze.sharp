using Breeze.Sharp.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Breeze.Sharp {

  public class MetadataMismatchEventArgs {
    public String EntityTypeName { get; internal set; }
    public String PropertyName { get; internal set; }
    public MetadataMismatchType MetadataMismatchType { get; set; }
    public String Detail { get; internal set; }
    public bool Allow { get; set; }
    public String Message {
      get {
        var mismatchDescr = UtilFns.SplitCamelCase(MetadataMismatchType.ToString()).Replace("C L R", "CLR");
        if (String.IsNullOrEmpty(PropertyName)) {
          return String.Format("Metadata mismatch classification: '{0}' - for EntityType: '{1}'.  {2}",
            mismatchDescr, EntityTypeName, Detail);
        } else {
          return String.Format("Metadata mismatch classification: '{0}' - for EntityType: '{1}' Property: '{2}'.  {3}",
            mismatchDescr, EntityTypeName, PropertyName, Detail);
        }
      }
    }
  }

  [Flags]
  public enum MetadataMismatchType {
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
    /// <summary>
    /// 
    /// </summary>
    AllAllowable = MissingCLREntityType | MissingCLRComplexType | MissingCLRDataProperty | MissingCLRNavigationProperty,
    NotAllowable = InconsistentCLRTypeDefinition | InconsistentCLRTypeDefinition,
  }
}
