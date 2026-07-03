# MASTER IMPLEMENTATION PLAN — HKD Book Accounting Report Fix (TT 152/2025/TT-BTC Compliance)

> **Status:** ✅ APPROVED (v2) — Awaiting Wave 0 execution
> **Created:** 2026-07-03
> **Last Updated:** 2026-07-03 (v2 — 10 waves, 8 root-cause issues, 3 amendments + 5 concerns resolved)
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

### Branch protocol (v2 — per-wave merge to main, always-green)
```
main ← feature/hkd-fix-wave0-preflight          (merge after W0 verify pass)
main ← feature/hkd-fix-wave1-encoding-mojibake  (merge after build+guard pass)
main ← feature/hkd-fix-wave2-bridge-journal-persistence
main ← feature/hkd-fix-wave3-wire-calc-engine-di
main ← feature/hkd-fix-wave4-route-through-generation-service
main ← feature/hkd-fix-wave5a-account-mapping-pit-fix
main ← feature/hkd-fix-wave5b-industry-sector-tax-rates  (CONDITIONAL — may descope)
main ← feature/hkd-fix-wave6-retrofit-numeric-tests
main ← feature/hkd-fix-wave7-api-endpoint-di-smoke
main ← feature/hkd-fix-wave8-ui-docx-export-regression
```
- **Mỗi wave là branch riêng, branch từ `main` mới nhất, merge ngược về `main` sau khi pass** (build + guard + test)
- **`main` luôn green** — không có long-lived branch 14-session
- **Branch wave kế tiếp phải rebase/pull từ `main` trước khi bắt đầu** (đảm bảo có fix wave trước)
- **Wave 5b có thể skip** (descope) mà không ảnh hưởng wave 6-8 (5b không block — 5a đã fix PIT + account mapping)
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

## 1. CURRENT ISSUES SUMMARY (Root Causes)

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
- `VatAmount = TotalRevenue * 0.05` — cứng 5%. TT 152 có nhiều tỷ lệ GTGT theo ngành nghề (1%; 1,5%; 2%; 2,5%; 3%; 5%).
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
**Task Card:** `docs/AI/tasks/wave1_hkd_fix_encoding_mojibake_task_card.md`

### Tasks
| # | Task ID | Task | Files | Status |
|---|---|---|---|---|
| 1 | W1-T1 | Fix mojibake trong `S1aHKDTemplateImpl` constructor (TemplateName, DisplayName, GenerateReportAsync header) | `3_CoreHub/Services/Template/TemplateFactory.cs` (L114-175) | ⏳ PENDING |
| 2 | W1-T2 | Fix mojibake trong `S2aHKDTemplateImpl` constructor + GenerateReportAsync | `3_CoreHub/Services/Template/TemplateFactory.cs` (L177-249) | ⏳ PENDING |
| 3 | W1-T3 | Verify S2b-S3a TemplateImpl không có mojibake (đã OK — chỉ verify) | `3_CoreHub/Services/Template/TemplateFactory.cs` (L251-658) | ⏳ PENDING |
| 4 | W1-T4 | `dotnet build VanAn.sln` Release pass | Solution-wide | ⏳ PENDING |

### Entry criteria
- [ ] Wave 0 complete (verify môi trường)
- [ ] Git status clean

### Exit criteria
- [ ] 0 mojibake string còn lại trong `Services/Template/TemplateFactory.cs` (grep `Ã|Â|á»|áº|á»|Ä` — no matches)
- [ ] `dotnet build VanAn.sln` Release — 0 errors
- [ ] guard-check.ps1 PASSED
- [ ] Committed on `feature/hkd-fix-wave1-encoding-mojibake`

### Why first
- Risk thấp nhất — chỉ sửa string literal, không thay đổi logic
- Block Wave 3 (wire DI) — nếu không fix, báo cáo sẽ có ký tự rác
- Độc lập với data flow — có thể làm song song Wave 0/Wave 2

---

## 3. WAVE 2 — Bridge AccountingEntry → JournalEntry Persistence

**Branch:** `feature/hkd-fix-wave2-bridge-journal-persistence`
**Estimated sessions:** 1-2
**Conflict risk:** MEDIUM (thay đổi write path)
**Priority:** 2 (Critical — block Wave 3/4/5/6 — calc engine cần data)
**Task Card:** `docs/AI/tasks/wave2_hkd_fix_bridge_journal_persistence_task_card.md`

### Tasks
| # | Task ID | Task | Files | Status |
|---|---|---|---|---|
| 1 | W2-T1 | Modify `HKDBookService.RecordRevenueAsync` — sau khi persist `AccountingEntry`, tạo + persist `JournalEntry` với double-entry lines (Dr 111/Cash, Cr 511/Revenue) dùng `IHKDBookRepository.AddToBookAsync` | `3_CoreHub/Services/HKDBookService.cs` (L43-71) | ⏳ PENDING |
| 2 | W2-T2 | Modify `HKDBookService.RecordExpenseAsync` — cùng pattern (Dr 611/Expense, Cr 111/Cash) | `3_CoreHub/Services/HKDBookService.cs` (L73-101) | ⏳ PENDING |
| 3 | W2-T3 | Verify `JournalEntry.AddLine(accountNumber, debit, credit, description)` API — confirm signature + immutability | `1_Shared/Domain/JournalEntry.cs` (READ) | ⏳ PENDING |
| 4 | W2-T4 | Verify `IHKDBookRepository.AddToBookAsync` — confirm persist `JournalEntry` + assign `AccountingBookType` | `3_CoreHub/Repositories/HKDBookRepository.cs` (L135-154) | ⏳ PENDING |
| 5 | W2-T5 | Add unit test: `RecordRevenueAsync_ShouldPersistJournalEntry_WithCorrectDoubleEntryLines` — assert JournalEntry có 2 lines, Dr 111 = Cr 511 = amount | `6_Tests/VanAn.Core.Tests/Accounting/HKDBookServiceTests.cs` | ⏳ PENDING |
| 6 | W2-T6 | Add unit test: `RecordExpenseAsync_ShouldPersistJournalEntry_WithCorrectDoubleEntryLines` — assert Dr 611 = Cr 111 = amount | `6_Tests/VanAn.Core.Tests/Accounting/HKDBookServiceTests.cs` | ⏳ PENDING |
| 7 | W2-T7 | `dotnet build VanAn.sln` Release + `dotnet test` pass | Solution-wide | ⏳ PENDING |

### Entry criteria
- [ ] Wave 1 merged
- [ ] `IHKDBookRepository.AddToBookAsync` confirmed persist JournalEntry (Wave 0 verify)
- [ ] **W0-T11 double-write audit complete** — write-path map documented in Wave 2 task card

### Double-write decision tree (Concern 1 resolution)
Based on W0-T11 audit results, choose path BEFORE implementing W2-T1/T2:

| W0-T11 Finding | Decision | Action |
|---|---|---|
| **A.** No existing code persists `JournalEntry` (table empty, `AddToBookAsync` never called) | ✅ SAFE to bridge | Implement W2-T1/T2 as specified — add `JournalEntry` persistence in `RecordRevenue/Expense` |
| **B.** Existing path persists `JournalEntry` from a DIFFERENT source (not `AccountingEntry`) | ✅ SAFE but verify no overlap | Implement W2-T1/T2, add guard: check if `JournalEntry` with same `ReferenceId` already exists before persisting (idempotent) |
| **C.** Existing path persists `JournalEntry` from `AccountingEntry` (rare — would mean audit missed something) | ⚠️ STOP — potential double-write | Do NOT add new persistence. Instead, verify existing path produces correct double-entry lines. If lines are wrong (single-entry), fix existing path rather than adding new. Report to user before proceeding. |
| **D.** `JournalEntries` table has data but source unknown | ⚠️ INVESTIGATE | Query sample rows, trace `TenantId` + `ReferenceId` back to source code. Resolve to A/B/C before proceeding. |

**If decision = C or D → STOP and report to user.** Do not guess.

### Exit criteria
- [ ] `RecordRevenueAsync` persist cả `AccountingEntry` (immutable) + `JournalEntry` (double-entry)
- [ ] `RecordExpenseAsync` persist cả `AccountingEntry` + `JournalEntry`
- [ ] `JournalEntry` có 2 lines: 1 Debit + 1 Credit, tổng Debit = tổng Credit = amount
- [ ] Account numbers đúng: Revenue → Dr 111, Cr 511; Expense → Dr 611, Cr 111
- [ ] **If W0-T11 = B: idempotent guard present** (no duplicate JournalEntry for same ReferenceId)
- [ ] 2 unit test pass (verify double-entry lines)
- [ ] `dotnet build VanAn.sln` Release — 0 errors
- [ ] guard-check.ps1 PASSED

### Why second
- Block tất cả wave sau — calc engine query `JournalEntries`, nếu bảng rỗng thì SUM_ACCOUNT = 0
- Risk medium — thay đổi write path, cần đảm bảo không break `AccountingEntry` immutability
- Phải làm trước Wave 3 (wire DI) để có data test
- **Decision tree (W0-T11 prereq) loại bỏ risk double-write** — không guess, verify trước

---

## 4. WAVE 3 — Wire Calculation Engine into DI

**Branch:** `feature/hkd-fix-wave3-wire-calc-engine-di`
**Estimated sessions:** 1
**Conflict risk:** LOW (chỉ thêm DI registration)
**Priority:** 3 (Critical — block Wave 4)
**Task Card:** `docs/AI/tasks/wave3_hkd_fix_wire_calc_engine_di_task_card.md`

### Tasks
| # | Task ID | Task | Files | Status |
|---|---|---|---|---|
| 1 | W3-T1 | Register `ProductionFormulaEngine` as `IFormulaEngine` (Scoped) trong `3_CoreHub/Program.cs` | `3_CoreHub/Program.cs` | ⏳ PENDING |
| 2 | W3-T2 | Register `ScopedDataProvider` as `IDataProvider` (Scoped — cần `IMemoryCache`, `IPreAggregationService`, `IBookResultCache`) | `3_CoreHub/Program.cs` | ⏳ PENDING |
| 3 | W3-T3 | Register `SmartPreAggregationService` as `IPreAggregationService` (Scoped — cần `VanAnDbContext`, `IFormulaEngine`) | `3_CoreHub/Program.cs` | ⏳ PENDING |
| 4 | W3-T4 | Register `TemplateFactory` (mới, `Services/Template/`) as self (Scoped — cần `IFormulaEngine`, `IDataProvider`, `ILoggerFactory`) | `3_CoreHub/Program.cs` | ⏳ PENDING |
| 5 | W3-T5 | Register `HKDBookGenerationService` as `IHKDBookGenerationService` (Scoped — cần `VanAnDbContext`, `TemplateFactory`, `IBookResultCache`, `ILogger`) | `3_CoreHub/Program.cs` | ⏳ PENDING |
| 6 | W3-T6 | Verify `IBookResultCache` đã đăng ký (grep — nếu chưa, đăng ký) | `3_CoreHub/Program.cs` | ⏳ PENDING |
| 7 | W3-T7 | Verify `IMemoryCache` đã đăng ký (`AddMemoryCache()` — nếu chưa, thêm) | `3_CoreHub/Program.cs` | ⏳ PENDING |
| 8 | W3-T8 | Resolve conflict `ITemplateFactory` — hiện đăng ký bản cũ `Services/TemplateFactory.cs`; bản mới `Services/Template/TemplateFactory.cs` là class khác. Quyết định: giữ bản cũ cho `OrderService`, thêm bản mới với tên riêng hoặc refactor. | `3_CoreHub/Program.cs` | ⏳ PENDING |
| 9 | W3-T9 | `dotnet build VanAn.sln` Release pass | Solution-wide | ⏳ PENDING |

### Entry criteria
- [ ] Wave 2 merged (JournalEntries có data)
- [ ] Wave 1 merged (TemplateFactory mới không còn mojibake)

### Exit criteria
- [ ] 5 service mới đăng ký DI: `ProductionFormulaEngine`, `ScopedDataProvider`, `SmartPreAggregationService`, `TemplateFactory` (mới), `HKDBookGenerationService`
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
**Task Card:** `docs/AI/tasks/wave4_hkd_fix_route_through_generation_service_task_card.md`

### Tasks
| # | Task ID | Task | Files | Status |
|---|---|---|---|---|
| 1 | W4-T1 | Inject `IHKDBookGenerationService` vào `HKDBookService` constructor | `3_CoreHub/Services/HKDBookService.cs` (L13-20) | ⏳ PENDING |
| 2 | W4-T2 | Rewrite `GenerateS1aBookAsync` — thay `new S1aHKDTemplate()` + `ConvertToJournalEntries` bằng `_hkdBookGenerationService.GenerateBookAsync(tenantId, period, "S1a_HKD")` | `3_CoreHub/Services/HKDBookService.cs` (L460-486) | ⏳ PENDING |
| 3 | W4-T3 | Rewrite `GenerateS2aBookAsync` — cùng pattern, templateCode `"S2a_HKD"` | `3_CoreHub/Services/HKDBookService.cs` (L488-514) | ⏳ PENDING |
| 4 | W4-T4 | Rewrite `GenerateS2bBookAsync` — templateCode `"S2b_HKD"` | `3_CoreHub/Services/HKDBookService.cs` (L516-542) | ⏳ PENDING |
| 5 | W4-T5 | Rewrite `GenerateS2cBookAsync` — templateCode `"S2c_HKD"` | `3_CoreHub/Services/HKDBookService.cs` (L544-570) | ⏳ PENDING |
| 6 | W4-T6 | Rewrite `GenerateS2dBookAsync` — templateCode `"S2d_HKD"` | `3_CoreHub/Services/HKDBookService.cs` (L572-598) | ⏳ PENDING |
| 7 | W4-T7 | Rewrite `GenerateS2eBookAsync` — templateCode `"S2e_HKD"` | `3_CoreHub/Services/HKDBookService.cs` (L600-626) | ⏳ PENDING |
| 8 | W4-T8 | Rewrite `GenerateS3aBookAsync` — templateCode `"S3a_HKD"` | `3_CoreHub/Services/HKDBookService.cs` (L628-654) | ⏳ PENDING |
| 9 | W4-T9 | Mark `ConvertToJournalEntries` as obsolete hoặc xóa nếu không còn dùng (grep verify) | `3_CoreHub/Services/HKDBookService.cs` (L718-751) | ⏳ PENDING |
| 10 | W4-T10 | `dotnet build VanAn.sln` Release pass | Solution-wide | ⏳ PENDING |
| 11 | W4-T11 | **[AMENDMENT 3] Add smoke test `GenerateS1aBook_NumericValues_ShouldNotBeEmpty_Tripwire`** — seed minimal JournalEntries (1 revenue entry), call `GenerateS1aBookAsync`, assert `result.NumericValues` NOT empty AND `result.NumericValues["TotalRevenue"] > 0`. This is a tripwire — if it fails, Wave 4 routing is broken and Wave 5/6 cannot proceed. Do NOT wait for Wave 6 full retrofit. | `6_Tests/VanAn.Core.Tests/Services/HKDBookServiceTests.cs` (NEW — minimal) | ⏳ PENDING |

### Entry criteria
- [ ] Wave 3 merged (IHKDBookGenerationService đã đăng ký DI)
- [ ] Wave 2 merged (JournalEntries có data)

### Exit criteria
- [ ] 7 method `GenerateS*BookAsync` gọi `_hkdBookGenerationService.GenerateBookAsync` thay vì `new S*HKDTemplate()`
- [ ] `ConvertToJournalEntries` không còn dùng (hoặc xóa)
- [ ] **Smoke test W4-T11 pass** — `NumericValues` không rỗng cho S1a (tripwire before Wave 5)
- [ ] `dotnet build VanAn.sln` Release — 0 errors
- [ ] guard-check.ps1 PASSED

### Why fourth
- Sau Wave 3 (DI) — `IHKDBookGenerationService` đã có sẵn để inject
- Đây là wave fix Issue 1 cốt lõi — `NumericValues` sẽ có số liệu
- Risk medium — thay đổi 7 method production, cần đảm bảo backward compat
- **Smoke test (W4-T11) là tripwire** — phát hiện routing bug ngay, không đợi đến Wave 6 (governance retrofit-TDD: EXISTING code phải có test trước khi hoàn thành)

---

## 6. WAVE 5a — Fix Account Mapping + PIT-on-Revenue (No Domain mod, no industry modeling)

**Branch:** `feature/hkd-fix-wave5a-account-mapping-pit-fix`
**Estimated sessions:** 1
**Conflict risk:** MEDIUM (thay đổi service logic + 1 Domain account-number fix)
**Priority:** 5a (High — compliance pháp lý, nhưng không cần modeling ngành nghề)
**Task Card:** `docs/AI/tasks/wave5a_hkd_fix_account_mapping_pit_task_card.md` (NEW — split from old wave5 card)

### Tasks
| # | Task ID | Task | Files | Status |
|---|---|---|---|---|
| 1 | W5a-T1 | Fix `_vietnameseAccounts` dictionary — sửa 211, 811, 821, 841, 521; thêm 311 (vay ngắn hạn), 333 (thuế phải nộp), 5118 (doanh thu dịch vụ) | `3_CoreHub/Services/HKDBookService.cs` (L22-41) | ⏳ PENDING |
| 2 | W5a-T2 | Sửa `S2aHKDTemplateImpl` — `PersonalIncomeTax` tính trên `TotalRevenue` (không phải `VatAmount`). Giữ `VatAmount = TotalRevenue * defaultRate` tạm thời (defaultRate = 5% hoặc rate từ W0-T10 finding) — industry-specific rates sang Wave 5b. | `3_CoreHub/Services/Template/TemplateFactory.cs` (L177-249) | ⏳ PENDING |
| 3 | W5a-T3 | Sửa `S2bHKDTemplateImpl` — thay account `"521"` (sai) bằng `"5118"` cho doanh thu dịch vụ | `3_CoreHub/Services/Template/TemplateFactory.cs` (L251-313) | ⏳ PENDING |
| 4 | W5a-T4 | Sửa `S2bHKDTemplate` (Domain) — cùng fix account `"512"` → `"5118"` | `1_Shared/Domain/HKDTemplates.cs` (L206-294) — **CẦN TECH LEAD APPROVAL** (Domain modification, account-number only — không thêm field) | ⏳ PENDING |
| 5 | W5a-T5 | Add unit test: `S2aBook_PersonalIncomeTax_ShouldCalculateOnRevenue_NotOnVat` — assert PIT = Revenue * rate, không phải Vat * 0.1 | `6_Tests/VanAn.Core.Tests/Services/HKDBookServiceTests.cs` | ⏳ PENDING |
| 6 | W5a-T6 | Add unit test: `S2bBook_ServiceRevenue_ShouldUseAccount5118_Not521` — assert account mapping | `6_Tests/VanAn.Core.Tests/Services/HKDBookServiceTests.cs` | ⏳ PENDING |
| 7 | W5a-T7 | `dotnet build VanAn.sln` Release + `dotnet test` pass | Solution-wide | ⏳ PENDING |

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
**Task Card:** `docs/AI/tasks/wave5b_hkd_fix_industry_sector_tax_rates_task_card.md` (NEW)

### Conditional execution
- **IF W0-T10 confirms `Tenant.IndustrySector` exists** → proceed normally
- **IF W0-T10 finds field missing AND Tech Lead approves Domain mod** → add `IndustrySector` enum/string to `Tenant` first (W5b-T0), then proceed
- **IF W0-T10 finds field missing AND Tech Lead does NOT approve** → **DESCOPE Wave 5b**, use single default rate from W5a, log as technical debt (`docs/AI/technical_debt.md`), proceed to Wave 6

### Tasks
| # | Task ID | Task | Files | Status |
|---|---|---|---|---|
| 0 | W5b-T0 | (CONDITIONAL — only if Tenant.IndustrySector missing + approved) Add `IndustrySector` field to `Tenant` domain entity | `1_Shared/Domain/Tenant.cs` — **CẦN TECH LEAD APPROVAL** | ⏳ PENDING |
| 1 | W5b-T1 | Verify `HKDRevenueClassificationService` + `IHKDTaxClassificationService` — đọc API, confirm có mapping suất thuế theo ngành nghề | `3_CoreHub/Services/Orchestration/HKDRevenueClassificationService.cs`, `3_CoreHub/Services/IHKDTaxClassificationService.cs` (READ) | ⏳ PENDING |
| 2 | W5b-T2 | Nếu mapping suất thuế chưa có → tạo lookup table ngành nghề → (GTGT%, TNCN%) theo TT 152 (1%; 1,5%; 2%; 2,5%; 3%; 5% GTGT; 0,5%; 1%; 1,5%; 2% TNCN) | `3_CoreHub/Services/Orchestration/HKDRevenueClassificationService.cs` (UPDATE) hoặc file mới | ⏳ PENDING |
| 3 | W5b-T3 | Sửa `S2aHKDTemplateImpl` — thay `VatAmount = TotalRevenue * defaultRate` (từ W5a) bằng gọi `HKDRevenueClassificationService.GetVatRate(industry)` | `3_CoreHub/Services/Template/TemplateFactory.cs` (L177-249) | ⏳ PENDING |
| 4 | W5b-T4 | Thêm khái niệm "nhóm ngành nghề" vào template S2a — mỗi ngành có `Tổng cộng (n) / Thuế GTGT / Thuế TNCN` riêng | `3_CoreHub/Services/Template/TemplateFactory.cs` (S2aHKDTemplateImpl) | ⏳ PENDING |
| 5 | W5b-T5 | Add unit test: `S2aBook_VatAmount_ShouldUseIndustryRate_NotHardcoded5Percent` — seed tenant với ngành nghề 1% GTGT, assert VatAmount = Revenue * 0.01 | `6_Tests/VanAn.Core.Tests/Services/HKDBookServiceTests.cs` | ⏳ PENDING |
| 6 | W5b-T6 | Add unit test: `S2aBook_IndustryGrouping_ShouldSeparateBySector` — seed 2 ngành nghề, assert 2 group totals | `6_Tests/VanAn.Core.Tests/Services/HKDBookServiceTests.cs` | ⏳ PENDING |
| 7 | W5b-T7 | `dotnet build VanAn.sln` Release + `dotnet test` pass | Solution-wide | ⏳ PENDING |

### Entry criteria
- [ ] Wave 5a merged (PIT fix + account mapping done)
- [ ] W0-T10 result: `Tenant.IndustrySector` exists OR Tech Lead approval for W5b-T0
- [ ] **If descope triggered** → skip this wave, document in `docs/AI/technical_debt.md`, proceed to Wave 6

### Exit criteria (only if executed)
- [ ] `S2aHKDTemplateImpl` dùng `HKDRevenueClassificationService` cho suất thuế (không cứng)
- [ ] S2a template có nhóm ngành nghề (mỗi ngành có tổng cộng + GTGT + TNCN riêng)
- [ ] 2 unit test pass
- [ ] `dotnet build VanAn.sln` Release — 0 errors
- [ ] guard-check.ps1 PASSED

### Why fifth-b (separate from 5a)
- Risk cao nhất — cần modeling ngành nghề + có thể cần Domain mod (Tenant.IndustrySector)
- Tách ra để 5a có thể merge độc lập — 5b không block Wave 6 nếu descope
- Compliance pháp lý đầy đủ — sai = báo cáo sai thuế, nhưng 5a đã fix 2 bug nghiêm trọng trước

---

## 7. WAVE 6 — Retrofit Tests with Numeric Assertions

**Branch:** `feature/hkd-fix-wave6-retrofit-numeric-tests`
**Estimated sessions:** 1-2
**Conflict risk:** LOW (chỉ sửa test)
**Priority:** 6 (Critical — verify fix Issue 1/4)
**Task Card:** `docs/AI/tasks/wave6_hkd_fix_retrofit_numeric_tests_task_card.md`

### Tasks
| # | Task ID | Task | Files | Status |
|---|---|---|---|---|
| 1 | W6-T1 | Update `GenerateS1aBookAsync_ShouldGenerateBook_WhenTenantIsHKDGroup1` — seed `JournalEntries` với account 511 (Credit 1000) + 611 (Debit 500), assert `result.NumericValues["TotalRevenue"] == 1000m`, `["TotalExpense"] == 500m`, `["NetProfit"] == 500m` | `6_Tests/VanAn.Core.Tests/Services/HKDBookServiceTests.cs` (L41-66) | ⏳ PENDING |
| 2 | W6-T2 | Update `GenerateS2aBookAsync_ShouldGenerateBook_WhenTenantIsHKDGroup2` — seed JournalEntries, assert `NumericValues["TotalRevenue"]`, `["VatAmount"]`, `["PersonalIncomeTax"]`, `["NetRevenue"]` đúng công thức | `6_Tests/VanAn.Core.Tests/Services/HKDBookServiceTests.cs` (L68-93) | ⏳ PENDING |
| 3 | W6-T3 | Update `GenerateS2bBookAsync_ShouldGenerateRevenueBook_WhenTenantIsHKDGroup2` — seed JournalEntries với 511 + 5118, assert `NumericValues["SalesRevenue"]`, `["ServiceRevenue"]`, `["TotalRevenue"]` | `6_Tests/VanAn.Core.Tests/Services/HKDBookServiceTests.cs` (L155-180) | ⏳ PENDING |
| 4 | W6-T4 | Add test `GenerateS2cBookAsync_ShouldCalculateGrossProfitAndNetProfit` — seed 511/632/641/642, assert `NumericValues["Revenue"]`, `["CostOfGoodsSold"]`, `["OperatingExpenses"]`, `["NetProfit"]` | `6_Tests/VanAn.Core.Tests/Services/HKDBookServiceTests.cs` (NEW) | ⏳ PENDING |
| 5 | W6-T5 | Add test `GenerateS2dBookAsync_ShouldCalculateInventoryTotals` — seed 152/153/155/156, assert `NumericValues["Materials"]`, `["Tools"]`, `["Products"]`, `["Goods"]`, `["TotalInventory"]` | `6_Tests/VanAn.Core.Tests/Services/HKDBookServiceTests.cs` (NEW) | ⏳ PENDING |
| 6 | W6-T6 | Add test `GenerateS2eBookAsync_ShouldCalculateCashTotals` — seed 111/112, assert cash values | `6_Tests/VanAn.Core.Tests/Services/HKDBookServiceTests.cs` (NEW) | ⏳ PENDING |
| 7 | W6-T7 | Add test `GenerateS3aBookAsync_ShouldGenerateTrialBalanceBook` — seed multi-account, assert `NumericValues` | `6_Tests/VanAn.Core.Tests/Services/HKDBookServiceTests.cs` (NEW) | ⏳ PENDING |
| 8 | W6-T8 | Add test `GenerateS1aBook_NumericValues_ShouldNotBeEmpty_AfterWave4Fix` — regression test cho Issue 1 (no-op CalculateAsync) | `6_Tests/VanAn.Core.Tests/Services/HKDBookServiceTests.cs` (NEW) | ⏳ PENDING |
| 9 | W6-T9 | `dotnet build VanAn.sln` Release + `dotnet test` pass (tất cả test mới + cũ) | Solution-wide | ⏳ PENDING |

### Entry criteria
- [ ] Wave 5a merged (account mapping + PIT fix)
- [ ] Wave 5b merged OR descoped (W0-T10 result determines — if descoped, document in technical_debt.md)
- [ ] Wave 4 merged (NumericValues có số liệu + smoke test pass)
- [ ] Wave 2 merged (JournalEntries có data)

### Exit criteria
- [ ] 7 test `GenerateS*BookAsync` assert `NumericValues` cụ thể (không chỉ metadata)
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
**Task Card:** `docs/AI/tasks/wave7_hkd_fix_api_endpoint_di_smoke_task_card.md`

### Tasks
| # | Task ID | Task | Files | Status |
|---|---|---|---|---|
| 1 | W7-T1 | Create DTO `HKDBookDto` — TenantId, Period, BookTypeCode, NumericValues (Dictionary), Entries (list) | `3_CoreHub/Services/Dtos/HKDBookDtos.cs` (NEW) hoặc existing Dtos folder | ⏳ PENDING |
| 2 | W7-T2 | Add endpoint `GET /api/hkd-books/{templateCode}?year=&month=` trong `AccountingEntriesController` (hoặc controller mới `HKDBooksController`) — gọi `_hkdBookService.GenerateS*BookAsync` dựa templateCode, return `HKDBookDto` | `2_Gateway/Controllers/AccountingEntriesController.cs` hoặc `2_Gateway/Controllers/HKDBooksController.cs` (NEW) | ⏳ PENDING |
| 3 | W7-T3 | Add endpoint `GET /api/hkd-books` — list all available templates for tenant's HKDGroup | Same controller | ⏳ PENDING |
| 4 | W7-T4 | Add DI smoke test — assert `IHKDBookGenerationService`, `IFormulaEngine`, `IDataProvider`, `IPreAggregationService` resolvable từ DI container | `6_Tests/VanAn.Integration.Tests/` (NEW — `HKDBookDISmokeTests.cs`) | ⏳ PENDING |
| 5 | W7-T5 | Add integration test `GET_hkd_books_S1a_ShouldReturnBookWithNumericValues` — seed data, call endpoint, assert response có NumericValues | `6_Tests/VanAn.Integration.Tests/` (NEW) | ⏳ PENDING |
| 6 | W7-T6 | **[Concern 7] Add multi-tenancy isolation test `GET_hkd_books_ShouldNotLeakCrossTenantData`** — seed tenant A + tenant B with distinct JournalEntries, authenticate as tenant A, call `GET /api/hkd-books/S1a_HKD`, assert response ONLY contains tenant A's data (TotalRevenue matches tenant A, not A+B). Verify endpoint enforces `TenantId` filter. | `6_Tests/VanAn.Integration.Tests/` (NEW) | ⏳ PENDING |
| 7 | W7-T7 | `dotnet build VanAn.sln` Release + `dotnet test` pass | Solution-wide | ⏳ PENDING |

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
**Task Card:** `docs/AI/tasks/wave8_hkd_fix_ui_docx_export_regression_task_card.md`

### Tasks
| # | Task ID | Task | Files | Status |
|---|---|---|---|---|
| 1 | W8-T1 | Add Razor page `/accounting/hkd-books` — list available templates theo HKDGroup, link tới page render từng book | `5_WebApps/ShopERP/Components/Pages/Accounting/HKDBooks.razor` (NEW) | ⏳ PENDING |
| 2 | W8-T2 | Add Razor page `/accounting/hkd-books/{templateCode}` — render book với TT 152 layout (header: HỘ/CÁ NHÂN KD + MST + địa chỉ; bảng: chứng từ + diễn giải + số tiền; footer: tổng thuế + chữ ký) dùng UI Platform components (VanAnCard, VanATable, VanAForm) | `5_WebApps/ShopERP/Components/Pages/Accounting/HKDBookDetail.razor` (NEW) | ⏳ PENDING |
| 3 | W8-T3 | Add "Export DOCX" button — generate .docx theo layout mẫu TT 152 (dùng library `DocX` hoặc `OpenXML SDK` — verify package trong project trước) | `5_WebApps/ShopERP/Components/Pages/Accounting/HKDBookDetail.razor` + service export | ⏳ PENDING |
| 4 | W8-T4 | Add "Export XLSX" button — generate .xlsx (dùng `ClosedXML` nếu đã có, hoặc `EPPlus`) | Same | ⏳ PENDING |
| 5 | W8-T5 | Add E2E test `hkd-books.spec.ts` — navigate to `/accounting/hkd-books`, verify list templates, click template, verify render có header/bảng/footer, verify export button | `6_Testing/e2e-tests/hkd-books.spec.ts` (NEW) | ⏳ PENDING |
| 6 | W8-T6 | Add architecture test — verify không có `HKDBookTemplate` subclass với `CalculateAsync` body chỉ là `await Task.CompletedTask` (regression prevention cho Issue 1) | `6_Tests/VanAn.Core.Tests/` (NEW — `HKDBookTemplateArchitectureTests.cs`) | ⏳ PENDING |
| 7 | W8-T7 | Add lint rule — grep mojibake pattern (`Ã|Â|á»|áº|Ä`) trong `.cs` files, fail CI nếu có | `scripts/check-encoding.ps1` (NEW) hoặc thêm vào guard-check.ps1 | ⏳ PENDING |
| 8 | W8-T8 | Update `docs/UI_Platform_Implementation_Guide.md` hoặc README — document HKD book page pattern + export pattern | docs | ⏳ PENDING |
| 9 | W8-T9 | Update `project_state.md` — mark HKD Book Fix stream complete | `docs/AI/project_state.md` | ⏳ PENDING |
| 10 | W8-T10 | `dotnet build VanAn.sln` Release + `dotnet test` + `npx playwright test --list` pass | Solution-wide | ⏳ PENDING |

### Entry criteria
- [ ] Wave 7 merged (endpoint có sẵn)
- [ ] UI Platform components available (VanAnCard, VanATable — verify)
- [ ] Docx/Xlsx library available hoặc approval thêm dependency

### Exit criteria
- [ ] Page `/accounting/hkd-books` list templates theo HKDGroup
- [ ] Page `/accounting/hkd-books/{templateCode}` render book với TT 152 layout (header + bảng + footer + chữ ký)
- [ ] Export DOCX generate file đúng layout TT 152
- [ ] Export XLSX generate file đúng layout
- [ ] E2E test pass (parse + runtime nếu services chạy)
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

### TT 152 Compliance (Concern 4 — RESOLVED, Wave 5 split)
- 7 mẫu báo cáo: S1a (Group 1), S2a-S2e (Group 2), S3a (Group 3)
- Layout từng mẫu đã extract trong Wave 0 (docx → text)
- Wave 8 phải match layout: header (HỘ/CÁ NHÂN KD + MST + địa chỉ + "Mẫu số X-HKD (Kèm theo TT 152/2025/TT-BTC)"), bảng (chứng từ + diễn giải + số tiền), footer (tổng thuế + chữ ký NGƯỜI ĐẠI DIỆN HKD)
- **Wave 5a fix PIT-on-revenue + account mapping** (logic bugs, không cần modeling)
- **Wave 5b industry-sector rates** (full compliance, conditional — may descope if Tenant.IndustrySector missing)

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

## 11. APPROVAL CHECKLIST (v2)

- [x] Master plan reviewed (v2 — 10 waves, 8 root-cause issues, 3 amendments + 5 concerns resolved)
- [ ] 10 task cards reviewed (Wave 0-8, with Wave 5 split into 5a + 5b)
- [ ] HKD Book report audit reviewed (8 issues — see Section 1)
- [ ] Wave 0 pre-flight verification complete (11 tasks — including 4 promoted architecture decisions)
- [ ] `dotnet build VanAn.sln` Release baseline pass
- [ ] Data flow gap confirmed (JournalEntries table rỗng)
- [ ] DI registrations confirmed (5 service chưa đăng ký)
- [ ] **W0-T8: `ITemplateFactory` conflict resolution documented** (promoted from W3-T8)
- [ ] **W0-T9: docx/xlsx library availability confirmed OR dependency-add approved** (promoted from W8-T3/T4)
- [ ] **W0-T10: `Tenant.IndustrySector` field existence confirmed** (determines Wave 5b scope)
- [ ] **W0-T11: Double-write audit complete** (write-path map documented, decision tree path selected)
- [ ] Tech Lead approval cho W5a-T4 (Domain modification — account number `"512"` → `"5118"`, no new field) — **chỉ cần trước Wave 5a**
- [ ] Tech Lead approval cho W5b-T0 (conditional — add `IndustrySector` to `Tenant`) — **chỉ cần trước Wave 5b IF W0-T10 finds field missing**
- [ ] Branch strategy confirmed (per-wave merge to main, always-green)
- [ ] Sẵn sàng implement Wave 0 + Wave 1 (có thể cùng session)

---

## 12. EFFORT SUMMARY (v2)

| Wave | Description | Sessions | Risk |
|---|---|---|---|
| Wave 0 | Pre-flight verification (11 tasks — baseline + 4 architecture decisions promoted) | 0.5-1 | None |
| Wave 1 | Fix UTF-8 mojibake (mechanical) | 0.5 | Low |
| Wave 2 | Bridge AccountingEntry → JournalEntry persistence (with double-write decision tree) | 1-2 | Medium |
| Wave 3 | Wire calc engine into DI (conflict pre-resolved in W0-T8) | 1 | Low |
| Wave 4 | Route HKDBookService through IHKDBookGenerationService + smoke test tripwire | 1 | Medium |
| Wave 5a | Fix account mapping + PIT-on-revenue (no industry modeling, 1 Domain account-number fix) | 1 | Medium |
| Wave 5b | Industry-sector tax rates per TT 152 (CONDITIONAL — may descope) | 1-2 | High |
| Wave 6 | Retrofit tests with numeric assertions | 1-2 | Low |
| Wave 7 | API endpoint + DI smoke + multi-tenancy isolation test | 1 | Low |
| Wave 8 | UI page + DOCX export + regression prevention | 2-3 | Medium |
| **Total** | | **10-15 sessions** (5b optional) | |

**Critical path:** Wave 0 → Wave 1 → Wave 2 → Wave 3 → Wave 4 → Wave 5a → Wave 6 → Wave 7 → Wave 8
**Optional path:** Wave 5b (between 5a and 6) — executes only if W0-T10 confirms `Tenant.IndustrySector` exists OR Tech Lead approves Domain mod
**Parallel path:** Wave 0 + Wave 1 có thể cùng session (cả 2 non-code/low-risk, độc lập)
**Descope path:** If Wave 5b descoped → use default rate from 5a, log technical debt, Wave 6-8 proceed normally

**Fix target (v2):**
- Before: 7 HKD book templates, `NumericValues` luôn rỗng, output plain text, không endpoint/UI, test pass trắng
- After (full): 7 HKD book templates, `NumericValues` có số liệu thực, output docx/xlsx theo TT 152 layout, endpoint + UI page, test assert numeric values, regression prevention, multi-tenancy enforced
- After (if 5b descoped): Same as full but tax rates use single default (not industry-specific) — technical debt logged
- After (if export descoped): Same as full but UI render-only, no DOCX/XLSX export — technical debt logged
- Compliance: TT 152/2025/TT-BTC (suất thuế theo ngành nghề nếu 5b executed, layout mẫu, chữ ký)

---

## 13. ROLLBACK PLAN (v2)

Nếu wave fail không fix được:
- **Wave 1-4:** Revert branch — không ảnh hưởng production (code cũ vẫn chạy, chỉ không có số liệu)
- **Wave 5a:** Revert branch — giữ `_vietnameseAccounts` sai + PIT tính trên VAT (sai nhưng chạy được)
- **Wave 5b:** Revert branch OR descope — giữ default rate từ 5a (sai về ngành nghề nhưng chạy được, log technical debt)
- **Wave 6:** Revert test — giữ test cũ (pass trắng, không phát hiện bug)
- **Wave 7:** Revert endpoint — không có API mới
- **Wave 8:** Revert UI — không có page mới (hoặc descope export only, giữ UI render)

**Không có wave nào break production** — tất cả là additive fix hoặc fix logic không ảnh hưởng existing flow (trừ Wave 2 thay đổi write path — cần rollback cẩn thận nếu double-write, decision tree W0-T11 loại bỏ risk này).

**Per-wave merge to main** cho phép revert từng wave độc lập — không cần revert toàn bộ stream.

---

## 14. REFERENCES (v2)

- **Mẫu TT 152:** `docs/plan_MVP/HKD_BookAcc/*.docx` (7 files — S1a, S2a-S2e, S3a)
- **Audit report:** Session 2026-07-03 (chat history — 8 root causes)
- **v2 review:** Session 2026-07-03 (3 amendments + 5 concerns resolved)
- **E2E cleanup master plan (template):** `docs/AI/tasks/e2e_test_cleanup_master_plan.md`
- **Governance:** `.devin/rules/governance.md` (Domain protection, Hard Stops, UI Platform)
- **Workflow:** `.devin/workflows/newfeaturebuild.md` (ANALYZE → IMPLEMENT)
- **Task cards (v2 — 10 cards):**
  - `wave0_hkd_fix_preflight_task_card.md` (expanded — 11 tasks)
  - `wave1_hkd_fix_encoding_mojibake_task_card.md`
  - `wave2_hkd_fix_bridge_journal_persistence_task_card.md` (added decision tree)
  - `wave3_hkd_fix_wire_calc_engine_di_task_card.md` (W3-T8 demoted to verify-only)
  - `wave4_hkd_fix_route_through_generation_service_task_card.md` (added W4-T11 smoke test)
  - `wave5a_hkd_fix_account_mapping_pit_task_card.md` (NEW — split from wave5)
  - `wave5b_hkd_fix_industry_sector_tax_rates_task_card.md` (NEW — split from wave5, conditional)
  - `wave6_hkd_fix_retrofit_numeric_tests_task_card.md`
  - `wave7_hkd_fix_api_endpoint_di_smoke_task_card.md` (added W7-T6 multi-tenancy test)
  - `wave8_hkd_fix_ui_docx_export_regression_task_card.md`

> **NOTE:** Wave 5 task card needs to be split into 5a + 5b cards (action item before Wave 5a starts). Old `wave5_hkd_fix_account_mapping_tax_formulas_task_card.md` to be replaced.
- **Project state:** `docs/AI/project_state.md` (update sau mỗi wave)
