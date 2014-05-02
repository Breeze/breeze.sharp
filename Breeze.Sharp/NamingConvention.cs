using System;
using System.Collections.Generic;
using System.Linq;

namespace Breeze.Sharp {

  /// <summary>
  /// A NamingConvention instance is used to specify the naming conventions under 
  /// which the MetadataStore will translate type and property names between the server and the .NET client.
  /// The default NamingConvention does not perform any translation, it simply passes property names thru unchanged.
  /// </summary>
  public class NamingConvention {

    public static List<NamingConvention> __namingConventions = new List<NamingConvention>();

    public NamingConvention() {
      Name = this.GetType().Name;
    }

    public NamingConvention(String name) {
      Name = name;
    }

    /// <summary>
    /// The name of this NamingConvention. This name will be used when serializing and deserializing metadata
    /// to insure that the correct NamingConvention is set for any exported metadata.
    /// </summary>
    public String Name { 
      get { return _name; }
      set {
        if (_name == value) return;
        if (_name != null) {
          throw new Exception("A NamingConvention's 'Name' cannot be changed once set");
        }
        _name = value;
        lock (__namingConventions) {
          __namingConventions.Add(this);
        }
      }
    }

    /// <summary>
    /// Translates a server <see cref="TypeNameInfo"/> into a client TypeNameInfo.
    /// </summary>
    /// <param name="serverNameInfo"></param>
    /// <returns></returns>
    public virtual TypeNameInfo ServerTypeNameToClient(TypeNameInfo serverNameInfo) {
      
      String clientNs;
      if (_serverTypeNamespaceMap.TryGetValue(serverNameInfo.Namespace, out clientNs)) {
        return new TypeNameInfo(serverNameInfo.ShortName, clientNs);
      } else {
        return serverNameInfo;
      }
    }

    /// <summary>
    /// Translates a server property name into a client property name. 
    /// </summary>
    /// <param name="serverPropertyName"></param>
    /// <param name="parentType"></param>
    /// <returns></returns>
    public virtual String ServerPropertyNameToClient(String serverPropertyName, StructuralType parentType) {
      return serverPropertyName;
    }

    /// <summary>
    ///  Translates a client <see cref="TypeNameInfo"/> into a server TypeNameInfo.
    /// </summary>
    /// <param name="clientNameInfo"></param>
    /// <returns></returns>
    public virtual TypeNameInfo ClientTypeNameToServer(TypeNameInfo clientNameInfo) {
      
      String serverNs;
      if (_clientTypeNamespaceMap.TryGetValue(clientNameInfo.Namespace, out serverNs)) {
        return new TypeNameInfo(clientNameInfo.ShortName, serverNs);
      } else {
        return clientNameInfo;
      }
    }

    /// <summary>
    /// Translates a server property name into a client property name. 
    /// </summary>
    /// <param name="clientPropertyName"></param>
    /// <param name="parentType"></param>
    /// <returns></returns>
    public virtual String ClientPropertyNameToServer(String clientPropertyName, StructuralType parentType) {
      return clientPropertyName;
    }

    public void AddClientServerNamespaceMapping(String clientNamespace, String serverNamespace) {
      _clientTypeNamespaceMap[clientNamespace] = serverNamespace;
      _serverTypeNamespaceMap[serverNamespace] = clientNamespace;
    }

    private String _name;
    private readonly Dictionary<String, String> _serverTypeNamespaceMap = new Dictionary<string, string>();
    private readonly Dictionary<String, String> _clientTypeNamespaceMap = new Dictionary<string, string>();

    /// <summary>
    /// The 'Default' NamingConvention. - Basically does nothing to either type or property names.
    /// </summary>
    public static NamingConvention Default = new NamingConvention("Default");
    /// <summary>
    /// A NamingConvention that causes properties to be camelCased on the client.
    /// </summary>
    public static NamingConvention CamelCaseProperties = new CamelCasePropertiesNamingConvention();

    /// <summary>
    /// Returns any NamingConvention based on its 'Name'.
    /// </summary>
    /// <param name="name"></param>
    /// <returns></returns>
    public static NamingConvention FromName(String name) {
      lock (__namingConventions) {
        return __namingConventions.FirstOrDefault(nc => nc.Name == name);
      }
    }

    public String TestPropertyName(String testVal, StructuralType parentType, bool toServer) {
      Func<String, StructuralType, String> fn1;
      Func<String, StructuralType, String> fn2;
      if (toServer) {
        fn1 = ClientPropertyNameToServer;
        fn2 = ServerPropertyNameToClient;
      } else {
        fn1 = ServerPropertyNameToClient;
        fn2 = ClientPropertyNameToServer;
      }
      var t1 = fn1(testVal, parentType);
      var t2 = fn2(t1, parentType);
      if (t2 != testVal) {
        throw new Exception("NamingConvention: " + this.Name + " does not roundtrip the following value correctly: " + testVal);
      }
      return t1;
    }
  }

  /// <summary>
  /// The "camelCase" naming convention - This implementation only lowercases the first character
  ///  of the server property name but leaves the rest of the property name intact. 
  /// If a more complicated version is needed then another type should be created that 
  /// inherits from NamingConvention.
  /// </summary>
  public class CamelCasePropertiesNamingConvention : NamingConvention  {
    public CamelCasePropertiesNamingConvention()
      : base("CamelCase") {
    }
    
    public override String ServerPropertyNameToClient(String serverName, StructuralType parentType) {
      return serverName.Substring(0,1).ToLower() + serverName.Substring(1);
    }
    public override String ClientPropertyNameToServer(String clientName, StructuralType parentType) {
      return clientName.Substring(0, 1).ToUpper() + clientName.Substring(1);
    }
  }
}
