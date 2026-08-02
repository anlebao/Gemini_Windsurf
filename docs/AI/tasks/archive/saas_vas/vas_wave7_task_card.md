# TASK CARD — VAS Wave 7: Tests with Numeric Assertions

> **Status:** ✅ COMPLETE | INVESTIGATE → PLAN → IMPLEMENT 100%
> **Prerequisite:** W6 merged (UI + API + services available) ✅
> **Branch:** `feature/vas-wave7-numeric-tests`
> **Estimated sessions:** 1 (actual)

## Objective
Unit + integration tests with specific numeric values, not just Assert.NotNull.

## Files Modified (5 existing test files)
| File | Tests Added |
|------|-------------|
| `6_Tests/VanAn.Core.Tests/Services/BalanceSheetServiceTests.cs` | +5 W7 numeric tests |
| `6_Tests/VanAn.Core.Tests/Services/IncomeStatementServiceTests.cs` | +5 W7 numeric tests |
| `6_Tests/VanAn.Core.Tests/Services/CashFlowStatementServiceTests.cs` | +5 W7 numeric tests |
| `6_Tests/VanAn.Core.Tests/Services/TrialBalanceServiceTests.cs` | +5 W7 numeric tests |
| `6_Tests/VanAn.Integration.Tests/VasReportsEndpointTests.cs` | +4 W7 numeric API tests |

## Files Created (1 new test file)
| File | Tests |
|------|-------|
| `6_Tests/VanAn.Core.Tests/Services/VasMultiTenantTests.cs` | 5 multi-tenant isolation tests |

## Test Summary
- **Core.Tests W7:** 20 tests (5 BS + 5 IS + 5 CF + 5 TB) — all PASS
- **Multi-tenant W7:** 5 tests (BS + IS + CF + TB + DB query) — all PASS
- **Integration W7:** 4 tests (BS + IS + CF + TB endpoint numeric assertions) — all PASS
- **Total W7:** 29 tests, 29/29 PASS

## Key Numeric Values Verified (period 2026-06, VasSampleDataSeeder)

### Balance Sheet
- TotalAssetsEnding = 433.5M (> 400M threshold)
- TotalAssetsOpening = 431.5M (> 400M threshold)
- Account 111 (Tiền mặt) Ending = 46M
- Assets line count >= 4 (actual: 6)
- Equity includes NetIncome plug (421 line)

### Income Statement
- TotalRevenueEnding = 45M (511: 15M + 30M; T19's 5M in July)
- TotalRevenueOpening = 0 (no 2025-06 entries)
- NetProfitEnding = 13.5M (Revenue 45M - COGS 31.5M; 6421/6422 not in chart → OpEx = 0)
- NetProfit formula: Revenue - COGS = 13.5M ✓
- Expense line count >= 1 (only 632; 6421/6422 sub-accounts not in chart)

### Cash Flow Statement
- OpeningCash = 172.5M (111: 50.5M + 112: 122M)
- ClosingCash = 209M (T19/T20 in July, not included)
- NetChange = 36.5M (Closing - Opening)
- NetChange formula: Closing - Opening = 36.5M ✓
- Operating activities non-empty (6 lines)

### Trial Balance
- TotalDebit = TotalCredit = 124M (8 June entries; T19/T20 in July)
- IsBalanced = true
- Account count >= 10 (actual: 15)
- Account 511 Credit = 45M
- Account 632 Debit = 31.5M

### Integration Tests (own seed, period 2026-06)
- BS: TotalAssets = 432M
- IS: Revenue = 10M, NetProfit = 3M (6421 not in chart → skipped)
- CF: ClosingCash = 159M
- TB: TotalDebit = TotalCredit = 450M, IsBalanced = true

## Key Findings During Implementation
1. **T19/T20 date overflow:** VasSampleDataSeeder uses `baseDate.AddDays(16/18)` with baseDate=2026-06-15 → T19 (06-15+16=07-01) and T20 (06-15+18=07-03) fall in July, NOT June. This affects all period 2026-06 calculations.
2. **6421/6422 not in account chart:** AccountChartSeeder defines "642" but NOT sub-accounts "6421"/"6422". IS service skips accounts not in chart → OpEx = 0 → NetProfit = Revenue - COGS only.
3. **BS NetIncome plug:** BS service uses residual approach (TotalAssets - TotalLiabilities - TotalEquity = NetIncome plug as equity line 421). Works even when sub-accounts not in chart.

## Verification ✅
- [x] All tests have numeric assertions (no `Assert.NotNull` only)
- [x] Tests use known seed values (documented expected values above)
- [x] Multi-tenant test pass (5/5)
- [x] Build pass (0 errors) + guard pass (ALL CHECKS PASSED)
- [x] W7 Core.Tests: 20/20 PASS
- [x] W7 Integration.Tests: 4/4 PASS

## Rollback
- Git revert (test files only — no production code changed)
