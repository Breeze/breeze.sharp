using Breeze.Sharp;
using Breeze.Sharp.Core;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Breeze.Sharp.Tests {
  public static class TestFns {

    /// <summary> URL to the Breeze server </summary>
    public static readonly string serviceRoot;
    /// <summary> URL for NorthwindIBModelController </summary>
    public static readonly string serviceName;
    /// <summary> Query encoding style used for tests.  Changing this will change the URLs above. </summary>
    public static readonly QueryUriStyle queryUriStyle = QueryUriStyle.OData;

    static TestFns() {
      Configuration.Instance.QueryUriStyle = queryUriStyle;

      if (queryUriStyle == QueryUriStyle.JSON) {
        //.NET Core server - use with QueryUriStyle.JSON
        serviceRoot = "http://localhost:34377/";
      } else {
        // .NET Framework server - use with QueryUriStyle.OData
        serviceRoot = "http://localhost:7149/breezeTests/";
      }
      serviceName = serviceRoot + "breeze/NorthwindIBModel/";
  }

  public static void RunInWpfSyncContext(Func<Task> function) {
#if NETFRAMEWORK
      if (function == null) throw new ArgumentNullException("function");
      var prevCtx = SynchronizationContext.Current;
      try {
        var syncCtx = new DispatcherSynchronizationContext();
        SynchronizationContext.SetSynchronizationContext(syncCtx);

        var task = function();
        if (task == null) throw new InvalidOperationException();

        var frame = new DispatcherFrame();
        var t2 = task.ContinueWith(x => { frame.Continue = false; }, TaskScheduler.Default);
        Dispatcher.PushFrame(frame);   // execute all tasks until frame.Continue == false

        task.GetAwaiter().GetResult(); // rethrow exception when task has failed 
      } finally {
        SynchronizationContext.SetSynchronizationContext(prevCtx);
      }
#else
      var task = function();
      if (task == null) throw new InvalidOperationException();
#endif
    }

    public static MetadataStore DefaultMetadataStore = new MetadataStore();

    public static async Task<EntityManager> NewEm(string serviceName, MetadataStore metadataStore = null) {
      metadataStore = metadataStore ?? DefaultMetadataStore;
#if NHIBERNATE
      metadataStore.AllowedMetadataMismatchTypes = MetadataMismatchType.MissingCLREntityType;
      var nc = metadataStore.NamingConvention.WithClientServerNamespaceMapping("Foo", "Models.NorthwindIB.NH");
      metadataStore.NamingConvention = nc;
#else
      metadataStore.MetadataMismatch += (s, e) => {
        if (e.StructuralTypeInfo.ShortName == "Geospatial") {
          e.Allow = true;
        }
      };

#endif
      var em = new EntityManager(serviceName, metadataStore);
      if (metadataStore.GetDataService(serviceName) == null) {
        await em.FetchMetadata();
      } 

      return em;
    }

    public static async Task<EntityManager> NewEm(DataService dataService, MetadataStore metadataStore = null) {
      metadataStore = metadataStore ?? DefaultMetadataStore;
      if (dataService.HasServerMetadata && metadataStore.GetDataService(dataService.ServiceName) == null) {
        var em = new EntityManager(dataService.ServiceName, metadataStore);
        await em.FetchMetadata();
        return em;
      } else {
        return new EntityManager(dataService, metadataStore);
      }
    }

    // within this using block make the DetachedMetadataStore == ms
    public static UsingBlock ShareWithDetached(MetadataStore ms) {
      return UsingBlock.Create(MetadataStore.Detached, ms, (x) => MetadataStore.Detached = x);
    }

    public static bool DEBUG_MONGO = false;
    public static bool DEBUG_ODATA = false;
    public static string EmployeeKeyName = "EmployeeID";
    public static string CustomerKeyName = "CustomerID";

    public static class WellKnownData {
      public static Guid AlfredsID = new Guid("785efa04-cbf2-4dd7-a7de-083ee17b6ad2");
      public static int NancyEmployeeID = 1;
      public static int ChaiProductID = 1;
      public static int DummyOrderID = 999;
      public static int DummyEmployeeID = 9999;
      public static Object[] AlfredsOrderDetailKey = new Object[] { 10643, 28 };
    }

    public static String MorphString(String val) {
      var suffix = "__";
      if (String.IsNullOrEmpty(val)) {
        return suffix;
      } else {
        if (val.EndsWith(suffix)) {
          val = val.Substring(0, val.Length - 2);
        } else {
          val = val + suffix;
        }
      }
      return val;
    }

    private static String Revese(String s) {
      var charArray = s.ToCharArray();
      Array.Reverse(charArray);
      return new string(charArray);
    }

    public static String RandomSuffix(int length) {
      // length should be less <= 18
      var suffix = DateTime.Now.Ticks.ToString().Reverse().Take(length).ToArray();
      return new string(suffix);
    }

    // in practice should always return a monotonically increasing integer - even across separate runs.
    public static Int32 GetNextInt() {
      return __seedInt++;
    }

    private static Int32 __seedInt = (DateTime.Now - new DateTime(2014, 3, 1)).Milliseconds;
  }
}
