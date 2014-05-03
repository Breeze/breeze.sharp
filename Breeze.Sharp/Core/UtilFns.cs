using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace Breeze.Sharp.Core {
  public class UtilFns {

    public static bool DictionariesEqual<K,V>(Dictionary<K,V> d1, Dictionary<K, V> d2)  {
      return d1.Keys.Count == d2.Keys.Count
        && d1.Keys.All(k => d2.ContainsKey(k) && object.Equals(d1[k], d2[k]));
    }

    public static T[] ToArray<T>(params T[] p) {
      return p;
    }

    public static string ToCamelCase(string s) {
      if (s.Length > 1) {
        return s.Substring(0, 1).ToLower() + s.Substring(1);
      } else if (s.Length == 1) {
        return s.Substring(0, 1).ToLower();
      } else {
        return s;
      }
    }

    public static string TypeToSerializationName(Type type, String suffix) {
      var typeName = type.Name;
      if (typeName == suffix) return "none";
      var name = (typeName.EndsWith(suffix)) ? typeName.Substring(0, typeName.Length - suffix.Length) : typeName;
      name = UtilFns.ToCamelCase(name);
      return name;
    }
  }
}
