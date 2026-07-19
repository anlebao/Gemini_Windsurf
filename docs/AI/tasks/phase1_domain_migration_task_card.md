# Task Card: Phase 1 — Domain + Migration (Gateway Router Option C)

> **Master plan:** `gateway_router_multi_vps_master_plan.md`
> **Workflow:** `newfeaturebuild.md` (ANALYZE → IMPLEMENT)
> **Phase:** 1 of 7
> **Depends on:** —
> **Unlocks:** Phase 2, Phase 3, Phase 6

---

## 1. Use Case & Business Design

**Problem:** Multi-VPS routing requires a `ShopInstance` entity to track which ShopERP URL serves which tenant. No such entity exists in Domain. Tenant has no FK to a hosting ShopERP instance.

**Goal:** Add `ShopInstance` entity + `Tenant.ShopInstanceId` FK + EF migration (additive, with seed backfill). Foundation for all later phases.

**Out of scope:** API endpoints (Phase 2), Gateway router logic (Phase 3), Admin UI (Phase 6).

---

## 2. Reverse Impact Analysis

### Domain Layer (`1_Shared/`)
- **`Domain.cs`** (or split: `1_Shared/Domain/ShopInstance.cs`) — ADD `ShopInstance` entity:
  - Inherits `BaseEntity` (PK `Id` = `BaseEntity.Id`, follows Single-Identity Pattern).
  - No business key VO (this is an infrastructure-routing entity, not a business aggregate).
  - Properties: `BaseUrl` (string, required), `Label` (string, required), `MaxTenants` (int, default 50), `IsActive` (bool, default true), `HealthCheckUrl` (string?, optional), `LastHealthCheck` (DateTime?), `HealthStatus` (string, default "Unknown"), `CreatedAt` (DateTime).
  - Factory: `ShopInstance.Create(baseUrl, label, maxTenants = 50, healthCheckUrl = null)`.
  - Methods: `UpdateHealth(status, checkedAt)`, `Activate()`, `Deactivate()`, `UpdateLabel(label)`, `UpdateMaxTenants(max)`.
  - **Domain Modification — requires user approval per governance IMPLEMENT rule.**
- **`Domain.cs` → `Tenant` entity** — ADD:
  - `Guid? ShopInstanceId { get; private set; }` (nullable FK, backward compatible).
  - `AssignToShopInstance(Guid shopInstanceId)` method (validates non-empty, sets + UpdateAudit).
  - **Domain Modification — requires user approval.**

### Infrastructure Layer (`3_CoreHub/Infrastructure/`)
- **`IVanAnDbContext.cs`** — ADD `DbSet<ShopInstance> ShopInstances { get; }`.
- **`VanAnDbContext.cs`** — IMPLEMENT `DbSet<ShopInstance>` + `OnModelCreating` call to new configuration.
- **NEW: `Configurations/ShopInstanceConfiguration.cs`** — EF config:
  - Table name `"ShopInstances"`.
  - `builder.Ignore(e => e.BusinessKey)` (Single-Identity Pattern compliance — even though ShopInstance has no business key VO, BaseEntity has one; ignore it).
  - `Url` property max length 500, required.
  - `Label` max length 100, required.
  - `HealthStatus` max length 20, default "Unknown".
  - Index on `BaseUrl` (unique).
- **`Configurations/TenantConfiguration.cs`** — UPDATE:
  - Add `HasOne` relationship from `Tenant.ShopInstanceId` → `ShopInstance.Id` (nullable FK, no cascade delete — deleting ShopInstance must be blocked if tenants assigned).
- **NEW: `Migrations/{timestamp}_AddShopInstancesAndTenantFk.cs`** — migration:
  - `CreateTable("ShopInstances", ...)` with all columns + unique index on `BaseUrl`.
  - `AddColumn<Guid?>("ShopInstanceId", "Tenants", nullable: true)`.
  - `CreateForeignKey("FK_Tenants_ShopInstances_ShopInstanceId", "Tenants", "ShopInstanceId", "ShopInstances", "Id", restrict)`.
  - **Seed:** Insert 1 ShopInstance row (`Id = deterministic Guid`, `BaseUrl = read from config or default "http://shoperp:5003"`, `Label = "Default Local"`, `IsActive = true`).
  - **Backfill:** `UPDATE "Tenants" SET "ShopInstanceId" = {seeded_id} WHERE "ShopInstanceId" IS NULL`.
  - **No data loss. No drop. Additive only.**

### Tests
- **NEW: `6_Tests/VanAn.Core.Tests/Domain/ShopInstanceTests.cs`** — unit tests:
  - `Create_SetsProperties_Correctly`
  - `Create_WithEmptyBaseUrl_ThrowsArgumentException`
  - `Create_WithEmptyLabel_ThrowsArgumentException`
  - `Create_WithNegativeMaxTenants_ThrowsArgumentException`
  - `UpdateHealth_SetsStatusAndTimestamp`
  - `Deactivate_SetsIsActiveFalse`
  - `Activate_SetsIsActiveTrue`
  - `UpdateLabel_ThrowsForEmptyLabel`
- **NEW: `6_Tests/VanAn.Core.Tests/Domain/TenantShopInstanceAssignmentTests.cs`**:
  - `AssignToShopInstance_SetsShopInstanceId`
  - `AssignToShopInstance_WithEmptyGuid_ThrowsArgumentException`
- **Existing test suites:** Run full Core.Tests to ensure no regression from Tenant entity change.

### TDD Plan
1. Write failing tests for `ShopInstance.Create` + methods.
2. Implement `ShopInstance` entity → tests pass.
3. Write failing tests for `Tenant.AssignToShopInstance`.
4. Implement `Tenant` change → tests pass.
5. Add `ShopInstanceConfiguration` + update `TenantConfiguration`.
6. Add migration. Test migration applies on local PG (use `dotnet ef database update` against local Docker PG).
7. Verify backfill: query tenants after migration, all have `ShopInstanceId` set.

---

## 3. Detailed Coding Plan

### Namespace Strategy
- `VanAn.Shared.Domain` (ShopInstance entity, Tenant change)
- `VanAn.CoreHub.Infrastructure` (DbContext, IVanAnDbContext)
- `VanAn.CoreHub.Infrastructure.Configurations` (ShopInstanceConfiguration, TenantConfiguration update)
- `VanAn.CoreHub.Infrastructure.Migrations` (new migration)
- `VanAn.Core.Tests.Domain` (tests)

### Implementation Steps
**Step 1 — Domain (1-2 files):**
- Add `ShopInstance` entity (split file preferred: `1_Shared/Domain/ShopInstance.cs` to keep `Domain.cs` from growing).
- Modify `Tenant` in `Domain.cs` (or split file) — add `ShopInstanceId` + `AssignToShopInstance`.
- Build → 0 errors expected (no EF yet, just entity).

**Step 2 — Tests for Domain (2 new files):**
- Write `ShopInstanceTests.cs` + `TenantShopInstanceAssignmentTests.cs`.
- Run tests → all pass.

**Step 3 — Infrastructure (3 files):**
- `IVanAnDbContext.cs` add `DbSet<ShopInstance>`.
- `VanAnDbContext.cs` implement + `OnModelCreating` register `ShopInstanceConfiguration`.
- New `ShopInstanceConfiguration.cs`.
- Update `TenantConfiguration.cs` with FK.
- Build → 0 errors.

**Step 4 — Migration (1 file):**
- `dotnet ef migrations add AddShopInstancesAndTenantFk --project 3_CoreHub --startup-project 2_Gateway` (or appropriate project setup).
- Inspect generated migration: ensure it's additive (no drop).
- Edit migration `Up` method to add seed + backfill SQL (or use `migrationBuilder.Sql(...)`).
- Apply on local Docker PG: `dotnet ef database update`.
- Verify via `psql`: `\d "ShopInstances"` shows table; `SELECT "Id", "BaseUrl", "Label" FROM "ShopInstances";` shows 1 row; `SELECT count(*) FROM "Tenants" WHERE "ShopInstanceId" IS NOT NULL;` = total tenants.

**Step 5 — Full regression:**
- `dotnet build VanAn.sln` — 0 errors.
- Run all `6_Tests/VanAn.Core.Tests` — 0 failures (existing 998+ tests must still pass).
- `guard-check.ps1` PASS.

### Active Skills
- `domain-integrity-validation` (new entity + Single-Identity Pattern compliance)
- `outbox-pattern-implementation` (no outbox changes here, but ensure no outbox coupling breaks)

---

## 4. Validation Gates

| Gate | Command | Expected |
|---|---|---|
| Build | `dotnet build VanAn.sln` | 0 errors |
| Unit tests | `dotnet test 6_Tests/VanAn.Core.Tests` | All pass, including new ShopInstance + Tenant tests |
| Migration apply (local) | `dotnet ef database update --project 3_CoreHub --startup-project 2_Gateway` | Success, no data loss |
| Migration verify (local) | `psql` queries | 1 ShopInstance row, all Tenants backfilled |
| Guard check | `./guard-check.ps1` | PASS |

---

## 5. Deliverables

- New file: `1_Shared/Domain/ShopInstance.cs`
- Modified: `1_Shared/Domain.cs` (Tenant entity) OR split file `1_Shared/Domain/Tenant.cs` if Tenant is already split
- Modified: `3_CoreHub/Infrastructure/IVanAnDbContext.cs`
- Modified: `3_CoreHub/Infrastructure/VanAnDbContext.cs`
- New file: `3_CoreHub/Infrastructure/Configurations/ShopInstanceConfiguration.cs`
- Modified: `3_CoreHub/Infrastructure/Configurations/TenantConfiguration.cs`
- New file: `3_CoreHub/Infrastructure/Migrations/{timestamp}_AddShopInstancesAndTenantFk.cs`
- New file: `6_Tests/VanAn.Core.Tests/Domain/ShopInstanceTests.cs`
- New file: `6_Tests/VanAn.Core.Tests/Domain/TenantShopInstanceAssignmentTests.cs`

---

## 6. Approval Gate

**Domain modification requires user approval per governance IMPLEMENT rule.**

Before opening this task card for execution, user must confirm:
- [ ] `ShopInstance` entity addition approved
- [ ] `Tenant.ShopInstanceId` FK + `AssignToShopInstance` method approved
- [ ] Migration strategy (additive + seed + backfill) approved
