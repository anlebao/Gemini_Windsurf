# TASK CARD — Phase 2: Platform Fee on Marketplace Orders

> **Status:** 📋 PENDING (requires Phase 1 complete)
> **Priority:** P1 — Economic foundation (needed for INV-009, Phase 3 budget)
> **Branch:** `feature/valcn-v2-phase2-platform-fee`
> **Estimated sessions:** 1-2
> **Mode:** IMPLEMENT
> **Domain modification:** NO (logic only — fields from Phase 1)

## Objective
Extend `OrderService.SnapshotCommerceModeAsync` để set `PlatformFeeRate` (từ `ShopFeatureSettingsEntity`) + `PlatformFeeAmount` trên **Marketplace** orders. Hiện tại chỉ set ở **Reseller** mode — Marketplace mode là no-op (line 858-859).

**Why:** BOM v2.0 core pivot — Vạn An revenue = take-rate % GMV. Nếu Marketplace orders không có `PlatformFeeAmount`, không tính được platform revenue (Phase 7) hay enforce INV-009.

## Prerequisites
- [ ] Phase 1 complete — `Order.PlatformFeeAmount` field exists + `IFeatureFlagService` registered
- [ ] Phase 0 verified `ShopFeatureSettingsEntity.PlatformFeeRate` exists + default value
- [ ] `dotnet build VanAn.sln` Release — 0 errors (baseline)

## Files to Modify

| File | Line | Change |
|------|------|--------|
| `3_CoreHub/Services/OrderService.cs` | 845-905 (SnapshotCommerceModeAsync) | Extend Marketplace branch — **wrapped in feature flag check** |
| `3_CoreHub/Services/OrderService.cs` | constructor | Inject `IFeatureFlagService` |
| Tests | (find order service tests) | Add tests: feature ON + feature OFF |

## Detailed Changes

### Change 1: SnapshotCommerceModeAsync — Marketplace branch (feature-flagged)
**Current (line 858-859):** Marketplace mode = no-op, PlatformFeeRate remains null.

**New — wrapped in toggle (default OFF = existing behavior preserved):**
```csharp
case CommerceMode.Marketplace:
{
    // VALCN v2.0 Phase 2 — feature-flagged, default OFF
    // When OFF: existing behavior (no-op, PlatformFeeAmount = null)
    // When ON: set PlatformFeeRate + PlatformFeeAmount
    if (await _featureFlagService.IsEnabledAsync("ValcnV2_PlatformFee", ct))
    {
        var platformFeeRate = await GetPlatformFeeRateAsync(tenantId);
        order.PlatformFeeRate = platformFeeRate;
        order.PlatformFeeAmount = order.TotalAmount * platformFeeRate;
    }
    // When OFF: no-op (existing behavior — PlatformFeeRate/Amount remain null)
    break;
}
```

### Change 2: Inject IFeatureFlagService into OrderService
```csharp
// In OrderService constructor — add IFeatureFlagService
public class OrderService(
    // ... existing deps ...
    IFeatureFlagService featureFlagService)  // NEW
{
    private readonly IFeatureFlagService _featureFlagService = featureFlagService;
}
```

### Change 3: GetPlatformFeeRateAsync helper (per-tenant with global fallback)
```csharp
private async Task<decimal> GetPlatformFeeRateAsync(TenantId tenantId)
{
    // Per-tenant rate (Phase 1 field, default 5%)
    var settings = await _shopFeatureSettingsService.GetSettingsAsync(tenantId.Value);
    if (settings?.PlatformFeeRate.HasValue == true)
        return settings.PlatformFeeRate.Value;

    // Fallback: global SystemSetting.DefaultPlatformFeeRate (existing, default 30%)
    var globalRates = await _commerceModeService.GetDefaultRatesAsync(ct);
    return globalRates.PlatformFeeRate;
}
```

## Verification
- [ ] `dotnet build VanAn.sln` Release — 0 errors
- [ ] `guard-check.ps1` — PASS
- [ ] Test: Feature OFF (default) → Create Marketplace order → `PlatformFeeRate = null` + `PlatformFeeAmount = null` (existing behavior preserved)
- [ ] Test: Feature ON → Create Marketplace order → `PlatformFeeRate > 0` + `PlatformFeeAmount > 0`
- [ ] Test: Feature ON → Create Reseller order → behavior unchanged
- [ ] Test: Feature ON → Tenant without `ShopFeatureSettingsEntity.PlatformFeeRate` → default 5%
- [ ] Test: Toggle OFF→ON runtime → new orders have PlatformFeeAmount (no restart needed)
- [ ] Existing tests pass (feature OFF = same as before)

## Rollback
`git revert <commit>` OR toggle OFF via `/admin/valcn-features` — no code revert needed. Feature OFF = existing behavior.

---

## ANALYZE UPDATE (to be filled during INVESTIGATE step)

### INVESTIGATE checklist
- [ ] Read `OrderService.SnapshotCommerceModeAsync` full method (line 845-905)
- [ ] Verify Marketplace branch is no-op (line 858-859)
- [ ] Find `IShopFeatureSettingsRepository` (or equivalent) — method to get by tenant [fix I1/M1]
- [ ] Verify `ShopFeatureSettingsEntity.PlatformFeeRate` default value
- [ ] Find all callers of `SnapshotCommerceModeAsync` — confirm called for all orders
- [ ] Find existing order service tests — pattern for new test
- [ ] Verify `IFeatureFlagService` registered in DI (from Phase 1)
- [ ] Confirm `OrderService` constructor — can add `IFeatureFlagService` without breaking existing DI

### Verified Accurate
- (fill after investigation)

### DRIFT
- (fill if investigation finds drift)
