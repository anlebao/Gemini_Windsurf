# TASK CARD: PRODUCTION_HYGIENE - WAVE12 - Fix Authorization Gaps

## 1. GOAL & CONTEXT
- **Mục tiêu cốt lõi:** Fix bất kỳ endpoints thiếu authorization theo audit results
- **Nghiệp vụ áp dụng:** Security fix - protect all unauthorized API endpoints

## 2. ACTIVE WORKFLOW ROUTING
- **Target Workflow:** `Authorization fix workflow`
- **Execution Mode:** FIX_ONLY

## 3. RELEVANT FILES (CONTEXT BOUNDARY)
- **Files được phép đọc/sửa:**
  - `docs/AI/project_state.md` (Bắt buộc đọc đầu phiên)
  - Gateway Controllers (nếu audit tìm thấy gaps)
  - ShopERP Controllers (nếu audit tìm thấy gaps)
  - `docs/AI/tasks/PRODUCTION_HYGIENE_master_plan.md` (cập nhật status)
- **Boundary Rules (Nghiêm cấm):**
  - KHÔNG sửa endpoint logic - chỉ thêm authorization
  - KHÔNG sửa configuration files
  - Chỉ fix endpoints được identify trong audit

## 4. TECHNICAL & REGULATORY CONSTRAINTS (HARDENING GATES)
- [ ] **Authorization Required:** Tất cả endpoints cần authorization trừ legitimate public endpoints
- [ ] **Pattern Consistency:** Sử dụng [Authorize] hoặc policy-based auth như các endpoints khác
- [ ] **Minimal Change:** Chỉ thêm authorization, không sửa business logic
- [ ] **Build Verification:** `dotnet build VanAn.sln` phải PASS sau khi sửa
- [ ] **Test Verification:** Integration tests phải PASS sau khi sửa

## 5. SUCCESS CRITERIA (ĐO LƯỜNG ĐƯỢC)
- [ ] **SC1:** Tất cả endpoints thiếu authorization đã được fix
- [ ] **SC2:** Authorization pattern consistent với các endpoints khác
- [ ] **SC3:** `dotnet build VanAn.sln` → 0 errors, 0 warnings mới
- [ ] **SC4:** `guard-check.ps1` → PASS
- [ ] **SC5:** `VanAn.Architecture.Tests`: 7/7 PASS
- [ ] **SC6:** `VanAn.Integration.Tests`: không có test nào bị break
- [ ] **SC7:** PRODUCTION_HYGIENE_master_plan.md updated với W12-T4 status = ✅ DONE
- [ ] **SC8:** Manual smoke: Verify endpoints require auth

**Implementation Date:** 2026-06-24
**Branch:** feature/wave12-api-authorization

## 6. ACTIVE SKILLS (MAX 3)
- `security-audit` — Apply authorization correctly
- `pattern-based-fixing` — Follow existing authorization patterns
- `build-error-analysis` — Verify build passes after changes

## 7. AI HEALTH CHECK MATRIX (INITIAL)
- **Evidence Count:** 1
- **Verified Facts:**
  - Fact 1: Audit results will identify authorization gaps
- **Assumptions:**
  - Authorization gaps will be found in audit
- **Open Questions:**
  - Q1: Có bao nhiêu endpoints cần fix?
  - Q2: Authorization pattern nào phù hợp cho mỗi endpoint?
- **Recommended Action:** Wait for audit results, then fix identified gaps

## 8. REVERSE IMPACT ANALYSIS
| File thay đổi | Reverse impact | Mitigation |
|---|---|---|
| Gateway/ShopERP Controllers (nếu cần) | Endpoints sẽ cần authentication | Update clients to send auth token |
| PRODUCTION_HYGIENE_master_plan.md (update status) | Không có reverse impact | Update task status to ✅ DONE |

## 9. TDD & E2E TESTING STRATEGY
- **Verification Strategy:** Manual smoke test for authorization
- **Test boundary:**
  - Unit tests: N/A
  - Integration tests: Verify endpoints require auth
  - E2E tests: N/A

## 10. JIT PLANNING + PURE EXECUTION (MICRO-PHASES)

### Chiến lược thực thi: JIT Planning + Pure Execution

Review audit results, fix authorization gaps, verify.

### Micro-phase breakdown cho WAVE12 - Fix Authorization Gaps

| Session | JIT Planning (chốt gì) | Pure Execution (viết gì) |
|---|---|---|
| **S1** | Review audit results | Analyze W12-T1, W12-T2 audit reports, identify gaps |
| **S2** | Apply authorization fixes | Add [Authorize] or policy-based auth to identified endpoints |
| **S3** | Verify build and manual smoke | Run dotnet build, manual smoke test for auth |
| **S4** | Update documentation | Update master plan status |

### Rules
- Only fix endpoints identified in audit
- Only add authorization, don't modify business logic
- Follow existing authorization patterns in the same Controller
- Update documentation immediately after completion

## 11. ESTIMATED EFFORT
- Medium effort - depends on audit results
- 3 sessions theo JIT Planning
- **BLOCKER:** Không có blockers
