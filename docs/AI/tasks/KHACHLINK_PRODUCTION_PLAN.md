# KHACHLINK — Real Production Plan
# Biến KhachLink từ Demo → Production-Ready

**Created:** 2026-06-25
**Last Updated:** 2026-06-26
**Scope:** `5_WebApps/KhachLink/` only
**Waves:** 15 (Page Cleanup) ✅ → 16 (Real Production Flows)

---

## WAVE 15 — KhachLink Page Cleanup

**Branch:** `feature/wave15-khachlink-page-cleanup`
**Estimated sessions:** 3
**Priority:** 🟡 HIGH — Demo/dead pages gây confusion, block E2E, vi phạm architecture VA-KHACHLINK-004
**Conflict risk:** MEDIUM — Xóa files + đổi routing fallback + rewrite 1 component
**Depends on:** Wave 14 (API request signing) complete

### Vấn đề cụ thể cần fix

Được phát hiện qua audit `e2e-gap-backlog.md`, `TD-001_KhachLink_ArchitecturalViolation.md`, E2E specs (`order-flow.spec.ts`, `voice-command.spec.ts`) và `functional-requirements.md §1.3`.

| File | Loại vấn đề | Nguồn xác nhận |
|------|------------|----------------|
| `Pages/Index.razor` | Dead code — route `@page "/"` conflict với `Home.razor`, `SampleProducts` hardcode, `Checkout()` fake `Task.Delay` | `e2e-gap-backlog.md` §Route #9 |
| `Pages/IndexModern.cshtml` (`/modern`) | Demo landing page thuần — stats bịa (`1000+ quán`, `50K+/ngày`), không có handler, không có test reference | Không có reference trong bất kỳ tài liệu nào |
| `Pages/_Host.cshtml` | Orphan HTML shell — không được route đến bởi `Program.cs`, link `/order-tracking/demo` trỏ route không tồn tại | `Program.cs` line 175 `MapFallbackToPage("/Index")` |
| `Pages/Index.cshtml` + `Index.cshtml.cs` | Duplicate landing vi phạm `VA-KHACHLINK-004` — direct DB access, social proof JS fake, `FeaturedProducts` hardcode | `TD-001`, `Index.cshtml.cs` TECH DEBT comment |
| `Pages/VoiceNote.razor` | Sai kiến trúc — `POST /api/orders/voice-note` không tồn tại, inject `HttpClient` trực tiếp thay vì `IHttpClientFactory("gateway")`, `<html><head><body>` trong `.razor`, `alert()` native | `voice-command.spec.ts` TC_Voice_Flow, `functional-requirements.md §1.3` |
| `Components/Pages/Home.razor` | Scaffold "Hello world" 5 dòng — `@page "/"` conflict trực tiếp với `Pages/Home.razor` đang live, non-deterministic routing | Rà soát 2026-06-25 |
| `Pages/Dashboard.cshtml` | 3 dòng shell rỗng — không có logic, Wave 16-T4 sẽ tạo `Pages/Dashboard.razor` thay thế | Rà soát 2026-06-25 |

### Quyết định kiến trúc

- **Index.razor:** XÓA — dead code, bị shadow bởi `Home.razor` (canonical page được E2E test)
- **IndexModern.cshtml:** XÓA — demo prototype không kết nối hệ thống, không có test coverage
- **_Host.cshtml:** XÓA — orphan, không được route đến, nội dung marketing tĩnh không giá trị
- **Index.cshtml + Index.cshtml.cs:** XÓA — duplicate landing, vi phạm VA-KHACHLINK-004 (direct DB)
- **Components/Pages/Home.razor:** XÓA — scaffold Blazor template, route conflict live ngay
- **Pages/Dashboard.cshtml:** XÓA — shell rỗng, sẽ được thay bởi `Pages/Dashboard.razor` ở Wave 16
- **VoiceNote.razor:** REWRITE — nghiệp vụ thật (FR §1.3 KDS voice note), nhưng cần sửa endpoint, kiến trúc, cấu trúc
- **Program.cs:** Sau khi xóa `Index.cshtml`, đổi `MapFallbackToPage("/Index")` → dùng Blazor routing thuần

### Bối cảnh nghiệp vụ

`Home.razor` là **canonical entry point** được E2E xác nhận:
- `order-flow.spec.ts` navigate về `KHACHLINK_URL/home`, tìm `.feature-card`, click `button:has-text("Đặt ngay")`
- `order-tracking.spec.ts` navigate về `KHACHLINK_URL/home` trước khi checkout
- `functional-requirements.md §1.3`: KDS cần ghi chú voice — `VoiceNote.razor` có nghiệp vụ thật nhưng sai implementation

`VoiceNote.razor` sau khi rewrite phải:
- Dùng `IHttpClientFactory("gateway")` + endpoint `POST /api/v1/voicecommand/text-command` (đã có trong `voice-command.spec.ts` TC_Voice_TextCommand)
- Không có `<html><head><body>` tag trong Razor component
- Dùng `VanAnAlert` thay `alert()` native

### Tasks

| # | Task ID | Task | Depends on | Task card | Status |
|---|---------|------|-----------|-----------|--------|
| 1 | W15-T1 | Xóa 7 dead/demo pages + convert `Dashboard.cshtml` → `Dashboard.razor` | — | [W15-T1-card.md](W15-T1-card.md) | ✅ DONE |
| 2 | W15-T2 | Modernize `Program.cs` — Blazor Web App routing (`AddRazorComponents` + `MapRazorComponents<App>`) | W15-T1 | [W15-T2-card.md](W15-T2-card.md) | ✅ DONE |
| 3 | W15-T3 | Rewrite `VoiceNote.razor` — fix endpoint, inject `IHttpClientFactory`, xóa `<html>` tags, thay `alert()` bằng UI Platform | W15-T2 | [W15-T3-card.md](W15-T3-card.md) | ✅ DONE |
| 4 | W15-T4 | Verify build 0 errors + E2E selector contract (`Home.razor` `.feature-card`, `Đặt ngay`) | W15-T3 | [W15-T4-card.md](W15-T4-card.md) | ✅ DONE |
| 5 | W15-T5 | Update `project_state.md` — ghi nhận cleanup, cập nhật Next Actions | W15-T4 | [W15-T5-card.md](W15-T5-card.md) | ✅ DONE |

### Files được phép sửa/xóa (W15)

**XÓA:**
- `5_WebApps/KhachLink/Pages/Index.razor`
- `5_WebApps/KhachLink/Pages/IndexModern.cshtml`
- `5_WebApps/KhachLink/Pages/_Host.cshtml`
- `5_WebApps/KhachLink/Pages/Index.cshtml`
- `5_WebApps/KhachLink/Pages/Index.cshtml.cs`
- `5_WebApps/KhachLink/Components/Pages/Home.razor` ← thêm mới
- `5_WebApps/KhachLink/Pages/Dashboard.cshtml` ← thêm mới

**SỬA:**
- `5_WebApps/KhachLink/Program.cs` — dòng `MapFallbackToPage("/Index")`
- `5_WebApps/KhachLink/Pages/VoiceNote.razor` — rewrite theo kiến trúc đúng

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
- [ ] Verify: 7 files đã xóa không còn trong repository
- [ ] Verify: `Home.razor` serve tại `http://localhost:5002/` — có `.feature-card` và `button:has-text("Đặt ngay")` (E2E selector contract từ `order-flow.spec.ts`)
- [ ] Verify: `VoiceNote.razor` không có `<html>`, không có `HttpClient` direct inject, endpoint trỏ `IHttpClientFactory("gateway")` → `/api/v1/voicecommand/text-command`
- [ ] Verify: Không có broken `@using` hay `@inject` references đến các files đã xóa trong toàn bộ `KhachLink/`
- [ ] Verify: Không còn `@page "/"` conflict — chỉ còn `Pages/Home.razor` là handler duy nhất cho route `/`

### Risk notes
- **Route conflict resolution:** Khi xóa `Index.razor` + `Components/Pages/Home.razor`, Blazor Router sẽ chỉ còn `Home.razor` làm handler duy nhất cho `/` — verify không có double-route error.
- **`_Host.cshtml` xóa:** Cần kiểm tra không có `@layout` hay `@using` nào trong components khác reference đến nó.
- **VoiceNote rewrite:** Chỉ fix kiến trúc và cấu trúc — KHÔNG thay đổi business logic (speech recognition, transcription flow). Giữ nguyên `[JSInvokable] SetTranscriptionText()`.

### Why this wave
- `e2e-gap-backlog.md` có task **T-01** và **T-02** là P0 priorities; cleanup pages này unblock các E2E tests
- Vi phạm `VA-KHACHLINK-004` (direct DB trong `Index.cshtml.cs`) là kiến trúc violation cần clear trước production
- Route conflict `@page "/"` giữa 3 files là non-deterministic behavior hiện đang live

---

## 0. TRẠNG THÁI BASELINE (sau Wave 15)

### Luồng nào đã hoạt động đúng kiến trúc
| Luồng | Route | Trạng thái sau W15 |
|-------|-------|-------------------|
| Đặt hàng (Checkout) | `/checkout` | ✅ Gọi `POST api/orders` qua Gateway đúng chuẩn |
| Theo dõi đơn | `/order-tracking/{id}` | ✅ Gọi `GET api/orders/{id}` qua Gateway |
| Giỏ hàng (Cart logic) | `/cart` | ✅ `CartService` + localStorage — cơ chế tốt |
| Voice Note | `/voice-note` | ✅ Sau W15-T3: endpoint đúng, `IHttpClientFactory("gateway")` |

### Luồng vẫn còn giả/broken — mục tiêu Wave 16
| Luồng | Vấn đề | Task |
|-------|---------|------|
| Xem sản phẩm | `Home.razor` hardcode 4 sản phẩm với `Guid.NewGuid()` | W16-T1 + W16-T2 |
| Chiến dịch Marketing | `Campaign.cshtml` inject CoreHub trực tiếp, order giả, social proof bịa | W16-T3 |
| Dashboard | `VanAnDashboard.razor` — 3 TODO + `RealTimeDashboard.razor` hardcode `"demo-shop"` | W16-T4 |
| VoiceCommand (staff) | `VoiceCommand.razor` inject `HttpClient` trực tiếp | W16-T5 |

### Lưu ý bổ sung Wave 15 → 16
W15-T1 đã được cập nhật để xóa thêm `Components/Pages/Home.razor` và `Pages/Dashboard.cshtml`. Wave 16-T4 sẽ tạo `Pages/Dashboard.razor` thay thế.

---

## 1. QUYẾT ĐỊNH KIẾN TRÚC DASHBOARD

### Phân tích: VanAnDashboard.razor vs RealTimeDashboard.razor

| | `VanAnDashboard.razor` | `RealTimeDashboard.razor` |
|---|---|---|
| Dòng code | 566 dòng | 696 dòng |
| Route | `@page "/VanAnDashboard"` | Không có `@page` — component nhúng |
| Data source | 3 TODO — KHÔNG có data | SignalR `HubConnection` — có infrastructure |
| ShopId | Không có | Hardcode `"demo-shop"` (cần fix) |
| DI | `@inject ILogger` only — DI crash đã fix (W15) | `@inject IJSRuntime`, `@inject NavigationManager` |
| SignalR events | Không có | `ShopMetricsUpdate`, `ShopOrderUpdate`, `InventoryUpdate`, `Notification` |

**Quyết định: XÓA `VanAnDashboard.razor`, GIỮ VÀ FIX `RealTimeDashboard.razor`**

Lý do:
- `RealTimeDashboard.razor` có đầy đủ SignalR infrastructure, UI hoàn chỉnh 696 dòng
- `VanAnDashboard.razor` là 566 dòng TODO không có giá trị — shell với data giả
- `RealTimeDashboard.razor` chỉ cần fix 1 điểm: `"demo-shop"` → TenantId thật từ `ITenantService`
- Sau khi fix, tạo page wrapper `Pages/Dashboard.razor` với `@page "/dashboard"` nhúng component

---

## 2. WAVE 16 — TASKS

### Dependency chain
```
W16-T2 (Gateway /api/products endpoint)
    ↓
W16-T1 (Home.razor connect API thật)
    ↓
W16-T3 (Campaign.cshtml — refactor + Gateway endpoints)
    ↓
W16-T4 (Dashboard — fix RealTimeDashboard TenantId)

W16-T5 (VoiceCommand.razor) — độc lập, song song với T3/T4
```

---

### W16-T2 — Tạo Gateway endpoint `GET /api/products`

**Branch:** `feature/wave16-khachlink-production`
**Priority:** 🔴 CRITICAL — prerequisite của T1
**Conflict risk:** MEDIUM — tạo mới Controller + ShopERP service

#### Vấn đề
- `Home.razor` cần `GET /api/products?tenantId=xxx` nhưng Gateway không có endpoint này
- `ShopERP` không có `ProductsController`
- Domain entity `Product` tồn tại trong `1_Shared/Domain.cs` line 514
- `KhachLink/Models/ProductDto.cs` đã có với đúng fields cần thiết

#### Mapping Domain → DTO
```csharp
// Domain: 1_Shared/Domain.cs
Product.ProductId  (ProductId value object) → ProductDto.ProductId (Guid)
Product.Name       → ProductDto.Name
Product.Description → ProductDto.Description
Product.Price      → ProductDto.Price
Product.Category   → ProductDto.Category
Product.IsActive   → ProductDto.IsActive
Product.VatRate    → ProductDto.VatRate
Product.ImageUrl   → ProductDto.ImageUrl
```

#### Files cần tạo/sửa
| File | Action |
|------|--------|
| `5_WebApps/ShopERP/Controllers/ProductsController.cs` | TẠO MỚI — `GET /api/products?tenantId={id}&isActive=true` |
| `2_Gateway/Controllers/ProductsController.cs` | TẠO MỚI — forward về ShopERP |

#### ShopERP ProductsController (target)
```csharp
[ApiController]
[Route("api/[controller]")]
public class ProductsController(IVanAnDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetProducts([FromQuery] Guid tenantId, [FromQuery] bool isActive = true)
    {
        if (tenantId == Guid.Empty) return BadRequest("tenantId required");
        var products = await db.Products
            .Where(p => p.TenantId == tenantId && p.IsActive == isActive)
            .OrderBy(p => p.Name)
            .Select(p => new
            {
                ProductId = p.ProductId.Value,
                p.Name, p.Description, p.Price,
                p.Category, p.IsActive, p.VatRate, p.ImageUrl
            })
            .ToListAsync();
        return Ok(products);
    }
}
```

#### Gateway ProductsController (target)
```csharp
[ApiController]
[Route("api/[controller]")]
public class ProductsController(IHttpClientFactory factory) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetProducts([FromQuery] Guid tenantId, [FromQuery] bool isActive = true)
    {
        var client = factory.CreateClient("shoperp");
        var response = await client.GetAsync($"api/products?tenantId={tenantId}&isActive={isActive}");
        return response.IsSuccessStatusCode
            ? Ok(await response.Content.ReadFromJsonAsync<object>())
            : StatusCode((int)response.StatusCode);
    }
}
```

#### Success criteria
- [ ] `GET http://localhost:5001/api/products?tenantId={id}` → 200 + JSON array
- [ ] `dotnet build VanAn.sln` → 0 errors
- [ ] Response shape khớp `KhachLink/Models/ProductDto.cs`

---

### W16-T1 — Fix `Home.razor` — Connect sản phẩm thật từ Gateway

**Priority:** 🔴 CRITICAL — unblocks toàn bộ order flow
**Depends on:** W16-T2 (endpoint phải tồn tại)
**Conflict risk:** LOW — chỉ sửa `Pages/Home.razor`

#### Vấn đề
```csharp
// HIỆN TẠI — SAI
private void LoadProducts()  // sync, không có API call
{
    products = new List<ProductDto>
    {
        new() { ProductId = Guid.NewGuid(), ... },  // GUID giả mỗi render
        ...
    };
}
await Task.Delay(100); // ShowNotification — không làm gì
```

```csharp
// SAU — ĐÚNG
@inject IHttpClientFactory HttpClientFactory
@inject ITenantService TenantService

private async Task LoadProductsAsync()
{
    try
    {
        var tenantId = TenantService.GetCurrentTenantId();
        var http = HttpClientFactory.CreateClient("gateway");
        var result = await http.GetFromJsonAsync<List<ProductDto>>(
            $"api/products?tenantId={tenantId}&isActive=true");
        products = result ?? new();
    }
    catch (Exception ex)
    {
        Console.WriteLine($"LoadProducts error: {ex.Message}");
        products = new(); // empty, không crash
    }
}
```

#### Thay đổi cụ thể
| Dòng hiện tại | Thay đổi |
|--------------|---------|
| `private void LoadProducts()` | `private async Task LoadProductsAsync()` |
| 4 dòng `new() { ProductId = Guid.NewGuid(), ... }` | `GetFromJsonAsync<List<ProductDto>>(...)` |
| `await CartService.LoadCartFromStorageAsync(); LoadProducts();` | `await CartService.LoadCartFromStorageAsync(); await LoadProductsAsync();` |
| `private async Task ShowNotification(...) { await Task.Delay(100); }` | State variable + VanAnAlert (hoặc xóa nếu không dùng) |
| Stats hardcode: `1,234 khách`, `567 đơn` | Xóa hoặc thay bằng data thật (nếu có API) — nếu không: **xóa hẳn stats section** |

#### Success criteria
- [ ] `Home.razor` không còn `Guid.NewGuid()` trong product list
- [ ] `Home.razor` không còn `Task.Delay` vô nghĩa
- [ ] Product cards render đúng với data từ Gateway
- [ ] `AddToCart` add đúng ProductId thật → Checkout hoạt động end-to-end
- [ ] `dotnet build` → 0 errors

---

### W16-T3 — Refactor `Campaign.cshtml` — Xóa CoreHub inject, fix order flow

**Priority:** 🟡 HIGH
**Depends on:** W16-T2 (cần `GET /api/products` cho campaign products)
**Conflict risk:** HIGH — đụng cả `.cshtml` + `.cshtml.cs` + cần thêm 2 Gateway endpoints

#### Vấn đề hiện tại
| Vấn đề | Severity |
|--------|---------|
| `Campaign.cshtml.cs` inject `ISocialCampaignService` từ CoreHub trực tiếp — vi phạm TD-001 | 🔴 Architecture |
| `orderProduct()` JS dùng `setTimeout` giả — không gọi API | 🔴 Fake flow |
| Social proof: `setInterval` + hardcode tên `Minh Anh`, `Hoàng Nam`... | 🟡 Fake UX |
| `originalPrice = item.Price * 1.25m` — giá gốc bịa | 🟡 Fake data |
| `Products` list luôn rỗng (comment trong code xác nhận) | 🔴 Broken |
| `customer_device_id` dùng `Math.random()` trong JS (client) | 🟢 Minor |

#### Quyết định
- **Social proof section:** XÓA hoàn toàn — fake data không có giá trị production
- **`originalPrice * 1.25`:** XÓA discount badge — hiển thị giá thật
- **`ISocialCampaignService`:** Thay bằng `HttpClient("gateway")`
- **`orderProduct()`:** Gọi `POST /api/orders` thật thay vì `setTimeout`

#### Gateway endpoints cần thêm
```
GET  /api/campaigns/{trackingCode}     — load campaign info
POST /api/campaigns/click/{code}       — record click (đã có route trong Campaign.cshtml)
```

#### Campaign.cshtml.cs target state
```csharp
public class CampaignModel(IHttpClientFactory factory) : PageModel
{
    private readonly HttpClient _http = factory.CreateClient("gateway");

    public async Task<IActionResult> OnGetAsync(string trackingCode)
    {
        // Load campaign từ Gateway
        var campaign = await _http.GetFromJsonAsync<CampaignDto>($"api/campaigns/{trackingCode}");
        if (campaign == null) return NotFound();

        Campaign = campaign;
        TrackingCode = trackingCode;

        // Record click (fire and forget)
        _ = _http.PostAsync($"api/campaigns/click/{trackingCode}", null);

        // Load products từ Gateway
        Products = await _http.GetFromJsonAsync<List<ProductDto>>(
            $"api/products?tenantId={campaign.TenantId}&isActive=true") ?? new();

        return Page();
    }
}
```

#### Success criteria
- [ ] `Campaign.cshtml.cs` không còn inject `ISocialCampaignService`
- [ ] Products load từ `api/products` thật
- [ ] Social proof section đã xóa
- [ ] `orderProduct()` gọi `POST /api/orders` thật
- [ ] `dotnet build` → 0 errors

---

### W16-T4 — Dashboard: Xóa VanAnDashboard, Fix RealTimeDashboard TenantId

**Priority:** 🟡 HIGH
**Depends on:** Không có dependency
**Conflict risk:** LOW

#### Quyết định: XÓA VanAnDashboard.razor, GIỮ RealTimeDashboard.razor

**Tại sao:**
- `RealTimeDashboard.razor` (696 dòng): SignalR infrastructure đầy đủ, UI hoàn chỉnh — **chỉ cần fix 1 điểm**
- `VanAnDashboard.razor` (566 dòng): 3 TODO không data, không có SignalR — dead weight

#### Vấn đề duy nhất trong RealTimeDashboard
```csharp
// HIỆN TẠI — SAI: hardcode tenantId
var shopId = "demo-shop";
await _hubConnection.InvokeAsync("JoinShopGroup", shopId);
// ...
await _hubConnection.InvokeAsync("RequestShopMetrics", "demo-shop");
```

```csharp
// SAU — ĐÚNG: lấy từ ITenantService
@inject ITenantService TenantService

var shopId = TenantService.GetCurrentTenantId().ToString();
await _hubConnection.InvokeAsync("JoinShopGroup", shopId);
await _hubConnection.InvokeAsync("RequestShopMetrics", shopId);
```

#### Tạo page wrapper
```razor
// TẠO MỚI: Pages/Dashboard.razor
@page "/dashboard"
<RealTimeDashboard />
```
→ Thay thế `Pages/Dashboard.cshtml` (3 dòng rỗng — đã plan xóa ở W15-T1 bổ sung)

#### Files thay đổi
| File | Action |
|------|--------|
| `Components/Pages/VanAnDashboard.razor` | XÓA |
| `Components/Dashboard/RealTimeDashboard.razor` | SỬA: 2 chỗ `"demo-shop"` → `TenantService.GetCurrentTenantId().ToString()` |
| `Pages/Dashboard.razor` | TẠO MỚI — page wrapper `@page "/dashboard"` nhúng `<RealTimeDashboard />` |

#### Success criteria
- [ ] `VanAnDashboard.razor` đã xóa
- [ ] `RealTimeDashboard.razor` không còn `"demo-shop"` hardcode
- [ ] `Pages/Dashboard.razor` tồn tại tại route `/dashboard`
- [ ] `dotnet build` → 0 errors

---

### W16-T5 — Fix `VoiceCommand.razor` — Inject đúng HttpClient

**Priority:** 🟢 MEDIUM — độc lập, không block các task khác
**Depends on:** Không có
**Conflict risk:** VERY LOW — 1 thay đổi nhỏ

#### Vấn đề
```csharp
// HIỆN TẠI — SAI
@inject HttpClient Http
// ...
await Http.PutAsJsonAsync($"/api/v1/orders/{currentOrderId}/note", updateData);
```

```csharp
// SAU — ĐÚNG
@inject IHttpClientFactory HttpClientFactory
// ...
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
| W16-T2 | `ShopERP/Controllers/ProductsController.cs` + `Gateway/Controllers/ProductsController.cs` | — | 📋 TODO |
| W16-T1 | `Pages/Home.razor` | W16-T2 | 📋 TODO |
| W16-T3 | `Pages/Campaign.cshtml` + `.cshtml.cs` + Gateway endpoints | W16-T2 | 📋 TODO |
| W16-T4 | `Components/Pages/VanAnDashboard.razor` (XÓA) + `Components/Dashboard/RealTimeDashboard.razor` (FIX) + `Pages/Dashboard.razor` (TẠO) | — | 📋 TODO |
| W16-T5 | `Components/VoiceCommand.razor` | — | 📋 TODO |

---

## 4. EXIT CRITERIA — WAVE 16 HOÀN CHỈNH

Toàn bộ happy path end-to-end phải hoạt động:

```
1. User truy cập KhachLink → Home.razor load sản phẩm thật từ Gateway
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

## WAVE 17 — KhachLink Retention & Loyalty

**Branch:** `feature/wave17-khachlink-retention`
**Estimated sessions:** 5
**Priority:** 🟡 HIGH — Không có lý do để giữ app sau khi cài PWA; Wave 17 tạo retention loop
**Conflict risk:** MEDIUM-HIGH — Thêm Customer Identity layer, 4 Gateway endpoints mới, 4 UI pages mới
**Depends on:** Wave 16 complete (sản phẩm thật, order flow thật)

### Bối cảnh — Tại sao cần Wave 17

Sau Wave 16, KhachLink có order flow hoàn chỉnh nhưng **user là anonymous hoàn toàn**:
- Không có account → không thể associate điểm thưởng
- Không có push subscription endpoint → PWA install vô nghĩa
- Không có lịch sử đơn hàng → user phải nhớ OrderId để track
- `GoogleMaps.razor` có `AIzaSyDummyKey` placeholder → bản đồ không load

**Backend đã sẵn sàng** — `LoyaltyRewards` entity, `ILoyaltyRewardsService`, `LoyaltyRewardsRepository`, `CustomerOnboardingService` (có SMS), `Customer` entity với `PhoneNumber` + `DeviceId` + `CustomerTier` (Bronze/Silver/Gold/Platinum), `IdentityUpgradeModal.razor` component — tất cả đã tồn tại. Wave 17 chỉ cần **expose qua Gateway + xây UI**.

---

### Dependency chain Wave 17

```
W17-T1 (Customer Identity — Phone OTP)
    ↓
W17-T2 (Loyalty Dashboard — điểm + lịch sử + tier)
    ↓
W17-T3 (Lịch sử đơn hàng cá nhân)

W17-T4 (Fix PWAService.DisposeAsync + Push Subscription endpoint)  ← độc lập
W17-T5 (Store Finder — fix Google Maps key + multi-shop)           ← độc lập
W17-T6 (NavMenu update)                                            ← sau T1
W17-T9 (End-User Layout — đẹp + tùy biến ShopConfig)              ← sau T1, T6
W17-T7 (Verify + E2E retention flow)                               ← sau T1-T6, T9
W17-T8 (Update project_state.md)                                   ← sau T7
```

---

### W17-T1 — Customer Identity (Phone OTP Login)

**Priority:** 🔴 CRITICAL — prerequisite của T2, T3
**Files mới:** `Pages/Login.razor`, `Pages/Profile.razor`
**Gateway endpoints mới:** `POST /api/customers/otp/send`, `POST /api/customers/otp/verify`

#### Hiện trạng
- `Customer` entity có `PhoneNumber` + `DeviceId` + `CustomerTier` — **tồn tại trong Domain**
- `CustomerOnboardingService` có SMS send — **infrastructure có**
- `IdentityUpgradeModal.razor` — **UI component có sẵn**, chưa wire vào luồng thực
- `Order.CustomerDeviceId` — zero-friction fallback đã có trong Domain
- Không có OTP service, không có session/token management

#### Thiết kế luồng: Zero-friction → Upgrade

```
Lần đầu vào KhachLink
    │
    ▼
DeviceId tự sinh (localStorage) → đặt hàng được ngay (zero friction)
    │
    ▼ (sau 1 đơn thành công)
IdentityUpgradeModal hiện — "Bảo vệ điểm của bạn!"
    │
    ├─ User nhập SĐT → POST /api/customers/otp/send
    │       → SMS OTP (6 số, TTL 5 phút)
    │
    ├─ User nhập OTP → POST /api/customers/otp/verify
    │       → trả CustomerToken (JWT-lite, 30 ngày)
    │       → lưu localStorage "customer_token"
    │
    └─ Từ đây: mọi request mang header X-Customer-Token
```

#### Gateway endpoints target
```
POST /api/customers/otp/send
    Body: { phoneNumber, tenantId, deviceId }
    → ShopERP: tìm/tạo Customer, sinh OTP, gửi SMS

POST /api/customers/otp/verify
    Body: { phoneNumber, tenantId, otp }
    → ShopERP: verify OTP, trả { customerId, customerToken, tier, pointBalance }
```

#### Scope giới hạn
- **KHÔNG** tích hợp ASP.NET Identity — quá nặng cho KhachLink (customer-facing)
- **KHÔNG** sửa Domain.cs — `Customer` entity đã đủ fields
- Token là JWT-lite ký bằng `IDataProtector` — không cần Identity infrastructure
- `IdentityUpgradeModal.razor` đã có — chỉ cần wire `OnUpgrade` callback vào luồng OTP

#### Success criteria
- [ ] User có thể nhập SĐT → nhận SMS → verify → nhận token
- [ ] Token lưu `localStorage("customer_token")` và gửi theo mọi request
- [ ] `IdentityUpgradeModal` hiện sau lần đặt hàng đầu tiên (check `localStorage`)
- [ ] `dotnet build` → 0 errors

---

### W17-T2 — Loyalty Dashboard (Điểm + Lịch sử + Tier)

**Priority:** 🔴 HIGH — tính năng retention chính
**Depends on:** W17-T1 (cần CustomerToken để biết user là ai)
**Files mới:** `Pages/LoyaltyCard.razor`
**Gateway endpoints mới:** `GET /api/customers/{id}/loyalty`

#### Hiện trạng backend
| Thành phần | Trạng thái |
|-----------|-----------|
| `LoyaltyRewards` entity — `PointBalance`, `History` (JSON) | ✅ Domain |
| `ILoyaltyRewardsService.GetHistoryAsync()`, `GetAvailablePointsAsync()` | ✅ CoreHub |
| `LoyaltyRewardsRepository` | ✅ CoreHub |
| `Customer.CustomerTier` — Bronze/Silver/Gold/Platinum | ✅ Domain |
| `LoyaltyUpgradeConfig.MinOrdersForUpgrade`, `MinTotalAmountForUpgrade` | ✅ Domain |
| Gateway endpoint `GET /api/customers/{id}/loyalty` | ❌ Chưa có |
| KhachLink UI | ❌ Chưa có |

#### `LoyaltyCard.razor` — target UI
```
┌─────────────────────────────────────┐
│  [Avatar]  Nguyễn Văn A             │
│  🥉 Bronze Member                   │
│                                     │
│  ┌─────────────────────────────┐    │
│  │  💎  1,250 điểm             │    │
│  │  Cần thêm 750đ → Silver     │    │
│  └─────────────────────────────┘    │
│                                     │
│  Tiến độ lên hạng ████░░░░ 63%      │
│                                     │
│  Lịch sử gần đây                    │
│  ┌─────────────────────────────┐    │
│  │ +45đ  Trà Sua Matcha  hôm nay│   │
│  │ +38đ  Trà Chanh       hôm qua│   │
│  │ +100đ Welcome bonus  12/6    │   │
│  └─────────────────────────────┘    │
│                                     │
│  [Quy đổi điểm]  [Chia sẻ]         │
└─────────────────────────────────────┘
```

#### Tier rules (từ Domain)
| Tier | Điểm tích lũy | Ưu đãi |
|------|--------------|--------|
| Bronze | 0–999 | 1% cashback |
| Silver | 1,000–4,999 | 2% cashback |
| Gold | 5,000–19,999 | 3% cashback + priority |
| Platinum | 20,000+ | 5% cashback + free item/tháng |

> Tier rules cần xác nhận với business — chưa có trong Domain, cần config trong `LoyaltyUpgradeConfig`.

#### Gateway endpoint target
```
GET /api/customers/{customerId}/loyalty
    Header: X-Customer-Token: {token}
    → ShopERP: ILoyaltyRewardsService.GetCustomerRewardsAsync()
    → Response: { customerId, tier, pointBalance, history[], nextTierPoints, progressPercent }
```

#### Success criteria
- [ ] `GET /api/customers/{id}/loyalty` → 200 với đúng data
- [ ] `LoyaltyCard.razor` hiển thị tier badge, điểm, progress bar, 10 giao dịch gần nhất
- [ ] User không đăng nhập → redirect về Login

---

### W17-T3 — Lịch sử đơn hàng cá nhân

**Priority:** 🟡 HIGH
**Depends on:** W17-T1
**Files mới:** `Pages/OrderHistory.razor`
**Gateway endpoints mới:** `GET /api/orders?customerId={id}`

#### Hiện trạng
- `Order.CustomerDeviceId` đã có — link order với device (zero-friction)
- `OrdersController` ở Gateway hiện chỉ có `GET /api/orders/{id}` (1 đơn theo ID)
- Không có `GET /api/orders?customerId=` query

#### `OrderHistory.razor` — target UI
```
┌─────────────────────────────────────┐
│  📋 Lịch sử đơn hàng                │
│                                     │
│  [Tất cả] [Đang xử lý] [Hoàn thành]│
│                                     │
│  ┌─────────────────────────────┐    │
│  │ #3f2a1b  Hôm nay 14:23     │    │
│  │ 2 sản phẩm · 83,000đ       │    │
│  │ 🟢 Đã giao          [Xem]  │    │
│  └─────────────────────────────┘    │
│  ┌─────────────────────────────┐    │
│  │ #7c4d20  Hôm qua 10:15     │    │
│  │ 1 sản phẩm · 45,000đ       │    │
│  │ 🟢 Đã giao          [Xem]  │    │
│  └─────────────────────────────┘    │
└─────────────────────────────────────┘
```

#### Gateway endpoint target
```
GET /api/orders?customerId={id}&tenantId={tid}&page=1&pageSize=20
    Header: X-Customer-Token: {token}
    → ShopERP: query Orders WHERE CustomerDeviceId = deviceId OR CustomerId = customerId
    → Response: { orders[], totalCount, page }
```

#### Success criteria
- [ ] Endpoint `GET /api/orders?customerId=` trả đúng danh sách đơn
- [ ] `OrderHistory.razor` tại route `/my-orders` hiển thị đúng
- [ ] Click "Xem" → navigate đến `/order-tracking/{id}` đã có
- [ ] Anonymous user (chưa login) thấy đơn theo `DeviceId`

---

### W17-T4 — Fix PWAService + Push Subscription Endpoint

**Priority:** 🟡 HIGH — PWA install vô nghĩa nếu không có push
**Depends on:** W17-T1 (cần CustomerToken để gắn subscription với customer)
**Files sửa:** `Components/PWA/PWAInstallPrompt.razor`, `Services/PWA/PWAService.cs`
**Files mới:** Gateway `NotificationController`

#### PWA Bug fixes (từ review)

**Bug 1 — `async void Dispose()` → `IAsyncDisposable`**
```csharp
// TRƯỚC
public async void Dispose() { ... }

// SAU
@implements IAsyncDisposable
public async ValueTask DisposeAsync()
{
    PWAService.OnInstallStateChanged -= HandleInstallStateChanged;
    PWAService.OnOnlineStateChanged  -= HandleOnlineStateChanged;
    await PWAService.DisposeAsync();
}
```

**Bug 2 — Dismiss không persist**
```csharp
// SAU khi dismiss: lưu localStorage
private async Task DismissPrompt()
{
    _dismissed = true;
    _showInstallPrompt = false;
    await JSRuntime.InvokeVoidAsync("localStorage.setItem", "pwa_dismissed", "true");
}

// Khi init: kiểm tra trước
var dismissed = await JSRuntime.InvokeAsync<string>("localStorage.getItem", "pwa_dismissed");
if (dismissed == "true") return; // không show
```

**Bug 3 — CSS transition không kích hoạt**
```csharp
// TRƯỚC: display:none ngắt transition
private string GetDisplayStyle() => _showInstallPrompt ? "" : "display: none;";

// SAU: dùng CSS class .hidden (đã có trong file, chỉ cần dùng)
private string GetCssClass() => _showInstallPrompt && !_dismissed
    ? "pwa-install-prompt"
    : "pwa-install-prompt hidden";
```

**Bug 4 — `Task.Delay(3000)` không có CancellationToken**
```csharp
// SAU
private CancellationTokenSource _cts = new();

protected override async Task OnInitializedAsync()
{
    // ...
    await Task.Delay(3000, _cts.Token);
    if (!_cts.IsCancellationRequested)
        _showInstallPrompt = true;
}

public async ValueTask DisposeAsync()
{
    _cts.Cancel();
    // ...
}
```

#### Push Subscription endpoint (mới)
```
POST /api/notifications/push/subscribe
    Header: X-Customer-Token
    Body: { subscription: PushSubscriptionJson, tenantId }
    → Lưu subscription vào DB

POST /api/notifications/push/send  (internal — chỉ gọi từ ShopERP/CoreHub)
    Body: { customerId, title, body, data }
    → Web Push gửi đến subscription của customer
```

> **Note:** Cần VAPID key pair — generate 1 lần, lưu vào `appsettings.json` (`Notifications:VapidPublicKey`, `Notifications:VapidPrivateKey`). Không commit key vào git — dùng User Secrets.

#### Success criteria
- [ ] `PWAInstallPrompt.razor` implements `IAsyncDisposable` đúng
- [ ] Dismiss persist qua localStorage — không hiện lại sau reload
- [ ] Banner slide animation hoạt động (CSS class thay vì display:none)
- [ ] `Task.Delay` cancel khi component dispose
- [ ] `POST /api/notifications/push/subscribe` → lưu subscription
- [ ] Offline indicator chỉ hiện khi `!IsOnline`

---

### W17-T5 — Store Finder (Fix Google Maps + Multi-shop)

**Priority:** 🟢 MEDIUM
**Depends on:** Không có
**Files sửa:** `Components/GoogleMaps.razor`
**Files mới:** `Pages/StoreFinder.razor`
**Gateway endpoints mới:** `GET /api/shops?lat={}&lng={}&radius={}`

#### Hiện trạng
- `GoogleMaps.razor` có `AIzaSyDummyKey` — **bản đồ không load**
- `ShopConfig.Latitude/Longitude` đã có trong Domain
- Chỉ hiển thị **1 cửa hàng cố định** — không phải store finder
- `Shop` entity có `Address` nhưng không có `Latitude/Longitude` trên entity (chỉ có trên `ShopConfig`)

#### Fix 1 — Google Maps API key
```csharp
// TRƯỚC — hardcode placeholder
return $"...&key=AIzaSyDummyKey&...";

// SAU — lấy từ config
@inject IConfiguration Configuration
return $"...&key={Configuration["GoogleMaps:ApiKey"]}&...";
```
> Google Maps API key lưu User Secrets / appsettings — không commit.

#### Fix 2 — `StoreFinder.razor` tại `/stores`
```
┌─────────────────────────────────────┐
│  📍 Tìm cửa hàng gần bạn           │
│                                     │
│  [Bản đồ]                           │
│  ┌─────────────────────────────┐    │
│  │ 📍 Vạn An - Quận 1          │    │
│  │ 123 Nguyễn Trãi · 0.3 km   │    │
│  │ ⭐ Chấp nhận quy đổi điểm  │    │
│  │ [Đường đi]  [Đặt hàng]     │    │
│  └─────────────────────────────┘    │
│  ┌─────────────────────────────┐    │
│  │ 📍 Vạn An - Quận 3          │    │
│  │ 456 Lê Văn Sỹ   · 1.2 km   │    │
│  │ ⭐ Chấp nhận quy đổi điểm  │    │
│  └─────────────────────────────┘    │
└─────────────────────────────────────┘
```

#### Gateway endpoint target
```
GET /api/shops?lat={userLat}&lng={userLng}&radius=5&tenantId={id}
    → ShopERP: query Shops trong radius, sort by distance
    → Response: { shops[{ shopId, name, address, lat, lng, distanceKm, acceptsRedemption }] }
```

> **Domain note:** `Shop` entity hiện không có `Latitude/Longitude` — chỉ có `ShopConfig` record. Cần thêm `Latitude/Longitude` vào `Shop` entity. **Đây là Domain modification** → cần approval trước khi thực thi W17-T5. Nếu không muốn sửa Domain: dùng ShopConfig làm source of truth và query qua ShopConfigService.

#### Success criteria
- [ ] Google Maps load với key thật từ config
- [ ] `StoreFinder.razor` tại `/stores` hiện danh sách shops
- [ ] Sort by distance từ vị trí user (browser Geolocation API)
- [ ] Badge "Chấp nhận quy đổi điểm" hiển thị đúng

---

### W17-T6 — NavMenu + Deep Link cho Retention Features

**Priority:** 🟢 MEDIUM
**Depends on:** W17-T1 hoàn thành (cần biết user có login không)
**Files sửa:** `Components/Layout/NavMenu.razor`

#### Mục tiêu
Sau Wave 17, KhachLink có 4 pages retention mới — cần accessible từ nav:

| Route | Icon | Hiện khi |
|-------|------|---------|
| `/my-loyalty` | 💎 Điểm thưởng | Luôn hiện (anonymous thấy prompt login) |
| `/my-orders` | 📋 Đơn hàng | Luôn hiện |
| `/stores` | 📍 Tìm cửa hàng | Luôn hiện |
| `/profile` | 👤 Tài khoản | Luôn hiện |

#### Success criteria
- [ ] NavMenu có 4 items mới
- [ ] Active route highlight đúng
- [ ] Mobile nav menu responsive

---

### W17-T7 — Verify + E2E Retention Flow

**Priority:** 🔴 CRITICAL — gate trước merge
**Depends on:** T1–T6 xong

#### Verification scripts
```powershell
# 1. Build
dotnet build VanAn.sln --no-restore | Select-String "error"

# 2. Architecture tests
dotnet test 6_Tests/VanAn.Architecture.Tests --no-build

# 3. Loyalty endpoints
$token = "test-customer-token"
Invoke-WebRequest "http://localhost:5001/api/customers/otp/send" `
    -Method POST -Body '{"phoneNumber":"0901234567","tenantId":"..."}' `
    -ContentType "application/json"

# 4. No Guid.NewGuid() trong product loading
Select-String -Path "5_WebApps\KhachLink\Pages\Home.razor" -Pattern "Guid\.NewGuid"
# Expected: 0 matches

# 5. No async void Dispose
Select-String -Path "5_WebApps\KhachLink\Components\PWA\PWAInstallPrompt.razor" -Pattern "async void Dispose"
# Expected: 0 matches

# 6. No hardcode API key
Select-String -Path "5_WebApps\KhachLink\Components\GoogleMaps.razor" -Pattern "DummyKey"
# Expected: 0 matches

# 7. No "demo-shop" hardcode
Select-String -Recurse "5_WebApps\KhachLink" -Pattern '"demo-shop"' -Include "*.razor","*.cs"
# Expected: 0 matches
```

#### Success criteria
- [ ] `dotnet build VanAn.sln` → 0 errors
- [ ] `guard-check.ps1` → PASS
- [ ] `VanAn.Architecture.Tests` → 7/7 PASS
- [ ] Happy path end-to-end: cài PWA → đặt hàng → upgrade identity → xem điểm → xem lịch sử đơn
- [ ] Không còn anonymous dependency (tất cả loyalty features cần token)

---

### W17-T8 — Update project_state.md

**Priority:** 🟢 LOW
**Depends on:** W17-T7 PASS

Cập nhật `docs/AI/project_state.md`:
- Section 2: Wave 17 hoàn chỉnh
- Section 3: thêm retention features vào completed
- Section 4: Next Actions = Wave 18 (nếu có)
- Section 11: Last Updated

---

### Bảng trạng thái Wave 17

| Task | Mô tả | Depends | Card | Status |
|------|-------|---------|------|--------|
| W17-T1 | Customer Identity — Phone OTP | — | [W17-T1-card.md](W17-T1-card.md) | 📋 TODO |
| W17-T2 | Loyalty Dashboard | W17-T1 | [W17-T2-card.md](W17-T2-card.md) | 📋 TODO |
| W17-T3 | Lịch sử đơn hàng | W17-T1 | [W17-T3-card.md](W17-T3-card.md) | 📋 TODO |
| W17-T4 | Fix PWAService + Push endpoint | W17-T1 | [W17-T4-card.md](W17-T4-card.md) | 📋 TODO |
| W17-T5 | Store Finder + fix Maps key + Domain Lat/Lng | — | [W17-T5-card.md](W17-T5-card.md) | 📋 TODO |
| W17-T6 | NavMenu update | W17-T1 | [W17-T6-card.md](W17-T6-card.md) | 📋 TODO |
| W17-T7 | Verify E2E retention flow | T1–T6 | [W17-T7-card.md](W17-T7-card.md) | 📋 TODO |
| W17-T8 | Update project_state.md | W17-T7 | [W17-T8-card.md](W17-T8-card.md) | 📋 TODO |
| W17-T9 | End-User Layout — đẹp + tùy biến theo ShopConfig | W17-T1, W17-T6 | [W17-T9-card.md](W17-T9-card.md) | 📋 TODO |

---

### Hard stops Wave 17

- **W17-T5 Domain modification:** Thêm `Latitude/Longitude` vào `Shop` entity **phải có approval** trước khi thực thi. Nếu không được approve → dùng `ShopConfig` làm source.
- **VAPID keys + Google Maps API key:** KHÔNG commit vào git — dùng User Secrets (`dotnet user-secrets set`).
- **OTP/SMS:** Nếu SMS provider chưa config (`INotificationService`) → implement dry-run mode trả OTP trong response (dev only, feature flag).
- **CustomerToken:** KHÔNG dùng ASP.NET Identity — dùng `IDataProtector` JWT-lite để tránh database schema changes lớn.

---

## 5. MAINTENANCE LOG

* **2026-06-25:** Wave 17 T9 added — End-User Layout (KhachLinkLayout.razor tùy biến theo ShopConfig: 5 themes Classic/Modern/Teen/Lady/Premium, CSS variables --shop-primary, hero header với logo thật)
* **2026-06-25:** Wave 17 added — Retention & Loyalty (8 tasks: OTP identity, loyalty dashboard, order history, PWA fixes, store finder, nav, verify, state update)
  - Root prerequisite: Customer Identity (W17-T1) — mọi retention feature phụ thuộc vào đây
  - Domain modification alert: `Shop.Latitude/Longitude` cần approval trước W17-T5
  - Backend đã sẵn sàng: `LoyaltyRewards`, `ILoyaltyRewardsService`, `CustomerOnboardingService` (SMS), `IdentityUpgradeModal.razor`
* **2026-06-25:** File created — Wave 15 moved from `PRODUCTION_HYGIENE_master_plan.md`, Wave 16 scope defined
  - Wave 15 bổ sung 2 files xóa thêm: `Components/Pages/Home.razor`, `Pages/Dashboard.cshtml` (phát hiện khi rà soát route conflicts)
  - Quyết định kiến trúc Dashboard: XÓA `VanAnDashboard.razor`, FIX `RealTimeDashboard.razor`
  - Root cause của broken order flow: `Home.razor` `Guid.NewGuid()` → fix ở W16-T1 sau khi có Gateway endpoint (W16-T2)
