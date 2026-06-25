# TASK CARD: PRODUCTION_HYGIENE - WAVE8 - Implement Login Page with JWT Authentication

## 1. GOAL & CONTEXT
- **Mục tiêu cốt lõi:** Implement login page với JWT authentication cho VanAn_Dashboard.html
- **Nghiệp vụ áp dụng:** Security enhancement - add authentication to dashboard

## 2. ACTIVE WORKFLOW ROUTING
- **Target Workflow:** `Authentication implementation workflow`
- **Execution Mode:** FIX_ONLY

## 3. RELEVANT FILES (CONTEXT BOUNDARY)
- **Files được phép đọc/sửa:**
  - `docs/AI/project_state.md` (Bắt buộc đọc đầu phiên)
  - `VanAn_Dashboard.html` (implement login page)
  - `docs/AI/tasks/PRODUCTION_HYGIENE_master_plan.md` (cập nhật status)
- **Boundary Rules (Nghiêm cấm):**
  - KHÔNG sửa ShopERP authentication logic
  - KHÔNG sửa Gateway authentication
  - Chỉ implement client-side login page

## 4. TECHNICAL & REGULATORY CONSTRAINTS (HARDENING GATES)
- [ ] **JWT Integration:** Integrate với ShopERP JWT authentication
- [ ] **Login Form:** Implement username/password login form
- [ ] **Token Storage:** Store JWT token securely (localStorage or sessionStorage)
- [ ] **Session Management:** Implement session check and auto-logout
- [ ] **Error Handling:** Add proper error handling cho login failures

## 5. SUCCESS CRITERIA (ĐO LƯỜNG ĐƯỢC)
- [ ] **SC1:** Login page implemented in VanAn_Dashboard.html
- [ ] **SC2:** JWT authentication integrated với ShopERP
- [ ] **SC3:** Token storage implemented securely
- [ ] **SC4:** Session management implemented
- [ ] **SC5:** Error handling implemented
- [ ] **SC6:** PRODUCTION_HYGIENE_master_plan.md updated với W8-T2 status = ✅ DONE

**Implementation Date:** 2026-06-24
**Branch:** feature/wave8-upgrade-dashboard

## 6. ACTIVE SKILLS (MAX 3)
- `pattern-based-fixing` — Follow existing authentication patterns
- `build-error-analysis` — Verify implementation works correctly

## 7. AI HEALTH CHECK MATRIX (INITIAL)
- **Evidence Count:** 2
- **Verified Facts:**
  - Fact 1: ShopERP has JWT authentication (Wave 0 completed)
  - Fact 2: Login endpoint exists in ShopERP
- **Assumptions:**
  - Can use existing ShopERP login endpoint
- **Open Questions:**
  - Q1: Login endpoint URL là gì?
  - Q2: Token format là gì?
- **Recommended Action:** Implement login page with ShopERP JWT integration

## 8. REVERSE IMPACT ANALYSIS
| File thay đổi | Reverse impact | Mitigation |
|---|---|---|
| VanAn_Dashboard.html (implement login) | Dashboard sẽ require authentication | Add fallback for unauthenticated users |

## 9. TDD & E2E TESTING STRATEGY
- **Verification Strategy:** Manual smoke test for login flow
- **Test boundary:**
  - Unit tests: N/A
  - Integration tests: N/A
  - E2E tests: N/A

## 10. JIT PLANNING + PURE EXECUTION (MICRO-PHASES

### Chiến lược thực thi: JIT Planning + Pure Execution

Implement login page with JWT authentication, verify login flow.

### Micro-phase breakdown cho WAVE8 - Implement Login Page

| Session | JIT Planning (chốt gì) | Pure Execution (viết gì) |
|---|---|---|
| **S1** | Investigate ShopERP login endpoint | Check ShopERP login endpoint URL and format |
| **S2** | Implement login form UI | Add login form to VanAn_Dashboard.html |
| **S3** | Implement JWT authentication logic | Add JWT token handling and storage |
| **S4** | Implement session management | Add session check and auto-logout |
| **S5** | Test and update documentation | Manual smoke test, update master plan status |

### Rules
- Follow existing ShopERP authentication patterns
- Store token securely
- Update documentation immediately after completion

## 11. ESTIMATED EFFORT
- Medium effort - authentication implementation
- 4 sessions theo JIT Planning
- **BLOCKER:** Không có blockers
