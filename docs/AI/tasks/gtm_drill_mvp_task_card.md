# TASK CARD — Máy Khoan Thủng MVP (GTM Growth Machine, 5 tuần)

> **Status:** ✅ APPROVED 2026-09-06 — user chốt: (1) D1/D2 domain mods APPROVED · (2) landing audit = Directory (timlathay.com) · (3) hoa hồng referral = dynamic do SystemAdmin đặt
> **Priority:** P2 — Tuyến B (growth machine), chạy song song Tuyến A (KTV + HĐĐT + dogfood — việc thương mại, không phụ thuộc card này)
> **Branch:** `feature/gtm-drill-mvp` (từ `main`)
> **Mode:** IMPLEMENT — W1 CODE COMPLETE (`e8cd4e62`: build 0 errors + pre-commit GUARD PASS + arch 41/41; E2E run pending ecosystem). Next: W2 Interactive Demo.
> **Source of truth:** `docs/AI/plans/ecosystem-master-business-model.md` Section 4 (GTM build map) · GTM review đã vá 3 hố (fake precision / cold-start / scoring)
> **Workflow:** `newfeaturebuild.md`

## Objective

Dựng 5 mảnh GTM còn thiếu của "Máy khoan thủng thị trường" trên hạ tầng có sẵn — biến merchant từ **tự phát hiện → tự xem → tự đăng ký**, sales chỉ vào sau intent:

1. Merchant Audit (landing "Kiểm tra cửa hàng" + report từ dữ liệu crawl có sẵn)
2. Interactive Demo (preview storefront trước khi đăng ký — demo = onboarding)
3. Revenue Proof (counter view/chat + dashboard "Tháng này" per merchant)
4. Merchant Referral (QR/link CTV → claim attribution + hoa hồng manual)
5. Remote closing consent (UX + Zalo, KHÔNG build co-browse)

## Out of scope (DEFER — chống over-build)

- AI SDR + auto scoring — **Gate G1**: chỉ build sau khi event pipeline có ≥30 ngày dữ liệu thật (hiện 0 telemetry [V]). Giai đoạn này scoring thủ công bằng ClaimsQueue + admin dashboard.
- Google Places / FB Graph enrichment cho audit (phí + ToS; pha 1 chỉ dùng dữ liệu crawl + đo được nội bộ)
- Co-browsing tự build (Zalo/Meet screen-share + impersonation #103 đủ)
- Search counter (đếm lượt tìm dẫn tới store) — thuộc event pipeline G1, MVP chỉ view/chat
- `sitemap.xml` toàn site — thuộc BOM #4 T2, task riêng
- Payment gateway — Gate G2
- Upgrade/thanh toán flow tự động — thuộc BOM #1/#4 (chuyển khoản + admin bật tay giai đoạn đầu); card chỉ xây tới "merchant thấy tiền" (dashboard W3)

## Quy tắc số liệu (Ground Rule 5 — bắt buộc mọi UI mới)

- Chỉ hiển thị số ĐO ĐƯỢC: có/không trong mạng lưới, có storefront, có social, IndustryPeerCount (đếm từ dữ liệu crawl — "Trong dữ liệu Vạn An có X cửa hàng cùng ngành").
- Mọi ước tính phải dán nhãn "ước tính". Cấm fake precision kiểu "420 lượt tìm quanh đây".

---

## TUẦN 1 — Merchant Audit

### Task 1.1: Gateway audit endpoint
**File:** `2_Gateway/Controllers/GrowthController.cs` — NEW
- `GET /api/v1/growth/audit?name={q}&mst={taxCode}` — `[AllowAnonymous]` + `[EnableRateLimiting("growth-audit")]`
- Logic: match tenant theo tên (ILIKE, theo pattern `TenantStoreController.Search` L223-228) HOẶC theo MST (`Settings.TaxCode` — public business data). Input SĐT/FB link DEFER v2 (SĐT crawl = internal-only per M3, không dùng làm public lookup). → build `MerchantAuditDto`:
  - `Found`: name, slug, `HasStorefront` (slug != null), `HasKhachLinkDomain`, `SocialLinks`, `IsPending`
  - `IndustryPeerCount`: `Count(Tenants where Settings.BusinessField ILIKE '%{industryToken}%')` — số đo được từ crawl
  - `Checklist`: hiện diện danh bạ / storefront / kênh đặt hàng / social
  - `ClaimUrl`: link claim KhachLink
- **Cấm** trả `CrawledPhone` (M3 — internal only). Pending tenant: chỉ name + IsPending (không address/phone).
- Query tenant theo Pattern #8: `t.Id == new TenantId(guid)` / so sánh trực tiếp property — cấm `EF.Property<Guid>`.

### Task 1.2: Rate limit policy
**File:** `2_Gateway/Program.cs`
- Thêm policy `growth-audit`: 10 req/IP/hour FixedWindow (pattern như `claim-submit` — đã có `AddRateLimiter` L103-137 [V])

### Task 1.3: Directory landing page
**Files:** `5_WebApps/Directory/Components/Pages/Audit.razor` — NEW · `5_WebApps/Directory/Services/GrowthAuditService.cs` — NEW
- Route `/kiem-tra-cua-hang` (SSR, SEO-friendly, dùng `CatalogService` pattern gọi Gateway)
- **Copy theo nguyên tắc "bán kết quả, không bán phần mềm" (§2 GTM):** tiêu đề dùng câu hỏi kết quả kiểu "Thử xem khách gần cửa hàng bạn đang tìm gì" / "Kiểm tra cửa hàng bạn có thể nhận thêm bao nhiêu đơn quanh đây" — KHÔNG mô tả "giải pháp quản lý bán hàng toàn diện"
- Form nhập tên cửa hàng hoặc MST → render report bằng **UI Platform components** + meta/OG tags
- CTA: "Đưa cửa hàng lên TimLaThay — Đăng ký miễn phí" → `{KhachLink domain}/claim?name={...}`
- Không load danh sách khi vào trang (pattern #157 — tránh initial load)

### Task 1.4: E2E
**File:** `6_Testing/e2e-tests/gtm-audit.spec.ts` — NEW (Gate 4)
- Truy cập /kiem-tra-cua-hang → nhập tên tenant test → thấy report + CTA; spam >10 lần → 429

---

## TUẦN 2 — Interactive Demo

### Task 2.1: Demo preview page
**Files:** `5_WebApps/KhachLink/Pages/Demo.razor` — NEW (+ `DemoStoreState.cs` model in-memory)
- Route `/demo`, anonymous, **không persistence** (session-only state)
- Input: tên quán + ngành → dựng storefront mock: logo (upload qua `ImageUploadService` có sẵn), 3-5 sản phẩm mẫu theo ngành (name + price editable, thêm/xóa), giờ mở cửa (editable), chọn màu theme
- Render tái dùng markup pattern từ `Store.razor` (storefront card). **UI Platform components.**

### Task 2.2: Demo → Claim chuyển tiếp
- Nút "Đưa cửa hàng lên TimLaThay" → `NavigateTo($"/claim?name={Uri.EscapeDataString(demoName)}")`
- **File:** `5_WebApps/KhachLink/Pages/Claim.razor` — UPDATE: đọc query param `name` (và `ref` ở Tuần 4) prefill form

### Task 2.3: E2E
**File:** `6_Testing/e2e-tests/gtm-demo.spec.ts` — NEW
- Vào /demo → nhập tên → thấy storefront mock → sửa 1 sản phẩm → bấm nút → landing /claim với tên đã prefill

---

## TUẦN 3 — Revenue Proof (counters + dashboard)

### Task 3.1: DOMAIN MOD D1 (cần approve) — audit entity
**File:** `1_Shared/Domain/Aggregates/GrowthAggregate/StoreMetricDaily.cs` — NEW
- Precedent: `CrawlSource` (audit entity, PG-only, FK qua `BaseEntity.TenantId`, Single-Identity: Id = PK GUIDv7, không business key VO)
- Fields: `MetricDate` (date-only), `StoreViews` (int), `ChatClicks` (int) — unique (TenantId, MetricDate)
- KHÔNG đụng `AccountingEntry`/`BaseEntity`. Orders/GMV KHÔNG denormalize — query runtime từ Orders.

### Task 3.2: EF config + migration
**Files:** `3_CoreHub/Infrastructure/Configurations/StoreMetricDailyConfiguration.cs` — NEW · `IVanAnDbContext` + `VanAnDbContext` + DbSet · `5_WebApps/ShopERP/ShopERPDbContext` + `Ignore<StoreMetricDaily>()` (PG-only, pattern TenantClaimRequest) · PG migration — NEW

### Task 3.3: Metrics API
**File:** `2_Gateway/Controllers/GrowthController.cs` — UPDATE
- `POST /api/v1/growth/metrics/{tenantId}/event` (body `{type: "view"|"chat"}`) — `[AllowAnonymous]` + rate limit `growth-metrics` (60/IP/min) — upsert ngày hiện tại (idempotent increment)
- `GET /api/v1/growth/metrics/{tenantId}/monthly?month=yyyy-MM` → `{ storeViews, chatClicks, orders, gmv }` (orders/GMV query Orders theo tháng; Pattern #8: filter `e.TenantId == tenantId` trực tiếp)

### Task 3.4: KhachLink counter wiring
**Files:** `5_WebApps/KhachLink/Services/Http/GrowthHttpService.cs` — NEW · `5_WebApps/KhachLink/Pages/Store.razor` — UPDATE
- Store page load xong → POST view (1 call, không batch MVP); click nút chat/call → POST chat
- HTTP-only qua Gateway — KHÔNG inject DbContext (hard stop KhachLink)

### Task 3.5: Merchant dashboard
**File:** `5_WebApps/ShopERP/Pages/Growth/Monthly.razor` — NEW
- "Tháng này của bạn": lượt xem cửa hàng → lượt chat → đơn → GMV (funnel Revenue Proof — pay-after-value)
- `[Authorize(Policy="RequireOwnerRole")]`, **UI Platform components**

### Task 3.6: Tests
- Service: upsert idempotent (2 lần POST view → 2, không tạo row mới)
- Controller integration: anonymous POST 200; monthly aggregate đúng số

---

## TUẦN 4 — Merchant Referral

### Task 4.1: DOMAIN MOD D2 (cần approve) — claim referral fields
**File:** `1_Shared/Domain/Aggregates/TenantAggregate/TenantClaimRequest.cs` — UPDATE (file theo vị trí thực tế khi implement)
- Thêm 2 nullable fields: `ReferredByCustomerId` (Guid?) + `ReferralChannel` (string? "qr"|"link") — backward compatible (existing rows NULL), không đổi identity

### Task 4.2: Migration
- PG migration: `TenantClaimRequests` + 2 cột (PG-only)

### Task 4.3: Claim flow wiring
**Files:** `1_Shared/DTOs/.../ClaimDtos.cs` — UPDATE (`SubmitClaimRequest` + `ReferrerCustomerId`) · `2_Gateway/Controllers/TenantClaimController.cs` — UPDATE · `3_CoreHub/Services/TenantClaimService.cs` — UPDATE (SubmitClaimAsync ghi nhận referrer)

### Task 4.4: CTV refer page (KhachLink)
**File:** `5_WebApps/KhachLink/Pages/Refer.razor` — NEW (customer auth)
- Hiển thị link `{domain}/claim?ref={customerId:N}` + QR code (dùng qrcode.js official v1.4.4 vendored — precedent Guard QR fix `9f849e9`)
- Copy link button

### Task 4.5: Claim.razor đọc `?ref=`
**File:** `5_WebApps/KhachLink/Pages/Claim.razor` — UPDATE (đã mở ở Task 2.2)

### Task 4.6: Admin referral payout (manual MVP)
**File:** `5_WebApps/ShopERP/Pages/Admin/TenantManagement.razor` — UPDATE (tab "Referrals" hoặc mở rộng ClaimsQueue)
- List claim đã approve có referrer + nút "Tín dụng hoa hồng" → credit Wallet CTV (dùng phương thức credit có sẵn của `WalletService` — verify lúc implement) — amount **dynamic do SystemAdmin đặt**: prefill từ SystemSetting `Referral_CommissionAmount` (default 50.000đ [A]), chỉnh trực tiếp trong payout modal + option "đặt làm mặc định mới"
- **Manual payout MVP** — auto trigger khi có payment tracking = DEFER

### Task 4.7: Tests
- Domain: TenantClaimRequest giữ referrer fields
- Service: SubmitClaimAsync với/without referrer; approve → payout credit đúng amount

---

## TUẦN 5 — Remote closing + flags + deploy + RV

### Task 5.1: Remote support consent page
**File:** `5_WebApps/KhachLink/Pages/Support.razor` — NEW
- Nút [Cho phép Vạn An hỗ trợ từ xa] → mở Zalo OA/meet link từ SystemSetting `Support_ZaloUrl` [A] — 1 trang + 1 setting, KHÔNG storage, KHÔNG co-browse

### Task 5.2: Feature flag
**Files:** appsettings `2_Gateway` + `5_WebApps/KhachLink` + `5_WebApps/Directory`
- `GrowthMachine:Enabled` default **OFF** (pattern MultiProfile R1 — zero regression) — gate: audit page/endpoint, demo page, metrics endpoints + dashboard, refer page
- Toggle ON cho test trên 1 subdomain trước khi bật toàn bộ

### Task 5.3: Meta/OG cho trang mới
- Audit.razor + Demo.razor + Store (cửa hàng Active): title/OG cơ bản (full sitemap = BOM #4 T2, task riêng)

### Task 5.4: Full validation + deploy + RV
- `guard-check.ps1` + `dotnet build VanAn.sln` 0 errors + `dotnet test` ALL PASS
- 4 E2E specs mới chạy SAU khi build pass (Playwright isolation)
- CD Multi-VPS deploy → **RV 5-layer theo `.devin/rules/runtime-verification.md`**, stop at first failure:
  - L1 API: audit 200 + 429 khi spam; metrics upsert 200; monthly 200
  - L3: static assets trang mới (blazor.web.js MIME đúng — precedent #157)
  - L4: /kiem-tra-cua-hang render + report; /demo mock storefront; /claim?ref= pass referrer qua API
  - L5 DB: bảng `StoreMetricDaily` + 2 cột mới `TenantClaimRequests` tồn tại trong PG

---

## Validation (Definition of Done)

- [ ] `dotnet build VanAn.sln` — 0 errors
- [ ] `guard-check.ps1` — ALL PASSED
- [ ] `dotnet test` — ALL PASS (unit + integration + arch)
- [ ] 4 E2E specs (`gtm-audit` / `gtm-demo` / growth-metrics / claim-ref) PASS — chạy sau build
- [ ] CD deploy SUCCESS + RV L1/L3/L4/L5 PASS
- [ ] Flag `GrowthMachine:Enabled` còn OFF trên production sau merge (bật riêng sau review)

## Governance checklist

- Domain purity: 2 mods (D1/D2) đều audit-type, precedent CrawlSource — KHÔNG đụng AccountingEntry/BaseEntity — **chờ user approve trước Tuần 3/4**
- KhachLink HTTP-only: mọi data qua `GrowthHttpService` → Gateway
- UI Platform: 100% trang mới dùng VanAn.UI.Platform — no custom CSS
- Không tạo .csproj mới
- Pattern #8: mọi query tenant/metrics so sánh property trực tiếp — cấm `EF.Property<Guid>`
- M3/legal: audit không lộ CrawledPhone; Pending chỉ name

## Success metrics (đo sau 4 tuần bật flag — target [A], chỉnh theo dữ liệu thật)

| Metric | Target [A] |
|---|---|
| Audit runs/tuần | ≥ 200 |
| Audit → claim conversion | ≥ 3% |
| Demo → claim conversion | ≥ 10% |
| Claim có referrer | ≥ 20% claim mới |
| Merchant mở dashboard Monthly | ≥ 30% anchor active |

## Files Modified (expected)

| # | File | Action | Tuần |
|---|---|---|---|
| 1 | `2_Gateway/Controllers/GrowthController.cs` | NEW | 1, 3 |
| 2 | `2_Gateway/Program.cs` | UPDATE (rate limits + flag) | 1, 3, 5 |
| 3 | `5_WebApps/Directory/Components/Pages/Audit.razor` | NEW | 1 |
| 4 | `5_WebApps/Directory/Services/GrowthAuditService.cs` | NEW | 1 |
| 5 | `5_WebApps/KhachLink/Pages/Demo.razor` (+ DemoStoreState) | NEW | 2 |
| 6 | `5_WebApps/KhachLink/Pages/Claim.razor` | UPDATE (name + ref prefill) | 2, 4 |
| 7 | `1_Shared/Domain/.../StoreMetricDaily.cs` | NEW (D1) | 3 |
| 8 | `3_CoreHub/Infrastructure/Configurations/StoreMetricDailyConfiguration.cs` + DbSet + migrations | NEW (D1) | 3 |
| 9 | `5_WebApps/KhachLink/Services/Http/GrowthHttpService.cs` | NEW | 3 |
| 10 | `5_WebApps/KhachLink/Pages/Store.razor` | UPDATE (counters) | 3 |
| 11 | `5_WebApps/ShopERP/Pages/Growth/Monthly.razor` | NEW | 3 |
| 12 | `1_Shared/Domain/.../TenantClaimRequest.cs` + ClaimDtos + TenantClaimController + TenantClaimService + migration | UPDATE (D2) | 4 |
| 13 | `5_WebApps/KhachLink/Pages/Refer.razor` | NEW | 4 |
| 14 | `5_WebApps/ShopERP/Pages/Admin/TenantManagement.razor` | UPDATE (Referrals) | 4 |
| 15 | `5_WebApps/KhachLink/Pages/Support.razor` | NEW | 5 |
| 16 | `6_Testing/e2e-tests/gtm-*.spec.ts` | NEW ×4 | 1-5 |
| 17 | `6_Tests/VanAn.Core.Tests/Growth/...` | NEW | 3, 4 |

## Decisions (RESOLVED 2026-09-06 — user approved)

1. **Domain mods D1 + D2: APPROVED** — `StoreMetricDaily` (audit entity, precedent CrawlSource) + `TenantClaimRequest` +2 nullable referral fields. Không đụng AccountingEntry/BaseEntity.
2. **Landing audit = Directory app (timlathay.com)** — route `/kiem-tra-cua-hang`, 0 infra mới (không subdomain riêng).
3. **Hoa hồng referral = dynamic bởi SystemAdmin** — prefill từ SystemSetting `Referral_CommissionAmount`, chỉnh trong payout modal + option đặt làm mặc định mới.
