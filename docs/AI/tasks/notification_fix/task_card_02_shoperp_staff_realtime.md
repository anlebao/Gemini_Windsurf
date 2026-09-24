# Task Card NF-2: ShopERP staff realtime — đăng ký IOrderNotificationService + broadcast sau sync

> **Status:** ✅ IMPLEMENTED 2026-09-24 — `ShopErpOrderNotificationService` (mới, `5_WebApps/ShopERP/Services/`) implement `IOrderNotificationService` bằng `IHubContext<OrderHub>` với signature positional `(Guid orderId, Guid tenantId, string oldStatus, string newStatus)` khớp client `On<Guid,Guid,string,string>`; đăng ký DI trong `Program.cs`; `OrderSyncSubscriber.SyncOrderStatusChangedAsync` broadcast `OrderStatusChanged` sau khi save SQLite (best-effort try/catch — không rollback sync). Build + guard PASS. **Còn:** RV production (owner đổi status → màn Orders/Kitchen update không F5).
> **Priority:** P0 — owner/kitchen/POS không thấy status update realtime
> **Created:** 2026-09-24
> **Master plan:** `docs\AI\tasks\notification_fix\master_plan.md`
> **Effort:** 3-4h + tests

## Problem

Owner/staff đổi status đơn trên ShopERP (POS, Kitchen Display, Orders) → các màn hình khác **không cập nhật realtime**, chỉ thấy sau poll timer (10-30s) hoặc F5. Tương tự khi status đổi từ Gateway (shipper accept → `delivering`, delivered → `completed`) — owner UI cũng không biết.

## Root cause (VERIFIED — file:line)

**RC-4a — Missing DI:** `IOrderNotificationService` chỉ đăng ký ở Gateway (`2_Gateway/Program.cs:1396`). Grep `5_WebApps/ShopERP` → 0 match. Comment trong `OrderWorkflowService.cs:50` xác nhận: `"null in ShopERP scope — Gateway has OrderHub"`.

Hệ quả — 3 call-site đều no-op khi chạy trong ShopERP:
- `OrderWorkflowService.cs:160-164` (`TransitionStatusCoreAsync`)
- `KitchenService.cs:166-171` (kitchen all-items-done → ready)
- `OrderService.cs:1296-1299` (`MarkPaidAsync` → PaymentConfirmed)

→ `On("OrderStatusChanged")` trong `Orders/Index.razor:618`, `Orders/Detail.razor:394`, `Kitchen/Display.razor:303`, `VanADashboard.razor:259` và `On("PaymentConfirmed")` trong `Index.razor:632` = **dead listeners**.

**RC-5 — Missing broadcast sau sync:** `OrderSyncSubscriber.cs:404-466` (`SyncOrderStatusChangedAsync`) update SQLite nhưng không phát SignalR → status từ Gateway (shipper actions) vô hình với owner UI.

**E5 — Contract mismatch (thiết kế khi fix):** Gateway sender gửi `SendAsync("OrderStatusChanged", new { orderId, tenantId, oldStatus, newStatus, timestamp })` (1 object) → `Shop_{tenantId}` group (Gateway hub). ShopERP client chờ `On<Guid,Guid,string,string>` trên ShopERP hub — phải implement sender MỚI khớp signature client, không reuse payload shape của Gateway.

## Solution

### A. Implement `ShopErpOrderNotificationService` (file mới trong ShopERP)

`5_WebApps/ShopERP/Services/ShopErpOrderNotificationService.cs`:

- `IHubContext<VanAn.CoreHub.Hubs.OrderHub>` (hub ShopERP map tại `/orderHub`, `Program.cs:1104`)
- `NotifyOrderStatusChangedAsync(orderId, tenantId, oldStatus, newStatus)` →
  `Clients.All.SendAsync("OrderStatusChanged", orderId, tenantId, oldStatus, newStatus)` — **4 args rời khớp `On<Guid,Guid,string,string>`**
- `NotifyPaymentConfirmedAsync` → `Clients.All.SendAsync("PaymentConfirmed", orderId, tenantId, transactionId)` — khớp `On<Guid,Guid,string>`
- `NotifyKitchenItemCompletedAsync` → `Clients.All.SendAsync("KitchenItemCompleted", orderId, orderItemId, newStatus)` (kitchen client nếu có listener — check trước, nếu không thì vẫn gửi harmless)
- `Clients.All` (không group) — cùng pattern `OrderSyncSubscriber` `OrderCreated` broadcast (`OrderSyncSubscriber.cs:383`); ShopERP là single-tenant-per-instance nên không leak cross-tenant.
- Best-effort: try/catch log, không throw (khớp contract interface).

DI: `5_WebApps/ShopERP/Program.cs` thêm
`_ = builder.Services.AddScoped<VanAn.CoreHub.Interfaces.IOrderNotificationService, VanAn.ShopERP.Services.ShopErpOrderNotificationService>();`

### B. Broadcast sau NATS status sync (RC-5)

`OrderSyncSubscriber.SyncOrderStatusChangedAsync` — sau `SaveChangesAsync` (line 458) thêm:

```csharp
await _hubContext.Clients.All.SendAsync("OrderStatusChanged",
    orderId, order.TenantId.Value, oldStatusValue, newStatus, cancellationToken);
```

(capture `oldStatus = order.Status.Value` trước `UpdateOrderStatus`; tenantId lấy từ `order.TenantId.Value` hoặc payload `tenantId`.)

### C. Không đụng

- Gateway `OrderNotificationService` + `Shop_{tenantId}` group — giữ nguyên (phục vụ client Gateway-hub nếu có).
- `OrderSyncSubscriber.SyncOrderCreatedAsync` broadcast `OrderCreated` — đã đúng.

## Tests

- [ ] Unit: `ShopErpOrderNotificationService` — mock `IHubContext<OrderHub>`/`IClientProxy`, assert `SendAsync` đúng method name + 4 args, exception → swallow + log (pattern test hiện có cho Gateway `OrderNotificationService` nếu tồn tại — check `6_Tests`).
- [ ] Unit/integration: `SyncOrderStatusChangedAsync` — sau update status, verify hub `SendAsync("OrderStatusChanged", …)` được gọi (test subclass đã có `CreateSubscriptionConnection` virtual — reuse).
- [ ] Build `VanAn.sln` 0 errors · guard-check PASS · Core.Tests PASS.

## Acceptance

- [ ] Owner mở Orders/Index + Kitchen/Display trên 2 tab/thiết bị → đổi status ở 1 tab → tab kia update <2s (không F5)
- [ ] Kitchen mark-ready → Orders/Index update realtime
- [ ] Shipper accept (Gateway) → NATS sync → owner UI update realtime
- [ ] `MarkPaidAsync` trên ShopERP → PaymentConfirmed listener fire
- [ ] RV: 2 màn hình thật trên production shop-a
