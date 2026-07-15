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

**[QUICKSETUP FIX + PRODUCT MANAGEMENT WITH QR CODE � MASTER PLAN + 6 TASK CARDS CREATED ? � IMPLEMENT PENDING]**

Build Product Management feature cho Owner/Admin: fix QuickSetup orphan page (SystemAdmin tool), th�m Product CRUD API (Clean Architecture: IProductService + IProductRepository), Product Management UI (VanAnDataGrid + VanAForm), QR code view + print (batch A4 2x5), E2E tests. Master plan: `docs/AI/tasks/quicksetup_product_management_master_plan.md` � 6 phases, 6 task cards created.

**Phase 1 (NOT STARTED ?):** Fix QuickSetup Orphan Page � `@rendermode` + `[Authorize(Policy="SystemAdminOnly")]` + `IHttpClientFactory` + tenant selection t? TenantManagement row ? redirect `/quick-setup?tenantId={id}` + FIX BUG TemplateType (Guid string thay v� "cafe"). Task card: `quicksetup_phase1_fix_task_card.md`.

**Phase 2 (NOT STARTED ?):** Domain � `Product.Update()` + `Deactivate()` + `Activate()` + `MarkAsDeleted()` methods (g?i `UpdateAudit()`/`base.MarkAsDelete()`). Task card: `product_phase2_domain_task_card.md`.

**Phase 3 (NOT STARTED ?):** Product CRUD API � `IProductService` + `IProductRepository` (Clean Architecture, G3) + `IImageStorageService` + `CloudinaryImageStorageService` (G8) + 3 DTOs (`ProductDetailDto`, `CreateProductRequest`, `UpdateProductRequest`) + POST/PUT/DELETE/activate/deactivate/image endpoints. Task card: `product_phase3_api_task_card.md`.

**Phase 4 (NOT STARTED ?):** Product Management UI � `/products` page (VanAnDataGrid + VanAForm + VanAnModal) + shared `CurrencyHelper` (`1_Shared/Helpers/`, G4) + prerequisite fixes (VanAnButton disabled bug + VanAnDataGrid render order bug) + NavMenu/AccountingLayout/Sitemap integration. Task card: `product_phase4_ui_task_card.md`.

**Phase 5 (NOT STARTED ?):** QR Code View + Print � QR modal + print 1 QR + batch print (A4 2x5, page break) + QR scan ? KhachLink AddToCart. Task card: `product_phase5_qr_print_task_card.md`.

**Phase 6 (NOT STARTED ?):** E2E Tests � 3 specs (product-crud-flow, product-qr-print, quicksetup-flow) + Page Object + 55 RV tests on VPS khachvip.online (ch? 5-10 ph�t CD deploy). Task card: `product_phase6_e2e_task_card.md`.

**Gap Review (2026-07-14 � COMPLETE ?):** Review master plan ph�t hi?n 5 blocking gaps + 6 minor gaps. T?t c? resolved: G1 (6 task cards created), G2 (TemplateType Guid fix), G3 (IProductService Clean Architecture � user locked), G4 (shared CurrencyHelper � user locked), G5 (UpdateAudit mandatory), G6 (Deactivate vs MarkAsDeleted separate), G7 (tenant selection t? TenantManagement row � user locked), G8 (Cloudinary image upload � user locked), G9 (3 DTOs v?i DataAnnotations), G10 (QR payload reconciled), G11 (no DB migration needed).

**Previous (completed):** Tiered Auth P0-P3 ? � KhachLink Waves 0-4 ? � Accounting PostgreSQL 3 Waves ? � KhachLink UI/UX Fix ? � Payment Webhook Fix ? (pending VPS deploy). See archive for details.

---

## 3. Current Status

- **Branch:** `main`
- **Last commit:** `8b650f2e` [CI] Trigger CD after VPS disk cleanup — reword NATS comment
- **Uncommitted changes:** 7 new task card files + master plan (untracked) � pending commit
- **.NET SDK:** 8.0.422 (system path, CVEs patched, global.json pinned)
- **DB:** SQLite `vanan_shoperp.db` (local dev, business) � PostgreSQL `vanan_accounting` (accounting, Docker `vanan-pg-local`, role `vanan_admin`)
- **Tests (Debug, 2026-07-14):** Arch 38/38 � Core **995/995** � KhachLinkStartup 4/4. Build 0 errors, 296 warnings (pre-existing). guard-check PASS.
- **E2E (2026-07-14):** `khachlink-full-order-flow` PASS local + VPS production.
- **QuickSetup + Product Management (2026-07-14):** Master plan + 6 task cards created. Gap review complete. Ready for IMPLEMENT Phase 1 + Phase 2 (song song).
- **Tiered Auth:** P0-P3 ? (Online RV 14/14 PASS). P4 Facebook ? � P5 Zalo ZNS ? � P6 E2E ?.
- **Payment Webhook Fix:** CODE COMPLETE ?, merged to `main` (`f9b0392f`). **PENDING VPS DEPLOY.**
- **Local infra (Debug):** Docker + PostgreSQL 5432 + NATS 4222 + ShopERP 5003 + KhachLink 5002 + Gateway 5001.
- **Tech debt:** Tier 5 � True Offline Edge. Tier 4 � Roslyn Analyzers dead code.
- **Completed streams (all merged to main):** KhachLink Waves 0-4 � Tiered Auth P0-P3 � Platform SystemAdmin � Stream G/F/D/C/B � Order Lifecycle � Bucket A. See archive for details.

---

## 4. Next Actions

> **RV POLICY UPDATE (2026-07-13):** CI/CD pipeline online d� ?n d?nh. T? nay **Runtime Verification (RV) m?c d?nh th?c hi?n tr�n m�i tru?ng online** (production domain `khachvip.online` / `https://api.khachvip.online`), KH�NG c�n test du?i `localhost`. Local debug ch? cho build/lint. RV cu ghi "ShopERP 5003 + KhachLink 5002 + Gateway 5001 boot local" ? d�ng cho reproduce l?i c? th? khi c?n, kh�ng ph?i default flow. �? m� production: deploy commit ? ch?y RV tr�n domain th?t ? sign-off task card.

**Immediate (QuickSetup + Product Management � IMPLEMENT):**
1. **Phase 1:** Fix QuickSetup orphan page � `feature/quicksetup-fix-phase1` branch. Task card: `quicksetup_phase1_fix_task_card.md`. 9 tasks (rendermode + authorize + IHttpClientFactory + tenant selection t? TenantManagement row + FIX BUG TemplateType Guid + Sitemap link).
2. **Phase 2:** Domain � `Product.Update()` + `Deactivate()` + `Activate()` + `MarkAsDeleted()` (g?i UpdateAudit). `feature/product-mgmt-phase2-domain` branch. Task card: `product_phase2_domain_task_card.md`. 5 tasks. **C� th? ch?y song song v?i Phase 1** (d?c l?p).
3. **Phase 3:** Product CRUD API � IProductService + IProductRepository + Cloudinary + 3 DTOs + POST/PUT/DELETE endpoints. `feature/product-mgmt-phase3-api` branch. Task card: `product_phase3_api_task_card.md`. 23 tasks. **Ph? thu?c Phase 2.**
4. **Phase 4:** Product Management UI � `/products` page + shared CurrencyHelper + prerequisite fixes (VanAnButton + VanAnDataGrid). `feature/product-mgmt-phase4-ui` branch. Task card: `product_phase4_ui_task_card.md`. 20 tasks. **Ph? thu?c Phase 3 + prerequisite fixes.**
5. **Phase 5:** QR Code View + Print � QR modal + batch print A4 2x5. `feature/product-mgmt-phase5-qr-print` branch. Task card: `product_phase5_qr_print_task_card.md`. 11 tasks. **Ph? thu?c Phase 4.**
6. **Phase 6:** E2E Tests � 3 specs + 55 RV tests on VPS khachvip.online (ch? 5-10 ph�t CD deploy). `feature/product-mgmt-phase6-e2e` branch. Task card: `product_phase6_e2e_task_card.md`. 7 tasks. **Ph? thu?c Phase 5.**

**Immediate (Payment Webhook Fix � DEPLOY + VERIFY):**
1. **S6:** Push origin `main` ? trigger CD ? deploy VPS ? verify webhook returns 200 + PostgreSQL `JournalEntries` table has revenue + COGS entries
2. **S7:** Update `manual-test-vps-guide.md` (remove "500 ch?p nh?n du?c" workaround) + sign-off task card

**Immediate (Tiered Auth � P4 next):**
1. **USER ACTION:** Google Cloud Console � delete old leaked OAuth client (leaked secret in commit `72c815e`)
2. **USER ACTION:** Add GitHub secrets `GOOGLE_CLIENT_ID` + `GOOGLE_CLIENT_SECRET` (via GitHub web UI � PAT lacks secrets:write)
3. **Phase 4:** Facebook OAuth � reuse `IGoogleAuthService` pattern
4. **Phase 5:** Zalo ZNS OTP (300d/OTP) + CompositeOtpService (Zalo priority, eSMS fallback)
5. **Phase 6:** E2E Playwright tests � 2 scenarios (social login flow + OTP login flow)

**Deferred (pre-existing, not blocking KhachLink flow):**
1. **Fix Accounting Entries 500 (pre-existing):** Gateway SQLite `AccountingEntries` table missing `AccountCode` column � schema migration gap.
2. **Fix GET /dev/login route ambiguity:** Pre-existing routing conflict.
3. **PostgreSQL migrations sync (2026-07-14 found):** PostgreSQL only has 2 migrations (`InitialCreate`, `AddPlatformUsersTable`) vs SQLite has 6+ (`AddShopFeatureSettingsTable`, `AddPollingIntervalSeconds`, `AddCustomerIdentityLevel`, etc.). `Customers.IdentityLevel` column manually added via ALTER TABLE on VPS. Need proper EF Core migration for PostgreSQL to sync schema. Not blocking E2E (workaround in place).
4. **Payment webhook 400 (pre-existing AuditLog bug):** Payment webhook returns 400 due to AuditLog tenant ID mismatch. Order still marked Paid. Not blocking E2E (test accepts 200 or 400). **Note:** The separate Payment webhook 500 (JournalEntry duplicate key) was FIXED 2026-07-14 (commit `2f084bf1`, branch `fix/payment-webhook-journalentry-duplicate-key`, pending VPS deploy).
5. **Access Matrix Phase 1: ANALYZE** � khi user approve `platform_systemadmin_access_matrix_master_plan.md`
6. **W8: Final Regression + Production Tag** � full regression + `saas-production-v1.0` tag
7. **W6-T2 (user-side):** Email Viettel + MISA for sandbox credentials (1-2 tu?n bottleneck)
8. **W6-T6:** Staging integration tests � gated by `EINVOICE_STAGING_ENABLED=true`, blocked by W6-T2
9. **KhachLink?Gateway QR auth forwarding** � architectural, `QrPaymentModal.razor` needs JWT forwarding
10. **Roslyn Analyzer wiring fix** � Tier 4 debt, low priority (Architecture Tests d? enforce)
11. **EInvoice auto-trigger (TD-KL-01):** Ch? sandbox Viettel/MISA xong m?i l�m

---

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
