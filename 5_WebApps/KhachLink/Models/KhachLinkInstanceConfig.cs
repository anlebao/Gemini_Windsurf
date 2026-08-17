using VanAn.Shared.Domain.Aggregates.KhachLinkAggregate;

namespace VanAn.KhachLink.Models;

/// <summary>
/// Client-side KhachLink instance config — fetched from Gateway by-domain endpoint.
/// Drives NavMenu + header icon visibility via KhachLinkNavFlagsDto.
/// All flags default to true (FullCommerce fallback when feature flag OFF or fetch fails).
/// </summary>
public sealed class KhachLinkInstanceConfig
{
    public KhachLinkProfile Profile { get; set; } = KhachLinkProfile.FullCommerce;
    public Guid? OwnerTenantId { get; set; }
    public KhachLinkNavFlagsDto NavFlags { get; set; } = new();
    /// <summary>#134: If false, the instance is disabled — KhachLinkLayout shows a "disabled" page.</summary>
    public bool IsActive { get; set; } = true;
}

/// <summary>
/// 15 boolean UI toggles — mirrors server-side KhachLinkNavFlags.
/// Default all true (FullCommerce behavior) so feature-flag-OFF path is a no-op.
/// </summary>
public sealed class KhachLinkNavFlagsDto
{
    public bool ShowHome { get; set; } = true;
    public bool ShowCart { get; set; } = true;
    public bool ShowOrders { get; set; } = true;
    public bool ShowLoyaltyHistory { get; set; } = true;
    public bool ShowMissions { get; set; } = true;
    public bool ShowRewards { get; set; } = true;
    public bool ShowAllianceWallet { get; set; } = true;
    public bool ShowStores { get; set; } = true;
    public bool ShowCampaigns { get; set; } = true;
    public bool ShowScan { get; set; } = true;
    public bool ShowQrClaim { get; set; } = true;
    public bool ShowCommunity { get; set; } = true;
    public bool ShowJobs { get; set; } = false; // R3 — hidden in R1
    public bool ShowProfile { get; set; } = true;
    public bool ShowStaffDashboard { get; set; } = true;
}
