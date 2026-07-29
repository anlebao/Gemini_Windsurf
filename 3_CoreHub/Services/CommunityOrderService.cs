using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using VanAn.CoreHub.Infrastructure;
using VanAn.Shared.Domain;
using VanAn.Shared.Domain.Aggregates.TenantAggregate;

namespace VanAn.CoreHub.Services;

/// <summary>
/// CC-S1-T1/T2 (Sprint 1): Community Order Service implementation.
/// Nearby orders (Haversine) + accept (concurrency-safe via DeliveryTask unique constraint).
/// Queries Gateway PG directly (IVanAnDbContext) — cross-tenant (no TenantId filter).
/// </summary>
public class CommunityOrderService(
    IVanAnDbContext dbContext,
    ILogger<CommunityOrderService> logger) : ICommunityOrderService
{
    private readonly IVanAnDbContext _dbContext = dbContext;
    private readonly ILogger<CommunityOrderService> _logger = logger;

    public async Task<List<NearbyOrderDto>> GetNearbyOrdersAsync(double lat, double lng, int radiusKm, Guid shipperId)
    {
        // Query DELIVERY orders, cross-tenant (IgnoreQueryFilters).
        // Status is a value object (OrderStatusId) — filter by Status.Value in-memory (EF can't translate .Value).
        var orders = await _dbContext.Orders
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(o => o.OrderType == "DELIVERY")
            .ToListAsync();

        // Filter by status in-memory (confirmed/ready only)
        var acceptStatuses = new[] { "confirmed", "ready" };
        var candidates = orders.Where(o => acceptStatuses.Contains(o.Status.Value)).ToList();

        // Exclude orders with active DeliveryTask (Assigned/PickedUp/OutForDelivery)
        var activeStatuses = new[] { DeliveryTaskStatus.Assigned, DeliveryTaskStatus.PickedUp, DeliveryTaskStatus.OutForDelivery };
        var orderIds = candidates.Select(o => o.Id).ToList();
        var assignedOrderIds = (await _dbContext.DeliveryTasks
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(dt => orderIds.Contains(dt.OrderId) && activeStatuses.Contains(dt.Status))
            .Select(dt => dt.OrderId)
            .ToListAsync()).ToHashSet();

        candidates = candidates.Where(o => !assignedOrderIds.Contains(o.Id)).ToList();

        // Load tenants for shop info (cross-tenant)
        var tenantIds = candidates.Select(o => o.TenantId).Distinct().ToList();
        var tenants = await _dbContext.Tenants
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(t => tenantIds.Contains(t.Id))
            .ToListAsync();

        var tenantMap = tenants.ToDictionary(t => t.Id, t => t);

        // Calculate Haversine distance + filter by radius
        var result = new List<NearbyOrderDto>();
        foreach (var order in candidates)
        {
            if (!tenantMap.TryGetValue(order.TenantId, out var tenant))
                continue;

            var shopLat = tenant.Settings?.Latitude ?? 0;
            var shopLng = tenant.Settings?.Longitude ?? 0;
            if (shopLat == 0 && shopLng == 0)
                continue; // shop without coordinates — skip

            var distance = CalculateHaversineKm(lat, lng, shopLat, shopLng);
            if (distance > radiusKm)
                continue;

            result.Add(new NearbyOrderDto
            {
                OrderId = order.Id,
                TenantId = order.TenantId.Value,
                ShopName = tenant.Name ?? "Unknown Shop",
                ShopLat = shopLat,
                ShopLng = shopLng,
                DeliveryAddress = order.DeliveryAddress,
                DeliveryLat = order.DeliveryLat,
                DeliveryLng = order.DeliveryLng,
                TotalAmount = order.TotalAmount,
                Status = order.Status.Value,
                DistanceKm = Math.Round(distance, 2)
            });
        }

        // Sort by distance ascending
        return result.OrderBy(r => r.DistanceKm).ToList();
    }

    public async Task<DeliveryTask?> AcceptOrderAsync(Guid orderId, Guid shipperId)
    {
        // Load order (cross-tenant — IgnoreQueryFilters)
        var order = await _dbContext.Orders
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(o => o.Id == orderId);

        if (order == null)
        {
            _logger.LogWarning("AcceptOrder: Order {OrderId} not found", orderId);
            return null;
        }

        // Check status — only confirmed/ready can be accepted
        var acceptStatuses = new[] { "confirmed", "ready" };
        if (!acceptStatuses.Contains(order.Status.Value))
        {
            _logger.LogWarning("AcceptOrder: Order {OrderId} status {Status} not accept-able", orderId, order.Status.Value);
            return null;
        }

        // Check no active DeliveryTask already exists (concurrency safety)
        var activeStatuses = new[] { DeliveryTaskStatus.Assigned, DeliveryTaskStatus.PickedUp, DeliveryTaskStatus.OutForDelivery };
        var existingTask = await _dbContext.DeliveryTasks
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(dt => dt.OrderId == orderId && activeStatuses.Contains(dt.Status));

        if (existingTask != null)
        {
            _logger.LogWarning("AcceptOrder: Order {OrderId} already has active DeliveryTask {TaskId}", orderId, existingTask.Id);
            return null; // 409 Conflict
        }

        // Load tenant for shop coordinates
        var tenant = await _dbContext.Tenants
            .IgnoreQueryFilters()
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.Id == order.TenantId);

        var shopLat = tenant?.Settings?.Latitude ?? 0;
        var shopLng = tenant?.Settings?.Longitude ?? 0;

        // Create DeliveryTask
        var deliveryTask = new DeliveryTask(
            order.TenantId,
            orderId,
            shipperId,
            shopLat,
            shopLng,
            order.DeliveryLat,
            order.DeliveryLng);

        _dbContext.DeliveryTasks.Add(deliveryTask);

        // Set order shipper + delivery location + status → delivering (CC-S1-T0)
        order.AssignShipper(shipperId);
        if (order.DeliveryLat.HasValue && order.DeliveryLng.HasValue)
            order.SetDeliveryLocation(order.DeliveryLat.Value, order.DeliveryLng.Value);
        order.UpdateOrderStatus(new OrderStatusId("delivering"));

        await _dbContext.SaveChangesAsync();

        _logger.LogInformation("AcceptOrder: Order {OrderId} accepted by shipper {ShipperId} → DeliveryTask {TaskId}",
            orderId, shipperId, deliveryTask.Id);

        return deliveryTask;
    }

    /// <summary>
    /// Haversine formula — calculate distance between two lat/lng points in km.
    /// </summary>
    private static double CalculateHaversineKm(double lat1, double lng1, double lat2, double lng2)
    {
        const double R = 6371; // Earth radius km
        var dLat = (lat2 - lat1) * Math.PI / 180;
        var dLng = (lng2 - lng1) * Math.PI / 180;
        var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
                Math.Cos(lat1 * Math.PI / 180) * Math.Cos(lat2 * Math.PI / 180) *
                Math.Sin(dLng / 2) * Math.Sin(dLng / 2);
        var c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
        return R * c;
    }
}
