# TASK CARD: PRODUCTION_HYGIENE - WAVE9 - Delete Orphan Controller

## 1. GOAL & CONTEXT
- **Mục tiêu cốt lõi:** Xóa ShopERP/Controllers/CustomersController.cs - orphan controller với security bypass
- **Nghiệp vụ áp dụng:** Security hardening - loại bỏ controller bypass service layer và authorization

## 2. ACTIVE WORKFLOW ROUTING
- **Target Workflow:** `Controller deletion workflow`
- **Execution Mode:** FIX_ONLY

## 3. RELEVANT FILES (CONTEXT BOUNDARY)
- **Files được phép đọc/sửa:**
  - `docs/AI/project_state.md` (Bắt buộc đọc đầu phiên)
  - `5_WebApps/ShopERP/Controllers/CustomersController.cs` (xóa)
  - `docs/AI/tasks/PRODUCTION_HYGIENE_master_plan.md` (cập nhật status)
- **Boundary Rules (Nghiêm cấm):**
  - KHÔNG sửa bất kỳ file nào khác ngoài những file được liệt kê
  - KHÔNG sửa integration tests trong task này (đó là task W9-T2)
  - KHÔNG sửa Program.cs hoặc DI registrations

## 4. TECHNICAL & REGULATORY CONSTRAINTS (HARDENING GATES)
- [ ] **Security Critical:** Controller có [AllowAnonymous] trên tất cả endpoints - security risk
- [ ] **Architecture Violation:** Controller bypass service layer (DbContext trực tiếp)
- [ ] **Orphan Code:** Controller không được production sử dụng
- [ ] **Clean Removal:** Đảm bảo không có broken references sau khi xóa
- [ ] **Build Verification:** `dotnet build VanAn.sln` phải PASS sau khi xóa

## 5. SUCCESS CRITERIA (ĐO LƯỜNG ĐƯỢC)
- [ ] **SC1:** CustomersController.cs đã xóa khỏi repository
- [ ] **SC2:** Không có code references đến CustomersController trong production code
- [ ] **SC3:** `dotnet build VanAn.sln` → 0 errors, 0 warnings mới
- [ ] **SC4:** `guard-check.ps1` → PASS
- [ ] **SC5:** `VanAn.Architecture.Tests`: 7/7 PASS
- [ ] **SC6:** PRODUCTION_HYGIENE_master_plan.md updated với W9-T1 status = ✅ DONE
- [ ] **SC7:** Verify integration tests sẽ được xử lý trong task W9-T2

**Implementation Date:** 2026-06-24
**Branch:** feature/wave9-cleanup-controller

## 6. ACTIVE SKILLS (MAX 3)
- `system-refactor-safety` — Đảm bảo xóa controller không gây broken references
- `domain-integrity-validation` — Verify không ảnh hưởng domain layer
- `build-error-analysis` — Verify build passes sau deletion

## 7. AI HEALTH CHECK MATRIX (INITIAL)
- **Evidence Count:** 5
- **Verified Facts:**
  - Fact 1: CustomersController.cs tồn tại với [AllowAnonymous] attributes
  - Fact 2: Controller sử dụng DbContext trực tiếp thay vì service layer
  - Fact 3: Controller không được production sử dụng (KhachLink dùng Gateway)
  - Fact 4: Integration tests test controller trực tiếp (wrong architecture)
  - Fact 5: Controller có security bypass với [AllowAnonymous]
- **Assumptions:**
  - Controller là orphan code không cần thiết
  - Integration tests sẽ được refactored trong task W9-T2
- **Open Questions:**
  - Q1: Có bất kỳ production code reference controller không?
  - Q2: Có DI registration cần xóa không?
- **Recommended Action:** Delete controller and verify no production references

## 8. REVERSE IMPACT ANALYSIS
| File thay đổi | Reverse impact | Mitigation |
|---|---|---|
| CustomersController.cs (xóa) | Integration tests sẽ fail (expected) | Tests sẽ được refactored trong W9-T2 |
| PRODUCTION_HYGIENE_master_plan.md (update status) | Không có reverse impact | Update task status to ✅ DONE |

## 9. TDD & E2E TESTING STRATEGY
- **No new tests needed:** Controller deletion không cần test mới
- **Verification only:** 
  - Grep search for references to controller
  - Build verification
  - Architecture tests verification
- **Test boundary:**
  - Unit tests: N/A
  - Integration tests: Will be updated in W9-T2
  - E2E tests: N/A

## 10. JIT PLANNING + PURE EXECUTION (MICRO-PHASES)

### Chiến lược thực thi: JIT Planning + Pure Execution

Delete controller file, verify no production references, update documentation.

### Micro-phase breakdown cho WAVE9 - Delete Controller

| Session | JIT Planning (chốt gì) | Pure Execution (viết gì) |
|---|---|---|
| **S1** | Verify no production references to CustomersController | Execute grep search, analyze results, confirm safe to delete |
| **S2** | Delete CustomersController.cs file | Execute file deletion, verify file removed |
| **S3** | Verify build and update documentation | Run dotnet build, update master plan status |

### Rules
- Verify references before deletion (safety first)
- Integration test failures expected - will be handled in W9-T2
- Update documentation immediately after completion

## 11. ESTIMATED EFFORT
- Medium effort - controller deletion with reference verification
- 3 sessions theo JIT Planning
- **BLOCKER:** Không có blockers
