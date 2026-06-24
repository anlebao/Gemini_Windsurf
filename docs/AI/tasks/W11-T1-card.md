# TASK CARD: PRODUCTION_HYGIENE - WAVE11 - Delete SocialCampaignManager

## 1. GOAL & CONTEXT
- **Mục tiêu cốt lõi:** Xóa SocialCampaignManager.cshtml - invalid Razor Pages + Blazor syntax mix
- **Nghiệp vụ áp dụng:** Code cleanup - loại bỏ file không thể chạy gây confusion

## 2. ACTIVE WORKFLOW ROUTING
- **Target Workflow:** `Invalid file deletion workflow`
- **Execution Mode:** FIX_ONLY

## 3. RELEVANT FILES (CONTEXT BOUNDARY)
- **Files được phép đọc/sửa:**
  - `docs/AI/project_state.md` (Bắt buộc đọc đầu phiên)
  - `5_WebApps/ShopERP/Pages/SocialCampaignManager.cshtml` (xóa)
  - `docs/AI/tasks/PRODUCTION_HYGIENE_master_plan.md` (cập nhật status)
- **Boundary Rules (Nghiêm cấm):**
  - KHÔNG sửa bất kỳ file nào khác ngoài những file được liệt kê
  - KHÔNG cố gắng fix file - syntax error quá nghiêm trọng
  - KHÔNG tạo replacement file

## 4. TECHNICAL & REGULATORY CONSTRAINTS (HARDENING GATES)
- [ ] **Invalid Syntax:** File mix Razor Pages + Blazor syntax - không thể chạy
- [ ] **Broken Code:** Hardcoded empty data với broken @code block
- [ ] **Not Production Ready:** File không phù hợp production deployment
- [ ] **Clean Removal:** Đảm bảo không có broken references sau khi xóa
- [ ] **Build Verification:** `dotnet build VanAn.sln` phải PASS sau khi xóa

## 5. SUCCESS CRITERIA (ĐO LƯỜNG ĐƯỢC)
- [ ] **SC1:** SocialCampaignManager.cshtml đã xóa khỏi repository
- [ ] **SC2:** Không có code references đến SocialCampaignManager
- [ ] **SC3:** `dotnet build VanAn.sln` → 0 errors, 0 warnings mới
- [ ] **SC4:** `guard-check.ps1` → PASS
- [ ] **SC5:** `VanAn.Architecture.Tests`: 7/7 PASS
- [ ] **SC6:** `VanAn.Integration.Tests`: không có test nào bị break
- [ ] **SC7:** PRODUCTION_HYGIENE_master_plan.md updated với W11-T1 status = ✅ DONE

**Implementation Date:** 2026-06-24
**Branch:** feature/wave11-cleanup-invalid-files

## 6. ACTIVE SKILLS (MAX 3)
- `system-refactor-safety` — Đảm bảo xóa file không gây broken references
- `domain-integrity-validation` — Verify không ảnh hưởng domain layer
- `build-error-analysis` — Verify build passes sau deletion

## 7. AI HEALTH CHECK MATRIX (INITIAL)
- **Evidence Count:** 4
- **Verified Facts:**
  - Fact 1: SocialCampaignManager.cshtml mix Razor Pages + Blazor syntax
  - Fact 2: File có hardcoded empty data
  - Fact 3: File có broken @code block
  - Fact 4: File không thể chạy được
- **Assumptions:**
  - File không được production sử dụng
- **Open Questions:**
  - Q1: Có bất kỳ code reference file không?
- **Recommended Action:** Delete file and verify no references

## 8. REVERSE IMPACT ANALYSIS
| File thay đổi | Reverse impact | Mitigation |
|---|---|---|
| SocialCampaignManager.cshtml (xóa) | Không có reverse impact | File không thể chạy anyway |
| PRODUCTION_HYGIENE_master_plan.md (update status) | Không có reverse impact | Update task status to ✅ DONE |

## 9. TDD & E2E TESTING STRATEGY
- **No new tests needed:** Invalid file deletion không cần test mới
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

Delete invalid file, verify no references, update documentation.

### Micro-phase breakdown cho WAVE11 - Delete SocialCampaignManager

| Session | JIT Planning (chốt gì) | Pure Execution (viết gì) |
|---|---|---|
| **S1** | Verify no references to SocialCampaignManager | Execute grep search, analyze results, confirm safe to delete |
| **S2** | Delete SocialCampaignManager.cshtml file | Execute file deletion, verify file removed |
| **S3** | Verify build and update documentation | Run dotnet build, update master plan status |

### Rules
- Verify references before deletion (safety first)
- File cannot run anyway - safe to delete
- Update documentation immediately after completion

## 11. ESTIMATED EFFORT
- Low effort - invalid file deletion with verification
- 2 sessions theo JIT Planning
- **BLOCKER:** Không có blockers
