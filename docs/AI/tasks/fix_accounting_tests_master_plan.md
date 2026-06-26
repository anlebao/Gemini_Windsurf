# MASTER IMPLEMENTATION PLAN — Fix Accounting Tests Suite

**Created:** 2026-06-26
**Last Updated:** 2026-06-26
**Current Status:** PLANNING
**Branch strategy:** feature/test-accounting-fix (per wave)
**Execution principle:** Incremental validation - each wave must pass before next

---

## 0. EXECUTION RULES

### Session protocol
1. **Mỗi session chỉ làm 1 wave** - không跳步
2. **Trước khi session end**: Chạy full test suite của wave đó, đảm bảo 100% pass
3. **Sau mỗi session**: Commit với message format `[WAVE X] Task description`
4. **Nếu test fail**: Stop session, analyze, fix, rerun trước khi continue
5. **Không modify production code** trừ khi cần thiết để pass test

### Branch protocol
```
main (align-consumer-phase4)
  └── feature/test-wave0-infrastructure (Wave 0)
      └── feature/test-wave1-e2e-fix (Wave 1)
          └── feature/test-wave2-integration-real (Wave 2)
              └── feature/test-wave3-unit-reduce-mocks (Wave 3)
                  └── feature/test-wave4-add-missing-tests (Wave 4)
                      └── feature/test-wave5-cicd-integration (Wave 5)
```
- Mỗi wave có branch riêng để dễ rollback
- Merge wave vào branch trước đó (cherry-pick hoặc rebase)
- Final merge vào main khi tất cả waves complete

### Hard rules (không violate)
- **KHÔNG xóa test đang pass** - chỉ refactor hoặc add mới
- **KHÔNG giảm coverage** - mỗi wave phải maintain hoặc tăng coverage
- **KHÔNG bypass test** - không dùng `skip` hoặc `ignore` trừ khi có lý do documented
- **Domain layer tests phải remain pure** - không thêm EF Core hay DbContext vào domain tests
- **Test Pyramid phải được tuân thủ** - nhiều unit tests, ít integration tests, rất ít E2E tests

---

## 1. WAVE 0 — Test Infrastructure Setup

**Branch:** feature/test-wave0-infrastructure
**Completed:** TBD
**Sessions:** 2-3

### Tasks
| # | Task ID | Task | Files | Task card | Status |
|---|---|---|---|---|---|
| 1 | W0-T1 | Install Testcontainers packages | 6_Tests/*.csproj | Add Testcontainers.Sqlite, Testcontainers.Postgres | PENDING |
| 2 | W0-T2 | Create TestDatabaseFixture base class | 6_Tests/VanAn.Integration.Tests/Infrastructure/TestDatabaseFixture.cs | Implement SQLite test container lifecycle | PENDING |
| 3 | W0-T3 | Create TestDbContextFactory | 6_Tests/VanAn.Integration.Tests/Infrastructure/TestDbContextFactory.cs | Factory pattern for test DbContext with real SQLite | PENDING |
| 4 | W0-T4 | Add test configuration | 6_Tests/appsettings.test.json | Test connection strings, test data seeding config | PENDING |
| 5 | W0-T5 | Create TestDataSeeder utility | 6_Tests/VanAn.Integration.Tests/Infrastructure/TestDataSeeder.cs | Seed test tenants, users, accounting entries | PENDING |

### Entry criteria (Wave 0)
- [ ] Project builds successfully (`dotnet build`)
- [ ] Current test suite runs (even if fake tests pass)
- [ ] Git status clean (no uncommitted changes)

### Exit criteria (Wave 0) — ALL PASSED
- [ ] Testcontainers packages installed in all test projects
- [ ] TestDatabaseFixture can spin up SQLite container successfully
- [ ] TestDbContextFactory creates DbContext with real SQLite connection
- [ ] TestDataSeeder can seed test data without errors
- [ ] Integration test project can reference new infrastructure
- [ ] Documentation added: `6_Tests/README.md` - how to run tests with Testcontainers
- [ ] Sample test written using new infrastructure proves it works

### Why first
- Test infrastructure là foundation cho tất cả waves sau
- Không thể convert integration tests sang real database nếu không có test container
- Cần stable test environment trước khi fix business logic tests
- Setup một lần, reuse cho tất cả waves sau

---

## 2. WAVE 1 — Fix E2E Tests (Playwright)

**Branch:** feature/test-wave1-e2e-fix
**Completed:** TBD
**Sessions:** 3-4
**Conflict risk:** LOW (chỉ ảnh hưởng Playwright config và test files)

### Tasks (sequential — E2E tests cần environment trước khi fix test logic)
| # | Task ID | Task | Depends on | Task card | Status |
|---|---|---|---|---|---|
| 6 | W1-T1 | Create docker-compose.test.yml | 6_Testing/docker-compose.test.yml | Spin up ShopERP, Gateway, KhachLink for E2E tests | PENDING |
| 7 | W1-T2 | Update global-setup.ts | 6_Testing/global-setup.ts | Wait for services ready, handle service startup timeout | PENDING |
| 8 | W1-T3 | Fix accounting-entry-flow.spec.ts auth | 6_Testing/e2e-tests/accounting-entry-flow.spec.ts | Implement proper login flow instead of hardcoded credentials | PENDING |
| 9 | W1-T4 | Fix accounting-flow.spec.ts selectors | 6_Testing/e2e-tests/accounting-flow.spec.ts | Update selectors to match actual UI components | PENDING |
| 10 | W1-T5 | Add retry logic for flaky network tests | 6_Testing/playwright.config.ts | Configure retries for network-dependent tests | PENDING |
| 11 | W1-T6 | Run E2E test suite locally | 6_Testing/ | All accounting E2E tests pass locally | PENDING |

### Entry criteria
- [ ] Wave 0 completed and merged
- [ ] Docker Desktop installed and running
- [ ] ShopERP project builds successfully
- [ ] Current E2E tests documented (which fail, why)

### Exit criteria Phase 1 — Infrastructure Ready
- [ ] docker-compose.test.yml spins up all services (ShopERP:5003, Gateway:5001, KhachLink:5002)
- [ ] Services are healthy before tests start (health check passes)
- [ ] global-setup.ts waits for services with timeout handling
- [ ] Manual curl test to ShopERP succeeds: `curl http://localhost:5003/health`

### Exit criteria Phase 2 — Tests Fixed
- [ ] accounting-entry-flow.spec.ts: 4/4 tests pass
- [ ] accounting-flow.spec.ts: All accounting tests pass
- [ ] E2E tests can run in CI environment
- [ ] Test report generated (HTML + JSON)
- [ ] No hardcoded credentials in test files (use environment variables)

### Why here
- E2E tests cần full stack running - không thể test với mocks
- Infrastructure setup (Wave 0) cần thiết trước
- E2E failures block CI/CD pipeline - ưu tiên cao
- Fix E2E trước khi refactor integration/unit tests để tránh regressions

---

## 3. WAVE 2 — Convert Integration Tests to Real Database

**Branch:** feature/test-wave2-integration-real
**Estimated sessions:** 5-6
**Conflict risk:** MEDIUM (sẽ refactor nhiều integration test files)

### Tasks (sequential — Cần refactor từng test class để tránh break toàn bộ suite)
| # | Task ID | Task | Depends on | Task card |
|---|---|---|---|---|
| 12 | W2-T1 | Delete AccountingEntryServiceStub | 6_Tests/VanAn.Integration.Tests/Infrastructure/AccountingEntryServiceStub.cs | Remove fake implementation |
| 13 | W2-T2 | Refactor AccountingEntryFlowTests | 6_Tests/VanAn.Integration.Tests/Accounting/AccountingEntryFlowTests.cs | Use TestDatabaseFixture + real DbContext |
| 14 | W2-T3 | Refactor BalanceCalculationTests | 6_Tests/VanAn.Integration.Tests/Accounting/BalanceCalculationTests.cs | Use real database, verify SQL queries work |
| 15 | W2-T4 | Refactor MultiTenancyTests | 6_Tests/VanAn.Integration.Tests/Accounting/MultiTenancyTests.cs | Test real tenant isolation in database |
| 16 | W2-T5 | Refactor TransactionHistoryQueryTests | 6_Tests/VanAn.Integration.Tests/Accounting/TransactionHistoryQueryTests.cs | Test real SQL filtering logic |
| 17 | W2-T6 | Refactor AccountingUIServiceTests | 6_Tests/VanAn.Integration.Tests/Accounting/AccountingUIServiceTests.cs | Replace Mock<IAccountingService> with real service + test DB |
| 18 | W2-T7 | Add cleanup logic to TestDatabaseFixture | 6_Tests/VanAn.Integration.Tests/Infrastructure/TestDatabaseFixture.cs | Cleanup database between test runs |
| 19 | W2-T8 | Run full integration test suite | 6_Tests/VanAn.Integration.Tests/ | All 15 integration tests pass with real database |

### Entry criteria
- [ ] Wave 1 completed and merged
- [ ] Wave 0 infrastructure (TestDatabaseFixture) working
- [ ] Current integration tests documented (which use stubs, which use mocks)
- [ ] Database schema stable (no pending migrations)

### Exit criteria Phase A — Stub Removal
- [ ] AccountingEntryServiceStub.cs deleted
- [ ] No in-memory collections used in integration tests
- [ ] All integration tests use TestDatabaseFixture
- [ ] Tests still pass (15/15)

### Exit criteria Phase B — Real Database Verification
- [ ] Each test class uses real SQLite database
- [ ] Database cleanup between tests verified (no data leakage)
- [ ] Multi-tenancy isolation verified at database level
- [ ] SQL queries tested (not LINQ-to-Objects)
- [ ] Performance acceptable (tests run < 30s total)

### Why here (not earlier)
- Cần Testcontainers infrastructure từ Wave 0
- Cần E2E passing từ Wave 1 để ensure backend API working
- Integration tests là middle layer - fix sau unit, trước E2E
- Refactor integration tests có risk break nhiều files - cần stable foundation

---

## 4. WAVE 3 — Reduce Mocks in Unit Tests

**Branch:** feature/test-wave3-unit-reduce-mocks
**Estimated sessions:** 6-8
**Conflict risk:** HIGH (sẽ refactor nhiều unit test files, có thể break dependencies)

### Tasks (sequential — Refactor theo dependency order để minimize breakage)
| # | Task ID | Task | Depends on | Task card |
|---||---|---|---|
| 20 | W3-T1 | Analyze mock usage in unit tests | 6_Tests/VanAn.Core.Tests/Accounting/ | Document which mocks are necessary vs unnecessary |
| 21 | W3-T2 | Refactor PeriodClosingServiceTests | 6_Tests/VanAn.Core.Tests/Accounting/PeriodClosingServiceTests.cs | Remove self-mock, test real implementation |
| 22 | W3-T3 | Refactor AccountingEntryServiceTests | 6_Tests/VanAn.Core.Tests/Accounting/AccountingEntryServiceTests.cs | Keep only external dependency mocks (repository, audit trail) |
| 23 | W3-T4 | Refactor ReversalServiceTests | 6_Tests/VanAn.Core.Tests/Accounting/ReversalServiceTests.cs | Test real reversal logic, keep repository mock |
| 24 | W3-T5 | Refactor HKDBookServiceTests | 6_Tests/VanAn.Core.Tests/Accounting/HKDBookServiceTests.cs | Test real period calculations, keep repository mock |
| 25 | W3-T6 | Refactor JournalTemplateTests | 6_Tests/VanAn.Core.Tests/Accounting/JournalTemplateTests.cs | Remove logger mock, test real template logic |
| 26 | W3-T7 | Refactor AccountingEntriesControllerTests | 6_Tests/VanAn.Core.Tests/Accounting/AccountingEntriesControllerTests.cs | Keep service mocks (test API layer, not business logic) |
| 27 | W3-T8 | Refactor EnhancedJournalFactoryTests | 6_Tests/VanAn.Core.Tests/Accounting/EnhancedJournalFactoryTests.cs | Test real factory logic, minimize mocks |
| 28 | W3-T9 | Delete BusinessRuleTestsPlaceholder | 6_Tests/VanAn.Core.Tests/Accounting/BusinessRulesTests.cs | Remove placeholder, add TODO for real business rules tests |
| 29 | W3-T10 | Run full unit test suite | 6_Tests/VanAn.Core.Tests/Accounting/ | All unit tests pass with reduced mocks |

### Entry criteria
- [ ] Wave 2 completed and merged
- [ ] Integration tests passing with real database
- [ ] Mock analysis documented (which to keep, which to remove)
- [ ] Production code stable (no pending refactors)

### Exit criteria Phase 3 — Mock Reduction
- [ ] Self-mocks removed (PeriodClosingServiceTests)
- [ ] Logger mocks removed (non-critical dependencies)
- [ ] Internal service mocks removed where possible
- [ ] Only external dependency mocks remain (repository, HTTP, file system)
- [ ] Placeholder tests removed or replaced with real tests

### Exit criteria Phase 4 — Test Quality Verification
- [ ] Unit tests still test single responsibility
- [ ] Test isolation maintained (no shared state between tests)
- [ ] Test execution time acceptable (< 60s for full suite)
- [ ] Code coverage maintained or increased
- [ ] Tests document behavior (not just implementation)

### Why parallel with Wave 2
- Unit tests và integration tests independent - có thể refactor song song
- Reduce mocks sau khi integration tests real để catch regressions
- Unit test refactoring có risk cao - cần integration tests làm safety net
- Tối ưu thời gian: refactor 2 layers cùng lúc

---

## 5. WAVE 4 — Add Missing Real Tests

**Branch:** feature/test-wave4-add-missing-tests
**Estimated sessions:** 4-5
**Conflict risk:** LOW (chỉ add new tests, không modify existing)

### Tasks (priority order, có thể parallel)
| # | Task ID | Task | Depends on |
|---|---|---|---|
| 30 | W4-T1 | Add PeriodClosing integration tests | 6_Tests/VanAn.Integration.Tests/Accounting/PeriodClosingIntegrationTests.cs | Wave 2 (real database) |
| 31 | W4-T2 | Add Reversal integration tests | 6_Tests/VanAn.Integration.Tests/Accounting/ReversalIntegrationTests.cs | Wave 2 (real database) |
| 32 | W4-T3 | Add JournalEntry integration tests | 6_Tests/VanAn.Integration.Tests/Accounting/JournalEntryIntegrationTests.cs | Wave 2 (real database) |
| 33 | W4-T4 | Add HKDBook integration tests | 6_Tests/VanAn.Integration.Tests/Accounting/HKDBookIntegrationTests.cs | Wave 2 (real database) |
| 34 | W4-T5 | Add business rules unit tests | 6_Tests/VanAn.Core.Tests/Accounting/BusinessRulesTests.cs | Wave 3 (replace placeholder) |
| 35 | W4-T6 | Add edge case unit tests | 6_Tests/VanAn.Core.Tests/Accounting/AccountingEdgeCaseTests.cs | Wave 3 (reduce mocks) |
| 36 | W4-T7 | Add API contract tests | 6_Tests/VanAn.Integration.Tests/Api/AccountingApiContractTests.cs | Wave 1 (E2E passing) |
| 37 | W4-T8 | Run full test suite | 6_Tests/ | All new tests pass, existing tests still pass |

### Entry criteria
- [ ] Wave 3 completed and merged
- [ ] Wave 2 completed (real database infrastructure)
- [ ] Wave 1 completed (E2E environment working)
- [ ] Coverage report generated (identify gaps)

### Exit criteria Phase C
- [ ] PeriodClosing integration tests cover close/reopen scenarios
- [ ] Reversal integration tests test database transactions
- [ ] JournalEntry integration tests test debit/credit balance
- [ ] HKDBook integration tests test period calculations

### Exit criteria Phase D
- [ ] Business rules unit tests replace placeholder
- [ ] Edge case unit tests cover boundary conditions
- [ ] API contract tests verify request/response formats
- [ ] Overall code coverage > 80% for accounting module
- [ ] Test pyramid balanced: 70% unit, 20% integration, 10% E2E

### Why here
- Cần foundation từ waves trước (infrastructure, real tests, reduced mocks)
- Add tests sau khi refactor existing để avoid duplicate work
- Coverage gaps rõ ràng sau khi refactor existing tests
- Priority thấp hơn fix existing tests nhưng cần thiết cho long-term

---

## 6. WAVE 5 — CI/CD Integration & Documentation

**Branch:** feature/test-wave5-cicd-integration
**Estimated sessions:** 3-4
**Conflict risk:** LOW (chỉ CI config và documentation)

### Tasks (priority order, có thể parallel)
| # | Task ID | Task | Depends on |
|---||---|---|
| 38 | W5-T1 | Update GitHub Actions workflow | .github/workflows/test-accounting.yml | Run all accounting tests in CI |
| 39 | W5-T2 | Add test coverage reporting | .github/workflows/test-accounting.yml | Generate coverage report with codecov |
| 40 | W5-T3 | Configure test parallelization | 6_Testing/playwright.config.ts, .github/workflows/ | Optimize CI test execution time |
| 41 | W5-T4 | Add test documentation | 6_Tests/README.md, docs/testing/ | How to run tests, troubleshooting guide |
| 42 | W5-T5 | Add test maintenance guide | docs/testing/test-maintenance.md | When to add tests, how to refactor tests |
| 43 | W5-T6 | Run full CI pipeline | .github/workflows/ | All tests pass in CI environment |
| 44 | W5-T7 | Merge all waves to main | git | Final integration and validation |

### Entry criteria
- [ ] All waves 0-4 completed and merged
- [ ] All tests passing locally
- [ ] No pending test failures
- [ ] Git history clean (each wave properly committed)

### Exit criteria Phase C
- [ ] GitHub Actions workflow runs all accounting tests
- [ ] Test execution time < 10 minutes in CI
- [ ] Coverage report generated and uploaded
- [ ] Failed tests block PR merges (branch protection)

### Exit criteria Phase D
- [ ] Test documentation complete and up-to-date
- [ ] Maintenance guide reviewed and approved
- [ ] Onboarding guide for new developers (how to run tests)
- [ ] Test suite integrated into release process
- [ ] Final merge to main successful, all tests passing

### Why here
- CI/CD integration cuối cùng để validate toàn bộ pipeline
- Documentation sau khi tests stable để avoid outdated docs
- Final merge ensure tất cả waves work together
- Priority thấp nhưng critical cho long-term maintainability

---

## 7. FILE CONFLICT MATRIX (tại sao thứ tự này)

| File zone | Wave 0 | Wave 1 | Wave 2 | Wave 3 | Wave 4 | Conflict mitigation |
|---|---|---|---|---|---|---|
| **Test Infrastructure** | ✅ NEW | - | 🔧 USE | 🔧 USE | 🔧 USE | Wave 0 creates base, others reuse |
| **Integration Test Files** | - | - | 🔄 REFACTOR | - | ➕ ADD | Wave 2 refactor, Wave 4 add new |
| **Unit Test Files** | - | - | - | 🔄 REFACTOR | ➕ ADD | Wave 3 refactor, Wave 4 add new |
| **E2E Test Files** | - | 🔄 FIX | - | - | - | Wave 1 only, independent |
| **Playwright Config** | - | 🔄 UPDATE | - | - | - | Wave 1 only |
| **Docker Compose** | - | ➕ NEW | - | - | - | Wave 1 only |
| **CI/CD Config** | - | - | - | - | 🔄 UPDATE | Wave 5 only |
| **Documentation** | ➕ ADD | - | - | - | 🔄 UPDATE | Wave 0 base, Wave 5 final |
| **Production Code** | - | - | - | ⚠️ MINOR | - | Wave 3 might need minor fixes |
| **Test Projects** | 🔄 UPDATE | - | - | - | - | Wave 0 only (add packages) |

**Legend:**
- ✅ NEW = Create new file
- 🔄 REFACTOR = Modify existing file
- ➕ ADD = Add new content to existing file
- 🔧 USE = Use existing infrastructure
- ⚠️ MINOR = Minor modifications if needed
- - = No changes

**Conflict mitigation:**
- Wave 0 creates infrastructure, waves 2-4 reuse (no conflict)
- Wave 1 (E2E) independent from unit/integration tests
- Wave 2 (integration) và Wave 3 (unit) có thể chạy parallel
- Wave 4 (add tests) only adds, doesn't modify existing
- Wave 5 (CI/CD) only config, no test logic changes

---

## 8. VISUAL TIMELINE

```
Wave 0: Infrastructure Setup (2-3 sessions)
├─ Install Testcontainers
├─ Create TestDatabaseFixture
├─ Create TestDbContextFactory
├─ Add test configuration
└─ Create TestDataSeeder
         ↓
Wave 1: Fix E2E Tests (3-4 sessions)
├─ Create docker-compose.test.yml
├─ Update global-setup.ts
├─ Fix accounting-entry-flow.spec.ts
├─ Fix accounting-flow.spec.ts
├─ Add retry logic
└─ Run E2E test suite
         ↓
Wave 2: Integration Tests Real DB (5-6 sessions) ───┐
├─ Delete AccountingEntryServiceStub                │
├─ Refactor AccountingEntryFlowTests                │
├─ Refactor BalanceCalculationTests                 │
├─ Refactor MultiTenancyTests                       │
├─ Refactor TransactionHistoryQueryTests           │
├─ Refactor AccountingUIServiceTests                │
├─ Add cleanup logic                                │
└─ Run integration test suite                       │
         ↓                                           │
Wave 3: Reduce Unit Test Mocks (6-8 sessions) ───────┤
├─ Analyze mock usage                               │
├─ Refactor PeriodClosingServiceTests               │
├─ Refactor AccountingEntryServiceTests            │
├─ Refactor ReversalServiceTests                    │
├─ Refactor HKDBookServiceTests                     │
├─ Refactor JournalTemplateTests                    │
├─ Refactor AccountingEntriesControllerTests       │
├─ Refactor EnhancedJournalFactoryTests             │
├─ Delete placeholder tests                         │
└─ Run unit test suite                              │
         ↓                                           │
Wave 4: Add Missing Tests (4-5 sessions) ───────────┤
├─ Add PeriodClosing integration tests             │
├─ Add Reversal integration tests                   │
├─ Add JournalEntry integration tests               │
├─ Add HKDBook integration tests                     │
├─ Add business rules unit tests                    │
├─ Add edge case unit tests                        │
├─ Add API contract tests                           │
└─ Run full test suite                              │
         ↓                                           │
Wave 5: CI/CD Integration (3-4 sessions) ───────────┘
├─ Update GitHub Actions workflow
├─ Add test coverage reporting
├─ Configure test parallelization
├─ Add test documentation
├─ Add test maintenance guide
├─ Run full CI pipeline
└─ Merge all waves to main
```

**Parallel execution:**
- Wave 2 và Wave 3 có thể chạy parallel (integration vs unit tests)
- Wave 4 phụ thuộc vào cả Wave 2 và Wave 3
- Wave 5 phải chạy cuối cùng (sau khi tất cả tests stable)

**Estimated total time:** 23-30 sessions (assuming 1 session = 2-4 hours)

---

## 9. SESSION CHECKLIST (cho mỗi session)

### Before session start
- [ ] Pull latest changes from main
- [ ] Checkout correct wave branch
- [ ] Review wave exit criteria from previous session
- [ ] Read task card for current session's tasks
- [ ] Ensure local environment ready (Docker, .NET SDK, Node.js)

### During session
- [ ] Work on tasks in sequential order
- [ ] Run tests after each task completion
- [ ] Commit changes with proper message format
- [ ] Document any issues or blockers found
- [ ] Update task status in this plan document

### Before session end
- [ ] Run full test suite for current wave
- [ ] Verify all exit criteria for current wave phase met
- [ ] Commit all changes with descriptive message
- [ ] Push branch to remote (if needed)
- [ ] Update this plan with session progress
- [ ] Note any blockers or decisions for next session

---

## 10. ROLLBACK PLAN

Nếu wave fail/conflict không resolve:
1. **Abort current wave** - Stop working on current wave immediately
2. **Assess impact** - Determine which files were modified
3. **Partial rollback** - Git reset to last known good commit within wave
4. **Document failure** - Add note to this plan about what failed and why
5. **Create issue** - GitHub issue with detailed error logs and reproduction steps
6. **Re-strategize** - Review remaining tasks, adjust approach if needed

**Wave-specific rollback:**
- **Wave 0:** Remove Testcontainers packages, revert infrastructure files
- **Wave 1:** Revert docker-compose.test.yml, restore original E2E tests
- **Wave 2:** Restore AccountingEntryServiceStub, revert integration test refactors
- **Wave 3:** Restore original unit test files with mocks
- **Wave 4:** Delete newly added test files
- **Wave 5:** Revert CI/CD config changes

**Critical rollback scenarios:**
- If production code needs modification to pass tests → STOP, review with team
- If test execution time > 10 minutes → STOP, optimize before continue
- If coverage decreases → STOP, investigate before continue
- If tests flaky (intermittent failures) → STOP, stabilize before continue

---

## REFERENCES

### Internal Documentation
- `.devin/rules/governance.md` - Project governance and hard rules
- `.devin/workflows/Fix_Errors.md` - Error fixing workflow
- `6_Tests/README.md` - Current test documentation (to be updated)
- `docs/AI/project_state.md` - Current project state

### External Resources
- [Testcontainers .NET Documentation](https://dotnet.testcontainers.org/)
- [Playwright Best Practices](https://playwright.dev/docs/best-practices)
- [xUnit.net Documentation](https://xunit.net/docs/)
- [Testing .NET with Testcontainers](https://blog.testcontainers.org/blog/2023/02/20/testcontainers-dotnet-getting-started/)

### Test Design Principles
- Test Pyramid: https://martinfowler.com/articles/practical-test-pyramid.html
- London School of TDD: Mockist vs Classicist
- Integration Tests: https://kentbeck.com/articles/integration-test-clean-code/
- Unit Test Best Practices: https://martinfowler.com/bliki/UnitTest.html

### Related Issues
- Issue #XX: Accounting tests failing in CI
- Issue #YY: Need real integration tests for accounting
- Issue #ZZ: E2E tests blocked by missing test environment

---

## APPENDIX: CURRENT TEST INVENTORY

### E2E Tests (Playwright) - 4 tests, 0 passing
- `accounting-entry-flow.spec.ts` - 4 tests (all FAILED: ERR_CONNECTION_REFUSED)
- `accounting-flow.spec.ts` - N tests (not run due to timeout)

### Integration Tests (C#) - 15 tests, 15 passing (FAKE)
- `AccountingEntryFlowTests.cs` - 3 tests (uses AccountingEntryServiceStub)
- `AccountingUIServiceTests.cs` - 4 tests (uses Mock<IAccountingService>)
- `BalanceCalculationTests.cs` - 3 tests (uses AccountingEntryServiceStub)
- `MultiTenancyTests.cs` - 2 tests (uses AccountingEntryServiceStub)
- `TransactionHistoryQueryTests.cs` - 3 tests (uses AccountingEntryServiceStub)

### Unit Tests (C#) - 100+ tests, 100+ passing (70% MOCKED)
**Real Unit Tests (~30 tests):**
- `JournalEntryTests.cs` - 22 tests (domain logic, no mocks)
- `AccountCodeValidationTests.cs` - 7 tests (validation logic, no mocks)
- `PeriodClosingDomainTests.cs` - 5 tests (domain entities, no mocks)
- `DynamicFormExtensionTests.cs` - 4 tests (Blazor components, no mocks)

**Mock-based Unit Tests (~70 tests):**
- `AccountingEntryServiceTests.cs` - 15+ tests (mocks repository, audit trail, period closing)
- `ReversalServiceTests.cs` - 10+ tests (mocks repository)
- `PeriodClosingServiceTests.cs` - 10+ tests (self-mocks IPeriodClosingService)
- `HKDBookServiceTests.cs` - 10+ tests (mocks repository)
- `JournalTemplateTests.cs` - 15+ tests (mocks logger)
- `AccountingEntriesControllerTests.cs` - 10+ tests (mocks service layer)
- `EnhancedJournalFactoryTests.cs` - 15+ tests (mocks multiple dependencies)
- `BusinessRuleTestsPlaceholder.cs` - 1 test (placeholder, always passes)

**Total:** 119+ tests, ~30 real (25%), ~89 fake (75%)
