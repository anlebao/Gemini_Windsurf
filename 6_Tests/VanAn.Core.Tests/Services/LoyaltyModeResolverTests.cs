using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using VanAn.CoreHub.Infrastructure;
using VanAn.CoreHub.Services;
using VanAn.CoreHub.Tests.TestInfrastructure;
using VanAn.Shared.Domain;
using VanAn.Shared.Services;
using Xunit;

namespace VanAn.Tests.Services;

/// <summary>
/// Loyalty Alliance Phase 2A — unit tests for LoyaltyModeResolver.
/// Uses SQLite in-memory via VanAnDbContextTestFactory.
/// Spec: docs/specs/loyalty-alliance-spec.md v1.0.
/// </summary>
public class LoyaltyModeResolverTests : IDisposable
{
    private readonly TestContextScope _scope;
    private readonly VanAnDbContext _db;
    private readonly LoyaltyModeResolver _sut;

    public LoyaltyModeResolverTests()
    {
        _scope = VanAnDbContextTestFactory.Create();
        _db = _scope.Context;
        _sut = new LoyaltyModeResolver(_db, NullLogger<LoyaltyModeResolver>.Instance);
    }

    public void Dispose() => _scope.Dispose();

    // ──────────────────────────────────────────────────────────
    // Helper: seed global config
    // ──────────────────────────────────────────────────────────

    private async Task<LoyaltyGlobalConfig> AddGlobalAsync(LoyaltyMode mode, int maxWallet = 100_000)
    {
        var cfg = new LoyaltyGlobalConfig();
        cfg.UpdateMode(mode, "test");
        // UpdateMode does not set MaxWalletPoints — use reflection-free path: direct add then update limits
        cfg.UpdateLimits(maxPointsPerOrder: 30, maxWalletPoints: maxWallet, changedBy: "test");
        _ = _db.LoyaltyGlobalConfigs.Add(cfg);
        await _db.SaveChangesAsync();
        return cfg;
    }

    private async Task<LoyaltyTenantConfig> AddTenantConfigAsync(
        Guid tenantId, LoyaltyMode? mode, bool isMember, int? maxWallet = null)
    {
        var tenantIdValue = new TenantId(tenantId);
        var cfg = new LoyaltyTenantConfig(tenantIdValue);
        cfg.SetMode(mode, "test");
        cfg.SetAllianceMembership(isMember, "test");
        if (maxWallet is not null)
        {
            cfg.SetMaxWalletPoints(maxWallet, "test");
        }
        // LoyaltyTenantConfig is IMustHaveTenant → query filter would hide the row
        // when the active tenant context doesn't match. Use IgnoreQueryFilters for seeding.
        _ = _db.LoyaltyTenantConfigs.Add(cfg);
        await _db.SaveChangesAsync();
        return cfg;
    }

    // ──────────────────────────────────────────────────────────
    // GetEffectiveModeAsync
    // ──────────────────────────────────────────────────────────

    [Fact(DisplayName = "LA-MR-1: GetEffectiveMode — tenant override returns tenant mode")]
    public async Task GetEffectiveMode_TenantOverride_ReturnsTenantMode()
    {
        var tenantId = Guid.NewGuid();
        await AddGlobalAsync(LoyaltyMode.Silo);
        await AddTenantConfigAsync(tenantId, LoyaltyMode.Alliance, isMember: true);

        LoyaltyMode mode = await _sut.GetEffectiveModeAsync(tenantId);

        mode.Should().Be(LoyaltyMode.Alliance);
    }

    [Fact(DisplayName = "LA-MR-2: GetEffectiveMode — no override returns global mode")]
    public async Task GetEffectiveMode_NoOverride_ReturnsGlobalMode()
    {
        var tenantId = Guid.NewGuid();
        await AddGlobalAsync(LoyaltyMode.Alliance);

        LoyaltyMode mode = await _sut.GetEffectiveModeAsync(tenantId);

        mode.Should().Be(LoyaltyMode.Alliance);
    }

    [Fact(DisplayName = "LA-MR-3: GetEffectiveMode — tenant opt-out (IsAllianceMember=false) forces Silo even if Mode=Alliance")]
    public async Task GetEffectiveMode_TenantOptOut_ForcesSilo()
    {
        var tenantId = Guid.NewGuid();
        await AddGlobalAsync(LoyaltyMode.Alliance);
        await AddTenantConfigAsync(tenantId, LoyaltyMode.Alliance, isMember: false);

        LoyaltyMode mode = await _sut.GetEffectiveModeAsync(tenantId);

        mode.Should().Be(LoyaltyMode.Silo, "Q2: full opt-out forces Silo regardless of Mode override");
    }

    // ──────────────────────────────────────────────────────────
    // GetEffectiveMaxWalletPointsAsync
    // ──────────────────────────────────────────────────────────

    [Fact(DisplayName = "LA-MR-4: GetEffectiveMaxWalletPoints — tenant override returns tenant value")]
    public async Task GetEffectiveMaxWalletPoints_TenantOverride_ReturnsTenantValue()
    {
        var tenantId = Guid.NewGuid();
        await AddGlobalAsync(LoyaltyMode.Silo, maxWallet: 100_000);
        await AddTenantConfigAsync(tenantId, mode: null, isMember: true, maxWallet: 50_000);

        int max = await _sut.GetEffectiveMaxWalletPointsAsync(tenantId);

        max.Should().Be(50_000);
    }

    [Fact(DisplayName = "LA-MR-5: GetEffectiveMaxWalletPoints — no override returns global value")]
    public async Task GetEffectiveMaxWalletPoints_NoOverride_ReturnsGlobalValue()
    {
        var tenantId = Guid.NewGuid();
        await AddGlobalAsync(LoyaltyMode.Silo, maxWallet: 200_000);

        int max = await _sut.GetEffectiveMaxWalletPointsAsync(tenantId);

        max.Should().Be(200_000);
    }

    // ──────────────────────────────────────────────────────────
    // IsAllianceMemberAsync
    // ──────────────────────────────────────────────────────────

    [Fact(DisplayName = "LA-MR-6: IsAllianceMember — no config returns false")]
    public async Task IsAllianceMember_NoConfig_ReturnsFalse()
    {
        var tenantId = Guid.NewGuid();
        await AddGlobalAsync(LoyaltyMode.Alliance);

        bool isMember = await _sut.IsAllianceMemberAsync(tenantId);

        isMember.Should().BeFalse();
    }

    [Fact(DisplayName = "LA-MR-7: IsAllianceMember — config with IsAllianceMember=true returns true")]
    public async Task IsAllianceMember_ConfigTrue_ReturnsTrue()
    {
        var tenantId = Guid.NewGuid();
        await AddTenantConfigAsync(tenantId, mode: null, isMember: true);

        bool isMember = await _sut.IsAllianceMemberAsync(tenantId);

        isMember.Should().BeTrue();
    }
}
