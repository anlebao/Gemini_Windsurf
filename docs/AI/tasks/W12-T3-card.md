# TASK CARD: PRODUCTION_HYGIENE - WAVE12 - Add Authorization for Voice Note Endpoint

## 1. GOAL & CONTEXT
- **Mục tiêu cốt lõi:** Add [Authorize] hoặc policy-based auth cho voice note endpoint
- **Nghiệp vụ áp dụng:** Security fix - protect public API endpoint

## 2. ACTIVE WORKFLOW ROUTING
- **Target Workflow:** `Authorization fix workflow`
- **Execution Mode:** FIX_ONLY

## 3. RELEVANT FILES (CONTEXT BOUNDARY)
- **Files được phép đọc/sửa:**
  - `docs/AI/project_state.md` (Bắt buộc đọc đầu phiên)
  - `2_Gateway/Controllers/VoiceCommandController.cs` (nếu voice note endpoint ở đây)
  - `5_WebApps/KhachLink/Pages/VoiceNote.razor` (verify endpoint usage)
  - `docs/AI/tasks/PRODUCTION_HYGIENE_master_plan.md` (cập nhật status)
- **Boundary Rules (Nghiêm cấm):**
  - KHÔNG sửa endpoint logic - chỉ thêm authorization
  - KHÔNG sửa VoiceNote.razor UI logic
  - KHÔNG sửa configuration files

## 4. TECHNICAL & REGULATORY CONSTRAINTS (HARDENING GATES)
- [ ] **Authorization Required:** Voice note endpoint cần authorization
- [ ] **Pattern Consistency:** Sử dụng [Authorize] hoặc policy-based auth như các endpoints khác
- [ ] **Minimal Change:** Chỉ thêm authorization, không sửa business logic
- [ ] **Build Verification:** `dotnet build VanAn.sln` phải PASS sau khi sửa
- [ ] **Test Verification:** Integration tests phải PASS sau khi sửa

## 5. SUCCESS CRITERIA (ĐO LƯỜNG ĐƯỢC)
- [ ] **SC1:** Voice note endpoint có [Authorize] hoặc policy-based auth
- [ ] **SC2:** Authorization pattern consistent với các endpoints khác
- [ ] **SC3:** `dotnet build VanAn.sln` → 0 errors, 0 warnings mới
- [ ] **SC4:** `guard-check.ps1` → PASS
- [ ] **SC5:** `VanAn.Architecture.Tests`: 7/7 PASS
- [ ] **SC6:** `VanAn.Integration.Tests`: không có test nào bị break
- [ ] **SC7:** PRODUCTION_HYGIENE_master_plan.md updated với W12-T3 status = ✅ DONE
- [ ] **SC8:** Manual smoke: Verify endpoint requires auth

**Implementation Date:** 2026-06-24
**Branch:** feature/wave12-api-authorization

## 6. ACTIVE SKILLS (MAX 3)
- `security-audit` — Apply authorization correctly
- `pattern-based-fixing` — Follow existing authorization patterns
- `build-error-analysis` — Verify build passes after changes

## 7. AI HEALTH CHECK MATRIX (INITIAL)
- **Evidence Count:** 2
- **Verified Facts:**
  - Fact 1: Voice note endpoint thiếu authorization
  - Fact 2: Endpoint là POST /api/orders/voice-note
- **Assumptions:**
  - Endpoint cần [Authorize] hoặc policy-based auth
- **Open Questions:**
  - Q1: Endpoint nằm ở Controller nào?
  - Q2: Authorization pattern nào phù hợp?
- **Recommended Action:** Investigate endpoint location, apply appropriate authorization

## 8. REVERSE IMPACT ANALYSIS
| File thay đổi | Reverse impact | Mitigation |
|---|---|---|
| VoiceCommandController.cs (nếu có) | Endpoint sẽ cần authentication | Update client to send auth token |
| PRODUCTION_HYGIENE_master_plan.md (update status) | Không có reverse impact | Update task status to ✅ DONE |

## 9. TDD & E2E TESTING STRATEGY
- **Verification Strategy:** Manual smoke test for authorization
- **Test boundary:**
  - Unit tests: N/A
  - Integration tests: Verify endpoint requires auth
  - E2E tests: N/A

## 10. JIT PLANNING + PURE EXECUTION (MICRO-PHASES)

### Chiến lược thực thi: JIT Planning + Pure Execution

Investigate voice note endpoint location, apply appropriate authorization, verify.

### Micro-phase breakdown cho WAVE12 - Add Voice Note Authorization

| Session | JIT Planning (chốt gì) | Pure Execution (viết gì) |
|---|---|---|
| **S1** | Locate voice note endpoint | Search for endpoint definition, identify Controller |
| **S2** | Apply authorization attribute | Add [Authorize] or policy-based auth to endpoint |
| **S3** | Verify build and manual smoke | Run dotnet build, manual smoke test for auth |
| **S4** | Update documentation | Update master plan status |

### Rules
- Only add authorization, don't modify business logic
- Follow existing authorization patterns in the same Controller
- Update documentation immediately after completion

## 11. ESTIMATED EFFORT
- Medium effort - investigation + authorization fix
- 3 sessions theo JIT Planning
- **BLOCKER:** Không có blockers
