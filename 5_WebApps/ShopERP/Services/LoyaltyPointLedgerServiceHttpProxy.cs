using System.Net.Http.Json;
using System.Text.Json;
using VanAn.CoreHub.Services;
using VanAn.Shared.Domain;

namespace VanAn.ShopERP.Services;

/// <summary>
/// Loyalty Points Integrity (Batch 2) — ShopERP POS HTTP proxy for ILoyaltyPointLedgerService.
/// Routes EVERY loyalty write (award/spend/refund/reversal) to the Gateway PG ledger
/// (/api/internal/loyalty/*, X-Internal-Api-Key) — PG = single source of truth (multi-VPS).
///
/// Graceful degradation (decision D5):
///   - Gateway unreachable / 5xx → AwardAsync/RefundAsync return Skipped (order/voucher still
///     completes; points are NOT written — compensated later via backfill/data repair).
///   - SpendAsync → Rejected with a clear message (a redemption must not silently succeed
///     without deducting the ledger).
/// </summary>
public sealed class LoyaltyPointLedgerServiceHttpProxy(
    IHttpClientFactory httpClientFactory,
    ILogger<LoyaltyPointLedgerServiceHttpProxy> logger) : ILoyaltyPointLedgerService
{
    private readonly IHttpClientFactory _httpClientFactory = httpClientFactory;
    private readonly ILogger<LoyaltyPointLedgerServiceHttpProxy> _logger = logger;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    /// <inheritdoc/>
    public async Task<LedgerResult> AwardAsync(AwardRequest request, CancellationToken cancellationToken = default)
    {
        try
        {
            var client = _httpClientFactory.CreateClient("GatewayInternal");
            var resp = await client.PostAsJsonAsync("api/internal/loyalty/award", request, JsonOptions, cancellationToken);
            return await ParseLedgerResponseAsync(resp, "award", request.CustomerId, cancellationToken);
        }
        catch (Exception ex)
        {
            // D5: Gateway down → skip award (do NOT fail the order). Points compensated later.
            _logger.LogWarning(ex, "LoyaltyPointLedger: award unreachable for customer {CustomerId} — SKIPPED (order still completes)", request.CustomerId);
            return LedgerResult.Skipped("Gateway unavailable — award skipped");
        }
    }

    /// <inheritdoc/>
    public async Task<LedgerResult> SpendAsync(SpendRequest request, CancellationToken cancellationToken = default)
    {
        try
        {
            var client = _httpClientFactory.CreateClient("GatewayInternal");
            var resp = await client.PostAsJsonAsync("api/internal/loyalty/spend", request, JsonOptions, cancellationToken);
            return await ParseLedgerResponseAsync(resp, "spend", request.CustomerId, cancellationToken);
        }
        catch (Exception ex)
        {
            // A redemption must not silently succeed without a ledger deduction.
            _logger.LogError(ex, "LoyaltyPointLedger: spend unreachable for customer {CustomerId} — REJECTED", request.CustomerId);
            return LedgerResult.Rejected("Gateway unavailable — không thể đổi điểm ngay bây giờ");
        }
    }

    /// <inheritdoc/>
    public async Task<LedgerResult> RefundAsync(RefundRequest request, CancellationToken cancellationToken = default)
    {
        try
        {
            var client = _httpClientFactory.CreateClient("GatewayInternal");
            var resp = await client.PostAsJsonAsync("api/internal/loyalty/refund", request, JsonOptions, cancellationToken);
            return await ParseLedgerResponseAsync(resp, "refund", request.CustomerId, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "LoyaltyPointLedger: refund unreachable for customer {CustomerId} — SKIPPED (voucher cancel still completes)", request.CustomerId);
            return LedgerResult.Skipped("Gateway unavailable — refund skipped");
        }
    }

    /// <inheritdoc/>
    public async Task<int> RevertOrderAsync(Guid orderId, TenantId tenantId, string reason, CancellationToken cancellationToken = default)
    {
        try
        {
            var client = _httpClientFactory.CreateClient("GatewayInternal");
            var resp = await client.PostAsJsonAsync("api/internal/loyalty/revert-order",
                new { orderId, tenantId = tenantId.Value, reason }, JsonOptions, cancellationToken);
            if (!resp.IsSuccessStatusCode)
            {
                _logger.LogWarning("LoyaltyPointLedger: revert-order HTTP {Status} for order {OrderId}", resp.StatusCode, orderId);
                return 0;
            }

            using var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync(cancellationToken));
            return doc.RootElement.TryGetProperty("pointsReversed", out var p) ? p.GetInt32() : 0;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "LoyaltyPointLedger: revert-order unreachable for order {OrderId} — reversed=0", orderId);
            return 0;
        }
    }

    /// <inheritdoc/>
    public async Task<int?> GetAwardedPointsAsync(Guid orderId, Guid tenantId, CancellationToken cancellationToken = default)
    {
        try
        {
            var client = _httpClientFactory.CreateClient("GatewayInternal");
            var resp = await client.GetAsync($"api/internal/loyalty/awarded?orderId={orderId}&tenantId={tenantId}", cancellationToken);
            if (!resp.IsSuccessStatusCode)
            {
                return null;
            }

            using var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync(cancellationToken));
            if (doc.RootElement.TryGetProperty("awardedPoints", out var awarded) && awarded.ValueKind == JsonValueKind.Number)
            {
                return awarded.GetInt32();
            }

            return null;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "LoyaltyPointLedger: awarded query unreachable for order {OrderId}", orderId);
            return null;
        }
    }

    /// <inheritdoc/>
    public async Task<int> GetBalanceAsync(Guid customerId, Guid tenantId, CancellationToken cancellationToken = default)
    {
        try
        {
            var client = _httpClientFactory.CreateClient("GatewayInternal");
            var resp = await client.GetAsync($"api/internal/loyalty/balance?customerId={customerId}&tenantId={tenantId}", cancellationToken);
            if (!resp.IsSuccessStatusCode)
            {
                return 0;
            }

            using var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync(cancellationToken));
            return doc.RootElement.TryGetProperty("balance", out var balance) ? balance.GetInt32() : 0;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "LoyaltyPointLedger: balance query unreachable for customer {CustomerId} — returning 0", customerId);
            return 0;
        }
    }

    private async Task<LedgerResult> ParseLedgerResponseAsync(
        HttpResponseMessage resp, string op, Guid customerId, CancellationToken cancellationToken)
    {
        string json = await resp.Content.ReadAsStringAsync(cancellationToken);
        using var doc = JsonDocument.Parse(json);

        bool skipped = doc.RootElement.TryGetProperty("skipped", out var skipProp) && skipProp.GetBoolean();
        bool success = doc.RootElement.TryGetProperty("success", out var s) && s.GetBoolean();
        int newBalance = doc.RootElement.TryGetProperty("newBalance", out var b) ? b.GetInt32() : 0;
        string? error = doc.RootElement.TryGetProperty("error", out var e) ? e.GetString()
            : doc.RootElement.TryGetProperty("message", out var m) ? m.GetString()
            : null;

        if (resp.IsSuccessStatusCode && skipped)
        {
            return LedgerResult.Skipped(error ?? "Skipped by ledger");
        }

        if (resp.IsSuccessStatusCode && success)
        {
            return LedgerResult.Ok(newBalance);
        }

        _logger.LogWarning("LoyaltyPointLedger: {Op} rejected for customer {CustomerId} (HTTP {Status}): {Error}",
            op, customerId, resp.StatusCode, error);
        return LedgerResult.Rejected(error ?? "Ledger rejected the operation");
    }
}
