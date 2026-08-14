# TASK CARD — Sprint 3: KhachLink Runtime (KhachLink Multi-Profile R1)

> **Status:** ⏳ PENDING
> **Priority:** P1 — After Sprint 2 approval
> **Branch:** `feature/khachlink-multi-profile-r1`
> **Mode:** IMPLEMENT (UI Phase)

## Objective
Refactor NavMenu.razor (15 hardcoded items → flag-driven) + KhachLinkLayout.razor (fetch instance config + cascade NavFlags + header icons flag-driven) + create InstanceHttpService.

## Prerequisites
- [x] Sprint 2 complete (Gateway API)
- [x] Build pass

## Task 1: KhachLinkInstanceHttpService
**File:** `5_WebApps/KhachLink/Services/Http/KhachLinkInstanceHttpService.cs`
- Inject `IHttpClientFactory` (client "gateway") + `ILogger`
- `GetByCurrentDomainAsync(IJSRuntime js)`:
  - Read `window.location.hostname` via JS interop
  - GET `api/v1/khachlink-instances/by-domain/{hostname}`
  - Return `KhachLinkInstanceConfig?` (null if 404 or error)
- `KhachLinkInstanceConfig` model: Profile, OwnerTenantId?, NavFlags (KhachLinkNavFlagsDto)
- Cache in localStorage (TTL 5 min) — key `khachlink_instance_config`

## Task 2: KhachLinkInstanceConfig + KhachLinkNavFlagsDto models
**File:** `5_WebApps/KhachLink/Models/KhachLinkInstanceConfig.cs`
- `KhachLinkInstanceConfig` record (Profile enum, OwnerTenantId?, NavFlags)
- `KhachLinkNavFlagsDto` record (15 bool properties, default all true)

## Task 3: KhachLinkLayout.razor refactor
**File:** `5_WebApps/KhachLink/Components/Layout/KhachLinkLayout.razor`
- OnInitializedAsync:
  - Read feature flag (from config or API) — if OFF → `_navFlags = new()` (all true), skip fetch
  - If ON: `_instanceConfig = await InstanceHttp.GetByCurrentDomainAsync(JSRuntime)`
  - If `_instanceConfig != null`: `_navFlags = _instanceConfig.NavFlags`; if OwnerTenantId != null → `TenantService.SetCurrentTenant(owner)`
  - If null: `_navFlags = new()` (FullCommerce default fallback)
- Wrap NavMenu in `<CascadingValue Value="_navFlags" Name="NavFlags">`
- Header icons (cart, rewards, missions, loyalty history, profile) — wrap each in `@if (_navFlags.ShowXxx)`

## Task 4: NavMenu.razor refactor
**File:** `5_WebApps/KhachLink/Components/Layout/NavMenu.razor`
- Add `[CascadingParameter(Name = "NavFlags")] private KhachLinkNavFlagsDto _navFlags = new();` (default all true)
- Wrap each of 15 desktop nav items in `@if (_navFlags.ShowXxx)`:
  - ShowHome → Trang chủ
  - ShowCart → Giỏ hàng
  - ShowOrders → Đơn hàng
  - ShowLoyaltyHistory → Lịch sử tích điểm
  - ShowMissions → Nhiệm vụ
  - ShowRewards → Đổi điểm
  - ShowAllianceWallet → Ví liên minh (existing `_isAllianceMode` AND flag)
  - ShowStores → Cửa hàng
  - ShowCampaigns → Khuyến mãi
  - ShowScan → Quét QR
  - ShowQrClaim → QR gửi xe
  - ShowCommunity → Community tabs (existing `_isShipper/_isSalesman/_isShopOwner` AND flag)
  - ShowProfile → Tài khoản
  - ShowStaffDashboard → Dashboard (existing `_isStaff` AND flag)
  - ShowJobs → Sàn việc (R3 — link to /jobs, hidden in R1)
- Mobile bottom nav: same — wrap each `<a>` in `@if (_navFlags.ShowXxx)`
- **Keep existing role-based `@if` INSIDE flag check** (flag AND role — both must be true)

## Task 5: DI Register
**File:** `5_WebApps/KhachLink/Program.cs`
- `services.AddScoped<KhachLinkInstanceHttpService>();`

## Validation
- [ ] `dotnet build VanAn.sln` 0 errors
- [ ] `guard-check.ps1` ALL PASSED
- [ ] Manual: flag OFF → existing domain (diemthuong2) → NavMenu unchanged (all true default)
- [ ] Manual: flag ON + seed instance → existing domain → NavMenu all true (FullCommerce)
- [ ] Manual: flag ON + create Directory instance + point domain → NavMenu only Home/Stores/Profile

## Files Modified (expected)
1. `5_WebApps/KhachLink/Services/Http/KhachLinkInstanceHttpService.cs` — NEW
2. `5_WebApps/KhachLink/Models/KhachLinkInstanceConfig.cs` — NEW
3. `5_WebApps/KhachLink/Components/Layout/KhachLinkLayout.razor` — REFACTOR
4. `5_WebApps/KhachLink/Components/Layout/NavMenu.razor` — REFACTOR
5. `5_WebApps/KhachLink/Program.cs` — ADD DI

## Approval Gate
- [ ] Build pass
- [ ] User approval before Sprint 4
