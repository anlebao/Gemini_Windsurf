# Master Plan — Notification Feature Fix (2026-09-24)

**Created:** 2026-09-24
**Status:** TC-01 → TC-04 ✅ IMPLEMENTED + DEPLOYED (commit `07827a4a`, CI/Acct-Tests/CD Multi-VPS ALL SUCCESS) · **RV 2026-09-24: pipeline E2E sống nhưng last-mile send FAIL — E16 mới (xem Batch 3)** · **TC-05 còn lại (P2, decisions đã duyệt)**
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
| **E16** | `NotificationsController.cs:45` vs `PushNotificationService.cs:98` (+7 chỗ deserialize khác) | **CONFIRMED PROD (RV 2026-09-24):** subscribe lưu `SubscriptionJson` = serialize cả `PushSubscriptionRequest` DTO → shape nested `{Endpoint, Keys:{P256dh,Auth}}`. Send side `JsonSerializer.Deserialize<WebPush.PushSubscription>` cần flat `{endpoint,p256dh,auth}` → P256dh/Auth = null → `GenerateRequestDetails` throw `ArgumentException: subscription must have 'auth' and 'p256dh' keys` → **100% send fail**. Trước bị che bởi RC-1 (không row nào tới được DB). Fix hướng: parse nested shape → `new PushSubscription(endpoint, p256dh, auth)` ở send side (an toàn với data cũ), hoặc flatten ở store side + migrate rows hiện có |
| **E17** | prod ShopERP logs | `PushNotificationService initialized` mỗi ~30s — một background worker resolve scoped service theo poll (noise/perf, không lỗi chức năng) |
| **E11-prod** | `PushSubscriptions` row thật trên prod | `TenantId='00000000-…'` (ShopERPDbContext `_currentTenantId=Guid.Empty`) — hiện vô hại vì `GetByCustomerIdAsync` chỉ filter `CustomerId+IsActive`, nhưng là multi-tenant data-quality debt |

## Fix plan (thứ tự đề xuất)

### Batch 1 — Khôi phục pipeline hiện có (không đụng Domain) — ✅ DONE 2026-09-24
1. **TC-01** ✅ — YARP `notifications-route` → `shoperp-cluster` (`appsettings.json`). HMAC passive (không chặn).
2. **TC-02** ✅ — `ShopErpOrderNotificationService` + DI + `OrderSyncSubscriber` broadcast `OrderStatusChanged` sau SQLite save.
3. **TC-03** ✅ — `DataSyncSubscriber.SyncOrderStatusAsync` broadcast `OrderStatusUpdated` lên LocationHub sau PG save.

### Batch 2 — Role fan-out — ✅ DONE 2026-09-24 (scope đã duyệt)
4. **TC-04** ✅ — Owner đơn-mới (payload `OwnerCustomerId` + push trong `OrderSyncSubscriber`) · salesman-completed (`salesmanId` trong `order.status.changed` → `/community/sales-dashboard`) · assigned-shipper-cancelled (`assignedShipperId`=`Order.ShipperId` → `/community/active-deliveries`) · Gateway `rolesOnly` republish cho transition ShopERP-initiated · strict `GetGuid` parse (không TryParse-stub). Tests: `PushNotificationFanOutTests` 8/8. **Deferred:** shipper "đơn mới cần nhận" theo khu vực/eligible (plan trong card).

### Batch 3 — Hardening + debt — ⏳ PLAN (decisions đã duyệt + findings từ RV)
5. **E16 (P0 trong batch — blocker thực của toàn kênh push):** normalize subscription shape nested↔flat (xem bảng lỗi).
6. **TC-05** — Cleanup: stub E6, dead hub methods E7, status text E13, NatsEventPublisher reconnect E9, SignalR retry E15, E17 (service re-init mỗi 30s), E11-prod (TenantId.Empty). Đã duyệt: giữ push best-effort; `order.status.changed` qua Outbox = tech-debt defer; RC-2 guest push còn mở.
7. **Deferred theo duyệt:** shipper "đơn mới cần nhận" (khu vực/eligible) — plan trong `task_card_04`.

## Hard stop checks
- Không card nào đụng `Domain.cs` hay `AccountingEntry` ✓
- **TC-01 Option A** thêm YARP route — config change, không business logic → OK; **Option B** (Gateway proxy controller) nếu cần forward header/custom logic.
- **TC-04** = feature mới → workflow `newfeaturebuild.md`, cần duyệt scope vai trò × sự kiện trước khi IMPLEMENT.
- **E8** đổi `order.status.changed` sang Outbox = architecture decision → cần duyệt (mặc định giữ best-effort).
- Gate 4: UI layout change → E2E spec `6_Testing/e2e-tests/` (TC-02 nếu đụng razor; TC-04 có UI toggle).
- Playwright ISOLATION: không chạy E2E diện rộng trong IMPLEMENT; spec viết xong chạy khi có window.

## Acceptance criteria — RV 2026-09-24 trên prod
- [x] Build `VanAn.sln` 0 errors · `guard-check.ps1` PASS · Core.Tests PASS (fan-out 8/8 + regression suites) — 2026-09-24
- [x] **TC-01 routing:** `/api/notifications/push/subscribe|status` trước 405/HTML qua khachlink → giờ **401 JSON từ ShopERP auth** (đúng service); `PushSubscriptions` có 1 row thật sau khi route mở ✓
- [x] **Deploy markers:** `rolesOnly`/`assignedShipperId`/`OwnerCustomerId` trong binaries; `PushNotificationBackgroundService` subscribed `order.status.changed`; `OrderSyncSubscriber` subscribed routed subjects `vanan.cloud.order.*.9e94f876-…`; `VAPID_PRIVATE_KEY` present (RC-3 clear trên prod)
- [x] **SignalR hubs reachable:** `/orderHub/negotiate` 200, `/hubs/location/negotiate` 200
- [x] **E2E NATS→consumer→send-attempt:** publish `order.status.changed` test (orderId `6947fb4b…`, customerId thật `6d0eba8a…`, salesmanId random) → ShopERP log: tìm đúng subscription → gọi WebPush cho buyer + bulk cho salesman (`Sent=0, Failed=1` vì random GUID không có sub) → `dispatched` logged. **Toàn bộ đường dẫn code hoạt động.**
- [ ] **Last-mile WebPush send: FAIL — E16** (shape mismatch, xem bảng lỗi). `Push notifications sent: 0/1`.
- [ ] TC-02/TC-03 realtime trong browser + TC-04 trên order thật (owner/salesman/shipper thật có subscription) — **cần tài khoản/device thật, chờ sau khi E16 fixed**

## Task cards
- `task_card_01_push_subscribe_routing.md` — RC-1 + RC-3 (P0)
- `task_card_02_shoperp_staff_realtime.md` — RC-4 + RC-5 + E5 (P0)
- `task_card_03_buyer_realtime_shoperp_status.md` — RC-6 (P1)
- `task_card_04_role_fanout_push.md` — RC-8 (P1, cần duyệt scope)
- `task_card_05_notification_hardening_cleanup.md` — E6/E7/E8/E9/E13/E15 + RC-2 (P2)
