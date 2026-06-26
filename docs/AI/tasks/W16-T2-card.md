# TASK CARD: W16-T2 — Fix Dashboard: Xóa VanAnDashboard + RealTimeDashboard "demo-shop"

## 1. GOAL & CONTEXT
- **Mục tiêu cốt lõi:** Xóa `Components/Pages/VanAnDashboard.razor` (route `/VanAnDashboard` cũ, không còn dùng sau Wave 15 tạo `Pages/Dashboard.razor`). Fix `RealTimeDashboard.razor` thay `"demo-shop"` hardcode bằng `ITenantService.GetCurrentTenantId()`
- **Nghiệp vụ áp dụng:** Dashboard dùng SignalR để hiển thị real-time metrics. Mỗi shop chỉ thấy data của mình — hardcode `"demo-shop"` là lỗ hổng multi-tenancy nghiêm trọng
- **Master plan:** `docs/AI/tasks/KHACHLINK_PRODUCTION_PLAN.md` § W16-T2
- **Depends on:** W15-T1 complete (Wave 15 đã tạo `Pages/Dashboard.razor` tại `/dashboard`)

## 2. ACTIVE WORKFLOW ROUTING
- **Target Workflow:** `newfeaturebuild.md` — IMPLEMENT (đơn giản, scope hẹp)
- **Execution Mode:** IMPLEMENT — 2 thay đổi nhỏ, không cần phase ANALYZE dài

## 3. PHÂN TÍCH HIỆN TRẠNG

### VanAnDashboard.razor — File thừa
```
5_WebApps/KhachLink/Components/Pages/VanAnDashboard.razor
  @page "/VanAnDashboard"           ← route cũ
  @attribute [StreamRendering]
  @inject ILogger<VanAnDashboard> Logger
```
- Route `/VanAnDashboard` đã bị NavMenu.razor cũ reference (`href="VanAnDashboard"`)
- Wave 15-T1 xóa NavMenu references + tạo `Pages/Dashboard.razor` tại `/dashboard`
- Sau Wave 15, `VanAnDashboard.razor` là dead code không có route nào trỏ vào
- **Kết luận:** XÓA file này

### RealTimeDashboard.razor — `"demo-shop"` hardcode
```csharp
// Line 599 — JoinShopGroup()
var shopId = "demo-shop";     // "In production, get actual shop ID" ← TODO từ dev
await _hubConnection.InvokeAsync("JoinShopGroup", shopId);

// Line 625 — RefreshData()
await _hubConnection.InvokeAsync("RequestShopMetrics", "demo-shop");  // ← hardcode thứ 2
```
- `ITenantService` đã available trong KhachLink: `Program.cs` line 49 register `AddScoped<ITenantService, TenantService>()`
- `ITenantService.GetCurrentTenantId()` → `Guid` — dùng `.ToString()` truyền vào SignalR

## 4. QUYẾT ĐỊNH

| Item | Quyết định |
|------|-----------|
| `VanAnDashboard.razor` | XÓA (dead code, route cũ) |
| `"demo-shop"` trong `JoinShopGroup()` | THAY bằng `_tenantService.GetCurrentTenantId().ToString()` |
| `"demo-shop"` trong `RefreshData()` | THAY bằng `_shopId` (field được set tại `JoinShopGroup`) |
| SignalR Hub server-side | KHÔNG sửa (nằm trong CoreHub/Gateway — ngoài scope) |

## 5. RELEVANT FILES

**Files được phép sửa/xóa:**
- `5_WebApps/KhachLink/Components/Pages/VanAnDashboard.razor` (**XÓA**)
- `5_WebApps/KhachLink/Components/Dashboard/RealTimeDashboard.razor` (**SỬA** — inject ITenantService, thay `"demo-shop"`)

**Files đọc để verify:**
- `5_WebApps/KhachLink/Program.cs` (xác nhận ITenantService đã register — ✅ line 49)
- `UI.Platform/Services/ITenantService.cs` (xác nhận `GetCurrentTenantId()` method — ✅ line 9)

**KHÔNG được sửa:**
- `1_Shared/Domain.cs`
- `3_CoreHub/` bất kỳ file nào
- `Pages/Dashboard.razor` (đã tạo ở Wave 15-T1)

## 6. TARGET STATE

### `RealTimeDashboard.razor` — Sau fix

**Thêm inject:**
```razor
@inject VanAn.UI.Platform.Services.ITenantService TenantService
```

**Thêm field:**
```csharp
private string _shopId = string.Empty;
```

**Sửa `JoinShopGroup()`:**
```csharp
// TRƯỚC
private async Task JoinShopGroup()
{
    if (_hubConnection != null && _isConnected)
    {
        var shopId = "demo-shop";
        await _hubConnection.InvokeAsync("JoinShopGroup", shopId);
    }
}

// SAU
private async Task JoinShopGroup()
{
    if (_hubConnection != null && _isConnected)
    {
        _shopId = TenantService.GetCurrentTenantId().ToString();
        await _hubConnection.InvokeAsync("JoinShopGroup", _shopId);
    }
}
```

**Sửa `RefreshData()`:**
```csharp
// TRƯỚC
await _hubConnection.InvokeAsync("RequestShopMetrics", "demo-shop");

// SAU
await _hubConnection.InvokeAsync("RequestShopMetrics", _shopId);
```

## 7. BƯỚC THỰC HIỆN

```
S1: Verify VanAnDashboard.razor không được reference ở đâu
    → Grep 5_WebApps/KhachLink/ cho "VanAnDashboard"
    → Nếu KHÔNG có reference: xóa file

S2: Xóa VanAnDashboard.razor
    → Chỉ xóa file này, không thêm file mới

S3: Sửa RealTimeDashboard.razor
    → Thêm @inject ITenantService TenantService
    → Thêm private string _shopId = string.Empty;
    → Sửa JoinShopGroup() — gán _shopId từ TenantService
    → Sửa RefreshData() — dùng _shopId thay "demo-shop"

S4: Build
    → dotnet build VanAn.sln → 0 errors
    → Đặc biệt verify: VanAnDashboard route `/VanAnDashboard` không còn tồn tại

S5: Anti-pattern check
    → Select-String "demo-shop" trong KhachLink/Components/ → 0 matches

S6: Commit
    → "[W16-T2] Remove VanAnDashboard.razor (dead route), fix RealTimeDashboard TenantId"
```

## 8. SUCCESS CRITERIA
- [ ] **SC1:** File `5_WebApps/KhachLink/Components/Pages/VanAnDashboard.razor` không còn tồn tại
- [ ] **SC2:** Route `/VanAnDashboard` không còn active trong app
- [ ] **SC3:** `RealTimeDashboard.razor` không còn string `"demo-shop"` (0 matches)
- [ ] **SC4:** `RealTimeDashboard.razor` inject `ITenantService` và gọi `GetCurrentTenantId()`
- [ ] **SC5:** `dotnet build VanAn.sln` → 0 errors
- [ ] **SC6:** `guard-check.ps1` → PASS

## 9. VERIFIED FACTS
- Fact 1: `Components/Pages/VanAnDashboard.razor` line 1: `@page "/VanAnDashboard"` — confirmed
- Fact 2: `RealTimeDashboard.razor` line 599: `var shopId = "demo-shop"` — confirmed
- Fact 3: `RealTimeDashboard.razor` line 625: `"RequestShopMetrics", "demo-shop"` — confirmed
- Fact 4: `KhachLink/Program.cs` line 49: `AddScoped<ITenantService, TenantService>()` — confirmed
- Fact 5: `ITenantService.GetCurrentTenantId()` returns `Guid` — confirmed (`UI.Platform/Services/ITenantService.cs` line 9)
- Fact 6: Wave 15-T1 tạo `Pages/Dashboard.razor` tại route `/dashboard` để thay thế `/VanAnDashboard`

## 10. ESTIMATED EFFORT
- Low effort — 1 file xóa, 1 file sửa 3 dòng
- 0.25 session
- **BLOCKER:** Wave 15-T1 phải complete (VanAnDashboard.razor là dead code chỉ sau khi wave 15 done)
