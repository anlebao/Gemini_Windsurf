using VanAn.Shared.Domain;
using Xunit;

namespace VanAn.Core.Tests.Community
{
    /// <summary>
    /// AppInstallAttribution risk scoring tests (Community Commerce Sprint 0 v1.2).
    /// Cases 40-42: SetRiskScore thresholds (60→Held, 80→Rejected, 30→Pending).
    /// </summary>
    public class AppInstallAttributionRiskScoreTests
    {
        private static AppInstallAttribution CreateAttribution()
        {
            var tenantId = new TenantId(Guid.NewGuid());
            return new AppInstallAttribution(tenantId, Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 20_000m);
        }

        [Fact(DisplayName = "40: AppInstallAttribution_SetRiskScore_60_SetsHeld")]
        public void AppInstallAttribution_SetRiskScore_60_SetsHeld()
        {
            var attr = CreateAttribution();
            var before = DateTime.UtcNow;

            attr.SetRiskScore(60, "{}");

            Assert.Equal(60, attr.RiskScore);
            Assert.Equal(AttributionStatus.Held, attr.AttributionStatus);
            Assert.NotNull(attr.HoldUntil);
            Assert.True(attr.HoldUntil >= before.AddHours(48));
        }

        [Fact(DisplayName = "41: AppInstallAttribution_SetRiskScore_80_SetsRejected")]
        public void AppInstallAttribution_SetRiskScore_80_SetsRejected()
        {
            var attr = CreateAttribution();

            attr.SetRiskScore(80, "{}");

            Assert.Equal(80, attr.RiskScore);
            Assert.Equal(AttributionStatus.Rejected, attr.AttributionStatus);
        }

        [Fact(DisplayName = "42: AppInstallAttribution_SetRiskScore_30_StaysPending")]
        public void AppInstallAttribution_SetRiskScore_30_StaysPending()
        {
            var attr = CreateAttribution();

            attr.SetRiskScore(30, "{}");

            Assert.Equal(30, attr.RiskScore);
            Assert.Equal(AttributionStatus.Pending, attr.AttributionStatus);
            Assert.Null(attr.HoldUntil);
        }
    }
}
