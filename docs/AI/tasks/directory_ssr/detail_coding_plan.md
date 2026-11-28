# Detail Coding Plan — Directory SSR

**Created:** 2026-08-23
**Status:** AWAITING REVIEW
**Master plan:** `docs/AI/tasks/directory_ssr/master_plan.md`
**Task card:** `docs/AI/tasks/directory_ssr/task_card.md`

> **Không implement cho đến khi user approve.** File này chỉ là coding plan chi tiết.

---

## Phase A — Project Scaffold + Layout (Day 1)

### A1: `5_WebApps/Directory/VanAn.Directory.csproj`

```xml
<Project Sdk="Microsoft.NET.Sdk.Web">
  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <TreatWarningsAsErrors>false</TreatWarningsAsErrors>
  </PropertyGroup>
  <ItemGroup>
    <ProjectReference Include="..\..\1_Shared\VanAn.Shared.csproj" />
    <ProjectReference Include="..\..\UI.Platform\VanAn.UI.Platform.csproj" />
  </ItemGroup>
</Project>
```

**Lý do:**
- `Microsoft.NET.Sdk.Web` — Blazor SSR cần ASP.NET host (khác KhachLink dùng `BlazorWebAssembly`)
- ProjectReference UI.Platform + Shared — reuse components + DTOs
- KHÔNG ProjectReference CoreHub hoặc Gateway — Directory chỉ gọi Gateway qua HttpClient, không inject DbContext

### A2: `5_WebApps/Directory/Program.cs`

```csharp
using Microsoft.AspNetCore.Components;
using VanAn.Directory.Services;

var builder = WebApplication.CreateBuilder(args);

// Blazor SSR (.NET 8 interactive server render mode)
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

// HttpClient cho Gateway API calls
builder.Services.AddHttpClient<InstanceConfigService>(client =>
{
    client.BaseAddress = new Uri(builder.Configuration["Gateway:BaseUrl"] ?? "http://localhost:5001/");
});
builder.Services.AddHttpClient<ShopConfigService>(client =>
{
    client.BaseAddress = new Uri(builder.Configuration["Gateway:BaseUrl"] ?? "http://localhost:5001/");
});
builder.Services.AddHttpClient<CatalogService>(client =>
{
    client.BaseAddress = new Uri(builder.Configuration["Gateway:BaseUrl"] ?? "http://localhost:5001/");
});

// IMemoryCache — cache instance config + store data 5 phút
builder.Services.AddMemoryCache();

var app = builder.Build();

app.UseStaticFiles();
app.UseAntiforgery();

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

// Health check endpoint cho Docker + nginx
app.MapGet("/health", () => Results.Ok(new { status = "Healthy", service = "VanAn Directory SSR" }));

app.Run();
```

**Lý do:**
- `AddRazorComponents` + `AddInteractiveServerComponents` — Blazor SSR .NET 8
- 3 typed HttpClient (InstanceConfig, ShopConfig, Catalog) — clean separation
- `AddMemoryCache` — cache Gateway API responses 5 phút (tránh call mỗi request)
- `/health` endpoint — Docker healthcheck + nginx upstream check

### A3: `5_WebApps/Directory/Components/App.razor`

```razor
@using VanAn.Directory.Components.Layout
<!DOCTYPE html>
<html lang="vi">
<head>
    <meta charset="utf-8" />
    <meta name="viewport" content="width=device-width, initial-scale=1.0" />
    <base href="/" />
    <link rel="stylesheet" href="bootstrap/bootstrap.min.css" />
    <link rel="stylesheet" href="https://cdn.jsdelivr.net/npm/bootstrap-icons@1.11.3/font/bootstrap-icons.min.css" />
    <link rel="stylesheet" href="app.css" />
    <link rel="stylesheet" href="VanAn.Directory.styles.css" />
    <link rel="stylesheet" href="/lib/leaflet/leaflet.css" />
    <link rel="icon" type="image/png" href="favicon.png" />
    <HeadOutlet />
</head>
<body>
    <Routes @rendermode="InteractiveServer" />
    <script src="/lib/leaflet/leaflet.js"></script>
    <script src="/js/directory-map.js"></script>
    <script src="_framework/blazor.web.js"></script>
</body>
</html>
```

**Lý do:**
- `<HeadOutlet />` — Blazor SSR head content (PageTitle, meta tags)
- `@rendermode="InteractiveServer"` — interactive mode cho search/map interaction
- Leaflet JS load client-side — map render trong browser, không cần server
- `directory-map.js` — custom JS cho Leaflet init (adapt từ KhachLink/leaflet.js)

### A4: `5_WebApps/Directory/Components/Routes.razor`

```razor
@using VanAn.Directory.Components.Pages
<Router AppAssembly="typeof(Program).Assembly">
    <Found Context="routeData">
        <RouteView RouteData="routeData" DefaultLayout="typeof(DirectoryLayout)" />
        <FocusOnNavigate RouteData="routeData" Selector="h1" />
    </Found>
    <NotFound>
        <LayoutView Layout="typeof(DirectoryLayout)">
            <h1>404 — Không tìm thấy trang</h1>
            <p>Trang bạn tìm không tồn tại.</p>
            <a href="/">Về trang chủ</a>
        </LayoutView>
    </NotFound>
</Router>
```

### A5: `5_WebApps/Directory/Services/InstanceConfigService.cs`

```csharp
using Microsoft.Extensions.Caching.Memory;
using System.Text.Json;
using VanAn.Shared.Domain;

namespace VanAn.Directory.Services;

/// <summary>
/// Fetch KhachLink instance config from Gateway by-domain endpoint.
/// Server-side cache (IMemoryCache, 5 min TTL) — thay thế localStorage cache của KhachLink WASM.
/// </summary>
public class InstanceConfigService
{
    private readonly HttpClient _httpClient;
    private readonly IMemoryCache _cache;
    private readonly ILogger<InstanceConfigService> _logger;
    private static readonly TimeSpan CacheTtl = TimeSpan.FromMinutes(5);

    public InstanceConfigService(HttpClient httpClient, IMemoryCache cache, ILogger<InstanceConfigService> logger)
    {
        _httpClient = httpClient;
        _cache = cache;
        _logger = logger;
    }

    /// <summary>
    /// Get instance config by domain. Returns null if not found or feature flag OFF.
    /// Caches result 5 phút keyed by domain.
    /// </summary>
    public async Task<DirectoryInstanceConfig?> GetByDomainAsync(string domain)
    {
        if (string.IsNullOrWhiteSpace(domain))
            return null;

        var cacheKey = $"instance:{domain}";
        if (_cache.TryGetValue(cacheKey, out DirectoryInstanceConfig? cached))
            return cached;

        try
        {
            var resp = await _httpClient.GetAsync($"/api/v1/khachlink-instances/by-domain/{Uri.EscapeDataString(domain)}");
            if (!resp.IsSuccessStatusCode)
            {
                if (resp.StatusCode == System.Net.HttpStatusCode.NotFound)
                    _logger.LogDebug("GetByDomainAsync: 404 for {Domain} (not registered)", domain);
                return null;
            }

            var dto = await resp.Content.ReadFromJsonAsync<KhachLinkInstanceDto>();
            if (dto == null) return null;

            var config = new DirectoryInstanceConfig
            {
                Profile = dto.Profile,
                IsActive = dto.IsActive,
                OwnerTenantId = dto.OwnerTenantId,
                Theme = dto.Theme,
                LogoUrl = dto.LogoUrl,
                NavColor = dto.NavColor,
                HeaderColor = dto.HeaderColor,
                FooterColor = dto.FooterColor
            };

            _cache.Set(cacheKey, config, CacheTtl);
            return config;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "GetByDomainAsync: error fetching instance config for {Domain}", domain);
            return null;
        }
    }
}

public class DirectoryInstanceConfig
{
    public KhachLinkProfile Profile { get; set; } = KhachLinkProfile.FullCommerce;
    public bool IsActive { get; set; } = true;
    public Guid? OwnerTenantId { get; set; }
    public string? Theme { get; set; }
    public string? LogoUrl { get; set; }
    public string? NavColor { get; set; }
    public string? HeaderColor { get; set; }
    public string? FooterColor { get; set; }
}

// DTO matching Gateway KhachLinkInstanceDto (2_Gateway/Controllers/KhachLinkInstanceController.cs L265-283)
public class KhachLinkInstanceDto
{
    public Guid Id { get; set; }
    public string Label { get; set; } = string.Empty;
    public KhachLinkProfile Profile { get; set; }
    public string CustomDomain { get; set; } = string.Empty;
    public Guid? OwnerTenantId { get; set; }
    public bool IsActive { get; set; }
    public string? Theme { get; set; }
    public string? LogoUrl { get; set; }
    public string? NavColor { get; set; }
    public string? HeaderColor { get; set; }
    public string? FooterColor { get; set; }
}
```

**Lý do:**
- `IMemoryCache` 5 phút — thay thế `localStorage` cache của KhachLink WASM (không có localStorage trên server)
- Typed `HttpClient` — DI inject, BaseAddress set trong Program.cs
- DTO mirror Gateway `KhachLinkInstanceDto` — cùng field names cho JSON deserialization
- Không cần `KhachLinkNavFlagsDto` — Directory layout hardcode NavFlags (chỉ Home + StoreFinder + Profile)

### A6: `5_WebApps/Directory/Services/ShopConfigService.cs`

```csharp
using Microsoft.Extensions.Caching.Memory;
using VanAn.Shared.Domain;

namespace VanAn.Directory.Services;

/// <summary>
/// Fetch ShopConfig from Gateway by tenant ID.
/// Server-side cache (IMemoryCache, 5 min TTL).
/// Adapt từ KhachLink ShopConfigHttpService — bỏ IHttpClientFactory, dùng typed HttpClient.
/// </summary>
public class ShopConfigService
{
    private readonly HttpClient _httpClient;
    private readonly IMemoryCache _cache;
    private readonly ILogger<ShopConfigService> _logger;
    private static readonly TimeSpan CacheTtl = TimeSpan.FromMinutes(5);

    public ShopConfigService(HttpClient httpClient, IMemoryCache cache, ILogger<ShopConfigService> logger)
    {
        _httpClient = httpClient;
        _cache = cache;
        _logger = logger;
    }

    public static ShopConfig DefaultShopConfig => new();

    /// <summary>
    /// Get ShopConfig by tenant ID. Calls GET /api/tenants/{tenantId}/store-info.
    /// Returns DefaultShopConfig on 404 or error.
    /// </summary>
    public async Task<ShopConfig> GetByTenantIdAsync(Guid tenantId)
    {
        if (tenantId == Guid.Empty)
            return DefaultShopConfig;

        var cacheKey = $"shopconfig:{tenantId}";
        if (_cache.TryGetValue(cacheKey, out ShopConfig? cached))
            return cached!;

        try
        {
            var resp = await _httpClient.GetAsync($"api/tenants/{tenantId}/store-info");
            if (!resp.IsSuccessStatusCode)
            {
                _logger.LogDebug("GetByTenantIdAsync: {Status} for tenant {TenantId}", resp.StatusCode, tenantId);
                return DefaultShopConfig;
            }

            var store = await resp.Content.ReadFromJsonAsync<TenantStoreDto>();
            if (store == null) return DefaultShopConfig;

            var config = BuildShopConfigFromStore(store);
            _cache.Set(cacheKey, config, CacheTtl);
            return config;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "GetByTenantIdAsync: error for tenant {TenantId}", tenantId);
            return DefaultShopConfig;
        }
    }

    private static ShopConfig BuildShopConfigFromStore(TenantStoreDto store)
    {
        return DefaultShopConfig with
        {
            TenantId = store.Id,
            ShopName = string.IsNullOrWhiteSpace(store.Name) ? DefaultShopConfig.ShopName : store.Name,
            Address = store.Address,
            Phone = store.Phone,
            Email = store.Email,
            Latitude = store.Latitude,
            Longitude = store.Longitude,
            SocialLinksFb = store.SocialLinksFb,
            SocialLinksTiktok = store.SocialLinksTiktok,
            ActiveTheme = store.Theme,
            NavColor = store.NavColor,
            HeaderColor = store.HeaderColor,
            FooterColor = store.FooterColor,
            LogoUrl = !string.IsNullOrWhiteSpace(store.LogoUrl) && Uri.TryCreate(store.LogoUrl, UriKind.Absolute, out var logoUri)
                ? logoUri
                : DefaultShopConfig.LogoUrl
        };
    }
}

// DTO matching Gateway TenantStoreDto (2_Gateway/Controllers/TenantStoreController.cs L289-316)
public class TenantStoreDto
{
    public Guid Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public string Address { get; init; } = string.Empty;
    public string Phone { get; init; } = string.Empty;
    public string Email { get; init; } = string.Empty;
    public double? Latitude { get; init; }
    public double? Longitude { get; init; }
    public double? DistanceKm { get; init; }
    public string? Slug { get; init; }
    public string? SocialLinksFb { get; init; }
    public string? SocialLinksTiktok { get; init; }
    public string? BrandStory { get; init; }
    public string? LogoUrl { get; init; }
    public ThemeType Theme { get; init; } = ThemeType.Classic;
    public string? NavColor { get; init; }
    public string? HeaderColor { get; init; }
    public string? FooterColor { get; init; }
    public string? KhachLinkDomain { get; init; }
}
```

**Lý do:**
- Adapt từ `ShopConfigHttpService` — cùng logic BuildShopConfigFromStore
- Dùng `TenantStoreDto` (Gateway DTO) thay vì `ShopDto` (KhachLink WASM DTO) — Gateway trả TenantStoreDto cho `/api/tenants/{id}/store-info`
- `ShopConfig` record từ `1_Shared/Domain.cs` L2009 — reuse hoàn toàn

### A7: `5_WebApps/Directory/Services/CatalogService.cs`

```csharp
using Microsoft.Extensions.Caching.Memory;

namespace VanAn.Directory.Services;

/// <summary>
/// Fetch store directory from Gateway.
/// GET /api/tenants/search?name=&lat=&lng= — search by name/location
/// GET /api/tenants/nearby?lat=&lng=&radiusKm= — nearby stores
/// </summary>
public class CatalogService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<CatalogService> _logger;

    public CatalogService(HttpClient httpClient, ILogger<CatalogService> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    /// <summary>Search stores by name. Returns empty list on error.</summary>
    public async Task<List<TenantStoreDto>> SearchStoresAsync(string? name, double? lat = null, double? lng = null)
    {
        var query = $"api/tenants/search?";
        if (!string.IsNullOrWhiteSpace(name))
            query += $"name={Uri.EscapeDataString(name)}&";
        if (lat.HasValue && lng.HasValue)
            query += $"lat={lat}&lng={lng}&";
        query = query.TrimEnd('&', '?');

        try
        {
            var resp = await _httpClient.GetAsync(query);
            if (!resp.IsSuccessStatusCode)
            {
                _logger.LogWarning("SearchStoresAsync: {Status}", resp.StatusCode);
                return [];
            }
            return await resp.Content.ReadFromJsonAsync<List<TenantStoreDto>>() ?? [];
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "SearchStoresAsync: error");
            return [];
        }
    }

    /// <summary>Find nearby stores by user location.</summary>
    public async Task<List<TenantStoreDto>> GetNearbyStoresAsync(double lat, double lng, int radiusKm = 5)
    {
        try
        {
            var resp = await _httpClient.GetAsync($"api/tenants/nearby?lat={lat}&lng={lng}&radiusKm={radiusKm}");
            if (!resp.IsSuccessStatusCode)
            {
                _logger.LogWarning("GetNearbyStoresAsync: {Status}", resp.StatusCode);
                return [];
            }
            return await resp.Content.ReadFromJsonAsync<List<TenantStoreDto>>() ?? [];
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "GetNearbyStoresAsync: error");
            return [];
        }
    }
}
```

**Lý do:**
- 2 method: SearchStoresAsync (Home page) + GetNearbyStoresAsync (StoreFinder page)
- Không cache — search/nearby results dynamic, cache gây stale data
- Trả empty list on error — graceful degradation (page render "Không tìm thấy cửa hàng")

### A8: `5_WebApps/Directory/Components/Layout/DirectoryLayout.razor`

**Adapt từ `KhachLinkLayout.razor`** — thay đổi:
- Bỏ `IJSRuntime`, `PWAService`, `KhachLinkInstanceHttpService` (client-side services)
- Thay bằng `InstanceConfigService`, `ShopConfigService` (server-side)
- Bỏ PWA install button (Directory không cần PWA)
- NavFlags hardcode cho Directory: chỉ ShowHome + ShowStores + ShowProfile
- Giữ theme logic (CSS variables, style override, theme class)
- Giữ header + footer layout

```razor
@using VanAn.UI.Platform.Components.Atomic
@using VanAn.UI.Platform.Tokens
@using VanAn.Shared.Domain
@using VanAn.Directory.Services
@inject InstanceConfigService InstanceService
@inject ShopConfigService ShopConfigService
@inject IHttpContextAccessor HttpContextAccessor

<HeadContent>
    <style>
        :root {
            --shop-primary: @(_shopConfig?.PrimaryColor ?? "#8B4513");
            --shop-secondary: @(_shopConfig?.SecondaryColor ?? "#D2691E");
            --shop-nav-color: @(_instanceStyle?.NavColor ?? _shopConfig?.NavColor ?? "#1a1a2e");
            --shop-header-color: @(_instanceStyle?.HeaderColor ?? _shopConfig?.HeaderColor ?? (_shopConfig?.PrimaryColor ?? "#8B4513"));
            --shop-footer-color: @(_instanceStyle?.FooterColor ?? _shopConfig?.FooterColor ?? "#f5f5f5");
        }
    </style>
</HeadContent>

@if (_isInstanceDisabled)
{
    <div class="khachlink-disabled-page">
        <div class="disabled-card">
            <i class="bi bi-pause-circle-fill disabled-icon"></i>
            <h1>Instance tạm ngưng</h1>
            <p>Instance KhachLink cho domain này hiện đang bị vô hiệu hoá.</p>
        </div>
    </div>
}
else
{
<div class="khachlink-layout theme-@GetThemeClass()">
    <!-- Hero Header -->
    <header class="khachlink-hero-header">
        <div class="hero-content d-flex justify-content-between align-items-center px-3 py-2">
            <div class="d-flex align-items-center gap-2">
                @if (!string.IsNullOrEmpty(_instanceStyle?.LogoUrl))
                {
                    <img src="@_instanceStyle.LogoUrl" alt="Logo" class="shop-logo" />
                }
                else if (_shopConfig?.LogoUrl != null)
                {
                    <img src="@_shopConfig.LogoUrl" alt="@_shopConfig.ShopName" class="shop-logo" />
                }
                else
                {
                    <i class="bi bi-shop text-white" style="font-size:1.5rem;"></i>
                }
            </div>
            <div class="d-flex gap-2 align-items-center">
                <!-- Directory nav: chỉ Home + Stores -->
                <a href="/" class="header-icon-btn" aria-label="Trang chủ" title="Trang chủ">
                    <i class="bi bi-house text-white"></i>
                </a>
                <a href="/stores" class="header-icon-btn" aria-label="Tìm cửa hàng" title="Tìm cửa hàng">
                    <i class="bi bi-geo-alt text-white"></i>
                </a>
            </div>
        </div>
    </header>

    <!-- Main Content -->
    <main class="khachlink-main">
        @ChildContent
    </main>

    <!-- Footer -->
    <footer class="khachlink-footer mt-4">
        <div class="text-center py-3 px-3">
            <div class="fw-semibold">@(_shopConfig?.ShopName ?? "Danh bạ Vạn An")</div>
            <div class="mt-2 small">
                <a href="/privacy-policy.html" class="footer-link" target="_blank" rel="noopener">Chính sách Quyền riêng tư</a>
            </div>
        </div>
    </footer>
</div>
}

@code {
    private ShopConfig? _shopConfig;
    private bool _isInstanceDisabled;
    private DirectoryInstanceConfig? _instanceStyle;
    private Guid _currentTenantId;

    protected override async Task OnInitializedAsync()
    {
        // Get domain from HTTP context (server-side — không cần JS interop)
        var httpContext = HttpContextAccessor.HttpContext;
        var domain = httpContext?.Request.Host.Host ?? "";

        if (string.IsNullOrWhiteSpace(domain))
            return;

        // Fetch instance config from Gateway
        var instanceConfig = await InstanceService.GetByDomainAsync(domain);
        if (instanceConfig != null)
        {
            if (!instanceConfig.IsActive)
            {
                _isInstanceDisabled = true;
                return;
            }

            _instanceStyle = instanceConfig;
            if (instanceConfig.OwnerTenantId != null && instanceConfig.OwnerTenantId != Guid.Empty)
            {
                _currentTenantId = instanceConfig.OwnerTenantId.Value;
                _shopConfig = await ShopConfigService.GetByTenantIdAsync(_currentTenantId);
            }
        }
    }

    private string GetThemeClass()
    {
        if (!string.IsNullOrEmpty(_instanceStyle?.Theme)
            && Enum.TryParse<ThemeType>(_instanceStyle.Theme, true, out var instanceTheme))
        {
            return instanceTheme switch
            {
                ThemeType.Teen    => "teen",
                ThemeType.Lady    => "lady",
                ThemeType.Premium => "premium",
                ThemeType.Modern  => "modern",
                _                 => "classic"
            };
        }
        return (_shopConfig?.ActiveTheme ?? ThemeType.Classic) switch
        {
            ThemeType.Teen    => "teen",
            ThemeType.Lady    => "lady",
            ThemeType.Premium => "premium",
            ThemeType.Modern  => "modern",
            _                 => "classic"
        };
    }
}
```

**CSS:** Copy từ `KhachLinkLayout.razor` L139-216 (không thay đổi).

**Lý do thay đổi:**
- `IHttpContextAccessor` — lấy domain từ `Request.Host.Host` (server-side), thay vì `IJSRuntime.eval("window.location.hostname")` (client-side)
- Bỏ `CascadingValue NavFlags` — Directory hardcode nav (Home + Stores only)
- Bỏ PWA install button, cart, rewards, missions, loyalty, profile icons — Directory không có
- Giữ theme + style override logic identical — admin config vẫn hoạt động

---

## Phase B — Pages (Day 2-3)

### B1: Copy static assets

```bash
# Copy từ KhachLink wwwroot
cp 5_WebApps/KhachLink/wwwroot/bootstrap/ 5_WebApps/Directory/wwwroot/bootstrap/
cp 5_WebApps/KhachLink/wwwroot/app.css 5_WebApps/Directory/wwwroot/app.css
cp 5_WebApps/KhachLink/wwwroot/lib/leaflet/ 5_WebApps/Directory/wwwroot/lib/leaflet/
cp 5_WebApps/KhachLink/wwwroot/favicon.png 5_WebApps/Directory/wwwroot/favicon.png
```

**Không copy:**
- `_framework/` — Blazor SSR không serve WASM files
- `js/qr-scanner.js`, `js/cart-animation.js`, `js/fingerprint.js`, etc. — Directory không dùng
- `service-worker.js`, `service-worker-assets.js` — Directory không phải PWA (Phase đầu)
- `manifest.json` — defer PWA

### B2: `5_WebApps/Directory/Components/Pages/Home.razor`

**Adapt từ `KhachLink/Pages/Home.razor`** — simplify:
- Bỏ CartDrawer, commerce mode badge, campaigns, featured products, recently viewed
- Giữ: store directory search box + store list
- Search gọi `CatalogService.SearchStoresAsync(name)` — server-side render results

```razor
@page "/"
@using VanAn.Directory.Services
@using VanAn.UI.Platform.Components.Atomic
@inject CatalogService CatalogService
@inject NavigationManager Navigation

<PageTitle>Danh bạ Vạn An — Tìm cửa hàng gần bạn</PageTitle>

<DirectoryLayout>
    <div class="container py-4">
        <!-- Hero -->
        <div class="text-center mb-4">
            <i class="bi bi-geo-alt-fill text-primary" style="font-size: 2.5rem;"></i>
            <h3 class="mt-2 mb-1">Tìm cửa hàng gần bạn</h3>
            <p class="text-muted mb-0">Tìm theo tên, sản phẩm, hoặc vị trí</p>
        </div>

        <!-- Search box -->
        <VanAnCard CssClass="mb-4 py-4">
            <div class="input-group input-group-lg">
                <span class="input-group-text bg-white border-end-0">
                    <i class="bi bi-search text-primary"></i>
                </span>
                <input type="text" class="form-control border-start-0 border-end-0"
                       @bind="_searchKeyword"
                       @onkeyup="OnSearchKeyUp"
                       placeholder="Tìm theo tên cửa hàng, sản phẩm, dịch vụ..."
                       aria-label="Tìm cửa hàng" />
                @if (!string.IsNullOrWhiteSpace(_searchKeyword))
                {
                    <button class="btn btn-outline-secondary" type="button" @onclick="ClearSearch">
                        <i class="bi bi-x-circle"></i>
                    </button>
                }
            </div>
            <div class="d-flex gap-2 mt-3">
                <VanAnButton Variant="ButtonVariant.Primary" OnClick="SearchStores">
                    <i class="bi bi-search me-1"></i> Tìm kiếm
                </VanAnButton>
                <VanAnButton Variant="ButtonVariant.Outline" OnClick="GoToStoreFinder">
                    <i class="bi bi-geo-alt me-1"></i> Dùng vị trí của tôi
                </VanAnButton>
            </div>
        </VanAnCard>

        <!-- Results -->
        @if (_loading)
        {
            <div class="text-center py-4">
                <div class="spinner-border text-primary" role="status"></div>
                <p class="mt-2">Đang tải...</p>
            </div>
        }
        else if (_stores.Count == 0)
        {
            <div class="text-center py-4 text-muted">
                <i class="bi bi-inbox" style="font-size: 2rem;"></i>
                <p class="mt-2">Không tìm thấy cửa hàng nào.</p>
            </div>
        }
        else
        {
            <div class="row g-3">
                @foreach (var store in _stores)
                {
                    <div class="col-md-6 col-lg-4">
                        <VanAnCard CssClass="h-100 store-card">
                            <div class="d-flex align-items-start gap-3">
                                @if (!string.IsNullOrEmpty(store.LogoUrl))
                                {
                                    <img src="@store.LogoUrl" alt="@store.Name" class="store-logo-sm" />
                                }
                                else
                                {
                                    <i class="bi bi-shop text-primary" style="font-size: 2rem;"></i>
                                }
                                <div class="flex-grow-1">
                                    <h5 class="mb-1">@store.Name</h5>
                                    @if (!string.IsNullOrEmpty(store.Address))
                                    {
                                        <p class="small text-muted mb-1">
                                            <i class="bi bi-geo-alt"></i> @store.Address
                                        </p>
                                    }
                                    @if (!string.IsNullOrEmpty(store.Phone))
                                    {
                                        <p class="small mb-1">
                                            <a href="tel:@store.Phone" class="text-decoration-none">
                                                <i class="bi bi-telephone"></i> @store.Phone
                                            </a>
                                        </p>
                                    }
                                    @if (store.DistanceKm.HasValue)
                                    {
                                        <p class="small text-primary mb-0">
                                            <i class="bi bi-signpost"></i> @store.DistanceKm.Value.ToString("F1") km
                                        </p>
                                    }
                                </div>
                            </div>
                        </VanAnCard>
                    </div>
                }
            </div>
        }
    </div>
</DirectoryLayout>

@code {
    private string _searchKeyword = "";
    private List<TenantStoreDto> _stores = new();
    private bool _loading;

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender)
        {
            await LoadAllStoresAsync();
        }
    }

    private async Task LoadAllStoresAsync()
    {
        _loading = true;
        _stores = await CatalogService.SearchStoresAsync(null);
        _loading = false;
    }

    private async Task SearchStores()
    {
        _loading = true;
        _stores = await CatalogService.SearchStoresAsync(_searchKeyword);
        _loading = false;
    }

    private async Task OnSearchKeyUp(Microsoft.AspNetCore.Components.Web.KeyboardEventArgs e)
    {
        if (e.Key == "Enter")
            await SearchStores();
    }

    private void ClearSearch()
    {
        _searchKeyword = "";
        _ = LoadAllStoresAsync();
    }

    private void GoToStoreFinder()
    {
        Navigation.NavigateTo("/stores");
    }
}
```

### B3: `5_WebApps/Directory/Components/Pages/StoreFinder.razor`

**Adapt từ `KhachLink/Pages/StoreFinder.razor`** — SSR + Leaflet client-side:

```razor
@page "/stores"
@using VanAn.Directory.Services
@using VanAn.UI.Platform.Components.Atomic
@inject CatalogService CatalogService
@inject IJSRuntime JSRuntime
@inject NavigationManager Navigation

<PageTitle>Tìm cửa hàng gần bạn — Danh bạ Vạn An</PageTitle>

<DirectoryLayout>
    <div class="container py-4">
        <div class="text-center mb-4">
            <h1 class="display-5 fw-bold mb-2">
                <i class="bi bi-geo-alt-fill"></i> Tìm cửa hàng gần bạn
            </h1>
            <p class="text-muted mb-0">Khám phá các cửa hàng trong khu vực của bạn</p>
        </div>

        <!-- Search + location controls -->
        <VanAnCard CssClass="mb-4">
            <div class="input-group input-group-lg mb-3">
                <span class="input-group-text bg-white border-end-0">
                    <i class="bi bi-search text-primary"></i>
                </span>
                <input type="text" class="form-control border-start-0"
                       @bind="_searchKeyword"
                       @onkeyup="OnSearchKeyUp"
                       placeholder="Tìm theo tên cửa hàng..." />
            </div>
            <div class="d-flex gap-2 flex-wrap">
                <VanAnButton Variant="ButtonVariant.Primary" OnClick="UseMyLocation">
                    <i class="bi bi-geo-alt me-1"></i> Dùng vị trí của tôi
                </VanAnButton>
                <VanAnButton Variant="ButtonVariant.Outline" OnClick="LoadAllStores">
                    <i class="bi bi-list me-1"></i> Tất cả cửa hàng
                </VanAnButton>
                <div class="ms-auto d-flex align-items-center gap-2">
                    <label class="small text-muted mb-0">Bán kính:</label>
                    <select class="form-select form-select-sm" style="width: auto;" @bind="_radiusKm">
                        <option value="2">2 km</option>
                        <option value="5">5 km</option>
                        <option value="10">10 km</option>
                    </select>
                </div>
            </div>
        </VanAnCard>

        <!-- Map -->
        <div id="directory-map" class="mb-4" style="height: 400px; border-radius: 12px; overflow: hidden;"></div>

        <!-- Store list -->
        @if (_loading)
        {
            <div class="text-center py-4">
                <div class="spinner-border text-primary" role="status"></div>
            </div>
        }
        else if (_stores.Count == 0)
        {
            <div class="text-center py-4 text-muted">
                <i class="bi bi-inbox" style="font-size: 2rem;"></i>
                <p class="mt-2">Không tìm thấy cửa hàng nào trong khu vực.</p>
            </div>
        }
        else
        {
            <div class="row g-3">
                @foreach (var store in _stores)
                {
                    <div class="col-md-6 col-lg-4">
                        <VanAnCard CssClass="h-100">
                            <div class="d-flex align-items-start gap-3">
                                @if (!string.IsNullOrEmpty(store.LogoUrl))
                                {
                                    <img src="@store.LogoUrl" alt="@store.Name" class="store-logo-sm" />
                                }
                                else
                                {
                                    <i class="bi bi-shop text-primary" style="font-size: 2rem;"></i>
                                }
                                <div class="flex-grow-1">
                                    <h6 class="mb-1">@store.Name</h6>
                                    @if (!string.IsNullOrEmpty(store.Address))
                                    {
                                        <p class="small text-muted mb-1"><i class="bi bi-geo-alt"></i> @store.Address</p>
                                    }
                                    @if (store.DistanceKm.HasValue)
                                    {
                                        <p class="small text-primary mb-0">@store.DistanceKm.Value.ToString("F1") km</p>
                                    }
                                    @if (store.Latitude.HasValue && store.Longitude.HasValue)
                                    {
                                        <a href="https://maps.google.com/?q=@store.Latitude,@store.Longitude"
                                           target="_blank" class="small text-decoration-none">
                                            <i class="bi bi-map"></i> Chỉ đường
                                        </a>
                                    }
                                </div>
                            </div>
                        </VanAnCard>
                    </div>
                }
            </div>
        }
    </div>
</DirectoryLayout>

@code {
    private string _searchKeyword = "";
    private int _radiusKm = 5;
    private List<TenantStoreDto> _stores = new();
    private bool _loading;
    private double? _userLat;
    private double? _userLng;

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender)
        {
            await LoadAllStores();
        }
    }

    private async Task LoadAllStores()
    {
        _loading = true;
        _stores = await CatalogService.SearchStoresAsync(null);
        _loading = false;
        await RenderMapAsync();
    }

    private async Task UseMyLocation()
    {
        try
        {
            // JS interop — get geolocation from browser
            var coords = await JSRuntime.InvokeAsync<UserCoords>("directoryMap.getUserLocation");
            _userLat = coords.Lat;
            _userLng = coords.Lng;
            _loading = true;
            _stores = await CatalogService.GetNearbyStoresAsync(coords.Lat, coords.Lng, _radiusKm);
            _loading = false;
            await RenderMapAsync();
        }
        catch
        {
            // Geolocation denied — load all stores
            await LoadAllStores();
        }
    }

    private async Task OnSearchKeyUp(Microsoft.AspNetCore.Components.Web.KeyboardEventArgs e)
    {
        if (e.Key == "Enter")
        {
            _loading = true;
            _stores = await CatalogService.SearchStoresAsync(_searchKeyword, _userLat, _userLng);
            _loading = false;
            await RenderMapAsync();
        }
    }

    private async Task RenderMapAsync()
    {
        var storesWithCoords = _stores
            .Where(s => s.Latitude.HasValue && s.Longitude.HasValue)
            .Select(s => new { s.Name, s.Latitude, s.Longitude, s.Address })
            .ToList();
        await JSRuntime.InvokeVoidAsync("directoryMap.render", "directory-map", storesWithCoords, _userLat, _userLng);
    }

    private record UserCoords(double Lat, double Lng);
}
```

### B4: `5_WebApps/Directory/wwwroot/js/directory-map.js`

```javascript
// Directory SSR — Leaflet map helper
// Adapt từ KhachLink/wwwroot/js/leaflet.js — simplify cho Directory (no markers clustering)

window.directoryMap = (function () {
    let map = null;

    function init(elementId) {
        if (map) { map.remove(); map = null; }
        map = L.map(elementId).setView([10.7626, 106.6603], 12); // HCM default
        L.tileLayer('https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png', {
            attribution: '© OpenStreetMap'
        }).addTo(map);
    }

    function render(elementId, stores, userLat, userLng) {
        init(elementId);
        if (userLat && userLng) {
            L.marker([userLat, userLng]).addTo(map).bindPopup('Vị trí của bạn');
            map.setView([userLat, userLng], 14);
        }
        stores.forEach(s => {
            if (s.latitude && s.longitude) {
                L.marker([s.latitude, s.longitude]).addTo(map)
                    .bindPopup(`<strong>${s.name}</strong><br>${s.address || ''}`);
            }
        });
    }

    function getUserLocation() {
        return new Promise((resolve, reject) => {
            if (!navigator.geolocation) { reject('No geolocation'); return; }
            navigator.geolocation.getCurrentPosition(
                pos => resolve({ lat: pos.coords.latitude, lng: pos.coords.longitude }),
                err => reject(err),
                { timeout: 10000 }
            );
        });
    }

    return { render, getUserLocation };
})();
```

---

## Phase C — Docker + nginx + CI/CD (Day 3-4)

### C1: `5_WebApps/Directory/Dockerfile`

```dockerfile
# syntax=docker/dockerfile:1
# Blazor SSR — ASP.NET runtime (not nginx static like KhachLink WASM)

# Build stage
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
ENV NUGET_PACKAGES=/root/.nuget/packages

COPY ["VanAn.sln", "./"]
COPY ["Directory.Packages.props", "./"]
COPY ["Directory.Build.props", "./"]
COPY ["1_Shared/VanAn.Shared.csproj", "1_Shared/"]
COPY ["UI.Platform/VanAn.UI.Platform.csproj", "UI.Platform/"]
COPY ["5_WebApps/Directory/VanAn.Directory.csproj", "5_WebApps/Directory/"]
RUN dotnet restore "5_WebApps/Directory/VanAn.Directory.csproj"

COPY . .
WORKDIR "/src/5_WebApps/Directory"
RUN dotnet publish "VanAn.Directory.csproj" -c Release -o /app/publish

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 8080

RUN apt-get update && apt-get install -y curl && rm -rf /var/lib/apt/lists/*
ENV ASPNETCORE_URLS=http://+:8080

COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "VanAn.Directory.dll"]
HEALTHCHECK --interval=30s --timeout=10s --retries=3 --start-period=10s \
  CMD curl -f http://localhost:8080/health || exit 1
```

**Lý do:**
- `aspnet:8.0` runtime (không phải nginx) — Blazor SSR cần .NET host
- Port 8080 (không conflict với KhachLink port 80)
- `curl` cho healthcheck
- Chỉ copy csproj cần thiết (Directory + Shared + UI.Platform) — không copy Gateway/CoreHub/ShopERP/KhachLink

### C2: `docker-compose.directory.yml`

```yaml
version: '3.8'

# Directory SSR container — deploy on KhachLink VPS
# Serves Directory profile domains (timlathay.com, etc.)
# Commerce profile domains served by vanan-khachlink-1 (nginx + WASM)

services:
  directory:
    image: ${IMAGE_PREFIX:-ghcr.io/anlebao}/vanan-directory:${IMAGE_TAG:-latest}
    environment:
      - ASPNETCORE_ENVIRONMENT=Production
      - ASPNETCORE_URLS=http://+:8080
      - Gateway__BaseUrl=http://${GATEWAY_REMOTE_HOST}:80/
    networks:
      - vanan-network
    ports:
      - "8080:8080"
    healthcheck:
      test: ["CMD-SHELL", "curl -f http://127.0.0.1:8080/health || exit 1"]
      interval: 30s
      timeout: 10s
      retries: 5
      start_period: 10s
    restart: unless-stopped
    deploy:
      resources:
        limits:
          memory: 256m
    logging:
      driver: "json-file"
      options:
        max-size: "10m"
        max-file: "3"

networks:
  vanan-network:
    driver: bridge
```

### C3: Update `5_WebApps/KhachLink/nginx.conf`

Thêm Directory routing **trước** existing server block:

```nginx
# Directory domain detection — map file (reload khi thêm domain)
# Updated by scripts/add-directory-domain.sh
map $host $is_directory {
    default 0;
    # timlathay.com 1;  # Uncomment khi deploy Directory
}

# Directory upstream — proxy to vanan-directory container
upstream directory_backend {
    server vanan-directory:8080;
}

# Existing server block — thêm Directory check
server {
    listen 80;
    server_name _;

    # Directory domains → proxy to SSR container
    if ($is_directory) {
        proxy_pass http://directory_backend;
        break;
    }

    # Default: serve WASM static files (existing behavior)
    root /usr/share/nginx/html;
    index index.html;
    # ... rest of existing config unchanged
}
```

**Lý do:**
- `map` directive — O(1) lookup, không if-else chain
- `upstream` — nginx health check + retry
- `if ($is_directory)` — redirect Directory domains to SSR container
- Default — Commerce domains serve WASM như cũ
- Map file ban đầu comment out `timlathay.com` — enable khi Directory deploy thành công

### C4: `scripts/add-directory-domain.sh`

```bash
#!/bin/bash
# Add Directory domain to nginx map + reload
# Usage: ./add-directory-domain.sh <domain>
# Checks Gateway API — domain must have KhachLinkProfile.Directory

DOMAIN="$1"
NGINX_CONF="/etc/nginx/conf.d/default.conf"
GATEWAY_URL="${GATEWAY_URL:-http://vanan-gateway-1:80}"

if [ -z "$DOMAIN" ]; then
    echo "Usage: $0 <domain>"
    exit 1
fi

# Verify domain is Directory profile via Gateway API
PROFILE=$(curl -s "$GATEWAY_URL/api/v1/khachlink-instances/by-domain/$DOMAIN" | jq -r '.profile // empty')

if [ "$PROFILE" != "Directory" ]; then
    echo "Error: $DOMAIN is not Directory profile (got: $PROFILE)"
    exit 1
fi

# Add to nginx map (uncomment or add line)
if grep -q "# $DOMAIN 1;" "$NGINX_CONF"; then
    sed -i "s|# $DOMAIN 1;|$DOMAIN 1;|" "$NGINX_CONF"
elif ! grep -q "$DOMAIN 1;" "$NGINX_CONF"; then
    sed -i "/default 0;/a\\    $DOMAIN 1;" "$NGINX_CONF"
fi

# Reload nginx
nginx -t && nginx -s reload
echo "Added $DOMAIN to Directory routing. nginx reloaded."
```

### C5: Update `docker-compose.khachlink.yml`

Thêm vanan-directory service vào existing compose file:

```yaml
# Thêm vào services: section
  directory:
    image: ${IMAGE_PREFIX:-ghcr.io/anlebao}/vanan-directory:${IMAGE_TAG:-latest}
    environment:
      - ASPNETCORE_ENVIRONMENT=Production
      - ASPNETCORE_URLS=http://+:8080
      - Gateway__BaseUrl=http://${GATEWAY_REMOTE_HOST}:80/
    networks:
      - vanan-network
    ports:
      - "8080:8080"
    healthcheck:
      test: ["CMD-SHELL", "curl -f http://127.0.0.1:8080/health || exit 1"]
      interval: 30s
      timeout: 10s
      retries: 5
      start_period: 10s
    restart: unless-stopped
    deploy:
      resources:
        limits:
          memory: 256m
    logging:
      driver: "json-file"
      options:
        max-size: "10m"
        max-file: "3"
```

### C6: Update `.github/workflows/cd-multivps.yml`

**Thêm Build & push Directory** (sau KhachLink build, ~L103):

```yaml
      - name: Build & push Directory
        uses: docker/build-push-action@v5
        with:
          context: .
          file: 5_WebApps/Directory/Dockerfile
          push: true
          no-cache: true
          tags: |
            ${{ env.REGISTRY }}/${{ github.repository_owner }}/vanan-directory:${{ steps.meta.outputs.version }}
            ${{ env.REGISTRY }}/${{ github.repository_owner }}/vanan-directory:latest
          labels: ${{ steps.meta.outputs.labels }}
```

**Deploy Directory** — tích hợp vào existing `deploy-khachlink` job (cùng VPS):
- `docker compose -f docker-compose.khachlink.yml pull directory`
- `docker compose -f docker-compose.khachlink.yml up -d directory`

### C7: Update `VanAn.sln`

```bash
dotnet sln VanAn.sln add 5_WebApps/Directory/VanAn.Directory.csproj
```

---

## Phase D — RV (Day 4)

### D1-D2: Build + CI

```bash
dotnet build VanAn.sln -c Release
# CI pipeline: build + unit tests + integration tests PASS
```

### D3: Deploy

```bash
# CD pipeline deploy vanan-directory to KhachLink VPS
# Verify:
gcloud compute ssh vanan-khachlink --zone=asia-southeast1-c --command="docker ps | grep directory"
```

### D4: Load time

```bash
# Directory domain (sau khi enable trong nginx map)
curl -o /dev/null -w '%{time_total}s' http://timlathay.com/
# Target: <3s
```

### D5-D6: Page render

```bash
# Home page
curl -s http://timlathay.com/ | grep -o '<h[1-6].*Danh bạ.*</h[1-6]>'
# StoreFinder
curl -s http://timlathay.com/stores | grep -o '<h[1-6].*Tìm cửa hàng.*</h[1-6]>'
```

### D7: Commerce unaffected

```bash
# Commerce domain vẫn serve WASM
curl -o /dev/null -w '%{http_code}' http://diemthuong2.khachvip.online/_framework/blazor.webassembly.js
# Target: 200
```

### D8: Memory

```bash
gcloud compute ssh vanan-khachlink --zone=asia-southeast1-c --command="docker stats vanan-directory-1 --no-stream"
# Target: <256MB
```

---

## File summary

### Files mới (12 files)

| File | Purpose | Lines (est.) |
|---|---|---|
| `5_WebApps/Directory/VanAn.Directory.csproj` | Project file | 15 |
| `5_WebApps/Directory/Program.cs` | Bootstrap + DI | 35 |
| `5_WebApps/Directory/Dockerfile` | Multi-stage build | 25 |
| `5_WebApps/Directory/Components/App.razor` | Root component | 20 |
| `5_WebApps/Directory/Components/Routes.razor` | Router | 15 |
| `5_WebApps/Directory/Components/Layout/DirectoryLayout.razor` | Layout (adapt KhachLinkLayout) | 150 |
| `5_WebApps/Directory/Components/Pages/Home.razor` | Home page | 120 |
| `5_WebApps/Directory/Components/Pages/StoreFinder.razor` | StoreFinder page | 130 |
| `5_WebApps/Directory/Services/InstanceConfigService.cs` | Instance config service | 80 |
| `5_WebApps/Directory/Services/ShopConfigService.cs` | ShopConfig service | 90 |
| `5_WebApps/Directory/Services/CatalogService.cs` | Catalog service | 60 |
| `5_WebApps/Directory/wwwroot/js/directory-map.js` | Leaflet map helper | 40 |
| `docker-compose.directory.yml` | Docker compose | 30 |
| `scripts/add-directory-domain.sh` | Domain management script | 35 |

### Files sửa (4 files)

| File | Change |
|---|---|
| `5_WebApps/KhachLink/nginx.conf` | Thêm Directory routing (map + upstream + if) |
| `docker-compose.khachlink.yml` | Thêm vanan-directory service |
| `.github/workflows/cd-multivps.yml` | Thêm Build & push Directory + deploy |
| `VanAn.sln` | Thêm VanAn.Directory.csproj |

### Static assets copy (4 items)

| Source | Destination |
|---|---|
| `KhachLink/wwwroot/bootstrap/` | `Directory/wwwroot/bootstrap/` |
| `KhachLink/wwwroot/app.css` | `Directory/wwwroot/app.css` |
| `KhachLink/wwwroot/lib/leaflet/` | `Directory/wwwroot/lib/leaflet/` |
| `KhachLink/wwwroot/favicon.png` | `Directory/wwwroot/favicon.png` |

---

## Open questions (need user input before implement)

1. **timlathay.com domain** — hiện đã có KhachLinkInstance với profile Directory? Cần verify trước khi enable nginx routing.
2. **Gateway__BaseUrl** — Directory container gọi Gateway qua VPC internal IP (`http://10.148.0.x:80`) hay qua domain (`https://api.vanan.cloud`)? VPC IP nhanh hơn + không cần SSL.
3. **CSS isolation** — `VanAn.Directory.styles.css` auto-generated bởi Blazor, cần ensure UI Platform components CSS load đúng.
4. **Bootstrap icons** — hiện load từ CDN (`cdn.jsdelivr.net`). Directory SSR có nên bundle local thay vì CDN? (CDN faster first load, local reliable offline).
