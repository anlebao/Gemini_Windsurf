# TASK CARD — VAS Wave 2: Domain Records

> **Status:** NOT STARTED | INVESTIGATE → PLAN → IMPLEMENT
> **Prerequisite:** W1 merged to main · **Domain modification APPROVED (D5)**
> **Branch:** `feature/vas-wave2-domain-records`
> **Estimated sessions:** 1

## Objective
Add Domain records cho 4 BCTC + OpeningBalance + AccountChart + TenantType enum + **HKD↔DN conversion fields (D9)**.

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
| `1_Shared/Domain/Aggregates/TenantAggregate/Tenant.cs` | ADD conversion fields (D9) |
| `1_Shared/Domain/Aggregates/TenantAggregate/TenantStatus.cs` | ADD `Converted` status (D9) |
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

### W2-T3: Add HKD↔DN conversion fields to Tenant (D9 — Option B: New Tenant + Link)
Add to `Tenant.cs`:
```csharp
// D9: HKD→DN conversion link (Option B — New Tenant + Link)
// Predecessor: Tenant cũ (HKD) mà DN này được convert từ
public TenantId? PredecessorTenantId { get; private set; }
// Successor: Tenant mới (DN) mà HKD này đã convert sang
public TenantId? SuccessorTenantId { get; private set; }
public DateTime? ConvertedAt { get; private set; }
public AccountingStandard? ConvertedToStandard { get; private set; }  // Chuẩn kế toán DN mới

// Factory: Create DN from HKD conversion
public static Tenant CreateFromConversion(
    TenantId newId, string name, TenantType newType,
    TenantId predecessorTenantId, AccountingStandard standard,
    TenantSettings? settings = null)
{
    ArgumentException.ThrowIfNullOrWhiteSpace(name);
    var tenant = new Tenant
    {
        Id = newId,
        Name = name,
        BusinessType = BusinessType.Company,
        Status = TenantStatus.Active,
        Settings = settings ?? TenantSettings.Empty(),
        PredecessorTenantId = predecessorTenantId,
        ConvertedAt = DateTime.UtcNow,
        ConvertedToStandard = standard
    };
    tenant.SetTenantId(newId);
    tenant.AddDomainEvent(new TenantCreatedEvent(newId.Value, name, settings?.ContactEmail, DateTime.UtcNow));
    return tenant;
}

// Mark HKD as converted to DN (deactivate + link successor)
public void MarkConvertedTo(TenantId successorTenantId)
{
    if (Status == TenantStatus.Inactive)
        throw new InvalidOperationException("Cannot convert an inactive tenant.");
    Status = TenantStatus.Converted;
    SuccessorTenantId = successorTenantId;
    UpdateAudit();
    AddDomainEvent(new TenantConvertedEvent(Id.Value, successorTenantId.Value, DateTime.UtcNow));
}
```

Add to `TenantStatus.cs`:
```csharp
Converted = 4  // HKD đã chuyển đổi thành DN — read-only, historical reports vẫn truy cập
```

Add `TenantConvertedEvent` domain event (in Events folder).

### W2-T4: Build + guard pass
- No service changes yet, just Domain records
- Build must pass (records only, no logic)

### W2-T5: Architecture tests verify
- Domain still pure (no EF Core, no DbContext, no DataAnnotations)
- All architecture tests pass (28+ including new conversion fields)

## Verification
- [ ] Build 0 errors
- [ ] guard-check.ps1 pass
- [ ] Architecture tests pass
- [ ] Domain records không có EF Core references
- [ ] Tenant.CreateFromConversion factory works
- [ ] Tenant.MarkConvertedTo sets Status=Converted + SuccessorTenantId

## Rollback
- Git revert (Domain records + conversion fields, no service dependency yet)

## Open Questions
- Q1: Append to Domain.cs hay tạo file mới VASReports.cs?
- Q2: TrialBalance record đã có — có cần modify hay giữ nguyên?
- Q3: AccountChartEntry có cần EF Core mapping (W3) hay chỉ in-memory?
- Q4: TenantConvertedEvent — cần thêm event handler (outbox) hay chỉ Domain event?
- Q5: Converted status — có cần separate enum value hay reuse Inactive + SuccessorTenantId?
