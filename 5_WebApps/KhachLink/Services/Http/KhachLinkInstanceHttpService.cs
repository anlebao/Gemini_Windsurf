using System.Net;
using System.Text.Json;
using Microsoft.JSInterop;
using VanAn.KhachLink.Models;
using VanAn.Shared.Domain.Aggregates.KhachLinkAggregate;

namespace VanAn.KhachLink.Services.Http;

/// <summary>
/// Fetches KhachLink instance config from Gateway by-domain endpoint.
/// KhachLink runtime calls this on layout init to determine NavFlags + OwnerTenantId.
/// Caches in localStorage (TTL 5 min) to avoid repeated API calls on page navigation.
///
/// When feature flag OFF (Gateway returns 404) or fetch fails → returns null
/// → caller falls back to FullCommerce default (all flags true).
/// </summary>
public class KhachLinkInstanceHttpService(
    IHttpClientFactory httpClientFactory,
    IJSRuntime jsRuntime,
    ILogger<KhachLinkInstanceHttpService> logger)
{
    private readonly HttpClient _httpClient = httpClientFactory.CreateClient("gateway");
    private readonly IJSRuntime _jsRuntime = jsRuntime;
    private readonly ILogger<KhachLinkInstanceHttpService> _logger = logger;

    private static readonly JsonSerializerOptions _jsonOpts = new() { PropertyNameCaseInsensitive = true };
    // #134-fix: Changed cache key from khachlink_instance_config → _v2 to invalidate
    // stale cache from old format (which didn't have IsActive field → deserialized
    // as true → disabled instances still rendered FullCommerce layout).
    private const string _cacheKey = "khachlink_instance_config_v2";
    private const string _cacheTsKey = "khachlink_instance_config_v2_ts";
    // #134-fix: Reduced from 5 min → 1 min so deactivation takes effect faster.
    private static readonly TimeSpan _cacheTtl = TimeSpan.FromMinutes(1);

    /// <summary>
    /// Fetch KhachLink instance config by current browser hostname.
    /// Returns null if feature flag OFF (404), domain not found, or error.
    /// Uses localStorage cache (5 min TTL) to avoid repeated API calls.
    /// </summary>
    public async Task<KhachLinkInstanceConfig?> GetByCurrentDomainAsync()
    {
        // 1. Check localStorage cache
        var cached = await TryGetCachedAsync();
        if (cached != null)
        {
            _logger.LogDebug("GetByCurrentDomainAsync: cache hit");
            return cached;
        }

        // 2. Read current hostname via JS interop
        string hostname;
        try
        {
            hostname = await _jsRuntime.InvokeAsync<string>("eval", "window.location.hostname");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "GetByCurrentDomainAsync: cannot read hostname via JS interop");
            return null;
        }

        if (string.IsNullOrWhiteSpace(hostname))
        {
            _logger.LogDebug("GetByCurrentDomainAsync: empty hostname, returning null");
            return null;
        }

        // 3. Query Gateway by-domain endpoint (anonymous)
        try
        {
            var resp = await _httpClient.GetAsync($"/api/v1/khachlink-instances/by-domain/{WebUtility.UrlEncode(hostname)}");
            if (resp.StatusCode == HttpStatusCode.NotFound)
            {
                // Feature flag OFF or domain not registered → FullCommerce fallback
                _logger.LogDebug("GetByCurrentDomainAsync: 404 for {Hostname} (feature flag OFF or not registered)", hostname);
                return null;
            }
            resp.EnsureSuccessStatusCode();

            var body = await resp.Content.ReadAsStringAsync();
            var dto = JsonSerializer.Deserialize<ByDomainResponse>(body, _jsonOpts);
            if (dto == null)
                return null;

            var config = new KhachLinkInstanceConfig
            {
                Profile = (KhachLinkProfile)dto.Profile,
                OwnerTenantId = dto.OwnerTenantId,
                IsActive = dto.IsActive,
                NavFlags = new KhachLinkNavFlagsDto
                {
                    ShowHome = dto.NavFlags?.ShowHome ?? true,
                    ShowCart = dto.NavFlags?.ShowCart ?? true,
                    ShowOrders = dto.NavFlags?.ShowOrders ?? true,
                    ShowLoyaltyHistory = dto.NavFlags?.ShowLoyaltyHistory ?? true,
                    ShowMissions = dto.NavFlags?.ShowMissions ?? true,
                    ShowRewards = dto.NavFlags?.ShowRewards ?? true,
                    ShowAllianceWallet = dto.NavFlags?.ShowAllianceWallet ?? true,
                    ShowStores = dto.NavFlags?.ShowStores ?? true,
                    ShowCampaigns = dto.NavFlags?.ShowCampaigns ?? true,
                    ShowScan = dto.NavFlags?.ShowScan ?? true,
                    ShowQrClaim = dto.NavFlags?.ShowQrClaim ?? true,
                    ShowCommunity = dto.NavFlags?.ShowCommunity ?? true,
                    ShowJobs = dto.NavFlags?.ShowJobs ?? false,
                    ShowProfile = dto.NavFlags?.ShowProfile ?? true,
                    ShowStaffDashboard = dto.NavFlags?.ShowStaffDashboard ?? true
                }
            };

            await SetCachedAsync(config);
            return config;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "GetByCurrentDomainAsync: error fetching instance config for {Hostname}", hostname);
            return null;
        }
    }

    /// <summary>Clear localStorage cache (force re-fetch on next call).</summary>
    public async Task InvalidateCacheAsync()
    {
        try
        {
            await _jsRuntime.InvokeVoidAsync("localStorage.removeItem", _cacheKey);
            await _jsRuntime.InvokeVoidAsync("localStorage.removeItem", _cacheTsKey);
        }
        catch { /* non-critical */ }
    }

    private async Task<KhachLinkInstanceConfig?> TryGetCachedAsync()
    {
        try
        {
            var tsStr = await _jsRuntime.InvokeAsync<string?>("localStorage.getItem", _cacheTsKey);
            if (tsStr == null || !long.TryParse(tsStr, out var tsUnix) || DateTimeOffset.FromUnixTimeMilliseconds(tsUnix) < DateTimeOffset.UtcNow)
                return null;

            var json = await _jsRuntime.InvokeAsync<string?>("localStorage.getItem", _cacheKey);
            if (string.IsNullOrEmpty(json))
                return null;

            return JsonSerializer.Deserialize<KhachLinkInstanceConfig>(json, _jsonOpts);
        }
        catch { return null; }
    }

    private async Task SetCachedAsync(KhachLinkInstanceConfig config)
    {
        try
        {
            var json = JsonSerializer.Serialize(config, _jsonOpts);
            await _jsRuntime.InvokeVoidAsync("localStorage.setItem", _cacheKey, json);
            await _jsRuntime.InvokeVoidAsync("localStorage.setItem", _cacheTsKey, DateTimeOffset.UtcNow.Add(_cacheTtl).ToUnixTimeMilliseconds().ToString());
        }
        catch { /* non-critical — cache miss is acceptable */ }
    }

    // ── Response DTO (mirrors server-side KhachLinkInstanceDto) ──────────────
    private sealed class ByDomainResponse
    {
        public int Profile { get; set; }
        public Guid? OwnerTenantId { get; set; }
        public bool IsActive { get; set; } = true;
        public NavFlagsResponse? NavFlags { get; set; }
    }

    private sealed class NavFlagsResponse
    {
        public bool ShowHome { get; set; }
        public bool ShowCart { get; set; }
        public bool ShowOrders { get; set; }
        public bool ShowLoyaltyHistory { get; set; }
        public bool ShowMissions { get; set; }
        public bool ShowRewards { get; set; }
        public bool ShowAllianceWallet { get; set; }
        public bool ShowStores { get; set; }
        public bool ShowCampaigns { get; set; }
        public bool ShowScan { get; set; }
        public bool ShowQrClaim { get; set; }
        public bool ShowCommunity { get; set; }
        public bool ShowJobs { get; set; }
        public bool ShowProfile { get; set; }
        public bool ShowStaffDashboard { get; set; }
    }
}
