namespace VanAn.Atrs.Domain;

/// <summary>Kết quả policy (FR-010).</summary>
public enum RiskDecisionKind
{
    ALLOW = 0,
    ALLOW_WITH_STEP_UP = 1,
    REQUIRE_MANUAL_REVIEW = 2,
    TEMPORARY_RESTRICT = 3,
    DENY = 4
}

/// <summary>
/// RiskDecision — mỗi quyết định PHẢI có reason code + policy version (FR-010).
/// </summary>
public sealed record RiskDecision(RiskDecisionKind Decision, IReadOnlyList<string> ReasonCodes, string PolicyVersion)
{
    public static RiskDecision Of(RiskDecisionKind decision, string policyVersion, params string[] reasonCodes)
        => new(decision, reasonCodes, policyVersion);
}

/// <summary>
/// RiskContext — input tín hiệu cho policy engine (FR-010 §10). Tín hiệu là
/// deterministic flags do caller thu thập (device registry, events, ledger refs);
/// KHÔNG dùng fingerprint xâm lấn / điểm số mơ hồ.
/// </summary>
public sealed record RiskContext(
    Guid TrustSubjectId,
    string Action,                              // "recovery.submit" | "device.register" | "dispute.adjust" | ...
    bool IsNewDevice = false,
    bool DeviceLostOrRevoked = false,
    bool RecentAuthFailures = false,
    bool RecentRecovery = false,
    bool CredentialRecentlyChanged = false,
    bool HighAccountValue = false);

/// <summary>
/// Policy engine v1 — luật xác định (deterministic), KHÔNG AI/ML (FR-010).
/// Thứ tự ưu tiên: R4 (takeover) > R3 (high-risk) > R2 (recovery-assisted) > R1 (new device) > R0.
/// </summary>
public sealed class RiskPolicyEngine : IRiskPolicyEngine
{
    public const string PolicyVersion = "atrs-risk-v1";

    public RiskDecision Evaluate(RiskContext ctx)
    {
        ArgumentNullException.ThrowIfNull(ctx);

        // R4 — nghi chiếm quyền rõ ràng: thiết bị mất/revoke + auth thất bại + credential vừa đổi + giá trị cao.
        if (ctx.DeviceLostOrRevoked && ctx.RecentAuthFailures && ctx.CredentialRecentlyChanged && ctx.HighAccountValue)
            return RiskDecision.Of(RiskDecisionKind.TEMPORARY_RESTRICT, PolicyVersion,
                "suspected-takeover", "lost-device", "auth-failures", "high-value");

        // R3 — high risk: recovery gần đây + (giá trị cao | auth thất bại) → manual review.
        if (ctx.RecentRecovery && (ctx.HighAccountValue || ctx.RecentAuthFailures))
            return RiskDecision.Of(RiskDecisionKind.REQUIRE_MANUAL_REVIEW, PolicyVersion,
                "recent-recovery", ctx.HighAccountValue ? "high-value" : "auth-failures");

        // R2 — recovery-assisted: recovery + giá trị cao → step-up (recovery code/contact chính là step-up).
        if (ctx.Action.StartsWith("recovery", StringComparison.Ordinal) && ctx.HighAccountValue)
            return RiskDecision.Of(RiskDecisionKind.ALLOW_WITH_STEP_UP, PolicyVersion, "high-value-recovery");

        // R1 — thiết bị mới → step-up.
        if (ctx.IsNewDevice)
            return RiskDecision.Of(RiskDecisionKind.ALLOW_WITH_STEP_UP, PolicyVersion, "new-device");

        // R0 — routine.
        return RiskDecision.Of(RiskDecisionKind.ALLOW, PolicyVersion, "routine");
    }
}

/// <summary>
/// RiskPolicyEngine interface — domain pure, deterministic, có policy version.
/// </summary>
public interface IRiskPolicyEngine
{
    RiskDecision Evaluate(RiskContext ctx);
}