# Project State

> **Mục đích:** Single Source of Truth cho AI về trạng thái dự án. BẮT BUỘC đọc đầu mỗi phiên.
> **Archived:** 2026-07-26 + 2026-08-03 — All completed objectives + full history/maintenance log moved to `docs/AI/project_state_archive.md`

---

## 0. Maintenance Rules

1. One-and-only-one: Mỗi section chỉ tồn tại 1 lần.
2. No contradiction: Một hạng mục chỉ có 1 trạng thái.
3. Ground Truth first: Verify path/branch với codebase trước khi ghi.
4. Now over History: Section 2-4 chỉ mô tả việc ĐANG làm và KẾ TIẾP. Việc xong gom vào archive.
5. Actionable Next Actions: Xóa action đã quá hạn/sai bối cảnh.
6. Stamp every edit: Cập nhật Section 10 mỗi lần sửa.

---

## 1. Project Overview

**Dự án:** Vạn An Accounting System MVP — giải pháp kế toán HKD theo TT 152/2025/TT-BTC.
**Stack:** .NET 8 — EF Core — SQLite — Blazor Server (ShopERP) — Blazor WebAssembly (KhachLink PWA) — SignalR — YARP Gateway — xUnit — Playwright.
**Kiến trúc:** Clean Architecture + DDD + Multi-tenancy. Data flow: `KhachLink WASM (5002) -> Gateway (5001) -> ShopERP (5003) -> SQLite`.

**Modules:** `1_Shared` (Domain + Services contracts) — `2_Gateway` (YARP) — `3_CoreHub` (Services, in-process) — `5_WebApps/ShopERP` (Blazor Server) — `5_WebApps/KhachLink` (Blazor WASM, served by nginx) — `UI.Platform` (Shared components) — `6_Tests`.

**Hard stops:** Domain PURE — `AccountingEntry` immutable — Gateway = Order Creator + Routed Async Delivery (Option C) — KhachLink HTTP-only — ShopERP SQLite (Business) + PostgreSQL (Accounting) — ALWAYS dùng UI Platform components.

---

## 2. Current Objective

**Tenant Management + Accounting UI Fixes (4 Bugs)** — ✅ ALL 4 PHASES COMPLETE + DEPLOYED + VPS VERIFIED (HTTP-level). Browser functional testing for authenticated users on VPS is the only remaining step.

- **Master plan:** `docs/AI/tasks/tenant_accounting_fixes/tenant_accounting_fixes_master_plan.md` (4 phases: P0 Bug 3 debug, P1 Bug 2A hide HKD menu, P2 Bug 2B VAS export, P3 Bug 1 edit BusinessType)
- **Phase 0 (Bug 3):** ✅ COMPLETE — commit `89fb90b6`, CI PASS (1253s), CD SUCCESS (6min), VPS HTTP-level RV 7/7 PASS. Root cause: `ScopedDataProvider.cs:86,126` sync-over-async deadlock in Blazor Server. Fix: `Task.Run` wrapper. Tech debt TD-ASYNCDP-001 logged for proper async-native fix.
- **Phase 1 (Bug 2A):** ✅ COMPLETE — commit `5f21ab36`, CI PASS (923s), CD SUCCESS (6min), VPS HTTP-level RV 5/5 PASS. Hide "Sổ HKD (TT 152)" menu for Company tenants via `_isHkd` conditional in `AccountingLayout.razor`. E2E test `hkd-menu-visibility.spec.ts`.
- **Phase 2 (Bug 2B):** ✅ COMPLETE — commit `c0fbcef6`, CI PASS (1218s), CD SUCCESS (5min), VPS HTTP-level RV 7/7 PASS. New `IFinancialReportExportService` (Open XML SDK DOCX + EPPlus XLSX) + DI + 4 UI pages (BalanceSheet/IncomeStatement/CashFlowStatement/TrialBalance) with "📄 Xuất DOCX" + "📊 Xuất XLSX" buttons. E2E test `vas-export.spec.ts`.
- **Phase 3 (Bug 1):** ✅ COMPLETE — commit `424c3aa7`, CI PASS (1229s, 1261+17+39+144 tests 0 failures), CD SUCCESS (5min), VPS HTTP-level RV 6/6 PASS. Domain `Tenant.ChangeBusinessType()` + `TenantBusinessTypeChangedEvent` (8 unit tests PASS). Service `ChangeBusinessTypeAsync()` with AccountingEntry data integrity guard (IAccountingDbContext). Gateway API `PUT /api/v1/tenants/{id}/business-type` (409 if accounting data exists). UI Edit modal: BusinessType dropdown + HKDGroup + Reason field. E2E test `tenant-edit-businesstype.spec.ts`.

**Last completed:** Tenant Management + Accounting UI Fixes — ALL 4 PHASES COMPLETE + DEPLOYED + VPS VERIFIED (commits `89fb90b6` → `424c3aa7`, 2026-08-03).

**Recently completed (full detail in archive):**
- **KhachLink LoyaltyMode UI Hide** — COMPLETE + VPS VERIFIED (RV 10/10 PASS, commit `133e8061`, CD run `30789469902`, 2026-08-03). When SystemAdmin sets LoyaltyMode=Silo, KhachLink hides all "Ví liên minh" UI (NavMenu desktop+mobile tabs, LoyaltyCard link, AllianceWallet page shows "Tính năng liên minh đang tắt"). New public endpoint `GET /api/loyalty/mode` (anonymous) returns global mode. New `LoyaltyModeHttpService` (cached 5 min, defaults Silo on error). 8 files changed. CI PASS (1347s). CD SUCCESS (5m35s). VPS RV 10/10 PASS — endpoint returns `{"mode":"Silo"}`, WASM fresh, all pages 200.
- **KhachLink UI Polish** — COMPLETE (commits `29180a53` + `482e481f`, 2026-08-03). (1) NavMenu.razor: removed 4 duplicate footer icons (Giỏ hàng, Điểm thưởng, Nhiệm vụ, Đổi điểm) — already in header. (2) Home.razor: fixed store search box — `@bind:event="oninput"` (was `onchange` → query empty on Enter) + restructured render tree (search box always visible, was hidden after search). Build 0 errors.
- **Order Status Sync Fix** — COMPLETE (commit `29180a53`, 2026-08-03). Payment status + completion status not propagating to KhachLink. ConfirmPaymentAsync now enqueues OrderPaymentStatusChanged outbox event. SyncOrderCompletedAsync fixed camelCase property names. Added order.payment.status.changed case in DataSyncSubscriber.
- **UI Fix Batch (5 issues)** — COMPLETE + VPS VERIFIED (RV 7/7 PASS, commit `6179fdd7`, 2026-08-03). 5 UI issues fixed across ShopERP + KhachLink + UI.Platform. 11 files modified. Pre-push CI ALL PASSED (994s). CD SUCCESS. VPS RV 7/7 PASS.
- **Loyalty Consistency Fix** — COMPLETE + VPS VERIFIED (RV 37/37 PASS, 2026-08-03). 9 bugs (BUG #0-#9) fixed via 2-layer execution. Architecture: Option B (HTTP proxy + cache + idempotency, multi-VPS ready). D1-D5 all APPROVED.
- **Loyalty Alliance System** — ALL 7 PHASES COMPLETE + DEPLOYED + VERIFIED (commits `2e2eaa4e` → `25a70b9f`, RV 14/14 PASS). Phase 1 (Domain+EF+Migration) → Phase 2A-2C (Mode routing + Wallet + Sync) → Phase 3A-3B (Admin API + Customer API) → Phase 4 (Mode Switch Migration) → Phase 5A-5B (Admin UI + Customer UI) → Phase 6A-6B (Unit + E2E tests) → Phase 7 (VPS RV). FULLY OPERATIONAL — tenant currently in Silo mode, Alliance infrastructure ready.
- **SystemAdmin Guide Review** — COMPLETE + VPS VERIFIED (commit `9743054a`, RV 24/24 PASS).
- **VPS Bug Fix Batch (3 bugs)** — COMPLETE + VPS VERIFIED (commits `141b944b` + `c47b89d6`).
- **Community Commerce Sprint 7** — Commerce Mode Toggle — COMPLETE + VPS VERIFIED (RV7 18/18 PASS, commit `3fba1e8d`).
- **Community Commerce Sprint 6** — Admin + Fraud Review + Polish + Legal v1.2 (commit `e73453b9`, RV 13/14 PASS).
- **Community Commerce Sprint 5** — Wallet + COD + Settlement + Shop-Confirmed Advance (commit `2c038fc0`, RV 34/35 PASS).
- **Community Commerce Sprint 4** — Salesman + Composite QR Referral + Per-Product Commission + App-Install Bonus + Risk Scoring + FraudFlag (commit `b78b71d5`, RV 26/26 PASS).
- **Community Commerce Sprint 3** — Chat (Customer ↔ Shipper) (commit `cd1b200f`, RV 18/18 PASS).
- **Community Commerce Sprint 2** — Delivery Workflow + GPS Tracking (commit `a3f4c25e`, RV 19/19 PASS).
- **Community Commerce Sprint 1** — Nearby Orders + Accept (commits `4e7d9507` + `64d3bf77` + `76d82e2c`).
- **Community Commerce Sprint 0** — Foundation: 11 Domain entities + 42 tests + migration (commits `e1a75bbf` + `f563e415`, RV 18/18 PASS).

> **Full detail** (file lists, RV step-by-step, plan deviations) for all completed objectives: see `docs/AI/project_state_archive.md` → "Archived 2026-08-03".

---

## 3. Current Status

- **Branch:** `main`
- **Last commit:** `424c3aa7` [TENANT-FIX P3] add SystemAdmin Edit Tenant BusinessType (Bug 1)
- **Working tree:** Clean (all 4 phases committed + pushed). Branch in sync with origin/main.
- **.NET SDK:** 8.0.422
- **DB:** SQLite `vanan_shoperp.db` (business) + PostgreSQL `VanAnCoreHub` (accounting + Gateway + Community tables)
- **Build:** 0 errors across full solution. CI pre-push ALL PASSED.
- **VPS:** 7-8 containers healthy. CD deploys automatically on push to main. Domains: `khachvip.online` (ShopERP), `diemthuong.khachvip.online` (KhachLink), `api.khachvip.online` (Gateway).
- **Local infra:** Docker PostgreSQL 15-alpine (5432) + NATS 2-alpine (4222) + ShopERP 5003 + KhachLink 5002 + Gateway 5001.
- **Loyalty Alliance System:** FULLY OPERATIONAL (Phase 1-7 COMPLETE + DEPLOYED + VPS VERIFIED). Tenant currently in Silo mode — Alliance infrastructure ready for when tenant switches.
- **CustomerRepository.AddAsync fix (commit `550f5619`):** Fixed bug where AddAsync created a new Customer with wrong Id. Loyalty points now correctly awarded after order completion.
- **Tech debt:** TD-MVPS-001 through TD-MVPS-004 (see `docs/AI/tasks/tech_debt_multi_vps_checkout.md`). TD-PWA-001 (WASM conversion complete). Tier 5 — True Offline Edge (post-PoC). **TD-CUSTSYNC-001 (2026-07-27):** Customers created in ShopERP SQLite (CRM local) are NOT synced to Gateway PG — Gateway `OrderService.CreateOrderFromCommandAsync` validates CustomerId against PG and falls back to null if missing. Bug 6 fix mitigates this for guest checkout (DeviceId fallback + stub creation in SQLite), but full Customer sync SQLite→PG still needed for cross-system customer identity. **TD-ASYNCDP-001 (2026-08-03, NEW):** `ScopedDataProvider.GetAccountSum`/`GetAccountBalance` are sync methods that internally call async `GetPreAggregatedDataAsync` via `Task.Run(...).GetAwaiter().GetResult()` (Phase 0 Bug 3 quick fix). Proper fix: make `IFormulaEngine.Evaluate` + `IDataProvider.GetAccountSum` async (`EvaluateAsync`/`GetAccountSumAsync`) so the entire chain is async-native — eliminates sync-over-async + thread pool offload overhead. Large interface change, touch many callers.

> **Full detail** (per-sprint file lists, VPS RV step-by-step, plan deviations, Loyalty/CRM Audit Fix P0-P3, KhachLink Bugs 1-3, Bug 5/6, 4-Bug Fix, Sprint 0): see `docs/AI/project_state_archive.md` → "Archived 2026-08-03" → Section 3.

---

## 4. Next Actions

1. **(CURRENT — Browser RV)** Browser functional testing on VPS for all 4 phases (authenticated user flows):
   - Phase 0: Log in → navigate to accounting pages → verify no deadlock/hang.
   - Phase 1: Log in as Company tenant → verify "Sổ HKD" menu hidden; HKD tenant → verify visible.
   - Phase 2: Log in → 4 VAS report pages → click "📄 Xuất DOCX" / "📊 Xuất XLSX" → verify file downloads.
   - Phase 3: Log in as SystemAdmin → /admin/tenants → Edit tenant → change BusinessType → verify success (no accounting data) or 409 error (with accounting data).
2. **Post-Sprint 7 flaky tests:** Fix 4 EInvoiceOrchestratorTests (currently skipped via `Category!=Flaky` CI filter).
3. **CC-S6-T5 (Sprint 6) — Collaborator SMS OTP + Deposit Wallet (TOGGLE):** SystemAdmin toggle ON/OFF. Default OFF. Cần Domain Modification approval.
4. **A2 follow-up — Guid case audit (P2):** Audit + fix Guid case mismatch across all tables (not just OutboxMessages).
5. **Tech debt cleanup** — TD-MVPS-001 through TD-MVPS-004. **TD-CUSTSYNC-001:** Customer sync SQLite→PG. **TD-ASYNCDP-001:** Make `IFormulaEngine`/`IDataProvider` async-native (eliminates Phase 0 quick-fix sync-over-async).
6. **(Env)** Fix local DB role mismatch — ShopERP `vanan_admin` vs Gateway `vanan_dev`. (Note: this session manually created `vanan_admin` role + `vanan_accounting` DB in `vanan-postgres-local` container to unblock Phase 0 debug — see Maintenance Log.)
7. **(Guard-check script)** Investigate transient `$LASTEXITCODE` false-positive in fast-test-gate.
8. **(Facebook OAuth)** Config real Facebook OAuth credentials — Sprint 7+. Currently stub redirect in `Login.razor:148`.
9. **(Loyalty Alliance activation)** When tenant switches to Alliance mode in production, run end-to-end RV: create order → verify EARN to PG wallet → redeem → verify REDEEM from PG wallet → check KhachLink `/alliance-wallet` displays cross-tenant breakdown.
10. **(Bug 3 full verify)** Re-print QR for product with image to fully verify Scan.razor image rendering on VPS.

### Pruned (2026-07-29)

- ~~Sprint 1 Nearby Orders~~ — COMPLETE per Section 2 (commit `76d82e2c`).
- ~~Replace FingerprintJS stub~~ — DONE. Real FingerprintJS v5.2.0 vendored at `5_WebApps/KhachLink/wwwroot/lib/fingerprintjs/fingerprint.js` (F1 fix).
- ~~Cosmetic: `?` in Checkout.razor + `isTabVisible` in OrderTracking.razor~~ — DONE. Fixed by commit `a06ea092` (2026-07-23): Vietnamese content corruption + isTabVisible freeze bug.

---

## 5. Active Architecture Decisions

| Decision | Lý do |
|---|---|
| Gateway = Order Creator + Routed Async Delivery (Option C) | Multi-VPS support, PG source of truth, NATS routed by ShopInstanceId |
| CoreHub = in-process background service trong Gateway | Monolith Phase 1-2 |
| ShopERP = SQLite (Business) + PostgreSQL (Accounting) | ADR-001: accounting always online |
| CustomerToken = `IDataProtector` | Tránh library mới |
| `AccountingEntry` immutable, Reversal Entry | Audit trail bắt khu xâm phạm |
| Multi-tenancy `TenantId` filter mọi layer | Data isolation per HKD |
| EF Core Migrations = official schema management | Stream E |
| Dual Deployment Modes: SaaS (all-in-one) + Edge (tách biệt) | See Section 5a |
| Loyalty Alliance = Option B (HTTP proxy + cache + idempotency) | Multi-VPS ready, ShopERP does NOT connect to PG directly |

### 5a. Deployment Modes

**SaaS:** `docker-compose.prod.yml` — all modules on 1 VPS. Gateway → PG. ShopERP → SQLite. KhachLink → Gateway (HTTP).

**Edge:** `docker-compose.edge.yml` — Server A (Edge): ShopERP + SQLite + NATS sync. Server B (Central): Gateway + PG + KhachLink. Sync via NATS Outbox.

---

## 6. History Log (compressed — see archive + git log)

* [2026-08-03] **TENANT MANAGEMENT + ACCOUNTING UI FIXES — ALL 4 PHASES COMPLETE.** Commits `89fb90b6` (P0 Bug 3 deadlock) → `5f21ab36` (P1 Bug 2A HKD menu hide) → `c0fbcef6` (P2 Bug 2B VAS export DOCX/XLSX) → `424c3aa7` (P3 Bug 1 Edit BusinessType). All CI PASS, CD SUCCESS, VPS HTTP-level RV PASS. Browser functional testing remaining.
* [2026-08-03] **UI FIX BATCH (5 ISSUES) COMPLETE.** Commit `6179fdd7`. RV 7/7. Impersonate + store search + payment status + QR cart + POS font/QR.
* [2026-08-03] **LOYALTY CONSISTENCY FIX COMPLETE.** RV 37/37. 9 bugs fixed via 2-layer execution. Option B HTTP proxy + cache + idempotency.
* [2026-08-02] **LOYALTY ALLIANCE PHASE 7 COMPLETE.** Commit `25a70b9f`. RV 14/14. ALL 7 PHASES COMPLETE + DEPLOYED.
* [2026-08-02] **LOYALTY ALLIANCE PHASES 1-6 COMPLETE.** Domain+EF+Migration → Mode routing → Admin/Customer API → Mode Switch Migration → Admin/Customer UI → Unit+E2E tests.
* [2026-08-01] **SYSTEMADMIN GUIDE REVIEW COMPLETE.** Commit `9743054a`. RV 24/24.
* [2026-07-31] **VPS BUG FIX BATCH (3 bugs) COMPLETE.** Commits `141b944b` + `c47b89d6`. SQLite migration + nginx charset + QR image + entrypoint LF.
* [2026-07-30] **POST-SPRINT 7 CRITICAL FIXES COMPLETE.** Commit `ef8519c9`. RV 21/21. ICommerceModeService wired into OrderService.
* [2026-07-30] **CC-S7 SPRINT 7 COMMERCE MODE TOGGLE COMPLETE.** Commit `3fba1e8d`. RV7 18/18.
* [2026-07-30] **CC-S5 SPRINT 5 WALLET + COD + SETTLEMENT COMPLETE.** Commit `2c038fc0`. RV 34/35.
* [2026-07-30] **CC-S4 SPRINT 4 SALESMAN + COMPOSITE QR COMPLETE.** Commit `b78b71d5`. RV 26/26.
* [2026-07-29] **CC-S3 SPRINT 3 CHAT COMPLETE.** Commit `cd1b200f`. RV 18/18.
* [2026-07-29] **CC-S2 SPRINT 2 DELIVERY + GPS COMPLETE.** Commit `a3f4c25e`. RV 19/19.
* [2026-07-29] **CC-S1 SPRINT 1 NEARBY ORDERS COMPLETE.** Commits `4e7d9507` + `64d3bf77` + `76d82e2c`.
* [2026-07-28] **LOYALTY/CRM AUDIT FIX P0-P3 COMPLETE.** Commits `4aa0c6e2` → `018a42c2` on `fix/loyalty-crm-audit-fix` (NOT yet merged).
* [2026-07-28] **VPS CRM/LOYALTY VERIFICATION + P0/P1 FIX COMPLETE.** Commits `8d75abc1` + `e47dad26`. Outbox COLLATE NOCASE fix.
* [2026-07-27] **KHACHLINK BUGS 1-3 FIX COMPLETE.** Commit `35dc9de6`. `[ResolveCustomerTenant]` action filter.
* [2026-07-27] **BUG 5+6 FIX COMPLETE.** Commit `30e42e69`. OrderHub AllowAnonymous + DeviceId fallback.
* [2026-07-27] **4-BUG CHECKOUT-TO-KITCHEN FIX COMPLETE.** Commit `4af5672e`.
* [2026-07-26] **SPRINT 0 COMPLETE.** 11 entities + 42 tests + migration. RV 18/18.
* [2026-07-26] **DOC v1.4-v1.1 COMPLETE.** 4 doc-only sessions.
* [2026-07-24] **LOYALTY L-C COMPLETE.** RV 57/57. Gamification + config UI + notification jobs.
* [2026-07-24] **LOYALTY L-B COMPLETE.** RV 13/13. Redemption system.
* [2026-07-24] **LOYALTY L-A + PHASE 5 PUSH COMPLETE.** Configurable formula + push notifications.
* [2026-07-23] **PRODUCT PICKER + ORDER STATUS UNIFICATION.** RV 4/4.
* [2026-07-23] **FONT FIX + FREEZE FIX.** Double-encoding + IAsyncDisposable.
* [2026-07-22] **THEME + PWA PHASES 1-3.** 5 themes. Blazor Server → WASM. Offline caching.
* [2026-07-20] **MULTI-VPS OPTION C PHASES 1-7 COMPLETE.** ShopInstance + Order Creator + NATS routed.
* [2026-07-18] **MULTI-TENANT BUG FIX + QUICK-SETUP REAL.** 5 commits.
* [2026-07-17] **SINGLE-IDENTITY REFACTOR COMPLETE.** All 5 entities. VPS verified.
* [2026-07-16] **UUIDv7 REFACTOR + DATA SYNC HARDENING.**
* [2026-07-15] **ORDER SYNC TRACK E1 COMPLETE.** Option D. RC-1/2/3 fixed.
* [2026-07-14] **KHACHLINK E2E VPS PASS + UI/UX FIX BATCH.**
* [2026-07-13] **TIERED AUTH P1-P3 RV COMPLETE.** 14/14.
* [2026-07-09-10] **ACCOUNTING POSTGRESQL ONLINE.** 3 waves. 1223/1223.
* **Older:** See `docs/AI/project_state_archive.md`.

---

## 7. Active Files Reference

| File | Role |
|---|---|
| `docs/AI/tasks/task_cc_sprint0_foundation-2c5017.md` | Sprint 0 task card (COMPLETE) |
| `docs/AI/tasks/task_cc_sprint1_nearby_orders-2c5017.md` | Sprint 1 task card (NEXT) |
| `docs/AI/tasks/sprint1_nearby_orders_detailed_plan-2c5017.md` | Sprint 1 detailed plan |
| `docs/AI/tasks/community-commerce-master-plan-2c5017.md` | Community Commerce master plan |
| `docs/AI/tasks/community-commerce-requirements-spec-2c5017.md` | Requirements spec v1.4 |
| `docs/AI/tasks/tech_debt_multi_vps_checkout.md` | Tech debt register |
| `docs/Architecture/ADR001-Station-Architecture.md` | ADR-001 v3 (Option C) |
| `docs/specs/loyalty-alliance-spec.md` | Loyalty Alliance spec v1.0 |
| `docs/plans/loyalty-alliance-{master-plan,task-cards,detail-coding-plan}.md` | Loyalty Alliance 3 plan files |
| `docs/plans/loyalty-consistency-fix-{master-plan,task-cards,detail-coding-plan}.md` | Loyalty Consistency Fix 3 plan files |
| `docs/AI/project_state_archive.md` | Archived history (2026-07-26 + 2026-08-03) |

---

## 8. Architecture Quick Reference

```
=== SaaS Mode (docker-compose.prod.yml) ===
KhachLink (5002) → Gateway (5001) → ShopERP (5003) → SQLite (local)
                       ↓
              [in-process CoreHub]
                       ↓
                  PostgreSQL (central)

=== Edge Mode (docker-compose.edge.yml) ===
Server A (Edge):              Server B (Central):
  ShopERP → SQLite              Gateway → PostgreSQL
  NATS sync worker              [in-process CoreHub]
       ↓ NATS ↓
  ---------------→ Gateway
                   KhachLink → Gateway (HTTP)
```

**Auth:** Cookie (Blazor Server) + JWT Bearer (API). `DevLoginController` (`#if DEBUG`) for E2E.
**Roles:** `UserRole` (tenant-scoped) + `PlatformRole` (cross-tenant: SystemAdmin).

---

## 9. AI Health Check

- **Assumptions:** 0
- **Verified Facts:** Branch=`main`, last commit `89fb90b6` (Phase 0 Bug 3 fix). CI PASS (1253s). CD SUCCESS (6min, run `30815588126`). VPS HTTP-level RV 7/7 PASS — ShopERP/KhachLink/Gateway all 200, HKD books + detail routes 200. Root cause confirmed via server log: `ScopedDataProvider.cs:86,126` sync-over-async deadlock. Fix: `Task.Run` wrapper. Tech debt TD-ASYNCDP-001 logged. Phases 1+2+3 READY. All prior sprints COMPLETE.
- **Open Questions:** 0
- **Gate 6 Status:** ✅ Assumptions (0) < Verified Facts (80+), Open Questions (0) < 3

---

## 10. Maintenance Log

> Full historical maintenance log: see `docs/AI/project_state_archive.md` → "Archived 2026-08-03" → Section 10.

* **2026-08-03 — TENANT FIXES ALL 4 PHASES COMPLETE + DEPLOYED + VPS VERIFIED.**
  - **Phase 0 (Bug 3 — deadlock):** commit `89fb90b6`, CD run `30815588126`, RV 7/7. Fix: `Task.Run` wrapper in `ScopedDataProvider.cs`. TD-ASYNCDP-001 logged.
  - **Phase 1 (Bug 2A — HKD menu hide):** commit `5f21ab36`, CD run `30823357227`, RV 5/5. `_isHkd` conditional in `AccountingLayout.razor`. E2E: `hkd-menu-visibility.spec.ts`.
  - **Phase 2 (Bug 2B — VAS Reports export):** commit `c0fbcef6`, CD run `30823357227`, RV 7/7. New `IFinancialReportExportService` (Open XML SDK DOCX + EPPlus XLSX) + 4 UI pages + E2E `vas-export.spec.ts`.
  - **Phase 3 (Bug 1 — Edit BusinessType):** commit `424c3aa7`, CD run `30826995144`, RV 6/6. Domain `Tenant.ChangeBusinessType()` + `TenantBusinessTypeChangedEvent` (8 unit tests). Service `ChangeBusinessTypeAsync()` with AccountingEntry guard (IAccountingDbContext). Gateway API `PUT /api/v1/tenants/{id}/business-type` (409 if accounting data). UI Edit modal: BusinessType dropdown + HKDGroup + Reason. E2E: `tenant-edit-businesstype.spec.ts`. CI PASS (1229s, 1261+17+39+144 tests 0 failures).
  - **All phases:** HTTP-level RV PASS. Browser functional testing for authenticated users on VPS is the only remaining step. Branch: `main`. Last commit: `424c3aa7`. In sync with origin.
* **2026-08-03 — TENANT FIXES PHASE 0 (BUG 3) COMPLETE + DEPLOYED + VPS VERIFIED (commit `89fb90b6`, CD run `30815588126`, RV 7/7 PASS).** Bug 3: tenant HKD clicks "📖 Mở sổ" at `/accounting/hkd-books` → page hangs forever (loading spinner). Root cause: `ScopedDataProvider.cs:86,126` sync-over-async — `GetPreAggregatedDataAsync(context).GetAwaiter().GetResult()` blocks Blazor Server single-threaded sync context; the async chain (`GetPreAggregatedDataAsync` → `GetAccountAggregatesAsync` → `GetAccountSumAsync` → `ToListAsync()`) awaits without `ConfigureAwait(false)`, so its continuation cannot resume → infinite deadlock. Server log evidence: SQL executed (7ms) at 17:50:59, then 28s silence, Blazor circuit died (61s timeout) + reconnected. Fix (Option A — quick): wrapped both calls in `Task.Run(() => GetPreAggregatedDataAsync(context)).GetAwaiter().GetResult()` — offloads async chain to thread pool (no sync context) so continuation completes. CI PASS (1253s, 1253+17+39+115 tests). CD SUCCESS (6min: Build 4m20s + Validate 8s + Deploy 1m38s). VPS HTTP-level RV 7/7 PASS — ShopERP/KhachLink/Gateway all 200, HKD books + detail routes 200. Tech debt TD-ASYNCDP-001 logged for proper async-native fix (Option B). Also: manually created `vanan_admin` role + `vanan_accounting` DB in `vanan-postgres-local` container (was missing — env issue). Branch: `main`. Last commit: `89fb90b6`. In sync with origin.
* **2026-08-03 — KHACHLINK LOYALTYMODE UI HIDE COMPLETE + VPS VERIFIED (RV 10/10 PASS, commit `133e8061`, CD run `30789469902`).** When SystemAdmin sets LoyaltyMode=Silo, KhachLink hides all "Ví liên minh" UI to prevent customer confusion. New public endpoint `GET /api/loyalty/mode` (anonymous) returns global mode. New `LoyaltyModeHttpService` (cached 5 min, defaults Silo on error). 3 UI points hidden: NavMenu desktop+mobile tabs, LoyaltyCard link, AllianceWallet page (shows "Tính năng liên minh đang tắt" guard message). 8 files changed. CI PASS (1347s, 1253+17+233 tests). CD SUCCESS (5m35s). VPS RV 10/10 PASS — endpoint returns `{"mode":"Silo"}`, WASM fresh (2 min), Gateway DLL fresh (4 min), all pages 200. Branch: `main`. Last commit: `133e8061`. In sync with origin.
* **2026-08-03 — KHACHLINK UI POLISH + HOME SEARCH FIX COMPLETE (commits `29180a53` + `482e481f`).** (1) NavMenu.razor: removed 4 duplicate footer icons (Giỏ hàng, Điểm thưởng, Nhiệm vụ, Đổi điểm) — already in header. Mobile bottom-nav reduced from 10 → 6 tabs. (2) Home.razor: fixed store search box — `@bind:event="oninput"` (was `onchange` → query empty on Enter due to binding race condition) + restructured render tree (search box always visible above results, was hidden inside `else if` conditional after search). No-results message now distinguishes location vs keyword search. Build 0 errors. (3) Order Status Sync Fix: ConfirmPaymentAsync enqueues OrderPaymentStatusChanged outbox event + SyncOrderCompletedAsync camelCase fix + order.payment.status.changed case in DataSyncSubscriber. Branch: `main`. Last commit: `482e481f`.
* **2026-08-03 — PROJECT STATE ARCHIVED (reduction 395 → ~280 lines).** Moved all Section 2 "Previous:" objectives (full detail), Section 3 per-sprint status items, and Section 10 maintenance log entries (2026-07-26 → 2026-08-03) to `docs/AI/project_state_archive.md` under new "Archived 2026-08-03" section. Branch: `main`. Last commit: `6179fdd7`.
* **2026-08-03 — UI FIX BATCH (5 ISSUES) COMPLETE + VPS VERIFIED (RV 7/7 PASS, commit `6179fdd7`).** 5 UI issues fixed across 11 files. Pre-push CI ALL PASSED (994s). CD SUCCESS. VPS RV 7/7 PASS. (Full detail in archive.)
* **2026-08-03 — LOYALTY CONSISTENCY FIX COMPLETE + VPS VERIFIED (RV 37/37 PASS).** 9 bugs fixed via 2-layer execution. Option B HTTP proxy + cache + idempotency. (Full detail in archive.)
* **2026-08-02 — LOYALTY ALLIANCE PHASE 7 COMPLETE + RV 14/14 PASS (commit `25a70b9f`).** ALL 7 PHASES COMPLETE + DEPLOYED + VERIFIED. (Full detail in archive.)
