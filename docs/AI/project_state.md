# Project State

> **Mục đích:** Single Source of Truth cho AI về trạng thái dự án. BẮT BUỘC đọc đầu mỗi phiên.
> **Archived:** 2026-07-24 + 2026-08-03 + 2026-08-09 — All completed objectives + full history/maintenance log moved to `docs/AI/project_state_archive.md`

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

**GUARD QR VERIFICATION (ISSUE #126) — SPRINT 0 + SPRINT 1 COMPLETE. SPRINT 2 NEXT.**

**Source:** GitHub Issue #126 — "Guard page đang hardcode" — Guard Scanner page 100% hardcoded UI mockup, no backend.
**Branch:** `feature/guard-qr-r1` → merge to `main`
**Plan:** `docs/AI/tasks/guard_qr_verify/master_plan.md` (7 sprints: 0=Analyze, 1=Domain+Infra, 2=Gateway API, 3=Guard UI, 4=KhachLink Claim, 5=Printer, 6=Tests)

**Sprint 0 — Analyze (COMPLETE):**
- 6 integration points verified (UserRole.Guard ✅, R2 ✅ verified 5/5 tests, QR gen ✅ reuse qrcode.js, QR scan ✅ reuse QRScanner.razor, Printer ✅ reuse PrintBill.razor pattern, EF migration ✅ clear path)
- 8 BR spec drafted + approved (BR-G01 Issuance → BR-G08 C→A Migration)
- R2 bucket `vanan-guard-photos` created + verified (Account ID: 18947627801f833aecc202f086d66af5)
- Findings: `docs/AI/tasks/guard_qr_verify/sprint0_findings.md`

**Sprint 1 — Domain + Infrastructure (COMPLETE):**
- Domain: `VehicleSession` aggregate root + `GuardScanLog` entity + 2 enums + 2 VOs in `1_Shared/Domain.cs`
- EF: 2 configurations (`VehicleSessionConfiguration`, `GuardScanLogConfiguration`) + migration `20260814042520_AddGuardQrVerifyTables` (clean — 2 tables + 7 indexes, no drift)
- R2: `IR2StorageService` + `R2StorageService` (AWSSDK.S3, presigned URL pattern)
- Repositories: `IVehicleSessionRepository` + `VehicleSessionRepository` + `IGuardScanLogRepository` + `GuardScanLogRepository`
- DI: registered in `2_Gateway/Program.cs` + R2 config in `appsettings.json`
- Validation: `dotnet build` 0 errors · `guard-check.ps1` ALL PASSED · Architecture Guard PASSED · Fast test gate PASSED

**Next: Sprint 2 — Gateway API (GuardController).**
Endpoints: `/api/guard/issue`, `/api/guard/verify`, `/api/guard/claim`, `/api/guard/checkout`, `/api/guard/flag`, `/api/guard/sessions/today`.

> **Previous: HARDCODED TENANT ID CLEANUP + SETTLEMENT HISTORY UI — SPRINT A+B — archived.** See history log below.

---

## 3. Current Status

- **Branch:** `feature/guard-qr-r1` → merging to `main` · **Build:** 0 errors · **Guard-check:** ALL PASSED
- **.NET SDK:** 8.0.422
- **CI/CD:** CI SUCCESS — 1261 unit tests + 233 integration tests + 39 architecture tests ALL PASS (last run on `main`).
- **Issue #126 Guard QR Verify:** Sprint 0 + Sprint 1 COMPLETE. Sprint 2 (Gateway API) next.
- **Sprint A+B (previous):** ✅ Hardcoded tenant ID cleanup + Settlement History admin page + NavMenu completeness (30/30). On `main`.
- **GitHub Issues Batch:** ✅ #114 + #123 + #124 + #125 ALL FIXED + DEPLOYED + RV 33/33 PASS on VPS (previous sprint).
- **VALCN v2.0 RV:** 10 PASS + 1 PARTIAL + 2 FAIL→FIXED→VERIFIED (archived)
- **Order Sync:** ✅ FIXED + VERIFIED end-to-end (archived 2026-08-10).
- **GCP VPS (3 instances):** `vanan-gateway` (e2-small 2GB) — Gateway + Nginx + PG + NATS · `vanan-khachlink` (e2-micro) — KhachLink · `vanan-shop-a` (e2-micro) — ShopERP
- **Domains:** `api2.khachvip.online` (Gateway), `app2.khachvip.online` (ShopERP), `diemthuong2.khachvip.online` (KhachLink), `www2.khachvip.online` (main)
- **nginx:** 5-layer rate limit (static/api/auth/blazor/page) — 0 503 in load test (500+ requests)
- **Background Service Toggle:** `/admin/background-services` — 8 services toggleable
- **Loyalty Alliance:** FULLY OPERATIONAL. Tenant in Silo mode — Alliance infrastructure ready.
- **Cloudflare R2:** `vanan-guard-photos` bucket created + verified (Account ID: 18947627801f833aecc202f086d66af5). Used by Guard QR Verify (Sprint 1+).
- **Known gaps (verified, not bugs):** Network Dashboard cache 10-min (by design); TD-NETDASH-001 (Option B — Order.SetCustomerId Domain change, deferred).
- **Tech debt:** TD-MVPS-001→004, TD-CUSTSYNC-001, TD-ASYNCDP-001, TD-GCP-001, TD-NETDASH-001

---

## 4. Next Actions

**Issue #126 Guard QR Verify — Sprint 2 NEXT:**

1. **(Sprint 2 — Gateway API)** Implement `GuardController` with endpoints: `/api/guard/issue`, `/api/guard/verify`, `/api/guard/claim`, `/api/guard/checkout`, `/api/guard/flag`, `/api/guard/sessions/today`. Use `IVehicleSessionRepository` + `IGuardScanLogRepository` + `IR2StorageService` + `IGuardService` (new). Add `[Authorize(Roles="Guard")]`.
2. **(Sprint 3 — Guard UI)** Replace hardcoded `Pages/Guard/Scan.cshtml` with Blazor page + QR scanner (reuse `qr-scanner.js`) + photo upload (R2 presigned PUT) + QR display (qrcode.js).
3. **(Sprint 4 — KhachLink Claim)** New `/qr/claim` page using existing `QRScanner.razor` component. POST to `/api/guard/claim`.
4. **(Sprint 5 — Printer)** `PrintTicket.razor` (reuse `PrintBill.razor` pattern — `window.print()` + `@@media print` CSS).
5. **(Sprint 6 — Tests)** E2E + integration tests for full flow.

**Deferred / monitoring (from previous sprints):**
6. **(Deploy Sprint A+B)** Deploy `f7201ef4` to VPS via CD pipeline (when ready).
7. **(Browser RV — Settlements page)** Login ShopERP as SystemAdmin → /admin/settlements → verify page renders, filters work, pagination works.
8. **(Browser RV — #114 POS price entry + notes + voice + Kitchen TTS)** Login ShopERP → /pos → verify inline price/name/VAT inputs, voice notes, kitchen TTS.
9. **(Browser RV — #124 redeem button + #125 bottom nav)** KhachLink → /rewards → verify redeem; mobile view → verify bottom nav.
10. **(Post-PoC remaining gaps)** Kitchen-initiated orders (not yet implemented). Native app GPS + attestation limitations (documented, deferred).
11. **(GCP Data Seeding)** Seed production data vào GCP DB (fresh DB chỉ có 3 tenants test).
12. **(#99-3 Phase B APPROVAL)** Alliance VND Normalization — HIGH risk, feature-gated. Awaiting user approval.
13. **(Hybrid Strategy Bước 2 — Monitor)** Trigger khi CPU sustained > 70% / Memory > 80%.
14. **Post-Sprint 7 flaky tests:** Fix 4 EInvoiceOrchestratorTests (skipped via `Category!=Flaky` CI filter).
15. **Tech debt cleanup** — TD-MVPS-001→004, TD-CUSTSYNC-001, TD-ASYNCDP-001, TD-GCP-001, TD-NETDASH-001.
16. **(VPS Disk Monitoring)** Cân nhắc `docker image prune -af` vào deploy script hoặc cron job.
17. **(v3.0 deferred)** INV-009 (PointValue field), payment provider integration (VNPay/Momo), Ops Cost metric, Tier Distribution.
18. **(nginx deferred task cards)** `docs/AI/tasks/{nginx_per_user_rate_limit,blazor_api_aggregation,api_rate_limit_classification}_task_card.md`

---

## 5. Active Architecture Decisions

| Decision | Lý do |
|---|---|
| Gateway = Order Creator + Routed Async Delivery (Option C) | Multi-VPS support, PG source of truth, NATS routed by ShopInstanceId |
| CoreHub = in-process background service trong Gateway | Monolith Phase 1-2 |
| ShopERP = SQLite (Business) + PostgreSQL (Accounting) | ADR-001: accounting always online |
| `AccountingEntry` immutable, Reversal Entry | Audit trail bắt khu xâm phạm |
| Multi-tenancy `TenantId` filter mọi layer | Data isolation per HKD |
| Loyalty Alliance = Option B (HTTP proxy + cache + idempotency) | Multi-VPS ready, ShopERP does NOT connect to PG directly |
| nginx 5-layer rate limit | Separate API/page/auth/WebSocket/static quotas — prevents 503 on fast navigation |

**Deployment Modes:** SaaS (`docker-compose.prod.yml` — all on 1 VPS) ‖ Edge (`docker-compose.edge.yml` — Server A: ShopERP+SQLite+NATS, Server B: Gateway+PG+KhachLink).

---

## 6. History Log (compressed — see archive + git log)

* [2026-08-14] **GUARD QR VERIFY (ISSUE #126) — SPRINT 0 + SPRINT 1 COMPLETE.** Branch `feature/guard-qr-r1`. Sprint 0: 6 integration points verified + 8 BR spec drafted + R2 bucket `vanan-guard-photos` created + verified. Sprint 1: Domain entities (`VehicleSession` + `GuardScanLog` + 2 enums + 2 VOs) + EF config + migration `20260814042520_AddGuardQrVerifyTables` (clean, 2 tables + 7 indexes) + R2 storage service (`IR2StorageService` + `R2StorageService` with AWSSDK.S3) + repositories + DI registration. Build 0 errors · guard-check ALL PASSED · Architecture Guard PASSED · Fast test gate PASSED. Next: Sprint 2 (Gateway API — GuardController).
* [2026-08-13] **HARDCODED TENANT ID CLEANUP + SETTLEMENT HISTORY UI — SPRINT A+B COMPLETE.** Commit `f7201ef4`. Sprint A: 4 files fixed (ProductReferralConfigService, SocialAuthController, CustomerIdentityController, PermissionGroupManagement) — all hardcoded `Guid.Parse("00000000-...")` replaced with `IConfiguration["Seed:TenantId"]` fallback. Sprint B1: Settlement History admin page (`SettlementAdminController.cs` + `SettlementApiClient.cs` + `Settlements.razor`) + NavMenu completeness (30/30 admin pages have nav links, added Background Services link). Sprint B2: Tenant Settings already covered by TenantManagement edit modal. CI: 1261 unit + 233 integration + 39 architecture tests ALL PASS.
* [2026-08-11] **GITHUB ISSUES BATCH #114/#123/#124/#125 — ALL 4 FIXED + DEPLOYED + RV 33/33 PASS.** 3 commits: `716e7eec` (#123+#124+#125) + `07228b7e` (#114 initial) + `f46f544c` (#114 r1/r2/r3 revisions). RV on VPS: 33 PASS + 0 FAIL + 1 WARN (false positive). All 3 VPS healthy, no regression.
* [2026-08-09] **VALCN v2.0 PLATFORM-LIGHT — ALL 3 WAVES COMPLETE + DEPLOYED + RV PASS.** 7 commits. RV 10 PASS + 1 PARTIAL + 2 FAIL→FIXED. nginx 503 fixed (5-layer rate limit). 3 deferred task cards created.
* [2026-08-09] **GATEWAY REFACTOR HYBRID BƯỚC 1 COMPLETE + DEPLOYED + RV 11/11 PASS.** REQ-1.1 (poll 5s→10s) + REQ-1.2 (6 background service toggles) + REQ-1.3 (logging reduction ~90%).
* [2026-08-03] **TT 99/2025/TT-BTC COMPLIANCE FIXES — 3 WAVES COMPLETE.** 8 gaps fixed. RV 10/10 per wave.
* [2026-08-03] **TENANT MANAGEMENT + ACCOUNTING UI FIXES — 4 PHASES COMPLETE.** RV PASS.
* [2026-08-03] **LOYALTY CONSISTENCY FIX COMPLETE.** RV 37/37. 9 bugs fixed.
* [2026-08-02] **LOYALTY ALLIANCE ALL 7 PHASES COMPLETE.** RV 14/14.
* [2026-07-30] **COMMUNITY COMMERCE SPRINTS 4-7 COMPLETE.** Commerce Mode Toggle + Wallet + COD + Salesman + QR Referral.
* [2026-07-20] **MULTI-VPS OPTION C PHASES 1-7 COMPLETE.** ShopInstance + Order Creator + NATS routed.
* **Older:** See `docs/AI/project_state_archive.md`.

---

## 7. Active Files Reference

| File | Role |
|---|---|
| `docs/AI/tasks/valcn_v2_platform_light/` | VALCN v2.0 master plan + task cards + RV report |
| `docs/AI/tasks/{nginx_per_user_rate_limit,blazor_api_aggregation,api_rate_limit_classification}_task_card.md` | Deferred nginx improvement task cards |
| `docs/AI/tasks/tech_debt_multi_vps_checkout.md` | Tech debt register |
| `docs/Architecture/ADR001-Station-Architecture.md` | ADR-001 v3 (Option C) |
| `docs/AI/project_state_archive.md` | Archived history (2026-07-24 + 2026-08-03 + 2026-08-09) |

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
- **Verified Facts:** Branch=`main`, last commit `f7201ef4`. Sprint A+B COMPLETE + PUSHED to origin. 4 hardcoded tenant IDs replaced with config-driven values. Settlement History admin page created (Gateway controller + API client + Razor page + nav link). NavMenu 30/30 admin pages covered. Build: 0 errors. CI: 1261 unit + 233 integration + 39 architecture tests ALL PASS. Guard-check ALL PASSED.
- **Open Questions:** 0
- **Gate 6 Status:** ✅ Assumptions (0) < Verified Facts (50+), Open Questions (0) < 3

---

## 10. Maintenance Log

> Full historical maintenance log: see `docs/AI/project_state_archive.md`.

* **2026-08-14 — GUARD QR VERIFY (ISSUE #126) SPRINT 0 + SPRINT 1 COMPLETE.** Branch `feature/guard-qr-r1` → merge to `main`. Sprint 0: 6 integration points verified (UserRole.Guard, R2, QR gen, QR scan, Printer, EF migration) + 8 BR spec drafted + R2 bucket `vanan-guard-photos` created + verified (5/5 tests pass). Sprint 1: Domain (`VehicleSession` + `GuardScanLog` + 2 enums + 2 VOs in `1_Shared/Domain.cs`) + EF config (2 configurations) + migration `20260814042520_AddGuardQrVerifyTables` (clean — 2 tables + 7 indexes) + R2 storage service (`IR2StorageService` + `R2StorageService` with AWSSDK.S3) + repositories (4 files) + DI registration + appsettings R2 config. Build 0 errors · guard-check ALL PASSED · Architecture Guard PASSED · Fast test gate PASSED. Next: Sprint 2 (Gateway API — GuardController).
* **2026-08-13 — SPRINT A+B COMPLETE + PUSHED.** Commit `f7201ef4`. Sprint A: 4 hardcoded tenant IDs → config-driven (`IConfiguration["Seed:TenantId"]`). Sprint B1: Settlement History admin page (Gateway `SettlementAdminController` + ShopERP `SettlementApiClient` + `Settlements.razor` + NavMenu link). Sprint B2: Tenant Settings already in TenantManagement edit modal. NavMenu: 30/30 admin pages now have nav links (added Background Services). CI: 1261 unit + 233 integration + 39 arch tests ALL PASS. Guard-check ALL PASSED.
* **2026-08-11 — GITHUB ISSUES BATCH #114/#123/#124/#125 — ALL 4 FIXED + DEPLOYED + RV 33/33 PASS.** 3 commits: `716e7eec` (#123 SQLite IsGlobal migration + #124 redeem button IsAvailable + admin menu + #125 KhachLink bottom nav responsive) + `07228b7e` (#114 initial — IsPosOnly field + Product entity + DTO + EF migration + seed + filter + POS Create.razor) + `f46f544c` (#114 r1/r2/r3 — seed update existing products IsPosOnly=true + Include Items in kitchen query + POS CustomerNotes + voice note STT + Kitchen TTS auto-read). RV on VPS: 33 PASS + 0 FAIL + 1 WARN (false positive — Login page SSR no "blazor" keyword). All 3 VPS healthy, no 502/503/504, no regression. POS-only "Sản phẩm dịch vụ" hidden from public catalog + grouped catalog. JS files served: pos-voice-note.js + tts-reader.js. Global catalog has isAvailable field (#124 verified).
  - **#114 r1.1 root cause:** Product seeded BEFORE IsPosOnly flag added → `if(exists) continue` skipped update → IsPosOnly=false → IsPriceEditable=false → no inline price input.
  - **#114 r1.2 root cause:** `OrderRepository.GetByStatusAsync` used `AsNoTracking()` without `.Include(o => o.Items)` → Items null → kitchen shows empty items.
  - **#114 r2:** Added CustomerNotes textarea + Web Speech API (vi-VN) STT button + `pos-voice-note.js` + `CreateOrderCommand.CustomerNotes` (was missing for POS orders).
  - **#114 r3:** Kitchen Display.razor — "Đọc ghi chú" TTS button on all 3 columns + AutoReadNewOrderNotes (orders <30s old) using existing `tts-reader.js`.
* **2026-08-10 — ORDER SYNC FIX COMPLETE + DEPLOYED + VERIFIED END-TO-END.** 7 commits: `55ece765` (Gateway seed + OrderSyncSubscriber retry) + `6d4bec87` (voice search StoreFinder) + `2c701e94` (test hang fix) + `ffe76c89` (CD .env.gateway + container name) + `e3700af4` (seed auto-create + reassign drifted) + `76378549` (CD env section) + `7bbc26c2` (CD SSH envs list). Root cause: 4-layer CD config gap → SHOP_INSTANCE_ID never reached Gateway VPS → seed fallback to wrong ShopInstance → NATS subject mismatch. RV full test: 8/8 PASS. Order sync verified: ShopInstance `9e94f876-...` auto-created, 10 tenants reassigned, GMV +108,900 exact.
  - **RV Full Test (8 cases):** Login PASS · Feature Flags PASS · Network Dashboard PASS · Background Services PASS · Toggle Flag PASS · Order Sync PASS · Voice Search PASS · Dashboard Metrics PASS.
  - **3 Issues verified:** (1) Order sync mismatch — FIXED. (2) GMV cache 10-min — by design, not a bug. (3) ActiveCustomers=0 — guest checkout CustomerId=null, defer to CRM phase.
* **2026-08-09 — VALCN v2.0 PLATFORM-LIGHT — WAVE 3 COMPLETE + DEPLOYED + RV PASS.** 7 commits: `9a4d0e9b` (W3 code) + `d1e71f21` (CD SSH fix) + `f9f59ef6` (DI fix) + `f0e42a28` (NavMenu + user guide fix) + `33b4c40f` (SQLite migration) + `e7514adc` (nginx 5-layer rate limit) + `bb698f7c` (deferred task cards). RV: 10 PASS + 1 PARTIAL + 2 FAIL→FIXED→VERIFIED. CI/CD SUCCESS. Build: 0 errors.
  - **NavMenu fix:** AdminLayout.razor missing 3 nav entries → added + verified.
  - **SQLite migration:** ShopFeatureSettings.PlatformFeeRate missing in SQLite → migration added → GET+PUT 200.
  - **nginx 503 fix:** Root cause = API + page loads shared rate limit quota. 5-layer strategy: /api/ (zone=api burst=200) + /Login (zone=auth 5r/m) + /_blazor (limit_conn only) + / (zone=web burst=200). Load test: 0 503 across 500+ requests.
  - **3 deferred task cards:** per-user rate limit, Blazor bootstrap, API classification.
* **2026-08-09 — VALCN v2.0 WAVE 3 CODE COMPLETE (Phase 4 + Phase 7).** RefundOrchestrationService (4-step reversal) + NetworkDashboardService (8 metrics). Both feature-flagged, default OFF.
* **2026-08-09 — VALCN v2.0 WAVE 2 COMPLETE (Phase 2 + Phase 3).** Platform Fee + Loyalty Budget. Both feature-flagged, default OFF.
* **2026-08-09 — VALCN v2.0 WAVE 1 COMPLETE (Phase 0 + Phase 1).** 12 additive fields + LoyaltyIssuanceRecord + feature flag infra. All flags default OFF.
* **2026-08-09 — GATEWAY REFACTOR HYBRID BƯỚC 1 COMPLETE + RV 11/11 PASS.** Poll 10s + 6 toggles + logging reduction.
* **2026-08-09 — PROJECT STATE ARCHIVED (reduction 423 → ~190 lines).** Wave 1-3 details, history log, maintenance log moved to archive.
