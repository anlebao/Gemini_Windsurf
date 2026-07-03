# TASK CARD — VAS Wave 3: Account Code Map

> **Status:** NOT STARTED | INVESTIGATE → PLAN → IMPLEMENT
> **Prerequisite:** W2 merged (Domain records available)
> **Branch:** `feature/vas-wave3-account-code-map`
> **Estimated sessions:** 1-2

## Objective
Create AccountCode mapping table + 3 standards (TT 99/133/58) + **HKD→DN account mapping (D9)** + refactor hardcoded GetAccountName.

## Prerequisites (verify before code)
- [ ] W2 merged (AccountChartEntry, AccountingStandard enum available)
- [ ] Verify HKDBookService.GetAccountName method (hardcoded)
- [ ] Grep GetAccountName usage
- [ ] Search web for TT 99/2025 phụ lục TK (R1)

## Files to Create/Modify
| File | Action |
|------|--------|
| `3_CoreHub/Services/IAccountChartService.cs` | CREATE interface |
| `3_CoreHub/Services/AccountChartService.cs` | CREATE implementation |
| `3_CoreHub/Services/HKDBookService.cs` | MODIFY GetAccountName → call IAccountChartService |
| `5_WebApps/ShopERP/Program.cs` | ADD DI registration |

## Detailed Task List

### W3-T1: Create IAccountChartService + AccountChartService
```csharp
public interface IAccountChartService
{
    Task<string> GetAccountNameAsync(string accountCode, AccountingStandard standard);
    Task<List<AccountChartEntry>> GetAccountsByTypeAsync(AccountType type, AccountingStandard standard);
    Task<AccountType> GetAccountTypeAsync(string accountCode, AccountingStandard standard);
}
```

### W3-T2: Seed AccountChart data (3 standards)
- TT 99/2025: full TK 1xx-9xx (~100 accounts)
- TT 133/2016: rút gọn (~60 accounts)
- TT 58/2026: siêu nhỏ (~30 accounts)
- Store: DB table `AccountCharts` hoặc in-memory dictionary (decide in session)
- Ưu tiên TT 133 trước (R3)

### W3-T3: Add HKD→DN account mapping (D9 — for conversion opening balance)
Add mapping table for HKD internal synthetic accounts → DN chart of accounts:
```csharp
public interface IHkdToEnterpriseAccountMapper
{
    // Map HKD internal account → DN account code (per standard)
    string MapToEnterpriseAccount(string hkdAccountKey, AccountingStandard standard);
    // Get all mappings for a standard
    Dictionary<string, string> GetMappings(AccountingStandard standard);
}
```
Mapping data (HKD single-entry → DN double-entry):
| HKD internal key | DN TT 133 | DN TT 99 | DN TT 58 | Ghi chú |
|------------------|-----------|----------|----------|---------|
| Revenue | 511 | 511 | 511 | Doanh thu |
| COGS | 632 | 632 | 632 | Giá vốn |
| Cash | 111 | 111 | 111 | Tiền mặt |
| CashBank | 112 | 112 | 112 | Tiền gửi NH |
| Inventory | 156 | 156 | 156 | Hàng hóa |
| Materials | 152 | 152 | 152 | Vật liệu |
| SellingExpense | 641 | 641 | 641 | CP bán hàng |
| AdminExpense | 642 | 642 | 642 | CP QLDN |
| TaxOutput | 3331 | 3331 | 3331 | Thuế GTGT đầu ra |
| TaxInput | 1331 | 1331 | 1331 | Thuế GTGT đầu vào |
| Payroll | 334 | 334 | 334 | Phải trả lương |
| FixedAsset | 211 | 211 | 211 | TSCĐ |
| Depreciation | 214 | 214 | 214 | KH TSCĐ |
| Equity | 411 | 411 | 411 | Vốn CSH |

**Lưu ý:** HKD single-entry không có double-entry structure. Mapping là "best-effort" — opening balance migration (W8) sẽ cần manual review.

### W3-T4: Refactor HKDBookService.GetAccountName
- Replace hardcoded dictionary → call IAccountChartService
- Inject IAccountChartService vào HKDBookService constructor

### W3-T5: Build + guard pass

## Verification
- [ ] GetAccountNameAsync("511", TT133_2016) returns "Doanh thu bán HHDV"
- [ ] GetAccountTypeAsync("511", TT133_2016) returns Revenue
- [ ] MapToEnterpriseAccount("Revenue", TT133_2016) returns "511"
- [ ] Build pass + guard pass

## Rollback
- Git revert
- GetAccountName fallback to hardcoded if needed

## Open Questions
- Q1: DB table hay in-memory? (performance vs persistence)
- Q2: TT 99 phụ lục TK — tìm được chưa? (R1)
- Q3: Có cần migration cho AccountCharts table?
- Q4: HKD→DN mapping — có đủ HKD internal keys không? (verify HKDBookService hardcoded dictionary)
