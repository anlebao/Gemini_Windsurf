# TASK CARD: ADR-001 - Wave 1 - CI Check Compliance Test

## 1. GOAL & CONTEXT
- **Mục tiêu cốt lõi:** Thêm automated test để enforce ADR-001 compliance trong CI pipeline
- **Nghiệp vụ áp dụng:** Architecture enforcement - detect architecture drift early

## 2. ACTIVE WORKFLOW ROUTING
- **Target Workflow:** `.devin/workflows/newfeaturebuild.md`
- **Execution Mode:** IMPLEMENT (thêm tests, không sửa production code)

## 3. RELEVANT FILES (CONTEXT BOUNDARY)
- **Files được phép đọc/sửa:**
  - `docs/AI/project_state.md` (Bắt buộc đọc đầu phiên)
  - `docs/decisions/ADR-001-SQLite-Offline-First.md`
  - `6_Tests/VanAn.Architecture.Tests/ArchitectureRulesTests.cs`
  - `architecture-guard.ps1`
  - `docker-compose.prod.yml` (read-only cho validation)
- **Boundary Rules (Nghiêm cấm):**
  - KHÔNG sửa production deployment code trong Wave 1
  - KHÔNG sửa docker-compose.prod.yml trong Wave 1
  - CHỈ thêm tests, KHÔNG sửa production logic

## 4. TECHNICAL & REGULATORY CONSTRAINTS (HARDENING GATES)
- [ ] **ADR-001 Test Must Fail Initially:** Test SHOULD fail since architecture drift exists
- [ ] **No Production Code Changes:** Wave 1 chỉ thêm tests, không sửa deployment
- [ ] **CI Integration:** Test phải chạy trong CI pipeline (.github/workflows/ci.yml)
- [ ] **Architecture Test Pattern:** Sử dụng pattern existing trong ArchitectureRulesTests.cs
- [ ] **Guard Check Integration:** architecture-guard.ps1 phải check ADR-001

## 5. SUCCESS CRITERIA (ĐO LƯỜNG ĐƯỢC)
- [ ] **SC-1:** ADR-001 compliance test added to ArchitectureRulesTests.cs
- [ ] **SC-2:** Test validates docker-compose.prod.yml contains SQLite stations
- [ ] **SC-3:** Test validates docker-compose.prod.yml contains NATS sync workers
- [ ] **SC-4:** Test FAILS initially (confirms architecture drift detection works)
- [ ] **SC-5:** architecture-guard.ps1 updated to check ADR-001 compliance
- [ ] **SC-6:** CI pipeline (.github/workflows/ci.yml) runs ADR-001 test
- [ ] **SC-7:** All existing Architecture tests still pass (21/21)
- [ ] **SC-8:** Build: 0 errors
- [ ] **SC-9:** guard-check.ps1 passes with new ADR-001 check
- [ ] **SC-10:** Test documented with clear ADR-001 reference

**Implementation Date:** 2026-06-29
**Branch:** feature/adr001-wave1-ci-check

## 6. ACTIVE SKILLS (MAX 3)
- `test-system-upgrade` — Thêm ADR-001 compliance test vào architecture test suite
- `build-error-analysis` — Verify CI pipeline integration

## 7. AI HEALTH CHECK MATRIX (INITIAL)
- **Evidence Count:** 3
- **Verified Facts:**
  - Fact 1: ADR-001 approved 2026-06-01 (SQLite + NATS + PostgreSQL)
  - Fact 2: Current docker-compose.prod.yml uses PostgreSQL direct (no SQLite stations)
  - Fact 3: CI pipeline runs Architecture Tests in separate job
- **Assumptions:**
  - Assumption 1: Test should fail initially (architecture drift exists)
  - Assumption 2: CI pipeline has permission to run architecture tests
- **Open Questions:**
  - Q1: Should test be in ArchitectureRulesTests.cs or separate file?
  - Q2: Should architecture-guard.ps1 fail immediately on ADR-001 violation?
- **Recommended Action:** IMPLEMENT - Add ADR-001 compliance test to detect drift

## 8. REVERSE IMPACT ANALYSIS
| File thay đổi | Reverse impact | Mitigation |
|---|---|---|
| 6_Tests/VanAn.Architecture.Tests/ArchitectureRulesTests.cs | New test added, no production impact | Test-only change |
| architecture-guard.ps1 | New check added, may fail CI initially | Document expected failure in Wave 1 |
| .github/workflows/ci.yml | No change (Architecture Tests already run) | N/A |

## 9. TDD & E2E TESTING STRATEGY
- **Architecture Test Strategy:**
  - Test reads docker-compose.prod.yml
  - Validates SQLite station service exists
  - Validates NATS sync worker service exists
  - Test should FAIL initially (drift detection)
- **Test boundary:**
  - Unit tests: N/A (architecture validation)
  - Integration tests: N/A
  - E2E tests: N/A

## 10. JIT PLANNING + PURE EXECUTION (MICRO-PHASES)

### Chiến lược thực thi: JIT Planning + Pure Execution

Wave 1 là test-only wave, nên Planning phase đơn giản:
- S1: Review ADR-001 requirements → Design test assertions
- S2: Implement test → Verify FAIL initially
- S3: Update architecture-guard.ps1 → Verify integration

### Micro-phase breakdown cho Wave 1

| Session | JIT Planning (chốt gì) | Pure Execution (viết gì) |
|---|---|---|
| **S1** | Design ADR-001 test assertions (SQLite stations, NATS workers) | Add test method to ArchitectureRulesTests.cs |
| **S2** | Plan architecture-guard.ps1 integration | Update architecture-guard.ps1 with ADR-001 check |
| **S3** | Plan CI verification | Run CI workflow to verify ADR-001 test runs |

### Rules
- Test MUST fail initially (confirms drift detection)
- KHÔNG sửa production deployment code
- Test follows existing ArchitectureRulesTests.cs pattern

## 11. ESTIMATED EFFORT
- 1-2 hours (test-only wave)
- 1-2 sessions theo JIT Planning
- **BLOCKER:** None (test-only, low risk)