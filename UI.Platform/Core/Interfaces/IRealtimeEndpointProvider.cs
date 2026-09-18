namespace VanAn.UI.Platform.Core.Interfaces;

/// <summary>
/// Realtime Platform P4 (2026-09-17, F4): tells the realtime components where the Gateway lives.
///
/// The RCL is host-agnostic: KhachLink (WASM), ShopERP (Blazor Server) and Directory (SSR) all
/// reference it, and none of them can hard-code a Gateway host. The default implementation derives
/// the URL from <c>NavigationManager.BaseUri</c> (subdomain suffix maps 1:1 for *.khachvip.online,
/// same-origin for custom domains — nginx proxies /api/ + /hubs/ to the Gateway), so a host just
/// calls <c>AddRealtimePlatform()</c> and gets working endpoints with zero configuration.
/// </summary>
public interface IRealtimeEndpointProvider
{
    /// <summary>Gateway base URL (scheme + host, no trailing slash), e.g. https://api2.khachvip.online.</summary>
    string GetGatewayBaseUrl();

    /// <summary>Full URL of the generic chat hub (/hubs/messaging). Credentials are appended by the caller.</summary>
    string GetMessagingHubUrl();

    /// <summary>Full URL of the generic tracking hub (/hubs/tracking). Credentials are appended by the caller.</summary>
    string GetTrackingHubUrl();
}
