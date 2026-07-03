# TASK CARD — VAS Wave 5: API Endpoints

> **Status:** NOT STARTED | INVESTIGATE → PLAN → IMPLEMENT
> **Prerequisite:** W4 merged (4 services available)
> **Branch:** `feature/vas-wave5-api-endpoints`
> **Estimated sessions:** 1

## Objective
4 HTTP endpoints expose 4 report services.

## Prerequisites (verify before code)
- [ ] W4 merged (BalanceSheetService, IncomeStatementService, CashFlowStatementService, TrialBalanceService)
- [ ] Verify existing controllers pattern in `5_WebApps/ShopERP/Controllers/`
- [ ] Verify DI registration pattern in `5_WebApps/ShopERP/Program.cs`
- [ ] Check KhachLink checklist rules (governance.md)

## Files to Create/Modify
| File | Action |
|------|--------|
| `5_WebApps/ShopERP/Controllers/BalanceSheetsController.cs` | CREATE |
| `5_WebApps/ShopERP/Controllers/IncomeStatementsController.cs` | CREATE |
| `5_WebApps/ShopERP/Controllers/CashFlowStatementsController.cs` | CREATE |
| `5_WebApps/ShopERP/Controllers/TrialBalancesController.cs` | CREATE |
| `5_WebApps/ShopERP/Program.cs` | ADD DI registrations |
| `6_Tests/VanAn.Integration.Tests/KhachLinkStartupTests.cs` | ADD assertions (if KhachLink needs) |

## Detailed Task List

### W5-T1: BalanceSheetsController
- GET `/api/balance-sheets/{tenantId}?period=YYYY-MM`
- Returns BalanceSheet record

### W5-T2: IncomeStatementsController
- GET `/api/income-statements/{tenantId}?period=YYYY-MM`

### W5-T3: CashFlowStatementsController
- GET `/api/cash-flow-statements/{tenantId}?period=YYYY-MM`

### W5-T4: TrialBalancesController
- GET `/api/trial-balances/{tenantId}?period=YYYY-MM`

### W5-T5: DI registration
- Register 4 services + 4 controllers in Program.cs

### W5-T6: KhachLink checklist (if applicable)
- Register Http services if KhachLink needs
- Add StartupTests assertions

### W5-T7: Build + guard pass

## Verification
- [ ] 4 endpoints return 200 with seed data
- [ ] Endpoints return 404 for non-existent tenant
- [ ] Build pass + guard pass

## Rollback
- Git revert (controllers + DI only)

## Open Questions
- Q1: TenantId từ route hay từ JWT token?
- Q2: Period format — query string `?period=YYYY-MM` hay `?year=X&month=Y`?
- Q3: KhachLink cần truy cập 4 BCTC không?
