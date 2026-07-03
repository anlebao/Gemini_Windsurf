# TASK CARD — VAS Wave 2: Domain Records

> **Status:** NOT STARTED | INVESTIGATE → PLAN → IMPLEMENT
> **Prerequisite:** W1 merged to main · **Domain modification APPROVED (D5)**
> **Branch:** `feature/vas-wave2-domain-records`
> **Estimated sessions:** 1

## Objective
Add Domain records cho 4 BCTC + OpeningBalance + AccountChart + TenantType enum.

## Prerequisites (verify before code)
- [ ] W1 merged (seed data available)
- [ ] Verify Domain.cs structure: `1_Shared/Domain.cs`
- [ ] Verify existing TrialBalance record (Domain.cs:1518)
- [ ] Check architecture tests: `6_Tests/VanAn.Architecture.Tests/`
- [ ] Verify Domain purity rules (governance.md)

## Files to Modify
| File | Action |
|------|--------|
| `1_Shared/Domain.cs` OR `1_Shared/Domain/VASReports.cs` (new) | ADD records |
| `6_Tests/VanAn.Architecture.Tests/` | VERIFY still pass |

## Detailed Task List

### W2-T1: Add Domain records
Add these records (decide: append to Domain.cs or new file VASReports.cs):

```csharp
public record BalanceSheet(
    TenantId TenantId, AccountingPeriod Period, DateTime GeneratedAt,
    IEnumerable<BalanceSheetLine> Assets,
    IEnumerable<BalanceSheetLine> Liabilities,
    IEnumerable<BalanceSheetLine> Equity,
    decimal TotalAssets, decimal TotalLiabilities, decimal TotalEquity,
    bool IsBalanced
);
public record BalanceSheetLine(string AccountCode, string AccountName, decimal Amount, bool IsDebitNormal);

public record IncomeStatement(
    TenantId TenantId, AccountingPeriod Period, DateTime GeneratedAt,
    decimal Revenue, decimal CostOfGoodsSold, decimal GrossProfit,
    decimal OperatingExpenses, decimal OperatingProfit,
    decimal OtherIncome, decimal OtherExpense, decimal NetProfit,
    IEnumerable<IncomeStatementLine> Lines
);
public record IncomeStatementLine(string AccountCode, string AccountName, decimal Amount, bool IsRevenue);

public record CashFlowStatement(
    TenantId TenantId, AccountingPeriod Period, DateTime GeneratedAt,
    decimal OpeningCash, decimal ClosingCash, decimal NetChange,
    decimal OperatingActivities, decimal InvestingActivities, decimal FinancingActivities,
    IEnumerable<CashFlowLine> Lines
);
public record CashFlowLine(string Category, decimal Amount, string Description);

// TrialBalance đã có (Domain.cs:1518) — giữ nguyên, chỉ sửa service ở W4

public record OpeningBalance(
    TenantId TenantId, AccountingPeriod Period,
    IEnumerable<OpeningBalanceLine> Lines
);
public record OpeningBalanceLine(string AccountCode, decimal DebitOpening, decimal CreditOpening);

public record AccountChartEntry(
    string AccountCode, string AccountName, AccountType Type,
    AccountingStandard Standard
);
public enum AccountType { Asset, Liability, Equity, Revenue, Expense, Contra }
public enum AccountingStandard { TT99_2025, TT133_2016, TT58_2026 }
```

### W2-T2: Add TenantType enum
```csharp
public enum TenantType
{
    HKD,                    // Hộ kinh doanh (TT 152/2025)
    Enterprise_SuperSmall,  // DN siêu nhỏ (TT 58/2026)
    Enterprise_SME,         // DN vừa và nhỏ (TT 133/2016)
    Enterprise_Large        // DN lớn (TT 99/2025)
}
```

### W2-T3: Build + guard pass
- No service changes yet, just Domain records
- Build must pass (records only, no logic)

### W2-T4: Architecture tests verify
- Domain still pure (no EF Core, no DbContext, no DataAnnotations)
- All 28 architecture tests pass

## Verification
- [ ] Build 0 errors
- [ ] guard-check.ps1 pass
- [ ] Architecture tests 28/28 pass
- [ ] Domain records không có EF Core references

## Rollback
- Git revert (Domain records only, no service dependency yet)

## Open Questions
- Q1: Append to Domain.cs hay tạo file mới VASReports.cs?
- Q2: TrialBalance record đã có — có cần modify hay giữ nguyên?
- Q3: AccountChartEntry có cần EF Core mapping (W3) hay chỉ in-memory?
