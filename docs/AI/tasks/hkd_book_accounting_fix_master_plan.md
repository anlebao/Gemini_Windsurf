# MASTER IMPLEMENTATION PLAN — HKD Book Accounting Report Fix (TT 152/2025/TT-BTC Compliance)

> **Status:** ✅ APPROVED (v3) — Wave 0 + 0.5 + 1 + 2 ✅ COMPLETE & merged; Wave 3 next
> **Created:** 2026-07-03
> **Last Updated:** 2026-07-03 (Wave 2 complete — Option A implemented, SQLite decimal SumAsync fix)
> **Target Workflow:** `newfeaturebuild.md` (ANALYZE → IMPLEMENT)
> **Branch strategy:** `main` → feature branches per wave (sequential)
> **Execution principle:** Dependency-ordered fix (data → DI → routing → formulas → tests → API → UI → export)
> **Prerequisite:** HKD Book report audit (2026-07-03 — see Section 1 root causes)

---

## 0. EXECUTION RULES

### Dependency-Ordered Fix Strategy
**Nguyên tắc cốt lõi:** Khác với E2E cleanup (pattern-based, có thể làm song song), stream này có **chuỗi phụ thuộc nghiêm ngặt**:
```
Data (Wave 2) → DI (Wave 3) → Routing (Wave 4) → Formulas (Wave 5) → Tests (Wave 6) → API (Wave 7) → UI/Export (Wave 8)
```
Wave 1 (encoding) là wave duy nhất có thể làm song song với Wave 0/2 (độc lập với data flow).

**Bước 1: INVESTIGATE & ANALYZE (Đã xong — audit 2026-07-03)**
- Đã đọc: HKDBookService, HKDBookGenerationService, TemplateFactory (cũ + mới), BaseHKDBookTemplate, TemplateCalculationEngine, ProductionFormulaEngine, ScopedDataProvider, SmartPreAggregationService, HKDTemplates (Domain), GenericHKDBook, AccountingEntry, JournalEntry, HKDBookRepository, SimpleAccountingEventHandler, AccountingEntriesController, 2 test files, 7 mẫu docx TT 152
- Đã verify: DI registrations (grep), data flow gap (AccountingEntries vs JournalEntries), no endpoint exposes book generation, no UI page renders books

**Bước 2: IMPLEMENT (Execution Phase)**
- Mỗi wave fix 1 tầng trong stack, build pass + test pass trước khi sang wave kế
- KHÔNG thay đổi approach khi đang implement
- Mỗi wave xong: `dotnet build VanAn.sln` Release pass + `guard-check.ps1` pass + commit
- Sau wave cuối: chạy integration test + smoke test subset

### Session protocol
1. **Mỗi session chỉ làm 1 wave** (trừ Wave 0 + Wave 1 có thể cùng session — cả 2 đều non-code/low-risk)
2. **Bắt đầu mỗi session:** Đọc `project_state.md` + task card wave đang làm
3. **Sau khi plan chốt:** Execution Phase
4. **Trước khi session end:** `dotnet build VanAn.sln` Release pass + commit
5. **Sau mỗi wave:** Commit với message format `[HKD-FIX WAVE X] <short description>`

### Branch protocol (v3 — per-wave merge to main, always-green)
```
main ← feature/hkd-fix-wave0-preflight          (merge after W0 verify pass)
main ← feature/hkd-fix-wave0p5-arch-decision-hkd-data-source  (merge after W0.5 decision)
main ← feature/hkd-fix-wave1-encoding-mojibake  (merge after build+guard pass)
main ← feature/hkd-fix-wave2-data-source-bridge (Option A or B per W0.5)
main ← feature/hkd-fix-wave3-wire-calc-engine-di
main ← feature/hkd-fix-wave4-route-through-generation-service
main ← feature/hkd-fix-wave5a-account-mapping-pit-fix
main ← feature/hkd-fix-wave5b-industry-sector-tax-rates  (CONDITIONAL — may descope)
main ← feature/hkd-fix-wave5c-2026-regulatory-compliance  (CRITICAL — pháp lý)
main ← feature/hkd-fix-wave6-retrofit-numeric-tests
main ← feature/hkd-fix-wave7-api-endpoint-di-smoke
main ← feature/hkd-fix-wave8-ui-docx-export-regression
```
- **Mỗi wave là branch riêng, branch từ `main` mới nhất, merge ngược về `main` sau khi pass** (build + guard + test)
- **`main` luôn green** — không có long-lived branch
- **Branch wave kế tiếp phải rebase/pull từ `main` trước khi bắt đầu** (đảm bảo có fix wave trước)
- **Wave 5b có thể skip** (descope) — không block 5c/6-8 (5c dùng default rate nếu 5b descoped)
- **Wave 5c KHÔNG skip** — CRITICAL pháp lý, phải execute
- Nếu conflict khi merge wave → resolve trên feature branch, không force-push main

### Hard rules
- **KHÔNG sửa Domain layer** (`1_Shared/Domain/*.cs`) trừ khi có Tech Lead approval (governance §Domain Modification By Mode). `HKDTemplates.cs`, `GenericHKDBook.cs`, `JournalEntry.cs` KHÔNG được sửa trong stream này.
- **KHÔNG sửa `AccountingEntry` immutability** — `AccountingEntry` remains immutable in all modes (governance Hard Stop).
- **KHÔNG thay đổi public API** của `IHKDBookService` trừ Wave 7 (endpoint mới) — giữ backward compat.
- **Mỗi wave phải pass `dotnet build VanAn.sln` Release** — 0 errors.
- **Mỗi wave phải pass `guard-check.ps1`** — windsurf-guard + architecture-guard.
- **TDD áp dụng từ Wave 6** — Wave 2-5 retrofit test sau khi logic ổn (Wave 6 gom lại).
- **Playwright DISABLED** trong Wave 1-7 — chỉ chạy E2E sau Wave 8 (UI xong).
- **Multi-tenancy phải enforce** ở mọi tầng mới (endpoint, UI, export).
- **[AMENDMENT 4 — v3] KHÔNG dual-write trong `RecordRevenueAsync`/`RecordExpenseAsync`** — Option C (hardcode 2 lệnh ghi trong cùng method) bị LOẠI per phản biện kiến trúc. Wave 0.5 phải chốt Option A (refactor SmartPreAggregationService query AccountingEntries) hoặc Option B (event-driven Outbox) TRƯỚC khi Wave 2 bắt đầu.
- **[AMENDMENT 5 — v3] HKD accounting regime = phương pháp đơn (single-entry)** theo TT 88/2021/TT-BTC + TT 152/2025/TT-BTC. Account mapping (111/511/611...) là **Internal Synthetic Mapping** để tận dụng chung Formula Engine — KHÔNG phải nghĩa vụ hạch toán kép theo TT 200/133. UI/UX + báo cáo pháp lý MUST tuân thủ chế độ HKD.
- **[AMENDMENT 5 — v3] Suất thuế 2026 phải theo Luật Thuế GTGT/TNCN sửa đổi 2025 + ND 117/2025** — 4 nhóm ngành nghề (1%/0.5%, 3%/1.5%, 5%/2%, 2%/1%). KHÔNG dùng "2,5%" (fabricated, không tồn tại trong luật). Threshold chịu thuế: 500M → **1 TỶ VND** từ 01/01/2026.

### Critical context
- **7 HKD book templates** theo TT 152/2025/TT-BTC: S1a_HKD (Group 1), S2a-S2e_HKD (Group 2), S3a_HKD (Group 3)
- **2 implementations song song**:
  - `1_Shared/Domain/HKDTemplates.cs` — record templates, `CalculateAsync` là **no-op** (comment "Formula engine handles everything" nhưng không ai gọi)
  - `3_CoreHub/Services/Template/TemplateFactory.cs` — `*TemplateImpl` kế thừa `BaseHKDBookTemplate`, gọi `TemplateCalculationEngine` → `ProductionFormulaEngine` → `ScopedDataProvider` → `SmartPreAggregationService` (query DB). **Đường này có tính thật nhưng KHÔNG wire DI.**
- **Data flow gap**: `RecordRevenueAsync`/`RecordExpenseAsync` ghi `AccountingEntry` vào bảng `AccountingEntries`. `SmartPreAggregationService` query bảng `JournalEntries.Lines` — **bảng này rỗng** vì không ai persist `JournalEntry` từ `AccountingEntry`.
- **`ConvertToJournalEntries`** (HKDBookService L718-751) tạo `JournalEntry` in-memory với 1 dòng (account 511 hoặc 611), không persist, không có đối ứng Nợ/Có — nên `SUM_ACCOUNT("632","Debit")` luôn = 0.
- **Mojibake**: `Services/Template/TemplateFactory.cs` có UTF-8 bị hỏng (`"Sá» ké toÃ¡n"`, `"Tá»ng doanh thu"`, `"VNÄ"`).
- **Account mapping sai**: `HKDBookService._vietnameseAccounts` (L22-41) — 211="Ngắn hạn vay ngân hàng" (sai, 211=TSCĐ hữu hình), 811="Lợi nhuận gộp" (sai, 811=Xác định KQKD), 521="Doanh thu dịch vụ" (sai, 521=Giảm trừ doanh thu).
- **Công thức thuế sai**: S2a template cứng `VatAmount = TotalRevenue * 0.05` + `PersonalIncomeTax = VatAmount * 0.1`. TT 152 có **nhiều tỷ lệ theo ngành nghề** (GTGT: 1%-5%; TNCN: 0,5%-2%). Không có khái niệm "nhóm ngành nghề" trong template.
- **Output là plain text**, không phải docx/xlsx — thiếu header (HỘ/CÁ NHÂN KD, MST, địa chỉ), bảng chứng từ (số hiệu + ngày tháng + diễn giải + số tiền), nhóm ngành nghề, footer (tổng thuế phải nộp + chữ ký).
- **Test che lấp bug**: `HKDBookServiceTests` chỉ assert `BookTypeCode`/`TenantId`/`Period`/`Entries.Count` — không assert `NumericValues` → bug "NumericValues rỗng" đi qua CI.
- **Không endpoint/UI**: `AccountingEntriesController` chỉ expose `revenue/summary`, `expense/summary`, `profit/summary` — không có `hkd-books/{templateCode}`. Không có trang Razor nào render S1a/S2a-S2e/S3a.

---

## 0.5. WAVE 0 — Pre-flight Verification (Non-code, start immediately)

> **Verify nhanh trước khi bắt đầu — đảm bảo baseline sạch + chốt data flow gap + resolve architecture decisions BEFORE IMPLEMENT mode**

### Tasks
| # | Task | Owner | Status |
|---|---|---|---|
| 1 | Confirm `dotnet build VanAn.sln` Release pass baseline (0 errors) | AI | ⏳ PENDING |
| 2 | Grep `3_CoreHub/Program.cs` + `5_WebApps/ShopERP/Program.cs` — list tất cả `AddScoped<...>`/`AddSingleton<...>` để confirm `HKDBookGenerationService`, `TemplateFactory` (mới), `ScopedDataProvider`, `SmartPreAggregationService`, `ProductionFormulaEngine` CHƯA đăng ký | AI | ⏳ PENDING |
| 3 | Grep toàn codebase — confirm không có endpoint `hkd-books`/`hkdbooks`/`GenerateS*BookAsync` gọi từ controller | AI | ⏳ PENDING |
| 4 | Grep `Components/Pages/` — confirm không có Razor page render S1a/S2a-S2e/S3a | AI | ⏳ PENDING |
| 5 | Verify `JournalEntries` table rỗng trong DB dev (query `SELECT COUNT(*) FROM JournalEntries`) — hoặc confirm không có code path nào persist JournalEntry từ AccountingEntry (trừ `HKDBookRepository.AddToBookAsync` không được ai gọi) | AI | ⏳ PENDING |
| 6 | Snapshot `git status` sạch trước khi bắt đầu Wave 1 | AI | ⏳ PENDING |
| 7 | Read 7 mẫu docx TT 152 (`docs/plan_MVP/HKD_BookAcc/*.docx`) — extract layout từng mẫu (header, bảng, footer) để chốt spec cho Wave 8 | AI | ⏳ PENDING |
| 8 | **[AMENDMENT 1a] Resolve `ITemplateFactory` conflict (promoted from W3-T8)** — Read both `3_CoreHub/Services/TemplateFactory.cs` (old, for `OrderService`) and `3_CoreHub/Services/Template/TemplateFactory.cs` (new). Verify: (a) does new one implement `ITemplateFactory`? (b) what does `OrderService` depend on? (c) decide resolution: rename new class to `HKDTemplateFactory` OR keep both with distinct interfaces. Document decision in Wave 3 task card BEFORE Wave 3 starts. | AI | ⏳ PENDING |
| 9 | **[AMENDMENT 1b] Verify docx/xlsx export library availability (promoted from W8-T3/T4 prereq)** — grep `5_WebApps/ShopERP/*.csproj` + `Directory.Packages.props` for `DocX`, `DocumentFormat.OpenXml`, `ClosedXML`, `EPPlus`. If none present → flag for dependency-add approval (governance: version ≥7 days old, no floating ranges). Document available library OR approval-needed status in Wave 8 task card. | AI | ⏳ PENDING |
| 10 | **[AMENDMENT 1c] Verify `Tenant.IndustrySector` field exists (W5-T7 prereq)** — Read `1_Shared/Domain/Tenant.cs` (or wherever Tenant entity lives). If `IndustrySector`/`BusinessSector`/`NganhNghe` field missing → Wave 5b becomes Domain Modeling Defect (needs Tech Lead approval) OR descope W5-T7 (use single default rate). Document finding in Wave 5 task card. | AI | ⏳ PENDING |
| 11 | **[Concern 1 prereq] Rigorous double-write audit** — grep ALL callers of `RecordRevenueAsync`/`RecordExpenseAsync` AND all persisters of `JournalEntry` (search `_context.JournalEntries.Add`, `_repo.AddToBookAsync`, `JournalEntry.Create`). Build a complete write-path map. If ANY existing path persists `JournalEntry` → Wave 2 must NOT double-write (skip bridge or deduplicate). Document the map + decision in Wave 2 task card. | AI | ⏳ PENDING |

### Tracking
- Update `project_state.md` Maintenance Log khi verify xong
- Nếu build baseline fail → STOP, báo user (cần fix build trước)
- Nếu `JournalEntries` table có data → verify nguồn (có thể đã có code path ta chưa thấy)
- **Nếu W0-T8 conflict không resolve được bằng rename → STOP, báo Tech Lead** (architecture decision không được tự quyết trong IMPLEMENT)
- **Nếu W0-T9 không có library nào + user không approve thêm dependency → Wave 8 export descoped** (UI render-only, export deferred to follow-up stream)
- **Nếu W0-T10 `Tenant.IndustrySector` missing + Tech Lead không approve Domain mod → Wave 5b descoped** (dùng default rate, ghi technical debt)

---

## 0.7. WAVE 0.5 — Architecture Decision: HKD Data Source (A vs B, loại C dual-write)

> **[AMENDMENT 4 — v3] Resolve architecture decision BEFORE Wave 2.** Phản biện cảnh báo: Option C (hardcode 2 lệnh ghi AccountingEntry + JournalEntry trong cùng `RecordRevenueAsync`) là Dual Write anti-pattern — rủi ro mất đồng bộ dữ liệu, bẩn Service layer. Wave 0.5 chọn Option A hoặc B thay thế.

**Branch:** `feature/hkd-fix-wave0p5-arch-decision-hkd-data-source`
**Estimated sessions:** 0.5-1 (ANALYZE — read + decision, no code change)
**Conflict risk:** None (decision only)
**Priority:** 0.5 (CRITICAL — block Wave 2 — quyết định kiến trúc dữ liệu)
**Task Card:** `docs/AI/tasks/wave0p5_hkd_fix_arch_decision_data_source_task_card.md` ✅ CREATED

### Background — 3 Options

| Option | Mô tả | Ưu điểm | Nhược điểm | Khi nào chọn |
|---|---|---|---|---|
| **A: Refactor SmartPreAggregationService query AccountingEntries** | `GetAccountSumAsync` thay query `JournalEntries.Lines` bằng `AccountingEntries` (filter `AccountCode.StartsWith(pattern)` + `EntryType` làm sign thay Debit/Credit). **Skip Wave 2 entirely** — không cần JournalEntry cho HKD. | Single Source of Truth, không dual write, không cần JournalEntry, effort thấp nhất | Formula Engine không dùng chung được với khối Doanh nghiệp (Doanh nghiệp cần double-entry Debit/Credit) | **HKD-only product** — không có kế hoạch mở rộng Formula Engine cho Doanh nghiệp |
| **B: Event-Driven / Outbox Pattern** | `RecordRevenueAsync` chỉ lưu `AccountingEntry` (immutable) + phát Domain Event `AccountingEntryRecorded`. Background handler (NATS subscriber hoặc Outbox worker) lắng nghe + sinh `JournalEntry` double-entry. **Wave 2 trở thành "add event handler" thay vì "modify RecordRevenue".** | Tách biệt write path, tận dụng Outbox infrastructure đã có (`OutboxRepository`, `NatsSyncWorker`, `NatsEventPublisher`), dùng chung Formula Engine với Doanh nghiệp, governance-compliant (event-driven) | Effort trung bình — thêm event + handler + idempotent guard | **Share Formula Engine với Doanh nghiệp** — có kế hoạch mở rộng |
| ~~C: Hardcode dual write trong RecordRevenue~~ | ~~`RecordRevenueAsync` gọi cả `_repository.AddAsync(entry)` + `_hkdBookRepository.AddToBookAsync(journalEntry)`~~ | ~~Effort thấp nhất~~ | ~~Dual write anti-pattern, rủi ro mất đồng bộ, bẩn Service, vi phạm Single Responsibility~~ | **LOẠI — per phản biện kiến trúc** |

### Codebase evidence (đã verify)
- `AccountingEntry` có field `AccountCode` (DMD-1 fix 2026-06-20, `1_Shared/Domain.cs` L284) + `EntryType` + `Amount` + `PeriodYear/Month` + `TenantId` → **đủ thông tin cho Option A**
- `SmartPreAggregationService.GetAccountSumAsync` (L155-185) query `_context.JournalEntries...Lines` → **refactor-able thành `_context.AccountingEntries`** cho Option A
- `OutboxRepository` + `IOutboxRepository` + `NatsSyncWorker` + `NatsEventPublisher` + `OutboxNotificationService` đã tồn tại → **Option B không cần xây mới infrastructure**
- `SimpleAccountingEventHandler` đã là NATS BackgroundService subscribe `vanan.events.ordercompleted` → **Option B thêm 1 subscription `vanan.events.accountingentryrecorded`**
- Skill `.devin/skills/outbox-pattern-implementation.md` đã có → **Option B có guideline**

### Tasks
| # | Task ID | Task | Files | Status |
|---|---|---|---|---|
| 1 | W0.5-T1 | Verify `AccountingEntry.AccountCode` được populate cho mọi entry — grep tất cả caller của `AccountingEntry.CreateRevenue`/`CreateExpense`, confirm có truyền `AccountCode` không (nếu không → Option A cần thêm field population trước khi refactor) | `1_Shared/Domain.cs` (AccountingEntry factory), grep callers | ⏳ PENDING |
| 2 | W0.5-T2 | Verify Formula Engine dependency — đọc `ProductionFormulaEngine.GetDependencies` + template formulas, confirm engine cần `Account_{pattern}_Credit/Debit` aggregates (có thể map từ `EntryType`) hay cần cứng `JournalEntries.Lines` structure | `3_CoreHub/Services/Formula/ProductionFormulaEngine.cs`, template formulas | ⏳ PENDING |
| 3 | W0.5-T3 | Verify có kế hoạch mở rộng Formula Engine cho khối Doanh nghiệp không — đọc `docs/AI/project_state.md` Section 1 (Project Overview) + roadmap, confirm HKD-only hay share engine | `docs/AI/project_state.md`, roadmap docs | ⏳ PENDING |
| 4 | W0.5-T4 | **DECISION:** Chọn Option A (refactor SmartPreAggregationService, skip Wave 2) HOẶC Option B (event-driven Outbox, Wave 2 = add event handler). Document rationale. **Option C LOẠI.** | Decision document | ⏳ PENDING |
| 5 | W0.5-T5 | If Option A → update Wave 2 task card: replace "Bridge JournalEntry persistence" bằng "Refactor SmartPreAggregationService query AccountingEntries" + skip JournalEntry creation | `wave2_hkd_fix_bridge_journal_persistence_task_card.md` (REWRITE) | ⏳ PENDING |
| 6 | W0.5-T6 | If Option B → update Wave 2 task card: replace "modify RecordRevenue/Expense" bằng "add Domain Event + NATS/Outbox handler sinh JournalEntry" + idempotent guard | `wave2_hkd_fix_bridge_journal_persistence_task_card.md` (REWRITE) | ⏳ PENDING |

### Entry criteria
- [ ] Wave 0 complete (baseline verified, T11 double-write audit done)

### Exit criteria
- [ ] **DECISION documented** (Option A or B, with rationale) — NOT Option C
- [ ] Wave 2 task card updated to reflect chosen option
- [ ] If Option A: Wave 2 scope changed to "refactor query" (no JournalEntry creation)
- [ ] If Option B: Wave 2 scope changed to "add event handler" (no modify RecordRevenue)
- [ ] `dotnet build VanAn.sln` Release — 0 errors (no code change, just decision)
- [ ] guard-check.ps1 PASSED

### Why between Wave 0 and Wave 1
- **Block Wave 2** — quyết định kiến trúc dữ liệu phải chốt trước khi implement bridge
- **Không block Wave 1** (encoding fix độc lập với data source)
- **Read-only ANALYZE** — không code change, chỉ decision + task card update
- **Phản biện kiến trúc cảnh báo: Option C là technical debt nguy hiểm** — không được default vào C vì "effort thấp nhất"

### Issue 1: Production path `CalculateAsync` no-op — `NumericValues` luôn rỗng (Critical)
**Status:** ❌ BROKEN — Báo cáo chỉ in header, không in số liệu
**Priority:** 1 (Critical — toàn bộ stream tồn tại để fix cái này)
**Root cause:** `HKDBookService.GenerateS*BookAsync` dùng `new S*HKDTemplate()` (Domain layer) — override `CalculateAsync(GenericHKDBook book)` thành `await Task.CompletedTask` (HKDTemplates.cs L63-66, lặp 7 lần). Comment nói "Formula engine handles everything" nhưng không ai gọi formula engine.

**Files liên quan:**
- `3_CoreHub/Services/HKDBookService.cs` (L460-654 — 7 method `GenerateS*BookAsync`)
- `1_Shared/Domain/HKDTemplates.cs` (L63-66, L163-166, L262-265, L364-367, L479-482, L583-586, L685-688 — 7 no-op `CalculateAsync`)

### Issue 2: Calculation engine tồn tại nhưng KHÔNG wire DI (Critical)
**Status:** ❌ DISCONNECTED — Code tính thật có nhưng không dùng
**Priority:** 2 (Critical — fix Issue 1 cần đường tính thật)
**Root cause:** `3_CoreHub/Services/Template/` có `HKDBookGenerationService`, `TemplateFactory` (mới), `BaseHKDBookTemplate`, `TemplateCalculationEngine` — đường tính thật qua `ProductionFormulaEngine` → `ScopedDataProvider` → `SmartPreAggregationService`. Nhưng grep `AddScoped<HKDBookGenerationService>` / `AddScoped<TemplateFactory>` (mới) / `AddScoped<ScopedDataProvider>` / `AddScoped<SmartPreAggregationService>` / `AddScoped<ProductionFormulaEngine>` → **0 matches**.

**Files liên quan:**
- `3_CoreHub/Services/Template/HKDBookGenerationService.cs` (chưa đăng ký)
- `3_CoreHub/Services/Template/TemplateFactory.cs` (chưa đăng ký — bản mới)
- `3_CoreHub/Services/Template/BaseHKDBookTemplate.cs`
- `3_CoreHub/Services/Template/TemplateCalculationEngine.cs`
- `3_CoreHub/Services/Formula/ProductionFormulaEngine.cs` (chưa đăng ký)
- `3_CoreHub/Services/Data/ScopedDataProvider.cs` (chưa đăng ký)
- `3_CoreHub/Services/PreAggregation/SmartPreAggregationService.cs` (chưa đăng ký)
- `3_CoreHub/Program.cs` (chỗ đăng ký DI)

### Issue 3: Data flow gap — `JournalEntries` table rỗng (Critical)
**Status:** ❌ BROKEN — Calc engine query bảng rỗng → SUM_ACCOUNT = 0
**Priority:** 3 (Critical — ngay cả khi wire DI, số liệu vẫn = 0)
**Root cause:** `RecordRevenueAsync`/`RecordExpenseAsync` (HKDBookService L43-101) ghi `AccountingEntry` vào bảng `AccountingEntries`. `SmartPreAggregationService.GetAccountSumAsync` (L155-185) query `_context.JournalEntries...Lines.Where(AccountNumber.StartsWith(pattern))` — **bảng `JournalEntries` rỗng** vì không ai persist `JournalEntry` từ `AccountingEntry`. `ConvertToJournalEntries` (L718-751) tạo in-memory, không persist.

**Files liên quan:**
- `3_CoreHub/Services/HKDBookService.cs` (L43-101 `RecordRevenue/Expense`, L718-751 `ConvertToJournalEntries`)
- `3_CoreHub/Services/PreAggregation/SmartPreAggregationService.cs` (L155-185 `GetAccountSumAsync`)
- `3_CoreHub/Repositories/HKDBookRepository.cs` (L135-154 `AddToBookAsync` — tồn tại nhưng không ai gọi)
- `3_CoreHub/Services/Events/SimpleAccountingEventHandler.cs` (L98-105 — gọi `RecordRevenueAsync`, không gọi `AddToBookAsync`)

### Issue 4: Test che lấp bug — pass nhưng không kiểm số liệu (Critical)
**Status:** ❌ FAKE — Test pass trắng, bug đi qua CI
**Priority:** 4 (Critical — cần test thật để verify fix)
**Root cause:** `HKDBookServiceTests` (Services/HKDBookServiceTests.cs L41-93) chỉ assert `BookTypeCode`, `TenantId`, `Period`, `Entries.Count`. Không một test nào assert `result.NumericValues["TotalRevenue"]` hay bất kỳ giá trị số.

**Files liên quan:**
- `6_Tests/VanAn.Core.Tests/Services/HKDBookServiceTests.cs` (L41-180 — 3 test Generate, 0 numeric assert)
- `6_Tests/VanAn.Core.Tests/Accounting/HKDBookServiceTests.cs` (L30-313 — 0 test Generate, chỉ test RecordRevenue/Expense/GetTotals)

### Issue 5: Công thức thuế sai hoàn toàn vs TT 152 (High)
**Status:** ❌ NON-COMPLIANT — Cứng 5% GTGT + 10% TNCN, không phân ngành nghề
**Priority:** 5 (High — sai về mặt pháp lý + logic)
**Root cause:** `S2aHKDTemplate` (HKDTemplates.cs L119-140):
- `VatAmount = TotalRevenue * 0.05` — cứng 5%. TT 152 + Luật 2025 có 4 tỷ lệ GTGT theo ngành nghề (1%; 3%; 5%; 2% — per ND 117/2025). KHÔNG có "2,5%".
- `PersonalIncomeTax = VatAmount * 0.1` — sai logic: TNCN tính trên doanh thu, không tính trên GTGT; suất TNCN cũng theo ngành nghề (0,5%; 1%; 1,5%; 2%).
- Không có khái niệm "nhóm ngành nghề" — mẫu S2a-HKD thật BẮT BUỘC phân chia theo ngành nghề, mỗi ngành có `Tổng cộng (n) / Thuế GTGT / Thuế TNCN` riêng.

**Files liên quan:**
- `1_Shared/Domain/HKDTemplates.cs` (L101-200 S2a, L206-294 S2b, L300-401 S2c, L407-521 S2d, L523-... S2e)
- `3_CoreHub/Services/Template/TemplateFactory.cs` (L177-249 S2aHKDTemplateImpl — cùng vấn đề)
- `3_CoreHub/Services/Orchestration/HKDRevenueClassificationService.cs` (tồn tại — cần verify có mapping suất thuế theo ngành nghề)
- `3_CoreHub/Services/IHKDTaxClassificationService.cs`

### Issue 6: Output là plain text, không phải mẫu docx/xlsx TT 152 (High)
**Status:** ❌ NON-COMPLIANT — Thiếu header/bảng/chứng từ/footer/chữ ký
**Priority:** 6 (High — user request "xuất ra đúng mẫu")
**Root cause:** `GenerateReportAsync` trong 7 template chỉ trả `string` nhiều dòng. So sánh `S2aHKDTemplate.GenerateReportAsync` (code) với `S2a_HKD.docx` (mẫu thật):

| Yếu tố | Mẫu thật (docx) | Code hiện tại |
|---|---|---|
| Header | "HỘ, CÁ NHÂN KD: … / Địa chỉ: … / MST: … / Mẫu số S2a-HKD (Kèm theo TT 152/2025/TT-BTC…)" | Chỉ `SỔ KẾ TOÁN S2a_HKD - {year}/{month}` (sai tiêu đề — mẫu thật là "SỔ DOANH THU BÁN HÀNG HÓA, DỊCH VỤ") |
| Bảng | Cột Chứng từ (số hiệu + ngày tháng), Diễn giải, Số tiền; nhóm theo ngành nghề 1/2/3… | Không có bảng, không có chứng từ, không nhóm ngành nghề |
| Footer | "Tổng số thuế GTGT phải nộp / Tổng số thuế TNCN phải nộp" + block ký tên | Không có |
| Định dạng | .docx (Word) | `string` nhiều dòng |

**Files liên quan:**
- `1_Shared/Domain/HKDTemplates.cs` (7 method `GenerateReportAsync`)
- `3_CoreHub/Services/Template/TemplateFactory.cs` (7 method `GenerateReportAsync` trong `*TemplateImpl`)
- `docs/plan_MVP/HKD_BookAcc/*.docx` (7 mẫu thật — spec cho Wave 8)

### Issue 7: UTF-8 mojibake trong `Services/Template/TemplateFactory.cs` (Medium)
**Status:** ❌ ENCODING — Header báo cáo sẽ là ký tự rác nếu wire DI
**Priority:** 7 (Medium — block Wave 3 wire DI)
**Root cause:** File `3_CoreHub/Services/Template/TemplateFactory.cs` có nhiều chuỗi tiếng Việt bị hỏng encoding (UTF-8 bị đọc lại Latin-1 rồi lưu): `"Sá» ké toÃ¡n cho há» kinh doanh khÃ´ng chá»u thuÃ© GTGT"`, `"Tá»ng doanh thu"`, `"VNÄ"` (L121-150, lặp cho 7 template impl).

**Files liên quan:**
- `3_CoreHub/Services/Template/TemplateFactory.cs` (L114-249 — S1a/S2a TemplateImpl, mojibake; L251-658 — S2b-S3a, OK)

### Issue 8: Account mapping hallucinated (Medium)
**Status:** ❌ WRONG — Tên tài khoản sai, ảnh hưởng General Ledger + Trial Balance
**Priority:** 8 (Medium — ảnh hưởng display, không block số liệu)
**Root cause:** `HKDBookService._vietnameseAccounts` (L22-41):
- `"211"` → `"Ngắn hạn vay ngân hàng"` — sai (211=TSCĐ hữu hình theo TT 200; vay ngắn hạn là 311)
- `"811"` → `"Lợi nhuận gộp"` — sai (811=Xác định KQKD)
- `"821"` → `"Chi phí tài chính"` — sai (821=Chi phí thuế TNDN)
- `"841"` → `"Lợi nhuận sau thuế"` — không tồn tại trong HTKT VN
- `"521"` (dùng trong S2b template) — sai (521=Giảm trừ doanh thu, không phải "Doanh thu dịch vụ" — dịch vụ là 5118)

**Files liên quan:**
- `3_CoreHub/Services/HKDBookService.cs` (L22-41 `_vietnameseAccounts`)
- `1_Shared/Domain/HKDTemplates.cs` (S2b dùng `"512"` cho "Doanh thu dịch vụ" — sai, 512 không tồn tại trong TT 200; dịch vụ là 5118)

---

## 2. WAVE 1 — Fix UTF-8 Mojibake in `Services/Template/TemplateFactory.cs`

**Branch:** `feature/hkd-fix-wave1-encoding-mojibake`
**Estimated sessions:** 0.5
**Conflict risk:** LOW
**Priority:** 1 (block Wave 3 — wire DI)
**Task Card:** `docs/AI/tasks/wave1_hkd_fix_encoding_mojibake_task_card.md` → **Full task details (W1-T1 to W1-T4, file paths, line numbers, test specs)**

**Summary:** Fix mojibake string literals trong `S1aHKDTemplateImpl` + `S2aHKDTemplateImpl` (constructor + GenerateReportAsync header). Verify S2b-S3a không có mojibake. Mechanical fix — no logic change.

### Entry criteria
- [ ] Wave 0 complete (verify môi trường)
- [ ] Git status clean

### Exit criteria
- [ ] 0 mojibake string còn lại trong `Services/Template/TemplateFactory.cs`
- [ ] `dotnet build VanAn.sln` Release — 0 errors
- [ ] guard-check.ps1 PASSED

### Why first
- Risk thấp nhất — chỉ sửa string literal, không thay đổi logic
- Block Wave 3 (wire DI) — nếu không fix, báo cáo sẽ có ký tự rác
- Độc lập với data flow — có thể làm song song Wave 0/Wave 2

---

## 3. WAVE 2 — Data Source Bridge (Option A: refactor query OR Option B: event-driven — per Wave 0.5 decision)

**Branch:** `feature/hkd-fix-wave2-data-source-bridge-option-a`
**Estimated sessions:** 1-2 (Option A: 0.5-1, Option B: 1-2)
**Conflict risk:** MEDIUM (Option B thay đổi write path via event) / LOW (Option A chỉ refactor query)
**Priority:** 2 (Critical — block Wave 3/4/5/6 — calc engine cần data)
**Task Card:** `docs/AI/tasks/wave2_hkd_fix_data_source_bridge_task_card.md` (RENAMED + REWRITE per W0.5) → **Full task details (W2A-T1 to W2A-T4 for Option A, W2B-T1 to W2B-T7 for Option B, file paths, line numbers, test specs, idempotent guard details)**
**Status:** ✅ COMPLETE — Commit `7269bc1` on `feature/hkd-fix-wave2-data-source-bridge-option-a`. Option A implemented. Awaiting user review → merge to main.

> **[AMENDMENT 4 — v3] Wave 2 scope phụ thuộc Wave 0.5 decision.** Option C (dual write trong RecordRevenue) bị LOẠI. Task card phải rewrite theo Option A hoặc B.

**Summary:**
- **Option A:** Refactor `SmartPreAggregationService.GetAccountSumAsync` query `AccountingEntries` (filter `AccountCode.StartsWith(pattern)` + `EntryType` làm sign). Skip JournalEntry creation entirely.
- **Option B:** Add Domain Event `AccountingEntryRecorded` + NATS/Outbox handler sinh `JournalEntry` double-entry. `RecordRevenueAsync` chỉ persist `AccountingEntry` + phát event (no dual write).

**Execution result (2026-07-03):**
- **Chosen:** Option A (per W0.5-T4)
- **Implementation:** `SmartPreAggregationService.GetAccountSumAsync` (L155-205) now queries `AccountingEntries` directly. `EntryType` → side mapping (Revenue=Credit, Expense=Debit). Null `AccountCode` uses `EntryType` heuristic (Revenue→"5", Expense→"6").
- **Root cause fix:** SQLite cannot apply aggregate `Sum` on `decimal` server-side (`NotSupportedException` from `SqliteQueryableAggregateMethodTranslator`). Materialize `Amount` values via `ToListAsync()` then `Sum()` client-side to preserve decimal precision.
- **Tests:** 3 new unit tests (`SmartPreAggregationServiceWave2Tests.cs`) — all passing (Revenue→Credit=1000, Expense→Debit=500, Null AccountCode→heuristic=2000).
- **Verification:** Build 0 errors / 990 warnings · Core.Tests 776/0 · Architecture.Tests 28/0 · guard-check PASSED.

### Entry criteria
- [ ] Wave 1 merged
- [ ] **Wave 0.5 decision documented** (Option A or B — NOT C)
- [ ] Wave 2 task card rewritten per chosen option

### Exit criteria (high-level — detailed per-option in task card)
- [ ] Data source bridge implemented per chosen option (A or B)
- [ ] Unit tests pass per chosen option
- [ ] `dotnet build VanAn.sln` Release — 0 errors
- [ ] guard-check.ps1 PASSED

### Why second
- Block tất cả wave sau — calc engine cần data source
- **Option A: Single Source of Truth, không cần JournalEntry cho HKD** — tối ưu nếu HKD-only
- **Option B: Event-driven, tách biệt write path, share Formula Engine với Doanh nghiệp** — tối ưu nếu mở rộng
- **Option C LOẠI** — dual write anti-pattern per phản biện kiến trúc

---

## 4. WAVE 3 — Wire Calculation Engine into DI

**Branch:** `feature/hkd-fix-wave3-wire-calc-engine-di`
**Estimated sessions:** 1
**Conflict risk:** LOW (chỉ thêm DI registration)
**Priority:** 3 (Critical — block Wave 4)
**Task Card:** `docs/AI/tasks/wave3_hkd_fix_wire_calc_engine_di_task_card.md` → **Full task details (W3-T1 to W3-T9, DI registration snippets, ITemplateFactory conflict resolution)**

**Summary:** Register 5 calc engine services vào DI container (`ProductionFormulaEngine`, `ScopedDataProvider`, `SmartPreAggregationService`, `TemplateFactory` mới, `HKDBookGenerationService`). Verify `IBookResultCache` + `IMemoryCache` đã đăng ký. Resolve `ITemplateFactory` conflict (W0-T8 pre-resolved).

### Entry criteria
- [ ] Wave 2 merged (data source bridge implemented)
- [ ] Wave 1 merged (TemplateFactory mới không còn mojibake)

### Exit criteria
- [ ] 5 service mới đăng ký DI
- [ ] `IBookResultCache` + `IMemoryCache` confirmed đăng ký
- [ ] Conflict `ITemplateFactory` resolved (không break `OrderService`)
- [ ] `dotnet build VanAn.sln` Release — 0 errors
- [ ] guard-check.ps1 PASSED

### Why third
- Sau Wave 2 (data) — có data để calc engine test
- Sau Wave 1 (encoding) — TemplateFactory mới không còn ký tự rác
- Risk thấp — chỉ thêm DI, không thay đổi logic

---

## 5. WAVE 4 — Route `HKDBookService.GenerateS*BookAsync` through `IHKDBookGenerationService`

**Branch:** `feature/hkd-fix-wave4-route-through-generation-service`
**Estimated sessions:** 1
**Conflict risk:** MEDIUM (thay đổi 7 method production)
**Priority:** 4 (Critical — fix Issue 1 — NumericValues sẽ có số liệu)
**Task Card:** `docs/AI/tasks/wave4_hkd_fix_route_through_generation_service_task_card.md` → **Full task details (W4-T1 to W4-T11, 7 method rewrites with line numbers, smoke test tripwire spec)**

**Summary:** Inject `IHKDBookGenerationService` vào `HKDBookService`. Rewrite 7 method `GenerateS*BookAsync` (S1a, S2a-S2e, S3a) — thay `new S*HKDTemplate()` + `ConvertToJournalEntries` bằng `_hkdBookGenerationService.GenerateBookAsync(tenantId, period, templateCode)`. Mark `ConvertToJournalEntries` obsolete. **[AMENDMENT 3] Add smoke test W4-T11** — tripwire assert `NumericValues` NOT empty cho S1a (catch routing bug before Wave 5/6).

### Entry criteria
- [ ] Wave 3 merged (IHKDBookGenerationService đã đăng ký DI)
- [ ] Wave 2 merged (data source bridge implemented)

### Exit criteria
- [ ] 7 method `GenerateS*BookAsync` gọi `_hkdBookGenerationService.GenerateBookAsync`
- [ ] `ConvertToJournalEntries` không còn dùng (hoặc xóa)
- [ ] **Smoke test W4-T11 pass** — `NumericValues` không rỗng cho S1a (tripwire)
- [ ] `dotnet build VanAn.sln` Release — 0 errors
- [ ] guard-check.ps1 PASSED

### Why fourth
- Sau Wave 3 (DI) — `IHKDBookGenerationService` đã có sẵn để inject
- Đây là wave fix Issue 1 cốt lõi — `NumericValues` sẽ có số liệu
- Risk medium — thay đổi 7 method production, cần đảm bảo backward compat
- **Smoke test (W4-T11) là tripwire** — phát hiện routing bug ngay, không đợi đến Wave 6

---

## 6. WAVE 5a — Fix Account Mapping + PIT-on-Revenue (No Domain mod, no industry modeling)

**Branch:** `feature/hkd-fix-wave5a-account-mapping-pit-fix`
**Estimated sessions:** 1
**Conflict risk:** MEDIUM (thay đổi service logic + 1 Domain account-number fix)
**Priority:** 5a (High — compliance pháp lý, nhưng không cần modeling ngành nghề)
**Task Card:** `docs/AI/tasks/wave5a_hkd_fix_account_mapping_pit_task_card.md` ✅ CREATED → **Full task details (W5a-T1 to W5a-T7, account mapping table, PIT formula fix, S2b account fix, 2 unit test specs, micro-phase breakdown)**

**Summary:** Fix `_vietnameseAccounts` dictionary (5 entry sai + 3 entry thiếu). Fix `PersonalIncomeTax` formula tính trên `TotalRevenue` (không phải `VatAmount`). Fix account `"521"`/`"512"` → `"5118"` cho doanh thu dịch vụ (Service + Domain layer). Default rate tạm thời (5% GTGT, 0.5% TNCN) — industry-specific rates sang Wave 5b.

### Entry criteria
- [ ] Wave 4 merged (GenerateS*BookAsync dùng IHKDBookGenerationService)
- [ ] **Tech Lead approval** cho W5a-T4 (Domain modification — account number `"512"` → `"5118"`, không thêm field)
- [ ] W0-T10 result known (Tenant.IndustrySector exists or not — determines if 5b is in-scope)

### Exit criteria
- [ ] `_vietnameseAccounts` sửa 5 entry sai + thêm entry mới
- [ ] `PersonalIncomeTax` tính trên `TotalRevenue`, không phải `VatAmount`
- [ ] Account `"521"`/`"512"` → `"5118"` (doanh thu dịch vụ)
- [ ] 2 unit test pass
- [ ] `dotnet build VanAn.sln` Release — 0 errors
- [ ] guard-check.ps1 PASSED

### Why fifth-a
- Sau Wave 4 (routing) — đã có đường tính, giờ sửa 2 bug logic rõ ràng (PIT base + account mapping)
- Risk medium — chỉ 1 Domain account-number fix (W5a-T4), không thêm field, không modeling
- Có thể merge độc lập — không phụ thuộc W5b

---

## 6.5. WAVE 5b — Industry-Sector Tax Rates per TT 152 (Conditional — needs W0-T10 + Tech Lead approval)

**Branch:** `feature/hkd-fix-wave5b-industry-sector-tax-rates`
**Estimated sessions:** 1-2
**Conflict risk:** HIGH (modeling ngành nghề + có thể cần Domain mod nếu Tenant.IndustrySector missing)
**Priority:** 5b (High — full TT 152 compliance, nhưng có thể descope nếu W0-T10 fail)
**Task Card:** `docs/AI/tasks/wave5b_hkd_fix_industry_sector_tax_rates_task_card.md` ✅ CREATED → **Full task details (W5b-T0 to W5b-T7, 4 nhóm ngành nghề lookup table, industry grouping spec, 2 unit test specs, conditional execution paths)**

**Summary:** Thay default rate cứng (từ Wave 5a) bằng suất thuế theo ngành nghề per Luật 2025 + ND 117/2025 (4 nhóm: Phân phối 1%/0.5%, Sản xuất 3%/1.5%, Dịch vụ 5%/2%, Khác 2%/1%). Thêm nhóm ngành nghề vào S2a template (mỗi ngành có Tổng cộng + GTGT + TNCN riêng). **CONDITIONAL** — may descope if W0-T10 finds `Tenant.IndustrySector` missing + Tech Lead doesn't approve Domain mod.

### Conditional execution
- **IF W0-T10 confirms `Tenant.IndustrySector` exists** → proceed normally
- **IF W0-T10 finds field missing AND Tech Lead approves Domain mod** → add `IndustrySector` to `Tenant` first (W5b-T0), then proceed
- **IF W0-T10 finds field missing AND Tech Lead does NOT approve** → **DESCOPE Wave 5b**, use default rate from W5a, log technical debt, proceed to Wave 5c

### Entry criteria
- [ ] Wave 5a merged (PIT fix + account mapping done)
- [ ] W0-T10 result: `Tenant.IndustrySector` exists OR Tech Lead approval for W5b-T0
- [ ] **If descope triggered** → skip this wave, document in `docs/AI/technical_debt.md`, proceed to Wave 5c

### Exit criteria (only if executed)
- [ ] `S2aHKDTemplateImpl` dùng `HKDRevenueClassificationService` cho suất thuế (không cứng)
- [ ] S2a template có nhóm ngành nghề (mỗi ngành có tổng cộng + GTGT + TNCN riêng)
- [ ] 2 unit test pass
- [ ] `dotnet build VanAn.sln` Release — 0 errors
- [ ] guard-check.ps1 PASSED

### Why fifth-b (separate from 5a)
- Risk cao nhất — cần modeling ngành nghề + có thể cần Domain mod (Tenant.IndustrySector)
- Tách ra để 5a có thể merge độc lập — 5b không block Wave 5c/6 nếu descope
- Compliance pháp lý đầy đủ — sai = báo cáo sai thuế, nhưng 5a đã fix 2 bug nghiêm trọng trước

---

## 6.7. WAVE 5c — 2026 Regulatory Compliance Fix (CRITICAL — pháp lý)

**Branch:** `feature/hkd-fix-wave5c-2026-regulatory-compliance`
**Estimated sessions:** 1-2
**Conflict risk:** HIGH (thay đổi threshold + formula tính thuế — ảnh hưởng toàn bộ HKD tax calculation)
**Priority:** 5c (CRITICAL — sai = báo cáo sai thuế + phạt hành chính)
**Task Card:** `docs/AI/tasks/wave5c_hkd_fix_2026_regulatory_compliance_task_card.md` ✅ CREATED → **Full task details (W5c-T1 to W5c-T13, codebase evidence, 2026 regulatory changes table, 4 revenue groups, TNCN formulas per group, 5 unit test specs, legal source references, micro-phase breakdown)**

> **[AMENDMENT 5c — v3] Phát hiện qua phản biện pháp lý:** `HKDRevenueClassificationService` hiện tại (L12-14) có threshold SAI hoàn toàn vs luật 2026. v2 plan hoàn toàn missing 2026 regulatory changes. Đây là bug pháp lý nghiêm trọng — không chỉ là industry-sector rates (Wave 5b).

**Summary:** Fix `HKDRevenueClassificationService` thresholds (500M→1B, 4 revenue groups mới: ≤1B / 1B-3B / 3B-50B / >50B). Add TNCN formulas per group (Nhóm 2: `(Doanh thu - 1B) × industryRate`, Nhóm 3: `(Doanh thu - chi phí) × 17%`, Nhóm 4: `(Doanh thu - chi phí) × 20%`). Add GTGT exemption Nhóm 1 (≤1B). Document thuế khoán + lệ phí môn bài abolished from 01/01/2026. 5 unit tests. **Legal review recommended.**

### Entry criteria
- [ ] Wave 5a merged (account mapping + PIT-on-revenue fix)
- [ ] Wave 5b merged OR descoped (W5c-T3 needs industryRate from W5b if executed; if descoped, use default rate + log technical debt)
- [ ] **Legal review recommended** — confirm 2026 regulatory changes với bộ phận pháp lý/thuế

### Exit criteria
- [ ] `HKDRevenueClassificationService` thresholds: 1B / 3B / 50B / >50B (4 groups mới)
- [ ] TNCN Nhóm 2: `(Doanh thu - 1B) × industryRate` (not `Doanh thu × rate`)
- [ ] TNCN Nhóm 3: `(Doanh thu - chi phí) × 17%`
- [ ] TNCN Nhóm 4: `(Doanh thu - chi phí) × 20%`
- [ ] GTGT Nhóm 1: 0 (exemption)
- [ ] Thuế khoán + lệ phí môn bài abolished documented
- [ ] 5 unit test pass
- [ ] `dotnet build VanAn.sln` Release — 0 errors
- [ ] guard-check.ps1 PASSED

### Why 5c (separate from 5b)
- **CRITICAL pháp lý** — sai threshold = sai toàn bộ tax calculation = phạt hành chính
- **Khác scope 5b** — 5b là industry-sector rates (suất thuế theo ngành), 5c là revenue-group thresholds + TNCN formula (theo nhóm doanh thu)
- **Có thể chạy độc lập** — 5c không phụ thuộc 5b (dùng default rate nếu 5b descoped)
- **Bối cảnh 2026:** Cơ quan thuế siết e-invoice từ máy tính tiền — phân loại sai nhóm = tính sai nghĩa vụ thuế = rủi ro phạt nặng cho khách hàng

---

## 7. WAVE 6 — Retrofit Tests with Numeric Assertions

**Branch:** `feature/hkd-fix-wave6-retrofit-numeric-tests`
**Estimated sessions:** 1-2
**Conflict risk:** LOW (chỉ sửa test)
**Priority:** 6 (Critical — verify fix Issue 1/4)
**Task Card:** `docs/AI/tasks/wave6_hkd_fix_retrofit_numeric_tests_task_card.md` → **Full task details (W6-T1 to W6-T9, 3 test updates + 5 new tests with seed data + assertion specs, regression test for Issue 1)**

**Summary:** Update 3 existing tests (S1a, S2a, S2b) + add 5 new tests (S2c, S2d, S2e, S3a, regression) — all assert `NumericValues` cụ thể (TotalRevenue, VatAmount, PIT, NetProfit...) thay vì chỉ metadata. 1 regression test verify `NumericValues` không rỗng (Issue 1 fix).

### Entry criteria
- [ ] Wave 5a merged (account mapping + PIT fix)
- [ ] Wave 5b merged OR descoped
- [ ] **Wave 5c merged** (2026 regulatory compliance)
- [ ] Wave 4 merged (NumericValues có số liệu + smoke test pass)
- [ ] Wave 2 merged (data source bridge)

### Exit criteria
- [ ] 7 test `GenerateS*BookAsync` assert `NumericValues` cụ thể
- [ ] 1 regression test verify `NumericValues` không rỗng
- [ ] Tất cả test pass (`dotnet test`)
- [ ] `dotnet build VanAn.sln` Release — 0 errors
- [ ] guard-check.ps1 PASSED

### Why sixth
- Sau Wave 4+5 — logic đã đúng, giờ verify bằng test
- Risk thấp — chỉ sửa test, không sửa production
- Retrofit TDD per governance (EXISTING code: retrofit tests before completion)

---

## 8. WAVE 7 — API Endpoint + DI Smoke Test

**Branch:** `feature/hkd-fix-wave7-api-endpoint-di-smoke`
**Estimated sessions:** 1
**Conflict risk:** LOW (thêm endpoint mới, không break cũ)
**Priority:** 7 (High — expose cho UI Wave 8)
**Task Card:** `docs/AI/tasks/wave7_hkd_fix_api_endpoint_di_smoke_task_card.md` → **Full task details (W7-T1 to W7-T7, DTO spec, endpoint routes, DI smoke test, multi-tenancy isolation test)**

**Summary:** Create `HKDBookDto` + 2 endpoints (`GET /api/hkd-books/{templateCode}?year=&month=`, `GET /api/hkd-books`). Add DI smoke test (4 service resolvable). Add integration test (endpoint return NumericValues). **[Concern 7] Add multi-tenancy isolation test** — tenant A cannot read tenant B data.

### Entry criteria
- [ ] Wave 6 merged (test pass)
- [ ] Wave 4 merged (GenerateS*BookAsync có số liệu)

### Exit criteria
- [ ] Endpoint `GET /api/hkd-books/{templateCode}` return `HKDBookDto` với NumericValues
- [ ] Endpoint `GET /api/hkd-books` list templates theo HKDGroup
- [ ] DI smoke test pass (4 service resolvable)
- [ ] Integration test pass (endpoint return NumericValues)
- [ ] **Multi-tenancy isolation test pass** (tenant A cannot read tenant B data — Concern 7 resolved)
- [ ] `dotnet build VanAn.sln` Release — 0 errors
- [ ] guard-check.ps1 PASSED

### Why seventh
- Sau Wave 6 (test pass) — logic ổn, expose API
- UI Wave 8 cần endpoint để gọi
- Risk thấp — endpoint mới, không break cũ

---

## 9. WAVE 8 — UI Page + DOCX Export + Regression Prevention

**Branch:** `feature/hkd-fix-wave8-ui-docx-export-regression`
**Estimated sessions:** 2-3
**Conflict risk:** MEDIUM (thêm UI + dependency mới)
**Priority:** 8 (Final — user request "xuất ra đúng mẫu")
**Task Card:** `docs/AI/tasks/wave8_hkd_fix_ui_docx_export_regression_task_card.md` → **Full task details (W8-T1 to W8-T10, Razor page specs, TT 152 layout, export library options, E2E test, architecture test, encoding lint)**

**Summary:** Add 2 Razor pages (`/accounting/hkd-books` list + `/accounting/hkd-books/{templateCode}` render TT 152 layout). Add DOCX + XLSX export buttons. Add E2E test + architecture test (no no-op `CalculateAsync` regression) + encoding lint rule. Update docs + project_state.

### Entry criteria
- [ ] Wave 7 merged (endpoint có sẵn)
- [ ] UI Platform components available (VanAnCard, VanATable — verify)
- [ ] Docx/Xlsx library available hoặc approval thêm dependency (W0-T9)

### Exit criteria
- [ ] Page `/accounting/hkd-books` list templates theo HKDGroup
- [ ] Page `/accounting/hkd-books/{templateCode}` render book với TT 152 layout (header + bảng + footer + chữ ký)
- [ ] Export DOCX generate file đúng layout TT 152
- [ ] Export XLSX generate file đúng layout
- [ ] E2E test pass
- [ ] Architecture test pass (no no-op CalculateAsync)
- [ ] Encoding lint pass (0 mojibake)
- [ ] `dotnet build VanAn.sln` Release — 0 errors
- [ ] guard-check.ps1 PASSED
- [ ] `project_state.md` updated

### Why last
- Phụ thuộc tất cả wave trước (data + DI + routing + formulas + tests + API)
- UI + export là output cuối cùng user thấy
- Regression prevention đảm bảo bug không tái xuất

---

## 10. CROSS-WAVE CONCERNS (v2 — all 8 review concerns resolved)

### Domain Protection
- **KHÔNG sửa `1_Shared/Domain/*.cs`** trừ W5a-T4 (account number, cần Tech Lead approval) và W5b-T0 (conditional, cần Tech Lead approval)
- **`AccountingEntry` immutable** trong mọi wave — không thay đổi
- **`HKDTemplates.cs`** (Domain) có no-op `CalculateAsync` — KHÔNG sửa, thay vào đó dùng `Services/Template/*TemplateImpl` (đã có calc thật)
- Nếu W5a-T4/W5b-T0 cần sửa Domain → STOP, báo Tech Lead

### Data Flow Integrity (Concern 1 — RESOLVED)
- Wave 2 bridge AccountingEntry → JournalEntry là **critical path** — không skip
- **W0-T11 double-write audit phải complete trước Wave 2** — build write-path map, select decision tree path (A/B/C/D)
- **Decision tree trong Wave 2 section** — không guess, verify trước
- Multi-tenancy: mọi query `JournalEntries` phải filter `TenantId` (đã có trong `SmartPreAggregationService` L164)

### DI Conflict (Concern 2 — RESOLVED, promoted to Wave 0)
- `ITemplateFactory` hiện đăng ký bản cũ `Services/TemplateFactory.cs` (dùng cho `OrderService`)
- Bản mới `Services/Template/TemplateFactory.cs` là class khác (không implement `ITemplateFactory`)
- **W0-T8 resolve architecture decision BEFORE Wave 3** — rename new class to `HKDTemplateFactory` OR keep both with distinct interfaces. Document in Wave 3 task card.

### Testing Strategy (Concern 3 — RESOLVED, smoke test added)
- **Unit test:** Wave 2 (double-entry), Wave 4 (smoke tripwire), Wave 5a (PIT + account), Wave 5b (industry rates), Wave 6 (numeric assertions)
- **Integration test:** Wave 7 (endpoint + DI smoke + multi-tenancy isolation)
- **Architecture test:** Wave 8 (no no-op CalculateAsync)
- **E2E test:** Wave 8 (UI page) — chỉ parse check trong Wave 1-7, runtime sau Wave 8
- **Playwright DISABLED** Wave 1-7 per governance (IMPLEMENT mode)
- **Wave 4 smoke test (W4-T11)** — tripwire phát hiện routing bug ngay, không đợi Wave 6

### TT 152 Compliance (Concern 4 — RESOLVED, Wave 5 split + 5c 2026 regulatory)
- 7 mẫu báo cáo: S1a (Group 1), S2a-S2e (Group 2), S3a (Group 3)
- Layout từng mẫu đã extract trong Wave 0 (docx → text)
- Wave 8 phải match layout: header (HỘ/CÁ NHÂN KD + MST + địa chỉ + "Mẫu số X-HKD (Kèm theo TT 152/2025/TT-BTC)"), bảng (chứng từ + diễn giải + số tiền), footer (tổng thuế + chữ ký NGƯỜI ĐẠI DIỆN HKD)
- **Wave 5a fix PIT-on-revenue + account mapping** (logic bugs, không cần modeling)
- **Wave 5b industry-sector rates** (4 nhóm ngành nghề per Luật 2025 + ND 117/2025, conditional — may descope if Tenant.IndustrySector missing)
- **Wave 5c 2026 regulatory compliance** (threshold 500M→1B, 4 revenue groups mới, TNCN formula Nhóm 2/3/4, thuế khoán abolished) — CRITICAL pháp lý

### HKD Accounting Regime Disclaimer (Amendment 5a — v3)
> **CRITICAL:** HKD theo TT 88/2021/TT-BTC + TT 152/2025/TT-BTC ghi sổ **phương pháp đơn (single-entry)** — Sổ theo dõi chi phí, doanh thu, dòng tiền. **KHÔNG có nghĩa vụ hạch toán định khoản kép (Nợ/Có) theo TT 200/2014/TT-BTC hay TT 133/2016/TT-BTC.**
>
> Account mapping (111/511/611/632/641/642/811/821...) trong `HKDBookService._vietnameseAccounts` là **Internal Synthetic Mapping** — ánh xạ giả lập nội bộ của CoreHub để tận dụng chung Formula Engine với khối Doanh nghiệp. **KHÔNG phải nghĩa vụ hạch toán kép theo TT 200/133.**
>
> **UI/UX + báo cáo pháp lý cho HKD MUST tuân thủ chế độ HKD:**
> - KHÔNG render Bảng cân đối kế toán (Balance Sheet) chuẩn TT 200
> - KHÔNG render Báo cáo kết quả hoạt động kinh doanh chuẩn TT 200
> - Render sổ theo dõi doanh thu, chi phí, dòng tiền per TT 88/2021 + TT 152/2025
> - Document này phải ghi rõ trong code comment + UI label + export template
>
> **Risk nếu không clarify:** Dev sau này lầm tưởng HKD phải lên Bảng cân đối / Báo cáo KQKD chuẩn TT 200 → thiết kế sai UI/UX + báo cáo pháp lý → khách hàng nộp sai báo cáo cho cơ quan thuế.

### 2026 Tax Rate Lookup Table (Amendment 5b — v3, per Luật Thuế GTGT/TNCN sửa đổi 2025 + ND 117/2025)
> **CRITICAL:** Suất thuế HKD 2026 theo 4 nhóm ngành nghề (NGUỒN CHÍNH THỨC: meinvoice.vn/MISA 14/04/2026, trích Luật 2025 + ND 117/2025 + Nghị quyết 198/2025/QH15):
>
> | Nhóm ngành nghề | GTGT | TNCN (Nhóm 2, tính trên doanh thu) |
> |---|---|---|
> | Phân phối, cung cấp hàng hóa | **1%** | **0,5%** |
> | Sản xuất, vận tải, dịch vụ có gắn với hàng hóa, xây dựng có bao thầu NVL | **3%** | **1,5%** |
> | Dịch vụ, xây dựng không bao thầu nguyên vật liệu | **5%** | **2%** |
> | Hoạt động kinh doanh khác | **2%** | **1%** |
>
> **KHÔNG có "2,5%"** — v2 plan có "1%; 1,5%; 2%; 2,5%; 3%; 5%" là FABRICATED. Sửa thành đúng 4 nhóm trên.
>
> **TNCN formula theo nhóm doanh thu (2026):**
> - **Nhóm 1 (≤1B):** Không chịu thuế GTGT + TNCN
> - **Nhóm 2 (1B-3B):** `(Doanh thu - 1_000_000_000) × industryRate` (KHÔNG phải `Doanh thu × rate`) — hoặc `(Doanh thu - chi phí) × 15%` nếu xác định được chi phí
> - **Nhóm 3 (3B-50B):** `(Doanh thu - chi phí) × 17%` (bắt buộc theo lợi nhuận)
> - **Nhóm 4 (>50B):** `(Doanh thu - chi phí) × 20%` (bắt buộc theo lợi nhuận)
>
> **Threshold chịu thuế: 500M → 1 TỶ VND từ 01/01/2026** (Luật Thuế GTGT/TNCN sửa đổi 2025)
> **Thuế khoán BÃI BỎ từ 01/01/2026** — tất cả kê khai + tự nộp (Nghị quyết 198/2025/QH15)
> **Lệ phí môn bài BÃI BỎ từ 01/01/2026** (Điều 10, Nghị quyết 198/2025/QH15)

### Tenant Industry Sector (Concern 4 prereq — RESOLVED, promoted to Wave 0)
- **W0-T10 verify `Tenant.IndustrySector` field exists** before Wave 5b
- If missing → Wave 5b conditional: Tech Lead approval for W5b-T0 OR descope + technical debt

### Export Library (Concern 6 — RESOLVED, promoted to Wave 0)
- **W0-T9 verify docx/xlsx library availability** before Wave 8
- If none present → flag for dependency-add approval (governance: version ≥7 days old)
- If user does not approve → Wave 8 export descoped (UI render-only)

### Multi-Tenancy Isolation (Concern 7 — RESOLVED, test added)
- **W7-T6 multi-tenancy isolation test** — tenant A cannot read tenant B data via new endpoint
- Cross-tenant leak = governance Hard Stop violation

### Branch Strategy (Concern 8 — RESOLVED, per-wave merge)
- **Per-wave merge to main** — `main` always green, no long-lived 14-session branch
- Each wave branches from latest `main`, merges back after pass
- Wave 5b optional — does not block Wave 6-8 if descoped

### UI Platform Compliance
- Wave 8 UI page MUST dùng UI Platform components (VanAnCard, VanATable, VanAForm, VanAnButton)
- KHÔNG tạo custom HTML/CSS — governance Hard Stop
- Mobile-first design với breakpoints (Mobile ≤640px, Tablet 641-1024px, Desktop ≥1025px)

---

## 11. APPROVAL CHECKLIST (v3)

- [x] Master plan reviewed (v3 — 12 waves, 8 root-cause issues + 2 architecture/legal findings, 5 amendments + 5 concerns resolved)
- [ ] 12 task cards reviewed (Wave 0, 0.5, 1-8, with Wave 5 split into 5a + 5b + 5c)
- [ ] HKD Book report audit reviewed (8 issues — see Section 1)
- [ ] **Phản biện kiến trúc (Dual Write) reviewed** — Option C loại, Wave 0.5 chốt A or B
- [ ] **Phản biện pháp lý (HKD regime + 2026 regulatory) reviewed** — Wave 5c added, suất thuế sửa, disclaimer thêm
- [ ] Wave 0 pre-flight verification complete (11 tasks — including 4 promoted architecture decisions)
- [ ] **Wave 0.5 architecture decision complete** (Option A or B documented — NOT C)
- [ ] `dotnet build VanAn.sln` Release baseline pass
- [ ] Data flow gap confirmed (JournalEntries table rỗng OR AccountingEntries đủ field cho Option A)
- [ ] DI registrations confirmed (5 service chưa đăng ký)
- [ ] **W0-T8: `ITemplateFactory` conflict resolution documented** (promoted from W3-T8)
- [ ] **W0-T9: docx/xlsx library availability confirmed OR dependency-add approved** (promoted from W8-T3/T4)
- [ ] **W0-T10: `Tenant.IndustrySector` field existence confirmed** (determines Wave 5b scope)
- [ ] **W0-T11: Double-write audit complete** (write-path map documented)
- [ ] **W0.5-T4: HKD Data Source decision documented** (Option A or B — NOT C)
- [ ] Tech Lead approval cho W5a-T4 (Domain modification — account number `"512"` → `"5118"`, no new field) — **chỉ cần trước Wave 5a**
- [ ] Tech Lead approval cho W5b-T0 (conditional — add `IndustrySector` to `Tenant`) — **chỉ cần trước Wave 5b IF W0-T10 finds field missing**
- [ ] **Legal review recommended cho Wave 5c** — confirm 2026 regulatory changes với bộ phận pháp lý/thuế
- [ ] Branch strategy confirmed (per-wave merge to main, always-green)
- [ ] Sẵn sàng implement Wave 0 + Wave 0.5 + Wave 1 (có thể cùng session)

---

## 12. EFFORT SUMMARY (v3)

| Wave | Description | Sessions | Risk | Status |
|---|---|---|---|---|
| Wave 0 | Pre-flight verification (11 tasks — baseline + 4 architecture decisions promoted) | 0.5-1 | None | ✅ COMPLETE (`88a7fb4`) |
| Wave 0.5 | **[NEW] Architecture Decision: HKD Data Source (A vs B, loại C dual-write)** | 0.5-1 | None (decision only) | ✅ COMPLETE (`88a7fb4`) |
| Wave 1 | Fix UTF-8 mojibake (mechanical) | 0.5 | Low | ✅ COMPLETE (`83cca48`) |
| Wave 2 | Data source bridge (Option A: refactor query OR Option B: event-driven — per W0.5) | 0.5-2 | Medium/Low | ✅ COMPLETE (`7269bc1`) — awaiting merge |
| Wave 3 | Wire calc engine into DI (conflict pre-resolved in W0-T8) | 1 | Low | ⏳ PENDING (next) |
| Wave 4 | Route HKDBookService through IHKDBookGenerationService + smoke test tripwire | 1 | Medium | ⏳ PENDING |
| Wave 5a | Fix account mapping + PIT-on-revenue (no industry modeling, 1 Domain account-number fix) | 1 | Medium | ⏳ PENDING |
| Wave 5b | Industry-sector tax rates per Luật 2025 + ND 117/2025 (CONDITIONAL — may descope) | 1-2 | High | ⏳ CONDITIONAL |
| Wave 5c | **[NEW] 2026 Regulatory Compliance Fix (threshold 500M→1B, 4 revenue groups, TNCN formulas, thuế khoán abolished)** | 1-2 | High (pháp lý) | ⏳ PENDING (CRITICAL) |
| Wave 6 | Retrofit tests with numeric assertions | 1-2 | Low | ⏳ PENDING |
| Wave 7 | API endpoint + DI smoke + multi-tenancy isolation test | 1 | Low | ⏳ PENDING |
| Wave 8 | UI page + DOCX export + regression prevention | 2-3 | Medium | ⏳ PENDING |
| **Total** | | **11-18 sessions** (5b optional, 5c mandatory) | | **4/12 complete** |

**Critical path:** Wave 0 → Wave 0.5 → Wave 1 → Wave 2 → Wave 3 → Wave 4 → Wave 5a → **Wave 5c** → Wave 6 → Wave 7 → Wave 8
**Optional path:** Wave 5b (between 5a and 5c) — executes only if W0-T10 confirms `Tenant.IndustrySector` exists OR Tech Lead approves Domain mod
**Parallel path:** Wave 0 + Wave 0.5 + Wave 1 có thể cùng session (cả 3 non-code/low-risk, độc lập)
**Descope path:** If Wave 5b descoped → use default rate, log technical debt, Wave 5c/6-8 proceed (5c uses default rate if 5b descoped)
**Mandatory path:** Wave 5c KHÔNG skip — CRITICAL pháp lý (sai threshold = sai thuế = phạt hành chính)

**Fix target (v3):**
- Before: 7 HKD book templates, `NumericValues` luôn rỗng, output plain text, không endpoint/UI, test pass trắng, threshold sai (500M), TNCN formula sai, dual write risk
- After (full): 7 HKD book templates, `NumericValues` có số liệu thực, output docx/xlsx theo TT 152 layout, endpoint + UI page, test assert numeric values, regression prevention, multi-tenancy enforced, **2026 regulatory compliant** (threshold 1B, 4 revenue groups, TNCN formulas đúng), **no dual write** (Option A or B per W0.5)
- After (if 5b descoped): Same as full but tax rates use single default (not industry-specific) — technical debt logged, **5c still executes** (threshold + TNCN formula fix mandatory)
- After (if export descoped): Same as full but UI render-only, no DOCX/XLSX export — technical debt logged
- Compliance: TT 152/2025/TT-BTC + TT 88/2021/TT-BTC (HKD single-entry regime) + Luật Thuế GTGT/TNCN sửa đổi 2025 + ND 117/2025 + Nghị quyết 198/2025/QH15 (2026 regulatory)

---

## 13. ROLLBACK PLAN (v3)

Nếu wave fail không fix được:
- **Wave 1-4:** Revert branch — không ảnh hưởng production (code cũ vẫn chạy, chỉ không có số liệu)
- **Wave 5a:** Revert branch — giữ `_vietnameseAccounts` sai + PIT tính trên VAT (sai nhưng chạy được)
- **Wave 5b:** Revert branch OR descope — giữ default rate (sai về ngành nghề nhưng chạy được, log technical debt)
- **Wave 5c:** **KHÔNG recommend revert** — CRITICAL pháp lý. Nếu revert → threshold 500M sai + TNCN formula sai = báo cáo sai thuế = phạt hành chính. Nếu fail, fix-forward thay vì revert.
- **Wave 6:** Revert test — giữ test cũ (pass trắng, không phát hiện bug)
- **Wave 7:** Revert endpoint — không có API mới
- **Wave 8:** Revert UI — không có page mới (hoặc descope export only, giữ UI render)

**Không có wave nào break production** — tất cả là additive fix hoặc fix logic không ảnh hưởng existing flow.
- **Wave 2 (Option A):** revert refactor query — quay lại query JournalEntries (rỗng, SUM=0, nhưng không crash)
- **Wave 2 (Option B):** revert event handler — RecordRevenue không phát event, JournalEntries rỗng (như cũ)

**Per-wave merge to main** cho phép revert từng wave độc lập — không cần revert toàn bộ stream.
**Wave 5c là exception** — fix-forward thay vì revert (pháp lý critical).

---

## 14. REFERENCES (v3)

- **Mẫu TT 152:** `docs/plan_MVP/HKD_BookAcc/*.docx` (7 files — S1a, S2a-S2e, S3a)
- **Audit report:** Session 2026-07-03 (chat history — 8 root causes)
- **v2 review:** Session 2026-07-03 (3 amendments + 5 concerns resolved)
- **v3 review:** Session 2026-07-03 (2 phản biện — Dual Write architecture + HKD legal regime + 2026 regulatory)
- **Nguồn pháp lý 2026:** meinvoice.vn/MISA (14/04/2026) — trích Luật Thuế GTGT/TNCN sửa đổi 2025 + ND 117/2025 + Nghị quyết 198/2025/QH15
- **E2E cleanup master plan (template):** `docs/AI/tasks/e2e_test_cleanup_master_plan.md`
- **Governance:** `.devin/rules/governance.md` (Domain protection, Hard Stops, UI Platform)
- **Workflow:** `.devin/workflows/newfeaturebuild.md` (ANALYZE → IMPLEMENT)
- **Outbox skill:** `.devin/skills/outbox-pattern-implementation.md` (Option B reference)
- **Task cards (v3 — 12 cards):**
  - `wave0_hkd_fix_preflight_task_card.md` (expanded — 11 tasks)
  - `wave0p5_hkd_fix_arch_decision_data_source_task_card.md` ✅ CREATED (Option A vs B decision)
  - `wave1_hkd_fix_encoding_mojibake_task_card.md`
  - `wave2_hkd_fix_data_source_bridge_task_card.md` (RENAMED + REWRITE per W0.5 — Option A or B)
  - `wave3_hkd_fix_wire_calc_engine_di_task_card.md` (W3-T8 demoted to verify-only)
  - `wave4_hkd_fix_route_through_generation_service_task_card.md` (added W4-T11 smoke test)
  - `wave5a_hkd_fix_account_mapping_pit_task_card.md` ✅ CREATED (split from wave5)
  - `wave5b_hkd_fix_industry_sector_tax_rates_task_card.md` ✅ CREATED (split from wave5, conditional)
  - `wave5c_hkd_fix_2026_regulatory_compliance_task_card.md` ✅ CREATED (2026 regulatory fix)
  - `wave6_hkd_fix_retrofit_numeric_tests_task_card.md`
  - `wave7_hkd_fix_api_endpoint_di_smoke_task_card.md` (added W7-T6 multi-tenancy test)
  - `wave8_hkd_fix_ui_docx_export_regression_task_card.md`

> **NOTE:** All 12 task cards created (Wave 0, 0.5, 1-8, with Wave 5 split into 5a + 5b + 5c). Old `wave5_hkd_fix_account_mapping_tax_formulas_task_card.md` superseded by 5a + 5b + 5c cards. Wave 2 task card needs rename + rewrite per W0.5 decision (after Option A or B chosen).
- **Project state:** `docs/AI/project_state.md` (update sau mỗi wave)
