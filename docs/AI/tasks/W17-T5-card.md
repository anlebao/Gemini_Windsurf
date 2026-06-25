# W17-T5 — Store Finder (Fix Google Maps key + Multi-shop + Lat/Lng Domain)

**Wave:** 17 — KhachLink Retention & Loyalty
**Branch:** `feature/wave17-khachlink-retention`
**Priority:** 🟢 MEDIUM — độc lập, không block T1-T4
**Conflict risk:** MEDIUM — Domain modification approved (Latitude/Longitude trên Shop entity)
**Depends on:** Không có dependency
**Estimated effort:** 0.5 session

---

## Quyết định kiến trúc đã được approve

**APPROVED:** Thêm `Latitude` và `Longitude` vào `Shop` entity trong `1_Shared/Domain.cs`.

**Lý do:** `ShopConfig` record có `Latitude/Longitude` nhưng là config record (per-request), không phải persistent entity. Store Finder cần query địa lý từ database → cần field trên `Shop` entity.

---

## Hiện trạng

| Vấn đề | File | Chi tiết |
|--------|------|---------|
| Google Maps API key hardcode | `Components/GoogleMaps.razor` line 59 | `AIzaSyDummyKey` → bản đồ không load |
| 1 cửa hàng cố định | `GoogleMaps.razor` | Hiển thị tọa độ từ `ShopConfig` của 1 shop |
| Không có Store Finder page | — | Không có `/stores` route |
| Không có `GET /api/shops` endpoint | — | Không có cách lấy danh sách shops |
| `Shop` entity thiếu Lat/Lng | `1_Shared/Domain.cs` | Chỉ có `Address` string |

---

## Files cần tạo/sửa

### SỬA: `1_Shared/Domain.cs` — Shop entity (APPROVED Domain modification)

```csharp
// THÊM 2 fields vào class Shop (sau IsActive):
public double? Latitude  { get; protected set; }
public double? Longitude { get; protected set; }

// THÊM vào constructor Shop(TenantId, string, string, string, string):
// Không cần — default null là đúng

// THÊM business method:
public void SetCoordinates(double latitude, double longitude)
{
    Latitude  = latitude;
    Longitude = longitude;
    UpdateAudit();
}

// SỬA UpdateShopDetails — thêm Lat/Lng params:
public void UpdateShopDetails(string name, string address, string phone,
    string email, bool isActive, double? latitude = null, double? longitude = null)
{
    Name     = name;
    Address  = address;
    Phone    = phone;
    Email    = email;
    IsActive = isActive;
    if (latitude.HasValue && longitude.HasValue)
        SetCoordinates(latitude.Value, longitude.Value);
    UpdateAudit();
}
```

### SỬA: `Components/GoogleMaps.razor`

```razor
<!-- TRƯỚC -->
@inject IJSRuntime JSRuntime

<!-- SAU — thêm IConfiguration inject -->
@inject IJSRuntime JSRuntime
@inject IConfiguration Configuration
```

```csharp
// TRƯỚC
return $"https://www.google.com/maps/embed/v1/place?key=AIzaSyDummyKey&q={lat},{lng}&zoom=16&maptype=roadmap&language=vi";

// SAU
var apiKey = Configuration["GoogleMaps:ApiKey"] ?? "";
return $"https://www.google.com/maps/embed/v1/place?key={apiKey}&q={lat},{lng}&zoom=16&maptype=roadmap&language=vi";
```

### TẠO MỚI: `5_WebApps/ShopERP/Controllers/ShopsController.cs`
```csharp
[ApiController]
[Route("api/shops")]
public class ShopsController(IVanAnDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetShops(
        [FromQuery] Guid tenantId,
        [FromQuery] double? lat  = null,
        [FromQuery] double? lng  = null,
        [FromQuery] double radius = 10.0)
    {
        if (tenantId == Guid.Empty)
            return BadRequest("tenantId required");

        var shops = await db.Shops
            .Where(s => s.TenantId.Value == tenantId && s.IsActive)
            .Select(s => new
            {
                shopId   = s.Id,
                s.Name,
                s.Address,
                s.Phone,
                latitude  = s.Latitude,
                longitude = s.Longitude,
                acceptsRedemption = true // Wave 18: per-shop config
            })
            .ToListAsync();

        // Client-side distance filter nếu có lat/lng
        if (lat.HasValue && lng.HasValue)
        {
            shops = shops
                .Where(s => s.latitude.HasValue && s.longitude.HasValue)
                .Select(s => new
                {
                    s.shopId, s.Name, s.Address, s.Phone,
                    s.latitude, s.longitude, s.acceptsRedemption,
                    distanceKm = HaversineKm(lat.Value, lng.Value, s.latitude!.Value, s.longitude!.Value)
                })
                .Where(s => s.distanceKm <= radius)
                .OrderBy(s => s.distanceKm)
                .Cast<object>()
                .ToList()!;
        }

        return Ok(shops);
    }

    // Haversine formula — tính khoảng cách km
    private static double HaversineKm(double lat1, double lon1, double lat2, double lon2)
    {
        const double R = 6371;
        var dLat = (lat2 - lat1) * Math.PI / 180;
        var dLon = (lon2 - lon1) * Math.PI / 180;
        var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2)
              + Math.Cos(lat1 * Math.PI / 180) * Math.Cos(lat2 * Math.PI / 180)
              * Math.Sin(dLon / 2) * Math.Sin(dLon / 2);
        return R * 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
    }
}
```

### TẠO MỚI: `2_Gateway/Controllers/ShopsController.cs`
```csharp
[ApiController]
[Route("api/shops")]
public class ShopsController(IHttpClientFactory factory) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetShops(
        [FromQuery] Guid tenantId,
        [FromQuery] double? lat   = null,
        [FromQuery] double? lng   = null,
        [FromQuery] double radius = 10.0)
    {
        var http = factory.CreateClient("shoperp");
        var url  = $"api/shops?tenantId={tenantId}&radius={radius}";
        if (lat.HasValue) url += $"&lat={lat}&lng={lng}";
        var resp = await http.GetAsync(url);
        return resp.IsSuccessStatusCode
            ? Ok(await resp.Content.ReadFromJsonAsync<object>())
            : StatusCode((int)resp.StatusCode);
    }
}
```

### TẠO MỚI: `5_WebApps/KhachLink/Pages/StoreFinder.razor`
```razor
@page "/stores"
@inject IHttpClientFactory HttpClientFactory
@inject IJSRuntime JSRuntime
@inject IConfiguration Configuration

<PageTitle>Tìm cửa hàng — Vạn An</PageTitle>

<div class="container py-4">
    <h4 class="mb-4">📍 Tìm cửa hàng gần bạn</h4>

    @if (_loading)
    {
        <div class="text-center py-4"><div class="spinner-border text-primary"></div></div>
    }
    else if (!_shops.Any())
    {
        <VanAnAlert Variant="AlertVariant.Info">Không tìm thấy cửa hàng nào.</VanAnAlert>
    }
    else
    {
        @foreach (var shop in _shops)
        {
            <VanAnCard Shadow="true" CssClass="mb-3">
                <div class="d-flex justify-content-between align-items-start">
                    <div>
                        <div class="fw-semibold">📍 @shop.Name</div>
                        <div class="text-muted small">@shop.Address</div>
                        @if (shop.DistanceKm.HasValue)
                        {
                            <div class="text-muted small">@shop.DistanceKm.Value.ToString("F1") km</div>
                        }
                        @if (shop.AcceptsRedemption)
                        {
                            <span class="badge bg-success mt-1">⭐ Chấp nhận quy đổi điểm</span>
                        }
                    </div>
                    <div class="d-flex flex-column gap-2">
                        @if (shop.Latitude.HasValue && shop.Longitude.HasValue)
                        {
                            <a href="https://maps.google.com/?q=@shop.Latitude,@shop.Longitude"
                               target="_blank" class="btn btn-sm btn-outline-primary">
                                Đường đi
                            </a>
                        }
                        <a href="/" class="btn btn-sm btn-primary">Đặt hàng</a>
                    </div>
                </div>
            </VanAnCard>
        }
    }
</div>

@code {
    private List<ShopInfo> _shops = new();
    private bool _loading = true;

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (!firstRender) return;
        try
        {
            var tenantId = await JSRuntime.InvokeAsync<string>("localStorage.getItem", "tenant_id")
                           ?? Guid.Empty.ToString();
            double? lat = null, lng = null;
            try
            {
                var pos = await JSRuntime.InvokeAsync<GeoPosition>("vananPWA.getCurrentPosition");
                lat = pos?.Latitude; lng = pos?.Longitude;
            }
            catch { /* geolocation denied or unavailable */ }

            var http = HttpClientFactory.CreateClient("gateway");
            var url  = $"api/shops?tenantId={tenantId}";
            if (lat.HasValue) url += $"&lat={lat}&lng={lng}&radius=10";
            _shops = await http.GetFromJsonAsync<List<ShopInfo>>(url) ?? new();
        }
        catch { /* show empty state */ }
        _loading = false;
        StateHasChanged();
    }

    private record GeoPosition(double Latitude, double Longitude);
    private record ShopInfo(
        Guid ShopId, string Name, string Address, string Phone,
        double? Latitude, double? Longitude,
        bool AcceptsRedemption, double? DistanceKm);
}
```

### Config: `appsettings.json` (chỉ thêm placeholder — key thật vào User Secrets)
```json
"GoogleMaps": {
    "ApiKey": ""
}
```

---

## EF Migration cần thiết

Sau khi thêm `Latitude/Longitude` vào `Shop` entity:
```bash
dotnet ef migrations add AddShopCoordinates --project 3_CoreHub --startup-project 5_WebApps/ShopERP
dotnet ef database update --project 3_CoreHub --startup-project 5_WebApps/ShopERP
```

---

## Entry criteria
- [ ] Domain modification approved ✅ (approved trong session này)
- [ ] Google Maps API key có sẵn (lưu User Secrets)

## Success criteria
- [ ] `Shop` entity có `Latitude`, `Longitude`, `SetCoordinates()`, updated `UpdateShopDetails()`
- [ ] EF Migration tạo thành công, database updated
- [ ] `GET /api/shops?tenantId={id}` → 200 + shop list
- [ ] `GET /api/shops?tenantId={id}&lat=10.77&lng=106.69&radius=5` → filtered + sorted by distance
- [ ] `StoreFinder.razor` tại `/stores` hiển thị danh sách shop
- [ ] Google Maps embed load (không còn `AIzaSyDummyKey`)
- [ ] `dotnet build VanAn.sln` → 0 errors
- [ ] `VanAn.Architecture.Tests` → 7/7 PASS (Domain change không break architecture rules)
