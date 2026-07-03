# TASK CARD — VAS Wave 8: Feature Flag + TenantType

> **Status:** NOT STARTED | INVESTIGATE → PLAN → IMPLEMENT
> **Prerequisite:** W7 merged (tests pass)
> **Branch:** `feature/vas-wave8-feature-flag-tenanttype`
> **Estimated sessions:** 1

## Objective
Tách HKD vs VAS module, feature flag gating.

## Prerequisites (verify before code)
- [ ] W7 merged (all tests pass)
- [ ] W2 TenantType enum available
- [ ] Verify Tenant entity: `1_Shared/Domain/Aggregates/TenantAggregate/Tenant.cs`
- [ ] Verify existing feature flag pattern (if any)

## Files to Modify
| File | Changes |
|------|---------|
| `1_Shared/Domain/Aggregates/TenantAggregate/Tenant.cs` | ADD TenantType field |
| `3_CoreHub/Infrastructure/Migrations/` | ADD migration for TenantType |
| `5_WebApps/ShopERP/Controllers/*.cs` (4 BCTC controllers) | ADD feature flag check |
| `5_WebApps/ShopERP/Components/Pages/Accounting/` | ADD UI gating |
| `6_Tests/VanAn.Architecture.Tests/` | ADD TenantType isolation test |

## Detailed Task List

### W8-T1: Add TenantType to Tenant entity
- Add `TenantType Type { get; private set; }` to Tenant
- Default: HKD (backward compatible)
- Migration for new column

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

### W8-T7: Build + guard pass

## Verification
- [ ] HKD tenant → 403 on `/api/balance-sheets`
- [ ] Enterprise tenant → 200 on `/api/balance-sheets`
- [ ] HKD tenant → S1a-S3a still work
- [ ] Build pass + guard pass

## Rollback
- Git revert (TenantType field + gating)
- Migration: cần rollback migration nếu revert

## Open Questions
- Q1: TenantType — field trên Tenant hay separate TenantSettings table?
- Q2: Feature flag — per-tenant (DB) hay global (appsettings)?
- Q3: Existing tenants — default HKD hay Enterprise_SME?
