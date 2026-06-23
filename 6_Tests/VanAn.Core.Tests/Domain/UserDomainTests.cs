using FluentAssertions;
using VanAn.Shared.Domain;
using DemoUser = VanAn.Shared.Domain.Aggregates.UserAggregate.DemoUser;
using UserRole = VanAn.Shared.Domain.Aggregates.UserAggregate.UserRole;
using UserCreatedEvent = VanAn.Shared.Domain.Aggregates.UserAggregate.UserCreatedEvent;
using UserDeactivatedEvent = VanAn.Shared.Domain.Aggregates.UserAggregate.UserDeactivatedEvent;
using UserPasswordChangedEvent = VanAn.Shared.Domain.Aggregates.UserAggregate.UserPasswordChangedEvent;
using UserRoleChangedEvent = VanAn.Shared.Domain.Aggregates.UserAggregate.UserRoleChangedEvent;
using Xunit;

namespace VanAn.Core.Tests.Domain
{
    /// <summary>
    /// Wave 6 — W6-T1: Domain tests for DemoUser aggregate lifecycle.
    /// </summary>
    public class UserDomainTests
    {
        private static TenantId TestTenantId() => new(Guid.NewGuid());

        [Fact(DisplayName = "W6-D1: Create user raises UserCreatedEvent and sets active")]
        public void Create_RaisesUserCreatedEvent_And_IsActive()
        {
            var tenantId = TestTenantId();
            var user = DemoUser.Create(tenantId, "alice@vanan.vn", "hash", "Alice", UserRole.Owner);

            user.Should().NotBeNull();
            user.Username.Should().Be("alice@vanan.vn");
            user.DisplayName.Should().Be("Alice");
            user.Role.Should().Be(UserRole.Owner);
            user.IsActive.Should().BeTrue();
            user.TenantId.Should().Be(tenantId);
            user.DomainEvents.Should().ContainSingle(e => e is UserCreatedEvent);
        }

        [Fact(DisplayName = "W6-D2: Deactivate sets IsActive false and raises event")]
        public void Deactivate_SetsInactive_And_RaisesEvent()
        {
            var user = DemoUser.Create(TestTenantId(), "u@vanan.vn", "hash", "U", UserRole.Staff);
            user.ClearDomainEvents();

            user.Deactivate();

            user.IsActive.Should().BeFalse();
            user.DomainEvents.Should().ContainSingle(e => e is UserDeactivatedEvent);
        }

        [Fact(DisplayName = "W6-D3: Deactivate already inactive user throws")]
        public void Deactivate_AlreadyInactive_Throws()
        {
            var user = DemoUser.Create(TestTenantId(), "u@vanan.vn", "hash", "U", UserRole.Staff);
            user.Deactivate();
            user.ClearDomainEvents();

            user.Invoking(u => u.Deactivate())
                .Should().Throw<InvalidOperationException>();
        }

        [Fact(DisplayName = "W6-D4: Reactivate sets IsActive true")]
        public void Reactivate_SetsActive()
        {
            var user = DemoUser.Create(TestTenantId(), "u@vanan.vn", "hash", "U", UserRole.Staff);
            user.Deactivate();
            user.ClearDomainEvents();

            user.Reactivate();

            user.IsActive.Should().BeTrue();
        }

        [Fact(DisplayName = "W6-D5: ChangePassword updates password hash")]
        public void ChangePassword_UpdatesHash_And_RaisesEvent()
        {
            var user = DemoUser.Create(TestTenantId(), "u@vanan.vn", "oldhash", "U", UserRole.Staff);
            user.ClearDomainEvents();

            user.ChangePassword("newhash");

            user.PasswordHash.Should().Be("newhash");
            user.DomainEvents.Should().ContainSingle(e => e is UserPasswordChangedEvent);
        }

        [Fact(DisplayName = "W6-D6: AssignRole changes role and raises event")]
        public void AssignRole_ChangesRole_And_RaisesEvent()
        {
            var user = DemoUser.Create(TestTenantId(), "u@vanan.vn", "hash", "U", UserRole.Staff);
            user.ClearDomainEvents();

            user.AssignRole(UserRole.Owner);

            user.Role.Should().Be(UserRole.Owner);
            user.DomainEvents.Should().ContainSingle(e => e is UserRoleChangedEvent);
        }

        [Fact(DisplayName = "W6-D7: UpdateProfile changes display name")]
        public void UpdateProfile_ChangesDisplayName()
        {
            var user = DemoUser.Create(TestTenantId(), "u@vanan.vn", "hash", "Old", UserRole.Staff);

            user.UpdateProfile("New");

            user.DisplayName.Should().Be("New");
        }
    }
}
