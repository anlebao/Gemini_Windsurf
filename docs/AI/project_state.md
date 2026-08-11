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

**GITHUB ISSUES BATCH #114/#123/#124/#125 — ALL 4 FIXED + DEPLOYED + RV 33/33 PASS. VALCN v2.0 PLATFORM-LIGHT — ALL 3 WAVES COMPLETE + DEPLOYED + RUNTIME VERIFIED (archived).**

**Source:** User request 2026-08-11 — fix 4 GitHub issues from "Ready" column + follow-up revisions (r1/r2/r3) for #114.
**Branch:** `main` (always-green, per-issue commits)

**Issues fixed (2026-08-11, 3 commits):**

| Issue | Title | Status | Commit |
|-------|-------|--------|--------|
| #123 | SQLite missing `IsGlobal` column on RedemptionCatalogItems | ✅ FIXED + DEPLOYED + RV PASS | `716e7eec` |
| #124 | Redemption button disabled + missing admin menu | ✅ FIXED + DEPLOYED + RV PASS | `716e7eec` |
| #125 | KhachLink bottom icons hidden with sidebar | ✅ FIXED + DEPLOYED + RV PASS | `716e7eec` |
| #114 | Default "Sản phẩm dịch vụ" POS-only product | ✅ FIXED + DEPLOYED + RV PASS | `07228b7e` + `f46f544c` |

**#114 revisions (r1/r2/r3, commit `f46f544c`):**
- **r1.1** — POS không nhập được giá bán: seed update existing service products `IsPosOnly=true` (was skipped by `if(exists) continue`)
- **r1.2** — Đơn không hiện trên bếp: `OrderRepository.GetByStatusAsync` missing `.Include(o => o.Items)` → Items null → kitchen empty
- **r2** — POS thiếu ghi chú + voice note: added CustomerNotes textarea + Web Speech API (vi-VN) STT button + `pos-voice-note.js` + `CreateOrderCommand.CustomerNotes`
- **r3** — Kitchen TTS: added "Đọc ghi chú" button (ttsReader.speak vi-VN) on all 3 kitchen columns + auto-read for orders <30s old

**RV on VPS (2026-08-11):** 33 PASS + 0 FAIL + 1 WARN (false positive — Login page SSR no "blazor" keyword). All 3 VPS (Gateway + ShopERP + KhachLink) healthy, no regression, no 502/503/504.

> **VALCN v2.0 + Order Sync (archived 2026-08-10):** see `docs/AI/project_state_archive.md` → "Archived 2026-08-09"

---

## 3. Current Status

- **Branch:** `main` · **Last commit:** `f46f544c` · **Working tree:** Clean (2 untracked RV scripts in `.devin/`)
- **.NET SDK:** 8.0.422 · **Build:** 0 errors
- **CI/CD:** CI SUCCESS, CD Multi-VPS SUCCESS (6/6 jobs: Build+Push, Pre-Deploy Validation, Deploy Gateway/KhachLink/ShopERP, Post-Deploy Smoke Test). All for commit `f46f544c`.
- **GitHub Issues Batch:** ✅ #114 + #123 + #124 + #125 ALL FIXED + DEPLOYED + RV 33/33 PASS on VPS.
- **#114 r1/r2/r3:** ✅ POS price entry + kitchen items + notes/voice + TTS — all deployed + verified.
- **VALCN v2.0 RV:** 10 PASS + 1 PARTIAL + 2 FAIL→FIXED→VERIFIED (archived)
- **Order Sync:** ✅ FIXED + VERIFIED end-to-end (archived 2026-08-10).
- **GCP VPS (3 instances):** `vanan-gateway` (e2-small 2GB) — Gateway + Nginx + PG + NATS · `vanan-khachlink` (e2-micro) — KhachLink · `vanan-shop-a` (e2-micro) — ShopERP
- **Domains:** `api2.khachvip.online` (Gateway), `app2.khachvip.online` (ShopERP), `diemthuong2.khachvip.online` (KhachLink), `www2.khachvip.online` (main)
- **nginx:** 5-layer rate limit (static/api/auth/blazor/page) — 0 503 in load test (500+ requests)
- **Background Service Toggle:** `/admin/background-services` — 8 services toggleable
- **Loyalty Alliance:** FULLY OPERATIONAL. Tenant in Silo mode — Alliance infrastructure ready.
- **Known gaps (verified, not bugs):** Network Dashboard cache 10-min (by design); TD-NETDASH-001 (Option B — Order.SetCustomerId Domain change, deferred).
- **Tech debt:** TD-MVPS-001→004, TD-CUSTSYNC-001, TD-ASYNCDP-001, TD-GCP-001, TD-NETDASH-001

---

## 4. Next Actions

**GitHub Issues Batch #114/#123/#124/#125 — ALL COMPLETE + DEPLOYED + RV 33/33 PASS. Browser-level RV pending:**

1. **(Browser RV — #114 POS price entry)** Login ShopERP → /pos → add "Sản phẩm dịch vụ" to cart → verify inline price/name/VAT inputs appear → enter price → create order → verify order appears on /kitchen with items.
2. **(Browser RV — #114 POS notes + voice)** On /pos → enter text in "Ghi chú cho bếp" → click "Ghi âm" → speak → verify transcription fills textarea → create order → verify notes appear on /kitchen.
3. **(Browser RV — #114 Kitchen TTS)** On /kitchen → order with notes → click "Đọc ghi chú" → verify vi-VN speech plays. Verify auto-read for new orders (<30s).
4. **(Browser RV — #124 redeem button)** Login KhachLink → /rewards → verify redeem button enabled for available items → click redeem → verify flow works.
5. **(Browser RV — #125 bottom nav)** KhachLink mobile view → toggle sidebar → verify bottom nav icons still visible.
6. **(Browser RV — Loyalty Config)** Open `/admin/loyalty-config` — verify 4 budget cap fields render (carried over from VALCN v2.0).
7. **(Browser RV — Voice Search)** Open `/stores` on KhachLink → click mic button → speak store name → verify search filters (carried over).

**Deferred / monitoring:**
8. **(GCP Data Seeding)** Seed production data vào GCP DB (fresh DB chỉ có 3 tenants test).
9. **(#99-3 Phase B APPROVAL)** Alliance VND Normalization — HIGH risk, feature-gated. Awaiting user approval.
10. **(Hybrid Strategy Bước 2 — Monitor)** Trigger khi CPU sustained > 70% / Memory > 80%.
11. **Post-Sprint 7 flaky tests:** Fix 4 EInvoiceOrchestratorTests (skipped via `Category!=Flaky` CI filter).
12. **Tech debt cleanup** — TD-MVPS-001→004, TD-CUSTSYNC-001, TD-ASYNCDP-001, TD-GCP-001, TD-NETDASH-001.
13. **(VPS Disk Monitoring)** Cân nhắc `docker image prune -af` vào deploy script hoặc cron job.
14. **(v3.0 deferred)** INV-009 (PointValue field), payment provider integration (VNPay/Momo), Ops Cost metric, Tier Distribution.
15. **(nginx deferred task cards)** `docs/AI/tasks/{nginx_per_user_rate_limit,blazor_api_aggregation,api_rate_limit_classification}_task_card.md`

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
- **Verified Facts:** Branch=`main`, last commit `f46f544c`. GitHub Issues #114/#123/#124/#125 ALL FIXED + DEPLOYED + RV 33/33 PASS on VPS. 3 commits (`716e7eec` + `07228b7e` + `f46f544c`). Build: 0 errors. CI/CD SUCCESS (6/6 CD jobs). VALCN v2.0 + Order Sync archived (COMPLETE + VERIFIED). All 3 feature flags default OFF.
- **Open Questions:** 0
- **Gate 6 Status:** ✅ Assumptions (0) < Verified Facts (70+), Open Questions (0) < 3

---

## 10. Maintenance Log

> Full historical maintenance log: see `docs/AI/project_state_archive.md`.

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
