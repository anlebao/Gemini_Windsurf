# TASK CARD: Accounting PostgreSQL Online — Wave 1 — Split Interface + DbContext Updates

## 1. GOAL & CONTEXT
- **Mục tiêu cốt lõi:** Split `IVanAnDbContext` thành business-only (21 DbSets) + tạo `IAccountingDbContext` (6 accounting DbSets). `VanAnDbContext` implement cả 2, `ShopERPDbContext` implement chỉ business.
- **Nghiệp vụ áp dụng:** ADR-001 compliance — accounting always online trên PostgreSQL
- **Status:** PENDING — Planning & Approval
- **Branch:** `feature/accounting-pg-wave1-interface-split`
- **Estimated Sessions:** 1

---

## 2. ACTIVE WORKFLOW ROUTING
- **Target Workflow:** `newfeaturebuild.md` (ANALYZE → IMPLEMENT)
- **Execution Mode:** IMPLEMENT
- **Current Phase:** Wave 1 of 3
- **Dependency:** None (first wave)

---

## 3. RELEVANT FILES (CONTEXT BOUNDARY)

### Files được phép đọc/sửa
- `docs/AI/project_state.md` (Bắt buộc đọc đầu phiên)
- `docs/AI/tasks/accounting_postgresql_online_master_plan.md` (READ)
- `3_CoreHub/Infrastructure/IVanAnDbContext.cs` (MODIFY — remove 6 accounting DbSets)
- `3_CoreHub/Infrastructure/IAccountingDbContext.cs` (CREATE — new interface)
- `3_CoreHub/Infrastructure/VanAnDbContext.cs` (MODIFY — add IAccountingDbContext to declaration)
- `5_WebApps/ShopERP/Infrastructure/ShopERPDbContext.cs` (MODIFY — remove 6 accounting DbSets)
- `1_Shared/Domain.cs` (READ ONLY — confirm entity types)

### Boundary Rules
- KHÔNG sửa `1_Shared/Domain.cs`
- KHÔNG tạo throw stubs (Option A rejected — Option B only)
- KHÔNG update services/repos trong wave này (Wave 2)
- KHÔNG update DI registration trong wave này (Wave 2)

---

## 4. TECHNICAL CONSTRAINTS
- [ ] **Domain Protection:** KHÔNG sửa Domain.cs
- [ ] **Compile-time safety:** No throw stubs — interface segregation clean
- [ ] **Backward Compat:** Business services vẫn build (IVanAnDbContext giữ 21 business DbSets)
- [ ] **No runtime guards:** Compile errors là enforcement đầu tiên

---

## 5. SUCCESS CRITERIA
- [ ] **SC1:** `IAccountingDbContext` exists with 6 accounting DbSets
- [ ] **SC2:** `IVanAnDbContext` has 21 business DbSets (6 accounting removed)
- [ ] **SC3:** `VanAnDbContext` implements both `IVanAnDbContext` + `IAccountingDbContext`
- [ ] **SC4:** `ShopERPDbContext` has 21 business DbSets only (no accounting DbSet declarations)
- [ ] **SC5:** Build: 0 errors
- [ ] **SC6:** No throw stubs in ShopERPDbContext

---

## 6. DETAILED IMPLEMENTATION

### 6.1. Create IAccountingDbContext (W1-T1)

**File:** `3_CoreHub/Infrastructure/IAccountingDbContext.cs` (NEW)

```csharp
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using VanAn.Shared.Domain;
using VanAn.Shared.Domain.Audit;
using VanAn.CoreHub.Infrastructure.Entities;

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
        DbSet<AccountChartEntity> AccountCharts { get; }
        DbSet<PeriodClosingStatusEntity> PeriodClosingStatuses { get; }

        Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
        Task<IDbContextTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default);
    }
}
```

### 6.2. Remove 6 accounting DbSets from IVanAnDbContext (W1-T2)

**File:** `3_CoreHub/Infrastructure/IVanAnDbContext.cs`

**Remove these 6 lines:**
```csharp
DbSet<AccountingEntry> AccountingEntries { get; }      // → IAccountingDbContext
DbSet<JournalEntry> JournalEntries { get; }             // → IAccountingDbContext
DbSet<AuditLog> AuditLogs { get; }                      // → IAccountingDbContext
DbSet<PendingInvoiceQueue> PendingInvoiceQueues { get; } // → IAccountingDbContext
DbSet<AccountChartEntity> AccountCharts { get; }        // → IAccountingDbContext
DbSet<PeriodClosingStatusEntity> PeriodClosingStatuses { get; } // → IAccountingDbContext
```

**Keep these 21 business DbSets:**
Orders, OrderItems, Customers, Products, Inventories, Ingredients, Recipes, Shops, LoyaltyRewards, SocialCampaigns, OutboxMessages, Users, Tenants, UserTenants, PermissionGroups, UserPermissionGroups, ApiKeys, PushSubscriptions, PlatformUsers

### 6.3. VanAnDbContext implement both (W1-T3)

**File:** `3_CoreHub/Infrastructure/VanAnDbContext.cs`

```csharp
// Change class declaration:
public class VanAnDbContext(DbContextOptions<VanAnDbContext> options) 
    : DbContext(options), IVanAnDbContext, IAccountingDbContext
```

VanAnDbContext đã có đầy đủ 27 DbSets — chỉ thêm `IAccountingDbContext` vào interface list. Không cần thêm property nào.

### 6.4. ShopERPDbContext remove accounting DbSets (W1-T4)

**File:** `5_WebApps/ShopERP/Infrastructure/ShopERPDbContext.cs`

**Remove these 6 DbSet declarations (lines 44, 48, 49, 50, 72, 75):**
```csharp
public DbSet<AccountingEntry> AccountingEntries { get; set; }        // line 44 — REMOVE
public DbSet<JournalEntry> JournalEntries { get; set; }              // line 48 — REMOVE
public DbSet<AuditLog> AuditLogs { get; set; }                       // line 49 — REMOVE
public DbSet<PendingInvoiceQueue> PendingInvoiceQueues { get; set; } // line 50 — REMOVE
public DbSet<AccountChartEntity> AccountCharts { get; set; }         // line 72 — REMOVE
public DbSet<PeriodClosingStatusEntity> PeriodClosingStatuses { get; set; } // line 75 — REMOVE
```

**Also remove:** `public DbSet<HKDBook> HKDBooks { get; set; }` (line 47) — already ignored in OnModelCreating, DbSet declaration dư.

**Keep:** All 21 business DbSet declarations.

### 6.5. Fix compile errors (W1-T5)

Sau khi remove 6 accounting DbSets từ `IVanAnDbContext`, các services/repos nào query `_context.AccountingEntries`, `_context.JournalEntries`, etc. sẽ **compile error**.

**Expected compile errors (fix in Wave 2, NOT Wave 1):**
- 10 services: `PeriodClosingService`, `BalanceSheetService`, `IncomeStatementService`, `CashFlowStatementService`, `TrialBalanceService`, `AccountChartService`, `SmartPreAggregationService`, `TenantConversionService`, `HKDBookGenerationService`, `VasFeatureFlagService`
- 3 repos: `AccountingEntryRepository`, `AuditLogRepository`, `HKDBookRepository`
- `AccountChartSeeder`

**Wave 1 strategy:** Fix compile errors bằng cách thêm `IAccountingDbContext` injection vào các services/repos này (temporary — full swap in Wave 2). Hoặc:

**Alternative:** Wave 1 chỉ tạo `IAccountingDbContext` + implement trên `VanAnDbContext`, CHƯA remove từ `IVanAnDbContext`. Remove khỏi `IVanAnDbContext` ở đầu Wave 2 (cùng lúc swap services).

> **DECISION POINT:** Nếu compile errors quá nhiều (>20), tách W1-T2 sang đầu Wave 2. Wave 1 chỉ tạo interface mới + implement. Wave 2 bắt đầu bằng remove + swap đồng thời.

---

## 7. AI HEALTH CHECK MATRIX (INITIAL)
- **Evidence Count:** 5
- **Verified Facts:**
  - Fact 1: `IVanAnDbContext` has 27 DbSets (line 18-66) — 6 accounting + 21 business
  - Fact 2: `ShopERPDbContext` implements `IVanAnDbContext` (line 23) with 27 DbSet declarations
  - Fact 3: `VanAnDbContext` implements `IVanAnDbContext` (line 20) with 27 DbSet declarations
  - Fact 4: `HKDBooks` DbSet in ShopERPDbContext (line 47) is already ignored in OnModelCreating (line 152)
  - Fact 5: 47 files reference `IVanAnDbContext` in 3_CoreHub
- **Assumptions:**
  - Removing 6 DbSets from IVanAnDbContext sẽ cause ~13-15 compile errors (services + repos + seeder)
  - Business services (OrderService, ShopService, etc.) không bị affect vì không query accounting DbSets
- **Open Questions:**
  - Q1: Nên remove 6 DbSets trong Wave 1 hay Wave 2? (Recommend: Wave 2 đầu — giảm compile error window)

---

## 8. REVERSE IMPACT ANALYSIS
| File thay đổi | Reverse impact | Mitigation |
|---|---|---|
| `IVanAnDbContext.cs` (remove 6 DbSets) | Compile error cho 13 services/repos + seeder | Fix trong Wave 2 (swap sang IAccountingDbContext) |
| `IAccountingDbContext.cs` (new) | No impact — new file | None |
| `VanAnDbContext.cs` (add interface) | No impact — đã có đầy đủ DbSets | None |
| `ShopERPDbContext.cs` (remove 6 DbSets) | Compile error nếu có code query accounting qua ShopERPDbContext | Build catch — fix trong Wave 2 |

---

## 9. TDD & TESTING STRATEGY
- **Unit tests:** Không trong wave này
- **Integration tests:** Không trong wave này
- **Architecture tests:** Không trong wave này (Wave 3)
- **Verification:** `dotnet build VanAn.sln` → 0 errors (sau khi fix compile errors)
