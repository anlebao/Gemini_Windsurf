# TASK CARD: HKD Book Fix - Wave 0.5 - Architecture Decision: HKD Data Source (A vs B, loại C dual-write)

## 1. GOAL & CONTEXT
- **Mục tiêu cốt lõi:** Chốt architecture decision cho HKD data source TRƯỚC khi Wave 2 bắt đầu. Option C (hardcode dual write trong `RecordRevenueAsync`) bị LOẠI per phản biện kiến trúc. Chọn Option A (refactor query) hoặc Option B (event-driven Outbox).
- **Nghiệp vụ áp dụng:** Architecture decision cho stream HKD Book Accounting Report Fix — resolves Dual Write anti-pattern cảnh báo bởi phản biện kiến trúc (session 2026-07-03).
- **Status:** PENDING — Planning & Approval (v3 — Amendment 4)
- **Branch:** `feature/hkd-fix-wave0p5-arch-decision-hkd-data-source`
- **Estimated Sessions:** 0.5-1 (ANALYZE — read + decision, no code change)
- **Master plan link:** `docs/AI/tasks/hkd_book_accounting_fix_master_plan.md` Section 0.7 (Wave 0.5)

---

## 2. ACTIVE WORKFLOW ROUTING
- **Target Workflow:** `newfeaturebuild.md` (ANALYZE phase — decision only, no code change)
- **Execution Mode:** ANALYZE
- **Current Phase:** Wave 0.5 of 12 (v3 — between Wave 0 pre-flight and Wave 1 encoding)
- **Dependency:** Wave 0 complete (baseline verified, W0-T11 double-write audit done)
- **Blocks:** Wave 2 (data source bridge scope depends on this decision)

---

## 3. RELEVANT FILES (CONTEXT BOUNDARY)

### Files được phép đọc
- `docs/AI/project_state.md` (Bắt buộc đọc đầu phiên)
- `docs/AI/tasks/hkd_book_accounting_fix_master_plan.md` (READ — Section 0.7 Wave 0.5)
- `docs/AI/tasks/wave0_hkd_fix_preflight_task_card.md` (READ — W0-T11 double-write audit result)
- `1_Shared/Domain.cs` (READ — `AccountingEntry` entity, L265-287, verify `AccountCode` field + factory methods `CreateRevenue`/`CreateExpense`)
- `3_CoreHub/Services/HKDBookService.cs` (READ — L43-101 `RecordRevenueAsync`/`RecordExpenseAsync`, L22-41 `_vietnameseAccounts`)
- `3_CoreHub/Services/PreAggregation/SmartPreAggregationService.cs` (READ — L155-185 `GetAccountSumAsync`, query `JournalEntries.Lines`)
- `3_CoreHub/Services/Formula/ProductionFormulaEngine.cs` (READ — `GetDependencies`, verify engine needs `Account_{pattern}_Credit/Debit` aggregates)
- `3_CoreHub/Services/Template/TemplateFactory.cs` (READ — template formulas, verify dependency on `Account_*` aggregates)
- `3_CoreHub/Services/Events/SimpleAccountingEventHandler.cs` (READ — existing NATS BackgroundService pattern)
- `3_CoreHub/Infrastructure/Messaging/OutboxRepository.cs` (READ — existing Outbox infrastructure)
- `3_CoreHub/Infrastructure/Messaging/NatsEventPublisher.cs` (READ — existing event publisher)
- `3_CoreHub/Services/NatsSyncWorker.cs` (READ — existing NATS worker pattern)
- `.devin/skills/outbox-pattern-implementation.md` (READ — Outbox guideline)
- `docs/AI/project_state.md` Section 1 (READ — Project Overview, confirm HKD-only vs share engine with Doanh nghiệp)

### Boundary Rules (Nghiêm cấm)
- KHÔNG sửa code — Wave 0.5 là decision only
- KHÔNG tạo file production code — chỉ ghi decision vào task card + update Wave 2 task card
- KHÔNG chọn Option C (dual write) — đã LOẠI per phản biện

---

## 4. TECHNICAL & REGULATORY CONSTRAINTS (HARDENING GATES)
- [ ] **Option C LOẠI:** KHÔNG được chọn Option C (hardcode 2 lệnh ghi `AccountingEntry` + `JournalEntry` trong cùng `RecordRevenueAsync`/`RecordExpenseAsync`). Đây là Dual Write anti-pattern — rủi ro mất đồng bộ dữ liệu, bẩn Service layer, vi phạm Single Responsibility.
- [ ] **Option A prerequisite:** `AccountingEntry.AccountCode` phải được populate cho mọi entry (verify W0.5-T1). Nếu caller nào không truyền `AccountCode` → Option A cần thêm field population step.
- [ ] **Option B prerequisite:** Outbox infrastructure phải confirmed hoạt động (OutboxRepository + NatsEventPublisher + NatsSyncWorker). Đã verify tồn tại — cần confirm hoạt động được.
- [ ] **Formula Engine compatibility:** Nếu chọn Option A — verify engine có thể map `EntryType.Revenue` → "Credit", `EntryType.Expense` → "Debit" (thay vì query `JournalEntries.Lines` Debit/Credit). Nếu engine cần cứng Debit/Credit structure → Option A không khả thi, phải Option B.
- [ ] **HKD Accounting Regime:** Cả 2 option đều phải tuân thủ — HKD = single-entry per TT 88/2021 + TT 152/2025. Account mapping (111/511/611) là Internal Synthetic Mapping, KHÔNG phải nghĩa vụ hạch toán kép.
- [ ] **AccountingEntry immutability:** Cả 2 option KHÔNG được modify `AccountingEntry` (governance Hard Stop).

---

## 5. SUCCESS CRITERIA (ĐO LƯỜNG ĐƯỢC)
- [ ] **SC1:** W0.5-T1 complete — `AccountingEntry.AccountCode` population status documented (populated for all entries OR needs fix)
- [ ] **SC2:** W0.5-T2 complete — Formula Engine dependency documented (needs `Account_*_Credit/Debit` aggregates OR can map from `EntryType`)
- [ ] **SC3:** W0.5-T3 complete — Product roadmap status documented (HKD-only OR share engine with Doanh nghiệp)
- [ ] **SC4:** W0.5-T4 complete — **DECISION documented** (Option A or B, with rationale) — NOT Option C
- [ ] **SC5:** W0.5-T5 (if Option A) OR W0.5-T6 (if Option B) complete — Wave 2 task card rewritten per chosen option
- [ ] **SC6:** `dotnet build VanAn.sln` Release — 0 errors (no code change, just decision)
- [ ] **SC7:** guard-check.ps1 PASSED

---

## 6. ACTIVE SKILLS (MAX 3)
- `outbox-pattern-implementation` — Option B reference (event-driven Outbox)
- `domain-integrity-validation` — Verify AccountingEntry immutability + AccountCode field
- `dynamic-hkd-book-architecture` — HKD book architecture context

---

## 7. AI HEALTH CHECK MATRIX (INITIAL)
- **Evidence Count:** 5 verified facts from codebase
- **Verified Facts:**
  - Fact 1: `AccountingEntry` có field `AccountCode` (DMD-1 fix 2026-06-20, `1_Shared/Domain.cs` L284) + `EntryType` + `Amount` + `PeriodYear/Month` + `TenantId` → đủ thông tin cho Option A
  - Fact 2: `SmartPreAggregationService.GetAccountSumAsync` (L155-185) query `_context.JournalEntries...Lines` → refactor-able thành `_context.AccountingEntries` cho Option A
  - Fact 3: `OutboxRepository` + `IOutboxRepository` + `NatsSyncWorker` + `NatsEventPublisher` + `OutboxNotificationService` đã tồn tại → Option B không cần xây mới infrastructure
  - Fact 4: `SimpleAccountingEventHandler` đã là NATS BackgroundService subscribe `vanan.events.ordercompleted` → Option B thêm 1 subscription `vanan.events.accountingentryrecorded`
  - Fact 5: `RecordRevenueAsync` (L43-71) hiện tại CLEAN — single write `_repository.AddAsync(entry)` → Option C sẽ DIRTY method này
- **Assumptions (to verify in T1-T3):**
  - `AccountingEntry.AccountCode` được populate cho mọi entry (T1 will confirm)
  - Formula Engine có thể map `EntryType` → Credit/Debit sign (T2 will confirm)
  - Product roadmap là HKD-only (T3 will confirm — determines A vs B)
- **Open Questions:**
  - Q1: Caller nào của `AccountingEntry.CreateRevenue`/`CreateExpense` không truyền `AccountCode`? (T1)
  - Q2: Formula Engine cần cứng `JournalEntries.Lines` Debit/Credit hay chỉ cần `Account_{pattern}_Credit/Debit` aggregates? (T2)
  - Q3: Có kế hoạch mở rộng Formula Engine cho khối Doanh nghiệp không? (T3)
- **Recommended Action:** PROCEED — read-only decision, risk none. **Option C đã LOẠI, chỉ chọn A or B.**

---

## 8. REVERSE IMPACT ANALYSIS
| File verify | Reverse impact | Mitigation |
|---|---|---|
| `1_Shared/Domain.cs` (AccountingEntry) | None — read only | N/A |
| `HKDBookService.cs` | None — read only | N/A |
| `SmartPreAggregationService.cs` | None — read only | N/A |
| `ProductionFormulaEngine.cs` | None — read only | N/A |
| `SimpleAccountingEventHandler.cs` | None — read only | N/A |
| `OutboxRepository.cs` | None — read only | N/A |

### Decision output (T4 → Wave 2 task card)
| Task | Output written to | Affects wave |
|---|---|---|
| T4 | Wave 2 task card (REWRITE — replace "Bridge JournalEntry persistence" với Option A or B scope) | Wave 2 |
| T4 | `docs/AI/project_state.md` Section 4 (update Wave 2 description) | Wave 2 |

---

## 9. TDD & TESTING STRATEGY
- **Unit tests:** N/A — Wave 0.5 là decision only
- **Integration tests:** N/A
- **E2E tests:** N/A
- **Verification:** `dotnet build VanAn.sln` Release pass (no code change) + decision documented

---

## 10. JIT PLANNING + PURE EXECUTION (MICRO-PHASES)

### Chiến lược thực thi: Sequential verify → decision → propagate
1. Verify AccountCode population (T1) → 2. Verify Formula Engine dependency (T2) → 3. Verify product roadmap (T3) → 4. **DECISION** (T4) → 5. Rewrite Wave 2 task card (T5 or T6)

### Micro-phase breakdown

| Session | JIT Planning (chốt gì) | Pure Execution (viết gì) |
|---|---|---|
| **S1** (T1-T3 — verify) | - T1: Chốt AccountCode population status<br>- T2: Chốt Formula Engine dependency (cứng Debit/Credit OR map from EntryType)<br>- T3: Chốt product roadmap (HKD-only OR share engine) | - Grep callers of `AccountingEntry.CreateRevenue`/`CreateExpense`<br>- Read `ProductionFormulaEngine.GetDependencies` + template formulas<br>- Read `project_state.md` Section 1 + roadmap docs |
| **S1/S2** (T4 — decision) | - **DECISION:** Option A (refactor query) OR Option B (event-driven Outbox)<br>- Rationale: 1-2 câu | - Write decision to this task card Section 11<br>- Write decision to Wave 2 task card (rewrite scope) |
| **S2** (T5 or T6 — propagate) | - If Option A: rewrite Wave 2 task card thành "refactor SmartPreAggregationService query AccountingEntries"<br>- If Option B: rewrite Wave 2 task card thành "add Domain Event + NATS/Outbox handler sinh JournalEntry" | - Rewrite `wave2_hkd_fix_data_source_bridge_task_card.md`<br>- Update `project_state.md` Section 4 Wave 2 description |

### Rules
- 1 verify step tại 1 thời điểm
- **Option C KHÔNG được chọn** — đã LOẠI per phản biện
- **Nếu T1 phát hiện AccountCode không populate + caller không fix được → Option A khó khả thi, nghiêng về Option B**
- **Nếu T2 phát hiện Formula Engine cần cứng Debit/Credit → Option A không khả thi, phải Option B**
- **Nếu T3 xác định HKD-only product → nghiêng về Option A (Single Source of Truth, không cần JournalEntry)**
- **Nếu T3 xác định share engine với Doanh nghiệp → nghiêng về Option B (event-driven, share Formula Engine)**

---

## 11. DECISION TEMPLATE (output format)

```
## HKD Data Source Architecture Decision (from Wave 0.5 T4)
- W0.5-T1 Result: AccountingEntry.AccountCode populated for [ALL/SOME/NONE] entries
  - Callers not passing AccountCode: [list OR "none"]
- W0.5-T2 Result: Formula Engine [CAN/CANNOT] map EntryType → Credit/Debit sign
  - Engine dependency: [Account_{pattern}_Credit/Debit aggregates (mappable) / JournalEntries.Lines Debit/Credit structure (hardcoded)]
- W0.5-T3 Result: Product roadmap is [HKD-ONLY / SHARE ENGINE WITH DOANH NGHIỆP]
- DECISION: [Option A — refactor SmartPreAggregationService query AccountingEntries / Option B — event-driven Outbox]
- Rationale: [1-2 sentences explaining why A or B based on T1-T3 findings]
- Wave 2 scope: [Option A: refactor GetAccountSumAsync query / Option B: add AccountingEntryRecorded event + handler]
```

---

## 12. ESTIMATED EFFORT
- 0.5-1 session (read + decision, no code change)
- **BLOCKER:** Wave 2 — không thể bắt đầu Wave 2 без decision này
- **PARALLEL:** Có thể làm cùng session với Wave 0 + Wave 1 (cả 3 non-code/low-risk, độc lập)
- **CRITICAL OUTPUT:** Decision propagates to Wave 2 task card — MUST complete before Wave 2 starts

---

## 13. DECISION OUTPUT (executed 2026-07-03, branch `feature/hkd-fix-wave0-wave0p5-preflight`)

### W0.5-T1 Result: AccountingEntry.AccountCode populated for SOME entries
- Factory signature: `CreateRevenue(tenantId, period, amount, description, string? accountCode = null, string? reference = null)` — `accountCode` is OPTIONAL, defaults to `null`
- Production callers NOT passing AccountCode:
  - `HKDBookService.RecordRevenueAsync` (L56) — `CreateRevenue(tenantId, period, new Money(amount), description)` → **AccountCode = null**
  - `HKDBookService.RecordExpenseAsync` (L86) — same pattern → **AccountCode = null**
  - `AccountingEntryService.CreateAsync` (L83-84) — `CreateRevenue/CreateExpense(tenantId, period, amount, description)` → **AccountCode = null**
- Production callers PASSING AccountCode:
  - `AccountingEntryService.CreateRevenueEntryAsync` (L147) — `CreateRevenue(..., accountCode: accountCode, reference: reference)` → populated
  - `AccountingEntryService.CreateExpenseEntryAsync` (L196) — `CreateExpense(..., accountCode: accountCode, ...)` → populated
  - `OrderService.GenerateAccountingEntriesAsync` (L103-108, L128-133) — calls `CreateRevenueEntryAsync(accountCode: "511")` / `CreateExpenseEntryAsync(accountCode: "621")` → **populated**
- **Conclusion:** AccountCode is null for entries created via `HKDBookService.RecordRevenue/ExpenseAsync` (the NATS event handler path) and via `AccountingEntryService.CreateAsync`. AccountCode is populated for entries created via `AccountingEntryService.CreateRevenueEntryAsync/CreateExpenseEntryAsync` (the OrderService path). Option A needs an EntryType-based fallback heuristic for null AccountCode entries.

### W0.5-T2 Result: Formula Engine CAN map EntryType → Credit/Debit sign
- Template formulas use `SUM_ACCOUNT("5", "Credit")` / `SUM_ACCOUNT("6", "Debit")` → engine produces `Account_{pattern}_Credit` / `Account_{pattern}_Debit` aggregate keys
- `SmartPreAggregationService.GetAccountSumAsync` (L155-185) currently queries `_context.JournalEntries...Lines.Where(AccountNumber.StartsWith(pattern))` and sums `CreditAmount` / `DebitAmount`
- **For Option A:** Refactor `GetAccountSumAsync` to query `_context.AccountingEntries` directly:
  - Map `EntryType.Revenue` → "Credit" side (revenue increases equity → credit)
  - Map `EntryType.Expense` → "Debit" side (expense decreases equity → debit)
  - Filter by `AccountCode.StartsWith(pattern)` when AccountCode is not null
  - When AccountCode is null: use EntryType-based heuristic — Revenue entries match pattern "5" (doanh thu), Expense entries match pattern "6" (chi phí)
- **Engine dependency:** `Account_{pattern}_Credit/Debit` aggregates are mappable from EntryType + AccountCode. NOT hardcoded to `JournalEntries.Lines` Debit/Credit structure. **Option A is FEASIBLE.**

### W0.5-T3 Result: Product roadmap is HKD-ONLY
- `project_state.md` Section 1: "Vạn An Accounting System MVP — giải pháp kế toán HKD theo TT 152/2025/TT-BTC"
- No mention of Doanh nghiệp expansion or sharing Formula Engine with corporate accounting
- **Conclusion:** HKD-only product → no need for double-entry JournalEntry structure (which is a Doanh nghiệp requirement per TT 200/133). HKD uses single-entry per TT 88/2021 + TT 152/2025.

### W0.5-T4 DECISION: **Option A — refactor SmartPreAggregationService to query AccountingEntries directly**

**Rationale:**
1. **HKD-only product (T3)** → no need to share Formula Engine with Doanh nghiệp → no need for JournalEntry double-entry structure. HKD = single-entry per TT 88/2021 + TT 152/2025.
2. **AccountingEntry is the immutable Single Source of Truth** (governance Hard Stop) → querying it directly is cleaner than maintaining a separate JournalEntry projection that must be kept in sync.
3. **T11 finding (Wave 0):** `OrderService.GenerateAccountingEntriesAsync` ALREADY persists JournalEntry via `AddToBookAsync` (L114-141). Adding MORE JournalEntry writes via Option B would compound the existing write paths and risk double-write for the same business event (order completed → OrderService writes JournalEntry + NATS handler writes another via Option B). **Option A avoids this entirely** — no JournalEntry writing, just query AccountingEntries.
4. **EntryType → Credit/Debit mapping is straightforward (T2)** — Revenue = Credit, Expense = Debit. Formula Engine's `Account_{pattern}_Credit/Debit` aggregates are mappable.
5. **AccountCode populated for OrderService path (T1)** — entries from `OrderService` have AccountCode="511"/"621". For null AccountCode entries (from `RecordRevenueAsync`), EntryType-based heuristic works (Revenue → "5" pattern, Expense → "6" pattern).
6. **Existing JournalEntries from OrderService** become irrelevant for HKD book calculation (can be deprecated or ignored). No migration needed — Option A queries AccountingEntries which already has all data.

**Wave 2 scope (Option A):**
- Refactor `SmartPreAggregationService.GetAccountSumAsync` (L155-185) to query `_context.AccountingEntries` instead of `_context.JournalEntries...Lines`
- Map `EntryType.Revenue` → Credit side, `EntryType.Expense` → Debit side
- Filter by `AccountCode.StartsWith(pattern)` when AccountCode is not null; when null, use EntryType-based heuristic (Revenue → "5", Expense → "6")
- **NO JournalEntry writing** — no modification to `RecordRevenueAsync` / `RecordExpenseAsync`
- **NO Domain modification** — `AccountingEntry` stays immutable, no new fields
- Optionally (Wave 2 stretch): fix `RecordRevenueAsync` to pass AccountCode (minor Service-layer change, not Domain modification) — improves Option A query accuracy

**Option B NOT chosen because:**
- Adds event-driven JournalEntry writing → compounds existing OrderService JournalEntry writes (T11 double-write risk)
- More complex (event + handler + idempotent guard) for a HKD-only product that doesn't need double-entry structure
- AccountingEntry already has all needed data (AccountCode + EntryType + Amount + Period) — no need for a JournalEntry projection

**Option C NOT chosen (LOẠI per phản biện):**
- Dual write anti-pattern — rủi ro mất đồng bộ, bẩn Service layer, vi phạm Single Responsibility

---

## 14. POST-DECISION CAVEAT — Dev DB Schema STALE (discovered during Wave 0 T5 execution)

**CRITICAL:** The W0.5-T4 decision (Option A) is architecturally sound, BUT execution revealed a **dev environment blocker**:

- Dev DB (`5_WebApps/ShopERP/vanan_shoperp.db`) was created via `EnsureCreatedAsync()` BEFORE `AccountingEntryConfiguration.cs` was added with domain column mappings
- `AccountingEntries` table in dev DB only has BaseEntity columns (Id, AccountingEntryId, TenantId, CreatedAt, UpdatedAt, CreatedBy, UpdatedBy, IsDeleted)
- **MISSING columns needed by Option A:** `AccountCode`, `EntryType`, `Amount`, `PeriodYear`, `PeriodMonth`, `AccountingBookType`, `VatRate`, `Description`
- `EnsureCreatedAsync()` does NOT update existing DB schema — it only creates if DB doesn't exist
- No Migrations folder (architecture decision: `EnsureCreated` strategy, Migrations forbidden per `VA-ARCH-001`)

**Impact on Option A:**
- Option A refactors `SmartPreAggregationService.GetAccountSumAsync` to query `AccountingEntries.AccountCode`, `EntryType`, `Amount`, `PeriodYear`, `PeriodMonth`
- If these columns don't exist in the dev DB → **runtime SQLite error** when Wave 2 code is tested against dev DB
- Tests using in-memory SQLite + `EnsureCreated()` will have correct schema (no issue)
- **BLOCKER for Wave 2 manual/integration verification against dev DB**

**Resolution (tracked as W0-T12 in Wave 0 task card):**
- Delete stale dev DB files (`vanan_shoperp.db` + `bin/Debug/net8.0/vanan_shoperp.db`)
- Run ShopERP once → `EnsureCreatedAsync()` regenerates schema with current EF Core model (including all `AccountingEntryConfiguration` domain columns)
- Verify `PRAGMA table_info(AccountingEntries)` has all domain columns
- **Needs user confirmation** before deleting dev DB files (environment reset, not production data)

**Decision validity:** Option A is STILL the correct architecture decision. The stale DB is an environment issue, not an architecture issue. Once DB is recreated, Option A works as designed.
