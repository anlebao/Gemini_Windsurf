# VẠN AN LOCAL BUSINESS OS
## Financial Management & Business Intelligence Specification

**Version:** 1.0  
**Status:** Architecture / Business Specification  
**Target:** Hộ kinh doanh siêu nhỏ, SME nhỏ, Café, Barbershop/Hair Salon, Spa, Nail, Nhà hàng, Quán ăn, Retail và dịch vụ địa phương.

---

# 1. Vision

Vạn An Local Business OS là nền tảng vận hành và quản trị dành cho nhóm doanh nghiệp kinh tế tầng thấp, kết hợp:

- Sales / POS
- Inventory
- Recipe / BOM
- Customer
- Loyalty
- Accounting
- Cash Management
- AR/AP
- Tax
- Financial Management
- Business Planning
- Forecasting
- Scenario Simulation
- Management Advisory

Mục tiêu không phải chỉ là:

> "Ghi nhận doanh nghiệp đã làm gì."

Mà tiến tới:

> "Hiểu doanh nghiệp đang ở đâu, dự báo sẽ đi về đâu và đề xuất chủ doanh nghiệp nên làm gì."

---

# 2. Product Positioning

## 2.1 Traditional Accounting

```text
Transaction
    ↓
Accounting
    ↓
Report
```

Hệ thống chủ yếu trả lời:

- Đã bán bao nhiêu?
- Có bao nhiêu tiền?
- Có bao nhiêu tài sản?
- Nợ bao nhiêu?
- Lợi nhuận bao nhiêu?

## 2.2 Vạn An Business OS

```text
Transaction
    ↓
Accounting
    ↓
Financial Model
    ↓
Analysis
    ↓
Forecast
    ↓
Scenario
    ↓
Recommendation
    ↓
Business Action
    ↓
New Transaction
```

Hệ thống trả lời thêm:

- Tại sao lợi nhuận giảm?
- Điểm hòa vốn ở đâu?
- Nếu doanh thu giảm 10% thì sao?
- Muốn lời thêm 20 triệu phải làm gì?
- Có nên tăng giá?
- Có nên giảm chi phí?
- Có nên mở rộng?
- Có nên thu hẹp?
- Có nguy cơ thiếu tiền không?
- Sản phẩm nào đang làm giảm lợi nhuận?

---

# 3. Target Actors

## ACT-01 — Business Owner

Chủ hộ kinh doanh / chủ SME.

Quyền chính:

- Xem dashboard
- Xem tình hình tài chính
- Thiết lập mục tiêu
- Lập kế hoạch
- Chạy scenario
- Nhận cảnh báo
- Xem recommendation
- Phê duyệt hành động

---

## ACT-02 — Store Manager

Quản lý cửa hàng.

Quyền:

- Sales
- Inventory
- Purchase
- Staff operation
- Daily closing
- Operational dashboard
- Cost control

Không nhất thiết được xem toàn bộ thông tin tài chính nhạy cảm.

---

## ACT-03 — Accountant

Kế toán.

Quyền:

- Accounting
- Journal
- Ledger
- AR/AP
- Cash
- Bank
- Tax
- Reconciliation
- Financial statements
- Period closing

---

## ACT-04 — Cashier / Sales Staff

Quyền:

- Create order
- Receive payment
- Refund
- Close shift
- View assigned operational data

Không có quyền thay đổi accounting hoặc business plan.

---

## ACT-05 — Inventory Manager

Quyền:

- Receive goods
- Issue goods
- Stock count
- Waste
- Adjustment
- Purchase
- Recipe/BOM
- Inventory analysis

---

## ACT-06 — Financial Advisor

Actor của Vạn An.

Có thể là:

- Rule Engine
- Financial Intelligence Engine
- AI Advisor
- Human advisor trong CS4B

Nhiệm vụ:

```text
Detect
Explain
Forecast
Simulate
Recommend
```

---

## ACT-07 — System Administrator

Quản trị nền tảng.

Quyền:

- Tenant
- User
- Role
- Configuration
- Provider
- Integration
- System policy

---

## ACT-08 — External Provider

Các hệ thống bên ngoài:

- POS
- eInvoice
- Payment
- Bank
- Tax
- Accounting provider
- Delivery provider

---

# 4. Core Domain Model

```text
Tenant
│
├── BusinessProfile
│
├── Users
│
├── Stores
│
├── Products
│
├── Services
│
├── Customers
│
├── Suppliers
│
├── Sales
│
├── Purchases
│
├── Inventory
│
├── Recipe/BOM
│
├── Accounting
│   ├── GL
│   ├── AR
│   ├── AP
│   ├── Cash
│   ├── Bank
│   ├── Assets
│   └── Tax
│
├── FinancialManagement
│
├── BusinessPlans
│
├── Forecasts
│
├── Scenarios
│
└── Recommendations
```

---

# 5. Architectural Principle

## 5.1 Accounting is Financial Source of Truth

Accounting không bị thay thế.

Accounting là:

> Financial Source of Truth.

Các module phía trên đọc dữ liệu từ Accounting và operational systems.

---

# 6. High-Level Architecture

```text
                         USER
                           │
        ┌──────────────────┼──────────────────┐
        │                  │                  │
     Owner              Manager           Accountant
        │                  │                  │
        └──────────────────┼──────────────────┘
                           ▼
                 VẠN AN BUSINESS OS
                           │
        ┌──────────────────┼───────────────────┐
        │                  │                   │
        ▼                  ▼                   ▼
   OPERATIONS          ACCOUNTING         INTELLIGENCE
        │                  │                   │
   Sales/POS              GL              Financial
   Inventory              AR              Planning
   Recipe                 AP              Forecast
   Purchase               Cash            Scenario
   Customer               Bank            Advisor
   Loyalty                Assets
                          Tax
        │                  │                   │
        └──────────────────┼───────────────────┘
                           ▼
                       COREHUB
                           │
             ┌─────────────┼─────────────┐
             ▼             ▼             ▼
          eInvoice       Payment        Bank
```

---

# 7. Functional Domains

## DOMAIN-01 — Business Profile

### UC-BP-001 Create Business Profile

Actor:

- Business Owner
- System Admin

Input:

- Business type
- Industry
- Store
- Operating hours
- Products/services
- Employees
- Fixed costs
- Expected revenue
- Capital

Output:

`BusinessProfile`

---

### UC-BP-002 Configure Business Model

Owner khai báo:

- Revenue model
- Pricing model
- Cost model
- Capacity
- Product mix
- Service mix

---

# 8. DOMAIN-02 — Sales & Operations

## UC-SALES-001 Create Order

Actor:

- Cashier
- Sales
- Customer

Output:

- Order
- Revenue transaction
- Payment

---

## UC-SALES-002 Close Shift

Actor:

- Cashier
- Manager

Output:

- Shift report
- Cash reconciliation
- Sales summary

---

## UC-INV-001 Receive Inventory

Actor:

- Inventory Manager

Output:

- Stock increase
- AP / payable event
- Inventory valuation

---

## UC-INV-002 Consume Inventory

Trigger:

- Sales
- Production
- Manual issue

Output:

- Stock decrease
- COGS impact

---

## UC-INV-003 Detect Inventory Variance

System compares:

```text
Expected Consumption
vs
Actual Consumption
```

Output:

- Variance
- Waste alert
- Loss alert

---

# 9. DOMAIN-03 — Accounting

## UC-ACC-001 Post Transaction

Trigger:

- Sales
- Purchase
- Payment
- Expense
- Inventory
- Asset
- Adjustment

Output:

`JournalEntry`

---

## UC-ACC-002 Manage AR

Actor:

- Accountant

Functions:

- Create receivable
- Record collection
- Aging
- Overdue detection

---

## UC-ACC-003 Manage AP

Functions:

- Supplier payable
- Payment
- Aging
- Due-date alert

---

## UC-ACC-004 Cash Management

Functions:

- Cash balance
- Cash inflow
- Cash outflow
- Bank reconciliation

---

## UC-ACC-005 Financial Statements

Generate:

- Income Statement
- Balance Sheet
- Cash Flow
- Trial Balance
- General Ledger

---

# 10. DOMAIN-04 — Financial Management

## UC-FM-001 Calculate Gross Profit

System calculates:

```text
Revenue
- COGS
= Gross Profit
```

Metrics:

- Gross Margin
- Product Margin
- Category Margin
- Store Margin

---

## UC-FM-002 Calculate Operating Profit

```text
Gross Profit
- Operating Expenses
= Operating Profit
```

---

## UC-FM-003 Calculate Break-even

Input:

- Fixed Cost
- Variable Cost
- Selling Price
- Product Mix

Output:

- Break-even Revenue
- Break-even Volume
- Margin of Safety

---

## UC-FM-004 Calculate Target Profit

Input:

`Target Profit`

System calculates required:

- Revenue
- Volume
- Price
- Margin

---

## UC-FM-005 Analyze Unit Economics

For each product/service:

```text
Selling Price
- Variable Cost
= Contribution Margin
```

System ranks products according to:

- Revenue
- Margin
- Contribution
- Volume
- Profit contribution

---

# 11. DOMAIN-05 — Business Planning

## UC-PLAN-001 Create Business Plan

Actor:

- Business Owner

Input:

- Planning period
- Revenue target
- Profit target
- Expense budget
- Volume target
- Pricing
- Investment

Output:

`BusinessPlan`

---

## UC-PLAN-002 Create Budget

Budget categories:

- Revenue
- COGS
- Payroll
- Rent
- Utilities
- Marketing
- Logistics
- Other OPEX
- CAPEX

---

## UC-PLAN-003 Plan Target Profit

Example:

```text
Target Profit = 50M/month
```

System determines:

```text
Required Revenue
Required Volume
Maximum Cost
Required Margin
```

---

# 12. DOMAIN-06 — Forecasting

## UC-FC-001 Revenue Forecast

Input:

- Historical revenue
- Current sales
- Seasonality
- Trend
- Business plan

Output:

`RevenueForecast`

---

## UC-FC-002 Profit Forecast

```text
Forecast Revenue
- Forecast COGS
- Forecast OPEX
= Forecast Profit
```

---

## UC-FC-003 Cashflow Forecast

```text
Opening Cash
+ Forecast Inflow
- Forecast Outflow
= Forecast Closing Cash
```

System detects:

- Cash shortage
- Liquidity risk
- Payment pressure

---

## UC-FC-004 Forecast Plan Achievement

System compares:

```text
Actual
vs
Plan
vs
Forecast
```

Example:

```text
Plan Revenue       500M
Actual to date     180M
Forecast           435M

Expected Gap       -65M
```

---

# 13. DOMAIN-07 — Scenario Simulation

## UC-SC-001 Simulate Price Change

Input:

`Price + X%`

System estimates:

- Revenue
- Volume impact
- Gross profit
- Net profit

---

## UC-SC-002 Simulate Volume Change

Input:

`Volume + X%`

Output:

- Revenue
- Variable Cost
- Contribution
- Profit

---

## UC-SC-003 Simulate Cost Reduction

Input:

`COGS - X%`

Output:

- Profit improvement
- Break-even change

---

## UC-SC-004 Simulate Expansion

Input:

- New rent
- New employees
- CAPEX
- Expected volume

Output:

- New break-even
- Cash requirement
- Profit forecast
- Payback estimate

---

## UC-SC-005 Simulate Contraction

System estimates:

- Cost reduction
- Revenue loss
- Profit change
- Cash improvement

---

## UC-SC-006 Compare Scenarios

Owner can compare:

```text
Current
Scenario A
Scenario B
Scenario C
```

Output:

| Metric | Current | A | B | C |
|---|---:|---:|---:|---:|
| Revenue | | | | |
| COGS | | | | |
| Gross Profit | | | | |
| OPEX | | | | |
| Net Profit | | | | |
| Cash | | | | |
| Break-even | | | | |

---

# 14. DOMAIN-08 — Management Advisor

## UC-ADV-001 Detect Problem

Engine analyzes:

- Revenue
- Margin
- COGS
- Expenses
- Inventory
- Cash
- AR
- AP
- Debt
- Working Capital

Output:

`BusinessAlert`

---

## UC-ADV-002 Explain Problem

Không chỉ báo:

> "Lợi nhuận giảm."

Mà phải giải thích:

```text
Profit ↓ 18%

Caused by:
COGS       +9%
Revenue    -5%
OPEX       +4%
```

---

## UC-ADV-003 Generate Recommendation

Example:

```text
Problem:
COGS tăng 8%.

Possible actions:
1. Supplier renegotiation
2. Recipe optimization
3. Reduce waste
4. Increase price
5. Remove low-margin products
```

---

## UC-ADV-004 Recommend Best Action

Engine đánh giá:

```text
Expected Impact
Risk
Cost
Time
Feasibility
```

Sau đó xếp hạng:

```text
1. Reduce waste      HIGH IMPACT / LOW RISK
2. Renegotiate COGS  HIGH IMPACT / MEDIUM RISK
3. Increase price    MEDIUM IMPACT / MEDIUM RISK
```

---

## UC-ADV-005 Expansion Recommendation

System determines:

```text
Capacity
Demand
Margin
Cash
Debt
Working Capital
```

Output:

- Expand
- Maintain
- Optimize
- Contract

---

# 15. DOMAIN-09 — Management Dashboard

## UC-DASH-001 Financial Health

Dashboard:

```text
Revenue
Profit
Margin
Cash
AR
AP
Inventory
Debt
Break-even
```

---

## UC-DASH-002 Revenue Dashboard

Chart:

- Daily
- Weekly
- Monthly
- Actual
- Plan
- Forecast

---

## UC-DASH-003 Cashflow Dashboard

```text
Opening Cash
     +
Cash In
     -
Cash Out
     =
Closing Cash
```

---

## UC-DASH-004 Asset Dashboard

Theo dõi:

- Cash
- Inventory
- Fixed Assets
- Other Assets
- Total Assets

---

## UC-DASH-005 Liability Dashboard

Theo dõi:

- Supplier debt
- Tax payable
- Loans
- Other liabilities

---

## UC-DASH-006 Receivable Dashboard

Theo dõi:

- Total AR
- Current
- Overdue
- Aging
- Collection rate

---

# 16. DOMAIN-10 — Management KPI

Các KPI lõi:

### Revenue

- Revenue
- Revenue Growth
- Revenue per Day
- Revenue per Employee

### Profit

- Gross Profit
- Gross Margin
- Operating Profit
- Net Profit
- Net Margin

### Cost

- COGS %
- OPEX %
- Payroll %
- Rent %
- Marketing %

### Liquidity

- Cash
- Cashflow
- Current Ratio
- Working Capital

### Efficiency

- Inventory Turnover
- AR Days
- AP Days
- Cash Conversion Cycle

### Break-even

- Break-even Revenue
- Break-even Volume
- Margin of Safety

---

# 17. Cross-Domain Use Cases

## UC-CROSS-001 Sales → Accounting

```text
Order
 ↓
Payment
 ↓
Revenue
 ↓
Journal
```

---

## UC-CROSS-002 Sales → Inventory → COGS → Accounting

```text
Sale
 ↓
Recipe/BOM
 ↓
Inventory Consumption
 ↓
COGS
 ↓
Accounting
```

---

## UC-CROSS-003 Accounting → Financial Management

```text
Ledger
 ↓
Financial Model
 ↓
Profitability
 ↓
Break-even
```

---

## UC-CROSS-004 Financial Management → Forecast

```text
Actual
 ↓
Trend
 ↓
Forecast
 ↓
Plan Variance
```

---

## UC-CROSS-005 Forecast → Advisor

```text
Forecast
 ↓
Risk Detection
 ↓
Scenario
 ↓
Recommendation
```

---

## UC-CROSS-006 Advisor → Owner

```text
Alert
 ↓
Explanation
 ↓
Options
 ↓
Recommendation
 ↓
Owner Decision
```

---

# 18. End-to-End Use Case

## UC-E2E-001 — "Tôi muốn biết tháng này có lời không?"

Actor:

Business Owner

Flow:

```text
Sales
   ↓
Inventory / COGS
   ↓
Accounting
   ↓
P&L
   ↓
Financial Engine
   ↓
Profit Analysis
   ↓
Forecast
```

Output:

> Doanh thu hiện tại: 420M  
> Forecast: 465M  
> Forecast Profit: 38M  
> Target Profit: 50M  
> Expected Gap: -12M

Advisor:

> Có khả năng không đạt mục tiêu 12M.

---

# 19. UC-E2E-002 — "Muốn lời thêm 20 triệu phải làm gì?"

Actor:

Business Owner

Input:

`Target Profit +20M`

System generates scenarios:

```text
A. Increase volume
B. Increase price
C. Reduce COGS
D. Reduce OPEX
E. Product mix optimization
```

System calculates impact.

Output:

```text
Recommended:

Reduce COGS 3%
+
Increase high-margin product mix 8%

Expected additional profit:
+21M
```

---

# 20. UC-E2E-003 — "Có nên mở rộng?"

System checks:

```text
Current Revenue
Current Profit
Capacity
Demand
Margin
Cash
Debt
Working Capital
```

Then runs expansion scenario.

Output:

```text
Expansion Investment       300M
Additional Fixed Cost       45M/month
Expected Revenue           +120M/month
Expected Profit             +22M/month
Payback                     14 months

Recommendation:
CONDITIONAL GO
```

---

# 21. Event-driven Architecture

Các event quan trọng:

```text
OrderCreated
PaymentReceived
OrderCompleted

PurchaseCreated
InventoryReceived
InventoryConsumed
InventoryAdjusted
WasteRecorded

ExpenseRecorded
JournalPosted

InvoiceCreated
InvoicePaid

ARCreated
ARCollected

APCreated
APPaid

PeriodClosed
```

Financial Intelligence Engine subscribe các event cần thiết.

---

# 22. Data Flow

```text
                OPERATIONAL DATA
                       │
          ┌────────────┼────────────┐
          ▼            ▼            ▼
        Sales       Inventory     Purchase
          │            │            │
          └────────────┼────────────┘
                       ▼
                  ACCOUNTING
                       │
       ┌───────────────┼────────────────┐
       ▼               ▼                ▼
      P&L          Balance Sheet     Cashflow
       │               │                │
       └───────────────┼────────────────┘
                       ▼
             FINANCIAL MODEL
                       │
          ┌────────────┼────────────┐
          ▼            ▼            ▼
      Break-even    Forecast      KPI
          │            │            │
          └────────────┼────────────┘
                       ▼
                  SCENARIO ENGINE
                       │
                       ▼
                ADVISORY ENGINE
                       │
                       ▼
                 OWNER DECISION
```

---

# 23. Layered Architecture

## Layer 1 — Transaction

```text
POS
Sales
Purchase
Inventory
Customer
Payment
```

## Layer 2 — Accounting

```text
GL
AR
AP
Cash
Bank
Assets
Tax
```

## Layer 3 — Financial Intelligence

```text
Profitability
Break-even
Unit Economics
Working Capital
Financial Health
```

## Layer 4 — Planning

```text
Budget
Target
Forecast
Scenario
```

## Layer 5 — Advisory

```text
Detection
Explanation
Recommendation
Decision Support
```

---

# 24. Technology Architecture

Phù hợp với kiến trúc Vạn An hiện tại:

```text
                 KhachLink
                     │
                 ShopERP
                     │
                     ▼
                  Gateway
                     │
        ┌────────────┼────────────┐
        ▼            ▼            ▼
   Order Service  Accounting   Inventory
                     │
                     ▼
                  CoreHub
                     │
        ┌────────────┼────────────┐
        ▼            ▼            ▼
    PostgreSQL    Event Bus    Reporting
                     │
                     ▼
       Financial Intelligence Engine
                     │
        ┌────────────┼────────────┐
        ▼            ▼            ▼
     Rules       Forecasting    Scenario
        │            │            │
        └────────────┼────────────┘
                     ▼
               Advisor Engine
```

---

# 25. Core Services

Đề xuất tách:

```text
VanAn.Accounting
VanAn.Inventory
VanAn.Sales
VanAn.FinancialManagement
VanAn.BusinessPlanning
VanAn.Forecasting
VanAn.ScenarioEngine
VanAn.ManagementAdvisor
VanAn.Reporting
```

Không nên để toàn bộ logic trong Accounting.

Accounting cung cấp financial truth.

Financial Management consume financial truth.

---

# 26. Multi-Tenant Model

Mọi financial intelligence đều phải Tenant-scoped.

```text
TenantId
   │
   ├── BusinessProfile
   ├── Accounting
   ├── Sales
   ├── Inventory
   ├── Plans
   ├── Forecasts
   ├── Scenarios
   └── Recommendations
```

Không được phép cross-tenant data leakage.

---

# 27. Advisory Trust Model

Hệ thống phải phân biệt rõ:

### Level 1 — Deterministic

Dựa trên công thức/kế toán:

> Break-even = ...

Độ tin cậy cao.

### Level 2 — Rule-based

> COGS tăng > 10% → cảnh báo.

### Level 3 — Statistical Forecast

> Dự báo doanh thu tháng tới = ...

### Level 4 — AI Advisory

> Đề xuất hành động.

AI không được tự sửa số liệu kế toán.

AI không được tự ghi journal.

AI chỉ được:

```text
Analyze
Explain
Recommend
```

trừ khi Owner explicitly authorizes một workflow tự động đã được kiểm soát.

---

# 28. Business Rules

## BR-001

Accounting là nguồn dữ liệu tài chính chuẩn.

## BR-002

Financial Intelligence không được thay đổi historical accounting data.

## BR-003

Forecast không thay đổi Actual.

## BR-004

Scenario không ảnh hưởng dữ liệu thật.

## BR-005

Recommendation phải truy ngược được nguồn dữ liệu.

Ví dụ:

```text
Recommendation
 ↓
Reason
 ↓
KPI
 ↓
Accounting transaction
```

## BR-006

Mọi calculation phải có version.

Ví dụ:

```text
FinancialModelVersion = 1.0
```

## BR-007

Mọi forecast phải có timestamp.

## BR-008

Mọi recommendation phải có confidence level.

---

# 29. MVP Scope

Không nên xây toàn bộ ngay.

## MVP-1

```text
Accounting
+
Sales
+
Inventory
+
Cash
+
AR/AP
```

## MVP-2

```text
P&L
Cashflow
Break-even
Unit Economics
Financial Dashboard
```

## MVP-3

```text
Business Plan
Target Profit
Forecast
Actual vs Plan
```

## MVP-4

```text
Scenario Engine
```

## MVP-5

```text
Management Advisor
```

---

# 30. First Killer Use Cases

Nếu phải chọn chỉ 5 use case để chứng minh sản phẩm, chọn:

### KU-01

> **"Tháng này tôi có lời không?"**

### KU-02

> **"Điểm hòa vốn của tôi là bao nhiêu?"**

### KU-03

> **"Muốn lời 50 triệu thì phải bán bao nhiêu?"**

### KU-04

> **"Nếu tình hình hiện tại tiếp tục thì cuối tháng tôi lời hay lỗ?"**

### KU-05

> **"Tôi nên tăng giá, tăng doanh số hay giảm chi phí?"**

Đây là 5 câu hỏi mà chủ hộ kinh doanh/SME có thể hiểu ngay giá trị.

---

# 31. Business Value Proposition

Vạn An không bán:

> Accounting software.

Vạn An bán:

> **Business Control & Decision System.**

Thông điệp sản phẩm:

```text
Bán hàng → Biết doanh thu
Kế toán  → Biết lời lỗ
Vạn An   → Biết phải làm gì tiếp theo
```

Hoặc:

> **"Vạn An giúp chủ doanh nghiệp nhỏ nhìn thấy tiền, hiểu lợi nhuận, dự báo tương lai và biết nên hành động thế nào."**

---

# 32. Strategic Architecture

Đây là kiến trúc chiến lược cuối cùng:

```text
                    VẠN AN
                       │
              LOCAL BUSINESS OS
                       │
       ┌───────────────┼────────────────┐
       │               │                │
       ▼               ▼                ▼
      RUN            RECORD           DECIDE
       │               │                │
       ▼               ▼                ▼
   Operations       Accounting      Intelligence
       │               │                │
       │               ▼                ▼
       │          Financial Truth    Forecast
       │                                │
       │                                ▼
       │                           Simulation
       │                                │
       └────────────────────────────────┤
                                        ▼
                                  Recommendation
                                        │
                                        ▼
                                  Owner Action
                                        │
                                        ▼
                                  New Transaction
                                        │
                                        └───────→ LOOP
```

**Định hướng dài hạn:** Vạn An trở thành **Local Business Operating System cho nền kinh tế tầng thấp**, trong đó Accounting là **financial backbone**, còn Financial Management + Forecasting + Advisory là **decision layer**. 