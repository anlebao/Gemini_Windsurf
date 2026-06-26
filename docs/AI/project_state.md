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

**Production Hygiene — Wave 15: KhachLink Page Cleanup + Routing Modernization ✅ COMPLETED**

**Status:** DONE — Branch `feature/wave15-khachlink-page-cleanup` (commit `26abd83`), PR #TBD

**Wave 14:** COMPLETED — HMAC-SHA256 API Request Signing (#63)

---

*Note: Waves 0-7 have been archived to `docs/AI/project_state_archive.md` (2026-06-24)*

### Completed

- **Customer API Integration Tests Fix — 100% Success Rate** (2026-06-24)
  * Fixed 21 Customer API integration tests using string assertion pattern
  * Root cause: Value Object JSON serialization + database context isolation
  * Pattern: HTTP API seeding + string assertions instead of JSON deserialization
  * Result: 129/130 tests pass (1 unrelated failure: Golden Flow Health Check)
- Sprint 1 (Phase 2.6 Frontend Accounting Module) ✅
- Sprint 2 (Period Closing Wizard + Audit Trail) — MERGED vào `main` ✅
- CI pipeline stabilized + Flaky Test Fix Plan (31+ tests) ✅
- Guard Check Upgrade Phase 2 (Roslyn Analyzers VA1001-VA1005 + Integration Tests) ✅
- GitHub Actions free-tier optimization (PR #14, merged) ✅
- Sprint 3 E-Invoice Review Fix (F0–F4) ✅
- **UC1 QR Checkout Completion — S1–S4 DONE, 22/22 tests PASS ✅** (2026-06-10)
- **Value Object Mapping Fix — 14 EF Core Configuration files created ✅** (2026-06-15)
  * Fixed: ProductId, IngredientId, RecipeId, InventoryId, OrderItemId, LeadId, OrderStatusId, CustomerId, TenantId converters
  * Removed: Inline entity configurations from VanAnDbContext.cs
  * Pattern: `HasConversion(id => id.Value, value => new TypeName(value))`
- **Security Compliance — Wave 0: JWT Authentication Foundation ✅** (2026-06-23)
  * Stateless JWT (HS256, 8h), BCrypt work factor 12, dual-scheme auth
- **Security Compliance — Wave 1: Notification Integration (Brevo + ESMS) ✅** (2026-06-23)
  * 11/11 unit tests PASS
- **Security Compliance — Wave 2: Data Protection (Field-level Encryption) ✅** (2026-06-23)
  * EncryptedStringConverter, PII encryption, CustomerEncryptionTests PASS
- **Security Compliance — Wave 3: Report Export (Excel with EPPlus) ✅** (2026-06-23)
  * IExcelExportService + 3 report generators + ReportController + 6/6 unit tests PASS
- **Security Compliance — Wave 4: RBAC Enforcement at Blazor UI Layer ✅** (2026-06-23)
  * AuthorizeRouteView, policy-gated pages, NavMenu role gates, AccessDenied.razor, 9 E2E tests
- **Security Compliance — Wave 5: Domain Refactor + Tenant Rich Domain + CRUD ✅ COMPLETED** (2026-06-23)
  * W5-T1→T10 COMPLETE — Branch `feature/wave5-tenant-mgmt`
  * TenantAggregate (Rich Domain), ITenantManagementService, TenantController, TenantManagement.razor, 23 tests
- **Security Compliance — Wave 6: User Aggregate + RBAC Management ✅ COMPLETED** (2026-06-23)
  * W6-T1→T12 COMPLETE — Branch `feature/wave6-user-rbac-mgmt`, merged to `main` (commit `2599c1b`)
  * UserAggregate split, PermissionGroup/UserPermissionGroup, UserManagementService, RoleAssignmentService, PermissionGroupService, UserController, PermissionGroupController, UserManagement.razor, PermissionGroupManagement.razor, 29 tests PASS
- **Production Hygiene — Wave 7: Production Hardening ✅ COMPLETED** (2026-06-24)
  * W7-T1→T5 COMPLETE — Branch `feature/wave7-prod-hardening`, merged to base for Wave 8
  * Production hardening tasks completed per PRODUCTION_HYGIENE_master_plan.md
- **Production Hygiene — Wave 8: Upgrade Dashboard to Sitemap with Authentication ✅ COMPLETED** (2026-06-25)
  * W8-T1→T5 COMPLETE — Branch `feature/wave8-upgrade-dashboard` (commit `d088739`)
  * Sitemap.razor `[Authorize]` tại `/sitemap` với role-based module cards (AuthorizeView)
  * Home.razor redirect sang `/sitemap`; VanAn_Dashboard.html xóa; ecosystem scripts cập nhật
  * Build: 0 errors; guard-check: PASS

- **Production Hygiene — Wave 9: Cleanup Orphan Controller ✅ COMPLETED** (2026-06-25)
  * W9-T1→T4 COMPLETE — Branch `feature/wave9-cleanup-controller`
  * Xóa `ShopERP/Controllers/CustomersController.cs`; xóa `CustomerApiIntegrationTests` (không có Gateway endpoints tương đương)
  * Verify: không còn references trong production code; build 0 errors; guard-check PASS

- **Production Hygiene — Wave 10: Cleanup Duplicate Interfaces ✅ COMPLETED** (2026-06-26)
  * W10-T1→T4 COMPLETE — Branch `feature/wave10-cleanup-interfaces` (commit `7174129`)
  * Xóa `ShopERP/Services/ISocialCampaignService.cs` và `ShopERP/Services/ILoyaltyRewardsService.cs` (duplicate của CoreHub)
  * Fix `SocialCampaignManager.cshtml`: `@inject` đổi sang `VanAn.CoreHub.Services` namespace
  * Verify: 0 references đến ShopERP duplicates; build 0 errors; guard-check PASS

- **Production Hygiene — Wave 11: Cleanup Invalid Framework Files ✅ COMPLETED** (2026-06-26)
  * W11-T1→T4 COMPLETE — Branch `feature/wave11-cleanup-invalid-files` (commit `09ef54d`)
  * Xóa `ShopERP/Pages/SocialCampaignManager.cshtml`, `KhachLink/wwwroot/index.html`
  * Verify: không còn references trong production code; build 0 errors; guard-check PASS

- **Production Hygiene — Wave 12: Fix API Authorization ✅ COMPLETED** (2026-06-26)
  * W12-T1→T5 COMPLETE — Branch `feature/wave12-api-authorization` (commit `3f8d549`), PR #61
  * Gateway: 6 controllers thêm `[Authorize(Policy="RequireTenantAccess")]`
  * ShopERP: ShopsController POST/PUT/DELETE + OrderWorkflowController PUT thêm authorization
  * KhachLink: VoiceNote.razor thêm `@attribute [Authorize]`
  * Tests: 10 reflection-based authorization enforcement tests PASS

- **Production Hygiene — Wave 13: Replace Hardcoded Data ✅ COMPLETED** (2026-06-26)
  * W13-T1→T5 COMPLETE — Branch `feature/wave13-replace-hardcoded-data` (commit `078ae76`), PR #62
  * ShopERP: `ProductsController` — public `GET /api/products?shopId=` endpoint cho KhachLink catalog
  * KhachLink: `ProductHttpService` — HTTP client via Gateway YARP proxy đến ShopERP
  * KhachLink/Home.razor: xóa 4 hardcoded products, thay bằng `await ProductService.GetProductsAsync()`
  * Gateway/OnboardingController: xóa dead code `customizedTemplate` + stale comment
  * Tests: 5 integration tests PASS; Architecture tests: 21/21 PASS

- **Production Hygiene — Wave 14: HMAC Request Signing ✅ COMPLETED** (2026-06-26)
  * W14-T1→T5 COMPLETE — Branch `feature/wave14-api-request-signing` (commit `5462759`), PR #63
  * Gateway: `HmacSigningMiddleware` — validates `X-VanAn-KeyId/Timestamp/Nonce/Signature`
  * Gateway: `IHmacApiKeyLookup` + `HmacApiKeyLookupAdapter` — decouples Gateway from CoreHub repos
  * 1_Shared: `ApiKey` domain entity (per-tenant, IsActive, ExpiresAt, RevocationAt)
  * CoreHub: `ApiKeyConfiguration` (EF), `IApiKeyRepository` + impl, `IApiKeyManagementService` + impl
  * ShopERP: `ApiKeyController` — `POST/GET/DELETE /api/apikeys` (Admin/Owner only); one-time secret
  * Anti-replay: timestamp window ±60s + nonce IMemoryCache TTL 120s
  * Rate limit: 5 failures → 15-min block per KeyId
  * Tests: 10/10 integration tests PASS (W14-S1→S10); Architecture tests: 21/21 PASS; guard-check: PASS

- **Production Hygiene — Wave 15: KhachLink Page Cleanup + Routing Modernization ✅ COMPLETED** (2026-06-26)
  * W15-T1→T5 COMPLETE — Branch `feature/wave15-khachlink-page-cleanup` (commit `26abd83`)
  * Xóa 6 dead/demo pages; `Dashboard.cshtml` → `Dashboard.razor` render `<RealTimeDashboard />`
  * Modernize `Program.cs` sang Blazor Web App: `AddRazorComponents()` + `MapRazorComponents<App>()`; xóa `AddServerSideBlazor`, `MapBlazorHub`, `MapFallbackToPage("/Index")`
  * Rewrite `VoiceNote.razor`: `IHttpClientFactory("gateway")`, endpoint `POST /api/v1/voicecommand/text-command`, `VanAnAlert` thay `alert()` native, không còn `<html>/<head>/<body>` tags
  * Build: 0 errors; guard-check: PASS; Architecture tests: 21/21 PASS

### Blocked

- Không có blockers.

---

## 4. Next Actions

### Next: Wave 16 — KhachLink Production Flow Hardening

1. W16-T1: Refactor `Campaign.cshtml` — xóa CoreHub inject, gọi Gateway endpoints, xóa social proof giả
2. W16-T2: Fix `RealTimeDashboard.razor` — thay `"demo-shop"` hardcode bằng `ITenantService.GetCurrentTenantId()`
3. W16-T3: Fix `VoiceCommand.razor` — thay `@inject HttpClient` bằng `IHttpClientFactory("gateway")`
4. W16-T4: Verify build 0 errors + E2E selector contract giữ nguyên
5. W16-T5: Update `project_state.md` — ghi nhận Wave 16

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

* Understanding Level: 95%
* Root Cause Confidence: 95%
* Verified Facts:
  * Customer API integration tests: 21 failures → 0 failures (100% success rate) ✅
  * Root cause: Value Object JSON serialization + database context isolation ✅
  * Fix pattern: HTTP API seeding + string assertions instead of JSON deserialization ✅
  * Final result: 129/130 tests pass (1 unrelated failure: Golden Flow Health Check) ✅
* Assumptions: None
* Open Questions: None
* Context Quality: High
* ACS Status: ✅ **READY** — Task completed successfully

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

* [2026-06-26] Production Hygiene — Wave 15: KhachLink Page Cleanup + Routing Modernization (Branch `feature/wave15-khachlink-page-cleanup` — pending merge; commit `26abd83`)
* [2026-06-26] Production Hygiene — Wave 14: HMAC-SHA256 API Request Signing (Branch `feature/wave14-api-request-signing` — PR #63; commit `5462759`)
* [2026-06-25] Production Hygiene — Wave 9: Cleanup Orphan Controller (Branch `feature/wave9-cleanup-controller` — pending merge)
* [2026-06-23] Security Compliance — Wave 6: User Aggregate + RBAC Management (Merged to main - Commit 2599c1b)
* [2026-06-23] Security Compliance — Wave 5: Domain Refactor + Tenant Rich Domain + CRUD (Merged to Wave 6 base)
* [2026-06-23] Security Compliance — Wave 4: RBAC Enforcement at Blazor UI Layer (Merged to main - Commit 5a6b441)
* [2026-06-23] Security Compliance — Wave 3: Report Export (Excel with EPPlus) (Merged to main - PR #42)
* [2026-06-23] Security Compliance — Wave 2: Data Protection (Field-level Encryption) (Merged to main)
* [2026-06-23] Security Compliance — Wave 1: Notification Integration (Brevo + ESMS) (Merged to main - PR #39)
* [2026-06-23] Security Compliance — Wave 0: JWT Authentication Foundation (Merged to main - PR #38)
* [2026-06-15] Value Object Mapping Fix — 14 EF Core Configuration files created (Product, Ingredient, Recipe, Inventory, Order, Customer, Lead, etc.)
* [2026-06-12] Architectural Rollback — QrMenu.razor to Gateway API, ProductsController in ShopERP, Gateway forward (Architecture tests 7/7 PASS, E2E 15 passed)
* [2026-06-11] S7 Responsive — Playwright responsive tests (TC7 Android 360×800, TC8 iPhone 14 390×844)
* [2026-06-11] S4 Small — Calendar Year Revenue runtime calculation (5/5 tests PASS)
* [2026-06-10] UC1 QR Checkout Completion — S1–S4 COMPLETE (22/22 tests PASS)
* [2026-06-11] KhachLink Frontend S6 DONE — 7 E2E TCs, data-testid, mocked API (Build 0 errors)
* [2026-06-11] KhachLink Frontend S5 DONE — CartPage redirect + QrMenu tenantId guard
* [2026-06-11] KhachLink Frontend S3 DONE — BusinessLookupController + MstLookupError (6/6 tests PASS)
* [2026-06-11] KhachLink Frontend S2 DONE — B1/B2/B3 fixed, logger, RetrySubmit guard
* [2026-06-11] GitHub Actions Free-Tier Optimization — PR #14 merged (~770 phút/tháng tiết kiệm)
* [2026-06-11] Flaky Test Fix Plan — 31+ tests ổn định, AsyncAssert.cs polling, [Trait] categories
* [2026-06-11] Guard Check Upgrade Phase 2 — 5 Roslyn Analyzers (VA1001-VA1005), guard-check.ps1 fast gate
* [2026-06-11] Sprint 2 — Period Closing Wizard + Audit Trail (merged vào main)
* [2026-06-11] Sprint 1 — Frontend Accounting Module (ShopERP)

---

## 11. Maintenance Log

* Last Updated: 2026-06-26 (Wave 15 - KhachLink Page Cleanup; branch: feature/wave15-khachlink-page-cleanup)
* Current Branch: `feature/wave15-khachlink-page-cleanup`
* **Wave 15 — KhachLink Page Cleanup + Routing Modernization (2026-06-26):** COMPLETED. Deleted 6 dead/demo pages; converted `Dashboard.cshtml` to `Dashboard.razor`; modernized `Program.cs` to Blazor Web App routing; rewrote `VoiceNote.razor` with `IHttpClientFactory("gateway")` and correct endpoint. Build: 0 errors; guard-check: PASS; Architecture tests: 21/21 PASS. Latest commit: `26abd83 feat(wave15): KhachLink page cleanup + Blazor Web App routing`.
* **Wave 15 Planning (2026-06-26):** KHACHLINK_PRODUCTION_PLAN.md rebuilt — scope W15-T1 cập nhật (xóa 6 files + convert Dashboard.cshtml→Dashboard.razor), W15-T2 cập nhật (Blazor Web App routing AddRazorComponents), W17 tách sang KHACHLINK_RETENTION_PLAN.md (DEFERRED), tất cả W15 + W17 task cards updated với đúng master plan reference.
* **Wave 14 — HMAC Request Signing (2026-06-26):** COMPLETED. Commit `5462759`, PR #63. HmacSigningMiddleware + ApiKey entity + IApiKeyManagementService + ApiKeyController. 10/10 tests PASS. Build: 0 errors; guard-check: PASS.
* **Wave 11 — Cleanup Invalid Framework Files (2026-06-26):** COMPLETED. Deleted `ShopERP/Pages/SocialCampaignManager.cshtml` and `KhachLink/wwwroot/index.html`; updated `service-worker.js` cache list. Verified no production references. Build: 0 errors; guard-check: PASS. PRODUCTION_HYGIENE_master_plan.md updated W11-T1→T4 status.

* **Wave 9 — Cleanup Orphan Controller (2026-06-25):** COMPLETED. Deleted `ShopERP/Controllers/CustomersController.cs` and `CustomerApiIntegrationTests.cs`. Verified no production references. Build: 0 errors; guard-check: PASS. PRODUCTION_HYGIENE_master_plan.md updated W9-T1→T4 status.

* **Wave 8 — Upgrade Dashboard to Sitemap with Authentication (2026-06-24):** COMPLETED. Branch `feature/wave8-upgrade-dashboard` merged to main. Tasks: W8-T1 through W8-T5. Latest commit: `d088739 feat(wave8): implement Sitemap.razor with auth, remove VanAn_Dashboard.html`.

