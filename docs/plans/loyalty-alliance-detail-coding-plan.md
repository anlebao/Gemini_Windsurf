# Loyalty Alliance System — Detail Coding Plan

## Phase 1A — Domain Entities (Session 1)

### File: `1_Shared/Domain.cs`
**Insert after line ~2114 (after `LoyaltyRewards` class, before `IdempotentOperation`)**

```csharp
// === Loyalty Alliance System ===

public enum LoyaltyMode { Silo = 0, Alliance = 1 }
public enum AllianceTransactionType { EARN = 0, REDEEM = 1, ADJUST = 2 }

// Global loyalty config — single row, NOT tenant-scoped
public class LoyaltyGlobalConfig : BaseEntity
{
    public LoyaltyMode Mode { get; protected set; } = LoyaltyMode.Silo;
    public int PointsRate { get; protected set; } = 1;
    public int MinPointsPerOrder { get; protected set; } = 10;
    public int MaxPointsPerOrder { get; protected set; } = 30;
    public int MaxWalletPoints { get; protected set; } = 100000;
    public DateTime? LastChangedAt { get; protected set; }
    public string? LastChangedBy { get; protected set; }

    protected LoyaltyGlobalConfig() { }

    public LoyaltyGlobalConfig()
        : base(TenantId.Empty)
    {
        Mode = LoyaltyMode.Silo;
        LastChangedAt = DateTime.UtcNow;
    }

    public void UpdateMode(LoyaltyMode mode, string changedBy)
    {
        Mode = mode;
        LastChangedAt = DateTime.UtcNow;
        LastChangedBy = changedBy;
        UpdateAudit();
    }

    public void UpdateLimits(int maxPointsPerOrder, int maxWalletPoints, string changedBy)
    {
        MaxPointsPerOrder = maxPointsPerOrder;
        MaxWalletPoints = maxWalletPoints;
        LastChangedAt = DateTime.UtcNow;
        LastChangedBy = changedBy;
        UpdateAudit();
    }
}

// Per-tenant loyalty override — tenant-scoped
public class LoyaltyTenantConfig : BaseEntity, IMustHaveTenant
{
    public LoyaltyMode? Mode { get; protected set; }  // null = inherit global
    public bool IsAllianceMember { get; protected set; } = false;
    public int? MaxWalletPoints { get; protected set; }  // null = inherit global
    public DateTime? LastChangedAt { get; protected set; }
    public string? LastChangedBy { get; protected set; }

    protected LoyaltyTenantConfig() { }

    public LoyaltyTenantConfig(TenantId tenantId)
        : base(tenantId)
    {
        IsAllianceMember = false;
    }

    public void SetMode(LoyaltyMode? mode, string changedBy)
    {
        Mode = mode;
        LastChangedAt = DateTime.UtcNow;
        LastChangedBy = changedBy;
        UpdateAudit();
    }

    public void SetAllianceMembership(bool isMember, string changedBy)
    {
        IsAllianceMember = isMember;
        LastChangedAt = DateTime.UtcNow;
        LastChangedBy = changedBy;
        UpdateAudit();
    }

    public void SetMaxWalletPoints(int? max, string changedBy)
    {
        MaxWalletPoints = max;
        LastChangedAt = DateTime.UtcNow;
        LastChangedBy = changedBy;
        UpdateAudit();
    }
}

// Cross-tenant wallet — 1 per customer device identity
public class AllianceWallet : BaseEntity
{
    public Guid CustomerDeviceId { get; protected set; }
    public string? PhoneNumber { get; protected set; }
    public int TotalPointBalance { get; protected set; }
    public bool IsActive { get; protected set; } = true;
    public DateTime LastEarnAt { get; protected set; }
    public DateTime LastRedeemAt { get; protected set; }

    protected AllianceWallet() { }

    public AllianceWallet(Guid customerDeviceId, string? phoneNumber)
        : base(TenantId.Empty)
    {
        CustomerDeviceId = customerDeviceId;
        PhoneNumber = phoneNumber;
        TotalPointBalance = 0;
        IsActive = true;
    }

    public void AddPoints(int points)
    {
        TotalPointBalance += points;
        LastEarnAt = DateTime.UtcNow;
        UpdateAudit();
    }

    public void DeductPoints(int points)
    {
        TotalPointBalance = Math.Max(0, TotalPointBalance - points);
        LastRedeemAt = DateTime.UtcNow;
        UpdateAudit();
    }

    public void Freeze()
    {
        IsActive = false;
        UpdateAudit();
    }
}

// Transaction log — every earn/redeem/refund across tenants
public class AllianceTransaction : BaseEntity
{
    public Guid WalletId { get; protected set; }
    public Guid TenantId { get; protected set; }
    public AllianceTransactionType Type { get; protected set; }
    public int Points { get; protected set; }
    public int BalanceAfter { get; protected set; }
    public string Reason { get; protected set; } = string.Empty;
    public Guid? SourceOrderId { get; protected set; }
    public string? VoucherCode { get; protected set; }
    public Guid? RefundTenantId { get; protected set; }
    public DateTime TransactionAt { get; protected set; }

    protected AllianceTransaction() { }

    public AllianceTransaction(
        Guid walletId, Guid tenantId, AllianceTransactionType type,
        int points, int balanceAfter, string reason,
        Guid? sourceOrderId = null, string? voucherCode = null, Guid? refundTenantId = null)
        : base(TenantId.Empty)
    {
        WalletId = walletId;
        TenantId = tenantId;
        Type = type;
        Points = points;
        BalanceAfter = balanceAfter;
        Reason = reason;
        SourceOrderId = sourceOrderId;
        VoucherCode = voucherCode;
        RefundTenantId = refundTenantId;
        TransactionAt = DateTime.UtcNow;
    }
}
```

---

## Phase 1B — EF Configs + Migration + DI (Session 2)

### Files to create (4 EF configs in `3_CoreHub/Infrastructure/Configurations/`)

**`LoyaltyGlobalConfigConfiguration.cs`**:
```csharp
public class LoyaltyGlobalConfigConfiguration : IEntityTypeConfiguration<LoyaltyGlobalConfig>, IEntityConfiguration
{
    public void Configure(EntityTypeBuilder<LoyaltyGlobalConfig> builder)
    {
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Mode).HasConversion<int>();
        builder.Property(e => e.LastChangedBy).HasMaxLength(256);
        // Single-row enforcement
        builder.HasIndex(e => e.Id).IsUnique();
    }
}
```

**`LoyaltyTenantConfigConfiguration.cs`**:
```csharp
public class LoyaltyTenantConfigConfiguration : IEntityTypeConfiguration<LoyaltyTenantConfig>, IEntityConfiguration
{
    public void Configure(EntityTypeBuilder<LoyaltyTenantConfig> builder)
    {
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Mode).HasConversion<int?>();
        builder.HasIndex(e => new { e.TenantId }).IsUnique(); // 1 config per tenant
    }
}
```

**`AllianceWalletConfiguration.cs`**:
```csharp
public class AllianceWalletConfiguration : IEntityTypeConfiguration<AllianceWallet>, IEntityConfiguration
{
    public void Configure(EntityTypeBuilder<AllianceWallet> builder)
    {
        builder.HasKey(e => e.Id);
        builder.HasIndex(e => e.CustomerDeviceId).IsUnique(); // 1 wallet per device
        builder.Property(e => e.PhoneNumber).HasMaxLength(20);
    }
}
```

**`AllianceTransactionConfiguration.cs`**:
```csharp
public class AllianceTransactionConfiguration : IEntityTypeConfiguration<AllianceTransaction>, IEntityConfiguration
{
    public void Configure(EntityTypeBuilder<AllianceTransaction> builder)
    {
        builder.HasKey(e => e.Id);
        builder.HasIndex(e => new { e.WalletId, e.TransactionAt });
        builder.Property(e => e.Type).HasConversion<int>();
        builder.Property(e => e.Reason).HasMaxLength(500);
        builder.Property(e => e.VoucherCode).HasMaxLength(50);
    }
}
```

### Files to edit

**`IVanAnDbContext.cs`** — add DbSets:
```csharp
// Loyalty Alliance: cross-tenant wallet system (PG-only, NOT tenant-scoped)
DbSet<LoyaltyGlobalConfig> LoyaltyGlobalConfigs { get; }
DbSet<LoyaltyTenantConfig> LoyaltyTenantConfigs { get; }
DbSet<AllianceWallet> AllianceWallets { get; }
DbSet<AllianceTransaction> AllianceTransactions { get; }
```

**`VanAnDbContext.cs`** — add DbSet properties + OnModelCreating apply configs

**`ShopERPDbContext.cs`** — add DbSet properties (interface satisfaction, PG-only — add `Ignore()` in OnModelCreating for SQLite)

**`2_Gateway/Program.cs`** — register repositories:
```csharp
services.AddScoped<IGenericRepository<LoyaltyGlobalConfig>, GenericRepository<LoyaltyGlobalConfig>>();
services.AddScoped<IGenericRepository<LoyaltyTenantConfig>, GenericRepository<LoyaltyTenantConfig>>();
services.AddScoped<IGenericRepository<AllianceWallet>, GenericRepository<AllianceWallet>>();
services.AddScoped<IGenericRepository<AllianceTransaction>, GenericRepository<AllianceTransaction>>();
```

### Migration command
```bash
dotnet ef migrations add LoyaltyAlliance --project 3_CoreHub --startup-project 2_Gateway
```

---

## Phase 2A — LoyaltyModeResolver + AllianceWalletService (Session 3)

### Files to create

**`1_Shared/Services/ILoyaltyModeResolver.cs`**:
```csharp
public interface ILoyaltyModeResolver
{
    Task<LoyaltyMode> GetEffectiveModeAsync(Guid tenantId);
    Task<int> GetEffectiveMaxWalletPointsAsync(Guid tenantId);
    Task<bool> IsAllianceMemberAsync(Guid tenantId);
}
```

**`3_CoreHub/Services/LoyaltyModeResolver.cs`**:
```csharp
public class LoyaltyModeResolver(
    IVanAnDbContext dbContext,
    ILogger<LoyaltyModeResolver> logger) : ILoyaltyModeResolver
{
    // Logic:
    // 1. Query LoyaltyTenantConfig by tenantId
    //    - If Mode != null → return tenant Mode
    //    - If Mode == null → return global Mode
    // 2. For MaxWalletPoints:
    //    - If tenant MaxWalletPoints != null → return tenant value
    //    - Else → return global value
    // 3. IsAllianceMemberAsync:
    //    - Query LoyaltyTenantConfig.IsAllianceMember
    //    - If no config → false (default)
}
```

**`1_Shared/Services/IAllianceWalletService.cs`**:
```csharp
public interface IAllianceWalletService
{
    Task<AllianceWallet?> GetWalletByDeviceIdAsync(Guid customerDeviceId);
    Task<AllianceWallet> GetOrCreateWalletAsync(Guid customerDeviceId, string? phoneNumber);
    Task<(bool Success, int NewBalance, string? Error)> AddPointsAsync(
        Guid customerDeviceId, Guid tenantId, int points, string reason, Guid? sourceOrderId = null);
    Task<(bool Success, int NewBalance, string? Error)> DeductPointsAsync(
        Guid customerDeviceId, Guid tenantId, int points, string reason, string? voucherCode = null);
    Task<(bool Success, int NewBalance, string? Error)> RefundAsync(
        Guid customerDeviceId, Guid tenantId, int points, string reason, string voucherCode);
    Task<IReadOnlyList<AllianceTransaction>> GetTransactionsAsync(Guid walletId, int limit = 20);
    Task<IReadOnlyList<AllianceTransaction>> GetTransactionsByTenantAsync(Guid walletId, Guid tenantId, int limit = 20);
}
```

**`3_CoreHub/Services/AllianceWalletService.cs`**:
```csharp
public class AllianceWalletService(
    IVanAnDbContext dbContext,
    ILoyaltyModeResolver modeResolver,
    INatsEventPublisher? natsEventPublisher,
    ILogger<AllianceWalletService> logger) : IAllianceWalletService
{
    // AddPointsAsync logic:
    //   1. Get or create wallet
    //   2. Check MaxWalletPoints: if TotalPointBalance + points > max → reject
    //   3. wallet.AddPoints(points)
    //   4. INSERT AllianceTransaction(EARN, tenantId, +points, balanceAfter, reason, sourceOrderId)
    //   5. SaveChanges
    //   6. Publish NATS: vanan.cloud.loyalty.changed.{customerDeviceId}

    // DeductPointsAsync logic:
    //   1. Get wallet
    //   2. Check TotalPointBalance >= points → reject if insufficient
    //   3. wallet.DeductPoints(points)
    //   4. INSERT AllianceTransaction(REDEEM, tenantId, -points, balanceAfter, reason, voucherCode, refundTenantId=tenantId)
    //   5. SaveChanges
    //   6. Publish NATS

    // RefundAsync logic (Q4: refund to tenant where redeem occurred):
    //   1. Get wallet
    //   2. wallet.AddPoints(points)  // back to wallet
    //   3. INSERT AllianceTransaction(ADJUST, tenantId, +points, balanceAfter, reason, voucherCode, refundTenantId=tenantId)
    //   4. SaveChanges
    //   5. Publish NATS
    //   Note: RefundTenantId = tenantId (where redeem happened) for audit trail
}
```

---

## Phase 2B — Modify OrderWorkflowService EARN (Session 4)

### File: `3_CoreHub/Services/OrderWorkflowService.cs`

**Constructor change** — add 2 injections:
```csharp
ILoyaltyModeResolver loyaltyModeResolver,
IAllianceWalletService? allianceWalletService = null,
```

**Modify `ProcessLoyaltyPointsAsync` (line ~267)**:
```csharp
private async Task ProcessLoyaltyPointsAsync(Order order)
{
    // ... existing customer lookup logic (lines 269-307) — UNCHANGED ...

    // ... existing points calculation (lines 309-348) — UNCHANGED ...

    // === NEW: Mode routing ===
    var effectiveMode = await _loyaltyModeResolver.GetEffectiveModeAsync(order.TenantId.Value);

    if (effectiveMode == LoyaltyMode.Alliance && _allianceWalletService != null)
    {
        // Check IsAllianceMember (Q2: opt-out toàn phần)
        bool isMember = await _loyaltyModeResolver.IsAllianceMemberAsync(order.TenantId.Value);
        if (!isMember)
        {
            _logger.LogInformation("Loyalty: Tenant {TenantId} is not alliance member — skipping alliance earn", order.TenantId);
            // Fall through to Silo flow (tenant opted out)
        }
        else
        {
            // Alliance EARN
            Guid deviceGuid = Guid.TryParse(order.CustomerDeviceId, out var d) ? d : customer.Id;
            var (success, newBalance, error) = await _allianceWalletService.AddPointsAsync(
                deviceGuid, order.TenantId.Value, pointsToAward, reason, order.Id);

            if (success)
                _logger.LogInformation("🎁 ALLIANCE EARN: {Points} points to wallet for device {DeviceId} (balance={Balance})",
                    pointsToAward, deviceGuid, newBalance);
            else
                _logger.LogWarning("Alliance EARN failed: {Error}", error);
            return;
        }
    }

    // === EXISTING: Silo flow (unchanged) ===
    bool siloSuccess = await _loyaltyRewardsService.AddPointsAsync(customer.Id, pointsToAward, reason);
    // ... existing logging ...
}
```

---

## Phase 2C — Modify RedemptionService REDEEM + NATS Sync (Session 5)

### File: `3_CoreHub/Services/RedemptionService.cs`

**Constructor change** — add 2 injections:
```csharp
ILoyaltyModeResolver loyaltyModeResolver,
IAllianceWalletService? allianceWalletService = null,
```

**Modify `RedeemAsync`**:
```csharp
public async Task<RedeemResult> RedeemAsync(Guid customerId, Guid catalogItemId)
{
    // ... existing catalog item lookup — UNCHANGED ...

    // === NEW: Mode routing ===
    var tenantId = _tenantProvider.GetTenantId();
    var effectiveMode = await _loyaltyModeResolver.GetEffectiveModeAsync(tenantId);

    if (effectiveMode == LoyaltyMode.Alliance && _allianceWalletService != null)
    {
        bool isMember = await _loyaltyModeResolver.IsAllianceMemberAsync(tenantId);
        if (!isMember)
        {
            return RedeemResult.Fail("Tenant không tham gia liên minh điểm thưởng");
        }

        // Get customer's device ID for wallet lookup
        var customer = await _dbContext.Customers.FirstOrDefaultAsync(c => c.Id == customerId);
        Guid deviceGuid = customer?.DeviceId ?? customerId;

        // Alliance REDEEM: deduct from wallet, create local voucher
        var (success, newBalance, error) = await _allianceWalletService.DeductPointsAsync(
            deviceGuid, tenantId, catalogItem.PointsRequired,
            $"Redeem: {catalogItem.ProductName}", voucherCode);

        if (!success)
            return RedeemResult.Fail(error ?? "Insufficient points");

        // Create RedemptionRecord + Voucher in local SQLite (same as Silo)
        // ... existing voucher creation logic ...

        return RedeemResult.Ok(catalogItem.PointsRequired, newBalance, voucher);
    }

    // === EXISTING: Silo flow (unchanged) ===
    // ... existing deduct from LoyaltyRewards + create voucher ...
}
```

### File to create: `5_WebApps/ShopERP/Services/LoyaltySyncSubscriber.cs`

```csharp
// BackgroundService subscribing to vanan.cloud.loyalty.changed.*
// On message:
//   1. Deserialize payload: { walletId, totalBalance, change, tenantId, type }
//   2. Find local LoyaltyRewards by CustomerDeviceId (match via Customer.DeviceId)
//   3. Update LoyaltyRewards.PointBalance = totalBalance
//   4. SaveChanges (SQLite)
// Pattern: same as OrderSyncSubscriber
```

---

## Phase 3A — SystemAdmin API (Session 6)

### File to create: `2_Gateway/Controllers/LoyaltyConfigController.cs`

```csharp
[ApiController]
[Route("api/platform/loyalty")]
[Authorize] // SystemAdmin only
public class LoyaltyConfigController(
    IVanAnDbContext dbContext,
    ILogger<LoyaltyConfigController> logger) : ControllerBase
{
    // GET /api/platform/loyalty/config
    //   → query LoyaltyGlobalConfigs (single row)
    //   → return DTO { mode, pointsRate, minPointsPerOrder, maxPointsPerOrder, maxWalletPoints }

    // PUT /api/platform/loyalty/config
    //   → validate SystemAdmin role from claims
    //   → update LoyaltyGlobalConfig
    //   → return 200 OK

    // GET /api/platform/loyalty/tenant/{tenantId}/config
    //   → query LoyaltyTenantConfigs by tenantId
    //   → return DTO { mode, isAllianceMember, maxWalletPoints }

    // PUT /api/platform/loyalty/tenant/{tenantId}/config
    //   → validate SystemAdmin role
    //   → update or create LoyaltyTenantConfig
    //   → return 200 OK

    // POST /api/platform/loyalty/migrate
    //   → trigger mode switch migration (Phase 4)
    //   → Body: { tenantId, targetMode }
    //   → return 200 OK or 400 if migration fails
}
```

---

## Phase 3B — Customer API (Session 7)

### Modify: `2_Gateway/Controllers/LoyaltyController.cs`

Add endpoint:
```csharp
[HttpGet("wallet")]
public async Task<IActionResult> GetWallet()
{
    // Extract customer token from X-Customer-Token header
    // Query AllianceWallet + AllianceTransactions from PG
    // Return DTO: { totalPointBalance, breakdown: [{tenantId, tenantName, points}], recentTransactions: [...] }
}
```

### Modify: `2_Gateway/Controllers/RedemptionController.cs`

Modify `Redeem` forward to include `tenantId`:
```csharp
[HttpPost("redeem")]
public Task<IActionResult> Redeem() => ForwardAsync(HttpMethod.Post, "/api/redemption/redeem", includeBody: true);
// Body now includes: { catalogItemId, tenantId? }
// If tenantId provided → cross-tenant redeem (Alliance mode)
```

### Modify: `5_WebApps/ShopERP/Controllers/RedemptionController.cs`

Modify `Redeem` endpoint to accept `tenantId`:
```csharp
[HttpPost("redeem")]
public async Task<IActionResult> Redeem(
    [FromHeader(Name = "X-Customer-Token")] string? customerToken,
    [FromBody] RedeemCatalogRequest request)
{
    // request now has optional TenantId
    // If TenantId provided and different from current tenant → Alliance cross-tenant
    // ... route to AllianceWalletService ...
}
```

---

## Phase 4 — Mode Switch Migration (Session 8)

### File: `3_CoreHub/Services/AllianceWalletService.cs`

Add methods:
```csharp
public async Task<MigrationResult> ConsolidateWalletsAsync(Guid tenantId, string changedBy)
{
    // Silo → Alliance for specific tenant
    // 1. Query all Customers in tenantId (from PG or via NATS-synced SQLite data)
    // 2. For each customer:
    //    a. Get LoyaltyRewards.PointBalance (from SQLite via existing service)
    //    b. Get or create AllianceWallet by CustomerDeviceId
    //    c. wallet.AddPoints(pointBalance)
    //    d. INSERT AllianceTransaction(ADJUST, tenantId, +pointBalance, "Silo→Alliance migration")
    // 3. Log summary
}

public async Task<MigrationResult> SplitWalletsAsync(Guid tenantId, string changedBy)
{
    // Alliance → Silo for specific tenant (Q1: chia theo nguồn)
    // 1. Query all AllianceWallets that have transactions with this tenantId
    // 2. For each wallet:
    //    a. Query AllianceTransactions: calculate net EARN per-tenant
    //       netEarn[tenantX] = SUM(Points WHERE Type=EARN AND TenantId=tenantX)
    //                        - SUM(ABS(Points) WHERE Type=REDEEM AND TenantId=tenantX)
    //    b. totalNetEarn = SUM(netEarn values > 0)
    //    c. For each tenant with netEarn > 0:
    //       allocation = (netEarn / totalNetEarn) * wallet.TotalPointBalance
    //       Update local LoyaltyRewards.PointBalance += allocation
    //    d. Freeze wallet (IsActive = false, TotalPointBalance = 0)
    //    e. INSERT AllianceTransaction(ADJUST, each-tenant, ±adjustment, "Alliance→Silo split")
    // 3. Edge case: tenant with netEarn ≤ 0 → no allocation
}
```

---

## Phase 5A — Admin UI (Session 9)

### File to create: `5_WebApps/ShopERP/Pages/Admin/LoyaltyConfigAdmin.razor`

```razor
@page "/admin/loyalty-config"
@attribute [Authorize(Roles = "SystemAdmin")]

<MudCard>
    <MudCardHeader>
        <MudText Typo="Typo.h6">Loyalty Configuration</MudText>
    </MudCardHeader>
    <MudCardContent>
        @* Global Config Section *@
        <MudText Typo="Typo.subtitle1">Global Settings</MudText>
        <MudSelect T="LoyaltyMode" @bind-Value="_globalMode" Label="Loyalty Mode">
            <MudSelectItem Value="LoyaltyMode.Silo">Silo (per-tenant)</MudSelectItem>
            <MudSelectItem Value="LoyaltyMode.Alliance">Alliance (cross-tenant)</MudSelectItem>
        </MudSelect>
        <MudNumericField @bind-Value="_globalMaxWalletPoints" Label="Max Wallet Points" Min="1" />

        @* Per-Tenant Config Section *@
        <MudText Typo="Typo.subtitle1">Per-Tenant Override</MudText>
        <MudTable Items="_tenantConfigs" Hover="true">
            <HeaderContent>
                <MudTh>Tenant</MudTh>
                <MudTh>Mode Override</MudTh>
                <MudTh>Alliance Member</MudTh>
                <MudTh>Max Wallet Override</MudTh>
            </HeaderContent>
            <RowTemplate>
                <MudTd>@context.TenantName</MudTd>
                <MudTd><MudSelect @bind-Value="context.Mode" /></MudTd>
                <MudTd><MudSwitch @bind-Value="context.IsAllianceMember" /></MudTd>
                <MudTd><MudNumericField @bind-Value="context.MaxWalletPoints" /></MudTd>
            </RowTemplate>
        </MudTable>

        @* Mode Switch Migration *@
        <MudButton Color="Color.Warning" OnClick="TriggerMigration">Switch Mode (Migration)</MudButton>
    </MudCardContent>
</MudCard>
```

---

## Phase 5B — Customer UI (Session 10)

### File to create: `5_WebApps/KhachLink/Components/Pages/Wallet.razor`

```razor
@page "/wallet"

<MudCard>
    <MudText Typo="Typo.h4">My Loyalty Wallet</MudText>
    <MudText Typo="Typo.h2">@_wallet.TotalPointBalance points</MudText>

    @* Breakdown by tenant *@
    <MudText Typo="Typo.subtitle1">Breakdown</MudText>
    <MudList Items="_wallet.Breakdown">
        <MudListItem>@item.TenantName: @item.Points points</MudListItem>
    </MudList>

    @* Recent transactions *@
    <MudText Typo="Typo.subtitle1">Recent Activity</MudText>
    <MudTable Items="_wallet.RecentTransactions">
        <RowTemplate>
            <MudTd>@context.Type</MudTd>
            <MudTd>@context.TenantName</MudTd>
            <MudTd>@context.Points</MudTd>
            <MudTd>@context.Reason</MudTd>
            <MudTd>@context.Timestamp</MudTd>
        </RowTemplate>
    </MudTable>
</MudCard>

@* Cross-tenant redeem *@
<MudButton OnClick="OpenRedeemDialog">Redeem Points</MudButton>
@* Dialog: select tenant → browse catalog → confirm redeem *@
```

---

## Phase 6A — Unit + Integration Tests (Session 11)

### Files to create in `6_Tests/VanAn.Core.Tests/`

**`Services/LoyaltyModeResolverTests.cs`**:
- `GetEffectiveMode_TenantOverride_ReturnsTenantMode`
- `GetEffectiveMode_NoOverride_ReturnsGlobalMode`
- `GetEffectiveMaxWalletPoints_TenantOverride_ReturnsTenantValue`
- `GetEffectiveMaxWalletPoints_NoOverride_ReturnsGlobalValue`
- `IsAllianceMember_NoConfig_ReturnsFalse`
- `IsAllianceMember_ConfigTrue_ReturnsTrue`

**`Services/AllianceWalletServiceTests.cs`**:
- `AddPoints_NewWallet_CreatesWalletAndAddsPoints`
- `AddPoints_ExceedsMaxWallet_ReturnsError`
- `DeductPoints_InsufficientBalance_ReturnsError`
- `DeductPoints_SufficientBalance_DeductsAndLogs`
- `Refund_AddsPointsBackToWallet`
- `GetTransactions_ReturnsOrderedByDate`

**`Services/ModeSwitchMigrationTests.cs`**:
- `Consolidate_MergesLoyaltyRewardsIntoWallet`
- `Split_DistributesByNetEarn_Proportional`
- `Split_TenantWithNegativeNetEarn_GetsZero`
- `Split_FreezesWallet`

**`Services/OrderWorkflowAllianceTests.cs`**:
- `ProcessLoyaltyPoints_AllianceMode_RoutesToAllianceWallet`
- `ProcessLoyaltyPoints_SiloMode_RoutesToLoyaltyRewards`
- `ProcessLoyaltyPoints_AllianceMode_TenantOptOut_FallsToSilo`

**`Services/RedemptionAllianceTests.cs`**:
- `Redeem_AllianceMode_DeductsFromWallet`
- `Redeem_AllianceMode_TenantNotMember_ReturnsError`
- `Redeem_SiloMode_UsesExistingFlow`

---

## Phase 6B — E2E Tests (Session 12)

### File to create: `6_Testing/e2e-tests/loyalty-alliance.spec.ts`

```typescript
test('Loyalty Alliance: earn at A, redeem at B', async ({ page }) => {
    // 1. Admin: set global mode = Alliance
    // 2. Admin: set tenant A + B as alliance members
    // 3. Customer: place order at tenant A
    // 4. Admin: transition order to completed
    // 5. Customer: check wallet → points earned
    // 6. Customer: redeem at tenant B → success
    // 7. Customer: check wallet → balance reduced
    // 8. Admin: cancel voucher → refund to tenant B
});

test('Loyalty Silo: existing flow unchanged', async ({ page }) => {
    // 1. Admin: set global mode = Silo
    // 2. Customer: place order → complete → earn points (SQLite)
    // 3. Customer: redeem at same tenant → success
    // 4. Customer: try redeem at different tenant → fails
});
```

---

## Phase 7 — VPS Runtime Verification (Session 13)

### Prerequisites
- All code merged to main branch
- CD pipeline completed: `git push origin main` → CI build → CD deploy → VPS containers restarted
- PG migration applied automatically by CD (or manual `dotnet ef database update` in Gateway container)

### Step 1: Verify PG Migration Applied

```bash
# SSH into VPS
ssh -i C:\VibeCoding\CD\SSH\vanan.pem ubuntu@<vps-ip>

# Check new tables exist in PostgreSQL
docker exec gateway psql -U postgres -d vanan -c "\dt \"LoyaltyGlobalConfigs\""
docker exec gateway psql -U postgres -d vanan -c "\dt \"LoyaltyTenantConfigs\""
docker exec gateway psql -U postgres -d vanan -c "\dt \"AllianceWallets\""
docker exec gateway psql -U postgres -d vanan -c "\dt \"AllianceTransactions\""

# Check default global config row exists
docker exec gateway psql -U postgres -d vanan -c "SELECT * FROM \"LoyaltyGlobalConfigs\";"
```

### Step 2: SystemAdmin — Configure Alliance Mode

```powershell
# Login as SystemAdmin
$loginResp = Invoke-RestMethod -Uri "https://khachvip.online/api/platform/login" `
    -Method Post -ContentType "application/json" `
    -Body '{"email":"sysadmin@vanan.vn","password":"Admin@123"}'
$sessionCookie = $loginResp.Headers["Set-Cookie"]

# Set global mode = Alliance
Invoke-RestMethod -Uri "https://api.khachvip.online/api/platform/loyalty/config" `
    -Method Put -ContentType "application/json" `
    -Headers @{Cookie=$sessionCookie} `
    -Body '{"mode":"Alliance","maxWalletPoints":100000}'

# Set tenant A as alliance member
$tenantA = "eb7f9261-0751-4ff9-b0b2-b3698949cc80"
Invoke-RestMethod -Uri "https://api.khachvip.online/api/platform/loyalty/tenant/$tenantA/config" `
    -Method Put -ContentType "application/json" `
    -Headers @{Cookie=$sessionCookie} `
    -Body '{"isAllianceMember":true,"mode":"Alliance"}'
```

### Step 3: Customer — Earn Points (Order Complete)

```powershell
# Customer OTP login
$phone = "0901234567"
Invoke-RestMethod -Uri "https://api.khachvip.online/api/customer/otp/send" `
    -Method Post -ContentType "application/json" `
    -Body '{"phoneNumber":"'$phone'","tenantId":"'$tenantA'"}'
# Get OTP from response header X-Dev-OTP (dev mode) or use dev bypass header

$otpResp = Invoke-RestMethod -Uri "https://api.khachvip.online/api/customer/otp/verify" `
    -Method Post -ContentType "application/json" `
    -Body '{"phoneNumber":"'$phone'","otp":"123456","tenantId":"'$tenantA'"}'
$customerToken = $otpResp.customerToken

# Place order (use existing checkout API)
$orderResp = Invoke-RestMethod -Uri "https://api.khachvip.online/api/public/orders/checkout" `
    -Method Post -ContentType "application/json" `
    -Body '{...}'
$orderId = $orderResp.orderId

# Complete order via admin
Invoke-RestMethod -Uri "https://khachvip.online/api/orderworkflow/$orderId/status" `
    -Method Put -ContentType "application/json" `
    -Headers @{Cookie=$sessionCookie} `
    -Body '{"status":"completed"}'
```

### Step 4: Verify Wallet + Transactions

```powershell
# Customer checks wallet
$wallet = Invoke-RestMethod -Uri "https://api.khachvip.online/api/loyalty/wallet" `
    -Headers @{"X-Customer-Token"=$customerToken}
Write-Host "Total balance: $($wallet.totalPointBalance)"
Write-Host "Breakdown: $($wallet.breakdown | ConvertTo-Json)"
```

```bash
# Verify in PostgreSQL
docker exec gateway psql -U postgres -d vanan -c "
  SELECT w.\"TotalPointBalance\", t.\"Type\", t.\"Points\", t.\"TenantId\", t.\"Reason\"
  FROM \"AllianceTransactions\" t
  JOIN \"AllianceWallets\" w ON t.\"WalletId\" = w.\"Id\"
  ORDER BY t.\"TransactionAt\" DESC LIMIT 5;
"

# Verify NATS sync to SQLite
docker exec shoperp sqlite3 /data/shoperp.db "
  SELECT lr.PointBalance, c.PhoneNumber
  FROM LoyaltyRewards lr
  JOIN Customers c ON lr.CustomerId = c.Id
  ORDER BY lr.UpdatedAt DESC LIMIT 5;
"
```

### Step 5: Cross-Tenant Redeem

```powershell
# Customer redeems at tenant B
$tenantB = "<tenant-B-guid>"
$catalogItemId = "<catalog-item-guid>"

$redeemResp = Invoke-RestMethod -Uri "https://api.khachvip.online/api/redemption/redeem" `
    -Method Post -ContentType "application/json" `
    -Headers @{"X-Customer-Token"=$customerToken} `
    -Body '{"catalogItemId":"'$catalogItemId'","tenantId":"'$tenantB'"}'
Write-Host "Voucher: $($redeemResp.voucherCode)"
Write-Host "New balance: $($redeemResp.newPointBalance)"
```

### Step 6: Verify Voucher + Logs

```bash
# Verify voucher in tenant B's SQLite
docker exec shoperp sqlite3 /data/shoperp.db "
 SELECT VoucherCode, Status, ExpiresAt FROM Vouchers ORDER BY CreatedAt DESC LIMIT 1;
"

# Check wallet balance decreased
docker exec gateway psql -U postgres -d vanan -c "
 SELECT \"TotalPointBalance\" FROM \"AllianceWallets\" ORDER BY \"UpdatedAt\" DESC LIMIT 1;
"

# Check docker logs for errors
docker logs gateway --tail 50 2>&1 | grep -i "error\|exception"
docker logs shoperp --tail 50 2>&1 | grep -i "error\|exception"
docker logs nats --tail 20 2>&1 | grep -i "error"
```

### Step 7: Mode Switch Back to Silo (Split Migration)

```powershell
# Trigger split migration
Invoke-RestMethod -Uri "https://api.khachvip.online/api/platform/loyalty/migrate" `
    -Method Post -ContentType "application/json" `
    -Headers @{Cookie=$sessionCookie} `
    -Body '{"tenantId":"'$tenantA'","targetMode":"Silo"}'
```

```bash
# Verify wallet frozen
docker exec gateway psql -U postgres -d vanan -c "
 SELECT \"IsActive\", \"TotalPointBalance\" FROM \"AllianceWallets\";
"

# Verify points distributed back to SQLite
docker exec shoperp sqlite3 /data/shoperp.db "
 SELECT lr.PointBalance FROM LoyaltyRewards lr
 ORDER BY lr.UpdatedAt DESC LIMIT 5;
"
```

### Acceptance Criteria
- [ ] All 4 PG tables exist with correct schema
- [ ] SystemAdmin can set global + per-tenant config
- [ ] Customer earns points → AllianceWallet balance increases
- [ ] NATS sync updates SQLite LoyaltyRewards.PointBalance
- [ ] Cross-tenant redeem creates voucher in target tenant SQLite
- [ ] Wallet balance decreases after redeem
- [ ] Docker logs show no errors/exceptions
- [ ] Mode switch back to Silo freezes wallet + distributes points back
