using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Breeze.Sharp {
  public class NamingConvention {

    public static List<NamingConvention> __namingConventions = new List<NamingConvention>();

    public NamingConvention(String name) {
      Name = name;
      lock (__namingConventions) {
        __namingConventions.Add(this);
      }
    }

    public String Name { get; protected set; }

    public virtual TypeNameInfo ServerTypeNameToClient(TypeNameInfo serverNameInfo) {
      
      String clientNs;
      if (_serverTypeNamespaceMap.TryGetValue(serverNameInfo.Namespace, out clientNs)) {
        return new TypeNameInfo(serverNameInfo.ShortName, clientNs);
      } else {
        return serverNameInfo;
      }
    }

    public virtual String ServerPropertyNameToClient(String clientName) {
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

    public virtual String ClientPropertyNameToServer(String serverName) {
      return serverName;
    }

    public void AddClientServerNamespaceMapping(String clientNamespace, String serverNamespace) {
      _clientTypeNamespaceMap[clientNamespace] = serverNamespace;
      _serverTypeNamespaceMap[serverNamespace] = clientNamespace;
    }

    public Dictionary<String, String> _serverTypeNamespaceMap = new Dictionary<string, string>();
    public Dictionary<String, String> _clientTypeNamespaceMap = new Dictionary<string, string>();


    public static NamingConvention Default = new NamingConvention("Default");
    public static NamingConvention CamelCase = new CamelCaseNamingConvention();

    public static NamingConvention FromName(String name) {
      lock (__namingConventions) {
        return __namingConventions.FirstOrDefault(nc => nc.Name == name);
      }
    }

    public String TestPropertyName(String testVal, bool toServer) {
      Func<String, String> fn1;
      Func<String, String> fn2;
      if (toServer) {
        fn1 = ClientPropertyNameToServer;
        fn2 = ServerPropertyNameToClient;
      } else {
        fn1 = ServerPropertyNameToClient;
        fn2 = ClientPropertyNameToServer;
      }
      var t1 = fn1(testVal);
      var t2 = fn2(t1);
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
    
    public override String ServerPropertyNameToClient(String serverName) {
      return serverName.Substring(0,1).ToLower() + serverName.Substring(1);
    }
    public override String ClientPropertyNameToServer(String clientName) {
      return clientName.Substring(0, 1).ToUpper() + clientName.Substring(1);
    }
  }
}
