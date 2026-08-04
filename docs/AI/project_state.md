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

**TT 99/2025/TT-BTC Compliance Fixes (8 Gaps)** — � WAVES 1-3 COMPLETE (6/7 phases). Phase 5 (B 09-DN Thuyết minh BCTC) remaining. 8 gaps verified against 5 official sources (MISA, thuvienphapluat, Grant Thornton, Bộ Tài chính, tanngoctax). 6 task cards + 1 Phase 5a verified against codebase via 6 parallel subagents.

- **Master plan:** `docs/AI/tasks/tt99_compliance_fixes/tt99_compliance_fixes_master_plan.md`
- **ANALYZE report:** `docs/AI/tasks/tt99_compliance_fixes/ANALYZE_REPORT_reverse_impact.md` (full reverse impact review)
- **Task cards:** `phase1` → `phase6` + `phase5a` (all 6 implemented phases marked ✅ COMPLETE)
- **Bộ BCTC năm theo TT 99 (DN hoạt động liên tục):** B 01-DN (Báo cáo tình hình TC), B 02-DN (KQ HĐKD), B 03-DN (Lưu chuyển tiền tệ), B 09-DN (Thuyết minh BCTC)
- **8 Gaps:** (1) B 09-DN THIẾU — Phase 5 NEXT, (2) B 01-DN sai tên — ✅, (3) B 03-DN thiếu indirect method — ✅, (4) flat account list thay vì TT99 template — ✅, (5) default standard = TT133 — ✅, (6) thiếu TT58 — ✅, (7) thiếu chỉ tiêu BĐSĐT — ✅, (8) TrialBalance nằm trong bộ BCTC — ✅
- **Wave 1 (commit `66c9cfaf`):** Phase 1 (rename 7 files) + Phase 5a (TenantSettings: LegalForm/BusinessField/CharterCapital) + Phase 6 (seed TK 5117/6327 + verify TK 217 Investing) + Phase 2 (auto-select TT99_2025 for Enterprise_Large via IVasFeatureFlagService + TT58_2026 dropdown gated by CanAccessVasReportsAsync). CD SUCCESS, VPS RV 10/10 PASS.
- **Wave 2 (commit `27d34b40`):** Phase 4 — Tt99TemplateLine + Tt99ReportTemplate records in Domain.cs + new Tt99Templates.cs (B 01-DN/B 02-DN/B 03-DN verified templates) + BalanceSheetService/IncomeStatementService/CashFlowStatementService refactored to Mã số structure with backward compatibility. CD SUCCESS, VPS RV 10/10 PASS.
- **Wave 3 (commit `f98ddea5`):** Phase 3 — CashFlowMethod enum (Direct/Indirect) + CashFlowStatement.Method field + GenerateIndirectAsync (Mã 01-17 + working capital deltas) + injected IBalanceSheetService + IIncomeStatementService + UI toggle in CashFlowStatement.razor + 2 test files updated. CD run `30873505215` SUCCESS, VPS RV 10/10 PASS. **Follow-up:** "Accounting Tests" workflow failed (run `30873505237`) — separate from main CI/CD, needs investigation.
- **NEXT — Wave 4:** Phase 5 (B 09-DN Thuyết minh BCTC) — new report, depends on Phase 5a (✅) + Phase 4 (✅), both DONE. 2-3 sessions. New FinancialStatementNotes record + service + razor page + export + hub card.

**Previous objective — COMPLETE:** Tenant Management + Accounting UI Fixes (4 Bugs) — ✅ ALL 4 PHASES COMPLETE + DEPLOYED + VPS VERIFIED (HTTP-level). Browser functional testing for authenticated users on VPS is the only remaining step.

- **Master plan:** `docs/AI/tasks/tenant_accounting_fixes/tenant_accounting_fixes_master_plan.md` (4 phases: P0 Bug 3 debug, P1 Bug 2A hide HKD menu, P2 Bug 2B VAS export, P3 Bug 1 edit BusinessType)
- **Phase 0 (Bug 3):** ✅ COMPLETE — commit `89fb90b6`, CI PASS (1253s), CD SUCCESS (6min), VPS HTTP-level RV 7/7 PASS. Root cause: `ScopedDataProvider.cs:86,126` sync-over-async deadlock in Blazor Server. Fix: `Task.Run` wrapper. Tech debt TD-ASYNCDP-001 logged for proper async-native fix.
- **Phase 1 (Bug 2A):** ✅ COMPLETE — commit `5f21ab36`, CI PASS (923s), CD SUCCESS (6min), VPS HTTP-level RV 5/5 PASS. Hide "Sổ HKD (TT 152)" menu for Company tenants via `_isHkd` conditional in `AccountingLayout.razor`. E2E test `hkd-menu-visibility.spec.ts`.
- **Phase 2 (Bug 2B):** ✅ COMPLETE — commit `c0fbcef6`, CI PASS (1218s), CD SUCCESS (5min), VPS HTTP-level RV 7/7 PASS. New `IFinancialReportExportService` (Open XML SDK DOCX + EPPlus XLSX) + DI + 4 UI pages (BalanceSheet/IncomeStatement/CashFlowStatement/TrialBalance) with "📄 Xuất DOCX" + "📊 Xuất XLSX" buttons. E2E test `vas-export.spec.ts`.
- **Phase 3 (Bug 1):** ✅ COMPLETE — commit `424c3aa7`, CI PASS (1229s, 1261+17+39+144 tests 0 failures), CD SUCCESS (5min), VPS HTTP-level RV 6/6 PASS. Domain `Tenant.ChangeBusinessType()` + `TenantBusinessTypeChangedEvent` (8 unit tests PASS). Service `ChangeBusinessTypeAsync()` with AccountingEntry data integrity guard (IAccountingDbContext). Gateway API `PUT /api/v1/tenants/{id}/business-type` (409 if accounting data exists). UI Edit modal: BusinessType dropdown + HKDGroup + Reason field. E2E test `tenant-edit-businesstype.spec.ts`.

**Last completed:** TT 99/2025/TT-BTC Compliance Fixes — ANALYZE COMPLETE (commits `03fcb459` master plan + `94c29dcf` ANALYZE report, 2026-08-03). Previous: Tenant Management + Accounting UI Fixes — ALL 4 PHASES COMPLETE + DEPLOYED + VPS VERIFIED (commits `89fb90b6` → `424c3aa7`, 2026-08-03).

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
- **Last commit:** `5e2217f4` fix(test): guard ObjectDisposedException in Blazor timer callbacks
- **Working tree:** Modified `ShopERPDbContextModelSnapshot.cs` (uncommitted — auto-generated by EF tooling) + untracked `.devin/*` scripts + new migration `20260804154728_AddTenantSettingsB09DNAndStyleColumns` (uncommitted). Branch in sync with origin/main.
- **.NET SDK:** 8.0.422
- **DB:** SQLite `vanan_shoperp.db` (business) + PostgreSQL `VanAnCoreHub` (accounting + Gateway + Community tables)
- **Build:** 0 errors across full solution. CI pre-push ALL PASSED.
- **CI/CD:** GitHub Actions CI + CD both SUCCESS for commit `5e2217f4` (run `30924502034` CI, `30924502035` CD). Previous CI failures (commits `c9ac98cc` → `8d1a7b41`) resolved by 2 test fixes (see Section 10).
- **VPS:** 8 containers healthy (gateway, shoperp, khachlink, nginx, seq, certbot, postgres, nats). CD deploys automatically on push to main. Domains: `khachvip.online` (ShopERP), `diemthuong.khachvip.online` (KhachLink), `api.khachvip.online` (Gateway).
- **Local infra:** Docker PostgreSQL 15-alpine (5432) + NATS 2-alpine (4222) + ShopERP 5003 + KhachLink 5002 + Gateway 5001.
- **Loyalty Alliance System:** FULLY OPERATIONAL (Phase 1-7 COMPLETE + DEPLOYED + VPS VERIFIED). Tenant currently in Silo mode — Alliance infrastructure ready for when tenant switches.
- **CustomerRepository.AddAsync fix (commit `550f5619`):** Fixed bug where AddAsync created a new Customer with wrong Id. Loyalty points now correctly awarded after order completion.
- **Tech debt:** TD-MVPS-001 through TD-MVPS-004 (see `docs/AI/tasks/tech_debt_multi_vps_checkout.md`). TD-PWA-001 (WASM conversion complete). Tier 5 — True Offline Edge (post-PoC). **TD-CUSTSYNC-001 (2026-07-27):** Customers created in ShopERP SQLite (CRM local) are NOT synced to Gateway PG — Gateway `OrderService.CreateOrderFromCommandAsync` validates CustomerId against PG and falls back to null if missing. Bug 6 fix mitigates this for guest checkout (DeviceId fallback + stub creation in SQLite), but full Customer sync SQLite→PG still needed for cross-system customer identity. **TD-ASYNCDP-001 (2026-08-03, NEW):** `ScopedDataProvider.GetAccountSum`/`GetAccountBalance` are sync methods that internally call async `GetPreAggregatedDataAsync` via `Task.Run(...).GetAwaiter().GetResult()` (Phase 0 Bug 3 quick fix). Proper fix: make `IFormulaEngine.Evaluate` + `IDataProvider.GetAccountSum` async (`EvaluateAsync`/`GetAccountSumAsync`) so the entire chain is async-native — eliminates sync-over-async + thread pool offload overhead. Large interface change, touch many callers.

### 3a. Ready Issues (GitHub Project — NOT in repo)

> Tracked trên GitHub Issues (anlebao/Gemini_Windsurf). KHÔNG lưu task cards trong repo. Đóng issue trên GitHub sau khi RV pass.

| Issue | Title | Status | Commit | RV |
|---|---|---|---|---|
| #87 | Commerce mode JSON + push campaign error handling | ✅ DEPLOYED + RV PASS | `defdabf3` | 20 PASS |
| #88 | (subset of #87) Push campaign error handling | ✅ DEPLOYED + RV PASS | `defdabf3` | 20 PASS |
| #89 | Export DOCX empty + font tiếng Việt | ✅ DEPLOYED + RV PASS | `7edbdd7f` | (covered by #97) |
| #93 | KhachLink style customization (admin UI colors + logo) | ✅ DEPLOYED + RV PASS | `e1121579` | DB cols + store-info API PASS |
| #97 | Export DOCX empty + font tiếng Việt (consolidated) | ✅ DEPLOYED + RV PASS | `7edbdd7f` | (covered) |
| #98 | Sync status orders not smooth — realtime push to KhachLink | ✅ DEPLOYED + RV PASS | `c9ac98cc` | LocationHub + OrderHub /negotiate 200 |
| #99 | Redemption "Internal server error" — tenant filter + identity gate | ✅ DEPLOYED + RV PASS | `a8b5510f` | Redeem invalid/no token: 401 (not 500) |
| #100 | Cải tiến layout KhachLink — 4 sub-tasks (mobile sticky, home toggles, FB/TikTok, save notification) | ✅ DEPLOYED + RV PASS | `76d61670` | 4 Home_* cols in DB + feature settings endpoint 200 + KhachLink pages 200 |

**Tất cả 8 issues (#87, #88, #89, #93, #97, #98, #99, #100) đã deploy + RV pass trên VPS. Cần đóng issues trên GitHub.**

> **Full detail** (per-sprint file lists, VPS RV step-by-step, plan deviations, Loyalty/CRM Audit Fix P0-P3, KhachLink Bugs 1-3, Bug 5/6, 4-Bug Fix, Sprint 0): see `docs/AI/project_state_archive.md` → "Archived 2026-08-03" → Section 3.

---

## 4. Next Actions

1. **(CURRENT — Close GitHub Issues)** Đóng 8 issues đã RV pass trên GitHub: #87, #88, #89, #93, #97, #98, #99, #100. Comment mỗi issue với RV summary (commit + RV result). Sử dụng `gh issue close <num> --comment "<text>"`.
2. **(Previous — TT99 Compliance Fixes Wave 4)** ALL 7 PHASES COMPLETE. TT 99/2025/TT-BTC compliance fully implemented:
   - **Wave 4a COMPLETE (commit `d6fd850e`):** (B) Menu nav link B 09-DN added to Sitemap + FinancialReports hub + (C) Vietnamese number format `vi-VN` in 5 razor pages + 2 export services + (D) Fix 4 PeriodClosingPersistenceTests (`EnsureDeletedAsync` before `EnsureCreatedAsync`).
   - **Wave 4b COMPLETE (commit `51738298`):** Phase 5 B 09-DN Thuyết minh BCTC — new FinancialStatementNotes + NoteSection records + IFinancialStatementNotesService + FinancialStatementNotesService + FinancialStatementNotes.razor page + Export DOCX/XLSX + DI registration. All 4 mandatory financial reports now implemented (B 01-DN + B 02-DN + B 03-DN + B 09-DN).
   - **Waves 1-3 COMPLETE:** Wave 1 commit `66c9cfaf` (P1+P2+P5a+P6), Wave 2 commit `27d34b40` (P4 template structure), Wave 3 commit `f98ddea5` (P3 indirect method). All CD SUCCESS + VPS RV 10/10 PASS.
   - **Official templates VERIFIED:** REFERENCE_B01DN/B02DN/B03DN/B09DN_official.md (from vplsdms.vn Phụ lục IV TT 99)
2. **(Previous — Browser RV, deferred)** Browser functional testing on VPS for Tenant Fixes 4 phases (authenticated user flows):
   - Phase 0: Log in → accounting pages → verify no deadlock/hang.
   - Phase 1: Company tenant → "Sổ HKD" menu hidden; HKD tenant → visible.
   - Phase 2: 4 VAS report pages → "📄 Xuất DOCX" / "📊 Xuất XLSX" → verify file downloads.
   - Phase 3: SystemAdmin → /admin/tenants → Edit tenant → change BusinessType → verify success/409.
3. **Post-Sprint 7 flaky tests:** Fix 4 EInvoiceOrchestratorTests (currently skipped via `Category!=Flaky` CI filter).
4. **CC-S6-T5 (Sprint 6) — Collaborator SMS OTP + Deposit Wallet (TOGGLE):** SystemAdmin toggle ON/OFF. Default OFF. Cần Domain Modification approval.
5. **A2 follow-up — Guid case audit (P2):** Audit + fix Guid case mismatch across all tables (not just OutboxMessages).
6. **Tech debt cleanup** — TD-MVPS-001 through TD-MVPS-004. **TD-CUSTSYNC-001:** Customer sync SQLite→PG. **TD-ASYNCDP-001:** Make `IFormulaEngine`/`IDataProvider` async-native (eliminates Phase 0 quick-fix sync-over-async).
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

* [2026-08-03] **TT 99/2025/TT-BTC COMPLIANCE FIXES — ANALYZE COMPLETE.** Commits `03fcb459` (master plan + 6 task cards) + `94c29dcf` (ANALYZE report + 7 task cards updated). 8 gaps verified against 5 official sources. 6 subagents verified all task cards against codebase. New Phase 5a discovered (TenantSettings extension). 4 open questions for user.
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
- **Verified Facts:** Branch=`main`, last commit `5e2217f4` (fix ObjectDisposedException in Blazor timer callbacks). CI + CD both SUCCESS (runs `30924502034` + `30924502035`). 8 GitHub issues (#87, #88, #89, #93, #97, #98, #99, #100) DEPLOYED + RV PASS on VPS. ALL 7 TT 99 phases COMPLETE. Build 0 errors. All prior sprints COMPLETE.
- **Open Questions:** 0
- **Gate 6 Status:** ✅ Assumptions (0) < Verified Facts (80+), Open Questions (0) < 3

---

## 10. Maintenance Log

> Full historical maintenance log: see `docs/AI/project_state_archive.md` → "Archived 2026-08-03" → Section 10.

* **2026-08-04 — GITHUB ISSUES #87-#100 DEPLOYED + VPS RV PASS (8 issues).** 8 issues implemented + deployed + runtime-verified on VPS:
  - **#87 + #88** (commit `defdabf3`): Commerce mode JSON + push campaign error handling.
  - **#89 + #97** (commit `7edbdd7f`): Export DOCX empty + font tiếng Việt.
  - **#93** (commit `e1121579`): KhachLink style customization — admin UI colors (nav/header/footer) + logo. DB cols `Settings_NavColor/HeaderColor/FooterColor` + store-info API returns all 3.
  - **#98** (commit `c9ac98cc`): Sync status orders — realtime push to KhachLink. LocationHub `/hubs/location/negotiate`: 200, OrderHub `/orderHub/negotiate`: 200.
  - **#99** (commit `a8b5510f`): Redemption "Internal server error" — tenant filter + identity gate. Redeem invalid/no token: 401 (not 500).
  - **#100** (commit `76d61670`): Cải tiến layout KhachLink — 4 sub-tasks: (1) mobile sticky action buttons on Cart, (2) SystemAdmin toggle on/off 4 home sections (Campaign/Store/Featured/SocialHub), (3) FB/TikTok link config (already in TenantManagement), (4) save notification on ShopERP config page. Migration `AddHomeSectionToggles` applied to PG — 4 `Home_*_Enabled` columns in DB. Feature settings endpoint 200. KhachLink home + cart pages render.
  - **CD blocker resolved:** CD cho #100 fail ban đầu do GitHub Actions secondary rate limit (push 4 commits liên tiếp trong 2h). Rerun CD → SUCCESS → VPS deploy → migration applied.
  - **RV functional (script `rv_functional_20260804.sh`):** 20 PASS, 0 WARN, 1 FAIL (FAIL do RV script sai path `/locationHub` thay vì `/hubs/location` — manual verify trả 200).
  - Branch: `main`. Last commit at RV time: `76d61670`. In sync with origin.
* **2026-08-04 — CI TEST FIXES (2 commits) — RESOLVE PRE-EXISTING CI FAILURES.**
  - **Commit `8d1a7b41`:** Fix `TestDatabaseFixture` static `_schemaCreated` flag causing "no such table: Tenants". Root cause: commits `5f02b5cf` + `37a5d15b` added static flag to prevent "table AccountCharts already exists" race condition, but flag persists across fixture instances — when fixture A disposes (connection closes, in-memory DB destroyed), fixture B sees `_schemaCreated=true` and skips `EnsureCreatedAsync()` → no schema. Fix: remove static flag, keep lock. `EnsureCreatedAsync()` is idempotent. Verified: TestDatabaseFixtureTests 5/5 PASS, PeriodClosingPersistenceTests 4/4 PASS, full suite 176 passed / 0 failed.
  - **Commit `5e2217f4`:** Guard `ObjectDisposedException` in Blazor timer callbacks. Root cause: CI test host process crashes with unhandled `ObjectDisposedException` AFTER all tests complete (131 passed, 0 failed, 9 skipped, but exit code 1). Two sources: (1) `Orders/Index.razor:318` — `_pollTimer` (5s) callback calls `ScopeFactory.CreateScope()` after test host disposes ServiceProvider; (2) `Kitchen/Display.razor:280` — retry `Task.Run` (10s delay) calls `_hubConnection.StartAsync()` after component disposes HubConnection. Fix: wrap `CreateScope()` in try/catch(`ObjectDisposedException`) silently return; catch `ObjectDisposedException` in Kitchen SignalR retry. Verified: full integration suite 233 passed / 0 failed / 13 skipped, exit code 0. CI run `30924502034` SUCCESS, CD run `30924502035` SUCCESS.
  - Branch: `main`. Last commit: `5e2217f4`. In sync with origin.

* **2026-08-03 — TT 99/2025/TT-BTC COMPLIANCE FIXES — ANALYZE COMPLETE (commits `03fcb459` + `94c29dcf`).** User requested verify codebase against TT 99/2025/TT-BTC (BCTC năm, DN hoạt động liên tục). Verified against 5 official sources: MISA (amis.misa.vn), thuvienphapluat.vn, Grant Thornton, Bộ Tài chính (portal.mof.gov.vn), tanngoctax.vn. 8 gaps identified: (1) B 09-DN Thuyết minh THIẾU hoàn toàn, (2) B 01-DN sai tên "Bảng CĐKT" → "Báo cáo tình hình TC", (3) B 03-DN thiếu phương pháp gián tiếp, (4) flat account list thay vì TT99 template (Mã số 100/110...), (5) default standard = TT133 không auto-select, (6) thiếu TT58 dropdown, (7) thiếu chỉ tiêu BĐSĐT, (8) TrialBalance nằm trong bộ BCTC. Created master plan + 6 task cards. ANALYZE pass: 6 subagents verified all task cards against codebase in parallel. Key findings: Phase 1 needs 7 files (was 3 — Sitemap + tests missing); Phase 2 simpler (IVasFeatureFlagService.GetTenantTypeAsync() already exists, no DTO change); Phase 3 needs DI injection (10 files); Phase 5 BLOCKER (Tenant missing LegalForm/BusinessField/CharterCapital → new Phase 5a TenantSettings extension); Phase 6 TK 5117/6327 missing from seeder, Mã số "75" unverified. 4 open questions for user. Branch: `main`. Last commit: `94c29dcf`. In sync with origin.
* **2026-08-03 — TENANT FIXES ALL 4 PHASES COMPLETE + DEPLOYED + VPS VERIFIED.**
  - **Phase 0 (Bug 3 — deadlock):** commit `89fb90b6`, CD run `30815588126`, RV 7/7. Fix: `Task.Run` wrapper in `ScopedDataProvider.cs`. TD-ASYNCDP-001 logged.
  - **Phase 1 (Bug 2A — HKD menu hide):** commit `5f21ab36`, CD run `30823357227`, RV 5/5. `_isHkd` conditional in `AccountingLayout.razor`. E2E: `hkd-menu-visibility.spec.ts`.
  - **Phase 2 (Bug 2B — VAS Reports export):** commit `c0fbcef6`, CD run `30823357227`, RV 7/7. New `IFinancialReportExportService` (Open XML SDK DOCX + EPPlus XLSX) + 4 UI pages + E2E `vas-export.spec.ts`.
  - **Phase 3 (Bug 1 — Edit BusinessType):** commit `424c3aa7`, CD run `30826995144`, RV 6/6. Domain `Tenant.ChangeBusinessType()` + `TenantBusinessTypeChangedEvent` (8 unit tests). Service `ChangeBusinessTypeAsync()` with AccountingEntry guard (IAccountingDbContext). Gateway API `PUT /api/v1/tenants/{id}/business-type` (409 if accounting data). UI Edit modal: BusinessType dropdown + HKDGroup + Reason. E2E: `tenant-edit-businesstype.spec.ts`. CI PASS (1229s, 1261+17+39+144 tests 0 failures).
  - **All phases:** HTTP-level RV PASS. Browser functional testing for authenticated users on VPS is the only remaining step. Branch: `main`. Last commit: `424c3aa7`. In sync with origin.
* **2026-08-04 — TT 99/2025/TT-BTC COMPLIANCE FIXES WAVES 1-3 COMPLETE + VPS VERIFIED (RV 10/10 PASS each wave).**
  - **Wave 1 (commit `66c9cfaf`):** Phase 1 (rename B 01-DN "Bảng CĐKT" → "Báo cáo tình hình TC" in 7 files) + Phase 5a (TenantSettings: LegalForm/BusinessField/CharterCapital) + Phase 6 (seed TK 5117/6327 + verify TK 217 Investing) + Phase 2 (auto-select TT99_2025 for Enterprise_Large via IVasFeatureFlagService + TT58_2026 dropdown). CD SUCCESS, RV 10/10.
  - **Wave 2 (commit `27d34b40`):** Phase 4 — Tt99TemplateLine + Tt99ReportTemplate records in Domain.cs + new Tt99Templates.cs (B 01-DN/B 02-DN/B 03-DN verified templates) + 3 services refactored to Mã số structure with backward compatibility. CD SUCCESS, RV 10/10.
  - **Wave 3 (commit `f98ddea5`):** Phase 3 — CashFlowMethod enum (Direct/Indirect) + CashFlowStatement.Method field + GenerateIndirectAsync (Mã 01-17 + working capital deltas) + injected IBalanceSheetService + IIncomeStatementService + UI toggle in CashFlowStatement.razor + 2 test files updated. CD run `30873505215` SUCCESS, RV 10/10. **Follow-up:** "Accounting Tests" workflow failed (run `30873505237`) — 4 PeriodClosingPersistenceTests fail with `SQLite Error 1: 'table "AccountCharts" already exists'` at TestDatabaseFixture.cs:74 (`EnsureCreatedAsync`). Test infra issue, NOT Wave 3 code bug. Fixed in Wave 4a.
  - **Wave 4 IN PROGRESS (2 commits planned):** Commit 4a (quick fixes: menu nav link + vi-VN number format + test fix) + Commit 4b (Phase 5 B 09-DN Thuyết minh BCTC — new report). Branch: `main`. Last commit: `f98ddea5`. In sync with origin.
* **2026-08-04 — TT 99/2025/TT-BTC COMPLIANCE FIXES WAVE 4 COMPLETE — ALL 7 PHASES DONE.**
  - **Wave 4a (commit `d6fd850e`):** Quick fixes — (B) B 09-DN link added to Sitemap.razor + FinancialReports.razor hub card, (C) Vietnamese number format `vi-VN` via `CultureInfo.GetCultureInfo("vi-VN")` in 5 razor pages (BalanceSheet, IncomeStatement, CashFlowStatement, TrialBalance, TransactionHistory) + 2 export services (FinancialReportExportService, HKDBookExportService), removed hacky `InvariantCulture.Replace(",", ".")`, (D) Fix 4 PeriodClosingPersistenceTests — added `EnsureDeletedAsync()` before `EnsureCreatedAsync()` in TestDatabaseFixture.cs to prevent SQLite `table AccountCharts already exists` error. Build 0 errors. CI passed before push. Pushed to main.
  - **Wave 4b (commit `51738298`):** Phase 5 B 09-DN Thuyết minh BCTC — new `FinancialStatementNotes` + `NoteSection` records in Domain.cs (5 sections: I/II/III/IV/X per Phụ lục IV TT 99), new `IFinancialStatementNotesService` + `FinancialStatementNotesService` (pulls tenant info from TenantSettings for Phần I, TT 99 standard template text for Phần IV 29 policies), new `FinancialStatementNotes.razor` page at `/accounting/financial-statement-notes` with period picker + export buttons, new `ExportNotesToDocxAsync` + `ExportNotesToXlsxAsync` in FinancialReportExportService (textual export), DI registration in Program.cs. Build 0 errors. All 4 mandatory financial reports now implemented. Branch: `main`. Last commit: `51738298`.
* **2026-08-03 — TENANT FIXES PHASE 0 (BUG 3) COMPLETE + DEPLOYED + VPS VERIFIED (commit `89fb90b6`, CD run `30815588126`, RV 7/7 PASS).** Bug 3: tenant HKD clicks "📖 Mở sổ" at `/accounting/hkd-books` → page hangs forever (loading spinner). Root cause: `ScopedDataProvider.cs:86,126` sync-over-async — `GetPreAggregatedDataAsync(context).GetAwaiter().GetResult()` blocks Blazor Server single-threaded sync context; the async chain (`GetPreAggregatedDataAsync` → `GetAccountAggregatesAsync` → `GetAccountSumAsync` → `ToListAsync()`) awaits without `ConfigureAwait(false)`, so its continuation cannot resume → infinite deadlock. Server log evidence: SQL executed (7ms) at 17:50:59, then 28s silence, Blazor circuit died (61s timeout) + reconnected. Fix (Option A — quick): wrapped both calls in `Task.Run(() => GetPreAggregatedDataAsync(context)).GetAwaiter().GetResult()` — offloads async chain to thread pool (no sync context) so continuation completes. CI PASS (1253s, 1253+17+39+115 tests). CD SUCCESS (6min: Build 4m20s + Validate 8s + Deploy 1m38s). VPS HTTP-level RV 7/7 PASS — ShopERP/KhachLink/Gateway all 200, HKD books + detail routes 200. Tech debt TD-ASYNCDP-001 logged for proper async-native fix (Option B). Also: manually created `vanan_admin` role + `vanan_accounting` DB in `vanan-postgres-local` container (was missing — env issue). Branch: `main`. Last commit: `89fb90b6`. In sync with origin.
* **2026-08-03 — KHACHLINK LOYALTYMODE UI HIDE COMPLETE + VPS VERIFIED (RV 10/10 PASS, commit `133e8061`, CD run `30789469902`).** When SystemAdmin sets LoyaltyMode=Silo, KhachLink hides all "Ví liên minh" UI to prevent customer confusion. New public endpoint `GET /api/loyalty/mode` (anonymous) returns global mode. New `LoyaltyModeHttpService` (cached 5 min, defaults Silo on error). 3 UI points hidden: NavMenu desktop+mobile tabs, LoyaltyCard link, AllianceWallet page (shows "Tính năng liên minh đang tắt" guard message). 8 files changed. CI PASS (1347s, 1253+17+233 tests). CD SUCCESS (5m35s). VPS RV 10/10 PASS — endpoint returns `{"mode":"Silo"}`, WASM fresh (2 min), Gateway DLL fresh (4 min), all pages 200. Branch: `main`. Last commit: `133e8061`. In sync with origin.
* **2026-08-03 — KHACHLINK UI POLISH + HOME SEARCH FIX COMPLETE (commits `29180a53` + `482e481f`).** (1) NavMenu.razor: removed 4 duplicate footer icons (Giỏ hàng, Điểm thưởng, Nhiệm vụ, Đổi điểm) — already in header. Mobile bottom-nav reduced from 10 → 6 tabs. (2) Home.razor: fixed store search box — `@bind:event="oninput"` (was `onchange` → query empty on Enter due to binding race condition) + restructured render tree (search box always visible above results, was hidden inside `else if` conditional after search). No-results message now distinguishes location vs keyword search. Build 0 errors. (3) Order Status Sync Fix: ConfirmPaymentAsync enqueues OrderPaymentStatusChanged outbox event + SyncOrderCompletedAsync camelCase fix + order.payment.status.changed case in DataSyncSubscriber. Branch: `main`. Last commit: `482e481f`.
* **2026-08-03 — PROJECT STATE ARCHIVED (reduction 395 → ~280 lines).** Moved all Section 2 "Previous:" objectives (full detail), Section 3 per-sprint status items, and Section 10 maintenance log entries (2026-07-26 → 2026-08-03) to `docs/AI/project_state_archive.md` under new "Archived 2026-08-03" section. Branch: `main`. Last commit: `6179fdd7`.
* **2026-08-03 — UI FIX BATCH (5 ISSUES) COMPLETE + VPS VERIFIED (RV 7/7 PASS, commit `6179fdd7`).** 5 UI issues fixed across 11 files. Pre-push CI ALL PASSED (994s). CD SUCCESS. VPS RV 7/7 PASS. (Full detail in archive.)
* **2026-08-03 — LOYALTY CONSISTENCY FIX COMPLETE + VPS VERIFIED (RV 37/37 PASS).** 9 bugs fixed via 2-layer execution. Option B HTTP proxy + cache + idempotency. (Full detail in archive.)
* **2026-08-02 — LOYALTY ALLIANCE PHASE 7 COMPLETE + RV 14/14 PASS (commit `25a70b9f`).** ALL 7 PHASES COMPLETE + DEPLOYED + VERIFIED. (Full detail in archive.)
