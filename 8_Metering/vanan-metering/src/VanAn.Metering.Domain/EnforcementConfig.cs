namespace VanAn.Metering.Domain;

/// <summary>
/// Issue #197 (2026-10-29) — master switch "phí nền tảng" toàn hệ thống.
/// Singleton (1 row, pattern TopupConfig): OFF → check luôn deny=false + ingest
/// KHÔNG deduct (usage fact vẫn ghi, CreditsApplied=null) + không fire alert.
/// Row chưa tồn tại = ENABLED (backward compat — không đổi hành vi GĐ-1).
/// </summary>
public sealed class EnforcementConfig
{
    public Guid Id { get; private set; }
    public bool Enabled { get; private set; } = true;
    public string UpdatedBy { get; private set; } = string.Empty;
    public DateTime UpdatedAtUtc { get; private set; }

    private EnforcementConfig() { } // EF Core

    public static EnforcementConfig Create(bool enabled, string actor, DateTime nowUtc)
        => new()
        {
            Id = Guid.NewGuid(),
            Enabled = enabled,
            UpdatedBy = actor,
            UpdatedAtUtc = nowUtc
        };

    public void Set(bool enabled, string actor, DateTime nowUtc)
    {
        Enabled = enabled;
        UpdatedBy = actor;
        UpdatedAtUtc = nowUtc;
    }
}