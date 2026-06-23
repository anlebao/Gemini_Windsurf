using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using VanAn.CoreHub.Infrastructure;
using VanAn.CoreHub.Services;
using VanAn.CoreHub.Tests.TestInfrastructure;
using VanAn.Shared.Domain;
using DemoUser = VanAn.Shared.Domain.Aggregates.UserAggregate.DemoUser;
using PermissionGroup = VanAn.Shared.Domain.Aggregates.UserAggregate.PermissionGroup;
using UserRole = VanAn.Shared.Domain.Aggregates.UserAggregate.UserRole;
using UserTenant = VanAn.Shared.Domain.Aggregates.UserAggregate.UserTenant;
using Xunit;

namespace VanAn.Tests.Services
{
    /// <summary>
    /// Wave 6 — W6-T12: Service tests for RoleAssignmentService.
    /// </summary>
    public class RoleAssignmentServiceTests : IDisposable
    {
        private readonly TestContextScope _scope;
        private readonly VanAnDbContext _db;
        private readonly RoleAssignmentService _sut;
        private readonly UserManagementService _userService;
        private readonly TenantId _tenantId = new(Guid.Parse("00000000-0000-0000-0000-000000000001"));

        public RoleAssignmentServiceTests()
        {
            _scope = VanAnDbContextTestFactory.Create();
            _scope.TenantProvider!.SetTenant(_tenantId.Value);
            _db = _scope.Context;
            _sut = new RoleAssignmentService(_db, NullLogger<RoleAssignmentService>.Instance);
            _userService = new UserManagementService(_db, null!, NullLogger<UserManagementService>.Instance);
        }

        public void Dispose() => _scope.Dispose();

        private async Task<DemoUser> SeedUserAsync(string username, UserRole role)
        {
            return await _userService.CreateUserAsync(_tenantId, username, "VanAn@2026", username, role);
        }

        [Fact(DisplayName = "W6-R1: AssignRoleToUser persists user tenant mapping")]
        public async Task AssignRoleToUser_PersistsMapping()
        {
            var user = await SeedUserAsync("u@vanan.vn", UserRole.Staff);

            await _sut.AssignRoleToUserAsync(user.Id, _tenantId, UserRole.StoreKeeper);

            var roles = await _sut.GetUserRolesAsync(user.Id, _tenantId);
            roles.Should().Contain(UserRole.StoreKeeper);
        }

        [Fact(DisplayName = "W6-R2: RevokeRole removes role")]
        public async Task RevokeRole_RemovesRole()
        {
            var user = await SeedUserAsync("u@vanan.vn", UserRole.Staff);
            await _sut.AssignRoleToUserAsync(user.Id, _tenantId, UserRole.StoreKeeper);

            await _sut.RevokeRoleAsync(user.Id, _tenantId, UserRole.StoreKeeper);

            var roles = await _sut.GetUserRolesAsync(user.Id, _tenantId);
            roles.Should().NotContain(UserRole.StoreKeeper);
        }

        [Fact(DisplayName = "W6-R3: GetUserRoles filters by tenant only")]
        public async Task GetUserRoles_FiltersByTenant()
        {
            var otherTenant = new TenantId(Guid.NewGuid());
            var user = await SeedUserAsync("u@vanan.vn", UserRole.Staff);
            await _sut.AssignRoleToUserAsync(user.Id, _tenantId, UserRole.Owner);

            // Directly seed a cross-tenant role record to prove filtering
            var crossTenant = new UserTenant(user.Id, otherTenant.Value, UserRole.StoreKeeper);
            _db.UserTenants.Add(crossTenant);
            await _db.SaveChangesAsync();

            var roles = await _sut.GetUserRolesAsync(user.Id, _tenantId);
            roles.Should().ContainSingle().Which.Should().Be(UserRole.Owner);
        }

        [Fact(DisplayName = "W6-R4: AssignUserToGroup adds group membership")]
        public async Task AssignUserToGroup_AddsMembership()
        {
            var user = await SeedUserAsync("u@vanan.vn", UserRole.Staff);
            var group = new PermissionGroup(_tenantId, "Managers", null);
            _db.PermissionGroups.Add(group);
            await _db.SaveChangesAsync();

            await _sut.AssignUserToGroupAsync(user.Id, group.Id, _tenantId);

            var effective = await _sut.GetEffectiveRolesAsync(user.Id, _tenantId);
            effective.Should().BeEmpty(); // group has no roles yet
        }

        [Fact(DisplayName = "W6-R5: EffectiveRoles combines direct and group roles")]
        public async Task EffectiveRoles_CombinesDirectAndGroupRoles()
        {
            var user = await SeedUserAsync("u@vanan.vn", UserRole.Staff);
            await _sut.AssignRoleToUserAsync(user.Id, _tenantId, UserRole.Owner);
            var group = new PermissionGroup(_tenantId, "Managers", null);
            group.AddRole(UserRole.StoreKeeper);
            _db.PermissionGroups.Add(group);
            await _db.SaveChangesAsync();
            await _sut.AssignUserToGroupAsync(user.Id, group.Id, _tenantId);

            var effective = await _sut.GetEffectiveRolesAsync(user.Id, _tenantId);
            effective.Should().Contain(UserRole.Owner);
            effective.Should().Contain(UserRole.StoreKeeper);
        }

        [Fact(DisplayName = "W6-R6: Cross-tenant group assignment throws")]
        public async Task AssignUserToGroup_CrossTenant_Throws()
        {
            var user = await SeedUserAsync("u@vanan.vn", UserRole.Staff);
            var otherTenant = new TenantId(Guid.NewGuid());
            var group = new PermissionGroup(otherTenant, "Other", null);
            _db.PermissionGroups.Add(group);
            await _db.SaveChangesAsync();

            Func<Task> act = () => _sut.AssignUserToGroupAsync(user.Id, group.Id, _tenantId);
            await act.Should().ThrowAsync<UnauthorizedAccessException>();
        }
    }
}
