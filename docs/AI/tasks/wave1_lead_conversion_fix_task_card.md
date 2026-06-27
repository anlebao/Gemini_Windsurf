# TASK CARD: TEST FIXING - WAVE 1 - LEAD CONVERSION TESTS (FOREIGN KEY CONSTRAINTS)

## 1. GOAL & CONTEXT
- **Mục tiêu cốt lõi:** Fix 5 Lead Conversion integration tests failing with SQLite FOREIGN KEY constraint errors
- **Nghiệp vụ áp dụng:** Lead to Customer Conversion workflow - core business logic for converting qualified leads to customers with loyalty rewards and onboarding

## 2. ACTIVE WORKFLOW ROUTING
- **Target Workflow:** `.devin/workflows/Fix_Tests.md`
- **Execution Mode:** FIX_ONLY_TESTS
- **Master Plan:** `docs/AI/tasks/fix_integration_tests_master_plan.md` (Wave 1)

## 3. RELEVANT FILES (CONTEXT BOUNDARY)
- **Files được phép đọc/sửa:**
  - `docs/AI/project_state.md` (Bắt buộc đọc đầu phiên)
  - `docs/AI/tasks/fix_integration_tests_master_plan.md` (Master plan reference)
  - `6_Tests/VanAn.Integration.Tests/LeadToCustomerConversionTests.cs` (Failing tests)
  - `6_Tests/VanAn.Integration.Tests/Infrastructure/TestEntityBuilder.cs` (Test data builder)
  - `3_CoreHub/Domain/Entities.cs` (Lead entity definition)
  - `3_CoreHub/Infrastructure/Configurations/LeadConfiguration.cs` (EF Core configuration)
  - `3_CoreHub/Infrastructure/Configurations/CustomerConfiguration.cs` (Customer FK relationships)
  - `3_CoreHub/Infrastructure/Configurations/CustomerOnboardingConfiguration.cs` (Onboarding FK)
  - `3_CoreHub/Infrastructure/Configurations/LoyaltyRewardsConfiguration.cs` (Loyalty FK)
- **Boundary Rules (Nghiêm cấm):**
  - KHÔNG sửa Domain entities (Lead, Customer, etc.) trừ khi phát hiện modeling defect thực sự
  - KHÔNG sửa production services logic chỉ để pass test
  - KHÔNG bypass foreign key constraints với reflection tricks
  - KHÔNG weaken assertions để test pass
  - KHÔNG sửa các test files khác không liên quan đến Lead Conversion

## 4. TECHNICAL & REGULATORY CONSTRAINTS (HARDENING GATES)
- [ ] **Domain Integrity:** Lead entity phải remain immutable pattern, chỉ sửa test data builder
- [ ] **Foreign Key Constraints:** SQLite FK constraints phải được respect, không bypass
- [ ] **Test Data Builder:** TestEntityBuilder.CreateLead() phải tạo valid Lead entity với tất cả required properties
- [ ] **Multi-tenancy:** Tất cả test data phải sử dụng TestTenantId đúng như IntegrationTestBase
- [ ] **Test Isolation:** Mỗi test phải independent, không phụ thuộc data từ test khác

## 5. SUCCESS CRITERIA (ĐO LƯỜNG ĐƯỢC)
- [ ] **SC1:** Lead entity foreign key relationships được identify và document
- [ ] **SC2:** TestEntityBuilder.CreateLead() tạo Lead entity valid với tất cả required properties
- [ ] **SC3:** LeadConversion_Flow_ShouldCreateCustomerWithLoyalty test passes
- [ ] **SC4:** LeadConversion_Failed_ShouldRollbackChanges test passes (line 182 fixed)
- [ ] **SC5:** LeadConversion_ValidateLead_ShouldCheckQualification test passes
- [ ] **SC6:** LeadConversion_WithOrders_ShouldImportOrderHistory test passes
- [ ] **SC7:** LeadConversion_Batch_ShouldProcessMultipleLeads test passes
- [ ] **SC8:** Tất cả 5 Lead conversion tests pass (5/5)
- [ ] **SC9:** Không có test failures mới được introduce
- [ ] **SC10:** Build: 0 errors
- [ ] **SC11:** Guard-check.ps1: PASS
- [ ] **SC12:** Domain integrity maintained (không có Domain.cs modifications)

**Implementation Date:** 2026-06-27
**Branch:** feature/fix-integration-wave1-lead-conversion

## 6. ACTIVE SKILLS (MAX 3)
- `test-system-upgrade` — Upgrade test infrastructure and fix test data builders
- `pattern-based-fixing` — Identify and apply consistent fix pattern across all 5 tests
- `domain-integrity-validation` — Ensure domain entities remain pure and immutable

## 7. AI HEALTH CHECK MATRIX (INITIAL)
- **Evidence Count:** 3
- **Verified Facts:**
  - Fact 1: 5 Lead conversion tests failing with "SQLite Error 19: FOREIGN KEY constraint failed"
  - Fact 2: TestEntityBuilder.CreateLead() uses reflection to bypass protected constructor
  - Fact 3: Lead entity has foreign key relationships to Customer, CustomerOnboarding, LoyaltyRewards
- **Assumptions:**
  - TestEntityBuilder.CreateLead() không properly initialize tất cả required properties cho FK relationships
  - Lead entity có FK constraints đến Customer, LoyaltyRewards, hoặc CustomerOnboarding tables
  - SQLite in-memory database enforce FK constraints giống production database
- **Open Questions:**
  - Q1: Chính xác foreign key relationship nào đang fail? (Lead → Customer? Lead → LoyaltyRewards?)
  - Q2: TestEntityBuilder.CreateLead() đang thiếu property nào?
  - Q3: Có cần sửa test setup logic hay chỉ cần sửa TestEntityBuilder?
- **Recommended Action:** INVESTIGATE (JIT Planning Phase) — Determine exact FK relationship causing failure before fixing
- **Planning Gate:** KHÔNG viết code cho đến khi Q1, Q2, Q3 được answer và detailed coding plan được chốt

## 8. REVERSE IMPACT ANALYSIS
| File thay đổi | Reverse impact | Mitigation |
|---|---|---|
| TestEntityBuilder.cs | Ảnh hưởng tất cả tests dùng CreateLead() | Verify không break tests khác đang pass |
| LeadToCustomerConversionTests.cs | Chỉ ảnh hưởng 5 Lead conversion tests | Run full test suite sau khi fix |
| LeadConfiguration.cs | Ảnh hưởng tất cả Lead entity operations | Chỉ sửa nếu absolutely necessary, ưu tiên test fix |

## 9. TDD & E2E TESTING STRATEGY
- **Integration Test Fix Strategy:**
  - Fix test data builder trước (TestEntityBuilder.CreateLead())
  - Apply pattern từ 1 test passing rồi apply cho 4 tests còn lại
  - Verify từng test pass trước khi move đến test tiếp theo
  - Run full Lead conversion test suite sau khi fix xong tất cả
- **Test boundary:**
  - Unit tests: Không ảnh hưởng (domain tests separate)
  - Integration tests: Fix 5 Lead conversion tests trong VanAn.Integration.Tests
  - E2E tests: Không ảnh hưởng (Playwright tests separate)

## 10. JIT PLANNING + PURE EXECUTION (MICRO-PHASES)

### Chiến lược thực thi: JIT Planning + Pure Execution

**QUY TẮC SẮC:** KHÔNG code mò mẫm - Investigate trước, Implement sau

**Bước 1: INVESTIGATE & ANALYZE (Planning Phase)**
- Đọc error messages: "SQLite Error 19: FOREIGN KEY constraint failed"
- Đọc Lead entity configuration để hiểu FK relationships
- Đọc TestEntityBuilder.CreateLead() để hiểu hiện tại đang setup gì
- Identify chính xác FK relationship nào đang fail
- Lập detailed coding plan: sửa property nào, thêm gì, remove gì
- Chốt approach trước khi viết code

**Bước 2: IMPLEMENT (Execution Phase)**
- Thực hiện sửa TestEntityBuilder.CreateLead() theo plan
- KHÔNG thay đổi approach khi đang implement
- Mỗi sửa xong, run test để verify
- Nếu test fail theo cách khác, DỪNG LẠI và quay lại Planning Phase

**Execution Flow:** Investigate exact FK relationship failure → Fix TestEntityBuilder.CreateLead() → Apply fix to each test sequentially → Verify all 5 tests pass

### Micro-phase breakdown cho Wave 1: Lead Conversion Tests Fix

| Session | JIT Planning (chốt gì) | Pure Execution (viết gì) |
|---|---|---|
| **S1** | **PLANNING PHASE:** Investigate exact FK relationship causing failure, identify missing properties in TestEntityBuilder.CreateLead(), chốt detailed coding plan | **EXECUTION PHASE:** (Không có - S1 là planning only) |
| **S2** | (Skip - plan đã chốt từ S1) | **EXECUTION PHASE:** Fix TestEntityBuilder.CreateLead() theo plan từ S1; Verify fix với 1 test đầu tiên |
| **S3** | (Skip - pattern đã established từ S2) | **EXECUTION PHASE:** Apply pattern từ test đầu tiên sang 4 tests còn lại; Run full Lead conversion test suite |
| **S4** | (Skip - verification only) | **EXECUTION PHASE:** Run full integration test suite (130+ tests); Verify không có regressions; Commit wave 1 changes |

### Rules
- **S1 là PLANNING ONLY:** Không viết code, chỉ investigate và lập plan
- **S2-S4 là EXECUTION ONLY:** Thực hiện theo plan đã chốt, KHÔNG thay đổi approach
- Mỗi session chỉ làm 1 micro-phase, không跳步
- Sau mỗi session, run targeted tests để verify fix
- Nếu test failure tăng >3, DỪNG EXECUTION và quay lại PLANNING
- KHÔNG modify Domain entities trừ khi có approval
- Document mỗi FK relationship được identify

## 11. ESTIMATED EFFORT
- 2-3 sessions theo JIT Planning
- Mỗi session 30-45 minutes
- **BLOCKER:** Nếu cần modify Domain entities → STOP và request approval
