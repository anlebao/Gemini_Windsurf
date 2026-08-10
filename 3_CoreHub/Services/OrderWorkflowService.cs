using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Options;
using VanAn.Shared.Services;
using System.Text.Json;
using VanAn.CoreHub.Infrastructure;
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
        IOrderNotificationService? orderNotificationService = null,
        IOptions<LoyaltyPointsConfig>? loyaltyPointsConfig = null,
        ILoyaltyModeResolver? loyaltyModeResolver = null,
        IAllianceWalletService? allianceWalletService = null,
        IVanAnDbContext? dbContext = null,
        ILoyaltyBudgetService? loyaltyBudgetService = null,
        IFeatureFlagService? featureFlagService = null,
        IRefundOrchestrationService? refundOrchestrationService = null) : IOrderWorkflowService
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
        // Loyalty-A: Global default points formula (IOptions fallback when tenant has no per-tenant config)
        private readonly LoyaltyPointsConfig _loyaltyPointsConfig = loyaltyPointsConfig?.Value ?? new LoyaltyPointsConfig();
        // Loyalty Alliance Phase 2B: mode resolver + cross-tenant wallet service (null in Silo-only deployments)
        private readonly ILoyaltyModeResolver? _loyaltyModeResolver = loyaltyModeResolver;
        private readonly IAllianceWalletService? _allianceWalletService = allianceWalletService;
        // VALCN v2.0 Phase 1: DbContext for LoyaltyIssuanceRecord creation (null in test contexts)
        private readonly IVanAnDbContext? _dbContext = dbContext;
        // VALCN v2.0 Phase 3: Loyalty budget service + feature flag (null in test contexts — feature OFF = existing behavior)
        private readonly ILoyaltyBudgetService? _loyaltyBudgetService = loyaltyBudgetService;
        private readonly IFeatureFlagService? _featureFlagService = featureFlagService;
        // VALCN v2.0 Phase 4: Refund orchestration (null in test contexts — feature OFF = existing silent-cancel behavior)
        private readonly IRefundOrchestrationService? _refundOrchestrationService = refundOrchestrationService;
        // _shopFeatureSettingsService already declared at line 34 (Wave 1-T6) — reused for Loyalty-C WS-A per-tenant formula override.

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
                else if (newStatus.Value == "delivered")
                {
                    // #99-2: Award loyalty points on delivery (not just completion).
                    // Shop owners often mark orders as "delivered" but forget to click "completed",
                    // leaving customers without their earned points. Award on delivery with a
                    // double-award guard in ProcessLoyaltyPointsAsync (checks loyalty history
                    // for existing entry with this OrderId) — safe if order later transitions
                    // to "completed" which would attempt to award again.
                    await ProcessLoyaltyPointsAsync(order);
                }
                else if (newStatus.Value == "cancelled")
                {
                    // VALCN v2.0 Phase 4: Refund orchestration on cancel (UC-06 — 4-step reversal).
                    // Feature-flagged via ValcnV2_RefundReversal (default OFF = existing silent-cancel behavior).
                    // When ON: 2a payment/accrual + 2b accounting reversal + 2c loyalty reversal + 2d referral reversal.
                    await HandleOrderCancelledAsync(order, reason ?? "Order cancelled");
                }

                // 📡 Wave 9: Publish NATS event for push notifications (non-blocking)
                await PublishOrderStatusChangedEventAsync(order, oldStatus, newStatus);

                // Sync: Enqueue Outbox event so NatsSyncWorker publishes "vanan.shoperp.order.status.changed"
                // → Gateway DataSyncSubscriber updates PostgreSQL → KhachLink OrderTracking sees new status.
                await EnqueueOrderStatusChangedEventAsync(order, oldStatus, newStatus);

                // Persist Outbox events (enqueued by HandleOrderCompletedAsync + EnqueueOrderStatusChangedEventAsync)
                // before committing the transaction. Without this, Outbox events are in the change tracker
                // but never flushed to DB → NatsSyncWorker never picks them up → status never syncs to PG.
                await _orderRepository.SaveChangesAsync();

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

        /// <summary>
        /// VALCN v2.0 Phase 4: Handle order cancellation — feature-flagged 4-step refund reversal (UC-06).
        /// When ValcnV2_RefundReversal is OFF (default): existing silent-cancel behavior (no reversal).
        /// When ON: RefundOrchestrationService.OrchestrateReversalAsync (2a + 2b + 2c + 2d).
        /// </summary>
        private async Task HandleOrderCancelledAsync(Order order, string reason)
        {
            if (_featureFlagService == null || _refundOrchestrationService == null)
            {
                _logger.LogDebug("VALCN v2.0 Phase 4: Feature flag service or refund orchestration not available — skipping reversal for order {OrderId}", order.Id);
                return;
            }

            try
            {
                if (await _featureFlagService.IsEnabledAsync("ValcnV2_RefundReversal"))
                {
                    await _refundOrchestrationService.OrchestrateReversalAsync(order.Id, order.TenantId, $"Order cancelled: {reason}");
                }
                else
                {
                    _logger.LogDebug("VALCN v2.0 Phase 4: ValcnV2_RefundReversal feature OFF — existing silent-cancel behavior for order {OrderId}", order.Id);
                }
            }
            catch (Exception ex)
            {
                // Safe-fail: don't fail the cancel operation if reversal orchestration fails.
                // The cancel itself already succeeded (status updated + saved). Reversal can be retried.
                _logger.LogError(ex, "VALCN v2.0 Phase 4: Refund reversal failed for order {OrderId} — cancel succeeded but reversal incomplete", order.Id);
            }
        }

        private async Task HandleOrderCompletedAsync(Order order, IDbContextTransaction transaction)
        {
            try
            {
                // 📋 NHIỆM VỤ A: Ghi sự kiện Outbox (giả lập)
                await RecordOrderCompletedEventAsync(order);

                // 🔄 NHIỆM VỤ B: Kích hoạt Flywheel
                // Loyalty-A: Guard TrackingCode now configurable via LoyaltyPointsConfig.AwardOnAllOrders.
                //   AwardOnAllOrders=true  → all orders get loyalty points (bỏ guard).
                //   AwardOnAllOrders=false → only orders with TrackingCode get points (giữ behavior cũ).
                bool hasTrackingCode = !string.IsNullOrEmpty(order.TrackingCode);
                bool shouldAwardLoyalty = _loyaltyPointsConfig.AwardOnAllOrders || hasTrackingCode;

                if (hasTrackingCode)
                {
                    await ProcessSocialCampaignConversionAsync(order.TrackingCode!);
                }

                if (shouldAwardLoyalty)
                {
                    await ProcessLoyaltyPointsAsync(order);
                }

                // Phase 5: Update customer order stats (LastOrderDate + TotalSpent) for ALL completed orders.
                // This runs regardless of TrackingCode — all orders update customer stats for segmentation.
                await UpdateCustomerOrderStatsAsync(order);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to handle order completed for order {OrderId}", order.Id);
                throw; // Re-throw to trigger transaction rollback
            }
        }

        /// <summary>
        /// Phase 5: Update Customer.LastOrderDate + TotalSpent when an order completes.
        /// Runs for ALL orders (not just campaign orders) to keep segmentation data accurate.
        /// </summary>
        private async Task UpdateCustomerOrderStatsAsync(Order order)
        {
            if (order.CustomerId == null || order.CustomerId == Guid.Empty)
            {
                _logger.LogDebug("Order {OrderId} has no CustomerId — skipping customer stats update", order.Id);
                return;
            }

            Customer? customer = await _customerRepository.GetByIdAsync(order.CustomerId.Value);
            if (customer == null)
            {
                _logger.LogWarning("Customer {CustomerId} not found for order {OrderId} — skipping stats update",
                    order.CustomerId, order.Id);
                return;
            }

            customer.UpdateOrderStats(DateTime.UtcNow, order.TotalAmount);
            await _customerRepository.UpdateAsync(customer);

            _logger.LogInformation("Updated customer stats for {CustomerId}: LastOrderDate={Date}, TotalSpent+={Amount}",
                customer.Id, DateTime.UtcNow.ToString("yyyy-MM-dd"), order.TotalAmount);
        }

        private async Task RecordOrderCompletedEventAsync(Order order)
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
                eventData,
                correlationId: order.Id);  // VALCN v2.0 Phase 1 — trace root

            // Enqueue to Outbox (added to EF change tracker — committed with the order transaction)
            await _outboxRepository.EnqueueAsync(outboxEvent);
            _logger.LogInformation("Enqueued OrderCompleted event to Outbox for order {OrderId} (EventId={EventId})",
                order.Id, orderCompletedEvent.EventId);
        }

        /// <summary>
        /// Enqueue OrderStatusChanged event to Outbox for SQLite→PG sync.
        /// NatsSyncWorker publishes "vanan.shoperp.order.status.changed" → Gateway DataSyncSubscriber
        /// updates PostgreSQL so KhachLink OrderTracking sees the new status.
        /// </summary>
        private async Task EnqueueOrderStatusChangedEventAsync(Order order, OrderStatusId oldStatus, OrderStatusId newStatus)
        {
            if (_outboxRepository == null)
            {
                _logger.LogWarning("OutboxRepository not available — OrderStatusChanged event for order {OrderId} not persisted", order.Id);
                return;
            }

            var payload = new
            {
                orderId = order.Id,
                tenantId = order.TenantId.Value,
                oldStatus = oldStatus.Value,
                newStatus = newStatus.Value,
                timestamp = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ")
            };
            string eventData = JsonSerializer.Serialize(payload, EventJsonOptions);
            var outboxEvent = new OutboxEvent(
                order.TenantId,
                new ElectronicInvoiceId(Guid.Empty),
                "OrderStatusChanged",
                eventData,
                correlationId: order.Id);  // VALCN v2.0 Phase 1 — trace root
            await _outboxRepository.EnqueueAsync(outboxEvent);
            _logger.LogInformation("Enqueued OrderStatusChanged event to Outbox for order {OrderId}: {OldStatus} → {NewStatus}",
                order.Id, oldStatus.Value, newStatus.Value);
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

            // Bug 6 fix: Try CustomerId first, then DeviceId. If neither finds a customer,
            // create a Customer stub from DeviceId + CustomerInfo so loyalty points can be awarded.
            // Previously: only CustomerId was checked → all guest checkout orders (CustomerId=null)
            // skipped loyalty points, even though they have CustomerDeviceId + CustomerInfo.
            Customer? customer = null;
            if (order.CustomerId.HasValue)
            {
                customer = await _customerRepository.GetByIdAsync(order.CustomerId.Value);
            }

            // Fallback 1: find by DeviceId (guest checkout with device fingerprint)
            if (customer == null && !string.IsNullOrEmpty(order.CustomerDeviceId)
                && Guid.TryParse(order.CustomerDeviceId, out Guid deviceId))
            {
                customer = await _customerRepository.GetByDeviceIdAsync(deviceId);
            }

            // Fallback 2: create Customer stub from DeviceId + CustomerInfo
            if (customer == null && !string.IsNullOrEmpty(order.CustomerDeviceId)
                && Guid.TryParse(order.CustomerDeviceId, out Guid deviceIdForStub))
            {
                customer = await CreateCustomerStubAsync(order, deviceIdForStub);
                if (customer != null)
                {
                    _logger.LogInformation("Bug 6 fix: Created customer stub {CustomerId} for order {OrderId} (device-based loyalty)", customer.Id, order.Id);
                }
            }

            if (customer == null)
            {
                _logger.LogWarning("Customer not found for order {OrderId}", order.Id);
                return;
            }

            // #99-2: Double-award guard — check if loyalty points were already awarded for this order.
            // This prevents duplicate awards when an order transitions delivered→completed
            // (both statuses trigger ProcessLoyaltyPointsAsync). Checks the customer's loyalty
            // history for an existing EARN entry referencing this order ID.
            try
            {
                var existingRewards = await _loyaltyRewardsService.GetCustomerRewardsAsync(customer.Id);
                if (existingRewards != null && !string.IsNullOrEmpty(existingRewards.History))
                {
                    var historyEntries = JsonSerializer.Deserialize<List<LoyaltyHistoryEntry>>(existingRewards.History);
                    if (historyEntries != null && historyEntries.Any(h =>
                        h.Type == "EARN" && h.Reason.Contains($"#{order.Id}")))
                    {
                        _logger.LogInformation("Loyalty: Skipped duplicate award for order {OrderId} — already in history", order.Id);
                        return;
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Loyalty: Failed to check duplicate award for order {OrderId} — proceeding with award", order.Id);
            }

            // Loyalty-A + Loyalty-C WS-A: Configurable points formula.
            //   Issue #118 fix: Read LoyaltyGlobalConfig from DB (admin-managed via /admin/loyalty-config).
            //   Fall back to IOptions<LoyaltyPointsConfig> (appsettings.json) if DB unavailable or no row.
            //   Loyalty-C WS-A: per-tenant override via ShopFeatureSettingsService (DB-backed).
            //     Fallback: if tenant has no per-tenant config (entity null or field 0/null), use global default.
            //
            // Type reconciliation:
            //   LoyaltyGlobalConfig.PointsRate is int (1 = 1% of order total → 0.01 decimal rate)
            //   LoyaltyPointsConfig.PointsRate is decimal (0.1 = 10% of order total)
            //   Convert DB int → decimal: rate = dbPointsRate / 100m
            decimal rate = _loyaltyPointsConfig.PointsRate;
            int minPoints = _loyaltyPointsConfig.MinPointsPerOrder;
            int? maxPoints = _loyaltyPointsConfig.MaxPointsPerOrder;
            bool awardOnAll = _loyaltyPointsConfig.AwardOnAllOrders;

            // Issue #118: Override with DB-backed LoyaltyGlobalConfig if available.
            // This makes admin UI changes (/admin/loyalty-config) actually affect point calculations.
            if (_dbContext != null)
            {
                try
                {
                    var globalConfig = await _dbContext.LoyaltyGlobalConfigs.FirstOrDefaultAsync();
                    if (globalConfig != null)
                    {
                        // DB int PointsRate (1=1%) → decimal rate (0.01)
                        // Only override if DB value > 0 (0 means "use appsettings default")
                        if (globalConfig.PointsRate > 0)
                        {
                            rate = globalConfig.PointsRate / 100m;
                        }
                        minPoints = globalConfig.MinPointsPerOrder;
                        maxPoints = globalConfig.MaxPointsPerOrder;
                        _logger.LogDebug("Loyalty: Using DB-backed global config — rate={Rate}, min={Min}, max={Max}",
                            rate, minPoints, maxPoints?.ToString() ?? "none");
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Loyalty: Failed to load LoyaltyGlobalConfig from DB — falling back to appsettings default");
                }
            }

            if (_shopFeatureSettingsService != null && customer.TenantId.Value != Guid.Empty)
            {
                try
                {
                    var tenantSettings = await _shopFeatureSettingsService.GetSettingsAsync(customer.TenantId);

                    // #99-3: Check Loyalty_Program_Enabled toggle — tenant can disable loyalty entirely.
                    // Previously: toggle existed in ShopFeatureSettingsDto but was never checked → points
                    // awarded even when tenant turned off loyalty program. Fail-open (default=true) if
                    // service throws — preserves existing behavior for tenants without explicit config.
                    if (!tenantSettings.Loyalty_Program_Enabled)
                    {
                        _logger.LogInformation("Loyalty: Skipped award for order {OrderId} — Loyalty_Program_Enabled=false for tenant {TenantId}",
                            order.Id, customer.TenantId.Value);
                        return;
                    }

                    // Override only if tenant has explicitly configured (non-zero/non-null values)
                    if (tenantSettings.Loyalty_PointsRate > 0m) rate = tenantSettings.Loyalty_PointsRate;
                    if (tenantSettings.Loyalty_MinPointsPerOrder > 0) minPoints = tenantSettings.Loyalty_MinPointsPerOrder;
                    if (tenantSettings.Loyalty_MaxPointsPerOrder.HasValue) maxPoints = tenantSettings.Loyalty_MaxPointsPerOrder;
                    awardOnAll = tenantSettings.Loyalty_AwardOnAllOrders;
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to load per-tenant loyalty formula for tenant {TenantId}. Using global default.", customer.TenantId);
                }
            }

            // AwardOnAllOrders=false: only award if order has TrackingCode (campaign-referred)
            if (!awardOnAll && string.IsNullOrEmpty(order.TrackingCode))
            {
                _logger.LogInformation(" Loyalty: Skipped award for order {OrderId} — AwardOnAllOrders=false and no TrackingCode.", order.Id);
                return;
            }

            // PointsRate * TotalAmount, clamped to [MinPointsPerOrder, MaxPointsPerOrder].
            int pointsToAward = (int)(order.TotalAmount * rate);
            pointsToAward = Math.Max(minPoints, pointsToAward);
            if (maxPoints.HasValue)
            {
                pointsToAward = Math.Min(maxPoints.Value, pointsToAward);
            }

            // VALCN v2.0 Phase 3: Loyalty budget enforcement (feature-flagged, default OFF).
            // When OFF: pointsToAward unchanged (existing behavior — no budget check).
            // When ON: CheckAndAdjustPointsAsync applies caps (per-order, monthly, daily, per-customer).
            //   If budget exhausted (returns 0) → skip reward, order still completes.
            bool budgetCheckEnabled = false;
            if (_featureFlagService != null && _loyaltyBudgetService != null)
            {
                try
                {
                    budgetCheckEnabled = await _featureFlagService.IsEnabledAsync("ValcnV2_LoyaltyBudget");
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Loyalty: Failed to check ValcnV2_LoyaltyBudget flag for order {OrderId} — defaulting to OFF (existing behavior)", order.Id);
                }
            }

            if (budgetCheckEnabled)
            {
                int originalPoints = pointsToAward;
                pointsToAward = await _loyaltyBudgetService.CheckAndAdjustPointsAsync(
                    order.TenantId.Value, customer.Id, order.TotalAmount, pointsToAward);

                if (pointsToAward <= 0)
                {
                    _logger.LogInformation(
                        "Loyalty: Budget exhausted for tenant {TenantId} — skipping reward for order {OrderId} (original points would have been {Orig})",
                        order.TenantId.Value, order.Id, originalPoints);
                    return;  // No reward, but order still completes
                }

                if (pointsToAward < originalPoints)
                {
                    _logger.LogInformation(
                        "Loyalty: Budget cap applied for order {OrderId} — points adjusted {Orig}→{New}",
                        order.Id, originalPoints, pointsToAward);
                }
            }

            // Lấy thông tin campaign để ghi lịch sử (null if no tracking code — AwardOnAllOrders mode)
            SocialCampaign? campaign = null;
            string campaignName = "Direct Order";
            if (!string.IsNullOrEmpty(order.TrackingCode))
            {
                campaign = await _socialCampaignService.GetCampaignByTrackingCodeAsync(order.TrackingCode!);
                campaignName = campaign?.CampaignName ?? "Unknown Campaign";
            }

            string reason = $"Hoàn tiền từ chiến dịch {campaignName} - Đơn hàng #{order.Id}";

            // === Loyalty Alliance Phase 2B: Mode routing ===
            // If mode=Alliance and tenant is an alliance member, route EARN to the cross-tenant PG wallet.
            // If mode=Silo, or tenant opted out (IsAllianceMember=false), fall through to the existing Silo flow.
            if (_loyaltyModeResolver is not null && _allianceWalletService is not null)
            {
                LoyaltyMode effectiveMode = await _loyaltyModeResolver.GetEffectiveModeAsync(order.TenantId.Value);
                if (effectiveMode == LoyaltyMode.Alliance)
                {
                    bool isMember = await _loyaltyModeResolver.IsAllianceMemberAsync(order.TenantId.Value);
                    if (isMember)
                    {
                        Guid deviceGuid = Guid.TryParse(order.CustomerDeviceId, out var d) ? d : customer.Id;
                        var (allianceSuccess, newBalance, allianceError) = await _allianceWalletService.AddPointsAsync(
                            deviceGuid, order.TenantId.Value, pointsToAward, reason, order.Id,
                            idempotencyKey: $"earn:{order.Id}");

                        if (allianceSuccess)
                        {
                            _logger.LogInformation("🎁 ALLIANCE EARN: {Points} points to wallet for device {DeviceId} (balance={Balance})",
                                pointsToAward, deviceGuid, newBalance);

                            // VALCN v2.0 Phase 1: Create LoyaltyIssuanceRecord for per-order tracking (Phase 4 reversal)
                            await CreateLoyaltyIssuanceRecordAsync(order.Id, customer.Id, order.TenantId, pointsToAward);

                            // VALCN v2.0 Phase 3: Record issuance for budget counters (feature-flagged)
                            if (budgetCheckEnabled)
                            {
                                await _loyaltyBudgetService.RecordIssuanceAsync(order.TenantId.Value, pointsToAward);
                            }
                        }
                        else
                        {
                            _logger.LogWarning("Alliance EARN failed for order {OrderId}: {Error}", order.Id, allianceError);
                        }
                        return;
                    }

                    _logger.LogInformation("Loyalty: Tenant {TenantId} is not alliance member — falling through to Silo earn", order.TenantId);
                }
            }

            // === EXISTING: Silo flow (unchanged) ===
            bool success = await _loyaltyRewardsService.AddPointsAsync(customer.Id, pointsToAward, reason);

            if (success)
            {
                _logger.LogInformation("🎁 LOYALTY: Awarded {Points} points to customer {CustomerId} from order {OrderId} (rate={Rate}, min={Min}, max={Max})",
                    pointsToAward, customer.Id, order.Id, rate, minPoints, maxPoints?.ToString() ?? "none");

                // VALCN v2.0 Phase 1: Create LoyaltyIssuanceRecord for per-order tracking (Phase 4 reversal)
                await CreateLoyaltyIssuanceRecordAsync(order.Id, customer.Id, order.TenantId, pointsToAward);

                // VALCN v2.0 Phase 3: Record issuance for budget counters (feature-flagged)
                if (budgetCheckEnabled)
                {
                    await _loyaltyBudgetService.RecordIssuanceAsync(order.TenantId.Value, pointsToAward);
                }
            }
        }

        /// <summary>
        /// Bug 6 fix: Create a Customer stub from DeviceId + Order.CustomerInfo for loyalty points.
        /// Used when an order is completed but has no CustomerId (guest checkout).
        /// The stub allows loyalty points to be awarded and tracked by DeviceId.
        /// </summary>
        private async Task<Customer?> CreateCustomerStubAsync(Order order, Guid deviceId)
        {
            try
            {
                string fullName = order.CustomerInfo?.FullName ?? "Khách lẻ";
                string phone = order.CustomerInfo?.PhoneNumber ?? "0000000000";
                var customer = new Customer(order.TenantId, fullName, phone);
                customer.UpdateCustomerDetails(fullName, phone, null, "Bronze", deviceId, true);
                await _customerRepository.AddAsync(customer);

                // TD-CUSTSYNC-001: Enqueue CustomerCreated outbox event for SQLite→PG sync.
                // NatsSyncWorker publishes "vanan.shoperp.customer.created" → Gateway DataSyncSubscriber
                // upserts customer to PostgreSQL so Gateway knows about DeviceId-based stubs.
                await EnqueueCustomerCreatedEventAsync(customer);

                return customer;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Bug 6 fix: Failed to create customer stub for order {OrderId}", order.Id);
                return null;
            }
        }

        /// <summary>
        /// VALCN v2.0 Phase 1: Create a LoyaltyIssuanceRecord after AddPoints succeeds.
        /// Tracks points issued per order — used by Phase 4 RefundOrchestrationService for reversal.
        /// Safe-fail: if DbContext null or exception, log warning but don't fail the order (loyalty already awarded).
        /// </summary>
        private async Task CreateLoyaltyIssuanceRecordAsync(Guid orderId, Guid customerId, TenantId tenantId, int pointsIssued)
        {
            if (_dbContext == null) return;  // Test context or ShopERP without DbContext
            try
            {
                var record = new LoyaltyIssuanceRecord(tenantId, orderId, customerId, pointsIssued);
                await _dbContext.LoyaltyIssuanceRecords.AddAsync(record);
                // Note: SaveChanges is called by the caller's transaction (HandleOrderCompletedAsync)
                _logger.LogDebug("LoyaltyIssuanceRecord created for order {OrderId}: {Points} points to customer {CustomerId}",
                    orderId, pointsIssued, customerId);
            }
            catch (Exception ex)
            {
                // Safe-fail: don't fail the order if issuance record can't be created
                _logger.LogWarning(ex, "VALCN v2.0: Failed to create LoyaltyIssuanceRecord for order {OrderId}", orderId);
            }
        }

        /// <summary>
        /// TD-CUSTSYNC-001: Enqueue CustomerCreated outbox event for SQLite→PG sync.
        /// NatsSyncWorker publishes "vanan.shoperp.customer.created" → Gateway DataSyncSubscriber
        /// upserts customer to PostgreSQL.
        /// </summary>
        private async Task EnqueueCustomerCreatedEventAsync(Customer customer)
        {
            if (_outboxRepository == null)
            {
                _logger.LogDebug("OutboxRepository not available — CustomerCreated event for customer {CustomerId} not persisted", customer.Id);
                return;
            }

            var payload = new
            {
                customerId = customer.Id,
                tenantId = customer.TenantId.Value,
                fullName = customer.FullName,
                phoneNumber = customer.PhoneNumber,
                email = customer.Email,
                deviceId = customer.DeviceId,
                identityLevel = (int)customer.IdentityLevel
            };
            string eventData = JsonSerializer.Serialize(payload, EventJsonOptions);
            var outboxEvent = new OutboxEvent(
                customer.TenantId,
                new ElectronicInvoiceId(Guid.Empty),
                "CustomerCreated",
                eventData);
            await _outboxRepository.EnqueueAsync(outboxEvent);
            _logger.LogInformation("Enqueued CustomerCreated event to Outbox for customer {CustomerId}", customer.Id);
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
                    ["confirmed"] = ["completed", "cancelled", "delivered", "delivering"],
                    ["preparing"] = ["ready", "cancelled", "completed"], // Safety: allow recovery if already in preparing
                    ["ready"] = ["completed", "cancelled", "delivered", "delivering"], // CC-S1-T0: shipper accept → delivering
                    ["delivering"] = ["completed", "cancelled", "delivered"], // CC-S1-T0: shipper in transit
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
                    ["pending"] = ["preparing", "confirmed", "cancelled", "completed"], // 🛡️ PHASE 3 FIX: Allow direct to completed; confirmed = owner manually accepted
                    ["confirmed"] = ["preparing", "cancelled", "completed", "delivering"], // CC-S1-T0: allow shipper accept from confirmed
                    ["preparing"] = ["ready", "cancelled", "completed"], // 🛡️ PHASE 3 FIX: Allow direct to completed
                    ["ready"] = ["completed", "cancelled", "delivered", "delivering"], // W2-T3 + CC-S1-T0: shipper accept → delivering
                    ["delivering"] = ["completed", "cancelled", "delivered"], // CC-S1-T0: shipper in transit
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
