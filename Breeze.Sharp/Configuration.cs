using Breeze.Sharp.Core;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Breeze.Sharp {

  /// <summary>
  /// A singleton class that provides basic registration mechanisms for a Breeze application.
  /// </summary>
  public class Configuration  {

    #region Ctor related 

    // Explicit static constructor to tell C# compiler
    // not to mark type as beforefieldinit
    static Configuration() { }

    private Configuration() {

      RegisterTypeDiscoveryActionCore(typeof(IEntity), (t) => RegisterStructuralType(t), true);
      RegisterTypeDiscoveryActionCore(typeof(IComplexObject), (t) => RegisterStructuralType(t), true);
      RegisterTypeDiscoveryActionCore(typeof(Validator), (t) => RegisterValidator(t), true);
      RegisterTypeDiscoveryActionCore(typeof(NamingConvention), (t) => RegisterNamingConvention(t), true);
    }
  
    /// <summary>
    /// The Singleton instance.
    /// </summary>
    public static Configuration Instance {
      get {
        return __instance;
      }
    }

    #endregion

    #region Public methods

    /// <summary>
    /// For testing purposes only. - Replaces the current instance with a new one, effectively clearing any 
    /// previously cached or registered data.
    /// </summary>
    public static void __Reset() {
      lock (__lock) {
        var x = __instance._probedAssemblies;
        __instance = new Configuration();
      }
    }

    /// <summary>
    /// Tell's Breeze to probe the specified assemblies and automatically discover any
    /// Entity types, Complex types, Validators, NamingConventions and any other types 
    /// for which a type discovery action is registered.
    /// </summary>
    /// <param name="assembliesToProbe"></param>
    /// <returns></returns>
    public bool ProbeAssemblies(params Assembly[] assembliesToProbe) {
      lock (_typeDiscoveryActions) {
        var assemblies = assembliesToProbe.Except(_probedAssemblies).ToList();
        if (assemblies.Any()) {
          assemblies.ForEach(asm => {
            _probedAssemblies.Add(asm);
            _typeDiscoveryActions.Where(tpl => tpl.Item3 == null || tpl.Item3(asm))
              .ForEach(tpl => {
                var type = tpl.Item1;
                var action = tpl.Item2;
                TypeFns.GetTypesImplementing(type, asm).ForEach(action);
              });
          });
          return true;
        } else {
          return false;
        }
      }
    }

    /// <summary>
    /// Returns the CLR type for a specified structuralTypeName or null if not found.
    /// </summary>
    /// <param name="structuralTypeName"></param>
    /// <returns></returns>
    public Type GetClrType(String structuralTypeName) {
      lock (_clrTypeMap) {
        Type type;
        _clrTypeMap.TryGetValue(structuralTypeName, out type);
        return type;
      }
    }

    /// <summary>
    /// Returns whether the specified CLR type is either an IEntity or a IComplexObject.
    /// </summary>
    /// <param name="clrType"></param>
    /// <returns></returns>
    public static bool IsStructuralType(Type clrType) {
      return typeof(IStructuralObject).IsAssignableFrom(clrType);
    }

   
    /// <summary>
    /// Allows for custom actions to be performed as a result of any ProbeAssemblies call.
    /// </summary>
    /// <param name="type"></param>
    /// <param name="action"></param>
    public void RegisterTypeDiscoveryAction(Type type, Action<Type> action) {
      RegisterTypeDiscoveryActionCore(type, action, false);
    }


    #endregion

    

    // includeThisAssembly = whether to probe the assembly where 'type' is defined
    private void RegisterTypeDiscoveryActionCore(Type type, Action<Type> action, bool includeThisAssembly) {
      Func<Assembly, bool> shouldProcessAssembly = (a) => {
        return includeThisAssembly ? true : a != this.GetType().GetTypeInfo().Assembly;
      };
      lock (_typeDiscoveryActions) {
        _typeDiscoveryActions.Add(Tuple.Create(type, action, shouldProcessAssembly));
      }
    }

   

    #region Validator & NamingConvention methods

    internal Validator FindOrCreateValidator(JNode jNode) {
      // locking is handled internally
      return _validatorCache.FindOrCreate(jNode);
    }

    internal NamingConvention FindOrCreateNamingConvention(JNode jNode) {
      // locking is handled internally
      return _namingConventionCache.FindOrCreate(jNode);
    }

    internal Validator InternValidator(Validator validator) {
      // locking is handled internally
      return _validatorCache.Intern(validator);
    }

    internal NamingConvention InternNamingConvention(NamingConvention nc) {
      // locking is handled internally
      return _namingConventionCache.Intern(nc);
    }

    private void RegisterStructuralType(Type clrType) {
      lock (_clrTypeMap) {
        var stName = TypeNameInfo.FromClrType(clrType).StructuralTypeName;
        _clrTypeMap[stName] = clrType;
      }
    }

    
    private void RegisterValidator(Type validatorType) {
       // locking is handled internally
      _validatorCache.Register(validatorType, Validator.Suffix);
      
    }

    private void RegisterNamingConvention(Type namingConventionType) {
      // locking is handled internally
      _namingConventionCache.Register(namingConventionType, NamingConvention.Suffix);
    }   

    #endregion
    
    #region Inner classes 

    private class InternCache<T> where T : Internable {
      public readonly Dictionary<String, Type> TypeMap = new Dictionary<string, Type>();
      public readonly Dictionary<JNode, T> JNodeMap = new Dictionary<JNode, T>();

      internal T FindOrCreate(JNode jNode) {
        try {
          lock (TypeMap) {
            T internable;

            if (JNodeMap.TryGetValue(jNode, out internable)) {
              return internable;
            }

            internable = InternableFromJNode(jNode);
            JNodeMap[jNode] = internable;
            return internable;
          }
        } catch (Exception e) {
          throw new Exception("Unable to deserialize type: " + typeof(T).Name + " item: " + jNode);
        }
      }

      public T Intern(T internable) {
        if (internable.IsInterned) return internable;
        var jNode = internable.ToJNode();

        lock (TypeMap) {
          if (!TypeMap.ContainsKey(internable.Name)) {
            TypeMap[internable.Name] = internable.GetType();
          }
          T cachedInternable;
          if (JNodeMap.TryGetValue(jNode, out cachedInternable)) {
            cachedInternable.IsInterned = true;
            return (T)cachedInternable;
          } else {
            JNodeMap[jNode] = internable;
            internable.IsInterned = true;
            return internable;
          }
        }
      }

      public void Register(Type internableType, String defaultSuffix) {
        var ti = internableType.GetTypeInfo();
        if (ti.IsAbstract) return;
        if (ti.GenericTypeParameters.Length != 0) return;
        var key = UtilFns.TypeToSerializationName(internableType, defaultSuffix);
        lock (TypeMap) {
          TypeMap[key] = internableType;
        }
      }

      private T InternableFromJNode(JNode jNode) {
        var name = jNode.Get<String>("name");
        Type type;
        if (!TypeMap.TryGetValue(name, out type)) {
          return null;
        }
        // Deserialize the object
        var vr = (T)jNode.ToObject(type, true);
        return vr;
      }


    }
    
    #endregion

    #region Private and Internal vars

    internal static String ANONTYPE_PREFIX = "_IB_";

    private static Configuration __instance = new Configuration();
    private static readonly Object __lock = new Object();
    
    private readonly AsyncSemaphore _asyncSemaphore = new AsyncSemaphore(1);
    private readonly Object _lock = new Object();

    private Dictionary<String, Type> _clrTypeMap = new Dictionary<string, Type>();

    private readonly HashSet<Assembly> _probedAssemblies = new HashSet<Assembly>();
    private readonly List<Tuple<Type, Action<Type>, Func<Assembly, bool>>> _typeDiscoveryActions = new List<Tuple<Type, Action<Type>, Func<Assembly, bool>>>();
    
    private readonly Dictionary<String, String> _shortNameMap = new Dictionary<string, string>();

    private InternCache<Validator> _validatorCache = new InternCache<Validator>();
    private InternCache<NamingConvention> _namingConventionCache = new InternCache<NamingConvention>();

    #endregion

  
  }

}
