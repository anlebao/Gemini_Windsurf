# ShopERP — Tài liệu mô tả chi tiết

## 1. Tổng quan

| Thuộc tính | Giá trị |
|---|---|
| **Tên dự án** | VanAn.ShopERP |
| **Loại ứng dụng** | Blazor Server (Server-Side Rendering) |
| **Framework** | .NET 8, ASP.NET Core, EF Core, SignalR |
| **Port mặc định** | 5003 |
| **Mục đích** | Staff/admin UI — quản lý đơn hàng, kế toán, hóa đơn, người dùng, tenant |
| **Database** | SQLite (edge node) — `vanan_shoperp.db` |
| **Kiến trúc** | SQLite-only edge node, không dùng Npgsql trực tiếp |

---

## 2. Cấu trúc thư mục

```
5_WebApps/ShopERP/
├── Components/
│   ├── App.razor
│   ├── Routes.razor
│   ├── Layout/
│   │   ├── MainLayout.razor
│   │   └── NavMenu.razor
│   ├── Pages/
│   │   ├── Home.razor                 # Redirect to /sitemap
│   │   ├── Sitemap.razor              # Trang điều hướng chính
│   │   ├── Counter.razor
│   │   ├── Error.razor
│   │   ├── AccessDenied.razor
│   │   ├── Accounting/                # Module Kế toán
│   │   │   ├── AccountingIndex.razor
│   │   │   ├── AccountingLayout.razor
│   │   │   ├── RevenueEntry.razor
│   │   │   ├── ExpenseEntry.razor
│   │   │   ├── TransactionHistory.razor
│   │   │   ├── AccountBalance.razor
│   │   │   └── PeriodClosing.razor
│   │   ├── EInvoice/                  # Module Hóa đơn điện tử
│   │   │   ├── EInvoiceDashboard.razor
│   │   │   ├── EInvoiceLayout.razor
│   │   │   ├── InvoiceManagement.razor
│   │   │   ├── ProviderManagement.razor
│   │   │   ├── ProviderConfiguration.razor
│   │   │   ├── HealthMonitoring.razor
│   │   │   └── AlertManagement.razor
│   │   └── Admin/                     # Module Quản trị
│   │       ├── TenantManagement.razor
│   │       ├── UserManagement.razor
│   │       ├── PermissionGroupManagement.razor
│   │       └── AuditTrail.razor
│   ├── RedirectToLogin.razor
│   ├── RedirectToAccessDenied.razor
│   ├── VanADashboard.razor
│   └── _Imports.razor
├── Pages/                             # Razor Pages (MVC-style)
│   ├── Index.cshtml
│   ├── Login.cshtml
│   ├── Logout.cshtml
│   ├── GuardRedirect.cshtml
│   ├── Guard/Scan.cshtml
│   ├── Kitchen/Index.cshtml
│   └── WorkflowConfig.cshtml
├── Controllers/                       # API Controllers
│   ├── ProductsController.cs
│   ├── OrdersController.cs
│   ├── OrderWorkflowController.cs
│   ├── CustomerOrdersController.cs
│   ├── CustomerIdentityController.cs
│   ├── SocialCampaignsController.cs
│   ├── LoyaltyController.cs
│   ├── DashboardController.cs
│   ├── ShopsController.cs
│   ├── TenantController.cs
│   ├── UserController.cs
│   ├── PermissionGroupController.cs
│   ├── ApiKeyController.cs
│   ├── NotificationsController.cs
│   └── DevLoginController.cs
├── EInvoice/                          # EInvoice Bounded Context
│   ├── Controllers/
│   │   └── HKDElectronicInvoiceController.cs
│   └── Dtos/
│       ├── CreateInvoiceRequest.cs
│       ├── InvoiceDto.cs
│       ├── InvoiceItemDto.cs
│       ├── InvoiceStatusResponse.cs
│       └── SubmitInvoiceResponse.cs
├── Infrastructure/
│   ├── ShopERPDbContext.cs
│   └── SqliteRetryPolicy.cs
├── Services/
│   ├── Accounting/
│   │   └── AccountingUIService.cs
│   ├── CustomerTokenService.cs
│   ├── ErrorNotificationService.cs
│   ├── HttpContextAuthenticationStateProvider.cs
│   ├── OrderManagementService.cs
│   ├── OrderQueueService.cs
│   ├── OtpService.cs
│   ├── QrCodeService.cs
│   ├── RealtimeDashboardService.cs
│   ├── SimpleOutboxProcessor.cs
│   ├── TenantProvider.cs
│   ├── NatsConnectionFactory.cs
│   └── INatsConnectionFactory.cs
├── Models/
│   └── NavigationItem.cs
└── Program.cs
```

---

## 3. Các trang (Routes) và chức năng

### 3.1 Trang điều hướng & xác thực
| Route | Tên | Mô tả | Phân quyền |
|---|---|---|---|
| `/` | **Home** | Redirect tới `/sitemap` | `[Authorize]` |
| `/sitemap` | **Sitemap** | Trang điều hướng tập trung | `[Authorize]` |
| `/Login` | **Login** | Đăng nhập Razor Page | Anonymous |
| `/Logout` | **Logout** | Đăng xuất | `[Authorize]` |
| `/AccessDenied` | **AccessDenied** | Trang từ chối truy cập | — |

### 3.2 Quản lý Đơn hàng
| Route | Tên | Mô tả | Phân quyền |
|---|---|---|---|
| `/orders` | — | Danh sách đơn hàng | Owner, StoreKeeper |
| `/guard/scan` | **Guard Scan** | Quét QR check-in/check-out | Guard |
| `/kitchen` | **Kitchen** | Màn hình bếp | — |
| `/workflow-config` | **WorkflowConfig** | Cấu hình workflow | — |

### 3.3 Kế toán (Accounting)
| Route | Tên | Mô tả | Phân quyền |
|---|---|---|---|
| `/accounting` | **AccountingIndex** | Dashboard kế toán | Owner |
| `/accounting/revenue` | **RevenueEntry** | Nhập doanh thu | Owner |
| `/accounting/expense` | **ExpenseEntry** | Nhập chi phí | Owner |
| `/accounting/history` | **TransactionHistory** | Lịch sử giao dịch | Owner |
| `/accounting/balance` | **AccountBalance** | Số dư tài khoản | Owner |
| `/accounting/period-closing` | **PeriodClosing** | Đóng kỳ kế toán | Owner |

### 3.4 Hóa đơn điện tử (EInvoice)
| Route | Tên | Mô tả | Phân quyền |
|---|---|---|---|
| `/einvoice` | **EInvoiceDashboard** | Dashboard hóa đơn | StoreManagement |
| `/einvoice/invoices` | **InvoiceManagement** | Quản lý hóa đơn | StoreManagement |
| `/einvoice/providers` | **ProviderManagement** | Quản lý provider | Owner |
| `/einvoice/provider-config` | **ProviderConfiguration** | Cấu hình provider | Owner |
| `/einvoice/health` | **HealthMonitoring** | Giám sát health | StoreManagement |
| `/einvoice/alerts` | **AlertManagement** | Quản lý cảnh báo | StoreManagement |

### 3.5 Quản trị hệ thống (Admin)
| Route | Tên | Mô tả | Phân quyền |
|---|---|---|---|
| `/admin/tenants` | **TenantManagement** | Quản lý tenant + Onboarding | SystemAdmin |
| `/admin/users` | **UserManagement** | Quản lý người dùng | Owner |
| `/admin/permission-groups` | **PermissionGroupManagement** | Nhóm quyền | Owner |
| `/admin/audit-trail` | **AuditTrail** | Nhật ký audit | Admin |

---

## 4. Các nghiệp vụ chính (Business Modules)

### 4.1 Product Catalog Management
- `ProductsController` cung cấp API catalog công khai cho KhachLink
- `GET /api/products` — lấy sản phẩm active theo `shopId` (tenant)
- `GET /api/products/{id}` — lấy chi tiết sản phẩm
- `GET /api/products/recommended` — recommendations cá nhân hóa
- Quản lý sản phẩm trong `ShopERPDbContext.Products`

### 4.2 Order Management
- `OrdersController` quản lý đơn hàng trong ngày và theo trạng thái
- `OrderWorkflowController` xử lý chuyển trạng thái đơn hàng
- `OrderManagementService` cung cấp metrics, dashboard, assign/cancel
- `CustomerOrdersController` — đơn hàng của khách hàng
- `OrderQueueService` — queue xử lý đơn (đã tạm disable)

### 4.3 Kitchen Display System
- Razor Page `/Kitchen/Index` — màn hình hiển thị đơn cho bếp
- Tích hợp SignalR real-time

### 4.4 Guard QR Scan
- Razor Page `/Guard/Scan` — quét QR cho bảo vệ
- Phân quyền `GuardOnly`

### 4.5 Kế toán (Accounting)
- `AccountingUIService` là adapter giữa Blazor UI và `IAccountingService`
- Các chức năng:
  - Nhập doanh thu (`CreateRevenueEntryAsync`)
  - Nhập chi phí (`CreateExpenseEntryAsync`)
  - Xem lịch sử giao dịch
  - Số dư tài khoản
  - Đóng kỳ kế toán
  - Period-over-period comparison
- Domain `AccountingEntry` bất biến (immutable), thay đổi qua Reversal Entry
- Hỗ trợ HKD Book theo TT 152/2025/TT-BTC

### 4.6 Hóa đơn điện tử HKD (EInvoice)
- Bounded Context riêng: `VanAn.ShopERP.EInvoice`
- `HKDElectronicInvoiceController` — API `/api/einvoice`
- ACID: Invoice creation + Revenue recognition + Inventory deduction trong một Unit of Work
- `IEInvoiceOrchestrator` xử lý nghiệp vụ ở CoreHub
- DTOs: `CreateInvoiceRequest`, `InvoiceDto`, `InvoiceStatusResponse`, `SubmitInvoiceResponse`
- Quản lý provider, health monitoring, alerts

### 4.7 Loyalty & Customer
- `LoyaltyController` — `GET /api/loyalty/my` với `X-Customer-Token`
- Tính tier: Bronze → Silver → Gold → Platinum
- `CustomerTokenService` — tạo/validate token bằng `IDataProtector`, TTL 30 ngày
- `CustomerIdentityController` — quản lý identity khách hàng

### 4.8 Social Campaign Management
- `SocialCampaignsController` — API surface cho KhachLink
- `GET /api/socialcampaigns/by-tracking-code/{code}`
- `POST /api/socialcampaigns/record-click/{code}`
- `GET /api/socialcampaigns/by-shop/{shopId}`

### 4.9 Dashboard & Real-time
- `DashboardController` — metrics API cho KhachLink
- `RealtimeDashboardService` — aggregation và broadcast metrics
- SignalR integration cho real-time updates
- Health checks: `/health`, `/health/detail` (yêu cầu `OwnerOnly`)

### 4.10 Tenant & User Management
- `TenantManagement` — CRUD tenant (chỉ SystemAdmin)
- `UserManagement` — quản lý người dùng (Owner)
- `PermissionGroupManagement` — nhóm quyền RBAC (Owner)
- `AuditTrail` — nhật ký audit (Admin)

### 4.11 Notifications & API Keys
- `NotificationsController` — Web Push notifications
- `ApiKeyController` — quản lý API keys cho HMAC signing

### 4.12 Offline-First / Edge Sync
- `ShopERPDbContext` đăng ký như `IVanAnDbContext`
- SQLite WAL mode cho concurrency
- `NatsSyncWorker` — background service đồng bộ với NATS (kích hoạt bằng `--sync-worker`)
- `SimpleOutboxProcessor` — đã tạm disable

### 4.13 Tenant Onboarding (Multi-Industry)
- **UI:** `TenantManagement.razor` — modal "+ Tạo Tenant + Onboarding" với industry selection (F&B enabled)
- **Service:** `TenantOnboardingApiClient` — Gateway API client với SystemAdmin JWT minting
- **Gateway API:** `POST /api/v1/onboarding/tenants` — SystemAdmin Bearer JWT required
- **Orchestrator:** `TenantOnboardingService` (CoreHub) — single-call flow: tenant → owner → role → seed → groups → assignment
- **Seed Strategy:** `FnbSeedStrategy` — 1 shop, 8 products, 12 ingredients, 14 recipes, 12 inventory
- **Permission Groups:** 4 default groups (Quản lý, Thu ngân, Bếp, Kho) with Owner assigned to Quản lý
- **Extensibility:** Generic `IIndustrySeedStrategy` interface for SPA, Hotel, Barber, Clothes, Healthy, Pet Shop
- **Files:**
  - `3_CoreHub/Services/Onboarding/IIndustrySeedStrategy.cs`, `ITenantOnboardingService.cs`
  - `3_CoreHub/Services/Onboarding/TenantOnboardingService.cs`
  - `3_CoreHub/Services/Onboarding/Strategies/FnbSeedStrategy.cs`
  - `3_CoreHub/Services/Onboarding/Dtos/OnboardingDtos.cs`
  - `2_Gateway/Controllers/TenantOnboardingController.cs`
  - `5_WebApps/ShopERP/Services/TenantOnboardingApiClient.cs`
  - `5_WebApps/ShopERP/Components/Pages/Admin/TenantManagement.razor`
  - `6_Tests/VanAn.Integration.Tests/TenantOnboardingIntegrationTests.cs`

---

## 5. Dependency Injection (Program.cs)

### Database & Data Protection
| Service | Lifetime | Mô tả |
|---|---|---|
| `ShopERPDbContext` | Scoped | SQLite DbContext |
| `IVanAnDbContext` → `ShopERPDbContext` | Scoped | Decouple từ VanAnDbContext PostgreSQL |
| `IDataProtectionProvider` | Singleton | File-system key persistence |
| `PiiDataMigrationService` | Scoped | Mã hóa PII cũ |

### CoreHub Services
| Service | Lifetime | Mô tả |
|---|---|---|
| `IShopConfigService` | Scoped | Cấu hình shop |
| `ISocialCampaignService` | Scoped | Social campaign |
| `ILoyaltyRewardsService` | Scoped | Loyalty |
| `IOnboardingService` | Scoped | Onboarding |
| `IVoiceCommandService` | Scoped | Voice command |
| `ICustomerService` | Scoped | Customer management |
| `IOrderService` | Scoped | Order domain service |
| `IOrderWorkflowService` | Scoped | Order workflow |
| `IAccountingService` | Scoped | Accounting entries |
| `IRoleAssignmentService` | Scoped | Role assignment |
| `IPermissionGroupService` | Scoped | Permission groups |
| `IApiKeyManagementService` | Scoped | API keys |
| `IDashboardService` | Scoped | Dashboard metrics |
| `ITenantManagementService` | Scoped | Tenant CRUD |
| `ITenantOnboardingService` | Scoped | Tenant onboarding orchestrator |
| `INatsEventPublisher` | Singleton | NATS event publishing |

### ShopERP-specific Services
| Service | Lifetime | Mô tả |
|---|---|---|
| `IOrderManagementService` | Scoped | Order management adapter |
| `AccountingUIService` | Scoped | Accounting UI adapter |
| `ICustomerTokenService` | Scoped | Customer token |
| `IShopQrCodeService` | Scoped | QR code generation |
| `IErrorNotificationService` | Scoped | Error notification |
| `IHttpContextAccessor` | Scoped | HTTP context access |
| `ITenantProvider` → `HttpContextTenantProvider` | Scoped | Resolve tenant từ claims |
| `AuthenticationStateProvider` → `HttpContextAuthenticationStateProvider` | Scoped | Bridge auth Pages → Blazor |
| `INatsConnectionFactory` | Scoped | NATS connection factory |

### UI Platform
| Service | Lifetime | Mô tả |
|---|---|---|
| `ITenantService` | Scoped | UI Platform tenant |
| `IThemeProvider` | Scoped | UI Platform theme |
| `ICssAdapter` | Scoped | Bootstrap adapter |

### Repositories
| Repository | Lifetime |
|---|---|
| `ICustomerRepository` | Scoped |
| `IOrderRepository` | Scoped |
| `IAccountingEntryRepository` | Scoped |
| `IHKDBookRepository` | Scoped |
| `ILoyaltyRewardsRepository` | Scoped |
| `IApiKeyRepository` | Scoped |
| `IOutboxRepository` | Scoped (chỉ trong `--sync-worker` mode) |

### Authentication & Authorization
- Cookie Authentication (default)
- OpenIdConnect (optional, tích hợp Gateway/Identity)
- Policies:
  - `RequireAuthenticatedUser`
  - `RequireTenantAccess`
  - `OwnerOnly`
  - `StoreManagement` (Owner + StoreKeeper)
  - `GuardOnly`
  - `StaffOrAbove`
  - `SystemAdmin`

### Rate Limiting
- `LoginRateLimit`: 5 requests / minute / IP

---

## 6. API Controllers

| Controller | Route | Chức năng chính |
|---|---|---|
| `ProductsController` | `api/products` | Catalog sản phẩm cho KhachLink |
| `OrdersController` | `api/orders` | Quản lý đơn hàng |
| `OrderWorkflowController` | `api/orderworkflow` | Chuyển trạng thái đơn |
| `CustomerOrdersController` | `api/customerorders` | Đơn hàng của khách |
| `CustomerIdentityController` | `api/customeridentity` | Identity khách hàng |
| `SocialCampaignsController` | `api/socialcampaigns` | Social campaign API |
| `LoyaltyController` | `api/loyalty` | Loyalty dashboard |
| `DashboardController` | `api/dashboard` | Metrics API |
| `ShopsController` | `api/shops` | Quản lý cửa hàng |
| `TenantController` | `api/tenants` | Tenant API |
| `UserController` | `api/users` | User API |
| `PermissionGroupController` | `api/permissiongroups` | Permission group API |
| `ApiKeyController` | `api/apikeys` | API key management |
| `NotificationsController` | `api/notifications` | Web Push notifications |
| `HKDElectronicInvoiceController` | `api/einvoice` | Hóa đơn điện tử HKD |
| `DevLoginController` | `/dev/login` | Chỉ Development — E2E test helper |

---

## 7. Database (SQLite)

### ShopERPDbContext
DbSet bao gồm:
- `Orders`, `OrderItems`
- `Shops`
- `Customers`, `Products`, `Inventories`, `Ingredients`
- `AccountingEntries`, `LoyaltyRewards`, `SocialCampaigns`
- `HKDBooks`, `JournalEntries`, `AuditLogs`
- `PendingInvoiceQueues`
- `Tenants`, `Users`, `UserTenants`, `PermissionGroups`, `UserPermissionGroups`
- `ApiKeys`, `PushSubscriptions`
- `OutboxMessages`

### SQLite Optimizations (WAL mode)
- `PRAGMA journal_mode=WAL;`
- `PRAGMA busy_timeout=30000;`
- `PRAGMA cache_size=10000;`
- `PRAGMA synchronous=NORMAL;`

### Seed Data
- Tự động seed `DemoUser` với BCrypt (work factor 12):
  - `admin@vanan.vn` — Owner
  - `kho@vanan.vn` — StoreKeeper
  - `baove@vanan.vn` — Guard
  - `staff@vanan.vn` — Staff
  - `bep@vanan.vn` — Masterchef
- Default tenant: `00000000-0000-0000-0000-000000000001`

---

## 8. Kiến trúc & Ràng buộc

### Data Flow
```
KhachLink (5002) → Gateway (5001) → ShopERP (5003) → SQLite
                         ↓
              [in-process CoreHub services]
                         ↓
                   PostgreSQL / SQLite / NATS
```

### Nguyên tắc
- ShopERP là **SQLite-only edge node** — không có Npgsql trực tiếp
- ShopERP **KHÔNG** được chứa business logic trong Controllers (thin adapter)
- Tích hợp CoreHub services qua DI
- Multi-tenancy qua `TenantId` filter và `ITenantProvider`

---

## 9. Cấu hình

### Connection String
- Mặc định: `Data Source={BaseDirectory}/vanan_shoperp.db`
- Override: `SQLITE_DB_PATH` environment variable

### ASPNETCORE_URLS
- Mặc định: `http://0.0.0.0:5003`

### Authentication
- Cookie: `.VanAn.Auth`, HttpOnly, SameSite=Strict, Secure=Always
- OIDC Authority: `Authentication:Authority` (default `https://localhost:5001`)

### Data Protection
- Key directory: `DataProtection:KeyDirectory` hoặc `{BaseDirectory}/keys/shoperp`

### Health Checks
- `/health` — public
- `/health/detail` — yêu cầu `OwnerOnly`

---

## 10. Tích hợp UI Platform

- Sử dụng các component: `VanAButton`, `VanACard`, `VanAAlert`, `VanAMetricsCard`, `VanASpinner`, `VanATable`
- Inject `IThemeProvider` + `ITenantProvider` cho theming
- Mobile-first responsive
- Sử dụng design tokens

---

## 11. Bảo mật

- BCrypt password hashing (work factor 12)
- Field-level PII encryption via Data Protection
- Role-based authorization (`Owner`, `StoreKeeper`, `Staff`, `Guard`, `Masterchef`, `SystemAdmin`, `Admin`)
- Rate limiting cho login
- Antiforgery token
- HTTPS redirection trong Production
- Forwarded headers cho nginx reverse proxy

---

## 12. E2E Test Contracts

- `Sitemap.razor` có nhiều `data-testid` cho Playwright:
  - `sitemap-page`, `sitemap-title`, `btn-logout`, `sitemap-grid`
  - `card-orders`, `card-accounting`, `card-einvoice`, `card-khachlink`, `card-admin`, `card-audit`
- `EInvoiceDashboard.razor`: `data-testid="einvoice-dashboard"`, `data-testid="btn-refresh"`, `data-testid="btn-new-invoice"`

---

## 13. Các chế độ chạy

| Chế độ | Lệnh | Mô tả |
|---|---|---|
| **Normal** | `dotnet run` | ShopERP chạy như edge node SQLite |
| **Sync Worker** | `dotnet run -- --sync-worker` | Bật `NatsSyncWorker` để đồng bộ với NATS |

---

## 14. Lưu ý kiến trúc

- `3_CoreHub/VanAn.CoreHub.csproj` hiện đang có `<OutputType>Exe</OutputType>` — vi phạm hard stop rule *"CoreHub MUST remain pure Class Library (.dll)"*. Cần xem xét chuyển về Class Library.
- `OrderQueueService` và `SimpleOutboxProcessor` đã tạm disable trong `Program.cs`.
- `ShopERP` hiện đăng ký `IVanAnDbContext` trỏ tới `ShopERPDbContext` — đúng kiến trúc edge node.
