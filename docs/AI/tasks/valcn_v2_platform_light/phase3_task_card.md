# TASK CARD — Phase 3: Loyalty Budget Enforcement

> **Status:** 📋 PENDING (requires Phase 1 + Phase 2 complete)
> **Priority:** P1 — Risk reduction (INV-007/009)
> **Branch:** `feature/valcn-v2-phase3-loyalty-budget`
> **Estimated sessions:** 2-3
> **Mode:** IMPLEMENT
> **Domain modification:** NO (logic only — fields from Phase 1)

## Objective
`LoyaltyBudgetService` check budget trước `AddPoints()` + 2 reset jobs (daily/monthly). Khi budget exhausted → reward rate = 0 (không pause tenant). Counter increment dùng `ExecuteUpdateAsync` (atomic, fix I1). Jobs inject `IServiceScopeFactory` (fix I2).

## Prerequisites
- [ ] Phase 1 complete — `LoyaltyTenantConfig` has 6 budget fields + `LoyaltyIssuanceRecord` entity exists + `IFeatureFlagService` registered
- [ ] Phase 2 complete — `Order.PlatformFeeAmount` set (for INV-009)
- [ ] `dotnet build VanAn.sln` Release — 0 errors (baseline)

## Files to Modify/Create

| File | Status | Purpose |
|------|--------|---------|
| `3_CoreHub/Services/ILoyaltyBudgetService.cs` | NEW | Interface |
| `3_CoreHub/Services/LoyaltyBudgetService.cs` | NEW | Budget check + atomic counter increment + decrement |
| `3_CoreHub/Services/OrderWorkflowService.cs` | MODIFY | Inject budget check before AddPoints — **wrapped in feature flag** |
| `3_CoreHub/Services/LoyaltyBudgetDailyResetJob.cs` | NEW | Daily 00:00 reset PointsIssuedToday |
| `3_CoreHub/Services/LoyaltyBudgetMonthlyResetJob.cs` | NEW | 1st of month reset PointsIssuedThisMonth |
| DI registration | MODIFY | Register service + 2 jobs (inject IServiceScopeFactory in jobs) |
| BackgroundServiceToggleService | MODIFY | Add 2 new jobs to toggleable KnownServices list |
| Tests | NEW | Budget enforcement + atomic increment + reset + feature ON/OFF tests |

## Detailed Changes

### Change 1: ILoyaltyBudgetService interface
```csharp
public interface ILoyaltyBudgetService
{
    Task<int> CheckAndAdjustPointsAsync(TenantId tenantId, Guid customerId, decimal orderAmount, int requestedPoints, CancellationToken ct = default);
    Task RecordIssuanceAsync(TenantId tenantId, int pointsIssued, CancellationToken ct = default);
    Task DecrementIssuanceAsync(TenantId tenantId, int pointsToReverse, CancellationToken ct = default);  // for Phase 4
}
```

### Change 2: LoyaltyBudgetService — atomic counter increment (fix I1)
```csharp
public class LoyaltyBudgetService : ILoyaltyBudgetService
{
    private readonly ILoyaltyTenantConfigRepository _configRepo;
    private readonly ILoyaltyIssuanceRecordRepository _issuanceRepo;  // Phase 1 entity — for per-customer daily check
    private readonly IOrderRepository _orderRepo;

    public async Task<int> CheckAndAdjustPointsAsync(
        TenantId tenantId, Guid customerId, decimal orderAmount, int requestedPoints, CancellationToken ct)
    {
        var config = await _configRepo.GetByTenantIdAsync(tenantId, ct);
        if (config == null) return requestedPoints;

        // Check 1: Per-order rate cap
        if (config.PerOrderRateCap.HasValue)
            requestedPoints = Math.Min(requestedPoints, (int)(orderAmount * config.PerOrderRateCap.Value));

        // Check 2: Monthly budget
        if (config.MonthlyPointsBudget.HasValue)
        {
            var remaining = config.MonthlyPointsBudget.Value - config.PointsIssuedThisMonth;
            requestedPoints = Math.Min(requestedPoints, Math.Max(0, remaining));
        }

        // Check 3: Daily budget
        if (config.DailyPointsBudget.HasValue)
        {
            var remaining = config.DailyPointsBudget.Value - config.PointsIssuedToday;
            requestedPoints = Math.Min(requestedPoints, Math.Max(0, remaining));
        }

        // Check 4: Per-customer daily limit (query LoyaltyIssuanceRecord — Phase 1 entity)
        if (config.PerCustomerDailyLimit.HasValue)
        {
            var customerIssuedToday = await _issuanceRepo.GetPointsIssuedTodayByCustomerAsync(tenantId, customerId, ct);
            var remaining = config.PerCustomerDailyLimit.Value - customerIssuedToday;
            requestedPoints = Math.Min(requestedPoints, Math.Max(0, remaining));
        }

        // Check 5: INV-009 — Platform Fee ≥ Loyalty Cost per order
        // (PlatformFeeAmount from Phase 2, loyalty cost = requestedPoints × pointValue)
        // TODO in INVESTIGATE: find point value from LoyaltyGlobalConfig

        return requestedPoints;
    }

    public async Task RecordIssuanceAsync(TenantId tenantId, int pointsIssued, CancellationToken ct)
    {
        // FIX I1: atomic increment via ExecuteUpdateAsync (EF Core 7+)
        // Avoids read-modify-write race condition with concurrent AddPoints
        await _context.LoyaltyTenantConfigs
            .Where(c => c.TenantId == tenantId.Value)
            .ExecuteUpdateAsync(s => s
                .SetProperty(c => c.PointsIssuedThisMonth, c => c.PointsIssuedThisMonth + pointsIssued)
                .SetProperty(c => c.PointsIssuedToday, c => c.PointsIssuedToday + pointsIssued), ct);
    }

    public async Task DecrementIssuanceAsync(TenantId tenantId, int pointsToReverse, CancellationToken ct)
    {
        // For Phase 4 — atomic decrement (not below 0)
        await _context.LoyaltyTenantConfigs
            .Where(c => c.TenantId == tenantId.Value)
            .ExecuteUpdateAsync(s => s
                .SetProperty(c => c.PointsIssuedThisMonth, c => Math.Max(0, c.PointsIssuedThisMonth - pointsToReverse))
                .SetProperty(c => c.PointsIssuedToday, c => Math.Max(0, c.PointsIssuedToday - pointsToReverse)), ct);
    }
}
```

### Change 3: OrderWorkflowService — inject budget check (feature-flagged)
**Default OFF = existing behavior (AddPoints trực tiếp, no budget check).**

```csharp
// Before line 428/449 — insert budget check wrapped in feature flag
int adjustedPoints = calculatedPoints;  // default = original (when feature OFF)

if (await _featureFlagService.IsEnabledAsync("ValcnV2_LoyaltyBudget", ct))
{
    // Feature ON — check budget caps
    adjustedPoints = await _loyaltyBudgetService.CheckAndAdjustPointsAsync(
        tenantId, customerId, order.TotalAmount, calculatedPoints, ct);

    if (adjustedPoints <= 0)
    {
        _logger.LogInformation("Loyalty budget exhausted for tenant {TenantId}, skipping reward for order {OrderId}", tenantId, order.Id);
        return;  // no reward, but order still completes
    }
}
// When feature OFF: adjustedPoints = calculatedPoints (existing behavior — no budget check, AddPoints directly)

// Use adjustedPoints (either capped if ON, or original if OFF)
await _allianceWalletService.AddPointsAsync(..., adjustedPoints, ...);  // or silo mode

if (await _featureFlagService.IsEnabledAsync("ValcnV2_LoyaltyBudget", ct))
{
    await _loyaltyBudgetService.RecordIssuanceAsync(tenantId, adjustedPoints, ct);
}
// LoyaltyIssuanceRecord creation already done in Phase 1
```

### Change 4: LoyaltyBudgetDailyResetJob — IServiceScopeFactory (fix I2)
```csharp
public class LoyaltyBudgetDailyResetJob : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;  // FIX I2: singleton-safe
    private readonly ILogger<LoyaltyBudgetDailyResetJob> _logger;

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            var nextRun = DateTime.UtcNow.Date.AddDays(1);
            await Task.Delay(nextRun - DateTime.UtcNow, ct);

            using var scope = _scopeFactory.CreateScope();  // FIX I2: scope per execution
            var configRepo = scope.ServiceProvider.GetRequiredService<ILoyaltyTenantConfigRepository>();
            await configRepo.ResetAllDailyCountersAsync(ct);
        }
    }
}
```

### Change 5: LoyaltyBudgetMonthlyResetJob — same pattern
```csharp
// Same IServiceScopeFactory pattern
// Run 1st of month at 00:00
// Reset PointsIssuedThisMonth for all tenants
```

### Change 6: DI + BackgroundServiceToggleService
```csharp
services.AddScoped<ILoyaltyBudgetService, LoyaltyBudgetService>();
services.AddHostedService<LoyaltyBudgetDailyResetJob>();
services.AddHostedService<LoyaltyBudgetMonthlyResetJob>();
// Add 2 jobs to BackgroundServiceToggleService toggleable list
```

## Verification
- [ ] `dotnet build VanAn.sln` Release — 0 errors
- [ ] `guard-check.ps1` — PASS
- [ ] Test: Feature OFF (default) → AddPoints trực tiếp, no budget check (existing behavior preserved)
- [ ] Test: Feature ON → Budget exhausted → `CheckAndAdjustPointsAsync` returns 0 → no reward
- [ ] Test: Feature ON → Budget available → returns capped points
- [ ] Test: Feature ON → Per-order cap 3% × 100k VND = 3000 points max
- [ ] Test: Feature ON → **Concurrent AddPoints** (2 threads, same tenant) → counters correct (atomic increment, fix I1)
- [ ] Test: Feature ON → Daily reset job → `PointsIssuedToday = 0` for all tenants
- [ ] Test: Feature ON → Monthly reset job → `PointsIssuedThisMonth = 0` for all tenants
- [ ] Test: Feature ON → Order completes with budget exhausted → no reward, order succeeds
- [ ] Test: Feature ON → INV-009: Platform fee < loyalty cost → points reduced (skip if PlatformFeeAmount null — Edge 2)
- [ ] Test: Toggle OFF→ON runtime → new orders get budget check (no restart needed)
- [ ] Existing tests pass (feature OFF = same as before)

## Rollback
`git revert <commit>` OR toggle OFF via `/admin/valcn-features` — feature OFF = existing behavior.

---

## ANALYZE UPDATE (to be filled during INVESTIGATE step)

### INVESTIGATE checklist
- [ ] Read `OrderWorkflowService.ProcessLoyaltyPointsAsync` full method (line 355-449)
- [ ] Verify AddPoints call sites (line 428 Alliance, 449 Silo)
- [ ] Find `ILoyaltyTenantConfigRepository` — method signatures
- [ ] Find `LoyaltyGlobalConfig` — point value field (for INV-009)
- [ ] Verify EF Core version supports `ExecuteUpdateAsync` (EF Core 7+)
- [ ] Find `BirthdayBonusJob` — daily scheduling + IServiceScopeFactory pattern reference
- [ ] Check `BackgroundServiceToggleService` — how to register new jobs as toggleable
- [ ] Find `ILoyaltyIssuanceRecordRepository` (created in Phase 1) — confirm GetPointsIssuedTodayByCustomerAsync method or add

### Verified Accurate
- (fill after investigation)

### DRIFT
- (fill if investigation finds drift)
