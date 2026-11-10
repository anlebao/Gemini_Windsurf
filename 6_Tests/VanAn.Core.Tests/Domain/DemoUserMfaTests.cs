using FluentAssertions;
using VanAn.Shared.Domain;
using DemoUser = VanAn.Shared.Domain.Aggregates.UserAggregate.DemoUser;
using UserRole = VanAn.Shared.Domain.Aggregates.UserAggregate.UserRole;
using UserTrustLevel = VanAn.Shared.Domain.Aggregates.UserAggregate.UserTrustLevel;
using Xunit;

namespace VanAn.Core.Tests.Domain
{
    /// <summary>
    /// MFA-1 (2026-11-10, user duyệt): DemoUser MFA fields — MfaEnabled + MfaSecret (encrypted at rest).
    /// </summary>
    public class DemoUserMfaTests
    {
        private static TenantId TestTenantId() => new(Guid.NewGuid());

        private static DemoUser CreateUser() =>
            DemoUser.Create(TestTenantId(), "admin@vanan.vn", "hash", "Admin", UserRole.Owner);

        [Fact]
        public void Create_Defaults_MfaDisabled()
        {
            var user = CreateUser();
            user.MfaEnabled.Should().BeFalse();
            user.MfaSecret.Should().BeNull();
        }

        [Fact]
        public void EnableMfa_SetsSecretAndFlag()
        {
            var user = CreateUser();
            user.EnableMfa("SECRETBASE32");
            user.MfaEnabled.Should().BeTrue();
            user.MfaSecret.Should().Be("SECRETBASE32");
        }

        [Fact]
        public void EnableMfa_AlreadyEnabled_Throws()
        {
            var user = CreateUser();
            user.EnableMfa("SECRET-A");
            user.Invoking(u => u.EnableMfa("SECRET-B"))
                .Should().Throw<InvalidOperationException>();
            user.MfaSecret.Should().Be("SECRET-A");
        }

        [Fact]
        public void EnableMfa_EmptySecret_Throws()
        {
            var user = CreateUser();
            user.Invoking(u => u.EnableMfa(""))
                .Should().Throw<ArgumentException>();
            user.MfaEnabled.Should().BeFalse();
        }

        [Fact]
        public void DisableMfa_ClearsSecretAndFlag_IsIdempotent()
        {
            var user = CreateUser();
            user.EnableMfa("SECRETBASE32");
            user.DisableMfa();
            user.MfaEnabled.Should().BeFalse();
            user.MfaSecret.Should().BeNull();

            user.DisableMfa(); // idempotent — không lỗi
        }

        [Fact]
        public void DisableMfa_DoesNotAffectTrustFields()
        {
            var user = CreateUser();
            user.SetContactInfo("0901234567", null);
            user.MarkPhoneVerified("email");
            user.MarkBusinessVerified();
            user.EnableMfa("SECRETBASE32");

            user.DisableMfa();

            user.TrustLevel.Should().Be(UserTrustLevel.BusinessVerified);
            user.PhoneNumber.Should().Be("0901234567");
            user.PhoneVerifiedAt.Should().NotBeNull();
        }
    }
}