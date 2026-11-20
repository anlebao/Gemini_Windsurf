using System.Net.Http.Json;

namespace VanAn.KhachLink.Services.Http;

/// <summary>
/// Task P13 — anonymous storefront top-up via Gateway's public metering proxy
/// (/api/public/metering/topup-*). WASM cannot hold metering caller secrets,
/// so Gateway injects them server-side; this client only speaks anonymous HTTP.
///
/// Guarantees (G9): NEVER throws into page code — transport errors return null.
/// </summary>
public class PublicTopupHttpService(IHttpClientFactory httpClientFactory, ILogger<PublicTopupHttpService> logger)
{
    private static readonly TimeSpan TopupTimeout = TimeSpan.FromSeconds(10);

    private readonly HttpClient _httpClient = httpClientFactory.CreateClient("gateway");
    private readonly ILogger<PublicTopupHttpService> _logger = logger;

    public sealed record PackageOption(string Code, string Name, decimal PriceVnd, decimal Credits, int ValidityDays);
    public sealed record TopupOptions(bool Allowed, List<PackageOption> Packages);
    public sealed record CreatedTopup(string ReferenceCode, decimal? AmountVnd, string Status,
        DateTime ExpiresAtUtc, string? QrPayload, string? BillId);

    /// <summary>Returns null on transport failure; Allowed=false for healthy tenants.</summary>
    public async Task<TopupOptions?> GetOptionsAsync(Guid tenantId, CancellationToken ct = default)
    {
        try
        {
            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeoutCts.CancelAfter(TopupTimeout);
            var dto = await _httpClient.GetFromJsonAsync<OptionsWire>(
                $"/api/public/metering/topup-options?tenantId={tenantId}", timeoutCts.Token);
            if (dto == null) return null;
            return new TopupOptions(dto.Allowed,
                dto.Packages?.Select(p => new PackageOption(p.Code, p.Name, p.PriceVnd, p.Credits, p.ValidityDays)).ToList()
                ?? new List<PackageOption>());
        }
        catch (Exception ex) when (!ct.IsCancellationRequested)
        {
            _logger.LogWarning(ex, "Public topup options failed for tenant {TenantId}", tenantId);
            return null;
        }
    }

    /// <summary>Creates a pending top-up + dynamic QR. Null on failure.</summary>
    public async Task<CreatedTopup?> CreateAsync(Guid tenantId, string? packageCode, decimal? amountVnd,
        string idempotencyKey, CancellationToken ct = default)
    {
        try
        {
            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeoutCts.CancelAfter(TopupTimeout);
            using var resp = await _httpClient.PostAsJsonAsync("/api/public/metering/topups",
                new { tenantId, packageCode, amountVnd, idempotencyKey }, timeoutCts.Token);
            if (!resp.IsSuccessStatusCode)
            {
                _logger.LogWarning("Public topup create failed: HTTP {Status} for tenant {TenantId}",
                    (int)resp.StatusCode, tenantId);
                return null;
            }
            var dto = await resp.Content.ReadFromJsonAsync<TopupWire>(timeoutCts.Token);
            return dto == null ? null : new CreatedTopup(
                dto.ReferenceCode ?? string.Empty, dto.AmountVnd, dto.Status ?? "pending",
                dto.ExpiresAtUtc, dto.QrPayload, dto.BillId);
        }
        catch (Exception ex) when (!ct.IsCancellationRequested)
        {
            _logger.LogWarning(ex, "Public topup create failed for tenant {TenantId}", tenantId);
            return null;
        }
    }

    private sealed class OptionsWire
    {
        public bool Allowed { get; set; }
        public List<PackageWire>? Packages { get; set; }
    }

    private sealed class PackageWire
    {
        public string Code { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public decimal PriceVnd { get; set; }
        public decimal Credits { get; set; }
        public int ValidityDays { get; set; }
    }

    private sealed class TopupWire
    {
        public string? ReferenceCode { get; set; }
        public decimal? AmountVnd { get; set; }
        public string? Status { get; set; }
        public DateTime ExpiresAtUtc { get; set; }
        public string? QrPayload { get; set; }
        public string? BillId { get; set; }
    }
}
