using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using VanAn.CoreHub.Infrastructure;
using VanAn.CoreHub.Repositories;
using VanAn.CoreHub.Domain.Repositories;
using Microsoft.Extensions.Logging;
using VanAn.Shared.Domain;
using System.Text.Json;

namespace VanAn.CoreHub.Services;

public class OrderWorkflowService : IOrderWorkflowService
{
    private readonly IOrderRepository _orderRepository;
    private readonly ILogger<OrderWorkflowService> _logger;
    private readonly ISocialCampaignService _socialCampaignService;
    private readonly ILoyaltyRewardsService _loyaltyRewardsService;
    private readonly ICustomerRepository _customerRepository;

    public OrderWorkflowService(
        IOrderRepository orderRepository, 
        ILogger<OrderWorkflowService> logger,
        ISocialCampaignService socialCampaignService,
        ILoyaltyRewardsService loyaltyRewardsService,
        ICustomerRepository customerRepository)
    {
        _orderRepository = orderRepository;
        _logger = logger;
        _socialCampaignService = socialCampaignService;
        _loyaltyRewardsService = loyaltyRewardsService;
        _customerRepository = customerRepository;
    }

    public async Task<Order?> TransitionStatusAsync(Guid orderId, OrderStatusId newStatus, string? reason = null)
    {
        using var transaction = await _orderRepository.BeginTransactionAsync();
        try
        {
            var order = await _orderRepository.GetByIdWithIncludesAsync(orderId);

            if (order == null)
            {
                _logger.LogWarning("Order {OrderId} not found", orderId);
                return null;
            }

            if (!await IsTransitionValidAsync(order.Status, newStatus))
            {
                _logger.LogWarning("Invalid status transition for order {OrderId}: {CurrentStatus} -> {NewStatus}", 
                    orderId, order.Status.Value, newStatus.Value);
                return null;
            }

            var oldStatus = order.Status;
            order.UpdateOrderStatus(newStatus);

            await _orderRepository.UpdateAsync(order);
            await _orderRepository.SaveChangesAsync();

            // 🛡️ PHASE 3: Event-Driven & Core Services
            if (newStatus.Value == "completed")
            {
                await HandleOrderCompletedAsync(order, transaction);
            }

            await transaction.CommitAsync();

            _logger.LogInformation("Order {OrderId} transitioned from {OldStatus} to {NewStatus}", 
                orderId, oldStatus.Value, newStatus.Value);

            return order;
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            _logger.LogError(ex, "Failed to transition order {OrderId} to status {NewStatus}", orderId, newStatus.Value);
            return null;
        }
    }

    private async Task HandleOrderCompletedAsync(Order order, IDbContextTransaction transaction)
    {
        try
        {
            // 📋 NHIỆM VỤ A: Ghi sự kiện Outbox (giả lập)
            RecordOrderCompletedEvent(order);

            // 🔄 NHIỆM VỤ B: Kích hoạt Flywheel
            if (!string.IsNullOrEmpty(order.TrackingCode))
            {
                await ProcessSocialCampaignConversionAsync(order.TrackingCode);
                await ProcessLoyaltyPointsAsync(order);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to handle order completed for order {OrderId}", order.Id);
            throw; // Re-throw to trigger transaction rollback
        }
    }

    private void RecordOrderCompletedEvent(Order order)
    {
        // 📋 Outbox Pattern - Giả lập ghi vào Message Queue
        var orderCompletedEvent = new
        {
            EventId = Guid.NewGuid(),
            EventType = "OrderCompleted",
            OrderId = order.Id,
            CustomerDeviceId = order.CustomerDeviceId,
            CustomerId = order.CustomerId,
            Items = order.Items.Select(i => new {
                ProductId = i.ProductId,
                ProductName = i.Product?.Name ?? "Unknown",
                Quantity = i.Quantity,
                UnitPrice = i.UnitPrice,
                TotalAmount = i.TotalAmount
            }).ToList(),
            SubTotal = order.SubTotal,
            TotalVatAmount = order.TotalVatAmount,
            TotalAmount = order.TotalAmount,
            CompletedAt = DateTime.UtcNow,
            TrackingCode = order.TrackingCode
        };

        // TODO: Thực tế sẽ đẩy vào NATS/RabbitMQ/Kafka
        // Hiện tại chỉ log để giả lập
        _logger.LogInformation("📋 OUTBOX EVENT: OrderCompleted - {@Event}", orderCompletedEvent);
    }

    private async Task ProcessSocialCampaignConversionAsync(string trackingCode)
    {
        var campaign = await _socialCampaignService.GetCampaignByTrackingCodeAsync(trackingCode);
        if (campaign != null)
        {
            await _socialCampaignService.IncrementConvertedOrdersAsync(campaign.Id);
            _logger.LogInformation("🔄 FLYWHEEL: Incremented conversion for campaign {CampaignName}", campaign.CampaignName);
        }
    }

    private async Task ProcessLoyaltyPointsAsync(Order order)
    {
        if (string.IsNullOrEmpty(order.CustomerDeviceId) && !order.CustomerId.HasValue)
        {
            _logger.LogWarning("Cannot process loyalty points: No customer identifier for order {OrderId}", order.Id);
            return;
        }

        // Try Customer CRM first, fallback to DemoUser
        Customer? customer = null;
        if (order.CustomerId.HasValue)
        {
            customer = await _customerRepository.GetByIdAsync(order.CustomerId.Value);
        }
        // Note: DemoUser fallback removed as it requires direct DbContext access
        // This should be handled by CustomerRepository in future iterations

        if (customer == null)
        {
            _logger.LogWarning("Customer not found for order {OrderId}", order.Id);
            return;
        }

        // Tính điểm thưởng (10% giá trị đơn hàng, tối thiểu 10 điểm)
        var pointsToAward = Math.Max(10, (int)(order.TotalAmount * 0.1m));

        // Lấy thông tin campaign để ghi lịch sử
        var campaign = await _socialCampaignService.GetCampaignByTrackingCodeAsync(order.TrackingCode!);
        var campaignName = campaign?.CampaignName ?? "Unknown Campaign";

        var reason = $"Hoàn tiền từ chiến dịch {campaignName} - Đơn hàng #{order.Id}";
        
        var success = await _loyaltyRewardsService.AddPointsAsync(customer.Id, pointsToAward, reason);
        
        if (success)
        {
            _logger.LogInformation("🎁 LOYALTY: Awarded {Points} points to customer {CustomerId} from order {OrderId}", 
                pointsToAward, customer.Id, order.Id);
        }
    }

    public async Task<Order?> GetOrderAsync(Guid orderId)
    {
        return await _orderRepository.GetByIdWithIncludesAsync(orderId);
    }

    public async Task<List<Order>> GetOrdersByCustomerAsync(string customerDeviceId)
    {
        // This method needs CustomerRepository to work properly
        // For now, return empty list as it requires direct DbContext access
        _logger.LogWarning("GetOrdersByCustomerAsync requires CustomerRepository - not implemented");
        return new List<Order>();
    }

    public async Task<List<Order>> GetOrdersByStatusAsync(OrderStatusId status)
    {
        var orders = await _orderRepository.GetByStatusAsync(new TenantId(Guid.Empty), status.Value);
        return orders.ToList();
    }

    public async Task<bool> IsTransitionValidAsync(OrderStatusId currentStatus, OrderStatusId newStatus)
    {
        await Task.CompletedTask;
        // Simple validation logic - can be enhanced
        var validTransitions = new Dictionary<string, List<string>>
        {
            ["pending"] = new List<string> { "preparing", "cancelled", "completed" }, // 🛡️ PHASE 3 FIX: Allow direct to completed
            ["preparing"] = new List<string> { "ready", "cancelled", "completed" }, // 🛡️ PHASE 3 FIX: Allow direct to completed
            ["ready"] = new List<string> { "completed", "cancelled" },
            ["completed"] = new List<string>(), // Final state
            ["cancelled"] = new List<string>()  // Final state
        };

        return validTransitions.ContainsKey(currentStatus.Value) && 
               validTransitions[currentStatus.Value].Contains(newStatus.Value);
    }
}
