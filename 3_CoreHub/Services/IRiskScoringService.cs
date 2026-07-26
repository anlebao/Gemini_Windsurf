using VanAn.Shared.Domain;

namespace VanAn.CoreHub.Services
{
    /// <summary>
    /// Input for risk scoring — 8 factors (v1.2 Community Commerce Sprint 0).
    /// All factors are boolean flags or simple values; scoring is deterministic.
    /// </summary>
    public record RiskScoreInput(
        bool SameFingerprint,           // salesman + customer cùng fingerprint (self-deal) — +50
        bool SameIp24h,                 // same IP used in last 24h by multiple accounts — +15
        bool CustomerAgeDaysLessThan7,  // customer registered < 7 days ago — +30
        bool DeviceFirstSeenLessThan24h,// device first seen < 24h ago — +20
        bool OrdersFromDeviceTodayGreaterThan3, // > 3 orders from same device today (bot) — +25
        bool ReferralBonusAmountGreaterThan50K, // bonus > 50,000 VND (high-value target) — +10
        bool AppInstallTimeLessThan30s, // app install completed in < 30 seconds (bot) — +40
        bool BlacklistedFingerprint     // fingerprint matches blacklist — +60
    );

    /// <summary>
    /// Result of risk scoring — score 0-100 + JSON factors detail.
    /// </summary>
    public record RiskScoreResult(int Score, string RiskFactors);

    /// <summary>
    /// RiskScoringService — deterministic 8-factor risk score computation (v1.2).
    /// Same input ALWAYS produces same output (no random, no time-dependent logic).
    /// Score thresholds:
    ///   0-59: Pending (cooling 24h via Sprint 4 CoolingPeriodJob)
    ///   60-79: Held (hold 48h, admin review)
    ///   80-100: Rejected (auto-reject)
    /// </summary>
    public interface IRiskScoringService
    {
        RiskScoreResult CalculateScore(RiskScoreInput input);
    }
}
