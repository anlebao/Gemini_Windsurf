# TASK CARD: HKD Book Fix - Wave 7 - API Endpoint + DI Smoke Test

## 1. GOAL & CONTEXT
- **Mục tiêu cốt lõi:** (1) Tạo DTO `HKDBookDto`, (2) Add endpoint `GET /api/hkd-books/{templateCode}` + `GET /api/hkd-books`, (3) Add DI smoke test verify 5 service resolvable, (4) Add integration test endpoint return NumericValues
- **Nghiệp vụ áp dụng:** Expose HKD book generation cho UI (Wave 8)
- **Status:** PENDING — Planning & Approval
- **Branch:** `feature/hkd-fix-wave7-api-endpoint-di-smoke`
- **Estimated Sessions:** 1

---

## 2. ACTIVE WORKFLOW ROUTING
- **Target Workflow:** `newfeaturebuild.md` (IMPLEMENT phase — new endpoint + tests)
- **Execution Mode:** IMPLEMENT
- **Current Phase:** Wave 7 of 9
- **Dependency:** Wave 6 (test pass), Wave 4 (GenerateS*BookAsync có số liệu)

---

## 3. RELEVANT FILES (CONTEXT BOUNDARY)

### Files được phép đọc/sửa
- `docs/AI/project_state.md` (Bắt buộc đọc đầu phiên)
- `docs/AI/tasks/hkd_book_accounting_fix_master_plan.md` (READ)
- `3_CoreHub/Services/Dtos/` (UPDATE — add `HKDBookDtos.cs` hoặc existing Dtos file)
- `2_Gateway/Controllers/AccountingEntriesController.cs` (UPDATE — add 2 endpoints) HOẶC `2_Gateway/Controllers/HKDBooksController.cs` (NEW)
- `3_CoreHub/Services/IHKDBookService.cs` (READ — verify method signatures)
- `3_CoreHub/Services/HKDBookService.cs` (READ — verify implementation)
- `6_Tests/VanAn.Integration.Tests/` (UPDATE — add `HKDBookDISmokeTests.cs` + endpoint integration test)
- `6_Tests/VanAn.Integration.Tests/Infrastructure/` (READ — verify test factory pattern)

### Boundary Rules (Nghiêm cấm)
- KHÔNG sửa `IHKDBookService` interface (backward compat)
- KHÔNG thêm business logic vào controller (governance — controller chỉ forward)
- KHÔNG sửa `1_Shared/Domain/*.cs`
- KHÔNG tạo spec file mới (chỉ .cs test files)
- KHÔNG thay đổi existing endpoint

---

## 4. TECHNICAL & REGULATORY CONSTRAINTS (HARDENING GATES)
- [ ] **Controller Purity:** Controller chỉ forward call tới `IHKDBookService` — không business logic (governance)
- [ ] **Multi-tenancy:** Endpoint phải extract TenantId từ auth context (hiện `ExtractTenantIdFromRequest` return null — verify pattern)
- [ ] **DTO Mapping:** `HKDBookDto` map từ `GenericHKDBook` — không expose Domain entity trực tiếp
- [ ] **DI Smoke:** Test verify 5 service resolvable: `IHKDBookGenerationService`, `IFormulaEngine`, `IDataProvider`, `IPreAggregationService`, `TemplateFactory`
- [ ] **Build Check:** `dotnet build VanAn.sln` Release pass
- [ ] **Test Check:** `dotnet test` pass

---

## 5. SUCCESS CRITERIA (ĐO LƯỜNG ĐƯỢC)
- [ ] **SC1:** `HKDBookDto` created — properties: TenantId, Period, BookTypeCode, NumericValues (Dictionary<string, decimal>), Entries (list)
- [ ] **SC2:** Endpoint `GET /api/hkd-books/{templateCode}?year=&month=` return `HKDBookDto` với NumericValues
- [ ] **SC3:** Endpoint `GET /api/hkd-books` return list available templates theo HKDGroup
- [ ] **SC4:** DI smoke test pass — 5 service resolvable
- [ ] **SC5:** Integration test `GET_hkd_books_S1a_ShouldReturnBookWithNumericValues` pass
- [ ] **SC6:** Controller không có business logic (governance — chỉ forward)
- [ ] **SC7:** `dotnet build VanAn.sln` Release — 0 errors
- [ ] **SC8:** `dotnet test` — all pass
- [ ] **SC9:** guard-check.ps1 PASSED

---

## 6. ACTIVE SKILLS (MAX 3)
- `domain-integrity-validation` — Verify DTO mapping + multi-tenancy
- `test-system-upgrade` — DI smoke + integration test patterns
- `build-error-analysis` — Fix endpoint routing error nếu có

---

## 7. AI HEALTH CHECK MATRIX (INITIAL)
- **Evidence Count:** 5
- **Verified Facts:**
  - Fact 1: `AccountingEntriesController` (L23-32) inject `IAccountingService`, `IReversalService`, `IHKDBookService`, `ILogger` — đã có `IHKDBookService`
  - Fact 2: `ExtractTenantIdFromRequest` (L346) hiện return null — cần verify pattern multi-tenancy
  - Fact 3: `IHKDBookService` có 7 method `GenerateS*BookAsync` + `GenerateAllHKDBooksAsync` (IHKDBookService.cs)
  - Fact 4: `GenericHKDBook` có `TenantId`, `Period`, `BookTypeCode`, `NumericValues`, `Entries`, `Template` (GenericHKDBook.cs)
  - Fact 5: Integration test factory pattern — `6_Tests/VanAn.Integration.Tests/Infrastructure/` có `KhachLinkWebApplicationFactory` (reference)
- **Assumptions:**
  - TenantId extraction từ auth claim (verify pattern hiện có)
  - Integration test dùng `WebApplicationFactory` pattern
- **Open Questions:**
  - Q1: Tạo controller mới `HKDBooksController` hay thêm endpoint vào `AccountingEntriesController`? (Likely new controller — separation of concerns)
  - Q2: `ExtractTenantIdFromRequest` return null — làm sao lấy TenantId? (Verify auth pattern)
- **Recommended Action:** PROCEED — risk thấp, endpoint mới + tests

---

## 8. REVERSE IMPACT ANALYSIS
| File thay đổi | Reverse impact | Mitigation |
|---|---|---|
| `HKDBookDtos.cs` (new) | None — new DTO | N/A |
| `HKDBooksController.cs` (new) hoặc `AccountingEntriesController` (update) | New endpoint — không break cũ | New controller preferred |
| `HKDBookDISmokeTests.cs` (new) | None — new test | N/A |
| Integration test (new) | None — new test | N/A |

---

## 9. TDD & TESTING STRATEGY
- **Unit tests:** N/A (Wave 6 đã cover)
- **Integration tests:** 2 new (DI smoke + endpoint)
- **E2E tests:** N/A (Wave 8)
- **Verification:** `dotnet build` + `dotnet test` pass

### Test specs
**DI Smoke Test: `HKDBookDISmokeTests`**
- Arrange: Build DI container (or use WebApplicationFactory)
- Act: Resolve `IHKDBookGenerationService`, `IFormulaEngine`, `IDataProvider`, `IPreAggregationService`, `TemplateFactory`
- Assert: All 5 service not null

**Integration Test: `GET_hkd_books_S1a_ShouldReturnBookWithNumericValues`**
- Arrange: Seed tenant + JournalEntries (account 511 Credit 1000, 611 Debit 500)
- Act: `GET /api/hkd-books/S1a_HKD?year=2024&month=1`
- Assert: Response 200, body có `NumericValues["TotalRevenue"] == 1000`, `["TotalExpense"] == 500`, `["NetProfit"] == 500`

---

## 10. JIT PLANNING + PURE EXECUTION (MICRO-PHASES)

### Chiến lược thực thi: DTO → Controller → DI smoke → Integration test
1. Create `HKDBookDto` + mapping
2. Create `HKDBooksController` (or add to AccountingEntriesController) — 2 endpoints
3. Add DI smoke test
4. Add integration test
5. Build + test

### Micro-phase breakdown

| Session | JIT Planning (chốt gì) | Pure Execution (viết gì) |
|---|---|---|
| **S1** | - Đọc `AccountingEntriesController` pattern (verify TenantId extraction)<br>- Đọc Integration test factory pattern<br>- Chốt: new controller hay add to existing (likely new)<br>- Chốt: DTO mapping approach (manual map vs AutoMapper) | - Create `HKDBookDto`<br>- Create `HKDBooksController` with 2 endpoints<br>- Add `HKDBookDISmokeTests.cs`<br>- Add integration test<br>- Run `dotnet build` + `dotnet test`<br>- Commit |

### Rules
- 1 component tại 1 thời điểm — DTO, build, controller, build, test, build
- Controller KHÔNG có business logic — chỉ forward + DTO map
- Multi-tenancy: verify TenantId extraction pattern trước khi code

---

## 11. ESTIMATED EFFORT
- 1 session (DTO + 2 endpoints + 2 tests)
- **BLOCKER:** Wave 6 phải merged (test pass baseline)
- **VALUE:** Expose HKD book cho UI Wave 8 + verify DI wiring thật
