# TASK CARD: PRODUCTION_HYGIENE - WAVE8 - Update Documentation

## 1. GOAL & CONTEXT
- **Mục tiêu cốt lõi:** Update documentation nếu có references đến dashboard
- **Nghiệp vụ áp dụng:** Documentation maintenance

## 2. ACTIVE WORKFLOW ROUTING
- **Target Workflow:** `Simple documentation update workflow`
- **Execution Mode:** FIX_ONLY

## 3. RELEVANT FILES (CONTEXT BOUNDARY)
- **Files được phép đọc/sửa:**
  - `docs/AI/project_state.md` (Bắt buộc đọc đầu phiên)
  - Documentation files (.md) nếu có references
- **Boundary Rules (Nghiêm cấm):**
  - KHÔNG sửa code files
  - KHÔNG sửa configuration files
  - Chỉ update documentation nếu cần thiết

## 4. TECHNICAL & REGULATORY CONSTRAINTS (HARDENING GATES)
- [ ] **Minimal Changes:** Chỉ update documentation nếu thực sự có references
- [ ] **Accuracy:** Đảm bảo documentation cập nhật đúng
- [ ] **Consistency:** Đảm bảo documentation consistent với codebase state

## 5. SUCCESS CRITERIA (ĐO LƯỜNG ĐƯỢC)
- [ ] **SC1:** Documentation updated nếu có references
- [ ] **SC2:** Không có outdated references to dashboard
- [ ] **SC3:** Documentation accurate and consistent

**Implementation Date:** 2026-06-24
**Branch:** feature/wave8-cleanup-dashboard

## 6. ACTIVE SKILLS (MAX 3)
- N/A - simple documentation task

## 7. AI HEALTH CHECK MATRIX (INITIAL)
- **Evidence Count:** 1
- **Verified Facts:**
  - Fact 1: Dashboard deletion may require documentation updates
- **Assumptions:**
  - Documentation may have references
- **Open Questions:**
  - Q1: Are there documentation references?
- **Recommended Action:** Check and update if needed

## 8. REVERSE IMPACT ANALYSIS
| File thay đổi | Reverse impact | Mitigation |
|---|---|---|
| Documentation files (nếu có) | Không có reverse impact | N/A |

## 9. TDD & E2E TESTING STRATEGY
- **Test boundary:**
  - Unit tests: N/A
  - Integration tests: N/A
  - E2E tests: N/A

## 10. JIT PLANNING + PURE EXECUTION (MICRO-PHASES)

### Chiến lược thực thi: JIT Planning + Pure Execution

Check documentation for references and update if needed.

### Micro-phase breakdown cho WAVE8 - Update Documentation

| Session | JIT Planning (chốt gì) | Pure Execution (viết gì) |
|---|---|---|
| **S1** | Check documentation for references | Grep search in .md files, update if needed |

### Rules
- Only update if references found
- Keep documentation accurate

## 11. ESTIMATED EFFORT
- Very low effort - conditional update
- 1 session theo JIT Planning
- **BLOCKER:** Không có blockers
