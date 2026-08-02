# TASK CARD — VAS Wave 8: Feature Flag + TenantType

> **Status:** NOT STARTED | INVESTIGATE → PLAN → IMPLEMENT
> **Prerequisite:** W7 merged (tests pass)
> **Branch:** `feature/vas-wave8-feature-flag-tenanttype`
> **Estimated sessions:** 1

## Objective
Tách HKD vs VAS module, feature flag gating + **HKD→DN conversion service (D9)** + read-only historical access.

## Prerequisites (verify before code)
- [ ] W7 merged (all tests pass)
- [ ] W2 TenantType enum available
- [ ] Verify Tenant entity: `1_Shared/Domain/Aggregates/TenantAggregate/Tenant.cs`
- [ ] Verify existing feature flag pattern (if any)

## Files to Modify
| File | Changes |
|------|---------|
| `1_Shared/Domain/Aggregates/TenantAggregate/Tenant.cs` | ADD TenantType field + migration for conversion fields (W2 added fields) |
| `3_CoreHub/Infrastructure/Migrations/` | ADD migration for TenantType + conversion fields |
| `3_CoreHub/Services/ITenantConversionService.cs` | CREATE (D9) |
| `3_CoreHub/Services/TenantConversionService.cs` | CREATE (D9) |
| `5_WebApps/ShopERP/Controllers/*.cs` (4 BCTC controllers) | ADD feature flag check |
| `5_WebApps/ShopERP/Controllers/*.cs` (HKD controllers) | ADD read-only gating if Status==Converted |
| `5_WebApps/ShopERP/Components/Pages/Accounting/` | ADD UI gating + conversion wizard |
| `6_Tests/VanAn.Architecture.Tests/` | ADD TenantType isolation test |
| `6_Tests/VanAn.Core.Tests/Services/` | ADD TenantConversionServiceTests |

## Detailed Task List

### W8-T1: Add TenantType to Tenant entity
- Add `TenantType Type { get; private set; }` to Tenant
- Default: HKD (backward compatible)
- Migration for new column + conversion fields (PredecessorTenantId, SuccessorTenantId, ConvertedAt, ConvertedToStandard from W2)

### W8-T2: Feature flag VAS_REPORTS_ENABLED
- Only Enterprise_* tenants access 4 BCTC
- HKD tenants get 403 on VAS endpoints

### W8-T3: Route gating
- 4 BCTC controllers: check TenantType before return
- Return 403 if TenantType == HKD

### W8-T4: UI gating
- Hide VAS menu if TenantType == HKD
- Show HKD menu (S1a-S3a) if TenantType == HKD
- Show VAS menu if TenantType == Enterprise_*

### W8-T5: HKD reports untouched
- S1a-S3a still accessible for HKD tenants
- No regression

### W8-T6: Architecture test
- Verify TenantType isolation
- HKD tenant cannot access VAS endpoints

### W8-T7: HKD→DN Conversion Service (D9 — Option B: New Tenant + Link)
Create `ITenantConversionService` + `TenantConversionService`:
```csharp
public interface ITenantConversionService
{
    // Convert HKD → DN: tạo Tenant mới, migrate opening balance, deactivate HKD
    Task<Tenant> ConvertHkdToEnterpriseAsync(
        Guid hkdTenantId, TenantType newType, AccountingStandard standard, string newName);
    
    // Get predecessor tenant (HKD cũ) — for read-only historical access
    Task<Tenant?> GetPredecessorAsync(Guid enterpriseTenantId);
    
    // Get successor tenant (DN mới) — from HKD perspective
    Task<Tenant?> GetSuccessorAsync(Guid hkdTenantId);
}
```

Conversion flow:
1. Validate: HKD tenant exists, Status=Active, not already converted
2. Create new Tenant via `Tenant.CreateFromConversion` (W2 factory)
3. Migrate opening balance:
   - Query HKD closing balance (AccountingEntry aggregates)
   - Map HKD accounts → DN accounts via `IHkdToEnterpriseAccountMapper` (W3)
   - Create OpeningBalance entries cho DN mới
   - Verify: DN opening balance cân (debit = credit)
4. Mark HKD as converted: `hkdTenant.MarkConvertedTo(newTenantId)`
5. Save both tenants
6. Raise TenantConvertedEvent

### W8-T8: Read-only historical access (D9)
- HKD tenant with Status=Converted: reports read-only (no new entries)
- DN tenant: can access predecessor HKD reports via link
  - UI: "Xem báo cáo cũ (HKD)" button → redirect to predecessor tenant (read-only mode)
  - API: `GET /api/hkd-books?tenantId={predecessorId}&readonly=true`
- Gating: if Tenant.Status == Converted → HKD endpoints return read-only flag

### W8-T9: Conversion UI Wizard
- Page: `Components/Pages/Accounting/ConvertToEnterprise.razor`
- Wizard steps:
  1. Confirm: "Chuyển đổi HKD sang Doanh nghiệp?"
  2. Select: TenantType (SME/Large/SuperSmall) + AccountingStandard (TT 99/133/58)
  3. Enter: New tenant name
  4. Preview: Opening balance migration (HKD closing → DN opening, mapped)
  5. Execute: Call ITenantConversionService
  6. Result: New tenant created, redirect to DN dashboard

### W8-T10: Build + guard pass

## Verification
- [ ] HKD tenant → 403 on `/api/balance-sheets`
- [ ] Enterprise tenant → 200 on `/api/balance-sheets`
- [ ] HKD tenant → S1a-S3a still work
- [ ] Conversion: HKD → DN creates new Tenant with PredecessorTenantId
- [ ] Conversion: HKD Status = Converted, SuccessorTenantId set
- [ ] Conversion: Opening balance migrated (debit=credit cân)
- [ ] Read-only: Converted HKD reports accessible via predecessor link
- [ ] Build pass + guard pass

## Rollback
- Git revert (TenantType field + gating + conversion service)
- Migration: cần rollback migration nếu revert
- Converted tenants: cần manual data cleanup (delete DN tenant, reactivate HKD)

## Open Questions
- Q1: TenantType — field trên Tenant hay separate TenantSettings table?
- Q2: Feature flag — per-tenant (DB) hay global (appsettings)?
- Q3: Existing tenants — default HKD hay Enterprise_SME?
- Q4: Conversion wizard — có cần admin approval hay user tự convert?
- Q5: Opening balance migration — auto hay manual review trước khi confirm?
- Q6: DN→HKD conversion (ngược lại) — có cần support hay chỉ HKD→DN?
