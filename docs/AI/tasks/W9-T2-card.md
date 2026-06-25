# TASK CARD: PRODUCTION_HYGIENE - WAVE9 - Refactor Integration Tests

## 1. GOAL & CONTEXT
- **Mục tiêu cốt lõi:** Refactor CustomerApiIntegrationTests để test Gateway endpoints hoặc xóa tests
- **Nghiệp vụ áp dụng:** Test architecture fix - tests should reflect production flow

## 2. ACTIVE WORKFLOW ROUTING
- **Target Workflow:** `Test refactoring workflow`
- **Execution Mode:** FIX_ONLY

## 3. RELEVANT FILES (CONTEXT BOUNDARY)
- **Files được phép đọc/sửa:**
  - `docs/AI/project_state.md` (Bắt buộc đọc đầu phiên)
  - `6_Tests/VanAn.Integration.Tests/Api/CustomerApiIntegrationTests.cs` (refactor hoặc xóa)
  - `6_Tests/VanAn.Integration.Tests/Infrastructure/HttpIntegrationTestBase.cs` (nếu cần)
  - `docs/AI/tasks/PRODUCTION_HYGIENE_master_plan.md` (cập nhật status)
- **Boundary Rules (Nghiêm cấm):**
  - KHÔNG sửa production code (ShopERP, Gateway, CoreHub)
  - KHÔNG sửa unit tests khác
  - KHÔNG sửa E2E tests

## 4. TECHNICAL & REGULATORY CONSTRAINTS (HARDENING GATES)
- [ ] **Architecture Alignment:** Tests phải test production flow (KhachLink → Gateway → CoreHub)
- [ ] **Test Coverage:** Giữ test coverage cho customer API operations nếu cần
- [ ] **Clean Removal:** Nếu xóa tests, đảm bảo không có broken test references
- [ ] **Build Verification:** `dotnet build VanAn.sln` phải PASS sau khi refactor
- [ ] **Test Execution:** Integration tests phải PASS sau khi refactor

## 5. SUCCESS CRITERIA (ĐO LƯỜNG ĐƯỢC)
- [ ] **SC1:** CustomerApiIntegrationTests refactored hoặc xóa
- [ ] **SC2:** Tests test Gateway endpoints nếu refactor (production flow)
- [ ] **SC3:** `dotnet build VanAn.sln` → 0 errors, 0 warnings mới
- [ ] **SC4:** `guard-check.ps1` → PASS
- [ ] **SC5:** `VanAn.Architecture.Tests`: 7/7 PASS
- [ ] **SC6:** `VanAn.Integration.Tests`: tests PASS hoặc removed cleanly
- [ ] **SC7:** PRODUCTION_HYGIENE_master_plan.md updated với W9-T2 status = ✅ DONE
- [ ] **SC8:** Test architecture reflects production flow

**Implementation Date:** 2026-06-24
**Branch:** feature/wave9-cleanup-controller

## 6. ACTIVE SKILLS (MAX 3)
- `test-system-upgrade` — Refactor tests to use correct architecture
- `build-error-analysis` — Verify build passes after test changes
- `pattern-based-fixing` — Apply consistent test patterns

## 7. AI HEALTH CHECK MATRIX (INITIAL)
- **Evidence Count:** 4
- **Verified Facts:**
  - Fact 1: CustomerApiIntegrationTests test ShopERP directly
  - Fact 2: Production flow is KhachLink → Gateway → CoreHub
  - Fact 3: Tests test wrong architecture
  - Fact 4: ShopERP CustomersController will be deleted in W9-T1
- **Assumptions:**
  - Gateway has equivalent endpoints or tests can be removed
- **Open Questions:**
  - Q1: Does Gateway have equivalent customer API endpoints?
  - Q2: Should tests be refactored or removed?
- **Recommended Action:** Investigate Gateway endpoints, decide refactor vs remove

## 8. REVERSE IMPACT ANALYSIS
| File thay đổi | Reverse impact | Mitigation |
|---|---|---|
| CustomerApiIntegrationTests.cs (refactor/xóa) | Test coverage changes | Ensure customer API still tested if needed |
| HttpIntegrationTestBase.cs (nếu cần) | Test infrastructure changes | Keep test infrastructure consistent |

## 9. TDD & E2E TESTING STRATEGY
- **Test Strategy:** 
  - Option 1: Refactor tests to call Gateway endpoints instead of ShopERP
  - Option 2: Remove tests if Gateway doesn't have equivalent endpoints
- **Test boundary:**
  - Unit tests: N/A
  - Integration tests: Refactor or remove CustomerApiIntegrationTests
  - E2E tests: N/A

## 10. JIT PLANNING + PURE EXECUTION (MICRO-PHASES)

### Chiến lược thực thi: JIT Planning + Pure Execution

Investigate Gateway endpoints, decide between refactor vs remove, execute accordingly.

### Micro-phase breakdown cho WAVE9 - Refactor Tests

| Session | JIT Planning (chốt gì) | Pure Execution (viết gì) |
|---|---|---|
| **S1** | Investigate Gateway endpoints for customer API | Check Gateway Controllers, identify equivalent endpoints |
| **S2** | Decide refactor vs remove tests | Based on investigation, choose refactor or remove |
| **S3** | Execute refactor or removal | Update tests accordingly, verify build |
| **S4** | Verify tests and update documentation | Run tests, update master plan status |

### Rules
- Investigate before deciding refactor vs remove
- Keep test coverage if Gateway has equivalent endpoints
- Update documentation immediately after completion

## 11. ESTIMATED EFFORT
- Medium effort - investigation + decision + implementation
- 4 sessions theo JIT Planning
- **BLOCKER:** Không có blockers
