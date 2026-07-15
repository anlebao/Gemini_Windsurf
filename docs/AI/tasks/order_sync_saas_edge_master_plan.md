# MASTER IMPLEMENTATION PLAN — Order Sync SaaS vs Edge Mode + Edge Kitchen UI

> **Status:** APPROVED 2026-07-15 — Option C locked, 2 task cards created
> **Created:** 2026-07-15
> **Target Workflow:** `newfeaturebuild.md` (ANALYZE → IMPLEMENT)
> **Branch strategy:** `main` → 2 feature branches (SaaS first, Edge after)
> **Execution principle:** Verify-first (local docker) → Fix root cause → Verify again
> **Prerequisite:** Investigation session 2026-07-15 — confirmed 6 previous commits patched symptoms, not root cause
> **Reference:** `docs/AI/tasks/quicksetup_product_management_master_plan.md` (format template)
>
> **Task cards (locked 2026-07-15):**
> - SaaS Mode: `docs/AI/tasks/order_sync_saas_mode_task_card.md`
> - Edge Mode + Kitchen UI: `docs/AI/tasks/order_sync_edge_mode_kitchen_ui_task_card.md`

---

## 0. EXECUTION RULES

### JIT Planning + Verify-First
**Nguyên tắc:** Mỗi phase BẮT BUỘC verify local trước + sau khi sửa. KHÔNG ship nếu chưa có evidence.

**Bước 1: VERIFY-REPRO** — Bật docker compose, reproduce bug, chụp baseline (counts, log lines)
**Bước 2: IMPLEMENT** — Theo plan, fix root cause
**Bước 3: VERIFY-FIX** — Re-run scenario, assert evidence thay đổi đúng kỳ vọng
**Bước 4: COMMIT** — Chỉ commit khi VERIFY-FIX PASS

### Session protocol
1. Mỗi session chỉ làm 1 task card
2. Đầu session: `docker compose up` infra (PG + NATS) + app services cần thiết
3. Git branch riêng per task card
4. Trước commit: `guard-check.ps1` + `dotnet build VanAn.sln`
5. Format commit: `[SYNC-SAAS] Task description` hoặc `[SYNC-EDGE] Task description`

### Branch protocol
```
main
  ├── feature/order-sync-saas-mode       (SaaS task card — merge trước)
  └── feature/order-sync-edge-mode-kitchen  (Edge task card — merge sau, depends SaaS)
```

### Hard rules
- **Flag `Sync__EdgeMode`:** BẮT BUỘC set trong docker-compose: `.prod.yml`=`false`, `.edge.yml`=`true`. Mọi logic sync PG↔SQLite gate bằng flag này.
- **SaaS Mode = PostgreSQL SSoT:** KHÔNG sync PG→SQLite. UI đọc qua Gateway HTTP.
- **Edge Mode = 2-way sync:** PG↔SQLite qua NATS. BẮT BUỘC fix root cause (RC-1/2/3), KHÔNG patch symptom.
- **Atomic Outbox (RC-1):** Order + Outbox enqueue trong 1 transaction (`BeginTransactionAsync` → add + enqueue → `SaveChangesAsync` → `CommitAsync`). KHÔNG tách 2 `SaveChangesAsync`.
- **Subject namespace (RC-2):** PG→SQLite dùng `vanan.cloud.order.created`. SQLite→PG giữ `vanan.shoperp.order.created`. Mỗi subscriber chỉ listen đúng hướng.
- **OrderItem full payload (RC-3):** Bắt buộc truyền `ProductName`, `VatRate`, `TotalAmount` từ payload. Factory hoặc internal setter cho 3 field này.
- **UI Platform:** Mọi UI mới (Edge task card) PHẢI dùng VanAnButton, VanAnCard, VanAForm, VanAnDataGrid. KHÔNG custom HTML/CSS.
- **Domain purity:** KHÔNG sửa `Domain.cs` cho task này (RC-1/2/3 fix nằm ở Service + Infrastructure layer).
- **Playwright DISABLED** trong IMPLEMENT. Chỉ enable ở verification phase cuối (online RV trên khachvip.online).
- **3-Round Fix Limit:** Mỗi task fail quá 3 rounds → STOP, report, ask user.

### Critical context (verified 2026-07-15)
- **PostgreSQL `VanAnLocal`:** 13 orders, **0 OutboxMessages** → path enqueue chưa chạy trong production (RC-1 surface)
- **ShopERP `Orders/Index.razor:147-149` + `Detail.razor:213-214`:** đã đọc qua `IHttpClientFactory("gateway")` → SaaS Mode Owner Orders page **đã work**
- **ShopERP `DashboardController.cs:106-108`:** đọc `_dbContext.Orders` (SQLite qua `IVanAnDbContext`) → **hiển thị 0 cho orders tạo qua KhachLink**
- **ShopERP `OrderManagementService.cs:173-189`:** tương tự, đọc SQLite → metrics sai
- **ShopERP `OrdersController.cs:162-173` `POST /api/orders`:** staff entry point, tạo empty order (no items) → SQLite trực tiếp, không qua Outbox
- **KhachLink `Checkout.razor:295-296`:** customer entry point, **luôn** gọi `api/public/orders/checkout` qua Gateway → PostgreSQL
- **Order status workflow (`Domain.cs:425-432`):** Pending → Confirmed → Preparing → Ready → Delivering → Completed (+ Cancelled)
- **Existing kitchen UI:** `Orders/Detail.razor:72-115` có transition buttons (confirmed→preparing→ready→completed), gated by `Kitchen_Workflow_Enabled` toggle
- **KHÔNG có Kitchen Display page** (Sitemap.razor:43-44 comment: "legacy Razor Page removed, Blazor equivalent not yet built")
- **`DataSyncSubscriber.SyncOrderCreatedAsync` (Gateway):** stub — chỉ log, không upsert (`5b1e3e32/240-245`)
- **`OrderSyncSubscriber` (ShopERP):** drop `ProductName`, `VatRate`, `TotalAmount` (RC-3)
- **`OutboxRepository.EnqueueAsync`:** chỉ add to change tracker — caller phải `SaveChangesAsync` (interface comment line 12)
- **`OrderRepository.AddAsync:107-114`:** gọi `SaveChangesAsync` → order committed trước khi Outbox enqueue → RC-1
- **`docker-compose.yml` (local):** gateway env có `Nats__Url=nats://nats:4222`; shoperp env **KHÔNG có** NATS env vars (chỉ có Accounting)
- **`docker-compose.prod.yml`:** cần verify (task SaaS-T1)
- **`docker-compose.edge.yml`:** có `shoperp-nats-sync` worker (line 154)

---

## 1. PROBLEM STATEMENT

### Symptom (reported by user)
Order tạo qua KhachLink checkout (Gateway → PostgreSQL) không xuất hiện trong ShopERP Owner UI.

### Root cause (verified 2026-07-15)
**6 commits trước (`4d1e7b90`→`19e8686e`) patch symptom, không fix root cause:**

| RC | Mô tả | Severity | Affected mode |
|---|---|---|---|
| RC-1 | Outbox không atomic — `OrderRepository.AddAsync` commit order trước, Outbox enqueue là transaction thứ 2 → nếu fail, order ở PG, Outbox mất, customer retry → duplicate order | CRITICAL | Edge (SaaS không dùng Outbox path) |
| RC-2 | Subject collision — cả 2 hướng dùng `vanan.shoperp.order.created`. Tạm không loop vì `DataSyncSubscriber.SyncOrderCreatedAsync` là stub, nhưng bomb hẹn giờ khi hoàn thiện stub | CRITICAL | Edge |
| RC-3 | OrderItem mất field — `OrderSyncSubscriber:119-126` chỉ truyền `(itemId, tenantId, orderId, productId, quantity, unitPrice)`. Payload có `ProductName`, `VatRate`, `TotalAmount` nhưng bị drop → SQLite có VAT mặc định + không có tên product | DATA | Edge |
| RC-4 | `OutboxMessage` implement `IMustHaveTenant` → buộc `IgnoreQueryFilters` ở mọi read/write (commit `85fb43b9`) — workaround cho modeling sai ban đầu. Precedent đúng: `AccountChartEntity` không có BaseEntity | DEBT | Edge |
| RC-5 | 6 commits, 0 verify local — vi phạm Gate 1 Anti-Guessing | PROCESS | Cả 2 |
| RC-6 | `PublicOrdersController.cs:95` hardcode `tenantId = 00000000-0000-0000-0000-000000000001` — che giấu vấn đề tenant resolution thật | DEBT | Cả 2 |

### False assumption (phiên trước)
"Sync PostgreSQL→SQLite là mảnh ghép còn thiếu" — **SAI cho SaaS Mode**. Owner Orders page đã đọc qua Gateway HTTP. Phần UI duy nhất thực sự hỏng trong SaaS là Dashboard metrics (2 method đọc SQLite trực tiếp). Sync PG→SQLite chỉ cần thiết cho **Edge Mode** (offline-first).

---

## 2. SOLUTION ARCHITECTURE — Option C

### SaaS Mode (`Sync__EdgeMode=false`, default)
```
KhachLink → Gateway → PostgreSQL (SSoT)
ShopERP UI → Gateway HTTP (Orders page đã work, Dashboard cần refactor)
NATS sync PG→SQLite: DISABLED
NATS sync SQLite→PG: DISABLED (không cần — SaaS dùng SQLite chỉ cho non-order business data)
```

### Edge Mode (`Sync__EdgeMode=true`)
```
Server A (Edge)                Server B (Central)
ShopERP UI → SQLite            KhachLink → Gateway → PostgreSQL
ShopERP POS input → SQLite     Checkout → PostgreSQL
                ↓                              ↓
        Outbox (SQLite)                Outbox (PostgreSQL)
                ↓                              ↓
        NATS subject:                   NATS subject:
        vanan.shoperp.order.created     vanan.cloud.order.created
                ↓                              ↓
        DataSyncSubscriber              OrderSyncSubscriber
        (Gateway, SQLite→PG)            (ShopERP, PG→SQLite)
```

Subject separation (RC-2 fix):
- `vanan.shoperp.*` — events ORIGINATING from ShopERP/Edge (SQLite→PG)
- `vanan.cloud.*` — events ORIGINATING from Gateway/Cloud (PG→SQLite)

Mỗi subscriber chỉ listen prefix của hướng ngược lại.

---

## 3. PHASE BREAKDOWN

### Phase S — SaaS Mode Cleanup (`order_sync_saas_mode_task_card.md`)
Mục tiêu: PostgreSQL SSoT, loại bỏ sync PG→SQLite không cần thiết, fix 2 Dashboard method.

| Task | Mô tả |
|---|---|
| S-T1 | Verify local SaaS Mode: `docker compose up` + checkout + đo Dashboard `shop-metrics` response (evidence: 0 orders) |
| S-T2 | Thêm endpoint `GET /api/dashboard/shop-metrics/{shopId}` ở Gateway (đọc PG qua `IVanAnDbContext`) |
| S-T3 | Refactor `DashboardController.GetShopMetrics` (ShopERP) → gọi Gateway HTTP thay vì đọc SQLite |
| S-T4 | Refactor `OrderManagementService.GetOrderMetricsAsync` → gọi Gateway HTTP |
| S-T5 | Disable sync PG→SQLite cho SaaS: gate `OrderSyncSubscriber` + `NatsSyncWorker` (Gateway) + Outbox enqueue block bằng `Sync__EdgeMode=false` |
| S-T6 | Set `Sync__EdgeMode=false` trong `docker-compose.prod.yml` (explicit), env var local |
| S-T7 | Verify fix local: `docker compose up` + checkout + Dashboard response có data |
| S-T8 | Build + guard-check + commit + update `project_state.md` (gỡ "VERIFY PENDING" sai) |

### Phase E — Edge Mode Root Cause Fix + Kitchen UI (`order_sync_edge_mode_kitchen_ui_task_card.md`)
Mục tiêu: Sync 2 chiều chạy đúng + UI Edge Mode đầy đủ (POS input → payment → kitchen → deliver).

**Track E1 — Sync Root Cause Fix:**
| Task | Mô tả |
|---|---|
| E-T1 | Verify local Edge Mode: `docker compose -f docker-compose.edge.yml up` + checkout qua Gateway + đo SQLite Orders count (evidence: 0 — sync broken) |
| E-T2 | RC-1 Atomic: refactor `OrderService.CreateOrderFromCommandAsync` dùng `BeginTransactionAsync` → add order + enqueue outbox → 1 `SaveChangesAsync` → `CommitAsync`. **Lưu ý:** `OrderRepository.AddAsync` hiện gọi `SaveChangesAsync` — cần tách Unit of Work (governance: báo user nếu phải change public API) |
| E-T3 | RC-2 Subject: đổi `NatsSyncWorker.BuildSubject` (Gateway) cho event PG→SQLite thành `vanan.cloud.{eventType}`. Đổi subscription ở `OrderSyncSubscriber` (ShopERP) thành `vanan.cloud.order.created` + `vanan.cloud.order.statuschanged`. Giữ `vanan.shoperp.*` cho SQLite→PG |
| E-T4 | RC-3 OrderItem full payload: `OrderSyncSubscriber.SyncOrderCreatedAsync` parse đủ `ProductName`, `VatRate`, `TotalAmount`. Thêm internal setter hoặc factory mới trên `OrderItem` (Domain) — **report nếu cần sửa Domain** |
| E-T5 | Hoàn thiện `DataSyncSubscriber.SyncOrderCreatedAsync` (Gateway) — hiện là stub, cần full upsert order + items vào PostgreSQL |
| E-T6 | Set `Sync__EdgeMode=true` trong `docker-compose.edge.yml` (explicit) |
| E-T7 | Verify local Edge: checkout qua Gateway → assert SQLite có order sau ~2s với đúng VAT + ProductName |
| E-T8 | Build + guard-check + commit |

**Track E2 — Edge Kitchen UI:**
| Task | Mô tả |
|---|---|
| E-T9 | UI: Page POS Order Input `/pos` (VanAForm + VanAnDataGrid + product picker + qty + customer info + payment method) |
| E-T10 | UI: Payment integration (cash + QR — reuse existing `IShopQrCodeService` / VietQR) |
| E-T11 | UI: Kitchen Display page `/kitchen` (real-time list orders by status: Confirmed → Preparing → Ready columns, SignalR hoặc polling) |
| E-T12 | UI: Status transition buttons (Confirmed→Preparing→Ready→Delivering→Completed) — reuse pattern từ `Orders/Detail.razor:72-115` |
| E-T13 | UI: "Trả đơn cho khách" flow (Ready → Delivering → Completed, trigger loyalty/accounting qua existing `OrderWorkflowService`) |
| E-T14 | NavMenu + Sitemap integration (current Sitemap:43-44 đã có link `/Kitchen` nhưng comment "not yet built") |
| E-T15 | Verify local Edge UI: POS input → payment → kitchen transitions → complete → assert order status trong SQLite |
| E-T16 | Build + commit |

---

## 4. GAP REVIEW (locked 2026-07-15)

| # | Gap | Resolution |
|---|---|---|
| G1 | `OrderRepository.AddAsync` gọi `SaveChangesAsync` (public API change cần thiết cho RC-1) | E-T2 báo user trước khi sửa. Alternative: thêm `AddAsyncNoSave(Order)` internal hoặc move SaveChanges ra caller |
| G2 | `OrderItem` domain có thể không có setter/factory cho `ProductName`, `VatRate`, `TotalAmount` | E-T4 inspect Domain trước, report nếu phải sửa Domain |
| G3 | Edge Mode UI có thể không dùng được `IHttpClientFactory("gateway")` (Server A không có Gateway khi offline) | E-T9–E-T15 dùng SQLite trực tiếp (Edge = offline-first). HTTP chỉ cho sync path |
| G4 | `docker-compose.edge.yml` có `shoperp-nats-sync` worker nhưng local chưa verify | E-T1 verify đầu tiên |
| G5 | KhachLink trong Edge Mode gọi Gateway (Server B) — nếu internet xuống, checkout fail | Out of scope — KhachLink offline-mode đã có `OfflineOrderService.cs` (IndexedDB) |
| G6 | Hardcoded tenant `00000000-0000-0000-0000-000000000001` (RC-6) | Deferred — không block sync fix |
| G7 | `OutboxMessage` là `IMustHaveTenant` (RC-4) | Deferred — `IgnoreQueryFilters` đã work, không block production. Tech debt Tier 3 |

---

## 5. RISK REGISTER

| Risk | Mitigation |
|---|---|
| SaaS refactor Dashboard HTTP tăng latency ~50ms | Acceptable cho dashboard. Cache 30s nếu cần |
| Edge Mode sync lag ~2-5s (NATS poll) | Acceptable — Owner view không real-time critical |
| `OrderRepository.AddAsync` API change break caller | Audit callers trước (grep `AddAsync`), report impact |
| Domain `OrderItem` sửa cho RC-3 vi phạm Domain Protection rule | E-T4 inspect trước, nếu cần sửa → user approval (governance) |
| 2 mode phải test độc lập → effort verify cao | Verify local trước (docker compose), RV online sau |
| Sync__EdgeMode flag set sai trong deployment → silent break | Architecture Test mới: assert flag giá trị theo compose file |

---

## 6. VERIFICATION PROTOCOL

### Local verify (BẮT BUỘC trước commit)
```powershell
# SaaS Mode
docker compose up -d postgres nats gateway shoperp
# Checkout qua Gateway
curl -X POST http://localhost:5001/api/public/orders/checkout -H "Content-Type: application/json" -d '{"items":[{"productId":"...","quantity":1,"unitPrice":10000}],"customerName":"Test"}'
# Assert: Dashboard shop-metrics response có TodayOrders >= 1

# Edge Mode
docker compose -f docker-compose.edge.yml up -d
# Checkout qua Gateway (Server B)
curl -X POST http://localhost:5001/api/public/orders/checkout -d '...'
# Assert: sau 5s, SQLite (Server A) có order với ProductName + VatRate đúng
```

### Build verify
```powershell
.\scripts\guard-check.ps1
dotnet build VanAn.sln
# Expected: 0 errors, guard-check PASS
```

### Online RV (sau khi merge main)
- SaaS: deploy → checkout trên khachvip.online → Dashboard response có data
- Edge: deploy VPS edge (nếu có) → tương tự

---

## 7. ACCEPTANCE CRITERIA (overall)

- [ ] SaaS Mode: Dashboard `shop-metrics` response có `TodayOrders >= 1` sau checkout (không cần sync PG→SQLite)
- [ ] SaaS Mode: `NatsSyncWorker` (Gateway) + `OrderSyncSubscriber` (ShopERP) disabled khi `Sync__EdgeMode=false`
- [ ] Edge Mode: checkout qua Gateway → SQLite có order sau ~5s với `ProductName` + `VatRate` đúng
- [ ] Edge Mode: Order + Outbox atomic (1 transaction) — test kill giữa chừng không tạo orphan
- [ ] Edge Mode: subject namespace tách biệt — `vanan.cloud.*` vs `vanan.shoperp.*`
- [ ] Edge Mode UI: POS input → payment → kitchen transitions → complete — full flow PASS local
- [ ] Build: 0 errors, guard-check PASS
- [ ] `project_state.md` updated — gỡ "VERIFY PENDING" sai (commit `db49639c`)

---

## 8. TIMELINE

Không cam kết thời gian cụ thể (governance rule). Thứ tự ưu tiên:
1. **Phase S trước** (SaaS Mode) — đơn giản hơn, resolve 80% user pain (Dashboard metrics)
2. **Phase E1** (Sync Root Cause Fix) — sau khi SaaS merge
3. **Phase E2** (Kitchen UI) — sau khi E1 verify PASS

Mỗi phase: verify local → commit → RV online → sign-off task card.

---

## 9. REFERENCES

- Investigation session 2026-07-15: conversation transcript
- Previous commits (patched symptoms, NOT root cause): `4d1e7b90`, `d04142ed`, `4bb5bd4b`, `084f3798`, `85fb43b9`, `19e8686e`
- ADR-001: `docs/Architecture/ADR001-Station-Architecture.md`
- Deployment Modes: `docs/AI/project_state.md` Section 5a
- Order W-1 task card (historical sync implementation): `docs/AI/tasks/order_w-1_task_card.md`
