# TASK CARD — Sprint 7: Type 5 Reseller + Owner Role Assignment (KhachLink Multi-Profile R2)

> **Status:** ⏳ PENDING
> **Priority:** P2 — R2 (after R1 merge)
> **Branch:** `feature/khachlink-multi-profile-r2` (from main after R1 merge)
> **Mode:** IMPLEMENT
> **Last Updated:** 2026-09-04 (EXPANDED — added Tasks 5-9: TenantOwner role assignment flow per user-identified gap)

## Objective
Add Reseller profile preset to `KhachLinkNavFlags.ForProfile()` + enable Reseller option in SystemAdmin UI + verify CommerceMode.Reseller integration + **NEW: enable Tenant Owner (Reseller owner) to assign Salesman/Shipper roles to customers of own tenant via dedicated Owner panel** + tests.

## Background — Owner Role Assignment Gap (identified 2026-09-04)
- Existing `CommunityAdminService.ActivateRoleAsync` + `CommunityAdminController` (route `/api/admin/community/*`) + `AdminPanel.razor` are all `[Authorize(Policy="SystemAdmin")]` → **only SystemAdmin can activate Salesman/Shipper roles**.
- Reseller business requires tenant owner to manage their own community collaborators without platform intervention.
- Reuse existing `RequireOwnerRole` policy (`2_Gateway/Program.cs:278-281`) — `tenant_id` claim + `Owner` role. No new policy handler needed.
- `Customer : BaseEntity, IMustHaveTenant` → `Customer.TenantId` available for tenant-scoped filtering.
- `CommunityRole.TenantId = Customer.TenantId` at activation time (already done by `CommunityAdminService.ActivateRoleAsync` line 105: `new CommunityRole(customer.TenantId, customerId, role, activatedBy)`).

## Prerequisites
- [x] R1 merged + deployed + RV pass
- [x] Feature flag ON in production
- [x] Existing `RequireOwnerRole` policy verified in `2_Gateway/Program.cs:278-281`
- [x] `Customer` implements `IMustHaveTenant` (verified `1_Shared/Domain.cs:659`)

## Task 1: Reseller preset
**File:** `1_Shared/Domain/Aggregates/KhachLinkAggregate/KhachLinkNavFlags.cs`
- Update `ForProfile(Reseller)` case: return `new KhachLinkNavFlags()` (all true) — full commerce + reseller extensions
- Remove `// TODO R2` comment

## Task 2: SystemAdmin UI enable
**File:** `5_WebApps/ShopERP/Components/Pages/Admin/KhachLinkInstances.razor`
- Enable Reseller in Profile dropdown (remove "R2" disabled tooltip if any — verify current state)
- Note: Dropdown line 322 already shows `new("4", "Reseller (Type 5) — R2")` — may already be selectable. Verify + remove any disabled state.

## Task 3: CommerceMode integration verify
- KhachLinkInstance profile=Reseller + OwnerTenantId=reseller → tenant context = reseller (KhachLinkLayout sets TenantService)
- Orders created on reseller instance → snapshot `CommerceMode.Reseller` (existing flow via `TenantSettings.CommerceModeOverride` or `GlobalCommerceMode`)
- **No code change needed** — existing CommerceModeService handles this. Verify only.

## Task 4 (NEW): Service layer — tenant-scoped overloads
**Files:**
- `3_CoreHub/Services/ICommunityAdminService.cs` — ADD:
  - `Task<PagedResult<EligibleCustomerDto>> GetEligibleCustomersForTenantAsync(Guid tenantId, int page, int pageSize)` — filter `Customer.TenantId == tenantId` (still apply IdentityLevel + LoyaltyPoints eligibility)
  - `Task<CommunityRole> ActivateRoleForTenantAsync(Guid tenantId, Guid customerId, CommunityRoleType role, Guid activatedBy)` — verify `customer.TenantId == tenantId` (IDOR guard) → throw `UnauthorizedAccessException` if mismatch → reuse existing activation logic
  - `Task DeactivateRoleForTenantAsync(Guid tenantId, Guid customerId, CommunityRoleType role)` — same IDOR guard
  - `Task<List<CommunityRole>> GetCustomerRolesForTenantAsync(Guid tenantId, Guid customerId)` — verify ownership
- `3_CoreHub/Services/CommunityAdminService.cs` — IMPLEMENT above methods (refactor existing logic to shared private helper, both SystemAdmin cross-tenant + Owner tenant-scoped use same core activation)

**IDOR guard pattern:**
```csharp
if (customer.TenantId != tenantId)
    throw new UnauthorizedAccessException($"Customer {customerId} does not belong to tenant {tenantId}.");
```

## Task 5 (NEW): Gateway endpoints — TenantOwner variant
**File:** `2_Gateway/Controllers/TenantCommunityAdminController.cs` (NEW — separate from `CommunityAdminController` to keep SystemAdmin flow untouched)
- Route prefix: `/api/v1/tenant-community`
- `[Authorize(Policy = "RequireOwnerRole", AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]` at controller level
- Endpoints:
  - `GET /api/v1/tenant-community/eligible?page=1&pageSize=20` — read `tenant_id` from JWT claim → call `GetEligibleCustomersForTenantAsync(tenantId, page, pageSize)`
  - `POST /api/v1/tenant-community/{customerId}/activate-role` — body `ActivateRoleRequest { Role }` → read `tenant_id` from JWT → `ActivateRoleForTenantAsync(tenantId, customerId, roleType, adminId)` (adminId = JWT `sub` claim)
  - `POST /api/v1/tenant-community/{customerId}/deactivate-role` — same pattern
  - `GET /api/v1/tenant-community/{customerId}/roles` — same pattern
- **No route param `tenantId`** — pulled from JWT to avoid IDOR via route tampering
- Reuse existing `ActivateRoleRequest` DTO from `CommunityAdminController.cs`
- Apply Pattern #10 (Gateway charset) — N/A (no content forwarding, only JSON request body)
- DI: `ICommunityAdminService` already registered in `2_Gateway/Program.cs:390`

## Task 6 (NEW): ShopERP — Owner panel UI
**Files:**
- `5_WebApps/ShopERP/Components/Pages/Community/OwnerPanel.razor` (NEW) — `[Authorize(Roles = "Owner")]` (NOT SystemAdmin)
  - Reuse `AdminPanel.razor` layout pattern (table of eligible customers + activate/deactivate buttons + alert banner)
  - Inject `CommunityAdminApiClient` (existing) — add new methods OR create `TenantCommunityAdminApiClient` (preferred for separation)
- `5_WebApps/ShopERP/Services/ApiClients/TenantCommunityAdminApiClient.cs` (NEW) — calls `/api/v1/tenant-community/*` endpoints with JWT bearer (Owner's token)
  - `GetEligibleAsync(int page, int pageSize)` — returns `PagedResult<EligibleCustomerDto>`
  - `ActivateRoleAsync(Guid customerId, string role)`
  - `DeactivateRoleAsync(Guid customerId, string role)`
  - `GetCustomerRolesAsync(Guid customerId)`
- `5_WebApps/ShopERP/Components/Layout/NavMenu.razor` (UPDATE) — add "Cộng tác viên" nav link visible only when `User.IsInRole("Owner")` AND not `User.IsInRole("SystemAdmin")` (avoid duplicate with existing SystemAdmin menu)
- `5_WebApps/ShopERP/Program.cs` (UPDATE) — register `TenantCommunityAdminApiClient` as scoped

**UI Platform compliance (Gate 5):** Use `VanAButton`, `VanAnCard`, `VanAnAlert` components (same as `AdminPanel.razor`).

## Task 7 (NEW): JWT claim verification
**Verify** (no code change unless claim is missing):
- ShopERP login flow issues JWT with `tenant_id` claim + `role` claim containing `"Owner"` for tenant owner users
- `RequireOwnerRole` policy passes for Owner users
- If claim format mismatch found → fix in `5_WebApps/ShopERP/Services/AuthTokenService.cs` (or equivalent JWT issuer) — likely already correct since policy is used elsewhere
- Reference: `RoleClaimNormalizer.cs` handles short-form ↔ long-form `role` claim compatibility

## Task 8: Tests
**File:** `6_Tests/VanAn.Core.Tests/KhachLink/KhachLinkNavFlagsResellerTests.cs` (NEW)
- `ForProfile(Reseller)` = all 15 true

**File:** `6_Tests/VanAn.Core.Tests/Community/CommunityAdminServiceTenantScopedTests.cs` (NEW)
- `GetEligibleCustomersForTenantAsync(tenantId_A)` returns only customers with `TenantId == A` (not B)
- `ActivateRoleForTenantAsync(tenantId_A, customerId_B, ...)` throws `UnauthorizedAccessException` (IDOR guard)
- `ActivateRoleForTenantAsync(tenantId_A, customerId_A, ...)` succeeds — `CommunityRole.TenantId == A`
- `DeactivateRoleForTenantAsync` IDOR guard works
- Eligibility criteria (IdentityLevel + LoyaltyPoints) still enforced in tenant-scoped path

**File:** `6_Tests/VanAn.Integration.Tests/Community/TenantCommunityAdminControllerTests.cs` (NEW)
- Owner token (tenant_id=A, role=Owner) → `GET /eligible` 200 with A's customers only
- Owner token (tenant_id=A) → `POST /{customerId_B}/activate-role` 403 (or 401/Unauthorized depending on where guard throws — service throws → controller returns 403)
- Owner token (tenant_id=A) → `POST /{customerId_A}/activate-role` 200
- Anonymous → 401
- SystemAdmin token (no tenant_id claim) → 403 (SystemAdmin does not have `tenant_id` claim → `RequireOwnerRole` policy fails → SystemAdmin uses existing `/api/admin/community/*` endpoints)

**File:** `6_Tests/VanAn.Integration.Tests/KhachLink/KhachLinkInstanceResellerTests.cs` (NEW or extend existing)
- Integration: create reseller instance → fetch by-domain → NavFlags all true + OwnerTenantId returned

## Validation
- [ ] `dotnet build VanAn.sln` 0 errors
- [ ] `guard-check.ps1` ALL PASSED
- [ ] `dotnet test` ALL PASS (existing + new)
- [ ] Manual: SystemAdmin tạo reseller instance → truy cập → all icons + tenant context = reseller
- [ ] Manual: Owner login ShopERP → /community/owner-panel → thấy eligible customers of own tenant → activate Salesman → verify in DB
- [ ] Manual: Owner tenant A → cannot activate for customer tenant B (IDOR guard)
- [ ] Manual: SystemAdmin → existing /admin/community/admin-panel → still works cross-tenant
- [ ] Deploy + RV

## Files Modified (expected)
1. `1_Shared/Domain/Aggregates/KhachLinkAggregate/KhachLinkNavFlags.cs` — UPDATE Reseller case (Task 1)
2. `5_WebApps/ShopERP/Components/Pages/Admin/KhachLinkInstances.razor` — ENABLE dropdown (Task 2)
3. `3_CoreHub/Services/ICommunityAdminService.cs` — ADD tenant-scoped overloads (Task 4)
4. `3_CoreHub/Services/CommunityAdminService.cs` — IMPLEMENT tenant-scoped methods + IDOR guard (Task 4)
5. `2_Gateway/Controllers/TenantCommunityAdminController.cs` — NEW (Task 5)
6. `5_WebApps/ShopERP/Components/Pages/Community/OwnerPanel.razor` — NEW (Task 6)
7. `5_WebApps/ShopERP/Services/ApiClients/TenantCommunityAdminApiClient.cs` — NEW (Task 6)
8. `5_WebApps/ShopERP/Components/Layout/NavMenu.razor` — ADD Owner-only "Cộng tác viên" link (Task 6)
9. `5_WebApps/ShopERP/Program.cs` — REGISTER `TenantCommunityAdminApiClient` (Task 6)
10. `6_Tests/VanAn.Core.Tests/KhachLink/KhachLinkNavFlagsResellerTests.cs` — NEW (Task 8)
11. `6_Tests/VanAn.Core.Tests/Community/CommunityAdminServiceTenantScopedTests.cs` — NEW (Task 8)
12. `6_Tests/VanAn.Integration.Tests/Community/TenantCommunityAdminControllerTests.cs` — NEW (Task 8)
13. `6_Tests/VanAn.Integration.Tests/KhachLink/KhachLinkInstanceResellerTests.cs` — NEW or extend (Task 8)

## Files NOT modified (boundaries respected)
- `1_Shared/Domain.cs` — `Customer` already `IMustHaveTenant`, `CommunityRole` already has `TenantId`. No domain change.
- `2_Gateway/Controllers/CommunityAdminController.cs` — SystemAdmin cross-tenant flow unchanged
- `5_WebApps/ShopERP/Components/Pages/Community/AdminPanel.razor` — SystemAdmin panel unchanged
- `AccountingEntry` — immutable (HARD STOP respected)
- `KhachLinkInstance` entity — no domain change (R1 already complete)

## R2 Complete Gate
- [ ] Build + tests PASS
- [ ] User approval → merge R2 → deploy → RV

## Out-of-Scope (DEFERRED to R2.2 — separate release)

**Reseller Accounting-Cashflow Fix** — discovered 2026-09-04 during Sprint 7 plan review. NOT in Sprint 7 scope.

**Gap:** `OrderService.GenerateAccountingEntriesAsync` (line 162-254) has NO `CommerceMode.Reseller` branch. All orders generate identical Revenue(511) + VAT(3331) + COGS(632) on `order.TenantId`'s books. But `WalletService.ConfirmCodResellerAsync` (line 223-336) shows Vạn An (Platform) is middleman: order.TenantId = SUPPLIER receives only `costPrice` via Wallet Settlement, customer pays `sellPrice`, margin kept by Vạn An (PlatformFee + CommunityFund + Commission + VanAn net).

**Hệ quả:** Supplier tenant's accounting shows inflated revenue (sellPrice) they never receive → violates TT 152/2025/TT-BTC cash-basis (doanh thu ghi nhận theo thực thu).

**Deferred to:** `feature/reseller-accounting-fix` branch (R2.2) — separate release after R2 merge. See `master_plan.md` Section 5 + `release_strategy.md` Section 2 (R2.2) for full scope.

**Why deferred:** (1) Accounting fix touches immutable `AccountingEntry` patterns — needs isolated sprint for safety; (2) May require Domain entity extension (Platform tenant accounting metadata) → governance approval needed; (3) Sprint 7 already expanded with Owner Role Assignment (Tasks 4-8) — adding accounting fix would exceed scope limit + increase risk.

## Open Questions (to resolve during Task 7)
- Q1: Does ShopERP JWT issuer include `tenant_id` claim for Owner users? (Verify in `AuthTokenService` or equivalent — likely yes since `RequireOwnerRole` policy already used)
- Q2: SystemAdmin role vs Owner role — does SystemAdmin user also have `tenant_id` claim? If yes, `RequireOwnerRole` would pass for SystemAdmin too → SystemAdmin could use both endpoints. Acceptable (more permissive) but worth noting. If no, clean separation.
