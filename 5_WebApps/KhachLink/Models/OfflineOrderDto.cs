using VanAn.Shared.Domain;
using System.Text.Json.Serialization;

namespace VanAn.KhachLink.Models;

public class OfflineOrderDto
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;
    
    [JsonPropertyName("customerId")]
    public string CustomerId { get; set; } = string.Empty;
    
    [JsonPropertyName("shopId")]
    public string ShopId { get; set; } = string.Empty;
    
    [JsonPropertyName("items")]
    public List<OfflineOrderItemDto> Items { get; set; } = new();
    
    [JsonPropertyName("totalAmount")]
    public decimal TotalAmount { get; set; }
    
    [JsonPropertyName("status")]
    public string Status { get; set; } = OrderStatusId.Pending.ToString();
    
    [JsonPropertyName("createdAt")]
    public long CreatedAtTimestamp { get; set; }
    
    [JsonPropertyName("syncedAt")]
    public long? SyncedAtTimestamp { get; set; }
    
    [JsonPropertyName("syncAttempts")]
    public int SyncAttempts { get; set; } = 0;
    
    [JsonPropertyName("lastSyncError")]
    public string? LastSyncError { get; set; }
    
    // Validation properties
    [JsonIgnore]
    public DateTime CreatedAt => DateTimeOffset.FromUnixTimeMilliseconds(CreatedAtTimestamp).DateTime;
    
    [JsonIgnore]
    public DateTime? SyncedAt => SyncedAtTimestamp.HasValue 
        ? DateTimeOffset.FromUnixTimeMilliseconds(SyncedAtTimestamp.Value).DateTime 
        : null;
    
    [JsonIgnore]
    public bool IsSynced => SyncedAt.HasValue;
    
    [JsonIgnore]
    public bool CanRetrySync => !IsSynced && SyncAttempts < 3;
    
    public Order ToDomain()
    {
        // Create Order using static factory method
        Guid? customerId = null;
        if (!string.IsNullOrEmpty(CustomerId) && Guid.TryParse(CustomerId, out var parsedCustomerId))
        {
            customerId = parsedCustomerId;
        }
        
        var order = Order.Create(
            Guid.Parse(Id),
            new TenantId(Guid.Parse(ShopId)),
            customerId,
            Items.Select(i => i.ToDomain()).ToList()
        );
        
        // Set status using UpdateOrderStatus
        order.UpdateOrderStatus(new OrderStatusId(Status));
        
        return order;
    }
    
    public static OfflineOrderDto FromDomain(Order order)
    {
        return new OfflineOrderDto
        {
            Id = order.Id.ToString(),
            CustomerId = order.CustomerId?.ToString() ?? string.Empty,
            ShopId = order.TenantId.Value.ToString(),
            Items = order.Items.Select(OfflineOrderItemDto.FromDomain).ToList(),
            TotalAmount = order.TotalAmount,
            Status = order.Status.Value,
            CreatedAtTimestamp = ((DateTimeOffset)order.CreatedAt).ToUnixTimeMilliseconds()
        };
    }
}

public class OfflineOrderItemDto
{
    [JsonPropertyName("productId")]
    public string ProductId { get; set; } = string.Empty;
    
    [JsonPropertyName("productName")]
    public string? ProductName { get; set; }
    
    [JsonPropertyName("quantity")]
    public int Quantity { get; set; }
    
    [JsonPropertyName("unitPrice")]
    public decimal UnitPrice { get; set; }
    
    [JsonPropertyName("totalPrice")]
    public decimal TotalPrice { get; set; }
    
    public OrderItem ToDomain()
    {
        // Create OrderItem using static factory method
        return OrderItem.Create(
            Guid.NewGuid(), // Generate new Id
            new TenantId(Guid.Empty), // Will be set by parent Order
            Guid.Empty, // OrderId will be set by parent Order
            Guid.Parse(ProductId),
            Quantity,
            UnitPrice
        );
    }
    
    public static OfflineOrderItemDto FromDomain(OrderItem item)
    {
        return new OfflineOrderItemDto
        {
            ProductId = item.ProductId.ToString(),
            Quantity = item.Quantity,
            UnitPrice = item.UnitPrice,
            TotalPrice = item.TotalPrice
        };
    }
}
