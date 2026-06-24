# TASK CARD: PRODUCTION_HYGIENE - WAVE12 - Write Authorization Integration Tests

## 1. GOAL & CONTEXT
- **Mục tiêu cốt lõi:** Viết integration tests cho authorization enforcement
- **Nghiệp vụ áp dụng:** Test coverage - verify authorization works correctly

## 2. ACTIVE WORKFLOW ROUTING
- **Target Workflow:** `Test writing workflow`
- **Execution Mode:** FIX_ONLY_TESTS

## 3. RELEVANT FILES (CONTEXT BOUNDARY)
- **Files được phép đọc/sửa:**
  - `docs/AI/project_state.md` (Bắt buộc đọc đầu phiên)
  - `6_Tests/VanAn.Integration.Tests/Api/` (tạo mới tests)
  - `6_Tests/VanAn.Integration.Tests/Infrastructure/` (nếu cần)
  - `docs/AI/tasks/PRODUCTION_HYGIENE_master_plan.md` (cập nhật status)
- **Boundary Rules (Nghiêm cấm):**
  - KHÔNG sửa production code (Gateway, ShopERP, CoreHub)
  - KHÔNG sửa unit tests khác
  - Chỉ viết integration tests cho authorization

## 4. TECHNICAL & REGULATORY CONSTRAINTS (HARDENING GATES)
- [ ] **Test Coverage:** Tests phải verify authorization enforcement
- [ ] **Test Patterns:** Sử dụng existing integration test patterns
- [ ] **Minimal Scope:** Chỉ test authorization, không test business logic
- [ ] **Build Verification:** `dotnet build VanAn.sln` phải PASS sau khi viết tests
- [ ] **Test Execution:** Integration tests phải PASS

## 5. SUCCESS CRITERIA (ĐO LƯỜNG ĐƯỢC)
- [ ] **SC1:** Integration tests cho authorization enforcement đã viết
- [ ] **SC2:** Tests verify unauthorized requests rejected (401/403)
- [ ] **SC3:** Tests verify authorized requests accepted (200)
- [ ] **SC4:** `dotnet build VanAn.sln` → 0 errors, 0 warnings mới
- [ ] **SC5:** `guard-check.ps1` → PASS
- [ ] **SC6:** `VanAn.Architecture.Tests`: 7/7 PASS
- [ ] **SC7:** `VanAn.Integration.Tests`: authorization tests PASS
- [ ] **SC8:** PRODUCTION_HYGIENE_master_plan.md updated với W12-T5 status = ✅ DONE

**Implementation Date:** 2026-06-24
**Branch:** feature/wave12-api-authorization

## 6. ACTIVE SKILLS (MAX 3)
- `test-system-upgrade` — Write integration tests for authorization
- `pattern-based-fixing` — Follow existing test patterns
- `build-error-analysis` — Verify build passes after test changes

## 7. AI HEALTH CHECK MATRIX (INITIAL)
- **Evidence Count:** 2
- **Verified Facts:**
  - Fact 1: Authorization fixes applied in W12-T3, W12-T4
  - Fact 2: Need test coverage for authorization enforcement
- **Assumptions:**
  - Existing integration test patterns can be reused
- **Open Questions:**
  - Q1: Có bao nhiêu endpoints cần test?
  - Q2: Test pattern nào phù hợp?
- **Recommended Action:** Write integration tests for authorization enforcement

## 8. REVERSE IMPACT ANALYSIS
| File thay đổi | Reverse impact | Mitigation |
|---|---|---|
| Integration tests (tạo mới) | Không có reverse impact | Tests verify authorization works |
| PRODUCTION_HYGIENE_master_plan.md (update status) | Không có reverse impact | Update task status to ✅ DONE |

## 9. TDD & E2E TESTING STRATEGY
- **Test Strategy:** 
  - Test unauthorized requests → expect 401/403
  - Test authorized requests → expect 200
  - Test different roles/tenants if applicable
- **Test boundary:**
  - Unit tests: N/A
  - Integration tests: Write authorization tests
  - E2E tests: N/A

## 10. JIT PLANNING + PURE EXECUTION (MICRO-PHASES)

### Chiến lược thực thi: JIT Planning + Pure Execution

Write integration tests for authorization enforcement, verify tests pass.

### Micro-phase breakdown cho WAVE12 - Write Authorization Tests

| Session | JIT Planning (chốt gì) | Pure Execution (viết gì) |
|---|---|---|
| **S1** | Review existing integration test patterns | Study existing integration tests, identify patterns |
| **S2** | Write authorization tests | Create test class, write test cases for auth |
| **S3** | Verify tests pass | Run integration tests, verify all pass |
| **S4** | Update documentation | Update master plan status |

### Rules
- Follow existing integration test patterns
- Test both authorized and unauthorized scenarios
- Update documentation immediately after completion

## 11. ESTIMATED EFFORT
- Medium effort - test writing + verification
- 3 sessions theo JIT Planning
- **BLOCKER:** Không có blockers
