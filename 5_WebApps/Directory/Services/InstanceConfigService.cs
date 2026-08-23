using Microsoft.Extensions.Caching.Memory;
using System.Text.Json;
using VanAn.Shared.Domain;
using VanAn.Shared.Domain.Aggregates.KhachLinkAggregate;

namespace VanAn.Directory.Services;

/// <summary>
/// Fetch KhachLink instance config from Gateway by-domain endpoint.
/// Server-side cache (IMemoryCache, 5 min TTL) — thay thế localStorage cache của KhachLink WASM.
/// </summary>
public class InstanceConfigService
{
    private readonly HttpClient _httpClient;
    private readonly IMemoryCache _cache;
    private readonly ILogger<InstanceConfigService> _logger;
    private static readonly TimeSpan CacheTtl = TimeSpan.FromMinutes(5);

    public InstanceConfigService(HttpClient httpClient, IMemoryCache cache, ILogger<InstanceConfigService> logger)
    {
        _httpClient = httpClient;
        _cache = cache;
        _logger = logger;
    }

    /// <summary>
    /// Get instance config by domain. Returns null if not found or feature flag OFF.
    /// Caches result 5 phút keyed by domain.
    /// </summary>
    public async Task<DirectoryInstanceConfig?> GetByDomainAsync(string domain)
    {
        if (string.IsNullOrWhiteSpace(domain))
            return null;

        var cacheKey = $"instance:{domain}";
        if (_cache.TryGetValue(cacheKey, out DirectoryInstanceConfig? cached))
            return cached;

        try
        {
            var resp = await _httpClient.GetAsync($"/api/v1/khachlink-instances/by-domain/{Uri.EscapeDataString(domain)}");
            if (!resp.IsSuccessStatusCode)
            {
                if (resp.StatusCode == System.Net.HttpStatusCode.NotFound)
                    _logger.LogDebug("GetByDomainAsync: 404 for {Domain} (not registered)", domain);
                return null;
            }

            var dto = await resp.Content.ReadFromJsonAsync<KhachLinkInstanceDto>();
            if (dto == null) return null;

            var config = new DirectoryInstanceConfig
            {
                Profile = dto.Profile,
                IsActive = dto.IsActive,
                OwnerTenantId = dto.OwnerTenantId,
                Theme = dto.Theme,
                LogoUrl = dto.LogoUrl,
                NavColor = dto.NavColor,
                HeaderColor = dto.HeaderColor,
                FooterColor = dto.FooterColor
            };

            _cache.Set(cacheKey, config, CacheTtl);
            return config;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "GetByDomainAsync: error fetching instance config for {Domain}", domain);
            return null;
        }
    }
}

public class DirectoryInstanceConfig
{
    public KhachLinkProfile Profile { get; set; } = KhachLinkProfile.FullCommerce;
    public bool IsActive { get; set; } = true;
    public Guid? OwnerTenantId { get; set; }
    public string? Theme { get; set; }
    public string? LogoUrl { get; set; }
    public string? NavColor { get; set; }
    public string? HeaderColor { get; set; }
    public string? FooterColor { get; set; }
}

// DTO matching Gateway KhachLinkInstanceDto (2_Gateway/Controllers/KhachLinkInstanceController.cs L266-283)
public class KhachLinkInstanceDto
{
    public Guid Id { get; set; }
    public string Label { get; set; } = string.Empty;
    public KhachLinkProfile Profile { get; set; }
    public string CustomDomain { get; set; } = string.Empty;
    public Guid? OwnerTenantId { get; set; }
    public bool IsActive { get; set; }
    public string? Theme { get; set; }
    public string? LogoUrl { get; set; }
    public string? NavColor { get; set; }
    public string? HeaderColor { get; set; }
    public string? FooterColor { get; set; }
}
