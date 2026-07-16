# Project State

> **M?c d�ch:** Single Source of Truth cho AI v? tr?ng th�i d? �n. B?T BU?C d?c d?u m?i phi�n.
> **Archived:** 2026-07-08 � completed waves moved to `docs/AI/project_state_archive.md`

---

## 0. Maintenance Rules

1. One-and-only-one: M?i section ch? t?n t?i 1 l?n.
2. No contradiction: M?t h?ng m?c ch? c� 1 tr?ng th�i.
3. Ground Truth first: Verify path/branch v?i codebase tru?c khi ghi.
4. Now over History: Section 2-4 ch? m� t? vi?c �ANG l�m v� K? TI?P. Vi?c xong ? gom v�o Section 6.
5. Actionable Next Actions: X�a action d� qu� h?n/sai b?i c?nh.
6. Stamp every edit: C?p nh?t Section 9 m?i l?n s?a.

---

## 1. Project Overview

**D? �n:** V?n An Accounting System MVP � gi?i ph�p k? to�n HKD theo TT 152/2025/TT-BTC.
**Stack:** .NET 8 � EF Core � SQLite � Blazor Server (ShopERP) � Blazor WebAssembly (KhachLink PWA) � SignalR � YARP Gateway � xUnit � Playwright.
**Ki?n tr�c:** Clean Architecture + DDD + Multi-tenancy. Data flow: `KhachLink (5002) ? Gateway (5001) ? ShopERP (5003) ? SQLite`.

**Modules:** `1_Shared` (Domain) � `2_Gateway` (YARP) � `3_CoreHub` (Services, in-process) � `5_WebApps/ShopERP` (Blazor Server) � `5_WebApps/KhachLink` (Blazor WASM) � `UI.Platform` (Shared components) � `6_Tests/6_Testing`.

**Hard stops:** Domain PURE � `AccountingEntry` immutable � Gateway STATELESS � KhachLink HTTP-only � ShopERP SQLite (Business) + PostgreSQL (Accounting) � ALWAYS d�ng UI Platform components.

---

## 2. Current Objective

**[ORDER UUIDv7 SINGLE IDENTITY REFACTOR - COMPLETE + VPS VERIFY PENDING]**

Refactor `Order` entity to use a single, sequential UUIDv7 identifier, resolving the dual-identity problem where `Order.Id` (PK) and `Order.OrderId` (domain ID) were separately generated.

**5 Phases (ALL COMPLETE + LOCAL RUNTIME VERIFIED 2026-07-16):**
1. **Phase 1: Add UUIDNext 4.2.4** — CPM (`Directory.Packages.props`) + 5 projects (Shared, CoreHub, ShopERP, Gateway, KhachLink)
2. **Phase 2: Domain sync** — `Order.Create` syncs `OrderId = new OrderId(id)` after setting `Id` (single identity)
3. **Phase 3: UUIDv7 generation** — Replace `Guid.NewGuid()` → `Uuid.NewDatabaseFriendly(Database.PostgreSql)` at 3 sites (OrderService, OrdersController, OmnichannelOrderService) + fix `RevenueExcelReport` to use `order.Id`
4. **Phase 4: EF Core + Migration** — `OrderConfiguration` ignores `OrderId` property + 2 migrations (SQLite + PostgreSQL) drop `Orders.OrderId` column
5. **Phase 5: Tests + Runtime** — 3 test files updated (query by `o.Id`, not `o.OrderId.Value`); 83/83 order tests PASS; runtime verified: new order `019f6a18-7800-72e6-...` (UUIDv7 prefix, version nibble 7), transition `pending → preparing` 200 OK, Outbox enqueued, NATS published, dead column dropped, 4 pre-existing orders preserved.

**PENDING:** Commit + push + CD + VPS runtime verification.

**Previous (completed):** ShopERP UI Fix Batch + Sitemap/Nav Restructure -> Order Sync Fix + Edge Kitchen UI + VPS Data Sync Hardening -> QuickSetup + Product Management plan (IMPLEMENT PENDING - parked) -> Tiered Auth P0-P3 -> KhachLink Waves 0-4 -> Accounting PostgreSQL 3 Waves -> KhachLink UI/UX Fix -> Payment Webhook Fix (pending VPS deploy). See archive for details.

## 3. Current Status

- **Branch:** `main`
- **Last commit:** `adf00b3e` [FIX] ShopERP UI batch: VanAForm preventDefault + accounting data loss + sitemap restructure
- **Uncommitted changes:** 17 modified + 10 new files (Order UUIDv7 refactor) — pending commit
- **.NET SDK:** 8.0.422 (system path, CVEs patched, global.json pinned)
- **DB:** SQLite `vanan_shoperp.db` (local dev + VPS, business) - PostgreSQL `VanAnCoreHub` (VPS, accounting + Gateway business) - PostgreSQL `vanan_accounting` (local, accounting)
- **Build (2026-07-16):** 0 errors. VanAn.sln build PASS.
- **Order UUIDv7 Refactor (2026-07-16 - COMPLETE + LOCAL VERIFIED):** 5 phases done. UUIDNext 4.2.4 added. `Order.Create` syncs `OrderId = Id`. 3 sites use `Uuid.NewDatabaseFriendly(Database.PostgreSql)`. `OrderConfiguration` ignores `OrderId` + 2 migrations drop `Orders.OrderId` column. 3 test files updated. 83/83 order tests PASS. Local runtime: new order `019f6a18-7800-72e6-...` (UUIDv7), transition 200 OK, Outbox + NATS OK, dead column dropped, 4 pre-existing orders preserved.
- **UI Fix Batch (2026-07-16 - COMPLETE):** VanAForm preventDefault fix + RevenueEntry/ExpenseEntry accountCode pass-through + TransactionHistory CSV export + Sitemap restructure (settings card, sysadmin roles, HKD/Company VAS split) + NavMenu Home→/sitemap.
- **Order Sync Fix (2026-07-15 - COMPLETE):** Track E1 T1-T8 done. Sync PG->SQLite works for both SaaS and Edge Mode.
- **VPS Data Sync Hardening (2026-07-15 - COMPLETE):** GUID case mismatch fixed, product dedup by Name, SQLite persistent volume, deterministic seed GUIDs, ShopERP always SQLite (all environments). E2E verified on VPS.
- **QuickSetup + Product Management (2026-07-14):** Master plan + 6 task cards created. Gap review complete. **PARKED** while Order Sync + Kitchen UI is active.
- **Tiered Auth:** P0-P3 PASS (Online RV 14/14 PASS). P4 Facebook - P5 Zalo ZNS - P6 E2E.
- **Payment Webhook Fix:** CODE COMPLETE, merged to `main` (`f9b0392f`). **PENDING VPS DEPLOY.**
- **Local infra (Debug):** Docker + PostgreSQL 5432 + NATS 4222 + ShopERP 5003 + KhachLink 5002 + Gateway 5001.
- **VPS (Production):** khachvip.online — Gateway, ShopERP, KhachLink, PostgreSQL, NATS, Seq, Nginx. SQLite DB at `/app/keys/vanan_shoperp.db` (persistent volume `shoperp_data`).
- **Tech debt:** Tier 5 - True Offline Edge. Tier 4 - Roslyn Analyzers dead code.
- **Completed streams (all merged to main):** KhachLink Waves 0-4 - Tiered Auth P0-P3 - Platform SystemAdmin - Stream G/F/D/C/B - Order Lifecycle - Bucket A - Order Sync Fix Track E1+E2 - VPS Data Sync Hardening. See archive for details.

## 4. Next Actions

**Immediate (Order UUIDv7 Refactor - COMMIT + DEPLOY + VPS VERIFY):**
1. Commit 27 files (17 modified + 10 new — UUIDv7 refactor + master plan + 5 task cards)
2. Push origin `main` -> trigger CD -> wait for deploy -> VPS runtime verification
3. VPS verify: new order UUIDv7 prefix, transition API, Outbox + NATS, dead column dropped, pre-existing orders preserved

**Immediate (Payment Webhook Fix - DEPLOY + VERIFY):**
1. **S6:** Push origin `main` -> trigger CD -> deploy VPS -> verify webhook returns 200 + PostgreSQL `JournalEntries` table has revenue + COGS entries
2. **S7:** Update `manual-test-vps-guide.md` (remove "500 chap nhan duoc" workaround) + sign-off task card

**Immediate (QuickSetup + Product Management - IMPLEMENT):**
1. Review 6 task cards in `docs/AI/tasks/` (previously parked, now unblocked)
2. Phase 1: QuickSetup wizard implementation
3. Phase 2-6: Product management features

**Deferred (pre-existing, not blocking):**
1. **Fix Accounting Entries 500 (pre-existing):** Gateway SQLite `AccountingEntries` table missing `AccountCode` column - schema migration gap.
2. **Fix GET /dev/login route ambiguity:** Pre-existing routing conflict.
3. **PostgreSQL migrations sync:** PostgreSQL only has 2 migrations vs SQLite has 6+. `Customers.IdentityLevel` manually added on VPS. Not blocking E2E.
4. **Payment webhook 400 (pre-existing AuditLog bug):** Payment webhook returns 400 due to AuditLog tenant ID mismatch. Order still marked Paid.
5. **Access Matrix Phase 1: ANALYZE** - when user approve `platform_systemadmin_access_matrix_master_plan.md`
6. **W8: Final Regression + Production Tag** - full regression + `saas-production-v1.0` tag
7. **Roslyn Analyzer wiring fix** - Tier 4 debt, low priority
8. **RC-7 debt:** OrderService doesn't enrich OrderItems with ProductName/VatRate from Product entity (pre-existing, not sync bug).

## 5. Active Architecture Decisions

| Decision | L� do |
|---|---|
| CoreHub = in-process background service trong Gateway | Monolith Phase 1-2 (Option B approved 2026-07-05) |
| Gateway = DI composition root cho CoreHub | Program.cs dang k� CoreHub DbContext/Services |
| ShopERP = SQLite (Business) + PostgreSQL (Accounting) | ADR-001: accounting always online. ShopERPDbContext (SQLite) cho Business/Platform, VanAnDbContext (PostgreSQL) cho Accounting qua IAccountingDbContext. **ALL 3 WAVES COMPLETE 2026-07-10** � interface split + service swap + DI + docker-compose + 4 Architecture Tests (Rule J/K/L/M) + test fixes. 1223/1223 tests PASS. ? ENFORCED. |
| CustomerToken = `IDataProtector` | Tr�nh library m?i |
| `AccountingEntry` immutable, Reversal Entry | Audit trail b?t kh? x�m ph?m |
| Multi-tenancy `TenantId` filter m?i layer | Data isolation per HKD |
| EF Core Migrations = official schema management | Stream E � replace `EnsureCreated` for production |
| HKD Data Source = Option A (query AccountingEntries directly) | Wave 0.5 � AccountingEntry is immutable SSoT |
| DOCX export = DocumentFormat.OpenXml + XLSX = EPPlus 7.6.1 | Wave 0 T9 � user approved |
| **[NEW] PlatformUser = Infrastructure entity (non-tenant)** | Precedent: AccountChartEntity � cross-tenant admin, no BaseEntity |
| **[NEW] Execution Discipline Rules (EDR)** | 8 EDR rules in `platform_systemadmin_master_plan.md` Section 7 � r�ng bu?c execution ch?ng t�i di?n deviations |
| **[NEW] Access Matrix = verification plan ri�ng** | `platform_systemadmin_access_matrix_master_plan.md` � 4 phases, 5 EDR-AM rules, depends on F1-F5 COMPLETE |
| **[NEW] Dual Deployment Modes (2026-07-09)** | 2 mode production � xem Section 5a b�n du?i |

### 5a. Deployment Modes (Production)

**Mode 1 � SaaS (online, all-in-one VPS):**
- Compose: `docker-compose.prod.yml`
- T?t c? module ch?y tr�n 1 VPS: PostgreSQL + NATS + Seq + Gateway (in-process CoreHub) + ShopERP + KhachLink + Nginx
- Gateway ? PostgreSQL (central data)
- ShopERP ? SQLite local (offline-first, sync qua NATS Outbox khi online)
- KhachLink ? Gateway (HTTP)
- Use case: SaaS multi-tenant, kh�ch h�ng kh�ng c?n edge node ri�ng

**Mode 2 � Edge (t�ch bi?t, offline-capable):**
- Compose: `docker-compose.edge.yml`
- **Server A (Edge):** ShopERP + SQLite + NATS sync worker � ch?y d?c l?p, kh�ng c?n PostgreSQL
- **Server B (Central):** Gateway (in-process CoreHub) + PostgreSQL + KhachLink + Nginx
- Sync: ShopERP Outbox ? NATS ? Gateway ? PostgreSQL
- Use case: C?a h�ng offline-first, internet kh�ng ?n d?nh, data local t?i edge
- ADR-001: SQLite local + NATS sync + PostgreSQL cloud (accounting always online)

**Luu y quan trong (verified 2026-07-16):**
- ShopERP dung SQLite trong CA 2 mode (Program.cs luon UseSqlite, khong co UseNpgsql path cho ShopERPDbContext)
- docker-compose.prod.yml ShopERP set ConnectionStrings__DefaultConnection=Data Source=/app/keys/vanan_shoperp.db + volume shoperp_data:/app/keys (persistent across redeploy)
- docker-compose.edge.yml ShopERP set SQLITE_DB_PATH=Data Source=/data/shoperp.db + volume shoperp_sqlite_data
- docker-compose.edge.yml co them shoperp-nats-sync worker (command --sync-worker) de poll Outbox + publish NATS
- Safety check (2026-07-16): Program.cs throws InvalidOperationException if connection string contains Host= or Port= (PostgreSQL pattern) - prevents silent PostgreSQL fallback
- Startup log (2026-07-16): [ShopERP] ShopERPDbContext (SQLite) connection: ... printed on every startup for verification

---

## 6. History Log (compressed � see archive + git log for details)

* [2026-07-14] **QUICKSETUP + PRODUCT MANAGEMENT MASTER PLAN + 6 TASK CARDS.** Gap review: 5 blocking + 6 minor gaps all resolved. 6 task cards created. Ready for IMPLEMENT.
* [2026-07-14] **PAYMENT WEBHOOK 500 FIX � CODE COMPLETE ?.** Root cause: `OrderService` called `AddToBookAsync` twice with same JournalEntry instance. Fix A+B (TDD). 995/995 PASS. Merged to main. Pending VPS deploy.
* [2026-07-14] **KHACHLINK UI/UX FIX BATCH ?.** 3 batches, 10 commits. PWA + currency formatting + iOS install + fly-to-cart + checkout JSON. 8/8 VPS verify PASS.
* [2026-07-14] **KHACHLINK E2E VPS PRODUCTION PASS ?.** 7 root causes fixed. E2E PASS local + VPS.
* [2026-07-13] **TIERED AUTH P1+P2+P3 ONLINE RV COMPLETE ?.** 14/14 PASS on khachvip.online. 7 CD runs for deploy fixes.
* [2026-07-12] **TIERED AUTH MASTER PLAN + 7 TASK CARDS.** 7 phases, 96% cost saving.
* [2026-07-12] **KHACHLINK WAVE 3 + WAVE 4 COMPLETE.** Voice Note STT + TTS + QR Table Number + Configurable Polling.
* [2026-07-11] **KHACHLINK WAVE 0 + WAVE 2 COMPLETE.** Toggle infrastructure + Payment Flow + Kitchen UI. Live RV Protocol established.
* [2026-07-09 ? 07-10] **ACCOUNTING POSTGRESQL ONLINE � 3 WAVES COMPLETE ?.** IAccountingDbContext split + services/DI/config + 4 Architecture Tests. 1223/1223 PASS.
* [2026-07-09] **DOCKER CONFIG FIX + DEPLOYMENT MODES.** Port swap + MigrateAsync + Dual Deployment Modes (SaaS + Edge).
* **Older (2026-07-08 and before):** Platform SystemAdmin � Entry Point Check � SDK 8.0.422 � Stream G/F/D/C/B � Order Lifecycle � Bucket A. See `docs/AI/project_state_archive.md` for full history.

---

## 7. Active Files Reference

### Stream G (SaaS Hardening)
| File | Role |
|---|---|
| `docs/AI/tasks/saas_production_hardening_master_plan.md` | Master plan (W0-W8, 3 sprints) |
| `docs/AI/tasks/saas_w{0-8}_task_card.md` | 9 task cards |

### Stream F (VAS Reports)
| File | Role |
|---|---|
| `docs/AI/tasks/vas_enterprise_reports_master_plan.md` | Master plan (W0-W9, COMPLETE) |

---

## 8. Architecture Quick Reference

```
=== SaaS Mode (docker-compose.prod.yml) � all-in-one VPS ===

KhachLink (5002) ? Gateway (5001) ? ShopERP (5003) ? SQLite (local)
                        ?
              [in-process CoreHub services]
                        ?
                  PostgreSQL (central data)

=== Edge Mode (docker-compose.edge.yml) � t�ch bi?t 2 server ===

Server A (Edge):                      Server B (Central):
  ShopERP ? SQLite (local)              Gateway ? PostgreSQL
  shoperp-nats-sync worker              [in-process CoreHub]
       ? NATS Outbox sync ?
  ---------------? NATS ---------------? Gateway
                                         KhachLink ? Gateway (HTTP)
```

**Auth:** Cookie (Blazor Server) + JWT Bearer (API). `DevLoginController` (`#if DEBUG`) for E2E. BCrypt work factor 12.
**Roles:** `UserRole` (tenant-scoped: Owner/StoreKeeper/Guard/Staff/Masterchef) � `PlatformRole` (cross-tenant: SystemAdmin)

---

## 9. Maintenance Log

* **2026-07-16 -- ORDER UUIDv7 SINGLE IDENTITY REFACTOR COMPLETE + LOCAL RUNTIME VERIFIED.** 5-phase refactor to resolve dual-identity problem (`Order.Id` PK vs `Order.OrderId` domain ID generated independently). Phase 1: Added `UUIDNext 4.2.4` to CPM + 5 projects (Shared, CoreHub, ShopERP, Gateway, KhachLink). Phase 2: `Order.Create` syncs `OrderId = new OrderId(id)` after setting `Id` (single identity). Phase 3: Replaced `Guid.NewGuid()` → `Uuid.NewDatabaseFriendly(Database.PostgreSql)` at 3 sites (OrderService.cs, OrdersController.cs, OmnichannelOrderService.cs) + fixed `RevenueExcelReport.cs` to use `order.Id` (was `order.OrderId.Value`). Phase 4: `OrderConfiguration.cs` ignores `OrderId` property + 2 migrations (SQLite `20260716082930_DropOrderOrderIdColumn` + PostgreSQL `20260716083001_DropOrderOrderIdColumn`) drop `Orders.OrderId` column. Kept `OrderIdConverter` for `ElectronicInvoice.OrderId` + `PendingInvoiceQueue.OrderId`. Phase 5: Updated 3 test files (`OrderApiTests.cs`, `OrderWorkflowServiceTests.cs`, `OrderFinancialCalculationTests.cs`) — query by `o.Id`, removed `OrderId = new OrderId(...)` sets. 83/83 order tests PASS. Local runtime verified: new order `019f6a18-7800-72e6-b61a-7a85c39b4b1c` (UUIDv7 prefix `019f6a18`, version nibble `7`), transition `pending → preparing` 200 OK, OutboxEvent enqueued, NATS published, `Orders.OrderId` column dropped (40 columns remain), 4 pre-existing orders preserved (UUIDv4 → UUIDv7 transition clean). Cross-DB sync subscribers (DataSyncSubscriber, OrderSyncSubscriber) NOT modified. Build 0 errors. Branch: `main`. 27 files (17 modified + 10 new).

* **2026-07-16 -- SHOPERP UI FIX BATCH + SITEMAP/NAV RESTRUCTURE COMPLETE.** Fixed 3 batches of UI bugs: (1) VanAForm `@onsubmit:preventDefault` — Blazor Category B native form submit bug causing silent fail on 4 forms (TenantManagement create+onboarding, ProductManagement create+edit). (2) RevenueEntry/ExpenseEntry — pass accountCode/vendor/category/reference to AccountingEntryService (was null → duplicate check false-positive + data loss). ExpenseEntry.ValidateForm strip thousands separators. (3) TransactionHistory ExportToExcel — implemented CSV export via `vanAn.downloadFile` JS interop (was stub). Sitemap restructure: added "Cấu hình & Thiết lập" card (Owner only, link `/settings/shop-features`), moved "Quản Trị Hệ Thống" Owner→SystemAdmin, moved "Hóa Đơn Điện Tử" Owner/StoreKeeper→SystemAdmin, VAS BCTC reports card only for Company tenants (load BusinessType via ITenantManagementService, hidden for HKD). NavMenu Home button → `/sitemap`. Build 0 errors. Branch: `main`. 11 files modified.

* **2026-07-16 -- VPS DATA SYNC HARDENING + ENVIRONMENT PARITY COMPLETE.** 5-phase nuclear cleanup + fix: (1) Cleaned PG garbage (40 test orders, 450 dup products, 11 outbox messages deleted). (2) Wiped SQLite + force-recreate (fresh: 9 products, 5 users, 2 tenants). (3) Synced products PG↔SQLite with lowercase GUIDs (match exactly). (4) Fixed OrderSyncSubscriber to auto-create product stub from event payload if ProductId missing (prevents FK violation) + product sync dedup by Name+TenantId instead of ProductId (prevents duplicates across restarts with GUID case mismatch). (5) Deployed + verified E2E on VPS: checkout → 200 OK → NATS sync → "synced order → SQLite (1 items, 61600 VND)" → GET /api/orders/{id} → 200 OK. PG products: 9 (no duplicates after redeploy). Then added SQLite persistent volume (`ConnectionStrings__DefaultConnection` env var → `/app/keys/vanan_shoperp.db` in volume) + deterministic seed GUIDs (lowercase, match PostgreSQL) + ShopERP ALWAYS uses SQLite in all environments (appsettings.json/Production/Staging all have `DefaultConnection = Data Source=vanan_shoperp.db` + safety check that throws if connection string looks like PostgreSQL + startup log). Commits: `b3a8b3d6`, `89b69d3d`, `9840ddf6`. Branch: `main`. CD runs: 3 (all success). VPS verified: `[ShopERP] ShopERPDbContext (SQLite) connection: Data Source=/app/keys/vanan_shoperp.db`.

* **2026-07-16 -- STATE + MASTER PLAN UPDATED: Track E1 COMPLETE, Track E2 STARTING.** Updated `docs/AI/project_state.md` Sections 2/3/4 to reflect Order Sync Track E1 complete (commits c2de0c2b, dd13bc19) and Track E2 Kitchen UI in progress. Updated `docs/AI/tasks/order_sync_fix_kitchen_ui_master_plan.md` and `order_sync_fix_kitchen_ui_task_card.md` to mark T1-T8 DONE. Branch: `feature/order-sync-fix-kitchen-ui`.

* **2026-07-15 -- ORDER SYNC FIX TRACK E1 COMPLETE (Option D).** RC-1/2/3 fixed + 2 bonus bugs found during verify. Changes: (1) RC-1 Atomic Outbox: IOrderRepository.AddAsyncNoSave + BeginTransactionAsync + single SaveChangesAsync + CommitAsync in OrderService.CreateOrderFromCommandAsync. (2) RC-2 Subject: NatsSyncWorker.BuildSubject splits camelCase (OrderCreated -> order.created), Sync:SubjectPrefix config (Gateway=cloud, ShopERP=shoperp), OrderSyncSubscriber subscribes vanan.cloud.*, DataSyncSubscriber keeps vanan.shoperp.*. (3) RC-3 OrderItem payload: OrderSyncSubscriber parses ProductName + VatRate via reflection. (4) OutboxEvent ID fix: OutboxRepository.ToDomain preserves m.Id as OutboxEventId (constructor generated new Guid -> MarkAsProcessedAsync could never find row -> events stuck Pending forever). (5) DataSyncSubscriber.SyncOrderCreatedAsync completed (was stub). (6) SQLite product seed FK fix: override Products.Id = Products.ProductId (FK_OrderItems_Products_ProductId references Products.Id PK). Verify T6 PASS: checkout -> PG (atomic) -> NATS (vanan.cloud.order.created) -> SQLite (1 order, Dashboard TodayOrders=1, TodayRevenue=55000). Build 0 errors. Tests 995/995 PASS. Guard PASS. Commit: c2de0c2b. **Known debt RC-7:** OrderService doesn't enrich OrderItems with ProductName/VatRate from Product entity (pre-existing, not sync bug). Branch: feature/order-sync-fix-kitchen-ui.

* **2026-07-15 -- ORDER SYNC PLAN PIVOT: Option C -> Option D.** S-T1 verify local discovered Option C hypothesis was wrong: ShopERP Orders/Index.razor uses IHttpClientFactory.CreateClient('gateway') but 'gateway' client is NOT registered in Program.cs (no BaseAddress) -> relative URL 'api/orders' resolves to ShopERP itself (SQLite), NOT Gateway HTTP. So Owner UI reads SQLite in BOTH SaaS and Edge modes -> sync PG->SQLite is MANDATORY for both modes. Option D locked: sync PG->SQLite for both modes, drop Sync__EdgeMode flag, drop Dashboard HTTP refactor. Master plan + task card rewritten as single unified card (16 tasks: 8 sync fix + 8 kitchen UI). Old files: order_sync_saas_edge_master_plan.md renamed to order_sync_fix_kitchen_ui_master_plan.md, order_sync_saas_mode_task_card.md + order_sync_edge_mode_kitchen_ui_task_card.md deleted (replaced by order_sync_fix_kitchen_ui_task_card.md). Branch: main.

* **2026-07-15 -- ORDER SYNC INVESTIGATION RE-DO -- PREVIOUS DIAGNOSIS SUPERSEDED.** Investigation session 2026-07-15 found 6 commits (4d1e7b90 through 19e8686e) patched symptoms, not root cause. Real root causes: RC-1 (Outbox non-atomic), RC-2 (subject namespace collision), RC-3 (OrderItem missing fields), RC-4 (OutboxMessage IMustHaveTenant modeling), RC-5 (no verify local), RC-6 (hardcoded tenant). Previous entry "VERIFY PENDING" (commit db49639c) was based on wrong assumption that sync PG->SQLite is missing piece for SaaS Mode. Verified fact: ShopERP Orders/Index.razor already reads via Gateway HTTP. Only Dashboard metrics (2 methods) actually broken in SaaS Mode. **New plan (Option C locked):** SaaS Mode disable sync PG->SQLite (PostgreSQL SSoT, UI via HTTP). Edge Mode fix RC-1/2/3 + add Kitchen UI (POS input -> payment -> kitchen -> deliver). Master plan: docs/AI/tasks/order_sync_saas_edge_master_plan.md. 2 task cards: order_sync_saas_mode_task_card.md (8 tasks) + order_sync_edge_mode_kitchen_ui_task_card.md (16 tasks, Track E1 sync fix + Track E2 kitchen UI). **Branch:** main. **Next:** Phase S first (SaaS Mode cleanup), then Phase E1 + E2.

* **2026-07-15 -- ORDER SYNC GATEWAY (PostgreSQL) → SHOPERP (SQLite) -- CODE COMPLETE, VERIFY PENDING.** Root cause: sync 1-way (SQLite→PostgreSQL), thiếu reverse direction. Orders tạo qua Gateway (KhachLink checkout) lưu PostgreSQL, Owner query SQLite → empty. **Fix (Option B: Gateway Outbox + NATS subscriber):**
  1. `OrderService.CreateOrderFromCommandAsync`: enqueue `OrderCreated` event to Outbox (full payload: items, customer info, status, amounts) + `SaveChangesAsync` sau enqueue (OutboxRepository.EnqueueAsync chỉ add to change tracker).
  2. `2_Gateway/Program.cs`: register `IOutboxRepository` + `NatsSyncWorker` + `INatsEventPublisher` + `IOrderService`.
  3. `5_WebApps/ShopERP/Services/OrderSyncSubscriber.cs` (NEW): subscribe `vanan.shoperp.order.created` + `order.statuschanged` → sync to SQLite (idempotent, DDD factory methods).
  4. `docker-compose.prod.yml`: thêm `Nats__Url` + `NATS__Url` + `ConnectionStrings__Nats` cho shoperp + `NATS__Url` cho gateway.
  5. `NatsEventPublisher`: đọc 6 config keys (NATS__Url, Nats__Url, NATS:Url, Nats:Url, ConnectionStrings:Nats, ConnectionStrings__Nats) — Linux env vars case-sensitive.
  6. `OutboxRepository`: `IgnoreQueryFilters` cho GetPendingEventsAsync/MarkAsProcessed/MarkAsFailed/GetById — OutboxMessage là IMustHaveTenant, global filter loại tất cả messages khi CurrentTenantIdValue=Guid.Empty trong background worker.
  **Commits:** 4d1e7b90, d04142ed, 4bb5bd4b, 084f3798, 85fb43b9, 8b650f2e. **Build:** 0 errors. **CI:** PASS. **CD:** 4/6 success, 1 fail (VPS disk full 45G/44G — cleanup 37GB Docker images), 1 in-progress (8b650f2e). **VERIFY PENDING:** order chưa sync qua NATS do CD chưa deploy fix cuối (IgnoreQueryFilters). **Branch:** main.

* **2026-07-15 -- PROJECT STATE ARCHIVED.** Reduced from 301 to ~170 lines. Moved Maintenance Log entries 2026-07-13 and earlier + completed status items to docs/AI/project_state_archive.md (Section "Archived 2026-07-15"). Kept: 2026-07-14 entries + current/pending items. **Branch:** main.

* **2026-07-14 -- QUICKSETUP + PRODUCT MANAGEMENT MASTER PLAN + 6 TASK CARDS CREATED.** Gap review: 5 blocking + 6 minor gaps all resolved. G3 (IProductService Clean Architecture), G4 (shared CurrencyHelper), G7 (tenant selection from TenantManagement row), G8 (Cloudinary image upload) -- user locked. 6 task cards created. 55 RV tests planned in 6 blocks. **Branch:** main.

* **2026-07-14 -- PAYMENT WEBHOOK 500 FIX -- CODE COMPLETE, PENDING VPS DEPLOY.** Root cause: OrderService called AddToBookAsync twice with same JournalEntry instance. Fix A+B (TDD): dedup caller + track-aware guard. 6 new tests. 995/995 PASS. Commit 2f084bf1, merged to main 9b0392f. **Remaining:** push origin main -> CD -> verify on VPS.

* **2026-07-14 -- KHACHLINK UI/UX BUG FIX BATCH COMPLETE.** 3 batches, 10 commits. Batch 1: PWA manifest + QR scanner + responsive layout. Batch 2: Currency C# formatting (replaced JS race condition). Batch 3: PWA iOS install + fly-to-cart animation + checkout JSON mapping fix. Google OAuth production wiring + profile crash fix. 8/8 VPS verify PASS.

* **2026-07-14 -- KHACHLINK E2E VPS PRODUCTION PASS.** 7 root causes fixed: SQLite->PostgreSQL product sync, FK constraint mapping, DevLogin->SystemAdmin impersonation, Playwright baseURL, Gateway public tracking endpoint, PostgreSQL IdentityLevel column. E2E PASS local (28.1s) + VPS (11.7s).

* **Older entries (2026-07-13 and before):** See docs/AI/project_state_archive.md Section "Archived 2026-07-15".
