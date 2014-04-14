using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Newtonsoft.Json.Linq;

namespace Breeze.Sharp {

  /// <summary>
  /// This types describes the result of each <see cref="IJsonResultsAdapter.VisitNode"/> operation.
  /// </summary>
  public class JsonNodeInfo {
    public TypeNameInfo ServerTypeNameInfo { get; set; }
    public String NodeId { get; set; }
    public String NodeRefId { get; set; }
    public bool Ignore { get; set; }
    public JObject Node { get; set; }
  } 

  /// <summary>
  ///  Instances of this interface are used to provide custom extraction and parsing logic
  ///  on the json results returned by any web service. This facility makes it possible for breeze
  ///  to talk to virtually any web service and return objects that will be first class 'breeze' citizens.
  /// </summary>
  public interface IJsonResultsAdapter {
    String Name { get; }
    JToken ExtractResults(JToken node);
    JsonNodeInfo VisitNode(JObject node, MappingContext mappingContext, NodeContext nodeContext);
  }
}
