# TASK CARD — Sprint 2: Gateway API + Services (Issue #126)

> **Status:** ✅ COMPLETE (2026-08-14)
> **Priority:** P2 — After Sprint 1 approval
> **Branch:** `feature/guard-qr-r2-sprint2`
> **Mode:** IMPLEMENT (Application Phase)
> **Domain modification:** NO

## Objective
Implement `GuardController` (9 endpoints) + `GuardService` business logic + QR generation + R2 presigned URL flow. Build pass + guard-check pass.

## Prerequisites
- [x] Sprint 1 complete (Domain + EF + R2 client)
- [x] Sprint 0 QR library choice confirmed

## Task 1: GuardService (3_CoreHub/Services)

### Interface: IGuardService
```csharp
Task<PresignUploadResult> PresignUploadAsync(Guid tenantId, string contentType);
Task<IssueResult> IssueAsync(Guid tenantId, Guid guardId, IssueRequest req);
Task<ClaimResult> ClaimAsync(Guid tenantId, Guid customerId, ClaimRequest req);
Task<VerifyResult> VerifyAsync(Guid tenantId, Guid guardId, string scannedQrPayload);
Task<CheckoutResult> CheckoutAsync(Guid tenantId, Guid guardId, Guid sessionId);
Task<FlagResult> FlagAsync(Guid tenantId, Guid guardId, Guid sessionId, string reason);
Task<VoidResult> VoidAsync(Guid tenantId, Guid guardId, Guid sessionId, string reason);
Task<TodaySessionsResult> GetTodaySessionsAsync(Guid tenantId, VehicleSessionStatus? status, int page, int pageSize);
Task<SessionDetailResult> GetSessionAsync(Guid tenantId, Guid sessionId);
```

### Implementation logic:
- **PresignUpload:** Generate 2 keys (`plates/{tenantId}/{guid}.jpg`, `customers/{tenantId}/{guid}.jpg`) → call R2StorageService → return 2 presigned PUT URLs + keys
- **Issue:**
  1. Generate QrPayload = `{sessionId, token, tenantId}` (JSON or JWT-like)
  2. Hash QrPayload (SHA256) → QrTokenHash
  3. Generate ShortCode (6-digit random, check unique per tenant per day)
  4. Create VehicleSession(Issued, ...) → save
  5. Return QrPayload + ShortCode + SessionId
- **Claim:**
  1. Lookup by QrTokenHash (from qrPayload) OR ShortCode
  2. If Status != Issued → throw (already claimed or voided)
  3. session.Claim(customerId) → save
  4. Generate presigned GET URLs for photos (TTL 1h)
  5. Return session detail + photo URLs
- **Verify:**
  1. Hash scannedQrPayload → lookup by QrTokenHash
  2. If not found / voided → return error
  3. Generate presigned GET URLs for photos
  4. Return session detail + photos
  5. Log GuardScanLog (Match or Mismatch based on lookup result)
- **Checkout:** session.Checkout(guardId) → save → return
- **Flag:** session.Flag(reason, guardId) → save → log GuardScanLog(Flagged) → return
- **Void:** session.Void() → save → return
- **GetTodaySessions:** query by TenantId + IssuedAt >= today + optional status filter → paginate
- **GetTodayStats:** count by status (Issued+Claimed = inLot, CheckedOut = checkOut, total issued = checkIn)

### QR generation:
- Use QRCoder (or chosen lib) to generate QR PNG from QrPayload
- Return as base64 string in IssueResult (for Guard UI to display + printer)

## Task 2: GuardController (2_Gateway/Controllers)

### Endpoints per master plan Section 4
- All endpoints `[Authorize(Roles="Guard")]` except `/claim` (Customer JWT)
- TenantId from JWT claim (standard pattern)
- GuardId from JWT sub claim
- Input validation (FluentValidation or DataAnnotations — match existing pattern)
- **Pattern #10 compliance:** Strip charset from Content-Type in any forward controller

## Task 3: DI Registration
- Register `IGuardService` → `GuardService`
- Register `IVehicleSessionRepository` → `VehicleSessionRepository`
- Register `IGuardScanLogRepository` → `GuardScanLogRepository`
- Register `IR2StorageService` → `R2StorageService` (if not done in Sprint 1)

## Task 4: Feature Flag
- `Guard:QrVerifyEnabled` in appsettings.json (default false)
- GuardController checks flag → 503 if disabled (graceful fallback to old page)

## Validation
- [ ] `dotnet build VanAn.sln` 0 errors
- [ ] `guard-check.ps1` ALL PASSED
- [ ] Swagger shows 9 new endpoints under `/api/guard/*`
- [ ] Manual test: presign-upload → issue → verify → checkout (via Swagger/curl)
- [ ] Unit tests: GuardService (Issue, Claim, Verify, Checkout, Flag) — happy path + edge cases

## Files Modified (expected)
1. `3_CoreHub/Services/GuardService.cs` (new)
2. `3_CoreHub/Services/IGuardService.cs` (new)
3. `2_Gateway/Controllers/GuardController.cs` (new)
4. `2_Gateway/Program.cs` — DI registration
5. `2_Gateway/appsettings.json` — feature flag

## Rollback
- Feature flag OFF → old hardcode page still works
- `git revert` commit if needed

## Approval Gate
- [ ] Build pass + unit tests pass
- [ ] User approval before Sprint 3
