# TASK CARD: PRODUCTION_HYGIENE - WAVE12 - Audit Gateway API Authorization

## 1. GOAL & CONTEXT
- **Mục tiêu cốt lõi:** Audit tất cả API endpoints trong Gateway cho authorization
- **Nghiệp vụ áp dụng:** Security audit - verify all Gateway endpoints have proper authorization

## 2. ACTIVE WORKFLOW ROUTING
- **Target Workflow:** `Security audit workflow`
- **Execution Mode:** INVESTIGATE_ONLY

## 3. RELEVANT FILES (CONTEXT BOUNDARY)
- **Files được phép đọc/sửa:**
  - `docs/AI/project_state.md` (Bắt buộc đọc đầu phiên)
  - `2_Gateway/Controllers/*.cs` (audit only)
  - `docs/AI/tasks/PRODUCTION_HYGIENE_master_plan.md` (cập nhật status)
- **Boundary Rules (Nghiêm cấm):**
  - KHÔNG sửa authorization trong task này (đó là task W12-T3, W12-T4)
  - KHÔNG sửa configuration files
  - Chỉ audit và báo cáo

## 4. TECHNICAL & REGULATORY CONSTRAINTS (HARDENING GATES)
- [ ] **Comprehensive Audit:** Audit tất cả Controllers trong Gateway
- [ ] **Authorization Pattern:** Verify [Authorize] hoặc policy-based authorization
- [ ] **Public Endpoints:** Identify legitimate public endpoints (AllowAnonymous)
- [ ] **Security Gap Detection:** Identify endpoints thiếu authorization
- [ ] **Documentation:** Document audit findings

## 5. SUCCESS CRITERIA (ĐO LƯỜNG ĐƯỢC)
- [ ] **SC1:** Audit hoàn thành cho tất cả Gateway Controllers
- [ ] **SC2:** Danh sách endpoints với authorization status documented
- [ ] **SC3:** Identify endpoints thiếu authorization (nếu có)
- [ ] **SC4:** Identify legitimate public endpoints (nếu có)
- [ ] **SC5:** Audit report documented
- [ ] **SC6:** PRODUCTION_HYGIENE_master_plan.md updated với W12-T1 status = ✅ DONE

**Implementation Date:** 2026-06-24
**Branch:** feature/wave12-api-authorization

## 6. ACTIVE SKILLS (MAX 3)
- `security-audit` — Comprehensive authorization audit
- `pattern-based-fixing` — Identify authorization patterns
- `domain-integrity-validation` — Verify security patterns consistent

## 7. AI HEALTH CHECK MATRIX (INITIAL)
- **Evidence Count:** 2
- **Verified Facts:**
  - Fact 1: Gateway có nhiều Controllers (OrdersController, OnboardingController, etc.)
  - Fact 2: Voice note endpoint thiếu authorization
- **Assumptions:**
  - Gateway endpoints should have authorization
- **Open Questions:**
  - Q1: Có bao nhiêu endpoints thiếu authorization?
  - Q2: Có legitimate public endpoints không?
- **Recommended Action:** Comprehensive audit of all Gateway Controllers

## 8. REVERSE IMPACT ANALYSIS
| File thay đổi | Reverse impact | Mitigation |
|---|---|---|
| N/A (audit only) | Không có reverse impact | N/A |

## 9. TDD & E2E TESTING STRATEGY
- **Audit Strategy:** Read all Gateway Controllers, analyze authorization attributes
- **Test boundary:**
  - Unit tests: N/A
  - Integration tests: N/A
  - E2E tests: N/A

## 10. JIT PLANNING + PURE EXECUTION (MICRO-PHASES)

### Chiến lược thực thi: JIT Planning + Pure Execution

Comprehensive audit of all Gateway Controllers for authorization status.

### Micro-phase breakdown cho WAVE12 - Audit Gateway Authorization

| Session | JIT Planning (chốt gì) | Pure Execution (viết gì) |
|---|---|---|
| **S1** | List all Gateway Controllers | Find all Controllers in 2_Gateway/Controllers/ |
| **S2** | Audit each Controller for authorization | Read each Controller, analyze [Authorize] attributes |
| **S3** | Document audit findings | Create audit report with authorization status |
| **S4** | Update documentation | Update master plan status |

### Rules
- Audit all Controllers comprehensively
- Document both authorized and unauthorized endpoints
- Identify legitimate public endpoints (AllowAnonymous)

## 11. ESTIMATED EFFORT
- Medium effort - comprehensive audit of multiple Controllers
- 3 sessions theo JIT Planning
- **BLOCKER:** Không có blockers
