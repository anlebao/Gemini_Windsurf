using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using VanAn.CoreHub.Infrastructure;
using VanAn.CoreHub.Infrastructure.Seed;
using VanAn.CoreHub.Services;
using VanAn.CoreHub.Tests.TestInfrastructure;
using VanAn.Shared.Domain;
using VanAn.Shared.Domain.Aggregates.TenantAggregate;
using Xunit;
using Tenant = VanAn.Shared.Domain.Aggregates.TenantAggregate.Tenant;

namespace VanAn.Core.Tests.Services;

/// <summary>
/// VAS Wave 8 — Feature flag + Tenant Conversion tests.
/// Verifies: (1) HKD tenants blocked from VAS reports, (2) Enterprise tenants allowed,
/// (3) D9 conversion: HKD→DN creates new tenant + links + marks HKD Converted.
/// </summary>
[Trait("Category", "VASWave8")]
public class VasFeatureFlagTests
{
    private async Task<(VanAnDbContext db, VasFeatureFlagService flagSvc, TenantConversionService convSvc)> SetupAsync()
    {
        TestContextScope scope = VanAnDbContextTestFactory.Create();
        scope.TenantProvider?.SetTenant(VasSampleDataSeeder.VasEnterpriseTenantGuid);
        VanAnDbContext db = scope.Context;
        _ = await AccountChartSeeder.SeedAsync(db, NullLogger.Instance);
        _ = await VasSampleDataSeeder.SeedAsync(db);
        var mapper = new HkdToEnterpriseAccountMapper();
        return (
            db,
            new VasFeatureFlagService(db, NullLogger<VasFeatureFlagService>.Instance),
            new TenantConversionService(db, db, mapper, NullLogger<TenantConversionService>.Instance)
        );
    }

    private async Task<(VanAnDbContext db, VasFeatureFlagService flagSvc, TenantConversionService convSvc, TenantId hkdTenantId)> SetupWithHkdAsync()
    {
        TestContextScope scope = VanAnDbContextTestFactory.Create();
        VanAnDbContext db = scope.Context;
        _ = await AccountChartSeeder.SeedAsync(db, NullLogger.Instance);
        _ = await VasSampleDataSeeder.SeedAsync(db);

        // Create an HKD tenant
        var hkdId = new TenantId(Guid.NewGuid());
        var hkdTenant = Tenant.CreateHouseholdBusiness(hkdId, "Test HKD", HKDGroup.Group1);
        db.Tenants.Add(hkdTenant);
        await db.SaveChangesAsync();

        var mapper = new HkdToEnterpriseAccountMapper();
        return (
            db,
            new VasFeatureFlagService(db, NullLogger<VasFeatureFlagService>.Instance),
            new TenantConversionService(db, db, mapper, NullLogger<TenantConversionService>.Instance),
            hkdId
        );
    }

    // ── Feature Flag Tests ────────────────────────────────────────────────

    // W8-FF1: Enterprise tenant (VasSampleDataSeeder) → CanAccessVasReports == true.
    [Fact]
    public async Task W8_FF1_EnterpriseTenant_CanAccessVasReports()
    {
        var (_, flagSvc, _) = await SetupAsync();
        bool canAccess = await flagSvc.CanAccessVasReportsAsync(VasSampleDataSeeder.VasEnterpriseTenantId);
        Assert.True(canAccess, "Enterprise tenant should be able to access VAS reports");
    }

    // W8-FF2: HKD tenant → CanAccessVasReports == false.
    [Fact]
    public async Task W8_FF2_HkdTenant_CannotAccessVasReports()
    {
        var (_, flagSvc, _, hkdId) = await SetupWithHkdAsync();
        bool canAccess = await flagSvc.CanAccessVasReportsAsync(hkdId);
        Assert.False(canAccess, "HKD tenant should NOT be able to access VAS reports");
    }

    // W8-FF3: Non-existent tenant → CanAccessVasReports == false (safe default).
    [Fact]
    public async Task W8_FF3_NonExistentTenant_CannotAccessVasReports()
    {
        var (_, flagSvc, _) = await SetupAsync();
        bool canAccess = await flagSvc.CanAccessVasReportsAsync(new TenantId(Guid.NewGuid()));
        Assert.False(canAccess, "Non-existent tenant should NOT be able to access VAS reports");
    }

    // W8-FF4: GetTenantType returns Enterprise_SME for VasSampleDataSeeder tenant.
    [Fact]
    public async Task W8_FF4_GetTenantType_EnterpriseSme_ForSeededTenant()
    {
        var (_, flagSvc, _) = await SetupAsync();
        TenantType? type = await flagSvc.GetTenantTypeAsync(VasSampleDataSeeder.VasEnterpriseTenantId);
        Assert.NotNull(type);
        Assert.NotEqual(TenantType.HKD, type);
    }

    // W8-FF5: GetTenantType returns HKD for HKD tenant.
    [Fact]
    public async Task W8_FF5_GetTenantType_Hkd_ForHkdTenant()
    {
        var (_, flagSvc, _, hkdId) = await SetupWithHkdAsync();
        TenantType? type = await flagSvc.GetTenantTypeAsync(hkdId);
        Assert.Equal(TenantType.HKD, type);
    }

    // W8-FF6: IsReadOnly == false for Active tenant.
    [Fact]
    public async Task W8_FF6_IsReadOnly_False_ForActiveTenant()
    {
        var (_, flagSvc, _) = await SetupAsync();
        bool isReadOnly = await flagSvc.IsReadOnlyAsync(VasSampleDataSeeder.VasEnterpriseTenantId);
        Assert.False(isReadOnly, "Active tenant should not be read-only");
    }

    // ── Tenant Conversion Tests (D9) ──────────────────────────────────────

    // W8-CONV1: ConvertHkdToEnterprise creates new DN tenant with PredecessorTenantId link.
    [Fact]
    public async Task W8_CONV1_ConvertHkdToEnterprise_CreatesNewTenantWithPredecessorLink()
    {
        var (db, _, convSvc, hkdId) = await SetupWithHkdAsync();

        Tenant newDn = await convSvc.ConvertHkdToEnterpriseAsync(
            hkdId.Value,
            TenantType.Enterprise_SME,
            AccountingStandard.TT133_2016,
            "Converted DN Co.");

        Assert.NotEqual(hkdId, newDn.Id);
        Assert.Equal(hkdId, newDn.PredecessorTenantId);
        Assert.Equal(TenantType.Enterprise_SME, newDn.Type);
        Assert.Equal(AccountingStandard.TT133_2016, newDn.AccountingStandard);
        Assert.Equal(TenantStatus.Active, newDn.Status);
    }

    // W8-CONV2: After conversion, HKD tenant has Status=Converted + SuccessorTenantId set.
    [Fact]
    public async Task W8_CONV2_AfterConversion_HkdMarkedConvertedWithSuccessorLink()
    {
        var (db, _, convSvc, hkdId) = await SetupWithHkdAsync();

        Tenant newDn = await convSvc.ConvertHkdToEnterpriseAsync(
            hkdId.Value,
            TenantType.Enterprise_SME,
            AccountingStandard.TT133_2016,
            "Converted DN Co.");

        Tenant? hkdAfter = await db.Tenants.IgnoreQueryFilters().FirstOrDefaultAsync(t => t.Id == hkdId);
        Assert.NotNull(hkdAfter);
        Assert.Equal(TenantStatus.Converted, hkdAfter!.Status);
        Assert.Equal(newDn.Id, hkdAfter.SuccessorTenantId);
        Assert.NotNull(hkdAfter.ConvertedAt);
    }

    // W8-CONV3: GetPredecessorAsync returns HKD tenant from DN tenant.
    [Fact]
    public async Task W8_CONV3_GetPredecessor_ReturnsHkdFromDn()
    {
        var (_, _, convSvc, hkdId) = await SetupWithHkdAsync();

        Tenant newDn = await convSvc.ConvertHkdToEnterpriseAsync(
            hkdId.Value,
            TenantType.Enterprise_SME,
            AccountingStandard.TT133_2016,
            "Converted DN Co.");

        Tenant? predecessor = await convSvc.GetPredecessorAsync(newDn.Id.Value);
        Assert.NotNull(predecessor);
        Assert.Equal(hkdId, predecessor!.Id);
    }

    // W8-CONV4: GetSuccessorAsync returns DN tenant from HKD tenant.
    [Fact]
    public async Task W8_CONV4_GetSuccessor_ReturnsDnFromHkd()
    {
        var (_, _, convSvc, hkdId) = await SetupWithHkdAsync();

        Tenant newDn = await convSvc.ConvertHkdToEnterpriseAsync(
            hkdId.Value,
            TenantType.Enterprise_SME,
            AccountingStandard.TT133_2016,
            "Converted DN Co.");

        Tenant? successor = await convSvc.GetSuccessorAsync(hkdId.Value);
        Assert.NotNull(successor);
        Assert.Equal(newDn.Id, successor!.Id);
    }

    // W8-CONV5: Cannot convert an already-converted HKD tenant.
    [Fact]
    public async Task W8_CONV5_CannotConvert_AlreadyConvertedHkd_Throws()
    {
        var (_, _, convSvc, hkdId) = await SetupWithHkdAsync();

        // First conversion
        _ = await convSvc.ConvertHkdToEnterpriseAsync(
            hkdId.Value,
            TenantType.Enterprise_SME,
            AccountingStandard.TT133_2016,
            "First DN Co.");

        // Second conversion should throw
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            convSvc.ConvertHkdToEnterpriseAsync(
                hkdId.Value,
                TenantType.Enterprise_SME,
                AccountingStandard.TT133_2016,
                "Second DN Co."));
    }

    // W8-CONV6: Cannot convert with TenantType.HKD as target.
    [Fact]
    public async Task W8_CONV6_CannotConvert_ToHkdType_Throws()
    {
        var (_, _, convSvc, hkdId) = await SetupWithHkdAsync();

        await Assert.ThrowsAsync<ArgumentException>(() =>
            convSvc.ConvertHkdToEnterpriseAsync(
                hkdId.Value,
                TenantType.HKD,
                AccountingStandard.TT133_2016,
                "Invalid HKD target"));
    }

    // W8-CONV7: Cannot convert non-existent tenant.
    [Fact]
    public async Task W8_CONV7_CannotConvert_NonExistentTenant_Throws()
    {
        var (_, _, convSvc, _) = await SetupWithHkdAsync();

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            convSvc.ConvertHkdToEnterpriseAsync(
                Guid.NewGuid(),
                TenantType.Enterprise_SME,
                AccountingStandard.TT133_2016,
                "Non-existent HKD"));
    }

    // W8-CONV8: After conversion, converted HKD is read-only (IsReadOnly == true).
    [Fact]
    public async Task W8_CONV8_AfterConversion_HkdIsReadOnly()
    {
        var (_, flagSvc, convSvc, hkdId) = await SetupWithHkdAsync();

        _ = await convSvc.ConvertHkdToEnterpriseAsync(
            hkdId.Value,
            TenantType.Enterprise_SME,
            AccountingStandard.TT133_2016,
            "Converted DN Co.");

        bool isReadOnly = await flagSvc.IsReadOnlyAsync(hkdId);
        Assert.True(isReadOnly, "Converted HKD tenant should be read-only");
    }

    // W8-CONV9: MigrateOpeningBalance returns summary (best-effort mapping).
    [Fact]
    public async Task W8_CONV9_MigrateOpeningBalance_ReturnsSummary()
    {
        var (db, _, convSvc, hkdId) = await SetupWithHkdAsync();

        // Add some HKD accounting entries
        var entry = AccountingEntry.CreateRevenue(
            hkdId,
            new AccountingPeriod(2026, 6),
            new Money(10_000_000m),
            "Test revenue",
            accountCode: "Revenue");
        db.AccountingEntries.Add(entry);
        await db.SaveChangesAsync();

        var (mappingsCount, totalDebit, totalCredit) = await convSvc.MigrateOpeningBalanceAsync(
            hkdId.Value,
            Guid.NewGuid(),
            AccountingStandard.TT133_2016);

        Assert.True(mappingsCount >= 1, $"Expected >= 1 mapping, got {mappingsCount}");
        Assert.True(totalDebit + totalCredit > 0, "Expected non-zero balance");
    }
}
