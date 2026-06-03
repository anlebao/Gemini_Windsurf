# Accounting Domain

> **Kế toán VAT 2026 compliant - Immutability và Audit Trail**

## Overview

Accounting domain quản lý ghi nhận doanh thu, chi phí, và báo cáo tài chính theo quy định VAT 2026 cho cả Doanh nghiệp và Hộ kinh doanh (HKD).

**Key Principle**: Tất cả entries là **immutable append-only** (ADR-003).

## Business Types

```csharp
public enum BusinessType
{
    Company = 1,           // Doanh nghiệp - Thông tư 200/2014/TT-BTC
    HouseholdBusiness = 2  // Hộ kinh doanh - Thông tư 152/2025/TT-BTC
}

public enum HKDGroup
{
    Group1 = 1,  // Không chịu thuế GTGT, không nộp thuế TNCN
    Group2 = 2,  // Nộp thuế GTGT và TNCN theo tỷ lệ %
    Group3 = 3   // Hoạt động chịu các loại thuế khác
}
```

## Entities

### AccountingEntry (Immutable)

```csharp
public sealed class AccountingEntry : BaseEntity
{
    // Read-only properties - NO SETTERS
    public decimal Amount { get; }
    public AccountingEntryType EntryType { get; }
    public VatRate VatRate { get; }
    public DateTime TransactionDate { get; }
    public AccountingBookType AccountingBookType { get; }
    public int PeriodYear { get; }
    public int PeriodMonth { get; }
    public Guid? ReversalEntryId { get; }  // Link to reversal if any
    public string Description { get; }
    public Guid? ReferenceId { get; }  // Link to Order/Payment
    public string? ReferenceType { get; }
    
    // Private constructor - Factory only
    private AccountingEntry() { }
}

public enum AccountingEntryType
{
    Revenue = 1,
    Expense = 2,
    TaxPayment = 3,
    Adjustment = 4
}

public enum VatRate
{
    Exempt = -1,
    Zero = 0,
    Five = 5,
    Ten = 10
}
```

### AccountingBookType

```csharp
public enum AccountingBookType
{
    // COMPANY BOOKS
    RevenueBook = 1,        // Sách chi doanh thu
    ExpenseBook = 2,        // Sách chi chi phí
    CashBankBook = 3,       // Sách chi tiền mặt ngân hàng
    TaxDeclarationBook = 4, // Sách chi kê khai thuế
    
    // HKD BOOKS - 7 types per Thông tư 152/2025/TT-BTC
    S1a_HKD = 5,   // Sổ theo dõi hàng hóa (không chịu thuế GTGT)
    S2a_HKD = 6,   // Sổ theo dõi hàng hóa (nộp thuế GTGT)
    S2b_HKD = 7,   // Sổ doanh thu bán hàng
    S2c_HKD = 8,   // Sổ chi tiết doanh thu, chi phí
    S2d_HKD = 9,   // Sổ chi tiết vật liệu, dụng cụ
    S2e_HKD = 10,  // Sổ chi tiết tiền
    S3a_HKD = 11    // Sổ theo dõi hoạt động chịu thuế khác
}
```

### AccountingPeriod

```csharp
public record AccountingPeriod(int Year, int Month)
{
    public override string ToString() => $"{Year:0000}-{Month:00}";
    public static AccountingPeriod FromDateTime(DateTime date) => new(date.Year, date.Month);
    public DateTime StartDate => new DateTime(Year, Month, 1);
    public DateTime EndDate => StartDate.AddMonths(1).AddTicks(-1);
}
```

## Factory Methods

```csharp
// Revenue entry
public static AccountingEntry CreateRevenue(
    TenantId tenantId, 
    AccountingPeriod period, 
    Money amount, 
    string description)

// Expense entry
public static AccountingEntry CreateExpense(
    TenantId tenantId, 
    AccountingPeriod period, 
    Money amount, 
    string description)

// Reversal (for corrections)
public static AccountingEntry CreateReversal(
    AccountingEntry original, 
    string reason)
// Returns entry with -Amount and link to original
```

## Business Rules

### BR-001: Immutability
- **No edits**: Không thể sửa bất kỳ field nào sau khi tạo
- **No delete**: Không xóa entries
- **Reversal only**: Sai → tạo reversal entry với amount âm

### BR-002: Reversal Pattern
```csharp
// ❌ WRONG
entry.Amount = 1000;  // COMPILER ERROR

// ✅ CORRECT
var original = AccountingEntry.CreateRevenue(..., 1000m, ...);
var reversal = AccountingEntry.CreateReversal(original, "Sai số liệu");
// reversal.Amount = -1000m
// reversal.ReversalEntryId = original.Id
```

### BR-003: Period Integrity
- Entries được ghi vào period dựa trên TransactionDate
- Sau khi **Period Closed**: Không thể thêm entry mới
- Period close = khóa sổ kỳ

### BR-004: VAT Calculation
```csharp
public decimal VatAmount => EntryType switch
{
    AccountingEntryType.Revenue => Amount * (int)VatRate / 100,
    AccountingEntryType.Expense => Amount * (int)VatRate / 100,
    _ => 0
};
```

### BR-005: Book Type Selection
| Business Type | Entry Type | Book Type |
|--------------|------------|-----------|
| Company | Revenue | RevenueBook |
| Company | Expense | ExpenseBook |
| HKD Group 1 | Revenue | S1a_HKD |
| HKD Group 2 | Revenue | S2b_HKD |
| HKD Group 2 | Expense | S2c_HKD |

## Reporting

### Balance Summary
```csharp
public class BalanceSummary
{
    public decimal TotalRevenue { get; set; }
    public decimal TotalExpenses { get; set; }
    public decimal NetProfit => TotalRevenue - TotalExpenses;
    public decimal VatPayable { get; set; }
    public decimal TaxEstimate { get; set; }
    public AccountingPeriod Period { get; set; }
}
```

### Period Comparison
```csharp
public class PeriodComparison
{
    public decimal RevenueDeltaPercent { get; set; }
    public string RevenueLabel { get; set; } = "";
    public decimal ExpenseDeltaPercent { get; set; }
    public string ExpenseLabel { get; set; } = "";
    public decimal ProfitDeltaPercent { get; set; }
    public string ProfitLabel { get; set; } = "";
}
```

## Integration Points

| Domain | Integration | Trigger |
|--------|-------------|---------|
| Order | Revenue entry | Order → Completed |
| Inventory | COGS entry | Inventory transaction |
| Payment | Payment tracking | Payment → Paid |
| Reporting | Balance calculations | Period close request |

## Events

```csharp
public record AccountingEntryCreated(
    AccountingEntryId EntryId, 
    TenantId TenantId, 
    AccountingPeriod Period, 
    decimal Amount);

public record AccountingEntryReversed(
    AccountingEntryId OriginalId, 
    AccountingEntryId ReversalId, 
    string Reason);

public record PeriodClosed(
    TenantId TenantId, 
    AccountingPeriod Period, 
    DateTime ClosedAt);
```

## DTOs

### RevenueEntryRequest
```csharp
public class RevenueEntryDto
{
    public DateTime Date { get; set; }
    public decimal Amount { get; set; }
    public string Description { get; set; } = "";
    public VatRate VatRate { get; set; } = VatRate.Ten;
    public AccountingBookType? BookType { get; set; }  // Auto-detected if null
}
```

### ExpenseEntryRequest
```csharp
public class ExpenseEntryDto
{
    public DateTime Date { get; set; }
    public decimal Amount { get; set; }
    public string Description { get; set; } = "";
    public VatRate VatRate { get; set; } = VatRate.Ten;
    public string? Category { get; set; }
}
```

### AccountingEntryResponse
```csharp
public class AccountingEntryDto
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public decimal Amount { get; set; }
    public string EntryType { get; set; } = "";
    public string Description { get; set; } = "";
    public DateTime TransactionDate { get; set; }
    public int PeriodYear { get; set; }
    public int PeriodMonth { get; set; }
    public string? ReferenceType { get; set; }
    public Guid? ReversalEntryId { get; set; }
    public bool IsReversal => Amount < 0;
}
```

## Period Closing Workflow

```
1. User requests period close
     ↓
2. Validate all entries (no draft, no pending)
     ↓
3. Generate reports (Revenue, Expense, VAT)
     ↓
4. Lock period (no new entries allowed)
     ↓
5. Create closing snapshot
     ↓
6. Notify user (ready for tax filing)
```

## Compliance Notes

### Company (Thông tư 200)
- Sổ kế toán chi tiết theo từng account
- Báo cáo tài chính: BCTC, BCKQKD, BCLCTT
- Thời hạn nộp: Quý 4 hàng năm

### HKD Group 1 (Thông tư 152)
- Sổ S1a: Theo dõi hàng hóa, dịch vụ
- Không chịu thuế GTGT
- Không nộp thuế TNCN

### HKD Group 2 (Thông tư 152)
- 7 loại sổ (S1a, S2a, S2b, S2c, S2d, S2e, S3a)
- Nộp thuế GTGT theo tỷ lệ doanh thu
- Nộp thuế TNCN theo tỷ lệ doanh thu

## References

- `1_Shared/Domain.cs` - AccountingEntry implementation
- `docs/decisions/ADR-003-Accounting-Immutability.md`
- `.windsurf/skills/domain-integrity-validation.md`
- Thông tư 200/2014/TT-BTC
- Thông tư 152/2025/TT-BTC

---

*Last Updated: June 1, 2026*  
*Compliance: VAT 2026 Ready*
