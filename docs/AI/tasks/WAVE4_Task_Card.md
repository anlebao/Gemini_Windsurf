# TASK CARD: MISSING TESTS - WAVE 4 - Add Missing Real Tests

## 1. GOAL & CONTEXT
- **Mục tiêu cốt lõi:** Add missing integration và unit tests để improve coverage và test completeness
- **Nghiệp vụ áp dụng:** Achieve >80% code coverage, balanced test pyramid (70% unit, 20% integration, 10% E2E)

## 2. ACTIVE WORKFLOW ROUTING
- **Target Workflow:** `newfeaturebuild.md` (7-step ANALYZE → IMPLEMENT)
- **Execution Mode:** IMPLEMENT (add new tests, không modify existing tests)

## 3. RELEVANT FILES (CONTEXT BOUNDARY)
- **Files được phép đọc/sửa:**
  - `docs/AI/project_state.md` (Bắt buộc đọc đầu phiên)
  - `6_Tests/VanAn.Integration.Tests/Accounting/PeriodClosingIntegrationTests.cs` (Create new)
  - `6_Tests/VanAn.Integration.Tests/Accounting/ReversalIntegrationTests.cs` (Create new)
  - `6_Tests/VanAn.Integration.Tests/Accounting/JournalEntryIntegrationTests.cs` (Create new)
  - `6_Tests/VanAn.Integration.Tests/Accounting/HKDBookIntegrationTests.cs` (Create new)
  - `6_Tests/VanAn.Core.Tests/Accounting/BusinessRulesTests.cs` (Replace placeholder)
  - `6_Tests/VanAn.Core.Tests/Accounting/AccountingEdgeCaseTests.cs` (Create new)
  - `6_Tests/VanAn.Integration.Tests/Api/AccountingApiContractTests.cs` (Create new)
- **Boundary Rules (Nghiêm cấm):**
  - KHÔNG modify existing test files (Wave 4 chỉ add new tests)
  - KHÔNG modify production code (chỉ add tests)
  - KHÔNG add tests cho features không implemented
  - KHÔNG bypass existing tests - focus on adding new coverage

## 4. TECHNICAL & REGULATORY CONSTRAINTS (HARDENING GATES)
- [ ] **Test Pyramid Balance:** Maintain 70% unit, 20% integration, 10% E2E ratio
- [ ] **Coverage Target:** Achieve >80% code coverage cho accounting module
- [ ] **Test Quality:** New tests phải be meaningful (test behavior, not implementation)
- [ ] **Test Isolation:** New tests phải be isolated (không shared state)
- [ ] **Test Performance:** New tests không increase execution time quá 30%

## 5. SUCCESS CRITERIA (ĐO LƯỜNG ĐƯỢC)
- [ ] **SC1:** PeriodClosingIntegrationTests covers close/reopen scenarios với real database
- [ ] **SC2:** ReversalIntegrationTests tests database transactions cho reversal operations
- [ ] **SC3:** JournalEntryIntegrationTests tests debit/credit balance với real database
- [ ] **SC4:** HKDBookIntegrationTests tests period calculations với real database
- [ ] **SC5:** BusinessRulesTests replaces placeholder với real business rules tests
- [ ] **SC6:** AccountingEdgeCaseTests covers boundary conditions và edge cases
- [ ] **SC7:** AccountingApiContractTests verifies request/response formats
- [ ] **SC8:** All new tests pass
- [ ] **SC9:** Existing tests still pass (no regressions)
- [ ] **SC10:** Code coverage >80% cho accounting module
- [ ] **SC11:** Test pyramid balanced (70% unit, 20% integration, 10% E2E)
- [ ] **SC12:** Test execution time < 90 seconds cho full test suite

**Implementation Date:** 2026-06-26
**Branch:** feature/test-wave4-add-missing-tests

## 6. ACTIVE SKILLS (MAX 3)
- `load-context` — Load project context để understand coverage gaps
- `update-state` — Update project_state.md sau khi Wave 4 complete
- `devin-for-terminal` — Lookup testing best practices nếu cần

## 7. AI HEALTH CHECK MATRIX (INITIAL)
- **Evidence Count:** 3
- **Verified Facts:**
  - Fact 1: PeriodClosing integration tests không exist
  - Fact 2: Reversal integration tests không exist
  - Fact 3: BusinessRulesTests là placeholder test
- **Assumptions:**
  - Coverage report available để identify gaps
  - Real database infrastructure từ Wave 2 working
  - Business rules implementation available để test
- **Open Questions:**
  - Q1: Coverage gaps ở đâu trong accounting module?
  - Q2: Business rules nào cần test?
  - Q3: API contract tests cần test endpoints nào?
- **Recommended Action:** Start với ANALYZE phase để generate coverage report và identify test gaps

## 8. REVERSE IMPACT ANALYSIS
| File thay đổi | Reverse impact | Mitigation |
|---|---|---|
| PeriodClosingIntegrationTests.cs | New file, no existing impact | Delete file if needed |
| ReversalIntegrationTests.cs | New file, no existing impact | Delete file if needed |
| JournalEntryIntegrationTests.cs | New file, no existing impact | Delete file if needed |
| HKDBookIntegrationTests.cs | New file, no existing impact | Delete file if needed |
| BusinessRulesTests.cs | Replace placeholder | Restore placeholder from git if needed |
| AccountingEdgeCaseTests.cs | New file, no existing impact | Delete file if needed |
| AccountingApiContractTests.cs | New file, no existing impact | Delete file if needed |

## 9. TDD & E2E TESTING STRATEGY
- **New Test Strategy:**
  - Generate coverage report để identify gaps
  - Add integration tests cho missing critical paths (PeriodClosing, Reversal, JournalEntry, HKDBook)
  - Add unit tests cho business rules và edge cases
  - Add API contract tests để verify request/response formats
- **Test boundary:**
  - Unit tests: Add business rules và edge case tests
  - Integration tests: Add missing integration tests với real database
  - E2E tests: Không affected (Wave 4 chỉ unit/integration tests)

## 10. JIT PLANNING + PURE EXECUTION (MICRO-PHASES)

### Chiến lược thực thi: JIT Planning + Pure Execution

Wave 4 add new tests nên sẽ follow approach:
1. **ANALYZE phase:** Generate coverage report, identify test gaps, plan new test structure
2. **IMPLEMENT phase:** Add integration tests, add unit tests, add API contract tests, verify coverage

### Micro-phase breakdown cho Wave 4

| Session | JIT Planning (chốt gì) | Pure Execution (viết gì) |
|---|---|---|
| **S1** | Generate coverage report, identify coverage gaps, prioritize test additions, plan integration test structure | Generate coverage report, document test gaps, plan test additions |
| **S2** | Plan PeriodClosingIntegrationTests scenarios (close, reopen, validation), identify test data needs | Create PeriodClosingIntegrationTests.cs, implement close/reopen scenarios, verify tests pass |
| **S3** | Plan ReversalIntegrationTests scenarios (create reversal, tenant isolation, transaction rollback), identify test data needs | Create ReversalIntegrationTests.cs, implement reversal scenarios, verify tests pass |
| **S4** | Plan JournalEntryIntegrationTests scenarios (debit/credit balance, validation, immutability), identify test data needs | Create JournalEntryIntegrationTests.cs, implement balance scenarios, verify tests pass |
| **S5** | Plan HKDBookIntegrationTests scenarios (period calculations, revenue/expense totals, profit calculations), identify test data needs | Create HKDBookIntegrationTests.cs, implement period calculation scenarios, verify tests pass |
| **S6** | Plan BusinessRulesTests (replace placeholder), identify business rules to test, plan test scenarios | Replace BusinessRulesTests placeholder with real business rules tests, verify tests pass |
| **S7** | Plan AccountingEdgeCaseTests (boundary conditions, null handling, invalid inputs), plan test scenarios | Create AccountingEdgeCaseTests.cs, implement edge case scenarios, verify tests pass |
| **S8** | Plan AccountingApiContractTests (request/response formats, error handling, validation), identify endpoints to test | Create AccountingApiContractTests.cs, implement contract tests, run full suite, verify coverage |

### Rules
- Add tests theo priority (critical paths trước)
- Run tests sau mỗi addition để verify không break existing tests
- Commit sau mỗi session với message format `[WAVE4] Task description`
- Generate coverage report sau Wave 4 complete để verify >80% target

## 11. ESTIMATED EFFORT
- 4-5 sessions theo JIT Planning
- **BLOCKER:** Coverage report không available hoặc business rules implementation không testable
