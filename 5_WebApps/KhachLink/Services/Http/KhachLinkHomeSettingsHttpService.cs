using System.Net.Http.Json;

namespace VanAn.KhachLink.Services.Http;

/// <summary>
/// #100: HTTP client for KhachLink home page section toggles — GLOBAL (not tenant-scoped).
/// Calls Gateway /api/platform/khachlink-home-settings directly (no tenantId needed).
/// KhachLink Home.razor uses this to show/hide sections based on SystemAdmin's global config.
/// </summary>
public class KhachLinkHomeSettingsHttpService(IHttpClientFactory httpClientFactory, ILogger<KhachLinkHomeSettingsHttpService> logger)
{
    private readonly HttpClient _httpClient = httpClientFactory.CreateClient("gateway");
    private readonly ILogger<KhachLinkHomeSettingsHttpService> _logger = logger;

    /// <summary>Get global KhachLink home settings (no tenant context needed).</summary>
    public async Task<KhachLinkHomeSettingsDto?> GetSettingsAsync()
    {
        try
        {
            KhachLinkHomeSettingsDto? result = await _httpClient.GetFromJsonAsync<KhachLinkHomeSettingsDto>(
                "api/platform/khachlink-home-settings");
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching KhachLink home settings (global)");
            return null;
        }
    }
}

/// <summary>
/// #100: DTO for KhachLink home page section toggles (global, not tenant-scoped).
/// Mirrors Gateway's KhachLinkHomeSettingsDto.
/// </summary>
public class KhachLinkHomeSettingsDto
{
    public bool Home_CampaignSection_Enabled { get; set; } = true;
    public bool Home_StoreSection_Enabled { get; set; } = true;
    public bool Home_FeaturedSection_Enabled { get; set; } = true;
    public bool Home_SocialHub_Enabled { get; set; } = true;
}
