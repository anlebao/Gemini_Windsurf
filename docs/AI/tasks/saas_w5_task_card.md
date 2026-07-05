# TASK CARD — SaaS W5: Period Closing Persist + Auth Hardening

> **Status:** NOT STARTED | INVESTIGATE → PLAN → IMPLEMENT
> **Prerequisite:** W0+W1+W2 merged
> **Branch:** `feature/saas-w5-period-persist-auth-hardening`
> **Estimated sessions:** 1-2
> **Sprint:** 2 (Hardening)

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
