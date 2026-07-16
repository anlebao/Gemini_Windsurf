using Microsoft.EntityFrameworkCore.Storage;
using System.Text.Json;
using VanAn.CoreHub.Repositories;
using VanAn.CoreHub.Domain.Repositories;
using VanAn.CoreHub.Interfaces;
using Microsoft.Extensions.Logging;
using VanAn.Shared.Domain;
using VanAn.Shared.Domain.Events;
using VanAn.CoreHub.Infrastructure.Messaging;

namespace VanAn.CoreHub.Services
{
    public class OrderWorkflowService(
        IOrderRepository orderRepository,
        ILogger<OrderWorkflowService> logger,
        ISocialCampaignService socialCampaignService,
        ILoyaltyRewardsService loyaltyRewardsService,
        ICustomerRepository customerRepository,
        INatsEventPublisher? natsEventPublisher,
        IShopFeatureSettingsService? shopFeatureSettingsService = null,
        IOutboxRepository? outboxRepository = null,
        IOrderNotificationService? orderNotificationService = null) : IOrderWorkflowService
    {
        private readonly IOrderRepository _orderRepository = orderRepository;
        private readonly ILogger<OrderWorkflowService> _logger = logger;
        private readonly ISocialCampaignService _socialCampaignService = socialCampaignService;
        private readonly ILoyaltyRewardsService _loyaltyRewardsService = loyaltyRewardsService;
        private readonly ICustomerRepository _customerRepository = customerRepository;
        private readonly INatsEventPublisher? _natsEventPublisher = natsEventPublisher;
        // W1-T6: Shop feature settings — for kitchen workflow toggle bypass
        private readonly IShopFeatureSettingsService? _shopFeatureSettingsService = shopFeatureSettingsService;
        // W-1-T7: Outbox repository for persisting events before NATS publish (reliable delivery)
        private readonly IOutboxRepository? _outboxRepository = outboxRepository;
        // W0-T4: SignalR notification service (null in ShopERP scope — Gateway has OrderHub)
        private readonly IOrderNotificationService? _orderNotificationService = orderNotificationService;

        // W-1-T7: CamelCase JSON options — matches SimpleAccountingEventHandler deserialization policy
        private static readonly JsonSerializerOptions EventJsonOptions = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

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

                if (!await IsTransitionValidAsync(order.Status, newStatus, order.TenantId.Value))
                {
                    _logger.LogWarning("Invalid status transition for order {OrderId}: {CurrentStatus} -> {NewStatus}",
                        orderId, order.Status.Value, newStatus.Value);
                    return null;
                }

                OrderStatusId oldStatus = order.Status;
                order.UpdateOrderStatus(newStatus);

                _ = await _orderRepository.UpdateAsync(order);
                await _orderRepository.SaveChangesAsync();

                // 🛡️ PHASE 3: Event-Driven & Core Services
                if (newStatus.Value == "completed")
                {
                    await HandleOrderCompletedAsync(order, transaction);
                }

                // 📡 Wave 9: Publish NATS event for push notifications (non-blocking)
                await PublishOrderStatusChangedEventAsync(order, oldStatus, newStatus);

                await transaction.CommitAsync();

                // W0-T4: Broadcast SignalR notification to ShopERP staff (best-effort, non-blocking)
                // Null in ShopERP scope (no OrderHub) — in v2 edge mode, NATS → DataSyncSubscriber handles it.
                if (_orderNotificationService != null)
                {
                    _ = _orderNotificationService.NotifyOrderStatusChangedAsync(
                        order.Id, order.TenantId.Value, oldStatus.Value, newStatus.Value);
                }

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
            // W-1-T7: Persist OrderCompleted event to Outbox table for reliable async processing.
            // NatsSyncWorker will poll Outbox and publish to NATS → SimpleAccountingEventHandler
            // creates accounting entries + HKD books in PostgreSQL.
            //
            // OutboxEvent constructor requires ElectronicInvoiceId (domain modeling limitation — R14).
            // For non-invoice events, pass Guid.Empty. Subscribers parse EventData for type-specific fields.
            if (_outboxRepository == null)
            {
                // Fallback: log only (pre-W-1 behavior) — Outbox not registered in DI
                _logger.LogWarning("OutboxRepository not available — OrderCompleted event for order {OrderId} not persisted to Outbox",
                    order.Id);
                return;
            }

            var orderCompletedEvent = new OrderCompletedEvent
            {
                EventId = Guid.NewGuid(),
                OrderId = order.Id,
                CustomerId = order.CustomerId,
                CustomerDeviceId = order.CustomerDeviceId ?? string.Empty,
                TenantId = order.TenantId,
                TotalAmount = order.TotalAmount,
                Items = order.Items.Select(i => new OrderItemEvent
                {
                    ProductId = i.ProductId,
                    ProductName = i.Product?.Name ?? "Unknown",
                    Quantity = i.Quantity,
                    UnitPrice = i.UnitPrice,
                    TotalAmount = i.TotalAmount
                }).ToList(),
                SubTotal = order.SubTotal,
                TotalVatAmount = order.TotalVatAmount,
                CompletedAt = DateTime.UtcNow,
                TrackingCode = order.TrackingCode
            };

            string eventData = JsonSerializer.Serialize(orderCompletedEvent, EventJsonOptions);

            var outboxEvent = new OutboxEvent(
                order.TenantId,
                new ElectronicInvoiceId(Guid.Empty), // R14: domain modeling limitation — non-invoice events use Guid.Empty
                "OrderCompleted",
                eventData);

            // Enqueue to Outbox (added to EF change tracker — committed with the order transaction)
            _ = _outboxRepository.EnqueueAsync(outboxEvent);
            _logger.LogInformation("Enqueued OrderCompleted event to Outbox for order {OrderId} (EventId={EventId})",
                order.Id, orderCompletedEvent.EventId);
        }

        private async Task ProcessSocialCampaignConversionAsync(string trackingCode)
        {
            SocialCampaign? campaign = await _socialCampaignService.GetCampaignByTrackingCodeAsync(trackingCode);
            if (campaign != null)
            {
                _ = await _socialCampaignService.IncrementConvertedOrdersAsync(campaign.Id);
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
            // Legacy: no tenant filter — returns empty due to Guid.Empty tenant mismatch.
            // Use GetOrdersByStatusAsync(status, tenantId) instead.
            IEnumerable<Order> orders = await _orderRepository.GetByStatusAsync(new TenantId(Guid.Empty), status.Value);
            return orders.ToList();
        }

        public async Task<List<Order>> GetOrdersByStatusAsync(OrderStatusId status, Guid tenantId)
        {
            IEnumerable<Order> orders = await _orderRepository.GetByStatusAsync(new TenantId(tenantId), status.Value);
            return orders.ToList();
        }

        /// <summary>
        /// Public interface method — defaults to kitchen ON when no tenant context available.
        /// Used by OrderWorkflowController.IsTransitionValid endpoint (no order loaded → no tenantId).
        /// </summary>
        public async Task<bool> IsTransitionValidAsync(OrderStatusId currentStatus, OrderStatusId newStatus)
        {
            return await IsTransitionValidAsync(currentStatus, newStatus, null);
        }

        /// <summary>
        /// W1-T6: Internal validation with tenant context — checks Kitchen_Workflow_Enabled toggle.
        /// When kitchen toggle OFF: bypass preparing/ready, allow confirmed→completed directly.
        /// When kitchen toggle ON (default): normal flow pending→preparing→ready→completed.
        /// </summary>
        private async Task<bool> IsTransitionValidAsync(OrderStatusId currentStatus, OrderStatusId newStatus, Guid? tenantId)
        {
            // W1-T6: Check kitchen toggle — default ON if service unavailable or no tenant context
            bool kitchenEnabled = true;
            if (tenantId.HasValue && tenantId.Value != Guid.Empty && _shopFeatureSettingsService != null)
            {
                try
                {
                    kitchenEnabled = await _shopFeatureSettingsService.IsEnabledAsync(
                        tenantId.Value,
                        nameof(ShopFeatureSettingsDto.Kitchen_Workflow_Enabled));
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to check kitchen toggle for tenant {TenantId} — defaulting to ON", tenantId);
                    kitchenEnabled = true;
                }
            }

            Dictionary<string, List<string>> validTransitions;

            if (!kitchenEnabled)
            {
                // W1-T6: Kitchen bypass — skip preparing/ready, allow confirmed→completed directly
                validTransitions = new()
                {
                    ["pending"] = ["confirmed", "cancelled", "completed"],
                    ["confirmed"] = ["completed", "cancelled", "delivered"],
                    ["preparing"] = ["ready", "cancelled", "completed"], // Safety: allow recovery if already in preparing
                    ["ready"] = ["completed", "cancelled", "delivered"],
                    ["delivered"] = ["completed", "cancelled"],
                    ["completed"] = [],
                    ["cancelled"] = []
                };
            }
            else
            {
                // Normal kitchen flow
                validTransitions = new()
                {
                    ["pending"] = ["preparing", "cancelled", "completed"], // 🛡️ PHASE 3 FIX: Allow direct to completed
                    ["preparing"] = ["ready", "cancelled", "completed"], // 🛡️ PHASE 3 FIX: Allow direct to completed
                    ["ready"] = ["completed", "cancelled", "delivered"], // W2-T3: Customer confirm receipt
                    ["delivered"] = ["completed", "cancelled"], // W2-T3: delivered is intermediate state
                    ["completed"] = [], // Final state
                    ["cancelled"] = []  // Final state
                };
            }

            return validTransitions.ContainsKey(currentStatus.Value) &&
                   validTransitions[currentStatus.Value].Contains(newStatus.Value);
        }

        /// <summary>
        /// Wave 9: Publish order status change event to NATS for push notifications.
        /// Non-blocking - wrapped in try/catch to prevent workflow failures.
        /// </summary>
        private async Task PublishOrderStatusChangedEventAsync(Order order, OrderStatusId oldStatus, OrderStatusId newStatus)
        {
            if (_natsEventPublisher == null)
            {
                _logger.LogDebug("NATS event publisher not available - skipping order status event publishing");
                return;
            }

            try
            {
                var payload = new
                {
                    orderId = order.Id,
                    tenantId = order.TenantId.Value,
                    customerId = order.CustomerId,
                    oldStatus = oldStatus.Value,
                    newStatus = newStatus.Value,
                    timestamp = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ")
                };

                var payloadBytes = System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(payload);
                await _natsEventPublisher.PublishAsync("order.status.changed", payloadBytes);

                _logger.LogInformation("Published order status changed event to NATS: OrderId={OrderId}, Status={Status}", 
                    order.Id, newStatus.Value);
            }
            catch (Exception ex)
            {
                // Log but don't throw - NATS failures should not block order workflow
                _logger.LogError(ex, "Failed to publish order status changed event to NATS for OrderId: {OrderId}", order.Id);
            }
        }
    }
}
