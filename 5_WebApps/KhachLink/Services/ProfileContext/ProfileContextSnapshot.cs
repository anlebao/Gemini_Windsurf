namespace VanAn.KhachLink.Services.ProfileContext;

/// <summary>
/// Flat input snapshot for <see cref="ProfileContextCardBuilder"/>.
/// Built client-side in Profile.razor from existing API responses — no HTTP/EF inside.
/// </summary>
public sealed class ProfileContextSnapshot
{
    /// <summary>Customer identity level: "Verified" | "Full" | "Social" | "Guest" | null.</summary>
    public string? IdentityLevel { get; init; }

    /// <summary>Loyalty tier display name: "Bronze" | "Silver" | "Gold" | "Platinum".</summary>
    public string Tier { get; init; } = "Bronze";

    /// <summary>Current loyalty point balance.</summary>
    public int PointBalance { get; init; }

    /// <summary>Customer birthday (null = not set).</summary>
    public DateTime? Birthday { get; init; }

    /// <summary>Push notification enabled state. Null = status unknown (API failed).</summary>
    public bool? PushEnabled { get; init; }

    /// <summary>Count of orders in active statuses (pending/confirmed/preparing/ready/delivering).</summary>
    public int ActiveOrderCount { get; init; }

    /// <summary>Count of missions not yet completed.</summary>
    public int IncompleteMissionCount { get; init; }

    /// <summary>Highest PointsReward among incomplete missions (0 when none).</summary>
    public int MaxIncompleteMissionPoints { get; init; }

    // === v2 signals (from GET /api/customer-context/me — optional, legacy path leaves defaults) ===

    /// <summary>Stable card ids the customer dismissed (server-side, TTL 7 days).</summary>
    public IReadOnlySet<string>? DismissedCardIds { get; init; }

    /// <summary>Valid vouchers expiring within the next 7 days.</summary>
    public int ExpiringVoucherCount { get; init; }

    /// <summary>Days until the soonest expiring voucher (null when none).</summary>
    public int? VoucherDaysLeft { get; init; }

    /// <summary>Total orders ever placed (0 = never ordered).</summary>
    public int TotalOrderCount { get; init; }

    /// <summary>Days since the most recent order (null when no orders).</summary>
    public int? DaysSinceLastOrder { get; init; }

    /// <summary>Evaluation timestamp (injectable for tests). Defaults to UtcNow.</summary>
    public DateTime UtcNow { get; init; } = DateTime.UtcNow;
}
