using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using VanAn.CoreHub.Infrastructure;
using VanAn.CoreHub.Infrastructure.Entities;
using VanAn.Shared.Domain;
using VanAn.Shared.Domain.Common;

namespace VanAn.CoreHub.Services;

/// <summary>
/// Implementation of <see cref="IShopFeatureSettingsService"/>.
/// Reads/writes <see cref="ShopFeatureSettingsEntity"/> per tenant via <see cref="IVanAnDbContext"/>.
/// </summary>
public class ShopFeatureSettingsService : IShopFeatureSettingsService
{
    private readonly IVanAnDbContext _context;
    private readonly ILogger<ShopFeatureSettingsService> _logger;

    public ShopFeatureSettingsService(IVanAnDbContext context, ILogger<ShopFeatureSettingsService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<ShopFeatureSettingsDto> GetSettingsAsync(Guid tenantId, CancellationToken ct = default)
    {
        ShopFeatureSettingsEntity? entity = await GetEntityAsync(tenantId, ct);
        if (entity == null)
        {
            // Return defaults without creating — creation happens on first Update
            return new ShopFeatureSettingsDto();
        }
        return ToDto(entity);
    }

    public async Task<ShopFeatureSettingsDto> UpdateSettingsAsync(Guid tenantId, ShopFeatureSettingsDto settings, CancellationToken ct = default)
    {
        ShopFeatureSettingsEntity? entity = await GetEntityAsync(tenantId, ct);
        if (entity == null)
        {
            // Create new — TenantId set via BaseEntity constructor
            entity = new ShopFeatureSettingsEntity(new TenantId(tenantId));
            _context.ShopFeatureSettings.Add(entity);
        }

        entity.UpdateToggles(
            settings.QR_TableNumber_Enabled,
            settings.Kitchen_Workflow_Enabled,
            settings.Voice_Note_Enabled,
            settings.Loyalty_Program_Enabled,
            settings.Accounting_Sync_Enabled,
            settings.EInvoice_Auto_Export_Enabled,
            settings.PollingIntervalSeconds,
            settings.VAT_Display_Enabled,
            settings.Price_Validation_Enabled,
            settings.Campaign_Section_Enabled,
            settings.VibeShowcase_Section_Enabled,
            settings.GoogleMap_Section_Enabled,
            settings.SocialHub_Section_Enabled,
            settings.AIChat_Enabled);

        await _context.SaveChangesAsync(ct);
        _logger.LogInformation("Updated shop feature settings for tenant {TenantId}", tenantId);
        return ToDto(entity);
    }

    public async Task<bool> IsEnabledAsync(Guid tenantId, string toggleName, CancellationToken ct = default)
    {
        ShopFeatureSettingsDto settings = await GetSettingsAsync(tenantId, ct);
        return toggleName switch
        {
            nameof(ShopFeatureSettingsDto.QR_TableNumber_Enabled) => settings.QR_TableNumber_Enabled,
            nameof(ShopFeatureSettingsDto.Kitchen_Workflow_Enabled) => settings.Kitchen_Workflow_Enabled,
            nameof(ShopFeatureSettingsDto.Voice_Note_Enabled) => settings.Voice_Note_Enabled,
            nameof(ShopFeatureSettingsDto.Loyalty_Program_Enabled) => settings.Loyalty_Program_Enabled,
            nameof(ShopFeatureSettingsDto.Accounting_Sync_Enabled) => settings.Accounting_Sync_Enabled,
            nameof(ShopFeatureSettingsDto.EInvoice_Auto_Export_Enabled) => settings.EInvoice_Auto_Export_Enabled,
            nameof(ShopFeatureSettingsDto.VAT_Display_Enabled) => settings.VAT_Display_Enabled,
            nameof(ShopFeatureSettingsDto.Price_Validation_Enabled) => settings.Price_Validation_Enabled,
            nameof(ShopFeatureSettingsDto.Campaign_Section_Enabled) => settings.Campaign_Section_Enabled,
            nameof(ShopFeatureSettingsDto.VibeShowcase_Section_Enabled) => settings.VibeShowcase_Section_Enabled,
            nameof(ShopFeatureSettingsDto.GoogleMap_Section_Enabled) => settings.GoogleMap_Section_Enabled,
            nameof(ShopFeatureSettingsDto.SocialHub_Section_Enabled) => settings.SocialHub_Section_Enabled,
            nameof(ShopFeatureSettingsDto.AIChat_Enabled) => settings.AIChat_Enabled,
            _ => false
        };
    }

    private async Task<ShopFeatureSettingsEntity?> GetEntityAsync(Guid tenantId, CancellationToken ct)
    {
        // Use IgnoreQueryFilters to find by raw TenantId (since the entity is tenant-scoped)
        // Pattern #1: use direct TenantId comparison, NOT .Value accessor (EF Core applies TenantIdConverter)
        var tid = new TenantId(tenantId);
        return await _context.ShopFeatureSettings
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(s => s.TenantId == tid, ct);
    }

    private static ShopFeatureSettingsDto ToDto(ShopFeatureSettingsEntity entity) => new()
    {
        QR_TableNumber_Enabled = entity.QR_TableNumber_Enabled,
        Kitchen_Workflow_Enabled = entity.Kitchen_Workflow_Enabled,
        Voice_Note_Enabled = entity.Voice_Note_Enabled,
        Loyalty_Program_Enabled = entity.Loyalty_Program_Enabled,
        Accounting_Sync_Enabled = entity.Accounting_Sync_Enabled,
        EInvoice_Auto_Export_Enabled = entity.EInvoice_Auto_Export_Enabled,
        VAT_Display_Enabled = entity.VAT_Display_Enabled,
        Price_Validation_Enabled = entity.Price_Validation_Enabled,
        PollingIntervalSeconds = entity.PollingIntervalSeconds,
        Campaign_Section_Enabled = entity.Campaign_Section_Enabled,
        VibeShowcase_Section_Enabled = entity.VibeShowcase_Section_Enabled,
        GoogleMap_Section_Enabled = entity.GoogleMap_Section_Enabled,
        SocialHub_Section_Enabled = entity.SocialHub_Section_Enabled,
        AIChat_Enabled = entity.AIChat_Enabled
    };
}
