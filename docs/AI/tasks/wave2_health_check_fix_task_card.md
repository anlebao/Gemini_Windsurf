# TASK CARD: TEST FIXING - WAVE 2 - HEALTH CHECK ENDPOINT TEST

## 1. GOAL & CONTEXT
- **Mục tiêu cốt lõi:** Fix Golden Flow Health Check test failing due to missing or inaccessible /health endpoint
- **Nghiệp vụ áp dụng:** Infrastructure health verification - ensure application services are running and accessible

## 2. ACTIVE WORKFLOW ROUTING
- **Target Workflow:** `.devin/workflows/Fix_Tests.md`
- **Execution Mode:** FIX_ONLY_TESTS
- **Master Plan:** `docs/AI/tasks/fix_integration_tests_master_plan.md` (Wave 2)

## 3. RELEVANT FILES (CONTEXT BOUNDARY)
- **Files được phép đọc/sửa:**
  - `docs/AI/project_state.md` (Bắt buộc đọc đầu phiên)
  - `docs/AI/tasks/fix_integration_tests_master_plan.md` (Master plan reference)
  - `6_Tests/VanAn.Integration.Tests/GoldenFlowSystemTests.cs` (Failing health check test)
  - `6_Tests/VanAn.Integration.Tests/Infrastructure/CustomWebApplicationFactory.cs` (Test factory)
  - `5_WebApps/ShopERP/Program.cs` (ShopERP startup configuration)
  - `2_Gateway/Program.cs` (Gateway startup configuration)
- **Boundary Rules (Nghiêm cấm):**
  - KHÔNG sửa production business logic chỉ để pass test
  - KHÔNG add complex health check logic nếu không cần thiết
  - KHÔNG modify test infrastructure files khác không liên quan
  - KHÔNG bypass test với skip/ignore
  - KHÔNG sửa các test files khác trong GoldenFlowSystemTests.cs

## 4. TECHNICAL & REGULATORY CONSTRAINTS (HARDENING GATES)
- [ ] **Minimal Production Change:** Chỉ add minimal health endpoint nếu không tồn tại
- [ ] **Test Robustness:** Test phải handle cả trường hợp endpoint exists và missing
- [ ] **Infrastructure Integrity:** Không break existing ShopERP/Gateway functionality
- [ ] **Test Isolation:** Health check test phải independent, không phụ thuộc test khác
- [ ] **Performance:** Health check phải respond nhanh (< 5s)

## 5. SUCCESS CRITERIA (ĐO LƯỜNG ĐƯỢC)
- [ ] **SC1:** /health endpoint existence được verify trong ShopERP hoặc Gateway
- [ ] **SC2:** Test properly handle cả trường hợp endpoint exists và missing scenarios
- [ ] **SC3:** GoldenFlow_HealthCheck_ReturnsHealthy test passes
- [ ] **SC4:** Test completes trong reasonable time (< 5s)
- [ ] **SC5:** Không có test failures mới được introduce
- [ ] **SC6:** Build: 0 errors
- [ ] **SC7:** Guard-check.ps1: PASS
- [ ] **SC8:** Production code changes minimized (chỉ health endpoint nếu cần)
- [ ] **SC9:** Test fallback logic properly handles 404 as acceptable
- [ ] **SC10:** CustomWebApplicationFactory có thể create test client thành công
- [ ] **SC11:** Health endpoint (nếu được add) có proper logging và error handling
- [ ] **SC12:** Documentation cập nhật nếu health endpoint được add

**Implementation Date:** 2026-06-27
**Branch:** feature/fix-integration-wave2-health-check

## 6. ACTIVE SKILLS (MAX 3)
- `test-system-upgrade` — Upgrade test infrastructure and fix test setup
- `pattern-based-fixing` — Apply consistent pattern for handling missing endpoints
- `build-error-analysis` — Analyze if health endpoint addition causes build issues

## 7. AI HEALTH CHECK MATRIX (INITIAL)
- **Evidence Count:** 2
- **Verified Facts:**
  - Fact 1: GoldenFlow_HealthCheck_ReturnsHealthy test failing với timeout/error
  - Fact 2: Test đã có fallback logic cho 404 nhưng vẫn fail
- **Assumptions:**
  - /health endpoint có thể không exist trong ShopERP hoặc Gateway
  - CustomWebApplicationFactory có thể không properly configure cho health endpoint
  - Test có thể đang timeout thay vì return 404
- **Open Questions:**
  - Q1: /health endpoint exist trong ShopERP hay Gateway?
  - Q2: Test đang fail do timeout hay do endpoint không exist?
  - Q3: Cần add health endpoint hay chỉ cần improve test fallback logic?
- **Recommended Action:** INVESTIGATE (JIT Planning Phase) — Verify health endpoint existence before deciding approach
- **Planning Gate:** KHÔNG viết code cho đến khi Q1, Q2, Q3 được answer và detailed coding plan được chốt

## 8. REVERSE IMPACT ANALYSIS
| File thay đổi | Reverse impact | Mitigation |
|---|---|---|
| GoldenFlowSystemTests.cs | Chỉ ảnh hưởng 1 health check test | Verify test logic robust |
| Program.cs (ShopERP/Gateway) | Ảnh hưởng application startup | Chỉ add minimal health endpoint nếu cần |
| CustomWebApplicationFactory.cs | Ảnh hưởng tất cả tests dùng factory | Test thoroughly nếu cần modify |

## 9. TDD & E2E TESTING STRATEGY
- **Infrastructure Test Fix Strategy:**
  - Verify health endpoint existence trong production code
  - Nếu không exist, add minimal health endpoint
  - Nếu exist, fix test để properly handle response
  - Improve test fallback logic để handle 404 gracefully
- **Test boundary:**
  - Unit tests: Không ảnh hưởng
  - Integration tests: Fix 1 health check test trong GoldenFlowSystemTests.cs
  - E2E tests: Không ảnh hưởng

## 10. JIT PLANNING + PURE EXECUTION (MICRO-PHASES)

### Chiến lược thực thi: JIT Planning + Pure Execution

**QUY TẮC SẮC:** KHÔNG code mò mẫm - Investigate trước, Implement sau

**Bước 1: INVESTIGATE & ANALYZE (Planning Phase)**
- Đọc test failure: timeout hay 404?
- Đọc ShopERP/Program.cs và Gateway/Program.cs để check /health endpoint
- Đọc CustomWebApplicationFactory để hiểu test setup
- Identify root cause: endpoint không exist hay test setup sai?
- Lập detailed coding plan: add endpoint hay improve test logic?
- Chốt approach trước khi viết code

**Bước 2: IMPLEMENT (Execution Phase)**
- Thực hiện theo plan đã chốt
- Nếu add health endpoint: keep minimal, chỉ return 200 OK
- Nếu improve test logic: enhance fallback logic
- KHÔNG thay đổi approach khi đang implement
- Mỗi sửa xong, run test để verify

**Execution Flow:** Verify health endpoint existence → Decide approach (add endpoint vs fix test) → Implement fix → Verify test passes

### Micro-phase breakdown cho Wave 2: Health Check Test Fix

| Session | JIT Planning (chốt gì) | Pure Execution (viết gì) |
|---|---|---|
| **S1** | **PLANNING PHASE:** Verify /health endpoint existence trong ShopERP và Gateway; Analyze exact test failure reason (timeout vs 404); Chốt detailed coding plan | **EXECUTION PHASE:** (Không có - S1 là planning only) |
| **S2** | (Skip - plan đã chốt từ S1) | **EXECUTION PHASE:** Implement chosen approach theo plan từ S1: add minimal /health endpoint HOẶC improve test fallback logic |
| **S3** | (Skip - verification only) | **EXECUTION PHASE:** Run GoldenFlow_HealthCheck_ReturnsHealthy test; Verify test passes < 5s; Run full integration test suite; Commit wave 2 changes |

### Rules
- **S1 là PLANNING ONLY:** Không viết code, chỉ investigate và lập plan
- **S2-S3 là EXECUTION ONLY:** Thực hiện theo plan đã chốt, KHÔNG thay đổi approach
- Mỗi session chỉ làm 1 micro-phase, không跳步
- Ưu tiên improve test fallback logic trước khi modify production code
- Nếu cần add health endpoint, keep it minimal (return 200 OK)
- Document nếu health endpoint được add
- Verify không break existing functionality

## 11. ESTIMATED EFFORT
- 1-2 sessions theo JIT Planning
- Mỗi session 20-30 minutes
- **BLOCKER:** Nếu health endpoint addition yêu cầu complex changes → STOP và re-evaluate
