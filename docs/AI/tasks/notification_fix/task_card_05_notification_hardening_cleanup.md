# Task Card NF-5: Notification hardening + cleanup (stubs, dead code, resilience)

> **Status:** PLAN — chưa implement. Decisions đã duyệt 2026-09-24: giữ push best-effort (C5); route `order.status.changed` qua Outbox = **tech-debt, defer phase sau**. Còn lại chờ duyệt/triển khai: E6 stub `SubscribeToNatsAsync`, E7 dead hub methods, E9 NATS reconnect, E13 status text, E15 SignalR retry, RC-2 guest push.
> **Priority:** P2
> **Created:** 2026-09-24
> **Master plan:** `docs\AI\tasks\notification_fix\master_plan.md`
> **Effort:** 4-6h tuỳ scope

## Mục tiêu

Dọn các lỗi nhỏ/stub/dead-code phát hiện trong review + tăng resilience. **Không** blocking cho TC-01..04.

## Danh sách

### C1 — Xoá stub `SubscribeToNatsAsync` (E6)
`PushNotificationService.cs:788-807` — placeholder tự nhận "This is a placeholder", không ai gọi. Subscription thật = `PushNotificationBackgroundService`. Xoá method hoặc mark `[Obsolete]` → xoá luôn (internal, không public contract).

### C2 — Dead hub methods (E7)
`3_CoreHub/Hubs/OrderHub.cs`:
- `NotifyStaffAsync`/`NotifyCustomerAsync` — hub methods client-side gọi, không client nào gọi → xoá hoặc comment deprecated.
- `GetTenantId()` trả `Guid.Empty` khi không có claim `TenantId` → `JoinOrderGroup`/`JoinTenantGroup` join group `tenant_0000…` vô nghĩa. Giữ group join cho `order_{id}` nhưng bỏ tenant group khi `Guid.Empty` (hoặc log warning).
- ⚠️ Trước khi xoá: grep toàn bộ `.razor`/`.js` confirm không ai invoke `NotifyStaff`/`NotifyCustomer`/`JoinOrderGroup`/`JoinTenantGroup` (đã grep sơ bộ — re-verify khi implement).

### C3 — Status text thiếu (E13)
`PushNotificationService.cs:398-407` `GetStatusMessage`:
- Thêm `"preparing" => "Đơn hàng đang được chuẩn bị"`, `"delivering" => "Đơn hàng đang được giao"`.
- `processing` — status không tồn tại trong state machine (`OrderWorkflowService.cs:912-942`) → đổi thành `preparing` hoặc xoá.

### C4 — NatsEventPublisher reconnect (E9)
`NatsEventPublisher.cs:95-101` — `CreateConnection` fail → `_connection = null` → `IsConnected` false mãi → publish skip lặng vĩnh viễn (status sync vẫn sống nhờ Outbox, nhưng push event chết).
- Đề xuất: lazy reconnect trong `PublishAsync` — `if (!IsConnected) TryReconnect()` với throttle (e.g. 30s/attempt). Đơn giản, không cần background loop.
- Hoặc: đăng ký `opts.ReconnectedEventHandler`/`DisconnectedEventHandler` — NATS client auto-reconnect đã có `MaxReconnect=5`; vấn đề chỉ khi connect ban đầu fail → thêm retry logic ở publish-time.

### C5 — Push event durability (E8) — **DECIDED 2026-09-24: giữ best-effort, mark tech-debt**
`order.status.changed` publish trực tiếp bypass Outbox → NATS down tại thời điểm transition = push mất (status sync vẫn an toàn nhờ Outbox riêng). Polling + TC-03 SignalR cover correctness → push-only loss chấp nhận được.
- **DEFER phase sau (đã ghi plan):** gộp vào `EnqueueOrderStatusChangedEventAsync` payload thêm `customerId` + consumer (PushNotificationBackgroundService) đọc `vanan.shoperp.order.status.changed.{shopInstanceId}` thay vì `order.status.changed` → durable miễn phí (event đã qua outbox). Trade-off: chỉ ShopERP của shopInstance nhận — đúng hơn broadcast hiện tại.

### C6 — Guest push (RC-2) — **cần duyệt**
Guest không login → không `customerId` → skip push. Subscribe flow cũng cần `X-Customer-Token`.
- Option: subscribe theo `CustomerDeviceId` (guest có device fingerprint) — schema `PushSubscriptions` cần thêm cột/alternate key → **đụng Domain entity** → nếu làm phải qua approval riêng. Đề xuất defer tech-debt.

### C7 — SignalR retry 1-shot (E15)
`Orders/Index.razor:644-649` — retry 1 lần sau 10s rồi bỏ. `WithAutomaticReconnect()` đã có (line 615) — retry loop thủ công chỉ cover initial-connect fail. Đề xuất: thay bằng `WithAutomaticReconnect` retry policy tuỳ biến (e.g. `[0, 2, 10, 30]s` rồi lặp) hoặc exponential backoff loop — áp dụng chung cho Detail/Kitchen/VanADashboard nếu cùng pattern.

### C8 — TenantId trên PushSubscription/PushNotificationDelivery (E11/E12)
- `PushSubscriptionRepository.cs:15` — `_currentTenantId = Guid.Empty` trong ShopERP scope (ShopERPDbContext ≠ VanAnDbContext) → subscription tạo với `TenantId.Empty`. Resolve tenant thật từ `ResolveCustomerTenant`/tenant provider.
- `PushNotificationService.cs:335-341` — `PushNotificationDelivery` tạo `TenantId(Guid.Empty)` + gán `NotificationId` qua reflection `SetValue` — fragile; dùng proper setter/ctor nếu domain cho phép (check Domain trước — nếu cần đổi Domain → approval).

## Acceptance

- [ ] Không còn stub `SubscribeToNatsAsync`, dead hub methods dọn xong (hoặc documented)
- [ ] Push body đúng cho mọi status (`preparing`, `delivering`)
- [ ] NATS down-lúc-startup → publish phục hồi khi NATS quay lại (test bằng cách stop/start nats container)
- [ ] C5/C6: quyết định ghi nhận trong card (implement hoặc mark tech-debt)
- [ ] Build 0 errors · guard-check PASS · Core.Tests PASS
