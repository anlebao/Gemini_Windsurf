# TASK CARD — VAS Wave 3: Account Code Map

> **Status:** NOT STARTED | INVESTIGATE → PLAN → IMPLEMENT
> **Prerequisite:** W2 merged (Domain records available)
> **Branch:** `feature/vas-wave3-account-code-map`
> **Estimated sessions:** 1-2

## Objective
Create AccountCode mapping table + 3 standards (TT 99/133/58) + refactor hardcoded GetAccountName.

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

### W3-T3: Refactor HKDBookService.GetAccountName
- Replace hardcoded dictionary → call IAccountChartService
- Inject IAccountChartService vào HKDBookService constructor

### W3-T4: Build + guard pass

## Verification
- [ ] GetAccountNameAsync("511", TT133_2016) returns "Doanh thu bán HHDV"
- [ ] GetAccountTypeAsync("511", TT133_2016) returns Revenue
- [ ] Build pass + guard pass

## Rollback
- Git revert
- GetAccountName fallback to hardcoded if needed

## Open Questions
- Q1: DB table hay in-memory? (performance vs persistence)
- Q2: TT 99 phụ lục TK — tìm được chưa? (R1)
- Q3: Có cần migration cho AccountCharts table?
