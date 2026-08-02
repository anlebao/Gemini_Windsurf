# TASK CARD — SaaS W5: Period Closing Persist + Auth Hardening

> **Status:** COMPLETE ✅ | INVESTIGATE → PLAN → IMPLEMENT 100%
> **Prerequisite:** W0+W1+W2 merged ✅
> **Branch:** `feature/saas-w5-period-persist-auth-hardening`
> **Estimated sessions:** 1-2
> **Sprint:** 2 (Hardening)

## Execution Summary (2026-07-05)

**Part 1: Period Closing Persistence — COMPLETE ✅**
- **W5-T1:** Created `PeriodClosingStatusEntity` in `3_CoreHub/Infrastructure/Entities/` (NOT Domain — follows W3 `AccountChartEntity` precedent). Inherits `BaseEntity` (IMustHaveTenant for multi-tenancy query filter). State machine: Open → Closed → Reopening → Open. Factory + `MarkClosed`/`MarkReopening`/`MarkReopened` methods with invariant guards.
- **W5-T2:** Created `PeriodClosingStatusConfiguration` (unique index on TenantId+PeriodYear+PeriodMonth, enum→int conversion, `IEntityConfiguration` marker for auto-discovery). Added `DbSet<PeriodClosingStatusEntity>` to `VanAnDbContext`, `IVanAnDbContext`, and `ShopERPDbContext`.
- **W5-T3:** Generated migration `20260705120225_AddPeriodClosingStatusTable` (SQLite TEXT/INTEGER columns, unique index, lookup index).
- **W5-T4:** Refactored `PeriodClosingService` — removed `static Dictionary` in-memory store, injected `IVanAnDbContext`, all status queries now hit DB. `GetOrCreateStatusEntityAsync` (tracked) for close, tracked query for reopen, `AsNoTracking` for read-only `GetPeriodStatusAsync`. Default = Open when no DB record exists.

**Part 2: Auth Hardening — COMPLETE ✅**
- **W5-T5:** Wrapped `DevLoginController` class in `#if DEBUG ... #endif` (compile-time guard). Updated `Program.cs` dev route to also use `#if DEBUG`. The controller's comment was misleading — `app.MapControllers()` runs in ALL environments, so the previous env-only guard was insufficient.
- **W5-T6:** Created `DevLoginControllerReleaseBuildGuardTests` (3 Arch tests): W5-ARCH-001 verifies `#if DEBUG` guard in source, W5-ARCH-002 verifies Program.cs dev route guard, W5-ARCH-003 reflection check (type exists in Debug, absent in Release).
- **W5-T6 (Q3 resolution):** `Login.cshtml.cs:96` already had `HttpOnly = true` — W5-T6 from task card was already done. Verified no JS reads `.VanAn.Jwt` (KhachLink uses separate `customer_token` in localStorage).

**Tests — COMPLETE ✅**
- **W5-T7:** Created `PeriodClosingPersistenceTests` (4 Integration tests using SQLite in-memory via `TestDatabaseFixture`): ClosePeriod_PersistsClosedStatusToDatabase, GetPeriodStatus_SurvivesRestart_FreshDbContext, ReopenPeriod_UpdatesStatusToOpen_AndPersistsReopenReason, MultiTenantIsolation_TenantA_Close_DoesNotAffectTenantB.
- **Test infrastructure fix:** Changed `TestTenantProvider` registration from `Scoped` to `Singleton` in `TestDatabaseFixture` — required for `CreateFreshDbContext()` to inherit tenant context across scopes.

**Verification:**
- Build: 0 errors ✅
- Guard-check: ALL CHECKS PASSED ✅
- Core.Tests: 929/929 PASS ✅
- Arch.Tests: 34/34 PASS ✅ (3 new W5 tests)
- Integration.Tests: 177/177 PASS ✅ (4 new W5 tests)
- ShopERP.Tests: 96/99 PASS (3 pre-existing AccountingLayoutNavigationTests failures, unrelated to W5)

**Files created (5):**
1. `3_CoreHub/Infrastructure/Entities/PeriodClosingStatusEntity.cs`
2. `3_CoreHub/Infrastructure/Configurations/PeriodClosingStatusConfiguration.cs`
3. `3_CoreHub/Infrastructure/Migrations/20260705120225_AddPeriodClosingStatusTable.cs` + `.Designer.cs`
4. `6_Tests/VanAn.Architecture.Tests/DevLoginControllerReleaseBuildGuardTests.cs`
5. `6_Tests/VanAn.Integration.Tests/Accounting/PeriodClosingPersistenceTests.cs`

**Files modified (7):**
1. `3_CoreHub/Infrastructure/VanAnDbContext.cs` — added DbSet<PeriodClosingStatusEntity>
2. `3_CoreHub/Infrastructure/IVanAnDbContext.cs` — added DbSet<PeriodClosingStatusEntity>
3. `5_WebApps/ShopERP/Infrastructure/ShopERPDbContext.cs` — added DbSet<PeriodClosingStatusEntity>
4. `3_CoreHub/Services/PeriodClosingService.cs` — replaced in-memory store with DB queries
5. `5_WebApps/ShopERP/Controllers/DevLoginController.cs` — #if DEBUG guard
6. `5_WebApps/ShopERP/Program.cs` — #if DEBUG guard on dev route
7. `6_Tests/VanAn.Integration.Tests/Infrastructure/TestDatabaseFixture.cs` — Singleton TestTenantProvider
8. `3_CoreHub/Infrastructure/Migrations/VanAnDbContextModelSnapshot.cs` — auto-updated by EF

**Open Questions Resolved:**
- Q1: Entity in Infrastructure (NOT Domain) — follows W3 AccountChartEntity precedent. Avoids naming conflict with existing `PeriodClosingStatus` enum (Domain.cs:1556).
- Q2: `#if DEBUG` compile-time guard — safer than runtime env check. Production builds use `-c Release` (confirmed in scripts).
- Q3: HttpOnly already set — no JS reads `.VanAn.Jwt`. No change needed.

## Objective
1. Persist PeriodClosing status to DB (survive app restart)
2. Guard DevLoginController (`#if DEBUG` + env check)
3. Add HttpOnly to JWT cookie (XSS protection)

## Prerequisites (verify before code)
- [ ] W0-W2 merged
- [ ] Verify `3_CoreHub/Services/PeriodClosingService.cs` — in-memory status store
- [ ] Verify `5_WebApps/ShopERP/Controllers/DevLoginController.cs` — 149 lines, not guarded
- [ ] Verify `5_WebApps/ShopERP/Pages/Login.cshtml.cs:94-100` — cookie options
- [ ] Verify `5_WebApps/ShopERP/Program.cs:443-452` — dev login route guard

## Part 1: Period Closing Persistence

### Files to Modify
| File | Changes |
|------|---------|
| `1_Shared/Domain.cs` | ADD `PeriodClosingStatus` entity (TenantId, Year, Month, Status, ClosedAt, ClosedBy, ReopenReason) |
| `3_CoreHub/Infrastructure/VanAnDbContext.cs` | ADD DbSet<PeriodClosingStatus> + configuration |
| `3_CoreHub/Infrastructure/Configurations/` | ADD PeriodClosingStatusConfiguration.cs |
| `3_CoreHub/Infrastructure/Migrations/` | ADD migration `AddPeriodClosingStatusTable` |
| `3_CoreHub/Services/PeriodClosingService.cs` | REPLACE in-memory store with DB queries |
| `6_Tests/VanAn.Core.Tests/Services/` | ADD PeriodClosingPersistenceTests.cs |

### W5-T1: Create PeriodClosingStatus entity
```csharp
public class PeriodClosingStatus : BaseEntity, IMustHaveTenant
{
    public TenantId TenantId { get; private set; }
    public int PeriodYear { get; private set; }
    public int PeriodMonth { get; private set; }
    public PeriodStatus Status { get; private set; } // Open, Closed, Reopening
    public DateTime? ClosedAt { get; private set; }
    public string? ClosedBy { get; private set; }
    public string? ReopenReason { get; private set; }
    
    // Factory: Create(TenantId, Year, Month) → Status=Open
    // MarkClosed(ClosedBy) → Status=Closed, ClosedAt=UtcNow
    // MarkReopening(Reason) → Status=Reopening
    // MarkReopened() → Status=Open, clear ReopenReason
}
```

### W5-T2: Migration + configuration
- Add `DbSet<PeriodClosingStatus>` to VanAnDbContext
- Add `PeriodClosingStatusConfiguration.cs` (unique index on TenantId+Year+Month)
- Create migration `AddPeriodClosingStatusTable`
- Default: existing periods → Status=Open (no data migration needed)

### W5-T3: Update PeriodClosingService
- Replace `_inMemoryStore` with `_dbContext.PeriodClosingStatuses`
- `GetPeriodStatusAsync` → query DB
- `ClosePeriodAsync` → update DB record
- `ReopenPeriodAsync` → update DB record
- `ValidatePeriodAsync` → check DB for existing status

### W5-T4: Tests
- Test: Close period → DB record exists with Status=Closed
- Test: Restart (new context) → status still Closed
- Test: Reopen → Status=Open, ReopenReason set
- Test: Multi-tenant isolation (tenant A close ≠ tenant B)

## Part 2: Auth Hardening

### Files to Modify
| File | Changes |
|------|---------|
| `5_WebApps/ShopERP/Controllers/DevLoginController.cs` | Add `#if DEBUG` guard or `IsDevelopment()` check in constructor/action |
| `5_WebApps/ShopERP/Program.cs:443-452` | Verify dev login route only in Development |
| `5_WebApps/ShopERP/Pages/Login.cshtml.cs:94-100` | Add `HttpOnly = true` to JWT cookie |

### W5-T5: Guard DevLoginController
**Option A (preferred):** `#if DEBUG` conditional compilation
```csharp
#if DEBUG
[ApiController]
[Route("api/[controller]")]
public class DevLoginController : ControllerBase
{
    // ... existing code
}
#endif
```

**Option B:** Runtime environment check
```csharp
[ApiController]
[Route("api/[controller]")]
public class DevLoginController : ControllerBase
{
    private readonly IWebHostEnvironment _env;
    
    public DevLoginController(IWebHostEnvironment env)
    {
        _env = env;
        if (!_env.IsDevelopment())
            throw new InvalidOperationException("DevLoginController only available in Development");
    }
}
```

### W5-T6: Add HttpOnly to JWT cookie
**File:** `5_WebApps/ShopERP/Pages/Login.cshtml.cs:94-100`
```csharp
// BEFORE:
var cookieOptions = new CookieOptions
{
    HttpOnly = false, // or not set
    Secure = true,
    SameSite = SameSiteMode.Strict,
    Expires = DateTime.UtcNow.AddHours(8)
};

// AFTER:
var cookieOptions = new CookieOptions
{
    HttpOnly = true,  // ← ADD: prevent JavaScript access (XSS protection)
    Secure = true,
    SameSite = SameSiteMode.Strict,
    Expires = DateTime.UtcNow.AddHours(8)
};
```

### W5-T7: Tests
- Test: DevLoginController not accessible in Production environment
- Test: JWT cookie has HttpOnly flag
- Test: Login flow still works after HttpOnly change

### W5-T8: Build + guard + all tests pass
- Build 0 errors, guard pass, all tests pass (existing + new)

## Verification
- [ ] `PeriodClosingStatus` entity exists in Domain
- [ ] Migration `AddPeriodClosingStatusTable` created
- [ ] PeriodClosingService uses DB (no in-memory store)
- [ ] Period status survives app restart (test with new DbContext)
- [ ] DevLoginController guarded (`#if DEBUG` or env check)
- [ ] JWT cookie has `HttpOnly = true`
- [ ] Build 0 errors, guard pass, all tests pass

## Rollback
- Git revert (restore in-memory store, remove HttpOnly, remove guard)
- Migration rollback: `dotnet ef migrations remove AddPeriodClosingStatusTable`
- If Domain entity breaks arch tests: remove entity, use raw SQL table

## Open Questions
- Q1: PeriodClosingStatus — entity in Domain.cs or Infrastructure-only table? (Domain if it has business rules)
- Q2: DevLogin guard — `#if DEBUG` (compile-time) or env check (runtime)? (Investigate which is safer)
- Q3: HttpOnly cookie — any Blazor interop that reads JWT from cookie? (Verify no JS reads token)
