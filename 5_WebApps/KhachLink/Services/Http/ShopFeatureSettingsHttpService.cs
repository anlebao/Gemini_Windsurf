using VanAn.CoreHub.Services;

namespace VanAn.KhachLink.Services.Http;

/// <summary>
/// HTTP client for shop feature toggle settings.
/// KhachLink calls Gateway: shoperp/api/shop/settings/features
/// YARP forwards to ShopERP /api/shop/settings/features.
/// </summary>
public class ShopFeatureSettingsHttpService(IHttpClientFactory httpClientFactory, ILogger<ShopFeatureSettingsHttpService> logger)
{
    private readonly HttpClient _httpClient = httpClientFactory.CreateClient("gateway");
    private readonly ILogger<ShopFeatureSettingsHttpService> _logger = logger;

    /// <summary>Get feature settings for a tenant.</summary>
    public async Task<ShopFeatureSettingsDto?> GetSettingsAsync(Guid tenantId)
    {
        try
        {
            string url = $"shoperp/api/shop/settings/features?tenantId={tenantId}";
            ShopFeatureSettingsDto? result = await _httpClient.GetFromJsonAsync<ShopFeatureSettingsDto>(url);
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching shop feature settings for tenant {TenantId}", tenantId);
            return null;
        }
    }

    /// <summary>Update feature settings for a tenant.</summary>
    public async Task<ShopFeatureSettingsDto?> UpdateSettingsAsync(Guid tenantId, ShopFeatureSettingsDto settings)
    {
        try
        {
            string url = $"shoperp/api/shop/settings/features?tenantId={tenantId}";
            HttpResponseMessage response = await _httpClient.PutAsJsonAsync(url, settings);
            response.EnsureSuccessStatusCode();
            ShopFeatureSettingsDto? result = await response.Content.ReadFromJsonAsync<ShopFeatureSettingsDto>();
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating shop feature settings for tenant {TenantId}", tenantId);
            return null;
        }
    }

    /// <summary>Check if a specific toggle is enabled. Returns default value if fetch fails.</summary>
    public async Task<bool> IsEnabledAsync(Guid tenantId, string toggleName)
    {
        ShopFeatureSettingsDto? settings = await GetSettingsAsync(tenantId);
        if (settings == null)
            return false;

        return toggleName switch
        {
            nameof(ShopFeatureSettingsDto.QR_TableNumber_Enabled) => settings.QR_TableNumber_Enabled,
            nameof(ShopFeatureSettingsDto.Kitchen_Workflow_Enabled) => settings.Kitchen_Workflow_Enabled,
            nameof(ShopFeatureSettingsDto.Voice_Note_Enabled) => settings.Voice_Note_Enabled,
            nameof(ShopFeatureSettingsDto.Loyalty_Program_Enabled) => settings.Loyalty_Program_Enabled,
            nameof(ShopFeatureSettingsDto.Accounting_Sync_Enabled) => settings.Accounting_Sync_Enabled,
            nameof(ShopFeatureSettingsDto.EInvoice_Auto_Export_Enabled) => settings.EInvoice_Auto_Export_Enabled,
            _ => false
        };
    }
}
