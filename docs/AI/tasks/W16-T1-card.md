# TASK CARD: W16-T1 — Refactor Campaign.cshtml

## 1. GOAL & CONTEXT
- **Mục tiêu cốt lõi:** Xóa `ISocialCampaignService` inject trực tiếp từ CoreHub trong `Campaign.cshtml.cs` (vi phạm TD-001), fix `orderProduct()` JS giả, xóa social proof fake, hiển thị giá thật — biến trang campaign thành flow thật
- **Nghiệp vụ áp dụng:** Trang `/c/{trackingCode}` là landing page marketing cho khách hàng từ QR code / link chia sẻ. Đây là first touch point quan trọng — phải gọi API thật
- **Master plan:** `docs/AI/tasks/KHACHLINK_PRODUCTION_PLAN.md` § W16-T1
- **Depends on:** Wave 15 complete (app khởi động clean, Blazor Web App routing)

## 2. ACTIVE WORKFLOW ROUTING
- **Target Workflow:** `newfeaturebuild.md` (ANALYZE → IMPLEMENT — sửa kiến trúc + thêm endpoint)
- **Execution Mode:** IMPLEMENT (plan đã được approve trong master plan)

## 3. PHÂN TÍCH HIỆN TRẠNG

### Campaign.cshtml.cs — Vi phạm kiến trúc
```csharp
// HIỆN TẠI — SAI: inject CoreHub services trực tiếp (vi phạm TD-001)
public class CampaignModel(ISocialCampaignService socialCampaignService, IShopConfigService shopConfigService) : PageModel
{
    // ...
    Campaign = await _socialCampaignService.GetCampaignByTrackingCodeAsync(TrackingCode);
    _ = await _socialCampaignService.RecordClickAsync(TrackingCode);
    // Products stays empty — không load được
}
```

### Campaign.cshtml — Vấn đề fake
| Vấn đề | File / Dòng | Severity |
|--------|------------|---------|
| `originalPrice = item.Price * 1.25m` — giá gốc bịa | `Campaign.cshtml` line 95 | 🔴 Fake data |
| `Products` list luôn rỗng — comment "TODO" | `Campaign.cshtml.cs` line 78 | 🔴 Broken |
| `orderProduct()` dùng `setTimeout` giả — không gọi API | `Campaign.cshtml` JS function | 🔴 Fake flow |
| Social proof: `setInterval` + tên hardcode + vị trí bịa | `Campaign.cshtml` lines 220–261 | 🟡 Fake UX |
| `ISocialCampaignService` inject trực tiếp từ `VanAn.CoreHub.Services` | `Campaign.cshtml.cs` line 3, 8 | 🔴 Architecture |

## 4. QUYẾT ĐỊNH KIẾN TRÚC

| Vấn đề | Quyết định |
|--------|-----------|
| `ISocialCampaignService` inject | THAY bằng `IHttpClientFactory("gateway")` — gọi `GET /api/campaigns/{code}` và `POST /api/campaigns/click/{code}` |
| `originalPrice * 1.25` discount badge | XÓA — hiển thị `item.Price` thật, không có giá bịa |
| Social proof section | XÓA hoàn toàn — fake data là vi phạm trust |
| `orderProduct()` JS | THAY bằng `fetch("api/orders", { method: "POST", body: {...} })` thật |
| Products list | Load từ `GET /api/products?shopId=` qua Gateway (đã có từ Wave 13) |

## 5. GATEWAY ENDPOINTS CẦN THÊM

```
GET  /api/campaigns/{trackingCode}   → load campaign info
POST /api/campaigns/click/{code}     → record click (analytics)
```

> **Note:** `POST /api/campaigns/click/{code}` hiện tại đã được gọi từ JS `recordClick()` — cần đảm bảo endpoint tồn tại trong Gateway. Kiểm tra `2_Gateway/Controllers/` trước khi sửa.

## 6. RELEVANT FILES (CONTEXT BOUNDARY)

**Files được phép đọc/sửa:**
- `5_WebApps/KhachLink/Pages/Campaign.cshtml` (SỬA — xóa fake sections, fix JS)
- `5_WebApps/KhachLink/Pages/Campaign.cshtml.cs` (SỬA — thay CoreHub inject bằng HttpClient)
- `5_WebApps/KhachLink/Program.cs` (đọc — xác nhận `"gateway"` named HttpClient tồn tại)
- `2_Gateway/Controllers/` (đọc — verify campaign endpoints tồn tại, tạo mới nếu thiếu)

**KHÔNG được sửa:**
- `1_Shared/Domain.cs` — Domain Layer Protection
- `3_CoreHub/Services/ISocialCampaignService.cs` — không sửa CoreHub
- `5_WebApps/KhachLink/Pages/Home.razor` — canonical page, không thay đổi

## 7. TARGET STATE

### Campaign.cshtml.cs — Sau fix
```csharp
using System.Net.Http;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc;

namespace VanAn.KhachLink.Pages
{
    public class CampaignModel(IHttpClientFactory httpClientFactory) : PageModel
    {
        private HttpClient Http => httpClientFactory.CreateClient("gateway");

        public CampaignInfo? Campaign { get; set; }
        public string TrackingCode { get; set; } = string.Empty;
        public List<ProductSummary> Products { get; set; } = [];

        [FromQuery(Name = "shopId")]
        public Guid? ShopId { get; set; }

        public async Task<IActionResult> OnGetAsync(string trackingCode)
        {
            TrackingCode = trackingCode ?? string.Empty;
            if (string.IsNullOrEmpty(TrackingCode)) return NotFound();

            // Load campaign from Gateway
            var campaignResp = await Http.GetAsync($"api/campaigns/{TrackingCode}");
            if (!campaignResp.IsSuccessStatusCode) return NotFound();
            Campaign = await campaignResp.Content.ReadFromJsonAsync<CampaignInfo>();

            // Record click (fire-and-forget, no await needed for UX)
            _ = Http.PostAsync($"api/campaigns/click/{TrackingCode}", null);

            // Load products via Gateway (already exists from Wave 13)
            if (ShopId.HasValue)
            {
                var productsResp = await Http.GetAsync($"api/products?shopId={ShopId}");
                if (productsResp.IsSuccessStatusCode)
                    Products = await productsResp.Content.ReadFromJsonAsync<List<ProductSummary>>() ?? [];
            }

            return Page();
        }
    }

    public record CampaignInfo(string CampaignName, int TotalClicks, int ConvertedOrders, Guid TenantId);
    public record ProductSummary(Guid Id, string Name, string Description, decimal Price, string ImageUrl);
}
```

### Campaign.cshtml — Các thay đổi HTML/JS
```diff
- var originalPrice = item.Price * 1.25m; // Simulate original price
- <span class="position-absolute top-0 end-0 m-2 badge bg-danger">
-     -@Math.Round((1 - (item.Price / originalPrice)) * 100)%
- </span>
- <span class="text-decoration-line-through text-muted">@originalPrice.ToString("N0")đ</span>
+ // Giá thật, không có discount badge bịa

- <!-- Social Proof Section --> ... setInterval fake ...
+ // XÓA toàn bộ social proof section

- function orderProduct(productId) {
-     // ...
-     setTimeout(() => { showNotification('Đơn hàng đã được ghi nhận!', 'success'); }, 2000);
- }
+ async function orderProduct(productId, productName) {
+     showNotification('Đang xử lý đơn hàng...', 'info');
+     const resp = await fetch('/api/orders', {
+         method: 'POST',
+         headers: { 'Content-Type': 'application/json' },
+         body: JSON.stringify({
+             productId,
+             customerDeviceId: customerId,
+             trackingCode: trackingCode,
+             quantity: 1,
+             tenantId: '@(Model.Campaign?.TenantId)'
+         })
+     });
+     if (resp.ok) {
+         const order = await resp.json();
+         window.location.href = `/order-tracking/${order.orderId ?? order.id}`;
+     } else {
+         showNotification('Không thể đặt hàng. Vui lòng thử lại.', 'danger');
+     }
+ }
```

## 8. BƯỚC THỰC HIỆN

```
S1: Verify Gateway campaign endpoints tồn tại
    → Grep 2_Gateway/Controllers/ cho "campaigns"
    → Nếu KHÔNG có: tạo 2_Gateway/Controllers/CampaignsController.cs (forward)
    → Nếu CÓ: note endpoint paths chính xác

S2: Verify Gateway orders endpoint nhận trackingCode
    → Kiểm tra OrdersController có nhận order với trackingCode field không
    → Nếu cần: thêm field vào CreateOrderRequest DTO

S3: Sửa Campaign.cshtml.cs
    → Thay ISocialCampaignService/IShopConfigService → IHttpClientFactory
    → Thêm DTO records (CampaignInfo, ProductSummary)
    → dotnet build → 0 errors

S4: Sửa Campaign.cshtml
    → Xóa discount badge (originalPrice * 1.25)
    → Xóa Social Proof section
    → Sửa orderProduct() JS gọi API thật
    → Sửa button onclick truyền productId (Guid) thay vì productName string

S5: Build + verify
    → dotnet build VanAn.sln → 0 errors
    → Kiểm tra trang /c/test-code trả 404 (đúng — không có campaign)

S6: Commit
    → "[W16-T1] Refactor Campaign.cshtml — replace CoreHub inject with Gateway HttpClient, remove fake social proof"
```

## 9. SUCCESS CRITERIA (ĐO LƯỜNG ĐƯỢC)
- [ ] **SC1:** `Campaign.cshtml.cs` không còn `using VanAn.CoreHub.Services`
- [ ] **SC2:** `Campaign.cshtml.cs` không còn `ISocialCampaignService` hoặc `IShopConfigService` inject
- [ ] **SC3:** `Campaign.cshtml.cs` dùng `IHttpClientFactory` để gọi `api/campaigns/{code}`
- [ ] **SC4:** `Campaign.cshtml` không còn `originalPrice * 1.25` và discount badge
- [ ] **SC5:** `Campaign.cshtml` không còn Social Proof section (`setInterval` + hardcode names)
- [ ] **SC6:** `orderProduct()` gọi `fetch('/api/orders', ...)` thật — không còn `setTimeout` fake
- [ ] **SC7:** `dotnet build VanAn.sln` → 0 errors
- [ ] **SC8:** Gateway có endpoint `GET /api/campaigns/{code}` (mới hoặc đã có)

**Implementation Date:** TBD
**Branch:** `feature/wave16-khachlink-production`

## 10. ACTIVE SKILLS (MAX 3)
- `build-error-analysis` — Fix compile errors sau khi thay inject
- `domain-integrity-validation` — Verify không ảnh hưởng Domain layer

## 11. AI HEALTH CHECK MATRIX (INITIAL)
- **Verified Facts:**
  - Fact 1: `Campaign.cshtml.cs` line 3: `using VanAn.CoreHub.Services;` — confirmed
  - Fact 2: `Campaign.cshtml.cs` line 8: `ISocialCampaignService socialCampaignService` constructor param — confirmed
  - Fact 3: `Campaign.cshtml.cs` line 79: comment "Products stays empty until real /api/products call" — confirmed
  - Fact 4: `Campaign.cshtml` line 95: `originalPrice = item.Price * 1.25m` — confirmed
  - Fact 5: `Campaign.cshtml` JS `orderProduct()`: `setTimeout(() => { showNotification(...) }, 2000)` — confirmed
  - Fact 6: Social proof section: `setInterval` + hardcode names (Minh Anh, Hoàng Nam...) — confirmed
  - Fact 7: `KhachLink/Program.cs` đã register `"gateway"` named HttpClient (verified Wave 13)
- **Assumptions:**
  - Gateway campaign endpoints chưa tồn tại — cần verify ở S1
  - `CreateOrderRequest` DTO có thể cần thêm `TrackingCode` field
- **Open Questions:**
  - Q1: Gateway đã có `GET/POST /api/campaigns/...` chưa? (verify ở S1)
  - Q2: OrdersController nhận `trackingCode` trong request body không? (verify ở S2)
- **Recommended Action:** IMPLEMENT — nhưng phải verify Q1, Q2 trước khi viết code

## 12. REVERSE IMPACT ANALYSIS
| Thay đổi | Reverse impact | Mitigation |
|---|---|---|
| Xóa `ISocialCampaignService` inject | `CampaignModel` không còn đọc campaign từ DB trực tiếp | Gateway endpoint cần cung cấp thay thế |
| Xóa social proof | Page trông "ít sôi động" hơn | Đây là intentional — fake social proof là anti-trust |
| `orderProduct()` gọi API thật | Đơn hàng thật được tạo → cần Gateway `POST /api/orders` hoạt động | Wave 13+ đã có endpoint này |

## 13. ESTIMATED EFFORT
- Medium effort — 2 files sửa + có thể tạo Gateway controller
- 1 session
- **BLOCKER:** Wave 15 phải complete (app chạy clean)
- **UNBLOCKS:** W16-T2 (song song, nhưng tốt hơn nếu T1 đã ổn định)
