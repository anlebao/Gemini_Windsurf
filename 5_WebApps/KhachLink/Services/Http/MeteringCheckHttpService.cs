using System.Net.Http.Json;

namespace VanAn.KhachLink.Services.Http;

/// <summary>
/// Storefront deny gate (2026-11-18): calls Gateway's anonymous
/// GET /api/public/metering/check?tenantId=&amp;serviceCode= before rendering Store.razor.
/// Response is intentionally minimal ({ allowed }) — no wallet internals leak.
///
/// Guarantees (G9 — same as server-side clients): NEVER throws into page code.
/// Transport error / timeout / non-2xx / missing "allowed" → treated as allowed
/// (fail-open): a metering outage must never take a storefront down.
/// </summary>
public class MeteringCheckHttpService(IHttpClientFactory httpClientFactory, ILogger<MeteringCheckHttpService> logger)
{
    private static readonly TimeSpan CheckTimeout = TimeSpan.FromSeconds(3);

    private readonly HttpClient _httpClient = httpClientFactory.CreateClient("gateway");
    private readonly ILogger<MeteringCheckHttpService> _logger = logger;

    /// <summary>
    /// Returns false only when the gateway explicitly reports allowed=false
    /// (tenant wallet empty + rate card Enforcement=Deny for store.visit).
    /// </summary>
    public async Task<bool> IsStorefrontAllowedAsync(Guid tenantId, CancellationToken ct = default)
    {
        try
        {
            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeoutCts.CancelAfter(CheckTimeout);

            var url = $"/api/public/metering/check?tenantId={tenantId}&serviceCode=store.visit";
            var dto = await _httpClient.GetFromJsonAsync<CheckResponse>(url, timeoutCts.Token);
            return dto?.Allowed != false;
        }
        catch (Exception ex) when (!ct.IsCancellationRequested)
        {
            _logger.LogWarning(ex, "Metering storefront check failed for tenant {TenantId} — fail-open (allow)", tenantId);
            return true;
        }
    }

    private sealed class CheckResponse
    {
        public bool Allowed { get; set; } = true;
    }
}
