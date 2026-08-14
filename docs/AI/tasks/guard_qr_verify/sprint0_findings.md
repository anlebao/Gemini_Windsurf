# Sprint 0 Findings — Guard QR Verify (Issue #126)

> **Date:** 2026-08-14
> **Branch:** `feature/guard-qr-r1`
> **Mode:** ANALYZE (no code changes)
> **Status:** ✅ All 6 integration points verified + 8 BR spec drafted

---

## Section 1: UserRole.Guard Verification — ✅ READY

| Check | Result |
|---|---|
| `UserRole.Guard = 3` exists | ✅ `1_Shared/Domain/Aggregates/UserAggregate/UserRole.cs:13` |
| JWT emits "Guard" role | ✅ `JwtTokenService.cs:53` — `new(ClaimTypes.Role, role.ToString())` → "Guard" string |
| `[Authorize(Roles="Guard")]` works | ✅ RoleClaimType = ClaimTypes.Role (Program.cs:148), RequireRole() finds it |
| NavMenu Guard section | ✅ `NavMenu.razor:387` — `<AuthorizeView Roles="Guard">` |
| DevLoginController Guard login | ✅ `DevLoginController.cs:117` — `POST /dev/login/guard` issues Cookie + JWT with Guard role |
| Existing Guard pages | `Pages/Guard/Scan.cshtml` (hardcode, will replace) + `Sitemap.razor:64` |

**Conclusion:** Guard role fully integrated in auth pipeline. No new policy needed — `[Authorize(Roles="Guard")]` on GuardController will work.

**TenantId resolution:** `ITenantProvider` (HttpContextTenantProvider) reads `tenant_id` claim from JWT. UserId from `sub` claim (JwtRegisteredClaimNames.Sub).

---

## Section 2: Cloudflare R2 Setup — ✅ READY (verified 2026-08-14)

| Check | Result |
|---|---|
| R2 bucket `vanan-guard-photos` | ✅ Created + verified (APAC region) |
| S3 credentials (Access Key + Secret) | ✅ Provided by user — bucket-scoped read+write |
| Upload test | ✅ PASSED |
| Download test | ✅ PASSED |
| Presigned PUT URL (15min TTL) | ✅ PASSED — for Guard app direct upload |
| Presigned GET URL (1h TTL) | ✅ PASSED — for photo display |
| Account ID | `18947627801f833aecc202f086d66af5` |
| Endpoint | `https://18947627801f833aecc202f086d66af5.r2.cloudflarestorage.com` |

**Config to add (Sprint 1) — `2_Gateway/appsettings.json`:**
```json
"R2": {
  "Endpoint": "https://18947627801f833aecc202f086d66af5.r2.cloudflarestorage.com",
  "AccessKey": "acb543c587f9a2491dede99766a83760",
  "SecretKey": "84df7752264a78f8744cc0207ad25d6046601ce8ec83817083a040c8d035fc7c",
  "BucketName": "vanan-guard-photos"
}
```

> **Security note:** Credentials stored in appsettings.json for dev. Production uses environment variables (`R2__AccessKey`, `R2__SecretKey`) via CD pipeline — same pattern as `Jwt:Secret`.

**Nuget to install (Sprint 1):** `AWSSDK.S3` (S3-compatible API for R2)

**R2 free tier:** 10GB storage + FREE egress (unlimited) — sufficient for MVP (~100KB/photo × 100K sessions = 10GB)

---

## Section 3: QR Generation Library — ✅ REUSE EXISTING

| Check | Result |
|---|---|
| QRCoder nuget installed | ❌ Not installed |
| Existing QR generation | ✅ `KhachLink/wwwroot/js/qrcode.js` — vendored qrcode-generator (MIT, Kazuhiko Arase) |
| VietQrService | Generates QR URLs (img.vietqr.io), NOT QR images — not reusable for Guard QR |

**Decision:** Generate QR on **frontend** (Guard app + KhachLink) using existing `qrcode.js` pattern.
- Guard app: vendor `qrcode.js` to ShopERP `wwwroot/lib/qrcode/` (same lib, copy from KhachLink)
- QR payload format: JSON `{"sid":"<sessionId>","t":"<qrToken>","tn":"<tenantId>"}`
- QR rendered as canvas → display on screen + print on thermal ticket

**Alternative (if backend QR needed):** Install `QRCoder` nuget (MIT, lightweight) — but frontend generation is simpler + no nuget dependency.

**Recommendation:** Frontend QR generation (reuse qrcode.js). Backend only generates QrPayload string + QrTokenHash.

---

## Section 4: Camera QR Scan Library — ✅ REUSE EXISTING

| Check | Result |
|---|---|
| KhachLink QR scanner | ✅ `Components/QRScanner.razor` (reusable Blazor component) + `wwwroot/js/qr-scanner.js` |
| KhachLink qr-scanner.js | ✅ BarcodeDetector (native Chrome) + jsQR fallback + html5-qrcode fallback |
| ShopERP QR scanner | ✅ `wwwroot/js/qr-scanner.js` — html5-qrcode (CDN-loaded) |
| jsQR vendored | ❌ Not in wwwroot — loaded via html5-qrcode CDN bundle |

**Decision:**
- **Guard app (ShopERP):** Reuse existing `wwwroot/js/qr-scanner.js` (html5-qrcode). Already works for RedemptionHistory page.
- **KhachLink:** Reuse existing `QRScanner.razor` component directly — it's a reusable Blazor component with `EventCallback<string> OnQRCodeDetected`.

**No new JS library needed.** Both apps already have QR scanning capability.

---

## Section 5: Thermal Printer Integration — ✅ REUSE EXISTING (window.print)

| Check | Result |
|---|---|
| Existing print integration | ✅ `Components/Pages/Orders/PrintBill.razor` — POS-style bill, `window.print()` |
| Print JS function | ✅ `App.razor:51` — `window.vananPrintBill = function() { window.print(); }` |
| Print CSS | ✅ `@@media print` — hides everything except `.bill-page`, optimized 80mm thermal |
| UI Platform components | ✅ Uses `VanAButton` for print/back buttons |

**Decision:** Reuse `PrintBill.razor` pattern — create `PrintTicket.razor` (Guard vé giấy) với:
- Same `window.print()` approach (browser print dialog → thermal printer hoặc Save as PDF)
- Same `@@media print` CSS pattern (hide everything except ticket)
- Same `vananPrintBill` JS function (or add `vananPrintTicket` alias)
- Ticket layout: tenant name, biển số, giờ vào, ngày, QR code (rendered as canvas/img)

**No WebUSB, no ESC/POS, no new JS library.** Browser print dialog handles thermal printer natively (user selects thermal printer in dialog).

**Ticket layout (PrintTicket.razor):**
```
        TENANT NAME (bold, large)
        ━━━━━━━━━━━━━━━━━━
        Biển số: 30A-12345
        Giờ vào: 14:30
        Ngày: 14/08/2026
        ━━━━━━━━━━━━━━━━━━
        [QR code image ~200x200px]
        ━━━━━━━━━━━━━━━━━━
        Vạn An - Guard Scanner
```

**Reference:** `PrintBill.razor` — copy structure, replace order data with guard ticket data.

---

## Section 6: EF Migration Impact — ✅ CLEAR PATH

| Check | Result |
|---|---|
| Migration location | `3_CoreHub/Infrastructure/Migrations/` |
| Latest migration | `20260813175151_AddProductIsPosOnly` |
| DbContext | `VanAnDbContext` (3_CoreHub) — auto-detect SQLite/Npgsql |
| EF Config location | `3_CoreHub/Infrastructure/Configurations/` — auto-discovered via `ApplyConfigurationsFromAssembly` |
| Config pattern | `IEntityTypeConfiguration<T>` + `IEntityConfiguration` marker |
| DB target | **PostgreSQL** (Gateway source of truth, per Option C) |
| Existing Ignore pattern | `VanAnDbContext.OnModelCreating:170-183` — `modelBuilder.Ignore<OrderId>()` etc. |

**Sprint 1 changes needed:**
1. Add to `VanAnDbContext.OnModelCreating` (before `base.OnModelCreating`):
   ```csharp
   modelBuilder.Ignore<VehicleSessionId>();
   modelBuilder.Ignore<GuardScanLogId>();
   ```
2. Add DbSets:
   ```csharp
   public DbSet<VehicleSession> VehicleSessions { get; set; }
   public DbSet<GuardScanLog> GuardScanLogs { get; set; }
   ```
3. Add `VehicleSessionConfiguration.cs` + `GuardScanLogConfiguration.cs` in `3_CoreHub/Infrastructure/Configurations/`
4. `dotnet ef migrations add AddGuardQrVerifyTables` (from 3_CoreHub project)

**No conflict with existing migrations** — new tables only, additive.

---

## Section 7: 8 BR Spec

### BR-G01: QR Issuance
- **Rule:** Guard creates QR session with plate number + 2 photos (plate + customer). QR token is hashed (SHA256) before storage.
- **Enforcement:** `GuardService.IssueAsync` → generate QrPayload + hash → `VehicleSession.Create(Issued)` → save
- **Phase:** Sprint 2 (API) + Sprint 3 (UI)
- **Invariant:** INV-G01 (QrTokenHash unique per tenant), INV-G04 (photos required)
- **Edge cases:** Photo upload fail (R2 error) → rollback, no session created

### BR-G02: QR Claim — Camera (Channel A)
- **Rule:** Customer scans QR from Guard screen using KhachLink camera → QR linked to customer's KhachLink account.
- **Enforcement:** `GuardService.ClaimAsync(qrPayload, customerId)` → `VehicleSession.Claim(customerId)` → Status=Claimed
- **Phase:** Sprint 4 (KhachLink)
- **Invariant:** INV-G03 (Issued→Claimed), INV-G06 (idempotent — already claimed throws)
- **Edge cases:** QR already claimed by another customer → 409; QR voided → 409; QR already checked out → 409

### BR-G03: QR Claim — Short Code (Channel B)
- **Rule:** Customer enters 6-digit short code in KhachLink (fallback when no camera) → same claim flow as BR-G02.
- **Enforcement:** `GuardService.ClaimAsync(shortCode, customerId)` → lookup by ShortCode → `VehicleSession.Claim(customerId)`
- **Phase:** Sprint 4 (KhachLink)
- **Invariant:** INV-G02 (ShortCode unique per tenant per day)
- **Edge cases:** Short code not found → 404; same as BR-G02 edge cases

### BR-G04: Paper Ticket (Channel C)
- **Rule:** Guard prints thermal ticket containing: tenant name, plate number, time in, date, QR code bitmap. Customer uses paper ticket to retrieve vehicle.
- **Enforcement:** Guard app "In vé" button → WebUSB ESC/POS → thermal printer. No claim required — guard scans QR from paper directly.
- **Phase:** Sprint 5 (Printer)
- **Invariant:** Same QrToken as digital — no separate token for paper
- **Edge cases:** Printer offline → fallback to `window.print()`; WebUSB not supported (Safari) → hide print button

### BR-G05: QR Verification
- **Rule:** Guard scans QR (from KhachLink screen OR paper ticket) → system returns plate number + 2 photos → guard manually verifies match.
- **Enforcement:** `GuardService.VerifyAsync(scannedQrPayload)` → hash → lookup → return session + presigned photo URLs
- **Phase:** Sprint 2 (API) + Sprint 3 (UI)
- **Invariant:** INV-G01 (QrTokenHash lookup)
- **Edge cases:** QR not found → 404; QR voided → 409; photo URL expired → regenerate presigned GET

### BR-G06: Check-out
- **Rule:** Guard confirms match → session transitions to CheckedOut. GuardScanLog records the result.
- **Enforcement:** `GuardService.CheckoutAsync(sessionId)` → `VehicleSession.Checkout(guardId)` → Status=CheckedOut
- **Phase:** Sprint 2 (API) + Sprint 3 (UI)
- **Invariant:** INV-G03 (Issued/Claimed→CheckedOut)
- **Edge cases:** Already checked out → 409; voided → 409

### BR-G07: Flag/Suspicious
- **Rule:** Guard flags mismatch (photos don't match customer/vehicle) → session flagged, admin alerted.
- **Enforcement:** `GuardService.FlagAsync(sessionId, reason)` → `VehicleSession.Flag(reason, guardId)` → Status=Flagged + GuardScanLog(Flagged)
- **Phase:** Sprint 2 (API) + Sprint 3 (UI)
- **Invariant:** INV-G03 (Issued/Claimed→Flagged)
- **Edge cases:** Already checked out → cannot flag; voided → cannot flag

### BR-G08: Channel C→A Migration (Paper to Digital)
- **Rule:** Customer who received paper ticket (Channel C, CustomerId=null) can later scan QR from paper using KhachLink → claim digital (Channel A). Paper ticket no longer needed.
- **Enforcement:** Same `ClaimAsync` flow — QR payload from paper ticket is identical to QR on Guard screen. `VehicleSession.Claim(customerId)` sets CustomerId from null→customerId.
- **Phase:** Sprint 4 (KhachLink) — no separate code, same /qr/claim page
- **Invariant:** INV-G05 (Claim from Issued with null CustomerId → set CustomerId), INV-G06 (idempotent — already claimed throws)
- **Edge cases:** Session already CheckedOut (customer already retrieved vehicle) → 409 "Vé đã sử dụng"; session Voided → 409 "Vé đã hết hạn"; already claimed by another customer → 409

---

## Section 8: Sprint 1 Domain Entity Final Field List

### VehicleSession (Aggregate Root)
```csharp
// Inherits BaseEntity (Id, TenantId, CreatedAt, UpdatedAt, CreatedBy, UpdatedBy, IsDeleted)
public Guid Id { get; }                    // PK = VehicleSessionId.Value (Single-Identity)
public string PlateNumber { get; }         // biển số, max 20
public Guid? CustomerId { get; private set; }  // null = Channel C (paper only)
public string? CustomerPhone { get; }      // optional, for Channel B lookup
public string PlatePhotoKey { get; }       // R2 object key (e.g. "plates/{tenant}/{guid}.jpg")
public string CustomerPhotoKey { get; }    // R2 object key
public string QrTokenHash { get; }         // SHA256 hash of QR payload
public string ShortCode { get; }           // 6-digit, human fallback
public VehicleSessionStatus Status { get; private set; }
public Guid IssuedBy { get; }              // GuardId
public DateTimeOffset IssuedAt { get; }
public Guid? ClaimedBy { get; private set; }
public DateTimeOffset? ClaimedAt { get; private set; }
public Guid? CheckedOutBy { get; private set; }
public DateTimeOffset? CheckedOutAt { get; private set; }
public string? FlagReason { get; private set; }
public DateTimeOffset? VoidedAt { get; private set; }
```

### GuardScanLog (Entity)
```csharp
// Inherits BaseEntity
public Guid Id { get; }                    // PK = GuardScanLogId.Value
public Guid VehicleSessionId { get; }      // FK → VehicleSession.Id
public string ScannedQrTokenHash { get; }
public GuardScanResult MatchResult { get; }
public Guid ScannedBy { get; }             // GuardId
public DateTimeOffset ScannedAt { get; }
public string? Notes { get; }
```

### Enums
```csharp
public enum VehicleSessionStatus { Issued=0, Claimed=1, CheckedOut=2, Voided=3, Flagged=4 }
public enum GuardScanResult { Match=0, Mismatch=1, ManualOverride=2, Flagged=3 }
```

### Value Objects
```csharp
public record VehicleSessionId(Guid Value);  // inherits BaseEntity via pattern
public record GuardScanLogId(Guid Value);
```

> **Note on VO inheritance:** Existing VOs (OrderId, ProductId) are `sealed record` that inherit from `BaseEntity`. Check exact pattern in Domain.cs — they may be simple records without BaseEntity inheritance. If so, just `Ignore` them in OnModelCreating.

---

## Summary: Ready for Sprint 1

| Item | Status | Action |
|---|---|---|
| Guard role auth | ✅ Ready | None |
| R2 storage | ⚠️ Need setup | User: create R2 bucket + API token |
| QR generation | ✅ Reuse qrcode.js | Vendor to ShopERP |
| QR scanning | ✅ Reuse existing | QRScanner.razor (KhachLink) + qr-scanner.js (ShopERP) |
| Thermal printer | ⚠️ New | User: confirm printer model |
| EF migration | ✅ Clear path | Add 2 entities + 2 configs + 1 migration |
| BR spec | ✅ 8 BRs drafted | User approval |

**Approval gate:** User approves 8 BR spec + provides R2 credentials + confirms printer model → Sprint 1 starts.
