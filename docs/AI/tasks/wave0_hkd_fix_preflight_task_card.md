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
