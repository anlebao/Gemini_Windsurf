using Microsoft.Extensions.Logging;
using Microsoft.EntityFrameworkCore;
using VanAn.CoreHub.Repositories;
using VanAn.CoreHub.Interfaces;
using VanAn.CoreHub.Infrastructure;
using VanAn.CoreHub.Common;
using VanAn.Shared.Domain;
using VanAn.CoreHub.Commands;
using Tenant = VanAn.Shared.Domain.Aggregates.TenantAggregate.Tenant;

namespace VanAn.CoreHub.Services
{
    /// <summary>
    /// Order Service implementation with accounting integration
    /// Phase 2.2: Order to Accounting Integration
    /// </summary>
    public class OrderService(
        IOrderRepository orderRepository,
        IAccountingService accountingService,
        IHKDBookRepository hkdBookRepository,
        IAccountingEntryRepository accountingEntryRepository,
        ILogger<OrderService> logger,
        IInventoryService? inventoryService = null,
        ITemplateFactory? templateFactory = null,
        IOrderHub? orderHub = null,
        IVanAnDbContext? dbContext = null,
        IOrderNotificationService? orderNotificationService = null) : IOrderService
    {
        // EXISTING DEPENDENCIES (keep)
        private readonly IOrderRepository _orderRepository = orderRepository;
        private readonly IAccountingService _accountingService = accountingService;
        private readonly IHKDBookRepository _hkdBookRepository = hkdBookRepository;
        private readonly IAccountingEntryRepository _accountingEntryRepository = accountingEntryRepository;
        private readonly ILogger<OrderService> _logger = logger;

        // NEW DEPENDENCIES
        private readonly IInventoryService _inventoryService = inventoryService;
        private readonly ITemplateFactory _templateFactory = templateFactory;
        private readonly IOrderHub _orderHub = orderHub;

        // Wave 5: DbContext for Tenant.DefaultIndustrySector lookup (Order.IndustrySector ?? Tenant.DefaultIndustrySector)
        private readonly IVanAnDbContext? _dbContext = dbContext;

        // W0-T5: SignalR notification service (null in ShopERP scope — Gateway has OrderHub)
        private readonly IOrderNotificationService? _orderNotificationService = orderNotificationService;

        /// <summary>
        /// Get today's order count for a specific tenant
        /// </summary>
        public async Task<int> GetTodayOrderCountAsync(Guid tenantId)
        {
            TenantId tenantIdObj = new(tenantId);
            DateTime today = DateTime.UtcNow.Date;
            DateTime tomorrow = today.AddDays(1);

            return await _orderRepository.GetCountByDateRangeAsync(tenantIdObj, today, tomorrow);
        }

        /// <summary>
        /// Get orders by date range for a tenant
        /// </summary>
        public async Task<IEnumerable<Order>> GetOrdersByDateRangeAsync(Guid tenantId, DateTime startDate, DateTime endDate)
        {
            TenantId tenantIdObj = new(tenantId);
            return await _orderRepository.GetByDateRangeAsync(tenantIdObj, startDate, endDate);
        }

        /// <summary>
        /// Get order by ID
        /// </summary>
        public async Task<Order?> GetOrderByIdAsync(Guid orderId, Guid tenantId)
        {
            OrderId orderIdObj = new(orderId);
            TenantId tenantIdObj = new(tenantId);
            return await _orderRepository.GetByIdAsync(orderIdObj, tenantIdObj);
        }

        /// <summary>
        /// Create new order. Accounting entries are NOT generated here.
        /// Sprint B: Accounting entries generated only after payment confirmed via ConfirmPaymentAsync().
        /// TT 152/2025/TT-BTC: doanh thu ghi nhận theo thực thu (cash-basis accounting).
        /// </summary>
        public async Task<Order> CreateOrderAsync(Order order, Guid tenantId)
        {
            TenantId tenant = new(tenantId);

            try
            {
                // 1. Create order using repository (NO accounting entries — see ConfirmPaymentAsync)
                Order newOrder = await _orderRepository.AddAsync(order);

                _logger.LogInformation("Created order {OrderId} for tenant {TenantId}. Accounting pending payment confirmation.",
                    newOrder.Id, tenantId);

                return newOrder;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating order for tenant {TenantId}", tenantId);
                throw;
            }
        }

        /// <summary>
        /// Generate accounting entries for order
        /// Phase 2.2: Order to Accounting Integration
        /// Wave 5: Pass IndustrySector (Order.IndustrySector ?? Tenant.DefaultIndustrySector) to accounting entries.
        /// </summary>
        private async Task GenerateAccountingEntriesAsync(Order order, TenantId tenantId)
        {
            // W0-T6 (H4): Use OrderDate (not UtcNow) — entry belongs to the period when order was placed.
            AccountingPeriod period = AccountingPeriod.Create(order.OrderDate.Year, order.OrderDate.Month);
            string orderRef = order.Id.ToString();

            try
            {
                // Wave 5: Resolve industry sector — per-order override falls back to Tenant default
                IndustrySector? sector = order.IndustrySector;
                if (sector == null && _dbContext != null)
                {
                    Tenant? tenant = await _dbContext.Tenants
                        .FirstOrDefaultAsync(t => t.Id == tenantId);
                    sector = tenant?.DefaultIndustrySector;
                }

                // W0-T3 (C3): Split VAT — net revenue on 511 + VAT liability on 3331 (if VAT > 0).
                // W0-T8 (H2): Net revenue approach (HKD path) — credit 511 = SubTotal - DiscountAmount.
                //   (Discount reduces revenue directly; VAS Gross+521 path deferred to W8 feature-flag.)
                decimal netRevenue = order.SubTotal - order.DiscountAmount;
                // W0-T7 (H5): Pass order reference for traceability.
                _ = await _accountingService.CreateRevenueEntryAsync(
                    tenantId,
                    period,
                    netRevenue,
                    $"Doanh thu bán hàng (net) #{order.Id}",
                    accountCode: "511",
                    reference: orderRef,
                    industrySector: sector);

                if (order.TotalVatAmount > 0)
                {
                    // VAT output liability (thuế GTGT đầu ra) — account 3331.
                    // NOTE: EntryType=Revenue (semantically imperfect — no Liability factory exists in Domain).
                    // VAS Wave 4 reports query by AccountCode (3331), so aggregation is correct.
                    // Semantic EntryType refinement deferred to a future wave (would require Domain mod + approval).
                    _ = await _accountingService.CreateRevenueEntryAsync(
                        tenantId,
                        period,
                        order.TotalVatAmount,
                        $"Thuế GTGT đầu ra #{order.Id}",
                        accountCode: "3331",
                        reference: orderRef,
                        industrySector: sector);
                }

                // 2. Generate HKD books for revenue (JournalEntry path — 3 lines if VAT, 2 if not)
                JournalEntry revenueJournalEntry = await CreateRevenueEntryAsync(order, tenantId, period);
                // Use appropriate HKD book types based on business type
                await _hkdBookRepository.AddToBookAsync(revenueJournalEntry, AccountingBookType.S2b_HKD); // Revenue book
                await _hkdBookRepository.AddToBookAsync(revenueJournalEntry, AccountingBookType.S2c_HKD); // Detailed book

                // 3. COGS entry — W0-T4 (C1): shared CalculateCogsAmount syncs Path A and Path B.
                decimal cogsAmount = CalculateCogsAmount(order);
                if (cogsAmount > 0)
                {
                    // W0-T5 (B3): Fix AccountCode 621→632 (Giá vốn hàng bán).
                    Shared.DTOs.AccountingEntryDto cogsEntry = await _accountingService.CreateExpenseEntryAsync(
                        tenantId,
                        period,
                        cogsAmount,
                        $"Giá vốn hàng bán #{order.Id}",
                        accountCode: "632",
                        reference: orderRef,
                        industrySector: sector);

                    // Create COGS journal entry for HKD books
                    JournalEntry? cogsJournalEntry = await CreateCOGSEntryAsync(order, tenantId, period);
                    if (cogsJournalEntry != null)
                    {
                        // Use appropriate HKD book types based on business type
                        await _hkdBookRepository.AddToBookAsync(cogsJournalEntry, AccountingBookType.S2c_HKD); // Detailed book
                        // W0-T10 (M9): Removed AddToBookAsync(S2d_HKD) — COGS does not belong in materials book.
                    }
                }

                _logger.LogInformation("Generated accounting entries for order {OrderId} (IndustrySector: {Sector})", order.Id, sector?.ToString() ?? "NULL");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating accounting entries for order {OrderId}", order.Id);
                throw;
            }
        }

        /// <summary>
        /// W0-T4 (C1): Shared COGS calculation — syncs Path A (AccountingEntry) and Path B (JournalEntry).
        /// Uses actual Product.CostPrice per item; falls back to 70% of UnitPrice for legacy products
        /// (CostPrice not set); ultimate fallback 70% of TotalPrice when Items not loaded.
        /// </summary>
        private static decimal CalculateCogsAmount(Order order)
        {
            if (order.Items.Any())
            {
                return order.Items.Sum(item =>
                {
                    decimal costPrice = item.Product?.CostPrice ?? 0m;
                    decimal effectiveCost = costPrice > 0 ? costPrice : item.UnitPrice * 0.7m; // Fallback for legacy products
                    return item.Quantity * effectiveCost;
                });
            }
            return order.TotalPrice * 0.7m; // Ultimate fallback when Items not loaded
        }

        /// <summary>
        /// Create revenue accounting entry
        /// Phase 2.2: Order to Accounting Integration
        /// </summary>
        private static async Task<JournalEntry> CreateRevenueEntryAsync(Order order, TenantId tenantId, AccountingPeriod period)
        {
            string description = $"Doanh thu bán hàng #{order.Id}";

            // W0-T6 (H4): Use OrderDate (not UtcNow) — entry date = order date.
            JournalEntry journalEntry = new(
                tenantId,
                order.OrderDate,
                description,
                "Order",
                order.Id
            );

            // W0-T2 (C2+H1): Map PaymentMethod → cash account (111 cash, 112 bank).
            string cashAccount = PaymentMethodConstants.MapCashAccount(order.PaymentMethod);

            // W0-T3 (C3): Split VAT — debit cash (gross ex-shipping), credit 511 (net revenue), credit 3331 (VAT).
            // W0-T8 (H2): Net revenue — credit 511 = SubTotal - DiscountAmount (discount reduces revenue).
            // NOTE: Shipping deferred (W0-T9) — debit cash excludes ShippingFee to keep entry balanced.
            //   When shipping accounting is added (future wave), debit will include ShippingFee + matching credit.
            decimal netRevenue = order.SubTotal - order.DiscountAmount;
            decimal cashDebit = netRevenue + order.TotalVatAmount; // excludes ShippingFee (deferred)

            journalEntry.AddLine(cashAccount, cashDebit, 0, "Tiền thu từ bán hàng"); // Cash/Bank
            journalEntry.AddLine("511", 0, netRevenue, "Doanh thu bán hàng (net)"); // Net revenue
            if (order.TotalVatAmount > 0)
            {
                journalEntry.AddLine("3331", 0, order.TotalVatAmount, "Thuế GTGT đầu ra"); // VAT liability
            }

            return await Task.FromResult(journalEntry);
        }

        /// <summary>
        /// Create COGS accounting entry
        /// Phase 2.2: Order to Accounting Integration
        /// </summary>
        private static async Task<JournalEntry?> CreateCOGSEntryAsync(Order order, TenantId tenantId, AccountingPeriod period)
        {
            // W0-T4 (C1): Use shared CalculateCogsAmount — syncs Path A (AccountingEntry) and Path B (JournalEntry).
            decimal cogsAmount = CalculateCogsAmount(order);

            if (cogsAmount <= 0)
            {
                return null;
            }

            string description = $"Giá vốn hàng bán #{order.Id}";

            // W0-T6 (H4): Use OrderDate (not UtcNow).
            JournalEntry journalEntry = new(
                tenantId,
                order.OrderDate,
                description,
                "Order",
                order.Id
            );

            // Add COGS lines (debit COGS, credit inventory)
            journalEntry.AddLine("632", cogsAmount, 0, "Giá vốn hàng bán"); // COGS
            journalEntry.AddLine("156", 0, cogsAmount, "Giảm hàng tồn kho"); // Inventory

            return await Task.FromResult(journalEntry);
        }

        /// <summary>
        /// Update order status
        /// </summary>
        public async Task<bool> UpdateOrderStatusAsync(Guid orderId, string newStatus, Guid tenantId)
        {
            OrderId orderIdObj = new(orderId);
            TenantId tenantIdObj = new(tenantId);
            Order? order = await _orderRepository.GetByIdAsync(orderIdObj, tenantIdObj);

            if (order == null)
            {
                _logger.LogWarning("Order {OrderId} not found for tenant {TenantId}", orderId, tenantId);
                return false;
            }

            order.UpdateOrderStatus(new OrderStatusId(newStatus));

            _ = await _orderRepository.UpdateAsync(order);
            await _orderRepository.SaveChangesAsync();

            _logger.LogInformation("Updated order {OrderId} status to {Status}", orderId, newStatus);
            return true;
        }

        public async Task<bool> UpdateOrderVoiceNoteAsync(Guid orderId, string voiceNoteText, Guid tenantId)
        {
            OrderId orderIdObj = new(orderId);
            TenantId tenantIdObj = new(tenantId);
            Order? order = await _orderRepository.GetByIdAsync(orderIdObj, tenantIdObj);

            if (order == null)
            {
                _logger.LogWarning("Order {OrderId} not found for tenant {TenantId}", orderId, tenantId);
                return false;
            }

            order.UpdateVoiceNotes(voiceNoteText, null);

            _ = await _orderRepository.UpdateAsync(order);
            await _orderRepository.SaveChangesAsync();

            _logger.LogInformation("Updated voice note for order {OrderId}", orderId);
            return true;
        }

        // NEW METHODS

        public async Task<Order> CreateOrderWithQueueAsync(Order order, Guid tenantId)
        {
            TenantId tenant = new(tenantId);

            try
            {
                // Validate order with business rules
                await ValidateOrderAsync(order, tenant);

                // Save to database with queue priority
                using Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction transaction = await _orderRepository.BeginTransactionAsync();

                Order savedOrder = await _orderRepository.AddAsync(order);

                // Sprint B: Accounting entries removed from order creation.
                // They are generated only after payment confirmed via ConfirmPaymentAsync().
                // TT 152/2025/TT-BTC: cash-basis — doanh thu ghi nhận theo thực thu.

                // Create HKD books (operational — not accounting, safe to generate here)
                await GenerateHKDBooksAsync(savedOrder, tenant);

                await transaction.CommitAsync();

                // Real-time notifications
                if (_orderHub != null)
                {
                    await _orderHub.NotifyStaffAsync(savedOrder);
                }

                _logger.LogInformation("Created order {OrderId} with queue for tenant {TenantId}",
                    savedOrder.Id, tenantId);

                return savedOrder;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating order with queue for tenant {TenantId}", tenantId);
                throw;
            }
        }

        public async Task<List<Order>> GetQueuedOrdersAsync(Guid tenantId)
        {
            TenantId tenantIdObj = new(tenantId);
            IEnumerable<Order> orders = await _orderRepository.GetByStatusAsync(tenantIdObj, "Pending");
            return [.. orders.OrderBy(o => o.CreatedAt)];
        }

        public async Task<bool> IsTransitionValidAsync(OrderStatusId currentStatus, OrderStatusId newStatus)
        {
            // Define valid transitions
            Dictionary<OrderStatusId, List<OrderStatusId>> validTransitions = new()
            {
                [OrderStatusId.Pending] = [OrderStatusId.Processing, OrderStatusId.Cancelled],
                [OrderStatusId.Processing] = [OrderStatusId.Completed, OrderStatusId.Cancelled],
                [OrderStatusId.Completed] = [], // Final state
                [OrderStatusId.Cancelled] = [] // Final state
            };

            return validTransitions.ContainsKey(currentStatus) &&
                   validTransitions[currentStatus].Contains(newStatus);
        }

        public async Task<List<Order>> GetOrdersByStatusAsync(OrderStatusId status, Guid tenantId)
        {
            TenantId tenantIdObj = new(tenantId);
            IEnumerable<Order> orders = await _orderRepository.GetByStatusAsync(tenantIdObj, status.Value);
            return [.. orders.OrderByDescending(o => o.CreatedAt)];
        }

        public async Task<OrderDashboardData> GetDashboardDataAsync(Guid tenantId)
        {
            TenantId tenantIdObj = new(tenantId);
            DateTime today = DateTime.UtcNow.Date;
            DateTime tomorrow = today.AddDays(1);

            IEnumerable<Order> orders = await _orderRepository.GetByDateRangeAsync(tenantIdObj, today, tomorrow);

            return new OrderDashboardData
            {
                TodayOrderCount = orders.Count(),
                TodayRevenue = orders.Where(o => o.Status == OrderStatusId.Completed).Sum(o => o.TotalPrice),
                PendingOrders = orders.Count(o => o.Status == OrderStatusId.Pending),
                ProcessingOrders = orders.Count(o => o.Status == OrderStatusId.Processing),
                CompletedOrders = orders.Count(o => o.Status == OrderStatusId.Completed)
            };
        }

        public async Task<OrderSummary> GetOrderSummaryAsync(Guid orderId, Guid tenantId)
        {
            Order? order = await _orderRepository.GetByIdWithIncludesAsync(orderId);

            return order == null
                ? new OrderSummary()
                : new OrderSummary
                {
                    OrderId = order.Id,
                    CustomerId = order.CustomerId?.ToString() ?? string.Empty,
                    Status = order.Status,
                    CreatedAt = order.CreatedAt,
                    TotalAmount = order.TotalPrice,
                    ItemCount = order.Items.Count,
                    Items = order.Items.Select(i => new OrderItemSummary
                    {
                        ProductId = i.ProductId,
                        Quantity = i.Quantity,
                        UnitPrice = i.UnitPrice,
                        TotalPrice = i.TotalPrice
                    }).ToList()
                };
        }

        public async Task<List<AccountingEntry>> GetEntriesByOrderAsync(Guid orderId, TenantId tenantId)
        {
            AccountingEntryId accountingEntryId = new(orderId);
            IEnumerable<AccountingEntry> entries = await _accountingEntryRepository.GetByTenantAsync(tenantId);
            return
            [
                .. entries
                                .Where(e => e.Id.Equals(accountingEntryId) || (e.ReversalEntryId.HasValue && e.ReversalEntryId.Value.Equals(accountingEntryId)))
                                .OrderByDescending(e => e.CreatedAt)
,
            ];
        }

        // HELPER METHODS

        private async Task ValidateOrderAsync(Order order, TenantId tenantId)
        {
            // Business rule validation
            if (order.Items.Count == 0)
            {
                throw new ArgumentException("Order must have at least one item");
            }

            if (order.TotalPrice <= 0)
            {
                throw new ArgumentException("Order total must be positive");
            }

            // Inventory validation (if applicable)
            if (_inventoryService != null)
            {
                bool canFulfill = await _inventoryService.CanFulfillOrderAsync(order, [], []);
                if (!canFulfill)
                {
                    throw new InvalidOperationException("Insufficient inventory for order");
                }
            }
        }


        private async Task GenerateHKDBooksAsync(Order order, TenantId tenantId)
        {
            if (_templateFactory == null)
            {
                _logger.LogWarning("TemplateFactory not available, skipping HKD book generation");
                return;
            }

            AccountingPeriod period = AccountingPeriod.Create(order.OrderDate.Year, order.OrderDate.Month);

            try
            {
                // Get journal entries for this order
                List<AccountingEntry> entries = await GetEntriesByOrderAsync(order.Id, tenantId);

                // Generate HKD books using template system
                List<string> templates = await _templateFactory.GetTemplatesForTenant(tenantId);

                foreach (string templateName in templates)
                {
                    await _templateFactory.GenerateHKDBookAsync(order, tenantId);
                }

                _logger.LogInformation("Generated HKD books for order {OrderId}", order.Id);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating HKD books for order {OrderId}", order.Id);
                // Don't throw - HKD books should not block order creation
            }
        }

        /// <summary>
        /// Create order from Gateway Command - Clean Architecture Pattern
        /// Phase 2.5.4: Unified API Integration - Single Backend Service
        /// </summary>
        public async Task<Order> CreateOrderFromCommandAsync(CreateOrderCommand command, Guid tenantId)
        {
            try
            {
                // Create domain entity using DDD compliant factory methods
                Guid orderId = Guid.NewGuid();
                TenantId tenantIdObj = new(tenantId);

                // Create OrderItems using DDD factory methods
                List<OrderItem> orderItems = command.Items.Select(i =>
                    OrderItem.Create(Guid.NewGuid(), tenantIdObj, orderId, i.ProductId, i.Quantity, i.UnitPrice)
                ).ToList();

                // Create Order using DDD factory method
                Order order = Order.Create(orderId, tenantIdObj, command.CustomerDeviceId, orderItems);

                // Save order using existing repository pattern
                Order createdOrder = await _orderRepository.AddAsync(order);

                _logger.LogInformation("Created order {OrderId} from Gateway command", createdOrder.Id);

                return createdOrder;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating order from Gateway command");
                throw;
            }
        }

        /// <summary>
        /// Sprint B: Confirm payment and generate accounting entries.
        /// Called by WebhookController after bank/VietQR confirms payment.
        /// Idempotent: second call for same orderId returns without creating duplicate entries.
        /// TT 152/2025/TT-BTC: doanh thu ghi nhận theo thực thu (cash-basis accounting).
        /// </summary>
        public async Task ConfirmPaymentAsync(Guid orderId, Guid tenantId, string transactionId, CancellationToken cancellationToken = default)
        {
            TenantId tenantIdObj = new(tenantId);
            OrderId orderIdObj = new(orderId);

            // Fast check with lightweight query (no includes) for idempotency guard
            Order? order = await _orderRepository.GetByIdAsync(orderIdObj, tenantIdObj);

            if (order == null)
            {
                _logger.LogWarning("ConfirmPaymentAsync: Order {OrderId} not found for tenant {TenantId}", orderId, tenantId);
                throw new KeyNotFoundException($"Order {orderId} not found for tenant {tenantId}");
            }

            // Idempotency guard: if already paid, do not create duplicate accounting entries
            if (order.PaymentStatus == "Paid")
            {
                _logger.LogInformation("ConfirmPaymentAsync: Order {OrderId} already confirmed (idempotent noop)", orderId);
                return;
            }

            // 1. Mark order as paid (Domain method — immutability of AccountingEntry is preserved)
            // W0-T1 (C2): Pass PaymentMethod into ConfirmPayment so it is recorded on the order
            //   (was previously lost — Domain default "VIETQR" was always used).
            order.ConfirmPayment(transactionId, order.PaymentMethod ?? PaymentMethodConstants.Cash);
            await _orderRepository.UpdateAsync(order);
            await _orderRepository.SaveChangesAsync();

            // 2. Reload order with Items + Product for COGS calculation (C-3: use actual CostPrice)
            Order? orderWithItems = await _orderRepository.GetByIdWithIncludesAsync(orderId, cancellationToken);
            Order accountingOrder = orderWithItems ?? order; // fallback to plain order if reload fails

            // 3. Generate accounting entries (Revenue + COGS) — only after payment confirmed
            await GenerateAccountingEntriesAsync(accountingOrder, tenantIdObj);

            // W0-T5: Broadcast SignalR PaymentConfirmed notification to ShopERP staff (best-effort)
            // Null in ShopERP scope — in v2 edge mode, NATS → DataSyncSubscriber handles it.
            if (_orderNotificationService != null)
            {
                _ = _orderNotificationService.NotifyPaymentConfirmedAsync(order.Id, tenantId, transactionId);
            }

            _logger.LogInformation("ConfirmPaymentAsync: Payment confirmed for order {OrderId}, accounting entries generated", orderId);
        }

        /// <summary>
        /// W6/Bucket D: Public tracking — fetch by Id only (no tenant filter).
        /// OrderId is globally unique Guid. Used by KhachLink customer-facing tracking page.
        /// Reuses GetByIdWithIncludesAsync (fetches by Id only, includes Items+Product+Customer).
        /// </summary>
        public async Task<Order?> GetOrderByIdForPublicTrackingAsync(Guid orderId, CancellationToken cancellationToken = default)
        {
            return await _orderRepository.GetByIdWithIncludesAsync(orderId, cancellationToken);
        }
    }
}
