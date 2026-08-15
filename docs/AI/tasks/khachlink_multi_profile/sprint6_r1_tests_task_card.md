# TASK CARD — Sprint 6: R1 Tests (KhachLink Multi-Profile R1)

> **Status:** ✅ COMPLETE (merged `50f55e8d` → `5047ed8c`)
> **Priority:** P1 — After Sprint 5 approval
> **Branch:** `feature/khachlink-multi-profile-r1`
> **Mode:** IMPLEMENT (Test Phase)

## Objective
Domain unit tests + service integration tests + API integration tests for KhachLinkInstance + KhachLinkNavFlags + KhachLinkInstanceController. CI green → R1 complete.

## Prerequisites
- [x] Sprint 5 complete (nginx + SSL)
- [x] Build pass

## Task 1: Domain unit tests
**File:** `6_Tests/VanAn.Core.Tests/KhachLink/KhachLinkInstanceTests.cs`
- `Create` sets Label/Profile/CustomDomain/NavFlags correctly
- `Create` normalizes CustomDomain to lowercase
- `Create` throws on empty Label
- `Create` throws on empty CustomDomain
- `Create` with navFlagsOverride=null → uses ForProfile(profile) preset
- `KhachLinkNavFlags.ForProfile(FullCommerce)` = all 15 true
- `KhachLinkNavFlags.ForProfile(Directory)` = ShowHome/Stores/Profile true, rest false
- `UpdateProfile` resets NavFlags to preset
- `UpdateNavFlags` overrides individual flags
- `Deactivate` sets IsActive=false
- `Activate` sets IsActive=true
- `TenantId` always Guid.Empty (platform sentinel)

## Task 2: Service integration tests
**File:** `6_Tests/VanAn.Core.Tests/KhachLink/KhachLinkInstanceServiceTests.cs`
- Use in-memory DbContext or test DB
- `CreateAsync` validates unique CustomDomain (throws on duplicate)
- `GetByDomainAsync` returns instance or null
- `GetAllAsync` returns all active instances
- `UpdateAsync` persists profile + nav flags
- `DeactivateAsync` sets IsActive=false
- Feature flag OFF → `GetByDomainAsync` returns null (or service checks flag)

## Task 3: API integration tests
**File:** `6_Tests/VanAn.Integration.Tests/KhachLink/KhachLinkInstanceControllerTests.cs`
- CRUD endpoints (SystemAdmin auth — use DevLoginController or test JWT)
- `by-domain` endpoint (anonymous):
  - Flag OFF → 404
  - Flag ON + existing domain → 200 with DTO
  - Flag ON + non-existent domain → 404
- 403 non-admin on CRUD endpoints
- POST create → 201 with Location header
- PUT update → 200 with updated DTO
- DELETE → 204 (IsActive=false)

## Validation
- [ ] `dotnet build VanAn.sln` 0 errors
- [ ] `guard-check.ps1` ALL PASSED
- [ ] `dotnet test` ALL PASS (existing + new)
- [ ] CI pipeline green
- [ ] Architecture tests 39/39 PASS (no layer violations)

## Files Modified (expected)
1. `6_Tests/VanAn.Core.Tests/KhachLink/KhachLinkInstanceTests.cs` — NEW
2. `6_Tests/VanAn.Core.Tests/KhachLink/KhachLinkInstanceServiceTests.cs` — NEW
3. `6_Tests/VanAn.Integration.Tests/KhachLink/KhachLinkInstanceControllerTests.cs` — NEW

## R1 Complete Gate
- [ ] All Sprint 1-6 tasks done
- [ ] Build + guard-check + tests PASS
- [ ] User approval → merge R1 to main → deploy → RV
