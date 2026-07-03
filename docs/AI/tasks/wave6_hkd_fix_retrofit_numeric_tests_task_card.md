# TASK CARD: HKD Book Fix - Wave 6 - Retrofit Tests with Numeric Assertions

## 1. GOAL & CONTEXT
- **Mục tiêu cốt lõi:** Update 3 test `GenerateS*BookAsync` hiện có (chỉ assert metadata) + add 5 test mới — tất cả assert `result.NumericValues["<field>"]` cụ thể. Fix Issue 4 (test pass trắng, che lấp bug)
- **Nghiệp vụ áp dụng:** Retrofit TDD per governance (EXISTING code: retrofit tests before completion)
- **Status:** PENDING — Planning & Approval
- **Branch:** `feature/hkd-fix-wave6-retrofit-numeric-tests`
- **Estimated Sessions:** 1-2

---

## 2. ACTIVE WORKFLOW ROUTING
- **Target Workflow:** `newfeaturebuild.md` (IMPLEMENT phase — test only)
- **Execution Mode:** IMPLEMENT
- **Current Phase:** Wave 6 of 9
- **Dependency:** Wave 4 (NumericValues có số liệu), Wave 5 (formulas đúng), Wave 2 (JournalEntries có data)

---

## 3. RELEVANT FILES (CONTEXT BOUNDARY)

### Files được phép đọc/sửa
- `docs/AI/project_state.md` (Bắt buộc đọc đầu phiên)
- `docs/AI/tasks/hkd_book_accounting_fix_master_plan.md` (READ)
- `6_Tests/VanAn.Core.Tests/Services/HKDBookServiceTests.cs` (UPDATE — 3 test existing + 5 test new)
- `6_Tests/VanAn.Core.Tests/Accounting/HKDBookServiceTests.cs` (READ — reference test patterns)
- `6_Tests/VanAn.Core.Tests/TestInfrastructure/TestEntityBuilder.cs` (READ — verify builder API)
- `3_CoreHub/Services/HKDBookService.cs` (READ — verify constructor sau Wave 4)
- `1_Shared/Domain/HKDTemplates.cs` (READ — verify field names per template)

### Boundary Rules (Nghiêm cấm)
- KHÔNG sửa production code — chỉ sửa test
- KHÔNG xóa test cũ — update assertion thêm (giữ metadata assert + thêm numeric assert)
- KHÔNG sửa `TestEntityBuilder` — dùng API hiện có
- KHÔNG tạo test cho template không có (chỉ 7 template: S1a, S2a-S2e, S3a)

---

## 4. TECHNICAL & REGULATORY CONSTRAINTS (HARDENING GATES)
- [ ] **Numeric Assertion:** Mỗi test `GenerateS*BookAsync` phải assert ít nhất 2 `NumericValues` entries
- [ ] **Seed Data:** Test phải seed `JournalEntries` (qua mock `IHKDBookRepository` hoặc `IHKDBookGenerationService` mock) với account numbers đúng
- [ ] **Regression Test:** W6-T8 verify `NumericValues` không rỗng (regression cho Issue 1)
- [ ] **Mock Update:** `HKDBookService` constructor sau Wave 4 inject `IHKDBookGenerationService` — test phải mock service này
- [ ] **Build Check:** `dotnet build VanAn.sln` Release pass
- [ ] **Test Check:** `dotnet test` — all pass (8 test mới/updated + cũ)

---

## 5. SUCCESS CRITERIA (ĐO LƯỜNG ĐƯỢC)
- [ ] **SC1:** `GenerateS1aBookAsync_ShouldGenerateBook_WhenTenantIsHKDGroup1` assert `NumericValues["TotalRevenue"]`, `["TotalExpense"]`, `["NetProfit"]`
- [ ] **SC2:** `GenerateS2aBookAsync_ShouldGenerateBook_WhenTenantIsHKDGroup2` assert `NumericValues["TotalRevenue"]`, `["VatAmount"]`, `["PersonalIncomeTax"]`, `["NetRevenue"]`
- [ ] **SC3:** `GenerateS2bBookAsync_ShouldGenerateRevenueBook_WhenTenantIsHKDGroup2` assert `NumericValues["SalesRevenue"]`, `["ServiceRevenue"]`, `["TotalRevenue"]`
- [ ] **SC4:** New test `GenerateS2cBookAsync_ShouldCalculateGrossProfitAndNetProfit` pass
- [ ] **SC5:** New test `GenerateS2dBookAsync_ShouldCalculateInventoryTotals` pass
- [ ] **SC6:** New test `GenerateS2eBookAsync_ShouldCalculateCashTotals` pass
- [ ] **SC7:** New test `GenerateS3aBookAsync_ShouldGenerateTrialBalanceBook` pass
- [ ] **SC8:** New regression test `GenerateS1aBook_NumericValues_ShouldNotBeEmpty_AfterWave4Fix` pass
- [ ] **SC9:** `dotnet build VanAn.sln` Release — 0 errors
- [ ] **SC10:** `dotnet test` — all pass
- [ ] **SC11:** guard-check.ps1 PASSED

---

## 6. ACTIVE SKILLS (MAX 3)
- `test-system-upgrade` — Retrofit test patterns
- `pattern-based-fixing` — Apply cùng assertion pattern cho 7 template
- `domain-integrity-validation` — Verify numeric values đúng công thức

---

## 7. AI HEALTH CHECK MATRIX (INITIAL)
- **Evidence Count:** 5
- **Verified Facts:**
  - Fact 1: `HKDBookServiceTests` (Services/) L41-93 — 3 test `GenerateS*BookAsync` chỉ assert `BookTypeCode`, `TenantId`, `Period`, `Entries.Count`
  - Fact 2: `HKDBookServiceTests` (Accounting/) L30-313 — 0 test `GenerateS*BookAsync`, chỉ test RecordRevenue/Expense/GetTotals
  - Fact 3: `TestEntityBuilder.CreateAccountingEntry` tồn tại (dùng trong test hiện có)
  - Fact 4: `HKDBookService` constructor sau Wave 4 inject `IHKDBookGenerationService` — test phải mock thêm
  - Fact 5: 7 template field names (từ HKDTemplates.cs): S1a (TotalRevenue, TotalExpense, NetProfit), S2a (TotalRevenue, VatAmount, PersonalIncomeTax, NetRevenue), S2b (SalesRevenue, ServiceRevenue, TotalRevenue), S2c (Revenue, CostOfGoodsSold, OperatingExpenses, NetProfit), S2d (Materials, Tools, Products, Goods, TotalInventory), S2e (cash fields), S3a (trial balance fields)
- **Assumptions:**
  - Mock `IHKDBookGenerationService.GenerateBookAsync` return `GenericHKDBook` với `NumericValues` populated (test setup)
  - Hoặc: integration test approach (seed DB, call real service) — decide per test
- **Open Questions:**
  - Q1: Test approach — mock `IHKDBookGenerationService` (unit) hay seed DB + real service (integration)? (Likely mock for unit, integration ở Wave 7)
  - Q2: `TestEntityBuilder` có method tạo `JournalEntry` không? (Verify)
- **Recommended Action:** PROCEED — risk thấp, chỉ sửa test

---

## 8. REVERSE IMPACT ANALYSIS
| File thay đổi | Reverse impact | Mitigation |
|---|---|---|
| `HKDBookServiceTests` (Services/) | Test chặt hơn — phát hiện bug nếu Wave 4/5 fail | Đây là mục đích |
| Mock setup | Thêm `Mock<IHKDBookGenerationService>` | Update constructor call |

---

## 9. TDD & TESTING STRATEGY
- **Unit tests:** 8 test (3 update + 5 new)
- **Integration tests:** N/A (Wave 7)
- **E2E tests:** N/A
- **Verification:** `dotnet build` + `dotnet test` pass

### Test specs (chi tiết)
**W6-T1 (update): `GenerateS1aBookAsync_ShouldGenerateBook_WhenTenantIsHKDGroup1`**
- Arrange: Mock `IHKDBookGenerationService.GenerateBookAsync` return book với `NumericValues = {TotalRevenue: 1000, TotalExpense: 500, NetProfit: 500}`
- Act: `await _service.GenerateS1aBookAsync(_testTenantId, _testPeriod)`
- Assert (existing): `BookTypeCode == "S1a_HKD"`, `TenantId`, `Period`, `Entries.Count`
- Assert (new): `NumericValues["TotalRevenue"] == 1000m`, `["TotalExpense"] == 500m`, `["NetProfit"] == 500m`

**W6-T8 (new regression): `GenerateS1aBook_NumericValues_ShouldNotBeEmpty_AfterWave4Fix`**
- Arrange: Mock `IHKDBookGenerationService.GenerateBookAsync` return book với `NumericValues` populated
- Act: `await _service.GenerateS1aBookAsync(...)`
- Assert: `result.NumericValues.Count > 0` (regression cho Issue 1 — no-op CalculateAsync)

---

## 10. JIT PLANNING + PURE EXECUTION (MICRO-PHASES)

### Chiến lược thực thi: Update 3 test + add 5 test
1. Update test constructor — thêm `Mock<IHKDBookGenerationService>`
2. Update 3 test existing (S1a, S2a, S2b) — thêm numeric assertions
3. Add 5 test new (S2c, S2d, S2e, S3a, regression)
4. Build + test

### Micro-phase breakdown

| Session | JIT Planning (chốt gì) | Pure Execution (viết gì) |
|---|---|---|
| **S1** | - Đọc `HKDBookService` constructor sau Wave 4 (verify inject)<br>- Đọc 7 template field names (HKDTemplates.cs)<br>- Chốt: mock approach (mock IHKDBookGenerationService return book với NumericValues)<br>- Chốt: test data values per template | - Update test constructor (add Mock<IHKDBookGenerationService>)<br>- Update 3 test existing (S1a, S2a, S2b) — add numeric assert<br>- Add 5 test new (S2c, S2d, S2e, S3a, regression)<br>- Run `dotnet build` + `dotnet test`<br>- Commit |

### Rules
- 1 test tại 1 thời điểm — write, run, verify pass, rồi sang test tiếp
- KHÔNG xóa metadata assertions (BookTypeCode, TenantId, etc.) — chỉ thêm numeric
- Mock setup phải return `GenericHKDBook` với `NumericValues` dictionary populated

---

## 11. ESTIMATED EFFORT
- 1-2 sessions (8 test update/new + mock setup + verify)
- **BLOCKER:** Wave 4 + Wave 5 phải merged trước (NumericValues có giá trị đúng)
- **VALUE:** Verify fix Issue 1 + Issue 4 — test thật sự phát hiện bug
