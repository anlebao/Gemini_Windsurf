# TASK CARD: Stream E - DB Migration Strategy (EF Core Migrations)

> **SPAWNED from:** Wave 0 Gap-1 (stale dev DB schema). See `wave0_hkd_fix_preflight_task_card.md` Section 14 Gap-1.
> **User decision (2026-07-03):** (1) Use EF Core Migrations as official schema management, (2) Remove `EnsureCreated()` from production, (3) Modify VA-ARCH-001 to allow Migrations at correct layer (Infrastructure) while still preventing other architecture violations.

## 1. GOAL & CONTEXT
- **Mục tiêu cốt lõi:** Replace `EnsureCreatedAsync()` strategy with EF Core Migrations for production-safe schema management. Fix stale dev DB schema (AccountingEntries missing ALL domain columns: Amount, EntryType, AccountCode, PeriodYear, PeriodMonth, etc.). Enable production deployment of Stream D Wave 2+ changes without data loss.
- **Nghiệp vụ áp dụng:** Production deployment safety — `EnsureCreatedAsync()` only creates schema if DB doesn't exist, never updates existing schema. No Migrations currently (VA-ARCH-001 forbids). This is a **production deployment trap**.
- **Status:** PENDING — Planning & Approval (user decision made, execution not started)
- **Branch:** `feature/stream-e-db-migration-strategy`
- **Estimated Sessions:** 1-2

---

## 2. ACTIVE WORKFLOW ROUTING
- **Target Workflow:** `newfeaturebuild.md` (ANALYZE → IMPLEMENT — architectural change)
- **Execution Mode:** ANALYZE first (verify all EnsureCreated callers + VA-ARCH-001 scope), then IMPLEMENT (after user approval)
- **Current Phase:** Stream E (separate from Stream D waves)
- **Dependency:** Wave 0 Gap-1 finding (stale DB schema confirmed)
- **Blocks:** Stream D Wave 2 verification (Option A query needs domain columns in AccountingEntries)

---

## 3. RELEVANT FILES (CONTEXT BOUNDARY)

### Files được phép đọc/sửa
- `docs/AI/project_state.md` (READ)
- `6_Tests/VanAn.Architecture.Tests/ArchitectureTests.cs` (UPDATE — VA-ARCH-001: allow Migrations at Infrastructure layer, still prevent in Application layer)
- `3_CoreHub/Program.cs` (UPDATE — replace `EnsureCreatedAsync()` L48 with `MigrateAsync()`)
- `5_WebApps/ShopERP/Program.cs` (UPDATE — replace `EnsureCreatedAsync()` L302 with `MigrateAsync()`)
- `3_CoreHub/Infrastructure/VanAnDbContext.cs` (READ — verify model configuration)
- `3_CoreHub/Infrastructure/Configurations/AccountingEntryConfiguration.cs` (READ — verify column mappings)
- `Directory.Packages.props` (UPDATE — add `Microsoft.EntityFrameworkCore.Design` package if not present)
- `3_CoreHub/VanAn.CoreHub.csproj` (UPDATE — add `<PackageReference>` for EF Core Design if needed)
- `5_WebApps/ShopERP/*.csproj` (UPDATE — same)
- `docs/knowledge-base/08-ai/AGENTS.md` (UPDATE — remove "EnsureCreated only" rule, add Migrations policy)
- `docs/knowledge-base/04-standards/ReviewChecklist.md` (UPDATE — change "No EnsureCreated in production" to "Use MigrateAsync in production, EnsureCreated only for tests")

### NEW files to create
- `3_CoreHub/Infrastructure/Migrations/` (NEW folder — EF Core Migrations)
- `3_CoreHub/Infrastructure/Migrations/<timestamp>_InitialCreate.cs` (NEW — initial migration from current model)
- `3_CoreHub/Infrastructure/DesignTimeDbContextFactory.cs` (NEW — EF Core design-time factory for `dotnet ef` CLI)

### Boundary Rules (Nghiêm cấm)
- KHÔNG xóa VA-ARCH-001 entirely — modify to allow Migrations at Infrastructure layer ONLY
- KHÔNG allow Migrations in Application layer (5_WebApps, 2_Gateway) — still forbidden
- KHÔNG modify Domain layer (`1_Shared/Domain*.cs`)
- KHÔNG delete existing dev DB data without confirmation (use `MigrateAsync()` which preserves data)
- KHÔNG use `EnsureDeletedAsync()` in production code

---

## 4. TECHNICAL & REGULATORY CONSTRAINTS (HARDENING GATES)
- [ ] **VA-ARCH-001 modification:** Change test to check `3_CoreHub/Infrastructure/Migrations/` is ALLOWED, but `5_WebApps/*/Migrations/` and `2_Gateway/Migrations/` are STILL FORBIDDEN
- [ ] **MigrateAsync replaces EnsureCreatedAsync:** `await context.Database.MigrateAsync()` instead of `await context.Database.EnsureCreatedAsync()`
- [ ] **Tests still use EnsureCreated:** In-memory SQLite test contexts keep `EnsureCreated()` (faster, no migration files needed for tests)
- [ ] **Initial migration:** `dotnet ef migrations add InitialCreate` from current model — generates schema for ALL entities including AccountingEntry domain columns
- [ ] **Dev DB upgrade:** `dotnet ef database update` applies migration to existing dev DB (preserves data, adds missing columns)
- [ ] **Production deployment:** `MigrateAsync()` runs on app startup — applies pending migrations automatically. Safe for production (EF Core migrations are idempotent + transactional)
- [ ] **Multi-tenancy:** Migration must not break multi-tenancy query filters (verify after migration)
- [ ] **Build Check:** `dotnet build VanAn.sln` Release pass
- [ ] **Test Check:** `dotnet test` — all existing tests pass (tests use EnsureCreated, not affected)
- [ ] **Architecture Test:** VA-ARCH-001 modified test passes (allows Infrastructure Migrations, forbids Application Migrations)

---

## 5. SUCCESS CRITERIA (ĐO LƯỜNG ĐƯỢC)
- [ ] **SC1:** VA-ARCH-001 test modified — allows `3_CoreHub/Infrastructure/Migrations/`, still forbids `5_WebApps/*/Migrations/` + `2_Gateway/Migrations/`
- [ ] **SC2:** `DesignTimeDbContextFactory` created in `3_CoreHub/Infrastructure/`
- [ ] **SC3:** `Microsoft.EntityFrameworkCore.Design` package added (if not present)
- [ ] **SC4:** Initial migration created (`dotnet ef migrations add InitialCreate`) — generates AccountingEntries with ALL domain columns (Amount, EntryType, AccountCode, PeriodYear, PeriodMonth, AccountingBookType, VatRate, Description, ReversalEntryId, Vendor, Category, Reference)
- [ ] **SC5:** `EnsureCreatedAsync()` replaced with `MigrateAsync()` in `3_CoreHub/Program.cs` L48
- [ ] **SC6:** `EnsureCreatedAsync()` replaced with `MigrateAsync()` in `5_WebApps/ShopERP/Program.cs` L302
- [ ] **SC7:** Dev DB migrated — `PRAGMA table_info(AccountingEntries)` shows ALL domain columns
- [ ] **SC8:** `dotnet build VanAn.sln` Release — 0 errors
- [ ] **SC9:** `dotnet test` — all pass (existing tests unaffected, use EnsureCreated in-memory)
- [ ] **SC10:** VA-ARCH-001 modified test passes
- [ ] **SC11:** guard-check.ps1 PASSED
- [ ] **SC12:** Governance docs updated (AGENTS.md, ReviewChecklist.md)

---

## 6. ACTIVE SKILLS (MAX 3)
- `domain-integrity-validation` — Verify migration doesn't break Domain entity mappings
- `build-error-analysis` — Fix build errors from migration scaffolding
- `ci-build-debug` — Verify CI compatibility with new migration files

---

## 7. AI HEALTH CHECK MATRIX (INITIAL)
- **Evidence Count:** 6 verified facts from Wave 0
- **Verified Facts:**
  - Fact 1: Dev DB `AccountingEntries` schema STALE — only BaseEntity columns (Id, AccountingEntryId, TenantId, CreatedAt, UpdatedAt, CreatedBy, UpdatedBy, IsDeleted). Missing: Amount, EntryType, AccountCode, PeriodYear, PeriodMonth, AccountingBookType, VatRate, Description, ReversalEntryId, Vendor, Category, Reference
  - Fact 2: `EnsureCreatedAsync()` called in `5_WebApps/ShopERP/Program.cs` L302 + `3_CoreHub/Program.cs` L48
  - Fact 3: No Migrations folder exists (VA-ARCH-001 forbids — ArchitectureTests.cs L70-115)
  - Fact 4: `AccountingEntryConfiguration.cs` maps all domain columns correctly (Amount precision 18,2; EntryType int; AccountCode maxLength 20; PeriodYear/Month int)
  - Fact 5: `ApplyConfigurationsFromAssembly` called in VanAnDbContext L143 — configurations will be included in migration
  - Fact 6: Tests use `EnsureCreated()` in-memory (12+ test files) — NOT affected by migration change
- **Assumptions:**
  - `Microsoft.EntityFrameworkCore.Design` package may not be in Directory.Packages.props (need to check + add)
  - SQLite supports ALTER TABLE ADD COLUMN (EF Core migration will generate this for existing DB)
- **Open Questions:**
  - Q1: Is `Microsoft.EntityFrameworkCore.Design` already in deps? (Check Directory.Packages.props)
  - Q2: Does `3_CoreHub` or `5_WebApps/ShopERP` need the Design package? (Design package needed in project where `dotnet ef` runs)
  - Q3: Where should `dotnet ef` run — `3_CoreHub` (where VanAnDbContext lives) or `5_WebApps/ShopERP` (where app runs)?
- **Recommended Action:** PROCEED after ANALYZE — verify deps + VA-ARCH-001 scope, then implement

---

## 8. REVERSE IMPACT ANALYSIS
| File thay đổi | Reverse impact | Mitigation |
|---|---|---|
| `ArchitectureTests.cs` (VA-ARCH-001) | Test logic changes — must still catch Application-layer Migrations | Modified test: allow `3_CoreHub/Infrastructure/Migrations/`, forbid `5_WebApps` + `2_Gateway` |
| `3_CoreHub/Program.cs` (EnsureCreated → Migrate) | App startup behavior changes — runs pending migrations on startup | MigrateAsync is idempotent + transactional (safe) |
| `5_WebApps/ShopERP/Program.cs` (same) | Same | Same |
| `3_CoreHub/Infrastructure/Migrations/` (NEW) | New folder + migration files — adds to repo size | Migration files are small C# classes, version-controlled |
| `DesignTimeDbContextFactory.cs` (NEW) | Needed for `dotnet ef` CLI to instantiate DbContext at design time | Standard EF Core pattern |
| Dev DB (vanan_shoperp.db) | Schema upgraded — missing columns added | `MigrateAsync()` preserves existing data, adds columns with NULL default |
| Tests (NOT changed) | Tests use in-memory EnsureCreated — unaffected | No action needed |

---

## 9. TDD & TESTING STRATEGY
- **Unit tests:** N/A — migration is infrastructure, not business logic
- **Integration tests:** Existing tests unaffected (use EnsureCreated in-memory)
- **Architecture test:** VA-ARCH-001 modified — verify allows Infrastructure Migrations, forbids Application Migrations
- **Manual verification:** `PRAGMA table_info(AccountingEntries)` after migration shows ALL domain columns
- **Verification:** `dotnet build` + `dotnet test` + `dotnet ef database update` + PRAGMA query

---

## 10. JIT PLANNING + PURE EXECUTION (MICRO-PHASES)

### Chiến lược thực thi: ANALYZE deps → modify VA-ARCH-001 → add Design package → create factory → create migration → replace EnsureCreated → migrate dev DB → verify
1. ANALYZE: Check `Directory.Packages.props` for `Microsoft.EntityFrameworkCore.Design` (W0.5-T1)
2. ANALYZE: Read VA-ARCH-001 full test logic — determine modification scope
3. IMPLEMENT: Modify VA-ARCH-001 test (allow Infrastructure Migrations, forbid Application Migrations)
4. IMPLEMENT: Add `Microsoft.EntityFrameworkCore.Design` to `Directory.Packages.props` + csproj
5. IMPLEMENT: Create `DesignTimeDbContextFactory.cs` in `3_CoreHub/Infrastructure/`
6. IMPLEMENT: `dotnet ef migrations add InitialCreate` (in `3_CoreHub` project)
7. IMPLEMENT: Replace `EnsureCreatedAsync()` with `MigrateAsync()` in both Program.cs files
8. IMPLEMENT: `dotnet ef database update` (apply migration to dev DB)
9. VERIFY: `PRAGMA table_info(AccountingEntries)` — confirm ALL domain columns present
10. VERIFY: `dotnet build` + `dotnet test` + guard-check
11. UPDATE: Governance docs (AGENTS.md, ReviewChecklist.md)

### Micro-phase breakdown

| Session | JIT Planning | Pure Execution |
|---|---|---|
| **S1** (ANALYZE) | Check deps, read VA-ARCH-001, determine `dotnet ef` target project | Document findings |
| **S1/S2** (IMPLEMENT) | Chốt: VA-ARCH-001 modification approach, migration target project | Modify VA-ARCH-001, add Design package, create factory, create initial migration, replace EnsureCreated, migrate dev DB, verify schema, build + test, update docs |

### Rules
- 1 step tại 1 thời điểm — build verify sau mỗi change
- KHÔNG delete dev DB — use `MigrateAsync()` (preserves data)
- KHÔNG skip VA-ARCH-001 modification — must allow Infrastructure Migrations explicitly
- If `dotnet ef migrations add` fails → check Design package + DesignTimeDbContextFactory
- If `dotnet ef database update` fails on existing DB → check SQLite ALTER TABLE compatibility

---

## 11. ESTIMATED EFFORT
- 1-2 sessions (ANALYZE + IMPLEMENT + verify)
- **BLOCKER:** None — user decision made, standard EF Core migration pattern
- **CRITICAL:** Blocks Stream D Wave 2 verification (Option A query needs domain columns)
- **RISK:** Low — EF Core Migrations is standard pattern, MigrateAsync is idempotent + transactional
- **PRODUCTION SAFETY:** MigrateAsync on app startup is the recommended EF Core deployment pattern. Existing data preserved. Missing columns added with NULL default (safe for existing rows).
