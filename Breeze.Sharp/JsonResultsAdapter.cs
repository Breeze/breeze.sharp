using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Newtonsoft.Json.Linq;

namespace Breeze.Sharp {

  

  public class NodeContext {
    
  }

  public class JsonNodeInfo {
    public String EntityTypeName { get; set; }
    public String NodeId { get; set; }
    public String NodeRefId { get; set; }
    public bool Ignore { get; set; }
    public JToken Node { get; set; }
  }

  public interface IJsonResultsAdapter {
    String Name { get; }
    JToken ExtractResults(JToken node);
    JsonNodeInfo VisitNode(JToken node, MappingContext mappingContext, NodeContext nodeContext);
  }
}
