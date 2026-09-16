# Community Commerce Fixes — Batch 2 (RV 2026-09-16)

> **Source:** RV full-flow Community Commerce (2026-09-16) — 4 pre-existing issues discovered.
> **Priority:** #1 + #2 CRITICAL (fix this session) → #3 + #4 LOW (deferred).

---

## Issue #1 — Gateway OrdersController 400 "Operation is not valid in the current state" [CRITICAL]

### Symptom
- `GET /api/orders` → 400 `"Operation is not valid in the current state"`
- `GET /api/orders/{id}` → 400 same message
- `PUT /api/orders/{id}/status` → 400 same message
- ALL `OrdersController` endpoints fail — not just status transitions.

### Root cause
`OrdersController` constructor injects `IOrderWorkflowService` (line 19):
```csharp
public class OrdersController(
    IOrderService orderService,
    IVietQrService vietQrService,
    IHubContext<OrderHub> orderHub,
    IOrderWorkflowService orderWorkflowService,   // ← NOT REGISTERED in Gateway DI
    ILogger<OrdersController> logger) : ControllerBase
```
`2_Gateway/Program.cs` registers `IOrderService` (line 548 + 641) but **NOT** `IOrderWorkflowService`.
→ DI resolution throws `InvalidOperationException` at controller activation.
→ `UnifiedErrorHandler` maps `InvalidOperationException` → 400 `"Operation is not valid in the current state"`.

### Impact
- Owner cannot use Gateway API for any order operation (list, get, confirm, status update).
- Workaround: use ShopERP API (`app2.khachvip.online/api/orders/*`) with cookie auth — works because ShopERP Program.cs line 216 registers `IOrderWorkflowService`.
- But E2E tests + KhachLink flows that hit Gateway `/api/orders/*` are broken.

### Fix
Register `IOrderWorkflowService` → `OrderWorkflowService` in `2_Gateway/Program.cs`:
```csharp
_ = builder.Services.AddScoped<Shared.Services.IOrderWorkflowService, CoreHub.Services.OrderWorkflowService>();
```
Place near existing `IOrderService` registration (line 548).

### Verification
- `GET /api/orders` with owner JWT → 200 (today's orders list)
- `PUT /api/orders/{id}/status` with `{"status":"confirmed"}` → 204
- `GET /api/orders/{id}` → 200

### Files
- `2_Gateway/Program.cs` (1 line add)

### Risk
LOW — `OrderWorkflowService` is already used in ShopERP with same registration pattern. Gateway already has all its dependencies registered (OrderRepository, VanAnDbContext, NATS, etc.) since `IOrderService` (which uses the same deps) works.

---

## Issue #2 — DeliveryWorkflowService bypasses OrderWorkflowService (no loyalty points, no NATS sync, no accounting) [CRITICAL]

### Symptom
- Shipper marks order delivered → PG `Orders.Status = "completed"` ✅
- But customer `LoyaltyPoints` balance stays 0 (RV confirmed `PointBalance: 0`)
- ShopERP SQLite doesn't receive status update (no NATS sync)
- No accounting entries created for the order
- No referral commission processed

### Root cause
`3_CoreHub/Services/DeliveryWorkflowService.cs` line 56-66:
```csharp
if (newStatus == DeliveryTaskStatus.Delivered)
{
    var order = await _dbContext.Orders.IgnoreQueryFilters()
        .FirstOrDefaultAsync(o => o.Id == orderId);
    if (order != null)
    {
        order.UpdateOrderStatus(new OrderStatusId("completed"));  // direct update — bypasses workflow
    }
}
await _dbContext.SaveChangesAsync();
```

This bypasses `OrderWorkflowService.TransitionStatusAsync` which handles:
1. `ProcessLoyaltyPointsAsync` — awards loyalty points to customer
2. `HandleOrderCompletedAsync` — accounting entries, referral commission, customer stats update
3. `EnqueueOrderStatusChangedEventAsync` — Outbox event for NATS sync to ShopERP
4. `PublishOrderStatusChangedEventAsync` — NATS push notification

### Impact
- **Loyalty points never awarded** — customer sees 0 balance after completed delivery
- **No NATS sync** — ShopERP SQLite keeps old status (confirmed/preparing), kitchen display stale
- **No accounting** — revenue not recorded for delivered orders
- **No referral commission** — salesman doesn't earn from referred orders

### Fix
Inject `IOrderWorkflowService` into `DeliveryWorkflowService`. When `DeliveryTaskStatus.Delivered`:
1. Save the DeliveryTask status change (PickedUp/OutForDelivery/Delivered)
2. Delegate the Order status transition to `OrderWorkflowService.TransitionStatusAsync(orderId, "completed")`
3. This ensures loyalty points, NATS sync, accounting, and referral commission all fire

```csharp
public class DeliveryWorkflowService(
    IVanAnDbContext dbContext,
    IOrderWorkflowService orderWorkflowService,   // ← NEW injection
    ILogger<DeliveryWorkflowService> logger) : IDeliveryWorkflowService
{
    // ...
    if (newStatus == DeliveryTaskStatus.Delivered)
    {
        // Delegate to OrderWorkflowService for unified state-machine + loyalty + NATS + accounting.
        // DeliveryTask.MarkDelivered() already called above; now sync Order status.
        Order? result = await _orderWorkflowService.TransitionStatusAsync(orderId, new OrderStatusId("completed"));
        if (result == null)
        {
            _logger.LogWarning("DeliveryWorkflow: OrderWorkflowService.TransitionStatusAsync returned null for order {OrderId} (completed) — order may already be completed or transition invalid", orderId);
        }
    }
    await _dbContext.SaveChangesAsync();
}
```

### Verification
- Shipper delivers → customer `LoyaltyPoints` incremented in PG
- Shipper delivers → ShopERP SQLite `Orders.Status = "completed"` (NATS sync)
- Shipper delivers → accounting entries created
- Unit test: `Delivered_DelegatesToOrderWorkflowService_Completed`

### Files
- `3_CoreHub/Services/DeliveryWorkflowService.cs` (inject + delegate)
- `3_CoreHub/Services/IDeliveryWorkflowService.cs` (no change — same interface)
- `2_Gateway/Program.cs` (DI registration already exists line 450)
- `6_Tests/VanAn.Core.Tests/Community/` (new unit test)

### Risk
MEDIUM — `OrderWorkflowService.TransitionStatusAsync` validates state transitions. Current order status when shipper delivers is "delivering" (set by `AcceptOrderAsync`). Transition `delivering → completed` IS valid (line 778 of OrderWorkflowService). But if order is already "completed" (idempotent retry), `TransitionStatusAsync` returns null — logged as warning, not error.

---

## Issue #3 — Public order tracking 404 when Customer has encrypted-field data corruption [LOW — DEFERRED]

### Symptom
- `GET /api/public/orders/{id}` → 404 `"Order not found"` when `Orders.CustomerId` points to a Customer record with corrupt encrypted fields.
- Works fine when `CustomerId = NULL`.

### Root cause
`3_CoreHub/Repositories/OrderRepository.cs` line 204-221:
```csharp
return await _context.Orders
    .AsNoTracking()
    .IgnoreQueryFilters()
    .Include(o => o.Items).ThenInclude(i => i.Product)
    .Include(o => o.Customer)                    // ← triggers Customer load
    .FirstOrDefaultAsync(o => o.Id == orderId);
```
`CustomerConfiguration` line 32: `PhoneNumber` uses `EncryptedStringConverter` (DataProtection).
If Customer was SQL-inserted with plain-text phone (bypassing app layer), `Include(o => o.Customer)` triggers decryption → `CryptographicException` → catch block returns null → 404.

### Impact
LOW — only affects test data created via raw SQL INSERT. Production customers created via API/dev-token have properly encrypted phone numbers.

### Fix (deferred)
Remove `Include(o => o.Customer)` from `GetByIdWithIncludesIgnoreFiltersAsync` — `PublicOrderTrackingDto` doesn't use `order.Customer` data. Or split into two methods: one with Customer Include (for internal use), one without (for public tracking).

### Files
- `3_CoreHub/Repositories/OrderRepository.cs` (remove or split Include)

---

## Issue #4 — Public checkout creates orders with CustomerId=NULL [LOW — DEFERRED]

### Symptom
- Order created via `POST /api/public/orders/checkout` has `CustomerId = NULL` in PG.
- Chat requires `CustomerId` (ChatService line 48: `if (order.CustomerId == null || order.CustomerId == Guid.Empty) return null`)
- Loyalty points can't be awarded without CustomerId.

### Root cause
`CheckoutOrderRequest.CustomerId` is optional (line 522):
```csharp
public Guid? CustomerId { get; set; }
```
KhachLink checkout flow doesn't always send `CustomerId` — only when customer is logged in and the checkout page reads `localStorage["customer_id"]`.

### Impact
LOW for guest checkout (by design). MODERATE for community commerce flow where customer should be authenticated.

### Fix (deferred)
1. KhachLink `Checkout.razor` + `KhachLinkLayout.razor`: read `customer_id` from localStorage and include in checkout request body.
2. If `X-Customer-Token` is present but `CustomerId` is not in the request, Gateway should resolve CustomerId from the token and set it on the order.
3. For direct free-order creation (KhachLinkLayout bypass flow), include `CustomerId` from localStorage.

### Files
- `5_WebApps/KhachLink/Pages/Checkout.razor`
- `5_WebApps/KhachLink/Components/Layout/KhachLinkLayout.razor`
- `2_Gateway/Controllers/PublicOrdersController.cs` (optional: resolve from token)

---

## Execution Order

| # | Issue | Priority | Session | Effort |
|---|---|---|---|---|
| 1 | Gateway OrdersController DI | CRITICAL | This session | 15 min |
| 2 | DeliveryWorkflowService bypass | CRITICAL | This session | 1-2 hours |
| 3 | Public tracking Customer Include | LOW | Deferred | 30 min |
| 4 | Checkout CustomerId NULL | LOW | Deferred | 1 hour |

## Verification Plan (after #1 + #2)

1. Build 0 errors + guard ALL PASSED
2. Unit tests: new DeliveryWorkflowService test for delegation
3. Push + CD Multi-VPS deploy
4. RV on VPS:
   - `GET /api/orders` with owner JWT → 200 (Issue #1)
   - `PUT /api/orders/{id}/status` → 204 (Issue #1)
   - Shipper delivers → check customer `LoyaltyPoints > 0` (Issue #2)
   - Shipper delivers → check ShopERP SQLite `Orders.Status = "completed"` (Issue #2 NATS sync)
