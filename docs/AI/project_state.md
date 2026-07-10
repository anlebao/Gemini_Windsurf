# Project State

> **Mục đích:** Single Source of Truth cho AI về trạng thái dự án. BẮT BUỘC đọc đầu mỗi phiên.
> **Archived:** 2026-07-08 — completed waves moved to `docs/AI/project_state_archive.md`

---

## 0. Maintenance Rules

1. One-and-only-one: Mỗi section chỉ tồn tại 1 lần.
2. No contradiction: Một hạng mục chỉ có 1 trạng thái.
3. Ground Truth first: Verify path/branch với codebase trước khi ghi.
4. Now over History: Section 2-4 chỉ mô tả việc ĐANG làm và KẾ TIẾP. Việc xong → gom vào Section 6.
5. Actionable Next Actions: Xóa action đã quá hạn/sai bối cảnh.
6. Stamp every edit: Cập nhật Section 9 mỗi lần sửa.

---

## 1. Project Overview

**Dự án:** Vạn An Accounting System MVP — giải pháp kế toán HKD theo TT 152/2025/TT-BTC.
**Stack:** .NET 8 · EF Core · SQLite · Blazor Server (ShopERP) · Blazor WebAssembly (KhachLink PWA) · SignalR · YARP Gateway · xUnit · Playwright.
**Kiến trúc:** Clean Architecture + DDD + Multi-tenancy. Data flow: `KhachLink (5002) → Gateway (5001) → ShopERP (5003) → SQLite`.

**Modules:** `1_Shared` (Domain) · `2_Gateway` (YARP) · `3_CoreHub` (Services, in-process) · `5_WebApps/ShopERP` (Blazor Server) · `5_WebApps/KhachLink` (Blazor WASM) · `UI.Platform` (Shared components) · `6_Tests/6_Testing`.

**Hard stops:** Domain PURE · `AccountingEntry` immutable · Gateway STATELESS · KhachLink HTTP-only · ShopERP SQLite (Business) + PostgreSQL (Accounting) · ALWAYS dùng UI Platform components.

---

## 2. Current Objective

**[ENTRY POINT CHECK FIX — COMPLETE ✅]**

Fix 4 error groups từ entry point check (12 endpoints fail). Tất cả fixes applied + verified.

**Fixes applied:**
- **Nhóm 1A:** `TenantManagementService.CreateTenantAsync` gọi `SetTenantType(Enterprise_SME)` cho Company tenants + VAS tenant seed vào SQLite (ShopERPDbContext) cho feature flag routing.
- **Nhóm 1B:** `Forbid("msg")` → `StatusCode(403, ...)` trong 4 VAS controllers (BalanceSheets, CashFlow, IncomeStatements, TrialBalances).
- **Nhóm 3A+3B:** New endpoint `POST /dev/login/systemadmin/{tenantId:guid}` — SystemAdmin impersonation với JWT tenant_id GUID thực (không phải "system"). Gateway `ForwardDefaultSelector` đã handle JWT → 401 root cause là `tenant_id="system"` fail `Guid.TryParse`.
- **Nhóm 2:** Confirmed **by design** — 4 AllowAnonymous endpoints require `X-Customer-Token` (customer session), not ASP.NET auth. Seeded customer via OTP flow → all 4 return 200.
- **Npgsql fix:** `AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true)` trong ShopERP + Gateway Program.cs — Domain `DateTime` with `Kind=Unspecified` compatible với PostgreSQL `timestamp with time zone`.
- **VAS seed fix:** `VasSampleDataSeeder` thêm `IgnoreQueryFilters()` cho idempotency check (multi-tenancy filter chặn cross-tenant seed query).
- **AccountingEntries fix:** `DateTime.MinValue` → `new DateTime(2000,1,1)` trong Gateway `GetEntriesByTenant()` (repository reject default DateTime).

**Verification results:**
- Nhóm 1 (VAS Reports): **4/4 → 200 OK** (Balance Sheet, Cash Flow, Income Statement, Trial Balance)
- Nhóm 2 (AllowAnonymous): **4/4 → 200 OK** with X-Customer-Token (by design confirmed)
- Nhóm 3 (Gateway JWT): **2/3 → 200 OK** (HKD Books, Orders). Accounting Entries 500 = pre-existing SQLite schema gap (`no such column: a.AccountCode`), auth passes.
- Nhóm 4 (HKDBooks Cookie): 401 by design (JWT Bearer only)
- Tests: Architecture 38/38 PASS · Core 983/984 PASS (1 flaky SQLite concurrency) · Integration 201/201 PASS

**Previous (completed):** Entry Point Check + Local Infra Boot ✅ (commit `f4eb676`). Accounting PostgreSQL Online — ALL 3 WAVES COMPLETE ✅ (merged to main `33d18fa`).

---

## 3. Current Status

- **Branch:** `main`
- **Last commit:** `f4eb676` Entry point check: SystemAdmin impersonation flow + 2 startup fixes
- **.NET SDK:** 8.0.422 (system path, CVEs patched, global.json pinned)
- **DB:** SQLite `vanan_shoperp.db` (local dev, business) · PostgreSQL `vanan_accounting` (accounting, Docker `vanan-postgres-local`, role `vanan_admin`)
- **Tests (Release):** 1222/1223 PASS — 38 Architecture + 983 Core + 201 Integration (1 flaky SQLite concurrency test, passes in isolation). Verified 2026-07-10.
- **Local infra (Debug):** Docker Desktop + PostgreSQL 5432 + NATS 4222 + Gateway 5001 + ShopERP 5003 — all healthy. SystemAdmin login + impersonation flow verified. VAS reports 200 OK. Customer OTP flow verified.
- **Tech debt:** Tier 5 recorded — True Offline Edge (Accounting via HTTP), task card `true_offline_edge_accounting_http_task_card.md`. Trigger: true 2-server Edge deployment. Severity: Low (not triggered — all compose files have PostgreSQL on same machine).
- **Entry point check (2026-07-10):** 4 error groups ALL FIXED. Nhóm 1 (VAS reports) 4/4 → 200. Nhóm 2 (AllowAnonymous) 4/4 → 200 with customer token (by design). Nhóm 3 (Gateway JWT) 2/3 → 200 (Accounting Entries 500 = pre-existing SQLite schema gap). Nhóm 4 (HKDBooks Cookie) by design.
- **Completed streams (all merged to main):**
  - Platform SystemAdmin ✅ (commit `dde219e`) — F1-F5 fix + docs pending commit
  - Stream G: SaaS Production Hardening W0-W7 ✅ (W8 pending — final regression + tag)
  - Stream F: VAS Enterprise Reports W0-W9 ✅ (tag `saas-production-v1.0`)
  - Stream D: HKD Book Accounting Fix W0-W8 ✅
  - Stream C: ShopERP UI Fix W0-W6 ✅
  - Stream B: E2E Test Cleanup W0-W8 ✅
  - Order Lifecycle W-1→W5 + edge cases ✅
  - Bucket A: Guest Checkout + PostgreSQL migration ✅
  - E2E Fix: qr-payment-ui 6/6 PASS ✅

---

## 4. Next Actions

**Immediate:**
1. **Fix Accounting Entries 500 (pre-existing):** Gateway SQLite `AccountingEntries` table missing `AccountCode` column — schema migration gap. Auth passes (JWT validated, policy OK), 500 from `SQLite Error 1: 'no such column: a.AccountCode'`.
2. **Fix GET /dev/login route ambiguity:** Pre-existing routing conflict between `LoginInfo` (GET /dev/login) and Blazor `/Index` page. POST works fine.

**Deferred:**
3. **Access Matrix Phase 1: ANALYZE** — khi user approve `platform_systemadmin_access_matrix_master_plan.md`
4. **W8: Final Regression + Production Tag** — full regression + `saas-production-v1.0` tag
5. **W6-T2 (user-side):** Email Viettel + MISA for sandbox credentials (1-2 tuần bottleneck)
6. **W6-T6:** Staging integration tests — gated by `EINVOICE_STAGING_ENABLED=true`, blocked by W6-T2
7. **KhachLink→Gateway QR auth forwarding** — architectural, `QrPaymentModal.razor` needs JWT forwarding
8. **Roslyn Analyzer wiring fix** — Tier 4 debt, low priority (Architecture Tests đủ enforce)

---

## 5. Active Architecture Decisions

| Decision | Lý do |
|---|---|
| CoreHub = in-process background service trong Gateway | Monolith Phase 1-2 (Option B approved 2026-07-05) |
| Gateway = DI composition root cho CoreHub | Program.cs đăng ký CoreHub DbContext/Services |
| ShopERP = SQLite (Business) + PostgreSQL (Accounting) | ADR-001: accounting always online. ShopERPDbContext (SQLite) cho Business/Platform, VanAnDbContext (PostgreSQL) cho Accounting qua IAccountingDbContext. **ALL 3 WAVES COMPLETE 2026-07-10** — interface split + service swap + DI + docker-compose + 4 Architecture Tests (Rule J/K/L/M) + test fixes. 1223/1223 tests PASS. ✅ ENFORCED. |
| CustomerToken = `IDataProtector` | Tránh library mới |
| `AccountingEntry` immutable, Reversal Entry | Audit trail bất khả xâm phạm |
| Multi-tenancy `TenantId` filter mọi layer | Data isolation per HKD |
| EF Core Migrations = official schema management | Stream E — replace `EnsureCreated` for production |
| HKD Data Source = Option A (query AccountingEntries directly) | Wave 0.5 — AccountingEntry is immutable SSoT |
| DOCX export = DocumentFormat.OpenXml + XLSX = EPPlus 7.6.1 | Wave 0 T9 — user approved |
| **[NEW] PlatformUser = Infrastructure entity (non-tenant)** | Precedent: AccountChartEntity — cross-tenant admin, no BaseEntity |
| **[NEW] Execution Discipline Rules (EDR)** | 8 EDR rules in `platform_systemadmin_master_plan.md` Section 7 — ràng buộc execution chống tái diễn deviations |
| **[NEW] Access Matrix = verification plan riêng** | `platform_systemadmin_access_matrix_master_plan.md` — 4 phases, 5 EDR-AM rules, depends on F1-F5 COMPLETE |
| **[NEW] Dual Deployment Modes (2026-07-09)** | 2 mode production — xem Section 5a bên dưới |

### 5a. Deployment Modes (Production)

**Mode 1 — SaaS (online, all-in-one VPS):**
- Compose: `docker-compose.prod.yml`
- Tất cả module chạy trên 1 VPS: PostgreSQL + NATS + Seq + Gateway (in-process CoreHub) + ShopERP + KhachLink + Nginx
- Gateway → PostgreSQL (central data)
- ShopERP → SQLite local (offline-first, sync qua NATS Outbox khi online)
- KhachLink → Gateway (HTTP)
- Use case: SaaS multi-tenant, khách hàng không cần edge node riêng

**Mode 2 — Edge (tách biệt, offline-capable):**
- Compose: `docker-compose.edge.yml`
- **Server A (Edge):** ShopERP + SQLite + NATS sync worker — chạy độc lập, không cần PostgreSQL
- **Server B (Central):** Gateway (in-process CoreHub) + PostgreSQL + KhachLink + Nginx
- Sync: ShopERP Outbox → NATS → Gateway → PostgreSQL
- Use case: Cửa hàng offline-first, internet không ổn định, data local tại edge
- ADR-001: SQLite local + NATS sync + PostgreSQL cloud (accounting always online)

**Lưu ý quan trọng (verified 2026-07-09):**
- ShopERP dùng SQLite trong CẢ 2 mode (Program.cs luôn `UseSqlite`, không có `UseNpgsql` path)
- `docker-compose.prod.yml` ShopERP không set `SQLITE_DB_PATH` → fallback local file trong container
- `docker-compose.edge.yml` ShopERP set `SQLITE_DB_PATH=Data Source=/data/shoperp.db` + volume `shoperp_sqlite_data`
- `docker-compose.edge.yml` có thêm `shoperp-nats-sync` worker (command `--sync-worker`) để poll Outbox + publish NATS

---

## 6. History Log (compressed — see git log + archive for details)

* [2026-07-09] **ACCOUNTING POSTGRESQL ONLINE — WAVE 1 COMPLETE.** User approved "Full Wave 1 as written" (merge Wave 2 service-swap into Wave 1). INVESTIGATE found ~98 compile-error sites if removing 6 DbSets from IVanAnDbContext (task card §6.5 threshold >20 met). Implementation: (1) Created `IAccountingDbContext` (6 accounting DbSets), (2) Removed 6 accounting DbSets from `IVanAnDbContext` (now 19 business-only), (3) `VanAnDbContext` implements both interfaces, (4) `ShopERPDbContext` implements IVanAnDbContext only (removed 6 accounting + HKDBooks DbSet), (5) SWAP 11 files (TrialBalanceService, IncomeStatementService, BalanceSheetService, CashFlowStatementService, AccountChartService, PeriodClosingService, DataProviderService, AccountingEntryRepository, HKDBookRepository, AuditLogRepository, AccountChartSeeder), (6) DUAL-INJECT 3 files (TenantConversionService, SmartPreAggregationService, HKDBookGenerationService — keep IVanAnDbContext for Tenants, add IAccountingDbContext for accounting), (7) DI registration in ShopERP Program.cs (VanAnDbContext with UseNpgsql + IAccountingDbContext), (8) AccountingConnection in appsettings (base/dev/prod), (9) Fix 3 test files (PeriodClosingPersistenceTests, VasFeatureFlagTests, SmartPreAggregationServiceWave2Tests). Plan discrepancies fixed: 25 DbSets not 27 (19 business not 21), SmartPreAggregationService is dual-inject not direct-inject, DataProviderService added to SWAP list. Build 0 errors Debug. Guard-check PASS. Commit `9d589bd`. **Branch:** `feature/accounting-pg-wave1-interface-split`.

* [2026-07-09] **ACCOUNTING POSTGRESQL ONLINE — MASTER PLAN v2 (OPTION B) + 3 TASK CARDS.** Review v1 (697 dòng, 10 phases, Option A throw stubs) phát hiện 3 bugs + 1 over-engineering tendency. User chọn Option B (split interface, compile-time safety). Rewrite master plan theo template `einvoice_provider_rewrite_master_plan.md`: 246 dòng, 3 waves (interface split → services/DI/config → tests/verify). Tạo 3 task cards chi tiết. Fix 3 bugs: Rule J exclude repo-inject services, Rule K đơn giản hóa (no throw stubs), AccountChartSeeder callers audit. **Branch:** `main`.

* [2026-07-09] **ACCOUNTING POSTGRESQL ONLINE — MASTER PLAN + DEBT AUDIT.** Điều tra git history phát hiện vi phạm ADR-001 từ 2026-06-03 (commit `957ac95`): Accounting module chạy trên SQLite thay vì PostgreSQL. Root cause: ShopERP Program.cs hardcoded `UseSqlite()`, accounting services inject `IVanAnDbContext` → resolve `ShopERPDbContext` (SQLite). PR #55 (`754e2b3`) cố sửa nhưng crash, PR #56 (`cf05eb1`) revert + hiểu sai ADR-001. 10 services + 3 repositories affected. Roslyn Analyzers audit: 9 analyzers = dead code (wiring sai — chỉ reference bởi project trống, 0 test, 3 outdated Gateway Option B, path separator bug). Ghi debt Tier 4 trong `TECHNICAL_DEBT_LEDGER.md`. User quyết định skip toàn bộ analyzers, dùng Architecture Tests thay. Master plan tạo: `accounting_postgresql_online_master_plan.md` — 10 phases (IAccountingDbContext interface, 13 services/repos update, 4 Architecture Tests Rule J/K/L/M). Awaiting approval. **Branch:** `main`.

* [2026-07-08] **PLATFORM SYSTEMADMIN REVIEW + F1-F5 FIX** — Post-implementation review phát hiện 5 deviations. Fixed F1-F5: `[AllowAnonymous]` on Login (auth deadlock), integration test idempotent (UNIQUE constraint), unit tests re-created (5/5 PASS SQLite in-memory), `Seed:SysAdminPassword` config + production guard, AuditTrail `Policy="SystemAdmin"`. Updated master plan with EDR-1..EDR-8 (Execution Discipline Rules). Created Access Matrix master plan + task card (4 phases, 12 tasks, EDR-AM-1..EDR-AM-5). Build 0 errors Debug+Release. Tests: 1174/1174 PASS (Core 957 + Arch 34 + Integration 183). Pending commit.
* [2026-07-08] **PLATFORM SYSTEMADMIN IMPLEMENT COMPLETE** — Implemented T1-T9: PlatformUser entity (non-tenant Infrastructure entity), PlatformUserConfiguration, 3 DbContext DbSet registrations, EF Migration (AddPlatformUsersTable), PlatformUserLoginService (BCrypt verify + JWT mint), PlatformUserLoginController (POST /api/platform/login, production, no #if DEBUG), DI registration + 3 policy updates (OwnerOnly, StoreManagement, StaffOrAbove add SystemAdmin) + seed sysadmin@vanan.vn, unit + integration tests. Build 0 errors, guard pass. Commit `dde219e`.
* [2026-07-08] **PLATFORM SYSTEMADMIN PLANNING COMPLETE** — Investigated 2 role systems (`UserRole` tenant-scoped vs `PlatformRole` cross-tenant), `DevLoginController` (`#if DEBUG`), `DemoUser` (rejects `TenantId=Empty`). User chose pattern 2 lớp. Created master plan + task card (9 tasks, 12 files). Commit `792cc3f`.
* [2026-07-07] **SDK 8.0.422 + TRIAGE + E2E FIX + BUCKET A + W6 GOLDEN TESTS** — 14 commits total: SDK to system path (CVEs patched), 5 pre-existing issues triaged, qr-payment-ui 6/6 PASS (`24718b8`), guest checkout + PostgreSQL migration (`310f3da`+`8867dbc`), 21/22 golden tests PASS (`fd7b038`). See archive for details.
* [2026-07-05] **STREAM G W0-W7 + STREAM F W0-W9 COMPLETE** — SaaS hardening (Gateway Option B, secrets, package security, CI restore, UI tests, period closing, e-invoice rewrite, tech debt, Docker hardening). VAS reports (10 waves, 4 BCTC, 124 accounts, feature flag, conversion service). All merged to main. 1152/1152 tests PASS.
* [2026-07-04] **STREAM D W0-W8 + STREAM E COMPLETE** — HKD Book Accounting Fix (12 waves, TT 152 compliance, 7 templates, DOCX/XLSX export). DB Migration Strategy (EF Core Migrations enabled). All merged to main.
* [2026-07-03] **STREAM B W0-W8 + STREAM C W0-W6 COMPLETE** — E2E Test Cleanup (8 waves, 7 anti-patterns). ShopERP UI Fix (6 waves, 23 .razor files, UI Platform compliance). All merged to main.
* [2026-07-02] **ORDER LIFECYCLE + ONBOARDING + PLANNING** — Order Lifecycle W-1→W5 merged. Tenant Onboarding 6 waves merged. ShopConfig Refactor 3 phases. EInvoice + UI Fix + E2E Cleanup planning.
* **Older:** See `docs/AI/project_state_archive.md` for full history (Wave 8-16, auth, CI/CD, etc.)

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
=== SaaS Mode (docker-compose.prod.yml) — all-in-one VPS ===

KhachLink (5002) → Gateway (5001) → ShopERP (5003) → SQLite (local)
                        ↓
              [in-process CoreHub services]
                        ↓
                  PostgreSQL (central data)

=== Edge Mode (docker-compose.edge.yml) — tách biệt 2 server ===

Server A (Edge):                      Server B (Central):
  ShopERP → SQLite (local)              Gateway → PostgreSQL
  shoperp-nats-sync worker              [in-process CoreHub]
       ↓ NATS Outbox sync ↓
  ───────────────→ NATS ───────────────→ Gateway
                                         KhachLink → Gateway (HTTP)
```

**Auth:** Cookie (Blazor Server) + JWT Bearer (API). `DevLoginController` (`#if DEBUG`) for E2E. BCrypt work factor 12.
**Roles:** `UserRole` (tenant-scoped: Owner/StoreKeeper/Guard/Staff/Masterchef) · `PlatformRole` (cross-tenant: SystemAdmin)

---

## 9. Maintenance Log

* **2026-07-10 — ENTRY POINT CHECK FIX — ALL 4 ERROR GROUPS FIXED.** Fixed 12 failing endpoints from entry point check. Nhóm 1A: `TenantManagementService.CreateTenantAsync` calls `SetTenantType(Enterprise_SME)` for Company tenants + VAS tenant seed into SQLite for feature flag routing. Nhóm 1B: `Forbid("msg")` → `StatusCode(403, ...)` in 4 VAS controllers. Nhóm 3B: New `POST /dev/login/systemadmin/{tenantId:guid}` endpoint for SystemAdmin impersonation with real tenant_id GUID. Npgsql `EnableLegacyTimestampBehavior` switch in ShopERP + Gateway. `VasSampleDataSeeder` idempotency check with `IgnoreQueryFilters()`. `AccountingEntriesController` DateTime.MinValue fix. Verification: VAS reports 4/4 → 200, AllowAnonymous 4/4 → 200 with customer token (by design), Gateway JWT 2/3 → 200 (Accounting Entries 500 = pre-existing schema gap). Tests: Arch 38/38, Core 983/984 (1 flaky), Integration 201/201. **Branch:** `main`.

* **2026-07-10 — ENTRY POINT CHECK + LOCAL INFRA BOOT.** Khởi động full stack local Debug: Docker Desktop + PostgreSQL (vanan-postgres-local:5432) + NATS (4222) + Gateway (5001) + ShopERP (5003) + KhachLink (5002). Login SystemAdmin qua `POST /dev/login/systemadmin` (DEBUG-only endpoint). Extracted 150+ routes từ 45 controllers (21 ShopERP + 24 Gateway) qua subagent. Test 57 entry points: Phase 1 (no impersonation) 18/57 OK; Phase 2 (with impersonation) 17/29 OK. Fixes applied: (1) Gateway `IAccountingDbContext` DI registration trong `2_Gateway/Program.cs` (Wave 1-3 gap — Gateway crashed on startup), (2) `VanAnDbContext.ApplyMultiTenancyFilters` bỏ throw khi TenantId empty trong `OnModelCreating` (break startup khi no HTTP context). Created 2 test tenants qua API: HKD (HouseholdBusiness) + Company (Enterprise). Impersonation flow verified: Login → List Tenants → Impersonate → Test → Exit. 4 error groups documented trong `docs/AI/entry_point_check_4_error_groups.md`: Nhóm 1 (4×500 VAS reports — TenantType null + Forbid misuse), Nhóm 2 (4×401 AllowAnonymous — cần điều tra), Nhóm 3 (3×401 Gateway JWT scheme mismatch), Nhóm 4 (1×401 HKDBooks Cookie — by design). **Branch:** `main`.

* **2026-07-10 — WAVE 3 COMPLETE — ALL 3 WAVES DONE.** Added 4 Architecture Tests (Rule J/K/L/M) to `ArchitectureRulesTests.cs`: Rule J (accounting services inject IAccountingDbContext), Rule K (ShopERPDbContext no accounting DbSets), Rule L (docker-compose AccountingConnection), Rule M (ShopERP Program.cs UseNpgsql). Fixed Rule C (ShopERP exempt — now legitimately uses Npgsql for accounting, only KhachLink checked). Fixed W5-ARCH-003 (used MetadataReader instead of Assembly.LoadFrom to avoid ReflectionTypeLoadException when loading Debug assembly from Release test run). Fixed 6 integration test factories (added IAccountingDbContext → VanAnDbContext DI registration): CustomWebApplicationFactory, AuthRealWebApplicationFactory, GatewayWebApplicationFactory, IntegrationTestBase (2 sites), EInvoiceDISmokeTests, TestDatabaseFixture. Full verification: 38 Architecture + 984 Core + 201 Integration = 1223/1223 PASS (Release). Guard-check ALL CHECKS PASSED. Updated: master plan (Wave 3 ✅), Wave 3 task card (✅), project_state.md (§2/3/4/5/9). **Branch:** `feature/accounting-pg-wave1-interface-split`.

* **2026-07-10 — WAVE 2 RESIDUAL COMPLETE.** Added `ConnectionStrings__AccountingConnection` env var to `docker-compose.yml` + `docker-compose.prod.yml` + `docker-compose.edge.yml` shoperp service. Uses `${POSTGRES_DB:-VanAnCoreHub}` (matches postgres service default + Gateway — corrected task card's `vanan_accounting` default mismatch bug). Added `ACCOUNTING_CONNECTION_STRING` optional override to `.env.example`. Build: 0 errors. Guard-check: domain/arch/Roslyn/build PASS; Architecture.Tests 1 fail (Rule C Npgsql — pre-existing from Wave 1, fix in Wave 3 W3-T5). Updated: master plan (Wave 2 ✅), Wave 2 task card (SC7b ✅, §6.6 ✅, health check Q1 resolved), project_state.md (§2/4/5/9). **Branch:** `feature/accounting-pg-wave1-interface-split`.

* **2026-07-09 — DOCS SYNC + TIER 5 DEBT RECORDED.** Synced all docs with Wave 1 source code: project_state.md (§2/3/4/5/6/9), master_plan.md (Wave 1 ✅, Wave 2 🟡, Cross-Wave discrepancies fixed), 3 task cards (Wave 1 ✅, Wave 2 🟡, Wave 3 ⏳). Commit `2fc2ce6`. Then: user reviewed proposed "Option C with graceful degradation" for Edge mode — rejected (7 points: throw stub = Option A rejected, Service Locator anti-pattern, ADR-001 violation via empty data, problem doesn't exist yet, breaks 17 files, pattern churn, false "production-ready" claim). User approved simpler approach: add env var to 3 compose files, no code changes. Recorded Tier 5 debt: true offline Edge (2-server) accounting via Gateway HTTP API. Task card `true_offline_edge_accounting_http_task_card.md` (158 dòng, 7 sections, impact analysis reserve). Debt ledger Tier 5 added. Commit `ebda286`. Updated Section 3 (last commit, tech debt note), 9 (maintenance log). **Branch:** `feature/accounting-pg-wave1-interface-split`.

* **2026-07-09 — WAVE 1 COMPLETE.** User approved "Full Wave 1 as written" (merge Wave 2 service-swap). INVESTIGATE: ~98 compile-error sites (task card §6.5 threshold >20 met). Implementation: IAccountingDbContext created (6 DbSets), 6 removed from IVanAnDbContext (19 business-only), VanAnDbContext implements both, ShopERPDbContext business-only. 11 SWAP + 3 DUAL-INJECT files. DI: VanAnDbContext UseNpgsql + IAccountingDbContext registered in ShopERP Program.cs. AccountingConnection in appsettings. 3 test files fixed. Plan discrepancies: 25 DbSets not 27 (19 business not 21), SmartPreAggregationService dual-inject not direct-inject, DataProviderService added. Build 0 errors. Guard-check PASS. Commit `9d589bd`. Updated Section 2 (objective — Wave 1 ✅, Wave 2 partial 🟡), 3 (status — branch, commit, DB), 4 (next actions — docker-compose + Wave 3 + merge), 5 (decisions — ADR-001 Wave 1 complete), 6 (history), 9 (maintenance log). **Branch:** `feature/accounting-pg-wave1-interface-split`.

* **2026-07-09 — ACCOUNTING POSTGRESQL ONLINE MASTER PLAN v2 + 3 TASK CARDS.** Review v1 master plan (697 dòng, 10 phases, Option A throw stubs) phát hiện 3 bugs kỹ thuật + 1 over-engineering tendency (Option A ISP violation). User chọn Option B (split interface, compile-time safety). Rewrite master plan theo template `einvoice_provider_rewrite_master_plan.md`: 246 dòng (-65%), 3 waves (Wave 1: interface split + DbContext, Wave 2: services/repos + DI + config, Wave 3: architecture tests + verify). Tạo 3 task cards chi tiết. Fix 3 bugs: (1) Rule J exclude repo-inject services (AccountingEntryService, ReversalService, AuditTrailService, HKDBookService), (2) Rule K đơn giản hóa (Option B no throw stubs → string contains check), (3) AccountChartSeeder callers audit (signature change). Updated Section 2 (objective v2), 3 (status), 4 (next actions 3 waves), 5 (decisions), 6 (history), 9 (maintenance log). **Branch:** `main`.

* **2026-07-09 — ACCOUNTING POSTGRESQL ONLINE MASTER PLAN + ANALYZER DEBT AUDIT.** Git history investigation found ADR-001 violation since 2026-06-03 (commit `957ac95`): accounting on SQLite instead of PostgreSQL. 10 services + 3 repos affected. Roslyn Analyzers audit: 9 analyzers dead (wiring, 0 test, 3 outdated). Debt Tier 4 recorded in `TECHNICAL_DEBT_LEDGER.md`. Master plan created: `docs/AI/tasks/accounting_postgresql_online_master_plan.md` (10 phases, IAccountingDbContext + 4 Architecture Tests). Updated Section 2 (objective), 4 (next actions), 5 (decisions), 6 (history). Hard stop updated: "ShopERP SQLite (Business) + PostgreSQL (Accounting)". **Branch:** `main`.

* **2026-07-09 — DOCKER CONFIG FIX + DEPLOYMENT MODES RECORDED.** Fixed 3 user-reported errors: (1) docker-compose.yml port swap (gateway=5010→5001, shoperp=5002→5003, khachlink=5003→5002), (2) ShopERP 500 crash — SQLite volume stale (no PlatformUsers table), fixed by adding DesignTimeDbContextFactory + InitialCreate migration + switching EnsureCreatedAsync→MigrateAsync, (3) KhachLink 500 crash — missing Gateway__BaseUrl in Production env, fixed by adding env var + appsettings default. Also: service-worker.js cache v2→v3, pwa.js controllerchange auto-reload, .env.example port comments. Removed dead CoreHub:BaseUrl from Gateway appsettings. Fixed ProductsController/DashboardController injecting VanAnDbContext (not registered) → switched to IVanAnDbContext. Updated VA-ARCH-001 to allow ShopERP/Migrations. Recorded Dual Deployment Modes (SaaS + Edge) in Section 5a. Commits: `9b2d209`, `b9ed4a2`. **Branch:** `main`.

* **2026-07-08 — PLATFORM SYSTEMADMIN REVIEW + F1-F5 FIX.** Post-implementation review: 5 deviations found. Fixed F1-F5 (AllowAnonymous, idempotent test, unit tests re-created, config password, AuditTrail role). Master plan updated with EDR-1..EDR-8. Access Matrix master plan + task card created (4 phases, 12 tasks). Build 0 errors Debug+Release. Tests: 1174/1174 PASS (Core 957 + Arch 34 + Integration 183). Pending commit. **Branch:** `main`.

* **2026-07-08 — PLATFORM SYSTEMADMIN IMPLEMENT COMPLETE.** Implemented T1-T9: PlatformUser entity (non-tenant Infrastructure entity), PlatformUserConfiguration, 3 DbContext DbSet registrations, EF Migration (AddPlatformUsersTable), PlatformUserLoginService (BCrypt verify + JWT mint), PlatformUserLoginController (POST /api/platform/login, production, no #if DEBUG), DI registration + 3 policy updates (OwnerOnly, StoreManagement, StaffOrAbove add SystemAdmin) + seed sysadmin@vanan.vn, unit + integration tests. Build 0 errors, guard pass. Commit `dde219e`. **Branch:** `main`.

* **2026-07-08 — PROJECT STATE ARCHIVED.** Reduced `project_state.md` from 528→~200 lines. Moved completed waves (Stream G W0-W7, Stream F W0-W9, Stream D W0-W8, Stream C W0-W6, Stream B W0-W8, Order Lifecycle, Bucket A, E2E Fix, Golden Tests, older waves) to `docs/AI/project_state_archive.md`. Kept: current objectives, active decisions, next actions, recent history (2026-07-02 onward). **Branch:** `main`.

* **2026-07-07 — SDK 8.0.422 + TRIAGE + E2E FIX + BUCKET A.** 14 commits: SDK to system path (CVEs patched), 5 pre-existing issues triaged, qr-payment-ui 6/6 PASS, guest checkout + PostgreSQL migration, 21/22 golden tests PASS. See archive for full details. **Branch:** `main`.
