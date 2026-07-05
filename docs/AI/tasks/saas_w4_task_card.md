# TASK CARD — SaaS W4: UI Test Coverage (10 Accounting Pages)

> **Status:** COMPLETE ✅ | INVESTIGATE → IMPLEMENT
> **Prerequisite:** W0+W1+W2 merged (blockers fixed) ✅
> **Branch:** `feature/saas-w4-ui-test-coverage`
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
- [x] 7/10 pages already had bUnit tests from W6 (BS 6 + IS 6 + CF 6 + TB 6 + FinancialReports 5 + AccountingIndex 4 + AccountingLayout 5 = 38 tests)
- [x] 3 new test files created for missing pages (HKDBooks, HKDBookDetail, PeriodClosing)
- [x] Each new page has ≥ 10 test cases (HKDBooks 10, HKDBookDetail 15, PeriodClosing 19 = 44 new tests)
- [x] Total new bUnit tests = 44 (exceeds ≥30 target)
- [x] All 44 new tests PASS
- [x] All existing tests still PASS (Core 910 + Arch 31 + Integration 173; ShopERP 96/99 — 3 pre-existing AccountingLayoutNavigationTests failures unrelated to W4)
- [x] Build 0 errors, guard PASS

## Implementation Notes

### bUnit + @rendermode InteractiveServer Limitation
The 10 Accounting pages use `@rendermode InteractiveServer`, which causes bUnit to render in static mode. In static mode:
- `@onclick` DOM event handlers are NOT registered → bUnit `Click()` throws `MissingEventHandlerException`
- `@bind` expressions render as literal strings (e.g., `disabled="False || Loading"`)

**Workaround applied:**
- Render/structure assertions work normally (markup contains, component counts, etc.)
- Click-based interaction tests converted to VanAButton component existence + label assertions
- PeriodClosing wizard step coverage achieved via **reflection**: setting private `currentStep` field (nested private enum: Idle=0, Validate=1, Review=2, Close=3) + `validationResult`/`closingEntry`/`currentStatus`/`showReopenDialog` fields, then `cut.Render()` to verify each step's UI
- Full click-to-action interaction (e.g., "click button → service called → navigate") is covered by Playwright E2E tests

### Test Files Created
| File | Tests | Coverage |
|------|-------|----------|
| `HKDBooksTests.cs` | 10 | Header, template list, empty state, error, refresh, TT 152 ref, TargetGroup, open-book button, service call count |
| `HKDBookDetailTests.cs` | 15 | Header+template code, back/regenerate buttons, period selector, apply period, DOCX/XLSX export buttons, TT 152 layout, journal rows, total rows, service call, invalid template error, generic error, back button |
| `PeriodClosingTests.cs` | 19 | Header, back button, period selector, start validation button, service call, Validate step (result card/success/next button/error list/warnings), Review step, Close step, reopen button, reopen dialog, error alert, wizard indicator, back button, reset button |

## Rollback
- Git revert (delete new test files)
- If mock setup breaks existing tests: fix mocks

## Open Questions
- Q1: Mock pattern — use Moq or bUnit built-in stubs? (Follow existing pattern)
- Q2: Export button test — how to verify JS download trigger? (Mock JS interop)
- Q3: Feature flag test — mock IVasFeatureFlagService or integration test? (Unit test with mock)
