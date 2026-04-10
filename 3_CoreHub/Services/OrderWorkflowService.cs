using Microsoft.EntityFrameworkCore;
using VanAn.CoreHub.Infrastructure;
using Microsoft.Extensions.Logging;
using VanAn.Shared.Domain;
using System.Text.Json;

namespace VanAn.CoreHub.Services;

public class OrderWorkflowService : IOrderWorkflowService
{
    private readonly VanAnDbContext _context;
    private readonly ILogger<OrderWorkflowService> _logger;
    private readonly ISocialCampaignService _socialCampaignService;
    private readonly ILoyaltyRewardsService _loyaltyRewardsService;

    public OrderWorkflowService(
        VanAnDbContext context, 
        ILogger<OrderWorkflowService> logger,
        ISocialCampaignService socialCampaignService,
        ILoyaltyRewardsService loyaltyRewardsService)
    {
        _context = context;
        _logger = logger;
        _socialCampaignService = socialCampaignService;
        _loyaltyRewardsService = loyaltyRewardsService;
    }

    public async Task<Order?> TransitionStatusAsync(Guid orderId, OrderStatusId newStatus, string? reason = null)
    {
        using var transaction = await _context.Database.BeginTransactionAsync();
        try
        {
            var order = await _context.Orders
                .Include(o => o.Items)
                .ThenInclude(i => i.Product)
                .Include(o => o.Customer)
                .FirstOrDefaultAsync(o => o.Id == orderId);

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
            order.Status = newStatus;
            order.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();

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

    private async Task HandleOrderCompletedAsync(Order order, Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction transaction)
    {
        try
        {
            // 📋 NHIỆM VỤ A: Ghi sự kiện Outbox (giả lập)
            await RecordOrderCompletedEventAsync(order);

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

    private async Task RecordOrderCompletedEventAsync(Order order)
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
            customer = await _context.Customers.FindAsync(order.CustomerId.Value);
        }
        else if (!string.IsNullOrEmpty(order.CustomerDeviceId))
        {
            // Fallback to DemoUser for backward compatibility
            var demoUser = await _context.Users
                .FirstOrDefaultAsync(u => u.Username == order.CustomerDeviceId);
            if (demoUser != null)
            {
                // Create Customer record for future CRM
                customer = new Customer
                {
                    FullName = demoUser.DisplayName,
                    PhoneNumber = "Unknown",
                    CustomerTier = "Bronze",
                    TenantId = Guid.Empty // Will be set by SaveChangesAsync
                };
                _context.Customers.Add(customer);
                await _context.SaveChangesAsync();
            }
        }

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
        return await _context.Orders
            .Include(o => o.Items)
            .ThenInclude(i => i.Product)
            .Include(o => o.Customer)
            .FirstOrDefaultAsync(o => o.Id == orderId);
    }

    public async Task<List<Order>> GetOrdersByCustomerAsync(string customerDeviceId)
    {
        return await _context.Orders
            .Include(o => o.Items)
            .ThenInclude(i => i.Product)
            .Include(o => o.Customer)
            .Where(o => o.CustomerDeviceId == customerDeviceId || (o.Customer != null && o.Customer.PhoneNumber == customerDeviceId))
            .OrderByDescending(o => o.OrderDate)
            .ToListAsync();
    }

    public async Task<List<Order>> GetOrdersByStatusAsync(OrderStatusId status)
    {
        return await _context.Orders
            .Include(o => o.Items)
            .ThenInclude(i => i.Product)
            .Include(o => o.Customer)
            .Where(o => o.Status == status)
            .OrderByDescending(o => o.OrderDate)
            .ToListAsync();
    }

    public async Task<bool> IsTransitionValidAsync(OrderStatusId currentStatus, OrderStatusId newStatus)
    {
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
