using System.Net.Http.Json;
using System.Text.Json;
using VanAn.CoreHub.Domain.Repositories;
using VanAn.CoreHub.Repositories;
using VanAn.CoreHub.Services;
using VanAn.Shared.Domain;
using VanAn.Shared.Services;

namespace VanAn.ShopERP.Services;

/// <summary>
/// Loyalty Points Integrity (Batch 2, T1.4) — ShopERP POS proxy for ILoyaltyRewardsService.
///
/// WRITES (AddPointsAsync / SubtractPointsAsync) are forwarded to the Gateway PG ledger
/// (/api/internal/loyalty/award + /spend) — PG is the single source of truth (multi-VPS).
/// READS (GetCustomerRewardsAsync / GetOrCreate / GetAll / UpdateHistory) stay on the LOCAL
/// SQLite mirror (offline-first display, kept in sync via NATS vanan.cloud.loyalty.changed.*).
///
/// IdentityLevel gate: enforced LOCALLY before forwarding a spend (exact same semantics as the
/// old SQLite SubtractPointsAsync — ShopERP customer record is authoritative for redemption UX).
///
/// Graceful degradation (decision D5):
///   - Award/refund: Gateway unreachable → returns true (Skipped) — order/voucher still completes,
///     points NOT written (compensated later via backfill/data repair).
///   - Spend: Gateway unreachable → returns false — a redemption must not succeed without a
///     ledger deduction.
/// </summary>
public sealed class LoyaltyRewardsServiceHttpProxy(
    ILoyaltyRewardsRepository repository,
    ICustomerRepository customerRepository,
    IShopFeatureSettingsService? shopFeatureSettingsService,
    IHttpClientFactory httpClientFactory,
    ILogger<LoyaltyRewardsServiceHttpProxy> logger) : ILoyaltyRewardsService
{
    private readonly ILoyaltyRewardsRepository _repository = repository;
    private readonly ICustomerRepository _customerRepository = customerRepository;
    private readonly IShopFeatureSettingsService? _shopFeatureSettingsService = shopFeatureSettingsService;
    private readonly IHttpClientFactory _httpClientFactory = httpClientFactory;
    private readonly ILogger<LoyaltyRewardsServiceHttpProxy> _logger = logger;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    /// <inheritdoc/>
    public async Task<LoyaltyRewards> GetOrCreateCustomerRewardsAsync(Guid customerId, TenantId tenantId)
    {
        LoyaltyRewards? rewards = await _repository.GetByCustomerAndTenantIdAsync(customerId, tenantId);
        if (rewards is not null)
        {
            return rewards;
        }

        rewards = new LoyaltyRewards(tenantId, customerId);
        rewards.UpdateHistory(JsonSerializer.Serialize(new List<LoyaltyHistoryEntry>()));
        _ = await _repository.AddAsync(rewards);
        await _repository.SaveChangesAsync();
        _logger.LogInformation("LoyaltyRewardsHttpProxy: created local mirror row for customer {CustomerId} tenant {TenantId}", customerId, tenantId.Value);
        return rewards;
    }

    /// <inheritdoc/>
    public async Task<bool> AddPointsAsync(Guid customerId, Guid tenantId, int points, string reason)
    {
        if (points <= 0)
        {
            return false;
        }

        try
        {
            // POS award → Gateway PG ledger. Auto-generated key (retries NOT idempotent — order
            // awards pass "earn:{orderId}" through the ledger path instead and are guarded centrally).
            string idempotencyKey = $"pos:{customerId}:{Guid.NewGuid()}";
            _logger.LogWarning("LoyaltyRewardsHttpProxy: no idempotency key provided for add (customer {CustomerId}) — auto-generated. Retries NOT idempotent.", customerId);

            var client = _httpClientFactory.CreateClient("GatewayInternal");
            var resp = await client.PostAsJsonAsync("api/internal/loyalty/award", new AwardRequest
            {
                CustomerId = customerId,
                TenantId = tenantId,
                Points = points,
                Reason = reason,
                CustomerDeviceId = null,
                OrderAmount = null,
                IdempotencyKey = idempotencyKey
            }, JsonOptions);

            LedgerResult result = await ParseLedgerResponseAsync(resp);
            if (result.Status == LedgerOperationStatus.Success)
            {
                _logger.LogInformation("LoyaltyRewardsHttpProxy: awarded {Points} pts via Gateway ledger (customer {CustomerId}, balance={Balance})",
                    points, customerId, result.NewBalance);
                return true;
            }

            // Skipped (guard/budget/flag) is NOT an error — caller continues (D5).
            _logger.LogInformation("LoyaltyRewardsHttpProxy: award skipped by ledger for customer {CustomerId}: {Message}",
                customerId, result.Error);
            return result.Status == LedgerOperationStatus.Skipped;
        }
        catch (Exception ex)
        {
            // D5: Gateway down → do not fail the order; points compensated later.
            _logger.LogWarning(ex, "LoyaltyRewardsHttpProxy: award unreachable for customer {CustomerId} — SKIPPED (order still completes)", customerId);
            return true;
        }
    }

    /// <inheritdoc/>
    public async Task<bool> SubtractPointsAsync(Guid customerId, Guid tenantId, int points, string reason)
    {
        if (points <= 0)
        {
            return false;
        }

        try
        {
            // IdentityLevel gate — LOCAL (ShopERP customer is authoritative for redemption UX).
            // Same semantics as the old SQLite SubtractPointsAsync (kept before the proxy cutover).
            Customer? customer = await _customerRepository.GetByIdAsync(customerId);
            if (customer is not null)
            {
                bool requirePhoneVerification = true;
                if (_shopFeatureSettingsService is not null && customer.TenantId is not null)
                {
                    try
                    {
                        var settings = await _shopFeatureSettingsService.GetSettingsAsync(customer.TenantId.Value);
                        requirePhoneVerification = settings.Loyalty_RequirePhoneVerificationForRedeem;
                    }
                    catch (Exception settingsEx)
                    {
                        _logger.LogWarning(settingsEx, "LoyaltyRewardsHttpProxy: failed to load ShopFeatureSettings for tenant {TenantId} — using default (require verification)", customer.TenantId);
                    }
                }

                if (requirePhoneVerification && customer.IdentityLevel < IdentityLevel.Verified)
                {
                    _logger.LogWarning("LoyaltyRewardsHttpProxy: redeem blocked for customer {CustomerId}: IdentityLevel={Current} < Required={Required}",
                        customerId, customer.IdentityLevel, IdentityLevel.Verified);
                    throw new IdentityLevelNotSufficientException(customerId, customer.IdentityLevel, IdentityLevel.Verified);
                }
            }

            var client = _httpClientFactory.CreateClient("GatewayInternal");
            string idempotencyKey = $"pos-spend:{customerId}:{Guid.NewGuid()}";
            var resp = await client.PostAsJsonAsync("api/internal/loyalty/spend", new SpendRequest
            {
                CustomerId = customerId,
                TenantId = tenantId,
                Points = points,
                Reason = reason,
                IdempotencyKey = idempotencyKey
            }, JsonOptions);

            LedgerResult result = await ParseLedgerResponseAsync(resp);
            if (result.Status == LedgerOperationStatus.Success)
            {
                _logger.LogInformation("LoyaltyRewardsHttpProxy: spent {Points} pts via Gateway ledger (customer {CustomerId}, balance={Balance})",
                    points, customerId, result.NewBalance);
                return true;
            }

            _logger.LogWarning("LoyaltyRewardsHttpProxy: spend rejected by ledger for customer {CustomerId}: {Error}", customerId, result.Error);
            return false;
        }
        catch (IdentityLevelNotSufficientException)
        {
            throw;
        }
        catch (Exception ex)
        {
            // Gateway down → a redemption must NOT silently succeed without a ledger deduction.
            _logger.LogError(ex, "LoyaltyRewardsHttpProxy: spend unreachable for customer {CustomerId} — REJECTED", customerId);
            return false;
        }
    }

    /// <inheritdoc/>
    public async Task<LoyaltyRewards?> GetCustomerRewardsAsync(Guid customerId)
        => await _repository.GetByCustomerIdAsync(customerId);

    /// <inheritdoc/>
    public async Task<List<LoyaltyRewards>> GetAllRewardsAsync()
    {
        IEnumerable<LoyaltyRewards> rewards = await _repository.GetActiveAsync();
        return rewards.ToList();
    }

    /// <inheritdoc/>
    public async Task<bool> UpdateHistoryAsync(Guid customerId, string historyEntry)
    {
        LoyaltyRewards? rewards = await GetCustomerRewardsAsync(customerId);
        if (rewards is null)
        {
            return false;
        }

        rewards.UpdateHistory(historyEntry);
        _ = await _repository.UpdateAsync(rewards);
        await _repository.SaveChangesAsync();
        return true;
    }

    /// <inheritdoc/>
    public async Task<bool> ActivateCustomerAsync(Guid customerId)
    {
        try
        {
            Customer? customer = await _customerRepository.GetByIdAsync(customerId);
            if (customer is null || customer.TenantId is null)
            {
                _logger.LogWarning("LoyaltyRewardsHttpProxy: activation skipped — customer {CustomerId} not found", customerId);
                return false;
            }

            // Local mirror row (read path) + welcome bonus through the Gateway ledger (idempotent by customerId).
            _ = await GetOrCreateCustomerRewardsAsync(customerId, customer.TenantId);

            var client = _httpClientFactory.CreateClient("GatewayInternal");
            var resp = await client.PostAsJsonAsync("api/internal/loyalty/award", new AwardRequest
            {
                CustomerId = customerId,
                TenantId = customer.TenantId.Value,
                Points = 100,
                Reason = "Welcome bonus for joining loyalty program",
                CustomerDeviceId = customer.DeviceId,
                IdempotencyKey = $"welcome:{customerId}"
            }, JsonOptions);

            LedgerResult result = await ParseLedgerResponseAsync(resp);
            _logger.LogInformation("LoyaltyRewardsHttpProxy: welcome bonus result for customer {CustomerId}: {Status} {Message}",
                customerId, result.Status, result.Error);
            return result.Status is LedgerOperationStatus.Success or LedgerOperationStatus.Skipped;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "LoyaltyRewardsHttpProxy: activation failed for customer {CustomerId}", customerId);
            return false;
        }
    }

    private static async Task<LedgerResult> ParseLedgerResponseAsync(HttpResponseMessage resp)
    {
        string json = await resp.Content.ReadAsStringAsync();
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

        return LedgerResult.Rejected(error ?? "Ledger rejected the operation");
    }
}
