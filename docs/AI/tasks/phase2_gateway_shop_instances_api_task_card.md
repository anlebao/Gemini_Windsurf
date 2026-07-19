# Task Card: Phase 2 — Gateway ShopInstances API

> **Master plan:** `gateway_router_multi_vps_master_plan.md`
> **Workflow:** `newfeaturebuild.md`
> **Phase:** 2 of 7
> **Depends on:** Phase 1 (ShopInstance entity + migration)
> **Unlocks:** Phase 3 (Gateway router uses ShopInstance lookup)

---

## 1. Use Case & Business Design

**Problem:** After Phase 1, the `ShopInstances` table exists in PG but there is no API to manage it. SysAdmin cannot create/edit/disable ShopERP instances. Gateway router (Phase 3) has no way to query instances either (it could query DbContext directly, but a service layer is cleaner + testable).

**Goal:** Build CRUD API + service layer + health check for `ShopInstance` on Gateway. Admin-only authorization (SystemAdmin role).

**Out of scope:** Admin UI page (Phase 6), Gateway router checkout logic (Phase 3).

---

## 2. Reverse Impact Analysis

### Service Layer (`3_CoreHub/Services/`)
- **NEW: `IShopInstanceService.cs`** — interface:
  - `Task<ShopInstance> CreateAsync(string baseUrl, string label, int maxTenants, string? healthCheckUrl, CancellationToken ct)`
  - `Task<ShopInstance?> GetByIdAsync(Guid id, CancellationToken ct)`
  - `Task<List<ShopInstance>> GetAllAsync(CancellationToken ct)`
  - `Task<List<ShopInstance>> GetActiveAsync(CancellationToken ct)`
  - `Task<bool> UpdateAsync(Guid id, string label, int maxTenants, CancellationToken ct)`
  - `Task<bool> SetActiveAsync(Guid id, bool isActive, CancellationToken ct)`
  - `Task<HealthCheckResult> CheckHealthAsync(Guid id, CancellationToken ct)` — pings `HealthCheckUrl` (or `BaseUrl/health`), updates `HealthStatus` + `LastHealthCheck`.
  - `Task<int> CountTenantsAsync(Guid shopInstanceId, CancellationToken ct)` — for capacity display.
- **NEW: `ShopInstanceService.cs`** — implementation using `IVanAnDbContext`.
  - Validates: BaseUrl format (Uri.TryCreate), unique BaseUrl (query existing), MaxTenants >= 1.
  - Health check: HttpClient with 3s timeout, GET `{baseUrl}/health` (or `HealthCheckUrl` if set), parse response. Update entity. SaveChanges.
- **NEW: `HealthCheckResult.cs`** (DTO): `Status` (Healthy/Degraded/Down), `LatencyMs`, `CheckedAt`, `ErrorMessage`.

### API Layer (`2_Gateway/Controllers/`)
- **NEW: `ShopInstancesController.cs`** — REST CRUD:
  - `POST /api/v1/shop-instances` — create (SystemAdmin)
  - `GET /api/v1/shop-instances` — list all (SystemAdmin)
  - `GET /api/v1/shop-instances/{id}` — get by id (SystemAdmin)
  - `PUT /api/v1/shop-instances/{id}` — update label/maxTenants (SystemAdmin)
  - `PUT /api/v1/shop-instances/{id}/activate` — set IsActive=true (SystemAdmin)
  - `PUT /api/v1/shop-instances/{id}/deactivate` — set IsActive=false (SystemAdmin)
  - `POST /api/v1/shop-instances/{id}/health-check` — trigger health check (SystemAdmin)
  - All endpoints `[Authorize(Policy = "SystemAdmin")]`.

### DI Registration (`2_Gateway/Program.cs`)
- `builder.Services.AddScoped<IShopInstanceService, ShopInstanceService>();`
- `builder.Services.AddHttpClient<IShopInstanceService, ShopInstanceService>();` (for health check HTTP calls).

### DTOs (`2_Gateway/Controllers/ShopInstancesController.cs` or separate DTO file)
- `CreateShopInstanceRequest` { BaseUrl, Label, MaxTenants, HealthCheckUrl }
- `UpdateShopInstanceRequest` { Label, MaxTenants }
- `ShopInstanceDto` { Id, BaseUrl, Label, MaxTenants, IsActive, HealthCheckUrl, LastHealthCheck, HealthStatus, TenantCount, CreatedAt }
- `HealthCheckResultDto` { Status, LatencyMs, CheckedAt, ErrorMessage }

### Tests
- **NEW: `6_Tests/VanAn.Core.Tests/Services/ShopInstanceServiceTests.cs`** — unit tests with in-memory DbContext or mocked IVanAnDbContext:
  - `CreateAsync_WithValidInput_CreatesInstance`
  - `CreateAsync_WithDuplicateBaseUrl_Throws`
  - `CreateAsync_WithInvalidUrl_Throws`
  - `UpdateAsync_WithNonExistentId_ReturnsFalse`
  - `SetActiveAsync_TogglesFlag`
  - `CheckHealthAsync_WithHealthyEndpoint_UpdatesStatus`
  - `CheckHealthAsync_WithUnreachableEndpoint_SetsDownStatus`
  - `CountTenantsAsync_ReturnsCorrectCount`
- **NEW: `6_Tests/VanAn.Integration.Tests/ShopInstancesControllerTests.cs`** — integration tests with TestServer:
  - `POST_Create_AsSystemAdmin_Returns201`
  - `POST_Create_AsAnonymous_Returns401`
  - `GET_List_ReturnsAllInstances`
  - `POST_HealthCheck_UpdatesLastHealthCheck`

### TDD Plan
1. Write failing unit tests for `ShopInstanceService`.
2. Implement service → tests pass.
3. Write failing integration tests for `ShopInstancesController`.
4. Implement controller + DI → tests pass.
5. Manual smoke test: run Gateway locally, `curl -H "Authorization: Bearer {sysadmin_jwt}" -X POST https://localhost:5001/api/v1/shop-instances -d '{...}'`.

---

## 3. Detailed Coding Plan

### Namespace Strategy
- `VanAn.CoreHub.Services` (IShopInstanceService, ShopInstanceService, HealthCheckResult)
- `VanAn.Gateway.Controllers` (ShopInstancesController, DTOs)
- `VanAn.Core.Tests.Services` (unit tests)
- `VanAn.Integration.Tests` (integration tests)

### Implementation Steps
**Step 1 — Service interface + DTOs (2 files):**
- `IShopInstanceService.cs` + `HealthCheckResult.cs`.
- Build → 0 errors.

**Step 2 — Service tests (1 file):**
- `ShopInstanceServiceTests.cs` with mocked IVanAnDbContext (use `Microsoft.EntityFrameworkCore.InMemory` or Moq).
- Run → all fail (service not implemented).

**Step 3 — Service implementation (1 file):**
- `ShopInstanceService.cs`.
- Run tests → all pass.

**Step 4 — Controller + DI (2 files):**
- `ShopInstancesController.cs` with all DTOs.
- Update `2_Gateway/Program.cs` DI registration.
- Build → 0 errors.

**Step 5 — Integration tests (1 file):**
- `ShopInstancesControllerTests.cs` with `WebApplicationFactory<GatewayProgram>`.
- Use existing test infrastructure (look at `6_Tests/VanAn.Integration.Tests/GuestCheckoutEndpointTests.cs` for pattern).
- Run → all pass.

**Step 6 — Manual smoke (local):**
- Start Gateway.
- Mint a SystemAdmin JWT (existing pattern from `TenantOnboardingApiClient.MintSystemAdminTokenAsync`).
- curl POST create, GET list, POST health-check.
- Verify PG row created + `HealthStatus = "Healthy"` after health check (Gateway is running, `http://shoperp:5003/health` should respond).

### Active Skills
- `domain-integrity-validation` (uses ShopInstance from Phase 1 — verify Single-Identity compliance)
- `accounting-ui-implementation` (NOT applicable — no UI in this phase)

---

## 4. Validation Gates

| Gate | Command | Expected |
|---|---|---|
| Build | `dotnet build VanAn.sln` | 0 errors |
| Unit tests | `dotnet test 6_Tests/VanAn.Core.Tests --filter ShopInstance` | All pass |
| Integration tests | `dotnet test 6_Tests/VanAn.Integration.Tests --filter ShopInstances` | All pass |
| Guard check | `./guard-check.ps1` | PASS |
| Manual smoke | curl create + health-check | 201 + Healthy status |

---

## 5. Deliverables

- New: `3_CoreHub/Services/IShopInstanceService.cs`
- New: `3_CoreHub/Services/ShopInstanceService.cs`
- New: `3_CoreHub/Services/HealthCheckResult.cs`
- New: `2_Gateway/Controllers/ShopInstancesController.cs`
- Modified: `2_Gateway/Program.cs` (DI)
- New: `6_Tests/VanAn.Core.Tests/Services/ShopInstanceServiceTests.cs`
- New: `6_Tests/VanAn.Integration.Tests/ShopInstancesControllerTests.cs`

---

## 6. Approval Gate

No domain modification in this phase. Standard IMPLEMENT approval (user confirms task card, execution begins).
