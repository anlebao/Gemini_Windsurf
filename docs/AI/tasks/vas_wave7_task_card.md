# TASK CARD — VAS Wave 7: Tests with Numeric Assertions

> **Status:** NOT STARTED | INVESTIGATE → PLAN → IMPLEMENT
> **Prerequisite:** W6 merged (UI + API + services available)
> **Branch:** `feature/vas-wave7-numeric-tests`
> **Estimated sessions:** 1

## Objective
Unit + integration tests với số liệu cụ thể, không white-test.

## Prerequisites (verify before code)
- [ ] W4-W6 merged (services + API + UI)
- [ ] W1 seed data available (known values for assertions)
- [ ] Verify existing test pattern in `6_Tests/VanAn.Core.Tests/`
- [ ] Verify integration test pattern in `6_Tests/VanAn.Integration.Tests/`

## Files to Create
| File | Tests |
|------|-------|
| `6_Tests/VanAn.Core.Tests/Services/BalanceSheetServiceTests.cs` | BS numeric |
| `6_Tests/VanAn.Core.Tests/Services/IncomeStatementServiceTests.cs` | IS numeric |
| `6_Tests/VanAn.Core.Tests/Services/CashFlowStatementServiceTests.cs` | CF numeric |
| `6_Tests/VanAn.Core.Tests/Services/TrialBalanceServiceTests.cs` | TB numeric |
| `6_Tests/VanAn.Integration.Tests/VasReportsEndpointTests.cs` | API integration |
| `6_Tests/VanAn.Core.Tests/Services/VasMultiTenantTests.cs` | Multi-tenant isolation |

## Detailed Task List

### W7-T1: BalanceSheetServiceTests
- Seed known data → assert TotalAssets == X (specific value)
- Assert IsBalanced == true
- Assert account count == N

### W7-T2: IncomeStatementServiceTests
- Assert Revenue == X (specific value from seed)
- Assert COGS == Y
- Assert NetProfit == Z
- Assert NetProfit == Revenue - COGS - OpEx

### W7-T3: CashFlowStatementServiceTests
- Assert OpeningCash == X
- Assert ClosingCash == Y
- Assert NetChange == ClosingCash - OpeningCash
- Assert 3 dòng sum to NetChange

### W7-T4: TrialBalanceServiceTests
- Assert TotalDebit == TotalCredit
- Assert account count == N
- Assert IsBalanced == true

### W7-T5: Multi-tenant isolation test
- Tenant A data không leak tenant B
- Query tenant A → only tenant A entries

### W7-T6: Integration test (API endpoint)
- Full stack: HTTP request → controller → service → DB
- Assert response body có correct numeric values

### W7-T7: Build + guard + all tests pass

## Verification
- [ ] All tests have numeric assertions (no `Assert.NotNull` only)
- [ ] Tests use known seed values (document expected values)
- [ ] Multi-tenant test pass
- [ ] Build pass + guard pass + all tests pass

## Rollback
- Git revert (test files only)

## Open Questions
- Q1: Test data — use W1 seed hay tạo test-specific seed?
- Q2: Expected values — hardcode hay calculate from known formulas?
