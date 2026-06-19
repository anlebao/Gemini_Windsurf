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

**Phase Next — Sprint A/B/C: Order & Accounting Data Integrity Improvements**

**Status:** ✅ **Sprint A COMPLETED** (merged) | ✅ **Sprint B COMPLETED** — branch `feat/sprint-b-entry-timing`, ready to merge

**Nguồn:** `docs/AI/phase-next-order-accounting-improvements.md` — đã đối soát với source code thực tế (2026-06-20).

### Sprint A — Data Integrity (P0, effort thấp) ✅ COMPLETED

| Task | Mô tả | File chính | Trạng thái |
|---|---|---|---|
| **MP-A0** | JIT Planning — đọc call sites | `IAccountingService.cs`, `AccountingEntryService.cs`, `OrderService.cs` | ✅ DONE |
| **MP-A1** | Domain fix — thêm `AccountCode`, `Vendor`, `Category`, `Reference` vào `AccountingEntry` entity + factory methods | `1_Shared/Domain.cs` | ✅ DONE |
| **MP-A2** | Update `IAccountingService` + `AccountingEntryService` signatures | `3_CoreHub/Services/IAccountingService.cs`, `AccountingEntryService.cs` | ✅ DONE |
| **MP-A3** | Update `AccountingEntriesController` request DTOs + call sites | `2_Gateway/Controllers/AccountingEntriesController.cs` | ✅ DONE |
| **MP-A4** | Update `OrderService` + `AccountingUIService` call sites | `OrderService.cs`, `AccountingUIService.cs` | ✅ DONE |
| **MP-A5** | EF Core config for new fields (no migration — uses `EnsureCreatedAsync`) | `AccountingEntryConfiguration.cs` | ✅ DONE |
| **Verify** | `dotnet build` 0 errors + guard pass + commit | — | ✅ DONE |

**Files đã sửa (branch `feat/sprint-a-accountcode-fields`):**
- `1_Shared/Domain.cs` — `AccountingEntry`: thêm 4 properties + cập nhật constructor + 4 factory methods
- `3_CoreHub/Services/IAccountingService.cs` — `CreateRevenueEntryAsync` / `CreateExpenseEntryAsync` có optional params mới
- `3_CoreHub/Services/AccountingEntryService.cs` — map params xuống entity factory methods
- `2_Gateway/Controllers/AccountingEntriesController.cs` — request DTOs + call sites wired
- `3_CoreHub/Services/OrderService.cs` — call sites thêm `accountCode: "511"` / `"621"`
- `5_WebApps/ShopERP/Services/Accounting/AccountingUIService.cs` — wire form fields through
- `3_CoreHub/Infrastructure/Configurations/AccountingEntryConfiguration.cs` — EF config MaxLength
- `6_Tests/VanAn.Core.Tests/Accounting/AccountingEntriesControllerTests.cs` — fix Moq CS0854
- `6_Tests/VanAn.Integration.Tests/Accounting/AccountingUIServiceTests.cs` — fix Moq CS0854
- `guard-check.ps1` — fix pre-existing PowerShell here-string syntax bug

---

### Sprint B — Accounting Entry Timing (P0, effort medium) ✅ COMPLETED

| Task | Mô tả | File chính | Trạng thái |
|---|---|---|---|
| **B-1a** | Guard `GenerateAccountingEntriesAsync` — xóa unconditional call khỏi `CreateOrderAsync` + `CreateOrderWithQueueAsync` | `OrderService.cs` | ✅ DONE |
| **B-1b** | Thêm `ConfirmPayment()` vào `Order` entity (Domain method) + `ConfirmPaymentAsync()` vào `IOrderService` + implement | `1_Shared/Domain.cs`, `IOrderService.cs`, `OrderService.cs` | ✅ DONE |
| **B-1c** | Thêm `POST /api/webhooks/payment` vào `WebhookController` — nhận payment confirm → gọi `ConfirmPaymentAsync` | `2_Gateway/Controllers/WebhookController.cs` | ✅ DONE |
| **B-2** | Unit tests SC11/SC12/SC13 — 20/20 PASS | `6_Tests/VanAn.Core.Tests/Services/OrderServiceTests.cs` | ✅ DONE |

**Files đã sửa (branch `feat/sprint-b-entry-timing`):**
- `1_Shared/Domain.cs` — `Order.ConfirmPayment(transactionId, paymentMethod)` domain method (idempotency guard)
- `3_CoreHub/Services/IOrderService.cs` — thêm `ConfirmPaymentAsync(orderId, tenantId, transactionId, ct)`
- `3_CoreHub/Services/OrderService.cs` — guard `CreateOrderAsync` + `CreateOrderWithQueueAsync` + implement `ConfirmPaymentAsync`
- `2_Gateway/Controllers/WebhookController.cs` — thêm `POST /api/webhooks/payment` + `PaymentConfirmRequest` DTO + inject `IOrderService`
- `6_Tests/VanAn.Core.Tests/Services/OrderServiceTests.cs` — SC11 (no accounting at creation), SC12 (accounting after payment), SC13 (idempotency)

---

### Sprint C — Service Layer Guards (P2, effort thấp-medium)

| Task | Mô tả | File chính | Trạng thái |
|---|---|---|---|
| **C-1** | Server-side duplicate detection trong `AccountingEntryService` | `AccountingEntryService.cs` | ⬜ TODO |
| **C-2** | Period closing block new entries: check `IPeriodClosingService` trước khi create entry | `AccountingEntryService.cs` | ⬜ TODO |
| **C-3** | COGS từ `Product.CostPrice` thay 70% hardcode | `OrderService.cs:119`, `Domain.cs` (Product entity cần `CostPrice` — **Domain change, cần approval**) | ⬜ BLOCKED (Domain change) |

**Bằng chứng cần fix:**
- `AccountingEntryService.cs` không có duplicate check — client-only (`_recentEntries` list trong Razor)
- `AccountingEntryService.CreateRevenue/ExpenseEntryAsync()` không gọi `IPeriodClosingService`
- `OrderService.cs:119`: `decimal cogsAmount = order.TotalPrice * 0.7m; // Assume 70% COGS for MVP`
- `Product` entity trong `Domain.cs` không có `CostPrice` field

## 3. Current Status

### Completed (tóm tắt — chi tiết xem §10 History)

| Milestone | Date | Branch |
|---|---|---|
| Sprint 1 — Accounting Module Frontend | 2026-06-xx | main |
| Sprint 2 — Period Closing + Audit Trail | 2026-06-xx | main |
| UC1 QR Checkout (22/22 tests) | 2026-06-10 | main |
| Value Object Mapping (14 EF configs) | 2026-06-15 | main |
| Wave 0 — Quick Wins (P0-3, P0-7) | 2026-06-18 | main |
| Wave 1 — TenantId Foundation (P0-1a/b) | 2026-06-18 | main |
| Wave 2 — EInvoice API Layer (P0-6) | 2026-06-19 | main |
| Wave 3 — KhachLink Tenant Context + Accounting Razor Cleanup | 2026-06-19 | main |
| Wave 4 — EInvoice UI + E2E (6 pages, 3 specs) | 2026-06-20 | main |
| P0-2 — E2E False-Positive Fix (T-16/17/18/19/21) | 2026-06-20 | main |
| T-20 — Dev Login + global-setup auth | 2026-06-20 | main |
| T-07 — Gateway /api/accounting alias | 2026-06-20 | main |
| T-02 — KhachLink OrderTracking + Checkout | 2026-06-20 | main |
| T-03 — KhachLink QR Payment Modal | 2026-06-20 | main |
| T-06/T-11/T-21 — E2E Backlog Clearance (balance-dashboard, van-an-dashboard) | 2026-06-20 | main |
| e2e-gap-backlog.md — All P0/P1 items verified OK or fixed | 2026-06-20 | — |

**E2E infrastructure state (verified 2026-06-20):**
- 16 spec files — tất cả dùng `expect()` thật, `storageState` auth, `GATEWAY_URL` thay `COREHUB_URL`
- `global-setup.ts` — POST `/dev/login` → `auth/admin.json`
- `playwright.config.ts` — `storageState: 'auth/admin.json'` global

### Blocked

- **C-3 (COGS từ CostPrice)**: cần thêm `CostPrice` vào `Product` entity trong `1_Shared/Domain.cs` → **Domain change cần Tech Lead approval** trước khi implement.

---

## 4. Next Actions

### Phase Next — Execution Plan (Sprint A → B → C)

> Nguồn: `docs/AI/phase-next-order-accounting-improvements.md` (đã đối soát source code 2026-06-20)

#### Sprint A — Data Integrity Fixes (P0) ✅ COMPLETED

> **Branch:** `feat/sprint-a-accountcode-fields` | All tasks DONE | Build 0 errors | Guard pass

| Task | Mô tả ngắn | Trạng thái |
|---|---|---|
| **MP-A1~A5** | Domain + Service + Controller + OrderService + EF Config | ✅ ALL DONE |
| **Next:** | Merge to `main` → start Sprint B | ⬜ PENDING USER |

#### Sprint B — Accounting Entry Timing (P0, effort medium)

| Task | File cần sửa | Mô tả ngắn |
|---|---|---|
| **B-1** Payment webhook | `OrderService.cs` (xóa/guard `GenerateAccountingEntriesAsync` ở line 80) + `WebhookController.cs` (thêm `POST /api/webhooks/payment` → gọi accounting service) | Doanh thu hiện ghi nhận trước khi khách thanh toán — vi phạm nguyên tắc thực thu |

#### Sprint C — Service Guards (P2)

| Task | File cần sửa | Mô tả ngắn |
|---|---|---|
| **C-1** Duplicate detection | `AccountingEntryService.cs` | Client-only check hiện có thể bypass bằng API call trực tiếp |
| **C-2** Period closing guard | `AccountingEntryService.CreateRevenue/ExpenseEntryAsync()` | Kỳ đã đóng vẫn cho tạo entry qua API |
| **C-3** COGS từ CostPrice | `Domain.cs` (thêm `Product.CostPrice`) + `OrderService.cs` | **BLOCKED** — Domain change cần Tech Lead approval |

#### Backlog còn lại (P1-P3, chưa làm)

| ID | Task | Ghi chú |
|---|---|---|
| P1-3 | Webhook notify Kitchen via SignalR sau payment confirm | `WebhookController.cs` + `OrderHub.cs` + `KitchenService.cs` |
| P1-4 | KhachLink Architecture Debt — `GatewayCustomerService`, `GatewayLoyaltyRewardsService`, `CustomersController` | Fix tạm đã áp dụng (loyalty tắt). Fix triệt để cần Gateway endpoints mới |
| P2-4 | Flaky tests 31+ — `FLAKY_TEST_FIX_PLAN.md` | CI stability |
| P2-5 | Re-enable E2E in CI (`if: false` → conditional) | Cần P0-2 + P1-1 đã DONE ✅ → có thể làm ngay |
| P3-1 | `AccountBalance.razor` dùng `IHKDBookService` thay in-memory grouping | Low priority |
| P3-2 | Export CSV/Excel `TransactionHistory.razor` | Low priority |

### References
- `docs/AI/phase-next-order-accounting-improvements.md` — chi tiết Order/Accounting issues
- `docs/AI/e2e-gap-backlog.md` — E2E gap audit (all P0/P1 items DONE ✅)
- `docs/AI/FLAKY_TEST_FIX_PLAN.md` — Flaky test plan (8 phases)

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

* File Path: `docs/AI/tasks/master-implementation-plan.md`
* Purpose: Sequential wave-by-wave execution plan cho P0 items và TenantId remediation. 6 waves, branch strategy, session protocol.

* File Path: `docs/AI/tasks/task-p0-3-dashboard-di-crash.md`
* Purpose: Wave 0 task P0-3 — fix VanAnDashboard.razor DI crash (FIX_ONLY).

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

* **Wave 3 — TenantId Completion COMPLETED & MERGED (2026-06-19)** — Branch `fix/tenantid-wave3` → `main`. **Phase 3 (P0-1c):** Removed all `Guid.NewGuid()` demo tenant data từ KhachLink: `Index.cshtml.cs` + `Campaign.cshtml.cs` resolve tenant từ `?shopId=xxx` query param. `DashboardHub.JoinTenantGroup` + `JoinShopGroup` verify JWT claim, throw `HubException("Unauthorized: tenant mismatch.")`. `OfflineOrderService.SyncSingleOrderAsync` validate `ShopId` non-empty/valid Guid. **Phase 4 (P0-1d):** 6 Accounting Razor pages (`TransactionHistory`, `ExpenseEntry`, `PeriodClosing`, `RevenueEntry`, `AccountingIndex`, `AccountBalance`) refactored: xóa `_tenantId` field + `FindFirst("TenantId")` + hardcoded `00000000-0000-0000-0000-000000000001`, thay bằng `@inject ITenantProvider` + `TenantProvider.HasTenant` guard. `PeriodClosing` giữ `AuthStateProvider` chỉ cho `userId` (sub claim). Build 0 errors, Arch tests 11/11 PASS. 11 files, 218 insertions, 268 deletions.
* **Wave 2 — EInvoice API Layer COMPLETED (2026-06-19)** — P0-6a Phase A + P0-6b Phase B. Phase A: DELETED `EInvoiceE2ETests.cs` (5 tests dead code), fixed `WebhookController` route from `api/webhook` (singular) to `api/webhooks` (plural), fixed body shape to accept raw provider payload (Viettel/MISA format) with automatic `invoiceNo` extraction. Phase B: Created `EInvoice/` Bounded Context trong `5_WebApps/ShopERP` với file-scoped types, `HKDElectronicInvoiceController` với 4 endpoints (`POST /api/einvoice`, `GET /api/einvoice/{id}`, `POST /api/einvoice/{id}/submit`, `GET /api/einvoice/{id}/status`), 5 DTOs (`CreateInvoiceRequest`, `InvoiceDto`, `InvoiceItemDto`, `SubmitInvoiceResponse`, `InvoiceStatusResponse`), tenant isolation via `ITenantProvider`, integration với `IEInvoiceOrchestrator`. Verification: build 0 errors, arch tests 11/11 PASS. Branch `fix/einvoice-cleanup`.
* **Wave 1 — TenantId Foundation COMPLETED (2026-06-18)** — P0-1a Phase 1 + P0-1b Phase 2 merged to main. Phase 1: Fixed claim name mismatch (`HttpContextTenantProvider` reads "TenantId"), removed `Guid.NewGuid()` from `OrdersController.CreateOrder`, removed body/header TenantId from `AccountingEntriesController`, removed query tenantId from `ProviderController`, `VanAnDbContext` throws on empty TenantId. Phase 2: Created `UserTenant` entity (UserId, TenantId, Role, AssignedAt, IsActive), `UserTenantConfiguration.cs` with composite index, `UserTenants` DbSet, Login.cshtml.cs DB lookup with fallback, standardized claim name to `tenant_id` (snake_case), applied `[Authorize(Policy="RequireTenantAccess")]` to all Gateway controllers, registered `ITenantProvider` in Gateway. Verification: build 0 errors, arch tests 11/11 PASS. Branch `fix/tenantid-remediation` merged to `main`.
* **EInvoice Task Card Review Audit** (2026-06-18) — COMPLETED (REVIEW_ONLY). Audited 2 task cards (`task_sprint3_einvoice.md` + `task_sprint3b_provider_integration.md`) vs codebase thực tế. Phát hiện:
  - **Sprint 3B card outdated:** 3 claims đã done (DI wiring, RetryPolicyService submitAction, config) nhưng card vẫn ghi `[ ]`. Đã update SC + Health Check.
  - **Sprint 3B card false claim:** Fact 12 "HKDElectronicInvoiceController REAL" — FALSE, file đã DELETE commit `e89b6c6`. Đã update.
  - **Sprint 3 card STALE:** vẫn ở ANALYZE mode dù implementation đã diễn ra. Đã mark SUPERSEDED by Sprint 3B + update SC/Health Check.
  - **Test coverage ảo:** EInvoiceProviderTests 0 HTTP mock (9 cases S2 plan chưa viết), CircuitBreakerTests không tồn tại, Core.Tests WebhookService stub (comment author tự ghi "stub implementation"), EInvoiceOrchestratorTests thiếu CreateInvoiceAsync flow.
  - **E2E dead code:** `EInvoiceE2ETests.cs` 5 tests gọi `/api/einvoice*` endpoints không tồn tại (controller đã delete) + `/api/webhooks` (plural, mismatch route `api/webhook` singular) + body shape mismatch. TC-E2E-04 stub `OK || NotFound` always-pass. E2E disabled in CI (`if: false`).
  - **Missing UI:** 6 EInvoice Razor pages + 3 Playwright specs — 0 files exist.
  - **POS stubs:** KiotViet, Sapo — `Task.FromResult(Success: true)` hardcoded, deferred Sprint 4.
  - **Tạo task card mới:** `docs/AI/tasks/task-einvoice-deadcode-cleanup.md` — plan dọn dead code + tạo controller/UI/E2E.
  - **Files updated:** `task_sprint3_einvoice.md`, `task_sprint3b_provider_integration.md`, `sprint3b_provider_detailed_plan.md`. **File created:** `task-einvoice-deadcode-cleanup.md`.
* **Value Object Mapping Fix** (2026-06-15) — COMPLETED. Created 14 EF Core Configuration files for all entities with value objects. Fixed `requires a primary key` errors for ProductId, IngredientId, RecipeId, InventoryId, OrderItemId, LeadId, OrderStatusId, CustomerId, TenantId. Removed inline configurations from VanAnDbContext.cs. Pattern: `HasConversion(id => id.Value, value => new TypeName(value))`
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

* Last Updated: 2026-06-19 — Sprint B COMPLETED. B-1a/B-1b/B-1c/B-2 DONE. Build 0 errors, guard pass, 20/20 tests. Branch `feat/sprint-b-entry-timing`. Files: Domain.cs (Order.ConfirmPayment), IOrderService.cs, OrderService.cs (guard + ConfirmPaymentAsync), WebhookController.cs (POST /api/webhooks/payment), OrderServiceTests.cs (SC11/12/13).
* Previous: 2026-06-19 — Sprint A COMPLETED. All tasks MP-A0~A5 DONE. Build 0 errors, guard pass. 11 files changed. Ready to merge to `main`.
* Previous: 2026-06-20 — Phase Next Sprint A/B/C planning. E2E backlog (P0/P1) DONE. 16 spec files, 0 false-positives.
* Current Branch: `feat/sprint-b-entry-timing`
* **Wave 3 MERGED (2026-06-19):** `fix/tenantid-wave3` → `main`. Phase 3 (KhachLink: Index/Campaign ?shopId, DashboardHub JWT verify, OfflineOrderService ShopId guard) + Phase 4 (6 Accounting Razor pages: replace FindFirst/hardcode with ITenantProvider). Build 0 errors, Arch tests 11/11 PASS, Guard PASSED. 11 files, 218 insertions, 268 deletions.
* **Wave 2 MERGED (2026-06-19):** `fix/einvoice-cleanup` → `main`. Phase A (DELETE EInvoiceE2ETests.cs, fix WebhookController route/body) + Phase B (HKDElectronicInvoiceController with Bounded Context). Build 0 errors, Arch tests 11/11 PASS.
* **Wave 1 Phase 1 COMPLETED (2026-06-18):** TenantId "Stop the Bleeding" security fixes. Files modified: `HttpContextTenantProvider.cs` (claim name fix), `OrdersController.cs` (removed Guid.NewGuid, JWT claim-based), `AccountingEntriesController.cs` (removed body/header TenantId, JWT claim-based), `ProviderController.cs` (removed query tenantId, JWT claim-based), `VanAnDbContext.cs` (throw if TenantId empty). Pre-existing issues fixed: `EInvoiceOrchestratorTests.cs` (corrected property names AggregateId→InvoiceId, Payload→EventData), `VanAnDashboard.razor` (commented out broken DashboardService calls). Build passes, Architecture tests 11/11 PASS.
* **Wave 0 Execution COMPLETED (2026-06-18):** Implemented P0-3 (fix VanAnDashboard.razor DI crash — removed `@inject IDashboardService`) and P0-7 (EInvoice test coverage). P0-7 scope: verified CircuitBreakerServiceTests exist (16+ tests), verified ViettelEInvoiceProviderTests/MisaEInvoiceProviderTests have HTTP mock tests (8 each), added 6 new CreateInvoiceAsync flow tests to EInvoiceOrchestratorTests.cs, rewrote WebhookServiceTests.cs with real DbContext (18 tests vs 12 stub tests). Build passes. 38 tests passed.
* **EInvoice Task Card Review Audit (2026-06-18):** REVIEW_ONLY audit 2 Sprint 3 cards vs codebase. Update `task_sprint3_einvoice.md` (mark SUPERSEDED + update SC/Health Check với 14 verified facts), `task_sprint3b_provider_integration.md` (update SC + Health Check + fix Fact 12 false claim), `sprint3b_provider_detailed_plan.md` (update status tables + mark S1 DONE). Tạo `task-einvoice-deadcode-cleanup.md` (plan dọn dead code EInvoiceE2ETests + fix WebhookController route + tạo HKDElectronicInvoiceController + 6 Razor pages + 3 Playwright specs). Phát hiện chính: backend Sprint 3B đã done, nhưng API controller đã delete, UI/E2E chưa tồn tại, test coverage ảo (stub tests + missing tests + dead E2E tests).
* **Value Object Mapping Fix COMPLETED (2026-06-15):** Created 14 EF Core Configuration files (ElectronicInvoiceConfiguration.cs, OrderConfiguration.cs, CustomerConfiguration.cs, ProductConfiguration.cs, IngredientConfiguration.cs, RecipeConfiguration.cs, InventoryConfiguration.cs, LeadConfiguration.cs, FacebookLeadConfiguration.cs, OrderItemConfiguration.cs, ShopConfiguration.cs, DemoUserConfiguration.cs, SocialCampaignConfiguration.cs, LoyaltyRewardsConfiguration.cs). All value objects now have proper HasConversion. Inline configs removed from VanAnDbContext.cs. Build passes with 0 errors.
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
