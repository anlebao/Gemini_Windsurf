# TASK CARD: ARCHITECTURE - PHASE 0 - Validation Layer Enhancement (BLOCKING)

## 1. GOAL & CONTEXT
- **Mục tiêu cốt lõi:** Enhance architecture validation layer to detect future architecture mismatches
- **Nghiệp vụ áp dụng:** Prevent CoreHub vs docker-compose mismatch type issues from recurring

## 2. ACTIVE WORKFLOW ROUTING
- **Target Workflow:** `newfeaturebuild.md` (7-step ANALYZE → IMPLEMENT)
- **Execution Mode:** ANALYZE → IMPLEMENT

## 3. RELEVANT FILES (CONTEXT BOUNDARY)
- **Files được phép đọc/sửa:**
  - `docs/AI/project_state.md` (Bắt buộc đọc đầu phiên)
  - `6_Tests/VanAn.Architecture.Tests/ArchitectureConsistencyTests.cs` (New file to create)
  - `6_Tests/VanAn.Architecture.Tests/ArchitectureTests.cs` (Reference - existing tests)
  - `scripts/validate-docker-compose.ps1` (New file to create)
  - `scripts/validate-env-vars.ps1` (New file to create)
  - `6_Tests/VanAn.Integration.Tests/GatewayStartupTests.cs` (Enhance)
  - `6_Tests/VanAn.Integration.Tests/KhachLinkStartupTests.cs` (Enhance)
  - `.github/workflows/ci.yml` (Add validation job)
  - `.github/workflows/cd.yml` (Add pre-deployment validation)
- **Boundary Rules (Nghiêm cấm):**
  - KHÔNG sửa application code (only tests and validation scripts)
  - KHÔNG sửa docker-compose files (Phase 2)
  - KHÔNG sửa CI/CD deployment logic (Phase 3)
  - KHÔNG modify production configuration
  - KHÔNG bypass existing tests

## 4. TECHNICAL & REGULATORY CONSTRAINTS (HARDENING GATES)
- [ ] **Test-Only Changes:** Phase 0 chỉ thêm tests và validation, không sửa production code
- [ ] **CI Pipeline Must Pass:** All new validations must pass in CI
- [ ] **No Breaking Changes:** Existing tests must continue to pass
- [ ] **Backward Compatible:** Validation scripts must work with existing config
- [ ] **Documentation:** All validation rules must be documented

## 5. SUCCESS CRITERIA (ĐO LƯỜNG ĐƯỢC)
- [ ] **SC1:** ArchitectureConsistencyTests.cs created with code vs docker-compose validation
- [ ] **SC2:** Docker compose validation script created and working
- [ ] **SC3:** Environment variable validation script created and working
- [ ] **SC4:** Startup tests enhanced with architecture validation
- [ ] **SC5:** CI job for docker-compose validation added and passing
- [ ] **SC6:** Pre-deployment validation added to CD pipeline
- [ ] **SC7:** All validations tested and passing
- [ ] **SC8:** Documentation updated
- [ ] **SC9:** CI pipeline passes with new validations
- [ ] **SC10:** Existing tests still pass (no regressions)
- [ ] **SC11:** Validation detects CoreHub vs docker-compose mismatch
- [ ] **SC12:** Ready to proceed with Phase 1

**Implementation Date:** 2026-06-30
**Branch:** feature/architecture-refactor-phase0-validation

## 6. ACTIVE SKILLS (MAX 3)
- `domain-integrity-validation` — Ensure validation rules don't violate domain logic
- `system-refactor-safety` — Ensure safe addition of validation layer
- `pattern-based-fixing` — Apply consistent patterns to validation scripts

## 7. AI HEALTH CHECK MATRIX (INITIAL)
- **Evidence Count:** 5
- **Verified Facts:**
  - Fact 1: Current architecture tests only validate code structure
  - Fact 2: Startup tests only validate DI and health
  - Fact 3: CI/CD pipeline has no docker-compose validation
  - Fact 4: CoreHub vs docker-compose mismatch was not detected
  - Fact 5: No cross-layer validation exists
- **Assumptions:**
  - [ASSUMPTION_1] Validation scripts can parse docker-compose.yml
  - [ASSUMPTION_2] Architecture consistency can be validated programmatically
  - [ASSUMPTION_3] CI pipeline can accommodate new validation jobs
- **Open Questions:**
  - Q1: Should validation fail CI pipeline or just warn?
  - Q2: How to validate CoreHub background service vs HTTP service config?
  - Q3: Should validation be blocking or non-blocking?
- **Recommended Action:** Implement as blocking validation to prevent future issues

## 8. REVERSE IMPACT ANALYSIS
| File thay đổi | Reverse impact | Mitigation |
|---|---|---|
| 6_Tests/VanAn.Architecture.Tests/ArchitectureConsistencyTests.cs | New test file, no impact | Add to CI architecture tests |
| scripts/validate-docker-compose.ps1 | New validation script | Test thoroughly before use |
| scripts/validate-env-vars.ps1 | New validation script | Test thoroughly before use |
| GatewayStartupTests.cs | Enhanced with architecture validation | Ensure no test regressions |
| KhachLinkStartupTests.cs | Enhanced with architecture validation | Ensure no test regressions |
| .github/workflows/ci.yml | New validation job | Test CI pipeline |
| .github/workflows/cd.yml | Pre-deployment validation | Test CD pipeline |

## 9. TDD & E2E TESTING STRATEGY
- **Architecture Consistency Testing:**
  - Test code vs docker-compose consistency
  - Test environment variable consistency
  - Test container dependency consistency
- **Validation Script Testing:**
  - Test docker-compose validation script
  - Test environment variable validation script
  - Test error handling and edge cases
- **Integration Testing:**
  - Test CI pipeline with new validation job
  - Test CD pipeline with pre-deployment validation
- **Test boundary:**
  - Unit tests: Architecture consistency tests
  - Integration tests: CI/CD pipeline validation
  - E2E tests: Not needed (validation phase only)

## 10. JIT PLANNING + PURE EXECUTION (MICRO-PHASES)

### Chiến lược thực thi: JIT Planning + Pure Execution

Phase 0 is test-only, low-risk, but BLOCKING. Must complete before any architecture changes.

### Micro-phase breakdown cho Phase 0 (Validation Layer Enhancement)

| Session | JIT Planning (chốt gì) | Pure Execution (viết gì) |
|---|---|---|
| **S1** | Analyze current architecture tests, plan ArchitectureConsistencyTests.cs structure, plan validation script requirements, plan startup test enhancements | Create ArchitectureConsistencyTests.cs, implement docker-compose validation script, implement env var validation script, enhance startup tests |
| **S2** | Plan CI job for docker-compose validation, plan pre-deployment validation in CD, plan testing strategy, plan documentation | Add CI job to ci.yml, add pre-deployment validation to cd.yml, test all validations, run CI pipeline, document validation rules, verify all tests pass |

### Rules
- [RULE_1] Test-only changes - no production code modifications
- [RULE_2] All validations must pass in CI
- [RULE_3] Existing tests must continue to pass
- [RULE_4] Must document all validation rules

## 11. ESTIMATED EFFORT
- 2-3 sessions (2-3 hours per session)
- Total: 4-9 hours
- **BLOCKER:** None (test-only, low risk)