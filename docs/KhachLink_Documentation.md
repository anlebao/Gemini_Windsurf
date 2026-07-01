# KhachLink — Tài liệu mô tả chi tiết

## 1. Tổng quan

| Thuộc tính | Giá trị |
|---|---|
| **Tên dự án** | VanAn.KhachLink |
| **Loại ứng dụng** | Blazor WebAssembly PWA (Progressive Web App) |
| **Framework** | .NET 8, ASP.NET Core Blazor, SignalR |
| **Port mặc định** | 5002 |
| **Mục đích** | Customer-facing PWA — kết nối khách hàng với hệ sinh thái Vạn An |
| **Kiến trúc** | HTTP-only via Gateway, **không truy cập DB trực tiếp** |

---

## 2. Cấu trúc thư mục

```
5_WebApps/KhachLink/
├── Components/
│   ├── App.razor                      # Root component
│   ├── Routes.razor                     # Blazor Router
│   ├── CartDrawer.razor                 # Drawer giỏ hàng
│   ├── Dashboard/
│   │   └── RealTimeDashboard.razor     # Dashboard real-time
│   ├── DynamicThemeProvider.razor       # Theme động
│   ├── GoogleMaps.razor                 # Google Maps integration
│   ├── IdentityUpgradeModal.razor       # Modal nâng cấp tài khoản
│   ├── Layout/
│   │   ├── KhachLinkLayout.razor      # Layout chính
│   │   ├── MainLayout.razor
│   │   ├── NavMenu.razor
│   │   └── VanAnLayout.razor
│   ├── PWA/
│   │   └── PWAInstallPrompt.razor       # Prompt cài đặt PWA
│   ├── QRScanner.razor                  # Quét QR sản phẩm
│   ├── QrPaymentModal.razor             # Modal thanh toán QR
│   ├── SocialHub.razor                  # Social media embed
│   ├── VibeShowcase.razor               # Showcase sản phẩm
│   ├── VoiceCommand.razor               # Voice command UI
│   └── _Imports.razor
├── Pages/
│   ├── Home.razor           # Trang chủ
│   ├── Cart.razor           # Giỏ hàng
│   ├── Checkout.razor        # Thanh toán
│   ├── Dashboard.razor       # Dashboard
│   ├── Login.razor           # Đăng nhập OTP
│   ├── LoyaltyCard.razor     # Thẻ điểm thưởng
│   ├── OrderHistory.razor    # Lịch sử đơn hàng
│   ├── OrderTracking.razor   # Theo dõi đơn hàng
│   ├── Profile.razor         # Tài khoản
│   ├── Scan.razor            # Quét QR
│   ├── StoreFinder.razor     # Tìm cửa hàng
│   ├── VoiceNote.razor       # Ghi chú giọng nói
│   └── Campaign.cshtml.cs    # Razor Page campaign
├── Services/
│   ├── CartService.cs
│   ├── CartState.cs
│   ├── CheckoutFlowState.cs
│   ├── ConflictResolutionService.cs
│   ├── IndexedDBService.cs
│   ├── OfflineOrderService.cs
│   ├── PWAService.cs
│   ├── RecentlyViewedService.cs
│   ├── SyncConflictResolver.cs
│   ├── Dashboard/
│   │   └── RealTimeDashboardService.cs
│   └── Http/
│       ├── DashboardHttpService.cs
│       ├── OrderWorkflowHttpService.cs
│       ├── ProductHttpService.cs
│       └── SocialCampaignHttpService.cs
├── Models/
│   ├── OfflineOrderDto.cs
│   ├── OrderInfo.cs
│   └── ProductDto.cs
├── Hubs/
│   └── DashboardHub.cs
├── Components/Shared/
│   └── CurrencyHelper.cs
└── Program.cs
```

---

## 3. Các trang (Routes) và chức năng

| Route | Tên trang | Mô tả |
|---|---|---|
| `/` `/home` | **Home** | Trang chủ, hiển thị sản phẩm, recommendations, hero section |
| `/cart` | **Cart** | Xem/sửa giỏ hàng, tăng/giảm số lượng, xóa sản phẩm |
| `/checkout` | **Checkout** | Thanh toán đơn hàng, tạo QR thanh toán |
| `/my-orders` | **OrderHistory** | Lịch sử đơn hàng, lọc theo trạng thái |
| `/order-tracking/{orderId}` | **OrderTracking** | Theo dõi chi tiết đơn hàng bằng timeline |
| `/my-loyalty` | **LoyaltyCard** | Xem điểm thưởng, hạng thành viên, tiến độ lên hạng |
| `/profile` | **Profile** | Thông tin tài khoản, menu điều hướng |
| `/login` | **Login** | Đăng nhập bằng OTP (Phone → OTP → Verify) |
| `/stores` | **StoreFinder** | Tìm cửa hàng gần đây, hiển thị Google Maps |
| `/scan` | **Scan** | Quét QR sản phẩm, thêm vào giỏ hàng |
| `/voice-note` | **VoiceNote** | Ghi chú giọng nói cho đơn hàng |
| `/dashboard` | **Dashboard** | Dashboard real-time |

---

## 4. Các nghiệp vụ chính (Business Modules)

### 4.1 Product Catalog & Shopping
- Hiển thị danh mục sản phẩm từ `ProductHttpService`
- Recommendations cá nhân hóa dựa trên `customerId` + `tenantId`
- "Recently viewed" sử dụng `localStorage`
- Xem chi tiết sản phẩm từ catalog

### 4.2 Cart & Checkout
- `CartService` quản lý giỏ hàng bằng `localStorage`
- `CartState` lưu trữ items, tính tổng tiền
- `CheckoutFlowState` là state machine gồm 4 bước:
  - `Cart`
  - `CustomerInfo`
  - `Payment`
  - `Confirmation`
- Thanh toán QR qua `QrPaymentModal`
- Tích hợp `EnhancedCartService`

### 4.3 Order Management
- Tạo đơn hàng qua HTTP API
- Xem lịch sử đơn hàng (`/my-orders`)
- Theo dõi đơn hàng real-time (`/order-tracking/{orderId}`)
- Chuyển trạng thái đơn hàng qua `OrderWorkflowHttpService`

### 4.4 Offline-First Order
- `OfflineOrderService` tạo đơn hàng offline lưu vào IndexedDB
- Đồng bộ khi có mạng (`SyncOrdersAsync`, `SyncSingleOrderAsync`)
- Phân giải xung đột đồng bộ (`SyncConflictResolver`, `ConflictResolutionService`)
- Validate `ShopId` (tenant context) trước khi sync

### 4.5 Loyalty & Customer
- `LoyaltyCard` hiển thị điểm thưởng và hạng thành viên
- Tier tính toán on-the-fly từ `PointBalance`
- `Login` dùng OTP (Phone + MemoryCache ở backend)
- `Profile` tổng hợp loyalty, orders, stores

### 4.6 Store Finder
- Tìm cửa hàng gần đây
- Hiển thị bản đồ Google Maps (`GoogleMaps.razor`)
- Lọc theo bán kính, xem tất cả cửa hàng

### 4.7 QR & Scan
- Quét QR sản phẩm (`QRScanner.razor`)
- Thêm sản phẩm vào giỏ hàng từ QR
- Thanh toán QR (`QrPaymentModal`)

### 4.8 Social & Marketing
- `SocialHub.razor` nhúng Facebook/TikTok
- `SocialCampaignHttpService` quản lý chiến dịch
- Tracking URL, record click, conversion
- `Campaign.cshtml.cs` — landing page campaign

### 4.9 Real-time Dashboard
- `DashboardHub` — SignalR hub
- `RealTimeDashboardService` broadcast metrics mỗi 30s
- `DashboardHttpService` lấy PostgreSQL/SQLite metrics, sync status, system health
- Hỗ trợ nhóm theo `tenant_{id}` và `shop_{id}`

### 4.10 PWA Capabilities
- `PWAService` xử lý:
  - Install prompt
  - Service worker registration
  - Online/offline state
  - Push notification subscription
  - Local notification
  - Cache clearing
- `PWAInstallPrompt.razor` — UI prompt cài đặt
- `IndexedDBService` — lưu trữ local (orders, cart, products)

### 4.11 Voice Interaction
- `VoiceCommand.razor` — điều khiển bằng giọng nói
- `VoiceNote.razor` — ghi chú giọng nói cho đơn hàng

---

## 5. Dependency Injection (Program.cs)

| Service | Lifetime | Mục đích |
|---|---|---|
| `ICssAdapter` → `BootstrapAdapter` | Scoped | UI Platform CSS adapter |
| `IThemeProvider` → `ThemeProvider` | Scoped | Theme management |
| `ITenantService` → `TenantService` | Scoped | Multi-tenancy |
| `IOrderWorkflowService` → `OrderWorkflowHttpService` | Scoped | Order workflow HTTP |
| `ISocialCampaignService` → `SocialCampaignHttpService` | Scoped | Social campaign HTTP |
| `IDashboardService` → `DashboardHttpService` | Scoped | Dashboard HTTP |
| `IShopConfigService` → `ShopConfigService` | Scoped | Shop config (TODO: replace bằng HTTP) |
| `CartService` | Scoped | Giỏ hàng |
| `CheckoutFlowState` | Scoped | Checkout state machine |
| `PWAService` | Scoped | PWA features |
| `ProductHttpService` | Scoped | Product catalog HTTP |
| `RecentlyViewedService` | Scoped | Recently viewed |
| `RealTimeDashboardService` | Scoped | Real-time dashboard |
| `HttpClient "gateway"` | — | Base từ `Gateway:BaseUrl` |
| `IMemoryCache` | Singleton | Cache |
| `SignalR` | — | Real-time |

---

## 6. HTTP Client Integrations (via Gateway)

| Service | Endpoint | Chức năng |
|---|---|---|
| `ProductHttpService` | `shoperp/api/products` | Lấy danh sách sản phẩm |
| | `shoperp/api/products/recommended` | Recommendations |
| `OrderWorkflowHttpService` | `api/orderworkflow/{id}/status` | Chuyển trạng thái đơn |
| | `api/orderworkflow/{id}` | Get order |
| | `api/orderworkflow/by-customer/{id}` | Lấy đơn theo khách |
| | `api/orderworkflow/by-status/{status}` | Lấy đơn theo trạng thái |
| | `api/orderworkflow/transition-valid` | Kiểm tra transition hợp lệ |
| `SocialCampaignHttpService` | `api/socialcampaigns` | CRUD campaign |
| | `api/socialcampaigns/{id}/tracking-url` | Tạo tracking URL |
| | `api/socialcampaigns/record-click/{code}` | Ghi nhận click |
| | `api/socialcampaigns/{id}/increment-conversion` | Conversion |
| `DashboardHttpService` | `api/dashboard/postgresql-metrics` | PostgreSQL metrics |
| | `api/dashboard/sqlite-metrics/{nodeType}` | SQLite metrics |
| | `api/dashboard/sync-status` | Sync status |
| | `api/dashboard/system-health` | System health |

---

## 7. Models/DTOs

### ProductDto
```csharp
public class ProductDto
{
    public Guid ProductId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public decimal Price { get; set; }
    public string? Category { get; set; }
    public bool IsActive { get; set; }
    public decimal VatRate { get; set; }
    public string? ImageUrl { get; set; }
}
```

### RecommendedProductDto
Kế thừa `ProductDto`, thêm:
- `FrequencyScore`
- `TotalSpent`
- `RecommendationReason`

### OfflineOrderDto
- Lưu trữ offline order trong IndexedDB
- Có `ToDomain()` / `FromDomain()` để chuyển đổi với `Order` entity
- Validate `ShopId` (tenant) trước khi sync

### OrderInfo
- DTO thông tin đơn hàng hiển thị trên UI

---

## 8. Components quan trọng

| Component | Mô tả |
|---|---|
| `CartDrawer.razor` | Drawer hiển thị giỏ hàng trên mọi trang |
| `QRScanner.razor` | Component quét QR bằng camera |
| `QrPaymentModal.razor` | Modal hiển thị mã QR thanh toán |
| `GoogleMaps.razor` | Bản đồ cửa hàng |
| `SocialHub.razor` | Embed Facebook/TikTok |
| `VoiceCommand.razor` | Voice command UI |
| `PWAInstallPrompt.razor` | Prompt cài đặt PWA |
| `RealTimeDashboard.razor` | Real-time dashboard UI |
| `IdentityUpgradeModal.razor` | Modal nâng cấp tài khoản khách |
| `DynamicThemeProvider.razor` | Cung cấp theme động |
| `KhachLinkLayout.razor` | Layout chính của KhachLink |

---

## 9. Kiến trúc & Ràng buộc

### Data Flow
```
KhachLink (5002) → Gateway (5001) → ShopERP (5003) → SQLite
                         ↓
              [in-process CoreHub services]
                         ↓
                   PostgreSQL / SQLite
```

### Nguyên tắc
- KhachLink **KHÔNG** được inject `IVanAnDbContext` hay query DB local
- KhachLink **KHÔNG** được inject CoreHub services có repository dependencies
- Mọi tương tác dữ liệu phải qua HTTP via Gateway

### Cảnh báo kiến trúc hiện tại
⚠️ `OfflineOrderService` inject `IOrderService` trực tiếp từ `VanAn.CoreHub.Services` — vi phạm nguyên tắc trên. Cần thay bằng HTTP implementation.

---

## 10. Cấu hình

### appsettings.json
```json
{
  "Gateway": {
    "BaseUrl": "http://localhost:5001/"
  }
}
```

### ASPNETCORE_URLS
- Mặc định: `http://0.0.0.0:5002`

### Health Check
- Endpoint: `/health`
- Trả về: `{ Status: "Healthy", Service: "VanAn KhachLink", Timestamp: ... }`

---

## 11. Tích hợp UI Platform

- Sử dụng các component: `VanAnButton`, `VanAnCard`, `VanAnAlert`, `VanAnInput`, `VanAForm`, `VanATable`
- Inject `IThemeProvider` + `ITenantService` cho theming
- Mobile-first responsive
- Sử dụng design tokens

---

## 12. Bảo mật & Xác thực

- Đăng nhập bằng OTP (phone-based)
- `CustomerToken` sử dụng `IDataProtector` (không dùng JWT)
- Multi-tenancy qua `TenantId` filter tại mọi layer
- `Authorize` attribute trên các trang cần auth

---

## 13. E2E Test Contracts

Một số trang có `data-testid` hoặc class CSS để phục vụ E2E:
- `OrderTracking.razor`: `.order-tracking`
- `VoiceNote.razor`: `data-testid="voice-note-container"`, `data-testid="voice-note-header"`, `data-testid="transcription-text"`
