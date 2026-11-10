# Task MFA-1: Admin MFA — TOTP + Trusted Device + Email OTP Fallback (Gate 5)

> **Master plan:** `docs/AI/plans/van-an-identity-tiered-trust-master-plan.md` §4 MFA-1 · **REVIEW:** findings B2 (REVIEW_ONLY 2026-11-10 — verified: DemoUser password-only bcrypt, admin = cửa ngõ L3 verify)
> **Mode:** ANALYZE → IMPLEMENT — **✅ ĐƯỢC DUYỆT 2026-11-10** (đề xuất #4 trong 4 đề xuất review; ưu tiên TRƯỚC ID-P2)
> **Nguyên tắc zero-cost:** TOTP (authenticator app) = 0đ · email OTP fallback qua `BrevoEmailService` (free 300 mail/ngày) — KHÔNG cần SMS/ZNS trả phí
> **Gate 5:** `DemoUser` += MFA fields = Domain modeling change trên UserAggregate (KHÔNG đụng `IdentityLevel`/`TrustLevel`/L0-L3 model) — đã duyệt trong quyết định MFA-1

## 0. CONTEXT — VÌ SAO PHẢI LÀM (threat model)

Admin (sysadmin/owner) là **cửa ngõ duy nhất của L3** (`VerifyAsync` duyệt tenant, approve/reject, đổi Type HTX↔Công ty, /admin/metering/*, /admin/tenant-ai/*, loyalty reconcile). Password-only bcrypt → credential stuffing/phishing 1 lần = chiếm toàn platform:
- **Đầu độc trust registry** — verify tenant giả → chủ thể kinh doanh "hợp pháp" ảo → e-invoice Viettel thật + sổ kế toán giả = rửa tiền qua HĐĐT (trách nhiệm hình sự).
- Toàn bộ PII mọi tenant (owner SĐT/email, doanh thu, khách hàng) — vi phạm NĐ 13/2023.
- Thao túng ví metering/topup, rate cards, loyalty.

**Trớ trêu:** hệ xây moat L3 (thứ phân biệt Vạn An với Google/Zalo) mà cổng duyệt đó chỉ có 1 mật khẩu. MFA-1 rẻ hơn mọi feature consumer và chặn hậu quả lớn hơn nhiều.

## 1. OBJECTIVE

Bật MFA cho admin: **TOTP (authenticator)** bắt buộc với `SystemAdmin`/`Owner`, tùy chọn staff · **trusted-device cookie** (30 ngày, signed) để không hỏi lại mỗi lần login · **email OTP fallback** (Brevo) khi mất authenticator · audit log MFA events · KHÔNG đổi JWT/API contract login hiện có ngoài challenge step.

## 2. FILES

| File | Change |
|---|---|
| `1_Shared/Domain/Aggregates/UserAggregate/DemoUser.cs` | += `MfaEnabled` (bool), `MfaSecret` (string?, encrypted) + domain methods `EnableMfa`/`DisableMfa` |
| `3_CoreHub/Infrastructure/Configurations/DemoUserConfiguration.cs` | Map `MfaSecret` += `EncryptedStringConverter` protector `DemoUser.MfaSecret` · `MfaEnabled` bool |
| `3_CoreHub/Infrastructure/Migrations/` + `5_WebApps/ShopERP/Migrations/` | Migration `AddDemoUserMfa` (PG + SQLite, additive nullable) |
| `3_CoreHub/Services/` — service mới `MfaService` (+ `IMfaService`) | TOTP generate/verify (OtpNet) · trusted-device issue/validate/revoke · attempt lock · email OTP fallback |
| `5_WebApps/ShopERP/Pages/Login.cshtml.cs` + `Login.razor` | Password OK + `MfaEnabled` → step 2 nhập TOTP (hoặc nút "gửi mã qua email") |
| `5_WebApps/ShopERP/Pages/Account/MfaSettings.razor` (mới) + NavMenu | Setup: QR + secret + nhập code xác nhận → enable · disable (yêu cầu nhập lại password) |
| `2_Gateway/Program.cs` hoặc `appsettings` | Config `Mfa:EnforcedRoles` (default `SystemAdmin,Owner`) · `Mfa:TrustedDeviceDays` (30) · `Mfa:MaxAttempts` (5) · `Mfa:LockoutMinutes` (15) |
| `5_WebApps/ShopERP/Program.cs` | DI `IMfaService` |
| `6_Tests/VanAn.Core.Tests` + `VanAn.ShopERP.Tests` | Domain + service + login flow tests (§4) |
| `6_Testing/e2e-tests/` | Playwright spec MFA setup + login (local) — RV prod spec pattern `playwright-rv-*.config.ts` (§5.5) |

**KHÔNG làm (defer):** recovery backup codes · multi trusted-device (1 hash column MVP) · MFA cho customer login · WebAuthn/passkey (ID-P7) · Gateway-side MFA endpoint riêng.

## 3. CHANGES CHI TIẾT

### 3.1 Domain — `DemoUser` MFA fields

```csharp
public bool MfaEnabled { get; private set; }
public string? MfaSecret { get; private set; }   // encrypted at rest (converter như PhoneNumber)
```

- `EnableMfa(string secret)` — guard: secret không rỗng + chưa enable → set + `UpdateAudit()`.
- `DisableMfa()` — `MfaEnabled=false; MfaSecret=null`; idempotent.
- Ctor/`Create` GIỮ NGUYÊN — fields default (`false`/`null`).
- EF: `MfaSecret` += `EncryptedStringConverter(DataProtectionProviderAccessor.CreateProtector("DemoUser.MfaSecret"))` — protector KHÁC `DemoUser.PhoneNumber`/`DemoUser.Email` (test cross-decrypt fail).
- **KHÔNG index `MfaSecret`** (random ciphertext).

### 3.2 `MfaService` (CoreHub — DI cả ShopERP)

Dependency: **`OtpNet`** (NuGet, MIT, stable 1.4.0) — `dotnet add package OtpNet` vào CoreHub + ShopERP (hoặc CPM nếu pattern repo dùng).

```csharp
public interface IMfaService
{
    MfaSetupResult GenerateSetup(string userId);                        // secret + otpauth:// URI + QR payload
    Task<bool> EnableAsync(string userId, string code);                 // verify code trước khi lưu secret
    Task DisableAsync(string userId, string password, string hashed);   // require re-auth password
    Task<MfaVerifyResult> VerifyLoginAsync(string userId, string? code, string? trustedToken);
    string IssueTrustedToken(string userId);                            // random 128-bit -> cookie value
    Task<bool> IsTrustedAsync(string userId, string? trustedToken);
    Task RevokeTrustAsync(string userId);
    Task<string?> SendEmailFallbackOtpAsync(string userId);             // Brevo -> DemoUser.Email, purpose "mfa-recovery"
}
```

- **GenerateSetup:** `KeyGeneration.GenerateRandomKey(20)` → base32 secret → `otpauth://totp/VanAn:{username}?secret=...&issuer=VanAn` → QR data (reuse pattern QR component đã có trong booking/QR pages — verify trước).
- **VerifyLoginAsync:** flow — token truyền vào:
  1. `IsTrustedAsync` → trusted cookie hợp lệ → success (skip MFA).
  2. Ngược lại nếu `MfaEnabled`: verify TOTP code — `Totp.Verify(code, secret, verificationWindow: 2)` (chấp nhận ±1 window 30s).
  3. Sai ≥ `Mfa:MaxAttempts` (5) trong 15' → lock `mfa-lock:{userId}` (IMemoryCache) → `MfaVerifyResult.Locked`.
  4. Đúng → xóa attempt counter → `Success` (+ `IssueTrustedToken` để set cookie).
- **Email fallback:** nếu `DemoUser.Email != null` → `BrevoEmailService.SendEmailAsync` code 6 số (`RandomNumberGenerator`) TTL 10' single-use (cache `mfa-email-otp:{userId}`), purpose `mfa-recovery` — **dùng `BrevoEmailService` TRỰC TIẾP** (đã có sẵn + tests) — KHÔNG chờ P4 channel abstraction (tránh dependency chéo; khi P4 xong có thể đổi sang `IIdentityOtpChannel` — ghi nợ).
- **Trusted device:** cookie `va_mfa_trust` = `{userId}.{token}` signed (HMAC với `Jwt__Secret` — pattern cookie sign hiện có) · server lưu **SHA-256 hash** token vào cột `TrustedDeviceHash` trên `DemoUser` (1 device MVP — ghi nợ multi-device) · expiry `Mfa:TrustedDeviceDays` (30) — set trên cookie.
- **Audit:** mọi enable/disable/verify/fail/lock log qua pattern audit IP/UserAgent có sẵn (W4 pháp lý) — reason: `mfa.enable|disable|login.success|login.fail|lockout|email-fallback.sent`.

### 3.3 Login flow (ShopERP `Login.cshtml.cs`)

1. **Bước 1 (không đổi):** username+password → bcrypt verify → nếu fail: như cũ.
2. Password OK → `user.MfaEnabled && role ∈ Mfa:EnforcedRoles` (SystemAdmin, Owner) → **KHÔNG login ngay** — render step 2 (TOTP input / nút "Gửi mã qua email") với `pendingLogin` (userId tạm, session-limited, TTL 5').
3. **Bước 2:** `VerifyLoginAsync` → success → tạo JWT + cookie (như cũ) + set `va_mfa_trust` cookie.
4. **Trusted cookie hợp lệ → bỏ qua step 2** (từ bước 1 sang thẳng JWT).
5. **Staff** (không thuộc EnforcedRoles): login như cũ — MFA optional (trang Settings hiển thị nếu đã bật, không bắt buộc).
6. **Sysadmin/Owner chưa setup MFA:** login bình thường (backward compat user cũ) + **cảnh báo banner** "Bật xác thực 2 lớp để bảo vệ tài khoản" (fail-safe — KHÔNG chặn login cứng, tránh lockout admin).

### 3.4 Settings UI — `/account/mfa` (ShopERP, UI Platform components)

- Chưa bật: QR + secret text + input code → "Kích hoạt" (verify trước khi lưu) → success badge.
- Đã bật: trạng thái + "Tắt MFA" (yêu cầu nhập password hiện tại) + "Quên thiết bị này" (revoke trusted).
- NavMenu nhóm Tài khoản (grep vị trí `UserHeader`/account menu hiện có).

## 4. TEST CASES (TDD — viết trước)

### 4.1 Domain — `DemoUserMfaTests`
- [ ] `Create` → `MfaEnabled=false`, `MfaSecret=null`
- [ ] `EnableMfa(secret)` → set đúng; gọi lại với secret khác khi đã enable → `InvalidOperationException`
- [ ] `EnableMfa("")` → guard fail
- [ ] `DisableMfa` → clear cả 2; idempotent (gọi 2 lần không lỗi)
- [ ] `DisableMfa` không ảnh hưởng `TrustLevel`/`PhoneNumber` (regression ID-P1 fields)

### 4.2 Service — `MfaServiceTests`
- [ ] `GenerateSetup` → secret base32 32 chars + URI chứa `otpauth://totp/` + issuer `VanAn`
- [ ] `EnableAsync` code đúng (OtpNet `Totp` generate cùng secret) → `MfaEnabled=true`
- [ ] `EnableAsync` code sai → false, không lưu secret
- [ ] `VerifyLoginAsync` đúng code → Success; sai → Fail; sai 5 lần → Locked
- [ ] Locked → kể cả code đúng → vẫn Locked tới khi hết lockout (test với lockout ngắn)
- [ ] `IsTrustedAsync` token đúng hash → true; token khác → false
- [ ] `IssueTrustedToken` → hash lưu DB, cookie value ≠ hash (không lộ)
- [ ] `RevokeTrustAsync` → hash null
- [ ] `SendEmailFallbackOtpAsync` user có email → gửi Brevo (mock) + OTP verify được; user không email → null + không crash
- [ ] Email fallback OTP single-use (verify 1 lần → lần 2 fail)
- [ ] EF: `MfaSecret` raw column không plaintext; decrypt đúng; cross-decrypt với protector `DemoUser.PhoneNumber` → fail

### 4.3 Login flow — ShopERP.Tests (bUnit/service)
- [ ] Password đúng + `MfaEnabled` + role enforced → challenge step (chưa có JWT)
- [ ] Step 2 code đúng → JWT + cookie trust set
- [ ] Trusted cookie hợp lệ → bỏ qua challenge
- [ ] Staff không enforced → login thẳng (không đổi)
- [ ] Sysadmin chưa setup MFA → login được + banner cảnh báo
- [ ] Code sai 5 lần → lock message
- [ ] Regression: password-only login user cũ (fields null) nguyên vẹn

## 5. ACCEPTANCE CRITERIA

- [ ] `dotnet build VanAn.sln` 0 errors + `.\scripts\guard-check.ps1` PASS
- [ ] Tất cả test §4 PASS
- [ ] Migration PG + SQLite additive, apply sạch
- [ ] Dev verify: bật MFA bằng authenticator thật (Google Authenticator scan QR) → login 2 bước → trusted cookie skip → tắt MFA → login 1 bước
- [ ] Email fallback: mất authenticator → nhận mail code → login OK
- [ ] **Gate 5 checklist:** change trong approved plan ✓ · AccountingEntry không đụng ✓ · Domain pure ✓ · KHÔNG đụng `IdentityLevel`/`TrustLevel`/L0-L3 ✓

## 5.5 VERIFICATION + RV (bắt buộc — user: "verify được khi chạy rv test")

**Local (trước push):**
```powershell
dotnet build VanAn.sln
.\scripts\guard-check.ps1
dotnet test 6_Tests/VanAn.Core.Tests --filter "FullyQualifiedName~Mfa|DemoUser"
dotnet test 6_Tests/VanAn.ShopERP.Tests --filter "FullyQualifiedName~Mfa|Login"
```

**RV production (sau deploy — sweep-first, STOP ở fail đầu):**
- [ ] **L1 markers:** `MfaService`/`GenerateSetup` trong CoreHub.dll image mới · migration `AddDemoUserMfa` (PG + SQLite) applied
- [ ] **L2:** health 200 · route `/account/mfa` 302→Login · login page render không đổi
- [ ] **L3 Playwright prod** (`playwright-rv-mfa.config.ts` + storageState — TOTP tính trong test bằng thư viện node `otplib` + secret từ endpoint/test-helper): sysadmin bật MFA (setup flow) → logout → login step 2 (TOTP đúng) → success · TOTP sai → fail + attempt count · trusted cookie skip · disable MFA → login 1 bước (regression) · banner user chưa setup
- [ ] **L4 manual (user):** scan QR bằng Google Authenticator thật trên điện thoại + email fallback thật 1 lần
- [ ] Cleanup: reset MFA account test · ghi kết quả vào project_state
- [ ] **⚠️ QUY TẮC RV ACCOUNT (bắt buộc — xem `.devin/rules/runtime-verification.md` §RV ACCOUNTS & MFA):** KHÔNG bật MFA trên account RV (`sysadmin@vanan.vn`/`adminvanan1`/staff) — test MFA chỉ trên account RV chuyên dụng + secret trong rv config + `otplib`; storageState lưu kèm cookie `va_mfa_trust`.

## 6. RISKS / NOTES

- **Lockout admin:** không chặn cứng login khi chưa setup MFA (banner thôi) — tránh khóa admin ngoài ý muốn; khi MFA bật rồi thì enforce đủ.
- **Email fallback = single factor nếu email bị chiếm** — chấp nhận MVP (email OTP qua Brevo + login từ IP lạ có audit); recovery codes + WebAuthn là phase sau.
- **`MfaSecret` encrypt at rest** — key từ DataProtection (như PhoneNumber/Email) — nếu mất key → user phải reset MFA qua sysadmin (thủ công, ghi vào runbook).
- **Trusted device 1 cột hash** — user dùng 2 máy → login máy 2 yêu cầu TOTP (revoke máy 1) — ghi nợ multi-device table.
- **OtpNet license** — MIT, phổ biến, stable 1.4.0 (2023) — đủ tuổi an toàn; thêm qua `dotnet add package` (không sửa tay csproj).
- **Không đổi JWT claims / API contract** — chỉ thêm challenge step ở login page ShopERP; Gateway/khachlink không bị ảnh hưởng.