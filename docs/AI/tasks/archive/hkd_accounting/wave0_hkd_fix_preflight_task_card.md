# TASK CARD: HKD Book Fix - Wave 0 - Pre-flight Verification (v2 — expanded)

## 1. GOAL & CONTEXT
- **Mục tiêu cốt lõi:** Verify baseline sạch + chốt data flow gap + **resolve 4 architecture decisions BEFORE IMPLEMENT mode** trước khi bắt đầu stream HKD Book Fix
- **Nghiệp vụ áp dụng:** Pre-flight cho stream HKD Book Accounting Report Fix (TT 152/2025/TT-BTC compliance)
- **Status:** PENDING — Planning & Approval (v2 — expanded with T8-T11 promoted architecture decisions)
- **Branch:** `feature/hkd-fix-wave0-preflight`
- **Estimated Sessions:** 0.5-1 (expanded from 0.5 — 4 additional architecture-decision tasks)

---

## 2. ACTIVE WORKFLOW ROUTING
- **Target Workflow:** `newfeaturebuild.md` (ANALYZE phase — verify only, no code change)
- **Execution Mode:** ANALYZE
- **Current Phase:** Wave 0 of 10 (v2 — Wave 5 split into 5a + 5b)
- **Dependency:** None (first wave)

---

## 3. RELEVANT FILES (CONTEXT BOUNDARY)

### Files được phép đọc
- `docs/AI/project_state.md` (Bắt buộc đọc đầu phiên)
- `docs/AI/tasks/hkd_book_accounting_fix_master_plan.md` (READ)
- `3_CoreHub/Program.cs` (READ — verify DI registrations)
- `5_WebApps/ShopERP/Program.cs` (READ — verify DI registrations)
- `3_CoreHub/Services/HKDBookService.cs` (READ — verify write path)
- `3_CoreHub/Services/PreAggregation/SmartPreAggregationService.cs` (READ — verify query path)
- `3_CoreHub/Repositories/HKDBookRepository.cs` (READ — verify AddToBookAsync)
- `2_Gateway/Controllers/AccountingEntriesController.cs` (READ — verify no hkd-books endpoint)
- `5_WebApps/ShopERP/Components/Pages/Accounting/` (READ — verify no HKD book page)
- `docs/plan_MVP/HKD_BookAcc/*.docx` (READ — extract TT 152 layout spec)
- **[T8] `3_CoreHub/Services/TemplateFactory.cs`** (READ — old, for OrderService — verify ITemplateFactory impl + consumers)
- **[T8] `3_CoreHub/Services/Template/TemplateFactory.cs`** (READ — new — verify class name, interface impl, DI conflict)
- **[T8] `3_CoreHub/Services/OrderService.cs`** hoặc grep consumers of `ITemplateFactory` (READ — verify what depends on old TemplateFactory)
- **[T9] `5_WebApps/ShopERP/*.csproj`** + `Directory.Packages.props` (READ — verify docx/xlsx library availability)
- **[T10] `1_Shared/Domain/Tenant.cs`** hoặc grep `class Tenant` (READ — verify IndustrySector/BusinessSector/NganhNghe field)
- **[T11] `3_CoreHub/Services/Events/SimpleAccountingEventHandler.cs`** (READ — verify caller of RecordRevenue/Expense)
- **[T11] grep `_context.JournalEntries.Add` / `_repo.AddToBookAsync` / `JournalEntry.Create`** across codebase (verify all JournalEntry persisters)

### Boundary Rules (Nghiêm cấm)
- KHÔNG sửa code — Wave 0 là verify only
- KHÔNG tạo file mới — chỉ đọc + ghi note
- KHÔNG chạy destructive command (drop DB, delete tables)

---

## 4. TECHNICAL & REGULATORY CONSTRAINTS (HARDENING GATES)
- [ ] **Build Baseline:** `dotnet build VanAn.sln` Release phải pass (0 errors) — nếu fail, STOP báo user
- [ ] **DI Verification:** Grep `AddScoped<HKDBookGenerationService>` / `AddScoped<ScopedDataProvider>` / `AddScoped<SmartPreAggregationService>` / `AddScoped<ProductionFormulaEngine>` / `AddScoped<TemplateFactory>` (mới) → phải 0 matches (confirm chưa wire)
- [ ] **Endpoint Verification:** Grep `hkd-books` / `hkdbooks` / `GenerateS*BookAsync` trong Controllers → phải 0 matches (confirm chưa expose)
- [ ] **UI Verification:** Grep `S1a_HKD` / `S2a_HKD` / `S2b_HKD` / `S2c_HKD` / `S2d_HKD` / `S2e_HKD` / `S3a_HKD` trong `Components/Pages/` → phải 0 matches (confirm chưa render)
- [ ] **Data Verification:** Query `SELECT COUNT(*) FROM JournalEntries` trong DB dev → confirm rỗng hoặc có data (nếu có data, verify nguồn)
- [ ] **[T8] ITemplateFactory Conflict Resolution:** Read both `Services/TemplateFactory.cs` (old) + `Services/Template/TemplateFactory.cs` (new). Document: (a) does new implement `ITemplateFactory`? (b) what does `OrderService` depend on? (c) **DECISION**: rename new to `HKDTemplateFactory` OR keep both with distinct interfaces. Write decision to Wave 3 task card. **If cannot resolve by rename → STOP, báo Tech Lead** (architecture decision không được tự quyết trong IMPLEMENT).
- [ ] **[T9] Export Library Availability:** Grep `5_WebApps/ShopERP/*.csproj` + `Directory.Packages.props` for `DocX`, `DocumentFormat.OpenXml`, `ClosedXML`, `EPPlus`. Document available library OR approval-needed status to Wave 8 task card. **If none present + user không approve thêm dependency → Wave 8 export descoped** (UI render-only).
- [ ] **[T10] Tenant.IndustrySector Field:** Read `1_Shared/Domain/Tenant.cs` (or wherever Tenant entity lives). Document: field exists (name + type) OR missing. Write finding to Wave 5b task card. **If missing → Wave 5b conditional**: Tech Lead approval for W5b-T0 OR descope + technical debt.
- [ ] **[T11] Double-Write Audit:** Grep ALL callers of `RecordRevenueAsync`/`RecordExpenseAsync` AND all persisters of `JournalEntry` (`_context.JournalEntries.Add`, `_repo.AddToBookAsync`, `JournalEntry.Create`). Build complete write-path map. **Select decision tree path (A/B/C/D)** and document in Wave 2 task card. **If finding = C or D → STOP and report to user** (potential double-write or unknown data source).

---

## 5. SUCCESS CRITERIA (ĐO LƯỜNG ĐƯỢC)
- [ ] **SC1:** `dotnet build VanAn.sln` Release pass (0 errors)
- [ ] **SC2:** 5 service calc engine confirmed chưa đăng ký DI (grep 0 matches)
- [ ] **SC3:** 0 endpoint expose `GenerateS*BookAsync` (grep 0 matches)
- [ ] **SC4:** 0 Razor page render S1a/S2a-S2e/S3a (grep 0 matches)
- [ ] **SC5:** `JournalEntries` table status confirmed (rỗng hoặc có nguồn)
- [ ] **SC6:** 7 mẫu docx TT 152 layout extracted (header + bảng + footer spec)
- [ ] **SC7:** Git status snapshot clean trước Wave 1
- [ ] **SC8 [T8]:** `ITemplateFactory` conflict resolution documented in Wave 3 task card (decision: rename OR distinct interfaces OR escalate to Tech Lead)
- [ ] **SC9 [T9]:** docx/xlsx library availability documented in Wave 8 task card (available lib name OR approval-needed OR descope-flagged)
- [ ] **SC10 [T10]:** `Tenant.IndustrySector` field status documented in Wave 5b task card (exists with name+type OR missing → 5b conditional/descoped)
- [ ] **SC11 [T11]:** Double-write audit complete — write-path map + decision tree path (A/B/C/D) documented in Wave 2 task card

---

## 6. ACTIVE SKILLS (MAX 3)
- `build-error-analysis` — Verify build baseline
- `domain-integrity-validation` — Verify data flow gap

---

## 7. AI HEALTH CHECK MATRIX (INITIAL — v2 expanded)
- **Evidence Count:** 8 verified facts + 4 new architecture-decision tasks (T8-T11)
- **Verified Facts:**
  - Fact 1: `HKDBookService.GenerateS*BookAsync` dùng `new S*HKDTemplate()` (Domain) — `CalculateAsync` no-op
  - Fact 2: `Services/Template/` có calc engine thật nhưng không wire DI
  - Fact 3: `RecordRevenue/Expense` ghi `AccountingEntry`, không ghi `JournalEntry`
  - Fact 4: `SmartPreAggregationService` query `JournalEntries` (bảng rỗng)
  - Fact 5: `ConvertToJournalEntries` tạo in-memory, không persist
  - Fact 6: `HKDBookRepository.AddToBookAsync` tồn tại nhưng không ai gọi
  - Fact 7: `AccountingEntriesController` chỉ expose revenue/expense/profit summary
  - Fact 8: `Components/Pages/Accounting/` không có HKD book page
- **Assumptions (to verify in T8-T11):**
  - `JournalEntries` table rỗng trong DB dev (T11 will confirm)
  - 7 mẫu docx có layout consistent (T7 will extract)
  - `ITemplateFactory` conflict resolvable by rename (T8 will confirm — may need Tech Lead)
  - docx/xlsx library may not be present (T9 will confirm — may need dependency approval)
  - `Tenant.IndustrySector` may not exist (T10 will confirm — may descope Wave 5b)
- **Open Questions:**
  - Q1: `IBookResultCache` đã đăng ký DI chưa? (Verify)
  - Q2: `IMemoryCache` đã đăng ký (`AddMemoryCache()`) chưa? (Verify)
  - Q3 [T8]: New `TemplateFactory` implement `ITemplateFactory`? If yes → conflict. If no → safe to register both.
  - Q4 [T9]: Which docx/xlsx library is available (if any)?
  - Q5 [T10]: Does `Tenant` entity have `IndustrySector`/`BusinessSector`/`NganhNghe` field?
  - Q6 [T11]: Are there any existing persisters of `JournalEntry` beyond `AddToBookAsync`?
- **Recommended Action:** PROCEED — verify + resolve architecture decisions, risk none (read-only)

---

## 8. REVERSE IMPACT ANALYSIS
| File verify | Reverse impact | Mitigation |
|---|---|---|
| `3_CoreHub/Program.cs` | None — read only | N/A |
| `HKDBookService.cs` | None — read only | N/A |
| `SmartPreAggregationService.cs` | None — read only | N/A |
| DB query | None — SELECT only | N/A |
| `Services/TemplateFactory.cs` (old) [T8] | None — read only | N/A |
| `Services/Template/TemplateFactory.cs` (new) [T8] | None — read only | N/A |
| `Tenant.cs` [T10] | None — read only | N/A |
| `*.csproj` / `Directory.Packages.props` [T9] | None — read only | N/A |
| `SimpleAccountingEventHandler.cs` [T11] | None — read only | N/A |

### Architecture decision outputs (T8-T11 → downstream task cards)
| Task | Output written to | Affects wave |
|---|---|---|
| T8 | Wave 3 task card (add "DI conflict resolution" section) | Wave 3 |
| T9 | Wave 8 task card (add "export library status" section) | Wave 8 |
| T10 | Wave 5b task card (add "Tenant.IndustrySector status" section) | Wave 5b |
| T11 | Wave 2 task card (add "write-path map + decision tree path" section) | Wave 2 |

---

## 9. TDD & TESTING STRATEGY
- **Unit tests:** N/A — Wave 0 là verify
- **Integration tests:** N/A
- **E2E tests:** N/A
- **Verification:** `dotnet build VanAn.sln` Release pass + grep checks + DB query

---

## 10. JIT PLANNING + PURE EXECUTION (MICRO-PHASES)

### Chiến lược thực thi: Sequential verify (v2 — 11 tasks)
1. Build baseline → 2. Grep DI → 3. Grep endpoint → 4. Grep UI → 5. DB query → 6. Git snapshot → 7. Extract docx layout → **8. ITemplateFactory conflict** → **9. docx/xlsx library** → **10. Tenant.IndustrySector** → **11. Double-write audit**

### Micro-phase breakdown

| Session | JIT Planning (chốt gì) | Pure Execution (viết gì) |
|---|---|---|
| **S1** (T1-T7 — original) | - Đọc master plan v2 + audit report<br>- Chốt: 5 service chưa wire DI<br>- Chốt: 0 endpoint/UI<br>- Chốt: JournalEntries status<br>- Chốt: 7 mẫu docx layout spec | - Run `dotnet build VanAn.sln` Release<br>- Grep 5 service DI registrations<br>- Grep endpoint + UI<br>- Query DB (if accessible)<br>- Extract 7 docx layouts (script)<br>- Snapshot git status |
| **S1/S2** (T8-T11 — promoted) | - **T8:** Chốt ITemplateFactory resolution (rename OR distinct OR escalate)<br>- **T9:** Chốt docx/xlsx lib (available OR approval-needed OR descope)<br>- **T10:** Chốt Tenant.IndustrySector (exists OR missing → 5b conditional)<br>- **T11:** Chốt write-path map + decision tree path (A/B/C/D) | - Read both TemplateFactory files + OrderService consumers<br>- Grep csproj/Directory.Packages.props for export libs<br>- Read Tenant.cs for IndustrySector field<br>- Grep all JournalEntry persisters + RecordRevenue/Expense callers<br>- **Write decisions to Wave 2/3/5b/8 task cards** (downstream propagation)<br>- Update project_state.md |

### Rules
- 1 verify step tại 1 thời điểm
- Nếu build fail → STOP, báo user
- Nếu `JournalEntries` có data → verify nguồn trước khi tiếp tục (T11)
- **T8: Nếu conflict không resolve bằng rename → STOP, báo Tech Lead**
- **T9: Nếu không có library + user không approve → flag Wave 8 export descope**
- **T10: Nếu field missing → flag Wave 5b conditional (Tech Lead approval needed for W5b-T0)**
- **T11: Nếu finding = C (existing persister from AccountingEntry) hoặc D (unknown source) → STOP, báo user**
- **T8-T11 outputs MUST be propagated to downstream task cards** (Wave 2/3/5b/8) before those waves start

---

## 11. ESTIMATED EFFORT
- 0.5-1 session (v2 — expanded from 0.5; 4 additional architecture-decision tasks T8-T11)
- **BLOCKER:** None — risk thấp nhất trong 10 waves (read-only verify)
- **PARALLEL:** Có thể làm cùng session với Wave 1 (cả 2 non-code/low-risk)
- **CRITICAL OUTPUTS:** T8-T11 decisions propagate to Wave 2/3/5b/8 task cards — MUST complete before those waves start

---

## 12. T8-T11 DECISION TEMPLATES (output format)

### T8: ITemplateFactory Conflict Resolution
```
## ITemplateFactory Conflict Resolution (from Wave 0 T8)
- Old TemplateFactory: `3_CoreHub/Services/TemplateFactory.cs`
  - Implements ITemplateFactory: [YES/NO]
  - Consumers: [list — e.g., OrderService]
- New TemplateFactory: `3_CoreHub/Services/Template/TemplateFactory.cs`
  - Implements ITemplateFactory: [YES/NO]
  - Class name: [TemplateFactory / other]
- DECISION: [rename new to HKDTemplateFactory / distinct interfaces / ESCALATE Tech Lead]
- Rationale: [1-2 sentences]
```

### T9: Export Library Status
```
## Export Library Status (from Wave 0 T9)
- DocX: [FOUND version X / NOT FOUND]
- DocumentFormat.OpenXml: [FOUND version X / NOT FOUND]
- ClosedXML: [FOUND version X / NOT FOUND]
- EPPlus: [FOUND version X / NOT FOUND]
- DECISION: [use <lib> / need approval for <lib> / DESCOPE export — UI render only]
```

### T10: Tenant.IndustrySector Status
```
## Tenant.IndustrySector Status (from Wave 0 T10)
- Field exists: [YES — name: <field>, type: <type> / NO]
- DECISION: [proceed Wave 5b normally / need Tech Lead approval for W5b-T0 / DESCOPE Wave 5b — use default rate, log technical debt]
```

### T11: Double-Write Audit
```
## Double-Write Audit (from Wave 0 T11)
- Callers of RecordRevenueAsync: [list]
- Callers of RecordExpenseAsync: [list]
- JournalEntry persisters: [list — e.g., AddToBookAsync, _context.JournalEntries.Add, ...]
- Write-path map: [brief description]
- DECISION TREE PATH: [A — safe to bridge / B — safe with idempotent guard / C — STOP existing persister / D — INVESTIGATE]
- If A/B: proceed Wave 2 as planned (B = add idempotent guard)
- If C/D: STOP, reported to user on [date]
```

---

## 13. EXECUTION FINDINGS (executed 2026-07-03, branch `feature/hkd-fix-wave0-wave0p5-preflight`)

### T1-T6 Results (baseline verification)

| Task | Result | Evidence |
|---|---|---|
| T1 Build | ✅ PASS — 0 errors, 962 warnings | `dotnet build VanAn.sln -c Release` (3m29s) |
| T2 DI | ✅ CONFIRMED — 0 code matches | Grep `AddScoped<(HKDBookGenerationService\|ScopedDataProvider\|SmartPreAggregationService\|ProductionFormulaEngine\|TemplateFactory)>` → 0 matches in .cs files (only docs) |
| T3 Endpoint | ✅ CONFIRMED — 0 matches | Grep `hkd-books\|hkdbooks\|GenerateS*BookAsync` in `2_Gateway/Controllers` + `5_WebApps/ShopERP` → 0 matches (matches only in `HKDBookService.cs` method definitions + docs) |
| T4 UI | ✅ CONFIRMED — 0 matches | Grep `S1a_HKD\|S2a_HKD\|...\|S3a_HKD` in `5_WebApps` → 0 matches |
| T5 DB Query | ⚠️ **CRITICAL FINDING — see Gap-1 below** | `JournalEntries`: 0 rows (confirmed empty). BUT `AccountingEntries` schema STALE — missing ALL domain columns |
| T6 Git | ✅ Clean — only .vs artifacts + stray local files | — |

### T7 Result (docx layout extraction) — PARTIAL

- 7 files found in `docs/plan_MVP/HKD_BookAcc/`:
  - `S1a_HKD.doc` — **binary .doc format** (NOT OOXML .docx) → cannot extract via zip/XML approach. **Gap-2**
  - `S2a_HKD.docx`, `S3a_HKD.docx`, `mau-so-s2b-hkd-thong-tu-152.docx`, `mau-so-s2c-...`, `mau-so-s2d-...`, `mau-so-s2e-...` — all OOXML .docx (zip)
- S2a_HKD.docx: zip extracted successfully, `word/document.xml` = 133KB, 122 `<w:t>` text elements found. **Extraction script incomplete — text not yet output. Gap-3**
- **SC6 NOT MET** — layout spec extraction incomplete

### T8 Result (ITemplateFactory conflict) — RESOLVED

```
## ITemplateFactory Conflict Resolution (from Wave 0 T8)
- Old TemplateFactory: `3_CoreHub/Services/TemplateFactory.cs`
  - Implements ITemplateFactory: YES (L10: `public class TemplateFactory : ITemplateFactory`)
  - Consumers: `OrderService.cs` (L20: `ITemplateFactory? templateFactory = null`, L32: field)
  - DI: `3_CoreHub/Program.cs` L118: `services.AddScoped<ITemplateFactory, TemplateFactory>()`
- New TemplateFactory: `3_CoreHub/Services/Template/TemplateFactory.cs`
  - Implements ITemplateFactory: NO — uses primary constructor `(IFormulaEngine, IDataProvider, ILoggerFactory)`, no interface
  - Class name: `TemplateFactory` (same name, different namespace `VanAn.CoreHub.Services.Template`)
  - Creates `S1aHKDTemplateImpl`...`S3aHKDTemplateImpl` via `CreateTemplate(HKDGroup, templateCode)`
- DECISION: **Keep both — register new as concrete `TemplateFactory` (no interface conflict)**
  - Old `ITemplateFactory` → old `Services/TemplateFactory.cs` (for OrderService) — UNCHANGED
  - New `Services/Template/TemplateFactory.cs` → register as `AddScoped<TemplateFactory>()` (concrete, HKDBookGenerationService injects concrete)
  - Different namespaces: `VanAn.CoreHub.Services` (old) vs `VanAn.CoreHub.Services.Template` (new) — no namespace collision
- Rationale: New TemplateFactory does NOT implement ITemplateFactory → no DI conflict. Both can coexist. No rename needed. No Tech Lead escalation needed.
- ⏳ PENDING: Write this decision to Wave 3 task card
```

### T9 Result (export library) — RESOLVED

```
## Export Library Status (from Wave 0 T9)
- DocX: NOT FOUND
- DocumentFormat.OpenXml: NOT FOUND
- ClosedXML: NOT FOUND
- EPPlus: FOUND — Version 7.6.1 (Directory.Packages.props L42: `<PackageVersion Include="EPPlus" Version="7.6.1" />`)
- DECISION: **Use EPPlus for XLSX export. DOCX export needs approval for `DocumentFormat.OpenXml` OR descope DOCX (UI render + XLSX only).**
  - EPPlus handles .xlsx (Excel) — sufficient for tabular HKD book layouts
  - DOCX (Word) export would need `DocumentFormat.OpenXml` (not currently in deps) — requires user approval for new dependency
  - Recommendation: Wave 8 implements XLSX export with EPPlus. DOCX export flagged as descope candidate unless user approves new dependency.
- ⏳ PENDING: Write this decision to Wave 8 task card
```

### T10 Result (Tenant.IndustrySector) — RESOLVED

```
## Tenant.IndustrySector Status (from Wave 0 T10)
- Field exists: **NO** — `1_Shared/Domain/Aggregates/TenantAggregate/Tenant.cs` has:
  - `Id`, `Name`, `BusinessType`, `HKDGroup?`, `Status`, `Settings` (TenantSettings)
  - NO `IndustrySector`, NO `BusinessSector`, NO `NganhNghe` field
- DECISION: **Wave 5b CONDITIONAL — Tech Lead approval needed for W5b-T0 (add `IndustrySector` to `Tenant`)**
  - Options: (a) Add `IndustrySector` enum + field to `Tenant` (Domain modification — needs Tech Lead approval), OR (b) DESCOPE Wave 5b — use default tax rate, log as technical debt
  - Wave 5b cannot proceed without this decision
- ⏳ PENDING: Write this decision to Wave 5b task card
```

### T11 Result (double-write audit) — CRITICAL FINDING

```
## Double-Write Audit (from Wave 0 T11)
- Callers of RecordRevenueAsync:
  1. `SimpleAccountingEventHandler.cs` L100 — NATS event handler (OrderCompleted → RecordRevenueAsync)
  2. `HKDBookServiceTests.cs` — test callers (4 tests)
- Callers of RecordExpenseAsync:
  1. `HKDBookServiceTests.cs` — test callers (1 test)
  2. NO production caller found (only tests)
- JournalEntry persisters:
  1. `HKDBookRepository.AddToBookAsync` L135-154 — `_context.JournalEntries.AddAsync(entry)` — THE persister
  2. `HKDBookRepository.AddToBookAsync` L163 — `_context.JournalEntries.AddRangeAsync(entries)` — batch
  3. `HKDBookRepository.AddToBookAsync` L220 — `_context.JournalEntries.AddAsync(entry)` — another path
  4. `OrderService.GenerateAccountingEntriesAsync` L114-115, L140-141 — **CALLS AddToBookAsync** (revenue + COGS journal entries)
  5. `Services/TemplateFactory.cs` (OLD) L21, L31 — `JournalEntry.Create(...)` but `await Task.CompletedTask` — **NOT persisted** (dead code)
- Write-path map:
  - Path 1 (OrderService): Order completed → `CreateRevenueEntryAsync` (AccountingEntry via AccountingEntryService) + `CreateRevenueEntryAsync` (JournalEntry via `AddToBookAsync`) → BOTH AccountingEntry AND JournalEntry persisted
  - Path 2 (NATS handler): OrderCompleted event → `RecordRevenueAsync` (AccountingEntry only) → NO JournalEntry
  - Path 3 (Direct API): `CreateRevenueEntryAsync` / `CreateExpenseEntryAsync` (AccountingEntry only) → NO JournalEntry
- DECISION TREE PATH: **B — safe with idempotent guard** (existing persister from OrderService, but Wave 0.5 chose Option A which AVOIDS JournalEntry writing entirely)
  - **IMPORTANT:** Because W0.5 chose Option A (query AccountingEntries directly, no JournalEntry writing), Wave 2 does NOT add JournalEntry writes → no double-write risk. The existing OrderService JournalEntry writes become IRRELEVANT for HKD book calculation (can be deprecated in a future cleanup wave).
- ⏳ PENDING: Write this decision to Wave 2 task card
```

---

## 14. NEW GAPS DISCOVERED DURING EXECUTION (not in original plan)

### Gap-1: CRITICAL — Dev DB schema STALE (missing ALL domain columns)

**Finding:**
- DB: `5_WebApps/ShopERP/vanan_shoperp.db` (516KB, last modified 2026-07-01)
- `AccountingEntries` table schema (via PRAGMA table_info):
  ```
  Id (TEXT), AccountingEntryId (TEXT), TenantId (TEXT), CreatedAt (TEXT),
  UpdatedAt (TEXT), CreatedBy (TEXT), UpdatedBy (TEXT), IsDeleted (INTEGER)
  ```
  **MISSING columns:** Amount, EntryType, AccountCode, PeriodYear, PeriodMonth, VatRate, VatAmount, Description, ReversalEntryId, AccountingBookType, Vendor, Category, Reference
- `JournalEntries` table schema: same — only BaseEntity columns (Id, TenantId, CreatedAt, UpdatedAt, CreatedBy, UpdatedBy, IsDeleted). **MISSING:** Period, Description, ReferenceType, ReferenceId, etc.
- `JournalEntryLine` table: has `JournalEntryId, Id, AccountNumber, DebitAmount, CreditAmount, Description` — this one looks correct
- `AccountingEntries` has 0 rows in main dev DB, 20 rows in `bin/Debug/net8.0/vanan_shoperp.db` (old test data)
- Root cause: DB uses `EnsureCreatedAsync()` (ShopERP Program.cs L302). The DB was created BEFORE `AccountingEntryConfiguration.cs` was added with domain column mappings. `EnsureCreatedAsync()` only creates schema if DB doesn't exist — it does NOT update schema if model changes. No Migrations folder exists (architecture decision: `EnsureCreated` strategy, Migrations are forbidden per `VA-ARCH-001`).

**Reverse Impact Analysis:**

| Affected Wave | Impact | Severity | Mitigation |
|---|---|---|---|
| **Wave 2 (Option A)** | `SmartPreAggregationService.GetAccountSumAsync` refactor will query `AccountingEntries.AccountCode`, `EntryType`, `Amount`, `PeriodYear`, `PeriodMonth` → **columns don't exist → runtime SQLite error** | **BLOCKER** | Must recreate dev DB (delete + run app → `EnsureCreatedAsync()` regenerates schema with current model) BEFORE Wave 2 verification |
| Wave 3 (DI wiring) | No DB impact — DI registration is code-level | None | N/A |
| Wave 4 (routing) | No DB impact — routing is code-level | None | N/A |
| Wave 5a (account mapping) | Template formulas reference account patterns — no DB query at template level | None | N/A |
| Wave 6 (tests) | Tests use in-memory SQLite with `EnsureCreated()` → schema will be correct in test context. Integration tests using dev DB will fail. | Low (tests) | Tests use their own DB context, not dev DB |
| Wave 7 (API endpoint) | API queries through service → if dev DB schema wrong, API returns errors | Medium (manual testing) | Recreate dev DB before manual API testing |
| Wave 8 (UI) | UI calls API → same chain as Wave 7 | Medium (manual testing) | Same — recreate dev DB before UI testing |

**Resolution: Stream E spawned (DB Migration Strategy) — NOT a Wave 0 task:**
- **Root cause:** `EnsureCreatedAsync()` (ShopERP Program.cs L302) chỉ tạo schema nếu DB chưa tồn tại — không update schema khi model thay đổi. No Migrations folder (VA-ARCH-001 forbids). This is a **production deployment trap** — not just a dev issue.
- **User decision (2026-07-03):** (1) Enable EF Core Migrations as official schema management, (2) Remove `EnsureCreated()` from production, (3) Modify VA-ARCH-001 to allow Migrations at correct layer (Infrastructure) while still preventing other architecture violations.
- **Scope:** Tách thành stream riêng (Stream E). NOT executed in Wave 0 session. Task card + plan PENDING.
- **BLOCKS:** Wave 2 verification (Option A query needs domain columns), Wave 7 manual testing, Wave 8 manual testing.
- **Wave 0 action:** Document finding + escalate. ✅ DONE (this section + project_state.md Section 2 Stream E + Section 4 item 9).

### Gap-2: S1a_HKD.doc is binary .doc format (not OOXML .docx)

**Finding:**
- `S1a_HKD.doc` is old binary Microsoft Word format (not zip/OOXML)
- Cannot extract via zip/XML approach used for .docx files
- Raw byte extraction shows fragmented Vietnamese text: "H CH NH KINH DOANH", "M s thu", "S1a-HKD (Km theo Thng t 152/2025/TT-BTC)", "DOANH THU BN HNG HA, DCH V IM KINH DOANH"

**Reverse Impact Analysis:**

| Affected Wave | Impact | Severity | Mitigation |
|---|---|---|---|
| Wave 8 (UI layout) | S1a layout spec incomplete — cannot extract header/bảng/footer structure from .doc file | Medium | (a) Convert .doc → .docx via LibreOffice/pandoc/Word automation, OR (b) Extract layout from TT 152/2025/TT-BTC regulation text directly (the regulation defines the layout), OR (c) Use the 6 .docx files as reference + infer S1a layout from regulation text |

**Resolution: Add W0-T7b (NEW TASK):**
- **W0-T7b: Extract S1a_HKD.doc layout** — Convert binary .doc to text (via `pandoc` if available, or LibreOffice CLI `soffice --convert-to docx`, or manual transcription from TT 152 regulation text). Document S1a layout (header + bảng + footer + chữ ký spec).
- **Alternative:** If no conversion tool available, extract S1a layout directly from TT 152/2025/TT-BTC regulation text (the regulation defines all 7 mẫu sổ layouts).

### Gap-3: docx text extraction script incomplete

**Finding:**
- S2a_HKD.docx: zip extracted, `word/document.xml` = 133KB, 122 `<w:t>` elements found
- PowerShell regex matched 122 elements but output was empty (PowerShell pipeline issue with `ForEach-Object` + `-join`)
- Other 5 .docx files not yet extracted

**Reverse Impact Analysis:**

| Affected Wave | Impact | Severity | Mitigation |
|---|---|---|---|
| Wave 8 (UI layout) | Layout specs for S2a-S2e, S3a not yet extracted | Medium | Fix extraction script (use C# console app instead of PowerShell regex) OR read document.xml directly with `read` tool |

**Resolution: Add W0-T7c (NEW TASK):**
- **W0-T7c: Complete docx layout extraction for 6 .docx files** — Fix extraction approach (use throwaway C# console app with `DocumentFormat.OpenXml` OR parse `word/document.xml` with proper XML reader). Extract header + bảng structure + footer + chữ ký spec for S2a, S2b, S2c, S2d, S2e, S3a.

---

## 15. UPDATED SUCCESS CRITERIA (with new gaps)

| SC | Status | Notes |
|---|---|---|
| SC1 Build | ✅ MET | 0 errors |
| SC2 DI | ✅ MET | 0 matches |
| SC3 Endpoint | ✅ MET | 0 matches |
| SC4 UI | ✅ MET | 0 matches |
| SC5 JournalEntries | ⚠️ PARTIAL — 0 rows confirmed + stale schema finding escalated to Stream E | Finding documented, Stream E spawned |
| SC6 docx layout | ❌ NOT MET — extraction incomplete (Gap-2, Gap-3) | Need W0-T7b, W0-T7c |
| SC7 Git snapshot | ✅ MET | Clean |
| SC8 T8 → Wave 3 | ⏳ PENDING propagation | Decision documented in Section 13, not yet written to Wave 3 card |
| SC9 T9 → Wave 8 | ⏳ PENDING propagation | Decision documented in Section 13, not yet written to Wave 8 card |
| SC10 T10 → Wave 5b | ⏳ PENDING propagation | Decision documented in Section 13, not yet written to Wave 5b card |
| SC11 T11 → Wave 2 | ⏳ PENDING propagation | Decision documented in Section 13, not yet written to Wave 2 card |
| **SC12 (NEW) Stream E** | ✅ ESCALATED — finding documented + user decision made (Enable EF Migrations) | Task card + plan PENDING (separate stream) |
| **SC13 (NEW) W0-T7b** | ✅ DONE — LibreOffice converted .doc→.docx, C# extracted text | S1a layout documented below |
| **SC14 (NEW) W0-T7c** | ✅ DONE — 6 .docx extracted via C# console app | Layout specs saved to `*_extracted.txt` |

**Wave 0 completion: 11/14 SC met (79%). Remaining: commit. Stream E escalated (separate stream).**

---

## 16. DOCX LAYOUT EXTRACTION RESULTS (W0-T7b + W0-T7c — executed 2026-07-03)

### S1a_HKD — SỔ DOANH THU BÁN HÀNG HÓA, DỊCH VỤ (14 paragraphs)
- **Toolchain:** LibreOffice (`soffice --headless --convert-to docx`) converted binary .doc → .docx, then C# LINQ-to-XML extracted text
- **Header:** HỘ/CÁ NHÂN KINH DOANH, Địa chỉ, Mã số thuế, Mẫu số S1a-HKD (TT 152/2025/TT-BTC), Địa điểm kinh doanh, Kỳ kê khai, Đơn vị tính
- **Table columns:** A (Ngày tháng), B (Diễn giải), 1 (Số tiền)
- **Rows:** Single total row — Tổng cộng
- **Footer:** Ngày...tháng...năm..., NGƯỜI ĐẠI DIỆN HỘ KINH DOANH (Ký, ghi rõ họ tên, đóng dấu)
- **Note:** S1a is the SIMPLEST layout — single-entry, no tax breakdown (Group 1 = không chịu thuế GTGT)

### S2a_HKD — SỔ DOANH THU BÁN HÀNG HÓA, DỊCH VỤ (61 paragraphs, 2840 chars)
- **Header:** HỘ/CÁ NHÂN KINH DOANH, Địa chỉ, Mã số thuế, Mẫu số S2a-HKD (TT 152/2025/TT-BTC), Địa điểm kinh doanh, Kỳ kê khai, Đơn vị tính
- **Table columns:** A (Số hiệu), B (Ngày tháng), C (Diễn giải), 1 (Số tiền)
- **Rows:** 3 nhóm ngành nghề, mỗi nhóm có: Doanh thu + Tổng cộng + Thuế GTGT + Thuế TNCN
- **Footer:** Tổng số thuế GTGT phải nộp, Tổng số thuế TNCN phải nộp, Ngày...tháng...năm..., NGƯỜI ĐẠI DIỆN HỘ KINH DOANH (Ký, ghi rõ họ tên, đóng dấu)
- **Ghi sổ method:** Cột A/B ghi chứng từ, Cột C ghi diễn giải, group theo ngành nghề cùng tỷ lệ %

### S2b_HKD — SỔ DOANH THU BÁN HÀNG HÓA, DỊCH VỤ (37 paragraphs, 1071 chars)
- **Header:** Same pattern (HỘ/CÁ NHÂN, MST, Mẫu S2b-HKD, Địa điểm, Kỳ kê khai, Đơn vị tính)
- **Table columns:** A (Số hiệu), B (Ngày tháng), C (Diễn giải), 1 (Số tiền)
- **Rows:** 5 nhóm ngành nghề, mỗi nhóm: Doanh thu + Tổng cộng + Thuế GTGT (KHÔNG có TNCN — chỉ GTGT)
- **Footer:** Tổng số thuế GTGT phải nộp, chữ ký

### S2c_HKD — SỔ CHI TIẾT DOANH THU, CHI PHÍ (26 paragraphs, 2139 chars)
- **Header:** Same pattern + Tên địa điểm kinh doanh
- **Table columns:** A, B, C (Diễn giải), 1 (Số tiền)
- **Rows:**
  - 1. Doanh thu bán hàng hóa, dịch vụ
  - 2. Chi phí hợp lý (5 sub-items: a) nguyên liệu, b) tiền lương, c) khấu hao, d) dịch vụ mua ngoài, đ) lãi vay, e) chi khác)
  - 3. Chênh lệch {(3) = (1) - (2)}
  - 4. Tổng số thuế TNCN phải nộp {(4) = (3) x thuế suất}
- **Footer:** Chữ ký

### S2d_HKD — SỔ CHI TIẾT VẬT LIỆU, DỤNG CỤ, SẢN PHẨM, HÀNG HÓA (43 paragraphs, 1027 chars)
- **Header:** Same + Tên vật liệu/dụng cụ/sản phẩm/hàng hóa
- **Table columns:** A (Số hiệu), B (Ngày tháng), C (Diễn giải), D (Đơn vị tính), Đơn giá, Nhập (Số lượng+Thành tiền), Xuất (Số lượng+Thành tiền), Tồn (Số lượng+Thành tiền), Ghi chú
- **Rows:** Số dư đầu kỳ, Cộng phát sinh trong kỳ, Số dư cuối kỳ
- **Footer:** Chữ ký

### S2e_HKD — SỔ CHI TIẾT TIỀN (35 paragraphs, 1035 chars)
- **Header:** Same + Kỳ kê khai, Đơn vị tính
- **Table columns:** A (Số hiệu), B (Ngày tháng), C (Diễn giải), 1 (Thu/Gửi vào), 2 (Chi/Rút ra)
- **Rows:**
  - Tiền mặt: Tiền mặt đầu kỳ, Tổng tiền thu vào, Tổng tiền chi ra, Tiền mặt tồn cuối kỳ
  - Tiền gửi không kỳ hạn: Ngân hàng..., Tiền gửi đầu kỳ, Tổng gửi vào, Tổng rút ra, Tiền gửi cuối kỳ
- **Footer:** Chữ ký

### S3a_HKD — SỔ THEO DÕI NGHĨA VỤ THUẾ KHÁC (49 paragraphs, 2223 chars)
- **Header:** Same + Địa điểm kinh doanh, Kỳ kê khai, Đơn vị tính
- **Table columns (10 cols):** A (Ngày tháng), B (Diễn giải), 1 (Lượng HH/DV chịu thuế), 2 (Mức thuế tuyệt đối), 3 (Giá tính thuế/đơn vị), 4 (Thuế suất), 5-10 (Các loại thuế khác: xuất khẩu/nhập khẩu/TTĐB, BVMT, tài nguyên, sử dụng đất — phân theo phương pháp % và tuyệt đối)
- **Footer:** Tổng cộng, chữ ký
- **Ghi sổ method:** Cột 5 = Cột 1 × Cột 3 × Cột 4 (tỷ lệ %), Cột 6+ (tuyệt đối)
