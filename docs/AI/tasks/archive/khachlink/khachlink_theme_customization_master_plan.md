# Master Plan — KhachLink Theme Customization (SysAdmin → Tenant → KhachLink UI)

> **Created:** 2026-07-22
> **Status:** NOT STARTED
> **Priority:** Medium (P2) — UX/branding enhancement
> **Branch:** `main`
> **ADR impact:** None (Option C unchanged — KhachLink vẫn HTTP-only qua Gateway)

## Phase Progress Summary

| Phase | Status | Date | Commit | Verified |
|---|---|---|---|---|
| 1 — Domain + EF + Migration | NOT STARTED | — | — | — |
| 2 — Service + Gateway API | NOT STARTED | — | — | — |
| 3 — ShopERP Admin UI | NOT STARTED | — | — | — |
| 4 — KhachLink render theme | NOT STARTED | — | — | — |

---

## 1. Problem Statement

KhachLink hiện có 5 theme CSS (Classic, Modern, Teen, Lady, Premium) được define trong `KhachLinkLayout.razor`:

```css
.theme-teen     { background: linear-gradient(135deg, #ffe0f0, #e0e7ff); }
.theme-lady     { background: linear-gradient(135deg, #fff8f9, #fce4ec); }
.theme-premium  { background: #0a0a0a; color: #f0e68c; }
.theme-modern   { background: #fafafa; }
.theme-classic  { background: #fdf6ec; }
```

`ShopConfig.ActiveTheme` (Domain, `1_Shared/Domain.cs` line 1308) đã tồn tại nhưng **không được persist** — `TenantSettings` không có field Theme. `ShopConfigHttpService.BuildShopConfigFromShop` luôn trả về `DefaultShopConfig` (Classic) — comment nói "branding fields not stored on Tenant entity".

### Hệ quả

1. **Tất cả tenant đều có theme Classic** — không phân biệt cửa hàng cà phê, spa, thời trang, cao cấp.
2. **Sysadmin không có UI** để đặt theme cho tenant.
3. **Store profile page** (`/store/{slug}`) dùng hardcoded gradient cam (`#ff9966 → #ff5e62`) — không đọc theme, không đổi theo tenant.
4. **KhachLink pages** (Home, Cart, Checkout) đọc `ShopConfig.ActiveTheme` nhưng luôn nhận Classic → CSS class luôn là `theme-classic`.

### Evidence (verified 2026-07-22)

- `1_Shared/Domain.cs` line 1307-1308: `Theme` + `ActiveTheme` properties trên `ShopConfig` record, default `ThemeType.Classic`
- `1_Shared/Domain/Aggregates/TenantAggregate/TenantSettings.cs`: KHÔNG có `Theme` property
- `3_CoreHub/Infrastructure/Configurations/TenantConfiguration.cs` line 52-70: `OwnsOne(Settings)` — KHÔNG map `Theme`
- `5_WebApps/KhachLink/Services/Http/ShopConfigHttpService.cs` line 101-115: `BuildShopConfigFromShop` — KHÔNG set `ActiveTheme`, comment line 98-99 nói "branding fields not stored on Tenant entity"
- `5_WebApps/KhachLink/Pages/Store.razor` line 278: `.store-hero { background: linear-gradient(135deg, #ff9966 0%, #ff5e62 100%); }` — hardcoded, không đọc theme
- `5_WebApps/ShopERP/Components/Pages/Admin/TenantManagement.razor` line 376-492: Edit modal — KHÔNG có theme selector

---

## 2. Goal

Cho phép **SysAdmin** chọn 1 trong 5 phong cách giao diện (theme) cho mỗi tenant. Theme được persist vào PostgreSQL (Gateway DB), truyền qua API đến KhachLink, áp dụng cho **cả 2**:

1. **KhachLink pages** (Home, Cart, Checkout, Profile...) — qua `KhachLinkLayout.razor` → CSS class `theme-teen`, `theme-lady`...
2. **Store profile page** (`/store/{slug}`) — qua `Store.razor` → CSS variables per theme

### Success Definition

- SysAdmin mở Edit Tenant → thấy dropdown 5 theme với mô tả tiếng Việt
- Chọn theme → Save → persist vào DB → KhachLink render đúng theme
- Đổi theme → khách hàng refresh trang thấy ngay
- Store profile page (`/store/{slug}`) và KhachLink pages cùng tenant → cùng vibe

### Non-goals

- NOT cho tenant owner tự đổi theme (chỉ SysAdmin)
- NOT thêm theme mới (giữ 5 theme hiện có)
- NOT thêm color picker / custom CSS (chỉ 5 preset)
- NOT thay đổi layout structure (chỉ đổi màu/gradient/vibe)
- NOT động đến `DynamicThemeProvider.razor` (component legacy, không dùng trong flow chính)

---

## 3. Architecture Decision: Theme lưu ở TenantSettings (Domain)

### Quyết định

Theme được lưu trong `TenantSettings` (Domain, owned entity của `Tenant` aggregate) — flatten vào bảng `Tenants` với cột `Settings_Theme` (int).

### Lý do

1. `ShopConfig` (Domain) **đã có** `ActiveTheme` property — chỉ chưa được persist. Việc thêm vào `TenantSettings` là consistent với model hiện có.
2. Theme là **tenant-level branding decision** do SysAdmin đặt, ảnh hưởng tất cả khách hàng của tenant đó — không phải user preference cá nhân.
3. `TenantSettings` đã lưu các tenant-level config tương tự: `Slug`, `SocialLinksFb`, `SocialLinksTiktok`, `BrandStory`, `Latitude`, `Longitude` — Theme cùng pattern.
4. Rule governance nói "UI settings MUST be stored in Presentation Layer only" — nhưng rule đó nói về **user-level preferences** (display mode, dark mode per user). Theme ở đây là **tenant-level config** do admin đặt, giống `BrandStory` (cũng là branding, cũng lưu ở `TenantSettings`).

### Data flow (Option C unchanged)

```
SysAdmin (ShopERP /admin/tenants)
  → TenantApiClient.UpdateProfileAsync(theme: Teen)
  → HTTP PUT /api/v1/tenants/{id}/profile (Gateway, SystemAdmin JWT)
  → TenantManagementService.UpdateProfileAsync
  → tenant.UpdateProfile(name, settings.WithTheme(Teen))
  → EF Core save → PostgreSQL Tenants.Settings_Theme = 2

KhachLink render:
  → ShopConfigHttpService.GetShopConfigByTenantIdAsync
  → HTTP GET /api/tenants/{id}/store-info (Gateway, anonymous)
  → TenantStoreController.GetStoreInfo → TenantStoreDto.Theme = Teen
  → BuildShopConfigFromShop → ShopConfig.ActiveTheme = Teen
  → KhachLinkLayout.GetThemeClass() → "theme-teen" → CSS gradient hồng-tím
  → Store.razor → CSS variables per theme → gradient hồng-tím
```

---

## 4. Phase Breakdown

### Phase 1 — Domain + EF + Migration (Backend foundation)

**Files:**
- `1_Shared/Domain/Aggregates/TenantAggregate/TenantSettings.cs` — thêm `Theme` property + `WithTheme()` method
- `3_CoreHub/Infrastructure/Configurations/TenantConfiguration.cs` — map `Settings_Theme` column (int)
- `3_CoreHub/Infrastructure/Migrations/` — `AddTenantTheme` migration (auto-generated)

**Success Criteria:**
- `TenantSettings` có `Theme` property (ThemeType, default Classic)
- `WithTheme()` method tuân thủ immutable pattern
- EF config map `Settings_Theme` (int, default 0)
- Migration tạo cột `Settings_Theme` trong PostgreSQL, default 0 (Classic)
- `dotnet build VanAn.sln` 0 errors

### Phase 2 — Service + Gateway API (Backend API)

**Files:**
- `3_CoreHub/Services/ITenantManagementService.cs` — thêm `Theme` vào `UpdateTenantProfileRequest`
- `3_CoreHub/Services/TenantManagementService.cs` — apply theme trong `UpdateProfileAsync`
- `2_Gateway/Controllers/TenantsController.cs` — thêm `Theme` vào request/response DTO
- `2_Gateway/Controllers/TenantStoreController.cs` — thêm `Theme` vào `TenantStoreDto`
- `5_WebApps/ShopERP/Services/TenantApiClient.cs` — thêm `Theme` vào `TenantApiDto` + request

**Success Criteria:**
- `UpdateTenantProfileRequest` có `ThemeType? Theme` (nullable — giữ existing nếu null)
- `TenantStoreDto` có `ThemeType Theme` (response cho KhachLink)
- `TenantApiDto` có `ThemeType Theme` (response cho ShopERP admin)
- `dotnet build VanAn.sln` 0 errors
- Unit test: `TenantManagementServiceTests` — update profile với theme mới → DB có theme đúng

### Phase 3 — ShopERP Admin UI (SysAdmin interface)

**Files:**
- `5_WebApps/ShopERP/Components/Pages/Admin/TenantManagement.razor` — theme selector dropdown trong Edit modal

**Success Criteria:**
- Edit modal có dropdown 5 theme với mô tả tiếng Việt
- Dropdown bind 2 chiều — load theme hiện tại, save theme mới
- `HandleEditSubmit` gửi `Theme` trong `UpdateTenantProfileApiRequest`
- (Optional) Tenant list table hiển thị cột "Phong cách"
- `dotnet build VanAn.sln` 0 errors

### Phase 4 — KhachLink render theme (Customer-facing UI)

**Files:**
- `5_WebApps/KhachLink/Models/ShopDto.cs` — thêm `Theme` property
- `5_WebApps/KhachLink/Services/Http/ShopConfigHttpService.cs` — set `ActiveTheme` trong `BuildShopConfigFromShop`
- `5_WebApps/KhachLink/Pages/Store.razor` — thay hardcoded gradient bằng CSS variables per theme

**Success Criteria:**
- `ShopDto` có `Theme` property (deserialize từ API response)
- `BuildShopConfigFromShop` set `ActiveTheme = shop.Theme`
- `Store.razor` apply theme class lên wrapper div, CSS variables thay hardcoded gradients
- 5 theme đều có gradient riêng cho `.store-hero`, `.store-body`, `.btn-cta`
- `dotnet build VanAn.sln` 0 errors
- E2E: KhachLink Home + Store profile cùng tenant → cùng theme class

---

## 5. Dependency Chain

```
Phase 1 (Domain + EF + Migration)
  ↓ — TenantSettings.Theme phải tồn tại trước khi service dùng
Phase 2 (Service + Gateway API)
  ↓ — API phải trả Theme trước khi UI consume
Phase 3 (ShopERP Admin UI)     Phase 4 (KhachLink render)
  ↓ — cả 2 phụ thuộc Phase 2, độc lập với nhau
  (có thể chạy song song)
```

**Thứ tự implement:** 1 → 2 → (3 ∥ 4)

---

## 6. Risk Analysis

| Risk | Likelihood | Impact | Mitigation |
|------|-----------|--------|------------|
| Migration thêm cột lock table lâu | Low | Medium | PG 11+ hỗ trợ fast default (NOT NULL + DEFAULT) — không lock |
| Tenant cũ mất theme sau migration | Low | Low | `DEFAULT 0` = Classic — tenant cũ giữ Classic (không break) |
| Store.razor mất thiết kế showcase khi thêm theme | Medium | Medium | KHÔNG wrap trong KhachLinkLayout — chỉ apply theme class lên wrapper div, giữ layout showcase |
| `DynamicThemeProvider.razor` conflict | Low | Low | Không động đến — component legacy không dùng trong flow chính |
| KhachLink WASM không deserialize enum đúng | Low | Medium | `ThemeType` là enum — System.Text.Json serialize as int by default, KhachLink đã `@using VanAn.Shared.Domain` |
| Service worker cache stale theme | Medium | Low | `store-info` endpoint đã trong SWR pattern (Phase 3 PWA) — 24h expiry. User refresh = network-first |

---

## 7. Test Strategy

| Level | Scope | Tool |
|-------|-------|------|
| Unit | `TenantManagementService.UpdateProfileAsync` với theme mới | xUnit (`TenantManagementServiceTests`) |
| Integration | Gateway API `PUT /tenants/{id}/profile` với theme, `GET /tenants/{id}/store-info` trả theme | xUnit (`Integration.Tests`) |
| E2E | ShopERP admin chọn theme → KhachLink render đúng theme class | Playwright (optional, Gate 4) |
| Runtime | Live site: đổi theme admin → refresh KhachLink → thấy theme mới | Manual / Playwright RT |

---

## 8. Theme CSS Specifications

### KhachLink pages (via KhachLinkLayout.razor — đã có)

| Theme | CSS class | Background | Vibe |
|-------|-----------|------------|------|
| Classic | `.theme-classic` | `#fdf6ec` (kem nâu) | Ấm cúng, truyền thống |
| Modern | `.theme-modern` | `#fafafa` (trắng) | Sạch, tối giản |
| Teen | `.theme-teen` | `linear-gradient(135deg, #ffe0f0, #e0e7ff)` (hồng-tím) | Trẻ trung, năng động |
| Lady | `.theme-lady` | `linear-gradient(135deg, #fff8f9, #fce4ec)` (hồng pastel) | Nữ tính, nhẹ nhàng |
| Premium | `.theme-premium` | `#0a0a0a` + text `#f0e68c` (đen + vàng gold) | Cao cấp, sang trọng |

### Store profile page (via Store.razor — cần thêm)

| Theme | `.store-hero` gradient | `.store-body` gradient | `.btn-cta` gradient |
|-------|------------------------|------------------------|---------------------|
| Classic | `#8B4513 → #D2691E` (nâu-chocolate) | `#fdf6ec → #f5e6d3` | `#8B4513 → #D2691E` |
| Modern | `#2c3e50 → #3498db` (xanh navy) | `#fafafa → #f0f0f0` | `#2c3e50 → #3498db` |
| Teen | `#ff6b9d → #c44dff` (hồng-tím neon) | `#ffe0f0 → #e0e7ff` | `#ff6b9d → #c44dff` |
| Lady | `#f8a5c2 → #fbc5d8` (hồng pastel) | `#fff8f9 → #fce4ec` | `#f8a5c2 → #fbc5d8` |
| Premium | `#0a0a0a → #1a1a2e` (đen) + text gold | `#1a1a1a → #0a0a0a` | `#f0e68c → #daa520` (gold) |

---

## 9. Rollout Plan

1. **Implement Phase 1-4** trên branch `main`
2. **Build + unit tests** pass local
3. **Push** → CI pipeline (build + unit + integration + architecture)
4. **CD deploy** → Gateway + ShopERP + KhachLink lên VPS
5. **Migration auto-apply** → `dotnet ef database update` trong CD
6. **Runtime verification** → SysAdmin đổi theme 1 tenant → refresh KhachLink → thấy theme mới
7. **Update project_state.md** → mark complete
