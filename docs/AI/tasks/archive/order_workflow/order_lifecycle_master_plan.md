# MASTER PLAN — Order Lifecycle Realtime (Admin Confirm + Kitchen Complete + Payment + Realtime Push)

> **Status:** ✅ COMPLETE — VPS Runtime Verified (2026-07-15)
> **Created:** 2026-07-05 · **Last Updated:** 2026-07-15
> **Workflow:** `newfeaturebuild.md` (ANALYZE → IMPLEMENT) · **Branch:** per-wave feature branch
> **Architecture decision:** Hybrid — HTTP Polling (KhachLink 500-1000 khách) + SignalR (ShopERP staff 5-20 người)
> **Payment decision:** Option B — Admin ShopERP manual confirm "Đã nhận tiền"

---

## 0. JIT PLANNING STRATEGY (NON-NEGOTIABLE)

**Nguyên tắc cốt lõi:** KHÔNG code mò mẫm — **Investigate trước, Implement sau**. Áp dụng cho mỗi wave.

### 3-Phase per wave
```
Phase 1 (INVESTIGATE): Đọc task card wave + verify codebase hiện tại
  → Confirm file paths, signatures, dependencies vẫn đúng
  → Grep usage của methods/symbols sẽ touch
  → Identify blast radius (ai gọi method này?)
  → Output: confirm task card vẫn accurate, hoặc flag drift

Phase 2 (PLAN): Detail coding plan
  → Liệt kê exact changes (file:line, old→new)
  → Identify test files cần update
  → Identify DI registrations cần thêm
  → Output: checklist implement

Phase 3 (IMPLEMENT): Code + verify
  → Apply changes theo checklist
  → Build + guard + tests pass
  → Commit
```

### Anti-Guessing Gate (Gate 1 từ .windsurfrules)
- Assumptions ≥ Verified Facts → CẤM code, chuyển Investigate
- Mỗi wave phải có ≥ 3 verified facts trước khi implement

---

## 1. EXECUTION RULES

### Dependency chain
```
W-1 (Sync fix) → W0 (SignalR wiring) → W1 (Kitchen → OrderStatus) → W2 (Admin UI Orders page) → W3 (Payment confirm UI) → W4 (KhachLink polling optimize) → W5 (Tests + Sitemap)
```
- W-1 là nền tảng tuyệt đối — sync SQLite→PostgreSQL phải hoạt động trước
- W0 phụ thuộc W-1 (SignalR cần data đã sync)
- W1-W2 có thể song song (Kitchen logic vs Admin UI)
- W3 phụ thuộc W2 (cùng trang Orders)
- W4 độc lập (chỉ tối ưu KhachLink polling)
- Mỗi wave xong: `dotnet build VanAn.sln` pass + commit

### Session protocol
1. Mỗi session làm 1 wave
2. Bắt đầu session: đọc `project_state.md` + task card wave
3. Trước session end: build pass + commit
4. Commit format: `[ORDER-LIFECYCLE WAVE X] <short description>`

### Branch protocol
```
main ← feature/order-w-1-sync-mechanism-fix
main ← feature/order-w0-signalr-wiring
main ← feature/order-w1-kitchen-status-transition
main ← feature/order-w2-admin-orders-ui
main ← feature/order-w3-payment-confirm-ui
main ← feature/order-w4-khachlink-polling-optimize
main ← feature/order-w5-tests-sitemap
```

---

## 2. AUDIT FINDINGS SUMMARY

### 2.1. Gap Registry (7 gaps — investigated 2026-07-05)

| # | Gap | Layer | Severity | Wave fix |
|---|-----|-------|----------|----------|
| G1 | Không UI "Xác nhận đơn" trong ShopERP | UI | 🔴 Cao | W2 |
| G2 | Kitchen complete KHÔNG transition OrderStatus → Ready | Service | 🔴 Cao | W1 |
| G3 | OrderWorkflowService KHÔNG broadcast SignalR sau status change | Service | 🔴 Cao | W0 |
| G4 | KhachLink OrderTracking không kết nối SignalR | UI | 🟡 Thấp | W4 (giữ polling + tối ưu) |
| G5 | QrPaymentModal không gọi confirm payment sau khi khách thanh toán | UI | 🔴 Cao | W3 |
| G6 | Không UI ShopERP "Xác nhận nhận tiền" (manual) | UI | 🔴 Cao | W3 |
| G7 | ConfirmPayment không broadcast SignalR | Service | 🔴 Cao | W0 |
| S1 | `NatsSyncWorker` chỉ chạy với `--sync-worker` flag — mặc định không sync | Config | 🔴 Cao | W-1 |
| S2 | `SimpleOutboxProcessor` bị comment out trong DI | Config | 🟡 TB | W-1 |
| S3 | `OutboxRepository` inject `VanAnDbContext` (PostgreSQL) thay vì `IVanAnDbContext` — đọc Outbox sai DB | Service | 🔴 Cao | W-1 |
| S4 | Không có NATS subscriber consume events → write PostgreSQL (data sync missing) | Service | 🔴 Cao | W-1 |
| S5 | `SimpleAccountingEventHandler` không registered trong Program.cs — OrderCompleted không xử lý | Config | 🟡 TB | W-1 |

### 2.2. Existing Infrastructure (verified)

| Component | File | Status |
|-----------|------|--------|
| `OrderStatusId` domain (pending→confirmed→preparing→ready→completed) | `1_Shared/Domain.cs:422-433` | ✅ |
| `Order.ConfirmPayment(transactionId, paymentMethod)` | `1_Shared/Domain.cs:1038-1047` | ✅ Idempotent |
| `Order.UpdateOrderStatus(status)` | `1_Shared/Domain.cs:977-981` | ✅ |
| `Order.MarkAsCompleted()` | `1_Shared/Domain.cs:998-1002` | ✅ |
| `OrderService.ConfirmPaymentAsync` (mark paid + accounting entries) | `3_CoreHub/Services/OrderService.cs:550-586` | ✅ |
| `POST api/webhooks/payment` (Gateway) | `2_Gateway/Controllers/WebhookController.cs:115-154` | ✅ AllowAnonymous |
| `OrderHub` (Gateway) mapped `/orderHub` | `2_Gateway/Hubs/OrderHub.cs` + `Program.cs:325` | ✅ Basic (JoinShopGroup only) |
| `KitchenHub` (Gateway) mapped `/kitchenhub` | `2_Gateway/Hubs/KitchenHub.cs` + `Program.cs:326` | ✅ |
| `KitchenService.UpdateItemStatusAsync` (check all items completed) | `3_CoreHub/Services/KitchenService.cs:86-121` | ✅ But no OrderStatus transition |
| `OrderWorkflowService.TransitionStatusAsync` | `3_CoreHub/Services/OrderWorkflowService.cs:25-73` | ✅ But no SignalR |
| `PUT api/orders/{id}/status` (ShopERP) | `5_WebApps/ShopERP/Controllers/OrdersController.cs:66-106` | ✅ |
| `PUT api/orderworkflow/{id}/status` (ShopERP) | `5_WebApps/ShopERP/Controllers/OrderWorkflowController.cs:54-72` | ✅ |
| Kitchen UI (MVC cshtml + JS + SignalR) | `5_WebApps/ShopERP/Pages/Kitchen/Index.cshtml` | ✅ |
| KhachLink OrderTracking (HTTP polling 5s) | `5_WebApps/KhachLink/Pages/OrderTracking.razor` | ✅ Polling only |
| `IOrderHub` interface | `3_CoreHub/Interfaces/IOrderHub.cs` | ❌ Dead code — no implementation |
| ShopERP Orders page (`@page "/orders"`) | — | ❌ Does not exist |
| ShopERP OrderDetail page (`@page "/orders/{id}"`) | — | ❌ Does not exist |
| Sitemap link to `/orders` or `/kitchen` | `5_WebApps/ShopERP/Components/Pages/Sitemap.razor` | ❌ Missing |
| `NatsSyncWorker` (poll SQLite Outbox → NATS) | `3_CoreHub/Services/NatsSyncWorker.cs` | ⚠️ Code đầy đủ, chỉ chạy với `--sync-worker` flag |
| `SimpleOutboxProcessor` (backup processor) | `5_WebApps/ShopERP/Services/SimpleOutboxProcessor.cs` | ❌ Commented out in DI |
| `NatsEventPublisher` (publish to NATS) | `3_CoreHub/Infrastructure/Messaging/NatsEventPublisher.cs` | ✅ Registered (ShopERP + Gateway) |
| `OutboxRepository` (EF Core, map OutboxEvent↔OutboxMessage) | `3_CoreHub/Infrastructure/Messaging/OutboxRepository.cs` | ❌ Inject `VanAnDbContext` (PostgreSQL) — should be `IVanAnDbContext` |
| `PushNotificationBackgroundService` (subscribe NATS → push) | `3_CoreHub/Services/PushNotificationBackgroundService.cs` | ✅ Registered (ShopERP line 205) |
| `SimpleAccountingEventHandler` (subscribe NATS → accounting) | `3_CoreHub/Services/Events/SimpleAccountingEventHandler.cs` | ❌ Not registered in any Program.cs |
| `OrderWorkflowService.PublishOrderStatusChangedEventAsync` | `3_CoreHub/Services/OrderWorkflowService.cs:217-248` | ✅ Publishes `order.status.changed` to NATS |
| ADR-001 Station Architecture doc | `docs/Architecture/ADR001-Station-Architecture.md` | ✅ Design doc — v2 edge sync strategy |

### 2.3. Architecture Decision: Hybrid Realtime

| Nhóm | Giải pháp | Lý do |
|------|-----------|-------|
| **KhachLink (500-1000 khách)** | HTTP Polling + tối ưu (adaptive interval + ETag) | VPS 2GB RAM, không cần <1s latency, 200 RPS acceptable |
| **ShopERP Staff (5-20 người)** | SignalR OrderHub + KitchenHub | Cần real-time <100ms, 20 connection × 200KB = ~4MB RAM |

**Lý do KHÔNG dùng SignalR cho KhachLink:**
- 1000 WebSocket persistent connections × ~200KB = ~200MB RAM
- + Blazor Server circuit overhead = ~300MB total
- VPS 2GB RAM: risky (Gateway ~200MB + DB ~200MB + 300MB = ~700MB)
- HTTP polling 5s: độ trễ chấp nhận được cho order tracking (không phải chat)

---

## 3. SCOPE DECISIONS (APPROVED 2026-07-05)

| # | Quyết định | Lựa chọn |
|---|-------------|----------|
| D1 | Realtime architecture | Hybrid: Polling (KhachLink) + SignalR (ShopERP staff) |
| D2 | Payment confirm flow | Option B: Manual confirm — Admin ShopERP "Xác nhận nhận tiền" + KhachLink "Tôi đã thanh toán" (cả 2 tự confirm qua `POST api/webhooks/payment`) |
| D3 | Kitchen → OrderStatus | Auto-transition to `Ready` khi all items completed |
| D4 | SignalR broadcast scope | ShopERP staff only (OrderHub) — KhachLink dùng polling |
| D5 | Polling optimization | Adaptive interval (5s pending → 15s confirmed → stop completed) + tab visibility check |
| D6 | IOrderHub interface | Implement concrete class in Gateway (not CoreHub — needs IHubContext) |
| D7 | Domain modification | KHÔNG — chỉ dùng existing `Order.UpdateOrderStatus` + `ConfirmPayment` |
| D8 | Sync mechanism (ADR-001 v2) | Fix S1-S5 trước W0 — Outbox → NATS → PostgreSQL sync phải hoạt động trước khi thêm SignalR |
| D9 | OutboxRepository DbContext | Đổi `VanAnDbContext` → `IVanAnDbContext` (ShopERP resolves SQLite, Gateway resolves PostgreSQL) |
| D10 | NatsSyncWorker activation | Mặc định chạy (không cần `--sync-worker` flag) — hoặc config flag `Sync__Enabled=true` |
| D11 | DataSyncSubscriber | Tạo mới BackgroundService subscribe NATS → write PostgreSQL (Gateway scope) |

---

## 4. WAVE OVERVIEW (7 waves)

| Wave | Tên | Mode | Domain? | Task Card | Gaps fixed | Status |
|------|-----|------|---------|-----------|------------|--------|
| W-1 | Sync Mechanism Fix (Outbox → NATS → PostgreSQL) | IMPLEMENT | ❌ | `order_w-1_task_card.md` | S1, S2, S3, S4, S5 | ✅ Done |
| W0 | SignalR Wiring (OrderHub broadcast) | IMPLEMENT | ❌ | `order_w0_task_card.md` | G3, G7 | ✅ Done |
| W1 | Kitchen → OrderStatus Transition | IMPLEMENT | ❌ | `order_w1_task_card.md` | G2 | ✅ Done |
| W2 | Admin Orders UI (list + confirm + detail) | IMPLEMENT | ❌ | `order_w2_task_card.md` | G1 | ✅ Done |
| W3 | Payment Confirm UI (Admin + KhachLink) | IMPLEMENT | ❌ | `order_w3_task_card.md` | G5, G6 | ✅ Done |
| W4 | KhachLink Polling Optimize (adaptive) | IMPLEMENT | ❌ | `order_w4_task_card.md` | G4 | ✅ Done |
| W5 | Tests + Sitemap links | IMPLEMENT | ❌ | `order_w5_task_card.md` | — | ✅ Done |

### Wave dependency graph:
```
W-1 (Sync fix) ──→ W0 (SignalR wiring) ─┬─→ W1 (Kitchen → Ready) ──→ W5 (Tests)
                                        ├─→ W2 (Admin Orders UI) ──→ W3 (Payment UI) ──→ W5
                                        └─→ W4 (Polling optimize) ──────────────────────→ W5
```
- **W-1 là nền tảng tuyệt đối** — Outbox → NATS → PostgreSQL sync phải hoạt động trước mọi thứ khác
- W0 phụ thuộc W-1 (SignalR broadcast cần data đã sync)
- W1 + W2 + W4 có thể song song sau W0
- W3 phụ thuộc W2 (cùng trang Orders, thêm nút confirm payment)
- W5 cuối cùng: tests + sitemap

---

## 5. WAVE DETAILS

### W-1: Sync Mechanism Fix (Outbox → NATS → PostgreSQL)
**Gaps fixed:** S1 (NatsSyncWorker flag), S2 (SimpleOutboxProcessor commented), S3 (OutboxRepository wrong DbContext), S4 (missing DataSyncSubscriber), S5 (AccountingEventHandler not registered)

**Mục tiêu:** Kích hoạt cơ chế đồng bộ SQLite (ShopERP) → PostgreSQL (Gateway/CoreHub) qua Outbox Pattern + NATS. Đây là nền tảng tuyệt đối — mọi wave sau (W0-W5) phụ thuộc data đã sync.

**Architecture (ADR-001 v2 — verified):**
```
ShopERP SQLite
  │ 1. Order created/updated → OutboxMessages table (SQLite)
  ↓ NatsSyncWorker (poll every 1s)
  │ 2. Publish to NATS "vanan.shoperp.{eventType}"
  ↓ NATS Broker
  │ 3. DataSyncSubscriber (NEW) consume → write PostgreSQL
  │    SimpleAccountingEventHandler consume → create accounting entries
  │    PushNotificationBackgroundService consume → push notification (already works)
  ↓ PostgreSQL (CoreHub)
  │ 4. Data persisted — Gateway reads from PostgreSQL
```

**Changes:**
1. **S3 fix:** `OutboxRepository` — đổi `VanAnDbContext` → `IVanAnDbContext` (ShopERP resolves SQLite, Gateway resolves PostgreSQL)
2. **S1 fix:** `NatsSyncWorker` — chạy mặc định (không cần `--sync-worker` flag), hoặc dùng config `Sync__Enabled=true`
3. **S2 fix:** Uncomment `SimpleOutboxProcessor` HOẶC xóa (chọn 1 processor — khuyến nghị xóa, NatsSyncWorker đủ)
4. **S4 fix:** Tạo `DataSyncSubscriber` BackgroundService — subscribe NATS subjects → write PostgreSQL (Gateway scope)
5. **S5 fix:** Register `SimpleAccountingEventHandler` trong Gateway Program.cs
6. **OrderWorkflowService:** Cần enqueue events vào Outbox table (hiện chỉ log, không enqueue)

**Note:** `OrderWorkflowService.RecordOrderCompletedEvent` (line 96-124) hiện chỉ `_logger.LogInformation` — cần đổi thành `_outboxRepository.EnqueueAsync()` để thực sự ghi vào Outbox table.

### W0: SignalR Wiring (OrderHub broadcast)
**Gaps fixed:** G3 (OrderWorkflowService không broadcast), G7 (ConfirmPayment không broadcast)

**Mục tiêu:** Khi order status thay đổi (confirm, kitchen complete, payment confirm), OrderHub broadcast event cho ShopERP staff real-time.

**Changes:**
1. `OrderWorkflowService.cs` — inject `IHubContext<OrderHub>`, gọi `SendAsync("OrderStatusChanged", ...)` sau `TransitionStatusAsync`
2. `OrderService.cs` — inject `IHubContext<OrderHub>` (via Gateway, không phải CoreHub), gọi `SendAsync("PaymentConfirmed", ...)` sau `ConfirmPaymentAsync`
3. `KitchenController.cs` — sau `UpdateItemStatus`, broadcast `OrderHub.SendAsync("KitchenItemCompleted", ...)` (ngoài KitchenHub)
4. `OrderHub.cs` — thêm method `JoinOrderGroup(orderId)` cho staff subscribe 1 order cụ thể

**Note:** CoreHub là class library, không reference SignalR. Cần inject `IHubContext<OrderHub>` ở Gateway controller level, hoặc tạo interface `IOrderNotificationService` trong CoreHub, implement trong Gateway.

### W1: Kitchen → OrderStatus Transition
**Gap fixed:** G2 (Kitchen complete không transition OrderStatus → Ready)

**Mục tiêu:** Khi tất cả OrderItems trong 1 order đạt `KitchenStatus.Completed`, tự động transition `OrderStatus` → `Ready`.

**Changes:**
1. `KitchenService.UpdateItemStatusAsync` — sau khi check all items completed, gọi `order.UpdateOrderStatus(OrderStatusId.Ready)` (ngoài `MarkAsCompleted`)
2. Cần inject `IOrderRepository` vào `KitchenService` (hoặc dùng existing `IVanAnDbContext`)
3. Broadcast `OrderHub.SendAsync("OrderStatusChanged", {orderId, status: "ready"})` qua W0 wiring

**Lưu ý:** `MarkAsCompleted` set `CompletedAt` — cần review xem có nên tách `Ready` (kitchen xong) vs `Completed` (khách nhận hàng) không. Hiện tại `MarkAsCompleted` được gọi khi all items done — có thể cần đổi thành `UpdateOrderStatus(Ready)` thay vì `MarkAsCompleted`.

### W2: Admin Orders UI (list + confirm + detail)
**Gap fixed:** G1 (Không UI "Xác nhận đơn")

**Mục tiêu:** Tạo trang `/orders` (list) + `/orders/{id}` (detail) trong ShopERP, có nút "Xác nhận đơn" (confirm → status=confirmed).

**Changes:**
1. Tạo `Components/Pages/Orders/Index.razor` (`@page "/orders"`) — list orders với filter theo status, nút "Xác nhận" cho mỗi order pending
2. Tạo `Components/Pages/Orders/Detail.razor` (`@page "/orders/{orderId:guid}"`) — chi tiết 1 order + timeline + nút confirm
3. Nút "Xác nhận" gọi `PUT api/orders/{id}/status` với body `{status: "confirmed"}`
4. Subscribe OrderHub `OrderStatusChanged` event để real-time update list
5. Sử dụng UI Platform components (VanAnTable, VanAnButton, VanAnCard) — KHÔNG custom HTML

### W3: Payment Confirm UI (Admin + KhachLink)
**Gaps fixed:** G5 (QrPaymentModal không confirm), G6 (Không UI Admin confirm payment)

**Mục tiêu:**
- Admin ShopERP: nút "Xác nhận đã nhận tiền" trong trang Order Detail → gọi `POST api/webhooks/payment`
- KhachLink: nút "Tôi đã thanh toán" trong QrPaymentModal → gọi `POST api/webhooks/payment` (self-confirm — KhachLink CÓ TenantId từ Order)

**TenantId flow (verified):**
```
Product.TenantId (1_Shared/Domain.cs:573) → ProductDto.TenantId (KhachLink/Models/ProductDto.cs:6)
  → Cart (CartItem không có TenantId — gap nhỏ, nhưng Order có)
  → POST api/orders → Order.TenantId (set bởi OrderService từ product tenant)
  → Checkout.razor captures response.TenantId → truyền cho QrPaymentModal
  → QrPaymentModal gửi {OrderId, TenantId, TransactionId} → POST api/webhooks/payment
```

**Changes:**
1. `Orders/Detail.razor` (W2) — thêm nút "Xác nhận đã nhận tiền" (chỉ hiện khi `PaymentStatus == "Pending"`)
2. `QrPaymentModal.razor` — thêm nút "Tôi đã thanh toán" → POST `api/webhooks/payment` với `{orderId, tenantId, transactionId: manual}`
3. Sau confirm → OrderHub broadcast `PaymentConfirmed` (W0 wiring) → ShopERP Dashboard real-time update

**Note:** Option B (manual) — admin xác nhận sau khi kiểm tra tài khoản ngân hàng. Webhook tự động từ bank là tương lai (cần integration bank API).

### W4: KhachLink Polling Optimize
**Gap fixed:** G4 (tối ưu polling thay vì SignalR)

**Mục tiêu:** Giảm 60-70% polling request bằng adaptive interval + tab visibility.

**Changes:**
1. `OrderTracking.razor` — adaptive poll interval:
   - `pending` → 5s (khách đang chờ, cần update nhanh)
   - `confirmed` / `preparing` → 10s (đang xử lý, ít thay đổi)
   - `ready` → 5s (sắp nhận hàng, cần update nhanh)
   - `completed` / `delivered` → STOP polling (đơn xong)
2. Tab visibility: pause polling khi `document.visibilityState === 'hidden'` (đã có)
3. ETag support (optional — nếu time permits): server trả ETag header, client gửi `If-None-Match`, server trả 304 nếu không đổi

### W5: Tests + Sitemap
**Mục tiêu:** Tests cho toàn bộ flow + thêm link vào Sitemap.

**Changes:**
1. Sitemap.razor — thêm link "Danh sách đơn hàng" → `/orders` + "Màn hình bếp" → `/Kitchen` (existing MVC page)
2. Unit tests: `OrderWorkflowService` broadcast SignalR after status change
3. Unit tests: `KitchenService` transitions OrderStatus to Ready when all items completed
4. Integration tests: `POST api/webhooks/payment` → order PaymentStatus = "Paid" + accounting entries
5. bUnit tests: Orders/Index.razor renders list + confirm button works
6. bUnit tests: QrPaymentModal "Tôi đã thanh toán" calls API

---

## 6. RISK REGISTER

| # | Risk | Mitigation | Wave |
|---|------|------------|------|
| R1 | CoreHub không reference SignalR → không inject `IHubContext<OrderHub>` | Tạo `IOrderNotificationService` interface trong CoreHub, implement trong Gateway | W0 |
| R2 | `KitchenService` dùng `IVanAnDbContext` trực tiếp, không có `IOrderRepository` | Dùng `_context.Orders.FirstOrDefaultAsync()` trực tiếp (đã có pattern) | W1 |
| R3 | `MarkAsCompleted` vs `UpdateOrderStatus(Ready)` semantic conflict | Review: `Ready` = kitchen xong chờ giao, `Completed` = khách nhận. Tách 2 trạng thái | W1 |
| R4 | ShopERP Orders page cần auth + tenant filtering | Dùng `[Authorize]` + `GetTenantId()` pattern existing | W2 |
| R5 | UI Platform compliance — Orders page phải dùng VanAn components | Reference `docs/UI_Platform_Implementation_Guide.md` | W2 |
| ~~R6~~ | ~~KhachLink không có TenantId~~ → **RESOLVED:** Order có TenantId (từ Product), KhachLink có `order.TenantId.Value` | KhachLink "Tôi đã thanh toán" CÓ THỂ tự confirm payment (gửi OrderId + TenantId + TransactionId) | W3 |
| R7 | Polling adaptive interval logic phức tạp | Giữ đơn giản: switch theo status, không dynamic backoff | W4 |
| R8 | SignalR OrderHub broadcast cho ALL clients (không group) | Thêm `JoinShopGroup(shopId)` + broadcast theo group (đã có trong OrderHub) | W0 |
| R9 | `OutboxRepository` đổi DbContext có thể break Gateway (PostgreSQL) | `IVanAnDbContext` resolves đúng theo scope — Gateway=PostgreSQL, ShopERP=SQLite | W-1 |
| R10 | `DataSyncSubscriber` write PostgreSQL cần inject `VanAnDbContext` (PostgreSQL) | Gateway scope đã có `VanAnDbContext` registered — subscriber dùng scope riêng | W-1 |
| R11 | `NatsSyncWorker` chạy mặc định có thể ảnh hưởng dev mode (NATS không có) | Graceful degradation — `NatsEventPublisher` đã log warning + skip khi NATS unavailable | W-1 |
| R12 | `OrderWorkflowService.RecordOrderCompletedEvent` chỉ log, không enqueue Outbox | Cần inject `IOutboxRepository` + gọi `EnqueueAsync` — nhưng phải trong cùng transaction | W-1 |
| R13 | `SimpleOutboxProcessor` vs `NatsSyncWorker` — 2 processor cùng chạy = duplicate publish | Chọn 1: giữ `NatsSyncWorker` (configurable), xóa/comment `SimpleOutboxProcessor` | W-1 |
| R14 | `OutboxRepository.ToMessage` hardcode `invoiceId` — không generic cho Order events | Cần generalize: serialize toàn bộ `EventData` thay vì wrap trong `{invoiceId, originalData}` | W-1 |

---

## 7. SUCCESS CRITERIA

**End-to-end flow (sau tất cả waves):**
- ✅ **W-1:** Order tạo ở ShopERP (SQLite) → Outbox → NATS → DataSyncSubscriber → PostgreSQL (sync hoạt động)
- ✅ **W-1:** OrderCompleted events → SimpleAccountingEventHandler → accounting entries tự động
- ✅ Khách đặt hàng → Admin thấy real-time trên Dashboard (OrderHub.NewOrderReceived — đã có)
- ✅ Admin bấm "Xác nhận đơn" → status=confirmed → OrderHub broadcast → Dashboard update real-time
- ✅ Kitchen bấm "Hoàn thành" → all items completed → OrderStatus=Ready → OrderHub broadcast
- ✅ Admin bấm "Xác nhận đã nhận tiền" → PaymentStatus=Paid → accounting entries → OrderHub broadcast
- ✅ KhachLink OrderTracking: polling adaptive → khách thấy status update (5-10s delay, acceptable)
- ✅ ShopERP staff: SignalR real-time (<100ms delay)
- ✅ Build 0 errors, all tests pass
- ✅ UI Platform components used (no custom HTML)
- ✅ Sitemap có link Orders + Kitchen

**Non-goals (out of scope):**
- ❌ SignalR cho KhachLink (giữ HTTP polling)
- ❌ Bank webhook tự động (Option B manual only — bank integration là phase sau)
- ❌ Web Push notifications (đã có task card riêng `wave4_order_status_realtime_task_card.md`)
- ❌ Domain modification (dùng existing methods)
- ❌ KhachLink SQLite local DB (ADR-001 v2 defer — KhachLink dùng HTTP via Gateway)
- ❌ docker-compose.edge.yml creation (deployment config — separate task)

---

## 8. REFERENCES

- **Governance:** `.devin/rules/governance.md`
- **Workflow:** `.devin/workflows/newfeaturebuild.md`
- **UI Platform:** `docs/UI_Platform_Implementation_Guide.md`
- **Existing Kitchen task card:** `docs/AI/tasks/wave4_order_status_realtime_task_card.md`
- **ADR-001 Station Architecture:** `docs/Architecture/ADR001-Station-Architecture.md`
- **Outbox config fix plan:** `docs/MVP_Product/architectural-fix-outboxmessage-configuration-plan.md`
- **VAS master plan (format reference):** `docs/AI/tasks/vas_enterprise_reports_master_plan.md`
- **Task cards:** `docs/AI/tasks/order_w{-1..5}_task_card.md`
