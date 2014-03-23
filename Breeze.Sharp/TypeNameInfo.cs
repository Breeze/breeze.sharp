using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

using System.Reflection;
using Breeze.Sharp.Core;
using System.Diagnostics;

namespace Breeze.Sharp {

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

    public String Name { get; private set; }
    public String ShortName { get; private set; }
    public String Namespace { get; private set; }
    public bool IsAnonymous { get; private set; }

    public TypeNameInfo ToClient() {
      return MetadataStore.Instance.NamingConvention.ServerTypeNameToClient(this);
    }

    public TypeNameInfo ToServer() {
      return MetadataStore.Instance.NamingConvention.ClientTypeNameToServer(this);
    }

    public static String QualifyTypeName(String shortName, String ns) {
      return shortName + ":#" + ns;
    }

    public static bool IsQualifiedTypeName(String typeName) {
      return typeName.IndexOf(":#") >= 0;
    }

  }


}
