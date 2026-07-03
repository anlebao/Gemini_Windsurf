# TASK CARD: HKD Book Fix — Wave 5 (MERGED 5a+5b+partial 5c) — Industry Sector + PIT Fix + Account Mapping + 2026 Tax Rates

> **Supersedes:** `wave5a_hkd_fix_account_mapping_pit_task_card.md`, `wave5b_hkd_fix_industry_sector_tax_rates_task_card.md` (partial — 2026 regulatory threshold fix deferred to Wave 5c proper)
>
> **Reason for merge:** Investigation (2026-07-03) proved that the original W5a account-mapping fix (521/512→5118) was based on a false premise (TT 200 compliance) and would not fix the empty-result bug (no production write path writes 5118). The real fix requires `IndustrySector` on `AccountingEntry` so S2a/S2b can split revenue by industry sector per TT 152 layout. This merges the 5a PIT fix + 5b industry sector + partial 5c (4-group tax rates) into one coherent wave.

## 1. GOAL & CONTEXT

- **Mục tiêu cốt lõi:**
  1. Fix PIT formula (`VatAmount*0.1` → `TotalRevenue*industryPitRate`) — bug thật
  2. Add `IndustrySector` enum + field to `AccountingEntry` (Domain) + `Tenant.DefaultIndustrySector` (Domain) + `Order.IndustrySector` (Domain)
  3. Extend Formula Engine DSL: `SUM_ACCOUNT_BY_INDUSTRY("5", "Credit", "Distribution")`
  4. Redesign S2a/S2b templates: 4 industry groups per TT 152 layout (not goods-vs-service split)
  5. Fix `_vietnameseAccounts` labels (211/811/821/841) + fix stale "TT 200" comment
  6. 4-group tax rate lookup per Luật 2025 + ND 117/2025 (1%/0.5%, 3%/1.5%, 5%/2%, 2%/1%)
- **Nghiệp vụ áp dụng:** TT 152/2025/TT-BTC + TT 88/2021/TT-BTC (single-entry) + Luật Thuế GTGT/TNCN sửa đổi 2025 + ND 117/2025 + Nghị quyết 198/2025/QH15
- **Status:** PENDING — Planning (Tech Lead approval granted 2026-07-03 for Domain modification)
- **Branch:** `feature/hkd-fix-wave5-industry-sector-pit`
- **Estimated Sessions:** 3-4 (Domain + Formula Engine + Templates + Tests)
- **Master plan link:** `docs/AI/tasks/hkd_book_accounting_fix_master_plan.md` Section 6 (Wave 5a+5b)

---

## 2. ACTIVE WORKFLOW ROUTING

- **Target Workflow:** `newfeaturebuild.md` (ANALYZE → IMPLEMENT)
- **Execution Mode:** IMPLEMENT (Tech Lead approval granted)
- **Current Phase:** Wave 5 of 12 (merged 5a+5b+partial 5c)
- **Dependency:** Wave 4 merged (GenerateS*BookAsync → IHKDBookGenerationService)
- **Blocks:** Wave 5c proper (2026 threshold 500M→1B, TNCN formulas Nhóm 2/3/4), Wave 6 (tests), Wave 7 (API), Wave 8 (UI)
- **Tech Lead approval:** GRANTED 2026-07-03 — Domain modification (add `IndustrySector` field to `AccountingEntry` + `Tenant` + `Order`)

---

## 3. INVESTIGATION FINDINGS (2026-07-03)

### 3.1 AccountCode Write Map (production paths only)

| Write site | AccountCode | EntryType | Notes |
|---|---|---|---|
| `OrderService.cs` L108 | `"511"` | Revenue | All order revenue (goods + service) |
| `OrderService.cs` L133 | `"621"` | Expense | COGS |
| `OrderService.cs` L173-174 | `"111"` / `"511"` | JournalEntry | Cash debit / Revenue credit |
| `OrderService.cs` L205-206 | `"632"` / `"156"` | JournalEntry | COGS debit / Inventory credit |
| `HKDBookService.RecordRevenueAsync` L59 | `null` | Revenue | No AccountCode set |
| `HKDBookService.RecordExpenseAsync` L89 | `null` | Expense | No AccountCode set |
| `HKDBookService.ConvertToJournalEntries` L718 | `"511"` / `"611"` | OBSOLETE (Wave 4) | Marked `[Obsolete]` |
| `HKDTaxReportingService` L502/522/588 etc | `"5111"` / `"6321"` / `"1111"` | MOCK DATA | Fake data, not production |

**Key finding:** NO production write path ever writes `"512"`, `"521"`, or `"5118"`. The original W5a fix (521/512→5118) would swap one empty query for another.

### 3.2 AccountCode Query Map (SUM_ACCOUNT in templates)

| Template | Field | Current query | Production write match? |
|---|---|---|---|
| S1a | TotalRevenue | `SUM_ACCOUNT("5","Credit")` | YES — matches "511" (StartsWith "5") + null+Revenue heuristic |
| S2a | TotalRevenue | `SUM_ACCOUNT("5","Credit")` | YES — same |
| S2b | SalesRevenue | `SUM_ACCOUNT("511","Credit")` | YES — matches "511" |
| S2b | ServiceRevenue | `SUM_ACCOUNT("512"/"521","Credit")` | NO — no write to 512/521 → always 0 |
| S2c | TotalRevenue | `SUM_ACCOUNT("5","Credit")` | YES |
| S2c | COGS | `SUM_ACCOUNT("632","Debit")` | YES — matches OrderService L205 |
| S2c | Admin | `SUM_ACCOUNT("641","Debit")` | NO — no write to 641 |
| S2c | Selling | `SUM_ACCOUNT("642","Debit")` | NO — no write to 642 |
| S2d | Materials/Supplies/Tools/Goods | `SUM_ACCOUNT("152"/"153"/"155"/"156","Debit")` | NO — only 156 has write (OrderService L206) |
| S2e | Cash/Bank | `SUM_ACCOUNT("111"/"112","Debit")` | PARTIAL — 111 has write, 112 does not |

### 3.3 TT 152 Layout vs Code Structure

| Template | TT 152 layout | Code structure | Match? |
|---|---|---|---|
| S1a | 1 col, 1 total row | TotalRevenue + TotalExpense + NetProfit | YES |
| S2a | **5 industry groups**, each: Total + GTGT + TNCN | TotalRevenue + VatAmount + PIT (single group) | NO — missing industry split |
| S2b | **5 industry groups**, each: Total + GTGT | SalesRevenue + ServiceRevenue (goods-vs-service) | NO — wrong split axis |
| S2c | Doanh thu + 6 chi phí sub-categories + chênh lệch + TNCN | TotalRevenue + COGS + Admin + Selling | PARTIAL — 3/7 expense categories |
| S2d | Per-item Nhap/Xuat/Ton | Account-level aggregates | NO — wrong granularity |
| S2e | Cash + Bank (per bank), with opening balance | Cash + Bank + Receivable | PARTIAL — no opening balance |
| S3a | 10 tax columns | (mock only) | NO — not implemented |

**Key finding:** TT 152 S2a/S2b split by **industry sector** (1-5 nhóm ngành nghề), NOT by goods-vs-service. The code's `SalesRevenue`/`ServiceRevenue` split is a hallucination from TT 200 thinking.

### 3.4 Formula Engine DSL Constraint

Current DSL: `SUM_ACCOUNT("pattern", "side")` — 2 params only.
`IDataProvider.GetAccountSum(context, accountPattern, side)` — no sector filter.
`SmartPreAggregationService.GetAccountSumAsync` — filters by AccountCode.StartsWith OR EntryType heuristic, no sector.

**To split by industry sector, we need:**
- New DSL function: `SUM_ACCOUNT_BY_INDUSTRY("5", "Credit", "Distribution")`
- `IDataProvider.GetAccountSum` overload with `industrySector` parameter
- `SmartPreAggregationService.GetAccountSumAsync` filter by `IndustrySector`

### 3.5 Existing Tax Classification Service

`IHKDTaxClassificationService` exists but has NO `GetVatRate(industry)` / `GetPitRate(industry)` method. Needs extension.

`HKDRevenueClassificationService` has WRONG thresholds (500M/1B/3B — should be 1B/3B/50B per 2026 law). This is Wave 5c proper — **deferred** from this merged wave (only tax RATES included here, not thresholds).

`HKDRevenueClassification.CalculateGroup` in `Domain.cs` L2032-2037 also uses wrong thresholds (500M/1B/3B). **Deferred to Wave 5c proper.**

---

## 4. RELEVANT FILES (CONTEXT BOUNDARY)

### Files được phép đọc + sửa

**Domain layer (TECH LEAD APPROVAL GRANTED):**
- `1_Shared/Domain.cs` — Add `IndustrySector` enum, add `IndustrySector` field to `AccountingEntry`, add `DefaultIndustrySector` to `Tenant`, add `IndustrySector` to `Order`, update `CreateRevenue`/`CreateExpense` factory methods
- `1_Shared/Domain/HKDTemplates.cs` — Redesign S2a/S2b templates (4 industry groups), fix S2b formula

**Service layer:**
- `3_CoreHub/Services/HKDBookService.cs` — Fix `_vietnameseAccounts` labels, fix stale "TT 200" comment, update `RecordRevenueAsync`/`RecordExpenseAsync` to accept `IndustrySector`
- `3_CoreHub/Services/Template/TemplateFactory.cs` — Redesign `S2aHKDTemplateImpl`/`S2bHKDTemplateImpl` (4 industry groups), fix PIT formula
- `3_CoreHub/Services/AccountingEntryService.cs` — Update `CreateRevenueEntryAsync`/`CreateExpenseEntryAsync` to accept + persist `IndustrySector`
- `3_CoreHub/Services/OrderService.cs` — Pass `IndustrySector` from `Order.IndustrySector ?? Tenant.DefaultIndustrySector` to `CreateRevenueEntryAsync`
- `3_CoreHub/Services/IAccountingService.cs` — Add `IndustrySector` parameter to interface methods
- `3_CoreHub/Services/Formula/ProductionFormulaEngine.cs` — Add `SUM_ACCOUNT_BY_INDUSTRY` parsing + evaluation
- `3_CoreHub/Services/Formula/IFormulaEngine.cs` — (no change needed if DSL is string-based)
- `3_CoreHub/Services/Data/IDataProvider.cs` — Add `GetAccountSum` overload with `industrySector`
- `3_CoreHub/Services/Data/ScopedDataProvider.cs` — Implement sector-filtered query
- `3_CoreHub/Services/Data/DataProviderService.cs` — Implement sector-filtered query
- `3_CoreHub/Services/PreAggregation/SmartPreAggregationService.cs` — Filter by `IndustrySector` in `GetAccountSumAsync`
- `3_CoreHub/Services/IHKDTaxClassificationService.cs` — Add `GetVatRate(industry)` / `GetPitRate(industry)` methods
- `3_CoreHub/Services/HKDTaxClassificationService.cs` (or new service) — Implement 4-group rate lookup

**Infrastructure:**
- `3_CoreHub/Migrations/*` — New EF Core migration for `IndustrySector` column (Stream E migrations enabled)
- `3_CoreHub/Repositories/AccountingEntryRepository.cs` — (verify no change needed)

**Tests:**
- `6_Tests/VanAn.Core.Tests/Services/HKDBookServiceTests.cs` — Add tests
- `6_Tests/VanAn.Core.Tests/Services/SmartPreAggregationServiceWave2Tests.cs` — Update seed data with `IndustrySector`
- `6_Tests/VanAn.Integration.Tests/Infrastructure/TestDataSeeder.cs` — Update seed with `IndustrySector`

### Files được phép đọc (verify only)
- `3_CoreHub/Services/Orchestration/HKDRevenueClassificationService.cs` — verify threshold (WRONG — Wave 5c will fix)
- `3_CoreHub/Services/HKDTaxReportingService.cs` — verify mock data patterns
- `1_Shared/DTOs/AccountingEntryDto.cs` — verify DTO has AccountCode (will need IndustrySector)

### Boundary Rules (Nghiêm cấm)
- KHÔNG sửa `AccountingEntry` immutability — adding field is OK (via constructor + factory), but no mutation after creation
- KHÔNG sửa `HKDRevenueClassification.CalculateGroup` thresholds (500M→1B) — that's Wave 5c proper
- KHÔNG sửa `HKDRevenueClassificationService` thresholds — that's Wave 5c proper
- KHÔNG thêm field mới nào khác ngoài `IndustrySector` (no expense category, no opening balance — those are follow-up streams)
- KHÔNG thay đổi `IHKDBookService` interface public contract
- KHÔNG cứng suất thuế mới ngoài 4 nhóm đã approved (1%/0.5%, 3%/1.5%, 5%/2%, 2%/1%)

---

## 5. TECHNICAL & REGULATORY CONSTRAINTS

### 5.1 IndustrySector Enum (W5-T1 — Domain)

```csharp
/// <summary>
/// 4 industry sector groups per Luật Thuế GTGT/TNCN sửa đổi 2025 + ND 117/2025.
/// Determines VAT + PIT rate for HKD Group 2 businesses.
/// </summary>
public enum IndustrySector
{
    Distribution = 1,        // Phân phối, cung cấp hàng hóa — GTGT 1%, TNCN 0.5%
    ProductionTransport = 2, // Sản xuất, vận tải, dịch vụ gắn hàng hóa, xây dựng bao thầu NVL — GTGT 3%, TNCN 1.5%
    Service = 3,             // Dịch vụ, xây dựng không bao thầu NVL — GTGT 5%, TNCN 2%
    OtherBusiness = 4        // Hoạt động kinh doanh khác — GTGT 2%, TNCN 1%
}
```

### 5.2 AccountingEntry Field (W5-T2 — Domain)

Add `IndustrySector?` field to `AccountingEntry`:
- Nullable (existing entries get NULL — backward compatible)
- Set via factory method `CreateRevenue(tenantId, period, amount, description, accountCode, industrySector, reference)`
- Immutable after creation (no setter)
- EF Core migration adds column
- **NULL handling:** entries with NULL IndustrySector are counted in the **OtherBusiness** group in S2a/S2b reports (ensures TotalRevenue = SUM(all sector revenues) always holds)

### 5.3 Tenant.DefaultIndustrySector + Order.IndustrySector (W5-T3 — Domain)

Add `IndustrySector? DefaultIndustrySector` to `Tenant`:
- Nullable (existing tenants get NULL — must be set before generating S2a/S2b)
- Used as fallback when `Order.IndustrySector` is not set

Add `IndustrySector? IndustrySector` to `Order`:
- Nullable (existing orders get NULL — falls back to `Tenant.DefaultIndustrySector`)
- Per-order override: if set, takes precedence over Tenant default
- `OrderService.GenerateAccountingEntriesAsync` uses `order.IndustrySector ?? tenant.DefaultIndustrySector`

### 5.4 Formula Engine DSL Extension (W5-T4 — Service)

New DSL function: `SUM_ACCOUNT_BY_INDUSTRY("pattern", "side", "sectorName")`
- Example: `SUM_ACCOUNT_BY_INDUSTRY("5", "Credit", "Distribution")`
- Falls back to `SUM_ACCOUNT` when sector is NULL (backward compatible)
- `IDataProvider.GetAccountSum(context, accountPattern, side, industrySector?)` — new overload

### 5.4b NULL IndustrySector Handling (W5-T4b — Service)

When querying `SUM_ACCOUNT_BY_INDUSTRY`:
- Entries with `IndustrySector == NULL` are counted in the **OtherBusiness** group
- This ensures `TotalRevenue = SUM(all sector revenues)` always holds
- `SmartPreAggregationService.GetAccountSumAsync`: filter `(e.IndustrySector == sector || (e.IndustrySector == null && sector == OtherBusiness))`

### 5.5 Tax Rate Lookup (W5-T5 — Service)

4-group rate table (per Luật 2025 + ND 117/2025):

| IndustrySector | GTGT | TNCN (Nhóm 2) |
|---|---|---|
| Distribution | 1% | 0.5% |
| ProductionTransport | 3% | 1.5% |
| Service | 5% | 2% |
| OtherBusiness | 2% | 1% |

### 5.6 S2a Template Redesign (W5-T6 — Service + Domain)

Per TT 152 S2a layout (4 industry groups × 3 fields each):

```
For each IndustrySector (4 groups):
  - Revenue_{sector} = SUM_ACCOUNT_BY_INDUSTRY("5", "Credit", "{sector}")
  - VatAmount_{sector} = Revenue_{sector} * vatRate_{sector}
  - PIT_{sector} = Revenue_{sector} * pitRate_{sector}
TotalRevenue = SUM(Revenue_{all sectors})
TotalVat = SUM(VatAmount_{all sectors})
TotalPIT = SUM(PIT_{all sectors})
NetRevenue = TotalRevenue - TotalVat - TotalPIT
```

### 5.7 S2b Template Redesign (W5-T7 — Service + Domain)

Per TT 152 S2b layout (4 industry groups × 2 fields each):

```
For each IndustrySector (4 groups):
  - Revenue_{sector} = SUM_ACCOUNT_BY_INDUSTRY("5", "Credit", "{sector}")
  - VatAmount_{sector} = Revenue_{sector} * vatRate_{sector}
TotalRevenue = SUM(Revenue_{all sectors})
TotalVat = SUM(VatAmount_{all sectors})
```

### 5.8 PIT Formula Fix (W5-T8 — included in T6)

`PersonalIncomeTax = TotalRevenue * pitRate` (NOT `VatAmount * 0.1`)
Default pitRate = 0.005 (0.5% — Distribution, lowest group) when sector is NULL.

### 5.9 Account Mapping Labels (W5-T9 — Service)

Fix `_vietnameseAccounts`:
| Code | Hiện tại (SAI) | Sửa thành |
|---|---|---|
| 211 | "Ngắn hạn vay ngân hàng" | "Tài sản cố định hữu hình" |
| 811 | "Lợi nhuận gộp về bán hàng" | "Xác định kết quả kinh doanh" |
| 821 | "Chi phí tài chính" | "Chi phí thuế TNDN" |
| 841 | "Lợi nhuận sau thuế" | **XÓA** (841 không tồn tại TT 200) |

Thêm: 311→"Vay ngắn hạn ngân hàng", 333→"Thuế và các khoản phải nộp nhà nước"
**KHÔNG thêm 5118** (không dùng — S2b redesign không tách goods-vs-service).

Fix stale comment L11: "Implements Vietnamese Accounting Standard (Thông tư 200/2014/TT-BTC)" → "Implements TT 88/2021/TT-BTC + TT 152/2025/TT-BTC (HKD single-entry). Account mapping is Internal Synthetic Mapping."

### 5.10 Hardening Gates
- [ ] **Build:** `dotnet build VanAn.sln` Release — 0 errors
- [ ] **Guard:** guard-check.ps1 PASSED
- [ ] **Tests:** all existing tests pass + new tests pass
- [ ] **Domain protection:** `IndustrySector` field added via constructor + factory only (immutable)
- [ ] **AccountingEntry immutability:** preserved (no setter for IndustrySector)
- [ ] **Migration:** EF Core migration created + applied to dev DB
- [ ] **Architecture tests:** 28/28 pass (VA-ARCH-001 allows Infrastructure Migrations)

---

## 6. SUCCESS CRITERIA

- [ ] **SC1:** `IndustrySector` enum exists in Domain with 4 values (Distribution, ProductionTransport, Service, OtherBusiness)
- [ ] **SC2:** `AccountingEntry.IndustrySector` field exists (nullable, immutable, set via factory)
- [ ] **SC3:** `Tenant.DefaultIndustrySector` field exists (nullable)
- [ ] **SC4:** `Order.IndustrySector` field exists (nullable, overrides Tenant default)
- [ ] **SC5:** EF Core migration adds `IndustrySector` column to `AccountingEntries` + `Tenants` + `Orders`
- [ ] **SC6:** `SUM_ACCOUNT_BY_INDUSTRY("5","Credit","Distribution")` DSL works in Formula Engine
- [ ] **SC7:** `IDataProvider.GetAccountSum` has overload with `industrySector` parameter
- [ ] **SC8:** `SmartPreAggregationService.GetAccountSumAsync` filters by `IndustrySector`
- [ ] **SC9:** `OrderService` passes `Order.IndustrySector ?? Tenant.DefaultIndustrySector` to `CreateRevenueEntryAsync`
- [ ] **SC10:** `HKDBookService.RecordRevenueAsync` accepts + persists `IndustrySector`
- [ ] **SC11:** S2a template has 4 industry groups × 3 fields (Revenue, VatAmount, PIT per group)
- [ ] **SC12:** S2b template has 4 industry groups × 2 fields (Revenue, VatAmount per group)
- [ ] **SC13:** PIT formula = `TotalRevenue * pitRate` (NOT `VatAmount * 0.1`)
- [ ] **SC14:** `_vietnameseAccounts` labels fixed (211/811/821/841) + stale "TT 200" comment corrected
- [ ] **SC15:** Tax rate lookup returns correct rates per IndustrySector (1%/0.5%, 3%/1.5%, 5%/2%, 2%/1%)
- [ ] **SC16:** NULL IndustrySector entries counted in OtherBusiness group
- [ ] **SC17:** Unit test: S2a PIT calculated on Revenue, not VatAmount
- [ ] **SC18:** Unit test: S2b Revenue split by IndustrySector (seed 2 sectors, verify separate sums)
- [ ] **SC19:** Unit test: S2a/S2b with NULL IndustrySector falls back to OtherBusiness group
- [ ] **SC20:** Unit test: FormulaEngine SUM_ACCOUNT_BY_INDUSTRY filters by sector
- [ ] **SC21:** `dotnet build VanAn.sln` Release — 0 errors
- [ ] **SC22:** `dotnet test` — all pass
- [ ] **SC23:** guard-check.ps1 PASSED

---

## 7. ACTIVE SKILLS (MAX 3)
- `dynamic-hkd-book-architecture` — HKD book domain knowledge + TT 152 layout
- `domain-integrity-validation` — Verify IndustrySector field + immutability + migration
- `einvoice-integration` — Tax rate lookup pattern reference

---

## 8. AI HEALTH CHECK MATRIX (INITIAL)
- **Evidence Count:** 15+ verified facts from codebase investigation
- **Verified Facts:**
  - Fact 1: No production write path writes 512/521/5118 (grep confirmed)
  - Fact 2: OrderService writes "511" for all revenue (L108)
  - Fact 3: RecordRevenueAsync writes NULL AccountCode (L59)
  - Fact 4: TT 152 S2a/S2b split by industry sector (5 groups), not goods-vs-service (extracted .docx)
  - Fact 5: Formula Engine DSL only supports 2 params (SUM_ACCOUNT pattern, side)
  - Fact 6: IDataProvider.GetAccountSum has no sector filter
  - Fact 7: SmartPreAggregationService filters by AccountCode.StartsWith OR EntryType heuristic
  - Fact 8: IHKDTaxClassificationService has no GetVatRate(industry) method
  - Fact 9: HKDRevenueClassificationService thresholds are WRONG (500M — should be 1B per 2026)
  - Fact 10: Tenant.IndustrySector MISSING (W0-T10 confirmed)
  - Fact 11: EF Core Migrations enabled (Stream E complete)
  - Fact 12: AccountingEntry is immutable (factory methods, no setters)
  - Fact 13: 4 tax rate groups per Luật 2025 + ND 117/2025 (1%/0.5%, 3%/1.5%, 5%/2%, 2%/1%)
  - Fact 14: Amendment 5a — HKD = single-entry, Internal Synthetic Mapping (not TT 200)
  - Fact 15: S2c/S2d/S2e/S3a have additional structural gaps (deferred to follow-up streams)
- **Assumptions:**
  - Tenant.DefaultIndustrySector + Order.IndustrySector sufficient for MVP (per-order override)
  - 4 industry sectors cover all HKD use cases (TT 152 allows up to 5 groups, but 4 per Luật 2025)
  - Default rate 0.5% PIT (Distribution) acceptable for NULL sector fallback → OtherBusiness (1% PIT)
- **Open Questions:** RESOLVED
  - Q1: RESOLVED — Both Tenant + Order have IndustrySector (Order overrides Tenant)
  - Q2: RESOLVED — 4 sectors per Luật 2025 (TT 152's 5th group = OtherBusiness)
  - Q3: RESOLVED — NULL → OtherBusiness mapping

---

## 9. REVERSE IMPACT ANALYSIS

| File modify | Reverse impact | Mitigation |
|---|---|---|
| `AccountingEntry` + `IndustrySector` field | All callers of `CreateRevenue`/`CreateExpense` must pass new param | Optional param with default NULL — backward compatible |
| `Tenant` + `DefaultIndustrySector` | Tenant onboarding flow must set this | Nullable — existing tenants get NULL, UI prompt in Wave 8 |
| `Order` + `IndustrySector` field | Order creation flow must support sector | Nullable — existing orders get NULL, falls back to Tenant default |
| EF Migration | Dev DB gets new column | Stream E migrations working — low risk |
| `IDataProvider.GetAccountSum` overload | All implementations must add overload | 3 implementations (Scoped, DataProviderService, test mock) |
| `SmartPreAggregationService` filter | Existing queries still work (NULL sector = OtherBusiness) | Backward compatible — sector filter applies via NULL→OtherBusiness mapping |
| `ProductionFormulaEngine` new DSL | Existing formulas unaffected | New function `SUM_ACCOUNT_BY_INDUSTRY` — old `SUM_ACCOUNT` unchanged |
| S2a/S2b template redesign | `NumericValues` keys change (Revenue_Distribution etc.) | Wave 6 tests + Wave 8 UI must use new keys |
| `OrderService` passes IndustrySector | All orders now carry sector info | From `Order.IndustrySector ?? Tenant.DefaultIndustrySector` |
| `_vietnameseAccounts` label fix | Display labels change in General Ledger | No logic impact — display only |
| `HKDBookService` comment fix | No impact | Documentation only |

---

## 10. TDD & TESTING STRATEGY

- **Unit tests (NEW — 4 tests):**
  1. `S2aBook_PersonalIncomeTax_ShouldCalculateOnRevenue_NotOnVat`
     - Arrange: Tenant with `DefaultIndustrySector = Distribution`, seed Revenue 1000 (AccountingEntry with IndustrySector=Distribution)
     - Act: `GenerateS2aBookAsync`
     - Assert: `NumericValues["PIT_Distribution"] == 5m` (1000 * 0.005), NOT `0.5m` (10 * 0.1 — old formula)
  2. `S2bBook_Revenue_ShouldSplitByIndustrySector`
     - Arrange: Tenant with `DefaultIndustrySector = Service`, seed 2 entries: Revenue 600 (Distribution) + Revenue 400 (Service)
     - Act: `GenerateS2bBookAsync`
     - Assert: `NumericValues["Revenue_Distribution"] == 600m`, `NumericValues["Revenue_Service"] == 400m`, `NumericValues["TotalRevenue"] == 1000m`
  3. `S2aBook_WithNullIndustrySector_ShouldMapToOtherBusiness`
     - Arrange: Tenant with `DefaultIndustrySector = null`, seed Revenue 1000 (IndustrySector=NULL)
     - Act: `GenerateS2aBookAsync`
     - Assert: `NumericValues["Revenue_OtherBusiness"] == 1000m`, `NumericValues["PIT_OtherBusiness"] == 10m` (1000 * 0.01)
  4. `FormulaEngine_SumAccountByIndustry_ShouldFilterBySector`
     - Arrange: Seed 2 entries: Revenue 600 (Distribution) + Revenue 400 (Service)
     - Act: Evaluate `SUM_ACCOUNT_BY_INDUSTRY("5", "Credit", "Distribution")`
     - Assert: Result == 600m (not 1000m)

- **Integration tests:** Update `TestDataSeeder` to set `IndustrySector` on seed entries
- **E2E tests:** N/A (Wave 8)
- **Verification:** `dotnet build VanAn.sln` Release + `dotnet test` pass + guard-check.ps1

---

## 11. JIT PLANNING + PURE EXECUTION (MICRO-PHASES)

### Chiến lược: Domain first → Migration → Formula Engine → Service → Templates → Tests

| Session | JIT Planning | Pure Execution |
|---|---|---|
| **S1** (W5-T1/T2/T3 — Domain) | Confirm enum values, field names, factory method signatures | Add `IndustrySector` enum to `Domain.cs`, add field to `AccountingEntry` + `Tenant` + `Order`, update factory methods, update DTO |
| **S2** (W5-T4 — Migration + Formula Engine) | Confirm DSL syntax, IDataProvider overload signature | Create EF migration, add `SUM_ACCOUNT_BY_INDUSTRY` to `ProductionFormulaEngine`, add `IDataProvider.GetAccountSum` overload, implement in `ScopedDataProvider` + `DataProviderService`, update `SmartPreAggregationService` |
| **S3** (W5-T5/T8/T9 — Tax rates + labels) | Confirm 4-group rate table, label fixes | Add tax rate lookup to `IHKDTaxClassificationService` + implementation, fix `_vietnameseAccounts`, fix stale comment |
| **S4** (W5-T6/T7 — Templates) | Confirm S2a/S2b field structure per TT 152 | Redesign `S2aHKDTemplate`/`S2bHKDTemplate` (Domain) + `S2aHKDTemplateImpl`/`S2bHKDTemplateImpl` (Service) with 4 industry groups |
| **S5** (W5-T10 — Production write path) | Confirm OrderService passes Order.IndustrySector ?? Tenant.DefaultIndustrySector | Update `OrderService`, `AccountingEntryService`, `HKDBookService.RecordRevenueAsync` |
| **S6** (Tests + verify) | Confirm test scenarios | Add 4 unit tests, update seed data, run `dotnet build` + `dotnet test` + guard-check, commit |

### Rules
- KHÔNG làm W5-T1/T2/T3 (Domain) trước Tech Lead approval (HARD STOP) — **APPROVAL GRANTED 2026-07-03**
- 1 layer tại 1 thời điểm — build verify sau mỗi session
- Migration must be created + applied before Service layer changes

---

## 12. ESTIMATED EFFORT
- 3-4 sessions (Domain + Migration + Formula Engine + Service + Templates + Tests)
- **BLOCKER:** Wave 5c proper (2026 threshold 500M→1B, TNCN formulas Nhóm 2/3/4), Wave 6 (tests), Wave 7 (API), Wave 8 (UI)
- **Tech Lead approval:** GRANTED 2026-07-03 — Domain modification (add `IndustrySector` field to `AccountingEntry` + `Tenant.DefaultIndustrySector` + `Order.IndustrySector`)
- **Deferred to Wave 5c proper:** Threshold fix (500M→1B in `HKDRevenueClassification.CalculateGroup` + `HKDRevenueClassificationService`), TNCN formulas Nhóm 2/3/4, thuế khoán abolished, lệ phí môn bài abolished
- **Deferred to follow-up streams:** S2c 6 expense sub-categories, S2d per-item inventory, S2e opening balance, S3a 10 tax columns

---

## 13. DEFERRED ITEMS (NOT IN THIS WAVE)

| Item | Reason | Target |
|---|---|---|
| Threshold 500M→1B in `HKDRevenueClassification.CalculateGroup` | 2026 regulatory — separate legal review | Wave 5c proper |
| Threshold in `HKDRevenueClassificationService` | Same | Wave 5c proper |
| TNCN formulas Nhóm 2/3/4 (`(Doanh thu - 1B) × rate` etc.) | Needs threshold fix first | Wave 5c proper |
| Thuế khoán abolished | Legal declaration | Wave 5c proper |
| Lệ phí môn bài abolished | Legal declaration | Wave 5c proper |
| S2c 6 expense sub-categories | Needs `ExpenseCategory` enum (Domain mod) | Follow-up stream |
| S2d per-item inventory tracking | Needs inventory item entity | Follow-up stream |
| S2e opening balance | Needs period-opening logic | Follow-up stream |
| S3a 10 tax columns | Needs tax-per-item tracking | Follow-up stream |

---

## 14. GOVERNANCE FLAGS

1. **Domain modification (HARD STOP):** Adding `IndustrySector` to `AccountingEntry` (immutable entity) + `Tenant` + `Order` — Tech Lead approval GRANTED 2026-07-03.
2. **AccountingEntry immutability preserved:** Field added via constructor + factory method only. No public setter. Existing entries get NULL (backward compatible).
3. **EF Core Migration:** Stream E enabled migrations at Infrastructure layer (VA-ARCH-001 modified). Migration is allowed.
4. **Formula Engine DSL extension:** New function `SUM_ACCOUNT_BY_INDUSTRY` — does not break existing `SUM_ACCOUNT` (backward compatible).
5. **Scope expansion acknowledgment:** This wave merges original 5a + 5b + partial 5c. The per-wave merge strategy is preserved (this is still one wave, merged to main when complete). Wave 5c proper (thresholds + TNCN formulas) remains separate.
6. **NULL → OtherBusiness mapping:** Ensures backward compatibility with existing entries while maintaining `TotalRevenue = SUM(all sector revenues)` invariant.
