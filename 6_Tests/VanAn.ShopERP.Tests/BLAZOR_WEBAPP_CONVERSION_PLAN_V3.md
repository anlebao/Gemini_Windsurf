# Detailed Coding Plan V3: ShopERP Blazor Migration (4-PR Strategy + Gates)

> **Workflow:** `test-refactor-workflow.md` → Step 4: Implementation Plan
> **Status:** AWAITING USER APPROVAL (PR1 only — PR2/3/4 not yet approvable)
> **Revision:** V3 — addresses 6 V2 issues (premature provider, wrong CLI, source-tree artifact, oversized inventory, missing PR2 abort, PR3 scope creep)
> **Estimate:** 0.5–2 days total

---

## 0. Why V3

V2 issues:
1. PR1 pre-committed to creating `HttpContextAuthenticationStateProvider` before inspecting existing registrations
2. `dotnet run -- --list-services` is not a real ASP.NET CLI feature
3. `STARTUP_DIAGNOSIS.md` placed in source tree (`5_WebApps/ShopERP/`) — should be artifact location
4. PR1 endpoint inventory included `/accounting/*` checks that are irrelevant pre-routing
5. PR2 lacked explicit abort conditions
6. PR3 mixed route cleanup with optional hardening (Routes.razor, QuickSetup) — scope creep

**Fix:** Inspect-first, artifacts out-of-tree, minimal PR1 inventory, hard PR2 abort gates, split PR3/PR4.

---

## Decision Gates (mandatory before each PR)

### Gate A — before PR1 starts coding
Required artifacts in `docs/diagnostics/startup-diag-YYYYMMDD.md`:
- A1: Full unhandled exception stack trace from `dotnet run`
- A2: `TenantService` constructor signature + dependencies + lifetime
- A3: Consumer graph — every place `ITenantService` is injected, with the lifetime of its consumer
- A4: DI dump showing actual registrations for `AuthenticationStateProvider`, `IHttpContextAccessor`, `CascadingAuthenticationState`, anything Blazor-related
- A5: Confirmation of which scheme is in play: Razor Pages only / `AddServerSideBlazor` / `AddRazorComponents` / hybrid

**Without all 5 items, PR1 does not proceed.**

### Gate B — before PR2 starts coding
Required proof:
- B1: Endpoint inventory from PR1 baseline saved
- B2: Confirmation `Components/App.razor` is the intended Blazor root
- B3: Decision on rendering mode (InteractiveServer / InteractiveAuto / Static SSR) — documented with rationale
- B4: Confirmation no existing SignalR hub conflicts with `/_blazor`

### Gate C — before PR3 starts coding
Required proof:
- C1: Route conflict actually observed (duplicate-route exception OR wrong page served for `/`)
- C2: List of routes that need cleanup, each with evidence

If C1 fails (no actual conflict), **PR3 is cancelled**.

---

## PR1 — Diagnose + Unblock Startup (Inspect-First)

### 1.1 Goal
Get `localhost:5010` listening. **No assumption** about which fix is needed until inspection completes.

### 1.2 Phase 1.A — Inspection (NO code changes)

**Step 1 — Inspect existing auth registrations**
```
grep -rn "AddAuthentication\|AddAuthorization\|AddCascadingAuth\|AddServerSideBlazor\|AddRazorComponents\|AddInteractiveServer\|AddBlazor" 5_WebApps/ShopERP/
grep -rn "AuthenticationStateProvider" 5_WebApps/ShopERP/ UI.Platform/
```

**Step 2 — Confirm `IHttpContextAccessor` registration**
```
grep -rn "AddHttpContextAccessor" 5_WebApps/ShopERP/
```

**Step 3 — Check if any `AuthenticationStateProvider` implementation exists**
```
grep -rn "class.*AuthenticationStateProvider" 5_WebApps/ShopERP/ UI.Platform/ 1_Shared/
```

**Step 4 — Inspect `TenantService` constructor**
File: `UI.Platform/Services/TenantService.cs`
Document:
- All constructor parameters
- Lifetime declared in DI (`AddScoped`/`AddSingleton`/`AddTransient`)
- Whether it's resolved at startup (validation) or per-request

**Step 5 — Find all consumers of `ITenantService`**
```
grep -rn "ITenantService" 5_WebApps/ShopERP/ UI.Platform/ 3_CoreHub/
```
For each consumer, note its lifetime and whether reachable at startup.

**Step 6 — Capture full stack trace**
```
cd 5_WebApps/ShopERP
dotnet run --urls http://localhost:5010 > startup.log 2>&1
```
Wait until error printed, kill process. Copy `startup.log` to `docs/diagnostics/startup-diag-YYYYMMDD.md`.

**Step 7 — DI dump (replace V2's invalid CLI)**
Add a temporary diagnostic block in `Program.cs` BEFORE `builder.Build()`:
```csharp
// DIAGNOSTIC ONLY — remove after capture
foreach (var sd in builder.Services
    .Where(s =>
        (s.ServiceType.FullName ?? "").Contains("Authentication") ||
        (s.ServiceType.FullName ?? "").Contains("Tenant") ||
        (s.ServiceType.FullName ?? "").Contains("Cascading") ||
        (s.ServiceType.FullName ?? "").Contains("HttpContext"))
    .OrderBy(s => s.ServiceType.FullName))
{
    Console.WriteLine($"{sd.Lifetime,-9} {sd.ServiceType.FullName} -> {sd.ImplementationType?.FullName ?? sd.ImplementationFactory?.GetType().Name ?? "factory"}");
}
return; // exit before Build() to avoid the crash
```
Run, capture output, save to diagnosis doc, **revert this diagnostic block** before any real fix.

### 1.3 Phase 1.B — Decision (after inspection)

Use the decision matrix:

| Inspection Finding | Diagnosis | Fix Action |
|---|---|---|
| `AuthenticationStateProvider` already registered somewhere | Other DI bug (lifetime, scope) | Investigate further — do NOT add provider |
| Existing custom `AuthenticationStateProvider` class exists but not registered | Just register it | Add 1-line registration only |
| No provider class exists, `IHttpContextAccessor` registered | Need new provider | Create `HttpContextAuthenticationStateProvider` |
| `IHttpContextAccessor` not registered | Need both | Register `AddHttpContextAccessor()` + new provider |
| Stack trace points to scope mismatch (singleton consuming scoped) | Lifetime bug | Fix lifetime, not registration |
| `TenantService` only consumed by Blazor components (currently dead) | Service is dead-code reachable via DI validation only | Two options: (a) keep service, register provider; (b) remove `TenantService` registration until PR2 enables Blazor — prefer (a) for simplicity |

### 1.4 Phase 1.C — Implementation (only after decision)

**Conditional code change** — exact change determined by 1.3 outcome.

The implementation might be:
- Option α: 1-line registration of an already-existing provider
- Option β: Create new `HttpContextAuthenticationStateProvider` + 1-line registration
- Option γ: Fix scope mismatch (no new file)
- Option δ: Different fix entirely based on evidence

**File locations if Option β chosen:**
- `5_WebApps/ShopERP/Services/HttpContextAuthenticationStateProvider.cs` (NEW)
- `5_WebApps/ShopERP/Program.cs` (1-line addition)

**Do NOT touch:** routing, `MapRazorPages`, `MapFallbackToPage`, `Home.razor`, `Routes.razor`, `App.razor`, any `Components/` files.

### 1.5 Validation (PR1) — minimal scope

**Build:**
```
dotnet build 5_WebApps/ShopERP/VanAn.ShopERP.csproj
```

**Endpoint inventory (PR1 — only 2 endpoints):**
```
curl -i -o nul -w "%{http_code}  /\n" http://localhost:5010/
curl -i -o nul -w "%{http_code}  /login\n" http://localhost:5010/login
```
Expected:
- `/` → 200 or 302
- `/login` → 200

**Bunit regression:**
```
dotnet test 6_Tests/VanAn.ShopERP.Tests/VanAn.ShopERP.Tests.csproj
```

`/accounting/*` checks **deferred to PR2**.

### 1.6 PR1 Exit Criteria
- [ ] Gate A artifacts saved in `docs/diagnostics/startup-diag-YYYYMMDD.md`
- [ ] Inspection completed and decision documented
- [ ] App listens on `localhost:5010`
- [ ] `/` returns 200/302
- [ ] `/login` returns 200
- [ ] Diagnostic block reverted (no leftover code)
- [ ] No Bunit regression

### 1.7 PR1 Files Changed (final list determined post-inspection)
Likely:
1. `docs/diagnostics/startup-diag-YYYYMMDD.md` (NEW — outside source tree)
2. (optional) `5_WebApps/ShopERP/Services/HttpContextAuthenticationStateProvider.cs` — only if Option β
3. `5_WebApps/ShopERP/Program.cs` — 1 line

**Estimate:** 2–4 hours.

---

## PR2 — Enable Blazor Routing (with hard abort gates)

### 2.1 Goal
`/accounting/*` reachable. Keep `MapFallbackToPage`. No `Home.razor` edits.

### 2.2 Pre-conditions
- PR1 merged
- Gate B passed (rendering mode decided, App.razor confirmed, no SignalR conflict)

### 2.3 Implementation

**Add services** (after `AddRazorPages()`):
```csharp
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();   // mode chosen at Gate B3
builder.Services.AddCascadingAuthenticationState();
```

**Add endpoint** (between `MapRazorPages()` and `MapFallbackToPage`):
```csharp
app.MapRazorPages();
app.MapRazorComponents<VanAn.ShopERP.Components.App>()
    .AddInteractiveServerRenderMode();
app.MapFallbackToPage("/Index");  // KEEP
```

### 2.4 Hard Abort Conditions (revert immediately)

Abort PR2 (revert + replan) if any occurs:
- **AB1:** Duplicate-route exception during `app.Run()`
- **AB2:** Auth redirect loop (e.g., `/accounting/balance` → `/login` → `/accounting/balance` → `/login` …) more than 3 hops
- **AB3:** Process memory after warm-up exceeds PR1 baseline + 20%
- **AB4:** Bunit regression (any previously-passing test now fails)
- **AB5:** SignalR hub registration conflict
- **AB6:** Razor Page `/Login` no longer reachable

Capture baseline before PR2:
```
# PR1 baseline
Get-Process -Name dotnet | Where-Object {$_.MainWindowTitle -like "*ShopERP*"} | Select WS, PM
```

### 2.5 Validation (PR2) — full endpoint inventory

```
/                    → expect 200 (Razor Page Index)
/login               → expect 200 (Razor Page)
/accounting/balance  → expect 200 or 302 (Blazor)
/accounting/revenue  → expect 200 or 302
/accounting/expense  → expect 200 or 302
/accounting/transactions → expect 200 or 302
/counter             → expect 200
/nonexistent         → expect fallback to /Index (Razor Page)
```

Auth flow:
- Unauth `/accounting/balance` → 302 to `/login`
- Login via Razor Page → cookie set
- Auth `/accounting/balance` → 200 with auth state cascading

E2E smoke: `.\run-accounting-full.bat` — ShopERP starts, E2E reaches Blazor pages.

### 2.6 PR2 Exit Criteria
- [ ] Gate B passed
- [ ] Full endpoint inventory matches expected
- [ ] No abort condition AB1–AB6 triggered
- [ ] Auth flow works
- [ ] No Bunit regression

### 2.7 PR2 Files Changed
1. `5_WebApps/ShopERP/Program.cs`

**Estimate:** 4–8 hours.

---

## PR3 — Route Cleanup (narrow scope)

### 3.1 Goal
Resolve **proven** route conflicts only. Nothing optional.

### 3.2 Pre-conditions
- PR2 merged
- Gate C passed (route conflict actually observed)

### 3.3 Scope (route cleanup ONLY)

In scope:
- 3.3.A — `Home.razor` route change OR deletion (based on PR2 inspection of conflicts)
- 3.3.B — Remove `MapFallbackToPage("/Index")` if and only if Blazor `<NotFound>` proven sufficient via PR2 inventory
- 3.3.C — Remove duplicate `AddScoped<CascadingAuthenticationState>()` (line 159) if confirmed redundant by PR2

Out of scope (moved to PR4):
- ~~`Routes.razor` hardening~~
- ~~`QuickSetup.razor` migration~~

### 3.4 Validation
- Re-run full endpoint inventory from PR2
- Bunit regression
- E2E smoke

### 3.5 PR3 Files Changed
- `5_WebApps/ShopERP/Components/Pages/Home.razor` (if 3.3.A)
- `5_WebApps/ShopERP/Program.cs` (if 3.3.B or 3.3.C)

**Estimate:** 1–3 hours.

---

## PR4 — Optional Hardening (only if needed)

### 4.1 Goal
Optional improvements. None of these are required for the test-refactor objective.

### 4.2 Candidate sub-changes (each independently approvable)
- 4.2.A — `Routes.razor`: add `<AuthorizeRouteView>` + `<NotFound>`
- 4.2.B — Migrate `Pages/QuickSetup.razor` → `Components/Pages/QuickSetup.razor`
- 4.2.C — Cleanup NU1506 duplicate `PackageVersion` warnings

### 4.3 Pre-conditions
- PR3 merged
- Specific user request for each sub-change

### 4.4 Validation
- Per sub-change: endpoint inventory + Bunit + E2E smoke

**Estimate:** 1–4 hours, optional.

---

## Cumulative Risk Matrix

| Risk | PR1 | PR2 | PR3 | PR4 |
|---|---|---|---|---|
| Wrong root cause | High → mitigated by Gate A | — | — | — |
| Hosting model side-effects | — | High → AB1–AB6 | — | — |
| Route conflict | — | Detected here | Fixed here | — |
| Premature edits | Avoided by inspect-first | Avoided by Gate B | Avoided by Gate C | — |
| Scope creep | — | — | Avoided by splitting PR4 | Per-change approval |

---

## Validation Tooling (shared)

**Endpoint inventory script** (`scripts/endpoint-inventory.ps1` — outside source tree):
```powershell
param([string]$BaseUrl = "http://localhost:5010")
$urls = @("/", "/login", "/accounting/balance", "/accounting/revenue",
          "/accounting/expense", "/accounting/transactions", "/counter", "/nonexistent")
foreach ($u in $urls) {
    $code = (curl.exe -s -o NUL -w "%{http_code}" "$BaseUrl$u")
    Write-Host "$code  $u"
}
```

**Diagnosis location:** `docs/diagnostics/startup-diag-YYYYMMDD.md` (NOT in source tree)

**Endpoint baseline files:** `artifacts/endpoint-inventory-pr{1,2,3,4}-YYYYMMDD.txt`

---

## User Decision Required

### PR1 (revised — addresses V2 issues 1–4)
- [ ] Inspect-first approach (no provider commitment) — agreed?
- [ ] Replace `--list-services` with in-Program DI dump — agreed?
- [ ] Diagnosis files in `docs/diagnostics/` (not source tree) — agreed?
- [ ] PR1 endpoint inventory limited to `/` and `/login` — agreed?

### PR2 (revised — addresses V2 issue 5)
- [ ] Hard abort conditions AB1–AB6 — agreed?
- [ ] Gate B requirements — agreed?

### PR3 (revised — addresses V2 issue 6)
- [ ] Narrow scope (route cleanup only, gated by C1) — agreed?

### PR4 (new)
- [ ] Optional hardening parked here — agreed?

**Status:** ⏸️ PAUSED — only PR1 is currently approvable. PR2/3/4 reviewed after their pre-conditions met.

---

## Appendix: Open Questions (must resolve at relevant Gate)

| # | Question | Resolved at |
|---|---|---|
| Q1 | Is `TenantService` actually used in production paths or only DI-validation-reachable? | Gate A (A3 consumer graph) |
| Q2 | Rendering mode: InteractiveServer / InteractiveAuto / Static SSR? | Gate B (B3) |
| Q3 | Cookie auth produces `TenantId` claim correctly? | Gate B (auth flow test) |
| Q4 | SignalR hub conflict with `/_blazor`? | Gate B (B4) |
| Q5 | Is `AuthenticationStateProvider` already registered somewhere? | Gate A (A4 DI dump) |
