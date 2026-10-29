using Microsoft.EntityFrameworkCore;
using VanAn.CoreHub.Infrastructure;
using VanAn.Shared.Domain;
using VanAn.Shared.Domain.Aggregates.TenantAggregate;
using VanAn.Shared.Domain.Common;
using Tenant = VanAn.Shared.Domain.Aggregates.TenantAggregate.Tenant;

namespace VanAn.Gateway.Services;

/// <summary>
/// Issue #197 (D1=A) — resolves tenant group claims từ PostgreSQL (Gateway = source of
/// truth cho Tenants) để metering check/ingest scope rate-card resolution.
/// Fail-safe: null khi thiếu row/lỗi → Global chain (GĐ-1 behaviour). Cache 60s.
/// </summary>
public sealed class MeteringTenantAttributeResolver(
    IVanAnDbContext db,
    ILogger<MeteringTenantAttributeResolver> logger) : IMeteringTenantAttributeResolver
{
    private static readonly TimeSpan CacheTtl = TimeSpan.FromSeconds(60);
    private readonly Dictionary<Guid, (MeteringTenantAttributes? Value, DateTime ExpiresAtUtc)> _cache = new();

    public async Task<MeteringTenantAttributes?> ResolveAsync(Guid tenantId, CancellationToken ct = default)
    {
        lock (_cache)
        {
            if (_cache.TryGetValue(tenantId, out var hit) && hit.ExpiresAtUtc > DateTime.UtcNow)
                return hit.Value;
        }

        MeteringTenantAttributes? result = null;
        try
        {
            var tenant = await db.Tenants.AsNoTracking()
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(t => t.Id == new TenantId(tenantId), ct);
            if (tenant is { Type: not null })
            {
                var type = tenant.Type.Value.ToString(); // enum names == canonical strings
                var status = tenant.Status switch
                {
                    TenantStatus.Active => MeteringAttributeValues.Verified,
                    TenantStatus.Pending => MeteringAttributeValues.Unverified,
                    _ => null
                };
                result = new MeteringTenantAttributes(type, status);
            }
        }
        catch (Exception ex) when (!ct.IsCancellationRequested)
        {
            logger.LogWarning(ex, "MeteringTenantAttributeResolver: resolve failed for {TenantId}", tenantId);
        }

        lock (_cache) _cache[tenantId] = (result, DateTime.UtcNow + CacheTtl);
        return result;
    }
}