# TASK CARD — Sprint 1: Domain + Infrastructure (Issue #126)

> **Status:** ✅ COMPLETE (2026-08-14)
> **Priority:** P1 — After Sprint 0 approval
> **Branch:** `feature/guard-qr-r1`
> **Mode:** IMPLEMENT (Domain Phase + Infrastructure Phase)
> **Domain modification:** YES (approved 2026-08-14)

## Objective
Add `VehicleSession` + `GuardScanLog` aggregates to Domain.cs + EF config + migration + R2 storage client. Build pass + guard-check pass.

## Prerequisites
- [x] Sprint 0 findings approved
- [x] R2 credentials available (in `appsettings.json`)
- [x] Sprint 1 field list confirmed

## Phase A: Domain (1_Shared/Domain.cs)

### Task A1: Add Enums
```csharp
public enum VehicleSessionStatus { Issued=0, Claimed=1, CheckedOut=2, Voided=3, Flagged=4 }
public enum GuardScanResult { Match=0, Mismatch=1, ManualOverride=2, Flagged=3 }
```

### Task A2: Add Value Objects
```csharp
public sealed record VehicleSessionId(Guid Value) : BaseEntity(tenantId);
public sealed record GuardScanLogId(Guid Value) : BaseEntity(tenantId);
```
> Note: BaseEntity requires TenantId — check existing VO pattern (e.g., OrderId).

### Task A3: Add VehicleSession Aggregate Root
- Fields per master plan Section 3
- Constructor: `Create(tenantId, plateNumber, platePhotoKey, customerPhotoKey, issuedBy, qrTokenHash, shortCode)` → sets `Id = VehicleSessionId.Value`, Status=Issued
- Methods: `Claim(customerId)`, `Checkout(guardId)`, `Flag(reason, guardId)`, `Void()`
- **Immutable pattern:** Status transitions via methods, không public setter
- **Guard clauses:** Claim throws nếu Status != Issued; Checkout throws nếu Status == Voided/CheckedOut

### Task A4: Add GuardScanLog Entity
- Fields per master plan Section 3
- Constructor: `Create(tenantId, vehicleSessionId, scannedQrTokenHash, matchResult, scannedBy, notes)`
- Not aggregate root — simple entity

### Task A5: Domain invariants
- INV-G01: QrTokenHash unique per tenant (enforced in service + DB unique index)
- INV-G02: ShortCode unique per tenant per day (enforced in service + DB)
- INV-G03: Status transition: Issued→Claimed→CheckedOut, Issued→CheckedOut (paper, no claim), Issued→Voided, Issued/Claimed→Flagged
- INV-G04: Photos required at issuance (PlatePhotoKey + CustomerPhotoKey not empty)
- INV-G05: **Channel C→A migration** — `Claim(customerId)` cho phép gọi trễ bất cứ lúc nào khi Status=Issued (khách nhận paper ticket trước, claim digital sau). CustomerId ban đầu = null (Channel C), sau claim = CustomerId của KhachLink user. Không tạo QR mới — cùng QrToken.
- INV-G06: `Claim` idempotent reject — nếu session đã Claimed (CustomerId != null) → throw, không ghi đè CustomerId (chống 1 vé claim bởi 2 khách)

## Phase B: Infrastructure (EF Core + R2)

### Task B1: EF Configuration
- `VehicleSessionConfiguration` — in Gateway EF config folder
  - `Ignore(e => e.VehicleSessionId)` (Single-Identity)
  - `HasIndex(e => new { e.TenantId, e.QrTokenHash }).IsUnique()`
  - `HasIndex(e => new { e.TenantId, e.ShortCode, e.IssuedAt.Date }).IsUnique()`
  - `HasIndex(e => new { e.TenantId, e.Status })` (query today's sessions)
- `GuardScanLogConfiguration`
  - `Ignore(e => e.GuardScanLogId)`
  - `HasOne(e => e.VehicleSession).WithMany().HasForeignKey(e => e.VehicleSessionId)`
  - `HasIndex(e => new { e.TenantId, e.ScannedAt })`

### Task B2: EF Migration
- `AddMigration AddGuardQrVerifyTables`
- Tables: `VehicleSessions`, `GuardScanLogs` (PG — Gateway source of truth)
- **NOT** in SQLite (ShopERP) — Guard API qua Gateway HTTP

### Task B3: R2 Storage Client
- Nuget: `AWSSDK.S3` (if not installed)
- `IR2StorageService` interface (in 3_CoreHub or 2_Gateway)
- `R2StorageService` implementation:
  - `GetPresignedUploadUrl(key, contentType, ttlMinutes)` → string
  - `GetPresignedDownloadUrl(key, ttlMinutes)` → string
  - Config: `R2:Endpoint`, `R2:AccessKey`, `R2:SecretKey`, `R2:BucketName`
- Register in DI (Gateway Program.cs)

### Task B4: Repository
- `IVehicleSessionRepository` + `VehicleSessionRepository`
  - `GetByIdAsync(id, tenantId)`
  - `GetByQrTokenHashAsync(hash, tenantId)`
  - `GetByShortCodeAsync(shortCode, tenantId, date)`
  - `GetTodaySessionsAsync(tenantId, status?, page, pageSize)`
  - `AddAsync(session)`
  - `GetTodayStatsAsync(tenantId)` → (checkInCount, checkOutCount, inLotCount)
- `IGuardScanLogRepository` + `GuardScanLogRepository`
  - `AddAsync(log)`
  - `GetBySessionAsync(sessionId, tenantId)`

## Validation
- [ ] `dotnet build VanAn.sln` 0 errors
- [ ] `guard-check.ps1` ALL PASSED
- [ ] Migration `Up` + `Down` tested locally
- [ ] R2 presigned URL generation tested (unit test or manual)
- [ ] Domain invariants unit tested (VehicleSession state transitions)

## Files Modified (expected)
1. `1_Shared/Domain.cs` — add enums + VOs + 2 entities
2. `1_Shared/Domain/Aggregates/` — new folder `GuardAggregate/` with partial classes (if split)
3. `2_Gateway/` — EF config + migration + R2 service + repositories
4. `2_Gateway/Program.cs` — DI registration
5. `2_Gateway/appsettings.json` — R2 config section

## Rollback
- `dotnet ef migrations remove` (if migration not applied)
- `git checkout -- .` (if not committed)
- R2 bucket can be deleted (no cost)

## Approval Gate
- [ ] Build pass
- [ ] User approval before Sprint 2
