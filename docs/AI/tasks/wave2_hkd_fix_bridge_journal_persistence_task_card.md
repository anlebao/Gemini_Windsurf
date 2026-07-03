# TASK CARD: HKD Book Fix - Wave 2 - Bridge AccountingEntry → JournalEntry Persistence

## 1. GOAL & CONTEXT
- **Mục tiêu cốt lõi:** Sau khi persist `AccountingEntry` (immutable), tạo + persist `JournalEntry` (double-entry) vào bảng `JournalEntries` — để calc engine (`SmartPreAggregationService`) có data query
- **Nghiệp vụ áp dụng:** Data flow integrity — block Wave 3/4/5/6 (calc engine cần data)
- **Status:** PENDING — Planning & Approval
- **Branch:** `feature/hkd-fix-wave2-bridge-journal-persistence`
- **Estimated Sessions:** 1-2

---

## 2. ACTIVE WORKFLOW ROUTING
- **Target Workflow:** `newfeaturebuild.md` (IMPLEMENT phase — production write path change)
- **Execution Mode:** IMPLEMENT
- **Current Phase:** Wave 2 of 9
- **Dependency:** Wave 1 (encoding fix — không bắt buộc nhưng recommended)

---

## 3. RELEVANT FILES (CONTEXT BOUNDARY)

### Files được phép đọc/sửa
- `docs/AI/project_state.md` (Bắt buộc đọc đầu phiên)
- `docs/AI/tasks/hkd_book_accounting_fix_master_plan.md` (READ)
- `3_CoreHub/Services/HKDBookService.cs` (UPDATE — `RecordRevenueAsync` L43-71, `RecordExpenseAsync` L73-101, `ConvertToJournalEntries` L718-751)
- `3_CoreHub/Repositories/HKDBookRepository.cs` (READ — verify `AddToBookAsync` L135-154)
- `1_Shared/Domain/JournalEntry.cs` (READ — verify `AddLine` signature, immutability)
- `1_Shared/Domain/HKDTemplates.cs` (READ — verify `AccountingBookType` enum values)
- `6_Tests/VanAn.Core.Tests/Accounting/HKDBookServiceTests.cs` (UPDATE — add 2 unit tests)

### Boundary Rules (Nghiêm cấm)
- KHÔNG sửa `1_Shared/Domain/*.cs` (Domain layer — governance)
- KHÔNG sửa `AccountingEntry` immutability — `AccountingEntry.CreateRevenue/CreateExpense` vẫn immutable
- KHÔNG thay đổi `RecordRevenueAsync`/`RecordExpenseAsync` public signature
- KHÔNG xóa `ConvertToJournalEntries` (sẽ mark obsolete ở Wave 4)
- KHÔNG thay đổi `IHKDBookRepository.AddToBookAsync` signature

---

## 4. TECHNICAL & REGULATORY CONSTRAINTS (HARDENING GATES)
- [ ] **Double-Entry Integrity:** Mỗi `JournalEntry` phải có 2 lines: 1 Debit + 1 Credit, tổng Debit = tổng Credit = amount
- [ ] **Account Numbers:** Revenue → Dr 111 (Tiền mặt), Cr 511 (Doanh thu bán hàng); Expense → Dr 611 (Chi phí), Cr 111 (Tiền mặt)
- [ ] **Multi-tenancy:** `JournalEntry.TenantId` phải = `AccountingEntry.TenantId`
- [ ] **Period Consistency:** `JournalEntry.Period` phải = `AccountingEntry.Period`
- [ ] **Immutability:** `AccountingEntry` vẫn immutable — chỉ thêm `JournalEntry` record mới, không sửa `AccountingEntry`
- [ ] **Build Check:** `dotnet build VanAn.sln` Release pass
- [ ] **Test Check:** `dotnet test` pass (2 unit test mới + cũ)

---

## 5. SUCCESS CRITERIA (ĐO LƯỜNG ĐƯỢC)
- [ ] **SC1:** `RecordRevenueAsync` persist cả `AccountingEntry` (immutable) + `JournalEntry` (double-entry Dr 111/Cr 511)
- [ ] **SC2:** `RecordExpenseAsync` persist cả `AccountingEntry` + `JournalEntry` (double-entry Dr 611/Cr 111)
- [ ] **SC3:** `JournalEntry` có 2 lines, tổng Debit = tổng Credit = amount
- [ ] **SC4:** `JournalEntry.TenantId` = `AccountingEntry.TenantId`
- [ ] **SC5:** `JournalEntry.Period` = `AccountingEntry.Period`
- [ ] **SC6:** Unit test `RecordRevenueAsync_ShouldPersistJournalEntry_WithCorrectDoubleEntryLines` pass
- [ ] **SC7:** Unit test `RecordExpenseAsync_ShouldPersistJournalEntry_WithCorrectDoubleEntryLines` pass
- [ ] **SC8:** `dotnet build VanAn.sln` Release — 0 errors
- [ ] **SC9:** `dotnet test` — all pass
- [ ] **SC10:** guard-check.ps1 PASSED

---

## 6. ACTIVE SKILLS (MAX 3)
- `domain-integrity-validation` — Verify double-entry + immutability
- `outbox-pattern-implementation` — Pattern reference (atomic write 2 records)
- `pattern-based-fixing` — Apply cùng pattern cho Revenue + Expense

---

## 7. AI HEALTH CHECK MATRIX (INITIAL)
- **Evidence Count:** 6
- **Verified Facts:**
  - Fact 1: `RecordRevenueAsync` (L43-71) hiện chỉ gọi `_repository.AddAsync(entry)` — persist `AccountingEntry` vào bảng `AccountingEntries`
  - Fact 2: `RecordExpenseAsync` (L73-101) cùng pattern — chỉ persist `AccountingEntry`
  - Fact 3: `SmartPreAggregationService.GetAccountSumAsync` (L155-185) query `_context.JournalEntries...Lines` — bảng rỗng
  - Fact 4: `IHKDBookRepository.AddToBookAsync` (L135-154) tồn tại, persist `JournalEntry` vào `JournalEntries` table — nhưng không ai gọi
  - Fact 5: `JournalEntry.AddLine(accountNumber, debit, credit, description)` — signature confirmed (JournalEntry.cs L140-167)
  - Fact 6: `AccountingEntry.CreateRevenue/CreateExpense` immutable — không sửa
- **Assumptions:**
  - `JournalEntry` constructor accept (tenantId, entryDate, description, referenceType, referenceId) — confirmed HKDBookService L725-731
  - `IHKDBookRepository` đã inject vào `HKDBookService` (L19 `_hkdBookRepository`)
- **Open Questions:**
  - Q1: `AccountingBookType` cho Revenue JournalEntry là gì? (Verify enum — likely `RevenueBook` hoặc `S2a_HKD`)
  - Q2: Có cần transaction wrap 2 writes (AccountingEntry + JournalEntry)? (Likely yes — atomic)
- **Recommended Action:** PROCEED — risk medium, cần cẩn thận double-entry + transaction

---

## 8. REVERSE IMPACT ANALYSIS
| File thay đổi | Reverse impact | Mitigation |
|---|---|---|
| `HKDBookService.RecordRevenueAsync` | Thêm 1 write path (JournalEntry) — tăng write load | Atomic transaction; nếu JournalEntry fail, rollback AccountingEntry |
| `HKDBookService.RecordExpenseAsync` | Same | Same |
| `SimpleAccountingEventHandler` (caller) | Tự động hưởng lợi — JournalEntry có data | No change needed (caller không sửa) |
| `HKDBookRepository.AddToBookAsync` (được gọi) | Bây giờ có caller — verify persist logic đúng | Verify L135-154 |

---

## 9. TDD & TESTING STRATEGY
- **Unit tests:** 2 test mới (double-entry lines verify)
- **Integration tests:** N/A (Wave 7)
- **E2E tests:** N/A
- **Verification:** `dotnet build` + `dotnet test` pass

### Test specs
**Test 1: `RecordRevenueAsync_ShouldPersistJournalEntry_WithCorrectDoubleEntryLines`**
- Arrange: TenantId, amount=1000, description, date
- Mock: `_repository.AddAsync` (AccountingEntry) + `_hkdBookRepository.AddToBookAsync` (JournalEntry)
- Act: `await _service.RecordRevenueAsync(tenantId, 1000, "Test", date)`
- Assert:
  - `_hkdBookRepository.AddToBookAsync` called once with JournalEntry
  - JournalEntry.Lines.Count == 2
  - Line 1: AccountNumber="111", DebitAmount=1000, CreditAmount=0
  - Line 2: AccountNumber="511", DebitAmount=0, CreditAmount=1000
  - JournalEntry.TenantId == tenantId
  - JournalEntry.Period == AccountingPeriod.FromDateTime(date)

**Test 2: `RecordExpenseAsync_ShouldPersistJournalEntry_WithCorrectDoubleEntryLines`**
- Same pattern, account 611 (Debit) + 111 (Credit)

---

## 10. JIT PLANNING + PURE EXECUTION (MICRO-PHASES)

### Chiến lược thực thi: Modify write path + add tests
1. Verify `JournalEntry` constructor + `AddLine` signature (READ JournalEntry.cs)
2. Verify `AccountingBookType` enum (READ HKDTemplates.cs hoặc Domain.cs)
3. Modify `RecordRevenueAsync` — sau `_repository.AddAsync(entry)`, tạo JournalEntry + gọi `_hkdBookRepository.AddToBookAsync`
4. Modify `RecordExpenseAsync` — cùng pattern
5. Add 2 unit tests
6. Build + test

### Micro-phase breakdown

| Session | JIT Planning (chốt gì) | Pure Execution (viết gì) |
|---|---|---|
| **S1** | - Đọc JournalEntry.cs (constructor + AddLine)<br>- Đọc AccountingBookType enum<br>- Chốt: account numbers (111/511/611)<br>- Chốt: có cần transaction không (likely yes) | - Modify `RecordRevenueAsync` (add JournalEntry creation + AddToBookAsync call)<br>- Modify `RecordExpenseAsync` (same)<br>- Add 2 unit tests<br>- Run `dotnet build` + `dotnet test`<br>- Commit |

### Rules
- 1 method tại 1 thời điểm — fix Revenue, verify build, rồi fix Expense
- KHÔNG break `AccountingEntry` immutability
- Nếu `AddToBookAsync` throw → rollback `AccountingEntry` (transaction hoặc try/catch + delete)
- Verify mock setup đúng trong test (Mock<IHKDBookRepository>)

---

## 11. ESTIMATED EFFORT
- 1-2 sessions (2 method modify + 2 unit tests + verify double-entry)
- **BLOCKER:** None — nhưng cần cẩn thận transaction + double-entry integrity
- **CRITICAL:** Wave này block tất cả wave sau (calc engine cần data)
