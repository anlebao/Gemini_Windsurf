# **ĐẶC TẢ YÊU CẦU NGHIỆP VỤ VÀ KỸ THUẬT (SRS)**

## **INVENTORY INTELLIGENCE ENGINE — BÁO CÁO CUỐI CA & PHÂN TÍCH HAO HỤT F&B (VA-IIE)**

**Ngày cập nhật:** 05/08/2026
**Trạng thái:** Sẵn sàng cho AI Code Generation (Vibe Coding / Agentic Workflow)
**Phạm vi:** ShopERP (per-tenant SQLite) + Gateway (PostgreSQL) trong hệ sinh thái Vạn An
**Ngành áp dụng:** Toàn bộ F&B (Cà phê, Nhà hàng, Trà sữa, Tiệm bánh, Fast Food, Quán ăn)

---

## **1. TỔNG QUAN HỆ THỐNG (SYSTEM OVERVIEW)**

### **1.1. Bối cảnh nghiệp vụ (Business Context)**

Mẫu giấy **"ĐẦM COFFEE — BÁO CÁO CUỐI CA (NHÂN VIÊN)"** là một phiên bản thủ công, rất đơn giản của một hệ thống ERP cho ngành F&B. Báo cáo gồm 5 phần:

| # | Phần báo cáo | Bản chất nghiệp vụ ERP |
|---|---|---|
| 1 | Kiểm tra ly (ly giấy, ly nhựa, bình...) | Kiểm kê vật tư tiêu hao (consumables) |
| 2 | Kiểm tra tồn kho nguyên liệu | Kiểm kê nguyên liệu (raw materials) |
| 3 | Kiểm tra vật tư tiêu hao | Kiểm kê vật tư (supplies) |
| 4 | Tiền mặt cuối ca | Bàn giao tiền mặt (cash handover) |
| 5 | Ghi chú bàn giao | Handover notes (shift handover) |

Mẫu giấy này phản ánh **6 nghiệp vụ lõi** của vận hành F&B:

1. **Kiểm kê đầu ca** (Opening inventory check)
2. **Nhập thêm trong ca** (Mid-shift stock-in / restocking)
3. **Tiêu hao trong ca** (Consumption during shift)
4. **Bán hàng** (Sales via POS)
5. **Tồn cuối ca** (Closing inventory)
6. **Bàn giao ca** (Shift handover)

### **1.2. Mục tiêu bài toán**

Xây dựng **Inventory Intelligence Engine (VA-IIE)** — một động cơ phân tích chủ động tích hợp vào ShopERP, biến báo cáo cuối ca thủ công thành hệ thống số hóa tự động có khả năng:

- Ghi nhận số liệu kiểm kê đầu/ca/cuối ca
- Tự động tính **tiêu hao lý thuyết** từ POS + Công thức pha chế (Recipe/BOM)
- **Đối chiếu tiêu hao lý thuyết vs tồn kho thực tế** để phát hiện sai lệch
- Sinh **cảnh báo chủ động** về hao hụt, thất thoát, tồn kho thấp, gian lận
- Tính **Food Cost, COGS, tỷ lệ hao hụt** theo từng món / từng ca
- **Dự báo** nhập hàng / hết hàng

### **1.3. Nguyên lý cốt lõi**

Hệ thống không chỉ **ghi nhận** số liệu mà còn **chủ động phân tích** và đưa ra cảnh báo. Nguyên lý hoạt động:

```
POS bán được (Order Items)
       ↓
Công thức pha chế (Recipe / BOM)
       ↓
Tiêu hao lý thuyết (Theoretical Consumption)
       ↓
So sánh với tồn kho thực tế (Physical Inventory Count)
       ↓
Variance Analysis → Cảnh báo / Báo cáo
```

### **1.4. Giá trị kinh doanh (Business Value)**

Đây là một trong những tính năng có giá trị nhất để giúp các quán F&B:

- **Giảm thất thoát** nguyên liệu, vật tư
- **Tăng hiệu quả vận hành** qua phát hiện sớm sai lệch
- **Kiểm soát chi phí** qua Food Cost / COGS theo món
- **Phát hiện gian lận** (nhân viên lấy hàng, tặng khách không nhập hệ thống)
- **Tối ưu tồn kho** qua dự báo nhập/hết hàng

---

## **2. PHẠM VI ÁP DỤNG (SCOPE)**

### **2.1. Ngành F&B áp dụng được**

Mô hình tổng quát cho toàn bộ ngành F&B:

| Loại hình | Ví dụ | Đặc thù |
|---|---|---|
| Quán cà phê | Đầm Coffee | Ly, nắp, ống hút + nguyên liệu pha chế |
| Nhà hàng | Nhà hàng VN | Nguyên liệu tươi sống, gia vị |
| Trà sữa | Trà sữa chain | Topping, syrup, ly, seal |
| Tiệm bánh | Bakery | Bột, kem, hộp, túi |
| Fast Food | Gà rán, burger | Nguyên liệu chế biến, bao bì |
| Quán ăn | Quán cơm, phở | Nguyên liệu nấu ăn, vật tư |

### **2.2. Tích hợp vào hệ sinh thái Vạn An**

| Thành phần | Vai trò trong VA-IIE |
|---|---|
| **ShopERP** (per-tenant SQLite) | Nguồn dữ liệu POS, Inventory, Recipe — chạy engine phân tích per-tenant |
| **Gateway** (PostgreSQL) | Source of truth cho Orders (PG→SQLite routed async) + Accounting (COGS entry) |
| **KhachLink** | Không liên quan trực tiếp (customer-facing) |
| **Domain layer** (`1_Shared/Domain.cs`) | Định nghĩa entity mới (ShiftReport, InventoryCount, VarianceAlert, ...) |

### **2.3. Trong phạm vi (In-Scope)**

- Số hóa báo cáo cuối ca (Shift Report)
- Kiểm kê đầu/cuối ca (Inventory Count)
- Quản lý công thức pha chế (Recipe / BOM)
- Tính tiêu hao lý thuyết từ POS sales
- Variance analysis (lý thuyết vs thực tế)
- Hệ thống cảnh báo (Alert Engine)
- Food Cost / COGS / Tỷ lệ hao hụt
- Dự báo nhập hàng / hết hàng
- Báo cáo theo ca / theo món / theo ngày

### **2.4. Ngoài phạm vi (Out-of-Scope)**

- Mua hàng / Procurement workflow (module riêng)
- Quản lý nhà cung cấp chi tiết (Supplier Portal)
- Kế toán COGS tự động (tách module Accounting, VA-IIE chỉ cung cấp số liệu)
- IoT tích hợp cân đo tự động (future enhancement)

---

## **3. ĐẶC TẢ NGHIỆP VỤ VÀ LUỒNG ĐIỀU HƯỚNG (BUSINESS & WORKFLOW REQUIREMENTS)**

### **3.1. Luồng Báo cáo cuối ca (Shift Report Workflow)**

#### **3.1.1. Mở ca (Shift Open)**

| Bước | Actor | Hành động |
|---|---|---|
| 1 | Nhân viên ca mới | Đăng nhập ShopERP → mở ca mới (chọn Shift type: Sáng/Trưa/Chiều/Tối/Full) |
| 2 | Nhân viên | Kiểm kê đầu ca: nhập số lượng thực tế từng nguyên liệu, vật tư, ly/nắp/ống hút |
| 3 | Hệ thống | Lưu Opening Inventory Count → snapshot tồn kho đầu ca |
| 4 | Hệ thống | So sánh với tồn kho hệ thống (book inventory) → ghi nhận chênh lệch mở ca (nếu có) |

#### **3.1.2. Trong ca (During Shift)**

- POS bán hàng tự động ghi nhận Order Items → hệ thống tính tiêu hao lý thuyết real-time
- Nhập thêm trong ca (restocking): nhân viên ghi nhận mỗi lần nhập thêm nguyên liệu/vật tư
- Hệ thống cập nhật book inventory liên tục

#### **3.1.3. Đóng ca (Shift Close / Handover)**

| Bước | Actor | Hành động |
|---|---|---|
| 1 | Nhân viên cuối ca | Kiểm kê cuối ca: nhập số lượng thực tế còn lại |
| 2 | Nhân viên | Nhập tiền mặt cuối ca (cash count) |
| 3 | Nhân viên | Nhập ghi chú bàn giao (handover notes) |
| 4 | Hệ thống | Tính toán tự động: tiêu hao thực tế = (Tồn đầu + Nhập thêm) − Tồn cuối |
| 5 | Hệ thống | Tính tiêu hao lý thuyết từ POS sales × Recipe/BOM |
| 6 | Hệ thống | Tính Variance = Tiêu hao thực tế − Tiêu hao lý thuyết |
| 7 | Hệ thống | Sinh cảnh báo nếu Variance vượt ngưỡng |
| 8 | Hệ thống | Tạo Shift Report hoàn chỉnh → bàn giao cho ca tiếp theo |
| 9 | Nhân viên ca mới | Xác nhận nhận bàn giao → mở ca mới (loop về 3.1.1) |

### **3.2. Luồng Quản lý Công thức pha chế (Recipe / BOM Management)**

#### **3.2.1. Định nghĩa Recipe**

Mỗi món bán hàng (Product) có 1 Recipe chứa danh sách nguyên liệu + định lượng:

```
Recipe: Cà phê sữa đá (Cà phê sữa đá)
├── Cà phê rang xay: 18g
├── Sữa đặc: 15ml
├── Đường: 10g
├── Đá viên: 120g
├── Ly giấy 12oz: 1 cái
├── Nắp đậy: 1 cái
└── Ống hút: 1 cái
```

#### **3.2.2. Quy tắc Recipe**

- Mỗi Product có **1 Recipe mặc định** + có thể có **Recipe variant** (size M/L, ít đường, thêm đá...)
- Recipe có **yield** (số lượng sản phẩm tạo ra từ 1 lần pha — thường = 1)
- Recipe có thể có **waste factor** (hao hụt chuẩn trong pha chế, ví dụ 2%)
- Nguyên liệu trong Recipe quy đổi về **đơn vị cơ sở** (g, ml, cái) — thống nhất với đơn vị kiểm kê

#### **3.2.3. Versioning**

- Recipe có version — khi sửa định lượng, tạo version mới, giữ version cũ để đối chiếu lịch sử
- Tiêu hao lý thuyết của Order cũ tính theo Recipe version active tại thời điểm bán

### **3.3. Luồng Tính Tiêu hao Lý thuyết (Theoretical Consumption Calculation)**

```
Cho mỗi Order trong ca:
  Cho mỗi OrderItem trong Order:
    Recipe = GetRecipe(ProductId, OrderTime)
    For each RecipeLine in Recipe:
      TheoreticalConsumption[Ingredient] += RecipeLine.Quantity × OrderItem.Quantity × (1 + WasteFactor)
```

Kết quả: Dictionary<IngredientId, TheoreticalQuantity> cho toàn ca.

### **3.4. Luồng Variance Analysis**

```
Variance[Ingredient] = ActualConsumption − TheoreticalConsumption

Trong đó:
  ActualConsumption = OpeningCount + MidShiftStockIn − ClosingCount

Phân loại Variance:
  Variance > 0  → Tiêu hao thực > lý thuyết → HAO HỤT (loss / waste)
  Variance < 0  → Tiêu hao thực < lý thuyết → BẤT THƯỜNG (có thể sai kiểm kê / gian lận ẩn)
  Variance ≈ 0  → Bình thường
```

### **3.5. Luồng Bàn giao ca (Shift Handover)**

- Shift Report có trạng thái: `DRAFT` → `SUBMITTED` → `ACKNOWLEDGED` → `CLOSED`
- Nhân viên ca cũ SUBMITTED → nhân viên ca mới ACKNOWLEDGED → ca cũ CLOSED
- Nếu ca mới không ACKNOWLEDGED trong thời gian quy định → cảnh báo cho quản lý
- Handover notes: text自由 + có thể đính kèm flag (cần quản lý xem)

---

## **4. HỆ THỐNG CẢNH BÁO (ALERT ENGINE)**

### **4.1. Danh sách cảnh báo**

| # | Mã cảnh báo | Tên cảnh báo | Điều kiện kích hoạt | Mức độ |
|---|---|---|---|---|
| 1 | `ING_VARIANCE_HIGH` | Hao hụt nguyên liệu | Variance > ngưỡng % (mặc định 5%) | ⚠️ Warning |
| 2 | `STOCK_LOW` | Tồn kho thấp | ClosingCount < ReorderPoint | ⚠️ Warning |
| 3 | `CONSUMPTION_OVER_LIMIT` | Tiêu hao vượt định mức | ActualConsumption > Theoretical × (1 + MaxVariance%) | 🔴 Critical |
| 4 | `SALES_HIGH_STOCK_STABLE` | Doanh số cao nhưng nguyên liệu không giảm | Sales > ngưỡng AND StockChange ≈ 0 | 🔴 Critical (gian lận) |
| 5 | `STOCK_DROP_NO_SALES` | Nguyên liệu giảm nhưng doanh số không tăng | StockChange < 0 AND Sales ≈ 0 | 🔴 Critical (thất thoát) |
| 6 | `MIDSHIFT_RESTOCK_UNUSUAL` | Nhập thêm bất thường trong ca | RestockQty > ngưỡng bất thường (outlier) | ⚠️ Warning |
| 7 | `CONSUMABLE_OVER_STANDARD` | Ly/nắp/ống hút sử dụng vượt chuẩn | ConsumableUsage > Sales × StandardRatio × (1 + Tolerance%) | ⚠️ Warning |
| 8 | `CASH_MISMATCH` | Tiền mặt chênh lệch | CashCount ≠ POS Cash Total ± Tolerance | 🔴 Critical |
| 9 | `RECIPE_MISSING` | Thiếu công thức pha chế | Product có bán hàng nhưng chưa định nghĩa Recipe | 🔴 Critical |
| 10 | `SHIFT_NOT_ACKNOWLEDGED` | Ca không được xác nhận bàn giao | Shift SUBMITTED > X giờ không ACKNOWLEDGED | ⚠️ Warning |

### **4.2. Cấu hình ngưỡng cảnh báo (Alert Thresholds)**

- Ngưỡng cấu hình **per-tenant** (mỗi quán tự cấu hình)
- Có giá trị mặc định hợp lý (sane defaults)
- Có thể override **per-ingredient** (ví dụ: nguyên liệu đắt tiền ngưỡng thấp hơn)
- Có thể override **per-category** (ví dụ: nguyên liệu tươi sống ngưỡng cao hơn)

| Tham số | Mặc định | Mô tả |
|---|---|---|
| `VarianceThresholdPercent` | 5% | Ngưỡng chênh lệch tiêu hao cho phép |
| `MaxVariancePercent` | 15% | Ngưỡng vượt định mức → Critical |
| `ReorderPoint` | per-item | Điểm đặt hàng lại |
| `ConsumableTolerancePercent` | 10% | Dung sai ly/nắp/ống hút |
| `CashTolerance` | 10,000 VND | Dung sai tiền mặt |
| `ShiftAckTimeoutHours` | 2h | Thời gian tối đa chờ xác nhận bàn giao |

### **4.3. Kênh thông báo cảnh báo**

| Kênh | Điều kiện |
|---|---|
| In-app (ShopERP) | Tất cả cảnh báo |
| Email | Critical + Warning (configurable) |
| Telegram/Zalo Bot | Critical (optional, per-tenant config) |
| Dashboard widget | Real-time summary |

---

## **5. BÁO CÁO & PHÂN TÍCH (REPORTING & ANALYTICS)**

### **5.1. Báo cáo cuối ca (Shift Report)**

Số hóa mẫu giấy "ĐẦM COFFEE — BÁO CÁO CUỐI CA", gồm:

**Phần 1: Thông tin ca**
- Tên nhân viên, ca làm việc, ngày, giờ mở/đóng ca
- Trạng thái bàn giao

**Phần 2: Kiểm tra ly & vật tư tiêu hao**
| Mặt hàng | Tồn đầu | Nhập thêm | Tồn cuối | Tiêu hao thực | Tiêu hao lý thuyết | Chênh lệch |

**Phần 3: Kiểm tra tồn kho nguyên liệu**
| Nguyên liệu | Tồn đầu | Nhập thêm | Tồn cuối | Tiêu hao thực | Tiêu hao lý thuyết | Chênh lệch |

**Phần 4: Tiền mặt cuối ca**
- POS Cash Total (tự động từ hệ thống)
- Cash Count (nhân viên đếm)
- Chênh lệch

**Phần 5: Ghi chú bàn giao**
- Text notes
- Cảnh báo tự động sinh ra trong ca

**Phần 6: Tóm tắt ca (auto-generated)**
- Tổng doanh số ca
- Số đơn hàng
- Food Cost ca
- COGS ca
- Tỷ lệ hao hụt trung bình
- Danh sách cảnh báo

### **5.2. Báo cáo phân tích (Analytics Reports)**

| Báo cáo | Tần suất | Nội dung |
|---|---|---|
| **Variance Analysis Report** | Theo ca / Ngày / Tuần | Chênh lệch tiêu hao theo nguyên liệu, top hao hụt |
| **Food Cost Report** | Theo món / Ca / Ngày | Chi phí nguyên liệu / món, % food cost |
| **COGS Report** | Ngày / Tuần / Tháng | Cost of Goods Sold tổng + theo category |
| **Waste Ratio Report** | Tuần / Tháng | Tỷ lệ hao hụt theo nguyên liệu, xu hướng |
| **Fraud Detection Report** | Theo ca | Các ca có cảnh báo Critical, pattern gian lận |
| **Restock Forecast** | Hàng ngày | Dự báo cần nhập hàng trong N ngày tới |
| **Stockout Forecast** | Hàng ngày | Dự báo nguyên liệu sắp hết |
| **Profitability per Item** | Tuần / Tháng | Lợi nhuận theo từng món (Revenue − Food Cost) |
| **Profitability per Shift** | Tuần / Tháng | Lợi nhuận theo từng ca làm việc |

### **5.3. Dashboard**

- Real-time widget: tồn kho hiện tại, cảnh báo active, doanh số ca hiện tại
- Trend chart: doanh số vs tiêu hao theo ca
- Heatmap: variance theo nguyên liệu × ca
- Top 5 hao hụt / top 5 cảnh báo Critical

---

## **6. MÔ HÌNH DỮ LIỆU (DATA MODEL)**

> **Lưu ý:** Tuân thủ Single-Identity Pattern (HARD STOP). Mọi entity kế thừa `BaseEntity` dùng `Id` (PK) làm identity duy nhất. Business key VO bị Ignore trong EF config. Constructor phải set `Id = BusinessKey.Value`.

### **6.1. Entity mới (Domain.cs)**

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
    // Navigation
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

// === Công thức pha chế ===
public class Recipe : BaseEntity
{
    public RecipeId RecipeId { get; private set; }       // VO — Ignore
    public Guid ProductId { get; private set; }          // FK → Product.Id (PK)
    public int Version { get; private set; }
    public decimal Yield { get; private set; }            // default 1
    public decimal WasteFactor { get; private set; }      // default 0
    public bool IsActive { get; private set; }
    public DateTime EffectiveFrom { get; private set; }
    public List<RecipeLine> Lines { get; private set; } = new();
}

public class RecipeLine : BaseEntity
{
    public RecipeLineId RecipeLineId { get; private set; }  // VO — Ignore
    public Guid RecipeId { get; private set; }          // FK → Recipe.Id (PK)
    public Guid IngredientId { get; private set; }      // FK → Ingredient.Id (PK)
    public decimal Quantity { get; private set; }
    public string Unit { get; private set; }
}

// === Nguyên liệu / Vật tư ===
public class Ingredient : BaseEntity
{
    public IngredientId IngredientId { get; private set; }  // VO — Ignore
    public string Name { get; private set; }
    public string Unit { get; private set; }             // g, ml, cái
    public IngredientCategory Category { get; private set; }  // RawMaterial/Consumable/Supply
    public decimal ReorderPoint { get; private set; }
    public decimal CurrentStock { get; private set; }
    public decimal? VarianceThresholdPercent { get; private set; }  // override per-item
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
```

### **6.2. Enum mới**

```csharp
public enum ShiftType { Morning, Noon, Evening, Night, Full }
public enum ShiftStatus { Draft, Submitted, Acknowledged, Closed }
public enum CountType { Opening, Closing }
public enum IngredientCategory { RawMaterial, Consumable, Supply }
public enum AlertSeverity { Warning, Critical }
```

### **6.3. EF Core Configuration**

- Tuân thủ Single-Identity Pattern: `builder.Ignore(e => e.ShiftId)` (VO), v.v.
- Constructor sync: `Id = ShiftId.Value` sau `base(tenantId)`
- FK references `BaseEntity.Id` (PK), NOT business key VO
- Migration: tạo bảng `Shifts`, `InventoryCounts`, `Recipes`, `RecipeLines`, `Ingredients`, `ShiftAlerts`, `TheoreticalConsumptions`

### **6.4. Lưu trữ**

| Entity | Database | Lý do |
|---|---|---|
| Shift, InventoryCount, Recipe, RecipeLine, Ingredient, ShiftAlert, TheoreticalConsumption | **ShopERP SQLite** (per-tenant) | Dữ liệu vận hành per-tenant, cần offline-first |
| COGS Accounting Entry (optional) | **Gateway PostgreSQL** | Nếu tích hợp kế toán COGS tự động (future) |

---

## **7. ĐẶC TẢ KỸ THUẬT (TECHNICAL SPECIFICATIONS)**

### **7.1. Kiến trúc tổng thể**

```
ShopERP (Blazor Server, per-tenant SQLite)
├── Domain Layer (1_Shared/Domain.cs)
│   ├── Shift, InventoryCount, Recipe, RecipeLine, Ingredient, ShiftAlert, TheoreticalConsumption
│   └── Pure domain logic (variance calc, alert rule evaluation)
├── Infrastructure Layer
│   ├── EF Core configurations + migrations (SQLite)
│   └── Repositories (IShiftRepository, IRecipeRepository, IIngredientRepository, ...)
├── Services Layer
│   ├── IShiftReportService — quản lý ca, kiểm kê, bàn giao
│   ├── IRecipeService — CRUD recipe, versioning
│   ├── ITheoreticalConsumptionService — tính tiêu hao lý thuyết từ POS
│   ├── IVarianceAnalysisService — variance analysis + sinh cảnh báo
│   ├── IAlertEngine — đánh giá alert rules, sinh ShiftAlert
│   ├── IFoodCostService — Food Cost / COGS / Waste Ratio
│   └── IForecastService — dự báo nhập hàng / hết hàng
└── API/Presentation Layer
    ├── Blazor pages (ShiftReport, RecipeManagement, InventoryDashboard, AlertCenter)
    └── API endpoints (REST cho mobile/PWA)
```

### **7.2. Dependency direction (Clean Architecture)**

```
API/Presentation → Services → Domain
                    ↓
              Infrastructure → Domain
```

- Domain layer PURE: NO EF Core, NO DbContext, NO DataAnnotations
- Services layer không phụ thuộc Infrastructure trực tiếp (qua interface)
- Tuân thủ Multi-tenancy ở mọi layer

### **7.3. Tính tiêu hao lý thuyết — Performance**

- Tính **real-time** khi đóng ca (không batch overnight)
- Cache Recipe active theo ProductId (in-memory, invalidate khi update Recipe)
- Nếu ca có > 500 đơn hàng → tính theo batch + aggregate (tránh OOM)
- Kết quả lưu `TheoreticalConsumption` table (cache, không tính lại)

### **7.4. Alert Engine — Rule evaluation**

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

### **7.5. Dự báo (Forecasting)**

- **Restock Forecast:** dựa trên Average Daily Consumption × Lead Time → đề xuất số lượng nhập
- **Stockout Forecast:** CurrentStock ÷ Average Daily Consumption = số ngày còn lại → cảnh báo nếu < Lead Time + Safety Days
- Average Daily Consumption: rolling window (mặc định 14 ngày), có thể config
- Future: ML model (Prophet / simple linear regression) — Phase 2

### **7.6. API Endpoints (REST)**

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
| `GET` | `/api/alerts` | Danh sách cảnh báo (filter by shift/severity/status) |
| `POST` | `/api/alerts/{id}/resolve` | Resolve cảnh báo |
| `GET` | `/api/analytics/variance` | Variance Analysis Report |
| `GET` | `/api/analytics/food-cost` | Food Cost Report |
| `GET` | `/api/analytics/cogs` | COGS Report |
| `GET` | `/api/forecast/restock` | Restock Forecast |
| `GET` | `/api/forecast/stockout` | Stockout Forecast |

---

## **8. YÊU CẦU GIAO DIỆN (UI REQUIREMENTS)**

> **HARD STOP:** ALWAYS dùng UI Platform components. NEVER bypass. Tham khảo `docs/UI_Platform_Implementation_Guide.md`.

### **8.1. Trang Shift Report**

- Form nhập kiểm kê đầu/cuối ca (table editable, auto-complete ingredient)
- Form nhập tiền mặt + ghi chú bàn giao
- Nút "Tính & Đóng ca" → trigger variance analysis + alert engine
- Hiển thị Shift Report đầy đủ (6 phần như Section 5.1)
- Export PDF / Excel

### **8.2. Trang Recipe Management**

- CRUD Recipe + Recipe variant
- Version history viewer
- Drag-drop ingredient + auto unit conversion

### **8.3. Trang Inventory Dashboard**

- Real-time tồn kho (table + progress bar vs reorder point)
- Widget cảnh báo active (Critical / Warning)
- Chart doanh số vs tiêu hao theo ca
- Top hao hụt / top cảnh báo

### **8.4. Trang Alert Center**

- Danh sách cảnh báo (filter, sort, search)
- Resolve cảnh báo + ghi note
- History cảnh báo theo ca / nguyên liệu

### **8.5. Trang Analytics**

- Variance Analysis (table + chart)
- Food Cost / COGS (table + chart)
- Waste Ratio (trend chart)
- Profitability per Item / per Shift

### **8.6. Trang Forecast**

- Restock Forecast (table + suggestion)
- Stockout Forecast (table + days remaining)

### **8.7. Responsive / PWA**

- ShopERP là PWA → UI phải responsive (mobile/tablet/desktop)
- Nhập kiểm kê tối ưu cho mobile (big touch target, numeric keypad)
- Offline-first: nhập kiểm kê offline → sync khi có mạng

---

## **9. YÊU CẦU PHI CHỨC NĂNG (NON-FUNCTIONAL REQUIREMENTS)**

| # | Yêu cầu | Mô tả |
|---|---|---|
| NFR-1 | Performance | Đóng ca (variance + alert) < 3 giây cho ca ≤ 500 đơn |
| NFR-2 | Scalability | Hỗ trợ per-tenant SQLite, không contention cross-tenant |
| NFR-3 | Offline-first | Nhập kiểm kê offline, sync khi online (PWA) |
| NFR-4 | Multi-tenancy | Mọi query filter by TenantId, không leak cross-tenant |
| NFR-5 | Security | Role-based: Staff (nhập kiểm kê), Manager (xem report + resolve alert), Owner (config thresholds) |
| NFR-6 | Audit trail | Mọi thay đổi Recipe, InventoryCount, Alert resolution có audit log |
| NFR-7 | Data integrity | InventoryCount immutable sau khi Shift Closed (append-only, sửa qua adjustment entry) |
| NFR-8 | Domain purity | Domain layer PURE, no EF Core, no DbContext |
| NFR-9 | Single Identity | Mọi entity tuân thủ Single-Identity Pattern (Id = PK, business key VO Ignore) |
| NFR-10 | Build gate | `guard-check.ps1` + `dotnet build VanAn.sln` MUST PASS |

---

## **10. LỊCH TRIỂN KHAI (ROADMAP)**

### **Phase 1 — Foundation (MVP)**
- Domain entities + EF config + migration
- Shift management (mở/đóng ca, kiểm kê đầu/cuối)
- Recipe management (CRUD + versioning)
- Theoretical consumption calculation
- Shift Report cơ bản (số hóa mẫu giấy)
- UI: Shift Report page + Recipe Management page

### **Phase 2 — Intelligence**
- Variance Analysis service
- Alert Engine (10 alert rules)
- Food Cost / COGS / Waste Ratio reports
- Alert Center UI
- Analytics dashboard

### **Phase 3 — Forecasting**
- Restock Forecast
- Stockout Forecast
- Forecast UI
- Trend analysis

### **Phase 4 — Polish & Integration**
- Profitability per Item / per Shift
- Export PDF/Excel
- Telegram/Zalo bot notification
- PWA offline optimization
- E2E tests (Playwright)

### **Phase 5 — Advanced (Future)**
- ML-based forecasting (Prophet / regression)
- COGS auto-accounting entry (Gateway PostgreSQL)
- IoT integration (auto-weigh)
- Multi-branch consolidation

---

## **11. RỦI RO & GIẢI PHÁP (RISKS & MITIGATIONS)**

| Rủi ro | Mức | Giải pháp |
|---|---|---|
| Nhân viên nhập sai kiểm kê | Cao | Validation + so sánh book inventory + cảnh báo chênh lệch mở ca |
| Recipe không cập nhật khi đổi định lượng | Trung bình | Versioning + cảnh báo RECIPE_MISSING |
| Tiêu hao lý thuyết sai do POS bán sai món | Trung bình | Cảnh báo SALES_HIGH_STOCK_STABLE + audit POS |
| Performance chậm khi ca nhiều đơn | Thấp | Batch calculation + cache TheoreticalConsumption |
| Offline sync conflict | Trung bình | Last-write-wins + audit log + manual resolve |
| Domain entity bloat | Trung bình | Tách module Inventory Intelligence thành aggregate riêng trong Domain |

---

## **12. TIÊU CHÍ NHẬN (ACCEPTANCE CRITERIA)**

- [ ] Mở ca → nhập kiểm kê đầu ca → lưu thành công
- [ ] POS bán hàng trong ca → book inventory cập nhật
- [ ] Nhập thêm trong ca → ghi nhận MidShiftStockIn
- [ ] Đóng ca → nhập kiểm kê cuối ca + tiền mặt + ghi chú
- [ ] Đóng ca → hệ thống tự tính tiêu hao lý thuyết + variance
- [ ] Đóng ca → sinh cảnh báo đúng theo rules
- [ ] Shift Report hiển thị đầy đủ 6 phần
- [ ] Bàn giao ca: SUBMITTED → ACKNOWLEDGED → CLOSED
- [ ] Recipe CRUD + versioning hoạt động
- [ ] Alert Center: filter + resolve cảnh báo
- [ ] Analytics: Food Cost / COGS / Variance report chính xác
- [ ] Forecast: Restock + Stockout hiển thị đúng
- [ ] Multi-tenancy: không leak cross-tenant
- [ ] Build PASS: `guard-check.ps1` + `dotnet build VanAn.sln`
- [ ] UI dùng 100% UI Platform components
- [ ] Domain layer PURE (no EF Core, no DbContext)
- [ ] Single-Identity Pattern tuân thủ 100%

---

## **13. TÀI LIỆU THAM KHẢO (REFERENCES)**

- Mẫu gốc: "ĐẦM COFFEE — BÁO CÁO CUỐI CA (NHÂN VIÊN)" (file: `c:\Temp\BaoCaoCuoiCa.jpg`)
- Governance: `.devin/rules/governance.md`
- UI Platform: `docs/UI_Platform_Implementation_Guide.md`
- Architecture: `docs/knowledge-base/00-core/PROJECT_CONTEXT.md`
- ShopERP Mini spec: `docs/requirements/ShopERPMIni.md`
- Single-Identity Pattern: `.devin/rules/governance.md` (Section: Single-Identity Pattern)
- Data flow Option C: `gateway_router_multi_vps_master_plan.md`

---

**Kết luận:** Việc kết hợp quản lý kho, công thức chế biến (Recipe/BOM), và so sánh với doanh số bán hàng (POS) sẽ làm được báo cáo/cảnh báo hao hụt, cảnh báo tồn kho — **đúng**. Đây chính là nền tảng của **Inventory Intelligence Engine**, một trong những tính năng có giá trị nhất để giúp các quán F&B giảm thất thoát và tăng hiệu quả vận hành.
