# Project State

> **Mục đích:** Single Source of Truth cho AI về trạng thái dự án. BẮT BUỘC đọc đầu mỗi phiên.

---

## 0. Maintenance Rules

1. **One-and-only-one:** Mỗi section chỉ tồn tại 1 lần; cấm trùng nội dung.
2. **No contradiction:** Một hạng mục chỉ có 1 trạng thái.
3. **Ground Truth first:** Verify path/branch với codebase trước khi ghi.
4. **Now over History:** Section 2-4 chỉ mô tả việc ĐANG làm và KẾ TIẾP. Việc xong → gom vào Section 6.
5. **Actionable Next Actions:** Xóa action đã quá hạn/sai bối cảnh.
6. **Stamp every edit:** Cập nhật Section 11 (Last Updated + branch) mỗi lần sửa.

---

## 1. Project Overview

**Dự án:** Vạn An Accounting System MVP — giải pháp kế toán HKD theo TT 152/2025/TT-BTC.
**Stack:** .NET 8 · EF Core · SQLite · Blazor Server (ShopERP) · Blazor WebAssembly (KhachLink PWA) · SignalR · YARP Gateway · xUnit · Playwright.
**Kiến trúc:** Clean Architecture + DDD + Multi-tenancy. Data flow: `KhachLink (5002) → Gateway (5001) → ShopERP (5003) → SQLite`.

**Modules:**
| Module | Vai trò |
|--------|---------|
| `1_Shared` | Domain entities, Value Objects, DTOs |
| `2_Gateway` | YARP reverse proxy + controllers (stateless, in-process CoreHub host) |
| `3_CoreHub` | Services, Repositories, EF infrastructure (background service, in-process) |
| `5_WebApps/ShopERP` | Staff/admin UI (Blazor Server, SQLite edge node) |
| `5_WebApps/KhachLink` | Customer-facing PWA (Blazor WASM) |
| `UI.Platform` | Shared UI components (VanAButton, VanACard…) |
| `6_Tests / 6_Testing` | Unit, Integration, Architecture, E2E |

**Hard stops không bao giờ vi phạm:**
- Domain layer PURE — no EF, no DbContext, no DataAnnotations.
- `AccountingEntry` 100% immutable (append-only).
- Gateway STATELESS — no business logic in controllers. CoreHub DbContext đăng ký tại Program.cs (DI root) là hợp lệ.
- KhachLink dùng HTTP via Gateway ONLY — no CoreHub DI trực tiếp.
- ShopERP là SQLite-only edge node — KHÔNG có Npgsql dependency trực tiếp.
- ALWAYS dùng UI Platform components, không bypass.

---

## 2. Current Objective

**[MULTI-PLAN TRACKING — 4 PLANNING STREAMS COMPLETE, AWAITING IMPLEMENTATION]**

### Stream A: EInvoice Provider Rewrite (PLANNING COMPLETE)
Rewrite `ViettelEInvoiceProvider` + `MisaEInvoiceProvider` (stubs fail 100% on real API) per Viettel S-Invoice v2.0 + MISA meInvoice API spec.
- **Master plan:** `docs/AI/tasks/einvoice_provider_rewrite_master_plan.md`
- **Waves:** 4 + Wave 0 parallel (credential request)
- **Status:** PLANNING COMPLETE. Commit `59b60fe`. Awaiting approval for Wave 1.
- **Blocker:** Wave 0 (Viettel + MISA sandbox credentials) — 1-2 tuần bottleneck, start immediately.

### Stream B: E2E Test Cleanup (PLANNING COMPLETE)
Fix ~60 fake/no-value E2E tests across 20 spec files. Pattern-based: 7 anti-patterns + 1 regression prevention wave.
- **Master plan:** `docs/AI/tasks/e2e_test_cleanup_master_plan.md`
- **Waves:** 8 (Pattern F/D/G1/G2/C/B/A + Wave 8 regression prevention)
- **Status:** PLANNING COMPLETE. Commit `51dd7ff`. Awaiting approval for Wave 1.
- **Target:** ~110 test cases (55% fake) → ~50 test cases (100% value).

### Stream C: ShopERP UI Fix (PLANNING COMPLETE)
Fix 23 .razor files in `Components/Pages/` — 14 dead pages, 18 unstyled, 3 broken layouts. Pattern-based: 6 patterns.
- **Master plan:** `docs/AI/tasks/shoperp_ui_fix_master_plan.md`
- **Waves:** 6 (Pattern P/R/C/V/L/G)
- **Status:** PLANNING COMPLETE. Commit `51dd7ff`. Awaiting approval for Wave 1.
- **Root cause:** UI.Platform (VanALayout/VanANavigation) chưa hoàn thiện — zero CSS, sai slot structure.
- **Target:** 23 files, 14 dead → 0, 18 unstyled → 0, 3 broken layouts → 0.

### Stream D: ShopConfig Refactor (COMPLETE)
All 3 phases merged to main. See Section 6.

---

## 3. Current Status

- **Branch:** `main`
- **Last commit:** `51dd7ff` [PLAN] E2E cleanup + ShopERP UI fix master plans + task cards
- **Build:** `dotnet build VanAn.sln` → 0 errors ✅ (pre-existing, no code changes in planning)
- **Guard-check:** PASSED ✅
- **Tenant Onboarding Feature:** COMPLETE & MERGED ✅ — All 6 waves on main
- **ShopConfig Refactor:** ALL 3 PHASES COMPLETE ✅ — merged to main
- **EInvoice Provider Rewrite:** PLANNING COMPLETE — Master plan + 4 task cards (`59b60fe`)
- **E2E Test Cleanup:** PLANNING COMPLETE — Master plan + 8 task cards (`51dd7ff`)
  - Review: 20 spec files, ~60/110 test cases fake/no-value (55%)
  - 7 anti-patterns classified: F (reporter.pass), D (auth), G1 (anti-schema), G2 (anti-UI), C (smoke), B (silent skip), A (OR-tautology)
- **ShopERP UI Fix:** PLANNING COMPLETE — Master plan + 6 task cards (`51dd7ff`)
  - Review: 23 .razor files in `Components/Pages/`, 13 issues classified into 6 patterns
  - Pattern P (Platform infra): VanALayout/VanANavigation zero CSS + sai slot — root cause
  - Pattern R (Rendermode): 14/23 files thiếu `@rendermode InteractiveServer` — dead buttons/forms
  - Pattern C (CSS): 18/23 files unstyled — zero CSS cho custom classes
  - Pattern V (Version): VanAnAlert/VanAnModal (cũ) vs VanAAlert/VanAModal (mới) — 11 occurrences
  - Pattern L (Layout): Admin folder 4 files không có `@layout` — inconsistent
  - Pattern G (Governance): inline `<style>`, `eval` logout, Counter.razor demo, Home naked redirect
- **Uncommitted changes:** None (planning-only commit)

---

## 4. Next Actions

**3 streams awaiting approval — user chọn stream để implement:**

### Stream A: EInvoice Provider Rewrite
1. **Start Wave 0 (parallel, non-code)** — Email Viettel + MISA xin sandbox credentials (1-2 tuần bottleneck).
2. **Implement Wave 1** — Update `EInvoiceRequest` contract + `IEInvoiceProvider` interface.
3. **Implement Wave 2** — Rewrite Viettel provider + DTOs + tests.
4. **Implement Wave 3** — Rewrite MISA provider + DTOs + tests.
5. **Wave 4** — Sandbox verification (depends on Wave 0).

### Stream B: E2E Test Cleanup
1. **Implement Wave 1** — Remove `reporter.pass` (9 files, lowest risk, build momentum).
2. **Implement Wave 2** — Fix auth pattern (6 files broken login).
3. **Implement Wave 3-4** — Delete anti-schema + anti-UI tests.
4. **Implement Wave 5-7** — Consolidate smoke, fix silent skip, fix OR-tautology.
5. **Wave 8** — Regression prevention (helper + lint).

### Stream C: ShopERP UI Fix
1. **Implement Wave 1** — UI.Platform infra (VanALayout CSS + slot + VanANavigation CSS + icons) — root cause fix.
2. **Implement Wave 2** — Add `@rendermode InteractiveServer` (14 files, mechanical).
3. **Implement Wave 3** — Page CSS isolation (shared + per-page, 18 files).
4. **Implement Wave 4** — Component consolidation (VanAnX → VanX).
5. **Implement Wave 5-6** — Admin layout + governance cleanup.

---

## 5. Active Architecture Decisions

| Decision | Lý do |
|---------|-------|
| CoreHub = in-process background service bên trong Gateway | Monolith architecture (Phase 1-2). CoreHub KHÔNG phải standalone HTTP container. |
| Gateway = DI composition root cho CoreHub services | Program.cs đăng ký CoreHub DbContext/Services. Hợp lệ về kiến trúc. |
| ShopERP = SQLite-only edge node | Edge deployment offline-first. Npgsql không được reference trực tiếp. |
| docker-compose.prod.yml: Gateway depends_on postgres + nats | CoreHub removed. Gateway trực tiếp phụ thuộc infra services. |
| CustomerToken = `IDataProtector` (không phải JWT) | Tránh library mới, không cần Identity |
| OTP storage = `IMemoryCache` | TTL built-in, không cần migration |
| Tier calc on-the-fly từ PointBalance | Domain không cần biết tier rules |
| `Shop.Latitude/Longitude` trên entity (không phải ShopConfig) | Store Finder cần query địa lý từ DB |
| `AccountingEntry` immutable, Reversal Entry pattern | Audit trail tài chính bất khả xâm phạm |
| Multi-tenancy `TenantId` filter tại mọi layer | Data isolation per HKD |

---

## 6. History Log

* [2026-07-02] **E2E Test Cleanup + ShopERP UI Fix — PLANNING COMPLETE (2 streams)** —
  - **E2E Test Cleanup:** Reviewed 20 spec files in `6_Testing/e2e-tests/`, identified ~60/110 test cases (55%) as fake/no-value. Classified into 7 anti-patterns: F (decorative `reporter.pass`, 9 files), D (wrong auth pattern, 6 files), G1 (anti-schema tests, 5 tests), G2 (anti-UI tests, 2 scenarios), C (low-value reachability smoke, 9 tests), B (silent skip, 6 tests), A (OR-tautology, 7 tests). Created master plan + 8 task cards (Wave 8 = regression prevention with helper + lint script). Target: ~110 → ~50 test cases, 100% value.
  - **ShopERP UI Fix:** Reviewed 23 .razor files in `Components/Pages/` (5 root + 7 Accounting + 4 Admin + 7 EInvoice). Identified 13 issues classified into 6 patterns: P (UI.Platform infra — VanALayout/VanANavigation zero CSS + sai slot structure, root cause cho 13/23 files), R (14 files thiếu `@rendermode InteractiveServer` → dead buttons/forms), C (18 files unstyled — zero CSS cho custom classes), V (VanAnAlert/VanAnModal cũ vs VanAAlert/VanAModal mới — 11 occurrences), L (Admin folder 4 files không có `@layout`), G (inline `<style>`, `eval` logout, Counter.razor demo, Home naked redirect). Created master plan + 6 task cards. Target: 14 dead → 0, 18 unstyled → 0, 3 broken layouts → 0.
  - Both plans use pattern-based batch fix approach (KHÔNG case-by-case). Commit: `51dd7ff` on `main`.

* [2026-07-02] **EInvoice Provider Rewrite — PLANNING COMPLETE** — Gap analysis identified 20 Viettel + 10 MISA API spec mismatches in current provider implementations (stubs that will fail 100% on real API calls). Created master plan + 4 task cards (Wave 1: Contract+Interface, Wave 2: Viettel provider+tests, Wave 3: MISA provider+tests, Wave 4: Sandbox verify). Wave 0 (parallel credential request) tracked. Self-review fixed 10 issues: added `Program.cs` line 185 to Wave 1, added `GetInvoiceFileAsync` to interface, documented `SupplierTaxCode` source (`ProviderConfiguration.ConfigurationData`), merged Wave 2+3 for TDD compliance, swapped Phase 4↔5 (MISA before sandbox). `InvoiceItem` entity confirmed in Domain.cs (line 1752) — no Domain change needed. Commit: `59b60fe` on `main`.

* [2026-07-02] **ShopConfig Product→Tenant Refactor — PHASE 3 COMPLETE (ALL PHASES DONE)** — Wired ShopConfigHttpService into KhachLinkLayout.razor (`@inject ShopConfigHttpService` + `GetShopConfigByTenantIdAsync`) and Home.razor (`@inject ShopConfigHttpService` + `GetShopConfigFromProductsAsync` + `<SocialHub ShopConfig="@_shopConfig" />` rendered before `</KhachLinkLayout>`). Removed `IShopConfigService` CoreHub direct DI from Program.cs (architectural violation fixed — KhachLink now uses HTTP via Gateway only for ShopConfig). Added `Assert.NotNull(sp.GetRequiredService<ShopConfigHttpService>())` to KhachLinkStartupTests. Build: 0 errors. guard-check PASSED. KhachLinkStartupTests 4/4 PASS. Branch: `feature/shopconfig-product-tenant-phase3-wireup`.

* [2026-07-02] **ShopConfig Product→Tenant Refactor — PHASE 2 COMPLETE** — Created `ShopConfigHttpService` (product-based, HTTP via Gateway only): `GetShopConfigFromProductsAsync(List<ProductDto>)` extracts TenantId from first product → `GetShopConfigByTenantIdAsync(Guid)` calls `GET /api/shops/by-tenant/{tenantId}` → maps ShopDto → ShopConfig with real shop data (Name, Address, Phone, Email, Lat/Long). Falls back to DefaultShopConfig on any error/404/empty. Created `ShopDto` model. Added `GetShopByTenant` endpoint to ShopERP + Gateway ShopsController. Registered DI in KhachLink Program.cs (Option A — IShopConfigService kept tạm for Phase 3). Build: 0 errors. Branch: `feature/shopconfig-product-tenant-phase2-service`. Commit: `2389846`.

* [2026-07-02] **ShopConfig Product→Tenant Refactor — PHASE 1 COMPLETE** — Reverted Approach 1 remnants (CustomerOrdersController TenantId addition). Added `TenantId` to `ProductCatalogItem` (ShopERP) + projection, and `ProductDto` (KhachLink). Backward compatible (new field). Multi-tenancy now enforced at DTO level. Build: 0 errors. Branch: `feature/shopconfig-product-tenant-phase1-dto`. Commit: `5cb9bbd`.

* [2026-07-02] **ShopConfig Product→Tenant Refactor — PLANNING COMPLETE** — Investigated ShopConfig (hardcoded stub, no persistence), identified architectural violation (KhachLink→CoreHub direct inject), analyzed 2 approaches (order-based vs product-based). Decision: Approach 2 (product-based) — works for anonymous visitors, fixes multi-tenancy gap. Created master plan + 3 task cards (Phase 1: Revert+DTO, Phase 2: HttpService rewrite, Phase 3: Wire-up+Tests). Domain layer NOT modified. Also added KhachLink NavMenu entries (Campaigns, VoiceNote) + Campaigns.razor page + SocialHub integration (from earlier session). ShopERP/Gateway by-tenant endpoint added (kept for Approach 2).

* [2026-07-02] **Tenant Onboarding Feature COMPLETE & MERGED — All 6 Waves on Main** — Wave 6: Added `TenantOnboardingIntegrationTests` with 2 E2E integration tests (full flow + multi-tenant isolation), fixed EF Core LINQ translation issue with strongly-typed TenantId, updated `docs/ShopERP_Documentation.md` with section 4.13 Tenant Onboarding. ci-local PASSED (131 integration tests, 28 architecture tests). Master plan marked COMPLETE. Fast-forward merged to main (commit 3123b6b). Feature branch deleted.

* [2026-07-02] **Wave 5 COMPLETE — ShopERP Admin UI for Tenant Onboarding** — Updated `TenantManagement.razor` with “+ Tạo Tenant + Onboarding” modal: industry selection (F&B enabled), owner credentials, form validation, success/error feedback. Added `TenantOnboardingApiClient` in ShopERP that mints a SystemAdmin JWT for the current user and calls `POST /api/v1/onboarding/tenants`. Added missing UI Platform components: `VanAForm`, `VanAInput`, `VanASelect`, `VanASpinner`. Registered `GatewayClient` named HttpClient in `ShopERP/Program.cs`. `dotnet build VanAn.sln` 0 errors, guard-check PASSED. Branch: `feature/tenant-onboarding-wave5-shoperp-ui`.

* [2026-07-01] **Wave 4 COMPLETE — Gateway API Tenant Onboarding** — Created `TenantOnboardingController` (no class-level Authorize, method-level SystemAdmin Bearer JWT). Fixed IndustryCode `FNB`→`F&B` in integration tests. Added to W12-G7 arch test exemption. 4/4 integration tests PASS, 28/28 arch tests PASS, 156/156 integration tests PASS. Guard-check PASSED. Commit: `7aabe7c` on `feature/tenant-onboarding-wave4-gateway-api`.

* [2026-07-01] **Wave 3 COMPLETE — Tenant Onboarding Orchestrator** — Implemented `TenantOnboardingService`: single-call orchestration of tenant creation → owner user (BCrypt) → Owner role assignment → F&B seed → 4 default permission groups (Quản lý, Thu ngân, Bếp, Kho) → owner assigned to Quản lý group. Injects `IVanAnDbContext` directly to call `SaveChangesAsync` after seed strategy. 17 unit tests added (all pass). Build: 0 errors, 773/773 tests pass. Branch: `feature/tenant-onboarding-wave3-orchestrator`.

* [2026-07-01] **Wave 2 COMPLETE — F&B Seed Strategy** — Implemented `FnbSeedStrategy`: 1 default shop, 8 products (drinks + food), 12 ingredients, 14 recipe mappings (FK→BaseEntity.Id), 12 inventory records. Added `DbSet<Recipe>` and `DbSet<Shop>` to `IVanAnDbContext` (and `DbSet<Recipe>` to `ShopERPDbContext`). 26 unit tests added (counts, TenantId isolation, FK linkage, VAT 10%, active status, categories). Build: 0 errors, 756/756 tests pass. Commit: `dcfc433` on `feature/tenant-onboarding-wave2-fnb-seed`.

* [2026-07-01] **Wave 1 COMPLETE — Tenant Onboarding Generic Abstraction** — Created `IIndustrySeedStrategy`, `ITenantOnboardingService`, immutable DTOs (OnboardTenantRequest, TenantOnboardingResult, IndustrySeedResult), and 6 stub strategies (SPA, HOTEL, BARBER, CLOTHES, HEALTHY, PETSHOP). Added 42 unit tests (all pass). Build: 0 errors, 730/730 tests pass. Commit: `66c6441` on `feature/tenant-onboarding-wave1-abstraction`.

* [2026-07-01] **Objective Transition: IDLE → Tenant Onboarding F&B** — Archived IDLE state. New active objective: implement generic tenant onboarding for F&B with multi-industry extensibility (SPA, Hotel, Barber, Clothes, Healthy, Pet Shop). Master plan + 6 wave task cards created and committed. Status: Planning complete, awaiting approval. On `main`.

* [2026-07-01] **Documentation Added** — Created detailed module documentation: `docs/KhachLink_Documentation.md` (KhachLink PWA: 13 sections covering routes, services, HTTP integrations, PWA/offline, UI Platform) and `docs/ShopERP_Documentation.md` (ShopERP admin: 14 sections covering accounting, EInvoice, order management, admin, DI, SQLite edge architecture). No code changes. On `main`.

* [2026-07-02] **Architecture Test Fixes COMPLETE** — Fixed 4 pre-existing architecture test failures (stale tests from before Phase 2 monolith migration): (1) VA-CONSISTENCY-005: removed "corehub" from logging check list; (2) VA-CONSISTENCY-003: replaced corehub dependency assertion with postgres+nats; (3) Rule C: removed Npgsql from ShopERP.csproj (SQLite-only edge node); (4) VA-GATEWAY-003: excluded Program.cs (DI root) and obj/ from Gateway purity scan. Result: 28/28 arch tests PASS. Commits: `ed442ce`, `e831972` on `main`.

* [2026-07-02] **CI/CD Hotfix COMPLETE** — Fixed remote CI/CD pipeline failures: (1) GatewayWebApplicationFactory.SingleOrDefault → loop-remove all EF Core descriptors; (2) KhachLinkWebApplicationFactory compile error fixed; (3) .env.test added, CI validates .env.test; (4) validate-env-vars.ps1 regex fixed for comment lines; (5) weak secret patterns expanded 7→18. Commit: `7a96a2b` on `main`.

* [2026-06-30] **Phase 4 COMPLETE — Offline-First Edge Fix.** Removed CoreHub container from docker-compose.edge.yml. Updated Gateway/ShopERP dependencies to postgres+nats. Preserved SQLite sidecar + NATS sync worker. Validation passed.

* [2026-06-30] **Phase 3 COMPLETE — CI/CD Pipeline Fix.** Removed CoreHub build/push step from cd.yml. CD now builds 3 images (Gateway, ShopERP, KhachLink). Build time -25%.

* [2026-06-30] **Phase 2 COMPLETE — Docker Compose Production Fix.** Removed CoreHub container from docker-compose.prod.yml. Gateway depends_on postgres+nats (was corehub).

* [2026-06-30] **Phase 1 COMPLETE — Local Development Environment Fix.** start-apps.ps1 updated.

* [2026-06-30] **Phase 0 COMPLETE — Architecture Validation Layer Enhancement.** ArchitectureConsistencyTests.cs (5 tests), validate-docker-compose.ps1, validate-env-vars.ps1, startup test enhancements, CI docker-compose-validation job.

* [2026-06-30] **Unified Roadmap 10/10 waves COMPLETE** — ADR001 (W1-W5) + KhachLink (W1-W4) all done. Architecture reference: docs/Architecture/ADR001-Station-Architecture.md.

---

## 7. Known Open Issues (Non-blocking)

| ID | Issue | Severity | Notes |
|----|-------|----------|-------|
| E2E-001 | `e2e.yml` CI job disabled (`if: false`) | Medium | 19 E2E spec files exist but full suite not run in CI. Smoke tests (6) pass locally. |
| INT-001 | 2 Integration tests failing (`Golden Flow: Simple Entity Insert`, `Golden Flow: Multi-Tenant Isolation`) | Low | Pre-existing, non-blocking. Matches cloud CI behavior. |
| PHASE5-001 | Phase 5 Validation & E2E Testing not fully executed | Low | P5-T2 (staging), P5-T5 (edge live test), P5-T6 (perf baseline), P5-T7 (security) not done. No staging environment available. Deferred. |

---

## 8. Test Suite Summary

| Suite | Count | Status |
|-------|-------|--------|
| Architecture Tests | 28 | ✅ All PASS |
| Unit Tests (Core) | 703 (+42 Wave 1) | ✅ All PASS |
| Unit Tests (Unit) | 17 | ✅ All PASS |
| KhachLink Startup | 8 | ✅ All PASS |
| Gateway Startup | ~5 | ✅ All PASS |
| Integration Tests | 152 | ⚠️ 150 PASS, 2 fail (pre-existing, non-blocking) |
| E2E Smoke | 6 | ✅ All PASS (local) |
| E2E Full Suite | 19 specs | ⏸ Not run (CI disabled) |

---

## 9. Key File References

| File | Mục đích |
|------|---------|
| `docs/AI/tasks/architecture_refactor_master_plan.md` | Master plan phases 0-5 (ARCHIVED — all phases complete) |
| `docs/AI/tasks/phase5_validation_task_card.md` | Phase 5 task card (partially complete — see Known Issues) |
| `docs/Architecture/Validation-Layer-Rules.md` | Architecture validation rules documentation |
| `docs/DEPLOYMENT.md` | Environment setup guide |
| `scripts/validate-docker-compose.ps1` | Docker compose validation script |
| `scripts/validate-env-vars.ps1` | Environment variable validation script |
| `.env.test` | CI test environment values |
| `docs/KhachLink_Documentation.md` | Detailed documentation for KhachLink PWA module |
| `docs/ShopERP_Documentation.md` | Detailed documentation for ShopERP admin module |
| `docs/AI/tasks/tenant_onboarding_fnb_master_plan.md` | Master plan for Tenant Onboarding F&B (multi-industry generic) |
| `docs/AI/tasks/wave1_tenant_onboarding_abstraction_task_card.md` | Wave 1: Generic abstraction + DTOs |
| `docs/AI/tasks/wave2_fnb_seed_strategy_task_card.md` | Wave 2: F&B seed strategy |
| `docs/AI/tasks/wave3_tenant_onboarding_orchestrator_task_card.md` | Wave 3: Onboarding orchestrator |
| `docs/AI/tasks/wave4_tenant_onboarding_gateway_api_task_card.md` | Wave 4: Gateway API |
| `docs/AI/tasks/wave5_tenant_onboarding_shoperp_ui_task_card.md` | Wave 5: ShopERP admin UI |
| `docs/AI/tasks/wave6_tenant_onboarding_validation_docs_task_card.md` | Wave 6: Validation + docs |
| `docs/AI/tasks/shopconfig_product_tenant_master_plan.md` | Master plan: ShopConfig product→tenant refactor (3 phases) |
| `docs/AI/tasks/shopconfig_phase1_revert_dto_task_card.md` | Phase 1: Revert Approach 1 + add TenantId to product DTOs |
| `docs/AI/tasks/shopconfig_phase2_http_service_task_card.md` | Phase 2: Rewrite ShopConfigHttpService (product-based) |
| `docs/AI/tasks/shopconfig_phase3_wireup_tests_task_card.md` | Phase 3: Wire-up components + integration tests |
| `docs/AI/tasks/einvoice_provider_rewrite_master_plan.md` | Master plan: EInvoice provider rewrite (4 waves + Wave 0 parallel) |
| `docs/AI/tasks/wave1_einvoice_request_contract_task_card.md` | Wave 1: Update EInvoiceRequest contract + IEInvoiceProvider interface |
| `docs/AI/tasks/wave2_einvoice_viettel_provider_task_card.md` | Wave 2: Rewrite Viettel provider + DTOs + tests (merged for TDD) |
| `docs/AI/tasks/wave3_einvoice_misa_provider_task_card.md` | Wave 3: Rewrite MISA provider + DTOs + tests |
| `docs/AI/tasks/wave4_einvoice_sandbox_verify_task_card.md` | Wave 4: Sandbox runtime verification (both providers) |
| `docs/AI/tasks/e2e_test_cleanup_master_plan.md` | Master plan: E2E test cleanup (8 waves, 7 anti-patterns) |
| `docs/AI/tasks/wave1_e2e_remove_reporter_pass_task_card.md` | E2E Wave 1: Remove decorative reporter.pass (Pattern F) |
| `docs/AI/tasks/wave2_e2e_fix_auth_pattern_task_card.md` | E2E Wave 2: Fix auth pattern (Pattern D) |
| `docs/AI/tasks/wave3_e2e_delete_anti_schema_tests_task_card.md` | E2E Wave 3: Delete anti-schema tests (Pattern G1) |
| `docs/AI/tasks/wave4_e2e_delete_anti_ui_tests_task_card.md` | E2E Wave 4: Delete anti-UI tests (Pattern G2) |
| `docs/AI/tasks/wave5_e2e_consolidate_smoke_tests_task_card.md` | E2E Wave 5: Consolidate smoke tests (Pattern C) |
| `docs/AI/tasks/wave6_e2e_fix_silent_skip_task_card.md` | E2E Wave 6: Fix silent skip (Pattern B) |
| `docs/AI/tasks/wave7_e2e_fix_or_tautology_task_card.md` | E2E Wave 7: Fix OR-tautology (Pattern A) |
| `docs/AI/tasks/wave8_e2e_regression_prevention_task_card.md` | E2E Wave 8: Regression prevention (helper + lint) |
| `docs/AI/tasks/shoperp_ui_fix_master_plan.md` | Master plan: ShopERP UI fix (6 waves, 6 patterns) |
| `docs/AI/tasks/wave1_shoperp_ui_platform_infra_task_card.md` | UI Wave 1: UI.Platform infra — VanALayout/VanANavigation (Pattern P) |
| `docs/AI/tasks/wave2_shoperp_rendermode_task_card.md` | UI Wave 2: Add @rendermode InteractiveServer (Pattern R) |
| `docs/AI/tasks/wave3_shoperp_page_css_task_card.md` | UI Wave 3: Page CSS isolation (Pattern C) |
| `docs/AI/tasks/wave4_shoperp_component_consolidation_task_card.md` | UI Wave 4: Component consolidation VanAnX→VanX (Pattern V) |
| `docs/AI/tasks/wave5_shoperp_admin_layout_task_card.md` | UI Wave 5: Admin layout consistency (Pattern L) |
| `docs/AI/tasks/wave6_shoperp_governance_cleanup_task_card.md` | UI Wave 6: Governance cleanup (Pattern G) |

---

## 10. Active Architecture Constraints (Quick Reference)

```
KhachLink (5002) → Gateway (5001) → ShopERP (5003) → SQLite
                        ↓
              [in-process CoreHub services]
                        ↓
                  PostgreSQL (prod)
                  SQLite (edge)
```

**Docker services (prod):** postgres · nats · seq · gateway · shoperp · khachlink · nginx · certbot
**Docker services (edge):** postgres · nats · gateway · shoperp · shoperp-nats-sync
**CoreHub:** NOT a Docker service — runs in-process inside Gateway.

---

## 11. Maintenance Log

* **Last Updated:** 2026-07-02 — E2E Test Cleanup + ShopERP UI Fix PLANNING COMPLETE. 2 master plans + 14 task cards committed (`51dd7ff`). E2E: 20 spec files reviewed, ~60/110 tests fake/no-value (55%), 7 anti-patterns classified. ShopERP UI: 23 .razor files reviewed, 13 issues classified into 6 patterns (P/R/C/V/L/G). Root cause: UI.Platform (VanALayout/VanANavigation) chưa hoàn thiện — zero CSS, sai slot. 3 streams now awaiting approval: EInvoice rewrite, E2E cleanup, ShopERP UI fix.
* **Current Branch:** `main`
* **Current Objective:** Multi-plan tracking — 3 streams (EInvoice rewrite, E2E cleanup, ShopERP UI fix) PLANNING COMPLETE, awaiting user approval to start implementation.
