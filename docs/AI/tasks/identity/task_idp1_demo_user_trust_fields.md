# Task ID-P1: Trust Model Core — DemoUser Contact/Trust Fields (Gate 5)

> **Master plan:** `docs/AI/plans/van-an-identity-tiered-trust-master-plan.md` §4 ID-P1 · **Audit:** `docs/AI/plans/identity-inventory-p0.md`
> **Mode:** ANALYZE → IMPLEMENT (card ĐÃ ĐỐI SOÁT codebase 2026-11-10 — **✅ ĐƯỢC DUYỆT 2026-11-10** + bổ sung 2 điểm review: `VerifiedChannel` provenance + downgrade hook khi tenant deactivate — Gate 5 Domain change đã duyệt)
> **Prerequisite:** Q1 đã chốt — 2 hệ song song (Customer ≠ DemoUser); KHÔNG unify
> **Layer:** Domain (Gate 5!) → Infrastructure/EF → Services → Sync → Tests

## 0. CONTEXT CHO SESSION MỚI (đọc trước khi code)

- `DemoUser` = credential + RBAC anchor cho tenant staff/owner. Login path: `ShopERP/Pages/Login.cshtml.cs` đọc SQLite `Users` → BCrypt → JWT+cookie. **P1 không đổi gì login.**
- `DemoUserConfiguration` apply qua `ApplyConfigurationsFromAssembly` trong CẢ `VanAnDbContext` (PG) LẪN `ShopERPDbContext` (SQLite) → sửa 1 config ăn 2 DB; vẫn cần **2 migration files riêng** (PG `3_CoreHub/Infrastructure/Migrations/` + SQLite `5_WebApps/ShopERP/Migrations/`).
- Owner tạo trong `TenantOnboardingService.VerifyAsync` step 5 qua `userService.CreateUserAsync` (**tự SaveChanges**) → populate trust fields SAU đó, step 8 `SaveChangesAsync` cuối đã cover.
- `VerifyAsync` nhận `VerifyTenantRequest` — hiện KHÔNG có field trust/OTP → phải thêm optional param (DTO change, không phải Domain).
- `SelfServePayload` (P0, ĐÃ DEPLOY) chỉ có: `OtpVerified` (bool), `ContactPhone`, `ContactEmail` — **CHƯA có** `OtpChannel`/`OtpVerifiedContact`/`OtpVerifiedAt` (đó là đề xuất P4 chưa implement). P1 dùng fields hiện có.
- `DemoUser.Email` có `private set`, KHÔNG có method set → cần domain method mới.
- `UserSyncSubscriber` (ShopERP) đọc `UserUpsertedEvent` payload bằng `TryGetProperty` → thêm field optional = backward compat thật.
- `EncryptedStringConverter` + `DataProtectionProviderAccessor` đã dùng cho `Email` trong cùng config file → phone làm y hệt.
- Staff user tạo từ ShopERP chỉ tồn tại SQLite (không sync ngược PG) → trust fields chỉ populate cho owner qua onboarding — BY DESIGN.

## 1. OBJECTIVE

Biến `DemoUser` thành "hồ sơ định danh có cấu trúc": `PhoneNumber` (encrypted PII) + `PhoneVerifiedAt` + `VerifiedChannel` + `TrustLevel` — populate khi tenant verify. Nền cho ID-P2 (OAuth) → ID-P5 (enforcement) → IdP vision.

**Bổ sung review 2026-11-10 (đã duyệt):**
- **`VerifiedChannel`** (string? — `email`/`zns`/`sms`/`otp`/`manual`) — provenance tối thiểu: owner đã verify liên hệ qua kênh nào (REVIEW B7). Rẻ — 1 cột, không index.
- **Downgrade hook khi tenant deactivate** (REVIEW B3): `RevokeBusinessVerification()` — tenant mất hiệu lực (reject/deactivate/timeout) → owner `BusinessVerified` → `ContactVerified` (KHÔNG xuống `None` — giữ lịch sử liên hệ đã verify).

## 2. FILES

| File | Change |
|---|---|
| `1_Shared/Domain/Aggregates/UserAggregate/DemoUser.cs` | += `PhoneNumber?`, `PhoneVerifiedAt?`, `VerifiedChannel?`, `TrustLevel` + domain methods (gồm `RevokeBusinessVerification`) |
| `1_Shared/Domain/Aggregates/UserAggregate/UserTrustLevel.cs` | **File MỚI** — `enum UserTrustLevel { None=0, ContactVerified=1, BusinessVerified=2 }` (convention: file riêng như `UserRole.cs`/`UserTenant.cs`) |
| `1_Shared/Domain.cs` (`Customer.IdentityLevel`, ~line 815) | Thêm `TrustRank` mapping (KHÔNG renumber — int đã persist) |
| `3_CoreHub/Infrastructure/Configurations/DemoUserConfiguration.cs` | Map props; `PhoneNumber` += `EncryptedStringConverter` protector `DemoUser.PhoneNumber` |
| `3_CoreHub/Infrastructure/Migrations/` | PG migration `AddDemoUserTrustFields` |
| `5_WebApps/ShopERP/Migrations/` | SQLite migration tương ứng |
| `3_CoreHub/Services/Onboarding/ITenantOnboardingService.cs` + `TenantOnboardingService.cs` | `VerifyTenantRequest` += optional trust hints; `VerifyAsync` populate owner; `PublishUserUpsertedEventAsync` payload += `TrustLevel` |
| `3_CoreHub/Services/Onboarding/MerchantSignupService.cs` | `VerifySignupAsync` truyền trust hint từ `SelfServePayload` hiện có |
| `5_WebApps/ShopERP/Services/UserSyncSubscriber.cs` | Parse + apply `TrustLevel` (optional, backward compat) |
| `6_Tests/VanAn.Core.Tests` | Domain + service tests (mục 4) |
| `6_Tests/VanAn.ShopERP.Tests` | Subscriber sync tests nếu có infra |

**KHÔNG làm (defer):** `LinkedIdentities`/external ids (ID-P2) · `EmailVerifiedAt` · `RiskScore` · JWT `trust_level` claim (ID-P5) · sync raw `PhoneNumber` xuống SQLite · blind-index phone search · staff populate · backfill user cũ.

## 3. CHANGES CHI TIẾT

### 3.1 Domain — `UserTrustLevel.cs` (file mới)

```csharp
namespace VanAn.Shared.Domain.Aggregates.UserAggregate;

/// <summary>ID-P1 (2026-11-10): trust tier cho DemoUser — L0-L3 rút gọn phía owner.
/// None=chưa verify liên hệ · ContactVerified=L2 (SĐT/email đã OTP-verify) ·
/// BusinessVerified=L3 (tenant đã sysadmin duyệt — chủ thể kinh doanh thật).</summary>
public enum UserTrustLevel { None = 0, ContactVerified = 1, BusinessVerified = 2 }
```

### 3.2 Domain — `DemoUser` additions

```csharp
public string? PhoneNumber { get; private set; }        // plaintext trong domain; EF encrypt at rest
public DateTime? PhoneVerifiedAt { get; private set; }
public string? VerifiedChannel { get; private set; }    // provenance: "email" | "zns" | "sms" | "otp" | "manual" — REVIEW B7
public UserTrustLevel TrustLevel { get; private set; } = UserTrustLevel.None;
```

Domain methods:

- `SetContactInfo(string? phone, string? email)` — **mới** (Email hiện không có setter): normalize phone digits (`+84/84`→`0`, reuse logic pattern `MerchantSignupService.NormalizePhone`); set `PhoneNumber` + `Email`; **nếu phone thay đổi → reset `PhoneVerifiedAt=null` + `VerifiedChannel=null`**; email set chỉ khi khác null (không clear email hiện có bằng null).
- `MarkPhoneVerified(string? channel = null)` — guard `PhoneNumber != null` (null → `InvalidOperationException`); set `PhoneVerifiedAt=UtcNow`; `VerifiedChannel = channel` (null → giữ cũ); auto-nâng `TrustLevel` lên ít nhất `ContactVerified` (**không bao giờ downgrade**).
- `MarkBusinessVerified()` — `TrustLevel = BusinessVerified`; idempotent.
- `RevokeBusinessVerification()` — **REVIEW B3 (2026-11-10):** nếu `TrustLevel == BusinessVerified` → `TrustLevel = ContactVerified` (KHÔNG xuống None — liên hệ vẫn đã verify); nếu đang `None`/`ContactVerified` → no-op. Gọi khi tenant→inactive.
- `ApplyTrustLevel(UserTrustLevel level)` — cho sync subscriber: **chỉ cho tăng hoặc giữ** (level < current → no-op + log? domain không log → silently ignore, test assert).
- `DemoUser.Create` ctor signature GIỮ NGUYÊN — fields mới default.

### 3.3 EF Config (`DemoUserConfiguration`)

```csharp
builder.Property(e => e.PhoneNumber).HasMaxLength(500)
    .HasConversion(new EncryptedStringConverter(
        DataProtectionProviderAccessor.CreateProtector("DemoUser.PhoneNumber")));
builder.Property(e => e.TrustLevel).HasConversion<int>();
builder.Property(e => e.VerifiedChannel).HasMaxLength(20);   // provenance — plain string, KHÔNG index (không search nhu cầu)
```

- **KHÔNG index PhoneNumber** — EncryptedStringConverter random IV → ciphertext khác nhau mỗi encrypt → index/search vô dụng. Ghi nợ blind-index.
- `PhoneVerifiedAt`/`TrustLevel`/`VerifiedChannel` index optional (để trống, thêm sau nếu cần).
- `VerifiedChannel` **KHÔNG sync xuống SQLite mirror** (giống PhoneNumber — PG-only provenance; SQLite chỉ nhận `TrustLevel` int qua event).

### 3.4 `VerifyTenantRequest` + populate

`VerifyTenantRequest` (DTO trong `ITenantOnboardingService.cs` hoặc file DTO riêng — check vị trí thật) += **optional trailing params** (backward compat):

```csharp
bool ContactVerified = false,          // contact (phone/email) đã qua kênh verify
string? VerifiedChannel = null,        // REVIEW B7: "email"|"zns"|"sms"|"otp"|"manual" — null → fallback "otp"
UserTrustLevel? TrustHint = null       // override trust đích; null = suy ra từ ContactVerified + tenant Active
```

`VerifyAsync` sau step 5 (`CreateUserAsync`):

```csharp
if (!string.IsNullOrWhiteSpace(req.OwnerPhone) || !string.IsNullOrWhiteSpace(req.OwnerEmail))
    ownerUser.SetContactInfo(req.OwnerPhone, req.OwnerEmail);
if (req.ContactVerified) ownerUser.MarkPhoneVerified(req.VerifiedChannel ?? "otp");   // chỉ khi phone là contact đã verify
ownerUser.MarkBusinessVerified();                          // tenant Active = L3 — luôn gọi
```

Lưu ý: `MarkBusinessVerified` luôn gọi (tenant verify thành công = business verified); `ContactVerified` chỉ khi caller xác nhận contact đã OTP-verify.

### 3.5 `MerchantSignupService.VerifySignupAsync`

- Truyền `ContactVerified: selfServe.OtpVerified` vào `VerifyTenantRequest` (fields hiện có — `OtpVerified` bool đã deploy).
- `VerifiedChannel: selfServe.OtpChannel ?? "otp"` — P4 chưa có `OtpChannel` trong `SelfServePayload` → fallback `"otp"` generic; khi P4 implement xong tự refine kênh thật.
- `OwnerPhone` đã truyền `selfServe.ContactPhone` sẵn → `SetContactInfo` tự chạy trong `VerifyAsync`.
- **P4 tương lai:** khi `SelfServePayload` có `OtpChannel`/`OtpVerifiedContact`/`OtpVerifiedAt` → refine `ContactVerified` theo kênh (email-verify ≠ phone-verify) — ghi nợ trong card P4, KHÔNG làm ở đây.

### 3.5a Downgrade hook — tenant deactivate → owner trust (REVIEW B3, đã duyệt)

**Semantics:** tenant mất hiệu lực (reject/deactivate/timeout) = chủ thể kinh doanh hết hiệu lực L3 → owner `TrustLevel` từ `BusinessVerified` → `ContactVerified`. Không xuống `None` (liên hệ đã verify vẫn còn giá trị).

**Helper (CoreHub):**
```csharp
// Trong service layer — tìm owner user của tenant rồi revoke:
public static async Task RevokeOwnerBusinessTrustAsync(
    VanAnDbContext db, Guid tenantId, CancellationToken ct)
{
    var ownerUserId = await db.UserTenants.IgnoreQueryFilters()
        .Where(ut => ut.TenantId == tenantId && ut.Role == UserRole.Owner)
        .Select(ut => (Guid?)ut.UserId).FirstOrDefaultAsync(ct);
    if (ownerUserId is null) return;
    var owner = await db.Users.IgnoreQueryFilters()
        .FirstOrDefaultAsync(u => u.Id == ownerUserId, ct);
    owner?.RevokeBusinessVerification();          // domain method — no-op nếu không phải BusinessVerified
    await db.SaveChangesAsync(ct);                // gọi bởi caller (pattern fail-safe)
}
```

**Call sites bắt buộc (grep + wire ALL — mọi path tenant→inactive):**
- [ ] `MerchantSignupService.RejectPendingAsync` — sau `tenant.Deactivate(...)` (verified line ~178)
- [ ] `TenantOnboardingService` — các path admin deactivate tenant (grep `Deactivate(` trong CoreHub)
- [ ] (Tương lai) AF1 §3.5 cleanup job Pending >30d — ghi nợ, thêm khi implement
- [ ] **KHÔNG** áp dụng cho staff users — chỉ owner (staff không có `BusinessVerified`).

**Lưu ý:** `RevokeBusinessVerification` KHÔNG publish `UserUpsertedEvent` tự động — nếu cần sync downgrade xuống SQLite mirror, caller publish event riêng (quyết định lúc implement: SQLite chỉ lưu TrustLevel → sync `TrustLevel=1` qua event).

### 3.6 `PublishUserUpsertedEventAsync` payload

```csharp
// += field (KHÔNG thêm PhoneNumber/PhoneVerifiedAt — PII, giữ PG-only)
TrustLevel = (int)ownerUser.TrustLevel
```

`UserSyncSubscriber.SyncUserUpsertedAsync`: `root.TryGetProperty("TrustLevel", out var tl)` → `user.ApplyTrustLevel((UserTrustLevel)tl.GetInt32())` trước `SaveChangesAsync`. Payload cũ không có field → skip, default `None`.

### 3.7 `Customer.IdentityLevel` TrustRank

Trong `Domain.cs` gần enum `IdentityLevel` (~line 815) thêm:

```csharp
public static class IdentityLevelRank
{
    // DeviceVerified=4 > Full=3 phá ordering so sánh enum trực tiếp —
    // rank map: Guest=0, Social=1, DeviceVerified=1, Verified=2, Full=3
    public static int Rank(this IdentityLevel level) => level switch
    {
        IdentityLevel.Guest => 0,
        IdentityLevel.Social or IdentityLevel.DeviceVerified => 1,
        IdentityLevel.Verified => 2,
        IdentityLevel.Full => 3,
        _ => 0
    };
}
```

`Customer.UpgradeIdentityLevel`: đổi `if (newLevel <= IdentityLevel)` → `if (newLevel.Rank() <= IdentityLevel.Rank())`.

## 4. TEST CASES (TDD — viết trước)

### 4.1 Domain — `DemoUserTrustFieldsTests` (VanAn.Core.Tests/Domain/)

- [ ] `Create` → `TrustLevel=None`, `PhoneNumber=null`, `PhoneVerifiedAt=null`
- [ ] `SetContactInfo("+84 901 234 567", null)` → `PhoneNumber="0901234567"`; `Email` giữ nguyên
- [ ] `SetContactInfo` đổi SĐT khác → `PhoneVerifiedAt` reset null
- [ ] `SetContactInfo(null, "a@b.com")` → phone giữ nguyên, `Email="a@b.com"` (không clear phone)
- [ ] `MarkPhoneVerified` khi `PhoneNumber=null` → `InvalidOperationException`
- [ ] `MarkPhoneVerified` → `PhoneVerifiedAt` set + `TrustLevel=ContactVerified`
- [ ] `MarkPhoneVerified("email")` → `VerifiedChannel="email"`; `MarkPhoneVerified(null)` giữ channel cũ
- [ ] `SetContactInfo` đổi SĐT → `PhoneVerifiedAt` reset null **và `VerifiedChannel` reset null**
- [ ] `RevokeBusinessVerification` từ `BusinessVerified` → `ContactVerified` (KHÔNG None)
- [ ] `RevokeBusinessVerification` khi đang `ContactVerified`/`None` → no-op, level giữ nguyên
- [ ] `RevokeBusinessVerification` rồi `MarkBusinessVerified` lại → lên lại được (idempotent, no-downgrade guard không chặn nâng)
- [ ] `MarkBusinessVerified` → `TrustLevel=BusinessVerified`; gọi lại idempotent
- [ ] `MarkPhoneVerified` khi `BusinessVerified` → vẫn `BusinessVerified` (no downgrade)
- [ ] `ApplyTrustLevel(ContactVerified)` khi `BusinessVerified` → giữ `BusinessVerified`
- [ ] `ApplyTrustLevel(BusinessVerified)` → set được
- [ ] Backward compat: `new DemoUser(tenantId, username, hash, name, role)` compile + defaults đúng

### 4.2 `IdentityLevelRank` tests

- [ ] `Social.Rank()=1`, `DeviceVerified.Rank()=1`, `Verified.Rank()=2`, `Full.Rank()=3`
- [ ] Customer `Social → UpgradeIdentityLevel(DeviceVerified)` → allowed (cùng rank? — spec: rank <= current → no-op; DeviceVerified rank=1 = Social rank=1 → NO-OP → test assert level giữ Social hoặc theo spec chốt: **same-rank upgrade allowed?** → QUYẾT: same-rank = no-op để tránh ghi DB thừa)
- [ ] `DeviceVerified → UpgradeIdentityLevel(Verified)` → upgrade được (2>1)
- [ ] `Full → UpgradeIdentityLevel(DeviceVerified)` → không downgrade

### 4.3 Service — `TenantOnboardingServiceTests` / `MerchantSignupServiceTests`

- [ ] `VerifyAsync` `OwnerPhone="0901..."` → `ownerUser.PhoneNumber` set (decrypt qua EF read), `TrustLevel=BusinessVerified`
- [ ] `VerifyAsync` `ContactVerified=true` + có phone → `PhoneVerifiedAt != null`
- [ ] `VerifyAsync` `ContactVerified=false` → `PhoneVerifiedAt=null` nhưng `TrustLevel=BusinessVerified`
- [ ] `VerifyAsync` không phone/email → `PhoneNumber=null`, `TrustLevel=BusinessVerified`
- [ ] `VerifyAsync` `ContactVerified=true` + `VerifiedChannel="email"` → `PhoneVerifiedAt != null` + `VerifiedChannel="email"`
- [ ] `VerifySignupAsync` `SelfServePayload.OtpVerified=true` → owner `PhoneVerifiedAt` set + `VerifiedChannel` (fallback `"otp"` khi payload chưa có `OtpChannel`)
- [ ] `VerifySignupAsync` `OtpVerified=false` → `PhoneVerifiedAt=null`
- [ ] **`RejectPendingAsync` → owner user của tenant bị downgrade `BusinessVerified`→`ContactVerified`** (REVIEW B3 — tìm owner qua `UserTenant.Role==Owner`)
- [ ] `RejectPendingAsync` khi owner không có `BusinessVerified` → no-op, không crash
- [ ] Regression: `UserUpsertedEvent` vẫn enqueue; payload chứa `TrustLevel`
- [ ] Regression: toàn bộ test onboarding/merchant-signup hiện có PASS

### 4.4 EF/Persistence

- [ ] PG: save `DemoUser.PhoneNumber` → raw column KHÔNG chứa plaintext; đọc lại decrypt đúng
- [ ] Protector `DemoUser.PhoneNumber` ≠ `DemoUser.Email` (không cross-decrypt — test encrypt phone, thử decrypt bằng email protector → fail)
- [ ] SQLite migration apply OK; `TrustLevel` persist int

### 4.5 Sync — `UserSyncSubscriber`

- [ ] Payload `TrustLevel=2` → SQLite user `BusinessVerified`
- [ ] Payload cũ không `TrustLevel` → `None`, không crash
- [ ] Payload KHÔNG sync `PhoneNumber` → SQLite phone null
- [ ] Redelivery idempotent

### 4.6 Regression tổng

- [ ] Login user cũ (fields null) bình thường — `Login.cshtml.cs` không đổi
- [ ] `UserManagementService.CreateUserAsync` staff → `TrustLevel=None`, phone null
- [ ] `VanAn.Core.Tests` + `VanAn.ShopERP.Tests` ALL PASS

## 5. ACCEPTANCE CRITERIA

- [ ] `dotnet build VanAn.sln` 0 errors + `.\scripts\guard-check.ps1` PASS
- [ ] Tất cả test 4.1-4.6 PASS (gồm VerifiedChannel + RevokeBusinessVerification + RejectPendingAsync downgrade)
- [ ] Migration PG + SQLite apply sạch (additive only — null/default columns)
- [ ] Dev verify: tạo tenant → owner có phone encrypted + `BusinessVerified` + `VerifiedChannel`; login OK
- [ ] Reject tenant (Pending) → owner trust xuống `ContactVerified` (query PG verify)
- [ ] Deploy order an toàn 2 chiều (subscriber cũ/mới đều chạy được)
- [ ] **Gate 5 checklist:** change trong approved plan ✓ · AccountingEntry không đụng ✓ · Domain pure (DemoUser.cs không using EF) ✓ · `UserTrustLevel.cs` cùng assembly Domain ✓

## 5.5 VERIFICATION + RV (bắt buộc — user: "verify được khi chạy rv test")

**Local (trước push):**
```powershell
dotnet build VanAn.sln
.\scripts\guard-check.ps1
dotnet test 6_Tests/VanAn.Core.Tests --filter "FullyQualifiedName~DemoUser|TrustLevel|IdentityLevel|Onboarding|MerchantSignup"
dotnet test 6_Tests/VanAn.ShopERP.Tests --filter "FullyQualifiedName~UserSync"
# EF migration: kiểm tra 2 file migration generated đúng cả PG + SQLite
```

**RV production (sau deploy — sweep-first, STOP ở fail đầu):**
- [ ] **L1 markers:** `RevokeBusinessVerification`/`VerifiedChannel` trong `CoreHub.dll` image mới · migration `AddDemoUserTrustFields` (PG + SQLite) applied
- [ ] **L2:** health 200 · route login không đổi (302)
- [ ] **L3 Playwright prod** (pattern `playwright-rv-*.config.ts` + storageState): tạo tenant test → owner `BusinessVerified` + `VerifiedChannel` trong PG (query qua script) · reject tenant → owner `ContactVerified` · login owner vẫn OK (regression)
- [ ] **L4 PG check:** `DemoUser.PhoneNumber` raw column KHÔNG plaintext (encrypted) · `TrustLevel` int đúng
- [ ] Cleanup: xóa tenant/user test · ghi kết quả vào project_state

## 7. RISKS / NOTES

- **EncryptedStringConverter random IV** → phone không SQL-search được — ghi nợ blind-index (fraud-check phone lookup quy mô lớn mới cần).
- **`VerifyTenantRequest` thêm optional params** → mọi call site hiện có compile không đổi (default values); grep call sites trước khi sửa: `TenantOnboardingController`, `PendingTenantController`, `MerchantSignupService`, tests.
- **`MarkBusinessVerified` luôn gọi trong VerifyAsync** → mọi tenant Active đều owner=BusinessVerified. Đúng semantics L3 (sysadmin đã duyệt). Nếu sau này cần owner chưa L3 → thêm flag riêng.
- **PII:** raw phone KHÔNG vào NATS payload/log — chỉ `TrustLevel` int. `VerifiedChannel` cũng PG-only, không sync SQLite.
- **Downgrade hook (REVIEW B3):** chỉ chạy khi tenant→inactive; `RevokeBusinessVerification` không bao giờ xuống `None` (liên hệ vẫn verify). Nếu sau này cần audit "ai revoke lúc nào" → thêm `TrustLevelChangedAt`/audit entry (defer, ghi nợ).
- **`RejectPendingAsync` downgrade cần load owner qua `UserTenant.Role==Owner`** — nếu tenant chưa có owner user (reject trước verify) → helper no-op an toàn (đã test).
- **`IdentityLevelRank` same-rank no-op** có thể khiến `DeviceVerified` không set được trên Customer đang Social — chấp nhận (cùng trust tier thực tế); nếu cần set flag riêng thì đó là DeviceId flow khác, ngoài scope.
- **Backfill user cũ:** defer — quyết sau (có thể `TrustLevel=BusinessVerified` cho owner hiện tại vì tenant của họ đã Active).
