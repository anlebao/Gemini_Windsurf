# Sprint 7 Detailed Plan — Commerce Mode Toggle (Marketplace ↔ Reseller)

**STATUS: DRAFT** | Spec: `commerce-mode-toggle-spec-v2-2c5017.md` | 18 test cases, 3 sessions, dual-mode financial flows.

---

## 1. DOMAIN CHANGES (Additive — cần Domain Modification approval)

### 1.1 CommerceMode enum (NEW)
```csharp
public enum CommerceMode
{
    Marketplace = 0,   // Sprint 0-6 hiện tại
    Reseller = 1,      // Sprint 7 — Vạn An mua-bán lại
    Inherit = -1       // Tenant override: dùng global
}
```

### 1.2 CommissionBase enum (NEW)
```csharp
public enum CommissionBase
{
    OnOrderTotal = 0,  // Marketplace: commission = orderTotal * rate
    OnMargin = 1       // Reseller: commission = margin * rate
}
```

### 1.3 Order fields (7 new — all nullable except CommerceMode)
```csharp
Order (existing, add):
- CommerceMode (CommerceMode, NOT NULL, default Marketplace) — snapshot tại creation
- CostPrice (decimal?) — Reseller: giá mua từ tenant
- SellPrice (decimal?) — Reseller: giá bán cho customer
- PlatformMargin (decimal?) — Reseller: SellPrice - CostPrice
- DeliveryFee (decimal?) — Reseller: phí giao hàng
- PlatformFeeRate (decimal?) — Reseller: % margin Vạn An giữ
- CommunityFundRate (decimal?) — Reseller: % margin quỹ cộng đồng
```

### 1.4 WalletTransactionType (3 new enum values)
```csharp
- PlatformFee = 9       // Reseller: Vạn An giữ margin
- CommunityFund = 10    // Reseller: quỹ phát triển
- DeliveryFee = 11      // Reseller: phí giao shipper (tách khỏi COD)
```

### 1.5 ProductReferralConfig (1 new field)
```csharp
- CommissionBase (CommissionBase, default OnOrderTotal)
```

### 1.6 TenantSettings (1 new field + With method)
```csharp
- CommerceModeOverride (CommerceMode, default Inherit)
- WithCommerceModeOverride(CommerceMode mode) — immutable update
```

### 1.7 SystemSetting entity (NEW)
```csharp
SystemSetting : BaseEntity, IMustHaveTenant (TenantId nullable — global settings)
- Key (string, unique, 100 chars)
- Value (string, 500 chars)
- UpdatedAt (DateTime?)
- UpdatedBy (Guid?)
```

### 1.8 SystemWalletIds (NEW static class)
```csharp
public static class SystemWalletIds
{
    public static readonly Guid PlatformWallet = Guid.Parse("00000000-0000-0000-0000-000000000001");
    public static readonly Guid CommunityFund = Guid.Parse("00000000-0000-0000-0000-000000000002");
}
```

---

## 2. FINANCIAL FLOW COMPARISON

### 2.1 COD Flow

```
Marketplace (existing — Sprint 5):
  Shipper thu COD → +amount (shipper), -amount (shop)
  2 transactions. Vạn An không tham gia.

Reseller (new — Sprint 7):
  Shipper thu COD → Vạn An phân phối:
  1. CODCollection  +codAmount    (shipper) — thu hộ
  2. Settlement     +costPrice    (tenant)  — Vạn An trả giá vốn
  3. DeliveryFee    +deliveryFee  (shipper) — Vạn An trả phí giao
  4. Commission     +commission   (salesman)— Vạn An trả hoa hồng (if referral)
  5. PlatformFee    +platformFee  (Vạn An)  — Vạn An giữ margin
  6. CommunityFund  +communityFund(quỹ)     — quỹ cộng đồng
  6 transactions (5 if no salesman).

  Invariant: costPrice + deliveryFee + commission + platformFee + communityFund = codAmount
```

### 2.2 Advance Payment Flow

```
Marketplace (existing):
  Shipper ứng → -amount (shipper), +amount (shop on confirm)
  Shipper là bên ứng.

Reseller (new):
  Vạn An ứng → -amount (PlatformWallet), +amount (tenant)
  Vạn An là bên ứng. Shipper không tham gia tài chính.
```

### 2.3 Commission Calculation

```
Marketplace:  Commission = orderTotal * CommissionRate
Reseller:     Commission = PlatformMargin * CommissionRate

Code:
  decimal base = order.CommerceMode == Reseller
      ? order.PlatformMargin ?? 0
      : order.TotalAmount;
  decimal commission = base * config.CommissionRate;
```

---

## 3. TDD PLAN (18 TEST CASES)

| # | Test Name | What It Verifies |
|---|---|---|
| 1 | `GetGlobalMode_Default_ReturnsMarketplace` | Default global = Marketplace |
| 2 | `SetGlobalMode_ChangesMode` | Toggle → new mode returned |
| 3 | `GetTenantMode_Inherit_ReturnsGlobal` | Inherit → fallback to global |
| 4 | `GetTenantMode_Override_ReturnsOverride` | Override → returns override |
| 5 | `SetTenantOverride_Persists` | Override saved + retrieved |
| 6 | `ResolveModeForOrder_Inherit_UsesGlobal` | Order creation resolves correctly |
| 7 | `ResolveModeForOrder_Override_UsesTenant` | Order uses tenant override |
| 8 | `ConfirmCod_Marketplace_ExistingBehavior` | Marketplace: 2 tx (existing) |
| 9 | `ConfirmCod_Reseller_CreatesAllTransactions` | Reseller: 6 tx (new) |
| 10 | `ConfirmCod_Reseller_FinancialBalance` | Sum tx amounts = COD collected |
| 11 | `ConfirmCod_Reseller_NoSalesman_SkipsCommission` | No salesman → 5 tx |
| 12 | `ConfirmAdvance_Marketplace_ShipperAdvances` | Marketplace: shipper -amount |
| 13 | `ConfirmAdvance_Reseller_VanAnAdvances` | Reseller: platform -amount, tenant +amount |
| 14 | `CreateCommission_Marketplace_OnOrderTotal` | Marketplace: orderTotal * rate |
| 15 | `CreateCommission_Reseller_OnMargin` | Reseller: margin * rate |
| 16 | `Order_SnapshotsCommerceMode_AtCreation` | Mode set, not changed by later toggle |
| 17 | `Order_Marketplace_NullCostPrice` | Marketplace: CostPrice = null |
| 18 | `Order_Reseller_HasAllPricingFields` | Reseller: all pricing fields set |

---

## 4. API SPECIFICATIONS

### 4.1 GET /api/admin/commerce-mode (SystemAdmin)
```
Response 200: {
  "globalMode": "Marketplace",
  "defaultPlatformFeeRate": 0.30,
  "defaultCommunityFundRate": 0.05,
  "defaultDeliveryFee": 15000,
  "tenantOverrides": [
    { "tenantId": "guid", "tenantName": "Shop A", "override": "Reseller" }
  ]
}
```

### 4.2 POST /api/admin/commerce-mode/global (SystemAdmin)
```
Body: { "mode": "Reseller", "platformFeeRate": 0.30, "communityFundRate": 0.05, "deliveryFee": 15000 }
Response 200: { "updated": true }
```

### 4.3 POST /api/admin/commerce-mode/tenant/{tenantId} (SystemAdmin)
```
Body: { "override": "Reseller" }
Response 200: { "updated": true }
```

### 4.4 GET /api/community/commerce-mode (CustomerToken)
```
Response 200: { "mode": "Marketplace", "isReseller": false }
```

### 4.5 Modified: POST /api/community/wallet/confirm-cod (dual-mode)
```
Marketplace: 2 transactions (existing)
Reseller: 6 transactions + breakdown object
```

---

## 5. UI SPECS

### 5.1 CommerceMode.razor (ShopERP admin — NEW)
```
@page "/admin/commerce-mode"
@attribute [Authorize(Roles="SystemAdmin")]
- Global settings card:
  - Mode toggle: Marketplace / Reseller (radio)
  - Platform Fee Rate: % slider (10-50%)
  - Community Fund Rate: % slider (1-10%)
  - Default Delivery Fee: number (VND)
  - "Lưu" button
  - Warning: "Áp dụng cho đơn hàng mới. Đơn cũ không bị ảnh hưởng."
- Tenant overrides table:
  - Columns: Tenant, Resolved Mode, Override dropdown, Save button
- Nav link: AdminLayout + NavMenu Community section
```

### 5.2 KhachLink mode-aware UI
```
// Wallet.razor — mode badge
// DeliveryTracking.razor — advance button only if Marketplace
// NearbyProducts.razor — price display: SellPrice (Reseller) vs tenant price (Marketplace)
```

---

## 6. CODING PLAN — 3 SESSIONS

| Session | JIT Planning | Pure Execution |
|---|---|---|
| **S1** | Domain + Service + tests | CommerceMode enum + CommissionBase enum + Order 7 fields + SystemSetting entity + SystemWalletIds + TenantSettings.CommerceModeOverride + ProductReferralConfig.CommissionBase + ICommerceModeService + CommerceModeService + 18 unit tests + EF configs + migration |
| **S2** | WalletService + SalesmanService dual-mode + Controller | WalletService.ConfirmCodAsync (branch) + ConfirmAdvanceAsync (branch) + SalesmanService.CreateCommissionAsync (branch) + CommunityAdminController (4 endpoints) + DI |
| **S3** | UI + nav + integration tests | CommerceMode.razor + KhachLink mode-aware UI + ShopERP AdminLayout/NavMenu nav + CommunityHttpService.GetCommerceModeAsync + integration tests + guard-check + build |

---

## 7. VPS VERIFICATION (Sprint 7)

| # | Test | Expected |
|---|---|---|
| RV7-1 | GET /api/admin/commerce-mode (no admin token) | 401 |
| RV7-2 | GET /api/admin/commerce-mode (admin token) | 200 + globalMode=Marketplace |
| RV7-3 | POST /api/admin/commerce-mode/global (admin) | 200 |
| RV7-4 | POST /api/admin/commerce-mode/tenant/{id} (admin) | 200 |
| RV7-5 | GET /api/community/commerce-mode (customer token) | 200 + mode |
| RV7-6 | Confirm COD Marketplace | 2 transactions (existing) |
| RV7-7 | Confirm COD Reseller (after toggle) | 6 transactions (new) |
| RV7-8 | Existing orders unaffected after toggle | Past orders still Marketplace |
| RV7-9 | Admin nav link /admin/commerce-mode | Page accessible |
| RV7-10 | guard-check + build | ALL PASSED |
| RV7-11 | Architecture tests | ALL PASS |
| RV7-12 | Regression Sprint 0-6 | All existing RV PASS |

---

## 8. OPEN QUESTIONS (resolve in ANALYZE phase before IMPLEMENT)

| # | Question | Default | Impact if unresolved |
|---|---|---|---|
| Q1 | CostPrice source: tenant manual input vs NATS query? | Manual (PoC) | Blocks Order creation in Reseller mode |
| Q2 | Platform fee rate: global only vs per-tenant? | Global only | If per-tenant needed → TenantSettings +2 fields |
| Q3 | Community fund management (withdraw + spend)? | Defer Sprint 8+ | No UC needed for Sprint 7 — just collect |
| Q4 | Reseller COD: shipper thu = SellPrice + DeliveryFee? | Yes (customer trả cả hàng + ship) | Affects COD amount calculation |
| Q5 | Reseller + non-COD (VietQR): flow? | Defer — spec COD only for Sprint 7 | Non-COD Reseller needs separate spec |
