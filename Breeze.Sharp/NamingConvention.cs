using System.Collections.ObjectModel;

using Breeze.Sharp.Core;
using System;
using System.Collections.Generic;
using System.Linq;

using Newtonsoft.Json;

namespace Breeze.Sharp {

  /// <summary>
  /// A NamingConvention instance is used to specify the naming conventions under 
  /// which the MetadataStore will translate type and property names between the server and the .NET client.
  /// The default NamingConvention does not perform any translation, it simply passes property names thru unchanged.
  /// </summary>
  public class NamingConvention : Internable {

    public static String Suffix = "NamingConvention";

    public static List<NamingConvention> __namingConventions = new List<NamingConvention>();

    public NamingConvention() {
      Name = UtilFns.TypeToSerializationName(this.GetType(), Suffix);
      _clientServerNamespaceMap = new Dictionary<string, string>();
    }

    /// <summary>
    /// Translates a server <see cref="TypeNameInfo"/> into a client TypeNameInfo.
    /// </summary>
    /// <param name="serverNameInfo"></param>
    /// <returns></returns>
    public virtual TypeNameInfo ServerTypeNameToClient(TypeNameInfo serverNameInfo) {
      
      String clientNs;
      if (_serverClientNamespaceMap == null) {
        _serverClientNamespaceMap = _clientServerNamespaceMap.ToDictionary(kvp => kvp.Value, kvp => kvp.Key);
      }
      if (_serverClientNamespaceMap.TryGetValue(serverNameInfo.Namespace, out clientNs)) {
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
      if (_clientServerNamespaceMap.TryGetValue(clientNameInfo.Namespace, out serverNs)) {
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

    public NamingConvention WithClientServerNamespaceMapping(IDictionary<String, String> clientServerNamespaceMap) {
      var clone = Clone();
      clone._clientServerNamespaceMap = new Dictionary<string, string>(clientServerNamespaceMap);
      _serverClientNamespaceMap = null;
      return clone;
    }

    public NamingConvention WithClientServerNamespaceMapping(String clientNamespace, String serverNamespace) {
      var clone = Clone();
      clone.AddClientServerNamespaceMapping(clientNamespace, serverNamespace);
      return clone;
    }

    protected void AddClientServerNamespaceMapping(String clientNamespace, String serverNamespace) {
      _clientServerNamespaceMap[clientNamespace] = serverNamespace;
      _serverClientNamespaceMap = null;
    }

    protected virtual NamingConvention Clone() {
      return (NamingConvention) this.ToJNode().ToObject(this.GetType(), true);
    }

    [JsonIgnore]
    public ReadOnlyDictionary<String, String> ClientServerNamespaceMap {
      get { return new ReadOnlyDictionary<String, String>(_clientServerNamespaceMap); }
    }

    [JsonProperty("ClientServerNamespaceMap")]
    private Dictionary<String, String> _clientServerNamespaceMap = new Dictionary<string, string>();
    private Dictionary<String, String> _serverClientNamespaceMap;


    /// <summary>
    /// The 'Default' NamingConvention. - Basically does nothing to either type or property names.
    /// </summary>
    public static NamingConvention Default = new NamingConvention();
    /// <summary>
    /// A NamingConvention that causes properties to be camelCased on the client.
    /// </summary>
    public static NamingConvention CamelCaseProperties = new CamelCasePropertiesNamingConvention();

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
    public CamelCasePropertiesNamingConvention() {
      
    }
    
    public override String ServerPropertyNameToClient(String serverName, StructuralType parentType) {
      return serverName.Substring(0,1).ToLower() + serverName.Substring(1);
    }
    public override String ClientPropertyNameToServer(String clientName, StructuralType parentType) {
      return clientName.Substring(0, 1).ToUpper() + clientName.Substring(1);
    }
  }
}
