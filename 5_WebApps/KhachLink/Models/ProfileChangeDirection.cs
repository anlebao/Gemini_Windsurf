using VanAn.Shared.Domain.Aggregates.KhachLinkAggregate;

namespace VanAn.KhachLink.Models;

/// <summary>
/// Sprint 2: Direction of profile transition — drives What's New banner message +
/// cart preservation modal trigger + onboarding tour trigger.
/// Computed in KhachLinkLayout by comparing localStorage lastSeenProfile (old) vs instanceConfig.Profile (new).
/// </summary>
public enum ProfileChangeDirection
{
    /// <summary>No profile change detected (first visit or same profile).</summary>
    None = 0,
    DirectoryToFullCommerce,
    FullCommerceToDirectory,
    DirectoryToReseller,
    ResellerToFullCommerce,
    FullCommerceToReseller,
    ResellerToDirectory,
    /// <summary>Any other transition (e.g. involving Logistics/JobMarket — R3, not covered by specific messaging).</summary>
    Other
}

/// <summary>Helper to compute direction from old + new profile.</summary>
public static class ProfileChangeDirectionHelper
{
    public static ProfileChangeDirection Compute(KhachLinkProfile? oldProfile, KhachLinkProfile newProfile)
    {
        if (oldProfile == null || oldProfile == newProfile)
            return ProfileChangeDirection.None;

        return (oldProfile.Value, newProfile) switch
        {
            (KhachLinkProfile.Directory, KhachLinkProfile.FullCommerce) => ProfileChangeDirection.DirectoryToFullCommerce,
            (KhachLinkProfile.FullCommerce, KhachLinkProfile.Directory) => ProfileChangeDirection.FullCommerceToDirectory,
            (KhachLinkProfile.Directory, KhachLinkProfile.Reseller) => ProfileChangeDirection.DirectoryToReseller,
            (KhachLinkProfile.Reseller, KhachLinkProfile.FullCommerce) => ProfileChangeDirection.ResellerToFullCommerce,
            (KhachLinkProfile.FullCommerce, KhachLinkProfile.Reseller) => ProfileChangeDirection.FullCommerceToReseller,
            (KhachLinkProfile.Reseller, KhachLinkProfile.Directory) => ProfileChangeDirection.ResellerToDirectory,
            _ => ProfileChangeDirection.Other
        };
    }
}
