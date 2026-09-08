# HƯỚNG DẪN SỬ DỤNG — W1 Merchant Audit + W2 Interactive Demo (Máy Khoan Thủng GTM)

> **Phiên bản:** W1 + W2 (tuần 1-2/5 của GTM Drill Machine MVP)
> **Ngày hoàn thành:** W1 — 2026-09-07 · W2 — 2026-09-08 (code complete + production RV PASS)
> **Commit:** W1 `e8cd4e62` + `a21fcffc` · W2 `f66a08a1` + `7ce6c73d`
> **Branch:** `main` @ `7ce6c73d`
> **Task card:** `docs/AI/tasks/gtm_drill_mvp/task_card_w2_interactive_demo.md`
> **Strategy:** `docs/AI/plans/ecosystem-master-business-model.md` Section 4

---

## 1. W1 LÀ GÌ — GIẢI QUYẾT VẤN ĐỀ GÌ

### Vấn đề

Merchant HKD (hộ kinh doanh) không tự phát hiện mình cần phần mềm quản lý. Door-to-door sales tốn kém và tỷ lệ chốt thấp vì merchant chưa thấy "kết quả" mà chỉ thấy "công cụ". Đây là cold-start của phễu khách hàng.

### Giải pháp W1

Trang **"Kiểm tra cửa hàng"** (`/kiem-tra-cua-hang` trên timlathay.com) cho phép chủ cửa hàng nhập tên hoặc mã số thuế (MST) → nhận báo cáo miễn phí về mức độ hiện diện của cửa hàng trên mạng lưới TimLaThay/Vạn An.

**Nguyên lý:** bán kết quả, không bán phần mềm. Câu hỏi mở đầu không phải "Bạn cần phần mềm quản lý không?" mà là *"Khách gần cửa hàng bạn đang tìm gì?"* — khơi gợi nhu cầu tự phát hiện.

### Phễu W1 tạo ra

```
Merchant tự tìm thấy trang (SEO / chia sẻ / CTV)
  → Nhập tên/MST
  → Nhận báo cáo hiện diện (có/không trong danh bạ, có storefront, có social, peer cùng ngành)
  → Nếu chưa có: CTA "Đưa cửa hàng lên TimLaThay — Đăng ký miễn phí"
  → Nếu đã có (Pending): CTA claim (chứng minh quyền sở hữu)
  → Nếu đã có (Active): thấy mình đang ở đâu trong mạng lưới → bước sang W3 Revenue Proof
```

---

## 2. CÁCH TRUY CẬP

### Trang audit (dành cho merchant)

**URL:** `https://timlathay.com/kiem-tra-cua-hang`

- SSR (Blazor Server) — load < 1 giây, SEO-friendly
- Anonymous — không cần đăng nhập
- Mobile-friendly (UI Platform components)

### API audit (dành cho developer / tích hợp)

**Endpoint:** `GET https://api2.khachvip.online/api/v1/growth/audit`

**Query params:**
- `name` (string, tùy chọn) — tên cửa hàng (ILIKE match)
- `mst` (string, tùy chọn) — mã số thuế (exact match)

**Bắt buộc:** ít nhất 1 trong 2 param phải có giá trị. Nếu cả 2 rỗng → HTTP 400.

**Rate limit:** 10 requests/IP/hour (FixedWindow). Vượt giới hạn → HTTP 429 + JSON `{"message":"Quá giới hạn yêu cầu. Vui lòng thử lại sau 1 giờ."}`.

**Auth:** Anonymous (không cần token).

**Ví dụ:**

```bash
# Tìm theo tên
curl "https://api2.khachvip.online/api/v1/growth/audit?name=Central%20Mall"

# Tìm theo MST
curl "https://api2.khachvip.online/api/v1/growth/audit?mst=0301234567"

# Tìm kết hợp (name OR mst)
curl "https://api2.khachvip.online/api/v1/growth/audit?name=Van%20An&mst=0301234567"
```

---

## 3. RESPONSE FORMAT

### Match — Active tenant (đã claim, đã Active)

```json
{
  "found": true,
  "tenant": {
    "id": "b391c72b-...",
    "name": "Central Mall",
    "isPending": false,
    "slug": null,
    "industry": null,
    "hasStorefront": false,
    "hasKhachLinkDomain": false,
    "khachLinkDomain": null,
    "hasSocialLinks": false,
    "socialLinksFb": null,
    "socialLinksTiktok": null
  },
  "industryPeerCount": null,
  "claimUrl": null,
  "registerUrl": null
}
```

### Match — Pending tenant (crawl chưa claim)

```json
{
  "found": true,
  "tenant": {
    "id": "b69b0608-...",
    "name": "CÔNG TY TNHH DONER LAB",
    "isPending": true,
    "slug": "pending-4202064797-73c9",
    "industry": null,
    "hasStorefront": true,
    "hasKhachLinkDomain": false,
    "khachLinkDomain": null,
    "hasSocialLinks": false,
    "socialLinksFb": null,
    "socialLinksTiktok": null
  },
  "industryPeerCount": null,
  "claimUrl": "/store/pending-4202064797-73c9/claim",
  "registerUrl": null
}
```

### No match

```json
{
  "found": false,
  "tenant": null,
  "industryPeerCount": null,
  "claimUrl": null,
  "registerUrl": null
}
```

### Empty params — HTTP 400

```json
{
  "message": "Vui lòng nhập tên cửa hàng hoặc mã số thuế."
}
```

### Rate limited — HTTP 429

```json
{
  "message": "Quá giới hạn yêu cầu. Vui lòng thử lại sau 1 giờ."
}
```

---

## 4. CÁC TRƯỜNG HỢP SỬ DỤNG

### 4.1 Merchant tự kiểm tra (self-discovery)

1. Merchant vào `https://timlathay.com/kiem-tra-cua-hang`
2. Nhập tên cửa hàng (hoặc MST)
3. Xem báo cáo:
   - **Chưa có trong danh bạ** → CTA "Đưa cửa hàng lên TimLaThay — Đăng ký miễn phí" → chuyển sang `/claim?name=...`
   - **Có nhưng Pending** (crawl tạo, chưa claim) → CTA claim → `/store/{slug}/claim` (upload giấy phép kinh doanh)
   - **Đã Active** → thấy mình đang ở đâu (storefront/social/domain) → bước sang W3 dashboard "Tháng này"

### 4.2 CTV/Salesman gửi link cho merchant

1. CTV copy link `https://timlathay.com/kiem-tra-cua-hang`
2. Gửi cho merchant qua Zalo/Messenger
3. Merchant tự nhập → tự thấy report → tự click CTA
4. (W4 sẽ thêm `?ref={customerId}` để CTV được ghi công hoa hồng)

### 4.3 Remote closer (sales) dùng trước khi gọi

1. Closer vào trang audit, nhập tên cửa hàng chuẩn bị gọi
2. Xem merchant đã có storefront/social chưa
3. Mở cuộc gọi bằng: *"Em thấy cửa hàng anh đang ở đây trên TimLaThay, có X sản phẩm. Em vào cùng màn hình 10 phút cấu hình luôn nhé?"* (W5 sẽ thêm nút [Cho phép Vạn An hỗ trợ từ xa])
4. Dùng impersonation #103 để vào cùng tài khoản merchant

### 4.4 Tích hợp vào tài liệu marketing

- QR code in trên card/flyer → dẫn về `/kiem-tra-cua-hang`
- Link trong signature Zalo OA
- Link trong bài đăng Facebook "Kiểm tra miễn phí cửa hàng bạn có trên mạng lưới Vạn An"

---

## 5. QUY TẮC SỐ LIỆU (GROUND RULE 5)

### Được hiển thị (số đo được)

- `found`: có/không trong mạng lưới
- `isPending`: trạng thái (crawl chưa claim / đã Active)
- `hasStorefront`: có storefront trên KhachLink chưa
- `hasKhachLinkDomain`: có domain riêng chưa
- `hasSocialLinks`: có link FB/Tiktok chưa
- `industryPeerCount`: số cửa hàng cùng ngành trong dữ liệu crawl (đếm từ `Settings.BusinessField`)

### KHÔNG được hiển thị (M3 privacy / legal)

- **CrawledPhone**: số điện thoại crawl = internal-only (ND13/2023 + M3 rule)
- **Email**: email crawl = internal-only
- **Address/location** của Pending tenant: chỉ hiển thị name + slug
- **Fake precision**: cấm "420 lượt tìm quanh đây", "+10-20 đơn/ngày" nếu không có dữ liệu đo được

### Pending tenant — chỉ trả

- `name` (tên doanh nghiệp từ crawl — public business data)
- `slug` (slug internal)
- `isPending: true`
- `hasStorefront` / `hasSocialLinks` (có/không, không lộ chi tiết)
- `claimUrl` (link claim)

---

## 6. KIẾN TRÚC & DATA FLOW

```
Merchant browser
  → https://timlathay.com/kiem-tra-cua-hang (Directory SSR, port 8080)
  → Directory GrowthAuditService (server-side HTTP call)
  → https://api2.khachvip.online/api/v1/growth/audit (Gateway, port 80)
  → Gateway GrowthController
  → PostgreSQL (Tenants + Settings + CrawlSources)
  → Response JSON
  → Directory SSR renders report
  → Merchant sees report + CTA
```

**XForwarded-For handling:** Directory gọi Gateway server-side, nhưng rate limit vẫn partition theo IP của **end user** (không phải IP container Directory). Directory `GrowthAuditService` forward IP user qua header `X-Forwarded-For`, Gateway `UseForwardedHeaders` rewrite `RemoteIpAddress` → rate limit partition đúng.

**nginx routing:** `timlathay.com` page/asset/blazor traffic → Directory SSR container (port 8080). API traffic (`/api/`) → Gateway (port 80). KhachLink WASM (port 80) chỉ phục vụ `*.khachvip.online` commerce domains.

---

## 7. FILES & VỊ TRÍ

| File | Vai trò |
|---|---|
| `2_Gateway/Controllers/GrowthController.cs` | API endpoint `GET /api/v1/growth/audit` |
| `2_Gateway/Program.cs` | Rate limit policy `growth-audit` (10/IP/h, 429 OnRejected) |
| `5_WebApps/Directory/Components/Pages/Audit.razor` | Trang `/kiem-tra-cua-hang` (Blazor Server SSR) |
| `5_WebApps/Directory/Services/GrowthAuditService.cs` | Service gọi Gateway + forward XFF |
| `5_WebApps/Directory/Program.cs` | DI registration |
| `nginx/templates/vanan.multivps.conf.template` | Routing timlathay.com → Directory SSR (port 8080) |
| `6_Testing/e2e-tests/gtm-audit.spec.ts` | E2E spec (4 tests, env-driven) |
| `6_Tests/VanAn.Architecture.Tests/AuthorizationEnforcementTests.cs` | Arch whitelist GrowthController |

---

## 8. E2E TEST (chạy khi ecosystem lên)

**File:** `6_Testing/e2e-tests/gtm-audit.spec.ts`

**Env vars cần set:**
- `DIRECTORY_URL` — URL Directory (default `https://timlathay.com`)
- `AUDIT_TENANT_NAME` — tên active tenant test (default "Central Mall")
- `AUDIT_PENDING_TENANT_NAME` — tên pending tenant test (default "DONER LAB")

**Chạy:**
```bash
cd 6_Testing/e2e-tests
DIRECTORY_URL=https://timlathay.com \
AUDIT_TENANT_NAME="Central Mall" \
AUDIT_PENDING_TENANT_NAME="DONER LAB" \
npx playwright test gtm-audit.spec.ts
```

**4 tests:**
1. Audit page renders với form + copy "bán kết quả"
2. Submit active tenant → thấy report + CTA
3. Submit pending tenant → thấy claim CTA, không lộ phone/email
4. Spam >10 requests → 429

---

## 9. GIỚI HẠN W1 (DEFER — không phải bug)

| Giới hạn | Lý do | Khi nào build |
|---|---|---|
| Không có "lượt tìm kiếm quanh đây" | 0 event tracking/telemetry [V] | Gate G1 — sau 30 ngày dữ liệu thật |
| Không có AI scoring/SDR | 0 behavioral data | Gate G1 — event pipeline trước |
| Không lookup theo SĐT/FB link | SĐT crawl = internal-only (M3) | v2 — sau khi có consent flow |
| Không có `industryPeerCount` nếu tenant không có `Settings.BusinessField` | Dữ liệu crawl chưa đầy đủ | Crawler phase 2 (industry enrichment) |
| Không có dashboard "Tháng này" | Cần W3 StoreMetricDaily | W3 Revenue Proof |
| Không có referral attribution | Cần W4 TenantClaimRequest +2 fields | W4 Merchant Referral |
| Không có remote support consent | Cần W5 Support.razor | W5 Remote closing |
| `GrowthMachine:Enabled` flag chưa có | W5 task | W5 — hiện audit luôn ON (anonymous, rate-limited, không harm) |

---

## 10. RV CHECKLIST (chạy lại khi cần)

| # | Test | Command | Expected |
|---|---|---|---|
| 1 | Directory audit page | `curl -sS -o /dev/null -w "%{http_code}" https://timlathay.com/kiem-tra-cua-hang` | 200 |
| 2 | Directory SSR (not WASM) | `curl -sS https://timlathay.com/kiem-tra-cua-hang \| grep "VanAn.Directory.styles.css"` | match |
| 3 | Gateway audit empty | `curl -sS -w "%{http_code}" "https://api2.khachvip.online/api/v1/growth/audit?name=&mst="` | 400 |
| 4 | Gateway audit no-match | `curl -sS "https://api2.khachvip.online/api/v1/growth/audit?name=ZZZNoMatch&mst=9999999999"` | `{"found":false}` |
| 5 | Gateway audit active | `curl -sS "https://api2.khachvip.online/api/v1/growth/audit?name=Central%20Mall"` | `{"found":true,...}` |
| 6 | Gateway audit pending | `curl -sS "https://api2.khachvip.online/api/v1/growth/audit?name=DONER%20LAB"` | `{"found":true,"isPending":true,...,"claimUrl":"..."}` |
| 7 | Rate limit | Spam 12 requests | 200 × 10, then 429 × 2 |
| 8 | nginx routing | `curl -sS https://timlathay.com/ \| grep "VanAn.Directory.styles.css"` | match (Directory SSR) |

---

## 11. W2 — INTERACTIVE DEMO + REGISTRATION

W1 hoàn thành phễu **tự phát hiện → tự xem report**. W2 thêm **tự trải nghiệm → tự đăng ký**:

```
Merchant vào /demo (KhachLink)
  → Nhập tên quán + ngành
  → Dựng storefront mock (logo, 3-5 sản phẩm, giờ mở cửa, theme)
  → Sửa sản phẩm/giờ/theme trực tiếp
  → CTA "Đưa cửa hàng lên TimLaThay"
  → /claim?name=<demo name> (prefill)
  → Điền form đăng ký (tên, SĐT, email, ngành)
  → Turnstile + honeypot (anti-bot)
  → POST /api/v1/tenant-registrations
  → Admin queue (SystemAdmin review)
```

### 11.1 Trang Demo

**URL:** `https://diemthuong2.khachvip.online/demo` (hoặc bất kỳ KhachLink commerce domain)

- Blazor WebAssembly — render client-side, không load server
- Standalone — không dùng KhachLinkLayout (render full-page, không sidebar/header)
- Session-only — refresh = reset demo, không lưu DB/localStorage
- Anonymous — không cần đăng nhập

**Industry seeds (sample products):**
- `cà phê` / `cafe` → 5 sản phẩm (cà phê sữa đá, bạc xỉu, cà phê đen, trà đào cam sả, trà sữa trân châu)
- `phở` / `pho` → 4 sản phẩm (phở bò tái, phở bò chín, phở gà, phở xào)
- `tạp hóa` → 4 sản phẩm (mì gói, nước suối, gạo, đường)
- `salon` / `tiệm nail` / `nail` → 4 sản phẩm (làm móng, sơn gel, đắp móng, vẽ móng)
- `ăn vặt` → 4 sản phẩm (khoai tây chiên, gà rán, trà sữa, xúc xích nướng)
- Khác (generic) → 3 sản phẩm (Sản phẩm 1/2/3)

**Theme:** Classic (nâu ấm) · Modern (xanh dương) · Teen (hồng-tím) · Lady (hồng pastel) · Premium (đen-vàng) — copy CSS từ `Store.razor`.

### 11.2 Trang Register (/claim)

**URL:** `https://diemthuong2.khachvip.online/claim?name=<shop name>`

- Route `/claim` — NEW `Register.razor` (không phải `Claim.razor` — Claim.razor vẫn cho `/store/{Slug}/claim` của Pending tenant có sẵn)
- `?name=` prefill từ demo
- `?ref=` (W4 — Merchant Referral attribution)
- Form fields: ShopName (bắt buộc) · Industry · ContactName (bắt buộc) · ContactPhone (bắt buộc) · ContactEmail
- Honeypot `website` field — ẩn (`display:none`, `aria-hidden`, `tabindex=-1`), bot auto-fill → server silent reject
- Turnstile widget — invisible challenge, không friction cho user thật
- Submit qua `RegistrationHttpService` → Gateway `POST /api/v1/tenant-registrations`

### 11.3 API Registration

**Endpoint:** `POST https://api2.khachvip.online/api/v1/tenant-registrations`

**Auth:** Anonymous

**Rate limit:** 5 requests/IP/24h (FixedWindow). Vượt → HTTP 429.

**Request body:**
```json
{
  "shopName": "Quán Cà Phê Nhất Nghệ",
  "contactName": "Nguyễn Văn A",
  "contactPhone": "0901234567",
  "source": "demo",
  "turnstileToken": "<token từ widget>",
  "industry": "cà phê",
  "contactEmail": "email@example.com",
  "honeypotWebsite": null
}
```

**Response — Success (200):**
```json
{
  "registrationId": "ebde9cb8-35ec-46c6-ab62-69254da7767d",
  "message": "Cảm ơn! Yêu cầu đăng ký đã gửi. Chúng tôi sẽ liên hệ trong 1-2 ngày làm việc."
}
```

**Response — Honeypot triggered (200, fake success, no DB record):**
```json
{
  "registrationId": "00000000-0000-0000-0000-000000000000",
  "message": "Cảm ơn! Yêu cầu đã gửi."
}
```

**Response — Rate limited (429):**
```json
{
  "message": "Quá giới hạn đăng ký. Vui lòng thử lại sau 24 giờ."
}
```

### 11.4 Anti-abuse (3 lớp)

| Lớp | Cơ chế | Hành vi |
|---|---|---|
| 1 | Cloudflare Turnstile (server-side verify) | Token rỗng/sai → 400. Dev fallback: nếu `Turnstile:SecretKey` không cấu hình → skip verify + log warning. Production (W5) bắt buộc cấu hình. |
| 2 | Honeypot `website` field | Bot auto-fill → 200 fake success, KHÔNG lưu DB. User thật không thấy field (display:none). |
| 3 | Rate limit `registration-submit` (5/IP/24h) | Vượt → 429. |

### 11.5 Admin Queue (SystemAdmin)

**Endpoint:** `GET /api/v1/tenant-registrations` (list pending) · `GET /api/v1/tenant-registrations/{id}` (detail) · `POST /api/v1/tenant-registrations/{id}/contact` · `POST /api/v1/tenant-registrations/{id}/onboard` · `POST /api/v1/tenant-registrations/{id}/reject`

**Auth:** SystemAdmin (JWT bearer).

**Lifecycle:** `Submitted` (default) → `Contacted` (admin đã liên hệ) → `Onboarded` (đã tạo tenant) hoặc `Rejected`.

### 11.6 D3 Domain Model — TenantRegistration

**Entity:** `1_Shared/Domain/Aggregates/TenantAggregate/TenantRegistration.cs`

- Audit-type (precedent `CrawlSource`) — không phải `TenantClaimRequest` (vì `TenantClaimRequest` yêu cầu tenant có sẵn)
- `TenantId = Guid.Empty` sentinel — pre-tenant lead, chưa thuộc tenant nào
- Excluded từ multi-tenancy query filter (anonymous registration xảy ra trước khi tenant được tạo)
- EF config: `3_CoreHub/Infrastructure/Configurations/TenantRegistrationConfiguration.cs`
- Migration: `3_CoreHub/Infrastructure/Migrations/20260908023803_AddTenantRegistrations.cs` (PG, indexes on Status + SubmittedAt)
- ShopERP `ShopERPDbContext` `Ignore<TenantRegistration>()` (PG-only, không mirror sang SQLite)

### 11.7 Files & vị trí (W2)

| File | Vai trò |
|---|---|
| `1_Shared/Domain/Aggregates/TenantAggregate/TenantRegistration.cs` | D3 entity (audit-type, lifecycle) |
| `3_CoreHub/Infrastructure/Configurations/TenantRegistrationConfiguration.cs` | EF config (no FK to Tenants) |
| `3_CoreHub/Infrastructure/Migrations/20260908023803_AddTenantRegistrations.cs` | PG migration |
| `3_CoreHub/Services/Registrations/RegistrationDtos.cs` | Request + result + admin DTOs |
| `3_CoreHub/Services/Registrations/ITenantRegistrationService.cs` + `TenantRegistrationService.cs` | Service (validate + duplicate + persist + lifecycle) |
| `3_CoreHub/Services/Registrations/TurnstileVerificationService.cs` | Cloudflare server-side verify (dev fallback) |
| `2_Gateway/Controllers/TenantRegistrationController.cs` | POST submit + admin queue |
| `2_Gateway/Program.cs` | Rate limit `registration-submit` (5/IP/24h) + DI |
| `2_Gateway/appsettings.json` | `Turnstile:SiteKey` + `Turnstile:SecretKey` placeholders |
| `5_WebApps/KhachLink/Pages/Demo.razor` | Trang `/demo` (standalone storefront mock) |
| `5_WebApps/KhachLink/Pages/DemoStoreState.cs` | In-memory model (5 industry seeds) |
| `5_WebApps/KhachLink/Pages/Register.razor` | Trang `/claim` (Turnstile + honeypot + prefill) |
| `5_WebApps/KhachLink/Services/Http/RegistrationHttpService.cs` | Gateway client |
| `5_WebApps/KhachLink/Services/Http/ImageUploadService.cs` | `UploadLogoAsync` (folder=demo-logos) |
| `6_Testing/e2e-tests/gtm-demo.spec.ts` | E2E spec (6 tests) |

### 11.8 E2E Test (W2)

**File:** `6_Testing/e2e-tests/gtm-demo.spec.ts`

**Env vars:**
- `KHACHLINK_URL` — URL KhachLink (default `http://localhost:5002`, production `https://diemthuong2.khachvip.online`)

**Chạy:**
```bash
cd 6_Testing/e2e-tests
KHACHLINK_URL=https://diemthuong2.khachvip.online npx playwright test gtm-demo.spec.ts
```

**6 tests:**
1. Demo setup form renders (shop name + industry inputs)
2. Entering shop name + industry → storefront mock with sample products
3. Editing product name + price updates render
4. Adding/deleting products updates count
5. Changing theme updates wrapper class
6. CTA navigates to `/claim?name=...` with prefilled shop name

**Production RV (2026-09-08):** 6/6 PASS on `diemthuong2.khachvip.online` (50.7s).

### 11.9 RV Checklist W2

| # | Test | Command | Expected |
|---|---|---|---|
| 1 | PG migration applied | `docker exec vanan-postgres-1 psql -U vanan_admin -d VanAnCoreHub -c 'SELECT count(*) FROM "TenantRegistrations"'` | 0 (or >0 if tested) |
| 2 | POST registration | `curl -X POST https://api2.khachvip.online/api/v1/tenant-registrations -H "Content-Type: application/json" -d '{"ShopName":"Test","ContactName":"Test","ContactPhone":"0900000001","Source":"demo","TurnstileToken":""}'` | 200 + registrationId |
| 3 | Honeypot silent reject | Same POST with `"HoneypotWebsite":"http://spam.com"` | 200 + `registrationId: "00000000-..."` (no DB record) |
| 4 | Rate limit | Spam 6+ requests same IP | 200 × 5, then 429 |
| 5 | /demo renders | `curl -sS -o /dev/null -w "%{http_code}" https://diemthuong2.khachvip.online/demo` | 200 |
| 6 | /claim renders | `curl -sS -o /dev/null -w "%{http_code}" https://diemthuong2.khachvip.online/claim` | 200 |
| 7 | E2E 6/6 | `KHACHLINK_URL=https://diemthuong2.khachvip.online npx playwright test gtm-demo.spec.ts` | 6 passed |

### 11.10 Giới hạn W2 (defer — không phải bug)

| Giới hạn | Lý do | Khi nào build |
|---|---|---|
| Turnstile chưa verify trên production | `Turnstile:SecretKey` chưa cấu hình (dev fallback skip) | W5 deploy — SysAdmin cấu hình keys |
| Rate limit dùng RemoteIpAddress (nginx IP) | Gateway chưa có `UseForwardedHeaders` cho registration endpoint | W5 deploy — thêm XFF handling |
| Admin queue chưa có UI | Chỉ có API endpoint | W3+ — thêm Blazor admin page trong ShopERP |
| Logo upload anonymous | Gateway `ImageUploadController` chưa verify anonymous upload path | W3 — verify hoặc defer to industry placeholder |
| Không tạo tenant tự động | Registration = lead, admin review → onboard | By design — tránh spam tenant |

---

## 12. BƯỚC TIẾP THEO (W3)

W1+W2 hoàn thành phễu **tự phát hiện → tự xem → tự trải nghiệm → tự đăng ký**. W3 sẽ thêm **tự thấy giá trị**:

- D1 domain mod — `StoreMetricDaily` entity (daily counters: orders, revenue, customers)
- GrowthDashboard "Tháng này" trên KhachLink — merchant Active thấy số đo được
- Counters từ PostgreSQL (Gateway source of truth)
- E2E `gtm-revenue-proof.spec.ts`

See `docs/AI/tasks/gtm_drill_mvp/task_card_w3_revenue_proof.md`.
