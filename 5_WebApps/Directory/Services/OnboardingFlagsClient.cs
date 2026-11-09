using Microsoft.Extensions.Caching.Memory;
using System.Text.Json;

namespace VanAn.Directory.Services;

/// <summary>
/// Self-Serve Onboarding P2 (2026-11-09, SRS FR-20/FR-22): Directory SSR client
/// cho GET /api/v1/platform/onboarding-flags (public, AllowAnonymous).
/// Cache IMemoryCache 60s; lỗi/timeout → null (CTA ẩn — fail-safe).
/// </summary>
public class OnboardingFlagsClient
{
    private readonly HttpClient _httpClient;
    private readonly IMemoryCache _cache;
    private readonly ILogger<OnboardingFlagsClient> _logger;
    private readonly JsonSerializerOptions _jsonOptions;
    private const string CacheKey = "onboarding-flags";
    private static readonly TimeSpan CacheTtl = TimeSpan.FromSeconds(60);

    public OnboardingFlagsClient(HttpClient httpClient, IMemoryCache cache, ILogger<OnboardingFlagsClient> logger, JsonSerializerOptions jsonOptions)
    {
        _httpClient = httpClient;
        _cache = cache;
        _logger = logger;
        _jsonOptions = jsonOptions;
    }

    /// <summary>4 toggles self-serve; null khi Gateway lỗi (CTA phải ẩn).</summary>
    public async Task<OnboardingFlagsInfo?> GetFlagsAsync(CancellationToken ct = default)
    {
        if (_cache.TryGetValue(CacheKey, out OnboardingFlagsInfo? cached))
            return cached;

        try
        {
            var resp = await _httpClient.GetAsync("api/v1/platform/onboarding-flags", ct);
            if (!resp.IsSuccessStatusCode) return null;

            var dto = await resp.Content.ReadFromJsonAsync<OnboardingFlagsInfo>(_jsonOptions, ct);
            if (dto is null) return null;

            _cache.Set(CacheKey, dto, CacheTtl);
            return dto;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "OnboardingFlagsClient: flags fetch failed — CTA hidden");
            return null;
        }
    }
}

/// <summary>Mirror OnboardingFlagsDto (Gateway) — 4 bool độc lập.</summary>
public record OnboardingFlagsInfo(
    bool SelfServeEnabled,
    bool DirectoryCta,
    bool QrEntry,
    bool DeeplinkPrefill);
