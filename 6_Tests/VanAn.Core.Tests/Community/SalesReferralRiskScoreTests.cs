using VanAn.Shared.Domain;
using Xunit;

namespace VanAn.Core.Tests.Community
{
    /// <summary>
    /// SalesReferral risk scoring tests (Community Commerce Sprint 0 v1.2).
    /// Cases 36-39: SetRiskScore thresholds (60→Held, 80→Rejected, 30→Pending), ApproveAfterHold.
    /// </summary>
    public class SalesReferralRiskScoreTests
    {
        private static SalesReferral CreateReferral()
        {
            var tenantId = new TenantId(Guid.NewGuid());
            return new SalesReferral(tenantId, Guid.NewGuid(), "ABC234", Guid.NewGuid(), "P001");
        }

        [Fact(DisplayName = "36: SalesReferral_SetRiskScore_60_SetsHeld")]
        public void SalesReferral_SetRiskScore_60_SetsHeld()
        {
            var referral = CreateReferral();
            var before = DateTime.UtcNow;

            referral.SetRiskScore(60, "{}");

            Assert.Equal(60, referral.RiskScore);
            Assert.Equal(CommissionStatus.Held, referral.CommissionStatus);
            Assert.NotNull(referral.HoldUntil);
            Assert.True(referral.HoldUntil >= before.AddHours(48));
        }

        [Fact(DisplayName = "37: SalesReferral_SetRiskScore_80_SetsRejected")]
        public void SalesReferral_SetRiskScore_80_SetsRejected()
        {
            var referral = CreateReferral();

            referral.SetRiskScore(80, "{}");

            Assert.Equal(80, referral.RiskScore);
            Assert.Equal(CommissionStatus.Rejected, referral.CommissionStatus);
        }

        [Fact(DisplayName = "38: SalesReferral_SetRiskScore_30_StaysPending")]
        public void SalesReferral_SetRiskScore_30_StaysPending()
        {
            var referral = CreateReferral();

            referral.SetRiskScore(30, "{}");

            Assert.Equal(30, referral.RiskScore);
            Assert.Equal(CommissionStatus.Pending, referral.CommissionStatus);
            Assert.Null(referral.HoldUntil);
        }

        [Fact(DisplayName = "39: SalesReferral_ApproveAfterHold_ClearsHold")]
        public void SalesReferral_ApproveAfterHold_ClearsHold()
        {
            var referral = CreateReferral();
            referral.SetRiskScore(60, "{}");
            Assert.Equal(CommissionStatus.Held, referral.CommissionStatus);

            referral.ApproveAfterHold();

            Assert.Equal(CommissionStatus.Pending, referral.CommissionStatus);
            Assert.Null(referral.HoldUntil);
        }
    }
}
