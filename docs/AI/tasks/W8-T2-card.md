# TASK CARD: PRODUCTION_HYGIENE - WAVE8 - Verify Dashboard References

## 1. GOAL & CONTEXT
- **Mục tiêu cốt lõi:** Verify không có references đến VanAn_Dashboard.html trong codebase
- **Nghiệp vụ áp dụng:** Safety verification trước khi xóa file

## 2. ACTIVE WORKFLOW ROUTING
- **Target Workflow:** `Simple verification workflow`
- **Execution Mode:** FIX_ONLY

## 3. RELEVANT FILES (CONTEXT BOUNDARY)
- **Files được phép đọc/sửa:**
  - `docs/AI/project_state.md` (Bắt buộc đọc đầu phiên)
  - Tất cả files trong repository (grep search only)
- **Boundary Rules (Nghiêm cấm):**
  - KHÔNG sửa bất kỳ file nào - chỉ đọc để verify
  - KHÔNG xóa file trong task này (đó là task W8-T1)

## 4. TECHNICAL & REGULATORY CONSTRAINTS (HARDENING GATES)
- [ ] **Comprehensive Search:** Grep search toàn repository cho references
- [ ] **Pattern Matching:** Search cho file name, path, và usage patterns
- [ ] **Documentation Check:** Check documentation files for references
- [ ] **Configuration Check:** Check config files for references
- [ ] **Safety First:** Không xóa file nếu có references found

## 5. SUCCESS CRITERIA (ĐO LƯỜNG ĐƯỢC)
- [ ] **SC1:** Grep search hoàn thành cho toàn repository
- [ ] **SC2:** Không có code references đến VanAn_Dashboard.html
- [ ] **SC3:** Không có documentation references đến VanAn_Dashboard.html
- [ ] **SC4:** Không có configuration references to VanAn_Dashboard.html
- [ ] **SC5:** Verification report documented

**Implementation Date:** 2026-06-24
**Branch:** feature/wave8-cleanup-dashboard

## 6. ACTIVE SKILLS (MAX 3)
- `build-error-analysis` — Analyze search results for potential issues
- `domain-integrity-validation` — Verify no broken dependencies

## 7. AI HEALTH CHECK MATRIX (INITIAL)
- **Evidence Count:** 2
- **Verified Facts:**
  - Fact 1: VanAn_Dashboard.html exists in root directory
  - Fact 2: Dashboard is development-only tool
- **Assumptions:**
  - Dashboard not referenced in production code
- **Open Questions:**
  - Q1: Are there any script references?
  - Q2: Are there any documentation references?
- **Recommended Action:** Comprehensive grep search to verify

## 8. REVERSE IMPACT ANALYSIS
| File thay đổi | Reverse impact | Mitigation |
|---|---|---|
| N/A (read-only verification) | Không có reverse impact | N/A |

## 9. TDD & E2E TESTING STRATEGY
- **Verification Strategy:** Grep search across all file types
- **Test boundary:**
  - Unit tests: N/A
  - Integration tests: N/A
  - E2E tests: N/A

## 10. JIT PLANNING + PURE EXECUTION (MICRO-PHASES)

### Chiến lược thực thi: JIT Planning + Pure Execution

Comprehensive grep search to verify no references exist before file deletion.

### Micro-phase breakdown cho WAVE8 - Verify References

| Session | JIT Planning (chốt gì) | Pure Execution (viết gì) |
|---|---|---|
| **S1** | Execute comprehensive grep search | Run grep commands, analyze results, document findings |

### Rules
- Search for exact file name and variations
- Search in all file types (.cs, .razor, .cshtml, .md, .json, etc.)
- Document all findings even if none found

## 11. ESTIMATED EFFORT
- Very low effort - grep search only
- 1 session theo JIT Planning
- **BLOCKER:** Không có blockers
