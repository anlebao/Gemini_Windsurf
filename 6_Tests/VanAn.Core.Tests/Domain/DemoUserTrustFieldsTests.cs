using FluentAssertions;
using VanAn.Shared.Domain;
using DemoUser = VanAn.Shared.Domain.Aggregates.UserAggregate.DemoUser;
using UserRole = VanAn.Shared.Domain.Aggregates.UserAggregate.UserRole;
using UserTrustLevel = VanAn.Shared.Domain.Aggregates.UserAggregate.UserTrustLevel;
using Xunit;

namespace VanAn.Core.Tests.Domain
{
    /// <summary>
    /// ID-P1 (2026-11-10, user duyệt): DemoUser trust/contact fields
    /// — PhoneNumber (encrypted at rest) + PhoneVerifiedAt + VerifiedChannel + TrustLevel.
    /// </summary>
    public class DemoUserTrustFieldsTests
    {
        private static TenantId TestTenantId() => new(Guid.NewGuid());

        private static DemoUser CreateOwner() =>
            DemoUser.Create(TestTenantId(), "owner@vanan.vn", "hash", "Owner", UserRole.Owner);

        [Fact]
        public void Create_Defaults_TrustFields()
        {
            var user = CreateOwner();
            user.TrustLevel.Should().Be(UserTrustLevel.None);
            user.PhoneNumber.Should().BeNull();
            user.PhoneVerifiedAt.Should().BeNull();
            user.VerifiedChannel.Should().BeNull();
        }

        [Fact]
        public void SetContactInfo_NormalizesPhone_KeepsEmail()
        {
            var user = CreateOwner();
            user.SetContactInfo("+84 901 234 567", null);
            user.PhoneNumber.Should().Be("0901234567");
            user.Email.Should().BeNull();
        }

        [Fact]
        public void SetContactInfo_PhoneChange_ResetsPhoneVerifiedAndChannel()
        {
            var user = CreateOwner();
            user.SetContactInfo("0901234567", null);
            user.MarkPhoneVerified("email");

            user.SetContactInfo("0911111111", null);

            user.PhoneNumber.Should().Be("0911111111");
            user.PhoneVerifiedAt.Should().BeNull();
            user.VerifiedChannel.Should().BeNull();
        }

        [Fact]
        public void SetContactInfo_EmailOnly_KeepsPhoneAndSetsEmail()
        {
            var user = CreateOwner();
            user.SetContactInfo("0901234567", null);

            user.SetContactInfo(null, "a@b.com");

            user.PhoneNumber.Should().Be("0901234567");
            user.Email.Should().Be("a@b.com");
        }

        [Fact]
        public void MarkPhoneVerified_NoPhone_Throws()
        {
            var user = CreateOwner();
            user.Invoking(u => u.MarkPhoneVerified())
                .Should().Throw<InvalidOperationException>();
        }

        [Fact]
        public void MarkPhoneVerified_SetsTimestampLevelAndChannel()
        {
            var user = CreateOwner();
            user.SetContactInfo("0901234567", null);

            user.MarkPhoneVerified("email");

            user.PhoneVerifiedAt.Should().NotBeNull();
            user.VerifiedChannel.Should().Be("email");
            user.TrustLevel.Should().Be(UserTrustLevel.ContactVerified);
        }

        [Fact]
        public void MarkPhoneVerified_NullChannel_KeepsExistingChannel()
        {
            var user = CreateOwner();
            user.SetContactInfo("0901234567", null);
            user.MarkPhoneVerified("zns");

            user.MarkPhoneVerified(null);

            user.VerifiedChannel.Should().Be("zns");
        }

        [Fact]
        public void MarkBusinessVerified_SetsLevel_AndIsIdempotent()
        {
            var user = CreateOwner();
            user.MarkBusinessVerified();
            user.TrustLevel.Should().Be(UserTrustLevel.BusinessVerified);

            user.MarkBusinessVerified();
            user.TrustLevel.Should().Be(UserTrustLevel.BusinessVerified);
        }

        [Fact]
        public void MarkPhoneVerified_WhenBusinessVerified_KeepsBusinessVerified()
        {
            var user = CreateOwner();
            user.MarkBusinessVerified();
            user.SetContactInfo("0901234567", null);

            user.MarkPhoneVerified("email");

            user.TrustLevel.Should().Be(UserTrustLevel.BusinessVerified);
        }

        [Fact]
        public void RevokeBusinessVerification_DowngradesToContactVerified()
        {
            var user = CreateOwner();
            user.MarkBusinessVerified();

            user.RevokeBusinessVerification();

            user.TrustLevel.Should().Be(UserTrustLevel.ContactVerified);
        }

        [Fact]
        public void RevokeBusinessVerification_WhenContactVerifiedOrNone_NoOp()
        {
            var contactOnly = CreateOwner();
            contactOnly.SetContactInfo("0901234567", null);
            contactOnly.MarkPhoneVerified("email");
            contactOnly.RevokeBusinessVerification();
            contactOnly.TrustLevel.Should().Be(UserTrustLevel.ContactVerified);

            var none = CreateOwner();
            none.RevokeBusinessVerification();
            none.TrustLevel.Should().Be(UserTrustLevel.None);
        }

        [Fact]
        public void RevokeThenMarkBusinessVerified_CanPromoteAgain()
        {
            var user = CreateOwner();
            user.MarkBusinessVerified();
            user.RevokeBusinessVerification();
            user.MarkBusinessVerified();
            user.TrustLevel.Should().Be(UserTrustLevel.BusinessVerified);
        }

        [Fact]
        public void ApplyTrustLevel_LowerThanCurrent_NoOp()
        {
            var user = CreateOwner();
            user.MarkBusinessVerified();

            user.ApplyTrustLevel(UserTrustLevel.ContactVerified);

            user.TrustLevel.Should().Be(UserTrustLevel.BusinessVerified);
        }

        [Fact]
        public void ApplyTrustLevel_Higher_Sets()
        {
            var user = CreateOwner();
            user.ApplyTrustLevel(UserTrustLevel.BusinessVerified);
            user.TrustLevel.Should().Be(UserTrustLevel.BusinessVerified);
        }

        [Fact]
        public void Constructor_BackwardCompat_Defaults()
        {
            var user = new DemoUser(TestTenantId(), "staff", "hash", "Staff", UserRole.Staff);
            user.TrustLevel.Should().Be(UserTrustLevel.None);
            user.PhoneNumber.Should().BeNull();
            user.MfaEnabled.Should().BeFalse();
        }
    }
}