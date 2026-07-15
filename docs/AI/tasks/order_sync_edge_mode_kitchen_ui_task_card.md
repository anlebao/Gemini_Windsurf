# TASK CARD — Phase E: Edge Mode Sync Root Cause Fix + Kitchen UI

> **Master plan:** `docs/AI/tasks/order_sync_saas_edge_master_plan.md` (Section 3 Phase E)
> **Branch:** `feature/order-sync-edge-mode-kitchen`
> **Priority:** 2 (After Phase S merged — depends on `Sync__EdgeMode` flag infrastructure)
> **Mode:** IMPLEMENT (user approval granted 2026-07-15)
> **Prerequisite:** Phase S merged to `main`, `Sync__EdgeMode` flag infrastructure in place
> **Estimated tasks:** 16 (split into Track E1: sync fix, Track E2: kitchen UI)

---

## 0. CONTEXT & DECISIONS (locked)

### Architecture decision (locked 2026-07-15)
- **Edge Mode = 2-way sync PG↔SQLite qua NATS.**
- **Subject separation (RC-2 fix):**
  - `vanan.shoperp.*` — events ORIGINATING from ShopERP/Edge (SQLite→PG) — Gateway `DataSyncSubscriber` listens
  - `vanan.cloud.*` — events ORIGINATING from Gateway/Cloud (PG→SQLite) — ShopERP `OrderSyncSubscriber` listens
- **Atomic Outbox (RC-1 fix):** Order + Outbox enqueue trong 1 DB transaction. KHÔNG 2 `SaveChangesAsync` riêng.
- **OrderItem full payload (RC-3 fix):** Truyền đủ `ProductName`, `VatRate`, `TotalAmount` từ event payload.
- **Edge UI = offline-first:** Đọc SQLite trực tiếp (KHÔNG qua Gateway HTTP — Server A có thể mất mạng Server B).

### Edge Mode data flow (target)
```
Server A (Edge - offline-first)         Server B (Central - online)
┌─────────────────────────┐             ┌─────────────────────────┐
│ ShopERP UI              │             │ KhachLink               │
│   POS /pos (NEW)        │             │   Checkout → Gateway    │
│   Kitchen /kitchen (NEW)│             │                         │
│   Orders /orders        │             │                         │
│        ↓                │             │        ↓                │
│   SQLite (local SSoT)   │             │   PostgreSQL (cloud SSoT)│
│        ↓                │             │        ↓                │
│   Outbox (SQLite)       │             │   Outbox (PostgreSQL)   │
│        ↓                │             │        ↓                │
│   NatsSyncWorker        │             │   NatsSyncWorker         │
│   subject:              │             │   subject:               │
│   vanan.shoperp.*       │             │   vanan.cloud.*          │
│        ↓                │             │        ↓                │
│   NATS ──────────────────┼─────────────┼─→ NATS                  │
│        ↓                │             │        ↓                │
│   OrderSyncSubscriber   │     ←───────┤   DataSyncSubscriber    │
│   (PG→SQLite)           │             │   (SQLite→PG)           │
└─────────────────────────┘             └─────────────────────────┘
```

### Verified facts (2026-07-15)
- `OrderRepository.AddAsync` (`3_CoreHub/Repositories/OrderRepository.cs:107-114`) gọi `SaveChangesAsync` → order committed trước Outbox enqueue → RC-1
- `OrderService.CreateOrderFromCommandAsync:616` — `_ = _outboxRepository.EnqueueAsync(outboxEvent)` (fire-and-forget, không await) + `SaveChangesAsync` thứ 2 → RC-1
- `NatsSyncWorker.BuildSubject:116-120` — `vanan.shoperp.{eventType}` cho cả 2 hướng → RC-2
- `OrderSyncSubscriber:58-67` — subscribe `vanan.shoperp.order.created` (cùng subject Gateway publish) → RC-2 collision
- `OrderSyncSubscriber:119-126` — drop `ProductName`, `VatRate`, `TotalAmount` → RC-3
- `DataSyncSubscriber.SyncOrderCreatedAsync:240-245` — stub, chỉ log → cần hoàn thiện
- Order status workflow (`Domain.cs:425-432`): Pending → Confirmed → Preparing → Ready → Delivering → Completed
- Existing kitchen transitions ở `Orders/Detail.razor:72-115` (per-order buttons, gated by `Kitchen_Workflow_Enabled`)
- **KHÔNG có Kitchen Display page** (Sitemap.razor:43-44 comment: "not yet built")
- ShopERP `OrdersController.cs:162-173` `POST /api/orders` — staff tạo empty order (no items) → cần POS UI input

### User decisions (locked 2026-07-15)
- **Edge UI layout full flow:** Form nhập order → payment → bếp tiếp nhận → chế biến → sẵn sàng → trả đơn cho khách
- **UI Platform components BẮT BUỘC** — VanAnButton, VanAnCard, VanAForm, VanAnDataGrid, VanAnModal

---

## 1. TASKS — Track E1: Sync Root Cause Fix

| # | Task ID | Task | Files | Status |
|---|---------|------|-------|--------|
| 1 | E-T1 | **VERIFY-REPRO Edge:** `docker compose -f docker-compose.edge.yml up -d`. Chụp baseline: (a) PostgreSQL `Orders` count, (b) SQLite Orders count (exec trong shoperp container), (c) `OutboxMessages` count cả 2 DB. Tạo order qua `POST http://localhost:5001/api/public/orders/checkout`. Đợi 10s. Assert: SQLite count KHÔNG tăng (sync broken). Record evidence. | Terminal only | ⬜ |
| 2 | E-T2 | **RC-1 Atomic Outbox.** Refactor `OrderService.CreateOrderFromCommandAsync:531-641`:<br>(a) Audit callers của `OrderRepository.AddAsync` — grep `\.AddAsync\(` trong order context. Report impact.<br>(b) Option preferred: thêm `AddAsyncNoSave(Order, CancellationToken)` vào `IOrderRepository` + `OrderRepository` (KHÔNG gọi SaveChangesAsync). Caller (`OrderService`) owns Unit of Work.<br>(c) Refactor `CreateOrderFromCommandAsync`: `BeginTransactionAsync` → `AddAsyncNoSave(order)` → `EnqueueAsync(outboxEvent)` → `SaveChangesAsync` (1 lần, commit cả order + outbox) → `CommitAsync`. Wrap trong try/catch với `RollbackAsync`.<br>(d) **HARD STOP:** Nếu phải sửa public API `AddAsync` thay vì thêm method mới → STOP, ask user approval (governance: "Do not change public API unless explicitly approved"). | `3_CoreHub/Repositories/IOrderRepository.cs`, `3_CoreHub/Repositories/OrderRepository.cs`, `3_CoreHub/Services/OrderService.cs` | ⬜ |
| 3 | E-T3 | **RC-2 Subject Namespace.** Tách 2 hướng:<br>(a) `NatsSyncWorker.BuildSubject` (`3_CoreHub/Services/NatsSyncWorker.cs:116-120`) — thêm param `string prefix = "shoperp"`. Subject = `vanan.{prefix}.{eventType}`.<br>(b) Gateway `NatsSyncWorker` registration (`Program.cs:301`) — khi publish PG→SQLite, truyền `prefix="cloud"`. **Cách:** thêm config `Sync__SubjectPrefix` (default `shoperp`, Gateway Edge Mode set `cloud`). Hoặc inject `IOptions<SyncOptions>`.<br>(c) `OrderSyncSubscriber` (`5_WebApps/ShopERP/Services/OrderSyncSubscriber.cs:58-67`) — đổi subscription thành `vanan.cloud.order.created` + `vanan.cloud.order.statuschanged`.<br>(d) `DataSyncSubscriber` (`2_Gateway/Services/DataSyncSubscriber.cs:63`) — giữ `vanan.shoperp.>` (SQLite→PG direction).<br>(e) `OrderWorkflowService.RecordOrderCompletedEvent` + `NatsSyncWorker` (ShopERP) — giữ prefix `shoperp` (default). | `3_CoreHub/Services/NatsSyncWorker.cs`, `5_WebApps/ShopERP/Services/OrderSyncSubscriber.cs`, `2_Gateway/Program.cs`, `2_Gateway/Services/DataSyncSubscriber.cs` (verify only) | ⬜ |
| 4 | E-T4 | **RC-3 OrderItem Full Payload.**<br>(a) Inspect `OrderItem` domain entity (`1_Shared/Domain.cs` — search `class OrderItem`). Verify có field `ProductName`, `VatRate`, `TotalAmount` + cách set.<br>(b) Nếu `OrderItem` đã có setter/factory cho 3 field này → dùng. Nếu KHÔNG → report Domain Modeling Defect, ask user approval trước khi sửa Domain (governance: Domain Protection).<br>(c) `OrderSyncSubscriber.SyncOrderCreatedAsync:115-127` — parse thêm `ProductName`, `VatRate`, `TotalAmount` từ item payload + truyền vào factory/setter.<br>(d) Verify payload từ `OrderService.CreateOrderFromCommandAsync:598-607` đã có 3 field này (đã verify: có). | `5_WebApps/ShopERP/Services/OrderSyncSubscriber.cs`, có thể `1_Shared/Domain.cs` (if defect) | ⬜ |
| 5 | E-T5 | **Hoàn thiện `DataSyncSubscriber.SyncOrderCreatedAsync`** (`2_Gateway/Services/DataSyncSubscriber.cs:223-245`). Hiện là stub. Implement full upsert:<br>(a) Parse full order payload (OrderId, TenantId, Items, CustomerInfo, Status, amounts).<br>(b) Tạo `Order.Create(orderId, tenantIdObj, customerId, items)` + set customer info + status.<br>(c) `dbContext.Orders.Add` + `SaveChangesAsync` (PostgreSQL).<br>(d) Idempotent: check `AnyAsync(o => o.Id == orderId)` trước.<br>(e) Pattern reference: `OrderSyncSubscriber.SyncOrderCreatedAsync:99-167` (mirror logic). | `2_Gateway/Services/DataSyncSubscriber.cs` | ⬜ |
| 6 | E-T6 | Set `Sync__EdgeMode=true` explicit trong `docker-compose.edge.yml` (ShopERP + Gateway env). Verify `docker-compose.prod.yml` vẫn `false` (từ Phase S). | `docker-compose.edge.yml` | ⬜ |
| 7 | E-T7 | **VERIFY-FIX Edge:** Re-run E-T1 scenario. **Expected:** (a) PostgreSQL `Orders` +1, (b) SQLite `Orders` +1 sau ~5s, (c) SQLite order có `ProductName` + `VatRate` đúng (RC-3), (d) `OutboxMessages` PostgreSQL có 1 row status=Processed. Record evidence. | Terminal only | ⬜ |
| 8 | E-T8 | **BUILD + COMMIT E1:** `guard-check.ps1` + `dotnet build VanAn.sln` 0 errors. Commit: `[SYNC-EDGE] Fix RC-1/2/3 atomic outbox + subject separation + full payload` | Solution-wide | ⬜ |

---

## 2. TASKS — Track E2: Edge Kitchen UI

| # | Task ID | Task | Files | Status |
|---|---------|------|-------|--------|
| 9 | E-T9 | **UI: POS Order Input Page `/pos`.** Tạo `5_WebApps/ShopERP/Components/Pages/POS/Create.razor`:<br>- `@page "/pos"` + `@rendermode InteractiveServer`<br>- VanAForm với: product picker (VanAnDataGrid list products từ SQLite — gọi `IProductRepository` hoặc `IVanAnDbContext.Products`), quantity input, customer info (name/phone/address optional), payment method (cash/QR select), table number (optional)<br>- "Tạo đơn" button → `OrderService.CreateOrderAsync` (lưu SQLite trực tiếp, KHÔNG qua Gateway HTTP — Edge offline-first)<br>- Sau tạo → redirect `/pos/payment/{orderId}`<br>- **Pattern reference:** `KhachLink/Pages/Checkout.razor` (customer-facing, tương tự nhưng Edge dùng SQLite trực tiếp) | `5_WebApps/ShopERP/Components/Pages/POS/Create.razor` (NEW) | ⬜ |
| 10 | E-T10 | **UI: Payment Page `/pos/payment/{orderId}`.** Tạo `5_WebApps/ShopERP/Components/Pages/POS/Payment.razor`:<br>- Hiển thị order summary (items, total, VAT)<br>- Cash: button "Xác nhận thanh toán" → `OrderService.UpdateOrderStatusAsync(orderId, "confirmed")` (skip payment gateway)<br>- QR: generate VietQR via existing `IShopQrCodeService` hoặc VietQR API, hiển thị QR image, poll payment status (reuse existing payment webhook logic — nhưng Edge offline nên cash primary)<br>- Sau confirm → redirect `/kitchen` (bếp tiếp nhận) | `5_WebApps/ShopERP/Components/Pages/POS/Payment.razor` (NEW) | ⬜ |
| 11 | E-T11 | **UI: Kitchen Display Page `/kitchen`.** Tạo `5_WebApps/ShopERP/Components/Pages/Kitchen/Display.razor`:<br>- `@page "/kitchen"` + `@rendermode InteractiveServer`<br>- 3 column layout (CSS Grid — VanAnCard per column):<br>  - Column 1 "Chờ tiếp nhận" (status=confirmed) — đỏ<br>  - Column 2 "Đang chế biến" (status=preparing) — vàng<br>  - Column 3 "Sẵn sàng" (status=ready) — xanh<br>- Mỗi card: order tracking code, items list, time elapsed, transition button<br>- Real-time: SignalR `OrderHub` (đã có ở Gateway) hoặc polling 5s (simpler cho Edge)<br>- **Gated by `Kitchen_Workflow_Enabled` toggle** — nếu OFF, redirect `/orders` | `5_WebApps/ShopERP/Components/Pages/Kitchen/Display.razor` (NEW) | ⬜ |
| 12 | E-T12 | **UI: Status Transition Buttons.** Trong Kitchen Display, mỗi order card có buttons:<br>- "Tiếp nhận" (confirmed → preparing) — gọi `OrderWorkflowService.TransitionStatusAsync` qua `shoperp/api/orderworkflow/{id}/status` (ShopERP local API, KHÔNG qua Gateway)<br>- "Hoàn thành" (preparing → ready)<br>- "Trả đơn" (ready → delivering → completed — 2 step hoặc 1 step tùy `Delivering` enable config)<br>- **Pattern reference:** `Orders/Detail.razor:72-115` (existing per-order buttons, reuse transition logic) | `5_WebApps/ShopERP/Components/Pages/Kitchen/Display.razor` | ⬜ |
| 13 | E-T13 | **UI: "Trả đơn cho khách" Flow.** Khi staff bấm "Trả đơn":<br>- Status: ready → delivering → completed (2 transitions)<br>- `completed` trigger: `OrderWorkflowService.HandleOrderCompletedAsync` → `RecordOrderCompletedEvent` (Outbox) → NATS → accounting entries (Edge path)<br>- Hiển thị success toast + remove card khỏi "Sẵn sàng" column<br>- **Lưu ý:** `OrderWorkflowService` đã có full logic (line 101-120) — chỉ cần gọi đúng endpoint | `5_WebApps/ShopERP/Components/Pages/Kitchen/Display.razor` | ⬜ |
| 14 | E-T14 | **NavMenu + Sitemap Integration.**<br>- `5_WebApps/ShopERP/Components/Layout/NavMenu.razor` — thêm menu items: "POS" (`/pos`), "Bếp" (`/kitchen`)<br>- `5_WebApps/ShopERP/Components/Pages/Sitemap.razor:43-44` — gỡ comment "not yet built", ensure link `/Kitchen` hoạt động<br>- Edge Mode only: gate NavMenu items bằng `Sync__EdgeMode` flag (hoặc feature toggle) — SaaS Mode ẩn POS/Kitchen (SaaS dùng KhachLink cho customer orders, không cần POS) | `5_WebApps/ShopERP/Components/Layout/NavMenu.razor`, `5_WebApps/ShopERP/Components/Pages/Sitemap.razor` | ⬜ |
| 15 | E-T15 | **VERIFY Edge UI Flow:** `docker compose -f docker-compose.edge.yml up` + manual flow:<br>(a) Mở `/pos` → chọn product → nhập qty → tạo đơn<br>(b) `/pos/payment/{id}` → cash payment → confirm<br>(c) `/kitchen` → order xuất hiện ở column "Chờ tiếp nhận"<br>(d) Bấm "Tiếp nhận" → order sang "Đang chế biến"<br>(e) Bấm "Hoàn thành" → order sang "Sẵn sàng"<br>(f) Bấm "Trả đơn" → order completed + biến khỏi display<br>(g) Assert SQLite: order status = `completed`, `OutboxMessages` có 1 row OrderCompleted<br>Record evidence (screenshot hoặc log). | Terminal + browser | ⬜ |
| 16 | E-T16 | **BUILD + COMMIT E2:** `guard-check.ps1` + `dotnet build VanAn.sln` 0 errors. Update `project_state.md` Section 6: "[2026-07-15] ORDER SYNC EDGE MODE + KITCHEN UI COMPLETE". Commit: `[SYNC-EDGE] Kitchen UI POS + Display + transitions` | Solution-wide | ⬜ |

---

## 3. EXIT CRITERIA

### Track E1 (Sync Fix)
- [ ] E-T1 evidence: SQLite Orders count KHÔNG tăng sau checkout (reproduce broken)
- [ ] Order + Outbox atomic — 1 transaction, `SaveChangesAsync` 1 lần
- [ ] Subject namespace tách: `vanan.cloud.*` (PG→SQLite) vs `vanan.shoperp.*` (SQLite→PG)
- [ ] `OrderSyncSubscriber` subscribe `vanan.cloud.order.created` + `vanan.cloud.order.statuschanged`
- [ ] `OrderSyncSubscriber` parse đủ `ProductName`, `VatRate`, `TotalAmount`
- [ ] `DataSyncSubscriber.SyncOrderCreatedAsync` full upsert (không còn stub)
- [ ] E-T7 evidence: SQLite Orders +1 sau checkout, với đúng ProductName + VatRate
- [ ] Build 0 errors

### Track E2 (Kitchen UI)
- [ ] Page `/pos` tồn tại, dùng VanAForm + VanAnDataGrid
- [ ] Page `/pos/payment/{orderId}` tồn tại, cash + QR payment
- [ ] Page `/kitchen` tồn tại, 3-column layout (VanAnCard), real-time hoặc polling 5s
- [ ] Status transitions: confirmed → preparing → ready → delivering → completed
- [ ] NavMenu + Sitemap có link POS + Kitchen
- [ ] E-T15 evidence: full flow PASS (POS → payment → kitchen → complete)
- [ ] Build 0 errors

### Overall
- [ ] `Sync__EdgeMode=true` trong `docker-compose.edge.yml`
- [ ] `Sync__EdgeMode=false` trong `docker-compose.prod.yml` (từ Phase S)
- [ ] `project_state.md` updated

---

## 4. ANTI-PATTERNS (KHÔNG làm)

- ❌ Sửa `Domain.cs` cho RC-3 mà KHÔNG có user approval (governance: Domain Protection)
- ❌ Sửa `OrderRepository.AddAsync` public signature mà KHÔNG có user approval (governance: public API)
- ❌ Edge UI gọi Gateway HTTP cho order read (Edge = offline-first, đọc SQLite trực tiếp)
- ❌ Custom HTML/CSS cho UI — BẮT BUỘC VanAnButton, VanAnCard, VanAForm, VanAnDataGrid
- ❌ Hardcode `Sync__EdgeMode=true` trong code — đọc từ config
- ❌ Disable `DataSyncSubscriber` (Gateway) — Edge Mode cần SQLite→PG direction
- ❌ Bật Playwright (governance: disabled trong IMPLEMENT)
- ❌ Commit E1 hoặc E2 mà không có E-T7 / E-T15 evidence
- ❌ Implement E2 trước E1 (UI flow cần sync working để verify completed order)

---

## 5. ROLLBACK PLAN

### Track E1 fail
1. Revert commit trên `feature/order-sync-edge-mode-kitchen`
2. Report: task fail tại E-T{x}, evidence cụ thể
3. Nếu RC-1 (atomic) cần public API change mà user không approve → revert + giữ 2-transaction (accept orphan risk), document as Tier 2 debt
4. Nếu RC-3 cần Domain change mà user không approve → giữ drop fields, document as Tier 2 debt

### Track E2 fail
1. Revert UI commits
2. Report: UI task fail, giữ sync fix (E1 đã merge)
3. Kitchen UI có thể defer — sync working là priority

---

## 6. VERIFICATION CHECKLIST

```powershell
# 1. Build
dotnet build VanAn.sln
# Expected: 0 errors

# 2. Guard check
.\scripts\guard-check.ps1
# Expected: PASS

# 3. Edge Mode verify (E-T1 + E-T7)
docker compose -f docker-compose.edge.yml up -d
# Wait ~30s

# Reproduce (E-T1)
$body = @{ items = @(@{ productId = "00000000-0000-0000-0000-000000000001"; quantity = 1; unitPrice = 10000 }); customerName = "Test" } | ConvertTo-Json
Invoke-RestMethod -Uri "http://localhost:5001/api/public/orders/checkout" -Method Post -Body $body -ContentType "application/json"
Start-Sleep 10
# Check PostgreSQL
docker exec vanan-postgres-local psql -U vanan_dev -d VanAnLocal -c "SELECT COUNT(*) FROM \"Orders\";"
# Check SQLite (in shoperp container)
docker exec vanan-shoperp sqlite3 /data/sqlite/vanan.db "SELECT COUNT(*) FROM Orders;"
# Expected before fix: PG +1, SQLite unchanged

# Fix verify (E-T7) — after implementation
# Re-run checkout + check SQLite — expected +1 with ProductName + VatRate
docker exec vanan-shoperp sqlite3 /data/sqlite/vanan.db "SELECT o.\"TrackingCode\", oi.\"ProductName\", oi.\"VatRate\" FROM Orders o JOIN OrderItems oi ON o.\"Id\" = oi.\"OrderId\" LIMIT 5;"

# 4. UI verify (E-T15) — manual browser
# Open http://localhost:5003/pos → create order
# http://localhost:5003/pos/payment/{id} → cash payment
# http://localhost:5003/kitchen → verify 3 columns + transitions
```

---

## 7. NOTES

### E-T2 (RC-1) — Critical governance note
`OrderRepository.AddAsync` hiện gọi `SaveChangesAsync` (line 112). Có 2 option:
- **Option A (preferred):** Thêm method `AddAsyncNoSave(Order, CancellationToken)` mới — không breaking public API
- **Option B:** Sửa `AddAsync` bỏ `SaveChangesAsync` — breaking public API, cần user approval

**Action:** Audit callers trước (grep `\.AddAsync\(.*[Oo]rder`), nếu chỉ `OrderService.CreateOrderFromCommandAsync` + `OrderService.CreateOrderAsync` gọi → Option A an toàn. Nếu nhiều callers → STOP, ask user.

### E-T4 (RC-3) — Domain inspection required
Trước khi sửa, đọc `1_Shared/Domain.cs` search `class OrderItem` — verify:
- Có field `ProductName`? Kiểu gì? Setter private/internal/public?
- Có field `VatRate`? Kiểu?
- Có field `TotalAmount`? Kiểu?
- Factory `OrderItem.Create(...)` hiện nhận params nào?

Nếu thiếu field hoặc setter → report Domain Modeling Defect per governance.

### E-T11 (Kitchen Display) — Real-time choice
2 option cho real-time update:
- **SignalR:** Gateway `OrderHub` đã có — nhưng Edge Mode Server A không luôn kết nối Gateway → không reliable
- **Polling 5s:** Simple, offline-friendly, dùng `IJSRuntime` setInterval hoặc Blazor `Timer` — preferred cho Edge

Chọn polling 5s cho E-T11.

### E-T14 (NavMenu gating) — SaaS vs Edge
SaaS Mode không cần POS (customer dùng KhachLink). Edge Mode cần POS (staff nhập order offline). Gate bằng:
```razor
@if (syncEdgeMode)
{
    <VanAnNavItem Href="/pos" Label="POS" />
    <VanAnNavItem Href="/kitchen" Label="Bếp" />
}
```
`syncEdgeMode` đọc từ `IConfiguration.GetValue<bool>("Sync__EdgeMode", false)`.

### E-T10 (Payment) — Edge offline note
Edge Mode offline → KHÔNG gọi VietQR API (cần internet). Cash payment là primary. QR generate offline via `IShopQrCodeService` (QRCoder library, local) — nhưng customer scan + pay cần internet → async. Implement: cash = instant confirm, QR = pending + poll khi online.
