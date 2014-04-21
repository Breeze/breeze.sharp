using System;
using System.Linq;


namespace Breeze.Sharp {

  /// <summary>
  /// Class used to describe, parse and convert between EntityType names and CLR type names.
  /// </summary>
  public class TypeNameInfo {


    public TypeNameInfo(String shortName, String ns, bool isAnonymous = false) {
      ShortName = shortName;
      Namespace = ns;
      Name = QualifyTypeName(shortName, ns);
      IsAnonymous = isAnonymous;
    }

    public static TypeNameInfo FromEntityTypeName(String name) {
      String shortName, ns;
      var ix = name.IndexOf(":#");
      if (ix == -1) {
        shortName = name;
        ns = String.Empty;
      } else {
        shortName = name.Substring(0, ix);
        ns = name.Substring(ix + 2);
      }
      return new TypeNameInfo(shortName, ns);
    }


    public static TypeNameInfo FromClrTypeName(String clrTypeName) {
      if (clrTypeName.StartsWith(MetadataStore.ANONTYPE_PREFIX)) {
        return new TypeNameInfo(clrTypeName, String.Empty, true );
      }

      var entityTypeNameNoAssembly = clrTypeName.Split(',')[0];
      var nameParts = entityTypeNameNoAssembly.Split('.');
      String ns;
      var shortName = nameParts[nameParts.Length - 1];
      if (nameParts.Length > 1) {
        ns = String.Join(".", nameParts.Take(nameParts.Length - 1));
      } else {
        ns = "";
      }
      return new TypeNameInfo(shortName, ns);
    }

    public String Name { get;  private set; }
    public String ShortName { get; set; }
    public String Namespace { get; set; }
    public bool IsAnonymous { get; private set; }

    public TypeNameInfo ToClient() {
      return MetadataStore.Instance.NamingConvention.ServerTypeNameToClient(this);
    }

    public TypeNameInfo ToServer() {
      return MetadataStore.Instance.NamingConvention.ClientTypeNameToServer(this);
    }

    public static String QualifyTypeName(String shortName, String ns) {
      if (String.IsNullOrEmpty(ns)) {
        return shortName;
      } else {
        return shortName + ":#" + ns;
      }
    }

    public static bool IsQualifiedTypeName(String typeName) {
      return typeName.IndexOf(":#", StringComparison.Ordinal) >= 0;
    }

  }


}
