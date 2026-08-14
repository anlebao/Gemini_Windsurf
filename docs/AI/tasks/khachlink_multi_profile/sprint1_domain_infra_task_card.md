# TASK CARD — Sprint 1: Domain + Infrastructure (KhachLink Multi-Profile R1)

> **Status:** 🔵 IN PROGRESS
> **Priority:** P1 — First sprint of R1
> **Branch:** `feature/khachlink-multi-profile-r1`
> **Mode:** IMPLEMENT (Domain Phase + Infrastructure Phase)
> **Domain modification:** YES (approved 2026-08-15)

## Objective
Add `KhachLinkProfile` enum + `KhachLinkNavFlags` VO + `KhachLinkInstance` entity to Domain + EF config + migration + seed existing instance. Build pass + guard-check pass.

## Prerequisites
- [x] Plan approved (`C:\Users\lebao\.devin\plans\plan-69592e1cef008788.md`)
- [x] Branch `feature/khachlink-multi-profile-r1` created from `main`
- [x] Reference pattern studied: `1_Shared/Domain/ShopInstance.cs` (platform-level routing entity)
- [x] Reference EF config studied: `3_CoreHub/Infrastructure/Configurations/ShopInstanceConfiguration.cs`

## Phase A: Domain (1_Shared/Domain/)

### Task A1: Create KhachLinkProfile enum
**File:** `1_Shared/Domain/Aggregates/KhachLinkAggregate/KhachLinkProfile.cs`
```csharp
public enum KhachLinkProfile
{
    FullCommerce = 0,   // Type 4 — default, all features on
    Directory = 1,      // Type 1 — directory only
    Logistics = 2,      // Type 2 — community commerce focus (R3)
    JobMarket = 3,      // Type 3 — job/service marketplace (R3)
    Reseller = 4        // Type 5 — tenant trung gian (R2)
}
```

### Task A2: Create KhachLinkNavFlags value object
**File:** `1_Shared/Domain/Aggregates/KhachLinkAggregate/KhachLinkNavFlags.cs`
- 15 boolean properties (init-only): ShowHome, ShowCart, ShowOrders, ShowLoyaltyHistory, ShowMissions, ShowRewards, ShowAllianceWallet, ShowStores, ShowCampaigns, ShowScan, ShowQrClaim, ShowCommunity, ShowJobs, ShowProfile, ShowStaffDashboard
- Static factory `ForProfile(KhachLinkProfile)` returns preset:
  - `FullCommerce`: all true (default)
  - `Directory`: ShowHome=true, ShowStores=true, ShowProfile=true, rest false
  - `Logistics`: preset added in R3 (throw `NotImplementedException` for now OR return FullCommerce default with comment "R3")
  - `JobMarket`: preset added in R3 (same)
  - `Reseller`: preset added in R2 (same)
- **R1 only implements FullCommerce + Directory presets.** Others return `new KhachLinkNavFlags()` (all true) as safe default with `// TODO R2/R3` comment.

### Task A3: Create KhachLinkInstance entity
**File:** `1_Shared/Domain/Aggregates/KhachLinkAggregate/KhachLinkInstance.cs`
- Inherit `BaseEntity` (NOT AggregateRoot — no domain events needed, follows ShopInstance pattern)
- Properties:
  - `Label` (string, private set, max 200)
  - `Profile` (KhachLinkProfile, private set)
  - `CustomDomain` (string, private set, max 255, stored lowercase)
  - `OwnerTenantId` (Guid?, private set — null = platform-level)
  - `NavFlags` (KhachLinkNavFlags, private set — owned entity)
  - `IsActive` (bool, private set, default true)
- `TenantId` = `Guid.Empty` always (platform sentinel — set in constructor via `base(new TenantId(Guid.Empty))`)
- Factory: `Create(label, profile, customDomain, ownerTenantId?, navFlagsOverride?)` → validates non-empty label + customDomain, sets NavFlags = override ?? ForProfile(profile)
- Methods: `UpdateProfile(profile, navFlagsOverride?)`, `UpdateNavFlags(flags)`, `Activate()`, `Deactivate()`
- All methods call `UpdateAudit()` from BaseEntity

### Task A4: Domain invariants
- INV-K01: CustomDomain unique (enforced in service + DB unique index)
- INV-K02: CustomDomain stored lowercase (normalize in Create)
- INV-K03: Label non-empty (throw ArgumentException)
- INV-K04: TenantId always Guid.Empty (platform sentinel — NOT tenant-scoped)
- INV-K05: NavFlags defaults to ForProfile(profile) if not overridden
- INV-K06: Deactivate = soft delete (IsActive=false), NOT hard delete

## Phase B: Infrastructure (EF Core)

### Task B1: EF Configuration
**File:** `3_CoreHub/Infrastructure/Configurations/KhachLinkInstanceConfiguration.cs`
- Implement `IEntityTypeConfiguration<KhachLinkInstance>, IEntityConfiguration`
- Table: `KhachLinkInstances`
- `HasKey(e => e.Id)`, `ValueGeneratedOnAdd()`
- `Property("TenantId").IsRequired()` (platform sentinel, NOT unique-indexed)
- `Property(e => e.Label).IsRequired().HasMaxLength(200)`
- `Property(e => e.Profile).HasConversion<int>().IsRequired()`
- `Property(e => e.CustomDomain).IsRequired().HasMaxLength(255)`
- `HasIndex(e => e.CustomDomain).IsUnique()`
- `Property(e => e.OwnerTenantId).IsRequired(false)`
- `Property(e => e.IsActive).IsRequired().HasDefaultValue(true)`
- `OwnsOne(e => e.NavFlags, nf => { ... })` — 15 bool properties with column names `ShowHome`, `ShowCart`, etc. + `HasDefaultValue(true/false)` matching preset defaults
- Audit fields: `CreatedAt`, `UpdatedAt` `HasDefaultValueSql("CURRENT_TIMESTAMP")`, `IsDeleted` `HasDefaultValue(false)`

### Task B2: Register DbSet + exclusion in VanAnDbContext
**File:** `3_CoreHub/Infrastructure/VanAnDbContext.cs`
- Add `public DbSet<KhachLinkInstance> KhachLinkInstances { get; set; }` (near ShopInstances line 116)
- Add `KhachLinkInstance` to exclusion list in `ApplyMultiTenancyFilters` (line ~318):
  ```csharp
  && e.ClrType != typeof(KhachLinkInstance)
  ```
- Add to `IVanAnDbContext.cs`: `DbSet<KhachLinkInstance> KhachLinkInstances { get; }`

### Task B3: EF Migration
- `dotnet ef migrations add AddKhachLinkInstances --project 3_CoreHub --startup-project 2_Gateway`
- Tables: `KhachLinkInstances` (PG — Gateway source of truth)
- Seed: 1 instance for existing deployment (FullCommerce, CustomDomain=`diemthuong2.khachvip.online`, OwnerTenantId=null)
  - Use fixed seed GUID for idempotency: `0191a000-0000-0000-0000-000000000001` (UUIDv7-style for sort order)
- Migration `Down` drops table

### Task B4: Verify
- [ ] `dotnet build VanAn.sln` 0 errors
- [ ] `guard-check.ps1` ALL PASSED
- [ ] Migration `Up` + `Down` tested locally
- [ ] Single-Identity pattern: no `KhachLinkInstanceId` VO created (Id = PK only)
- [ ] NavFlags.ForProfile(FullCommerce) = all 15 true
- [ ] NavFlags.ForProfile(Directory) = ShowHome/Stores/Profile true, rest false

## Files Modified (expected)
1. `1_Shared/Domain/Aggregates/KhachLinkAggregate/KhachLinkProfile.cs` — NEW
2. `1_Shared/Domain/Aggregates/KhachLinkAggregate/KhachLinkNavFlags.cs` — NEW
3. `1_Shared/Domain/Aggregates/KhachLinkAggregate/KhachLinkInstance.cs` — NEW
4. `3_CoreHub/Infrastructure/Configurations/KhachLinkInstanceConfiguration.cs` — NEW
5. `3_CoreHub/Infrastructure/VanAnDbContext.cs` — ADD DbSet + exclusion
6. `3_CoreHub/Infrastructure/IVanAnDbContext.cs` — ADD DbSet
7. `3_CoreHub/Infrastructure/Migrations/<timestamp>_AddKhachLinkInstances.cs` — NEW
8. `3_CoreHub/Infrastructure/Migrations/<timestamp>_AddKhachLinkInstances.Designer.cs` — NEW
9. `3_CoreHub/Infrastructure/Migrations/VanAnDbContextModelSnapshot.cs` — UPDATED

## Rollback
- `dotnet ef migrations remove` (if migration not applied)
- `git checkout -- .` (if not committed)

## Approval Gate
- [ ] Build pass
- [ ] User approval before Sprint 2
