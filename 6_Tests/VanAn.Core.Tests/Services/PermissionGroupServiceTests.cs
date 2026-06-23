using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using VanAn.CoreHub.Infrastructure;
using VanAn.CoreHub.Services;
using VanAn.CoreHub.Tests.TestInfrastructure;
using VanAn.Shared.Domain;
using PermissionGroup = VanAn.Shared.Domain.Aggregates.UserAggregate.PermissionGroup;
using UserRole = VanAn.Shared.Domain.Aggregates.UserAggregate.UserRole;
using Xunit;

namespace VanAn.Tests.Services
{
    /// <summary>
    /// Wave 6 — W6-T12: Service tests for PermissionGroupService.
    /// </summary>
    public class PermissionGroupServiceTests : IDisposable
    {
        private readonly TestContextScope _scope;
        private readonly VanAnDbContext _db;
        private readonly PermissionGroupService _sut;
        private readonly TenantId _tenantId = new(Guid.Parse("00000000-0000-0000-0000-000000000001"));

        public PermissionGroupServiceTests()
        {
            _scope = VanAnDbContextTestFactory.Create();
            _scope.TenantProvider!.SetTenant(_tenantId.Value);
            _db = _scope.Context;
            _sut = new PermissionGroupService(_db, NullLogger<PermissionGroupService>.Instance);
        }

        public void Dispose() => _scope.Dispose();

        [Fact(DisplayName = "W6-G1: CreateGroup persists group")]
        public async Task CreateGroup_PersistsGroup()
        {
            var group = await _sut.CreateGroupAsync(_tenantId, "Cashiers", "POS staff");

            group.Should().NotBeNull();
            group.Name.Should().Be("Cashiers");
            group.TenantId.Should().Be(_tenantId);
        }

        [Fact(DisplayName = "W6-G2: CreateGroup with empty name throws")]
        public async Task CreateGroup_EmptyName_Throws()
        {
            Func<Task> act = () => _sut.CreateGroupAsync(_tenantId, "", null);
            await act.Should().ThrowAsync<ArgumentException>();
        }

        [Fact(DisplayName = "W6-G3: ListGroups returns only tenant groups")]
        public async Task ListGroups_ReturnsTenantGroups()
        {
            var otherTenant = new TenantId(Guid.NewGuid());
            await _sut.CreateGroupAsync(_tenantId, "A", null);
            await _sut.CreateGroupAsync(otherTenant, "B", null);

            var list = await _sut.ListGroupsAsync(_tenantId);
            list.Should().HaveCount(1);
            list[0].Name.Should().Be("A");
        }

        [Fact(DisplayName = "W6-G4: AddRoleToGroup adds role")]
        public async Task AddRoleToGroup_AddsRole()
        {
            var group = await _sut.CreateGroupAsync(_tenantId, "Managers", null);

            await _sut.AddRoleToGroupAsync(group.Id, _tenantId, UserRole.Owner);

            var updated = await _sut.GetGroupAsync(group.Id, _tenantId);
            updated!.GetEffectiveRoles().Should().Contain(UserRole.Owner);
        }

        [Fact(DisplayName = "W6-G5: RemoveRoleFromGroup removes role")]
        public async Task RemoveRoleFromGroup_RemovesRole()
        {
            var group = await _sut.CreateGroupAsync(_tenantId, "Managers", null);
            await _sut.AddRoleToGroupAsync(group.Id, _tenantId, UserRole.Owner);

            await _sut.RemoveRoleFromGroupAsync(group.Id, _tenantId, UserRole.Owner);

            var updated = await _sut.GetGroupAsync(group.Id, _tenantId);
            updated!.GetEffectiveRoles().Should().NotContain(UserRole.Owner);
        }

        [Fact(DisplayName = "W6-G6: GetGroup cross-tenant throws")]
        public async Task GetGroup_CrossTenant_Throws()
        {
            var otherTenant = new TenantId(Guid.NewGuid());
            var group = await _sut.CreateGroupAsync(otherTenant, "Other", null);

            Func<Task> act = () => _sut.GetGroupAsync(group.Id, _tenantId);
            await act.Should().ThrowAsync<UnauthorizedAccessException>();
        }

        [Fact(DisplayName = "W6-G7: UpdateGroup changes name")]
        public async Task UpdateGroup_ChangesName()
        {
            var group = await _sut.CreateGroupAsync(_tenantId, "Old", "desc");

            await _sut.UpdateGroupAsync(group.Id, _tenantId, "New", "new desc");

            var updated = await _sut.GetGroupAsync(group.Id, _tenantId);
            updated!.Name.Should().Be("New");
            updated.Description.Should().Be("new desc");
        }
    }
}
