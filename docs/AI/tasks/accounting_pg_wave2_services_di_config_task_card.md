# TASK CARD: Accounting PostgreSQL Online — Wave 2 — Services/Repos + DI + Config

## 1. GOAL & CONTEXT
- **Mục tiêu cốt lõi:** Swap 13 services/repos sang `IAccountingDbContext`, register PostgreSQL DI trong ShopERP, thêm `AccountingConnection` config
- **Nghiệp vụ áp dụng:** ADR-001 compliance — accounting always online trên PostgreSQL
- **Status:** PENDING — Planning & Approval
- **Branch:** `feature/accounting-pg-wave2-services-di-config`
- **Estimated Sessions:** 1-2

---

## 2. ACTIVE WORKFLOW ROUTING
- **Target Workflow:** `newfeaturebuild.md` (ANALYZE → IMPLEMENT)
- **Execution Mode:** IMPLEMENT
- **Current Phase:** Wave 2 of 3
- **Dependency:** Wave 1 merged (IAccountingDbContext exists)

---

## 3. RELEVANT FILES (CONTEXT BOUNDARY)

### Files được phép đọc/sửa
- `docs/AI/project_state.md` (Bắt buộc đọc đầu phiên)
- `docs/AI/tasks/accounting_postgresql_online_master_plan.md` (READ)
- `docs/AI/tasks/accounting_pg_wave1_interface_split_task_card.md` (READ)
- `3_CoreHub/Repositories/AccountingEntryRepository.cs` (MODIFY)
- `3_CoreHub/Repositories/AuditLogRepository.cs` (MODIFY)
- `3_CoreHub/Repositories/HKDBookRepository.cs` (MODIFY)
- `3_CoreHub/Services/PeriodClosingService.cs` (MODIFY)
- `3_CoreHub/Services/BalanceSheetService.cs` (MODIFY)
- `3_CoreHub/Services/IncomeStatementService.cs` (MODIFY)
- `3_CoreHub/Services/CashFlowStatementService.cs` (MODIFY)
- `3_CoreHub/Services/TrialBalanceService.cs` (MODIFY)
- `3_CoreHub/Services/AccountChartService.cs` (MODIFY)
- `3_CoreHub/Services/PreAggregation/SmartPreAggregationService.cs` (MODIFY)
- `3_CoreHub/Services/TenantConversionService.cs` (MODIFY — dual inject)
- `3_CoreHub/Services/Template/HKDBookGenerationService.cs` (MODIFY — dual inject)
- `3_CoreHub/Infrastructure/Seed/AccountChartSeeder.cs` (MODIFY — signature change)
- `5_WebApps/ShopERP/Program.cs` (MODIFY — DI registration + seeder call)
- `5_WebApps/ShopERP/appsettings.json` (MODIFY — add AccountingConnection)
- `5_WebApps/ShopERP/appsettings.Development.json` (MODIFY — add AccountingConnection)
- `docker-compose.yml` (MODIFY — add AccountingConnection env)
- `docker-compose.prod.yml` (MODIFY — add AccountingConnection env)
- `1_Shared/Domain.cs` (READ ONLY)

### Boundary Rules
- KHÔNG sửa `1_Shared/Domain.cs`
- KHÔNG sửa `VasFeatureFlagService` (giữ `IVanAnDbContext` — chỉ cần Tenants)
- KHÔNG sửa business services (OrderService, ShopService, InventoryService, etc.)
- KHÔNG sửa `TenantOnboardingService` + 8 `IIndustrySeedStrategy` (seed business data)
- KHÔNG tạo Architecture Tests trong wave này (Wave 3)

---

## 4. TECHNICAL CONSTRAINTS
- [ ] **Domain Protection:** KHÔNG sửa Domain.cs
- [ ] **Compile-time safety:** Services inject `IAccountingDbContext` — build catch miss
- [ ] **Dual injection:** TenantConversionService + HKDBookGenerationService cần cả 2 interfaces
- [ ] **DI Registration:** ShopERP phải throw nếu thiếu `AccountingConnection`
- [ ] **Docker Compose:** Cả `docker-compose.yml` + `docker-compose.prod.yml` phải có `AccountingConnection`

---

## 5. SUCCESS CRITERIA
- [ ] **SC1:** 3 repositories inject `IAccountingDbContext` (AccountingEntryRepository, AuditLogRepository, HKDBookRepository)
- [ ] **SC2:** 7 direct-inject services inject `IAccountingDbContext`
- [ ] **SC3:** 2 dual-inject services inject cả `IAccountingDbContext` + `IVanAnDbContext`
- [ ] **SC4:** `VasFeatureFlagService` giữ `IVanAnDbContext` (không đổi)
- [ ] **SC5:** `AccountChartSeeder` signature đổi sang `IAccountingDbContext` + tất cả callers updated
- [ ] **SC6:** ShopERP `Program.cs` registers `VanAnDbContext` with `UseNpgsql` + `IAccountingDbContext` DI
- [ ] **SC7:** `AccountingConnection` trong appsettings + docker-compose
- [ ] **SC8:** Build: 0 errors

---

## 6. DETAILED IMPLEMENTATION

### 6.1. Repositories — swap IVanAnDbContext → IAccountingDbContext (W2-T1)

**3 files — same pattern:**

**`3_CoreHub/Repositories/AccountingEntryRepository.cs`:**
```csharp
// Before:
public class AccountingEntryRepository(IVanAnDbContext context, ...) : IAccountingEntryRepository
// After:
public class AccountingEntryRepository(IAccountingDbContext context, ...) : IAccountingEntryRepository
```

**`3_CoreHub/Repositories/AuditLogRepository.cs`:**
```csharp
// Before:
public class AuditLogRepository(IVanAnDbContext context, ...) : IAuditLogRepository
// After:
public class AuditLogRepository(IAccountingDbContext context, ...) : IAuditLogRepository
```

**`3_CoreHub/Repositories/HKDBookRepository.cs`:**
```csharp
// Before:
public class HKDBookRepository(IVanAnDbContext context, ...) : IHKDBookRepository
// After:
public class HKDBookRepository(IAccountingDbContext context, ...) : IHKDBookRepository
```

**Note:** Services inject repos (AccountingEntryService, ReversalService, AuditTrailService, HKDBookService) — KHÔNG cần đổi vì repo interface không đổi, chỉ repo implementation đổi DbContext.

### 6.2. 7 Direct-Inject Services — swap IVanAnDbContext → IAccountingDbContext (W2-T2)

**7 files — same pattern:**

| Service | File | DbSet used |
|---------|------|------------|
| PeriodClosingService | `3_CoreHub/Services/PeriodClosingService.cs` | PeriodClosingStatuses |
| BalanceSheetService | `3_CoreHub/Services/BalanceSheetService.cs` | JournalEntries |
| IncomeStatementService | `3_CoreHub/Services/IncomeStatementService.cs` | JournalEntries |
| CashFlowStatementService | `3_CoreHub/Services/CashFlowStatementService.cs` | JournalEntries |
| TrialBalanceService | `3_CoreHub/Services/TrialBalanceService.cs` | JournalEntries |
| AccountChartService | `3_CoreHub/Services/AccountChartService.cs` | AccountCharts |
| SmartPreAggregationService | `3_CoreHub/Services/PreAggregation/SmartPreAggregationService.cs` | JournalEntries |

**Pattern:**
```csharp
// Before:
public class BalanceSheetService(IVanAnDbContext context, ...) : IBalanceSheetService
// After:
public class BalanceSheetService(IAccountingDbContext context, ...) : IBalanceSheetService
```

### 6.3. 2 Dual-Inject Services (W2-T3)

**`3_CoreHub/Services/TenantConversionService.cs`:**
```csharp
// Before:
public class TenantConversionService(IVanAnDbContext context, ...) : ITenantConversionService
// After:
public class TenantConversionService(
    IAccountingDbContext accountingContext,  // AccountingEntries
    IVanAnDbContext businessContext,          // Tenants
    ...) : ITenantConversionService
```

**`3_CoreHub/Services/Template/HKDBookGenerationService.cs`:**
```csharp
// Before:
public class HKDBookGenerationService(IVanAnDbContext context, ...) : IHKDBookGenerationService
// After:
public class HKDBookGenerationService(
    IAccountingDbContext accountingContext,  // JournalEntries
    IVanAnDbContext businessContext,          // Tenants
    ...) : IHKDBookGenerationService
```

**Note:** Rename `_context` → `_accountingContext` + `_businessContext` trong body. Update tất cả references.

### 6.4. AccountChartSeeder — signature change + callers (W2-T4)

**`3_CoreHub/Infrastructure/Seed/AccountChartSeeder.cs`:**
```csharp
// Before:
public static async Task<int> SeedAsync(IVanAnDbContext dbContext, ILogger? logger = null, CancellationToken ct = default)
public static async Task CleanupAsync(IVanAnDbContext db, CancellationToken ct = default)
// After:
public static async Task<int> SeedAsync(IAccountingDbContext dbContext, ILogger? logger = null, CancellationToken ct = default)
public static async Task CleanupAsync(IAccountingDbContext db, CancellationToken ct = default)
```

**Callers cần update (grep `AccountChartSeeder.SeedAsync` + `AccountChartSeeder.CleanupAsync`):**
- `5_WebApps/ShopERP/Program.cs` — seeder section (~line 490)
- `2_Gateway/Program.cs` — nếu có (Gateway đã dùng VanAnDbContext = PostgreSQL, có thể giữ IVanAnDbContext nếu Gateway vẫn đăng ký cả 2)
- Test files — nếu có test gọi seeder

### 6.5. ShopERP Program.cs — DI Registration (W2-T5)

**`5_WebApps/ShopERP/Program.cs`:**

```csharp
// === Business + Platform (SQLite — offline-first) ===
string sqliteConnectionString = Environment.GetEnvironmentVariable("SQLITE_DB_PATH")
    ?? builder.Configuration.GetConnectionString("DefaultConnection")
    ?? $"Data Source={Path.Combine(AppContext.BaseDirectory, "vanan_shoperp.db")}";
_ = builder.Services.AddDbContext<ShopERPDbContext>(options =>
    options.UseSqlite(sqliteConnectionString));
_ = builder.Services.AddScoped<IVanAnDbContext>(provider =>
    provider.GetRequiredService<ShopERPDbContext>());

// === Accounting (PostgreSQL — always online, ADR-001) ===
string accountingConnectionString = builder.Configuration.GetConnectionString("AccountingConnection")
    ?? throw new InvalidOperationException(
        "ConnectionStrings:AccountingConnection is required — accounting must be online (ADR-001). " +
        "Set ConnectionStrings__AccountingConnection=Host=postgres;Port=5432;...");
_ = builder.Services.AddDbContext<VanAn.CoreHub.Infrastructure.VanAnDbContext>(options =>
    options.UseNpgsql(accountingConnectionString));
_ = builder.Services.AddScoped<VanAn.CoreHub.Infrastructure.IAccountingDbContext>(provider =>
    provider.GetRequiredService<VanAn.CoreHub.Infrastructure.VanAnDbContext>());
```

**Replace existing line 94-96:**
```csharp
// REMOVE:
_ = builder.Services.AddScoped<IVanAnDbContext>(provider => provider.GetRequiredService<ShopERPDbContext>());
// (replaced by dual registration above)
```

**Seeder section (~line 490):**
```csharp
// Before:
using var vanAnContext = app.Services.GetRequiredService<IVanAnDbContext>();
await AccountChartSeeder.CleanupAsync(vanAnContext, ct);
await AccountChartSeeder.SeedAsync(vanAnContext, logger, ct);
// After:
using var accountingContext = app.Services.GetRequiredService<IAccountingDbContext>();
await AccountChartSeeder.CleanupAsync(accountingContext, ct);
await AccountChartSeeder.SeedAsync(accountingContext, logger, ct);
```

### 6.6. Config + Docker (W2-T6)

**`5_WebApps/ShopERP/appsettings.json`:**
```json
"ConnectionStrings": {
  "DefaultConnection": "Data Source=vanan_shoperp.db",
  "AccountingConnection": "Host=localhost;Port=5432;Database=VanAnCoreHub;Username=vanan_admin;Password=vanan_password"
}
```

**`5_WebApps/ShopERP/appsettings.Development.json`:** Same as above.

**`docker-compose.yml` — shoperp service environment:**
```yaml
environment:
  - ConnectionStrings__DefaultConnection=Data Source=/data/shoperp.db
  - ConnectionStrings__AccountingConnection=Host=postgres;Port=5432;Database=${POSTGRES_DB:-VanAnCoreHub};Username=${POSTGRES_USER:-vanan_admin};Password=${POSTGRES_PASSWORD}
```

**`docker-compose.prod.yml` — shoperp service environment:** Same pattern.

---

## 7. AI HEALTH CHECK MATRIX (INITIAL)
- **Evidence Count:** 6
- **Verified Facts:**
  - Fact 1: 3 repos inject `IVanAnDbContext` (AccountingEntryRepository line 13, AuditLogRepository line 17, HKDBookRepository line 12)
  - Fact 2: 7 services inject `IVanAnDbContext` directly (PeriodClosingService line 22, BalanceSheetService line 16, IncomeStatementService line 16, CashFlowStatementService line 23, TrialBalanceService line 16, AccountChartService line 22, SmartPreAggregationService line 16)
  - Fact 3: TenantConversionService injects `IVanAnDbContext` (line 16) — uses Tenants + AccountingEntries
  - Fact 4: HKDBookGenerationService injects `IVanAnDbContext` (line 15) — uses JournalEntries + Tenants
  - Fact 5: VasFeatureFlagService injects `IVanAnDbContext` (line 33) — uses only Tenants (business)
  - Fact 6: AccountChartSeeder.SeedAsync + CleanupAsync take `IVanAnDbContext` param (line 28, 44)
- **Assumptions:**
  - AccountChartSeeder callers: ShopERP Program.cs + possibly Gateway Program.cs + tests
  - ShopERP Program.cs line 94-96 is the IVanAnDbContext registration to replace
- **Open Questions:**
  - Q1: Gateway Program.cs có gọi AccountChartSeeder không? (Cần grep verify)
  - Q2: Có test nào gọi AccountChartSeeder.SeedAsync không? (Cần grep verify)

---

## 8. REVERSE IMPACT ANALYSIS
| File thay đổi | Reverse impact | Mitigation |
|---|---|---|
| 3 repos (swap interface) | Services inject repos — no break (repo interface unchanged) | None |
| 7 services (swap interface) | DI container cần register IAccountingDbContext | W2-T5 registers it |
| 2 dual-inject services | Constructor signature change — DI cần cả 2 | W2-T5 registers both |
| AccountChartSeeder | All callers break (signature change) | W2-T4 updates all callers |
| ShopERP Program.cs | Startup behavior change | Test startup locally |
| appsettings + docker-compose | Config change — không break code | Verify config valid |

---

## 9. TDD & TESTING STRATEGY
- **Unit tests:** Không trong wave này (Wave 3 fix mocks)
- **Integration tests:** Không trong wave này
- **Architecture tests:** Không trong wave này (Wave 3)
- **Verification:** `dotnet build VanAn.sln` → 0 errors
- **Note:** Existing tests có thể fail (mock IVanAnDbContext cho accounting) — fix trong Wave 3
