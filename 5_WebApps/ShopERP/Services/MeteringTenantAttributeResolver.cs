using Microsoft.EntityFrameworkCore;
using VanAn.CoreHub.Infrastructure;
using VanAn.Shared.Domain;
using VanAn.Shared.Domain.Aggregates.TenantAggregate;
using VanAn.Shared.Domain.Common;
using Tenant = VanAn.Shared.Domain.Aggregates.TenantAggregate.Tenant;

namespace VanAn.ShopERP.Services;

/// <summary>
/// Issue #197 (D1=A) — resolves tenant group claims (loại hình + trạng thái verify)
/// from the SQLite mirror (TenantSyncSubscriber keeps Type/Status in sync) so metering
/// check/ingest calls can scope rate-card resolution. Fail-safe: null on missing row
/// or transport error → metering falls back to the Global chain (GĐ-1 behaviour).
/// Cached 60s per tenant (claims are slow-moving; cache avoids a query per call).
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
                    _ => null // Suspended/Inactive → no status claim → falls to type/global
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