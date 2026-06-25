# TASK CARD: PRODUCTION_HYGIENE - WAVE11 - Verify Deleted File References

## 1. GOAL & CONTEXT
- **Mục tiêu cốt lõi:** Verify không có references đến deleted files trong codebase
- **Nghiệp vụ áp dụng:** Safety verification sau khi xóa demo files

## 2. ACTIVE WORKFLOW ROUTING
- **Target Workflow:** `Simple verification workflow`
- **Execution Mode:** FIX_ONLY

## 3. RELEVANT FILES (CONTEXT BOUNDARY)
- **Files được phép đọc/sửa:**
  - `docs/AI/project_state.md` (Bắt buộc đọc đầu phiên)
  - Tất cả files trong repository (grep search only)
- **Boundary Rules (Nghiêm cấm):**
  - KHÔNG sửa bất kỳ file nào - chỉ đọc để verify
  - KHÔNG xóa hoặc sửa files trong task này (đã làm trong W11-T1, W11-T2, W11-T3)

## 4. TECHNICAL & REGULATORY CONSTRAINTS (HARDENING GATES)
- [ ] **Comprehensive Search:** Grep search toàn repository cho references
- [ ] **Pattern Matching:** Search cho file names, paths, và usage patterns
- [ ] **Documentation Check:** Check documentation files for references
- [ ] **Safety First:** Report any references found before proceeding

## 5. SUCCESS CRITERIA (ĐO LƯỜNG ĐƯỢC)
- [ ] **SC1:** Grep search hoàn thành cho toàn repository
- [ ] **SC2:** Không có code references đến deleted files
- [ ] **SC3:** Không có documentation references to deleted files
- [ ] **SC4:** Verification report documented

**Implementation Date:** 2026-06-24
**Branch:** feature/wave11-cleanup-invalid-files

## 6. ACTIVE SKILLS (MAX 3)
- `build-error-analysis` — Analyze search results for potential issues
- `domain-integrity-validation` — Verify no broken dependencies

## 7. AI HEALTH CHECK MATRIX (INITIAL)
- **Evidence Count:** 2
- **Verified Facts:**
  - Fact 1: Demo files deleted in W11-T1, W11-T2, W11-T3
  - Fact 2: Need to verify no broken references
- **Assumptions:**
  - No production references expected
- **Open Questions:**
  - Q1: Are there any documentation references?
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

Comprehensive grep search to verify no references exist after file deletion.

### Micro-phase breakdown cho WAVE11 - Verify Deleted File References

| Session | JIT Planning (chốt gì) | Pure Execution (viết gì) |
|---|---|---|
| **S1** | Execute comprehensive grep search | Run grep commands, analyze results, document findings |

### Rules
- Search for file names, paths, and variations
- Search in all file types (.cs, .razor, .cshtml, .md, .json, etc.)
- Document all findings even if none found

## 11. ESTIMATED EFFORT
- Very low effort - grep search only
- 1 session theo JIT Planning
- **BLOCKER:** Không có blockers
