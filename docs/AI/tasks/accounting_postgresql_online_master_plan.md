# Master Plan: Accounting Always-Online + PostgreSQL + Test Enforcement

> **Created:** 2026-07-09
> **Status:** PENDING APPROVAL
> **Mode:** ANALYZE → IMPLEMENT (awaiting user approval)
> **Branch:** main
> **Objective:** (1) Chuyển Accounting module trong ShopERP từ SQLite sang PostgreSQL direct-write — đáp ứng ADR-001 "accounting always online" + Thông tư 200/2014/TT-BTC + Thông tư 152/2025/TT-BTC. (2) Bổ sung Architecture Tests (Rule J/K/L) để đảm bảo yêu cầu này không bị vi phạm lại.

---

## 0. REFERENCE: VI PHẠM ĐÃ XẢY RA

### Quy định gốc (ngay từ đầu)

| Tài liệu | Ngày | Quy định |
|----------|------|----------|
| ADR-001 (SQLite Offline-First) | 2026-06-01 | "SQLite local + NATS + **PostgreSQL cloud**" — Accounting online PostgreSQL, SQLite chỉ cho order/kitchen stations |
| ADR-001-Station-Architecture | 2026-06-29 | v1 SaaS: Accounting = "Online PostgreSQL"; v2 Hybrid: Accounting = "Always online PostgreSQL" |
| ADR-003 (Accounting Immutability) | 2026-06-01 | Thông tư 200 + 152 compliance — audit trail bắt buộc, dữ liệu kế toán phải persistent online |
| UNIFIED_ROADMAP_master_plan | 2026-07 | "PostgreSQL remains online for accounting — v2 hybrid uses SQLite for order/loyalty only" |
| project_state.md line 118 | 2026-07-09 | "ADR-001: SQLite local + NATS sync + PostgreSQL cloud (accounting always online)" |

### Timeline vi phạm

| # | Ngày | Commit | Sự kiện | Mức |
|---|------|--------|---------|-----|
| 1 | 2026-06-03 | `957ac95` | PeriodClosingService đăng ký DI trong ShopERP, inject `IVanAnDbContext` → resolve `ShopERPDbContext` (SQLite). Accounting service đầu tiên chạy trên SQLite. | 🔴 Gốc rễ |
| 2 | 2026-06-25 | `754e2b3` (PR #55) | Thêm PostgreSQL connection string vào ShopERP → crash: *"Connection string keyword 'host' is not supported"* (hardcoded `UseSqlite()`) | ⚠️ Sửa sai cách |
| 3 | 2026-06-25 | `cf05eb1` (PR #56) | Revert PR #55, ghi "ShopERP uses SQLite (offline-first)" — hiểu SAI ADR-001. Accounting bị cemented trên SQLite. | 🔴 Khẳng định vi phạm |
| 4 | 2026-07-04 | `a3c9242` (VAS W3) | AccountChartService đổi `VanAnDbContext` → `IVanAnDbContext` "for ShopERP DI compatibility". 124 accounts chạy trên SQLite. | 🔴 Lan rộng |
| 5 | 2026-07-05 | `34ac67b` (SaaS W0) | Gateway Option B — CoreHub in-process (PostgreSQL). ShopERP vẫn giữ accounting services riêng trên SQLite → 2 bản sao, không sync. | ⚠️ Song song |

### Root cause

1. **Hardcoded `UseSqlite()`** trong ShopERP `Program.cs` (line 76-77) — không có provider auto-detect như Gateway
2. **Accounting services đăng ký DI trong ShopERP** thay vì chỉ trong Gateway/CoreHub
3. **`IVanAnDbContext` resolve thành `ShopERPDbContext` (SQLite)** thay vì `VanAnDbContext` (PostgreSQL)
4. **Không có test/enforcement** nào chặn vi phạm — Architecture Tests Rule H chỉ check docker-compose có `Host=postgres`, không check code path

---

## 1. SCOPE — CÁI GÌ CHUYỂN, CÁI GÌ GIỮ

### 1.1. DbSets chuyển sang PostgreSQL (6 accounting tables)

| DbSet | Loại | Immutability | Lý do |
|-------|------|--------------|-------|
| `AccountingEntries` | Core accounting | Append-only | ADR-003 compliance, audit trail |
| `JournalEntries` | Double-entry | Append-only | VAS reports (BS/IS/CF/TB) |
| `AuditLogs` | Audit trail | Append-only | Thông tư 200 audit requirement |
| `PeriodClosingStatuses` | Period state | Mutable (Open→Closed→Open) | Period integrity |
| `AccountCharts` | Reference data | Seeded (global, not tenant-scoped) | 124 VAS accounts — single source of truth |
| `PendingInvoiceQueues` | E-Invoice queue | Mutable | E-invoice orchestration |

### 1.2. DbSets GIỮ trong SQLite (Business + Platform)

| DbSet | Lý do giữ SQLite |
|-------|------------------|
| Orders, OrderItems, Products, Customers, Inventories, Ingredients, Recipes, Shops | Business operational data — offline-first per ADR-001 |
| OutboxMessages | Event sourcing local — NATS sync to PostgreSQL |
| Tenants, Users, UserTenants, PermissionGroups, UserPermissionGroups | Platform auth — local for offline login |
| ApiKeys, PushSubscriptions | Platform integration |
| LoyaltyRewards, SocialCampaigns | Social flywheel — business-specific |
| PlatformUsers | Platform admin — cross-tenant (decide later, keep SQLite for now) |

### 1.3. Services cần update (10 direct + 3 repository)

**Direct `IVanAnDbContext` injection → đổi sang `IAccountingDbContext`:**

| # | Service | DbSets used | File |
|---|---------|-------------|------|
| 1 | PeriodClosingService | PeriodClosingStatuses | `3_CoreHub/Services/PeriodClosingService.cs` |
| 2 | BalanceSheetService | JournalEntries | `3_CoreHub/Services/BalanceSheetService.cs` |
| 3 | IncomeStatementService | JournalEntries | `3_CoreHub/Services/IncomeStatementService.cs` |
| 4 | CashFlowStatementService | JournalEntries | `3_CoreHub/Services/CashFlowStatementService.cs` |
| 5 | TrialBalanceService | JournalEntries | `3_CoreHub/Services/TrialBalanceService.cs` |
| 6 | AccountChartService | AccountCharts | `3_CoreHub/Services/AccountChartService.cs` |
| 7 | HKDBookGenerationService | JournalEntries, Tenants | `3_CoreHub/Services/HKDBookGenerationService.cs` |
| 8 | VasFeatureFlagService | Tenants | `3_CoreHub/Services/VasFeatureFlagService.cs` |
| 9 | TenantConversionService | Tenants, AccountingEntries | `3_CoreHub/Services/TenantConversionService.cs` |
| 10 | SmartPreAggregationService | JournalEntries | `3_CoreHub/Services/SmartPreAggregationService.cs` |

**Via repository → repository đổi `IVanAnDbContext` → `IAccountingDbContext`:**

| # | Repository | DbSets used | File |
|---|-----------|-------------|------|
| 11 | AccountingEntryRepository | AccountingEntries | `3_CoreHub/Repositories/AccountingEntryRepository.cs` |
| 12 | AuditLogRepository | AuditLogs | `3_CoreHub/Repositories/AuditLogRepository.cs` |
| 13 | HKDBookRepository | JournalEntries | `3_CoreHub/Repositories/HKDBookRepository.cs` |

**Services KHÔNG cần update (không inject DbContext):**

TemplateFactory, ProductionFormulaEngine, ScopedDataProvider, DashboardService, HkdToEnterpriseAccountMapper — factory/caching/mapping layer, không có DB access.

### 1.4. Services cần DUAL injection (Accounting + Business)

| Service | Accounting DbSet | Business/Platform DbSet | Lý do |
|---------|------------------|------------------------|-------|
| TenantConversionService | AccountingEntries | Tenants | Chuyển đổi HKD→DN — cần cả accounting + tenant info |
| HKDBookGenerationService | JournalEntries | Tenants | Tạo HKD book — cần journal entries + tenant type |
| VasFeatureFlagService | — | Tenants | Đọc VAS feature flag từ Tenant — **chỉ Business, KHÔNG cần Accounting** |

**Decision:** VasFeatureFlagService giữ `IVanAnDbContext` (chỉ cần Tenants — Business/Platform). TenantConversionService + HKDBookGenerationService cần dual injection.

---

## 2. ARCHITECTURE DESIGN

### 2.1. IAccountingDbContext interface (NEW)

```csharp
// 3_CoreHub/Infrastructure/IAccountingDbContext.cs
namespace VanAn.CoreHub.Infrastructure
{
    /// <summary>
    /// Accounting-only DbContext abstraction — always PostgreSQL (online).
    /// Enforces ADR-001: "accounting always online" + ADR-003 immutability compliance.
    /// </summary>
    public interface IAccountingDbContext : IDisposable
    {
        DbSet<AccountingEntry> AccountingEntries { get; }
        DbSet<JournalEntry> JournalEntries { get; }
        DbSet<AuditLog> AuditLogs { get; }
        DbSet<PendingInvoiceQueue> PendingInvoiceQueues { get; }
        DbSet<VanAn.CoreHub.Infrastructure.Entities.AccountChartEntity> AccountCharts { get; }
        DbSet<VanAn.CoreHub.Infrastructure.Entities.PeriodClosingStatusEntity> PeriodClosingStatuses { get; }

        Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
        Task<IDbContextTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default);
    }
}
```

### 2.2. VanAnDbContext implements IAccountingDbContext

`VanAnDbContext` đã có đầy đủ 6 accounting DbSets. Thêm `IAccountingDbContext` vào interface list:

```csharp
public class VanAnDbContext(DbContextOptions<VanAnDbContext> options) : DbContext(options), IVanAnDbContext, IAccountingDbContext
```

Không cần tạo `AccountingDbContext` riêng — `VanAnDbContext` đã là PostgreSQL context với đầy đủ config.

### 2.3. ShopERPDbContext — remove accounting DbSets

`ShopERPDbContext` (SQLite) giữ Business + Platform DbSets, **xóa 6 accounting DbSets**:

| DbSet | Action |
|-------|--------|
| `AccountingEntries` | ❌ Remove |
| `JournalEntries` | ❌ Remove |
| `AuditLogs` | ❌ Remove |
| `PendingInvoiceQueues` | ❌ Remove |
| `AccountCharts` | ❌ Remove |
| `PeriodClosingStatuses` | ❌ Remove |
| `HKDBooks` | ❌ Remove (already ignored in OnModelCreating, nhưng DbSet declaration dư) |

**Lưu ý:** `IVanAnDbContext` interface vẫn có 6 accounting DbSets → `ShopERPDbContext` implement `IVanAnDbContext` sẽ break. Cần xử lý — xem Phase 2.

### 2.4. IVanAnDbContext — split hay giữ?

**Option A (chọn): Giữ IVanAnDbContext nguyên, ShopERPDbContext throw NotImplementedException cho accounting DbSets**

```csharp
// ShopERPDbContext.cs
public DbSet<AccountingEntry> AccountingEntries 
{ 
    get => throw new InvalidOperationException("AccountingEntries is on PostgreSQL (IAccountingDbContext), not SQLite (ShopERPDbContext). Use IAccountingDbContext."); 
    set { } 
}
```

**Lý do chọn Option A:**
- Không break existing code inject `IVanAnDbContext` (Business services vẫn dùng)
- Throw clear error nếu ai vô tình query accounting qua SQLite context
- Architecture Test Rule K sẽ catch DbSet declaration trong ShopERPDbContext

**Option B (reject): Split IVanAnDbContext thành IBusinessDbContext + IAccountingDbContext**
- Break tất cả services inject `IVanAnDbContext` — quá invasive
- 27 DbSets cần phân lại — rủi ro cao

### 2.5. DI Registration trong ShopERP Program.cs

```csharp
// SQLite — Business + Platform (existing, modified)
builder.Services.AddDbContext<ShopERPDbContext>(options =>
    options.UseSqlite(connectionString));
builder.Services.AddScoped<IVanAnDbContext>(provider => 
    provider.GetRequiredService<ShopERPDbContext>());

// PostgreSQL — Accounting (NEW)
string accountingConnectionString = builder.Configuration.GetConnectionString("AccountingConnection")
    ?? throw new InvalidOperationException("ConnectionStrings:AccountingConnection is required — accounting must be online (ADR-001).");
builder.Services.AddDbContext<VanAnDbContext>(options =>
    options.UseNpgsql(accountingConnectionString), ServiceLifetime.Scoped);
builder.Services.AddScoped<IAccountingDbContext>(provider =>
    provider.GetRequiredService<VanAnDbContext>());
```

### 2.6. Docker Compose — thêm Accounting connection string

```yaml
# docker-compose.yml — shoperp service
environment:
  - ConnectionStrings__DefaultConnection=Data Source=/data/shoperp.db  # SQLite (Business)
  - ConnectionStrings__AccountingConnection=Host=postgres;Port=5432;Database=${POSTGRES_DB:-VanAnCoreHub};Username=${POSTGRES_USER:-vanan_admin};Password=${POSTGRES_PASSWORD}  # PostgreSQL (Accounting)
```

### 2.7. Data Flow (sau khi implement)

```
ShopERP (5003)
  ├─ Business Services → IVanAnDbContext → ShopERPDbContext (SQLite)
  │   Orders, Products, Customers, Inventories, Tenants, Users, Outbox...
  │
  └─ Accounting Services → IAccountingDbContext → VanAnDbContext (PostgreSQL)
      AccountingEntries, JournalEntries, AuditLogs, AccountCharts,
      PeriodClosingStatuses, PendingInvoiceQueues

Gateway (5001) — Option B (in-process CoreHub)
  └─ CoreHub Services → IVanAnDbContext → VanAnDbContext (PostgreSQL)
      Toàn bộ (Business + Accounting) — Gateway là central, không có SQLite
```

---

## 3. TEST ENFORCEMENT — ARCHITECTURE TESTS

### 3.1. Rule J: ShopERP accounting services phải inject IAccountingDbContext

**File:** `6_Tests/VanAn.Architecture.Tests/ArchitectureRulesTests.cs`

```csharp
[Fact(DisplayName = "Rule J: ADR-001 - ShopERP accounting services MUST inject IAccountingDbContext (PostgreSQL), NOT IVanAnDbContext (SQLite)")]
public void ShopERP_AccountingServices_MustInject_IAccountingDbContext()
{
    // Arrange
    var repoRoot = GetRepoRoot();
    var servicesPath = Path.Combine(repoRoot, "3_CoreHub", "Services");
    
    // Accounting services that MUST use IAccountingDbContext
    var accountingServices = new[]
    {
        "PeriodClosingService.cs",
        "BalanceSheetService.cs",
        "IncomeStatementService.cs",
        "CashFlowStatementService.cs",
        "TrialBalanceService.cs",
        "AccountChartService.cs",
        "SmartPreAggregationService.cs",
        "AccountingEntryService.cs",
        "ReversalService.cs",
        "AuditTrailService.cs",
        "HKDBookService.cs"
    };
    
    var violations = new List<string>();
    
    foreach (var serviceFile in accountingServices)
    {
        var fullPath = Path.Combine(servicesPath, serviceFile);
        if (!File.Exists(fullPath)) continue;
        
        var content = File.ReadAllText(fullPath);
        
        // MUST inject IAccountingDbContext
        if (!content.Contains("IAccountingDbContext"))
        {
            violations.Add($"{serviceFile}: missing IAccountingDbContext injection");
        }
        
        // MUST NOT inject IVanAnDbContext for accounting-only services
        // Exception: TenantConversionService, HKDBookGenerationService need dual injection
        if (content.Contains("IVanAnDbContext") && 
            !serviceFile.Contains("TenantConversion") && 
            !serviceFile.Contains("HKDBookGeneration") &&
            !serviceFile.Contains("VasFeatureFlag"))
        {
            violations.Add($"{serviceFile}: still injects IVanAnDbContext (SQLite) — must use IAccountingDbContext (PostgreSQL)");
        }
    }
    
    Assert.True(violations.Count == 0,
        "ADR-001 violation: Accounting services must use IAccountingDbContext (PostgreSQL, online). " +
        "SQLite (IVanAnDbContext) is for Business data only.\n" +
        string.Join("\n", violations));
}
```

### 3.2. Rule K: ShopERPDbContext (SQLite) không có accounting DbSets

```csharp
[Fact(DisplayName = "Rule K: ADR-001 - ShopERPDbContext (SQLite) MUST NOT contain accounting DbSets")]
public void ShopERPDbContext_MustNotContain_AccountingDbSets()
{
    // Arrange
    var repoRoot = GetRepoRoot();
    var dbContextPath = Path.Combine(repoRoot, "5_WebApps", "ShopERP", "Infrastructure", "ShopERPDbContext.cs");
    
    if (!File.Exists(dbContextPath))
        Assert.Fail($"ShopERPDbContext.cs not found: {dbContextPath}");
    
    var content = File.ReadAllText(dbContextPath);
    
    // Accounting DbSets that MUST NOT be in SQLite context
    var forbiddenDbSets = new[]
    {
        "DbSet<AccountingEntry> AccountingEntries",
        "DbSet<JournalEntry> JournalEntries",
        "DbSet<AuditLog> AuditLogs",
        "DbSet<PendingInvoiceQueue> PendingInvoiceQueues",
        "DbSet<AccountChartEntity> AccountCharts",
        "DbSet<PeriodClosingStatusEntity> PeriodClosingStatuses"
    };
    
    var violations = new List<string>();
    foreach (var dbSet in forbiddenDbSets)
    {
        // Allow throw NotImplementedException pattern (Option A)
        // But flag if it's a real DbSet declaration with { get; set; }
        var lines = content.Split('\n');
        foreach (var line in lines)
        {
            if (line.Contains(dbSet) && !line.Contains("throw new InvalidOperationException"))
            {
                violations.Add($"Found accounting DbSet in SQLite context: {line.Trim()}");
            }
        }
    }
    
    Assert.True(violations.Count == 0,
        "ADR-001 violation: ShopERPDbContext (SQLite) must not contain accounting DbSets. " +
        "Accounting data is always online on PostgreSQL (IAccountingDbContext).\n" +
        string.Join("\n", violations));
}
```

### 3.3. Rule L: docker-compose ShopERP có AccountingConnection (PostgreSQL)

```csharp
[Fact(DisplayName = "Rule L: ADR-001 - docker-compose ShopERP MUST have AccountingConnection (PostgreSQL)")]
public void DockerCompose_ShopERP_MustHave_AccountingConnection_PostgreSQL()
{
    // Arrange
    var repoRoot = GetRepoRoot();
    var composeFiles = new[]
    {
        Path.Combine(repoRoot, "docker-compose.yml"),
        Path.Combine(repoRoot, "docker-compose.prod.yml")
    };
    
    foreach (var composeFile in composeFiles)
    {
        if (!File.Exists(composeFile)) continue;
        
        var content = File.ReadAllText(composeFile);
        
        // ShopERP MUST have AccountingConnection env var pointing to PostgreSQL
        var hasAccountingConnection = content.Contains("AccountingConnection") ||
                                       content.Contains("Accounting__ConnectionString");
        
        var hasPostgresHost = content.Contains("Host=postgres") ||
                              content.Contains("postgres:5432");
        
        Assert.True(hasAccountingConnection,
            $"ADR-001 violation: {Path.GetFileName(composeFile)} ShopERP must have AccountingConnection env var " +
            "(ConnectionStrings__AccountingConnection=Host=postgres...). " +
            "Accounting is always online on PostgreSQL.");
        
        Assert.True(hasPostgresHost,
            $"ADR-001 violation: {Path.GetFileName(composeFile)} must reference PostgreSQL host for accounting.");
    }
}
```

### 3.4. Rule M: ShopERP Program.cs đăng ký IAccountingDbContext với UseNpgsql

```csharp
[Fact(DisplayName = "Rule M: ADR-001 - ShopERP Program.cs MUST register IAccountingDbContext with UseNpgsql (PostgreSQL)")]
public void ShopERP_ProgramCs_MustRegister_IAccountingDbContext_Npgsql()
{
    // Arrange
    var repoRoot = GetRepoRoot();
    var programCsPath = Path.Combine(repoRoot, "5_WebApps", "ShopERP", "Program.cs");
    
    if (!File.Exists(programCsPath))
        Assert.Fail($"Program.cs not found: {programCsPath}");
    
    var content = File.ReadAllText(programCsPath);
    
    // MUST register IAccountingDbContext
    Assert.True(content.Contains("IAccountingDbContext"),
        "ADR-001 violation: ShopERP Program.cs must register IAccountingDbContext. " +
        "Accounting services inject IAccountingDbContext (PostgreSQL, online).");
    
    // MUST use UseNpgsql for accounting context (not UseSqlite)
    Assert.True(content.Contains("UseNpgsql"),
        "ADR-001 violation: ShopERP Program.cs must call UseNpgsql for accounting DbContext. " +
        "Accounting is always online on PostgreSQL.");
    
    // MUST NOT use UseSqlite for accounting context
    // (UseSqlite is OK for ShopERPDbContext — Business data)
    // Check: no line with both UseSqlite and Accounting
    var lines = content.Split('\n');
    var accountingSqliteLine = lines.FirstOrDefault(l => 
        l.Contains("UseSqlite") && l.Contains("Accounting"));
    
    Assert.True(accountingSqliteLine == null,
        "ADR-001 violation: ShopERP Program.cs must not use UseSqlite for accounting context. " +
        $"Found: {accountingSqliteLine?.Trim()}");
}
```

---

## 4. DETAILED IMPLEMENTATION PLAN

### Phase 1: Create IAccountingDbContext + implement on VanAnDbContext — 3 edits

| Step | File | Action | Chi tiết |
|-----|------|--------|----------|
| 1.1 | `3_CoreHub/Infrastructure/IAccountingDbContext.cs` | Create | New interface with 6 accounting DbSets (see §2.1) |
| 1.2 | `3_CoreHub/Infrastructure/VanAnDbContext.cs` | Edit | Add `IAccountingDbContext` to class declaration: `: DbContext(options), IVanAnDbContext, IAccountingDbContext` |
| 1.3 | `dotnet build` | Verify | Build pass — VanAnDbContext đã có 6 DbSets, chỉ thêm interface |

**Verify Phase 1:**
- `dotnet build VanAn.sln` → 0 errors
- `VanAnDbContext` implement cả `IVanAnDbContext` + `IAccountingDbContext`

---

### Phase 2: Update ShopERPDbContext — remove accounting DbSets — 1 edit

| Step | File | Action | Chi tiết |
|-----|------|--------|----------|
| 2.1 | `5_WebApps/ShopERP/Infrastructure/ShopERPDbContext.cs` | Edit | Xóa 6 DbSet declarations (AccountingEntries, JournalEntries, AuditLogs, PendingInvoiceQueues, AccountCharts, PeriodClosingStatuses) + HKDBooks. Thêm throw `NotImplementedException` implementations cho `IVanAnDbContext` accounting DbSets (Option A). |

**Lưu ý:** `ShopERPDbContext` implement `IVanAnDbContext` — interface vẫn có 6 accounting DbSets. Cần giữ property declarations nhưng throw nếu access:

```csharp
// Accounting DbSets — NOT available on SQLite, use IAccountingDbContext (PostgreSQL)
public DbSet<AccountingEntry> AccountingEntries
{
    get => throw new InvalidOperationException(
        "AccountingEntries is on PostgreSQL (IAccountingDbContext), not SQLite (ShopERPDbContext). " +
        "Inject IAccountingDbContext for accounting operations. ADR-001: accounting always online.");
    set { }
}
// ... same pattern for 5 other accounting DbSets
```

**Verify Phase 2:**
- `dotnet build` → 0 errors (ShopERPDbContext vẫn implement IVanAnDbContext, chỉ throw cho accounting)
- Existing Business services vẫn build (không query accounting DbSets)

---

### Phase 3: Update DI Registration in ShopERP Program.cs — 1 edit

| Step | File | Action | Chi tiết |
|-----|------|--------|----------|
| 3.1 | `5_WebApps/ShopERP/Program.cs` | Edit | Thêm registration cho `VanAnDbContext` (PostgreSQL) + `IAccountingDbContext`. Đọc `ConnectionStrings:AccountingConnection` — throw nếu missing. |

**Code:**
```csharp
// === Business + Platform (SQLite — offline-first) ===
string sqliteConnectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? $"Data Source={Path.Combine(AppContext.BaseDirectory, "vanan_shoperp.db")}";
builder.Services.AddDbContext<ShopERPDbContext>(options =>
    options.UseSqlite(sqliteConnectionString));
builder.Services.AddScoped<IVanAnDbContext>(provider =>
    provider.GetRequiredService<ShopERPDbContext>());

// === Accounting (PostgreSQL — always online, ADR-001) ===
string accountingConnectionString = builder.Configuration.GetConnectionString("AccountingConnection")
    ?? throw new InvalidOperationException(
        "ConnectionStrings:AccountingConnection is required — accounting must be online (ADR-001). " +
        "Set ConnectionStrings__AccountingConnection=Host=postgres;Port=5432;...");
builder.Services.AddDbContext<VanAn.CoreHub.Infrastructure.VanAnDbContext>(options =>
    options.UseNpgsql(accountingConnectionString));
builder.Services.AddScoped<VanAn.CoreHub.Infrastructure.IAccountingDbContext>(provider =>
    provider.GetRequiredService<VanAn.CoreHub.Infrastructure.VanAnDbContext>());
```

**Verify Phase 3:**
- `dotnet build` → 0 errors
- ShopERP startup: nếu thiếu `AccountingConnection` → throw clear error

---

### Phase 4: Update 10 Services + 3 Repositories — 13 edits

| Step | File | Action | Chi tiết |
|-----|------|--------|----------|
| 4.1 | `3_CoreHub/Repositories/AccountingEntryRepository.cs` | Edit | `IVanAnDbContext` → `IAccountingDbContext` |
| 4.2 | `3_CoreHub/Repositories/AuditLogRepository.cs` | Edit | `IVanAnDbContext` → `IAccountingDbContext` |
| 4.3 | `3_CoreHub/Repositories/HKDBookRepository.cs` | Edit | `IVanAnDbContext` → `IAccountingDbContext` |
| 4.4 | `3_CoreHub/Services/PeriodClosingService.cs` | Edit | `IVanAnDbContext` → `IAccountingDbContext` |
| 4.5 | `3_CoreHub/Services/BalanceSheetService.cs` | Edit | `IVanAnDbContext` → `IAccountingDbContext` |
| 4.6 | `3_CoreHub/Services/IncomeStatementService.cs` | Edit | `IVanAnDbContext` → `IAccountingDbContext` |
| 4.7 | `3_CoreHub/Services/CashFlowStatementService.cs` | Edit | `IVanAnDbContext` → `IAccountingDbContext` |
| 4.8 | `3_CoreHub/Services/TrialBalanceService.cs` | Edit | `IVanAnDbContext` → `IAccountingDbContext` |
| 4.9 | `3_CoreHub/Services/AccountChartService.cs` | Edit | `IVanAnDbContext` → `IAccountingDbContext` |
| 4.10 | `3_CoreHub/Services/SmartPreAggregationService.cs` | Edit | `IVanAnDbContext` → `IAccountingDbContext` |
| 4.11 | `3_CoreHub/Services/AccountingEntryService.cs` | Edit | Inject `IAccountingDbContext` (via repository — repo đã đổi ở 4.1, service tự động) |
| 4.12 | `3_CoreHub/Services/TenantConversionService.cs` | Edit | **Dual injection**: `IAccountingDbContext` (AccountingEntries) + `IVanAnDbContext` (Tenants) |
| 4.13 | `3_CoreHub/Services/HKDBookGenerationService.cs` | Edit | **Dual injection**: `IAccountingDbContext` (JournalEntries) + `IVanAnDbContext` (Tenants) |

**VasFeatureFlagService:** KHÔNG đổi — chỉ cần Tenants (Business), giữ `IVanAnDbContext`.

**Verify Phase 4:**
- `dotnet build` → 0 errors
- grep `IVanAnDbContext` trong 10 accounting service files → chỉ TenantConversionService + HKDBookGenerationService còn (dual)

---

### Phase 5: Update Docker Compose — 2 edits

| Step | File | Action | Chi tiết |
|-----|------|--------|----------|
| 5.1 | `docker-compose.yml` | Edit | Thêm `ConnectionStrings__AccountingConnection=Host=postgres;Port=5432;Database=...` vào shoperp environment |
| 5.2 | `docker-compose.prod.yml` | Edit | Thêm `ConnectionStrings__AccountingConnection=Host=postgres;Port=5432;Database=...` vào shoperp environment |

**Verify Phase 5:**
- `docker compose -f docker-compose.yml config` → valid
- ShopERP container có env `ConnectionStrings__AccountingConnection`

---

### Phase 6: Update appsettings — 2 edits

| Step | File | Action | Chi tiết |
|-----|------|--------|----------|
| 6.1 | `5_WebApps/ShopERP/appsettings.json` | Edit | Thêm `"AccountingConnection": "Host=localhost;Port=5432;Database=VanAnCoreHub;Username=vanan_admin;Password=vanan_password"` (dev default) |
| 6.2 | `5_WebApps/ShopERP/appsettings.Development.json` | Edit | Thêm `"AccountingConnection": "Host=localhost;Port=5432;Database=VanAnCoreHub;Username=vanan_admin;Password=vanan_password"` |

**Verify Phase 6:**
- ShopERP local dev: đọc `AccountingConnection` từ appsettings → không throw

---

### Phase 7: Update AccountChartSeeder — 1 edit

| Step | File | Action | Chi tiết |
|-----|------|--------|----------|
| 7.1 | `5_WebApps/ShopERP/Program.cs` (seeder section ~line 490) | Edit | Đổi `IVanAnDbContext vanAnContext = ...GetRequiredService<IVanAnDbContext>()` → `IAccountingDbContext accountingContext = ...GetRequiredService<IAccountingDbContext>()` cho AccountChartSeeder |

**Verify Phase 7:**
- ShopERP startup: AccountChartSeeder chạy trên PostgreSQL, không phải SQLite

---

### Phase 8: Write Architecture Tests (Rule J/K/L/M) — 1 edit

| Step | File | Action | Chi tiết |
|-----|------|--------|----------|
| 8.1 | `6_Tests/VanAn.Architecture.Tests/ArchitectureRulesTests.cs` | Edit | Thêm Rule J, K, L, M (see §3.1-3.4) |

**Verify Phase 8 (TDD):**
- **Red phase (trước implement):** Rule J/K/L/M FAIL — vì accounting vẫn trên SQLite
- **Green phase (sau Phase 1-7):** Rule J/K/L/M PASS

---

### Phase 9: Update Existing Tests — review + fix

| Step | File | Action | Chi tiết |
|-----|------|--------|----------|
| 9.1 | `6_Tests/VanAn.Core.Tests/Accounting/` | Review | Tests inject `IVanAnDbContext` mock — cần đổi sang `IAccountingDbContext` mock cho accounting tests |
| 9.2 | `6_Tests/VanAn.Integration.Tests/` | Review | Integration tests tạo `ShopERPDbContext` in-memory — accounting tests cần tạo separate PostgreSQL context hoặc mock `IAccountingDbContext` |
| 9.3 | `6_Tests/VanAn.ShopERP.Tests/` | Review | ShopERP-specific tests — verify accounting services inject `IAccountingDbContext` |

**Verify Phase 9:**
- `dotnet test` → tất cả tests PASS (sau khi fix mocks)

---

### Phase 10: Build + Full Verification

| Step | Command | Mục đích | Expected |
|-----|---------|----------|----------|
| 10.1 | `dotnet build VanAn.sln` | Build | 0 errors |
| 10.2 | `scripts/guard-check.ps1` | Guard | PASS |
| 10.3 | `dotnet test 6_Tests/VanAn.Architecture.Tests` | Architecture | Rule J/K/L/M PASS |
| 10.4 | `dotnet test 6_Tests/VanAn.Core.Tests --filter "Category!=Performance"` | Core tests | All PASS |
| 10.5 | `docker compose down && docker compose up -d` | Restart | All containers up |
| 10.6 | `curl http://localhost:5003/health` | ShopERP | 200 OK |
| 10.7 | `docker logs vanan-shoperp --tail 20` | ShopERP logs | No SQLite accounting error, no AccountingConnection missing error |
| 10.8 | `curl http://localhost:5003/api/accounting/balance-sheet` | Accounting API | 200 OK (data from PostgreSQL) |

---

## 5. RISKS & MITIGATIONS

| Rủi ro | Mức | Mitigation |
|--------|-----|------------|
| ShopERPDbContext implement IVanAnDbContext nhưng throw cho accounting DbSets → existing code break nếu có ai query accounting qua IVanAnDbContext | MED | Architecture Test Rule K catch. Compiler không catch (interface satisfied). Runtime throw clear error. Audit code: grep `_context.AccountingEntries` trong services inject `IVanAnDbContext` — phải không có. |
| 10 services + 3 repos đổi interface → nhiều file edit, rủi ro miss 1 file | MED | Architecture Test Rule J catch miss. Build sẽ fail nếu service inject interface không tồn tại. |
| TenantConversionService + HKDBookGenerationService cần dual injection → complex | LOW | 2 services rõ ràng. Document trong code comment lý do dual. |
| AccountChartSeeder chạy trên PostgreSQL → cần PostgreSQL available khi ShopERP startup | MED | ShopERP `Program.cs` throw nếu `AccountingConnection` missing. Docker compose có PostgreSQL dependency. Local dev: cần PostgreSQL chạy. |
| Existing tests mock `IVanAnDbContext` cho accounting → break | MED | Phase 9 review + fix. Số lượng tests affected: cần audit. |
| `VanAnDbContext` trong ShopERP + Gateway cùng connect PostgreSQL → 2 connection pools | LOW | EF Core handle connection pooling. Cùng database, khác DbContext instance — OK. |
| Data migration: accounting data hiện có trong SQLite cần migrate sang PostgreSQL | HIGH | **Out of scope plan này** — dev data, có thể re-seed. Production: cần script migration riêng nếu có data thật. |
| Roslyn Analyzers vẫn dead (Tier 4 debt) — không compile-time guard | LOW | Architecture Tests (Rule J/K/L/M) là CI gate, đủ enforce. Analyzer fix là debt riêng. |

---

## 6. EXECUTION ORDER (DEPENDENCY)

```
Phase 1 (IAccountingDbContext interface) ──┐
                                           ├─→ Phase 4 (update 13 services/repos)
Phase 2 (ShopERPDbContext remove accounting)┤
                                           ├─→ Phase 7 (AccountChartSeeder)
Phase 3 (DI registration Program.cs) ──────┤
                                           ├─→ Phase 9 (update existing tests)
Phase 5 (docker-compose) ──────────────────┤
                                           ├─→ Phase 10 (build + verify ALL)
Phase 6 (appsettings) ─────────────────────┤
                                           │
Phase 8 (Architecture Tests) ──────────────┘
                    │
                    └─→ TDD: Red (fail trước) → Green (pass sau Phase 1-7)
```

**Recommended execution:**
1. Phase 8 trước (TDD red — verify tests fail đúng)
2. Phase 1-2 (interface + ShopERPDbContext)
3. Phase 3 (DI registration)
4. Phase 4 (13 services/repos)
5. Phase 5-6 (docker + appsettings)
6. Phase 7 (seeder)
7. Phase 9 (fix existing tests)
8. Phase 10 (full verification — TDD green)

---

## 7. OUT OF SCOPE

| Item | Lý do |
|------|-------|
| Data migration SQLite → PostgreSQL | Dev data, re-seed được. Production migration = task riêng. |
| Roslyn Analyzer wiring fix | Tier 4 debt — Architecture Tests đủ enforce. |
| Split IVanAnDbContext thành IBusinessDbContext + IAccountingDbContext | Quá invasive, Option A (throw) đủ an toàn. |
| Platform DbSets (Tenants, Users) chuyển PostgreSQL | Decision pending — giữ SQLite cho offline login. |
| Outbox sync cho accounting | Không cần — direct PostgreSQL write = source of truth. |
| IAccountingEventService implementation | Không cần — không sync accounting qua NATS. |

---

## 8. GOVERNANCE COMPLIANCE

| Rule | Compliance |
|------|------------|
| ADR-001 (accounting always online) | ✅ Phase 1-7 enforce PostgreSQL direct write |
| ADR-003 (AccountingEntry immutable) | ✅ Không thay đổi Domain — chỉ thay đổi DbContext wiring |
| Domain layer pure | ✅ Không sửa 1_Shared/Domain.cs |
| Clean Architecture | ✅ Dependencies vẫn inward: API → Services → Infrastructure → Domain |
| Multi-tenancy | ✅ TenantId filter vẫn enforce qua cả 2 contexts |
| Hard stop: ShopERP SQLite-only | ⚠️ **Update needed** — project_state.md line 27 ghi "ShopERP SQLite-only" → cần đổi thành "ShopERP SQLite (Business) + PostgreSQL (Accounting)" |
| guard-check.ps1 + dotnet build | ✅ Phase 10 verify |

---

## 9. FILES CHANGED SUMMARY

| # | File | Phase | Action |
|---|------|-------|--------|
| 1 | `3_CoreHub/Infrastructure/IAccountingDbContext.cs` | 1 | Create |
| 2 | `3_CoreHub/Infrastructure/VanAnDbContext.cs` | 1 | Edit (add interface) |
| 3 | `5_WebApps/ShopERP/Infrastructure/ShopERPDbContext.cs` | 2 | Edit (remove accounting DbSets, add throw) |
| 4 | `5_WebApps/ShopERP/Program.cs` | 3, 7 | Edit (DI registration + seeder) |
| 5-14 | `3_CoreHub/Repositories/*.cs` + `3_CoreHub/Services/*.cs` (10 files) | 4 | Edit (IVanAnDbContext → IAccountingDbContext) |
| 15 | `docker-compose.yml` | 5 | Edit (add AccountingConnection) |
| 16 | `docker-compose.prod.yml` | 5 | Edit (add AccountingConnection) |
| 17 | `5_WebApps/ShopERP/appsettings.json` | 6 | Edit (add AccountingConnection) |
| 18 | `5_WebApps/ShopERP/appsettings.Development.json` | 6 | Edit (add AccountingConnection) |
| 19 | `6_Tests/VanAn.Architecture.Tests/ArchitectureRulesTests.cs` | 8 | Edit (add Rule J/K/L/M) |
| 20+ | `6_Tests/VanAn.Core.Tests/` + `6_Tests/VanAn.Integration.Tests/` | 9 | Edit (fix mocks) |
| 21 | `docs/AI/project_state.md` | 10 | Edit (update "ShopERP SQLite-only" → "ShopERP SQLite (Business) + PostgreSQL (Accounting)") |

**Total: ~21-25 files edited, 1 file created**

---

## 10. SUCCESS CRITERIA

- [ ] `dotnet build VanAn.sln` → 0 errors
- [ ] `scripts/guard-check.ps1` → PASS
- [ ] Architecture Tests Rule J/K/L/M → PASS
- [ ] Core Tests → All PASS (sau khi fix mocks)
- [ ] ShopERP startup → no `AccountingConnection missing` error
- [ ] ShopERP `/api/accounting/balance-sheet` → 200 OK (data from PostgreSQL)
- [ ] `docker logs vanan-shoperp` → no SQLite accounting error
- [ ] grep `IVanAnDbContext` trong 10 accounting service files → chỉ TenantConversion + HKDBookGeneration còn (dual)
- [ ] `ShopERPDbContext.cs` không có accounting DbSet declaration (chỉ throw stubs)
- [ ] `docker-compose.yml` ShopERP có `ConnectionStrings__AccountingConnection=Host=postgres...`
