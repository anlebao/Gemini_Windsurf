# TASK CARD: PRODUCTION_HYGIENE - WAVE15 - Update project_state.md

## 1. GOAL & CONTEXT
- **Mục tiêu cốt lõi:** Cập nhật `docs/AI/project_state.md` để phản ánh Wave 15 hoàn thành — đưa vào history, xóa khỏi backlog, cập nhật Next Actions
- **Nghiệp vụ áp dụng:** Session state management — đảm bảo AI session tiếp theo biết Wave 15 đã done và không làm lại
- **Master plan:** `docs/AI/tasks/PRODUCTION_HYGIENE_master_plan.md` § 9. WAVE 15 — task W15-T5
- **Depends on:** W15-T4 (verification gate pass)

## 2. ACTIVE WORKFLOW ROUTING
- **Target Workflow:** `update-state` skill
- **Execution Mode:** Invoke `update-state` skill — đọc hướng dẫn trước khi sửa

## 3. RELEVANT FILES (CONTEXT BOUNDARY)
- **Files được phép đọc/sửa:**
  - `docs/AI/project_state.md` (SỬA — theo Maintenance Rules trong Section 0)
  - `docs/AI/tasks/PRODUCTION_HYGIENE_master_plan.md` (SỬA — update W15-T5 = ✅ DONE + Last Updated)
- **KHÔNG được sửa bất kỳ file code nào trong task này**

## 4. NỘI DUNG CẦN CẬP NHẬT TRONG project_state.md

### Section 2 — Current Objective
Nếu Wave 15 là objective hiện tại: update status thành COMPLETED, move về History.

### Section 3 — Current Status (thêm vào)
```markdown
* **Wave 15 — KhachLink Page Cleanup COMPLETED (2026-[date]):**
  - XÓA: `Index.razor` (dead code, route conflict với Home.razor)
  - XÓA: `IndexModern.cshtml` (demo landing `/modern`, stats bịa)
  - XÓA: `_Host.cshtml` (orphan HTML shell, không được route đến)
  - XÓA: `Index.cshtml` + `Index.cshtml.cs` (duplicate landing, vi phạm VA-KHACHLINK-004)
  - REWRITE: `VoiceNote.razor` — endpoint đúng (`/api/v1/voicecommand/text-command`),
    `IHttpClientFactory("gateway")`, xóa `<html>` tags, `VanAnAlert` thay `alert()`
  - FIX: `Program.cs` — xóa `MapFallbackToPage("/Index")` (broken sau khi xóa Index.cshtml)
  - `Home.razor` xác nhận là canonical entry point tại `/` và `/home`
  - Build: 0 errors ✅ | guard-check: PASS ✅ | Architecture tests: 7/7 ✅
```

### Section 4 — Next Actions (xóa items liên quan đến Wave 15, thêm next wave)
- Xóa: bất kỳ backlog item nào liên quan đến KhachLink demo pages
- Thêm: `[ ] Wave 16 — [next wave nếu có]` hoặc ghi rõ production readiness status

### Section 11 — Maintenance Log
```markdown
* **[date]:** Wave 15 complete — KhachLink Page Cleanup
  - 5 files deleted, 1 rewritten, 1 routing fix
  - Current branch: feature/wave15-khachlink-page-cleanup → PR → merge
```

## 5. NỘI DUNG CẬP NHẬT TRONG PRODUCTION_HYGIENE_master_plan.md

Cập nhật header:
```markdown
**Last Updated:** [date]
**Current Status:** PLANNING — Wave 16 (hoặc COMPLETED nếu là wave cuối)
```

Cập nhật task table Wave 15:
```markdown
| 1 | W15-T1 | Xóa 4 dead/demo pages | — | W15-T1-card.md | ✅ DONE |
| 2 | W15-T2 | Fix Program.cs routing | W15-T1 | W15-T2-card.md | ✅ DONE |
| 3 | W15-T3 | Rewrite VoiceNote.razor | W15-T2 | W15-T3-card.md | ✅ DONE |
| 4 | W15-T4 | Verify build + E2E contract | W15-T3 | W15-T4-card.md | ✅ DONE |
| 5 | W15-T5 | Update project_state.md | W15-T4 | W15-T5-card.md | ✅ DONE |
```

Cập nhật Exit criteria (check all boxes):
```markdown
- [x] dotnet build VanAn.sln → 0 errors, 0 warnings mới
- [x] guard-check.ps1 → PASS
- [x] VanAn.Architecture.Tests: 7/7 PASS
- [x] Verify: 5 files đã xóa không còn trong repository
- [x] Verify: Home.razor serve tại http://localhost:5002/ — .feature-card và Đặt ngay present
- [x] Verify: VoiceNote.razor pass arch checks
- [x] Verify: Không có broken references
```

## 6. MAINTENANCE RULES (TỪPROJECT_STATE.MD SECTION 0)

Trước khi sửa, đọc Section 0 của `project_state.md` để tuân thủ:
- Không tạo duplicate sections
- Không contradictions giữa sections
- Verify paths/branches against real repo
- Append vào History — không xóa entries cũ
- Dùng skill `update-state` nếu available

## 7. BƯỚC THỰC HIỆN

```
S1: Invoke skill update-state (nếu available)
    → Skill sẽ hướng dẫn cách sửa project_state.md đúng format

S2: Đọc project_state.md Section 0 (Maintenance Rules)
    → Đọc Section 2 (Current Objective)
    → Đọc Section 3 (Current Status)
    → Đọc Section 4 (Next Actions)
    → Đọc Section 11 (Maintenance Log)

S3: Cập nhật theo mục 4 ở trên
    → Thêm Wave 15 vào history
    → Cập nhật Next Actions
    → Cập nhật Maintenance Log với date thực

S4: Cập nhật PRODUCTION_HYGIENE_master_plan.md
    → Đánh dấu tất cả W15-T1 → W15-T5 = ✅ DONE
    → Update Last Updated date
    → Update Current Status

S5: Commit
    → "[W15-T5] Update state — Wave 15 KhachLink Page Cleanup complete"
```

## 8. SUCCESS CRITERIA (ĐO LƯỜNG ĐƯỢC)
- [ ] **SC1:** `project_state.md` Section 3 có entry Wave 15 COMPLETED
- [ ] **SC2:** `project_state.md` Section 4 không còn backlog items liên quan đến KhachLink dead pages
- [ ] **SC3:** `project_state.md` Section 11 có log entry ngày hôm nay
- [ ] **SC4:** `PRODUCTION_HYGIENE_master_plan.md` — W15-T1 → W15-T5 = ✅ DONE
- [ ] **SC5:** `PRODUCTION_HYGIENE_master_plan.md` — Exit criteria Wave 15 tất cả checked
- [ ] **SC6:** `PRODUCTION_HYGIENE_master_plan.md` header `Last Updated` = ngày hôm nay
- [ ] **SC7:** Không có duplicate sections hoặc contradictions trong `project_state.md`

**Implementation Date:** TBD
**Branch:** `feature/wave15-khachlink-page-cleanup`

## 9. ACTIVE SKILLS (MAX 3)
- `update-state` — Skill chuyên dụng để cập nhật project_state.md đúng format

## 10. AI HEALTH CHECK MATRIX (INITIAL)
- **Evidence Count:** 2
- **Verified Facts:**
  - Fact 1: `project_state.md` có Section 0 chứa Maintenance Rules — phải đọc trước khi sửa
  - Fact 2: Các wave trước (Wave 3–8) đều có entry trong Section 3 History
- **Assumptions:**
  - Format history entry giống các waves trước
- **Open Questions:**
  - Q1: Wave 15 có phải là wave cuối không? → Nếu có → cập nhật Current Status = "All waves complete"
- **Recommended Action:** IMPLEMENT — simple documentation update

## 11. REVERSE IMPACT ANALYSIS
| Thay đổi | Reverse impact | Mitigation |
|---|---|---|
| Thêm history entry | Không có — append only | None |
| Update Next Actions | AI session tiếp theo sẽ biết đúng next steps | Review trước khi commit |
| Update master plan status | Planning doc reflects reality | None |

## 12. ESTIMATED EFFORT
- Very low effort — documentation update
- < 30 phút
- **BLOCKER:** W15-T4 (verification gate) phải PASS
- **UNBLOCKS:** PR creation → merge → Wave 16 (nếu có)
