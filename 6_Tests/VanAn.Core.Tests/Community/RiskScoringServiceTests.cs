using VanAn.CoreHub.Services;
using Xunit;

namespace VanAn.Core.Tests.Community
{
    /// <summary>
    /// RiskScoringService tests (Community Commerce Sprint 0 v1.2).
    /// Cases 30-35: deterministic 8-factor scoring.
    /// </summary>
    public class RiskScoringServiceTests
    {
        private readonly IRiskScoringService _service = new RiskScoringService();

        [Fact(DisplayName = "30: RiskScore_AllFactorsZero_Returns0")]
        public void RiskScore_AllFactorsZero_Returns0()
        {
            var input = new RiskScoreInput(
                SameFingerprint: false,
                SameIp24h: false,
                CustomerAgeDaysLessThan7: false,
                DeviceFirstSeenLessThan24h: false,
                OrdersFromDeviceTodayGreaterThan3: false,
                ReferralBonusAmountGreaterThan50K: false,
                AppInstallTimeLessThan30s: false,
                BlacklistedFingerprint: false);

            var result = _service.CalculateScore(input);

            Assert.Equal(0, result.Score);
        }

        [Fact(DisplayName = "31: RiskScore_SameFingerprint_Adds50")]
        public void RiskScore_SameFingerprint_Adds50()
        {
            var input = new RiskScoreInput(
                SameFingerprint: true,
                SameIp24h: false,
                CustomerAgeDaysLessThan7: false,
                DeviceFirstSeenLessThan24h: false,
                OrdersFromDeviceTodayGreaterThan3: false,
                ReferralBonusAmountGreaterThan50K: false,
                AppInstallTimeLessThan30s: false,
                BlacklistedFingerprint: false);

            var result = _service.CalculateScore(input);

            Assert.Equal(50, result.Score);
        }

        [Fact(DisplayName = "32: RiskScore_SameFingerprintPlusNewCustomer_Returns80")]
        public void RiskScore_SameFingerprintPlusNewCustomer_Returns80()
        {
            var input = new RiskScoreInput(
                SameFingerprint: true,           // +50
                SameIp24h: false,
                CustomerAgeDaysLessThan7: true,  // +30
                DeviceFirstSeenLessThan24h: false,
                OrdersFromDeviceTodayGreaterThan3: false,
                ReferralBonusAmountGreaterThan50K: false,
                AppInstallTimeLessThan30s: false,
                BlacklistedFingerprint: false);

            var result = _service.CalculateScore(input);

            Assert.Equal(80, result.Score); // 50 + 30 = 80 → auto-reject
        }

        [Fact(DisplayName = "33: RiskScore_BotInstall_Adds40")]
        public void RiskScore_BotInstall_Adds40()
        {
            var input = new RiskScoreInput(
                SameFingerprint: false,
                SameIp24h: false,
                CustomerAgeDaysLessThan7: false,
                DeviceFirstSeenLessThan24h: false,
                OrdersFromDeviceTodayGreaterThan3: false,
                ReferralBonusAmountGreaterThan50K: false,
                AppInstallTimeLessThan30s: true, // +40
                BlacklistedFingerprint: false);

            var result = _service.CalculateScore(input);

            Assert.Equal(40, result.Score);
        }

        [Fact(DisplayName = "34: RiskScore_BlacklistedFingerprint_Adds60")]
        public void RiskScore_BlacklistedFingerprint_Adds60()
        {
            var input = new RiskScoreInput(
                SameFingerprint: false,
                SameIp24h: false,
                CustomerAgeDaysLessThan7: false,
                DeviceFirstSeenLessThan24h: false,
                OrdersFromDeviceTodayGreaterThan3: false,
                ReferralBonusAmountGreaterThan50K: false,
                AppInstallTimeLessThan30s: false,
                BlacklistedFingerprint: true); // +60

            var result = _service.CalculateScore(input);

            Assert.Equal(60, result.Score);
        }

        [Fact(DisplayName = "35: RiskScore_Deterministic_SameInputSameOutput")]
        public void RiskScore_Deterministic_SameInputSameOutput()
        {
            var input = new RiskScoreInput(
                SameFingerprint: true,
                SameIp24h: true,
                CustomerAgeDaysLessThan7: true,
                DeviceFirstSeenLessThan24h: false,
                OrdersFromDeviceTodayGreaterThan3: true,
                ReferralBonusAmountGreaterThan50K: false,
                AppInstallTimeLessThan30s: true,
                BlacklistedFingerprint: false);

            var result1 = _service.CalculateScore(input);
            var result2 = _service.CalculateScore(input);

            Assert.Equal(result1.Score, result2.Score);
            Assert.Equal(result1.RiskFactors, result2.RiskFactors);
            // Cap at 100: 50+15+30+25+40 = 160 → 100
            Assert.Equal(100, result1.Score);
        }
    }
}
