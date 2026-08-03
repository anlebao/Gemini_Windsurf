# PLAN: Trang Admin Device Registration Management cho SystemAdmin

> **Ngày lập:** 2026-08-03
> **Trigger:** Review tài liệu `docs/user-guide/community-commerce` phát hiện gap — Section 9 `01-systemadmin.md` mô tả admin actions (deactivate/verify/update risk score/query fingerprint) nhưng chưa có UI page hay nav link.
> **Mode:** REVIEW_ONLY → chờ approval trước khi IMPLEMENT.
> **Branch đề xuất:** `feat/admin-device-registrations` (tạo mới khi approval).

---

## Context

- **Gap phát hiện**: Tài liệu `01-systemadmin.md` Section 9 mô tả admin actions (deactivate/verify/update risk score/query fingerprint) nhưng chưa có UI page hay nav link.
- **Domain sẵn có**: `DeviceRegistration` entity đã có 3 methods `Deactivate()`, `Verify()`, `UpdateRiskScore(int)` — **KHÔNG cần sửa Domain**.
- **Service sẵn có**: `IDeviceRegistrationService` chỉ có `RegisterDeviceAsync` — cần thêm admin operations.
- **API sẵn có**: Chỉ có `POST /api/customer-identity/device/register` (customer-facing) — cần thêm admin endpoints.
- **UI pattern sẵn có**: `FraudFlags.razor` + `CommunityAdminApiClient` + `AdminLayout` + `NavMenu.razor` (SystemAdmin section).

## Architecture flow (sẽ implement)

```
ShopERP Admin (Blazor Server)
  DeviceRegistrations.razor  →  CommunityAdminApiClient  →  HTTP Bearer JWT
                                                              ↓
Gateway (5001)  →  DeviceRegistrationAdminController  →  IDeviceRegistrationService (admin ops)
                                                              ↓
3_CoreHub  →  DeviceRegistrationService  →  IVanAnDbContext.DeviceRegistrations (PG)
```

## Hard constraints (governance)

- ✅ Domain KHÔNG đổi (entity đã có đủ methods).
- ✅ Layer direction: API → Services → Domain (OK).
- ✅ SystemAdmin policy + JwtBearer auth (giống FraudFlagController).
- ✅ UI Platform components (VanACard, VanAButton, VanAAlert, VanASpinner).
- ✅ Single Source of Truth: không thêm entity mới.
- ✅ Multi-tenancy: DeviceRegistration có `IMustHaveTenant` — query cross-tenant cho SystemAdmin (skip tenant filter).

---

## Phase 1: Service Layer (3_CoreHub)

### 1.1. Extend `IDeviceRegistrationService.cs`

**File**: `3_CoreHub/Services/IDeviceRegistrationService.cs`

Thêm 6 methods (giống pattern `IFraudReviewService`):

```csharp
// List với filter + pagination
Task<DeviceListResult> ListDevicesAsync(
    int page = 1, int pageSize = 20,
    Guid? customerId = null,
    bool? isActive = null,
    bool? isVerified = null,
    string? fingerprintHash = null,  // query anti-fraud: ai khác dùng fingerprint này?
    CancellationToken ct = default);

// Detail 1 device + related FraudFlags
Task<DeviceDetailDto?> GetDeviceDetailAsync(Guid deviceId, CancellationToken ct = default);

// 3 admin actions (wrap entity methods + SaveChanges)
Task DeactivateDeviceAsync(Guid deviceId, Guid adminId, CancellationToken ct = default);
Task VerifyDeviceAsync(Guid deviceId, Guid adminId, CancellationToken ct = default);
Task UpdateRiskScoreAsync(Guid deviceId, int score, Guid adminId, CancellationToken ct = default);

// Stats dashboard
Task<DeviceStatsDto> GetStatsAsync(CancellationToken ct = default);
```

### 1.2. Add DTOs (cuối file `IDeviceRegistrationService.cs` hoặc file mới `DeviceAdminDtos.cs`)

```csharp
public record DeviceListResult(int Total, List<DeviceListItem> Items);
public record DeviceListItem(
    Guid Id, Guid CustomerId, string CustomerName,
    string FingerprintHashShort, string Platform, string IpAddress,
    bool IsActive, bool IsVerified, int RiskScore,
    DateTime FirstSeenAt, DateTime LastSeenAt);
public record DeviceDetailDto(
    DeviceListItem Device,
    string FingerprintHash, string DeviceToken, string UserAgent,
    string FingerprintSignals,  // raw JSON
    List<RelatedFraudFlag> RelatedFlags,
    List<DeviceSharedWith> OtherCustomersWithSameFingerprint);
public record RelatedFraudFlag(Guid Id, string Status, int RiskScore, DateTime CreatedAt);
public record DeviceSharedWith(Guid CustomerId, string CustomerName, DateTime FirstSeenAt);
public record DeviceStatsDto(
    int TotalDevices, int ActiveDevices, int VerifiedDevices,
    int PendingReviewDevices,  // IsActive=false + IsVerified=false
    int HighRiskDevices,       // RiskScore >= 60
    int UniqueFingerprints);
```

### 1.3. Implement trong `DeviceRegistrationService.cs`

**File**: `3_CoreHub/Services/DeviceRegistrationService.cs`

- Query `DeviceRegistrations` cross-tenant (SystemAdmin context — `_tenantProvider.TenantId` có thể là platform tenant, query không filter).
- **Pitfall Pattern #1 (governance)**: dùng `d.TenantId == tenantId` trực tiếp, KHÔNG dùng `EF.Property<Guid>`.
- Join `Customers` để lấy `CustomerName` (cross-tenant — Customers cũng là community entity trên PG).
- Query `OtherCustomersWithSameFingerprint`: `WHERE FingerprintHash == device.FingerprintHash AND CustomerId != device.CustomerId`.
- Query `RelatedFraudFlags`: `WHERE EntityId == deviceId AND EntityType == DeviceRegistration`.
- Wrap entity methods + log admin action + `SaveChangesAsync`.

### 1.4. Tests

**File mới**: `6_Tests/VanAn.Core.Tests/Community/DeviceRegistrationAdminTests.cs`

- Pattern: SQLite in-memory (giống `DeviceRegistrationServiceTests.cs`).
- Test cases:
  1. `ListDevicesAsync_default_returnsAllPaginated`
  2. `ListDevicesAsync_filterByCustomerId`
  3. `ListDevicesAsync_filterByFingerprintHash_returnsOtherCustomers`
  4. `GetDeviceDetailAsync_includesRelatedFlagsAndSharedFingerprints`
  5. `DeactivateDeviceAsync_setsIsActiveFalse`
  6. `VerifyDeviceAsync_setsIsVerifiedTrue`
  7. `UpdateRiskScoreAsync_updatesScore`
  8. `GetStatsAsync_returnsCorrectCounts`
  9. `DeactivateDeviceAsync_notFound_throws`

---

## Phase 2: Gateway Admin API (2_Gateway)

### 2.1. New controller `DeviceRegistrationAdminController.cs`

**File mới**: `2_Gateway/Controllers/DeviceRegistrationAdminController.cs`

Pattern giống `FraudFlagController`:

```csharp
[ApiController]
[Route("api/admin/community")]
[Authorize(Policy = "SystemAdmin", AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
public class DeviceRegistrationAdminController(
    IDeviceRegistrationService deviceService,
    ILogger<DeviceRegistrationAdminController> logger) : ControllerBase
{
    [HttpGet("devices")]
    public async Task<IActionResult> ListDevices(
        [FromQuery] int page = 1, [FromQuery] int pageSize = 20,
        [FromQuery] Guid? customerId = null,
        [FromQuery] bool? isActive = null, [FromQuery] bool? isVerified = null,
        [FromQuery] string? fingerprintHash = null)

    [HttpGet("devices/{id:guid}")]
    public async Task<IActionResult> GetDeviceDetail(Guid id)

    [HttpPost("devices/{id:guid}/deactivate")]
    public async Task<IActionResult> Deactivate(Guid id)

    [HttpPost("devices/{id:guid}/verify")]
    public async Task<IActionResult> Verify(Guid id)

    [HttpPost("devices/{id:guid}/risk-score")]
    public async Task<IActionResult> UpdateRiskScore(Guid id, [FromBody] UpdateRiskScoreRequest req)

    [HttpGet("devices/stats")]
    public async Task<IActionResult> GetStats()

    private Guid GetAdminUserId() { /* copy từ FraudFlagController */ }
}
```

DTOs trong controller:

```csharp
public class UpdateRiskScoreRequest { public int Score { get; set; } }
```

### 2.2. Controller tests

**File mới**: `6_Tests/VanAn.Core.Tests/Community/DeviceRegistrationAdminControllerTests.cs`

- Pattern giống `DeviceRegistrationControllerTests.cs` (nếu có) hoặc `FraudFlagControllerTests.cs`.
- Test: 401 khi không có JWT, 200 khi có JWT, 404 khi id không tồn tại, 200 + verify state change.

### 2.3. DI — KHÔNG cần thêm

`IDeviceRegistrationService` đã register ở `2_Gateway/Program.cs:237`. Reuse.

---

## Phase 3: ShopERP UI (5_WebApps/ShopERP)

### 3.1. Extend `CommunityAdminApiClient.cs`

**File**: `5_WebApps/ShopERP/Services/CommunityAdminApiClient.cs`

Thêm 6 methods (giống `GetFraudFlagsAsync` pattern):

```csharp
public async Task<DeviceListResult> GetDevicesAsync(
    int page = 1, int pageSize = 20,
    Guid? customerId = null, bool? isActive = null,
    bool? isVerified = null, string? fingerprintHash = null,
    CancellationToken ct = default);

public async Task<DeviceDetailDto> GetDeviceDetailAsync(Guid id, CancellationToken ct = default);
public async Task DeactivateDeviceAsync(Guid id, CancellationToken ct = default);
public async Task VerifyDeviceAsync(Guid id, CancellationToken ct = default);
public async Task UpdateDeviceRiskScoreAsync(Guid id, int score, CancellationToken ct = default);
public async Task<DeviceStatsDto> GetDeviceStatsAsync(CancellationToken ct = default);
```

Thêm DTOs cuối file (match Gateway response shape — dùng `System.Text.Json` camelCase).

### 3.2. New page `DeviceRegistrations.razor`

**File mới**: `5_WebApps/ShopERP/Components/Pages/Admin/DeviceRegistrations.razor`

Pattern clone từ `FraudFlags.razor`:

```razor
@page "/admin/device-registrations"
@rendermode InteractiveServer
@layout VanAn.ShopERP.Components.Pages.Admin.AdminLayout
@using VanAn.UI.Platform.Components
@using VanAn.UI.Platform.Components.Composite
@using Microsoft.AspNetCore.Authorization
@using VanAn.ShopERP.Services
@inject CommunityAdminApiClient ApiClient
@inject IThemeProvider ThemeProvider
@inject ILogger<DeviceRegistrations> Logger

@attribute [Authorize(Policy = "SystemAdmin")]
```

**UI sections**:

1. **Header**: title "Device Registration Management" + refresh button.
2. **Filter bar**:
   - Status filter (All / Active / Inactive / Verified / Pending Review / High Risk)
   - CustomerId input (optional)
   - FingerprintHash input (optional — for self-deal detection query)
3. **Stats card** (top): Total / Active / Verified / Pending Review / High Risk / Unique Fingerprints.
4. **Table**: columns theo `DeviceListItem`:
   - Customer (name + id short)
   - Fingerprint (short hash — first 8 chars + tooltip full)
   - Platform / IP
   - Status badges (Active/Inactive + Verified/Pending)
   - RiskScore (color-coded: <60 xanh, 60-79 vàng, ≥80 đỏ — reuse `GetRiskClass` từ FraudFlags)
   - First Seen / Last Seen
   - Actions: Deactivate (nếu Active), Verify (nếu chưa Verified), Update Risk Score (modal), Detail
5. **Detail modal**: full info + related FraudFlags + Other customers with same fingerprint (anti-fraud insight).
6. **Update Risk Score modal**: input number 0-100 + Save.
7. **Pagination**: giống FraudFlags.
8. **Alert**: success/error feedback.

### 3.3. Add nav link trong `NavMenu.razor`

**File**: `5_WebApps/ShopERP/Components/Layout/NavMenu.razor`

Thêm trong SystemAdmin section (sau `admin/collaborator-verification` hoặc gần `Fraud Review`):

```razor
<div class="nav-item px-3">
    <NavLink class="nav-link" href="admin/device-registrations">
        <span class="bi bi-phone-nav-menu" aria-hidden="true"></span> Device Registrations
    </NavLink>
</div>
```

### 3.4. Add menu item trong `AdminLayout.razor`

**File**: `5_WebApps/ShopERP/Components/Pages/Admin/AdminLayout.razor`

Thêm vào `AdminMenuItems` list (gần Fraud Review):

```csharp
new() { Title = "Device Registrations", Icon = "phone", Url = "/admin/device-registrations" },
```

---

## Phase 4: Documentation Update

### 4.1. Update `01-systemadmin.md`

**File**: `docs/user-guide/community-commerce/01-systemadmin.md`

- Section 1 table: thêm row "Device Registrations | `/admin/device-registrations` | Xem/deactivate/verify device fingerprint"
- Section 9: cập nhật mô tả — admin giờ thao tác qua UI page thay vì chỉ API.

### 4.2. Update README.md (optional)

- Section mục lục: không cần (README không list admin pages chi tiết).

---

## Verification Plan

### Build

```powershell
dotnet build VanAn.sln
```

Phải pass 0 errors.

### Tests

```powershell
dotnet test 6_Tests/VanAn.Core.Tests --filter "DeviceRegistrationAdmin"
dotnet test 6_Tests/VanAn.Core.Tests --filter "DeviceRegistrationAdminController"
```

### Manual smoke test (post-build, không chạy tự động)

1. Login SystemAdmin → vào `/admin/device-registrations`.
2. Verify page load + stats hiển thị.
3. Filter theo status → list update.
4. Click Detail → modal hiện related flags + shared fingerprints.
5. Deactivate 1 device → status badge đổi + alert success.
6. Verify 1 device → badge Verified xuất hiện.
7. Update Risk Score → score mới hiển thị.

### Guard check

```powershell
.\guard-check.ps1
```

---

## Risk & Mitigation

| Risk | Mitigation |
|---|---|
| Cross-tenant query leak | SystemAdmin policy đã cross-tenant theo design (giống FraudFlag). KHÔNG cho Owner/Staff xem page này. |
| TenantId converter pitfall (Pattern #1) | Dùng `d.TenantId == tenantId` trực tiếp, KHÔNG `EF.Property<Guid>`. |
| Value object Id query (Pattern #8) | Query `DeviceRegistrations` bằng `d.Id == deviceId` (Guid PK), KHÔNG dùng value object. |
| Performance — fingerprint query full-scan | Đã có index `DeviceToken` unique. Cần thêm index `FingerprintHash` nếu query frequent — **đánh giá sau**: PoC data nhỏ, skip migration. |
| UI Platform compliance | Dùng 100% VanA* components, KHÔNG custom HTML/CSS ngoài style scoped cho badges (giống FraudFlags). |

---

## Scope Boundaries (FIX_ONLY/IMPLEMENT guard)

**IN scope**:

- 6 service methods + DTOs
- 6 controller endpoints
- 1 Razor page + 2 nav updates
- Tests service + controller
- Doc update

**OUT of scope** (KHÔNG làm):

- Thêm EF migration (index FingerprintHash) — defer nếu perf OK.
- Thêm audit log table riêng — admin actions đã log qua `ILogger` + `UpdateAudit()` trên entity.
- Customer-facing device management page (Profile page) — separate task.
- Bulk operations (bulk deactivate/verify) — defer.
- WebSocket real-time update — page dùng refresh button.

---

## File Change Summary

| File | Action | LOC estimate |
|---|---|---|
| `3_CoreHub/Services/IDeviceRegistrationService.cs` | Edit (extend) | +60 |
| `3_CoreHub/Services/DeviceRegistrationService.cs` | Edit (extend) | +180 |
| `2_Gateway/Controllers/DeviceRegistrationAdminController.cs` | **New** | ~120 |
| `5_WebApps/ShopERP/Services/CommunityAdminApiClient.cs` | Edit (extend) | +80 |
| `5_WebApps/ShopERP/Components/Pages/Admin/DeviceRegistrations.razor` | **New** | ~280 |
| `5_WebApps/ShopERP/Components/Layout/NavMenu.razor` | Edit (+1 nav item) | +5 |
| `5_WebApps/ShopERP/Components/Pages/Admin/AdminLayout.razor` | Edit (+1 menu item) | +1 |
| `6_Tests/VanAn.Core.Tests/Community/DeviceRegistrationAdminTests.cs` | **New** | ~250 |
| `6_Tests/VanAn.Core.Tests/Community/DeviceRegistrationAdminControllerTests.cs` | **New** | ~150 |
| `docs/user-guide/community-commerce/01-systemadmin.md` | Edit (Section 1 + 9) | +10 |

**Total**: ~1140 LOC, 3 files mới, 7 files edit.

---

## Execution Order

1. **Phase 1** (Service + tests) → build + test pass.
2. **Phase 2** (Controller + tests) → build + test pass.
3. **Phase 3** (UI) → build pass + manual smoke.
4. **Phase 4** (Doc) → commit.

Mỗi phase là 1 commit riêng. **KHÔNG push** cho đến khi user approve.

---

## Open Questions (chờ user quyết định trước khi IMPLEMENT)

1. **Tenant filter cho SystemAdmin**: SystemAdmin cross-tenant theo design — confirm query `DeviceRegistrations` không filter theo tenant? (FraudFlag controller đã làm vậy.)
2. **Stats scope**: `GetStatsAsync` trả tổng toàn platform hay per-tenant? Đề xuất: toàn platform (SystemAdmin view).
3. **Risk Score input range**: validate 0-100 ở controller hay chỉ ở UI? Đề xuất: cả hai.
4. **Audit log**: admin actions (deactivate/verify/update score) có cần ghi vào bảng audit riêng không, hay `ILogger` + `UpdateAudit()` trên entity là đủ? Đề xuất: đủ cho PoC.
5. **Index FingerprintHash**: có muốn thêm EF migration luôn (Phase 1.5) hay defer? Đề xuất: defer — PoC data nhỏ.
