using Microsoft.EntityFrameworkCore;
using VanAn.CoreHub.Infrastructure;
using VanAn.Shared.Domain;
using VanAn.Shared.Domain.Aggregates.TenantAggregate;
using VanAn.Shared.Services;

namespace VanAn.Gateway.Services
{
    /// <summary>Một dòng trong "Cửa hàng của tôi" — tenant khách đã mua/tích điểm.</summary>
    public sealed record MyStoreDto(
        Guid TenantId,
        string Name,
        string? Slug,
        string? LogoUrl,
        int OrderCount,
        DateTime? LastOrderAt,
        decimal TotalSpent,
        int PointBalance,
        /// <summary>"Alliance" khi điểm lấy từ AllianceWallet breakdown; ngược lại "Silo".</summary>
        string Mode);

    /// <summary>
    /// "Cửa hàng của tôi" (2026-11-28): union 3 nguồn PG theo customer deviceId —
    /// ① Orders.CustomerDeviceId (cover cả guest checkout) ② Customers.DeviceId →
    /// LoyaltyRewards (Silo points/tenant — PG là write source qua LoyaltyPointLedgerService)
    /// ③ AllianceWallet → GetTenantBalancesAsync (Alliance breakdown).
    /// Rules: cancelled loại khỏi count/spent; tenant chỉ-cancelled chỉ hiện khi còn điểm;
    /// merge alliance-wins; sắp xếp LastOrderAt desc; cap 50. Pure-read — không mutation.
    /// </summary>
    public interface IMyStoresService
    {
        Task<IReadOnlyList<MyStoreDto>> GetMyStoresAsync(Guid deviceId, CancellationToken ct = default);
    }

    public class MyStoresService(
        IVanAnDbContext dbContext,
        IAllianceWalletService allianceWallet,
        ITenantSearchService tenantSearch,
        ILogger<MyStoresService> logger) : IMyStoresService
    {
        private const int MaxStores = 50;

        public async Task<IReadOnlyList<MyStoreDto>> GetMyStoresAsync(Guid deviceId, CancellationToken ct = default)
        {
            // ── ① Orders theo device (guest + logged — CustomerDeviceId luôn ghi) ──
            var did = deviceId.ToString().ToLowerInvariant();
            var orders = await dbContext.Orders
                .IgnoreQueryFilters() // cross-tenant — device là phạm vi, không phải tenant
                .AsNoTracking()
                .Where(o => o.CustomerDeviceId != null && o.CustomerDeviceId.ToLower() == did)
                .Select(o => new { o.TenantId, o.Status, o.TotalAmount, o.OrderDate })
                .ToListAsync(ct);

            var ordersByTenant = orders
                .GroupBy(o => o.TenantId.Value)
                .ToDictionary(g => g.Key, g =>
                {
                    var valid = g.Where(o => o.Status != OrderStatusId.Cancelled).ToList();
                    return (OrderCount: valid.Count,
                            LastOrderAt: (DateTime?)g.Max(o => o.OrderDate),
                            TotalSpent: valid.Sum(o => o.TotalAmount));
                });

            // ── ② Silo: Customers.DeviceId → LoyaltyRewards.PointBalance/tenant ──
            var customerRows = await dbContext.Customers
                .IgnoreQueryFilters()
                .AsNoTracking()
                .Where(c => c.DeviceId == deviceId)
                .Select(c => new { c.Id, c.TenantId })
                .ToListAsync(ct);

            var siloByTenant = new Dictionary<Guid, int>();
            if (customerRows.Count > 0)
            {
                var customerIds = customerRows.Select(c => c.Id).ToList();
                var rewards = await dbContext.LoyaltyRewards
                    .IgnoreQueryFilters()
                    .AsNoTracking()
                    .Where(r => customerIds.Contains(r.CustomerId))
                    .Select(r => new { r.TenantId, r.PointBalance })
                    .ToListAsync(ct);
                siloByTenant = rewards
                    .GroupBy(r => r.TenantId.Value)
                    .ToDictionary(g => g.Key, g => g.Sum(r => r.PointBalance));
            }

            // ── ③ Alliance: wallet → per-tenant net (fail-open — Silo vẫn trả) ──
            var allianceByTenant = new Dictionary<Guid, int>();
            try
            {
                var wallet = await allianceWallet.GetWalletByDeviceIdAsync(deviceId);
                if (wallet != null)
                {
                    foreach (var b in await allianceWallet.GetTenantBalancesAsync(wallet.Id))
                        allianceByTenant[b.TenantId] = b.NetPoints;
                }
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "MyStores: alliance breakdown lỗi cho device {DeviceId} — trả Silo-only", deviceId);
            }

            // ── Union + enrich tenant info (batch — anti-drift W3-2d) ──
            var tenantIds = ordersByTenant.Keys
                .Union(siloByTenant.Keys)
                .Union(allianceByTenant.Keys)
                .ToList();
            if (tenantIds.Count == 0)
                return [];

            var infoMap = await tenantSearch.GetStoreInfoMapAsync(tenantIds, ct);

            return tenantIds
                .Select(tid =>
                {
                    ordersByTenant.TryGetValue(tid, out var o);
                    // Alliance-wins: tenant có trong breakdown → điểm lấy net alliance
                    var isAlliance = allianceByTenant.TryGetValue(tid, out var net);
                    var points = isAlliance ? net : siloByTenant.GetValueOrDefault(tid);
                    return infoMap.TryGetValue(tid, out var info)
                        ? new MyStoreDto(tid, info.Name, info.Slug, info.LogoUrl,
                            o.OrderCount, o.LastOrderAt, o.TotalSpent,
                            Math.Max(0, points), isAlliance ? "Alliance" : "Silo")
                        : null; // tenant đã xóa/inactive → bỏ
                })
                .Where(s => s != null && (s.OrderCount > 0 || s.PointBalance > 0))
                .OrderByDescending(s => s!.LastOrderAt ?? DateTime.MinValue)
                .ThenBy(s => s!.Name)
                .Take(MaxStores)
                .Cast<MyStoreDto>()
                .ToList();
        }
    }
}
