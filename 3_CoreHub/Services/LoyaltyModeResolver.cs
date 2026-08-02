using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using VanAn.CoreHub.Infrastructure;
using VanAn.Shared.Domain;
using VanAn.Shared.Services;

namespace VanAn.CoreHub.Services;

/// <summary>
/// Resolves effective loyalty mode + limits per tenant.
/// Tenant override (non-null) wins; otherwise global config is used.
/// A tenant with IsAllianceMember=false is forced to Silo (Q2 full opt-out).
/// </summary>
public class LoyaltyModeResolver(
    IVanAnDbContext dbContext,
    ILogger<LoyaltyModeResolver> logger) : ILoyaltyModeResolver
{
    private readonly IVanAnDbContext _dbContext = dbContext;
    private readonly ILogger<LoyaltyModeResolver> _logger = logger;

    /// <inheritdoc/>
    public async Task<LoyaltyMode> GetEffectiveModeAsync(Guid tenantId)
    {
        LoyaltyTenantConfig? tenantCfg = await QueryTenantConfigAsync(tenantId);

        // Q2: full opt-out — IsAllianceMember=false forces Silo regardless of Mode override
        if (tenantCfg is not null && !tenantCfg.IsAllianceMember)
        {
            return LoyaltyMode.Silo;
        }

        if (tenantCfg is not null && tenantCfg.Mode is not null)
        {
            return tenantCfg.Mode.Value;
        }

        LoyaltyGlobalConfig globalCfg = await GetOrCreateGlobalConfigAsync();
        return globalCfg.Mode;
    }

    /// <inheritdoc/>
    public async Task<int> GetEffectiveMaxWalletPointsAsync(Guid tenantId)
    {
        LoyaltyTenantConfig? tenantCfg = await QueryTenantConfigAsync(tenantId);

        if (tenantCfg is not null && tenantCfg.MaxWalletPoints is not null)
        {
            return tenantCfg.MaxWalletPoints.Value;
        }

        LoyaltyGlobalConfig globalCfg = await GetOrCreateGlobalConfigAsync();
        return globalCfg.MaxWalletPoints;
    }

    /// <inheritdoc/>
    public async Task<bool> IsAllianceMemberAsync(Guid tenantId)
    {
        LoyaltyTenantConfig? tenantCfg = await QueryTenantConfigAsync(tenantId);
        return tenantCfg is not null && tenantCfg.IsAllianceMember;
    }

    // ──────────────────────────────────────────────────────────
    // Helpers
    // ──────────────────────────────────────────────────────────

    /// <summary>
    /// Query a tenant's LoyaltyTenantConfig by explicit tenantId.
    /// Uses IgnoreQueryFilters because this is a cross-tenant lookup by design
    /// (the caller asks "what is the config for tenant X" — not "what is visible
    /// in my current tenant scope"). The unique index on TenantId ensures
    /// at most one row is returned.
    /// </summary>
    private Task<LoyaltyTenantConfig?> QueryTenantConfigAsync(Guid tenantId)
    {
        var tenantIdValue = new TenantId(tenantId);
        return _dbContext.LoyaltyTenantConfigs
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(c => c.TenantId == tenantIdValue);
    }

    // ──────────────────────────────────────────────────────────
    // Helpers
    // ──────────────────────────────────────────────────────────

    private async Task<LoyaltyGlobalConfig> GetOrCreateGlobalConfigAsync()
    {
        LoyaltyGlobalConfig? cfg = await _dbContext.LoyaltyGlobalConfigs.FirstOrDefaultAsync();
        if (cfg is not null)
        {
            return cfg;
        }

        // No global config row yet — create default (Silo, 100k cap).
        // This branch is rare; the SystemAdmin API (Phase 3A) normally seeds the row.
        cfg = new LoyaltyGlobalConfig();
        _ = _dbContext.LoyaltyGlobalConfigs.Add(cfg);
        await _dbContext.SaveChangesAsync();
        _logger.LogInformation("Seeded default LoyaltyGlobalConfig (Silo, MaxWalletPoints=100000)");
        return cfg;
    }
}
