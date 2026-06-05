# Detailed Coding Plan: Convert ShopERP to Blazor Web App (Approach 1)

> **Workflow:** `test-refactor-workflow.md` → Step 4: Implementation Plan
> **Status:** AWAITING USER APPROVAL
> **Justification:** Production code change is required because E2E tests cannot run without ShopERP starting. The DI bug blocks the entire test-refactor validation phase.

---

## 1. Problem Statement

### Symptom
ShopERP fails to start with DI validation error:
```
Unable to resolve service for type 'Microsoft.AspNetCore.Components.Authorization.AuthenticationStateProvider'
while attempting to activate 'VanAn.UI.Platform.Services.TenantService'
```

### Root Cause
ShopERP is architecturally a **.NET 8 Blazor Web App** (evidenced by `App.razor` containing `blazor.web.js`, `<HeadOutlet />`, `<Routes />`) but `Program.cs` is configured as **Razor Pages only** (`AddRazorPages()` + `MapRazorPages()`).

- `AuthenticationStateProvider` is registered automatically by Blazor's interactive components stack, but the stack is not enabled.
- `TenantService` depends on `AuthenticationStateProvider` → DI validation fails on `builder.Build()`.

### Impact on E2E Tests
- E2E tests navigate to `/accounting/balance`, `/accounting/revenue`, etc. (Blazor routes in `Components/Pages/Accounting/*.razor`).
- These routes are **not served** today because no Blazor routing endpoint is mapped.
- E2E tests fail with `ERR_CONNECTION_REFUSED` because ShopERP never reaches `Listening on http://localhost:5010`.

---

## 2. Architecture Analysis

### Current Mixed Reality
| Folder | File Pattern | Routing Today | Routing Needed |
|---|---|---|---|
| `/Pages/*.cshtml` | Razor Pages | `MapRazorPages()` ✅ | Keep |
| `/Pages/QuickSetup.razor` | Blazor (legacy location) | None ❌ | Inspect; out-of-scope |
| `/Components/Pages/*.razor` | Blazor `@page` | None ❌ | `MapRazorComponents<App>()` |
| `/Components/Pages/Accounting/*.razor` | Blazor `@page` | None ❌ | `MapRazorComponents<App>()` |

### Route Conflicts (must resolve)
| Route | Razor Page | Blazor Component | Decision |
|---|---|---|---|
| `/` | `/Pages/Index.cshtml` | `/Components/Pages/Home.razor` | Keep `Index.cshtml`, change `Home.razor` to `/home` (or remove) |
| `/Login` | `/Pages/Login.cshtml` | none | Keep Razor Page (auth gateway) |
| `/accounting/*` | none | `/Components/Pages/Accounting/*.razor` | Blazor only |
| `/counter` | none | `/Components/Pages/Counter.razor` | Blazor only |

### Files Inventoried
- 9 `.razor` files with `@page` directive
- 10 `.cshtml` Razor Pages
- 1 `Routes.razor`, 1 `App.razor`, 1 `MainLayout.razor`, 1 `NavMenu.razor`

---

## 3. Detailed Changes

### File 1: `5_WebApps/ShopERP/Program.cs`

#### Change 1.1 — Add Blazor Web App service registration

**Location:** Line 43

**Before:**
```csharp
builder.Services.AddRazorPages();
```

**After:**
```csharp
// Keep Razor Pages for /Login, /Index, /Kitchen, /GuardRedirect, /Logout, etc.
builder.Services.AddRazorPages();

// Add Blazor Web App support for /Components/**/*.razor with @page
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

// Required for AuthenticationStateProvider to flow into Blazor components
builder.Services.AddCascadingAuthenticationState();
```

**Why:** `AddInteractiveServerComponents()` registers the Blazor circuit infrastructure, which transitively registers `AuthenticationStateProvider`. `AddCascadingAuthenticationState()` makes the auth state cascade through Blazor components (required by `TenantService`).

#### Change 1.2 — Remove duplicate `CascadingAuthenticationState` registration

**Location:** Line 159

**Before:**
```csharp
builder.Services.AddScoped<Microsoft.AspNetCore.Components.Authorization.CascadingAuthenticationState>();
```

**After:** (delete this line)

**Why:** Replaced by idiomatic `AddCascadingAuthenticationState()` in Change 1.1.

#### Change 1.3 — Add Blazor endpoint mapping

**Location:** Lines 203–205

**Before:**
```csharp
app.MapControllers();
app.MapRazorPages();
app.MapFallbackToPage("/Index");
```

**After:**
```csharp
app.MapControllers();
app.MapRazorPages();

// Map Blazor components AFTER Razor Pages so Razor Page routes win on conflict
app.MapRazorComponents<VanAn.ShopERP.Components.App>()
    .AddInteractiveServerRenderMode();

// MapFallbackToPage removed: Blazor router handles unmatched routes via Routes.razor
```

**Why:** 
- `MapRazorComponents<App>()` registers Blazor routing using `App.razor` as root.
- Endpoint order matters: Razor Pages registered first means `/Login`, `/Index` take precedence over any conflicting Blazor `@page`.
- `MapFallbackToPage` removed because Blazor router now handles unmatched routes.

---

### File 2: `5_WebApps/ShopERP/Components/Pages/Home.razor`

#### Change 2.1 — Resolve route conflict with `/Pages/Index.cshtml`

**Location:** Line 1

**Before:**
```razor
@page "/"
```

**After:**
```razor
@page "/home"
```

**Why:** `/Pages/Index.cshtml` already owns route `/`. Moving `Home.razor` to `/home` removes the conflict. (Alternative: delete `Home.razor` entirely if unused — confirm during implementation.)

---

### File 3: `5_WebApps/ShopERP/Components/Routes.razor`

**Status:** **No change in this PR.** Current `<Router>` + `<RouteView>` works with `AddInteractiveServerRenderMode()`. Optional `<AuthorizeRouteView>` + `<NotFound>` deferred to follow-up.

---

### File 4: `5_WebApps/ShopERP/Components/_Imports.razor`

**Status:** **Inspect only.** Add missing usings only if build fails:
```razor
@using Microsoft.AspNetCore.Components.Authorization
@using Microsoft.AspNetCore.Components.Web
@using Microsoft.AspNetCore.Components.Routing
```

---

### File 5: `5_WebApps/ShopERP/Pages/QuickSetup.razor`

**Status:** **Out of scope.** This file has `.razor` extension but lives in `/Pages/` (Razor Pages folder). May be a legacy hosted component. Will verify during runtime testing. If broken, document for follow-up PR — do not fix in this PR.

---

## 4. Files NOT Changed (explicit list)

To respect Phase Isolation and avoid scope creep:
- All `Components/Pages/Accounting/*.razor` (correct as-is)
- All `Pages/*.cshtml` Razor Pages (correct as-is)
- `UI.Platform/Services/TenantService.cs` (works once `AuthenticationStateProvider` is available)
- Any test files (this is a production fix to unblock tests)
- `docker-compose.yml`, `run-accounting-full.bat` (separate concern)

---

## 5. Validation Plan

### Phase A: Build Validation
1. `dotnet build 5_WebApps/ShopERP/VanAn.ShopERP.csproj`
2. Expected: 0 errors. NU1506 warnings acceptable (pre-existing).

### Phase B: Runtime Validation
1. `cd 5_WebApps/ShopERP && dotnet run --urls http://localhost:5010`
2. Expected: `Now listening on: http://localhost:5010` in stdout.
3. `curl -i http://localhost:5010/login` → HTTP 200 (Razor Page renders).
4. `curl -i http://localhost:5010/accounting/balance` → HTTP 200 or 302 (redirect to /login if unauth).

### Phase C: Unit/Bunit Regression
1. `dotnet test 6_Tests/VanAn.ShopERP.Tests/VanAn.ShopERP.Tests.csproj`
2. Expected: 26/26 passing (no regression).

### Phase D: E2E Smoke
1. `.\run-accounting-full.bat`
2. Expected: ShopERP starts within 60s; E2E reaches `/login` and `/accounting/*`.
3. **Exit criterion:** App reachable. E2E pass/fail itself is out-of-scope for this PR.

---

## 6. Risk Assessment

| Risk | Likelihood | Impact | Mitigation |
|---|---|---|---|
| Route conflict `/` (Razor Page vs Blazor Home) | High | App fails to start | Change 2.1 — move Home.razor to `/home` |
| `QuickSetup.razor` breaks | Medium | One page broken | Document; follow-up PR |
| Login flow breaks | Low | E2E auth fails | Login stays as Razor Page; cookie shared |
| `TenantService` still fails after change | Low | Same error | `AddInteractiveServerComponents` registers `AuthenticationStateProvider` in .NET 8 (verified) |
| Razor Page routes break | Low | 404s | Register Razor Pages BEFORE Blazor (Change 1.3) |
| Bunit regression | Low | Test failures | No test code changes; Bunit has own DI |

---

## 7. Rollback Plan

If implementation fails:
1. `git checkout -- 5_WebApps/ShopERP/Program.cs 5_WebApps/ShopERP/Components/Pages/Home.razor`
2. Fall back to **Approach 2** (custom `HttpContextAuthenticationStateProvider`).

---

## 8. Implementation Order

1. Git commit current state (clean baseline)
2. Change 1.1 — Blazor service registration
3. Change 1.2 — Remove duplicate `CascadingAuthenticationState`
4. Change 1.3 — Add `MapRazorComponents<App>()`, remove `MapFallbackToPage`
5. Build → verify compile
6. Change 2.1 — Update `Home.razor` route
7. Build → verify compile
8. Runtime test — Phase B
9. Bunit regression — Phase C
10. E2E smoke — Phase D
11. Document QuickSetup.razor status (works / broken / follow-up)

**Estimated time:** 30–45 minutes (excluding E2E debugging).

---

## 9. Out-of-Scope Follow-ups

- E2E test failures unrelated to app startup
- `QuickSetup.razor` migration
- `Home.razor` content review / deletion
- `<AuthorizeRouteView>` and `<NotFound>` hardening in `Routes.razor`
- NU1506 duplicate PackageVersion warnings
- `run-accounting-full.bat` improvements

---

## 10. User Decision Required

Please review and approve:

- [ ] Architecture analysis correct?
- [ ] Files in scope acceptable (Program.cs + Home.razor only)?
- [ ] Route conflict resolution: `Home.razor` → `/home` (vs delete)?
- [ ] Defer `Routes.razor` hardening?
- [ ] Rollback to Approach 2 if Approach 1 fails — acceptable?

**Status:** ⏸️ PAUSED — awaiting approval before implementation.
