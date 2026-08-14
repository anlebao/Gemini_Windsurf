# TASK CARD — Sprint 7: Type 5 Reseller (KhachLink Multi-Profile R2)

> **Status:** ⏳ PENDING
> **Priority:** P2 — R2 (after R1 merge)
> **Branch:** `feature/khachlink-multi-profile-r2` (from main after R1 merge)
> **Mode:** IMPLEMENT

## Objective
Add Reseller profile preset to `KhachLinkNavFlags.ForProfile()` + enable Reseller option in SystemAdmin UI + verify CommerceMode.Reseller integration + tests.

## Prerequisites
- [x] R1 merged + deployed + RV pass
- [x] Feature flag ON in production

## Task 1: Reseller preset
**File:** `1_Shared/Domain/Aggregates/KhachLinkAggregate/KhachLinkNavFlags.cs`
- Update `ForProfile(Reseller)` case: return `new KhachLinkNavFlags()` (all true) — full commerce + reseller extensions
- Remove `// TODO R2` comment

## Task 2: SystemAdmin UI enable
**File:** `5_WebApps/ShopERP/Pages/Admin/KhachLinkInstances.razor`
- Enable Reseller in Profile dropdown (remove "R2" disabled tooltip)

## Task 3: CommerceMode integration verify
- KhachLinkInstance profile=Reseller + OwnerTenantId=reseller → tenant context = reseller (KhachLinkLayout sets TenantService)
- Orders created on reseller instance → snapshot `CommerceMode.Reseller` (existing flow via `TenantSettings.CommerceModeOverride` or `GlobalCommerceMode`)
- **No code change needed** — existing CommerceModeService handles this. Verify only.

## Task 4: Tests
**File:** `6_Tests/VanAn.Core.Tests/KhachLink/KhachLinkNavFlagsResellerTests.cs`
- `ForProfile(Reseller)` = all 15 true
- Integration: create reseller instance → fetch by-domain → verify NavFlags all true + OwnerTenantId returned

## Validation
- [ ] `dotnet build VanAn.sln` 0 errors
- [ ] `guard-check.ps1` ALL PASSED
- [ ] `dotnet test` ALL PASS
- [ ] Manual: create reseller instance → truy cập → all icons + tenant context = reseller
- [ ] Deploy + RV

## Files Modified (expected)
1. `1_Shared/Domain/Aggregates/KhachLinkAggregate/KhachLinkNavFlags.cs` — UPDATE Reseller case
2. `5_WebApps/ShopERP/Pages/Admin/KhachLinkInstances.razor` — ENABLE dropdown
3. `6_Tests/VanAn.Core.Tests/KhachLink/KhachLinkNavFlagsResellerTests.cs` — NEW

## R2 Complete Gate
- [ ] Build + tests PASS
- [ ] User approval → merge R2 → deploy → RV
