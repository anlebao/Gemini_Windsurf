# MASTER PLAN: SELF-SERVE MERCHANT ONBOARDING (OCR + OWNER TỰ KHAI)

> **Created:** 2026-11-09
> **SRS:** `docs/requirements/van_an_self_serve_onboarding_srs_v1.md` v1.0 (DRAFT — chờ duyệt)
> **Workflow:** `newfeaturebuild.md` (ANALYZE → IMPLEMENT)
> **Tiền nhiệm:** `ocr-tenant-onboarding-master-plan.md` (sysadmin wizard — ĐÃ IMPLEMENT + deploy)
> **Status:** ⏳ Chờ user duyệt SRS §9 (Q1-Q5) + plan này (Step 4)
> **Domain modification:** **KHÔNG** (SRS [G9] — mọi dữ liệu trong `CrawlSource.PayloadJson` + `CrawlListingDto` mở rộng optional; nếu implement buộc sửa Domain → STOP báo user, Gate 5)

---

## 1. MỤC TIÊU

Biến onboarding tenant từ "sysadmin phải có mặt tại chỗ" thành **self-serve**: merchant tự chụp GPKD/biển hiệu/menu trên điện thoại → tạo **Pending tenant** → sysadmin verify-only → owner dùng credentials **tự đặt** đăng nhập. Đối tượng: shop/quán/tiệm/người bán hàng rong nhỏ, không GPKD, không biết thuật ngữ thuế.

**Nguyên tắc:** tái dùng tối đa pipeline hiện có (Pending→Verify, CrawlSource provenance, R2 image upload, Turnstile, rate-limit, ClaimsQueue/TenantManagement pending UI, `/api/products/import`). Chỉ thêm: 1 public submit endpoint + 1 verify-signup endpoint + 1 reject endpoint + 1 signup-detail endpoint + trang `/onboard` KhachLink + nâng cấp pending review UI.

---

## 2. HIỆN TRẠNG (đã verify 2026-11-09)

| Thành phần | Tái dùng | Gap cần làm |
|---|---|---|
| `OnboardUnverifiedAsync` / `VerifyAsync` (`TenantOnboardingService`) | ✅ 100% — đã hỗ trợ businessType/hkdGroup/PhotoUrls | Không |
| `TenantOcrOnboardingService` (Pending→Verify 1 call, sysadmin) | Pattern tham chiếu | Self-serve cần tách: submit (Pending only) ≠ verify (sysadmin) |
| `CrawlListingDto` + `CrawlSource.PayloadJson` | ✅ provenance sẵn | Thêm optional `SelfServePayload` field (CoreHub DTO) |
| `IDataProtection` (QRChannel.EncryptedToken pattern) | ✅ protect/unprotect | Dùng cho owner password |
| `ITurnstileVerificationService` + honeypot + `registration-submit` rate-limit | ✅ pattern `/claim` | Policy mới `merchant-signup` |
| `POST /api/v1/images/upload` (R2, AllowAnon, 10/h/IP) | ✅ | Nâng limit cho folder `onboarding` (~30/h) + trả kèm R2 key [G7] |
| `PendingTenantController` (`/api/v1/tenants/pending`, `/verify`, `/duplicates`) | ✅ list + direct verify | Thêm `/verify-signup`, `/reject-pending`, `/signup-detail` |
| `TenantManagement.razor` pending tab + verify modal + duplicates | ✅ UI khung | Mở rộng: signup payload viewer + verify-signup + reject + import products |
| `OcrOnboardingWizard.razor` + `onboarding-ocr.js` + `onboarding-wizard.js` | ✅ Track A giữ nguyên | Hardening F11 (photo upload, HKDGroup helper, GPS manual, autosave, auto-retry) |
| KhachLink `/claim` + `Register.razor` + Turnstile JS + `ImageUploadService` | ✅ host + anti-bot + upload client | Trang `/onboard` mới + port OCR JS/assets |
| `ISmsService`/`EsmsNotificationService` + `OtpService` pattern | ⚠️ code có, **eSMS `not-configured` prod** | P4: config keys + wire `SendOtp` gọi SMS thật + `RequireOtp` flag |
| `ProductImportController` `/api/products/import` + `MintSystemAdminTokenAsync` | ✅ | Nút import trên review UI |
| Metering `feature.ocr` | ✅ pattern `TenantOcrOnboardingService` | Ghi event ở submit (tenantId = pending) |

---

## 3. QUYẾT ĐỊNH THIẾT KẾ (chi tiết trong SRS §3-§5)

| # | Quyết định | Lý do |
|---|---|---|
| D1 | Self-serve submit → **Pending tenant ngay** (không qua `TenantRegistration`) | Tái dùng pending UI + dedup MST tự động + không extend entity; spam kiểm soát bằng Turnstile/rate-limit/pending-only |
| D2 | Owner password merchant đặt → `IDataProtection.Protect` → `CrawlSource` payload; verify-signup `Unprotect` → `VerifyAsync` | Password không lộ cho sysadmin/client; zero domain change; cùng trust boundary Gateway PG |
| D3 | Self-serve = trang mới `/onboard` trên **KhachLink WASM** | Đúng đối tượng (mobile/PWA); OCR JS plain → port nguyên; Track A sysadmin wizard không đụng |
| D4 | OTP sau cờ `MerchantSignup:RequireOtp` (default false MVP) | eSMS chưa config prod; anti-abuse đã đủ bằng Turnstile+rate-limit+sysadmin duyệt; P4 bật khi có keys |
| D5 | "Bán hàng rong chưa đăng ký" = case thứ 3 → HKD+Group1+`unregistered` flag trong payload | Tránh domain change; sysadmin thấy cảnh báo pháp lý |
| D6 | Dedup: Case A MST → `MarkPotentialDuplicateOf` tự động (tab Duplicates, Verify bị chặn → resolve flow có sẵn); response submit luôn generic | Chống enumeration + tái dùng duplicate pipeline |
| D7 | Ảnh upload R2 ngay khi chụp → URL vào payload; submit không bắt buộc ảnh | Provenance đầy đủ; offline/ảnh lỗi không chặn signup |
| D8 | Sản phẩm import = nút thủ công sau duyệt+sync (không auto) | Tránh cross-service call phức tạp; owner cũng tự import được sau login |
| D9 | Reject pending = endpoint mới `reject-pending` + reason vào log/AuditLog (không thêm cột nếu không bắt buộc) | [G9] zero schema change ngoài những gì EF đã có |

---

## 4. SCOPE — PHASES + TASK CARDS

| Phase | Nội dung | Layer | Task card | Phụ thuộc |
|---|---|---|---|---|
| **P0** | **Signup API core (Gateway + CoreHub)** — `MerchantSignupRequest/Result/SelfServePayload/SignupDetailDto` · `CrawlListingDto.SelfServePayload` · `MerchantSignupService` (validate + dedup + OnboardUnverifiedAsync + payload + metering) · `MerchantSignupController` (`POST /api/v1/merchant-signups` AllowAnon+Turnstile+rate-limit) · `GET signup-detail` + `POST verify-signup` + `POST reject-pending` (SystemAdmin) · rate-limit policies · **`GET/PUT /api/v1/platform/onboarding-flags` + `IPlatformOnboardingFlagsService` (SystemSetting `Onboarding.*` keys — 4 toggles: master + directory-cta + qr-entry + deeplink-prefill)** · Core.Tests | 3_CoreHub + 2_Gateway | `task_p0_signup_api_core.md` | — |
| **P1** | **KhachLink `/onboard` wizard** — port OCR JS + Tesseract assets · 6 bước (case/GPKD+HKDGroup helper/biển+menu/GPS+map/account/review) · photo→R2 upload · Turnstile+honeypot · draft autosave · `/onboard/status` + status endpoint · HKDGroup helper · **deep-link params `?case&name&address&lat&lng&src` + flags check (đóng khi master off)** · OCR-assist hook sẵn | 5_WebApps/KhachLink | `task_p1_khachlink_wizard.md` | P0 (API contract) |
| **P2** | **Sysadmin review UX + kênh entry (ShopERP + Directory)** — `PendingTenantDto`+`signup-detail` · pending tab: badge nguồn self-serve/cảnh báo · modal hồ sơ (ảnh+menu+SĐT+flags) · Duyệt→`verify-signup` (override slug/ShopInstance/password optional) · Từ chối→`reject-pending` · Import sản phẩm sau duyệt · bUnit · **card toggles 4 kênh trên `/admin/onboarding-ocr`** · **QR generator (deep-link `?src=qr&...`)** · **Directory CTAs (Home/StoreFinder/Audit — app riêng `5_WebApps/Directory`)** | 5_WebApps/ShopERP + 5_WebApps/Directory | `task_p2_shoperp_review_ux.md` | P0 |
| **P3** | **Sysadmin wizard hardening (Track A)** — wire `PhotoUrls` upload · HKDGroup helper + bỏ auto-Group2 · địa chỉ optional Case B · lat/lng manual + map click · draft autosave · auto-retry import 409 · option "chưa đăng ký" | 5_WebApps/ShopERP | `task_p3_shoperp_wizard_hardening.md` | độc lập (có thể song song P1/P2) |
| **P4** | **OTP SĐT** — `otp/send`+`otp/verify` (Gateway, `ISmsService` eSMS) · `RequireOtp` config · wire `SendOtp` gọi SMS thật · UI OTP step trong `/onboard` · **PREREQ: ESMS_API_KEY/SECRET production** | 2_Gateway + 3_CoreHub + KhachLink | `task_p4_otp_phone_verification.md` | P0 + P1 + ESMS keys |
| **P5** | **E2E + deploy + RV** — spec `merchant-onboarding.spec.ts` (self-serve submit→pending→sysadmin verify→login owner) + hardening spec · guard + build + full tests · CD Multi-VPS · RV L1-L5 production | 6_Tests + production | `task_p5_e2e_rv.md` | P0-P3 (P4 nếu bật) |
| **P6** | **OCR quality upgrade** — `POST /api/v1/ocr-assist` (provider switch Gemini/Vision, consent gate, rate-limit, metering) · client fallback khi confidence thấp + nút "Nhận diện nâng cao" · **spike on-device**: PaddleOCR.js/ONNX web vs ML Kit-feasibility trên ảnh ngoài trời → implement hoặc defer | 2_Gateway + 3_CoreHub + KhachLink + ShopERP wizard | `task_p6_ocr_quality_upgrade.md` | P0 + P1 (có thể song song P4/P5) |

**Dependency graph:** `P0 → {P1, P2}` · `P3` độc lập · `P4` sau P0+P1 · `P6` sau P0+P1 (song song được với P4/P5) · `P5` cuối (bao gồm P6 nếu kịp — nếu không P6 deploy sau, không block go-live).
**Song song hóa:** sau khi P0 chốt API contract → P1 ∥ P2 ∥ P3 (3 session riêng).

---

## 5. THIẾT KẾ CHI TIẾT

### 5.1 Data flow self-serve (P0+P1)

```
/onboard submit
  → POST /api/v1/merchant-signups {case, fields, photoUrls, productLines,
       ownerUsername, ownerPassword(plaintext→Protect ngay trong service),
       turnstileToken, honeypot}
  → MerchantSignupService:
      honeypot? → fake 200
      turnstile? → 400
      validate per case (Case A: MST 10/13; B/Rong: Name+HkdGroup)
      RequireOtp? → check otpVerifiedPhone==ContactPhone
      listing = CrawlListingDto(SourceSite="OCR-SELF-GPKD"|"OCR-SELF-SIGN",
                 PhotoUrls, Lat/Lng, TaxCode=MST|"")
              + SelfServePayload{productLines, contacts, ownerUsername,
                 ownerPasswordProtected=protector.Protect(pwd),
                 otpVerified, unregistered, case, ipHash, ua}
      tenantId = OnboardUnverifiedAsync(listing, bt, hkdGroup)
         └─ MST trùng → MarkPotentialDuplicateOf (tự động, có sẵn)
      metering.RecordUsageAsync("feature.ocr") fire-and-forget
  → 200 { message:"Đã ghi nhận hồ sơ. Chúng tôi sẽ liên hệ/xác nhận trong 1-2 ngày làm việc.", signupId }

Sysadmin /admin/tenants → tab Pending → row badge "Self-serve"
  → "Hồ sơ" → GET /api/v1/tenants/{id}/signup-detail
       → modal: ảnh thumbnails, fields, menu, SĐT, flags (unregistered/otp/dup)
  → "Duyệt" → POST /api/v1/tenants/{id}/verify-signup
       { slug?, shopInstanceId (bắt buộc), ownerUsername?, ownerPassword?, ownerDisplayName? }
       → service: parse payload → Unprotect pwd → VerifyAsync
         (OwnerPhone/Email từ payload; override nếu sysadmin nhập)
       → tenant Active + owner + groups + NATS → SQLite sync
       → response { slug, ownerUsername, usedMerchantPassword:true }
       → UI: hiện slug + "hướng dẫn owner đăng nhập" + nút Import sản phẩm
  → "Từ chối" → POST /api/v1/tenants/{id}/reject-pending { reason }
```

### 5.2 `SelfServePayload` (record trong `CrawlListingDto` — optional, backward compat)

```csharp
public record SelfServePayload(
    string SignupCase,                    // "CompanyWithLicense"|"SmallShopNoLicense"|"StreetVendorUnregistered"
    List<OcrProductLineDto> ProductLines,
    string ContactName, string ContactPhone, string? ContactEmail,
    string OwnerUsername,
    string OwnerPasswordProtected,        // IDataProtection-protected blob (base64)
    bool OtpVerified,
    bool Unregistered,                    // bán hàng rong chưa đăng ký HKD
    string? ClientIpHash, string? UserAgent);
```

Serialization: `JsonSerializer.Serialize(listing)` trong `OnboardUnverifiedAsync` đã tự chứa field mới → `CrawlSource.PayloadJson` đủ dữ liệu, **không cần cột/bảng mới**.

### 5.3 Verify-signup edge cases

- Tenant không phải self-serve (crawl thường, `SelfServePayload=null`) → 400.
- Tenant không Pending → 409.
- `PotentialDuplicateOf != null` → `Verify()` domain guard chặn → trả 409 + hướng dẫn resolve duplicate trước.
- Password override: sysadmin nhập password mới (plaintext trong request, chỉ dùng 1 lần) → `usedMerchantPassword=false`.
- Username trùng trong tenant → `CreateUserAsync` throw → 409 message rõ.

### 5.4 ShopERP pending UI mở rộng

- `PendingTenantDto` += `SourceSite?`, `HasSignupPayload`, `SignupCase?`, `ProductCount?`, `Unregistered?`, `OtpVerified?` — join `CrawlSources` mới nhất per tenant (1 query).
- Row badge: `Self-serve` / `OCR-GPKD` / `OCR-SIGN` / crawl sites.
- Modal "Hồ sơ": ảnh (presigned GET — TTL 7 ngày, cảnh báo nếu cũ), form fields read-only, menu table, flags cảnh báo.
- Verify-signup form: tái dùng `VerifyForm` + thêm checkbox "Dùng mật khẩu merchant đã đặt" (default ON — backend tự Unprotect; OFF → hiện ô nhập password mới).

### 5.5 Tests

- **Core.Tests (P0):** submit Case A happy → Pending + payload đủ + MST dup → PotentialDuplicateOf + response generic · Case B/Rong validate · Turnstile fail · honeypot · rate-limit policy tồn tại · verify-signup: unprotect→VerifyAsync→Active+owner login được · override password · non-self-serve → 400 · not-pending → 409 · dup-blocked → 409 · reject-pending · signup-detail không chứa password/protected blob · metering event fired.
- **ShopERP.Tests (P2):** bUnit pending tab render badge/modal; verify-signup flow mock.
- **KhachLink (P1):** component test wizard step transitions + validation (nếu có test infra WASM — kiểm tra; fallback: manual + E2E).
- **E2E (P5):** `merchant-onboarding.spec.ts` — mock OCR (điền tay fields) → submit → sysadmin verify → owner login → dashboard.

---

## 6. EXECUTION STRATEGY

| Session | Phase | Deliverable | Validation |
|---|---|---|---|
| 1 | P0 | Signup API + service + tests | guard-check + `dotnet build VanAn.sln` + Core.Tests |
| 2 | P1 ∥ P2 ∥ P3 | 3 nhánh song song (API contract P0 đã chốt) | mỗi nhánh: build + tests nhánh mình |
| 3 | P4 (nếu ESMS keys sẵn) | OTP end-to-end | build + SMS thật staging/prod |
| 4 | P5 | E2E + deploy + RV L1-L5 | CD Multi-VPS + RV production |

**Mỗi session:** restate objective · max 10 files/phase · guard-check trước submit · không mix phase.

---

## 7. SUCCESS CRITERIA

1. ✅ Merchant không login mở `/onboard` trên điện thoại → hoàn thành signup ≤ 10 phút (không cần gặp ai).
2. ✅ Case B/Rong không cần MST/GPKD; HKDGroup có helper gợi ý; địa chỉ optional với rong.
3. ✅ Submit → Pending tenant + `CrawlSource` payload đầy đủ (ảnh R2 URLs + menu + credentials protected).
4. ✅ Sysadmin thấy đủ hồ sơ trong pending tab → Duyệt 1 click → tenant Active + owner login bằng credentials **merchant tự đặt** (sysadmin không thấy password).
5. ✅ Từ chối có lý do; trùng MST tự vào tab Duplicates.
6. ✅ Ảnh giấy tờ xem được trong review modal (presigned R2).
7. ✅ Sysadmin wizard Track A: `PhotoUrls` có dữ liệu, HKDGroup helper, GPS manual, autosave — không regression.
8. ✅ Guard-check + build + Core.Tests/ShopERP.Tests PASS; E2E PASS; CD deploy + RV PASS.
9. ✅ **Zero Domain.cs modification** (hoặc STOP + báo user nếu bất khả kháng).

---

## 8. RISK ASSESSMENT

| Risk | Impact | Mitigation |
|---|---|---|
| eSMS chưa config → P4 block | OTP không bật được | MVP `RequireOtp=false`; P4 = session riêng sau khi có keys |
| Password protected blob trong PG payload | rò rỉ nếu DB lộ | DataProtection keys ngoài DB; payload không trả API; log không chứa |
| Spam signup vượt rate-limit (NAT chung IP) | Pending rác | Turnstile + honeypot + pending-only + reject nhanh + [ghi nợ: cleanup job Pending>30d] |
| WASM Tesseract assets copy sang KhachLink làm nặng deploy | image size | Lazy-load on-demand; PWA cache; assets đã có sẵn pattern ShopERP |
| `verify-signup` lộ password qua log | security | Log chỉ tenantId/sysadminId/usedMerchantPassword — KHÔNG log password/protected blob |
| Login tenant resolution chưa rõ (slug/subdomain?) | owner không biết đăng nhập ở đâu | **P0 task đầu tiên: verify cơ chế login ShopERP + ghi URL đăng nhập vào response/status page** |
| Presigned URL hết hạn trước review | không xem được ảnh | Chấp nhận MVP; P2 bổ sung trả R2 key → regenerate endpoint |
| Ảnh GPKD (PII) gửi Google OCR assist | privacy | Consent checkbox bắt buộc; `OcrAssist:Provider=vision` option (paid không train); chỉ fallback confidence thấp; user chấp nhận Gemini free-tier 2026-11-09 |
| On-device OCR engine mới quá nặng/chậm | UX mobile | Spike time-boxed P6; size budget <15MB lazy; fallback Tesseract + cloud assist |

---

## 9. RV PLAN

| Layer | Test | Expected |
|---|---|---|
| L1 | Markers: `MerchantSignupService`/`MerchantSignupController` trong CoreHub.dll/Gateway.dll; `/onboard` route trong KhachLink | Present |
| L2 | Health 200 · `/onboard` 200 public · `POST /api/v1/merchant-signups` no-token+no-turnstile → 400/403 · `verify-signup` no-auth → 401 | PASS |
| L3 | Playwright production: `/onboard` → điền form (mock OCR) → submit → Pending xuất hiện → sysadmin verify-signup → owner login app2 → dashboard | PASS |
| L4 | Manual: chụp ảnh thật GPKD/biển hiệu/menu trên mobile → OCR → signup → sysadmin review ảnh → duyệt → owner login | User |
| L5 | Regression: sysadmin wizard `/admin/onboarding-ocr` vẫn hoạt động + `PhotoUrls` giờ có data | PASS |

---

## 10. OPEN QUESTIONS → SRS §9

Q1 ESMS keys · Q2 Pending-ngay (đề xuất) · Q3 unregistered=HKD+Group1 · Q4 ai import sản phẩm · Q5 giữ encrypted password.

**Sau khi user chốt Q1-Q5 → update SRS v1.1 → bắt đầu P0.**
