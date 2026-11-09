# Task P0: Self-Serve Signup API Core (Gateway + CoreHub)

> **SRS:** `docs/requirements/van_an_self_serve_onboarding_srs_v1.md` — FR-1,3,4,5,6
> **Master plan:** `docs/AI/plans/self-serve-onboarding-master-plan.md` §5
> **Mode:** IMPLEMENT (sau khi SRS được duyệt)
> **Prerequisite:** User chốt SRS §9 Q1-Q5
> **Layer:** 3_CoreHub (Services + DTOs) + 2_Gateway (Controllers + rate-limit)
> **Domain modification:** KHÔNG — nếu bắt buộc → STOP báo user (Gate 5)

## 0. PRE-FLIGHT (làm đầu tiên — risk "login resolution chưa rõ")

- [x] **PF1:** ✅ DONE 2026-11-09 — Login = username+password trên ShopERP host (`PlatformUserLoginController`); tenant resolve qua `UserTenant`→JWT `tenant_id` — **slug KHÔNG cần cho login** (slug chỉ storefront `/store/{slug}`). Status endpoint trả `slug` cho storefront link.
- [x] **PF2:** ✅ DONE — `tenant.Deactivate(reason)` (Pending→Inactive). Không có status "Rejected" → status endpoint map Inactive+self-serve payload → "Rejected" (edge: Inactive vì lý do khác cũng hiển thị Rejected — chấp nhận MVP).
- [x] **PF3:** ✅ DONE — `ITurnstileVerificationService.VerifyAsync(token, ip, ct)`; controller pattern giống `TenantRegistrationController` (honeypot+turnstile ở controller, KHÔNG trong service).
- [x] **PF4:** ✅ DONE — `PersistKeysToFileSystem` + volume `gateway_keys:/app/keys` (`docker-compose.gateway.yml:162`) — keys persistent, `verify-signup` an toàn sau restart.

## 1. OBJECTIVE

Public endpoint nhận self-serve signup → tạo **Pending tenant** + `CrawlSource` payload đầy đủ (ảnh, menu, credentials được bảo vệ) → sysadmin endpoints: xem hồ sơ, verify-signup (dùng credentials merchant, không lộ password), reject-pending.

## 2. FILES

### Mới
| File | Nội dung |
|---|---|
| `3_CoreHub/Services/Onboarding/Dtos/MerchantSignupDtos.cs` | `MerchantSignupRequest`, `MerchantSignupResult`, `SelfServePayload`, `SignupDetailDto`, `VerifySignupRequest`, `RejectPendingRequest`, `SignupCase` enum (3 giá trị) |
| `3_CoreHub/Services/Onboarding/MerchantSignupService.cs` | `IMerchantSignupService` + impl: `SubmitAsync`, `GetSignupDetailAsync`, `VerifySignupAsync`, `RejectPendingAsync` |
| `2_Gateway/Controllers/MerchantSignupController.cs` | `POST /api/v1/merchant-signups` [AllowAnonymous + rate-limit + Turnstile] · `POST .../otp/send`+`/otp/verify` (stub 501 — P4) |
| `2_Gateway/Controllers/TenantSignupAdminController.cs` hoặc extend `TenantPendingController` | `GET /api/v1/tenants/{id}/signup-detail` · `POST /api/v1/tenants/{id}/verify-signup` · `POST /api/v1/tenants/{id}/reject-pending` + `GET /api/v1/merchant-signups/status` (public, rate-limit nhẹ) |

| `3_CoreHub/Services/Platform/IPlatformOnboardingFlagsService.cs` (hoặc Onboarding/) | Flags service đọc/ghi `SystemSetting` (pattern `CommerceModeService` — tìm service đó để copy pattern cache/DI) |
| `2_Gateway/Controllers/PlatformOnboardingFlagsController.cs` | GET public + PUT SystemAdmin |
| `6_Tests/VanAn.Core.Tests/Services/Onboarding/MerchantSignupServiceTests.cs` + flags tests | Submit/verify/reject/detail/flags |

### Sửa
| File | Change |
|---|---|
| `3_CoreHub/Services/Onboarding/Dtos/CrawlDtos.cs` | `CrawlListingDto` += optional `SelfServePayload? SelfServe = null` (record mới trong file DTOs mới — tham chiếu chéo OK cùng namespace) |
| `3_CoreHub/Services/Onboarding/TenantOcrOnboardingService.cs` | Refactor nhỏ: extract validation rules dùng chung? — KHÔNG bắt buộc; `MerchantSignupService` tự validate (submit ≠ verify flow khác nhau). Chỉ thêm `SelfServe` vào listing khi gọi `OnboardUnverifiedAsync` (listing record mới có field — positional record → thêm param optional cuối) |
| `3_CoreHub/Services/Onboarding/ITenantOnboardingService.cs` | Không đổi signature (`OnboardUnverifiedAsync` nhận `CrawlListingDto` — field mới đi kèm) |
| `2_Gateway/Program.cs` | Rate-limit policies: `merchant-signup` (5/24h/IP — pattern `registration-submit`), `signup-otp` (3/10min), `signup-status` (20/h/IP); DI `MerchantSignupService` + `IDataProtection` (nếu chưa) |
| `3_CoreHub/... DI registration` | Register `IMerchantSignupService` (theo pattern `ITenantOcrOnboardingService`) |

## 3. CHANGES CHI TIẾT

### 3.1 DTOs (`MerchantSignupDtos.cs`)

```csharp
public enum SignupCase { CompanyWithLicense = 0, SmallShopNoLicense = 1, StreetVendorUnregistered = 2 }

public record SelfServePayload(
    string SignupCase, List<OcrProductLineDto> ProductLines,
    string ContactName, string ContactPhone, string? ContactEmail,
    string OwnerUsername, string OwnerPasswordProtected,
    bool OtpVerified, bool Unregistered,
    string? ClientIpHash, string? UserAgent);

public record MerchantSignupRequest(
    SignupCase Case, string Name, string? Address,
    BusinessType BusinessType, HKDGroup? HkdGroup,
    OcrGpdkInfo? GpdkInfo, string? SignName,
    double? Latitude, double? Longitude,
    List<string>? PhotoUrls, List<OcrProductLineDto> ProductLines,
    string ContactName, string ContactPhone, string? ContactEmail,
    string OwnerUsername, string OwnerPassword,
    string? TurnstileToken, string? HoneypotWebsite,
    string? OtpVerifiedPhone = null);

public record MerchantSignupResult(string Message, Guid SignupId);
public record SignupDetailDto(... /* KHÔNG chứa password/protected */);
public record VerifySignupRequest(Guid? ShopInstanceId, string? Slug,
    string? OwnerUsername, string? OwnerPassword, string? OwnerDisplayName);
public record RejectPendingRequest(string Reason);
```

### 3.2 `MerchantSignupService.SubmitAsync`

Thứ tự bắt buộc:
1. Honeypot non-empty → return fake success (không lưu).
2. `turnstileService.VerifyAsync` fail → `ArgumentException`.
3. Validate per case:
   - `CompanyWithLicense`: MST valid 10/13 số (reuse `IsValidTaxCode` logic — copy private method hoặc extract internal static), Name non-empty.
   - `SmallShopNoLicense`: Name + HkdGroup bắt buộc; BusinessType phải HouseholdBusiness.
   - `StreetVendorUnregistered`: Name bắt buộc; **force** `HkdGroup=Group1`, `BusinessType=HouseholdBusiness`, `Unregistered=true` (ignore client gửi khác).
4. `RequireOtp` config (`MerchantSignup:RequireOtp`, default false): nếu true → `OtpVerifiedPhone == ContactPhone` else throw.
5. Normalize phone (strip non-digit, leading 0→84? — theo pattern `OtpService`/CustomerIdentity; ghi convention vào code).
6. Build `CrawlListingDto` (SourceSite: `OCR-SELF-GPKD` / `OCR-SELF-SIGN` / `OCR-SELF-RONG`; `SourceUrl="ocr://self-serve"`; PhotoUrls; Lat/Lng; TaxCode=MST hoặc null; CrawledPhone=null — SĐT đi qua payload) + `SelfServe` payload (`OwnerPasswordProtected = protector.Protect(request.OwnerPassword)`).
7. `ownerUsername` validate: `^[a-z0-9._-]{3,30}$` lowercase.
8. `OnboardUnverifiedAsync(listing, ct, businessType, hkdGroup)` → tenantId.
   - Case A MST trùng → `MarkPotentialDuplicateOf` tự động trong service — KHÔNG throw.
   - Case B/Rong: dedup gợi ý tên+địa chỉ+phone → thêm flag `DuplicateWarning` vào payload (không chặn).
9. Metering `feature.ocr` fire-and-forget (copy pattern `TenantOcrOnboardingService` — `metering.RecordUsageAsync`, try/catch log warning).
10. `ClientIpHash` = SHA256(ip + config salt `MerchantSignup:IpHashSalt`) — không lưu IP thô. `UserAgent` truncate 256.
11. Return `MerchantSignupResult` generic — message không tiết lộ duplicate [G10].

### 3.3 `VerifySignupAsync(tenantId, req, sysAdminUserId)`

1. Load tenant `IgnoreQueryFilters` → phải `Status==Pending` else 409; `PotentialDuplicateOf != null` → 409 "resolve duplicate trước".
2. Load `CrawlSources` mới nhất của tenant → parse `PayloadJson` → `CrawlListingDto.SelfServe` null → 400 "không phải self-serve signup".
3. `password = req.OwnerPassword ?? protector.Unprotect(payload.OwnerPasswordProtected)`.
4. `username = req.OwnerUsername ?? payload.OwnerUsername`; `displayName = req.OwnerDisplayName ?? tenant.Name[..80]`; `phone/email` từ payload (hoặc override).
5. `VerifyAsync(tenantId, new VerifyTenantRequest(username, password, displayName, phone, email, req.ShopInstanceId, req.Slug, sysAdminUserId))`.
6. Trả `{ TenantId, Slug=verifyResult.PublishedSlug, OwnerUsername=username, UsedMerchantPassword = req.OwnerPassword is null }`.
7. Log: tenantId, sysAdminUserId, usedMerchantPassword — KHÔNG log password/protected.

### 3.4 `RejectPendingAsync(tenantId, reason, sysAdminUserId)`

- Tenant Pending → lifecycle method từ PF2 (vd `Deactivate()`/`Suspend()` — chọn theo domain sẵn có).
- Reason → `ILogger` + (nếu entity cho phép) field sẵn có; KHÔNG thêm cột.
- Non-Pending → 409.

### 3.5 `GetSignupDetailAsync(tenantId)`

- Tenant + CrawlSource mới nhất → DTO: case, name/address/fields, photoUrls, productLines, contact info, ownerUsername, flags (otpVerified/unregistered/duplicateWarning), submittedAt, hasPassword=true (không bao giờ trả blob).

### 3.6 `GET /api/v1/merchant-signups/status` (public)

- Query: `signupId` (tenantId) + `phone` → trả `{ status: "Pending"|"Active"|"Rejected"|"NotFound", slug?, message? }`.
- Rate-limit `signup-status` 20/h/IP. Anti-enumeration: chỉ trả "Active+slug" khi phone khớp `Settings.ContactPhone` HOẶC `CrawledPhone`; Pending → generic "Đang chờ duyệt"; sai cặp → "NotFound" generic (không phân biệt tồn tại/không).
- Khi `RequireOtp=true` (P4): thay phone-check bằng OTP-verified session — P0 implement phone-check trước, P4 nâng cấp.

### 3.8 Onboarding flags — entry-point toggles (FR-22 backend, user chốt 2026-11-09)

- `IPlatformOnboardingFlagsService` (CoreHub): đọc/ghi `SystemSetting` keys `Onboarding.SelfServeEnabled` / `Onboarding.DirectoryCtaEnabled` / `Onboarding.QrEntryEnabled` / `Onboarding.DeeplinkPrefillEnabled` (TenantId=Empty; value `"true"`/`"false"`; **default `"true"`** khi key chưa tồn tại; cache in-memory ~30s).
- `GET /api/v1/platform/onboarding-flags` — **AllowAnonymous**, trả `{ selfServeEnabled, directoryCta, qrEntry, deeplinkPrefill }` (Directory + KhachLink đọc).
- `PUT /api/v1/platform/onboarding-flags` — `[SystemAdmin]`, body flags → update keys (`UpdatedBy`=sysadmin). Validate bool.
- `MerchantSignupService.SubmitAsync` check `SelfServeEnabled` đầu tiên → false → `InvalidOperationException("self-serve-closed")` → controller map **503** `{ error: "Đăng ký tự phục vụ tạm đóng" }`.
- `MerchantSignupRequest` += `string? Src` (entry channel: directory/audit/qr/zalo/other/direct — validate whitelist, default "direct") → lưu vào `SelfServePayload` (thêm field `Src`).

### 3.7 Rate-limit policies (`2_Gateway/Program.cs`)

Copy pattern `registration-submit` hiện có:
- `merchant-signup`: PermitLimit=5, Window=24h, partition theo IP.
- `signup-otp`: PermitLimit=3, Window=10min, partition phone+IP.
- `signup-status`: PermitLimit=20, Window=1h, partition IP.
- `image-upload` policy mở rộng: thêm rule cho `folder` bắt đầu `onboarding` → 30/h (hoặc policy riêng `onboarding-images` — chọn cách đơn giản hơn theo code rate-limit hiện tại).

## 4. ACCEPTANCE CRITERIA

> ✅ IMPLEMENTED 2026-11-09 (commit `6ac8e809`) — build 0 err · Core.Tests +35 (35/35 PASS · onboarding suite 121 PASS) · guard PASSED. Deviations: honeypot+turnstile ở controller (pattern TenantRegistration); `CrawlSource.RawJson` (không phải PayloadJson); `SelfServePayload` += SignName/GpdkInfo (không có trong CrawlListingDto); reject→`Deactivate` (không có status Rejected); `Src` trong payload.

- [x] `POST /api/v1/merchant-signups` Case A hợp lệ → 200 generic + Pending tenant trong DB (Status=Pending, SourceSite=OCR-SELF-GPKD, BusinessType đúng)
- [x] `CrawlSource.PayloadJson` chứa đủ: photoUrls, productLines, contactPhone, ownerUsername, ownerPasswordProtected, otpVerified, unregistered, ipHash — **không chứa plaintext password**
- [x] MST trùng tenant cũ → vẫn 200 generic + `PotentialDuplicateOf` set + tenant nằm tab Duplicates
- [x] Case B/Rong validate đúng; Rong force Group1+unregistered
- [x] Honeypot → 200 fake không lưu; Turnstile fail → 400; vượt rate-limit → 429
- [x] `verify-signup`: Pending self-serve → Active + owner user login được bằng password merchant đặt; `usedMerchantPassword=true`; password override → `false`
- [x] `verify-signup` trên crawl tenant (không payload) → 400; trên Active → 409; trên dup-blocked → 409
- [x] `signup-detail` KHÔNG chứa `OwnerPasswordProtected`/password ở bất kỳ field nào
- [x] `reject-pending` → tenant không còn Pending; reason logged
- [x] `status` endpoint: đúng cặp signupId+phone → trạng thái thật; sai → generic
- [x] Metering `feature.ocr` event fired (mock verify)
- [x] `dotnet build VanAn.sln` 0 errors · guard-check.ps1 ALL PASSED · Core.Tests mới PASS + không regression

## 5. VERIFICATION

```powershell
dotnet build VanAn.sln
dotnet test 6_Tests/VanAn.Core.Tests --filter "FullyQualifiedName~MerchantSignup"
.\scripts\guard-check.ps1   # hoặc đường dẫn thật trong repo
```

Manual API test (dev): submit → query PG `Tenant` Status=Pending + `CrawlSources.PayloadJson` → gọi verify-signup → login owner.

## 6. RISKS / NOTES

- `CrawlListingDto` là **positional record** — thêm `SelfServe` param optional ở CUỐI để không break call sites hiện có (compile sẽ bắt các chỗ thiếu arg nếu không optional).
- `JsonSerializer.Serialize(listing)` trong `OnboardUnverifiedAsync` — field `SelfServe` tự serialize; kiểm tra `CrawlSource` payload size limit (nvarchar(max)? — verify EF config; productLines lớn vẫn OK).
- `IDataProtection` ở Gateway: confirm `AddDataProtection` đã gọi (QR feature dùng); keys persist ở đâu (default EPHEMERAL nếu không config → **password protected mất sau restart → verify-signup fail**! → PF4 bắt buộc verify key persistence — nếu ephemeral phải config `PersistKeysTo...` hoặc chấp nhận re-protect? KHÔNG — phải persistent keys; nếu chưa có → task phụ config DataProtection keys directory/Redis). **Đây là risk lớn nhất P0.**
- OTP endpoints: tạo route stub trả `501 Not Implemented` + comment P4 (tránh 404 confuse client P1 dev).
- Username uniqueness: `CreateUserAsync` check trong tenant scope — verify behavior khi trùng (throw gì) để map 409 đúng.
