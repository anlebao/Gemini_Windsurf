# Hướng dẫn sử dụng VALCN v2.0 Platform-Light

> **Phiên bản:** v2.0 PLATFORM-LIGHT (2026-08-09)
> **Áp dụng:** Vạn An Local Commerce Network — hệ thống kế toán + thương mại đa tenant
> **Trạng thái:** Đã triển khai, tất cả feature flags mặc định **TẮT** (OFF) — không ảnh hưởng production cho đến khi SystemAdmin bật

---

## Mục lục

1. [Tổng quan VALCN v2.0](#1-tổng-quan-valcn-v20)
2. [Các loại người dùng](#2-các-loại-người-dùng)
3. [Hướng dẫn cho SystemAdmin](#3-hướng-dẫn-cho-systemadmin)
4. [Hướng dẫn cho Shop Owner / Tenant Admin](#4-hướng-dẫn-cho-shop-owner--tenant-admin)
5. [Hướng dẫn cho Customer (Khách hàng)](#5-hướng-dẫn-cho-customer-khách-hàng)
6. [Hướng dẫn cho Investor (Nhà đầu tư)](#6-hướng-dẫn-cho-investor-nhà-đầu-tư)
7. [Bảng tra cứu Feature Flags](#7-bảng-tra-cứu-feature-flags)
8. [Câu hỏi thường gặp (FAQ)](#8-câu-hỏi-thường-gặp-faq)
9. [Khắc phục sự cố](#9-khắc-phục-sự-cố)

---

## 1. Tổng quan VALCN v2.0

VALCN v2.0 PLATFORM-LIGHT là bản nâng cấp của Vạn An Local Commerce Network, bổ sung 3 tính năng cốt lõi:

| Tính năng | Mô tả | Feature Flag | Mặc định |
|-----------|-------|--------------|----------|
| **Platform Fee** | Tính phí nền tảng trên đơn hàng Marketplace | `ValcnV2_PlatformFee` | OFF |
| **Loyalty Budget** | Giới hạn ngân sách điểm thưởng | `ValcnV2_LoyaltyBudget` | OFF |
| **Refund Reversal** | Hoàn đảo 4 bước khi hủy đơn (UC-06) | `ValcnV2_RefundReversal` | OFF |

Ngoài ra có **Network Dashboard** — bảng điều khiển tổng hợp đa tenant (investor-facing), luôn sẵn sàng cho SystemAdmin (không cần bật flag).

### Nguyên tắc an toàn

- **Tất cả feature flags mặc định TẮT** — behavior hiện tại được bảo toàn cho đến khi admin bật
- Bật/tắt runtime qua UI `/admin/valcn-features` — có hiệu lực trong 30 giây (cache TTL)
- Tắt flag **không** hoàn tác dữ liệu đã tạo khi flag đang BẬT (ví dụ: đơn đã hủy khi RefundReversal ON sẽ giữ bút toán đảo, không undo)
- Mỗi tính năng độc lập — có thể bật từng cái riêng biệt

---

## 2. Các loại người dùng

VALCN v2.0 liên quan đến 4 loại người dùng:

| Vai trò | Quyền | Trang liên quan |
|---------|-------|-----------------|
| **SystemAdmin** | Quản trị toàn hệ thống Vạn An, bật/tắt feature flags, xem Network Dashboard | `/admin/valcn-features`, `/admin/network-dashboard`, `/admin/background-services` |
| **Shop Owner / Tenant Admin** | Quản lý quán (tenant), cấu hình per-tenant, xem báo cáo kế toán | `/admin/shop-feature-settings`, `/admin/loyalty-config`, `/accounting` |
| **Customer** | Đặt hàng, tích điểm, đổi điểm qua KhachLink PWA | KhachLink (app2.khachvip.online) |
| **Investor** | Xem metrics tổng hợp (read-only, investor-facing) | `/admin/network-dashboard` (qua SystemAdmin) |

---

## 3. Hướng dẫn cho SystemAdmin

SystemAdmin là quản trị viên nền tảng Vạn An — người duy nhất có quyền bật/tắt feature flags và xem Network Dashboard.

### 3.1. Đăng nhập

1. Truy cập `https://app2.khachvip.online/Login`
2. Đăng nhập bằng tài khoản SystemAdmin (ví dụ: `sysadmin@vanan.vn`)
3. Chuyển đến trang admin qua menu bên trái

### 3.2. Quản lý Feature Flags (`/admin/valcn-features`)

Đây là trang trung tâm điều khiển VALCN v2.0.

#### Cách bật/tắt tính năng

1. Vào menu bên trái → **"VALCN v2.0 Features"** (icon công tắc)
2. Bảng hiển thị 3 feature flags:

| Feature | Phase | Mô tả | Trạng thái mặc định |
|---------|-------|-------|---------------------|
| Platform Fee (Marketplace) | Phase 2 | Tính PlatformFeeAmount trên Marketplace orders | Disabled (OFF) |
| Loyalty Budget Cap | Phase 3 | Check budget trước AddPoints + reset jobs | Disabled (OFF) |
| Refund Reversal (UC-06) | Phase 4 | 4-step reversal khi hủy đơn | Disabled (OFF) |

3. Click nút **"Enable"** (xanh) để bật, **"Disable"** (đỏ) để tắt
4. Thay đổi có hiệu lực trong **30 giây** (cache TTL)

#### Khuyến nghị bật theo thứ tự

Nếu muốn bật tất cả, hãy bật theo thứ tự:

1. **Platform Fee** trước (cần thiết để Network Dashboard hiển thị Platform Revenue)
2. **Loyalty Budget** tiếp theo (cần thiết để Refund Reversal đảo đúng budget counters)
3. **Refund Reversal** cuối (phụ thuộc 2 cái trên)

> **Cảnh báo:** Bật Refund Reversal mà chưa bật Loyalty Budget → bước 2c (loyalty reversal) vẫn chạy nhưng budget counters không được đảo (DecrementIssuanceAsync không có config để giảm).

### 3.3. Xem Network Dashboard (`/admin/network-dashboard`)

Bảng điều khiển tổng hợp đa tenant — dành cho investor pitch và monitoring toàn network.

#### Cách truy cập

1. Menu bên trái → **"Network Dashboard"** (icon biểu đồ)
2. Trang hiển thị 8 metric cards + bộ lọc ngày

#### 8 Metrics

| Metric | Ý nghĩa | Công thức |
|--------|---------|-----------|
| **GMV** | Tổng giá trị giao dịch toàn network | SUM(TotalAmount) tất cả đơn hàng |
| **Active Tenants** | Số quán có đơn hàng trong kỳ | COUNT DISTINCT TenantId |
| **Active Customers** | Số khách hàng distinct | COUNT DISTINCT CustomerId |
| **Repeat Rate** | Tỷ lệ khách quay lại | (khách có >1 đơn) / (tổng khách) |
| **Platform Revenue** | Doanh thu nền tảng Vạn An | SUM(PlatformFeeAmount) |
| **Loyalty Cost** | Chi phí điểm thưởng (VND) | SUM(PointsIssued) × 1,000 VND |
| **Loyalty ROI** | Hiệu quả đầu tư loyalty | (repeatGmv - loyaltyCost) / loyaltyCost |
| **Contribution Profit** | Lợi nhuận đóng góp | PlatformRevenue - LoyaltyCost |

#### Bộ lọc ngày

- **Từ / Đến:** chọn ngày bắt đầu + kết thúc (mặc định: 30 ngày gần nhất)
- Click **"Áp dụng"** để tải lại metrics
- Cache **10 phút** — dữ liệu làm mới mỗi 10 phút (không real-time)

> **Lưu ý về Loyalty Cost:** Sử dụng fallback **1,000 VND/point** (INV-009 chưa triển khai — LoyaltyGlobalConfig chưa có field PointValue). Khi v3.0 thêm PointValue, số liệu sẽ chính xác hơn.

### 3.4. Quản lý Background Services (`/admin/background-services`)

2 background job mới của VALCN v2.0 (Phase 3) có thể bật/tắt tại đây:

| Job | Lịch chạy | Chức năng |
|-----|-----------|-----------|
| `LoyaltyBudgetDailyResetJob` | 00:00 UTC hàng ngày | Reset `PointsIssuedToday` về 0 cho tất cả tenant |
| `LoyaltyBudgetMonthlyResetJob` | 00:00 UTC ngày 1 hàng tháng | Reset `PointsIssuedThisMonth` về 0 cho tất cả tenant |

> **Lưu ý:** 2 job này chỉ có ý nghĩa khi `ValcnV2_LoyaltyBudget` = ON. Khi flag OFF, job vẫn chạy nhưng không có config để reset (no-op).

---

## 4. Hướng dẫn cho Shop Owner / Tenant Admin

Shop Owner là chủ quán (tenant) — bị ảnh hưởng bởi 3 tính năng VALCN v2.0 khi SystemAdmin bật.

### 4.1. Platform Fee (khi `ValcnV2_PlatformFee` = ON)

#### Khi nào áp dụng?

- Chỉ áp dụng cho đơn hàng **Marketplace mode** (không áp dụng Reseller mode)
- Phí được snapshot tại thời điểm tạo đơn — toggle affect future orders only

#### Cấu hình per-tenant

1. Vào `/admin/shop-feature-settings`
2. Field **`PlatformFeeRate`** — tỷ lệ phí nền tảng (decimal, mặc định 0.05 = 5%)
3. Nếu không set → fallback theo thứ tự:
   - Global `SystemSetting.DefaultPlatformFeeRate` (mặc định 30%)
   - Ultimate fallback: 5%

#### Cách tính

```
PlatformFeeAmount = TotalAmount × PlatformFeeRate
```

Ví dụ: Đơn 100,000 VND, PlatformFeeRate = 5% → PlatformFeeAmount = 5,000 VND

#### Xem trên đơn hàng

- Field `Order.PlatformFeeAmount` hiển thị trên chi tiết đơn
- Khi flag OFF: field = null (không tính phí)

### 4.2. Loyalty Budget (khi `ValcnV2_LoyaltyBudget` = ON)

#### Khi nào áp dụng?

- Áp dụng trước mỗi lần AddPoints (khi đơn completed/delivered)
- Nếu budget hết → khách hàng nhận 0 điểm (không lỗi, chỉ log)

#### 4 giới hạn (cấu hình per-tenant tại `/admin/loyalty-config`)

| Giới hạn | Field | Ý nghĩa | Khi null |
|----------|-------|---------|----------|
| Per-Order Rate Cap | `PerOrderRateCap` | Max points = orderAmount × rate | Unlimited |
| Monthly Budget | `MonthlyPointsBudget` | Max points/tháng/tenant | Unlimited |
| Daily Budget | `DailyPointsBudget` | Max points/ngày/tenant | Unlimited |
| Per-Customer Daily | `PerCustomerDailyLimit` | Max points/khách/ngày | Unlimited |

#### Cách hoạt động

1. Khi đơn completed/delivered → tính points theo formula hiện tại
2. `CheckAndAdjustPointsAsync` kiểm tra 4 caps → trả về adjusted points (có thể = 0)
3. Nếu adjusted > 0 → AddPoints + `RecordIssuanceAsync` (atomic increment)
4. Nếu adjusted = 0 → khách không nhận điểm (log warning)

#### Reset jobs

- **Hàng ngày 00:00 UTC:** `PointsIssuedToday` → 0 (tất cả tenant)
- **Đầu tháng 00:00 UTC:** `PointsIssuedThisMonth` → 0 (tất cả tenant)

> **Lưu ý:** ShopERP dùng HTTP proxy qua Gateway (PG là source of truth cho LoyaltyTenantConfig). Nếu Gateway không khả dụng → fallback trả points gốc (không cap) — an toàn, không crash.

### 4.3. Refund Reversal (khi `ValcnV2_RefundReversal` = ON)

#### Khi nào kích hoạt?

- Khi đơn chuyển sang status **"cancelled"** (qua `OrderWorkflowService.TransitionStatusAsync`)
- Chỉ full cancel — **không** partial refund

#### 4 bước đảo (UC-06)

| Bước | Hành động | Chi tiết |
|------|-----------|----------|
| **2a** | Accrual liability entry | Tạo bút toán chi phí (accountCode "331" — Phải trả khách hàng) = tổng revenue. Đảm bảo Cash = Accounting (TT 152/2025). |
| **2b** | Accounting reversal | `AccountingEntry.CreateReversal` cho mỗi entry gốc (đảo dấu amount, giữ CorrelationId). |
| **2c** | Loyalty reversal | `SubtractPointsAsync` + `LoyaltyIssuanceRecord.MarkReversed` + `DecrementIssuanceAsync` (giảm budget counters). |
| **2d** | Referral commission reversal | `WalletService.ReverseTransactionAsync` cho mỗi WalletTransaction type=Commission liên quan đến đơn. |

#### Idempotency

- Nếu gọi 2 lần → lần 2 skip (kiểm tra reversal entries đã tồn tại cho CorrelationId)
- An toàn khi retry

#### Khi flag OFF

- Hủy đơn = silent cancel (behavior cũ — không đảo gì)
- INV-002 bị vi phạm (refunded nhưng reward không reversed) — đây là gap đã biết, chỉ fix khi bật flag

> **Cảnh báo:** Tắt flag **không** hoàn tác reversal entries đã tạo. Đơn đã hủy khi ON sẽ giữ bút toán đảo.

### 4.4. Kiểm tra impact trên báo cáo kế toán

Khi RefundReversal ON, báo cáo kế toán sẽ phản ánh:

- **B 01-DN (Báo cáo tình hình TC):** Có mục "Phải trả khách hàng" (331) tăng khi có refund
- **B 02-DN (KQ HĐKD):** Doanh thu giảm (reversal entry đảo dấu revenue)
- **Cash = Accounting:** Đảm bảo theo TT 152/2025/TT-BTC

---

## 5. Hướng dẫn cho Customer (Khách hàng)

Customer là người dùng cuối qua KhachLink PWA — không cần thao tác gì thêm, nhưng bị ảnh hưởng gián tiếp.

### 5.1. Tích điểm (khi Loyalty Budget ON)

- Khách vẫn tích điểm bình thường khi đặt hàng
- **Nếu budget tenant hết** → khách có thể nhận ít điểm hơn dự kiến, hoặc 0 điểm
- Không có thông báo lỗi cho khách — chỉ log ở backend
- Budget reset hàng ngày (00:00 UTC) + đầu tháng → khách lại nhận điểm bình thường

### 5.2. Hoàn điểm khi hủy đơn (khi Refund Reversal ON)

- Khi đơn bị hủy → điểm thưởng của đơn đó bị **trừ ngược** (SubtractPoints)
- `LoyaltyIssuanceRecord.IsReversed = true` — đánh dấu đã đảo
- Nếu khách đã dùng điểm để đổi thưởng → balance có thể âm (không block, chỉ track)

### 5.3. Không thay đổi UX

- VALCN v2.0 **không thay đổi giao diện KhachLink**
- Tất cả thay đổi ở backend (feature flags, accounting, budget)
- Khách hàng không cần biết về feature flags

---

## 6. Hướng dẫn cho Investor (Nhà đầu tư)

Investor xem metrics tổng hợp qua Network Dashboard (do SystemAdmin trình bày).

### 6.1. Truy cập

- Investor không đăng nhập trực tiếp — SystemAdmin mở `/admin/network-dashboard` và trình bày
- Dashboard là **read-only** — không có thao tác chỉnh sửa

### 6.2. 8 Metrics giải thích cho investor

| Metric | Giải thích cho investor |
|--------|-------------------------|
| **GMV** | Tổng quy mô giao dịch toàn network — chỉ số tăng trưởng |
| **Active Tenants** | Số quán đang hoạt động — chỉ số adoption |
| **Active Customers** | Số khách hàng distinct — chỉ số reach |
| **Repeat Rate** | % khách quay lại — chỉ số retention (flywheel) |
| **Platform Revenue** | Doanh thu Vạn An từ phí nền tảng — unit economics |
| **Loyalty Cost** | Chi phí Vạn An chi cho điểm thưởng — investment vào retention |
| **Loyalty ROI** | Hiệu quả loyalty — (repeatGmv - cost) / cost. >1 = lợi nhuận, <1 = lỗ |
| **Contribution Profit** | Lợi nhuận đóng góp — PlatformRevenue - LoyaltyCost (chưa trừ Ops Cost) |

### 6.3. Giải thích công thức LoyaltyROI (fix C4)

```
LoyaltyROI = (repeatGmv - loyaltyCost) / loyaltyCost
```

- **repeatGmv** = GMV từ khách hàng có >1 đơn (repeat customers)
- **loyaltyCost** = tổng điểm phát hành × 1,000 VND (fallback)
- **Tại sao repeatGmv không phải totalGmv?** Vì loyalty chỉ ảnh hưởng đến khách quay lại — khách 1 lần thì loyalty không có role

### 6.4. Hạn chế hiện tại (để giải thích với investor)

- **Loyalty Cost dùng fallback 1,000 VND/point** — số liệu thực tế có thể khác (v3.0 sẽ có PointValue field)
- **Ops Cost chưa trừ** trong Contribution Profit (defer v3.0) — Contribution Profit là upper bound
- **Cache 10 phút** — số liệu không real-time, làm mới mỗi 10 phút

---

## 7. Bảng tra cứu Feature Flags

| Flag | SystemSetting Key | Hook point | Khi ON | Khi OFF (mặc định) |
|------|-------------------|------------|--------|---------------------|
| `ValcnV2_PlatformFee` | `Features:EnableValcnV2_PlatformFee` | `OrderService.SnapshotCommerceModeAsync` | Set PlatformFeeAmount trên Marketplace orders | PlatformFeeRate/Amount = null (no-op) |
| `ValcnV2_LoyaltyBudget` | `Features:EnableValcnV2_LoyaltyBudget` | `OrderWorkflowService.ProcessLoyaltyPointsAsync` | Check 4 caps trước AddPoints + 2 reset jobs | AddPoints trực tiếp (no cap) |
| `ValcnV2_RefundReversal` | `Features:EnableValcnV2_RefundReversal` | `OrderWorkflowService.HandleOrderCancelledAsync` | 4-step reversal (2a+2b+2c+2d) | Silent cancel (no reversal) |

> **Network Dashboard** không có flag — luôn sẵn sàng cho SystemAdmin (read-only, không thay đổi behavior).

---

## 8. Câu hỏi thường gặp (FAQ)

### Q: Tôi bật feature flag nhưng không thấy hiệu lực?
**A:** Cache TTL 30 giây — đợi 30s rồi kiểm tra lại. Hoặc click "Làm mới" trên trang `/admin/valcn-features`.

### Q: Bật Refund Reversal ảnh hưởng đơn đã hủy trước đó không?
**A:** **Không.** Chỉ áp dụng cho đơn hủy *sau khi* bật flag. Đơn đã hủy khi OFF giữ behavior cũ (silent cancel).

### Q: Tắt flag có hoàn tác dữ liệu đã tạo không?
**A:** **Không.** Reversal entries, budget counter changes, wallet reversals đã tạo sẽ giữ nguyên. Tắt flag chỉ ảnh hưởng đơn *mới*.

### Q: Loyalty Budget hết → khách có bị lỗi không?
**A:** Không. Khách nhận 0 điểm (thay vì points dự kiến). Không có error cho khách, chỉ log warning ở backend.

### Q: Network Dashboard hiển thị 0 cho mọi metric?
**A:** Kiểm tra: (1) có đơn hàng trong khoảng ngày đã chọn không, (2) Gateway có đang chạy không (ShopERP gọi Gateway qua HTTP), (3) thử mở rộng khoảng ngày (mặc định 30 ngày).

### Q: Platform Fee tính trên Reseller orders không?
**A:** **Không.** Chỉ áp dụng Marketplace mode. Reseller mode có cơ chế margin riêng (CostPrice/SellPrice/PlatformMargin).

### Q: Loyalty Cost trên Network Dashboard chính xác không?
**A:** **Chưa chính xác tuyệt đối.** Dùng fallback 1,000 VND/point (INV-009 chưa triển khai). v3.0 sẽ có PointValue field trong LoyaltyGlobalConfig để tính chính xác.

### Q: Tôi có thể bật Refund Reversal mà không bật Loyalty Budget không?
**A:** Có, nhưng **không khuyến nghị**. Bước 2c (loyalty reversal) vẫn chạy SubtractPoints + MarkReversed, nhưng `DecrementIssuanceAsync` không có config để giảm → budget counters sai. Bật theo thứ tự: PlatformFee → LoyaltyBudget → RefundReversal.

### Q: Đơn hủy khi Refund Reversal ON, sau đó tôi tắt flag rồi hủy tiếp đơn khác → behavior thế nào?
**A:** Đơn 1 (hủy khi ON) → 4-step reversal. Đơn 2 (hủy khi OFF) → silent cancel. Mỗi đơn xử lý độc lập theo trạng thái flag tại thời điểm hủy.

---

## 9. Khắc phục sự cố

### 9.1. Network Dashboard không tải được

**Triệu chứng:** Trang hiển thị "Không thể tải metrics. Kiểm tra kết nối Gateway."

**Nguyên nhân & xử lý:**

1. **Gateway không khả dụng** → Kiểm tra `vanan-gateway-1` container đang healthy
2. **Internal API key sai** → Kiểm tra config `InternalLoyalty:ApiKey` ở cả Gateway + ShopERP
3. **Lỗi 500 từ Gateway** → Xem log Gateway: `docker logs vanan-gateway-1 --tail 50`

### 9.2. Feature flag bật nhưng không thấy thay đổi

**Triệu chứng:** Bật `ValcnV2_PlatformFee` nhưng đơn mới vẫn không có PlatformFeeAmount.

**Xử lý:**

1. Đợi 30 giây (cache TTL)
2. Kiểm tra đơn có phải **Marketplace mode** không (Reseller không áp dụng)
3. Kiểm tra `ShopFeatureSettingsEntity.PlatformFeeRate` có set không (nếu null → fallback global)
4. Kiểm tra log `OrderService.SnapshotCommerceModeAsync` có log "feature ON" không

### 9.3. Refund Reversal chạy nhưng loyalty points không đảo

**Triệu chứng:** Hủy đơn khi ON, accounting reversal tạo, nhưng customer balance không giảm.

**Xử lý:**

1. Kiểm tra có `LoyaltyIssuanceRecord` cho OrderId không (Phase 1 entity — nếu đơn tạo trước Phase 1 thì không có record)
2. Kiểm tra `LoyaltyIssuanceRecord.IsReversed` — nếu true thì đã đảo rồi (idempotency skip)
3. Kiểm tra `SubtractPointsAsync` có throw không (xem log RefundOrchestrationService)

### 9.4. Loyalty Budget reset job không chạy

**Triệu chứng:** `PointsIssuedToday` không reset về 0 lúc 00:00 UTC.

**Xử lý:**

1. Kiểm tra job có enabled không tại `/admin/background-services`
2. Kiểm tra `LoyaltyBudgetDailyResetJob` log (chạy lúc 00:00 UTC — convert sang giờ local)
3. Kiểm tra Gateway container đang chạy (job chạy ở Gateway, không phải ShopERP)

---

## Phụ lục: Thông tin kỹ thuật

### Kiến trúc

```
KhachLink (5002) → Gateway (5001, order creator) → NATS (routed) → ShopERP (5003, per-tenant SQLite)
                    PG: Orders + Accounting + Tenants + LoyaltyTenantConfig
```

### Feature Flag Service

- **File:** `3_CoreHub/Services/FeatureFlagService.cs`
- **Pattern:** Singleton + `IServiceScopeFactory` + 30s `IMemoryCache`
- **Storage:** `SystemSetting` table (PG), key `Features:Enable{FeatureName}`, value `"true"`/`"false"`
- **Default:** `false` (OFF) — opposite of `BackgroundServiceToggleService`

### Các file chính VALCN v2.0

| File | Vai trò |
|------|---------|
| `3_CoreHub/Services/FeatureFlagService.cs` | Feature flag service (3 flags) |
| `3_CoreHub/Services/RefundOrchestrationService.cs` | 4-step reversal (Phase 4) |
| `3_CoreHub/Services/NetworkDashboardService.cs` | Cross-tenant metrics (Phase 7) |
| `3_CoreHub/Services/LoyaltyBudgetService.cs` | Budget enforcement (Phase 3) |
| `2_Gateway/Controllers/NetworkDashboardController.cs` | Internal API (Phase 7) |
| `5_WebApps/ShopERP/Components/Pages/Admin/ValcnFeatures.razor` | Feature flag admin UI |
| `5_WebApps/ShopERP/Components/Pages/Admin/NetworkDashboard.razor` | Network dashboard UI |

### Commits

| Wave | Commit | Phases |
|------|--------|--------|
| Wave 1 | `af09b8d0` | Phase 0 + Phase 1 |
| Wave 2 | `f1d46f24` + `7edf589a` | Phase 2 + Phase 3 |
| Wave 3 | `9a4d0e9b` | Phase 4 + Phase 7 |

---

**Tài liệu này áp dụng cho VALCN v2.0 PLATFORM-LIGHT (2026-08-09). Phiên bản v3.0 sẽ bổ sung: PointValue field (INV-009), payment provider integration, Ops Cost metric, Merchant Tiering.**
