# Task Card NF-3: Buyer realtime cho status đổi trên ShopERP (LocationHub broadcast)

> **Status:** ✅ IMPLEMENTED 2026-09-24 — `DataSyncSubscriber.SyncOrderStatusAsync` (Gateway) sau khi save PG broadcast qua `IOrderNotificationService.NotifyOrderStatusChangedAsync` → LocationHub `order_{orderId}` → `OrderStatusUpdated` cho `OrderTracking.razor`. GetService null-safe; best-effort. Build + guard PASS. **Còn:** RV production (owner confirm → buyer tracking realtime <2s).
> **Priority:** P1 — buyer hiện chỉ realtime được transition chạy trên Gateway; ShopERP-driven status chỉ qua 15s polling
> **Created:** 2026-09-24
> **Master plan:** `docs\AI\tasks\notification_fix\master_plan.md`
> **Effort:** 1-2h + tests

## Problem

Buyer mở `/order-tracking/{id}` (đã login → SignalR connect `/hubs/location` + `JoinOrderTracking`). Chỉ nhận `OrderStatusUpdated` realtime khi transition chạy **trên Gateway** (shipper accept → `delivering`, delivered → `completed` qua `DeliveryWorkflowService` → `OrderWorkflowService` Gateway scope → `OrderNotificationService` → LocationHub `order_{orderId}`).

Status do **owner đổi trên ShopERP** (confirmed/preparing/ready/completed/cancelled — đa số transitions) → path: ShopERP → Outbox → NATS `vanan.shoperp.order.status.changed` → Gateway `DataSyncSubscriber` update PG **âm thầm** → buyer chờ 15s poll → "stale status" flicker mà fix #98 đã giải quyết cho Gateway path nhưng chưa cho sync path.

## Root cause (VERIFIED — file:line)

```
DataSyncSubscriber.cs:222-263   SyncOrderStatusAsync — update order.Status trong PG,
                                SaveChangesAsync, KHÔNG broadcast SignalR
OrderNotificationService.cs:42-53  LocationHub order_{orderId} broadcast chỉ tồn tại
                                trong NotifyOrderStatusChangedAsync (chỉ chạy khi
                                TransitionStatusAsync thực thi trong Gateway scope)
OrderTracking.razor:512-528      client đã listen "OrderStatusUpdated" — chỉ thiếu sender
```

## Solution

Trong `SyncOrderStatusAsync`, sau `SaveChangesAsync` thành công (line 260-261), resolve `IOrderNotificationService` từ `scopeSp` (đã có scope) và gọi:

```csharp
var notifier = scopeSp.GetService<VanAn.CoreHub.Interfaces.IOrderNotificationService>();
if (notifier != null)
{
    _ = notifier.NotifyOrderStatusChangedAsync(orderId, tenantId, oldStatus, newStatus);
}
```

- `oldStatus` = `order.Status.Value` trước `UpdateOrderStatus` (capture trước khi mutate).
- `NotifyOrderStatusChangedAsync` broadcast cả `Shop_{tenantId}` (OrderHub) lẫn `order_{orderId}` (LocationHub) — Shop group broadcast harmless (không ai join group đó trên Gateway hub hiện tại).
- Chỉ broadcast khi status thực sự đổi (`order.Status.Value != newStatus` — đã có guard line 257).
- `GetService` (không `GetRequiredService`) — DataSyncSubscriber chạy trong test contexts có thể không đăng ký → null-safe.
- Best-effort: không await (fire-and-forget như `OrderWorkflowService.cs:162`), exception đã được service tự catch.

**Không đụng:** `OrderSyncSubscriber` phía ShopERP (đó là TC-02), `OrderTracking.razor` (client đã đúng), `RealtimeAuthorizer` (join group đã auth đúng).

## Edge cases

- Sync event `order.status.changed` cũng được publish khi Gateway tự transition (qua Outbox)? — Kiểm tra: `EnqueueOrderStatusChangedEventAsync` chạy trong cả Gateway scope → event `vanan.shoperp.order.status.changed` quay lại Gateway `DataSyncSubscriber` → status đã đúng trong PG → guard `order.Status.Value != newStatus` → skip → **không double-broadcast**. Verify khi implement.
- NATS redelivery (không ack sau crash) → event trùng → guard status-idem → skip broadcast lần 2. An toàn.

## Tests

- [ ] Unit `DataSyncSubscriber` (pattern test hiện có — check `6_Tests` cho `SyncOrderStatusAsync`): mock `IOrderNotificationService`, assert `NotifyOrderStatusChangedAsync` gọi đúng 1 lần khi status đổi; không gọi khi status đã bằng.
- [ ] Build 0 errors · guard-check PASS · Core.Tests PASS.

## Acceptance

- [ ] Owner confirm/preparing/ready/completed trên ShopERP → buyer OrderTracking (đã login, đang mở trang) update status <2s, không chờ 15s poll
- [ ] Không double-update khi transition xuất phát từ Gateway
- [ ] Guest (không token) vẫn polling 15s — hành vi giữ nguyên
- [ ] RV production: đơn thật, đổi status trên shop UI → buyer app2 thấy realtime
