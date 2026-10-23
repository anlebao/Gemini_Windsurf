namespace VanAn.Metering.Domain;

/// <summary>
/// P7 D4/D5 — threshold alert fired once per (TenantId, Level, CycleKey).
/// Levels reflect the % of GrantedTotal REMAINING (user-chốt): balance ≤50% →
/// Warn50 (vàng), ≤10% → Warn90 (đỏ), ==0 → Depleted. A new Grant bumps
/// Wallet.CycleKey, allowing the same levels to re-fire in the new cycle.
/// Delivery GĐ-1 = poll (alertLevel in wallet/check responses); NATS defer P9/P10.
/// </summary>
public sealed class WalletAlert
{
    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public WalletAlertLevel Level { get; private set; }
    public string CycleKey { get; private set; } = string.Empty;
    public DateTime FiredAtUtc { get; private set; }

    /// <summary>GĐ-1 = "poll" (response field only); NATS/webhook channels land later.</summary>
    public string NotifiedChannels { get; private set; } = "poll";

    private WalletAlert() { } // EF Core

    public static WalletAlert Create(Guid tenantId, WalletAlertLevel level, string cycleKey, DateTime nowUtc)
        => new()
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Level = level,
            CycleKey = cycleKey,
            FiredAtUtc = nowUtc
        };
}

public enum WalletAlertLevel
{
    /// <summary>Còn ≤50% GrantedTotal — cảnh báo vàng.</summary>
    Warn50 = 0,

    /// <summary>Còn ≤10% GrantedTotal — cảnh báo đỏ.</summary>
    Warn90 = 1,

    /// <summary>Số dư = 0 — hết hạn mức.</summary>
    Depleted = 2
}
