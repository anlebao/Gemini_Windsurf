# Task P4: OTP Xác Minh SĐT Cho Self-Serve Signup

> **SRS:** `docs/requirements/van_an_self_serve_onboarding_srs_v1.md` — FR-2, FR-15, FR-16
> **Master plan:** §4 P4
> **Mode:** IMPLEMENT
> **Prerequisite CỨNG:** `ESMS_API_KEY` + `ESMS_SECRET_KEY` có trên production (hiện `Esms__ApiKey=not-configured` trong `docker-compose.shoperp.yml` + `docker-compose.prod.yml`) — **hỏi user trước khi bắt đầu**
> **Layer:** 2_Gateway + 3_CoreHub (ISmsService đã có) + 5_WebApps/KhachLink (UI OTP)

## 1. OBJECTIVE

Khi `MerchantSignup:RequireOtp=true`: merchant phải xác minh SĐT bằng OTP SMS trước khi submit signup. `otpVerified` lưu vào `SelfServePayload` → sysadmin thấy badge "SĐT đã xác minh". Khi `false` (default): không đổi gì (P0 behavior).

## 2. FILES

| File | Change |
|---|---|
| `2_Gateway/Controllers/MerchantSignupController.cs` | Implement stub `otp/send` + `otp/verify` (P0 để 501) |
| `3_CoreHub/Services/Onboarding/…` hoặc service mới `MerchantSignupOtpService` | OTP generate/store/verify — IMemoryCache Gateway-side (pattern `OtpService` ShopERP) + gọi `ISmsService.SendSmsAsync` |
| `2_Gateway/Program.cs` | Rate-limit `signup-otp` (đã khai báo P0); config `MerchantSignup:RequireOtp` + `MerchantSignup:OtpDailyLimitPerPhone` |
| `5_WebApps/KhachLink/Pages/Onboard.razor` | Step account: nút "Gửi mã OTP" + ô nhập mã + resend cooldown 60s + trạng thái verified |
| `5_WebApps/KhachLink/Services/Http/MerchantSignupHttpService.cs` | `SendOtpAsync`, `VerifyOtpAsync`, `GetConfigAsync` |
| `docker-compose.*.yml` / `.env` mẫu | `MerchantSignup__RequireOtp=false` default; ESMS keys đã là env pattern sẵn (`Esms__ApiKey`) |
| `6_Tests/VanAn.Core.Tests` | OtpService tests (generate/verify/expiry/resend-limit) |

## 3. CHANGES CHI TIẾT

### 3.1 OTP service (Gateway-side)

- `IMerchantSignupOtpService`: `SendOtpAsync(phone)` → generate 6 số → IMemoryCache key `signup-otp:{phone}` TTL 5' + counter `signup-otp-count:{phone}:{yyyyMMdd}` limit 5/ngày → `ISmsService.SendSmsAsync(phone, $"Ma xac minh Vạn An: {otp} (hieu luc 5 phut)")` → trả `{sent:true, retryAfterSeconds}` — **không trả OTP** (khác pattern dev cũ — production an toàn).
- `VerifyOtpAsync(phone, otp)` → check cache + counter sai (sai → increment, ≥5 → khóa 24h) → success → set flag `signup-otp-verified:{phone}` TTL 30' → trả session token đơn giản (random guid lưu cache map→phone) để submit kèm `otpSessionToken`.
- `MerchantSignupRequest` += `string? OtpSessionToken` (P0 DTO thêm optional — backward compat).
- Submit check (khi RequireOtp=true): resolve token → phone khớp `ContactPhone` → mới accept; set `OtpVerified=true` trong payload.

### 3.2 SMS channel

- `ISmsService` đã đăng ký trong Gateway `Program.cs` (line ~1055 `EsmsNotificationService`) — **verify nó trỏ đúng config**; eSMS production keys phải set trong env (`.env` trên VPS — KHÔNG commit).
- `EsmsNotificationService.SendSmsAsync` — đọc implementation: check SmsType/template/brandname; OTP cần mẫu tin đủ ký tự (tiếng Việt không dấu cho SMS thường — `SmsType` config quyết định; eSMS type 2 = Unicode?). Task card note: verify nội dung SMS thực tế gửi được (ký tự có dấu bị mất → viết không dấu).
- Test gửi thật 1 SMS tới SĐT test trước khi bật RequireOtp.

### 3.3 UI `/onboard` step account

- `GET /api/v1/merchant-signups/config` → `{ requireOtp }` (P0 thêm endpoint nhỏ này nếu chưa — public, không nhạy cảm).
- RequireOtp=true: SĐT input → nút "Gửi mã xác minh" → OTP input + "Xác nhận" → badge "✓ Đã xác minh" → mới enable submit.
- RequireOtp=false: ẩn hoàn toàn OTP UI.
- Resend: disabled 60s countdown; sai mã → thông báo + còn lượt.

### 3.4 Sysadmin side

- `SignupDetailDto.OtpVerified` đã có (P0) → badge "SĐT đã xác minh ✓" / "SĐT chưa xác minh ⚠".
- RequireOtp=false → vẫn hiển thị "chưa xác minh" như cảnh báo nhẹ (không chặn duyệt).

## 4. ACCEPTANCE CRITERIA

- [ ] `otp/send` → SMS thật tới SĐT (verify trên máy thật); response không chứa OTP
- [ ] `otp/verify` đúng → session token; submit kèm token → `OtpVerified=true` trong payload
- [ ] Sai OTP → 400; quá 5 lần → khóa; OTP hết hạn 5' → từ chối
- [ ] Rate-limit: >3 send/10min → 429; >5 OTP/ngày/phone → từ chối
- [ ] RequireOtp=false → submit không cần OTP (P0 behavior nguyên vẹn)
- [ ] Submit với phone khác phone đã verify → 400
- [ ] Sysadmin modal hiển thị badge OTP đúng
- [ ] `dotnet build` + guard + Core.Tests PASS

## 5. VERIFICATION

```powershell
dotnet build VanAn.sln
dotnet test 6_Tests/VanAn.Core.Tests --filter "FullyQualifiedName~Otp|MerchantSignup"
# SMS thật: gửi tới SĐT test trên production/staging sau deploy
```

## 6. RISKS / NOTES

- **eSMS production keys**: hiện `not-configured` → PHẢI user cung cấp `ESMS_API_KEY`/`ESMS_SECRET_KEY` + set env trên VPS trước deploy. Nếu chưa có → defer toàn bộ P4 (MVP vẫn ship với RequireOtp=false).
- IMemoryCache Gateway: single instance VPS → OK; nếu scale multi-gateway sau này → chuyển Redis (ghi nợ).
- SMS cost: mỗi signup 1 SMS — rate-limit chặt; cân nhắc chỉ gửi khi RequireOtp=true.
- Alternative nếu eSMS không khả thi: Zalo ZNS (task card archived `tiered_auth_loyalty_phase5_zalo_zns_task_card.md` — đánh giá lại) hoặc email OTP (nếu email deliverability OK — hiện chưa có mail service confirmed).
