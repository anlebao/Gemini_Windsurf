# TASK CARD — Sprint 2: Gateway API (KhachLink Multi-Profile R1)

> **Status:** ✅ COMPLETE (merged `41a8994b` + `398610f9` → `5047ed8c`)
> **Priority:** P1 — After Sprint 1 approval
> **Branch:** `feature/khachlink-multi-profile-r1`
> **Mode:** IMPLEMENT (Application Phase)

## Objective
Create Repository + Service + DTOs + Controller (6 endpoints) for KhachLinkInstance CRUD + by-domain public lookup. DI register. Feature flag check.

## Prerequisites
- [x] Sprint 1 complete (Domain + EF config + migration)
- [x] Build pass

## Task 1: Repository
**Files:** `3_CoreHub/Repositories/KhachLinkInstanceRepository.cs` + `IKhachLinkInstanceRepository.cs`
- `GetByIdAsync(Guid id)` — `IgnoreQueryFilters()` (platform entity)
- `GetByDomainAsync(string domain)` — `IgnoreQueryFilters()`, lowercase compare
- `GetAllAsync()` — `IgnoreQueryFilters()`, return all active
- `AddAsync(KhachLinkInstance)`
- `UpdateAsync(KhachLinkInstance)`
- `DeactivateAsync(Guid id)` — set IsActive=false

## Task 2: Service
**Files:** `3_CoreHub/Services/KhachLinkInstanceService.cs` + `IKhachLinkInstanceService.cs`
- `GetByIdAsync`, `GetByDomainAsync`, `GetAllAsync` — delegate to repo
- `CreateAsync(CreateKhachLinkInstanceRequest)` — validate unique CustomDomain (throw if dup), apply ForProfile preset if NavFlags null
- `UpdateAsync(Guid id, UpdateKhachLinkInstanceRequest)` — update profile + nav flags
- `DeactivateAsync(Guid id)` — soft delete
- Inject `IConfiguration` for feature flag check: `_config["KhachLink:MultiProfileEnabled"]`

## Task 3: DTOs
**File:** `2_Gateway/DTOs/KhachLinkInstanceDto.cs`
- `KhachLinkInstanceDto` (response): Id, Label, Profile, CustomDomain, OwnerTenantId, OwnerTenantName?, NavFlags, IsActive, CreatedAt, UpdatedAt
- `KhachLinkNavFlagsDto`: 15 bool properties
- `CreateKhachLinkInstanceRequest`: Label, Profile, CustomDomain, OwnerTenantId?, NavFlagsOverride?
- `UpdateKhachLinkInstanceRequest`: Profile, NavFlags (always explicit)

## Task 4: Controller
**File:** `2_Gateway/Controllers/KhachLinkInstanceController.cs`
- Route: `api/v1/khachlink-instances`
- `[Authorize(Policy = "SystemAdmin, Bearer")]` on all except by-domain
- Endpoints:
  - `GET /` — list all
  - `GET /{id}` — get by ID
  - `GET /by-domain/{domain}` — `[AllowAnonymous]`, public lookup. If flag OFF → return 404
  - `POST /` — create (validate model, return 201)
  - `PUT /{id}` — update
  - `DELETE /{id}` — deactivate (return 204)
- **Pattern #10:** Strip charset from `Request.ContentType` if forwarding (known pattern registry)

## Task 5: DI Register
**File:** `2_Gateway/Program.cs`
- `services.AddScoped<IKhachLinkInstanceRepository, KhachLinkInstanceRepository>();`
- `services.AddScoped<IKhachLinkInstanceService, KhachLinkInstanceService>();`

## Task 6: Feature flag
**File:** `2_Gateway/appsettings.json`
```json
"KhachLink": {
  "MultiProfileEnabled": false
}
```

## Validation
- [ ] `dotnet build VanAn.sln` 0 errors
- [ ] `guard-check.ps1` ALL PASSED
- [ ] Manual test: `curl GET /api/v1/khachlink-instances/by-domain/diemthuong2.khachvip.online` → 404 (flag OFF)
- [ ] Manual test: toggle flag ON → `curl GET /api/v1/khachlink-instances/by-domain/diemthuong2.khachvip.online` → 200 with seed instance

## Files Modified (expected)
1. `3_CoreHub/Repositories/KhachLinkInstanceRepository.cs` + interface — NEW
2. `3_CoreHub/Services/KhachLinkInstanceService.cs` + interface — NEW
3. `2_Gateway/DTOs/KhachLinkInstanceDto.cs` — NEW
4. `2_Gateway/Controllers/KhachLinkInstanceController.cs` — NEW
5. `2_Gateway/Program.cs` — ADD DI
6. `2_Gateway/appsettings.json` — ADD feature flag

## Approval Gate
- [ ] Build pass
- [ ] User approval before Sprint 3
