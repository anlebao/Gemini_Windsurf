# VẠN AN LOCAL COMMERCE NETWORK
# BUSINESS OPERATING MODEL

## Version 1.0

---

# 1. Purpose

Tài liệu này định nghĩa **cơ chế vận hành kinh doanh chuẩn** của Vạn An Local Commerce Network (VALCN).

Tài liệu là lớp trung gian giữa:

```text
BUSINESS VISION
      ↓
BUSINESS OPERATING MODEL
      ↓
BUSINESS RULES
      ↓
PRODUCT REQUIREMENTS
      ↓
SOFTWARE REQUIREMENTS
      ↓
IMPLEMENTATION
```

Tài liệu này phải được xem là **Business Source of Truth** trước khi xây dựng hoặc thay đổi các module phần mềm liên quan đến VALCN.

---

# 2. Operating Model at a Glance

VALCN vận hành theo mô hình:

```text
                    LOCAL SUPPLIERS
                          │
                          │ Supply
                          ▼
                 ┌──────────────────┐
                 │      VẠN AN      │
                 │                  │
                 │  BUY → OPERATE   │
                 │  → SELL → REWARD │
                 └────────┬─────────┘
                          │
                          │ Retail
                          ▼
                     CUSTOMERS
                          │
                          │ Purchase
                          ▼
                        GMV
                          │
                          ▼
                     GROSS MARGIN
                          │
             ┌────────────┼────────────┐
             ▼            ▼            ▼
         Operations     Loyalty      Profit
             │            │
             │            ▼
             │        Retention
             │            │
             └────────────┴──────→ More GMV
```

---

# 3. Core Operating Principle

## BOM-001 — Every Commercial Flow Must Have an Economic Owner

Mỗi giao dịch phải xác định rõ:

- ai mua;
- ai bán;
- ai sở hữu hàng;
- ai chịu rủi ro;
- ai chịu chi phí;
- ai ghi nhận doanh thu;
- ai chịu trách nhiệm refund;
- ai tài trợ loyalty.

Không được có trạng thái mơ hồ:

```text
Vạn An thu tiền
nhưng Shop là người bán
```

hoặc:

```text
Shop bán hàng
nhưng Vạn An chịu toàn bộ nghĩa vụ
```

---

# 4. The Four Core Economic Relationships

VALCN có 4 quan hệ kinh tế độc lập.

## Relationship A — Supplier → VanAn

```text
Supplier
    ↓
Product / Service
    ↓
VanAn Purchase
```

Đây là **procurement transaction**.

---

## Relationship B — VanAn → Customer

```text
VanAn
   ↓
Product / Service
   ↓
Customer
```

Đây là **retail transaction**.

---

## Relationship C — VanAn → Logistics Provider

```text
VanAn
   ↓
Delivery Order
   ↓
Logistics Provider
```

Đây là **fulfillment transaction**.

---

## Relationship D — VanAn → Customer Loyalty

```text
VanAn
   ↓
Loyalty Program
   ↓
Customer Reward
```

Đây là **customer incentive relationship**.

Không phải payment relationship.

---

# 5. Money Flow

## 5.1 Standard Retail Flow

```text
Supplier
   │
   │ Invoice / Purchase
   ▼
VẠN AN
   │
   │ Purchase Cost
   ▼
Supplier
```

Sau đó:

```text
Customer
   │
   │ Payment
   ▼
VẠN AN
```

Vạn An ghi nhận:

```text
Revenue
-
COGS
=
Gross Margin
```

---

# 6. Money Flow Example

Giả sử:

```text
Supplier Price       = 40,000
Retail Price         = 50,000
```

Transaction:

```text
Customer pays VanAn
        50,000
           │
           ▼
        VanAn
           │
           ├── COGS          40,000
           ├── Gross Margin   10,000
           │
           └── Gross Margin Allocation
```

Ví dụ:

```text
Gross Margin = 10,000

Operations       3,000
Loyalty          2,000
Marketing        1,000
Risk Reserve     1,000
Profit           3,000
```

Các tỷ lệ trên chỉ là ví dụ.

**Tỷ lệ thật phải được xác định bằng unit economics của từng category.**

---

# 7. No Merchant Deposit Rule

## BOM-002

VALCN không được yêu cầu merchant:

```text
Deposit money
```

để:

```text
Create points
Back points
Fund another merchant
```

Nếu merchant cung cấp hàng hóa cho Vạn An:

> Merchant nhận tiền theo commercial purchase transaction theo hợp đồng và điều kiện thanh toán đã thỏa thuận.

---

# 8. Procurement Operating Models

Mỗi SKU phải có một trong các procurement modes:

```text
OUTRIGHT
ON_DEMAND
CONTROLLED_CONSIGNMENT
```

Không cho phép SKU không có procurement mode.

---

# 9. OUTRIGHT Model

## Flow

```text
Supplier
   ↓
Purchase Order
   ↓
Goods Received
   ↓
Inventory
   ↓
Customer Order
   ↓
Sale
```

Vạn An chịu:

- inventory risk;
- expiry risk;
- shrinkage risk;
- markdown risk.

Đổi lại:

- có quyền kiểm soát hàng;
- có thể đạt wholesale margin tốt hơn;
- fulfillment nhanh hơn.

---

# 10. ON-DEMAND Model

## Flow

```text
Customer
   ↓
Order
   ↓
VanAn
   ↓
Purchase Order
   ↓
Supplier
   ↓
Prepare
   ↓
Pickup
   ↓
Customer
```

Phù hợp với:

- coffee;
- food;
- fresh food;
- made-to-order services.

Ưu điểm:

- giảm inventory;
- giảm expiry;
- giảm working capital.

Nhược điểm:

- phụ thuộc SLA;
- khó kiểm soát preparation time;
- dễ phát sinh cancellation.

---

# 11. CONTROLLED CONSIGNMENT

Chỉ sử dụng khi có:

- hợp đồng rõ ràng;
- ownership rõ ràng;
- accounting treatment rõ ràng;
- inventory responsibility rõ ràng;
- return mechanism rõ ràng.

Không sử dụng consignment chỉ để né việc xác định seller/reseller relationship.

---

# 12. Product Onboarding

Mỗi sản phẩm trước khi được bán phải trải qua:

```text
Merchant Approval
      ↓
Product Approval
      ↓
Commercial Pricing
      ↓
Compliance Check
      ↓
Inventory / Fulfillment Setup
      ↓
Loyalty Configuration
      ↓
Published
```

Không cho phép merchant tự ý publish SKU vào VALCN mà không qua approval.

---

# 13. Product Commercial Gate

Một SKU cần có:

```text
Cost Price
Selling Price
Expected Gross Margin
Expected Contribution Margin
Fulfillment Cost
Payment Cost
Expected Loyalty Cost
Return Risk
Expiry Risk
```

Nếu không tính được economics:

> SKU chưa đủ điều kiện scale.

---

# 14. Pricing Architecture

Mỗi SKU có:

```text
Supplier Cost
+
VanAn Margin
+
Variable Costs
+
Risk Buffer
=
Minimum Sustainable Price
```

Giá bán không được xác định chỉ dựa trên:

```text
Competitor Price
```

mà phải đảm bảo:

```text
Contribution Margin ≥ Required Threshold
```

---

# 15. Dynamic Commercial Pricing

Vạn An có thể có:

```text
Standard Price
Campaign Price
Bundle Price
Member Price
Clearance Price
```

Nhưng mọi giá bán phải kiểm tra:

```text
Expected Contribution Margin
```

trước khi campaign được activate.

---

# 16. Promotion Rule

Promotion không được hiểu đơn giản là:

```text
Discount = Marketing
```

Mà phải xác định:

```text
Who funds discount?
```

Có 4 nguồn:

### A. VanAn-funded

Vạn An giảm từ margin của mình.

### B. Supplier-funded

Supplier hỗ trợ giá mua.

### C. Shared-funded

Vạn An và Supplier cùng tài trợ.

### D. Loyalty-funded

Reward được tài trợ từ Loyalty Budget của Vạn An.

Mỗi promotion phải có Funding Source.

---

# 17. Loyalty Funding Rule

## BOM-003

Loyalty chỉ được phát hành nếu transaction đáp ứng:

```text
Eligible Order
+
Positive / Approved Economics
+
Reward Budget Available
```

Không cho phép:

```text
Negative-margin order
+
Unlimited reward
```

---

# 18. Reward Calculation

Mỗi transaction có:

```text
Reward Policy
```

Ví dụ:

```text
Category: Coffee
Reward Rate: 3%

Order Value: 100,000

Reward Budget:
3,000 points-equivalent units
```

Nhưng reward rate phải được giới hạn bởi:

```text
Maximum Loyalty Cost %
```

của category.

---

# 19. Reward Lifecycle

```text
ORDER CREATED
      ↓
ORDER CONFIRMED
      ↓
ORDER COMPLETED
      ↓
REWARD PENDING
      ↓
FRAUD / ELIGIBILITY CHECK
      ↓
REWARD EARNED
      ↓
AVAILABLE
      ↓
REDEEMED / EXPIRED / CANCELLED
```

Reward không được available ngay khi order mới tạo.

---

# 20. Reward Reversal

Nếu:

```text
Order Cancelled
Order Refunded
Fraud Confirmed
Duplicate Transaction
```

thì loyalty tương ứng phải:

```text
Reverse
```

Không để customer giữ reward từ một transaction đã bị hoàn tiền.

---

# 21. Loyalty Budget

Mỗi category/campaign có:

```text
Monthly Loyalty Budget
Daily Loyalty Budget
Per Customer Limit
Per Order Limit
Campaign Limit
```

Ví dụ:

```text
Coffee Category
Monthly Loyalty Budget = 20M

Campaign A
Budget = 5M

Campaign B
Budget = 3M
```

Khi budget exhausted:

```text
Campaign → Pause
```

hoặc chuyển sang reward rate thấp hơn theo policy.

---

# 22. Customer Value Loop

VALCN tối ưu vòng lặp:

```text
DISCOVER
   ↓
PURCHASE
   ↓
REWARD
   ↓
REDEEM
   ↓
RETURN
   ↓
PURCHASE MORE
```

North Star:

```text
Contribution Profit
per Active Customer
```

---

# 23. Customer Acquisition

Có 4 nguồn:

```text
Organic
Referral
Merchant Traffic
Paid Marketing
```

VALCN phải đo:

```text
CAC by Source
LTV by Source
Contribution by Source
```

Không đánh giá campaign chỉ bằng số lượng user đăng ký.

---

# 24. Referral Economics

Referral reward chỉ được trả khi:

```text
New Customer
+
Qualified Purchase
+
Minimum Order Value
+
No Fraud
```

Không trả reward chỉ vì:

```text
Install App
Register Account
Click Referral
```

---

# 25. Merchant Economics

Merchant tham gia VALCN phải nhận được ít nhất một trong các giá trị:

```text
Incremental Sales
Higher Customer Reach
Higher Repeat Rate
Lower Marketing Cost
Better Demand Visibility
Better Inventory Utilization
```

Nếu Merchant chỉ bán cho Vạn An với cùng volume mà không có incremental value:

> VALCN chưa tạo ra network advantage.

---

# 26. Merchant Commercial Score

Mỗi merchant có:

```text
Revenue Contribution
Gross Margin Contribution
Order Volume
Fulfillment SLA
Cancellation Rate
Return Rate
Customer Rating
Complaint Rate
Repeat Customer Rate
```

Từ đó tính:

```text
Merchant Health Score
```

---

# 27. Merchant Tiering

## Tier S — Strategic

- high volume;
- high margin;
- high quality;
- high retention.

## Tier A — Growth

- good economics;
- growing demand.

## Tier B — Standard

- stable but limited scale.

## Tier C — Risk

- low margin;
- high cancellation;
- poor SLA.

Tier C bị hạn chế campaign hoặc bị loại khỏi scale.

---

# 28. Order Ownership

Mỗi Order phải xác định:

```text
SellerOfRecord
Supplier
FulfillmentProvider
PaymentRecipient
RevenueOwner
RewardOwner
```

Trong mô hình chuẩn:

```text
SellerOfRecord = VanAn
Supplier       = Merchant
RewardOwner    = VanAn
```

---

# 29. Refund Architecture

Refund phải quay về:

```text
Original Payment Method
```

theo chính sách thanh toán áp dụng.

Không dùng:

```text
Loyalty Points
```

để che giấu một refund tiền thật.

Khi refund:

```text
Revenue Reversed
COGS Reversed
Reward Reversed
Promotion Adjusted
Contribution Recalculated
```

---

# 30. Inventory Economics

Inventory phải được đo bằng:

```text
Inventory Turnover
Days Inventory Outstanding
Expiry Rate
Waste Rate
Shrinkage Rate
Stockout Rate
```

Category có:

```text
High Margin
+
Low Turnover
```

chưa chắc tốt.

Mục tiêu là:

> **Margin × Velocity**

---

# 31. Category Score

Mỗi category được chấm theo:

```text
Contribution Margin
Demand
Repeat Rate
Inventory Velocity
Loyalty ROI
Delivery Efficiency
Risk
```

Ví dụ:

```text
Coffee
CM            ★★★★★
Demand        ★★★★★
Repeat        ★★★★★
Inventory     ★★★★★
→ SCALE

Lunch
CM            ★★★
Demand        ★★★★★
Repeat        ★★★★
Delivery      ★★
→ OPTIMIZE

Low-margin Grocery
CM            ★
Demand        ★★★
Inventory     ★★
→ HOLD / EXIT
```

---

# 32. Unit Economics Gate

Mỗi category phải đi qua 4 trạng thái:

```text
TEST
  ↓
VALIDATE
  ↓
SCALE
  ↓
OPTIMIZE / EXIT
```

Không được:

```text
TEST → SCALE
```

mà bỏ qua validation.

---

# 33. Gate Criteria

## TEST

Chưa đủ dữ liệu.

## VALIDATE

Có đủ dữ liệu để xác định:

```text
CM
Retention
Demand
Loyalty ROI
```

## SCALE

Điều kiện tối thiểu:

```text
Contribution Margin > 0
+
Stable Demand
+
Acceptable Fulfillment
+
Acceptable Loyalty ROI
```

## EXIT

Nếu:

```text
CM < 0
```

và không có phương án cải thiện economics trong thời gian quy định.

---

# 34. Network Density

Một micro-market chỉ được mở rộng khi đạt:

```text
Merchant Density
+
Customer Density
+
Order Density
```

Không scale chỉ vì:

```text
"có thêm merchant"
```

---

# 35. Micro-Market Operating Unit

Đơn vị vận hành cơ bản của VALCN:

```text
LOCAL MARKET
```

Ví dụ:

```text
Ward / District
```

Mỗi market có:

```text
Market Manager
Merchant Base
Customer Base
Delivery Coverage
Category Mix
GMV
Contribution Profit
```

---

# 36. Market Launch Gate

Một market mới chỉ được mở khi có:

```text
≥ Required Anchor Merchants
≥ Required Product Categories
≥ Delivery Coverage
≥ Customer Acquisition Plan
≥ Positive Unit Economics
```

---

# 37. Anchor Merchant Strategy

Mỗi micro-market cần một số:

```text
Anchor Merchants
```

là những merchant có khả năng tạo traffic.

Ví dụ:

```text
Popular Coffee
+
Popular Lunch
+
Popular Grocery
+
Popular Beauty
```

Anchor merchants giúp tạo initial demand.

---

# 38. Community Commerce

Community Seller chỉ được hưởng commission từ:

```text
Qualified Commerce
```

Ví dụ:

```text
Customer Order = 100,000
Contribution Margin = 15,000

Community Commission = 3,000
```

Commission phải nằm trong economic model.

Không được:

```text
Recruit Person A
Recruit Person B
Recruit Person C
→ receive commission
```

Commission không dựa trên recruitment.

---

# 39. Community Seller Roles

Có thể có:

```text
Referral Partner
Sales Partner
Community Seller
Delivery Partner
```

Một người có thể có nhiều capability nhưng phải được hệ thống quản lý bằng role/permission riêng.

---

# 40. Delivery Economics

Mỗi Order phải tính:

```text
Delivery Revenue
-
Actual Delivery Cost
=
Delivery Contribution
```

Nếu Vạn An tuyên bố:

```text
FREE DELIVERY
```

thì:

```text
FREE TO CUSTOMER
≠
FREE TO VAN AN
```

Chi phí phải được đưa vào Contribution Margin.

---

# 41. Free Delivery Funding

Có thể được tài trợ bởi:

```text
VanAn
Supplier
Campaign
Customer minimum order value
Merchant marketing budget
```

Mỗi delivery promotion phải có:

```text
Funding Source
Budget
Maximum Orders
```

---

# 42. Accounting Event Model

Mỗi business event phải tạo accounting reference.

Ví dụ:

```text
PURCHASE_COMPLETED
      ↓
Purchase Accounting

SALE_COMPLETED
      ↓
Revenue Accounting

REFUND_COMPLETED
      ↓
Reversal Accounting

REWARD_EARNED
      ↓
Loyalty Accounting Reference

REWARD_REDEEMED
      ↓
Promotion / Reward Expense Reference
```

Accounting không được phụ thuộc vào UI state.

---

# 43. Three-Ledger Architecture

VALCN duy trì ba lớp:

```text
COMMERCIAL LEDGER
       │
       ├── Sales
       ├── Purchase
       ├── COGS
       └── Profit

LOYALTY LEDGER
       │
       ├── Earn
       ├── Redeem
       ├── Expire
       └── Reverse

OPERATIONAL LEDGER
       │
       ├── Inventory
       ├── Delivery
       ├── Waste
       └── Fulfillment
```

Mỗi transaction có:

```text
CorrelationId
```

để truy vết xuyên suốt 3 lớp.

---

# 44. Business Event Backbone

Các event quan trọng:

```text
MerchantApproved
ProductApproved
PurchaseOrderCreated
GoodsReceived
CustomerOrderCreated
OrderConfirmed
OrderCompleted
PaymentCompleted
RefundCompleted
RewardPending
RewardEarned
RewardRedeemed
RewardReversed
DeliveryCompleted
InventoryAdjusted
```

Mỗi event phải idempotent.

---

# 45. Data Ownership

## CoreHub

System of Record cho:

- Customer;
- Merchant;
- Product;
- Order;
- Loyalty;
- Promotion;
- commercial references.

## ShopERP

System of Record cho:

- local stock;
- preparation;
- local operational execution.

## KhachLink

Customer-facing application.

Không được coi localStorage/PWA là source of truth cho financial data.

---

# 46. Critical Invariants

Hệ thống phải bảo đảm:

### INV-001

```text
Order.Completed
→ Revenue can be recognized
```

### INV-002

```text
Refunded Order
→ Related Reward must be reversed
```

### INV-003

```text
No Supplier Deposit
→ No Point Liability
```

### INV-004

```text
Point Balance
≠
Cash Balance
```

### INV-005

```text
Negative Contribution Margin
→ Cannot be automatically scaled
```

### INV-006

```text
Every Reward
→ Must have Funding Source
```

### INV-007

```text
Every Promotion
→ Must have Budget
```

### INV-008

```text
Every Order
→ Must have Seller + Supplier + Economics
```

---

# 47. Operational Dashboard

Ban điều hành phải nhìn được mỗi ngày:

```text
GMV
Net Revenue
Gross Margin
Contribution Profit
Orders
AOV
Active Customers
Repeat Rate
CAC
LTV
Loyalty Cost
Loyalty ROI
Delivery Cost
Refund Rate
Merchant Revenue
Inventory Risk
```

---

# 48. Executive North Star Dashboard

Dashboard cấp CEO:

```text
┌───────────────────────────────────────────┐
│              VALCN HEALTH                 │
├───────────────────────────────────────────┤
│ GMV                         xxx            │
│ Contribution Profit         xxx            │
│ Active Customers             xxx           │
│ Contribution / Customer      xxx           │
│ Active Merchants              xxx          │
│ Repeat Rate                  xx%           │
│ Loyalty ROI                  x.x           │
│ Order Contribution           xxx           │
│ Inventory Risk               xx%           │
└───────────────────────────────────────────┘
```

---

# 49. Weekly Business Review

Mỗi tuần phải trả lời:

### Customer

- Có thêm bao nhiêu customer?
- CAC?
- Repeat rate?
- LTV?

### Merchant

- Merchant nào tăng trưởng?
- Merchant nào giảm?
- Merchant nào có economics xấu?

### Product

- SKU nào bán tốt?
- SKU nào margin tốt?
- SKU nào gây waste?

### Loyalty

- Bao nhiêu reward?
- Bao nhiêu redeemed?
- Loyalty tạo thêm bao nhiêu GMV?
- Loyalty ROI?

### Operations

- Delivery cost?
- Cancellation?
- Refund?
- SLA?

### Finance

- Gross margin?
- Contribution margin?
- Cash burn?
- Working capital?

---

# 50. Monthly Scale Review

Mỗi tháng phân loại:

```text
CATEGORY
├── SCALE
├── OPTIMIZE
├── HOLD
└── EXIT
```

và:

```text
MERCHANT
├── EXPAND
├── MAINTAIN
├── IMPROVE
└── OFFBOARD
```

---

# 51. Strategic Decision Rule

Không quyết định scale dựa trên:

```text
GMV
Downloads
Followers
Merchant Count
```

mà dựa trên:

```text
Contribution Economics
+
Retention
+
Network Density
+
Loyalty ROI
```

---

# 52. Capital Allocation Rule

Mỗi 1 đồng Gross Margin tạo ra phải được phân bổ có chủ đích:

```text
Gross Margin
    │
    ├── Operating Cost
    ├── Loyalty Investment
    ├── Marketing
    ├── Risk Reserve
    ├── Growth Investment
    └── Profit
```

Không được để loyalty trở thành:

```text
"chi phí bao nhiêu cũng được để tăng user"
```

---

# 53. The Economic Flywheel

Mô hình hoàn chỉnh:

```text
               MORE MERCHANTS
                      ↓
                MORE PRODUCTS
                      ↓
                MORE CHOICE
                      ↓
               MORE CUSTOMERS
                      ↓
                    MORE
                     GMV
                      ↓
                 MORE MARGIN
                      ↓
              MORE LOYALTY FUND
                      ↓
              BETTER CUSTOMER
                    VALUE
                      ↓
                MORE RETENTION
                      ↓
                 MORE ORDERS
                      ↓
               MORE DATA
                      ↓
          BETTER PROCUREMENT
                      ↓
            BETTER ECONOMICS
                      ↓
                MORE MARGIN
                      └─────────────┐
                                    ↓
                            NETWORK EFFECT
```

---

# 54. What VALCN Is Really Building

VALCN không đơn thuần xây:

```text
App
POS
Loyalty
Marketplace
Delivery
```

VALCN đang xây:

> **Local Commerce Operating System**

gồm:

```text
Demand
+
Supply
+
Commerce
+
Loyalty
+
Fulfillment
+
Accounting
+
Intelligence
```

---

# 55. Final Operating Principle

Toàn bộ VALCN phải tuân theo 7 nguyên tắc:

```text
1. REAL COMMERCE
2. REAL CUSTOMER
3. REAL MARGIN
4. CONTROLLED LOYALTY
5. POSITIVE UNIT ECONOMICS
6. LOCAL NETWORK DENSITY
7. SUSTAINABLE SCALE
```

Hay cô đọng thành:

> **No real commerce → No reward.  
> No positive economics → No scale.  
> No network density → No expansion.**

---

# 56. Business Architecture Summary

```text
                         VALCN
                           │
        ┌──────────────────┼──────────────────┐
        │                  │                  │
      SUPPLY             DEMAND            LOYALTY
        │                  │                  │
   Merchants          Customers          Rewards
        │                  │                  │
        └──────────────────┼──────────────────┘
                           │
                       COMMERCE
                           │
                ┌──────────┼──────────┐
                │          │          │
             Orders     Payments   Fulfillment
                │          │          │
                └──────────┼──────────┘
                           │
                     ECONOMICS
                           │
              ┌────────────┼────────────┐
              │            │            │
             COGS       MARGIN       COSTS
              │            │            │
              └────────────┼────────────┘
                           │
                    CONTRIBUTION
                       PROFIT
                           │
                           ▼
                    SCALE DECISION
                           │
              ┌────────────┴────────────┐
              ▼                         ▼
            SCALE                      EXIT
```

---

# 57. Definition of Success

VALCN được xem là **commercially validated** khi chứng minh được:

```text
Customers repeatedly purchase
            AND
Merchants repeatedly supply
            AND
Each transaction has positive contribution economics
            AND
Loyalty increases retention economically
            AND
Micro-market density increases
            AND
Network expansion improves economics
```

Khi đó:

> **Vạn An không còn là một phần mềm loyalty cho SME.**

Vạn An đã trở thành:

> **một Local Commerce Network có khả năng tự tạo tăng trưởng từ chính dòng thương mại của nó.**

---

# 58. Next Layer — Business Rules Specification

Từ Operating Model này, lớp tiếp theo phải được đặc tả thành:

```text
BR-001  Merchant Eligibility
BR-002  Merchant Approval
BR-003  Supplier Contract
BR-004  Product Approval
BR-005  Pricing
BR-006  Procurement
BR-007  Order Ownership
BR-008  Payment
BR-009  Refund
BR-010  Promotion
BR-011  Loyalty
BR-012  Reward Redemption
BR-013  Referral
BR-014  Community Seller
BR-015  Delivery
BR-016  Inventory
BR-017  Accounting
BR-018  Fraud
BR-019  Unit Economics
BR-020  Scale Gate
BR-021  Market Expansion
BR-022  Merchant Offboarding
```

Các Business Rules này sẽ là **contract giữa Business, Accounting và Engineering**.

Sau đó mới phân rã thành:

```text
Business Rules
      ↓
Use Cases
      ↓
Domain Model
      ↓
API Contracts
      ↓
Database Model
      ↓
UI / UX
      ↓
Automated Tests
```

Đây là cấu trúc tôi khuyến nghị dùng làm **baseline chính thức cho VALCN trước khi bắt đầu SRS/implementation**.