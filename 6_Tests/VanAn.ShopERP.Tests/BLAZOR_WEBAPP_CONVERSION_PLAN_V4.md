# Detailed Coding Plan V4: ShopERP Blazor Migration (4-PR Strategy + Extended Gates)

> **Workflow:** `test-refactor-workflow.md` → Step 4: Implementation Plan
> **Status:** AWAITING USER APPROVAL (PR1 only — PR2/3/4 not yet approvable)
> **Revision:** V4 — adds 5 gaps from V3 review (performance baseline, DB migration, logging diff, antiforgery, static files)
> **Estimate:** 0.5–2 days total

---

## 0. Changelog V3 → V4

V3 review found 5 gaps:
1. **Performance baseline incomplete** — only memory tracked, missing CPU + startup-time
2. **Database migration unclear** — `EnsureCreatedAsync` interaction with Blazor circuits unknown
3. **Logging diff missing** — Blazor circuits produce different log volume; Serilog config not verified
4. **Antiforgery compatibility missing** — current `AddAntiforgery` for Razor Pages, Blazor forms need verification
5. **Static files conflict missing** — `_framework/blazor.web.js` serving not verified

V4 additions:
- **Gate B5/B6/B7** for Antiforgery, Static files, CPU+startup baseline
- **PR1 Inspection Step 8** for Serilog config
- **PR2 AB7** abort condition for log spam (>5× baseline)
- **DB migration check** in Gate B

---

## Decision Gates (extended in V4)

### Gate A — before PR1 starts coding
Required artifacts in `docs/diagnostics/startup-diag-YYYYMMDD.md`:
- A1: Full unhandled exception stack trace from `dotnet run`
- A2: `TenantService` constructor signature + dependencies + lifetime
- A3: Consumer graph — every place `ITenantService` is injected, with consumer lifetime
- A4: DI dump for `AuthenticationStateProvider`, `IHttpContextAccessor`, `CascadingAuthenticationState`, Blazor-related
- A5: Scheme confirmation: Razor Pages only / `AddServerSideBlazor` / `AddRazorComponents` / hybrid
- **A6 (new):** Serilog sink config — current sinks, log level, structured logging on/off

**Without all 6 items, PR1 does not proceed.**

### Gate B — before PR2 starts coding
Required proof:
- B1: PR1 endpoint inventory baseline saved
- B2: `Components/App.razor` confirmed as Blazor root
- B3: Rendering mode decision documented (InteractiveServer / InteractiveAuto / Static SSR)
- B4: No SignalR hub conflicts with `/_blazor`
- **B5 (new):** Antiforgery compatibility verified
  - Inspect `builder.Services.AddAntiforgery(...)` config (line 162)
  - Confirm Blazor `@rendermode InteractiveServer` forms work with current `CookieSecurePolicy.SameAsRequest` + `SameSite.Lax`
  - Test: render a Blazor form, submit, verify no antiforgery validation failure
- **B6 (new):** Static files for `_framework/*` verified
  - `app.UseStaticFiles()` line 197 is before routing — OK
  - Confirm `_framework/blazor.web.js` accessible from `/Components/App.razor`'s base href
  - Test: `curl -i http://localhost:5010/_framework/blazor.web.js` → 200 with `application/javascript`
- **B7 (new):** Performance baseline captured (3 metrics, not just memory)
  - Memory: `WorkingSet64` after 30s warm-up
  - CPU: average % over 30s idle
  - Startup time: stopwatch from `dotnet run` to `Now listening`
  - Saved to `artifacts/perf-baseline-pr1-YYYYMMDD.txt`
- **B8 (new):** DB migration impact assessed
  - Confirm `EnsureCreatedAsync` (Program.cs line 175) runs once, not per-circuit
  - Confirm SQLite WAL mode (line 178) compatible with Blazor's concurrent circuit load
  - No schema change required by adding Blazor (verified by reading)

### Gate C — before PR3 starts coding
Required proof:
- C1: Route conflict actually observed (duplicate-route exception OR wrong page served)
- C2: List of routes that need cleanup, each with evidence

If C1 fails (no actual conflict), **PR3 is cancelled.**

---

## PR1 — Diagnose + Unblock Startup (Inspect-First, 8 steps)

### 1.1 Goal
`localhost:5010` listening. No fix committed until inspection completes.

### 1.2 Phase 1.A — Inspection (NO code changes)

**Steps 1–7:** unchanged from V3 (auth registrations, IHttpContextAccessor, provider class, TenantService constructor, consumers, stack trace, DI dump)

**Step 8 (new) — Serilog config audit**
```
grep -rn "UseSerilog\|WriteTo\|MinimumLevel\|Filter" 5_WebApps/ShopERP/Program.cs 5_WebApps/ShopERP/appsettings*.json
```
Document:
- Current sinks (Console / File / Seq)
- Minimum log level (Information / Warning / etc.)
- Any filters that already silence framework noise
- `LoggingConfig:EnableFileLogging` setting

**Why:** Blazor InteractiveServer creates one circuit per connected client → significantly more log events. Without a baseline, we can't detect log spam regression in PR2 (AB7).

### 1.3 Phase 1.B — Decision matrix
(Unchanged from V3 — Options α/β/γ/δ)

### 1.4 Phase 1.C — Implementation
(Unchanged from V3 — conditional, post-inspection)

### 1.5 Validation (PR1) — minimal scope

**Build:**
```
dotnet build 5_WebApps/ShopERP/VanAn.ShopERP.csproj
```

**Endpoint inventory (only 2 endpoints):**
```
curl -i -o nul -w "%{http_code}  /\n" http://localhost:5010/
curl -i -o nul -w "%{http_code}  /login\n" http://localhost:5010/login
```

**Performance baseline capture (new in V4):**
```powershell
# Start app, wait 30s, capture:
$p = Get-Process -Id <pid>
$startTime = ... # from log "Now listening" timestamp - process start
"Memory(MB): $([math]::Round($p.WorkingSet64/1MB,1))" | Tee-Object artifacts/perf-baseline-pr1-YYYYMMDD.txt -Append
"CPU(%): $($p.CPU)" | Tee-Object artifacts/perf-baseline-pr1-YYYYMMDD.txt -Append
"StartupSec: $startTime" | Tee-Object artifacts/perf-baseline-pr1-YYYYMMDD.txt -Append
```

**Log baseline capture (new in V4):**
```
# Run app for 60s with no traffic, count log lines:
wc -l shoperp.log > artifacts/log-baseline-pr1-YYYYMMDD.txt
```

**Bunit regression:**
```
dotnet test 6_Tests/VanAn.ShopERP.Tests/VanAn.ShopERP.Tests.csproj
```

### 1.6 PR1 Exit Criteria
- [ ] Gate A artifacts saved (6 items including A6 Serilog)
- [ ] App listens on `localhost:5010`
- [ ] `/` and `/login` return expected
- [ ] **Perf baseline saved** (`artifacts/perf-baseline-pr1-*.txt`)
- [ ] **Log baseline saved** (`artifacts/log-baseline-pr1-*.txt`)
- [ ] Diagnostic block reverted
- [ ] No Bunit regression

### 1.7 PR1 Files Changed
1. `docs/diagnostics/startup-diag-YYYYMMDD.md`
2. `artifacts/perf-baseline-pr1-YYYYMMDD.txt`
3. `artifacts/log-baseline-pr1-YYYYMMDD.txt`
4. (optional) `5_WebApps/ShopERP/Services/HttpContextAuthenticationStateProvider.cs`
5. `5_WebApps/ShopERP/Program.cs` — 1 line

**Estimate:** 3–5 hours (added 1 hour for baseline capture + Serilog audit).

---

## PR2 — Enable Blazor Routing (extended abort conditions)

### 2.1 Goal
`/accounting/*` reachable. Keep `MapFallbackToPage`. No `Home.razor` edits.

### 2.2 Pre-conditions
- PR1 merged
- Gate B passed (now 8 items: B1–B8)

### 2.3 Implementation
(Unchanged from V3)

### 2.4 Hard Abort Conditions (extended)

Abort PR2 immediately if:
- **AB1:** Duplicate-route exception during `app.Run()`
- **AB2:** Auth redirect loop > 3 hops
- **AB3:** Process memory after warm-up > PR1 baseline + 20%
- **AB4:** Bunit regression
- **AB5:** SignalR hub conflict
- **AB6:** Razor Page `/Login` no longer reachable
- **AB7 (new):** Log volume (60s no-traffic) > PR1 baseline × 5
- **AB8 (new):** CPU idle > PR1 baseline + 30%
- **AB9 (new):** Startup time > PR1 baseline + 50%
- **AB10 (new):** Antiforgery validation failure on Blazor form (e.g., `/accounting/revenue` submit)
- **AB11 (new):** Static file 404 for `_framework/blazor.web.js` or `_framework/blazor.server.js`
- **AB12 (new):** SQLite locking errors during first-circuit connection

### 2.5 Validation (PR2)

**Full endpoint inventory (V3 + new):**
```
/                          → 200 (Razor Page)
/login                     → 200 (Razor Page)
/accounting/balance        → 200/302 (Blazor)
/accounting/revenue        → 200/302
/accounting/expense        → 200/302
/accounting/transactions   → 200/302
/counter                   → 200
/nonexistent               → fallback to /Index
/_framework/blazor.web.js  → 200 application/javascript (new in V4)
```

**Auth flow:** unauth → 302 /login → login → 200 (unchanged)

**Antiforgery test (new in V4):**
```
# Render a Blazor form, capture __RequestVerificationToken
# Submit form, expect 200 (not 400 antiforgery failure)
```

**Performance comparison (new in V4):**
```
# Same captures as PR1, compare to artifacts/perf-baseline-pr1-*.txt
# Memory: must be < baseline × 1.20
# CPU: must be < baseline + 30%
# Startup: must be < baseline × 1.50
# Save to artifacts/perf-baseline-pr2-YYYYMMDD.txt
```

**Log volume comparison (new in V4):**
```
# 60s no-traffic run, count log lines
# Must be < PR1 baseline × 5
# Save to artifacts/log-baseline-pr2-YYYYMMDD.txt
```

**SQLite concurrency check (new in V4):**
```
# Open 5 concurrent Blazor circuits (5 browser tabs to /counter)
# Verify no SQLITE_BUSY errors in shoperp.log
```

**E2E smoke:** `.\run-accounting-full.bat` — ShopERP starts, E2E reaches Blazor pages.

### 2.6 PR2 Exit Criteria
- [ ] Gate B passed (B1–B8)
- [ ] Full endpoint inventory matches
- [ ] No abort AB1–AB12 triggered
- [ ] Auth flow works
- [ ] Antiforgery works on Blazor form
- [ ] Static `_framework/*` served
- [ ] Perf within thresholds
- [ ] Log volume within 5× baseline
- [ ] SQLite concurrent circuits OK
- [ ] No Bunit regression

### 2.7 PR2 Files Changed
1. `5_WebApps/ShopERP/Program.cs`
2. `artifacts/perf-baseline-pr2-YYYYMMDD.txt`
3. `artifacts/log-baseline-pr2-YYYYMMDD.txt`

**Estimate:** 6–10 hours (added 2 hours for AB7–AB12 validation).

---

## PR3 — Route Cleanup (narrow scope, unchanged from V3)

### 3.1 Goal
Resolve proven route conflicts only.

### 3.2 Pre-conditions
PR2 merged + Gate C passed.

### 3.3 Scope (in scope)
- 3.3.A — `Home.razor` route change OR delete
- 3.3.B — Remove `MapFallbackToPage("/Index")` if Blazor `<NotFound>` proven sufficient
- 3.3.C — Remove duplicate `AddScoped<CascadingAuthenticationState>()` (line 159) if redundant

### 3.4 Validation
- Full endpoint inventory (incl. `_framework/*`)
- Bunit regression
- E2E smoke
- Perf still within PR2 thresholds

### 3.5 PR3 Files Changed
- `5_WebApps/ShopERP/Components/Pages/Home.razor` (if 3.3.A)
- `5_WebApps/ShopERP/Program.cs` (if 3.3.B/C)

**Estimate:** 1–3 hours.

---

## PR4 — Optional Hardening (unchanged from V3)

### 4.1 Scope
- 4.2.A — `Routes.razor` add `<AuthorizeRouteView>` + `<NotFound>`
- 4.2.B — Migrate `Pages/QuickSetup.razor` → `Components/Pages/QuickSetup.razor`
- 4.2.C — Cleanup NU1506 warnings
- **4.2.D (new):** Tune Serilog filters if PR2 showed elevated log volume (even if under 5× threshold)

### 4.2 Pre-conditions
PR3 merged + specific user request per sub-change.

**Estimate:** 1–5 hours, optional.

---

## Cumulative Risk Matrix (extended)

| Risk | PR1 | PR2 | PR3 | PR4 |
|---|---|---|---|---|
| Wrong root cause | High → Gate A | — | — | — |
| Hosting model side-effects | — | High → AB1–AB12 | — | — |
| Route conflict | — | Detected | Fixed | — |
| **Antiforgery break (new)** | — | AB10 | — | — |
| **Static files break (new)** | — | AB11 | — | — |
| **Perf regression (new)** | — | AB3+AB8+AB9 | Re-check | — |
| **Log spam (new)** | — | AB7 | — | 4.2.D tune |
| **SQLite contention (new)** | — | AB12 | — | — |
| Premature edits | Inspect-first | Gate B | Gate C | Per-change |

---

## Validation Tooling (extended)

**Endpoint inventory script** (`scripts/endpoint-inventory.ps1`):
```powershell
param([string]$BaseUrl = "http://localhost:5010")
$urls = @(
    "/", "/login",
    "/accounting/balance", "/accounting/revenue",
    "/accounting/expense", "/accounting/transactions",
    "/counter", "/nonexistent",
    "/_framework/blazor.web.js"   # added in V4
)
foreach ($u in $urls) {
    $code = (curl.exe -s -o NUL -w "%{http_code}" "$BaseUrl$u")
    Write-Host "$code  $u"
}
```

**Perf capture script** (`scripts/perf-capture.ps1` — new in V4):
```powershell
param([int]$Pid, [string]$Output)
Start-Sleep -Seconds 30   # warm-up
$p = Get-Process -Id $Pid
@(
    "Memory(MB): $([math]::Round($p.WorkingSet64/1MB,1))"
    "CPU(s): $($p.CPU)"
    "Handles: $($p.HandleCount)"
    "Threads: $($p.Threads.Count)"
) | Out-File $Output
```

**Log baseline capture** (`scripts/log-baseline.ps1` — new in V4):
```powershell
param([string]$LogFile, [int]$Seconds = 60, [string]$Output)
$before = (Get-Content $LogFile -ErrorAction SilentlyContinue).Count
Start-Sleep -Seconds $Seconds
$after  = (Get-Content $LogFile).Count
"LogLinesPer60s: $($after - $before)" | Out-File $Output
```

**Locations:**
- Diagnostics: `docs/diagnostics/`
- Artifacts (perf, logs, inventories): `artifacts/`
- Scripts: `scripts/`

---

## User Decision Required

### PR1 (V4 changes)
- [ ] Add Step 8 Serilog audit — agreed?
- [ ] Add A6 (Serilog config) to Gate A — agreed?
- [ ] Capture perf + log baseline in PR1 validation — agreed?

### PR2 (V4 changes)
- [ ] Extend Gate B with B5/B6/B7/B8 — agreed?
- [ ] Extend abort conditions with AB7–AB12 — agreed?
- [ ] Add antiforgery/static-files/perf/log/SQLite checks — agreed?

### PR3 (unchanged)
- [ ] Narrow scope (gated by C1) — agreed?

### PR4 (V4 changes)
- [ ] Add 4.2.D (Serilog tuning) as optional — agreed?

**Status:** ⏸️ PAUSED — only PR1 currently approvable. PR2/3/4 reviewed after pre-conditions met.

---

## Appendix: Open Questions (extended)

| # | Question | Resolved at |
|---|---|---|
| Q1 | Is `TenantService` used in production paths or only DI-validation? | Gate A (A3) |
| Q2 | Rendering mode? | Gate B (B3) |
| Q3 | Cookie auth `TenantId` claim correct? | Gate B auth flow |
| Q4 | SignalR conflict with `/_blazor`? | Gate B (B4) |
| Q5 | `AuthenticationStateProvider` already registered? | Gate A (A4) |
| **Q6 (new)** | Antiforgery cookie policy compatible with Blazor forms? | Gate B (B5) |
| **Q7 (new)** | `_framework/*` static assets served correctly? | Gate B (B6) |
| **Q8 (new)** | Perf impact of Blazor circuits acceptable? | Gate B (B7) + AB3/8/9 |
| **Q9 (new)** | SQLite WAL handles concurrent Blazor circuits? | Gate B (B8) + AB12 |
| **Q10 (new)** | Serilog config will not flood logs with circuit events? | Gate A (A6) + AB7 |
