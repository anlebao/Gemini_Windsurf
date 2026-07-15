# TASK CARD — Order Sync PostgreSQL→SQLite Fix + Edge Kitchen UI (Option D)

> **Master plan:** `docs/AI/tasks/order_sync_fix_kitchen_ui_master_plan.md`
> **Branch:** `feature/order-sync-fix-kitchen-ui`
> **Priority:** 1 (Critical — fix user pain Orders + Dashboard not showing KhachLink orders)
> **Mode:** IMPLEMENT (user approval granted 2026-07-15 Option D)
> **Prerequisite:** Master plan approved, investigation session 2026-07-15 complete (Option C falsified)
> **Estimated tasks:** 16 (Track E1: sync fix 8 tasks, Track E2: kitchen UI 8 tasks)

---

## 0. CONTEXT & DECISIONS (locked)

### Architecture decision (locked 2026-07-15 Option D)
- **Sync PG→SQLite runs for BOTH SaaS Mode and Edge Mode.** No `Sync__EdgeMode` flag. PostgreSQL is SSoT when online, SQLite is local cache for Owner UI.
- **Subject separation (RC-2 fix):**
  - `vanan.cloud.*` — events ORIGINATING from Gateway/Cloud (PG→SQLite) — ShopERP `OrderSyncSubscriber` listens
  - `vanan.shoperp.*` — events ORIGINATING from ShopERP/Edge (SQLite→PG) — Gateway `DataSyncSubscriber` listens
- **Atomic Outbox (RC-1 fix):** Order + Outbox enqueue trong 1 DB transaction. KHÔNG 2 `SaveChangesAsync` riêng.
- **OrderItem full payload (RC-3 fix):** Truyền đủ `ProductName`, `VatRate`, `TotalAmount` từ event payload.
- **Edge UI = offline-first:** Đọc SQLite trực tiếp (KHÔNG qua Gateway HTTP — Server A có thể mất mạng Server B).

### Verified facts (2026-07-15 S-T1)
- PostgreSQL `VanAnLocal` clean baseline: 0 orders, 0 outbox
- SQLite `vanan_shoperp.db` clean baseline: 0 orders, 9 products (seeded), 4 tenants (seeded)
- Checkout `POST /api/public/orders/checkout` → PostgreSQL: order created (Id=`4e9dc6c6...`, status=pending, total=55000), Outbox PG = 1 (enqueue runs)
- **ShopERP Dashboard `GET /api/dashboard/shop-metrics/{shopId}` returns `todayOrders: 0`** (reads SQLite via `IVanAnDbContext` → `ShopERPDbContext`) — **BUG CONFIRMED**
- **`Orders/Index.razor:147-149` uses `IHttpClientFactory.CreateClient("gateway")`** but `"gateway"` client **NOT registered** in `ShopERP/Program.cs`, no `Gateway:BaseUrl` in appsettings → fallback default HttpClient no BaseAddress → relative URL `api/orders` resolves to **ShopERP itself** → hits `ShopERP/Controllers/OrdersController.cs:22-47` → reads SQLite
- **Conclusion:** Both Orders page AND Dashboard metrics read SQLite directly → sync PG→SQLite **MANDATORY** for both SaaS and Edge Mode

### Verified facts (architectural)
- `OrderRepository.AddAsync` (`3_CoreHub/Repositories/OrderRepository.cs:107-114`) calls `SaveChangesAsync` → order committed before Outbox enqueue → RC-1
- `OrderService.CreateOrderFromCommandAsync:616` — `_ = _outboxRepository.EnqueueAsync(outboxEvent)` (fire-and-forget) + `SaveChangesAsync` thứ 2 → RC-1
- `NatsSyncWorker.BuildSubject:116-120` — `vanan.shoperp.{eventType}` for both directions → RC-2
- `OrderSyncSubscriber:58-67` — subscribes `vanan.shoperp.order.created` (same subject Gateway publishes) → RC-2 collision
- `OrderSyncSubscriber:119-126` — drops `ProductName`, `VatRate`, `TotalAmount` → RC-3
- `DataSyncSubscriber.SyncOrderCreatedAsync:240-245` — stub, only logs → needs full upsert for Edge Mode SQLite→PG (orders created offline via ShopERP POS)
- `OrderWorkflowService.RecordOrderCompletedEvent:122-172` — also enqueues to Outbox (SQLite→PG direction), also affected by RC-1
- Order status workflow (`Domain.cs:425-432`): Pending → Confirmed → Preparing → Ready → Delivering → Completed
- Existing kitchen transitions at `Orders/Detail.razor:72-115` (per-order buttons, gated by `Kitchen_Workflow_Enabled`)
- **NO Kitchen Display page** (Sitemap.razor:43-44 comment: "not yet built")
- ShopERP `OrdersController.cs:162-173` `POST /api/orders` — staff creates empty order (no items) → needs POS UI input

### Why Option C was wrong (pivot 2026-07-15)
Option C claimed "ShopERP Orders page reads via Gateway HTTP, sync PG→SQLite only needed for Edge Mode." S-T1 verify discovered `"gateway"` HttpClient is **not registered** in ShopERP Program.cs → falls back to no-BaseAddress default → `api/orders` resolves to **ShopERP itself** (SQLite). So Owner UI reads SQLite in BOTH modes → sync is mandatory for BOTH modes.

### User decisions (locked 2026-07-15)
- **Option D:** Sync PG→SQLite for both modes, drop `Sync__EdgeMode` flag, drop Dashboard HTTP refactor
- **Edge UI layout full flow:** Form nhập order → payment → bếp tiếp nhận → chế biến → sẵn sàng → trả đơn cho khách
- **UI Platform components BẮT BUỘC** — VanAnButton, VanAnCard, VanAForm, VanAnDataGrid, VanAnModal

---

## 1. TASKS — Track E1: Sync Root Cause Fix

| # | Task ID | Task | Files | Status |
|---|---------|------|-------|--------|
| 1 | T1 | **DONE - VERIFY-REPRO:** Clean DBs (PG `DELETE FROM "OrderItems"; DELETE FROM "Orders"; DELETE FROM "OutboxMessages";` + remove SQLite `vanan_shoperp.db`). Start apps: `docker compose up -d postgres nats` + `dotnet run` Gateway (5001) + ShopERP (5003) with env `ConnectionStrings__DefaultConnection=Host=localhost;Port=5432;Database=VanAnLocal;Username=vanan_dev` + `ConnectionStrings__AccountingConnection=Host=localhost;Port=5432;Database=vanan_accounting;Username=vanan_admin` + `Nats__Url=nats://localhost:4222` + `Sync__Enabled=false` (disable SQLite→PG direction for clean repro). Wait healthcheck 200. Checkout: `POST http://localhost:5001/api/public/orders/checkout` body `{"items":[{"productId":"71a10538-8695-4742-b464-a317c5d4cae4","quantity":2,"unitPrice":25000}],"customerName":"Test"}`. Wait 10s. Assert: (a) PG `Orders` count = 1, (b) SQLite `Orders` count = 0 (sync broken), (c) `GET http://localhost:5003/api/dashboard/shop-metrics/00000000-0000-0000-0000-000000000001` returns `todayOrders: 0`. Record evidence. | Terminal only | ⬜ |
| 2 | T2 | **DONE - RC-1 Atomic Outbox.** (a) Audit callers of `OrderRepository.AddAsync` — grep `\.AddAsync\(` in order context. Report impact. (b) Option preferred: add `AddAsyncNoSave(Order, CancellationToken)` to `IOrderRepository` + `OrderRepository` (NO SaveChangesAsync). Caller owns Unit of Work. (c) Refactor `OrderService.CreateOrderFromCommandAsync:531-641`: `BeginTransactionAsync` → `AddAsyncNoSave(order)` → `EnqueueAsync(outboxEvent)` → `SaveChangesAsync` (1 lần, commit cả order + outbox) → `CommitAsync`. Wrap in try/catch with `RollbackAsync`. (d) Also fix `OrderWorkflowService.RecordOrderCompletedEvent:122-172` — ensure Outbox enqueue is in same transaction as order status update. (e) **HARD STOP:** If must change public API `AddAsync` instead of adding new method → STOP, ask user approval (governance: "Do not change public API unless explicitly approved"). | `3_CoreHub/Repositories/IOrderRepository.cs`, `3_CoreHub/Repositories/OrderRepository.cs`, `3_CoreHub/Services/OrderService.cs`, `3_CoreHub/Services/OrderWorkflowService.cs` | ⬜ |
| 3 | T3 | **DONE - RC-2 Subject Namespace.** Tách 2 hướng: (a) `NatsSyncWorker.BuildSubject` (`3_CoreHub/Services/NatsSyncWorker.cs:116-120`) — thêm param `string prefix = "shoperp"`. Subject = `vanan.{prefix}.{eventType}`. (b) Gateway `NatsSyncWorker` registration (`Program.cs:301`) — khi publish PG→SQLite, truyền `prefix="cloud"`. **Cách:** thêm config `Sync__SubjectPrefix` (default `shoperp`, Gateway set `cloud`). Hoặc inject `IOptions<SyncOptions>`. (c) `OrderSyncSubscriber` (`5_WebApps/ShopERP/Services/OrderSyncSubscriber.cs:58-67`) — đổi subscription thành `vanan.cloud.order.created` + `vanan.cloud.order.statuschanged`. (d) `DataSyncSubscriber` (`2_Gateway/Services/DataSyncSubscriber.cs:63`) — giữ `vanan.shoperp.>` (SQLite→PG direction). (e) `OrderWorkflowService.RecordOrderCompletedEvent` + `NatsSyncWorker` (ShopERP) — giữ prefix `shoperp` (default). | `3_CoreHub/Services/NatsSyncWorker.cs`, `5_WebApps/ShopERP/Services/OrderSyncSubscriber.cs`, `2_Gateway/Program.cs`, `2_Gateway/Services/DataSyncSubscriber.cs` (verify only) | ⬜ |
| 4 | T4 | **DONE - RC-3 OrderItem Full Payload.** (a) Inspect `OrderItem` domain entity (`1_Shared/Domain.cs` — search `class OrderItem`). Verify có field `ProductName`, `VatRate`, `TotalAmount` + cách set. (b) Nếu `OrderItem` đã có setter/factory cho 3 field này → dùng. Nếu KHÔNG → report Domain Modeling Defect, ask user approval trước khi sửa Domain (governance: Domain Protection). (c) `OrderSyncSubscriber.SyncOrderCreatedAsync:115-127` — parse thêm `ProductName`, `VatRate`, `TotalAmount` từ item payload + truyền vào factory/setter. (d) Verify payload từ `OrderService.CreateOrderFromCommandAsync:598-607` đã có 3 field này (đã verify: có). | `5_WebApps/ShopERP/Services/OrderSyncSubscriber.cs`, có thể `1_Shared/Domain.cs` (if defect) | ⬜ |
| 5 | T5 | **DONE - Hoàn thiện `DataSyncSubscriber.SyncOrderCreatedAsync`** (`2_Gateway/Services/DataSyncSubscriber.cs:223-245`). Hiện là stub. Implement full upsert: (a) Parse full order payload (OrderId, TenantId, Items, CustomerInfo, Status, amounts). (b) Tạo `Order.Create(orderId, tenantIdObj, customerId, items)` + set customer info + status. (c) `dbContext.Orders.Add` + `SaveChangesAsync` (PostgreSQL). (d) Idempotent: check `AnyAsync(o => o.Id == orderId)` trước. (e) Pattern reference: `OrderSyncSubscriber.SyncOrderCreatedAsync:99-167` (mirror logic). | `2_Gateway/Services/DataSyncSubscriber.cs` | ⬜ |
| 6 | T6 | **DONE - VERIFY-FIX:** Restart apps (with `Sync__Enabled=false` still — only testing PG→SQLite direction). Re-run T1 checkout scenario. Wait 10s. **Expected:** (a) PG `Orders` +1, (b) SQLite `Orders` +1 sau ~5s, (c) SQLite order có `ProductName` + `VatRate` đúng (RC-3), (d) `OutboxMessages` PG có 1 row status=Processed, (e) Dashboard `todayOrders >= 1`. Record evidence (SQLite query output, Dashboard response). | Terminal only | ⬜ |
| 7 | T7 | **DONE - BUILD + COMMIT E1:** `guard-check.ps1` + `dotnet build VanAn.sln` 0 errors. Commit: `[SYNC-FIX] RC-1/2/3 atomic outbox + subject separation + full payload` | Solution-wide | ⬜ |
| 8 | T8 | Update `docs/AI/project_state.md` Maintenance Log: mark Option C SUPERSEDED by Option D. Add entry: "ORDER SYNC FIX — RC-1/2/3 FIXED, sync PG→SQLite working for both modes". | `docs/AI/project_state.md` | ⬜ |

---

## 2. TASKS — Track E2: Edge Kitchen UI

| # | Task ID | Task | Files | Status |
|---|---------|------|-------|--------|
| 9 | T9 | **UI: POS Order Input Page `/pos`.** Tạo `5_WebApps/ShopERP/Components/Pages/POS/Create.razor`: `- @page "/pos"` + `@rendermode InteractiveServer` - VanAForm với: product picker (VanAnDataGrid list products từ SQLite — gọi `IProductRepository` hoặc `IVanAnDbContext.Products`), quantity input, customer info (name/phone/address optional), payment method (cash/QR select), table number (optional) - "Tạo đơn" button → `OrderService.CreateOrderAsync` (lưu SQLite trực tiếp, KHÔNG qua Gateway HTTP — offline-first) - Sau tạo → redirect `/pos/payment/{orderId}` - **Pattern reference:** `KhachLink/Pages/Checkout.razor` (customer-facing, tương tự nhưng Edge dùng SQLite trực tiếp) | `5_WebApps/ShopERP/Components/Pages/POS/Create.razor` (NEW) | ⬜ |
| 10 | T10 | **UI: Payment Page `/pos/payment/{orderId}`.** Tạo `5_WebApps/ShopERP/Components/Pages/POS/Payment.razor`: `- Hiển thị order summary (items, total, VAT) - Cash: button "Xác nhận thanh toán" → `OrderService.UpdateOrderStatusAsync(orderId, "confirmed")` (skip payment gateway) - QR: generate VietQR via existing `IShopQrCodeService` hoặc VietQR API, hiển thị QR image, poll payment status (reuse existing payment webhook logic — nhưng Edge offline nên cash primary) - Sau confirm → redirect `/kitchen` (bếp tiếp nhận) | `5_WebApps/ShopERP/Components/Pages/POS/Payment.razor` (NEW) | ⬜ |
| 11 | T11 | **UI: Kitchen Display Page `/kitchen`.** Tạo `5_WebApps/ShopERP/Components/Pages/Kitchen/Display.razor`: `- @page "/kitchen"` + `@rendermode InteractiveServer` - 3 column layout (CSS Grid — VanAnCard per column): - Column 1 "Chờ tiếp nhận" (status=confirmed) — đỏ - Column 2 "Đang chế biến" (status=preparing) — vàng - Column 3 "Sẵn sàng" (status=ready) — xanh - Mỗi card: order tracking code, items list, time elapsed, transition button - Real-time: polling 5s (simpler cho Edge, không cần SignalR) - **Gated by `Kitchen_Workflow_Enabled` toggle** — nếu OFF, redirect `/orders` | `5_WebApps/ShopERP/Components/Pages/Kitchen/Display.razor` (NEW) | ⬜ |
| 12 | T12 | **UI: Status Transition Buttons.** Trong Kitchen Display, mỗi order card có buttons: - "Tiếp nhận" (confirmed → preparing) — gọi `OrderWorkflowService.TransitionStatusAsync` qua `shoperp/api/orderworkflow/{id}/status` (ShopERP local API, KHÔNG qua Gateway) - "Hoàn thành" (preparing → ready) - "Trả đơn" (ready → delivering → completed — 2 step hoặc 1 step tùy `Delivering` enable config) - **Pattern reference:** `Orders/Detail.razor:72-115` (existing per-order buttons, reuse transition logic) | `5_WebApps/ShopERP/Components/Pages/Kitchen/Display.razor` | ⬜ |
| 13 | T13 | **UI: "Trả đơn cho khách" Flow.** Khi staff bấm "Trả đơn": - Status: ready → delivering → completed (2 transitions) - `completed` trigger: `OrderWorkflowService.HandleOrderCompletedAsync` → `RecordOrderCompletedEvent` (Outbox) → NATS → accounting entries - Hiển thị success toast + remove card khỏi "Sẵn sàng" column - **Lưu ý:** `OrderWorkflowService` đã có full logic (line 101-120) — chỉ cần gọi đúng endpoint | `5_WebApps/ShopERP/Components/Pages/Kitchen/Display.razor` | ⬜ |
| 14 | T14 | **NavMenu + Sitemap Integration.** - `5_WebApps/ShopERP/Components/Layout/NavMenu.razor` — thêm menu items: "POS" (`/pos`), "Bếp" (`/kitchen`) - `5_WebApps/ShopERP/Components/Pages/Sitemap.razor:43-44` — gỡ comment "not yet built", ensure link `/Kitchen` hoạt động | `5_WebApps/ShopERP/Components/Layout/NavMenu.razor`, `5_WebApps/ShopERP/Components/Pages/Sitemap.razor` | ⬜ |
| 15 | T15 | **VERIFY UI Flow:** `dotnet run` ShopERP + manual flow: (a) Mở `/pos` → chọn product → nhập qty → tạo đơn (b) `/pos/payment/{id}` → cash payment → confirm (c) `/kitchen` → order xuất hiện ở column "Chờ tiếp nhận" (d) Bấm "Tiếp nhận" → order sang "Đang chế biến" (e) Bấm "Hoàn thành" → order sang "Sẵn sàng" (f) Bấm "Trả đơn" → order completed + biến khỏi display (g) Assert SQLite: order status = `completed`, `OutboxMessages` có 1 row OrderCompleted Record evidence (screenshot hoặc log). | Terminal + browser | ⬜ |
| 16 | T16 | **BUILD + COMMIT E2:** `guard-check.ps1` + `dotnet build VanAn.sln` 0 errors. Update `project_state.md` Section 6: "[2026-07-15] ORDER SYNC FIX + KITCHEN UI COMPLETE". Commit: `[KITCHEN-UI] POS + Display + transitions` | Solution-wide | ⬜ |

---

## 3. EXIT CRITERIA

### Track E1 (Sync Fix)
- [x] T1 evidence: SQLite Orders count = 0 sau checkout (reproduce broken)
- [x] Order + Outbox atomic — 1 transaction, `SaveChangesAsync` 1 lần
- [x] Subject namespace tách: `vanan.cloud.*` (PG→SQLite) vs `vanan.shoperp.*` (SQLite→PG)
- [x] `OrderSyncSubscriber` subscribe `vanan.cloud.order.created` + `vanan.cloud.order.statuschanged`
- [x] `OrderSyncSubscriber` parse đủ `ProductName`, `VatRate`, `TotalAmount`
- [x] `DataSyncSubscriber.SyncOrderCreatedAsync` full upsert (không còn stub)
- [x] T6 evidence: SQLite Orders +1 sau checkout, với đúng ProductName + VatRate
- [x] Dashboard `todayOrders >= 1` sau checkout
- [x] Build 0 errors

### Track E2 (Kitchen UI)
- [ ] Page `/pos` tồn tại, dùng VanAForm + VanAnDataGrid
- [ ] Page `/pos/payment/{orderId}` tồn tại, cash + QR payment
- [ ] Page `/kitchen` tồn tại, 3-column layout (VanAnCard), polling 5s
- [ ] Status transitions: confirmed → preparing → ready → delivering → completed
- [ ] NavMenu + Sitemap có link POS + Kitchen
- [ ] T15 evidence: full flow PASS (POS → payment → kitchen → complete)
- [x] Build 0 errors

### Overall
- [x] `project_state.md` updated — Option C SUPERSEDED by Option D

---

## 4. ANTI-PATTERNS (KHÔNG làm)

- ❌ Sửa `Domain.cs` cho RC-3 mà KHÔNG có user approval (governance: Domain Protection)
- ❌ Sửa `OrderRepository.AddAsync` public signature mà KHÔNG có user approval (governance: public API)
- ❌ Edge UI gọi Gateway HTTP cho order read (Edge = offline-first, đọc SQLite trực tiếp)
- ❌ Custom HTML/CSS cho UI — BẮT BUỘC VanAnButton, VanAnCard, VanAForm, VanAnDataGrid
- ❌ Bật Playwright (governance: disabled trong IMPLEMENT)
- ❌ Commit E1 hoặc E2 mà không có T6 / T15 evidence
- ❌ Implement E2 trước E1 (UI flow cần sync working để verify completed order)
- ❌ Refactor Dashboard `GetShopMetrics` to call Gateway HTTP (Option C approach — DROPPED, SQLite will have data via sync)
- ❌ Add `Sync__EdgeMode` flag (Option C approach — DROPPED)

---

## 5. ROLLBACK PLAN

### Track E1 fail
1. Revert commit trên `feature/order-sync-fix-kitchen-ui`
2. Report: task fail tại T{x}, evidence cụ thể
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

# 3. Clean baseline
docker exec vanan-postgres-local psql -U vanan_dev -d VanAnLocal -c 'DELETE FROM "OrderItems"; DELETE FROM "Orders"; DELETE FROM "OutboxMessages";'
Remove-Item 5_WebApps/ShopERP/vanan_shoperp.db -Force -ErrorAction SilentlyContinue

# 4. Start apps
docker compose up -d postgres nats
# Terminal 1:
$env:ConnectionStrings__DefaultConnection = "Host=localhost;Port=5432;Database=VanAnLocal;Username=vanan_dev"
$env:ConnectionStrings__Nats = "nats://localhost:4222"
$env:Nats__Url = "nats://localhost:4222"
dotnet run --project 2_Gateway/VanAn.Gateway.csproj --urls "http://localhost:5001"
# Terminal 2:
$env:ConnectionStrings__AccountingConnection = "Host=localhost;Port=5432;Database=vanan_accounting;Username=vanan_admin"
$env:Sync__Enabled = "false"  # disable SQLite→PG direction for clean PG→SQLite test
dotnet run --project 5_WebApps/ShopERP/VanAn.ShopERP.csproj --urls "http://localhost:5003"

# Wait for health
Start-Sleep 30
Invoke-WebRequest "http://localhost:5001/health"
Invoke-WebRequest "http://localhost:5003/health"

# 5. Reproduce (T1) — before fix
$body = @{ items = @(@{ productId = "71a10538-8695-4742-b464-a317c5d4cae4"; quantity = 2; unitPrice = 25000 }); customerName = "Test" } | ConvertTo-Json
Invoke-RestMethod -Uri "http://localhost:5001/api/public/orders/checkout" -Method Post -Body $body -ContentType "application/json"
Start-Sleep 10
# Check PG
docker exec vanan-postgres-local psql -U vanan_dev -d VanAnLocal -c 'SELECT COUNT(*) FROM "Orders";'
# Check Dashboard — expected 0 before fix
Invoke-RestMethod -Uri "http://localhost:5003/api/dashboard/shop-metrics/00000000-0000-0000-0000-000000000001"

# 6. Fix verify (T6) — after implementation
# Re-run checkout + check Dashboard — expected todayOrders >= 1
# Check SQLite via dotnet script or docker exec shoperp sqlite3

# 7. UI verify (T15) — manual browser
# Open http://localhost:5003/pos → create order
# http://localhost:5003/pos/payment/{id} → cash payment
# http://localhost:5003/kitchen → verify 3 columns + transitions
```

---

## 7. NOTES

### T2 (RC-1) — Critical governance note
`OrderRepository.AddAsync` hiện gọi `SaveChangesAsync` (line 112). Có 2 option:
- **Option A (preferred):** Thêm method `AddAsyncNoSave(Order, CancellationToken)` mới — không breaking public API
- **Option B:** Sửa `AddAsync` bỏ `SaveChangesAsync` — breaking public API, cần user approval

**Action:** Audit callers trước (grep `\.AddAsync\(.*[Oo]rder`), nếu chỉ `OrderService.CreateOrderFromCommandAsync` + `OrderService.CreateOrderAsync` gọi → Option A an toàn. Nếu nhiều callers → STOP, ask user.

**Also:** `OrderWorkflowService.RecordOrderCompletedEvent:122-172` cũng enqueue Outbox (SQLite→PG direction). Cần ensure cùng transaction với order status update. Audit `OrderWorkflowService.TransitionStatusAsync` flow.

### T4 (RC-3) — Domain inspection required
Trước khi sửa, đọc `1_Shared/Domain.cs` search `class OrderItem` — verify:
- Có field `ProductName`? Kiểu gì? Setter private/internal/public?
- Có field `VatRate`? Kiểu?
- Có field `TotalAmount`? Kiểu?
- Factory `OrderItem.Create(...)` hiện nhận params nào?

Nếu thiếu field hoặc setter → report Domain Modeling Defect per governance.

### T11 (Kitchen Display) — Real-time choice
2 option cho real-time update:
- **SignalR:** Gateway `OrderHub` đã có — nhưng Edge Mode Server A không luôn kết nối Gateway → không reliable
- **Polling 5s:** Simple, offline-friendly, dùng `IJSRuntime` setInterval hoặc Blazor `Timer` — preferred

Chọn polling 5s cho T11.

### T10 (Payment) — Edge offline note
Edge Mode offline → KHÔNG gọi VietQR API (cần internet). Cash payment là primary. QR generate offline via `IShopQrCodeService` (QRCoder library, local) — nhưng customer scan + pay cần internet → async. Implement: cash = instant confirm, QR = pending + poll khi online.
