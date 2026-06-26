# W17-T3 — Lịch sử đơn hàng cá nhân

**Wave:** 17 — KhachLink Retention & Loyalty
**Master plan:** `docs/AI/tasks/KHACHLINK_RETENTION_PLAN.md` § T3
**Branch:** `feature/wave17-khachlink-retention`
**Priority:** 🟡 HIGH
**Conflict risk:** LOW — thêm 1 query param vào OrdersController đã có, 1 Razor page mới
**Depends on:** W17-T1 complete (cần CustomerId + CustomerToken)
**Estimated effort:** 0.5 session

---

## Vấn đề

`OrdersController` ở Gateway hiện có:
- `GET /api/orders/{id}` — xem 1 đơn theo ID ✅
- `GET /api/orders?status=` — lọc theo status (dành cho ShopERP staff) ✅
- **Thiếu:** `GET /api/orders?customerId={id}` — lịch sử đơn của 1 khách

`Order` entity có `CustomerDeviceId` (zero-friction) và `CustomerId` (sau khi upgrade identity). Cả hai đều cần được dùng để query lịch sử.

---

## Files cần tạo/sửa

### SỬA: `2_Gateway/Controllers/OrdersController.cs`

Thêm overload vào `GetOrders()` để xử lý `?customerId=`:

```csharp
[HttpGet]
public async Task<ActionResult<IEnumerable<Order>>> GetOrders(
    [FromQuery] string? status = null,
    [FromQuery] Guid? customerId = null,
    [FromQuery] int page = 1,
    [FromQuery] int pageSize = 20)
{
    try
    {
        Guid tenantId = GetTenantId();

        // NEW: customer history query
        if (customerId.HasValue)
        {
            // Validate customer token
            var token = Request.Headers["X-Customer-Token"].FirstOrDefault();
            // Forward to ShopERP — let ShopERP validate token ownership
            var http   = _factory.CreateClient("shoperp");
            http.DefaultRequestHeaders.TryAddWithoutValidation("X-Customer-Token", token ?? "");
            var resp   = await http.GetAsync(
                $"api/orders?customerId={customerId}&tenantId={tenantId}&page={page}&pageSize={pageSize}");
            return resp.IsSuccessStatusCode
                ? Ok(await resp.Content.ReadFromJsonAsync<object>())
                : StatusCode((int)resp.StatusCode);
        }

        // Existing logic unchanged
        if (string.IsNullOrEmpty(status))
        {
            DateTime today = DateTime.UtcNow.Date;
            IEnumerable<Order> orders = await _orderService.GetOrdersByDateRangeAsync(tenantId, today, today.AddDays(1));
            return Ok(orders);
        }
        else
        {
            OrderStatusId statusId = new(status);
            List<Order> orders = await _orderService.GetOrdersByStatusAsync(statusId, tenantId);
            return Ok(orders);
        }
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "Error getting orders");
        return StatusCode(500, "Internal server error");
    }
}
```

> **Note:** Cần inject `IHttpClientFactory` vào `OrdersController` constructor để forward customer queries về ShopERP.

### TẠO MỚI: `5_WebApps/ShopERP/Controllers/CustomerOrdersController.cs`
```csharp
[ApiController]
[Route("api/orders")]
public class CustomerOrdersController(IVanAnDbContext db, ICustomerTokenService tokenService) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetCustomerOrders(
        [FromQuery] Guid customerId,
        [FromQuery] Guid tenantId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        // Validate token
        var token = Request.Headers["X-Customer-Token"].FirstOrDefault();
        var tokenCustomerId = tokenService.ValidateToken(token ?? "");
        if (tokenCustomerId == null || tokenCustomerId != customerId)
            return Unauthorized(new { error = "Token không hợp lệ" });

        var orders = await db.Orders
            .Where(o => o.TenantId.Value == tenantId
                     && (o.CustomerId == customerId))
            .OrderByDescending(o => o.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(o => new
            {
                o.Id,
                Status      = o.Status.Value,
                o.TotalPrice,
                o.CreatedAt,
                ItemCount   = o.Items.Count,
                o.CustomerDeviceId
            })
            .ToListAsync();

        var total = await db.Orders
            .CountAsync(o => o.TenantId.Value == tenantId && o.CustomerId == customerId);

        return Ok(new { orders, totalCount = total, page, pageSize });
    }
}
```

### TẠO MỚI: `5_WebApps/KhachLink/Pages/OrderHistory.razor`
```razor
@page "/my-orders"
@inject IHttpClientFactory HttpClientFactory
@inject IJSRuntime JSRuntime
@inject NavigationManager Nav

<PageTitle>Lịch sử đơn hàng — Vạn An</PageTitle>

<div class="container py-4">
    <h4 class="mb-4">📋 Lịch sử đơn hàng</h4>

    @if (_loading)
    {
        <div class="text-center py-5"><div class="spinner-border text-primary"></div></div>
    }
    else if (_notLoggedIn)
    {
        <VanAnAlert Variant="AlertVariant.Info">
            Vui lòng <a href="/login?returnUrl=/my-orders">đăng nhập</a> để xem lịch sử.
        </VanAnAlert>
    }
    else
    {
        <!-- Filter tabs -->
        <div class="btn-group mb-4 w-100" role="group">
            @foreach (var f in new[] { "Tất cả", "Đang xử lý", "Hoàn thành" })
            {
                <button class="btn @(_filter == f ? "btn-primary" : "btn-outline-primary")"
                        @onclick="@(() => SetFilter(f))">@f</button>
            }
        </div>

        @if (!FilteredOrders.Any())
        {
            <p class="text-muted text-center py-4">Không có đơn hàng nào.</p>
        }
        else
        {
            @foreach (var o in FilteredOrders)
            {
                <VanAnCard Shadow="true" CssClass="mb-3">
                    <div class="d-flex justify-content-between align-items-start">
                        <div>
                            <div class="fw-semibold">#@o.Id.ToString()[..8]</div>
                            <div class="text-muted small">
                                @o.ItemCount sản phẩm · @o.TotalPrice.ToString("N0")đ
                            </div>
                            <div class="text-muted small">@o.CreatedAt.ToString("HH:mm dd/MM/yyyy")</div>
                        </div>
                        <div class="text-end">
                            <span class="badge @GetStatusBadge(o.Status) mb-2">@GetStatusLabel(o.Status)</span>
                            <br />
                            <a href="/order-tracking/@o.Id" class="btn btn-sm btn-outline-primary">Xem</a>
                        </div>
                    </div>
                </VanAnCard>
            }
        }
    }
</div>

@code {
    private List<OrderSummary> _orders = new();
    private bool _loading = true;
    private bool _notLoggedIn = false;
    private string _filter = "Tất cả";

    private IEnumerable<OrderSummary> FilteredOrders => _filter switch
    {
        "Đang xử lý" => _orders.Where(o => o.Status is "pending" or "confirmed" or "processing"),
        "Hoàn thành" => _orders.Where(o => o.Status is "delivered" or "completed"),
        _            => _orders
    };

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (!firstRender) return;
        var token      = await JSRuntime.InvokeAsync<string>("localStorage.getItem", "customer_token");
        var customerId = await JSRuntime.InvokeAsync<string>("localStorage.getItem", "customer_id");
        var tenantId   = await JSRuntime.InvokeAsync<string>("localStorage.getItem", "tenant_id");
        if (string.IsNullOrEmpty(token) || string.IsNullOrEmpty(customerId))
        {
            _notLoggedIn = true; _loading = false; StateHasChanged(); return;
        }
        try
        {
            var http = HttpClientFactory.CreateClient("gateway");
            http.DefaultRequestHeaders.TryAddWithoutValidation("X-Customer-Token", token);
            var result = await http.GetFromJsonAsync<OrderHistoryResponse>(
                $"api/orders?customerId={customerId}&tenantId={tenantId}");
            _orders = result?.Orders ?? new();
        }
        catch { _notLoggedIn = true; }
        _loading = false;
        StateHasChanged();
    }

    private void SetFilter(string f) { _filter = f; StateHasChanged(); }

    private static string GetStatusBadge(string status) => status switch
    {
        "pending" or "confirmed" or "processing" => "bg-warning text-dark",
        "delivered" or "completed"               => "bg-success",
        "cancelled"                              => "bg-danger",
        _                                        => "bg-secondary"
    };

    private static string GetStatusLabel(string status) => status switch
    {
        "pending"    => "Chờ xác nhận",
        "confirmed"  => "Đã xác nhận",
        "processing" => "Đang pha chế",
        "delivered"  => "Đã giao",
        "completed"  => "Hoàn thành",
        "cancelled"  => "Đã hủy",
        _            => status
    };

    private record OrderSummary(Guid Id, string Status, decimal TotalPrice, DateTime CreatedAt, int ItemCount);
    private record OrderHistoryResponse(List<OrderSummary> Orders, int TotalCount, int Page, int PageSize);
}
```

---

## Entry criteria
- [ ] W17-T1 complete
- [ ] `Order.CustomerId` populated sau khi user đã verify OTP

## Success criteria
- [ ] `GET /api/orders?customerId={id}` với valid token → 200 + JSON list
- [ ] `GET /api/orders?customerId={id}` với invalid token → 401
- [ ] `OrderHistory.razor` tại `/my-orders` hiển thị đúng list, filter hoạt động
- [ ] Click "Xem" navigate đến `/order-tracking/{id}` đã có từ Wave 15
- [ ] User chưa login → hiển thị prompt, không crash
- [ ] `dotnet build VanAn.sln` → 0 errors
