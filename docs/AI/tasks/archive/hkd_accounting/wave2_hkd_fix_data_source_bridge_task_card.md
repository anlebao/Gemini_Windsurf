# TASK CARD: HKD Book Fix - Wave 2 - Data Source Bridge (Option A: Refactor Query AccountingEntries)

> **RENAMED + REWRITTEN** from `wave2_hkd_fix_bridge_journal_persistence_task_card.md` per Wave 0.5 decision (Option A chosen, NOT Option C dual-write). Old card superseded.

## 1. GOAL & CONTEXT
- **Mục tiêu cốt lõi:** Refactor `SmartPreAggregationService.GetAccountSumAsync` (L155-185) để query `AccountingEntries` table trực tiếp (thay vì `JournalEntries.Lines` đang rỗng) — enabling calc engine to produce real `NumericValues` for HKD book templates.
- **Nghiệp vụ áp dụng:** Data flow integrity — block Wave 3/4/5/6 (calc engine cần data source thật)
- **Status:** PENDING — Planning & Approval (rewritten per W0.5 Option A decision)
- **Branch:** `feature/hkd-fix-wave2-data-source-bridge-option-a`
- **Estimated Sessions:** 1-2
- **Architecture decision source:** `wave0p5_hkd_fix_arch_decision_data_source_task_card.md` Section 13 (Option A)

---

## 2. ACTIVE WORKFLOW ROUTING
- **Target Workflow:** `newfeaturebuild.md` (IMPLEMENT phase — Service layer refactor)
- **Execution Mode:** IMPLEMENT
- **Current Phase:** Wave 2 of 12 (v3 — Option A per W0.5)
- **Dependency:** Wave 1 (encoding fix — không bắt buộc nhưng recommended), **Stream E (DB Migrations — BLOCKER: AccountingEntries table must have domain columns: AccountCode, EntryType, Amount, PeriodYear, PeriodMonth)**

---

## 3. RELEVANT FILES (CONTEXT BOUNDARY)

### Files được phép đọc/sửa
- `docs/AI/project_state.md` (Bắt buộc đọc đầu phiên)
- `docs/AI/tasks/hkd_book_accounting_fix_master_plan.md` (READ)
- `docs/AI/tasks/wave0p5_hkd_fix_arch_decision_data_source_task_card.md` (READ — Section 13 Option A decision)
- `3_CoreHub/Services/PreAggregation/SmartPreAggregationService.cs` (UPDATE — `GetAccountSumAsync` L155-185)
- `3_CoreHub/Infrastructure/VanAnDbContext.cs` (READ — verify `AccountingEntries` DbSet)
- `3_CoreHub/Infrastructure/Configurations/AccountingEntryConfiguration.cs` (READ — verify column mappings)
- `1_Shared/Domain.cs` (READ — `AccountingEntry` entity L265-345, verify fields: AccountCode, EntryType, Amount, PeriodYear, PeriodMonth, AccountingBookType)
- `6_Tests/VanAn.Core.Tests/Services/HKDBookServiceTests.cs` (UPDATE — add unit tests for refactored query)

### Boundary Rules (Nghiêm cấm)
- KHÔNG sửa `1_Shared/Domain/*.cs` (Domain layer — governance)
- KHÔNG sửa `AccountingEntry` immutability — `AccountingEntry.CreateRevenue/CreateExpense` vẫn immutable
- KHÔNG thay đổi `RecordRevenueAsync`/`RecordExpenseAsync` (Option A = query refactor, NOT write path change)
- KHÔNG thêm JournalEntry writing (Option C đã LOẠI per phản biện)
- KHÔNG thay đổi `IHKDBookRepository.AddToBookAsync` signature
- KHÔNG tạo mới JournalEntry records (Option A queries existing AccountingEntries only)

---

## 4. TECHNICAL & REGULATORY CONSTRAINTS (HARDENING GATES)
- [ ] **Option A scope:** Refactor `GetAccountSumAsync` query từ `_context.JournalEntries...Lines` → `_context.AccountingEntries`. **NO JournalEntry writing.**
- [ ] **EntryType → Credit/Debit mapping:**
  - `EntryType.Revenue` → "Credit" side (revenue increases equity → credit)
  - `EntryType.Expense` → "Debit" side (expense decreases equity → debit)
- [ ] **AccountCode pattern matching:**
  - When `AccountCode` is NOT NULL → filter by `AccountCode.StartsWith(pattern)` (e.g., "5" matches "511", "512", "5118")
  - When `AccountCode` is NULL → use EntryType-based heuristic: Revenue entries match pattern "5" (doanh thu), Expense entries match pattern "6" (chi phí)
- [ ] **Period filtering:** `PeriodYear == period.Year && PeriodMonth == period.Month`
- [ ] **Multi-tenancy:** `TenantId == tenantId.Value` (AccountingEntry excluded from global query filter per VanAnDbContext L228-230 — must filter manually)
- [ ] **Immutability:** `AccountingEntry` stays immutable — only READ, no modification
- [ ] **Build Check:** `dotnet build VanAn.sln` Release pass
- [ ] **Test Check:** `dotnet test` pass (new unit tests + existing)
- [ ] **BLOCKER — Stream E:** `AccountingEntries` table must have domain columns (Amount, EntryType, AccountCode, PeriodYear, PeriodMonth). Dev DB currently STALE (only BaseEntity columns). Stream E (EF Core Migrations) must be executed first.

---

## 5. SUCCESS CRITERIA (ĐO LƯỜNG ĐƯỢC)
- [ ] **SC1:** `GetAccountSumAsync` queries `_context.AccountingEntries` (NOT `_context.JournalEntries`)
- [ ] **SC2:** `EntryType.Revenue` → Credit side sum, `EntryType.Expense` → Debit side sum
- [ ] **SC3:** AccountCode pattern matching works for non-null AccountCode entries (e.g., "511" matches pattern "5")
- [ ] **SC4:** EntryType heuristic works for null AccountCode entries (Revenue → "5" pattern, Expense → "6" pattern)
- [ ] **SC5:** Period + TenantId filtering correct
- [ ] **SC6:** Unit test `GetAccountSumAsync_RevenueEntry_ReturnsCreditSum` pass
- [ ] **SC7:** Unit test `GetAccountSumAsync_ExpenseEntry_ReturnsDebitSum` pass
- [ ] **SC8:** Unit test `GetAccountSumAsync_NullAccountCode_UsesEntryTypeHeuristic` pass
- [ ] **SC9:** `dotnet build VanAn.sln` Release — 0 errors
- [ ] **SC10:** `dotnet test` — all pass
- [ ] **SC11:** guard-check.ps1 PASSED
- [ ] **SC12:** `RecordRevenueAsync`/`RecordExpenseAsync` UNCHANGED (no JournalEntry writing added)

---

## 6. ACTIVE SKILLS (MAX 3)
- `domain-integrity-validation` — Verify AccountingEntry immutability + EntryType mapping
- `dynamic-hkd-book-architecture` — HKD book architecture context
- `pattern-based-fixing` — Apply same pattern for Revenue + Expense queries

---

## 7. AI HEALTH CHECK MATRIX (INITIAL)
- **Evidence Count:** 8 verified facts from Wave 0 + Wave 0.5
- **Verified Facts:**
  - Fact 1: `SmartPreAggregationService.GetAccountSumAsync` (L155-185) currently queries `_context.JournalEntries...Lines.Where(AccountNumber.StartsWith(pattern))` — bảng rỗng
  - Fact 2: `AccountingEntries` table has 0 rows in dev DB, BUT schema is STALE (missing domain columns) — Stream E must fix
  - Fact 3: `AccountingEntry` has fields: AccountCode (nullable string), EntryType (enum: Revenue/Expense), Amount (decimal), PeriodYear (int), PeriodMonth (int), AccountingBookType (enum), TenantId (TenantId)
  - Fact 4: `AccountingEntryConfiguration.cs` maps all domain columns (Amount precision 18,2; EntryType int conversion; AccountCode maxLength 20; PeriodYear/Month int conversion)
  - Fact 5: W0.5-T1 — AccountCode populated for OrderService path (511/621), NULL for `RecordRevenueAsync` path → needs heuristic
  - Fact 6: W0.5-T2 — Formula Engine uses `Account_{pattern}_Credit/Debit` aggregates → mappable from EntryType
  - Fact 7: W0.5-T3 — Product is HKD-only → no need for JournalEntry double-entry structure
  - Fact 8: W0-T11 — OrderService L114-141 already persists JournalEntry via `AddToBookAsync` → Option A avoids compounding this
- **Assumptions:**
  - Refactored query returns same aggregate keys (`Account_{pattern}_Credit`, `Account_{pattern}_Debit`) as before
  - EntryType heuristic for null AccountCode is sufficient (Revenue → "5", Expense → "6")
- **Open Questions:**
  - Q1: Are there AccountingEntry records with EntryType other than Revenue/Expense? (Verify — likely not, but check)
  - Q2: Does `AccountingBookType` enum need filtering? (e.g., only RevenueBook entries for "5" pattern?) — Likely not, EntryType is sufficient
- **Recommended Action:** PROCEED after Stream E completes (DB schema migrated). Risk medium — query refactor + EntryType heuristic.

---

## 8. REVERSE IMPACT ANALYSIS
| File thay đổi | Reverse impact | Mitigation |
|---|---|---|
| `SmartPreAggregationService.GetAccountSumAsync` | Core calc engine data source changes — affects ALL templates (S1a-S3a) | Unit tests verify Revenue + Expense + null AccountCode |
| `RecordRevenueAsync`/`RecordExpenseAsync` (NOT changed) | No impact — Option A does NOT modify write path | N/A |
| `OrderService.GenerateAccountingEntriesAsync` (NOT changed) | Existing JournalEntry writes become IRRELEVANT for HKD book calc (can deprecate in future cleanup) | No action needed in Wave 2 — future technical debt |
| `HKDBookRepository.AddToBookAsync` (NOT changed) | Existing persister still works, but its JournalEntry data is no longer queried by calc engine | No action — JournalEntry table can remain for audit/future use |

---

## 9. TDD & TESTING STRATEGY
- **Unit tests:** 3 new tests (Revenue Credit sum, Expense Debit sum, null AccountCode heuristic)
- **Integration tests:** N/A (Wave 7)
- **E2E tests:** N/A
- **Verification:** `dotnet build` + `dotnet test` pass

### Test specs
**Test 1: `GetAccountSumAsync_RevenueEntry_ReturnsCreditSum`**
- Arrange: Seed `AccountingEntries` with Revenue entry (Amount=1000, AccountCode="511", PeriodYear=2024, PeriodMonth=1)
- Act: `GetAccountSumAsync(tenantId, period, "5", "Credit")`
- Assert: returns 1000

**Test 2: `GetAccountSumAsync_ExpenseEntry_ReturnsDebitSum`**
- Arrange: Seed `AccountingEntries` with Expense entry (Amount=500, AccountCode="611", PeriodYear=2024, PeriodMonth=1)
- Act: `GetAccountSumAsync(tenantId, period, "6", "Debit")`
- Assert: returns 500

**Test 3: `GetAccountSumAsync_NullAccountCode_UsesEntryTypeHeuristic`**
- Arrange: Seed `AccountingEntries` with Revenue entry (Amount=2000, AccountCode=null, PeriodYear=2024, PeriodMonth=1)
- Act: `GetAccountSumAsync(tenantId, period, "5", "Credit")`
- Assert: returns 2000 (heuristic: Revenue → "5" pattern)

---

## 10. DOUBLE-WRITE AUDIT (from Wave 0 T11 — propagated 2026-07-03)

- **Callers of RecordRevenueAsync:**
  1. `SimpleAccountingEventHandler.cs` L100 — NATS event handler
  2. `HKDBookServiceTests.cs` — test callers
- **Callers of RecordExpenseAsync:**
  1. `HKDBookServiceTests.cs` — test callers only (no production caller)
- **JournalEntry persisters:**
  1. `HKDBookRepository.AddToBookAsync` L135-154, L163, L220
  2. `OrderService.GenerateAccountingEntriesAsync` L114-115, L140-141 (calls `AddToBookAsync`)
  3. `Services/TemplateFactory.cs` (OLD) L21, L31 — `JournalEntry.Create` but NOT persisted (dead code)
- **Write-path map:**
  - Path 1 (OrderService): AccountingEntry + JournalEntry BOTH persisted
  - Path 2 (NATS handler): AccountingEntry only
  - Path 3 (Direct API): AccountingEntry only
- **DECISION TREE PATH: B** — existing persister from OrderService
- **Wave 2 action (Option A):** NO JournalEntry writing added. Existing OrderService JournalEntry writes become IRRELEVANT for HKD book calc. No double-write risk because Option A only queries AccountingEntries.

---

## 11. JIT PLANNING + PURE EXECUTION (MICRO-PHASES)

### Chiến lược thực thi: Refactor query + add tests
1. Verify Stream E completed (AccountingEntries has domain columns) — HARD BLOCKER
2. Read `AccountingEntry` entity (Domain.cs L265-345) — confirm fields
3. Refactor `GetAccountSumAsync` (L155-185) — replace `JournalEntries` query with `AccountingEntries` query
4. Implement EntryType → Credit/Debit mapping
5. Implement AccountCode null heuristic
6. Add 3 unit tests
7. Build + test

### Micro-phase breakdown

| Session | JIT Planning (chốt gì) | Pure Execution (viết gì) |
|---|---|---|
| **S1** | - Confirm Stream E done (DB schema migrated)<br>- Read `AccountingEntry` fields<br>- Chốt: EntryType mapping (Revenue=Credit, Expense=Debit)<br>- Chốt: null AccountCode heuristic (Revenue→"5", Expense→"6") | - Refactor `GetAccountSumAsync` query<br>- Add 3 unit tests<br>- Run `dotnet build` + `dotnet test`<br>- Commit |

### Rules
- 1 query refactor tại 1 thời điểm — verify build trước khi add tests
- KHÔNG break `AccountingEntry` immutability
- KHÔNG add JournalEntry writing (Option C LOẠI)
- If Stream E NOT complete → STOP, cannot proceed (DB columns missing)

---

## 12. ESTIMATED EFFORT
- 1-2 sessions (query refactor + 3 unit tests + verify)
- **BLOCKER:** Stream E (DB Migrations) — AccountingEntries table must have domain columns. Dev DB currently STALE.
- **CRITICAL:** Wave này block tất cả wave sau (calc engine cần data source thật)
- **NOT a write-path change:** Option A chỉ refactor READ query, không thay đổi write path → risk thấp hơn Option C
