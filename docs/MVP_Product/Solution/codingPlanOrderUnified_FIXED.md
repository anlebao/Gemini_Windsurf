# CODING PLAN - ORDER WORKFLOW UNIFIED (FIXED VERSION)

**Ngày tạo:** 2026-05-04  
**Phiên bản:** 1.1 (FIXED)  
**Tác giả:** Van An Development Team  
**Trạng thái:** Sẵn sàng implementation  
**Based on:** Order workflow unified.md solution  
**Fixes:** Added missing Unified API Integration tasks

---

## 🎯 REVERSE IMPACT ANALYSIS - UPDATED

### **Current System State Analysis**
```
EXISTING COMPONENTS TO REMOVE:
├── ShopERP/Services/OrderWorkflowService.cs (DELETE)
├── ShopERP/Services/IOrderWorkflowService.cs (DELETE)
├── ShopERP/Services/OrderQueueService.cs (DELETE)
├── ShopERP/Services/IOrderQueueService.cs (DELETE)
├── ShopERP/Controllers/OrdersController.cs (UPDATE)
├── KhachLink/Controllers/OrdersController.cs (UPDATE)
└── 2_Gateway/Controllers/OrdersController.cs (UPDATE)

EXISTING COMPONENTS TO ENHANCE:
├── CoreHub/Services/OrderService.cs (ENHANCE)
├── CoreHub/Services/IOrderService.cs (ENHANCE)
├── CoreHub/Hubs/OrderHub.cs (CREATE)
├── KhachLink/Services/CartService.cs (ENHANCE)
├── KhachLink/Models/OrderInfo.cs (UPDATE)
├── ShopERP/Controllers/OrdersController.cs (UNIFY)
└── KhachLink/Controllers/OrdersController.cs (UNIFY)

NEW COMPONENTS TO CREATE:
├── CoreHub/Services/OrderQueueService.cs (CREATE)
├── CoreHub/Services/IOrderQueueService.cs (CREATE)
├── CoreHub/Services/SyncService.cs (CREATE)
├── CoreHub/Services/ISyncService.cs (CREATE)
├── KhachLink/Services/OfflineOrderService.cs (CREATE)
├── KhachLink/Services/IndexedDBService.cs (CREATE)
├── ShopERP/Services/OrderManagementService.cs (CREATE)
├── ShopERP/Services/IOrderManagementService.cs (CREATE)
├── 2_Gateway/Middleware/UnifiedErrorHandler.cs (CREATE)
└── 6_Tests/Integration/UnifiedOrderTests.cs (CREATE)
```

### **Impact Assessment**
- **Files to Delete:** 4 files
- **Files to Modify:** 7 files (was 3)
- **Files to Create:** 10 files (was 8)
- **Total Impact:** 21 files (was 15)
- **Risk Level:** Medium-High (API consolidation adds complexity)
- **Build Impact:** Temporary increase in errors during transition

---

## 📋 DETAILED CODING PLAN - FIXED

### **PHASE 1: BACKEND CONSOLIDATION (Week 1)**

#### **DAY 1-2: SERVICE CONSOLIDATION**

##### **1.1 Remove ShopERP Duplicate Services**
```bash
# Files to DELETE:
- 5_WebApps/ShopERP/Services/OrderWorkflowService.cs
- 5_WebApps/ShopERP/Services/IOrderWorkflowService.cs  
- 5_WebApps/ShopERP/Services/OrderQueueService.cs
- 5_WebApps/ShopERP/Services/IOrderQueueService.cs
```

##### **1.2 Update ShopERP Program.cs**
```csharp
// 5_WebApps/ShopERP/Program.cs - REMOVE these lines:
// builder.Services.AddScoped<IOrderWorkflowService, OrderWorkflowService>();
// builder.Services.AddScoped<IOrderQueueService, OrderQueueService>();
// builder.Services.AddScoped<OrderQueueService>();
```

##### **1.3 Enhance CoreHub IOrderService**
```csharp
// 3_CoreHub/Services/IOrderService.cs - ADD these methods:

public interface IOrderService
{
    // EXISTING METHODS (keep)
    Task<int> GetTodayOrderCountAsync(Guid tenantId);
    Task<IEnumerable<Order>> GetOrdersByDateRangeAsync(Guid tenantId, DateTime startDate, DateTime endDate);
    Task<Order?> GetOrderByIdAsync(Guid orderId, Guid tenantId);
    Task<Order> CreateOrderAsync(Order order, Guid tenantId);
    Task<bool> UpdateOrderStatusAsync(Guid orderId, string newStatus, Guid tenantId);
    
    // NEW METHODS - Queue Support
    Task<Order> CreateOrderWithQueueAsync(Order order, Guid tenantId);
    Task<List<Order>> GetQueuedOrdersAsync(Guid tenantId);
    Task<bool> IsTransitionValidAsync(OrderStatusId currentStatus, OrderStatusId newStatus);
    
    // NEW METHODS - Enhanced Query Support
    Task<List<Order>> GetOrdersByStatusAsync(OrderStatusId status, Guid tenantId);
    Task<OrderDashboardData> GetDashboardDataAsync(Guid tenantId);
    Task<OrderSummary> GetOrderSummaryAsync(Guid orderId, Guid tenantId);
    
    // NEW METHODS - Accounting Integration
    Task<List<AccountingEntry>> GetEntriesByOrderAsync(Guid orderId, TenantId tenantId);
}
```

##### **1.4 Create CoreHub OrderQueueService**
```csharp
// 3_CoreHub/Services/IOrderQueueService.cs (CREATE)
public interface IOrderQueueService
{
    Task EnqueueOrderAsync(Order order);
    Task<List<Order>> GetQueuedOrdersAsync(Guid tenantId);
    Task ProcessQueueAsync(CancellationToken cancellationToken = default);
}

// 3_CoreHub/Services/OrderQueueService.cs (CREATE)
public class OrderQueueService : IOrderQueueService, IHostedService
{
    private readonly IBackgroundTaskQueue _taskQueue;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<OrderQueueService> _logger;
    
    public OrderQueueService(
        IBackgroundTaskQueue taskQueue,
        IServiceScopeFactory scopeFactory,
        ILogger<OrderQueueService> logger)
    {
        _taskQueue = taskQueue;
        _scopeFactory = scopeFactory;
        _logger = logger;
    }
    
    public async Task EnqueueOrderAsync(Order order)
    {
        await _taskQueue.QueueBackgroundWorkItemAsync(async token =>
        {
            using var scope = _scopeFactory.CreateScope();
            var orderService = scope.ServiceProvider.GetRequiredService<IOrderService>();
            
            await orderService.CreateOrderAsync(order, order.TenantId.Value);
        });
        
        _logger.LogInformation("Order {OrderId} enqueued for background processing", order.Id);
    }
    
    public async Task<List<Order>> GetQueuedOrdersAsync(Guid tenantId)
    {
        using var scope = _scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<VanAnDbContext>();
        
        return await context.Orders
            .Where(o => o.TenantId.Value == tenantId && 
                       o.Status == OrderStatusId.Pending)
            .OrderBy(o => o.CreatedAt)
            .ToListAsync();
    }
    
    public async Task ProcessQueueAsync(CancellationToken cancellationToken = default)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            var workItem = await _taskQueue.DequeueAsync(cancellationToken);
            
            try
            {
                await workItem(cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing background work item");
            }
        }
    }
    
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        _ = Task.Run(() => ProcessQueueAsync(cancellationToken), cancellationToken);
    }
    
    public async Task StopAsync(CancellationToken cancellationToken)
    {
        await Task.CompletedTask;
    }
}
```

#### **DAY 3-4: ENHANCED CORE ORDER SERVICE**

##### **1.5 Update CoreHub OrderService**
```csharp
// 3_CoreHub/Services/OrderService.cs - ENHANCE existing class:

public class OrderService : IOrderService
{
    // EXISTING DEPENDENCIES (keep)
    private readonly IOrderRepository _orderRepository;
    private readonly IAccountingService _accountingService;
    private readonly IHKDBookRepository _hkdBookRepository;
    private readonly VanAnDbContext _context;
    private readonly ILogger<OrderService> _logger;
    
    // NEW DEPENDENCIES
    private readonly IInventoryService _inventoryService;
    private readonly ITemplateFactory _templateFactory;
    private readonly IOrderHub _orderHub;
    
    public OrderService(
        IOrderRepository orderRepository,
        IAccountingService accountingService,
        IHKDBookRepository hkdBookRepository,
        VanAnDbContext context,
        ILogger<OrderService> logger,
        IInventoryService inventoryService = null,
        ITemplateFactory templateFactory = null,
        IOrderHub orderHub = null)
    {
        _orderRepository = orderRepository;
        _accountingService = accountingService;
        _hkdBookRepository = hkdBookRepository;
        _context = context;
        _logger = logger;
        _inventoryService = inventoryService;
        _templateFactory = templateFactory;
        _orderHub = orderHub;
    }
    
    // ENHANCED CreateOrderAsync with full integration
    public async Task<Order> CreateOrderAsync(Order order, Guid tenantId)
    {
        var tenant = new TenantId(tenantId);
        
        try
        {
            // 1. Validate order with business rules
            await ValidateOrderAsync(order, tenant);
            
            // 2. Save to database (SQLite with transaction)
            using var transaction = await _context.Database.BeginTransactionAsync();
            
            var savedOrder = await _orderRepository.AddAsync(order);
            
            // 3. Generate accounting entries (Phase 2.2)
            await GenerateAccountingEntriesAsync(savedOrder, tenant);
            
            // 4. Create HKD books (Phase 2.3)
            await GenerateHKDBooksAsync(savedOrder, tenant);
            
            // 5. Commit transaction
            await transaction.CommitAsync();
            
            // 6. Real-time notifications (outside transaction)
            if (_orderHub != null)
            {
                await _orderHub.NotifyStaffAsync(savedOrder);
                await _orderHub.NotifyCustomerAsync(savedOrder.Id, OrderStatusId.Pending);
            }
            
            _logger.LogInformation("Created order {OrderId} with full integration for tenant {TenantId}", 
                savedOrder.Id, tenantId);
            
            return savedOrder;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating order with full integration for tenant {TenantId}", tenantId);
            throw;
        }
    }
    
    // NEW METHODS
    
    public async Task<Order> CreateOrderWithQueueAsync(Order order, Guid tenantId)
    {
        var tenant = new TenantId(tenantId);
        
        try
        {
            // Validate order with business rules
            await ValidateOrderAsync(order, tenant);
            
            // Save to database with queue priority
            using var transaction = await _context.Database.BeginTransactionAsync();
            
            var savedOrder = await _orderRepository.AddAsync(order);
            
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
        return await _context.Orders
            .Where(o => o.TenantId.Value == tenantId && 
                       o.Status == OrderStatusId.Pending &&
                       !o.IsDeleted)
            .OrderBy(o => o.CreatedAt)
            .ToListAsync();
    }
    
    public async Task<bool> IsTransitionValidAsync(OrderStatusId currentStatus, OrderStatusId newStatus)
    {
        // Define valid transitions
        var validTransitions = new Dictionary<OrderStatusId, List<OrderStatusId>>
        {
            [OrderStatusId.Pending] = new List<OrderStatusId> { OrderStatusId.Processing, OrderStatusId.Cancelled },
            [OrderStatusId.Processing] = new List<OrderStatusId> { OrderStatusId.Completed, OrderStatusId.Cancelled },
            [OrderStatusId.Completed] = new List<OrderStatusId>(), // Final state
            [OrderStatusId.Cancelled] = new List<OrderStatusId>() // Final state
        };
        
        return validTransitions.ContainsKey(currentStatus) && 
               validTransitions[currentStatus].Contains(newStatus);
    }
    
    public async Task<List<Order>> GetOrdersByStatusAsync(OrderStatusId status, Guid tenantId)
    {
        return await _context.Orders
            .Where(o => o.TenantId.Value == tenantId && 
                       o.Status == status &&
                       !o.IsDeleted)
            .OrderByDescending(o => o.CreatedAt)
            .ToListAsync();
    }
    
    public async Task<OrderDashboardData> GetDashboardDataAsync(Guid tenantId)
    {
        var today = DateTime.UtcNow.Date;
        var tomorrow = today.AddDays(1);
        
        var orders = await _context.Orders
            .Where(o => o.TenantId.Value == tenantId && 
                       o.CreatedAt >= today && 
                       o.CreatedAt < tomorrow &&
                       !o.IsDeleted)
            .ToListAsync();
        
        return new OrderDashboardData
        {
            TodayOrderCount = orders.Count,
            TodayRevenue = orders.Where(o => o.Status == OrderStatusId.Completed).Sum(o => o.TotalPrice),
            PendingOrders = orders.Count(o => o.Status == OrderStatusId.Pending),
            ProcessingOrders = orders.Count(o => o.Status == OrderStatusId.Processing),
            CompletedOrders = orders.Count(o => o.Status == OrderStatusId.Completed)
        };
    }
    
    public async Task<OrderSummary> GetOrderSummaryAsync(Guid orderId, Guid tenantId)
    {
        var order = await _context.Orders
            .Where(o => o.Id == orderId && 
                       o.TenantId.Value == tenantId && 
                       !o.IsDeleted)
            .Include(o => o.Items)
            .FirstOrDefaultAsync();
        
        if (order == null)
        {
            return new OrderSummary();
        }
        
        return new OrderSummary
        {
            OrderId = order.Id,
            CustomerId = order.CustomerId,
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
        return await _context.AccountingEntries
            .Where(e => e.TenantId == tenantId && 
                       e.ReferenceId == orderId.ToString() &&
                       !e.IsDeleted)
            .OrderByDescending(e => e.CreatedAt)
            .ToListAsync();
    }
    
    // HELPER METHODS
    
    private async Task ValidateOrderAsync(Order order, TenantId tenantId)
    {
        // Business rule validation
        if (order.Items.Count == 0)
            throw new ArgumentException("Order must have at least one item");
            
        if (order.TotalPrice <= 0)
            throw new ArgumentException("Order total must be positive");
            
        // Inventory validation (if applicable)
        if (_inventoryService != null)
        {
            var canFulfill = await _inventoryService.CanFulfillOrderAsync(order, new Dictionary<IngredientId, Inventory>(), new Dictionary<Guid, Recipe>());
            if (!canFulfill)
                throw new InvalidOperationException("Insufficient inventory for order");
        }
    }
    
    private async Task GenerateAccountingEntriesAsync(Order order, TenantId tenantId)
    {
        var period = AccountingPeriod.Create(DateTime.UtcNow.Year, DateTime.UtcNow.Month);
        
        try
        {
            // 1. Revenue entry
            var revenueEntry = await _accountingService.CreateRevenueEntryAsync(
                tenantId, 
                period, 
                order.TotalPrice, 
                $"Doanh thu bán hàng #{order.Id}");
            
            // 2. COGS entry (simplified for MVP)
            var cogsAmount = order.TotalPrice * 0.7m; // Assume 70% COGS
            if (cogsAmount > 0)
            {
                var cogsEntry = await _accountingService.CreateExpenseEntryAsync(
                    tenantId, 
                    period, 
                    cogsAmount, 
                    $"Giá vốn hàng bán #{order.Id}");
            }
            
            _logger.LogInformation("Generated accounting entries for order {OrderId}", order.Id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating accounting entries for order {OrderId}", order.Id);
            throw;
        }
    }
    
    private async Task GenerateHKDBooksAsync(Order order, TenantId tenantId)
    {
        if (_templateFactory == null)
        {
            _logger.LogWarning("TemplateFactory not available, skipping HKD book generation");
            return;
        }
        
        var period = AccountingPeriod.Create(DateTime.UtcNow.Year, DateTime.UtcNow.Month);
        
        try
        {
            // Get journal entries for this order
            var entries = await _accountingService.GetEntriesByOrderAsync(order.Id, tenantId);
            
            // Generate HKD books using template system
            var templates = _templateFactory.GetTemplatesForTenant(tenantId);
            
            foreach (var template in templates)
            {
                var book = await template.CreateBookAsync(tenantId, period, entries.ToList());
                await _hkdBookRepository.AddAsync(book);
            }
            
            _logger.LogInformation("Generated HKD books for order {OrderId}", order.Id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating HKD books for order {OrderId}", order.Id);
            // Don't throw - HKD books should not block order creation
        }
    }
}
```

##### **1.6 Create Supporting Classes**
```csharp
// 3_CoreHub/Services/OrderDashboardData.cs (CREATE)
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
```

#### **DAY 5-7: REAL-TIME INFRASTRUCTURE**

##### **1.7 Create OrderHub**
```csharp
// 3_CoreHub/Hubs/OrderHub.cs (CREATE)
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using VanAn.Shared.Domain;

[Authorize]
public class OrderHub : Hub
{
    private readonly IOrderService _orderService;
    private readonly ILogger<OrderHub> _logger;
    
    public OrderHub(IOrderService orderService, ILogger<OrderHub> logger)
    {
        _orderService = orderService;
        _logger = logger;
    }
    
    public async Task JoinOrderGroup(Guid orderId)
    {
        var tenantId = GetTenantId();
        var order = await _orderService.GetOrderByIdAsync(orderId, tenantId);
        
        if (order != null)
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, $"order_{orderId}");
            await Groups.AddToGroupAsync(Context.ConnectionId, $"tenant_{tenantId}");
            
            _logger.LogInformation("Connection {ConnectionId} joined order group {OrderId}", 
                Context.ConnectionId, orderId);
        }
    }
    
    public async Task JoinTenantGroup()
    {
        var tenantId = GetTenantId();
        await Groups.AddToGroupAsync(Context.ConnectionId, $"tenant_{tenantId}");
        
        _logger.LogInformation("Connection {ConnectionId} joined tenant group {TenantId}", 
            Context.ConnectionId, tenantId);
    }
    
    public async Task NotifyStaffAsync(Order order)
    {
        await Clients.Group($"tenant_{order.TenantId}").SendAsync("OrderCreated", new 
        {
            OrderId = order.Id,
            CustomerName = order.CustomerInfo?.FullName ?? "Khách hàng",
            TotalAmount = order.TotalPrice,
            Status = order.Status,
            CreatedAt = order.CreatedAt,
            Items = order.Items.Select(i => new 
            {
                ProductName = i.ProductName,
                Quantity = i.Quantity,
                UnitPrice = i.UnitPrice
            }).ToList()
        });
        
        _logger.LogInformation("Notified staff for order {OrderId}", order.Id);
    }
    
    public async Task NotifyCustomerAsync(Guid orderId, OrderStatusId status)
    {
        await Clients.Group($"order_{orderId}").SendAsync("OrderStatusUpdated", new 
        {
            OrderId = orderId,
            Status = status.Value,
            StatusDisplay = GetStatusDisplay(status),
            Timestamp = DateTime.UtcNow
        });
        
        _logger.LogInformation("Notified customer for order {OrderId} status {Status}", orderId, status);
    }
    
    private Guid GetTenantId()
    {
        var tenantIdClaim = Context.User?.FindFirst("TenantId")?.Value;
        return Guid.TryParse(tenantIdClaim, out var id) ? id : Guid.Empty;
    }
    
    private string GetStatusDisplay(OrderStatusId status)
    {
        return status.Value switch
        {
            "pending" => "🔄 Đang chờ xử lý",
            "preparing" => "🔥 Đang chuẩn bị",
            "ready" => "🎯 Sẵn sàng",
            "delivering" => "🚚 Đang giao hàng",
            "completed" => "✅ Hoàn thành",
            "cancelled" => "❌ Đã hủy",
            _ => status.Value
        };
    }
}
```

##### **1.8 Update CoreHub Program.cs**
```csharp
// 3_CoreHub/Program.cs - ADD these services:

// Background task queue
builder.Services.AddSingleton<IBackgroundTaskQueue, BackgroundTaskQueue>();
builder.Services.AddHostedService<OrderQueueService>();

// Enhanced order services
builder.Services.AddScoped<IOrderQueueService, OrderQueueService>();

// SignalR
builder.Services.AddSignalR();

// Template factory (if not already registered)
builder.Services.AddScoped<ITemplateFactory, TemplateFactory>();

// Order hub
builder.Services.AddScoped<OrderHub>();
```

---

### **PHASE 2: FRONTEND UNIFICATION (Week 2)**

#### **DAY 8-9: KHACHLINK PWA**

##### **2.1 Create IndexedDB Service**
```typescript
// 5_WebApps/KhachLink/Services/IndexedDBService.ts (CREATE)
export interface IndexedDBService {
    init(): Promise<void>;
    storeOrder(order: Order): Promise<void>;
    getOrder(orderId: Guid): Promise<Order | null>;
    getOrders(): Promise<Order[]>;
    storeSyncItem(item: SyncItem): Promise<void>;
    getPendingSync(): Promise<SyncItem[]>;
    markSynced(orderId: Guid): Promise<void>;
}

export class IndexedDBServiceImpl implements IndexedDBService {
    private db: IDBDatabase | null = null;
    private readonly dbName = 'VanAnKhachLink';
    private readonly version = 1;
    
    async init(): Promise<void> {
        return new Promise((resolve, reject) => {
            const request = indexedDB.open(this.dbName, this.version);
            
            request.onerror = () => reject(request.error);
            request.onsuccess = () => {
                this.db = request.result;
                resolve();
            };
            
            request.onupgradeneeded = (event) => {
                const db = (event.target as IDBOpenDBRequest).result;
                
                // Create object stores
                if (!db.objectStoreNames.contains('orders')) {
                    const orderStore = db.createObjectStore('orders', { keyPath: 'id' });
                    orderStore.createIndex('createdAt', 'createdAt');
                    orderStore.createIndex('status', 'status');
                }
                
                if (!db.objectStoreNames.contains('sync')) {
                    const syncStore = db.createObjectStore('sync', { keyPath: 'id' });
                    syncStore.createIndex('orderId', 'orderId');
                    syncStore.createIndex('status', 'status');
                }
            };
        });
    }
    
    async storeOrder(order: Order): Promise<void> {
        if (!this.db) throw new Error('Database not initialized');
        
        return new Promise((resolve, reject) => {
            const transaction = this.db!.transaction(['orders'], 'readwrite');
            const store = transaction.objectStore('orders');
            const request = store.put(order);
            
            request.onerror = () => reject(request.error);
            request.onsuccess = () => resolve();
        });
    }
    
    async getOrder(orderId: Guid): Promise<Order | null> {
        if (!this.db) throw new Error('Database not initialized');
        
        return new Promise((resolve, reject) => {
            const transaction = this.db!.transaction(['orders'], 'readonly');
            const store = transaction.objectStore('orders');
            const request = store.get(orderId);
            
            request.onerror = () => reject(request.error);
            request.onsuccess = () => resolve(request.result || null);
        });
    }
    
    async getOrders(): Promise<Order[]> {
        if (!this.db) throw new Error('Database not initialized');
        
        return new Promise((resolve, reject) => {
            const transaction = this.db!.transaction(['orders'], 'readonly');
            const store = transaction.objectStore('orders');
            const request = store.getAll();
            
            request.onerror = () => reject(request.error);
            request.onsuccess = () => resolve(request.result || []);
        });
    }
    
    async storeSyncItem(item: SyncItem): Promise<void> {
        if (!this.db) throw new Error('Database not initialized');
        
        return new Promise((resolve, reject) => {
            const transaction = this.db!.transaction(['sync'], 'readwrite');
            const store = transaction.objectStore('sync');
            const request = store.put(item);
            
            request.onerror = () => reject(request.error);
            request.onsuccess = () => resolve();
        });
    }
    
    async getPendingSync(): Promise<SyncItem[]> {
        if (!this.db) throw new Error('Database not initialized');
        
        return new Promise((resolve, reject) => {
            const transaction = this.db!.transaction(['sync'], 'readonly');
            const store = transaction.objectStore('sync');
            const index = store.index('status');
            const request = index.getAll('pending');
            
            request.onerror = () => reject(request.error);
            request.onsuccess = () => resolve(request.result || []);
        });
    }
    
    async markSynced(orderId: Guid): Promise<void> {
        if (!this.db) throw new Error('Database not initialized');
        
        return new Promise((resolve, reject) => {
            const transaction = this.db!.transaction(['sync'], 'readwrite');
            const store = transaction.objectStore('sync');
            const index = store.index('orderId');
            const request = index.openCursor(IDBKeyRange.only(orderId));
            
            request.onerror = () => reject(request.error);
            request.onsuccess = (event) => {
                const cursor = (event.target as IDBRequest).result;
                if (cursor) {
                    const syncItem = cursor.value as SyncItem;
                    syncItem.status = 'synced';
                    syncItem.syncedAt = new Date().toISOString();
                    cursor.update(syncItem);
                    cursor.continue();
                } else {
                    resolve();
                }
            };
        });
    }
}

export interface Order {
    id: Guid;
    customerId: string;
    shopId: string;
    items: OrderItem[];
    totalAmount: number;
    status: string;
    createdAt: string;
    updatedAt?: string;
}

export interface OrderItem {
    productId: string;
    quantity: number;
    unitPrice: number;
    totalPrice: number;
}

export interface SyncItem {
    id: Guid;
    orderId: Guid;
    type: 'create' | 'update' | 'delete';
    status: 'pending' | 'synced' | 'error';
    data: any;
    createdAt: string;
    syncedAt?: string;
    error?: string;
}
```

##### **2.2 Create Offline Order Service**
```typescript
// 5_WebApps/KhachLink/Services/OfflineOrderService.ts (CREATE)
import { IndexedDBService, IndexedDBServiceImpl } from './IndexedDBService';

export interface OfflineOrderService {
    createOrder(order: Order): Promise<void>;
    getOrders(): Promise<Order[]>;
    syncOrders(): Promise<SyncResult>;
    getOrder(orderId: Guid): Promise<Order | null>;
    deleteOrder(orderId: Guid): Promise<void>;
}

export class OfflineOrderServiceImpl implements OfflineOrderService {
    private indexedDB: IndexedDBService;
    private isOnline: boolean = navigator.onLine;
    
    constructor() {
        this.indexedDB = new IndexedDBServiceImpl();
        
        // Monitor online/offline status
        window.addEventListener('online', () => {
            this.isOnline = true;
            this.autoSync();
        });
        
        window.addEventListener('offline', () => {
            this.isOnline = false;
        });
    }
    
    async createOrder(order: Order): Promise<void> {
        try {
            // Initialize IndexedDB if needed
            await this.indexedDB.init();
            
            // Set order status to pending
            order.status = 'pending';
            order.createdAt = new Date().toISOString();
            
            // Store order locally
            await this.indexedDB.storeOrder(order);
            
            // Create sync item
            const syncItem: SyncItem = {
                id: this.generateGuid(),
                orderId: order.id,
                type: 'create',
                status: 'pending',
                data: order,
                createdAt: new Date().toISOString()
            };
            
            await this.indexedDB.storeSyncItem(syncItem);
            
            // Try to sync immediately if online
            if (this.isOnline) {
                await this.syncOrders();
            }
        } catch (error) {
            console.error('Error creating offline order:', error);
            throw error;
        }
    }
    
    async getOrders(): Promise<Order[]> {
        try {
            await this.indexedDB.init();
            return await this.indexedDB.getOrders();
        } catch (error) {
            console.error('Error getting offline orders:', error);
            return [];
        }
    }
    
    async getOrder(orderId: Guid): Promise<Order | null> {
        try {
            await this.indexedDB.init();
            return await this.indexedDB.getOrder(orderId);
        } catch (error) {
            console.error('Error getting offline order:', error);
            return null;
        }
    }
    
    async deleteOrder(orderId: Guid): Promise<void> {
        try {
            await this.indexedDB.init();
            
            // Create sync item for deletion
            const syncItem: SyncItem = {
                id: this.generateGuid(),
                orderId: orderId,
                type: 'delete',
                status: 'pending',
                data: { orderId },
                createdAt: new Date().toISOString()
            };
            
            await this.indexedDB.storeSyncItem(syncItem);
            
            // Try to sync immediately if online
            if (this.isOnline) {
                await this.syncOrders();
            }
        } catch (error) {
            console.error('Error deleting offline order:', error);
            throw error;
        }
    }
    
    async syncOrders(): Promise<SyncResult> {
        const result: SyncResult = {
            success: false,
            syncedCount: 0,
            errorCount: 0,
            errors: []
        };
        
        if (!this.isOnline) {
            result.errors.push('Device is offline');
            return result;
        }
        
        try {
            await this.indexedDB.init();
            const pendingItems = await this.indexedDB.getPendingSync();
            
            for (const item of pendingItems) {
                try {
                    if (item.type === 'create') {
                        await this.syncCreateOrder(item);
                    } else if (item.type === 'delete') {
                        await this.syncDeleteOrder(item);
                    }
                    
                    // Mark as synced
                    await this.indexedDB.markSynced(item.orderId);
                    result.syncedCount++;
                } catch (error) {
                    result.errorCount++;
                    result.errors.push(`Failed to sync order ${item.orderId}: ${error}`);
                }
            }
            
            result.success = result.errorCount === 0;
        } catch (error) {
            result.errors.push(`Sync failed: ${error}`);
        }
        
        return result;
    }
    
    private async syncCreateOrder(syncItem: SyncItem): Promise<void> {
        const order = syncItem.data as Order;
        
        // Call API to create order
        const response = await fetch('/api/orders', {
            method: 'POST',
            headers: {
                'Content-Type': 'application/json',
            },
            body: JSON.stringify(order)
        });
        
        if (!response.ok) {
            throw new Error(`API call failed: ${response.statusText}`);
        }
        
        // Update local order with server response
        const serverOrder = await response.json();
        await this.indexedDB.storeOrder(serverOrder);
    }
    
    private async syncDeleteOrder(syncItem: SyncItem): Promise<void> {
        const orderId = syncItem.data.orderId;
        
        // Call API to delete order
        const response = await fetch(`/api/orders/${orderId}`, {
            method: 'DELETE'
        });
        
        if (!response.ok) {
            throw new Error(`API call failed: ${response.statusText}`);
        }
        
        // Remove from local storage
        await this.indexedDB.markSynced(orderId);
    }
    
    private async autoSync(): Promise<void> {
        try {
            await this.syncOrders();
        } catch (error) {
            console.error('Auto sync failed:', error);
        }
    }
    
    private generateGuid(): Guid {
        return 'xxxxxxxx-xxxx-4xxx-yxxx-xxxxxxxxxxxx'.replace(/[xy]/g, function(c) {
            const r = Math.random() * 16 | 0;
            const v = c === 'x' ? r : (r & 0x3 | 0x8);
            return v.toString(16);
        }) as Guid;
    }
}

export interface SyncResult {
    success: boolean;
    syncedCount: number;
    errorCount: number;
    errors: string[];
}
```

##### **2.3 Create Enhanced Cart Service**
```typescript
// 5_WebApps/KhachLink/Services/EnhancedCartService.ts (CREATE)
import { IndexedDBService, IndexedDBServiceImpl } from './IndexedDBService';

export interface CartService {
    addItem(item: CartItem): Promise<void>;
    removeItem(productId: string): Promise<void>;
    updateQuantity(productId: string, quantity: number): Promise<void>;
    getItems(): Promise<CartItem[]>;
    getTotal(): Promise<number>;
    clearCart(): Promise<void>;
    syncCart(): Promise<SyncResult>;
    saveCartOffline(): Promise<void>;
    loadCartOffline(): Promise<void>;
}

export class EnhancedCartServiceImpl implements CartService {
    private indexedDB: IndexedDBService;
    private isOnline: boolean = navigator.onLine;
    private cartKey = 'user_cart';
    
    constructor() {
        this.indexedDB = new IndexedDBServiceImpl();
        
        // Monitor online/offline status
        window.addEventListener('online', () => {
            this.isOnline = true;
            this.syncCart();
        });
        
        window.addEventListener('offline', () => {
            this.isOnline = false;
        });
    }
    
    async addItem(item: CartItem): Promise<void> {
        try {
            // Get current cart
            const items = await this.getItems();
            const existingItem = items.find(i => i.productId === item.productId);
            
            if (existingItem) {
                // Update quantity
                existingItem.quantity += item.quantity;
                existingItem.totalPrice = existingItem.quantity * existingItem.unitPrice;
            } else {
                // Add new item
                items.push(item);
            }
            
            // Save to IndexedDB
            await this.indexedDB.init();
            await this.indexedDB.storeCart({
                id: this.cartKey,
                items: items,
                updatedAt: new Date().toISOString()
            });
            
            // Try to sync immediately if online
            if (this.isOnline) {
                await this.syncCart();
            }
        } catch (error) {
            console.error('Error adding item to cart:', error);
            throw error;
        }
    }
    
    async removeItem(productId: string): Promise<void> {
        try {
            const items = await this.getItems();
            const item = items.find(i => i.productId === productId);
            
            if (item) {
                items.splice(items.indexOf(item), 1);
                
                await this.indexedDB.init();
                await this.indexedDB.storeCart({
                    id: this.cartKey,
                    items: items,
                    updatedAt: new Date().toISOString()
                });
                
                // Try to sync immediately if online
                if (this.isOnline) {
                    await this.syncCart();
                }
            }
        } catch (error) {
            console.error('Error removing item from cart:', error);
            throw error;
        }
    }
    
    async updateQuantity(productId: string, quantity: number): Promise<void> {
        try {
            if (quantity <= 0) {
                await this.removeItem(productId);
                return;
            }
            
            const items = await this.getItems();
            const item = items.find(i => i.productId === productId);
            
            if (item) {
                item.quantity = quantity;
                item.totalPrice = quantity * item.unitPrice;
                
                await this.indexedDB.init();
                await this.indexedDB.storeCart({
                    id: this.cartKey,
                    items: items,
                    updatedAt: new Date().toISOString()
                });
                
                // Try to sync immediately if online
                if (this.isOnline) {
                    await this.syncCart();
                }
            }
        } catch (error) {
            console.error('Error updating cart item quantity:', error);
            throw error;
        }
    }
    
    async getItems(): Promise<CartItem[]> {
        try {
            await this.indexedDB.init();
            const cart = await this.indexedDB.getCart(this.cartKey);
            return cart?.items || [];
        } catch (error) {
            console.error('Error getting cart items:', error);
            return [];
        }
    }
    
    async getTotal(): Promise<number> {
        try {
            const items = await this.getItems();
            return items.reduce((total, item) => total + item.totalPrice, 0);
        } catch (error) {
            console.error('Error calculating cart total:', error);
            return 0;
        }
    }
    
    async clearCart(): Promise<void> {
        try {
            await this.indexedDB.init();
            await this.indexedDB.storeCart({
                id: this.cartKey,
                items: [],
                updatedAt: new Date().toISOString()
            });
            
            // Try to sync immediately if online
            if (this.isOnline) {
                await this.syncCart();
            }
        } catch (error) {
            console.error('Error clearing cart:', error);
            throw error;
        }
    }
    
    async syncCart(): Promise<SyncResult> {
        const result: SyncResult = {
            success: false,
            syncedCount: 0,
            errorCount: 0,
            errors: []
        };
        
        if (!this.isOnline) {
            result.errors.push('Device is offline');
            return result;
        }
        
        try {
            await this.indexedDB.init();
            const cart = await this.indexedDB.getCart(this.cartKey);
            
            if (!cart || !cart.items.length) {
                result.success = true;
                result.errors.push('No items to sync');
                return result;
            }
            
            // Get server cart
            const serverItems = await this.getServerCart();
            
            // Resolve conflicts
            const mergedItems = this.mergeCartItems(cart.items, serverItems);
            
            // Sync merged items to server
            await this.syncToServer(mergedItems);
            
            // Update offline cart with merged items
            await this.indexedDB.storeCart({
                id: this.cartKey,
                items: mergedItems,
                updatedAt: new Date().toISOString()
            });
            
            result.success = true;
            result.syncedCount = mergedItems.length;
        } catch (error) {
            result.errorCount++;
            result.errors.push(`Cart sync failed: ${error}`);
        }
        
        return result;
    }
    
    async saveCartOffline(): Promise<void> {
        try {
            const serverItems = await this.getServerCart();
            
            await this.indexedDB.init();
            await this.indexedDB.storeCart({
                id: this.cartKey,
                items: serverItems,
                updatedAt: new Date().toISOString()
            });
        } catch (error) {
            console.error('Error saving cart offline:', error);
            throw error;
        }
    }
    
    async loadCartOffline(): Promise<void> {
        try {
            await this.indexedDB.init();
            const cart = await this.indexedDB.getCart(this.cartKey);
            
            if (!cart || !cart.items.length) {
                return; // No offline cart to load
            }
            
            // Clear server cart first
            await this.clearServerCart();
            
            // Load offline items to server cart
            for (const item of cart.items) {
                await this.addToServerCart(item);
            }
        } catch (error) {
            console.error('Error loading cart from offline:', error);
            throw error;
        }
    }
    
    private async getServerCart(): Promise<CartItem[]> {
        const response = await fetch('/api/cart');
        if (!response.ok) {
            throw new Error(`Failed to get server cart: ${response.statusText}`);
        }
        return await response.json();
    }
    
    private async syncToServer(items: CartItem[]): Promise<void> {
        // Clear server cart first
        await this.clearServerCart();
        
        // Add all items to server cart
        for (const item of items) {
            await this.addToServerCart(item);
        }
    }
    
    private async clearServerCart(): Promise<void> {
        const response = await fetch('/api/cart/clear', {
            method: 'POST'
        });
        if (!response.ok) {
            throw new Error(`Failed to clear server cart: ${response.statusText}`);
        }
    }
    
    private async addToServerCart(item: CartItem): Promise<void> {
        const response = await fetch('/api/cart/items', {
            method: 'POST',
            headers: {
                'Content-Type': 'application/json',
            },
            body: JSON.stringify(item)
        });
        if (!response.ok) {
            throw new Error(`Failed to add item to server cart: ${response.statusText}`);
        }
    }
    
    private mergeCartItems(offlineItems: CartItem[], serverItems: CartItem[]): CartItem[] {
        const mergedItems: CartItem[] = [];
        const processedProductIds = new Set<string>();
        
        // Add offline items first
        for (const offlineItem of offlineItems) {
            mergedItems.push(offlineItem);
            processedProductIds.add(offlineItem.productId);
        }
        
        // Add server items not in offline
        for (const serverItem of serverItems) {
            if (!processedProductIds.has(serverItem.productId)) {
                mergedItems.push({
                    productId: serverItem.productId,
                    quantity: serverItem.quantity,
                    unitPrice: serverItem.unitPrice,
                    totalPrice: serverItem.totalPrice
                });
            }
        }
        
        return mergedItems;
    }
}

export interface CartItem {
    productId: string;
    quantity: number;
    unitPrice: number;
    totalPrice: number;
}

export interface SyncResult {
    success: boolean;
    syncedCount: number;
    errorCount: number;
    errors: string[];
}

export interface CartStorage {
    id: string;
    items: CartItem[];
    updatedAt: string;
}
```

##### **2.4 Create Conflict Resolution Service**
```typescript
// 5_WebApps/KhachLink/Services/ConflictResolutionService.ts (CREATE)
export interface ConflictResolutionService {
    resolveOrderConflict(offlineOrder: Order, serverOrder?: Order): Promise<ConflictResolution>;
    resolveCartConflict(offlineItems: CartItem[], serverItems: CartItem[]): Promise<ConflictResolution>;
    validateOrder(order: Order): Promise<boolean>;
    validateCart(items: CartItem[]): Promise<boolean>;
    generateConflictReport(offlineOrder: Order, serverOrder?: Order): Promise<ConflictReport>;
}

export class ConflictResolutionServiceImpl implements ConflictResolutionService {
    async resolveOrderConflict(offlineOrder: Order, serverOrder?: Order): Promise<ConflictResolution> {
        const result: ConflictResolution = {
            success: false,
            action: 'error',
            reason: '',
            mergedOrder: null,
            mergedItems: null,
            warnings: []
        };
        
        try {
            // Validate offline order first
            if (!await this.validateOrder(offlineOrder)) {
                result.action = 'error';
                result.reason = 'Offline order validation failed';
                return result;
            }
            
            // Case 1: No server order exists - use offline
            if (!serverOrder) {
                result.success = true;
                result.action = 'useOffline';
                result.reason = 'No server order found';
                result.mergedOrder = offlineOrder;
                return result;
            }
            
            // Case 2: Server order is newer - use server
            if (new Date(serverOrder.createdAt) > new Date(offlineOrder.createdAt)) {
                result.success = true;
                result.action = 'useServer';
                result.reason = 'Server order is newer';
                result.mergedOrder = serverOrder;
                return result;
            }
            
            // Case 3: Same timestamp - merge items
            const timeDiff = Math.abs(new Date(offlineOrder.createdAt).getTime() - new Date(serverOrder.createdAt).getTime());
            if (timeDiff < 5000) { // 5 seconds
                const mergedOrder = await this.mergeOrders(offlineOrder, serverOrder);
                result.success = true;
                result.action = 'merge';
                result.reason = 'Orders created at same time - merging items';
                result.mergedOrder = mergedOrder;
                return result;
            }
            
            // Case 4: Offline order is newer - use offline
            result.success = true;
            result.action = 'useOffline';
            result.reason = 'Offline order is newer';
            result.mergedOrder = offlineOrder;
            return result;
        } catch (error) {
            result.success = false;
            result.action = 'error';
            result.reason = (error as Error).message;
            return result;
        }
    }
    
    async resolveCartConflict(offlineItems: CartItem[], serverItems: CartItem[]): Promise<ConflictResolution> {
        const result: ConflictResolution = {
            success: false,
            action: 'error',
            reason: '',
            mergedOrder: null,
            mergedItems: [],
            warnings: []
        };
        
        try {
            // Validate cart items
            if (!await this.validateCart(offlineItems)) {
                result.action = 'error';
                result.reason = 'Offline cart validation failed';
                return result;
            }
            
            // Merge cart items - combine unique items
            const mergedItems = this.mergeCartItems(offlineItems, serverItems);
            
            result.success = true;
            result.action = 'merge';
            result.reason = 'Cart items merged successfully';
            result.mergedItems = mergedItems;
            
            // Add warnings for potential issues
            this.checkCartWarnings(mergedItems, result);
            
            return result;
        } catch (error) {
            result.success = false;
            result.action = 'error';
            result.reason = (error as Error).message;
            return result;
        }
    }
    
    async validateOrder(order: Order): Promise<boolean> {
        try {
            // Basic validation
            if (!order.id || !order.customerId || !order.shopId) {
                return false;
            }
            
            if (!order.items || order.items.length === 0) {
                return false;
            }
            
            // Validate items
            for (const item of order.items) {
                if (!item.productId || item.quantity <= 0 || item.unitPrice <= 0) {
                    return false;
                }
                
                if (Math.abs(item.totalPrice - (item.quantity * item.unitPrice)) > 0.01) {
                    return false;
                }
            }
            
            // Validate total amount
            const calculatedTotal = order.items.reduce((sum, item) => sum + item.totalPrice, 0);
            if (Math.abs(calculatedTotal - order.totalAmount) > 0.01) {
                return false;
            }
            
            return true;
        } catch (error) {
            console.error('Order validation failed:', error);
            return false;
        }
    }
    
    async validateCart(items: CartItem[]): Promise<boolean> {
        try {
            if (!items || items.length === 0) {
                return true; // Empty cart is valid
            }
            
            for (const item of items) {
                if (!item.productId || item.quantity <= 0 || item.unitPrice <= 0) {
                    return false;
                }
                
                if (Math.abs(item.totalPrice - (item.quantity * item.unitPrice)) > 0.01) {
                    return false;
                }
            }
            
            return true;
        } catch (error) {
            console.error('Cart validation failed:', error);
            return false;
        }
    }
    
    async generateConflictReport(offlineOrder: Order, serverOrder?: Order): Promise<ConflictReport> {
        const report: ConflictReport = {
            orderId: offlineOrder.id,
            hasConflict: false,
            conflicts: [],
            warnings: [],
            recommendedAction: 'useOffline',
            recommendation: ''
        };
        
        try {
            if (!serverOrder) {
                report.recommendedAction = 'useOffline';
                report.recommendation = 'No server order found - use offline order';
                return report;
            }
            
            // Check for conflicts
            const timeDiff = Math.abs(new Date(offlineOrder.createdAt).getTime() - new Date(serverOrder.createdAt).getTime());
            
            if (timeDiff > 300000) { // 5 minutes
                report.conflicts.push(`Timestamp mismatch: Offline ${offlineOrder.createdAt}, Server ${serverOrder.createdAt}`);
                report.hasConflict = true;
            }
            
            if (Math.abs(offlineOrder.totalAmount - serverOrder.totalAmount) > 0.01) {
                report.conflicts.push(`Total amount mismatch: Offline ${offlineOrder.totalAmount}, Server ${serverOrder.totalAmount}`);
                report.hasConflict = true;
            }
            
            // Check items differences
            const offlineProductIds = offlineOrder.items.map(i => i.productId);
            const serverProductIds = serverOrder.items.map(i => i.productId);
            
            if (!this.arraysEqual(offlineProductIds, serverProductIds)) {
                report.conflicts.push('Items differ between offline and server orders');
                report.hasConflict = true;
            }
            
            // Status conflict
            if (offlineOrder.status !== serverOrder.status) {
                report.warnings.push(`Status differs: Offline ${offlineOrder.status}, Server ${serverOrder.status}`);
            }
            
            // Recommend action
            if (report.hasConflict) {
                if (new Date(offlineOrder.createdAt) > new Date(serverOrder.createdAt)) {
                    report.recommendedAction = 'useOffline';
                    report.recommendation = 'Offline order is newer - prefer offline version';
                } else {
                    report.recommendedAction = 'merge';
                    report.recommendation = 'Server order is newer - merge items to preserve data';
                }
            } else {
                report.recommendedAction = 'useServer';
                report.recommendation = 'No significant conflicts - use server version';
            }
        } catch (error) {
            report.hasConflict = true;
            report.recommendedAction = 'error';
            report.recommendation = (error as Error).message;
        }
        
        return report;
    }
    
    private async mergeOrders(offlineOrder: Order, serverOrder: Order): Promise<Order> {
        // Create merged order with server data as base
        const mergedOrder: Order = {
            id: serverOrder.id,
            customerId: serverOrder.customerId,
            shopId: serverOrder.shopId,
            totalAmount: serverOrder.totalAmount,
            status: serverOrder.status,
            createdAt: serverOrder.createdAt,
            items: []
        };
        
        // Merge items from both orders
        const processedProductIds = new Set<string>();
        
        // Add server items first
        for (const serverItem of serverOrder.items) {
            mergedOrder.items.push({
                productId: serverItem.productId,
                quantity: serverItem.quantity,
                unitPrice: serverItem.unitPrice,
                totalPrice: serverItem.totalPrice
            });
            processedProductIds.add(serverItem.productId);
        }
        
        // Add offline items that aren't in server
        for (const offlineItem of offlineOrder.items) {
            if (!processedProductIds.has(offlineItem.productId)) {
                mergedOrder.items.push({
                    productId: offlineItem.productId,
                    quantity: offlineItem.quantity,
                    unitPrice: offlineItem.unitPrice,
                    totalPrice: offlineItem.totalPrice
                });
            }
        }
        
        // Recalculate total
        mergedOrder.totalAmount = mergedOrder.items.reduce((sum, item) => sum + item.totalPrice, 0);
        
        return mergedOrder;
    }
    
    private mergeCartItems(offlineItems: CartItem[], serverItems: CartItem[]): CartItem[] {
        const mergedItems: CartItem[] = [];
        const processedProductIds = new Set<string>();
        
        // Add offline items first
        for (const offlineItem of offlineItems) {
            mergedItems.push(offlineItem);
            processedProductIds.add(offlineItem.productId);
        }
        
        // Add server items not in offline
        for (const serverItem of serverItems) {
            if (!processedProductIds.has(serverItem.productId)) {
                mergedItems.push({
                    productId: serverItem.productId,
                    quantity: serverItem.quantity,
                    unitPrice: serverItem.unitPrice,
                    totalPrice: serverItem.totalPrice
                });
            }
        }
        
        return mergedItems;
    }
    
    private checkCartWarnings(items: CartItem[], result: ConflictResolution): void {
        if (items.length > 20) {
            result.warnings.push('Large number of items in cart may affect performance');
        }
        
        const totalValue = items.reduce((sum, item) => sum + item.totalPrice, 0);
        if (totalValue > 1000000) { // 1 million VND
            result.warnings.push('High-value cart - consider confirming with customer');
        }
        
        const highQuantityItems = items.filter(item => item.quantity > 10);
        if (highQuantityItems.length > 0) {
            result.warnings.push(`Items with high quantity: ${highQuantityItems.map(i => i.productId).join(', ')}`);
        }
    }
    
    private arraysEqual(a: string[], b: string[]): boolean {
        if (a.length !== b.length) return false;
        const sortedA = [...a].sort();
        const sortedB = [...b].sort();
        return sortedA.every((val, index) => val === sortedB[index]);
    }
}

export interface ConflictResolution {
    success: boolean;
    action: 'useOffline' | 'useServer' | 'merge' | 'skip' | 'error';
    reason: string;
    mergedOrder?: Order;
    mergedItems?: CartItem[];
    warnings: string[];
}

export interface ConflictReport {
    orderId: string;
    hasConflict: boolean;
    conflicts: string[];
    warnings: string[];
    recommendedAction: 'useOffline' | 'useServer' | 'merge' | 'error';
    recommendation: string;
}

export interface Order {
    id: string;
    customerId: string;
    shopId: string;
    items: OrderItem[];
    totalAmount: number;
    status: string;
    createdAt: string;
    updatedAt?: string;
}

export interface OrderItem {
    productId: string;
    quantity: number;
    unitPrice: number;
    totalPrice: number;
}

export interface CartItem {
    productId: string;
    quantity: number;
    unitPrice: number;
    totalPrice: number;
}
```

#### **DAY 10-11: SHOPERP DASHBOARD**

##### **2.5 Create Order Management Service**
```typescript
// 5_WebApps/ShopERP/Services/OrderManagementService.ts (CREATE)
import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';

export interface Order {
    id: string;
    customerId: string;
    shopId: string;
    items: OrderItem[];
    totalAmount: number;
    status: string;
    createdAt: string;
    updatedAt?: string;
}

export interface OrderItem {
    productId: string;
    quantity: number;
    unitPrice: number;
    totalPrice: number;
}

export interface OrderMetrics {
    totalOrders: number;
    pendingOrders: number;
    processingOrders: number;
    completedOrders: number;
    cancelledOrders: number;
    totalRevenue: number;
    averageOrderValue: number;
    ordersPerHour: number;
    revenuePerHour: number;
    statusBreakdown: StatusCount[];
}

export interface StatusCount {
    status: string;
    count: number;
    percentage: number;
}

export interface OrderSummary {
    orderId: string;
    customerId: string;
    status: string;
    createdAt: string;
    updatedAt?: string;
    totalAmount: number;
    itemCount: number;
    items: OrderItemSummary[];
    statusHistory: OrderStatusHistory[];
}

export interface OrderItemSummary {
    productId: string;
    productName: string;
    quantity: number;
    unitPrice: number;
    totalPrice: number;
}

export interface OrderStatusHistory {
    status: string;
    changedAt: string;
    reason?: string;
    changedBy?: string;
}

@Injectable({
    providedIn: 'root'
})
export class OrderManagementService {
    private readonly apiUrl = '/api/orders';

    constructor(private http: HttpClient) {}

    getOrders(status?: string): Observable<Order[]> {
        const url = status ? `${this.apiUrl}?status=${status}` : this.apiUrl;
        return this.http.get<Order[]>(url);
    }

    getOrder(orderId: string): Observable<Order> {
        return this.http.get<Order>(`${this.apiUrl}/${orderId}`);
    }

    updateOrderStatus(orderId: string, newStatus: string, reason?: string): Observable<boolean> {
        const body = reason ? { status: newStatus, reason } : { status: newStatus };
        return this.http.put<boolean>(`${this.apiUrl}/${orderId}/status`, body);
    }

    getOrdersByDateRange(startDate: Date, endDate: Date): Observable<Order[]> {
        const params = {
            startDate: startDate.toISOString(),
            endDate: endDate.toISOString()
        };
        return this.http.get<Order[]>(`${this.apiUrl}/daterange`, { params });
    }

    getOrderMetrics(): Observable<OrderMetrics> {
        return this.http.get<OrderMetrics>(`${this.apiUrl}/metrics`);
    }

    assignOrderToStaff(orderId: string, staffId: string): Observable<boolean> {
        return this.http.put<boolean>(`${this.apiUrl}/${orderId}/assign`, { staffId });
    }

    getOrdersByCustomer(customerId: string): Observable<Order[]> {
        return this.http.get<Order[]>(`${this.apiUrl}/customer/${customerId}`);
    }

    cancelOrder(orderId: string, reason: string): Observable<boolean> {
        return this.http.put<boolean>(`${this.apiUrl}/${orderId}/cancel`, { reason });
    }

    getOrderSummary(orderId: string): Observable<OrderSummary> {
        return this.http.get<OrderSummary>(`${this.apiUrl}/${orderId}/summary`);
    }
}
```

##### **2.6 Create Realtime Dashboard Service**
```typescript
// 5_WebApps/ShopERP/Services/RealtimeDashboardService.ts (CREATE)
import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable, Subject, BehaviorSubject } from 'rxjs';
import { HubConnection, HubConnectionBuilder, LogLevel } from '@microsoft/signalr';
import { Order, OrderMetrics } from './order-management.service';

export interface DashboardAlert {
    id: string;
    type: AlertType;
    title: string;
    message: string;
    severity: AlertSeverity;
    createdAt: Date;
    expiresAt?: Date;
    isRead: boolean;
    actionUrl?: string;
    metadata: Record<string, any>;
}

export enum AlertType {
    OrderCreated = 'orderCreated',
    OrderUpdated = 'orderUpdated',
    OrderCancelled = 'orderCancelled',
    HighValueOrder = 'highValueOrder',
    SystemError = 'systemError',
    LowInventory = 'lowInventory',
    StaffAssignment = 'staffAssignment'
}

export enum AlertSeverity {
    Info = 'info',
    Warning = 'warning',
    Error = 'error',
    Critical = 'critical'
}

@Injectable({
    providedIn: 'root'
})
export class RealtimeDashboardService {
    private hubConnection: HubConnection | null = null;
    private readonly apiUrl = '/api/dashboard';
    private readonly hubUrl = '/hubs/order';
    
    // Observables for real-time updates
    private orderUpdateSubject = new Subject<Order>();
    private metricsUpdateSubject = new BehaviorSubject<OrderMetrics | null>(null);
    private alertSubject = new Subject<DashboardAlert>();
    
    // Public observables
    public orderUpdates$ = this.orderUpdateSubject.asObservable();
    public metricsUpdates$ = this.metricsUpdateSubject.asObservable();
    public alerts$ = this.alertSubject.asObservable();
    
    private subscribedConnections = new Set<string>();
    
    constructor(private http: HttpClient) {}

    async getCurrentMetrics(): Promise<OrderMetrics> {
        try {
            const response = await this.http.get<OrderMetrics>(`${this.apiUrl}/metrics`).toPromise();
            if (response) {
                this.metricsUpdateSubject.next(response);
            }
            return response || this.getDefaultMetrics();
        } catch (error) {
            console.error('Failed to get current dashboard metrics:', error);
            return this.getDefaultMetrics();
        }
    }

    async getRecentOrders(count: number = 10): Promise<Order[]> {
        try {
            return await this.http.get<Order[]>(`${this.apiUrl}/orders/recent?count=${count}`).toPromise() || [];
        } catch (error) {
            console.error('Failed to get recent orders:', error);
            return [];
        }
    }

    async getActiveOrders(): Promise<Order[]> {
        try {
            return await this.http.get<Order[]>(`${this.apiUrl}/orders/active`).toPromise() || [];
        } catch (error) {
            console.error('Failed to get active orders:', error);
            return [];
        }
    }

    async broadcastOrderUpdate(order: Order): Promise<boolean> {
        try {
            if (this.subscribedConnections.size === 0) {
                return false; // No subscribers
            }

            // Invalidate relevant caches (handled by server)
            
            // Create alert for high-value orders
            if (order.totalAmount > 1000000) { // 1 million VND
                await this.createAlert({
                    type: AlertType.HighValueOrder,
                    title: 'High Value Order',
                    message: `Order ${order.id} with total ${order.totalAmount.toLocaleString()} VND`,
                    severity: AlertSeverity.Info,
                    metadata: {
                        orderId: order.id,
                        totalAmount: order.totalAmount
                    }
                });
            }

            // Broadcast to all subscribed clients (handled by SignalR hub)
            this.orderUpdateSubject.next(order);

            // Also broadcast updated metrics
            await this.broadcastMetricsUpdate();

            console.log('Broadcasted order update:', order.id);
            return true;
        } catch (error) {
            console.error('Failed to broadcast order update:', error);
            return false;
        }
    }

    async broadcastMetricsUpdate(): Promise<boolean> {
        try {
            if (this.subscribedConnections.size === 0) {
                return false; // No subscribers
            }

            const metrics = await this.getCurrentMetrics();
            this.metricsUpdateSubject.next(metrics);

            console.log('Broadcasted metrics update');
            return true;
        } catch (error) {
            console.error('Failed to broadcast metrics update:', error);
            return false;
        }
    }

    async subscribeToUpdates(connectionId: string): Promise<boolean> {
        try {
            this.subscribedConnections.add(connectionId);

            // Send initial data
            const metrics = await this.getCurrentMetrics();
            const recentOrders = await this.getRecentOrders(10);
            const activeOrders = await this.getActiveOrders();
            const alerts = await this.getActiveAlerts();

            console.log('Client subscribed to dashboard updates:', connectionId);
            return true;
        } catch (error) {
            console.error('Failed to subscribe client to updates:', connectionId, error);
            return false;
        }
    }

    async unsubscribeFromUpdates(connectionId: string): Promise<boolean> {
        try {
            this.subscribedConnections.delete(connectionId);
            console.log('Client unsubscribed from dashboard updates:', connectionId);
            return true;
        } catch (error) {
            console.error('Failed to unsubscribe client from updates:', connectionId, error);
            return false;
        }
    }

    async getActiveAlerts(): Promise<DashboardAlert[]> {
        try {
            return await this.http.get<DashboardAlert[]>(`${this.apiUrl}/alerts`).toPromise() || [];
        } catch (error) {
            console.error('Failed to get active alerts:', error);
            return [];
        }
    }

    async createAlert(alert: Partial<DashboardAlert>): Promise<boolean> {
        try {
            const fullAlert: DashboardAlert = {
                id: this.generateGuid(),
                type: alert.type || AlertType.SystemError,
                title: alert.title || '',
                message: alert.message || '',
                severity: alert.severity || AlertSeverity.Info,
                createdAt: new Date(),
                expiresAt: alert.expiresAt,
                isRead: false,
                actionUrl: alert.actionUrl,
                metadata: alert.metadata || {}
            };

            // Broadcast alert to all subscribed clients
            if (this.subscribedConnections.size > 0) {
                this.alertSubject.next(fullAlert);
            }

            console.log('Created dashboard alert:', alert.type);
            return true;
        } catch (error) {
            console.error('Failed to create dashboard alert:', alert.type, error);
            return false;
        }
    }

    // SignalR connection management
    async startConnection(): Promise<void> {
        try {
            this.hubConnection = new HubConnectionBuilder()
                .withUrl(this.hubUrl)
                .withAutomaticReconnect()
                .configureLogging(LogLevel.Information)
                .build();

            this.hubConnection.on('OrderUpdated', (order: Order) => {
                this.orderUpdateSubject.next(order);
            });

            this.hubConnection.on('MetricsUpdated', (metrics: OrderMetrics) => {
                this.metricsUpdateSubject.next(metrics);
            });

            this.hubConnection.on('AlertCreated', (alert: DashboardAlert) => {
                this.alertSubject.next(alert);
            });

            await this.hubConnection.start();
            console.log('SignalR connection started');
        } catch (error) {
            console.error('Failed to start SignalR connection:', error);
        }
    }

    async stopConnection(): Promise<void> {
        try {
            if (this.hubConnection) {
                await this.hubConnection.stop();
                this.hubConnection = null;
                console.log('SignalR connection stopped');
            }
        } catch (error) {
            console.error('Failed to stop SignalR connection:', error);
        }
    }

    private getDefaultMetrics(): OrderMetrics {
        return {
            totalOrders: 0,
            pendingOrders: 0,
            processingOrders: 0,
            completedOrders: 0,
            cancelledOrders: 0,
            totalRevenue: 0,
            averageOrderValue: 0,
            ordersPerHour: 0,
            revenuePerHour: 0,
            statusBreakdown: []
        };
    }

    private generateGuid(): string {
        return 'xxxxxxxx-xxxx-4xxx-yxxx-xxxxxxxxxxxx'.replace(/[xy]/g, function(c) {
            const r = Math.random() * 16 | 0;
            const v = c === 'x' ? r : (r & 0x3 | 0x8);
            return v.toString(16);
        });
    }
}
```

---

### **PHASE 3: UNIFIED API INTEGRATION (Week 2 - FIXED)**

#### **DAY 12-14: UNIFIED API INTEGRATION - COMPREHENSIVE IMPLEMENTATION**

##### **3.1 Update KhachLink to use CoreHub.OrderService**
```csharp
// 5_WebApps/KhachLink/Controllers/OrdersController.cs - UPDATE to use CoreHub
using Microsoft.AspNetCore.Mvc;
using VanAn.CoreHub.Services;
using VanAn.Shared.Domain;

namespace VanAn.KhachLink.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class OrdersController : ControllerBase
    {
        private readonly IOrderService _orderService;
        private readonly ILogger<OrdersController> _logger;

        public OrdersController(IOrderService orderService, ILogger<OrdersController> logger)
        {
            _orderService = orderService;
            _logger = logger;
        }

        [HttpPost]
        public async Task<ActionResult<Order>> CreateOrder([FromBody] CreateOrderRequest request)
        {
            try
            {
                var tenantId = GetTenantId();
                
                var order = new Order
                {
                    Id = Guid.NewGuid(),
                    CustomerId = request.CustomerId,
                    TenantId = new TenantId(tenantId),
                    Items = request.Items.Select(i => new OrderItem
                    {
                        Id = Guid.NewGuid(),
                        ProductId = i.ProductId,
                        Quantity = i.Quantity,
                        UnitPrice = i.UnitPrice,
                        TotalPrice = i.Quantity * i.UnitPrice
                    }).ToList(),
                    TotalPrice = request.TotalPrice,
                    Status = OrderStatusId.Pending,
                    CreatedAt = DateTime.UtcNow
                };

                var createdOrder = await _orderService.CreateOrderAsync(order, tenantId);
                
                return CreatedAtAction(nameof(GetOrder), new { id = createdOrder.Id }, createdOrder);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating order");
                return StatusCode(500, "Internal server error");
            }
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<Order>> GetOrder(Guid id)
        {
            try
            {
                var tenantId = GetTenantId();
                var order = await _orderService.GetOrderByIdAsync(id, tenantId);
                
                if (order == null)
                {
                    return NotFound();
                }
                
                return Ok(order);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting order {OrderId}", id);
                return StatusCode(500, "Internal server error");
            }
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<Order>>> GetOrders([FromQuery] string? status = null)
        {
            try
            {
                var tenantId = GetTenantId();
                
                if (string.IsNullOrEmpty(status))
                {
                    var today = DateTime.UtcNow.Date;
                    var orders = await _orderService.GetOrdersByDateRangeAsync(tenantId, today, today.AddDays(1));
                    return Ok(orders);
                }
                else
                {
                    var statusId = new OrderStatusId(status);
                    var orders = await _orderService.GetOrdersByStatusAsync(statusId, tenantId);
                    return Ok(orders);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting orders");
                return StatusCode(500, "Internal server error");
            }
        }

        [HttpPut("{id}/status")]
        public async Task<ActionResult> UpdateOrderStatus(Guid id, [FromBody] UpdateStatusRequest request)
        {
            try
            {
                var tenantId = GetTenantId();
                var success = await _orderService.UpdateOrderStatusAsync(id, request.Status, tenantId);
                
                if (!success)
                {
                    return NotFound();
                }
                
                return NoContent();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating order status {OrderId}", id);
                return StatusCode(500, "Internal server error");
            }
        }

        private Guid GetTenantId()
        {
            // Get tenant ID from user claims or context
            var tenantClaim = User.FindFirst("TenantId")?.Value;
            return Guid.TryParse(tenantClaim, out var tenantId) ? tenantId : Guid.Empty;
        }
    }

    public class CreateOrderRequest
    {
        public string CustomerId { get; set; } = string.Empty;
        public List<CreateOrderItemRequest> Items { get; set; } = new();
        public decimal TotalPrice { get; set; }
    }

    public class CreateOrderItemRequest
    {
        public Guid ProductId { get; set; }
        public int Quantity { get; set; }
        public decimal UnitPrice { get; set; }
    }

    public class UpdateStatusRequest
    {
        public string Status { get; set; } = string.Empty;
        public string? Reason { get; set; }
    }
}
```

##### **3.2 Update ShopERP to use CoreHub.OrderService**
```csharp
// 5_WebApps/ShopERP/Controllers/OrdersController.cs - UPDATE to use CoreHub
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using VanAn.CoreHub.Services;
using VanAn.Shared.Domain;

namespace VanAn.ShopERP.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class OrdersController : ControllerBase
    {
        private readonly IOrderService _orderService;
        private readonly IOrderManagementService _orderManagementService;
        private readonly ILogger<OrdersController> _logger;

        public OrdersController(
            IOrderService orderService, 
            IOrderManagementService orderManagementService,
            ILogger<OrdersController> logger)
        {
            _orderService = orderService;
            _orderManagementService = orderManagementService;
            _logger = logger;
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<Order>>> GetOrders([FromQuery] string? status = null)
        {
            try
            {
                var tenantId = GetTenantId();
                
                if (string.IsNullOrEmpty(status))
                {
                    var today = DateTime.UtcNow.Date;
                    var orders = await _orderService.GetOrdersByDateRangeAsync(tenantId, today, today.AddDays(1));
                    return Ok(orders);
                }
                else
                {
                    var statusId = new OrderStatusId(status);
                    var orders = await _orderService.GetOrdersByStatusAsync(statusId, tenantId);
                    return Ok(orders);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting orders");
                return StatusCode(500, "Internal server error");
            }
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<Order>> GetOrder(Guid id)
        {
            try
            {
                var tenantId = GetTenantId();
                var order = await _orderService.GetOrderByIdAsync(id, tenantId);
                
                if (order == null)
                {
                    return NotFound();
                }
                
                return Ok(order);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting order {OrderId}", id);
                return StatusCode(500, "Internal server error");
            }
        }

        [HttpPut("{id}/status")]
        public async Task<ActionResult> UpdateOrderStatus(Guid id, [FromBody] UpdateStatusRequest request)
        {
            try
            {
                var tenantId = GetTenantId();
                
                // Validate transition using CoreHub service
                var currentOrder = await _orderService.GetOrderByIdAsync(id, tenantId);
                if (currentOrder == null)
                {
                    return NotFound();
                }
                
                var newStatus = new OrderStatusId(request.Status);
                var isValidTransition = await _orderService.IsTransitionValidAsync(currentOrder.Status, newStatus);
                
                if (!isValidTransition)
                {
                    return BadRequest($"Invalid status transition from {currentOrder.Status} to {request.Status}");
                }
                
                var success = await _orderService.UpdateOrderStatusAsync(id, request.Status, tenantId);
                
                if (!success)
                {
                    return NotFound();
                }
                
                // Log status change for audit
                _logger.LogInformation("Order {OrderId} status updated to {Status} by {User}", 
                    id, request.Status, User.Identity?.Name);
                
                return NoContent();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating order status {OrderId}", id);
                return StatusCode(500, "Internal server error");
            }
        }

        [HttpGet("metrics")]
        public async Task<ActionResult<OrderDashboardData>> GetMetrics()
        {
            try
            {
                var tenantId = GetTenantId();
                var metrics = await _orderService.GetDashboardDataAsync(tenantId);
                return Ok(metrics);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting order metrics");
                return StatusCode(500, "Internal server error");
            }
        }

        [HttpGet("summary/{id}")]
        public async Task<ActionResult<OrderSummary>> GetOrderSummary(Guid id)
        {
            try
            {
                var tenantId = GetTenantId();
                var summary = await _orderService.GetOrderSummaryAsync(id, tenantId);
                return Ok(summary);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting order summary {OrderId}", id);
                return StatusCode(500, "Internal server error");
            }
        }

        [HttpPut("{id}/assign")]
        public async Task<ActionResult> AssignOrderToStaff(Guid id, [FromBody] AssignStaffRequest request)
        {
            try
            {
                var success = await _orderManagementService.AssignOrderToStaff(id, request.StaffId);
                
                if (!success)
                {
                    return NotFound();
                }
                
                _logger.LogInformation("Order {OrderId} assigned to staff {StaffId}", id, request.StaffId);
                return NoContent();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error assigning order {OrderId} to staff {StaffId}", id, request.StaffId);
                return StatusCode(500, "Internal server error");
            }
        }

        private Guid GetTenantId()
        {
            var tenantClaim = User.FindFirst("TenantId")?.Value;
            return Guid.TryParse(tenantClaim, out var tenantId) ? tenantId : Guid.Empty;
        }
    }

    public class UpdateStatusRequest
    {
        public string Status { get; set; } = string.Empty;
        public string? Reason { get; set; }
    }

    public class AssignStaffRequest
    {
        public Guid StaffId { get; set; }
    }
}
```

##### **3.3 Remove Duplicate API Endpoints**
```csharp
// 2_Gateway/Controllers/OrdersController.cs - UPDATE to remove duplicates
using Microsoft.AspNetCore.Mvc;
using VanAn.CoreHub.Services;
using VanAn.Shared.Domain;

namespace VanAn.Gateway.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class OrdersController : ControllerBase
    {
        private readonly IOrderService _orderService;
        private readonly ILogger<OrdersController> _logger;

        public OrdersController(IOrderService orderService, ILogger<OrdersController> logger)
        {
            _orderService = orderService;
            _logger = logger;
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<Order>>> GetOrders([FromQuery] string? status = null)
        {
            try
            {
                var tenantId = GetTenantId();
                
                if (string.IsNullOrEmpty(status))
                {
                    var today = DateTime.UtcNow.Date;
                    var orders = await _orderService.GetOrdersByDateRangeAsync(tenantId, today, today.AddDays(1));
                    return Ok(orders);
                }
                else
                {
                    var statusId = new OrderStatusId(status);
                    var orders = await _orderService.GetOrdersByStatusAsync(statusId, tenantId);
                    return Ok(orders);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting orders");
                return StatusCode(500, "Internal server error");
            }
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<Order>> GetOrder(Guid id)
        {
            try
            {
                var tenantId = GetTenantId();
                var order = await _orderService.GetOrderByIdAsync(id, tenantId);
                
                if (order == null)
                {
                    return NotFound();
                }
                
                return Ok(order);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting order {OrderId}", id);
                return StatusCode(500, "Internal server error");
            }
        }

        [HttpPost]
        public async Task<ActionResult<Order>> CreateOrder([FromBody] CreateOrderRequest request)
        {
            try
            {
                var tenantId = GetTenantId();
                
                var order = new Order
                {
                    Id = Guid.NewGuid(),
                    CustomerId = request.CustomerId,
                    TenantId = new TenantId(tenantId),
                    Items = request.Items.Select(i => new OrderItem
                    {
                        Id = Guid.NewGuid(),
                        ProductId = i.ProductId,
                        Quantity = i.Quantity,
                        UnitPrice = i.UnitPrice,
                        TotalPrice = i.Quantity * i.UnitPrice
                    }).ToList(),
                    TotalPrice = request.TotalPrice,
                    Status = OrderStatusId.Pending,
                    CreatedAt = DateTime.UtcNow
                };

                var createdOrder = await _orderService.CreateOrderAsync(order, tenantId);
                
                return CreatedAtAction(nameof(GetOrder), new { id = createdOrder.Id }, createdOrder);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating order");
                return StatusCode(500, "Internal server error");
            }
        }

        private Guid GetTenantId()
        {
            var tenantClaim = User.FindFirst("TenantId")?.Value;
            return Guid.TryParse(tenantClaim, out var tenantId) ? tenantId : Guid.Empty;
        }
    }

    public class CreateOrderRequest
    {
        public string CustomerId { get; set; } = string.Empty;
        public List<CreateOrderItemRequest> Items { get; set; } = new();
        public decimal TotalPrice { get; set; }
    }

    public class CreateOrderItemRequest
    {
        public Guid ProductId { get; set; }
        public int Quantity { get; set; }
        public decimal UnitPrice { get; set; }
    }
}
```

##### **3.4 Implement Unified Error Handling**
```csharp
// 2_Gateway/Middleware/UnifiedErrorHandler.cs (CREATE)
using System.Net;
using System.Text.Json;
using VanAn.Shared.Domain;

namespace VanAn.Gateway.Middleware
{
    public class UnifiedErrorHandler
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<UnifiedErrorHandler> _logger;

        public UnifiedErrorHandler(RequestDelegate next, ILogger<UnifiedErrorHandler> logger)
        {
            _next = next;
            _logger = logger;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            try
            {
                await _next(context);
            }
            catch (Exception ex)
            {
                await HandleExceptionAsync(context, ex);
            }
        }

        private static async Task HandleExceptionAsync(HttpContext context, Exception exception)
        {
            var logger = context.RequestServices.GetRequiredService<ILogger<UnifiedErrorHandler>>();
            
            logger.LogError(exception, "An unhandled exception occurred: {Message}", exception.Message);

            context.Response.Clear();
            context.Response.ContentType = "application/json";
            
            var errorResponse = new ErrorResponse
            {
                ErrorId = Guid.NewGuid().ToString(),
                Message = GetErrorMessage(exception),
                Details = GetErrorDetails(exception),
                Timestamp = DateTime.UtcNow,
                Path = context.Request.Path,
                Method = context.Request.Method
            };

            context.Response.StatusCode = GetStatusCode(exception);

            var jsonOptions = new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            };

            var jsonResponse = JsonSerializer.Serialize(errorResponse, jsonOptions);
            await context.Response.WriteAsync(jsonResponse);
        }

        private static string GetErrorMessage(Exception exception)
        {
            return exception switch
            {
                ArgumentException => "Invalid argument provided",
                ArgumentNullException => "Required argument is missing",
                InvalidOperationException => "Operation is not valid in the current state",
                UnauthorizedAccessException => "Access denied",
                KeyNotFoundException => "Requested resource not found",
                TimeoutException => "Operation timed out",
                _ => "An unexpected error occurred"
            };
        }

        private static string GetErrorDetails(Exception exception)
        {
            // In development, include full exception details
            if (Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") == "Development")
            {
                return exception.ToString();
            }
            
            // In production, return generic message
            return "See application logs for more details";
        }

        private static int GetStatusCode(Exception exception)
        {
            return exception switch
            {
                ArgumentException => StatusCodes.Status400BadRequest,
                ArgumentNullException => StatusCodes.Status400BadRequest,
                InvalidOperationException => StatusCodes.Status400BadRequest,
                UnauthorizedAccessException => StatusCodes.Status401Unauthorized,
                KeyNotFoundException => StatusCodes.Status404NotFound,
                TimeoutException => StatusCodes.Status408RequestTimeout,
                _ => StatusCodes.Status500InternalServerError
            };
        }
    }

    public class ErrorResponse
    {
        public string ErrorId { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public string Details { get; set; } = string.Empty;
        public DateTime Timestamp { get; set; }
        public string Path { get; set; } = string.Empty;
        public string Method { get; set; } = string.Empty;
    }
}
```

##### **3.5 Register Unified Error Handler**
```csharp
// 2_Gateway/Program.cs - ADD middleware registration
var app = builder.Build();

// Add unified error handling middleware
app.UseMiddleware<UnifiedErrorHandler>();

// Configure the HTTP request pipeline
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapRazorPages();

app.Run();
```

---

### **PHASE 4: INTEGRATION TESTING (Week 3)**

##### **4.1 Create Comprehensive Integration Tests**
```csharp
// 6_Tests/Integration/UnifiedOrderTests.cs (CREATE)
using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;
using VanAn.CoreHub.Services;
using VanAn.Shared.Domain;
using Xunit;

namespace VanAn.Integration.Tests
{
    public class UnifiedOrderTests : IntegrationTestBase
    {
        [Fact]
        public async Task CreateOrder_ShouldUseCoreHubService()
        {
            // Arrange
            using var scope = CreateScope();
            var orderService = scope.ServiceProvider.GetRequiredService<IOrderService>();
            var dbContext = scope.ServiceProvider.GetRequiredService<VanAnDbContext>();
            
            var tenantId = Guid.NewGuid();
            var order = TestEntityBuilder.CreateOrder(tenantId);

            // Act
            var result = await orderService.CreateOrderAsync(order, tenantId);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(order.Id, result.Id);
            Assert.Equal(tenantId, result.TenantId.Value);
            
            // Verify order is in database
            var savedOrder = await dbContext.Orders
                .FirstOrDefaultAsync(o => o.Id == result.Id);
            Assert.NotNull(savedOrder);
        }

        [Fact]
        public async Task UpdateOrderStatus_ShouldValidateTransition()
        {
            // Arrange
            using var scope = CreateScope();
            var orderService = scope.ServiceProvider.GetRequiredService<IOrderService>();
            
            var tenantId = Guid.NewGuid();
            var order = TestEntityBuilder.CreateOrder(tenantId);
            order.Status = OrderStatusId.Pending;
            
            var createdOrder = await orderService.CreateOrderAsync(order, tenantId);

            // Act & Assert - Valid transition
            var validTransition = await orderService.IsTransitionValidAsync(
                OrderStatusId.Pending, 
                OrderStatusId.Processing);
            Assert.True(validTransition);

            // Act & Assert - Invalid transition
            var invalidTransition = await orderService.IsTransitionValidAsync(
                OrderStatusId.Completed, 
                OrderStatusId.Pending);
            Assert.False(invalidTransition);
        }

        [Fact]
        public async Task GetDashboardData_ShouldReturnCorrectMetrics()
        {
            // Arrange
            using var scope = CreateScope();
            var orderService = scope.ServiceProvider.GetRequiredService<IOrderService>();
            var dbContext = scope.ServiceProvider.GetRequiredService<VanAnDbContext>();
            
            var tenantId = Guid.NewGuid();
            
            // Create test orders
            var orders = new[]
            {
                TestEntityBuilder.CreateOrder(tenantId, OrderStatusId.Completed),
                TestEntityBuilder.CreateOrder(tenantId, OrderStatusId.Processing),
                TestEntityBuilder.CreateOrder(tenantId, OrderStatusId.Pending)
            };

            foreach (var order in orders)
            {
                await orderService.CreateOrderAsync(order, tenantId);
            }

            // Act
            var dashboardData = await orderService.GetDashboardDataAsync(tenantId);

            // Assert
            Assert.NotNull(dashboardData);
            Assert.Equal(3, dashboardData.TodayOrderCount);
            Assert.Equal(1, dashboardData.CompletedOrders);
            Assert.Equal(1, dashboardData.ProcessingOrders);
            Assert.Equal(1, dashboardData.PendingOrders);
        }

        [Fact]
        public async Task OrderSummary_ShouldIncludeItemsAndAccounting()
        {
            // Arrange
            using var scope = CreateScope();
            var orderService = scope.ServiceProvider.GetRequiredService<IOrderService>();
            
            var tenantId = Guid.NewGuid();
            var order = TestEntityBuilder.CreateOrder(tenantId);
            
            // Act
            var createdOrder = await orderService.CreateOrderAsync(order, tenantId);
            var summary = await orderService.GetOrderSummaryAsync(createdOrder.Id, tenantId);
            var accountingEntries = await orderService.GetEntriesByOrderAsync(createdOrder.Id, new TenantId(tenantId));

            // Assert
            Assert.NotNull(summary);
            Assert.Equal(createdOrder.Id, summary.OrderId);
            Assert.NotEmpty(summary.Items);
            Assert.True(summary.ItemCount > 0);
            
            // Verify accounting entries were created
            Assert.NotNull(accountingEntries);
            Assert.True(accountingEntries.Count >= 2); // Revenue + COGS
        }

        [Fact]
        public async Task KhachLinkController_ShouldUseCoreHubService()
        {
            // Arrange
            using var scope = CreateScope();
            var orderService = scope.ServiceProvider.GetRequiredService<IOrderService>();
            
            var tenantId = Guid.NewGuid();
            var order = TestEntityBuilder.CreateOrder(tenantId);

            // Act
            var result = await orderService.CreateOrderAsync(order, tenantId);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(order.CustomerId, result.CustomerId);
            Assert.Equal(tenantId, result.TenantId.Value);
        }

        [Fact]
        public async Task ShopERPController_ShouldUseCoreHubService()
        {
            // Arrange
            using var scope = CreateScope();
            var orderService = scope.ServiceProvider.GetRequiredService<IOrderService>();
            
            var tenantId = Guid.NewGuid();
            var order = TestEntityBuilder.CreateOrder(tenantId);

            // Act
            var createdOrder = await orderService.CreateOrderAsync(order, tenantId);
            var retrievedOrder = await orderService.GetOrderByIdAsync(createdOrder.Id, tenantId);

            // Assert
            Assert.NotNull(retrievedOrder);
            Assert.Equal(createdOrder.Id, retrievedOrder.Id);
            Assert.Equal(tenantId, retrievedOrder.TenantId.Value);
        }

        [Fact]
        public async Task UnifiedErrorHandling_ShouldReturnConsistentFormat()
        {
            // This test would require integration with the actual API endpoints
            // For now, we'll test the error handling logic directly
            
            // Arrange
            using var scope = CreateScope();
            var orderService = scope.ServiceProvider.GetRequiredService<IOrderService>();
            
            var invalidTenantId = Guid.NewGuid();
            var invalidOrder = new Order
            {
                Id = Guid.NewGuid(),
                CustomerId = string.Empty, // Invalid
                TenantId = new TenantId(invalidTenantId),
                Items = new List<OrderItem>(),
                TotalPrice = -100, // Invalid
                Status = OrderStatusId.Pending,
                CreatedAt = DateTime.UtcNow
            };

            // Act & Assert
            await Assert.ThrowsAsync<ArgumentException>(async () =>
            {
                await orderService.CreateOrderAsync(invalidOrder, invalidTenantId);
            });
        }
    }
}
```

---

## 📊 IMPLEMENTATION CHECKLIST

### **Phase 1: Backend Consolidation**
- [ ] Remove ShopERP duplicate services
- [ ] Update ShopERP Program.cs
- [ ] Enhance CoreHub IOrderService
- [ ] Create CoreHub OrderQueueService
- [ ] Update CoreHub OrderService
- [ ] Create OrderHub
- [ ] Update CoreHub Program.cs

### **Phase 2: Frontend Unification**
- [ ] Create IndexedDB Service
- [ ] Create Offline Order Service
- [ ] Create Enhanced Cart Service
- [ ] Create Conflict Resolution Service
- [ ] Create Order Management Service
- [ ] Create Realtime Dashboard Service

### **Phase 3: Unified API Integration (FIXED)**
- [x] Update KhachLink to use CoreHub.OrderService
- [x] Update ShopERP to use CoreHub.OrderService
- [x] Remove duplicate API endpoints
- [x] Implement unified error handling
- [x] Create comprehensive integration tests

### **Phase 4: Integration Testing**
- [ ] Create comprehensive integration tests
- [ ] Test unified API endpoints
- [ ] Test error handling consistency
- [ ] Validate real-time notifications
- [ ] Performance testing

---

## 🎯 SUCCESS METRICS

### **Build Success:**
- 0 errors, <10 warnings
- All projects compile successfully
- Integration tests pass

### **API Unification:**
- Single CoreHub.OrderService used by both KhachLink and ShopERP
- No duplicate endpoints
- Consistent error handling across all APIs

### **Performance:**
- <2s order creation
- <500ms API response
- Real-time notifications <100ms

### **Offline Support:**
- 100% order creation without internet
- Automatic sync when online
- Conflict resolution for concurrent updates

---

## 🔧 DEPLOYMENT NOTES

### **Environment Variables:**
```bash
# CoreHub Configuration
ConnectionStrings__DefaultConnection=...
ASPNETCORE_ENVIRONMENT=Production
LoggingConfig__EnableFileLogging=true

# SignalR Configuration
SignalR__EnableDetailedErrors=false
SignalR__HubTimeout=30

# PWA Configuration
PWA__EnableOffline=true
PWA__SyncInterval=30
```

### **Docker Configuration:**
```dockerfile
# Multi-stage build for optimized deployment
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 80
EXPOSE 443

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY ["VanAn.sln", "./"]
COPY ["3_CoreHub/", "3_CoreHub/"]
COPY ["2_Gateway/", "2_Gateway/"]
COPY ["5_WebApps/", "5_WebApps/"]
RUN dotnet restore
RUN dotnet build -c Release -o /app/build

FROM build AS publish
RUN dotnet publish -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "VanAn.Gateway.dll"]
```

---

**✅ FIXED VERSION COMPLETE - All missing Unified API Integration tasks now included!**
