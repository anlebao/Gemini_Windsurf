using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using VanAn.KhachLink.Models;

namespace VanAn.KhachLink.Services.Http;

/// <summary>
/// Tenant Profile Page (2026-07-21): HTTP client for /store/{slug} page data.
/// Loads tenant store info by slug + public feature settings (section toggles).
/// Calls Gateway endpoints (anonymous â€” KhachLink is unauthenticated).
/// </summary>
public class TenantProfileHttpService(IHttpClientFactory httpClientFactory, ILogger<TenantProfileHttpService> logger)
{
    private readonly HttpClient _httpClient = httpClientFactory.CreateClient("gateway");
    private readonly ILogger<TenantProfileHttpService> _logger = logger;

    // Gateway API returns enums as strings (e.g. "theme":"Classic").
    // Same fix as ShopConfigHttpService + Directory SSR (issue #157).
    private static readonly JsonSerializerOptions GatewayJsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    /// <summary>Load tenant store info by URL slug. Returns null if not found.</summary>
    public async Task<ShopDto?> GetBySlugAsync(string slug, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(slug))
            return null;

        try
        {
            var response = await _httpClient.GetAsync($"api/tenants/by-slug/{Uri.EscapeDataString(slug)}", ct);
            if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                _logger.LogInformation("Tenant slug '{Slug}' not found", slug);
                return null;
            }
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadFromJsonAsync<ShopDto>(GatewayJsonOptions, cancellationToken: ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading tenant by slug '{Slug}'", slug);
            return null;
        }
    }

    /// <summary>Load public feature settings for a tenant (section toggles). Returns defaults on error.</summary>
    public async Task<TenantFeatureSettingsDto> GetFeatureSettingsAsync(Guid tenantId, CancellationToken ct = default)
    {
        if (tenantId == Guid.Empty)
            return new TenantFeatureSettingsDto();

        try
        {
            var response = await _httpClient.GetAsync($"api/tenants/{tenantId}/feature-settings", ct);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadFromJsonAsync<TenantFeatureSettingsDto>(cancellationToken: ct)
                ?? new TenantFeatureSettingsDto();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading feature settings for tenant {TenantId}", tenantId);
            return new TenantFeatureSettingsDto();
        }
    }
}

/// <summary>
/// Tenant Profile Page (2026-07-21): Public feature settings for /store/{slug} page.
/// Subset of ShopFeatureSettingsDto â€” only the section toggles relevant to customers.
/// Defaults match ShopFeatureSettingsEntity defaults (4 sections ON, AIChat OFF).
/// </summary>
public class TenantFeatureSettingsDto
{
    public bool Campaign_Section_Enabled { get; set; } = true;
    public bool VibeShowcase_Section_Enabled { get; set; } = true;
    public bool GoogleMap_Section_Enabled { get; set; } = true;
    public bool SocialHub_Section_Enabled { get; set; } = true;
    public bool AIChat_Enabled { get; set; }
}
