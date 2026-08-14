# TASK CARD — Sprint 9: Type 3 JobMarket + /jobs Page (KhachLink Multi-Profile R3)

> **Status:** ⏳ PENDING
> **Priority:** P3 — R3 (after Sprint 8)
> **Branch:** `feature/khachlink-multi-profile-r3`
> **Mode:** IMPLEMENT

## Objective
Add JobMarket profile preset + create `/jobs.razor` page (wrapper reuse `/stores` + filter products by keyword) + enable in SystemAdmin UI + tests. R3 complete.

## Prerequisites
- [x] Sprint 8 complete (Logistics)
- [x] Build pass

## Task 1: JobMarket preset
**File:** `1_Shared/Domain/Aggregates/KhachLinkAggregate/KhachLinkNavFlags.cs`
- Update `ForProfile(JobMarket)` case:
  ```csharp
  KhachLinkProfile.JobMarket => new()
  {
      ShowCart = false, ShowOrders = false, ShowLoyaltyHistory = false,
      ShowMissions = false, ShowRewards = false, ShowAllianceWallet = false,
      ShowCampaigns = false, ShowScan = false, ShowQrClaim = false,
      ShowCommunity = false, ShowJobs = true,
      ShowStaffDashboard = false
      // ShowHome, ShowStores, ShowProfile = true (default)
  },
  ```
- Remove `// TODO R3` comment

## Task 2: /jobs.razor page
**File:** `5_WebApps/KhachLink/Pages/Jobs.razor`
- Reuse existing `/stores` component logic (or shared component)
- Filter products by keyword in name (case-insensitive contains): "job", "việc", "dịch vụ", "service"
- If no Product category/tag field exists → client-side filter on loaded products list
- OR query param `?filter=jobs` passed to existing products API (if supported)
- Nav: `ShowJobs` flag → `<NavLink href="/jobs">Sàn việc</NavLink>` (already wired in R1 NavMenu refactor, just hidden)

## Task 3: SystemAdmin UI enable
**File:** `5_WebApps/ShopERP/Pages/Admin/KhachLinkInstances.razor`
- Enable JobMarket in Profile dropdown (remove "R3" disabled tooltip)

## Task 4: Tests
**File:** `6_Tests/VanAn.Core.Tests/KhachLink/KhachLinkNavFlagsJobMarketTests.cs`
- `ForProfile(JobMarket)` = ShowHome/Stores/Profile/Jobs true, rest false
- Integration: create jobmarket instance → fetch by-domain → verify NavFlags
- `/jobs` page filter test (unit test on filter logic — products with "job" in name returned, others filtered)

## Validation
- [ ] `dotnet build VanAn.sln` 0 errors
- [ ] `guard-check.ps1` ALL PASSED
- [ ] `dotnet test` ALL PASS
- [ ] Manual: create jobmarket instance → truy cập → Home + Stores + Jobs + Profile
- [ ] Manual: /jobs page → list products có text "job/việc/dịch vụ" trong name
- [ ] Deploy + RV

## Files Modified (expected)
1. `1_Shared/Domain/Aggregates/KhachLinkAggregate/KhachLinkNavFlags.cs` — UPDATE JobMarket case
2. `5_WebApps/KhachLink/Pages/Jobs.razor` — NEW
3. `5_WebApps/ShopERP/Pages/Admin/KhachLinkInstances.razor` — ENABLE dropdown
4. `6_Tests/VanAn.Core.Tests/KhachLink/KhachLinkNavFlagsJobMarketTests.cs` — NEW

## R3 Complete Gate
- [ ] Build + tests PASS
- [ ] User approval → merge R3 → deploy → RV
- [ ] DONE — KhachLink Multi-Profile feature complete
