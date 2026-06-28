# KHACHLINK — Retention & Loyalty Plan
# Wave 17: Biến KhachLink thành ứng dụng giữ chân khách hàng

**Created:** 2026-06-26
**Scope:** `5_WebApps/KhachLink/` — customer-facing retention features
**Status:** DEFERRED — sẽ thực hiện sau Wave 16
**Depends on:** Wave 15 + Wave 16 complete (KhachLink production-ready order flow)
**Source:** Tách từ `KHACHLINK_PRODUCTION_PLAN.md` để giữ plan chính tập trung

---
## 0. EXECUTION RULES

### Session protocol
1. **Đọc `docs/AI/project_state.md` + task card của wave đang active TRƯỚC KHI viết bất kỳ dòng code nào.**
2. **Chạy `dotnet build VanAn.sln` trước khi bắt đầu và sau khi kết thúc session — 0 errors bắt buộc.**
3. **Chỉ sửa files nằm trong "Files được phép" của task card đang active — không drift sang module khác.**
4. **Sau mỗi micro-phase: commit intermediate, ghi rõ `[WaveX-SY]` trong commit message.**
5. **Nếu phát sinh compile error > 5: STOP, ghi vào investigation_log.md, hỏi user trước khi tiếp tục.**

## Tại sao tách file riêng

- Retention là scope creep so với mục tiêu "production-ready order flow" (Waves 15–16)
- Các task T1–T9 của Wave 17 phụ thuộc lẫn nhau và phức tạp; cần phase riêng
- Wave 15–16 phải COMPLETE trước — không nên kết hợp cleanup + new features trong cùng 1 PR

---

## Wave 17 — Mục tiêu

Sau Wave 16, KhachLink có đầy đủ order flow nhưng user là **anonymous hoàn toàn**. Wave 17 xây dựng retention loop:

```
Lần đầu vào app → đặt hàng ngay (zero-friction, DeviceId)
    ↓ sau đơn hàng thứ nhất
IdentityUpgradeModal (phone OTP login)
    ↓ sau login
Điểm thưởng tích lũy + Lịch sử đơn hàng + Push notification
    ↓ dynamic layout
KhachLink đẹp, brand của từng shop (logo, màu sắc, theme)
```

---

## Dependency Tree

```
W17-T1 (Customer Identity — Phone OTP)
    ├── W17-T2 (Loyalty Dashboard)        — cần CustomerToken
    ├── W17-T3 (Order History)            — cần CustomerId
    ├── W17-T4 (PWA Bug Fixes + Push)     — cần CustomerToken
    └── W17-T6 (NavMenu)                  — cần login state pattern
            └── W17-T9 (KhachLink Layout) — cần NavMenu mới

W17-T5 (Store Finder)                    — độc lập
W17-T7 (Verify + E2E)                    — sau tất cả T1–T6, T9
W17-T8 (Update project_state.md)         — sau T7 PASS
```

---

## Tasks

| # | Task ID | Task | Depends on | Task card | Status |
|---|---------|------|-----------|-----------|--------|
| 1 | W17-T1 | Customer Identity — Phone OTP login, zero-friction → upgrade, CustomerToken | Wave 16 | [W17-T1-card.md](W17-T1-card.md) | 📋 DEFERRED |
| 2 | W17-T2 | Loyalty Dashboard — `/my-loyalty`, tier badge, point balance, progress bar, history | W17-T1 | [W17-T2-card.md](W17-T2-card.md) | 📋 DEFERRED |
| 3 | W17-T3 | Order History — `/my-orders`, filter tabs, pagination, link OrderTracking | W17-T1 | [W17-T3-card.md](W17-T3-card.md) | 📋 DEFERRED |
| 4 | W17-T4 | PWA Bug Fixes + Push Subscription endpoint | W17-T1 | [W17-T4-card.md](W17-T4-card.md) | 📋 DEFERRED |
| 5 | W17-T5 | Store Finder — `Shop.Latitude/Longitude`, `/stores` page, Google Maps key fix | — | [W17-T5-card.md](W17-T5-card.md) | 📋 DEFERRED |
| 6 | W17-T6 | NavMenu update — 6 retention routes, mobile bottom tab bar, scaffold items xóa | W17-T1 | [W17-T6-card.md](W17-T6-card.md) | 📋 DEFERRED |
| 7 | W17-T7 | Verify + E2E — build 0 errors, route contract, anti-pattern checks, gateway smoke test | T1–T6, T9 | [W17-T7-card.md](W17-T7-card.md) | 📋 DEFERRED |
| 8 | W17-T8 | Update `project_state.md` — Wave 17 complete, tech debt Wave 18 | W17-T7 | [W17-T8-card.md](W17-T8-card.md) | 📋 DEFERRED |
| 9 | W17-T9 | KhachLink End-User Layout — hero header, dynamic theme từ ShopConfig, 5 themes | W17-T1, W17-T6 | [W17-T9-card.md](W17-T9-card.md) | 📋 DEFERRED |

---

## T1 — Customer Identity (Phone OTP Login)

**Priority:** 🔴 CRITICAL — prerequisite của T2, T3, T4, T6
**Branch:** `feature/wave17-khachlink-retention`
**Conflict risk:** HIGH — OTP service mới, 2 Gateway endpoints mới, 2 Razor pages mới

### Thiếu (cần tạo mới)
| Thành phần | File |
|-----------|------|
| OTP generation + IMemoryCache TTL 5 phút | `ShopERP/Services/OtpService.cs` |
| CustomerToken (IDataProtector, 30 ngày) | `ShopERP/Services/CustomerTokenService.cs` |
| ShopERP CustomerIdentityController | `ShopERP/Controllers/CustomerIdentityController.cs` |
| Gateway CustomersController (forward) | `2_Gateway/Controllers/CustomersController.cs` |
| `Pages/Login.razor` (`@page "/login"`) | KhachLink login page |
| `Pages/Profile.razor` (`@page "/profile"`) | KhachLink profile page |

### Luồng thiết kế
```
Lần đầu vào: DeviceId = localStorage("device_id") ?? crypto.randomUUID()
    → đặt hàng ngay được, không cần login

Sau đơn đầu tiên: IdentityUpgradeModal hiện
    → User nhập số điện thoại
    → POST /api/customers/otp/send → SMS OTP (hoặc X-Dev-OTP header nếu Dev:ExposeOtp=true)
    → User nhập OTP
    → POST /api/customers/otp/verify → { customerId, customerToken, tier, pointBalance }
    → localStorage("customer_token") = customerToken
    → Từ đây: IHttpClientFactory("gateway") tự gắn X-Customer-Token header
```

### Hard rules
- KHÔNG dùng ASP.NET Identity
- KHÔNG sửa `1_Shared/Domain.cs` (Customer entity đã đủ fields)
- OTP lưu IMemoryCache (không cần EF migration)
- Token = IDataProtector.Protect(customerId + ":" + expiry)

---

## T2 — Loyalty Dashboard

**Priority:** 🔴 HIGH
**Conflict risk:** MEDIUM — Gateway endpoint mới, 1 Razor page mới

### Backend đã có sẵn
- `LoyaltyRewards` entity: `PointBalance`, `History` (JSON)
- `LoyaltyRewardsService.GetCustomerRewardsAsync()`, `AddPointsAsync()`
- `OrderWorkflowService.ProcessLoyaltyPointsAsync()`: tự động cộng điểm sau mỗi đơn

### Tier System
| Tier | PointBalance | Badge |
|------|-------------|-------|
| Bronze | 0–999 | `bg-warning` |
| Silver | 1,000–4,999 | `bg-secondary` |
| Gold | 5,000–19,999 | `bg-warning text-dark` |
| Platinum | 20,000+ | `bg-info` |

**Quyết định:** Tính tier on-the-fly trong response DTO (không cần sửa Domain)

### Cần tạo mới
- `ShopERP/Controllers/LoyaltyController.cs` — `GET /api/customers/{id}/loyalty`
- `2_Gateway/Controllers/LoyaltyController.cs` — forward + X-Customer-Token
- `KhachLink/Pages/LoyaltyCard.razor` — `@page "/my-loyalty"`

---

## T3 — Lịch sử đơn hàng

**Priority:** 🟡 HIGH
**Conflict risk:** LOW — thêm query param vào OrdersController đã có

### Cần tạo/sửa
- Sửa `2_Gateway/Controllers/OrdersController.cs` — thêm `?customerId=` param (forward về ShopERP)
- Tạo `ShopERP/Controllers/CustomerOrdersController.cs` — `GET /api/orders?customerId=&tenantId=`
- Tạo `KhachLink/Pages/OrderHistory.razor` — `@page "/my-orders"`

---

## T4 — PWA Bug Fixes + Push Subscription

**Priority:** 🟡 HIGH
**Conflict risk:** LOW-MEDIUM

### 4 bugs cần fix trong `PWAInstallPrompt.razor`
| Bug | Vấn đề | Fix |
|-----|---------|-----|
| Bug 1 | `async void Dispose()` → ObjectDisposedException | Implement `IAsyncDisposable.DisposeAsync()` |
| Bug 2 | `_dismissed` không persist | `localStorage.setItem("pwa_dismissed", "true")` |
| Bug 3 | `display: none` bỏ qua CSS transition | Dùng class `.hidden` đã viết sẵn |
| Bug 4 | `Task.Delay(3000)` không có CancellationToken | Inject `CancellationTokenSource _cts` |

### Cần tạo mới
- `2_Gateway/Controllers/NotificationsController.cs` — `POST /api/notifications/push/subscribe`
- `ShopERP/Controllers/NotificationsController.cs` — validate token + log subscription
- Wire `PWAService.SubscribeAndRegisterAsync()` → gọi endpoint

> **Domain note:** `Customer.PushSubscriptionJson` chờ Wave 18 approve — T4 log subscription tạm, không lưu DB.

---

## T5 — Store Finder

**Priority:** 🟢 MEDIUM — độc lập
**Conflict risk:** MEDIUM — Domain modification approved

### Domain change (APPROVED)
Thêm `Latitude` và `Longitude` vào `Shop` entity trong `1_Shared/Domain.cs`:
```csharp
public double? Latitude  { get; protected set; }
public double? Longitude { get; protected set; }
public void SetCoordinates(double lat, double lng) { ... }
```

**Lý do:** `ShopConfig` record không queryable từ DB. Store Finder cần query địa lý.

### Cần tạo/sửa
- Sửa `1_Shared/Domain.cs` — `Shop.Latitude/Longitude` + `SetCoordinates()`
- Sửa `Components/GoogleMaps.razor` — `Configuration["GoogleMaps:ApiKey"]` thay `AIzaSyDummyKey`
- Tạo `ShopERP/Controllers/ShopsController.cs` — `GET /api/shops?tenantId=&lat=&lng=&radius=`
- Tạo `2_Gateway/Controllers/ShopsController.cs` — forward
- Tạo `KhachLink/Pages/StoreFinder.razor` — `@page "/stores"`
- Tạo EF migration: `AddShopCoordinates`

---

## T6 — NavMenu Update

**Priority:** 🟢 MEDIUM
**Conflict risk:** VERY LOW

### Target NavMenu
```
🏠 Trang chủ   /
🛒 Giỏ hàng   /cart
📋 Đơn hàng   /my-orders
💎 Điểm thưởng /my-loyalty
📍 Cửa hàng   /stores
👤 Tài khoản  /profile (hoặc /login nếu anonymous)
── (staff only) ──
📊 Dashboard  /dashboard
```

- Xóa: `Counter`, `Weather`, `VanAnDashboard` scaffold links
- Thêm: 4 retention links + conditional login/profile
- Mobile: bottom tab bar (`position: fixed; bottom: 0`)

---

## T9 — KhachLink End-User Layout

**Priority:** 🟡 HIGH — ảnh hưởng toàn bộ UX end-user
**Conflict risk:** MEDIUM — sửa `KhachLinkLayout.razor` ảnh hưởng tất cả pages

### Thiết kế target
```
Hero Header (dynamic bg từ ShopConfig.PrimaryColor)
  [Logo] Tên shop | [🛒] [👤]
─────────────────────────────
@ChildContent
─────────────────────────────
Bottom Nav (mobile — W17-T6)
─────────────────────────────
Footer: shop name · phone · social links
```

### 5 themes từ ThemeType enum
| Theme | Vibe |
|-------|------|
| Classic | Cà phê, trà truyền thống — nâu ấm |
| Modern | Minimalist, specialty coffee — tối giản |
| Teen | Trà sữa, giới trẻ — pastel gradient |
| Lady | Milk tea, dessert — rose/cream elegant |
| Premium | Fine dining — dark + gold accent |

### Sửa `KhachLinkLayout.razor`
- Inject `IThemeProvider`, `ITenantService`, `IShopConfigService`
- `<HeadContent>` inject CSS variables: `--shop-primary`, `--shop-secondary`
- Hero header: logo thật (`ShopConfig.LogoUrl`), tên shop thật, cart count, login state
- Footer: phone, social links từ ShopConfig
- CSS theme class: `theme-classic | modern | teen | lady | premium`

---

## Entry criteria (Wave 17)

- [ ] Wave 15 merged — 6 files dead/demo đã xóa, `Dashboard.razor` tồn tại
- [ ] Wave 16 merged — Campaign, Dashboard TenantId, VoiceCommand đã fix
- [ ] `dotnet build VanAn.sln` → 0 errors
- [ ] Branch `feature/wave17-khachlink-retention` tạo từ `main` mới nhất

---

## Exit criteria (Wave 17) — TẤT CẢ phải PASS trước khi merge

- [ ] `dotnet build VanAn.sln` → 0 errors
- [ ] `guard-check.ps1` → PASS
- [ ] `VanAn.Architecture.Tests` → 7/7 PASS
- [ ] Route `/login` tồn tại (W17-T1)
- [ ] Route `/my-loyalty` tồn tại (W17-T2)
- [ ] Route `/my-orders` tồn tại (W17-T3)
- [ ] Route `/stores` tồn tại (W17-T5)
- [ ] `PWAInstallPrompt.razor` không còn `async void Dispose` (W17-T4)
- [ ] `GoogleMaps.razor` không còn `AIzaSyDummyKey` (W17-T5)
- [ ] NavMenu không còn Counter, Weather, VanAnDashboard links (W17-T6)
- [ ] `KhachLinkLayout.razor` inject `IShopConfigService` (W17-T9)
- [ ] W17-T7 verification scripts: tất cả green

---

## Tech Debt → Wave 18

| ID | Mô tả |
|----|-------|
| W18-TD1 | `Customer.PushSubscriptionJson` field chưa có trong Domain → push notification chưa hoạt động end-to-end |
| W18-TD2 | `LoyaltyRewards.History` là JSON blob string → không queryable. Cần migrate sang `LoyaltyHistoryEntry` entity riêng |
| W18-TD3 | Tier rules hardcode trong `LoyaltyController.CalcTier()` → cần config-driven |
| W18-TD4 | `CustomerOrdersController` query chỉ match `CustomerId` — không fallback `CustomerDeviceId` khi user chưa upgrade |
| W18-TD5 | `GoogleMaps.razor` + `StoreFinder.razor` — 2 component cho 1 chức năng, cần merge |

---

## Maintenance Log

* **2026-06-26:** File created — Wave 17 tách từ `KHACHLINK_PRODUCTION_PLAN.md` (DEFERRED)
  - Tất cả 9 task cards (W17-T1..T9) giữ nguyên nội dung chi tiết trong file card riêng
  - Master plan reference trong các task cards đã được cập nhật sang file này

* **2026-06-28:** Code review `Components/` — phát hiện các dead/broken components cần xử lý trong Wave 17:

  **DEAD CODE (không được mount ở đâu, cần xóa hoặc integrate):**
  - `AppInstallPrompt.razor` — gọi `window.isAppInstalled` và `window.installPWA` không tồn tại; JS thật nằm trong `window.vananPWA.*`. Cần fix JS bridge + integrate vào App.razor hoặc MainLayout. Backlog: **W17-PWA-FIX**
  - `VibeProductGrid.razor` — duplicate hoàn toàn của `VibeShowcase.razor` (cùng logic, cùng theme switch). Cần xóa 1 file. Backlog: **W17-CLEANUP-1**
  - `VibeShowcase.razor` — nhận `List<CartItem>` thay vì `List<ProductDto>` (type sai); không được dùng ở đâu. Backlog: **W17-CLEANUP-1**
  - `GoogleMaps.razor` — dùng `key=AIzaSyDummyKey` hardcode → map không load; chỉ hiện iframe 403. Cần real API key từ config. Backlog: **W17-MAPS**
  - `SocialBridge.razor` — duplicate logic của `SocialHub.razor` (cùng Facebook/TikTok embed). Xóa `SocialBridge.razor`, giữ `SocialHub.razor`. Backlog: **W17-CLEANUP-2**
  - `IdentityUpgradeModal.razor` — UI đúng, nhưng `OnUpgrade` callback trống (Wave 17 Identity chưa implement). Backlog: **W17-T2 (đã có)**

  **BROKEN INJECT (vi phạm kiến trúc):**
  - `DynamicThemeProvider.razor` — `@inject HttpClient Http` (direct inject, vi phạm VA-KHACHLINK-004). Cần đổi sang `IHttpClientFactory("gateway")`. Endpoint `/api/v1/shopconfig/shops/{id}/config` cũng chưa tồn tại ở Gateway. Backlog: **W17-THEME-FIX**

  **PRODUCTION READY (không cần làm gì):**
  - `CartDrawer.razor` — ✅ nhận params từ Home.razor, dùng CartService đúng cách
  - `PWAInstallPrompt.razor` — ✅ dùng `PWAService` đúng cách, có offline indicator
  - `SocialHub.razor` — ✅ conditional render khi ShopConfig có social links
  - `QrPaymentModal.razor` — ✅ đã verify ở Wave 16
  - `VoiceCommand.razor` — ✅ đã verify ở Wave 16
  - `RealTimeDashboard.razor` — ✅ đã verify ở Wave 16
