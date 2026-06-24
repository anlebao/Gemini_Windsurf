# TASK CARD: PRODUCTION_HYGIENE - WAVE8 - Delete Dashboard Security Risk

## 1. GOAL & CONTEXT
- **Mục tiêu cốt lõi:** Xóa VanAn_Dashboard.html - public infrastructure control dashboard không phù hợp production
- **Nghiệp vụ áp dụng:** Security hardening - loại bỏ public access dashboard với Docker control commands

## 2. ACTIVE WORKFLOW ROUTING
- **Target Workflow:** `Simple file deletion workflow`
- **Execution Mode:** FIX_ONLY

## 3. RELEVANT FILES (CONTEXT BOUNDARY)
- **Files được phép đọc/sửa:**
  - `docs/AI/project_state.md` (Bắt buộc đọc đầu phiên)
  - `VanAn_Dashboard.html` (xóa)
  - `docs/AI/tasks/PRODUCTION_HYGIENE_master_plan.md` (cập nhật status)
- **Boundary Rules (Nghiêm cấm):**
  - KHÔNG sửa bất kỳ file nào khác ngoài những file được liệt kê
  - KHÔNG sửa configuration files, Program.cs, hoặc bất kỳ runtime code
  - KHÔNG tạo replacement dashboard

## 4. TECHNICAL & REGULATORY CONSTRAINTS (HARDENING GATES)
- [ ] **Security First:** Dashboard có public infrastructure control commands - không phù hợp production
- [ ] **Zero Impact:** Xóa file không ảnh hưởng production flow vì dashboard không được sử dụng
- [ ] **Clean Removal:** Đảm bảo không có broken references sau khi xóa
- [ ] **Documentation Sync:** Cập nhật master plan status sau khi hoàn thành
- [ ] **Build Verification:** `dotnet build VanAn.sln` phải PASS sau khi xóa

## 5. SUCCESS CRITERIA (ĐO LƯỜNG ĐƯỢC)
- [ ] **SC1:** VanAn_Dashboard.html đã xóa khỏi repository
- [ ] **SC2:** Không có broken references đến VanAn_Dashboard.html trong codebase
- [ ] **SC3:** `dotnet build VanAn.sln` → 0 errors, 0 warnings mới
- [ ] **SC4:** `guard-check.ps1` → PASS
- [ ] **SC5:** `VanAn.Architecture.Tests`: 7/7 PASS
- [ ] **SC6:** `VanAn.Integration.Tests`: không có test nào bị break
- [ ] **SC7:** PRODUCTION_HYGIENE_master_plan.md updated với W8-T1 status = ✅ DONE
- [ ] **SC8:** Verify không có HTML files khác với similar security issues

**Implementation Date:** 2026-06-24
**Branch:** feature/wave8-cleanup-dashboard

## 6. ACTIVE SKILLS (MAX 3)
- `system-refactor-safety` — Đảm bảo xóa file không gây broken references
- `domain-integrity-validation` — Verify không ảnh hưởng domain layer
- `build-error-analysis` — Verify build passes sau deletion

## 7. AI HEALTH CHECK MATRIX (INITIAL)
- **Evidence Count:** 5
- **Verified Facts:**
  - Fact 1: VanAn_Dashboard.html tồn tại ở root directory
  - Fact 2: Dashboard có hardcoded baseIp = 'localhost'
  - Fact 3: Dashboard có simulated Docker commands và build checks
  - Fact 4: Dashboard không có authentication/authorization
  - Fact 5: Dashboard không được production sử dụng
- **Assumptions:**
  - Dashboard là development-only tool
  - Không có production dependencies vào dashboard
- **Open Questions:**
  - Q1: Có bất kỳ scripts hay automation tools reference dashboard không?
  - Q2: Có documentation references dashboard không?
- **Recommended Action:** Delete file và verify no broken references

## 8. REVERSE IMPACT ANALYSIS
| File thay đổi | Reverse impact | Mitigation |
|---|---|---|
| VanAn_Dashboard.html (xóa) | Không có reverse impact - file không được sử dụng | Verify no references before deletion |
| PRODUCTION_HYGIENE_master_plan.md (update status) | Không có reverse impact | Update task status to ✅ DONE |

## 9. TDD & E2E TESTING STRATEGY
- **No new tests needed:** File deletion không cần test mới
- **Verification only:** 
  - Grep search cho references đến file name
  - Build verification
  - Architecture tests verification
- **Test boundary:**
  - Unit tests: N/A
  - Integration tests: N/A
  - E2E tests: N/A

## 10. JIT PLANNING + PURE EXECUTION (MICRO-PHASES)

### Chiến lược thực thi: JIT Planning + Pure Execution

Simple file deletion task - verify references first, then delete file, then verify build.

### Micro-phase breakdown cho WAVE8 - Delete Dashboard

| Session | JIT Planning (chốt gì) | Pure Execution (viết gì) |
|---|---|---|
| **S1** | Verify no references to VanAn_Dashboard.html via grep search | Execute grep search, analyze results, confirm safe to delete |
| **S2** | Delete VanAn_Dashboard.html file | Execute file deletion, verify file removed |
| **S3** | Verify build and update documentation | Run dotnet build, update master plan status |

### Rules
- Verify references before deletion (safety first)
- Update documentation immediately after completion
- Run build verification after deletion

## 11. ESTIMATED EFFORT
- Low effort - simple file deletion with verification
- 2 sessions theo JIT Planning
- **BLOCKER:** Không có blockers
