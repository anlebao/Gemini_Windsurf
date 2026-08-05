# TASK CARD: Loyalty Points Visibility + Shop Owner Loyalty Dashboard

## 1. GOAL & CONTEXT

- **Mục tiêu cốt lõi:**
  1. Backend: Check `Loyalty_Program_Enabled` toggle trước khi tích điểm — tenant tắt loyalty thì không tích.
  2. API: Trả `PointsAwarded` + `LoyaltyEnabled` trong `PublicOrderTrackingDto` để KhachLink hiển thị.
  3. Frontend: Hiển thị banner điểm thưởng trên `OrderTracking.razor` khi đơn completed/delivered.
  4. Frontend: Hiển thị ước tính điểm trên `Checkout.razor` sau khi đặt hàng thành công.
  5. **Phase 3 (Option A — APPROVED):** Alliance mode dùng fixed rate `1 điểm = 1000 VND` — bỏ per-tenant rate. Convert existing balances khi Silo→Alliance migration.
  6. **Phase 5 (NEW):** Shop owner dashboard 4 chỉ số: điểm chờ đổi, đã đổi, điểm CTKM chờ thưởng, dự trù điểm thưởng.
- **Nghiệp vụ:**
  - Khách hàng (đăng nhập hoặc guest) thấy số điểm thưởng nhận được sau khi đơn hàng hoàn thành. Điều kiện: tenant có bật `Loyalty_Program_Enabled`. Alliance mode: 1 điểm = 1000 VND (global constant, configurable).
  - Shop owner thấy thống kê điểm thưởng để quản trị tài chính loyalty + đánh giá hiệu quả CTKM.
- **Status:** 🔲 PENDING APPROVAL — Chờ user approve IMPLEMENT
- **Branch:** `main` (sẽ commit trực tiếp — không tạo branch riêng)
- **Dependency:** #99-2 (double-award guard) đã COMPLETE
- **Master plan:** `docs/AI/tasks/loyalty_points_visibility_master_plan.md`
- **Rollout strategy:** Safe Incremental Rollout with Feature Gate
  - **Phase A** (Batch 1+3): Customer visibility + Shop owner dashboard — LOW-MEDIUM risk, activate ngay
  - **Phase B** (Batch 2): Alliance VND normalization — HIGH risk, feature-gated (chỉ activate khi `Mode=Alliance`)

---

## 2. ACTIVE WORKFLOW ROUTING

- **Target Workflow:** `newfeaturebuild.md` (ANALYZE → IMPLEMENT)
- **Execution Mode:** ANALYZE COMPLETE → chờ APPROVE → IMPLEMENT (Phase A → Phase B)
- **Current Phase:** ANALYZE (review + plan done, awaiting approval)
- **Dependency:** None (existing loyalty infrastructure intact)

---

## 3. RELEVANT FILES (CONTEXT BOUNDARY)

### Files được phép MODIFY

| # | File | Phase | Mô tả thay đổi |
|---|------|-------|----------------|
| 1 | `3_CoreHub/Services/OrderWorkflowService.cs` | 1 | Thêm check `Loyalty_Program_Enabled` ở đầu `ProcessLoyaltyPointsAsync` (sau line 350, trước double-award guard) |
| 2 | `1_Shared/DTOs/PublicOrderTrackingDto.cs` | 1 | Thêm 2 field: `int? PointsAwarded`, `bool LoyaltyEnabled = true` |
| 3 | `2_Gateway/Controllers/PublicOrdersController.cs` | 1 | Trong `GetPublicOrder` (line 314): query `IsEnabledAsync(tenantId, "Loyalty_Program_Enabled")` + parse loyalty history → set 2 field mới |
| 4 | `5_WebApps/KhachLink/Pages/OrderTracking.razor` | 2 | Thêm banner điểm thưởng sau status steps card (khi completed/delivered + PointsAwarded > 0) |
| 5 | `5_WebApps/KhachLink/Pages/Checkout.razor` | 2 | Sau line 504: ước tính điểm + thông báo (nếu LoyaltyEnabled) |

### Files READ ONLY (investigate patterns)

| # | File | Mục đích |
|---|------|----------|
| 1 | `1_Shared/Services/IShopFeatureSettingsService.cs` | Check `IsEnabledAsync` signature + `ShopFeatureSettingsDto.Loyalty_Program_Enabled` default |
| 2 | `3_CoreHub/Services/ShopFeatureSettingsService.cs` | Implementation pattern — how settings loaded from DB |
| 3 | `5_WebApps/KhachLink/Services/Http/ShopFeatureSettingsHttpService.cs` | KhachLink HTTP pattern for tenant settings |
| 4 | `5_WebApps/KhachLink/Pages/LoyaltyCard.razor` | Existing loyalty display pattern (tier, points, history) |
| 5 | `2_Gateway/Controllers/PublicOrdersController.cs` | Existing DTO mapping pattern in `GetPublicOrder` |

### Boundary Rules

- ❌ KHÔNG sửa `1_Shared/Domain.cs` — chỉ thêm field DTO, không đụng Domain entity
- ❌ KHÔNG tạo migration — dùng existing tables (`ShopFeatureSettings`, `LoyaltyRewards`)
- ❌ KHÔNG inject CoreHub services vào KhachLink — dùng HTTP qua Gateway API
- ❌ KHÔNG tạo UI custom HTML/CSS — dùng existing KhachLink styling pattern
- ❌ KHÔNG sửa `Order` entity hoặc `OrderStatusId` — dùng existing status flow
- ✅ Dùng `IShopFeatureSettingsService.IsEnabledAsync` (đã có sẵn, default=true)
- ✅ Fail-open: nếu service lỗi → default `LoyaltyEnabled = true` (tiếp tục tích điểm)

---

## 4. IMPLEMENTATION TASKS

### Phase 1: Backend (3 tasks)

#### Task 1.1: Check `Loyalty_Program_Enabled` trong `ProcessLoyaltyPointsAsync`

**File:** `3_CoreHub/Services/OrderWorkflowService.cs`
**Location:** Sau line 350 (sau khi resolve customer, trước double-award guard)
**Thay đổi:** Thêm block check `Loyalty_Program_Enabled`:

```csharp
// #99-3: Check Loyalty_Program_Enabled toggle — tenant can disable loyalty entirely.
if (_shopFeatureSettingsService != null && order.TenantId.HasValue)
{
    try
    {
        bool loyaltyEnabled = await _shopFeatureSettingsService.IsEnabledAsync(
            order.TenantId.Value,
            nameof(ShopFeatureSettingsDto.Loyalty_Program_Enabled));
        if (!loyaltyEnabled)
        {
            _logger.LogInformation("Loyalty: Skipped award for order {OrderId} — Loyalty_Program_Enabled=false for tenant {TenantId}",
                order.Id, order.TenantId.Value);
            return;
        }
    }
    catch (Exception ex)
    {
        _logger.LogWarning(ex, "Failed to check Loyalty_Program_Enabled for tenant {TenantId} — proceeding (default=true)", order.TenantId);
    }
}
```

**Verify:** Build pass. Unit test: tenant tắt loyalty → `ProcessLoyaltyPointsAsync` return sớm, không gọi `AddPointsAsync`.

#### Task 1.2: Thêm field `PointsAwarded` + `LoyaltyEnabled` vào `PublicOrderTrackingDto`

**File:** `1_Shared/DTOs/PublicOrderTrackingDto.cs`
**Thay đổi:** Thêm 2 property:

```csharp
/// <summary>#99-3: Points awarded for this order (null = not yet awarded; >0 = awarded).</summary>
public int? PointsAwarded { get; init; }

/// <summary>#99-3: Whether tenant has loyalty program enabled.</summary>
public bool LoyaltyEnabled { get; init; } = true;
```

**Verify:** Build pass. DTO có 2 field mới.

#### Task 1.3: Query loyalty history + tenant settings trong `GetPublicOrder`

**File:** `2_Gateway/Controllers/PublicOrdersController.cs`
**Location:** Trong `GetPublicOrder` (line 314), sau khi map Order → DTO
**Thay đổi:**

1. Inject `IShopFeatureSettingsService` vào controller (nếu chưa có)
2. Query `IsEnabledAsync(tenantId, "Loyalty_Program_Enabled")` → set `LoyaltyEnabled`
3. Query loyalty history:
   - Nếu order có `CustomerId` → `_loyaltyRewardsService.GetCustomerRewardsAsync(customerId)`
   - Parse history JSON → tìm entry `Type == "EARN"` && `Reason.Contains($"#{order.Id}")`
   - Nếu tìm thấy → set `PointsAwarded = entry.Points`
   - Nếu không tìm thấy hoặc lỗi → `PointsAwarded = null`
4. **Edge case:** Alliance mode — query `AllianceTransactions` thay vì `LoyaltyRewards` (nếu tenant là alliance member)

**Verify:** Build pass. API `GET /api/public/orders/{id}` trả `pointsAwarded` + `loyaltyEnabled` trong JSON response.

### Phase 2: Frontend (2 tasks)

#### Task 2.1: Banner điểm thưởng trên `OrderTracking.razor`

**File:** `5_WebApps/KhachLink/Pages/OrderTracking.razor`
**Location:** Sau status steps card (sau line ~78), trước payment status card
**Thay đổi:**

1. Thêm field `_pointsAwarded` + `_loyaltyEnabled` vào `@code` block
2. Parse từ `_orderData` (PublicOrderTrackingDto response) trong `LoadOrderAsync`
3. Thêm banner HTML:

```razor
@if ((_orderStatus == "completed" || _orderStatus == "delivered") && _pointsAwarded > 0)
{
    <div class="alert alert-success loyalty-points-banner" data-testid="loyalty-points-banner">
        <span class="bi bi-gift-fill me-2"></span>
        Bạn đã nhận được <strong>@_pointsAwarded điểm thưởng</strong> từ đơn hàng này!
        <a href="/my-loyalty" class="alert-link ms-2">Xem điểm của tôi →</a>
    </div>
}
```

4. CSS: dùng existing `alert alert-success` (Bootstrap) — không tạo CSS riêng

**Verify:** Mở `/order-tracking/{id}` với order completed có điểm → banner hiện. Order pending → banner không hiện. Tenant tắt loyalty → banner không hiện.

#### Task 2.2: Ước tính điểm trên `Checkout.razor`

**File:** `5_WebApps/KhachLink/Pages/Checkout.razor`
**Location:** Sau line 504 (sau `_showLoyaltySignupModal = true`), trước cash flow redirect
**Thay đổi:**

1. Thêm field `_estimatedPoints` + `_loyaltyEnabled` vào `@code` block
2. Trong `OnAfterRenderAsync` (firstRender): query `ShopFeatureSettingsHttpService.GetSettingsAsync(tenantId)` → check `Loyalty_Program_Enabled` + `Loyalty_PointsRate`
3. Sau khi order tạo thành công: tính `_estimatedPoints = (int)(orderTotal * rate)`
4. Hiển thị thông báo:

```razor
@if (_loyaltyEnabled && _estimatedPoints > 0 && orderCreated)
{
    <div class="alert alert-info mt-3" data-testid="loyalty-estimate-banner">
        <span class="bi bi-info-circle me-2"></span>
        Đơn hàng của bạn sẽ được tích <strong>~@_estimatedPoints điểm thưởng</strong> khi hoàn thành.
    </div>
}
```

**Verify:** Đặt hàng thành công → thông báo ước tính hiện. Tenant tắt loyalty → không hiện.

---

## 5. VERIFY CHECKLIST (RV)

| # | Test | Cách | Pass criteria |
|---|------|------|---------------|
| V1 | Tenant tắt loyalty → không tích | ShopERP tắt `Loyalty_Program_Enabled`, hoàn thành order | Gateway logs: "Skipped award — Loyalty_Program_Enabled=false" |
| V2 | Tenant bật loyalty → tích điểm | Hoàn thành order (default toggle=true) | Gateway logs: "Awarded {Points} points" |
| V3 | API trả `PointsAwarded` | `GET /api/public/orders/{id}` với order completed | JSON có `pointsAwarded > 0` |
| V4 | API trả `LoyaltyEnabled` | `GET /api/public/orders/{id}` | JSON có `loyaltyEnabled: true/false` |
| V5 | OrderTracking banner | Mở `/order-tracking/{id}`, order completed | Banner "Bạn đã nhận được X điểm thưởng" hiện |
| V6 | Guest checkout banner | Guest đặt hàng, hoàn thành, mở tracking | Banner hiện (DeviceId-based loyalty) |
| V7 | Tenant tắt loyalty → banner ẩn | Tắt loyalty, hoàn thành order, mở tracking | Banner không hiện |
| V8 | Checkout ước tính | Đặt hàng thành công | Thông báo "Đơn hàng sẽ được tích ~X điểm" hiện |
| V9 | Build pass | `dotnet build VanAn.sln` | 0 errors |
| V10 | Unit tests pass | `dotnet test` | 0 failed (excluding pre-existing) |

---

## 6. RỦI RO

| # | Rủi ro | Mitigation |
|---|--------|------------|
| 1 | Loyalty history JSON parse fail | Fail-safe: `PointsAwarded = null`, banner không hiện |
| 2 | `ShopFeatureSettingsService` lỗi | Fail-open: `LoyaltyEnabled = true` (default), tích điểm bình thường |
| 3 | Alliance mode query khác Silo | Phase 1.3: check mode, query đúng table (AllianceTransactions vs LoyaltyRewards) |
| 4 | Performance: thêm 2 query per GET order | `IsEnabledAsync` cached; loyalty history query chỉ khi order completed/delivered |
| 5 | Multi-tenant checkout (Phase 5) | Mỗi order có 1 tenant → check per-order, đúng |

---

## 7. GOVERNANCE COMPLIANCE

- [x] Không sửa `Domain.cs` — chỉ DTO + Service logic
- [x] Không tạo migration — dùng existing tables
- [x] Không inject CoreHub services vào KhachLink — dùng HTTP
- [x] Multi-tenancy: check toggle per-tenant, per-order
- [x] Layer boundaries: DTO → Service → API → Client
- [x] AccountingEntry: không liên quan
- [x] UI Platform: dùng Bootstrap `alert` (existing pattern, không custom CSS)

---

## 8. PHASE 3: Alliance Mode — Normalize VND (Option A — APPROVED)

**User decision:** Option A (1.000 VND = 1 điểm), gộp vào plan này.

### Tasks

#### Task 3.1: `LoyaltyGlobalConfig` — Thêm `VndPerPoint` field

**File:** `1_Shared/Domain.cs` (line 2143)
**Thay đổi:**
```csharp
public int VndPerPoint { get; protected set; } = 1000; // Option A: 1 điểm = 1000 VND (Alliance mode)

public void UpdateVndPerPoint(int vndPerPoint, string changedBy)
{
    VndPerPoint = vndPerPoint > 0 ? vndPerPoint : 1000;
    LastChangedAt = DateTime.UtcNow;
    LastChangedBy = changedBy;
    UpdateAudit();
}
```
**Verify:** Build pass. `LoyaltyGlobalConfig.VndPerPoint` default = 1000.

#### Task 3.2: `LoyaltyModeResolver` — Thêm `GetVndPerPointAsync()`

**File:** `3_CoreHub/Services/LoyaltyModeResolver.cs`
**Thay đổi:**
```csharp
public async Task<int> GetVndPerPointAsync()
{
    LoyaltyGlobalConfig globalCfg = await GetOrCreateGlobalConfigAsync();
    return globalCfg.VndPerPoint > 0 ? globalCfg.VndPerPoint : 1000;
}
```
**Verify:** Build pass. Method returns 1000 by default.

#### Task 3.3: `ProcessLoyaltyPointsAsync` — Alliance fixed rate

**File:** `3_CoreHub/Services/OrderWorkflowService.cs` (line 386)
**Thay đổi:**
```csharp
// Option A: Alliance mode = fixed VndPerPoint. Silo mode = per-tenant rate (unchanged).
int pointsToAward;
bool isAllianceMode = _loyaltyModeResolver != null
    && await _loyaltyModeResolver.GetEffectiveModeAsync(order.TenantId.Value) == LoyaltyMode.Alliance
    && await _loyaltyModeResolver.IsAllianceMemberAsync(order.TenantId.Value);

if (isAllianceMode)
{
    int vndPerPoint = await _loyaltyModeResolver.GetVndPerPointAsync();
    pointsToAward = (int)(order.TotalAmount / vndPerPoint);
}
else
{
    pointsToAward = (int)(order.TotalAmount * rate); // existing Silo flow
}
pointsToAward = Math.Max(minPoints, pointsToAward);
if (maxPoints.HasValue) pointsToAward = Math.Min(maxPoints.Value, pointsToAward);
```
**Verify:** Alliance mode: mua 100.000đ → 100 điểm (100.000/1000). Silo mode: unchanged.

#### Task 3.4: `ConsolidateWalletsAsync` — Convert Silo→Alliance

**File:** `3_CoreHub/Services/AllianceWalletService.cs` (line 238)
**Thay đổi:**
```csharp
// Option A: Convert Silo points to VND-equivalent before adding to Alliance wallet.
// vndValue = siloPoints / tenantRate; alliancePoints = vndValue / vndPerPoint
int vndPerPoint = await GetVndPerPointAsync();
decimal tenantRate = await GetTenantLoyaltyRateAsync(tenantId);
if (tenantRate <= 0) tenantRate = 0.001m; // fallback default

decimal vndValue = input.PointBalance / tenantRate;
int alliancePoints = (int)(vndValue / vndPerPoint);
wallet.AddPoints(alliancePoints);
```
**Verify:** Tenant A (rate=0.001, 100 điểm): vndValue=100.000, alliancePoints=100. Tenant B (rate=0.0005, 50 điểm): vndValue=100.000, alliancePoints=100.

#### Task 3.5: `SplitWalletsAsync` — Convert Alliance→Silo

**File:** `3_CoreHub/Services/AllianceWalletService.cs` (line 308)
**Thay đổi:**
```csharp
// Option A: Convert Alliance points back to Silo points per-tenant.
// vndValue = alliancePoints * vndPerPoint; siloPoints = vndValue * tenantRate
decimal vndValue = allocatedAlliancePoints * vndPerPoint;
int siloPoints = (int)(vndValue * tenantRate);
```
**Verify:** Alliance 100 điểm → VND 100.000 → Tenant A (rate=0.001): 100 điểm. Tenant B (rate=0.0005): 50 điểm.

#### Task 3.6: `LoyaltyConfigController` — API `VndPerPoint`

**File:** `2_Gateway/Controllers/LoyaltyConfigController.cs`
**Thay đổi:**
- `GlobalConfigDto`: thêm `int VndPerPoint`
- `UpdateGlobalConfigRequest`: thêm `int VndPerPoint`
- `UpdateGlobalConfig`: gọi `config.UpdateVndPerPoint(body.VndPerPoint, changedBy)`

**Verify:** `GET /api/platform/loyalty/config` trả `vndPerPoint: 1000`. `PUT` cập nhật được.

#### Task 3.7: `LoyaltyConfigAdmin.razor` — UI input

**File:** `5_WebApps/ShopERP/Components/Pages/Admin/LoyaltyConfigAdmin.razor`
**Thay đổi:**
- Thêm input "VND per point (Alliance mode)" — chỉ hiện khi `Mode = Alliance`
- Label: "Số VND tương đương 1 điểm thưởng (Alliance mode). Default: 1000"
- Bind to `_globalForm.VndPerPoint`

**Verify:** UI hiện input khi Alliance mode. Save → API update.

#### Task 3.8: Migration `AddVndPerPointColumn`

**File:** `3_CoreHub/Infrastructure/Migrations/` (new)
**Thay đổi:**
```csharp
migrationBuilder.AddColumn<int>(
    name: "VndPerPoint",
    table: "LoyaltyGlobalConfigs",
    type: "integer",
    nullable: false,
    defaultValue: 1000);
```
**Verify:** Migration apply. PG column exists with default 1000.

#### Task 3.9: Convert `RedemptionCatalogItem.PointsRequired` khi Silo→Alliance

**File:** `3_CoreHub/Services/AllianceWalletService.cs` (thêm vào `ConsolidateWalletsAsync` hoặc method riêng)
**Vấn đề:** `RedemptionCatalogItem.PointsRequired` được admin set theo Silo rate cũ. Khi switch Alliance (1 điểm = 1000 VND), `PointsRequired` cũ sai giá trị VND.

**Kịch bản lỗi:**
```
Tenant B (Silo, rate=0.0005, 1 điểm = 2000 VND):
  Admin set "Giảm 100.000đ" = 50 điểm (50 × 2000 = 100.000đ ✓ Silo)

Switch sang Alliance (1 điểm = 1000 VND):
  Cùng 50 điểm trong wallet = 50.000đ (50 × 1000) — SAI! Khách mất 50.000đ giá trị
  Phải là 100 điểm (100 × 1000 = 100.000đ) — giữ nguyên giá trị VND
```

**Thay đổi:** Khi `ConsolidateWalletsAsync` chạy, convert tất cả active catalog items:
```csharp
// Task 3.9: Convert RedemptionCatalogItem.PointsRequired từ Silo rate sang Alliance rate.
// Formula: newPointsRequired = oldPointsRequired × tenantRate / vndPerPoint
// VD: Tenant B, 50 điểm (Silo, 1đ=2000đ) → 50 × 2000 / 1000 = 100 điểm (Alliance, 1đ=1000đ)
// Giữ nguyên giá trị VND của voucher — khách không mất giá trị khi switch mode.
int vndPerPoint = await GetVndPerPointAsync();
decimal tenantRate = await GetTenantLoyaltyRateAsync(tenantId);
if (tenantRate <= 0) tenantRate = 0.001m; // fallback default

var activeCatalogItems = await _dbContext.RedemptionCatalogItems
    .Where(c => c.TenantId.Value == tenantId && c.IsActive)
    .ToListAsync();

foreach (var item in activeCatalogItems)
{
    int newPointsRequired = (int)(item.PointsRequired * tenantRate / vndPerPoint * 1000);
    // × 1000 vì tenantRate là decimal (0.001 = 1 điểm/1000đ), VndPerPoint là int (1000)
    // Thực tế: newPoints = oldPoints × (1/tenantRate_VND) / vndPerPoint
    //         = oldPoints × tenantRate_VND / vndPerPoint
    // Trong đó tenantRate_VND = 1/tenantRate (VND per point trong Silo)
    // VD: rate=0.0005 → 1/0.0005 = 2000 VND/point → newPoints = 50 × 2000 / 1000 = 100
    item.UpdateDetails(item.ProductName, item.Description, item.ImageUrl,
        newPointsRequired, item.StockCount, item.ValidTo, item.VoucherExpiryDays);
}

await _dbContext.SaveChangesAsync();
_logger.LogInformation("Task 3.9: Converted {Count} catalog items for tenant {TenantId} (Silo→Alliance, VndPerPoint={VndPerPoint})",
    activeCatalogItems.Count, tenantId, vndPerPoint);
```

**Edge cases:**
- Tenant chưa config rate (default 0) → dùng fallback 0.001 (1 điểm = 1000đ, không cần convert)
- Catalog item inactive → skip (không ảnh hưởng)
- `PointsRequired` mới = 0 (order TotalAmount quá nhỏ) → giữ minimum 1 điểm

**Verify:**
- Tenant B (rate=0.0005): "Giảm 100.000đ" = 50 điểm (Silo) → 100 điểm (Alliance) — giá trị VND giữ nguyên 100.000đ
- Tenant A (rate=0.001): "Giảm 50.000đ" = 50 điểm (Silo) → 50 điểm (Alliance) — không đổi (rate đã = VndPerPoint)

---

## 9. VERIFY CHECKLIST (Phase 3)

| # | Test | Cách | Pass criteria |
|---|------|------|---------------|
| V7 | Alliance: 1 điểm = 1000 VND | Switch Alliance, mua 100.000đ | earn 100 điểm (100.000/1000) |
| V8 | Silo→Alliance convert | Tenant A (rate=0.001, 100đ) + Tenant B (rate=0.0005, 50đ) migrate | Cả 2 → 100 điểm Alliance (cùng VND value) |
| V9 | Alliance→Silo convert | Alliance 100đ split → Tenant A: 100đ, Tenant B: 50đ | Đúng per-tenant rate |
| V10 | `VndPerPoint` configurable | SystemAdmin set = 500 | API + UI update, earn 200 điểm/100.000đ |
| V11 | Silo mode unchanged | Tenant Silo, rate=0.0005, mua 100.000đ | 50 điểm (per-tenant rate, not affected) |
| V12 | Catalog convert Silo→Alliance | Tenant B (rate=0.0005), "Giảm 100.000đ" = 50 điểm (Silo) → switch Alliance | `PointsRequired = 100` (100×1000=100.000đ, giữ nguyên VND value) |
| V12 | Migration apply | `dotnet ef database update` | Column `VndPerPoint` exists, default 1000 |

---

---

## 9. PHASE 5: Shop Owner Loyalty Dashboard (ShopERP) — NEW

Shop owner thấy 4 chỉ số điểm thưởng trên dashboard.

### 4 Chỉ số

| # | Chỉ số | Formula | Ý nghĩa |
|---|--------|---------|---------|
| 1 | Điểm chờ đổi | `SUM(LoyaltyRewards.PointBalance)` (Silo) hoặc `SUM(AllianceWallet.TotalPointBalance)` (Alliance) | Tổng điểm khách đang có, chưa redeem |
| 2 | Đã đổi | `SUM(RedemptionRecord.PointsSpent)` WHERE Status=Fulfilled | Tổng điểm đã đổi thành voucher |
| 3 | Điểm CTKM chờ thưởng | `SUM(Order.TotalAmount × rate)` cho pending orders có TrackingCode | Điểm sẽ trả cho CTKM đang chạy |
| 4 | Dự trù điểm thưởng | `SUM(Order.TotalAmount × rate)` cho ALL pending orders | Tổng dự trù tài chính loyalty |

### Tasks

#### Task 5.1: `LoyaltyController` — `GET /api/loyalty/dashboard`

**File:** `5_WebApps/ShopERP/Controllers/LoyaltyController.cs`
**Thay đổi:** Thêm endpoint `GET /api/loyalty/dashboard` (Authorize — shop owner only)

```csharp
[HttpGet("dashboard")]
[Authorize] // Shop owner only — not [AllowAnonymous] like /my
public async Task<ActionResult<LoyaltyDashboardStats>> GetDashboard()
{
    Guid tenantId = _tenantProvider.TenantId;
    decimal rate = await GetTenantLoyaltyRateAsync(tenantId);

    // Chỉ số 1: Points pending redemption
    int pendingRedemption = await _dbContext.LoyaltyRewards
        .Where(lr => lr.TenantId.Value == tenantId && lr.IsActive)
        .SumAsync(lr => lr.PointBalance);

    // Chỉ số 2: Points redeemed (Fulfilled only — Cancelled already refunded)
    int redeemed = await _dbContext.RedemptionRecords
        .Where(r => r.TenantId.Value == tenantId && r.Status == "Fulfilled")
        .SumAsync(r => r.PointsSpent);

    // Chỉ số 3: Points in active campaigns (pending orders with TrackingCode)
    var campaignOrders = await _dbContext.Orders
        .Where(o => o.TenantId.Value == tenantId
            && o.TrackingCode != null
            && o.Status.Value != "completed" && o.Status.Value != "cancelled"
            && o.Status.Value != "delivered")
        .Select(o => o.TotalAmount)
        .ToListAsync();
    int pointsInCampaigns = campaignOrders.Sum(a => (int)(a * rate));

    // Chỉ số 4: Points reserved (ALL pending orders)
    var allPendingOrders = await _dbContext.Orders
        .Where(o => o.TenantId.Value == tenantId
            && o.Status.Value != "completed" && o.Status.Value != "cancelled"
            && o.Status.Value != "delivered")
        .Select(o => o.TotalAmount)
        .ToListAsync();
    int pointsReserved = allPendingOrders.Sum(a => (int)(a * rate));

    return Ok(new LoyaltyDashboardStats
    {
        PointsPendingRedemption = pendingRedemption,
        PointsRedeemed = redeemed,
        PointsInCampaigns = pointsInCampaigns,
        PointsReserved = pointsReserved
    });
}
```

**DTO:**
```csharp
public class LoyaltyDashboardStats
{
    public int PointsPendingRedemption { get; set; }
    public int PointsRedeemed { get; set; }
    public int PointsInCampaigns { get; set; }
    public int PointsReserved { get; set; }
}
```

**Verify:** `GET /api/loyalty/dashboard` trả 4 chỉ số đúng.

#### Task 5.2: `LoyaltyDashboard.razor` — UI dashboard

**File:** `5_WebApps/ShopERP/Components/Pages/Loyalty/LoyaltyDashboard.razor` (NEW)
**Route:** `/loyalty/dashboard`

```razor
@page "/loyalty/dashboard"
<PageTitle>Thống kê điểm thưởng</PageTitle>

<div class="row g-3 mb-4">
    <div class="col-md-3">
        <VanAnCard Shadow="true">
            <div class="stat-card">
                <i class="bi bi-gift-fill text-primary fs-2"></i>
                <h3>@_stats.PointsPendingRedemption.ToString("N0")</h3>
                <p>Điểm chờ đổi</p>
            </div>
        </VanAnCard>
    </div>
    <div class="col-md-3">
        <VanAnCard Shadow="true">
            <div class="stat-card">
                <i class="bi bi-check-circle-fill text-success fs-2"></i>
                <h3>@_stats.PointsRedeemed.ToString("N0")</h3>
                <p>Đã đổi</p>
            </div>
        </VanAnCard>
    </div>
    <div class="col-md-3">
        <VanAnCard Shadow="true">
            <div class="stat-card">
                <i class="bi bi-megaphone-fill text-warning fs-2"></i>
                <h3>@_stats.PointsInCampaigns.ToString("N0")</h3>
                <p>Điểm CTKM chờ thưởng</p>
            </div>
        </VanAnCard>
    </div>
    <div class="col-md-3">
        <VanAnCard Shadow="true">
            <div class="stat-card">
                <i class="bi bi-piggy-bank-fill text-danger fs-2"></i>
                <h3>@_stats.PointsReserved.ToString("N0")</h3>
                <p>Dự trù điểm thưởng</p>
            </div>
        </VanAnCard>
    </div>
</div>
```

**Verify:** Mở `/loyalty/dashboard` → 4 cards hiển thị đúng số liệu.

#### Task 5.3: `NavMenu.razor` — Link dashboard

**File:** `5_WebApps/ShopERP/Components/Layout/NavMenu.razor`
**Thay đổi:** Thêm nav link:
```razor
<NavLink href="/loyalty/dashboard">
    <i class="bi bi-gift"></i> Thống kê điểm thưởng
</NavLink>
```

**Verify:** NavMenu hiện link → click → navigate to dashboard.

---

## 10. VERIFY CHECKLIST (Phase 5)

| # | Test | Cách | Pass criteria |
|---|------|------|---------------|
| V11 | Dashboard: Điểm chờ đổi | 3 khách có 100/200/300 điểm | `PointsPendingRedemption = 600` |
| V12 | Dashboard: Đã đổi | Redeem 50 điểm (Fulfilled) | `PointsRedeemed = 50` |
| V13 | Dashboard: Điểm CTKM chờ thưởng | 2 orders pending có TrackingCode, 50.000+100.000, rate=0.001 | `PointsInCampaigns = 150` |
| V14 | Dashboard: Dự trù điểm thưởng | 3 orders pending (2 có + 1 không TrackingCode), 50.000+100.000+80.000, rate=0.001 | `PointsReserved = 230` |

---

## 11. CHIẾN LƯỢC THỰC THI: Safe Incremental Rollout with Feature Gate

### Phase A: Batch 1 + Batch 3 (gộp — 1 commit, LOW-MEDIUM risk, activate ngay)

| Step | Task | File | Risk |
|------|------|------|------|
| A1 | P1.1: Check `Loyalty_Program_Enabled` | `OrderWorkflowService.cs` | LOW |
| A2 | P1.2: Thêm `PointsAwarded` + `LoyaltyEnabled` | `PublicOrderTrackingDto.cs` | LOW |
| A3 | P1.3: Query loyalty history + tenant settings | `PublicOrdersController.cs` | LOW |
| A4 | P2.1: Banner điểm thưởng | `OrderTracking.razor` | LOW |
| A5 | P2.2: Ước tính điểm | `Checkout.razor` | LOW |
| A6 | P5.1: `GET /api/loyalty/dashboard` | `LoyaltyController.cs` | MEDIUM |
| A7 | P5.2: LoyaltyDashboard.razor | NEW file | MEDIUM |
| A8 | P5.3: NavMenu link | `NavMenu.razor` | LOW |
| A9 | **Build + test + commit + push + RV** | — | — |

**RV Phase A:** V1-V6 (customer visibility) + V11-V14 (dashboard)
**Activate ngay:** ✅ Có

### Phase B: Batch 2 (riêng — 1-2 commits, HIGH risk, feature-gated)

| Step | Task | File | Risk |
|------|------|------|------|
| B1 | P3.1: `LoyaltyGlobalConfig.VndPerPoint` | `Domain.cs` | HIGH |
| B2 | P3.7: Migration `AddVndPerPointColumn` | Migrations/ | HIGH |
| B3 | P3.2: `GetVndPerPointAsync()` | `LoyaltyModeResolver.cs` | MEDIUM |
| B4 | P3.3: Alliance fixed rate (feature-gated) | `OrderWorkflowService.cs` | HIGH |
| B5 | P3.4: `ConsolidateWalletsAsync` convert | `AllianceWalletService.cs` | HIGH |
| B6 | P3.5: `SplitWalletsAsync` convert | `AllianceWalletService.cs` | HIGH |
| B7 | P3.9: Convert `RedemptionCatalogItem.PointsRequired` | `AllianceWalletService.cs` | HIGH |
| B8 | P3.6: API `VndPerPoint` field | `LoyaltyConfigController.cs` | MEDIUM |
| B9 | P3.6: UI input | `LoyaltyConfigAdmin.razor` | MEDIUM |
| B10 | **Build + test + commit + push + RV** | — | — |

**RV Phase B:** V7-V12 (Alliance rate + catalog convert)
**Activate ngay:** ❌ Không — feature-gated, chỉ activate khi SystemAdmin switch `Mode=Alliance`

### Feature Gate

```csharp
// Phase B code chỉ chạy khi Mode=Alliance (hiện Silo → zero impact khi deploy)
if (isAllianceMode)  // ← feature gate
{
    int vndPerPoint = await _loyaltyModeResolver.GetVndPerPointAsync();
    pointsToAward = (int)(order.TotalAmount / vndPerPoint);
}
else
{
    pointsToAward = (int)(order.TotalAmount * rate); // existing Silo flow — unchanged
}
```

### Rollback

| Phase | Rollback cách | Ảnh hưởng |
|-------|--------------|-----------|
| Phase A | `git revert` commit | Banner ẩn, dashboard ẩn (quay về hiện trạng) |
| Phase B (trước activate) | `git revert` + revert migration | Zero impact (Silo mode) |
| Phase B (sau activate) | Switch `Mode=Silo` → Phase B code tắt | Cần `SplitWalletsAsync` chia lại |

---

## 12. APPROVAL

- [x] **User approve Option A:** 1.000 VND = 1 điểm (Alliance mode)
- [x] **User approve gộp vào plan:** Phase 3 + Phase 5 thêm vào task card này
- [x] **User approve chiến lược:** Safe Incremental Rollout with Feature Gate (Phase A → Phase B)
- [ ] **User approve IMPLEMENT Phase A:** Chuyển sang IMPLEMENT mode (Phase A trước)
- [ ] **User approve IMPLEMENT Phase B:** Sau Phase A RV pass (Phase B sau)
- [ ] **User request changes:** Update plan + re-review

**Status:** 🔲 PENDING APPROVAL — Chờ user approve IMPLEMENT Phase A để bắt đầu
