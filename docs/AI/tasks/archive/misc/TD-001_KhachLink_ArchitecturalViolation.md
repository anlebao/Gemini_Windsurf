# TD-001: KhachLink — Architectural Violation (Direct CoreHub Service Injection)

**Type:** Technical Debt — Architectural Violation  
**Severity:** High (blocks E2E CI, pre-existing, pre-Wave 5)  
**Discovered:** Wave 5 / E2E test run (2026-06-23)  
**Status:** Open — scheduled for Wave 6 or dedicated TD sprint  
**Branch to target:** `feature/td-001-khachlink-http-refactor`

---

## Problem Statement

`KhachLink` (port 5002) is a **Client UI layer** that should communicate exclusively via
`HttpClient → Gateway (5001)`. Instead, it directly injects server-side `CoreHub` services
that carry a full DB/repository dependency chain, violating the architectural contract:

```
REQUIRED:  KhachLink (5002) → HttpClient → Gateway (5001) → ShopERP (5003) → SQLite
ACTUAL:    KhachLink (5002) → IOrderWorkflowService → IOrderRepository → IVanAnDbContext → SQLite
```

This causes `InvalidOperationException` at startup (DI validation fails: unable to resolve
`IOrderRepository`, `ISocialCampaignRepository`, `ISystemMetricsRepository`,
`ILoyaltyRewardsService`, etc.) because KhachLink has no `IVanAnDbContext` registration.

### Violating registrations in `5_WebApps/KhachLink/Program.cs`

| Service | Requires (transitively) |
|---|---|
| `IOrderWorkflowService` → `OrderWorkflowService` | `IOrderRepository`, `ILoyaltyRewardsService` |
| `ISocialCampaignService` → `SocialCampaignService` | `ISocialCampaignRepository` |
| `IDashboardService` → `DashboardService` | `ISystemMetricsRepository` |

### What NOT to do
- ❌ Add `IVanAnDbContext` / `KhachLinkDbContext` to KhachLink (duplicate infra, TX scope conflict)
- ❌ Copy-paste `ShopERPDbContext` with a new class name
- ❌ Register repositories in KhachLink chasing the full dependency chain

---

## Solution Plan (Option B — Correct Architectural Fix)

### Phase 1: Create Gateway endpoints for each violating service (1–2h)

For each service that KhachLink calls directly, ensure a corresponding REST endpoint exists
in `ShopERP` (or `Gateway`):

| Need | Existing endpoint? | Action |
|---|---|---|
| Order workflow state/actions | `ShopERP /api/orders/*` | Verify coverage |
| Social campaign list/create | `ShopERP /api/social-campaigns` | Add if missing |
| Dashboard metrics | `ShopERP /api/dashboard` | Add if missing |

### Phase 2: Create HTTP client service wrappers in KhachLink (2–3h)

Create thin HTTP wrappers under `5_WebApps/KhachLink/Services/Http/`:

```
KhachLink/Services/Http/
  OrderWorkflowHttpService.cs        implements IOrderWorkflowService (or new interface)
  SocialCampaignHttpService.cs       implements ISocialCampaignService (or new interface)  
  DashboardHttpService.cs            implements IDashboardService (or new interface)
```

Each wrapper uses the existing `"gateway"` named `HttpClient` (already registered):

```csharp
// Example: DashboardHttpService.cs
public class DashboardHttpService(IHttpClientFactory factory) : IDashboardService
{
    private readonly HttpClient _http = factory.CreateClient("gateway");

    public async Task<DashboardSummary> GetSummaryAsync(Guid tenantId, ...)
        => await _http.GetFromJsonAsync<DashboardSummary>($"/api/dashboard/{tenantId}");
    // ... other methods
}
```

### Phase 3: Replace registrations in KhachLink/Program.cs (30min)

```csharp
// REMOVE (violating):
_ = builder.Services.AddScoped<IOrderWorkflowService, OrderWorkflowService>();
_ = builder.Services.AddScoped<ISocialCampaignService, SocialCampaignService>();
_ = builder.Services.AddScoped<IDashboardService, DashboardService>();

// ADD (correct):
_ = builder.Services.AddScoped<IOrderWorkflowService, OrderWorkflowHttpService>();
_ = builder.Services.AddScoped<ISocialCampaignService, SocialCampaignHttpService>();
_ = builder.Services.AddScoped<IDashboardService, DashboardHttpService>();
```

### Phase 4: Update E2E tests if any assertions changed (30min)

Verify Playwright tests that exercise KhachLink dashboard/order flows still pass.

### Phase 5: Validate (CI full pipeline)

```
dotnet build VanAn.sln            → 0 errors
ci-full.ps1 -SkipInfra            → E2E PASS (KhachLink starts clean)
```

---

## Acceptance Criteria

- [ ] KhachLink starts without DI validation errors
- [ ] No `IVanAnDbContext` or repository types registered in KhachLink DI
- [ ] All KhachLink data access goes through `HttpClient → Gateway`
- [ ] `ci-full.ps1` E2E step passes (ShopERP 5003 + KhachLink 5002 both health-check OK)
- [ ] Architecture tests still pass (11/11)

---

## Affected Files

- `5_WebApps/KhachLink/Program.cs` — remove violating registrations
- `5_WebApps/KhachLink/Services/Http/*.cs` — new HTTP wrappers (create)
- `5_WebApps/ShopERP/Controllers/DashboardController.cs` — add if missing
- `5_WebApps/ShopERP/Controllers/SocialCampaignController.cs` — add if missing
- `2_Gateway/` — ensure routes forward `/api/dashboard`, `/api/social-campaigns`

---

## Notes

- `IShopConfigService`, `IOnboardingService`, `IVoiceCommandService` may also need
  similar treatment — verify their dependency chains.
- The `"gateway"` HttpClient is already registered in KhachLink (`Program.cs` line ~93).
- Pre-existing issue: this bug existed before Wave 5 and was hidden because Docker/E2E was
  not running locally.
