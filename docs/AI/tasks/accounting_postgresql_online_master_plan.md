# MASTER IMPLEMENTATION PLAN — Accounting Always-Online + PostgreSQL + Test Enforcement

> **Status:** WAVE 1 COMPLETE ✅ — WAVE 2 COMPLETE ✅ — WAVE 3 COMPLETE ✅ — ALL 3 WAVES DONE
> **Created:** 2026-07-09
> **Last Updated:** 2026-07-10 (v5 — Wave 3 complete, all tests passing, ready for merge)
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

## 2. WAVE 1 — Split Interface + DbContext Updates + Service Swap ✅ COMPLETE

**Branch:** `feature/accounting-pg-wave1-interface-split`
**Commit:** `9d589bd`
**Completed:** 2026-07-09
**Conflict risk:** MEDIUM (IVanAnDbContext change affects 47 files)
**Priority:** 1
**Task Card:** `docs/AI/tasks/accounting_pg_wave1_interface_split_task_card.md`

> **NOTE:** User approved "Full Wave 1 as written" — merging Wave 2 service-swap (W2-T1 through W2-T5) into Wave 1. INVESTIGATE found ~98 compile-error sites (task card §6.5 threshold >20 met). All service/repo swaps + DI registration + appsettings config done in Wave 1.

### Tasks
| # | Task ID | Task | Files | Status |
|---|---------|------|-------|--------|
| 1 | W1-T1 | Create `IAccountingDbContext` interface (6 accounting DbSets) | `3_CoreHub/Infrastructure/IAccountingDbContext.cs` | ✅ DONE |
| 2 | W1-T2 | Remove 6 accounting DbSets from `IVanAnDbContext` | `3_CoreHub/Infrastructure/IVanAnDbContext.cs` | ✅ DONE |
| 3 | W1-T3 | `VanAnDbContext` implement both `IVanAnDbContext` + `IAccountingDbContext` | `3_CoreHub/Infrastructure/VanAnDbContext.cs` | ✅ DONE |
| 4 | W1-T4 | `ShopERPDbContext` remove 6 accounting DbSet declarations + HKDBooks | `5_WebApps/ShopERP/Infrastructure/ShopERPDbContext.cs` | ✅ DONE |
| 5 | W1-T5 | Fix compile errors — SWAP 11 files + DUAL-INJECT 3 files | Solution-wide (14 CoreHub files + 3 test files) | ✅ DONE |
| 6 | W1-T6 | Verify build: 0 errors | Solution-wide | ✅ DONE |

### Exit criteria — ALL MET
- [x] `IAccountingDbContext` exists with 6 accounting DbSets
- [x] `IVanAnDbContext` has 19 business DbSets (6 accounting removed) — **plan said 21, actual 19**
- [x] `VanAnDbContext` implements both interfaces
- [x] `ShopERPDbContext` has 19 business DbSets only (no accounting + no HKDBooks)
- [x] Build: 0 errors
- [x] No throw stubs (Option B — compile-time clean)

### Plan discrepancies found during implementation
1. **DbSet count:** `IVanAnDbContext` had **25 DbSets** (not 27): 6 accounting + **19 business** (not 21). Plan doc error.
2. **SmartPreAggregationService:** Plan listed as "direct-inject" (W2-T2) → actually **dual-inject** (uses `_context.Tenants` line 297 + `_context.AccountingEntries` line 249).
3. **DataProviderService:** Not in original plan list → added as SWAP (accounting-only consumer, 5 `_context.AccountingEntries` accesses).
4. **HKDBooks DbSet:** Removed from ShopERPDbContext (abstract base, ignored in OnModelCreating — never persisted).

### Why first
- Foundation cho Wave 2 (services cần `IAccountingDbContext` để inject)
- Build sẽ catch mọi miss (compile-time safety)
- Business services không cần đổi (IVanAnDbContext vẫn có business DbSets)

---

## 3. WAVE 2 — Update Services/Repos + DI + Config + Docker ✅ COMPLETE

**Branch:** `feature/accounting-pg-wave1-interface-split` (done in Wave 1 + Wave 2 residual)
**Conflict risk:** MEDIUM
**Priority:** 2
**Task Card:** `docs/AI/tasks/accounting_pg_wave2_services_di_config_task_card.md`

> **NOTE:** W2-T1 through W2-T5 + W2-T6 (appsettings) completed in Wave 1. W2-T6 (docker-compose) + W2-T7 (verify) completed in Wave 2 residual session (2026-07-10).

### Tasks
| # | Task ID | Task | Files | Status |
|---|---------|------|-------|--------|
| 1 | W2-T1 | 3 repositories: `IVanAnDbContext` → `IAccountingDbContext` | `AccountingEntryRepository.cs`, `AuditLogRepository.cs`, `HKDBookRepository.cs` | ✅ DONE (Wave 1) |
| 2 | W2-T2 | 7 direct-inject services: `IVanAnDbContext` → `IAccountingDbContext` | `PeriodClosingService`, `BalanceSheetService`, `IncomeStatementService`, `CashFlowStatementService`, `TrialBalanceService`, `AccountChartService`, `DataProviderService` | ✅ DONE (Wave 1) |
| 3 | W2-T3 | 2 dual-inject services: add `IAccountingDbContext` + keep `IVanAnDbContext` | `TenantConversionService`, `HKDBookGenerationService` | ✅ DONE (Wave 1) |
| 4 | W2-T4 | `AccountChartSeeder`: change param `IVanAnDbContext` → `IAccountingDbContext` + update all callers | `AccountChartSeeder.cs` + callers | ✅ DONE (Wave 1) |
| 5 | W2-T5 | ShopERP `Program.cs`: register `VanAnDbContext` with `UseNpgsql` + `IAccountingDbContext` DI | `5_WebApps/ShopERP/Program.cs` | ✅ DONE (Wave 1) |
| 6 | W2-T6 | Add `AccountingConnection` to appsettings + docker-compose | `appsettings.json`, `appsettings.Development.json`, `appsettings.Production.json`, `docker-compose.yml`, `docker-compose.prod.yml`, `docker-compose.edge.yml`, `.env.example` | ✅ DONE |
| 7 | W2-T7 | Verify build: 0 errors | Solution-wide | ✅ DONE (Wave 1 build) |

### Exit criteria — ALL MET
- [x] 3 repos + 7 services inject `IAccountingDbContext` (11 SWAP files total — DataProviderService added)
- [x] 3 dual-inject services have both `IAccountingDbContext` + `IVanAnDbContext` (SmartPreAggregationService recategorized)
- [x] `VasFeatureFlagService` giữ `IVanAnDbContext` (chỉ cần Tenants — business)
- [x] ShopERP `Program.cs` registers `VanAnDbContext` with `UseNpgsql`
- [x] `AccountingConnection` in appsettings (base/dev/prod)
- [x] `AccountingConnection` in docker-compose.yml + docker-compose.prod.yml + docker-compose.edge.yml
- [x] Build: 0 errors

### Why second
- Cần `IAccountingDbContext` từ Wave 1
- Services/repositories là consumers chính của interface mới

---

## 4. WAVE 3 — Architecture Tests + Existing Tests + Verify ✅ COMPLETE

**Branch:** `feature/accounting-pg-wave1-interface-split` (done on same branch as Wave 1+2)
**Completed:** 2026-07-10
**Conflict risk:** LOW
**Priority:** 3
**Task Card:** `docs/AI/tasks/accounting_pg_wave3_tests_verify_task_card.md`

### Tasks
| # | Task ID | Task | Files | Status |
|---|---------|------|-------|--------|
| 1 | W3-T1 | Add Architecture Test Rule J: accounting services inject `IAccountingDbContext` | `ArchitectureRulesTests.cs` | ✅ DONE |
| 2 | W3-T2 | Add Architecture Test Rule K: `ShopERPDbContext` has no accounting DbSets | `ArchitectureRulesTests.cs` | ✅ DONE |
| 3 | W3-T3 | Add Architecture Test Rule L: docker-compose has `AccountingConnection` (PostgreSQL) | `ArchitectureRulesTests.cs` | ✅ DONE |
| 4 | W3-T4 | Add Architecture Test Rule M: ShopERP `Program.cs` registers `IAccountingDbContext` with `UseNpgsql` | `ArchitectureRulesTests.cs` | ✅ DONE |
| 5 | W3-T5 | Fix existing tests: Rule C (ShopERP exempt from Npgsql check), W5-ARCH-003 (MetadataReader), 6 integration test factories (IAccountingDbContext DI registration) | `ArchitectureRulesTests.cs`, `DevLoginControllerReleaseBuildGuardTests.cs`, `CustomWebApplicationFactory.cs`, `AuthRealWebApplicationFactory.cs`, `GatewayWebApplicationFactory.cs`, `IntegrationTestBase.cs`, `EInvoiceDISmokeTests.cs`, `TestDatabaseFixture.cs` | ✅ DONE |
| 6 | W3-T6 | Full verification: build + guard-check + all tests | Solution-wide | ✅ DONE |
| 7 | W3-T7 | Update `project_state.md` | `docs/AI/project_state.md` | ✅ DONE |

### Entry criteria — ALL MET
- [x] Wave 2 complete
- [x] All services/repos updated
- [x] DI registration complete

### Exit criteria — ALL MET
- [x] Rule J/K/L/M PASS (38/38 Architecture Tests)
- [x] All existing tests pass (984 Core + 201 Integration = 1185 + 38 Arch = 1223 total)
- [x] `dotnet build VanAn.sln` → 0 errors
- [x] `guard-check.ps1` → ALL CHECKS PASSED
- [x] `project_state.md` updated

### Why third
- Cần implementation xong trước khi test (green phase)
- Tests verify enforcement — không phải driver

---

## 5. CROSS-WAVE CONCERNS

### Domain Protection
- **KHÔNG sửa `1_Shared/Domain.cs`** — chỉ thay đổi Infrastructure/Services wiring
- `AccountingEntry` immutable trong tất cả modes
- Multi-tenancy TenantId filter vẫn enforce qua cả 2 contexts

### Interface Split (Option B) — IMPLEMENTED
- `IVanAnDbContext` → 19 business DbSets (giữ tên, giảm churn cho business services)
- `IAccountingDbContext` → 6 accounting DbSets (NEW — created Wave 1)
- `VanAnDbContext` implements cả 2 (PostgreSQL — có đầy đủ 25+ DbSets)
- `ShopERPDbContext` implements `IVanAnDbContext` only (SQLite — 19 business DbSets, no accounting + no HKDBooks)
- **Compile-time safety:** Nếu ai query accounting qua `IVanAnDbContext` → compile error (không có DbSet)
- **No throw stubs:** Clean interface segregation, no ISP violation

### Services NOT Changed
- `VasFeatureFlagService` — giữ `IVanAnDbContext` (chỉ cần Tenants — business)
- `TenantOnboardingService` + 8 `IIndustrySeedStrategy` — giữ `IVanAnDbContext` (seed business data)
- Business services (OrderService, ShopService, InventoryService, etc.) — không đổi

### Dual Injection — 3 services (recategorized during Wave 1)
- `TenantConversionService`: `IVanAnDbContext` (Tenants) + `IAccountingDbContext` (AccountingEntries)
- `SmartPreAggregationService`: `IVanAnDbContext` (Tenants) + `IAccountingDbContext` (AccountingEntries) — **plan originally listed as direct-inject, recategorized to dual-inject**
- `HKDBookGenerationService`: `IVanAnDbContext` (Tenants) + `IAccountingDbContext` (JournalEntries)

### Data Migration
- **Out of scope:** Dev data, re-seed được. Production migration = task riêng nếu có data thật

---

## 6. APPROVAL CHECKLIST — ALL APPROVED + EXECUTED

- [x] Master plan reviewed (v2 — Option B, 3 waves, template-aligned)
- [x] 3 task cards reviewed (Wave 1-3)
- [x] Option B confirmed (split interface, compile-time safety)
- [x] `IVanAnDbContext` 25 DbSets confirmed (6 accounting + 19 business — plan said 27/21, actual 25/19)
- [x] 10 services + 3 repos identified for interface swap — **actual: 11 SWAP + 3 DUAL-INJECT** (DataProviderService added, SmartPreAggregationService recategorized)
- [x] 3 dual-inject services identified (TenantConversion + SmartPreAggregation + HKDBookGeneration)
- [x] `VasFeatureFlagService` confirmed business-only (giữ IVanAnDbContext)
- [x] AccountChartSeeder callers updated (signature change)
- [x] Wave 1 implemented + committed (`9d589bd`)

---

## 7. EFFORT SUMMARY

| Wave | Description | Sessions | Status | Bottleneck |
|---|---|---|---|---|
| Wave 1 | Split interface + DbContext updates + service swap | 1 | ✅ COMPLETE (`9d589bd`) | None (build catches all misses) |
| Wave 2 | Services/repos + DI + config + docker | 1-2 | ✅ COMPLETE (Wave 1 + residual 2026-07-10) | None |
| Wave 3 | Architecture tests + existing tests + verify | 1 | ✅ COMPLETE (2026-07-10) | None |
| **Total** | | **3-5 sessions** | **Wave 1 done in 1 session** | |

**Critical path:** Wave 1 ✅ → Wave 2 ✅ → Wave 3 ✅ — ALL COMPLETE, READY FOR MERGE
