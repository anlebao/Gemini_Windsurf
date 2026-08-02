# Loyalty Point Storage Consistency — Detail Coding Plan

**Created:** 2026-08-02
**Status:** COMPLETE — superseded by `loyalty-consistency-fix-master-plan.md` (rolled into master plan + 9 review gaps)
**Mode:** FIX_ONLY (consistency fixes, no new features)
**Reference:** Codebase review of loyalty point storage consistency
**Implementation:** Layer 1 (commit `0f924ec9` + `aa4d008c` + `8d7e2c25`) + Layer 2 (commit `70897151`) — VPS RV 37/37 PASS

---

## Problem Summary

Points are stored in **2 parallel systems** routed by `LoyaltyModeResolver.GetEffectiveModeAsync(tenantId)`:

| System | Database | Mode | Source of truth |
|---|---|---|---|
| `LoyaltyRewards` | SQLite (per-tenant, ShopERP) | Silo | Yes in Silo |
| `AllianceWallet` + `AllianceTransaction` | PostgreSQL (Gateway, cross-tenant) | Alliance | Yes in Alliance |

Sync PG→SQLite is best-effort via NATS `LoyaltySyncSubscriber` (fire-and-forget, not transactional).

**5 inconsistencies identified** — all stem from missing Alliance mode routing in point write/read paths.

---

## Critical Pre-Existing Bug: DI Registration Gap (BUG #0)

**Severity:** CRITICAL — Alliance mode is completely non-functional in production

**Root cause:** `ILoyaltyModeResolver` and `IAllianceWalletService` are registered ONLY in Gateway `Program.cs` (lines 263-264). They are NOT registered in ShopERP `Program.cs`.

**Impact:** `OrderWorkflowService` and `RedemptionService` both run inside the ShopERP process and declare these as optional constructor params (`= null` default). Since DI doesn't find a registration, it injects `null` → the Alliance mode check (`if (_loyaltyModeResolver is not null && _allianceWalletService is not null)`) always evaluates to `false` → **all point operations fall through to Silo SQLite**, even when the tenant is configured for Alliance mode.

**Why tests pass:** Tests (`OrderWorkflowAllianceTests`, `RedemptionAllianceTests`) explicitly register mocked `ILoyaltyModeResolver` + `IAllianceWalletService` in their test ServiceProvider — they never test the real DI registration path.

**Fix:** Register `ILoyaltyModeResolver` and `IAllianceWalletService` in ShopERP `Program.cs` so they're available to `OrderWorkflowService`, `RedemptionService`, and `MissionService`.

### File: `5_WebApps/ShopERP/Program.cs`
**Insert after line 360 (after MissionService registration, before BirthdayBonusJob):**

```csharp
// Loyalty Alliance System — Phase 2A: mode resolver + cross-tenant wallet service
// REQUIRED in ShopERP so OrderWorkflowService, RedemptionService, MissionService can route
// point operations to PG AllianceWallet when tenant is in Alliance mode.
// Without this, Alliance mode routing is silently skipped (null injection → Silo fallback).
_ = builder.Services.AddScoped<VanAn.Shared.Services.ILoyaltyModeResolver, VanAn.CoreHub.Services.LoyaltyModeResolver>();
_ = builder.Services.AddScoped<VanAn.Shared.Services.IAllianceWalletService, VanAn.CoreHub.Services.AllianceWalletService>();
```

**Note:** `AllianceWalletService` and `LoyaltyModeResolver` both depend on `IVanAnDbContext`. ShopERP's `IVanAnDbContext` is the SQLite `VanAnDbContext`. This means `AllianceWalletService` will try to query `AllianceWallets` / `AllianceTransactions` / `LoyaltyTenantConfigs` / `LoyaltyGlobalConfigs` tables from SQLite — but these are PG-only tables.

**Sub-issue:** `AllianceWalletService` and `LoyaltyModeResolver` need a **PG DbContext**, not the ShopERP SQLite DbContext. Options:
- **Option A (preferred):** Add a separate `IPgVanAnDbContext` interface + `PgVanAnDbContext` implementation registered in ShopERP DI, pointing to the PG connection string. `AllianceWalletService` and `LoyaltyModeResolver` depend on `IPgVanAnDbContext` instead of `IVanAnDbContext`.
- **Option B:** Register a named HttpClient in ShopERP that calls Gateway's PG-backed Alliance endpoints. `AllianceWalletService` in ShopERP is replaced with an HTTP client wrapper.

**Recommendation:** Option A is cleaner (no HTTP hop, transactional integrity). But requires creating `IPgVanAnDbContext` + registering it. Option B adds latency but is simpler to implement.

**Decision needed from user:** Option A (PG DbContext in ShopERP) or Option B (HTTP client to Gateway)?

---

## BUG #1: MissionService — mission points only write SQLite (CRITICAL)

**File:** `3_CoreHub/Services/MissionService.cs`
**Lines affected:** 19-36 (constructor), 131, 252

**Root cause:** `MissionService` constructor does not inject `ILoyaltyModeResolver` or `IAllianceWalletService`. Both `CompleteMissionAsync` (line 131) and `CompleteAnnualMissionAsync` (line 252) call `_loyaltyRewardsService.AddPointsAsync()` unconditionally → always writes to SQLite, never to PG AllianceWallet.

**Impact:** In Alliance mode, mission points are "phantom" — they exist in SQLite but not in the cross-tenant PG wallet. Customer's AllianceWallet balance is lower than it should be.

### Fix: Add Alliance mode routing to MissionService

#### Step 1: Add constructor params (line 19-27)

```csharp
public class MissionService(
    IMissionRepository repository,
    ICustomerRepository customerRepository,
    ILoyaltyRewardsService loyaltyRewardsService,
    ITenantProvider tenantProvider,
    IVanAnDbContext dbContext,
    IShopFeatureSettingsService? shopFeatureSettingsService,
    PushNotificationService? pushNotificationService,
    ILogger<MissionService> logger,
    ILoyaltyModeResolver? loyaltyModeResolver = null,      // NEW
    IAllianceWalletService? allianceWalletService = null)   // NEW
    : IMissionService
{
    // ... existing fields ...
    private readonly ILoyaltyModeResolver? _loyaltyModeResolver = loyaltyModeResolver;
    private readonly IAllianceWalletService? _allianceWalletService = allianceWalletService;
```

#### Step 2: Add Alliance routing helper method (after constructor)

```csharp
/// <summary>
/// Loyalty Alliance: Route point award to PG AllianceWallet (Alliance mode) or SQLite (Silo mode).
/// Returns (success, newBalance). In Alliance mode, writes to PG only.
/// In Silo mode, calls the existing AddPointsAsync (SQLite).
/// </summary>
private async Task<(bool Success, int NewBalance)> AwardPointsWithModeRoutingAsync(
    Guid customerId, int points, string reason)
{
    if (_loyaltyModeResolver is not null && _allianceWalletService is not null)
    {
        Guid tenantId = _tenantProvider.TenantId;
        LoyaltyMode effectiveMode = await _loyaltyModeResolver.GetEffectiveModeAsync(tenantId);
        if (effectiveMode == LoyaltyMode.Alliance)
        {
            bool isMember = await _loyaltyModeResolver.IsAllianceMemberAsync(tenantId);
            if (isMember)
            {
                var customer = await _customerRepository.GetByIdAsync(customerId);
                Guid deviceGuid = customer?.DeviceId ?? customerId;
                var (success, newBalance, error) = await _allianceWalletService.AddPointsAsync(
                    deviceGuid, tenantId, points, reason);
                if (!success)
                {
                    _logger.LogWarning("Alliance mission award failed for customer {CustomerId}: {Error}", customerId, error);
                    return (false, 0);
                }
                _logger.LogInformation("🎁 ALLIANCE MISSION: {Points} points to wallet for device {DeviceId} (balance={Balance})",
                    points, deviceGuid, newBalance);
                return (true, newBalance);
            }
            _logger.LogInformation("Mission: Tenant {TenantId} not alliance member — Silo earn", tenantId);
        }
    }

    // Silo fallback
    bool awarded = await _loyaltyRewardsService.AddPointsAsync(customerId, points, reason);
    if (awarded)
    {
        var rewards = await _loyaltyRewardsService.GetCustomerRewardsAsync(customerId);
        return (true, rewards?.PointBalance ?? 0);
    }
    return (false, 0);
}
```

#### Step 3: Replace line 131 in `CompleteMissionAsync`

**Before:**
```csharp
bool awarded = await _loyaltyRewardsService.AddPointsAsync(customerId, mission.PointsReward, $"Mission: {mission.Title}");
if (!awarded) { ... rollback ... }
```

**After:**
```csharp
var (awarded, newBalance) = await AwardPointsWithModeRoutingAsync(customerId, mission.PointsReward, $"Mission: {mission.Title}");
if (!awarded) { ... rollback ... }
```

Also update the `newBalance` read at line 155-156 — in Alliance mode, `GetCustomerRewardsAsync` returns the SQLite mirror (may be stale). Use `newBalance` from the routing result instead.

#### Step 4: Replace line 252 in `CompleteAnnualMissionAsync`

Same pattern as Step 3, using `"Annual mission: {mission.Title} ({currentYear})"` as reason.

#### Step 5: Update notification balance (lines 155-156, 264-265)

In Alliance mode, the balance returned by `AwardPointsWithModeRoutingAsync` is the PG wallet balance. Use it directly instead of re-reading from SQLite:

```csharp
// Use balance from routing result (PG in Alliance, SQLite in Silo)
int newBalance = awarded ? newBalanceFromRouting : 0;
```

---

## BUG #2: RedemptionService.CancelAsync — refund only writes SQLite (CRITICAL)

**File:** `3_CoreHub/Services/RedemptionService.cs`
**Line:** 322

**Root cause:** `CancelAsync` calls `_loyaltyRewardsService.AddPointsAsync()` to refund. No Alliance mode check. If the original redeem deducted from PG (Alliance mode), the refund goes to SQLite instead of PG → customer's PG wallet is not refunded.

**Impact:** Customer loses points in PG wallet after a cancelled redemption. SQLite gets phantom points that don't exist in PG.

### Fix: Add Alliance mode routing to CancelAsync

`RedemptionService` already has `_loyaltyModeResolver` and `_allianceWalletService` injected (lines 31-32). The fix is to add mode routing in `CancelAsync`.

#### Step 1: Add refund routing helper

```csharp
/// <summary>
/// Loyalty Alliance: Route point refund to PG AllianceWallet (Alliance mode) or SQLite (Silo mode).
/// Uses IAllianceWalletService.RefundAsync in Alliance mode, AddPointsAsync in Silo mode.
/// </summary>
private async Task<bool> RefundPointsWithModeRoutingAsync(Guid customerId, Guid tenantId, int points, string reason, string? voucherCode = null)
{
    if (_loyaltyModeResolver is not null && _allianceWalletService is not null)
    {
        LoyaltyMode effectiveMode = await _loyaltyModeResolver.GetEffectiveModeAsync(tenantId);
        if (effectiveMode == LoyaltyMode.Alliance)
        {
            bool isMember = await _loyaltyModeResolver.IsAllianceMemberAsync(tenantId);
            if (isMember)
            {
                var customer = await _dbContext.Customers.FirstOrDefaultAsync(c => c.Id == customerId);
                Guid deviceGuid = customer?.DeviceId ?? customerId;
                var (success, _, error) = await _allianceWalletService.RefundAsync(
                    deviceGuid, tenantId, points, reason, voucherCode ?? "CANCEL");
                if (!success)
                {
                    _logger.LogWarning("Alliance refund failed for customer {CustomerId}: {Error}", customerId, error);
                    return false;
                }
                _logger.LogInformation("🎁 ALLIANCE REFUND: {Points} points refunded to wallet for device {DeviceId}", points, deviceGuid);
                return true;
            }
        }
    }

    // Silo fallback
    return await _loyaltyRewardsService.AddPointsAsync(customerId, points, reason);
}
```

#### Step 2: Replace line 322 in `CancelAsync`

**Before:**
```csharp
_ = await _loyaltyRewardsService.AddPointsAsync(record.CustomerId, record.PointsSpent, $"Refund: cancelled redemption {redemptionRecordId}");
```

**After:**
```csharp
string? voucherCode = cancelledVoucher?.VoucherCode;
_ = await RefundPointsWithModeRoutingAsync(record.CustomerId, record.TenantId.Value, record.PointsSpent, $"Refund: cancelled redemption {redemptionRecordId}", voucherCode);
```

**Note:** Need to move the voucher lookup BEFORE the refund call so the voucher code is available for PG refund audit trail. Currently the voucher lookup is at line 329-337 (after the refund). Restructure:

```csharp
// 1. Fetch voucher first (needed for refund audit + expiry marking)
Voucher? cancelledVoucher = null;
if (record.VoucherId.HasValue)
{
    cancelledVoucher = await _repository.GetVoucherByIdAsync(record.VoucherId.Value);
}

// 2. Refund points (route by mode)
_ = await RefundPointsWithModeRoutingAsync(record.CustomerId, record.TenantId.Value, record.PointsSpent, $"Refund: cancelled redemption {redemptionRecordId}", cancelledVoucher?.VoucherCode);

// 3. Mark record as cancelled
record.MarkAsCancelled(notes);
_ = await _repository.UpdateRecordAsync(record);

// 4. Mark voucher as expired
if (cancelledVoucher != null && cancelledVoucher.Status == "Active")
{
    cancelledVoucher.MarkAsExpired();
    _ = await _repository.UpdateVoucherAsync(cancelledVoucher);
}
```

---

## BUG #3: ShopERP LoyaltyController.Redeem — deducts SQLite only (CRITICAL)

**File:** `5_WebApps/ShopERP/Controllers/LoyaltyController.cs`
**Line:** 94

**Root cause:** `POST /api/loyalty/redeem` calls `_loyaltyService.SubtractPointsAsync()` directly. No Alliance mode check. In Alliance mode, this deducts from SQLite instead of PG wallet.

**Note:** This endpoint is the "legacy" raw point deduction (deduct N points, no catalog item, no voucher). The catalog-based redeem (`POST /api/redemption/redeem`) already has Alliance routing in `RedemptionService`. But this legacy endpoint is still exposed and callable.

**Impact:** Customer can bypass PG wallet and deduct from SQLite "phantom" balance in Alliance mode.

### Fix: Add Alliance mode routing to LoyaltyController.Redeem

#### Step 1: Add constructor params

```csharp
public class LoyaltyController(
    ILoyaltyRewardsService loyaltyService,
    ICustomerTokenService customerTokenService,
    ICustomerRepository customerRepository,
    ILogger<LoyaltyController> logger,
    ILoyaltyModeResolver? loyaltyModeResolver = null,      // NEW
    IAllianceWalletService? allianceWalletService = null)   // NEW
    : ControllerBase
```

#### Step 2: Add fields

```csharp
private readonly ILoyaltyModeResolver? _loyaltyModeResolver = loyaltyModeResolver;
private readonly IAllianceWalletService? _allianceWalletService = allianceWalletService;
```

#### Step 3: Add Alliance routing in Redeem action (replace line 92-106)

```csharp
try
{
    // === Loyalty Alliance: Mode routing for point deduction ===
    if (_loyaltyModeResolver is not null && _allianceWalletService is not null)
    {
        LoyaltyMode effectiveMode = await _loyaltyModeResolver.GetEffectiveModeAsync(customer.TenantId.Value);
        if (effectiveMode == LoyaltyMode.Alliance)
        {
            bool isMember = await _loyaltyModeResolver.IsAllianceMemberAsync(customer.TenantId.Value);
            if (isMember)
            {
                Guid deviceGuid = customer.DeviceId ?? customerId.Value;
                var (success, newBalance, error) = await _allianceWalletService.DeductPointsAsync(
                    deviceGuid, customer.TenantId.Value, request.Points, request.Reason);
                if (!success)
                {
                    return BadRequest(new { error = error ?? "Không đủ điểm để đổi." });
                }
                return Ok(new RedeemResponse
                {
                    Success = true,
                    NewBalance = newBalance,
                    PointsRedeemed = request.Points
                });
            }
        }
    }

    // === Silo fallback (existing code) ===
    var successSilo = await _loyaltyService.SubtractPointsAsync(customerId.Value, request.Points, request.Reason);
    if (!successSilo)
    {
        return BadRequest(new { error = "Không đủ điểm để đổi. Vui lòng kiểm tra số dư." });
    }
    var rewards = await _loyaltyService.GetCustomerRewardsAsync(customerId.Value);
    return Ok(new RedeemResponse
    {
        Success = true,
        NewBalance = rewards?.PointBalance ?? 0,
        PointsRedeemed = request.Points
    });
}
```

**Note:** The IdentityLevel gate (lines 79-90) stays BEFORE the mode routing — Verified identity is required regardless of mode.

---

## BUG #4: RedemptionCatalog displays SQLite balance, not PG balance (MEDIUM)

**File:** `5_WebApps/KhachLink/Pages/RedemptionCatalog.razor`
**Line:** 164

**Root cause:** The `/rewards` page loads point balance from `/api/loyalty/my` (ShopERP SQLite). In Alliance mode, SQLite is a best-effort NATS mirror — may be stale or missing. Customer sees wrong balance and may be blocked from redeeming despite having enough PG points.

### Fix: Load PG wallet balance in Alliance mode

#### Option A (preferred): Add a new endpoint `/api/loyalty/my-balance` that returns the correct balance by mode

**Gateway LoyaltyController.cs — add new endpoint:**

```csharp
/// <summary>
/// GET /api/loyalty/my-balance — returns the customer's effective point balance.
/// In Alliance mode: queries PG AllianceWallet (source of truth).
/// In Silo mode: forwards to ShopERP /api/loyalty/my (SQLite source of truth).
/// Used by KhachLink RedemptionCatalog to display the correct balance before redeeming.
/// </summary>
[HttpGet("my-balance")]
public async Task<IActionResult> GetMyBalance()
{
    try
    {
        if (!Request.Headers.TryGetValue("X-Customer-Token", out var token) || string.IsNullOrEmpty(token))
            return Unauthorized(new { error = "Thiếu X-Customer-Token header." });

        // Resolve customer identity via ShopERP
        var client = _httpClientFactory.CreateClient("shoperp");
        var identityReq = new HttpRequestMessage(HttpMethod.Get, "/api/loyalty/my-identity");
        identityReq.Headers.Add("X-Customer-Token", token.ToString());
        var identityResp = await client.SendAsync(identityReq);

        if (!identityResp.IsSuccessStatusCode)
            return new ContentResult { StatusCode = (int)identityResp.StatusCode, Content = await identityResp.Content.ReadAsStringAsync(), ContentType = "application/json" };

        var identityJson = await identityResp.Content.ReadAsStringAsync();
        using var identityDoc = JsonDocument.Parse(identityJson);
        var deviceIdToken = identityDoc.RootElement.GetProperty("deviceId");

        if (deviceIdToken.ValueKind == JsonValueKind.Null || deviceIdToken.GetGuid() == Guid.Empty)
        {
            // No device identity — fall back to Silo balance from ShopERP
            var siloReq = new HttpRequestMessage(HttpMethod.Get, "/api/loyalty/my");
            siloReq.Headers.Add("X-Customer-Token", token.ToString());
            var siloResp = await client.SendAsync(siloReq);
            var siloContent = await siloResp.Content.ReadAsStringAsync();
            return new ContentResult { StatusCode = (int)siloResp.StatusCode, Content = siloContent, ContentType = "application/json" };
        }

        Guid deviceId = deviceIdToken.GetGuid();

        // Check mode for the customer's tenant
        // Resolve tenantId from identity response
        // (Need to add tenantId to /api/loyalty/my-identity response — see below)
        Guid tenantId = identityDoc.RootElement.GetProperty("tenantId").GetGuid();
        LoyaltyMode mode = await _allianceWalletService.GetWalletByDeviceIdAsync(deviceId) != null
            ? LoyaltyMode.Alliance  // Wallet exists → Alliance mode
            : LoyaltyMode.Silo;     // No wallet → Silo

        if (mode == LoyaltyMode.Alliance)
        {
            var wallet = await _allianceWalletService.GetWalletByDeviceIdAsync(deviceId);
            return Ok(new { pointBalance = wallet?.TotalPointBalance ?? 0, mode = "Alliance" });
        }

        // Silo: forward to ShopERP
        var siloReq2 = new HttpRequestMessage(HttpMethod.Get, "/api/loyalty/my");
        siloReq2.Headers.Add("X-Customer-Token", token.ToString());
        var siloResp2 = await client.SendAsync(siloReq2);
        var siloContent2 = await siloResp2.Content.ReadAsStringAsync();
        return new ContentResult { StatusCode = (int)siloResp2.StatusCode, Content = siloContent2, ContentType = "application/json" };
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "Error getting my-balance");
        return StatusCode(500, new { error = "Internal server error" });
    }
}
```

**ShopERP LoyaltyController.cs — add tenantId to my-identity response (line 144-148):**

```csharp
return Ok(new
{
    customerId = customer.Id,
    deviceId = customer.DeviceId,
    phoneNumber = customer.PhoneNumber,
    tenantId = customer.TenantId.Value  // NEW — needed by Gateway for mode check
});
```

#### Option B (simpler): KhachLink calls `/api/loyalty/wallet` (PG) first, falls back to `/api/loyalty/my` (SQLite)

**RedemptionCatalog.razor — replace line 164:**

```csharp
// Try PG Alliance wallet first (source of truth in Alliance mode)
var walletTask = SendWithTokenAsync(client, "/api/loyalty/wallet", token);
var loyaltyTask = SendWithTokenAsync(client, "/api/loyalty/my", token);

await Task.WhenAll(walletTask, loyaltyTask);

var walletResp = await walletTask;
if (walletResp.IsSuccessStatusCode)
{
    var wallet = await walletResp.Content.ReadFromJsonAsync<AllianceWalletResponse>();
    if (wallet != null)
    {
        _pointBalance = wallet.TotalPointBalance;
    }
}

// Fallback to Silo balance if wallet not available
if (_pointBalance == 0)
{
    var loyaltyResp = await loyaltyTask;
    if (loyaltyResp.IsSuccessStatusCode)
    {
        var loyalty = await loyaltyResp.Content.ReadFromJsonAsync<LoyaltyMyResponse>();
        _pointBalance = loyalty?.PointBalance ?? 0;
    }
}
```

**Recommendation:** Option B is simpler and avoids a new endpoint. But it always calls both endpoints (2x latency). Option A is cleaner (1 call) but requires more code.

**Decision needed from user:** Option A (new `/api/loyalty/my-balance` endpoint) or Option B (dual-call with fallback)?

---

## BUG #5: LoyaltyCard page — same SQLite/PG display inconsistency (MEDIUM)

**File:** `5_WebApps/KhachLink/Pages/LoyaltyCard.razor`
**Line:** 182

**Root cause:** Same as BUG #4. `LoyaltyCard.razor` loads from `/api/loyalty/my` (SQLite). In Alliance mode, this may be stale.

**Fix:** Same as BUG #4 — apply the same balance-loading pattern (Option A or B) to `LoyaltyCard.razor`.

Also applies to:
- `Missions.razor` line 250
- `Profile.razor` line 391 (via profile API)
- `Checkout.razor` (if it displays points)

---

## Implementation Order

1. **BUG #0 (DI registration)** — CRITICAL, must fix first. All other fixes depend on this working.
2. **BUG #1 (MissionService)** — CRITICAL, customers lose mission points in Alliance mode.
3. **BUG #2 (RedemptionService.CancelAsync)** — CRITICAL, refunds go to wrong system.
4. **BUG #3 (LoyaltyController.Redeem)** — CRITICAL, legacy redeem endpoint bypasses PG.
5. **BUG #4 (RedemptionCatalog display)** — MEDIUM, UX issue with stale balance.
6. **BUG #5 (LoyaltyCard + other pages)** — MEDIUM, same UX issue.

---

## Test Plan

### Unit Tests (new)

1. **MissionServiceAllianceTests** — verify mission points route to PG in Alliance mode
   - `CompleteMission_AllianceMode_AddPointsToPGWallet`
   - `CompleteMission_SiloMode_AddPointsToSQLite`
   - `CompleteMission_AllianceMode_NotMember_FallsBackToSilo`
   - `CompleteAnnualMission_AllianceMode_AddPointsToPGWallet`

2. **RedemptionCancelAllianceTests** — verify refund routes to PG in Alliance mode
   - `Cancel_AllianceMode_RefundsToPGWallet`
   - `Cancel_SiloMode_RefundsToSQLite`
   - `Cancel_AllianceMode_NotMember_FallsBackToSilo`

3. **LoyaltyControllerRedeemAllianceTests** — verify legacy redeem routes to PG
   - `Redeem_AllianceMode_DeductsFromPGWallet`
   - `Redeem_SiloMode_DeductsFromSQLite`

### Integration Tests

4. **DI Registration Test** — verify `ILoyaltyModeResolver` and `IAllianceWalletService` are resolvable from ShopERP's ServiceProvider.

### Existing Tests (verify still pass)

5. `OrderWorkflowAllianceTests` — 4 tests (earn routing)
6. `RedemptionAllianceTests` — existing Alliance redeem tests
7. `LoyaltyRewardsServiceVerificationGateTests` — IdentityLevel gate
8. `LoyaltySyncSubscriberTests` — NATS sync

---

## Risk Assessment

| Risk | Likelihood | Impact | Mitigation |
|---|---|---|---|
| BUG #0 fix: PG DbContext in ShopERP breaks SQLite queries | Medium | High | Use separate `IPgVanAnDbContext` or HTTP client wrapper. Test with real PG connection. |
| MissionService Alliance routing: PG write fails silently | Low | Medium | Log warning + return false → caller rolls back transaction. |
| CancelAsync restructure: voucher lookup order change | Low | Low | Keep same logic, just reorder. Test covers it. |
| Balance display: 2x latency from dual endpoint call | Low | Low | Use Option A (single endpoint) if latency is a concern. |

---

## Open Questions for User

1. **BUG #0 DI fix approach:** Option A (separate PG DbContext in ShopERP) or Option B (HTTP client to Gateway Alliance endpoints)?
2. **BUG #4 balance display:** Option A (new `/api/loyalty/my-balance` endpoint) or Option B (dual-call with fallback)?
3. **Should the legacy `POST /api/loyalty/redeem` endpoint be deprecated/removed** instead of fixed? The catalog-based `POST /api/redemption/redeem` already has Alliance routing. If the legacy endpoint is unused, fixing it (BUG #3) adds complexity for no benefit.

---

## Files to Modify

| File | Bug(s) | Changes |
|---|---|---|
| `5_WebApps/ShopERP/Program.cs` | #0 | Register `ILoyaltyModeResolver` + `IAllianceWalletService` |
| `3_CoreHub/Services/MissionService.cs` | #1 | Add constructor params + routing helper + replace 2 AddPointsAsync calls |
| `3_CoreHub/Services/RedemptionService.cs` | #2 | Add refund routing helper + restructure CancelAsync voucher order |
| `5_WebApps/ShopERP/Controllers/LoyaltyController.cs` | #3 | Add constructor params + Alliance routing in Redeem action |
| `5_WebApps/KhachLink/Pages/RedemptionCatalog.razor` | #4 | Load PG balance (Option A or B) |
| `5_WebApps/KhachLink/Pages/LoyaltyCard.razor` | #5 | Same as #4 |
| `5_WebApps/KhachLink/Pages/Missions.razor` | #5 | Same as #4 |
| `2_Gateway/Controllers/LoyaltyController.cs` | #4 (Option A only) | Add `/api/loyalty/my-balance` endpoint |
| `5_WebApps/ShopERP/Controllers/LoyaltyController.cs` | #4 (Option A only) | Add tenantId to my-identity response |
| `6_Tests/VanAn.Core.Tests/Services/MissionServiceAllianceTests.cs` | #1 | NEW test file |
| `6_Tests/VanAn.Core.Tests/Services/RedemptionCancelAllianceTests.cs` | #2 | NEW test file |
| `6_Tests/VanAn.Core.Tests/Services/LoyaltyControllerRedeemAllianceTests.cs` | #3 | NEW test file |
