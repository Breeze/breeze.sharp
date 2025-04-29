using System;
using System.Linq;
using System.Threading.Tasks;
using Foo;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Breeze.Sharp.Tests
{
    [TestClass]
    public class QueryConcurrencyTests
    {
        private String _serviceName;

        [TestInitialize]
        public void TestInitializeMethod()
        {
            Configuration.Instance.ProbeAssemblies(typeof(Customer).Assembly);
            //_serviceName = "http://localhost:7150/breeze/NorthwindIBModel/";
            _serviceName = TestFns.serviceName;
        }



        [TestCleanup]
        public void TearDown()
        {

        }

        [TestMethod]
        public async Task ConcurrentQueryResultsAreAddedToTheEntityManagerCache()
        {
            var em = await TestFns.NewEm(_serviceName);

            var q = EntityQuery.From<Customer>().Take(10).Select(x => new { x.CustomerID});

            var ids = (await q.Execute(em)).Select(x => x.CustomerID).ToList();

            Assert.IsNotNull(ids);
            Assert.IsTrue(ids.Count == 10);           

            em.Clear();

            var customerTasks =
                ids.Select(x =>
                {
                    var customerQuery = EntityQuery.From<Customer>().Where(c => c.CustomerID == x);

                    return em.ExecuteQuery(customerQuery);

                }).ToArray();

            Task.WaitAll(customerTasks);

            var customers =
                customerTasks.Select(x => x.Status == TaskStatus.RanToCompletion 
                                         ? x.Result.FirstOrDefault() 
                                         : null)
                             .ToList();

            CollectionAssert.AllItemsAreNotNull(customers);

            var localCustomersQuery = EntityQuery.From<Customer>();

            var localCustomers = em.ExecuteQueryLocally(localCustomersQuery).ToDictionary(x => x.CustomerID);

            Assert.IsTrue(customers.All(x => localCustomers.ContainsKey(x.CustomerID)));
        }
    }
}
