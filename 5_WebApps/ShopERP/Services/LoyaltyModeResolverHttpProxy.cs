using System.Text.Json;
using Microsoft.Extensions.Caching.Memory;
using VanAn.Shared.Domain;
using VanAn.Shared.Services;

namespace VanAn.ShopERP.Services;

/// <summary>
/// Loyalty Consistency Fix Phase 0 (Option B): HTTP proxy implementation of ILoyaltyModeResolver
/// for ShopERP. Calls Gateway internal API (/api/internal/loyalty/effective-config/{tenantId})
/// instead of querying PG LoyaltyTenantConfigs/LoyaltyGlobalConfigs directly.
/// Multi-VPS ready: ShopERP never connects to PG; mode resolution routes through Gateway.
///
/// Caching strategy:
///   - Mode config (mode + maxWalletPoints + isAllianceMember) cached 60s per tenant via IMemoryCache.
///   - Mode changes are rare admin operations (SystemAdmin updates via Gateway) — 60s TTL acceptable.
///   - All 3 interface methods share the same cached config object (1 HTTP call per tenant per 60s).
///
/// Graceful degradation:
///   - Gateway unreachable / 5xx → fallback to Silo mode (safe default — Silo is non-Alliance behavior).
///   - Customer never blocked by Gateway outage — reads fall back to SQLite balance.
/// </summary>
public class LoyaltyModeResolverHttpProxy(
    IHttpClientFactory httpClientFactory,
    IMemoryCache cache,
    ILogger<LoyaltyModeResolverHttpProxy> logger) : ILoyaltyModeResolver
{
    private readonly IHttpClientFactory _httpClientFactory = httpClientFactory;
    private readonly IMemoryCache _cache = cache;
    private readonly ILogger<LoyaltyModeResolverHttpProxy> _logger = logger;
    private static readonly TimeSpan ModeCacheTtl = TimeSpan.FromSeconds(60);
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    // Safe fallback when Gateway is unreachable — Silo mode + default 100k cap + non-member.
    // Customer reads fall back to SQLite balance (LoyaltyReadRouter handles this).
    private const LoyaltyMode FallbackMode = LoyaltyMode.Silo;
    private const int FallbackMaxWalletPoints = 100_000;
    private const bool FallbackIsAllianceMember = false;

    /// <inheritdoc/>
    public async Task<LoyaltyMode> GetEffectiveModeAsync(Guid tenantId)
        => (await GetCachedConfigAsync(tenantId)).Mode;

    /// <inheritdoc/>
    public async Task<int> GetEffectiveMaxWalletPointsAsync(Guid tenantId)
        => (await GetCachedConfigAsync(tenantId)).MaxWalletPoints;

    /// <inheritdoc/>
    public async Task<bool> IsAllianceMemberAsync(Guid tenantId)
        => (await GetCachedConfigAsync(tenantId)).IsAllianceMember;

    // === Helpers ===

    private async Task<CachedModeConfig> GetCachedConfigAsync(Guid tenantId)
    {
        if (tenantId == Guid.Empty)
        {
            return new CachedModeConfig(FallbackMode, FallbackMaxWalletPoints, FallbackIsAllianceMember);
        }

        string cacheKey = $"loyalty_mode_{tenantId}";
        if (_cache.TryGetValue(cacheKey, out CachedModeConfig? cached) && cached is not null)
        {
            return cached;
        }

        CachedModeConfig config = await FetchConfigFromGatewayAsync(tenantId);
        _cache.Set(cacheKey, config, ModeCacheTtl);
        return config;
    }

    private async Task<CachedModeConfig> FetchConfigFromGatewayAsync(Guid tenantId)
    {
        try
        {
            var client = _httpClientFactory.CreateClient("GatewayInternal");
            var resp = await client.GetAsync($"/api/internal/loyalty/effective-config/{tenantId}");
            if (!resp.IsSuccessStatusCode)
            {
                _logger.LogWarning("Mode resolver HTTP failed for tenant {TenantId}: {Status} — fallback Silo", tenantId, resp.StatusCode);
                return new CachedModeConfig(FallbackMode, FallbackMaxWalletPoints, FallbackIsAllianceMember);
            }

            var dto = await resp.Content.ReadFromJsonAsync<EffectiveConfigDto>(JsonOptions);
            if (dto is null || string.IsNullOrEmpty(dto.Mode))
            {
                _logger.LogWarning("Mode resolver HTTP empty response for tenant {TenantId} — fallback Silo", tenantId);
                return new CachedModeConfig(FallbackMode, FallbackMaxWalletPoints, FallbackIsAllianceMember);
            }

            if (!Enum.TryParse<LoyaltyMode>(dto.Mode, ignoreCase: true, out var mode))
            {
                _logger.LogWarning("Mode resolver HTTP unknown mode '{Mode}' for tenant {TenantId} — fallback Silo", dto.Mode, tenantId);
                return new CachedModeConfig(FallbackMode, FallbackMaxWalletPoints, FallbackIsAllianceMember);
            }

            return new CachedModeConfig(mode, dto.MaxWalletPoints, dto.IsAllianceMember);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Mode resolver HTTP unreachable for tenant {TenantId} — fallback Silo", tenantId);
            return new CachedModeConfig(FallbackMode, FallbackMaxWalletPoints, FallbackIsAllianceMember);
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Mode resolver HTTP response parse failed for tenant {TenantId} — fallback Silo", tenantId);
            return new CachedModeConfig(FallbackMode, FallbackMaxWalletPoints, FallbackIsAllianceMember);
        }
    }

    // Immutable cached record
    private sealed record CachedModeConfig(LoyaltyMode Mode, int MaxWalletPoints, bool IsAllianceMember);

    // DTO matching Gateway EffectiveConfigResponse
    private sealed class EffectiveConfigDto
    {
        public string Mode { get; set; } = string.Empty;
        public int MaxWalletPoints { get; set; }
        public bool IsAllianceMember { get; set; }
    }
}
