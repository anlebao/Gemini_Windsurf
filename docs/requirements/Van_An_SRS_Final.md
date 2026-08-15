# VẠN AN LOCAL BUSINESS OS
## Software Requirements Specification (SRS) — Final

**Version:** 2.0 (Unified)
**Status:** Ready for AI Code Generation (Vibe Coding / Agentic Workflow)
**Target:** Hộ kinh doanh siêu nhỏ, SME nhỏ, Café, Barbershop/Hair Salon, Spa, Nail, Nhà hàng, Quán ăn, Retail và dịch vụ địa phương.
**Stack:** .NET 8 — EF Core — SQLite (per-tenant Business) + PostgreSQL (Accounting) — Blazor Server (ShopERP) — Blazor WebAssembly (KhachLink PWA) — SignalR — YARP Gateway — NATS — xUnit — Playwright.
**Architecture:** Clean Architecture + DDD + Multi-tenancy. Data flow (Option C, approved 2026-07-18): `KhachLink WASM (5002) → Gateway (5001, order creator, PG source of truth) → NATS (routed by ShopInstanceId) → ShopERP-A/B/C... (per-tenant SQLite)`.

> **Tài liệu này là sự gộp của:**
> 1. `docs/specs/Vạn An Local Business OS — Financial Management & Business Intelligence Specification.md` (vision, positioning, business domains, advisory trust model)
> 2. `docs/requirements/Van_An_SRS_Inventory_Intelligence_Engine.md` (chi tiết kỹ thuật Shift Report, Recipe/BOM, Variance Analysis, Alert Engine)
>
> **Đồng thời sửa các mâu thuẫn đã phát hiện trong review:**
> - Diagram §24/25 cũ sai flow Option C → đã sửa
> - Thiếu UCs cho Recipe/BOM, Tax/eInvoice, Period Closing → đã bổ sung
> - Đề xuất 9 services `.csproj` mới vi phạm governance → đã chuyển thành logical modules trong CoreHub + ShopERP
> - Công thức break-even đơn sản phẩm → đã sửa thành weighted contribution margin
> - Thiếu NFRs → đã bổ sung

---

# PHẦN I — TẦM NHÌN & ĐỊNH VỊ

## 1. Vision

Vạn An Local Business OS là nền tảng vận hành và quản trị dành cho nhóm doanh nghiệp kinh tế tầng thấp, kết hợp:

- Sales / POS
- Inventory
- Recipe / BOM
- Customer
- Loyalty
- Accounting
- Cash Management
- AR/AP
- Tax / eInvoice
- Financial Management
- Business Planning
- Forecasting
- Scenario Simulation
- Management Advisory
- Inventory Intelligence Engine (Shift Report + Variance Analysis)

Mục tiêu không phải chỉ là:

> "Ghi nhận doanh nghiệp đã làm gì."

Mà tiến tới:

> "Hiểu doanh nghiệp đang ở đâu, dự báo sẽ đi về đâu và đề xuất chủ doanh nghiệp nên làm gì."

## 2. Product Positioning

### 2.1 Traditional Accounting

```text
Transaction → Accounting → Report
```

Hệ thống chủ yếu trả lời: Đã bán bao nhiêu? Có bao nhiêu tiền? Có bao nhiêu tài sản? Nợ bao nhiêu? Lợi nhuận bao nhiêu?

### 2.2 Vạn An Business OS

```text
Transaction → Accounting → Financial Model → Analysis → Forecast
            → Scenario → Recommendation → Business Action → New Transaction
```

Hệ thống trả lời thêm:

- Tại sao lợi nhuận giảm?
- Điểm hòa vốn ở đâu?
- Nếu doanh thu giảm 10% thì sao?
- Muốn lời thêm 20 triệu phải làm gì?
- Có nên tăng giá / giảm chi phí / mở rộng / thu hẹp?
- Có nguy cơ thiếu tiền không?
- Sản phẩm nào đang làm giảm lợi nhuận?
- Hao hụt nguyên liệu ca này bao nhiêu? Có thất thoát không?

## 3. Target Actors

| ID | Actor | Quyền chính |
|---|---|---|
| ACT-01 | Business Owner | Dashboard, financial view, mục tiêu, kế hoạch, scenario, cảnh báo, recommendation, phê duyệt hành động, cấu hình ngưỡng cảnh báo |
| ACT-02 | Store Manager | Sales, inventory, purchase, staff operation, daily closing, operational dashboard, cost control, resolve alert |
| ACT-03 | Accountant | Accounting, journal, ledger, AR/AP, cash, bank, tax, eInvoice, reconciliation, financial statements, period closing |
| ACT-04 | Cashier / Sales Staff | Create order, receive payment, refund, close shift, nhập kiểm kê, view assigned operational data |
| ACT-05 | Inventory Manager | Receive goods, issue goods, stock count, waste, adjustment, purchase, recipe/BOM, inventory analysis |
| ACT-06 | Financial Advisor | Rule Engine / Financial Intelligence Engine / AI Advisor / Human advisor — Detect, Explain, Forecast, Simulate, Recommend |
| ACT-07 | System Administrator | Tenant, user, role, configuration, provider, integration, system policy |
| ACT-08 | External Provider | POS, eInvoice, Payment, Bank, Tax, Accounting provider, Delivery provider |

## 4. Core Domain Model

```text
Tenant
│
├── BusinessProfile
├── Users
├── Stores / ShopInstances
├── Products / Services
├── Customers
├── Suppliers
├── Sales (Orders, OrderItems, Payments)
├── Purchases
├── Inventory (Ingredients, StockMovements)
├── Recipe / BOM (Recipe, RecipeLine, Versions)
├── Shifts (Shift, InventoryCount, ShiftAlert, TheoreticalConsumption)
├── Accounting
│   ├── GL (JournalEntry, AccountingEntry — immutable)
│   ├── AR / AP
│   ├── Cash / Bank
│   ├── Assets
│   └── Tax / eInvoice (ElectronicInvoice, PendingInvoiceQueue)
├── FinancialManagement (Profitability, Break-even, Unit Economics)
├── BusinessPlans (Budget, Target)
├── Forecasts (Revenue, Profit, Cashflow, Restock, Stockout)
├── Scenarios (PriceChange, VolumeChange, CostReduction, Expansion, Contraction)
└── Recommendations (BusinessAlert, AdvisoryAction)
```

---

# PHẦN II — YÊU CẦU NGHIỆP VỤ (FUNCTIONAL REQUIREMENTS)

## 5. Architectural Principle

### 5.1 Accounting is Financial Source of Truth

Accounting không bị thay thế. Accounting là:

> Financial Source of Truth.

Các module phía trên đọc dữ liệu từ Accounting và operational systems. `AccountingEntry` 100% immutable — sửa số liệu chỉ qua Reversal Entry.

### 5.2 Layered Architecture (Clean Architecture + DDD)

```text
API/Presentation (5_WebApps) → Services (3_CoreHub) → Domain (1_Shared)
                                  ↓
                            Infrastructure (3_CoreHub) → Domain
```

- Domain layer PURE: NO EF Core, NO DbContext, NO DataAnnotations
- Single Source of Truth: `1_Shared/Domain.cs`
- Multi-tenancy enforced at every layer (`TenantId` filter)
- Single-Identity Pattern: mọi entity dùng `BaseEntity.Id` (PK) làm identity duy nhất; business key VO bị `Ignore` trong EF config; constructor set `Id = BusinessKey.Value`

### 5.3 High-Level Architecture (Option C — corrected)

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
   Sales/POS              GL              Financial Planning
   Inventory              AR              Forecast
   Recipe/BOM             AP              Scenario
   Shift Report           Cash            Advisor
   Purchase               Bank            Inventory Intelligence
   Customer               Assets
   Loyalty                Tax/eInvoice
        │                  │                   │
        └──────────────────┼───────────────────┘
                           ▼
                       COREHUB
                           │
             ┌─────────────┼─────────────┐
             ▼             ▼             ▼
          eInvoice       Payment        Bank
```

### 5.4 Technology Architecture (Option C — corrected)

```text
KhachLink (WASM, 5002)
    │  HTTP only — NO direct DB access
    ▼
Gateway (5001)  ← Order Creator + PG source of truth
    │  Orders + Accounting + Tenants + ShopInstances + Users + FeaturedProducts
    │  YARP forward cho select traffic (static assets, PWA, catalog browse)
    ▼
NATS  ← routed by ShopInstanceId
    │  Subject: vanan.cloud.order.created.{shopInstId}
    ▼
ShopERP-A / B / C ... (per-tenant SQLite, Blazor Server)
    │  POS, Inventory, Recipe, Shift Report, Kitchen display
    │  Replica of Orders (NOT source of truth)
    ▼
CoreHub (in-process background service trong Gateway)
    │  Accounting, Financial Intelligence Engine, Scenario Engine, Advisor Engine
    ▼
PostgreSQL (Accounting) + Event Bus (NATS) + Reporting
```

**Modules (logical, không tạo .csproj mới):**

| Module | Vai trò | Project host |
|---|---|---|
| VanAn.Accounting | GL, AR/AP, Cash, Tax, eInvoice, Financial Statements | 3_CoreHub (services) + 5_WebApps/ShopERP (UI) |
| VanAn.Inventory | Ingredients, StockMovements, Variance | 3_CoreHub + ShopERP |
| VanAn.Sales | Orders, POS, Kitchen | 3_CoreHub + ShopERP |
| VanAn.Recipe | Recipe/BOM CRUD + versioning | 3_CoreHub + ShopERP |
| VanAn.ShiftReport | Shift, InventoryCount, ShiftAlert, TheoreticalConsumption | 3_CoreHub + ShopERP |
| VanAn.FinancialManagement | Profitability, Break-even, Unit Economics | 3_CoreHub |
| VanAn.BusinessPlanning | Budget, Target | 3_CoreHub |
| VanAn.Forecasting | Revenue/Profit/Cashflow/Restock/Stockout forecast | 3_CoreHub |
| VanAn.ScenarioEngine | What-if simulation (no opinion) | 3_CoreHub |
| VanAn.ManagementAdvisor | Ranks scenarios, emits recommendation | 3_CoreHub |
| VanAn.Reporting | Export PDF/Excel, dashboards | 3_CoreHub + ShopERP |

> **HARD STOP:** KHÔNG tạo mới `.csproj` (vd `VanAn.CoreHub.Api`). Tất cả modules là namespace/folder trong `3_CoreHub` (class library) và `5_WebApps/ShopERP` (Web API host + Blazor Server).

---

## 6. Functional Domains

### DOMAIN-01 — Business Profile

#### UC-BP-001 Create Business Profile
- **Actor:** Business Owner, System Admin
- **Input:** Business type, industry, store, operating hours, products/services, employees, fixed costs, expected revenue, capital
- **Output:** `BusinessProfile`

#### UC-BP-002 Configure Business Model
- **Actor:** Business Owner
- **Input:** Revenue model, pricing model, cost model, capacity, product mix, service mix

### DOMAIN-02 — Sales & Operations

#### UC-SALES-001 Create Order
- **Actor:** Cashier, Sales, Customer
- **Output:** Order, Revenue transaction, Payment
- **Flow:** KhachLink → Gateway (order creator, PG) → NATS routed → ShopERP (kitchen/POS display)

#### UC-SALES-002 Close Shift
- **Actor:** Cashier, Manager
- **Output:** Shift report, Cash reconciliation, Sales summary
- **Detail:** xem DOMAIN-11 Shift Report

#### UC-INV-001 Receive Inventory
- **Actor:** Inventory Manager
- **Output:** Stock increase, AP/payable event, Inventory valuation

#### UC-INV-002 Consume Inventory
- **Trigger:** Sales, Production, Manual issue
- **Output:** Stock decrease, COGS impact

#### UC-INV-003 Detect Inventory Variance
- **System compares:** Expected Consumption vs Actual Consumption
- **Output:** Variance, Waste alert, Loss alert
- **Detail:** xem DOMAIN-11 Inventory Intelligence Engine

### DOMAIN-03 — Accounting

#### UC-ACC-001 Post Transaction
- **Trigger:** Sales, Purchase, Payment, Expense, Inventory, Asset, Adjustment
- **Output:** `JournalEntry` (chứa `AccountingEntry` immutable)
- **Rule:** `AccountingEntry` 100% immutable — sửa chỉ qua Reversal Entry

#### UC-ACC-002 Manage AR
- **Actor:** Accountant
- **Functions:** Create receivable, Record collection, Aging, Overdue detection

#### UC-ACC-003 Manage AP
- **Functions:** Supplier payable, Payment, Aging, Due-date alert

#### UC-ACC-004 Cash Management
- **Functions:** Cash balance, Cash inflow, Cash outflow, Bank reconciliation

#### UC-ACC-005 Financial Statements
- **Generate:** Income Statement, Balance Sheet, Cash Flow, Trial Balance, General Ledger

#### UC-ACC-006 Close Period
- **Actor:** Accountant
- **Functions:** Lock period (no new entries), Audit trail, Status: Open → Closing → Closed
- **Rule:** Period Closed → `AccountingEntry` trong period không thể tạo/sửa (trừ Reversal Entry có approve)

#### UC-ACC-007 Reopen Period
- **Actor:** Accountant + Owner approval
- **Functions:** Mở lại period đã Close, ghi audit log, giới hạn thời gian reopen

### DOMAIN-04 — Tax & eInvoice (BỔ SUNG — codebase đã có)

#### UC-TAX-001 Issue eInvoice
- **Actor:** Cashier (auto) / Accountant (manual)
- **Trigger:** Order completed → sinh eInvoice qua provider (VietQr / MeInvoice / etc.)
- **Output:** `ElectronicInvoice` + PDF
- **Fallback:** Nếu provider fail → vào `PendingInvoiceQueue` retry

#### UC-TAX-002 VAT Report
- **Actor:** Accountant
- **Output:** Báo cáo VAT theo kỳ (TT 152/2025/TT-BTC cho HKD)

#### UC-TAX-003 HKD Period Declaration
- **Actor:** Accountant
- **Output:** Tờ khai thuế HKD theo quý/năm, export XML/PDF
- **Rule:** Tuân thủ TT 152/2025/TT-BTC — hệ thống sách kế toán HKD

#### UC-TAX-004 eInvoice Health Monitoring
- **Actor:** Accountant, System Admin
- **Output:** Dashboard trạng thái provider, alert nếu provider down, retry queue status

### DOMAIN-05 — Financial Management

#### UC-FM-001 Calculate Gross Profit
- **Formula:** `Revenue − COGS = Gross Profit`
- **Metrics:** Gross Margin, Product Margin, Category Margin, Store Margin

#### UC-FM-002 Calculate Operating Profit
- **Formula:** `Gross Profit − Operating Expenses = Operating Profit`

#### UC-FM-003 Calculate Break-even (multi-product — corrected)
- **Input:** Fixed Cost, Variable Cost per unit, Selling Price per unit, Product Mix (% revenue/volume)
- **Formula (weighted contribution margin):**
  ```text
  WACM = Σ (CMi × Mixi)   // CMi = Pricei − VarCosti, Mixi = % volume
  Break-even Revenue = Fixed Cost / WACM
  Break-even Volume = Fixed Cost / WACM (per mix unit)
  Margin of Safety = (Actual Revenue − Break-even Revenue) / Actual Revenue
  ```
- **Output:** Break-even Revenue, Break-even Volume, Margin of Safety

#### UC-FM-004 Calculate Target Profit
- **Input:** `Target Profit`
- **Output:** Required Revenue, Volume, Price, Margin

#### UC-FM-005 Analyze Unit Economics
- **Per product/service:** `Selling Price − Variable Cost = Contribution Margin`
- **Rank by:** Revenue, Margin, Contribution, Volume, Profit contribution

### DOMAIN-06 — Business Planning

#### UC-PLAN-001 Create Business Plan
- **Actor:** Business Owner
- **Input:** Planning period, Revenue target, Profit target, Expense budget, Volume target, Pricing, Investment
- **Output:** `BusinessPlan`

#### UC-PLAN-002 Create Budget
- **Categories:** Revenue, COGS, Payroll, Rent, Utilities, Marketing, Logistics, Other OPEX, CAPEX

#### UC-PLAN-003 Plan Target Profit
- **Input:** `Target Profit = 50M/month`
- **Output:** Required Revenue, Required Volume, Maximum Cost, Required Margin

### DOMAIN-07 — Forecasting

#### UC-FC-001 Revenue Forecast
- **Input:** Historical revenue, Current sales, Seasonality, Trend, Business plan
- **Output:** `RevenueForecast`

#### UC-FC-002 Profit Forecast
- **Formula:** `Forecast Revenue − Forecast COGS − Forecast OPEX = Forecast Profit`

#### UC-FC-003 Cashflow Forecast
- **Formula:** `Opening Cash + Forecast Inflow − Forecast Outflow = Forecast Closing Cash`
- **Detect:** Cash shortage, Liquidity risk, Payment pressure

#### UC-FC-004 Forecast Plan Achievement
- **Compare:** Actual vs Plan vs Forecast
- **Example:** Plan 500M / Actual 180M / Forecast 435M → Gap −65M

#### UC-FC-005 Restock Forecast (từ VA-IIE)
- **Formula:** `Average Daily Consumption × Lead Time → suggested restock qty`
- **Window:** Rolling 14 ngày (configurable)

#### UC-FC-006 Stockout Forecast (từ VA-IIE)
- **Formula:** `CurrentStock ÷ Average Daily Consumption = days remaining`
- **Alert:** Nếu days < Lead Time + Safety Days

### DOMAIN-08 — Scenario Simulation

> **Contract:** Scenario = "what-if calculator (NO opinion)". Advisor (DOMAIN-10) consumes Scenario outputs để rank.

#### UC-SC-001 Simulate Price Change
- **Input:** `Price + X%`
- **Output:** Revenue, Volume impact, Gross profit, Net profit

#### UC-SC-002 Simulate Volume Change
- **Input:** `Volume + X%`
- **Output:** Revenue, Variable Cost, Contribution, Profit

#### UC-SC-003 Simulate Cost Reduction
- **Input:** `COGS - X%`
- **Output:** Profit improvement, Break-even change

#### UC-SC-004 Simulate Expansion
- **Input:** New rent, New employees, CAPEX, Expected volume
- **Output:** New break-even, Cash requirement, Profit forecast, Payback estimate

#### UC-SC-005 Simulate Contraction
- **Output:** Cost reduction, Revenue loss, Profit change, Cash improvement

#### UC-SC-006 Compare Scenarios
- **Output table:** Metric × {Current, A, B, C} — Revenue, COGS, Gross Profit, OPEX, Net Profit, Cash, Break-even

### DOMAIN-09 — Management Dashboard

#### UC-DASH-001 Financial Health
- **Widgets:** Revenue, Profit, Margin, Cash, AR, AP, Inventory, Debt, Break-even

#### UC-DASH-002 Revenue Dashboard
- **Chart:** Daily/Weekly/Monthly × Actual/Plan/Forecast

#### UC-DASH-003 Cashflow Dashboard
- **Formula:** `Opening Cash + Cash In − Cash Out = Closing Cash`

#### UC-DASH-004 Asset Dashboard
- **Track:** Cash, Inventory, Fixed Assets, Other Assets, Total Assets

#### UC-DASH-005 Liability Dashboard
- **Track:** Supplier debt, Tax payable, Loans, Other liabilities

#### UC-DASH-006 Receivable Dashboard
- **Track:** Total AR, Current, Overdue, Aging, Collection rate

### DOMAIN-10 — Management KPI

| Group | KPIs |
|---|---|
| Revenue | Revenue, Revenue Growth, Revenue per Day, Revenue per Employee |
| Profit | Gross Profit, Gross Margin, Operating Profit, Net Profit, Net Margin |
| Cost | COGS %, OPEX %, Payroll %, Rent %, Marketing % |
| Liquidity | Cash, Cashflow, Current Ratio, Working Capital |
| Efficiency | Inventory Turnover, **Inventory Days (DIO)**, AR Days, AP Days, Cash Conversion Cycle |
| Break-even | Break-even Revenue, Break-even Volume, Margin of Safety |

> **Bổ sung Inventory Days (DIO)** — cần thiết cho Cash Conversion Cycle (CCC = DIO + AR Days − AP Days).

### DOMAIN-11 — Management Advisor

#### UC-ADV-001 Detect Problem
- **Analyze:** Revenue, Margin, COGS, Expenses, Inventory, Cash, AR, AP, Debt, Working Capital
- **Output:** `BusinessAlert`

#### UC-ADV-002 Explain Problem
- **Không chỉ báo:** "Lợi nhuận giảm."
- **Mà giải thích:**
  ```text
  Profit ↓ 18%
  Caused by: COGS +9%, Revenue -5%, OPEX +4%
  ```

#### UC-ADV-003 Generate Recommendation
- **Example:**
  ```text
  Problem: COGS tăng 8%.
  Possible actions:
  1. Supplier renegotiation
  2. Recipe optimization
  3. Reduce waste
  4. Increase price
  5. Remove low-margin products
  ```

#### UC-ADV-004 Recommend Best Action
- **Đánh giá:** Expected Impact, Risk, Cost, Time, Feasibility
- **Rank:**
  ```text
  1. Reduce waste      HIGH IMPACT / LOW RISK
  2. Renegotiate COGS  HIGH IMPACT / MEDIUM RISK
  3. Increase price    MEDIUM IMPACT / MEDIUM RISK
  ```

#### UC-ADV-005 Expansion Recommendation
- **Input:** Capacity, Demand, Margin, Cash, Debt, Working Capital
- **Output:** Expand / Maintain / Optimize / Contract

### DOMAIN-12 — Recipe / BOM (BỔ SUNG — codebase đã có entity)

#### UC-RECIPE-001 Define Recipe
- **Actor:** Inventory Manager, Owner
- **Input:** Product, danh sách nguyên liệu + định lượng, yield, waste factor
- **Output:** `Recipe` (chứa `RecipeLine[]`)

#### UC-RECIPE-002 Manage Recipe Variant
- **Input:** Size M/L, ít đường, thêm đá...
- **Output:** Recipe variant cùng Product

#### UC-RECIPE-003 Recipe Versioning
- **Rule:** Sửa định lượng → tạo version mới, giữ version cũ
- **Tiêu hao lý thuyết của Order cũ tính theo Recipe version active tại thời điểm bán

#### UC-RECIPE-004 Detect Missing Recipe
- **Trigger:** Product có bán hàng nhưng chưa định nghĩa Recipe
- **Output:** `RECIPE_MISSING` alert (Critical)

### DOMAIN-13 — Shift Report & Inventory Intelligence Engine (từ VA-IIE)

#### UC-SHIFT-001 Open Shift
- **Actor:** Cashier (ca mới)
- **Flow:**
  1. Đăng nhập ShopERP → mở ca (chọn ShiftType: Sáng/Trưa/Chiều/Tối/Full)
  2. Kiểm kê đầu ca: nhập số lượng thực tế từng nguyên liệu, vật tư, ly/nắp/ống hút
  3. Hệ thống lưu Opening InventoryCount → snapshot tồn kho đầu ca
  4. So sánh với book inventory → ghi nhận chênh lệch mở ca (nếu có)

#### UC-SHIFT-002 During Shift
- POS bán hàng tự động ghi nhận OrderItems → hệ thống tính tiêu hao lý thuyết real-time
- Nhập thêm trong ca (restocking): nhân viên ghi nhận mỗi lần nhập thêm
- Hệ thống cập nhật book inventory liên tục

#### UC-SHIFT-003 Close Shift / Handover
- **Flow:**
  1. Nhân viên cuối ca kiểm kê cuối ca
  2. Nhập tiền mặt cuối ca (cash count)
  3. Nhập ghi chú bàn giao (handover notes)
  4. Hệ thống tính: `Tiêu hao thực tế = (Tồn đầu + Nhập thêm) − Tồn cuối`
  5. Hệ thống tính tiêu hao lý thuyết từ POS sales × Recipe/BOM
  6. Hệ thống tính `Variance = Tiêu hao thực tế − Tiêu hao lý thuyết`
  7. Sinh cảnh báo nếu Variance vượt ngưỡng
  8. Tạo Shift Report hoàn chỉnh → bàn giao cho ca tiếp theo
  9. Nhân viên ca mới ACKNOWLEDGED → mở ca mới (loop về UC-SHIFT-001)

#### UC-SHIFT-004 Shift Handover State Machine
- **States:** `DRAFT → SUBMITTED → ACKNOWLEDGED → CLOSED`
- **Rule:** Ca cũ SUBMITTED → ca mới ACKNOWLEDGED → ca cũ CLOSED
- **Alert:** Ca mới không ACKNOWLEDGED trong `ShiftAckTimeoutHours` (mặc định 2h) → cảnh báo Manager

#### UC-SHIFT-005 Theoretical Consumption Calculation
```text
Cho mỗi Order trong ca:
  Cho mỗi OrderItem trong Order:
    Recipe = GetRecipeActiveAt(ProductId, Order.CreatedAt)
    Cho mỗi RecipeLine trong Recipe:
      TheoreticalConsumption[IngredientId] += Line.Quantity × OrderItem.Qty × (1 + Recipe.WasteFactor)
```

#### UC-SHIFT-006 Variance Analysis
```text
Variance = ActualConsumption − TheoreticalConsumption
  Variance > 0  → HAO HỤT (loss / waste)
  Variance < 0  → BẤT THƯỜNG (có thể sai kiểm kê / gian lận ẩn)
  Variance ≈ 0  → Bình thường
```

---

## 7. Hệ thống cảnh báo (Alert Engine)

### 7.1 Danh sách cảnh báo

| # | Mã cảnh báo | Tên | Điều kiện | Mức |
|---|---|---|---|---|
| 1 | `ING_VARIANCE_HIGH` | Hao hụt nguyên liệu | Variance > ngưỡng % (mặc định 5%) | ⚠️ Warning |
| 2 | `STOCK_LOW` | Tồn kho thấp | ClosingCount < ReorderPoint | ⚠️ Warning |
| 3 | `CONSUMPTION_OVER_LIMIT` | Tiêu hao vượt định mức | Actual > Theoretical × (1 + MaxVariance%) | 🔴 Critical |
| 4 | `SALES_HIGH_STOCK_STABLE` | Doanh số cao nhưng nguyên liệu không giảm | Sales > ngưỡng AND StockChange ≈ 0 | 🔴 Critical (gian lận) |
| 5 | `STOCK_DROP_NO_SALES` | Nguyên liệu giảm nhưng doanh số không tăng | StockChange < 0 AND Sales ≈ 0 | 🔴 Critical (thất thoát) |
| 6 | `MIDSHIFT_RESTOCK_UNUSUAL` | Nhập thêm bất thường trong ca | RestockQty > outlier ngưỡng | ⚠️ Warning |
| 7 | `CONSUMABLE_OVER_STANDARD` | Ly/nắp/ống hút vượt chuẩn | Usage > Sales × StandardRatio × (1 + Tolerance%) | ⚠️ Warning |
| 8 | `CASH_MISMATCH` | Tiền mặt chênh lệch | CashCount ≠ POS Cash Total ± Tolerance | 🔴 Critical |
| 9 | `RECIPE_MISSING` | Thiếu công thức pha chế | Product có bán hàng nhưng chưa có Recipe | 🔴 Critical |
| 10 | `SHIFT_NOT_ACKNOWLEDGED` | Ca không được xác nhận bàn giao | Shift SUBMITTED > X giờ không ACK | ⚠️ Warning |
| 11 | `CASH_SHORTAGE_FORECAST` | Dự báo thiếu tiền | Cashflow Forecast < 0 trong N ngày tới | 🔴 Critical |
| 12 | `MARGIN_DECLINE` | Margin giảm | Gross Margin giảm > X% so với kỳ trước | ⚠️ Warning |

### 7.2 Cấu hình ngưỡng (per-tenant, override per-ingredient / per-category)

| Tham số | Mặc định | Mô tả |
|---|---|---|
| `VarianceThresholdPercent` | 5% | Ngưỡng chênh lệch tiêu hao cho phép |
| `MaxVariancePercent` | 15% | Ngưỡng vượt định mức → Critical |
| `ReorderPoint` | per-item | Điểm đặt hàng lại |
| `ConsumableTolerancePercent` | 10% | Dung sai ly/nắp/ống hút |
| `CashTolerance` | 10,000 VND | Dung sai tiền mặt |
| `ShiftAckTimeoutHours` | 2h | Thời gian tối đa chờ xác nhận bàn giao |
| `ForecastWindowDays` | 14 | Rolling window cho Average Daily Consumption |

### 7.3 Kênh thông báo

| Kênh | Điều kiện |
|---|---|
| In-app (ShopERP) | Tất cả cảnh báo |
| Email | Critical + Warning (configurable) |
| Telegram/Zalo Bot | Critical (optional, per-tenant config) |
| Dashboard widget | Real-time summary |

---

## 8. Cross-Domain Use Cases

### UC-CROSS-001 Sales → Accounting
```text
Order → Payment → Revenue → Journal
```

### UC-CROSS-002 Sales → Inventory → COGS → Accounting
```text
Sale → Recipe/BOM → Inventory Consumption → COGS → Accounting
```

### UC-CROSS-003 Sales → Shift → Variance → Alert
```text
POS OrderItems → Theoretical Consumption (× Recipe) → Compare với InventoryCount cuối ca → Variance → ShiftAlert
```

### UC-CROSS-004 Accounting → Financial Management
```text
Ledger → Financial Model → Profitability → Break-even
```

### UC-CROSS-005 Financial Management → Forecast
```text
Actual → Trend → Forecast → Plan Variance
```

### UC-CROSS-006 Forecast → Advisor
```text
Forecast → Risk Detection → Scenario → Recommendation
```

### UC-CROSS-007 Advisor → Owner
```text
Alert → Explanation → Options → Recommendation → Owner Decision
```

### UC-CROSS-008 Order → eInvoice → Tax
```text
Order Completed → eInvoice issued (provider) → VAT record → HKD Period Declaration
```

---

## 9. End-to-End Use Cases

### UC-E2E-001 — "Tôi muốn biết tháng này có lời không?"
- **Actor:** Business Owner
- **Flow:** Sales → Inventory/COGS → Accounting → P&L → Financial Engine → Profit Analysis → Forecast
- **Output:**
  ```text
  Doanh thu hiện tại: 420M
  Forecast: 465M
  Forecast Profit: 38M
  Target Profit: 50M
  Expected Gap: -12M
  Advisor: Có khả năng không đạt mục tiêu 12M.
  ```

### UC-E2E-002 — "Muốn lời thêm 20 triệu phải làm gì?"
- **Actor:** Business Owner
- **Input:** `Target Profit +20M`
- **System generates scenarios:** A. Increase volume / B. Increase price / C. Reduce COGS / D. Reduce OPEX / E. Product mix optimization
- **Output:**
  ```text
  Recommended: Reduce COGS 3% + Increase high-margin product mix 8%
  Expected additional profit: +21M
  ```

### UC-E2E-003 — "Có nên mở rộng?"
- **System checks:** Current Revenue/Profit, Capacity, Demand, Margin, Cash, Debt, Working Capital
- **Output:**
  ```text
  Expansion Investment       300M
  Additional Fixed Cost       45M/month
  Expected Revenue           +120M/month
  Expected Profit             +22M/month
  Payback                     14 months
  Recommendation: CONDITIONAL GO
  ```

### UC-E2E-004 — "Ca này có thất thoát không?" (từ VA-IIE)
- **Actor:** Store Manager
- **Flow:** Đóng ca → Variance Analysis → Alert Engine
- **Output:**
  ```text
  Ca Chiều 14/08 — Staff: Nguyễn A
  Doanh số: 3.2M | Số đơn: 47
  Cà phê rang xay: Tiêu hao thực 920g, lý thuyết 846g → Variance +74g (+8.7%) ⚠️ ING_VARIANCE_HIGH
  Ly giấy 12oz: Tiêu hao thực 52 cái, lý thuyết 47 cái → Variance +5 (+10.6%) ⚠️ CONSUMABLE_OVER_STANDARD
  Tiền mặt: POS 3.2M | Cash count 3.18M | Chênh lệch -20k (trong dung sai) ✓
  Recommendation: Kiểm tra lại quy trình pha chế ca Nguyễn A.
  ```

---

# PHẦN III — KIẾN TRÚC DỮ LIỆU & SỰ KIỆN

## 10. Event-driven Architecture

### 10.1 Events quan trọng

```text
OrderCreated              → NATS: vanan.cloud.order.created.{shopInstId}
PaymentReceived
OrderCompleted

PurchaseCreated
InventoryReceived
InventoryConsumed
InventoryAdjusted
WasteRecorded

ExpenseRecorded
JournalPosted

InvoiceCreated            → eInvoice provider
InvoicePaid

ARCreated / ARCollected
APCreated / APPaid

ShiftOpened / ShiftClosed / ShiftAcknowledged
VarianceDetected
AlertRaised

PeriodClosed
```

### 10.2 Routing (Option C)

- Orders: **PG → SQLite** (routed by ShopInstanceId) — Gateway là order creator
- Order status: **SQLite → PG** (kitchen/POS status updates)
- Accounting: PG (source of truth)
- Inventory/Recipe/Shift: SQLite per-tenant (local operational data)
- Financial Intelligence Engine subscribe các event cần thiết từ PG + NATS

### 10.3 Data Flow

```text
                OPERATIONAL DATA
                       │
          ┌────────────┼────────────┐
          ▼            ▼            ▼
        Sales       Inventory     Purchase
          │            │            │
          └────────────┼────────────┘
                       ▼
                  ACCOUNTING (PG)
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

## 11. Layered Architecture (logical layers)

| Layer | Modules |
|---|---|
| Layer 1 — Transaction | POS, Sales, Purchase, Inventory, Customer, Payment, Shift |
| Layer 2 — Accounting | GL, AR, AP, Cash, Bank, Assets, Tax/eInvoice |
| Layer 3 — Financial Intelligence | Profitability, Break-even, Unit Economics, Working Capital, Financial Health, Inventory Intelligence |
| Layer 4 — Planning | Budget, Target, Forecast, Scenario |
| Layer 5 — Advisory | Detection, Explanation, Recommendation, Decision Support |

---

# PHẦN IV — MÔ HÌNH DỮ LIỆU

## 12. Domain Entities (Single-Identity Pattern)

> **HARD STOP:** Mọi entity kế thừa `BaseEntity` dùng `Id` (PK) làm identity duy nhất. Business key VO bị `Ignore` trong EF config. Constructor set `Id = BusinessKey.Value` sau `base(tenantId)`. FK references `BaseEntity.Id` (PK), NOT business key VO.

### 12.1 Entities đã có (codebase hiện tại)

| Entity | Vai trò | DB |
|---|---|---|
| `Order`, `OrderItem` | Sales | PG (source) → SQLite (replica) |
| `Product` | Catalog | SQLite per-tenant |
| `Customer` | CRM | PG |
| `JournalEntry`, `AccountingEntry` | Accounting (immutable) | PG |
| `AccountChart` | Chart of accounts | PG |
| `Ingredient` | Raw material / consumable / supply | SQLite |
| `Recipe`, `RecipeLine` | BOM | SQLite |
| `Inventory` | Stock | SQLite |
| `ElectronicInvoice`, `PendingInvoiceQueue` | eInvoice | PG |
| `PeriodClosingStatus` | Period lock | PG |
| `Tenant`, `ShopInstance`, `UserTenant`, `PermissionGroup` | Multi-tenant infra | PG |
| `OutboxMessage` | Outbox pattern | PG |

### 12.2 Entities mới (VA-IIE + Intelligence Layer)

```csharp
// === Ca làm việc ===
public class Shift : BaseEntity
{
    public ShiftId ShiftId { get; private set; }       // VO — Ignore in EF
    public ShiftType ShiftType { get; private set; }    // Morning/Noon/Evening/Night/Full
    public DateTime StartTime { get; private set; }
    public DateTime? EndTime { get; private set; }
    public string StaffName { get; private set; }
    public string? AcknowledgedBy { get; private set; }
    public DateTime? AcknowledgedAt { get; private set; }
    public ShiftStatus Status { get; private set; }     // Draft/Submitted/Acknowledged/Closed
    public string? HandoverNotes { get; private set; }
    public decimal? CashCount { get; private set; }
    public decimal? PosCashTotal { get; private set; }
    public List<InventoryCount> InventoryCounts { get; private set; } = new();
    public List<ShiftAlert> Alerts { get; private set; } = new();
}

// === Kiểm kê (mỗi dòng = 1 mặt hàng trong 1 ca) ===
public class InventoryCount : BaseEntity
{
    public InventoryCountId InventoryCountId { get; private set; }  // VO — Ignore
    public Guid ShiftId { get; private set; }           // FK → Shift.Id (PK)
    public Guid IngredientId { get; private set; }      // FK → Ingredient.Id (PK)
    public CountType CountType { get; private set; }     // Opening/Closing
    public decimal Quantity { get; private set; }
    public string Unit { get; private set; }             // g, ml, cái
    public decimal? MidShiftStockIn { get; private set; }
}

// === Cảnh báo ca ===
public class ShiftAlert : BaseEntity
{
    public ShiftAlertId ShiftAlertId { get; private set; }  // VO — Ignore
    public Guid ShiftId { get; private set; }           // FK → Shift.Id (PK)
    public string AlertCode { get; private set; }        // ING_VARIANCE_HIGH, ...
    public AlertSeverity Severity { get; private set; }  // Warning/Critical
    public string Message { get; private set; }
    public Guid? IngredientId { get; private set; }      // FK → Ingredient.Id (nullable)
    public decimal? VarianceValue { get; private set; }
    public decimal? VariancePercent { get; private set; }
    public bool IsResolved { get; private set; }
    public DateTime? ResolvedAt { get; private set; }
    public string? ResolvedBy { get; private set; }
    public string? ResolutionNote { get; private set; }
}

// === Tiêu hao lý thuyết (cache per shift per ingredient) ===
public class TheoreticalConsumption : BaseEntity
{
    public TheoreticalConsumptionId TheoreticalConsumptionId { get; private set; }  // VO — Ignore
    public Guid ShiftId { get; private set; }           // FK → Shift.Id (PK)
    public Guid IngredientId { get; private set; }      // FK → Ingredient.Id (PK)
    public decimal TheoreticalQuantity { get; private set; }
    public decimal ActualQuantity { get; private set; }  // = Opening + StockIn - Closing
    public decimal Variance { get; private set; }        // Actual - Theoretical
    public decimal VariancePercent { get; private set; }
}

// === Intelligence Layer ===
public class BusinessProfile : BaseEntity { /* Domain-01 */ }
public class BusinessPlan : BaseEntity { /* Domain-06 */ }
public class Forecast : BaseEntity { /* Domain-07 — versioned, timestamped */ }
public class Scenario : BaseEntity { /* Domain-08 — không ảnh hưởng dữ liệu thật */ }
public class Recommendation : BaseEntity { /* Domain-11 — confidence level + traceability */ }
public class BusinessAlert : BaseEntity { /* Domain-11 */ }
```

### 12.3 Enums mới

```csharp
public enum ShiftType { Morning, Noon, Evening, Night, Full }
public enum ShiftStatus { Draft, Submitted, Acknowledged, Closed }
public enum CountType { Opening, Closing }
public enum IngredientCategory { RawMaterial, Consumable, Supply }
public enum AlertSeverity { Warning, Critical }
public enum AdvisoryTrustLevel { Deterministic, RuleBased, StatisticalForecast, AIAdvisory }
```

### 12.4 EF Core Configuration rules

- Tuân thủ Single-Identity Pattern: `builder.Ignore(e => e.ShiftId)` (VO), v.v.
- Constructor sync: `Id = ShiftId.Value` sau `base(tenantId)`
- FK references `BaseEntity.Id` (PK), NOT business key VO
- Migration: tạo bảng `Shifts`, `InventoryCounts`, `ShiftAlerts`, `TheoreticalConsumptions`, `BusinessProfiles`, `BusinessPlans`, `Forecasts`, `Scenarios`, `Recommendations`, `BusinessAlerts`

### 12.5 Lưu trữ (Option C storage split)

| Entity | Database | Lý do |
|---|---|---|
| Shift, InventoryCount, ShiftAlert, TheoreticalConsumption | **ShopERP SQLite** (per-tenant) | Dữ liệu vận hành per-tenant, offline-first |
| Recipe, RecipeLine, Ingredient, Inventory | **ShopERP SQLite** (per-tenant) | Per-tenant operational |
| Order, OrderItem (source), JournalEntry, AccountingEntry, AccountChart, ElectronicInvoice, PeriodClosingStatus, Tenant, ShopInstance, User | **Gateway PostgreSQL** | Source of truth, accounting always online |
| BusinessProfile, BusinessPlan, Forecast, Scenario, Recommendation, BusinessAlert | **Gateway PostgreSQL** | Cross-period intelligence, cần aggregate cross-tenant-safe |
| COGS Accounting Entry (từ VA-IIE) | **Gateway PostgreSQL** | Accounting immutable |

---

# PHẦN V — ĐẶC TẢ KỸ THUẬT

## 13. Technical Specifications

### 13.1 Kiến trúc tổng thể

```text
ShopERP (Blazor Server, per-tenant SQLite)
├── Domain Layer (1_Shared/Domain.cs)
│   ├── Shift, InventoryCount, ShiftAlert, TheoreticalConsumption
│   ├── Recipe, RecipeLine, Ingredient, Inventory
│   ├── BusinessProfile, BusinessPlan, Forecast, Scenario, Recommendation
│   └── Pure domain logic (variance calc, alert rule evaluation, break-even)
├── Infrastructure Layer (3_CoreHub)
│   ├── EF Core configurations + migrations (SQLite cho operational, PG cho accounting)
│   └── Repositories (IShiftRepository, IRecipeRepository, IIngredientRepository, ...)
├── Services Layer (3_CoreHub)
│   ├── IShiftReportService — quản lý ca, kiểm kê, bàn giao
│   ├── IRecipeService — CRUD recipe, versioning
│   ├── ITheoreticalConsumptionService — tính tiêu hao lý thuyết từ POS
│   ├── IVarianceAnalysisService — variance analysis + sinh cảnh báo
│   ├── IAlertEngine — đánh giá alert rules, sinh ShiftAlert
│   ├── IFoodCostService — Food Cost / COGS / Waste Ratio
│   ├── IForecastService — dự báo nhập hàng / hết hàng / revenue / profit / cashflow
│   ├── IScenarioEngine — what-if simulation
│   ├── IManagementAdvisor — rank scenarios, emit recommendation
│   └── IFinancialReportService — P&L, Balance Sheet, Cashflow, Trial Balance
└── API/Presentation Layer (5_WebApps/ShopERP)
    ├── Blazor pages (ShiftReport, RecipeManagement, InventoryDashboard, AlertCenter, Forecast, Scenario, Advisor)
    └── API endpoints (REST cho mobile/PWA)
```

### 13.2 Dependency direction (Clean Architecture)

```text
API/Presentation → Services → Domain
                    ↓
              Infrastructure → Domain
```

- Domain layer PURE: NO EF Core, NO DbContext, NO DataAnnotations
- Services layer không phụ thuộc Infrastructure trực tiếp (qua interface)
- Multi-tenancy ở mọi layer

### 13.3 Tính tiêu hao lý thuyết — Performance

- Tính **real-time** khi đóng ca (không batch overnight)
- Cache Recipe active theo ProductId (in-memory, invalidate khi update Recipe)
- Nếu ca có > 500 đơn hàng → tính theo batch + aggregate (tránh OOM)
- Kết quả lưu `TheoreticalConsumption` table (cache, không tính lại)

### 13.4 Alert Engine — Rule evaluation

- Rule-based, declarative (mỗi alert code = 1 rule class implementing `IAlertRule`)
- Evaluate khi đóng ca + evaluate real-time (configurable)
- Rule có thể enable/disable per-tenant
- Rule output: `ShiftAlert` entity → persist + notify

```csharp
public interface IAlertRule
{
    string AlertCode { get; }
    AlertSeverity Severity { get; }
    Task<List<ShiftAlert>> EvaluateAsync(ShiftContext context, CancellationToken ct);
}
```

### 13.5 Forecasting

- **Restock Forecast:** `Average Daily Consumption × Lead Time → suggested restock qty`
- **Stockout Forecast:** `CurrentStock ÷ Average Daily Consumption = days remaining` → alert nếu < Lead Time + Safety Days
- **Revenue/Profit/Cashflow Forecast:** Time series + seasonality + trend + business plan
- Average Daily Consumption: rolling window (mặc định 14 ngày, configurable)
- Future: ML model (Prophet / simple linear regression) — Phase 5

### 13.6 Scenario Engine contract

- **Input:** thay đổi tham số (price, volume, cost, expansion, contraction)
- **Output:** metrics (revenue, COGS, gross profit, OPEX, net profit, cash, break-even)
- **Rule:** Scenario KHÔNG ảnh hưởng dữ liệu thật (BR-004). Scenario KHÔNG emit opinion.
- **Consumed by:** Management Advisor (DOMAIN-11) để rank + recommend

### 13.7 Advisory Trust Model

| Level | Type | Độ tin cậy | AI có ghi journal? |
|---|---|---|---|
| 1 | Deterministic (break-even formula) | Cao | ❌ |
| 2 | Rule-based (COGS tăng > 10% → cảnh báo) | Cao | ❌ |
| 3 | Statistical Forecast (dự báo doanh thu) | Trung bình | ❌ |
| 4 | AI Advisory (đề xuất hành động) | Cần confidence level | ❌ (trừ khi Owner explicitly authorizes workflow tự động đã kiểm soát) |

**Confidence bands (Level 4):**
- HIGH: ≥ 80% — hiển thị recommendation prominently
- MEDIUM: 60-79% — hiển thị với caveat
- LOW: < 60% — chỉ hiển thị khi user yêu cầu

### 13.8 API Endpoints (REST)

| Method | Path | Mô tả |
|---|---|---|
| `POST` | `/api/shifts` | Mở ca mới |
| `POST` | `/api/shifts/{id}/inventory-counts` | Nhập kiểm kê (đầu/cuối ca) |
| `POST` | `/api/shifts/{id}/restock` | Ghi nhận nhập thêm trong ca |
| `POST` | `/api/shifts/{id}/submit` | Submit báo cáo cuối ca |
| `POST` | `/api/shifts/{id}/acknowledge` | Xác nhận bàn giao |
| `GET` | `/api/shifts/{id}/report` | Lấy Shift Report đầy đủ |
| `GET/POST/PUT` | `/api/recipes` | CRUD Recipe |
| `GET/POST/PUT` | `/api/ingredients` | CRUD Ingredient |
| `GET` | `/api/alerts` | Danh sách cảnh báo (filter) |
| `POST` | `/api/alerts/{id}/resolve` | Resolve cảnh báo |
| `GET` | `/api/analytics/variance` | Variance Analysis Report |
| `GET` | `/api/analytics/food-cost` | Food Cost Report |
| `GET` | `/api/analytics/cogs` | COGS Report |
| `GET` | `/api/analytics/break-even` | Break-even Analysis (multi-product) |
| `GET` | `/api/analytics/unit-economics` | Unit Economics per product |
| `GET` | `/api/forecast/restock` | Restock Forecast |
| `GET` | `/api/forecast/stockout` | Stockout Forecast |
| `GET` | `/api/forecast/revenue` | Revenue Forecast |
| `GET` | `/api/forecast/profit` | Profit Forecast |
| `GET` | `/api/forecast/cashflow` | Cashflow Forecast |
| `POST` | `/api/scenarios` | Tạo scenario simulation |
| `GET` | `/api/scenarios/compare` | Compare scenarios |
| `GET` | `/api/recommendations` | List recommendations (filter by confidence) |
| `GET` | `/api/kpis` | KPI dashboard data |
| `GET` | `/api/financial-statements/{type}` | P&L / Balance Sheet / Cashflow / Trial Balance |

---

## 14. UI Requirements

> **HARD STOP:** ALWAYS dùng UI Platform components. NEVER bypass. Tham khảo `docs/UI_Platform_Implementation_Guide.md`.

### 14.1 Trang Shift Report
- Form nhập kiểm kê đầu/cuối ca (table editable, auto-complete ingredient)
- Form nhập tiền mặt + ghi chú bàn giao
- Nút "Tính & Đóng ca" → trigger variance analysis + alert engine
- Hiển thị Shift Report đầy đủ (6 phần)
- Export PDF / Excel

### 14.2 Trang Recipe Management
- CRUD Recipe + Recipe variant
- Version history viewer
- Drag-drop ingredient + auto unit conversion

### 14.3 Trang Inventory Dashboard
- Real-time tồn kho (table + progress bar vs reorder point)
- Widget cảnh báo active (Critical / Warning)
- Chart doanh số vs tiêu hao theo ca
- Top hao hụt / top cảnh báo

### 14.4 Trang Alert Center
- Danh sách cảnh báo (filter, sort, search)
- Resolve cảnh báo + ghi note
- History cảnh báo theo ca / nguyên liệu

### 14.5 Trang Analytics
- Variance Analysis (table + chart)
- Food Cost / COGS (table + chart)
- Waste Ratio (trend chart)
- Profitability per Item / per Shift
- Break-even (multi-product weighted)
- Unit Economics

### 14.6 Trang Forecast
- Restock Forecast (table + suggestion)
- Stockout Forecast (table + days remaining)
- Revenue / Profit / Cashflow Forecast (chart)

### 14.7 Trang Scenario Simulator
- Form nhập tham số (price/volume/cost/expansion/contraction)
- Output table so sánh Current vs A/B/C
- Save scenario + compare

### 14.8 Trang Advisor
- List active alerts + explanations
- Recommendations ranked by impact/risk/feasibility
- Confidence level badge
- Owner approve/reject action

### 14.9 Trang Financial Dashboard (VanADashboard)
- Financial Health widgets (9 metrics spec §15)
- Revenue / Cashflow / Asset / Liability / Receivable dashboards

### 14.10 Responsive / PWA
- ShopERP là PWA → UI phải responsive (mobile/tablet/desktop)
- Nhập kiểm kê tối ưu cho mobile (large touch targets)
- Offline-first: nhập kiểm kê offline → sync khi có mạng

---

## 15. Non-Functional Requirements

| # | Yêu cầu | Mô tả |
|---|---|---|
| NFR-1 | Performance | Đóng ca (variance + alert) < 3 giây cho ca ≤ 500 đơn |
| NFR-2 | Scalability | Hỗ trợ per-tenant SQLite, không contention cross-tenant. Multi-VPS qua ShopInstances routing |
| NFR-3 | Offline-first | Nhập kiểm kê offline, sync khi online (PWA). Last-write-wins + audit log + manual resolve |
| NFR-4 | Multi-tenancy | Mọi query filter by TenantId, không leak cross-tenant |
| NFR-5 | Security | Role-based: Staff (nhập kiểm kê), Manager (xem report + resolve alert), Owner (config thresholds + approve action), Accountant (accounting + tax + period close) |
| NFR-6 | Audit trail | Mọi thay đổi Recipe, InventoryCount, Alert resolution, Period close/reopen có audit log |
| NFR-7 | Data integrity | InventoryCount immutable sau khi Shift Closed (append-only, sửa qua adjustment entry). AccountingEntry 100% immutable (Reversal Entry only) |
| NFR-8 | Domain purity | Domain layer PURE, no EF Core, no DbContext, no DataAnnotations |
| NFR-9 | Single Identity | Mọi entity tuân thủ Single-Identity Pattern (Id = PK, business key VO Ignore) |
| NFR-10 | Build gate | `guard-check.ps1` + `dotnet build VanAn.sln` MUST PASS |
| NFR-11 | Traceability | Recommendation phải truy ngược được nguồn: Recommendation → Reason → KPI → Accounting transaction (BR-005) |
| NFR-12 | Versioning | Mọi calculation phải có version (FinancialModelVersion). Mọi forecast phải có timestamp (BR-006, BR-007) |
| NFR-13 | Confidence | Mọi recommendation phải có confidence level (BR-008) |
| NFR-14 | Immutability | Forecast không thay đổi Actual (BR-003). Scenario không ảnh hưởng dữ liệu thật (BR-004) |
| NFR-15 | Layer boundary | 3_CoreHub MUST remain pure Class Library (.dll). NO `<OutputType>Exe</OutputType>`. KhachLink MUST NOT inject `IVanAnDbContext` |

---

## 16. Business Rules

| ID | Rule |
|---|---|
| BR-001 | Accounting là nguồn dữ liệu tài chính chuẩn |
| BR-002 | Financial Intelligence không được thay đổi historical accounting data |
| BR-003 | Forecast không thay đổi Actual |
| BR-004 | Scenario không ảnh hưởng dữ liệu thật |
| BR-005 | Recommendation phải truy ngược được nguồn dữ liệu (Recommendation → Reason → KPI → Accounting transaction) |
| BR-006 | Mọi calculation phải có version (vd `FinancialModelVersion = 1.0`) |
| BR-007 | Mọi forecast phải có timestamp |
| BR-008 | Mọi recommendation phải có confidence level |
| BR-009 | AI không được tự sửa số liệu kế toán. AI không được tự ghi journal. AI chỉ được Analyze / Explain / Recommend — trừ khi Owner explicitly authorizes workflow tự động đã kiểm soát |
| BR-010 | Period Closed → không tạo/sửa AccountingEntry trong period (trừ Reversal Entry có approve) |
| BR-011 | Recipe có version — tiêu hao lý thuyết của Order cũ tính theo Recipe version active tại thời điểm bán |

---

## 17. Roadmap

### MVP-1 — Foundation (Accounting + Sales + Inventory + Cash + AR/AP + Tax/eInvoice)
- Domain entities + EF config + migration (còn thiếu)
- AR aging + overdue detection (UC-ACC-002)
- AP management (UC-ACC-003)
- Close Shift cơ bản (UC-SALES-002)
- eInvoice issuance + VAT report (UC-TAX-001/002) — codebase đã có UI
- HKD Period Declaration (UC-TAX-003)
- Period Closing (UC-ACC-006/007) — codebase đã có UI

### MVP-2 — Financial Intelligence (P&L + Cashflow + Break-even + Unit Economics + Dashboard)
- P&L, Balance Sheet, Cashflow, Trial Balance — codebase đã có
- Break-even multi-product (UC-FM-003) — mới
- Unit Economics (UC-FM-005) — mới
- Financial Health Dashboard (UC-DASH-001) — verify/upgrade VanADashboard
- KPI layer (DOMAIN-10) — mới

### MVP-3 — Shift Report & Inventory Intelligence Engine (từ VA-IIE)
- Shift management (mở/đóng ca, kiểm kê đầu/cuối)
- Recipe management (CRUD + versioning) — entity đã có
- Theoretical consumption calculation
- Variance Analysis service
- Alert Engine (12 alert rules)
- Food Cost / COGS / Waste Ratio reports
- Shift Report UI + Alert Center UI + Analytics dashboard

### MVP-4 — Planning & Forecast
- Business Plan + Budget (UC-PLAN-001/002/003)
- Revenue / Profit / Cashflow Forecast (UC-FC-001/002/003/004)
- Restock + Stockout Forecast (UC-FC-005/006)
- Actual vs Plan variance

### MVP-5 — Scenario Engine
- Price / Volume / Cost / Expansion / Contraction simulation
- Compare scenarios UI

### MVP-6 — Management Advisor
- Detect + Explain problem
- Generate + Rank recommendation
- Expansion recommendation
- Advisory Trust Model + confidence level

### Phase 7 — Polish & Integration
- Profitability per Item / per Shift
- Export PDF/Excel
- Telegram/Zalo bot notification
- PWA offline optimization
- E2E tests (Playwright)

### Phase 8 — Advanced (Future)
- ML-based forecasting (Prophet / regression)
- COGS auto-accounting entry (Gateway PostgreSQL) từ VA-IIE
- IoT integration (auto-weigh)
- Multi-branch consolidation

---

## 18. First Killer Use Cases

Nếu phải chọn 5 use case để chứng minh sản phẩm:

| ID | Câu hỏi | MVP |
|---|---|---|
| KU-01 | "Tháng này tôi có lời không?" | MVP-2 |
| KU-02 | "Điểm hòa vốn của tôi là bao nhiêu?" | MVP-2 |
| KU-03 | "Muốn lời 50 triệu thì phải bán bao nhiêu?" | MVP-4 |
| KU-04 | "Nếu tình hình hiện tại tiếp tục thì cuối tháng tôi lời hay lỗ?" | MVP-4 |
| KU-05 | "Tôi nên tăng giá, tăng doanh số hay giảm chi phí?" | MVP-6 |

**Killer use case bổ sung từ VA-IIE (F&B):**

| ID | Câu hỏi | MVP |
|---|---|---|
| KU-06 | "Ca này có thất thoát không?" | MVP-3 |
| KU-07 | "Nguyên liệu nào đang hao hụt bất thường?" | MVP-3 |
| KU-08 | "Khi nào cần nhập lại nguyên liệu?" | MVP-4 |

---

## 19. Risks & Mitigations

| Rủi ro | Mức | Giải pháp |
|---|---|---|
| Nhân viên nhập sai kiểm kê | Cao | Validation + so sánh book inventory + cảnh báo chênh lệch mở ca |
| Recipe không cập nhật khi đổi định lượng | Trung bình | Versioning + cảnh báo RECIPE_MISSING |
| Tiêu hao lý thuyết sai do POS bán sai món | Trung bình | Cảnh báo SALES_HIGH_STOCK_STABLE + audit POS |
| Performance chậm khi ca nhiều đơn | Thấp | Batch calculation + cache TheoreticalConsumption |
| Offline sync conflict | Trung bình | Last-write-wins + audit log + manual resolve |
| Domain entity bloat | Trung bình | Tách module Inventory Intelligence thành aggregate riêng trong Domain |
| Break-even sai do multi-product mix | Trung bình | Weighted contribution margin formula + mix % input |
| AI recommendation sai | Cao | Confidence level + traceability (BR-005) + AI không ghi journal (BR-009) |
| Cross-tenant data leak | Cao | TenantId filter mọi layer + architecture tests (39 tests đã có) |
| Period close bypass | Trung bình | PeriodClosingStatus lock + audit trail + Reversal Entry only |

---

## 20. Acceptance Criteria

### MVP-1 (Foundation)
- [ ] AR aging + overdue detection hoạt động
- [ ] AP management CRUD + aging
- [ ] Close Shift cơ bản (cash reconciliation)
- [ ] eInvoice issuance thành công + retry queue
- [ ] VAT report export đúng TT 152/2025/TT-BTC
- [ ] HKD Period Declaration export XML/PDF
- [ ] Period Close/Reopen + audit trail
- [ ] Multi-tenancy: không leak cross-tenant
- [ ] Build PASS: `guard-check.ps1` + `dotnet build VanAn.sln`
- [ ] UI dùng 100% UI Platform components
- [ ] Domain layer PURE (no EF Core, no DbContext)
- [ ] Single-Identity Pattern tuân thủ 100%

### MVP-2 (Financial Intelligence)
- [ ] Break-even multi-product (weighted contribution margin) chính xác
- [ ] Unit Economics per product hiển thị đúng
- [ ] Financial Health Dashboard hiển thị 9 metrics
- [ ] KPI layer tính đúng 6 groups KPIs (incl. Inventory Days / DIO)

### MVP-3 (Shift Report & IIE)
- [ ] Mở ca → nhập kiểm kê đầu ca → lưu thành công
- [ ] POS bán hàng trong ca → book inventory cập nhật
- [ ] Nhập thêm trong ca → ghi nhận MidShiftStockIn
- [ ] Đóng ca → nhập kiểm kê cuối ca + tiền mặt + ghi chú
- [ ] Đóng ca → hệ thống tự tính tiêu hao lý thuyết + variance
- [ ] Đóng ca → sinh cảnh báo đúng theo 12 rules
- [ ] Shift Report hiển thị đầy đủ 6 phần
- [ ] Bàn giao ca: SUBMITTED → ACKNOWLEDGED → CLOSED
- [ ] Recipe CRUD + versioning hoạt động
- [ ] Alert Center: filter + resolve cảnh báo
- [ ] Analytics: Food Cost / COGS / Variance report chính xác

### MVP-4 (Planning & Forecast)
- [ ] Business Plan + Budget CRUD
- [ ] Target Profit → Required Revenue/Volume/Margin
- [ ] Revenue/Profit/Cashflow Forecast hiển thị đúng
- [ ] Restock + Stockout Forecast chính xác
- [ ] Actual vs Plan variance hiển thị

### MVP-5 (Scenario Engine)
- [ ] 5 simulation types (price/volume/cost/expansion/contraction) hoạt động
- [ ] Compare scenarios table hiển thị đúng 7 metrics
- [ ] Scenario KHÔNG ảnh hưởng dữ liệu thật (BR-004)

### MVP-6 (Management Advisor)
- [ ] Detect problem + explain root cause
- [ ] Generate + rank recommendation (impact/risk/feasibility)
- [ ] Confidence level hiển thị trên mọi recommendation
- [ ] Traceability: Recommendation → Reason → KPI → Accounting transaction
- [ ] AI không ghi journal (BR-009)

---

## 21. Business Value Proposition

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

## 22. Strategic Architecture

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

**Định hướng dài hạn:** Vạn An trở thành **Local Business Operating System cho nền kinh tế tầng thấp**, trong đó Accounting là **financial backbone**, Inventory Intelligence Engine là **operational loss-prevention layer**, còn Financial Management + Forecasting + Advisory là **decision layer**.

---

## 23. References

- Mẫu gốc Shift Report: "ĐẦM COFFEE — BÁO CÁO CUỐI CA (NHÂN VIÊN)" (file: `c:\Temp\BaoCaoCuoiCa.jpg`)
- Governance: `.devin/rules/governance.md`
- UI Platform: `docs/UI_Platform_Implementation_Guide.md`
- Architecture: `docs/knowledge-base/00-core/PROJECT_CONTEXT.md`
- ShopERP Mini spec: `docs/requirements/ShopERPMini.md`
- Single-Identity Pattern: `.devin/rules/governance.md` (Section: Single-Identity Pattern)
- Data flow Option C: `gateway_router_multi_vps_master_plan.md`
- Project state: `docs/AI/project_state.md`
- Source specs đã gộp:
  - `docs/specs/Vạn An Local Business OS — Financial Management & Business Intelligence Specification.md`
  - `docs/requirements/Van_An_SRS_Inventory_Intelligence_Engine.md`

---

**Kết luận:** Tài liệu này thống nhất vision "Business OS cho nền kinh tế tầng thấp" với chi tiết kỹ thuật của Inventory Intelligence Engine, sửa các mâu thuẫn với kiến trúc Option C, bổ sung UCs cho Recipe/Tax/PeriodClosing, và làm rõ contract giữa Scenario Engine (no opinion) với Management Advisor (rank + recommend). Foundation codebase hiện tại khớp ~80% MVP-1 và ~55% MVP-2 — khoảng cách **gần** với MVP-1/2/3, **xa** với MVP-4/5/6 (intelligence layer cần xây mới).
