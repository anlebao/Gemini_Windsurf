using System.Text.RegularExpressions;

namespace VanAn.UI.Platform.Adapters;

/// <summary>
/// Realtime Platform P4 (F4): pure URL-derivation logic for the Gateway, extracted from
/// KhachLink's ChatPanel.DeriveGatewayUrl (C8 — the gateway host was derived by hand in the
/// component). Keeping it a pure static helper makes the mapping unit-testable.
/// </summary>
public static class RealtimeEndpointUrls
{
    /// <summary>
    /// Derive the Gateway base URL from the app's own base URL.
    /// Rule: subdomain number suffix maps 1:1 (diemthuong2 → api2, app3 → api3, diemthuong → api).
    /// For custom domains (not *.khachvip.online), use the same origin — nginx proxies
    /// /api/ + /hubs/ to the Gateway (vanan.multivps.conf.template).
    /// </summary>
    public static string DeriveGatewayBaseUrl(string baseUrl)
    {
        var uri = new Uri(baseUrl);
        var host = uri.Host;
        var match = Regex.Match(host, @"^([a-z]+)(\d*)\.khachvip\.online$");
        if (match.Success)
        {
            var suffix = match.Groups[2].Value; // "" for Oracle, "2"/"3"/"4" for GCP
            return $"https://api{suffix}.khachvip.online";
        }
        // Custom domain or localhost — same origin (nginx proxies /api/ + /hubs/ → Gateway).
        return baseUrl.TrimEnd('/');
    }
}
