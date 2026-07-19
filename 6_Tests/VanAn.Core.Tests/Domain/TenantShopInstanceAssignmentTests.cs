using VanAn.Shared.Domain;
using VanAn.Shared.Domain.Aggregates.TenantAggregate;
using Xunit;
using Tenant = VanAn.Shared.Domain.Aggregates.TenantAggregate.Tenant;

namespace VanAn.Core.Tests.Domain
{
    /// <summary>
    /// Phase 1 (Multi-VPS Checkout): Unit tests for Tenant.AssignToShopInstance.
    /// Verifies the FK assignment + validation for multi-VPS routing.
    /// </summary>
    public class TenantShopInstanceAssignmentTests
    {
        [Fact]
        public void AssignToShopInstance_SetsShopInstanceId()
        {
            var tenantId = new TenantId(Guid.NewGuid());
            var tenant = Tenant.CreateCompany(tenantId, "Test Tenant");
            var shopInstanceId = Guid.NewGuid();

            tenant.AssignToShopInstance(shopInstanceId);

            Assert.Equal(shopInstanceId, tenant.ShopInstanceId);
        }

        [Fact]
        public void AssignToShopInstance_WithEmptyGuid_ThrowsArgumentException()
        {
            var tenantId = new TenantId(Guid.NewGuid());
            var tenant = Tenant.CreateCompany(tenantId, "Test Tenant");

            Assert.Throws<ArgumentException>(() => tenant.AssignToShopInstance(Guid.Empty));
        }

        [Fact]
        public void AssignToShopInstance_CanReassignToDifferentInstance()
        {
            var tenantId = new TenantId(Guid.NewGuid());
            var tenant = Tenant.CreateCompany(tenantId, "Test Tenant");
            var first = Guid.NewGuid();
            var second = Guid.NewGuid();

            tenant.AssignToShopInstance(first);
            tenant.AssignToShopInstance(second);

            Assert.Equal(second, tenant.ShopInstanceId);
        }

        [Fact]
        public void NewTenant_HasNullShopInstanceId_ByDefault()
        {
            var tenantId = new TenantId(Guid.NewGuid());
            var tenant = Tenant.CreateCompany(tenantId, "Test Tenant");

            Assert.Null(tenant.ShopInstanceId);
        }
    }
}
