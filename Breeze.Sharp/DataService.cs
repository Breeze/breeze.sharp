using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;

namespace Breeze.Sharp {

  /// <summary>
  /// A DataService instance is used to encapsulate the details of a single 'service'; 
  /// this includes a serviceName, a dataService adapterInstance, and whether the service has server side metadata.
  /// </summary>
  /// <remarks>
  /// You can construct an EntityManager with either a serviceName or a DataService instance, 
  /// if you use a serviceName then a DataService is constructed for you. 
  /// The same applies to the MetadataStore.FetchMetadata method, i.e. it takes either a serviceName or a DataService instance.
  /// Each metadataStore contains a list of DataServices, each accessible via its ‘serviceName’. 
  /// ( see MetadataStore.GetDataService and MetadataStore.addDataService). 
  /// The ‘addDataService’ method is called internally anytime a MetadataStore.FetchMetadata call 
  /// occurs with a new dataService ( or service name).
  /// </remarks>
  public class DataService : IJsonSerializable {

    /// <summary>
    /// 
    /// </summary>
    /// <param name="serviceName"></param>
    public DataService(String serviceName) {
      ServiceName = serviceName;
      HasServerMetadata = true;
      UseJsonP = false;
      Adapter = new WebApiDataServiceAdapter();
      JsonResultsAdapter = Adapter.JsonResultsAdapter;
      InitializeHttpClient();
    }

    /// <summary>
    /// For internal use only.
    /// </summary>
    /// <param name="jNode"></param>
    public DataService(JNode jNode) {
      ServiceName = jNode.Get<String>("serviceName");
      HasServerMetadata = jNode.Get<bool>("hasServerMetadata");
      
      UseJsonP = jNode.Get<bool>("useJsonp");
      Adapter = GetAdapter(jNode.Get<String>("adapterName"));
      // TODO: need to do the same as above with JsonResultsAdapter.
      JsonResultsAdapter = Adapter.JsonResultsAdapter;
      InitializeHttpClient();
    }

    private void InitializeHttpClient() {
      _client = new HttpClient();
      _client.BaseAddress = new Uri(ServiceName);

      // Add an Accept header for JSON format.
      _client.DefaultRequestHeaders.Accept.Add(
          new MediaTypeWithQualityHeaderValue("application/json"));
    }

    private IDataServiceAdapter GetAdapter(string adapterName) {
      // TODO: fix this later using some form of DI
      return new WebApiDataServiceAdapter();
    }

    public String ServiceName {get; private set; }

    public bool UseJsonP { get; set; }

    public bool HasServerMetadata { get; set; }

    public IDataServiceAdapter Adapter { get; set; }

    public IJsonResultsAdapter JsonResultsAdapter { get; set; }

    // Only available for server retrieved metadata
    public String ServerMetadata { get; internal set; }

    public async Task<String> GetAsync(String resourcePath) {
      try {

        var response = await _client.GetAsync(resourcePath);

        var result = await response.Content.ReadAsStringAsync();
        if (!response.IsSuccessStatusCode) {
          throw new HttpRequestException(result);
        }
        return result;
      } catch (HttpRequestException ex) {
        Debug.WriteLine(ex.Message);
        throw;
      } catch (Exception e) {
        Debug.WriteLine(e.Message);
        throw;
      } finally {

      }
    }

    public async Task<String> PostAsync(String resourcePath, String json) {

      var content = new StringContent(json, Encoding.UTF8, "application/json");
      // example of how to use FormUrl instead.
      //var content = new FormUrlEncodedContent(new[] 
      //    {
      //        new KeyValuePair<string, string>("", "login")
      //    });

      var response = await _client.PostAsync(resourcePath, content);

      var result = await response.Content.ReadAsStringAsync();
        
      if (!response.IsSuccessStatusCode) {
        throw new HttpRequestException(result);
      }
      return result;

    }

    JNode IJsonSerializable.ToJNode(Object config) {
      var jo = new JNode();
      jo.AddPrimitive("serviceName", this.ServiceName);
      jo.AddPrimitive("adapterName", this.Adapter == null ? null : this.Adapter.Name);
      jo.AddPrimitive("hasServerMetadata", this.HasServerMetadata);
      jo.AddPrimitive("jsonResultsAdapter", this.JsonResultsAdapter == null ? null : this.JsonResultsAdapter.Name);
      jo.AddPrimitive("useJsonp", this.UseJsonP);
      return jo;
    }

 
    private HttpClient _client;


  }
}
