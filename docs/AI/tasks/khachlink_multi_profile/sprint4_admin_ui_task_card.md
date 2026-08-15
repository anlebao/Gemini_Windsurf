# TASK CARD — Sprint 4: SystemAdmin UI (KhachLink Multi-Profile R1)

> **Status:** ✅ COMPLETE (merged `e2d4bece` → `5047ed8c`)
> **Priority:** P1 — After Sprint 3 approval
> **Branch:** `feature/khachlink-multi-profile-r1`
> **Mode:** IMPLEMENT (UI Phase)

## Objective
Create ShopERP admin page `/admin/khachlink-instances` + API client for SystemAdmin to create/edit/deactivate KhachLinkInstance with profile preset + nav flag toggle grid.

## Prerequisites
- [x] Sprint 3 complete (KhachLink runtime)
- [x] Build pass

## Task 1: KhachLinkInstanceApiClient
**File:** `5_WebApps/ShopERP/Services/ApiClients/KhachLinkInstanceApiClient.cs`
- Inject `HttpClient` (Gateway client)
- `GetAllAsync()` → `IReadOnlyList<KhachLinkInstanceDto>`
- `CreateAsync(CreateKhachLinkInstanceRequest)` → `KhachLinkInstanceDto`
- `UpdateAsync(Guid id, UpdateKhachLinkInstanceRequest)` → `KhachLinkInstanceDto`
- `DeactivateAsync(Guid id)` → void
- Handle errors (throw on non-success status)

## Task 2: Admin page
**File:** `5_WebApps/ShopERP/Pages/Admin/KhachLinkInstances.razor` (+ `.razor.cs`)
- Table list: Label, Profile, CustomDomain, OwnerTenant (name or "Platform"), IsActive, actions
- Create/Edit modal (use UI Platform components — Gate 5):
  - Label input
  - Profile dropdown (FullCommerce + Directory enabled; Logistics/JobMarket/Reseller disabled with "R3/R2" tooltip)
  - CustomDomain input (validate format + unique)
  - OwnerTenant dropdown (nullable — "Platform (no tenant)" + list of tenants)
  - Nav flags checkbox grid (15 toggles) — auto-loaded from Profile preset, SystemAdmin can override individual
  - "Apply Profile Preset" button — reset nav flags to preset
- Deactivate button (soft delete confirmation)
- Use `[Authorize(Policy = "SystemAdmin, Bearer")]` on page

## Task 3: NavMenu link
**File:** `5_WebApps/ShopERP/Components/Layout/NavMenu.razor` (ShopERP, not KhachLink)
- Add link to `/admin/khachlink-instances` in admin section (SystemAdmin only)

## Task 4: DI Register
**File:** `5_WebApps/ShopERP/Program.cs`
- `services.AddScoped<KhachLinkInstanceApiClient>();`

## Validation
- [ ] `dotnet build VanAn.sln` 0 errors
- [ ] `guard-check.ps1` ALL PASSED
- [ ] Manual: create FullCommerce instance → list shows it
- [ ] Manual: create Directory instance → nav flags auto-set (Home/Stores/Profile true, rest false)
- [ ] Manual: edit nav flags individually → override preset
- [ ] Manual: deactivate → IsActive=false in list
- [ ] UI Platform components used (Gate 5)

## Files Modified (expected)
1. `5_WebApps/ShopERP/Services/ApiClients/KhachLinkInstanceApiClient.cs` — NEW
2. `5_WebApps/ShopERP/Pages/Admin/KhachLinkInstances.razor` — NEW
3. `5_WebApps/ShopERP/Pages/Admin/KhachLinkInstances.razor.cs` — NEW (code-behind)
4. `5_WebApps/ShopERP/Components/Layout/NavMenu.razor` — ADD admin link
5. `5_WebApps/ShopERP/Program.cs` — ADD DI

## Approval Gate
- [ ] Build pass
- [ ] User approval before Sprint 5
