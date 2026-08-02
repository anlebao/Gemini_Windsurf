# Loyalty Point Storage Consistency — Detail Coding Plan

**Master plan:** `loyalty-consistency-fix-master-plan.md`
**Task cards:** `loyalty-consistency-fix-task-cards.md`
**Architecture:** Option B — HTTP proxy + cache + idempotency (multi-VPS ready)
**All decisions D1-D5 approved.**

---

## Phase 0 — Session 1: HTTP Proxy Infrastructure (BUG #0)

### 0.1 Domain: Add IdempotencyKey to AllianceTransaction

**File: `1_Shared/Domain.cs` — `AllianceTransaction` class**
Add property + update constructor:
```csharp
public class AllianceTransaction : BaseEntity
{
    // ... existing properties ...
    public string? IdempotencyKey { get; protected set; }  // NEW — for retry-safe HTTP proxy

    // Update existing constructor — add optional param at end
    public AllianceTransaction(
        AllianceTransactionId walletId, TenantId transactionTenantId,
        AllianceTransactionType type, int points, int balanceAfter,
        string reason, Guid? sourceOrderId = null,
        string? voucherCode = null, TenantId? refundTenantId = null,
        string? idempotencyKey = null)                       // NEW
    {
        // ... existing assignments ...
        IdempotencyKey = idempotencyKey;
    }
}
```

### 0.2 EF Config: Map + Index IdempotencyKey

**File: `3_CoreHub/Infrastructure/Configurations/AllianceTransactionConfiguration.cs`**
```csharp
// Add to OnModelCreating / Configure:
builder.Property(t => t.IdempotencyKey).HasMaxLength(200).IsRequired(false);
builder.HasIndex(t => t.IdempotencyKey).HasDatabaseName("IX_AllianceTransactions_IdempotencyKey");
// Non-unique index — most transactions have NULL idempotencyKey; only ShopERP-proxied calls set it
```

**Migration:**
```bash
cd 3_CoreHub
dotnet ef migrations add AddAllianceTransactionIdempotencyKey --output-dir Infrastructure/Migrations
# Review migration file before applying
```

### 0.3 Interface: Add idempotencyKey Optional Param

**File: `1_Shared/Services/IAllianceWalletService.cs`**
```csharp
Task<(bool Success, int NewBalance, string? Error)> AddPointsAsync(
    Guid customerDeviceId, Guid tenantId, int points, string reason,
    Guid? sourceOrderId = null, string? idempotencyKey = null);  // ADD idempotencyKey

Task<(bool Success, int NewBalance, string? Error)> DeductPointsAsync(
    Guid customerDeviceId, Guid tenantId, int points, string reason,
    string? voucherCode = null, string? idempotencyKey = null);   // ADD idempotencyKey

Task<(bool Success, int NewBalance, string? Error)> RefundAsync(
    Guid customerDeviceId, Guid tenantId, int points, string reason,
    string voucherCode, string? idempotencyKey = null);           // ADD idempotencyKey
```

### 0.4 Real Implementation: Idempotency Check

**File: `3_CoreHub/Services/AllianceWalletService.cs`**
```csharp
public async Task<(bool Success, int NewBalance, string? Error)> AddPointsAsync(
    Guid customerDeviceId, Guid tenantId, int points, string reason,
    Guid? sourceOrderId = null, string? idempotencyKey = null)    // ADD param
{
    if (points <= 0) return (false, 0, "Points must be positive");

    // IDEMPOTENCY CHECK — if key provided and already processed, return cached result
    if (idempotencyKey is not null)
    {
        var existing = await _dbContext.AllianceTransactions
            .FirstOrDefaultAsync(t => t.IdempotencyKey == idempotencyKey);
        if (existing is not null)
        {
            _logger.LogInformation("Idempotency hit: key={Key} → returning cached balance={Balance}", idempotencyKey, existing.BalanceAfter);
            return (true, existing.BalanceAfter, null);
        }
    }

    AllianceWallet wallet = await GetOrCreateWalletAsync(customerDeviceId, phoneNumber: null);
    int maxWallet = await _modeResolver.GetEffectiveMaxWalletPointsAsync(tenantId);
    if (wallet.TotalPointBalance + points > maxWallet)
        return (false, wallet.TotalPointBalance, $"Wallet cap exceeded: {wallet.TotalPointBalance} + {points} > {maxWallet}");

    wallet.AddPoints(points);
    var tx = new AllianceTransaction(
        walletId: wallet.Id, transactionTenantId: tenantId,
        type: AllianceTransactionType.EARN, points: points,
        balanceAfter: wallet.TotalPointBalance, reason: reason,
        sourceOrderId: sourceOrderId, idempotencyKey: idempotencyKey);  // PASS key
    _ = _dbContext.AllianceTransactions.Add(tx);
    await _dbContext.SaveChangesAsync();
    await PublishLoyaltyChangedAsync(customerDeviceId, wallet.TotalPointBalance, tx);
    return (true, wallet.TotalPointBalance, null);
}
```
Apply same idempotency-check pattern to `DeductPointsAsync` + `RefundAsync`. Also update `PublishLoyaltyChangedAsync` signature to accept the `AllianceTransaction tx` (for extended payload in Phase 3 / BUG #9).

### 0.5 Gateway Internal API Key Auth

**File: `2_Gateway/Filters/InternalApiKeyAttribute.cs` (NEW)**
```csharp
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.Configuration;

namespace VanAn.Gateway.Filters;

public class InternalApiKeyAttribute : Attribute, IAsyncAuthorizationFilter
{
    public async Task OnAuthorizationAsync(AuthorizationFilterContext context)
    {
        var config = context.HttpContext.RequestServices.GetRequiredService<IConfiguration>();
        string? expectedKey = config["InternalLoyalty:ApiKey"];

        if (string.IsNullOrEmpty(expectedKey))
        {
            context.Result = new StatusCodeResult(503); // Service not configured
            return;
        }

        if (!context.HttpContext.Request.Headers.TryGetValue("X-Internal-Api-Key", out var provided) ||
            provided.ToString() != expectedKey)
        {
            context.Result = new UnauthorizedResult();
            return;
        }
        await Task.CompletedTask;
    }
}
```

### 0.6 Gateway Internal Controller

**File: `2_Gateway/Controllers/InternalLoyaltyController.cs` (NEW)**
```csharp
using Microsoft.AspNetCore.Mvc;
using VanAn.Gateway.Filters;
using VanAn.Shared.Domain;
using VanAn.Shared.Services;

namespace VanAn.Gateway.Controllers;

[ApiController]
[Route("api/internal/loyalty")]
[InternalApiKey]
public class InternalLoyaltyController(
    ILoyaltyModeResolver modeResolver,
    IAllianceWalletService walletService,
    ILogger<InternalLoyaltyController> logger) : ControllerBase
{
    /// <summary>GET /api/internal/loyalty/effective-config/{tenantId}</summary>
    [HttpGet("effective-config/{tenantId}")]
    public async Task<IActionResult> GetEffectiveConfig(Guid tenantId)
    {
        var mode = await modeResolver.GetEffectiveModeAsync(tenantId);
        var maxWallet = await modeResolver.GetEffectiveMaxWalletPointsAsync(tenantId);
        var isMember = await modeResolver.IsAllianceMemberAsync(tenantId);
        return Ok(new { mode = mode.ToString(), maxWalletPoints = maxWallet, isAllianceMember = isMember });
    }

    /// <summary>POST /api/internal/loyalty/points/add</summary>
    [HttpPost("points/add")]
    public async Task<IActionResult> AddPoints([FromBody] AddPointsRequest req)
    {
        var (success, balance, error) = await walletService.AddPointsAsync(
            req.CustomerDeviceId, req.TenantId, req.Points, req.Reason, req.SourceOrderId, req.IdempotencyKey);
        return success ? Ok(new { success, newBalance = balance })
                       : BadRequest(new { success, error });
    }

    /// <summary>POST /api/internal/loyalty/points/deduct</summary>
    [HttpPost("points/deduct")]
    public async Task<IActionResult> DeductPoints([FromBody] DeductPointsRequest req)
    {
        var (success, balance, error) = await walletService.DeductPointsAsync(
            req.CustomerDeviceId, req.TenantId, req.Points, req.Reason, req.VoucherCode, req.IdempotencyKey);
        return success ? Ok(new { success, newBalance = balance })
                       : BadRequest(new { success, error });
    }

    /// <summary>POST /api/internal/loyalty/points/refund</summary>
    [HttpPost("points/refund")]
    public async Task<IActionResult> RefundPoints([FromBody] RefundPointsRequest req)
    {
        var (success, balance, error) = await walletService.RefundAsync(
            req.CustomerDeviceId, req.TenantId, req.Points, req.Reason, req.VoucherCode, req.IdempotencyKey);
        return success ? Ok(new { success, newBalance = balance })
                       : BadRequest(new { success, error });
    }

    /// <summary>GET /api/internal/loyalty/wallet/{deviceId}</summary>
    [HttpGet("wallet/{deviceId}")]
    public async Task<IActionResult> GetWallet(Guid deviceId)
    {
        var wallet = await walletService.GetWalletByDeviceIdAsync(deviceId);
        if (wallet == null) return Ok(new { totalPointBalance = 0, isActive = false });
        return Ok(new { totalPointBalance = wallet.TotalPointBalance, isActive = wallet.IsActive });
    }
}

// Request DTOs
public class AddPointsRequest {
    public Guid CustomerDeviceId { get; set; }
    public Guid TenantId { get; set; }
    public int Points { get; set; }
    public string Reason { get; set; } = "";
    public Guid? SourceOrderId { get; set; }
    public string? IdempotencyKey { get; set; }
}
public class DeductPointsRequest {
    public Guid CustomerDeviceId { get; set; }
    public Guid TenantId { get; set; }
    public int Points { get; set; }
    public string Reason { get; set; } = "";
    public string? VoucherCode { get; set; }
    public string? IdempotencyKey { get; set; }
}
public class RefundPointsRequest {
    public Guid CustomerDeviceId { get; set; }
    public Guid TenantId { get; set; }
    public int Points { get; set; }
    public string Reason { get; set; } = "";
    public string VoucherCode { get; set; } = "";
    public string? IdempotencyKey { get; set; }
}
```

### 0.7 ShopERP HTTP Proxies

**File: `5_WebApps/ShopERP/Services/AllianceWalletServiceHttpProxy.cs` (NEW)**
```csharp
using System.Net.Http.Headers;
using System.Text.Json;
using Microsoft.Extensions.Caching.Memory;
using VanAn.Shared.Domain;
using VanAn.Shared.Services;

namespace VanAn.ShopERP.Services;

/// <summary>
/// HTTP proxy for IAllianceWalletService — calls Gateway internal API.
/// Multi-VPS safe: ShopERP never connects to PG directly.
/// Cache: wallet reads cached 10s. Write ops invalidate cache for device.
/// Idempotency: caller provides stable key; proxy forwards in X-Idempotency-Key header.
/// If caller omits key, proxy generates GUID (with warning — retries NOT idempotent).
/// </summary>
public class AllianceWalletServiceHttpProxy(
    IHttpClientFactory httpClientFactory,
    IMemoryCache cache,
    ILogger<AllianceWalletServiceHttpProxy> logger) : IAllianceWalletService
{
    private readonly IHttpClientFactory _httpClientFactory = httpClientFactory;
    private readonly IMemoryCache _cache = cache;
    private readonly ILogger<AllianceWalletServiceHttpProxy> _logger = logger;
    private static readonly TimeSpan WalletCacheTtl = TimeSpan.FromSeconds(10);

    public async Task<AllianceWallet?> GetWalletByDeviceIdAsync(Guid customerDeviceId)
    {
        string cacheKey = $"alliance_wallet_{customerDeviceId}";
        if (_cache.TryGetValue(cacheKey, out var cached))
            return cached as AllianceWallet;

        var client = _httpClientFactory.CreateClient("GatewayInternal");
        var resp = await client.GetAsync($"/api/internal/loyalty/wallet/{customerDeviceId}");
        if (!resp.IsSuccessStatusCode) return null;

        var json = await resp.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);
        int balance = doc.RootElement.GetProperty("totalPointBalance").GetInt32();
        bool isActive = doc.RootElement.GetProperty("isActive").GetBoolean();

        // Reconstruct a lightweight wallet object (enough for read paths)
        var wallet = new AllianceWallet(customerDeviceId, null);
        typeof(AllianceWallet).GetProperty(nameof(AllianceWallet.TotalPointBalance))!
            .SetValue(wallet, balance);
        typeof(AllianceWallet).GetProperty(nameof(AllianceWallet.IsActive))!
            .SetValue(wallet, isActive);

        _cache.Set(cacheKey, wallet, WalletCacheTtl);
        return wallet;
    }

    public async Task<AllianceWallet> GetOrCreateWalletAsync(Guid customerDeviceId, string? phoneNumber)
    {
        // ShopERP never creates wallets — Gateway creates on first AddPointsAsync.
        // For read paths, return existing or empty.
        return await GetWalletByDeviceIdAsync(customerDeviceId) ?? new AllianceWallet(customerDeviceId, phoneNumber);
    }

    public async Task<(bool Success, int NewBalance, string? Error)> AddPointsAsync(
        Guid customerDeviceId, Guid tenantId, int points, string reason,
        Guid? sourceOrderId = null, string? idempotencyKey = null)
    {
        idempotencyKey ??= GenerateKey("add", customerDeviceId, tenantId, points);
        var (success, balance, error) = await PostPointsAsync("add",
            new { CustomerDeviceId = customerDeviceId, TenantId = tenantId, Points = points,
                  Reason = reason, SourceOrderId = sourceOrderId, IdempotencyKey = idempotencyKey },
            customerDeviceId);
        return (success, balance, error);
    }

    public async Task<(bool Success, int NewBalance, string? Error)> DeductPointsAsync(
        Guid customerDeviceId, Guid tenantId, int points, string reason,
        string? voucherCode = null, string? idempotencyKey = null)
    {
        idempotencyKey ??= GenerateKey("deduct", customerDeviceId, tenantId, points);
        var (success, balance, error) = await PostPointsAsync("deduct",
            new { CustomerDeviceId = customerDeviceId, TenantId = tenantId, Points = points,
                  Reason = reason, VoucherCode = voucherCode, IdempotencyKey = idempotencyKey },
            customerDeviceId);
        return (success, balance, error);
    }

    public async Task<(bool Success, int NewBalance, string? Error)> RefundAsync(
        Guid customerDeviceId, Guid tenantId, int points, string reason,
        string voucherCode, string? idempotencyKey = null)
    {
        idempotencyKey ??= GenerateKey("refund", customerDeviceId, tenantId, points);
        var (success, balance, error) = await PostPointsAsync("refund",
            new { CustomerDeviceId = customerDeviceId, TenantId = tenantId, Points = points,
                  Reason = reason, VoucherCode = voucherCode, IdempotencyKey = idempotencyKey },
            customerDeviceId);
        return (success, balance, error);
    }

    public Task<IReadOnlyList<AllianceTransaction>> GetTransactionsAsync(Guid walletId, int limit = 20)
        => throw new NotSupportedException("Transaction history reads are Gateway-only. Use GET /api/loyalty/wallet (customer-facing).");

    public Task<IReadOnlyList<AllianceTransaction>> GetTransactionsByTenantAsync(Guid walletId, Guid tenantId, int limit = 20)
        => throw new NotSupportedException("Transaction history reads are Gateway-only.");

    public Task<MigrationResult> ConsolidateWalletsAsync(Guid tenantId, IReadOnlyList<CustomerBalanceInput> customerBalances, string changedBy)
        => throw new NotSupportedException("Migration operations are Gateway-only admin ops.");

    public Task<MigrationResult> SplitWalletsAsync(Guid tenantId, string changedBy)
        => throw new NotSupportedException("Migration operations are Gateway-only admin ops.");

    // === Helpers ===

    private async Task<(bool Success, int Balance, string? Error)> PostPointsAsync(
        string op, object body, Guid deviceForCacheInvalidation)
    {
        try
        {
            var client = _httpClientFactory.CreateClient("GatewayInternal");
            var content = JsonContent.Create(body);
            var resp = await client.PostAsync($"/api/internal/loyalty/points/{op}", content);
            var json = await resp.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(json);

            bool success = doc.RootElement.GetProperty("success").GetBoolean();
            int balance = doc.RootElement.TryGetProperty("newBalance", out var b) ? b.GetInt32() : 0;
            string? error = doc.RootElement.TryGetProperty("error", out var e) ? e.GetString() : null;

            if (success)
                _cache.Remove($"alliance_wallet_{deviceForCacheInvalidation}");

            return (success, balance, error);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "HTTP proxy {Op} failed — Gateway unreachable", op);
            return (false, 0, "Gateway unavailable");
        }
    }

    private string GenerateKey(string op, Guid deviceId, Guid tenantId, int points)
    {
        _logger.LogWarning("AllianceWalletServiceHttpProxy: no idempotency key provided for {Op} — auto-generated. Retries NOT idempotent.", op);
        return $"{op}:{deviceId}:{tenantId}:{points}:{Guid.NewGuid()}";
    }
}
```

**File: `5_WebApps/ShopERP/Services/LoyaltyModeResolverHttpProxy.cs` (NEW)**
```csharp
using System.Text.Json;
using Microsoft.Extensions.Caching.Memory;
using VanAn.Shared.Domain;
using VanAn.Shared.Services;

namespace VanAn.ShopERP.Services;

/// <summary>
/// HTTP proxy for ILoyaltyModeResolver — calls Gateway internal API.
/// Cache: mode config cached 60s per tenant (mode changes are rare admin ops).
/// Multi-VPS safe: ShopERP never queries PG LoyaltyTenantConfigs directly.
/// </summary>
public class LoyaltyModeResolverHttpProxy(
    IHttpClientFactory httpClientFactory,
    IMemoryCache cache,
    ILogger<LoyaltyModeResolverHttpProxy> logger) : ILoyaltyModeResolver
{
    private readonly IHttpClientFactory _httpClientFactory = httpClientFactory;
    private readonly IMemoryCache _cache = cache;
    private readonly ILogger<LoyaltyModeResolverHttpProxy> _logger = logger;
    private static readonly TimeSpan ModeCacheTtl = TimeSpan.FromSeconds(60);

    private record CachedModeConfig(LoyaltyMode Mode, int MaxWalletPoints, bool IsAllianceMember);

    private async Task<CachedModeConfig> GetCachedConfigAsync(Guid tenantId)
    {
        string cacheKey = $"loyalty_mode_{tenantId}";
        if (_cache.TryGetValue(cacheKey, out var cached) && cached is CachedModeConfig cmc)
            return cmc;

        try
        {
            var client = _httpClientFactory.CreateClient("GatewayInternal");
            var resp = await client.GetAsync($"/api/internal/loyalty/effective-config/{tenantId}");
            if (!resp.IsSuccessStatusCode)
            {
                _logger.LogWarning("Mode resolver HTTP failed for tenant {TenantId}: {Status}", tenantId, resp.StatusCode);
                return new CachedModeConfig(LoyaltyMode.Silo, 100000, false); // Safe fallback
            }

            var json = await resp.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(json);
            var mode = Enum.Parse<LoyaltyMode>(doc.RootElement.GetProperty("mode").GetString()!);
            int maxWallet = doc.RootElement.GetProperty("maxWalletPoints").GetInt32();
            bool isMember = doc.RootElement.GetProperty("isAllianceMember").GetBoolean();

            var config = new CachedModeConfig(mode, maxWallet, isMember);
            _cache.Set(cacheKey, config, ModeCacheTtl);
            return config;
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Mode resolver HTTP unreachable for tenant {TenantId} — fallback Silo", tenantId);
            return new CachedModeConfig(LoyaltyMode.Silo, 100000, false);
        }
    }

    public async Task<LoyaltyMode> GetEffectiveModeAsync(Guid tenantId)
        => (await GetCachedConfigAsync(tenantId)).Mode;

    public async Task<int> GetEffectiveMaxWalletPointsAsync(Guid tenantId)
        => (await GetCachedConfigAsync(tenantId)).MaxWalletPoints;

    public async Task<bool> IsAllianceMemberAsync(Guid tenantId)
        => (await GetCachedConfigAsync(tenantId)).IsAllianceMember;
}
```

### 0.8 DI Registration

**File: `2_Gateway/Program.cs` — add after line 264:**
```csharp
// Internal API key for service-to-service auth (ShopERP → Gateway internal endpoints)
// Configure via: InternalLoyalty__ApiKey env var or appsettings.json
_ = builder.Services.Configure<InternalLoyaltyOptions>(builder.Configuration.GetSection("InternalLoyalty"));
```
Add options class:
```csharp
public class InternalLoyaltyOptions { public string? ApiKey { get; set; } }
```

**File: `5_WebApps/ShopERP/Program.cs` — add after line 360 (after MissionService):**
```csharp
// Loyalty Alliance — Phase 0 fix: HTTP proxies (Option B — multi-VPS ready)
// ShopERP calls Gateway internal API via HttpClient. No direct PG access.
// Auth: X-Internal-Api-Key header (shared secret in config).
_ = builder.Services.AddHttpClient("GatewayInternal", client =>
{
    client.BaseAddress = new Uri(builder.Configuration["Gateway:BaseUrl"] ?? "http://gateway:8080");
    client.Timeout = TimeSpan.FromSeconds(10);
    client.DefaultRequestHeaders.Add("X-Internal-Api-Key", builder.Configuration["InternalLoyalty:ApiKey"] ?? "");
});
_ = builder.Services.AddScoped<VanAn.Shared.Services.ILoyaltyModeResolver, VanAn.ShopERP.Services.LoyaltyModeResolverHttpProxy>();
_ = builder.Services.AddScoped<VanAn.Shared.Services.IAllianceWalletService, VanAn.ShopERP.Services.AllianceWalletServiceHttpProxy>();
```

### 0.9 Existing Callers — Pass Idempotency Keys

**File: `3_CoreHub/Services/OrderWorkflowService.cs` line 378:**
```csharp
// BEFORE:
var (allianceSuccess, newBalance, allianceError) = await _allianceWalletService.AddPointsAsync(
    deviceGuid, order.TenantId.Value, pointsToAward, reason, order.Id);

// AFTER:
var (allianceSuccess, newBalance, allianceError) = await _allianceWalletService.AddPointsAsync(
    deviceGuid, order.TenantId.Value, pointsToAward, reason, order.Id,
    idempotencyKey: $"earn:{order.Id}");  // Retry-safe: same order → same key → no double points
```

**File: `3_CoreHub/Services/RedemptionService.cs` line 128:**
```csharp
// BEFORE:
var (success, newBalance, error) = await _allianceWalletService.DeductPointsAsync(
    deviceGuid, tenantId, catalogItem.PointsRequired,
    $"Redeem: {catalogItem.ProductName}", voucherCode);

// AFTER:
var (success, newBalance, error) = await _allianceWalletService.DeductPointsAsync(
    deviceGuid, tenantId, catalogItem.PointsRequired,
    $"Redeem: {catalogItem.ProductName}", voucherCode,
    idempotencyKey: $"redeem:{record.Id}");  // Retry-safe
```

### 0.10 Config (appsettings)

**Both `2_Gateway/appsettings.json` + `5_WebApps/ShopERP/appsettings.json`:**
```json
{
  "InternalLoyalty": {
    "ApiKey": "vanan-internal-loyalty-key-2026"
  }
}
```
**Production:** use env var `InternalLoyalty__ApiKey` (CD pipeline injects).

**ShopERP `appsettings.json` — add Gateway base URL:**
```json
{
  "Gateway": {
    "BaseUrl": "http://gateway:8080"
  }
}
```
**Production:** `Gateway__BaseUrl=https://api.khachvip.online` (or internal Docker network `http://gateway:8080`).

---

## Phase 1 — Session 2: Point-Write Mode Routing (BUG #1, #2, #3, #6)

### BUG #1: `3_CoreHub/Services/MissionService.cs`

**Constructor (line 19-27) — add 2 nullable params:**
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
    ILoyaltyModeResolver? loyaltyModeResolver = null,
    IAllianceWalletService? allianceWalletService = null) : IMissionService
{
    // ... existing fields ...
    private readonly ILoyaltyModeResolver? _loyaltyModeResolver = loyaltyModeResolver;
    private readonly IAllianceWalletService? _allianceWalletService = allianceWalletService;
```

**Helper (insert after constructor):**
```csharp
private async Task<(bool Success, int NewBalance)> AwardPointsWithModeRoutingAsync(
    Guid customerId, int points, string reason, string idempotencyKey)
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
                    deviceGuid, tenantId, points, reason, idempotencyKey: idempotencyKey);
                if (!success)
                {
                    _logger.LogWarning("Alliance mission award failed for {CustomerId}: {Error}", customerId, error);
                    return (false, 0);
                }
                return (true, newBalance);
            }
        }
    }
    bool awarded = await _loyaltyRewardsService.AddPointsAsync(customerId, points, reason);
    if (awarded)
    {
        var rewards = await _loyaltyRewardsService.GetCustomerRewardsAsync(customerId);
        return (true, rewards?.PointBalance ?? 0);
    }
    return (false, 0);
}
```

**Replace line 131 in `CompleteMissionAsync`:**
```csharp
// completion.Id is set after AddCompletionAsync (line 128) — use it as idempotency key
var (awarded, newBalance) = await AwardPointsWithModeRoutingAsync(
    customerId, mission.PointsReward, $"Mission: {mission.Title}",
    idempotencyKey: $"mission:{completion.Id}");
if (!awarded) { /* rollback unchanged */ }
```
Use `newBalance` from routing result at line 155-156 (skip re-read in Alliance mode).

**Replace line 252 in `CompleteAnnualMissionAsync`:** Same pattern, key = `$"mission_annual:{completion.Id}"`.

### BUG #2: `3_CoreHub/Services/RedemptionService.cs` `CancelAsync`

**Add refund routing helper:**
```csharp
private async Task<bool> RefundPointsWithModeRoutingAsync(
    Guid customerId, Guid tenantId, int points, string reason, string? voucherCode, string idempotencyKey)
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
                    deviceGuid, tenantId, points, reason, voucherCode ?? "CANCEL", idempotencyKey);
                if (!success)
                {
                    _logger.LogWarning("Alliance refund failed for {CustomerId}: {Error}", customerId, error);
                    return false;
                }
                return true;
            }
        }
    }
    return await _loyaltyRewardsService.AddPointsAsync(customerId, points, reason);
}
```

**Restructure `CancelAsync` (lines 320-337) — move voucher lookup BEFORE refund:**
```csharp
// 1. Fetch voucher first
Voucher? cancelledVoucher = null;
if (record.VoucherId.HasValue)
    cancelledVoucher = await _repository.GetVoucherByIdAsync(record.VoucherId.Value);

// 2. Refund (route by mode)
_ = await RefundPointsWithModeRoutingAsync(
    record.CustomerId, record.TenantId.Value, record.PointsSpent,
    $"Refund: cancelled redemption {redemptionRecordId}",
    cancelledVoucher?.VoucherCode,
    idempotencyKey: $"refund:{record.Id}");

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

### BUG #3: `5_WebApps/ShopERP/Controllers/LoyaltyController.cs` `Redeem` — D3 deprecate
```csharp
[HttpPost("redeem")]
[Obsolete("Use POST /api/redemption/redeem — catalog-based redeem with Alliance routing.")]
public IActionResult Redeem()
{
    return StatusCode(410, new { error = "Endpoint deprecated. Use POST /api/redemption/redeem." });
}
```

### BUG #6: `3_CoreHub/Services/LoyaltyRewardsService.cs` `ActivateCustomerAsync`

**Constructor — add 3 nullable params:**
```csharp
public class LoyaltyRewardsService(
    ILoyaltyRewardsRepository repository,
    ILogger<LoyaltyRewardsService> logger,
    INatsEventPublisher? natsEventPublisher = null,
    IOutboxRepository? outboxRepository = null,
    ILoyaltyModeResolver? loyaltyModeResolver = null,
    IAllianceWalletService? allianceWalletService = null,
    ICustomerRepository? customerRepository = null) : ILoyaltyRewardsService
{
    // ... existing fields ...
    private readonly ILoyaltyModeResolver? _loyaltyModeResolver = loyaltyModeResolver;
    private readonly IAllianceWalletService? _allianceWalletService = allianceWalletService;
    private readonly ICustomerRepository? _customerRepository = customerRepository;
```

**Replace line 329 in `ActivateCustomerAsync`:**
```csharp
bool welcomeAwarded;
if (_loyaltyModeResolver is not null && _allianceWalletService is not null && _customerRepository is not null)
{
    var customer = await _customerRepository.GetCustomerByIdAsync(customerId);
    Guid tenantId = customer?.TenantId.Value ?? Guid.Empty;
    if (tenantId != Guid.Empty)
    {
        LoyaltyMode mode = await _loyaltyModeResolver.GetEffectiveModeAsync(tenantId);
        if (mode == LoyaltyMode.Alliance && await _loyaltyModeResolver.IsAllianceMemberAsync(tenantId))
        {
            Guid deviceGuid = customer?.DeviceId ?? customerId;
            var (ok, _, err) = await _allianceWalletService.AddPointsAsync(
                deviceGuid, tenantId, 100, "Welcome bonus",
                idempotencyKey: $"welcome:{customerId}");
            welcomeAwarded = ok;
            if (!ok) _logger.LogWarning("Alliance welcome bonus failed for {CustomerId}: {Error}", customerId, err);
        }
        else
        {
            welcomeAwarded = await AddPointsAsync(customerId, 100, "Welcome bonus for joining loyalty program");
        }
    }
    else
    {
        welcomeAwarded = await AddPointsAsync(customerId, 100, "Welcome bonus for joining loyalty program");
    }
}
else
{
    welcomeAwarded = await AddPointsAsync(customerId, 100, "Welcome bonus for joining loyalty program");
}
```

**Note:** `ICustomerRepository` must be registered in ShopERP DI (already done — Program.cs line 210).

---

## Phase 2 — Session 3: Point-Read Mode Routing (BUG #4, #5, #7, #8)

### NEW: `3_CoreHub/Services/LoyaltyReadRouter.cs`
```csharp
using VanAn.Shared.Domain;
using VanAn.Shared.Services;

namespace VanAn.CoreHub.Services;

public class LoyaltyReadRouter(
    ILoyaltyModeResolver? modeResolver,
    IAllianceWalletService? walletService,
    ILogger<LoyaltyReadRouter> logger)
{
    public async Task<int> GetEffectiveBalanceAsync(Guid tenantId, Guid? deviceGuid, int sqliteBalance)
    {
        if (modeResolver is null || walletService is null || deviceGuid is null || deviceGuid.Value == Guid.Empty)
            return sqliteBalance;

        try
        {
            LoyaltyMode mode = await modeResolver.GetEffectiveModeAsync(tenantId);
            if (mode != LoyaltyMode.Alliance) return sqliteBalance;
            if (!await modeResolver.IsAllianceMemberAsync(tenantId)) return sqliteBalance;

            var wallet = await walletService.GetWalletByDeviceIdAsync(deviceGuid.Value);
            return wallet?.TotalPointBalance ?? 0;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "LoyaltyReadRouter: wallet query failed for tenant {TenantId} — fallback SQLite", tenantId);
            return sqliteBalance;
        }
    }
}
```
Register in ShopERP `Program.cs`: `_ = builder.Services.AddScoped<VanAn.CoreHub.Services.LoyaltyReadRouter>();`

### BUG #4+#5: `LoyaltyController.cs` `GetMyLoyalty`
Inject `LoyaltyReadRouter` + `ICustomerRepository`. After getting `rewards`, resolve customer → use `LoyaltyReadRouter.GetEffectiveBalanceAsync(customer.TenantId.Value, customer.DeviceId, rewards.PointBalance)` for `effectiveBalance`. Use `effectiveBalance` for tier calc + response.

### BUG #7: `CustomerIdentityController.cs`
Inject `LoyaltyReadRouter`. In `GetMe` (line 130) + `VerifyOtp` (line 97): replace `rewards.PointBalance` with `await _readRouter.GetEffectiveBalanceAsync(customer.TenantId.Value, customer.DeviceId, rewards.PointBalance)`.

### BUG #8: `CustomerController.cs`
Inject `LoyaltyReadRouter`. Add helper:
```csharp
private async Task<int> GetEffectiveBalanceForCustomerAsync(Shared.Domain.Customer c)
{
    var rewards = await _loyaltyRewardsService.GetCustomerRewardsAsync(c.Id);
    int sqliteBalance = rewards?.PointBalance ?? 0;
    return await _readRouter.GetEffectiveBalanceAsync(c.TenantId.Value, c.DeviceId, sqliteBalance);
}
```
Replace SQLite reads in `List` (line 74), `PreviewSegment` (line 95), `ListGlobal` (lines 135, 166).

---

## Phase 3 — Session 4: NATS Sync Fidelity (BUG #9)

### `3_CoreHub/Services/AllianceWalletService.cs` `PublishLoyaltyChangedAsync`
Change signature to accept `AllianceTransaction tx` and include extended fields:
```csharp
private async Task PublishLoyaltyChangedAsync(Guid customerDeviceId, int newBalance, AllianceTransaction tx)
{
    if (_natsEventPublisher is null) return;
    try
    {
        var payload = new
        {
            customerDeviceId,
            pointBalance = newBalance,
            updatedAt = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ"),
            type = tx.Type.ToString(),
            points = tx.Points,
            reason = tx.Reason,
            tenantId = tx.TransactionTenantId.Value.ToString()
        };
        byte[] bytes = JsonSerializer.SerializeToUtf8Bytes(payload, EventJsonOptions);
        await _natsEventPublisher.PublishAsync($"vanan.cloud.loyalty.changed.{customerDeviceId}", bytes);
    }
    catch (Exception ex) { _logger.LogError(ex, "PublishLoyaltyChangedAsync failed for {DeviceId}", customerDeviceId); }
}
```
Update 3 callers (`AddPointsAsync`, `DeductPointsAsync`, `RefundAsync`) to pass `tx`.

### `5_WebApps/ShopERP/Services/LoyaltySyncSubscriber.cs` `SyncLoyaltyBalanceAsync`
Extend to append history when extended fields present. See previous version of detail plan (unchanged logic — parse optional `type`/`points`/`reason`/`tenantId`/`updatedAt`, append `LoyaltyHistoryEntry` if not duplicate, then update `PointBalance`). Backward compat: legacy payload → balance-only.

---

## Phase 4 — Session 5: Tests + VPS RV

### Test files (NEW):
| File | Tests | Bugs |
|---|---|---|
| `ShopErpDiRegistrationTests.cs` | 1 — DI resolves HTTP proxies | #0 |
| `InternalApiKeyAuthTests.cs` | 2 — missing key 401, wrong key 401, valid key 200 | #0 |
| `IdempotencyTests.cs` | 2 — same key → cached result, different key → process | #0 |
| `MissionServiceAllianceTests.cs` | 4 — Alliance+member, Silo, opt-out, annual | #1 |
| `RedemptionCancelAllianceTests.cs` | 3 — Alliance+member, Silo, opt-out | #2 |
| `LoyaltyRewardsActivateAllianceTests.cs` | 3 — Alliance+member, Silo, opt-out | #6 |
| `LoyaltyReadRoutingTests.cs` | 3 — my, me, customers list | #4, #7, #8 |
| `LoyaltySyncHistoryTests.cs` | 2 — extended payload, legacy payload | #9 |

**Test pattern:** Mock `HttpMessageHandler` for proxy tests; mock `ILoyaltyModeResolver` + `IAllianceWalletService` for service-level tests (same as existing `OrderWorkflowAllianceTests`).

### VPS RV — 14-step checklist
See `loyalty-consistency-fix-master-plan.md` Session 5.

### Build + commit + push + RV + state update
```powershell
guard-check.ps1
dotnet build VanAn.sln
dotnet test
git add -A
git commit -m "fix(loyalty): consistency BUG #0-#9 — HTTP proxy infra + write/read routing + sync history + idempotency"
```

---

## Files Modified Summary

| File | Bug(s) | Phase | Status |
|---|---|---|---|
| `1_Shared/Domain.cs` | #0 | 0 | EDIT — IdempotencyKey on AllianceTransaction |
| `3_CoreHub/Infrastructure/Configurations/AllianceTransactionConfiguration.cs` | #0 | 0 | EDIT — map + index |
| **PG migration (new)** | #0 | 0 | NEW — AddAllianceTransactionIdempotencyKey |
| `1_Shared/Services/IAllianceWalletService.cs` | #0 | 0 | EDIT — idempotencyKey param |
| `3_CoreHub/Services/AllianceWalletService.cs` | #0, #9 | 0, 3 | EDIT — idempotency check + extended NATS payload |
| `2_Gateway/Filters/InternalApiKeyAttribute.cs` | #0 | 0 | NEW |
| `2_Gateway/Controllers/InternalLoyaltyController.cs` | #0 | 0 | NEW — 5 endpoints |
| `2_Gateway/Program.cs` | #0 | 0 | EDIT — configure API key |
| `5_WebApps/ShopERP/Services/AllianceWalletServiceHttpProxy.cs` | #0 | 0 | NEW |
| `5_WebApps/ShopERP/Services/LoyaltyModeResolverHttpProxy.cs` | #0 | 0 | NEW |
| `5_WebApps/ShopERP/Program.cs` | #0 | 0 | EDIT — register proxies + HttpClient |
| `3_CoreHub/Services/OrderWorkflowService.cs` | #0 | 0 | EDIT — pass idempotency key |
| `3_CoreHub/Services/RedemptionService.cs` | #0, #2 | 0, 1 | EDIT — idempotency key + CancelAsync routing |
| `3_CoreHub/Services/MissionService.cs` | #1 | 1 | EDIT — routing + idempotency |
| `5_WebApps/ShopERP/Controllers/LoyaltyController.cs` | #3, #4 | 1, 2 | EDIT — deprecate + mode-aware read |
| `3_CoreHub/Services/LoyaltyRewardsService.cs` | #6 | 1 | EDIT — ActivateCustomerAsync routing |
| `3_CoreHub/Services/LoyaltyReadRouter.cs` | #4, #7, #8 | 2 | NEW |
| `5_WebApps/ShopERP/Controllers/CustomerIdentityController.cs` | #7 | 2 | EDIT — mode-aware balance |
| `5_WebApps/ShopERP/Controllers/CustomerController.cs` | #8 | 2 | EDIT — mode-aware balance |
| `5_WebApps/ShopERP/Services/LoyaltySyncSubscriber.cs` | #9 | 3 | EDIT — history sync |
| `2_Gateway/appsettings.json` | #0 | 0 | EDIT — InternalLoyalty:ApiKey |
| `5_WebApps/ShopERP/appsettings.json` | #0 | 0 | EDIT — Gateway:BaseUrl + InternalLoyalty:ApiKey |
| 8 test files | #0-#9 | 4 | NEW |

**Total:** 12 edited + 10 new (incl. migration) + 8 test files = 30 files.
