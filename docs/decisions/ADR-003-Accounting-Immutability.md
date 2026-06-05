# ADR-003: Accounting Immutability

## Status

**Approved** (2026-06-01)

## Context

ShopERP cần đáp ứng **VAT 2026 compliance** cho cả Doanh nghiệp và Hộ kinh doanh. Yêu cầu pháp lý:

- **Audit trail**: Mọi giao dịch kế toán phải truy vết được
- **No silent edits**: Không được sửa/xóa bút toán đã ghi sổ
- **Reversal only**: Nếu sai, phải lập bút toán điều chỉnh (reversal)
- **Period integrity**: Số liệu kỳ không thay đổi sau khi khóa

Business types cần support:
- **Company**: Theo Thông tư 200/2014/TT-BTC
- **HKD**: Theo Thông tư 152/2025/TT-BTC (7 loại sổ)

## Decision

**Accounting entries là IMMUTABLE - Append-only, không edit, không delete. Chỉ reversal được phép.**

```csharp
public sealed class AccountingEntry : BaseEntity
{
    // All properties are GET-ONLY (no setter)
    public decimal Amount { get; }  // No setter!
    public AccountingEntryType EntryType { get; }
    public VatRate VatRate { get; }
    public DateTime TransactionDate { get; }
    public AccountingBookType AccountingBookType { get; }
    public int PeriodYear { get; }
    public int PeriodMonth { get; }
    public Guid? ReversalEntryId { get; }  // Link to reversal if any
    
    // Private constructor - Factory only
    private AccountingEntry() { }
    
    // Factory methods - Immutable creation
    public static AccountingEntry CreateRevenue(...)
    public static AccountingEntry CreateExpense(...)
    public static AccountingEntry CreateReversal(AccountingEntry original, string reason)
}
```

### Immutability Rules

1. **No setters** - Properties chỉ có getter
2. **Private constructor** - Chỉ factories có thể tạo
3. **No Update method** - Không có method nào thay đổi properties
4. **Reversal pattern** - Sai thì tạo entry mới với amount âm
5. **Period locked** - Sau khi khóa kỳ, không thêm entry được

### Reversal Pattern

```csharp
// ❌ WRONG: Edit existing entry
entry.Amount = 1000;  // COMPILER ERROR - no setter

// ✅ CORRECT: Create reversal entry
var reversal = AccountingEntry.CreateReversal(originalEntry, "Sai số liệu");
// reversal.Amount = -originalEntry.Amount
// reversal.ReversalEntryId = originalEntry.Id
```

### Accounting Book Types

```csharp
public enum AccountingBookType
{
    // Company books
    RevenueBook = 1,
    ExpenseBook = 2,
    CashBankBook = 3,
    TaxDeclarationBook = 4,
    
    // HKD books (7 types per Thông tư 152/2025/TT-BTC)
    S1a_HKD = 5,   // Theo dõi hàng hóa (không chịu thuế GTGT)
    S2a_HKD = 6,   // Theo dõi hàng hóa (nộp thuế GTGT)
    S2b_HKD = 7,   // Doanh thu bán hàng
    S2c_HKD = 8,   // Chi tiết doanh thu, chi phí
    S2d_HKD = 9,   // Chi tiết vật liệu, dụng cụ
    S2e_HKD = 10,  // Chi tiết tiền
    S3a_HKD = 11   // Hoạt động chịu thuế khác
}
```

## Consequences

### Positive

- [x] **Audit compliance**: Đáp ứng Thông tư 200 và 152
- [x] **Data integrity**: Không thể "cheating" số liệu
- [x] **Simple reasoning**: What you see is what happened
- [x] **Event sourcing friendly**: Phù hợp với ADR-001 (SQLite + NATS)
- [x] **Clear history**: Reversal chain shows complete story

### Negative

- [ ] **Storage growth**: Data không xóa, chỉ tăng
- [ ] **Complex corrections**: Cần hiểu reversal pattern
- [ ] **UI complexity**: Phải explain reversal cho users
- [ ] **Migration difficulty**: Không thể "fix" data cũ

### Risks

| Risk | Impact | Mitigation |
|------|--------|------------|
| User confusion | Medium | Training, UI warnings, confirmation dialogs |
| Storage cost | Low | Archive old periods, data compression |
| Wrong entry | Medium | Confirmation workflow, draft mode |
| Legal changes | Medium | ADR review cycle, extensible design |

## Alternatives Considered

| Option | Pros | Cons | Decision |
|--------|------|------|----------|
| Mutable with audit log | Flexible, familiar | Audit log có thể tampered | Rejected |
| Soft delete (IsDeleted) | Simple | Still mutable, not compliant | Rejected |
| Pure immutable | Compliant, trustworthy | Learning curve | **Selected** |

## Implementation

- [x] `AccountingEntry` sealed class with read-only properties
- [x] Factory methods for all entry types
- [x] Reversal pattern implementation
- [x] `AccountingPeriod` value object
- [x] Period locking mechanism
- [ ] UI for reversal workflow
- [ ] Report showing original + reversal pairs
- [ ] Period close/lock automation

## Code Examples

### Creating Entries

```csharp
// Revenue entry
var revenue = AccountingEntry.CreateRevenue(
    tenantId: tenantId,
    period: new AccountingPeriod(2026, 6),
    amount: Money.FromDecimal(1000000),
    description: "Bán cà phê sáng"
);

// Expense entry
var expense = AccountingEntry.CreateExpense(
    tenantId: tenantId,
    period: new AccountingPeriod(2026, 6),
    amount: Money.FromDecimal(500000),
    description: "Mua nguyên liệu"
);

// Reversal (when mistake found)
var reversal = AccountingEntry.CreateReversal(
    original: revenue,
    reason: "Khách trả lại hàng"
);
```

### Querying

```csharp
// Get net amount (original - reversals)
var entries = await _dbContext.AccountingEntries
    .Where(e => e.TenantId == tenantId)
    .Where(e => e.PeriodYear == 2026 && e.PeriodMonth == 6)
    .ToListAsync();

var netRevenue = entries
    .Where(e => e.EntryType == AccountingEntryType.Revenue)
    .Sum(e => e.Amount);  // Automatically includes reversals (negative amounts)
```

## References

- `.windsurf/rules/.windsurfrules` - AccountingEntry immutability section
- `.windsurf/skills/domain-integrity-validation.md`
- `1_Shared/Domain.cs` - AccountingEntry implementation
- `6_Tests/VanAn.Core.Tests/Accounting/` - Immutability tests

## Related

- ADR-001: Event-driven sync phù hợp với append-only model
- ADR-002: Multi-tenancy cần được preserve cho accounting
- ADR-004: UI components cho accounting phải respect immutability

## Notes

- **Proposed by**: AI Assistant
- **Approved by**: User (implicit via roadmap approval)
- **Date**: 2026-06-01
- **Review cycle**: 3 months (compliance-critical)
- **Hard stop**: AI MUST refuse any Domain change that threatens AccountingEntry immutability
- **Legal reference**: Thông tư 200/2014/TT-BTC, Thông tư 152/2025/TT-BTC
