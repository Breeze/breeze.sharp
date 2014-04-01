using System;
using System.Collections.Generic;
using System.Linq;

namespace Breeze.Sharp {

  /// <summary>
  /// 
  /// </summary>
  public class NamingConvention {

    public static List<NamingConvention> __namingConventions = new List<NamingConvention>();

    public NamingConvention() {
      Name = this.GetType().Name;
    }

    public NamingConvention(String name) {
      Name = name;
    }

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

    public virtual TypeNameInfo ServerTypeNameToClient(TypeNameInfo serverNameInfo) {
      
      String clientNs;
      if (_serverTypeNamespaceMap.TryGetValue(serverNameInfo.Namespace, out clientNs)) {
        return new TypeNameInfo(serverNameInfo.ShortName, clientNs);
      } else {
        return serverNameInfo;
      }
    }

    public virtual String ServerPropertyNameToClient(String clientName, StructuralType parentType) {
      return clientName;
    }

    public virtual TypeNameInfo ClientTypeNameToServer(TypeNameInfo clientNameInfo) {
      
      String serverNs;
      if (_clientTypeNamespaceMap.TryGetValue(clientNameInfo.Namespace, out serverNs)) {
        return new TypeNameInfo(clientNameInfo.ShortName, serverNs);
      } else {
        return clientNameInfo;
      }
    }

    public virtual String ClientPropertyNameToServer(String serverName, StructuralType parentType) {
      return serverName;
    }

    public void AddClientServerNamespaceMapping(String clientNamespace, String serverNamespace) {
      _clientTypeNamespaceMap[clientNamespace] = serverNamespace;
      _serverTypeNamespaceMap[serverNamespace] = clientNamespace;
    }

    private String _name;
    private Dictionary<String, String> _serverTypeNamespaceMap = new Dictionary<string, string>();
    private Dictionary<String, String> _clientTypeNamespaceMap = new Dictionary<string, string>();


    public static NamingConvention Default = new NamingConvention("Default");
    public static NamingConvention CamelCase = new CamelCaseNamingConvention();

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

  public class CamelCaseNamingConvention : NamingConvention  {
    public CamelCaseNamingConvention()
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
