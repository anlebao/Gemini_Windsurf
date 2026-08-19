using System.Net.Http.Json;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Extensions.Caching.Memory;
using VanAn.CoreHub.Services;

namespace VanAn.ShopERP.Services;

/// <summary>
/// OCR Hub S2: ShopERP client for Gateway OCR config API.
/// Calls /api/ocr/config with SystemAdmin Bearer JWT.
/// Used by /admin/ocr-settings Blazor page.
/// Implements IOcrConfigService for DI compatibility (same pattern as FeatureFlagApiClient).
/// </summary>
public sealed class OcrConfigApiClient : GatewayAdminApiClientBase, IOcrConfigService
{
    private static readonly TimeSpan CacheTtl = TimeSpan.FromSeconds(60);
    private readonly IMemoryCache _cache;
    private const string CacheKey = "ocr_config_shoperp";

    public OcrConfigApiClient(
        IHttpClientFactory httpClientFactory,
        IConfiguration configuration,
        IJwtTokenService jwtTokenService,
        AuthenticationStateProvider authStateProvider,
        IMemoryCache cache,
        ILogger<OcrConfigApiClient> logger)
        : base(httpClientFactory, configuration, jwtTokenService, authStateProvider, logger)
    {
        _cache = cache;
    }

    public async Task<OcrEngineConfig> GetConfigAsync(CancellationToken ct = default)
    {
        if (_cache.TryGetValue<OcrEngineConfig>(CacheKey, out var cached))
            return cached!;

        var token = await MintSystemAdminTokenAsync();
        HttpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        var response = await HttpClient.GetAsync("api/ocr/config", ct);
        if (!response.IsSuccessStatusCode)
        {
            // Default: Tesseract (backward compat)
            var defaultConfig = new OcrEngineConfig();
            _cache.Set(CacheKey, defaultConfig, CacheTtl);
            return defaultConfig;
        }

        var config = await response.Content.ReadFromJsonAsync<OcrEngineConfig>(cancellationToken: ct);
        config ??= new OcrEngineConfig();
        _cache.Set(CacheKey, config, CacheTtl);
        return config;
    }

    public async Task UpdateConfigAsync(OcrEngineConfig config, Guid updatedBy, CancellationToken ct = default)
    {
        var token = await MintSystemAdminTokenAsync();
        HttpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        var response = await HttpClient.PutAsJsonAsync("api/ocr/config",
            new { PlateEngine = config.PlateEngine, MenuEngine = config.MenuEngine }, ct);
        response.EnsureSuccessStatusCode();

        _cache.Remove(CacheKey);
    }
}
