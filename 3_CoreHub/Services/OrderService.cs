using Microsoft.Extensions.Logging;
using VanAn.CoreHub.Repositories;
using VanAn.CoreHub.Interfaces;
using VanAn.Shared.Domain;
using VanAn.CoreHub.Commands;

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
        IOrderHub? orderHub = null) : IOrderService
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
        /// Create new order with accounting integration
        /// Phase 2.2: Order to Accounting Integration
        /// </summary>
        public async Task<Order> CreateOrderAsync(Order order, Guid tenantId)
        {
            TenantId tenant = new(tenantId);

            try
            {
                // 1. Create order using repository
                Order newOrder = await _orderRepository.AddAsync(order);

                // 2. Generate accounting entries
                await GenerateAccountingEntriesAsync(newOrder, tenant);

                _logger.LogInformation("Created order {OrderId} with accounting integration for tenant {TenantId}",
                    newOrder.Id, tenantId);

                return newOrder;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating order with accounting integration for tenant {TenantId}", tenantId);
                throw;
            }
        }

        /// <summary>
        /// Generate accounting entries for order
        /// Phase 2.2: Order to Accounting Integration
        /// </summary>
        private async Task GenerateAccountingEntriesAsync(Order order, TenantId tenantId)
        {
            AccountingPeriod period = AccountingPeriod.Create(DateTime.UtcNow.Year, DateTime.UtcNow.Month);

            try
            {
                // 1. Revenue entry using IAccountingService
                Shared.DTOs.AccountingEntryDto revenueEntry = await _accountingService.CreateRevenueEntryAsync(
                    tenantId,
                    period,
                    order.TotalPrice,
                    $"Doanh thu bán hàng #{order.Id}");

                // 2. Generate HKD books for revenue
                // Note: For MVP, we'll create a simple journal entry for HKD books
                JournalEntry revenueJournalEntry = await CreateRevenueEntryAsync(order, tenantId, period);
                // Use appropriate HKD book types based on business type
                await _hkdBookRepository.AddToBookAsync(revenueJournalEntry, AccountingBookType.S2b_HKD); // Revenue book
                await _hkdBookRepository.AddToBookAsync(revenueJournalEntry, AccountingBookType.S2c_HKD); // Detailed book

                // 3. COGS entry (simplified for MVP)
                decimal cogsAmount = order.TotalPrice * 0.7m; // Assume 70% COGS for MVP
                if (cogsAmount > 0)
                {
                    Shared.DTOs.AccountingEntryDto cogsEntry = await _accountingService.CreateExpenseEntryAsync(
                        tenantId,
                        period,
                        cogsAmount,
                        $"Giá vốn hàng bán #{order.Id}");

                    // Create COGS journal entry for HKD books
                    JournalEntry? cogsJournalEntry = await CreateCOGSEntryAsync(order, tenantId, period);
                    if (cogsJournalEntry != null)
                    {
                        // Use appropriate HKD book types based on business type
                        await _hkdBookRepository.AddToBookAsync(cogsJournalEntry, AccountingBookType.S2c_HKD); // Detailed book
                        await _hkdBookRepository.AddToBookAsync(cogsJournalEntry, AccountingBookType.S2d_HKD); // Materials book
                    }
                }

                _logger.LogInformation("Generated accounting entries for order {OrderId}", order.Id);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating accounting entries for order {OrderId}", order.Id);
                throw;
            }
        }

        /// <summary>
        /// Create revenue accounting entry
        /// Phase 2.2: Order to Accounting Integration
        /// </summary>
        private static async Task<JournalEntry> CreateRevenueEntryAsync(Order order, TenantId tenantId, AccountingPeriod period)
        {
            string description = $"Doanh thu bán hàng #{order.Id}";

            // Create journal entry with revenue lines
            JournalEntry journalEntry = new(
                tenantId,
                DateTime.UtcNow,
                description,
                "Order",
                order.Id
            );

            // Add revenue line (debit cash/bank, credit revenue)
            // Simplified for MVP - using standard Vietnamese accounts
            journalEntry.AddLine("111", order.TotalPrice, 0, "Tiền mặt thu từ bán hàng"); // Cash
            journalEntry.AddLine("511", 0, order.TotalPrice, "Doanh thu bán hàng"); // Revenue

            return journalEntry;
        }

        /// <summary>
        /// Create COGS accounting entry
        /// Phase 2.2: Order to Accounting Integration
        /// </summary>
        private static async Task<JournalEntry?> CreateCOGSEntryAsync(Order order, TenantId tenantId, AccountingPeriod period)
        {
            // Simplified COGS calculation for MVP
            // In real implementation, this would calculate based on actual inventory costs
            decimal cogsAmount = order.TotalPrice * 0.7m; // Assume 70% COGS for MVP

            if (cogsAmount <= 0)
            {
                return null;
            }

            string description = $"Giá vốn hàng bán #{order.Id}";

            JournalEntry journalEntry = new(
                tenantId,
                DateTime.UtcNow,
                description,
                "Order",
                order.Id
            );

            // Add COGS lines (debit COGS, credit inventory)
            journalEntry.AddLine("632", cogsAmount, 0, "Giá vốn hàng bán"); // COGS
            journalEntry.AddLine("156", 0, cogsAmount, "Giảm hàng tồn kho"); // Inventory

            return journalEntry;
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

                // Generate accounting entries
                await GenerateAccountingEntriesAsync(savedOrder, tenant);

                // Create HKD books
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

            AccountingPeriod period = AccountingPeriod.Create(DateTime.UtcNow.Year, DateTime.UtcNow.Month);

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
    }
}
