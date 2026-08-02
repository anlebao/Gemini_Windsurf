# TASK CARD: TEST FIXING - WAVE 3 - SHOP API TESTS (PROGRAM CLASS VISIBILITY)

## 1. GOAL & CONTEXT
- **Mục tiêu cốt lõi:** Re-enable 8 Shop API integration tests currently disabled due to Program class visibility issue with top-level statements
- **Nghiệp vụ áp dụng:** Shop API integration testing - verify CRUD operations, statistics, search, and multi-tenant isolation for Shop entity

## 2. ACTIVE WORKFLOW ROUTING
- **Target Workflow:** `.devin/workflows/Fix_Tests.md`
- **Execution Mode:** FIX_ONLY_TESTS
- **Master Plan:** `docs/AI/tasks/fix_integration_tests_master_plan.md` (Wave 3)

## 3. RELEVANT FILES (CONTEXT BOUNDARY)
- **Files được phép đọc/sửa:**
  - `docs/AI/project_state.md` (Bắt buộc đọc đầu phiên)
  - `docs/AI/tasks/fix_integration_tests_master_plan.md` (Master plan reference)
  - `6_Tests/VanAn.Integration.Tests/Api/ShopApiIntegrationTests.cs` (8 disabled tests)
  - `6_Tests/VanAn.Integration.Tests/Infrastructure/HttpIntegrationTestBase.cs` (HTTP test base)
  - `6_Tests/VanAn.Integration.Tests/Infrastructure/CustomWebApplicationFactory.cs` (Test factory)
  - `5_WebApps/ShopERP/Program.cs` (ShopERP startup with top-level statements)
  - `2_Gateway/Controllers/ShopsController.cs` (Shop API endpoints)
  - `5_WebApps/ShopERP/Controllers/ShopsController.cs` (ShopERP Shop endpoints)
- **Boundary Rules (Nghiêm cấm):**
  - KHÔNG modify top-level statements structure trong Program.cs trừ khi absolutely necessary
  - KHÔNG bypass tests với skip/ignore permanent
  - KHÔNG modify production API controllers logic chỉ để pass test
  - KHÔNG sửa các test files khác không liên quan đến Shop API
  - KHÔNG weaken API business logic để test pass

## 4. TECHNICAL & REGULATORY CONSTRAINTS (HARDENING GATES)
- [ ] **Program Class Visibility:** Preserve top-level statements structure if possible
- [ ] **Test Infrastructure:** CustomWebApplicationFactory phải work với Program.cs structure
- [ ] **API Integrity:** Không modify Shop API business logic
- [ ] **Multi-tenancy:** Shop API tests phải verify tenant isolation properly
- [ ] **Test Completeness:** 8 tests phải be fully implemented, không partial fixes

## 5. SUCCESS CRITERIA (ĐO LƯỜNG ĐƯỢC)
- [ ] **SC1:** Program class visibility issue được resolve HOẶC alternative test factory implemented
- [ ] **SC2:** CustomWebApplicationFactory có thể create test client thành công
- [ ] **SC3:** API: Create Shop - Valid Request test passes
- [ ] **SC4:** API: Get Shop by ID - Valid Request test passes
- [ ] **SC5:** API: Update Shop Details - Valid Request test passes
- [ ] **SC6:** API: Delete Shop - Valid Request test passes
- [ ] **SC7:** API: Shop Statistics - Valid Request test passes
- [ ] **SC8:** API: Shop Search - Valid Request test passes
- [ ] **SC9:** API: Multi-Tenant Shop Isolation test passes
- [ ] **SC10:** API: Shop Orders - Valid Request test passes
- [ ] **SC11:** Tất cả 8 Shop API tests pass (8/8)
- [ ] **SC12:** Không còn `await Task.CompletedTask;` trong ShopApiIntegrationTests.cs
- [ ] **SC13:** Build: 0 errors
- [ ] **SC14:** Guard-check.ps1: PASS

**Implementation Date:** 2026-06-27
**Branch:** feature/fix-integration-wave3-shop-api

## 6. ACTIVE SKILLS (MAX 3)
- `test-system-upgrade` — Upgrade test infrastructure and fix test factory
- `pattern-based-fixing` — Apply consistent pattern across all 8 Shop API tests
- `build-error-analysis` — Analyze if Program.cs changes cause build issues

## 7. AI HEALTH CHECK MATRIX (INITIAL)
- **Evidence Count:** 2
- **Verified Facts:**
  - Fact 1: 8 Shop API tests intentionally disabled với `await Task.CompletedTask;`
  - Fact 2: Comment trong test: "Program class visibility issue with top-level statements"
- **Assumptions:**
  - CustomWebApplicationFactory không thể access Program class với top-level statements
  - Cần either make Program class accessible OR implement alternative test factory pattern
  - Shop API endpoints exist và working trong production code
- **Open Questions:**
  - Q1: Chính xác issue gì với Program class visibility và top-level statements?
  - Q2: CustomWebApplicationFactory đang require gì từ Program class?
  - Q3: Alternative approach nào viable (test factory pattern, manual WebHostBuilder, etc.)?
- **Recommended Action:** INVESTIGATE (JIT Planning Phase) — Understand exact Program class visibility issue before deciding approach
- **Planning Gate:** KHÔNG viết code cho đến khi Q1, Q2, Q3 được answer và detailed coding plan được chốt

## 8. REVERSE IMPACT ANALYSIS
| File thay đổi | Reverse impact | Mitigation |
|---|---|---|
| ShopApiIntegrationTests.cs | Chỉ ảnh hưởng 8 Shop API tests | Implement từng test, verify từng cái pass |
| CustomWebApplicationFactory.cs | Ảnh hưởng tất cả HTTP integration tests | Test thoroughly nếu cần modify |
| Program.cs (ShopERP) | Ảnh hưởng application startup | Chỉ modify nếu absolutely necessary, preserve top-level statements |
| HttpIntegrationTestBase.cs | Ảnh hưởng tất cả HTTP tests | Verify không break existing HTTP tests |

## 9. TDD & E2E TESTING STRATEGY
- **API Integration Test Strategy:**
  - Investigate Program class visibility issue depth
  - Decide approach: fix Program visibility OR implement alternative test factory
  - Re-enable tests từng cái một, verify từng cái pass
  - Ensure proper test data setup cho Shop entity CRUD operations
  - Verify multi-tenant isolation trong Shop API tests
- **Test boundary:**
  - Unit tests: Không ảnh hưởng
  - Integration tests: Fix 8 Shop API tests trong ShopApiIntegrationTests.cs
  - E2E tests: Không ảnh hưởng

## 10. JIT PLANNING + PURE EXECUTION (MICRO-PHASES)

### Chiến lược thực thi: JIT Planning + Pure Execution

**QUY TẮC SẮC:** KHÔNG code mò mẫm - Investigate trước, Implement sau

**Bước 1: INVESTIGATE & ANALYZE (Planning Phase)**
- Đọc comment: "Program class visibility issue with top-level statements"
- Đọc ShopERP/Program.cs structure (top-level statements)
- Đọc CustomWebApplicationFactory để hiểu nó cần gì từ Program class
- Research test patterns cho top-level statements
- Identify root cause: CustomWebApplicationFactory requirements vs Program.cs structure
- Lập detailed coding plan: fix Program visibility hay alternative factory?
- Chốt approach trước khi viết code

**Bước 2: IMPLEMENT (Execution Phase)**
- Thực hiện theo plan đã chốt
- Ưu tiên alternative test factory pattern trước khi modify Program.cs
- Nếu modify Program.cs: preserve top-level statements structure
- KHÔNG thay đổi approach khi đang implement
- Mỗi test re-enable xong, run test để verify

**Execution Flow:** Investigate Program class visibility issue → Decide approach (fix Program vs alternative factory) → Implement infrastructure fix → Re-enable tests sequentially → Verify all 8 tests pass

### Micro-phase breakdown cho Wave 3: Shop API Tests Fix

| Session | JIT Planning (chốt gì) | Pure Execution (viết gì) |
|---|---|---|
| **S1** | **PLANNING PHASE:** Investigate exact Program class visibility issue; Analyze CustomWebApplicationFactory requirements; Chốt detailed coding plan | **EXECUTION PHASE:** (Không có - S1 là planning only) |
| **S2** | (Skip - plan đã chốt từ S1) | **EXECUTION PHASE:** Implement chosen approach theo plan từ S1: modify Program.cs accessibility HOẶC create alternative test factory |
| **S3** | (Skip - pattern đã established từ S2) | **EXECUTION PHASE:** Re-enable first 2 Shop API tests (Create Shop, Get Shop by ID); Verify 2 tests pass |
| **S4** | (Skip - pattern đã established) | **EXECUTION PHASE:** Re-enable next 3 Shop API tests (Update Shop, Delete Shop, Shop Statistics); Verify 3 tests pass |
| **S5** | (Skip - pattern đã established) | **EXECUTION PHASE:** Re-enable final 3 Shop API tests (Shop Search, Multi-Tenant Isolation, Shop Orders); Verify 3 tests pass; Run full Shop API test suite |
| **S6** | (Skip - verification only) | **EXECUTION PHASE:** Run full integration test suite (144 tests); Verify không có regressions; Commit wave 3 changes |

### Rules
- **S1 là PLANNING ONLY:** Không viết code, chỉ investigate và lập plan
- **S2-S6 là EXECUTION ONLY:** Thực hiện theo plan đã chốt, KHÔNG thay đổi approach
- Mỗi session chỉ làm 1 micro-phase, không跳步
- Ưu tiên alternative test factory pattern trước khi modify Program.cs
- Nếu cần modify Program.cs, preserve top-level statements structure
- Implement tests từng cái một, verify từng cái pass trước khi move tiếp
- Ensure proper test data setup cho Shop entity
- Verify multi-tenant isolation trong tests

## 11. ESTIMATED EFFORT
- 3-4 sessions theo JIT Planning
- Mỗi session 30-45 minutes
- **BLOCKER:** Nếu Program class modification yêu cầu architectural changes → STOP và request approval
