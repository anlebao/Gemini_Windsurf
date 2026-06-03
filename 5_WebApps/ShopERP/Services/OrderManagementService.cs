using Microsoft.AspNetCore.Http;
using VanAn.Shared.Domain;
using VanAn.CoreHub.Services;

namespace VanAn.ShopERP.Services;

public interface IOrderManagementService
{
    Task<List<Order>> GetOrdersAsync(OrderStatusId? status = null);
    Task<Order?> GetOrderAsync(Guid orderId);
    Task<bool> UpdateOrderStatusAsync(Guid orderId, OrderStatusId newStatus, string? reason = null);
    Task<List<Order>> GetOrdersByDateRangeAsync(DateTime startDate, DateTime endDate);
    Task<OrderMetrics> GetOrderMetricsAsync();
    Task<bool> AssignOrderToStaffAsync(Guid orderId, Guid staffId);
    Task<List<Order>> GetOrdersByCustomerAsync(string customerId);
    Task<bool> CancelOrderAsync(Guid orderId, string reason);
    Task<OrderSummary> GetOrderSummaryAsync(Guid orderId);
    Task<OrderDashboardData> GetDashboardDataAsync(Guid tenantId);
}

public class OrderMetrics
{
    public int TotalOrders { get; set; }
    public int PendingOrders { get; set; }
    public int ProcessingOrders { get; set; }
    public int CompletedOrders { get; set; }
    public int CancelledOrders { get; set; }
    public decimal TotalRevenue { get; set; }
    public decimal AverageOrderValue { get; set; }
    public int OrdersPerHour { get; set; }
    public decimal RevenuePerHour { get; set; }
    public List<StatusCount> StatusBreakdown { get; set; } = new();
}

public class StatusCount
{
    public OrderStatusId Status { get; set; }
    public int Count { get; set; }
    public decimal Percentage { get; set; }
}

public class OrderSummary
{
    public Guid OrderId { get; set; }
    public string CustomerId { get; set; } = string.Empty;
    public OrderStatusId Status { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public decimal TotalAmount { get; set; }
    public int ItemCount { get; set; }
    public string? StaffId { get; set; }
    public List<OrderItemSummary> Items { get; set; } = new();
    public List<OrderStatusHistory> StatusHistory { get; set; } = new();
}

public class OrderItemSummary
{
    public Guid ProductId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public int Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal TotalPrice { get; set; }
}

public class OrderStatusHistory
{
    public OrderStatusId Status { get; set; }
    public DateTime ChangedAt { get; set; }
    public string? Reason { get; set; }
    public string? ChangedBy { get; set; }
}

public class OrderManagementService : IOrderManagementService
{
    private readonly IOrderService _orderService;
    private readonly IOrderWorkflowService _orderWorkflowService;
    private readonly ILogger<OrderManagementService> _logger;
    private readonly IHttpContextAccessor _httpContextAccessor;
    
    public OrderManagementService(
        IOrderService orderService,
        IOrderWorkflowService orderWorkflowService,
        ILogger<OrderManagementService> logger,
        IHttpContextAccessor httpContextAccessor)
    {
        _orderService = orderService;
        _orderWorkflowService = orderWorkflowService;
        _logger = logger;
        _httpContextAccessor = httpContextAccessor;
    }
    
    private Guid GetCurrentTenantId()
    {
        var tenantClaim = _httpContextAccessor.HttpContext?.User.FindFirst("TenantId")?.Value;
        return Guid.TryParse(tenantClaim, out var tenantId) ? tenantId : Guid.Empty;
    }
    
    public async Task<List<Order>> GetOrdersAsync(OrderStatusId? status = null)
    {
        try
        {
            if (status != null)
            {
                return await _orderWorkflowService.GetOrdersByStatusAsync(status);
            }
            
            var pending = await _orderWorkflowService.GetOrdersByStatusAsync(OrderStatusId.Pending);
            var processing = await _orderWorkflowService.GetOrdersByStatusAsync(OrderStatusId.Processing);
            var completed = await _orderWorkflowService.GetOrdersByStatusAsync(OrderStatusId.Completed);
            var cancelled = await _orderWorkflowService.GetOrdersByStatusAsync(OrderStatusId.Cancelled);
            return pending.Concat(processing).Concat(completed).Concat(cancelled).ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get orders with status filter: {Status}", status);
            return new List<Order>();
        }
    }
    
    public async Task<Order?> GetOrderAsync(Guid orderId)
    {
        try
        {
            return await _orderWorkflowService.GetOrderAsync(orderId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get order: {OrderId}", orderId);
            return null;
        }
    }
    
    public async Task<bool> UpdateOrderStatusAsync(Guid orderId, OrderStatusId newStatus, string? reason = null)
    {
        try
        {
            var order = await _orderWorkflowService.GetOrderAsync(orderId);
            if (order == null)
            {
                _logger.LogWarning("Order not found for status update: {OrderId}", orderId);
                return false;
            }
            
            var updatedOrder = await _orderWorkflowService.TransitionStatusAsync(order.Id, newStatus, reason);
            
            if (updatedOrder != null)
            {
                _logger.LogInformation("Order status updated successfully: {OrderId} -> {Status}", orderId, newStatus);
                return true;
            }
            else
            {
                _logger.LogWarning("Failed to update order status: {OrderId} -> {Status}", orderId, newStatus);
                return false;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update order status: {OrderId} -> {Status}", orderId, newStatus);
            return false;
        }
    }
    
    public async Task<List<Order>> GetOrdersByDateRangeAsync(DateTime startDate, DateTime endDate)
    {
        try
        {
            var orders = await _orderService.GetOrdersByDateRangeAsync(GetCurrentTenantId(), startDate, endDate);
            return orders.ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get orders by date range: {StartDate} - {EndDate}", startDate, endDate);
            return new List<Order>();
        }
    }
    
    public async Task<OrderMetrics> GetOrderMetricsAsync()
    {
        try
        {
            var pOrders = await _orderWorkflowService.GetOrdersByStatusAsync(OrderStatusId.Pending);
            var prOrders = await _orderWorkflowService.GetOrdersByStatusAsync(OrderStatusId.Processing);
            var cOrders = await _orderWorkflowService.GetOrdersByStatusAsync(OrderStatusId.Completed);
            var canOrders = await _orderWorkflowService.GetOrdersByStatusAsync(OrderStatusId.Cancelled);
            var allOrders = pOrders.Concat(prOrders).Concat(cOrders).Concat(canOrders).ToList();
            var now = DateTime.UtcNow;
            var twentyFourHoursAgo = now.AddHours(-24);
            
            var metrics = new OrderMetrics
            {
                TotalOrders = allOrders.Count,
                PendingOrders = allOrders.Count(o => o.Status == OrderStatusId.Pending),
                ProcessingOrders = allOrders.Count(o => o.Status == OrderStatusId.Processing),
                CompletedOrders = allOrders.Count(o => o.Status == OrderStatusId.Completed),
                CancelledOrders = allOrders.Count(o => o.Status == OrderStatusId.Cancelled),
                TotalRevenue = allOrders.Where(o => o.Status == OrderStatusId.Completed).Sum(o => o.TotalPrice),
                OrdersPerHour = allOrders.Count(o => o.CreatedAt >= twentyFourHoursAgo) / 24
            };
            
            metrics.AverageOrderValue = metrics.TotalOrders > 0 ? metrics.TotalRevenue / metrics.TotalOrders : 0;
            metrics.RevenuePerHour = metrics.TotalRevenue / 24; // Daily average
            
            // Status breakdown
            var statusGroups = allOrders.GroupBy(o => o.Status).ToList();
            foreach (var group in statusGroups)
            {
                metrics.StatusBreakdown.Add(new StatusCount
                {
                    Status = group.Key,
                    Count = group.Count(),
                    Percentage = metrics.TotalOrders > 0 ? (decimal)group.Count() / metrics.TotalOrders * 100 : 0
                });
            }
            
            return metrics;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get order metrics");
            return new OrderMetrics();
        }
    }
    
    public async Task<bool> AssignOrderToStaffAsync(Guid orderId, Guid staffId)
    {
        try
        {
            var order = await _orderWorkflowService.GetOrderAsync(orderId);
            if (order == null)
            {
                _logger.LogWarning("Order not found for staff assignment: {OrderId}", orderId);
                return false;
            }
            
            // In a real implementation, this would update the order's assigned staff
            // For now, we'll just log the assignment
            _logger.LogInformation("Order assigned to staff: {OrderId} -> {StaffId}", orderId, staffId);
            
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to assign order to staff: {OrderId} -> {StaffId}", orderId, staffId);
            return false;
        }
    }
    
    public async Task<List<Order>> GetOrdersByCustomerAsync(string customerId)
    {
        try
        {
            return await _orderWorkflowService.GetOrdersByCustomerAsync(customerId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get orders by customer: {CustomerId}", customerId);
            return new List<Order>();
        }
    }
    
    public async Task<bool> CancelOrderAsync(Guid orderId, string reason)
    {
        try
        {
            return await UpdateOrderStatusAsync(orderId, OrderStatusId.Cancelled, reason);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to cancel order: {OrderId}", orderId);
            return false;
        }
    }
    
    public async Task<OrderSummary> GetOrderSummaryAsync(Guid orderId)
    {
        try
        {
            var order = await _orderWorkflowService.GetOrderAsync(orderId);
            if (order == null)
            {
                return new OrderSummary();
            }
            
            var summary = new OrderSummary
            {
                OrderId = order.Id,
                CustomerId = order.CustomerId?.ToString() ?? string.Empty,
                Status = order.Status,
                CreatedAt = order.CreatedAt,
                UpdatedAt = order.UpdatedAt,
                TotalAmount = order.TotalPrice,
                ItemCount = order.Items.Count,
                Items = order.Items.Select(item => new OrderItemSummary
                {
                    ProductId = item.ProductId,
                    ProductName = $"Product {item.ProductId}", // In real implementation, fetch from product service
                    Quantity = item.Quantity,
                    UnitPrice = item.UnitPrice,
                    TotalPrice = item.TotalPrice
                }).ToList(),
                StatusHistory = new List<OrderStatusHistory>
                {
                    new OrderStatusHistory
                    {
                        Status = order.Status,
                        ChangedAt = order.CreatedAt,
                        Reason = "Order created"
                    }
                }
            };
            
            return summary;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get order summary: {OrderId}", orderId);
            return new OrderSummary();
        }
    }
    
    public async Task<OrderDashboardData> GetDashboardDataAsync(Guid tenantId)
    {
        try
        {
            return await _orderService.GetDashboardDataAsync(tenantId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get dashboard data for tenant: {TenantId}", tenantId);
            return new OrderDashboardData();
        }
    }
}
