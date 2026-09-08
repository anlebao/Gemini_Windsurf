# GTM Drill Machine MVP — Master Plan

**Created:** 2026-09-06
**Revised:** 2026-09-07 (split into folder + per-week task cards)
**Status:** W1 COMPLETE + PRODUCTION RV PASS · W2 NEXT
**Branch target:** `main` @ `e9e892e5`
**Source:** User request — dựng 5 mảnh GTM còn thiếu của "Máy khoan thủng thị trường" trên hạ tầng có sẵn. Strategy: `docs/AI/plans/ecosystem-master-business-model.md` Section 4 (GTM build map).

## Problem

### Hiện trạng (verify 2026-09-06)

Vạn An có hạ tầng kỹ thuật mạnh (Crawler + Claim + Wallet + Accounting + FI + Directory SSR + Impersonation) nhưng thiếu 5 mảnh GTM để biến merchant từ **tự phát hiện → tự xem → tự đăng ký**, sales chỉ vào sau intent:

1. Merchant Audit — merchant tự kiểm tra "cửa hàng tôi có trong mạng lưới Vạn An không"
2. Interactive Demo — merchant tự xem storefront mock trước khi đăng ký
3. Revenue Proof — merchant tự thấy "Vạn An đã tạo ra bao nhiêu doanh số cho tôi"
4. Merchant Referral — CTV giới thiệu merchant qua QR/link + hoa hồng
5. Remote closing consent — merchant cho phép Vạn An hỗ trợ từ xa (Zalo, không co-browse)

### GTM review đã vá 3 hố (2026-09-06)

| Hố | Bản gốc | Bản vá |
|---|---|---|
| Fake precision trong Merchant Analyzer | "420 người có hành vi tìm kiếm quanh 2km", "+10-20 đơn/ngày" | Chỉ hiển thị số đo được (search/view/order thật trên TimLaThay) + nhãn "ước tính" cho mô hình. Trước khi có traffic: audit chỉ từ dữ liệu crawl công khai. KHÔNG tích Google Places/FB Graph pha 1 (phí + ToS + không scrape page người khác) |
| Cold-start demand | Giả định có traffic khách tìm | Traffic khách chính = **SEO từ chính các trang listing crawl** (free, hàng nghìn trang ngành×tỉnh) — kênh này tài liệu gốc không nhắc. Ads chỉ test nhỏ SAU khi funnel có chuyển hóa tự nhiên. Metric trung gian trung thực: searches → views → chat (chưa hứa GMV) |
| Scoring không có dữ liệu | Bảng điểm 9 tín hiệu | Codebase hiện **0 event tracking** [V]. Scoring tự động defer (Gate G1). Giai đoạn 0: scoring thủ công bằng ClaimsQueue + admin dashboard đã có |

### Out of scope (DEFER — chống over-build)

- AI SDR + auto scoring — **Gate G1**: chỉ build sau khi event pipeline có ≥30 ngày dữ liệu thật (hiện 0 telemetry [V]). Giai đoạn này scoring thủ công bằng ClaimsQueue + admin dashboard.
- Google Places / FB Graph enrichment cho audit (phí + ToS; pha 1 chỉ dùng dữ liệu crawl + đo được nội bộ)
- Co-browsing tự build (Zalo/Meet screen-share + impersonation #103 đủ)
- Search counter (đếm lượt tìm dẫn tới store) — thuộc event pipeline G1, MVP chỉ view/chat
- `sitemap.xml` toàn site — thuộc BOM #4 T2, task riêng
- Payment gateway — Gate G2
- Upgrade/thanh toán flow tự động — thuộc BOM #1/#4 (chuyển khoản + admin bật tay giai đoạn đầu); card chỉ xây tới "merchant thấy tiền" (dashboard W3)

## Solution

### Kiến trúc tổng thể

```
Merchant journey (zero-CAC flywheel):

  Crawler → Pending tenant (crawl doanhnghiep.vn)
     ↓
  SEO landing (Directory SSR — timlathay.com)
     ↓
  /kiem-tra-cua-hang (W1 — audit "cửa hàng tôi có không?")
     ↓
  /demo (W2 — preview storefront mock)
     ↓
  /claim?name=... (existing — Claim pipeline)
     ↓
  Active storefront + Store page (existing)
     ↓
  /growth/monthly (W3 — "Tháng này Vạn An tạo ra X doanh số cho bạn")
     ↓
  CTV /refer (W4 — QR referral + hoa hồng)
     ↓
  /support (W5 — consent remote closing → Zalo)
```

### Tech approach

- **W1 Merchant Audit:** Gateway `GrowthController` (anonymous, rate-limited) + Directory SSR landing `/kiem-tra-cua-hang`. 0 infra mới.
- **W2 Interactive Demo:** KhachLink `/demo` page, in-memory state (no persistence), reuse `ImageUploadService` + storefront markup pattern.
- **W3 Revenue Proof:** Domain mod D1 (`StoreMetricDaily` audit entity, precedent `CrawlSource`) + Gateway metrics endpoints + KhachLink counter wiring + ShopERP dashboard.
- **W4 Merchant Referral:** Domain mod D2 (`TenantClaimRequest` +2 nullable referral fields) + KhachLink `/refer` page (QR) + ShopERP admin manual payout (dynamic amount).
- **W5 Consent + flags + deploy + RV:** KhachLink `/support` consent page + `GrowthMachine:Enabled` flag (default OFF) + full validation + CD deploy + 5-layer RV.

### Quy tắc số liệu (Ground Rule 5 — bắt buộc mọi UI mới)

- Chỉ hiển thị số ĐO ĐƯỢC: có/không trong mạng lưới, có storefront, có social, IndustryPeerCount (đếm từ dữ liệu crawl — "Trong dữ liệu Vạn An có X cửa hàng cùng ngành").
- Mọi ước tính phải dán nhãn "ước tính". Cấm fake precision kiểu "420 lượt tìm quanh đây".

## Component reuse map

| Hạ tầng có sẵn [V] | Mảnh GTM dùng | Reuse strategy |
|---|---|---|
| Crawler + CrawlSources + TenantSearch 4 cấp | W1 Audit | Query crawl data cho IndustryPeerCount |
| Directory SSR (Blazor Server, timlathay.com) | W1 Audit landing | Add `/kiem-tra-cua-hang` route |
| Gateway rate limiter (`AddRateLimiter` L103-137) | W1 + W3 | Add `growth-audit` + `growth-metrics` policies |
| KhachLink Claim pipeline (`Claim.razor` + `ClaimHttpService`) | W2 + W4 | Prefill `?name=` (W2) + `?ref=` (W4) |
| KhachLink `ImageUploadService` | W2 Demo | Logo upload in demo |
| KhachLink `Store.razor` storefront markup | W2 Demo | Reuse card pattern |
| `Order` aggregate + PG query | W3 Revenue Proof | Query orders by month for GMV |
| `WalletService` credit method | W4 Referral payout | Credit CTV wallet (manual trigger) |
| `qrcode.js` official v1.4.4 vendored (Guard QR fix `9f849e9`) | W4 Refer page | QR generation |
| Impersonation #103 | W5 Remote closing | SysAdmin impersonate merchant (existing) |
| `SystemSetting` table | W4 + W5 | `Referral_CommissionAmount` + `Support_ZaloUrl` |
| UI Platform components | ALL pages | 100% trang mới dùng VanAn.UI.Platform |

## Gateway API endpoints

| Endpoint | Method | Purpose | Week | Status |
|---|---|---|---|---|
| `/api/v1/growth/audit?name=&mst=` | GET | Merchant audit from crawl data | W1 | ✅ LIVE |
| `/api/v1/growth/metrics/{tenantId}/event` | POST | Record view/chat event | W3 | ⏳ |
| `/api/v1/growth/metrics/{tenantId}/monthly?month=` | GET | Monthly metrics aggregate | W3 | ⏳ |

## Files mới cần tạo

```
2_Gateway/Controllers/GrowthController.cs          (W1 ✅ + W3 UPDATE)
5_WebApps/Directory/Components/Pages/Audit.razor   (W1 ✅)
5_WebApps/Directory/Services/GrowthAuditService.cs (W1 ✅)
5_WebApps/KhachLink/Pages/Demo.razor               (W2)
5_WebApps/KhachLink/Pages/DemoStoreState.cs        (W2 — in-memory model)
1_Shared/Domain/Aggregates/GrowthAggregate/StoreMetricDaily.cs (W3 — D1)
3_CoreHub/Infrastructure/Configurations/StoreMetricDailyConfiguration.cs (W3)
5_WebApps/KhachLink/Services/Http/GrowthHttpService.cs (W3)
5_WebApps/ShopERP/Pages/Growth/Monthly.razor       (W3)
5_WebApps/KhachLink/Pages/Refer.razor              (W4)
5_WebApps/KhachLink/Pages/Support.razor            (W5)
6_Testing/e2e-tests/gtm-audit.spec.ts              (W1 ✅)
6_Testing/e2e-tests/gtm-demo.spec.ts               (W2)
6_Testing/e2e-tests/gtm-metrics.spec.ts            (W3)
6_Testing/e2e-tests/gtm-referral.spec.ts           (W4)
6_Tests/VanAn.Core.Tests/Growth/...                (W3, W4)
```

## Files sửa

```
2_Gateway/Program.cs                               (W1 ✅ + W3 + W5 — rate limits + flag)
5_WebApps/KhachLink/Pages/Claim.razor              (W2 + W4 — name + ref prefill)
5_WebApps/KhachLink/Pages/Store.razor              (W3 — counter wiring)
1_Shared/Domain/.../TenantClaimRequest.cs          (W4 — D2 +2 fields)
1_Shared/DTOs/.../ClaimDtos.cs                     (W4 — referrer field)
2_Gateway/Controllers/TenantClaimController.cs     (W4 — referrer pass-through)
3_CoreHub/Services/TenantClaimService.cs           (W4 — referrer attribution)
5_WebApps/ShopERP/Pages/Admin/TenantManagement.razor (W4 — Referrals tab)
3_CoreHub/Infrastructure/VanAnDbContext.cs + IVanAnDbContext.cs (W3 — DbSet)
5_WebApps/ShopERP/ShopERPDbContext.cs              (W3 — Ignore<StoreMetricDaily>)
appsettings (Gateway + KhachLink + Directory)      (W5 — GrowthMachine:Enabled)
nginx/templates/vanan.multivps.conf.template       (W1 ✅ — Directory SSR routing)
```

## Phases (5 weeks × 1 mảnh)

### W1 — Merchant Audit ✅ COMPLETE + RV PASS (2026-09-07)
- Gateway `GrowthController` (anonymous, rate-limit 10/IP/h, 429 response)
- Directory `/kiem-tra-cua-hang` landing (Blazor Server SSR)
- nginx routing fix (timlathay.com → Directory SSR port 8080)
- E2E spec (4 tests, chưa chạy — cần ecosystem)
- Production RV 9/9 PASS
- **Task card:** `task_card_w1_merchant_audit.md`

### W2 — Interactive Demo ⏳ NEXT
- KhachLink `/demo` page (in-memory storefront mock)
- Demo → Claim prefill (`?name=`)
- E2E spec
- **Task card:** `task_card_w2_interactive_demo.md`

### W3 — Revenue Proof (D1) ⏳
- Domain mod D1: `StoreMetricDaily` audit entity (APPROVED)
- EF config + PG migration
- Gateway metrics endpoints (POST event + GET monthly)
- KhachLink counter wiring (Store page)
- ShopERP merchant dashboard `/growth/monthly`
- Tests + E2E
- **Task card:** `task_card_w3_revenue_proof.md`

### W4 — Merchant Referral (D2) ⏳
- Domain mod D2: `TenantClaimRequest` +2 nullable referral fields (APPROVED)
- PG migration
- Claim flow wiring (referrer pass-through)
- KhachLink `/refer` page (QR + copy link)
- ShopERP admin manual payout (dynamic amount)
- Tests + E2E
- **Task card:** `task_card_w4_merchant_referral.md`

### W5 — Consent + flags + deploy + RV ⏳
- KhachLink `/support` consent page (Zalo link from SystemSetting)
- `GrowthMachine:Enabled` flag (default OFF — zero regression)
- Meta/OG tags for new pages
- Full validation + CD deploy + 5-layer RV
- **Task card:** `task_card_w5_consent_flags_deploy.md`

## Hard stops (governance)

- **Domain PURE** — 2 mods (D1/D2) đều audit-type, precedent CrawlSource. KHÔNG đụng `AccountingEntry`/`BaseEntity`. **Chờ user approve trước W3/W4** (✅ APPROVED 2026-09-06).
- **UI Platform components bắt buộc** — 100% trang mới dùng VanAn.UI.Platform, no custom CSS.
- **Gateway = Order Creator + Routed Async Delivery (Option C)** — không revert.
- **Multi-tenancy enforced at every layer** — Pattern #8: mọi query tenant so sánh property trực tiếp, cấm `EF.Property<Guid>`.
- **KhachLink HTTP-only** — KHÔNG inject DbContext, mọi data qua Gateway.
- **No new .csproj** — dùng existing projects.
- **M3/legal:** audit không lộ CrawledPhone; Pending tenant chỉ name (no phone/email/address).
- **Playwright isolation** — E2E chỉ chạy SAU khi build pass + implementation complete.

## Risks

| # | Risk | Mitigation |
|---|---|---|
| R1 | Domain mod D1/D2 phá AccountingEntry/BaseEntity | Audit-type entity (precedent CrawlSource), FK qua `BaseEntity.TenantId`, Single-Identity pattern. KHÔNG đụng immutable entities. |
| R2 | KhachLink counter wiring gây chậm Store page | 1 POST view call/page load (không batch MVP); fire-and-forget; rate limit 60/IP/min |
| R3 | Referral payout sai amount | Manual MVP (admin confirm); amount dynamic editable trong modal; auto trigger = DEFER |
| R4 | Flag `GrowthMachine:Enabled` leak ON production | Default OFF; toggle ON cho 1 subdomain test trước; verify flag state sau merge |
| R5 | E2E specs cần ecosystem lên | Chạy theo Playwright rules (sau build pass); env `DIRECTORY_URL`/`AUDIT_TENANT_NAME`/`AUDIT_PENDING_TENANT_NAME` |
| R6 | nginx routing sai (W1 đã gặp) | RV Layer 3 verify static assets + Blazor Server SSR (precedent #157) |
| R7 | Over-build (cái chết §13 tài liệu gốc) | Ground Rule 6 + Gates G1-G5 + kill-list hằng quý + Out of scope list |

## Decisions (RESOLVED 2026-09-06 — user approved)

1. **Domain mods D1 + D2: APPROVED** — `StoreMetricDaily` (audit entity, precedent CrawlSource) + `TenantClaimRequest` +2 nullable referral fields. Không đụng AccountingEntry/BaseEntity.
2. **Landing audit = Directory app (timlathay.com)** — route `/kiem-tra-cua-hang`, 0 infra mới (không subdomain riêng).
3. **Hoa hồng referral = dynamic bởi SystemAdmin** — prefill từ SystemSetting `Referral_CommissionAmount`, chỉnh trong payout modal + option đặt làm mặc định mới.

## Success metrics (đo sau 4 tuần bật flag — target [A], chỉnh theo dữ liệu thật)

| Metric | Target [A] |
|---|---|
| Audit runs/tuần | ≥ 200 |
| Audit → claim conversion | ≥ 3% |
| Demo → claim conversion | ≥ 10% |
| Claim có referrer | ≥ 20% claim mới |
| Merchant mở dashboard Monthly | ≥ 30% anchor active |

## Related

- Master Business Model: `docs/AI/plans/ecosystem-master-business-model.md` Section 4 (GTM build map)
- Task cards: `docs/AI/tasks/gtm_drill_mvp/task_card_w1_merchant_audit.md` · `task_card_w2_interactive_demo.md` · `task_card_w3_revenue_proof.md` · `task_card_w4_merchant_referral.md` · `task_card_w5_consent_flags_deploy.md`
- Top-level task card: `docs/AI/tasks/gtm_drill_mvp/task_card.md`
- GTM source: `docs/requirements/mô hình kinh doanh (khoan thủng thị trường).md`
- Directory SSR (precedent): `docs/AI/tasks/directory_ssr/`
- Crawl-to-Onboard (precedent): `docs/AI/plans/crawl-onboarding-master-plan.md`
