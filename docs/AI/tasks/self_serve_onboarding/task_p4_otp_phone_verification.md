# Task P4: OTP Xác Minh Liên Hệ Cho Self-Serve Signup (Channel-Agnostic — L2)

> **SRS:** `docs/requirements/van_an_self_serve_onboarding_srs_v1.md` — FR-2, FR-15, FR-16
> **Master plan:** §4 P4 · **Identity plan:** `docs/AI/plans/van-an-identity-tiered-trust-master-plan.md` (P4 = **ID-P3** — L2 Contact Verification channel, KHÔNG phải identity foundation)
> **Mode:** IMPLEMENT
> **Prerequisite CỨNG:** ÍT NHẤT 1 kênh OTP khả dụng trên production — **✅ CHỐT 2026-11-10 (user duyệt): kênh đầu = Brevo email 0đ** (`Brevo__ApiKey` free tier — code + tests ĐÃ CÓ sẵn `BrevoEmailService`) · slot ZNS 300đ khi có Zalo OA verified (phase sau) · eSMS SMS chỉ khi có use-case thuần SMS · composite fallback `email` (mặc định) — chi tiết §6.1
> **Layer:** 2_Gateway + 3_CoreHub (ISmsService/IEmailService đã có) + 5_WebApps/KhachLink (UI OTP)
> **Bổ sung review 2026-11-10 (đã duyệt):** hardening `OtpService` cũ (ShopERP customer OTP login — `Random` non-CSPRNG + thiếu attempt limit + thiếu purpose binding) **cùng wave này** — xem §8

## 1. OBJECTIVE

Khi `MerchantSignup:RequireOtp=true`: merchant phải xác minh liên hệ (SĐT qua SMS/ZNS/voice, hoặc email) bằng OTP trước khi submit signup. `otpVerified` lưu vào `SelfServePayload` → sysadmin thấy badge "Đã xác minh". Khi `false` (default): không đổi gì (P0 behavior).

**Thiết kế channel-agnostic (update 2026-11-10):** OTP service KHÔNG hardcode SMS — trừu tượng `IIdentityOtpChannel` để đổi/bổ sung kênh (SMS → ZNS → email → voice) chỉ bằng cách thêm 1 implementation + config, không rewrite service/UI flow.

**Định vị lại theo Zero-Cost Identity (2026-11-10):** P4 là kênh **L2 Contact Verification** trong mô hình trust tier L0-L3 — KHÔNG phải cơ chế định danh chính. Merchant signup thực chất cần L1 (account) + sysadmin verify = L3 tay; OTP chỉ bật khi có use-case cần SĐT thật (`RequireOtp` default false). Khi ID-P1 (`ContactVerification` aggregate) có, `OtpVerified` bool sẽ migrate sang record có `channel`/`contact`/`verifiedAt`/`purpose`.

## 2. FILES

| File | Change |
|---|---|
| `2_Gateway/Controllers/MerchantSignupController.cs` | Implement stub `otp/send` + `otp/verify` (P0 để 501) |
| `3_CoreHub/Services/Onboarding/…` hoặc service mới `MerchantSignupOtpService` | OTP generate/store/verify — IMemoryCache Gateway-side (pattern `OtpService` ShopERP) + gọi `IIdentityOtpChannel` (xem 3.1a) |
| `3_CoreHub/Services/Otp/` (mới) | `IIdentityOtpChannel` + `EsmsSmsOtpChannel` (wrap `ISmsService`) + `BrevoEmailOtpChannel` (wrap `IEmailService`, optional) — ZNS channel thêm sau khi có OA |
| `2_Gateway/Program.cs` | Rate-limit `signup-otp` (đã khai báo P0); config `MerchantSignup:RequireOtp` + `MerchantSignup:OtpDailyLimitPerPhone` + `MerchantSignup:OtpChannel` (`sms`\|`email`\|`zns`) |
| `5_WebApps/KhachLink/Pages/Onboard.razor` | Step account: nút "Gửi mã OTP" + ô nhập mã + resend cooldown 60s + trạng thái verified |
| `5_WebApps/KhachLink/Services/Http/MerchantSignupHttpService.cs` | `SendOtpAsync`, `VerifyOtpAsync`, `GetConfigAsync` |
| `docker-compose.*.yml` / `.env` mẫu | `MerchantSignup__RequireOtp=false` default; ESMS keys đã là env pattern sẵn (`Esms__ApiKey`) |
| `6_Tests/VanAn.Core.Tests` | OtpService tests (generate/verify/expiry/resend-limit) |

## 3. CHANGES CHI TIẾT

### 3.1 OTP service (Gateway-side)

- `IMerchantSignupOtpService`: `SendOtpAsync(contact, channel?)` → generate 6 số bằng **`RandomNumberGenerator.GetInt32` (CSPRNG)** — KHÔNG copy `System.Random` của ShopERP `OtpService` (nợ bảo mật đã ghi trong identity-inventory §3.4) → IMemoryCache key `signup-otp:{contact}` TTL 5' + counter `signup-otp-count:{contact}:{yyyyMMdd}` limit 5/ngày → gọi `IIdentityOtpChannel.SendOtpAsync(contact, code)` → trả `{sent:true, retryAfterSeconds}` — **không trả OTP** (khác pattern dev cũ — production an toàn).
- `VerifyOtpAsync(contact, otp)` → check cache + counter sai (sai → increment, ≥5 → khóa 24h) → success → **consume token ngay (single-use, chống replay)** → set flag `signup-otp-verified:{contact}` TTL 30' → trả session token (random guid lưu cache map→`{contact, channel, purpose="merchant-signup", expiry}`) để submit kèm `otpSessionToken`.
- `MerchantSignupRequest` += `string? OtpSessionToken` (P0 DTO thêm optional — backward compat).
- Submit check (khi RequireOtp=true): resolve token → **purpose == "merchant-signup"** + contact khớp `ContactPhone` (hoặc `ContactEmail` nếu channel=email) + chưa hết hạn → mới accept; **consume luôn session token sau submit thành công** (1 lần dùng).
- `SelfServePayload` += optional trailing params `OtpChannel`, `OtpVerifiedContact`, `OtpVerifiedAt` (additive, backward compat — record positional params có default) → sysadmin thấy verify qua kênh nào, lúc nào. Khi ID-P1 `ContactVerification` entity có → migrate sang đó (ghi nợ).

### 3.1a Channel abstraction (mới — 2026-11-10)

```csharp
public interface IIdentityOtpChannel
{
    string ChannelName { get; }                       // "sms" | "email" | "zns" | "voice"
    Task<bool> SendOtpAsync(string contact, string code, CancellationToken ct = default);
}
```

- `MerchantSignup:OtpChannel` chọn implementation active (**CHỐT 2026-11-10: default `email`** khi có Brevo key; `sms` chỉ khi eSMS keys; `zns` khi OA) + `MerchantSignup:OtpFallbackChannels` default `"email"` (thêm `"zns"`/`"sms"` khi kênh khả dụng).
- `EsmsSmsOtpChannel`: wrap `ISmsService.SendSmsAsync` — nội dung `Ma xac minh Van An: {code} (hieu luc 5 phut)` (không dấu cho SMS thường; verify `Esms:SmsType` — type 2/4 = Unicode).
- `BrevoEmailOtpChannel`: wrap `IEmailService.SendEmailAsync` — subject `Ma xac minh Van An`, HTML body chứa code lớn + hạn 5'. **PRIMARY channel (chốt) — implement + test đầy đủ trước.**
- `ZnsOtpChannel` (phase sau, khi có OA): cùng interface, gọi Zalo ZNS template "xác thực" đã duyệt — 300đ/tin, chỉ tính tin thành công. **Không chặn P4 — slot giữ sẵn, thêm implementation khi OA sẵn sàng.**
- **Composite fallback (bắt buộc — chốt):** `FallbackOtpChannel` thử primary (`email`) → nếu `SendOtpAsync` trả false (provider lỗi) → thử channel kế tiếp — tránh mất signup vì 1 kênh chết. Config `MerchantSignup:OtpFallbackChannels` = `"email"` (mặc định), về sau `"email,zns"`.
- `MerchantSignupOtpService` chỉ biết `contact` + `code` — KHÔNG biết kênh; session token lưu kèm `channel` đã verify để submit check đúng field.

### 3.2 SMS/ZNS/email channel

- `ISmsService` đã đăng ký trong Gateway `Program.cs` (line ~1055 `EsmsNotificationService`) — **verify nó trỏ đúng config**; eSMS production keys phải set trong env (`.env` trên VPS — KHÔNG commit).
- `EsmsNotificationService.SendSmsAsync` — đọc implementation: check SmsType/template/brandname; OTP cần mẫu tin đủ ký tự (tiếng Việt không dấu cho SMS thường — `SmsType` config quyết định; eSMS type 2 = Unicode?). Task card note: verify nội dung SMS thực tế gửi được (ký tự có dấu bị mất → viết không dấu).
- `BrevoEmailService` đã có sẵn + `BrevoEmailServiceTests` — chỉ cần `Brevo__ApiKey` free tier (300 mail/ngày) + `Brevo__SenderEmail` trên production. Hiện `Brevo__ApiKey=not-configured` trong compose — cùng pattern set env như ESMS.
- Test gửi thật 1 tin (SMS/email) tới địa chỉ test trước khi bật RequireOtp.

### 3.3 UI `/onboard` step account

- `GET /api/v1/merchant-signups/config` → `{ requireOtp, otpChannel, otpFallback }` (P0 thêm endpoint nhỏ này nếu chưa — public, không nhạy cảm).
- RequireOtp=true: contact input (SĐT mặc định; nếu `otpChannel=email` hoặc user chọn "nhận mã qua email" → dùng `ContactEmail`) → nút "Gửi mã xác minh" → OTP input + "Xác nhận" → badge "✓ Đã xác minh" → mới enable submit.
- RequireOtp=false: ẩn hoàn toàn OTP UI.
- Resend: disabled 60s countdown; sai mã → thông báo + còn lượt.

### 3.4 Sysadmin side

- `SignupDetailDto.OtpVerified` đã có (P0) → badge "Liên hệ đã xác minh ✓" / "Liên hệ chưa xác minh ⚠" (đổi label từ "SĐT" vì channel có thể là email).
- RequireOtp=false → vẫn hiển thị "chưa xác minh" như cảnh báo nhẹ (không chặn duyệt).

## 4. ACCEPTANCE CRITERIA

- [ ] `otp/send` → tin OTP thật tới contact test (SMS/email tuỳ channel); response không chứa OTP
- [ ] `otp/verify` đúng → session token; submit kèm token → `OtpVerified=true` trong payload
- [ ] Sai OTP → 400; quá 5 lần → khóa; OTP hết hạn 5' → từ chối
- [ ] Rate-limit: >3 send/10min → 429; >5 OTP/ngày/contact → từ chối
- [ ] RequireOtp=false → submit không cần OTP (P0 behavior nguyên vẹn)
- [ ] Submit với contact khác contact đã verify → 400
- [ ] Channel fallback: primary fail → kênh kế tiếp gửi được (nếu config fallback)
- [ ] Sysadmin modal hiển thị badge OTP đúng
- [ ] `dotnet build` + guard + Core.Tests PASS

## 5. VERIFICATION

```powershell
dotnet build VanAn.sln
dotnet test 6_Tests/VanAn.Core.Tests --filter "FullyQualifiedName~Otp|MerchantSignup"
# SMS/ZNS/email thật: gửi tới contact test trên production/staging sau deploy
```

## 6. RISKS / NOTES

### 6.1 Điều kiện tiên quyết — CHỌN KÊNH TRƯỚC KHI IMPLEMENT

> **Bối cảnh mới (2026-11-10):** theo `van-an-identity-tiered-trust-master-plan.md`, P4 = ID-P3 (L2 channel). OTP KHÔNG nằm trên critical path — `RequireOtp` giữ `false`; implement P4 khi có use-case cụ thể cần verify liên hệ. **✅ QUYẾT ĐỊNH ĐÃ DUYỆT 2026-11-10: kênh đầu = email (Brevo 0đ); ZNS giữ slot; eSMS defer.**

Cả 2 provider hiện `not-configured` trong compose → P4 block cho tới khi có ÍT NHẤT 1 kênh. Bảng so sánh (verify 2026-11-10):

| Kênh | Chi phí/OTP | Điều kiện tiên quyết | Reach merchant VN | Ghi chú |
|---|---|---|---|---|
| **eSMS SMS brandname** (P4 gốc) | ~550-800đ/tin + phí đăng ký brandname | `ESMS_API_KEY`/`SECRET_KEY` + brandname "VanAn" đã duyệt | ~85% (SMS có thể bị chặn) | Code `EsmsNotificationService` đã có; brandname approval chậm (ngày-tuần) |
| **Zalo ZNS OTP** | **300đ/tin** — chỉ tính tin gửi thành công | Zalo OA **đã xác thực** (cần GPKD doanh nghiệp) + ZBS account nạp trước + template "xác thực" duyệt ~2-3 ngày | ~90%+ (ai có Zalo) | Rẻ hơn SMS ~50%, OTP trong 15s; OA mới limit 20k tin/ngày |
| **Brevo email OTP** | **0đ** (free 300 mail/ngày) | `Brevo__ApiKey` free — đăng ký 5 phút | Thấp với merchant nhỏ/rong (nhiều người không có email) | `BrevoEmailService` + tests **đã có sẵn** — rẻ nhất để unblock P4 ngay; nên làm fallback channel dù sao |
| **Voice OTP / missed-call** | 250-500đ/cuộc (chỉ tính khi bắt máy) | Provider mới (VoiceOTP.vn, Vinasite…) — tích hợp thêm | ~98% | Rẻ hơn SMS 30-50%; scope thêm — cân nhắc phase sau |
| **Telegram Bot OTP** | 0đ | Bot API free | Thấp (merchant VN ít dùng Telegram) | Chỉ nên làm fallback, không primary |

**Khuyến nghị thứ tự:** ① **Email OTP (Brevo)** — unblock P4 ngay với 0đ, reuse `BrevoEmailService` 100% · ② sau đó thêm **ZNS** khi OA sẵn sàng (rẻ + reach cao nhất cho SĐT) · ③ eSMS SMS chỉ nếu cần kênh thuần SMS. Channel-agnostic design cho phép bật cả 3 cùng lúc qua `OtpFallbackChannels`. **→ ĐÃ CHỐT: làm ① trước, thiết kế sẵn ②/③.**

### 6.3 Quyết định review 2026-11-10 (đã duyệt)

1. **Bỏ eSMS SMS làm primary** — hợp lý: cost cao, brandname approval chậm, `not-configured`, delivery bị chặn ~15%, không nằm critical path.
2. **Defer Zalo Login (L1)** — mốc re-evaluate sau ID-P2 (master plan §7 Q3).
3. **KHÔNG bỏ hẳn phone-verify khỏi lộ trình** — email reach thấp với merchant nhỏ → ZNS 300đ là kênh phone rẻ nhất VN, giữ slot L2.
4. **Hardening `OtpService` cũ (ShopERP) cùng wave** — lỗ hổng sống (§8).

### 6.2 Rủi ro kỹ thuật

- **eSMS production keys**: hiện `not-configured` → xem 6.1 chọn kênh; set env trên VPS (KHÔNG commit).
- IMemoryCache Gateway: single instance VPS → OK; nếu scale multi-gateway sau này → chuyển Redis (ghi nợ).
- SMS/ZNS cost: rate-limit chặt (`signup-otp` 3/10min/IP + 5 OTP/ngày/contact); fallback chain có thể gửi 2 kênh/1 request → tính cost cả 2 — chấp nhận được ở volume signup.
- Email OTP: check `ContactEmail` bắt buộc khi channel=email; merchant không có email → bắt buộc SĐT → fallback SMS/ZNS.
- **Lưu ý deliverability email**: Brevo free tier 300/ngày đủ cho MVP; sender domain `noreply@vanan.vn` cần verify SPF/DKIM trong Brevo dashboard nếu mail vào spam.
- **Phạm vi rõ ràng:** ShopERP `OtpService` (customer OTP login `/api/customer-identity/*`) là hệ RIÊNG — **hardening của nó nằm trong §8 của card này (2026-11-10 đã duyệt)** — cùng wave, không phải "hệ khác defer nữa".

---

## 7. HARDENING `OtpService` CŨ (ShopERP — REVIEW B1, 2026-11-10 ĐÃ DUYỆT)

> **Lỗ hổng verified:** `5_WebApps/ShopERP/Services/OtpService.cs:18` — `private static readonly Random _rng = new()` (non-CSPRNG) · IMemoryCache 5' · `VerifyOtp` không attempt counter · không bind `purpose`/`contact`. Luồng customer OTP login + collaborator ĐANG CHẠY production → không phải chuyện defer.
> **Nguyên tắc:** đây là hardening thuần (KHÔNG đổi API contract, KHÔNG đổi Domain) → không cần Gate 5. `CustomerIdentityController` signature giữ nguyên.

### 7.1 Thay đổi

| File | Change |
|---|---|
| `5_WebApps/ShopERP/Services/OtpService.cs` | `Random` → `RandomNumberGenerator.GetInt32(0, 1_000_000)` pad 6 số · thêm attempt counter per OTP (cache key `otp-attempt:{key}`) · max 5 sai → xóa OTP + khóa 5' (`otp-lock:{key}`) · `VerifyOtp(phone, otp)` giữ signature (backward compat) nhưng thêm internal purpose binding: cache key = `otp:{purpose}:{contact}` |
| `5_WebApps/ShopERP/Controllers/CustomerIdentityController.cs` | (chỉ nếu cần) truyền purpose `"customer-login"` / `"customer-upgrade"` vào generate/verify — verify hiện tại call site để nguyên |
| `6_Tests/VanAn.ShopERP.Tests` | +test: generate dùng CSPRNG (không assert giá trị — assert 6 số, unique trong batch) · verify sai 5 lần → khóa · đúng trước 5 → OK · hết hạn 5' → fail · replay sau success → fail (single-use nếu có; hiện OTP xóa sau verify thành công — giữ) |
| `docs/AI/plans/identity-inventory-p0.md` §3.4 | cập nhật trạng thái debt → DONE (sau implement) |

### 7.2 Lưu ý

- **KHÔNG đổi** `IOtpService` interface public (tránh vỡ call sites) — hardening nội bộ: thay RNG, thêm attempt/lock check trong `VerifyOtp` trước khi so sánh code.
- Purpose binding tối thiểu: key namespace `otp:{purpose}:{contact}` — purpose hiện tại 2 loại (`customer-login`, `customer-upgrade`) — controller truyền hằng số; OTP của purpose này không dùng được cho purpose khác.
- State store vẫn IMemoryCache (single instance ShopERP) — ghi nợ Redis khi scale.

### 7.3 Test + RV

- Local: `dotnet test 6_Tests/VanAn.ShopERP.Tests --filter "FullyQualifiedName~Otp"` + build sln 0 errors.
- RV production: customer OTP login flow 1 lần thật (L3 Playwright hoặc manual) — send → verify đúng → login OK; verify sai 5 lần → lock message; regression customer login không OTP (password) không đổi.

---

## 8. VERIFICATION + RV (bắt buộc — user: "verify được khi chạy rv test")

**Local (trước push):**
```powershell
dotnet build VanAn.sln
.\scripts\guard-check.ps1
dotnet test 6_Tests/VanAn.Core.Tests --filter "FullyQualifiedName~Otp|MerchantSignup"
dotnet test 6_Tests/VanAn.ShopERP.Tests --filter "FullyQualifiedName~Otp"
```

**RV production (sau deploy — sweep-first, STOP ở fail đầu):**
- [ ] **L1 markers:** `BrevoEmailOtpChannel`/`FallbackOtpChannel`/`IIdentityOtpChannel` trong CoreHub.dll image mới · OtpService hardening marker (`RandomNumberGenerator` — check IL hoặc test-only)
- [ ] **L2:** health 200 · `/api/v1/merchant-signups/config` trả `requireOtp` + `otpChannel=email`
- [ ] **L3 Playwright prod** (`playwright-rv-*.config.ts` pattern): gửi OTP email tới địa chỉ test → verify → badge "Liên hệ đã xác minh" · sai OTP 5 lần → khóa · `RequireOtp=false` → submit không cần OTP (regression P0)
- [ ] **L4 manual:** check Brevo dashboard delivery log 1 tin thật · email không vào spam (SPF/DKIM)
- [ ] Cleanup: xóa tenant test · ghi kết quả vào project_state
