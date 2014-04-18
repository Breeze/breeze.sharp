using System;

namespace Breeze.Sharp {

  /// <summary>
  /// A SaveOptions instance is used to specify the 'options' under which a save will occur.
  /// </summary>
  public class SaveOptions : IJsonSerializable {

    public SaveOptions(string resourceName=null, DataService dataService=null,  String tag=null) {
      ResourceName = resourceName;
      DataService = dataService;
      Tag = tag;
    }

    public SaveOptions(SaveOptions saveOptions) {
      ResourceName = saveOptions.ResourceName;
      DataService = saveOptions.DataService;
      Tag = saveOptions.Tag;
    }
    
    //public SaveOptions(JNode jNode) {
      
      
    //}

    JNode IJsonSerializable.ToJNode(object config) {
      var jn = new JNode();
      
      jn.AddPrimitive("tag", Tag);
      return jn;
    }


    public static SaveOptions Default = new SaveOptions(null, null, null);

    public String ResourceName { get; internal set; }
    public DataService DataService { get; internal set; }
    public String Tag { get; set;  }
    
  }

 
}
