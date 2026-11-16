using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using System.Text.Json.Serialization;
using VanAn.Atrs.Contracts;

namespace VanAn.Atrs.Infra;

/// <summary>
/// ILoyaltyGatewayClient impl — gọi Gateway /api/internal/loyalty/* (X-Internal-Api-Key).
/// Fail-closed cho adjust (dispute không được mark RESOLVED khi ledger không confirm);
/// fail-open rỗng cho entries read (investigator vẫn xem được case, transactions = []).
/// </summary>
public sealed class LoyaltyGatewayClient(
    IHttpClientFactory httpClientFactory,
    ILogger<LoyaltyGatewayClient> logger) : ILoyaltyGatewayClient
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
    };

    public async Task<IReadOnlyList<LoyaltyLedgerEntryDto>> GetEntriesAsync(
        Guid customerId, Guid tenantId, DateTime? fromUtc = null, DateTime? toUtc = null,
        int limit = 100, CancellationToken ct = default)
    {
        try
        {
            var client = httpClientFactory.CreateClient("gateway-internal");
            var url = $"api/internal/loyalty/entries?customerId={customerId}&tenantId={tenantId}&limit={limit}";
            if (fromUtc.HasValue) url += $"&from={Uri.EscapeDataString(fromUtc.Value.ToUniversalTime().ToString("o"))}";
            if (toUtc.HasValue) url += $"&to={Uri.EscapeDataString(toUtc.Value.ToUniversalTime().ToString("o"))}";

            var resp = await client.GetAsync(url, ct);
            if (!resp.IsSuccessStatusCode)
            {
                logger.LogWarning("LoyaltyGatewayClient: entries HTTP {Status} for customer {CustomerId} tenant {TenantId}",
                    resp.StatusCode, customerId, tenantId);
                return [];
            }
            var entries = await resp.Content.ReadFromJsonAsync<List<LoyaltyLedgerEntryDto>>(JsonOptions, ct);
            return entries ?? [];
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException)
        {
            logger.LogWarning(ex, "LoyaltyGatewayClient: entries unreachable for customer {CustomerId} tenant {TenantId}", customerId, tenantId);
            return [];
        }
    }

    public async Task<LoyaltyAdjustResult> AdjustPointsAsync(
        Guid customerId, Guid tenantId, int points, string reason,
        string idempotencyKey, CancellationToken ct = default)
    {
        try
        {
            var client = httpClientFactory.CreateClient("gateway-internal");
            var resp = await client.PostAsJsonAsync("api/internal/loyalty/points/adjust-customer",
                new { customerId, tenantId, points, reason, idempotencyKey }, JsonOptions, ct);

            var body = await resp.Content.ReadAsStringAsync(ct);
            using var doc = JsonDocument.Parse(string.IsNullOrWhiteSpace(body) ? "{}" : body);
            bool success = doc.RootElement.TryGetProperty("success", out var s) && s.GetBoolean();
            string? error = doc.RootElement.TryGetProperty("error", out var e) ? e.GetString() : null;
            // Authoritative reference = AllianceTransaction id; fallback idempotency key.
            string? reference = null;
            if (doc.RootElement.TryGetProperty("transactionId", out var tx) && tx.ValueKind == JsonValueKind.String)
                reference = tx.GetString();
            else if (doc.RootElement.TryGetProperty("newBalance", out var b) && b.ValueKind == JsonValueKind.Number)
                reference = $"balance:{b.GetInt32()}";

            if (resp.IsSuccessStatusCode && success)
                return new LoyaltyAdjustResult(true, reference ?? idempotencyKey, null);

            logger.LogWarning("LoyaltyGatewayClient: adjust-customer HTTP {Status} customer {CustomerId}: {Error}",
                resp.StatusCode, customerId, error);
            return new LoyaltyAdjustResult(false, null, error ?? $"HTTP {(int)resp.StatusCode}");
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException)
        {
            logger.LogError(ex, "LoyaltyGatewayClient: adjust-customer unreachable for customer {CustomerId}", customerId);
            return new LoyaltyAdjustResult(false, null, "Ledger gateway không liên lạc được.");
        }
    }
}
