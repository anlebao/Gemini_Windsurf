# Detailed Coding Plan V5: ShopERP Blazor Migration (Lean 4-PR Strategy)

> **Workflow:** `test-refactor-workflow.md` → Step 4: Implementation Plan
> **Status:** PR1 🟢 approvable · PR2 🟡 partial · PR3 🟢 approvable · PR4 🟢 approvable
> **Revision:** V5 — leaner than V4. Removes over-engineering (CPU baseline, log baseline, antiforgery test in PR2, SQLite concurrency, ×5 log threshold)
> **Estimate:** 0.5–1.5 days total

---

## 0. Changelog V4 → V5

User feedback identified 5 over-engineering signals in V4:
1. PR1 perf baseline too broad (CPU + log baseline irrelevant pre-traffic)
2. Serilog audit (A6) shouldn't block PR1 startup unblock
3. Antiforgery form-submit test in PR2 — premature (PR2 = routing, not forms)
4. SQLite 5-concurrent-circuit check is a load test, not routing validation
5. Log-volume ×5 threshold is arbitrary — should warn, not abort

V5 fixes:
- **PR1:** Keep only StartupSec + WorkingSet. Serilog audit demoted to optional observation.
- **PR2:** Drop AB7 (log abort), AB8 (CPU), AB10 (antiforgery test), AB12 (SQLite concurrency). Keep duplicate-route, auth-loop, memory, Bunit, SignalR, /Login, startup, `_framework/*`.
- **PR4:** Antiforgery form-submit test parked here.

---

## Decision Gates

### Gate A — before PR1 starts coding
Required (blockers):
- A1: Full unhandled exception stack trace
- A2: `TenantService` constructor + dependencies + lifetime
- A3: Consumer graph for `ITenantService`
- A4: DI dump for `AuthenticationStateProvider`, `IHttpContextAccessor`, `CascadingAuthenticationState`
- A5: Scheme confirmation (Razor Pages / Blazor / hybrid)

Optional (observation only, does NOT block PR1):
- ~~A6 Serilog audit~~ → moved to optional observation; not a blocker

**Without A1–A5, PR1 does not proceed.**

### Gate B — before PR2 starts coding
Required:
- B1: PR1 endpoint inventory baseline saved
- B2: `Components/App.razor` confirmed as Blazor root
- B3: Rendering mode decision documented (InteractiveServer / InteractiveAuto / Static SSR)
- B4: No SignalR hub conflicts with `/_blazor`
- B5: `_framework/*` static assets reachable plan verified by inspection (not yet runtime-tested)

Not in Gate B (deferred):
- ~~Antiforgery form-submit test~~ → PR4
- ~~CPU baseline~~ → not collected
- ~~DB migration deep-check~~ → only basic inspection (no schema change required by routing)

### Gate C — before PR3 starts coding
- C1: Route conflict actually observed
- C2: List of routes needing cleanup with evidence

If C1 fails, **PR3 is cancelled.**

---

## PR1 — Diagnose + Unblock Startup 🟢 APPROVABLE

### 1.1 Goal
`localhost:5010` listening. Minimal change, evidence-first.

### 1.2 Phase 1.A — Inspection (NO code changes)

**Steps 1–7** (unchanged from V4):
1. Inspect existing auth registrations
2. Confirm `IHttpContextAccessor` registration
3. Check for existing `AuthenticationStateProvider` implementations
4. Inspect `TenantService` constructor
5. Find all consumers of `ITenantService`
6. Capture full stack trace
7. DI dump via temporary in-Program diagnostic block + early `return`

**Step 8 (optional, V5 demoted):** Serilog audit
- Grep `UseSerilog/WriteTo/MinimumLevel` in `Program.cs` and `appsettings*.json`
- **Observation only.** Does not block PR1.

### 1.3 Phase 1.B — Decision matrix
(Unchanged: Options α/β/γ/δ based on inspection)

### 1.4 Phase 1.C — Implementation
(Unchanged: conditional, post-inspection)

### 1.5 Validation (PR1) — minimal

**Build:**
```
dotnet build 5_WebApps/ShopERP/VanAn.ShopERP.csproj
```

**Endpoint inventory (only 2 endpoints):**
```
curl -i -o nul -w "%{http_code}  /\n"      http://localhost:5010/
curl -i -o nul -w "%{http_code}  /login\n" http://localhost:5010/login
```

**Minimal baseline capture (V5 — 2 metrics only):**
```powershell
"StartupSec: <measured>" | Out-File artifacts/perf-baseline-pr1-YYYYMMDD.txt -Append
$p = Get-Process -Id <pid>
"WorkingSetMB: $([math]::Round($p.WorkingSet64/1MB,1))" | Out-File artifacts/perf-baseline-pr1-YYYYMMDD.txt -Append
```

Dropped from V4:
- ~~CPU baseline~~ (idle CPU pre-traffic not meaningful)
- ~~Log volume baseline~~ (no Blazor traffic yet)

**Bunit regression:**
```
dotnet test 6_Tests/VanAn.ShopERP.Tests/VanAn.ShopERP.Tests.csproj
```

### 1.6 PR1 Exit Criteria
- [ ] Gate A artifacts (A1–A5) saved in `docs/diagnostics/`
- [ ] App listens on `localhost:5010`
- [ ] `/` and `/login` return expected codes
- [ ] StartupSec + WorkingSetMB saved to `artifacts/perf-baseline-pr1-*.txt`
- [ ] Diagnostic DI dump block reverted
- [ ] No Bunit regression

### 1.7 PR1 Files Changed
1. `docs/diagnostics/startup-diag-YYYYMMDD.md`
2. `artifacts/perf-baseline-pr1-YYYYMMDD.txt`
3. (optional) `5_WebApps/ShopERP/Services/HttpContextAuthenticationStateProvider.cs`
4. `5_WebApps/ShopERP/Program.cs` — 1 line

**Estimate:** 2–4 hours.

---

## PR2 — Enable Blazor Routing 🟡 PARTIAL APPROVAL

### 2.1 Goal
`/accounting/*` reachable. Keep `MapFallbackToPage`. No `Home.razor` edits.

### 2.2 Pre-conditions
- PR1 merged
- Gate B passed (B1–B5)

### 2.3 Implementation
(Unchanged: `AddRazorComponents().AddInteractiveServerComponents()`, `AddCascadingAuthenticationState()`, `MapRazorComponents<App>().AddInteractiveServerRenderMode()`)

### 2.4 Hard Abort Conditions (V5 trimmed)

Abort PR2 immediately if:
- **AB1:** Duplicate-route exception during `app.Run()`
- **AB2:** Auth redirect loop > 3 hops
- **AB3:** Process memory after warm-up > PR1 baseline + 20%
- **AB4:** Bunit regression
- **AB5:** SignalR hub conflict
- **AB6:** Razor Page `/Login` no longer reachable
- **AB9:** Startup time > PR1 baseline + 50%
- **AB11:** `_framework/blazor.web.js` returns non-200

Removed in V5:
- ~~AB7 (log volume ×5)~~ → arbitrary, demoted to warning
- ~~AB8 (CPU +30%)~~ → no PR1 baseline
- ~~AB10 (antiforgery form-submit)~~ → moved to PR4
- ~~AB12 (SQLite concurrency)~~ → load test, future perf validation

### 2.5 Validation (PR2)

**Endpoint inventory:**
```
/                          → 200
/login                     → 200
/accounting/balance        → 200 or 302
/accounting/revenue        → 200 or 302
/accounting/expense        → 200 or 302
/accounting/transactions   → 200 or 302
/counter                   → 200
/nonexistent               → fallback /Index
/_framework/blazor.web.js  → 200 application/javascript
```

**Auth flow (single-pass render, not form submit):**
- Unauth `/accounting/balance` → 302 to `/login`
- Login via Razor Page → cookie set
- Auth `/accounting/balance` → 200 page renders

**Perf comparison (memory + startup only):**
- Capture PR2 WorkingSetMB after 30s warm-up
- Memory must be < PR1 × 1.20 → else AB3
- Startup must be < PR1 × 1.50 → else AB9
- Save to `artifacts/perf-baseline-pr2-YYYYMMDD.txt`

**Log observation (warning only, not abort):**
- Note approximate log line rate. If 5×+ baseline → flag for PR4 follow-up. Does NOT abort PR2.

**E2E smoke:** `.\run-accounting-full.bat` — ShopERP starts, E2E reaches Blazor pages.

### 2.6 PR2 Exit Criteria
- [ ] Gate B (B1–B5) passed
- [ ] Endpoint inventory matches expected
- [ ] No abort AB1, AB2, AB3, AB4, AB5, AB6, AB9, AB11 triggered
- [ ] Auth render flow works (no submit test)
- [ ] `_framework/blazor.web.js` returns 200
- [ ] Memory + startup within thresholds
- [ ] No Bunit regression

### 2.7 PR2 Files Changed
1. `5_WebApps/ShopERP/Program.cs`
2. `artifacts/perf-baseline-pr2-YYYYMMDD.txt`

**Estimate:** 4–7 hours.

---

## PR3 — Route Cleanup 🟢 APPROVABLE (gated by C1)

### 3.1 Goal
Resolve proven route conflicts only.

### 3.2 Pre-conditions
- PR2 merged
- Gate C passed (C1 + C2)

If C1 fails → **PR3 cancelled.**

### 3.3 Scope
- 3.3.A — `Home.razor` route change OR delete
- 3.3.B — Remove `MapFallbackToPage("/Index")` if Blazor `<NotFound>` proven sufficient
- 3.3.C — Remove duplicate `AddScoped<CascadingAuthenticationState>()` (line 159) if confirmed redundant

### 3.4 Validation
- Re-run endpoint inventory from PR2
- Bunit regression
- E2E smoke

### 3.5 PR3 Files Changed
- `5_WebApps/ShopERP/Components/Pages/Home.razor` (if 3.3.A)
- `5_WebApps/ShopERP/Program.cs` (if 3.3.B/C)

**Estimate:** 1–3 hours.

---

## PR4 — Optional Hardening 🟢 APPROVABLE (per sub-change)

### 4.1 Scope (each independently approvable)
- 4.2.A — `Routes.razor` add `<AuthorizeRouteView>` + `<NotFound>`
- 4.2.B — Migrate `Pages/QuickSetup.razor` → `Components/Pages/QuickSetup.razor`
- 4.2.C — Cleanup NU1506 warnings
- **4.2.D — Antiforgery form-submit test** (moved from PR2)
  - Render `/accounting/revenue` form
  - Submit with `__RequestVerificationToken`
  - Verify 200 (not 400 antiforgery failure)
- **4.2.E — Serilog tuning** (if PR2 observation showed log volume regression)
- **4.2.F — SQLite concurrency load test** (future perf validation)
  - 5+ concurrent circuits, verify no SQLITE_BUSY

### 4.2 Pre-conditions
PR3 merged + specific user request per sub-change.

**Estimate:** 1–6 hours, optional.

---

## Cumulative Risk Matrix (V5)

| Risk | PR1 | PR2 | PR3 | PR4 |
|---|---|---|---|---|
| Wrong root cause | High → Gate A | — | — | — |
| Hosting model side-effects | — | Bounded → AB1–AB6, AB9, AB11 | — | — |
| Route conflict | — | Detected | Fixed (Gate C) | — |
| Static `_framework/*` break | — | AB11 | — | — |
| Memory/startup regression | — | AB3, AB9 | Re-check | — |
| Antiforgery (deferred) | — | — | — | 4.2.D |
| Log spam (deferred) | — | Warning only | — | 4.2.E |
| SQLite contention (deferred) | — | — | — | 4.2.F |
| Premature edits | Inspect-first | Gate B | Gate C | Per-change |

---

## Validation Tooling

**Endpoint inventory** (`scripts/endpoint-inventory.ps1`):
```powershell
param([string]$BaseUrl = "http://localhost:5010")
$urls = @(
    "/", "/login",
    "/accounting/balance", "/accounting/revenue",
    "/accounting/expense", "/accounting/transactions",
    "/counter", "/nonexistent",
    "/_framework/blazor.web.js"
)
foreach ($u in $urls) {
    $code = (curl.exe -s -o NUL -w "%{http_code}" "$BaseUrl$u")
    Write-Host "$code  $u"
}
```

**Minimal perf capture** (`scripts/perf-capture.ps1`):
```powershell
param([int]$Pid, [string]$Output)
Start-Sleep -Seconds 30
$p = Get-Process -Id $Pid
@(
    "WorkingSetMB: $([math]::Round($p.WorkingSet64/1MB,1))"
    "StartupSec: <fill from log timestamp>"
) | Out-File $Output
```

Locations:
- Diagnostics: `docs/diagnostics/`
- Artifacts: `artifacts/`
- Scripts: `scripts/`

---

## User Approval Snapshot (V5)

| PR | Status | Required action |
|---|---|---|
| PR1 | 🟢 Approve | None — V5 trimmed |
| PR2 | 🟡 Pending | Confirm AB7/8/10/12 dropped (V5 dropped them) |
| PR3 | 🟢 Approve | Gated by C1 |
| PR4 | 🟢 Approve | Per sub-change |

---

## Open Questions (V5)

| # | Question | Resolved at |
|---|---|---|
| Q1 | Is `TenantService` only DI-validation-reachable? | Gate A (A3) |
| Q2 | Rendering mode? | Gate B (B3) |
| Q3 | Cookie auth `TenantId` claim correct? | Gate B render flow |
| Q4 | SignalR conflict with `/_blazor`? | Gate B (B4) |
| Q5 | `AuthenticationStateProvider` already registered? | Gate A (A4) |
| Q6 | `_framework/*` reachable? | Gate B (B5) + AB11 |
| Q7 | Antiforgery compatible with Blazor forms? | **PR4 (4.2.D)** |
| Q8 | SQLite handles concurrent circuits? | **PR4 (4.2.F)** |
| Q9 | Serilog will not flood logs? | PR2 observation → PR4 (4.2.E) |

---

## Philosophy

**Prove app starts → prove routing works → cleanup → harden.**

Each PR has a single narrow goal. Production concerns (forms, load, log tuning) deferred until basics proven. No premature optimization, no premature hardening.
