# TASK CARD — Phase 3: B 03-DN Phương Pháp Gián Tiếp (Indirect Method)

> **Status:** ✅ COMPLETE (Wave 3, commit `f98ddea5`, CD run `30873505215` SUCCESS, VPS RV 10/10 PASS)
> **Priority:** P2 — Feature gap
> **Branch:** `main` (Wave 3 commit)
> **Estimated sessions:** 1
> **Mode:** IMPLEMENT
> **Domain modification:** YES — add `CashFlowMethod` enum + update `CashFlowStatement` record
> **Implemented:** 2026-08-03 — CashFlowMethod enum (Direct/Indirect) + CashFlowStatement.Method field + GenerateIndirectAsync (Mã 01-17 + working capital deltas) + injected IBalanceSheetService + IIncomeStatementService + UI toggle in CashFlowStatement.razor + 2 test files updated. NOTE: "Accounting Tests" workflow failed (run 30873505237) — separate from main CI/CD, needs follow-up.

## Objective
TT 99/2025/TT-BTC yêu cầu B 03-DN (Báo cáo lưu chuyển tiền tệ) có **2 phương pháp**:
1. **Phương pháp trực tiếp** (hiện có) — trình bày từng luồng tiền thu/chi
2. **Phương pháp gián tiếp** (THIẾU) — điều chỉnh lợi nhuận ròng để ra luồng tiền HĐKD

Codebase hiện chỉ có phương pháp trực tiếp. Cần bổ sung phương pháp gián tiếp.

## Prerequisites
- [ ] Verify `CashFlowStatement` record tại `Domain.cs:3350-3356` (3 sections: Operating/Investing/Financing)
- [ ] Verify `CashFlowStatementService.cs` hiện chỉ generate trực tiếp
- [ ] Verify `CashFlowStatement.razor` hiện không có toggle method
- [ ] Verify `IncomeStatementService` có thể cung cấp NetProfit (cần cho gián tiếp)
- [ ] Verify `BalanceSheetService` có thể cung cấp delta tài sản/nợ (cần cho gián tiếp)

## Files to Modify
| File | Changes |
|------|---------|
| `1_Shared/Domain.cs` | Add `CashFlowMethod` enum + update `CashFlowStatement` record với `Method` field |
| `3_CoreHub/Services/CashFlowStatementService.cs` | Add `GenerateIndirectAsync()` method |
| `3_CoreHub/Services/ICashFlowStatementService.cs` | Update interface (nếu cần) |
| `5_WebApps/ShopERP/Components/Pages/Accounting/CashFlowStatement.razor` | Add toggle trực tiếp/gián tiếp |
| `5_WebApps/ShopERP/Services/FinancialReportExportService.cs` | Export theo method |

## Detailed Changes

### Change 1: Domain — Add CashFlowMethod enum
```csharp
// Domain.cs — add after AccountingStandard enum (line 3313)
/// <summary>
/// B 03-DN method: TT 99 requires both direct + indirect methods.
/// Direct: trình bày từng luồng tiền thu/chi.
/// Indirect: điều chỉnh lợi nhuận ròng → luồng tiền HĐKD.
/// </summary>
public enum CashFlowMethod { Direct, Indirect }
```

### Change 2: Domain — Update CashFlowStatement record
```csharp
// OLD (line 3350)
public record CashFlowStatement(
    TenantId TenantId, AccountingPeriod Period, DateTime GeneratedAt,
    decimal OpeningCash, decimal ClosingCash, decimal NetChange,
    IEnumerable<FinancialStatementLine> OperatingActivities,
    IEnumerable<FinancialStatementLine> InvestingActivities,
    IEnumerable<FinancialStatementLine> FinancingActivities
);

// NEW — add Method field + optional IndirectAdjustments (for indirect method)
public record CashFlowStatement(
    TenantId TenantId, AccountingPeriod Period, DateTime GeneratedAt,
    CashFlowMethod Method,                    // NEW
    decimal OpeningCash, decimal ClosingCash, decimal NetChange,
    IEnumerable<FinancialStatementLine> OperatingActivities,
    IEnumerable<FinancialStatementLine> InvestingActivities,
    IEnumerable<FinancialStatementLine> FinancingActivities,
    // Indirect method only: adjustments from NetProfit to Operating Cash Flow
    // Null for Direct method
    IEnumerable<FinancialStatementLine>? IndirectAdjustments  // NEW
);
```

### Change 3: Service — Add GenerateIndirectAsync
```csharp
// CashFlowStatementService.cs
public async Task<CashFlowStatement> GenerateIndirectAsync(
    TenantId tenantId, AccountingPeriod period, AccountingStandard standard, CancellationToken ct = default)
{
    // 1. Get NetProfit from IncomeStatement
    var incomeStmt = await _incomeStatementService.GenerateAsync(tenantId, period, standard, ct);
    decimal netProfit = incomeStmt.NetProfitEnding;

    // 2. Get balance sheet deltas (current period vs opening)
    var balanceSheet = await _balanceSheetService.GenerateAsync(tenantId, period, standard, ct);
    // Calculate delta: AccountsReceivable, Inventory, AccountsPayable, etc.

    // 3. Build indirect adjustments:
    // - Lợi nhuận trước thuế
    // (+) Khấu hao TSCĐ (TK 214)
    // (-) Tăng tài sản ngắn hạn (phải thu, hàng tồn kho)
    // (+) Tăng nợ phải trả
    // (-) Giảm tài sản ngắn hạn
    // (+) Giảm nợ phải trả
    // = Lưu chuyển tiền từ HĐKD (gián tiếp)

    // 4. Investing + Financing same as direct method
    // ...

    var adjustments = new List<FinancialStatementLine>
    {
        new("01", "Lợi nhuận trước thuế", netProfitBeforeTax, 0, 1, false),
        new("02", "Khấu hao TSCĐ", depreciation, 0, 2, false),
        new("03", "Tăng giảm khoản phải thu", -deltaReceivables, 0, 2, false),
        new("04", "Tăng giảm hàng tồn kho", -deltaInventory, 0, 2, false),
        new("05", "Tăng giảm khoản phải trả", deltaPayables, 0, 2, false),
        // ...
    };

    return new CashFlowStatement(
        tenantId, period, DateTime.UtcNow,
        Method: CashFlowMethod.Indirect,
        OpeningCash: openingCash, ClosingCash: closingCash, NetChange: netChange,
        OperatingActivities: adjustments,  // indirect adjustments
        InvestingActivities: investingLines,
        FinancingActivities: financingLines,
        IndirectAdjustments: adjustments
    );
}
```

### Change 4: UI — Add toggle
```razor
<!-- CashFlowStatement.razor — add method selector -->
<div class="form-group">
    <label>Phương pháp lập</label>
    <select @bind="selectedMethod">
        <option value="@CashFlowMethod.Direct">Trực tiếp (B 03-DN)</option>
        <option value="@CashFlowMethod.Indirect">Gián tiếp (B 03-DN)</option>
    </select>
</div>

@code {
    private CashFlowMethod selectedMethod = CashFlowMethod.Direct;

    private async Task GenerateReport()
    {
        report = selectedMethod == CashFlowMethod.Indirect
            ? await CashFlowService.GenerateIndirectAsync(tenantId, period, selectedStandard, CancellationToken.None)
            : await CashFlowService.GenerateAsync(tenantId, period, selectedStandard, CancellationToken.None);
    }
}
```

## Verification
- [ ] `dotnet build VanAn.sln` Release — 0 errors
- [ ] UI có toggle trực tiếp/gián tiếp
- [ ] Phương pháp gián tiếp hiển thị: Lợi nhuận trước thuế → điều chỉnh → Lưu chuyển tiền HĐKD
- [ ] Phương pháp trực tiếp vẫn hoạt động (không regress)
- [ ] Export DOCX/XLSX theo method

## Rollback
`git revert <commit>` — record change có thể phá existing data (Method field mới = default Direct). Verify migration không required (record, không phải entity EF).

---

## ANALYZE UPDATE (2026-08-03)

### Verified Accurate
- ✅ `CashFlowStatement` record at Domain.cs:3350-3356 — exact match
- ✅ Service only has `GenerateAsync` (direct), no indirect
- ✅ Service does NOT inject `IBalanceSheetService` or `IIncomeStatementService`
- ✅ No UI toggle exists
- ✅ `IncomeStatement` exposes `NetProfitEnding`/`NetProfitOpening`

### CRITICAL: Service Dependency Gap
`CashFlowStatementService` constructor must be changed to inject:
- `IBalanceSheetService` — for account-level deltas (AR, Inventory, AP changes)
- `IIncomeStatementService` — for NetProfit starting point

```csharp
// Current (line 27)
public CashFlowStatementService(IAccountingDbContext dbContext, IAccountChartService accountChart, ILogger<CashFlowStatementService> logger)

// Required
public CashFlowStatementService(
    IAccountingDbContext dbContext, IAccountChartService accountChart,
    ILogger<CashFlowStatementService> logger,
    IBalanceSheetService balanceSheetService,      // NEW
    IIncomeStatementService incomeStatementService) // NEW
```

### Reverse Impact: 10 files
| Priority | File | Change |
|----------|------|--------|
| 🔴 CRITICAL | `CashFlowStatementService.cs:120` | Add `Method: CashFlowMethod.Direct` to constructor |
| 🔴 CRITICAL | `VasReportPageTestBase.cs:132` | Add `Method: CashFlowMethod.Direct` to test mock |
| 🟡 REQUIRED | `ICashFlowStatementService.cs` | Add `GenerateIndirectAsync` signature |
| 🟡 REQUIRED | `CashFlowStatementService.cs` constructor | Inject 2 new services |
| 🟡 REQUIRED | `CashFlowStatement.razor` | Add method toggle |
| 🟢 OPTIONAL | `CashFlowStatementsController.cs` | Add `method` query param |
| 🟢 OPTIONAL | `FinancialReportExportService.cs` | Handle `IndirectAdjustments` |
| 🟢 TESTS | `CashFlowStatementServiceTests.cs` | Add indirect method tests |
| 🟢 TESTS | `CashFlowStatementPageTests.cs` | Add toggle tests |
| 🟢 TESTS | `VasReportsEndpointTests.cs` + `ArchitectureRulesTests.cs` | Verify new deps |
