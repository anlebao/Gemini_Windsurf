# Task Card W2: Interactive Demo — Preview Storefront

> **Status:** ⏳ NEXT (awaiting session start)
> **Week:** W2 / 5
> **Effort:** ~1 tuần
> **Master plan:** `docs/AI/tasks/gtm_drill_mvp/master_plan.md`
> **Top-level card:** `docs/AI/tasks/gtm_drill_mvp/task_card.md`
> **Prerequisite:** ✅ W1 COMPLETE + RV PASS

## Objective

Merchant tự xem storefront mock **trước khi đăng ký** — nhập tên quán + ngành → dựng storefront demo (logo, 3-5 sản phẩm mẫu, giờ mở cửa, màu theme) → nút "Đưa cửa hàng lên TimLaThay" → `/claim?name=...` prefill.

**Demo = onboarding.** Merchant thấy kết quả trước, đăng ký sau. Không cần account, không persistence — session-only state.

## Scope Checklist

### Task 2.1: Demo preview page
**Files:** `5_WebApps/KhachLink/Pages/Demo.razor` — NEW · `5_WebApps/KhachLink/Pages/DemoStoreState.cs` — NEW (in-memory model)
- [ ] Route `/demo`, anonymous, **không persistence** (session-only state)
- [ ] Input: tên quán + ngành → dựng storefront mock:
  - Logo (upload qua `ImageUploadService` có sẵn [V] — cần thêm `folder` param, see Task 2.4)
  - 3-5 sản phẩm mẫu theo ngành (name + price editable, thêm/xóa)
  - Giờ mở cửa (editable)
  - Chọn màu theme (reuse theme system từ `KhachLinkLayout`)
- [ ] **Design decision (approved 2026-09-08):** Render storefront standalone (giống `Store.razor` — không wrap trong `KhachLinkLayout`). Lý do: demo không thuộc tenant, layout wrap trigger HTTP call tải ShopConfig/instance không cần thiết. Copy theme CSS classes (`theme-classic`/`modern`/`teen`/`lady`/`premium` + `--store-*` vars) từ `Store.razor` lines 551-575 vào `Demo.razor`.
- [ ] **Design decision (approved 2026-09-08):** KHÔNG reuse `VibeShowcase.razor` cho product display. Viết display-only markup mới trong `Demo.razor` (không có cart, không AddToCart callback). Lý do: `VibeShowcase` coupling với `ShopConfig.ActiveTheme` switch + `AddToCart` Action — demo không có cart, ghép no-op tạo coupling không cần.
- [ ] Render tái dùng markup pattern từ `Store.razor` hero section (lines 61-129) — copy markup, không extract component (R3: đơn giản → copy).
- [ ] Session state: `DemoStoreState` (CascadingValue hoặc component state — KHÔNG inject DbContext, KHÔNG localStorage cho data thật)
- [ ] UI Platform components (`VanAnCard`, `VanAnButton`, `VanAnAlert`) cho form inputs + CTA.

### Task 2.4: ImageUploadService folder param
**File:** `5_WebApps/KhachLink/Services/Http/ImageUploadService.cs` — UPDATE
- [ ] **Design decision (approved 2026-09-08):** Thêm `folder` param vào `UploadGpkdAsync` (default `"gpkd-claims"`) hoặc thêm method mới `UploadLogoAsync(IBrowserFile file)` với `folder="demo-logos"`. Lý do: method hiện tại hardcode `?folder=gpkd-claims` (line 51) — demo logo cần folder khác.
- [ ] **Verify lúc implement:** Gateway endpoint `api/v1/images/upload` có thực sự `[AllowAnonymous]`? (comment line 10 nói AllowAnonymous + rate-limited 10/hour/IP). Nếu cần auth → defer logo upload v2, dùng placeholder image theo ngành (R2 mitigation).

### Task 2.2: Demo → Register chuyển tiếp
- [ ] Nút "Đưa cửa hàng lên TimLaThay" → `NavigateTo($"/claim?name={Uri.EscapeDataString(demoName)}")`
- [ ] **File:** `5_WebApps/KhachLink/Pages/Register.razor` — **NEW** (route `/claim`, merchant mới chưa có store)
  - **Design decision (approved 2026-09-08):** Tạo trang NEW `Register.razor` cho merchant mới — KHÔNG thêm route thứ 2 vào `Claim.razor`. Lý do: `Claim.razor` (`/store/{Slug}/claim`) load pending store đã tồn tại qua slug; demo→claim target merchant mới chưa có store, không có slug, không có tax code pre-fill. Hai flow khác biệt — gộp tạo branching phức tạp.
  - `System.Web.HttpUtility.ParseQueryString(Navigation.ToAbsoluteUri(Navigation.Uri).Query)` → `name` param → prefill `ShopName` field (pattern reuse từ `Login.razor` line 89)
  - Backward compatible: nếu không có `name` param, form trống
  - **Note:** W4 (`?ref=`) + W5 RV (`/claim?ref=`) đều assume route `/claim` top-level tồn tại — `Register.razor` là target cho cả W2 + W4. W4 sẽ UPDATE `Register.razor` để đọc thêm `?ref=` param.

### Task 2.5: Anti-bot security (Turnstile + honeypot)
**File:** `5_WebApps/KhachLink/Pages/Register.razor` — NEW (part of Task 2.2)
- [ ] **GAP 1 fix (approved 2026-09-08):** Cloudflare Turnstile widget trên Register form submit
  - Free, privacy-friendly (no cookie, no tracking), invisible challenge (no user friction)
  - Turnstile token sent với registration request → Gateway verifies server-side via Cloudflare API
  - Fallback: nếu Turnstile script fail to load → form still submit (Turnstile token = empty → Gateway rejects → user retry). KHÔNG hard-block UX.
  - Config: `Turnstile:SiteKey` + `Turnstile:SecretKey` trong Gateway appsettings (W5 deploy config)
- [ ] **GAP 3 fix (approved 2026-09-08):** Honeypot field — hidden input `website` (CSS `display:none`), label "Đừng điền mục này"
  - Bot auto-fill → server detect `website` non-empty → silently reject (return 200 fake success, không save)
  - Zero dependency, zero UX impact cho human user

### Task 2.6: TenantRegistration domain entity (D3 — DOMAIN MOD)
**Files:** `1_Shared/Domain/Aggregates/TenantAggregate/TenantRegistration.cs` — NEW · `3_CoreHub/Infrastructure/Configurations/TenantRegistrationConfiguration.cs` — NEW · PG migration — NEW
- [ ] **GAP 4 fix Option A (approved 2026-09-08):** NEW domain entity `TenantRegistration` — merchant mới chưa có tenant
  - **DOMAIN MOD D3 — needs governance approval.** Audit-type entity (precedent `CrawlSource`, `StoreMetricDaily`), KHÔNG đụng `AccountingEntry`/`BaseEntity`.
  - **Why needed:** `TenantClaimRequest` requires existing Pending tenant (FK to `Tenants.Id` via `BaseEntity.TenantId`). Merchant từ demo chưa có tenant → không thể reuse claim endpoint. Option A = clean separation: registration record → admin review → admin manually create tenant via existing Crawl-to-Onboard pipeline.
  - **Fields:**
    - `ShopName` (string, required — from demo prefill)
    - `Industry` (string?, optional — from demo)
    - `LogoUrl` (string?, optional — from demo upload)
    - `ContactName` (string, required)
    - `ContactPhone` (string, required)
    - `ContactEmail` (string?, optional)
    - `Source` (string — "demo" | "audit" | "direct")
    - `TurnstileVerified` (bool — server-side verification result)
    - `Status` (enum: Submitted=0 → Contacted=1 → Onboarded=2 → Rejected=3)
    - `SubmittedAt` (DateTime)
    - `ReviewedByUserId` (Guid?, set by admin)
    - `ReviewedAt` (DateTime?, set by admin)
    - `RejectionReason` (string?, set by admin)
  - **Single-Identity Pattern:** `TenantRegistration` inherits `BaseEntity` (no business key VO). `Id` = PK. Constructor sets `Id` if business key needed (likely none — pure audit entity like CrawlSource).
  - **TenantId:** `BaseEntity.TenantId` — nhưng registration chưa thuộc tenant nào. Set `TenantId = Guid.Empty` (or nullable — verify EF config allows). Alternative: `IMustHaveTenant` skip, use `BaseEntity` without tenant constraint (precedent: CrawlSource inherits `BaseEntity` directly, not `IMustHaveTenant`).
  - **Lifecycle:** `Create()` factory (like CrawlSource) → `MarkContacted()` → `MarkOnboarded(tenantId)` → `Reject(reason)`. Domain events optional (defer — admin queue notification via polling, not event-driven MVP).

### Task 2.7: Registration API endpoint + service
**Files:** `2_Gateway/Controllers/TenantRegistrationController.cs` — NEW · `3_CoreHub/Services/Registrations/TenantRegistrationService.cs` — NEW · `3_CoreHub/Services/Registrations/RegistrationDtos.cs` — NEW · `5_WebApps/KhachLink/Services/Http/RegistrationHttpService.cs` — NEW
- [ ] **Endpoint:** `POST /api/v1/tenant-registrations` — `[AllowAnonymous]` + rate-limited (new policy `registration-submit`: 5/IP/24h — generous hơn claim 3/24h vì registration nhẹ hơn, no GPKD)
- [ ] **Turnstile server-side verification:** Gateway gọi Cloudflare `https://challenges.cloudflare.com/turnstile/v0/siteverify` với `secret` + `token` + `remoteip` → verify `success: true` trước khi save. Nếu Turnstile not configured (dev) → skip verification (log warning).
- [ ] **Honeypot check:** Nếu request body có `website` field non-empty → return 200 fake success (không save, không log error — silent reject để bot không retry).
- [ ] **Server-side validation:** `ShopName`, `ContactName`, `ContactPhone` required (ThrowIfNullOrWhiteSpace — precedent `TenantClaimService.SubmitClaimAsync` lines 24-27).
- [ ] **Admin queue:** `GET /api/v1/tenant-registrations` + `GET /{id}` + `POST /{id}/contact` + `POST /{id}/reject` — `[Authorize(Policy="SystemAdmin")]` (precedent `TenantClaimController` admin endpoints).
- [ ] **KhachLink client:** `RegistrationHttpService` → `POST /api/v1/tenant-registrations` với `RegistrationRequestDto` (ShopName, Industry, LogoUrl, ContactName, ContactPhone, ContactEmail, Source, TurnstileToken, HoneypotWebsite).

### Task 2.3: E2E spec
**File:** `6_Testing/e2e-tests/gtm-demo.spec.ts` — NEW
- [ ] Vào /demo → nhập tên → thấy storefront mock
- [ ] Sửa 1 sản phẩm (name + price) → thấy update render
- [ ] Upload logo (hoặc skip nếu không có test image) → thấy logo render
- [ ] Bấm "Đưa cửa hàng lên TimLaThay" → landing `/claim` với tên đã prefill
- [ ] Verify `name` query param matches input

## Prerequisites

- ✅ W1 COMPLETE + RV PASS (2026-09-07)
- ✅ KhachLink `ImageUploadService` (live — cần thêm `folder` param, Task 2.4)
- ✅ KhachLink `Store.razor` storefront markup (reuse pattern — hero lines 61-129, theme CSS lines 551-575)
- ✅ KhachLink `Claim.razor` + `ClaimHttpService` (live — **KHÔNG đụng**, `Register.razor` NEW thay thế)
- ✅ KhachLink theme system (`KhachLinkLayout.razor` — copy CSS classes, không wrap layout)
- ✅ UI Platform components (`VanAnCard`, `VanAnButton`, `VanAnAlert`)
- ✅ `ProductDto` model (live — reuse for demo products in-memory)
- ✅ `CrawlSource` entity (precedent — audit-type entity pattern for D3 `TenantRegistration`)
- ✅ `TenantClaimController` + `TenantClaimService` (precedent — admin queue pattern for registration admin endpoints)
- ✅ Gateway rate limiter infrastructure (`AddRateLimiter` — add new `registration-submit` policy)
- ⏳ Cloudflare Turnstile account (free — get SiteKey + SecretKey before W5 deploy)
- ⏳ `GrowthMachine:Enabled` flag — W2 page có thể build không cần flag (demo page không nhạy cảm — defer flag gating đến W5 nếu user muốn)
- ⏳ **DOMAIN MOD D3 approval** — `TenantRegistration` entity (audit-type, precedent CrawlSource). Needs explicit governance approval before IMPLEMENT.

## Verification

1. **Build:** `dotnet build VanAn.sln` → 0 errors
2. **Migration:** PG migration apply success — `TenantRegistrations` table tồn tại
3. **Local smoke test:** `dotnet run --project 5_WebApps/KhachLink` → http://localhost:5002/demo render Demo page
4. **Demo flow:** nhập tên "Quán Test" + ngành "Cà phê" → thấy storefront mock với 3-5 sản phẩm cà phê mẫu
5. **Edit product:** sửa tên + giá → render update
6. **Logo upload:** upload image → logo render trong storefront mock
7. **Theme select:** chọn màu → storefront mock đổi màu
8. **CTA:** bấm "Đưa cửa hàng lên TimLaThay" → navigate `/claim?name=Quán%20Test` → Register form prefilled
9. **Turnstile:** Register form render Turnstile widget → submit → Gateway verify token server-side → 200
10. **Honeypot:** submit form với `website` field filled → server return 200 fake success (không save record)
11. **Registration API:** `POST /api/v1/tenant-registrations` → 200 + record saved; 11th request → 429
12. **Admin queue:** `GET /api/v1/tenant-registrations` (SysAdmin auth) → list registrations
13. **E2E:** `gtm-demo.spec.ts` PASS (chạy sau build, theo Playwright rules)

## Governance checklist

- [ ] **DOMAIN MOD D3 (approved 2026-09-08):** `TenantRegistration` entity — audit-type (precedent `CrawlSource`), KHÔNG đụng `AccountingEntry`/`BaseEntity`. Single-Identity pattern. Constructor syncs `Id`. EF config `Ignore` business key (nếu có).
- [ ] Domain purity: D3 = audit-type entity only, no business logic in controllers
- [ ] KhachLink HTTP-only: Demo page không inject DbContext, không gọi Gateway (session-only state). Register page gọi Gateway qua `RegistrationHttpService`.
- [ ] UI Platform: Demo.razor + Register.razor dùng VanAn.UI.Platform components — no custom CSS
- [ ] Không tạo .csproj mới
- [ ] No persistence: `DemoStoreState` = session-only, KHÔNG lưu DB/localStorage cho data thật
- [ ] ImageUploadService: reuse existing (đã có [V] + thêm `folder` param)
- [ ] **Anti-bot:** Turnstile server-side verify + honeypot silent reject
- [ ] **Rate limit:** `registration-submit` policy 5/IP/24h (new policy in Gateway Program.cs)

## Risks

| # | Risk | Mitigation |
|---|---|---|
| R1 | Demo state mất khi refresh | Session-only by design — refresh = reset demo. Hiển thị hint "Refresh sẽ đặt lại demo". KHÔNG persistence MVP. |
| R2 | ImageUploadService cần auth | Verify: ImageUploadService có anonymous path? Nếu cần auth → demo logo upload defer v2, dùng placeholder image theo ngành. |
| R3 | Store.razor markup quá phức tạp để reuse | Extract storefront card pattern thành component riêng (nếu cần) hoặc copy markup pattern (không extract nếu đơn giản) |
| R4 | Sản phẩm mẫu theo ngành không có data | Hardcode 3-5 sản phẩm mẫu cho 5-10 ngành phổ biến (cà phê, phở, tạp hóa, salon, tiệm nail...). Ngành khác = generic 3 sản phẩm. |
| R5 | Domain mod D3 phá backward compatibility | Audit-type entity (precedent CrawlSource), additive only. KHÔNG đụng AccountingEntry/BaseEntity. Migration tạo table mới. |
| R6 | Turnstile script fail to load (network/CDN) | Fallback: form vẫn submit, token empty → Gateway rejects → user retry. KHÔNG hard-block UX. |
| R7 | Honeypot false positive (screen reader user) | Label "Đừng điền mục này" + `aria-hidden="true"` + `tabindex="-1"`. Screen reader skip field. |
| R8 | Turnstile token replay attack | Server-side verify với `remoteip` — Cloudflare check IP match. Token single-use (Cloudflare default). |
| R9 | Registration spam queue quá tải | Rate limit 5/IP/24h + Turnstile + honeypot + manual admin review. Bot traffic bị chặn ở 3 lớp. |

## Files

| # | File | Action | Status |
|---|---|---|---|
| 1 | `5_WebApps/KhachLink/Pages/Demo.razor` | NEW | ⏳ |
| 2 | `5_WebApps/KhachLink/Pages/DemoStoreState.cs` | NEW (in-memory model) | ⏳ |
| 3 | `5_WebApps/KhachLink/Pages/Register.razor` | NEW (route `/claim`, merchant mới — Turnstile + honeypot) | ⏳ |
| 4 | `5_WebApps/KhachLink/Services/Http/ImageUploadService.cs` | UPDATE (thêm `folder` param hoặc `UploadLogoAsync` method) | ⏳ |
| 5 | `6_Testing/e2e-tests/gtm-demo.spec.ts` | NEW | ⏳ |
| 6 | `1_Shared/Domain/Aggregates/TenantAggregate/TenantRegistration.cs` | NEW (D3 — domain entity, audit-type) | ⏳ |
| 7 | `3_CoreHub/Infrastructure/Configurations/TenantRegistrationConfiguration.cs` | NEW (EF config) | ⏳ |
| 8 | `3_CoreHub/Infrastructure/VanAnDbContext.cs` + `IVanAnDbContext.cs` | UPDATE (DbSet<TenantRegistration>) | ⏳ |
| 9 | PG migration | NEW (TenantRegistrations table) | ⏳ |
| 10 | `2_Gateway/Controllers/TenantRegistrationController.cs` | NEW (POST submit + admin queue endpoints) | ⏳ |
| 11 | `3_CoreHub/Services/Registrations/TenantRegistrationService.cs` | NEW | ⏳ |
| 12 | `3_CoreHub/Services/Registrations/RegistrationDtos.cs` | NEW | ⏳ |
| 13 | `5_WebApps/KhachLink/Services/Http/RegistrationHttpService.cs` | NEW | ⏳ |
| 14 | `2_Gateway/Program.cs` | UPDATE (add `registration-submit` rate limit policy + Turnstile verify service) | ⏳ |
| 15 | `2_Gateway/appsettings.json` | UPDATE (Turnstile:SiteKey + Turnstile:SecretKey — W5 deploy config) | ⏳ |

## Open questions (resolve before/during W2 session)

1. ~~**ImageUploadService auth:** Demo page (anonymous) có upload được logo không?~~ → **Resolved (approved 2026-09-08):** Verify runtime lúc implement. Nếu `[AllowAnonymous]` confirmed → thêm `folder` param (Task 2.4). Nếu cần auth → defer logo upload v2, dùng placeholder theo ngành.
2. ~~**Sản phẩm mẫu data source:**~~ → **Resolved:** Hardcode 3-5 sản phẩm cho 5-10 ngành phổ biến (zero Gateway dependency, zero auth). Ngành khác = 3 generic products.
3. ~~**Theme select scope:**~~ → **Resolved:** Color picker MVP (đơn giản hơn, đủ "thấy kết quả"). 5 theme classes copy từ `Store.razor` lines 551-575.
4. ~~**Route `/claim` design:**~~ → **Resolved (approved 2026-09-08):** Tạo `Register.razor` NEW (route `/claim`) cho merchant mới. KHÔNG thêm route thứ 2 vào `Claim.razor`. `Register.razor` là target cho W2 (`?name=`) + W4 (`?ref=`).
5. ~~**`VibeShowcase` reuse:**~~ → **Resolved:** KHÔNG reuse. Viết display-only markup mới trong `Demo.razor`.
6. ~~**`KhachLinkLayout` wrap:**~~ → **Resolved:** Render standalone (giống `Store.razor`), không wrap layout.
7. ~~**Anti-bot (GAP 1):**~~ → **Resolved (approved 2026-09-08):** Cloudflare Turnstile trên Register form submit. Server-side verify via Cloudflare API. Config trong Gateway appsettings (W5 deploy).
8. ~~**Honeypot (GAP 3):**~~ → **Resolved (approved 2026-09-08):** Hidden `website` field — bot fill → silent reject (200 fake success). `aria-hidden` + `tabindex=-1` cho accessibility.
9. ~~**Registration endpoint (GAP 4):**~~ → **Resolved (approved 2026-09-08):** Option A — NEW endpoint `POST /api/v1/tenant-registrations` + NEW domain entity `TenantRegistration` (D3). Audit-type (precedent CrawlSource). Admin queue for review.
10. **D3 TenantId handling:** `TenantRegistration` chưa thuộc tenant — `BaseEntity.TenantId` = `Guid.Empty`? Or skip `IMustHaveTenant` (precedent CrawlSource inherits `BaseEntity` directly)? → Verify lúc implement. CrawlSource inherits `BaseEntity` (not `IMustHaveTenant`) — follow same pattern.
11. **Turnstile dev fallback:** Khi `Turnstile:SecretKey` not configured (dev/test) → skip verification (log warning) hay hard-fail? → Skip + log warning (dev convenience). Production must have keys.

## Related

- Master plan: `docs/AI/tasks/gtm_drill_mvp/master_plan.md`
- Top-level card: `docs/AI/tasks/gtm_drill_mvp/task_card.md`
- W1 (done): `docs/AI/tasks/gtm_drill_mvp/task_card_w1_merchant_audit.md`
- W3 (next): `docs/AI/tasks/gtm_drill_mvp/task_card_w3_revenue_proof.md`
- W4 (later): `docs/AI/tasks/gtm_drill_mvp/task_card_w4_merchant_referral.md` — sẽ UPDATE `Register.razor` cho `?ref=` param
- KhachLink Store page (reuse pattern): `5_WebApps/KhachLink/Pages/Store.razor` (hero lines 61-129, theme CSS lines 551-575)
- KhachLink Claim page (KHÔNG đụng — flow khác biệt): `5_WebApps/KhachLink/Pages/Claim.razor`
- KhachLink ImageUploadService: `5_WebApps/KhachLink/Services/Http/ImageUploadService.cs` (cần thêm `folder` param)
