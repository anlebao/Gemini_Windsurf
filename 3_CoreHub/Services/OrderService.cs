using Microsoft.Extensions.Logging;
using Microsoft.EntityFrameworkCore;
using VanAn.CoreHub.Repositories;
using VanAn.CoreHub.Interfaces;
using VanAn.CoreHub.Infrastructure;
using VanAn.CoreHub.Infrastructure.Messaging;
using VanAn.CoreHub.Common;
using VanAn.Shared.Domain;
using UUIDNext;
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
        IOrderNotificationService? orderNotificationService = null,
        IShopFeatureSettingsService? shopFeatureSettingsService = null,
        IOutboxRepository? outboxRepository = null,
        IProductRepository? productRepository = null) : IOrderService
    {
        // EXISTING DEPENDENCIES (keep)
        private readonly IOrderRepository _orderRepository = orderRepository;
        private readonly IAccountingService _accountingService = accountingService;
        private readonly IHKDBookRepository _hkdBookRepository = hkdBookRepository;
        private readonly IAccountingEntryRepository _accountingEntryRepository = accountingEntryRepository;
        private readonly ILogger<OrderService> _logger = logger;
        // W2-T6: Shop feature settings — for accounting sync toggle bypass
        private readonly IShopFeatureSettingsService? _shopFeatureSettingsService = shopFeatureSettingsService;

        // NEW DEPENDENCIES
        private readonly IInventoryService _inventoryService = inventoryService;
        private readonly ITemplateFactory _templateFactory = templateFactory;
        private readonly IOrderHub _orderHub = orderHub;

        // Wave 5: DbContext for Tenant.DefaultIndustrySector lookup (Order.IndustrySector ?? Tenant.DefaultIndustrySector)
        private readonly IVanAnDbContext? _dbContext = dbContext;

        // RC-7: Product repository for snapshotting ProductName + VatRate into OrderItem at creation time.
        // TT 152/2025/TT-BTC: VAT must come from server-side Product entity, not client claim.
        private readonly IProductRepository? _productRepository = productRepository;

        // W0-T5: SignalR notification service (null in ShopERP scope — Gateway has OrderHub)
        private readonly IOrderNotificationService? _orderNotificationService = orderNotificationService;

        // Sync: Outbox for publishing OrderCreated events (Gateway → NATS → ShopERP SQLite)
        private readonly IOutboxRepository? _outboxRepository = outboxRepository;

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

        /// <summary>SystemAdmin: Get ALL orders across all tenants (no tenant filter).</summary>
        public async Task<IEnumerable<Order>> GetAllOrdersByDateRangeAsync(DateTime startDate, DateTime endDate)
        {
            if (_dbContext == null)
                return new List<Order>();
            return await _dbContext.Orders
                .IgnoreQueryFilters()
                .Where(o => o.CreatedAt.Date >= startDate.Date && o.CreatedAt.Date <= endDate.Date)
                .OrderByDescending(o => o.CreatedAt)
                .ToListAsync();
        }

        /// <summary>SystemAdmin: Get ALL orders by status across all tenants.</summary>
        public async Task<List<Order>> GetAllOrdersByStatusAsync(OrderStatusId status)
        {
            if (_dbContext == null)
                return new List<Order>();
            return await _dbContext.Orders
                .IgnoreQueryFilters()
                .Where(o => o.Status == status)
                .OrderByDescending(o => o.CreatedAt)
                .ToListAsync();
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
        /// <summary>
        /// Phase 3.5: Made PUBLIC for PaymentConfirmedSubscriber to call after receiving NATS event.
        /// Creates Revenue + COGS accounting entries for an already-Paid order.
        /// Idempotent: caller should check JournalEntry.Reference before calling (subscriber does this).
        /// </summary>
        public async Task GenerateAccountingEntriesAsync(Order order, TenantId tenantId)
        {
            // W0-T6 (H4): Use OrderDate (not UtcNow) — entry belongs to the period when order was placed.
            AccountingPeriod period = AccountingPeriod.Create(order.OrderDate.Year, order.OrderDate.Month);
            string orderRef = order.Id.ToString();

            try
            {
                // Wave 5: Resolve industry sector — per-order override falls back to Tenant default
                // Phase 3.5 fix: Select only DefaultIndustrySector (not full Tenant entity) to avoid
                // SQLite "no such column: t.ShopInstanceId" error (ShopInstanceId is PG-only column).
                IndustrySector? sector = order.IndustrySector;
                if (sector == null && _dbContext != null)
                {
                    sector = await _dbContext.Tenants
                        .Where(t => t.Id == tenantId)
                        .Select(t => t.DefaultIndustrySector)
                        .FirstOrDefaultAsync();
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
                // W-FIX (Payment Webhook 500 root cause): AddToBookAsync persists the JournalEntry
                // to the DB. Calling it twice with the SAME entity instance (once for S2b, once for
                // S2c) triggers SQLite UNIQUE constraint violation on JournalNo — the second insert
                // is rejected because the row already exists. Current AddToBookAsync does not
                // differentiate by bookType (see HKDBookRepository comment) — it simply persists
                // the entry. Book membership for multiple book types (S2b + S2c) will be tracked
                // via a mapping table in a future implementation. For now, call ONCE per entry.
                await _hkdBookRepository.AddToBookAsync(revenueJournalEntry, AccountingBookType.S2b_HKD); // Revenue book (also covers S2c_HKD — detailed book)

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

            OrderStatusId oldStatus = order.Status;
            order.UpdateOrderStatus(new OrderStatusId(newStatus));

            _ = await _orderRepository.UpdateAsync(order);

            // Enqueue Outbox event so NatsSyncWorker publishes "vanan.shoperp.order.status.changed"
            // → Gateway DataSyncSubscriber updates PostgreSQL → KhachLink OrderTracking sees new status.
            if (_outboxRepository != null)
            {
                var payload = new
                {
                    orderId = order.Id,
                    tenantId = order.TenantId.Value,
                    oldStatus = oldStatus.Value,
                    newStatus = newStatus,
                    timestamp = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ")
                };
                string eventData = System.Text.Json.JsonSerializer.Serialize(payload);
                var outboxEvent = new OutboxEvent(
                    order.TenantId,
                    new ElectronicInvoiceId(Guid.Empty),
                    "OrderStatusChanged",
                    eventData);
                await _outboxRepository.EnqueueAsync(outboxEvent);
            }
            else
            {
                _logger.LogWarning("OutboxRepository not available — OrderStatusChanged event for order {OrderId} not persisted", orderId);
            }

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
        /// Phase 3 (Multi-VPS Checkout): Uses client-provided ProductName + VatRate snapshot.
        /// Falls back to LoadProductsForSnapshotAsync when ProductName is empty (legacy callers).
        /// routingKey: when set, Outbox event NATS subject includes the routing key (ShopInstanceId).
        /// </summary>
        public async Task<Order> CreateOrderFromCommandAsync(CreateOrderCommand command, Guid tenantId, string? routingKey = null)
        {
            try
            {
                // Create domain entity using DDD compliant factory methods
                Guid orderId = Uuid.NewDatabaseFriendly(Database.PostgreSql);
                TenantId tenantIdObj = new(tenantId);

                // Phase 3: Use client-provided snapshot (ProductName + VatRate) when available.
                // Only load products from DB when the client didn't provide a snapshot (legacy fallback).
                // This eliminates the broken PG product lookup that caused the KhachLink checkout bug.
                bool needsProductLookup = command.Items.Any(i => string.IsNullOrEmpty(i.ProductName));
                Dictionary<Guid, Product> productLookup = needsProductLookup
                    ? await LoadProductsForSnapshotAsync(command.Items, tenantIdObj)
                    : [];

                // Create OrderItems using DDD factory methods — pass snapshot ProductName + VatRate.
                List<OrderItem> orderItems = [];
                foreach (var i in command.Items)
                {
                    string productName = i.ProductName;
                    decimal vatRate = i.VatRate;

                    // Backward compat: if client didn't provide snapshot, load from DB
                    if (string.IsNullOrEmpty(productName))
                    {
                        if (!productLookup.TryGetValue(i.ProductId, out Product? product))
                        {
                            throw new KeyNotFoundException(
                                $"Product {i.ProductId} not found for tenant {tenantId}. Order creation aborted — " +
                                "cannot snapshot ProductName/VatRate. Ensure the product exists before checkout " +
                                "OR provide ProductName + VatRate in the command (Phase 3 client snapshot).");
                        }
                        productName = product.Name;
                        vatRate = product.VatRate;
                    }

                    orderItems.Add(OrderItem.Create(Guid.NewGuid(), tenantIdObj, orderId, i.ProductId, i.Quantity, i.UnitPrice, productName, vatRate));
                }

                // Create Order using DDD factory method.
                // customerId: when the checkout is performed by a logged-in customer (KhachLink
                // sends command.CustomerId from localStorage), pass it so the order is linked to
                // the Customer entity and appears in /api/customerorders history.
                // For anonymous/guest checkout, CustomerId is null — only CustomerDeviceId is set.
                // Previously command.CustomerDeviceId (Guid) was passed as customerId, causing
                // FK_Orders_Customers_CustomerId violations for guest checkout (no Customer row exists).
                // Bucket A feature fix (approved 2026-07-07): pass null customerId, set device id separately.
                Guid? customerId = command.CustomerId;

                // Bug 1a fix (2026-07-17): Validate CustomerId exists in DB before passing to Order.Create.
                // If Customer entity doesn't exist (e.g., user logged in via OAuth but no Customer row was
                // created), set CustomerId to null to avoid FK_Orders_Customers_CustomerId violation.
                // Order will still be created and linked via CustomerDeviceId for order history.
                if (customerId.HasValue && _dbContext != null)
                {
                    bool customerExists = await _dbContext.Customers
                        .AnyAsync(c => c.Id == customerId.Value);
                    if (!customerExists)
                    {
                        _logger.LogWarning(
                            "Checkout: CustomerId {CustomerId} not found in DB — falling back to guest mode (CustomerId=null). " +
                            "Order will be linked via CustomerDeviceId only.",
                            customerId.Value);
                        customerId = null;
                    }
                }

                Order order = Order.Create(orderId, tenantIdObj, customerId, orderItems);
                order.SetCustomerDeviceId(command.CustomerDeviceId.ToString());

                // Bucket A feature (approved 2026-07-07): attach guest customer info if provided.
                // CustomerInfo value object is persisted via OwnsOne (columns already exist).
                if (!string.IsNullOrWhiteSpace(command.CustomerName)
                    || !string.IsNullOrWhiteSpace(command.CustomerPhone)
                    || !string.IsNullOrWhiteSpace(command.CustomerAddress))
                {
                    var info = new CustomerInfo(
                        command.CustomerName ?? string.Empty,
                        command.CustomerPhone ?? string.Empty,
                        email: string.Empty,
                        address: command.CustomerAddress);
                    order.SetCustomerInfo(info);
                }

                // Save order + Outbox event atomically (RC-1 fix: single transaction).
                // Previously: AddAsync (SaveChangesAsync) then EnqueueAsync + SaveChangesAsync (2nd save).
                // If 2nd save failed, order was committed but Outbox event was lost → sync never runs.
                using Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction transaction =
                    await _orderRepository.BeginTransactionAsync();

                Order createdOrder = await _orderRepository.AddAsyncNoSave(order);

                // Sync: Enqueue OrderCreated event to Outbox for NATS → ShopERP SQLite sync
                // Gateway writes to PostgreSQL; ShopERP reads from SQLite (Owner UI).
                // Without this, ShopERP Owner cannot see orders created via Gateway (KhachLink checkout).
                if (_outboxRepository != null)
                {
                    var orderCreatedEvent = new
                    {
                        EventId = Guid.NewGuid(),
                        OrderId = createdOrder.Id,
                        TenantId = createdOrder.TenantId.Value,
                        CustomerId = createdOrder.CustomerId,
                        CustomerDeviceId = createdOrder.CustomerDeviceId ?? string.Empty,
                        Status = createdOrder.Status.Value,
                        TotalAmount = createdOrder.TotalAmount,
                        SubTotal = createdOrder.SubTotal,
                        TotalVatAmount = createdOrder.TotalVatAmount,
                        PaymentStatus = createdOrder.PaymentStatus,
                        OrderType = createdOrder.OrderType ?? "DineIn",
                        OrderDate = createdOrder.OrderDate,
                        CreatedAt = createdOrder.CreatedAt,
                        TrackingCode = createdOrder.TrackingCode,
                        CustomerInfo = new
                        {
                            FullName = createdOrder.CustomerInfo?.FullName ?? "",
                            PhoneNumber = createdOrder.CustomerInfo?.PhoneNumber ?? "",
                            Email = createdOrder.CustomerInfo?.Email ?? "",
                            Address = createdOrder.CustomerInfo?.Address ?? ""
                        },
                        Items = createdOrder.Items.Select(i => new
                        {
                            ItemId = i.Id,
                            ProductId = i.ProductId,
                            // RC-7: ProductName is now snapshot at creation — no "Unknown" fallback needed.
                            ProductName = i.ProductName,
                            Quantity = i.Quantity,
                            UnitPrice = i.UnitPrice,
                            TotalAmount = i.TotalAmount,
                            VatRate = i.VatRate
                        }).ToList()
                    };

                    string eventData = System.Text.Json.JsonSerializer.Serialize(orderCreatedEvent);
                    var outboxEvent = new OutboxEvent(
                        createdOrder.TenantId,
                        new ElectronicInvoiceId(Guid.Empty),
                        "OrderCreated",
                        eventData,
                        routingKey);
                    await _outboxRepository.EnqueueAsync(outboxEvent);
                    _logger.LogInformation("Enqueued OrderCreated event to Outbox for order {OrderId}", createdOrder.Id);
                }
                else
                {
                    _logger.LogWarning("OutboxRepository not available — OrderCreated event for order {OrderId} not persisted", createdOrder.Id);
                }

                // Single SaveChangesAsync commits both order + outbox event atomically.
                if (_dbContext != null)
                {
                    await _dbContext.SaveChangesAsync();
                }
                await transaction.CommitAsync();

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
        /// RC-7: Load Product entities for all distinct ProductIds in the command to snapshot
        /// ProductName + VatRate into OrderItem. Uses IProductRepository when available (preferred —
        /// tenant-filtered). Falls back to IVanAnDbContext.Products query when repository is null
        /// but DbContext is available. Returns empty dictionary when neither is wired (legacy tests).
        /// </summary>
        private async Task<Dictionary<Guid, Product>> LoadProductsForSnapshotAsync(
            List<OrderItemRequest> items, TenantId tenantIdObj)
        {
            if (items.Count == 0) return [];

            HashSet<Guid> productIds = items.Select(i => i.ProductId).ToHashSet();

            // Path A: IProductRepository (preferred — explicit tenant filter via GetByIdAsync).
            if (_productRepository != null)
            {
                var lookup = new Dictionary<Guid, Product>();
                foreach (Guid pid in productIds)
                {
                    Product? product = await _productRepository.GetByIdAsync(new ProductId(pid), tenantIdObj);
                    if (product != null) lookup[pid] = product;
                }
                return lookup;
            }

            // Path B: IVanAnDbContext (fallback — global query filter applies tenant scope).
            if (_dbContext != null)
            {
                List<Product> products = await _dbContext.Products
                    .Where(p => productIds.Contains(p.Id))
                    .ToListAsync();
                return products.ToDictionary(p => p.Id);
            }

            // Path C: neither wired (legacy unit tests without product snapshot) — return empty.
            _logger.LogWarning("LoadProductsForSnapshotAsync: neither IProductRepository nor IVanAnDbContext available — OrderItem ProductName/VatRate will use factory defaults");
            return [];
        }
        /// <summary>
        /// Phase 3.5: MarkPaidAsync — sets order status=Paid + optionally enqueues OrderPaymentConfirmed Outbox event.
        /// Called by Gateway WebhookController (enqueuePaymentConfirmedEvent=true) → NATS → ShopERP PaymentConfirmedSubscriber.
        /// Does NOT create accounting entries (those are created by ShopERP subscriber via GenerateAccountingEntriesAsync).
        /// Idempotent: second call for same orderId returns without duplicate action.
        /// </summary>
        public async Task MarkPaidAsync(Guid orderId, Guid tenantId, string transactionId, bool enqueuePaymentConfirmedEvent = false, CancellationToken cancellationToken = default)
        {
            TenantId tenantIdObj = new(tenantId);
            OrderId orderIdObj = new(orderId);

            Order? order = await _orderRepository.GetByIdAsync(orderIdObj, tenantIdObj);

            if (order == null)
            {
                _logger.LogWarning("MarkPaidAsync: Order {OrderId} not found for tenant {TenantId}", orderId, tenantId);
                throw new KeyNotFoundException($"Order {orderId} not found for tenant {tenantId}");
            }

            // Idempotency guard: if already paid, do not enqueue duplicate event
            if (order.PaymentStatus == "Paid")
            {
                _logger.LogInformation("MarkPaidAsync: Order {OrderId} already paid (idempotent noop)", orderId);
                return;
            }

            // 1. Mark order as paid (Domain method)
            order.ConfirmPayment(transactionId, order.PaymentMethod ?? PaymentMethodConstants.Cash);
            await _orderRepository.UpdateAsync(order);
            await _orderRepository.SaveChangesAsync();

            // 2. Enqueue OrderPaymentConfirmed Outbox event (if requested by Gateway webhook)
            if (enqueuePaymentConfirmedEvent && _outboxRepository != null)
            {
                // Phase 3.5: Lookup ShopInstanceId for routing key
                string? routingKey = null;
                if (_dbContext != null)
                {
                    var shopInstanceId = await _dbContext.Tenants
                        .IgnoreQueryFilters()
                        .Where(t => t.Id == tenantIdObj && t.ShopInstanceId.HasValue)
                        .Select(t => t.ShopInstanceId!.Value)
                        .FirstOrDefaultAsync(cancellationToken);
                    if (shopInstanceId != Guid.Empty)
                        routingKey = shopInstanceId.ToString();
                }

                var paymentConfirmedEvent = new
                {
                    EventId = Guid.NewGuid(),
                    OrderId = order.Id,
                    TenantId = tenantId,
                    TransactionId = transactionId,
                    PaymentMethod = order.PaymentMethod ?? PaymentMethodConstants.Cash,
                    PaidAt = DateTime.UtcNow
                };

                string eventData = System.Text.Json.JsonSerializer.Serialize(paymentConfirmedEvent);
                var outboxEvent = new OutboxEvent(
                    tenantIdObj,
                    new ElectronicInvoiceId(Guid.Empty),
                    "OrderPaymentConfirmed",
                    eventData,
                    routingKey);
                await _outboxRepository.EnqueueAsync(outboxEvent);
                await _orderRepository.SaveChangesAsync();
                _logger.LogInformation("MarkPaidAsync: Enqueued OrderPaymentConfirmed event for order {OrderId}, routingKey={RoutingKey}", orderId, routingKey ?? "(none)");
            }

            // 3. Broadcast SignalR PaymentConfirmed notification (best-effort — customer sees "Paid" immediately)
            if (_orderNotificationService != null)
            {
                _ = _orderNotificationService.NotifyPaymentConfirmedAsync(order.Id, tenantId, transactionId);
            }

            _logger.LogInformation("MarkPaidAsync: Order {OrderId} marked as Paid", orderId);
        }

        /// <summary>
        /// Sprint B: Payment confirmation — triggers accounting entry generation
        /// Called by WebhookController after bank/VietQR confirms payment.
        /// Idempotent: second call for same orderId returns without creating duplicate entries.
        /// TT 152/2025/TT-BTC: doanh thu ghi nhận theo thực thu (cash-basis accounting).
        /// Phase 3.5: Now a backward-compat wrapper — calls MarkPaidAsync (no event) + GenerateAccountingEntriesAsync.
        /// POS Payment.razor uses this (entries created locally in SQLite, no NATS event needed).
        /// </summary>
        public async Task ConfirmPaymentAsync(Guid orderId, Guid tenantId, string transactionId, CancellationToken cancellationToken = default)
        {
            TenantId tenantIdObj = new(tenantId);
            OrderId orderIdObj = new(orderId);

            // Phase 3.5: Check idempotency BEFORE calling MarkPaidAsync.
            // If order is already Paid, this is a duplicate call → skip entirely (no MarkPaid, no accounting).
            Order? existingOrder = await _orderRepository.GetByIdAsync(orderIdObj, tenantIdObj, cancellationToken);
            if (existingOrder == null)
            {
                _logger.LogWarning("ConfirmPaymentAsync: Order {OrderId} not found for tenant {TenantId}", orderId, tenantId);
                throw new KeyNotFoundException($"Order {orderId} not found for tenant {tenantId}");
            }

            if (existingOrder.PaymentStatus == "Paid")
            {
                _logger.LogInformation("ConfirmPaymentAsync: Order {OrderId} already paid (idempotent noop)", orderId);
                return;
            }

            // Phase 3.5: Delegate to MarkPaidAsync (no Outbox event — POS creates entries locally)
            await MarkPaidAsync(orderId, tenantId, transactionId, enqueuePaymentConfirmedEvent: false, cancellationToken);

            // Reload order with Items + Product for COGS calculation (C-3: use actual CostPrice)
            Order? orderWithItems = await _orderRepository.GetByIdWithIncludesAsync(orderId, cancellationToken);
            if (orderWithItems == null) return;
            Order accountingOrder = orderWithItems;

            // Generate accounting entries (Revenue + COGS) — only after payment confirmed
            // W2-T6: Skip if Accounting_Sync_Enabled toggle is OFF for this tenant
            bool accountingEnabled = true;
            if (_shopFeatureSettingsService != null)
            {
                try
                {
                    accountingEnabled = await _shopFeatureSettingsService.IsEnabledAsync(
                        tenantId,
                        nameof(ShopFeatureSettingsDto.Accounting_Sync_Enabled));
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "ConfirmPaymentAsync: Failed to fetch Accounting_Sync_Enabled toggle for tenant {TenantId} — defaulting to ON", tenantId);
                }
            }

            if (accountingEnabled)
            {
                // ISSUE #5 FIX: Wrap accounting entry generation in try-catch.
                // Order status is already saved as Paid. Accounting entries are
                // secondary — if they fail, the payment confirmation should still succeed.
                try
                {
                    await GenerateAccountingEntriesAsync(accountingOrder, tenantIdObj);
                }
                catch (Exception ex) when (ex is not KeyNotFoundException)
                {
                    _logger.LogError(ex, "ConfirmPaymentAsync: Accounting entry generation failed for order {OrderId} — order is already marked Paid. Accounting entries will need manual reconciliation.", orderId);
                }
            }
            else
            {
                _logger.LogInformation("ConfirmPaymentAsync: Accounting sync disabled for tenant {TenantId} — skipping entry generation for order {OrderId}", tenantId, orderId);
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
            // Use IgnoreQueryFilters variant — public endpoint has no JWT/tenant context.
            // Safe: PublicOrderTrackingDto strips all sensitive fields before returning to client.
            return await _orderRepository.GetByIdWithIncludesIgnoreFiltersAsync(orderId, cancellationToken);
        }
    }
}
