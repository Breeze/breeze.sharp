using Breeze.Sharp.Core;
using Newtonsoft.Json;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using System.Threading;
using Newtonsoft.Json.Linq;

namespace Breeze.Sharp {

  // JsonObject attribute is needed so this is NOT deserialized as an Enumerable

  /// <summary>
  /// The actual result of any query that has an InlineCount specified.  
  /// This class also implement IEnumerable{T} for the actual returned entities.
  /// If you want the InlineCount for any query you will need to cast the return type
  /// from the query into a QueryResult{T}). 
  /// </summary>
  /// <typeparam name="T"></typeparam>
  [JsonObject]
  public class QueryResult<T> : IEnumerable<T>, IHasInlineCount {
    public IEnumerable<T> Results { get; set; }
    public Int64? InlineCount { get; set; }
    public IEnumerator<T> GetEnumerator() {
      return Results.GetEnumerator();
    }

    System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() {
      return Results.GetEnumerator();
    }

  }

  /// <summary>
  /// Interface that indicates whether an object returned by a <see cref="EntityQuery{T}"/> has an InlineCount property. 
  /// </summary>
  public interface IHasInlineCount {
    /// <summary>
    /// The actual inline count.
    /// </summary>
    Int64? InlineCount { get; }
  }




 

}



