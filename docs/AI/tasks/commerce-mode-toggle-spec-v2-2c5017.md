# Commerce Mode Toggle — Spec v2.1 (Sprint 7)

**Nguyên tắc cốt lõi:** "Mua giúp — Bán dùm". Vạn An mua hàng hóa từ tenant rồi bán lại cho khách hàng cuối. Vạn An quyết định tỷ lệ phân chia lợi nhuận thành: chi phí bán hàng (salesman), chi phí giao hàng (shipper), chi phí nền tảng hạ tầng, quỹ phát triển cộng đồng.

**Cơ chế:** Toggle toàn cục (Global) + override cấp tenant. SysAdmin thiết lập mode mặc định cho toàn platform. Tenant cụ thể có thể override (ví dụ: tenant lớn giữ Marketplace, tenant nhỏ dùng Reseller).

> **Revision history**
> - v2.1 — 2026-07-30 — ANALYZE phase complete. Fixed 2 spec errors (WalletTransactionType count, DeliveryFee vs ShippingFee). Resolved Q1-Q5. Full scope: Q3 (community fund management) + Q5 (non-COD Reseller) included. Added ProductCostPrice + CommunityFundSpendRecord entities. Enum values renumbered 7-11. ~24 tests, 4 sessions, 18 VPS RV checks.
> - v2.0 — 2026-07-30 — Initial draft. Bổ sung Commerce Mode Toggle (Marketplace ↔ Reseller) lên nền tảng Community Commerce đã deploy (Sprint 0-6). Additive — không phá existing data/logic. Past orders giữ mode snapshot tại creation time.

---

## 1. Business Context

### 1.1 Hai mô hình thương mại

| Khía cạnh | Marketplace (hiện tại — Sprint 0-6) | Reseller (mới — Sprint 7) |
|---|---|---|
| **Vai trò Vạn An** | Nền tảng giới thiệu + giao hàng | Bên mua từ tenant → bán lại cho customer |
| **Ai định giá** | Tenant tự định giá | Vạn An định giá bán (dựa trên cost price từ tenant + margin) |
| **Dòng tiền COD** | Shipper thu hộ → shop nhận trực tiếp | Customer trả → Vạn An nhận → Vạn An phân phối (shop + shipper + salesman + platform) |
| **Commission base** | `% orderTotal` | `% margin` (SellPrice - CostPrice) |
| **Advance payment** | Shipper ứng tiền cho shop | Vạn An ứng tiền cho shop (mua trước) |
| **Settlement** | Shipper ↔ Shop trực tiếp | Tất cả qua Vạn An wallet |
| **Platform fee** | Không có (Vạn An chỉ thu commission từ salesman) | Vạn An giữ margin → phân chia 4 khoản |
| **Community fund** | Không có | % margin vào quỹ phát triển cộng đồng |

### 1.2 Toggle mechanism

```
┌─────────────────────────────────────────────────────┐
│  Global Setting (SystemAdmin)                       │
│  CommerceMode = Marketplace (default) | Reseller    │
│                                                     │
│  ┌──────────────┐    ┌──────────────┐              │
│  │  Tenant A    │    │  Tenant B    │              │
│  │  Override:   │    │  Override:   │              │
│  │  Inherit     │    │  Reseller    │              │
│  │  → Marketplace│   │  → Reseller  │              │
│  └──────────────┘    └──────────────┘              │
│                                                     │
│  ┌──────────────┐                                  │
│  │  Tenant C    │                                  │
│  │  Override:   │                                  │
│  │  Marketplace │                                  │
│  │  → Marketplace│                                 │
│  └──────────────┘                                  │
└─────────────────────────────────────────────────────┘
```

**Quy tắc ưu tiên:**
1. `TenantSettings.CommerceModeOverride` ≠ `Inherit` → dùng override
2. `TenantSettings.CommerceModeOverride` == `Inherit` (hoặc null) → dùng `GlobalCommerceMode`
3. Mỗi Order snapshot `CommerceMode` tại creation time — toggle affect **future orders only**

### 1.3 Khi nào bật Reseller?

| Giai đoạn | Mode | Lý do |
|---|---|---|
| PoC (50 users) | Marketplace (default) | Friction thấp, tenant tự định giá, nhanh onboarding |
| Scale (500+ users) | Reseller (toggle ON) | Vạn An kiểm soát margin, tối ưu phân chia, thu phí nền tảng |
| Tenant lớn (F&B chain) | Override: Marketplace | Họ tự định giá, không muốn Vạn An chen vào |
| Tenant nhỏ (cửa hàng cá thể) | Override: Reseller (hoặc Inherit) | Vạn An lo toàn bộ, họ chỉ cần giao hàng |

---

## 2. Domain Changes (Additive — cần approval per governance)

### 2.1 CommerceMode enum (MỚI)

```csharp
public enum CommerceMode
{
    Marketplace = 0,   // Sprint 0-6 hiện tại — tenant bán trực tiếp
    Reseller = 1,      // Sprint 7 — Vạn An mua-bán lại
    Inherit = -1       // Tenant override: dùng global setting
}
```

### 2.2 Order fields bổ sung (additive, nullable — backward compatible)

```csharp
Order (existing, add fields):
- CommerceMode (CommerceMode, default Marketplace) — snapshot tại creation, KHÔNG nullable
- CostPrice (decimal?) — Reseller only: giá Vạn An mua từ tenant (per-order, snapshot)
- SellPrice (decimal?) — Reseller only: giá Vạn An bán cho customer (per-order, snapshot)
- PlatformMargin (decimal?) — Reseller only: SellPrice - CostPrice (computed, snapshot)
- DeliveryFee (decimal?) — Reseller only: phí giao hàng Vạn An trả shipper
- PlatformFeeRate (decimal?) — Reseller only: % margin Vạn An giữ (snapshot từ config)
- CommunityFundRate (decimal?) — Reseller only: % margin vào quỹ cộng đồng (snapshot)
```

**Lưu ý:**
- `TotalAmount` giữ nguyên — Marketplace mode: `TotalAmount` = giá customer trả. Reseller mode: `TotalAmount` = `SellPrice` (alias, cùng giá trị).
- Tất cả field mới nullable (trừ `CommerceMode`) — Marketplace orders có `CostPrice = null`.
- Snapshot tại creation: không thay đổi khi toggle sau đó.
- **`DeliveryFee` vs `ShippingFee` (v2.1 correction):** Order đã có `ShippingFee` (line 1498 — tenant-set delivery cost, Marketplace). `DeliveryFee` (new) = VanAn's fee paid to shipper in Reseller mode. Distinct semantics, separate columns. Per user decision D1 (2026-07-30).

### 2.3 WalletTransactionType bổ sung (enum append)

```csharp
WalletTransactionType (existing 6 + new 5):
// VERIFIED 2026-07-30: actual code has 6 values (not 8 as v2.0 claimed).
// Deposit=7/SmsOtpFee=8 belong to CC-S6-T5 (not yet implemented).
- CODCollection = 1     // (existing) Marketplace: shipper thu hộ
- AdvancePayment = 2    // (existing) Marketplace: shipper ứng
- Commission = 3        // (existing) cả 2 mode
- Withdrawal = 4        // (existing)
- Settlement = 5        // (existing) Marketplace: shipper↔shop. Reseller: Vạn An↔shop
- Reversal = 6          // (existing)
- PlatformFee = 7       // (MỚI) Reseller: Vạn An giữ margin
- CommunityFund = 8     // (MỚI) Reseller: quỹ phát triển cộng đồng
- DeliveryFee = 9       // (MỚI) Reseller: phí giao hàng trả shipper (tách khỏi COD)
- ExternalPayment = 10  // (MỚI, Q5) non-COD Reseller: customer trả Vạn An qua VietQR/card
- CommunityFundSpend = 11 // (MỚI, Q3) community fund disbursement (SysAdmin rút tiền tái đầu tư)
```
**Coordination note:** If CC-S6-T5 (SMS OTP toggle) lands first, it takes 7=Deposit, 8=SmsOtpFee, and Sprint 7 renumbers to 9-13.

### 2.4 ProductReferralConfig bổ sung

```csharp
ProductReferralConfig (existing, add fields):
- CommissionBase (enum CommissionBase: OnOrderTotal=0, OnMargin=1) — MỚI
  - Marketplace: OnOrderTotal (default, existing behavior)
  - Reseller: OnMargin (commission tính trên PlatformMargin)
```

**Lý do thêm field (không overload semantics):** Cùng `CommissionRate` field, 2 ý nghĩa theo mode. Thêm `CommissionBase` để code check rõ ràng, không guess.

### 2.5 TenantSettings bổ sung

```csharp
TenantSettings (existing value object, add field):
- CommerceModeOverride (CommerceMode, default Inherit) — MỚI
  - Inherit (-1): dùng GlobalCommerceMode (default)
  - Marketplace (0): ép Marketplace cho tenant này
  - Reseller (1): ép Reseller cho tenant này
```

**With method:** `WithCommerceModeOverride(CommerceMode mode)` — immutable update pattern (giống existing `WithSlug`, `WithTheme`).

### 2.6 Global Setting (SystemSetting — MỚI entity hoặc config)

```csharp
// Option A: SystemSetting entity (key-value table) — APPROVED
SystemSetting : BaseEntity, IMustHaveTenant (TenantId nullable — global settings)
- Key (string, unique, 100 chars)
- Value (string, 500 chars)
- UpdatedAt (DateTime?)
- UpdatedBy (Guid?)

// Option B: hardcode trong appsettings.json + IOptions pattern — REJECTED (no runtime toggle)
```

**Settings cần thiết:**
- `GlobalCommerceMode` (CommerceMode, default Marketplace)
- `DefaultPlatformFeeRate` (decimal, default 0.30 = 30% margin Vạn An giữ)
- `DefaultCommunityFundRate` (decimal, default 0.05 = 5% margin vào quỹ)
- `DefaultDeliveryFee` (decimal, default 15000 = 15K VND per order)

### 2.7 ProductCostPrice entity (MỚI — Q1 resolution)

```csharp
ProductCostPrice : BaseEntity, IMustHaveTenant
- ProductId (Guid) — FK reference (Product lives in ShopERP SQLite; this entity in Gateway PG)
- TenantId (Guid) — which tenant this cost price applies to
- CostPrice (decimal) — giá Vạn An mua từ tenant (negotiated offline)
- UpdatedAt (DateTime?)
- UpdatedBy (Guid?)
// Unique index on (TenantId, ProductId)
// Van An admin sets via POST /api/admin/product-cost-price. Tenant negotiates offline.
```

### 2.8 CommunityFundSpendRecord entity (MỚI — Q3 resolution)

```csharp
CommunityFundSpendRecord : BaseEntity, IMustHaveTenant (TenantId = Guid.Empty — system-wide)
- Amount (decimal) — số tiền rút từ quỹ
- Reason (string, 500 chars) — lý do chi (vd: "Tài trợ sự kiện cộng đồng X")
- Recipient (string, 200 chars) — người nhận / đối tác
- ApprovedBy (Guid) — SystemAdmin userId
- SpentAt (DateTime)
- WalletTransactionId (Guid) — FK to WalletTransaction (CommunityFundSpend tx)
// Audit trail for community fund disbursements.
```

---

## 3. Financial Flow — Reseller Mode

### 3.1 Order Creation (Reseller mode)

```
1. Customer places order (KhachLink)
2. Gateway receives order request:
   a. Resolve tenant → check CommerceMode (tenant override or global)
   b. Load product CostPrice from tenant (via NATS query or cached)
   c. Compute: SellPrice = CostPrice + Margin
      Margin = CostPrice * DefaultPlatformFeeRate (or per-product override)
   d. Snapshot vào Order: CommerceMode=Reseller, CostPrice, SellPrice, PlatformMargin, DeliveryFee, PlatformFeeRate, CommunityFundRate
   e. TotalAmount = SellPrice + DeliveryFee (customer trả cả hàng + ship)
3. Order flows: pending → confirmed → preparing → ready → delivering (giống hiện tại)
```

### 3.2 COD Flow (Reseller mode) — Vạn An là trung gian

```
1. Shipper delivers to customer
2. Shipper collects cash from customer (COD = SellPrice + DeliveryFee)
3. Shipper taps "Đã thu COD" → POST /api/community/wallet/confirm-cod
4. System creates (Reseller mode):
   a. WalletTransaction(CODCollection, +codAmount, shipper) — shipper thu hộ
   b. WalletTransaction(Settlement, +costPrice, shop/tenant) — Vạn An trả tenant giá vốn
   c. WalletTransaction(DeliveryFee, +deliveryFee, shipper) — Vạn An trả shipper phí giao
   d. WalletTransaction(Commission, +commissionAmount, salesman) — Vạn An trả salesman (if referral)
   e. WalletTransaction(PlatformFee, +platformFee, VANAN_PLATFORM_WALLET) — Vạn An giữ margin
   f. WalletTransaction(CommunityFund, +communityFund, VANAN_COMMUNITY_FUND_WALLET) — quỹ cộng đồng
   g. Order.CodCollectedAt = now
5. Financial balance check:
   COD collected = CostPrice + DeliveryFee + Commission + PlatformFee + CommunityFund
   (tất cả khoản phân chia = tổng tiền customer trả)
```

**So sánh Marketplace mode (hiện tại):**
```
Marketplace:
   a. WalletTransaction(CODCollection, +codAmount, shipper)
   b. WalletTransaction(Settlement, -codAmount, shop) — shop owes shipper
   → Chỉ 2 transaction. Vạn An không tham gia.
```

### 3.3 Advance Payment Flow (Reseller mode) — Vạn An ứng tiền

```
1. Order confirmed → Vạn An cần mua hàng từ tenant
2. Vạn An ứng tiền cho tenant (AdvancePayment)
   a. WalletTransaction(AdvancePayment, -advanceAmount, VANAN_PLATFORM_WALLET) — Vạn An ứng
   b. WalletTransaction(Settlement, +advanceAmount, shop/tenant) — tenant nhận
3. Shipper picks up from shop (không tham gia tài chính)
4. Shipper delivers → COD flow (§3.2)
5. Settlement final:
   - Vạn An đã ứng: advanceAmount
   - Vạn An phải trả tenant thêm: costPrice - advanceAmount (nếu costPrice > advance)
   - Hoặc Vạn An thu lại: advanceAmount - costPrice (nếu advance > cost)
```

**So sánh Marketplace mode (hiện tại):**
```
Marketplace:
   a. WalletTransaction(AdvancePayment, -advanceAmount, shipper) — shipper ứng
   b. Shop confirms received → WalletTransaction(Settlement, +advanceAmount, shop)
   → Shipper là bên ứng, không phải Vạn An.
```

### 3.4 Commission Calculation (Reseller mode)

```
Marketplace:  Commission = orderTotal * CommissionRate      (vd: 100K * 5% = 5K)
Reseller:     Commission = PlatformMargin * CommissionRate   (vd: margin 20K * 5% = 1K)
```

**Code logic:**
```csharp
decimal commissionBase = order.CommerceMode == CommerceMode.Reseller
    ? order.PlatformMargin ?? 0          // Reseller: tính trên margin
    : order.TotalAmount;                  // Marketplace: tính trên orderTotal

decimal commission = commissionBase * config.CommissionRate;
```

### 3.5 Platform Wallet & Community Fund Wallet

**Vấn đề:** Reseller mode cần 2 "system wallet":
- `VANAN_PLATFORM_WALLET` — Vạn An giữ platform fee
- `VANAN_COMMUNITY_FUND_WALLET` — quỹ phát triển cộng đồng

**Giải pháp:** Dùng `OwnerId` đặc biệt (reserved GUID):
```csharp
public static class SystemWalletIds
{
    public static readonly Guid PlatformWallet = Guid.Parse("00000000-0000-0000-0000-000000000001");
    public static readonly Guid CommunityFund = Guid.Parse("00000000-0000-0000-0000-000000000002");
}
```

WalletTransaction với `OwnerId = SystemWalletIds.PlatformWallet` → Vạn An platform wallet. Không cần entity Customer cho 2 wallet này.

---

## 4. API Specifications

### 4.1 GET /api/admin/commerce-mode (MỚI — SystemAdmin)

```
Header: Authorization: Bearer {adminJWT}
Auth: SystemAdmin policy
Response 200: {
  "globalMode": "Marketplace",
  "defaultPlatformFeeRate": 0.30,
  "defaultCommunityFundRate": 0.05,
  "defaultDeliveryFee": 15000,
  "tenantOverrides": [
    { "tenantId": "guid", "tenantName": "Shop A", "override": "Reseller" },
    { "tenantId": "guid", "tenantName": "Shop B", "override": "Inherit" }
  ]
}
```

### 4.2 POST /api/admin/commerce-mode/global (MỚI — SystemAdmin)

```
Header: Authorization: Bearer {adminJWT}
Body: { "mode": "Marketplace" | "Reseller", "platformFeeRate": 0.30, "communityFundRate": 0.05, "deliveryFee": 15000 }
Response 200: { "updated": true }
Side effects: Future orders use new mode. Past orders unaffected (snapshot).
```

### 4.3 POST /api/admin/commerce-mode/tenant/{tenantId} (MỚI — SystemAdmin)

```
Header: Authorization: Bearer {adminJWT}
Body: { "override": "Inherit" | "Marketplace" | "Reseller" }
Response 200: { "updated": true }
Response 404: Tenant not found
Side effects: Future orders for this tenant use override. Past orders unaffected.
```

### 4.4 GET /api/community/commerce-mode (MỚI — CustomerToken, cho UI)

```
Header: X-Customer-Token
Response 200: {
  "mode": "Marketplace",
  "isReseller": false
}
```
UI dùng để: hiển thị giá khác nhau (Marketplace: giá tenant. Reseller: giá Vạn An), ẩn/hiện advance payment button, v.v.

### 4.5 Modified: POST /api/community/wallet/confirm-cod (dual-mode)

```
Header: X-Customer-Token
Body: { "orderId": "guid", "amount": 50000 }
Response 200: {
  "transactionId": "guid",
  "balanceAfter": 150000,
  "mode": "Marketplace",
  "transactionsCreated": 2
}
// Reseller mode:
Response 200: {
  "transactionId": "guid",
  "balanceAfter": 150000,
  "mode": "Reseller",
  "transactionsCreated": 6,
  "breakdown": {
    "costPrice": 80000,
    "deliveryFee": 15000,
    "commission": 1000,
    "platformFee": 3000,
    "communityFund": 1000
  }
}
```

---

## 5. Service Specifications

### 5.1 ICommerceModeService (MỚI)

```csharp
public interface ICommerceModeService
{
    Task<CommerceMode> GetGlobalModeAsync();
    Task SetGlobalModeAsync(CommerceMode mode, decimal platformFeeRate, decimal communityFundRate, decimal deliveryFee);
    Task<CommerceMode> GetTenantModeAsync(Guid tenantId);
    Task SetTenantOverrideAsync(Guid tenantId, CommerceMode override);
    Task<CommerceMode> ResolveModeForOrderAsync(Guid tenantId); // Check override → fallback global
    Task<CommerceModeConfig> GetConfigAsync(); // All settings + tenant overrides list
}
```

### 5.2 WalletService — dual-mode ConfirmCodAsync

```csharp
public async Task<WalletTransaction> ConfirmCodAsync(Guid shipperId, Guid orderId, decimal amount)
{
    var order = await LoadOrderAsync(orderId);

    if (order.CommerceMode == CommerceMode.Reseller)
        return await ConfirmCodResellerAsync(shipperId, order, amount);
    else
        return await ConfirmCodMarketplaceAsync(shipperId, order, amount); // existing logic
}

private async Task<WalletTransaction> ConfirmCodResellerAsync(Guid shipperId, Order order, decimal amount)
{
    // 1. CODCollection tx for shipper (+amount) — thu hộ
    var shipperTx = await CreateTransactionAsync(shipperId, CODCollection, amount, ...);

    // 2. Settlement tx for tenant (+costPrice) — Vạn An trả tenant giá vốn
    await CreateTransactionAsync(order.TenantId.Value, Settlement, order.CostPrice.Value, ...);

    // 3. DeliveryFee tx for shipper (+deliveryFee) — Vạn An trả phí giao
    await CreateTransactionAsync(shipperId, DeliveryFee, order.DeliveryFee.Value, ...);

    // 4. Commission tx for salesman (if referral) — Vạn An trả commission
    if (order.SalesmanId.HasValue)
    {
        var commission = (order.PlatformMargin ?? 0) * order.CommissionRate;
        await CreateTransactionAsync(order.SalesmanId.Value, Commission, commission, ...);
    }

    // 5. PlatformFee tx for Vạn An wallet (+platformFee)
    var platformFee = (order.PlatformMargin ?? 0) * order.PlatformFeeRate;
    await CreateTransactionAsync(SystemWalletIds.PlatformWallet, PlatformFee, platformFee, ...);

    // 6. CommunityFund tx for community fund wallet (+communityFund)
    var communityFund = (order.PlatformMargin ?? 0) * order.CommunityFundRate;
    await CreateTransactionAsync(SystemWalletIds.CommunityFund, CommunityFund, communityFund, ...);

    // 7. Mark order COD collected
    order.MarkCodCollected(amount);
    await _dbContext.SaveChangesAsync();

    return shipperTx;
}

private async Task<WalletTransaction> ConfirmCodMarketplaceAsync(Guid shipperId, Order order, decimal amount)
{
    // EXISTING Sprint 5 logic — unchanged
    // 1. CODCollection tx for shipper (+amount)
    // 2. Settlement tx for shop (-amount)
    // 3. Order.MarkCodCollected(amount)
}
```

### 5.3 WalletService — dual-mode ConfirmAdvanceAsync

```csharp
public async Task<WalletTransaction> ConfirmAdvanceAsync(Guid callerId, Guid orderId, decimal amount)
{
    var order = await LoadOrderAsync(orderId);

    if (order.CommerceMode == CommerceMode.Reseller)
        return await ConfirmAdvanceResellerAsync(order, amount); // Vạn An ứng
    else
        return await ConfirmAdvanceMarketplaceAsync(callerId, order, amount); // Shipper ứng (existing)
}

private async Task<WalletTransaction> ConfirmAdvanceResellerAsync(Order order, decimal amount)
{
    // Vạn An platform wallet ứng tiền cho tenant
    // 1. AdvancePayment tx for Vạn An wallet (-amount)
    await CreateTransactionAsync(SystemWalletIds.PlatformWallet, AdvancePayment, -amount, ...);

    // 2. Settlement tx for tenant (+amount) — tenant nhận tiền ứng
    await CreateTransactionAsync(order.TenantId.Value, Settlement, amount, ...);

    // Note: Shipper không tham gia tài chính trong Reseller advance
}
```

### 5.4 SalesmanService — dual-mode commission calculation

```csharp
// In CreateCommissionAsync (existing method, add mode check):
var commissionBase = order.CommerceMode == CommerceMode.Reseller
    ? order.PlatformMargin ?? 0
    : order.TotalAmount;

referral.AttachToOrder(orderId, order.CustomerId ?? Guid.Empty, commissionBase, config.CommissionRate);
// CommissionAmount = commissionBase * CommissionRate
```

---

## 6. UI Specifications

### 6.1 Admin Commerce Mode Settings (ShopERP — MỚI)

```
@page "/admin/commerce-mode"
@attribute [Authorize(Roles="SystemAdmin")]
- Header: "Thiết lập mô hình thương mại"
- Global settings card:
  - Mode toggle: Marketplace (default) / Reseller — radio buttons
  - Platform Fee Rate: % slider (10-50%, default 30%)
  - Community Fund Rate: % slider (1-10%, default 5%)
  - Default Delivery Fee: number input (VND, default 15000)
  - "Lưu" button → POST /api/admin/commerce-mode/global
  - Warning: "Thay đổi áp dụng cho đơn hàng mới. Đơn hàng cũ không bị ảnh hưởng."
- Tenant overrides table:
  - Columns: Tenant, Current Mode (resolved), Override, Actions
  - Override dropdown: Inherit / Marketplace / Reseller
  - "Lưu" button per row → POST /api/admin/commerce-mode/tenant/{id}
- Nav link: thêm vào ShopERP AdminLayout + NavMenu (Community section)
```

### 6.2 KhachLink — mode-aware UI

```
// Wallet.razor — show mode badge
@if (commerceMode == "Reseller")
{
    <VanAnBadge Variant="Info">Mô hình Reseller — Vạn An mua bán</VanAnBadge>
}
else
{
    <VanAnBadge Variant="Secondary">Mô hình Marketplace — Tenant bán trực tiếp</VanAnBadge>
}

// DeliveryTracking.razor — advance payment button
@if (commerceMode == "Marketplace")
{
    // Show advance payment button (shipper ứng tiền) — existing Sprint 5
    <VanAnButton OnClick="ConfirmAdvance">Ứng tiền cho shop</VanAnButton>
}
// Reseller mode: ẩn advance button (Vạn An ứng, không phải shipper)

// NearbyProducts.razor — price display
@if (commerceMode == "Reseller")
{
    // Show SellPrice (Vạn An price) + "Giá đã bao gồm phí nền tảng"
}
else
{
    // Show tenant price (existing)
}
```

---

## 7. Migration Plan

### 7.1 Database migration (additive)

```sql
-- Order table: add columns (nullable, backward compatible)
ALTER TABLE "Orders" ADD COLUMN "CommerceMode" int NOT NULL DEFAULT 0; -- Marketplace
ALTER TABLE "Orders" ADD COLUMN "CostPrice" decimal(18,2) NULL;
ALTER TABLE "Orders" ADD COLUMN "SellPrice" decimal(18,2) NULL;
ALTER TABLE "Orders" ADD COLUMN "PlatformMargin" decimal(18,2) NULL;
ALTER TABLE "Orders" ADD COLUMN "DeliveryFee" decimal(18,2) NULL;
ALTER TABLE "Orders" ADD COLUMN "PlatformFeeRate" decimal(5,4) NULL;
ALTER TABLE "Orders" ADD COLUMN "CommunityFundRate" decimal(5,4) NULL;

-- TenantSettings: add CommerceModeOverride (owned entity → column in Tenants table)
ALTER TABLE "Tenants" ADD COLUMN "Settings_CommerceModeOverride" int NOT NULL DEFAULT -1; -- Inherit

-- ProductReferralConfig: add CommissionBase
ALTER TABLE "ProductReferralConfigs" ADD COLUMN "CommissionBase" int NOT NULL DEFAULT 0; -- OnOrderTotal

-- SystemSetting table (NEW)
CREATE TABLE "SystemSettings" (
    "Id" uuid PRIMARY KEY,
    "Key" varchar(100) UNIQUE NOT NULL,
    "Value" varchar(500) NOT NULL,
    "UpdatedAt" timestamp NULL,
    "UpdatedBy" uuid NULL,
    "TenantId" uuid NULL -- global settings have NULL TenantId
);

-- Seed default settings
INSERT INTO "SystemSettings" ("Id", "Key", "Value") VALUES
(NEWID(), 'GlobalCommerceMode', '0'),          -- Marketplace
(NEWID(), 'DefaultPlatformFeeRate', '0.30'),
(NEWID(), 'DefaultCommunityFundRate', '0.05'),
(NEWID(), 'DefaultDeliveryFee', '15000');
```

### 7.2 Existing data — no change needed

- All existing orders: `CommerceMode = 0 (Marketplace)` — default value
- All existing WalletTransactions: unaffected (no new types used yet)
- All existing TenantSettings: `CommerceModeOverride = -1 (Inherit)` — default value
- All existing ProductReferralConfig: `CommissionBase = 0 (OnOrderTotal)` — existing behavior

### 7.3 Rollout strategy

| Phase | Action | Risk |
|---|---|---|
| 1. Deploy Sprint 7 code | Toggle default OFF (Marketplace) | Zero — existing behavior unchanged |
| 2. Test Reseller mode on 1 tenant | Override 1 tenant → Reseller | Isolated — only that tenant's new orders |
| 3. Toggle global → Reseller | All tenants (except overrides) switch | Medium — monitor financial flows |
| 4. Full Reseller | All tenants Reseller | High — require full RV |

---

## 8. TDD Plan (18 Test Cases)

| # | Test Name | What It Verifies |
|---|---|---|
| 1 | `GetGlobalMode_Default_ReturnsMarketplace` | Default global = Marketplace |
| 2 | `SetGlobalMode_ChangesMode` | Toggle → new mode returned |
| 3 | `GetTenantMode_Inherit_ReturnsGlobal` | Inherit → fallback to global |
| 4 | `GetTenantMode_Override_ReturnsOverride` | Override → returns override, not global |
| 5 | `SetTenantOverride_Persists` | Override saved + retrieved |
| 6 | `ResolveModeForOrder_Inherit_UsesGlobal` | Order creation resolves mode correctly |
| 7 | `ResolveModeForOrder_Override_UsesTenant` | Order creation uses tenant override |
| 8 | `ConfirmCod_Marketplace_ExistingBehavior` | Marketplace: 2 tx (CODCollection + Settlement) |
| 9 | `ConfirmCod_Reseller_CreatesAllTransactions` | Reseller: 6 tx (COD + Settlement + DeliveryFee + Commission + PlatformFee + CommunityFund) |
| 10 | `ConfirmCod_Reseller_FinancialBalance` | Sum of all tx amounts = COD amount collected |
| 11 | `ConfirmCod_Reseller_NoSalesman_SkipsCommission` | No salesman → 5 tx (skip Commission) |
| 12 | `ConfirmAdvance_Marketplace_ShipperAdvances` | Marketplace: shipper -amount (existing) |
| 13 | `ConfirmAdvance_Reseller_VanAnAdvances` | Reseller: platform wallet -amount, tenant +amount |
| 14 | `CreateCommission_Marketplace_OnOrderTotal` | Marketplace: commission = orderTotal * rate |
| 15 | `CreateCommission_Reseller_OnMargin` | Reseller: commission = margin * rate |
| 16 | `Order_SnapshotsCommerceMode_AtCreation` | Order.CommerceMode set, not changed by later toggle |
| 17 | `Order_Marketplace_NullCostPrice` | Marketplace orders: CostPrice = null |
| 18 | `Order_Reseller_HasAllPricingFields` | Reseller orders: CostPrice, SellPrice, Margin, DeliveryFee all set |

---

## 9. Coding Plan — 3 Sessions

| Session | JIT Planning | Pure Execution |
|---|---|---|
| **S1** | Domain + Service + tests | CommerceMode enum + Order fields + SystemSetting entity + TenantSettings.CommerceModeOverride + ProductReferralConfig.CommissionBase + ICommerceModeService + CommerceModeService + 18 unit tests + EF configs + migration |
| **S2** | WalletService dual-mode + SalesmanService dual-mode | WalletService.ConfirmCodAsync (branch by mode) + WalletService.ConfirmAdvanceAsync (branch by mode) + SalesmanService.CreateCommissionAsync (branch by mode) + CommunityAdminController (3 endpoints) + DI registration |
| **S3** | UI + nav + integration tests | CommerceMode.razor (admin settings page) + KhachLink mode-aware UI (Wallet badge + DeliveryTracking advance button + NearbyProducts price) + ShopERP AdminLayout/NavMenu nav link + integration tests (auth guards + DI) + guard-check + build |

---

## 10. VPS Verification (Sprint 7)

| # | Test | Expected |
|---|---|---|
| RV7-1 | GET /api/admin/commerce-mode (no admin token) | 401 |
| RV7-2 | GET /api/admin/commerce-mode (admin token) | 200 + globalMode=Marketplace |
| RV7-3 | POST /api/admin/commerce-mode/global (admin) | 200 + updated=true |
| RV7-4 | POST /api/admin/commerce-mode/tenant/{id} (admin) | 200 + updated=true |
| RV7-5 | GET /api/community/commerce-mode (customer token) | 200 + mode=Marketplace |
| RV7-6 | Confirm COD Marketplace mode | 2 transactions (existing behavior) |
| RV7-7 | Confirm COD Reseller mode (after toggle) | 6 transactions (new behavior) |
| RV7-8 | Existing orders unaffected after toggle | Past orders still Marketplace |
| RV7-9 | Admin nav link to /admin/commerce-mode | Page accessible from menu |
| RV7-10 | guard-check + build | ALL PASSED |
| RV7-11 | Architecture tests | ALL PASS |
| RV7-12 | Regression Sprint 0-6 | All existing RV still PASS |

---

## 11. Constraints & Boundary Rules

1. **Additive only:** Không xóa/sửa existing WalletTransactionType values, không sửa existing Order fields semantics
2. **Snapshot at creation:** Order.CommerceMode set khi tạo order, KHÔNG thay đổi khi toggle sau đó
3. **Past orders immutable:** Toggle affect future orders only
4. **Marketplace = existing behavior:** Khi mode=Marketplace, tất cả logic giống Sprint 0-6 (no change)
5. **Reseller = new behavior:** Khi mode=Reseller, WalletService branch sang new code path
6. **SystemWalletIds:** 2 reserved GUID cho platform wallet + community fund — không tạo Customer entity cho chúng
7. **Financial balance invariant:** Reseller COD: tổng tất cả tx amounts = COD amount collected (verify in test #10)
8. **Domain Modification approval:** Cần approval per governance — 6 Order fields + 1 enum + 3 WalletTransactionType + 1 ProductReferralConfig field + 1 TenantSettings field + 1 SystemSetting entity
9. **UI Platform compliance:** Admin settings page dùng VanAnButton, VanAnCard, VanAnTable, VanAnBadge
10. **Auth:** Admin endpoints = SystemAdmin JWT. Customer endpoint = X-Customer-Token

---

## 12. Open Questions (RESOLVED 2026-07-30 in ANALYZE phase)

| # | Question | Resolution |
|---|---|---|
| Q1 | CostPrice lấy từ đâu? | **RESOLVED — Manual via Gateway admin API.** Van An admin sets CostPrice per product via `POST /api/admin/product-cost-price`. Stored in new `ProductCostPrice` table in PG. Tenant negotiates offline. NATS query = Sprint 8+ scale. |
| Q2 | Platform fee rate: global only hay per-tenant? | **RESOLVED — Global only.** One `DefaultPlatformFeeRate` in SystemSetting. Per-tenant = Sprint 8+ if demand. |
| Q3 | Community fund wallet: ai quản lý? | **RESOLVED — Included in Sprint 7 (full scope).** `POST /api/admin/community-fund/spend` + balance + history endpoints. `CommunityFundSpendRecord` audit entity. SysAdmin can disburse for community reinvestment. |
| Q4 | Reseller COD: shipper thu = SellPrice + DeliveryFee? | **RESOLVED — Yes, customer pays both.** COD amount = SellPrice + DeliveryFee. Shipper collects both; system splits into 5 tx. |
| Q5 | Reseller + non-COD payment (VietQR, credit card)? | **RESOLVED — Included in Sprint 7 (full scope).** `POST /api/community/wallet/confirm-external-payment`. Van An receives payment externally, confirms via endpoint → 5-split (no CODCollection tx). `ExternalPayment=10` enum value. |

---

## 13. Success Criteria

- [ ] **SC1:** CommerceMode enum + Order fields + SystemSetting entity + TenantSettings.CommerceModeOverride + ProductReferralConfig.CommissionBase added to Domain
- [ ] **SC2:** ICommerceModeService + CommerceModeService — get/set global + tenant override + resolve for order
- [ ] **SC3:** WalletService.ConfirmCodAsync dual-mode — Marketplace (2 tx, existing) + Reseller (6 tx, new)
- [ ] **SC4:** WalletService.ConfirmAdvanceAsync dual-mode — Marketplace (shipper ứng) + Reseller (Vạn An ứng)
- [ ] **SC5:** SalesmanService.CreateCommissionAsync dual-mode — OnOrderTotal (Marketplace) + OnMargin (Reseller)
- [ ] **SC6:** CommunityAdminController — 3 endpoints (GET global, POST global, POST tenant override)
- [ ] **SC7:** GET /api/community/commerce-mode — customer-facing mode query
- [ ] **SC8:** CommerceMode.razor admin page — toggle + rate settings + tenant overrides table
- [ ] **SC9:** KhachLink mode-aware UI — Wallet badge + DeliveryTracking advance button conditional + NearbyProducts price display
- [ ] **SC10:** ShopERP AdminLayout + NavMenu — nav link to /admin/commerce-mode
- [ ] **SC11:** 18 unit tests PASS
- [ ] **SC12:** `dotnet build` 0 errors + guard-check pass
- [ ] **SC13:** Architecture tests pass
- [ ] **SC14:** VPS RV7-1 to RV7-12 ALL PASS
- [ ] **SC15:** Regression Sprint 0-6 — all existing behavior unchanged when mode=Marketplace (default)
- [ ] **SC16:** Financial balance invariant — Reseller COD: sum(tx amounts) = COD collected (test #10)
- [ ] **SC17:** Order snapshot — past orders unaffected by toggle (test #16)
- [ ] **SC18:** Migration additive — existing data intact, no data loss

**Branch:** `feature/commerce-mode-toggle-sprint7`
