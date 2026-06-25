# TASK CARD: PRODUCTION_HYGIENE - WAVE10 - Delete SocialCampaignService Interface

## 1. GOAL & CONTEXT
- **Mục tiêu cốt lõi:** Xóa ShopERP/Services/ISocialCampaignService.cs - duplicate interface không được sử dụng
- **Nghiệp vụ áp dụng:** Technical debt cleanup - loại bỏ duplicate interfaces gây confusion

## 2. ACTIVE WORKFLOW ROUTING
- **Target Workflow:** `Interface deletion workflow`
- **Execution Mode:** FIX_ONLY

## 3. RELEVANT FILES (CONTEXT BOUNDARY)
- **Files được phép đọc/sửa:**
  - `docs/AI/project_state.md` (Bắt buộc đọc đầu phiên)
  - `5_WebApps/ShopERP/Services/ISocialCampaignService.cs` (xóa)
  - `docs/AI/tasks/PRODUCTION_HYGIENE_master_plan.md` (cập nhật status)
- **Boundary Rules (Nghiêm cấm):**
  - KHÔNG sửa CoreHub interface (3_CoreHub/Services/ISocialCampaignService.cs)
  - KHÔNG sửa implementation (3_CoreHub/Services/SocialCampaignService.cs)
  - KHÔNG sửa code sử dụng interface

## 4. TECHNICAL & REGULATORY CONSTRAINTS (HARDENING GATES)
- [ ] **Duplicate Interface:** ShopERP interface là duplicate của CoreHub interface
- [ ] **Not Used:** ShopERP interface không được sử dụng bởi bất kỳ code nào
- [ ] **CoreHub is Source of Truth:** CoreHub interface và implementation là production standard
- [ ] **Clean Removal:** Đảm bảo không có broken references sau khi xóa
- [ ] **Build Verification:** `dotnet build VanAn.sln` phải PASS sau khi xóa

## 5. SUCCESS CRITERIA (ĐO LƯỜNG ĐƯỢC)
- [ ] **SC1:** ISocialCampaignService.cs (ShopERP) đã xóa khỏi repository
- [ ] **SC2:** Không có code references đến ShopERP ISocialCampaignService
- [ ] **SC3:** `dotnet build VanAn.sln` → 0 errors, 0 warnings mới
- [ ] **SC4:** `guard-check.ps1` → PASS
- [ ] **SC5:** `VanAn.Architecture.Tests`: 7/7 PASS
- [ ] **SC6:** `VanAn.Integration.Tests`: không có test nào bị break
- [ ] **SC7:** PRODUCTION_HYGIENE_master_plan.md updated với W10-T1 status = ✅ DONE

**Implementation Date:** 2026-06-24
**Branch:** feature/wave10-cleanup-interfaces

## 6. ACTIVE SKILLS (MAX 3)
- `system-refactor-safety` — Đảm bảo xóa interface không gây broken references
- `domain-integrity-validation` — Verify không ảnh hưởng domain layer
- `build-error-analysis` — Verify build passes sau deletion

## 7. AI HEALTH CHECK MATRIX (INITIAL)
- **Evidence Count:** 3
- **Verified Facts:**
  - Fact 1: ShopERP ISocialCampaignService là duplicate của CoreHub interface
  - Fact 2: CoreHub có interface và implementation hoàn chỉnh
  - Fact 3: ShopERP interface không được sử dụng bởi bất kỳ code nào
- **Assumptions:**
  - CoreHub interface là source of truth
  - Không có production dependencies vào ShopERP interface
- **Open Questions:**
  - Q1: Có bất kỳ code reference ShopERP interface không?
- **Recommended Action:** Delete duplicate interface and verify no references

## 8. REVERSE IMPACT ANALYSIS
| File thay đổi | Reverse impact | Mitigation |
|---|---|---|
| ISocialCampaignService.cs (xóa) | Không có reverse impact | CoreHub interface is source of truth |
| PRODUCTION_HYGIENE_master_plan.md (update status) | Không có reverse impact | Update task status to ✅ DONE |

## 9. TDD & E2E TESTING STRATEGY
- **No new tests needed:** Interface deletion không cần test mới
- **Verification only:** 
  - Grep search for references to interface
  - Build verification
  - Architecture tests verification
- **Test boundary:**
  - Unit tests: N/A
  - Integration tests: N/A
  - E2E tests: N/A

## 10. JIT PLANNING + PURE EXECUTION (MICRO-PHASES)

### Chiến lược thực thi: JIT Planning + Pure Execution

Delete duplicate interface file, verify no references, update documentation.

### Micro-phase breakdown cho WAVE10 - Delete SocialCampaignService Interface

| Session | JIT Planning (chốt gì) | Pure Execution (viết gì) |
|---|---|---|
| **S1** | Verify no references to ShopERP ISocialCampaignService | Execute grep search, analyze results, confirm safe to delete |
| **S2** | Delete ISocialCampaignService.cs file | Execute file deletion, verify file removed |
| **S3** | Verify build and update documentation | Run dotnet build, update master plan status |

### Rules
- Verify references before deletion (safety first)
- CoreHub interface remains as source of truth
- Update documentation immediately after completion

## 11. ESTIMATED EFFORT
- Low effort - interface deletion with verification
- 2 sessions theo JIT Planning
- **BLOCKER:** Không có blockers
