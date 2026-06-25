# W17-T1 — Customer Identity (Phone OTP Login)

**Wave:** 17 — KhachLink Retention & Loyalty
**Branch:** `feature/wave17-khachlink-retention`
**Priority:** 🔴 CRITICAL — prerequisite của W17-T2, W17-T3
**Conflict risk:** HIGH — thêm OTP service mới, 2 Gateway endpoints mới, 2 Razor pages mới
**Depends on:** Wave 16 complete
**Estimated effort:** 1.5 sessions

---

## Vấn đề

Sau Wave 16, KhachLink có order flow hoàn chỉnh nhưng **user là anonymous hoàn toàn**. Không có customer identity đồng nghĩa với:
- Điểm thưởng tích được không gắn với ai cụ thể
- Push notification không thể cá nhân hóa
- Lịch sử đơn hàng không thể hiển thị cho đúng người
- `OrderWorkflowService.ProcessLoyaltyPointsAsync()` đã có logic nhưng `customer == null` → bỏ qua (line 134)

## Hiện trạng — đã có sẵn

| Thành phần | File | Trạng thái |
|-----------|------|-----------|
| `Customer` entity — `PhoneNumber`, `DeviceId`, `CustomerTier` | `1_Shared/Domain.cs` line 563 | ✅ |
| `CustomerOnboardingService` — `StartOnboardingAsync`, SMS via `INotificationService` | `3_CoreHub/Services/CustomerOnboardingService.cs` | ✅ |
| `ICustomerOnboardingService` | `3_CoreHub/Services/ICustomerOnboardingService.cs` | ✅ |
| `IdentityUpgradeModal.razor` — modal UI với "Nâng cấp tài khoản" | `5_WebApps/KhachLink/Components/IdentityUpgradeModal.razor` | ✅ |
| `Order.CustomerDeviceId` — zero-friction fallback | `1_Shared/Domain.cs` line 750 | ✅ |
| `ShopERP/Program.cs` — `ILoyaltyRewardsService` registered | line 88 | ✅ |

## Thiếu (cần tạo mới)

| Thành phần | Cần tạo |
|-----------|---------|
| OTP generation + TTL storage | `ShopERP/Services/OtpService.cs` |
| CustomerToken (JWT-lite via IDataProtector) | `ShopERP/Services/CustomerTokenService.cs` |
| Gateway `CustomersController` | `2_Gateway/Controllers/CustomersController.cs` |
| ShopERP `CustomerIdentityController` | `5_WebApps/ShopERP/Controllers/CustomerIdentityController.cs` |
| `Pages/Login.razor` | `5_WebApps/KhachLink/Pages/Login.razor` |
| `Pages/Profile.razor` | `5_WebApps/KhachLink/Pages/Profile.razor` |

---

## Luồng thiết kế: Zero-friction → Upgrade

```
Lần đầu vào app
    │
    ▼
DeviceId = localStorage("device_id") ?? crypto.randomUUID()
    → đặt hàng ngay được, không cần login

    ▼ (sau đơn hàng đầu tiên thành công — Checkout.razor redirect về /order-tracking)
IdentityUpgradeModal hiện (check localStorage("identity_upgraded") == null)
    │
    ├─ User nhập số điện thoại
    │       → POST /api/customers/otp/send
    │           Body: { phoneNumber, tenantId, deviceId }
    │           → ShopERP: tìm/tạo Customer, lưu OTP hash + TTL 5 phút
    │           → SMS: "Mã OTP của bạn: 123456 (hết hạn sau 5 phút)"
    │
    ├─ User nhập OTP
    │       → POST /api/customers/otp/verify
    │           Body: { phoneNumber, tenantId, otp }
    │           → ShopERP: verify OTP, link DeviceId → CustomerId
    │           → Response: { customerId, customerToken, tier, pointBalance }
    │           → localStorage("customer_token") = customerToken
    │           → localStorage("identity_upgraded") = "true"
    │
    └─ Từ đây: IHttpClientFactory("gateway") tự gắn X-Customer-Token header
```

---

## Scope giới hạn (HARD RULES)

- **KHÔNG** dùng ASP.NET Identity — quá nặng
- **KHÔNG** sửa `1_Shared/Domain.cs` — `Customer` entity đã đủ fields
- **KHÔNG** thêm EF migration trong task này — OTP lưu in-memory cache (`IMemoryCache`) với TTL, không cần bảng DB
- Token = `IDataProtector.Protect(customerId + ":" + expiry)` — không cần JWT library mới
- SMS provider: nếu `INotificationService` chưa config → dry-run mode trả OTP trong response header `X-Dev-OTP` (feature flag `Dev:ExposeOtp=true` trong appsettings.Development.json)

---

## Files cần tạo/sửa

### TẠO MỚI: `5_WebApps/ShopERP/Services/OtpService.cs`
```csharp
public interface IOtpService
{
    string Generate(string phoneNumber);            // sinh 6 số, cache với TTL 5 phút
    bool Verify(string phoneNumber, string otp);    // verify + xóa khỏi cache sau khi dùng
}

public class OtpService(IMemoryCache cache) : IOtpService
{
    public string Generate(string phoneNumber)
    {
        var otp = Random.Shared.Next(100000, 999999).ToString();
        cache.Set($"otp:{phoneNumber}", otp, TimeSpan.FromMinutes(5));
        return otp;
    }

    public bool Verify(string phoneNumber, string otp)
    {
        if (!cache.TryGetValue($"otp:{phoneNumber}", out string? stored)) return false;
        if (stored != otp) return false;
        cache.Remove($"otp:{phoneNumber}");
        return true;
    }
}
```

### TẠO MỚI: `5_WebApps/ShopERP/Services/CustomerTokenService.cs`
```csharp
public interface ICustomerTokenService
{
    string CreateToken(Guid customerId);
    Guid? ValidateToken(string token);
}

public class CustomerTokenService(IDataProtectionProvider provider) : ICustomerTokenService
{
    private readonly IDataProtector _protector =
        provider.CreateProtector("VanAn.KhachLink.CustomerToken");

    public string CreateToken(Guid customerId)
    {
        var payload = $"{customerId}:{DateTimeOffset.UtcNow.AddDays(30).ToUnixTimeSeconds()}";
        return _protector.Protect(payload);
    }

    public Guid? ValidateToken(string token)
    {
        try
        {
            var payload = _protector.Unprotect(token);
            var parts = payload.Split(':');
            if (parts.Length != 2) return null;
            if (!Guid.TryParse(parts[0], out var id)) return null;
            if (DateTimeOffset.FromUnixTimeSeconds(long.Parse(parts[1])) < DateTimeOffset.UtcNow) return null;
            return id;
        }
        catch { return null; }
    }
}
```

### TẠO MỚI: `5_WebApps/ShopERP/Controllers/CustomerIdentityController.cs`
```csharp
[ApiController]
[Route("api/customers")]
public class CustomerIdentityController(
    IVanAnDbContext db,
    IOtpService otpService,
    ICustomerTokenService tokenService,
    INotificationService notificationService,
    IConfiguration config) : ControllerBase
{
    [HttpPost("otp/send")]
    public async Task<IActionResult> SendOtp([FromBody] SendOtpRequest req)
    {
        // Tìm hoặc tạo Customer
        var customer = await db.Customers
            .FirstOrDefaultAsync(c => c.PhoneNumber == req.PhoneNumber
                                   && c.TenantId.Value == req.TenantId);
        if (customer == null)
        {
            customer = new Customer(new TenantId(req.TenantId), "Khách hàng", req.PhoneNumber);
            db.Customers.Add(customer);
            await db.SaveChangesAsync();
        }

        var otp = otpService.Generate(req.PhoneNumber);

        // SMS or dev-mode expose
        if (config.GetValue<bool>("Dev:ExposeOtp"))
            Response.Headers["X-Dev-OTP"] = otp;
        else
            await notificationService.SendSMSAsync(req.PhoneNumber, $"Mã OTP Vạn An: {otp} (5 phút)");

        return Ok(new { message = "OTP đã gửi" });
    }

    [HttpPost("otp/verify")]
    public async Task<IActionResult> VerifyOtp([FromBody] VerifyOtpRequest req)
    {
        if (!otpService.Verify(req.PhoneNumber, req.Otp))
            return BadRequest(new { error = "OTP không hợp lệ hoặc đã hết hạn" });

        var customer = await db.Customers
            .FirstOrDefaultAsync(c => c.PhoneNumber == req.PhoneNumber
                                   && c.TenantId.Value == req.TenantId);
        if (customer == null) return NotFound();

        // Link DeviceId nếu có
        if (!string.IsNullOrEmpty(req.DeviceId) && Guid.TryParse(req.DeviceId, out var deviceGuid))
            customer.UpdateCustomerDetails(customer.FullName, customer.PhoneNumber,
                customer.Email, customer.CustomerTier, deviceGuid, true);

        await db.SaveChangesAsync();

        var token = tokenService.CreateToken(customer.CustomerId.Value);
        return Ok(new
        {
            customerId  = customer.CustomerId.Value,
            customerToken = token,
            tier        = customer.CustomerTier,
            pointBalance = customer.LoyaltyPoints
        });
    }
}

public record SendOtpRequest(string PhoneNumber, Guid TenantId, string? DeviceId);
public record VerifyOtpRequest(string PhoneNumber, Guid TenantId, string Otp, string? DeviceId);
```

### TẠO MỚI: `2_Gateway/Controllers/CustomersController.cs`
```csharp
[ApiController]
[Route("api/customers")]
public class CustomersController(IHttpClientFactory factory) : ControllerBase
{
    private HttpClient ShopERP => factory.CreateClient("shoperp");

    [HttpPost("otp/send")]
    public async Task<IActionResult> SendOtp([FromBody] object body)
    {
        var resp = await ShopERP.PostAsJsonAsync("api/customers/otp/send", body);
        // Forward X-Dev-OTP header in dev mode
        if (resp.Headers.TryGetValues("X-Dev-OTP", out var vals))
            Response.Headers["X-Dev-OTP"] = vals.First();
        return resp.IsSuccessStatusCode ? Ok(await resp.Content.ReadFromJsonAsync<object>())
                                        : StatusCode((int)resp.StatusCode);
    }

    [HttpPost("otp/verify")]
    public async Task<IActionResult> VerifyOtp([FromBody] object body)
    {
        var resp = await ShopERP.PostAsJsonAsync("api/customers/otp/verify", body);
        return resp.IsSuccessStatusCode ? Ok(await resp.Content.ReadFromJsonAsync<object>())
                                        : StatusCode((int)resp.StatusCode, await resp.Content.ReadAsStringAsync());
    }
}
```

### TẠO MỚI: `5_WebApps/KhachLink/Pages/Login.razor`
```razor
@page "/login"
@inject IHttpClientFactory HttpClientFactory
@inject IJSRuntime JSRuntime
@inject NavigationManager Nav

<PageTitle>Đăng nhập — Vạn An</PageTitle>

<div class="container py-5">
    <div class="row justify-content-center">
        <div class="col-md-5">
            <VanAnCard Shadow="true">
                <h4 class="text-center mb-4">🔐 Xác nhận số điện thoại</h4>

                @if (_step == LoginStep.Phone)
                {
                    <VanAnInput Label="Số điện thoại" @bind-Value="_phone"
                                Placeholder="0901234567" InputType="tel" />
                    <VanAnButton Variant="ButtonVariant.Primary" OnClick="SendOtp"
                                 IsLoading="_loading" CssClass="w-100 mt-3">
                        Gửi mã OTP
                    </VanAnButton>
                }
                else if (_step == LoginStep.Otp)
                {
                    <p class="text-muted text-center">Nhập mã 6 số đã gửi đến <strong>@_phone</strong></p>
                    <VanAnInput Label="Mã OTP" @bind-Value="_otp"
                                Placeholder="123456" InputType="number" />
                    <VanAnButton Variant="ButtonVariant.Primary" OnClick="VerifyOtp"
                                 IsLoading="_loading" CssClass="w-100 mt-3">
                        Xác nhận
                    </VanAnButton>
                    <VanAnButton Variant="ButtonVariant.Secondary" OnClick="@(() => _step = LoginStep.Phone)"
                                 CssClass="w-100 mt-2">
                        Đổi số điện thoại
                    </VanAnButton>
                }

                @if (!string.IsNullOrEmpty(_error))
                {
                    <VanAnAlert Variant="AlertVariant.Danger" CssClass="mt-3">@_error</VanAnAlert>
                }
            </VanAnCard>
        </div>
    </div>
</div>

@code {
    [SupplyParameterFromQuery] public string? ReturnUrl { get; set; }

    private enum LoginStep { Phone, Otp }
    private LoginStep _step = LoginStep.Phone;
    private string _phone = "";
    private string _otp   = "";
    private string _error = "";
    private bool   _loading = false;

    private async Task SendOtp()
    {
        _loading = true; _error = "";
        try
        {
            var deviceId = await JSRuntime.InvokeAsync<string>("localStorage.getItem", "device_id");
            var tenantId = await JSRuntime.InvokeAsync<string>("localStorage.getItem", "tenant_id");
            var http     = HttpClientFactory.CreateClient("gateway");
            var resp     = await http.PostAsJsonAsync("api/customers/otp/send",
                new { phoneNumber = _phone, tenantId = Guid.Parse(tenantId ?? Guid.Empty.ToString()), deviceId });
            if (resp.IsSuccessStatusCode) _step = LoginStep.Otp;
            else _error = "Không thể gửi OTP. Vui lòng thử lại.";
        }
        catch { _error = "Lỗi kết nối. Vui lòng thử lại."; }
        finally { _loading = false; }
    }

    private async Task VerifyOtp()
    {
        _loading = true; _error = "";
        try
        {
            var deviceId = await JSRuntime.InvokeAsync<string>("localStorage.getItem", "device_id");
            var tenantId = await JSRuntime.InvokeAsync<string>("localStorage.getItem", "tenant_id");
            var http     = HttpClientFactory.CreateClient("gateway");
            var resp     = await http.PostAsJsonAsync("api/customers/otp/verify",
                new { phoneNumber = _phone, tenantId = Guid.Parse(tenantId ?? Guid.Empty.ToString()),
                      otp = _otp, deviceId });
            if (resp.IsSuccessStatusCode)
            {
                var result = await resp.Content.ReadFromJsonAsync<OtpVerifyResult>();
                await JSRuntime.InvokeVoidAsync("localStorage.setItem", "customer_token",  result!.CustomerToken);
                await JSRuntime.InvokeVoidAsync("localStorage.setItem", "customer_id",     result.CustomerId.ToString());
                await JSRuntime.InvokeVoidAsync("localStorage.setItem", "customer_tier",   result.Tier);
                await JSRuntime.InvokeVoidAsync("localStorage.setItem", "identity_upgraded", "true");
                Nav.NavigateTo(ReturnUrl ?? "/my-loyalty");
            }
            else _error = "OTP không hợp lệ hoặc đã hết hạn.";
        }
        catch { _error = "Lỗi kết nối. Vui lòng thử lại."; }
        finally { _loading = false; }
    }

    private record OtpVerifyResult(Guid CustomerId, string CustomerToken, string Tier, int PointBalance);
}
```

### SỬA: `5_WebApps/KhachLink/Pages/Checkout.razor`

Sau khi checkout thành công, kiểm tra xem đã upgrade identity chưa:
```csharp
// Thêm vào sau NavigationManager.NavigateTo($"/order-tracking/{createdOrderId}"):
var upgraded = await JSRuntime.InvokeAsync<string>("localStorage.getItem", "identity_upgraded");
if (string.IsNullOrEmpty(upgraded))
    _showUpgradeModal = true; // trigger IdentityUpgradeModal
```

---

## Entry criteria
- [ ] Wave 16 merged + `dotnet build VanAn.sln` → 0 errors
- [ ] Branch `feature/wave17-khachlink-retention` tạo từ `main`
- [ ] `INotificationService` available trong ShopERP DI container

## Success criteria
- [ ] `POST /api/customers/otp/send` → 200 + SMS gửi (hoặc `X-Dev-OTP` header trong dev mode)
- [ ] `POST /api/customers/otp/verify` → 200 + `{ customerId, customerToken, tier, pointBalance }`
- [ ] `localStorage("customer_token")` được set sau verify
- [ ] `IdentityUpgradeModal` hiện sau đơn hàng đầu tiên (check `localStorage("identity_upgraded")`)
- [ ] `Login.razor` render đúng, validate input, hiển thị lỗi bằng VanAnAlert
- [ ] `dotnet build VanAn.sln` → 0 errors
- [ ] Architecture tests: 7/7 PASS

## Hard stops
- KHÔNG sửa `1_Shared/Domain.cs` cho task này
- KHÔNG thêm ASP.NET Identity packages
- KHÔNG commit OTP codes, token secrets, hay API keys
- OTP storage dùng `IMemoryCache` — KHÔNG tạo bảng DB mới
