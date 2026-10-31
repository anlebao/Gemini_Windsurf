using Microsoft.Extensions.Logging.Abstractions;
using VanAn.CoreHub.Services;
using VanAn.CoreHub.Tests.TestInfrastructure;
using VanAn.Shared.Domain;
using VanAn.Shared.Domain.Aggregates.SystemSettingAggregate;
using VanAn.Shared.Services;
using Xunit;

namespace VanAn.Core.Tests.Services;

/// <summary>
/// W2 accounting engine: IsEnabledAsync fallback cho ops-managed flags
/// (AccrualReceivableJE / AccountMappingEnabled) qua SystemSetting key
/// "ShopFeature:{tenantId}:{toggleName}" — default OFF khi không có row.
/// </summary>
public class ShopFeatureSettingsServiceTests
{
    private static ShopFeatureSettingsService BuildService(TestContextScope scope)
        => new(scope.Context, NullLogger<ShopFeatureSettingsService>.Instance);

    [Fact]
    public async Task IsEnabledAsync_UnknownToggle_DefaultsFalse_WhenNoSystemSettingRow()
    {
        using var scope = VanAnDbContextTestFactory.Create();
        var svc = BuildService(scope);

        Assert.False(await svc.IsEnabledAsync(Guid.NewGuid(), "AccountMappingEnabled"));
    }

    [Fact]
    public async Task IsEnabledAsync_UnknownToggle_True_WhenSystemSettingRow()
    {
        using var scope = VanAnDbContextTestFactory.Create();
        Guid tenantId = Guid.NewGuid();
        scope.Context.SystemSettings.Add(
            new SystemSetting(new TenantId(Guid.Empty), $"ShopFeature:{tenantId}:AccountMappingEnabled", "true"));
        await scope.Context.SaveChangesAsync();
        var svc = BuildService(scope);

        Assert.True(await svc.IsEnabledAsync(tenantId, "AccountMappingEnabled"));
    }

    [Fact]
    public async Task IsEnabledAsync_UnknownToggle_ScopedPerTenant()
    {
        using var scope = VanAnDbContextTestFactory.Create();
        Guid tenantId = Guid.NewGuid();
        scope.Context.SystemSettings.Add(
            new SystemSetting(new TenantId(Guid.Empty), $"ShopFeature:{tenantId}:AccrualReceivableJE", "true"));
        await scope.Context.SaveChangesAsync();
        var svc = BuildService(scope);

        Assert.True(await svc.IsEnabledAsync(tenantId, "AccrualReceivableJE"));
        Assert.False(await svc.IsEnabledAsync(Guid.NewGuid(), "AccrualReceivableJE"));
    }

    [Fact]
    public async Task IsEnabledAsync_KnownToggle_StillReadsSettingsEntity_NotSystemSetting()
    {
        using var scope = VanAnDbContextTestFactory.Create();
        var svc = BuildService(scope);

        // Accounting_Sync_Enabled default = true (DTO default khi chưa có entity row).
        Assert.True(await svc.IsEnabledAsync(Guid.NewGuid(), nameof(ShopFeatureSettingsDto.Accounting_Sync_Enabled)));
    }
}
