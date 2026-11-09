# SRS — SELF-SERVE MERCHANT ONBOARDING (OCR + OWNER TỰ KHAI) v1.0

> **Ngày:** 2026-11-09 · **Trạng thái:** DRAFT v1.0 — chờ user duyệt (Step 4 `newfeaturebuild.md`)
> **Nguồn:** Review luồng OCR onboarding 2026-11-09 — user đồng ý toàn bộ đề xuất nâng cấp (P0 self-serve · P1 giảm trường bắt buộc · P2 resilience/dedup)
> **Tiền nhiệm:** `docs/AI/plans/ocr-tenant-onboarding-master-plan.md` (sysadmin wizard — ĐÃ IMPLEMENT)
> **Nguyên tắc:** "những điều chưa rõ → giả định kịch bản ĐƠN GIẢN NHẤT" (mọi giả định đánh dấu **[G#]**)

---

## 1. BỐI CẢNH & MỤC TIÊU

### 1.1 Hiện trạng (đã verify code 2026-11-09)

| Thành phần | Hiện trạng | Vấn đề |
|---|---|---|
| Wizard `/admin/onboarding-ocr` (ShopERP) | SystemAdmin-only, Blazor Server, OCR client-side Tesseract.js `vie+eng`, camera + GPS + MapLibre | **Chủ shop không tự onboard được** — sysadmin phải có mặt tại chỗ, cầm thiết bị admin |
| Credentials | `owner<mst>` + password random — hiện 1 lần cho sysadmin → bàn giao thủ công | Chủ shop không tự đặt mật khẩu; phụ thuộc gặp trực tiếp |
| `PhotoUrls` | Có trong `OcrOnboardingRequest`/`CrawlListingDto`/service mapping | **Wizard không bao giờ gửi** — provenance ảnh = null (plan gốc hứa upload R2/Cloudinary chưa nối) |
| `/claim` (KhachLink) + `TenantRegistration` + ClaimsQueue | Form đăng ký đơn giản (tên/SĐT) → Turnstile + honeypot + rate-limit 5/24h → sysadmin queue | Chỉ là "lead" — không tạo tenant, không OCR, không sản phẩm |
| Pending tenant + verify modal (`TenantManagement.razor`) | List Pending + DirectVerify (sysadmin nhập owner credentials) + tab Duplicates + Resolve | **Đã đủ cho verify-only** — chỉ thiếu hiển thị payload self-serve (ảnh, SĐT, menu) |
| `POST /api/v1/images/upload` (Gateway) | AllowAnonymous, rate-limit `image-upload` 10/h/IP, R2 storage, presigned GET 7 ngày | ✅ Tái dùng được — wizard cần nâng limit cho nhiều ảnh |
| OTP/SMS | `OtpService` (IMemoryCache 5') + `ISmsService`/`EsmsNotificationService` (eSMS) — **NHƯNG `Esms__ApiKey=not-configured` trên production** và `SendOtp` hiện **không gọi SMS** | Kênh SMS chưa vận hành → OTP là dependency, không phải "có sẵn" |
| `OnboardUnverifiedAsync` + `VerifyAsync` | Pending → Verify uniform, hỗ trợ `businessType`/`hkdGroup`, `MarkPotentialDuplicateOf` tự động theo MST | ✅ Tái dùng 100% cho self-serve |

### 1.2 Mục tiêu

**Mục tiêu kinh doanh:** một người bán hàng rong / chủ quán / tiệm tạp hóa — kể cả **không có GPKD, không biết kế toán, chỉ có điện thoại** — tự đăng ký thành tenant trên Vạn An trong ≤ 10 phút, không cần gặp sysadmin, không cần biết thuật ngữ HKD/thuế.

**Mục tiêu kỹ thuật:**
1. Self-serve wizard trên **KhachLink PWA** (public, mobile-first) — tái dùng OCR client-side + camera + GPS.
2. Submit → **Pending tenant** (không Active ngay) → sysadmin **verify-only** trong UI hiện có.
3. Owner **tự đặt username/password** lúc đăng ký → không còn bàn giao credentials.
4. Ảnh giấy tờ (GPKD/biển hiệu/menu) upload R2 → provenance đầy đủ trong `CrawlSource`.
5. Menu OCR → sản phẩm import vào ShopERP sau khi tenant Active.
6. Chống abuse: Turnstile + honeypot + rate-limit + (tùy chọn) OTP SĐT.

---

## 2. PHẠM VI

### 2.1 Trong phạm vi

| # | Nhu cầu | Giải pháp |
|---|---|---|
| F1 | Self-serve wizard public | Trang `/onboard` trên KhachLink (Blazor WASM, cài được PWA) — port wizard + OCR JS |
| F2 | Chọn case A/B như sysadmin wizard | Case A có GPKD / Case B không GPKD (+ option "bán hàng rong chưa đăng ký") |
| F3 | OCR GPKD/biển hiệu/menu client-side | Port `onboarding-ocr.js` + assets Tesseract `vie+eng` sang KhachLink |
| F4 | Upload ảnh provenance | Reuse `POST /api/v1/images/upload` (R2) — cả 2 wizard (self-serve + sysadmin) |
| F5 | Owner tự đặt credentials | Username + password + SĐT tại bước account; password **mã hóa DataProtection** lưu trong `CrawlSource` payload; sysadmin duyệt → backend tự dùng, không lộ password |
| F6 | Sysadmin verify-only có đủ thông tin | Pending tab hiển thị: ảnh giấy tờ, SĐT, menu đã OCR, HKDGroup gợi ý, cờ OTP/duplicate → 1 click duyệt hoặc từ chối |
| F7 | HKDGroup không còn rào cản tri thức | Helper: hỏi doanh thu ước tính + "có nộp thuế GTGT?" → gợi ý group; sysadmin override lúc duyệt |
| F8 | Địa chỉ/GPS linh hoạt | Địa chỉ optional với Case B; lat/lng nhập tay luôn hiện; click-chọn trên bản đồ; GPS chỉ là 1 nguồn |
| F9 | Menu → sản phẩm | ProductLines lưu trong payload; sau duyệt sysadmin/owner import qua `/api/products/import` hiện có |
| F10 | Chống spam/abuse | Turnstile + honeypot + rate-limit `merchant-signup` + giới hạn ảnh + (P4) OTP SĐT |
| F11 | Sysadmin wizard hardening | Nối photo upload (fix `PhotoUrls` null), HKDGroup helper, Case A HKD không auto-Group2, draft autosave, auto-retry import |
| F12 | Trạng thái yêu cầu cho merchant | Trang "theo dõi đăng ký" — xem Pending/Active/Rejected + lý do |
| F13 | **OCR cloud assist (fallback)** | Khi Tesseract client-side confidence thấp → gọi `POST /api/v1/ocr-assist` (Gateway proxy → Gemini Vision / Cloud Vision, provider config) → trả structured JSON; API key server-side only |
| F14 | **On-device OCR upgrade (ngoài trời)** | Đánh giá + tích hợp engine on-device tốt hơn Tesseract cho biển hiệu/menu dưới ánh sáng ngoài trời: ML Kit (nếu có đường native) hoặc PaddleOCR.js/ONNX web WASM — zero server cost, ảnh không rời máy |
| F15 | **Entry points + toggle on/off (user 2026-11-09)** | 3 kênh thu hút merchant: ① CTA trên timlathay.com (Directory Home/StoreFinder/Audit) ② QR deep-link toolkit cho sysadmin field ③ deep-link params `?case=&name=&lat=&lng=&src=` trên `/onboard`. **Sysadmin PHẢI toggle được từng kênh** + master switch — lưu `SystemSetting` global keys (runtime, không restart), admin UI trên ShopERP |

### 2.2 Ngoài phạm vi (ghi nợ)

- Tạo tenant loại mới cho "chưa đăng ký kinh doanh" (domain change lớn — MVP dùng HKD+Group1 + cờ cảnh báo trong payload) [G9]
- Tự động kích hoạt tenant không cần sysadmin (rủi ro abuse — mọi self-serve đều qua Pending)
- PaddleOCR VN server-side / EasyOCR server (không đổi — trên 2GB VPS không khả thi); **on-device upgrade đánh giá riêng ở FR-18/P6**
- Zalo ZNS / kênh notify khác ngoài SMS eSMS
- Đăng ký nhiều chi nhánh / chuỗi cửa hàng trong 1 signup
- Tự động import sản phẩm ngay khi duyệt (MVP: nút import thủ công sau sync — auto là P2+)

---

## 3. GIẢ ĐỊNH — KỊCH BẢN ĐƠN GIẢN NHẤT

| # | Giả định | Mô tả |
|---|---|---|
| [G1] | **Self-serve tạo Pending tenant ngay** | Submit hợp lệ → `OnboardUnverifiedAsync` → Pending (SourceSite `OCR-SELF-*`). Spam bị chặn bởi Turnstile + rate-limit; Pending không Active nên không gây hại. Sysadmin từ chối = tenant không bao giờ Active. **Phương án thay thế (chỉ tạo `TenantRegistration` lead, tenant tạo lúc duyệt) bị loại** vì phải extend entity + payload storage mới — Pending tenant tái dùng toàn bộ pipeline sẵn có. |
| [G2] | **Owner credentials do merchant đặt, lưu tạm mã hóa** | Merchant nhập username+password 1 lần → server `IDataProtection.Protect` → nhúng vào `CrawlSource` payload JSON (cùng cơ chế `QRChannel.EncryptedToken`). Khi sysadmin duyệt: backend `Unprotect` → truyền plaintext vào `VerifyAsync` → **password không bao giờ hiển thị cho sysadmin hay trả về client**. Sau verify, payload vẫn giữ bản mã hóa — rủi ro thấp (cùng trust boundary Gateway PG). |
| [G3] | **Username merchant tự chọn, unique trong tenant** | Username chỉ cần unique trong phạm vi tenant (pattern `owner<mst>` hiện tại cũng vậy). Validate: 3-30 ký tự `[a-z0-9._-]`. Login ShopERP resolve tenant bằng slug/subdomain như hiện tại — **P0 phải verify cơ chế login tenant hiện tại** (nếu login yêu cầu tenant context, trang hướng dẫn sau duyệt phải nói rõ "đăng nhập tại `{slug}.app2...`" hoặc tương đương). |
| [G4] | **OTP SĐT là tùy chọn có cờ config** | `MerchantSignup:RequireOtp` (default `false` ở MVP nếu eSMS chưa config). Khi `true`: bắt buộc OTP verify trước submit. Khi `false`: SĐT lưu `PhoneVerified=false` → sysadmin thấy cảnh báo vàng khi duyệt. **Prerequisite cứng khi bật OTP:** `ESMS_API_KEY`/`ESMS_SECRET_KEY` phải có trên production (hiện `not-configured`). |
| [G5] | **"Bán hàng rong chưa đăng ký" = HKD + Group1 + cờ unregistered** | Wizard Case B có thêm lựa chọn "Tôi chưa đăng ký hộ kinh doanh" → `BusinessType.HouseholdBusiness` + `HKDGroup.Group1` + `unregistered=true` trong payload → sysadmin thấy cảnh báo "có thể chưa đăng ký kinh doanh — kiểm tra pháp lý". KHÔNG tạo loại tenant mới (tránh domain change). |
| [G6] | **Dedup self-serve: Case A chặn ngầm, Case B chỉ gắn cờ** | Case A trùng MST → `OnboardUnverifiedAsync` tự `MarkPotentialDuplicateOf` → tenant rơi vào tab Duplicates, `Verify()` bị chặn tới khi resolve — sysadmin xử lý. Response submit **luôn generic** (không lộ MST đã tồn tại — chống enumeration). Case B trùng tên+địa chỉ+SĐT → warning trong payload + badge trên pending row. |
| [G7] | **Ảnh upload trước submit, URL nhúng payload** | Mỗi ảnh chụp/chọn → upload ngay `POST /api/v1/images/upload?folder=onboarding` → giữ presigned URL (TTL 7 ngày) → submit kèm `photoUrls`. Ảnh lỗi upload → cho phép submit không ảnh (ảnh là provenance bổ sung, không bắt buộc — biển hiệu/menu vẫn OCR được từ file local). **Lưu ý:** presigned URL hết hạn 7 ngày → sysadmin review chậm có thể không xem được ảnh; chấp nhận MVP (ghi nợ: regenerate presigned từ R2 key — endpoint upload hiện chỉ trả URL, P2 có thể trả kèm key). |
| [G8] | **Sản phẩm import thủ công sau duyệt** | `productLines` lưu trong payload. Sau khi tenant Active + NATS sync xong, sysadmin (hoặc owner sau login) bấm import → `POST /api/products/import` (ShopERP, token sysadmin-minted theo pattern wizard hiện tại). Không auto-import trong MVP. |
| [G9] | **Không domain change** | Mọi dữ liệu self-serve nằm trong `CrawlSource.PayloadJson` (đã là JSON tự do) + `CrawlListingDto` mở rộng field optional (CoreHub DTO, không phải Domain entity). `Tenant`, `TenantStatus.Pending`, `BusinessType`, `HKDGroup` dùng nguyên. **Nếu implement phát hiện bắt buộc sửa Domain → STOP, báo user (Gate 5).** |
| [G10] | **Anti-enumeration** | Response submit luôn `200 { message: "Đã ghi nhận..." }` kể cả khi trùng MST/tên — chi tiết duplicate chỉ lộ cho sysadmin trong review UI. |
| [G11] | **ShopERP wizard (sysadmin) giữ nguyên quyền tạo Active ngay** | Track A không đổi behavior: sysadmin tại chỗ vẫn tạo tenant Active luôn + credentials hiện 1 lần (tiện cho onboard hộ). Chỉ hardening UX/data (F11). |
| [G12] | **KhachLink WASM port OCR JS nguyên bản** | `onboarding-ocr.js` + `onboarding-wizard.js` là plain JS — copy sang `5_WebApps/KhachLink/wwwroot/js/`, assets Tesseract `/js/lib/ocr/*` copy nguyên (lazy-load, PWA cache). Blazor interop WASM giống Server (`IJSRuntime.InvokeAsync` + `JSInvokable`). |
| [G13] | **OCR cloud assist = opt-in fallback, provider config** | `OcrAssist:Provider` = `gemini` (default — free tier đủ cho volume signup thấp, trả structured JSON) hoặc `vision` (Cloud Vision DOCUMENT_TEXT_DETECTION — 1.000 ảnh/tháng free, $1.50/1k sau; paid tier không train trên data). Endpoint `POST /api/v1/ocr-assist` chỉ gọi khi client-side confidence < ngưỡng (config `OcrAssist:ConfidenceThreshold`, default 60) hoặc user bấm "Nhận diện nâng cao". Ảnh gửi lên provider = PII (GPKD có MST/tên) → **free tier Gemini được dùng data train — user đã chấp nhận rủi ro này khi duyệt đề xuất 2026-11-09**; có thể đổi `Provider=vision` bất cứ lúc nào không đổi code client. API key chỉ nằm server-side (Gateway env), rate-limit riêng `ocr-assist` (20/h/IP self-serve). |
| [G14] | **On-device OCR upgrade là spike + implement** | Mục tiêu: độ chính xác biển hiệu/menu **ngoài trời** tốt hơn Tesseract (contrast/ánh sáng/xuyên sáng). Ràng buộc: KhachLink là **PWA/browser — ML Kit SDK chính thức chỉ native Android/iOS** → spike đầu tiên đánh giá: (a) PaddleOCR.js/ONNX Runtime Web (WASM, chạy browser, model detect+recognize — kiểm tra model hỗ trợ tiếng Việt), (b) ML Kit chỉ khả thi nếu có kế hoạch wrap native/CAPACITOR (ghi nợ, không block). Tiêu chí chọn: accuracy test trên 10 ảnh biển hiệu/menu thật ngoài trời > Tesseract, size < ~15MB lazy-load, license OK. Kết quả spike quyết định implement hay defer. |
| [G15] | **Entry-point toggles = `SystemSetting` global keys** | 4 keys (value `"true"`/`"false"`, default `"true"` trừ master): `Onboarding.SelfServeEnabled` (master — submit endpoint trả 503 + `/onboard` hiện thông báo đóng) · `Onboarding.DirectoryCtaEnabled` · `Onboarding.QrEntryEnabled` · `Onboarding.DeeplinkPrefillEnabled` (off → `/onboard` bỏ qua query params). Đọc/ghi qua `SystemSetting` entity (TenantId=Empty, pattern `CommerceModeService`) + `IPlatformOnboardingFlagsService` cache ~30s. Public endpoint `GET /api/v1/platform/onboarding-flags` trả 4 bool (Directory/KhachLink đọc); admin `PUT` SystemAdmin-only. QR toggle off → trang QR generator ẩn + link `src=qr` vẫn hoạt động (QR chỉ là deep-link — kill thật là master switch). |

---

## 4. KIẾN TRÚC & LUỒNG DỮ LIỆU

```
MERCHANT (điện thoại)                          SYSDMIN (desktop)
┌─────────────────────────────┐               ┌──────────────────────────────┐
│ KhachLink /onboard (WASM)   │               │ ShopERP /admin/tenants       │
│  B1 chọn case A/B/rong      │               │  tab Pending (mở rộng)       │
│  B2 GPKD / HKDGroup helper  │               │   ├─ Xem hồ sơ signup        │
│  B3 biển hiệu + menu (OCR)  │               │   │  (ảnh R2, SĐT, menu, cờ)  │
│  B4 GPS + bản đồ + địa chỉ  │               │   ├─ Duyệt → verify-signup   │
│  B5 account: user/pass/SĐT  │               │   │  (password auto, không    │
│  B6 review → SUBMIT         │               │   │   lộ — override được)     │
└──────────┬──────────────────┘               │   ├─ Từ chối (lý do)         │
           │ HTTPS                            │   └─ Import sản phẩm (sau   │
           ▼                                  │      NATS sync)             │
┌─────────────────────────────┐               └──────────────┬───────────────┘
│ Gateway (PG source of truth)│                              │
│  POST /api/v1/merchant-     │                              │
│    signups [AllowAnon+      │                              │
│    Turnstile+rate-limit]    │                              │
│  POST .../otp/send|verify   │                              │
│  POST /api/v1/tenants/      │                              │
│    {id}/verify-signup       │                              │
│    [SystemAdmin]            │                              │
│  POST /api/v1/tenants/      │                              │
│    {id}/reject-pending      │                              │
│    [SystemAdmin]            │                              │
│       │                     │                              │
│       ▼                     │                              │
│  MerchantSignupService      │                              │
│   (3_CoreHub/Onboarding)    │                              │
│   → OnboardUnverifiedAsync  │──→ Tenant Pending (PG)       │
│   → CrawlSource payload:    │    SourceSite=OCR-SELF-*     │
│     photos+fields+menu+     │    PayloadJson = full signup │
│     ownerUser+ProtectedPwd  │    (password IDataProtected) │
│   → dedup MST→PotentialDup  │                              │
│   → metering feature.ocr    │                              │
│                             │                              │
│  verify-signup: Unprotect   │◄─────────────────────────────┘
│   pwd → VerifyAsync → Active│    + owner user + groups
│   → Outbox → NATS → SQLite  │
└─────────────────────────────┘
```

**Vai trò lớp (Option C giữ nguyên):** Gateway PG = source of truth tenant; ShopERP SQLite = Products + runtime; OCR 100% client-side; ảnh R2 (provenance, không phải dữ liệu nghiệp vụ).

---

## 5. YÊU CẦU CHỨC NĂNG (FR)

### 5.1 API Gateway — Self-serve (P0)

**FR-1. `POST /api/v1/merchant-signups`** — `[AllowAnonymous]` + `[EnableRateLimiting("merchant-signup")]` + Turnstile verify (reuse `ITurnstileVerificationService`) + honeypot silent-reject.

Request `MerchantSignupRequest`:
```
Case (CompanyWithLicense | SmallShopNoLicense | StreetVendorUnregistered)
Name, Address?, BusinessType, HkdGroup?, SignName?,
GpdkInfo? { Mst, Name, Address, LegalForm, Owner },
Latitude?, Longitude?, PhotoUrls[]?, ProductLines[] {Name, Price, LineNo},
ContactName, ContactPhone, ContactEmail?,
OwnerUsername, OwnerPassword,           // plaintext 1 lần → server Protect ngay
TurnstileToken?, HoneypotWebsite?,
OtpVerifiedPhone?                       // null khi RequireOtp=false
```

Xử lý `MerchantSignupService.SubmitAsync`:
1. Honeypot → fake 200. Turnstile fail → 400.
2. Validate: Case A bắt buộc MST hợp lệ (10/13 số) + Name; Case B/StreetVendor bắt buộc Name + HkdGroup (StreetVendor ép Group1) + SignName/Name.
3. OTP check nếu `RequireOtp=true` → `OtpVerifiedPhone == ContactPhone` else 400.
4. `OnboardUnverifiedAsync(listing{SourceSite="OCR-SELF-GPKD"|"OCR-SELF-SIGN", PhotoUrls, Lat/Lng}, businessType, hkdGroup)`.
   - `CrawlListingDto` thêm optional `SelfServePayload` (record chứa: productLines, contactName/Phone/Email, ownerUsername, ownerPasswordProtected, otpVerified, unregistered flag, clientIpHash, userAgent, signupCase).
5. Metering `feature.ocr` fire-and-forget (như `TenantOcrOnboardingService`).
6. Response luôn generic: `{ message, signupId = tenantId }` — KHÔNG lộ duplicate [G10].

**FR-2. `POST /api/v1/merchant-signups/otp/send` + `/otp/verify`** (P4) — rate-limit `signup-otp` (3/10 phút/phone+IP); gửi SMS qua `ISmsService` (eSMS); OTP IMemoryCache 5' (pattern `OtpService`). Chỉ active khi `RequireOtp=true`.

**FR-3. `GET /api/v1/tenants/{id}/signup-detail`** — `[SystemAdmin]` — trả `SignupDetailDto`: case, ảnh (photoUrls), fields OCR, productLines, contactPhone/Email, ownerUsername, otpVerified, unregistered, duplicate flags, submittedAt, clientIpHash. **TUYỆT ĐỐI không trả password** (kể cả encrypted cũng không cần — chỉ cần flag `hasPassword`).

**FR-4. `POST /api/v1/tenants/{id}/verify-signup`** — `[SystemAdmin]`:
- Đọc `CrawlSource` mới nhất của tenant → parse `SelfServePayload`.
- `Unprotect(ownerPasswordProtected)` → build `VerifyTenantRequest` với credentials merchant (sysadmin override được: `OwnerUsername?`, `OwnerPassword?` plaintext mới, `Slug?`, `ShopInstanceId?`, `OwnerPhone/Email` lấy từ payload nếu không override).
- `VerifyAsync` → Active + owner + groups + NATS.
- Response: `{ tenantId, slug, ownerUsername, usedMerchantPassword: bool }` — **không trả password**.
- Nếu payload không phải self-serve (crawl thường) → 400 "không phải signup tự phục vụ — dùng /verify".

**FR-5. `POST /api/v1/tenants/{id}/reject-pending`** — `[SystemAdmin]` — từ chối Pending tenant self-serve (và crawl): set status phù hợp (`Inactive`/`Rejected` — P0 verify method Domain khả dụng, **không sửa Domain**; nếu không có method → dùng lifecycle hiện có gần nhất, ghi chú trong task card). Lưu `RejectionReason` — cần chỗ lưu: nếu không có cột → lưu vào AuditLog/ghi log + trả reason trong response; **không thêm cột DB nếu tránh được** [G9].

**FR-6. Rate-limit policies mới** (Gateway `Program.cs`):
- `merchant-signup`: 5 requests/24h/IP (pattern `registration-submit`).
- `signup-otp`: 3 requests/10 phút theo phone+IP.
- `onboarding-images`: nâng/riêng cho `image-upload` folder `onboarding*` — 30/h/IP (wizard cần ~4-8 ảnh).

### 5.2 KhachLink `/onboard` — Self-serve wizard (P1)

**FR-7.** Trang public `@page "/onboard"` (KhachLinkLayout, KHÔNG cần login), mobile-first, tiếng Việt.

**FR-8.** 6 bước:
1. **Case select**: 3 card — "Có GPKD" / "Shop nhỏ không GPKD" / "Bán hàng rong chưa đăng ký" (mô tả dễ hiểu, không thuật ngữ thuế).
2. **Giấy tờ/cơ bản**: Case A → chụp GPKD → OCR prefill MST/tên/địa chỉ/loại hình (editable + confidence badge); Case B/Rong → tên + địa chỉ (optional với rong) + **HKDGroup helper** (FR-9).
3. **Biển hiệu + menu**: camera capture hoặc file input (reuse JS pattern `guard-camera`/`onboarding-wizard`); menu 1..n ảnh → bảng sản phẩm review/edit.
4. **Vị trí**: GPS button + bản đồ (MapLibre `vananMap`) + **lat/lng input luôn hiện** + click-chọn marker + địa chỉ text (optional với Case B/Rong).
5. **Account**: họ tên, SĐT (+ nút "Gửi OTP" khi RequireOtp=true), email optional, **username** (check format realtime), **password** (min 8, confirm) — cảnh báo "lưu lại, dùng để đăng nhập sau khi được duyệt".
6. **Review + Submit**: tóm tắt + ảnh đính kèm thumbnail + Turnstile + honeypot → submit → màn "Đã ghi nhận — chờ duyệt (1-2 ngày)" + link trang trạng thái.

**FR-9. HKDGroup helper** (cả self-serve + sysadmin wizard): thay dropdown trần bằng 2 câu hỏi — "Doanh thu ước tính/tháng?" (<5tr / 5-100tr / >100tr / không rõ) + "Có nộp thuế GTGT không?" (không biết/có/không) → gợi ý Group (kèm giải thích 1 câu) → vẫn cho đổi tay. Case A legalForm=HKD → gợi ý thay vì hardcode Group2.

**FR-10. Ảnh → R2 upload**: sau mỗi capture/file → `ImageUploadService` (KhachLink đã có) `POST /api/v1/images/upload?folder=onboarding` → lưu URL; hiển thị thumbnail + nút chụp lại/xóa. Submit kèm `photoUrls`.

**FR-11. Draft autosave**: field text + OCR results + photoUrls lưu `localStorage` key `onboard-draft` (mỗi bước ghi; submit xong/xóa draft xóa key). Ảnh binary không persist — chỉ URL đã upload. Blazor WASM circuit disconnect không mất form.

**FR-12. Trang trạng thái `/onboard/status`**: nhập SĐT (+ OTP khi RequireOtp) hoặc dùng `signupId` từ localStorage → `GET /api/v1/merchant-signups/status?phone=...&signupId=...` → hiển thị Pending/Active(slug)/Rejected(lý do). Endpoint public, rate-limit nhẹ, trả tối thiểu thông tin (anti-enumeration: chỉ trả khi khớp cả phone+signupId hoặc OTP-verified phone).

### 5.3 ShopERP — Sysadmin review & hardening (P2 + P3)

**FR-13. Pending tab mở rộng** (`TenantManagement.razor`):
- `PendingTenantDto` thêm `SourceSite`, `HasSignupPayload`, `SignupSummary` (case, phone masked?, productCount, unregistered, otpVerified) — từ `CrawlSource` mới nhất.
- Nút **"Hồ sơ"** → modal: ảnh (presigned), fields, menu list, SĐT, cờ cảnh báo (duplicate/unregistered/OTP chưa verify) → nút "Duyệt"/"Từ chối".
- "Duyệt" → `verify-signup` (FR-4) với form override tối thiểu (slug, ShopInstanceId bắt buộc chọn — pattern hiện có; password mặc định = merchant's, nút "đặt mật khẩu khác" optional).
- "Từ chối" → `reject-pending` + reason.
- Sau duyệt: nút "Import N sản phẩm" (nếu productLines > 0) → reuse flow `MintSystemAdminTokenAsync` + `/api/products/import` + auto-retry 409.

**FR-14. Sysadmin wizard hardening** (`OcrOnboardingWizard.razor` + JS):
- Wire photo upload → `PhotoUrls` (fix gap — hiện luôn null).
- HKDGroup helper (FR-9) + Case A HKD không auto-Group2.
- Địa chỉ optional Case B; lat/lng manual luôn hiện; map click-chọn.
- Draft autosave sessionStorage.
- Import auto-retry khi 409 (poll ≤15s).
- Case B thêm option "chưa đăng ký" → Group1 + unregistered flag vào request (DTO thêm `bool Unregistered` optional — CoreHub DTO).

### 5.4 OTP SĐT (P4 — phụ thuộc eSMS)

**FR-15.** Khi `MerchantSignup:RequireOtp=true`: `/onboard` bắt buộc OTP trước submit; `otp/send` gọi `ISmsService.SendSmsAsync` (eSMS — cần `ESMS_API_KEY`/`ESMS_SECRET_KEY` production); sai/quá 5' → từ chối; resend cooldown 60s; tối đa 5 lần sai/phone/ngày. `otpVerified` lưu payload → sysadmin thấy badge "SĐT đã xác minh".

**FR-16.** Khi `RequireOtp=false` (MVP nếu eSMS chưa sẵn): SĐT lưu `PhoneVerified=false`; UI sysadmin cảnh báo; vẫn cho duyệt (quyết định con người).

### 5.5 OCR Assist — Cloud fallback (FR-17, P6)

**FR-17. `POST /api/v1/ocr-assist`** — Gateway endpoint mới:
- Auth: self-serve session token (từ signup flow) HOẶC rate-limit `ocr-assist` 20/h/IP cho anonymous; sysadmin wizard dùng SystemAdmin token (không tính quota public).
- Request: `{ kind: "gpdk"|"sign"|"menu", imageBase64 | imageUrl, hints? }` — ảnh đã upload R2 thì gửi `imageUrl` (presigned) tiết kiệm bandwidth.
- `IOcrAssistService` (CoreHub): provider switch `OcrAssist:Provider`:
  - `gemini`: gọi Gemini API (model Flash) với prompt extract structured — response parse về extraction tương đương (`mst/name/address/legalForm/owner` hoặc `signName` hoặc `productLines`).
  - `vision`: Cloud Vision `DOCUMENT_TEXT_DETECTION` → trả text lines → client chạy lại heuristic extraction hiện có (`extractGpdk`/`extractMenu` logic nhận `lines` thay vì file).
- Response thống nhất: `{ lines?: [{text,confidence,bbox}], extraction?: {...}, provider, rawConfidence }` — client merge vào form như OCR local (confidence badge dùng `rawConfidence`).
- **Privacy gate:** request chỉ nhận ảnh khi client gửi `consentCloudOcr=true` (checkbox "Cho phép gửi ảnh lên dịch vụ nhận diện đám mây để chính xác hơn" — unchecked → không gọi). Log không chứa ảnh/PII extract.
- Metering: `feature.ocr` event với metadata `{"assist":"cloud","provider":"..."}`.

### 5.6 On-device OCR upgrade (FR-18, P6 — spike trước)

**FR-18.** Spike đánh giá (time-box ~1 session):
1. Thu thập 10-15 ảnh test thật: biển hiệu quán + menu (ngoài trời, nắng/xuyên sáng) — user cung cấp hoặc chụp tại chỗ.
2. Benchmark Tesseract.js hiện tại vs candidates:
   - **PaddleOCR.js / ONNX Runtime Web** — WASM in-browser; kiểm tra có model nhận diện tiếng Việt (PaddleOCR có `vietnamese` trong danh sách ngôn ngữ — verify) + detect model mobile-size.
   - **ML Kit** — xác nhận chỉ native; nếu user muốn → đánh giá Capacitor wrap (ghi nợ riêng, không block P6).
3. Tiêu chí pass: accuracy fields (tên biển hiệu/menu lines) > Tesseract rõ rệt trên ảnh ngoài trời; lazy-load size hợp lý; không vi phạm license.
4. Pass → implement như engine thứ 2 trong `onboarding-ocr.js` (`extractSignNameV2`/`extractMenuV2` hoặc auto-fallback khi confidence thấp); Fail → ghi kết quả + giữ Tesseract + dựa vào FR-17 cloud assist.

### 5.7 Entry points & toggles (FR-19..22, user chốt 2026-11-09)

**FR-19. Deep-link params trên `/onboard`** (P1):
- Query params: `case` (`a`|`b`|`rong`), `name`, `address`, `lat`, `lng`, `src` (`directory`|`audit`|`qr`|`zalo`|`other`), `shop` (tên hiển thị).
- Validate + sanitize (không HTML, độ dài giới hạn); `lat/lng` range check.
- `DeeplinkPrefillEnabled=false` → bỏ qua toàn bộ params (vẫn vào step 0 bình thường).
- Params prefill step tương ứng: `case` → chọn sẵn card; `name`/`address` → điền step 1; `lat/lng` → điền step 3; `src` → gắn vào request submit.

**FR-20. Directory CTAs (timlathay.com)** — app `5_WebApps/Directory` (Blazor SSR, deploy riêng):
- `GET /api/v1/platform/onboarding-flags` (public, cache 30s) → `{ selfServeEnabled, directoryCta, qrEntry, deeplinkPrefill }`.
- `DirectoryCtaEnabled && SelfServeEnabled` → hiện nút "Chủ cửa hàng? Đăng ký miễn phí →" link `https://<khachlink-domain>/onboard?src=directory`:
  - `Home.razor`: CTA trong hero section.
  - `StoreFinder.razor`: CTA trên store card "Đây là cửa hàng của bạn? Quản lý ngay".
  - `Audit.razor` (`/kiem-tra-cua-hang`): CTA mạnh sau kết quả audit "Sở hữu cửa hàng này? Đăng ký để quản lý →" `?src=audit`.
- Flag off hoặc endpoint lỗi → ẩn CTA (fail-safe: không hiện link chết).

**FR-21. QR toolkit (ShopERP admin)**:
- Trang/section nhỏ trong `/admin/onboarding-ocr` (card "QR mời đăng ký"): form điền `case`/`name`/`address`/`lat`/`lng` → sinh QR PNG trỏ `https://<khachlink-domain>/onboard?src=qr&...` (reuse `qrcode.js` đã có trong KhachLink hoặc lib .NET — kiểm tra sẵn có trong UI.Platform/ShopERP).
- `QrEntryEnabled=false` → card ẩn. QR chỉ là deep-link — tắt thật sự là master switch.
- In/ảnh QR để sysadmin mang theo / dán Zalo / in poster.

**FR-22. Admin toggles (ShopERP)**:
- Card "Kênh đăng ký self-serve" trên `/admin/onboarding-ocr` (SystemAdmin): 4 toggle — Master `SelfServeEnabled` + 3 kênh — lưu `SystemSetting` (`Onboarding.*` keys, TenantId=Empty) qua `PUT /api/v1/platform/onboarding-flags` (SystemAdmin).
- Master OFF → `POST /api/v1/merchant-signups` trả `503 { error: "Đăng ký tự phục vụ tạm đóng" }` + `/onboard` (qua flags endpoint) hiện trang "Tạm đóng đăng ký — vui lòng liên hệ hỗ trợ".
- Mọi thay đổi log `UpdatedBy` + timestamp (SystemSetting có sẵn `UpdatedAt/UpdatedBy`).

---

## 6. DỮ LIỆU & BẢO MẬT

| Dữ liệu | Lưu ở đâu | Bảo vệ |
|---|---|---|
| Ảnh GPKD/biển hiệu/menu | R2 `onboarding/` prefix, presigned GET 7 ngày | Rate-limit upload; private bucket; [ghi nợ: lifecycle xóa sau N ngày — data minimization] |
| Password merchant | `CrawlSource.PayloadJson` → `SelfServePayload.OwnerPasswordProtected` | `IDataProtection.Protect` (cùng pattern `QRChannel.EncryptedToken`); không log, không trả API, không hiển thị UI; xóa khỏi payload sau verify? — **[G2 quyết: GIỮ]** (audit trail; rủi ro thấp trong PG) |
| SĐT chủ shop | `TenantSettings.ContactPhone` sau verify (consent — merchant tự nhập) + `CrawledPhone` internal | Pattern M3 hiện có |
| OTP | IMemoryCache Gateway 5' | Không DB; rate-limit send |
| Turnstile | `ITurnstileVerificationService` | Reuse production config |
| OCR assist provider keys | `OcrAssist:GeminiApiKey` / `OcrAssist:VisionApiKey` env Gateway | Server-side only; không commit; rotate được |
| Ảnh gửi cloud OCR | Provider (Google) xử lý transient | Consent checkbox bắt buộc; provider config chọn được (`vision` = không train); không log ảnh |
| PII trong payload | `clientIpHash` (SHA256+salt, không lưu IP thô) + userAgent rút gọn | Anti-abuse audit |

**Anti-abuse tổng hợp:** Turnstile + honeypot + `merchant-signup` 5/24h/IP + `signup-otp` limit + image-upload limit + pending-only (không Active tự động) + sysadmin duyệt bắt buộc + dedup MST.

---

## 7. YÊU CẦU PHI CHỨC NĂNG

- **NFR-1 Mobile-first:** wizard chạy tốt trên Android Chrome tầm trung; Tesseract WASM lazy-load (không chặn first paint); ảnh preprocess cap 2400px.
- **NFR-2 PWA:** `/onboard` cài được (manifest + SW KhachLink đã có); draft autosave chịu circuit disconnect.
- **NFR-3 Bảo mật:** mọi endpoint self-serve AllowAnonymous phải rate-limit + Turnstile; không endpoint nào trả password/protected payload; `verify-signup`/`reject-pending`/`signup-detail` SystemAdmin-only.
- **NFR-4 Không phá vẹn hiện có:** Track A wizard + crawl pipeline + `/claim` + TenantRegistration không regression; `CrawlListingDto` field mới đều optional (backward compat serialization).
- **NFR-5 Audit:** mọi submit/verify/reject log structured (tenantId, sysadminId, case, source) + metering `feature.ocr`.
- **NFR-6 i18n:** 100% tiếng Việt, ngôn ngữ người bán (không "HKDGroup", không "GPKD" khó hiểu — tooltip giải thích).

---

## 8. RỦI RO

| # | Rủi ro | Mức | Giảm thiểu |
|---|---|---|---|
| R1 | eSMS chưa config → OTP không khả thi | Cao (nếu RequireOtp=true) | MVP `RequireOtp=false` + cảnh báo sysadmin; P4 chỉ bật khi có ESMS keys |
| R2 | Spam Pending tenant | TB | Turnstile+honeypot+rate-limit+pending-only; sysadmin reject 1 click; dọn Pending >30 ngày (ghi nợ job) |
| R3 | Merchant quên password đã đặt | TB | Password chỉ dùng sau duyệt; trang status hướng dẫn "quên MK → liên hệ hỗ trợ reset" (reset flow = ghi nợ P2) |
| R4 | Ảnh presigned hết hạn 7 ngày trước khi sysadmin review | TB | [G7] chấp nhận; P2 trả kèm R2 key để regenerate |
| R5 | OCR biển hiệu chữ cách điệu sai | TB | Human review bắt buộc; tên hiệu editable; không ảnh hưởng pháp lý (sysadmin duyệt) |
| R6 | Username trùng trong tenant khác | Thấp | Unique per-tenant [G3]; verify login resolution P0 |
| R7 | Pending tenant trùng MST không verify được | Thấp | `MarkPotentialDuplicateOf` → tab Duplicates → resolve flow có sẵn |
| R8 | WASM Tesseract nặng trên máy yếu | TB | Lazy-load + PWA cache + cho phép "nhập tay bỏ qua OCR" mọi bước |
| R9 | Ảnh GPKD (PII) gửi Google qua OCR assist | TB | Consent checkbox; `Provider=vision` option (paid không train); chỉ fallback khi confidence thấp; user đã chấp nhận free-tier Gemini 2026-11-09 |
| R10 | On-device engine mới (PaddleOCR.js) quá nặng/chậm mobile | TB | Spike time-boxed; size budget <15MB; fallback Tesseract + cloud assist |

---

## 9. CÂU HỎI CẦN USER CHỐT (trước IMPLEMENT)

| # | Câu hỏi | Đề xuất |
|---|---|---|
| Q1 | OTP SĐT: có ESMS API key/secret để config production không? | Nếu chưa → MVP `RequireOtp=false` (P4 defer), vẫn đủ chống abuse nhờ Turnstile+rate-limit+sysadmin duyệt |
| Q2 | Self-serve tạo Pending tenant ngay khi submit (đề xuất [G1]) hay chỉ tạo lead chờ sysadmin? | Pending ngay — tái dùng toàn bộ pipeline, dedup sớm |
| Q3 | "Bán hàng rong chưa đăng ký" map HKD+Group1+cờ cảnh báo [G5] — chấp nhận? | Chấp nhận MVP; loại tenant riêng = domain change lớn, roadmap sau |
| Q4 | Sau duyệt, ai import sản phẩm từ menu? | Sysadmin bấm nút trong review (MVP); owner tự import sau login (P2); auto = P3+ |
| Q5 | Giữ encrypted password trong payload sau verify [G2]? | Giữ — audit; rủi ro thấp trong PG trust boundary |
| Q6 | OCR assist provider: Gemini free tier (data có thể train) hay Cloud Vision paid ($1.50/1k ảnh, không train)? | Config `OcrAssist:Provider` — default `gemini` cho MVP (free, structured JSON tốt nhất); đổi `vision` chỉ bằng config khi cần. User đã chấp nhận trade-off 2026-11-09 |
| Q7 | On-device OCR engine: ML Kit không chạy browser — chấp nhận spike PaddleOCR.js/ONNX web (P6) thay vì native wrap? | Đồng ý theo đề xuất — spike quyết định implement/defer |

---

## 10. TRACEABILITY

| FR | Task card | Phase |
|---|---|---|
| FR-1,3,4,5,6 | `task_p0_signup_api_core.md` | P0 |
| FR-2,15,16 | `task_p4_otp_phone_verification.md` | P4 |
| FR-7..12 | `task_p1_khachlink_wizard.md` | P1 |
| FR-13 | `task_p2_shoperp_review_ux.md` | P2 |
| FR-14 | `task_p3_shoperp_wizard_hardening.md` | P3 |
| FR-2,15,16 | `task_p4_otp_phone_verification.md` | P4 |
| FR-17,18 | `task_p6_ocr_quality_upgrade.md` | P6 |
| FR-19 | `task_p1_khachlink_wizard.md` (§3.9) | P1 |
| FR-20,21,22 + flags API | `task_p2_shoperp_review_ux.md` (§3.7-3.9) + `task_p0` (§3.8) | P0+P2 |
| Tất cả | `task_p5_e2e_rv.md` | P5 |
