# TASK CARD — Phase 5a: TenantSettings Extension (Prerequisite for Phase 5)

> **Status:** ✅ COMPLETE (Wave 1, commit `66c9cfaf`, CD SUCCESS, VPS RV 10/10 PASS)
> **Priority:** P1 — BLOCKER for Phase 5 (B 09-DN) — UNBLOCKED
> **Branch:** `main` (folded into Wave 1 commit)
> **Estimated sessions:** 1 (30 phút)
> **Mode:** IMPLEMENT
> **Domain modification:** YES — extend `TenantSettings` value object
> **Implemented:** 2026-08-03 — Added LegalForm, BusinessField, CharterCapital properties to TenantSettings.cs. Phase 5 (B 09-DN) prerequisite satisfied.

## Objective
B 09-DN (Bản thuyết minh BCTC) Phần I requires Tenant info: Hình thức pháp lý, Lĩnh vực kinh doanh, Vốn điều lệ. These fields DO NOT EXIST on Tenant entity. Must add before Phase 5 can populate Phần I.

**Option B chosen:** Add to `TenantSettings` (owned value object) — NO migration needed, fits existing pattern (ContactEmail, Address, TaxCode already there).

## Prerequisites
- [ ] Verify `TenantSettings` class location (in Tenant.cs or Domain.cs)
- [ ] Verify `TenantSettings` is owned value object (EF Core `OwnsOne`)
- [ ] Verify no existing `LegalForm`/`BusinessField`/`CharterCapital` properties

## Files to Modify
| File | Changes |
|------|---------|
| `1_Shared/Domain/Aggregates/TenantAggregate/Tenant.cs` (or Domain.cs) | Add 3 properties to `TenantSettings` |
| `3_CoreHub/Services/TenantManagementService.cs` | Update `UpdateProfileAsync` to accept new fields (if needed) |
| `2_Gateway/Controllers/TenantsController.cs` | Update DTO if profile edit includes these fields |
| `5_WebApps/ShopERP/Services/TenantApiClient.cs` | Add to `TenantApiDto` (optional — for UI display) |
| `5_WebApps/ShopERP/Components/Pages/Admin/TenantManagement.razor` | Add edit fields (optional — admin can set later) |

## Detailed Changes

### Change 1: TenantSettings — add 3 properties
```csharp
// In TenantSettings value object
public string? LegalForm { get; set; }         // Hình thức pháp lý (VD: "Công ty TNHH")
public string? BusinessField { get; set; }     // Lĩnh vực kinh doanh (VD: "F&B", "Thương mại")
public decimal? CharterCapital { get; set; }   // Vốn điều lệ (VND)
```

**Why TenantSettings (not Tenant):**
- Owned value object → EF Core stores as columns on Tenants table (no separate table)
- Adding nullable properties → EF Core auto-migrates (lightweight, no schema break)
- Existing pattern: ContactEmail, Address, TaxCode, BrandStory already in TenantSettings
- Tenant entity stays focused on identity + lifecycle; Settings holds profile metadata

### Change 2: UpdateProfileAsync (optional — can defer to admin UI)
```csharp
// ITenantManagementService — extend UpdateTenantProfileRequest
public string? LegalForm { get; init; }
public string? BusinessField { get; init; }
public decimal? CharterCapital { get; init; }
```

## Verification
- [ ] `dotnet build VanAn.sln` Release — 0 errors
- [ ] `TenantSettings` has 3 new nullable properties
- [ ] No migration required (owned value object, nullable columns auto-added)
- [ ] Existing tests pass (nullable properties don't break existing data)

## Rollback
`git revert <commit>` — nullable properties, no data loss risk.
