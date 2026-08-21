# **ĐẶC TẢ YÊU CẦU NGHIỆP VỤ VÀ KỸ THUẬT (SRS)**

## **FINANCIAL INTELLIGENCE — MVP-2 — BREAK-EVEN + UNIT ECONOMICS + FINANCIAL DASHBOARD (VA-FI-MVP2)**

**Ngày cập nhật:** 20/08/2026
**Trạng thái:** Sẵn sàng cho Review (ANALYZE)
**Phạm vi:** ShopERP (Blazor Server) + Gateway (PostgreSQL accounting source of truth)
**Target audience:** Hộ kinh doanh siêu nhỏ + SME nhỏ (1-10 nhân viên) — KHÔNG có AR/AP aging, KHÔNG có Inventory Manager actor, KHÔNG có AI Advisor
**Parent vision:** `docs/specs/Vạn An Local Business Os — Financial Management & Business Intelligence Specification.md` §29 MVP-2
**Source codebase context:** Đã có `IncomeStatementService`, `TrialBalanceService`, `CashFlowStatementService`, `IAccountChartService`, `AccountChartEntry`, `AccountingPeriod`, `AccountingStandard`, `JournalEntry`, `AccountingEntry`, `Product.CostPrice`, `ShopFeatureSettingsEntity` (tenant settings)

---

## **0. SCOPE BOUNDARY (HARD STOP — KHÔNG MỞ RỘNG)**

### **0.1. In Scope (MVP-2 only)**

| # | Feature | Source |
|---|---|---|
| F-0 | **IncomeStatement record extension** (4 additive fields: TotalCogsEnding/Opening, TotalOpExEnding/Opening) — A1 resolution Option 2 approved 2026-08-21. Single source of truth COGS extraction. | Investigation 2026-08-21 |
| F-1 | Business Profile (chỉ fixed costs + capacity + pricing model) | Parent spec UC-BP-001/002 (subset) |
| F-2 | Gross Profit + Gross Margin + Operating Profit | Parent spec UC-FM-001/002 |
| F-3 | Break-even Analysis (đơn sản phẩm + multi-product weighted) | Parent spec UC-FM-003 |
| F-4 | Target Profit Analysis | Parent spec UC-FM-004 |
| F-5 | Unit Economics (contribution margin per product, ranking) | Parent spec UC-FM-005 |
| F-6 | Financial Dashboard (5 widgets cho Owner) | Parent spec UC-DASH-001/002/003 |

### **0.1.1. Architecture decision (approved 2026-08-21)**

- **Option B:** MVP-2 services trong `3_CoreHub/Services/FinancialIntelligence/` subfolder + namespace `VanAn.CoreHub.Services.FinancialIntelligence`. NO new csproj. Match precedent `Journal/`, `Template/`, `Reports/`, `Orchestration/`.
- **A1 Option 2:** Extend `IncomeStatement` record với 4 additive fields (default = 0m). Avoid duplicate COGS extraction logic (DRY).
- **W8 bypass:** `FinancialIntelligenceController` inject `IIncomeStatementService` trực tiếp (services layer), KHÔNG qua `IncomeStatementsController` (blocked HKD tenant via W8 feature flag). Match precedent `NetworkDashboardService`.

### **0.2. Out of Scope (DEFER — không làm trong MVP-2)**

| Feature | Defer đến | Lý do |
|---|---|---|
| AR/AP + aging + overdue detection | — (not target audience) | HKD siêu nhỏ cash-based, không có AR/AP |
| Cashflow Forecast | MVP-3 | Cần data 3-6 tháng + MVP-2 stable |
| Business Plan + Budget | MVP-3 | Cần historical data |
| Scenario Simulation | MVP-4 | Phụ thuộc MVP-2+3 |
| Management Advisor (AI recommend) | MVP-5 | R&D 1-2 năm, cần domain expert |
| Inventory Variance Analysis | VA-IIE SRS riêng | Spec `Van_An_SRS_Inventory_Intelligence_Engine.md` |
| E2E "Tôi có lời không" forecast | MVP-3 | Phụ thuộc forecast engine |

### **0.3. Trigger build MVP-2**

- (a) Có ≥ 1 tenant active với **≥ 3 tháng dữ liệu kế toán** (AccountingEntry) trong PostgreSQL, HOẶC
- (b) User duyệt build regardless of tenant demand (educational product play)

---

## **1. TỔNG QUAN HỆ THỐNG (SYSTEM OVERVIEW)**

### **1.1. Bối cảnh nghiệp vụ**

Chủ HKD/SME nhỏ VN hiện trạng:
- **Không biết mình có lời không** cho đến cuối tháng đếm tiền trong két
- **Không biết điểm hòa vốn** ở đâu → không biết phải bán bao nhiêu để sinh tồn
- **Không biết món nào lợi nhuận cao** → mix sản phẩm theo cảm tính, bán nhiều món loss-maker
- **Không có báo cáo tài chính đơn giản** cho chủ (chỉ có BCTC formal kế toán → quá phức tạp)

MVP-2 giải 4 pain trên bằng cách:
1. **Đọc dữ liệu kế toán đã có** (`IncomeStatementService` + `TrialBalanceService`) — không yêu cầu nhập thêm
2. **Thêm 1 entity duy nhất**: `BusinessProfile` (fixed costs + capacity + pricing model) — chủ khai báo 1 lần
3. **Tính break-even + unit economics** = pure calculation dựa trên (P&L + CostPrice + BusinessProfile)
4. **Dashboard 5 widgets** cho Owner hiển thị realtime

### **1.2. Mục tiêu bài toán**

Xây dựng **Financial Intelligence Layer (MVP-2)** — decision-support layer nằm **phía trên Accounting source of truth**, KHÔNG thay thế Accounting.

**Thông điệp sản phẩm (killer use cases):**
- **KU-01:** "Tháng này tôi có lời không?" → Dashboard widget P&L summary
- **KU-02:** "Điểm hòa vốn của tôi là bao nhiêu?" → Break-even Analysis
- **KU-03:** "Muốn lời 50 triệu thì phải bán bao nhiêu?" → Target Profit
- **KU-05:** "Món nào đang làm giảm lợi nhuận?" → Unit Economics ranking

### **1.3. Nguyên lý cốt lõi**

```
Accounting (JournalEntry + AccountingEntry)  ← SOURCE OF TRUTH (existing)
       ↓
IncomeStatementService + TrialBalanceService  ← P&L (existing)
       ↓
+ BusinessProfile (fixed costs)               ← NEW entity (1 duy nhất)
+ Product.CostPrice                            ← EXISTING (variable cost proxy)
       ↓
Financial Intelligence Engine                  ← NEW services (pure calculation)
       ↓
Dashboard widgets                              ← NEW UI
```

**Business Rules (từ parent spec):**
- **BR-001:** Accounting là nguồn dữ liệu tài chính chuẩn — MVP-2 CHỈ ĐỌC, không ghi
- **BR-002:** Financial Intelligence không được thay đổi historical accounting data
- **BR-003:** Forecast không thay đổi Actual (N/A trong MVP-2 — không có forecast)
- **BR-005:** Mọi calculation phải truy ngược được nguồn → tất cả metric có `SourceAccountCodes[]`
- **BR-006:** Mọi calculation phải có version → `FinancialModelVersion` field trên result records
- **BR-008:** Mọi recommendation phải có confidence → MVP-2 deterministic (Level 1 trong Trust Model §27), confidence = 100%

### **1.4. Trust Model (theo parent spec §27)**

MVP-2 chỉ implement **Level 1 — Deterministic** (dựa trên công thức kế toán):
- Break-even = Fixed Cost / Contribution Margin Ratio → công thức, không phải dự đoán
- Gross Profit = Revenue − COGS → từ IncomeStatement đã có
- Unit Economics = Price − CostPrice → từ Product đã có

**KHÔNG implement** Level 2 (rule-based alert), Level 3 (statistical forecast), Level 4 (AI advisory) trong MVP-2.

---

## **2. PHÂN TÍCH ƯU KHUYẾT**

### **2.1. Ưu điểm**

| Ưu điểm | Chi tiết |
|---|---|
| Effort thấp | Đã có 80% foundation (P&L + Trial Balance + Account Chart). Chỉ thêm 1 entity + 3 services + 1 dashboard |
| ROI đo được | Chủ HKD biết điểm hòa vốn + món lời/lỗ → quyết định pricing/mix → tăng profit 5-15% |
| Match TT 152/2025/TT-BTC | HKD bắt buộc làm kế toán → cần hiểu số liệu kế toán → MVP-2 bridge gap |
| Không break existing code | Pure additive — không refactor Domain, không đổi migration hiện có (chỉ thêm bảng `BusinessProfiles`) |
| Universal (mọi tenant) | Không phụ thuộc ngành — mọi HKD đều cần break-even |
| Trust cao | Level 1 deterministic — chủ tin tưởng vì tính từ số kế toán thực |

### **2.2. Khuyết điểm**

| Khuyết điểm | Chi tiết |
|---|---|
| Phụ thuộc data kế toán chất lượng | Tenant không nhập đầy đủ AccountingEntry → break-even sai. Cần guard `INSUFFICIENT_DATA` |
| CostPrice có thể = 0 | Nhiều tenant chưa set CostPrice trên Product → unit economics vô nghĩa. Cần cảnh báo `COST_PRICE_MISSING` |
| Fixed cost nhập tay | BusinessProfile do chủ khai báo → có thể unaccurate. Có note "ước tính" |
| Multi-product break-even phức tạp | Weighted average contribution margin — chủ HKD có thể không hiểu. Cần UI giải thích |
| Không phải "wow feature" | Dashboard + calculation — không viral. Giá trị lâu dài, không phải marketing hook |

### **2.3. Khi nào KHÔNG nên build**

- Tenant chưa có 3 tháng dữ liệu kế toán → break-even vô nghĩa
- Tenant chưa set `Product.CostPrice` cho ≥ 50% products active → unit economics không dùng được
- Chủ HKD không quan tâm P&L (chỉ cần bán hàng + thu tiền) → defer cho đến khi tenant ask

---

## **3. YÊU CẦU CHI TIẾT (DETAILED REQUIREMENTS)**

### **3.1. BusinessProfile entity (Entity mới — DUY NHẤT trong MVP-2)**

> **Lưu ý:** Tuân thủ Single-Identity Pattern. `Id` (PK) = `BusinessProfileId.Value`. VO `BusinessProfileId` bị Ignore trong EF config.

```csharp
// 1_Shared/Domain.cs (or 1_Shared/Domain/BusinessProfile.cs)

/// <summary>
/// MVP-2: Tenant business profile for Financial Intelligence.
/// Stores fixed costs + capacity + pricing model — declared by Owner (1-time + updates).
/// NOT auto-derived from accounting (fixed costs = subjective estimate by Owner).
/// </summary>
public class BusinessProfile : BaseEntity
{
    public BusinessProfileId BusinessProfileId { get; private set; }  // VO — Ignore in EF

    // === Fixed Costs (monthly, VND) ===
    public decimal MonthlyRent { get; private set; }
    public decimal MonthlyPayroll { get; private set; }
    public decimal MonthlyUtilities { get; private set; }
    public decimal MonthlyMarketing { get; private set; }
    public decimal MonthlyLogistics { get; private set; }
    public decimal MonthlyOtherOpEx { get; private set; }
    public decimal MonthlyDepreciation { get; private set; }  // CAPEX amortization

    // === Capacity ===
    public int DailyCapacityUnits { get; private set; }  // max units/day (best estimate)
    public int OperatingDaysPerMonth { get; private set; }  // default 30

    // === Pricing model ===
    public PricingModel PricingModel { get; private set; }  // FixedPrice / DynamicPricing / Mixed

    // === Metadata ===
    public string? Notes { get; private set; }  // Owner note about assumptions
    public FinancialModelVersion Version { get; private set; }  // BR-006 — increment on update

    // Computed (not stored — calculated on read)
    public decimal TotalMonthlyFixedCost =>
        MonthlyRent + MonthlyPayroll + MonthlyUtilities + MonthlyMarketing +
        MonthlyLogistics + MonthlyOtherOpEx + MonthlyDepreciation;

    // EF Core constructor
    private BusinessProfile() { }

    public BusinessProfile(
        TenantId tenantId,
        decimal monthlyRent, decimal monthlyPayroll, decimal monthlyUtilities,
        decimal monthlyMarketing, decimal monthlyLogistics, decimal monthlyOtherOpEx,
        decimal monthlyDepreciation,
        int dailyCapacityUnits, int operatingDaysPerMonth,
        PricingModel pricingModel, string? notes = null)
        : base(tenantId)
    {
        Id = BusinessProfileId.Value;
        MonthlyRent = Math.Max(0, monthlyRent);
        MonthlyPayroll = Math.Max(0, monthlyPayroll);
        MonthlyUtilities = Math.Max(0, monthlyUtilities);
        MonthlyMarketing = Math.Max(0, monthlyMarketing);
        MonthlyLogistics = Math.Max(0, monthlyLogistics);
        MonthlyOtherOpEx = Math.Max(0, monthlyOtherOpEx);
        MonthlyDepreciation = Math.Max(0, monthlyDepreciation);
        DailyCapacityUnits = Math.Max(0, dailyCapacityUnits);
        OperatingDaysPerMonth = Math.Clamp(operatingDaysPerMonth, 1, 31);
        PricingModel = pricingModel;
        Notes = notes;
        Version = FinancialModelVersion.Initial;
    }

    /// <summary>Update fixed costs + capacity. Increments Version (BR-006).</summary>
    public void Update(
        decimal monthlyRent, decimal monthlyPayroll, decimal monthlyUtilities,
        decimal monthlyMarketing, decimal monthlyLogistics, decimal monthlyOtherOpEx,
        decimal monthlyDepreciation,
        int dailyCapacityUnits, int operatingDaysPerMonth,
        PricingModel pricingModel, string? notes = null)
    {
        MonthlyRent = Math.Max(0, monthlyRent);
        MonthlyPayroll = Math.Max(0, monthlyPayroll);
        MonthlyUtilities = Math.Max(0, monthlyUtilities);
        MonthlyMarketing = Math.Max(0, monthlyMarketing);
        MonthlyLogistics = Math.Max(0, monthlyLogistics);
        MonthlyOtherOpEx = Math.Max(0, monthlyOtherOpEx);
        MonthlyDepreciation = Math.Max(0, monthlyDepreciation);
        DailyCapacityUnits = Math.Max(0, dailyCapacityUnits);
        OperatingDaysPerMonth = Math.Clamp(operatingDaysPerMonth, 1, 31);
        PricingModel = pricingModel;
        Notes = notes;
        Version = Version.Increment();
        UpdateAudit();
    }
}

public enum PricingModel { FixedPrice, DynamicPricing, Mixed }
```

### **3.2. FinancialModelVersion value object**

```csharp
// 1_Shared/Domain.cs (or 1_Shared/Domain/ValueObjects/)

/// <summary>BR-006: Every calculation must have version. Start at 1.0, increment minor on update.</summary>
public readonly record struct FinancialModelVersion(int Major, int Minor)
{
    public static FinancialModelVersion Initial => new(1, 0);

    public FinancialModelVersion Increment() => new(Major, Minor + 1);

    public override string ToString() => $"{Major}.{Minor}";
}
```

### **3.3. Result records (read-only DTOs — NOT persisted)**

> **Design decision:** Break-even + Unit Economics results là **pure calculation**, không cần persist.
> Lý do: (1) phụ thuộc dữ liệu kế toán real-time, (2) recompute < 500ms, (3) không cần audit history (AccountingEntry đã immutable + audited).
> Nếu sau này cần "snapshot break-even cuối tháng" → MVP-3+ thêm persistence.

#### **3.3.1. IncomeStatement record extension (Option 2 approved 2026-08-21)**

Extend existing `IncomeStatement` record tại `1_Shared/Domain.cs:3531` với 4 additive fields:

```csharp
public record IncomeStatement(
    TenantId TenantId, AccountingPeriod Period, DateTime GeneratedAt,
    decimal TotalRevenueEnding, decimal TotalRevenueOpening,
    decimal NetProfitEnding, decimal NetProfitOpening,
    IEnumerable<FinancialStatementLine> Lines,
    // === MVP-2 additive fields (default = 0m, backward compat) ===
    decimal TotalCogsEnding = 0m, decimal TotalCogsOpening = 0m,
    decimal TotalOpExEnding = 0m, decimal TotalOpExOpening = 0m
);
```

**Why:** Single source of truth — `IncomeStatementService` đã compute COGS (`cogsEnding`/`cogsOpening`) + OpEx (`opexEnding`/`opexOpening`) internally (lines 184-185, 213-223 flat path; mã 02 + mã 11 TT99 path) nhưng không expose. Option 2 expose values đã compute → DRY, no duplicate logic.

**Backward compat:** Default values = 0m + 3 existing `new IncomeStatement(...)` call sites dùng named arguments → compile OK. Existing test fixture `VasReportPageTestBase.cs:108` compile OK. Existing render tests dùng `Lines` → không break.

**Modification files:**
- `1_Shared/Domain.cs:3531` — extend record signature
- `3_CoreHub/Services/IncomeStatementService.cs:165` (TT99 path) — thêm `TotalCogsEnding: m02e, TotalCogsOpening: m02o, TotalOpExEnding: m11e, TotalOpExOpening: m11o`
- `3_CoreHub/Services/IncomeStatementService.cs:244` (flat path) — thêm `TotalCogsEnding: cogsEnding, TotalCogsOpening: cogsOpening, TotalOpExEnding: opexEnding, TotalOpExOpening: opexOpening`
- `6_Tests/VanAn.ShopERP.Tests/Components/VasReports/VasReportPageTestBase.cs:108` — (optional) update fixture với `TotalCogsEnding: 7_000_000m, TotalOpExEnding: 2_000_000m` cho consistency với Lines

**Precedent:** VALCN v2.0 Phase 1 đã thêm `AccountingEntry.CorrelationId` + `Order.PlatformFeeAmount` cùng pattern additive trên existing records/entities.

```csharp
// 1_Shared/Domain.cs (or 1_Shared/Domain/FinancialIntelligence/)

/// <summary>Break-even analysis result — single period.</summary>
public record BreakEvenAnalysis(
    TenantId TenantId,
    AccountingPeriod Period,
    DateTime CalculatedAt,
    FinancialModelVersion ModelVersion,
    decimal TotalFixedCost,                    // from BusinessProfile (monthly)
    decimal TotalRevenue,                      // from IncomeStatement (period)
    decimal TotalVariableCost,                 // from COGS line of IncomeStatement
    decimal TotalContributionMargin,           // Revenue − Variable Cost
    decimal ContributionMarginRatio,           // Contribution / Revenue (0-1)
    decimal BreakEvenRevenue,                  // FixedCost / CMRatio
    decimal BreakEvenUnits,                    // FixedCost / (AvgPrice − AvgVarCost)
    decimal MarginOfSafetyRevenue,             // Revenue − BreakEvenRevenue
    decimal MarginOfSafetyPercent,             // (Revenue − BreakEven) / Revenue (0-1)
    BreakEvenStatus Status,                    // AboveBreakEven / AtBreakEven / BelowBreakEven / InsufficientData
    string? WarningMessage,                    // e.g. "CostPrice missing on 8/15 products"
    IReadOnlyList<string> SourceAccountCodes   // BR-005 traceability
);

public enum BreakEvenStatus
{
    AboveBreakEven,    // Revenue > BreakEven
    AtBreakEven,       // |Revenue − BreakEven| < tolerance (1% of revenue)
    BelowBreakEven,    // Revenue < BreakEven
    InsufficientData   // no IncomeStatement data OR no BusinessProfile OR no CostPrice
}

/// <summary>Multi-product break-even — weighted average contribution margin.</summary>
public record MultiProductBreakEven(
    TenantId TenantId,
    AccountingPeriod Period,
    DateTime CalculatedAt,
    FinancialModelVersion ModelVersion,
    decimal TotalFixedCost,
    decimal WeightedContributionMargin,        // Σ (CM_i × SalesMix_i)
    decimal WeightedContributionMarginRatio,   // WCM / TotalRevenue
    decimal BreakEvenRevenue,
    IReadOnlyList<ProductBreakEvenLine> ProductLines
);

public record ProductBreakEvenLine(
    Guid ProductId,
    string ProductName,
    decimal SellingPrice,
    decimal VariableCost,                      // CostPrice (or COGS if available)
    decimal ContributionMargin,                // Price − VarCost
    decimal ContributionMarginRatio,
    decimal SalesMixPercent,                   // % of total revenue (0-1)
    int UnitsSoldInPeriod,                     // from OrderItem count
    decimal ProductBreakEvenUnits              // FixedCost × SalesMix / CM_i  (allocation)
);

/// <summary>Unit Economics — per product ranking.</summary>
public record UnitEconomicsReport(
    TenantId TenantId,
    AccountingPeriod Period,
    DateTime CalculatedAt,
    FinancialModelVersion ModelVersion,
    IReadOnlyList<UnitEconomicsLine> Products, // sorted by ProfitContribution DESC
    int TotalProductsAnalyzed,
    int ProductsWithMissingCostPrice,          // warning count
    decimal TotalContribution,
    decimal AverageContributionMargin
);

public record UnitEconomicsLine(
    Guid ProductId,
    string ProductName,
    string Category,
    decimal SellingPrice,
    decimal VariableCost,
    decimal ContributionMargin,                // Price − VarCost
    decimal ContributionMarginPercent,         // CM / Price (0-1)
    int UnitsSold,
    decimal Revenue,
    decimal ProfitContribution,                // CM × UnitsSold
    double ProfitContributionRank,             // 1.0 = top, descending
    bool HasMissingCostPrice                   // warning flag
);

/// <summary>Target Profit analysis — given target, compute required volume/price/margin.</summary>
public record TargetProfitAnalysis(
    TenantId TenantId,
    AccountingPeriod Period,
    DateTime CalculatedAt,
    FinancialModelVersion ModelVersion,
    decimal TargetProfit,
    decimal TotalFixedCost,
    decimal AverageContributionMargin,         // weighted
    decimal RequiredRevenue,                   // (FixedCost + TargetProfit) / CMRatio
    decimal RequiredUnits,                     // RequiredRevenue / AvgPrice
    decimal RequiredDailyUnits,                // RequiredUnits / OperatingDaysPerMonth
    bool Feasible,                             // RequiredDailyUnits ≤ DailyCapacityUnits
    string? FeasibilityWarning                 // e.g. "Required 250 units/day but capacity 200/day"
);

/// <summary>Profit summary for dashboard — quick "tháng này có lời không".</summary>
public record ProfitSummary(
    TenantId TenantId,
    AccountingPeriod Period,
    DateTime CalculatedAt,
    decimal Revenue,
    decimal COGS,
    decimal GrossProfit,
    decimal GrossMarginPercent,
    decimal OperatingExpenses,
    decimal OperatingProfit,
    decimal NetProfit,
    decimal NetMarginPercent,
    ProfitStatus Status,                       // Profitable / AtBreakEven / Loss / InsufficientData
    string? WarningMessage
);

public enum ProfitStatus { Profitable, AtBreakEven, Loss, InsufficientData }
```

### **3.4. Service interfaces (CoreHub — pure calculation)**

> **Architecture rule:** Services nằm ở `3_CoreHub/Services/FinancialIntelligence/`. Inject `IIncomeStatementService` + `ITrialBalanceService` + `IAccountChartService` (existing) + `IProductRepository` (existing) + `IBusinessProfileRepository` (new). KHÔNG inject `IVanAnDbContext` trực tiếp — qua repository/interface.

```csharp
// 3_CoreHub/Services/FinancialIntelligence/IBusinessProfileService.cs
public interface IBusinessProfileService
{
    Task<BusinessProfile?> GetAsync(TenantId tenantId, CancellationToken ct = default);
    Task<BusinessProfile> GetOrCreateDefaultAsync(TenantId tenantId, CancellationToken ct = default);
    Task<BusinessProfile> UpdateAsync(TenantId tenantId, UpdateBusinessProfileCommand cmd, CancellationToken ct = default);
}

// 3_CoreHub/Services/FinancialIntelligence/IBreakEvenAnalysisService.cs
public interface IBreakEvenAnalysisService
{
    /// <summary>Single-period break-even. Returns InsufficientData if no BusinessProfile or no P&L.</summary>
    Task<BreakEvenAnalysis> AnalyzeAsync(TenantId tenantId, AccountingPeriod period, AccountingStandard standard, CancellationToken ct = default);

    /// <summary>Multi-product weighted break-even. Returns empty ProductLines if no OrderItem data.</summary>
    Task<MultiProductBreakEven> AnalyzeMultiProductAsync(TenantId tenantId, AccountingPeriod period, AccountingStandard standard, CancellationToken ct = default);
}

// 3_CoreHub/Services/FinancialIntelligence/IUnitEconomicsService.cs
public interface IUnitEconomicsService
{
    Task<UnitEconomicsReport> AnalyzeAsync(TenantId tenantId, AccountingPeriod period, CancellationToken ct = default);
}

// 3_CoreHub/Services/FinancialIntelligence/ITargetProfitService.cs
public interface ITargetProfitService
{
    Task<TargetProfitAnalysis> AnalyzeAsync(TenantId tenantId, AccountingPeriod period, AccountingStandard standard, decimal targetProfit, CancellationToken ct = default);
}

// 3_CoreHub/Services/FinancialIntelligence/IProfitSummaryService.cs
public interface IProfitSummaryService
{
    Task<ProfitSummary> GetAsync(TenantId tenantId, AccountingPeriod period, AccountingStandard standard, CancellationToken ct = default);
}
```

### **3.5. Service implementations — calculation formulas**

#### **3.5.1. BreakEvenAnalysisService**

```csharp
// Pseudocode — actual implementation follows Clean Architecture + DI
public async Task<BreakEvenAnalysis> AnalyzeAsync(...)
{
    // 1. Load BusinessProfile (fixed costs) — if null → InsufficientData
    var profile = await _profileService.GetAsync(tenantId, ct);
    if (profile is null)
        return InsufficientData("Chưa khai báo BusinessProfile — cần nhập fixed costs");

    // 2. Load IncomeStatement (Revenue + COGS) — if empty → InsufficientData
    var income = await _incomeStatementService.GenerateAsync(tenantId, period, standard, ct);
    if (income.TotalRevenueEnding == 0)
        return InsufficientData($"Chưa có dữ liệu kế toán kỳ {period}");

    // 3. Extract Revenue + Variable Cost (COGS) from IncomeStatement lines
    decimal revenue = income.TotalRevenueEnding;
    decimal variableCost = ExtractCogs(income.Lines);  // COGS lines per AccountChart TT99/TT133
    decimal contribution = revenue - variableCost;
    decimal cmRatio = revenue > 0 ? contribution / revenue : 0;

    // 4. Fixed cost — monthly → prorate to period (if period != monthly)
    decimal fixedCost = profile.TotalMonthlyFixedCost;  // assume monthly period

    // 5. Break-even
    decimal breakEvenRevenue = cmRatio > 0 ? fixedCost / cmRatio : decimal.MaxValue;
    decimal avgPrice = revenue / UnitsSoldInPeriod;
    decimal avgVarCost = variableCost / UnitsSoldInPeriod;
    decimal breakEvenUnits = (avgPrice - avgVarCost) > 0
        ? fixedCost / (avgPrice - avgVarCost) : decimal.MaxValue;

    // 6. Margin of safety
    decimal mosRevenue = revenue - breakEvenRevenue;
    decimal mosPercent = revenue > 0 ? mosRevenue / revenue : 0;

    // 7. Status
    var status = ClassifyStatus(revenue, breakEvenRevenue);

    // 8. Source traceability (BR-005)
    var sourceCodes = new[] { "511", "632", "641", "642", ... }; // Revenue/COGS/OpEx codes

    return new BreakEvenAnalysis(...);
}
```

#### **3.5.2. UnitEconomicsService**

```csharp
public async Task<UnitEconomicsReport> AnalyzeAsync(...)
{
    // 1. Load all active Products for tenant
    var products = await _productRepo.GetActiveAsync(tenantId, ct);

    // 2. Load OrderItems in period (units sold per product)
    var orderItems = await _orderItemRepo.GetByPeriodAsync(tenantId, period, ct);
    var unitsByProduct = orderItems.GroupBy(i => i.ProductId)
        .ToDictionary(g => g.Key, g => (Units: g.Sum(x => x.Quantity), Revenue: g.Sum(x => x.TotalAmount)));

    // 3. Build per-product line
    var lines = products.Select(p =>
    {
        unitsByProduct.TryGetValue(p.Id, out var sold);
        bool missingCost = p.CostPrice == 0;
        decimal varCost = missingCost ? EstimateVariableCost(p, orderItems) : p.CostPrice;
        decimal cm = p.Price - varCost;
        decimal cmPercent = p.Price > 0 ? cm / p.Price : 0;
        return new UnitEconomicsLine(
            p.Id, p.Name, p.Category, p.Price, varCost, cm, cmPercent,
            sold.Units, sold.Revenue, cm * sold.Units, 0 /* rank later */, missingCost);
    }).ToList();

    // 4. Rank by ProfitContribution DESC
    lines = lines.OrderByDescending(l => l.ProfitContribution).ToList();
    for (int i = 0; i < lines.Count; i++)
        lines[i] = lines[i] with { ProfitContributionRank = i + 1 };

    // 5. Warnings
    int missingCount = lines.Count(l => l.HasMissingCostPrice);
    string? warning = missingCount > 0
        ? $"{missingCount}/{lines.Count} sản phẩm chưa set CostPrice — Unit Economics có thể sai"
        : null;

    return new UnitEconomicsReport(...);
}
```

#### **3.5.3. TargetProfitService**

```csharp
// Formula: RequiredRevenue = (FixedCost + TargetProfit) / CMRatio
//          RequiredUnits = RequiredRevenue / AvgPrice
//          RequiredDaily = RequiredUnits / OperatingDaysPerMonth
//          Feasible = RequiredDaily ≤ DailyCapacityUnits
```

### **3.6. Repository (Infrastructure)**

```csharp
// 3_CoreHub/Infrastructure/Repositories/BusinessProfileRepository.cs
public interface IBusinessProfileRepository
{
    Task<BusinessProfile?> GetByTenantAsync(TenantId tenantId, CancellationToken ct = default);
    Task AddAsync(BusinessProfile profile, CancellationToken ct = default);
    Task UpdateAsync(BusinessProfile profile, CancellationToken ct = default);
}
```

### **3.7. EF Core Configuration + Migration**

```csharp
// 3_CoreHub/Infrastructure/Configurations/BusinessProfileConfiguration.cs
public class BusinessProfileConfiguration : IEntityTypeConfiguration<BusinessProfile>
{
    public void Configure(EntityTypeBuilder<BusinessProfile> b)
    {
        b.ToTable("BusinessProfiles");
        b.HasKey(e => e.Id);
        b.Ignore(e => e.BusinessProfileId);          // Single-Identity Pattern
        b.Ignore(e => e.TotalMonthlyFixedCost);       // computed, not stored
        b.Property(e => e.MonthlyRent).HasColumnType("decimal(18,2)");
        // ... other fixed cost columns
        b.Property(e => e.PricingModel).HasConversion<string>();
        b.Property(e => e.Version).HasConversion(
            v => v.ToString(),
            v => FinancialModelVersion.Parse(v));
        b.HasIndex(e => e.TenantId).IsUnique();       // 1 profile per tenant
    }
}
```

**Migration: `20260820XX_AddBusinessProfile`**
- CreateTable `BusinessProfiles` (13 columns + audit + TenantId)
- CreateIndex unique on TenantId
- **NO data backfill** (tenant tự khai báo khi dùng)

### **3.8. API Endpoints (Gateway — REST, tenant-scoped)**

> **Auth:** `[Authorize]` class-level + `AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme` (match `RedemptionController`, `OcrConfigController` precedent — Pattern #10 charset fix).

| Method | Path | Mô tả |
|---|---|---|
| `GET` | `/api/financial/business-profile` | Lấy BusinessProfile của tenant (or 404 nếu chưa có) |
| `PUT` | `/api/financial/business-profile` | Update/Create BusinessProfile (upsert) |
| `GET` | `/api/financial/profit-summary?period=2026-08` | ProfitSummary widget data |
| `GET` | `/api/financial/break-even?period=2026-08&standard=TT99_2025` | Break-even single |
| `GET` | `/api/financial/break-even/multi-product?period=2026-08` | Multi-product break-even |
| `GET` | `/api/financial/unit-economics?period=2026-08` | Unit Economics ranking |
| `POST` | `/api/financial/target-profit` | Body: `{period, targetProfit}` → TargetProfitAnalysis |

**Controller skeleton:**
```csharp
[ApiController]
[Route("api/financial")]
[Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
public class FinancialIntelligenceController : ControllerBase
{
    // Inject: IBusinessProfileService, IProfitSummaryService,
    //         IBreakEvenAnalysisService, IUnitEconomicsService, ITargetProfitService

    [HttpGet("business-profile")]
    public async Task<ActionResult<BusinessProfileDto>> GetProfile(CancellationToken ct) { ... }

    [HttpPut("business-profile")]
    public async Task<ActionResult<BusinessProfileDto>> UpdateProfile(
        [FromBody] UpdateBusinessProfileDto dto, CancellationToken ct) { ... }

    // ... other endpoints
}
```

**DTOs:** Map records → camelCase JSON DTOs in `2_Gateway/Controllers/FinancialIntelligenceController.cs` (or `3_CoreHub/Services/FinancialIntelligence/Dtos/`).

### **3.9. ShopERP HTTP Client (proxy pattern — match `NetworkDashboardHttpService`)**

```csharp
// 5_WebApps/ShopERP/Services/FinancialIntelligenceHttpService.cs
public class FinancialIntelligenceHttpService
{
    private readonly HttpClient _http;  // YARP forwards to Gateway
    private readonly ITenantContext _tenantCtx;

    public async Task<ProfitSummaryDto?> GetProfitSummaryAsync(AccountingPeriod period, CancellationToken ct) { ... }
    public async Task<BreakEvenAnalysisDto?> GetBreakEvenAsync(AccountingPeriod period, CancellationToken ct) { ... }
    public async Task<UnitEconomicsReportDto?> GetUnitEconomicsAsync(AccountingPeriod period, CancellationToken ct) { ... }
    public async Task<TargetProfitAnalysisDto?> AnalyzeTargetProfitAsync(AccountingPeriod period, decimal target, CancellationToken ct) { ... }
    public async Task<BusinessProfileDto?> GetBusinessProfileAsync(CancellationToken ct) { ... }
    public async Task UpdateBusinessProfileAsync(UpdateBusinessProfileDto dto, CancellationToken ct) { ... }
}
```

> **Architecture rule:** ShopERP không inject `IVanAnDbContext` cho MVP-2 (accounting source of truth ở Gateway PG). ShopERP chỉ HTTP proxy → Gateway. Match precedent `NetworkDashboardHttpService` (VALCN v2.0 Phase 7).

---

## **4. YÊU CẦU GIAO DIỆN (UI REQUIREMENTS)**

> **HARD STOP:** ALWAYS dùng UI Platform components (`VanAMetricsCard`, `VanAStatusForm`, `VanAButton`, `VanAChart` if exists). NEVER bypass. Tham khảo `docs/UI_Platform_Implementation_Guide.md`.

### **4.1. Trang `/financial` — Financial Dashboard (Owner landing)**

5 widgets theo layout responsive (mobile 1 col, desktop 2-3 col):

| Widget | Component | Data source | Refresh |
|---|---|---|---|
| **Profit Summary** | `VanAMetricsCard` (revenue, gross profit, net profit, margin %) | `ProfitSummaryService` | Manual + period picker |
| **Break-even Status** | `VanAMetricsCard` (break-even revenue, margin of safety %, status badge: Above/At/Below) | `BreakEvenAnalysisService` | Manual + period picker |
| **Top 5 Products by Profit** | `VanAList` (productName, CM%, profitContribution) | `UnitEconomicsService` (top 5) | Manual + period picker |
| **Bottom 5 Products (loss makers)** | `VanAList` (productName, CM%, negative contribution) | `UnitEconomicsService` (bottom 5) | Manual + period picker |
| **Quick Target Profit Calculator** | Inline form (targetProfit input → requiredRevenue, requiredUnits, feasibility badge) | `TargetProfitService` | On submit |

**Period picker:** Month dropdown (current month default) + year dropdown (current year default).
**Warning banner:** Nếu `ProductsWithMissingCostPrice > 0` → hiển thị warning "X sản phẩm chưa set CostPrice — kết quả có thể sai. [Cập nhật CostPrice]".

### **4.2. Trang `/financial/break-even` — Break-even Detail**

- Single-product break-even card (formula display, source account codes collapsible)
- Multi-product break-even table (per-product: Price, VarCost, CM, SalesMix%, BreakEvenUnits)
- Chart: Revenue line vs Break-even line (horizontal) — if `VanAChart` supports, else simple bar
- Export PDF (use existing export pattern from `RevenueExcelReport`)

### **4.3. Trang `/financial/unit-economics` — Unit Economics**

- Full ranking table (sortable: by Revenue / CM% / ProfitContribution / UnitsSold)
- Filter by Category
- Warning row highlight for `HasMissingCostPrice = true`
- Click product → navigate to `/admin/products/{id}/edit` (set CostPrice)
- Export Excel

### **4.4. Trang `/admin/business-profile` — BusinessProfile CRUD**

> **Placement:** `/admin/` (SystemAdmin or Owner role), NOT `/financial/` (separation: setup vs view).

- Form 7 fixed cost fields (numeric input, VND format `#,##0`)
- Capacity: DailyCapacityUnits, OperatingDaysPerMonth
- PricingModel: radio (FixedPrice / DynamicPricing / Mixed)
- Notes: textarea
- Save button → upsert via Gateway
- Show "Last updated: {date} (v{Version})" — BR-006 transparency

### **4.5. NavMenu + Sitemap**

- NavMenu (ShopERP AdminLayout): "Thông tin tài chính" → `/financial`
- Sitemap (`/sitemap`): "Phân tích tài chính" card → `/financial`

### **4.6. Responsive / PWA**

- ShopERP PWA → mobile-first layout (widgets stack 1-col on mobile)
- Big touch target numeric inputs (BusinessProfile form)
- Offline: dashboard cache last result in localStorage, refresh on reconnect

---

## **5. NGHIỆP VỤ & WORKFLOW (BUSINESS WORKFLOW)**

### **5.1. Setup flow (1-time per tenant)**

| Bước | Actor | Hành động |
|---|---|---|
| 1 | Owner | Đăng nhập ShopERP → nav "Thông tin tài chính" |
| 2 | System | Detect chưa có BusinessProfile → redirect `/admin/business-profile` với banner "Khai báo fixed costs để dùng phân tích tài chính" |
| 3 | Owner | Nhập 7 fixed cost fields + capacity + pricing model → Save |
| 4 | System | Upsert BusinessProfile → redirect `/financial` |

### **5.2. Daily/Weekly flow**

| Bước | Actor | Hành động |
|---|---|---|
| 1 | Owner | Mở `/financial` |
| 2 | System | Load ProfitSummary + BreakEven + UnitEconomics (parallel) cho kỳ hiện tại |
| 3 | Owner | Xem widget "Tháng này có lời không?" (KU-01) |
| 4 | Owner | Xem widget "Điểm hòa vốn" (KU-02) |
| 5 | Owner | Click "Top/Bottom 5" → xem món nào lợi/lỗ (KU-05) |
| 6 | Owner | (optional) Quick Target Profit calculator — nhập "50M" → xem required volume + feasibility (KU-03) |

### **5.3. Period close flow**

- Cuối tháng: Owner mở `/financial?period=2026-08` → xem full month summary
- Export PDF (break-even + unit economics) → lưu trữ / gửi accountant

---

## **6. CẢNH BÁO & DATA QUALITY (GUARD CONDITIONS)**

| Code | Điều kiện | Hành động |
|---|---|---|
| `PROFILE_MISSING` | Tenant chưa có BusinessProfile | Redirect setup + warning banner |
| `INSUFFICIENT_DATA` | IncomeStatement rỗng cho kỳ | Widget hiển thị "Chưa có dữ liệu kỳ này" + disable export |
| `COST_PRICE_MISSING` | > 0% products có CostPrice = 0 | Warning banner + row highlight + link update |
| `CM_RATIO_ZERO_OR_NEG` | ContributionMargin ≤ 0 (Revenue ≤ VariableCost) | Status `BelowBreakEven` + critical warning "Biên đóng góp âm — cần review giá hoặc chi phí" |
| `CAPACITY_EXCEEDED` | RequiredDailyUnits > DailyCapacityUnits | TargetProfit feasibility warning "Vượt capacity — cần tăng giá hoặc giảm fixed cost" |
| `FIXED_COST_ZERO` | TotalMonthlyFixedCost = 0 | Warning "Chưa nhập fixed costs — break-even = 0 không có ý nghĩa" |

---

## **7. YÊU CẦU PHI CHỨC NĂNG (NFR)**

| # | Yêu cầu | Mô tả | Verification |
|---|---|---|---|
| NFR-1 | Performance | ProfitSummary + BreakEven < 500ms (cold cache, ≤ 1000 AccountingEntry) | Stopwatch in test |
| NFR-2 | Performance | UnitEconomics < 1s (≤ 200 products, ≤ 5000 OrderItem in period) | Stopwatch in test |
| NFR-3 | Scalability | Hỗ trợ per-tenant, không contention cross-tenant | Architecture test W12-G7 |
| NFR-4 | Multi-tenancy | Mọi query filter by TenantId, không leak | Architecture test |
| NFR-5 | Security | Role: Owner + SystemAdmin xem `/financial`; Staff không | `[Authorize(Policy = "OwnerOrAdmin")]` |
| NFR-6 | Audit trail | BusinessProfile update có audit log (UpdatedAt + UpdatedBy từ BaseEntity) | Audit field check |
| NFR-7 | Domain purity | Domain layer PURE, no EF Core, no DbContext | Architecture test W12-G7 |
| NFR-8 | Single Identity | `BusinessProfile` tuân thủ Single-Identity Pattern (Id = PK, BusinessProfileId Ignore) | Migration + EF config check |
| NFR-9 | Immutability | `BreakEvenAnalysis`, `UnitEconomicsReport`, etc. là `record` (immutable) | Type check |
| NFR-10 | Build gate | `guard-check.ps1` + `dotnet build VanAn.sln` MUST PASS | CI |
| NFR-11 | UI Platform | 100% UI Platform components, no raw HTML/CSS bypass | UI Platform compliance review |
| NFR-12 | i18n | Tất cả label tiếng Việt (no hardcoded English in UI) | String review |
| NFR-13 | Currency | Tất cả monetary value VND, no decimal currency rounding (decimal(18,2)) | Migration column type |
| NFR-14 | Trust Level | Chỉ Level 1 deterministic (no AI, no statistical) — per parent spec §27 | Code review |

---

## **8. LỊCH TRIỂN KHAI (ROADMAP)**

### **Phase 1 — Foundation (3-4 ngày)**
- Domain: `BusinessProfile` + `FinancialModelVersion` + 5 result records + 2 enums (PricingModel, BreakEvenStatus, ProfitStatus)
- EF config + migration `AddBusinessProfile`
- `IBusinessProfileRepository` + impl
- `IBusinessProfileService` + impl
- DI registration (Gateway + ShopERP)
- Unit tests: BusinessProfile.Create/Update, FinancialModelVersion.Increment, TotalMonthlyFixedCost computed

### **Phase 2 — Calculation Services (4-5 ngày)**
- `IProfitSummaryService` + impl (wrap existing `IncomeStatementService`)
- `IBreakEvenAnalysisService` + impl (single + multi-product)
- `IUnitEconomicsService` + impl
- `ITargetProfitService` + impl
- Unit tests: mỗi service ≥ 3 tests (happy path + InsufficientData + edge case CM=0)
- Integration tests: real IncomeStatement data → break-even end-to-end

### **Phase 3 — API + HTTP Proxy (2-3 ngày)**
- `FinancialIntelligenceController` (7 endpoints)
- DTOs (camelCase JSON, match existing patterns)
- `FinancialIntelligenceHttpService` (ShopERP)
- W12-G7 architecture test: add controller to authorized list
- Integration tests: 401 no-auth, 200 with JWT, 404 missing profile

### **Phase 4 — UI (3-4 ngày)**
- `/admin/business-profile` form (CRUD)
- `/financial` dashboard (5 widgets)
- `/financial/break-even` detail
- `/financial/unit-economics` table
- NavMenu + Sitemap links
- UI Platform compliance check
- Playwright E2E: setup flow + dashboard render + target profit calculator

### **Phase 5 — Polish (1-2 ngày)**
- Export PDF (break-even + unit economics)
- Export Excel (unit economics)
- Warning banner UX
- i18n review (tiếng Việt labels)
- Documentation: admin guide for Owner

**Total effort: ~2-3 tuần (10-15 working days)** — phù hợp đánh giá ban đầu.

---

## **9. RỦI RO & GIẢI PHÁP (RISK ANALYSIS)**

| Risk | Probability | Impact | Mitigation |
|---|---|---|---|
| Tenant chưa nhập CostPrice → UnitEconomics sai | Cao | Trung bình | Warning `COST_PRICE_MISSING` + link update + "estimated" flag |
| Fixed cost nhập sai → break-even sai | Trung bình | Cao | Notes field encourage chủ ghi chú giả định + Version tracking |
| IncomeStatement rỗng (tenant mới) → InsufficientData | Cao | Thấp | Guard `INSUFFICIENT_DATA` + onboarding hint |
| Multi-product break-even phức tạp, chủ không hiểu | Trung bình | Trung bình | UI giải thích từng bước + tooltip + "simple mode" (single product) default |
| CostPrice ≠ actual COGS (do variance, waste) | Trung bình | Trung bình | Note "CostPrice ước tính — dùng VA-IIE Phase 2 cho actual COGS từ variance" |
| Performance chậm khi nhiều OrderItem | Thấp | Thấp | Cache per-period (IMemoryCache, 10-min TTL — match NetworkDashboardService precedent) |
| Tenant expect "AI advise" (như spec parent MVP-5) | Trung bình | Thấp | Clear messaging "MVP-2 chỉ tính toán, không advise" + roadmap transparency |
| Domain entity bloat | Thấp | Thấp | Chỉ 1 entity (BusinessProfile) — result là records không persist |

---

## **10. TIÊU CHÍ NHẬN (ACCEPTANCE CRITERIA)**

### **Functional**
- [ ] Owner khai báo BusinessProfile (7 fixed costs + capacity + pricing) → save thành công
- [ ] Owner update BusinessProfile → Version increment (BR-006)
- [ ] `/financial` hiển thị 5 widgets cho kỳ hiện tại
- [ ] Period picker đổi kỳ → widgets refresh
- [ ] ProfitSummary đúng số liệu từ IncomeStatement (cross-check với `/accounting/income-statement`)
- [ ] BreakEven: Revenue > BreakEven → status `AboveBreakEven`; Revenue < BreakEven → `BelowBreakEven`
- [ ] Multi-product break-even: Σ ProductBreakEvenUnits ≈ total break-even units (tolerance 5%)
- [ ] UnitEconomics: products sorted by ProfitContribution DESC
- [ ] UnitEconomics: products có CostPrice=0 flagged `HasMissingCostPrice` + warning banner
- [ ] TargetProfit: nhập 50M → RequiredRevenue + RequiredUnits + feasibility badge
- [ ] TargetProfit: RequiredDaily > Capacity → `Feasible = false` + warning
- [ ] Export PDF break-even + export Excel unit economics

### **Guard conditions**
- [ ] Tenant chưa có BusinessProfile → redirect setup + banner
- [ ] Tenant chưa có IncomeStatement data → widget "Chưa có dữ liệu kỳ này"
- [ ] CM ≤ 0 → critical warning "Biên đóng góp âm"
- [ ] FixedCost = 0 → warning "Chưa nhập fixed costs"

### **Non-functional**
- [ ] NFR-1: ProfitSummary + BreakEven < 500ms
- [ ] NFR-2: UnitEconomics < 1s
- [ ] NFR-4: Multi-tenancy — tenant A không thấy data tenant B
- [ ] NFR-5: Staff role không truy cập được `/financial`
- [ ] NFR-7: Domain layer pure (no EF Core)
- [ ] NFR-8: Single-Identity Pattern — `BusinessProfileConfiguration.Ignore(e => e.BusinessProfileId)`
- [ ] NFR-10: `guard-check.ps1` + `dotnet build VanAn.sln` PASS
- [ ] NFR-11: 100% UI Platform components
- [ ] NFR-14: Chỉ Level 1 deterministic calculation (no AI/statistical)

### **Architecture**
- [ ] W12-G7: `FinancialIntelligenceController` có class-level `[Authorize]`
- [ ] Pattern #10: Strip charset trước khi pass `Request.ContentType` (if any forward controller variant)
- [ ] Layer direction: API → Services → Domain (no reverse)
- [ ] ShopERP không inject `IVanAnDbContext` cho MVP-2 (HTTP proxy only)

---

## **11. TÀI LIỆU THAM KHẢO (REFERENCES)**

- Parent vision: `docs/specs/Vạn An Local Business Os — Financial Management & Business Intelligence Specification.md` §29 MVP-2
- Governance: `.devin/rules/governance.md` (Domain purity, Single-Identity Pattern, hard stops)
- UI Platform: `docs/UI_Platform_Implementation_Guide.md`
- Existing services: `IIncomeStatementService`, `ITrialBalanceService`, `IAccountChartService`
- Existing entities: `AccountingEntry` (Domain.cs line 287), `Product` (Domain.cs line 537), `AccountChartEntry` (Domain.cs line 3608)
- Precedent for tenant-scoped Infrastructure entity: `ShopFeatureSettingsEntity` (`3_CoreHub/Infrastructure/Entities/ShopFeatureSettingsEntity.cs`)
- Precedent for HTTP proxy pattern: `NetworkDashboardHttpService` (VALCN v2.0 Phase 7)
- Precedent for `[Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]`: `OcrConfigController`, `RedemptionController` (Pattern #10 fix #106)
- Pattern registry: governance.md §"Known Error Pattern Registry" (Pattern #1, #5, #8, #10)

---

## **12. KẾT LUẬN**

MVP-2 là bước hợp lý tiếp theo cho Financial Intelligence Layer vì:
1. **Foundation đã có 80%** — P&L + Trial Balance + Account Chart + Product.CostPrice
2. **Chỉ thêm 1 entity** (BusinessProfile) — minimal Domain change, pure additive
3. **Pure calculation** — Level 1 deterministic, trust cao, không R&D risk
4. **Universal value** — mọi tenant HKD cần biết "có lời không + hòa vốn ở đâu + món nào lời/lỗ"
5. **Match TT 152/2025/TT-BTC** — HKD bắt buộc kế toán → cần bridge để hiểu số liệu
6. **Effort hợp lý** — 2-3 tuần, không block task đang dở trong `project_state.md` §4

**Defer rõ ràng:** MVP-3 (Forecast + Plan) chờ MVP-2 stable + 3-6 tháng data. MVP-4 (Scenario) chờ MVP-3. MVP-5 (AI Advisor) chờ MVP-2+3+4 + 6-12 tháng data + domain expert.

**Điều kiện build:** (a) có tenant active với ≥ 3 tháng dữ liệu kế toán, HOẶC (b) user duyệt build regardless of tenant demand.
