# Task P2: ShopERP Sysadmin Review UX — Pending Signup Detail + Verify-Signup + Reject

> **SRS:** `docs/requirements/van_an_self_serve_onboarding_srs_v1.md` — FR-13
> **Master plan:** `docs/AI/plans/self-serve-onboarding-master-plan.md` §5.4
> **Mode:** IMPLEMENT (sau P0)
> **Layer:** 5_WebApps/ShopERP (Blazor Server, Admin)
> **Prerequisite:** P0 DONE (`signup-detail`, `verify-signup`, `reject-pending` endpoints)

## 1. OBJECTIVE

Nâng cấp tab "Pending" trong `/admin/tenants`: hiển thị hồ sơ self-serve đầy đủ (ảnh R2, menu OCR, SĐT, cờ cảnh báo) → sysadmin **Duyệt** (verify-signup dùng credentials merchant — không lộ password) hoặc **Từ chối** (kèm lý do) → sau duyệt import sản phẩm từ menu.

## 2. FILES

### Sửa
| File | Change |
|---|---|
| `5_WebApps/ShopERP/Services/…ClaimApi/PendingApi client` (tìm file chứa `ListPendingAsync`/`DirectVerifyAsync` — pattern `ClaimApi`) | Thêm methods: `GetSignupDetailAsync`, `VerifySignupAsync`, `RejectPendingAsync`; mở rộng `PendingTenantApiDto` (SourceSite, HasSignupPayload, SignupCase, ProductCount, Unregistered, OtpVerified, DuplicateWarning) |
| `5_WebApps/ShopERP/Components/Pages/Admin/TenantManagement.razor` | Pending tab: badge nguồn + cột cảnh báo; nút "Hồ sơ" mở modal; verify-signup form; reject modal; import products sau duyệt |
| `5_WebApps/ShopERP/Components/Pages/Admin/TenantManagement.razor.cs` (nếu code-behind tồn tại) hoặc `@code` | State + handlers mới |

### Mới (nếu tách component)

- `Components/Shared/SignupDetailModal.razor`
- `Components/Shared/OnboardingFlagsCard.razor` + `Components/Shared/QrSignupCard.razor` (toggles + QR generator)
- `Services/Http/OnboardingFlagsHttpService.cs`

### Directory app (`5_WebApps/Directory` — SSR riêng)

- `Components/Shared/OnboardCta.razor` (CTA dùng chung)
- `Services/OnboardingFlagsClient.cs` (typed HttpClient → Gateway flags endpoint)
- Sửa `Components/Pages/Home.razor`, `StoreFinder.razor`, `Audit.razor` — chèn CTA

| File | Nội dung |
|---|---|
| `Components/Pages/Admin/SignupDetailModal.razor` (+css) | Modal hồ sơ self-serve: ảnh gallery, fields, menu table, flags, nút Duyệt/Từ chối — **UI Platform components 100%** (Gate: cấm HTML/CSS tự chế ngoài platform) |

## 3. CHANGES CHI TIẾT

### 3.1 Pending list mở rộng

- `PendingTenantDto` từ P0 đã có `SourceSite`/`HasSignupPayload`/flags — map vào `PendingTenantApiDto`.
- Badge trên row: `Self-serve` (xanh dương) / `OCR-GPKD` / `OCR-SIGN` / crawl site name; icon ⚠ khi `Unregistered`/`DuplicateWarning`/`!OtpVerified`(khi RequireOtp bật thì bắt buộc, không thì chỉ hiển thị).
- Cột mới (optional, ẩn được): "Sản phẩm" (count từ payload), "Kênh" (self-serve/crawl).

### 3.2 Modal "Hồ sơ" (`SignupDetailModal`)

Data từ `GET /api/v1/tenants/{id}/signup-detail`:
- **Header:** tenant name + case label + submittedAt + badges (Unregistered ⚠ "chưa đăng ký kinh doanh — kiểm tra pháp lý" / OTP verified / duplicate warning).
- **Ảnh giấy tờ:** gallery thumbnails từ photoUrls (presigned — nếu 403/hết hạn hiển thị "ảnh hết hạn — liên hệ merchant gửi lại"); click → full size.
- **Thông tin:** tên/địa chỉ/MST/legalForm/owner (Case A); tên hiệu/nhóm thuế gợi ý; SĐT + email; GPS lat/lng (+ link Google Maps `https://maps.google.com/?q=lat,lng` — URL generated cho programming purpose, OK).
- **Menu OCR:** bảng name+price (read-only) + count.
- **Account:** ownerUsername + "merchant đã đặt mật khẩu — sẽ dùng khi duyệt" (KHÔNG có nút xem password).
- **Actions:** "Duyệt & Kích hoạt" / "Từ chối".

### 3.3 Duyệt → verify-signup

- Mở `VerifyForm` hiện có + thêm:
  - `ShopInstanceId` bắt buộc chọn (giữ rule hiện tại).
  - `Slug` auto-generate (giữ).
  - Toggle "Dùng mật khẩu merchant đã đặt" (default ON) → OFF mới hiện ô nhập password mới + confirm.
  - `OwnerDisplayName` default = tenant name.
- Submit → `POST verify-signup` → thành công: đóng modal, refresh pending, hiện result box: **slug** + ownerUsername + nút "Sao chép thông tin đăng nhập cho merchant" (username + hướng dẫn — KHÔNG password) + nút **"Import N sản phẩm"** (nếu productLines>0).
- Lỗi: 409 duplicate → alert hướng dẫn tab Duplicates; 400 → hiện message.

### 3.4 Import sản phẩm sau duyệt

- Button "Import N sản phẩm từ menu" → đợi NATS sync (pattern wizard hiện tại: `MintSystemAdminTokenAsync` + `POST api/products/import` + retry 409) → hiện Imported/Skipped/Errors.
- Chỉ hiện khi `signupDetail.ProductLines.Count > 0` và tenant vừa verify trong session (hoặc load lại detail).

### 3.5 Từ chối → reject-pending

- Modal lý do (bắt buộc, textarea) → `POST reject-pending` → refresh list + alert.
- Chỉ hiện với tenant Pending self-serve (và crawl — cho phép luôn, hữu ích).

### 3.7 Admin toggles — card "Kênh đăng ký" (FR-22 UI, user chốt 2026-11-09)

- Trên `OcrOnboardingWizard.razor` thêm card collapsible **"Kênh đăng ký self-serve"** (đầu trang, SystemAdmin sẵn có): 4 `VSwitch`/toggle:
  1. `SelfServeEnabled` (master — label "Mở đăng ký tự phục vụ")
  2. `DirectoryCtaEnabled` ("Nút CTA trên timlathay.com")
  3. `QrEntryEnabled` ("QR mời đăng ký")
  4. `DeeplinkPrefillEnabled` ("Điền sẵn từ link chia sẻ")
- Load `GET /api/v1/platform/onboarding-flags` khi vào page; toggle → `PUT` ngay + toast + reload. Hiển thị trạng thái hiện tại rõ ràng (master OFF → cảnh báo đỏ "Toàn bộ kênh đăng ký đang đóng").
- File mới `Components/Shared/OnboardingFlagsCard.razor` (tách component — wizard đã lớn) + `Services/Http/OnboardingFlagsHttpService.cs` (hoặc reuse `IHttpClientFactory` pattern sẵn có trong ShopERP).

### 3.8 QR generator card (FR-21)

- Card "QR mời đăng ký" trên cùng trang: form `case` (select) + `name` + `address` + `lat`/`lng` (optional, nút "Lấy vị trí hiện tại" qua JS geolocation) → preview QR PNG → nút Tải ảnh + nút Copy link.
- URL: `https://<khachlink-public-domain>/onboard?src=qr&case=..&name=..&...` — domain lấy từ config `KhachLink:PublicUrl` (appsettings; KHÔNG hardcode).
- `QrEntryEnabled=false` → card ẩn. QR chỉ deep-link — kill thật là master switch.
- QR lib: check `qrcode.js`/`QRCode` sẵn có trong ShopERP wwwroot/UI.Platform trước; nếu chưa có → dùng `qrcodejs`/`QRCoder` (chọn lib nhỏ, MIT, pin version ≥7 ngày).

### 3.9 Directory CTAs (FR-20 — app `5_WebApps/Directory` riêng)

- Directory fetch `GET <gateway>/api/v1/platform/onboarding-flags` lúc render (SSR — `IHttpClientFactory` typed client `OnboardingFlagsClient`; cache 60s static/IMemoryCache vì SSR mỗi request gọi Gateway cũng được nhưng cache tốt hơn).
- `directoryCta && selfServeEnabled` → render CTA component `Components/Shared/OnboardCta.razor`:
  - `Home.razor`: CTA trong hero.
  - `StoreFinder.razor`: trên store card "Đây là cửa hàng của bạn? Quản lý ngay".
  - `Audit.razor`: CTA mạnh sau kết quả audit `?src=audit`.
- Link base `https://<khachlink-domain>/onboard?src=directory` (config `KhachLink:PublicUrl` trong Directory appsettings).
- Endpoint lỗi/timeout → ẩn CTA (fail-safe).
- **Lưu ý deploy**: Directory là app riêng (SSR container) — thay đổi cần deploy Directory; flags endpoint ở Gateway nên thứ tự deploy: Gateway P0 trước → Directory/ShopERP/KhachLink sau.

### 3.10 bUnit tests (`VanAn.ShopERP.Tests`)

- Pending tab render badge self-serve.
- Modal mở → gọi signup-detail mock → render ảnh/menu/flags.
- Verify-signup submit → gọi đúng API + handle 409.
- Reject → gọi API với reason.

## 4. ACCEPTANCE CRITERIA

- [ ] Pending tab hiển thị badge nguồn + cờ cảnh báo đúng
- [ ] Modal hồ sơ load đủ: ảnh, fields, menu, SĐT, flags — KHÔNG chứa password/protected blob ở bất kỳ đâu
- [ ] Duyệt thành công → tenant Active; UI hiện slug + ownerUsername; password merchant hoạt động khi owner login (verify E2E P5)
- [ ] Override password/username/slug/shopInstance đều hoạt động
- [ ] Tenant `PotentialDuplicateOf != null` → verify-signup 409 → UI hướng dẫn resolve
- [ ] Crawl tenant (không self-serve payload) → nút "Hồ sơ" ẩn hoặc hiện "không có hồ sơ self-serve" (verify-signup vẫn dùng DirectVerify cũ — KHÔNG regression)
- [ ] Reject có lý do → tenant ra khỏi Pending
- [ ] Import sản phẩm sau duyệt + retry 409 hoạt động
- [ ] Toggles: master OFF → `merchant-signups` 503 + `/onboard` trang đóng; `directoryCta` OFF → CTA ẩn trên 3 trang Directory; `qrEntry` OFF → QR card ẩn; `deeplinkPrefill` OFF → params bị bỏ qua — **mỗi toggle độc lập**
- [ ] QR generator: link đúng format `?src=qr&case=..`, quét → `/onboard` prefill đúng
- [ ] Flags endpoint public trả đúng 4 bool; PUT chỉ SystemAdmin (anonymous → 401/403)
- [ ] UI Platform components 100%; bUnit tests PASS; build + guard PASS

## 5. VERIFICATION

```powershell
dotnet build VanAn.sln
dotnet test 6_Tests/VanAn.ShopERP.Tests --filter "FullyQualifiedName~Tenant|Signup"
```

Manual: tạo pending self-serve (P0 API hoặc P1 wizard) → /admin/tenants → Pending → hồ sơ → duyệt → check slug + login owner.

## 6. RISKS / NOTES

- `PendingTenantApiDto`/`ClaimApi` nằm đâu cần grep chính xác (`ListPendingAsync` → `TenantPendingController` `/api/v1/tenants/pending` — client class tên có thể `TenantPendingApiClient`/`ClaimApi` — kiểm tra bằng grep trước khi sửa).
- DirectVerify cũ (crawl) GIỮ NGUYÊN — self-serve dùng endpoint mới; UI quyết định nút nào hiện theo `HasSignupPayload`.
- Ảnh presigned TTL 7 ngày — modal nên hiện cảnh báo "ảnh có thể hết hạn" nếu `submittedAt > 6 ngày`.
- Password override: sysadmin nhập password mới → gửi plaintext trong `verify-signup` request → chỉ dùng 1 lần, không lưu — ghi rõ trong UI ("mật khẩu mới chỉ dùng để kích hoạt, merchant nên đổi sau").
