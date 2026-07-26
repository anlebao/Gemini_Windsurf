using VanAn.Shared.Domain;
using Xunit;

namespace VanAn.Core.Tests.Community
{
    /// <summary>
    /// CommunityRole entity tests (Community Commerce Sprint 0).
    /// Cases 1-4: creation, SalesmanCode generation, deactivate.
    /// </summary>
    public class CommunityRoleTests
    {
        [Fact(DisplayName = "1: CommunityRole_Create_Shipper_ValidFields")]
        public void CommunityRole_Create_Shipper_ValidFields()
        {
            var tenantId = new TenantId(Guid.NewGuid());
            var customerId = Guid.NewGuid();
            var activatedBy = Guid.NewGuid();

            var role = new CommunityRole(tenantId, customerId, CommunityRoleType.Shipper, activatedBy);

            Assert.Equal(customerId, role.CustomerId);
            Assert.Equal(CommunityRoleType.Shipper, role.RoleType);
            Assert.True(role.IsActive);
            Assert.Null(role.SalesmanCode);
            Assert.NotEqual(DateTime.MinValue, role.ActivatedAt);
        }

        [Fact(DisplayName = "2: CommunityRole_Create_Salesman_GeneratesCode")]
        public void CommunityRole_Create_Salesman_GeneratesCode()
        {
            var tenantId = new TenantId(Guid.NewGuid());
            var customerId = Guid.NewGuid();
            var activatedBy = Guid.NewGuid();

            var role = new CommunityRole(tenantId, customerId, CommunityRoleType.Salesman, activatedBy);

            Assert.NotNull(role.SalesmanCode);
            Assert.Equal(6, role.SalesmanCode!.Length);
            // Alphanumeric, no ambiguous chars (0, O, I, 1)
            Assert.Matches("^[ABCDEFGHJKLMNPQRSTUVWXYZ23456789]{6}$", role.SalesmanCode);
        }

        [Fact(DisplayName = "3: CommunityRole_Deactivate_SetsDeactivatedAt")]
        public void CommunityRole_Deactivate_SetsDeactivatedAt()
        {
            var tenantId = new TenantId(Guid.NewGuid());
            var role = new CommunityRole(tenantId, Guid.NewGuid(), CommunityRoleType.Shipper, Guid.NewGuid());

            role.Deactivate();

            Assert.False(role.IsActive);
            Assert.NotNull(role.DeactivatedAt);
        }

        [Fact(DisplayName = "4: CommunityRole_SalesmanCode_Unique_AcrossInstances")]
        public void CommunityRole_SalesmanCode_Unique_AcrossInstances()
        {
            var tenantId = new TenantId(Guid.NewGuid());

            var role1 = new CommunityRole(tenantId, Guid.NewGuid(), CommunityRoleType.Salesman, Guid.NewGuid());
            var role2 = new CommunityRole(tenantId, Guid.NewGuid(), CommunityRoleType.Salesman, Guid.NewGuid());

            Assert.NotEqual(role1.SalesmanCode, role2.SalesmanCode);
        }
    }
}
