using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading;
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
    /// Constructs a new DataService with the option to use an already configured HttpClient. If one is not provided
    /// then the DataService will create one internally.  In either case it will be available via the HttpClient property.
    /// </summary>
    /// <remarks>Note that if an HttpClient is passed in that it MUST be a different instance than that provided
    /// to any other DataService.  Whether passed in or created by the DataService, the HttpClient will automatically have 
    /// its BaseAddress set and will be configured to support for a 'application/json' media type request header.
    /// </remarks>
    /// <param name="serviceName"></param>
    /// <param name="httpClient"></param>
    public DataService(String serviceName, HttpClient httpClient = null) {
      if (String.IsNullOrEmpty(serviceName)) {
        throw new ArgumentNullException("serviceName");
      }
      ServiceName = serviceName;
      HasServerMetadata = true;
      UseJsonP = false;
      Adapter = new WebApiDataServiceAdapter();
      JsonResultsAdapter = Adapter.JsonResultsAdapter;
      InitializeHttpClient(httpClient);
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
      InitializeHttpClient(null);

    }

    /// <summary>
    /// Returns the HttpClient used by this data service.  This reference may be 
    /// used to customize  headers, buffer sizes and timeouts.  
    /// </summary>
    public HttpClient HttpClient {
      get { return _httpClient; }
    }

    /// <summary>
    /// The Default HttpMessageHandler to be used in the event that 
    /// Breeze creates the HttpClient automatically for this service.
    /// </summary>
    public static HttpMessageHandler DefaultHttpMessageHandler {
      get {
        lock (__lock) {
          return __defaultHttpMessageHandler;
        }
      }
      set {
        lock (__lock) {
          __defaultHttpMessageHandler = value;
        }
      }
    }

    private void InitializeHttpClient(HttpClient httpClient) {

      if (httpClient == null) {
        httpClient = DefaultHttpMessageHandler == null ? new HttpClient() : new HttpClient(DefaultHttpMessageHandler);
      }
      _httpClient = httpClient;
      _httpClient.BaseAddress = new Uri(ServiceName);
      
      // Add an Accept header for JSON format.
      _httpClient.DefaultRequestHeaders.Accept.Add(
         new MediaTypeWithQualityHeaderValue("application/json"));

    }

    private IDataServiceAdapter GetAdapter(string adapterName) {
      // TODO: fix this later using some form of DI
      return new WebApiDataServiceAdapter();
    }

    public String ServiceName {
      get { return _serviceName; }
      set {
        _serviceName = value.Trim();
        if (!_serviceName.EndsWith("/")) {
          _serviceName += "/";
        }
      }
    }

    public bool UseJsonP { get; set; }

    public bool HasServerMetadata { get; set; }

    public IDataServiceAdapter Adapter { get; set; }

    public IJsonResultsAdapter JsonResultsAdapter { get; set; }

    // Only available for server retrieved metadata
    public String ServerMetadata { get; internal set; }

    public async Task<String> GetAsync(String resourcePath) {
      return await GetAsync(resourcePath, CancellationToken.None);
    }

    public async Task<String> GetAsync(String resourcePath, CancellationToken cancellationToken) {
      try {
        var response = await _httpClient.GetAsync(resourcePath, cancellationToken);

        cancellationToken.ThrowIfCancellationRequested();

        return await ReadResult(response);
      } catch (Exception e) {
        Debug.WriteLine(e);
        throw;
      }
    }

    public async Task<String> PostAsync(String resourcePath, String json) {

      try {
        var content = new StringContent(json, Encoding.UTF8, "application/json");
        // example of how to use FormUrl instead.
        //var content = new FormUrlEncodedContent(new[] 
        //    {
        //        new KeyValuePair<string, string>("", "login")
        //    });

        var response = await _httpClient.PostAsync(resourcePath, content);
        return await ReadResult(response);
      }
      catch (Exception e) {
        Debug.WriteLine(e);
        throw;
      }
    }

    private static async Task<string> ReadResult(HttpResponseMessage response) {

      var result = await response.Content.ReadAsStringAsync();
      if (!response.IsSuccessStatusCode) {
        throw new DataServiceRequestException(response, result);
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


    private HttpClient _httpClient;
    private String _serviceName;
    private static HttpMessageHandler __defaultHttpMessageHandler;
    private static Object __lock = new Object();

  }

  /// <summary>
  /// Exception thrown when a DataService request fails.
  /// See the HttpResponse property for detailed information on the failed request.
  /// </summary>
  public class DataServiceRequestException : HttpRequestException {
    public DataServiceRequestException(String msg) : base(msg) {

    }

    public DataServiceRequestException(HttpResponseMessage httpResponse, String responseContent) : base(httpResponse.ReasonPhrase) {
      HttpResponse = httpResponse;
      ResponseContent = responseContent;
    }

    public String ResponseContent { get; private set; }
    public HttpResponseMessage HttpResponse { get; private set; }
    
  }

}
