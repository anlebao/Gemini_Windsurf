# TASK CARD: PRODUCTION_HYGIENE - WAVE13 - Verify Template Data Acceptable

## 1. GOAL & CONTEXT
- **Mục tiêu cốt lõi:** Verify template data trong OnboardingController acceptable cho production
- **Nghiệp vụ áp dụng:** Data validation - determine if template data needs replacement

## 2. ACTIVE WORKFLOW ROUTING
- **Target Workflow:** `Data validation workflow`
- **Execution Mode:** INVESTIGATE_ONLY

## 3. RELEVANT FILES (CONTEXT BOUNDARY)
- **Files được phép đọc/sửa:**
  - `docs/AI/project_state.md` (Bắt buộc đọc đầu phiên)
  - `2_Gateway/Controllers/OnboardingController.cs` (read only)
  - `docs/AI/tasks/PRODUCTION_HYGIENE_master_plan.md` (cập nhật status)
- **Boundary Rules (Nghiêm cấm):**
  - KHÔNG sửa OnboardingController trong task này
  - KHÔNG sửa configuration files
  - Chỉ verify và báo cáo

## 4. TECHNICAL & REGULATORY CONSTRAINTS (HARDENING GATES)
- [ ] **Template Data Purpose:** Verify template data là cho onboarding purposes
- [ ] **Production Acceptable:** Determine if template data acceptable cho production
- [ ] **Static vs Dynamic:** Verify if data nên là static template hoặc dynamic
- [ ] **Documentation:** Document findings and recommendation

## 5. SUCCESS CRITERIA (ĐO LƯỜNG ĐƯỢC)
- [ ] **SC1:** OnboardingController template data analyzed
- [ ] **SC2:** Purpose of template data verified
- [ ] **SC3:** Production acceptability determined
- [ ] **SC4:** Recommendation documented (keep as-is hoặc replace)
- [ ] **SC5:** PRODUCTION_HYGIENE_master_plan.md updated với W13-T3 status = ✅ DONE

**Implementation Date:** 2026-06-24
**Branch:** feature/wave13-replace-hardcoded-data

## 6. ACTIVE SKILLS (MAX 3)
- `domain-integrity-validation` — Verify data flow patterns
- `pattern-based-fixing` — Analyze template data patterns

## 7. AI HEALTH CHECK MATRIX (INITIAL)
- **Evidence Count:** 1
- **Verified Facts:**
  - Fact 1: OnboardingController có template data
- **Assumptions:**
  - Template data có thể acceptable cho onboarding
- **Open Questions:**
  - Q1: Template data purpose là gì?
  - Q2: Template data acceptable cho production không?
- **Recommended Action:** Analyze OnboardingController template data, determine acceptability

## 8. REVERSE IMPACT ANALYSIS
| File thay đổi | Reverse impact | Mitigation |
|---|---|---|
| N/A (investigation only) | Không có reverse impact | N/A |

## 9. TDD & E2E TESTING STRATEGY
- **Investigation Strategy:** Read OnboardingController, analyze template data
- **Test boundary:**
  - Unit tests: N/A
  - Integration tests: N/A
  - E2E tests: N/A

## 10. JIT PLANNING + PURE EXECUTION (MICRO-PHASES)

### Chiến lược thực thi: JIT Planning + Pure Execution

Analyze OnboardingController template data, determine production acceptability.

### Micro-phase breakdown cho WAVE13 - Verify Template Data

| Session | JIT Planning (chốt gì) | Pure Execution (viết gì) |
|---|---|---|
| **S1** | Read OnboardingController template data | Analyze template data structure and purpose |
| **S2** | Determine production acceptability | Evaluate if template data acceptable for production |
| **S3** | Document recommendation | Document findings and recommendation |
| **S4** | Update documentation | Update master plan status |

### Rules
- Analyze template data purpose thoroughly
- Consider onboarding use case when evaluating acceptability
- Document clear recommendation (keep or replace)

## 11. ESTIMATED EFFORT
- Low effort - investigation and analysis
- 2 sessions theo JIT Planning
- **BLOCKER:** Không có blockers
