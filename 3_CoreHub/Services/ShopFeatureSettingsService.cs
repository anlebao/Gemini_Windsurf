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
            settings.EInvoice_Auto_Export_Enabled);

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
            _ => false
        };
    }

    private async Task<ShopFeatureSettingsEntity?> GetEntityAsync(Guid tenantId, CancellationToken ct)
    {
        // Use IgnoreQueryFilters to find by raw TenantId (since the entity is tenant-scoped)
        return await _context.ShopFeatureSettings
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(s => s.TenantId.Value == tenantId, ct);
    }

    private static ShopFeatureSettingsDto ToDto(ShopFeatureSettingsEntity entity) => new()
    {
        QR_TableNumber_Enabled = entity.QR_TableNumber_Enabled,
        Kitchen_Workflow_Enabled = entity.Kitchen_Workflow_Enabled,
        Voice_Note_Enabled = entity.Voice_Note_Enabled,
        Loyalty_Program_Enabled = entity.Loyalty_Program_Enabled,
        Accounting_Sync_Enabled = entity.Accounting_Sync_Enabled,
        EInvoice_Auto_Export_Enabled = entity.EInvoice_Auto_Export_Enabled
    };
}
