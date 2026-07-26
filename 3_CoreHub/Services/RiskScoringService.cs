using System.Text.Json;
using VanAn.Shared.Domain;

namespace VanAn.CoreHub.Services
{
    /// <summary>
    /// RiskScoringService — deterministic 8-factor risk score computation (v1.2).
    /// Same input ALWAYS produces same output. Score capped at 100.
    /// </summary>
    public class RiskScoringService : IRiskScoringService
    {
        // Factor weights — deterministic, no external dependencies
        private const int WeightSameFingerprint = 50;              // self-deal (salesman + customer same fingerprint)
        private const int WeightSameIp24h = 15;                    // multiple accounts from same IP in 24h
        private const int WeightCustomerAgeDaysLessThan7 = 30;    // new customer (< 7 days)
        private const int WeightDeviceFirstSeenLessThan24h = 20;  // new device (< 24h)
        private const int WeightOrdersFromDeviceTodayGreaterThan3 = 25; // bot (> 3 orders/day from 1 device)
        private const int WeightReferralBonusAmountGreaterThan50K = 10; // high-value target
        private const int WeightAppInstallTimeLessThan30s = 40;    // bot install (< 30s)
        private const int WeightBlacklistedFingerprint = 60;      // known bad fingerprint

        private const int MaxScore = 100;

        public RiskScoreResult CalculateScore(RiskScoreInput input)
        {
            var factors = new List<string>();
            int score = 0;

            if (input.SameFingerprint)
            {
                score += WeightSameFingerprint;
                factors.Add($"SameFingerprint:+{WeightSameFingerprint}");
            }

            if (input.SameIp24h)
            {
                score += WeightSameIp24h;
                factors.Add($"SameIp24h:+{WeightSameIp24h}");
            }

            if (input.CustomerAgeDaysLessThan7)
            {
                score += WeightCustomerAgeDaysLessThan7;
                factors.Add($"CustomerAgeDaysLessThan7:+{WeightCustomerAgeDaysLessThan7}");
            }

            if (input.DeviceFirstSeenLessThan24h)
            {
                score += WeightDeviceFirstSeenLessThan24h;
                factors.Add($"DeviceFirstSeenLessThan24h:+{WeightDeviceFirstSeenLessThan24h}");
            }

            if (input.OrdersFromDeviceTodayGreaterThan3)
            {
                score += WeightOrdersFromDeviceTodayGreaterThan3;
                factors.Add($"OrdersFromDeviceTodayGreaterThan3:+{WeightOrdersFromDeviceTodayGreaterThan3}");
            }

            if (input.ReferralBonusAmountGreaterThan50K)
            {
                score += WeightReferralBonusAmountGreaterThan50K;
                factors.Add($"ReferralBonusAmountGreaterThan50K:+{WeightReferralBonusAmountGreaterThan50K}");
            }

            if (input.AppInstallTimeLessThan30s)
            {
                score += WeightAppInstallTimeLessThan30s;
                factors.Add($"AppInstallTimeLessThan30s:+{WeightAppInstallTimeLessThan30s}");
            }

            if (input.BlacklistedFingerprint)
            {
                score += WeightBlacklistedFingerprint;
                factors.Add($"BlacklistedFingerprint:+{WeightBlacklistedFingerprint}");
            }

            // Cap at 100
            if (score > MaxScore)
                score = MaxScore;

            string riskFactorsJson = JsonSerializer.Serialize(new { factors, totalScore = score });
            return new RiskScoreResult(score, riskFactorsJson);
        }
    }
}
