using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using VanAn.CoreHub.Infrastructure;
using VanAn.CoreHub.Services;
using VanAn.CoreHub.Tests.TestInfrastructure;
using VanAn.Shared.Domain;
using DemoUser = VanAn.Shared.Domain.Aggregates.UserAggregate.DemoUser;
using UserRole = VanAn.Shared.Domain.Aggregates.UserAggregate.UserRole;
using Xunit;

namespace VanAn.Tests.Services
{
    /// <summary>
    /// Wave 6 — W6-T12: Service tests for UserManagementService.
    /// Uses SQLite in-memory via VanAnDbContextTestFactory.
    /// </summary>
    public class UserManagementServiceTests : IDisposable
    {
        private readonly TestContextScope _scope;
        private readonly VanAnDbContext _db;
        private readonly Mock<INotificationService> _notificationMock;
        private readonly UserManagementService _sut;
        private readonly TenantId _tenantId = new(Guid.Parse("00000000-0000-0000-0000-000000000001"));

        public UserManagementServiceTests()
        {
            _scope = VanAnDbContextTestFactory.Create();
            _scope.TenantProvider!.SetTenant(_tenantId.Value);
            _db = _scope.Context;
            _notificationMock = new Mock<INotificationService>();
            _notificationMock.Setup(n => n.SendEmailAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
                .ReturnsAsync(true);
            _sut = new UserManagementService(_db, _notificationMock.Object, NullLogger<UserManagementService>.Instance);
        }

        public void Dispose() => _scope.Dispose();

        [Fact(DisplayName = "W6-S1: CreateUser persists user and sends welcome email")]
        public async Task CreateUser_PersistsUser_And_SendsEmail()
        {
            var user = await _sut.CreateUserAsync(_tenantId, "staff@vanan.vn", "VanAn@2026", "Phục Vụ", UserRole.Staff);

            user.Should().NotBeNull();
            user.Username.Should().Be("staff@vanan.vn");
            user.Role.Should().Be(UserRole.Staff);
            user.IsActive.Should().BeTrue();

            var fromDb = await _sut.GetUserByIdAsync(user.Id, _tenantId);
            fromDb.Should().NotBeNull();

            _notificationMock.Verify(n => n.SendEmailAsync("staff@vanan.vn", It.IsAny<string>(), It.IsAny<string>()), Times.Once);
        }

        [Fact(DisplayName = "W6-S2: CreateUser with duplicate username throws")]
        public async Task CreateUser_DuplicateUsername_Throws()
        {
            await _sut.CreateUserAsync(_tenantId, "dup@vanan.vn", "VanAn@2026", "U", UserRole.Staff);

            Func<Task> act = () => _sut.CreateUserAsync(_tenantId, "dup@vanan.vn", "VanAn@2026", "U2", UserRole.Staff);
            await act.Should().ThrowAsync<InvalidOperationException>();
        }

        [Fact(DisplayName = "W6-S3: ListUsers returns only tenant users")]
        public async Task ListUsers_ReturnsTenantUsers()
        {
            var otherTenant = new TenantId(Guid.NewGuid());
            await _sut.CreateUserAsync(_tenantId, "a@vanan.vn", "pwd", "A", UserRole.Staff);
            await _sut.CreateUserAsync(otherTenant, "b@vanan.vn", "pwd", "B", UserRole.Staff);

            var list = await _sut.ListUsersAsync(_tenantId);

            list.Should().HaveCount(1);
            list[0].Username.Should().Be("a@vanan.vn");
        }

        [Fact(DisplayName = "W6-S4: GetUserById cross-tenant throws UnauthorizedAccessException")]
        public async Task GetUserById_CrossTenant_Throws()
        {
            var otherTenant = new TenantId(Guid.NewGuid());
            var user = await _sut.CreateUserAsync(otherTenant, "x@vanan.vn", "pwd", "X", UserRole.Staff);

            Func<Task> act = () => _sut.GetUserByIdAsync(user.Id, _tenantId);
            await act.Should().ThrowAsync<UnauthorizedAccessException>();
        }

        [Fact(DisplayName = "W6-S5: UpdateProfile changes display name")]
        public async Task UpdateProfile_ChangesDisplayName()
        {
            var user = await _sut.CreateUserAsync(_tenantId, "u@vanan.vn", "pwd", "Old", UserRole.Staff);

            await _sut.UpdateProfileAsync(user.Id, _tenantId, "New");

            var updated = await _sut.GetUserByIdAsync(user.Id, _tenantId);
            updated!.DisplayName.Should().Be("New");
        }

        [Fact(DisplayName = "W6-S6: ChangePassword updates password")]
        public async Task ChangePassword_UpdatesPassword()
        {
            var user = await _sut.CreateUserAsync(_tenantId, "u@vanan.vn", "pwd", "U", UserRole.Staff);

            await _sut.ChangePasswordAsync(user.Id, _tenantId, "newpwd");

            var updated = await _sut.GetUserByIdAsync(user.Id, _tenantId);
            BCrypt.Net.BCrypt.Verify("newpwd", updated!.PasswordHash).Should().BeTrue();
        }

        [Fact(DisplayName = "W6-S7: DeactivateUser sets inactive")]
        public async Task DeactivateUser_SetsInactive()
        {
            var user = await _sut.CreateUserAsync(_tenantId, "u@vanan.vn", "pwd", "U", UserRole.Staff);

            await _sut.DeactivateUserAsync(user.Id, _tenantId);

            var updated = await _sut.GetUserByIdAsync(user.Id, _tenantId);
            updated!.IsActive.Should().BeFalse();
        }

        [Fact(DisplayName = "W6-S8: Deactivate last owner throws")]
        public async Task Deactivate_LastOwner_Throws()
        {
            var owner = await _sut.CreateUserAsync(_tenantId, "owner@vanan.vn", "pwd", "Owner", UserRole.Owner);

            Func<Task> act = () => _sut.DeactivateUserAsync(owner.Id, _tenantId);
            await act.Should().ThrowAsync<InvalidOperationException>();
        }

        [Fact(DisplayName = "W6-S9: ReactivateUser sets active")]
        public async Task ReactivateUser_SetsActive()
        {
            var user = await _sut.CreateUserAsync(_tenantId, "u@vanan.vn", "pwd", "U", UserRole.Staff);
            await _sut.DeactivateUserAsync(user.Id, _tenantId);

            await _sut.ReactivateUserAsync(user.Id, _tenantId);

            var updated = await _sut.GetUserByIdAsync(user.Id, _tenantId);
            updated!.IsActive.Should().BeTrue();
        }
    }
}
