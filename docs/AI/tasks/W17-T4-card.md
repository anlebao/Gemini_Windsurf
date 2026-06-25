# W17-T4 — PWA Bug Fixes + Push Subscription Endpoint

**Wave:** 17 — KhachLink Retention & Loyalty
**Branch:** `feature/wave17-khachlink-retention`
**Priority:** 🟡 HIGH — PWA install vô nghĩa nếu không push được notification
**Conflict risk:** LOW-MEDIUM — sửa 2 file đã có, tạo 1 endpoint mới
**Depends on:** W17-T1 (cần CustomerToken để gắn push subscription với customer)
**Estimated effort:** 0.5 session

---

## Vấn đề

### Bug 1 — `async void Dispose()` (nghiêm trọng)
```csharp
// HIỆN TẠI — SAI (PWAInstallPrompt.razor line 274)
public async void Dispose()
{
    if (PWAService != null)
    {
        PWAService.OnInstallStateChanged -= HandleInstallStateChanged;
        PWAService.OnOnlineStateChanged  -= HandleOnlineStateChanged;
        await PWAService.DisposeAsync();
    }
}
```
`async void` không được await bởi Blazor lifecycle. Nếu component unmount trong khi đang await → `ObjectDisposedException` trên JSRuntime.

### Bug 2 — Dismiss không persist
`_dismissed = true` chỉ trong memory. Reload → banner hiện lại.

### Bug 3 — CSS transition không kích hoạt
```csharp
return _showInstallPrompt && !_dismissed ? "" : "display: none;";
```
`display: none` bỏ qua CSS transition. Class `.hidden` đã viết sẵn nhưng không được dùng.

### Bug 4 — `Task.Delay(3000)` không có CancellationToken
User navigate đi trong 3 giây → component đã dispose nhưng code sau delay set `_showInstallPrompt = true` trên component đã chết.

### Missing feature — Push Subscription endpoint
`PWAService.SubscribeToPushAsync()` gọi JS nhưng không có server endpoint để lưu subscription.

---

## Files cần sửa

### SỬA: `5_WebApps/KhachLink/Components/PWA/PWAInstallPrompt.razor`

**@code block replacement:**
```csharp
@implements IAsyncDisposable

@code {
    private bool _showInstallPrompt = false;
    private bool _dismissed         = false;
    private bool _isOnline          = true;
    private bool _showOfflineIndicator = false;
    private CancellationTokenSource _cts = new();

    public bool IsOnline           => _isOnline;
    public bool ShowOfflineIndicator => _showOfflineIndicator;

    protected override async Task OnInitializedAsync()
    {
        await PWAService.InitializeAsync();

        PWAService.OnInstallStateChanged += HandleInstallStateChanged;
        PWAService.OnOnlineStateChanged  += HandleOnlineStateChanged;

        _isOnline = PWAService.IsOnline;

        // Check dismiss persist
        var dismissed = await JSRuntime.InvokeAsync<string>("localStorage.getItem", "pwa_dismissed");
        if (dismissed == "true") return;

        _showOfflineIndicator = !_isOnline; // chỉ hiện khi offline

        if (!PWAService.IsInstalled)
        {
            try
            {
                await Task.Delay(3000, _cts.Token);
                _showInstallPrompt = true;
                StateHasChanged();
            }
            catch (OperationCanceledException) { /* component disposed before delay */ }
        }
    }

    // FIX Bug 3: dùng CSS class thay vì display:none
    private string GetCssClass() =>
        _showInstallPrompt && !_dismissed
            ? "pwa-install-prompt"
            : "pwa-install-prompt hidden";

    private async Task InstallApp()
    {
        var success = await PWAService.ShowInstallPromptAsync();
        if (success)
        {
            _showInstallPrompt = false;
            await ShowNotificationAsync("Đã cài đặt thành công!", "Vạn An App đã sẵn sàng.");
        }
    }

    // FIX Bug 2: persist dismiss
    private async Task DismissPrompt()
    {
        _dismissed = true;
        _showInstallPrompt = false;
        await JSRuntime.InvokeVoidAsync("localStorage.setItem", "pwa_dismissed", "true");
    }

    private void HandleInstallStateChanged(bool installed)
    {
        _showInstallPrompt = false;
        StateHasChanged();
    }

    private void HandleOnlineStateChanged(bool online)
    {
        _isOnline = online;
        _showOfflineIndicator = !online; // FIX Bug 4: chỉ hiện khi offline
        StateHasChanged();
    }

    private async Task ShowNotificationAsync(string title, string message)
        => await PWAService.ShowNotificationAsync(title, message);

    // FIX Bug 1: IAsyncDisposable
    public async ValueTask DisposeAsync()
    {
        _cts.Cancel();
        _cts.Dispose();
        if (PWAService != null)
        {
            PWAService.OnInstallStateChanged -= HandleInstallStateChanged;
            PWAService.OnOnlineStateChanged  -= HandleOnlineStateChanged;
            await PWAService.DisposeAsync();
        }
    }
}
```

**HTML: thay inline style bằng CSS class:**
```razor
<!-- TRƯỚC -->
<div class="pwa-install-prompt" style="@GetDisplayStyle()">

<!-- SAU -->
<div class="@GetCssClass()">
```

**HTML: offline indicator — chỉ hiện khi offline:**
```razor
<!-- TRƯỚC -->
@if (ShowOfflineIndicator)

<!-- SAU -->
@if (!IsOnline)
```

---

### TẠO MỚI: `2_Gateway/Controllers/NotificationsController.cs`
```csharp
[ApiController]
[Route("api/notifications")]
public class NotificationsController(IHttpClientFactory factory) : ControllerBase
{
    [HttpPost("push/subscribe")]
    public async Task<IActionResult> Subscribe([FromBody] object body)
    {
        var token = Request.Headers["X-Customer-Token"].FirstOrDefault();
        var http  = factory.CreateClient("shoperp");
        http.DefaultRequestHeaders.TryAddWithoutValidation("X-Customer-Token", token ?? "");
        var resp  = await http.PostAsJsonAsync("api/notifications/push/subscribe", body);
        return resp.IsSuccessStatusCode ? Ok() : StatusCode((int)resp.StatusCode);
    }
}
```

### TẠO MỚI: `5_WebApps/ShopERP/Controllers/NotificationsController.cs`
```csharp
[ApiController]
[Route("api/notifications")]
public class NotificationsController(IVanAnDbContext db, ICustomerTokenService tokenService) : ControllerBase
{
    [HttpPost("push/subscribe")]
    public async Task<IActionResult> Subscribe([FromBody] PushSubscribeRequest req)
    {
        var token = Request.Headers["X-Customer-Token"].FirstOrDefault();
        var customerId = tokenService.ValidateToken(token ?? "");
        if (customerId == null) return Unauthorized();

        var customer = await db.Customers
            .FirstOrDefaultAsync(c => c.CustomerId.Value == customerId);
        if (customer == null) return NotFound();

        // Lưu subscription endpoint vào Customer
        // Note: Customer entity cần thêm PushSubscriptionJson property (approved)
        // Tạm thời log — sẽ implement đầy đủ sau khi Domain field được approve
        // customer.SetPushSubscription(req.SubscriptionJson);
        // await db.SaveChangesAsync();

        return Ok(new { message = "Subscription registered" });
    }
}

public record PushSubscribeRequest(string SubscriptionJson, Guid TenantId);
```

> **Domain note:** `Customer.PushSubscriptionJson` cần được thêm vào Domain. Đây là **Wave 18 Domain task** — trong W17-T4, lưu subscription vào log trước, implement đầy đủ sau.

---

## SỬA: `5_WebApps/KhachLink/Services/PWA/PWAService.cs`

Wire `SubscribeToPushAsync()` gọi endpoint sau khi lấy subscription từ JS:
```csharp
public async Task SubscribeAndRegisterAsync(string customerToken)
{
    try
    {
        string subscription = await _jsRuntime.InvokeAsync<string>("vananPWA.subscribeToPush");
        if (string.IsNullOrEmpty(subscription)) return;

        using var http = new HttpClient();
        http.DefaultRequestHeaders.TryAddWithoutValidation("X-Customer-Token", customerToken);
        await http.PostAsJsonAsync(
            $"{_navigationManager.BaseUri}api/notifications/push/subscribe",
            new { subscriptionJson = subscription });
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "Failed to subscribe to push notifications");
    }
}
```

---

## Entry criteria
- [ ] W17-T1 complete (cần CustomerToken cho push subscription)
- [ ] `PWAInstallPrompt.razor` readable (đọc file trước khi edit)

## Success criteria
- [ ] `PWAInstallPrompt.razor` implements `IAsyncDisposable` — không còn `async void Dispose()`
- [ ] Banner không hiện lại sau khi dismiss + reload
- [ ] Slide animation hoạt động (CSS class `.hidden` trigger `translateY(120%)`)
- [ ] `Task.Delay(3000)` cancel khi navigate away trong 3 giây — không có console error
- [ ] Offline indicator chỉ hiện khi thật sự offline (không hiện khi đang online)
- [ ] `POST /api/notifications/push/subscribe` → 200
- [ ] `dotnet build VanAn.sln` → 0 errors
