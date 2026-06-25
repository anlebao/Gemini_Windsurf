# TASK CARD: PRODUCTION_HYGIENE - WAVE13 - Replace Hardcoded Data with Real API Calls

## 1. GOAL & CONTEXT
- **Mục tiêu cốt lõi:** Replace hardcoded data với real API calls (nếu cần)
- **Nghiệp vụ áp dụng:** Data implementation - replace remaining hardcoded data

## 2. ACTIVE WORKFLOW ROUTING
- **Target Workflow:** `Data implementation workflow`
- **Execution Mode:** FIX_ONLY

## 3. RELEVANT FILES (CONTEXT BOUNDARY)
- **Files được phép đọc/sửa:**
  - `docs/AI/project_state.md` (Bắt buộc đọc đầu phiên)
  - Files identified in W13-T1 audit (implement real API calls)
  - `docs/AI/tasks/PRODUCTION_HYGIENE_master_plan.md` (cập nhật status)
- **Boundary Rules (Nghiêm cấm):**
  - KHÔNG sửa Gateway Controllers hoặc CoreHub Services
  - KHÔNG sửa template data nếu acceptable (determined in W13-T3)
  - Chỉ implement client-side API calls

## 4. TECHNICAL & REGULATORY CONSTRAINTS (HARDENING GATES)
- [ ] **Real API Calls:** Replace hardcoded data với real API calls
- [ ] **Gateway Pattern:** Sử dụng HttpClient("gateway") pattern
- [ ] **Error Handling:** Add proper error handling cho API calls
- [ ] **Build Verification:** `dotnet build VanAn.sln` phải PASS sau khi sửa
- [ ] **Test Verification:** Integration tests phải PASS sau khi sửa

## 5. SUCCESS CRITERIA (ĐO LƯỜNG ĐƯỢC)
- [ ] **SC1:** Hardcoded data replaced với real API calls (cho files identified)
- [ ] **SC2:** TODO comments removed
- [ ] **SC3:** Error handling implemented cho API calls
- [ ] **SC4:** `dotnet build VanAn.sln` → 0 errors, 0 warnings mới
- [ ] **SC5:** `guard-check.ps1` → PASS
- [ ] **SC6:** `VanAn.Architecture.Tests`: 7/7 PASS
- [ ] **SC7:** `VanAn.Integration.Tests`: không có test nào bị break
- [ ] **SC8:** PRODUCTION_HYGIENE_master_plan.md updated với W13-T4 status = ✅ DONE
- [ ] **SC9:** Manual smoke: Verify pages load real data

**Implementation Date:** 2026-06-24
**Branch:** feature/wave13-replace-hardcoded-data

## 6. ACTIVE SKILLS (MAX 3)
- `pattern-based-fixing` — Follow existing API call patterns
- `build-error-analysis` — Verify build passes after changes
- `domain-integrity-validation` — Verify data flow correct

## 7. AI HEALTH CHECK MATRIX (INITIAL)
- **Evidence Count:** 2
- **Verified Facts:**
  - Fact 1: W13-T1 audit sẽ identify files với hardcoded data
  - Fact 2: W13-T3 sẽ determine template data acceptability
- **Assumptions:**
  - Có thể có nhiều files cần replacement
- **Open Questions:**
  - Q1: Có bao nhiêu files cần replacement?
  - Q2: API pattern nào phù hợp cho mỗi file?
- **Recommended Action:** Wait for audit results, then implement replacements

## 8. REVERSE IMPACT ANALYSIS
| File thay đổi | Reverse impact | Mitigation |
|---|---|---|
| Files with hardcoded data (implement API calls) | Pages sẽ phụ thuộc vào API | Add error handling, fallback |
| PRODUCTION_HYGIENE_master_plan.md (update status) | Không có reverse impact | Update task status to ✅ DONE |

## 9. TDD & E2E TESTING STRATEGY
- **Verification Strategy:** Manual smoke test for data loading
- **Test boundary:**
  - Unit tests: N/A
  - Integration tests: N/A
  - E2E tests: N/A

## 10. JIT PLANNING + PURE EXECUTION (MICRO-PHASES

### Chiến lược thực thi: JIT Planning + Pure Execution

Review audit results, implement real API calls, verify.

### Micro-phase breakdown cho WAVE13 - Replace Hardcoded Data

| Session | JIT Planning (chốt gì) | Pure Execution (viết gì) |
|---|---|---|
| **S1** | Review audit results | Analyze W13-T1 audit, W13-T3 findings, identify files to fix |
| **S2** | Implement real API calls | Replace hardcoded data with HttpClient calls |
| **S3** | Add error handling and verify | Add error handling, manual smoke test |
| **S4** | Update documentation | Update master plan status |

### Rules
- Only fix files identified in audit as needing replacement
- Follow existing HttpClient("gateway") pattern
- Add proper error handling for API failures
- Update documentation immediately after completion

## 11. ESTIMATED EFFORT
- Medium effort - depends on audit results
- 3 sessions theo JIT Planning
- **BLOCKER:** Không có blockers
