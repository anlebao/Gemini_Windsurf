using VanAn.Shared.Domain;

namespace VanAn.CoreHub.Services;

/// <summary>
/// CC-S1-T1/T2 (Sprint 1): Community Order Service — nearby orders + accept for shipper flow.
/// Queries Gateway PG (Orders source of truth per Option C). Cross-tenant (shipper sees orders from many shops).
/// </summary>
public interface ICommunityOrderService
{
    /// <summary>
    /// Get DELIVERY orders within radiusKm of shipper's location.
    /// Filters: OrderType=DELIVERY, Status IN (confirmed, ready), no active DeliveryTask.
    /// Cross-tenant (no TenantId filter — shipper sees orders from multiple shops).
    /// </summary>
    Task<List<NearbyOrderDto>> GetNearbyOrdersAsync(double lat, double lng, int radiusKm, Guid shipperId);

    /// <summary>
    /// Accept an order for delivery. Creates DeliveryTask + sets Order.ShipperId + Order.SetDeliveryLocation.
    /// Concurrency-safe: double-accept returns null (caller returns 409 Conflict).
    /// </summary>
    Task<DeliveryTask?> AcceptOrderAsync(Guid orderId, Guid shipperId);
}

/// <summary>DTO for nearby orders list (shipper view).</summary>
public class NearbyOrderDto
{
    public Guid OrderId { get; set; }
    public Guid TenantId { get; set; }
    public string ShopName { get; set; } = string.Empty;
    public double ShopLat { get; set; }
    public double ShopLng { get; set; }
    public string? DeliveryAddress { get; set; }
    public double? DeliveryLat { get; set; }
    public double? DeliveryLng { get; set; }
    public decimal TotalAmount { get; set; }
    public string Status { get; set; } = string.Empty;
    public double DistanceKm { get; set; }
}
