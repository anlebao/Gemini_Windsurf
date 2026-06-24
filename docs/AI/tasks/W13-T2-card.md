# TASK CARD: PRODUCTION_HYGIENE - WAVE13 - Implement Real API Call for KhachLink Home

## 1. GOAL & CONTEXT
- **Mục tiêu cốt lõi:** Implement real API call cho KhachLink Home.razor products
- **Nghiệp vụ áp dụng:** Data implementation - replace hardcoded products with real API call

## 2. ACTIVE WORKFLOW ROUTING
- **Target Workflow:** `Data implementation workflow`
- **Execution Mode:** FIX_ONLY

## 3. RELEVANT FILES (CONTEXT BOUNDARY)
- **Files được phép đọc/sửa:**
  - `docs/AI/project_state.md` (Bắt buộc đọc đầu phiên)
  - `5_WebApps/KhachLink/Pages/Home.razor` (implement real API call)
  - `docs/AI/tasks/PRODUCTION_HYGIENE_master_plan.md` (cập nhật status)
- **Boundary Rules (Nghiêm cấm):**
  - KHÔNG sửa Gateway Controllers
  - KHÔNG sửa CoreHub Services
  - Chỉ implement client-side API call

## 4. TECHNICAL & REGULATORY CONSTRAINTS (HARDENING GATES)
- [ ] **Real API Call:** Replace hardcoded products với real API call
- [ ] **Gateway Pattern:** Sử dụng HttpClient("gateway") pattern như các pages khác
- [ ] **Error Handling:** Add proper error handling cho API call
- [ ] **Build Verification:** `dotnet build VanAn.sln` phải PASS sau khi sửa
- [ ] **Test Verification:** Integration tests phải PASS sau khi sửa

## 5. SUCCESS CRITERIA (ĐO LƯỜNG ĐƯỢC)
- [ ] **SC1:** Home.razor sử dụng real API call thay vì hardcoded products
- [ ] **SC2:** TODO comment removed
- [ ] **SC3:** Error handling implemented cho API call
- [ ] **SC4:** `dotnet build VanAn.sln` → 0 errors, 0 warnings mới
- [ ] **SC5:** `guard-check.ps1` → PASS
- [ ] **SC6:** `VanAn.Architecture.Tests`: 7/7 PASS
- [ ] **SC7:** `VanAn.Integration.Tests`: không có test nào bị break
- [ ] **SC8:** PRODUCTION_HYGIENE_master_plan.md updated với W13-T2 status = ✅ DONE
- [ ] **SC9:** Manual smoke: Verify Home page loads real data

**Implementation Date:** 2026-06-24
**Branch:** feature/wave13-replace-hardcoded-data

## 6. ACTIVE SKILLS (MAX 3)
- `pattern-based-fixing` — Follow existing API call patterns
- `build-error-analysis` — Verify build passes after changes
- `domain-integrity-validation` — Verify data flow correct

## 7. AI HEALTH CHECK MATRIX (INITIAL)
- **Evidence Count:** 2
- **Verified Facts:**
  - Fact 1: Home.razor có hardcoded products với TODO comment
  - Fact 2: KhachLink sử dụng HttpClient("gateway") pattern
- **Assumptions:**
  - Gateway có products endpoint
- **Open Questions:**
  - Q1: Gateway có products endpoint không?
  - Q2: API pattern nào phù hợp?
- **Recommended Action:** Investigate Gateway products endpoint, implement real API call

## 8. REVERSE IMPACT ANALYSIS
| File thay đổi | Reverse impact | Mitigation |
|---|---|---|
| Home.razor (implement API call) | Page sẽ phụ thuộc vào API | Add error handling, fallback |
| PRODUCTION_HYGIENE_master_plan.md (update status) | Không có reverse impact | Update task status to ✅ DONE |

## 9. TDD & E2E TESTING STRATEGY
- **Verification Strategy:** Manual smoke test for data loading
- **Test boundary:**
  - Unit tests: N/A
  - Integration tests: N/A
  - E2E tests: N/A

## 10. JIT PLANNING + PURE EXECUTION (MICRO-PHASES)

### Chiến lược thực thi: JIT Planning + Pure Execution

Investigate Gateway products endpoint, implement real API call, verify.

### Micro-phase breakdown cho WAVE13 - Implement Real API Call

| Session | JIT Planning (chốt gì) | Pure Execution (viết gì) |
|---|---|---|
| **S1** | Investigate Gateway products endpoint | Check Gateway Controllers for products endpoint |
| **S2** | Implement real API call in Home.razor | Replace hardcoded data with HttpClient call |
| **S3** | Add error handling and verify | Add error handling, manual smoke test |
| **S4** | Update documentation | Update master plan status |

### Rules
- Follow existing HttpClient("gateway") pattern in KhachLink
- Add proper error handling for API failures
- Update documentation immediately after completion

## 11. ESTIMATED EFFORT
- Medium effort - investigation + implementation
- 3 sessions theo JIT Planning
- **BLOCKER:** Không có blockers
