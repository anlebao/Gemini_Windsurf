# TASK CARD: PRODUCTION_HYGIENE - WAVE11 - Delete KhachLink Demo Index

## 1. GOAL & CONTEXT
- **Mục tiêu cốt lõi:** Xóa KhachLink/wwwroot/index.html - demo cũ không được sử dụng
- **Nghiệp vụ áp dụng:** Code cleanup - loại bỏ demo files gây confusion

## 2. ACTIVE WORKFLOW ROUTING
- **Target Workflow:** `Demo file deletion workflow`
- **Execution Mode:** FIX_ONLY

## 3. RELEVANT FILES (CONTEXT BOUNDARY)
- **Files được phép đọc/sửa:**
  - `docs/AI/project_state.md` (Bắt buộc đọc đầu phiên)
  - `5_WebApps/KhachLink/wwwroot/index.html` (xóa)
  - `docs/AI/tasks/PRODUCTION_HYGIENE_master_plan.md` (cập nhật status)
- **Boundary Rules (Nghiêm cấm):**
  - KHÔNG sửa bất kỳ file nào khác ngoài những file được liệt kê
  - KHÔNG sửa KhachLink Pages hoặc Components
  - KHÔNG sửa configuration files

## 4. TECHNICAL & REGULATORY CONSTRAINTS (HARDENING GATES)
- [ ] **Demo File:** File là demo cũ với vanilla JavaScript
- [ ] **Not Used:** KhachLink sử dụng Blazor Pages, không demo HTML
- [ ] **Clean Removal:** Đảm bảo không có broken references sau khi xóa
- [ ] **Build Verification:** `dotnet build VanAn.sln` phải PASS sau khi xóa

## 5. SUCCESS CRITERIA (ĐO LƯỜNG ĐƯỢC)
- [ ] **SC1:** KhachLink/wwwroot/index.html đã xóa khỏi repository
- [ ] **SC2:** Không có code references đến demo index.html
- [ ] **SC3:** `dotnet build VanAn.sln` → 0 errors, 0 warnings mới
- [ ] **SC4:** `guard-check.ps1` → PASS
- [ ] **SC5:** `VanAn.Architecture.Tests`: 7/7 PASS
- [ ] **SC6:** `VanAn.Integration.Tests`: không có test nào bị break
- [ ] **SC7:** PRODUCTION_HYGIENE_master_plan.md updated với W11-T2 status = ✅ DONE

**Implementation Date:** 2026-06-24
**Branch:** feature/wave11-cleanup-invalid-files

## 6. ACTIVE SKILLS (MAX 3)
- `system-refactor-safety` — Đảm bảo xóa file không gây broken references
- `domain-integrity-validation` — Verify không ảnh hưởng domain layer
- `build-error-analysis` — Verify build passes sau deletion

## 7. AI HEALTH CHECK MATRIX (INITIAL)
- **Evidence Count:** 3
- **Verified Facts:**
  - Fact 1: KhachLink/wwwroot/index.html là demo cũ với vanilla JavaScript
  - Fact 2: KhachLink sử dụng Blazor Pages (Home.razor) làm entry point
  - Fact 3: Demo file không được production sử dụng
- **Assumptions:**
  - Demo file không có production dependencies
- **Open Questions:**
  - Q1: Có bất kỳ code reference demo file không?
- **Recommended Action:** Delete demo file and verify no references

## 8. REVERSE IMPACT ANALYSIS
| File thay đổi | Reverse impact | Mitigation |
|---|---|---|
| index.html (xóa) | Không có reverse impact | KhachLink uses Blazor Pages |
| PRODUCTION_HYGIENE_master_plan.md (update status) | Không có reverse impact | Update task status to ✅ DONE |

## 9. TDD & E2E TESTING STRATEGY
- **No new tests needed:** Demo file deletion không cần test mới
- **Verification only:** 
  - Grep search for references to file
  - Build verification
  - Architecture tests verification
- **Test boundary:**
  - Unit tests: N/A
  - Integration tests: N/A
  - E2E tests: N/A

## 10. JIT PLANNING + PURE EXECUTION (MICRO-PHASES)

### Chiến lược thực thi: JIT Planning + Pure Execution

Delete demo file, verify no references, update documentation.

### Micro-phase breakdown cho WAVE11 - Delete KhachLink Demo Index

| Session | JIT Planning (chốt gì) | Pure Execution (viết gì) |
|---|---|---|
| **S1** | Verify no references to demo index.html | Execute grep search, analyze results, confirm safe to delete |
| **S2** | Delete index.html file | Execute file deletion, verify file removed |
| **S3** | Verify build and update documentation | Run dotnet build, update master plan status |

### Rules
- Verify references before deletion (safety first)
- Demo file not used in production - safe to delete
- Update documentation immediately after completion

## 11. ESTIMATED EFFORT
- Low effort - demo file deletion with verification
- 2 sessions theo JIT Planning
- **BLOCKER:** Không có blockers
