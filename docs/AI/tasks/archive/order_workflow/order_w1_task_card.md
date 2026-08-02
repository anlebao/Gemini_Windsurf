# TASK CARD — Order Lifecycle Wave 1: Kitchen → OrderStatus Transition

> **Status:** 📋 PLANNING — awaiting user review
> **Prerequisite:** W0 merged (SignalR wiring) · **Branch:** `feature/order-w1-kitchen-status-transition`
> **Estimated sessions:** 1
> **Gap fixed:** G2 (Kitchen complete không transition OrderStatus → Ready)

## Objective

Khi tất cả `OrderItem` trong 1 order đạt `KitchenStatus.Completed`, tự động transition `OrderStatus` → `Ready` (kitchen xong, chờ giao/serve). Broadcast `OrderStatusChanged` qua W0 wiring.

## Architecture Decision (D3)

- **Auto-transition:** `KitchenService` tự động set `OrderStatus = Ready` khi all items completed
- **Semantic split:** `Ready` = kitchen xong chờ giao (W1) | `Completed` = khách đã nhận hàng (manual, sau)
- **Current bug:** `KitchenService.UpdateItemStatusAsync` gọi `order.MarkAsCompleted()` (set `CompletedAt`) — cần đổi thành `order.UpdateOrderStatus(OrderStatusId.Ready)` thay vì `MarkAsCompleted`

## Prerequisites (to verify in INVESTIGATE)

- [ ] W0 merged — `IOrderNotificationService` available
- [ ] `3_CoreHub/Services/KitchenService.cs:86-121` — `UpdateItemStatusAsync` checks all items completed
- [ ] `1_Shared/Domain.cs:998-1002` — `Order.MarkAsCompleted()` sets `CompletedAt`
- [ ] `1_Shared/Domain.cs:977-981` — `Order.UpdateOrderStatus(status)` sets `Status`
- [ ] `1_Shared/Domain.cs:428` — `OrderStatusId.Ready = new("ready")` exists
- [ ] `OrderWorkflowService.IsTransitionValidAsync` — check `preparing → ready` is valid transition (line 203: YES)

## Open Questions

| Q | Question | Default answer |
|---|----------|----------------|
| Q1 | Đổi `MarkAsCompleted()` → `UpdateOrderStatus(Ready)` hay giữ cả 2? | Chỉ `UpdateOrderStatus(Ready)` — `MarkAsCompleted` dành cho `Completed` (khách nhận) |
| Q2 | `KitchenService` inject `IOrderNotificationService`? | Yes (nullable, same pattern as W0) |
| Q3 | Cần save changes sau `UpdateOrderStatus`? | Yes — `_context.SaveChangesAsync()` (đã có ở line 115) |

## Files to Modify (estimated 2 files)

| File | Action | Lines |
|------|--------|-------|
| `3_CoreHub/Services/KitchenService.cs` | UPDATE — replace `MarkAsCompleted()` with `UpdateOrderStatus(Ready)`, inject `IOrderNotificationService?`, broadcast after save | +15 lines |
| `6_Tests/VanAn.Core.Tests/Services/KitchenServiceTests.cs` | ADD — test all items completed → OrderStatus = Ready | +30 lines |

## Detailed Task List

### W1-T1: Modify `KitchenService.UpdateItemStatusAsync`

```csharp
// 3_CoreHub/Services/KitchenService.cs — line 102-113
// BEFORE:
if (remainingItems.All(oi => oi.KitchenStatus == KitchenStatus.Completed))
{
    orderItem.Order.UpdateKitchenStatus(KitchenStatus.Completed);
    orderItem.Order.MarkAsCompleted();  // ← BUG: should be Ready, not Completed
}

// AFTER:
if (remainingItems.All(oi => oi.KitchenStatus == KitchenStatus.Completed))
{
    orderItem.Order.UpdateKitchenStatus(KitchenStatus.Completed);
    orderItem.Order.UpdateOrderStatus(OrderStatusId.Ready);  // Kitchen done → Ready for pickup
    // NOTE: MarkAsCompleted() is for when customer receives order (Completed status), NOT kitchen done
}

// After SaveChangesAsync (line 115), add:
if (_orderNotificationService != null && /* all items completed */)
{
    await _orderNotificationService.NotifyOrderStatusChangedAsync(
        orderItem.OrderId, orderItem.Order.TenantId.Value,
        oldStatus: "preparing", newStatus: "ready");
}
```

### W1-T2: Inject `IOrderNotificationService?` into KitchenService constructor

```csharp
// Add to constructor params:
IOrderNotificationService? orderNotificationService = null

// Add field:
private readonly IOrderNotificationService? _orderNotificationService = orderNotificationService;
```

### W1-T3: Add unit test

```csharp
// Test: When all OrderItems completed → Order.Status == "ready"
// Test: When not all items completed → Order.Status unchanged
// Test: NotifyOrderStatusChangedAsync called with "ready"
```

### W1-T4: Build + verify

- `dotnet build VanAn.sln` — 0 errors
- Unit tests pass

## Verification Checklist

- [ ] Build 0 errors
- [ ] `KitchenService.UpdateItemStatusAsync` calls `UpdateOrderStatus(Ready)` (not `MarkAsCompleted`) when all items completed
- [ ] `IOrderNotificationService.NotifyOrderStatusChangedAsync` called after save with `newStatus: "ready"`
- [ ] `MarkAsCompleted()` NOT called in kitchen flow (reserved for customer pickup)
- [ ] Unit test: all items completed → OrderStatus = Ready
- [ ] Unit test: partial items completed → OrderStatus unchanged
- [ ] Nullable injection works (KitchenService works without IOrderNotificationService)

## Rollback Plan

Revert `KitchenService.cs` to use `MarkAsCompleted()` instead of `UpdateOrderStatus(Ready)`. Build passes.
