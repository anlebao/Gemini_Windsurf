# W17-T2 — Loyalty Dashboard (Điểm + Lịch sử + Tier)

**Wave:** 17 — KhachLink Retention & Loyalty
**Branch:** `feature/wave17-khachlink-retention`
**Priority:** 🔴 HIGH — tính năng retention chính, lý do số 1 để giữ app
**Conflict risk:** MEDIUM — tạo Gateway endpoint mới, 1 Razor page mới
**Depends on:** W17-T1 complete (cần CustomerToken)
**Estimated effort:** 1 session

---

## Vấn đề

Sau W17-T1, user có CustomerId và CustomerToken. Backend loyalty đã đầy đủ:
- `LoyaltyRewards` entity: `PointBalance` (int), `History` (JSON string của `LoyaltyHistoryEntry[]`)
- `LoyaltyRewardsService`: `GetCustomerRewardsAsync()`, `AddPointsAsync()`, `SubtractPointsAsync()`
- `LoyaltyRewardsRepository`: query by `CustomerId`
- `LoyaltyHistoryEntry`: `Type` (EARN/SPEND), `Points`, `Reason`, `Timestamp`, `BalanceAfter`
- `OrderWorkflowService.ProcessLoyaltyPointsAsync()`: tự động cộng điểm sau mỗi đơn

**Thiếu duy nhất:** Gateway endpoint + KhachLink UI.

---

## Tier System

| Tier | PointBalance | Badge | Ưu đãi |
|------|-------------|-------|--------|
| 🥉 Bronze | 0 – 999 | `bg-warning` | 1% cashback |
| 🥈 Silver | 1,000 – 4,999 | `bg-secondary` | 2% cashback |
| 🥇 Gold | 5,000 – 19,999 | `bg-warning text-dark` | 3% cashback + ưu tiên |
| 💎 Platinum | 20,000+ | `bg-info` | 5% cashback + 1 item miễn phí/tháng |

> Tier được lưu trong `Customer.CustomerTier` (string). `LoyaltyRewardsService.AddPointsAsync` cộng điểm nhưng **chưa tự update tier** → cần thêm `UpdateTierIfNeeded()` trong `AddPointsAsync` hoặc tính tier on-the-fly từ `PointBalance`.

**Quyết định:** Tính tier on-the-fly trong response DTO — không cần sửa Domain.

---

## Files cần tạo/sửa

### TẠO MỚI: `5_WebApps/ShopERP/Controllers/LoyaltyController.cs`
```csharp
[ApiController]
[Route("api/customers")]
public class LoyaltyController(ILoyaltyRewardsService loyaltyService, ICustomerTokenService tokenService) : ControllerBase
{
    [HttpGet("{customerId}/loyalty")]
    public async Task<IActionResult> GetLoyalty(Guid customerId)
    {
        // Validate token
        var token = Request.Headers["X-Customer-Token"].FirstOrDefault();
        var tokenCustomerId = tokenService.ValidateToken(token ?? "");
        if (tokenCustomerId == null || tokenCustomerId != customerId)
            return Unauthorized(new { error = "Token không hợp lệ" });

        var rewards = await loyaltyService.GetCustomerRewardsAsync(customerId);
        if (rewards == null)
        {
            rewards = await loyaltyService.GetOrCreateCustomerRewardsAsync(customerId);
        }

        var history = DeserializeHistory(rewards.History);
        var tier    = CalcTier(rewards.PointBalance);
        var next    = NextTierThreshold(rewards.PointBalance);

        return Ok(new
        {
            customerId,
            tier        = tier.Name,
            tierBadge   = tier.Badge,
            pointBalance = rewards.PointBalance,
            nextTierPoints = next.Required,
            nextTierName   = next.Name,
            progressPercent = next.Required > 0
                ? (int)Math.Min(100, (double)rewards.PointBalance / next.Required * 100)
                : 100,
            history = history.OrderByDescending(h => h.Timestamp).Take(20)
        });
    }

    private static (string Name, string Badge) CalcTier(int points) => points switch
    {
        >= 20000 => ("Platinum", "bg-info"),
        >= 5000  => ("Gold",     "bg-warning text-dark"),
        >= 1000  => ("Silver",   "bg-secondary"),
        _        => ("Bronze",   "bg-warning")
    };

    private static (int Required, string Name) NextTierThreshold(int points) => points switch
    {
        >= 20000 => (0, "Platinum"),
        >= 5000  => (20000, "Platinum"),
        >= 1000  => (5000, "Gold"),
        _        => (1000, "Silver")
    };

    private static List<LoyaltyHistoryEntry> DeserializeHistory(string json)
    {
        try { return JsonSerializer.Deserialize<List<LoyaltyHistoryEntry>>(json) ?? []; }
        catch { return []; }
    }
}
```

### TẠO MỚI: `2_Gateway/Controllers/LoyaltyController.cs`
```csharp
[ApiController]
[Route("api/customers")]
public class LoyaltyController(IHttpClientFactory factory) : ControllerBase
{
    [HttpGet("{customerId}/loyalty")]
    public async Task<IActionResult> GetLoyalty(Guid customerId)
    {
        var http = factory.CreateClient("shoperp");
        // Forward X-Customer-Token
        if (Request.Headers.TryGetValue("X-Customer-Token", out var token))
            http.DefaultRequestHeaders.TryAddWithoutValidation("X-Customer-Token", token.ToString());

        var resp = await http.GetAsync($"api/customers/{customerId}/loyalty");
        return resp.IsSuccessStatusCode
            ? Ok(await resp.Content.ReadFromJsonAsync<object>())
            : StatusCode((int)resp.StatusCode);
    }
}
```

### TẠO MỚI: `5_WebApps/KhachLink/Pages/LoyaltyCard.razor`
```razor
@page "/my-loyalty"
@inject IHttpClientFactory HttpClientFactory
@inject IJSRuntime JSRuntime
@inject NavigationManager Nav

<PageTitle>Điểm thưởng — Vạn An</PageTitle>

@if (_loading)
{
    <div class="text-center py-5"><div class="spinner-border text-primary"></div></div>
}
else if (_notLoggedIn)
{
    <div class="container py-4">
        <VanAnAlert Variant="AlertVariant.Info">
            Vui lòng <a href="/login?returnUrl=/my-loyalty">đăng nhập</a> để xem điểm thưởng.
        </VanAnAlert>
    </div>
}
else if (_data != null)
{
    <div class="container py-4">
        <!-- Tier Header -->
        <VanAnCard Shadow="true" CssClass="loyalty-header mb-4">
            <div class="d-flex align-items-center gap-3">
                <div class="loyalty-avatar">
                    <i class="fas fa-user-circle fa-3x text-primary"></i>
                </div>
                <div>
                    <span class="badge @_data.TierBadge fs-6">@_data.Tier</span>
                    <div class="text-muted small mt-1">Thành viên Vạn An</div>
                </div>
            </div>
        </VanAnCard>

        <!-- Point Balance -->
        <VanAnCard Shadow="true" CssClass="point-balance-card mb-4 text-center">
            <div class="display-5 fw-bold text-primary">@_data.PointBalance.ToString("N0")</div>
            <div class="text-muted">điểm tích lũy</div>
            @if (_data.NextTierPoints > 0)
            {
                <div class="mt-3 text-muted small">
                    Cần thêm <strong>@((_data.NextTierPoints - _data.PointBalance).ToString("N0"))</strong>
                    điểm để lên <strong>@_data.NextTierName</strong>
                </div>
                <div class="progress mt-2" style="height: 8px;">
                    <div class="progress-bar bg-primary" style="width: @_data.ProgressPercent%"></div>
                </div>
            }
            else
            {
                <VanAnAlert Variant="AlertVariant.Success" CssClass="mt-3">
                    🎉 Bạn đã đạt hạng cao nhất — Platinum!
                </VanAnAlert>
            }
        </VanAnCard>

        <!-- History -->
        <VanAnCard Shadow="true">
            <h5 class="mb-3">📜 Lịch sử giao dịch</h5>
            @if (!_data.History.Any())
            {
                <p class="text-muted text-center py-3">Chưa có giao dịch nào.</p>
            }
            else
            {
                <ul class="list-group list-group-flush">
                    @foreach (var h in _data.History)
                    {
                        <li class="list-group-item d-flex justify-content-between align-items-center">
                            <div>
                                <div class="fw-semibold">@h.Reason</div>
                                <div class="text-muted small">@h.Timestamp.ToString("HH:mm dd/MM/yyyy")</div>
                            </div>
                            <span class="badge @(h.Points > 0 ? "bg-success" : "bg-danger") rounded-pill fs-6">
                                @(h.Points > 0 ? "+" : "")@h.Points đ
                            </span>
                        </li>
                    }
                </ul>
            }
        </VanAnCard>
    </div>
}

@code {
    private LoyaltyData? _data;
    private bool _loading = true;
    private bool _notLoggedIn = false;

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (!firstRender) return;
        var token      = await JSRuntime.InvokeAsync<string>("localStorage.getItem", "customer_token");
        var customerId = await JSRuntime.InvokeAsync<string>("localStorage.getItem", "customer_id");
        if (string.IsNullOrEmpty(token) || string.IsNullOrEmpty(customerId))
        {
            _notLoggedIn = true; _loading = false; StateHasChanged(); return;
        }
        try
        {
            var http = HttpClientFactory.CreateClient("gateway");
            http.DefaultRequestHeaders.TryAddWithoutValidation("X-Customer-Token", token);
            _data = await http.GetFromJsonAsync<LoyaltyData>($"api/customers/{customerId}/loyalty");
        }
        catch { _notLoggedIn = true; }
        _loading = false;
        StateHasChanged();
    }

    private record LoyaltyHistoryItem(int Points, string Reason, DateTime Timestamp);
    private record LoyaltyData(
        string Tier, string TierBadge, int PointBalance,
        int NextTierPoints, string NextTierName, int ProgressPercent,
        List<LoyaltyHistoryItem> History);
}
```

---

## Entry criteria
- [ ] W17-T1 complete — `customer_token` và `customer_id` đã lưu trong localStorage
- [ ] `ILoyaltyRewardsService` registered trong ShopERP DI (đã có — `Program.cs` line 88)

## Success criteria
- [ ] `GET /api/customers/{id}/loyalty` với valid token → 200 + JSON đúng shape
- [ ] `GET /api/customers/{id}/loyalty` với invalid token → 401
- [ ] `LoyaltyCard.razor` tại `/my-loyalty` hiển thị: tier badge, điểm, progress bar, lịch sử 20 entries
- [ ] User chưa login → hiển thị link đăng nhập, không crash
- [ ] Điểm tính đúng sau khi đặt hàng (OrderWorkflowService đã auto-award)
- [ ] `dotnet build VanAn.sln` → 0 errors
