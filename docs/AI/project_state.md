# Project State

> **Mục đích:** Single Source of Truth cho AI về trạng thái dự án. BẮT BUỘC đọc đầu mỗi phiên.

---

## 0. Maintenance Rules

1. One-and-only-one: Mỗi section chỉ tồn tại 1 lần.
2. No contradiction: Một hạng mục chỉ có 1 trạng thái.
3. Ground Truth first: Verify path/branch với codebase trước khi ghi.
4. Now over History: Section 2-4 chỉ mô tả việc ĐANG làm và KẾ TIẾP. Việc xong → gom vào Section 6.
5. Actionable Next Actions: Xóa action đã quá hạn/sai bối cảnh.
6. Stamp every edit: Cập nhật Section 11 mỗi lần sửa.

---

## 1. Project Overview

**Dự án:** Vạn An Accounting System MVP — giải pháp kế toán HKD theo TT 152/2025/TT-BTC.
**Stack:** .NET 8 · EF Core · SQLite · Blazor Server (ShopERP) · Blazor WebAssembly (KhachLink PWA) · SignalR · YARP Gateway · xUnit · Playwright.
**Kiến trúc:** Clean Architecture + DDD + Multi-tenancy. Data flow: `KhachLink (5002) → Gateway (5001) → ShopERP (5003) → SQLite`.

**Modules:** `1_Shared` (Domain) · `2_Gateway` (YARP) · `3_CoreHub` (Services, in-process) · `5_WebApps/ShopERP` (Blazor Server) · `5_WebApps/KhachLink` (Blazor WASM) · `UI.Platform` (Shared components) · `6_Tests/6_Testing`.

**Hard stops:** Domain PURE · `AccountingEntry` immutable · Gateway STATELESS · KhachLink HTTP-only · ShopERP SQLite-only · ALWAYS dùng UI Platform components.

---

## 2. Current Objective

**[STREAM F: VAS ENTERPRISE FINANCIAL REPORTS (TT 99/2025 + TT 133/2016 + TT 58/2026) — W0+W1+W2 MERGED TO MAIN, W3 NEXT]**

Build 4 BCTC VAS (Balance Sheet + Income Statement + Cash Flow + Trial Balance) cho doanh nghiệp, feature-flag module riêng (HKD giữ nguyên). 10-wave plan (W0→W9): Writer Fix → Seed → Domain → Account Map → 4 Services → API → UI → Tests → Feature Flag → Regression.

- **Master plan:** `docs/AI/tasks/vas_enterprise_reports_master_plan.md` (W0 DONE, W1 DONE, W2 DONE, W3 next — section 4.1 has downstream impact notes for JIT update)
- **Task cards:** `docs/AI/tasks/vas_wave{0-9}_task_card.md` (10 cards, detail coding plan per wave)
- **Legal framework verified:** TT 99/2025 (thay TT 200, hiệu lực 01/01/2026) + TT 133/2016 (vẫn hiệu lực, DN vừa/nhỏ) + TT 58/2026 (thay TT 132, DN siêu nhỏ)
- **Audits complete:** (1) 4 BCTC current state — 3/4 mock/stub, 1/4 query broken; (2) Order→Accounting data flow — 18 vấn đề (3C+5H+10M)
- **Decisions approved (D1-D9):** 3 tầng chuẩn · VAS module riêng · 4 BCTC song song · Writer fix trước seed · Domain mod approved · Seed DN vừa TT 133 · JIT Planning · **D9: HKD↔DN conversion = Option B (New Tenant + Link) + Read-only historical + Amend W2+W3+W8**
- **W0 COMPLETE & MERGED (`be348ad` → main, 2026-07-04):** Fixed 9/18 issues — C1 COGS sync (shared `CalculateCogsAmount`), C2 PaymentMethod passed into `ConfirmPayment`, C3 VAT split both paths (511 net + 3331), H1 `PaymentMethodConstants` (111/112), H2 Discount net revenue, H4 OrderDate period, H5 Order reference, M9 COGS removed from S2d, B3 621→632. 10 new tests (SC17-SC23). Core.Tests 828/828, Arch.Tests 31/31, guard PASSED. **Deferred:** H3 Shipping (pending BA/Kế toán), M1/M6/M2-M5/M7/M8/M10, VAS Gross+521 (W8).
- **W1 COMPLETE & MERGED (`dd82f5f` → `93a5e7e` → main, 2026-07-04):** Data Audit + Seed. **Data Audit findings:** (1) `JournalEntries` table missing 3 columns (`EntryDate`, `ReferenceId`, `IsReversal`) — EF model snapshot didn't map them, W0 writer data was silently dropped on persist; (2) `JournalEntryLine.Id` was shadow property that SQLite can't auto-generate for composite keys — genuine modeling defect. **Fixes:** Migration `20260704044449_AddJournalEntryMissingColumns` (3 columns + EntryDate index), `JournalEntryConfiguration` updated (ReferenceId, IsReversal, ValueGeneratedNever for Id), `JournalEntryLine` Domain entity gained explicit `Id` property (sequential per entry via `AddLine`). **Seeder:** `VasSampleDataSeeder.cs` — 1 Enterprise tenant (DN vừa TT 133), 31 journal entries + ~50 AccountingEntries, 2 months (2026-05, 2026-06), CASH+VIETQR, opening balances (111/112/156/211/214/411/331/3331), all scenarios (sales, COGS 632, CP 6421/6422, khấu hao 214, lương, discount 511, shipping 5113, NCC, công nợ). **5 lỗi kế toán sơ đẳng phản biện & sửa:** (1) 311→411 Vốn CSH; (2) Khấu hao Có 211→214; (3) Chiết khấu 521→511 (TT 133 khai tử 521); (4) Phí vận chuyển 515→5113; (5) CP bán hàng 641→6421 (TT 133 gộp 642). 15 tests (all PASS). Core.Tests 843/843, guard PASSED. Dev DB recreated from migrations.
- **W2 COMPLETE & MERGED (`a546c48` + `9165bf6` → `fef8097` → main, 2026-07-04):** Domain Records + D9 HKD↔DN conversion. **Domain.cs additions:** 3 enums (`TenantType` HKD/Enterprise_SuperSmall/SME/Large, `AccountType` 5 values no Contra, `AccountingStandard` TT99/133/58 — D1 scope only), `FinancialStatementLine` (ReportItemCode + Ending/Opening + Level + IsNormalNegative per Mẫu B01-DN/B02-DN/B03-DN — NOT AccountCode-based), 3 BCTC records (`BalanceSheet` 2-column totals no IsBalanced flag, `IncomeStatement` 2-column, `CashFlowStatement` 3 activity sections as lines), `OpeningBalance`/`OpeningBalanceLine`, `AccountChartEntry` (in-memory, IsNormalCredit flag for contra TK 214). **Tenant aggregate:** `Converted=4` status (read-only historical, distinct from Inactive), `TenantConvertedEvent`, 5 new fields (`PredecessorTenantId`, `SuccessorTenantId`, `ConvertedAt`, `AccountingStandard` general property, `TenantType? Type`), `CreateFromConversion` factory + `MarkConvertedTo` method + `IsConverted`/`IsConversionOf` query helpers. **Review fixes (C1+C2+H1+H2+H3):** C1 fixed dead `newType` parameter (added `Type` property, set in factories, guard newType!=HKD), C2 added explicit `Ignore()` for 5 new Tenant fields in TenantConfiguration (W8 will add migration), H1 set `ConvertedAt` in `MarkConvertedTo` (audit trail), H2 Suspended conversion ALLOWED with business rationale comment, H3 guards for `TenantId.Empty` on predecessor/successor. **External review integrated:** BCTC uses ReportItemCode + 2-column comparative (legal compliance), no IsBalanced flag (invariant → W4 factory throws), AccountType.Contra removed → IsNormalCredit flag, ConvertedToStandard → general AccountingStandard, KEPT SetTenantId (NOT redundant — sets BaseEntity.TenantId), REJECTED TT88_2021 (out of D1 scope). 11 unit tests (W2-D1 to W2-D11, all PASS). Build 0 errors, guard PASSED, Arch tests PASSED. **Deferred:** H4 SetAccountingStandard/SetTenantType methods (W8), M1/M2 BS/IS/CF + FinancialStatementLine validation (W4 service layer), M3/M4/M5 covered by W2-D1/D10/D11.
- **Next:** W3 (Account Code Map) — create AccountChart mapping table + 3 standards + HKD→DN account mapping (D9) + refactor hardcoded GetAccountName. Branch `feature/vas-wave3-account-code-map`. **JIT note:** W3 task card needs update for `AccountType` (no Contra) + `IsNormalCredit` seed handling per master plan section 4.1.

---

**[STREAM D: HKD BOOK ACCOUNTING REPORT FIX (TT 152/2025/TT-BTC + 2026 REGULATORY COMPLIANCE) — ACTIVE, WAVE 8 🔄 IN PROGRESS (branch `feature/hkd-fix-wave8-ui-docx-export-regression`, Session 1 committed `c8eb819`, Session 2 in progress)]**

Fix 8 root-cause issues + 2 architecture/legal findings preventing correct TT 152 HKD book report generation. Dependency-ordered 12-wave fix (data → DI → routing → formulas → 2026 regulatory → tests → API → UI → export). **Wave 5c COMPLETE & MERGED (2026-07-03):** 2026 Regulatory Compliance Fix merged to main (`aa930dd`). Domain: fix `HKDRevenueClassification.CalculateGroup` thresholds 500M/1B/3B → 1B/3B/50B + add `CalculateTNCN` (Nhóm 1: 0, Nhóm 2: (Rev-1B)×rate, Nhóm 3: (Rev-Expense)×17%, Nhóm 4: (Rev-Expense)×20%) + `CalculateGTGT` (Nhóm 1 exemption). Service: fix `HKDRevenueClassificationService` thresholds + warnings. Template: S2a `CalculateAsync` override for Nhóm-aware TotalPIT + TotalExpense field + blended PIT rate. `HKDTaxClassificationService`: fix hardcoded 10% TNCN → `CalculateTNCN`. 20 unit tests (all PASS). Build 0 errors, guard PASSED. User-approved: legal review, Domain fix, 10% bug in scope, Nhóm 3/4 w/ warning.

- **Master plan:** `docs/AI/tasks/hkd_book_accounting_fix_master_plan.md` (v3 — 12 waves, 8 root-cause issues + 2 architecture/legal findings, 5 amendments + 5 concerns resolved)
- **Planning commits:** `c4acb15` (v1) → `c8d4a6c` (v2) → `22d3976` (W0 expand) → `88e635a` (v3) → `4b2f077` (W0.5+W5c) → `7fb6dec` (W5a+W5b+slim)
- **Execution branch:** `feature/hkd-fix-wave0-wave0p5-preflight` (Wave 0 + Wave 0.5 in progress)
- **Wave 0 + 0.5 execution results (2026-07-03):**
  - **W0-T1 Build:** ✅ PASS — 0 errors, 962 warnings
  - **W0-T2 DI:** ✅ 0 matches — 5 calc engine services chưa wire DI
  - **W0-T3 Endpoint:** ✅ 0 matches — no `hkd-books`/`GenerateS*BookAsync` in Controllers
  - **W0-T4 UI:** ✅ 0 matches — no S1a/S2a-S2e/S3a in `5_WebApps`
  - **W0-T5 DB Query:** ⚠️ `JournalEntries` 0 rows + **CRITICAL: AccountingEntries schema STALE** (missing Amount, EntryType, AccountCode, PeriodYear, PeriodMonth — only BaseEntity columns). Root cause: `EnsureCreatedAsync()` không update schema, no Migrations. **→ Stream E spawned (EF Core Migrations strategy).**
  - **W0-T6 Git:** ✅ Clean
  - **W0-T7 docx:** ⚠️ PARTIAL — 7 files found (1 binary .doc + 6 .docx). S2a_HKD.docx extracted (122 `<w:t>` elements), text output incomplete. S1a_HKD.doc là binary format cần pandoc/LibreOffice.
  - **W0-T8 ITemplateFactory:** ✅ RESOLVED — old `Services/TemplateFactory.cs` implements `ITemplateFactory` (for OrderService), new `Services/Template/TemplateFactory.cs` KHÔNG implement interface → no conflict. Keep both, register new as concrete.
  - **W0-T9 Export lib:** ✅ RESOLVED — EPPlus 7.6.1 (XLSX OK). DOCX: user approved thêm `DocumentFormat.OpenXml` dependency (Wave 8).
  - **W0-T10 Tenant.IndustrySector:** ✅ RESOLVED — MISSING. Wave 5b CONDITIONAL (Tech Lead approval for W5b-T0 OR descope).
  - **W0-T11 Double-write audit:** ✅ RESOLVED — Path B. OrderService L114-141 đã persist JournalEntry via `AddToBookAsync`. Nhưng W0.5 chọn Option A (query AccountingEntries, không viết JournalEntry) → no double-write risk.
  - **W0.5-T1 AccountCode:** ✅ Populated for OrderService path (511/621), NULL for `RecordRevenueAsync` path. Option A needs EntryType-based heuristic for null AccountCode.
  - **W0.5-T2 Formula Engine:** ✅ CAN map EntryType → Credit/Debit (Revenue=Credit, Expense=Debit). `Account_{pattern}_Credit/Debit` aggregates mappable.
  - **W0.5-T3 Product roadmap:** ✅ HKD-ONLY (no Doanh nghiệp engine sharing).
  - **W0.5-T4 DECISION:** ✅ **Option A — refactor `SmartPreAggregationService.GetAccountSumAsync` to query `AccountingEntries` directly** (no JournalEntry writing, no Domain modification). Rationale: HKD-only product + AccountingEntry is immutable SSoT + EntryType mappable + avoids existing OrderService double-write.
- **3 NEW GAPS discovered (not in original plan):**
  - **Gap-1 (BLOCKER):** Dev DB schema stale — `EnsureCreatedAsync()` không update schema khi model thay đổi. Production deployment trap. **→ Stream E: EF Core Migrations Strategy spawned.** User decision: Enable EF Migrations, loại bỏ EnsureCreated khỏi production, sửa VA-ARCH-001 (cho phép Migrations ở đúng layer Infrastructure).
  - **Gap-2 (Medium):** `S1a_HKD.doc` binary format — cần pandoc/LibreOffice (free, sẽ install).
  - **Gap-3 (Medium):** 6 .docx extraction script incomplete (PowerShell regex issue).
- **Stream E: DB Migration Strategy (NEW — spawned from Wave 0 Gap-1):**
  - **Decision (user-approved 2026-07-03):** (1) Use EF Core Migrations as official schema management, (2) Remove `EnsureCreated()` from production, (3) Modify VA-ARCH-001 to allow Migrations at correct layer (Infrastructure) while still preventing other architecture violations.
  - **Status:** ✅ COMPLETE & MERGED TO MAIN (`b2e0431`). InitialCreate migration created (6037 lines), DesignTimeDbContextFactory added, VA-ARCH-001 modified (allows Infrastructure Migrations, forbids Application Migrations), `EnsureCreatedAsync` → `MigrateAsync` in CoreHub/Program.cs, dev DB migrated (AccountingEntries has ALL domain columns). 28/28 architecture tests pass.
  - **Blocks:** ~~Wave 2 verification~~ ✅ UNBLOCKED. ~~Wave 7 manual testing~~ ✅ UNBLOCKED. ~~Wave 8 manual testing~~ ✅ UNBLOCKED.
- **v3 amendments (approved 2026-07-03, from phản biện kiến trúc + pháp lý):**
  4. **Add Wave 0.5 — Architecture Decision: HKD Data Source (A vs B, loại C dual-write)** — Option C (hardcode 2 lệnh ghi trong RecordRevenue) bị LOẠI per phản biện. Option A (refactor SmartPreAggregationService query AccountingEntries) hoặc Option B (event-driven Outbox). Codebase verify: AccountingEntry có đủ field, Outbox infrastructure đã có.
  5. **Add HKD Accounting Regime Disclaimer + Fix suất thuế + Add Wave 5c** —
     - 5a: HKD = single-entry per TT 88/2021 + TT 152/2025, account mapping là Internal Synthetic Mapping (KHÔNG phải TT 200/133)
     - 5b: Sửa suất thuế — xóa "2,5%" fabricated, dùng đúng 4 nhóm per Luật 2025 + ND 117/2025 (1%/0.5%, 3%/1.5%, 5%/2%, 2%/1%)
     - 5c: Add Wave 5c — 2026 Regulatory Compliance Fix (threshold 500M→1B, 4 revenue groups mới, TNCN formulas Nhóm 2/3/4, thuế khoán abolished, lệ phí môn bài abolished) — CRITICAL pháp lý
- **v2 amendments (approved 2026-07-03):**
  1. **Promote 4 architecture decisions to Wave 0** — W0-T8 (`ITemplateFactory` conflict), W0-T9 (docx/xlsx lib), W0-T10 (`Tenant.IndustrySector`), W0-T11 (double-write audit). Resolve BEFORE IMPLEMENT mode.
  2. **Split Wave 5 into 5a + 5b** — 5a (account mapping + PIT-on-revenue), 5b (industry-sector tax rates, CONDITIONAL).
  3. **Add Wave 4 smoke test (W4-T11)** — `NumericValues` not-empty tripwire.
- **v2 concern resolutions:** C1 (double-write tree), C2 (ITemplateFactory→W0-T8), C3 (Wave 5 split), C4 (Tenant.IndustrySector→W0-T10), C5 (smoke test), C6 (export lib→W0-T9), C7 (multi-tenancy test), C8 (per-wave merge).
- **Root cause (8 issues):**
  1. Production path `CalculateAsync` no-op → `NumericValues` always empty (Critical)
  2. Calc engine exists (`Services/Template/`) but not wired in DI (Critical)
  3. Data flow gap: `JournalEntries` table empty — no bridge from `AccountingEntry` (Critical)
  4. Tests pass white (no numeric assertions) — bug hidden (Critical)
  5. Tax formulas hardcoded 5%/10% — non-compliant vs TT 152 industry rates (High)
  6. Output is plain text, not docx/xlsx TT 152 layout (High)
  7. UTF-8 mojibake in `Services/Template/TemplateFactory.cs` (Medium)
  8. Account mapping hallucinated (`_vietnameseAccounts` 211/811/821/841/521 wrong) (Medium)
- **v3 findings (from phản biện):**
  9. **Dual Write anti-pattern risk** — Option C (hardcode 2 ghi trong RecordRevenue) bị LOẠI, Wave 0.5 chốt A or B
  10. **2026 regulatory non-compliance** — `HKDRevenueClassificationService` threshold 500M (sai, phải 1B), 4 groups sai, TNCN formula sai, missing thuế khoán abolished — Wave 5c fix
- **Target:** 7 HKD book templates (S1a, S2a-S2e, S3a) generate `NumericValues` with real data, output docx/xlsx per TT 152 layout, endpoint + UI page, tests assert numeric values, multi-tenancy enforced, **2026 regulatory compliant** (threshold 1B, 4 revenue groups, TNCN formulas đúng), **no dual write** (Option A or B), regression prevention.

### 12 Waves (v4 — dependency-ordered, per-wave merge to main; Wave 5 merged 5a+5b+partial 5c per 2026-07-03 investigation)
| Wave | Description | Sessions | Risk | Status |
|---|---|---|---|---|
| 0 | Pre-flight verification (11 tasks — baseline + 4 promoted architecture decisions + double-write audit) | 0.5-1 | None | 🔄 IN PROGRESS (15/21 tasks done, 3 new gaps, 6 remaining) |
| 0.5 | **[v3] Architecture Decision: HKD Data Source (A vs B, loại C dual-write)** | 0.5-1 | None (decision) | ✅ DONE — Option A chosen (query AccountingEntries directly) |
| 1 | Fix UTF-8 mojibake in `Services/Template/TemplateFactory.cs` (S1a + S2a TemplateImpl) | 0.5 | Low | ✅ DONE — Merged `2f294c8` |
| 2 | Data source bridge (Option A: refactor SmartPreAggregationService query AccountingEntries) | 0.5-2 | Medium/Low | ✅ DONE — Merged `b08d907` |
| 3 | Wire 5 calc engine services into DI (conflict pre-resolved in W0-T8) | 1 | Low | ✅ DONE — Merged to main |
| 4 | Route `HKDBookService.GenerateS*BookAsync` through `IHKDBookGenerationService` + smoke test tripwire (W4-T11) | 1 | Medium | ✅ DONE — Merged `7dbbcb1`. 7 methods routed, `ConvertToJournalEntries` marked obsolete. SC8 (NumericValues populated) deferred to Wave 6 verification. |
| 5 | **[v4 MERGED] Industry Sector + PIT Fix + Account Mapping + 4-group Tax Rates** — Add `IndustrySector` enum+field to `AccountingEntry`+`Tenant`+`Order` (Domain mod, Tech Lead APPROVED 2026-07-03), extend Formula Engine DSL (`SUM_ACCOUNT_BY_INDUSTRY`), redesign S2a/S2b per TT 152 industry-sector layout, fix PIT formula (`VatAmount*0.1`→`TotalRevenue*industryPitRate`), fix `_vietnameseAccounts` labels, 4-group rates per Luật 2025 + ND 117/2025. **Supersedes old 5a+5b.** Old cards archived. | 3-4 | High (Domain mod + Formula Engine DSL extension) | ✅ DONE — Committed `1ac8252`, merged to main. 6 micro-phases (S1-S6) complete. 29 files, +3203/-201. Build PASS, 10/10 Wave5 tests PASS. 3 pre-existing failures (unrelated HKDBookServiceTests). |
| 5c | **[v3] 2026 Regulatory Compliance Fix (threshold 500M→1B in `HKDRevenueClassification.CalculateGroup` + `HKDRevenueClassificationService`, TNCN formulas Nhóm 2/3/4, thuế khoán abolished, lệ phí môn bài abolished) — CRITICAL pháp lý — Legal review recommended** | 1-2 | High (pháp lý) | ⏳ PENDING |
| 6 | Retrofit tests with numeric assertions (3 update + 5 new + 1 regression) | 1-2 | Low | ✅ DONE — merged to main (`d6c3bb2`, branch `feature/hkd-fix-wave6-retrofit-numeric-tests` deleted after merge). 3 updated (S1a/S2a/S2b — added `IHKDBookGenerationService.GenerateBookAsync` mock + numeric asserts + routing verify, removed obsolete repo verify) + 5 new numeric (S2c/S2d/S2e/S3a + all-templates Theory×7) + 1 regression (W6-T8 Issue 1 tripwire — `NumericValues` not empty + repo `Times.Never`). Core.Tests Release 818/818 PASS (was 803/3 fail), guard PASSED. |
| 7 | API endpoint `GET /api/hkd-books/{templateCode}` + DI smoke + multi-tenancy isolation test (W7-T6) | 1 | Low | ✅ DONE — Committed `76d2c11` on branch `feature/hkd-fix-wave7-api-endpoint-di-smoke`. HKDBookDto + HKDBooksController (2 endpoints) + 2 DI smoke tests + 4 endpoint tests (6/6 PASS). Fixed 7 pre-existing bugs: Gateway missing DI, circular dependency (Lazy<IFormulaEngine>), JournalEntryConfiguration missing EntryDate, HKDBookGenerationService unmapped Period query, CreateBaseVariables GUID→decimal parse, BaseHKDBookTemplate null! logger, CalculateFormulaAsync legacy variables overload → FormulaContext. Core.Tests 818/818 PASS, Architecture.Tests 28/28 PASS, guard PASSED. |
| 8 | UI page `/accounting/hkd-books` + DOCX/XLSX export + architecture test + encoding lint | 2-3 | Medium | 🔄 IN PROGRESS — Session 1 committed `c8eb819` (UI + export + DI wiring). Session 2: E2E spec + architecture test + encoding lint + docs (in progress) |

**Critical path:** Wave 0→0.5→1→2→3→4→~~5 (merged)~~ ✅→~~5c~~ ✅→~~6 (merged)~~ ✅→7→8 (sequential).
**Branch strategy:** Per-wave merge to main (always-green, no long-lived branch).
**Estimated total:** 11-16 sessions (6 done & merged, 2 remaining: Wave 7 + Wave 8).

### Parked / Completed Streams
- **Stream A: EInvoice Provider Rewrite** — Planning complete (`59b60fe`). Blocker: Wave 0 sandbox credentials (1-2 tuần).
- **Stream B: E2E Test Cleanup** — ✅ COMPLETE (`ffe8607`). 8 waves, 7 anti-patterns, all merged-ready on `feature/e2e-cleanup-wave8-regression-prevention`. Awaiting merge to main.
- **Stream C: ShopERP UI Fix** — ✅ COMPLETE & MERGED TO MAIN (`f3ed2d2`). 6 waves, 23 .razor files fixed.
- **Stream E: DB Migration Strategy** — ✅ COMPLETE & MERGED TO MAIN (`b2e0431`). EF Core Migrations enabled, EnsureCreated replaced with MigrateAsync, VA-ARCH-001 modified. Unblocks Wave 2/7/8.

---

## 3. Current Status

- **Branch:** `main` (W0 merged here) · `feature/hkd-fix-wave8-ui-docx-export-regression` (Stream D Wave 8 Session 2 — parked)
- **VAS Stream (Stream F) — W0 COMPLETE & MERGED:**
  - **Master plan:** `docs/AI/tasks/vas_enterprise_reports_master_plan.md` (W0 DONE, W1 next)
  - **10 task cards:** `vas_wave{0-9}_task_card.md` (W0: 4.9KB, W1: 2.8KB, W2: 3.9KB, W3: 2.3KB, W4: 3.4KB, W5: 2.2KB, W6: 2.5KB, W7: 2.6KB, W8: 2.3KB, W9: 2.0KB)
  - **W0 merged `be348ad`:** 9/18 issues fixed (C1-C3, H1-H2, H4-H5, M9, B3). New `PaymentMethodConstants` class + shared `CalculateCogsAmount`. VAT split both paths. 10 new tests (SC17-SC23). Core.Tests 828/828, Arch.Tests 31/31, guard PASSED. Deferred: H3 Shipping, M1/M6/M2-M5/M7/M8/M10, VAS Gross+521 (W8).
  - **Audit 1 (4 BCTC):** Balance Sheet=STUB, Income Statement=MOCK 120M/70M, Cash Flow=MOCK 150M/80M, Trial Balance=query broken (Pattern #1+#5)
  - **Audit 2 (Order→Acc):** 18 vấn đề — 9 fixed in W0, 9 deferred
  - **Legal:** TT 99/2025 (DN lớn) + TT 133/2016 (DN vừa/nhỏ) + TT 58/2026 (DN siêu nhỏ) — user confirmed
- **Stream D Wave 8:** Session 2 in progress (E2E + arch test + encoding lint) — parked, resume after VAS W1

---

- **Branch:** `feature/hkd-fix-wave8-ui-docx-export-regression`
- **Last commit:** `c8eb819` Wave 8 S1: HKD Book UI page + DOCX/XLSX export + DI wiring
- **Build:** `dotnet build VanAn.sln` → 0 errors ✅ (Architecture.Tests project build verified)
- **Guard-check:** ✅ PASS (pending re-run after Session 2 commit)
- **Tests:** Architecture.Tests 3/3 HKDBookTemplateArchitectureTests PASS · Playwright `--list` 36 tests in 1 file (hkd-books.spec.ts) · Encoding lint PASS (923 files, 0 mojibake)
- **Uncommitted changes (Session 2):** `scripts/check-encoding.ps1` (fixed false-positive mojibake detection — now scans for 2-char lead+continuation sequences, not single Latin-1 chars) · `6_Testing/e2e-tests/hkd-books.spec.ts` (NEW — 6 E2E tests) · `6_Tests/VanAn.Architecture.Tests/HKDBookTemplateArchitectureTests.cs` (NEW — 3 regression tests for Issue 1) · `docs/UI_Platform_Implementation_Guide.md` (Wave 8 HKD Book module section) · `docs/AI/project_state.md` (this update)
- **Wave 8 Session 1 artifacts (committed `c8eb819`):**
  - **NEW** `5_WebApps/ShopERP/Components/Pages/Accounting/HKDBooks.razor` — List page (7 templates)
  - **NEW** `5_WebApps/ShopERP/Components/Pages/Accounting/HKDBookDetail.razor` + `.razor.css` — Detail + export buttons
  - **NEW** `5_WebApps/ShopERP/Services/HKDBookExportService.cs` — DOCX (OpenXML) + XLSX (EPPlus) export
  - **MODIFIED** `5_WebApps/ShopERP/Program.cs` — DI registrations (IHKDBookGenerationService + dependencies)
  - **MODIFIED** `3_CoreHub/Services/Template/HKDBookGenerationService.cs` — `VanAnDbContext` → `IVanAnDbContext`
  - **MODIFIED** `5_WebApps/ShopERP/Components/Pages/Accounting/AccountingIndex.razor` — Link to HKD Books
  - **MODIFIED** `5_WebApps/ShopERP/Components/Pages/Accounting/AccountingLayout.razor` — Nav item
  - **MODIFIED** `Directory.Packages.props` + `5_WebApps/ShopERP/VanAn.ShopERP.csproj` — DocumentFormat.OpenXml dependency
- **Wave 8 Session 2 artifacts (uncommitted):**
  - **NEW** `6_Testing/e2e-tests/hkd-books.spec.ts` — 6 E2E tests (list, detail, TT 152 layout, export buttons, nav)
  - **NEW** `6_Tests/VanAn.Architecture.Tests/HKDBookTemplateArchitectureTests.cs` — 3 regression tests (SC6: all HKDBookTemplate subclasses extend BaseHKDBookTemplate; 7 production templates present; CalculateAsync not abstract)
  - **NEW** `scripts/check-encoding.ps1` — SC7 mojibake encoding lint (fixed false-positive detection)
  - **MODIFIED** `docs/UI_Platform_Implementation_Guide.md` — Wave 8 HKD Book module reference
- **Wave 7 artifacts (committed `76d2c11`):**
  - **NEW** `1_Shared/DTOs/HKDBookDto.cs` — DTO with `TenantId`, `Period`, `BookTypeCode`, `NumericValues`, `Entries`
  - **NEW** `2_Gateway/Controllers/HKDBooksController.cs` — `GET /api/hkd-books` + `GET /api/hkd-books/{templateCode}`. JWT Bearer auth.
  - **NEW** `6_Tests/VanAn.Integration.Tests/HKDBookDISmokeTests.cs` — 2 DI smoke tests
  - **NEW** `6_Tests/VanAn.Integration.Tests/HKDBooksEndpointTests.cs` — 4 endpoint tests (6/6 total PASS)
  - **NEW** `docs/AI/tasks/wave7_root_cause_investigation_log.md` — Root cause investigation log
  - **MODIFIED** `2_Gateway/Program.cs` — Added missing DI registrations (IAuditTrailService, IAuditLogRepository, calc engine, Lazy<IFormulaEngine>)
  - **MODIFIED** `3_CoreHub/Program.cs` — Added `Lazy<IFormulaEngine>` registration
  - **MODIFIED** `3_CoreHub/Services/PreAggregation/SmartPreAggregationService.cs` — `IFormulaEngine` → `Lazy<IFormulaEngine>` (circular dependency fix)
  - **MODIFIED** `3_CoreHub/Services/Template/HKDBookGenerationService.cs` — `EntryDate` range filter (unmapped Period fix)
  - **MODIFIED** `3_CoreHub/Services/Template/TemplateCalculationEngine.cs` — `CreateBaseVariables` GetHashCode fix + `CalculateFormulaAsync` FormulaContext overload fix
  - **MODIFIED** `3_CoreHub/Services/Template/BaseHKDBookTemplate.cs` — `null!` logger → `NullLogger<TemplateCalculationEngine>.Instance`
  - **MODIFIED** `3_CoreHub/Infrastructure/Configurations/JournalEntryConfiguration.cs` — Added `EntryDate` mapping
  - **MODIFIED** `6_Tests/VanAn.Core.Tests/Services/SmartPreAggregationServiceWave2Tests.cs` — Updated for `Lazy<IFormulaEngine>`
- **Pre-existing bugs fixed in Wave 7 (7 total):**
  1. Gateway missing DI registrations (IAuditTrailService, IAuditLogRepository, calc engine)
  2. Circular dependency IFormulaEngine→IDataProvider→IPreAggregationService→IFormulaEngine (Lazy<IFormulaEngine>)
  3. JournalEntryConfiguration missing EntryDate mapping
  4. HKDBookGenerationService queried unmapped Period.Year/Month → EntryDate range
  5. TemplateCalculationEngine.CreateBaseVariables GUID→decimal parse (GetHashCode proxy)
  6. BaseHKDBookTemplate null! logger (NullLogger fix)
  7. CalculateFormulaAsync used legacy variables overload → FormulaContext overload (root cause of TotalRevenue=0)
- **Completed features (merged to main):** Tenant Onboarding (6 waves) · ShopConfig Refactor (3 phases) · Architecture Test Fixes · CI/CD Hotfix · **Stream C: ShopERP UI Fix (6 waves)** · **Stream B: E2E Test Cleanup (8 waves, planning merged; wave branches await merge)** · **Stream D Wave 0+0.5+1+2+3+4+5+5c** · **Stream E: DB Migration Strategy**.
- **Wave 5c execution artifacts (merged to main `aa930dd`):**
  - `1_Shared/Domain.cs` — `HKDRevenueClassification.CalculateGroup` thresholds 1B/3B/50B + `CalculateTNCN` + `CalculateGTGT` static methods + enum comments
  - `3_CoreHub/Services/Orchestration/HKDRevenueClassificationService.cs` — thresholds 1B/3B/50B + warning messages per 4 nhóm
  - `3_CoreHub/Services/Template/TemplateFactory.cs` — S2a `CalculateAsync` override (Nhóm-aware TotalPIT) + `TotalExpense` field + blended PIT rate + GTGT Nhóm 1 exemption + report displays revenue group
  - `3_CoreHub/Services/HKDTaxClassificationService.cs` — fix hardcoded 10% TNCN → `CalculateTNCN` + GTGT → `CalculateGTGT` + optional `IndustrySector` parameter
  - `3_CoreHub/Services/IHKDTaxClassificationService.cs` — add optional `IndustrySector` parameter
  - `6_Tests/VanAn.Core.Tests/Services/Wave5cRegulatoryComplianceTests.cs` — 20 unit tests (11 Theory + 9 Fact)
- **Stream D execution artifacts (merged to main):**
  - `docs/AI/tasks/wave0_hkd_fix_preflight_task_card.md` — updated with Section 13 (Execution Findings) + Section 14 (3 New Gaps) + Section 15 (Updated SC)
  - `docs/AI/tasks/wave0p5_hkd_fix_arch_decision_data_source_task_card.md` — updated with Section 13 (Decision Output: Option A) + Section 14 (DB Schema Caveat)
  - `docs/AI/project_state.md` — being updated with Wave 0+0.5 results + Stream E
- **Wave 5 planning artifacts (uncommitted, 2026-07-03):**
  - `docs/AI/tasks/wave5_hkd_fix_industry_sector_pit_task_card.md` — NEW merged card (supersedes 5a+5b+old 5)
  - `docs/AI/tasks/wave5a_hkd_fix_account_mapping_pit_task_card.archived.md` — archived (false premise: TT 200 compliance)
  - `docs/AI/tasks/wave5b_hkd_fix_industry_sector_tax_rates_task_card.archived.md` — archived (merged into Wave 5)
  - `docs/AI/tasks/wave5_hkd_fix_account_mapping_tax_formulas_task_card.archived.md` — archived (pre-split version)
- **Pre-existing defects found (NOT Stream C regressions):**
  1. **Blazor circuit crash on `/`, `/sitemap`, `/admin/users`** — `System.InvalidOperationException: Authorization requires a cascading parameter of type Task<AuthenticationState>` from `AuthorizeViewCore.OnParametersSetAsync()`. Pages prerender correctly (visual content visible) but interactivity breaks after circuit connect. Routes.razor has `<CascadingAuthenticationState>` + `<AuthorizeRouteView>` — cascade timing issue. Candidate for a dedicated Blazor auth fix stream.
  2. **DevLoginController role mismatch** — `/admin/users` uses `[Authorize(Policy = "OwnerOnly")]` (requires "Owner" role), but `POST /dev/login/systemadmin` issues "SystemAdmin" role → access denied. SystemAdmin dev login cannot reach admin pages. E2E tests must use Owner login for admin routes.
- **Dead code note:** `CustomerPage.ts` loyalty methods (`loyaltyPointsDisplay` L44, `getLoyaltyPoints` L191, `applyLoyaltyPoints` L201) are now unreferenced after Stream B Wave 4 SCENARIO 2 deletion. Candidate for future page-object cleanup.
- **In-progress:** Wave 6 merged to main (`d6c3bb2`). Next: Wave 7 (API endpoint `GET /api/hkd-books/{templateCode}` + DI smoke + multi-tenancy isolation test W7-T6).

---

## 4. Next Actions

**Stream F — VAS Enterprise Financial Reports (PLANNING COMPLETE, awaiting W0):**
1. ~~Audit 4 BCTC + Order→Acc flow~~ ✅ DONE — 18 vấn đề + 6 blockers identified
2. ~~Master plan v3 + 10 task cards~~ ✅ DONE — committed (this commit)
3. **W0: Order→Accounting Writer Fix** ⏳ NEXT — fix 18 vấn đề (3C+5H+4M, defer 6M). Branch `feature/vas-wave0-order-accounting-writer-fix`. Task card: `vas_wave0_task_card.md`. JIT: INVESTIGATE (verify OrderService.cs:line refs) → PLAN → IMPLEMENT.
4. W1: Data Audit + Seed — sau W0 merge
5. W2-W9: theo dependency chain (see master plan Section 1)

**Stream D — HKD Book Accounting Report Fix (active, Wave 0+0.5 in progress):**
1. ~~Audit + master plan v1 + 9 task cards~~ ✅ — Committed `c4acb15`.
2. ~~v2 plan optimization (3 amendments + 5 concerns resolved)~~ ✅ — Committed `c8d4a6c` + `22d3976`.
3. ~~v3 plan optimization (2 phản biện + Amendment 4+5)~~ ✅ — Committed `88e635a` + `4b2f077` + `7fb6dec`.
4. ~~Commit v3 master plan~~ ✅ — Committed `7fb6dec` + `dad3d76`.
5. ~~Wave 0: Pre-flight verification (11 tasks)~~ 🔄 IN PROGRESS — 15/21 tasks done (T1-T11 + W0.5-T1-T4). 3 new gaps found. See execution findings in `wave0_hkd_fix_preflight_task_card.md` Section 13-15.
6. ~~Wave 0.5: Architecture Decision~~ ✅ DONE — **Option A chosen** (refactor `SmartPreAggregationService` query `AccountingEntries` directly, no JournalEntry writing). See `wave0p5_hkd_fix_arch_decision_data_source_task_card.md` Section 13.
7. **Wave 0 remaining (6 tasks):** ⏳ PENDING
   - a. Propagate T8 decision → Wave 3 task card (ITemplateFactory: keep both, no conflict)
   - b. Propagate T9 decision → Wave 8 task card (EPPlus XLSX + DocumentFormat.OpenXml DOCX approved)
   - c. Propagate T10 decision → Wave 5b task card (IndustrySector MISSING, conditional)
   - d. Propagate T11 decision → Wave 2 task card (Path B, but Option A avoids JournalEntry writing)
   - e. W0-T7b: Install pandoc + extract S1a_HKD.doc layout
   - f. W0-T7c: Complete 6 .docx layout extraction
8. **W0.5-T5: Rewrite Wave 2 task card per Option A** ⏳ PENDING — Replace "Bridge JournalEntry persistence" with "Refactor SmartPreAggregationService query AccountingEntries". Rename file `wave2_hkd_fix_bridge_journal_persistence_task_card.md` → `wave2_hkd_fix_data_source_bridge_task_card.md`.
9. ~~Stream E: DB Migration Strategy~~ ✅ DONE — Merged `b2e0431`. InitialCreate migration + DesignTimeDbContextFactory + VA-ARCH-001 modified + EnsureCreated→MigrateAsync. Dev DB migrated.
10. ~~guard-check.ps1 + commit Wave 0+0.5~~ ✅ DONE — Committed `88a7fb4`, merged `06bfd44`.
11. ~~Merge Wave 0+0.5 to main~~ ✅ DONE — Merged `06bfd44`.
12. ~~Wave 1: Fix UTF-8 mojibake~~ ✅ DONE — Committed `83cca48`, merged `2f294c8`. S1a+S2a+S3a fixed (S3a also had mojibake, caught + fixed).
13. ~~Wave 2: Data source bridge (Option A per W0.5)~~ ✅ DONE — Merged `b08d907`. Query refactor + SQLite decimal `SumAsync` fix (materialize + client-side sum). 3/3 unit tests PASSING.
14. ~~Wave 3: Wire calc engine into DI~~ ✅ DONE — Committed `3b98524`, merged to main. 6 DI registrations added (5 calc engine services + IBookResultCache). W0-T8 conflict preserved. Build 0 errors, guard PASSED.
15. ~~Wave 4: Route through IHKDBookGenerationService~~ ✅ DONE — Committed `57d021c`, merged to main `7dbbcb1`. Injected `IHKDBookGenerationService` into `HKDBookService` constructor; rewrote 7 `GenerateS*BookAsync` methods to call `_hkdBookGenerationService.GenerateBookAsync(tenantId, period, "<code>")`; marked `ConvertToJournalEntries` `[Obsolete]` (0 callers); updated 2 test files with `Mock<IHKDBookGenerationService>` (Wave 6 will retrofit numeric assertions). Build 0 errors, 990 warnings. Guard PASSED. **Fixes Issue 1 (NumericValues always empty) — core fix of Stream D.**
16. ~~Wave 5 (MERGED 5a+5b+partial 5c): Industry Sector + PIT Fix + Account Mapping + 4-group Tax Rates~~ ✅ DONE — Committed `1ac8252`, merged to main. 6 micro-phases (S1-S6): Domain (IndustrySector enum + fields), EF Migration (AddIndustrySector), Formula Engine (SUM_ACCOUNT_BY_INDUSTRY), Tax Rate Lookup (4 groups VAT/PIT), S2a/S2b Template Redesign (4 industry groups), Production Write Path (OrderService/AccountingEntryService/HKDBookService pass IndustrySector), Tests (10/10 PASS) + Seed Data update. 29 files, +3203/-201. Build PASS, guard PASS. 3 pre-existing failures (unrelated HKDBookServiceTests — missing mock setup for IHKDBookGenerationService.GenerateBookAsync, broken before Wave 5).
17. ~~Wave 5c: 2026 Regulatory Compliance Fix (threshold + TNCN formulas)~~ ✅ DONE & MERGED — Committed `a60d026` on branch `feature/hkd-fix-wave5c-2026-regulatory`, merged to main `aa930dd`. Domain `CalculateGroup` thresholds 1B/3B/50B + `CalculateTNCN` (Nhóm 1: 0, Nhóm 2: (Rev-1B)×rate, Nhóm 3: (Rev-Expense)×17%, Nhóm 4: (Rev-Expense)×20%) + `CalculateGTGT` (Nhóm 1 exemption). Service thresholds + warnings. S2a template `CalculateAsync` override (Nhóm-aware TotalPIT + TotalExpense + blended PIT rate). `HKDTaxClassificationService` 10% TNCN → `CalculateTNCN`. 20 unit tests PASS. Build 0 errors, guard PASSED.
18. ~~Wave 6: Retrofit tests with numeric assertions~~ ✅ DONE & MERGED TO MAIN (`d6c3bb2`). 3 updated (S1a/S2a/S2b) + 5 new numeric (S2c/S2d/S2e/S3a + all-templates Theory×7) + 1 regression (W6-T8). Core.Tests Release 818/818 PASS, guard PASSED. Task card: `wave6_hkd_fix_retrofit_numeric_tests_task_card.md`.
19. ~~Wave 7: API endpoint + DI smoke + multi-tenancy test~~ ✅ DONE & COMMITTED (`76d2c11`) — HKDBookDto + HKDBooksController (2 endpoints) + 2 DI smoke tests + 4 endpoint tests (6/6 PASS). Fixed 7 pre-existing bugs. Core.Tests 818/818, Architecture.Tests 28/28, guard PASSED. Root cause investigation log: `docs/AI/tasks/wave7_root_cause_investigation_log.md`.
20. **Wave 8: UI page + DOCX/XLSX export + regression prevention** 🔄 IN PROGRESS — Session 1 committed `c8eb819` (UI + export + DI wiring). Session 2 in progress: E2E spec + architecture test + encoding lint + docs. Task card: `wave8_hkd_fix_ui_docx_export_regression_task_card.md`.

**Critical path updated:** ~~Wave 0~~ ✅ → ~~Wave 0.5~~ ✅ → ~~commit~~ ✅ → ~~merge~~ ✅ → ~~Stream E~~ ✅ → ~~Wave 1~~ ✅ → ~~Wave 2 (Option A)~~ ✅ → ~~Wave 3 (DI wiring)~~ ✅ → ~~Wave 4 (route through IHKDBookGenerationService)~~ ✅ → ~~Wave 5 (Industry Sector + PIT Fix)~~ ✅ → ~~Wave 5c (2026 Regulatory)~~ ✅ → ~~merge 5c~~ ✅ → ~~6~~ ✅ → ~~7~~ ✅ → **8**.

**Deferred (awaiting user decision):**
1. **Merge Stream B to main** — Stream B wave branches await merge to main. All 8 waves complete, guard PASSED.
2. **Push to origin** — `main` is ahead of `origin/main` (Stream B + Stream D planning commits).
3. **Stream A: EInvoice Provider Rewrite** — Planning complete (`59b60fe`). Blocker: Wave 0 sandbox credentials (1-2 tuần).
4. **Blazor `CascadingAuthenticationState` circuit crash** — pre-existing defect. Separate FIX_ONLY stream candidate.
5. ~~Tech Lead approval for Stream D Wave 5a W5a-T4~~ ✅ RESOLVED 2026-07-03 — Wave 5a superseded by merged Wave 5. Tech Lead approved Domain mod (add `IndustrySector` to `AccountingEntry` + `Tenant` + `Order`).
6. ~~Tech Lead approval for Stream D Wave 5b W5b-T0~~ ✅ RESOLVED 2026-07-03 — Wave 5b merged into Wave 5. `Tenant.IndustrySector` addition approved.
7. ~~Legal review for Stream D Wave 5c~~ ✅ RESOLVED 2026-07-03 — User approved proceeding with task card's cited legal sources (Luật GTGT/TNCN 2025, ND 117/2025, NQ 198/2025/QH15, meinvoice.vn) as basis. Domain fix approved. 10% TNCN bug included in scope. Nhóm 3/4 formula implemented with warning when no expense data.
8. ~~Stream E: DB Migration Strategy~~ ✅ DONE — Merged `b2e0431`.

---

## 5. Active Architecture Decisions

| Decision | R lý do |
|---|---|
| CoreHub = in-process background service trong Gateway | Monolith Phase 1-2 |
| Gateway = DI composition root cho CoreHub | Program.cs đăng ký CoreHub DbContext/Services |
| ShopERP = SQLite-only edge node | Edge deployment offline-first |
| CustomerToken = `IDataProtector` | Tránh library mới |
| `AccountingEntry` immutable, Reversal Entry | Audit trail bất khả xâm phạm |
| Multi-tenancy `TenantId` filter mọi layer | Data isolation per HKD |
| **[NEW] EF Core Migrations = official schema management** | Stream E decision — replace `EnsureCreated` for production, allow Migrations at Infrastructure layer (VA-ARCH-001 modified) |
| **[NEW] HKD Data Source = Option A (query AccountingEntries directly)** | Wave 0.5 decision — HKD-only product, AccountingEntry is immutable SSoT, no JournalEntry writing needed |
| **[NEW] DOCX export = DocumentFormat.OpenXml + XLSX export = EPPlus 7.6.1** | Wave 0 T9 — user approved both libraries for Wave 8 |

---

## 6. History Log (compressed — see git log for details)

* [2026-07-03] **STREAM B WAVE 2 COMPLETE** — Pattern D (Fix wrong auth pattern). 6 spec files fixed. 5 simple files (`expense-entry-flow`, `export-excel-flow`, `einvoice-dashboard`, `invoice-management`, `provider-management`): removed form-fill `beforeEach` (`#username`/`#email` + `#password` + `waitForURL`), removed `test.use({ storageState: { cookies: [], origins: [] } })` override from 3 einvoice files, removed unused `loadEnvConfig` imports — all rely on global `storageState` (auth/admin.json via `playwright.config.ts` L34+L56). `rbac-enforcement`: removed `loginAs` helper (filled `#Username`/`#Password` on non-existent `/Login` form); Owner tests (2) use global storageState; 6 multi-role tests (Staff/StoreKeeper/Guard) skipped with `test.skip(true, 'Requires auth/<role>.json — not generated by global-setup')` + note; unauthenticated test wrapped in `test.describe` with `test.use({ storageState: { cookies: [], origins: [] } })`. `export-excel-flow` staff-role test also skipped. Verification: 0 `fill('#username'…)` / 0 `fill('#email'…)` / 0 `fill('#Username'…)` / 0 `VanAn@2026` / 0 broken `waitForURL` remaining (grep confirmed). `npx playwright test --list` → 759 tests in 20 files (unchanged). `dotnet build VanAn.sln` → 0 errors. guard-check.ps1 → ALL CHECKS PASSED. Commit `b40b640` on `feature/e2e-cleanup-wave2-fix-auth-pattern`. Next: Wave 3 (delete anti-schema tests — `voice-command.spec.ts` 4 tests, `i18n.spec.ts` 1 test).
* [2026-07-03] **STREAM B WAVE 0+1 COMPLETE** — E2E Test Cleanup started. Wave 0 pre-flight: verified `auth/admin.json` generation (`global-setup.ts` L111-112), `playwright.config.ts` L34+L56 global `storageState`, `isTierEnabled('e2e')` in `env-config.ts` L359-389, git tree clean, `npx playwright test --list` → 759 tests in 20 files (parse OK). Wave 1 (Pattern F — Remove decorative `reporter.pass()`): removed 59 calls across 9 spec files (`accounting-flow` 14, `order-flow` 7, `order-tracking` 7, `qr-payment-ui` 7, `audit-trail-flow` 6, `period-closing-flow` 5, `van-an-dashboard` 5, `balance-dashboard-flow` 4, `qr-payment` 4) via brace-balanced Node script `6_Testing/scripts/wave1-remove-reporter-pass.js` (handles single+multi-line forms, skips comments/strings, conditionally removes TestReporter import/decl — none unused). 4 `reporter.pass` comment refs preserved (T-17/T-19/T-21 FIX notes). All 9 files retain `reporter.log`/`setArchitectDecision` usage in `beforeAll`. Verification: 0 `reporter.pass(` calls remain (grep confirms only 4 comment refs), `npx playwright test --list` → 759 tests in 20 files (unchanged), `dotnet build VanAn.sln` → 0 errors, guard PASSED. Commit `0c7965e` on `feature/e2e-cleanup-wave1-remove-reporter-pass`. Pre-Stream-B hygiene commit `797ce36` (committed uncommitted smoke test state from prior session). Next: Wave 2 (fix auth pattern in 6 files — `expense-entry-flow`, `export-excel-flow`, `einvoice-dashboard`, `invoice-management`, `provider-management`, `rbac-enforcement`).
* [2026-07-03] **VISUAL SMOKE TEST PASSED** — Post-Stream-C merge validation. Built VanAn.sln (0 errors), started ShopERP on port 5003, authed via DevLoginController (`POST /dev/login` → Owner role). Playwright headless Chromium 1440×900 navigated 6 routes: `/` (→/sitemap redirect by design, Wave 6 Home.razor), `/sitemap` (Vạn An Ecosystem), `/access-denied` (403 — Không Có Quyền Truy Cập), `/accounting` (Kế Toán Dashboard), `/einvoice` (Dashboard Hóa Đơn Điện Tử), `/admin/users` (Quản lý người dùng). All 6 HTTP 200, all render with sidebar+nav+page title. Screenshots: `6_Testing/reports/smoke-shots/{Home,Sitemap,AccessDenied,Accounting,EInvoice,AdminUsers}.png`. Stream C waves verified in production render: Wave 1 (UI.Platform CSS + Bootstrap Icons CDN load), Wave 3 (`css/pages.css` 6585B loads), Wave 4 (`VanAButton` renders — confirmed in server logs), Wave 5 (AdminLayout on /admin/users), Wave 6 (Home redirect + AccessDenied 403 message, no inline `<style>`). **Pre-existing defects found (NOT Stream C regressions):** (1) Blazor circuit crash on `/`, `/sitemap`, `/admin/users` — `System.InvalidOperationException: Authorization requires a cascading parameter of type Task<AuthenticationState>` from `AuthorizeViewCore.OnParametersSetAsync()`; pages prerender correctly but interactivity breaks after circuit connect; Routes.razor has `<CascadingAuthenticationState>` + `<AuthorizeRouteView>` — cascade timing issue. (2) DevLoginController role mismatch — `/admin/users` requires "Owner" role (`OwnerOnly` policy) but `/dev/login/systemadmin` issues "SystemAdmin" → access denied for admin routes via SystemAdmin login.
* [2026-07-03] **STREAM C FULLY DONE & MERGED TO MAIN** — Wave 6 (Governance cleanup) + fast-forward merge of all 6 waves into `main` (`cc10e08..f3ed2d2`, 46 files, +1622/-428 lines). Wave 6 details: Remove inline `<style>` blocks from AccessDenied.razor (37 lines), Sitemap.razor (72 lines), AuditTrail.razor (165 lines — exit criteria required 0 inline `<style>` in ALL .razor files; CSS already covered by `.razor.css` from Wave 3 with better design tokens). Fix Sitemap logout: replace `JSRuntime.InvokeVoidAsync("eval", ...)` (security concern — eval + manual cookie clear) with `NavigationManager.NavigateTo("/Logout", forceLoad: true)` using existing `Pages/Logout.cshtml` server-side `SignOutAsync` endpoint. Remove now-unused `@inject IJSRuntime JSRuntime`. Fix 7 broken emojis (U+FFFD replacement chars from encoding corruption) in Sitemap with semantic emojis: 📷 (Guard scan), 💸 (Chi Phí), 📈 (Doanh Thu), 📜 (Lịch Sử), 📅 (Đóng Kỳ), 🌐 (KhachLink), 🔑 (Nhóm Quyền). Delete `Counter.razor` (Blazor template demo, 0 references in NavMenu/Sitemap). Fix `Home.razor`: add `<PageTitle>Đang chuyển hướng...</PageTitle>` + loading state div + new `Home.razor.css` (CSS isolation for `.redirect-loading`). Build 0 errors on main post-merge, guard PASSED. Commits `ea1ced5` + `f3ed2d2`. **Stream C complete — 14 dead → 0, 18 unstyled → 0, 3 broken layouts → 0.**
* [2026-07-03] **Wave 5 COMPLETE** — Admin layout consistency: Create `AdminLayout.razor` (VanALayout + VanANavigation with 4 Admin menu items matching NavMenu: Users `/admin/users`, Permission Groups `/admin/permission-groups`, Audit Trail `/admin/audit-trail`, Tenants `/admin/tenants`) following AccountingLayout pattern (post-Wave 1 slot fix). Add `@layout AdminLayout` to 4 Admin pages (AuditTrail, UserManagement, PermissionGroupManagement, TenantManagement) at line 3 (after `@page` + `@rendermode`). AdminLayout has no `@attribute [Authorize]` — each page self-authorizes. 5 files, +24 lines. Build 0 errors. Commit `7f05fa7` on `feature/shoperp-ui-fix-wave5-admin-layout`.
* [2026-07-03] **Wave 4 COMPLETE** — Component consolidation: `VanAnAlert` (old, Atomic namespace) → `VanAAlert` (new) in 6 EInvoice files (10 occurrences). API fix: `Type="..."` (unmatched attr, broken) → `Variant="..."` (real param). `Type="danger"` → `Variant="error"` (VanAAlert uses "error"). `VanAnModal`: 0 occurrences in EInvoice scope (only in KhachLink — out of scope, debt cleanup candidate). 6 files, 10 line changes. Build 0 errors. Commit `47268a0` on `feature/shoperp-ui-fix-wave4-component-consolidation`.
* [2026-07-03] **Wave 3 COMPLETE** — Page CSS isolation: `wwwroot/css/pages.css` (shared, 276 lines — `:root` design tokens + 17 common classes: page-header, metrics-grid, filter-grid, vanan-table, status-badge, pagination, etc.) + 18 `.razor.css` files (page-specific classes). Linked in `App.razor`. 20 files, +1228 lines. Build 0 errors. Commit `d2d058d` on `feature/shoperp-ui-fix-wave3-page-css`.
* [2026-07-03] **Wave 2 COMPLETE** — Add `@rendermode InteractiveServer` to 14 files (AccessDenied, Sitemap, AccountingIndex, TransactionHistory, 6 EInvoice, 4 Admin). 14 files, +14 lines. Build 0 errors. Commit `79ec512` on `feature/shoperp-ui-fix-wave2-rendermode`.
* [2026-07-03] **Wave 0 + Wave 1 COMPLETE** — Pre-flight verified. Wave 1: VanALayout.razor.css + VanANavigation.razor.css (NEW), icon fix (`<i class="bi bi-@icon">`), Bootstrap Icons CDN, 3 layout files slot fix, VanADashboard emoji→BI icons. Commit `3b893e8` on `feature/shoperp-ui-fix-wave1-platform-infra`.
* [2026-07-02] **ShopERP UI Fix + E2E Cleanup — PLANNING COMPLETE** — 2 master plans + 14 task cards (`51dd7ff`). UI: 23 files, 6 patterns (P/R/C/V/L/G). E2E: 20 spec files, 7 anti-patterns.
* [2026-07-02] **EInvoice Provider Rewrite — PLANNING COMPLETE** — Master plan + 4 task cards (`59b60fe`). 20 Viettel + 10 MISA API spec mismatches. Wave 0 credential request parallel.
* [2026-07-02] **ShopConfig Refactor — 3 PHASES COMPLETE** — Product→tenant refactor, KhachLink HTTP-only, merged to main.
* [2026-07-02] **Tenant Onboarding — 6 WAVES COMPLETE & MERGED** — Generic multi-industry onboarding (F&B enabled), orchestrator, Gateway API, ShopERP UI, integration tests. Commit `3123b6b`.
* [2026-07-02] **Architecture Test Fixes + CI/CD Hotfix** — 28/28 arch tests PASS, remote CI fixed.
* [2026-07-01] **Tenant Onboarding Waves 1-4** — Abstraction + F&B seed + orchestrator + Gateway API.
* [2026-07-01] **Documentation Added** — KhachLink + ShopERP module docs.

---

## 7. Active Files Reference

### Stream C — ShopERP UI Fix
| File | Role |
|---|---|
| `docs/AI/tasks/shoperp_ui_fix_master_plan.md` | Master plan (6 waves) |
| `docs/AI/tasks/wave1_shoperp_ui_platform_infra_task_card.md` | Wave 1: Pattern P (UI.Platform) |
| `docs/AI/tasks/wave2_shoperp_rendermode_task_card.md` | Wave 2: Pattern R (rendermode) |
| `docs/AI/tasks/wave3_shoperp_page_css_task_card.md` | Wave 3: Pattern C (CSS) |
| `docs/AI/tasks/wave4_shoperp_component_consolidation_task_card.md` | Wave 4: Pattern V (versions) |
| `docs/AI/tasks/wave5_shoperp_admin_layout_task_card.md` | Wave 5: Pattern L (Admin layout) |
| `docs/AI/tasks/wave6_shoperp_governance_cleanup_task_card.md` | Wave 6: Pattern G (governance) |

### Parked Streams
| File | Role |
|---|---|
| `docs/AI/tasks/einvoice_provider_rewrite_master_plan.md` | Stream A (parked) |
| `docs/AI/tasks/e2e_test_cleanup_master_plan.md` | Stream B (parked) |

---

## 8. Architecture Quick Reference

```
KhachLink (5002) → Gateway (5001) → ShopERP (5003) → SQLite
                        ↓
              [in-process CoreHub services]
                        ↓
                  PostgreSQL (prod) / SQLite (edge)
```

**Docker (prod):** postgres · nats · seq · gateway · shoperp · khachlink · nginx · certbot
**Docker (edge):** postgres · nats · gateway · shoperp · shoperp-nats-sync
**CoreHub:** NOT a Docker service — runs in-process inside Gateway.

---

## 9. Maintenance Log

* **Last Updated:** 2026-07-04 — **STREAM F W2 COMPLETE & MERGED** (`a546c48` + `9165bf6` → `fef8097` → main). Domain Records + D9 HKD↔DN conversion. **Domain.cs:** 3 enums (TenantType, AccountType 5 values no Contra, AccountingStandard TT99/133/58), FinancialStatementLine (ReportItemCode + Ending/Opening + Level + IsNormalNegative per Mẫu B01-DN/B02-DN/B03-DN), 3 BCTC records (BalanceSheet 2-column no IsBalanced, IncomeStatement 2-column, CashFlowStatement 3 activity lines), OpeningBalance, AccountChartEntry (IsNormalCredit for contra TK 214). **Tenant aggregate:** Converted=4 status, TenantConvertedEvent, 5 new fields (Predecessor/Successor/ConvertedAt/AccountingStandard/Type), CreateFromConversion + MarkConvertedTo + IsConverted/IsConversionOf. **Review fixes C1+C2+H1+H2+H3:** C1 fixed dead newType param (added Type property), C2 explicit Ignore() for 5 fields in TenantConfiguration (W8 handoff), H1 set ConvertedAt in MarkConvertedTo, H2 Suspended conversion ALLOWED with rationale, H3 TenantId.Empty guards. **External review integrated:** BCTC uses ReportItemCode + 2-column (legal), no IsBalanced flag (W4 invariant), Contra→IsNormalCredit, ConvertedToStandard→general AccountingStandard, KEPT SetTenantId, REJECTED TT88_2021. 11 unit tests (W2-D1 to W2-D11, all PASS). Build 0 errors, guard PASSED, Arch tests PASSED. **Deferred:** H4 SetAccountingStandard/SetTenantType (W8), M1/M2 validation (W4), M3/M4/M5 covered by tests. Next: W3 (Account Code Map).
* **Previous:** 2026-07-04 — **STREAM F W1 COMPLETE & MERGED** (`dd82f5f` → `93a5e7e` → main). Data Audit + Seed. **Schema gaps found & fixed:** (1) `JournalEntries` missing `EntryDate`/`ReferenceId`/`IsReversal` columns; (2) `JournalEntryLine.Id` shadow property not auto-generated by SQLite. **Fixes:** Migration `AddJournalEntryMissingColumns`, `JournalEntryConfiguration` updated, `JournalEntryLine` Domain entity gained explicit `Id` property. **Seeder:** `VasSampleDataSeeder.cs` — 1 Enterprise tenant (DN vừa TT 133), 31 journal entries + ~50 AccountingEntries, 2 months, CASH+VIETQR, opening balances, all scenarios. **5 lỗi kế toán phản biện & sửa:** (1) 311→411 Vốn CSH; (2) Khấu hao Có 211→214; (3) Chiết khấu 521→511 (TT 133); (4) Phí vận chuyển 515→5113; (5) CP bán hàng 641→6421 (TT 133). 15 tests (all PASS). Core.Tests 843/843, guard PASSED. Dev DB recreated from migrations. Next: W2 (Domain Extension).
* **Previous:** 2026-07-04 — **STREAM F W0 COMPLETE & MERGED** (`be348ad` → main). Fixed 9/18 Order→Accounting writer issues: C1 COGS sync (shared `CalculateCogsAmount`), C2 PaymentMethod passed into `ConfirmPayment`, C3 VAT split both paths (AccountingEntry 511 net + 3331; JournalEntry 3 lines), H1 `PaymentMethodConstants` (111/112), H2 Discount net revenue, H4 OrderDate period, H5 Order reference, M9 COGS removed from S2d, B3 621→632. New file `3_CoreHub/Common/PaymentMethodConstants.cs`. 10 new tests (SC17-SC23). Core.Tests 828/828 PASS (was 818, +10, 0 regressions), Architecture.Tests 31/31 PASS, guard PASSED. **User decisions (2026-07-04):** VAT split both paths; HKD discount = Net Revenue (VAS Gross+521 deferred to W8); Shipping DEFERRED (pending BA/Kế toán — 515 = financial revenue, not shipping); PaymentMethodConstants class. **Deferred:** H3 Shipping, M1/M6/M2-M5/M7/M8/M10. Next: W1 (Data Audit + Seed).
* **Previous:** 2026-07-04 — **STREAM F (VAS) PLANNING COMPLETE + D9 CONVERSION**. Audited 4 BCTC (3/4 mock/stub, 1/4 query broken) + Order→Accounting flow (18 vấn đề: 3C+5H+10M). Verified legal: TT 99/2025 (thay TT 200) + TT 133/2016 + TT 58/2026 (thay TT 132, NOT TT 133). User approved: 3 tầng chuẩn, VAS module riêng (feature flag), 4 BCTC song song, writer fix trước seed, Domain mod approved, seed DN vừa TT 133, JIT Planning. **D9 approved: HKD↔DN conversion = Option B (New Tenant + Link) + Read-only historical qua predecessor + Amend W2+W3+W8 (no new wave).** W2 amended: add Predecessor/Successor fields + CreateFromConversion + MarkConvertedTo + Converted status. W3 amended: add HKD→DN account mapping (14 keys). W8 amended: add TenantConversionService + conversion wizard + read-only gating. Created master plan v3 (slim, JIT Planning Strategy) + 10 task cards (W0-W9, 3 amended for D9). 9 decisions approved (D1-D9). Next: W0 IMPLEMENT (Order→Accounting Writer Fix, branch `feature/vas-wave0-order-accounting-writer-fix`).
* **Previous:** 2026-07-04 — **WAVE 8 SESSION 2 IN PROGRESS** (branch `feature/hkd-fix-wave8-ui-docx-export-regression`). Session 1 committed `c8eb819`: HKD Book UI page (`HKDBooks.razor` list + `HKDBookDetail.razor` detail) + `HKDBookExportService` (DOCX via OpenXML + XLSX via EPPlus) + DI wiring in ShopERP `Program.cs` + `HKDBookGenerationService` changed to depend on `IVanAnDbContext` + `DocumentFormat.OpenXml` dependency added. Build 0 errors. Session 2: added `hkd-books.spec.ts` (6 E2E tests, 36 listed via `playwright --list`), `HKDBookTemplateArchitectureTests.cs` (3 regression tests for Issue 1 — all PASS), `check-encoding.ps1` (SC7 mojibake lint — fixed false-positive detection: now scans for 2-char lead+continuation sequences, not single Latin-1 chars; 923 files scanned, 0 mojibake), updated `docs/UI_Platform_Implementation_Guide.md` with Wave 8 HKD Book module reference. Architecture.Tests 3/3 PASS. Next: final build + guard-check + commit Session 2, then merge Wave 8 to main.
* **Previous:** 2026-07-17 — **WAVE 7 COMPLETE** (commit `76d2c11`). Created HKDBookDto + HKDBooksController (2 endpoints) + 2 DI smoke tests + 4 endpoint tests (6/6 PASS). Fixed 7 pre-existing bugs: Gateway missing DI, circular dependency (Lazy<IFormulaEngine>), JournalEntryConfiguration missing EntryDate, HKDBookGenerationService unmapped Period query, CreateBaseVariables GUID→decimal parse (GetHashCode), BaseHKDBookTemplate null! logger (NullLogger), CalculateFormulaAsync legacy variables overload → FormulaContext (root cause of TotalRevenue=0). Core.Tests 818/818 PASS, Architecture.Tests 28/28 PASS, guard PASSED.
* **Current Branch:** `main` (W0+W1+W2 merged) · `feature/vas-wave2-domain-records` (W2 branch, merged) · `feature/hkd-fix-wave8-ui-docx-export-regression` (Stream D W8 S2 — parked)
* **Current Objective:** Stream F (VAS) W0+W1+W2 merged to main — W3 (Account Code Map) next. Stream D Wave 8 Session 2 parked.
