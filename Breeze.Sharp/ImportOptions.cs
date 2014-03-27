﻿
namespace Breeze.Sharp {

  /// <summary>
  /// 
  /// </summary>
  public class ImportOptions {

    public ImportOptions(MergeStrategy? mergeStrategy = null, bool shouldMergeMetadata=true) {
      MergeStrategy = mergeStrategy;
      ShouldMergeMetadata = shouldMergeMetadata;
    }

    public static ImportOptions Default = new ImportOptions();
    public MergeStrategy? MergeStrategy { get; private set; }
    public bool ShouldMergeMetadata { get; private set; }
  }
}