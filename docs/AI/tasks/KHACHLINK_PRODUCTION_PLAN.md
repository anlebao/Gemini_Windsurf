# KHACHLINK — Real Production Plan
# Biến KhachLink từ Demo → Production-Ready

**Created:** 2026-06-25
**Last Updated:** 2026-06-26
**Scope:** `5_WebApps/KhachLink/` production-readiness (Waves 15–16)
**Waves:** 15 (Page Cleanup) ✅ COMPLETED → 16 (Real Production Flows)
**Status:** Wave 15 completed; Wave 16 ready; Wave 17 moved to `KHACHLINK_RETENTION_PLAN.md`

---

## Plan Alignment Note

**Wave 13 (Replace Hardcoded Data) đã hoàn thành trước khi plan này được viết.** Các thay đổi liên quan đã live trong source code:

- `5_WebApps/ShopERP/Controllers/ProductsController.cs` — public `GET /api/products` ✅
- `5_WebApps/KhachLink/Services/Http/ProductHttpService.cs` — gọi `shoperp/api/products` qua Gateway YARP ✅
- `5_WebApps/KhachLink/Pages/Home.razor` — dùng `ProductHttpService.GetProductsAsync()` thay vì hardcode ✅

Do đó, các task W16-T1 và W16-T2 (Replace hardcoded products) **không còn cần thiết**. Wave 16 được rút gọn chỉ còn Campaign refactor, Dashboard fix, VoiceCommand HttpClient fix.

---

## WAVE 15 — KhachLink Page Cleanup + Routing Modernization

**Branch:** `feature/wave15-khachlink-page-cleanup`
**Estimated sessions:** 3
**Priority:** 🟡 HIGH — Demo/dead pages gây confusion, route conflicts, vi phạm kiến trúc
**Conflict risk:** MEDIUM — Xóa files + chuyển sang Blazor Web App routing
**Depends on:** Wave 14 (API request signing) complete

### Vấn đề cụ thể cần fix

| File | Loại vấn đề | Nguồn xác nhận |
|------|------------|----------------|
| `Pages/Index.razor` | Dead code — route `@page "/"` conflict với `Home.razor`, `SampleProducts` hardcode, `Checkout()` fake `Task.Delay` | `e2e-gap-backlog.md` §Route #9 |
| `Pages/IndexModern.cshtml` (`/modern`) | Demo landing page thuần — stats bịa, không có handler, không có test reference | Không có reference |
| `Pages/Index.cshtml` + `Index.cshtml.cs` | Duplicate landing vi phạm `VA-KHACHLINK-004` — direct DB access, social proof JS fake, `FeaturedProducts` hardcode | `TD-001` |
| `Components/Pages/Home.razor` | Scaffold "Hello world" 5 dòng — `@page "/"` conflict trực tiếp với `Pages/Home.razor` đang live | Rà soát 2026-06-26 |
| `Pages/VoiceNote.razor` | Sai kiến trúc — `POST /api/orders/voice-note` không tồn tại, inject `HttpClient` trực tiếp, `<html><head><body>` trong `.razor`, `alert()` native | `voice-command.spec.ts` |
| `Pages/_Host.cshtml` | Orphan HTML shell — không còn phù hợp với Blazor Web App routing; cần xóa để dứt điểm kiến trúc lai tạp | Quyết định kiến trúc 2026-06-26 |
| `Pages/Dashboard.cshtml` | Razor Page wrapper render `RealTimeDashboard`; cần chuyển sang `.razor` để consistent với Blazor Web App | Rà soát 2026-06-26 |
| `Program.cs` | Dùng `AddServerSideBlazor()` + `MapBlazorHub()` + `MapFallbackToPage("/Index")` — kiến trúc lai tạp với `App.razor` Blazor Web App | Quyết định kiến trúc 2026-06-26 |

### Quyết định kiến trúc

- **Index.razor:** XÓA — dead code, bị shadow bởi `Home.razor` (canonical page được E2E test)
- **IndexModern.cshtml:** XÓA — demo prototype không kết nối hệ thống
- **Index.cshtml + Index.cshtml.cs:** XÓA — duplicate landing, vi phạm `VA-KHACHLINK-004`
- **Components/Pages/Home.razor:** XÓA — scaffold Blazor template, route conflict
- **_Host.cshtml:** XÓA — orphan host, không còn cần thiết trong Blazor Web App
- **Dashboard.cshtml:** CONVERT → `Pages/Dashboard.razor` — page wrapper nhúng `<RealTimeDashboard />`
- **VoiceNote.razor:** REWRITE — fix endpoint, inject `IHttpClientFactory`, xóa `<html>` tags, thay `alert()` bằng UI Platform
- **Program.cs:** CHUYỂN sang Blazor Web App — `AddRazorComponents()` + `MapRazorComponents<App>()`; xóa `MapFallbackToPage("/Index")`, `MapBlazorHub()`, `AddServerSideBlazor()`; fallback routing do Blazor Router xử lý
- **App.razor:** Là Host Page duy nhất — quản lý vòng đời ứng dụng và `blazor.web.js`

### Bối cảnh nghiệp vụ

`Home.razor` là **canonical entry point** được E2E xác nhận:
- `order-flow.spec.ts` navigate về `KHACHLINK_URL/home`, tìm `.feature-card`, click `button:has-text("Đặt ngay")`
- `order-tracking.spec.ts` navigate về `KHACHLINK_URL/home` trước khi checkout
- `functional-requirements.md §1.3`: KDS cần ghi chú voice — `VoiceNote.razor` có nghiệp vụ thật nhưng sai implementation

`VoiceNote.razor` sau khi rewrite phải:
- Dùng `IHttpClientFactory("gateway")` + endpoint `POST /api/v1/voicecommand/text-command`
- Không có `<html><head><body>` tag trong Razor component
- Dùng `VanAnAlert` thay `alert()` native

### Tasks

| # | Task ID | Task | Depends on | Task card | Status |
|---|---------|------|-----------|-----------|--------|
| 1 | W15-T1 | Xóa 6 dead/demo pages + convert `Dashboard.cshtml` → `Dashboard.razor` | — | [W15-T1-card.md](W15-T1-card.md) | ✅ DONE |
| 2 | W15-T2 | Modernize `Program.cs` — Blazor Web App routing (`AddRazorComponents` + `MapRazorComponents<App>`) | W15-T1 | [W15-T2-card.md](W15-T2-card.md) | ✅ DONE |
| 3 | W15-T3 | Rewrite `VoiceNote.razor` — fix endpoint, inject `IHttpClientFactory`, xóa `<html>` tags, thay `alert()` bằng UI Platform | W15-T2 | [W15-T3-card.md](W15-T3-card.md) | ✅ DONE |
| 4 | W15-T4 | Verify build 0 errors + E2E selector contract (`Home.razor` `.feature-card`, `Đặt ngay`) | W15-T3 | [W15-T4-card.md](W15-T4-card.md) | ✅ DONE |
| 5 | W15-T5 | Update `project_state.md` — ghi nhận cleanup, cập nhật Next Actions | W15-T4 | [W15-T5-card.md](W15-T5-card.md) | ✅ DONE |

### Files được phép sửa/xóa (W15)

**XÓA (6 files):**
- `5_WebApps/KhachLink/Pages/Index.razor`
- `5_WebApps/KhachLink/Pages/IndexModern.cshtml`
- `5_WebApps/KhachLink/Pages/Index.cshtml`
- `5_WebApps/KhachLink/Pages/Index.cshtml.cs`
- `5_WebApps/KhachLink/Pages/_Host.cshtml`
- `5_WebApps/KhachLink/Components/Pages/Home.razor`

**CONVERT / TẠO MỚI:**
- `5_WebApps/KhachLink/Pages/Dashboard.cshtml` → xóa, thay bằng `5_WebApps/KhachLink/Pages/Dashboard.razor`

**SỬA:**
- `5_WebApps/KhachLink/Program.cs` — chuyển sang Blazor Web App routing
- `5_WebApps/KhachLink/Pages/VoiceNote.razor` — rewrite theo kiến trúc đúng
- `5_WebApps/KhachLink/Components/App.razor` — nếu cần, đảm bảo là host page duy nhất

**KHÔNG ĐƯỢC CHẠM:**
- `5_WebApps/KhachLink/Pages/Home.razor` — canonical page, không thay đổi
- `5_WebApps/KhachLink/Pages/Cart.razor`, `Checkout.razor`, `OrderTracking.razor` — production flows
- `1_Shared/Domain.cs` — Domain Layer Protection
- `3_CoreHub/` — CoreHub không liên quan đến cleanup này

### Entry criteria (Wave 15)
- [ ] Wave 14 merged + `dotnet build VanAn.sln` → 0 errors
- [ ] Branch `feature/wave15-khachlink-page-cleanup` tạo từ `main` mới nhất
- [ ] Architecture tests: 7/7 PASS
- [ ] Xác nhận `Home.razor` hiện đang chạy đúng tại `/` và `/home` (manual test hoặc E2E smoke)

### Exit criteria (Wave 15) — TẤT CẢ phải PASS trước khi merge
- [ ] `dotnet build VanAn.sln` → 0 errors, 0 warnings mới
- [ ] `guard-check.ps1` → PASS
- [ ] `VanAn.Architecture.Tests`: 7/7 PASS
- [ ] Verify: 6 files dead/demo đã xóa không còn trong repository
- [ ] Verify: `Dashboard.razor` tồn tại tại route `/dashboard` và render `<RealTimeDashboard />`
- [ ] Verify: `Home.razor` serve tại `http://localhost:5002/` — có `.feature-card` và `button:has-text("Đặt ngay")` (E2E selector contract)
- [ ] Verify: `VoiceNote.razor` không có `<html>`, không có `HttpClient` direct inject, endpoint trỏ `IHttpClientFactory("gateway")` → `/api/v1/voicecommand/text-command`
- [ ] Verify: `Program.cs` sử dụng `AddRazorComponents()` + `MapRazorComponents<App>()`; không còn `MapFallbackToPage`, `MapBlazorHub`, `AddServerSideBlazor`
- [ ] Verify: Không có broken `@using` hay `@inject` references đến các files đã xóa trong toàn bộ `KhachLink/`
- [ ] Verify: Không còn `@page "/"` conflict — chỉ còn `Pages/Home.razor` là handler duy nhất cho route `/`

### Risk notes
- **Route conflict resolution:** Khi xóa `Index.razor` + `Components/Pages/Home.razor`, Blazor Router sẽ chỉ còn `Home.razor` làm handler duy nhất cho `/` — verify không có double-route error.
- **Blazor Web App migration:** Đây là thay đổi kiến trúc lớn nhất của Wave 15. Cần test kỹ routing deep links và SignalR hub (`/dashboardHub`) vẫn hoạt động sau migration.
- **VoiceNote rewrite:** Chỉ fix kiến trúc và cấu trúc — KHÔNG thay đổi business logic (speech recognition, transcription flow). Giữ nguyên `[JSInvokable] SetTranscriptionText()`.

---

## 0. TRẠNG THÁI BASELINE (sau Wave 15)

### Luồng nào đã hoạt động đúng kiến trúc
| Luồng | Route | Trạng thái sau W15 |
|-------|-------|-------------------|
| Đặt hàng (Checkout) | `/checkout` | ✅ Gọi `POST api/orders` qua Gateway đúng chuẩn |
| Theo dõi đơn | `/order-tracking/{id}` | ✅ Gọi `GET api/orders/{id}` qua Gateway |
| Giỏ hàng (Cart logic) | `/cart` | ✅ `CartService` + localStorage — cơ chế tốt |
| Voice Note | `/voice-note` | ✅ Sau W15-T3: endpoint đúng, `IHttpClientFactory("gateway")` |
| Dashboard | `/dashboard` | ✅ Sau W15-T1: `Dashboard.razor` render `RealTimeDashboard` |

### Luồng vẫn còn giả/broken — mục tiêu Wave 16
| Luồng | Vấn đề | Task |
|-------|---------|------|
| Chiến dịch Marketing | `Campaign.cshtml` inject CoreHub trực tiếp, order giả, social proof bịa | W16-T1 |
| Dashboard TenantId | `RealTimeDashboard.razor` hardcode `"demo-shop"` | W16-T2 |
| VoiceCommand (staff) | `VoiceCommand.razor` inject `HttpClient` trực tiếp | W16-T3 |

---

## 1. QUYẾT ĐỊNH KIẾN TRÚC DASHBOARD

### Phân tích: VanAnDashboard.razor vs RealTimeDashboard.razor

| | `VanAnDashboard.razor` | `RealTimeDashboard.razor` |
|---|---|---|
| Dòng code | 566 dòng | 696 dòng |
| Route | `@page "/VanAnDashboard"` | Không có `@page` — component nhúng |
| Data source | 3 TODO — KHÔNG có data | SignalR `HubConnection` — có infrastructure |
| ShopId | Không có | Hardcode `"demo-shop"` (cần fix) |
| DI | `@inject ILogger` only | `@inject IJSRuntime`, `@inject NavigationManager` |
| SignalR events | Không có | `ShopMetricsUpdate`, `ShopOrderUpdate`, `InventoryUpdate`, `Notification` |

**Quyết định: XÓA `VanAnDashboard.razor`, GIỮ VÀ FIX `RealTimeDashboard.razor`**

Lý do:
- `RealTimeDashboard.razor` có đầy đủ SignalR infrastructure, UI hoàn chỉnh
- `VanAnDashboard.razor` là 566 dòng TODO không có giá trị
- `RealTimeDashboard.razor` chỉ cần fix 1 điểm: `"demo-shop"` → TenantId thật từ `ITenantService`
- Sau khi fix, `Dashboard.razor` (tạo ở W15-T1) nhúng component

---

## 2. WAVE 16 — Production Flow Hardening

### Dependency chain

```
W16-T1 (Campaign.cshtml refactor + Gateway endpoints)
    ↓
W16-T2 (Dashboard — fix RealTimeDashboard TenantId)

W16-T3 (VoiceCommand.razor) — độc lập, song song với T1/T2
```

---

### W16-T1 — Refactor `Campaign.cshtml` — Xóa CoreHub inject, fix order flow

**Branch:** `feature/wave16-khachlink-production`
**Priority:** 🟡 HIGH
**Depends on:** Wave 15 complete
**Conflict risk:** HIGH — đụng cả `.cshtml` + `.cshtml.cs` + cần thêm Gateway endpoints
**Task card:** [W16-T1-card.md](W16-T1-card.md)

#### Vấn đề hiện tại
| Vấn đề | Severity |
|--------|---------|
| `Campaign.cshtml.cs` inject `ISocialCampaignService` từ CoreHub trực tiếp — vi phạm TD-001 | 🔴 Architecture |
| `orderProduct()` JS dùng `setTimeout` giả — không gọi API | 🔴 Fake flow |
| Social proof: `setInterval` + hardcode tên | 🟡 Fake UX |
| `originalPrice = item.Price * 1.25m` — giá gốc bịa | 🟡 Fake data |
| `Products` list luôn rỗng | 🔴 Broken |

#### Quyết định
- **Social proof section:** XÓA hoàn toàn
- **`originalPrice * 1.25`:** XÓA discount badge — hiển thị giá thật
- **`ISocialCampaignService`:** Thay bằng `HttpClient("gateway")`
- **`orderProduct()`:** Gọi `POST /api/orders` thật

#### Gateway endpoints cần thêm
```
GET  /api/campaigns/{trackingCode}     — load campaign info
POST /api/campaigns/click/{code}       — record click
```

#### Success criteria
- [ ] `Campaign.cshtml.cs` không còn inject `ISocialCampaignService`
- [ ] Products load từ `api/products` thật
- [ ] Social proof section đã xóa
- [ ] `orderProduct()` gọi `POST /api/orders` thật
- [ ] `dotnet build` → 0 errors

---

### W16-T2 — Dashboard: Xóa VanAnDashboard, Fix RealTimeDashboard TenantId

**Priority:** 🟡 HIGH
**Depends on:** W15-T1 (`Dashboard.razor` đã tồn tại)
**Conflict risk:** LOW
**Task card:** [W16-T2-card.md](W16-T2-card.md)

#### Vấn đề duy nhất trong RealTimeDashboard
```csharp
// HIỆN TẠI — SAI: hardcode tenantId
var shopId = "demo-shop";
await _hubConnection.InvokeAsync("JoinShopGroup", shopId);

// SAU — ĐÚNG: lấy từ ITenantService
@inject ITenantService TenantService
var shopId = TenantService.GetCurrentTenantId().ToString();
```

#### Files thay đổi
| File | Action |
|------|--------|
| `Components/Pages/VanAnDashboard.razor` | XÓA |
| `Components/Dashboard/RealTimeDashboard.razor` | SỬA: 2 chỗ `"demo-shop"` → `TenantService.GetCurrentTenantId().ToString()` |
| `Pages/Dashboard.razor` | Đã tạo ở W15-T1 — page wrapper `@page "/dashboard"` nhúng `<RealTimeDashboard />` |

#### Success criteria
- [ ] `VanAnDashboard.razor` đã xóa
- [ ] `RealTimeDashboard.razor` không còn `"demo-shop"` hardcode
- [ ] `Pages/Dashboard.razor` tồn tại tại route `/dashboard`
- [ ] `dotnet build` → 0 errors

---

### W16-T3 — Fix `VoiceCommand.razor` — Inject đúng HttpClient

**Priority:** 🟢 MEDIUM — độc lập, không block các task khác
**Depends on:** Không có
**Conflict risk:** VERY LOW — 1 thay đổi nhỏ
**Task card:** [W16-T3-card.md](W16-T3-card.md)

#### Vấn đề
```csharp
// HIỆN TẠI — SAI
@inject HttpClient Http
await Http.PutAsJsonAsync($"/api/v1/orders/{currentOrderId}/note", updateData);

// SAU — ĐÚNG
@inject IHttpClientFactory HttpClientFactory
private HttpClient Http => HttpClientFactory.CreateClient("gateway");
await Http.PutAsJsonAsync($"api/v1/orders/{currentOrderId}/note", updateData);
```

> **Note:** Endpoint `PUT /api/v1/orders/{id}/note` — cần verify tồn tại trong `2_Gateway/Controllers/VoiceCommandController.cs` trước khi sửa.

#### Success criteria
- [ ] Không còn `@inject HttpClient Http` trong `VoiceCommand.razor`
- [ ] Dùng `IHttpClientFactory("gateway")`
- [ ] `dotnet build` → 0 errors

---

## 3. BẢNG TRẠNG THÁI ĐẦY ĐỦ

| Task | File(s) | Depends | Status |
|------|---------|---------|--------|
| W15-T1 | 6 files xóa + `Dashboard.cshtml` → `Dashboard.razor` | — | 📋 TODO |
| W15-T2 | `Program.cs` Blazor Web App routing | W15-T1 | 📋 TODO |
| W15-T3 | `Pages/VoiceNote.razor` rewrite | W15-T2 | 📋 TODO |
| W15-T4 | Verify build + E2E contract | W15-T3 | 📋 TODO |
| W15-T5 | Update `project_state.md` | W15-T4 | 📋 TODO |
| W16-T1 | `Pages/Campaign.cshtml` + `.cshtml.cs` + Gateway endpoints | W15 complete | ✅ DONE |
| W16-T2 | `Components/Pages/VanAnDashboard.razor` (XÓA) + `Components/Dashboard/RealTimeDashboard.razor` (FIX) | W15-T1 | ✅ DONE |
| W16-T3 | `Components/VoiceCommand.razor` | — | ✅ DONE |

---

## 4. EXIT CRITERIA — WAVE 16 HOÀN CHỈNH

Toàn bộ happy path end-to-end phải hoạt động:

```
1. User truy cập KhachLink → Home.razor load sản phẩm thật từ Gateway (đã có từ Wave 13)
2. User click "Đặt ngay" → CartService.AddItem với ProductId thật
3. User vào /cart → thấy sản phẩm thật
4. User vào /checkout → POST api/orders với ProductId hợp lệ → nhận orderId thật
5. Redirect → /order-tracking/{orderId} → GET api/orders/{id} → thấy đơn thật
6. User vào /c/{code} → load campaign thật + sản phẩm thật + order thật
7. Staff vào /dashboard → RealTimeDashboard kết nối SignalR với TenantId thật
```

- [ ] `dotnet build VanAn.sln` → 0 errors
- [ ] `guard-check.ps1` → PASS
- [ ] `VanAn.Architecture.Tests` → 7/7 PASS
- [ ] Không còn `Guid.NewGuid()` trong product loading
- [ ] Không còn direct CoreHub inject trong KhachLink
- [ ] Không còn `"demo-shop"` hardcode
- [ ] Không còn `@inject HttpClient Http` (direct inject)

---

## 5. PHASE 2 — RETENTION & LOYALTY

Các tính năng retention (Customer Identity, Loyalty Dashboard, Order History, PWA Push, Store Finder, End-User Layout) đã được **tách sang file riêng**:

➡️ **`docs/AI/tasks/KHACHLINK_RETENTION_PLAN.md`**

Lý do tách:
- Retention là scope creep so với mục tiêu "production-ready order flow"
- Các task T1–T9 của Wave 17 phụ thuộc lẫn nhau và phức tạp; cần phase riêng
- Giữ KHACHLINK_PRODUCTION_PLAN.md tập trung vào cleanup + hardening cần thiết cho production

---

## 6. MAINTENANCE LOG

* **2026-06-26:** Plan rebuilt — align với Wave 13 completion + quyết định kiến trúc mới:
  - W15-T1: phạm vi đổi từ "xóa 7 files" → "xóa 6 files + convert Dashboard.cshtml → Dashboard.razor"
  - W15-T2: đổi từ "xóa MapFallbackToPage" → "migrate sang Blazor Web App routing (AddRazorComponents + MapRazorComponents<App>)"
  - _Host.cshtml: xác nhận XÓA theo directive kiến trúc (không phải giữ)
  - W16 rút gọn: T1 (Campaign) + T2 (Dashboard) + T3 (VoiceCommand) — T4/T5 cũ đã done ở Wave 13
  - Wave 17 tách sang KHACHLINK_RETENTION_PLAN.md (DEFERRED)
* **2026-06-25:** File created — Wave 15 moved from `PRODUCTION_HYGIENE_master_plan.md`, Wave 16 scope defined
