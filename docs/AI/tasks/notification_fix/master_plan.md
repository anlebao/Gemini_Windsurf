# Master Plan — Notification Feature Fix (2026-09-24)

**Created:** 2026-09-24
**Status:** TC-01 → TC-04 ✅ IMPLEMENTED 2026-09-24 — build `VanAn.sln` 0 errors · `guard-check.ps1` ALL PASSED · `PushNotificationFanOutTests` 8/8 PASS · CHƯA commit/deploy (staged local) · **TC-05 còn lại (P2, decisions đã duyệt)** + **RV production sau deploy**
**Decisions:** (1) TC-01 = Option A — YARP route `/api/notifications/*` → shoperp-cluster ✓ · (2) TC-04 scope = owner đơn-mới + salesman-completed + assigned-shipper-cancelled ✓ (shipper "đơn mới cần nhận" theo khu vực/eligible — defer phase sau, đã ghi plan trong card) · (3) TC-05 C5 = giữ push best-effort, mark tech-debt — route `order.status.changed` qua Outbox defer phase sau
**Branch target:** `main`
**Source:** Review session 2026-09-24 — "buyer, shipper, salesman, owner không nhận được thông báo trạng thái đơn hàng thay đổi"

## Summary — Root causes đã verify (file:line)

| # | Triệu chứng | Root cause (verified) | Loại |
|---|---|---|---|
| RC-1 | Buyer KHÔNG thể bật push (toggle luôn fail) | `Profile.razor:499` POST `/api/notifications/push/subscribe` → Gateway `api2` → **Gateway không có NotificationsController** + YARP không có route `/api/notifications/*` (`appsettings.json:88-171`) → `fallback-route` → khachlink-cluster → nginx static `try_files` → 405/HTML. `PushSubscriptions` SQLite luôn rỗng → `SendOrderStatusNotificationAsync` luôn trả 0 | Routing bug — FIX |
| RC-2 | Guest checkout không bao giờ được push | `PushNotificationBackgroundService.cs:129-133` skip event khi `customerId == null` (guest order không có CustomerId) | Limitation — cần duyệt hướng |
| RC-3 | Push fail nếu thiếu VAPID key | `PushNotificationService.cs:43-46` ctor THROW khi `VAPID_PRIVATE_KEY` unset → `GetService` throw → catch log → 0 sent. Dev config là placeholder | Config risk — verify VPS |
| RC-4 | Owner/kitchen/POS UI không realtime khi đổi status | `IOrderNotificationService` **không đăng ký trong ShopERP DI** (0 match trong `5_WebApps/ShopERP`) → `OrderWorkflowService.cs:160`, `KitchenService.cs:166`, `OrderService.cs:1296` đều null → `OrderStatusChanged`/`PaymentConfirmed` listeners trong Orders/Index, Detail, Kitchen/Display, VanADashboard = **dead code** | Missing DI + impl — FIX |
| RC-5 | Owner không thấy status từ Gateway (shipper accept/delivered) realtime | `OrderSyncSubscriber.cs:404-466` update SQLite âm thầm, **không broadcast SignalR** (chỉ `OrderCreated` có broadcast, line 383) | Missing broadcast — FIX |
| RC-6 | Buyer chỉ nhận realtime cho transition chạy trên Gateway | `DataSyncSubscriber.cs:222-263` update PG âm thầm — **không broadcast LocationHub** `order_{orderId}` → confirmed/preparing/ready/completed từ ShopERP chỉ tới buyer qua 15s polling | Missing broadcast — FIX |
| RC-7 | Guest không có SignalR | `OrderTracking.razor:473` bỏ connect khi không `_customerToken` (by design — polling fallback) | By design — giữ |
| RC-8 | Shipper/Salesman/Owner không có kênh notify nào | `SendOrderStatusNotificationAsync` chỉ target `order.CustomerId` (`PushNotificationService.cs:78`); không fan-out role; `INotificationService` (email/SMS) không gọi trong order flow | Design gap — FEATURE (cần duyệt scope) |

## Bảng lỗi/stub/dead-code phụ (cleanup)

| # | Vị trí | Vấn đề |
|---|---|---|
| E5 | `OrderNotificationService.cs:32` vs `Index.razor:618` | Sender: 1 anonymous object → group `Shop_{tenantId}` (Gateway hub). Client: `On<Guid,Guid,string,string>` trên ShopERP hub — hub, group name, signature đều lệch |
| E6 | `PushNotificationService.cs:788` | `SubscribeToNatsAsync` — stub placeholder, không ai gọi |
| E7 | `3_CoreHub/Hubs/OrderHub.cs` | `NotifyStaffAsync`/`NotifyCustomerAsync` dead (client không gọi); `GetTenantId()` trả `Guid.Empty` |
| E8 | `OrderWorkflowService.cs:953` | `order.status.changed` publish trực tiếp bypass Outbox → mất event khi NATS down |
| E9 | `NatsEventPublisher.cs:39-43,95-101` | Connect fail lúc startup → null vĩnh viễn, publish skip lặng (không reconnect) |
| E13 | `PushNotificationService.cs:398` | `GetStatusMessage` thiếu `delivering`, `preparing`; có `processing` (status không tồn tại) |
| E15 | `Orders/Index.razor:644` | SignalR retry chỉ 1 lần sau 10s → mất realtime vĩnh viễn đến khi F5 |

## Fix plan (thứ tự đề xuất)

### Batch 1 — Khôi phục pipeline hiện có (không đụng Domain) — ✅ DONE 2026-09-24
1. **TC-01** ✅ — YARP `notifications-route` → `shoperp-cluster` (`appsettings.json`). HMAC passive (không chặn).
2. **TC-02** ✅ — `ShopErpOrderNotificationService` + DI + `OrderSyncSubscriber` broadcast `OrderStatusChanged` sau SQLite save.
3. **TC-03** ✅ — `DataSyncSubscriber.SyncOrderStatusAsync` broadcast `OrderStatusUpdated` lên LocationHub sau PG save.

### Batch 2 — Role fan-out — ✅ DONE 2026-09-24 (scope đã duyệt)
4. **TC-04** ✅ — Owner đơn-mới (payload `OwnerCustomerId` + push trong `OrderSyncSubscriber`) · salesman-completed (`salesmanId` trong `order.status.changed` → `/community/sales-dashboard`) · assigned-shipper-cancelled (`assignedShipperId`=`Order.ShipperId` → `/community/active-deliveries`) · Gateway `rolesOnly` republish cho transition ShopERP-initiated · strict `GetGuid` parse (không TryParse-stub). Tests: `PushNotificationFanOutTests` 8/8. **Deferred:** shipper "đơn mới cần nhận" theo khu vực/eligible (plan trong card).

### Batch 3 — Hardening + debt — ⏳ PLAN (decisions đã duyệt)
5. **TC-05** — Cleanup: stub E6, dead hub methods E7, status text E13, NatsEventPublisher reconnect E9, SignalR retry E15. Đã duyệt: giữ push best-effort; `order.status.changed` qua Outbox = tech-debt defer; RC-2 guest push còn mở.

## Hard stop checks
- Không card nào đụng `Domain.cs` hay `AccountingEntry` ✓
- **TC-01 Option A** thêm YARP route — config change, không business logic → OK; **Option B** (Gateway proxy controller) nếu cần forward header/custom logic.
- **TC-04** = feature mới → workflow `newfeaturebuild.md`, cần duyệt scope vai trò × sự kiện trước khi IMPLEMENT.
- **E8** đổi `order.status.changed` sang Outbox = architecture decision → cần duyệt (mặc định giữ best-effort).
- Gate 4: UI layout change → E2E spec `6_Testing/e2e-tests/` (TC-02 nếu đụng razor; TC-04 có UI toggle).
- Playwright ISOLATION: không chạy E2E diện rộng trong IMPLEMENT; spec viết xong chạy khi có window.

## Acceptance criteria
- [x] Build `VanAn.sln` 0 errors · `guard-check.ps1` PASS · Core.Tests PASS (fan-out 8/8 + regression suites) — 2026-09-24
- [ ] TC-01: customer bật push trên Profile → 200 + row trong `PushSubscriptions`; `push/status` trả `enabled:true` — **chờ RV production**
- [ ] TC-02: owner đổi status trên ShopERP → màn hình Orders/Kitchen khác update realtime (không F5); status từ Gateway (shipper) cũng realtime — **chờ RV production**
- [ ] TC-03: owner confirm trên ShopERP → buyer OrderTracking nhận `OrderStatusUpdated` realtime (<2s) — **chờ RV production**
- [ ] TC-04: shipper/salesman/owner nhận push đúng sự kiện đã duyệt — **chờ RV production** (owner push cần `Tenant.OwnerCustomerId` đã bind trên PG — lazy bind qua CommunityController)
- [ ] RV production: subscribe → 200; đổi status → push tới thiết bị thật; Gateway/ShopERP log không còn "No active push subscriptions" cho user đã subscribe; verify `VAPID_PRIVATE_KEY` trong `.env.shoperp`

## Task cards
- `task_card_01_push_subscribe_routing.md` — RC-1 + RC-3 (P0)
- `task_card_02_shoperp_staff_realtime.md` — RC-4 + RC-5 + E5 (P0)
- `task_card_03_buyer_realtime_shoperp_status.md` — RC-6 (P1)
- `task_card_04_role_fanout_push.md` — RC-8 (P1, cần duyệt scope)
- `task_card_05_notification_hardening_cleanup.md` — E6/E7/E8/E9/E13/E15 + RC-2 (P2)
