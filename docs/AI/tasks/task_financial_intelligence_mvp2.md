# TASK CARD: Financial Intelligence MVP-2 — Sprint A

## 1. GOAL & CONTEXT
- **Mục tiêu cốt lõi:** Implement Break-even + Unit Economics + Financial Dashboard cho tenant HKD/SME nhỏ — biến số liệu kế toán đã có thành decision support cho chủ doanh nghiệp.
- **Context:** Foundation 80% đã có (`IncomeStatementService`, `TrialBalanceService`, `IAccountChartService`, `Product.CostPrice`). Chỉ thêm 1 entity `BusinessProfile` (fixed costs) + 5 calculation services + 1 dashboard. Pure additive, không break existing code.
- **Source SRS:** `docs/requirements/Van_An_SRS_Financial_Intelligence_MVP2.md` (863 dòng, đã review)
- **Master plan:** `docs/AI/tasks/master_plan_financial_intelligence_mvp2_va_iie.md`
- **Branch:** `feature/financial-intelligence-mvp2`
- **Status:** PENDING USER APPROVAL

---

## 2. ACTIVE WORKFLOW ROUTING
- **Target Workflow:** `newfeaturebuild.md` (ANALYZE → IMPLEMENT — 7 step)
- **Execution Mode:** ANALYZE (đã xong trong SRS) → IMPLEMENT (sau approval)
- **Skill matrix (max 3):** `accounting-ui-implementation` + `domain-integrity-validation` + `system-refactor-safety`

---

## 3. RELEVANT FILES (CONTEXT BOUNDARY)

### Architecture decision (approved 2026-08-21)
- **Option B:** MVP-2 services vào `3_CoreHub/Services/FinancialIntelligence/` subfolder + namespace `VanAn.CoreHub.Services.FinancialIntelligence`. NO new csproj. Match precedent `Journal/`, `Template/`, `Reports/`, `Orchestration/`.
- **Option 2 (A1 resolution):** Extend `IncomeStatement` record với 4 additive fields `TotalCogsEnding`, `TotalCogsOpening`, `TotalOpExEnding`, `TotalOpExOpening` (default = 0m). DRY — single source of truth COGS extraction.

### Files cần CREATE (new)
| Path | Role |
|---|---|
| `1_Shared/Domain/BusinessProfile.cs` (or Domain.cs append) | Entity mới (Single-Identity compliant) |
| `1_Shared/Domain/ValueObjects/FinancialModelVersion.cs` | VO record struct |
| `1_Shared/Domain/FinancialIntelligence/*Records.cs` | 5 result records (ProfitSummary, BreakEvenAnalysis, MultiProductBreakEven, UnitEconomicsReport, TargetProfitAnalysis) + enums |
| `3_CoreHub/Infrastructure/Entities/BusinessProfileEntity.cs` (or use Domain directly) | EF persistence |
| `3_CoreHub/Infrastructure/Configurations/BusinessProfileConfiguration.cs` | EF config (Ignore BusinessProfileId, unique index TenantId) |
| `3_CoreHub/Infrastructure/Migrations/2026082XXX_AddBusinessProfile.cs` | Migration |
| `3_CoreHub/Infrastructure/Repositories/BusinessProfileRepository.cs` + `IBusinessProfileRepository.cs` | Repository |
| `3_CoreHub/Services/FinancialIntelligence/IBusinessProfileService.cs` + impl | Service (namespace `VanAn.CoreHub.Services.FinancialIntelligence`) |
| `3_CoreHub/Services/FinancialIntelligence/IProfitSummaryService.cs` + impl | Service (wrap IncomeStatementService — read COGS/OpEx from extended record) |
| `3_CoreHub/Services/FinancialIntelligence/IBreakEvenAnalysisService.cs` + impl | Service (single + multi-product) |
| `3_CoreHub/Services/FinancialIntelligence/IUnitEconomicsService.cs` + impl | Service (per-product ranking) |
| `3_CoreHub/Services/FinancialIntelligence/ITargetProfitService.cs` + impl | Service |
| `2_Gateway/Controllers/FinancialIntelligenceController.cs` | 7 endpoints — inject services trực tiếp (bypass W8 VAS feature flag cho HKD tenant) |
| `2_Gateway/Controllers/Dtos/Financial/*.cs` | DTOs (camelCase JSON) |
| `5_WebApps/ShopERP/Services/FinancialIntelligenceHttpService.cs` | HTTP proxy |
| `5_WebApps/ShopERP/Components/Pages/Admin/BusinessProfile.razor` + `.cs` | CRUD form |
| `5_WebApps/ShopERP/Components/Pages/Financial/Dashboard.razor` + `.cs` | 5 widgets |
| `5_WebApps/ShopERP/Components/Pages/Financial/BreakEven.razor` + `.cs` | Detail + chart + export PDF |
| `5_WebApps/ShopERP/Components/Pages/Financial/UnitEconomics.razor` + `.cs` | Table + filter + export Excel |
| `6_Tests/VanAn.Core.Tests/Services/FinancialIntelligence/*Tests.cs` | Unit tests |
| `6_Tests/VanAn.Integration.Tests/FinancialIntelligenceEndpointsTests.cs` | Integration tests |
| `6_Tests/VanAn.ShopERP.Tests/Components/Financial/DashboardTestBase.cs` | Test fixture for MVP-2 (extend with realistic COGS/OpEx values — see §4) |
| `6_Tests/e2e-tests/financial-dashboard.spec.ts` | Playwright E2E |

### Files cần MODIFY (existing)
| Path | Change |
|---|---|
| `1_Shared/Domain.cs:3531` (IncomeStatement record) | **Option 2 approved:** Thêm 4 additive fields `TotalCogsEnding`, `TotalCogsOpening`, `TotalOpExEnding`, `TotalOpExOpening` — tất cả `= 0m` default. KHÔNG thêm OtherIncome/OtherExpense (MVP-2 không cần). |
| `3_CoreHub/Services/IncomeStatementService.cs:165` (TT99 return) | Thêm `TotalCogsEnding: m02e, TotalCogsOpening: m02o, TotalOpExEnding: m11e, TotalOpExOpening: m11o` vào `new IncomeStatement(...)`. Variables đã compute sẵn trong method. |
| `3_CoreHub/Services/IncomeStatementService.cs:244` (flat return) | Thêm `TotalCogsEnding: cogsEnding, TotalCogsOpening: cogsOpening, TotalOpExEnding: opexEnding, TotalOpExOpening: opexOpening`. Variables `cogsEnding/cogsOpening/opexEnding/opexOpening` đã compute sẵn (lines 184-185, 213-223). |
| `2_Gateway/Program.cs` | DI register 5 services + controller |
| `5_WebApps/ShopERP/Program.cs` | DI register `FinancialIntelligenceHttpService` |
| `5_WebApps/ShopERP/Components/Layout/AdminLayout.razor` | NavMenu entry "Thông tin tài chính" |
| `5_WebApps/ShopERP/Components/Pages/Sitemap.razor` | Sitemap card "Phân tích tài chính" |
| `6_Tests/VanAn.Architecture.Tests/W12G7ControllerAuthorizationTests.cs` | Add `FinancialIntelligenceController` to authorized list |
| `6_Tests/VanAn.ShopERP.Tests/Components/VasReports/VasReportPageTestBase.cs:108` | **Optional update:** Set `TotalCogsEnding: 7_000_000m, TotalOpExEnding: 2_000_000m` cho fixture consistency với Lines (line 96 mã 20 = 7M COGS, line 98 mã 40 = 2M OpEx). **Không bắt buộc** — existing fixture compile OK với default = 0 (named args + default values). Existing render tests dùng `Lines` trực tiếp, không break. Update chỉ cho consistency khi MVP-2 dashboard tests cần assert record fields. |

### Files READ ONLY (reference, không đổi)
- `3_CoreHub/Services/IncomeStatementService.cs` (logic read-only — chỉ modify 2 return statements)
- `3_CoreHub/Services/TrialBalanceService.cs` — reuse via `ITrialBalanceService`
- `3_CoreHub/Services/IAccountChartService.cs` — reuse cho account code → name
- `1_Shared/Domain.cs:287` — `AccountingEntry` (immutable, chỉ đọc)
- `1_Shared/Domain.cs:537` — `Product.CostPrice` (variable cost proxy)
- `3_CoreHub/Repositories/IOrderRepository.cs` — `GetByDateRangeAsync(tenantId, startDate, endDate, ct)` (Q1 resolved — dùng existing, không thêm method)
- `3_CoreHub/Infrastructure/Entities/ShopFeatureSettingsEntity.cs` — pattern precedent (tenant-scoped Infrastructure entity)
- `5_WebApps/ShopERP/Services/NetworkDashboardHttpService.cs` — HTTP proxy precedent
- `2_Gateway/Controllers/OcrConfigController.cs` — `[Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]` precedent (Pattern #10)
- `5_WebApps/ShopERP/Controllers/IncomeStatementsController.cs:44` — **W8 feature flag** reference (HKD tenant blocked — MVP-2 controller inject service trực tiếp, KHÔNG qua IncomeStatementsController)

### Files NOT CHANGED
- `AccountingEntry` — immutable in all modes (hard stop)
- `IncomeStatementService` logic COGS extraction — không refactor (Option 2 chỉ expose fields đã compute, không duplicate logic)
- `TrialBalanceService`, `CashFlowStatementService`, `BalanceSheetService` — không refactor, chỉ consume
- Migrations hiện có — không touch (chỉ thêm migration AddBusinessProfile mới)

---

## 4. TECHNICAL & REGULATORY CONSTRAINTS
- [ ] **Domain modification APPROVED (Option 2):** Extend `IncomeStatement` record tại Domain.cs:3531 với 4 additive fields `TotalCogsEnding`, `TotalCogsOpening`, `TotalOpExEnding`, `TotalOpExOpening` (default = 0m). Precedent: VALCN v2.0 Phase 1 đã thêm `AccountingEntry.CorrelationId` + `Order.PlatformFeeAmount` cùng pattern additive. Approval: user 2026-08-21.
- [ ] **NO modification to AccountingEntry** (immutable hard stop — kept intact)
- [ ] **Single-Identity Pattern:** `BusinessProfileConfiguration.Ignore(e => e.BusinessProfileId)`, constructor set `Id = BusinessProfileId.Value`
- [ ] **Domain purity:** No EF Core, no DbContext, no DataAnnotations trong Domain layer
- [ ] **AccountingEntry immutable:** Services chỉ ĐỌC AccountingEntry/JournalEntry, không ghi (BR-001/002)
- [ ] **BR-005 traceability:** Mọi result record có `SourceAccountCodes[]`
- [ ] **BR-006 versioning:** `BusinessProfile.Version` increment on update (FinancialModelVersion.Increment)
- [ ] **Trust Level 1 only:** Pure deterministic calculation — no AI, no statistical (NFR-14)
- [ ] **Auth:** `[Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]` class-level (Pattern #10 + OcrConfigController precedent)
- [ ] **W8 feature flag bypass:** `FinancialIntelligenceController` inject `IIncomeStatementService` trực tiếp (services layer), KHÔNG qua `IncomeStatementsController` (API layer — blocked HKD tenant). Match precedent: NetworkDashboardService inject services trực tiếp.
- [ ] **Namespace `VanAn.CoreHub.Services.FinancialIntelligence`:** MVP-2 services trong `3_CoreHub/Services/FinancialIntelligence/` subfolder (Option B). Match precedent `Journal/`, `Template/`, `Reports/`, `Orchestration/`.
- [ ] **ShopERP HTTP proxy only:** `FinancialIntelligenceHttpService` → Gateway, KHÔNG inject `IVanAnDbContext` (NetworkDashboardHttpService precedent)
- [ ] **Currency:** decimal(18,2) VND — no float
- [ ] **i18n:** Tất cả UI label tiếng Việt (NFR-12)
- [ ] **UI Platform 100%:** VanAMetricsCard, VanAStatusForm, VanAButton, VanAList — no raw HTML/CSS bypass (NFR-11, Gate 5)
- [ ] **E2E test:** Gate 4 — UI layout change → Playwright spec
- [ ] **Pattern #10 charset strip:** Nếu controller forward request body — strip `; charset=utf-8` trước khi pass ContentType
- [ ] **Backward compat IncomeStatement:** 4 new fields có default = 0m → existing 3 `new IncomeStatement(...)` call sites compile OK (named args + default values). Existing test fixture `VasReportPageTestBase.cs:108` compile OK. Existing render tests dùng `Lines` → không break.

---

## 5. PHASED EXECUTION (per SRS §8)

### Phase 1 — Foundation (3-4 ngày)
- [ ] P1.0: **IncomeStatement record extension (Option 2 approved):** Thêm 4 fields `TotalCogsEnding`, `TotalCogsOpening`, `TotalOpExEnding`, `TotalOpExOpening` (default = 0m) tại `1_Shared/Domain.cs:3531`. Update 2 return statements trong `IncomeStatementService.cs` (line 165 TT99 + line 244 flat) expose values đã compute sẵn.
- [ ] P1.1: Domain — `BusinessProfile` entity + `BusinessProfileId` VO + `FinancialModelVersion` VO + `PricingModel` enum
- [ ] P2.2: Domain — 5 result records + 2 enums (`BreakEvenStatus`, `ProfitStatus`) + `ProductBreakEvenLine`, `UnitEconomicsLine`
- [ ] P1.3: EF config `BusinessProfileConfiguration` (Ignore BusinessProfileId, unique index TenantId, decimal(18,2), PricingModel string conversion, FinancialModelVersion string conversion)
- [ ] P1.4: Migration `AddBusinessProfile` — CreateTable + unique index
- [ ] P1.5: `IBusinessProfileRepository` + impl
- [ ] P1.6: `IBusinessProfileService` + impl (Get/GetOrCreateDefault/Update)
- [ ] P1.7: DI registration (Gateway + ShopERP)
- [ ] P1.8: Unit tests — BusinessProfile.Create/Update, FinancialModelVersion.Increment, TotalMonthlyFixedCost computed, validation (Clamp, Max(0))
- [ ] P1.9: **Update existing test fixture** `VasReportPageTestBase.cs:108` — set `TotalCogsEnding: 7_000_000m, TotalOpExEnding: 2_000_000m` cho consistency với Lines (optional, but recommended cho realistic MVP-2 dashboard tests)

### Phase 2 — Calculation Services (4-5 ngày)
- [ ] P2.1: `IProfitSummaryService` + impl (wrap IncomeStatementService → extract Revenue/COGS/OpEx/NetProfit). **A1 RESOLVED:** IncomeStatement record hiện không expose COGS/OpEx/OtherIncome/OtherExpense (chỉ TotalRevenue + NetProfit + raw Lines). Option 1: replicate COGS extraction logic `accountCode.StartsWith("632")` trong service. Option 2 (khuyến nghị): extend `IncomeStatement` record thêm 4 additive fields + sửa IncomeStatementService expose — cần Domain modification approval.
- [ ] P2.2: `IBreakEvenAnalysisService.AnalyzeAsync` (single product) — formula: BreakEvenRevenue = FixedCost / CMRatio, BreakEvenUnits = FixedCost / (AvgPrice − AvgVarCost), MarginOfSafety. VariableCost = COGS từ P2.1.
- [ ] P2.3: `IBreakEvenAnalysisService.AnalyzeMultiProductAsync` — weighted average CM, per-product allocation. Per-product VariableCost = Product.CostPrice (fallback 70% UnitPrice — match OrderService.CalculateCogsAmount line 258).
- [ ] P2.4: `IUnitEconomicsService.AnalyzeAsync` — load Products + Orders (period) → aggregate `order.Items.GroupBy(i => i.ProductId)` in-memory → per-product CM + ranking + missing CostPrice flag. **Q1 RESOLVED:** dùng `IOrderRepository.GetByDateRangeAsync(tenantId, periodStart, periodEnd, ct)` (existing) — KHÔNG cần thêm repository method.
- [ ] P2.5: `ITargetProfitService.AnalyzeAsync` — RequiredRevenue = (FixedCost + TargetProfit) / CMRatio, RequiredUnits, RequiredDaily, Feasibility vs Capacity
- [ ] P2.6: Guard conditions — 6 codes (PROFILE_MISSING, INSUFFICIENT_DATA, COST_PRICE_MISSING, CM_RATIO_ZERO_OR_NEG, CAPACITY_EXCEEDED, FIXED_COST_ZERO)
- [ ] P2.7: Unit tests — mỗi service ≥ 3 tests (happy path + InsufficientData + edge case CM=0)
- [ ] P2.8: Integration tests — real IncomeStatement data → break-even end-to-end, cross-check với IncomeStatementService output

### Phase 3 — API + HTTP Proxy (2-3 ngày)
- [ ] P3.1: `FinancialIntelligenceController` — 7 endpoints (GET/PUT business-profile, GET profit-summary, GET break-even, GET break-even/multi-product, GET unit-economics, POST target-profit)
- [ ] P3.2: DTOs (camelCase JSON, map records → DTOs)
- [ ] P3.3: `[Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]` class-level
- [ ] P3.4: Pattern #10 charset strip (if any forward variant — likely not, but audit)
- [ ] P3.5: `FinancialIntelligenceHttpService` (ShopERP) — 6 methods (proxy to Gateway)
- [ ] P3.6: W12-G7 arch test — add controller to authorized list
- [ ] P3.7: Integration tests — 401 no-auth, 200 with JWT, 404 missing profile, upsert profile

### Phase 4 — UI (3-4 ngày)
- [ ] P4.1: `/admin/business-profile` form (7 fixed costs + capacity + pricing + notes) — VanAStatusForm
- [ ] P4.2: `/financial` dashboard — 5 widgets (ProfitSummary, BreakEven status, Top 5, Bottom 5, Target Profit calculator) — VanAMetricsCard
- [ ] P4.3: `/financial/break-even` — single + multi-product table + chart + export PDF
- [ ] P4.4: `/financial/unit-economics` — sortable table + filter by Category + warning row highlight + export Excel
- [ ] P4.5: Period picker (month + year dropdown)
- [ ] P4.6: Warning banner (CostPrice missing, FixedCost zero, CM ratio negative)
- [ ] P4.7: NavMenu entry "Thông tin tài chính" + Sitemap card "Phân tích tài chính"
- [ ] P4.8: UI Platform compliance check (no raw HTML/CSS bypass)
- [ ] P4.9: Playwright E2E — setup flow (create profile → redirect dashboard), dashboard render, target profit calculator, period switch

### Phase 5 — Polish (1-2 ngày)
- [ ] P5.1: Export PDF (break-even + unit economics) — use existing `RevenueExcelReport` / iTextSharp pattern
- [ ] P5.2: Export Excel (unit economics) — use `InventoryExcelReport` pattern
- [ ] P5.3: i18n review — all labels tiếng Việt
- [ ] P5.4: Admin guide doc (markdown) — "Cách dùng Phân tích tài chính" cho Owner
- [ ] P5.5: Final guard-check + build + test gate

---

## 6. SUCCESS CRITERIA (SRS §10)
- [ ] **SC1:** Owner khai báo BusinessProfile (7 fixed costs + capacity + pricing) → save thành công
- [ ] **SC2:** Owner update BusinessProfile → Version increment (BR-006)
- [ ] **SC3:** `/financial` hiển thị 5 widgets cho kỳ hiện tại
- [ ] **SC4:** Period picker đổi kỳ → widgets refresh
- [ ] **SC5:** ProfitSummary đúng số liệu từ IncomeStatement (cross-check)
- [ ] **SC6:** BreakEven: Revenue > BreakEven → `AboveBreakEven`; < → `BelowBreakEven`; ≈ → `AtBreakEven`
- [ ] **SC7:** Multi-product: Σ ProductBreakEvenUnits ≈ total (tolerance 5%)
- [ ] **SC8:** UnitEconomics: products sorted by ProfitContribution DESC
- [ ] **SC9:** Products CostPrice=0 → `HasMissingCostPrice` flag + warning banner
- [ ] **SC10:** TargetProfit: nhập 50M → RequiredRevenue + RequiredUnits + feasibility badge
- [ ] **SC11:** RequiredDaily > Capacity → `Feasible = false` + warning
- [ ] **SC12:** Export PDF break-even + export Excel unit economics
- [ ] **SC13:** Guard: PROFILE_MISSING → redirect setup + banner
- [ ] **SC14:** Guard: INSUFFICIENT_DATA → widget "Chưa có dữ liệu kỳ này"
- [ ] **SC15:** Guard: CM ≤ 0 → critical warning "Biên đóng góp âm"
- [ ] **SC16:** Guard: FixedCost = 0 → warning "Chưa nhập fixed costs"
- [ ] **SC17:** NFR-1: BreakEven < 500ms (cold cache, ≤ 1000 AccountingEntry)
- [ ] **SC18:** NFR-2: UnitEconomics < 1s (≤ 200 products, ≤ 5000 OrderItem)
- [ ] **SC19:** NFR-4: Multi-tenancy — tenant A không thấy data tenant B
- [ ] **SC20:** NFR-5: Staff role không truy cập `/financial`
- [ ] **SC21:** NFR-7: Domain pure (no EF Core in Domain)
- [ ] **SC22:** NFR-8: Single-Identity — `BusinessProfileConfiguration.Ignore(e => e.BusinessProfileId)`
- [ ] **SC23:** NFR-10: `guard-check.ps1` + `dotnet build VanAn.sln` PASS
- [ ] **SC24:** NFR-11: 100% UI Platform components
- [ ] **SC25:** NFR-14: Chỉ Level 1 deterministic (no AI/statistical)
- [ ] **SC26:** W12-G7: Controller có class-level `[Authorize]`
- [ ] **SC27:** Layer direction: API → Services → Domain (no reverse)
- [ ] **SC28:** ShopERP không inject `IVanAnDbContext` cho MVP-2

---

## 7. AI HEALTH CHECK MATRIX
- **Evidence Count:** 15 (A1 + Q1 + Q2 + W8 feature flag — all RESOLVED 2026-08-21)
- **Verified Facts:**
  - F1: `IncomeStatementService.GenerateAsync(tenantId, period, standard, ct)` exists + returns `IncomeStatement` record. Verified (file read 2026-08-20).
  - F2: `ITrialBalanceService.GenerateAsync(tenantId, period, standard, ct)` exists + returns `TrialBalance`. Verified.
  - F3: `IAccountChartService.GetAccountTypeAsync(code, standard)` exists — for COGS/Revenue account classification. Verified.
  - F4: `Product.CostPrice` field exists (decimal, default 0). Verified Domain.cs:543.
  - F5: `AccountingEntry` sealed + immutable + AccountCode field (TT 152). Verified Domain.cs:287-319.
  - F6: `AccountingPeriod` record(int Year, int Month) + `FromDateTime`. Verified Domain.cs:105.
  - F7: `AccountingStandard` enum (TT99_2025, TT133_2016, TT58_2026). Verified Domain.cs:3495.
  - F8: `AccountChartEntry` record (AccountCode, Name, Type, Standard, IsNormalCredit). Verified Domain.cs:3608.
  - F9: `ShopFeatureSettingsEntity` precedent — tenant-scoped Infrastructure entity, private ctor + factory + UpdateXxx method. Verified (full file read).
  - F10: `NetworkDashboardHttpService` precedent — ShopERP HTTP proxy to Gateway. Verified (project_state archive).
  - F11: `OcrConfigController` precedent — `[Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]` class-level (Pattern #10 fix). Verified governance registry.
  - F12: `IncomeStatement` record has `TotalRevenueEnding`, `NetProfitEnding`, `Lines` (FinancialStatementLine). Verified Domain.cs:3531.
  - F13 (A1 RESOLVED): `IncomeStatementService.GenerateFlatAsync` (lines 180-251) đã extract COGS internally — `cogsEnding`/`cogsOpening` computed bằng `accountCode.StartsWith("632")` (TK 632). OpEx = `64`/`641`/`642`. **Solution Option 2 approved (2026-08-21):** extend `IncomeStatement` record với 4 additive fields + update 2 return statements expose values đã compute.
  - F14 (Q1 RESOLVED): `IOrderRepository.GetByDateRangeAsync(tenantId, startDate, endDate, ct)` exists — dùng cho MVP-2 (period → startDate/endDate). KHÔNG có `IOrderItemRepository` — OrderItem là aggregate child, truy xuất qua `Order.Items` navigation. KHÔNG cần thêm repository method.
  - F15 (W8 feature flag finding): `IncomeStatementsController.cs:44` block HKD tenant qua `IVasFeatureFlagService.CanAccessVasReportsAsync` → 403. **MVP-2 target audience = HKD** → `FinancialIntelligenceController` inject `IIncomeStatementService` trực tiếp (services layer), KHÔNG qua IncomeStatementsController. Match precedent NetworkDashboardService.
  - F16 (test fixture safety): `VasReportPageTestBase.cs:108` BuildSampleIncomeStatement dùng **named arguments** (`TotalRevenueEnding:`, `NetProfitEnding:`, `Lines:`). Verified — thêm 4 fields với default = 0m → compile OK, existing tests không break (render dùng `Lines`). Optional update fixture cho consistency khi MVP-2 dashboard tests cần assert record fields.
- **Assumptions:** 0
- **Open Questions:** 0
- **Architecture decisions APPROVED (2026-08-21):**
  - Option B: `3_CoreHub/Services/FinancialIntelligence/` subfolder + namespace (no new csproj)
  - Option 2: Extend `IncomeStatement` record với 4 additive fields
  - W8 bypass: MVP-2 controller inject service trực tiếp (not via IncomeStatementsController)
- **Recommended Action:** PROCEED TO IMPLEMENT — Assumptions (0) < Verified Facts (16), Open Questions (0) < 3. Gate 1 + Gate 6 PASSED. All decisions resolved.

---

## 8. RISK & MITIGATION (SRS §9 tóm tắt)
| Risk | Mitigation |
|---|---|
| Tenant chưa nhập CostPrice | Warning `COST_PRICE_MISSING` + link update + "estimated" flag |
| Fixed cost nhập sai | Notes field + Version tracking + "ước tính" label |
| IncomeStatement rỗng (tenant mới) | Guard `INSUFFICIENT_DATA` + onboarding hint |
| Multi-product break-even phức tạp | UI tooltip + "simple mode" default + giải thích từng bước |
| CostPrice ≠ actual COGS (variance/waste) | Note "CostPrice ước tính — dùng VA-IIE Phase 2 cho actual COGS" |
| Performance chậm | Cache per-period IMemoryCache 10-min TTL (NetworkDashboardService precedent) |
| Tenant expect AI advise | Clear messaging "MVP-2 chỉ tính toán" + roadmap transparency |

---

## 9. POST-IMPLEMENT (RV plan — not in this card)
Sau merge + deploy:
1. RV L1 (API): 7 endpoints — 401 no-auth, 200 with JWT, 404 missing profile, upsert
2. RV L2 (Static): Blazor boot hash, no stale WASM
3. RV L3 (Playwright): dashboard render, target profit calc, period switch, export PDF/Excel
4. RV L4 (Manual browser): Owner flow — create profile → dashboard → break-even → unit economics → target profit
5. RV L5 (VPS SSH): container healthy, migration applied, BusinessProfiles table exists
