# Project State

> **Mục đích:** Single Source of Truth cho AI về trạng thái dự án. BẮT BUỘC đọc đầu mỗi phiên.
> **Archived:** 2026-07-24 + 2026-08-03 + 2026-08-09 + 2026-08-23 + 2026-09-06 — All completed objectives + full history/maintenance log moved to `docs/AI/project_state_archive.md`

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

**KHACHLINK PROFILE TRANSITION UX — SPRINT 2 COMPLETE + PUSHED + RV PASS.** 🟢
- **Task card:** `docs/AI/tasks/khachlink_profile_transition_ux/task_card_sprint2_transition_messaging.md`
- **Coding plan:** `docs/AI/tasks/khachlink_profile_transition_ux/coding_plan_sprint2_transition_messaging.md`
- **Branch:** `main` @ `61e4d4d4` (Sprint 2 impl)
- **SPRINT 2 COMPLETE (2026-09-09):** 3 UI features, no migration, no Domain changes:
  - P2.1 What's New banner (WhatsNewBanner.razor — direction-specific message + dismiss + auto-hide 10s; detection in KhachLinkLayout via localStorage `last_seen_profile_at` vs `instanceConfig.UpdatedAt`)
  - P3.2 Cart preservation modal (CartPreservationModal.razor — VanAnModal + VanAnButton; triggers on FullCommerce→Directory + cart has items; "Giỏ hàng của bạn (N sản phẩm) vẫn được lưu")
  - P3.1 Onboarding tour (OnboardingTour.razor + onboarding-tour.js + driver.js v1.3.1 vendored; 3-step tour cart→rewards→stores; viewport-aware; 1× per profile via localStorage flag)
  - Supporting: ProfileChangeDirection enum (6 directions + None + Other) · UpdatedAt added to client model + DTO (cache key `_v2`→`_v3`) · nav element IDs (#nav-cart, #nav-rewards, #nav-stores, #nav-stores-mobile)
- **Files:** 8 new + 6 modified (14 total, 1611 insertions)
- **Build:** 0 errors · **CI:** 1489 core + 17 unit + 41 arch + 276 integration ALL PASS · **Pushed:** `61e4d4d4` → main · **CD deployed**
- **RV (2026-09-09):** Layer 1 PASS (API returns updatedAt) · Layer 2 PASS (driver.js + onboarding-tour.js + CSS served, content verified) · Layer 3 PASS (6/6 Sprint 2 E2E tests, 34.2s) · Layer 4 5/6 PASS (cart modal needs manual RV with actual profile change) · Layer 5 PASS (first visit + same profile edge cases)
- **RV Gaps (manual RV needed):** P3.2 cart modal (admin must change diemthuong2 profile FullCommerce→Directory) · P3.1 onboarding tour (admin must change Directory→FullCommerce) · direction-specific banner messages (each direction)
- **Sprint 3 next:** Audit logging + service-worker update behavior

---

## PREVIOUS OBJECTIVE (archived 2026-09-08)

**GTM DRILL MACHINE MVP — W2 COMPLETE + CURRENCY AUTO-FORMAT FIX.** 🟢
- **Task card:** `docs/AI/tasks/gtm_drill_mvp/task_card_w2_interactive_demo.md` (D3 domain mod approved 2026-09-08)
- **Branch:** `main` @ `bfb97afd` (W2 impl `f66a08a1` + E2E fix `c66e94bf` + currency fix `e9688cd6` + E2E test `bfb97afd`)
- **W2 COMPLETE + PRODUCTION RV PASS (2026-09-08):**
  - D3 `TenantRegistration` entity (audit-type, precedent CrawlSource, TenantId=Guid.Empty sentinel, lifecycle Submitted→Contacted→Onboarded/Rejected)
  - PG migration `20260908023803_AddTenantRegistrations` applied on production
  - POST /api/v1/tenant-registrations (AllowAnonymous, rate-limit `registration-submit` 5/IP/24h, 429 response)
  - Turnstile server-side verification (dev fallback skips if no key)
  - Honeypot silent reject (200 fake success, no DB record)
  - KhachLink `/demo` (standalone storefront mock, session-only, 5 industry seeds + generic fallback, theme CSS from Store.razor)
  - KhachLink `/claim` (Register.razor, Turnstile widget + honeypot, ?name= prefill from demo)
  - E2E `gtm-demo.spec.ts` (6 tests, ALL PASS on production `diemthuong2.khachvip.online`)
  - **RV results:** Migration applied ✅ · POST 200 + registrationId ✅ · Honeypot 200 + Guid.Empty (no record) ✅ · Rate limit 429 ✅ · /demo renders ✅ · /claim renders ✅ · CTA navigation ✅ · 6/6 E2E PASS ✅
- **CURRENCY AUTO-FORMAT FIX (2026-09-08):** "Số Tiền (VNĐ)" field trong `/accounting/revenue` + `/accounting/expenses` auto-format với vi-VN thousands separator khi user gõ (55000→55.000). Client-side JS listener (`vananAttachCurrencyFormatter`) attach via `OnAfterRenderAsync` — fires trước Blazor `@bind`, format DOM instantly. E2E `rv-currency-format.spec.ts` 2/2 PASS on production `app2.khachvip.online`.
- **W1 COMPLETE + RV PASS (2026-09-07):** Gateway audit + Directory `/kiem-tra-cua-hang` + nginx routing + rate limit
- **W3-W5 theo card:** Revenue Proof counters (D1) → Merchant Referral (D2) → consent + flag `GrowthMachine:Enabled` default OFF + deploy + RV
- Đối chiếu vs `docs/requirements/Ý tưởng việc tự động hóa (Phễu khách hàng).md`: card hiện thực 6/7 MVP steps, defer AI SDR/scoring (Gate G1)

---

## 3. Current Status

- **KhachLink Profile Transition UX Sprint 2 (Transition Messaging):** ✅ CODE COMPLETE + PUSHED + RV PASS on `main` @ `61e4d4d4` (2026-09-09). 3 UI features, no migration: P2.1 What's New banner (WhatsNewBanner.razor — direction-specific message + dismiss + auto-hide 10s) · P3.2 cart preservation modal (CartPreservationModal.razor — VanAnModal + VanAnButton, FullCommerce→Directory + cart has items) · P3.1 onboarding tour (OnboardingTour.razor + onboarding-tour.js + driver.js v1.3.1 vendored, 3-step tour cart→rewards→stores, viewport-aware, 1× per profile). Supporting: ProfileChangeDirection enum (6 directions + None + Other) · UpdatedAt added to client model + ByDomainResponse (cache key `_v2`→`_v3`) · nav element IDs (#nav-cart, #nav-rewards, #nav-stores, #nav-stores-mobile). 8 new + 6 modified (14 total, 1611 insertions). Build 0 errors · CI 1489+17+41+276 ALL PASS · CD deployed. **RV Layer 1:** API returns updatedAt (diemthuong2=FullCommerce @ 2026-08-21, timlathay=Directory @ 2026-08-19) ✅. **RV Layer 2:** driver.min.js (20324B IIFE) + driver.min.css (3939B) + onboarding-tour.js (2394B) all 200 + content verified on diemthuong2 ✅. **RV Layer 3:** 6/6 Sprint 2 E2E tests PASS (34.2s) ✅. **RV Layer 4:** 5/6 flows PASS (banner shows "nâng cấp" ✅, dismiss+localStorage ✅, nav IDs ✅, first visit no banner ✅, same profile no banner ✅; cart modal on SSR = known limitation — needs WASM + actual profile change). **RV Layer 5:** edge cases PASS ✅. **RV Gaps (manual RV):** P3.2 cart modal (admin change FullCommerce→Directory on diemthuong2) · P3.1 tour (admin change Directory→FullCommerce) · direction-specific messages. See Section 2.
- **KhachLink Profile Transition UX Sprint 1 (Guardrail + Foundation):** ✅ CODE COMPLETE + PUSHED + RV LAYER 1+3 PASS on `main` @ `cbeff2a3` (2026-09-08). 4 UI-only changes, no migration: P1.1 confirm dialog + impact preview (KhachLinkInstances.razor) · P2.2 profile indicator footer (KhachLinkLayout.razor) · P2.3 route guard (ProfileGuard.razor NEW wraps 7 commerce pages) · P4.1 Reseller badge global (moved Home→Layout). 11 files modified + 3 new (ProfileGuard.razor, profile-toast.js, profile-transition.spec.ts 9 tests). Build 0 errors · CI 1489+17+41+276 ALL PASS. **RV Layer 1:** diemthuong2.khachvip.online HTTP 200 (1.2s) + profile-toast.js live ✅ · timlathay.com HTTP 200 (0.19s, no regression, Directory SSR) ✅ · Gateway API returns diemthuong2=FullCommerce (all nav flags true) + timlathay=Directory (most flags false) ✅. **RV Layer 3:** Playwright 2/2 PASS on diemthuong2 (FullCommerce: footer indicator hidden ✅, /cart renders normally ✅). **RV Gaps (can't test on production):** P2.2 indicator visible + P2.3 route guard redirect need Directory-profile KhachLink WASM domain (timlathay.com uses Directory SSR, not WASM) · P4.1 Reseller badge needs customer login + Reseller profile · P1.1 confirm dialog needs SystemAdmin auth (manual RV). See Section 2.
- **GTM Drill Machine W2 (Interactive Demo + Registration):** ✅ CODE COMPLETE + PRODUCTION RV PASS (2026-09-08) on `main` @ `bfb97afd`. D3 `TenantRegistration` entity (audit-type, precedent CrawlSource) + PG migration `20260908023803_AddTenantRegistrations` + POST /api/v1/tenant-registrations (AllowAnonymous, rate-limit 5/IP/24h) + Turnstile server-side verify (dev fallback) + Honeypot silent reject + KhachLink `/demo` (standalone storefront mock, session-only) + KhachLink `/claim` (Register.razor, Turnstile + honeypot, ?name= prefill) + E2E `gtm-demo.spec.ts` (6 tests ALL PASS on production). RV: migration ✅ · API 200 ✅ · honeypot silent ✅ · rate limit 429 ✅ · /demo renders ✅ · /claim renders ✅ · 6/6 E2E PASS ✅. See Section 2 + Section 10.
- **Currency Auto-Format Fix (2026-09-08):** ✅ CODE COMPLETE + PRODUCTION RV PASS on `main` @ `bfb97afd`. "Số Tiền (VNĐ)" field trong `/accounting/revenue` + `/accounting/expenses` auto-format vi-VN thousands separator (55000→55.000). Client-side JS listener `vananAttachCurrencyFormatter` attach via `OnAfterRenderAsync` trong `DynamicFormFields.razor` — fires trước Blazor `@bind`, format DOM instantly (no server round-trip delay). E2E `rv-currency-format.spec.ts` 2/2 PASS on `app2.khachvip.online`. Commits: `16414d9e` (JS interop attempt) → `b2810a38` (@oninput attempt) → `2e279922` (client-side JS listener) → `e9688cd6` (@bind + JS listener final) → `bfb97afd` (E2E test). See Section 2 + Section 10.
- **GTM Drill Machine W1 (Merchant Audit):** ✅ CODE COMPLETE + PRODUCTION RV PASS (2026-09-07) on `main` @ `a21fcffc`. Gateway `GrowthController` (GET /api/v1/growth/audit?name&mst, rate-limit `growth-audit` 10/IP/h, XFF client-IP, 429 response) + Directory `/kiem-tra-cua-hang` landing (Blazor Server SSR) + E2E spec + arch whitelist. **nginx fix:** timlathay.com → Directory SSR (port 8080) thay vì WASM (port 80). RV: Directory SSR ✅ · Gateway JSON ✅ · active tenant ✅ · pending privacy ✅ · rate limit ✅. See Section 2 + Section 10.
- **Branch:** `main` @ `cbeff2a3` (KhachLink Profile Transition Sprint 1. W2 + currency fix. W1 Merchant Audit + nginx Directory SSR routing fix + Gateway 429 rate limit. R2.2 Reseller Accounting — PR #169 merged. Crawl-to-Onboard 8 phases complete. Issue #103/#157/#161/#156 deployed). **Build full sln:** 0 errors · **CI:** 1489 core + 17 unit + 276 integration + 41 arch ALL PASS · **.NET SDK:** 8.0.422
- **R2.2 Reseller Accounting:** ✅ COMPLETE + DEPLOYED + RV PASS (2026-09-06). PR #169 merged. 3 tenant booksets (Supplier/Reseller/Platform-skip-when-VA) + `Order.OwnerTenantId` + Auditor UI `/admin/reseller-accounting-reconciliation` + 13 R2.2 tests + 28 pre-existing bUnit test fixes (DI mocks + vi-VN number format). CD Multi-VPS SUCCESS. RV Layer 1+3+4 PASS. Details: Section 10 + archive 2026-09-06.
- **Directory SSR:** ✅ COMPLETE — timlathay.com live (0.04s load, 10 stores, 56MiB). Issue #157 fixed (3 bugs). WebSocket + Leaflet markers fixed. Details: Section 10 + archive 2026-09-06.
- **KhachLink Commerce WASM:** ✅ ThemeType enum + shortcut icons + SW duplicate activate fixed (commit `6c9182da`). Pending RV on `diemthuong2.khachvip.online`.
- **Issue #161 (Accounting):** ✅ Fixed + deployed. Revenue/Expense validation (JS interop DOM read before validate + default formData.Values) + TransactionDate 3-layer fix (Domain optional param → Service pass-through → UI pass user date). Pending RV.
- **Issue #156 (Nav group/collapsible):** ✅ Fixed + deployed. All 4 nav menus converted to grouped/collapsible pattern (ShopERP NavMenu, AccountingLayout, EInvoiceLayout, KhachLink NavMenu). Pending RV.
- **Issue #103 (Impersonation + data isolation):** ✅ Fixed + deployed + RV PASS. Two commits: `c42c4cbe` (Razor Page flow + dual role + banner) + `73f77f14` (strip SystemAdmin role during impersonation → 9 pages auto-fix to tenant-scoped data). 25/25 access matrix tests PASS. RV 9/9 PASS on `app2.khachvip.online`.
- **Financial Intelligence MVP-2:** ✅ All 5 phases complete on feature branch (61/61 tests PASS), pending push + PR + CD + RV.
- **Infrastructure (all deployed + RV PASS):** GCP 3 VPS · nginx 5-layer rate limit · Cloudflare R2 (guard photos + auto-cleanup 30d) · Dynamic CORS from KhachLinkInstance registry · KhachLink Multi-Profile R1 enabled · Domain Reseller R1 (GoDaddy API) · Guard QR Verify (Issue #126) · OCR Hub R1 (PaddleOCR client-side) · Plate-as-metadata (PlateNumber optional).
- **Crawl-to-Onboard Tenant Pipeline:** ✅ ALL 8 PHASES COMPLETE + DEPLOYED + RV PASS (2026-08-26). PR #164 merged (Phase 6+7+8). 43 new tests (20 domain + 23 service). RV Layer 1 API + Layer 5 DB all PASS. Pattern #8 bug fix in `DuplicateDetectionService` (commit `9cc83534`). Details: Section 10 + archive 2026-09-06.
- **Known gaps (verified, not bugs):** Network Dashboard cache 10-min (by design); TD-NETDASH-001 (Order.SetCustomerId Domain change, deferred).
- **Tech debt:** TD-MVPS-001→004, TD-CUSTSYNC-001, TD-ASYNCDP-001, TD-GCP-001, TD-NETDASH-001, TD-OCR-01→05

---

## 4. Next Actions

**KhachLink Profile Transition UX (✅ Sprint 1 + Sprint 2 COMPLETE + PUSHED + RV PASS — Sprint 3 NEXT):**
- Task card: `docs/AI/tasks/khachlink_profile_transition_ux/task_card_sprint1_guardrail_foundation.md` (3 sprints × 4 mảnh; master plan: `docs/AI/tasks/khachlink_profile_transition_ux/master_plan.md`)
- ✅ Sprint 1 code complete (`cbeff2a3`): P1.1 confirm dialog + P2.2 profile indicator + P2.3 route guard + P4.1 Reseller badge global
- ✅ Sprint 1 pushed to `main`, CI ALL PASS (1489+17+41+276), RV Layer 1+3 PASS on diemthuong2
- ✅ Sprint 2 code complete (`61e4d4d4`): P2.1 What's New banner + P3.2 cart preservation modal + P3.1 driver.js onboarding tour + ProfileChangeDirection enum + UpdatedAt client model + nav IDs
- ✅ Sprint 2 pushed to `main`, CI ALL PASS, CD deployed, RV Layer 1-3+5 PASS, Layer 4 5/6 (cart modal needs manual RV with actual profile change)
- Next: **Manual RV** — admin change diemthuong2 profile FullCommerce→Directory (verify cart modal) + Directory→FullCommerce (verify onboarding tour) + each direction (verify banner messages)
- Next: **Sprint 3** (audit + SW) — AuditableEntityType.KhachLinkInstance=12 + IAuditTrailService inject + admin audit history view + SW version bump
- Branch `main` @ `61e4d4d4` — pushed, CD deployed

**GTM Drill Machine MVP (✅ W1 + W2 COMPLETE + RV PASS — W3 NEXT):**
- Task card: `docs/AI/tasks/gtm_drill_mvp/task_card_w2_interactive_demo.md` (5 tuần × 5 mảnh; strategy: `docs/AI/plans/ecosystem-master-business-model.md` Section 4)
- ✅ W1 Merchant Audit code complete (`e8cd4e62`) + nginx fix + 429 fix (`a21fcffc`): Gateway `GrowthController` + rate limit + Directory landing + E2E spec + arch whitelist + nginx routing to Directory SSR
- ✅ W1 Production RV PASS (2026-09-07): Directory SSR renders · Gateway audit JSON · active tenant found:true · pending tenant privacy · rate limit 10/IP/h
- ✅ W2 Interactive Demo + Registration code complete (`f66a08a1`) + E2E fix (`c66e94bf`): D3 TenantRegistration entity + PG migration + POST /api/v1/tenant-registrations + Turnstile + honeypot + rate limit + Demo.razor + Register.razor + E2E spec
- ✅ W2 Production RV PASS (2026-09-08): Migration applied · API 200 + registrationId · honeypot silent reject · rate limit 429 · /demo renders · /claim renders · 6/6 E2E PASS
- Next: **W3 Revenue Proof** (D1 domain mod — counters on tenant GrowthDashboard) → W4 Referral (D2) → W5 consent + flag `GrowthMachine:Enabled` + deploy + RV
- Branch `main` @ `cbeff2a3` — pushed, CD deployed
- DEFER theo Gates: AI SDR/auto scoring (G1 — 0 telemetry), search counter, sitemap toàn site (BOM #4 T2), payment gateway (G2)

**Crawl-to-Onboard Tenant Pipeline (✅ COMPLETE — all 8 phases deployed + RV PASS):**
- No further actions. All 8 phases complete, PR #164 merged, CD deployed, RV Layer 1 + Layer 5 PASS.
- Optional follow-up: end-to-end manual test (trigger real crawl → Pending tenant → Claim → Approve → Active) on production with real data.

**Crawler post-deploy RV (pending — commit `942467e0` deployed via CD):**
1. RV crawl trigger on production: (a) trigger crawl with industry + province + search term; (b) verify >20 Pending tenants created (pagination working); (c) verify Gateway log shows forwarded body with camelCase fields populated (industry, province, searchTerm NOT null); (d) verify CrawlSources audit rows inserted with correct RawJson

**Post-deploy RV (pending — commits `e7848be9`, `1eeb4615`, `6c9182da`, `5c5a07c5`, `4ee64719` deployed via CD; #103 RV already PASS):**
1. RV `timlathay.com`: (a) `/_framework/blazor.web.js` 200 + `application/javascript` MIME; (b) Home page empty until search (no tenant list on entry); (c) voice search input persists; (d) `/_blazor` WebSocket connects (no console error); (e) StoreFinder map markers render (no 404 for marker-icon.png); (f) close issue #157
2. RV `diemthuong2.khachvip.online`: (a) no `ThemeType` JsonException in console; (b) tenant branding loads from Gateway (theme, colors); (c) `/icons/shortcuts/search.png` + `categories.png` 200; (d) ServiceWorker `v19-merge-activate` activated (no "script evaluation failed"); (e) PWA shortcuts work
3. RV Issue #161 (app2.khachvip.online accounting): (a) Revenue entry submits successfully with all fields filled; (b) Expense entry submits successfully; (c) Transaction history "Ngày" column shows user-entered date (not creation time); (d) close issue #161
4. RV Issue #156 (all nav menus): (a) ShopERP main sidebar groups collapse/expand; (b) Accounting sidebar groups work; (c) EInvoice sidebar groups work; (d) KhachLink desktop sidebar groups work; (e) close issue #156

**Financial Intelligence MVP-2 (active):**
1. Push branch `feature/financial-intelligence-mvp2` (await user approval)
2. `gh pr create` → merge → CD Multi-VPS deploy
3. RV L1-L5

**KhachLink Multi-Profile R2/R3 (deferred):**
- R2.2 Reseller Accounting: ✅ COMPLETE + DEPLOYED + RV PASS (PR #169)
- R2 Sprint 7: Reseller profile preset + SystemAdmin UI + tests — ✅ COMPLETE (earlier PR #167)
- R3 Sprint 8-9: Logistics + JobMarket profiles — deferred
- **R2.2 follow-up:** Configure `PlatformAccountingTenantId` SystemSetting in production DB (1-time setup by SysAdmin via admin UI) to enable Platform bookset entries for non-Vạn An resellers.

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

* [2026-08-25] **ISSUE #103 — IMPERSONATION + DATA ISOLATION.** 2 commits on `main` @ `73f77f14`. `c42c4cbe`: Razor Page flow (`Impersonate.cshtml` + `ExitImpersonate.cshtml`) replacing HttpClient POST — proper HTTP context for Set-Cookie + redirect. Dual role (SystemAdmin + Owner) + `impersonating` marker. Global banner in `MainLayout.razor`. `73f77f14`: data isolation follow-up — strip SystemAdmin role during impersonation (filter `ClaimTypes.Role=="SystemAdmin"` from copied claims) → 9 pages auto-fix to tenant-scoped data. Re-add SystemAdmin on exit. `[AllowAnonymous]` on `exit-impersonation` API + `wasImpersonating` guard. 25/25 access matrix tests PASS. Pushed, CI PASS, CD deployed. RV 9/9 PASS on `app2.khachvip.online`.
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
| `C:\Users\lebao\.devin\plans\plan-915f0a1ede9cf9b3.md` | Crawl-to-Onboard Tenant Pipeline plan (8 phases, 12 design decisions, research complete) |
| `docs/AI/tasks/directory_ssr/` | Directory SSR master plan + task card + detail coding plan (COMPLETE) |
| `docs/AI/tasks/task_financial_intelligence_mvp2.md` | Financial Intelligence MVP-2 task card (5 phases complete, pending PR) |
| `docs/requirements/Van_An_SRS_Financial_Intelligence_MVP2.md` | Financial Intelligence SRS |
| `docs/AI/tasks/tech_debt_multi_vps_checkout.md` | Tech debt register |
| `docs/Architecture/ADR001-Station-Architecture.md` | ADR-001 v3 (Option C) |
| `docs/AI/plans/ecosystem-master-business-model.md` | Ecosystem Master Business Model v1.0 (7 phần + GTM khoan thủng + BOM registry — chiến lược kinh doanh, không phải implementation plan) |
| `docs/AI/tasks/gtm_drill_mvp/` | GTM Drill Machine MVP — master_plan.md + task_card.md (top-level) + 5 per-week task cards (W1 COMPLETE + RV PASS, W2-W5 planned) |
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
- **Verified Facts:** Branch=`main` @ `cbeff2a3` (KhachLink Profile Transition Sprint 1 impl). Sprint 1: 4 UI-only changes (P1.1 confirm dialog + P2.2 profile indicator + P2.3 route guard + P4.1 Reseller badge global), 11 files modified + 3 new (ProfileGuard.razor, profile-toast.js, profile-transition.spec.ts 9 tests). Build 0 errors · pre-commit GUARD v6.0 PASS · CI 1489+17+41+276 ALL PASS · CD Multi-VPS SUCCESS. GTM W2 Interactive Demo + Registration: CODE COMPLETE + PRODUCTION RV PASS — D3 TenantRegistration entity + PG migration `20260908023803_AddTenantRegistrations` applied + POST /api/v1/tenant-registrations 200 + honeypot silent reject + rate limit 429 + KhachLink /demo + /claim + E2E 6/6 PASS on production. GTM W1 Merchant Audit: COMPLETE + RV PASS (2026-09-07). Trên `main`: R2.2 COMPLETE + DEPLOYED + RV PASS (PR #169) · Crawl-to-Onboard 8/8 phases + RV PASS · issue #103/#157/#161/#156 deployed. Production: 3 tenant test (GCP Data Seeding pending).
- **Open Questions:** 1 (PlatformAccountingTenantId not yet configured in production DB — code degrades gracefully, SysAdmin 1-time setup). 1 (Turnstile SiteKey/SecretKey not yet configured in production — dev fallback skips verify, production W5 deploy must provide keys). 1 (Rate limit uses RemoteIpAddress = nginx container IP, not forwarded client IP — all users share quota; W5 deploy should add UseForwardedHeaders for per-IP rate limiting). 1 (Sprint 1 RV partial — Directory-profile WASM domain needed for P2.2 indicator visible + P2.3 route guard redirect RV; timlathay.com uses Directory SSR not WASM; P4.1 Reseller badge needs login; P1.1 confirm dialog needs SystemAdmin auth).
- **Gate 6 Status:** ✅ Assumptions (0) < Verified Facts (30+), Open Questions (4) = 4 → CLEAR (at threshold).

---

## 10. Maintenance Log

> Full historical maintenance log: see `docs/AI/project_state_archive.md`.

* **2026-09-09 — KHACHLINK PROFILE TRANSITION UX SPRINT 2 (TRANSITION MESSAGING) COMPLETE + PUSHED + RV PASS (commit `61e4d4d4` on `main`).** 3 UI features, no migration, no Domain changes: (1) **P2.1 What's New banner** — `WhatsNewBanner.razor` NEW (Components/Shared) — direction-specific message (Directory→FullCommerce="🎉 nâng cấp", FullCommerce→Directory="ℹ️ danh bạ", Directory→Reseller="🎉 mở bán", Reseller→FullCommerce="ℹ️ bán trực tiếp", Other="ℹ️ cập nhật") + dismiss button (sets `whats_new_dismissed` localStorage) + auto-hide 10s; detection in `KhachLinkLayout.OnInitializedAsync` — compares `instanceConfig.UpdatedAt` vs localStorage `last_seen_profile_at`; first visit = no banner; `ProfileChangeDirectionHelper.Compute(oldProfile, newProfile)` returns enum. (2) **P3.2 Cart preservation modal** — `CartPreservationModal.razor` NEW (Components/Shared) — VanAnModal + VanAnButton (UI Platform, verified API: `IsOpen`/`Title`/`OnClose`/`ChildContent`/`Footer`); triggers on FullCommerce→Directory + cart has items; "Giỏ hàng của bạn (N sản phẩm) vẫn được lưu. Do cửa hàng đã chuyển sang chế độ danh bạ, bạn không thể đặt hàng qua app."; cart count from `CartService.GetCartState()` (Scoped) + localStorage `vanan_cart` fallback. (3) **P3.1 Onboarding tour** — `OnboardingTour.razor` NEW + `onboarding-tour.js` NEW + driver.js v1.3.1 vendored (IIFE `window.driver.js.driver({...}).drive()`, ~24KB); 3-step tour cart→rewards→stores; viewport-aware; 1× per profile via localStorage flag; 500ms delay; triggers on Directory→FullCommerce/Reseller. Supporting: `ProfileChangeDirection.cs` NEW (enum 6 directions + None + Other) · `UpdatedAt` added to client model + DTO (cache key `_v2`→`_v3`) · nav IDs `#nav-cart`/`#nav-rewards`/`#nav-stores`/`#nav-stores-mobile`. Files: 8 new + 6 modified (14 total, 1611 insertions). Build: 0 errors · CI: 1489+17+41+276 ALL PASS · GUARD v6.0 PASS · CD deployed. **RV:** Layer 1 PASS (API updatedAt) · Layer 2 PASS (driver.js + onboarding-tour.js + CSS served, content verified) · Layer 3 PASS (6/6 Sprint 2 E2E, 34.2s) · Layer 4 5/6 PASS (cart modal on SSR = known limitation — JS interop not available in OnInitializedAsync during prerendering; needs WASM + actual profile change) · Layer 5 PASS (edge cases). **RV Gaps (manual):** P3.2 cart modal (admin change FullCommerce→Directory on diemthuong2) · P3.1 tour (admin change Directory→FullCommerce) · direction-specific messages. Branch: `main` @ `61e4d4d4`.

* **2026-09-08 — KHACHLINK PROFILE TRANSITION UX SPRINT 1 (GUARDRAIL + FOUNDATION) COMPLETE + PUSHED + RV LAYER 1+3 PASS (commit `cbeff2a3` on `main`).** Reduce customer confusion when SystemAdmin switches KhachLink profile (Directory → Reseller → FullCommerce). 4 UI-only changes, no migration: (1) **P1.1 Confirmation dialog + impact preview** trong `KhachLinkInstances.razor` — khi SystemAdmin đổi profile hoặc nav flags, modal confirm hiện diff list (nav items added/removed) trước khi save; `_editOriginal` snapshot + `_confirmDiff` list + `ConfirmSubmit` flow; CSS `.confirm-diff-alert` + `.confirm-diff-list`. (2) **P2.2 Profile indicator persistent** trong `KhachLinkLayout.razor` footer — `.profile-indicator` div hiện "Chế độ: {label}" khi profile != FullCommerce (default ẩn); `GetProfileLabel()` helper (Directory→"Danh bạ cửa hàng", Reseller→"Đại lý Vạn An", FullCommerce→"Cửa hàng trực tiếp"). (3) **P2.3 Route guard** — `ProfileGuard.razor` NEW (Components/Shared) — CascadingParameter NavFlags + RequiredFlag + FeatureName + redirect to / + toast JS (`vananShowProfileToast` in `profile-toast.js` NEW); wraps 7 commerce-only pages: Cart (ShowCart), OrderHistory (ShowOrders), RedemptionCatalog (ShowRewards), Missions (ShowMissions), LoyaltyCard (ShowLoyaltyHistory), Scan (ShowScan), Campaigns (ShowCampaigns). (4) **P4.1 Reseller badge global** — di chuyển badge block từ `Home.razor` → `KhachLinkLayout.razor` (sau header, trước main) — shows "Mô hình Reseller — Vạn An mua bán, giá đã bao gồm phí nền tảng" on all pages (not just Home); `_commerceModeLoaded` + `_isReseller` fields + `CommunityHttp.GetCommerceModeAsync` fetch in `OnAfterRenderAsync` (moved from Home.razor). Files: 11 modified + 3 new (ProfileGuard.razor, profile-toast.js, profile-transition.spec.ts 9 tests) + 5 docs (master_plan + 4 task cards). Build: 0 errors · CI: 1489+17+41+276 ALL PASS · pre-commit GUARD v6.0 PASS · CD Multi-VPS SUCCESS. **RV Layer 1:** diemthuong2.khachvip.online HTTP 200 (1.2s) + profile-toast.js live ✅ · timlathay.com HTTP 200 (0.19s, no regression, Directory SSR) ✅ · Gateway API: diemthuong2=FullCommerce (all nav flags true) + timlathay=Directory (most flags false) ✅. **RV Layer 3:** Playwright 2/2 PASS on diemthuong2 (FullCommerce: footer indicator hidden ✅, /cart renders normally ✅). **RV Gaps:** P2.2 indicator visible + P2.3 route guard redirect need Directory-profile KhachLink WASM domain (timlathay.com uses Directory SSR, not WASM) · P4.1 Reseller badge needs customer login + Reseller profile · P1.1 confirm dialog needs SystemAdmin auth (manual RV). Branch: `main` @ `cbeff2a3`.

* **2026-09-08 — CURRENCY AUTO-FORMAT FIX + PRODUCTION RV PASS (commits `16414d9e` → `b2810a38` → `2e279922` → `e9688cd6` → `bfb97afd` on `main`).** Bug: "Số Tiền (VNĐ)" field trong `/accounting/revenue` + `/accounting/expenses` không auto-format số tiền khi user gõ (55000 không hiện 55.000). Root cause: `DynamicFormFields.razor` `FieldType.Currency` dùng `@bind` + `@bind:after` để format, nhưng Blazor Server không push giá trị đã format ngược về DOM — user vẫn thấy giá trị thô. 3 iterations: (1) JS interop từ C# `FormatCurrencyField` — `@bind` overwrite DOM sau JS interop → FAIL; (2) `@oninput` + `value=` — race condition, Blazor re-render với empty `field.Value` trước khi `@oninput` xử lý → DOM bị clear → FAIL; (3) FINAL: `@bind` + client-side JS listener `vananAttachCurrencyFormatter` attach via `OnAfterRenderAsync` — JS listener fires **trước** Blazor `@bind` (client-side, synchronous), format DOM value → `@bind` đọc giá trị đã format → store `field.Value` → push back DOM (same value, no conflict). Files: `UI.Platform/Components/Composite/DynamicFormFields.razor` (OnAfterRenderAsync + HandleCurrencyInput) + `5_WebApps/ShopERP/Components/App.razor` (vananFormatCurrencyInput + vananAttachCurrencyFormatter JS helpers) + `6_Testing/e2e-tests/rv-currency-format.spec.ts` NEW (2 tests). CI: 1504+17+276+41 ALL PASS. CD Multi-VPS SUCCESS. RV: revenue 55000→55.000 ✅ · revenue 1000000→1.000.000 ✅ · expenses 75000→75.000 ✅ · 2/2 E2E PASS on `app2.khachvip.online` (25.2s) ✅. Branch: `main` @ `bfb97afd`.

* **2026-09-08 — GTM DRILL MACHINE W2 (INTERACTIVE DEMO + REGISTRATION) COMPLETE + DEPLOYED + PRODUCTION RV PASS (commits `f66a08a1` + `c66e94bf` on `main`).** D3 domain mod approved (audit-type `TenantRegistration` entity, precedent `CrawlSource`, TenantId=Guid.Empty sentinel, lifecycle Submitted→Contacted→Onboarded/Rejected). Implementation: `TenantRegistration.cs` NEW (1_Shared/Domain/Aggregates/TenantAggregate) + `TenantRegistrationConfiguration.cs` NEW (3_CoreHub/Infrastructure/Configurations) + DbSet added to IVanAnDbContext + VanAnDbContext + excluded from multi-tenancy query filter + ShopERP ShopERPDbContext Ignore (PG-only) + PG migration `20260908023803_AddTenantRegistrations` (indexes on Status + SubmittedAt) + `RegistrationDtos.cs` NEW (request + result + admin DTOs) + `ITenantRegistrationService.cs` + `TenantRegistrationService.cs` NEW (validate + duplicate detect + persist + lifecycle) + `TurnstileVerificationService.cs` NEW (Cloudflare server-side verify, dev fallback skips if no key) + `TenantRegistrationController.cs` NEW (POST /api/v1/tenant-registrations AllowAnonymous + rate-limit `registration-submit` 5/IP/24h + honeypot silent reject 200 fake success + admin queue SystemAdmin) + rate policy in `2_Gateway/Program.cs` + Turnstile config placeholders in `appsettings.json` + `RegistrationHttpService.cs` NEW (KhachLink Gateway client) + `ImageUploadService.cs` UPDATE (folder param + UploadLogoAsync for demo-logos) + `DemoStoreState.cs` NEW (in-memory model, 5 industry seeds cà phê/phở/tạp hóa/salon/ăn vặt + generic fallback) + `Demo.razor` NEW (route /demo, standalone no KhachLinkLayout, theme CSS from Store.razor, editable products/hours/theme, CTA → /claim?name=) + `Register.razor` NEW (route /claim, Turnstile widget + honeypot website field, ?name= prefill) + E2E `gtm-demo.spec.ts` NEW (6 tests). CI: 1504+17+276+41+10 ALL PASS. CD Multi-VPS #253 SUCCESS (10m18s). RV: (1) PG migration applied ✅; (2) POST /api/v1/tenant-registrations → 200 + registrationId ✅; (3) Honeypot → 200 + Guid.Empty (no DB record) ✅; (4) Rate limit → 429 ✅; (5) /demo renders (Blazor WASM) ✅; (6) /claim renders ✅; (7) E2E 6/6 PASS on `diemthuong2.khachvip.online` (50.7s) ✅. E2E fix: Blazor `@bind` on enum renders option value as enum name not number + `@bind` sets DOM property not HTML attribute (use `toHaveValue` not `[value=]`). Branch: `main` @ `c66e94bf`.

* **2026-09-07 — W1 PRODUCTION RV PASS + NGINX DIRECTORY SSR ROUTING FIX (commit `a21fcffc` on `main`).** Production RV phát hiện root cause: nginx `timlathay.com` server block route `location /` → `${KHACHLINK_REMOTE_HOST}:80` (KhachLink WASM) thay vì Directory SSR container (port 8080). Docker-compose có `directory` service trên port 8080 nhưng nginx config không có `map $is_directory` hay upstream nào trỏ tới 8080. Fix: trong section `@@EXT_DOMAIN_START:timlathay.com@@`, đổi tất cả non-API `proxy_pass` từ `${KHACHLINK_REMOTE_HOST}:80` → `:8080` (10 occurrences cho apex + wildcard, cả HTTP + HTTPS). API routes vẫn `gateway:80`. Deploy manual: SCP template → VPS → `docker restart vanan-nginx-1`. Gateway fix: `growth-audit` rate limiter thêm `OnRejected` callback trả 429 + JSON message thay vì default 503. RV results sau fix: (1) `timlathay.com/kiem-tra-cua-hang` → HTTP 200, Blazor Server SSR, `VanAn.Directory.styles.css` ✅; (2) `timlathay.com/` → Directory SSR ✅; (3) Gateway audit empty params → 400 + Vietnamese message ✅; (4) Gateway audit no-match → 200 `{"found":false}` ✅; (5) Gateway audit active tenant "Central Mall" → 200 `{"found":true,"tenant":{...}}` không expose phone/email ✅; (6) Gateway audit pending tenant "DONER LAB" → 200 `{"found":true,"tenant":{"isPending":true,...}}` không expose private contact (M3 privacy) + `claimUrl` provided ✅; (7) Rate limit kicks in after ~10 requests → 503 (sau CD sẽ là 429) ✅. Push `a21fcffc` → main, CD triggered. Pending: CD deploy Gateway 429 fix + E2E run.

* **2026-09-06 — GTM DRILL MACHINE W1 (MERCHANT AUDIT) CODE COMPLETE (commits `e8cd4e62` + docs `f78da932` on `feature/gtm-drill-mvp`).** User approved 3 decisions (D1/D2 domain mods · Directory landing · SystemAdmin-dynamic referral commission). Implementation: Gateway `GrowthController.cs` NEW (GET /api/v1/growth/audit?name&mst — anonymous + rate-limit `growth-audit` 10/IP/h; match MST exact per DuplicateDetection precedent + name ILIKE exact-then-contains per TenantStoreController.Search; report chỉ số đo được per Ground Rule 5: presence/storefront/social/KhachLinkDomain/IndustryPeerCount từ crawl; M3 Pending = name/slug only) + rate policy trong `2_Gateway/Program.cs` + Directory `Audit.razor` NEW (route `/kiem-tra-cua-hang`, copy "bán kết quả không bán phần mềm", no initial load per #157, Pending → claim CTA sang KhachLink) + `GrowthAuditService.cs` NEW (forward end-user IP via X-Forwarded-For — Gateway UseForwardedHeaders rewrite → rate-limit partition theo đúng IP user, không phải IP container Directory) + DI trong Directory `Program.cs` + E2E `gtm-audit.spec.ts` NEW (4 tests env-driven) + arch whitelist `GrowthController` trong `AuthorizationEnforcementTests` (precedent ImageUploadController). Fix lúc build: `OwnerTenantId == tenant.Id` ambiguous (Guid? vs TenantId) → `.Value` phía constant; `AlertVariant.Danger` → `Error`. Validation: build full sln 0 errors · pre-commit GUARD v6.0 PASS · arch 41/41 PASS. Note: guard-check.ps1 standalone FAIL ở encoding check trên untracked `.devin/*.json` cũ của session khác — không phải file W1, không đụng. Cùng commit: docs strategy (master business model + task card + 3 requirement docs) — pre-commit guard bắt buộc source + docs cùng batch. Pending: E2E run (cần ecosystem), W2-W5.
* **2026-09-06 — ECOSYSTEM MASTER BUSINESS MODEL v1.0 CREATED (docs-only, branch `main`).** Tổng hợp 3 tài liệu requirements (tầm nhìn 5 tầng "hình dung Vạn An", GTM "khoan thủng thị trường") + 2 session review (5 BOM → BOM 2.0 → GTM review) thành `docs/AI/plans/ecosystem-master-business-model.md`: Ground Rules (North Star = Active Local GMV, danh sách KHÔNG, business-driven gate + kill-list) · Value Chain 5 tầng × 10 layer (NOW/SAU/NEVER — L4 take-rate giờ NOW vì R2.2 done) · 4 actors (KTV = kênh 1-nhiều, hoa hồng tách theo product line) · Flywheel + 2 động cơ zero-CAC (crawler + KTV) · GTM máy khoan thủng bản vá 3 hố (fake precision → chỉ số đo được; cold-start → SEO danh bạ làm traffic chính; scoring defer vì 0 event tracking) · Unit Economics [V]/[A] · Roadmap 2 tuyến 8 tuần + Year 1-3 + Gates G1-G5 · 5-year model (100 tỷ = valuation story 5x revenue, không phải cash) · BOM Registry 5 mô hình chốt (Anchor Kit qua KTV · HĐĐT prepaid · FI premium · TimLaThay funnel · take-rate) + 2 ẩn số thế giới thực (provider HĐĐT margin, KTV/HKD willingness-to-pay). Không thay đổi code. Verified trong session: QĐ 1568/QĐ-BCT (60% DNNVV TMĐT · 100% HĐĐT · 80% cashless) · `Order.ReferralCode` là salesman-level (merchant-referral chưa có) · 0 telemetry/scoring data source (grep) · search ranking thuần relevance (không paid ranking) · production 3 tenant test. **Follow-up cùng session:** tạo task card `docs/AI/tasks/gtm_drill_mvp_task_card.md` (5 tuần × 5 mảnh GTM: Merchant Audit · Interactive Demo · Revenue Proof counters · Merchant Referral · Remote closing consent; 2 Domain mods D1/D2 + 3 open questions chờ user approve; branch `feature/gtm-drill-mvp`; flag `GrowthMachine:Enabled` default OFF; defer AI SDR/scoring theo Gate G1).
* **2026-09-06 — R2.2 RESELLER ACCOUNTING-CASHFLOW ALIGNMENT COMPLETE + DEPLOYED + RV PASS.** PR #169 merged to `main` (commit `2d98ee77`). 3 commits: `ff8827d8` (docs — M2+ design approved) + `598597a8` (impl — 3 tenant booksets) + `9de99c15` (test fix — 28 pre-existing bUnit failures). Implementation: `Order.OwnerTenantId` (Guid?) + `SetResellerPricing` overload + `SourceDomain` end-to-end (KhachLink→Gateway→CoreHub) + Reseller branch in `GenerateAccountingEntriesAsync` (Supplier 511/3331/632 + Reseller 511/3331/632/1331 + Platform 511 skip-when-VA) + Auditor UI `/admin/reseller-accounting-reconciliation` + PG migration `20260906072702_AddOrderOwnerTenantId` + SQLite migration `20260906072856_AddOrderOwnerTenantId` + 13 R2.2 tests. Pre-existing bUnit fixes: `VasReportPageTestBase` missing `IFinancialReportExportService` mock (24 tests) + `ComponentTestBase` missing `ITenantManagementService` mock (3 tests) + `HKDBookDetailTests` vi-VN number format `"20.000.000"` (1 test). CI: 1506+17+276+41+99 ALL PASS. CD Multi-VPS SUCCESS (Gateway+KhachLink+ShopERP+smoke). RV: Layer 1 API 200+CORS ✅, Layer 3 PG+SQLite migrations applied ✅, Layer 4 auditor page 302 (exists) ✅. Follow-up: configure `PlatformAccountingTenantId` SystemSetting in production (1-time SysAdmin setup). Branch: `main` @ `2d98ee77`.
* **2026-08-27 — CRAWLER POST-DEPLOY FIXES (commit `942467e0` on `main`).** 2 bugs from VPS logs: (1) Only 20 tenants despite maxResults=100 — doanhnghiep.vn API hard-caps at 20 items/page regardless of `limit` param; adapter sent limit=100, got 20, stopped. Fix: paginate via `page=1,2,3...` until MaxResults reached or empty page; deduplicate MSTs across pages via HashSet; log page count. (2) Filters not applied (industry=null, province=null in crawler logs) — Gateway forwarded anonymous object with PascalCase props via `PostAsJsonAsync`; crawler minimal API binds case-insensitively but explicit camelCase is safer. Fix: serialize with `JsonSerializerDefaults.Web` (camelCase) + log forwarded body for debugging. Files: `7_Tooling/VanAn.Crawler/Adapters/RestApiAdapter.cs` + `2_Gateway/Controllers/CrawlController.cs`. Build PASS. CI PASS (1454+17+273+41). CD Multi-VPS SUCCESS. Branch: `main` @ `942467e0`. Pending RV: trigger crawl → verify >20 Pending tenants + filters applied.
* **2026-08-26 — CRAWL-TO-ONBOARD PHASE 6+7+8 COMPLETE + DEPLOYED + RV PASS.** PR #164 merged to `main` (commit `845f19e4` + `9cc83534`). Phase 6: KhachLink UI (ImageUploadController + ImageUploadService + ClaimHttpService + Store.razor Pending banner + Claim.razor form). Phase 7: ShopERP Admin UI (TenantClaimApiClient + TenantManagement 3 tabs + ClaimsQueue + CrawlTrigger + NavMenu). Phase 8: 43 new tests (20 domain + 10 OnboardUnverified service + 13 TenantClaimService) + bug fix (ListPendingClaimsAsync c.Id→c.TenantId) + W12-G7 whitelist ImageUploadController. RV Layer 1 API: pending 200, duplicates 200 (after Pattern #8 fix in `9cc83534`), claims 200, crawl/trigger 202, images/upload 400. RV Layer 5 DB: TenantClaimRequests (17 cols) + CrawlSources (11 cols) + Tenants.PotentialDuplicateOf + Settings_CrawledPhone + Settings_ContactPhone all verified in PG. CI: 1454+17+273+41 ALL PASS. CD Multi-VPS SUCCESS. Branch: `main` @ `9cc83534`.
* **2026-08-25 — CRAWL-TO-ONBOARD TENANT PIPELINE — PLAN COMPLETE.** New objective. Plan file: `C:\Users\lebao\.devin\plans\plan-915f0a1ede9cf9b3.md` (600 dòng). Pipeline: crawl trangvangvietnam.com (HTML) + doanhnghiep.vn/xinvoice.vn (REST API) → Pending tenant (read-only, SĐT mask ND13/2023) → Owner claim (GPKD upload) → SysAdmin approve → Active tenant + admin user. 12 design decisions locked (governance exceptions: new `7_Tooling/VanAn.Crawler.csproj` + Domain modification). 8 phases: Domain → Migration → Services → API → Crawler → UI KhachLink → UI Admin → Tests+RV. Research verified against codebase. Legal: trangvangvietnam ToS cấm scraping quy mô lớn → batch nhỏ + polite 3-5s; doanhnghiep.vn API = preferred legal source. Awaiting implementation start.
* **2026-08-25 — ISSUE #103 DATA ISOLATION (FOLLOW-UP).** Commit `73f77f14` on `main`. Root cause: impersonation copied ALL claims + added Owner role but did NOT remove SystemAdmin role → user had BOTH roles → `IsInRole("SystemAdmin")=true` → 9 pages showed cross-tenant data (Orders "ALL tenants" dropdown with `IgnoreQueryFilters()`, Accounting/EInvoice EMPTY default, UserManagement/ShopFeatures tenant selector). Fix: strip SystemAdmin role during impersonation in `Impersonate.cshtml.cs` + `AdminController.cs` (filter `ClaimTypes.Role=="SystemAdmin"` from copied claims). Re-add SystemAdmin role on exit in `ExitImpersonate.cshtml.cs` + `AdminController.ExitImpersonation`. `[AllowAnonymous]` on `exit-impersonation` API endpoint + `wasImpersonating` guard. 2 new tests (AM-S24: `/admin/tenants` denied after impersonation; AM-S25: accessible after exit). 25/25 tests PASS. Pushed, CI PASS (1411+17+273+39), CD deployed. RV 9/9 PASS on `app2.khachvip.online` (login → impersonate → /admin/tenants 302 denied → /orders 200 tenant-scoped → exit → /admin/tenants 200 restored → API backward compat 200+200).
* **2026-08-25 — ISSUE #103 IMPERSONATE BUTTON NOT WORKING.** Commit `c42c4cbe` on `main`. Switched from HttpClient POST to Razor Pages (`Impersonate.cshtml` + `ExitImpersonate.cshtml`) for proper HTTP context handling (Set-Cookie + redirect). Dual role (SystemAdmin + Owner) + `impersonating` marker claim. Global banner in `MainLayout.razor` with exit button. `NavMenu.razor` hides "Hệ thống" menu when impersonating. `AdminLayout.razor` renders Owner menu. `TenantManagement.razor` uses NavigateTo. 23 integration tests PASS (18 original + 5 new Razor Page flow). Pushed, CI PASS, CD deployed. RV ALL PASS.
* **2026-08-23 — ISSUE #156 NAV GROUP + COLLAPSIBLE ALL MENUS.** Commit `4ee64719` on `main`. Áp dụng pattern group + collapsible (details/summary) cho tất cả nav: ShopERP NavMenu (convert sang VanANavigation role-based grouped), AccountingLayout (Nhập Bút Toán + Báo Cáo), EInvoiceLayout (Hóa Đơn + Cấu Hình + Giám Sát), KhachLink NavMenu desktop sidebar (Mua sắm + Tích điểm + Tiện ích + Cộng tác viên, mobile bottom bar giữ nguyên). Pushed, CI PASS (1411+17+266+39), CD deployed. Comment on #156.
* **2026-08-23 — ISSUE #161 ACCOUNTING ENTRY VALIDATION + TRANSACTION DATE FIX.** Commit `5c5a07c5` on `main`. Bug 1: Revenue/Expense validation fail — JS interop DOM read before ValidateForm + init formData.Values with defaults + default Value on account/category selects. Bug 2: TransactionDate wrong — 3-layer fix (Domain `AccountingEntry` constructor + `CreateRevenue`/`CreateExpense` factory methods add optional `transactionDate` param → `IAccountingService` + `AccountingEntryService` pass-through → `RevenueEntry.razor` + `ExpenseEntry.razor` pass user-entered date). 5 test files updated for Moq `It.IsAny<DateTime?>()`. Also fixed mojibake em dash in `AccountingEntryDto.cs`. Pushed, CI PASS, CD deployed. Comment on #161.
* **2026-08-23 — POST-DEPLOY FIXES — ISSUE #157 + RUNTIME BUGS.** 3 commits on `main` @ `6c9182da`. `e7848be9`: issue #157 (nginx `/_framework/` proxy for Directory domains + Home page no initial tenant load + voice search `@bind:event="oninput"`). `1eeb4615`: nginx WebSocket upgrade headers for `/_blazor` + Leaflet marker icons → unpkg CDN. `6c9182da`: KhachLink Commerce WASM — `ThemeType` enum `JsonStringEnumConverter` in `ShopConfigHttpService` + `TenantProfileHttpService` + missing shortcut icons + SW duplicate `activate` merge (v18 → v19-merge-activate). All pushed, CI PASS, CD deployed. Pending RV.
* **2026-08-23 — DIRECTORY SSR — ALL 4 PHASES COMPLETE + DEPLOYED + RV FULL PASS.** 7 commits on `main` @ `c34a428a`. New `5_WebApps/Directory` Blazor SSR .NET 8 app (port 8080, 256MB). nginx map-based routing with Docker DNS resolver. 4 runtime fixes: nginx upstream DNS, nginx proxy_pass location, Blazor LayoutComponentBase Body, System.Text.Json enum string conversion. CD 4 runs SUCCESS. RV D3-D8 all PASS: 0.04s cached load, 10 stores, Commerce unaffected, 56MiB. Local test PASS. Pre-push CI: 1411 unit + 266 integration + 39 arch ALL PASS.
* **2026-08-21 — FINANCIAL INTELLIGENCE MVP-2 — ALL 5 PHASES COMPLETE.** Branch `feature/financial-intelligence-mvp2` (4 commits). BusinessProfile entity + 4 calculation services + 7 endpoints API + 4 Blazor pages + EPPlus export. 61/61 tests PASS. Pending push + PR + CD + RV.
