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

**[ORDER SYNC FIX + EDGE KITCHEN UI - OPTION D - TRACK E1 COMPLETE, TRACK E2 IN PROGRESS]**

Fix PostgreSQL to SQLite order sync (RC-1/2/3 + 2 bonus bugs) + build Edge Kitchen UI (POS input, payment, kitchen display, transitions). Master plan: `docs/AI/tasks/order_sync_fix_kitchen_ui_master_plan.md`. Task card: `docs/AI/tasks/order_sync_fix_kitchen_ui_task_card.md`. 16 tasks (Track E1: sync fix T1-T8, Track E2: kitchen UI T9-T16).

**Track E1 (COMPLETE - commit `c2de0c2b` + `dd13bc19`):** RC-1 Atomic Outbox (AddAsyncNoSave + transaction), RC-2 Subject namespace (`vanan.cloud.*` vs `vanan.shoperp.*`), RC-3 OrderItem full payload (ProductName + VatRate), OutboxEvent ID fix, DataSyncSubscriber stub completed, SQLite product seed FK fix. Verify T6 PASS: checkout -> PG atomic -> NATS -> SQLite (1 order, Dashboard TodayOrders=1, TodayRevenue=55000). Build 0 errors. Tests 995/995 PASS.

**Track E2 (IN PROGRESS):** Edge Kitchen UI - 8 tasks:
- T9: POS Order Input page `/pos` (VanAForm + product picker + quantity + customer info + payment method)
- T10: Payment page `/pos/payment/{orderId}` (cash + QR)
- T11: Kitchen Display page `/kitchen` (3-column layout, polling 5s, VanAnCard)
- T12: Status transition buttons (confirmed -> preparing -> ready -> delivering -> completed)
- T13: "Tra don cho khach" flow (ready -> delivering -> completed + Outbox + accounting)
- T14: NavMenu + Sitemap integration (add POS + Kitchen links)
- T15: Verify UI flow (POS -> payment -> kitchen -> complete)
- T16: Build + commit E2

**Known debt RC-7:** OrderService doesn't enrich OrderItems with ProductName/VatRate from Product entity (pre-existing, not sync bug - deferred).

**Previous (completed):** QuickSetup + Product Management plan (IMPLEMENT PENDING - parked) -> Tiered Auth P0-P3 -> KhachLink Waves 0-4 -> Accounting PostgreSQL 3 Waves -> KhachLink UI/UX Fix -> Payment Webhook Fix (pending VPS deploy). See archive for details.

## 3. Current Status

- **Branch:** `feature/order-sync-fix-kitchen-ui`
- **Last commit:** `dd13bc19` [STATE] Track E1 sync fix complete - update maintenance log
- **Uncommitted changes:** clean (after state update)
- **.NET SDK:** 8.0.422 (system path, CVEs patched, global.json pinned)
- **DB:** SQLite `vanan_shoperp.db` (local dev, business) - PostgreSQL `VanAnLocal` (business, Docker) - PostgreSQL `vanan_accounting` (accounting, Docker)
- **Tests (Debug, 2026-07-15):** Arch 38/38 - Core **995/995** - KhachLinkStartup 4/4. Build 0 errors, 296 warnings (pre-existing). guard-check PASS.
- **E2E (2026-07-15):** `khachlink-full-order-flow` PASS local + VPS production.
- **Order Sync Fix (2026-07-15 - COMPLETE):** Track E1 T1-T8 done. Option C SUPERSEDED by Option D. Sync PG->SQLite works for both SaaS and Edge Mode. Verify T6 PASS. Commit `c2de0c2b`.
- **QuickSetup + Product Management (2026-07-14):** Master plan + 6 task cards created. Gap review complete. **PARKED** while Order Sync + Kitchen UI is active.
- **Tiered Auth:** P0-P3 PASS (Online RV 14/14 PASS). P4 Facebook - P5 Zalo ZNS - P6 E2E.
- **Payment Webhook Fix:** CODE COMPLETE, merged to `main` (`f9b0392f`). **PENDING VPS DEPLOY.**
- **Local infra (Debug):** Docker + PostgreSQL 5432 + NATS 4222 + ShopERP 5003 + KhachLink 5002 + Gateway 5001.
- **Tech debt:** Tier 5 - True Offline Edge. Tier 4 - Roslyn Analyzers dead code.
- **Completed streams (all merged to main):** KhachLink Waves 0-4 - Tiered Auth P0-P3 - Platform SystemAdmin - Stream G/F/D/C/B - Order Lifecycle - Bucket A. See archive for details.

## 4. Next Actions

**Immediate (Order Sync + Kitchen UI - IMPLEMENT Track E2):**
1. **T9:** POS Order Input page `/pos` - `5_WebApps/ShopERP/Components/Pages/POS/Create.razor` (NEW). Task card: `order_sync_fix_kitchen_ui_task_card.md`.
2. **T10:** Payment page `/pos/payment/{orderId}` - cash + QR. Task card: `order_sync_fix_kitchen_ui_task_card.md`.
3. **T11:** Kitchen Display page `/kitchen` - 3-column layout, polling 5s. Task card: `order_sync_fix_kitchen_ui_task_card.md`.
4. **T12:** Status transition buttons - confirmed -> preparing -> ready -> delivering -> completed. Task card: `order_sync_fix_kitchen_ui_task_card.md`.
5. **T13:** "Tra don cho khach" flow - completed trigger Outbox + accounting. Task card: `order_sync_fix_kitchen_ui_task_card.md`.
6. **T14:** NavMenu + Sitemap integration - add POS + Kitchen links. Task card: `order_sync_fix_kitchen_ui_task_card.md`.
7. **T15:** Verify local UI flow end-to-end (POS -> payment -> kitchen -> complete).
8. **T16:** Build + commit `[KITCHEN-UI] POS + Payment + Display + transitions`.

**Immediate (Payment Webhook Fix - DEPLOY + VERIFY):**
1. **S6:** Push origin `main` -> trigger CD -> deploy VPS -> verify webhook returns 200 + PostgreSQL `JournalEntries` table has revenue + COGS entries
2. **S7:** Update `manual-test-vps-guide.md` (remove "500 chap nhan duoc" workaround) + sign-off task card

**Deferred (pre-existing, not blocking):**
1. **QuickSetup + Product Management:** 6 phases created, parked until Order Sync + Kitchen UI complete.
2. **Fix Accounting Entries 500 (pre-existing):** Gateway SQLite `AccountingEntries` table missing `AccountCode` column - schema migration gap.
3. **Fix GET /dev/login route ambiguity:** Pre-existing routing conflict.
4. **PostgreSQL migrations sync:** PostgreSQL only has 2 migrations vs SQLite has 6+. `Customers.IdentityLevel` manually added on VPS. Not blocking E2E.
5. **Payment webhook 400 (pre-existing AuditLog bug):** Payment webhook returns 400 due to AuditLog tenant ID mismatch. Order still marked Paid.
6. **Access Matrix Phase 1: ANALYZE** - when user approve `platform_systemadmin_access_matrix_master_plan.md`
7. **W8: Final Regression + Production Tag** - full regression + `saas-production-v1.0` tag
8. **Roslyn Analyzer wiring fix** - Tier 4 debt, low priority

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

**Luu � quan tr?ng (verified 2026-07-09):**
- ShopERP d�ng SQLite trong C? 2 mode (Program.cs lu�n `UseSqlite`, kh�ng c� `UseNpgsql` path)
- `docker-compose.prod.yml` ShopERP kh�ng set `SQLITE_DB_PATH` ? fallback local file trong container
- `docker-compose.edge.yml` ShopERP set `SQLITE_DB_PATH=Data Source=/data/shoperp.db` + volume `shoperp_sqlite_data`
- `docker-compose.edge.yml` c� th�m `shoperp-nats-sync` worker (command `--sync-worker`) d? poll Outbox + publish NATS

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
