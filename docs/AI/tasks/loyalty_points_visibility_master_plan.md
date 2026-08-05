# MASTER PLAN: Loyalty Points Visibility + Shop Owner Loyalty Dashboard

## 1. MỤC TIÊU

### 1.1 Customer-facing (KhachLink)
Khách hàng trên KhachLink thấy được số điểm thưởng tích lũy sau khi đơn hàng hoàn thành (`completed` hoặc `delivered`). Điều kiện tích điểm: đơn hàng chứa sản phẩm của tenant có bật `Loyalty_Program_Enabled`. Áp dụng cho cả khách đã đăng nhập và guest checkout.

### 1.2 Shop owner-facing (ShopERP) — NEW
Shop owner có bảng thống kê điểm thưởng trên ShopERP dashboard với 4 chỉ số:
1. **Điểm chờ đổi** — Tổng điểm đang có trong wallet của tất cả khách hàng (chưa redeem)
2. **Đã đổi** — Tổng điểm đã redeem (sum `RedemptionRecord.PointsSpent` where Status=Fulfilled/Cancelled)
3. **Điểm trong CTKM chờ thưởng** — Tổng điểm dự kiến sẽ thưởng cho khách khi mua qua CTKM active (orders pending/delivering có TrackingCode + chưa completed)
4. **Dự trù điểm thưởng** — Tổng điểm dự kiến sẽ thưởng cho tất cả orders đang xử lý (pending→delivering, chưa completed/delivered)

## 2. VẤN ĐỀ HIỆN TẠI (GAP ANALYSIS)

### 2.1 Backend — Điểm có được tích không?

| Điều kiện | Hiện trạng | Yêu cầu |
|-----------|-----------|---------|
| Guest checkout (không đăng nhập) | ✅ Có — DeviceId fallback + Customer stub | ✅ Giữ nguyên |
| Tenant tắt `Loyalty_Program_Enabled` | ❌ **Vẫn tích** — toggle tồn tại nhưng `ProcessLoyaltyPointsAsync` không check | ❌ Phải skip |
| `AwardOnAllOrders=false` + no TrackingCode | ✅ Skip (đúng) | ✅ Giữ nguyên |
| Double-award guard (delivered→completed) | ✅ Có — check loyalty history | ✅ Giữ nguyên |

**Root cause #1:** `ProcessLoyaltyPointsAsync` (<ref_snippet file="C:\VibeCoding\Gemini_Windsurf\3_CoreHub\Services\OrderWorkflowService.cs" lines="287-444" />) load per-tenant config (`Loyalty_PointsRate`, `Loyalty_MinPointsPerOrder`, `Loyalty_AwardOnAllOrders`) nhưng **không bao giờ check `Loyalty_Program_Enabled`**. Tenant tắt loyalty program vẫn bị tích điểm.

### 2.2 Frontend — Khách có thấy điểm không?

| Trang | Hiện trạng | Yêu cầu |
|-------|-----------|---------|
| `OrderTracking.razor` | ❌ Chỉ status steps, không có điểm | ✅ Banner điểm khi completed/delivered |
| `Checkout.razor` | ❌ Chỉ IdentityUpgradeModal, không thông báo điểm | ✅ Ước tính điểm sau đặt hàng |
| `OrderHistory.razor` | ❌ Chỉ status + total, không có điểm | ⚠️ (optional) Hiển thị điểm đã nhận |
| `PublicOrderTrackingDto` | ❌ Không có field `PointsAwarded` | ✅ Thêm field |

**Root cause #2:** `PublicOrderTrackingDto` (<ref_file file="C:\VibeCoding\Gemini_Windsurf\1_Shared\DTOs\PublicOrderTrackingDto.cs" />) không có field `PointsAwarded` hay `LoyaltyEnabled`. Gateway API `GET /api/public/orders/{id}` không query loyalty history khi trả order detail.

### 2.3 Notification

| Cơ chế | Hiện trạng | Yêu cầu |
|--------|-----------|---------|
| NATS event cho loyalty | ❌ Không có event `loyalty.points.awarded` | ⚠️ (Phase 3 optional) Push toast khi khách online |
| Order status change NATS | ✅ Có `PublishOrderStatusChangedEventAsync` | ✅ Giữ nguyên |

---

## 3. KIẾN TRÚC GIẢI PHÁP

### Phase 1: Backend — Check toggle + trả điểm qua API

#### 1.1 `OrderWorkflowService.ProcessLoyaltyPointsAsync` — Check `Loyalty_Program_Enabled`

**File:** `3_CoreHub/Services/OrderWorkflowService.cs` (lines 287-444)

Thêm check ở đầu method, sau khi resolve tenant settings:

```csharp
// #99-3: Check Loyalty_Program_Enabled toggle — tenant can disable loyalty entirely.
// Previously: toggle existed in ShopFeatureSettingsDto but was never checked → points
// awarded even when tenant turned off loyalty program.
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

**Lý do:** Dùng `IsEnabledAsync` (đã có sẵn, default=true) thay vì `GetSettingsAsync` — tránh load toàn bộ DTO khi chỉ cần 1 field. Fail-open (default=true) nếu service lỗi.

#### 1.2 `PublicOrderTrackingDto` — Thêm `PointsAwarded` + `LoyaltyEnabled`

**File:** `1_Shared/DTOs/PublicOrderTrackingDto.cs`

```csharp
/// <summary>#99-3: Points awarded for this order (null = not yet awarded / order not completed; >0 = awarded).</summary>
public int? PointsAwarded { get; init; }

/// <summary>#99-3: Whether tenant has loyalty program enabled (for KhachLink to show/hide points banner).</summary>
public bool LoyaltyEnabled { get; init; } = true;
```

**Lý do:** DTO layer (không đụng Domain). `init` cho immutability. `null` = chưa tích (order chưa completed/delivered hoặc tenant tắt loyalty). `>0` = đã tích thành công.

#### 1.3 `PublicOrdersController.GetPublicOrder` — Query loyalty history + tenant settings

**File:** `2_Gateway/Controllers/PublicOrdersController.cs` (line 314)

Khi map Order → `PublicOrderTrackingDto`:
1. Query `ShopFeatureSettingsService.IsEnabledAsync(tenantId, "Loyalty_Program_Enabled")` → set `LoyaltyEnabled`
2. Query `LoyaltyRewardsService.GetCustomerRewardsAsync(customerId)` → parse history JSON → tìm EARN entry với `#{orderId}` → set `PointsAwarded`

**Lý do:** Gateway đã inject `IShopFeatureSettingsService` + `ILoyaltyRewardsService` qua `IVanAnDbContext`. Query thêm 2 lần (cached) — overhead tối thiểu.

**Edge case:** Order chưa có customer (guest checkout chưa tạo stub) → `PointsAwarded = null`. Order completed nhưng tenant tắt loyalty sau đó → vẫn trả `PointsAwarded` (đã tích rồi, không thu hồi).

### Phase 2: Frontend — Hiển thị thông báo trên KhachLink

#### 2.1 `OrderTracking.razor` — Banner điểm thưởng

**File:** `5_WebApps/KhachLink/Pages/OrderTracking.razor`

Thêm sau status steps card, khi `_orderStatus == "completed" || _orderStatus == "delivered"`:

```razor
@if ((_orderStatus == "completed" || _orderStatus == "delivered") && _orderData?.PointsAwarded > 0)
{
    <div class="loyalty-points-banner" data-testid="loyalty-points-banner">
        <span class="bi bi-gift-fill"></span>
        Bạn đã nhận được <strong>@_orderData.PointsAwarded điểm thưởng</strong> từ đơn hàng này!
    </div>
}
else if ((_orderStatus == "completed" || _orderStatus == "delivered") && _orderData?.LoyaltyEnabled == false)
{
    <!-- Tenant tắt loyalty — ẩn banner, không hiển thị gì -->
}
```

**Lý do:** Banner chỉ hiện khi order hoàn thành + có điểm thực tế. Tenant tắt loyalty → không hiện gì (tránh nhầm lẫn).

#### 2.2 `Checkout.razor` — Ước tính điểm sau đặt hàng

**File:** `5_WebApps/KhachLink/Pages/Checkout.razor` (sau line 504)

Sau khi order tạo thành công, nếu tenant có `Loyalty_Program_Enabled`:
- Hiển thị thông báo: "Đơn hàng của bạn sẽ được tích điểm khi hoàn thành. Ước tính: **~{estimatedPoints} điểm**."
- Ước tính: `TotalAmount × rate` (rate từ tenant settings qua `ShopFeatureSettingsHttpService`, hoặc default 0.001)

**Lý do:** Khách biết ngay sau đặt hàng rằng sẽ được tích điểm — tăng engagement. Ước tính (không chính xác tuyệt đối vì min/max clamp + status có thể bị cancel).

### Phase 3: Alliance Mode — Normalize VND (Option A — APPROVED)

Chi tiết trong Section 9 bên dưới. Tóm tắt:
- `LoyaltyGlobalConfig`: thêm `VndPerPoint = 1000` (1 điểm = 1000 VND)
- `ProcessLoyaltyPointsAsync`: Alliance mode dùng `TotalAmount / VndPerPoint`, bỏ per-tenant rate
- `ConsolidateWalletsAsync`: convert Silo points → VND-equivalent khi migrate
- `SplitWalletsAsync`: convert ngược khi Alliance→Silo
- `LoyaltyConfigAdmin.razor`: UI config `VndPerPoint`
- Migration: thêm column `VndPerPoint` vào `LoyaltyGlobalConfigs`

### Phase 4 (optional): Push notification

#### 4.1 `OrderWorkflowService` — Publish NATS event khi tích điểm

Sau `AddPointsAsync` thành công:
```csharp
await _natsEventPublisher.PublishAsync("vanan.loyalty.points.awarded", new {
    CustomerId = customer.Id,
    DeviceId = order.CustomerDeviceId,
    Points = pointsToAward,
    OrderId = order.Id,
    TenantId = order.TenantId
});
```

#### 4.2 KhachLink SignalR — Listen + toast

KhachLink SignalR hub subscribe `vanan.loyalty.points.awarded` → push toast notification nếu khách đang online.

**Priority:** LOW — chỉ làm nếu Phase 1+2 hoàn thành và user yêu cầu thêm.

### Phase 5: Shop Owner Loyalty Dashboard (ShopERP) — NEW

Shop owner thấy 4 chỉ số điểm thưởng trên dashboard. Data source: per-tenant SQLite (Silo) hoặc PG (Alliance).

#### 5.1 4 Chỉ số thống kê

| # | Chỉ số | Formula | Data source |
|---|--------|---------|-------------|
| 1 | **Điểm chờ đổi** | `SUM(LoyaltyRewards.PointBalance)` cho tất cả customer của tenant (Silo) HOẶC `SUM(AllianceWallet.TotalPointBalance)` cho device IDs của tenant's customers (Alliance) | SQLite `LoyaltyRewards` hoặc PG `AllianceWallet` |
| 2 | **Đã đổi** | `SUM(RedemptionRecord.PointsSpent)` WHERE `Status IN ('Fulfilled','Cancelled')` AND tenantId = current | SQLite `RedemptionRecords` |
| 3 | **Điểm CTKM chờ thưởng** | `SUM(Order.TotalAmount × PointsRate)` cho orders có `TrackingCode != null` AND `Status IN ('pending','confirmed','preparing','ready','delivering')` AND tenantId = current | SQLite `Orders` (pending orders có tracking code) |
| 4 | **Dự trù điểm thưởng** | `SUM(Order.TotalAmount × PointsRate)` cho orders có `Status IN ('pending','confirmed','preparing','ready','delivering')` AND tenantId = current (ALL pending orders, không cần TrackingCode) | SQLite `Orders` (all pending orders) |

**Phân biệt chỉ số 3 vs 4:**
- Chỉ số 3 (CTKM chờ thưởng): chỉ orders đến từ CTKM (có TrackingCode) — đo hiệu quả flywheel
- Chỉ số 4 (Dự trù): TẤT CẢ orders đang xử lý — tổng dự trù tài chính cho loyalty

**Lý do tách 3 vs 4:** Shop owner cần biết:
- "Tôi sẽ phải trả bao nhiêu điểm cho CTKM đang chạy?" (3) → quyết định tiếp tục/dừng CTKM
- "Tổng dự trù điểm cho tất cả orders đang xử lý là bao nhiêu?" (4) → quản trị tài chính loyalty

#### 5.2 API endpoint

**File:** `5_WebApps/ShopERP/Controllers/LoyaltyController.cs`
**Thêm:** `GET /api/loyalty/dashboard` — trả `LoyaltyDashboardStats` DTO

```csharp
public class LoyaltyDashboardStats
{
    public int PointsPendingRedemption { get; set; }   // Chỉ số 1
    public int PointsRedeemed { get; set; }             // Chỉ số 2
    public int PointsInCampaigns { get; set; }          // Chỉ số 3
    public int PointsReserved { get; set; }             // Chỉ số 4
    public int TotalCustomersWithPoints { get; set; }   // Bonus: số khách có điểm
    public int ActiveCampaignsCount { get; set; }       // Bonus: số CTKM active
}
```

**Logic:**
```csharp
// Chỉ số 1: Sum of all customer balances (Silo) or Alliance wallet balances (Alliance)
int pendingRedemption = await _dbContext.LoyaltyRewards
    .Where(lr => lr.TenantId == tenantId && lr.IsActive)
    .SumAsync(lr => lr.PointBalance);

// Chỉ số 2: Sum of redeemed points (Fulfilled + Cancelled — cancelled đã refund nên chỉ count Fulfilled)
int redeemed = await _dbContext.RedemptionRecords
    .Where(r => r.TenantId == tenantId && r.Status == "Fulfilled")
    .SumAsync(r => r.PointsSpent);

// Chỉ số 3: Pending orders with TrackingCode (campaign-referred, not yet completed)
var campaignOrders = await _dbContext.Orders
    .Where(o => o.TenantId == tenantId
        && o.TrackingCode != null
        && o.Status != "completed" && o.Status != "cancelled"
        && o.Status != "delivered")  // delivered sẽ tích điểm rồi
    .ToListAsync();
int pointsInCampaigns = campaignOrders.Sum(o => (int)(o.TotalAmount * rate));

// Chỉ số 4: ALL pending orders (not yet completed/delivered)
var allPendingOrders = await _dbContext.Orders
    .Where(o => o.TenantId == tenantId
        && o.Status != "completed" && o.Status != "cancelled"
        && o.Status != "delivered")
    .ToListAsync();
int pointsReserved = allPendingOrders.Sum(o => (int)(o.TotalAmount * rate));
```

**Edge case — Alliance mode:** Chỉ số 1 cần query PG `AllianceWallet` thay vì SQLite. Dùng `LoyaltyReadRouter` pattern hoặc direct Gateway API call.

#### 5.3 UI page

**File:** `5_WebApps/ShopERP/Components/Pages/Loyalty/LoyaltyDashboard.razor` (NEW)
**Route:** `/loyalty/dashboard`

Hiển thị 4 thẻ thống kê (cards) + bảng chi tiết:
- Card 1: "Điểm chờ đổi" — số lớn + icon gift
- Card 2: "Đã đổi" — số lớn + icon check-circle
- Card 3: "Điểm CTKM chờ thưởng" — số lớn + icon megaphone
- Card 4: "Dự trù điểm thưởng" — số lớn + icon piggy-bank
- Bảng: Top 10 khách hàng có điểm cao nhất (CustomerName + PointBalance)
- Bảng: Top 5 CTKM active (CampaignName + ConvertedOrders + estimated points)

**UI Platform:** Dùng `VanAnCard` + `VanAnStatCard` (nếu có) hoặc Bootstrap cards.

#### 5.4 NavMenu link

**File:** `5_WebApps/ShopERP/Components/Layout/NavMenu.razor`
**Thêm:** Link "Thống kê điểm thưởng" → `/loyalty/dashboard` (icon: `bi-gift`)

---

## 4. DANH SÁCH FILE THAY ĐỔI

| # | File | Phase | Loại | Mô tả |
|---|------|-------|------|-------|
| 1 | `3_CoreHub/Services/OrderWorkflowService.cs` | 1+3 | MODIFY | Check `Loyalty_Program_Enabled` (P1) + Alliance fixed rate (P3) |
| 2 | `1_Shared/DTOs/PublicOrderTrackingDto.cs` | 1 | MODIFY | Thêm `PointsAwarded` + `LoyaltyEnabled` |
| 3 | `2_Gateway/Controllers/PublicOrdersController.cs` | 1 | MODIFY | Query loyalty history + tenant settings khi GET order |
| 4 | `5_WebApps/KhachLink/Pages/OrderTracking.razor` | 2 | MODIFY | Banner điểm thưởng khi completed/delivered |
| 5 | `5_WebApps/KhachLink/Pages/Checkout.razor` | 2 | MODIFY | Ước tính điểm sau đặt hàng |
| 6 | `1_Shared/Domain.cs` | 3 | MODIFY | `LoyaltyGlobalConfig`: thêm `VndPerPoint` field + method |
| 7 | `3_CoreHub/Services/AllianceWalletService.cs` | 3 | MODIFY | `ConsolidateWalletsAsync` + `SplitWalletsAsync`: convert VND-equivalent |
| 8 | `3_CoreHub/Services/LoyaltyModeResolver.cs` | 3 | MODIFY | Thêm `GetVndPerPointAsync()` |
| 9 | `3_CoreHub/Infrastructure/IVanAnDbContext.cs` | 3 | MODIFY | Thêm `VndPerPoint` vào interface (nếu cần) |
| 10 | `2_Gateway/Controllers/LoyaltyConfigController.cs` | 3 | MODIFY | Thêm `VndPerPoint` vào DTO + PUT endpoint |
| 11 | `5_WebApps/ShopERP/Components/Pages/Admin/LoyaltyConfigAdmin.razor` | 3 | MODIFY | UI input "VND per point" (Alliance mode only) |
| 12 | `3_CoreHub/Infrastructure/Migrations/` | 3 | NEW | Migration `AddVndPerPointColumn` |
| 13 | `5_WebApps/ShopERP/Controllers/LoyaltyController.cs` | 5 | MODIFY | Thêm `GET /api/loyalty/dashboard` — 4 chỉ số |
| 14 | `5_WebApps/ShopERP/Components/Pages/Loyalty/LoyaltyDashboard.razor` | 5 | NEW | UI dashboard 4 cards + top customers/campaigns |
| 15 | `5_WebApps/ShopERP/Components/Layout/NavMenu.razor` | 5 | MODIFY | Link "Thống kê điểm thưởng" → `/loyalty/dashboard` |

**Không sửa:**
- `RedemptionCatalogItem` — per-tenant catalog, `PointsRequired` tính theo VND value (admin tự set đúng)

---

## 5. RỦI RO & EDGE CASES

| # | Edge case | Xử lý |
|---|-----------|-------|
| 1 | Tenant tắt loyalty sau khi đã tích điểm | `PointsAwarded` vẫn trả giá trị đã tích (không thu hồi) |
| 2 | Order cancelled sau khi delivered | Điểm đã tích không thu hồi (existing behavior — out of scope) |
| 3 | Guest checkout chưa có Customer stub | `PointsAwarded = null` — banner không hiện |
| 4 | Loyalty history JSON parse fail | `PointsAwarded = null` — fail-safe, banner không hiện |
| 5 | `ShopFeatureSettingsService` lỗi | Fail-open: `LoyaltyEnabled = true` (default), tích điểm bình thường |
| 6 | Order multi-tenant (Phase 5 checkout) | Mỗi order có 1 tenant → check toggle per-order, đúng |
| 7 | Alliance mode (cross-tenant wallet) | `PointsAwarded` query AllianceTransaction thay vì LoyaltyRewards history |

---

## 6. CHIẾN LƯỢC THỰC THI: Safe Incremental Rollout with Feature Gate

### 6.1 Nguyên tắc

- **Phase A trước (LOW-MEDIUM risk):** Customer visibility + Shop owner dashboard — giá trị ngay, không migration, không sửa Domain
- **Phase B sau (HIGH risk):** Alliance VND normalization — migration + Domain change, feature-gated (chỉ activate khi `Mode=Alliance`)
- **Không xung đột file:** Phase A và Phase B sửa khác file (trừ `OrderWorkflowService` — khác location, gộp được)

### 6.2 Phân tích xung đột file

| File | Phase nào dùng | Xung đột? |
|------|---------------|-----------|
| `OrderWorkflowService.cs` | P1.1 (line ~350) + P3.3 (line ~386) | ⚠️ Cùng file, khác location — gộp được |
| `AllianceWalletService.cs` | P3.4 + P3.9 (cùng `ConsolidateWalletsAsync`) | 🔴 Cùng method — BẮT BUỘC làm chung trong Phase B |
| `LoyaltyController.cs` | P5.1 only | ✅ Độc lập |
| `NavMenu.razor` | P5.3 only | ✅ Độc lập |
| `Domain.cs` | P3.1 only | ✅ Độc lập (Phase B) |
| `LoyaltyDashboard.razor` | P5.2 only | ✅ Độc lập (file mới) |

### 6.3 Mức rủi ro

| Phase | Risk | Lý do |
|-------|------|-------|
| Phase A (Batch 1+3) | **LOW-MEDIUM** | No Domain, no migration, chỉ toggle check + DTO + UI banner + read-only dashboard |
| Phase B (Batch 2) | **HIGH** | Domain.cs change + migration + AllianceWalletService — ảnh hưởng Alliance mode |

### 6.4 Thứ tự thực thi

#### Phase A: Batch 1 + Batch 3 (gộp — 1 commit, LOW-MEDIUM risk)

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
**Activate ngay:** ✅ Có — customer thấy banner, shop owner thấy dashboard

#### Phase B: Batch 2 (riêng — 1-2 commits, HIGH risk, feature-gated)

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

### 6.5 Feature Gate (Phase B)

**Principle:** Code Phase B deploy nhưng KHÔNG activate cho đến khi SystemAdmin switch `Mode = Alliance`.

```csharp
// Phase 3.3: Alliance fixed rate — chỉ chạy khi Mode=Alliance
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

**Hiện tại:** `LoyaltyGlobalConfig.Mode = Silo` (default) → Phase B code không activate → **zero impact** khi deploy.

**Khi nào activate:** SystemAdmin vào `LoyaltyConfigAdmin.razor` → switch `Mode = Alliance` → Phase B logic mới chạy.

### 6.6 Bảo đảm an toàn

| # | Safety measure | Áp dụng cho |
|---|----------------|-------------|
| 1 | **Migration forward-compatible** — `VndPerPoint` default 1000, old code không crash | Phase B |
| 2 | **Feature gate** — Phase B code chỉ chạy khi `Mode=Alliance` (hiện Silo) | Phase B |
| 3 | **Silo mode unchanged** — tất cả Phase B changes gated by `isAllianceMode` | Phase B |
| 4 | **Fail-open** — `ShopFeatureSettingsService` lỗi → default `LoyaltyEnabled=true` | Phase A |
| 5 | **Fail-safe** — loyalty history parse fail → `PointsAwarded=null`, banner ẩn | Phase A |
| 6 | **Idempotency** — `ConsolidateWalletsAsync` có `alreadyMigrated` guard | Phase B |
| 7 | **Read-only dashboard** — `GET /api/loyalty/dashboard` không write data | Phase A |
| 8 | **No breaking API** — `PublicOrderTrackingDto` thêm field (additive, không xóa) | Phase A |

### 6.7 Kế hoạch Rollback

| Phase | Rollback cách | Ảnh hưởng |
|-------|--------------|-----------|
| Phase A | `git revert` commit | Banner ẩn, dashboard ẩn — khách không thấy điểm (quay về hiện trạng) |
| Phase B (trước activate) | `git revert` commit + `dotnet ef database update` revert migration | Zero impact (Silo mode, code không activate) |
| Phase B (sau activate) | SystemAdmin switch `Mode=Silo` → Phase B code tắt | Alliance wallet vẫn còn data nhưng không dùng — cần `SplitWalletsAsync` để chia lại |

### 6.8 Tóm tắt

| Phase | Batches | Commits | Risk | Activate ngay? |
|-------|---------|---------|------|----------------|
| **A** | 1 + 3 | 1 | LOW-MEDIUM | ✅ Có (customer + dashboard) |
| **B** | 2 | 1-2 | HIGH | ❌ Không (feature-gated, chỉ khi switch Alliance) |

**Estimated effort:** 2 sessions (Phase A + Phase B).

---

## 7. VERIFY PLAN

| # | Check | Cách verify |
|---|-------|-------------|
| V1 | Tenant tắt loyalty → không tích điểm | ShopERP tắt `Loyalty_Program_Enabled`, hoàn thành order, check Gateway logs |
| V2 | Tenant bật loyalty → tích điểm | Hoàn thành order, check `PointsAwarded > 0` trong API response |
| V3 | OrderTracking hiển thị banner | Mở `/order-tracking/{id}`, status=completed, check banner |
| V4 | Guest checkout có banner | Guest đặt hàng, hoàn thành, check banner |
| V5 | Tenant tắt loyalty → không có banner | Tắt loyalty, hoàn thành order, check banner ẩn |
| V6 | Checkout ước tính điểm | Đặt hàng, check thông báo ước tính |
| V7 | Alliance mode: 1 điểm = 1000 VND | Switch Alliance, mua 100.000đ → earn 100 điểm | `pointsToAward = TotalAmount / 1000 = 100` |
| V8 | Silo→Alliance migration convert | Tenant A (rate=0.001, 100 điểm) → migrate → 100 điểm (100×1000/1000). Tenant B (rate=0.0005, 50 điểm) → migrate → 100 điểm (50×2000/1000) | Cùng VND value, khác số điểm cũ |
| V9 | `VndPerPoint` configurable | SystemAdmin set `VndPerPoint=500` → 1 điểm = 500đ | API + UI update |
| V10 | Silo mode không bị ảnh hưởng | Tenant Silo, rate=0.0005 → mua 100.000đ → 50 điểm (giữ per-tenant rate) | Existing behavior unchanged |
| V11 | Dashboard: Điểm chờ đổi | Tạo 3 khách có 100/200/300 điểm → dashboard hiển thị 600 | `PointsPendingRedemption = 600` |
| V12 | Dashboard: Đã đổi | Redeem 50 điểm (Fulfilled) → dashboard hiển thị 50 | `PointsRedeemed = 50` |
| V13 | Dashboard: Điểm CTKM chờ thưởng | 2 orders pending có TrackingCode, TotalAmount 50.000+100.000, rate=0.001 → 150 điểm | `PointsInCampaigns = 150` |
| V14 | Dashboard: Dự trù điểm thưởng | 3 orders pending (2 có TrackingCode + 1 không), TotalAmount 50.000+100.000+80.000, rate=0.001 → 230 điểm | `PointsReserved = 230` |

---

## 8. GOVERNANCE COMPLIANCE

- ✅ Phase 1-2: Không sửa `Domain.cs` — chỉ DTO + Service logic
- ✅ Phase 3: Sửa `Domain.cs` — thêm `VndPerPoint` vào `LoyaltyGlobalConfig` (approved feature, Domain Phase active)
- ✅ Không tạo UI custom HTML/CSS — dùng existing KhachLink styling
- ✅ Không inject CoreHub services vào KhachLink — dùng HTTP qua Gateway API
- ✅ Multi-tenancy: check toggle per-tenant, per-order
- ✅ Layer boundaries: DTO (1_Shared) → Service (3_CoreHub) → API (2_Gateway) → Client (5_WebApps/KhachLink)
- ✅ AccountingEntry: không liên quan (loyalty ≠ accounting)

---

## 9. ALLIANCE MODE: Normalize điểm về VND (Option A — APPROVED)

### 9.1 Quyết định

**User approved Option A:** 1.000 VND = 1 điểm (global constant). Gộp vào plan này.

- Alliance mode: tất cả điểm dùng fixed rate `1 điểm = 1000 VND` — bỏ per-tenant `Loyalty_PointsRate`
- Silo mode: giữ nguyên per-tenant `Loyalty_PointsRate` (không ảnh hưởng)
- Hiện chưa có tenant nào ở Alliance mode → không cần convert existing balances ngay
- Khi switch Silo→Alliance: `ConsolidateWalletsAsync` convert existing points sang VND-equivalent

### 9.2 Vấn đề giải quyết

| Trước (per-tenant rate) | Sau (Option A — fixed 1000 VND/point) |
|--------------------------|---------------------------------------|
| Tenant A: 1 điểm = 1000đ, Tenant B: 1 điểm = 2000đ | Cả A + B: 1 điểm = 1000đ |
| Alliance wallet trộn điểm khác giá trị → sai khi redeem | Alliance wallet đồng nhất → redeem đúng giá trị |
| Khách mua 100.000đ tại A → 100 điểm, tại B → 50 điểm | Khách mua 100.000đ tại A → 100 điểm, tại B → 100 điểm |

### 9.3 Thay đổi code (Phase 3)

#### 3.1 `LoyaltyGlobalConfig` — Thêm `VndPerPoint` constant

**File:** `1_Shared/Domain.cs` (line 2143)
**Thay đổi:** Thêm field `int VndPerPoint = 1000` + method `UpdateVndPerPoint()`

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

**Lý do:** Configurable global constant (default 1000). SystemAdmin có thể đổi nếu cần (VD: 1 điểm = 500đ). Lưu trong PG, không hardcode.

#### 3.2 `ProcessLoyaltyPointsAsync` — Alliance mode dùng fixed rate

**File:** `3_CoreHub/Services/OrderWorkflowService.cs` (line 386)
**Thay đổi:** Khi Alliance mode, bỏ per-tenant rate, dùng `TotalAmount / VndPerPoint`:

```csharp
// === Option A: Alliance mode uses fixed VND-per-point (1 điểm = 1000 VND) ===
// Silo mode: per-tenant Loyalty_PointsRate (existing behavior, unchanged)
// Alliance mode: fixed rate from LoyaltyGlobalConfig.VndPerPoint — no per-tenant override
int pointsToAward;
if (isAllianceMode)
{
    int vndPerPoint = await _loyaltyModeResolver.GetVndPerPointAsync();
    pointsToAward = (int)(order.TotalAmount / vndPerPoint);
    // Min/Max still apply (per-tenant or global) — guards against tiny/huge orders
}
else
{
    // Existing Silo flow: per-tenant rate
    pointsToAward = (int)(order.TotalAmount * rate);
}
pointsToAward = Math.Max(minPoints, pointsToAward);
if (maxPoints.HasValue) pointsToAward = Math.Min(maxPoints.Value, pointsToAward);
```

**Lý do:** Alliance mode = cross-tenant → phải đồng nhất rate. Silo mode = per-tenant → giữ flexibility.

#### 3.3 `ConsolidateWalletsAsync` — Convert existing balances khi Silo→Alliance

**File:** `3_CoreHub/Services/AllianceWalletService.cs` (line 238)
**Thay đổi:** Khi migrate, convert raw points sang VND-equivalent:

```csharp
// Option A: Convert Silo points to VND-equivalent before adding to Alliance wallet.
// Silo: points = TotalAmount * tenantRate → VND value = points / tenantRate
// Alliance: points = VND value / VndPerPoint
// Formula: alliancePoints = (siloPoints / tenantRate) / VndPerPoint
//   = siloPoints * (1/tenantRate) / VndPerPoint
//   = siloPoints * VndPerPoint_old_tenant / VndPerPoint_alliance
int vndPerPoint = await GetVndPerPointAsync(); // from LoyaltyGlobalConfig
decimal tenantRate = await GetTenantLoyaltyRateAsync(tenantId); // from ShopFeatureSettings
decimal vndValue = input.PointBalance / tenantRate; // VND value of existing points
int alliancePoints = (int)(vndValue / vndPerPoint); // convert to Alliance points
wallet.AddPoints(alliancePoints);
```

**Edge case:** Tenant chưa config rate (default 0) → dùng global default rate. Nếu rate=0 → skip (avoid div-by-zero).

#### 3.4 `SplitWalletsAsync` — Convert ngược khi Alliance→Silo

**File:** `3_CoreHub/Services/AllianceWalletService.cs` (line 308)
**Thay đổi:** Khi split, convert Alliance points back sang Silo points per-tenant:

```csharp
// Option A: Convert Alliance points back to Silo points per-tenant.
// Alliance points → VND value → Silo points (using tenant's own rate)
decimal vndValue = alliancePoints * vndPerPoint; // VND value of allocation
int siloPoints = (int)(vndValue * tenantRate); // convert to Silo points
```

#### 3.5 `LoyaltyModeResolver` — Thêm `GetVndPerPointAsync()`

**File:** `3_CoreHub/Services/LoyaltyModeResolver.cs`
**Thay đổi:** Thêm method đọc `VndPerPoint` từ `LoyaltyGlobalConfig`:

```csharp
public async Task<int> GetVndPerPointAsync()
{
    LoyaltyGlobalConfig globalCfg = await GetOrCreateGlobalConfigAsync();
    return globalCfg.VndPerPoint > 0 ? globalCfg.VndPerPoint : 1000;
}
```

#### 3.6 `LoyaltyConfigController` — API cho SystemAdmin config `VndPerPoint`

**File:** `2_Gateway/Controllers/LoyaltyConfigController.cs`
**Thay đổi:** Thêm field `VndPerPoint` vào `GlobalConfigDto` + `UpdateGlobalConfigRequest`. PUT endpoint cập nhật `VndPerPoint`.

#### 3.7 `LoyaltyConfigAdmin.razor` — UI cho SystemAdmin

**File:** `5_WebApps/ShopERP/Components/Pages/Admin/LoyaltyConfigAdmin.razor`
**Thay đổi:** Thêm input field "VND per point (Alliance mode)" — chỉ hiện khi `Mode = Alliance`.

#### 3.8 Migration — Add `VndPerPoint` column

**File:** `3_CoreHub/Infrastructure/Migrations/` (new migration)
**Thay đổi:** `AddColumn<int>("VndPerPoint", defaultValue: 1000)` trên `LoyaltyGlobalConfigs` table.

#### 3.9 Convert `RedemptionCatalogItem.PointsRequired` khi Silo→Alliance

**File:** `3_CoreHub/Services/AllianceWalletService.cs` (thêm vào `ConsolidateWalletsAsync` hoặc method riêng)
**Vấn đề:** `RedemptionCatalogItem.PointsRequired` được admin set theo Silo rate cũ. Khi switch Alliance (1 điểm = 1000 VND), `PointsRequired` cũ sai giá trị VND — khách redeem mất giá trị.

**Kịch bản lỗi:**
```
Tenant B (Silo, rate=0.0005, 1 điểm = 2000 VND):
  Admin set "Giảm 100.000đ" = 50 điểm (50 × 2000 = 100.000đ ✓ Silo)

Switch sang Alliance (1 điểm = 1000 VND):
  Cùng 50 điểm = 50.000đ (50 × 1000) — SAI! Khách mất 50.000đ
  Phải là 100 điểm (100 × 1000 = 100.000đ) — giữ nguyên VND value
```

**Thay đổi:** Khi `ConsolidateWalletsAsync` chạy, convert tất cả active catalog items:
```csharp
// Formula: newPointsRequired = oldPointsRequired × (VND_per_point_Silo) / VndPerPoint_Alliance
// VND_per_point_Silo = 1 / tenantRate
// VD: rate=0.0005 → 1/0.0005 = 2000 VND/point → newPoints = 50 × 2000 / 1000 = 100
int vndPerPoint = await GetVndPerPointAsync();
decimal tenantRate = await GetTenantLoyaltyRateAsync(tenantId);
if (tenantRate <= 0) tenantRate = 0.001m; // fallback

var activeCatalogItems = await _dbContext.RedemptionCatalogItems
    .Where(c => c.TenantId.Value == tenantId && c.IsActive)
    .ToListAsync();

foreach (var item in activeCatalogItems)
{
    decimal vndPerPointSilo = 1m / tenantRate;
    int newPointsRequired = (int)(item.PointsRequired * vndPerPointSilo / vndPerPoint);
    if (newPointsRequired < 1) newPointsRequired = 1; // minimum 1 point
    item.UpdateDetails(item.ProductName, item.Description, item.ImageUrl,
        newPointsRequired, item.StockCount, item.ValidTo, item.VoucherExpiryDays);
}
await _dbContext.SaveChangesAsync();
```

**Edge cases:**
- Tenant chưa config rate (default 0) → fallback 0.001 (1 điểm = 1000đ, không cần convert)
- Catalog item inactive → skip
- `PointsRequired` mới < 1 → clamp to 1

### 9.4 Edge cases

| # | Case | Xử lý |
|---|------|-------|
| 1 | Tenant chưa config `Loyalty_PointsRate` (default 0) | Dùng global default rate. Nếu rate=0 → skip migration (avoid div-by-zero) |
| 2 | Khách có điểm từ cả tenant A + B trước Alliance | `ConsolidateWalletsAsync` convert từng tenant's points separately → tổng vào wallet |
| 3 | `VndPerPoint` đổi sau khi đã có điểm trong wallet | Điểm cũ giữ nguyên (đã convert rồi). Điểm mới dùng rate mới. Không retroactive. |
| 4 | Order TotalAmount < 1000đ | `pointsToAward = 0` → `Math.Max(minPoints, 0)` = minPoints (default 10) → vẫn tích tối thiểu |
| 5 | Alliance mode nhưng tenant opted out (`IsAllianceMember=false`) | Silo flow — dùng per-tenant rate (existing behavior) |
| 6 | `RedemptionCatalogItem.PointsRequired` sai sau switch Silo→Alliance | Task 3.9: convert `PointsRequired` trong `ConsolidateWalletsAsync` — giữ nguyên VND value |
