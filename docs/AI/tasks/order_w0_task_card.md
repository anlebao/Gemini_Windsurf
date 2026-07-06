# TASK CARD — Order Lifecycle Wave 0: SignalR Wiring (OrderHub Broadcast)

> **Status:** 📋 PLANNING — awaiting user review
> **Prerequisite:** Master plan approved · **Branch:** `feature/order-w0-signalr-wiring` (to create from `main`)
> **Estimated sessions:** 1-2
> **Gaps fixed:** G3 (OrderWorkflowService không broadcast SignalR), G7 (ConfirmPayment không broadcast)

## Objective

Khi order status thay đổi (confirm, kitchen complete, payment confirm), `OrderHub` broadcast event cho ShopERP staff real-time (<100ms). Đây là nền tảng cho mọi wave sau (W1-W5 đều phụ thuộc).

## Architecture Decision (D1, D6)

- **Hybrid:** SignalR cho ShopERP staff (5-20 connections), HTTP Polling cho KhachLink (500-1000 khách)
- **IOrderNotificationService:** CoreHub là class library, không reference SignalR → tạo interface trong CoreHub, implement trong Gateway
- **D7:** KHÔNG sửa Domain — chỉ dùng existing `Order.UpdateOrderStatus` + `ConfirmPayment`

## Prerequisites (to verify in INVESTIGATE phase)

- [ ] `2_Gateway/Hubs/OrderHub.cs` exists — has `JoinShopGroup`/`LeaveShopGroup` only
- [ ] `2_Gateway/Program.cs:325` — `MapHub<OrderHub>("/orderHub")` exists
- [ ] `3_CoreHub/Services/OrderWorkflowService.cs` — `TransitionStatusAsync` at line 25, publishes NATS only (line 58), no SignalR
- [ ] `3_CoreHub/Services/OrderService.cs` — `ConfirmPaymentAsync` at line 550, no SignalR
- [ ] `3_CoreHub/Interfaces/IOrderHub.cs` exists — dead code, no implementation
- [ ] `2_Gateway/Controllers/OrdersController.cs:43` — already calls `_orderHub.Clients.All.SendAsync("NewOrderReceived", ...)` (pattern reference)
- [ ] `2_Gateway/Controllers/KitchenController.cs:43` — already broadcasts to KitchenHub group (pattern reference)

## Open Questions (to resolve in INVESTIGATE)

| Q | Question | Default answer |
|---|----------|----------------|
| Q1 | `IOrderNotificationService` interface đặt ở CoreHub hay Shared? | CoreHub.Interfaces (cùng chỗ IOrderHub) |
| Q2 | Broadcast ALL clients hay theo ShopGroup? | Theo ShopGroup (đã có `JoinShopGroup`) — staff join group khi login |
| Q3 | `OrderWorkflowService` inject `IOrderNotificationService` hay `IHubContext`? | `IOrderNotificationService` (CoreHub không reference SignalR) |
| Q4 | `OrderService.ConfirmPaymentAsync` cũng cần broadcast? | Yes — `PaymentConfirmed` event cho staff Dashboard |

## Files to Modify (estimated 5 files)

| File | Action | Lines |
|------|--------|-------|
| `3_CoreHub/Interfaces/IOrderNotificationService.cs` | CREATE — abstraction cho SignalR broadcast | +20 lines |
| `2_Gateway/Services/OrderNotificationService.cs` | CREATE — implement `IOrderNotificationService` using `IHubContext<OrderHub>` | +40 lines |
| `2_Gateway/Program.cs` | ADD — register `IOrderNotificationService` → `OrderNotificationService` in DI | +1 line |
| `3_CoreHub/Services/OrderWorkflowService.cs` | UPDATE — inject `IOrderNotificationService?`, call after `TransitionStatusAsync` | +10 lines |
| `3_CoreHub/Services/OrderService.cs` | UPDATE — inject `IOrderNotificationService?`, call after `ConfirmPaymentAsync` | +10 lines |

## Detailed Task List

### W0-T1: Create `IOrderNotificationService` interface (CoreHub)

```csharp
// 3_CoreHub/Interfaces/IOrderNotificationService.cs
using VanAn.Shared.Domain;

namespace VanAn.CoreHub.Interfaces
{
    /// <summary>
    /// Abstraction for real-time order notifications.
    /// Implemented in Gateway using IHubContext<OrderHub>.
    /// CoreHub remains pure class library (no SignalR dependency).
    /// </summary>
    public interface IOrderNotificationService
    {
        /// <summary>Notify staff that order status changed (confirm, preparing, ready, completed).</summary>
        Task NotifyOrderStatusChangedAsync(Guid orderId, Guid tenantId, string oldStatus, string newStatus);

        /// <summary>Notify staff that payment was confirmed for an order.</summary>
        Task NotifyPaymentConfirmedAsync(Guid orderId, Guid tenantId, string transactionId);

        /// <summary>Notify staff that a kitchen item was completed.</summary>
        Task NotifyKitchenItemCompletedAsync(Guid orderId, Guid orderItemId, string newStatus);
    }
}
```

### W0-T2: Create `OrderNotificationService` implementation (Gateway)

```csharp
// 2_Gateway/Services/OrderNotificationService.cs
using Microsoft.AspNetCore.SignalR;
using VanAn.CoreHub.Interfaces;
using VanAn.Gateway.Hubs;

namespace VanAn.Gateway.Services
{
    public class OrderNotificationService(IHubContext<OrderHub> hubContext, ILogger<OrderNotificationService> logger) : IOrderNotificationService
    {
        private readonly IHubContext<OrderHub> _hubContext = hubContext;
        private readonly ILogger<OrderNotificationService> _logger = logger;

        public async Task NotifyOrderStatusChangedAsync(Guid orderId, Guid tenantId, string oldStatus, string newStatus)
        {
            try
            {
                await _hubContext.Clients.Group($"Shop_{tenantId}")
                    .SendAsync("OrderStatusChanged", new { orderId, tenantId, oldStatus, newStatus, timestamp = DateTime.UtcNow });
                _logger.LogDebug("Broadcast OrderStatusChanged: {OrderId} {OldStatus}→{NewStatus}", orderId, oldStatus, newStatus);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to broadcast OrderStatusChanged for {OrderId}", orderId);
            }
        }

        public async Task NotifyPaymentConfirmedAsync(Guid orderId, Guid tenantId, string transactionId)
        {
            try
            {
                await _hubContext.Clients.Group($"Shop_{tenantId}")
                    .SendAsync("PaymentConfirmed", new { orderId, tenantId, transactionId, timestamp = DateTime.UtcNow });
                _logger.LogDebug("Broadcast PaymentConfirmed: {OrderId}", orderId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to broadcast PaymentConfirmed for {OrderId}", orderId);
            }
        }

        public async Task NotifyKitchenItemCompletedAsync(Guid orderId, Guid orderItemId, string newStatus)
        {
            try
            {
                await _hubContext.Clients.All
                    .SendAsync("KitchenItemCompleted", new { orderId, orderItemId, newStatus, timestamp = DateTime.UtcNow });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to broadcast KitchenItemCompleted for {OrderId}", orderId);
            }
        }
    }
}
```

### W0-T3: Register in Gateway DI

```csharp
// 2_Gateway/Program.cs — add after other service registrations
_ = builder.Services.AddScoped<VanAn.CoreHub.Interfaces.IOrderNotificationService, VanAn.Gateway.Services.OrderNotificationService>();
```

### W0-T4: Inject + call in `OrderWorkflowService`

```csharp
// 3_CoreHub/Services/OrderWorkflowService.cs
// Add to constructor params:
IOrderNotificationService? orderNotificationService = null

// Add field:
private readonly IOrderNotificationService? _orderNotificationService = orderNotificationService;

// In TransitionStatusAsync, AFTER transaction.CommitAsync() (line 60):
if (_orderNotificationService != null)
{
    await _orderNotificationService.NotifyOrderStatusChangedAsync(order.Id, order.TenantId.Value, oldStatus.Value, newStatus.Value);
}
```

### W0-T5: Inject + call in `OrderService.ConfirmPaymentAsync`

```csharp
// 3_CoreHub/Services/OrderService.cs
// Add to constructor params:
IOrderNotificationService? orderNotificationService = null

// Add field:
private readonly IOrderNotificationService? _orderNotificationService = orderNotificationService;

// In ConfirmPaymentAsync, AFTER GenerateAccountingEntriesAsync (line 583):
if (_orderNotificationService != null)
{
    await _orderNotificationService.NotifyPaymentConfirmedAsync(order.Id, tenantId, transactionId);
}
```

### W0-T6: Add `JoinOrderGroup` to OrderHub (optional — for per-order subscription)

```csharp
// 2_Gateway/Hubs/OrderHub.cs — add method:
public async Task JoinOrderGroup(string orderId)
{
    await Groups.AddToGroupAsync(Context.ConnectionId, $"Order_{orderId}");
}

public async Task LeaveOrderGroup(string orderId)
{
    await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"Order_{orderId}");
}
```

### W0-T7: Build + verify

- `dotnet build VanAn.sln` — 0 errors
- Verify DI: `IOrderNotificationService` resolves to `OrderNotificationService` in Gateway scope
- Verify: `OrderWorkflowService` constructor accepts nullable `IOrderNotificationService?` (ShopERP scope may not have it registered — graceful null)

## Verification Checklist

- [ ] Build 0 errors
- [ ] `IOrderNotificationService` interface exists in CoreHub.Interfaces (no SignalR dependency)
- [ ] `OrderNotificationService` implementation exists in Gateway.Services (uses IHubContext<OrderHub>)
- [ ] Gateway Program.cs registers `IOrderNotificationService` → `OrderNotificationService`
- [ ] `OrderWorkflowService.TransitionStatusAsync` calls `NotifyOrderStatusChangedAsync` after commit
- [ ] `OrderService.ConfirmPaymentAsync` calls `NotifyPaymentConfirmedAsync` after accounting entries
- [ ] `OrderHub` has `JoinOrderGroup`/`LeaveOrderGroup` methods
- [ ] CoreHub project does NOT reference `Microsoft.AspNetCore.SignalR` (purity intact)
- [ ] Nullable injection: services work even if `IOrderNotificationService` not registered (ShopERP scope)

## Rollback Plan

If build fails or tests break:
1. Revert `OrderWorkflowService.cs` and `OrderService.cs` constructor changes (remove `IOrderNotificationService?` param)
2. Delete `IOrderNotificationService.cs` and `OrderNotificationService.cs`
3. Remove DI registration from Gateway Program.cs
4. Build should pass (no dependencies on new code)

## Downstream Impact

| Wave | Impact | Note |
|------|--------|------|
| **W1** | `KitchenService` will call `IOrderNotificationService.NotifyOrderStatusChangedAsync` after transitioning to Ready | Needs `IOrderNotificationService?` injected (nullable, same pattern) |
| **W2** | ShopERP Orders page will subscribe to `OrderHub.OrderStatusChanged` event for real-time list update | JS: `connection.on("OrderStatusChanged", ...)` |
| **W3** | Payment confirm will trigger `NotifyPaymentConfirmedAsync` (already wired in W0-T5) | ShopERP Dashboard listens to `PaymentConfirmed` event |
| **W4** | No impact — KhachLink uses HTTP polling, not SignalR | — |
| **W5** | Tests will verify `NotifyOrderStatusChangedAsync` is called after `TransitionStatusAsync` | Mock `IOrderNotificationService` in unit tests |
