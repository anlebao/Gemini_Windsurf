# TASK CARD: Phase 2 — EF Configuration + Migration

> **Master plan:** `docs/AI/plans/crawl-onboarding-master-plan.md`
> **Research snapshot:** `docs/AI/plans/crawl-onboarding-research.md` (verify line refs before edit)
> **Depends on:** Phase 1 complete (Domain entities exist)
> **Status:** PENDING

## 1. OBJECTIVE

Map new Domain entities to EF Core + create migrations. **Critical:** BOTH CoreHub (PG) AND ShopERP (SQLite) need migration for Tenants column changes (correction C2).

## 2. GATES & HARD STOPS

- **🔴 Single-Identity Pattern:** EF config `Ignore` business key VOs. FK columns are `Guid`. No `HasConversion` on FK (only on `TenantId` PK if existing pattern requires).
- **Multi-tenancy:** `TenantClaimRequest` + `CrawlSource` must carry tenant scoping (FK to Tenants.Id).

## 3. PRE-CONDITIONS

- [ ] Phase 1 done — Domain compiles
- [ ] Re-verify `TenantConfiguration.cs:46-49` (Status default) + `ShopERPDbContext.cs:55` (DbSet<Tenant>)
- [ ] Decide: `CrawlSource` + `TenantClaimRequest` PG-only (no SQLite mirror) — confirm Gateway is only context querying them

## 4. FILES TO MODIFY / CREATE

### CoreHub (PG) — MODIFY
| Path | Change |
|---|---|
| `3_CoreHub/Infrastructure/Configurations/TenantConfiguration.cs` | Add `Settings_CrawledPhone` mapping in `OwnsOne(Settings)`: `settings.Property(s => s.CrawledPhone).HasColumnName("Settings_CrawledPhone").HasMaxLength(50)`. Add `PotentialDuplicateOf` (Guid?, nullable, NO FK constraint). DO NOT change `Status` default (keep `Active`). |

### CoreHub (PG) — CREATE
| Path | Role |
|---|---|
| `3_CoreHub/Infrastructure/Configurations/TenantClaimRequestConfiguration.cs` | Map `TenantClaimRequests` table: PK `Id` (Guid), FK `TenantId` → `Tenants.Id` (Restrict delete), string cols max length, `Status` enum→int, timestamps. Indexes: `IX_TenantClaimRequests_TenantId`, `IX_TenantClaimRequests_Status` (queue query). |
| `3_CoreHub/Infrastructure/Configurations/CrawlSourceConfiguration.cs` | Map `CrawlSources` table: PK `Id`, FK `TenantId` → `Tenants.Id` (Cascade delete — audit trail deleted with tenant), `RawJson` as `text`. Index: `IX_CrawlSources_TenantId`. |
| `3_CoreHub/Infrastructure/Migrations/<timestamp>_AddCrawlOnboarding.cs` | `CreateTable("TenantClaimRequests", ...)`, `CreateTable("CrawlSources", ...)`, `AddColumn<Guid?>("PotentialDuplicateOf", "Tenants")`, `AddColumn<string>("Settings_CrawledPhone", "Tenants", maxLength: 50, nullable: true)`, indexes. |

### CoreHub — MODIFY DbSets
- `IVanAnDbContext`: add `DbSet<TenantClaimRequest> TenantClaimRequests` + `DbSet<CrawlSource> CrawlSources`
- `VanAnDbContext`: same

### ShopERP (SQLite) — CREATE migration (correction C2)
| Path | Role |
|---|---|
| `5_WebApps/ShopERP.Migrations/<timestamp>_AddCrawlOnboardingTenantsColumns.cs` | **Only 2 columns on Tenants** (mirror): `AddColumn<Guid?>("PotentialDuplicateOf", "Tenants")` + `AddColumn<string>("Settings_CrawledPhone", "Tenants", maxLength: 50, nullable: true)`. NO `TenantClaimRequests`/`CrawlSources` tables (PG-only). |

## 5. ACCEPTANCE CRITERIA

- [ ] `dotnet build VanAn.sln` — 0 errors
- [ ] `dotnet ef migrations add AddCrawlOnboarding --project 3_CoreHub --startup-project 2_Gateway` succeeds
- [ ] ShopERP SQLite migration generated + builds
- [ ] `TenantConfiguration.Status` default UNCHANGED (`Active`) — Pending=5 set explicitly by factory
- [ ] `PotentialDuplicateOf` has NO FK constraint (just Guid? reference — avoid cascade issues)
- [ ] `CrawlSource.RawJson` is `text` (unbounded)
- [ ] Inspection of generated `.Designer.cs` shows correct model snapshot

## 6. VERIFICATION

```powershell
dotnet ef migrations add AddCrawlOnboarding --project 3_CoreHub --startup-project 2_Gateway
dotnet build VanAn.sln
```
Inspect generated migration files. Verify both PG (CoreHub) + SQLite (ShopERP) schemas updated.

## 7. CORRECTIONS APPLIED

| # | Correction |
|---|---|
| C1 | FK `TenantId` is Guid column, no TenantIdConverter on FK |
| C2 | ShopERP SQLite migration added for 2 Tenants columns (legacy plan said "NO ShopERP migration" — SAI) |
| H1 | Status default kept `Active`, Pending=5 explicit in factory |
