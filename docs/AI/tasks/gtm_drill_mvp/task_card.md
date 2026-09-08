# Task Card: GTM Drill Machine MVP — Máy Khoan Thủng Thị Trường (5 tuần)

> **Status:** ✅ W1 COMPLETE + PRODUCTION RV PASS (2026-09-07) · W2 NEXT
> **Priority:** P2 — Tuyến B (growth machine), chạy song song Tuyến A (KTV + HĐĐT + dogfood — việc thương mại, không phụ thuộc card này)
> **Created:** 2026-09-06
> **Revised:** 2026-09-07 (split into per-week task cards)
> **Branch:** `main` @ `e9e892e5`
> **Mode:** IMPLEMENT (W1 done, W2 next)
> **Master plan:** `docs/AI/tasks/gtm_drill_mvp/master_plan.md`
> **Source of truth:** `docs/AI/plans/ecosystem-master-business-model.md` Section 4 (GTM build map)
> **Workflow:** `newfeaturebuild.md`
> **Approval:** ✅ 2026-09-06 — user chốt: (1) D1/D2 domain mods APPROVED · (2) landing audit = Directory (timlathay.com) · (3) hoa hồng referral = dynamic do SystemAdmin đặt

## Progress

| Tuần | Mảnh | Status | Commit | Task card |
|---|---|---|---|---|
| W1 | Merchant Audit | ✅ COMPLETE + RV PASS | `e8cd4e62` + `a21fcffc` (nginx+429) | `task_card_w1_merchant_audit.md` |
| W2 | Interactive Demo | ⏳ NEXT | — | `task_card_w2_interactive_demo.md` |
| W3 | Revenue Proof (D1) | ⏳ | — | `task_card_w3_revenue_proof.md` |
| W4 | Merchant Referral (D2) | ⏳ | — | `task_card_w4_merchant_referral.md` |
| W5 | Consent + flags + deploy + RV | ⏳ | — | `task_card_w5_consent_flags_deploy.md` |

## Objective

Dựng 5 mảnh GTM còn thiếu của "Máy khoan thủng thị trường" trên hạ tầng có sẵn — biến merchant từ **tự phát hiện → tự xem → tự đăng ký**, sales chỉ vào sau intent:

1. **Merchant Audit** (W1) — landing "Kiểm tra cửa hàng" + report từ dữ liệu crawl có sẵn
2. **Interactive Demo** (W2) — preview storefront trước khi đăng ký — demo = onboarding
3. **Revenue Proof** (W3) — counter view/chat + dashboard "Tháng này" per merchant
4. **Merchant Referral** (W4) — QR/link CTV → claim attribution + hoa hồng manual
5. **Remote closing consent** (W5) — UX + Zalo, KHÔNG build co-browse

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

## Prerequisites

- ✅ Crawler + Claim pipeline deployed (PR #162-164, RV pass)
- ✅ Directory SSR live (timlathay.com, Blazor Server, port 8080)
- ✅ Gateway rate limiter infrastructure (`AddRateLimiter` L103-137)
- ✅ KhachLink Claim pipeline (`Claim.razor` + `ClaimHttpService`)
- ✅ `ImageUploadService` (KhachLink)
- ✅ `WalletService` credit method
- ✅ `qrcode.js` official v1.4.4 vendored (Guard QR fix `9f849e9`)
- ✅ Impersonation #103 (RV pass)
- ✅ `SystemSetting` table
- ✅ UI Platform csproj (`VanAn.UI.Platform.csproj`)
- ⏳ Domain mod D1/D2 approval — ✅ APPROVED 2026-09-06

## Verification (Definition of Done — across all 5 weeks)

- [ ] `dotnet build VanAn.sln` — 0 errors
- [ ] `guard-check.ps1` — ALL PASSED
- [ ] `dotnet test` — ALL PASS (unit + integration + arch)
- [ ] 4 E2E specs (`gtm-audit` / `gtm-demo` / `gtm-metrics` / `gtm-referral`) PASS — chạy sau build
- [ ] CD deploy SUCCESS + RV L1/L3/L4/L5 PASS
- [ ] Flag `GrowthMachine:Enabled` còn OFF trên production sau merge (bật riêng sau review)

## Governance checklist

- Domain purity: 2 mods (D1/D2) đều audit-type, precedent CrawlSource — KHÔNG đụng AccountingEntry/BaseEntity — ✅ user approved 2026-09-06
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

## Files Modified (expected — across all 5 weeks)

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

## Related

- Master plan: `docs/AI/tasks/gtm_drill_mvp/master_plan.md`
- Per-week task cards: `task_card_w1_merchant_audit.md` · `task_card_w2_interactive_demo.md` · `task_card_w3_revenue_proof.md` · `task_card_w4_merchant_referral.md` · `task_card_w5_consent_flags_deploy.md`
- Master Business Model: `docs/AI/plans/ecosystem-master-business-model.md` Section 4
- GTM source: `docs/requirements/mô hình kinh doanh (khoan thủng thị trường).md`
- Directory SSR (precedent): `docs/AI/tasks/directory_ssr/`
- Crawl-to-Onboard (precedent): `docs/AI/plans/crawl-onboarding-master-plan.md`
