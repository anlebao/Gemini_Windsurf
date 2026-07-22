# TASK CARD: THEME - PHASE 4 - KhachLink Render Theme

## 1. GOAL & CONTEXT
- **Mục tiêu cốt lõi:** KhachLink đọc theme từ API và render đúng theme cho cả 2 hệ thống: KhachLink pages (Home, Cart, Checkout) và Store profile page (/store/{slug}).
- **Nghiệp vụ áp dụng:** Khách hàng mở KhachLink → layout tự động apply theme sysadmin đã chọn. Đổi theme ở admin → khách refresh thấy ngay.

## 2. ACTIVE WORKFLOW ROUTING
- **Target Workflow:** `.devin/workflows/newfeaturebuild.md`
- **Execution Mode:** IMPLEMENT

## 3. RELEVANT FILES (CONTEXT BOUNDARY)
- **Files được phép đọc/sửa:**
  - `docs/AI/project_state.md` (Bắt buộc đọc đầu phiên)
  - `5_WebApps/KhachLink/Models/ShopDto.cs` (SỬA — thêm Theme property)
  - `5_WebApps/KhachLink/Services/Http/ShopConfigHttpService.cs` (SỬA — set ActiveTheme trong BuildShopConfigFromShop)
  - `5_WebApps/KhachLink/Pages/Store.razor` (SỬA — thay hardcoded gradient bằng CSS variables per theme)
- **Boundary Rules (Nghiêm cấm):**
  - KHÔNG sửa `KhachLinkLayout.razor` (đã có `GetThemeClass()` — chỉ cần `ActiveTheme` đúng)
  - KHÔNG sửa `MainLayout.razor` (layout structure không đổi)
  - KHÔNG sửa `DynamicThemeProvider.razor` (legacy component, không dùng)
  - KHÔNG thêm file CSS riêng — giữ CSS inline trong `.razor` `<style>` block (pattern hiện có)
  - KHÔNG wrap Store.razor trong `<KhachLinkLayout>` — giữ layout showcase riêng

## 4. TECHNICAL & REGULATORY CONSTRAINTS (HARDENING GATES)
- [ ] **ShopDto deserialize:** `Theme` property phải deserialize đúng từ `TenantStoreDto.Theme` (System.Text.Json, enum as int)
- [ ] **BuildShopConfigFromShop:** Set `ActiveTheme = shop.Theme` — KHÔNG giữ default Classic
- [ ] **Store.razor theme class:** Apply `theme-@GetThemeClass()` lên wrapper div (KHÔNG wrap trong KhachLinkLayout)
- [ ] **CSS variables:** Thay hardcoded gradients bằng `var(--store-hero-gradient, fallback)` — define per theme class
- [ ] **5 theme gradients:** Mỗi theme có gradient riêng cho `.store-hero`, `.store-body`, `.btn-cta` (xem master plan Section 8)
- [ ] **Backward compatible:** Tenant cũ (Classic) → gradient nâu-chocolate (thay cam hiện tại — chấp nhận thay đổi visual)
- [ ] **Gate 4 (UI Layout → E2E):** UI layout change → cần E2E test

## 5. SUCCESS CRITERIA (ĐO LƯỜNG ĐƯỢC)
- [ ] **SC1:** `ShopDto` có `public ThemeType Theme { get; set; } = ThemeType.Classic;`
- [ ] **SC2:** `BuildShopConfigFromShop` set `ActiveTheme = shop.Theme` (thay vì giữ default)
- [ ] **SC3:** `Store.razor` apply `theme-@themeClass` lên wrapper div (`themeClass` từ `_shopConfig?.ActiveTheme`)
- [ ] **SC4:** `.store-hero` dùng `var(--store-hero-gradient, ...)` thay vì hardcoded `#ff9966 → #ff5e62`
- [ ] **SC5:** `.store-body` dùng `var(--store-body-gradient, ...)` thay vì hardcoded `#f5f7fa → #c3cfe2`
- [ ] **SC6:** `.btn-cta` dùng `var(--store-cta-gradient, ...)` thay vì hardcoded `#ff9966 → #ff5e62`
- [ ] **SC7:** 5 CSS class blocks define gradient variables:
  - `.theme-classic { --store-hero-gradient: ...; --store-body-gradient: ...; --store-cta-gradient: ...; }`
  - `.theme-modern { ... }`
  - `.theme-teen { ... }`
  - `.theme-lady { ... }`
  - `.theme-premium { ... }`
- [ ] **SC8:** `dotnet build VanAn.sln` — 0 errors
- [ ] **SC9:** KhachLink Home + Store profile cùng tenant → cùng theme class
- [ ] **SC10:** E2E: Playwright verify KhachLink renders `theme-teen` class khi tenant theme = Teen

**Implementation Date:** 2026-07-22
**Branch:** `main`

## 6. ACTIVE SKILLS (MAX 3)
- `ui-platform-compliance-review` — Verify CSS variables pattern, không bypass UI Platform
- `accounting-ui-implementation` — Blazor CSS isolation reference
- `playwright_guard` — E2E test governance (Gate 4)

## 7. AI HEALTH CHECK MATRIX (INITIAL)
- **Evidence Count:** 8
- **Verified Facts:**
  - Fact 1: `ShopDto.cs` (line 11-36) — 12 properties, chưa có Theme
  - Fact 2: `ShopConfigHttpService.BuildShopConfigFromShop` (line 101-115) — `DefaultShopConfig with { ... }`, không set ActiveTheme
  - Fact 3: `KhachLinkLayout.razor` line 176-183: `GetThemeClass()` switch trên `_shopConfig?.ActiveTheme` → trả "teen", "lady", "premium", "modern", "classic"
  - Fact 4: `KhachLinkLayout.razor` line 23: `<div class="khachlink-layout theme-@GetThemeClass() tenant-@_currentTenantId">` — apply theme class
  - Fact 5: `Store.razor` line 278: `.store-hero { background: linear-gradient(135deg, #ff9966 0%, #ff5e62 100%); }` — hardcoded cam
  - Fact 6: `Store.razor` line 369: `.store-body { background: linear-gradient(135deg, #f5f7fa 0%, #c3cfe2 100%); }` — hardcoded xám xanh
  - Fact 7: `Store.razor` line 388: `.btn-cta { background: linear-gradient(135deg, #ff9966, #ff5e62); }` — hardcoded cam
  - Fact 8: `Store.razor` line 510: `private ShopConfig? _shopConfig;` — đã có ShopConfig, đọc theme được
- **Assumptions:**
  - CSS variables (`var(--name, fallback)`) hoạt động trong Blazor `<style>` block (standard CSS, không có lý do fail)
  - `Store.razor` có thể apply theme class lên wrapper div mà không cần KhachLinkLayout
- **Open Questions:**
  - Q1: Store.razor cần đọc `_shopConfig.ActiveTheme` ở đâu trong lifecycle? → Đã có `_shopConfig` sau `OnInitializedAsync` (line 539)
  - Q2: CSS class áp dụng lên element nào? → Wrapper div bao quanh toàn bộ content (thay vì `<section class="store-hero">` riêng lẻ)
- **Recommended Action:** PROCEED

## 8. REVERSE IMPACT ANALYSIS
| File thay đổi | Reverse impact | Mitigation |
|---|---|---|
| `ShopDto.cs` | Thêm 1 property — deserialize thêm 1 field | Non-breaking — extra field ignored if API không gửi |
| `ShopConfigHttpService.cs` | `ActiveTheme` thay đổi từ Classic → theme thực | Tenant cũ (Classic) → không thay đổi visual |
| `Store.razor` — CSS | Hardcoded gradient → CSS variables | Fallback value trong `var()` — nếu theme class không apply, vẫn có gradient |
| `Store.razor` — HTML | Thêm theme class lên wrapper div | Non-breaking — class thêm vào, không xoá class hiện có |
| `Store.razor` — visual | Classic tenant: cam → nâu-chocolate | Acceptable — consistent với KhachLink pages (cũng nâu) |

## 9. TDD & E2E TESTING STRATEGY
- **E2E (Gate 4 — MANDATORY):**
  - Playwright spec: `khachlink-theme-render.spec.ts`
  - Test 1: Set tenant theme = Teen → KhachLink Home → verify `.theme-teen` class present
  - Test 2: Set tenant theme = Teen → /store/{slug} → verify `.theme-teen` class present + `.store-hero` gradient contains hồng-tím
  - Test 3: Set tenant theme = Premium → verify `.theme-premium` + dark background
- **Test boundary:**
  - Unit tests: KHÔNG (CSS rendering, không testable via unit)
  - Integration tests: KHÔNG
  - E2E tests: Playwright (MANDATORY — Gate 4)

## 10. JIT PLANNING + PURE EXECUTION (MICRO-PHASES)

| Session | JIT Planning | Pure Execution |
|---|---|---|
| **S1** | Confirm ShopDto + ShopConfigHttpService change | Sửa `ShopDto.cs` + `ShopConfigHttpService.cs` |
| **S2** | Confirm CSS variable strategy + wrapper div placement | Sửa `Store.razor` — thêm theme class + CSS variables + 5 theme blocks |
| **S3** | Confirm E2E test scope | Viết Playwright spec `khachlink-theme-render.spec.ts` |
| **S4** | Verify build + E2E | `dotnet build` + Playwright run |

### Rules
- CSS variables define ở wrapper level, override per theme class
- Fallback value trong `var()` để tránh blank nếu theme class không apply
- E2E test chạy sau khi build pass

## 11. COMPLETION SUMMARY

**Phase 4 COMPLETE** — commit `<HASH>` on `main`.

### Files modified
| File | Change |
|------|--------|
| _TBD_ | _TBD_ |

### Files created
| File | Purpose |
|------|---------|
| _TBD_ | _TBD_ |

### Verification
| # | Test | Status | Evidence |
|---|------|--------|----------|
| RV1 | _TBD_ | _TBD_ | _TBD_ |

## 12. ESTIMATED EFFORT
- 4 sessions (S1-S4)
- **BLOCKER:** Phase 2 phải complete trước (TenantStoreDto cần có Theme property)
