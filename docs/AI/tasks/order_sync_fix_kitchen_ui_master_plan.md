# MASTER IMPLEMENTATION PLAN — Order Sync PostgreSQL→SQLite Fix + Edge Kitchen UI

> **Status:** APPROVED 2026-07-15 (Option D locked) — pivoted from Option C after S-T1 falsified hypothesis
> **Created:** 2026-07-15 (Option C initial) → updated 2026-07-15 (Option D)
> **Target Workflow:** `newfeaturebuild.md` (ANALYZE → IMPLEMENT)
> **Branch strategy:** `main` → `feature/order-sync-fix-kitchen-ui` (single branch)
> **Execution principle:** Verify-first (local docker) → Fix root cause → Verify again
> **Prerequisite:** Investigation sessions 2026-07-15 (root cause + S-T1 falsification)
> **Reference:** `docs/AI/tasks/quicksetup_product_management_master_plan.md` (format template)
>
> **Task card (locked 2026-07-15 Option D):**
> - `docs/AI/tasks/order_sync_fix_kitchen_ui_task_card.md` (single unified card, 16 tasks)

---

## 0. EXECUTION RULES

### JIT Planning + Verify-First
**Nguyên tắc:** Mỗi phase BẮT BUỘC verify local trước + sau khi sửa. KHÔNG ship nếu chưa có evidence.

**Bước 1: VERIFY-REPRO** — Bật docker compose, reproduce bug, chụp baseline (counts, log lines)
**Bước 2: IMPLEMENT** — Theo plan, fix root cause
**Bước 3: VERIFY-FIX** — Re-run scenario, assert evidence thay đổi đúng kỳ vọng
**Bước 4: COMMIT** — Chỉ commit khi VERIFY-FIX PASS

### Session protocol
1. Mỗi session làm theo task card
2. Đầu session: `docker compose up` infra (PG + NATS) + app services cần thiết
3. Git branch riêng
4. Trước commit: `guard-check.ps1` + `dotnet build VanAn.sln`
5. Format commit: `[SYNC-FIX] Task description` hoặc `[KITCHEN-UI] Task description`

### Branch protocol
```
main
  └── feature/order-sync-fix-kitchen-ui  (single branch — sync fix + kitchen UI)
```

### Hard rules
- **Sync PG→SQLite chạy cho CẢ SaaS Mode và Edge Mode.** KHÔNG phân biệt bằng flag. PostgreSQL là SSoT khi online, SQLite là local cache cho Owner UI.
- **Atomic Outbox (RC-1):** Order + Outbox enqueue trong 1 transaction. KHÔNG 2 `SaveChangesAsync` riêng.
- **Subject namespace (RC-2):** PG→SQLite dùng `vanan.cloud.*`. SQLite→PG giữ `vanan.shoperp.*`. Mỗi subscriber chỉ listen đúng hướng.
- **OrderItem full payload (RC-3):** Bắt buộc truyền `ProductName`, `VatRate`, `TotalAmount` từ payload.
- **UI Platform:** Mọi UI mới (Kitchen UI) PHẢI dùng VanAnButton, VanAnCard, VanAForm, VanAnDataGrid. KHÔNG custom HTML/CSS.
- **Domain purity:** KHÔNG sửa `Domain.cs` cho RC-1/RC-2 (fix ở Service + Infrastructure). RC-3 có thể cần sửa `OrderItem` — report nếu phải sửa Domain.
- **Playwright DISABLED** trong IMPLEMENT. Chỉ enable ở verification phase cuối.
- **3-Round Fix Limit:** Mỗi task fail quá 3 rounds → STOP, report, ask user.

### Critical context (verified 2026-07-15)
- **PostgreSQL `VanAnLocal`:** clean baseline = 0 orders, 0 outbox
- **SQLite `vanan_shoperp.db`:** clean baseline = 0 orders, 9 products (seeded), 4 tenants (seeded)
- **Checkout `POST /api/public/orders/checkout` → PostgreSQL:** verified, order created with `Id=4e9dc6c6...`, status=pending, total=55000
- **ShopERP Dashboard `GET /api/dashboard/shop-metrics/{shopId}`:** returns `todayOrders: 0` (reads SQLite via `IVanAnDbContext` → `ShopERPDbContext`) — **BUG CONFIRMED**
- **Outbox PG = 1 sau checkout:** path enqueue chạy (khác baseline trước khi clean DB)
- **`Orders/Index.razor:147-149` uses `IHttpClientFactory.CreateClient("gateway")`** — but `"gateway"` client **KHÔNG register** trong `ShopERP/Program.cs`, không có `Gateway:BaseUrl` trong appsettings → fallback default HttpClient không BaseAddress → relative URL `api/orders` resolves về **chính ShopERP** → hits `ShopERP/Controllers/OrdersController.cs:22-47` → reads SQLite
- **`docker-compose.prod.yml:178` có `Gateway__BaseUrl` nhưng dành cho KhachLink** (line 178 trong khachlink service env), **KHÔNG phải ShopERP**
- **Kết luận:** Cả Orders page VÀ Dashboard metrics đọc SQLite trực tiếp → sync PG→SQLite **BẮT BUỘC** cho cả SaaS lẫn Edge Mode

### Critical context (architectural, verified earlier)
- **`OrderRepository.AddAsync:107-114`** gọi `SaveChangesAsync` → order committed trước Outbox enqueue → RC-1
- **`OrderService.CreateOrderFromCommandAsync:616`** — `_ = _outboxRepository.EnqueueAsync(outboxEvent)` (fire-and-forget) + `SaveChangesAsync` thứ 2 → RC-1
- **`NatsSyncWorker.BuildSubject:116-120`** — `vanan.shoperp.{eventType}` cho cả 2 hướng → RC-2
- **`OrderSyncSubscriber:58-67`** — subscribe `vanan.shoperp.order.created` (cùng subject Gateway publish) → RC-2 collision
- **`OrderSyncSubscriber:119-126`** — drop `ProductName`, `VatRate`, `TotalAmount` → RC-3
- **`DataSyncSubscriber.SyncOrderCreatedAsync:240-245`** — stub, chỉ log → cần hoàn thiện cho Edge Mode SQLite→PG (orders tạo offline qua ShopERP POS)
- **Order status workflow (`Domain.cs:425-432`):** Pending → Confirmed → Preparing → Ready → Delivering → Completed
- **Existing kitchen transitions ở `Orders/Detail.razor:72-115`** (per-order buttons, gated by `Kitchen_Workflow_Enabled`)
- **KHÔNG có Kitchen Display page** (Sitemap.razor:43-44 comment: "not yet built")
- **`OutboxMessages` SQLite cũng = 0** → SQLite→PG path cũng không chạy (RC-1 affects both directions)

---

## 1. PROBLEM STATEMENT

### Symptom (reported by user)
Order tạo qua KhachLink checkout (Gateway → PostgreSQL) không xuất hiện trong ShopERP Owner UI (Orders page + Dashboard).

### Root cause (verified 2026-07-15)
**6 commits trước (`4d1e7b90`→`19e8686e`) patch symptom, không fix root cause:**

| RC | Mô tả | Severity |
|---|---|---|
| RC-1 | Outbox không atomic — `OrderRepository.AddAsync` commit order trước, Outbox enqueue là transaction thứ 2 → nếu fail, order ở PG, Outbox mất, customer retry → duplicate order | CRITICAL |
| RC-2 | Subject collision — cả 2 hướng dùng `vanan.shoperp.order.created`. Tạm không loop vì `DataSyncSubscriber.SyncOrderCreatedAsync` là stub | CRITICAL |
| RC-3 | OrderItem mất field — `OrderSyncSubscriber:119-126` chỉ truyền `(itemId, tenantId, orderId, productId, quantity, unitPrice)`. Payload có `ProductName`, `VatRate`, `TotalAmount` nhưng bị drop | DATA |
| RC-4 | `OutboxMessage` implement `IMustHaveTenant` → buộc `IgnoreQueryFilters` ở mọi read/write — workaround cho modeling sai ban đầu | DEBT |
| RC-5 | 6 commits, 0 verify local — vi phạm Gate 1 Anti-Guessing | PROCESS |
| RC-6 | `PublicOrdersController.cs:95` hardcode `tenantId = 00000000-0000-0000-0000-000000000001` — che giấu vấn đề tenant resolution thật | DEBT |

### Why Option C was wrong (pivot 2026-07-15)
Option C claimed "ShopERP Orders page reads via Gateway HTTP, sync PG→SQLite only needed for Edge Mode." S-T1 verify discovered `"gateway"` HttpClient is **not registered** in ShopERP Program.cs → falls back to no-BaseAddress default → `api/orders` resolves to **ShopERP itself** (SQLite). So Owner UI reads SQLite in BOTH modes → sync is mandatory for BOTH modes.

---

## 2. SOLUTION ARCHITECTURE — Option D

### Single data flow (SaaS + Edge Mode both)
```
KhachLink (customer) → Gateway → PostgreSQL (SSoT when online)
                                       ↓
                                Outbox (PostgreSQL)
                                       ↓
                                NatsSyncWorker (Gateway)
                                subject: vanan.cloud.order.created
                                       ↓
                                    NATS
                                       ↓
                                OrderSyncSubscriber (ShopERP)
                                       ↓
                                SQLite (local cache for Owner UI)

ShopERP POS (Edge staff, offline) → SQLite
                                       ↓
                                Outbox (SQLite)
                                       ↓
                                NatsSyncWorker (ShopERP, Sync:Enabled=true)
                                subject: vanan.shoperp.order.created
                                       ↓
                                    NATS
                                       ↓
                                DataSyncSubscriber (Gateway)
                                       ↓
                                PostgreSQL
```

Subject separation (RC-2 fix):
- `vanan.cloud.*` — events ORIGINATING from Gateway/Cloud (PG→SQLite)
- `vanan.shoperp.*` — events ORIGINATING from ShopERP/Edge (SQLite→PG)

Mỗi subscriber chỉ listen prefix của hướng ngược lại.

### Mode differentiation (no Sync__EdgeMode flag needed)
- **SaaS Mode:** Both directions sync active. SQLite serves as local cache for Owner UI responsiveness.
- **Edge Mode:** Both directions sync active. SQLite is offline-first SSoT when internet down; sync resumes when online.
- **Differentiation:** `Sync:Enabled` flag (already exists, ShopERP Program.cs:127) controls whether `NatsSyncWorker` (ShopERP, SQLite→PG direction) runs. SaaS Mode can set `Sync:Enabled=false` if SQLite→PG sync not needed (orders only created via Gateway in SaaS).

### What was dropped from Option C
- ❌ `Sync__EdgeMode` flag — not needed, sync runs for both modes
- ❌ Dashboard HTTP refactor (S-T3, S-T4) — SQLite will have data via sync, no refactor needed
- ❌ Gateway `DashboardController` direct PostgreSQL compute — revert to forwarding (or leave as-is, it's a no-op in SaaS since SQLite has data)
- ❌ Phase S / Phase E split — single task card

---

## 3. PHASE BREAKDOWN

### Track E1 — Sync Root Cause Fix (8 tasks)
| Task | Mô tả |
|---|---|
| T1 | Verify-repro: `docker compose up` + clean DBs + checkout + assert SQLite=0, Dashboard=0 (evidence) |
| T2 | RC-1 Atomic: refactor `OrderService.CreateOrderFromCommandAsync` dùng `BeginTransactionAsync` → add order + enqueue outbox → 1 `SaveChangesAsync` → `CommitAsync`. Audit `OrderRepository.AddAsync` callers — nếu phải change public API → STOP, ask user |
| T3 | RC-2 Subject: đổi `NatsSyncWorker.BuildSubject` (Gateway) cho event PG→SQLite thành `vanan.cloud.{eventType}`. Đổi subscription ở `OrderSyncSubscriber` (ShopERP) thành `vanan.cloud.order.created` + `vanan.cloud.order.statuschanged`. Giữ `vanan.shoperp.*` cho SQLite→PG |
| T4 | RC-3 OrderItem full payload: `OrderSyncSubscriber.SyncOrderCreatedAsync` parse đủ `ProductName`, `VatRate`, `TotalAmount`. Inspect `OrderItem` Domain — report nếu cần sửa Domain |
| T5 | Hoàn thiện `DataSyncSubscriber.SyncOrderCreatedAsync` (Gateway) — hiện là stub, cần full upsert order + items vào PostgreSQL |
| T6 | Verify-fix: checkout qua Gateway → assert SQLite có order sau ~5s với đúng VAT + ProductName |
| T7 | Build + guard-check + commit `[SYNC-FIX] RC-1/2/3 atomic + subject + full payload` |
| T8 | Update `project_state.md` Maintenance Log — mark Option C SUPERSEDED by Option D |

### Track E2 — Edge Kitchen UI (8 tasks)
| Task | Mô tả |
|---|---|
| T9 | UI: Page POS Order Input `/pos` (VanAForm + VanAnDataGrid + product picker + qty + customer info + payment method) |
| T10 | UI: Payment integration (cash + QR — reuse existing `IShopQrCodeService` / VietQR) |
| T11 | UI: Kitchen Display page `/kitchen` (real-time list orders by status: Confirmed → Preparing → Ready columns, polling 5s) |
| T12 | UI: Status transition buttons (Confirmed→Preparing→Ready→Delivering→Completed) — reuse pattern from `Orders/Detail.razor:72-115` |
| T13 | UI: "Trả đơn cho khách" flow (Ready → Delivering → Completed, trigger loyalty/accounting qua existing `OrderWorkflowService`) |
| T14 | NavMenu + Sitemap integration (current Sitemap:43-44 đã có link `/Kitchen` nhưng comment "not yet built") |
| T15 | Verify local: POS input → payment → kitchen transitions → complete → assert order status trong SQLite |
| T16 | Build + commit `[KITCHEN-UI] POS + Display + transitions` |

---

## 4. GAP REVIEW (locked 2026-07-15 Option D)

| # | Gap | Resolution |
|---|---|---|
| G1 | `OrderRepository.AddAsync` gọi `SaveChangesAsync` (public API change cần cho RC-1) | T2 báo user trước khi sửa. Alternative: thêm `AddAsyncNoSave(Order)` internal |
| G2 | `OrderItem` domain có thể không có setter/factory cho `ProductName`, `VatRate`, `TotalAmount` | T4 inspect Domain trước, report nếu phải sửa Domain |
| G3 | Edge UI có thể không dùng được `IHttpClientFactory("gateway")` (Server A không có Gateway khi offline) | T9–T15 dùng SQLite trực tiếp (Edge = offline-first). HTTP chỉ cho sync path |
| G4 | KhachLink offline (Edge Mode internet down) → checkout fail | Out of scope — KhachLink offline-mode đã có `OfflineOrderService.cs` (IndexedDB) |
| G5 | Hardcoded tenant `00000000-0000-0000-0000-000000000001` (RC-6) | Deferred — không block sync fix |
| G6 | `OutboxMessage` là `IMustHaveTenant` (RC-4) | Deferred — `IgnoreQueryFilters` đã work, không block production. Tech debt Tier 3 |
| G7 | SQLite also needs Outbox path fixed (RC-1 affects SQLite→PG direction too via `OrderWorkflowService.RecordOrderCompletedEvent`) | T2 fix covers both — atomic transaction applies to all Outbox enqueues |

---

## 5. RISK REGISTER

| Risk | Mitigation |
|---|---|
| `OrderRepository.AddAsync` API change break caller | Audit callers trước (grep `AddAsync`), report impact |
| Domain `OrderItem` sửa cho RC-3 vi phạm Domain Protection rule | T4 inspect trước, nếu cần sửa → user approval (governance) |
| Sync lag ~2-5s (NATS poll) cho cả SaaS → Owner view trễ vài giây | Acceptable — Owner dashboard không real-time critical |
| `OrderWorkflowService.RecordOrderCompletedEvent` cũng affected bởi RC-1 (SQLite→PG direction) | T2 fix phải cover cả 2 enqueue points (CreateOrderFromCommandAsync + RecordOrderCompletedEvent) |
| Subject change break existing tests | Run test suite after T3, fix any tests asserting old subject |

---

## 6. VERIFICATION PROTOCOL

### Local verify (BẮT BUỘC trước commit)
```powershell
# Clean baseline
docker exec vanan-postgres-local psql -U vanan_dev -d VanAnLocal -c 'DELETE FROM "OrderItems"; DELETE FROM "Orders"; DELETE FROM "OutboxMessages";'
Remove-Item 5_WebApps/ShopERP/vanan_shoperp.db -Force

# Start apps
docker compose up -d postgres nats
dotnet run --project 2_Gateway/VanAn.Gateway.csproj --urls "http://localhost:5001"
dotnet run --project 5_WebApps/ShopERP/VanAn.ShopERP.csproj --urls "http://localhost:5003"

# Checkout
$body = @{ items = @(@{ productId = "71a10538-8695-4742-b464-a317c5d4cae4"; quantity = 2; unitPrice = 25000 }); customerName = "Test" } | ConvertTo-Json
Invoke-RestMethod -Uri "http://localhost:5001/api/public/orders/checkout" -Method Post -Body $body -ContentType "application/json"

# Wait for sync
Start-Sleep 10

# Assert: SQLite has order with correct fields
dotnet run --project .tmp-inspect.csproj  # SELECT * FROM Orders; SELECT * FROM OrderItems;
# Expected: 1 order, 1 item with ProductName + VatRate + TotalAmount

# Assert: Dashboard response
Invoke-RestMethod -Uri "http://localhost:5003/api/dashboard/shop-metrics/00000000-0000-0000-0000-000000000001"
# Expected: todayOrders >= 1, todayRevenue > 0
```

### Build verify
```powershell
.\scripts\guard-check.ps1
dotnet build VanAn.sln
# Expected: 0 errors, guard-check PASS
```

---

## 7. ACCEPTANCE CRITERIA

- [ ] Checkout qua Gateway → SQLite có order sau ~5s với `ProductName` + `VatRate` + `TotalAmount` đúng
- [ ] Dashboard `shop-metrics` response có `TodayOrders >= 1` sau checkout (KHÔNG cần Dashboard HTTP refactor)
- [ ] Order + Outbox atomic (1 transaction) — test kill giữa chừng không tạo orphan
- [ ] Subject namespace tách: `vanan.cloud.*` (PG→SQLite) vs `vanan.shoperp.*` (SQLite→PG)
- [ ] `OrderSyncSubscriber` subscribe `vanan.cloud.order.created` + `vanan.cloud.order.statuschanged`
- [ ] `DataSyncSubscriber.SyncOrderCreatedAsync` full upsert (không còn stub)
- [ ] Edge UI: POS input → payment → kitchen transitions → complete — full flow PASS local
- [ ] Build: 0 errors, guard-check PASS
- [ ] `project_state.md` updated — Option C SUPERSEDED by Option D

---

## 8. REFERENCES

- Investigation session 2026-07-15: conversation transcript (Option C → S-T1 falsification → Option D)
- Previous commits (patched symptoms, NOT root cause): `4d1e7b90`, `d04142ed`, `4bb5bd4b`, `084f3798`, `85fb43b9`, `19e8686e`
- ADR-001: `docs/Architecture/ADR001-Station-Architecture.md`
- Deployment Modes: `docs/AI/project_state.md` Section 5a
- Order W-1 task card (historical sync implementation): `docs/AI/tasks/order_w-1_task_card.md`
