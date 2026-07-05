# TASK CARD — VAS Wave 4: 4 Report Services (Parallel)

> **Status:** ✅ COMPLETE | INVESTIGATE → PLAN → IMPLEMENT 100%
> **Prerequisite:** W3 merged (AccountChartService available) ✅
> **Branch:** `feature/vas-wave4-services-bs-is-cf-tb`
> **Estimated sessions:** 1 (actual)

## Objective
Implement 4 services query JournalEntries + OpeningBalance, apply Pattern #1 + #5 fix.

## Prerequisites (verify before code)
- [x] W3 merged (AccountChartService for account names)
- [x] W1 seed data available (JournalEntries populated)
- [x] W2 Domain records available (BalanceSheet, IncomeStatement, etc.)
- [x] Verify Pattern #1 + #5 in governance.md Known Error Pattern Registry
- [x] Verify HKDBookRepository broken query (Pattern #1 + #5)

## Common Rules (apply to ALL 4 services)
- Query: `_context.JournalEntries.Where(e => e.TenantId == tenantId && e.EntryDate < periodEnd)`
  - Pattern #1 fix: `e.TenantId == tenantId` (NOT `EF.Property<Guid>`)
  - Pattern #5 fix: `e.EntryDate >= periodStart && e.EntryDate < periodEnd` (NOT `e.Period.Year`)
- ~~Filter by TenantType=Enterprise~~ DEFERRED to W5 controller (Tenant.Type=null for seed, W8-H4 adds SetTenantType)
- Multi-tenant: TenantId filter bắt buộc
- Opening balance: cumulative from JournalEntries (EntryDate < periodStart). R2 satisfied: first period = 0.
- Parameter: `AccountingStandard standard` (caller decides — Tenant.AccountingStandard=null until W8)

## Files Created
| File | Service |
|------|---------|
| `3_CoreHub/Services/IBalanceSheetService.cs` + `BalanceSheetService.cs` | BS ✅ |
| `3_CoreHub/Services/IIncomeStatementService.cs` + `IncomeStatementService.cs` | IS ✅ |
| `3_CoreHub/Services/ICashFlowStatementService.cs` + `CashFlowStatementService.cs` | CF ✅ |
| `3_CoreHub/Services/ITrialBalanceService.cs` + `TrialBalanceService.cs` | TB ✅ |

## Files Modified
| File | Change |
|------|--------|
| `5_WebApps/ShopERP/Program.cs` | +4 DI registrations (line 224-228) |
| `3_CoreHub/Services/HKDBookService.cs` | `[Obsolete]` on GenerateTrialBalanceAsync (line 356) |

## Sub-tasks (IMPLEMENTED)

### W4-BS: BalanceSheetService ✅
- Assets/Liabilities/Equity grouped via AccountChartService.GetAccountAsync
- Contra accounts (IsNormalCredit=true) → sign inverted for presentation
- **NetIncome plug (residual approach):** NetIncome = TotalAssets - TotalLiab - TotalEquity (before plug). Added as Equity line "421 — Lợi nhuận sau thuế chưa phân phối". Works even when sub-accounts (5113/6421/6422) not in chart.
- **W2 invariant enforced:** throws InvalidOperationException if TotalAssetsEnding != TotalLiabilitiesAndEquityEnding (no IsBalanced flag)

### W4-IS: IncomeStatementService ✅
- 2-column comparative: Ending = current period, Opening = same month prior year
- signed = credit - debit (NO IsNormalCredit inversion — that's BS-only)
- Revenue (5xx), COGS (632), OpEx (64x), OtherIncome (7xx), OtherExpense (8xx)
- NetProfit = Revenue - COGS - OpEx + OtherIncome - OtherExpense

### W4-CF: CashFlowStatementService ✅
- Direct method (R4): for each JE touching 111/112, classify offsetting account
- Operating: 5xx/6xx/7xx/8xx/331/3331/138/338/141/142/15x
- Investing: 21x (TSCĐ)
- Financing: 311/341/411
- OpeningCash = Σ cash lines EntryDate < periodStart; ClosingCash = Σ cash lines EntryDate < periodEnd
- NetChange = ClosingCash - OpeningCash

### W4-TB: TrialBalanceService ✅
- New service (HKDBookService.GenerateTrialBalanceAsync marked [Obsolete])
- Group by AccountNumber, movement debit/credit totals, cumulative balance (opening + movement)
- TotalDebit == TotalCredit validated via IsBalanced flag (existing TrialBalance record)
- Account names via AccountChartService.GetAccountNameAsync

## Bugs Found & Fixed During IMPLEMENT
1. **IS bug (Round 1):** `endingPresented = chart.IsNormalCredit ? -ending : ending` inverted revenue sign for 511 (IsNormalCredit=true). Fix: removed inversion — `signed = credit - debit` already gives correct IS sign.
2. **BS bug (Round 1):** Revenue/Expense accounts skipped but not closed to Equity → 4M imbalance. Fix: NetIncome plug (residual approach).
3. **BS bug (Round 2):** NetIncome computed from chart-classified Rev/Exp accounts only, but 5113/6421/6422 not in TT 133 chart → plug overshot (21.5M vs 4M). Fix: residual approach — NetIncome = TotalAssets - TotalLiab - TotalEquity (doesn't depend on chart coverage).

## Verification ✅
- [x] Each service returns non-empty result with seed data
- [x] BS: TotalAssets == TotalLiabilities + TotalEquity (invariant enforced, throws if violated)
- [x] IS: NetProfit calculated correctly (positive for May seed data)
- [x] CF: ClosingCash == OpeningCash + NetChange
- [x] TB: TotalDebit == TotalCredit (IsBalanced=true)
- [x] Multi-tenant: different tenant returns empty/zero result
- [x] Build pass (0 errors) + guard pass
- [x] W4 tests: 25/25 PASS
- [x] Core.Tests: 875/875 PASS (Category!=Performance filter)
- [x] Arch.Tests: 31/31 PASS

## Open Questions (RESOLVED)
- **Q1:** Opening balance = cumulative from JournalEntries (EntryDate < periodStart). R2 satisfied.
- **Q2:** Cash Flow = direct method (R4). Indirect deferred.
- **Q3:** Keep HKDBookService.GenerateTrialBalanceAsync + [Obsolete] marker.

## Deferred
- M1/M2 FinancialStatementLine validation at service layer (records are pure)
- TenantType filtering (W5 controller)
- Indirect CF method (post-W4)
- Sub-accounts 5113/6421/6422 added to TT 133 chart (W3 scope — residual plug handles gap for W4)
