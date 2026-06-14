# Project State

> **Mục đích:** Single Source of Truth cho AI về trạng thái dự án. BẮT BUỘC đọc đầu mỗi phiên.

---

## 0. Maintenance Rules (Quy tắc giữ file đúng mục đích & giá trị cao)

Mọi cập nhật file này PHẢI tuân thủ:

1. **One-and-only-one:** Mỗi section chỉ tồn tại 1 lần. Cấm trùng số thứ tự, cấm trùng nội dung (Next Actions, Health Check...).
2. **No contradiction:** Một hạng mục chỉ có 1 trạng thái duy nhất (không vừa COMPLETED vừa IN PROGRESS).
3. **Ground Truth first:** Trước khi ghi path/branch/trạng thái, PHẢI verify với codebase thực tế. Cấm ghi đường dẫn/branch không tồn tại.
4. **Now over History:** Section 2-4 chỉ mô tả việc ĐANG làm và KẾ TIẾP. Việc đã xong gom gọn vào Section 10 (History Log).
5. **Actionable Next Actions:** Xóa ngay hành động đã quá hạn/sai bối cảnh. Mỗi action phải khả thi tại thời điểm hiện tại.
6. **Concise & DRY:** Mỗi fact ghi đúng 1 nơi; không copy nguyên khối log dài.
7. **Stamp every edit:** Mỗi lần sửa, cập nhật Section 11 (Last Updated + branch hiện tại).
8. **Honest Health Check (Gate 6):** Ghi đúng Assumptions/Open Questions. Assumptions >= Verified Facts HOẶC Open Questions >= 3 ⇒ khuyến nghị INVESTIGATE, cấm sửa code.

---

## 1. Project Overview

* Tên dự án: Vạn An Accounting System MVP.
* Mục tiêu tổng thể: Xây dựng giải pháp kế toán cho hộ kinh doanh / cá nhân kinh doanh tại Việt Nam, có nền tảng kế toán HKD, sổ sách theo Thông tư 152/2025/TT-BTC, workflow đơn hàng, dashboard vận hành và hướng tới production-ready compliance với E-Invoice multi-provider.
* Kiến trúc hiện tại: Clean Architecture + Domain-Driven Design + Multi-tenancy. Roadmap chính mô tả Hybrid Generic Template Architecture với Immutable AccountingEntry, Domain Services, Repository Pattern, Formula Engine DSL, Business Rule Registry, Data Provider Service, UI Platform và SignalR/PWA workflow.
* Công nghệ sử dụng: .NET 8, EF Core, SQLite cho test/integration scenarios, Blazor/ShopERP, KhachLink PWA, SignalR, Docker-related deployment assets, xUnit test projects, Playwright/E2E assets.
* Các module chính:
  * `1_Shared`: domain/value objects/DTOs/shared abstractions.
  * `2_Gateway`: API gateway/controllers/hubs.
  * `3_CoreHub`: core services, repositories, EF infrastructure, order/accounting logic.
  * `5_WebApps/ShopERP`: staff/accounting/admin UI.
  * `5_WebApps/KhachLink`: customer-facing PWA.
  * `UI.Platform`: reusable UI components such as VanAButton, VanACard, VanAAlert, VanAModal, VanALayout.
  * `6_Tests` and `6_Testing`: unit, integration, architecture, E2E and quality-gate assets.

---

## 2. Current Objective

**Architectural Rollback: Restore Distributed Data Flow for KhachLink**

**Status:** ✅ COMPLETED

**Final Architecture Flow:**
```
KhachLink (5002) → Gateway (5001) → ShopERP (5003) → SQLite Database
     ↓                  ↓                  ↓
  HttpClient   ProductsController   ProductsController
                (forward)         (query IVanAnDbContext)
```

**Completed Actions:**
1. ✅ Rolled back QrMenu.razor to use Gateway API (HttpClient) instead of IVanAnDbContext
2. ✅ Removed seed data from KhachLink Program.cs
3. ✅ Removed seed data from CoreHub Program.cs (Class Library)
4. ✅ Created ProductsController in ShopERP with IVanAnDbContext injection
5. ✅ Added seed data (5 products) to ShopERP Program.cs with TenantId: 00000000-0000-0000-0000-000000000001
6. ✅ Created Gateway ProductsController to forward requests to ShopERP via HttpClient
7. ✅ Fixed ShopERP DI issues (IAuditTrailService, IAuditLogRepository, ITenantProvider)
8. ✅ All services running: ShopERP (5003), Gateway (5001), KhachLink (5002)
9. ✅ API verification: curl http://localhost:5001/api/products?tenantId=... returns 200 OK with 5 products
10. ✅ Architecture tests: 7/7 PASS
11. ✅ Playwright E2E tests: 15 passed, 2 skipped

**Key Files Modified:**
- `5_WebApps/ShopERP/Controllers/ProductsController.cs` - API endpoint with IVanAnDbContext
- `5_WebApps/ShopERP/Program.cs` - Seed data + DI registrations
- `5_WebApps/ShopERP/Services/TenantProvider.cs` - Local implementation
- `2_Gateway/Controllers/ProductsController.cs` - HttpClient forward to ShopERP
- `5_WebApps/KhachLink/Pages/QrMenu.razor` - HttpClient API calls

## 3. Current Status

### Completed

- Sprint 1 (Phase 2.6 Frontend Accounting Module) ✅
- Sprint 2 (Period Closing Wizard + Audit Trail) — MERGED vào `main` ✅
- CI pipeline stabilized + Flaky Test Fix Plan (31+ tests) ✅
- Guard Check Upgrade Phase 2 (Roslyn Analyzers VA1001-VA1005 + Integration Tests) ✅
- GitHub Actions free-tier optimization (PR #14, merged) ✅
- Sprint 3 E-Invoice Review Fix (F0–F4) ✅
- **UC1 QR Checkout Completion — S1–S4 DONE, 22/22 tests PASS ✅** (2026-06-10)

### Blocked

- Không có blockers.

---

## 4. Next Actions

### Current: Architectural Rollback - ✅ COMPLETED

**Completed (2026-06-12):**
- ✅ QrMenu.razor rolled back to use Gateway API (HttpClient)
- ✅ Seed data removed from KhachLink and CoreHub Program.cs
- ✅ ProductsController created in ShopERP with IVanAnDbContext
- ✅ Seed data (5 products) added to ShopERP Program.cs
- ✅ Gateway ProductsController created to forward to ShopERP
- ✅ ShopERP DI issues fixed (IAuditTrailService, IAuditLogRepository, ITenantProvider)
- ✅ All services running and verified
- ✅ API endpoint verified (200 OK with 5 products)
- ✅ Architecture tests: 7/7 PASS
- ✅ Playwright E2E tests: 15 passed, 2 skipped

### Next Phase

**To be planned:** Phase 2 — KhachLink Màn 1→4 Integration & Hardening

---

## 5. Architecture Decisions

* Decision: Use immutable `AccountingEntry` with append-only behavior.
* Reason: Accounting data must be auditable and safe for financial workflows.
* Consequences: No update/delete flow for accounting entries; corrections and reopening flows must use reversal entries.

* Decision: Use Reversal Entry pattern for period reopening or accounting correction.
* Reason: Preserves historical auditability and aligns with immutable accounting model.
* Consequences: Period closing and reopening services must generate reversing entries instead of mutating existing entries.

* Decision: Enforce multi-tenancy with `TenantId` filtering.
* Reason: Accounting data must be isolated per tenant/HKD/shop.
* Consequences: Every query/repository/service must preserve tenant isolation; tests must include tenant-isolation scenarios.

* Decision: Use `OrderConfiguration` as the single source of truth for Customer-Order EF relationship.
* Reason: The fix plan states `OrderConfiguration` defines the complete relationship and aligns better with aggregate relationship ownership.
* Consequences: Duplicate relationship configuration in `CustomerConfiguration` should remain removed; regression tests involving `Customer.Orders` must validate navigation behavior.

* Decision: Use file-based SQLite for the isolated SQLite tests recorded in the fix plan.
* Reason: The fix plan states in-memory SQLite caused reliability issues in the test constructor pattern.
* Consequences: Test cleanup must remove temporary database files; WAL-mode expectations differ from in-memory SQLite.

* Decision: Use UI Platform components for ShopERP UI work.
* Reason: Roadmap non-negotiable constraint requires VanAButton, VanACard, VanAAlert, VanAModal, VanALayout and avoids custom HTML/CSS.
* Consequences: Phase 2.6 and later UI tasks should reuse existing UI.Platform components.

* Decision: S4.0 Architecture Spike — `TenantAnnualRevenue` calculated from runtime aggregate over `AccountingEntries`.
* Reason: Avoid DB schema changes during MVP stabilization; E2E coverage priority over persistence layer changes.
* Consequences: Slightly slower calculation but accurate; deferred to `Tenant` entity only in post-MVP phase.

* Decision: S4.0 — Keep MVP transaction boundary pattern (2 API calls) instead of merging into single endpoint.
* Reason: E2E test coverage is higher priority than transaction boundary refactor; architecture debt acceptable for MVP scope.
* Consequences: `OrderCreated` domain event logged for post-MVP refactor; partial failure risk mitigated by E2E validation.

* Decision: S4 Small — Calculate `TenantAnnualRevenue` runtime via `GetRevenueByDateRangeAsync()` for Calendar Year.
* Reason: Matches TT 152/2025 "1 tỷ/năm" rule; no schema change; single-file scope keeps risk minimal.
* Consequences: Revenue calculation adds 1 DB query per checkout; acceptable for MVP volume. Method extraction deferred until 3+ consumers.

* Decision: Treat Phase 5 E-Invoice Multi-Provider Integration as production compliance blocker.
* Reason: Roadmap states core MVP is functional but production-ready HKD compliance requires E-Invoice integration.
* Consequences: Product cannot be claimed production-ready for HKD compliance until E-Invoice implementation and verification are complete.

---

## 6. Coding Plan

Phase 1 — Unblock CI (COMPLETED)

* Task: ✅ DONE — Added test projects to `VanAn.sln`. CI `build-verify` PASSED.

Phase 2 — Complete Sprint 2 (COMPLETED)

* Task: ✅ DONE — Sprint 2 (Period Closing Wizard + Audit Trail) merged into `main`.

Phase 3 — Sprint 3 Recovery (IN PROGRESS)

* Session 8 — Integration Test Gate Fix ✅ DONE
  * Fixed `EInvoiceDISmokeTests.cs`: Added missing `ITenantProvider` and `IVanAnDbContext` DI registrations
  * Test results: 21/21 Integration Gate tests pass (CircuitBreaker + EInvoiceDI + EInvoiceProvider)
  * Root cause: `AuditTrailService` requires `ITenantProvider`, `AccountingEntryRepository` requires `IVanAnDbContext`
  * Solution: Added `TestTenantProvider` registration and `IVanAnDbContext` forwarding to `VanAnDbContext`

* Session 8b — CS0311 Build Fix ✅ DONE
  * Error: `EInvoiceWorker` cannot be used as type parameter `THostedService` in `AddHostedService<EInvoiceWorker>()`
  * Root cause: `VanAn.Integration.Tests.csproj` missing `Microsoft.Extensions.Hosting` package
  * Fix: Added `<PackageReference Include="Microsoft.Extensions.Hosting" />` to csproj


---

## 7. Known Risks

* Risk: Adding projects to `VanAn.sln` may introduce new build errors if those projects have unresolved dependencies.
* Impact: `dotnet build VanAn.sln` could fail at build step instead of test step.
* Mitigation: Verify `dotnet build VanAn.sln --configuration Release` passes locally after adding projects.

* Risk: CI runs on `ubuntu-latest` / Release configuration; local Windows / Debug may mask issues.
* Impact: A fix that passes locally may still fail on CI.
* Mitigation: Reproduce with `dotnet build --configuration Release` locally before pushing.

* Risk: Production HKD compliance depends on Phase 5 E-Invoice, which is still planned.
* Impact: Product is not production-compliance complete until E-Invoice is implemented.
* Mitigation: Keep Phase 5 visible and prioritize after Sprint 2 unblocks.

* Risk: Multi-tenant filtering mistakes can leak accounting data across tenants.
* Impact: Severe financial/data isolation issue.
* Mitigation: Require tenant-isolation tests for all repositories, services and UI-backed APIs.

* Risk: Immutable accounting constraints may be bypassed by convenience APIs or EF updates.
* Impact: Audit trail corruption and financial inconsistency.
* Mitigation: Preserve append-only design, use reversal entries for corrections.

* Risk: `LeadToCustomerConversionTests` regression check is incomplete due to a separate infrastructure issue.
* Impact: Customer-Order navigation regression partially unverified.
* Mitigation: Fix `IntegrationTestBase` relational provider configuration or add targeted SQLite regression test.

* Risk: `GetPostgreSQLMetricsAsync_Should_Perform_With_Large_Dataset` in `Core.Tests` is a flaky performance test.
* Impact: Asserts wall-clock `< 5000ms`; fails under high machine load (observed 7s during full-suite run, passes < 1ms in isolation). May cause intermittent CI failure.
* Mitigation: Separate perf tests via `[Trait("Category","Performance")]` and exclude from unit test CI step, or replace wall-clock assert with a stable metric. Technical debt — schedule as separate task after PR #6 merges.

---

## 8. Important Files

* File Path: `docs/AI/project_state.md`
* Purpose: Current AI project state source of truth.

* File Path: `.github/workflows/pr-check.yml`
* Purpose: GitHub CI pipeline for PRs; `build-verify` job is the current blocker.

* File Path: `docs/plan_MVP/RoadMap/MVP plan Account M.md`
* Purpose: Authoritative MVP roadmap with 5-sprint production-ready execution plan.

* File Path: `docs/plan_MVP/DETAIL_PLAN.md`
* Purpose: Detailed architecture and phase breakdown.

* File Path: `docs/SQLite_Configuration_Fix_Plan.md`
* Purpose: Records SQLite integration test root cause, fixes and remaining regression blocker.

* File Path: `docs/plan_MVP/HKD_BookAcc/`
* Purpose: 7 HKD book reference documents for S1a, S2a, S2b, S2c, S2d, S2e, S3a.

* File Path: `3_CoreHub/Infrastructure/Configurations/CustomerConfiguration.cs`
* Purpose: Customer EF Core config; duplicate Orders relationship removed per fix plan.

* File Path: `3_CoreHub/Infrastructure/Configurations/OrderConfiguration.cs`
* Purpose: Single source of truth for Customer-Order EF relationship.

* File Path: `6_Tests/VanAn.Integration.Tests/IsolatedSQLiteTests.cs`
* Purpose: SQLite isolation and multi-tenant tests; all 6 passing per fix plan.

* File Path: `6_Tests/VanAn.Integration.Tests/LeadToCustomerConversionTests.cs`
* Purpose: Regression area for `Customer.Orders` navigation; currently blocked by infrastructure issue.

* File Path: `5_WebApps/ShopERP/Components/Pages/Accounting/`
* Purpose: Sprint 1 output: accounting UI pages (completed).

* File Path: `1_Shared/Domain.cs`
* Purpose: Shared domain; E-Invoice + UC1 domain models (`PendingInvoiceQueue`, `RecipientType`, `PendingInvoiceStatus`) added.

* File Path: `3_CoreHub/Services/CheckoutCompletionService.cs`
* Purpose: UC1 orchestrator — phân loại recipient, ghi doanh thu, xếp hàng batch invoice.

* File Path: `3_CoreHub/Services/GuestMergeService.cs`
* Purpose: UC1 — merge guest DeviceId với Customer SĐT cho tích điểm Drips.

* File Path: `3_CoreHub/Services/MstLookupService.cs`
* Purpose: UC1 — tra cứu thông tin doanh nghiệp qua MST (api.vietqr.io).

* File Path: `3_CoreHub/Infrastructure/Messaging/BatchInvoiceProcessor.cs`
* Purpose: UC1 — background service xử lý `PendingInvoiceQueue` batch 5 phút/lần.

* File Path: `2_Gateway/Controllers/CheckoutController.cs`
* Purpose: UC1 — `POST /api/checkout/complete` endpoint.

* File Path: `docs/design/wireframe_qr_ecosystem.md`
* Purpose: Wireframe UC1+2+3+4 — design tokens, màn hình 1–4, decision tree.

* File Path: `docs/design/uc1_qr_checkout_completion_detail_plan.md`
* Purpose: Detailed plan UC1 — TDD plan T01–T22, coding sessions S1–S4, DoD checklist.

* File Path: `docs/AI/tasks/khachlink_frontend_completion_plan.md`
* Purpose: KhachLink Frontend Phase 1 completion plan — S1–S7, outcome-driven, tracking table.

* File Path: `docs/AI/tasks/khachlink_s2_order_creation_plan.md`
* Purpose: S2 detail coding plan — fix 3 silent fake bugs trong `PaymentSuccess.razor`.

* File Path: `5_WebApps/KhachLink/Pages/PaymentSuccess.razor`
* Purpose: Màn 4 QR checkout — đang sửa B1/B2/B3 fake OrderId fallback (S2).

* File Path: `5_WebApps/KhachLink/appsettings.json`
* Purpose: Config KhachLink — đã thêm `Gateway.BaseUrl` (S1 done).

* File Path: `3_CoreHub/Services/Orchestration/`, `3_CoreHub/Services/Resilience/`, `3_CoreHub/Services/Providers/EInvoice/`
* Purpose: Sprint 3 E-Invoice implementation — orchestration, circuit breaker/resilience, provider factory & registry.

* File Path: `3_CoreHub/Program.cs`
* Purpose: CoreHub DI composition root; E-Invoice service registrations cần được thêm tại đây.

---

## 9. AI Health Check (Gate 6)

* Understanding Level: 85%
* Root Cause Confidence: 70%
* Verified Facts:
  * Gateway API (port 5001) returns ProductDto correctly via curl ✅
  * KhachLink (port 5002) builds with 0 errors ✅
  * ProductDto has JsonPropertyName attributes for camelCase ✅
  * CartService.AddItemAsync(ProductDto) overload exists ✅
  * QrMenu.razor uses IHttpClientFactory with named client "gateway" ✅
  * E2E tests: 9/9 failing - `[data-testid="add-item"]` not visible ❌
* Assumptions:
  * Blazor Server component may need explicit re-render trigger after async data load
  * HttpClient base URL configuration may not be resolving correctly
* Open Questions:
  * Why does ProductDto data load successfully but not render in UI?
  * Is QrMenu.razor a Blazor component or Razor Page (affects lifecycle)?
  * Does HttpClient named client "gateway" have correct BaseUrl in appsettings.json?
* Context Quality: Medium
* ACS Status: ⚠️ **INVESTIGATE** — E2E regression blocking validation
* Recommended Action: **Debug Blazor component rendering lifecycle**

### S4.0 Architecture Spike — Decisions Locked (2026-06-11)

| Question | Decision | Rationale |
|----------|----------|-----------|
| **Q4: TenantAnnualRevenue source** | **Option B** — Runtime aggregate from `AccountingEntries` | No DB schema change at this phase |
| **Transaction boundary** | Keep MVP pattern: `POST /api/orders` → optional `POST /api/checkout/complete` | E2E coverage priority over architecture refactor |
| **Tech debt** | `OrderCreated` domain event for post-MVP refactor | Deferred to after E2E stabilization |

**Execution order:** S3 (DONE) → S5 (DONE) → S6 (DONE) → Stabilization → S4 (small) → Refactor post-MVP

### S4 Small — ✅ COMPLETED (2026-06-11)

**Decision:** Option A — Proceed (Calendar Year Revenue)

| Item | Status | Details |
|------|--------|---------|
| **Revenue Period** | ✅ Calendar Year (01/01 → 31/12) | Matches `AnnualRevenue` naming + TT 152/2025 rule (1B/year) |
| **Implementation** | ✅ Runtime via `GetRevenueByDateRangeAsync()` | Used existing API, no new method needed |
| **Data Source** | ✅ `AccountingEntries` aggregated | No Tenant.AnnualRevenue persistence |
| **Schema Change** | ✅ **NONE** | No migration, no `FiscalYearStartMonth`, no admin UI |
| **Request DTO** | ✅ Removed `TenantAnnualRevenue` | Revenue calculated backend; BusinessType preserved |
| **Files Changed** | ✅ 3 files | `CheckoutCompletionService.cs`, `CheckoutController.cs`, `CheckoutCompletionServiceTests.cs` |
| **Tests** | ✅ 5/5 PASS | All CheckoutCompletionServiceTests green |
| **Build** | ✅ 0 errors, 0 warnings | Clean build |

**Implementation Details:**
```csharp
// Calendar Year revenue calculation (S4)
var currentYear = DateTime.UtcNow.Year;
var yearStart = new DateTime(currentYear, 1, 1);
var yearEnd = new DateTime(currentYear, 12, 31);
var annualRevenue = await accountingService.GetRevenueByDateRangeAsync(
    request.TenantId, yearStart, yearEnd);
```

### S7 Responsive — ✅ COMPLETED (2026-06-11)

| Item | Status | Details |
|------|--------|---------|
| **TC7** | ✅ Android 360×800 | No overflow, sticky CTA clickable |
| **TC8** | ✅ iPhone 14 390×844 | No overflow, CTA visible |
| **Total Tests** | ✅ 8/8 listed | TC0–TC8 all present |
| **Test Configs** | ✅ 54 total | Multiple browser/device combinations |
| **File Changed** | ✅ 1 file | `qr-checkout-flow.spec.ts` |

**Test Pattern:**
```typescript
// TC7: Android 360×800
await page.setViewportSize({ width: 360, height: 800 });
const bodyWidth = await page.evaluate(() => document.body.scrollWidth);
expect(bodyWidth).toBeLessThanOrEqual(361); // 360 + 1px tolerance
```

---

## 10. History Log (Completed Initiatives)

* **KhachLink E2E Regression Fix** (2026-06-11) — In Progress. Gateway DI fixed, ProductDto created with JsonPropertyName, CartService overload added, QrMenu updated to use IHttpClientFactory. Gateway (5001) and KhachLink (5002) running. E2E tests: 9/9 failing - products not rendering in UI despite API returning data correctly.
* **S7 Responsive** (2026-06-11) — Playwright responsive tests. TC7: Android 360×800 (no overflow, CTA clickable). TC8: iPhone 14 390×844 (no overflow). 8/8 E2E tests listed, 54 total test configurations.
* **S4 Small** (2026-06-11) — Checkout Completion runtime revenue calculation. Calendar Year (01/01→31/12) revenue aggregated via `GetRevenueByDateRangeAsync()`. Removed `TenantAnnualRevenue` from DTOs. 3 files changed, 0 schema migration, 5/5 tests PASS.
* **KhachLink Frontend S6** (2026-06-11) — E2E Playwright. 7 test cases (TC0–TC6): cart guard redirect, add items, B2B/Retail/Anonymous navigation, order success mock, order failure mock. `data-testid` attributes added. Zero `waitForTimeout`. All tests independent. `TEST_TENANT_ID` from env.
* **KhachLink Frontend S5** (2026-06-11) — CartPage + QrMenu Fixes. CartPage auto-redirect khi cart trống. QrMenu: tenantId from query param, error khi missing/invalid, static list labeled `[DEV ONLY]`. Build 0 errors.
* **KhachLink Frontend S3** (2026-06-11) — Business Lookup Proxy. `BusinessLookupController` tạo mới, `MstLookupError` enum (NotFound/Timeout/ServiceError), Gateway DI wired, `InvoiceBusiness.razor` chuyển sang proxy. 6/6 unit tests PASS. Build 0 errors.
* **KhachLink Frontend S2** (2026-06-11) — Order Creation Fix. `PaymentSuccess.razor`: B1/B2/B3 fixed, `ILogger` injected, `RetrySubmit()` với guard + full reset, `ClearCartAsync` chỉ còn 1 nơi trong success path. Build 0 errors.
* **KhachLink Frontend S1** (2026-06-11) — Gateway Connectivity. `appsettings.json` + `Program.cs` named client `"gateway"` fail-fast + `PaymentSuccess.razor` + `InvoiceBusiness.razor` refactor sang `IHttpClientFactory`. Build 0 errors.
* **UC1 QR Checkout Completion** (2026-06-10) — S1–S4, 22/22 tests PASS. Services: `CheckoutCompletionService`, `GuestMergeService`, `MstLookupService`, `BatchInvoiceProcessor`, `CheckoutController`.
* **Sprint 3 E-Invoice F0–F4** (2026-06-09) — Anti-stub gate, RetryPolicyService fix, Outbox atomic, 5 stubs replaced.
* **GitHub Actions Free-Tier Optimization** — PR #14 merged (~770 phút/tháng tiết kiệm).
* **Flaky Test Fix Plan** — 31+ tests ổn định, `AsyncAssert.cs` polling, `[Trait]` categories.
* **CD Pipeline Verification (PAUSED)** — CoreHub SIGSEGV + MissingMethodException fixed. Paused vì resource.
* **Guard Check Upgrade Phase 2** — 5 Roslyn Analyzers (VA1001-VA1005), `guard-check.ps1` fast gate.
* **Sprint 2** — Period Closing Wizard + Audit Trail, merged vào `main`.
* **Sprint 1** — Frontend Accounting Module (ShopERP).

---

## 11. Maintenance Log

* Last Updated: 2026-06-12 14:40 UTC+7
* Current Branch: `main`
* **Architectural Rollback COMPLETED (2026-06-12):** QrMenu.razor rolled back to use Gateway API (HttpClient). Seed data removed from KhachLink and CoreHub Program.cs. ProductsController created in ShopERP with IVanAnDbContext injection. Seed data (5 products) added to ShopERP Program.cs with TenantId: 00000000-0000-0000-0000-000000000001. Gateway ProductsController created to forward requests to ShopERP via HttpClient. ShopERP DI issues fixed (IAuditTrailService, IAuditLogRepository, ITenantProvider). All services running: ShopERP (5003), Gateway (5001), KhachLink (5002). API verified: curl returns 200 OK with 5 products. Architecture tests: 7/7 PASS. Playwright E2E tests: 15 passed, 2 skipped.
* **SqliteException Fix COMPLETED (2026-06-12):** CustomerConfiguration.cs updated with BaseEntity audit properties (CreatedAt, UpdatedAt, CreatedBy, UpdatedBy, IsDeleted). VanAnDbContext.cs Product entity configuration updated with same properties. Database recreated via EnsureCreatedAsync().
* **E2E env-config Fix COMPLETED (2026-06-12):** Fixed path resolution using findProjectRoot(), added TEST_ENV_FILE override, installed @types/node, restored isTierEnabled() checks in all test files. Config loads correctly from root and 6_Testing directories.
* **KhachLink Backend 500 Fix COMPLETED (2026-06-12):** Fixed DynamicThemeProvider.razor HttpClientFactory error (removed HttpClientFactory.CreateClient, used injected Http). KhachLink service now returns HTTP 200.
* **Phase 1 COMPLETE (2026-06-11):** All 8 sessions (S1–S7 + S4.0) DONE ✅
* **S7 COMPLETED:** 2 responsive tests (TC7, TC8) added — 8/8 E2E tests listed
* **S4 Small COMPLETED:** ✅ 5/5 tests PASS — runtime revenue via `GetRevenueByDateRangeAsync`
* KhachLink Frontend S6 DONE (2026-06-11): 7 E2E TCs, data-testid, mocked API, independent tests. Build 0 errors, playwright --list 42 (7×6 projects).
* Priority reorder (2026-06-11): S6 E2E → S4.0 Arch Spike → S4 → S7. Rationale: no E2E coverage → refactoring checkout is risky.
* KhachLink Frontend S5 DONE (2026-06-11): CartPage redirect + QrMenu tenantId guard + [DEV ONLY]. Build 0 errors.
* KhachLink Frontend S3 DONE (2026-06-11): BusinessLookupController + MstLookupError enum + DI + razor fix. 6/6 tests PASS, build 0 errors.
* Phase 5 Doc Automation validated (2026-06-11): `CHANGELOG.md` S2 entry + `blazor_interactivity_debug.md` Pattern 5 appended.
* KhachLink Frontend S2 DONE (2026-06-11): B1/B2/B3 fixed, logger, RetrySubmit guard+reset, build 0 errors.
* UC1 QR Checkout Completion — S1–S4 COMPLETE (2026-06-10)
  * S1: Domain enums, `PendingInvoiceQueue`, `RecipientType`, `InvoicePolicyService` fix. T01–T09 PASS.
  * S2: `GuestMergeService`, `MstLookupService`. T10–T13 PASS.
  * S3: `CheckoutCompletionService`, `PendingInvoiceQueues` DbSet wiring. T14–T18 PASS.
  * S4: `BatchInvoiceProcessor` (testable overload), `CheckoutController`, DI in `Program.cs`. T19–T22 PASS.
  * Root fix for T19–T22: dùng SQLite in-memory `VanAnDbContextTestFactory` + direct overload thay mock DbSet.
* Removed: UC1 DoD checklist và Sprint 3 F5/F6 pending items (chuyển sang tracking riêng nếu cần).
* Phase 5: Documentation Automation — COMPLETED
  * Created `DOC_AUTOMATION.md` với auto-update rules
  * Created `CHANGELOG.md` với lịch sử đầy đủ
  * Created 3 templates: API, Changelog, Skill
  * Updated `AGENTS.md` với Documentation Agent (v1.1)
* Phase 6: Project Memory — COMPLETED
  * Created SQLite schema (`ProjectMemorySchema.sql`)
  * Created 5 entities: AiTask, AiFeature, AiDecision, AiAgentHistory, AiSession
  * Created `ProjectMemoryDbContext` + `IProjectMemoryService` + `ProjectMemoryService`
  * Created `PROJECT_MEMORY.md` documentation
  * Updated `AGENTS.md` với Project Memory Agent (v1.2)
  * Query patterns: GetWhatWeDidLastMonth, GenerateSprintRetrospective, FindSimilarPatterns
* History (Sprint 3 F0–F4): xem Section 10.
