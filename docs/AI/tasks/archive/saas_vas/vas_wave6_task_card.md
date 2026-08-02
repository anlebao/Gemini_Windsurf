# TASK CARD — VAS Wave 6: UI Pages

> **Status:** ✅ COMPLETE | INVESTIGATE → PLAN → IMPLEMENT (TDD) 100%
> **Prerequisite:** W5 merged (API endpoints available) ✅
> **Branch:** `feature/vas-wave6-ui-pages`
> **Estimated sessions:** 1 (actual)

## Objective
4 Blazor pages render reports using UI Platform components + 1 navigation hub.

## Prerequisites (verify before code)
- [x] W5 merged (4 API endpoints)
- [x] Verify UI Platform components: VanACard, VanAnDataGrid, VanAAlert, VanAButton
- [x] Verify existing Blazor pages pattern in `5_WebApps/ShopERP/Components/Pages/Accounting/`
- [x] Check VanATable, VanACard, VanAForm usage examples (HKDBooks.razor pattern)
- [x] Gate 4: UI layout change → bUnit tests (E2E deferred to W7 per governance "Playwright DISABLED during IMPLEMENT mode")

## Files Created
| File | Purpose |
|------|---------|
| `5_WebApps/ShopERP/Components/Pages/Accounting/BalanceSheet.razor` | BS page — 3 sections (Assets/Liabilities/Equity) + totals |
| `5_WebApps/ShopERP/Components/Pages/Accounting/IncomeStatement.razor` | IS page — key metrics + detail lines |
| `5_WebApps/ShopERP/Components/Pages/Accounting/CashFlowStatement.razor` | CF page — 3 activities + Opening/Closing/NetChange |
| `5_WebApps/ShopERP/Components/Pages/Accounting/TrialBalance.razor` | TB page — accounts table + IsBalanced indicator |
| `5_WebApps/ShopERP/Components/Pages/Accounting/FinancialReports.razor` | Navigation hub with links to 4 pages |

## Files Modified
| File | Change |
|------|--------|
| `5_WebApps/ShopERP/Components/Pages/Accounting/AccountingLayout.razor` | Added "Báo Cáo Tài Chính" menu entry |

## Test Files Created (TDD — tests written BEFORE implementation)
| File | Tests |
|------|-------|
| `6_Tests/VanAn.ShopERP.Tests/Components/VasReports/VasReportPageTestBase.cs` | Shared base class + sample data builders |
| `6_Tests/VanAn.ShopERP.Tests/Components/VasReports/BalanceSheetPageTests.cs` | 6 bUnit tests |
| `6_Tests/VanAn.ShopERP.Tests/Components/VasReports/IncomeStatementPageTests.cs` | 6 bUnit tests |
| `6_Tests/VanAn.ShopERP.Tests/Components/VasReports/CashFlowStatementPageTests.cs` | 6 bUnit tests |
| `6_Tests/VanAn.ShopERP.Tests/Components/VasReports/TrialBalancePageTests.cs` | 6 bUnit tests |
| `6_Tests/VanAn.ShopERP.Tests/Components/VasReports/FinancialReportsHubPageTests.cs` | 5 bUnit tests |

## Design Decisions (Open Questions RESOLVED)
- **Q1 Period picker:** Year + month dropdowns (consistent with HKDBookDetail pattern) + standard dropdown (TT133_2016 default, TT99_2025 option)
- **Q2 Export button:** Deferred (W6 focus = rendering; export in later wave)
- **Q3 Multi-tenant UI:** Auto from login (ITenantProvider, consistent with existing pages)

## TDD Approach
1. **Tests FIRST:** 29 bUnit tests written before any page implementation
2. **Then implement:** 5 pages + nav menu update
3. **Verify:** 29/29 tests pass, build 0 errors, guard ALL CHECKS PASSED

## Page Pattern (all 4 report pages)
- `@page "/accounting/{report-name}"` + `@rendermode InteractiveServer` + `@layout AccountingLayout`
- `@attribute [Authorize(Policy = "OwnerOnly")]`
- Inject W4 service + IThemeProvider + ITenantProvider + ILogger<T>
- Period picker: year (number input) + month (dropdown 1-12) + standard (dropdown)
- VanACard for sections + VanAnDataGrid for data + VanAAlert for errors
- 2-column comparative: "Số cuối kỳ" (Ending) + "Số đầu năm" (Opening)
- FinancialStatementLine.Level → padding-left indent for hierarchy
- IsNormalNegative → parentheses format for negative values (VN accounting convention)
- Fully qualified domain type in @code (avoids page class name collision with domain record)

## Bugs Found & Fixed During IMPLEMENT (3 rounds, within 3-round limit)
1. **Name collision:** Page class `BalanceSheet` shadows domain record `BalanceSheet` → CS1061 + CS0029 errors. **Fix:** Use `VanAn.Shared.Domain.BalanceSheet?` in @code block (all 4 pages).
2. **FinancialReports lambda quotes:** `OnClick="() => NavigateTo("/path")"` — nested double quotes break Razor parser (CS1025). **Fix:** Use named methods instead of inline lambdas.
3. **TrialBalance VanAAlert complex attribute:** Ternary expression in `Message` attribute breaks Razor (RZ9986). **Fix:** Extract to computed properties (`BalanceAlertType`, `BalanceAlertMessage`).
4. **FluentAssertions `.Or()` doesn't exist:** `Contain("a").Or.Contain("b")` fails. **Fix:** Use `Assert.True(markup.Contains("a") || markup.Contains("b"))`.
5. **VanAnDataGrid bUnit rendering:** Columns register in `OnInitialized` (after table renders) — data not visible on first bUnit render. **Fix:** `RenderWithReRender()` helper calls `cut.Render()` to force second render pass.
6. **FinancialReports VanAButton URLs:** VanAButton with OnClick doesn't produce href in markup. **Fix:** Use `<a>` tags instead of VanAButton for navigation links.
7. **CF-3 case sensitivity:** Test checked "Hoạt động kinh doanh" (uppercase H) but VanACard header has "hoạt động kinh doanh" (lowercase h). **Fix:** Match exact casing.

## Verification ✅
- [x] 4 pages render with seed data (no empty tables)
- [x] UI Platform components used (VanACard, VanAnDataGrid, VanAAlert, VanAButton)
- [x] bUnit tests pass (29/29)
- [x] Build pass (0 errors) + guard pass (ALL CHECKS PASSED)
- [x] Core.Tests PASSED (guard gate)
- [x] Arch.Tests PASSED (guard gate)
- [x] Integration tests PASSED (guard gate)
- [x] 2-column comparative (Ending + Opening) rendered
- [x] FinancialStatementLine.Level → hierarchy indent
- [x] IsNormalNegative → parentheses format
- [x] TB IsBalanced indicator (VanAAlert success/warning)
- [x] Navigation hub with links to 4 pages
- [x] AccountingLayout nav menu updated

## Rollback
- Git revert (5 pages + nav menu + 6 test files)
