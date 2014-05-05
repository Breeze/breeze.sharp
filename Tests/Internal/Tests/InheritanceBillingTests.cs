using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Threading.Tasks;
using System.Linq;
using Breeze.Sharp.Core;
using Breeze.Sharp;
using System.Collections.Generic;

using Model.Inheritance.Billing;
using System.Collections;

namespace Breeze.Sharp.Tests {

  [TestClass]
  public class InheritanceBillingTests {

    private String _serviceName;
    private MetadataStore _metadataStore = new MetadataStore();

    [TestInitialize]
    public void TestInitializeMethod() {
      // Note: the NamingConvention.Add method MUST come before the ProbeAssemblies call.
      
      _metadataStore.NamingConvention = 
        _metadataStore.NamingConvention.WithClientServerNamespaceMapping("Model.Inheritance.Billing", "Inheritance.Models");
      Configuration.Instance.ProbeAssemblies(typeof(BillingDetailTPC).Assembly);
      
      _serviceName = "http://localhost:7150/breeze/Inheritance/";
    }

    public async Task<EntityManager> NewEm() {
      return await TestFns.NewEm(_serviceName, _metadataStore);
    }

    [TestCleanup]
    public void TearDown() {
      
    }

    [TestMethod]
    public async Task SimpleTPH() {
      var em1 = await NewEm();
      var r = await QueryBillingBase<BillingDetailTPH>(em1, "BillingDetailTPH");
    }

    [TestMethod]
    public async Task SimpleBillingDetailTPT() {
      var em1 = await NewEm();
      var r = await QueryBillingBase<BillingDetailTPT>(em1, "BillingDetailTPT");
    }

    [TestMethod]
    public async Task SimpleBillingDetailTPC() {
      var em1 = await NewEm();
      var r = await QueryBillingBase<BillingDetailTPC>(em1, "BillingDetailTPC");
    }

    [TestMethod]
    public async Task SimpleBillingDetailTPH() {
      var em1 = await NewEm();
      var r = await QueryBillingBase<BillingDetailTPH>(em1, "BillingDetailTPH");
    }

    [TestMethod]
    public async Task SimpleBankAccountTPT() {
      var em1 = await NewEm();
      var r = await QueryBillingBase<BankAccountTPT>(em1, "BankAccountTPT");
    }

    [TestMethod]
    public async Task SimpleBankAccountTPC() {
      var em1 = await NewEm();
      var r = await QueryBillingBase<BankAccountTPC>(em1, "BankAccountTPC");
    }

    [TestMethod]
    public async Task SimpleBankAccountTPH() {
      var em1 = await NewEm();
      var r = await QueryBillingBase<BankAccountTPH>(em1, "BankAccountTPH");
    }

    [TestMethod]
    public async Task CanDeleteBankAccountTPT() {
      var em1 = await NewEm();
      var r = await CanDeleteBillingBase<BankAccountTPT>(em1, "BankAccountTPT", "Deposits");
    }

    [TestMethod]
    public async Task CanDeleteSimpleBankAccountTPC() {
      var em1 = await NewEm();
      var r = await CanDeleteBillingBase<BankAccountTPC>(em1, "BankAccountTPC", "Deposits");
    }

    [TestMethod]
    public async Task CanDeleteSimpleBankAccountTPH() {
      var em1 = await NewEm();
      var r = await CanDeleteBillingBase<BankAccountTPH>(em1, "BankAccountTPH", "Deposits");
    }

    [TestMethod]
    public async Task CreateBankAccoutTPT() {
      var em1 = await NewEm();
      var r = await CreateBillingDetail<BankAccountTPT>(em1);
    }

    [TestMethod]
    public async Task CreateBankAccountTPC() {
      var em1 = await NewEm();
      var r = await CreateBillingDetail<BankAccountTPC>(em1);
    }

    [TestMethod]
    public async Task CreateBankAccountTPH() {
      var em1 = await NewEm();
      var r = await CreateBillingDetail<BankAccountTPH>(em1);
    }

    private async Task<IEnumerable<T>> QueryBillingBase<T>(EntityManager em, String typeName) where T: IBillingDetail {
      var q0 = new EntityQuery<T>(typeName + "s").With(em);
      var r0 = await q0.Execute();
      if (r0.Count() == 0) {
        Assert.Inconclusive("Please restart the server - inheritance data was deleted by prior tests");
      }
      
      Assert.IsTrue(r0.All(r => typeof(T).IsAssignableFrom(r.GetType())));
      Assert.IsTrue(r0.All(r => r.Owner == r.Owner.ToUpper()), "all owners should be uppercase (from initializer)");
      Assert.IsTrue(r0.All(r => r.MiscData == "asdf"), "all 'MiscData' should be 'asdf' (from initializer)");
      return r0;
    }

    private async Task<IEnumerable<T>> CanDeleteBillingBase<T>(EntityManager em, String typeName, String expandPropName = null) where T:IEntity {
      //    // Registering resource names for each derived type
      //    // because these resource names are not in metadata
      //    // because there are no corresponding DbSets in the DbContext
      //    // and that's how Breeze generates resource names
      em.MetadataStore.SetResourceName(typeName + "s", typeof(T));
      var q0 = new EntityQuery<T>(typeName + "s").With(em).Take(1);
      if (expandPropName != null) {
        q0 = q0.Expand(expandPropName);
      }
      var r0 = await q0.Execute();
      if (r0.Count() == 0) {
        Assert.Inconclusive("Please restart the server - inheritance data was deleted by prior tests");
      }
      var targetEntity = r0.First();
      var targetKey = targetEntity.EntityAspect.EntityKey;
      List<IEntity> dependentEntities = null;
      if (expandPropName != null) {
        var expandVal = (IEnumerable)targetEntity.EntityAspect.GetValue(expandPropName);
        dependentEntities = expandVal.Cast<IEntity>().ToList();
        dependentEntities.ForEach(de => de.EntityAspect.Delete());
      }
      targetEntity.EntityAspect.Delete();
      var sr0 = await em.SaveChanges();
      var deletedEntities = sr0.Entities;
      Assert.IsTrue(deletedEntities.Contains(targetEntity), "should contain target");
      if (expandPropName != null) {
        Assert.IsTrue(deletedEntities.Count == dependentEntities.Count + 1);
      }
      Assert.IsTrue(deletedEntities.All(de => de.EntityAspect.EntityState.IsDetached()), "should all be detached");

      // try to refetch deleted
      var r1 = await em.ExecuteQuery(targetKey.ToQuery<T>());
      Assert.IsTrue(r1.Count() == 0, "should not be able to find entity after delete");
      return r1;
    }

    private async Task<SaveResult> CreateBillingDetail<T>(EntityManager em) where T:IBillingDetail, IEntity {
      var bd = em.CreateEntity<T>(EntityState.Detached);
      var ba = bd as IBankAccount;
      if (ba != null) {
        // because of EF TPC issues - see server comments.
        if (typeof(T) == typeof(BankAccountTPC)) {
          ba.Id = TestFns.GetNextInt();
        }
        ba.CreatedAt = DateTime.Now;
        ba.Owner = "Scrooge McDuck";
        ba.Number = "999-999-9";
        ba.BankName = "Bank of Duckburg";
        ba.Swift = "RICHDUCK";
      } else {
        // bd.Id = TestFns.GetNextInt();
        bd.CreatedAt = DateTime.Now;
        bd.Owner = "Richie Rich";
        bd.Number = "888-888-8";
      }
      em.AddEntity(bd);
      Assert.IsTrue(bd.MiscData == "asdf");
      return (SaveResult)null;
      // TODO: figure out how to save here
      //SaveResult sr = null;
      //try {
      //  sr = await em.SaveChanges();
      //  Assert.IsTrue(bd.EntityAspect.EntityState.IsUnchanged());
        
      //} catch (Exception e) {
      //  var x = e;
      //  throw;
      //}
      //return sr;
    }

 


  }
}

  
