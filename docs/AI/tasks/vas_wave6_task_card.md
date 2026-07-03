# TASK CARD — VAS Wave 6: UI Pages

> **Status:** NOT STARTED | INVESTIGATE → PLAN → IMPLEMENT
> **Prerequisite:** W5 merged (API endpoints available)
> **Branch:** `feature/vas-wave6-ui-pages`
> **Estimated sessions:** 1-2

## Objective
4 Blazor pages render reports using UI Platform components.

## Prerequisites (verify before code)
- [ ] W5 merged (4 API endpoints)
- [ ] Verify UI Platform components: `docs/UI_Platform_Implementation_Guide.md`
- [ ] Verify existing Blazor pages pattern in `5_WebApps/ShopERP/Components/Pages/`
- [ ] Check VanATable, VanACard, VanAForm usage examples
- [ ] Gate 4: UI layout change → BẮT BUỘC E2E test

## Files to Create
| File | Purpose |
|------|---------|
| `5_WebApps/ShopERP/Components/Pages/Accounting/BalanceSheet.razor` | BS page |
| `5_WebApps/ShopERP/Components/Pages/Accounting/IncomeStatement.razor` | IS page |
| `5_WebApps/ShopERP/Components/Pages/Accounting/CashFlowStatement.razor` | CF page |
| `5_WebApps/ShopERP/Components/Pages/Accounting/TrialBalance.razor` | TB page |
| `5_WebApps/ShopERP/Components/Pages/Accounting/FinancialReports.razor` | Navigation hub |
| `6_Testing/e2e-tests/` | E2E tests (Gate 4) |

## Detailed Task List

### W6-T1: BalanceSheet.razor
- VanATable cho Assets/Liabilities/Equity
- VanACard cho totals (TotalAssets, TotalLiabilities, TotalEquity)
- IsBalanced indicator (VanAnAlert green/red)

### W6-T2: IncomeStatement.razor
- VanATable cho Revenue/COGS/GrossProfit/OpEx/NetProfit
- VanACard cho key metrics

### W6-T3: CashFlowStatement.razor
- VanATable cho 3 dòng (Operating/Investing/Financing)
- VanACard cho Opening/Closing/NetChange

### W6-T4: TrialBalance.razor
- VanATable cho accounts (AccountCode, AccountName, Debit, Credit, Balance)
- Totals row + IsBalanced indicator

### W6-T5: Navigation menu
- Add "Accounting > Financial Reports" menu entry
- Link to FinancialReports.razor hub page

### W6-T6: E2E tests (Gate 4 mandatory)
- Test each page renders with seed data
- Test navigation works
- Test period filter UI

### W6-T7: Build + guard pass

## Verification
- [ ] 4 pages render with seed data (no empty tables)
- [ ] UI Platform components used (no custom HTML/CSS)
- [ ] E2E tests pass
- [ ] Build pass + guard pass
- [ ] Mobile responsive (≤640px, 641-1024px, ≥1025px)

## Rollback
- Git revert (pages + nav only)

## Open Questions
- Q1: Period picker — dropdown hay date input?
- Q2: Export button (PDF/Excel) — add in W6 hay defer?
- Q3: Multi-tenant UI — tenant selector hay auto from login?
