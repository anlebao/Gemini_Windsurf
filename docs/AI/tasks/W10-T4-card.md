# TASK CARD: PRODUCTION_HYGIENE - WAVE10 - Verify DI Registrations

## 1. GOAL & CONTEXT
- **Mục tiêu cốt lõi:** Verify Program.cs không có DI registrations cho duplicate interfaces
- **Nghiệp vụ áp dụng:** DI container cleanup verification

## 2. ACTIVE WORKFLOW ROUTING
- **Target Workflow:** `DI verification workflow`
- **Execution Mode:** FIX_ONLY

## 3. RELEVANT FILES (CONTEXT BOUNDARY)
- **Files được phép đọc/sửa:**
  - `docs/AI/project_state.md` (Bắt buộc đọc đầu phiên)
  - `5_WebApps/ShopERP/Program.cs` (verify only)
  - `docs/AI/tasks/PRODUCTION_HYGIENE_master_plan.md` (cập nhật status)
- **Boundary Rules (Nghiêm cấm):**
  - KHÔNG sửa DI registrations nếu không cần thiết
  - KHÔNG sửa bất kỳ configuration khác
  - Chỉ verify và báo cáo

## 4. TECHNICAL & REGULATORY CONSTRAINTS (HARDENING GATES)
- [ ] **DI Container Check:** Verify Program.cs không có registrations cho deleted interfaces
- [ ] **CoreHub Registrations:** CoreHub interfaces vẫn được đăng ký đúng
- [ ] **No Breaking Changes:** Không remove DI registrations cho CoreHub interfaces
- [ ] **Clean State:** DI container state consistent với codebase

## 5. SUCCESS CRITERIA (ĐO LƯỜNG ĐƯỢC)
- [ ] **SC1:** Program.cs không có DI registrations cho ShopERP duplicate interfaces
- [ ] **SC2:** CoreHub interfaces vẫn được đăng ký trong Program.cs
- [ ] **SC3:** `dotnet build VanAn.sln` → 0 errors, 0 warnings mới
- [ ] **SC4:** `guard-check.ps1` → PASS
- [ ] **SC5:** `VanAn.Architecture.Tests`: 7/7 PASS
- [ ] **SC6:** PRODUCTION_HYGIENE_master_plan.md updated với W10-T4 status = ✅ DONE

**Implementation Date:** 2026-06-24
**Branch:** feature/wave10-cleanup-interfaces

## 6. ACTIVE SKILLS (MAX 3)
- `build-error-analysis` — Analyze DI configuration for potential issues
- `domain-integrity-validation` — Verify DI container consistent

## 7. AI HEALTH CHECK MATRIX (INITIAL)
- **Evidence Count:** 2
- **Verified Facts:**
  - Fact 1: Duplicate interfaces deleted
  - Fact 2: Need to verify DI registrations
- **Assumptions:**
  - ShopERP interfaces không có DI registrations
- **Open Questions:**
  - Q1: Có DI registrations cần xóa không?
- **Recommended Action:** Verify Program.cs and remove if needed

## 8. REVERSE IMPACT ANALYSIS
| File thay đổi | Reverse impact | Mitigation |
|---|---|---|
| Program.cs (nếu cần sửa) | DI container changes | Verify CoreHub registrations still present |

## 9. TDD & E2E TESTING STRATEGY
- **Verification Strategy:** Check Program.cs for DI registrations
- **Test boundary:**
  - Unit tests: N/A
  - Integration tests: N/A
  - E2E tests: N/A

## 10. JIT PLANNING + PURE EXECUTION (MICRO-PHASES)

### Chiến lược thực thi: JIT Planning + Pure Execution

Verify Program.cs for DI registrations of deleted interfaces, remove if needed.

### Micro-phase breakdown cho WAVE10 - Verify DI Registrations

| Session | JIT Planning (chốt gì) | Pure Execution (viết gì) |
|---|---|---|
| **S1** | Check Program.cs for DI registrations | Search Program.cs for interface registrations, analyze results |
| **S2** | Remove DI registrations if needed | Remove registrations for deleted interfaces if found |
| **S3** | Verify build and update documentation | Run dotnet build, update master plan status |

### Rules
- Only remove registrations for deleted interfaces
- Keep CoreHub interface registrations intact
- Update documentation immediately after completion

## 11. ESTIMATED EFFORT
- Low effort - DI verification and potential cleanup
- 2 sessions theo JIT Planning
- **BLOCKER:** Không có blockers
