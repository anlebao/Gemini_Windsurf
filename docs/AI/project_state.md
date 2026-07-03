# Project State

> **Mục đích:** Single Source of Truth cho AI về trạng thái dự án. BẮT BUỘC đọc đầu mỗi phiên.

---

## 0. Maintenance Rules

1. One-and-only-one: Mỗi section chỉ tồn tại 1 lần.
2. No contradiction: Một hạng mục chỉ có 1 trạng thái.
3. Ground Truth first: Verify path/branch với codebase trước khi ghi.
4. Now over History: Section 2-4 chỉ mô tả việc ĐANG làm và KẾ TIẾP. Việc xong → gom vào Section 6.
5. Actionable Next Actions: Xóa action đã quá hạn/sai bối cảnh.
6. Stamp every edit: Cập nhật Section 11 mỗi lần sửa.

---

## 1. Project Overview

**Dự án:** Vạn An Accounting System MVP — giải pháp kế toán HKD theo TT 152/2025/TT-BTC.
**Stack:** .NET 8 · EF Core · SQLite · Blazor Server (ShopERP) · Blazor WebAssembly (KhachLink PWA) · SignalR · YARP Gateway · xUnit · Playwright.
**Kiến trúc:** Clean Architecture + DDD + Multi-tenancy. Data flow: `KhachLink (5002) → Gateway (5001) → ShopERP (5003) → SQLite`.

**Modules:** `1_Shared` (Domain) · `2_Gateway` (YARP) · `3_CoreHub` (Services, in-process) · `5_WebApps/ShopERP` (Blazor Server) · `5_WebApps/KhachLink` (Blazor WASM) · `UI.Platform` (Shared components) · `6_Tests/6_Testing`.

**Hard stops:** Domain PURE · `AccountingEntry` immutable · Gateway STATELESS · KhachLink HTTP-only · ShopERP SQLite-only · ALWAYS dùng UI Platform components.

---

## 2. Current Objective

**[STREAM B: E2E TEST CLEANUP — ACTIVE]**

Remove ~60 fake/low-value E2E test cases across 20 spec files in `6_Testing/e2e-tests/`. Pattern-based batch fix, 8 waves, 7 anti-patterns. No C# code changes — only `.ts` files.

- **Master plan:** `docs/AI/tasks/e2e_test_cleanup_master_plan.md`
- **Commit planning:** `51dd7ff`
- **Root cause:** ~55% of ~110 E2E test cases are decorative/silent-skip/tautology/anti-schema/anti-UI — false confidence.
- **Target:** 0 decorative `reporter.pass()`, 0 broken auth patterns, 0 anti-schema/anti-UI tests, 9→2 smoke tests, 0 silent-skip, 0 OR-tautology, +regression-prevention lint.

### 8 Waves
| Wave | Pattern | Description | Files | Status |
|---|---|---|---|---|
| 0 | — | Pre-flight verification (auth/admin.json, config, env-config, git clean) | — | ✅ COMPLETE |
| 1 | F | Remove decorative `reporter.pass()` calls | 9 | ✅ COMPLETE (`0c7965e`) |
| 2 | D | Fix wrong auth pattern (fill login form → use global storageState) | 6 | ✅ COMPLETE (`b40b640`) |
| 3 | G1 | Delete anti-schema tests (voice-command, i18n — hallucinated API schema) | 2 | ✅ COMPLETE (`5f57179`) |
| 4 | G2 | Delete anti-UI tests (omnichannel loyalty/offline — no such UI) | 1 | ✅ COMPLETE (`c1e5c59`) |
| 5 | C | Consolidate reachability smoke tests (9 → 2 in new gateway-smoke.spec.ts) | 5 | ✅ COMPLETE (`111197e`) |
| 6 | B | Fix silent-skip (`if(isVisible){}` no else → hard assert or `test.skip`) | 5 | PENDING |
| 7 | A | Fix OR-tautology assertions (`a||b||c` always true → specific state) | 5 | PENDING |
| 8 | — | Regression prevention (strict-assert helper + anti-pattern-lint + README) | 4 | PENDING |

### Parked Streams (awaiting approval)
- **Stream A: EInvoice Provider Rewrite** — Planning complete (`59b60fe`). Blocker: Wave 0 sandbox credentials (1-2 tuần).
- **Stream C: ShopERP UI Fix** — ✅ COMPLETE & MERGED TO MAIN (`f3ed2d2`). 6 waves, 23 .razor files fixed.

---

## 3. Current Status

- **Branch:** `feature/e2e-cleanup-wave5-consolidate-smoke-tests` (off Wave 4 branch @ `c8ed0ee`)
- **Last commit:** `111197e` [E2E-CLEANUP WAVE 5] Pattern C: Consolidate reachability smoke tests (9 → 2)
- **Build:** `dotnet build VanAn.sln` Release → 0 errors ✅ (1000 pre-existing warnings; .ts-only changes, C# unaffected)
- **Guard-check:** PASSED ✅ (untracked files, windsurf-guard v6.0, architecture-guard v1.0)
- **E2E parse check:** `npx playwright test --list` → 668 tests in 21 files ✅ (was 715 in 20; +1 new file, 9 old reachability tests deleted, 2 new consolidated tests added; parse OK)
- **Visual smoke test:** PASSED ✅ (2026-07-03, pre-Stream-B) — 6/6 routes HTTP 200. Screenshots at `6_Testing/reports/smoke-shots/`.
- **Uncommitted changes:** None on Wave 5 branch (only IDE .vs/ artifacts + stray local files: ci_local_output.txt, test_output*.txt, scripts/create-systemadmin.ps1)
- **Completed features (merged to main):** Tenant Onboarding (6 waves) · ShopConfig Refactor (3 phases) · Architecture Test Fixes · CI/CD Hotfix · **Stream C: ShopERP UI Fix (6 waves)**.
- **Pre-existing defects found (NOT Stream C regressions):**
  1. **Blazor circuit crash on `/`, `/sitemap`, `/admin/users`** — `System.InvalidOperationException: Authorization requires a cascading parameter of type Task<AuthenticationState>` from `AuthorizeViewCore.OnParametersSetAsync()`. Pages prerender correctly (visual content visible) but interactivity breaks after circuit connect. Routes.razor has `<CascadingAuthenticationState>` + `<AuthorizeRouteView>` — cascade timing issue. Candidate for a dedicated Blazor auth fix stream (out of Stream B scope — Stream B is .ts-only).
  2. **DevLoginController role mismatch** — `/admin/users` uses `[Authorize(Policy = "OwnerOnly")]` (requires "Owner" role), but `POST /dev/login/systemadmin` issues "SystemAdmin" role → access denied. SystemAdmin dev login cannot reach admin pages. E2E tests must use Owner login for admin routes. **Relevant to Stream B Wave 2** (rbac-enforcement multi-role auth).
- **Dead code note:** `CustomerPage.ts` loyalty methods (`loyaltyPointsDisplay` L44, `getLoyaltyPoints` L191, `applyLoyaltyPoints` L201) are now unreferenced after Wave 4 SCENARIO 2 deletion. Out of Wave 4 scope (master plan W4-T4 only covers test-data-cleaner.ts). Candidate for future page-object cleanup.
- **In-progress:** Stream B Wave 5 complete; Wave 6 (fix silent-skip) ready to start next session.

---

## 4. Next Actions

**Stream B — E2E Test Cleanup (active):**
1. ~~Wave 0 (pre-flight)~~ ✅ — `auth/admin.json` generated by `global-setup.ts` (L111-112); `playwright.config.ts` L34+L56 apply `storageState` globally; `isTierEnabled('e2e')` in `env-config.ts` L359-389; git tree clean; `npx playwright test --list` → 759 tests in 20 files (parse OK).
2. ~~Wave 1: Remove decorative `reporter.pass()`~~ ✅ — 59 calls removed across 9 spec files via brace-balanced Node script (`6_Testing/scripts/wave1-remove-reporter-pass.js`). 4 comment refs preserved. Imports/decls kept (all 9 files still use `reporter.log`/`setArchitectDecision`). `npx playwright test --list` → 759 tests (unchanged). `dotnet build` 0 errors. guard PASSED. Commit `0c7965e` on `feature/e2e-cleanup-wave1-remove-reporter-pass`.
3. ~~Wave 2: Fix auth pattern (Pattern D)~~ ✅ — 6 files fixed. Removed form-fill `beforeEach` (`#username`/`#email`/`#Username` + `waitForURL`) from 5 simple files → rely on global `storageState` (auth/admin.json). Removed `test.use({ storageState: { cookies: [], origins: [] } })` override from 3 einvoice files. `rbac-enforcement`: removed `loginAs` helper; Owner tests (2) use global storageState; 6 multi-role tests (Staff/StoreKeeper/Guard) skipped with note (need `auth/<role>.json` generation — separate task); unauthenticated test wrapped in `test.describe` with empty storageState. `export-excel-flow` staff-role test also skipped. 0 `fill('#username'…)` / 0 `VanAn@2026` / 0 broken `waitForURL` remaining. `npx playwright test --list` → 759 tests (unchanged). `dotnet build` 0 errors. guard PASSED. Commit `b40b640` on `feature/e2e-cleanup-wave2-fix-auth-pattern`.
4. ~~Wave 3: Delete anti-schema tests (Pattern G1)~~ ✅ — 5 tests deleted across 2 files. `voice-command.spec.ts`: removed `TC_Voice_TextCommand` (asserts `result.Command.CommandText`/`CommandType`/`OrderId` but `VoiceCommandController.ProcessTextCommand` returns `{ Success: bool }` @ L78), `TC_Voice_TTS` (asserts fabricated `tts-api.example.com` — string not in codebase; controller returns `{ AudioUrl }` @ L112), `TC_Voice_AudioStorage` (asserts `CleanedFiles`/`TotalExpired`/`Timestamp` schema with try/catch swallow), `TC_Voice_StatusUpdate` (asserts `result.Executed` + `result.Command.CommandType` — same schema mismatch). `i18n.spec.ts`: removed `TC_i18n_VoiceLanguage` (asserts `viResult.Executed`/`enResult.Executed` on voicecommand endpoint returning `{ Success: bool }`). Kept `TC_Voice_Flow` (silent skip — Wave 6) + 5 i18n switch/locale/product tests. `npx playwright test --list` → 729 tests in 20 files (was 759; -30 = 5 × 6 project instances). `dotnet build` 0 errors. windsurf-guard + architecture-guard PASSED. Commit `5f57179` on `feature/e2e-cleanup-wave3-delete-anti-schema-tests`.
5. ~~Wave 4: Delete anti-UI tests (Pattern G2)~~ ✅ — 2 scenarios deleted from `omnichannel-order-lifecycle.spec.ts`. Removed SCENARIO 2 (Returning Loyalty Customer Flow — uses `.loyalty-points`/`.points-balance` selectors + `applyLoyaltyPoints`/`getLoyaltyPoints` page-object methods; 0 matches for `loyalty-points`/`points-balance`/`applyLoyaltyPoints` in `5_WebApps/KhachLink` — no loyalty feature exists). Removed SCENARIO 3 (Network Interruption / Edge Offline Resiliency — asserts `.offline-indicator`/`.network-status` selectors; these strings only in `PWAInstallPrompt.razor` (install prompt, not offline indicator); `context.setOffline(true)` + `page.goto()` will fail since KhachLink is server-rendered Blazor WASM PWA without offline-first outbox). Kept SCENARIO 1 (First-Time Guest Omnichannel Order Flow — uses real CustomerPage selectors). Also removed `TestDataGenerator.generateLoyaltyCustomerData()` from `test-data-cleaner.ts` (only referenced by deleted SCENARIO 2). `npx playwright test --list` → 715 tests in 20 files (was 729; -14 = 2 × 7 project instances). windsurf-guard + architecture-guard PASSED. Commit `c1e5c59` on `feature/e2e-cleanup-wave4-delete-anti-ui-tests`. Dead code note: `CustomerPage.ts` loyalty methods now unreferenced (out of Wave 4 scope).
6. ~~Wave 5: Consolidate reachability smoke (Pattern C)~~ ✅ — 9 reachability tests across 4 files consolidated into 2 tests in new `gateway-smoke.spec.ts` using `test.step()`. Test 1: "Accounting API routes are reachable via Gateway (T-07 alias)" — 5 steps (GET /api/accounting-entries, GET /api/accounting, POST /api/accounting/revenue, POST /api/accounting/expense, GET /api/accounting/revenue/summary). Test 2: "Order, Inventory, and VietQR API routes are reachable via Gateway" — 4 steps (GET /api/orders, GET /api/inventory/check, GET /api/orders/{id}, POST /api/v1/vietqr/generate). Deleted from: `accounting-flow.spec.ts` (5 tests, entire "Gateway Accounting API" describe block), `order-flow.spec.ts` (2 tests: Order API + Inventory check), `order-tracking.spec.ts` (1 test: order status API), `qr-payment-ui.spec.ts` (1 test: VietQR generate; kept `supported-banks returns list` — validates response body, not just reachability). Each step asserts `status !== 404 && status !== 500`, preserving original strict assertions. `npx playwright test --list` → 668 tests in 21 files (was 715 in 20; +1 new file). windsurf-guard + architecture-guard PASSED. Commit `111197e` on `feature/e2e-cleanup-wave5-consolidate-smoke-tests`.
7. **Wave 6: Fix silent-skip (Pattern B)** — NEXT. 6 tests with `if(isVisible){...}` no else → hard assert or `test.skip(condition, reason)`. Branch: `feature/e2e-cleanup-wave6-fix-silent-skip`.
8. **Wave 7: Fix OR-tautology (Pattern A)** — 7 tests with `a||b||c` always-true → assert specific expected state.
9. **Wave 8: Regression prevention** — `utils/strict-assert.ts` helper + `utils/anti-pattern-lint.ts` script + `npm run lint:e2e` + README update + smoke subset run + final state update.

**Stream B — Wave 5 COMPLETE. Wave 6 next session.**

**Deferred (awaiting user decision):**
1. **Push to origin:** `git push origin main` (15 commits ahead of `origin/main` — publish when ready; Stream B Wave 1 is on a feature branch, not yet merged to main).
2. **Stream A: EInvoice Provider Rewrite** — Planning complete (`59b60fe`). Blocker: Wave 0 sandbox credentials (1-2 tuần).
3. **Blazor `CascadingAuthenticationState` circuit crash** — pre-existing, out of Stream B scope (Stream B is .ts-only). Separate FIX_ONLY stream candidate.

---

## 5. Active Architecture Decisions

| Decision | R lý do |
|---|---|
| CoreHub = in-process background service trong Gateway | Monolith Phase 1-2 |
| Gateway = DI composition root cho CoreHub | Program.cs đăng ký CoreHub DbContext/Services |
| ShopERP = SQLite-only edge node | Edge deployment offline-first |
| CustomerToken = `IDataProtector` | Tránh library mới |
| `AccountingEntry` immutable, Reversal Entry | Audit trail bất khả xâm phạm |
| Multi-tenancy `TenantId` filter mọi layer | Data isolation per HKD |

---

## 6. History Log (compressed — see git log for details)

* [2026-07-03] **STREAM B WAVE 2 COMPLETE** — Pattern D (Fix wrong auth pattern). 6 spec files fixed. 5 simple files (`expense-entry-flow`, `export-excel-flow`, `einvoice-dashboard`, `invoice-management`, `provider-management`): removed form-fill `beforeEach` (`#username`/`#email` + `#password` + `waitForURL`), removed `test.use({ storageState: { cookies: [], origins: [] } })` override from 3 einvoice files, removed unused `loadEnvConfig` imports — all rely on global `storageState` (auth/admin.json via `playwright.config.ts` L34+L56). `rbac-enforcement`: removed `loginAs` helper (filled `#Username`/`#Password` on non-existent `/Login` form); Owner tests (2) use global storageState; 6 multi-role tests (Staff/StoreKeeper/Guard) skipped with `test.skip(true, 'Requires auth/<role>.json — not generated by global-setup')` + note; unauthenticated test wrapped in `test.describe` with `test.use({ storageState: { cookies: [], origins: [] } })`. `export-excel-flow` staff-role test also skipped. Verification: 0 `fill('#username'…)` / 0 `fill('#email'…)` / 0 `fill('#Username'…)` / 0 `VanAn@2026` / 0 broken `waitForURL` remaining (grep confirmed). `npx playwright test --list` → 759 tests in 20 files (unchanged). `dotnet build VanAn.sln` → 0 errors. guard-check.ps1 → ALL CHECKS PASSED. Commit `b40b640` on `feature/e2e-cleanup-wave2-fix-auth-pattern`. Next: Wave 3 (delete anti-schema tests — `voice-command.spec.ts` 4 tests, `i18n.spec.ts` 1 test).
* [2026-07-03] **STREAM B WAVE 0+1 COMPLETE** — E2E Test Cleanup started. Wave 0 pre-flight: verified `auth/admin.json` generation (`global-setup.ts` L111-112), `playwright.config.ts` L34+L56 global `storageState`, `isTierEnabled('e2e')` in `env-config.ts` L359-389, git tree clean, `npx playwright test --list` → 759 tests in 20 files (parse OK). Wave 1 (Pattern F — Remove decorative `reporter.pass()`): removed 59 calls across 9 spec files (`accounting-flow` 14, `order-flow` 7, `order-tracking` 7, `qr-payment-ui` 7, `audit-trail-flow` 6, `period-closing-flow` 5, `van-an-dashboard` 5, `balance-dashboard-flow` 4, `qr-payment` 4) via brace-balanced Node script `6_Testing/scripts/wave1-remove-reporter-pass.js` (handles single+multi-line forms, skips comments/strings, conditionally removes TestReporter import/decl — none unused). 4 `reporter.pass` comment refs preserved (T-17/T-19/T-21 FIX notes). All 9 files retain `reporter.log`/`setArchitectDecision` usage in `beforeAll`. Verification: 0 `reporter.pass(` calls remain (grep confirms only 4 comment refs), `npx playwright test --list` → 759 tests in 20 files (unchanged), `dotnet build VanAn.sln` → 0 errors, guard PASSED. Commit `0c7965e` on `feature/e2e-cleanup-wave1-remove-reporter-pass`. Pre-Stream-B hygiene commit `797ce36` (committed uncommitted smoke test state from prior session). Next: Wave 2 (fix auth pattern in 6 files — `expense-entry-flow`, `export-excel-flow`, `einvoice-dashboard`, `invoice-management`, `provider-management`, `rbac-enforcement`).
* [2026-07-03] **VISUAL SMOKE TEST PASSED** — Post-Stream-C merge validation. Built VanAn.sln (0 errors), started ShopERP on port 5003, authed via DevLoginController (`POST /dev/login` → Owner role). Playwright headless Chromium 1440×900 navigated 6 routes: `/` (→/sitemap redirect by design, Wave 6 Home.razor), `/sitemap` (Vạn An Ecosystem), `/access-denied` (403 — Không Có Quyền Truy Cập), `/accounting` (Kế Toán Dashboard), `/einvoice` (Dashboard Hóa Đơn Điện Tử), `/admin/users` (Quản lý người dùng). All 6 HTTP 200, all render with sidebar+nav+page title. Screenshots: `6_Testing/reports/smoke-shots/{Home,Sitemap,AccessDenied,Accounting,EInvoice,AdminUsers}.png`. Stream C waves verified in production render: Wave 1 (UI.Platform CSS + Bootstrap Icons CDN load), Wave 3 (`css/pages.css` 6585B loads), Wave 4 (`VanAButton` renders — confirmed in server logs), Wave 5 (AdminLayout on /admin/users), Wave 6 (Home redirect + AccessDenied 403 message, no inline `<style>`). **Pre-existing defects found (NOT Stream C regressions):** (1) Blazor circuit crash on `/`, `/sitemap`, `/admin/users` — `System.InvalidOperationException: Authorization requires a cascading parameter of type Task<AuthenticationState>` from `AuthorizeViewCore.OnParametersSetAsync()`; pages prerender correctly but interactivity breaks after circuit connect; Routes.razor has `<CascadingAuthenticationState>` + `<AuthorizeRouteView>` — cascade timing issue. (2) DevLoginController role mismatch — `/admin/users` requires "Owner" role (`OwnerOnly` policy) but `/dev/login/systemadmin` issues "SystemAdmin" → access denied for admin routes via SystemAdmin login.
* [2026-07-03] **STREAM C FULLY DONE & MERGED TO MAIN** — Wave 6 (Governance cleanup) + fast-forward merge of all 6 waves into `main` (`cc10e08..f3ed2d2`, 46 files, +1622/-428 lines). Wave 6 details: Remove inline `<style>` blocks from AccessDenied.razor (37 lines), Sitemap.razor (72 lines), AuditTrail.razor (165 lines — exit criteria required 0 inline `<style>` in ALL .razor files; CSS already covered by `.razor.css` from Wave 3 with better design tokens). Fix Sitemap logout: replace `JSRuntime.InvokeVoidAsync("eval", ...)` (security concern — eval + manual cookie clear) with `NavigationManager.NavigateTo("/Logout", forceLoad: true)` using existing `Pages/Logout.cshtml` server-side `SignOutAsync` endpoint. Remove now-unused `@inject IJSRuntime JSRuntime`. Fix 7 broken emojis (U+FFFD replacement chars from encoding corruption) in Sitemap with semantic emojis: 📷 (Guard scan), 💸 (Chi Phí), 📈 (Doanh Thu), 📜 (Lịch Sử), 📅 (Đóng Kỳ), 🌐 (KhachLink), 🔑 (Nhóm Quyền). Delete `Counter.razor` (Blazor template demo, 0 references in NavMenu/Sitemap). Fix `Home.razor`: add `<PageTitle>Đang chuyển hướng...</PageTitle>` + loading state div + new `Home.razor.css` (CSS isolation for `.redirect-loading`). Build 0 errors on main post-merge, guard PASSED. Commits `ea1ced5` + `f3ed2d2`. **Stream C complete — 14 dead → 0, 18 unstyled → 0, 3 broken layouts → 0.**
* [2026-07-03] **Wave 5 COMPLETE** — Admin layout consistency: Create `AdminLayout.razor` (VanALayout + VanANavigation with 4 Admin menu items matching NavMenu: Users `/admin/users`, Permission Groups `/admin/permission-groups`, Audit Trail `/admin/audit-trail`, Tenants `/admin/tenants`) following AccountingLayout pattern (post-Wave 1 slot fix). Add `@layout AdminLayout` to 4 Admin pages (AuditTrail, UserManagement, PermissionGroupManagement, TenantManagement) at line 3 (after `@page` + `@rendermode`). AdminLayout has no `@attribute [Authorize]` — each page self-authorizes. 5 files, +24 lines. Build 0 errors. Commit `7f05fa7` on `feature/shoperp-ui-fix-wave5-admin-layout`.
* [2026-07-03] **Wave 4 COMPLETE** — Component consolidation: `VanAnAlert` (old, Atomic namespace) → `VanAAlert` (new) in 6 EInvoice files (10 occurrences). API fix: `Type="..."` (unmatched attr, broken) → `Variant="..."` (real param). `Type="danger"` → `Variant="error"` (VanAAlert uses "error"). `VanAnModal`: 0 occurrences in EInvoice scope (only in KhachLink — out of scope, debt cleanup candidate). 6 files, 10 line changes. Build 0 errors. Commit `47268a0` on `feature/shoperp-ui-fix-wave4-component-consolidation`.
* [2026-07-03] **Wave 3 COMPLETE** — Page CSS isolation: `wwwroot/css/pages.css` (shared, 276 lines — `:root` design tokens + 17 common classes: page-header, metrics-grid, filter-grid, vanan-table, status-badge, pagination, etc.) + 18 `.razor.css` files (page-specific classes). Linked in `App.razor`. 20 files, +1228 lines. Build 0 errors. Commit `d2d058d` on `feature/shoperp-ui-fix-wave3-page-css`.
* [2026-07-03] **Wave 2 COMPLETE** — Add `@rendermode InteractiveServer` to 14 files (AccessDenied, Sitemap, AccountingIndex, TransactionHistory, 6 EInvoice, 4 Admin). 14 files, +14 lines. Build 0 errors. Commit `79ec512` on `feature/shoperp-ui-fix-wave2-rendermode`.
* [2026-07-03] **Wave 0 + Wave 1 COMPLETE** — Pre-flight verified. Wave 1: VanALayout.razor.css + VanANavigation.razor.css (NEW), icon fix (`<i class="bi bi-@icon">`), Bootstrap Icons CDN, 3 layout files slot fix, VanADashboard emoji→BI icons. Commit `3b893e8` on `feature/shoperp-ui-fix-wave1-platform-infra`.
* [2026-07-02] **ShopERP UI Fix + E2E Cleanup — PLANNING COMPLETE** — 2 master plans + 14 task cards (`51dd7ff`). UI: 23 files, 6 patterns (P/R/C/V/L/G). E2E: 20 spec files, 7 anti-patterns.
* [2026-07-02] **EInvoice Provider Rewrite — PLANNING COMPLETE** — Master plan + 4 task cards (`59b60fe`). 20 Viettel + 10 MISA API spec mismatches. Wave 0 credential request parallel.
* [2026-07-02] **ShopConfig Refactor — 3 PHASES COMPLETE** — Product→tenant refactor, KhachLink HTTP-only, merged to main.
* [2026-07-02] **Tenant Onboarding — 6 WAVES COMPLETE & MERGED** — Generic multi-industry onboarding (F&B enabled), orchestrator, Gateway API, ShopERP UI, integration tests. Commit `3123b6b`.
* [2026-07-02] **Architecture Test Fixes + CI/CD Hotfix** — 28/28 arch tests PASS, remote CI fixed.
* [2026-07-01] **Tenant Onboarding Waves 1-4** — Abstraction + F&B seed + orchestrator + Gateway API.
* [2026-07-01] **Documentation Added** — KhachLink + ShopERP module docs.

---

## 7. Active Files Reference

### Stream C — ShopERP UI Fix
| File | Role |
|---|---|
| `docs/AI/tasks/shoperp_ui_fix_master_plan.md` | Master plan (6 waves) |
| `docs/AI/tasks/wave1_shoperp_ui_platform_infra_task_card.md` | Wave 1: Pattern P (UI.Platform) |
| `docs/AI/tasks/wave2_shoperp_rendermode_task_card.md` | Wave 2: Pattern R (rendermode) |
| `docs/AI/tasks/wave3_shoperp_page_css_task_card.md` | Wave 3: Pattern C (CSS) |
| `docs/AI/tasks/wave4_shoperp_component_consolidation_task_card.md` | Wave 4: Pattern V (versions) |
| `docs/AI/tasks/wave5_shoperp_admin_layout_task_card.md` | Wave 5: Pattern L (Admin layout) |
| `docs/AI/tasks/wave6_shoperp_governance_cleanup_task_card.md` | Wave 6: Pattern G (governance) |

### Parked Streams
| File | Role |
|---|---|
| `docs/AI/tasks/einvoice_provider_rewrite_master_plan.md` | Stream A (parked) |
| `docs/AI/tasks/e2e_test_cleanup_master_plan.md` | Stream B (parked) |

---

## 8. Architecture Quick Reference

```
KhachLink (5002) → Gateway (5001) → ShopERP (5003) → SQLite
                        ↓
              [in-process CoreHub services]
                        ↓
                  PostgreSQL (prod) / SQLite (edge)
```

**Docker (prod):** postgres · nats · seq · gateway · shoperp · khachlink · nginx · certbot
**Docker (edge):** postgres · nats · gateway · shoperp · shoperp-nats-sync
**CoreHub:** NOT a Docker service — runs in-process inside Gateway.

---

## 9. Maintenance Log

* **Last Updated:** 2026-07-03 — Stream B Wave 5 COMPLETE (Pattern C — Consolidate reachability smoke tests). 9 reachability tests across 4 files consolidated into 2 tests in new `gateway-smoke.spec.ts` using `test.step()`. Test 1: Accounting API routes (5 steps — /api/accounting-entries, /api/accounting, /api/accounting/revenue, /api/accounting/expense, /api/accounting/revenue/summary). Test 2: Order/Inventory/VietQR routes (4 steps — /api/orders, /api/inventory/check, /api/orders/{id}, /api/v1/vietqr/generate). Deleted from `accounting-flow.spec.ts` (5), `order-flow.spec.ts` (2), `order-tracking.spec.ts` (1), `qr-payment-ui.spec.ts` (1; kept `supported-banks returns list` — validates body, not just reachability). Each step asserts `status !== 404 && status !== 500`. `npx playwright test --list` → 668 tests in 21 files (was 715 in 20; +1 new file). windsurf-guard + architecture-guard PASSED. Commit `111197e` on `feature/e2e-cleanup-wave5-consolidate-smoke-tests`. Next: Wave 6 (fix silent-skip).
* **Current Branch:** `feature/e2e-cleanup-wave5-consolidate-smoke-tests`
* **Current Objective:** Stream B — E2E Test Cleanup. Wave 5 done, Wave 6 next.
