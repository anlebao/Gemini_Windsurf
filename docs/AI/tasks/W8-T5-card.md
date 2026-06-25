# TASK CARD: PRODUCTION_HYGIENE - WAVE8 - Test Authentication and Authorization

## 1. GOAL & CONTEXT
- **Mục tiêu cốt lõi:** Test authentication và authorization cho upgraded dashboard
- **Nghiệp vụ áp dụng:** Verification - ensure security features work correctly

## 2. ACTIVE WORKFLOW ROUTING
- **Target Workflow:** `Testing workflow`
- **Execution Mode:** FIX_ONLY

## 3. RELEVANT FILES (CONTEXT BOUNDARY)
- **Files được phép đọc/sửa:**
  - `docs/AI/project_state.md` (Bắt buộc đọc đầu phiên)
  - `VanAn_Dashboard.html` (test only)
  - `docs/AI/tasks/PRODUCTION_HYGIENE_master_plan.md` (cập nhật status)
- **Boundary Rules (Nghiêm cấm):**
  - KHÔNG sửa authentication logic (đã làm trong W8-T2, W8-T3)
  - KHÔNG sửa sitemap UI (đã làm trong W8-T4)
  - Chỉ test và report

## 4. TECHNICAL & REGULATORY CONSTRAINTS (HARDENING GATES)
- [ ] **Authentication Test:** Verify login flow works correctly
- [ ] **Authorization Test:** Verify role-based access control works
- [ ] **Session Test:** Verify session management works
- [ ] **Security Test:** Verify unauthorized access blocked
- [ ] **Documentation:** Document test results

## 5. SUCCESS CRITERIA (ĐO LƯỜNG ĐƯỢC)
- [ ] **SC1:** Login flow tested and working
- [ ] **SC2:** Authorization tested and working
- [ ] **SC3:** Session management tested and working
- [ ] **SC4:** Unauthorized access tested and blocked
- [ ] **SC5:** Test results documented
- [ ] **SC6:** PRODUCTION_HYGIENE_master_plan.md updated với W8-T5 status = ✅ DONE

**Implementation Date:** 2026-06-24
**Branch:** feature/wave8-upgrade-dashboard

## 6. ACTIVE SKILLS (MAX 3)
- `build-error-analysis` — Verify implementation works correctly
- `pattern-based-fixing` — Follow existing test patterns

## 7. AI HEALTH CHECK MATRIX (INITIAL)
- **Evidence Count:** 2
- **Verified Facts:**
  - Fact 1: Authentication implemented in W8-T2
  - Fact 2: Authorization implemented in W8-T3
- **Assumptions:**
  - Features work as designed
- **Open Questions:**
  - Q1: Có test failures không?
- **Recommended Action:** Test authentication and authorization thoroughly

## 8. REVERSE IMPACT ANALYSIS
| File thay đổi | Reverse impact | Mitigation |
|---|---|---|
| N/A (test only) | Không có reverse impact | N/A |

## 9. TDD & E2E TESTING STRATEGY
- **Test Strategy:** Manual smoke test for authentication and authorization
- **Test boundary:**
  - Unit tests: N/A
  - Integration tests: N/A
  - E2E tests: N/A

## 10. JIT PLANNING + PURE EXECUTION (MICRO-PHASES

### Chiến lược thực thi: JIT Planning + Pure Execution

Test authentication and authorization, document results.

### Micro-phase breakdown cho WAVE8 - Test Authentication

| Session | JIT Planning (chốt gì) | Pure Execution (viết gì) |
|---|---|---|
| **S1** | Test login flow | Manual test login with valid credentials |
| **S2** | Test authorization | Manual test access with different roles |
| **S3** | Test session management | Manual test session timeout and auto-logout |
| **S4** | Test unauthorized access | Manual test access without authentication |
| **S5** | Document and update | Document test results, update master plan status |

### Rules
- Test all authentication and authorization flows
- Document any issues found
- Update documentation immediately after completion

## 11. ESTIMATED EFFORT
- Low effort - manual testing
- 2 sessions theo JIT Planning
- **BLOCKER:** Không có blockers
