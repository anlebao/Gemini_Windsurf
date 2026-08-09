# VẠN AN LOCAL COMMERCE NETWORK
# BUSINESS OPERATING MODEL v2.0 — PLATFORM-LIGHT

> **Phiên bản:** 2.0 (PLATFORM-LIGHT) — sửa đổi từ v1.0 (RETAILER-HEAVY)
> **Ngày:** 2026-08-09
> **Mục đích:** Khớp codebase hiện tại tối đa, đơn giản vận hành, phù hợp giai đoạn chứng minh hiệu quả thay vì đốt tiền mua user.
> **Supersedes:** `VẠN AN LOCAL COMMERCE NETWORK (1).md` v1.0 (chỉ thay thế phần mô hình cốt lõi; các nguyên tắc kinh tế v1.0 vẫn giữ nếu không ghi đè).

---

## 0. Vì sao v1.0 phải sửa

v1.0 chọn mô hình **Retailer** (Vạn An BUY → OPERATE → SELL → REWARD, gánh 100% inventory). Phản biện phát hiện 3 lỗ hổng gốc:

1. **Retailer không có network effect** — flywheel Section 53 là tautology, không có cơ chế tự gia tốc.
2. **Capital intensity quá cao cho giai đoạn chứng minh** — gánh inventory + expiry + loyalty liability trên balance sheet SME.
3. **Khớp 0% với codebase** — code hiện là Marketplace (Tenant = seller), flip sang Reseller mặc định = pivot toàn bộ domain + accounting.

v2.0 chọn mô hình **Platform-Light**: Tenant = Seller of Record, Vạn An = Orchestrator + Loyalty Issuer + Community Engine. **Khớp ~95% codebase**, chỉ cần **additive fields**, không phá architecture.

---

## 1. Core Pivot — Một câu

> **v1.0:** Vạn An là nhà bán lẻ, gánh hàng, ghi nhận revenue.
> **v2.0:** Tenant là nhà bán lẻ, tự gánh hàng, tự ghi nhận revenue. Vạn An là **platform operator** thu take-rate + phát hành loyalty + điều phối community commerce.

Mọi nguyên tắc kinh tế v1.0 (unit economics, no deposit, reward reversal, loyalty budget) **giữ nguyên**. Chỉ **thay chủ thể gánh rủi ro** từ Vạn An sang Tenant — đúng với thực tế codebase và đúng với định nghĩa platform.

---

## 2. Operating Model at a Glance (v2.0)

```text
                    LOCAL TENANTS (SHOPS)
                          │
                          │ Sell directly (DROP-SHIP default)
                          ▼
                 ┌──────────────────┐
                 │      VẠN AN      │
                 │  PLATFORM LAYER  │
                 │                  │
                 │  ORCHESTRATE     │
                 │  → REWARD        │
                 │  → REFER         │
                 │  → SETTLE        │
                 └────────┬─────────┘
                          │
              ┌───────────┼───────────┐
              ▼           ▼           ▼
         CUSTOMERS    SHIPPERS    COMMUNITY
              │       (Delivery)   SELLERS
              │                      │
              ▼                      │
           GMV ──────────────────────┘
              │
              ▼
         TAKE-RATE → VAN AN REVENUE
         (không gánh COGS, không gánh inventory)
```

**Khác biệt cốt lõi vs v1.0:**
- Vạn An **không BUY**, không có COGS, không có inventory risk (mặc định).
- Vạn An revenue = **Platform Fee % GMV** (take-rate), không phải Gross Margin.
- Tenant tự ghi nhận Revenue/COGS trong sổ kế toán per-tenant (đã có sẵn).
- Loyalty liability vẫn trên Vạn An (đúng với codebase `LoyaltyRewards` + `AllianceWallet`).

---

## 3. The Four Core Economic Relationships (v2.0)

### Relationship A — Tenant ↔ Supplier (KHÔNG phải Vạn An ↔ Supplier)

```text
Supplier
   ↓
Product / Service
   ↓
Tenant Purchase   ← Tenant tự gánh, Vạn An không can thiệp
```

Đây là **procurement transaction của Tenant**, không phải của Vạn An. Vạn An không cần entity `Supplier`, không cần `PurchaseOrder`. **Tiết kiệm 4 entities mới** so với v1.0.

> **Codebase match:** `Product.CostPrice` đã có (DMD-2 fix). Tenant tự nhập cost price khi onboard product. Đủ cho unit economics calculation mà không cần procurement flow.

### Relationship B — Tenant → Customer (Vạn An orchestrate)

```text
Tenant (Seller of Record)
   ↓
Product / Service
   ↓
Customer          ← Vạn An điều phối qua KhachLink + Gateway
```

Vạn An **không phải seller**, là **channel + loyalty issuer**. Order ghi `SellerOfRecord = TenantId` (đã có qua `Order.TenantId`).

> **Codebase match:** `CommerceMode.Marketplace` = default. `Order.TenantId` = seller. 0 code change.

### Relationship C — Vạn An → Shipper (Community Delivery)

```text
Vạn An
   ↓
Delivery Order
   ↓
Shipper (Community Seller role)
```

Vạn An điều phối delivery, **không gánh delivery cost mặc định** — customer trả `ShippingFee` (đã có `Order.ShippingFee`). Free-delivery promotion = optional, funded by campaign budget.

> **Codebase match:** `DeliveryTask`, `DeliveryTracking`, `CommunityRole` (Delivery Partner) đã có. Community Commerce Sprint 2 COMPLETE.

### Relationship D — Vạn An → Customer Loyalty

```text
Vạn An
   ↓
Loyalty Program
   ↓
Customer Reward
```

**Giữ nguyên v1.0**. Vạn An là loyalty issuer, gánh loyalty liability. Đây là **moat duy nhất** Vạn An xây — không gánh inventory nhưng gánh loyalty, đúng với năng lực công ty phần mềm.

> **Codebase match:** `LoyaltyRewards`, `LoyaltyGlobalConfig`, `LoyaltyTenantConfig`, `AllianceWallet`, `AllianceTransaction`, `RedemptionRecord`, `Voucher` — đầy đủ. Loyalty Alliance 7 phases COMPLETE.

---

## 4. Procurement Modes (v2.0 — đơn giản hóa)

v1.0 có 3 modes: OUTRIGHT / ON_DEMAND / CONTROLLED_CONSIGNMENT — **tất cả đều capital-heavy cho Vạn An**.

v2.0 có 2 modes, **Tenant là chủ thể**, không phải Vạn An:

### Mode 1 — DROP-SHIP (mặc định, capital-light)

```text
Customer → Order → Tenant → Prepare → Shipper → Customer
```

- Tenant tự gánh hàng, tự chuẩn bị, tự chịu expiry/shrinkage.
- Vạn An không chạm hàng, không gánh inventory risk.
- **Đây là mode mặc định toàn network.**
- Phù hợp: F&B, grocery, mỹ phẩm, mọi category ở giai đoạn chứng minh.

> **Codebase match:** `CommerceMode.Marketplace` = DROP-SHIP. 0 code change.

### Mode 2 — RESELLER (tùy chọn per-SKU/per-Order, strategic only)

```text
Customer → Order → Vạn An (buy from Tenant) → Sell to Customer
```

- Vạn An gánh inventory risk **chỉ khi chọn** — cho category chiến lược có margin cao.
- **Không phải mặc định.** Toggle per-order qua `CommerceMode.Reseller`.
- Chỉ dùng khi: (a) margin > 30%, (b) volume đủ lớn để bù working capital, (c) có hợp đồng supplier độc quyền.

> **Codebase match:** `CommerceMode.Reseller` + `Order.CostPrice/SellPrice/PlatformMargin/DeliveryFee` đã có. Sprint 7 COMPLETE. 0 code change.

**Bỏ ON_DEMAND và CONTROLLED_CONSIGNMENT** — ON_DEMAND là subset của DROP-SHIP (tenant prepare on order), CONSIGNMENT phức tạp kế toán không đáng cho MVP.

---

## 5. Money Flow (v2.0)

### 5.1 Standard Drop-Ship Flow

```text
Customer pays Vạn An (via Gateway)
   │
   ├── ShippingFee → Shipper (hoặc giữ lại nếu self-deliver)
   ├── Platform Fee (take-rate %) → Vạn An REVENUE
   └── Remainder → Tenant (settlement)
```

Tenant ghi nhận:
```text
Revenue (full order value)
- COGS (tenant's own cost)
- Platform Fee paid to Vạn An
= Tenant Gross Margin
```

Vạn An ghi nhận:
```text
Platform Fee = Take-Rate % × GMV
- Loyalty Cost
- Delivery Subsidy (if free-delivery campaign)
- Ops Cost
= Vạn An Contribution Margin
```

> **Codebase match:** `Order.PlatformFeeRate` (snapshot tại creation) + `Order.PlatformMargin` đã có trong Reseller mode. Cần **additive field** `Order.PlatformFeeAmount` cho Marketplace mode (hoặc tính runtime = `TotalAmount × PlatformFeeRate`). 1 field additive.

### 5.2 Reseller Flow (optional)

```text
Customer pays Vạn An
   │
   ├── COGS → Tenant (Vạn An mua giá CostPrice)
   ├── DeliveryFee → Shipper
   └── SellPrice - CostPrice - DeliveryFee = Vạn An Gross Margin
```

> **Codebase match:** Đã có đầy đủ `CostPrice/SellPrice/PlatformMargin/DeliveryFee`. 0 code change.

---

## 6. No Merchant Deposit Rule (BOM-002 — giữ nguyên v1.0)

VALCN không yêu cầu tenant deposit tiền để tạo points / back points / fund tenant khác.

Tenant tham gia = **onboard product + chấp nhận take-rate**. Skin-in-the-game = **tenant tự gánh inventory** (DROP-SHIP) — đây là ràng buộc đủ mạnh, không cần deposit.

> **Phản biện v1.0 đã giải quyết:** v1.0 gánh inventory + no deposit = merchant 0% risk. v2.0 merchant gánh inventory + no deposit = merchant có skin-in-the-game (hàng của mình), Vạn An asset-light. Win-win.

---

## 7. Product Onboarding (v2.0 — đơn giản hóa)

```text
Tenant Self-Register
      ↓
Product Onboard (tenant nhập CostPrice + Price + Category)
      ↓
Vạn An Compliance Check (auto: VAT rate, category tag)
      ↓
Loyalty Config (reward rate per category)
      ↓
Published to KhachLink
```

**Bỏ "Merchant Approval" và "Product Commercial Gate" cứng** của v1.0 — quá nặng cho giai đoạn chứng minh. Thay bằng **soft gate**: dashboard flag SKUs có margin âm, SystemAdmin review tuần.

> **Codebase match:** `Product` đã có `CostPrice`, `Price`, `Category`, `VatRate`, `IsActive`. Tenant tự quản lý product qua ShopERP. 0 code change. Chỉ cần **reporting query** flag `Price - CostPrice < 0`.

---

## 8. Pricing Architecture (v2.0)

```text
Tenant sets:
  CostPrice (tenant's procurement cost)
  Price (selling price to customer)
  → Tenant Margin = Price - CostPrice

Vạn An sets:
  PlatformFeeRate (% of Price, default 5-10%)
  → Vạn An Revenue = Price × PlatformFeeRate

Customer pays:
  Price + VAT + ShippingFee - DiscountAmount
```

**Không có "Minimum Sustainable Price" cứng** của v1.0 — tenant tự quyết giá, Vạn An chỉ **monitor** margin qua dashboard. Nếu tenant bán lỗ chronic → flag + offboard (Section 27).

> **Codebase match:** `Product.Price` + `Product.CostPrice` đã có. `Order.PlatformFeeRate` đã có. Cần `TenantSettings.PlatformFeeRate` (additive, default 5%) — 1 field.

---

## 9. Promotion & Funding Source (v2.0 — additive)

Mỗi promotion phải có **Funding Source** (giữ v1.0 Section 16):

```text
A. Tenant-funded     — tenant giảm từ margin của mình (discount on product)
B. Vạn An-funded     — Vạn An giảm từ platform fee / marketing budget
C. Shared-funded     — Vạn An + Tenant cùng tài trợ
D. Loyalty-funded    — reward được tài trợ từ Loyalty Budget
```

> **Codebase change:** Add `PromoCampaign.FundingSource` enum (nullable, default null = tenant-funded). 1 additive field + 1 enum. Không break existing campaigns.

---

## 10. Loyalty Funding Rule (BOM-003 — giữ nguyên v1.0)

Loyalty chỉ phát hành nếu:
```text
Eligible Order (completed, not refunded)
+ Reward Budget Available
```

**Không phát reward cho negative-margin order** — nhưng "margin" ở v2.0 = **Tenant Margin** (Price - CostPrice), không phải Vạn An margin. Vạn An chỉ check **order eligible**, không check tenant margin (đó là tenant's business).

> **Codebase match:** `LoyaltyRewards.AddPoints()` đã có. Cần **budget check** trước khi AddPoints — xem Section 11.

---

## 11. Loyalty Budget (v2.0 — additive, quan trọng nhất)

Mỗi tenant có loyalty budget:
```text
Monthly Loyalty Budget    — cap issuance per tenant per month
Daily Loyalty Budget      — cap issuance per tenant per day
Per Customer Limit        — cap per customer per day
Per Order Limit           — cap per order (% of order value)
```

Khi budget exhausted → **reward rate giảm về 0** (không pause tenant, vẫn cho bán, chỉ không phát reward).

> **Codebase change:** Add fields to `LoyaltyTenantConfig`:
> - `int? MonthlyPointsBudget` (nullable, null = unlimited)
> - `int? DailyPointsBudget`
> - `int? PerCustomerDailyLimit`
> - `decimal? PerOrderRateCap` (e.g. 0.03 = max 3% of order value)
> - `int? PointsIssuedThisMonth` (runtime counter, reset monthly via job)
> - `int? PointsIssuedToday` (runtime counter, reset daily)
>
> 6 additive nullable fields. Không break existing config. Migration thêm columns. Budget enforcement logic trong `LoyaltyService` — check trước `AddPoints()`.

---

## 12. Reward Lifecycle (giữ nguyên v1.0 Section 19)

```text
ORDER CREATED → ORDER COMPLETED → REWARD PENDING
→ FRAUD CHECK → REWARD EARNED → AVAILABLE
→ REDEEMED / EXPIRED / CANCELLED
```

> **Codebase match:** `LoyaltyRewards.History` (JSON) track lifecycle. `FraudFlag` entity cho fraud check. Community Commerce Sprint 4 (Risk Scoring + FraudFlag) COMPLETE.

---

## 13. Reward Reversal (giữ nguyên v1.0 Section 20 + INV-002)

Order Cancelled / Refunded / Fraud → **reward phải reverse**.

> **Codebase match:** `LoyaltyRewards.DeductPoints()` đã có. `AccountingEntry` reversal pattern đã có. Cần **orchestration**: refund event → trigger reward reversal. Additive service logic, không break domain.

---

## 14. Customer Acquisition — KHÔNG đốt tiền quảng cáo (v2.0 core strategy)

4 nguồn (giữ v1.0 Section 23), nhưng **priority khác**:

| Source | v1.0 priority | v2.0 priority | Cơ chế |
|--------|---------------|---------------|--------|
| **Referral** | Medium | **#1 — PRIMARY** | Community Commerce viral loop (đã build) |
| **Merchant Traffic** | Medium | **#2** | Tenant tự mang traffic, Vạn An convert qua loyalty |
| Organic | Low | #3 | SEO + KhachLink PWA + word-of-mouth |
| Paid Marketing | High | **#4 — LAST RESORT** | Chỉ khi CAC < LTV proven |

### Tại sao Referral #1:

- **Community Commerce Sprint 0-7 đã COMPLETE** — `SalesReferral`, `ProductReferralConfig`, `AppInstallAttribution`, `CommunityRole`, `WalletTransaction`, `CommunityFundSpendRecord`. **0 code change cần thêm.**
- Referral reward chỉ trả khi **qualified purchase** (v1.0 Section 24 đúng) — không trả cho install/register. Đã enforce qua `SalesReferral` + `FraudFlag`.
- **Viral loop**: customer giới thiệu customer → nhận commission từ real commerce → không cần đốt tiền ads.
- **Investor pitch**: "0 paid CAC, growth từ referral commerce" = metrics hấp dẫn hơn "burn X VND for Y users".

### Merchant Traffic #2:

- Tenant có sẵn khách quen (quán cà phê, quán cơm). Vạn An convert offline customers → app users qua **loyalty incentive** (tích điểm lần đầu).
- **Tenant mang traffic, Vạn An giữ retention** — đúng vai trò platform.

> **Codebase match:** `KhachLinkHomeSettings` + `FeaturedProduct` cho tenant spotlight. `LoyaltyRewards` first-purchase bonus có thể config. 0 code change.

---

## 15. Merchant Economics & Tiering (v2.0 — additive, soft)

### Merchant Commercial Score (giữ v1.0 Section 26, simplify)

```text
Revenue Contribution       — GMV qua tenant
Order Volume               — số orders
Fulfillment SLA            — avg delivery time (from DeliveryTracking)
Cancellation Rate          — % orders cancelled
Customer Rating            — avg rating (if available)
Repeat Customer Rate       — % customers reorder
```

### Merchant Tier (additive field on Tenant)

```text
Tier S — Anchor    : high volume, high retention, featured on KhachLink
Tier A — Growth    : good economics, growing
Tier B — Standard  : stable
Tier C — Risk      : high cancellation / poor SLA → flag + review
```

> **Codebase change:** Add `Tenant.Tier` enum (nullable, default null = unranked). 1 additive field. Tier calculation = **read-only query** (no new entity), run weekly, store result in `Tenant.Tier`. FeaturedProduct đã có cho Tier S spotlight.

---

## 16. Order Ownership (v2.0 — đã có sẵn)

```text
SellerOfRecord    = Tenant (Order.TenantId)         ← đã có
Supplier          = Tenant's own supplier           ← không track (tenant's business)
FulfillmentProvider = Shipper (DeliveryTask)        ← đã có
PaymentRecipient  = Vạn An (Gateway collects)       ← đã có
RevenueOwner      = Tenant                          ← đã có (Marketplace mode)
RewardOwner       = Vạn An                          ← đã có (LoyaltyRewards tenant-scoped or Alliance)
```

> **Codebase match:** 100%. 0 code change. Đây là điểm khớp lớn nhất — v2.0 không đụng đến Order entity structure.

---

## 17. Refund Architecture (v2.0 — orchestration only)

Refund về **Original Payment Method** (giữ v1.0 Section 29). Khi refund:
```text
Payment Reversed          → PaymentProvider
Revenue Reversed          → Tenant AccountingEntry (reversal)
Reward Reversed           → LoyaltyRewards.DeductPoints
Platform Fee Reversed     → Vạn An (if already settled)
Promotion Adjusted        → Campaign budget restored
```

> **Codebase change:** 0 entity change. Cần **RefundOrchestrationService** (new service, not entity) — điều phối các reversal đã có. `AccountingEntry` reversal pattern + `LoyaltyRewards.DeductPoints` + `WalletTransaction` reversal đã build.

---

## 18. Inventory Economics (v2.0 — tenant's responsibility)

Vạn An **không track inventory** ở Marketplace/Drop-Ship mode. Tenant tự quản lý stock trong ShopERP SQLite (đã có `Inventory` entity).

Vạn An chỉ **aggregate reporting** cho dashboard network:
```text
Stockout Rate     — % products IsActive=false (proxy)
Category Velocity — orders per category per week
```

> **Codebase match:** `Inventory` entity đã có (per-tenant SQLite). `Product.IsActive` cho stockout proxy. 0 code change. Network dashboard = read-only cross-tenant query trên PG (Orders + Products metadata).

---

## 19. Category Score & Unit Economics Gate (v2.0 — reporting, not hard gate)

Giữ tư duy v1.0 (Section 31-33) nhưng **không hard-block orders** ở MVP. Thay bằng:

### Category Health Report (weekly, read-only)
```text
Category | CM avg | Demand | Repeat | Velocity | Loyalty ROI | Status
Coffee   | ★★★★★ | ★★★★★ | ★★★★★ | ★★★★★   | ★★★★        | SCALE
Lunch    | ★★★   | ★★★★★ | ★★★★  | ★★★      | ★★★         | OPTIMIZE
Grocery  | ★     | ★★★   | ★★    | ★★       | ★★          | HOLD/EXIT
```

### Gate States (giữ v1.0 Section 32)
```text
TEST → VALIDATE → SCALE → OPTIMIZE/EXIT
```

**Nhưng**: state = **dashboard label**, không phải order blocker. SystemAdmin dùng để quyết định: (a) spotlight category trên KhachLink, (b) adjust loyalty reward rate, (c) offboard tenant chronic âm margin.

> **Codebase change:** 0 entity change. Cần **CategoryHealthReportService** (new service, read-only query). `Product.Category` (string) đủ để aggregate. Không cần `Category` entity riêng ở MVP.

---

## 20. Micro-Market (v2.0 — reuse Tenant geo, no new entity)

v1.0 yêu cầu `MicroMarket` entity + `MarketLaunchGate`. v2.0 **không cần entity mới**:

- `TenantSettings.Address + Latitude + Longitude` **đã có**.
- Micro-market = **cluster tenants by geo proximity** (query: tenants within X km).
- Market Launch Gate = **reporting**: "Phường A có N tenants, M categories, K orders/week → READY/NOT READY".

> **Codebase match:** `TenantSettings.Latitude/Longitude` đã có. 0 code change. Micro-market dashboard = geo-cluster query. Có thể thêm `TenantSettings.District` (string, additive) cho easier clustering — 1 optional field.

---

## 21. Community Commerce (v2.0 — đã build, leverage tối đa)

v1.0 Section 38-39 đúng và **đã implement**:

```text
Referral Partner    → SalesReferral (Sprint 4)
Sales Partner       → CommunityRole (Sprint 0)
Community Seller    → CommunityRole + WalletTransaction (Sprint 5)
Delivery Partner    → DeliveryTask + CommunityRole (Sprint 2)
```

Commission từ **qualified commerce** (không từ recruitment) — đã enforce. Anti-MLM đúng.

> **Codebase match:** Community Commerce Sprint 0-7 COMPLETE. 0 code change. Đây là **growth engine chính** của v2.0 — thay thế paid ads.

---

## 22. Delivery Economics (v2.0 — giữ v1.0, đơn giản)

```text
Customer pays ShippingFee (Order.ShippingFee)
   ↓
Shipper receives DeliveryFee (Order.DeliveryFee in Reseller, or ShippingFee in Marketplace)
   ↓
If FREE DELIVERY campaign:
   Funding Source pays the gap
```

> **Codebase match:** `Order.ShippingFee` + `Order.DeliveryFee` đã có. Free-delivery = campaign với `FundingSource` (Section 9). 0 entity change.

---

## 23. Accounting Event Model (giữ v1.0 Section 42)

Mỗi business event tạo accounting reference:
```text
SALE_COMPLETED     → Tenant AccountingEntry (revenue) + Vạn An AccountingEntry (platform fee)
REFUND_COMPLETED   → Reversal entries (both)
REWARD_EARNED      → LoyaltyRewards + Loyalty liability reference
REWARD_REDEEMED    → RedemptionRecord + loyalty expense
```

> **Codebase match:** `AccountingEntry` immutable + reversal đã có. `OutboxEvent` cho event-driven. Cần **CorrelationId propagation** — xem Section 24.

---

## 24. Ledger Architecture (v2.0 — simplify từ 3-ledger enterprise)

v1.0 yêu cầu 3 ledger tách biệt (Commercial/Loyalty/Operational) với CorrelationId. v2.0 **không tạo ledger abstraction mới** — dùng entities hiện có + **1 additive field**:

### CorrelationId (additive, quan trọng)
```text
Order.Id (PK) = CorrelationId root
   ↓ propagate to:
   AccountingEntry.CorrelationId  (additive nullable Guid)
   LoyaltyRewards.History JSON     (đã có, add orderId entry)
   DeliveryTask.OrderId            (đã có)
   OutboxEvent.CorrelationId       (additive nullable Guid)
```

> **Codebase change:** Add `CorrelationId` (nullable Guid) to `AccountingEntry` + `OutboxEvent`. 2 additive fields. Set tại creation time = `Order.Id`. **Không break** existing entries (null = legacy). Trace xuyên suốt 3 "ledger" (thực chất là 3 nhóm entity) mà không cần ledger abstraction layer.

### "Three-Ledger" thực tế (không cần entity mới):
- **Commercial Ledger** = `AccountingEntry` (per-tenant, đã có) + Vạn An platform-fee entries (new tenant "VANAN_PLATFORM" hoặc separate PG table).
- **Loyalty Ledger** = `LoyaltyRewards.History` (JSON) + `AllianceTransaction` (đã có).
- **Operational Ledger** = `DeliveryTask` + `DeliveryTracking` + `Inventory` (đã có).

---

## 25. Data Ownership (giữ v1.0 Section 45 — đã khớp)

| Layer | System of Record | Codebase match |
|-------|-----------------|----------------|
| CoreHub/Gateway PG | Orders, Accounting, Tenants, Users, Loyalty (Alliance), FeaturedProducts | ✅ |
| ShopERP SQLite | Products, Inventory, local stock, preparation | ✅ |
| KhachLink | Customer-facing UI only, no financial source of truth | ✅ |

> **0 code change.** Kiến trúc Option C (Gateway = Order Creator + Routed Async Delivery) đã đúng.

---

## 26. Critical Invariants (v2.0 — adjust từ v1.0)

| ID | v1.0 | v2.0 | Codebase |
|----|------|------|----------|
| INV-001 | Order.Completed → Revenue recognized | **Giữ** — Tenant revenue | ✅ AccountingEntry |
| INV-002 | Refunded → Reward reversed | **Giữ** | ✅ DeductPoints |
| INV-003 | No Supplier Deposit → No Point Liability | **Giữ** | ✅ |
| INV-004 | Point Balance ≠ Cash Balance | **Giữ** | ✅ LoyaltyRewards ≠ Wallet |
| INV-005 | Negative CM → Cannot auto-scale | **Adjust**: Negative **Tenant** Margin → flag (not block) | ✅ Dashboard |
| INV-006 | Every Reward → Funding Source | **Giữ** — default = Vạn An | ⚠️ Add field |
| INV-007 | Every Promotion → Budget | **Giữ** | ⚠️ Add field |
| INV-008 | Every Order → Seller + Economics | **Adjust**: Seller = Tenant, Economics = Tenant margin + Platform fee | ✅ |
| **INV-009 (new)** | Platform Fee must be ≥ Loyalty Cost per order | Vạn An không phát reward nhiều hơn fee thu vào | ⚠️ Check logic |

---

## 27. Operational Dashboard (v2.0 — per-tenant + network aggregate)

### Per-Tenant Dashboard (đã có, enhance)
```text
GMV (tenant) | Orders | AOV | Repeat Rate | Loyalty Cost | Refund Rate
```
> **Codebase match:** ShopERP admin dashboard đã có. 0 code change.

### Network Dashboard (new, read-only, for investors)
```text
GMV (network)          = SUM all tenant orders
Active Tenants         = COUNT tenants with orders this month
Active Customers       = COUNT distinct customers
Repeat Rate            = % customers with >1 order
Platform Revenue       = SUM platform fees
Loyalty Cost           = SUM points issued × point value
Loyalty ROI            = (repeat GMV - loyalty cost) / loyalty cost
Contribution Profit    = Platform Revenue - Loyalty Cost - Ops Cost
CAC (referral)         = referral commission paid / new customers acquired
```

> **Codebase change:** 0 entity change. **NetworkDashboardService** (new, read-only cross-tenant query on PG). Đây là **investor-facing dashboard** — chứng minh unit economics mà không cần GMV hype.

---

## 28. Growth Strategy — Prove, Don't Burn (v2.0 core)

### Giai đoạn 1: Prove (0-6 tháng)
- **Onboard 5-10 anchor tenants** trong 1 phường (coffee, lunch, grocery, beauty).
- **Bật Loyalty Alliance** (đã build) — cross-tenant rewards tạo moat.
- **Community Commerce referral** (đã build) — viral acquisition, 0 paid CAC.
- **Measure**: per-tenant GMV, repeat rate, loyalty ROI, referral conversion.
- **Goal**: chứng minh 1 micro-market positive unit economics.

### Giai đoạn 2: Show (6-12 tháng)
- **Network Dashboard** cho investors: "X phường, Y tenants, Z% repeat rate, $ platform revenue, 0 paid CAC".
- **Expand 2-3 phường mới** dựa trên geo clustering (TenantSettings.Lat/Lng).
- **Tune loyalty budget** per tenant based on real data.
- **Goal**: 3 micro-markets, network-level positive contribution.

### Giai đoạn 3: Scale (12+ tháng, khi có vốn)
- **Paid marketing** chỉ khi CAC < 30% LTV proven từ referral data.
- **Reseller mode** cho 1-2 category chiến lược (margin > 30%, supplier độc quyền).
- **Micro-market expansion** theo gate criteria (Section 20).

> **Khác v1.0**: v1.0 muốn scale ngay từ "network effect". v2.0 scale **sau khi prove**, dùng data thu hút vốn, không đốt tiền mua user.

---

## 29. What Vạn An Is Really Building (v2.0 — honest)

v1.0 claim "Local Commerce Operating System" (7 mặt trận). v2.0 honest:

> **Vạn An là Loyalty-Network Platform cho hyper-local commerce.**

```text
Demand orchestration (KhachLink)
+ Loyalty network (Alliance — cross-tenant rewards)
+ Community referral engine (viral acquisition)
+ Settlement & accounting (per-tenant, compliant TT 152/2025)
= Retention moat cho tenant, take-rate revenue cho Vạn An
```

**Không phải**: marketplace (Shopee), delivery (Grab), wallet (Momo), procurement (Tiki Trading).
**Là**: lớp **loyalty + referral + settlement** trên top of tenant-owned commerce. Asset-light, capital-light, moat = loyalty network density.

---

## 30. Codebase Impact Summary — Tối thiểu

### 0 code change (reuse trực tiếp)
- `Order` entity + `CommerceMode.Marketplace` default
- `Product` + `CostPrice` + `Price` + `Category` + `VatRate`
- `Customer` + `LoyaltyRewards` + `AllianceWallet` + `AllianceTransaction`
- `PromoCampaign` + `PromoCampaignRecipient` (cần FundingSource — xem dưới)
- `Voucher` + `RedemptionRecord` + `RedemptionCatalogItem`
- `DeliveryTask` + `DeliveryTracking` + `CommunityRole`
- `SalesReferral` + `ProductReferralConfig` + `AppInstallAttribution`
- `WalletTransaction` + `CommunityFundSpendRecord` + `FraudFlag`
- `AccountingEntry` (immutable + reversal)
- `OutboxEvent` + `QueuedEvent` + `IdempotentOperation`
- `Tenant` + `TenantSettings` (Address, Lat/Lng, CommerceModeOverride)
- `FeaturedProduct` + `KhachLinkHomeSettings`
- `ShopInstance` (Multi-VPS routing)
- Gateway Option C architecture (Order Creator + Routed Async Delivery)
- KhachLink HTTP-only PWA
- ShopERP per-tenant SQLite

### Additive fields (không break existing)
| Entity | Field | Type | Default | Purpose |
|--------|-------|------|---------|---------|
| `LoyaltyTenantConfig` | `MonthlyPointsBudget` | `int?` | null (unlimited) | Cap issuance/month |
| `LoyaltyTenantConfig` | `DailyPointsBudget` | `int?` | null | Cap issuance/day |
| `LoyaltyTenantConfig` | `PerCustomerDailyLimit` | `int?` | null | Cap per customer/day |
| `LoyaltyTenantConfig` | `PerOrderRateCap` | `decimal?` | null | Cap % of order value |
| `LoyaltyTenantConfig` | `PointsIssuedThisMonth` | `int` | 0 | Runtime counter |
| `LoyaltyTenantConfig` | `PointsIssuedToday` | `int` | 0 | Runtime counter |
| `PromoCampaign` | `FundingSource` | `enum?` | null (tenant) | Who funds discount |
| `Tenant` | `Tier` | `enum?` | null (unranked) | S/A/B/C tier |
| `TenantSettings` | `PlatformFeeRate` | `decimal?` | 0.05m (5%) | Take-rate |
| `TenantSettings` | `District` | `string?` | null | Geo clustering |
| `AccountingEntry` | `CorrelationId` | `Guid?` | null (legacy) | Trace root = Order.Id |
| `OutboxEvent` | `CorrelationId` | `Guid?` | null (legacy) | Event trace |
| `Order` | `PlatformFeeAmount` | `decimal?` | null | Marketplace fee snapshot |

**Total: 13 additive nullable fields + 2 enums. 0 breaking change. 0 entity refactor. 0 architecture change.**

### New services (logic only, no new entities)
| Service | Purpose | Mode |
|---------|---------|------|
| `LoyaltyBudgetService` | Check budget before `AddPoints()` | Additive |
| `RefundOrchestrationService` | Coordinate reversal on refund | Additive |
| `CategoryHealthReportService` | Weekly category score (read-only) | Additive |
| `NetworkDashboardService` | Investor-facing network metrics | Additive |
| `MerchantTieringService` | Weekly tier calculation → `Tenant.Tier` | Additive |

### New enums
```csharp
public enum PromoFundingSource { TenantFunded = 0, VanAnFunded = 1, SharedFunded = 2, LoyaltyFunded = 3 }
public enum MerchantTier { Unranked = 0, S = 1, A = 2, B = 3, C = 4 }
```

### Migrations
- 1 migration: add 13 columns (all nullable/default) + 2 enums.
- Backward compat: existing rows get null/default, behavior unchanged.

---

## 31. Comparison v1.0 vs v2.0

| Aspect | v1.0 (Retailer) | v2.0 (Platform-Light) |
|--------|-----------------|----------------------|
| Seller of Record | Vạn An | Tenant |
| Inventory risk | Vạn An 100% | Tenant 100% (default) |
| Capital intensity | High (working capital) | Low (asset-light) |
| Revenue model | Gross Margin | Take-Rate % GMV |
| Network effect claim | Yes (but tautological) | No (honest: retention moat) |
| New entities needed | 12+ | 0 |
| Additive fields | N/A (major refactor) | 13 |
| Codebase match | ~10% | ~95% |
| Growth strategy | Flywheel + paid scale | Community referral + prove-then-scale |
| Investor pitch | "Network effect" (unproven) | "0 paid CAC, positive unit economics per micro-market" (data-driven) |
| Accounting | Network consolidation (major refactor) | Per-tenant (existing) + platform-fee entries |
| Complexity | Enterprise-grade (3-ledger, procurement, gates) | MVP-grade (reuse + additive discipline) |
| Time to prove | 6-12 months (build infrastructure first) | 1-3 months (add fields + services) |

---

## 32. Final Operating Principles (v2.0 — 5 nguyên tắc, không 7)

```text
1. REAL COMMERCE       — Tenant bán hàng thật, Vạn An orchestrate thật
2. REAL LOYALTY        — Reward từ real commerce, capped by budget, reversed on refund
3. REAL REFERRAL       — Commission từ qualified purchase, không từ recruitment
4. POSITIVE UNIT ECON  — Measure per-tenant + per-micro-market, scale only when proven
5. ASSET-LIGHT SCALE   — Tenant gánh hàng, Vạn An gánh loyalty, không gánh inventory
```

Cô đọng:
> **Tenant sells real goods. Vạn An rewards real commerce.
> No positive unit economics → no scale. No paid CAC → no burn.
> Prove in 1 micro-market, then expand with data, not with cash.**

---

## 33. Next Layer — Business Rules (giữ v1.0 Section 58, simplify)

Từ BOM v2.0, BR spec cần viết:

```text
BR-001  Tenant Eligibility & Onboarding (simplified, no merchant approval gate)
BR-002  Product Onboarding (tenant self-serve + soft compliance check)
BR-003  Pricing (tenant sets Price/CostPrice, Vạn An sets PlatformFeeRate)
BR-004  Order Ownership (Tenant = seller, Vạn An = orchestrator)
BR-005  Payment & Settlement (Gateway collects, settle to tenant)
BR-006  Refund & Reversal (coordinate payment + accounting + loyalty reversal)
BR-007  Promotion & Funding Source (4 sources, budget required)
BR-008  Loyalty Issuance (budget-capped, eligibility-checked)
BR-009  Loyalty Redemption (Alliance cross-tenant)
BR-010  Reward Reversal (on refund/cancel/fraud)
BR-011  Referral Commission (qualified purchase only, anti-MLM)
BR-012  Community Seller Roles (4 roles, capability-based)
BR-013  Delivery & Shipping (customer pays, free-delivery = campaign)
BR-014  Merchant Tiering (weekly score → S/A/B/C, soft)
BR-015  Unit Economics Reporting (category + tenant + micro-market, read-only)
BR-016  Loyalty Budget Enforcement (monthly/daily/per-customer/per-order caps)
```

**16 BR thay vì 22 BR của v1.0** — bỏ Supplier Contract, Procurement, Consignment, Market Expansion Gate, Merchant Offboarding (soft, không cần BR riêng ở MVP).

---

## 34. Definition of Success (v2.0 — honest, measurable)

VALCN v2.0 được xem là **commercially validated** khi:

```text
≥ 1 micro-market (1 phường) với:
  ≥ 5 active tenants
  ≥ 100 active customers
  ≥ 30% repeat rate
  ≥ 0 VND paid CAC (growth từ referral only)
  Platform Revenue > Loyalty Cost + Ops Cost (per micro-market)
  ≥ 3 months sustained
```

Khi đó:
> **Vạn An đã chứng minh loyalty-network platform có thể tạo tăng trưởng
> từ chính dòng thương mại thật, không cần đốt tiền mua user.**

Đây là **data point investor cần**, không phải "network effect" trừu tượng.

---

## Appendix A: Tại sao v2.0 win cho giai đoạn chứng minh

1. **0 paid CAC**: Community Commerce referral (đã build) = viral loop. Investor thấy "growth without burn".
2. **Asset-light**: Tenant gánh inventory, Vạn An không cần working capital. Runway dài hơn 10x.
3. **Khớp codebase 95%**: 13 additive fields vs 12 new entities. Ship trong 1-3 tháng, không 6-12.
4. **Honest moat**: Loyalty network density (cross-tenant Alliance) = moat thật, không phải "network effect" claim.
5. **Per-tenant accounting intact**: Không refactor `AccountingEntry`, không phá governance hard stops.
6. **Reseller mode保留**: Khi tìm được category chiến lược (margin > 30%), flip per-SKU — đã có code.
7. **Investor data > GMV hype**: Network Dashboard chứng minh unit economics, không cần "downloads/followers".

## Appendix B: Rủi ro v2.0 & mitigation

| Rủi ro | Mitigation |
|--------|-----------|
| Tenant không tham gia (take-rate太高) | Default 5%, có thể 0% cho anchor tenant (loss leader) |
| Loyalty liability runaway | Budget cap (Section 11) + redemption rate monitoring |
| Value prop yếu vs direct purchase | Loyalty Alliance cross-tenant = incentive khách mua qua app (tích điểm dùng ở quán khác) |
| Tenant bán lỗ chronic | Tier C flag + offboard (soft, Section 15) |
| No network effect (honest) | Retention moat đủ cho giai đoạn prove; network effect là aspiration, không phải assumption |
| Referral fraud | FraudFlag + risk scoring (Sprint 4 đã build) + qualified purchase gate |

---

**End of BOM v2.0 — PLATFORM-LIGHT.**

> Tài liệu này là **Business Source of Truth** cho giai đoạn chứng minh hiệu quả (0-12 tháng).
> Sau khi commercially validated (Section 34), có thể evolve toward v1.0's retailer elements (Reseller mode scale, procurement flow) **dựa trên data thật, không phải assumption**.
