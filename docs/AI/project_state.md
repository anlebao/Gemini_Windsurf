# Project State

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

Resolve the GitHub CI `pr-check.yml` `build-verify` failure that is blocking PR #1 of Sprint 2 (Phase 2.9.3 Period Closing + Audit Trail), so Sprint 2 can complete and Sprint 3 can begin.

---

## 3. Current Status

### Completed

* Sprint 1 (Phase 2.6 Frontend Accounting Module) is fully completed.
* `docs/AI/project_state.md` was created as the AI project state source of truth.
* Authoritative MVP roadmap `docs/plan_MVP/RoadMap/MVP plan Account M.md` has been read and understood.
* 7 HKD book reference documents confirmed in `docs/plan_MVP/HKD_BookAcc`.
* SQLite integration test fixes confirmed in `docs/SQLite_Configuration_Fix_Plan.md`.
* `.github/workflows/pr-check.yml` has been read and the `build-verify` job is understood.

### In Progress

* Sprint 2 PR #1 (Phase 2.9.3 Period Closing Wizard) is open but blocked by GitHub CI `build-verify` failure.
* Root cause investigation of the CI failure is the active technical priority.

### Blocked

* PR #1 of Sprint 2 cannot complete until GitHub CI `build-verify` passes.
* PR #2 of Sprint 2 (Phase 2.9.4 Audit Trail) cannot start until PR #1 is merged.
* Sprint 3 (Phase 5 E-Invoice Multi-Provider Integration) cannot start until Sprint 2 completes.
* The exact failing step in `build-verify` (restore / build / test) is not yet verified — no GitHub Actions error log has been provided.

---

## 4. Root Cause Analysis

### Problem

GitHub CI Pipeline fails in `.github/workflows/pr-check.yml` at the `build-verify` job, blocking completion of PR #1 in Sprint 2.

### Symptoms

* PR #1 of Sprint 2 cannot be completed and merged.
* PR #2 of Sprint 2 cannot start.
* Sprint 3 is waiting and has not started.
* GitHub CI reports `Build-verify` as failed for the PR.

### Verified Facts

* User confirmed Sprint 1 is completed.
* User confirmed Sprint 2 PR #1 is blocked by GitHub CI `build-verify` failure.
* User confirmed PR #2 of Sprint 2 and Sprint 3 have not started due to this blocker.
* `.github/workflows/pr-check.yml` `build-verify` job runs on `ubuntu-latest` and executes:
  * `dotnet restore VanAn.sln`
  * `dotnet build VanAn.sln --no-restore --configuration Release`
  * `dotnet test 6_Tests/ --verbosity normal`
* The `build-verify` job depends on `pr-metadata` passing first.

### Assumptions

* The failure occurs inside one of the three `build-verify` commands; the exact command is unknown without the GitHub Actions error log.
* The failure may be Linux/Release-specific because GitHub Actions runs on `ubuntu-latest` while local development is on Windows.
* File path case sensitivity or platform-specific code may be a contributing factor.
* Test failures in `6_Tests/` on Linux/Release configuration may differ from local results.

### Most Likely Root Cause

Unknown until GitHub Actions log is inspected. Root Cause Confidence is below threshold for code changes. Three possible failure points:
1. `dotnet restore` — package source, NuGet config, or missing package reference.
2. `dotnet build --configuration Release` — compile error, nullable annotation, analyzer warning-as-error, or Linux path issue.
3. `dotnet test 6_Tests/` — test runtime failure, SQLite file path difference on Linux, or test infrastructure configuration issue.

### Rejected Hypotheses

* Rejected: Sprint 2 can continue without resolving CI. PR #1 cannot merge and PR #2 cannot start.
* Rejected: The issue can be fixed by guessing. The exact failing command and error message must be obtained first.
* Rejected: Local passing build guarantees CI passing. GitHub CI runs on `ubuntu-latest` with `Release` configuration — these are different from local Windows Debug conditions.

---

### Problem

SQLite integration tests previously failed with `SQLite Error 1: 'no such table: Orders'` when inserting Customer entity.

### Symptoms

* Affected tests: `SQLite_SimpleEntity_Insert_WithBehavior_Works`, `SQLite_MultiTenant_WithBusinessRules_Isolation_Works`, `Debug_CustomerInsertOnly`.

### Verified Facts

* `docs/SQLite_Configuration_Fix_Plan.md` records that duplicate EF Core relationship configuration between `CustomerConfiguration.cs` and `OrderConfiguration.cs` was removed.
* Redundant `TenantIdConverter` in `OrderConfiguration.cs` was removed.
* `IsolatedSQLiteTests.cs` was changed from in-memory to file-based SQLite.
* All 6 `IsolatedSQLiteTests` are recorded as passing after the fixes.

### Assumptions

* Source files currently match the fix plan implementation results.
* These SQLite fixes have not been reverted in the branch under the PR.

### Most Likely Root Cause

Primary: duplicate/conflicting EF Core relationship configuration. Secondary: in-memory SQLite lifecycle issues. Tertiary: incorrect foreign key reference. All three were resolved per the fix plan.

### Rejected Hypotheses

* Rejected: The only issue was a missing table. The fix plan identifies relationship configuration conflicts as the deeper cause.

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

* Decision: Treat Phase 5 E-Invoice Multi-Provider Integration as production compliance blocker.
* Reason: Roadmap states core MVP is functional but production-ready HKD compliance requires E-Invoice integration.
* Consequences: Product cannot be claimed production-ready for HKD compliance until E-Invoice implementation and verification are complete.

---

## 6. Coding Plan

Phase 1 — Unblock CI (Current Priority)

* Task: Obtain GitHub Actions error log for PR #1 `build-verify` job.
* Expected Result: Exact failing command (restore / build / test) and error message identified.

Phase 2 — Fix CI Failure

* Task: Reproduce the failure locally using `dotnet build VanAn.sln --configuration Release` and `dotnet test 6_Tests/`, then apply the minimal fix.
* Expected Result: Build and tests pass in Release configuration; PR #1 CI passes.

Phase 3 — Complete Sprint 2

* Task: After PR #1 merges, open PR #2 for Phase 2.9.4 Audit Trail.
* Expected Result: Sprint 2 complete (Period Closing + Audit Trail merged).

Phase 4 — Begin Sprint 3

* Task: Start Phase 5 E-Invoice Multi-Provider Integration after Sprint 2 completes.
* Expected Result: Domain models, provider interfaces, circuit breaker and outbox pattern implemented.

---

## 7. Known Risks

* Risk: GitHub CI `build-verify` failure root cause is unknown without the error log.
* Impact: Risk of applying the wrong fix, wasting time or breaking other things.
* Mitigation: Get the exact failing step/error first; do not patch blindly.

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
* Purpose: Shared domain; E-Invoice domain models to be added here in Sprint 3.

* File Path: `3_CoreHub/Services/EInvoice/`
* Purpose: Planned Sprint 3 location for E-Invoice provider interfaces and infrastructure.

---

## 9. Next Actions

* Action: Provide the GitHub Actions error log for PR #1 `build-verify` job (copy failed step output from GitHub Actions UI).
* Expected Result: Exact failing command and error message identified; Root Cause Confidence rises above 80%.

* Action: Run `dotnet build VanAn.sln --no-restore --configuration Release` locally to check for Release-specific errors.
* Expected Result: Confirm whether the build passes or fails locally in Release mode.

* Action: Run `dotnet test 6_Tests/ --verbosity normal` locally to identify any test failures.
* Expected Result: Confirm test pass/fail state and isolate any failing test projects.

* Action: Apply minimal fix once root cause is verified, then push to the PR branch to re-trigger CI.
* Expected Result: `build-verify` passes on GitHub Actions; PR #1 can be reviewed and merged.

* Action: Update this `project_state.md` after CI passes and PR #1 merges.
* Expected Result: Status reflects Sprint 2 PR #1 complete; next action becomes PR #2 of Sprint 2.

---

## 10. AI Health Check

### Understanding Level

90%

### Root Cause Confidence

30%

### Number Of Unverified Assumptions

4

### Context Quality

Medium

### Recommended Action

Investigate Further

> Reasoning: Sprint status is now clear (Sprint 1 done, Sprint 2 blocked). However, the exact failing command and error message in CI `build-verify` have not been provided. Root Cause Confidence = 30% — below the 60% threshold required to make code changes. Do not touch source code until the GitHub Actions error log is reviewed.
