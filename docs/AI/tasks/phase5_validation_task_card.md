# TASK CARD: ARCHITECTURE - PHASE 5 - Validation & E2E Testing

## 1. GOAL & CONTEXT
- **Mục tiêu cốt lõi:** Comprehensive validation of all architecture changes across all environments
- **Nghiệp vụ áp dụng:** Ensure architecture refactor is production-ready with no regressions

## 2. ACTIVE WORKFLOW ROUTING
- **Target Workflow:** `review.md` (REVIEW_ONLY mode)
- **Execution Mode:** REVIEW_ONLY

## 3. RELEVANT FILES (CONTEXT BOUNDARY)
- **Files được phép đọc:**
  - `docs/AI/project_state.md` (Bắt buộc đọc đầu phiên)
  - `docs/AI/tasks/architecture_refactor_master_plan.md` (Reference - master plan)
  - `6_Testing/e2e-tests/omnichannel-order-lifecycle.spec.ts` (Reference - E2E test)
  - CI/CD pipeline logs (Read-only - verify pipeline status)
  - Service logs (Read-only - verify service health)
- **Boundary Rules (Nghiêm cấm):**
  - KHÔNG sửa any files (REVIEW_ONLY mode)
  - KHÔNG modify any configurations
  - KHÔNG deploy to production
  - KHÔNG bypass validation steps
  - KHÔNG approve changes without testing

## 4. TECHNICAL & REGULATORY CONSTRAINTS (HARDENING GATES)
- [ ] **Validation Completeness:** Must validate all environments (Local, Staging, Edge)
- [ ] **E2E Test Coverage:** Must run full E2E test suite
- [ ] **Performance Baseline:** Must ensure no performance regression
- [ ] **Security Validation:** Must ensure no security vulnerabilities
- [ ] **Rollback Readiness:** Must verify rollback plan works

## 5. SUCCESS CRITERIA (ĐO LƯỜNG ĐƯỢC)
- [ ] **SC1:** CI pipeline passes (all jobs)
- [ ] **SC2:** Staging deployment successful
- [ ] **SC3:** E2E tests pass (omnichannel flow)
- [ ] **SC4:** Local development works smoothly
- [ ] **SC5:** Edge deployment works correctly
- [ ] **SC6:** No performance regression (>20%)
- [ ] **SC7:** No security issues
- [ ] **SC8:** Documentation complete
- [ ] **SC9:** Rollback plan tested
- [ ] **SC10:** Ready for production deployment
- [ ] **SC11:** All health checks pass
- [ ] **SC12:** Zero critical bugs

**Implementation Date:** 2026-06-30
**Branch:** feature/architecture-refactor-phase5-validation

## 6. ACTIVE SKILLS (MAX 3)
- `domain-integrity-validation` — Ensure validation doesn't miss domain violations
- `system-refactor-safety` — Ensure validation catches all regressions
- `pattern-based-fixing` — Apply consistent validation patterns

## 7. AI HEALTH CHECK MATRIX (INITIAL)
- **Evidence Count:** 4
- **Verified Facts:**
  - Fact 1: All previous phases (1-4) complete
  - Fact 2: Architecture changes implemented across all environments
  - Fact 3: E2E test suite available for validation
  - Fact 4: Rollback plan documented
- **Assumptions:**
  - [ASSUMPTION_1] Staging environment is available
  - [ASSUMPTION_2] E2E test suite is up-to-date
  - [ASSUMPTION_3] Performance baseline exists
- **Open Questions:**
  - Q1: Are there any environment-specific issues?
  - Q2: Will E2E tests need updates for new architecture?
  - Q3: Are there any performance regressions?
- **Recommended Action:** Execute comprehensive validation before production deployment

## 8. REVERSE IMPACT ANALYSIS
| Component | Validation impact | Mitigation |
|---|---|---|
| CI pipeline | Verify pipeline integrity | Monitor pipeline logs |
| Staging deployment | Verify deployment works | Test thoroughly |
| E2E tests | Verify test coverage | Update tests if needed |
| Local development | Verify dev experience | Test local workflows |
| Edge deployment | Verify edge capabilities | Test edge features |

## 9. TDD & E2E TESTING STRATEGY
- **CI Pipeline Validation:**
  - Verify all CI jobs pass
  - Verify build times acceptable
  - Verify no pipeline errors
- **Staging Validation:**
  - Verify deployment successful
  - Verify all services healthy
  - Verify no deployment errors
- **E2E Test Validation:**
  - Run full E2E test suite
  - Verify omnichannel flow works
  - Verify no test failures
- **Performance Validation:**
  - Measure response times
  - Compare with baseline
  - Identify regressions
- **Security Validation:**
  - Scan for vulnerabilities
  - Verify authentication works
  - Verify authorization works
- **Test boundary:**
  - Unit tests: Not needed (validation phase)
  - Integration tests: Environment-specific testing
  - E2E tests: Full E2E test suite

## 10. JIT PLANNING + PURE EXECUTION (MICRO-PHASES)

### Chiến lược thực thi: JIT Planning + Pure Execution

Phase 5 is REVIEW_ONLY mode - no code changes. Comprehensive validation only.

### Micro-phase breakdown cho Phase 5 (Validation & E2E Testing)

| Session | JIT Planning (chốt gì) | Pure Execution (viết gì) |
|---|---|---|
| **S1** | Plan CI pipeline validation, plan staging deployment validation, plan E2E test execution, plan performance measurement strategy | Run CI pipeline validation, deploy to staging, run E2E tests, measure performance metrics |
| **S2** | Plan security validation, plan edge deployment validation, plan final documentation, plan production readiness assessment | Run security validation, test edge deployment, compile validation report, document findings, assess production readiness |

### Rules
- [RULE_1] REVIEW_ONLY mode - no code changes
- [RULE_2] Must validate all environments
- [RULE_3] Must document all findings
- [RULE_4] Must not approve if critical issues found

## 11. ESTIMATED EFFORT
- 2-3 sessions (2-3 hours per session)
- Total: 4-9 hours
- **BLOCKER:** Previous phases completion (all phases 1-4 must be complete)