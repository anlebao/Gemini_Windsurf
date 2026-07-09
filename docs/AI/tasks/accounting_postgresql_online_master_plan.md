# MASTER IMPLEMENTATION PLAN — Accounting Always-Online + PostgreSQL + Test Enforcement

> **Status:** PENDING — Awaiting Approval
> **Created:** 2026-07-09
> **Last Updated:** 2026-07-09 (v2 — Option B split interface, template-aligned, condensed)
> **Target Workflow:** `newfeaturebuild.md` (ANALYZE → IMPLEMENT)
> **Branch strategy:** `main` → feature branches per wave
> **Execution principle:** JIT Planning + Pure Execution
> **Prerequisite:** ADR-001 violation audit (git history 2026-06-03 → 2026-07-09)

---

## 0. EXECUTION RULES

### JIT Planning Strategy
**Nguyên tắc:** Investigate trước, Implement sau. KHÔNG code mò mẫm.

**Bước 1: INVESTIGATE** — Verify interface signatures, DbSet lists, service constructors
**Bước 2: IMPLEMENT** — Theo plan đã chốt, mỗi wave xong chạy `guard-check.ps1` + `dotnet build`

### Session protocol
1. Mỗi session chỉ làm 1 wave
2. Bắt đầu session: Đọc `project_state.md` + task card wave đang làm
3. Sau khi plan chốt: Execution Phase
4. Trước session end: Build + test
5. Sau mỗi wave: Commit `[WAVE X] Task description`

### Branch protocol
```
main
  └── feature/accounting-pg-wave1-interface-split
      └── feature/accounting-pg-wave2-services-di-config
          └── feature/accounting-pg-wave3-tests-verify
```

### Hard rules
- **Domain layer KHÔNG được sửa** — chỉ thay đổi Infrastructure/Services wiring
- **AccountingEntry immutable** — không thay đổi domain logic
- **Option B (APPROVED):** Split `IVanAnDbContext` → giữ `IVanAnDbContext` (business-only, 21 DbSets) + tạo `IAccountingDbContext` (6 accounting DbSets)
- **Compile-time safety > runtime throw** — KHÔNG dùng throw stubs (Option A rejected)
- **TDD:** Architecture Tests viết trong Wave 3 (sau khi implement xong) — verify green
- **Playwright DISABLED** cho đến khi build pass + implementation complete

### Critical regulatory context
- **ADR-001:** SQLite local + NATS sync + PostgreSQL cloud — accounting always online
- **ADR-003:** AccountingEntry immutable, Thông tư 200/2014/TT-BTC + TT 152/2025/TT-BTC compliance
- **Accounting data MUST be on PostgreSQL** — SQLite chỉ cho Business/Platform (offline-first)

---

## 1. CURRENT ISSUES SUMMARY

### Issue 1: Accounting module chạy trên SQLite (vi phạm ADR-001)
**Status:** ❌ VI PHẠM — Từ 2026-06-03 (commit `957ac95`)
**Priority:** 1 (Critical)

**Root cause:**
- ShopERP `Program.cs` hardcoded `UseSqlite()` — không có PostgreSQL path cho accounting
- `IVanAnDbContext` resolve thành `ShopERPDbContext` (SQLite) thay vì `VanAnDbContext` (PostgreSQL)
- 10 services + 3 repositories inject `IVanAnDbContext` cho accounting operations
- Không có Architecture Test nào chặn vi phạm

**Timeline vi phạm:**
| # | Commit | Sự kiện |
|---|--------|---------|
| 1 | `957ac95` (06-03) | PeriodClosingService đầu tiên chạy trên SQLite |
| 2 | `754e2b3` (06-25) | PR #55 cố thêm PostgreSQL → crash (hardcoded UseSqlite) |
| 3 | `cf05eb1` (06-25) | PR #56 revert, hiểu sai ADR-001 → cemented vi phạm |
| 4 | `a3c9242` (07-04) | AccountChartService lan rộng sang SQLite |
| 5 | `34ac67b` (07-05) | Gateway Option B song song — 2 bản sao không sync |

### Issue 2: Không có enforcement test
**Status:** ❌ MISSING
**Priority:** 2 (High)

Architecture Tests Rule H chỉ check docker-compose có `Host=postgres`, không check code path. Cần Rule J/K/L/M để enforce accounting-online ở code level.

---

## 2. WAVE 1 — Split Interface + DbContext Updates

**Branch:** `feature/accounting-pg-wave1-interface-split`
**Estimated sessions:** 1
**Conflict risk:** MEDIUM (IVanAnDbContext change affects 47 files)
**Priority:** 1
**Task Card:** `docs/AI/tasks/accounting_pg_wave1_interface_split_task_card.md`

### Tasks
| # | Task ID | Task | Files | Status |
|---|---------|------|-------|--------|
| 1 | W1-T1 | Create `IAccountingDbContext` interface (6 accounting DbSets) | `3_CoreHub/Infrastructure/IAccountingDbContext.cs` | PENDING |
| 2 | W1-T2 | Remove 6 accounting DbSets from `IVanAnDbContext` | `3_CoreHub/Infrastructure/IVanAnDbContext.cs` | PENDING |
| 3 | W1-T3 | `VanAnDbContext` implement both `IVanAnDbContext` + `IAccountingDbContext` | `3_CoreHub/Infrastructure/VanAnDbContext.cs` | PENDING |
| 4 | W1-T4 | `ShopERPDbContext` remove 6 accounting DbSet declarations | `5_WebApps/ShopERP/Infrastructure/ShopERPDbContext.cs` | PENDING |
| 5 | W1-T5 | Fix compile errors (services lost accounting DbSets via IVanAnDbContext) | Solution-wide | PENDING |
| 6 | W1-T6 | Verify build: 0 errors | Solution-wide | PENDING |

### Entry criteria
- [ ] Project builds successfully
- [ ] Git status clean
- [ ] `IVanAnDbContext` confirmed has 27 DbSets (line 18-66)
- [ ] `ShopERPDbContext` confirmed implements `IVanAnDbContext`

### Exit criteria
- [ ] `IAccountingDbContext` exists with 6 accounting DbSets
- [ ] `IVanAnDbContext` has 21 business DbSets (6 accounting removed)
- [ ] `VanAnDbContext` implements both interfaces
- [ ] `ShopERPDbContext` has 21 business DbSets only (no accounting)
- [ ] Build: 0 errors
- [ ] No throw stubs (Option B — compile-time clean)

### Why first
- Foundation cho Wave 2 (services cần `IAccountingDbContext` để inject)
- Build sẽ catch mọi miss (compile-time safety)
- Business services không cần đổi (IVanAnDbContext vẫn có business DbSets)

---

## 3. WAVE 2 — Update Services/Repos + DI + Config + Docker

**Branch:** `feature/accounting-pg-wave2-services-di-config`
**Estimated sessions:** 1-2
**Conflict risk:** MEDIUM
**Priority:** 2
**Task Card:** `docs/AI/tasks/accounting_pg_wave2_services_di_config_task_card.md`

### Tasks
| # | Task ID | Task | Files | Status |
|---|---------|------|-------|--------|
| 1 | W2-T1 | 3 repositories: `IVanAnDbContext` → `IAccountingDbContext` | `AccountingEntryRepository.cs`, `AuditLogRepository.cs`, `HKDBookRepository.cs` | PENDING |
| 2 | W2-T2 | 7 direct-inject services: `IVanAnDbContext` → `IAccountingDbContext` | `PeriodClosingService`, `BalanceSheetService`, `IncomeStatementService`, `CashFlowStatementService`, `TrialBalanceService`, `AccountChartService`, `SmartPreAggregationService` | PENDING |
| 3 | W2-T3 | 2 dual-inject services: add `IAccountingDbContext` + keep `IVanAnDbContext` | `TenantConversionService`, `HKDBookGenerationService` | PENDING |
| 4 | W2-T4 | `AccountChartSeeder`: change param `IVanAnDbContext` → `IAccountingDbContext` + update all callers | `AccountChartSeeder.cs` + callers | PENDING |
| 5 | W2-T5 | ShopERP `Program.cs`: register `VanAnDbContext` with `UseNpgsql` + `IAccountingDbContext` DI | `5_WebApps/ShopERP/Program.cs` | PENDING |
| 6 | W2-T6 | Add `AccountingConnection` to appsettings + docker-compose | `appsettings.json`, `appsettings.Development.json`, `docker-compose.yml`, `docker-compose.prod.yml` | PENDING |
| 7 | W2-T7 | Verify build: 0 errors | Solution-wide | PENDING |

### Entry criteria
- [ ] Wave 1 merged
- [ ] `IAccountingDbContext` exists
- [ ] `ShopERPDbContext` has no accounting DbSets

### Exit criteria
- [ ] 3 repos + 7 services inject `IAccountingDbContext`
- [ ] 2 dual-inject services have both `IAccountingDbContext` + `IVanAnDbContext`
- [ ] `VasFeatureFlagService` giữ `IVanAnDbContext` (chỉ cần Tenants — business)
- [ ] ShopERP `Program.cs` registers `VanAnDbContext` with `UseNpgsql`
- [ ] `AccountingConnection` in appsettings + docker-compose
- [ ] Build: 0 errors

### Why second
- Cần `IAccountingDbContext` từ Wave 1
- Services/repositories là consumers chính của interface mới

---

## 4. WAVE 3 — Architecture Tests + Existing Tests + Verify

**Branch:** `feature/accounting-pg-wave3-tests-verify`
**Estimated sessions:** 1-2
**Conflict risk:** LOW
**Priority:** 3
**Task Card:** `docs/AI/tasks/accounting_pg_wave3_tests_verify_task_card.md`

### Tasks
| # | Task ID | Task | Files | Status |
|---|---------|------|-------|--------|
| 1 | W3-T1 | Add Architecture Test Rule J: accounting services inject `IAccountingDbContext` | `ArchitectureRulesTests.cs` | PENDING |
| 2 | W3-T2 | Add Architecture Test Rule K: `ShopERPDbContext` has no accounting DbSets | `ArchitectureRulesTests.cs` | PENDING |
| 3 | W3-T3 | Add Architecture Test Rule L: docker-compose has `AccountingConnection` (PostgreSQL) | `ArchitectureRulesTests.cs` | PENDING |
| 4 | W3-T4 | Add Architecture Test Rule M: ShopERP `Program.cs` registers `IAccountingDbContext` with `UseNpgsql` | `ArchitectureRulesTests.cs` | PENDING |
| 5 | W3-T5 | Fix existing tests: mock `IAccountingDbContext` cho accounting tests | `6_Tests/VanAn.Core.Tests/`, `6_Tests/VanAn.Integration.Tests/` | PENDING |
| 6 | W3-T6 | Full verification: build + guard-check + all tests | Solution-wide | PENDING |
| 7 | W3-T7 | Update `project_state.md`: "ShopERP SQLite-only" → "ShopERP SQLite (Business) + PostgreSQL (Accounting)" | `docs/AI/project_state.md` | PENDING |

### Entry criteria
- [ ] Wave 2 merged
- [ ] All services/repos updated
- [ ] DI registration complete

### Exit criteria
- [ ] Rule J/K/L/M PASS
- [ ] All existing tests pass (after mock updates)
- [ ] `dotnet build VanAn.sln` → 0 errors
- [ ] `scripts/guard-check.ps1` → PASS
- [ ] `project_state.md` updated

### Why third
- Cần implementation xong trước khi test (green phase)
- Tests verify enforcement — không phải driver

---

## 5. CROSS-WAVE CONCERNS

### Domain Protection
- **KHÔNG sửa `1_Shared/Domain.cs`** — chỉ thay đổi Infrastructure/Services wiring
- `AccountingEntry` immutable trong tất cả modes
- Multi-tenancy TenantId filter vẫn enforce qua cả 2 contexts

### Interface Split (Option B)
- `IVanAnDbContext` → 21 business DbSets (giữ tên, giảm churn cho business services)
- `IAccountingDbContext` → 6 accounting DbSets (NEW)
- `VanAnDbContext` implements cả 2 (PostgreSQL — có đầy đủ 27 DbSets)
- `ShopERPDbContext` implements `IVanAnDbContext` only (SQLite — 21 business DbSets)
- **Compile-time safety:** Nếu ai query accounting qua `IVanAnDbContext` → compile error (không có DbSet)
- **No throw stubs:** Clean interface segregation, no ISP violation

### Services NOT Changed
- `VasFeatureFlagService` — giữ `IVanAnDbContext` (chỉ cần Tenants — business)
- `TenantOnboardingService` + 8 `IIndustrySeedStrategy` — giữ `IVanAnDbContext` (seed business data)
- Business services (OrderService, ShopService, InventoryService, etc.) — không đổi

### Dual Injection
- `TenantConversionService`: `IAccountingDbContext` (AccountingEntries) + `IVanAnDbContext` (Tenants)
- `HKDBookGenerationService`: `IAccountingDbContext` (JournalEntries) + `IVanAnDbContext` (Tenants)

### Data Migration
- **Out of scope:** Dev data, re-seed được. Production migration = task riêng nếu có data thật

---

## 6. APPROVAL CHECKLIST

- [ ] Master plan reviewed (v2 — Option B, 3 waves, template-aligned)
- [ ] 3 task cards reviewed (Wave 1-3)
- [ ] Option B confirmed (split interface, compile-time safety)
- [ ] `IVanAnDbContext` 27 DbSets confirmed (6 accounting + 21 business)
- [ ] 10 services + 3 repos identified for interface swap
- [ ] 2 dual-inject services identified (TenantConversion + HKDBookGeneration)
- [ ] `VasFeatureFlagService` confirmed business-only (giữ IVanAnDbContext)
- [ ] AccountChartSeeder callers cần update (signature change)
- [ ] Sẵn sàng implement Wave 1

---

## 7. EFFORT SUMMARY

| Wave | Description | Sessions | Bottleneck |
|---|---|---|---|
| Wave 1 | Split interface + DbContext updates | 1 | None (build catches all misses) |
| Wave 2 | Services/repos + DI + config + docker | 1-2 | None |
| Wave 3 | Architecture tests + existing tests + verify | 1-2 | Test mock update count |
| **Total** | | **3-5 sessions** | |

**Critical path:** Wave 1 → Wave 2 → Wave 3
