# Sprint 7 Detailed Plan — Commerce Mode Toggle (Marketplace ↔ Reseller)

**STATUS: DRAFT v2.1** | Spec: `commerce-mode-toggle-spec-v2-2c5017.md` | **~24 test cases, 4 sessions, dual-mode financial flows + community fund management + non-COD Reseller.**

> **Revision history**
> - v2.1 — 2026-07-30 — ANALYZE phase complete. Fixed spec errors (WalletTransactionType has 6 existing values, not 8). Added full scope per user decision: Q3 (community fund management) + Q5 (non-COD Reseller) included. Resolved Q1-Q5. Added ProductCostPrice + CommunityFundSpendRecord entities. Enum values renumbered 7-11.
> - v2.0 — 2026-07-30 — Initial draft. 18 test cases, 3 sessions, COD-only scope.

---

## 1. DOMAIN CHANGES (Additive — cần Domain Modification approval)

> **APPROVED 2026-07-30** per ANALYZE phase. Full scope (Q3+Q5 included).

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

### 1.4 WalletTransactionType (5 new enum values)
```csharp
// Existing (verified 2026-07-30): 1=CODCollection, 2=AdvancePayment, 3=Commission, 4=Withdrawal, 5=Settlement, 6=Reversal
// NOTE: Spec v2.0 incorrectly claimed 8 existing values. Actual = 6. Deposit=7/SmsOtpFee=8 belong to CC-S6-T5 (not yet implemented).
- PlatformFee = 7        // Reseller: Vạn An giữ margin
- CommunityFund = 8      // Reseller: quỹ phát triển cộng đồng
- DeliveryFee = 9        // Reseller: phí giao shipper (tách khỏi COD)
- ExternalPayment = 10   // Q5 — non-COD Reseller: customer trả Vạn An qua VietQR/card
- CommunityFundSpend = 11 // Q3 — community fund disbursement (SysAdmin rút tiền tái đầu tư)
```
**Coordination note:** If CC-S6-T5 (SMS OTP toggle) lands first, it takes 7=Deposit, 8=SmsOtpFee, and Sprint 7 renumbers to 9-13.

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
// Keys: GlobalCommerceMode, DefaultPlatformFeeRate, DefaultCommunityFundRate, DefaultDeliveryFee
```

### 1.8 ProductCostPrice entity (NEW — Q1 resolution)
```csharp
ProductCostPrice : BaseEntity, IMustHaveTenant
- ProductId (Guid) — FK to Product (in ShopERP SQLite, but this entity lives in Gateway PG)
- TenantId (Guid) — which tenant this cost price applies to
- CostPrice (decimal) — giá Vạn An mua từ tenant (negotiated offline)
- UpdatedAt (DateTime?)
- UpdatedBy (Guid?)
// Unique index on (TenantId, ProductId)
// Van An admin sets via Gateway admin API. Tenant negotiates offline.
```

### 1.9 CommunityFundSpendRecord entity (NEW — Q3 resolution)
```csharp
CommunityFundSpendRecord : BaseEntity, IMustHaveTenant (TenantId = Guid.Empty — system-wide)
- Amount (decimal) — số tiền rút từ quỹ
- Reason (string, 500 chars) — lý do chi (vd: "Tài trợ sự kiện cộng đồng X")
- Recipient (string, 200 chars) — người nhận / đối tác
- ApprovedBy (Guid) — SystemAdmin userId
- SpentAt (DateTime)
- WalletTransactionId (Guid) — FK to WalletTransaction (CommunityFundSpend tx)
```

### 1.10 SystemWalletIds (NEW static class)
```csharp
public static class SystemWalletIds
{
    public static readonly Guid PlatformWallet = Guid.Parse("00000000-0000-0000-0000-000000000001");
    public static readonly Guid CommunityFund = Guid.Parse("00000000-0000-0000-0000-000000000002");
}
// Location: 1_Shared/Domain/Common/SystemWalletIds.cs (Domain constant — business concept)
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

### 2.4 Non-COD Reseller Flow (Q5 — VietQR / Credit Card)

```
Reseller + non-COD (customer trả Vạn An trực tiếp qua VietQR/card):
  1. Customer pays via VietQR/card → Vạn An nhận tiền vào tài khoản ngân hàng (ngoài wallet)
  2. Van An admin (or auto-webhook) xác nhận đã nhận → POST /api/community/wallet/confirm-external-payment
  3. System creates:
     a. ExternalPayment  +amount        (PlatformWallet) — Vạn An nhận tiền customer
     b. Settlement       +costPrice     (tenant)          — Vạn An trả giá vốn
     c. DeliveryFee      +deliveryFee   (shipper)         — Vạn An trả phí giao
     d. Commission       +commission    (salesman)        — Vạn An trả hoa hồng (if referral)
     e. PlatformFee      +platformFee   (PlatformWallet)  — Vạn An giữ margin
     f. CommunityFund    +communityFund (CommunityFund)   — quỹ cộng đồng
  5 transactions (4 if no salesman). No CODCollection tx (customer paid externally).

  Invariant: costPrice + deliveryFee + commission + platformFee + communityFund = externalPaymentAmount
```

### 2.5 Community Fund Management Flow (Q3)

```
Collection (automatic — happens in COD/Non-COD Reseller flow):
  CommunityFund tx (+communityFund) → CommunityFund wallet balance increases.

Disbursement (SysAdmin manual — NEW):
  1. SysAdmin submits spend request: POST /api/admin/community-fund/spend
     Body: { amount, reason, recipient }
  2. System creates:
     a. CommunityFundSpend  -amount  (CommunityFund wallet) — rút tiền
     b. CommunityFundSpendRecord entity — audit trail (amount, reason, recipient, approvedBy, spentAt, walletTxId)
  3. GET /api/admin/community-fund/balance → current balance
  4. GET /api/admin/community-fund/history → paginated spend records

  Auth: SystemAdmin JWT (same as commerce-mode endpoints).
  Balance check: reject if amount > current CommunityFund balance.
```

---

## 3. TDD PLAN (~24 TEST CASES)

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
| 19 | `ConfirmExternalPayment_Reseller_CreatesAllTransactions` | Q5 — non-COD: 5 tx (no CODCollection) |
| 20 | `ConfirmExternalPayment_Reseller_FinancialBalance` | Q5 — sum tx = external payment amount |
| 21 | `ConfirmExternalPayment_Marketplace_Rejected` | Q5 — non-COD endpoint rejects Marketplace orders |
| 22 | `CommunityFundSpend_InsufficientBalance_Rejected` | Q3 — reject if amount > balance |
| 23 | `CommunityFundSpend_Valid_CreatesTxAndRecord` | Q3 — spend creates CommunityFundSpend tx + audit record |
| 24 | `CommunityFundSpend_History_Paginated` | Q3 — history returns spend records |

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

### 4.6 NEW: POST /api/community/wallet/confirm-external-payment (Q5 — CustomerToken or SystemAdmin)
```
Body: { "orderId": "guid", "amount": 50000, "paymentRef": "VietQR_TX123" }
Reseller only — rejects Marketplace orders with 400.
Creates 5 transactions (ExternalPayment + Settlement + DeliveryFee + Commission? + PlatformFee + CommunityFund).
Response 200: { "transactionId": "guid", "mode": "Reseller", "transactionsCreated": 5, "breakdown": {...} }
```

### 4.7 NEW: GET /api/admin/community-fund/balance (Q3 — SystemAdmin)
```
Response 200: { "balance": 1500000, "totalCollected": 2000000, "totalSpent": 500000 }
```

### 4.8 NEW: POST /api/admin/community-fund/spend (Q3 — SystemAdmin)
```
Body: { "amount": 500000, "reason": "Tài trợ sự kiện cộng đồng X", "recipient": "Ban tổ chức X" }
Response 200: { "transactionId": "guid", "spendRecordId": "guid", "balanceAfter": 1000000 }
Response 400: { "error": "Số dư quỹ không đủ." }
Side effects: CommunityFundSpend tx (-amount on CommunityFund wallet) + CommunityFundSpendRecord audit entity.
```

### 4.9 NEW: GET /api/admin/community-fund/history (Q3 — SystemAdmin)
```
Query: ?page=1&pageSize=20
Response 200: { "items": [...], "total": 5, "page": 1 }
```

### 4.10 NEW: GET/POST /api/admin/product-cost-price (Q1 — SystemAdmin)
```
GET /api/admin/product-cost-price/{tenantId}/{productId} → { "costPrice": 80000, "updatedAt": "..." }
POST /api/admin/product-cost-price → Body: { "tenantId": "guid", "productId": "guid", "costPrice": 80000 }
Response 200: { "updated": true }
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

### 5.2 CommunityFund.razor (ShopERP admin — NEW, Q3)
```
@page "/admin/community-fund"
@attribute [Authorize(Roles="SystemAdmin")]
- Balance card: current balance + total collected + total spent
- Spend form: amount, reason, recipient → POST /api/admin/community-fund/spend
- History table: paginated spend records (date, amount, reason, recipient, approvedBy)
- Nav link: AdminLayout + NavMenu SystemAdmin section
```

### 5.3 ProductCostPrice.razor (ShopERP admin — NEW, Q1)
```
@page "/admin/product-cost-price"
@attribute [Authorize(Roles="SystemAdmin")]
- Search: tenant dropdown + product search
- Set CostPrice form: productId, costPrice → POST /api/admin/product-cost-price
- Table: existing cost prices (tenant, product, costPrice, updatedAt)
- Nav link: AdminLayout + NavMenu SystemAdmin section
```

### 5.4 KhachLink mode-aware UI
```
// Wallet.razor — mode badge (Marketplace / Reseller)
// DeliveryTracking.razor — advance button only if Marketplace (hide in Reseller — Vạn An ứng, not shipper)
// NearbyProducts.razor — price display: SellPrice (Reseller) vs tenant price (Marketplace)
```

---

## 6. CODING PLAN — 4 SESSIONS

| Session | JIT Planning | Pure Execution |
|---|---|---|
| **S1** | Domain + Service + tests | CommerceMode enum + CommissionBase enum + Order 7 fields + SystemSetting entity + ProductCostPrice entity + CommunityFundSpendRecord entity + SystemWalletIds + TenantSettings.CommerceModeOverride + ProductReferralConfig.CommissionBase + ICommerceModeService + CommerceModeService + ~24 unit tests + EF configs + migration |
| **S2** | WalletService + SalesmanService dual-mode + Controller | WalletService.ConfirmCodAsync (branch) + ConfirmAdvanceAsync (branch) + ConfirmExternalPaymentAsync (Q5) + SpendCommunityFundAsync (Q3) + SalesmanService.CreateCommissionAsync (branch) + CommunityAdminController (commerce-mode endpoints) + CommunityFundController (Q3) + ProductCostPriceController (Q1) + DI |
| **S3** | UI + nav | CommerceMode.razor + CommunityFund.razor (Q3) + ProductCostPrice.razor (Q1) + KhachLink mode-aware UI + ShopERP AdminLayout/NavMenu nav + CommunityHttpService.GetCommerceModeAsync |
| **S4** | Integration + verification | Integration tests + E2E specs (community-fund-management, reseller-non-cod) + guard-check + build + architecture tests + VPS RV |

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
| RV7-13 | Q3 — GET /api/admin/community-fund/balance (admin) | 200 + balance |
| RV7-14 | Q3 — POST /api/admin/community-fund/spend (admin) | 200 + spendRecordId |
| RV7-15 | Q3 — GET /api/admin/community-fund/history (admin) | 200 + paginated |
| RV7-16 | Q5 — POST /api/community/wallet/confirm-external-payment (Reseller) | 200 + 5 tx |
| RV7-17 | Q5 — confirm-external-payment rejects Marketplace order | 400 |
| RV7-18 | Q1 — POST /api/admin/product-cost-price (admin) | 200 |

---

## 8. OPEN QUESTIONS (RESOLVED 2026-07-30 in ANALYZE phase)

| # | Question | Resolution | Decision |
|---|---|---|---|
| Q1 | CostPrice source: tenant manual input vs NATS query? | **Manual via Gateway admin API** — Van An admin sets CostPrice per product via `POST /api/admin/product-cost-price`. Stored in new `ProductCostPrice` table in PG. Tenant negotiates offline. NATS query = Sprint 8+ scale. | Approved |
| Q2 | Platform fee rate: global only vs per-tenant? | **Global only** — one `DefaultPlatformFeeRate` in SystemSetting. All tenants use same rate. Per-tenant = Sprint 8+ if demand. | Approved |
| Q3 | Community fund management (withdraw + spend)? | **Included in Sprint 7 (full scope)** — `POST /api/admin/community-fund/spend` + balance + history endpoints. `CommunityFundSpendRecord` audit entity. SysAdmin can disburse for community reinvestment. | Approved (full scope) |
| Q4 | Reseller COD: shipper thu = SellPrice + DeliveryFee? | **Yes — customer pays both.** COD amount = SellPrice + DeliveryFee. Shipper collects both; system splits into 5 tx. | Approved |
| Q5 | Reseller + non-COD (VietQR): flow? | **Included in Sprint 7 (full scope)** — `POST /api/community/wallet/confirm-external-payment`. Van An receives payment externally, confirms via endpoint → 5-split (no CODCollection tx). `ExternalPayment=10` enum value. | Approved (full scope) |

---

## 9. SPEC CORRECTIONS (v2.0 → v2.1)

| # | v2.0 error | v2.1 fix |
|---|---|---|
| 1 | Claimed `WalletTransactionType` has 8 existing values (CODCollection=1 through SmsOtpFee=8) | Actual = 6 values (1-6). Deposit=7/SmsOtpFee=8 belong to CC-S6-T5 (not yet implemented). Sprint 7 enum values renumbered: PlatformFee=7, CommunityFund=8, DeliveryFee=9, ExternalPayment=10, CommunityFundSpend=11. |
| 2 | Proposed `Order.DeliveryFee` without acknowledging existing `Order.ShippingFee` (line 1498) | `DeliveryFee` added as distinct 7th Order field (per user decision D1). `ShippingFee` = tenant-set delivery cost (Marketplace); `DeliveryFee` = VanAn's fee paid to shipper (Reseller). Different semantics, separate columns. |
