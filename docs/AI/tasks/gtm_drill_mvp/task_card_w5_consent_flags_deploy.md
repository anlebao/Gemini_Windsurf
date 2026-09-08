# Task Card W5: Consent + Flags + Deploy + RV

> **Status:** ⏳ PLANNED (awaiting session start)
> **Week:** W5 / 5
> **Effort:** ~2-3 ngày + deploy + RV
> **Master plan:** `docs/AI/tasks/gtm_drill_mvp/master_plan.md`
> **Top-level card:** `docs/AI/tasks/gtm_drill_mvp/task_card.md`
> **Prerequisite:** ✅ W4 COMPLETE

## Objective

1. Remote closing consent page — merchant cho phép Vạn An hỗ trợ từ xa (Zalo, KHÔNG build co-browse)
2. `GrowthMachine:Enabled` feature flag (default OFF — zero regression)
3. Meta/OG tags cho trang mới
4. Full validation + CD deploy + 5-layer RV

**Flag gating:** audit page/endpoint (W1), demo page (W2), metrics endpoints + dashboard (W3), refer page (W4) — tất cả gate bởi `GrowthMachine:Enabled`. Toggle ON cho test trên 1 subdomain trước khi bật toàn bộ.

## Scope Checklist

### Task 5.1: Remote support consent page
**File:** `5_WebApps/KhachLink/Pages/Support.razor` — NEW
- [ ] Nút [Cho phép Vạn An hỗ trợ từ xa] → mở Zalo OA/meet link từ SystemSetting `Support_ZaloUrl` [A]
- [ ] 1 trang + 1 setting, KHÔNG storage, KHÔNG co-browse
- [ ] Copy Zalo link button (JS interop `navigator.clipboard.writeText`)
- [ ] UI Platform components
- [ ] `[AllowAnonymous]` — merchant không cần login để mở consent (nhưng closer dùng Impersonation #103 cần admin auth)

### Task 5.2: Feature flag
**Files:** appsettings `2_Gateway` + `5_WebApps/KhachLink` + `5_WebApps/Directory`
- [ ] `GrowthMachine:Enabled` default **OFF** (pattern MultiProfile R1 — zero regression)
- [ ] Gate: audit page/endpoint (W1), demo page (W2), metrics endpoints + dashboard (W3), refer page (W4)
- [ ] Implementation: `IFeatureFlagService` (existing [V]) — check `GrowthMachine:Enabled` trong controller/page OnInitialized
- [ ] Toggle ON cho test trên 1 subdomain trước khi bật toàn bộ
- [ ] Verify flag state sau merge — flag còn OFF trên production

### Task 5.3: Meta/OG cho trang mới
- [ ] `Audit.razor` (W1) + `Demo.razor` (W2) + `Store.razor` (cửa hàng Active): title/OG cơ bản
- [ ] Full sitemap = BOM #4 T2, task riêng (DEFER)
- [ ] Meta tags: `og:title`, `og:description`, `og:image` (store logo nếu có), `og:url`

### Task 5.4: Full validation + deploy + RV
- [ ] `guard-check.ps1` + `dotnet build VanAn.sln` 0 errors + `dotnet test` ALL PASS
- [ ] 4 E2E specs mới chạy SAU khi build pass (Playwright isolation):
  - `gtm-audit.spec.ts` (W1 — đã có spec, chạy)
  - `gtm-demo.spec.ts` (W2)
  - `gtm-metrics.spec.ts` (W3)
  - `gtm-referral.spec.ts` (W4)
- [ ] CD Multi-VPS deploy → **RV 5-layer theo `.devin/rules/runtime-verification.md`**, stop at first failure:
  - **L1 API:** audit 200 + 429 khi spam; metrics upsert 200; monthly 200; claim with referrer 200
  - **L3:** static assets trang mới (blazor.web.js MIME đúng — precedent #157)
  - **L4:** /kiem-tra-cua-hang render + report; /demo mock storefront; /claim?ref= pass referrer qua API; /support consent page render; /growth/monthly dashboard render
  - **L5 DB:** bảng `StoreMetricDaily` + 2 cột mới `TenantClaimRequests` tồn tại trong PG
- [ ] Verify `GrowthMachine:Enabled` còn OFF trên production sau deploy

## Prerequisites

- ✅ W4 COMPLETE (referral + payout live)
- ✅ Impersonation #103 (RV pass — closer dùng admin auth)
- ✅ `IFeatureFlagService` (existing [V])
- ✅ `SystemSetting` table (live)
- ✅ CD Multi-VPS pipeline (`cd-multivps.yml`)
- ✅ Runtime Verification protocol (`.devin/rules/runtime-verification.md`)
- ✅ Playwright E2E infrastructure

## Verification (Definition of Done — W5 + toàn card)

- [ ] `dotnet build VanAn.sln` — 0 errors
- [ ] `guard-check.ps1` — ALL PASSED
- [ ] `dotnet test` — ALL PASS (unit + integration + arch)
- [ ] 4 E2E specs (`gtm-audit` / `gtm-demo` / `gtm-metrics` / `gtm-referral`) PASS — chạy sau build
- [ ] CD deploy SUCCESS + RV L1/L3/L4/L5 PASS
- [ ] Flag `GrowthMachine:Enabled` còn OFF trên production sau merge (bật riêng sau review)
- [ ] Meta/OG tags render trên Audit + Demo + Store pages
- [ ] Support page render + Zalo link từ SystemSetting

## Governance checklist

- [ ] Domain purity: 0 domain mods (W5 không đụng Domain.cs)
- [ ] KhachLink HTTP-only: Support page không inject DbContext
- [ ] UI Platform: Support.razor dùng VanAn.UI.Platform components
- [ ] Không tạo .csproj mới
- [ ] Feature flag: `GrowthMachine:Enabled` default OFF — zero regression (pattern MultiProfile R1)
- [ ] Flag gating: audit + demo + metrics + refer + support đều check flag
- [ ] No co-browse: Zalo/Meet screen-share + Impersonation #103 đủ
- [ ] Playwright isolation: E2E chỉ chạy SAU build pass + implementation complete

## Risks

| # | Risk | Mitigation |
|---|---|---|
| R1 | Flag leak ON production | Default OFF; verify flag state sau merge; toggle ON cho 1 subdomain test trước |
| R2 | E2E specs fail (ecosystem not up) | Chạy theo Playwright rules; env setup trước; nếu fail → triage theo `playwright_triage.md` |
| R3 | RV L3 fail (static assets MIME) | Precedent #157 — verify blazor.web.js MIME; nginx config check |
| R4 | RV L5 fail (migration not applied) | Verify PG migration applied; check `__EFMigrationsHistory` (Pattern #9 — PascalCase quoted) |
| R5 | Meta/OG tags không render SSR | Verify `<PageTitle>` + `<HeadContent>` trong Blazor SSR; check view-source |
| R6 | Support_ZaloUrl SystemSetting not seeded | Default fallback in code (empty string → hide button); seed via admin UI |

## Files

| # | File | Action | Status |
|---|---|---|---|
| 1 | `5_WebApps/KhachLink/Pages/Support.razor` | NEW | ⏳ |
| 2 | `2_Gateway/appsettings.json` + `5_WebApps/KhachLink/appsettings.json` + `5_WebApps/Directory/appsettings.json` | UPDATE (GrowthMachine:Enabled) | ⏳ |
| 3 | `2_Gateway/Controllers/GrowthController.cs` | UPDATE (flag check) | ⏳ |
| 4 | `5_WebApps/KhachLink/Pages/Demo.razor` | UPDATE (flag check + meta/OG) | ⏳ |
| 5 | `5_WebApps/Directory/Components/Pages/Audit.razor` | UPDATE (flag check + meta/OG) | ⏳ |
| 6 | `5_WebApps/KhachLink/Pages/Store.razor` | UPDATE (meta/OG) | ⏳ |
| 7 | `5_WebApps/KhachLink/Pages/Refer.razor` | UPDATE (flag check) | ⏳ |

## Open questions (resolve before/during W5 session)

1. **Flag check location:** Controller-level (`[FeatureGate("GrowthMachine")]`) hay page-level (`OnInitialized` check)? → Verify `IFeatureFlagService` pattern; controller-level cho API, page-level cho UI.
2. **Support_ZaloUrl:** Seed default hay empty? → Empty default (hide button if not set); SysAdmin set via admin UI.
3. **Meta/OG image:** Store logo nếu có, fallback Vạn An logo? → Verify OG image URL accessible (CDN/R2).
4. **Subdomain test:** Which subdomain for flag ON test? → `diemthuong2.khachvip.online` (existing test tenant) hoặc subdomain mới?

## RV Checklist (5-layer — stop at first failure)

### L1 — API checks
- [ ] `GET /api/v1/growth/audit?name=test` → 200 (flag ON) hoặc 404 (flag OFF)
- [ ] `POST /api/v1/growth/metrics/{tenantId}/event` → 200
- [ ] `GET /api/v1/growth/metrics/{tenantId}/monthly?month=current` → 200
- [ ] `POST /api/v1/tenant-claims/submit` with `ReferrerCustomerId` → 200
- [ ] Rate limit: 11th audit request → 429; 61st metrics request → 429

### L3 — Static assets
- [ ] `/_framework/blazor.web.js` → 200 + `application/javascript` MIME (precedent #157)
- [ ] `VanAn.Directory.styles.css` → 200
- [ ] `VanAn.KhachLink.styles.css` → 200

### L4 — UI flow
- [ ] `/kiem-tra-cua-hang` render + report (flag ON)
- [ ] `/demo` mock storefront render (flag ON)
- [ ] `/claim?ref={customerId}` pass referrer qua API
- [ ] `/support` consent page render + Zalo link
- [ ] `/growth/monthly` dashboard render (ShopERP, owner auth)

### L5 — DB
- [ ] `StoreMetricDaily` table tồn tại trong PG + unique index (TenantId, MetricDate)
- [ ] `TenantClaimRequests` + 2 cột mới (`ReferredByCustomerId`, `ReferralChannel`) tồn tại trong PG
- [ ] `__EFMigrationsHistory` có migration mới (Pattern #9 — PascalCase quoted)

## Related

- Master plan: `docs/AI/tasks/gtm_drill_mvp/master_plan.md`
- Top-level card: `docs/AI/tasks/gtm_drill_mvp/task_card.md`
- W4 (done): `docs/AI/tasks/gtm_drill_mvp/task_card_w4_merchant_referral.md`
- Runtime Verification protocol: `.devin/rules/runtime-verification.md`
- Playwright triage: `.devin/workflows/playwright_triage.md`
- Feature flag service: `3_CoreHub/Services/FeatureFlagService.cs`
- Impersonation (remote closing): Issue #103 (RV pass)
- CD pipeline: `.github/workflows/cd-multivps.yml`
