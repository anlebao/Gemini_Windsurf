# TASK CARD: PRODUCTION_HYGIENE - WAVE8 - Implement Role-Based Authorization Checks

## 1. GOAL & CONTEXT
- **Mục tiêu cốt lõi:** Implement role-based authorization checks cho dashboard modules
- **Nghiệp vụ áp dụng:** Security enhancement - add authorization to sitemap

## 2. ACTIVE WORKFLOW ROUTING
- **Target Workflow:** `Authorization implementation workflow`
- **Execution Mode:** FIX_ONLY

## 3. RELEVANT FILES (CONTEXT BOUNDARY)
- **Files được phép đọc/sửa:**
  - `docs/AI/project_state.md` (Bắt buộc đọc đầu phiên)
  - `VanAn_Dashboard.html` (implement authorization checks)
  - `docs/AI/tasks/PRODUCTION_HYGIENE_master_plan.md` (cập nhật status)
- **Boundary Rules (Nghiêm cấm):**
  - KHÔNG sửa ShopERP RBAC logic
  - KHÔNG sửa Gateway authorization
  - Chỉ implement client-side authorization checks

## 4. TECHNICAL & REGULATORY CONSTRAINTS (HARDENING GATES)
- [ ] **Role-Based Access:** Implement role-based access control cho từng module
- [ ] **JWT Claims:** Extract roles from JWT token
- [ ] **Module Permissions:** Define permissions cho KhachLink, ShopERP, Account, EInvoice, LoyaltyReward
- [ ] **Access Denied:** Implement access denied handling
- [ ] **Security First:** Ensure authorization checks cannot be bypassed

## 5. SUCCESS CRITERIA (ĐO LƯỜNG ĐƯỢC)
- [ ] **SC1:** Role-based authorization implemented
- [ ] **SC2:** JWT claims extraction implemented
- [ ] **SC3:** Module permissions defined
- [ ] **SC4:** Access denied handling implemented
- [ ] **SC5:** PRODUCTION_HYGIENE_master_plan.md updated với W8-T3 status = ✅ DONE

**Implementation Date:** 2026-06-24
**Branch:** feature/wave8-upgrade-dashboard

## 6. ACTIVE SKILLS (MAX 3)
- `pattern-based-fixing` — Follow existing RBAC patterns
- `build-error-analysis` — Verify authorization works correctly

## 7. AI HEALTH CHECK MATRIX (INITIAL)
- **Evidence Count:** 2
- **Verified Facts:**
  - Fact 1: System has RBAC (Waves 4-6 completed)
  - Fact 2: JWT tokens contain role claims
- **Assumptions:**
  - Can extract roles from JWT token
- **Open Questions:**
  - Q1: Role claim name là gì?
  - Q2: Permissions mapping là gì?
- **Recommended Action:** Implement role-based authorization checks

## 8. REVERSE IMPACT ANALYSIS
| File thay đổi | Reverse impact | Mitigation |
|---|---|---|
| VanAn_Dashboard.html (implement auth) | Modules will require authorization | Add access denied UI |

## 9. TDD & E2E TESTING STRATEGY
- **Verification Strategy:** Manual smoke test for authorization
- **Test boundary:**
  - Unit tests: N/A
  - Integration tests: N/A
  - E2E tests: N/A

## 10. JIT PLANNING + PURE EXECUTION (MICRO-PHASES

### Chiến lược thực thi: JIT Planning + Pure Execution

Implement role-based authorization checks, verify access control.

### Micro-phase breakdown cho WAVE8 - Implement Authorization

| Session | JIT Planning (chốt gì) | Pure Execution (viết gì) |
|---|---|---|
| **S1** | Investigate JWT role claims | Check JWT token structure and role claim name |
| **S2** | Define module permissions | Document permissions for each module |
| **S3** | Implement authorization checks | Add role-based access control logic |
| **S4** | Implement access denied handling | Add access denied UI |
| **S5** | Test and update documentation | Manual smoke test, update master plan status |

### Rules
- Follow existing RBAC patterns
- Ensure authorization cannot be bypassed
- Update documentation immediately after completion

## 11. ESTIMATED EFFORT
- Medium effort - authorization implementation
- 4 sessions theo JIT Planning
- **BLOCKER:** Không có blockers
