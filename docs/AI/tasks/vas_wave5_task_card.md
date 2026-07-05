# TASK CARD — VAS Wave 5: API Endpoints

> **Status:** ✅ COMPLETE | INVESTIGATE → PLAN → IMPLEMENT 100%
> **Prerequisite:** W4 merged (4 services available) ✅
> **Branch:** `feature/vas-wave5-api-controllers`
> **Estimated sessions:** 1 (actual)

## Objective
4 HTTP endpoints expose 4 report services.

## Prerequisites (verify before code)
- [x] W4 merged (BalanceSheetService, IncomeStatementService, CashFlowStatementService, TrialBalanceService)
- [x] Verify existing controllers pattern in `5_WebApps/ShopERP/Controllers/`
- [x] Verify DI registration pattern in `5_WebApps/ShopERP/Program.cs`
- [x] Check KhachLink checklist rules (governance.md) — N/A (KhachLink doesn't need BCTC)

## Files Created
| File | Endpoint |
|------|----------|
| `5_WebApps/ShopERP/Controllers/BalanceSheetsController.cs` | GET /api/balance-sheets |
| `5_WebApps/ShopERP/Controllers/IncomeStatementsController.cs` | GET /api/income-statements |
| `5_WebApps/ShopERP/Controllers/CashFlowStatementsController.cs` | GET /api/cash-flow-statements |
| `5_WebApps/ShopERP/Controllers/TrialBalancesController.cs` | GET /api/trial-balances |

## Files Modified
| File | Change |
|------|--------|
| `3_CoreHub/Infrastructure/Configurations/JournalEntryConfiguration.cs` | Added `IEntityConfiguration` marker (so ShopERPDbContext picks it up) |
| `5_WebApps/ShopERP/Infrastructure/ShopERPDbContext.cs` | Removed inline JournalEntry config (replaced by JournalEntryConfiguration from CoreHub assembly) |

## Files Created (Tests)
| File | Tests |
|------|-------|
| `6_Tests/VanAn.Integration.Tests/VasReportsEndpointTests.cs` | 5 endpoint tests |

## Design Decisions (Open Questions RESOLVED)
- **Q1 TenantId:** From JWT claim (`User.FindFirst("tenant_id")` — consistent with UserController/PermissionGroupController pattern). NOT from route. More secure (multi-tenancy at auth layer).
- **Q2 Period format:** `?year=2026&month=6` query params (consistent with HKDBooksController).
- **Q3 KhachLink:** No — KhachLink is customer-facing PWA, not accounting UI. W6 UI will be in ShopERP Blazor. No KhachLink @inject needed.
- **Standard:** Query param `?standard=TT133_2016` (default TT133_2016 — seed tenant is DN vừa; W8 will auto-detect from Tenant.AccountingStandard).

## Endpoint Pattern (all 4 controllers)
- `[ApiController] [Route("api/...")] [Authorize] [Produces("application/json")]`
- Inject service + ILogger<T> (primary constructor)
- `GetCurrentTenantId()` from JWT claim (UserController pattern)
- Returns Domain record directly (BalanceSheet, IncomeStatement, CashFlowStatement, TrialBalance)
- BS: 422 UnprocessableEntity on W2 invariant violation (unbalanced)
- DI already registered in W4 (Program.cs line 224-228)

## Bug Found & Fixed During IMPLEMENT
1. **ShopERPDbContext schema mismatch:** `JournalEntryConfiguration` didn't implement `IEntityConfiguration` marker → ShopERPDbContext's `ApplyConfigurationsFromAssembly` filter skipped it → inline config missing `Description`/`EntryDate`/`ReferenceId`/`IsReversal` mappings → `EnsureCreated()` schema had no `Description` column → seed failed with `SQLite Error 1: 'table JournalEntries has no column named Description'`. **Fix:** Added `IEntityConfiguration` to `JournalEntryConfiguration` + removed inline JournalEntry config from ShopERPDbContext (avoids duplicate OwnsMany conflict).
2. **JSON casing:** Tests asserted PascalCase property names but ASP.NET Core default JSON serialization uses camelCase. **Fix:** Changed assertions to camelCase (`totalAssetsEnding` not `TotalAssetsEnding`).

## Verification ✅
- [x] 4 endpoints return 200 with seed data
- [x] Response contains expected Domain record fields (camelCase JSON)
- [x] Build pass (0 errors) + guard pass (ALL CHECKS PASSED)
- [x] W5 tests: 5/5 PASS
- [x] Core.Tests: PASSED (guard gate)
- [x] Arch.Tests: PASSED (guard gate)
- [x] Integration tests: PASSED (guard gate)

## Rollback
- Git revert (4 controllers + 2 config fixes + 1 test file)
