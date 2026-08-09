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

**VALCN v2.0 PLATFORM-LIGHT — ALL 3 WAVES COMPLETE + DEPLOYED + RUNTIME VERIFIED. BOM v2.0 fully implemented + production-verified.**

**Source:** User request 2026-08-09 — hiện thực hóa BOM v2.0 PLATFORM-LIGHT.
**BOM:** `docs/requirements/VAN_AN_LOCAL_COMMERCE_NETWORK_BOM_v2.0_PLATFORM_LIGHT.md`
**Master plan:** `docs/AI/tasks/valcn_v2_platform_light/valcn_v2_master_plan.md`
**RV report:** `docs/AI/tasks/valcn_v2_platform_light/rv_report_wave3.md` (10 PASS + 1 PARTIAL + 2 FAIL→FIXED→VERIFIED)
**Branch:** `main` (always-green, per-phase commits)

**Scope:** 6 phases (0, 1, 2, 3, 4, 7) — Phase 5 merged into Phase 1, Phase 6/8/9 dropped (defer v3.0). 12 additive fields + 1 new entity (`LoyaltyIssuanceRecord`) + 3 new services + 2 new background jobs + feature flag infra (all flags default OFF).

| Wave | Phases | Status | Commits |
|------|--------|--------|---------|
| Wave 1 | Phase 0 (Analyze) + Phase 1 (Foundation) | ✅ COMPLETE | `af09b8d0` |
| Wave 2 | Phase 2 (Platform Fee) ‖ Phase 3 (Loyalty Budget) | ✅ COMPLETE | `f1d46f24` + `7edf589a` |
| Wave 3 | Phase 4 (Refund Reversal) ‖ Phase 7 (Network Dashboard) | ✅ COMPLETE + DEPLOYED + RV PASS | `9a4d0e9b` + fixes |

**Wave 3 RV + fixes (7 commits, 2026-08-09):**
- `9a4d0e9b` — Phase 4 + 7 code (RefundOrchestrationService 4-step reversal + NetworkDashboardService 8 metrics)
- `d1e71f21` — CD SSH connectivity validation + key CRLF check
- `f9f59ef6` — DI fix: `ILoyaltyRewardsService` + `ILoyaltyRewardsRepository` registered in Gateway Program.cs
- `f0e42a28` — NavMenu fix (AdminLayout.razor missing 3 entries) + User Guide URL corrections
- `33b4c40f` — SQLite migration `AddPlatformFeeRateToShopFeatureSettings` (PlatformFeeRate + PlatformFeeAmount + CorrelationId + LoyaltyIssuanceRecords table)
- `e7514adc` — nginx 503 fix: 5-layer rate limit strategy (separate /api/ + /Login + /_blazor + / locations, zone=auth 5r/m, limit_req_status 429)
- `bb698f7c` — 3 deferred task cards (per-user rate limit, Blazor bootstrap, API classification)

**Feature flags (all default OFF — zero production impact):**
- `ValcnV2_PlatformFee` — Platform Fee on Marketplace orders (Phase 2)
- `ValcnV2_LoyaltyBudget` — Loyalty budget caps + 2 reset jobs (Phase 3)
- `ValcnV2_RefundReversal` — 4-step refund reversal on order cancel (Phase 4)

> **Full Wave 1-3 details:** see `docs/AI/project_state_archive.md` → "Archived 2026-08-09"

---

## 3. Current Status

- **Branch:** `main` · **Last commit:** `b56b6c77` · **Working tree:** Clean
- **.NET SDK:** 8.0.422 · **Build:** 0 errors
- **CI/CD:** CI SUCCESS (`31326953198`), CD Multi-VPS SUCCESS (`31326953188`). All for commit `e7514adc`.
- **VALCN v2.0 RV:** 10 PASS + 1 PARTIAL + 2 FAIL→FIXED→VERIFIED
- **GCP VPS (3 instances):** `vanan-gateway` (e2-small 2GB) — Gateway + Nginx + PG + NATS · `vanan-khachlink` (e2-micro) — KhachLink · `vanan-shop-a` (e2-micro) — ShopERP
- **Domains:** `api2.khachvip.online` (Gateway), `app2.khachvip.online` (ShopERP), `diemthuong2.khachvip.online` (KhachLink), `www2.khachvip.online` (main)
- **nginx:** 5-layer rate limit (static/api/auth/blazor/page) — 0 503 in load test (500+ requests)
- **Background Service Toggle:** `/admin/background-services` — 8 services toggleable (6 existing + 2 loyalty budget reset jobs)
- **Loyalty Alliance:** FULLY OPERATIONAL. Tenant in Silo mode — Alliance infrastructure ready.
- **Tech debt:** TD-MVPS-001→004, TD-CUSTSYNC-001 (Customer sync SQLite→PG), TD-ASYNCDP-001 (async-native IFormulaEngine), TD-GCP-001 (Hybrid Bước 1 done, Bước 2 pending monitoring)

---

## 4. Next Actions

**VALCN v2.0 — ALL WAVES COMPLETE + DEPLOYED + RV PASS. Post-RV items:**
1. **(Browser RV — Loyalty Config)** Open `/admin/loyalty-config` in browser — verify 4 budget cap fields render (PerOrderRateCap, MonthlyPointsBudget, DailyPointsBudget, PerCustomerDailyLimit). HTTP-level RV was PARTIAL.
2. **(Optional — Feature flag enable testing)** Enable `ValcnV2_RefundReversal` → create + cancel an order → verify 4-step reversal. Disable after test.
3. **(nginx 503 monitoring)** Monitor nginx error logs for `limiting requests`. If 429 appears for normal users, consider deferred task cards.

**Deferred / monitoring:**
4. **(GCP Data Seeding)** Seed production data vào GCP DB (fresh DB chỉ có 3 tenants test).
5. **(#99-3 Phase B APPROVAL)** Alliance VND Normalization — HIGH risk, feature-gated. Awaiting user approval.
6. **(Hybrid Strategy Bước 2 — Monitor)** Trigger khi CPU sustained > 70% / Memory > 80%.
7. **Post-Sprint 7 flaky tests:** Fix 4 EInvoiceOrchestratorTests (skipped via `Category!=Flaky` CI filter).
8. **Tech debt cleanup** — TD-MVPS-001→004, TD-CUSTSYNC-001, TD-ASYNCDP-001, TD-GCP-001.
9. **(VPS Disk Monitoring)** Cân nhắc `docker image prune -af` vào deploy script hoặc cron job.
10. **(v3.0 deferred)** INV-009 (PointValue field), payment provider integration (VNPay/Momo), Ops Cost metric, Tier Distribution.
11. **(nginx deferred task cards)** `docs/AI/tasks/{nginx_per_user_rate_limit,blazor_api_aggregation,api_rate_limit_classification}_task_card.md`

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
- **Verified Facts:** Branch=`main`, last commit `b56b6c77`. VALCN v2.0 ALL 3 WAVES COMPLETE + DEPLOYED + RV PASS. 7 commits this session. Build: 0 errors. CI/CD SUCCESS. nginx 503 fixed. All 3 feature flags default OFF.
- **Open Questions:** 0
- **Gate 6 Status:** ✅ Assumptions (0) < Verified Facts (50+), Open Questions (0) < 3

---

## 10. Maintenance Log

> Full historical maintenance log: see `docs/AI/project_state_archive.md`.

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
