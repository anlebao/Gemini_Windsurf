# Project State

> **Mục đích:** Single Source of Truth cho AI về trạng thái dự án. BẮT BUỘC đọc đầu mỗi phiên.
> **Archived:** 2026-07-24 + 2026-08-03 + 2026-08-09 + 2026-08-23 — All completed objectives + full history/maintenance log moved to `docs/AI/project_state_archive.md`

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
**Stack:** .NET 8 — EF Core — SQLite — Blazor Server (ShopERP) — Blazor WebAssembly (KhachLink PWA) — Blazor SSR (Directory) — SignalR — YARP Gateway — xUnit — Playwright.
**Kiến trúc:** Clean Architecture + DDD + Multi-tenancy. Data flow: `KhachLink WASM/SSR (5002) -> Gateway (5001) -> ShopERP (5003) -> SQLite`.
**Modules:** `1_Shared` (Domain + Services contracts) — `2_Gateway` (YARP) — `3_CoreHub` (Services, in-process) — `5_WebApps/ShopERP` (Blazor Server) — `5_WebApps/KhachLink` (Blazor WASM, served by nginx) — `5_WebApps/Directory` (Blazor SSR, Directory-profile tenants) — `UI.Platform` (Shared components) — `6_Tests`.
**Hard stops:** Domain PURE — `AccountingEntry` immutable — Gateway = Order Creator + Routed Async Delivery (Option C) — KhachLink HTTP-only — ShopERP SQLite (Business) + PostgreSQL (Accounting) — ALWAYS dùng UI Platform components.

**VPS Access (GCP — for RV + manual deploy):**
- GCP project: `vanan-prod` (gcloud SDK at `C:\Users\lebao\AppData\Local\Google\Cloud SDK\google-cloud-sdk\bin\gcloud.cmd`)
- SSH command pattern: `gcloud compute ssh <INSTANCE_NAME> --zone <ZONE> --project vanan-prod`
- Instances (4): `vanan-gateway` (asia-southeast1-a, 136.85.94.119) · `vanan-shop-a` (asia-southeast1-b, 34.177.89.248) · `vanan-khachlink` (asia-southeast1-c, 136.85.111.51) · `vanan-khachlink-20260815-timlathay-com` (asia-southeast1-c, 136.85.78.51)
- CD: `cd-multivps.yml` (push to `main`) deploys to all 3 VPS + smoke tests. Legacy `cd.yml` (push to `oracle-prod`) — SSH broken since 2026-08-06, use multi-VPS CD only.

---

## 2. Current Objective

**DIRECTORY SSR — COMPLETE + DEPLOYED + RV FULL PASS.** ✅
- `main` @ `c34a428a` (7 commits). New `5_WebApps/Directory` Blazor SSR .NET 8 app for Directory-profile tenants (timlathay.com). Load: ~10s (22.8MB WASM) → **0.04s cached / 0.56s first**. nginx `map $is_directory` + variable `proxy_pass` with Docker DNS resolver → SSR container (port 8080). 4 runtime fixes (nginx DNS, nginx proxy_pass location, Blazor LayoutComponentBase Body, System.Text.Json enum). RV D3-D8 all PASS: 10 stores render, map works, Commerce unaffected, 56MiB memory. **No remaining actions.**

---

**POST-DEPLOY FIXES — ISSUE #157 + RUNTIME BUGS (3 commits on `main`).** ✅
- **`e7848be9` — Issue #157 (timlathay.com Home page 3 bugs):** (1) nginx `/_framework/` proxy for Directory domains (was 404 HTML → MIME text/html → browser refused blazor.web.js); (2) removed initial tenant list load on page entry (added `_hasSearched` flag); (3) `@bind:event="oninput"` for voice search input persistence.
- **`1eeb4615` — WebSocket + Leaflet markers:** nginx WebSocket upgrade headers (`proxy_http_version 1.1` + `Upgrade`/`Connection` + `proxy_read_timeout 86400s`) for `/_blazor` interactive render mode. Leaflet default marker icons → unpkg CDN (wwwroot/lib/leaflet ships no `images/` subfolder).
- **`6c9182da` — KhachLink Commerce WASM fixes (diemthuong2.khachvip.online):** (1) `ThemeType` JSON enum deserialization — added `JsonStringEnumConverter` to `ShopConfigHttpService` + `TenantProfileHttpService` (Gateway returns `"theme":"Classic"` string, System.Text.Json default expects numbers → JsonException → DefaultShopConfig fallback losing tenant branding); (2) missing `/icons/shortcuts/search.png` + `categories.png` → copied `icon-96x96.png`; (3) ServiceWorker script evaluation failed — merged TWO duplicate `activate` event listeners (v18 missing `event.waitUntil()` → race condition), bumped cache v18 → v19-merge-activate.
- **All 3 commits pushed to `origin/main`, CI PASS, CD deployed.** Pending RV on production.

---

**ISSUE #161 — ACCOUNTING ENTRY VALIDATION + TRANSACTION DATE FIX (commit `5c5a07c5`).** ✅
- **Bug 1:** Revenue/Expense entry forms fail validation dù nhập đủ. Root cause: Blazor `@bind` drop events khi nhập nhanh → `formData.Values` rỗng → `ValidateForm()` fail. Fix: JS interop đọc DOM BEFORE validation + khởi tạo `formData.Values` với defaults + set default Value cho account/category selects.
- **Bug 2:** Transaction history "Ngày" sai (hiện creation time thay vì user-entered date). Root cause: `AccountingEntry` constructor hardcode `TransactionDate = DateTime.UtcNow`. Fix (3-layer): Domain thêm optional `transactionDate` param (backward compatible) → Service pass-through → UI pass user-entered date.
- **Pushed to `origin/main`, CI PASS (1411+17+266+39), CD deployed.** Comment posted on #161.

---

**ISSUE #156 — APPLY GROUP + COLLAPSIBLE NAV TO ALL MENUS (commit `4ee64719`).** ✅
- Áp dụng pattern group-theo-nghiệp-vụ + collapsible (details/summary) từ AdminLayout cho tất cả nav menu:
  - **ShopERP NavMenu.razor:** Convert từ flat AuthorizeView sang VanANavigation với role-based grouped items (Vận hành, Sản phẩm, Kế Toán, Hóa Đơn, CRM, Quản trị, Hệ thống, v.v.)
  - **AccountingLayout.razor:** Group thành Nhập Bút Toán + Báo Cáo
  - **EInvoiceLayout.razor:** Group thành Hóa Đơn + Cấu Hình + Giám Sát
  - **KhachLink NavMenu.razor:** Group desktop sidebar thành Mua sắm + Tích điểm + Tiện ích + Cộng tác viên (mobile bottom bar giữ nguyên)
- **Pushed to `origin/main`, CI PASS (1411+17+266+39), CD deployed.** Comment posted on #156.

---

**FINANCIAL INTELLIGENCE MVP-2 — ALL 5 PHASES COMPLETE (committed, not pushed). Ready for PR + CD + RV.**
- **Branch:** `feature/financial-intelligence-mvp2` (forked from `main` @ `bb7f72c0`, 4 commits: `842b9178` + `bb7f72c0` + `52c55832` + `941e2fbd`)
- **SRS:** `docs/requirements/Van_An_SRS_Financial_Intelligence_MVP2.md` · **Task card:** `docs/AI/tasks/task_financial_intelligence_mvp2.md`
- Phase 1 ✅ Foundation (`bb7f72c0`): BusinessProfile entity + EF migration + repository + service + IncomeStatement extension. 15/15 unit PASS.
- Phase 2 ✅ Calculation Services (`52c55832`): 4 services (ProfitSummary, BreakEven, UnitEconomics, TargetProfit) + 6 guard codes. 37/37 unit + 5/5 integration PASS.
- Phase 3 ✅ API + HTTP Proxy (`52c55832`): FinancialIntelligenceController (7 endpoints) + FinancialIntelligenceHttpService. 10/10 endpoint + 9/9 arch PASS.
- Phase 4 ✅ UI (`941e2fbd`): 4 Blazor pages + Period picker + warning banners + NavMenu + Sitemap + UI Platform 100% + Playwright E2E spec.
- Phase 5 ✅ Polish (`941e2fbd`): FinancialExportService (EPPlus .xlsx) + admin guide doc. Build 0 errors · Guard-check v7.2 PASSED.

**Next Actions:**
1. Push branch `feature/financial-intelligence-mvp2` (await user approval)
2. `gh pr create` → merge → CD Multi-VPS deploy
3. RV L1-L5 (API → static → Playwright → UI flow → manual browser)

---

## 3. Current Status

- **Branch:** `main` @ `4ee64719` (Directory SSR + post-deploy fixes #157 + #161 accounting validation/date fix + #156 nav group/collapsible all menus). **Build full sln:** 0 errors · **CI:** 1411 unit + 17 unit + 266 integration + 39 arch ALL PASS · **.NET SDK:** 8.0.422
- **Directory SSR:** ✅ COMPLETE — timlathay.com live (0.04s load, 10 stores, 56MiB). Issue #157 fixed (3 bugs). WebSocket + Leaflet markers fixed. See Section 2.
- **KhachLink Commerce WASM:** ✅ ThemeType enum + shortcut icons + SW duplicate activate fixed (commit `6c9182da`). Pending RV on `diemthuong2.khachvip.online`.
- **Issue #161 (Accounting):** ✅ Fixed + deployed. Revenue/Expense validation (JS interop DOM read before validate + default formData.Values) + TransactionDate 3-layer fix (Domain optional param → Service pass-through → UI pass user date). Pending RV.
- **Issue #156 (Nav group/collapsible):** ✅ Fixed + deployed. All 4 nav menus converted to grouped/collapsible pattern (ShopERP NavMenu, AccountingLayout, EInvoiceLayout, KhachLink NavMenu). Pending RV.
- **Financial Intelligence MVP-2:** ✅ All 5 phases complete on feature branch (61/61 tests PASS), pending push + PR + CD + RV.
- **Infrastructure (all deployed + RV PASS):** GCP 3 VPS · nginx 5-layer rate limit · Cloudflare R2 (guard photos + auto-cleanup 30d) · Dynamic CORS from KhachLinkInstance registry · KhachLink Multi-Profile R1 enabled · Domain Reseller R1 (GoDaddy API) · Guard QR Verify (Issue #126) · OCR Hub R1 (PaddleOCR client-side) · Plate-as-metadata (PlateNumber optional).
- **Known gaps (verified, not bugs):** Network Dashboard cache 10-min (by design); TD-NETDASH-001 (Order.SetCustomerId Domain change, deferred).
- **Tech debt:** TD-MVPS-001→004, TD-CUSTSYNC-001, TD-ASYNCDP-001, TD-GCP-001, TD-NETDASH-001, TD-OCR-01→05

---

## 4. Next Actions

**Post-deploy RV (pending — commits `e7848be9`, `1eeb4615`, `6c9182da`, `5c5a07c5`, `4ee64719` deployed via CD):**
1. RV `timlathay.com`: (a) `/_framework/blazor.web.js` 200 + `application/javascript` MIME; (b) Home page empty until search (no tenant list on entry); (c) voice search input persists; (d) `/_blazor` WebSocket connects (no console error); (e) StoreFinder map markers render (no 404 for marker-icon.png); (f) close issue #157
2. RV `diemthuong2.khachvip.online`: (a) no `ThemeType` JsonException in console; (b) tenant branding loads from Gateway (theme, colors); (c) `/icons/shortcuts/search.png` + `categories.png` 200; (d) ServiceWorker `v19-merge-activate` activated (no "script evaluation failed"); (e) PWA shortcuts work
3. RV Issue #161 (app2.khachvip.online accounting): (a) Revenue entry submits successfully with all fields filled; (b) Expense entry submits successfully; (c) Transaction history "Ngày" column shows user-entered date (not creation time); (d) close issue #161
4. RV Issue #156 (all nav menus): (a) ShopERP main sidebar groups collapse/expand; (b) Accounting sidebar groups work; (c) EInvoice sidebar groups work; (d) KhachLink desktop sidebar groups work; (e) close issue #156

**Financial Intelligence MVP-2 (active):**
1. Push branch `feature/financial-intelligence-mvp2` (await user approval)
2. `gh pr create` → merge → CD Multi-VPS deploy
3. RV L1-L5

**KhachLink Multi-Profile R2/R3 (deferred):**
- R2 Sprint 7: Reseller profile preset + SystemAdmin UI + tests
- R3 Sprint 8-9: Logistics + JobMarket profiles

**Issue closure (pending manual RV):**
- Issue #130 (Guard QR creation) — 5 fixes applied, pending VPS RV + close
- Issue #126 (Guard QR Verify) — all 3 releases merged, pending manual RV + close

**Deferred / monitoring:**
- R2 (S4 EasyOCR) — deferred until VPS upgrade (4GB RAM) + tenant demand
- GCP Data Seeding — seed production data (fresh DB only 3 test tenants)
- #99-3 Phase B — Alliance VND Normalization (awaiting user approval)
- Hybrid Strategy Bước 2 — trigger when CPU > 70% / Memory > 80%
- Post-Sprint 7 flaky tests — 4 EInvoiceOrchestratorTests (skipped via CI filter)
- v3.0 deferred — INV-009, payment provider (VNPay/Momo), Ops Cost, Tier Distribution
- nginx deferred task cards — per-user rate limit, Blazor API aggregation, API classification

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
| Directory SSR = separate container, nginx map-based routing | Directory-profile tenants get <1s SSR load; Commerce domains keep WASM |

**Deployment Modes:** SaaS (`docker-compose.prod.yml` — all on 1 VPS) ‖ Edge (`docker-compose.edge.yml` — Server A: ShopERP+SQLite+NATS, Server B: Gateway+PG+KhachLink).

---

## 6. History Log (compressed — see archive + git log)

* [2026-08-23] **ISSUE #156 — NAV GROUP + COLLAPSIBLE ALL MENUS.** `4ee64719`. Áp dụng pattern group + collapsible (details/summary) cho tất cả nav: ShopERP NavMenu (convert sang VanANavigation role-based), AccountingLayout (Nhập Bút Toán + Báo Cáo), EInvoiceLayout (Hóa Đơn + Cấu Hình + Giám Sát), KhachLink NavMenu (Mua sắm + Tích điểm + Tiện ích + Cộng tác viên). Pushed, CI PASS, CD deployed. Comment on #156.
* [2026-08-23] **ISSUE #161 — ACCOUNTING ENTRY VALIDATION + TRANSACTION DATE FIX.** `5c5a07c5`. Bug 1: JS interop DOM read before ValidateForm + init formData.Values with defaults + default Value on selects. Bug 2: 3-layer TransactionDate fix (Domain optional param → Service pass-through → UI pass user date). 5 test files updated for Moq. Pushed, CI PASS, CD deployed. Comment on #161.
* [2026-08-23] **POST-DEPLOY FIXES — ISSUE #157 + RUNTIME BUGS (3 commits).** `e7848be9` (issue #157: nginx `/_framework/` proxy + Home page no initial load + voice search `@bind:event="oninput"`). `1eeb4615` (WebSocket `/_blazor` upgrade headers + Leaflet marker icons → CDN). `6c9182da` (KhachLink Commerce: `ThemeType` enum `JsonStringEnumConverter` + missing shortcut icons + SW duplicate `activate` merge v19). All pushed, CI PASS, CD deployed. Pending RV.
* [2026-08-23] **DIRECTORY SSR — ALL 4 PHASES COMPLETE + DEPLOYED + RV FULL PASS.** 7 commits on `main` @ `c34a428a`. New `5_WebApps/Directory` Blazor SSR .NET 8 app for Directory-profile KhachLink tenants (timlathay.com). Load: ~10s (22.8MB WASM) → 0.04s (cached) / 0.56s (first). 4 runtime fixes. CD 4 runs SUCCESS. RV D3-D8 all PASS.
* [2026-08-21] **FINANCIAL INTELLIGENCE MVP-2 — ALL 5 PHASES COMPLETE** on `feature/financial-intelligence-mvp2` (4 commits). BusinessProfile entity + 4 calculation services + 7 endpoints API + 4 Blazor pages + EPPlus export. 61/61 tests PASS. Pending push + PR + CD + RV.
* [2026-08-20] **PLATE-AS-METADATA REFACTOR + R2 PHOTO CLEANUP + QR/OCR FIXES — COMPLETE + DEPLOYED + RV PASS.** PlateNumber optional (154faf19). R2 Cleanup Service (60972c7c + a98e6f7e auth fix + e7911e23). QR white screen root cause (9f8495e9 — vendored qrcode.js corrupt → official v1.4.4). OCR 2-row plate (b07ec9cb).
* [2026-08-19] **OCR HUB R1 COMPLETE + MERGED + DEPLOYED.** QR Wallet 2-tab merge + OCR config infra + PaddleOCR ONNX client-side. #150 JSON case fix + #142 voice search auto-submit.
* [2026-08-17] **DYNAMIC CORS SPRINT 1 COMPLETE + MERGED (PR #133).** DynamicCorsService from KhachLinkInstance registry. RV 8/8 PASS.
* **Older (2026-08-15 and before):** KhachLink Multi-Profile R1, Issue #130, Guard QR Verify #126, Domain Reseller R1, Sprint A+B, GitHub Issues #114/#123/#124/#125, VALCN v2.0, Gateway Refactor, TT 99 compliance, Loyalty Alliance, Community Commerce, Multi-VPS Option C. See `docs/AI/project_state_archive.md`.

---

## 7. Active Files Reference

| File | Role |
|---|---|
| `docs/AI/tasks/directory_ssr/` | Directory SSR master plan + task card + detail coding plan (COMPLETE) |
| `docs/AI/tasks/task_financial_intelligence_mvp2.md` | Financial Intelligence MVP-2 task card (5 phases complete, pending PR) |
| `docs/requirements/Van_An_SRS_Financial_Intelligence_MVP2.md` | Financial Intelligence SRS |
| `docs/AI/tasks/tech_debt_multi_vps_checkout.md` | Tech debt register |
| `docs/Architecture/ADR001-Station-Architecture.md` | ADR-001 v3 (Option C) |
| `docs/AI/project_state_archive.md` | Archived history (2026-07-24 + 2026-08-03 + 2026-08-09 + 2026-08-23) |

---

## 8. Architecture Quick Reference

```
=== SaaS Mode (docker-compose.prod.yml) ===
KhachLink WASM/SSR (5002) → Gateway (5001) → ShopERP (5003) → SQLite (local)
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
- **Verified Facts:** Branch=`main` @ `4ee64719` (5 recent commits: `e7848be9` issue #157, `1eeb4615` WebSocket/Leaflet, `6c9182da` KhachLink enum/icons/SW, `5c5a07c5` issue #161 accounting validation+date, `4ee64719` issue #156 nav group/collapsible). Build 0 errors. 1411 unit + 17 unit + 266 integration + 39 arch ALL PASS. All 5 commits pushed + CI PASS + CD deployed. Fixes target: timlathay.com (Directory SSR) + diemthuong2.khachvip.online (Commerce WASM) + app2.khachvip.online (accounting + nav). Pending production RV for all 5 commits.
- **Open Questions:** 0
- **Gate 6 Status:** ✅ Assumptions (0) < Verified Facts (25+), Open Questions (0) < 3

---

## 10. Maintenance Log

> Full historical maintenance log: see `docs/AI/project_state_archive.md`.

* **2026-08-23 — ISSUE #156 NAV GROUP + COLLAPSIBLE ALL MENUS.** Commit `4ee64719` on `main`. Áp dụng pattern group + collapsible (details/summary) cho tất cả nav: ShopERP NavMenu (convert sang VanANavigation role-based grouped), AccountingLayout (Nhập Bút Toán + Báo Cáo), EInvoiceLayout (Hóa Đơn + Cấu Hình + Giám Sát), KhachLink NavMenu desktop sidebar (Mua sắm + Tích điểm + Tiện ích + Cộng tác viên, mobile bottom bar giữ nguyên). Pushed, CI PASS (1411+17+266+39), CD deployed. Comment on #156.
* **2026-08-23 — ISSUE #161 ACCOUNTING ENTRY VALIDATION + TRANSACTION DATE FIX.** Commit `5c5a07c5` on `main`. Bug 1: Revenue/Expense validation fail — JS interop DOM read before ValidateForm + init formData.Values with defaults + default Value on account/category selects. Bug 2: TransactionDate wrong — 3-layer fix (Domain `AccountingEntry` constructor + `CreateRevenue`/`CreateExpense` factory methods add optional `transactionDate` param → `IAccountingService` + `AccountingEntryService` pass-through → `RevenueEntry.razor` + `ExpenseEntry.razor` pass user-entered date). 5 test files updated for Moq `It.IsAny<DateTime?>()`. Also fixed mojibake em dash in `AccountingEntryDto.cs`. Pushed, CI PASS, CD deployed. Comment on #161.
* **2026-08-23 — POST-DEPLOY FIXES — ISSUE #157 + RUNTIME BUGS.** 3 commits on `main` @ `6c9182da`. `e7848be9`: issue #157 (nginx `/_framework/` proxy for Directory domains + Home page no initial tenant load + voice search `@bind:event="oninput"`). `1eeb4615`: nginx WebSocket upgrade headers for `/_blazor` + Leaflet marker icons → unpkg CDN. `6c9182da`: KhachLink Commerce WASM — `ThemeType` enum `JsonStringEnumConverter` in `ShopConfigHttpService` + `TenantProfileHttpService` + missing shortcut icons + SW duplicate `activate` merge (v18 → v19-merge-activate). All pushed, CI PASS, CD deployed. Pending RV.
* **2026-08-23 — DIRECTORY SSR — ALL 4 PHASES COMPLETE + DEPLOYED + RV FULL PASS.** 7 commits on `main` @ `c34a428a`. New `5_WebApps/Directory` Blazor SSR .NET 8 app (port 8080, 256MB). nginx map-based routing with Docker DNS resolver. 4 runtime fixes: nginx upstream DNS, nginx proxy_pass location, Blazor LayoutComponentBase Body, System.Text.Json enum string conversion. CD 4 runs SUCCESS. RV D3-D8 all PASS: 0.04s cached load, 10 stores, Commerce unaffected, 56MiB. Local test PASS. Pre-push CI: 1411 unit + 266 integration + 39 arch ALL PASS.
* **2026-08-21 — FINANCIAL INTELLIGENCE MVP-2 — ALL 5 PHASES COMPLETE.** Branch `feature/financial-intelligence-mvp2` (4 commits). BusinessProfile entity + 4 calculation services + 7 endpoints API + 4 Blazor pages + EPPlus export. 61/61 tests PASS. Pending push + PR + CD + RV.
