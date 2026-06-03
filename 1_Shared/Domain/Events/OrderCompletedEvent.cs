namespace VanAn.Shared.Domain.Events;

/// <summary>
/// Event data for OrderCompleted events
/// Published when an order is completed for accounting processing
/// </summary>
public sealed record OrderCompletedEvent
{
    public Guid EventId { get; init; }
    public Guid OrderId { get; init; }
    public Guid? CustomerId { get; init; }
    public string CustomerDeviceId { get; init; } = string.Empty;
    public TenantId TenantId { get; init; }
    public decimal TotalAmount { get; init; }
    public List<OrderItemEvent> Items { get; init; } = new();
    public decimal SubTotal { get; init; }
    public decimal TotalVatAmount { get; init; }
    public DateTime CompletedAt { get; init; }
    public string? TrackingCode { get; init; }
}

/// <summary>
/// Order item data for OrderCompleted events
/// </summary>
public sealed record OrderItemEvent
{
    public Guid ProductId { get; init; }
    public string ProductName { get; init; } = string.Empty;
    public int Quantity { get; init; }
    public decimal UnitPrice { get; init; }
    public decimal TotalAmount { get; init; }
}
