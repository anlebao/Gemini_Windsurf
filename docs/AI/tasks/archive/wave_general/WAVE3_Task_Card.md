# TASK CARD: UNIT TESTS - WAVE 3 - Reduce Mocks in Unit Tests

## 1. GOAL & CONTEXT
- **Mục tiêu cốt lõi:** Reduce unnecessary mocks trong unit tests, keep only external dependency mocks
- **Nghiệp vụ áp dụng:** Improve unit test quality, eliminate self-mocks, test real business logic

## 2. ACTIVE WORKFLOW ROUTING
- **Target Workflow:** `newfeaturebuild.md` (7-step ANALYZE → IMPLEMENT)
- **Execution Mode:** IMPLEMENT (refactor unit tests để reduce mocks)

## 3. RELEVANT FILES (CONTEXT BOUNDARY)
- **Files được phép đọc/sửa:**
  - `docs/AI/project_state.md` (Bắt buộc đọc đầu phiên)
  - `6_Tests/VanAn.Core.Tests/Accounting/PeriodClosingServiceTests.cs` (Refactor - remove self-mock)
  - `6_Tests/VanAn.Core.Tests/Accounting/AccountingEntryServiceTests.cs` (Refactor - keep only external mocks)
  - `6_Tests/VanAn.Core.Tests/Accounting/ReversalServiceTests.cs` (Refactor - keep repository mock)
  - `6_Tests/VanAn.Core.Tests/Accounting/HKDBookServiceTests.cs` (Refactor - keep repository mock)
  - `6_Tests/VanAn.Core.Tests/Accounting/JournalTemplateTests.cs` (Refactor - remove logger mock)
  - `6_Tests/VanAn.Core.Tests/Accounting/AccountingEntriesControllerTests.cs` (Refactor - keep service mocks)
  - `6_Tests/VanAn.Core.Tests/Accounting/EnhancedJournalFactoryTests.cs` (Refactor - minimize mocks)
  - `6_Tests/VanAn.Core.Tests/Accounting/BusinessRulesTests.cs` (Delete placeholder)
- **Boundary Rules (Nghiêm cấm):**
  - KHÔNG modify production code (1_Shared, 3_CoreHub)
  - KHÔNG modify integration test files (Wave 3 chỉ unit tests)
  - KHÔNG remove all mocks - keep external dependency mocks (repository, HTTP, file system)
  - KHÔNG delete tests đang pass - chỉ refactor implementation

## 4. TECHNICAL & REGULATORY CONSTRAINTS (HARDENING GATES)
- [ ] **External Mocks Only:** Keep only mocks cho external dependencies (repository, HTTP, file system)
- [ ] **No Self-Mocks:** Remove self-mocks (mock interface test chính nó)
- [ ] **Test Isolation:** Tests phải remain isolated (không shared state giữa tests)
- [ ] **Test Performance:** Test execution time không tăng quá 50% sau refactor
- [ ] **Code Coverage:** Code coverage phải maintain hoặc tăng sau refactor

## 5. SUCCESS CRITERIA (ĐO LƯỜNG ĐƯỢC)
- [ ] **SC1:** PeriodClosingServiceTests removes self-mock, tests real implementation
- [ ] **SC2:** AccountingEntryServiceTests keeps only external dependency mocks (repository, audit trail)
- [ ] **SC3:** ReversalServiceTests keeps repository mock, tests real reversal logic
- [ ] **SC4:** HKDBookServiceTests keeps repository mock, tests real period calculations
- [ ] **SC5:** JournalTemplateTests removes logger mock, tests real template logic
- [ ] **SC6:** AccountingEntriesControllerTests keeps service mocks (test API layer, not business logic)
- [ ] **SC7:** EnhancedJournalFactoryTests minimizes mocks, tests real factory logic
- [ ] **SC8:** BusinessRulesTests placeholder deleted hoặc replaced với real tests
- [ ] **SC9:** All unit tests pass với reduced mocks
- [ ] **SC10:** Test isolation maintained (không shared state giữa tests)
- [ ] **SC11:** Test execution time < 60 seconds cho full unit suite
- [ ] **SC12:** Code coverage maintained hoặc increased (>80% cho accounting module)

**Implementation Date:** 2026-06-26
**Branch:** feature/test-wave3-unit-reduce-mocks

## 6. ACTIVE SKILLS (MAX 3)
- `load-context` — Load project context để hiểu current unit test mock usage
- `update-state` — Update project_state.md sau khi Wave 3 complete
- `devin-for-terminal` — Lookup unit testing best practices nếu cần

## 7. AI HEALTH CHECK MATRIX (INITIAL)
- **Evidence Count:** 5
- **Verified Facts:**
  - Fact 1: PeriodClosingServiceTests uses Mock<IPeriodClosingService> (self-mock)
  - Fact 2: AccountingEntryServiceTests mocks repository, audit trail, period closing
  - Fact 3: ReversalServiceTests mocks repository
  - Fact 4: JournalTemplateTests mocks logger
  - Fact 5: BusinessRulesTests là placeholder test (always passes)
- **Assumptions:**
  - Real service implementations available để test
  - External dependencies (repository, HTTP, file system) cần keep mocks
  - Unit tests có thể test real business logic mà không external dependencies
- **Open Questions:**
  - Q1: PeriodClosingService real implementation có available không?
  - Q2: Logger mock có cần thiết không hay có thể test without?
  - Q3: BusinessRules placeholder nên replace với real tests hay delete?
- **Recommended Action:** Start với ANALYZE phase để analyze mock usage trong từng test class và identify which mocks are necessary

## 8. REVERSE IMPACT ANALYSIS
| File thay đổi | Reverse impact | Mitigation |
|---|---|---|
| PeriodClosingServiceTests.cs | Remove self-mock, test real implementation | Git revert test file changes |
| AccountingEntryServiceTests.cs | Keep only external mocks | Git revert test file changes |
| ReversalServiceTests.cs | Keep repository mock | Git revert test file changes |
| HKDBookServiceTests.cs | Keep repository mock | Git revert test file changes |
| JournalTemplateTests.cs | Remove logger mock | Git revert test file changes |
| AccountingEntriesControllerTests.cs | Keep service mocks | Git revert test file changes |
| EnhancedJournalFactoryTests.cs | Minimize mocks | Git revert test file changes |
| BusinessRulesTests.cs | Delete placeholder | Restore file from git if needed |

## 9. TDD & E2E TESTING STRATEGY
- **Unit Test Refactor Strategy:**
  - Analyze mock usage trước (document which mocks are necessary vs unnecessary)
  - Remove self-mocks trước (PeriodClosingServiceTests)
  - Remove unnecessary internal mocks (logger, internal services)
  - Keep only external dependency mocks (repository, HTTP, file system)
  - Delete placeholder tests
- **Test boundary:**
  - Unit tests: Refactor existing unit tests để reduce mocks
  - Integration tests: Không affected (Wave 3 chỉ unit tests)
  - E2E tests: Không affected (Wave 3 chỉ unit tests)

## 10. JIT PLANNING + PURE EXECUTION (MICRO-PHASES)

### Chiến lược thực thi: JIT Planning + Pure Execution

Wave 3 refactor nhiều unit test files nên sẽ follow approach:
1. **ANALYZE phase:** Analyze mock usage trong từng test class, document necessary vs unnecessary mocks
2. **IMPLEMENT phase:** Refactor test classes theo dependency order, remove unnecessary mocks, verify tests pass

### Micro-phase breakdown cho Wave 3

| Session | JIT Planning (chốt gì) | Pure Execution (viết gì) |
|---|---|---|
| **S1** | Analyze mock usage trong tất cả unit test files, document necessary vs unnecessary mocks, identify dependency order | Create mock analysis document, plan refactor order |
| **S2** | Plan PeriodClosingServiceTests refactor, identify self-mock removal strategy, verify real implementation available | Refactor PeriodClosingServiceTests to remove self-mock, test real implementation |
| **S3** | Plan AccountingEntryServiceTests refactor, identify which mocks to keep (external only), plan test data setup | Refactor AccountingEntryServiceTests to keep only external mocks, verify tests pass |
| **S4** | Plan ReversalServiceTests refactor, identify repository mock necessity, plan real reversal logic testing | Refactor ReversalServiceTests to keep repository mock, test real reversal logic |
| **S5** | Plan HKDBookServiceTests refactor, identify repository mock necessity, plan real period calculation testing | Refactor HKDBookServiceTests to keep repository mock, test real period calculations |
| **S6** | Plan JournalTemplateTests refactor, identify logger mock necessity, plan real template logic testing | Refactor JournalTemplateTests to remove logger mock, test real template logic |
| **S7** | Plan AccountingEntriesControllerTests refactor, identify service mock necessity (API layer testing), plan test scenarios | Refactor AccountingEntriesControllerTests to keep service mocks, verify API layer tests |
| **S8** | Plan EnhancedJournalFactoryTests refactor, minimize mocks, plan real factory logic testing, decide placeholder fate | Refactor EnhancedJournalFactoryTests to minimize mocks, delete BusinessRulesTests placeholder, run full suite |

### Rules
- Refactor test classes theo dependency order (avoid break dependencies)
- Run tests sau mỗi refactor để verify không break existing tests
- Commit sau mỗi session với message format `[WAVE3] Task description`
- Document mock analysis decisions trong test file XML comments

## 11. ESTIMATED EFFORT
- 6-8 sessions theo JIT Planning
- **BLOCKER:** Real service implementations không available hoặc mock removal breaks too many tests
