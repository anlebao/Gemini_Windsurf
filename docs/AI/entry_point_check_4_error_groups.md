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

**Root cause 3A — Gateway default auth scheme là Cookie, không JWT:**

`2_Gateway/Program.cs` dòng 80-84:
```csharp
_ = builder.Services.AddAuthentication(options =>
{
    // Cookie remains the default scheme — Blazor UI continues to work unchanged
    options.DefaultScheme = CookieAuthenticationDefaults.AuthenticationScheme;
})
```

Khi gửi `Authorization: Bearer <token>`, Gateway vẫn dùng Cookie auth làm default scheme → không parse JWT → 401.

Chỉ controllers có explicit `AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme` mới dùng JWT Bearer. `HKDBooksController` có explicit scheme nhưng vẫn 401 — xem root cause 3B.

**Root cause 3B — JWT token có `tenant_id: "system"` (string), không phải GUID:**

`DevLoginController.LoginAsSystemAdmin()` (`5_WebApps/ShopERP/Controllers/DevLoginController.cs` dòng 172-184) issue JWT với:
```csharp
var jwtToken = _jwtTokenService.GenerateToken(
    userId: TestUserId,
    email: "systemadmin@vanan.vn",
    role: UserRole.Owner,  // ← BUG: hardcode Owner, không phải SystemAdmin
    tenantId: TestTenantId);  // ← TestTenantId = 11111111-... (không phải "system")
```

Nhưng JWT payload thực tế decode được:
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

Policy chỉ require claim `tenant_id` tồn tại (không validate format). Nhưng controller dùng `TenantId` để query data — nếu `tenant_id = "system"` (string), parse sang Guid sẽ fail.

**Root cause 3C — HKDBooksController explicit JwtBearer scheme:**

`HKDBooksController` có:
```csharp
[Authorize(Policy = "RequireTenantAccess", AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
```

Yêu cầu JWT Bearer auth. Khi gửi Cookie → reject 401. Khi gửi JWT → parse OK nhưng `tenant_id = "system"` → policy pass (claim tồn tại) → controller chạy → query fail vì tenant_id không phải GUID.

### Giải pháp

**Fix 3A — Gateway: thêm JWT Bearer làm default scheme cho API routes:**

Option 1 (Policy-based): Thêm `AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme` cho tất cả Gateway controllers cần JWT.

Option 2 (Route-based): Cấu hình `app.MapControllerRoute` cho `/api/*` routes dùng JWT Bearer default, `/` routes dùng Cookie.

Option 3 (Recommended): Giữ Cookie default cho Blazor UI, thêm JWT Bearer cho API controllers qua base class hoặc convention:
```csharp
// 2_Gateway/Program.cs — thêm sau AddAuthentication
builder.Services.AddControllers(options =>
{
    // Tất cả controllers trong /api/* dùng JWT Bearer làm default
    options.Conventions.Add(new ApiJwtBearerConvention());
});
```

**Fix 3B — DevLoginController: issue JWT với đúng tenant_id GUID:**

Sau khi impersonate, issue JWT mới với `tenantId = impersonatedTenantId`:
```csharp
// DevLoginController.LoginAsSystemAdmin() — hiện tại hardcode
// Cần: hoặc issue JWT với tenant_id = Guid.Empty (cross-tenant)
// Hoặc: thêm endpoint /dev/login/systemadmin/{tenantId} để impersonate + issue JWT
```

Hoặc sửa `JwtTokenService.GenerateToken()` để accept `tenantId = Guid.Empty` cho SystemAdmin (cross-tenant), và Gateway policy `RequireTenantAccess` bypass cho role=SystemAdmin.

**Fix 3C — HKDBooksController:**

Nếu Fix 3A áp dụng JWT Bearer cho tất cả API controllers, HKDBooksController sẽ tự động dùng JWT. Không cần fix riêng.

**Khuyến nghị:** Fix 3A (Option 3 — convention-based) + Fix 3B (issue JWT với đúng tenant_id sau impersonation). Cần thiết kế `POST /dev/login/systemadmin/{tenantId}` để SystemAdmin lấy JWT cho tenant cụ thể.

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
| **P2** | Nhóm 3A (Gateway JWT scheme) | 3 | Trung bình — convention hoặc per-controller | Trung bình |
| **P2** | Nhóm 3B (JWT tenant_id) | 3 | Trung bình — thêm endpoint hoặc sửa GenerateToken | Trung bình |
| **P3** | Nhóm 2 (AllowAnonymous 401) | 4 | Cần điều tra trước — có thể by design | — |
| **—** | Nhóm 4 (HKDBooks Cookie) | 1 | By design — không fix | — |

**Tổng cộng:** Fix P1 (8 endpoints) + P2 (6 endpoints) = 14 endpoints có thể fix. P3 cần điều tra. Nhóm 4 by design.
