using System.Reflection;

using Breeze.Sharp.Core;

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;
using System.Threading;
using System.Data.Services.Client;
using System.Data.Services.Common;
using Newtonsoft.Json;
using Breeze.Sharp.Json;

namespace Breeze.Sharp {


  // TODO: EntityQuery is currently just additive - i.e. no way to remove clauses

  /// <summary>
  /// An EntityQuery instance is used to query entities either from a remote datasource or from a local EntityManager.
  /// EntityQueries are immutable - this means that all EntityQuery methods that return an EntityQuery actually create a new EntityQuery. 
  /// Therefore EntityQueries can be 'modified' without affecting any current instances.
  /// </summary>
  /// <typeparam name="T"></typeparam>
  public class EntityQuery<T> : EntityQuery, IQueryable<T>, IOrderedQueryable<T>, IQueryProvider {

    /// <summary>
    /// Constructor
    /// </summary>
    public EntityQuery() : base() {
      var context = new DataServiceContext(new Uri(__placeholderServiceName), DataServiceProtocolVersion.V3);
      DataServiceQuery = (DataServiceQuery<T>)context.CreateQuery<T>(__placeholderResourceName).Where(x => true);
      QueryableType = typeof(T);
    }

    /// <summary>
    /// Contructor for a query against a specific resource.
    /// </summary>
    /// <param name="resourceName"></param>
    public EntityQuery(String resourceName)
      : this() {
      if (resourceName != null) ResourceName = resourceName;
    }

    /// <summary>
    /// May be called by subclasses that need to add additional behavior to a query.
    /// The basic idea is to use this method to clone the query first and then add
    /// or modify properties on the cloned instance. 
    /// </summary>
    /// <param name="query"></param>
    protected EntityQuery(EntityQuery<T> query) : base(query) {
      DataServiceQuery = query.DataServiceQuery;
    }

    /// <summary>
    /// For internal use only.
    /// </summary>
    /// <returns></returns>
    public override object Clone() {
      return new EntityQuery<T>(this);
    }

    /// <summary>
    /// Returns a new query with the specified resource name.
    /// </summary>
    /// <param name="resourceName"></param>
    /// <returns></returns>
    public EntityQuery<T> From(String resourceName) {
      var q = new EntityQuery<T>(this);
      q.ResourceName = resourceName;
      return q;
    }

    /// <summary>
    /// Executes this query, against an optionally specified EntityManager. If no EntityManager
    /// is specified then the query is run on the EntityManager specified by the EntityManager 
    /// property on this instance. ( <see cref="EntityQueryExtensions.With{TQuery}(TQuery, EntityManager)"/> )
    /// If this value is null an exception will be thrown.
    /// </summary>
    /// <param name="entityManager"></param>
    /// <returns></returns>
    public new async Task<IEnumerable<T>> Execute(EntityManager entityManager = null) {
      return await Execute(CancellationToken.None, entityManager);
    }

    /// <summary>
    /// Executes this query, against an optionally specified EntityManager. If no EntityManager
    /// is specified then the query is run on the EntityManager specified by the EntityManager 
    /// property on this instance. ( <see cref="EntityQueryExtensions.With{TQuery}(TQuery, EntityManager)"/> )
    /// If this value is null an exception will be thrown.
    /// </summary>
    /// <param name="cancellationToken"></param>
    /// <param name="entityManager"></param>
    /// <returns></returns>
    public new Task<IEnumerable<T>> Execute(CancellationToken cancellationToken, EntityManager entityManager = null) {
      entityManager = CheckEm(entityManager);

      cancellationToken.ThrowIfCancellationRequested();

      return entityManager.ExecuteQuery<T>(this, cancellationToken);
    }

    /// <summary>
    /// Executes this query against the EntityManager's local cache, with an optionally specfied EntityManager.
    /// If no EntityManager
    /// is specified then the query is run on the EntityManager specified by the EntityManager 
    /// property on this instance. ( <see cref="EntityQueryExtensions.With{TQuery}(TQuery, EntityManager)"/> )
    /// If this value is null an exception will be thrown.
    /// </summary>
    /// <param name="entityManager"></param>
    /// <returns></returns>
    public new IEnumerable<T> ExecuteLocally(EntityManager entityManager = null) {
      var result = base.ExecuteLocally(entityManager);
      return result.Cast<T>();
    }

    /// <summary>
    /// Returns a new query that will return related entities nested within its results. 
    /// The Expand method allows you to identify related entities, via navigation property names such 
    /// that a graph of entities may be retrieved with a single request. Any filtering occurs before
    /// the results are 'expanded'.
    /// </summary>
    /// <typeparam name="TTarget"></typeparam>
    /// <param name="navigationPropertyFn"></param>
    /// <returns></returns>
    public EntityQuery<T> Expand<TTarget>(Expression<Func<T, TTarget>> navigationPropertyFn) {
      var q = new EntityQuery<T>(this);
      q.DataServiceQuery = this.DataServiceQuery.Expand(navigationPropertyFn);
      return q;
    }

    /// <summary>
    /// Returns a new query that will return related entities nested within its results. 
    /// The Expand method allows you to identify related entities, via navigation property names such 
    /// that a graph of entities may be retrieved with a single request. Any filtering occurs before
    /// the results are 'expanded'.
    /// </summary>
    /// <param name="path"></param>
    /// <returns></returns>
    public EntityQuery<T> Expand(String path) {
      var q = new EntityQuery<T>(this);
      q.DataServiceQuery = this.DataServiceQuery.Expand(path.Replace('.', '/'));
      return q;
    }

    // can be called from EntityQuery;
    protected internal override EntityQuery ExpandNonGeneric(String path) {
      return Expand(path);
    }

    /// <summary>
    /// Returns a new query that includes a specified parameter to pass to the server.
    /// </summary>
    /// <param name="name"></param>
    /// <param name="value"></param>
    /// <returns></returns>
    public EntityQuery<T> WithParameter(string name, Object value) {
      var q = this;

      if (value != null) {
        q = new EntityQuery<T>(this);
        q.DataServiceQuery = this.DataServiceQuery.AddQueryOption(name, value);
      }

      return q;
    }

    /// <summary>
    /// Returns a new query that includes a collection of parameters to pass to the server.
    /// </summary>
    /// <param name="dictionary"></param>
    /// <returns></returns>
    public EntityQuery<T> WithParameters(IDictionary<String, Object> dictionary) {
      var q = new EntityQuery<T>(this);
      var dsq = this.DataServiceQuery;
      dictionary.Where(kvp => kvp.Value != null).ForEach(kvp => dsq = dsq.AddQueryOption(kvp.Key, kvp.Value));
      q.DataServiceQuery = dsq;
      return q;
    }

    /// <summary>
    /// Returns a query with the 'inlineCount' capability either enabled or disabled. With 
    /// 'InlineCount' enabled, an additional 'InlineCount' property will be returned with the 
    /// query results that will contain the number of entities that would have been returned by 
    /// this query with only the 'where'/'filter' clauses applied, i.e. without any 'skip'/'take'
    /// operators applied. For local queries this clause is ignored.
    /// </summary>
    /// <returns></returns>
    public EntityQuery<T> InlineCount() {
      var q = new EntityQuery<T>(this);
      q.DataServiceQuery = this.DataServiceQuery.IncludeTotalCount();
      return q;
    }

    /// <summary>
    /// For internal use.
    /// </summary>
    /// <returns></returns>
    public override String GetResourcePath(MetadataStore metadataStore) {

      var resourceName = (String.IsNullOrEmpty(ResourceName))
        ? metadataStore.GetDefaultResourceName(this.QueryableType)
        : ResourceName;
      if (String.IsNullOrEmpty(resourceName)) {
        throw new Exception("Cannot find a default resource name for CLR type: " + this.QueryableType.FullName);
      }

      if (Configuration.Instance.QueryUriStyle == QueryUriStyle.JSON) {
        return GetJsonResourcePath(resourceName);
      } else {
        return GetOdataResourcePath(resourceName);
      }
    }

    /// <summary> Return the query as JSON url, e.g. "Customer?{where:{FirstName:'Maria'}}" </summary>
    private string GetJsonResourcePath(string resourceName) {
      var json = JsonQueryExpressionVisitor.Translate(this.Expression, out string parameters);
      if (json.Length > 2) {
        // TODO may be able to get away with not escaping the URI
        System.Diagnostics.Debug.WriteLine($"json query: {json}");
        var uri = Uri.EscapeUriString(json);
        return resourceName + '?' + uri + (parameters != null ? "&" + parameters : string.Empty);
      } else {
        return resourceName;
      }
    }

    /// <summary> Return the query as Odata URL, e.g. "Customer?$filter=FirstName%20eq%20'Maria'" </summary>
    private string GetOdataResourcePath(string resourceName) {
      var dsq = this.DataServiceQuery;
      var requestUri = dsq.RequestUri.AbsoluteUri;
      var s2 = requestUri.Replace(__placeholderServiceName, "");


      // if any filter conditions
      var queryResource = s2.Replace(__placeholderResourceName + "()", resourceName);
      // if no filter conditions
      queryResource = queryResource.Replace(__placeholderResourceName, resourceName);

      // TODO: Hack to avoid DataServiceQuery from inferring the entity key
      queryResource = queryResource.Replace("$filter=true%20and%20", "$filter=");
      queryResource = queryResource.Replace("$filter=true", "");
      // HACK
      queryResource = RewriteResourcePath(queryResource);

      return queryResource;
    }

    private string RewriteResourcePath(String resourcePath) {
      // This whole method is a hack to get around the fact that MS's current server OData implementations
      // do not understand the valid 'cast' operator the DataServiceQuery generates in the url.
      // So as a result we need to translate the cast query into a string query that the current server OData
      // implementation DOES understand. 
      return RewriteEdmCastOperations(RewriteEnumFilters(resourcePath));
    }

    private string RewriteEdmCastOperations(String resourcePath) {
      var pattern = @"(?<prefix>.*)cast\((?<propName>.*),'Edm\..*'\)(?<suffix>.*)";
      var m = System.Text.RegularExpressions.Regex.Match(resourcePath, pattern);
      if (m.Success) {

        var result = m.Groups["prefix"].Value
                   + m.Groups["propName"].Value
                   + m.Groups["suffix"].Value;
        // in case there is another cast in the string.
        return RewriteResourcePath(result);
      } else {
        return resourcePath;
      }
    }

    private string RewriteEnumFilters(string resourcePath) {

      var pattern = @"(?<prefix>.*)cast\((?<enumName>.*),.*\)%20eq%20(?<enumValue>.[^%&)]*)(?<suffix>.*)";
      var m = System.Text.RegularExpressions.Regex.Match(resourcePath, pattern);
      if (m.Success) {
        var enumName = m.Groups["enumName"].Value;
        var enumValue = m.Groups["enumValue"].Value;
        var pinfo = this.QueryableType.GetTypeInfo().GetDeclaredProperty(enumName);
        if (pinfo == null) return resourcePath;
        var enumType = TypeFns.GetNonNullableType(pinfo.PropertyType);
        if (!enumType.GetTypeInfo().IsEnum) return resourcePath;
        var enumString = "'" + Enum.GetName(enumType, Int32.Parse(enumValue)) + "'";
        var result = m.Groups["prefix"].Value
                   + enumName + "%20eq%20" + enumString
                   + m.Groups["suffix"].Value;
        // in case there is another cast in the string.
        return RewriteResourcePath(result);
      } else {
        return resourcePath;
      }
    }

    #region IQueryable impl 

    public IEnumerator<T> GetEnumerator() {
      throw new Exception("EntityQueries cannot be enumerated because they can only be executed asynchronously");
    }

    System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() {
      throw new Exception("EntityQueries cannot be enumerated because they can only be executed asynchronously");
    }

    //public Type ElementType {
    //  get { return DataServiceQuery.ElementType; }
    //}

    public override Expression Expression {
      get { return DataServiceQuery.Expression; }
    }

    public IQueryProvider Provider {
      get { return this; }
    }

    #endregion

    #region IQueryProvider Members

    public EntityQuery(Expression expression, IQueryable queryable) {
      var oldDataServiceQuery = ((IHasDataServiceQuery)queryable).DataServiceQuery;
      DataServiceQuery = (DataServiceQuery<T>)oldDataServiceQuery.Provider.CreateQuery<T>(expression);
      UpdateFrom((EntityQuery)queryable);
    }

    /// <summary>
    /// Internal use only - part of <see cref="IQueryProvider"/> implementation.
    /// </summary>
    /// <typeparam name="TElement"></typeparam>
    /// <param name="expression"></param>
    /// <returns></returns>
    IQueryable<TElement> IQueryProvider.CreateQuery<TElement>(Expression expression) {
      return new EntityQuery<TElement>(expression, this);
    }

    /// <summary>
    /// Internal use only - part of <see cref="IQueryProvider"/> implementation.
    /// </summary>
    /// <param name="expression"></param>
    /// <returns></returns>
    IQueryable IQueryProvider.CreateQuery(Expression expression) {
      // Not sure when this is called but it IS called on OfType() resolution
      var methodExpr = (MethodCallExpression)expression;
      // return type will be an IQueryable<X> 
      var returnType = methodExpr.Method.ReturnType;
      // extract X
      var typeT = TypeFns.GetGenericArgument(returnType);
      // now do the equivalent of => return new EntityQuery<{typeT}>(expression, this);
      var query = TypeFns.ConstructGenericInstance(typeof(EntityQuery<>), new Type[] { typeT },
        expression, this);
      return (IQueryable)query;
    }

    /// <summary>
    /// Internal use only - part of <see cref="IQueryProvider"/> implementation.
    /// </summary>
    /// <typeparam name="TResult"></typeparam>
    /// <param name="expression"></param>
    /// <returns></returns>
    TResult IQueryProvider.Execute<TResult>(Expression expression) {
      throw new Exception("EntityQueries can only be executed asynchronously");
    }



    /// <summary>
    /// Internal use only - part of <see cref="IQueryProvider"/> implementation.
    /// </summary>
    /// <param name="expression"></param>
    /// <returns></returns>
    Object IQueryProvider.Execute(Expression expression) {
      throw new Exception("EntityQueries can only be executed asynchronously");
    }

    #endregion 

    /// <summary>
    /// The element type of the IEnumerable{T} returned by this query.
    /// </summary>
    public override Type ElementType {
      get { return typeof(T); }
    }

    /// <summary>
    /// For internal use.
    /// </summary>
    protected new DataServiceQuery<T> DataServiceQuery {
      get { return (DataServiceQuery<T>)base.DataServiceQuery; }
      set { base.DataServiceQuery = value; }
    }


    private static String __placeholderServiceName = "http://localhost:7890/breeze/Undefined/";
    private static String __placeholderResourceName = "__Undefined__";

  }

  /// <summary>
  /// Base class for all EntityQueries.  This class is untyped and may be used
  /// when you need to create entity queries dynamically.
  /// </summary>
  public abstract class EntityQuery : IEntityQuery, IHasDataServiceQuery {
    public EntityQuery() {
      QueryOptions = new QueryOptions();
    }

    /// <summary>
    /// Creates a new typed EntityQuery for a specific type.  Same as new EntityQuery{T}
    /// but can be used where the generic parameter 'T' cannot be specified.
    /// </summary>
    /// <param name="clrEntityType"></param>
    /// <returns></returns>
    public static EntityQuery Create(Type clrEntityType) {
      var queryType = typeof(EntityQuery<>).MakeGenericType(clrEntityType);
      return (EntityQuery)Activator.CreateInstance(queryType);
    }

    /// <summary>
    /// The same as calling "new EntityQuery{T}.(resourceName)".
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <returns></returns>
    public static EntityQuery<T> From<T>() {
      return new EntityQuery<T>();
    }

    /// <summary>
    /// The same as calling "new EntityQuery{T}.(resourceName)".
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="resourceName"></param>
    /// <returns></returns>
    public static EntityQuery<T> From<T>(string resourceName) {
      return new EntityQuery<T>(resourceName);
    }

    /// <summary>
    /// This signature is intended to be use for queries that will return 
    /// an anonymous type. An example instance of the anonymous type you want
    /// returned may be passed as the 2nd argument and will be used to 
    /// materialize the result of the query. 
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="resourceName"></param>
    /// <param name="dummy"></param>
    /// <returns></returns>
    public static EntityQuery<T> From<T>(string resourceName, T dummy) {
      return new EntityQuery<T>(resourceName);
    }

    protected EntityQuery(EntityQuery query) {
      UpdateFrom(query);
    }

    /// <summary>
    /// Executes this query against a remote service. 
    /// This method requires that an EntityManager has been previously specified via the 
    /// <see cref="EntityQueryExtensions.With(EntityManager)"/> method.
    /// <see cref="Breeze.Sharp.EntityManager.ExecuteQuery{T}(EntityQuery{T})"/>
    /// </summary>
    /// <param name="entityManager"></param>
    /// <returns></returns>
    public async Task<IEnumerable> Execute(EntityManager entityManager = null) {
      return await Execute(CancellationToken.None, entityManager);
    }

    /// <summary>
    /// Executes this query against a remote service. 
    /// This method requires that an EntityManager has been previously specified via the 
    /// <see cref="EntityQueryExtensions.With(EntityManager)"/> method.
    /// <see cref="Breeze.Sharp.EntityManager.ExecuteQuery{T}(EntityQuery{T})"/>
    /// </summary>
    /// <param name="cancellationToken"></param>
    /// <param name="entityManager"></param>
    /// <returns></returns>
    public Task<IEnumerable> Execute(CancellationToken cancellationToken, EntityManager entityManager = null) {
      entityManager = CheckEm(entityManager);

      cancellationToken.ThrowIfCancellationRequested();

      return entityManager.ExecuteQuery(this, cancellationToken);
    }

    /// <summary>
    /// Executes this query against the local cache. 
    /// This method requires that an EntityManager has been previously specified via the 
    /// <see cref="EntityQueryExtensions.With(EntityManager)"/> method. 
    /// <seealso cref="EntityManager.ExecuteQueryLocally(EntityQuery)"/>
    /// </summary>
    /// <param name="entityManager"></param>
    /// <returns></returns>
    public IEnumerable ExecuteLocally(EntityManager entityManager = null) {
      entityManager = CheckEm(entityManager);
      var lambda = CacheQueryExpressionVisitor.Visit(this, entityManager.CacheQueryOptions);
      var func = lambda.Compile();
      return func(entityManager);
    }

    protected internal abstract EntityQuery ExpandNonGeneric(String path);

    protected void UpdateFrom(EntityQuery query) {
      ResourceName = query.ResourceName;
      ElementType = query.ElementType;
      QueryableType = query.QueryableType;
      DataService = query.DataService;
      EntityManager = query.EntityManager;
      QueryOptions = query.QueryOptions;
    }

    protected EntityManager CheckEm(EntityManager entityManager) {
      entityManager = entityManager ?? this.EntityManager;
      if (entityManager == null) {
        throw new ArgumentException("entityManager parameter is null and this EntityQuery does not have its own EntityManager specified");
      }
      return entityManager;
    }

    /// <summary>
    /// The resource name specified for this query.
    /// </summary>
    public String ResourceName { get; protected internal set; }

    /// <summary>
    /// The element type of the IEnumerable being returned by this query.
    /// </summary>
    public virtual Type ElementType { get; protected set; }

    /// <summary>
    /// The type being queried.  This may not be the same as the type returned in the case 
    /// of a 'Select'.
    /// </summary>
    public virtual Type QueryableType { get; protected set; }

    /// <summary>
    /// The DataService associated with this query.
    /// </summary>
    public DataService DataService { get; protected internal set; }


    /// <summary>
    /// The EntityManager associated with this query.
    /// </summary>
    public EntityManager EntityManager { get; protected internal set; }
    public abstract Expression Expression { get; }
    /// <summary>
    /// The QueryOptions associated with this query. 
    /// </summary>
    public QueryOptions QueryOptions { get; protected internal set; }
    /// <summary>
    /// For internal use only.
    /// </summary>
    /// <returns></returns>
    public abstract object Clone();
    public abstract String GetResourcePath(MetadataStore metadataStore);

    DataServiceQuery IHasDataServiceQuery.DataServiceQuery {
      get { return DataServiceQuery; }
    }
    internal DataServiceQuery DataServiceQuery { get; set; }

    public IJsonResultsAdapter JsonResultsAdapter { get; protected internal set; }
  }

  /// <summary>
  /// Interface for all Entity queries.
  /// </summary>
  public interface IEntityQuery {
    DataService DataService { get; }
    EntityManager EntityManager { get; }
    QueryOptions QueryOptions { get; }
    String ResourceName { get; }
    IJsonResultsAdapter JsonResultsAdapter { get; }
    Object Clone();
  }

  internal interface IHasDataServiceQuery {
    DataServiceQuery DataServiceQuery { get; }
  }
}
