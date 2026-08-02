using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Caching.Memory;
using VanAn.Shared.Domain;
using VanAn.Shared.Services;

namespace VanAn.ShopERP.Services;

/// <summary>
/// Loyalty Consistency Fix Phase 0 (Option B): HTTP proxy implementation of IAllianceWalletService
/// for ShopERP. Calls Gateway internal API (/api/internal/loyalty/*) instead of querying PG directly.
/// Multi-VPS ready: ShopERP never connects to PG; all Alliance operations route through Gateway.
///
/// Caching strategy:
///   - Wallet reads (GetWalletByDeviceIdAsync): cached 10s per device via IMemoryCache.
///   - Write ops (Add/Deduct/Refund): invalidate cache for that device on success.
///
/// Idempotency strategy:
///   - Caller provides stable key (e.g. $"earn:{orderId}"); forwarded in request body.
///   - If caller omits key, proxy generates a GUID (with warning — retries NOT idempotent).
///   - Gateway AllianceWalletService checks IdempotencyKey column → returns cached balance on retry.
///
/// Not supported (Gateway-only admin ops):
///   - GetTransactionsAsync, GetTransactionsByTenantAsync — read via /api/loyalty/wallet (customer-facing).
///   - ConsolidateWalletsAsync, SplitWalletsAsync — SystemAdmin migration ops, run on Gateway directly.
/// </summary>
public class AllianceWalletServiceHttpProxy(
    IHttpClientFactory httpClientFactory,
    IMemoryCache cache,
    ILogger<AllianceWalletServiceHttpProxy> logger) : IAllianceWalletService
{
    private readonly IHttpClientFactory _httpClientFactory = httpClientFactory;
    private readonly IMemoryCache _cache = cache;
    private readonly ILogger<AllianceWalletServiceHttpProxy> _logger = logger;
    private static readonly TimeSpan WalletCacheTtl = TimeSpan.FromSeconds(10);
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    /// <inheritdoc/>
    public async Task<AllianceWallet?> GetWalletByDeviceIdAsync(Guid customerDeviceId)
    {
        string cacheKey = $"alliance_wallet_{customerDeviceId}";
        if (_cache.TryGetValue(cacheKey, out AllianceWallet? cached) && cached is not null)
            return cached;

        try
        {
            var client = _httpClientFactory.CreateClient("GatewayInternal");
            var resp = await client.GetAsync($"/api/internal/loyalty/wallet/{customerDeviceId}");
            if (!resp.IsSuccessStatusCode)
            {
                _logger.LogDebug("GetWallet HTTP failed for device {DeviceId}: {Status}", customerDeviceId, resp.StatusCode);
                return null;
            }

            var dto = await resp.Content.ReadFromJsonAsync<WalletDto>(JsonOptions);
            if (dto is null) return null;

            // Reconstruct a lightweight wallet object (only TotalPointBalance + IsActive needed for read paths)
            var wallet = new AllianceWallet(customerDeviceId, null);
            typeof(AllianceWallet).GetProperty(nameof(AllianceWallet.TotalPointBalance))!
                .SetValue(wallet, dto.TotalPointBalance);
            typeof(AllianceWallet).GetProperty(nameof(AllianceWallet.IsActive))!
                .SetValue(wallet, dto.IsActive);

            _cache.Set(cacheKey, wallet, WalletCacheTtl);
            return wallet;
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "GetWalletByDeviceIdAsync HTTP unreachable for device {DeviceId} — returning null", customerDeviceId);
            return null;
        }
    }

    /// <inheritdoc/>
    public async Task<AllianceWallet> GetOrCreateWalletAsync(Guid customerDeviceId, string? phoneNumber)
    {
        // ShopERP never creates wallets — Gateway creates on first AddPointsAsync.
        // For read paths, return existing or empty stub (caller should fall back to Silo balance).
        return await GetWalletByDeviceIdAsync(customerDeviceId) ?? new AllianceWallet(customerDeviceId, phoneNumber);
    }

    /// <inheritdoc/>
    public async Task<(bool Success, int NewBalance, string? Error)> AddPointsAsync(
        Guid customerDeviceId, Guid tenantId, int points, string reason,
        Guid? sourceOrderId = null, string? idempotencyKey = null)
    {
        idempotencyKey ??= AutoGenerateKey("add", customerDeviceId);
        var body = new
        {
            customerDeviceId,
            tenantId,
            points,
            reason,
            sourceOrderId,
            idempotencyKey
        };
        return await PostPointsAsync("add", body, customerDeviceId);
    }

    /// <inheritdoc/>
    public async Task<(bool Success, int NewBalance, string? Error)> DeductPointsAsync(
        Guid customerDeviceId, Guid tenantId, int points, string reason,
        string? voucherCode = null, string? idempotencyKey = null)
    {
        idempotencyKey ??= AutoGenerateKey("deduct", customerDeviceId);
        var body = new
        {
            customerDeviceId,
            tenantId,
            points,
            reason,
            voucherCode,
            idempotencyKey
        };
        return await PostPointsAsync("deduct", body, customerDeviceId);
    }

    /// <inheritdoc/>
    public async Task<(bool Success, int NewBalance, string? Error)> RefundAsync(
        Guid customerDeviceId, Guid tenantId, int points, string reason,
        string voucherCode, string? idempotencyKey = null)
    {
        idempotencyKey ??= AutoGenerateKey("refund", customerDeviceId);
        var body = new
        {
            customerDeviceId,
            tenantId,
            points,
            reason,
            voucherCode,
            idempotencyKey
        };
        return await PostPointsAsync("refund", body, customerDeviceId);
    }

    /// <inheritdoc/>
    public Task<IReadOnlyList<AllianceTransaction>> GetTransactionsAsync(Guid walletId, int limit = 20)
        => throw new NotSupportedException("Transaction history reads are Gateway-only. Use GET /api/loyalty/wallet (customer-facing).");

    /// <inheritdoc/>
    public Task<IReadOnlyList<AllianceTransaction>> GetTransactionsByTenantAsync(Guid walletId, Guid tenantId, int limit = 20)
        => throw new NotSupportedException("Transaction history reads are Gateway-only.");

    /// <inheritdoc/>
    public Task<MigrationResult> ConsolidateWalletsAsync(Guid tenantId, IReadOnlyList<CustomerBalanceInput> customerBalances, string changedBy)
        => throw new NotSupportedException("Migration operations are Gateway-only admin ops.");

    /// <inheritdoc/>
    public Task<MigrationResult> SplitWalletsAsync(Guid tenantId, string changedBy)
        => throw new NotSupportedException("Migration operations are Gateway-only admin ops.");

    // === Helpers ===

    private async Task<(bool Success, int Balance, string? Error)> PostPointsAsync(
        string op, object body, Guid deviceForCacheInvalidation)
    {
        try
        {
            var client = _httpClientFactory.CreateClient("GatewayInternal");
            var content = JsonContent.Create(body, options: JsonOptions);
            var resp = await client.PostAsync($"/api/internal/loyalty/points/{op}", content);
            var json = await resp.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(json);

            bool success = doc.RootElement.TryGetProperty("success", out var s) && s.GetBoolean();
            int balance = doc.RootElement.TryGetProperty("newBalance", out var b) ? b.GetInt32() : 0;
            string? error = doc.RootElement.TryGetProperty("error", out var e) ? e.GetString() : null;

            if (success)
            {
                // Invalidate cache so next read fetches fresh balance from PG
                _cache.Remove($"alliance_wallet_{deviceForCacheInvalidation}");
            }

            return (success, balance, error);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "HTTP proxy {Op} failed for device {DeviceId} — Gateway unreachable", op, deviceForCacheInvalidation);
            return (false, 0, "Gateway unavailable");
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "HTTP proxy {Op} response parse failed for device {DeviceId}", op, deviceForCacheInvalidation);
            return (false, 0, "Gateway response invalid");
        }
    }

    private string AutoGenerateKey(string op, Guid deviceId)
    {
        _logger.LogWarning("AllianceWalletServiceHttpProxy: no idempotency key provided for {Op} (device {DeviceId}) — auto-generated. Retries NOT idempotent.", op, deviceId);
        return $"{op}:{deviceId}:{Guid.NewGuid()}";
    }

    // DTO matching Gateway WalletResponse
    private sealed class WalletDto
    {
        public int TotalPointBalance { get; set; }
        public bool IsActive { get; set; }
    }
}
