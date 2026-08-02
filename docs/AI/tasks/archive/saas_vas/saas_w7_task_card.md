# TASK CARD — SaaS W7: Tech Debt Cleanup (Tier 1+2)

> **Status:** NOT STARTED | INVESTIGATE → PLAN → IMPLEMENT
> **Prerequisite:** W0-W6 merged
> **Branch:** `feature/saas-w7-tech-debt-cleanup`
> **Estimated sessions:** 1-2
> **Sprint:** 3 (Cleanup)

## Objective
Resolve Tier 1 + Tier 2 tech debt items. Clean up obsolete code. Fix Docker hardening issues.

## Prerequisites (verify before code)
- [ ] W0-W6 merged
- [ ] Verify `5_WebApps/ShopERP/TECHNICAL_DEBT_LEDGER.md` — full debt list
- [ ] Verify each item below exists in codebase

## Tech Debt Items

### Tier 1: Tenant Isolation (HIGH — must fix before production)
| # | File | Issue | Fix |
|---|------|-------|-----|
| M1 | `TransactionHistory.razor:187-194` | Fallback tenant hardcode | Add TenantId claim to Login.cshtml.cs, remove fallback |
| M2 | `ExpenseEntry.razor:211-219` | Fallback tenant hardcode | Same fix as M1 |

### Tier 2: Component Binding (MEDIUM)
| # | File | Issue | Fix |
|---|------|-------|-----|
| M3 | `ExpenseEntry.razor:222-244` | JS interop workaround for @bind | Fix Blazor binding, remove JS workaround |
| M4 | `App.razor:18-27` | Global JS helper `vananReadElementValue` | Remove if M3 fixed |

### Tier 3: Code Cleanup (LOW)
| # | File | Issue | Fix |
|---|------|-------|-----|
| M5 | `HKDBookService.cs:356` | Obsolete `GenerateTrialBalanceAsync` (0 callers) | Remove method |
| M6 | `HKDBookService.cs:709` | Obsolete `ConvertToJournalEntries` (0 callers) | Remove method |
| M7 | `WebhookService.cs:40` | Obsolete constructor overload | Remove overload |

### Docker Hardening
| # | File | Issue | Fix |
|---|------|-------|-----|
| M8 | `docker-compose.yml` | No resource limits | Add CPU/memory limits per container |
| M9 | `docker-compose.yml` | No SQLite volume mount | Add volume for `SQLITE_DB_PATH` |
| M10 | `ShopERP/Dockerfile:33-34` | Tests disabled | Enable tests or add separate test stage |
| M11 | `docker-compose.yml` | No security headers | Add security headers middleware in Program.cs |

## Detailed Task List

### W7-T1: Fix Tier 1 — Tenant fallback hardcode
**File:** `5_WebApps/ShopERP/Pages/Login.cshtml.cs`
- Add TenantId claim to JWT token on successful login
- Extract TenantId from JWT in Blazor components (via AuthenticationStateProvider)

**File:** `5_WebApps/ShopERP/Components/Pages/Accounting/TransactionHistory.razor:187-194`
```csharp
// BEFORE:
var tenantId = TenantProvider.TenantId;
if (tenantId == Guid.Empty)
    tenantId = Guid.Parse("fallback-hardcode"); // ← REMOVE

// AFTER:
var tenantId = TenantProvider.TenantId
    ?? throw new InvalidOperationException("TenantId not found in claims.");
```

Same fix for `ExpenseEntry.razor:211-219`.

### W7-T2: Fix Tier 2 — JS interop workaround
**File:** `5_WebApps/ShopERP/Components/Pages/Accounting/ExpenseEntry.razor:222-244`
- Investigate why JS interop was needed for @bind
- Fix Blazor binding natively (likely Blazor interactivity issue — see Gate 2)
- Remove JS workaround code
- Remove `vananReadElementValue` from `App.razor:18-27` (if no longer used)

### W7-T3: Remove obsolete methods
**File:** `3_CoreHub/Services/HKDBookService.cs`
- Remove `GenerateTrialBalanceAsync` (line 356) — 0 callers, marked [Obsolete]
- Remove `ConvertToJournalEntries` (line 709) — 0 callers, marked [Obsolete]
- Verify no references after removal (grep)

**File:** `3_CoreHub/Services/WebhookService.cs:40`
- Remove obsolete constructor overload
- Verify single constructor used everywhere

### W7-T4: Docker hardening
**File:** `docker-compose.yml`
```yaml
# Add resource limits:
services:
  shopperp:
    deploy:
      resources:
        limits:
          cpus: '2.0'
          memory: 1G
        reservations:
          cpus: '0.5'
          memory: 256M
  
  # Add SQLite volume:
  volumes:
    - vanan-sqlite:/data/sqlite

volumes:
  vanan-sqlite:
```

**File:** `5_WebApps/ShopERP/Dockerfile:33-34`
- Enable tests or add separate test stage:
```dockerfile
# Test stage
FROM base AS test
RUN dotnet test --configuration Release --no-build
```

### W7-T5: Security headers middleware
**File:** `5_WebApps/ShopERP/Program.cs`
- Add security headers middleware:
```csharp
app.Use(async (context, next) =>
{
    context.Response.Headers["X-Content-Type-Options"] = "nosniff";
    context.Response.Headers["X-Frame-Options"] = "DENY";
    context.Response.Headers["X-XSS-Protection"] = "1; mode=block";
    context.Response.Headers["Referrer-Policy"] = "strict-origin-when-cross-origin";
    await next();
});
```

### W7-T6: Build + guard + all tests pass
- Build 0 errors, guard pass, all tests pass
- Verify no regression from obsolete method removal

## Verification
- [ ] `grep -r "fallback-hardcode" 5_WebApps/` — 0 results
- [ ] `grep -r "vananReadElementValue" 5_WebApps/` — 0 results (if M3+M4 fixed)
- [ ] `grep -r "\[Obsolete\]" 3_CoreHub/Services/HKDBookService.cs` — 0 results
- [ ] `docker-compose.yml` has resource limits + SQLite volume
- [ ] Security headers middleware added
- [ ] Build 0 errors, guard pass, all tests pass

## Rollback
- Git revert per item (each item independent)
- If Tier 2 fix breaks Blazor: revert M3+M4, keep as tech debt
- If obsolete removal breaks build: restore method, investigate caller

## Open Questions
- Q1: Tier 2 (JS interop) — is this a Blazor interactivity bug? (Gate 2 may apply)
- Q2: Docker tests in Dockerfile — enable or separate stage? (Separate stage preferred)
- Q3: NotImplemented in sync/audit services (M5 from review) — fix or defer? (Defer to post-production)
