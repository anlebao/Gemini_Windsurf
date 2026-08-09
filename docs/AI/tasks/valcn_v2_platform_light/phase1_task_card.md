# TASK CARD — Phase 1: Domain Fields + LoyaltyIssuanceRecord + AccountingEntry Factory Mod + CorrelationId Set

> **Status:** 📋 PENDING (requires Phase 0 complete)
> **Priority:** P0 — Foundation for all subsequent phases
> **Branch:** `feature/valcn-v2-phase1-domain-fields`
> **Estimated sessions:** 3-4 (tăng từ 2-3 — add Group G FeatureFlag infra + UI)
> **Mode:** IMPLEMENT
> **Domain modification:** YES (additive fields + 1 new entity + AccountingEntry factory param extension)

## Objective
Phase 1 gộp 3 concerns (từ v1 Phase 1 + Phase 5):
1. Add 10 additive fields + 1 new entity (`LoyaltyIssuanceRecord`) + 1 migration
2. Modify `AccountingEntry` factory chain (6 files) để accept + set `CorrelationId`
3. Set `CorrelationId` tại creation sites + tạo `LoyaltyIssuanceRecord` khi AddPoints

**Why merge:** AccountingEntry factory chain modification là prerequisite cho Phase 4 (query by CorrelationId để reversal). Gộp vào Phase 1 tránh dependency issue (C1) + gộp Domain changes.

## Prerequisites
- [ ] Phase 0 complete — `phase0_findings.md` approved
- [ ] `dotnet build VanAn.sln` Release — 0 errors (baseline)
- [ ] `guard-check.ps1` — PASS (baseline)

## Files to Modify (7 concern groups)

### Group A: Additive Fields (Domain.cs + EF configs)
| Entity | Field | Type | Default |
|--------|-------|------|---------|
| `LoyaltyTenantConfig` | `MonthlyPointsBudget` | `int?` | null |
| `LoyaltyTenantConfig` | `DailyPointsBudget` | `int?` | null |
| `LoyaltyTenantConfig` | `PerCustomerDailyLimit` | `int?` | null |
| `LoyaltyTenantConfig` | `PerOrderRateCap` | `decimal?` | null |
| `LoyaltyTenantConfig` | `PointsIssuedThisMonth` | `int` | 0 |
| `LoyaltyTenantConfig` | `PointsIssuedToday` | `int` | 0 |
| `AccountingEntry` | `CorrelationId` | `Guid?` | null |
| `OutboxEvent` | `CorrelationId` | `Guid?` | null |
| `Order` | `PlatformFeeAmount` | `decimal?` | null |
| `ShopFeatureSettingsEntity` | `PlatformFeeRate` | `decimal?` | 0.05m (5%) — per-tenant, fallback global 30% |

**District dropped** — Phase 9 (clustering) dropped, no MVP consumer. Defer v3.0.

### Group B: New Entity — LoyaltyIssuanceRecord
```csharp
public class LoyaltyIssuanceRecord : BaseEntity, IMustHaveTenant
{
    public LoyaltyIssuanceRecordId LoyaltyIssuanceRecordId { get; private set; }  // business key VO (ignored in EF)
    public Guid OrderId { get; protected set; }
    public Guid CustomerId { get; protected set; }
    public int PointsIssued { get; protected set; }
    public DateTime IssuedAt { get; protected set; }
    public bool IsReversed { get; protected set; }

    protected LoyaltyIssuanceRecord() { }

    public LoyaltyIssuanceRecord(TenantId tenantId, Guid orderId, Guid customerId, int pointsIssued)
        : base(tenantId)
    {
        LoyaltyIssuanceRecordId = new LoyaltyIssuanceRecordId(Guid.NewGuid());
        Id = LoyaltyIssuanceRecordId.Value;  // Single-Identity Pattern sync
        OrderId = orderId;
        CustomerId = customerId;
        PointsIssued = pointsIssued;
        IssuedAt = DateTime.UtcNow;
        IsReversed = false;
    }

    public void MarkReversed() => IsReversed = true;
}

public record LoyaltyIssuanceRecordId(Guid Value);
```

**EF config:**
```csharp
builder.Ignore(e => e.LoyaltyIssuanceRecordId);  // Single-Identity Pattern
builder.Property(e => e.OrderId).IsRequired();
builder.Property(e => e.CustomerId).IsRequired();
builder.HasIndex(e => e.OrderId);  // Phase 4 query: GetByOrderIdAsync
```

### Group C: AccountingEntry Factory Chain (6 files)
**Unavoidable** — sealed class, private constructor, factory methods không accept CorrelationId.

| File | Line | Change |
|------|------|--------|
| `1_Shared/Domain.cs` | 287-397 (AccountingEntry) | Add `CorrelationId` property + `correlationId` param to constructor |
| `1_Shared/Domain.cs` | 360-395 (factory methods) | Add `correlationId` optional param to `CreateRevenue`, `CreateExpense`, `CreateReversal`, `CreateReversalWithId` |
| `1_Shared/Domain.cs` | 378-395 (`CreateReversal`) | Preserve `CorrelationId` từ original (fix M3) |
| `1_Shared/DTOs/AccountingEntryDto.cs` | 9-38 | Add `CorrelationId` field |
| `3_CoreHub/Services/AccountingService.cs` | (CreateEntryAsync) | Pass `CorrelationId` from DTO to factory |
| `3_CoreHub/Services/Events/SimpleAccountingEventHandler.cs` | 114-125 | Set `CorrelationId = orderEvent.OrderId` in AccountingEntryDto |

**Backward compat:** `correlationId` param optional (default null) → existing callers không break.

### Group D: OutboxEvent CorrelationId Set
| File | Line | Change |
|------|------|--------|
| `3_CoreHub/Services/OrderService.cs` | 767 (OutboxEvent creation) | Set `CorrelationId = order.Id` |
| `3_CoreHub/Services/OrderWorkflowService.cs` | 193 (RecordOrderCompletedEventAsync) | Set `CorrelationId = order.Id` |

### Group E: LoyaltyIssuanceRecord Creation on AddPoints
| File | Line | Change |
|------|------|--------|
| `3_CoreHub/Services/OrderWorkflowService.cs` | 428/449 (AddPoints call sites) | Create `LoyaltyIssuanceRecord` after AddPoints succeeds |
| EF config for LoyaltyIssuanceRecord | NEW | Map entity |

### Group F: Migration
- 1 migration: `AddValcnV2PlatformLightFields`
- Add 10 nullable/default columns trên existing entities
- Add 1 new table `LoyaltyIssuanceRecords`
- Backward compat: existing rows unchanged

### Group G: Feature Flag Service + Admin UI (toggle infrastructure)
**Purpose:** SystemAdmin toggle ON/OFF từng VALCN v2.0 feature runtime, không overwrite existing behavior. Default = **OFF** (existing behavior preserved cho đến admin enable).

| File | Status | Purpose |
|------|--------|---------|
| `3_CoreHub/Services/IFeatureFlagService.cs` | NEW | Interface — `IsEnabledAsync(featureKey, ct)` |
| `3_CoreHub/Services/FeatureFlagService.cs` | NEW | Impl — copy pattern từ `BackgroundServiceToggleService` (IServiceScopeFactory + 30s cache + SystemSetting) |
| `2_Gateway/Controllers/FeatureFlagsController.cs` | NEW | API `GET/PUT /api/admin/feature-flags` — SystemAdmin JWT, class-level `[Authorize]` |
| `5_WebApps/ShopERP/Services/FeatureFlagHttpService.cs` | NEW | HTTP client (ShopERP → Gateway) |
| `5_WebApps/ShopERP/Components/Pages/Admin/ValcnFeatures.razor` | NEW | Admin UI `/admin/valcn-features` — toggle switches cho 3 features |
| `5_WebApps/ShopERP/Components/Layout/NavMenu.razor` (or equivalent) | MODIFY | Add "VALCN v2.0 Features" link trong admin section |
| DI registration | MODIFY | Register IFeatureFlagService + FeatureFlagHttpService |

**SystemSetting keys (default OFF — unlike BackgroundServiceToggleService which defaults ON):**
```
Features:EnableValcnV2_PlatformFee       → "false" (default) / "true" (enabled by admin)
Features:EnableValcnV2_LoyaltyBudget     → "false" / "true"
Features:EnableValcnV2_RefundReversal    → "false" / "true"
```

**KnownFeatures array (similar to KnownServices pattern):**
```csharp
private static readonly (string Key, string Display, string Desc, string Phase)[] KnownFeatures =
[
    ("ValcnV2_PlatformFee", "Platform Fee (Marketplace)", "Tính PlatformFeeAmount trên Marketplace orders (Phase 2)", "Phase 2"),
    ("ValcnV2_LoyaltyBudget", "Loyalty Budget Cap", "Check budget trước AddPoints + reset jobs (Phase 3)", "Phase 3"),
    ("ValcnV2_RefundReversal", "Refund Reversal (UC-06)", "4-step reversal on order cancel (Phase 4)", "Phase 4"),
];
```

**CRITICAL: Default OFF** — `IsEnabledAsync` returns `false` if no SystemSetting row exists (opposite of `BackgroundServiceToggleService` which returns `true` by default). This ensures existing behavior is preserved until admin explicitly enables.

## Detailed Changes

### Change 1: AccountingEntry — add CorrelationId
```csharp
// In AccountingEntry class (Domain.cs:287)
public Guid? CorrelationId { get; }  // additive — null = legacy

// In private constructor (Domain.cs:326-357) — add param:
private AccountingEntry(
    // ... existing params ...
    IndustrySector? industrySector = null,
    Guid? correlationId = null)  // NEW — optional, default null
{
    // ... existing assignments ...
    CorrelationId = correlationId;  // NEW
}
```

### Change 2: Factory methods — add correlationId param
```csharp
// CreateRevenue (line 360)
public static AccountingEntry CreateRevenue(TenantId tenantId, AccountingPeriod period, Money amount, string description,
    string? accountCode = null, string? reference = null, IndustrySector? industrySector = null,
    Guid? correlationId = null)  // NEW
{
    return new(tenantId, amount.Value, AccountingEntryType.Revenue, VatRate.Zero,
        AccountingBookType.RevenueBook, period.Year, period.Month, description,
        reversalEntryId: null, accountCode: accountCode, reference: reference,
        industrySector: industrySector, correlationId: correlationId);  // NEW
}

// Same pattern for CreateExpense, CreateReversal, CreateReversalWithId
```

### Change 3: CreateReversal — preserve CorrelationId (fix M3)
```csharp
// CreateReversal (line 378) — preserve CorrelationId from original
public static AccountingEntry CreateReversal(AccountingEntry original, string reason)
{
    // ... existing code ...
    return new AccountingEntry(
        original.TenantId,
        -original.Amount,
        original.EntryType,
        original.VatRate,
        original.AccountingBookType,
        original.PeriodYear,
        original.PeriodMonth,
        $"Reversal of: {original.Description} - {reason}",
        original.Id,
        accountCode: original.AccountCode,
        industrySector: original.IndustrySector,
        correlationId: original.CorrelationId);  // NEW — preserve for traceability
}
```

### Change 4: AccountingEntryDto — add CorrelationId
```csharp
// In AccountingEntryDto.cs
public Guid? CorrelationId { get; set; }  // NEW — for traceability (Order.Id)
```

### Change 5: AccountingService — pass CorrelationId
```csharp
// In AccountingService.CreateEntryAsync — pass CorrelationId from DTO to factory
var entry = AccountingEntry.CreateRevenue(
    tenantId, period, amount, dto.Description,
    accountCode: dto.AccountCode,
    reference: dto.Reference,
    industrySector: dto.IndustrySector,
    correlationId: dto.CorrelationId);  // NEW
```

### Change 6: SimpleAccountingEventHandler — set CorrelationId
```csharp
// In HandleOrderCompletedEventAsync (line 114-125)
AccountingEntryDto accountingEntry = await accountingService.CreateEntryAsync(new AccountingEntryDto
{
    TenantId = orderEvent.TenantId.Value,
    Amount = orderEvent.TotalAmount,
    Description = $"Order #{orderEvent.OrderId}",
    // ... existing fields ...
    CorrelationId = orderEvent.OrderId  // NEW — trace root = Order.Id
});
```

### Change 7: OutboxEvent — set CorrelationId
```csharp
// In OrderService.CreateOrderFromCommandAsync (line 767)
var outboxEvent = new OutboxEvent(/* existing params */, correlationId: order.Id);

// In OrderWorkflowService.RecordOrderCompletedEventAsync (line 193)
var outboxEvent = new OutboxEvent(/* existing params */, correlationId: order.Id);
```

### Change 8: LoyaltyIssuanceRecord creation on AddPoints
```csharp
// In OrderWorkflowService.ProcessLoyaltyPointsAsync (line 428/449)
// After AddPoints succeeds (Alliance or Silo mode):
var issuanceRecord = new LoyaltyIssuanceRecord(tenantId, order.Id, customerId, adjustedPoints);
await _loyaltyIssuanceRecordRepo.AddAsync(issuanceRecord, ct);
```

### Change 9: IFeatureFlagService interface + implementation
```csharp
// 3_CoreHub/Services/IFeatureFlagService.cs
namespace VanAn.CoreHub.Services;

/// <summary>
/// VALCN v2.0 feature toggle — SystemAdmin ON/OFF runtime.
/// Keys: "Features:Enable{FeatureName}" → "true"/"false".
/// Default: DISABLED (returns false if setting doesn't exist) — preserves existing behavior.
/// Cached 30s. Admin UI: /admin/valcn-features (SystemAdmin role).
/// </summary>
public interface IFeatureFlagService
{
    Task<bool> IsEnabledAsync(string featureName, CancellationToken ct = default);
    Task<IReadOnlyList<FeatureFlagDto>> GetAllAsync(CancellationToken ct = default);
    Task SetEnabledAsync(string featureName, bool enabled, Guid updatedBy, CancellationToken ct = default);
}

public record FeatureFlagDto(string FeatureName, string DisplayName, string Description, string Phase, bool IsEnabled);
```

```csharp
// 3_CoreHub/Services/FeatureFlagService.cs
// Copy pattern from BackgroundServiceToggleService (line 15-111)
// CRITICAL DIFFERENCE: default = false (not true)
public class FeatureFlagService : IFeatureFlagService
{
    // Same IServiceScopeFactory + IMemoryCache + 30s cache pattern
    // KnownFeatures array (3 features listed above)

    public async Task<bool> IsEnabledAsync(string featureName, CancellationToken ct = default)
    {
        // Same cache + SystemSetting read pattern as BackgroundServiceToggleService
        // CRITICAL: bool enabled = value == "true"; // default: DISABLED (opposite of BG toggle)
        // ... (copy from BackgroundServiceToggleService.cs:38-58, flip default)
    }

    // GetAllAsync + SetEnabledAsync — same pattern, KnownFeatures instead of KnownServices
}
```

### Change 10: FeatureFlagsController (Gateway)
```csharp
// 2_Gateway/Controllers/FeatureFlagsController.cs
[Authorize(Roles = "SystemAdmin")]  // class-level — W12-G7 pattern
[ApiController]
[Route("api/admin/feature-flags")]
public class FeatureFlagsController : ControllerBase
{
    private readonly IFeatureFlagService _flagService;

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<FeatureFlagDto>>> GetAll(CancellationToken ct)
        => Ok(await _flagService.GetAllAsync(ct));

    [HttpPut("{featureName}")]
    public async Task<IActionResult> SetEnabled(string featureName, [FromBody] bool enabled, CancellationToken ct)
    {
        var userId = User.GetUserId();  // existing extension
        await _flagService.SetEnabledAsync(featureName, enabled, userId, ct);
        return NoContent();
    }
}
```

### Change 11: FeatureFlagHttpService (ShopERP)
```csharp
// 5_WebApps/ShopERP/Services/FeatureFlagHttpService.cs
// HTTP proxy to Gateway — same pattern as BackgroundServiceToggleApiClient
public class FeatureFlagHttpService(HttpClient httpClient, ILogger<FeatureFlagHttpService> logger)
{
    public async Task<IReadOnlyList<FeatureFlagDto>> GetAllAsync(CancellationToken ct) { ... }
    public async Task SetEnabledAsync(string featureName, bool enabled, CancellationToken ct) { ... }
}
```

### Change 12: ValcnFeatures.razor (Admin UI)
```razor
@* 5_WebApps/ShopERP/Components/Pages/Admin/ValcnFeatures.razor *@
@page "/admin/valcn-features"
@attribute [Authorize(Roles = "SystemAdmin")]
@* UI Platform components (mandatory per governance) *@

<h1>VALCN v2.0 Features</h1>
<p>Bật/tắt tính năng VALCN v2.0. Mặc định: TẮT (giữ behavior hiện tại cho đến khi bật).</p>

@foreach (var flag in flags)
{
    <div class="feature-toggle-card">
        <h3>@flag.DisplayName</h3>
        <p>@flag.Description</p>
        <p><em>Phase: @flag.Phase</em></p>
        <ToggleSwitch Value="flag.IsEnabled" ValueChanged="@(v => ToggleFeature(flag.FeatureName, v))" />
        <span>@(flag.IsEnabled ? "Đã bật" : "Đã tắt (mặc định)")</span>
    </div>
}
```

### Change 13: NavMenu link
```razor
@* Add to NavMenu.razor admin section *@
<NavLink href="/admin/valcn-features" class="nav-link">
    <span class="oi oi-toggle-on" aria-hidden="true"></span> VALCN v2.0 Features
</NavLink>
```

### Change 14: DI Registration
```csharp
// Gateway Program.cs
services.AddScoped<IFeatureFlagService, FeatureFlagService>();
// ShopERP Program.cs
services.AddHttpClient<FeatureFlagHttpService>(client => client.BaseAddress = new Uri(gatewayUrl));
```

## Verification
- [ ] `dotnet build VanAn.sln` Release — 0 errors
- [ ] `guard-check.ps1` — PASS
- [ ] Migration apply thành công trên dev DB
- [ ] Existing tests pass (backward compat — optional param default null)
- [ ] New AccountingEntry có `CorrelationId = Order.Id`
- [ ] New OutboxEvent có `CorrelationId = Order.Id`
- [ ] New LoyaltyIssuanceRecord created khi AddPoints
- [ ] `CreateReversal` preserves `CorrelationId` từ original
- [ ] Single-Identity Pattern audit: LoyaltyIssuanceRecord constructor syncs `Id = LoyaltyIssuanceRecordId.Value`
- [ ] **FeatureFlagService**: `IsEnabledAsync("ValcnV2_PlatformFee")` returns `false` when no SystemSetting row (default OFF)
- [ ] **FeatureFlagService**: After `SetEnabledAsync("ValcnV2_PlatformFee", true)`, `IsEnabledAsync` returns `true` within 30s (cache)
- [ ] **API**: `GET /api/admin/feature-flags` — 401 without JWT, 403 without SystemAdmin, 200 with SystemAdmin → returns 3 features
- [ ] **API**: `PUT /api/admin/feature-flags/ValcnV2_PlatformFee` — toggles feature
- [ ] **UI**: `/admin/valcn-features` renders 3 toggle switches (all OFF by default)
- [ ] **UI**: NavMenu has "VALCN v2.0 Features" link in admin section
- [ ] **Architecture test**: FeatureFlagsController has class-level `[Authorize]` (W12-G7 pattern)

## Rollback
```bash
dotnet ef migrations remove  # if not applied
dotnet ef database update <PreviousMigration>  # if applied
git revert <commit>
```
Tất cả fields nullable/default + new entity → rollback = drop columns + drop table, 0 data loss.

---

## ANALYZE UPDATE (to be filled during INVESTIGATE step)

### INVESTIGATE checklist
- [ ] Verify `AccountingEntry` line range (Domain.cs:287-397)
- [ ] Verify all 4 factory methods signatures (CreateRevenue, CreateExpense, CreateReversal, CreateReversalWithId)
- [ ] Verify `AccountingEntryDto` has no CorrelationId (AccountingEntryDto.cs:9-38)
- [ ] Find `AccountingService.CreateEntryAsync` — confirm pass-through path
- [ ] Verify `SimpleAccountingEventHandler` line 114-125
- [ ] Find `OutboxEvent` constructor — does it accept optional params?
- [ ] Find `LoyaltyTenantConfig` EF config file
- [ ] Find `TenantSettings` EF config file
- [ ] Find `OutboxEvent` EF config file
- [ ] Find `Order` EF config file
- [ ] Find migration project path
- [ ] Find `LoyaltyRewardsRepository` — pattern for new `LoyaltyIssuanceRecordRepository`
- [ ] Verify `BaseEntity` — constructor signature for new entity
- [ ] Grep existing `LoyaltyIssuanceRecord` to confirm not exists
- [ ] Read `BackgroundServiceToggleService.cs` (line 15-111) — pattern reference for FeatureFlagService
- [ ] Read `BackgroundServicesController.cs` — pattern reference for FeatureFlagsController
- [ ] Read `BackgroundServiceToggleApiClient.cs` — pattern reference for FeatureFlagHttpService
- [ ] Read `BackgroundServicesManagement.razor` — pattern reference for ValcnFeatures.razor
- [ ] Find NavMenu.razor — admin section for link placement
- [ ] Verify `SystemSetting` entity — key/value/updatedBy fields for feature flag storage
- [ ] Confirm `User.GetUserId()` extension method exists (for controller)

### Verified Accurate
- (fill after investigation)

### DRIFT
- (fill if investigation finds drift)
