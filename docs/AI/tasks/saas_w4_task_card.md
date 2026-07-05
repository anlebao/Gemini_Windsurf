# TASK CARD — SaaS W4: UI Test Coverage (10 Accounting Pages)

> **Status:** NOT STARTED | INVESTIGATE → PLAN → IMPLEMENT
> **Prerequisite:** W0+W1+W2 merged (blockers fixed)
> **Branch:** `feature/saas-w4-ui-test-coverage`
> **Estimated sessions:** 2-3 (10 trang = effort lớn)
> **Sprint:** 2 (Hardening)

## Objective
Viết bUnit tests cho 10 trang Accounting còn thiếu. Đảm bảo UI render đúng, interaction hoạt động, error handling cover.

## Prerequisites (verify before code)
- [ ] W0-W2 merged
- [ ] Verify existing bUnit tests: `6_Tests/VanAn.ShopERP.Tests/` — 5 pages đã có tests
- [ ] Verify 10 pages thiếu tests (list below)
- [ ] Verify bUnit test pattern trong existing tests (follow same pattern)

## Pages Needing Tests (10)

| # | Page | Priority | Test Focus |
|---|------|----------|------------|
| 1 | `BalanceSheet.razor` | HIGH | Render report data, period selector, export button, error state |
| 2 | `IncomeStatement.razor` | HIGH | Render 2-column (Ending/Opening), period selector |
| 3 | `CashFlowStatement.razor` | HIGH | Render cash flow lines, opening/closing calculation |
| 4 | `TrialBalance.razor` | HIGH | Render debit/credit, balanced indicator, period selector |
| 5 | `HKDBooks.razor` | HIGH | Template list, generate button, navigation to detail |
| 6 | `HKDBookDetail.razor` | HIGH | Period selector, book generation, export DOCX/XLSX |
| 7 | `PeriodClosing.razor` | MEDIUM | Wizard steps (Validate→Review→Close), reopen dialog |
| 8 | `FinancialReports.razor` | MEDIUM | Hub page, navigation to 4 BCTC |
| 9 | `AccountingLayout.razor` | MEDIUM | Dynamic menu (HKD vs Enterprise), feature flag display |
| 10 | `AccountingIndex.razor` | LOW | Dashboard cards, navigation links |

## Files to Create
| File | Page Tested |
|------|-------------|
| `6_Tests/VanAn.ShopERP.Tests/Pages/BalanceSheetTests.cs` | BalanceSheet.razor |
| `6_Tests/VanAn.ShopERP.Tests/Pages/IncomeStatementTests.cs` | IncomeStatement.razor |
| `6_Tests/VanAn.ShopERP.Tests/Pages/CashFlowStatementTests.cs` | CashFlowStatement.razor |
| `6_Tests/VanAn.ShopERP.Tests/Pages/TrialBalanceTests.cs` | TrialBalance.razor |
| `6_Tests/VanAn.ShopERP.Tests/Pages/HKDBooksTests.cs` | HKDBooks.razor |
| `6_Tests/VanAn.ShopERP.Tests/Pages/HKDBookDetailTests.cs` | HKDBookDetail.razor |
| `6_Tests/VanAn.ShopERP.Tests/Pages/PeriodClosingTests.cs` | PeriodClosing.razor |
| `6_Tests/VanAn.ShopERP.Tests/Pages/FinancialReportsTests.cs` | FinancialReports.razor |
| `6_Tests/VanAn.ShopERP.Tests/Pages/AccountingLayoutTests.cs` | AccountingLayout.razor |
| `6_Tests/VanAn.ShopERP.Tests/Pages/AccountingIndexTests.cs` | AccountingIndex.razor |

## Detailed Task List

### W4-T1: INVESTIGATE existing test pattern
- Read `6_Tests/VanAn.ShopERP.Tests/Pages/RevenueEntryTests.cs` (existing pattern)
- Document pattern: test setup, mock services, render, assert markup
- Verify bUnit package version + test fixtures

### W4-T2: 4 BCTC pages (HIGH priority)
For each page (BS, IS, CF, TB):
- Mock service (`IBalanceSheetService` etc.) with sample data
- Test 1: Render with data — verify report lines rendered
- Test 2: Period selector — verify year/month dropdown works
- Test 3: Error state — verify error alert shown on service exception
- Test 4: Empty state — verify "no data" message when service returns empty
- Test 5: Feature flag — verify 403/forbidden message for HKD tenant (if applicable)

### W4-T3: HKD pages (HIGH priority)
For `HKDBooks.razor`:
- Test 1: Template list rendered
- Test 2: Generate button navigates to detail
- Test 3: Empty tenant state

For `HKDBookDetail.razor`:
- Test 1: Period selector works
- Test 2: Book generation calls service
- Test 3: Export buttons (DOCX/XLSX) trigger download
- Test 4: Loading state during generation

### W4-T4: PeriodClosing + Hub + Layout (MEDIUM priority)
For `PeriodClosing.razor`:
- Test 1: Wizard step 1 (Validate) renders
- Test 2: Step navigation (Validate → Review → Close)
- Test 3: Reopen dialog with reason input

For `FinancialReports.razor` + `AccountingLayout.razor` + `AccountingIndex.razor`:
- Test navigation links render
- Test dynamic menu (HKD vs Enterprise) for AccountingLayout
- Test dashboard cards for AccountingIndex

### W4-T5: Build + guard + all tests pass
- Build 0 errors, guard pass
- New tests PASS (target: 30-50 new bUnit tests)
- All existing tests still PASS

## Verification
- [ ] 10 new test files created
- [ ] Each page has ≥ 3 test cases
- [ ] Total new bUnit tests ≥ 30
- [ ] All new tests PASS
- [ ] All existing 1114+ tests still PASS
- [ ] Build 0 errors, guard pass

## Rollback
- Git revert (delete new test files)
- If mock setup breaks existing tests: fix mocks

## Open Questions
- Q1: Mock pattern — use Moq or bUnit built-in stubs? (Follow existing pattern)
- Q2: Export button test — how to verify JS download trigger? (Mock JS interop)
- Q3: Feature flag test — mock IVasFeatureFlagService or integration test? (Unit test with mock)
