# TASK CARD — VAS Wave 4: 4 Report Services (Parallel)

> **Status:** NOT STARTED | INVESTIGATE → PLAN → IMPLEMENT
> **Prerequisite:** W3 merged (AccountChartService available)
> **Branch:** `feature/vas-wave4-services-bs-is-cf-tb`
> **Estimated sessions:** 2-3 (4 services song song)

## Objective
Implement 4 services query JournalEntries + OpeningBalance, apply Pattern #1 + #5 fix.

## Prerequisites (verify before code)
- [ ] W3 merged (AccountChartService for account names)
- [ ] W1 seed data available (JournalEntries populated)
- [ ] W2 Domain records available (BalanceSheet, IncomeStatement, etc.)
- [ ] Verify Pattern #1 + #5 in governance.md Known Error Pattern Registry
- [ ] Verify HKDBookRepository broken query (Pattern #1 + #5)

## Common Rules (apply to ALL 4 services)
- Query: `_context.JournalEntries.Where(e => e.TenantId == tenantId && e.EntryDate >= periodStart && e.EntryDate < periodEnd)`
  - Pattern #1 fix: `e.TenantId == tenantId` (NOT `EF.Property<Guid>`)
  - Pattern #5 fix: `e.EntryDate >= periodStart && e.EntryDate < periodEnd` (NOT `e.Period.Year`)
- Filter by TenantType=Enterprise (HKD reports tách riêng)
- Multi-tenant: TenantId filter bắt buộc
- Opening balance: query OpeningBalance entity cho kỳ trước + accumulate (R2: start with 0)

## Files to Create
| File | Service |
|------|---------|
| `3_CoreHub/Services/IBalanceSheetService.cs` + `BalanceSheetService.cs` | BS |
| `3_CoreHub/Services/IIncomeStatementService.cs` + `IncomeStatementService.cs` | IS |
| `3_CoreHub/Services/ICashFlowStatementService.cs` + `CashFlowStatementService.cs` | CF |
| `3_CoreHub/Services/ITrialBalanceService.cs` + `TrialBalanceService.cs` | TB (rewrite from HKDBookService) |

## Sub-tasks (song song)

### W4-BS: BalanceSheetService
- Assets: SUM debit TK 1xx - SUM credit TK 2xx (contra-asset)
- Liabilities: SUM credit TK 3xx (trừ 4xx) - SUM debit TK 3xx (contra)
- Equity: SUM credit TK 4xx
- Validate: TotalAssets == TotalLiabilities + TotalEquity

### W4-IS: IncomeStatementService
- Revenue: SUM credit TK 5xx
- COGS: SUM debit TK 632
- OpEx: SUM debit TK 641 + 642
- OtherIncome: SUM credit TK 7xx
- OtherExpense: SUM debit TK 8xx
- NetProfit = Revenue - COGS - OpEx + OtherIncome - OtherExpense

### W4-CF: CashFlowStatementService
- OpeningCash: SUM debit TK 111 + 112 (opening balance)
- OperatingActivities: direct method first (R4), indirect later
- InvestingActivities: TK 211/213/217 changes
- FinancingActivities: TK 311/341 changes
- ClosingCash = OpeningCash + NetChange

### W4-TB: TrialBalanceService (rewrite)
- Replace HKDBookService.GenerateTrialBalanceAsync
- Group by AccountNumber, SUM debit/credit
- Include opening balance per account
- Validate total debit == total credit
- Fix Pattern #1 + #5 in query

## Verification
- [ ] Each service returns non-empty result with seed data
- [ ] BS: TotalAssets == TotalLiabilities + TotalEquity
- [ ] IS: NetProfit calculated correctly
- [ ] CF: ClosingCash == OpeningCash + NetChange
- [ ] TB: TotalDebit == TotalCredit
- [ ] Multi-tenant: tenant A data không leak tenant B
- [ ] Build pass + guard pass

## Rollback
- Git revert
- HKDBookService.GenerateTrialBalanceAsync vẫn còn (don't delete, just deprecate)

## Open Questions
- Q1: Opening balance — start with 0 hay implement accumulate? (R2)
- Q2: Cash Flow — direct hay indirect method? (R4)
- Q3: Có keep HKDBookService.GenerateTrialBalanceAsync hay delete?
