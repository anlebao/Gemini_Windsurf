using VanAn.Shared.Domain;

namespace VanAn.CoreHub.Services;

public class OrderDashboardData
{
    public int TodayOrderCount { get; set; }
    public decimal TodayRevenue { get; set; }
    public int PendingOrders { get; set; }
    public int ProcessingOrders { get; set; }
    public int CompletedOrders { get; set; }
}

public class OrderSummary
{
    public Guid OrderId { get; set; }
    public string CustomerId { get; set; } = string.Empty;
    public OrderStatusId Status { get; set; }
    public DateTime CreatedAt { get; set; }
    public decimal TotalAmount { get; set; }
    public int ItemCount { get; set; }
    public List<OrderItemSummary> Items { get; set; } = new();
}

public class OrderItemSummary
{
    public Guid ProductId { get; set; }
    public int Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal TotalPrice { get; set; }
}
