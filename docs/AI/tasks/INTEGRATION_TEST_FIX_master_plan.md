# MASTER IMPLEMENTATION PLAN — Integration Test Failures Fix
# VanAn Ecosystem — Integration Test Stabilization

**Created:** 2026-06-23
**Last Updated:** 2026-06-23
**Current Status:** Wave 1 IN PROGRESS
**Source Analysis:** `docs/AI/integration_test_failures_analysis.md`
**Branch strategy:** feature branch per wave → PR → merge into `main`
**Execution principle:** Wave-by-wave sequential. Wave N does not start until Wave N-1 passes exit criteria.

---

## 0. EXECUTION RULES

### Session protocol
1. **Read `docs/AI/project_state.md` + this master plan BEFORE writing any code.**
2. **Reproduce the failure first.** Each wave starts with running the affected tests and capturing the exact error.
3. **DO NOT modify `1_Shared/Domain.cs` to fix tests.** If a missing domain entity/property is discovered, report it as a Domain Modeling Defect and await approval.
4. **After every test fix, run the targeted test:** `dotnet test VanAn.Integration.Tests.csproj --filter "FullyQualifiedName~TestName"`
5. **Run `dotnet build VanAn.sln` + `guard-check.ps1` before each commit.**
6. **After each micro-phase: commit intermediate, message format:** `[IT-Fix-W{wave}-T{task}] {description}`.

### Branch protocol
```
main
    └── feature/integration-test-fix-w1    (Wave 1 — Investigation)
    └── feature/integration-test-fix-w2    (Wave 2 — Infrastructure Cleanup)
    └── feature/integration-test-fix-w3    (Wave 3 — Shop API Tests)
    └── feature/integration-test-fix-w4    (Wave 4 — Customer API Tests)
    └── feature/integration-test-fix-w5    (Wave 5 — Lead Conversion Tests)
    └── feature/integration-test-fix-w6    (Wave 6 — Health Check & Final Validation)
```
- Each wave branches from `main` (after previous wave is merged).
- PR description must link back to this master plan.
- Squash merge to keep history clean.

### Hard rules (non-negotiable)
- **Domain Layer Protection:** Do NOT modify `1_Shared/Domain.cs` to fix test failures.
- **AccountingEntry Immutability:** No changes to immutable accounting entries.
- **Multi-tenancy:** Preserve `TenantId` filtering in all fixes.
- **Architecture tests must PASS:** `6_Tests/VanAn.Architecture.Tests` must be green after each wave.
- **guard-check.ps1 must PASS:** Run before any PR submission.

---

## 1. WAVE 1 — Investigation & Reproduction

**Branch:** `feature/integration-test-fix-w1`
**Estimated sessions:** 1
**Priority:** 🔴 CRITICAL — Cannot fix what we cannot reproduce
**Conflict risk:** LOW — Read-only investigation

### Problem statement
- 21 integration tests fail with `SQLite Error 19: FOREIGN KEY constraint failed`.
- Failures cluster in 4 categories: Shop API (8), Customer API (7), Lead Conversion (5), Health Check (1).
- Root cause patterns: missing parent entity, multi-tenant isolation violation, database state pollution.

### Tasks
| # | Task ID | Task | Depends on | Status |
|---|---|---|---|---|
| 1 | IT-W1-T1 | Run full integration test suite: `dotnet test 6_Tests/VanAn.Integration.Tests/VanAn.Integration.Tests.csproj` and capture all 21 failures | — | ⬜ PENDING |
| 2 | IT-W1-T2 | Review `3_CoreHub/Infrastructure/Configurations/ShopConfiguration.cs` — identify FK dependencies | — | ⬜ PENDING |
| 3 | IT-W1-T3 | Review `CustomerConfiguration.cs`, `OrderConfiguration.cs`, `LoyaltyRewardsConfiguration.cs` | — | ⬜ PENDING |
| 4 | IT-W1-T4 | Inspect `IntegrationTestBase.cs` — verify `PRAGMA foreign_keys` enforcement and in-memory SQLite behavior | — | ⬜ PENDING |
| 5 | IT-W1-T5 | Inspect `TestTenantProvider.cs` and `TestEntityBuilder.cs` — confirm tenant consistency and parent-entity creation order | — | ⬜ PENDING |

### Entry criteria
- [ ] Branch `feature/integration-test-fix-w1` created from latest `main`
- [ ] `dotnet build VanAn.sln` → 0 errors on current branch
- [ ] `VanAn.Integration.Tests.csproj` exists and can be discovered

### Exit criteria — ALL must PASS
- [x] Exact list of root causes documented (reproduction completed 2026-06-23)
- [x] Classification complete: Group A (KhachLink DI) vs Group B (IntegrationTestBase DI/assertions/FK)
- [x] `investigation_log.md` updated with evidence and stack traces
- [ ] `dotnet build VanAn.sln` → 0 errors
- [ ] `guard-check.ps1` → PASS

---

## 1.1 WAVE 1 FINDINGS — Reproduction Results (2026-06-23)

### Actual failure distribution

| Group | Tests | Root Cause | Files involved |
|---|---|---|---|
| **Group A** | 16 tests (Shop API 8, Customer API 7, Health Check 1) | KhachLink `Program.cs` registers CoreHub services (`IOrderWorkflowService`, `ISocialCampaignService`, `IDashboardService`) without their required repositories. DI validation fails when `CustomWebApplicationFactory` builds the host. | `5_WebApps/KhachLink/Program.cs`, `6_Tests/VanAn.Integration.Tests/Infrastructure/CustomWebApplicationFactory.cs` |
| **Group B** | 5 tests (`LeadToCustomerConversionTests`) | `IntegrationTestBase` missing `INotificationService` for `CustomerOnboardingService`; 2 tests have stale assertion strings; 1 test has genuine FK constraint failure. | `6_Tests/VanAn.Integration.Tests/Infrastructure/IntegrationTestBase.cs`, `LeadToCustomerConversionTests.cs` |

### Group A detail — KhachLink DI validation
```
System.AggregateException : Some services are not able to be constructed
  - IOrderWorkflowService → requires IOrderRepository
  - ISocialCampaignService → requires ISocialCampaignRepository
  - IDashboardService → requires ISystemMetricsRepository
```
- This is the pre-existing **TD-001 KhachLink Architectural Violation** (`docs/AI/tasks/TD-001_KhachLink_ArchitecturalViolation.md`).
- KhachLink must not directly inject CoreHub services that depend on repositories/DbContext.

### Group B detail — LeadToCustomerConversionTests
| Test | Error |
|---|---|
| `LeadConversion_Flow_ShouldCreateCustomerWithLoyalty` | `Unable to resolve service for type 'INotificationService'` |
| `LeadConversion_Batch_ShouldProcessMultipleLeads` | `Unable to resolve service for type 'INotificationService'` |
| `LeadConversion_Failed_ShouldRollbackChanges` | `Assert.Contains` sub-string `"already exists"` not found |
| `LeadConversion_ValidateLead_ShouldCheckQualification` | `Assert.Contains` sub-string `"unqualified"` not found |
| `LeadConversion_WithOrders_ShouldImportOrderHistory` | `SQLite Error 19: FOREIGN KEY constraint failed` (only true FK failure) |

### Implications for plan
The original assumption that all 21 failures were SQLite FK constraints is **incorrect**. The work must be split into:
1. **Architecture decision:** How to handle Group A (TD-001) — correct fix, temporary fix, or test-only stub.
2. **Test infrastructure fix:** Group B can be fixed within the integration test project without touching production architecture.

---

## 2. WAVE 2 — Test Infrastructure Cleanup

**Branch:** `feature/integration-test-fix-w2`
**Estimated sessions:** 1–2
**Priority:** 🔴 CRITICAL — Foundation for all subsequent fixes
**Conflict risk:** MEDIUM — Changes shared test infrastructure
**Depends on:** Wave 1 (root cause confirmed)

### Problem statement
- SQLite in-memory does not enforce FK constraints by default (`PRAGMA foreign_keys = OFF`).
- Tests share a single in-memory connection but do not always seed parent entities (Tenant, Shop, Customer) before child inserts.
- No deterministic cleanup between tests, causing state pollution.

### Quyết định kiến trúc (to be confirmed after Wave 1)
- Enable `PRAGMA foreign_keys = ON` in `IntegrationTestBase`.
- Add `EnsureTestTenantExists()`, `EnsureTestShopExists()`, `EnsureTestCustomerExists()` helpers in base class.
- Update `TestEntityBuilder.CreateTestScenario()` to create entities in FK-safe order: Tenant → Customer → Shop → Order.
- Consider transaction rollback or per-test connection reset for cleanup.

### Tasks
| # | Task ID | Task | Depends on | Status |
|---|---|---|---|---|
| 6 | IT-W2-T1 | Enable `PRAGMA foreign_keys = ON` in `IntegrationTestBase` constructor | — | ⬜ PENDING |
| 7 | IT-W2-T2 | Add `EnsureTestTenantExists()` helper to seed a valid Tenant record | — | ⬜ PENDING |
| 8 | IT-W2-T3 | Add `EnsureTestShopExists()` helper to create a Shop before Order creation | — | ⬜ PENDING |
| 9 | IT-W2-T4 | Add `EnsureTestCustomerExists()` helper to create a Customer before LoyaltyPoints | — | ⬜ PENDING |
| 10 | IT-W2-T5 | Review/improve `Dispose()` and per-test cleanup to prevent state pollution | — | ⬜ PENDING |
| 11 | IT-W2-T6 | Update `TestEntityBuilder.CreateTestScenario()` for correct parent-first order | IT-W2-T2, T3, T4 | ⬜ PENDING |

### Entry criteria
- [ ] Wave 1 merged + `dotnet build` → 0 errors
- [ ] Branch `feature/integration-test-fix-w2` created from updated `main`

### Exit criteria — ALL must PASS
- [ ] FK enforcement active in test context
- [ ] Every test can start with a clean DB and valid tenant
- [ ] `dotnet build VanAn.sln` → 0 errors
- [ ] `guard-check.ps1` → PASS
- [ ] Architecture tests: 7/7 PASS
- [ ] No regression in tests that were previously passing

---

## 3. WAVE 3 — Shop API Tests Fix

**Branch:** `feature/integration-test-fix-w3`
**Estimated sessions:** 1
**Priority:** 🟠 HIGH — 8 failing tests
**Conflict risk:** LOW — Changes only test files
**Depends on:** Wave 2 (infrastructure helpers ready)

### Affected tests
1. Failed API: Update Shop Details — Valid Request
2. Failed API: Create Shop — Valid Request
3. Failed API: Delete Shop — Valid Request
4. Failed API: Shop Statistics — Valid Request
5. Failed API: Shop Search — Valid Request
6. Failed API: Multi-Tenant Shop Isolation
7. Failed API: Get Shop by ID — Valid Request
8. Failed API: Shop Orders — Valid Request

### Tasks
| # | Task ID | Task | Depends on | Status |
|---|---|---|---|---|
| 12 | IT-W3-T1 | Read `6_Tests/VanAn.Integration.Tests/Api/ShopApiIntegrationTests.cs` | — | ⬜ PENDING |
| 13 | IT-W3-T2 | Fix "Create Shop" — ensure Tenant exists before insert | IT-W2-T2 | ⬜ PENDING |
| 14 | IT-W3-T3 | Fix "Delete Shop" — handle FK cascade or delete children first | — | ⬜ PENDING |
| 15 | IT-W3-T4 | Fix "Shop Orders" — create Shop + Customer before Order | IT-W2-T3, T4 | ⬜ PENDING |
| 16 | IT-W3-T5 | Fix "Multi-Tenant Shop Isolation" — use two distinct tenants with complete parent records | — | ⬜ PENDING |
| 17 | IT-W3-T6 | Fix remaining tests: Update, Statistics, Search, Get by ID | — | ⬜ PENDING |

### Exit criteria
- [ ] 8/8 Shop API tests pass
- [ ] No FOREIGN KEY constraint errors
- [ ] `dotnet build VanAn.sln` → 0 errors
- [ ] `guard-check.ps1` → PASS
- [ ] No regression in other tests

---

## 4. WAVE 4 — Customer API Tests Fix

**Branch:** `feature/integration-test-fix-w4`
**Estimated sessions:** 1
**Priority:** 🟠 HIGH — 7 failing tests
**Conflict risk:** LOW — Changes only test files
**Depends on:** Wave 2

### Affected tests
1. Failed API: Get Customer by ID — Valid Request
2. Failed API: Create Customer — Valid Request
3. Failed API: Customer Loyalty Rewards — Valid Request
4. Failed API: Multi-Tenant Customer Isolation
5. Failed API: Add Loyalty Points — Valid Request
6. Failed API: Update Customer Details — Valid Request
7. Failed API: Delete Customer — Valid Request

### Tasks
| # | Task ID | Task | Depends on | Status |
|---|---|---|---|---|
| 18 | IT-W4-T1 | Read `6_Tests/VanAn.Integration.Tests/Api/CustomerApiIntegrationTests.cs` | — | ⬜ PENDING |
| 19 | IT-W4-T2 | Fix "Create Customer" — ensure Tenant exists before insert | IT-W2-T2 | ⬜ PENDING |
| 20 | IT-W4-T3 | Fix "Loyalty Rewards" / "Add Loyalty Points" — create Customer before LoyaltyRewards | IT-W2-T4 | ⬜ PENDING |
| 21 | IT-W4-T4 | Fix "Delete Customer" — delete LoyaltyRewards/Orders children first or use cascade | — | ⬜ PENDING |
| 22 | IT-W4-T5 | Fix "Multi-Tenant Customer Isolation" — use two distinct tenants with complete parent records | — | ⬜ PENDING |
| 23 | IT-W4-T6 | Fix remaining tests: Update, Get by ID | — | ⬜ PENDING |

### Exit criteria
- [ ] 7/7 Customer API tests pass
- [ ] No FOREIGN KEY constraint errors
- [ ] `dotnet build VanAn.sln` → 0 errors
- [ ] `guard-check.ps1` → PASS
- [ ] No regression in other tests

---

## 5. WAVE 5 — Lead Conversion Tests Fix

**Branch:** `feature/integration-test-fix-w5`
**Estimated sessions:** 1–2
**Priority:** 🟠 HIGH — 5 failing tests
**Conflict risk:** MEDIUM — May touch lead conversion services if production bug discovered
**Depends on:** Wave 2

### Affected tests
1. `LeadConversion_Flow_ShouldCreateCustomerWithLoyalty`
2. `LeadConversion_Failed_ShouldRollbackChanges`
3. `LeadConversion_ValidateLead_ShouldCheckQualification`
4. `LeadConversion_WithOrders_ShouldImportOrderHistory`
5. `LeadConversion_Batch_ShouldProcessMultipleLeads`

### Tasks
| # | Task ID | Task | Depends on | Status |
|---|---|---|---|---|
| 24 | IT-W5-T1 | Read `6_Tests/VanAn.Integration.Tests/LeadToCustomerConversionTests.cs` | — | ⬜ PENDING |
| 25 | IT-W5-T2 | Fix `LeadConversion_Flow_ShouldCreateCustomerWithLoyalty` — ensure Lead + Customer + Loyalty created in correct order | — | ⬜ PENDING |
| 26 | IT-W5-T3 | Fix `LeadConversion_Failed_ShouldRollbackChanges` — verify transaction rollback and DB cleanup | — | ⬜ PENDING |
| 27 | IT-W5-T4 | Fix `LeadConversion_WithOrders_ShouldImportOrderHistory` — create Customer + Shop before importing Orders | IT-W2-T3, T4 | ⬜ PENDING |
| 28 | IT-W5-T5 | Fix `LeadConversion_Batch_ShouldProcessMultipleLeads` — create all required parent entities for each lead | — | ⬜ PENDING |
| 29 | IT-W5-T6 | Fix `LeadConversion_ValidateLead_ShouldCheckQualification` — verify validation logic, not FK-related | — | ⬜ PENDING |

### Exit criteria
- [ ] 5/5 Lead Conversion tests pass
- [ ] Rollback test behaves deterministically
- [ ] No FOREIGN KEY constraint errors
- [ ] `dotnet build VanAn.sln` → 0 errors
- [ ] `guard-check.ps1` → PASS
- [ ] No regression in other tests

---

## 6. WAVE 6 — Health Check & Final Validation

**Branch:** `feature/integration-test-fix-w6`
**Estimated sessions:** 1
**Priority:** 🟡 MEDIUM — 1 failing test + final gate
**Conflict risk:** LOW
**Depends on:** Wave 5

### Affected test
1. Failed Golden Flow: Health Check Endpoint

### Tasks
| # | Task ID | Task | Depends on | Status |
|---|---|---|---|---|
| 30 | IT-W6-T1 | Read `6_Tests/VanAn.Integration.Tests/GoldenFlowSystemTests.cs` | — | ⬜ PENDING |
| 31 | IT-W6-T2 | Fix Health Check test — mock/seed required services or verify DB state before assertion | — | ⬜ PENDING |
| 32 | IT-W6-T3 | Run full integration test suite: `dotnet test VanAn.Integration.Tests.csproj` | — | ⬜ PENDING |
| 33 | IT-W6-T4 | Run `dotnet build VanAn.sln` | — | ⬜ PENDING |
| 34 | IT-W6-T5 | Run `guard-check.ps1` | — | ⬜ PENDING |
| 35 | IT-W6-T6 | Run `VanAn.Architecture.Tests` | — | ⬜ PENDING |
| 36 | IT-W6-T7 | Run full suite 3 consecutive times to verify no flakiness | — | ⬜ PENDING |

### Exit criteria — ALL must PASS
- [ ] 21/21 previously failing tests now pass
- [ ] No `SQLite Error 19` or FOREIGN KEY constraint errors
- [ ] Tests run consistently (3 consecutive runs green)
- [ ] `dotnet build VanAn.sln` → 0 errors, 0 new warnings
- [ ] `guard-check.ps1` → PASS
- [ ] `VanAn.Architecture.Tests`: 7/7 PASS

---

## 7. FILE CONFLICT MATRIX

| File zone | W1 | W2 | W3 | W4 | W5 | W6 | Conflict mitigation |
|---|---|---|---|---|---|---|---|
| `6_Tests/VanAn.Integration.Tests/Infrastructure/IntegrationTestBase.cs` | 👁 | ✏️ | — | — | — | — | W2 only — append helpers, no breaking changes |
| `6_Tests/VanAn.Integration.Tests/Infrastructure/TestEntityBuilder.cs` | 👁 | ✏️ | — | — | — | — | W2 only — add helpers, keep existing methods |
| `6_Tests/VanAn.Integration.Tests/Api/ShopApiIntegrationTests.cs` | — | — | ✏️ | — | — | — | W3 only |
| `6_Tests/VanAn.Integration.Tests/Api/CustomerApiIntegrationTests.cs` | — | — | — | ✏️ | — | — | W4 only |
| `6_Tests/VanAn.Integration.Tests/LeadToCustomerConversionTests.cs` | — | — | — | — | ✏️ | — | W5 only |
| `6_Tests/VanAn.Integration.Tests/GoldenFlowSystemTests.cs` | — | — | — | — | — | ✏️ | W6 only |
| `3_CoreHub/Infrastructure/Configurations/*.cs` | 👁 | — | — | — | — | — | Read-only unless production bug confirmed |

---

## 8. VALIDATION CRITERIA

After all waves:
- All 21 integration tests pass
- No FOREIGN KEY constraint errors
- Deterministic test runs (no flakiness)
- No new warnings in `dotnet build`
- No architectural violations
- `guard-check.ps1` PASS

---

## 9. RISK & MITIGATION

| Risk | Mitigation |
|---|---|
| Enabling FK enforcement breaks other passing tests | Run full suite after W2 and fix regressions immediately |
| `TenantId` mismatch between `IntegrationTestBase` and `TestEntityBuilder` | Audit both constants and align to single test tenant |
| Fixing tests exposes a real production bug (e.g., missing cascade) | Fix production code, not test workaround, and add regression test |
| Multi-tenant tests need 2 tenants | Seed 2 separate tenants with complete parent records |
| Long-lived feature branches cause merge conflicts | Keep waves small; merge promptly after exit criteria pass |

---

## 10. NOTES

- These failures are pre-existing and not caused by Wave 6 changes (per source analysis).
- Primary fix target is **test setup and infrastructure**, not production business logic.
- If a production bug is discovered during investigation, it must be reported and fixed separately with its own regression test.
