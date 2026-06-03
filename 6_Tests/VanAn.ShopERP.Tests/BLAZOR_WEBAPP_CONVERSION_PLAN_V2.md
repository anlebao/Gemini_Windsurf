# Detailed Coding Plan V2: ShopERP Blazor Migration (3-PR Strategy)

> **Workflow:** `test-refactor-workflow.md` → Step 4: Implementation Plan
> **Status:** AWAITING USER APPROVAL
> **Revision:** V2 — addresses user feedback on V1 (root cause not proven, scope too large, premature cleanup, insufficient validation)
> **Estimate:** 0.5–2 days total across 3 PRs

---

## 0. Why V2

V1 had 5 critical flaws:
1. Root cause asserted without evidence (full stack trace, DI resolution path, where `TenantService` is resolved)
2. Treated hosting-model migration as a small bugfix (routing/prerender/auth/lifecycle/SignalR all impacted)
3. Removed `MapFallbackToPage` without proof Blazor router safely covers all cases
4. Changed `Home.razor` route before proving Blazor routing is even active
5. Validation plan missing DI inventory and endpoint inventory

**Fix:** Split into 3 incremental PRs with clear exit criteria. Each PR independently shippable and rollback-safe.

---

## PR1 — Prove Root Cause + Unblock Startup (no architecture change)

### 1.1 Goal
Get `localhost:5010` listening. Prove the *exact* DI failure path. Do **not** add Blazor routing.

### 1.2 Evidence Gathering (must complete BEFORE any code change)

**Step A — Capture full stack trace**
```bash
cd 5_WebApps/ShopERP
dotnet run --urls http://localhost:5010 2>&1 | tee startup-error.log
```
Save full unhandled exception including all inner exceptions and stack frames.

**Step B — Inspect `TenantService` constructor**
- File: `UI.Platform/Services/TenantService.cs`
- Document: constructor signature, all injected dependencies, lifetime (Scoped/Singleton/Transient)

**Step C — Find where `TenantService` is consumed**
```
grep -rn "ITenantService" 5_WebApps/ShopERP/
grep -rn "ITenantService" UI.Platform/
```
- Determine: is it injected at startup (singleton parent) or per-request (middleware/page)?
- If injected by a singleton → scope-validation failure (different fix)
- If injected by a Razor Page / Blazor component → missing service registration (current hypothesis)

**Step D — Inspect DI registrations**
```bash
dotnet run -- --list-services 2>&1 | grep -E "(AuthenticationStateProvider|TenantService)"
```
Or add temporary diagnostic in `Program.cs` after `builder.Build()`:
```csharp
foreach (var sd in builder.Services.Where(s => 
    s.ServiceType.Name.Contains("AuthenticationStateProvider") ||
    s.ServiceType.Name.Contains("TenantService")))
{
    Console.WriteLine($"{sd.Lifetime} {sd.ServiceType.FullName} -> {sd.ImplementationType?.FullName}");
}
```

**Deliverable:** `5_WebApps/ShopERP/STARTUP_DIAGNOSIS.md` containing evidence A–D. **Without this file, PR1 cannot proceed.**

### 1.3 Hypothesis Decision Tree

After evidence, decide:

| Evidence | Diagnosis | Fix |
|---|---|---|
| Stack trace shows `BuildServiceProvider` validating `TenantService` and not finding `AuthenticationStateProvider` | Missing service registration | Register HttpContext-based `AuthenticationStateProvider` |
| Stack trace shows scope mismatch (singleton consuming scoped) | Lifetime mismatch | Fix lifetime, not registration |
| `TenantService` is consumed only by Blazor components (none currently mapped) | Service is dead code reachable only via DI validation | Either remove `TenantService` registration OR register provider |
| Different error entirely | New diagnosis | Re-plan |

### 1.4 Implementation (only after evidence confirms hypothesis)

**Most likely path** (custom HttpContext provider — minimal change):

**File:** `5_WebApps/ShopERP/Services/HttpContextAuthenticationStateProvider.cs` (NEW)
```csharp
using Microsoft.AspNetCore.Components.Authorization;
using System.Security.Claims;

namespace VanAn.ShopERP.Services;

public sealed class HttpContextAuthenticationStateProvider : AuthenticationStateProvider
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public HttpContextAuthenticationStateProvider(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public override Task<AuthenticationState> GetAuthenticationStateAsync()
    {
        var user = _httpContextAccessor.HttpContext?.User
            ?? new ClaimsPrincipal(new ClaimsIdentity());
        return Task.FromResult(new AuthenticationState(user));
    }
}
```

**File:** `5_WebApps/ShopERP/Program.cs`

Add ONE line after line 81 (`AddScoped<ITenantService, TenantService>`):
```csharp
builder.Services.AddScoped<Microsoft.AspNetCore.Components.Authorization.AuthenticationStateProvider,
    VanAn.ShopERP.Services.HttpContextAuthenticationStateProvider>();
```

**Do NOT change:** routing, `MapRazorPages`, `MapFallbackToPage`, `Home.razor`, `Routes.razor`, `App.razor`.

### 1.5 Validation (PR1)

**Build:**
```bash
dotnet build 5_WebApps/ShopERP/VanAn.ShopERP.csproj
```
Expected: 0 errors.

**Endpoint Inventory (required):**
```bash
# Start app in background, then:
curl -i -o nul -w "%{http_code}\n" http://localhost:5010/
curl -i -o nul -w "%{http_code}\n" http://localhost:5010/login
curl -i -o nul -w "%{http_code}\n" http://localhost:5010/accounting/balance
curl -i -o nul -w "%{http_code}\n" http://localhost:5010/accounting/revenue
curl -i -o nul -w "%{http_code}\n" http://localhost:5010/counter
```
Expected baseline (PR1):
- `/` → 200 or 302 (Razor Page Index)
- `/login` → 200 (Razor Page Login)
- `/accounting/*` → **404** (Blazor not yet mapped — acceptable for PR1)
- `/counter` → 404 (acceptable for PR1)

**DI Inventory (required):**
Add diagnostic logging (temporary) showing `AuthenticationStateProvider` and `TenantService` registrations. Capture in log file. Remove after verification.

**Bunit regression:**
```bash
dotnet test 6_Tests/VanAn.ShopERP.Tests/VanAn.ShopERP.Tests.csproj
```
Expected: no regression (current state).

### 1.6 PR1 Exit Criteria
- [ ] `STARTUP_DIAGNOSIS.md` exists with full evidence
- [ ] App listens on `localhost:5010`
- [ ] `/login` returns 200
- [ ] `/accounting/*` returns 404 (expected — not yet wired)
- [ ] DI inventory log shows `AuthenticationStateProvider` registered
- [ ] No Bunit regression

### 1.7 PR1 Files Changed
1. `5_WebApps/ShopERP/STARTUP_DIAGNOSIS.md` (NEW — evidence document)
2. `5_WebApps/ShopERP/Services/HttpContextAuthenticationStateProvider.cs` (NEW)
3. `5_WebApps/ShopERP/Program.cs` (1-line addition)

**Estimate:** 2–4 hours including evidence gathering.

---

## PR2 — Enable Blazor Routing (no cleanup)

### 2.1 Goal
Make `/accounting/*` and other Blazor `@page` routes reachable. Do **not** remove `MapFallbackToPage`. Do **not** change `Home.razor` route.

### 2.2 Pre-conditions
- PR1 merged and verified
- `STARTUP_DIAGNOSIS.md` confirms architecture is Blazor Web App pattern

### 2.3 Implementation

**File:** `5_WebApps/ShopERP/Program.cs`

**Add service registration** (after `AddRazorPages()` at line 43):
```csharp
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();
builder.Services.AddCascadingAuthenticationState();
```

**Add endpoint mapping** (between `MapRazorPages()` and `MapFallbackToPage`):
```csharp
app.MapControllers();
app.MapRazorPages();

// NEW: Blazor routing — registered AFTER Razor Pages so Razor Page routes win on conflict
app.MapRazorComponents<VanAn.ShopERP.Components.App>()
    .AddInteractiveServerRenderMode();

app.MapFallbackToPage("/Index");  // KEEP — proven safe net
```

**Conflict handling:**
- Both `/Pages/Index.cshtml` (`@page "/"`) and `/Components/Pages/Home.razor` (`@page "/"`) exist
- Endpoint order: Razor Pages registered first → Razor Page wins for `/`
- If Blazor router throws on duplicate route, **revert and replan** (do not patch in PR2)

### 2.4 Optional cleanup (line 159)

Remove duplicate `AddScoped<CascadingAuthenticationState>()` since `AddCascadingAuthenticationState()` replaces it.
- **Decision:** ONLY if it causes a build/runtime error. Otherwise leave for PR3.

### 2.5 Validation (PR2)

**Endpoint Inventory:**
```bash
curl -i -o nul -w "%{http_code}\n" http://localhost:5010/
curl -i -o nul -w "%{http_code}\n" http://localhost:5010/login
curl -i -o nul -w "%{http_code}\n" http://localhost:5010/accounting/balance
curl -i -o nul -w "%{http_code}\n" http://localhost:5010/accounting/revenue
curl -i -o nul -w "%{http_code}\n" http://localhost:5010/counter
curl -i -o nul -w "%{http_code}\n" http://localhost:5010/nonexistent
```
Expected after PR2:
- `/` → 200 (Razor Page Index, NOT Blazor Home)
- `/login` → 200 (Razor Page)
- `/accounting/*` → 200 or 302 (Blazor — was 404 in PR1)
- `/counter` → 200 (Blazor)
- `/nonexistent` → fallback to `/Index` (Razor Page) — proves fallback still works

**Auth flow test:**
- Hit `/accounting/balance` unauthenticated → expect redirect to `/login`
- Login via Razor Page → expect cookie set
- Hit `/accounting/balance` authenticated → expect 200 with auth state available

**Bunit regression:** must remain clean.

**E2E smoke:**
```bash
.\run-accounting-full.bat
```
Expected: ShopERP starts within 60s; E2E reaches `/accounting/*`. E2E test pass/fail itself still out-of-scope.

### 2.6 PR2 Exit Criteria
- [ ] All endpoints in inventory return expected codes
- [ ] `/` resolves to Razor Page (no route conflict crash)
- [ ] `/accounting/*` reachable
- [ ] `MapFallbackToPage` still functional
- [ ] No Bunit regression
- [ ] E2E can navigate (pass/fail not required)

### 2.7 PR2 Files Changed
1. `5_WebApps/ShopERP/Program.cs` (service + endpoint additions)

**Estimate:** 4–8 hours (includes route conflict debugging if it occurs).

---

## PR3 — Route Cleanup & Hardening

### 3.1 Goal
Clean up route conflicts, optionally migrate `QuickSetup.razor`, optionally harden `Routes.razor`.

### 3.2 Pre-conditions
- PR2 merged and verified
- Endpoint inventory baseline established

### 3.3 Candidate Changes (each independently approvable)

**3.3.A — `Home.razor` route**
- Option a: Change `@page "/"` → `@page "/home"` (keep file, move route)
- Option b: Delete `Home.razor` if unused
- **Decision criteria:** Inspect usage. If referenced → option a. If not → option b.

**3.3.B — Remove `MapFallbackToPage("/Index")`**
- Only if PR2 endpoint inventory + production testing shows Blazor `<NotFound>` covers all cases
- Add `<NotFound>` to `Routes.razor` first

**3.3.C — `Routes.razor` hardening**
```razor
<Router AppAssembly="@typeof(Program).Assembly">
    <Found Context="routeData">
        <AuthorizeRouteView RouteData="@routeData" DefaultLayout="@typeof(Layout.MainLayout)" />
        <FocusOnNavigate RouteData="@routeData" Selector="h1" />
    </Found>
    <NotFound>
        <PageTitle>Not found</PageTitle>
        <LayoutView Layout="@typeof(Layout.MainLayout)">
            <p>Sorry, there's nothing at this address.</p>
        </LayoutView>
    </NotFound>
</Router>
```

**3.3.D — `QuickSetup.razor` migration**
- Move from `/Pages/QuickSetup.razor` → `/Components/Pages/QuickSetup.razor`
- Only if PR2 testing reveals it's broken

**3.3.E — Remove duplicate `CascadingAuthenticationState` registration** (line 159)
- If not done in PR2

### 3.4 Validation (PR3)
- Re-run full endpoint inventory
- Re-run E2E smoke
- Re-run Bunit
- Verify auth flow

### 3.5 PR3 Files Changed
- `5_WebApps/ShopERP/Components/Pages/Home.razor` (if 3.3.A)
- `5_WebApps/ShopERP/Components/Routes.razor` (if 3.3.C)
- `5_WebApps/ShopERP/Program.cs` (if 3.3.B or 3.3.E)
- `5_WebApps/ShopERP/Pages/QuickSetup.razor` → moved (if 3.3.D)

**Estimate:** 2–6 hours depending on which sub-changes selected.

---

## Cumulative Risk Matrix

| Risk | PR1 | PR2 | PR3 | Mitigation |
|---|---|---|---|---|
| Wrong root cause | High | — | — | Evidence gathering before code (1.2) |
| Hosting model side-effects (SignalR, prerender) | — | High | — | PR2 endpoint inventory + auth flow test |
| Route conflict crashes app | — | High | — | Order endpoints; revert if duplicate-route exception |
| `MapFallbackToPage` removal breaks deep links | — | — | Medium | PR3 only after PR2 inventory proves Blazor `<NotFound>` |
| `Home.razor` premature edit | — | — | Low | Deferred to PR3 |
| Bunit regression | Low | Low | Low | Run after each PR |

---

## Rollback Plan (per PR)

- **PR1:** `git revert` — single commit, removes 1 file + 1 line
- **PR2:** `git revert` — removes service + endpoint registration; PR1 still functional
- **PR3:** `git revert` per sub-change — independent

---

## Validation Tooling (shared across PRs)

**Endpoint inventory script** (`5_WebApps/ShopERP/scripts/endpoint-inventory.ps1` — NEW, optional):
```powershell
$urls = @(
    "/", "/login", "/accounting/balance", "/accounting/revenue",
    "/accounting/expense", "/accounting/transactions", "/counter", "/nonexistent"
)
foreach ($u in $urls) {
    $code = (curl.exe -s -o NUL -w "%{http_code}" "http://localhost:5010$u")
    Write-Host "$code  $u"
}
```

**DI inventory** — temporary diagnostic in `Program.cs` (remove after each PR):
```csharp
#if DEBUG_DI
foreach (var sd in builder.Services.Where(s => 
    s.ServiceType.FullName?.Contains("Authentication") == true ||
    s.ServiceType.FullName?.Contains("Tenant") == true))
{
    Console.WriteLine($"{sd.Lifetime,-9} {sd.ServiceType.FullName,-80} -> {sd.ImplementationType?.FullName}");
}
#endif
```

---

## User Decision Required

Please review and approve **per PR**:

### PR1 Approval
- [ ] Evidence-first approach acceptable?
- [ ] `HttpContextAuthenticationStateProvider` as the minimal fix acceptable?
- [ ] No routing changes in PR1 — agreed?
- [ ] Exit criteria sufficient?

### PR2 Approval (review after PR1 lands)
- [ ] Add Blazor routing without removing fallback — agreed?
- [ ] Defer `Home.razor` route change — agreed?
- [ ] Endpoint inventory as gate — agreed?

### PR3 Approval (review after PR2 lands)
- [ ] Cleanup scope acceptable?
- [ ] Each sub-change independently approvable — agreed?

**Status:** ⏸️ PAUSED — awaiting PR1 approval to begin evidence gathering.

---

## Appendix: Open Questions

1. Is `TenantService` actually used in production code paths, or only by Blazor components that are currently dead routes? (Answer affects PR1 hypothesis.)
2. What's the intended rendering mode — InteractiveServer, InteractiveAuto, or Static SSR? (Affects PR2 `AddInteractiveServerComponents` choice.)
3. Does the cookie auth scheme produce claims compatible with `TenantService` expectations (`TenantId` claim)? (Affects PR2 auth flow validation.)
4. Are there any SignalR hubs already defined that conflict with Blazor's built-in `_blazor` hub? (Affects PR2.)
