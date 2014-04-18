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

    public static string SplitCamelCase(string input) {
      // From: http://weblogs.asp.net/jgalloway/archive/2005/09/27/426087.aspx
      return Regex.Replace(input, "([A-Z])", " $1").Trim();
      // Handle sequential uppercase chars as a single word.
      // return Regex.Replace(input, "([A-Z][A-Z]*)", " $1").Trim();

    }
  }
}
