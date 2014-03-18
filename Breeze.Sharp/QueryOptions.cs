
namespace Breeze.Sharp {
  public class QueryOptions : IJsonSerializable {

    public QueryOptions() {

    }
    
    public QueryOptions(JNode jNode) {
      FetchStrategy = jNode.GetNullableEnum<FetchStrategy>("fetchStrategy");
      MergeStrategy = jNode.GetNullableEnum<MergeStrategy>("mergeStrategy");
      
    }

    JNode IJsonSerializable.ToJNode(object config) {
      var jn = new JNode();
      jn.AddEnum("fetchStrategy", this.FetchStrategy );
      jn.AddEnum("mergeStrategy", this.MergeStrategy );
      return jn;
    }

    public QueryOptions(FetchStrategy fetchStrategy, MergeStrategy mergeStrategy) {
      FetchStrategy = fetchStrategy;
      MergeStrategy = mergeStrategy;
    }

    public static QueryOptions Default = new QueryOptions(Breeze.Sharp.FetchStrategy.FromServer, Breeze.Sharp.MergeStrategy.PreserveChanges);

    public FetchStrategy? FetchStrategy { get; internal set; }
    public MergeStrategy? MergeStrategy { get; internal set; }

    
  }

 
}
