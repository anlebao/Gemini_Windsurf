using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using VanAn.CoreHub.Services;

namespace VanAn.ShopERP.Services;

/// <summary>
/// VALCN v2.0 Phase 7 — ShopERP HTTP client for Network Dashboard.
/// Calls Gateway internal API (/api/internal/network-dashboard) with X-Internal-Api-Key header.
/// Pattern follows LoyaltyBudgetServiceHttpProxy (Phase 3).
///
/// Graceful degradation: Gateway unreachable / 5xx → returns zeroed metrics (dashboard shows zeros, no crash).
/// </summary>
public sealed class NetworkDashboardHttpService(
    IHttpClientFactory httpClientFactory,
    ILogger<NetworkDashboardHttpService> logger)
{
    private readonly IHttpClientFactory _httpClientFactory = httpClientFactory;
    private readonly ILogger<NetworkDashboardHttpService> _logger = logger;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public async Task<NetworkDashboardMetrics?> GetMetricsAsync(DateTime? from = null, DateTime? to = null, CancellationToken ct = default)
    {
        try
        {
            var client = _httpClientFactory.CreateClient("GatewayInternal");
            var startDate = from ?? DateTime.UtcNow.AddDays(-30);
            var endDate = to ?? DateTime.UtcNow;
            var url = $"api/internal/network-dashboard?from={startDate:yyyy-MM-dd}&to={endDate:yyyy-MM-dd}";
            var resp = await client.GetAsync(url, ct);
            if (!resp.IsSuccessStatusCode)
            {
                _logger.LogWarning("NetworkDashboard: Gateway returned {Status}", resp.StatusCode);
                return null;
            }
            return await resp.Content.ReadFromJsonAsync<NetworkDashboardMetrics>(JsonOptions, ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "NetworkDashboard: failed to call Gateway");
            return null;
        }
    }
}
