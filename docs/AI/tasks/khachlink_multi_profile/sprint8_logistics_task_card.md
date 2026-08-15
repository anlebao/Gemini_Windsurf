# TASK CARD — Sprint 8: Type 2 Logistics (KhachLink Multi-Profile R3)

> **Status:** ⏳ PENDING
> **Priority:** P3 — R3 (after R2 merge)
> **Branch:** `feature/khachlink-multi-profile-r3` (from main after R2 merge)
> **Mode:** IMPLEMENT

## Objective
Add Logistics profile preset to `KhachLinkNavFlags.ForProfile()` + enable in SystemAdmin UI + verify Community Commerce reuse + tests.

## Prerequisites
- [x] R2 merged + deployed + RV pass

## Task 1: Logistics preset
**File:** `1_Shared/Domain/Aggregates/KhachLinkAggregate/KhachLinkNavFlags.cs`
- Update `ForProfile(Logistics)` case:
  ```csharp
  KhachLinkProfile.Logistics => new()
  {
      ShowCart = false, ShowOrders = false, ShowLoyaltyHistory = false,
      ShowMissions = false, ShowRewards = false, ShowAllianceWallet = false,
      ShowCampaigns = false, ShowScan = false, ShowQrClaim = false,
      ShowCommunity = true,   // shipper/shop owner community
      ShowStaffDashboard = false
      // ShowHome, ShowStores, ShowProfile = true (default)
  },
  ```
- Remove `// TODO R3` comment

## Task 2: SystemAdmin UI enable
**File:** `5_WebApps/ShopERP/Pages/Admin/KhachLinkInstances.razor`
- Enable Logistics in Profile dropdown (remove "R3" disabled tooltip)

## Task 3: Community Commerce verify
- Logistics instance → `ShowCommunity=true` → community nav items visible
- Existing role-based `@if (_isShipper/_isSalesman/_isShopOwner)` inside `@if (_navFlags.ShowCommunity)` — both must be true
- Community pages (`/community/nearby-orders`, `/community/active-deliveries`, `/community/wallet`) render correctly
- **No code change needed** — reuse Community Commerce (Sprint 4-7 cũ). Verify only.

## Task 4: Tests
**File:** `6_Tests/VanAn.Core.Tests/KhachLink/KhachLinkNavFlagsLogisticsTests.cs`
- `ForProfile(Logistics)` = ShowHome/Stores/Profile/Community true, rest false
- Integration: create logistics instance → fetch by-domain → verify NavFlags

## Validation
- [ ] `dotnet build VanAn.sln` 0 errors
- [ ] `guard-check.ps1` ALL PASSED
- [ ] `dotnet test` ALL PASS

## Files Modified (expected)
1. `1_Shared/Domain/Aggregates/KhachLinkAggregate/KhachLinkNavFlags.cs` — UPDATE Logistics case
2. `5_WebApps/ShopERP/Pages/Admin/KhachLinkInstances.razor` — ENABLE dropdown
3. `6_Tests/VanAn.Core.Tests/KhachLink/KhachLinkNavFlagsLogisticsTests.cs` — NEW
