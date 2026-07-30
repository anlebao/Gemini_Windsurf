using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using VanAn.CoreHub.Infrastructure;
using VanAn.Shared.Domain;
using VanAn.Shared.Domain.Aggregates.SystemSettingAggregate;
using VanAn.Shared.Domain.Aggregates.TenantAggregate;
using Tenant = VanAn.Shared.Domain.Aggregates.TenantAggregate.Tenant;

namespace VanAn.CoreHub.Services;

/// <summary>
/// Sprint 7 — Commerce mode service implementation.
/// Reads/writes SystemSetting (global) + TenantSettings.CommerceModeOverride (per-tenant).
/// Cross-tenant (IgnoreQueryFilters) — SystemAdmin operations.
/// </summary>
public class CommerceModeService(
    IVanAnDbContext dbContext,
    ILogger<CommerceModeService> logger) : ICommerceModeService
{
    private readonly IVanAnDbContext _dbContext = dbContext;
    private readonly ILogger<CommerceModeService> _logger = logger;

    // SystemSetting keys
    private const string KeyGlobalCommerceMode = "GlobalCommerceMode";
    private const string KeyDefaultPlatformFeeRate = "DefaultPlatformFeeRate";
    private const string KeyDefaultCommunityFundRate = "DefaultCommunityFundRate";
    private const string KeyDefaultDeliveryFee = "DefaultDeliveryFee";

    // Defaults (used when SystemSetting row doesn't exist yet)
    private const CommerceMode DefaultMode = CommerceMode.Marketplace;
    private const decimal DefaultPlatformFeeRate = 0.30m;
    private const decimal DefaultCommunityFundRate = 0.05m;
    private const decimal DefaultDeliveryFee = 15000m;

    public async Task<CommerceModeSettingsDto> GetSettingsAsync()
    {
        var globalMode = await GetGlobalModeAsync();
        var (platformFeeRate, communityFundRate, deliveryFee) = await GetDefaultRatesAsync();

        var tenants = await _dbContext.Tenants
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(t => !t.IsDeleted)
            .Select(t => new TenantOverrideDto
            {
                TenantId = t.Id.Value,
                TenantName = t.Name,
                Override = t.Settings.CommerceModeOverride,
                ResolvedMode = t.Settings.CommerceModeOverride != CommerceMode.Inherit
                    ? t.Settings.CommerceModeOverride
                    : globalMode
            })
            .ToListAsync();

        return new CommerceModeSettingsDto
        {
            GlobalMode = globalMode,
            DefaultPlatformFeeRate = platformFeeRate,
            DefaultCommunityFundRate = communityFundRate,
            DefaultDeliveryFee = deliveryFee,
            TenantOverrides = tenants
        };
    }

    public async Task SetGlobalModeAsync(CommerceMode mode, decimal platformFeeRate, decimal communityFundRate, decimal deliveryFee, Guid updatedBy)
    {
        if (mode == CommerceMode.Inherit)
            throw new ArgumentException("Global mode cannot be Inherit (Inherit is only for tenant override)", nameof(mode));
        if (platformFeeRate < 0 || platformFeeRate > 1)
            throw new ArgumentOutOfRangeException(nameof(platformFeeRate), "PlatformFeeRate must be 0-1");
        if (communityFundRate < 0 || communityFundRate > 1)
            throw new ArgumentOutOfRangeException(nameof(communityFundRate), "CommunityFundRate must be 0-1");
        if (platformFeeRate + communityFundRate > 1)
            throw new ArgumentException("PlatformFeeRate + CommunityFundRate must be ≤ 1 (cannot exceed 100% margin)");
        if (deliveryFee < 0)
            throw new ArgumentOutOfRangeException(nameof(deliveryFee), "DeliveryFee cannot be negative");

        var tenantId = new TenantId(Guid.Empty); // global setting
        await UpsertSettingAsync(KeyGlobalCommerceMode, mode.ToString(), tenantId, updatedBy);
        await UpsertSettingAsync(KeyDefaultPlatformFeeRate, platformFeeRate.ToString(), tenantId, updatedBy);
        await UpsertSettingAsync(KeyDefaultCommunityFundRate, communityFundRate.ToString(), tenantId, updatedBy);
        await UpsertSettingAsync(KeyDefaultDeliveryFee, deliveryFee.ToString(), tenantId, updatedBy);

        await _dbContext.SaveChangesAsync();
        _logger.LogInformation("Global commerce mode set to {Mode} by {UpdatedBy}", mode, updatedBy);
    }

    public async Task SetTenantOverrideAsync(Guid tenantId, CommerceMode overrideMode, Guid updatedBy)
    {
        var tenantIdValue = new TenantId(tenantId);
        var tenant = await _dbContext.Tenants
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(t => t.Id == tenantIdValue);

        if (tenant == null)
            throw new InvalidOperationException($"Tenant {tenantId} not found");

        tenant.UpdateCommerceModeOverride(overrideMode);
        await _dbContext.SaveChangesAsync();
        _logger.LogInformation("Tenant {TenantId} commerce mode override set to {Mode} by {UpdatedBy}", tenantId, overrideMode, updatedBy);
    }

    public async Task<CommerceMode> ResolveModeForTenantAsync(Guid tenantId)
    {
        var tenantIdValue = new TenantId(tenantId);
        var tenant = await _dbContext.Tenants
            .IgnoreQueryFilters()
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.Id == tenantIdValue);

        if (tenant == null)
            return await GetGlobalModeAsync();

        if (tenant.Settings.CommerceModeOverride != CommerceMode.Inherit)
            return tenant.Settings.CommerceModeOverride;

        return await GetGlobalModeAsync();
    }

    public async Task<(decimal PlatformFeeRate, decimal CommunityFundRate, decimal DeliveryFee)> GetDefaultRatesAsync()
    {
        var platformFeeRate = await GetSettingValueAsync(KeyDefaultPlatformFeeRate, DefaultPlatformFeeRate);
        var communityFundRate = await GetSettingValueAsync(KeyDefaultCommunityFundRate, DefaultCommunityFundRate);
        var deliveryFee = await GetSettingValueAsync(KeyDefaultDeliveryFee, DefaultDeliveryFee);
        return (platformFeeRate, communityFundRate, deliveryFee);
    }

    private async Task<CommerceMode> GetGlobalModeAsync()
    {
        var value = await GetSettingRawAsync(KeyGlobalCommerceMode);
        if (value == null || !Enum.TryParse<CommerceMode>(value, out var mode))
            return DefaultMode;
        return mode;
    }

    private async Task<decimal> GetSettingValueAsync(string key, decimal defaultValue)
    {
        var raw = await GetSettingRawAsync(key);
        if (raw == null || !decimal.TryParse(raw, out var value))
            return defaultValue;
        return value;
    }

    private async Task<string?> GetSettingRawAsync(string key)
    {
        var setting = await _dbContext.SystemSettings
            .IgnoreQueryFilters()
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.Key == key);
        return setting?.Value;
    }

    private async Task UpsertSettingAsync(string key, string value, TenantId tenantId, Guid updatedBy)
    {
        var setting = await _dbContext.SystemSettings
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(s => s.Key == key);

        if (setting == null)
        {
            setting = new SystemSetting(tenantId, key, value, updatedBy);
            _dbContext.SystemSettings.Add(setting);
        }
        else
        {
            setting.Update(value, updatedBy);
        }
    }
}
