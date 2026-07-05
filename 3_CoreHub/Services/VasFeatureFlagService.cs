using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using VanAn.CoreHub.Infrastructure;
using VanAn.Shared.Domain;
using VanAn.Shared.Domain.Aggregates.TenantAggregate;
using Tenant = VanAn.Shared.Domain.Aggregates.TenantAggregate.Tenant;

namespace VanAn.CoreHub.Services;

/// <summary>
/// VAS Wave 8 — Feature flag service for VAS module access control.
/// Only Enterprise_* tenants (SME/Large/SuperSmall) can access VAS reports (4 BCTC).
/// HKD tenants are blocked — they use the HKD Book module (S1a-S3a, TT 152).
///
/// D9 decision: VAS module is a separate feature-flagged module per tenant.
/// TenantType field on Tenant entity determines access (set via factory methods).
/// </summary>
public interface IVasFeatureFlagService
{
    /// <summary>Check if the given tenant can access VAS reports (4 BCTC).</summary>
    /// <returns>True if TenantType is Enterprise_* (not HKD); false if HKD or tenant not found.</returns>
    Task<bool> CanAccessVasReportsAsync(TenantId tenantId, CancellationToken ct = default);

    /// <summary>Get the TenantType for a tenant (HKD / Enterprise_* / null if not found).</summary>
    Task<TenantType?> GetTenantTypeAsync(TenantId tenantId, CancellationToken ct = default);

    /// <summary>Check if a tenant is in read-only (Converted) status — HKD historical access.</summary>
    Task<bool> IsReadOnlyAsync(TenantId tenantId, CancellationToken ct = default);
}

/// <summary>
/// VAS Wave 8 — Feature flag service implementation.
/// Queries Tenant aggregate from IVanAnDbContext to determine VAS access.
/// </summary>
public class VasFeatureFlagService(IVanAnDbContext dbContext, ILogger<VasFeatureFlagService> logger) : IVasFeatureFlagService
{
    private readonly IVanAnDbContext _dbContext = dbContext;
    private readonly ILogger<VasFeatureFlagService> _logger = logger;

    /// <inheritdoc />
    public async Task<bool> CanAccessVasReportsAsync(TenantId tenantId, CancellationToken ct = default)
    {
        TenantType? type = await GetTenantTypeAsync(tenantId, ct).ConfigureAwait(false);
        if (type is null)
        {
            _logger.LogWarning("VAS feature flag: tenant {TenantId} not found or Type is null — access denied", tenantId.Value);
            return false;
        }

        bool canAccess = type != TenantType.HKD;
        _logger.LogDebug("VAS feature flag: tenant {TenantId} Type={Type} → CanAccess={CanAccess}", tenantId.Value, type, canAccess);
        return canAccess;
    }

    /// <inheritdoc />
    public async Task<TenantType?> GetTenantTypeAsync(TenantId tenantId, CancellationToken ct = default)
    {
        Tenant? tenant = await _dbContext.Tenants
            .AsNoTracking()
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(t => t.Id == tenantId, ct)
            .ConfigureAwait(false);
        return tenant?.Type;
    }

    /// <inheritdoc />
    public async Task<bool> IsReadOnlyAsync(TenantId tenantId, CancellationToken ct = default)
    {
        Tenant? tenant = await _dbContext.Tenants
            .AsNoTracking()
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(t => t.Id == tenantId, ct)
            .ConfigureAwait(false);
        return tenant?.IsConverted() ?? false;
    }
}
