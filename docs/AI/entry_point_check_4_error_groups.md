# Entry Point Check — 4 Nhóm Lỗi Còn Lại Sau Impersonation

> **Ngày kiểm tra:** 2026-07-10
> **Context:** SystemAdmin login → List Tenants → Impersonate 1 tenant → Test 29 endpoints
> **Kết quả:** 17/29 OK (59%). 12 endpoints fail, chia 4 nhóm.
> **Fixes đã áp dụng trong session:**
> 1. Gateway `IAccountingDbContext` DI registration (Wave 1-3 gap) — `2_Gateway/Program.cs` dòng 72-76
> 2. `VanAnDbContext.ApplyMultiTenancyFilters` bỏ throw khi TenantId empty — `3_CoreHub/Infrastructure/VanAnDbContext.cs` dòng 231-238

---

## Nhóm 1: 4 × HTTP 500 — Accounting Reports (VAS)

### Endpoints affected
| # | Endpoint | Controller |
|---|---|---|
| 1 | `GET /api/balance-sheets` | `BalanceSheetsController` |
| 2 | `GET /api/cash-flow-statements` | `CashFlowStatementsController` |
| 3 | `GET /api/income-statements` | `IncomeStatementsController` |
| 4 | `GET /api/trial-balances` | `TrialBalancesController` |

### Nguyên nhân

**Root cause 1A — `Tenant.Type` null khi tạo qua API:**

`Tenant.CreateCompany()` (factory method) không set `Type` (TenantType) — by design:
- File: `1_Shared/Domain/Aggregates/TenantAggregate/Tenant.cs` dòng 34-38
- Comment gốc: *"DN created directly via CreateCompany: Type=null until W8 SetTenantType() method added."*
- `Tenant.CreateHouseholdBusiness()` set `Type = TenantType.HKD` (dòng 80)
- `Tenant.CreateFromConversion()` set `Type = newType` (dòng 113)
- Nhưng `CreateCompany()` KHÔNG set `Type` → null

`VasFeatureFlagService.CanAccessVasReportsAsync()` (`3_CoreHub/Services/VasFeatureFlagService.cs` dòng 41-52):
```csharp
TenantType? type = await GetTenantTypeAsync(tenantId, ct);
if (type is null) {
    _logger.LogWarning("VAS feature flag: tenant {TenantId} not found or Type is null — access denied");
    return false;  // ← null Type = access denied
}
bool canAccess = type != TenantType.HKD;
```

→ Tenant tạo qua `POST /api/tenants` với `BusinessType=Company` có `Type=null` → VAS access denied → `Forbid()`.

**Root cause 1B — `Forbid("message")` misuse (bug thứ cấp):**

Controller dùng `return Forbid("VAS reports are only available...")` (`BalanceSheetsController.cs` dòng 49).
Trong ASP.NET Core, `Forbid(string)` treat tham số làm **authentication scheme name**, không phải error message.
→ ASP.NET Core tìm scheme tên *"VAS reports are only available for Enterprise tenants..."* → không tìm thấy → throw:
```
System.InvalidOperationException: No authentication handler is registered for the scheme
'VAS reports are only available for Enterprise tenants. HKD tenants use the HKD Book module.'.
The registered schemes are: Cookies, OpenIdConnect.
```

### Giải pháp

**Fix 1A — Set TenantType trong CreateCompany hoặc TenantManagementService:**

Option 1 (Domain layer — cần approval): Sửa `CreateCompany()` để set `Type = TenantType.Enterprise_SME` mặc định:
```csharp
// 1_Shared/Domain/Aggregates/TenantAggregate/Tenant.cs dòng 52-66
public static Tenant CreateCompany(TenantId id, string name, TenantSettings? settings = null)
{
    // ...
    Type = TenantType.Enterprise_SME,  // ← thêm dòng này
    // ...
}
```

Option 2 (Service layer — không cần domain approval): Sửa `TenantManagementService.CreateTenantAsync()` (`3_CoreHub/Services/TenantManagementService.cs` dòng 19-34) để gọi `SetTenantType()` sau khi create:
```csharp
Tenant tenant = request.BusinessType == BusinessType.HouseholdBusiness && request.HKDGroup.HasValue
    ? Tenant.CreateHouseholdBusiness(id, request.Name, request.HKDGroup.Value, settings)
    : Tenant.CreateCompany(id, request.Name, settings);

// Fix: classify Company tenant for VAS feature flag routing
if (request.BusinessType == BusinessType.Company)
{
    tenant.SetTenantType(TenantType.Enterprise_SME, AccountingStandard.TT133_2016);
}
```

Option 3 (API layer — expose endpoint): Thêm `POST /api/tenants/{id}/classify` cho SystemAdmin set TenantType manually.

**Khuyến nghị:** Option 2 — không sửa Domain, đúng intent của W8 design (SetTenantType method tồn tại chính để mục đích này).

**Fix 1B — Sửa `Forbid("message")` thành `StatusCode(403, ...)`:**

Sửa trong 4 controllers (BalanceSheets, CashFlow, IncomeStatements, TrialBalances):
```csharp
// BEFORE (bug):
return Forbid("VAS reports are only available for Enterprise tenants. HKD tenants use the HKD Book module.");

// AFTER (fix):
return StatusCode(403, new { error = "VAS reports are only available for Enterprise tenants. HKD tenants use the HKD Book module." });
```

Hoặc dùng `ObjectResult` với status 403:
```csharp
return Problem(
    statusCode: 403,
    title: "VAS Access Denied",
    detail: "VAS reports are only available for Enterprise tenants. HKD tenants use the HKD Book module.");
```

---

## Nhóm 2: 4 × HTTP 401 — AllowAnonymous endpoints bị chặn

### Endpoints affected
| # | Endpoint | Controller | Auth attribute |
|---|---|---|---|
| 1 | `GET /api/customeridentity/me` | `CustomerIdentityController` | `[AllowAnonymous]` (class-level) |
| 2 | `GET /api/customerorders` | `CustomerOrdersController` | `[AllowAnonymous]` (class-level) |
| 3 | `GET /api/loyalty/my` | `LoyaltyController` | `[AllowAnonymous]` (class-level) |
| 4 | `POST /api/notifications/push/subscribe` | `NotificationsController` | `[AllowAnonymous]` (class-level) |

### Nguyên nhân

Các controller có `[AllowAnonymous]` ở class-level nhưng vẫn trả 401 khi gọi với Cookie auth (SystemAdmin impersonating).

**Nghi vấn 1 — HMAC Signing Middleware chặn request:**

ShopERP `Program.cs` dòng ~522 có HMAC middleware. Nếu middleware chạy TRƯỚC authorization và require HMAC signature cho protected paths, nó có thể chặn request dù controller là `[AllowAnonymous]`.

Cần kiểm tra:
- `HmacSigning:ProtectedPaths` trong config — có bao gồm `/api/customeridentity`, `/api/customerorders`, etc.?
- Middleware order: HMAC middleware chạy trước hay sau `UseAuthentication`/`UseAuthorization`?

**Nghi vấn 2 — Cookie auth scheme override AllowAnonymous:**

Khi request đến với Cookie auth (SystemAdmin impersonating), `[AllowAnonymous]` nên cho phép truy cập ẩn danh. Nhưng nếu có custom middleware hoặc auth handler intercept request trước khi đến authorization layer, 401 có thể xảy ra.

**Nghi vấn 3 — Các endpoint này dành cho KhachLink PWA (customer-facing):**

`[AllowAnonymous]` đúng intent — các endpoint này phục vụ KhachLink PWA, khách hàng không cần đăng nhập. Khi SystemAdmin (có cookie) gọi, có thể có logic kiểm tra "customer identity" (DeviceId, OTP session) mà SystemAdmin không có → 401.

Cần đọc code từng controller để xác nhận:
- `CustomerIdentityController.GetMe()` — có require customer session?
- `CustomerOrdersController.GetMyOrders()` — có require customer DeviceId?
- `LoyaltyController.GetMyLoyalty()` — có require customer identity?
- `NotificationsController.Subscribe()` — có require customer PushSubscription?

### Giải pháp

**Bước 1 — Điều tra (REVIEW_ONLY):**

Đọc 4 controllers để xác định chính xác nguyên nhân:
- Kiểm tra HMAC middleware config (`appsettings.Development.json` → `HmacSigning:ProtectedPaths`)
- Kiểm tra middleware order trong `Program.cs` (`UseAuthentication` → `UseAuthorization` → `UseHmacSigning`?)
- Kiểm tra mỗi action method có require thêm header/param gì không (DeviceId, CustomerId, OTP token)

**Bước 2 — Fix tùy root cause:**

- Nếu HMAC middleware chặn: loại bỏ các `/api/*` paths khỏi `ProtectedPaths` trong Development config, hoặc thêm bypass cho `[AllowAnonymous]` endpoints
- Nếu require customer identity: đây là **expected behavior** — SystemAdmin không thể test customer-facing endpoints vì không có customer session. Ghi nhận là "by design", không fix.
- Nếu auth scheme conflict: đảm bảo `[AllowAnonymous]` được honor bởi tất cả middleware trong pipeline

**Khuyến nghị:** Điều tra từng controller, phân loại "by design" vs "bug". Có thể 3/4 endpoints là by design (customer-only), 1/4 là bug (HMAC middleware).

---

## Nhóm 3: 3 × HTTP 401 — Gateway JWT auth scheme mismatch

### Endpoints affected
| # | Endpoint | Controller | Auth |
|---|---|---|---|
| 1 | `GET /api/accounting-entries` (JWT) | `AccountingEntriesController` | `[Authorize(Policy="RequireTenantAccess")]` |
| 2 | `GET /api/hkd-books` (JWT) | `HKDBooksController` | `[Authorize(Policy="RequireTenantAccess", AuthenticationSchemes=JwtBearer)]` |
| 3 | `GET /api/orders` (JWT) | `OrdersController` | `[Authorize(Policy="RequireTenantAccess")]` |

### Nguyên nhân

> **Verification note (2026-07-10):** Root cause 3A đã được re-investigate — Gateway CÓ `ForwardDefaultSelector` tự forward Bearer header sang JWT Bearer scheme. Claim cũ "Cookie default → JWT không parse" là SAI. Xem chi tiết bên dưới.

**Root cause 3A — Gateway `ForwardDefaultSelector` tồn tại nhưng 401 vẫn xảy ra (cần điều tra thêm):**

`2_Gateway/Program.cs` dòng 86-105 cấu hình dual-scheme auth với `ForwardDefaultSelector`:
```csharp
_ = builder.Services.AddAuthentication(options =>
{
    // Cookie remains the default scheme — Blazor UI continues to work unchanged
    options.DefaultScheme = CookieAuthenticationDefaults.AuthenticationScheme;
})
    .AddCookie(CookieAuthenticationDefaults.AuthenticationScheme, options =>
    {
        options.LoginPath = "/login";
        options.ExpireTimeSpan = TimeSpan.FromHours(8);
        // W4 Fix: Forward to JWT Bearer when Authorization header is present.
        // This enables dual-scheme auth: Cookie for Blazor UI, JWT for API tests.
        options.ForwardDefaultSelector = context =>
        {
            if (context.Request.Headers.TryGetValue("Authorization", out var auth)
                && auth.ToString().StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
            {
                return JwtBearerDefaults.AuthenticationScheme;
            }
            return null; // Use Cookie (default scheme)
        };
    })
    .AddJwtBearer(JwtBearerDefaults.AuthenticationScheme, options => { /* ... */ });
```

→ Khi gửi `Authorization: Bearer <token>`, Gateway **CÓ** forward sang JWT Bearer scheme và parse token. Claim cũ trong document này ("Cookie default → không parse JWT → 401") là **SAI** — `ForwardDefaultSelector` đã handle case này.

**Vậy 401 đến từ đâu?** Các khả năng cần điều tra:
1. **JWT validation failure** — Issuer/Audience mismatch giữa token do ShopERP issue và Gateway validate (xem `TokenValidationParameters` dòng 112-129). Nếu `Jwt:Issuer`/`Jwt:Audience` config khác nhau giữa 2 apps → token bị reject.
2. **`tenant_id: "system"` claim** — Policy `RequireTenantAccess` chỉ require claim tồn tại (dòng 133-135), nhưng controller downstream có thể parse `tenant_id` sang Guid và fail. Xem root cause 3B.
3. **Controller không có explicit `AuthenticationSchemes`** — `AccountingEntriesController` và `OrdersController` rely vào default scheme. `ForwardDefaultSelector` chạy ở Cookie scheme level, có thể không apply cho tất cả policy evaluation paths.

**Root cause 3B — JWT token có `tenant_id: "system"` (string), không phải GUID:**

`DevLoginController.LoginAsSystemAdmin()` (`5_WebApps/ShopERP/Controllers/DevLoginController.cs` dòng 172-206) issue JWT với:
```csharp
// SystemAdmin JWT without tenant constraint
var jwtToken = _jwtTokenService.GenerateToken(
    userId: Guid.Parse("00000000-0000-0000-0000-000000000001"),
    email: "systemadmin@vanan.vn",
    role: PlatformRole.SystemAdmin.ToString(),  // ← đúng: SystemAdmin, KHÔNG phải Owner
    tenantId: Guid.Empty);  // ← Guid.Empty → JwtTokenService convert sang "system"
```

`JwtTokenService.GenerateToken(string role)` overload (`3_CoreHub/Services/JwtTokenService.cs` dòng 88-90) convert `Guid.Empty` → `"system"`:
```csharp
// For SystemAdmin, tenant_id may be empty or special value
new("tenant_id", tenantId == Guid.Empty ? "system" : tenantId.ToString()),
new("TenantId", tenantId == Guid.Empty ? "system" : tenantId.ToString()),
```

→ JWT payload thực tế decode được:
```json
{
  "sub": "00000000-0000-0000-0000-000000000001",
  "email": "systemadmin@vanan.vn",
  "tenant_id": "system",  // ← string "system", không phải GUID
  "TenantId": "system",
  "role": "SystemAdmin"
}
```

`RequireTenantAccess` policy (`2_Gateway/Program.cs` dòng 133-135):
```csharp
.AddPolicy("RequireTenantAccess", policy =>
    policy.RequireAuthenticatedUser()
           .RequireClaim("tenant_id"))  // ← require tenant_id claim exists
```

Policy chỉ require claim `tenant_id` tồn tại (không validate format) → `"system"` pass policy. Nhưng controller dùng `TenantId` để query data — nếu `tenant_id = "system"` (string), parse sang Guid sẽ fail ở service layer.

**Root cause 3C — HKDBooksController explicit JwtBearer scheme:**

`HKDBooksController` có:
```csharp
[Authorize(Policy = "RequireTenantAccess", AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
```

Yêu cầu JWT Bearer auth. Khi gửi Cookie → reject 401. Khi gửi JWT → parse OK nhưng `tenant_id = "system"` → policy pass (claim tồn tại) → controller chạy → query fail vì tenant_id không phải GUID.

### Giải pháp

> **Verification note (2026-07-10):** Vì `ForwardDefaultSelector` đã tồn tại, Fix 3A cũ (thêm convention-based JWT Bearer default) có thể **không cần thiết** — Gateway đã parse JWT khi có Bearer header. Cần xác nhận 401 đến từ validation failure hay downstream parse failure trước khi áp dụng fix.

**Fix 3A — Điều tra thực tế 401 trước khi fix (REVIEW_ONLY):**

Trước khi áp dụng fix, cần xác định 401 đến từ bước nào trong pipeline:
1. **JWT validation failure** — Kiểm tra `Jwt:Issuer`/`Jwt:Audience` config trong `appsettings.Development.json` của ShopERP và Gateway có match không. Nếu mismatch → token bị reject ở `TokenValidationParameters`.
2. **Policy pass nhưng controller fail** — Nếu JWT validate OK, policy `RequireTenantAccess` pass (claim `tenant_id = "system"` tồn tại), nhưng controller/service parse `"system"` sang Guid → throw → 500 (không phải 401). Nếu thấy 401, khả năng cao là validation failure.
3. **ForwardDefaultSelector edge case** — Kiểm tra `ForwardDefaultSelector` có chạy cho tất cả `RequireTenantAccess` policy evaluations không, đặc biệt khi controller không có explicit `AuthenticationSchemes`.

**Nếu 401 là JWT validation failure (Issuer/Audience mismatch):**
- Sync `Jwt:Issuer`/`Jwt:Audience` giữa ShopERP `appsettings.Development.json` và Gateway `appsettings.Development.json`.
- Không cần thay đổi auth scheme convention.

**Nếu 401 là do `ForwardDefaultSelector` không cover tất cả cases:**
- Option 1 (Policy-based): Thêm `AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme` cho Gateway controllers cần JWT.
- Option 2 (Convention-based): Thêm convention cho `/api/*` controllers:
```csharp
// 2_Gateway/Program.cs — thêm sau AddAuthentication
builder.Services.AddControllers(options =>
{
    // Tất cả controllers trong /api/* dùng JWT Bearer làm default
    options.Conventions.Add(new ApiJwtBearerConvention());
});
```

**Fix 3B — Issue JWT với đúng tenant_id GUID sau impersonation:**

Hiện tại `LoginAsSystemAdmin()` issue JWT với `tenantId: Guid.Empty` → `JwtTokenService` convert sang `"system"`. Đây là **by design cho cross-tenant SystemAdmin**, nhưng gây fail khi controller parse `tenant_id` sang Guid.

Giải pháp:
1. **Thêm endpoint impersonation:** `POST /dev/login/systemadmin/{tenantId}` — issue JWT với `tenantId = impersonatedTenantId` (GUID thực), dùng `GenerateToken(string role)` overload:
```csharp
// Endpoint mới: SystemAdmin impersonate tenant cụ thể + lấy JWT
[HttpPost("login/systemadmin/{tenantId:guid}")]
public async Task<IActionResult> LoginAsSystemAdminForTenant(Guid tenantId)
{
    // Issue JWT với tenantId GUID thực (không phải Guid.Empty)
    var jwtToken = _jwtTokenService.GenerateToken(
        userId: Guid.Parse("00000000-0000-0000-0000-000000000001"),
        email: "systemadmin@vanan.vn",
        role: PlatformRole.SystemAdmin.ToString(),
        tenantId: tenantId);  // ← GUID thực, không phải Guid.Empty
    // ... issue cookie + return token
}
```

2. **Hoặc bypass `RequireTenantAccess` cho role=SystemAdmin:** Sửa policy để bypass `RequireClaim("tenant_id")` khi role=SystemAdmin (cross-tenant admin không cần tenant_id). Cần custom `IAuthorizationHandler`.

**Fix 3C — HKDBooksController:**

`HKDBooksController` đã có explicit `AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme` → JWT Bearer đã works (nếu JWT validate OK). 401 khi gửi Cookie là by design (xem Nhóm 4). Không cần fix riêng cho JWT path — chỉ cần Fix 3B (issue JWT với đúng tenant_id GUID).

**Khuyến nghị:**
1. **Bước 1 (REVIEW_ONLY):** Điều tra 401 thực tế — kiểm tra `Jwt:Issuer`/`Jwt:Audience` config match giữa ShopERP và Gateway. Nếu mismatch → sync config, 401 sẽ hết.
2. **Bước 2 (IMPLEMENT):** Fix 3B — thêm `POST /dev/login/systemadmin/{tenantId}` endpoint để SystemAdmin lấy JWT với tenant_id GUID thực sau impersonation.
3. **Bước 3 (nếu cần):** Nếu `ForwardDefaultSelector` không cover tất cả cases → áp dụng convention-based JWT Bearer cho `/api/*` controllers (Fix 3A Option 2).

---

## Nhóm 4: 1 × HTTP 401 — Gateway HKDBooks (Cookie auth)

### Endpoint affected
| # | Endpoint | Controller | Auth |
|---|---|---|---|
| 1 | `GET /api/hkd-books` (Cookie) | `HKDBooksController` | `[Authorize(Policy="RequireTenantAccess", AuthenticationSchemes=JwtBearerDefaults.AuthenticationScheme)]` |

### Nguyên nhân

`HKDBooksController` có explicit `AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme`:
```csharp
[Authorize(Policy = "RequireTenantAccess", AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
public class HKDBooksController : ControllerBase
```

Controller chỉ chấp nhận JWT Bearer, reject Cookie auth. Khi SystemAdmin impersonate (Cookie) → 401.

Đây là **by design** — HKD Books là accounting module nhạy cảm, yêu cầu JWT Bearer (API-level auth) thay vì Cookie (UI-level auth). Cookie auth dành cho Blazor Server UI, JWT Bearer dành cho API clients (KhachLink, external integrations).

### Giải pháp

**Không cần fix — by design.**

SystemAdmin muốn test HKD Books phải dùng JWT Bearer token, không phải Cookie. Cần:

1. Issue JWT cho SystemAdmin với tenant_id GUID (sau impersonation) — xem Fix 3B
2. Gọi `GET /api/hkd-books` với `Authorization: Bearer <jwt>`

Hoặc nếu muốn SystemAdmin (Cookie) cũng truy cập được, bỏ `AuthenticationSchemes` constraint:
```csharp
// BEFORE:
[Authorize(Policy = "RequireTenantAccess", AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]

// AFTER:
[Authorize(Policy = "RequireTenantAccess")]
```

Nhưng **không khuyến nghị** — giảm security. HKD Books nên giữ JWT Bearer only.

---

## Tóm tắt ưu tiên fix

| Ưu tiên | Nhóm | Số endpoints | Fix complexity | Rủi ro |
|---|---|---|---|---|
| **P1** | Nhóm 1B (Forbid misuse) | 4 | Thấp — đổi 1 dòng × 4 files | Không |
| **P1** | Nhóm 1A (TenantType null) | 4 | Thấp — thêm 2 dòng trong service | Thấp |
| **P2** | Nhóm 3A (Gateway JWT 401) | 3 | Cần điều tra trước — có thể chỉ là config mismatch (Issuer/Audience) | Thấp–Trung bình |
| **P2** | Nhóm 3B (JWT tenant_id) | 3 | Trung bình — thêm endpoint `/dev/login/systemadmin/{tenantId}` | Trung bình |
| **P3** | Nhóm 2 (AllowAnonymous 401) | 4 | Cần điều tra trước — có thể by design | — |
| **—** | Nhóm 4 (HKDBooks Cookie) | 1 | By design — không fix | — |

**Tổng cộng:** Fix P1 (8 endpoints) + P2 (6 endpoints) = 14 endpoints có thể fix. P2 cần điều tra config mismatch trước khi code. P3 cần điều tra. Nhóm 4 by design.

> **Verification log (2026-07-10):** Nhóm 3 root cause 3A đã được re-investigate — `ForwardDefaultSelector` tồn tại trong Gateway `Program.cs` dòng 95-105, tự forward Bearer header sang JWT Bearer scheme. Claim cũ "Cookie default → JWT không parse" đã được đính chính. Root cause 3B code snippet đã được sửa — `LoginAsSystemAdmin()` dùng `PlatformRole.SystemAdmin.ToString()` và `Guid.Empty` (không phải `UserRole.Owner` và `TestTenantId` như claim cũ).
