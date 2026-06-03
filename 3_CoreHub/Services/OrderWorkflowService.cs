using Microsoft.EntityFrameworkCore.Storage;
using VanAn.CoreHub.Repositories;
using VanAn.CoreHub.Domain.Repositories;
using Microsoft.Extensions.Logging;
using VanAn.Shared.Domain;

namespace VanAn.CoreHub.Services
{
    public class OrderWorkflowService(
        IOrderRepository orderRepository,
        ILogger<OrderWorkflowService> logger,
        ISocialCampaignService socialCampaignService,
        ILoyaltyRewardsService loyaltyRewardsService,
        ICustomerRepository customerRepository) : IOrderWorkflowService
    {
        private readonly IOrderRepository _orderRepository = orderRepository;
        private readonly ILogger<OrderWorkflowService> _logger = logger;
        private readonly ISocialCampaignService _socialCampaignService = socialCampaignService;
        private readonly ILoyaltyRewardsService _loyaltyRewardsService = loyaltyRewardsService;
        private readonly ICustomerRepository _customerRepository = customerRepository;

        public async Task<Order?> TransitionStatusAsync(Guid orderId, OrderStatusId newStatus, string? reason = null)
        {
            using IDbContextTransaction transaction = await _orderRepository.BeginTransactionAsync();
            try
            {
                Order? order = await _orderRepository.GetByIdWithIncludesAsync(orderId);

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

                OrderStatusId oldStatus = order.Status;
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
                order.CustomerDeviceId,
                order.CustomerId,
                Items = order.Items.Select(i => new
                {
                    i.ProductId,
                    ProductName = i.Product?.Name ?? "Unknown",
                    i.Quantity,
                    i.UnitPrice,
                    i.TotalAmount
                }).ToList(),
                order.SubTotal,
                order.TotalVatAmount,
                order.TotalAmount,
                CompletedAt = DateTime.UtcNow,
                order.TrackingCode
            };

            // TODO: Thực tế sẽ đẩy vào NATS/RabbitMQ/Kafka
            // Hiện tại chỉ log để giả lập
            _logger.LogInformation("📋 OUTBOX EVENT: OrderCompleted - {@Event}", orderCompletedEvent);
        }

        private async Task ProcessSocialCampaignConversionAsync(string trackingCode)
        {
            SocialCampaign? campaign = await _socialCampaignService.GetCampaignByTrackingCodeAsync(trackingCode);
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
            int pointsToAward = Math.Max(10, (int)(order.TotalAmount * 0.1m));

            // Lấy thông tin campaign để ghi lịch sử
            SocialCampaign? campaign = await _socialCampaignService.GetCampaignByTrackingCodeAsync(order.TrackingCode!);
            string campaignName = campaign?.CampaignName ?? "Unknown Campaign";

            string reason = $"Hoàn tiền từ chiến dịch {campaignName} - Đơn hàng #{order.Id}";

            bool success = await _loyaltyRewardsService.AddPointsAsync(customer.Id, pointsToAward, reason);

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
            return [];
        }

        public async Task<List<Order>> GetOrdersByStatusAsync(OrderStatusId status)
        {
            IEnumerable<Order> orders = await _orderRepository.GetByStatusAsync(new TenantId(Guid.Empty), status.Value);
            return orders.ToList();
        }

        public async Task<bool> IsTransitionValidAsync(OrderStatusId currentStatus, OrderStatusId newStatus)
        {
            await Task.CompletedTask;
            // Simple validation logic - can be enhanced
            Dictionary<string, List<string>> validTransitions = new()
            {
                ["pending"] = ["preparing", "cancelled", "completed"], // 🛡️ PHASE 3 FIX: Allow direct to completed
                ["preparing"] = ["ready", "cancelled", "completed"], // 🛡️ PHASE 3 FIX: Allow direct to completed
                ["ready"] = ["completed", "cancelled"],
                ["completed"] = [], // Final state
                ["cancelled"] = []  // Final state
            };

            return validTransitions.ContainsKey(currentStatus.Value) &&
                   validTransitions[currentStatus.Value].Contains(newStatus.Value);
        }
    }
}
