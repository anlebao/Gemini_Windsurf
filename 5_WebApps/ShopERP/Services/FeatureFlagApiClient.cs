using System.Net.Http.Json;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Extensions.Caching.Memory;
using VanAn.CoreHub.Services;

namespace VanAn.ShopERP.Services;

/// <summary>
/// VALCN v2.0: ShopERP client for Gateway Feature Flags toggle API.
/// Calls /api/admin/feature-flags with SystemAdmin Bearer JWT.
/// Used by /admin/valcn-features Blazor page.
/// Also used by ShopERP services to check feature flag state (cached 30s).
/// Implements IFeatureFlagService for DI compatibility.
/// </summary>
public sealed class FeatureFlagApiClient : GatewayAdminApiClientBase, IFeatureFlagService
{
    private static readonly TimeSpan CacheTtl = TimeSpan.FromSeconds(30);
    private readonly IMemoryCache _cache;

    public FeatureFlagApiClient(
        IHttpClientFactory httpClientFactory,
        IConfiguration configuration,
        IJwtTokenService jwtTokenService,
        AuthenticationStateProvider authStateProvider,
        IMemoryCache cache,
        ILogger<FeatureFlagApiClient> logger)
        : base(httpClientFactory, configuration, jwtTokenService, authStateProvider, logger)
    {
        _cache = cache;
    }

    public async Task<bool> IsEnabledAsync(string featureName, CancellationToken ct = default)
    {
        string cacheKey = $"feat_flag_{featureName}";
        if (_cache.TryGetValue(cacheKey, out bool cached))
            return cached;

        var toggles = await GetAllAsync(ct);
        // CRITICAL: default = false (disabled) — opposite of BackgroundServiceToggleService
        bool enabled = toggles.FirstOrDefault(t => t.FeatureName == featureName)?.IsEnabled ?? false;
        _cache.Set(cacheKey, enabled, CacheTtl);
        return enabled;
    }

    public async Task<IReadOnlyList<FeatureFlagDto>> GetAllAsync(CancellationToken ct = default)
    {
        var token = await MintSystemAdminTokenAsync();
        HttpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        var response = await HttpClient.GetAsync("api/admin/feature-flags", ct);
        if (!response.IsSuccessStatusCode)
        {
            return [];
        }

        return await response.Content.ReadFromJsonAsync<List<FeatureFlagDto>>(cancellationToken: ct) ?? [];
    }

    public async Task SetEnabledAsync(string featureName, bool enabled, Guid updatedBy, CancellationToken ct = default)
    {
        var token = await MintSystemAdminTokenAsync();
        HttpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        var response = await HttpClient.PutAsJsonAsync($"api/admin/feature-flags/{featureName}", new { IsEnabled = enabled }, ct);
        response.EnsureSuccessStatusCode();

        _cache.Remove($"feat_flag_{featureName}");
    }
}
