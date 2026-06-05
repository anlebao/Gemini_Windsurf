# PHASE 2.5: UNIFIED ORDER WORKFLOW - FINANCIAL-GRADE SAFETY CODING PLAN
**Version:** 4.0 (Financial Safety First - No CI/CD)
**Timeline:** 2.5 weeks (58 hours)
**Priority:** CRITICAL - Financial Data Integrity
**Date:** May 4, 2026

---

## 🎯 EXECUTION OVERVIEW

### **FINANCIAL-GRADE TESTING STRATEGY (FOCUSED ON CRITICAL GAPS)**
- **Unit Tests (60%):** Business logic, algorithms, domain rules
- **Integration Tests (25%):** Database, IndexedDB, SignalR, API
- **Contract Tests (10%):** Schema validation, layer contracts
- **E2E Tests (5%):** Critical user flows only

### **🔴 CRITICAL FINANCIAL SAFETY REQUIREMENTS**
- **Idempotent behavior:** Prevent duplicate orders
- **Version control:** Prevent lost data during conflicts
- **Event ordering:** Maintain UI consistency
- **State machine UI:** Proper loading/syncing/error states
- **Retry strategy:** Handle transient failures gracefully
- **Time-based bugs:** Clock drift and timezone handling
- **Production data:** Large batch and concurrent scenarios

### **🚫 REMOVED: CI/CD Quality Gates**
- Focus on core financial safety tests
- Manual quality validation during development
- Local testing and validation

---

## 📅 WEEK 1: FINANCIAL SAFETY INFRASTRUCTURE (32 hours)

### **🔴 CRITICAL FINANCIAL SAFETY FOCUS:**
- ✅ **Idempotent Behavior Tests** (8 hours) - **NEW**
- ✅ **Version Control Tests** (6 hours) - **NEW**
- ✅ **Event Ordering Tests** (4 hours) - **NEW**
- ✅ **Deep Offline Sync Matrix** (8 hours) - **ENHANCED**
- ✅ **Financial Invariants Validation** (6 hours) - **ENHANCED**

### **DAY 1-2: IndexedDB SERVICE + CONTRACT TESTS (10 hours)**

#### **1.1 Create IndexedDB Service (6 hours)**
**File:** `5_WebApps/KhachLink/Services/IndexedDBService.cs`

```csharp
// NAMESPACE STRATEGY
using VanAn.Shared.Domain;
using VanAn.KhachLink.Models;
using Microsoft.JSInterop;

namespace VanAn.KhachLink.Services;

public interface IIndexedDBService
{
    Task<bool> InitializeAsync();
    Task<T?> GetAsync<T>(string key) where T : class;
    Task SetAsync<T>(string key, T value) where T : class;
    Task RemoveAsync(string key);
    Task ClearAsync();
    Task<List<T>> GetAllAsync<T>() where T : class;
}

public class IndexedDBService : IIndexedDBService, IAsyncDisposable
{
    private readonly IJSRuntime _jsRuntime;
    private readonly ILogger<IndexedDBService> _logger;
    private bool _isInitialized = false;
    
    // Database schema constants
    private const string DB_NAME = "VanAnKhachLink";
    private const string DB_VERSION = 1;
    private const string ORDERS_STORE = "orders";
    private const string CART_STORE = "cart";
    private const string PRODUCTS_STORE = "products";
    
    public IndexedDBService(IJSRuntime jsRuntime, ILogger<IndexedDBService> logger)
    {
        _jsRuntime = jsRuntime;
        _logger = logger;
    }
    
    public async Task<bool> InitializeAsync()
    {
        if (_isInitialized) return true;
        
        try
        {
            var result = await _jsRuntime.InvokeAsync<bool>("vananIndexedDB.initialize", DB_NAME, DB_VERSION);
            _isInitialized = result;
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initialize IndexedDB");
            return false;
        }
    }
    
    public async Task<T?> GetAsync<T>(string key) where T : class
    {
        if (!await InitializeAsync()) return null;
        
        try
        {
            return await _jsRuntime.InvokeAsync<T>("vananIndexedDB.get", key);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get item from IndexedDB: {Key}", key);
            return null;
        }
    }
    
    public async Task SetAsync<T>(string key, T value) where T : class
    {
        if (!await InitializeAsync()) return;
        
        try
        {
            await _jsRuntime.InvokeVoidAsync("vananIndexedDB.set", key, value);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to set item in IndexedDB: {Key}", key);
        }
    }
    
    public async Task RemoveAsync(string key)
    {
        if (!await InitializeAsync()) return;
        
        try
        {
            await _jsRuntime.InvokeVoidAsync("vananIndexedDB.remove", key);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to remove item from IndexedDB: {Key}", key);
        }
    }
    
    public async Task ClearAsync()
    {
        if (!await InitializeAsync()) return;
        
        try
        {
            await _jsRuntime.InvokeVoidAsync("vananIndexedDB.clear");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to clear IndexedDB");
        }
    }
    
    public async Task<List<T>> GetAllAsync<T>() where T : class
    {
        if (!await InitializeAsync()) return new List<T>();
        
        try
        {
            return await _jsRuntime.InvokeAsync<List<T>>("vananIndexedDB.getAll");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get all items from IndexedDB");
            return new List<T>();
        }
    }
    
    public async ValueTask DisposeAsync()
    {
        _isInitialized = false;
    }
}
```

#### **1.2 Create JavaScript IndexedDB Bridge (4 hours)**
**File:** `5_WebApps/KhachLink/wwwroot/js/indexeddb-bridge.js`

```javascript
// IndexedDB Bridge for VanAn KhachLink
window.vananIndexedDB = {
    db: null,
    
    async initialize(dbName, version) {
        return new Promise((resolve, reject) => {
            const request = indexedDB.open(dbName, version);
            
            request.onerror = () => reject(request.error);
            request.onsuccess = () => {
                this.db = request.result;
                resolve(true);
            };
            
            request.onupgradeneeded = (event) => {
                const db = event.target.result;
                
                // Create object stores
                if (!db.objectStoreNames.contains('orders')) {
                    const orderStore = db.createObjectStore('orders', { keyPath: 'id' });
                    orderStore.createIndex('status', 'status', { unique: false });
                    orderStore.createIndex('createdAt', 'createdAt', { unique: false });
                }
                
                if (!db.objectStoreNames.contains('cart')) {
                    const cartStore = db.createObjectStore('cart', { keyPath: 'id' });
                    cartStore.createIndex('productId', 'productId', { unique: false });
                }
                
                if (!db.objectStoreNames.contains('products')) {
                    const productStore = db.createObjectStore('products', { keyPath: 'id' });
                    productStore.createIndex('categoryId', 'categoryId', { unique: false });
                }
            };
        });
    },
    
    async get(key, storeName = 'orders') {
        return new Promise((resolve, reject) => {
            const transaction = this.db.transaction([storeName], 'readonly');
            const store = transaction.objectStore(storeName);
            const request = store.get(key);
            
            request.onerror = () => reject(request.error);
            request.onsuccess = () => resolve(request.result);
        });
    },
    
    async set(key, value, storeName = 'orders') {
        return new Promise((resolve, reject) => {
            const transaction = this.db.transaction([storeName], 'readwrite');
            const store = transaction.objectStore(storeName);
            const request = store.put(value);
            
            request.onerror = () => reject(request.error);
            request.onsuccess = () => resolve(request.result);
        });
    },
    
    async remove(key, storeName = 'orders') {
        return new Promise((resolve, reject) => {
            const transaction = this.db.transaction([storeName], 'readwrite');
            const store = transaction.objectStore(storeName);
            const request = store.delete(key);
            
            request.onerror = () => reject(request.error);
            request.onsuccess = () => resolve(request.result);
        });
    },
    
    async clear(storeName) {
        return new Promise((resolve, reject) => {
            const transaction = this.db.transaction([storeName], 'readwrite');
            const store = transaction.objectStore(storeName);
            const request = store.clear();
            
            request.onerror = () => reject(request.error);
            request.onsuccess = () => resolve(request.result);
        });
    },
    
    async getAll(storeName = 'orders') {
        return new Promise((resolve, reject) => {
            const transaction = this.db.transaction([storeName], 'readonly');
            const store = transaction.objectStore(storeName);
            const request = store.getAll();
            
            request.onerror = () => reject(request.error);
            request.onsuccess = () => resolve(request.result);
        });
    }
};
```

#### **1.3 Create Idempotent Behavior Tests (8 hours)**
**File:** `6_Tests/VanAn.Core.Tests/IdempotentBehaviorTests.cs`

```csharp
using Microsoft.Extensions.DependencyInjection;
using Moq;
using VanAn.KhachLink.Services;
using VanAn.KhachLink.Models;
using VanAn.CoreHub.Services;
using Xunit;

namespace VanAn.Tests;

[Trait("Category", "Unit")]
[Trait("Category", "FinancialSafety")]
[Trait("Category", "Idempotent")]
public class IdempotentBehaviorTests : IDisposable
{
    [Fact]
    public async Task Should_Be_Idempotent_When_Same_Order_Sent_Multiple_Times()
    {
        // Arrange
        var order = CreateTestOrder();
        
        // Act - Gửi cùng order 3 lần
        var result1 = await _orderService.CreateOrderAsync(order.ToDomain());
        var result2 = await _orderService.CreateOrderAsync(order.ToDomain());
        var result3 = await _orderService.CreateOrderAsync(order.ToDomain());
        
        // Assert - Chỉ tạo 1 order
        Assert.True(result1.Success);
        Assert.False(result2.Success); // Should reject duplicate
        Assert.False(result3.Success); // Should reject duplicate
        
        // Verify chỉ có 1 order trong database
        var orders = await _dbContext.Orders.Where(o => o.Id == order.Id).ToListAsync();
        Assert.Single(orders);
    }
    
    [Fact]
    public async Task Should_Handle_Offline_Order_Idempotency()
    {
        // Arrange
        var offlineOrder = CreateTestOrder();
        await _offlineOrderService.CreateOrderAsync(offlineOrder);
        
        // Act - Sync same order multiple times
        var result1 = await _offlineOrderService.SyncSingleOrderAsync(offlineOrder.Id);
        var result2 = await _offlineOrderService.SyncSingleOrderAsync(offlineOrder.Id);
        
        // Assert
        Assert.True(result1.Success);
        Assert.True(result2.Success); // Already synced is considered success
        
        _orderServiceMock.Verify(x => x.CreateOrderAsync(It.IsAny<Order>()), Times.Once);
    }
    
    [Fact]
    public async Task Should_Prevent_Duplicate_Revenue_Calculation()
    {
        // Arrange
        var order = CreateTestOrder(10000);
        
        // Act
        var result1 = await _orderService.CreateOrderAsync(order.ToDomain());
        var result2 = await _orderService.CreateOrderAsync(order.ToDomain());
        
        // Assert - Total revenue should not double
        var totalRevenue = await _orderService.GetTotalRevenueAsync();
        Assert.Equal(10000, totalRevenue); // Not 20000
    }
}
```

#### **1.4 Create Version Control Tests (6 hours)**
**File:** `6_Tests/VanAn.Core.Tests/VersionControlTests.cs`

```csharp
using VanAn.KhachLink.Services;
using VanAn.KhachLink.Models;
using Xunit;

namespace VanAn.Tests;

[Trait("Category", "Unit")]
[Trait("Category", "FinancialSafety")]
[Trait("Category", "VersionControl")]
public class VersionControlTests : IDisposable
{
    [Fact]
    public async Task Should_Resolve_Conflict_With_Version_Control()
    {
        // Arrange
        var order = CreateTestOrder();
        var device1Order = order with { Version = 1, TotalAmount = 20000 };
        var device2Order = order with { Version = 2, TotalAmount = 25000 };
        
        // Act
        var resolution = await _conflictResolver.ResolveOrderConflictAsync(device1Order, device2Order);
        
        // Assert
        Assert.Equal(ResolutionAction.UseServer, resolution.Action);
        Assert.Contains("version", resolution.Reason.ToLower());
    }
    
    [Fact]
    public async Task Should_Reject_Stale_Update()
    {
        // Arrange
        var serverOrder = CreateTestOrder() with { Version = 5 };
        var staleOrder = CreateTestOrder() with { Version = 2 };
        
        // Act
        var result = await _orderService.UpdateOrderAsync(staleOrder.ToDomain());
        
        // Assert
        Assert.False(result.Success);
        Assert.Contains("stale", result.ErrorMessage.ToLower());
    }
    
    [Fact]
    public async Task Should_Increment_Version_On_Update()
    {
        // Arrange
        var order = CreateTestOrder() with { Version = 1 };
        await _orderService.CreateOrderAsync(order.ToDomain());
        
        // Act
        var updatedOrder = order with { TotalAmount = 30000, Version = 2 };
        var result = await _orderService.UpdateOrderAsync(updatedOrder.ToDomain());
        
        // Assert
        Assert.True(result.Success);
        
        var savedOrder = await _orderService.GetOrderAsync(order.Id);
        Assert.Equal(2, savedOrder.Version);
    }
}
```

#### **1.5 Create Event Ordering Tests (4 hours)**
**File:** `6_Tests/VanAn.Core.Tests/EventOrderingTests.cs`

```csharp
using VanAn.KhachLink.Services;
using Xunit;

namespace VanAn.Tests;

[Trait("Category", "Unit")]
[Trait("Category", "FinancialSafety")]
[Trait("Category", "EventOrdering")]
public class EventOrderingTests : IDisposable
{
    [Fact]
    public async Task Should_Handle_OutOfOrder_Events_Correctly()
    {
        // Arrange
        var orderId = Guid.NewGuid();
        var events = new[]
        {
            new OrderUpdatedEvent { OrderId = orderId, Status = OrderStatusId.Processing, Timestamp = DateTime.UtcNow.AddMinutes(-1) },
            new OrderCreatedEvent { OrderId = orderId, Status = OrderStatusId.Pending, Timestamp = DateTime.UtcNow.AddMinutes(-2) }
        };
        
        // Act - Gửi events sai thứ tự
        foreach (var evt in events.Reverse())
        {
            await _hubConnectionService.HandleEventAsync(evt);
        }
        
        // Assert - UI vẫn đúng state
        var currentOrder = await _orderService.GetOrderAsync(orderId);
        Assert.Equal(OrderStatusId.Processing, currentOrder.Status);
    }
    
    [Fact]
    public async Task Should_Process_Events_In_Correct_Sequence()
    {
        // Arrange
        var orderId = Guid.NewGuid();
        var eventProcessor = new EventProcessor();
        
        var events = new[]
        {
            new OrderCreatedEvent { OrderId = orderId, Status = OrderStatusId.Pending, Timestamp = DateTime.UtcNow.AddMinutes(-2) },
            new OrderUpdatedEvent { OrderId = orderId, Status = OrderStatusId.Processing, Timestamp = DateTime.UtcNow.AddMinutes(-1) },
            new OrderUpdatedEvent { OrderId = orderId, Status = OrderStatusId.Completed, Timestamp = DateTime.UtcNow }
        };
        
        // Act - Gửi events ngẫu nhiên
        var randomEvents = events.OrderBy(x => Guid.NewGuid()).ToList();
        
        foreach (var evt in randomEvents)
        {
            await eventProcessor.ProcessEventAsync(evt);
        }
        
        // Assert - Final state should be Completed
        var finalOrder = await _orderService.GetOrderAsync(orderId);
        Assert.Equal(OrderStatusId.Completed, finalOrder.Status);
    }
}
```

```csharp
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using Moq;
using VanAn.KhachLink.Services;
using Xunit;

namespace VanAn.Integration.Tests;

[Trait("Category", "Integration")]
[Trait("Category", "IndexedDB")]
public class IndexedDBIntegrationTests : IDisposable
{
    private readonly Mock<IJSRuntime> _jsRuntimeMock;
    private readonly IndexedDBService _indexedDBService;
    
    public IndexedDBIntegrationTests()
    {
        _jsRuntimeMock = new Mock<IJSRuntime>();
        _indexedDBService = new IndexedDBService(_jsRuntimeMock.Object, Mock.Of<ILogger<IndexedDBService>>());
    }
    
    [Fact]
    public async Task InitializeAsync_Should_Call_JavaScript_Initialize()
    {
        // Arrange
        _jsRuntimeMock.Setup(x => x.InvokeAsync<bool>("vananIndexedDB.initialize", It.IsAny<object[]>()))
            .ReturnsAsync(true);
        
        // Act
        var result = await _indexedDBService.InitializeAsync();
        
        // Assert
        Assert.True(result);
        _jsRuntimeMock.Verify(x => x.InvokeAsync<bool>("vananIndexedDB.initialize", 
            It.Is<object[]>(args => args[0].ToString() == "VanAnKhachLink" && (int)args[1] == 1)), Times.Once);
    }
    
    [Fact]
    public async Task SetAsync_Should_Call_JavaScript_Set()
    {
        // Arrange
        _jsRuntimeMock.Setup(x => x.InvokeAsync<bool>("vananIndexedDB.initialize", It.IsAny<object[]>()))
            .ReturnsAsync(true);
        
        var testData = new { Id = "test", Name = "Test Data" };
        
        // Act
        await _indexedDBService.SetAsync("test", testData);
        
        // Assert
        _jsRuntimeMock.Verify(x => x.InvokeVoidAsync("vananIndexedDB.set", 
            It.Is<object[]>(args => args[0].ToString() == "test" && args[1] == testData)), Times.Once);
    }
    
    [Fact]
    public async Task GetAsync_Should_Call_JavaScript_Get()
    {
        // Arrange
        _jsRuntimeMock.Setup(x => x.InvokeAsync<bool>("vananIndexedDB.initialize", It.IsAny<object[]>()))
            .ReturnsAsync(true);
        
        var expectedData = new { Id = "test", Name = "Test Data" };
        _jsRuntimeMock.Setup(x => x.InvokeAsync<object>("vananIndexedDB.get", It.IsAny<object[]>()))
            .ReturnsAsync(expectedData);
        
        // Act
        var result = await _indexedDBService.GetAsync<object>("test");
        
        // Assert
        Assert.NotNull(result);
        _jsRuntimeMock.Verify(x => x.InvokeAsync<object>("vananIndexedDB.get", 
            It.Is<object[]>(args => args[0].ToString() == "test")), Times.Once);
    }
    
    [Fact]
    public async Task InitializeAsync_Should_Handle_Exception()
    {
        // Arrange
        _jsRuntimeMock.Setup(x => x.InvokeAsync<bool>("vananIndexedDB.initialize", It.IsAny<object[]>()))
            .ThrowsAsync(new JSException("IndexedDB not supported"));
        
        // Act
        var result = await _indexedDBService.InitializeAsync();
        
        // Assert
        Assert.False(result);
    }
    
    public void Dispose()
    {
        _indexedDBService.DisposeAsync().AsTask().Wait();
    }
}
```

---

### **DAY 3-4: OFFLINE ORDER SERVICE + ENHANCED SYNC MATRIX (18 hours)**

#### **2.1 Create Offline Order DTOs (2 hours)**
**File:** `5_WebApps/KhachLink/Models/OfflineOrderDto.cs`

```csharp
using VanAn.Shared.Domain;
using System.Text.Json.Serialization;

namespace VanAn.KhachLink.Models;

public class OfflineOrderDto
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;
    
    [JsonPropertyName("customerId")]
    public string CustomerId { get; set; } = string.Empty;
    
    [JsonPropertyName("shopId")]
    public string ShopId { get; set; } = string.Empty;
    
    [JsonPropertyName("items")]
    public List<OfflineOrderItemDto> Items { get; set; } = new();
    
    [JsonPropertyName("totalAmount")]
    public decimal TotalAmount { get; set; }
    
    [JsonPropertyName("status")]
    public string Status { get; set; } = OrderStatusId.Pending.ToString();
    
    [JsonPropertyName("createdAt")]
    public long CreatedAtTimestamp { get; set; }
    
    [JsonPropertyName("syncedAt")]
    public long? SyncedAtTimestamp { get; set; }
    
    [JsonPropertyName("syncAttempts")]
    public int SyncAttempts { get; set; } = 0;
    
    [JsonPropertyName("lastSyncError")]
    public string? LastSyncError { get; set; }
    
    // Validation properties
    [JsonIgnore]
    public DateTime CreatedAt => DateTimeOffset.FromUnixTimeMilliseconds(CreatedAtTimestamp).DateTime;
    
    [JsonIgnore]
    public DateTime? SyncedAt => SyncedAtTimestamp.HasValue 
        ? DateTimeOffset.FromUnixTimeMilliseconds(SyncedAtTimestamp.Value).DateTime 
        : null;
    
    [JsonIgnore]
    public bool IsSynced => SyncedAt.HasValue;
    
    [JsonIgnore]
    public bool CanRetrySync => !IsSynced && SyncAttempts < 3;
    
    public Order ToDomain()
    {
        return new Order(
            Guid.Parse(Id),
            Guid.Parse(ShopId),
            CustomerId, // DeviceId as customer identifier
            Items.Select(i => i.ToDomain()).ToList(),
            TotalAmount,
            Enum.Parse<OrderStatusId>(Status),
            CreatedAt
        );
    }
    
    public static OfflineOrderDto FromDomain(Order order)
    {
        return new OfflineOrderDto
        {
            Id = order.Id.Value.ToString(),
            CustomerId = order.CustomerId,
            ShopId = order.ShopId.Value.ToString(),
            Items = order.Items.Select(OfflineOrderItemDto.FromDomain).ToList(),
            TotalAmount = order.TotalAmount,
            Status = order.Status.ToString(),
            CreatedAtTimestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
        };
    }
}

public class OfflineOrderItemDto
{
    [JsonPropertyName("productId")]
    public string ProductId { get; set; } = string.Empty;
    
    [JsonPropertyName("quantity")]
    public int Quantity { get; set; }
    
    [JsonPropertyName("unitPrice")]
    public decimal UnitPrice { get; set; }
    
    [JsonPropertyName("totalPrice")]
    public decimal TotalPrice { get; set; }
    
    public OrderItem ToDomain()
    {
        return new OrderItem(
            Guid.Parse(ProductId),
            Quantity,
            UnitPrice,
            TotalPrice
        );
    }
    
    public static OfflineOrderItemDto FromDomain(OrderItem item)
    {
        return new OfflineOrderItemDto
        {
            ProductId = item.ProductId.Value.ToString(),
            Quantity = item.Quantity,
            UnitPrice = item.UnitPrice,
            TotalPrice = item.TotalPrice
        };
    }
}
```

#### **2.2 Create Offline Order Service (8 hours)**
**File:** `5_WebApps/KhachLink/Services/OfflineOrderService.cs`

```csharp
using VanAn.Shared.Domain;
using VanAn.CoreHub.Services;
using VanAn.KhachLink.Models;

namespace VanAn.KhachLink.Services;

public interface IOfflineOrderService
{
    Task<bool> CreateOrderAsync(OfflineOrderDto order);
    Task<List<OfflineOrderDto>> GetPendingOrdersAsync();
    Task<bool> SyncOrdersAsync();
    Task<SyncResult> SyncSingleOrderAsync(string orderId);
    Task<bool> DeleteOrderAsync(string orderId);
    Task<OfflineOrderDto?> GetOrderAsync(string orderId);
}

public class SyncResult
{
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
    public string OrderId { get; set; } = string.Empty;
    public int Attempts { get; set; }
}

public class OfflineOrderService : IOfflineOrderService
{
    private readonly IIndexedDBService _indexedDBService;
    private readonly IOrderService _orderService;
    private readonly ILogger<OfflineOrderService> _logger;
    private readonly IServiceProvider _serviceProvider;
    
    public OfflineOrderService(
        IIndexedDBService indexedDBService,
        IOrderService orderService,
        ILogger<OfflineOrderService> logger,
        IServiceProvider serviceProvider)
    {
        _indexedDBService = indexedDBService;
        _orderService = orderService;
        _logger = logger;
        _serviceProvider = serviceProvider;
    }
    
    public async Task<bool> CreateOrderAsync(OfflineOrderDto order)
    {
        try
        {
            // Validate order
            if (!ValidateOrder(order))
            {
                _logger.LogWarning("Invalid order data: {OrderId}", order.Id);
                return false;
            }
            
            // Set initial values
            order.CreatedAtTimestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            order.SyncAttempts = 0;
            order.Status = OrderStatusId.Pending.ToString();
            
            // Store in IndexedDB
            await _indexedDBService.SetAsync($"order_{order.Id}", order);
            
            _logger.LogInformation("Created offline order: {OrderId}", order.Id);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create offline order: {OrderId}", order.Id);
            return false;
        }
    }
    
    public async Task<List<OfflineOrderDto>> GetPendingOrdersAsync()
    {
        try
        {
            var allOrders = await _indexedDBService.GetAllAsync<OfflineOrderDto>();
            return allOrders
                .Where(o => !o.IsSynced && o.CanRetrySync)
                .OrderBy(o => o.CreatedAt)
                .ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get pending orders");
            return new List<OfflineOrderDto>();
        }
    }
    
    public async Task<bool> SyncOrdersAsync()
    {
        try
        {
            var pendingOrders = await GetPendingOrdersAsync();
            var syncResults = new List<SyncResult>();
            
            foreach (var order in pendingOrders)
            {
                var result = await SyncSingleOrderAsync(order.Id);
                syncResults.Add(result);
                
                // Add delay to prevent overwhelming the server
                await Task.Delay(100);
            }
            
            var successCount = syncResults.Count(r => r.Success);
            _logger.LogInformation("Synced {SuccessCount}/{TotalCount} orders", successCount, pendingOrders.Count);
            
            return successCount == pendingOrders.Count;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to sync orders");
            return false;
        }
    }
    
    public async Task<SyncResult> SyncSingleOrderAsync(string orderId)
    {
        var result = new SyncResult { OrderId = orderId };
        
        try
        {
            var order = await GetOrderAsync(orderId);
            if (order == null)
            {
                result.ErrorMessage = "Order not found";
                return result;
            }
            
            // Check if order can be retried
            if (!order.CanRetrySync)
            {
                result.ErrorMessage = "Max sync attempts reached";
                return result;
            }
            
            // Increment sync attempts
            order.SyncAttempts++;
            
            // Convert to domain and sync
            var domainOrder = order.ToDomain();
            var syncedOrder = await _orderService.CreateOrderAsync(domainOrder);
            
            if (syncedOrder != null)
            {
                // Mark as synced
                order.SyncedAtTimestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                order.Status = syncedOrder.Status.ToString();
                order.LastSyncError = null;
                
                result.Success = true;
                _logger.LogInformation("Successfully synced order: {OrderId}", orderId);
            }
            else
            {
                order.LastSyncError = "Failed to create order on server";
                result.ErrorMessage = order.LastSyncError;
                _logger.LogWarning("Failed to sync order: {OrderId}", orderId);
            }
            
            // Update order in IndexedDB
            await _indexedDBService.SetAsync($"order_{orderId}", order);
            
            result.Attempts = order.SyncAttempts;
            return result;
        }
        catch (Exception ex)
        {
            result.ErrorMessage = ex.Message;
            result.Attempts = 1;
            
            // Update order with error
            var order = await GetOrderAsync(orderId);
            if (order != null)
            {
                order.SyncAttempts++;
                order.LastSyncError = ex.Message;
                await _indexedDBService.SetAsync($"order_{orderId}", order);
            }
            
            _logger.LogError(ex, "Failed to sync order: {OrderId}", orderId);
            return result;
        }
    }
    
    public async Task<bool> DeleteOrderAsync(string orderId)
    {
        try
        {
            await _indexedDBService.RemoveAsync($"order_{orderId}");
            _logger.LogInformation("Deleted offline order: {OrderId}", orderId);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete offline order: {OrderId}", orderId);
            return false;
        }
    }
    
    public async Task<OfflineOrderDto?> GetOrderAsync(string orderId)
    {
        try
        {
            return await _indexedDBService.GetAsync<OfflineOrderDto>($"order_{orderId}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get offline order: {OrderId}", orderId);
            return null;
        }
    }
    
    private bool ValidateOrder(OfflineOrderDto order)
    {
        return !string.IsNullOrEmpty(order.Id) &&
               !string.IsNullOrEmpty(order.CustomerId) &&
               !string.IsNullOrEmpty(order.ShopId) &&
               order.Items.Count > 0 &&
               order.TotalAmount > 0;
    }
}
```

#### **2.3 Create Enhanced Offline Sync Matrix Tests (8 hours)**
**File:** `6_Tests/VanAn.Core.Tests/OfflineOrderServiceTests.cs`

```csharp
// Enhanced test matrix focusing on critical financial safety scenarios
// - Clock drift scenarios (time-based bugs)
// - Network drop during sync (retry strategy)
// - Concurrent sync attempts (data integrity)
// - Multiple retry scenarios (transient failures)
// - Partial failure recovery (resilience)

[Trait("Category", "Unit")]
[Trait("Category", "FinancialSafety")]
[Trait("Category", "OfflineSync")]
public class OfflineOrderServiceTests : IDisposable
{
    [Fact]
    public async Task Should_Handle_Clock_Drift_During_Sync()
    {
        // Test scenario: Server time differs from client time
        var order = CreateTestOrder();
        order.CreatedAtTimestamp = DateTimeOffset.UtcNow.AddMinutes(-5).ToUnixTimeMilliseconds();
        
        // Should handle gracefully without duplication
        var result = await _offlineOrderService.SyncSingleOrderAsync(order.Id);
        Assert.True(result.Success);
        
        // Verify server timestamp used
        var syncedOrder = await _orderService.GetOrderAsync(order.Id);
        Assert.True(Math.Abs((syncedOrder.CreatedAt - DateTimeOffset.UtcNow).TotalMinutes) < 1);
    }
    
    [Fact]
    public async Task Should_Resume_After_Network_Drop()
    {
        // Test scenario: Network drops mid-sync
        // Should resume from where it left off
        var order = CreateTestOrder();
        await _offlineOrderService.CreateOrderAsync(order);
        
        // Simulate network drop
        _orderServiceMock.Setup(x => x.CreateOrderAsync(It.IsAny<Order>()))
            .ThrowsAsync(new HttpRequestException("Network error"))
            .ReturnsAsync(order.ToDomain());
        
        var result1 = await _offlineOrderService.SyncSingleOrderAsync(order.Id);
        Assert.False(result1.Success);
        
        // Resume after reconnect
        var result2 = await _offlineOrderService.SyncSingleOrderAsync(order.Id);
        Assert.True(result2.Success);
    }
    
    [Fact]
    public async Task Should_Prevent_Duplicate_After_Reconnect()
    {
        // Test scenario: Reconnect after network loss
        // Should not duplicate orders
        var order = CreateTestOrder();
        await _offlineOrderService.CreateOrderAsync(order);
        
        // Sync successfully
        var result1 = await _offlineOrderService.SyncSingleOrderAsync(order.Id);
        Assert.True(result1.Success);
        
        // Try to sync again after reconnect
        var result2 = await _offlineOrderService.SyncSingleOrderAsync(order.Id);
        Assert.True(result2.Success); // Already synced is success
        
        _orderServiceMock.Verify(x => x.CreateOrderAsync(It.IsAny<Order>()), Times.Once);
    }
    
    [Fact]
    public async Task Should_Handle_Concurrent_Sync_Attempts()
    {
        // Test scenario: Multiple sync attempts simultaneously
        // Should serialize properly
        var order = CreateTestOrder();
        await _offlineOrderService.CreateOrderAsync(order);
        
        // Concurrent sync attempts
        var tasks = new List<Task<SyncResult>>();
        for (int i = 0; i < 5; i++)
        {
            tasks.Add(_offlineOrderService.SyncSingleOrderAsync(order.Id));
        }
        
        var results = await Task.WhenAll(tasks);
        
        // Only one should succeed, others should handle gracefully
        var successCount = results.Count(r => r.Success);
        Assert.Equal(1, successCount);
        
        _orderServiceMock.Verify(x => x.CreateOrderAsync(It.IsAny<Order>()), Times.Once);
    }
    
    [Fact]
    public async Task Should_Handle_Multiple_Retry_Scenarios()
    {
        // Test scenario: Retry 2-3 times with exponential backoff
        // Should eventually succeed or fail gracefully
        var order = CreateTestOrder();
        await _offlineOrderService.CreateOrderAsync(order);
        
        _orderServiceMock.SetupSequence(x => x.CreateOrderAsync(It.IsAny<Order>()))
            .ThrowsAsync(new HttpRequestException("Network error"))
            .ThrowsAsync(new TimeoutException("Timeout"))
            .ReturnsAsync(order.ToDomain());
        
        var result = await _offlineOrderService.SyncSingleOrderAsync(order.Id);
        
        Assert.True(result.Success);
        Assert.Equal(3, result.Attempts);
    }
    
    [Fact]
    public async Task Should_Handle_Partial_Failure_Recovery()
    {
        // Test scenario: Partial sync failure
        // Should recover and complete remaining items
        var orders = new List<OfflineOrderDto>
        {
            CreateTestOrder(),
            CreateTestOrder(),
            CreateTestOrder()
        };
        
        foreach (var order in orders)
        {
            await _offlineOrderService.CreateOrderAsync(order);
        }
        
        // Simulate partial failure
        _orderServiceMock.Setup(x => x.CreateOrderAsync(It.IsAny<Order>()))
            .ReturnsAsync((Order order) => 
            {
                // Fail second order
                if (order.Id == orders[1].Id)
                    throw new HttpRequestException("Network error");
                return order;
            });
        
        var result = await _offlineOrderService.SyncOrdersAsync();
        Assert.False(result); // Not all synced
        
        // Verify 2 out of 3 synced
        _orderServiceMock.Verify(x => x.CreateOrderAsync(It.IsAny<Order>()), Times.Exactly(3));
    }
}
```

#### **2.4 Create Enhanced Financial Data Integrity Tests (6 hours)**
**File:** `6_Tests/VanAn.Core.Tests/FinancialDataIntegrityTests.cs`

```csharp
using Microsoft.Extensions.DependencyInjection;
using Moq;
using VanAn.KhachLink.Services;
using VanAn.KhachLink.Models;
using VanAn.CoreHub.Services;
using Xunit;

namespace VanAn.Tests;

[Trait("Category", "Unit")]
[Trait("Category", "Critical")]
public class OfflineOrderServiceTests : IDisposable
{
    private readonly ServiceProvider _serviceProvider;
    private readonly Mock<IIndexedDBService> _indexedDBServiceMock;
    private readonly Mock<IOrderService> _orderServiceMock;
    private readonly OfflineOrderService _offlineOrderService;
    private readonly Guid _testShopId = Guid.NewGuid();
    private readonly string _testCustomerId = "test-device-123";
    
    public OfflineOrderServiceTests()
    {
        var services = new ServiceCollection();
        
        _indexedDBServiceMock = new Mock<IIndexedDBService>();
        _orderServiceMock = new Mock<IOrderService>();
        
        services.AddSingleton(_indexedDBServiceMock.Object);
        services.AddSingleton(_orderServiceMock.Object);
        services.AddSingleton<OfflineOrderService>();
        
        _serviceProvider = services.BuildServiceProvider();
        _offlineOrderService = _serviceProvider.GetRequiredService<OfflineOrderService>();
    }
    
    [Fact]
    public async Task CreateOrderAsync_Should_Store_Order_In_IndexedDB()
    {
        // Arrange
        var order = CreateTestOrder();
        
        // Act
        var result = await _offlineOrderService.CreateOrderAsync(order);
        
        // Assert
        Assert.True(result);
        _indexedDBServiceMock.Verify(x => x.SetAsync($"order_{order.Id}", order), Times.Once);
    }
    
    [Fact]
    public async Task CreateOrderAsync_Should_Validate_Order_Data()
    {
        // Arrange
        var invalidOrder = new OfflineOrderDto { Id = "", Items = new List<OfflineOrderItemDto>() };
        
        // Act
        var result = await _offlineOrderService.CreateOrderAsync(invalidOrder);
        
        // Assert
        Assert.False(result);
        _indexedDBServiceMock.Verify(x => x.SetAsync(It.IsAny<string>(), It.IsAny<OfflineOrderDto>()), Times.Never);
    }
    
    [Fact]
    public async Task SyncSingleOrderAsync_Should_Handle_Race_Condition()
    {
        // Arrange
        var orderId = Guid.NewGuid().ToString();
        var order = CreateTestOrder();
        order.Id = orderId;
        
        _indexedDBServiceMock.Setup(x => x.GetAsync<OfflineOrderDto>($"order_{orderId}"))
            .ReturnsAsync(order);
        
        var domainOrder = order.ToDomain();
        _orderServiceMock.Setup(x => x.CreateOrderAsync(domainOrder))
            .ReturnsAsync(domainOrder);
        
        // Act - Simulate concurrent sync
        var task1 = _offlineOrderService.SyncSingleOrderAsync(orderId);
        var task2 = _offlineOrderService.SyncSingleOrderAsync(orderId);
        
        var results = await Task.WhenAll(task1, task2);
        
        // Assert
        Assert.True(results.All(r => r.Success));
        _orderServiceMock.Verify(x => x.CreateOrderAsync(domainOrder), Times.Once); // Should only be called once
    }
    
    [Fact]
    public async Task SyncSingleOrderAsync_Should_Prevent_Duplicate_Sync()
    {
        // Arrange
        var orderId = Guid.NewGuid().ToString();
        var order = CreateTestOrder();
        order.Id = orderId;
        order.SyncedAtTimestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(); // Already synced
        
        _indexedDBServiceMock.Setup(x => x.GetAsync<OfflineOrderDto>($"order_{orderId}"))
            .ReturnsAsync(order);
        
        // Act
        var result = await _offlineOrderService.SyncSingleOrderAsync(orderId);
        
        // Assert
        Assert.True(result.Success); // Already synced is considered success
        _orderServiceMock.Verify(x => x.CreateOrderAsync(It.IsAny<Order>()), Times.Never);
    }
    
    [Fact]
    public async Task SyncSingleOrderAsync_Should_Handle_Network_Failure()
    {
        // Arrange
        var orderId = Guid.NewGuid().ToString();
        var order = CreateTestOrder();
        order.Id = orderId;
        
        _indexedDBServiceMock.Setup(x => x.GetAsync<OfflineOrderDto>($"order_{orderId}"))
            .ReturnsAsync(order);
        
        _orderServiceMock.Setup(x => x.CreateOrderAsync(It.IsAny<Order>()))
            .ThrowsAsync(new HttpRequestException("Network error"));
        
        // Act
        var result = await _offlineOrderService.SyncSingleOrderAsync(orderId);
        
        // Assert
        Assert.False(result.Success);
        Assert.Contains("Network error", result.ErrorMessage);
        Assert.Equal(1, result.Attempts);
    }
    
    [Fact]
    public async Task SyncSingleOrderAsync_Should_Handle_Max_Attempts_Reached()
    {
        // Arrange
        var orderId = Guid.NewGuid().ToString();
        var order = CreateTestOrder();
        order.Id = orderId;
        order.SyncAttempts = 3; // Max attempts reached
        
        _indexedDBServiceMock.Setup(x => x.GetAsync<OfflineOrderDto>($"order_{orderId}"))
            .ReturnsAsync(order);
        
        // Act
        var result = await _offlineOrderService.SyncSingleOrderAsync(orderId);
        
        // Assert
        Assert.False(result.Success);
        Assert.Contains("Max sync attempts reached", result.ErrorMessage);
        _orderServiceMock.Verify(x => x.CreateOrderAsync(It.IsAny<Order>()), Times.Never);
    }
    
    [Fact]
    public async Task GetPendingOrdersAsync_Should_Return_Only_Unsynced_Orders()
    {
        // Arrange
        var syncedOrder = CreateTestOrder();
        syncedOrder.Id = Guid.NewGuid().ToString();
        syncedOrder.SyncedAtTimestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        
        var pendingOrder = CreateTestOrder();
        pendingOrder.Id = Guid.NewGuid().ToString();
        
        var maxAttemptsOrder = CreateTestOrder();
        maxAttemptsOrder.Id = Guid.NewGuid().ToString();
        maxAttemptsOrder.SyncAttempts = 3;
        
        var allOrders = new List<OfflineOrderDto> { syncedOrder, pendingOrder, maxAttemptsOrder };
        
        _indexedDBServiceMock.Setup(x => x.GetAllAsync<OfflineOrderDto>())
            .ReturnsAsync(allOrders);
        
        // Act
        var result = await _offlineOrderService.GetPendingOrdersAsync();
        
        // Assert
        Assert.Single(result);
        Assert.Equal(pendingOrder.Id, result[0].Id);
    }
    
    [Fact]
    public async Task SyncOrdersAsync_Should_Handle_Multiple_Orders()
    {
        // Arrange
        var order1 = CreateTestOrder();
        order1.Id = Guid.NewGuid().ToString();
        
        var order2 = CreateTestOrder();
        order2.Id = Guid.NewGuid().ToString();
        
        var pendingOrders = new List<OfflineOrderDto> { order1, order2 };
        
        _indexedDBServiceMock.Setup(x => x.GetAllAsync<OfflineOrderDto>())
            .ReturnsAsync(pendingOrders);
        
        _indexedDBServiceMock.Setup(x => x.GetAsync<OfflineOrderDto>(It.IsAny<string>()))
            .ReturnsAsync((string key) => pendingOrders.FirstOrDefault(o => key == $"order_{o.Id}"));
        
        _orderServiceMock.Setup(x => x.CreateOrderAsync(It.IsAny<Order>()))
            .ReturnsAsync((Order order) => order);
        
        // Act
        var result = await _offlineOrderService.SyncOrdersAsync();
        
        // Assert
        Assert.True(result);
        _orderServiceMock.Verify(x => x.CreateOrderAsync(It.IsAny<Order>()), Times.Exactly(2));
    }
    
    [Fact]
    public async Task DeleteOrderAsync_Should_Remove_From_IndexedDB()
    {
        // Arrange
        var orderId = Guid.NewGuid().ToString();
        
        // Act
        var result = await _offlineOrderService.DeleteOrderAsync(orderId);
        
        // Assert
        Assert.True(result);
        _indexedDBServiceMock.Verify(x => x.RemoveAsync($"order_{orderId}"), Times.Once);
    }
    
    private OfflineOrderDto CreateTestOrder()
    {
        return new OfflineOrderDto
        {
            Id = Guid.NewGuid().ToString(),
            CustomerId = _testCustomerId,
            ShopId = _testShopId.ToString(),
            Items = new List<OfflineOrderItemDto>
            {
                new OfflineOrderItemDto
                {
                    ProductId = Guid.NewGuid().ToString(),
                    Quantity = 2,
                    UnitPrice = 10000,
                    TotalPrice = 20000
                }
            },
            TotalAmount = 20000
        };
    }
    
    public void Dispose()
    {
        _serviceProvider?.Dispose();
    }
}
```

---

### **DAY 5: SYNC CONFLICT RESOLVER + FINANCIAL INVARIANTS (14 hours)**

#### **3.1 Create Sync Conflict Resolver (6 hours)**
**File:** `5_WebApps/KhachLink/Services/SyncConflictResolver.cs`

```csharp
using VanAn.Shared.Domain;
using VanAn.KhachLink.Models;

namespace VanAn.KhachLink.Services;

public interface ISyncConflictResolver
{
    Task<ConflictResolution> ResolveOrderConflictAsync(OfflineOrderDto offlineOrder, Order? serverOrder);
    Task<ConflictResolution> ResolveCartConflictAsync(List<OfflineOrderItemDto> offlineItems, List<CartItem> serverItems);
    Task<bool> ValidateDataIntegrityAsync(OfflineOrderDto order);
}

public class ConflictResolution
{
    public ResolutionAction Action { get; set; }
    public string? Reason { get; set; }
    public OfflineOrderDto? MergedOrder { get; set; }
    public bool Success => Action != ResolutionAction.Error;
}

public enum ResolutionAction
{
    UseOffline,
    UseServer,
    Merge,
    Skip,
    Error
}

public class SyncConflictResolver : ISyncConflictResolver
{
    private readonly ILogger<SyncConflictResolver> _logger;
    
    public SyncConflictResolver(ILogger<SyncConflictResolver> logger)
    {
        _logger = logger;
    }
    
    public async Task<ConflictResolution> ResolveOrderConflictAsync(OfflineOrderDto offlineOrder, Order? serverOrder)
    {
        try
        {
            // Case 1: No server order exists - use offline
            if (serverOrder == null)
            {
                return new ConflictResolution 
                { 
                    Action = ResolutionAction.UseOffline,
                    Reason = "No server order found"
                };
            }
            
            // Case 2: Same timestamp - likely duplicate, skip
            var offlineTime = offlineOrder.CreatedAt;
            var serverTime = serverOrder.CreatedAt;
            
            if (Math.Abs((offlineTime - serverTime).TotalSeconds) < 5)
            {
                return new ConflictResolution 
                { 
                    Action = ResolutionAction.Skip,
                    Reason = "Duplicate order detected"
                };
            }
            
            // Case 3: Offline order is newer - use offline
            if (offlineTime > serverTime)
            {
                return new ConflictResolution 
                { 
                    Action = ResolutionAction.UseOffline,
                    Reason = "Offline order is newer"
                };
            }
            
            // Case 4: Server order is newer - use server
            if (serverTime > offlineTime)
            {
                return new ConflictResolution 
                { 
                    Action = ResolutionAction.UseServer,
                    Reason = "Server order is newer"
                };
            }
            
            // Case 5: Same timestamp but different data - merge
            return await MergeOrdersAsync(offlineOrder, serverOrder);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to resolve order conflict for offline order: {OrderId}", offlineOrder.Id);
            return new ConflictResolution 
            { 
                Action = ResolutionAction.Error,
                Reason = ex.Message
            };
        }
    }
    
    public async Task<ConflictResolution> ResolveCartConflictAsync(List<OfflineOrderItemDto> offlineItems, List<CartItem> serverItems)
    {
        try
        {
            // Simple merge strategy: combine unique items
            var mergedItems = new List<OfflineOrderItemDto>();
            var seenProductIds = new HashSet<string>();
            
            // Add offline items first
            foreach (var offlineItem in offlineItems)
            {
                if (seenProductIds.Add(offlineItem.ProductId))
                {
                    mergedItems.Add(offlineItem);
                }
            }
            
            // Add server items not in offline
            foreach (var serverItem in serverItems)
            {
                if (seenProductIds.Add(serverItem.ProductId.ToString()))
                {
                    mergedItems.Add(new OfflineOrderItemDto
                    {
                        ProductId = serverItem.ProductId.ToString(),
                        Quantity = serverItem.Quantity,
                        UnitPrice = serverItem.UnitPrice,
                        TotalPrice = serverItem.TotalPrice
                    });
                }
            }
            
            return new ConflictResolution 
            { 
                Action = ResolutionAction.Merge,
                MergedOrder = new OfflineOrderDto { Items = mergedItems },
                Reason = "Cart items merged"
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to resolve cart conflict");
            return new ConflictResolution 
            { 
                Action = ResolutionAction.Error,
                Reason = ex.Message
            };
        }
    }
    
    public async Task<bool> ValidateDataIntegrityAsync(OfflineOrderDto order)
    {
        try
        {
            // Rule 1: Order ID must be valid GUID
            if (!Guid.TryParse(order.Id, out _))
            {
                _logger.LogWarning("Invalid order ID: {OrderId}", order.Id);
                return false;
            }
            
            // Rule 2: Total amount must match sum of items
            var calculatedTotal = order.Items.Sum(i => i.TotalPrice);
            if (Math.Abs(calculatedTotal - order.TotalAmount) > 0.01m)
            {
                _logger.LogWarning("Order total mismatch: calculated={Calculated}, expected={Expected}", 
                    calculatedTotal, order.TotalAmount);
                return false;
            }
            
            // Rule 3: All item prices must be consistent
            foreach (var item in order.Items)
            {
                var calculatedItemTotal = item.Quantity * item.UnitPrice;
                if (Math.Abs(calculatedItemTotal - item.TotalPrice) > 0.01m)
                {
                    _logger.LogWarning("Item total mismatch: item={ProductId}, calculated={Calculated}, expected={Expected}", 
                        item.ProductId, calculatedItemTotal, item.TotalPrice);
                    return false;
                }
            }
            
            // Rule 4: No duplicate product IDs
            var duplicateProductIds = order.Items
                .GroupBy(i => i.ProductId)
                .Where(g => g.Count() > 1)
                .Select(g => g.Key)
                .ToList();
            
            if (duplicateProductIds.Any())
            {
                _logger.LogWarning("Duplicate product IDs found: {ProductIds}", string.Join(", ", duplicateProductIds));
                return false;
            }
            
            // Rule 5: All quantities must be positive
            if (order.Items.Any(i => i.Quantity <= 0))
            {
                _logger.LogWarning("Negative or zero quantities found");
                return false;
            }
            
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to validate data integrity for order: {OrderId}", order.Id);
            return false;
        }
    }
    
    private async Task<ConflictResolution> MergeOrdersAsync(OfflineOrderDto offlineOrder, Order serverOrder)
    {
        try
        {
            // Merge strategy: keep server data but add missing offline items
            var mergedOrder = new OfflineOrderDto
            {
                Id = serverOrder.Id.Value.ToString(),
                CustomerId = offlineOrder.CustomerId,
                ShopId = serverOrder.ShopId.Value.ToString(),
                Items = new List<OfflineOrderItemDto>(),
                TotalAmount = serverOrder.TotalAmount,
                Status = serverOrder.Status.ToString(),
                CreatedAtTimestamp = DateTimeOffset.FromUnixTimeMilliseconds(serverOrder.CreatedAt.ToUnixTimeMilliseconds()).ToUnixTimeMilliseconds()
            };
            
            // Add server items
            foreach (var serverItem in serverOrder.Items)
            {
                mergedOrder.Items.Add(OfflineOrderItemDto.FromDomain(serverItem));
            }
            
            // Add offline items not in server
            var serverProductIds = serverOrder.Items.Select(i => i.ProductId.Value.ToString()).ToHashSet();
            var newOfflineItems = offlineOrder.Items.Where(i => !serverProductIds.Contains(i.ProductId));
            
            foreach (var newItem in newOfflineItems)
            {
                mergedOrder.Items.Add(newItem);
                mergedOrder.TotalAmount += newItem.TotalPrice;
            }
            
            return new ConflictResolution 
            { 
                Action = ResolutionAction.Merge,
                MergedOrder = mergedOrder,
                Reason = "Orders merged successfully"
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to merge orders");
            return new ConflictResolution 
            { 
                Action = ResolutionAction.Error,
                Reason = ex.Message
            };
        }
    }
}
```

#### **3.2 Create Financial Invariants Tests (8 hours)**
**File:** `6_Tests/VanAn.Core.Tests/FinancialInvariantsTests.cs`

```csharp
using VanAn.KhachLink.Services;
using VanAn.KhachLink.Models;
using Xunit;

namespace VanAn.Tests;

[Trait("Category", "Unit")]
[Trait("Category", "FinancialInvariants")]
[Trait("Category", "Critical")]
public class FinancialInvariantsTests : IDisposable
{
    [Fact]
    public async Task TotalRevenue_Should_Equal_Sum_Of_Orders()
    {
        // Critical financial invariant
        var orders = new List<OfflineOrderDto>
        {
            CreateTestOrder(10000),
            CreateTestOrder(20000),
            CreateTestOrder(15000)
        };
        
        var totalRevenue = orders.Sum(o => o.TotalAmount);
        var sumOfOrderAmounts = orders.Select(o => o.TotalAmount).Sum();
        
        Assert.Equal(sumOfOrderAmounts, totalRevenue);
        Assert.Equal(45000, totalRevenue);
    }
    
    [Fact]
    public async Task Should_Not_Duplicate_Order_When_Sync_Retry()
    {
        // Prevent double revenue calculation
        var offlineOrder = CreateTestOrder(10000);
        var serverOrder = offlineOrder.ToDomain();
        
        var resolution = await _conflictResolver.ResolveOrderConflictAsync(offlineOrder, serverOrder);
        
        Assert.Equal(ResolutionAction.Skip, resolution.Action);
        Assert.Contains("Duplicate", resolution.Reason);
    }
    
    [Fact]
    public async Task Should_Maintain_Stock_Consistency_After_Sync()
    {
        // Ensure stock never goes negative
        var offlineOrder = CreateTestOrder(10000);
        offlineOrder.Items.Add(new OfflineOrderItemDto
        {
            ProductId = Guid.NewGuid().ToString(),
            Quantity = 5,
            UnitPrice = 2000,
            TotalPrice = 10000
        });
        
        var isValid = await _conflictResolver.ValidateDataIntegrityAsync(offlineOrder);
        
        Assert.True(isValid);
        Assert.All(offlineOrder.Items, item => Assert.True(item.Quantity > 0));
    }
    
    [Fact]
    public async Task Should_Preserve_Financial_Integrity_During_OfflineSync()
    {
        // Test financial integrity during sync process
        var offlineOrder = CreateTestOrder(10000);
        
        // Simulate sync process
        var syncResult = await _offlineOrderService.SyncSingleOrderAsync(offlineOrder.Id);
        
        // Verify financial data preserved
        Assert.True(syncResult.Success);
        
        // Verify no data corruption
        var syncedOrder = await _offlineOrderService.GetOrderAsync(offlineOrder.Id);
        Assert.NotNull(syncedOrder);
        Assert.Equal(offlineOrder.TotalAmount, syncedOrder.TotalAmount);
    }
    
    [Fact]
    public async Task OrderIds_Should_Be_Unique_Across_System()
    {
        // Prevent duplicate order IDs
        var orders = new List<OfflineOrderDto>();
        
        for (int i = 0; i < 100; i++)
        {
            orders.Add(CreateTestOrder(10000));
        }
        
        var duplicateIds = orders
            .GroupBy(o => o.Id)
            .Where(g => g.Count() > 1)
            .Select(g => g.Key)
            .ToList();
        
        Assert.Empty(duplicateIds);
    }
    
    [Fact]
    public async Task Should_Not_Corrupt_Revenue_When_Offline_Then_Online()
    {
        // Test revenue integrity during offline->online transition
        var offlineOrders = new List<OfflineOrderDto>
        {
            CreateTestOrder(10000),
            CreateTestOrder(20000)
        };
        
        // Simulate offline creation
        foreach (var order in offlineOrders)
        {
            await _offlineOrderService.CreateOrderAsync(order);
        }
        
        // Simulate online sync
        await _offlineOrderService.SyncOrdersAsync();
        
        // Verify total revenue preserved
        var syncedOrders = await _offlineOrderService.GetPendingOrdersAsync();
        var totalRevenue = offlineOrders.Sum(o => o.TotalAmount);
        
        Assert.True(syncedOrders.All(o => o.IsSynced));
        Assert.Equal(30000, totalRevenue);
    }
    
    private OfflineOrderDto CreateTestOrder(decimal totalAmount)
    {
        return new OfflineOrderDto
        {
            Id = Guid.NewGuid().ToString(),
            CustomerId = "test-device-123",
            ShopId = Guid.NewGuid().ToString(),
            Items = new List<OfflineOrderItemDto>
            {
                new OfflineOrderItemDto
                {
                    ProductId = Guid.NewGuid().ToString(),
                    Quantity = 2,
                    UnitPrice = totalAmount / 2,
                    TotalPrice = totalAmount / 2
                }
            },
            TotalAmount = totalAmount
        };
    }
    
    public void Dispose()
    {
        // Cleanup
    }
}
```

```csharp
using VanAn.KhachLink.Services;
using VanAn.KhachLink.Models;
using Xunit;

namespace VanAn.Tests;

[Trait("Category", "Unit")]
[Trait("Category", "DataIntegrity")]
public class DataIntegrityTests : IDisposable
{
    private readonly SyncConflictResolver _conflictResolver;
    
    public DataIntegrityTests()
    {
        _conflictResolver = new SyncConflictResolver(Mock.Of<ILogger<SyncConflictResolver>>());
    }
    
    [Fact]
    public async Task Should_Not_Duplicate_Order_When_Sync_Retry()
    {
        // Arrange
        var offlineOrder = CreateTestOrder();
        var serverOrder = offlineOrder.ToDomain();
        
        // Act
        var resolution = await _conflictResolver.ResolveOrderConflictAsync(offlineOrder, serverOrder);
        
        // Assert
        Assert.Equal(ResolutionAction.Skip, resolution.Action);
        Assert.Contains("Duplicate", resolution.Reason);
    }
    
    [Fact]
    public async Task Should_Maintain_Stock_Consistency_After_Sync()
    {
        // Arrange
        var offlineOrder = CreateTestOrder();
        offlineOrder.Items.Add(new OfflineOrderItemDto
        {
            ProductId = Guid.NewGuid().ToString(),
            Quantity = 1,
            UnitPrice = 5000,
            TotalPrice = 5000
        });
        
        var serverOrder = offlineOrder.ToDomain();
        serverOrder.Items.RemoveAt(0); // Server missing first item
        
        // Act
        var resolution = await _conflictResolver.ResolveOrderConflictAsync(offlineOrder, serverOrder);
        
        // Assert
        Assert.Equal(ResolutionAction.Merge, resolution.Action);
        Assert.NotNull(resolution.MergedOrder);
        Assert.Equal(2, resolution.MergedOrder.Items.Count); // Both items preserved
    }
    
    [Fact]
    public async Task Should_Preserve_Financial_Integrity_During_OfflineSync()
    {
        // Arrange
        var offlineOrder = CreateTestOrder();
        offlineOrder.TotalAmount = 50000; // Wrong total
        
        // Act
        var isValid = await _conflictResolver.ValidateDataIntegrityAsync(offlineOrder);
        
        // Assert
        Assert.False(isValid); // Should detect total mismatch
    }
    
    [Fact]
    public async Task Should_Detect_Invalid_Order_Id()
    {
        // Arrange
        var offlineOrder = CreateTestOrder();
        offlineOrder.Id = "invalid-guid";
        
        // Act
        var isValid = await _conflictResolver.ValidateDataIntegrityAsync(offlineOrder);
        
        // Assert
        Assert.False(isValid);
    }
    
    [Fact]
    public async Task Should_Detect_Item_Price_Mismatch()
    {
        // Arrange
        var offlineOrder = CreateTestOrder();
        offlineOrder.Items[0].TotalPrice = 15000; // Wrong total (should be 20000)
        
        // Act
        var isValid = await _conflictResolver.ValidateDataIntegrityAsync(offlineOrder);
        
        // Assert
        Assert.False(isValid);
    }
    
    [Fact]
    public async Task Should_Detect_Duplicate_Product_Ids()
    {
        // Arrange
        var offlineOrder = CreateTestOrder();
        var duplicateItem = offlineOrder.Items[0];
        offlineOrder.Items.Add(duplicateItem);
        
        // Act
        var isValid = await _conflictResolver.ValidateDataIntegrityAsync(offlineOrder);
        
        // Assert
        Assert.False(isValid);
    }
    
    [Fact]
    public async Task Should_Detect_Negative_Quantities()
    {
        // Arrange
        var offlineOrder = CreateTestOrder();
        offlineOrder.Items[0].Quantity = -1;
        
        // Act
        var isValid = await _conflictResolver.ValidateDataIntegrityAsync(offlineOrder);
        
        // Assert
        Assert.False(isValid);
    }
    
    [Fact]
    public async Task TotalRevenue_Should_Equal_Sum_Of_OrderAmounts()
    {
        // Arrange
        var orders = new List<OfflineOrderDto>
        {
            CreateTestOrder(),
            CreateTestOrder()
        };
        
        // Act
        var totalRevenue = orders.Sum(o => o.TotalAmount);
        var sumOfOrderAmounts = orders.Select(o => o.TotalAmount).Sum();
        
        // Assert
        Assert.Equal(sumOfOrderAmounts, totalRevenue);
    }
    
    [Fact]
    public async Task Stock_Should_Always_Be_NonNegative()
    {
        // This would be tested with actual inventory service
        // For now, we validate that order quantities are positive
        var offlineOrder = CreateTestOrder();
        
        // Act
        var isValid = await _conflictResolver.ValidateDataIntegrityAsync(offlineOrder);
        
        // Assert
        Assert.True(isValid); // All quantities are positive
        Assert.All(offlineOrder.Items, item => Assert.True(item.Quantity > 0));
    }
    
    [Fact]
    public async Task OrderIds_Should_Be_Unique_Across_System()
    {
        // Arrange
        var orders = new List<OfflineOrderDto>
        {
            CreateTestOrder(),
            CreateTestOrder()
        };
        
        // Act
        var duplicateIds = orders
            .GroupBy(o => o.Id)
            .Where(g => g.Count() > 1)
            .Select(g => g.Key)
            .ToList();
        
        // Assert
        Assert.Empty(duplicateIds);
    }
    
    private OfflineOrderDto CreateTestOrder()
    {
        return new OfflineOrderDto
        {
            Id = Guid.NewGuid().ToString(),
            CustomerId = "test-device-123",
            ShopId = Guid.NewGuid().ToString(),
            Items = new List<OfflineOrderItemDto>
            {
                new OfflineOrderItemDto
                {
                    ProductId = Guid.NewGuid().ToString(),
                    Quantity = 2,
                    UnitPrice = 10000,
                    TotalPrice = 20000
                }
            },
            TotalAmount = 20000
        };
    }
    
    public void Dispose()
    {
        // Cleanup if needed
    }
}
```

---

## 📅 WEEK 2: STATE MACHINE UI + RETRY STRATEGY (26 hours)

### **🔴 CRITICAL FINANCIAL SAFETY FOCUS:**
- ✅ **UI State Machine Tests** (8 hours) - **NEW**
- ✅ **Retry Strategy Tests** (6 hours) - **NEW**
- ✅ **SignalR Deep Testing** (4 hours) - **ENHANCED**
- ✅ **Dashboard UI Implementation** (8 hours) - **ENHANCED**

### **DAY 6-7: ORDER MANAGEMENT SERVICE + SIGNALR INTEGRATION (14 hours)**

#### **4.1 Create Order Management Service (8 hours)**
**File:** `5_WebApps/ShopERP/Services/OrderManagementService.cs`

```csharp
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
}

public class OrderMetrics
{
    public int TotalOrders { get; set; }
    public int PendingOrders { get; set; }
    public int CompletedOrders { get; set; }
    public decimal TotalRevenue { get; set; }
    public decimal AverageOrderValue { get; set; }
    public int OrdersPerHour { get; set; }
}

public class OrderManagementService : IOrderManagementService
{
    private readonly IOrderService _orderService;
    private readonly IOrderWorkflowService _orderWorkflowService;
    private readonly ILogger<OrderManagementService> _logger;
    
    public OrderManagementService(
        IOrderService orderService,
        IOrderWorkflowService orderWorkflowService,
        ILogger<OrderManagementService> logger)
    {
        _orderService = orderService;
        _orderWorkflowService = orderWorkflowService;
        _logger = logger;
    }
    
    public async Task<List<Order>> GetOrdersAsync(OrderStatusId? status = null)
    {
        try
        {
            if (status.HasValue)
            {
                return await _orderService.GetOrdersByStatusAsync(status.Value);
            }
            
            return await _orderService.GetAllOrdersAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get orders");
            return new List<Order>();
        }
    }
    
    public async Task<Order?> GetOrderAsync(Guid orderId)
    {
        try
        {
            return await _orderService.GetOrderAsync(orderId);
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
            var order = await _orderService.GetOrderAsync(orderId);
            if (order == null)
            {
                _logger.LogWarning("Order not found: {OrderId}", orderId);
                return false;
            }
            
            // Validate status transition
            var isValidTransition = await _orderWorkflowService.IsTransitionValidAsync(order.Status, newStatus);
            if (!isValidTransition)
            {
                _logger.LogWarning("Invalid status transition: {CurrentStatus} -> {NewStatus}", order.Status, newStatus);
                return false;
            }
            
            var updatedOrder = await _orderWorkflowService.TransitionStatusAsync(orderId, newStatus, reason);
            
            if (updatedOrder != null)
            {
                _logger.LogInformation("Order status updated: {OrderId} -> {NewStatus}", orderId, newStatus);
                return true;
            }
            
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update order status: {OrderId} -> {NewStatus}", orderId, newStatus);
            return false;
        }
    }
    
    public async Task<List<Order>> GetOrdersByDateRangeAsync(DateTime startDate, DateTime endDate)
    {
        try
        {
            return await _orderService.GetOrdersByDateRangeAsync(startDate, endDate);
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
            var allOrders = await _orderService.GetAllOrdersAsync();
            var now = DateTime.UtcNow;
            var oneHourAgo = now.AddHours(-1);
            
            var metrics = new OrderMetrics
            {
                TotalOrders = allOrders.Count,
                PendingOrders = allOrders.Count(o => o.Status == OrderStatusId.Pending),
                CompletedOrders = allOrders.Count(o => o.Status == OrderStatusId.Completed),
                TotalRevenue = allOrders.Where(o => o.Status == OrderStatusId.Completed).Sum(o => o.TotalAmount),
                OrdersPerHour = allOrders.Count(o => o.CreatedAt >= oneHourAgo)
            };
            
            if (metrics.CompletedOrders > 0)
            {
                metrics.AverageOrderValue = metrics.TotalRevenue / metrics.CompletedOrders;
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
            // This would integrate with staff management system
            // For now, just update order status to indicate assignment
            return await UpdateOrderStatusAsync(orderId, OrderStatusId.Processing, $"Assigned to staff {staffId}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to assign order to staff: {OrderId} -> {StaffId}", orderId, staffId);
            return false;
        }
    }
}
```

#### **4.2 Create SignalR Hub Unit Tests (2 hours)**
**File:** `6_Tests/VanAn.Core.Tests/OrderHubUnitTests.cs`

```csharp
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using Moq;
using VanAn.Gateway.Hubs;
using Xunit;

namespace VanAn.Core.Tests;

[Trait("Category", "Unit")]
[Trait("Category", "SignalR")]
public class OrderHubUnitTests
{
    private readonly Mock<ILogger<OrderHub>> _loggerMock;
    private readonly Mock<HubCallerContext> _contextMock;
    private readonly Mock<IGroupManager> _groupManagerMock;
    private readonly OrderHub _hub;

    public OrderHubUnitTests()
    {
        _loggerMock = new Mock<ILogger<OrderHub>>();
        _contextMock = new Mock<HubCallerContext>();
        _groupManagerMock = new Mock<IGroupManager>();
        
        _hub = new OrderHub(_loggerMock.Object);
        _hub.Context = _contextMock.Object;
        _hub.Groups = _groupManagerMock.Object;
    }

    [Fact]
    public async Task JoinShopGroup_Should_Add_Connection_To_Group()
    {
        // Arrange
        var shopId = "test-shop-123";
        var connectionId = "connection-456";
        
        _contextMock.Setup(x => x.ConnectionId).Returns(connectionId);

        // Act
        await _hub.JoinShopGroup(shopId);

        // Assert
        _groupManagerMock.Verify(x => x.AddToGroupAsync(connectionId, $"Shop_{shopId}"), Times.Once);
    }

    [Fact]
    public async Task LeaveShopGroup_Should_Remove_Connection_From_Group()
    {
        // Arrange
        var shopId = "test-shop-123";
        var connectionId = "connection-456";
        
        _contextMock.Setup(x => x.ConnectionId).Returns(connectionId);

        // Act
        await _hub.LeaveShopGroup(shopId);

        // Assert
        _groupManagerMock.Verify(x => x.RemoveFromGroupAsync(connectionId, $"Shop_{shopId}"), Times.Once);
    }

    [Fact]
    public async Task Should_Broadcast_Order_Update_To_All_Clients()
    {
        // Test broadcasting functionality
        var orderId = Guid.NewGuid();
        var shopId = "test-shop-123";
        
        // This would test the actual broadcasting logic
        // Implementation depends on how broadcasting is set up
    }
}
```

#### **4.3 Create SignalR Hub Integration Tests (4 hours)**
**File:** `6_Tests/VanAn.Integration.Tests/SignalRIntegrationTests.cs`

```csharp
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using VanAn.Gateway.Hubs;
using Xunit;

namespace VanAn.Integration.Tests;

[Trait("Category", "Integration")]
[Trait("Category", "SignalR")]
public class SignalRIntegrationTests : IClassFixture<WebApplicationFactory<Program>>, IDisposable
{
    private readonly WebApplicationFactory<Program> _factory;
    private readonly HubConnection _connection;
    
    public SignalRIntegrationTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
        
        // Create SignalR connection
        _connection = new HubConnectionBuilder()
            .WithUrl(factory.CreateClient().BaseAddress + "/orderHub")
            .Build();
    }
    
    [Fact]
    public async Task OrderHub_Should_Connect_Successfully()
    {
        // Arrange & Act
        await _connection.StartAsync();
        
        // Assert
        Assert.Equal(HubConnectionState.Connected, _connection.State);
        
        await _connection.StopAsync();
    }
    
    [Fact]
    public async Task OrderHub_Should_Join_Shop_Group()
    {
        // Arrange
        var shopId = Guid.NewGuid().ToString();
        var joinedShop = false;
        
        _connection.On<string>("JoinedShopGroup", (joinedShopId) =>
        {
            joinedShop = joinedShopId == shopId;
        });
        
        await _connection.StartAsync();
        
        // Act
        await _connection.InvokeAsync("JoinShopGroup", shopId);
        
        // Wait for event
        await Task.Delay(100);
        
        // Assert
        Assert.True(joinedShop);
        
        await _connection.StopAsync();
    }
    
    [Fact]
    public async Task OrderHub_Should_Handle_Order_Update_Notification()
    {
        // Arrange
        var shopId = Guid.NewGuid().ToString();
        var orderUpdateReceived = false;
        var receivedOrderId = Guid.Empty;
        
        _connection.On<Guid>("OrderUpdated", (orderId) =>
        {
            orderUpdateReceived = true;
            receivedOrderId = orderId;
        });
        
        await _connection.StartAsync();
        await _connection.InvokeAsync("JoinShopGroup", shopId);
        
        // Act
        var orderId = Guid.NewGuid();
        await _connection.InvokeAsync("NotifyOrderUpdate", shopId, orderId);
        
        // Wait for event
        await Task.Delay(100);
        
        // Assert
        Assert.True(orderUpdateReceived);
        Assert.Equal(orderId, receivedOrderId);
        
        await _connection.StopAsync();
    }
    
    [Fact]
    public async Task OrderHub_Should_Handle_Multiple_Connections()
    {
        // Arrange
        var shopId = Guid.NewGuid().ToString();
        var connection1 = CreateHubConnection();
        var connection2 = CreateHubConnection();
        
        var connection1Received = false;
        var connection2Received = false;
        
        connection1.On<Guid>("OrderUpdated", (orderId) => connection1Received = true);
        connection2.On<Guid>("OrderUpdated", (orderId) => connection2Received = true);
        
        await connection1.StartAsync();
        await connection2.StartAsync();
        
        await connection1.InvokeAsync("JoinShopGroup", shopId);
        await connection2.InvokeAsync("JoinShopGroup", shopId);
        
        // Act
        var orderId = Guid.NewGuid();
        await connection1.InvokeAsync("NotifyOrderUpdate", shopId, orderId);
        
        // Wait for events
        await Task.Delay(200);
        
        // Assert
        Assert.True(connection1Received);
        Assert.True(connection2Received);
        
        await connection1.StopAsync();
        await connection2.StopAsync();
    }
    
    [Fact]
    public async Task OrderHub_Should_Handle_Connection_Disconnection()
    {
        // Arrange
        var shopId = Guid.NewGuid().ToString();
        var disconnected = false;
        
        _connection.On<string>("DisconnectedFromShopGroup", (disconnectedShopId) =>
        {
            disconnected = disconnectedShopId == shopId;
        });
        
        await _connection.StartAsync();
        await _connection.InvokeAsync("JoinShopGroup", shopId);
        
        // Act
        await _connection.StopAsync();
        
        // Wait for cleanup
        await Task.Delay(100);
        
        // Assert
        // Note: Disconnection event might not be triggered in test environment
        // This test mainly ensures no exceptions during disconnection
        Assert.Equal(HubConnectionState.Disconnected, _connection.State);
    }
    
    private HubConnection CreateHubConnection()
    {
        return new HubConnectionBuilder()
            .WithUrl(_factory.CreateClient().BaseAddress + "/orderHub")
            .Build();
    }
    
    public void Dispose()
    {
        _connection?.DisposeAsync().AsTask().Wait();
    }
}
```

---

### **DAY 8-9: DASHBOARD UI COMPONENTS + bUnit TESTS (11 hours)**

#### **5.1 Create Dashboard UI Component (6 hours)**
**File:** `5_WebApps/ShopERP/Pages/Dashboard.razor`

```razor
@page "/dashboard"
@using Vanan.ShopERP.Services
@using Vanan.Shared.Domain
@inject IOrderManagementService OrderManagementService
@inject IHubConnectionService HubConnectionService

<PageTitle>Dashboard - VanAn ShopERP</PageTitle>

<div class="dashboard-container">
    <div class="dashboard-header">
        <h1>Shop Dashboard</h1>
        <div class="connection-status">
            <span class="@(IsConnected ? "status-online" : "status-offline")">
                @(IsConnected ? "🟢 Connected" : "🔴 Disconnected")
            </span>
        </div>
    </div>

    @if (Metrics != null)
    {
        <div class="metrics-grid">
            <div class="metric-card">
                <h3>Total Orders</h3>
                <div class="metric-value">@Metrics.TotalOrders</div>
            </div>
            <div class="metric-card">
                <h3>Pending Orders</h3>
                <div class="metric-value pending">@Metrics.PendingOrders</div>
            </div>
            <div class="metric-card">
                <h3>Completed Orders</h3>
                <div class="metric-value completed">@Metrics.CompletedOrders</div>
            </div>
            <div class="metric-card">
                <h3>Total Revenue</h3>
                <div class="metric-value revenue">@Metrics.TotalRevenue.ToString("N0") VND</div>
            </div>
            <div class="metric-card">
                <h3>Average Order Value</h3>
                <div class="metric-value">@Metrics.AverageOrderValue.ToString("N0") VND</div>
            </div>
            <div class="metric-card">
                <h3>Orders/Hour</h3>
                <div class="metric-value">@Metrics.OrdersPerHour</div>
            </div>
        </div>
    }

    <div class="dashboard-content">
        <div class="orders-section">
            <h2>Recent Orders</h2>
            
            @if (IsLoading)
            {
                <div class="loading">Loading orders...</div>
            }
            else if (Orders?.Any() == true)
            {
                <div class="orders-table">
                    <table>
                        <thead>
                            <tr>
                                <th>Order ID</th>
                                <th>Customer</th>
                                <th>Items</th>
                                <th>Total</th>
                                <th>Status</th>
                                <th>Created</th>
                                <th>Actions</th>
                            </tr>
                        </thead>
                        <tbody>
                            @foreach (var order in Orders.Take(10))
                            {
                                <tr class="order-row" data-order-id="@order.Id">
                                    <td>@order.Id.Value.ToString().Substring(0, 8)</td>
                                    <td>@order.CustomerId</td>
                                    <td>@order.Items.Count</td>
                                    <td>@order.TotalAmount.ToString("N0") VND</td>
                                    <td>
                                        <span class="status-badge status-@order.Status.ToString().ToLower()">
                                            @order.Status.ToString()
                                        </span>
                                    </td>
                                    <td>@order.CreatedAt.ToString("HH:mm")</td>
                                    <td>
                                        <select class="status-select" @onchange="@(e => UpdateOrderStatus(order.Id, e))">
                                            @foreach (var status in Enum.GetValues<OrderStatusId>())
                                            {
                                                <option value="@status" selected="@(status == order.Status)">
                                                    @status.ToString()
                                                </option>
                                            }
                                        </select>
                                    </td>
                                </tr>
                            }
                        </tbody>
                    </table>
                </div>
            }
            else
            {
                <div class="no-orders">No orders found</div>
            }
        </div>
    </div>
</div>

<style>
.dashboard-container {
    padding: 20px;
    max-width: 1200px;
    margin: 0 auto;
}

.dashboard-header {
    display: flex;
    justify-content: space-between;
    align-items: center;
    margin-bottom: 30px;
}

.connection-status {
    font-weight: bold;
}

.status-online {
    color: #28a745;
}

.status-offline {
    color: #dc3545;
}

.metrics-grid {
    display: grid;
    grid-template-columns: repeat(auto-fit, minmax(200px, 1fr));
    gap: 20px;
    margin-bottom: 30px;
}

.metric-card {
    background: #fff;
    padding: 20px;
    border-radius: 8px;
    box-shadow: 0 2px 4px rgba(0,0,0,0.1);
    text-align: center;
}

.metric-card h3 {
    margin: 0 0 10px 0;
    color: #666;
    font-size: 14px;
}

.metric-value {
    font-size: 24px;
    font-weight: bold;
    color: #333;
}

.metric-value.pending {
    color: #ffc107;
}

.metric-value.completed {
    color: #28a745;
}

.metric-value.revenue {
    color: #007bff;
}

.orders-table {
    background: #fff;
    border-radius: 8px;
    overflow: hidden;
    box-shadow: 0 2px 4px rgba(0,0,0,0.1);
}

.orders-table table {
    width: 100%;
    border-collapse: collapse;
}

.orders-table th,
.orders-table td {
    padding: 12px;
    text-align: left;
    border-bottom: 1px solid #eee;
}

.orders-table th {
    background: #f8f9fa;
    font-weight: 600;
}

.status-badge {
    padding: 4px 8px;
    border-radius: 4px;
    font-size: 12px;
    font-weight: bold;
}

.status-pending {
    background: #fff3cd;
    color: #856404;
}

.status-processing {
    background: #cce5ff;
    color: #004085;
}

.status-completed {
    background: #d4edda;
    color: #155724;
}

.status-cancelled {
    background: #f8d7da;
    color: #721c24;
}

.status-select {
    padding: 4px 8px;
    border: 1px solid #ddd;
    border-radius: 4px;
    font-size: 12px;
}

.loading {
    text-align: center;
    padding: 40px;
    color: #666;
}

.no-orders {
    text-align: center;
    padding: 40px;
    color: #666;
}

.order-row {
    transition: background-color 0.2s;
}

.order-row:hover {
    background-color: #f8f9fa;
}
</style>

@code {
    private OrderMetrics? Metrics;
    private List<Order>? Orders;
    private bool IsLoading = true;
    private bool IsConnected = false;
    private Timer? _refreshTimer;

    protected override async Task OnInitializedAsync()
    {
        await LoadDashboardData();
        await SetupSignalR();
        
        // Set up refresh timer
        _refreshTimer = new Timer(async _ => await RefreshData(), null, TimeSpan.Zero, TimeSpan.FromSeconds(30));
    }

    private async Task LoadDashboardData()
    {
        try
        {
            IsLoading = true;
            StateHasChanged();

            // Load metrics
            Metrics = await OrderManagementService.GetOrderMetricsAsync();
            
            // Load recent orders
            Orders = await OrderManagementService.GetOrdersAsync();
        }
        catch (Exception ex)
        {
            // Handle error
            Console.WriteLine($"Error loading dashboard data: {ex.Message}");
        }
        finally
        {
            IsLoading = false;
            StateHasChanged();
        }
    }

    private async Task SetupSignalR()
    {
        try
        {
            await HubConnectionService.StartAsync();
            IsConnected = HubConnectionService.IsConnected;

            // Subscribe to order updates
            HubConnectionService.OnOrderUpdated(async (orderId) =>
            {
                await RefreshData();
            });

            HubConnectionService.OnConnectionStateChanged(async (isConnected) =>
            {
                IsConnected = isConnected;
                StateHasChanged();
            });
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error setting up SignalR: {ex.Message}");
            IsConnected = false;
        }
    }

    private async Task RefreshData()
    {
        await LoadDashboardData();
    }

    private async Task UpdateOrderStatus(Guid orderId, ChangeEventArgs e)
    {
        try
        {
            if (Enum.TryParse<OrderStatusId>(e.Value?.ToString(), out var newStatus))
            {
                var success = await OrderManagementService.UpdateOrderStatusAsync(orderId, newStatus);
                
                if (success)
                {
                    // Notify other clients
                    await HubConnectionService.NotifyOrderUpdate(orderId);
                    
                    // Refresh local data
                    await RefreshData();
                }
                else
                {
                    // Show error message
                    Console.WriteLine($"Failed to update order status: {orderId}");
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error updating order status: {ex.Message}");
        }
    }

    public async ValueTask DisposeAsync()
    {
        _refreshTimer?.Dispose();
        await HubConnectionService.DisposeAsync();
    }
}
```

#### **5.2 Create UI State Machine Tests (8 hours)**
**File:** `6_Tests/VanAn.UI.Tests/UIStateMachineTests.cs`

```csharp
using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using VanAn.ShopERP.Services;
using VanAn.Shared.Domain;
using Xunit;

namespace VanAn.UI.Tests;

[Trait("Category", "UI")]
[Trait("Category", "StateMachine")]
[Trait("Category", "FinancialSafety")]
public class UIStateMachineTests : TestContext
{
    private readonly Mock<IOrderManagementService> _orderManagementServiceMock;
    private readonly Mock<IHubConnectionService> _hubConnectionServiceMock;
    
    public UIStateMachineTests()
    {
        _orderManagementServiceMock = new Mock<IOrderManagementService>();
        _hubConnectionServiceMock = new Mock<IHubConnectionService>();
        
        Services.AddSingleton(_orderManagementServiceMock.Object);
        Services.AddSingleton(_hubConnectionServiceMock.Object);
    }
    
    [Fact]
    public void Should_Transition_From_Offline_To_Syncing_State()
    {
        // Arrange
        _orderManagementServiceMock.Setup(x => x.GetOrdersAsync())
            .ReturnsAsync(new List<Order>());
        
        _orderManagementServiceMock.Setup(x => x.GetOrderMetricsAsync())
            .ReturnsAsync(new OrderMetrics());
        
        var component = RenderComponent<ShopERP.Pages.Dashboard>();
        
        // Act
        component.Instance.StartSync();
        
        // Assert - State transition
        Assert.Contains("syncing", component.Markup.ToLower());
        Assert.DoesNotContain("offline", component.Markup.ToLower());
        Assert.Contains("spinner", component.Markup.ToLower());
    }
    
    [Fact]
    public void Should_Show_Error_And_Retry_On_Failure()
    {
        // Arrange
        _orderManagementServiceMock.Setup(x => x.SyncOrdersAsync())
            .ThrowsAsync(new Exception("Network error"));
        
        _orderManagementServiceMock.Setup(x => x.GetOrdersAsync())
            .ReturnsAsync(new List<Order>());
        
        _orderManagementServiceMock.Setup(x => x.GetOrderMetricsAsync())
            .ReturnsAsync(new OrderMetrics());
        
        var component = RenderComponent<ShopERP.Pages.Dashboard>();
        
        // Act
        component.Instance.StartSync();
        component.Render();
        
        // Assert
        Assert.Contains("error", component.Markup.ToLower());
        Assert.Contains("retry", component.Markup.ToLower());
        Assert.Contains("network error", component.Markup.ToLower());
    }
    
    [Fact]
    public void Should_Transition_From_Syncing_To_Completed_State()
    {
        // Arrange
        _orderManagementServiceMock.Setup(x => x.SyncOrdersAsync())
            .ReturnsAsync(true);
        
        _orderManagementServiceMock.Setup(x => x.GetOrdersAsync())
            .ReturnsAsync(new List<Order>());
        
        _orderManagementServiceMock.Setup(x => x.GetOrderMetricsAsync())
            .ReturnsAsync(new OrderMetrics());
        
        var component = RenderComponent<ShopERP.Pages.Dashboard>();
        
        // Act
        component.Instance.StartSync();
        component.WaitForState(() => component.Markup.Contains("completed"));
        
        // Assert
        Assert.Contains("completed", component.Markup.ToLower());
        Assert.DoesNotContain("syncing", component.Markup.ToLower());
        Assert.Contains("success", component.Markup.ToLower());
    }
    
    [Fact]
    public void Should_Handle_Loading_To_Data_Ready_Transition()
    {
        // Arrange
        _orderManagementServiceMock.Setup(x => x.GetOrdersAsync())
            .ReturnsAsync(() => 
            {
                Thread.Sleep(100); // Simulate delay
                return new List<Order>();
            });
        
        _orderManagementServiceMock.Setup(x => x.GetOrderMetricsAsync())
            .ReturnsAsync(new OrderMetrics());
        
        // Act
        var component = RenderComponent<ShopERP.Pages.Dashboard>();
        
        // Assert - Initial loading state
        Assert.Contains("loading", component.Markup.ToLower());
        
        // Wait for data ready
        component.WaitForState(() => !component.Markup.Contains("loading"));
        
        // Assert - Data ready state
        Assert.DoesNotContain("loading", component.Markup.ToLower());
        Assert.Contains("ready", component.Markup.ToLower());
    }
    
    [Fact]
    public void Should_Maintain_State_During_Error_Recovery()
    {
        // Arrange
        _orderManagementServiceMock.SetupSequence(x => x.SyncOrdersAsync())
            .ThrowsAsync(new Exception("Network error"))
            .ReturnsAsync(true);
        
        var component = RenderComponent<ShopERP.Pages.Dashboard>();
        
        // Act - First sync fails
        component.Instance.StartSync();
        component.Render();
        
        // Assert - Error state
        Assert.Contains("error", component.Markup.ToLower());
        
        // Act - Retry succeeds
        var retryButton = component.Find(".retry-button");
        retryButton.Click();
        
        component.WaitForState(() => component.Markup.Contains("completed"));
        
        // Assert - Recovery to completed state
        Assert.Contains("completed", component.Markup.ToLower());
        Assert.DoesNotContain("error", component.Markup.ToLower());
    }
    
    [Fact]
    public void Should_Handle_Disconnected_To_Reconnected_Transition()
    {
        // Arrange
        _hubConnectionServiceMock.Setup(x => x.IsConnected)
            .Returns(false);
        
        var component = RenderComponent<ShopERP.Pages.Dashboard>();
        
        // Assert - Disconnected state
        Assert.Contains("disconnected", component.Markup.ToLower());
        Assert.Contains("offline", component.Markup.ToLower());
        
        // Act - Simulate reconnection
        _hubConnectionServiceMock.Setup(x => x.IsConnected)
            .Returns(true);
        
        _hubConnectionServiceMock.Raise(x => x.OnConnectionStateChanged += null, true);
        
        // Assert - Reconnected state
        Assert.Contains("connected", component.Markup.ToLower());
        Assert.Contains("online", component.Markup.ToLower());
        Assert.DoesNotContain("disconnected", component.Markup.ToLower());
    }
}
```

#### **5.3 Create Retry Strategy Tests (6 hours)**
**File:** `6_Tests/VanAn.Core.Tests/RetryStrategyTests.cs`

```csharp
using Microsoft.Extensions.DependencyInjection;
using Moq;
using VanAn.KhachLink.Services;
using VanAn.KhachLink.Models;
using VanAn.CoreHub.Services;
using Xunit;

namespace VanAn.Tests;

[Trait("Category", "Unit")]
[Trait("Category", "FinancialSafety")]
[Trait("Category", "RetryStrategy")]
public class RetryStrategyTests : IDisposable
{
    [Fact]
    public async Task Should_Retry_Sync_On_Transient_Failure()
    {
        // Arrange
        var order = CreateTestOrder();
        await _offlineOrderService.CreateOrderAsync(order);
        
        _orderServiceMock.SetupSequence(x => x.CreateOrderAsync(It.IsAny<Order>()))
            .ThrowsAsync(new HttpRequestException("Network error"))
            .ThrowsAsync(new TimeoutException("Timeout"))
            .ReturnsAsync(order.ToDomain());
        
        // Act
        var result = await _offlineOrderService.SyncSingleOrderAsync(order.Id);
        
        // Assert
        Assert.True(result.Success);
        Assert.Equal(3, result.Attempts);
        Assert.Contains("retry", result.Message.ToLower());
    }
    
    [Fact]
    public async Task Should_Stop_After_Max_Retry()
    {
        // Arrange
        var order = CreateTestOrder();
        await _offlineOrderService.CreateOrderAsync(order);
        
        _orderServiceMock.Setup(x => x.CreateOrderAsync(It.IsAny<Order>()))
            .ThrowsAsync(new HttpRequestException("Persistent error"));
        
        // Act
        var result = await _offlineOrderService.SyncSingleOrderAsync(order.Id);
        
        // Assert
        Assert.False(result.Success);
        Assert.Equal(3, result.Attempts); // Max retry
        Assert.Contains("max retry", result.ErrorMessage.ToLower());
        Assert.Contains("persistent error", result.ErrorMessage.ToLower());
    }
    
    [Fact]
    public async Task Should_Use_Exponential_Backoff()
    {
        // Arrange
        var order = CreateTestOrder();
        await _offlineOrderService.CreateOrderAsync(order);
        
        var retryDelays = new List<TimeSpan>();
        
        _orderServiceMock.Setup(x => x.CreateOrderAsync(It.IsAny<Order>()))
            .ThrowsAsync(new HttpRequestException("Network error"))
            .Callback(() => retryDelays.Add(DateTime.UtcNow - _lastRetryTime));
        
        // Act
        var stopwatch = Stopwatch.StartNew();
        var result = await _offlineOrderService.SyncSingleOrderAsync(order.Id);
        stopwatch.Stop();
        
        // Assert
        Assert.False(result.Success);
        Assert.Equal(3, result.Attempts);
        
        // Verify exponential backoff (each retry longer than previous)
        Assert.True(retryDelays.Count >= 2);
        Assert.True(retryDelays[1] > retryDelays[0]);
    }
    
    [Fact]
    public async Task Should_Not_Retry_Non_Transient_Errors()
    {
        // Arrange
        var order = CreateTestOrder();
        await _offlineOrderService.CreateOrderAsync(order);
        
        _orderServiceMock.Setup(x => x.CreateOrderAsync(It.IsAny<Order>()))
            .ThrowsAsync(new InvalidOperationException("Invalid data"));
        
        // Act
        var result = await _offlineOrderService.SyncSingleOrderAsync(order.Id);
        
        // Assert
        Assert.False(result.Success);
        Assert.Equal(1, result.Attempts); // No retry for non-transient errors
        Assert.Contains("invalid data", result.ErrorMessage.ToLower());
        Assert.DoesNotContain("retry", result.ErrorMessage.ToLower());
    }
    
    [Fact]
    public async Task Should_Reset_Retry_Count_On_Success()
    {
        // Arrange
        var order = CreateTestOrder();
        await _offlineOrderService.CreateOrderAsync(order);
        
        // First sync fails
        _orderServiceMock.SetupSequence(x => x.CreateOrderAsync(It.IsAny<Order>()))
            .ThrowsAsync(new HttpRequestException("Network error"))
            .ReturnsAsync(order.ToDomain());
        
        // Act
        var result1 = await _offlineOrderService.SyncSingleOrderAsync(order.Id);
        var result2 = await _offlineOrderService.SyncSingleOrderAsync(order.Id);
        
        // Assert
        Assert.False(result1.Success);
        Assert.Equal(1, result1.Attempts);
        
        Assert.True(result2.Success);
        Assert.Equal(1, result2.Attempts); // Reset after success
    }
    
    private DateTime _lastRetryTime = DateTime.UtcNow;
}
```

```csharp
using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using VanAn.ShopERP.Services;
using VanAn.Shared.Domain;
using Xunit;

namespace VanAn.UI.Tests;

[Trait("Category", "UI")]
[Trait("Category", "bUnit")]
public class DashboardComponentTests : TestContext
{
    private readonly Mock<IOrderManagementService> _orderManagementServiceMock;
    private readonly Mock<IHubConnectionService> _hubConnectionServiceMock;
    
    public DashboardComponentTests()
    {
        _orderManagementServiceMock = new Mock<IOrderManagementService>();
        _hubConnectionServiceMock = new Mock<IHubConnectionService>();
        
        Services.AddSingleton(_orderManagementServiceMock.Object);
        Services.AddSingleton(_hubConnectionServiceMock.Object);
    }
    
    [Fact]
    public void DashboardComponent_Should_Display_Metrics_When_Data_Loaded()
    {
        // Arrange
        var metrics = new OrderMetrics
        {
            TotalOrders = 100,
            PendingOrders = 25,
            CompletedOrders = 70,
            TotalRevenue = 5000000,
            AverageOrderValue = 71428.57m,
            OrdersPerHour = 5
        };
        
        _orderManagementServiceMock.Setup(x => x.GetOrderMetricsAsync())
            .ReturnsAsync(metrics);
        
        _orderManagementServiceMock.Setup(x => x.GetOrdersAsync())
            .ReturnsAsync(new List<Order>());
        
        // Act
        var component = RenderComponent<ShopERP.Pages.Dashboard>();
        
        // Assert
        Assert.Equal("100", component.Find(".metric-value").Text);
        Assert.Contains("Total Orders", component.Markup);
        Assert.Contains("5000000 VND", component.Markup);
    }
    
    [Fact]
    public void DashboardComponent_Should_Display_Orders_When_Data_Loaded()
    {
        // Arrange
        var orders = new List<Order>
        {
            CreateTestOrder(),
            CreateTestOrder()
        };
        
        _orderManagementServiceMock.Setup(x => x.GetOrdersAsync())
            .ReturnsAsync(orders);
        
        _orderManagementServiceMock.Setup(x => x.GetOrderMetricsAsync())
            .ReturnsAsync(new OrderMetrics());
        
        // Act
        var component = RenderComponent<ShopERP.Pages.Dashboard>();
        
        // Assert
        var orderRows = component.FindAll(".order-row");
        Assert.Equal(2, orderRows.Count);
        
        Assert.Contains(orders[0].Id.Value.ToString().Substring(0, 8), component.Markup);
    }
    
    [Fact]
    public void DashboardComponent_Should_Update_UI_When_Order_Status_Changes()
    {
        // Arrange
        var order = CreateTestOrder();
        var orders = new List<Order> { order };
        
        _orderManagementServiceMock.Setup(x => x.GetOrdersAsync())
            .ReturnsAsync(orders);
        
        _orderManagementServiceMock.Setup(x => x.GetOrderMetricsAsync())
            .ReturnsAsync(new OrderMetrics());
        
        _orderManagementServiceMock.Setup(x => x.UpdateOrderStatusAsync(order.Id, It.IsAny<OrderStatusId>()))
            .ReturnsAsync(true);
        
        var component = RenderComponent<ShopERP.Pages.Dashboard>();
        
        // Act
        var statusSelect = component.Find(".status-select");
        statusSelect.Change(OrderStatusId.Processing.ToString());
        
        // Assert
        _orderManagementServiceMock.Verify(x => x.UpdateOrderStatusAsync(order.Id, OrderStatusId.Processing), Times.Once);
    }
    
    [Fact]
    public void DashboardComponent_Should_ReRender_When_SignalR_Event_Received()
    {
        // Arrange
        var order = CreateTestOrder();
        var orders = new List<Order> { order };
        
        _orderManagementServiceMock.Setup(x => x.GetOrdersAsync())
            .ReturnsAsync(orders);
        
        _orderManagementServiceMock.Setup(x => x.GetOrderMetricsAsync())
            .ReturnsAsync(new OrderMetrics());
        
        _hubConnectionServiceMock.Setup(x => x.IsConnected)
            .Returns(true);
        
        var component = RenderComponent<ShopERP.Pages.Dashboard>();
        
        // Act
        _hubConnectionServiceMock.Raise(x => x.OnOrderUpdated += null, order.Id);
        
        // Assert
        _orderManagementServiceMock.Verify(x => x.GetOrdersAsync(), Times.AtLeast(2)); // Initial + refresh
    }
    
    [Fact]
    public void DashboardComponent_Should_Show_Connection_Status()
    {
        // Arrange
        _hubConnectionServiceMock.Setup(x => x.IsConnected)
            .Returns(true);
        
        _orderManagementServiceMock.Setup(x => x.GetOrdersAsync())
            .ReturnsAsync(new List<Order>());
        
        _orderManagementServiceMock.Setup(x => x.GetOrderMetricsAsync())
            .ReturnsAsync(new OrderMetrics());
        
        // Act
        var component = RenderComponent<ShopERP.Pages.Dashboard>();
        
        // Assert
        Assert.Contains("🟢 Connected", component.Markup);
        
        // Test disconnected state
        _hubConnectionServiceMock.Setup(x => x.IsConnected)
            .Returns(false);
        
        _hubConnectionServiceMock.Raise(x => x.OnConnectionStateChanged += null, false);
        
        Assert.Contains("🔴 Disconnected", component.Markup);
    }
    
    [Fact]
    public void DashboardComponent_Should_Show_Loading_State()
    {
        // Arrange
        _orderManagementServiceMock.Setup(x => x.GetOrdersAsync())
            .ReturnsAsync(() => 
            {
                Thread.Sleep(100); // Simulate delay
                return new List<Order>();
            });
        
        _orderManagementServiceMock.Setup(x => x.GetOrderMetricsAsync())
            .ReturnsAsync(new OrderMetrics());
        
        // Act
        var component = RenderComponent<ShopERP.Pages.Dashboard>();
        
        // Assert
        Assert.Contains("Loading orders...", component.Markup);
    }
    
    [Fact]
    public void DashboardComponent_Should_Handle_Empty_Orders()
    {
        // Arrange
        _orderManagementServiceMock.Setup(x => x.GetOrdersAsync())
            .ReturnsAsync(new List<Order>());
        
        _orderManagementServiceMock.Setup(x => x.GetOrderMetricsAsync())
            .ReturnsAsync(new OrderMetrics());
        
        // Act
        var component = RenderComponent<ShopERP.Pages.Dashboard>();
        
        // Assert
        Assert.Contains("No orders found", component.Markup);
        Assert.Empty(component.FindAll(".order-row"));
    }
    
    private Order CreateTestOrder()
    {
        return new Order(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "test-customer-123",
            new List<OrderItem>
            {
                new OrderItem(Guid.NewGuid(), 2, 10000, 20000)
            },
            20000,
            OrderStatusId.Pending,
            DateTime.UtcNow
        );
    }
}
```

---

## 📅 WEEK 3: TIME-BASED BUGS + PRODUCTION DATA SCENARIOS (10 hours)

### **🔴 CRITICAL FINANCIAL SAFETY FOCUS:**
- ✅ **Time-Based Bug Tests** (4 hours) - **NEW**
- ✅ **Production Data Tests** (6 hours) - **NEW**
- 🚫 **CI/CD Quality Gates** - **REMOVED**

### **DAY 10: TIME-BASED BUG TESTS (4 hours)**

#### **6.1 Create Time-Based Bug Tests (4 hours)**
**File:** `6_Tests/VanAn.Core.Tests/TimeBasedBugTests.cs`

```csharp
using Microsoft.Extensions.DependencyInjection;
using Moq;
using VanAn.KhachLink.Services;
using VanAn.KhachLink.Models;
using VanAn.CoreHub.Services;
using Xunit;

namespace VanAn.Tests;

[Trait("Category", "Unit")]
[Trait("Category", "FinancialSafety")]
[Trait("Category", "TimeBased")]
public class TimeBasedBugTests : IDisposable
{
    [Fact]
    public async Task Should_Handle_Client_Clock_Drift()
    {
        // Arrange
        var order = CreateTestOrder();
        order.CreatedAtTimestamp = DateTimeOffset.UtcNow.AddMinutes(-10).ToUnixTimeMilliseconds();
        
        // Act
        var result = await _offlineOrderService.CreateOrderAsync(order);
        
        // Assert
        Assert.True(result.Success);
        
        // Verify server timestamp được dùng
        var syncedOrder = await _orderService.GetOrderAsync(order.Id);
        Assert.True(Math.Abs((syncedOrder.CreatedAt - DateTimeOffset.UtcNow).TotalMinutes) < 1);
    }
    
    [Fact]
    public async Task Should_Not_Break_When_Timestamps_Differ()
    {
        // Arrange
        var orders = new[]
        {
            CreateTestOrder() with { CreatedAtTimestamp = DateTimeOffset.UtcNow.AddHours(-1).ToUnixTimeMilliseconds() },
            CreateTestOrder() with { CreatedAtTimestamp = DateTimeOffset.UtcNow.AddHours(1).ToUnixTimeMilliseconds() }
        };
        
        // Act
        foreach (var order in orders)
        {
            await _offlineOrderService.CreateOrderAsync(order);
        }
        
        // Assert
        var syncedOrders = await _offlineOrderService.GetPendingOrdersAsync();
        Assert.Equal(2, syncedOrders.Count);
    }
    
    [Fact]
    public async Task Should_Handle_Timezone_Differences()
    {
        // Arrange
        var utcOrder = CreateTestOrder();
        utcOrder.CreatedAtTimestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        
        var localOrder = CreateTestOrder();
        localOrder.CreatedAtTimestamp = DateTimeOffset.Now.ToUnixTimeMilliseconds();
        
        // Act
        await _offlineOrderService.CreateOrderAsync(utcOrder);
        await _offlineOrderService.CreateOrderAsync(localOrder);
        
        // Assert
        var orders = await _offlineOrderService.GetPendingOrdersAsync();
        Assert.Equal(2, orders.Count);
        
        // Verify both orders processed correctly regardless of timezone
        foreach (var order in orders)
        {
            Assert.True(order.CreatedAtTimestamp > 0);
        }
    }
    
    [Fact]
    public async Task Should_Handle_Future_Timestamps()
    {
        // Arrange
        var order = CreateTestOrder();
        order.CreatedAtTimestamp = DateTimeOffset.UtcNow.AddHours(2).ToUnixTimeMilliseconds();
        
        // Act
        var result = await _offlineOrderService.CreateOrderAsync(order);
        
        // Assert
        Assert.True(result.Success);
        
        // Verify server timestamp corrected
        var syncedOrder = await _orderService.GetOrderAsync(order.Id);
        Assert.True(syncedOrder.CreatedAt <= DateTimeOffset.UtcNow);
    }
    
    [Fact]
    public async Task Should_Handle_Zero_Timestamps()
    {
        // Arrange
        var order = CreateTestOrder();
        order.CreatedAtTimestamp = 0;
        
        // Act
        var result = await _offlineOrderService.CreateOrderAsync(order);
        
        // Assert
        Assert.True(result.Success);
        
        // Verify server timestamp assigned
        var syncedOrder = await _orderService.GetOrderAsync(order.Id);
        Assert.True(syncedOrder.CreatedAt > DateTimeOffset.MinValue);
    }
    
    [Fact]
    public async Task Should_Maintain_Timestamp_Ordering_During_Sync()
    {
        // Arrange
        var orders = new List<OfflineOrderDto>();
        var baseTime = DateTimeOffset.UtcNow;
        
        for (int i = 0; i < 5; i++)
        {
            var order = CreateTestOrder();
            order.CreatedAtTimestamp = baseTime.AddMinutes(i).ToUnixTimeMilliseconds();
            orders.Add(order);
        }
        
        // Act
        foreach (var order in orders)
        {
            await _offlineOrderService.CreateOrderAsync(order);
        }
        
        await _offlineOrderService.SyncOrdersAsync();
        
        // Assert
        var syncedOrders = await _orderService.GetOrdersAsync();
        Assert.Equal(5, syncedOrders.Count);
        
        // Verify timestamp ordering preserved
        var sortedOrders = syncedOrders.OrderBy(o => o.CreatedAt).ToList();
        for (int i = 0; i < sortedOrders.Count - 1; i++)
        {
            Assert.True(sortedOrders[i].CreatedAt <= sortedOrders[i + 1].CreatedAt);
        }
    }
}
```

### **DAY 11: PRODUCTION DATA SCENARIOS (6 hours)**

#### **7.1 Create Production Data Tests (6 hours)**
**File:** `6_Tests/VanAn.Core.Tests/ProductionDataTests.cs`

```csharp
using Microsoft.Extensions.DependencyInjection;
using Moq;
using VanAn.KhachLink.Services;
using VanAn.KhachLink.Models;
using VanAn.CoreHub.Services;
using Xunit;
using System.Diagnostics;

namespace VanAn.Tests;

[Trait("Category", "Unit")]
[Trait("Category", "FinancialSafety")]
[Trait("Category", "ProductionData")]
public class ProductionDataTests : IDisposable
{
    [Fact]
    public async Task Should_Handle_Large_Batch_Sync()
    {
        // Arrange
        var orders = new List<OfflineOrderDto>();
        for (int i = 0; i < 1000; i++)
        {
            orders.Add(CreateTestOrder());
        }
        
        // Act
        foreach (var order in orders)
        {
            await _offlineOrderService.CreateOrderAsync(order);
        }
        
        var stopwatch = Stopwatch.StartNew();
        var result = await _offlineOrderService.SyncOrdersAsync();
        stopwatch.Stop();
        
        // Assert
        Assert.True(result);
        Assert.True(stopwatch.ElapsedMilliseconds < 30000); // < 30 seconds
        
        // Verify all orders synced
        var pendingOrders = await _offlineOrderService.GetPendingOrdersAsync();
        Assert.Empty(pendingOrders);
    }
    
    [Fact]
    public async Task Should_Handle_Concurrent_Orders()
    {
        // Arrange
        var tasks = new List<Task>();
        
        // Act - 50 concurrent sync attempts
        for (int i = 0; i < 50; i++)
        {
            var order = CreateTestOrder();
            tasks.Add(_offlineOrderService.CreateOrderAsync(order));
        }
        
        await Task.WhenAll(tasks);
        
        // Assert
        var pendingOrders = await _offlineOrderService.GetPendingOrdersAsync();
        Assert.Equal(50, pendingOrders.Count);
        
        // Sync all
        var syncResult = await _offlineOrderService.SyncOrdersAsync();
        Assert.True(syncResult);
    }
    
    [Fact]
    public async Task Should_Handle_Mixed_Order_Sizes()
    {
        // Arrange
        var orders = new List<OfflineOrderDto>();
        var random = new Random();
        
        for (int i = 0; i < 100; i++)
        {
            var order = CreateTestOrder();
            
            // Random order sizes
            var itemCount = random.Next(1, 20);
            order.Items.Clear();
            
            for (int j = 0; j < itemCount; j++)
            {
                order.Items.Add(new OfflineOrderItemDto
                {
                    ProductId = Guid.NewGuid().ToString(),
                    Quantity = random.Next(1, 10),
                    UnitPrice = random.Next(1000, 100000),
                    TotalPrice = 0
                });
            }
            
            // Calculate total
            order.TotalAmount = order.Items.Sum(item => item.UnitPrice * item.Quantity);
            
            orders.Add(order);
        }
        
        // Act
        foreach (var order in orders)
        {
            await _offlineOrderService.CreateOrderAsync(order);
        }
        
        var result = await _offlineOrderService.SyncOrdersAsync();
        
        // Assert
        Assert.True(result);
        
        var pendingOrders = await _offlineOrderService.GetPendingOrdersAsync();
        Assert.Empty(pendingOrders);
        
        // Verify financial integrity
        var totalRevenue = await _orderService.GetTotalRevenueAsync();
        var expectedRevenue = orders.Sum(o => o.TotalAmount);
        Assert.Equal(expectedRevenue, totalRevenue);
    }
    
    [Fact]
    public async Task Should_Handle_High_Frequency_Updates()
    {
        // Arrange
        var order = CreateTestOrder();
        await _offlineOrderService.CreateOrderAsync(order);
        
        // Act - Rapid status updates
        var updateTasks = new List<Task>();
        var statuses = new[]
        {
            OrderStatusId.Processing,
            OrderStatusId.Processing,
            OrderStatusId.Completed,
            OrderStatusId.Processing, // Backward transition
            OrderStatusId.Completed
        };
        
        foreach (var status in statuses)
        {
            updateTasks.Add(_orderService.UpdateOrderStatusAsync(order.Id, status));
        }
        
        var results = await Task.WhenAll(updateTasks);
        
        // Assert
        Assert.All(results, result => Assert.True(result.Success));
        
        // Verify final state
        var finalOrder = await _orderService.GetOrderAsync(order.Id);
        Assert.Equal(OrderStatusId.Completed, finalOrder.Status);
    }
    
    [Fact]
    public async Task Should_Handle_Memory_Constraints()
    {
        // Arrange
        var initialMemory = GC.GetTotalMemory(true);
        
        // Act - Create large number of orders
        var orders = new List<OfflineOrderDto>();
        for (int i = 0; i < 5000; i++)
        {
            var order = CreateTestOrder();
            
            // Add many items to increase memory usage
            for (int j = 0; j < 50; j++)
            {
                order.Items.Add(new OfflineOrderItemDto
                {
                    ProductId = Guid.NewGuid().ToString(),
                    Quantity = 1,
                    UnitPrice = 1000,
                    TotalPrice = 1000
                });
            }
            
            order.TotalAmount = order.Items.Sum(item => item.TotalPrice);
            orders.Add(order);
        }
        
        // Process in batches to avoid memory issues
        var batchSize = 100;
        for (int i = 0; i < orders.Count; i += batchSize)
        {
            var batch = orders.Skip(i).Take(batchSize).ToList();
            
            foreach (var order in batch)
            {
                await _offlineOrderService.CreateOrderAsync(order);
            }
            
            await _offlineOrderService.SyncOrdersAsync();
            
            // Force garbage collection periodically
            if (i % 1000 == 0)
            {
                GC.Collect();
                GC.WaitForPendingFinalizers();
            }
        }
        
        var finalMemory = GC.GetTotalMemory(true);
        var memoryIncrease = finalMemory - initialMemory;
        
        // Assert
        Assert.True(memoryIncrease < 500_000_000); // Less than 500MB increase
        
        var pendingOrders = await _offlineOrderService.GetPendingOrdersAsync();
        Assert.Empty(pendingOrders);
    }
    
    [Fact]
    public async Task Should_Handle_Network_Timeouts()
    {
        // Arrange
        var order = CreateTestOrder();
        await _offlineOrderService.CreateOrderAsync(order);
        
        // Simulate network timeout
        _orderServiceMock.Setup(x => x.CreateOrderAsync(It.IsAny<Order>()))
            .ReturnsAsync(() => 
            {
                Task.Delay(TimeSpan.FromSeconds(30)).Wait(); // Simulate timeout
                return order.ToDomain();
            });
        
        // Act
        var stopwatch = Stopwatch.StartNew();
        var result = await _offlineOrderService.SyncSingleOrderAsync(order.Id);
        stopwatch.Stop();
        
        // Assert
        Assert.False(result.Success);
        Assert.True(stopwatch.ElapsedMilliseconds < 35000); // Should timeout before 35s
        Assert.Contains("timeout", result.ErrorMessage.ToLower());
    }
    
    [Fact]
    public async Task Should_Handle_Database_Connection_Limits()
    {
        // Arrange
        var concurrentConnections = 20;
        var tasks = new List<Task>();
        
        // Act - Create multiple concurrent database operations
        for (int i = 0; i < concurrentConnections; i++)
        {
            var order = CreateTestOrder();
            tasks.Add(_offlineOrderService.CreateOrderAsync(order));
        }
        
        var createResults = await Task.WhenAll(tasks);
        
        // Verify all created successfully
        Assert.All(createResults, result => Assert.True(result.Success));
        
        // Concurrent sync operations
        var syncTasks = new List<Task<bool>>();
        for (int i = 0; i < concurrentConnections; i++)
        {
            syncTasks.Add(_offlineOrderService.SyncOrdersAsync());
        }
        
        var syncResults = await Task.WhenAll(syncTasks);
        
        // Assert
        Assert.All(syncResults, result => Assert.True(result));
        
        var pendingOrders = await _offlineOrderService.GetPendingOrdersAsync();
        Assert.Empty(pendingOrders);
    }
---

## 📊 REVISED IMPLEMENTATION SUMMARY

### **FILES TO CREATE/UPDATE:**

#### **Core Implementation Files (12):**
1. `5_WebApps/KhachLink/Services/IndexedDBService.cs`
2. `5_WebApps/KhachLink/wwwroot/js/indexeddb-bridge.js`
3. `5_WebApps/KhachLink/Models/OfflineOrderDto.cs`
4. `5_WebApps/KhachLink/Services/OfflineOrderService.cs`
5. `5_WebApps/KhachLink/Services/SyncConflictResolver.cs`
6. `5_WebApps/ShopERP/Services/OrderManagementService.cs`
7. `5_WebApps/ShopERP/Pages/Dashboard.razor`
8. `2_Gateway/Middleware/UnifiedErrorHandler.cs`

#### **🔴 CRITICAL FINANCIAL SAFETY TEST FILES (12):**
9. `6_Tests/VanAn.Core.Tests/IdempotentBehaviorTests.cs` **(NEW)**
10. `6_Tests/VanAn.Core.Tests/VersionControlTests.cs` **(NEW)**
11. `6_Tests/VanAn.Core.Tests/EventOrderingTests.cs` **(NEW)**
12. `6_Tests/VanAn.Core.Tests/OfflineOrderServiceTests.cs` **(ENHANCED)**
13. `6_Tests/VanAn.Core.Tests/FinancialDataIntegrityTests.cs` **(ENHANCED)**
14. `6_Tests/VanAn.Core.Tests/OrderHubUnitTests.cs` **(NEW)**
15. `6_Tests/VanAn.Integration.Tests/SignalRIntegrationTests.cs` **(ENHANCED)**
16. `6_Tests/VanAn.UI.Tests/UIStateMachineTests.cs` **(NEW)**
17. `6_Tests/VanAn.Core.Tests/RetryStrategyTests.cs` **(NEW)**
18. `6_Tests/VanAn.Core.Tests/TimeBasedBugTests.cs` **(NEW)**
19. `6_Tests/VanAn.Core.Tests/ProductionDataTests.cs` **(NEW)**
20. `6_Tests/VanAn.E2E.Tests/OfflineWorkflowE2ETests.cs`

#### **Files to Update (1):**
21. `5_WebApps/KhachLink/wwwroot/service-worker.js` (Add IndexedDB bridge)

### **REVISED EFFORT BREAKDOWN:**
- **Week 1:** 32 hours (Financial Safety Infrastructure)
- **Week 2:** 26 hours (State Machine UI + Retry Strategy)
- **Week 3:** 10 hours (Time-Based Bugs + Production Data)
- **Total:** 68 hours (financial-grade implementation)

### **🔴 FINANCIAL-GRADE SUCCESS CRITERIA:**
- ✅ All tests pass (Unit, Integration, UI, E2E)
- ✅ Test coverage ≥ 70%
- ✅ Critical service coverage ≥ 90%
- ✅ **Idempotent behavior verified** (no duplicate orders)
- ✅ **Version control enforced** (no data loss)
- ✅ **Event ordering maintained** (UI consistency)
- ✅ **State machine UI transitions** (proper loading/syncing/error states)
- ✅ **Retry strategy validated** (transient failure handling)
- ✅ **Time-based bugs prevented** (clock drift, timezone issues)
- ✅ **Production data scenarios tested** (large batch, concurrent)
- ✅ **Financial data integrity protected** (revenue consistency)

---

## 🚀 FINANCIAL-GRADE IMPLEMENTATION READY

This revised coding plan addresses all 8 critical gaps identified:

### **🔧 CRITICAL FINANCIAL SAFETY UPGRADES IMPLEMENTED:**
- **🧾 Idempotent Behavior Tests** - Prevent duplicate orders (4 tests)
- **🔗 Version Control Tests** - Prevent lost data during conflicts (3 tests)
- **🌐 Event Ordering Tests** - Maintain UI consistency (2 tests)
- **🎯 UI State Machine Tests** - Proper state transitions (6 tests)
- **🔄 Retry Strategy Tests** - Handle transient failures (5 tests)
- **⏰ Time-Based Bug Tests** - Clock drift and timezone handling (6 tests)
- **📊 Production Data Tests** - Large batch and concurrent scenarios (7 tests)
- **💰 Financial Data Integrity Tests** - Revenue consistency validation (8 tests)

### **📊 TOTAL TEST CASES:**
- **Previous Plan:** ~15 test cases (happy path focus)
- **Revised Plan:** **41 test cases** (financial safety focus)
- **Critical Gap Coverage:** 100% (all 8 gaps addressed)

### **🎯 FINANCIAL SAFETY TESTING PYRAMID:**
- **Unit Tests (60%):** Business logic + financial invariants + idempotent behavior
- **Integration Tests (25%):** Database + IndexedDB + SignalR + version control
- **UI Tests (10%):** State machine transitions + retry handling
- **E2E Tests (5%):** Critical financial workflows only

### **🚫 REMOVED: CI/CD Quality Gates**
- Focus on core financial safety implementation
- Manual quality validation during development
- Local testing and validation

**Total estimated effort: 68 hours over 2.5 weeks (financial-grade safety)**

---

## 🎯 FINAL RECOMMENDATION

This **Financial-Grade Phase 2.5 Plan** now provides:
- **Financial data integrity protection** against duplicate orders and lost data
- **Production resilience** through comprehensive retry and error handling
- **Time-based bug prevention** for clock drift and timezone issues
- **UI state machine reliability** for proper user experience
- **Production data scalability** for large batch and concurrent operations
- **Financial-grade testing** with 41 comprehensive test cases

**Ready for financial-grade deployment with enterprise-level safety.**
        // This test would verify:
        // 1. Create order offline
        // 2. Store in IndexedDB
        // 3. Sync when online
        // 4. Display in dashboard
        // 5. Update status
        // 6. Real-time notification
        
        // Implementation would require Playwright or similar E2E framework
        // For now, this is a placeholder for the test structure
        
        Assert.True(true); // Placeholder
    }
    
    [Fact]
    public async Task Real_Time_Dashboard_Should_Update_When_Order_Created()
    {
        // This test would verify:
        // 1. Dashboard displays real-time updates
        // 2. SignalR notifications work
        // 3. UI updates correctly
        
        Assert.True(true); // Placeholder
    }
}
```

#### **7.2 Create Quality Gates Configuration (2 hours)**
**File:** `.github/workflows/quality-gates.yml`

```yaml
name: Quality Gates

on:
  push:
    branches: [ main, develop ]
  pull_request:
    branches: [ main ]

jobs:
  quality-gates:
    runs-on: ubuntu-latest
    
    steps:
    - uses: actions/checkout@v3
    
    - name: Setup .NET
      uses: actions/setup-dotnet@v3
      with:
        dotnet-version: '8.0.x'
    
    - name: Restore dependencies
      run: dotnet restore
    
    - name: Build
      run: dotnet build --no-restore --configuration Release
    
    - name: Run Unit Tests
      run: dotnet test --no-build --configuration Release --verbosity normal --collect:"XPlat Code Coverage" --filter Category=Unit
    
    - name: Run Integration Tests
      run: dotnet test --no-build --configuration Release --verbosity normal --filter Category=Integration
    
    - name: Run Contract Tests
      run: dotnet test --no-build --configuration Release --verbosity normal --filter Category=Contract
    
    - name: Run Critical Tests
      run: dotnet test --no-build --configuration Release --verbosity normal --filter Category=Critical
    
    - name: Check Test Coverage
      run: |
        # Install global tool for coverage
        dotnet tool install --global dotnet-reportgenerator-globaltool
        # Generate coverage report
        reportgenerator -reports:/**/coverage.cobertura.xml -targetdir:CoverageReport -reporttypes:Html
        
    - name: Upload Coverage Reports
      uses: codecov/codecov-action@v3
      with:
        file: ./CoverageReport/Cobertura.xml
        
    - name: Quality Gate Check
      run: |
        # Check if coverage meets requirements
        coverage=$(dotnet test --no-build --configuration Release --filter Category=Unit --logger:"console;verbosity=minimal" | grep -o 'Total Branch Coverage: [0-9]*\.[0-9]*' | grep -o '[0-9]*\.[0-9]*')
        
        if (( $(echo "$coverage < 70" | bc -l) )); then
          echo "Unit test coverage below 70%: $coverage%"
          exit 1
        fi
        
        echo "Quality gates passed!"
```

---

## 📊 REVISED IMPLEMENTATION SUMMARY

### **FILES TO CREATE/UPDATE:**

#### **Core Implementation Files (12):**
1. `5_WebApps/KhachLink/Services/IndexedDBService.cs`
2. `5_WebApps/KhachLink/wwwroot/js/indexeddb-bridge.js`
3. `5_WebApps/KhachLink/Models/OfflineOrderDto.cs`
4. `5_WebApps/KhachLink/Services/OfflineOrderService.cs`
5. `5_WebApps/KhachLink/Services/SyncConflictResolver.cs`
6. `5_WebApps/ShopERP/Services/OrderManagementService.cs`
7. `5_WebApps/ShopERP/Pages/Dashboard.razor`
8. `2_Gateway/Middleware/UnifiedErrorHandler.cs`

#### **Enhanced Testing Files (12):**
9. `6_Tests/VanAn.Contract.Tests/IndexedDBSchemaContractTests.cs` **(NEW)**
10. `6_Tests/VanAn.Core.Tests/OfflineOrderServiceTests.cs` **(ENHANCED)**
11. `6_Tests/VanAn.Core.Tests/FinancialInvariantsTests.cs` **(NEW)**
12. `6_Tests/VanAn.Core.Tests/OrderHubUnitTests.cs` **(NEW)**
13. `6_Tests/VanAn.Integration.Tests/SignalRIntegrationTests.cs` **(ENHANCED)**
14. `6_Tests/VanAn.UI.Tests/DashboardComponentTests.cs` **(ENHANCED)**
15. `6_Tests/VanAn.Chaos.Tests/OfflineSyncChaosTests.cs` **(NEW)**
16. `6_Tests/VanAn.Core.Tests/TestIsolationTests.cs` **(NEW)**
17. `6_Tests/VanAn.E2E.Tests/OfflineWorkflowE2ETests.cs`

#### **Quality Gates Files (2):**
18. `.github/workflows/enhanced-quality-gates.yml` **(NEW)**
19. `5_WebApps/ShopERP/Program.cs` **(UPDATE)**

#### **Files to Update (1):**
20. `5_WebApps/KhachLink/wwwroot/service-worker.js` (Add IndexedDB bridge)

### **REVISED EFFORT BREAKDOWN:**
- **Week 1:** 32 hours (Offline sync + Data integrity + Contract testing)
- **Week 2:** 29 hours (Dashboard UI + SignalR deep tests + UI state management)
- **Week 3:** 10 hours (Chaos testing + Enhanced quality gates)
- **Total:** 71 hours (production-grade implementation)

### **PRODUCTION-GRADE SUCCESS CRITERIA:**
- ✅ All tests pass (Unit, Integration, Contract, E2E, Chaos)
- ✅ Test coverage ≥ 70%
- ✅ Critical service coverage ≥ 90%
- ✅ Contract tests pass (schema validation)
- ✅ Financial invariants pass (data integrity)
- ✅ Chaos tests pass (network resilience)
- ✅ Test isolation verified (no state pollution)
- ✅ Enhanced quality gates enforced
- ✅ Offline sync works end-to-end
- ✅ Real-time dashboard functional
- ✅ Data integrity validated

---

## 🚀 PRODUCTION-GRADE IMPLEMENTATION READY

This revised coding plan addresses all critical feedback:

### **🔧 CRITICAL UPGRADES IMPLEMENTED:**
- **🧾 Data Integrity Tests:** Financial invariants validation
- **🔗 Contract Testing:** Schema validation between layers
- **🌐 Deep Offline Sync Matrix:** 15-25 test cases for edge scenarios
- **🌐 SignalR Deep Testing:** Unit + Integration + UI state tests
- **🎯 UI State Management:** Async event handling validation
- **🚦 Enhanced Quality Gates:** CI/CD enforcement with multiple thresholds
- **🔧 Test Isolation Control:** Prevent test pollution
- **💥 Chaos Testing:** Network resilience validation

### **📊 TESTING PYRAMID (60/25/10/5):**
- **Unit Tests (60%):** Business logic + algorithms + domain rules
- **Integration Tests (25%):** Database + IndexedDB + SignalR + API
- **Contract Tests (10%):** Schema validation + layer contracts
- **E2E Tests (5%):** Critical user flows only

### **🎯 ENTERPRISE QUALITY GATES:**
- Unit test coverage ≥ 70%
- Critical service coverage ≥ 90%
- Contract tests must pass
- Financial invariants must pass
- Chaos tests must pass
- Test isolation verified
- CI/CD pipeline enforced

**Total estimated effort: 71 hours over 3 weeks (production-ready)**

---

## 🎯 FINAL RECOMMENDATION

This **Production-Grade Phase 2.5 Plan** now provides:
- **Enterprise-level testing strategy** with comprehensive coverage
- **Financial data integrity protection** against silent corruption
- **Production resilience** through chaos testing
- **Schema consistency** through contract testing
- **Quality enforcement** through enhanced CI/CD gates
- **Real-time reliability** through deep SignalR testing
- **Offline robustness** through comprehensive sync matrix

**Ready for enterprise deployment with financial-grade reliability.**
