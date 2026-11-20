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

    /// <summary>Evaluation timestamp (injectable for tests). Defaults to UtcNow.</summary>
    public DateTime UtcNow { get; init; } = DateTime.UtcNow;
}
