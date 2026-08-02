# TASK CARD: INTEGRATION TESTS - WAVE 2 - Convert Integration Tests to Real Database

## 1. GOAL & CONTEXT
- **Mục tiêu cốt lõi:** Convert integration tests từ fake stub sang real SQLite database với Testcontainers
- **Nghiệp vụ áp dụng:** Eliminate fake integration tests, enable real database testing cho accounting module

## 2. ACTIVE WORKFLOW ROUTING
- **Target Workflow:** `newfeaturebuild.md` (7-step ANALYZE → IMPLEMENT)
- **Execution Mode:** IMPLEMENT (refactor existing integration tests với real database)

## 3. RELEVANT FILES (CONTEXT BOUNDARY)
- **Files được phép đọc/sửa:**
  - `docs/AI/project_state.md` (Bắt buộc đọc đầu phiên)
  - `6_Tests/VanAn.Integration.Tests/Infrastructure/AccountingEntryServiceStub.cs` (DELETE)
  - `6_Tests/VanAn.Integration.Tests/Accounting/AccountingEntryFlowTests.cs` (Refactor)
  - `6_Tests/VanAn.Integration.Tests/Accounting/BalanceCalculationTests.cs` (Refactor)
  - `6_Tests/VanAn.Integration.Tests/Accounting/MultiTenancyTests.cs` (Refactor)
  - `6_Tests/VanAn.Integration.Tests/Accounting/TransactionHistoryQueryTests.cs` (Refactor)
  - `6_Tests/VanAn.Integration.Tests/Accounting/AccountingUIServiceTests.cs` (Refactor)
  - `6_Tests/VanAn.Integration.Tests/Infrastructure/TestDatabaseFixture.cs` (Add cleanup logic)
- **Boundary Rules (Nghiêm cấm):**
  - KHÔNG modify production code (1_Shared, 3_CoreHub)
  - KHÔNG modify unit test files (Wave 2 chỉ integration tests)
  - KHÔNG bypass test failures - phải fix tests để pass với real database
  - KHÔNG delete tests - chỉ refactor implementation

## 4. TECHNICAL & REGULATORY CONSTRAINTS (HARDENING GATES)
- [ ] **Database Cleanup:** TestDatabaseFixture phải cleanup database giữa test runs (no data leakage)
- [ ] **Tenant Isolation:** MultiTenancyTests phải verify real tenant isolation ở database level
- [ ] **SQL Queries:** Tests phải verify SQL queries work, không LINQ-to-Objects
- [ ] **Performance:** Test execution time không tăng quá 50% so với stub version
- [ ] **Schema Sync:** Test database schema phải sync với production schema (migrations)

## 5. SUCCESS CRITERIA (ĐO LƯỜNG ĐƯỢC)
- [ ] **SC1:** AccountingEntryServiceStub.cs deleted
- [ ] **SC2:** AccountingEntryFlowTests uses TestDatabaseFixture + real DbContext
- [ ] **SC3:** BalanceCalculationTests uses real database, verifies SQL queries
- [ ] **SC4:** MultiTenancyTests verifies real tenant isolation ở database level
- [ ] **SC5:** TransactionHistoryQueryTests tests real SQL filtering logic
- [ ] **SC6:** AccountingUIServiceTests uses real service + test database (remove Mock<IAccountingService>)
- [ ] **SC7:** TestDatabaseFixture có cleanup logic giữa test runs
- [ ] **SC8:** All 15 integration tests pass với real database
- [ ] **SC9:** No in-memory collections used trong integration tests
- [ ] **SC10:** Database cleanup verified (không data leakage giữa tests)
- [ ] **SC11:** Test execution time < 30 seconds cho full integration suite
- [ ] **SC12:** Each test class uses TestDatabaseFixture consistently

**Implementation Date:** 2026-06-26
**Branch:** feature/test-wave2-integration-real

## 6. ACTIVE SKILLS (MAX 3)
- `load-context` — Load project context để hiểu current integration test structure
- `update-state` — Update project_state.md sau khi Wave 2 complete
- `devin-for-terminal` — Lookup Testcontainers documentation nếu cần

## 7. AI HEALTH CHECK MATRIX (INITIAL)
- **Evidence Count:** 5
- **Verified Facts:**
  - Fact 1: AccountingEntryServiceStub uses in-memory List<AccountingEntryDto>
  - Fact 2: All 5 integration test classes use stub or mocks
  - Fact 3: Current integration tests pass (15/15) nhưng với fake data
  - Fact 4: TestDatabaseFixture được tạo trong Wave 0
  - Fact 5: AccountingUIServiceTests uses Mock<IAccountingService>
- **Assumptions:**
  - TestDatabaseFixture từ Wave 0 working correctly
  - SQLite schema sync với production schema
  - Real DbContext có thể inject vào integration tests
- **Open Questions:**
  - Q1: Cần apply migrations trong TestDatabaseFixture hay seed schema trực tiếp?
  - Q2: Cleanup strategy: drop database hay truncate tables?
  - Q3: AccountingUIServiceTests có thể use real service không hay cần keep mock?
- **Recommended Action:** Start với ANALYZE phase để review TestDatabaseFixture từ Wave 0 và plan refactor strategy cho từng test class

## 8. REVERSE IMPACT ANALYSIS
| File thay đổi | Reverse impact | Mitigation |
|---|---|---|
| AccountingEntryServiceStub.cs | Delete fake implementation | Restore file from git if needed |
| AccountingEntryFlowTests.cs | Refactor to use real database | Git revert test file changes |
| BalanceCalculationTests.cs | Refactor to use real database | Git revert test file changes |
| MultiTenancyTests.cs | Refactor to use real database | Git revert test file changes |
| TransactionHistoryQueryTests.cs | Refactor to use real database | Git revert test file changes |
| AccountingUIServiceTests.cs | Refactor to use real service | Git revert test file changes |
| TestDatabaseFixture.cs | Add cleanup logic | Git revert cleanup logic changes |

## 9. TDD & E2E TESTING STRATEGY
- **Integration Test Refactor Strategy:**
  - Delete stub trước (AccountingEntryServiceStub)
  - Refactor từng test class (sequential để avoid break toàn bộ suite)
  - Add cleanup logic vào TestDatabaseFixture
  - Verify real SQL queries vs LINQ-to-Objects
- **Test boundary:**
  - Unit tests: Không affected (Wave 2 chỉ integration tests)
  - Integration tests: Refactor existing integration tests để use real database
  - E2E tests: Không affected (Wave 2 chỉ integration tests)

## 10. JIT PLANNING + PURE EXECUTION (MICRO-PHASES)

### Chiến lược thực thi: JIT Planning + Pure Execution

Wave 2 refactor nhiều test files nên sẽ follow approach:
1. **ANALYZE phase:** Review TestDatabaseFixture, plan refactor strategy cho từng test class, identify dependencies
2. **IMPLEMENT phase:** Delete stub, refactor test classes sequentially, add cleanup logic, verify all tests pass

### Micro-phase breakdown cho Wave 2

| Session | JIT Planning (chốt gì) | Pure Execution (viết gì) |
|---|---|---|
| **S1** | Review TestDatabaseFixture từ Wave 0, plan AccountingEntryServiceStub deletion strategy, identify test class dependencies | Delete AccountingEntryServiceStub.cs, update test class references |
| **S2** | Plan AccountingEntryFlowTests refactor, identify DbContext injection points, plan test data seeding | Refactor AccountingEntryFlowTests to use TestDatabaseFixture, verify tests pass |
| **S3** | Plan BalanceCalculationTests refactor, identify SQL query verification points, plan test data setup | Refactor BalanceCalculationTests to use real database, verify SQL queries work |
| **S4** | Plan MultiTenancyTests refactor, identify tenant isolation verification points, plan multi-tenant test data | Refactor MultiTenancyTests to verify real tenant isolation, verify tests pass |
| **S5** | Plan TransactionHistoryQueryTests refactor, identify SQL filtering verification points, plan test data scenarios | Refactor TransactionHistoryQueryTests to test real SQL filtering, verify tests pass |
| **S6** | Plan AccountingUIServiceTests refactor, decide real service vs mock strategy, plan cleanup logic | Refactor AccountingUIServiceTests to use real service, add cleanup logic to TestDatabaseFixture, run full suite |

### Rules
- Refactor test classes sequentially (một class tại một thời điểm)
- Run tests sau mỗi refactor để verify không break existing tests
- Commit sau mỗi session với message format `[WAVE2] Task description`
- Document cleanup strategy trong TestDatabaseFixture XML comments

## 11. ESTIMATED EFFORT
- 5-6 sessions theo JIT Planning
- **BLOCKER:** TestDatabaseFixture từ Wave 0 không working hoặc database schema sync issues
