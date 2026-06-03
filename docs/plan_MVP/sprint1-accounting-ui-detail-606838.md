# Sprint 1 — Frontend Accounting Module: Detail Coding Plan

Kế hoạch chi tiết để implement Phase 2.6 (Accounting UI) 100% tuân thủ UI Platform, với namespace strategy rõ ràng và component mapping cụ thể cho từng screen.

---

## Câu trả lời nhanh

| Câu hỏi | Trả lời |
|---|---|
| Dùng UI Platform không? | ✅ **BẮT BUỘC** — ShopERP đã có reference, VanADashboard đã dùng |
| Follow 100% guide không? | ✅ **YES** — không custom HTML/CSS khi có component sẵn |
| Cần detail plan không? | ✅ **CẦN** — DynamicForm thiếu Date/Number field types, cần extend trước |

---

## Pre-Implementation: Component Gap Analysis

### Có sẵn trong UI.Platform (DÙNG NGAY)
| Component | Dùng cho |
|---|---|
| `VanAnCard` (Atomic) | Container cho mỗi section |
| `VanAnButton` (Atomic) | Save, Cancel, Filter, Export |
| `VanAnAlert` (Atomic) | Validation errors, success messages |
| `DynamicForm` (Composite) với `FieldType.Text`, `FieldType.Select`, `FieldType.Checkbox` | Entry forms |
| `VanAnModal` (Composite) | Transaction detail popup |
| `VanAnDataGrid<TItem>` (Data) | Transaction history table |
| `VanAMetricsCard` (root) | Balance display cards |
| `VanALayout` (root) | Page layout wrapper |

### THIẾU — Phải extend UI.Platform trước (KHÔNG tự tạo trong ShopERP)
| Cần tạo | Location | Mô tả |
|---|---|---|
| `FieldType.Date` | `UI.Platform/Components/Composite/FormTypes.cs` | Thêm case Date vào enum + render `<input type="date">` trong DynamicForm |
| `FieldType.Currency` | `UI.Platform/Components/Composite/FormTypes.cs` | Thêm case Currency + render `<input type="number" step="0.01">` |
| `VanAnSearchBar.razor` | `UI.Platform/Components/Composite/` | Search + filter bar cho TransactionHistory |

> **RULE:** Extend `DynamicForm` và `FormTypes.cs` trong `UI.Platform` — KHÔNG viết `<input>` thẳng trong ShopERP pages.

---

## Namespace Strategy (BẮT BUỘC FIRST)

```csharp
// Mỗi Accounting page PHẢI có đủ các using này:
@using VanAn.UI.Platform.Components.Atomic      // VanAnCard, VanAnButton, VanAnAlert
@using VanAn.UI.Platform.Components.Composite   // DynamicForm, VanAnModal
@using VanAn.UI.Platform.Components.Data        // VanAnDataGrid, VanAnColumn
@using VanAn.UI.Platform.Components             // VanAMetricsCard, VanALayout
@using VanAn.UI.Platform.Models                 // ComponentModels
@using VanAn.CoreHub.Services                   // IAccountingEntryService, IHKDTaxReportingService
@using VanAn.Shared.Domain                      // TenantId, Money, AccountingPeriod

// KHÔNG dùng:
// @using VanAn.CoreHub.Domain (entities chỉ ở 1_Shared/Domain.cs)
// Custom HTML <input>, <select>, <table> trực tiếp
```

---

## File Structure (Accounting Module trong ShopERP)

```
5_WebApps/ShopERP/
├── Components/
│   └── Pages/
│       └── Accounting/                   ← TẠO MỚI
│           ├── AccountingLayout.razor    ← Layout riêng cho Accounting
│           ├── AccountingIndex.razor     ← /accounting (hub page)
│           ├── RevenueEntry.razor        ← /accounting/revenue
│           ├── ExpenseEntry.razor        ← /accounting/expenses  
│           ├── TransactionHistory.razor  ← /accounting/history
│           └── AccountBalance.razor      ← /accounting/balance
└── Services/
    └── Accounting/                       ← TẠO MỚI (UI-layer only)
        └── AccountingUIService.cs        ← Wrapper/adapter cho IAccountingEntryService

UI.Platform/
└── Components/
    ├── Composite/
    │   ├── FormTypes.cs                  ← EXTEND: thêm Date, Currency types
    │   ├── DynamicForm.razor             ← EXTEND: render case mới
    │   └── VanAnSearchBar.razor          ← TẠO MỚI
    └── Data/
        (giữ nguyên VanAnDataGrid)
```

---

## Implementation Steps (Theo thứ tự)

### STEP 0: Pre-validation (30 phút) — BẮT BUỘC
```powershell
dotnet build VanAn.sln  # Phải 0 errors trước khi bắt đầu
.\guard-check.ps1
```

---

### STEP 1: Extend UI.Platform — FormTypes + DynamicForm (2 giờ)

**File:** `UI.Platform/Components/Composite/FormTypes.cs`
- Thêm vào enum `FieldType`: `Date`, `Currency`, `TextArea`
- Giữ nguyên `Text`, `Select`, `Checkbox` hiện có

**File:** `UI.Platform/Components/Composite/DynamicForm.razor`
- Thêm render case cho `FieldType.Date`:
  ```html
  <input type="date" class="form-control" id="@field.Id" ... />
  ```
- Thêm render case cho `FieldType.Currency`:
  ```html
  <input type="number" class="form-control" step="0.01" min="0" id="@field.Id" ... />
  ```

**File:** `UI.Platform/Components/Composite/VanAnSearchBar.razor` (TẠO MỚI)
- Props: `Placeholder`, `OnSearch` (EventCallback<string>), `OnFilter`, `AmountMin` (decimal?), `AmountMax` (decimal?), `AccountTypeOptions` (List<SelectOption>?)
- Render: search input + filter button dùng `VanAnButton` + 2 number inputs cho amount range + account type dropdown
- Amount range inputs chỉ hiển thị khi `ShowAmountFilter=true`
- Account type dropdown chỉ hiển thị khi `AccountTypeOptions` not null/empty

**Build gate:** `dotnet build VanAn.sln` → 0 errors

---

### STEP 2: AccountingLayout + Navigation (1 giờ)

**File:** `ShopERP/Components/Pages/Accounting/AccountingLayout.razor`
```razor
@using VanAn.UI.Platform.Components
@inherits LayoutComponentBase

<VanALayout>
    <VanANavigation MenuItems="@AccountingMenuItems" />
    <main class="accounting-content">
        @Body
    </main>
</VanALayout>
```
Menu items: Dashboard → Revenue → Expenses → History → Balance

**Update:** `ShopERP/Components/VanADashboard.razor` — thêm "Kế Toán" vào GetDashboardMenu()

---

### STEP 3: AccountingIndex.razor — Hub Page (1 giờ)

**Route:** `@page "/accounting"`  
**Components dùng:** `VanAnCard`, `VanAMetricsCard`, `VanAnButton`, `VanALayout`

Layout: 4 MetricsCard (Doanh thu tháng, Chi phí tháng, Lợi nhuận, Entries count) + Quick action buttons.

**Service injection:**
```csharp
@inject IAccountingEntryService AccountingService
@inject ILogger<AccountingIndex> Logger
```
- `TenantId` lấy từ `AuthenticationState` (pattern giống VanADashboard.razor)

---

### STEP 4: RevenueEntry.razor (3 giờ)

**Route:** `@page "/accounting/revenue"`  
**Components dùng:** `VanAnCard`, `DynamicForm`, `VanAnAlert`, `VanAnButton`

**FieldDefinitions:**
```csharp
new FormField { Id="date", Label="Ngày", Type=FieldType.Date },
new FormField { Id="amount", Label="Số tiền (VNĐ)", Type=FieldType.Currency },
new FormField { Id="account", Label="Tài khoản", Type=FieldType.Select, 
    Options = GetRevenueAccounts() },  // 511, 515, 711...
new FormField { Id="description", Label="Diễn giải", Type=FieldType.TextArea },
new FormField { Id="reference", Label="Số chứng từ", Type=FieldType.Text }
```

**Service injection bổ sung:**
```csharp
@inject IJournalTemplateService JournalTemplateService
```

**Description auto-complete:**
- Khi user nhập vào field `description`, gọi `JournalTemplateService.GetSuggestionsAsync(input)` sau 300ms debounce
- Hiển thị dropdown suggestions dùng `VanAnModal` inline hoặc `<datalist>` HTML5
- `IJournalTemplateService` nằm trong `VanAn.CoreHub.Services` — KHÔNG tạo mới trong ShopERP

**OnSubmit logic:**
1. Validate: amount > 0, date in current period, account not empty
2. Validate account code format: gọi `AccountCodeValidator.IsValidVNAccountCode(accountCode)` — chặn submit nếu sai format (3 chữ số, thuộc danh mục 5xx)
3. Call `IAccountingEntryService.CreateRevenueEntryAsync(tenantId, ...)`
4. Show `VanAnAlert Type="success"` hoặc `Type="error"`
5. Duplicate check: same amount + date + account trong 5 phút

---

### STEP 5: ExpenseEntry.razor (2 giờ)

**Route:** `@page "/accounting/expenses"`  
**Components dùng:** Giống RevenueEntry nhưng account options = chi phí (621, 622, 627, 641...)

**FieldDefinitions bổ sung:**
```csharp
new FormField { Id="vendor", Label="Nhà cung cấp", Type=FieldType.Text },
new FormField { Id="category", Label="Loại chi phí", Type=FieldType.Select,
    Options = GetExpenseCategories() }
```

**Validation bổ sung (giống RevenueEntry):**
- Description auto-complete từ `JournalTemplateService.GetSuggestionsAsync(input)`
- Account code validation: `AccountCodeValidator.IsValidVNAccountCode(accountCode)` cho danh mục 6xx (chi phí)
- Vendor: bắt buộc khi `category` thuộc nhóm "Mua hàng hóa", "Mua vật liệu"

---

### STEP 6: TransactionHistory.razor (3 giờ)

**Route:** `@page "/accounting/history"`  
**Components dùng:** `VanAnSearchBar`, `VanAnDataGrid<AccountingEntryDto>`, `VanAnModal`, `VanAnCard`, `VanAnButton`

**DataGrid columns:**
| Cột | Property | Width |
|---|---|---|
| Ngày | Date | 1 |
| Loại | EntryType | 1 |
| Tài khoản | AccountCode | 1 |
| Diễn giải | Description | 3 |
| Số tiền | Amount (formatted) | 1 |
| Actions | (Edit/View button) | 1 |

**Filter:** VanAnSearchBar với `ShowAmountFilter=true` và `AccountTypeOptions=GetAccountTypeOptions()` → filter by:
- Period dropdown (tháng/năm)
- Account type dropdown: `Revenue` (5xx), `Expense` (6xx), `All` (default)
- Search by description (text)
- Amount range: `AmountMin` và `AmountMax` (2 inputs currency)

**GetAccountTypeOptions():**
```csharp
private List<SelectOption> GetAccountTypeOptions() => new()
{
    new SelectOption { Value = "",        Label = "Tất cả loại" },
    new SelectOption { Value = "Revenue",  Label = "Doanh thu (5xx)" },
    new SelectOption { Value = "Expense",  Label = "Chi phí (6xx)" }
};
```

**Filter signature truyền vào service:**
```csharp
await AccountingService.GetEntriesAsync(tenantId, searchText, periodStart, periodEnd, amountMin, amountMax, accountType)
```

**Export Excel:** `VanAnButton` gọi service export (dùng ClosedXML, thêm package vào ShopERP.csproj)

---

### STEP 7: AccountBalance.razor (2 giờ)

**Route:** `@page "/accounting/balance"`  
**Components dùng:** `VanAMetricsCard`, `VanAnCard`, `VanAnDataGrid<BalanceRow>`, `VanAnAlert`

**Service injection bổ sung:**
```csharp
@inject IAccountingEntryService AccountingService
```

**Data loading:**
```csharp
var currentMonth = await AccountingService.GetBalanceSummaryAsync(tenantId, currentPeriod);
var previousMonth = await AccountingService.GetBalanceSummaryAsync(tenantId, previousPeriod);
```

**Layout:**
- Row 1: 3 MetricsCard hiển thị **tháng này** (Tổng doanh thu, Tổng chi phí, Lợi nhuận ròng)
- Row 1b: Dưới mỗi MetricsCard — badge so sánh vs tháng trước: `▲ +15%` hoặc `▼ -8%` (tính từ `previousMonth`)
- Row 2: VanAnCard "Chi tiết theo tài khoản" → VanAnDataGrid
- Row 3: VanAnAlert nếu có balance âm

**Period comparison logic:**
```csharp
var revenueDelta = currentMonth.TotalRevenue - previousMonth.TotalRevenue;
var revenuePercent = previousMonth.TotalRevenue > 0
    ? (revenueDelta / previousMonth.TotalRevenue) * 100 : 0;
// Hiển thị: revenuePercent >= 0 ? $"▲ +{revenuePercent:F1}%" : $"▼ {revenuePercent:F1}%"
```

**Validation:** Nếu `totalExpenses > totalRevenue * 1.5` → Alert warning

---

### STEP 8: DI Registration + Build Validation (30 phút)

**File:** `ShopERP/Program.cs`
- Đảm bảo `IAccountingEntryService` đã được register (kiểm tra, không thêm duplicate)
- Register `AccountingUIService` nếu cần

```powershell
dotnet build VanAn.sln   # Target: 0 errors
.\guard-check.ps1        # Phải PASS
```

---

## Component Usage Summary (Accounting Module)

| Screen | VanAnCard | DynamicForm | VanAnDataGrid | VanAMetricsCard | VanAnAlert | VanAnModal |
|---|:---:|:---:|:---:|:---:|:---:|:---:|
| AccountingIndex | ✅ | | | ✅ | | |
| RevenueEntry | ✅ | ✅ | | | ✅ | |
| ExpenseEntry | ✅ | ✅ | | | ✅ | |
| TransactionHistory | ✅ | | ✅ | | | ✅ |
| AccountBalance | ✅ | | ✅ | ✅ | ✅ | |

**Rule:** Mọi layout wrapper = `VanALayout`. Mọi button = `VanAnButton`. KHÔNG có `<div class="card">`, `<button>`, `<table>` thẳng.

---

## Anti-Pattern List (KHÔNG được làm)

```razor
❌ <input type="text" class="form-control" />   → ✅ Dùng DynamicForm với FieldType.Text
❌ <table class="table">                         → ✅ Dùng VanAnDataGrid<T>
❌ <div class="card">                            → ✅ Dùng VanAnCard
❌ <button class="btn btn-primary">              → ✅ Dùng VanAnButton Variant="primary"
❌ <div class="alert alert-danger">              → ✅ Dùng VanAnAlert Type="error"
❌ Tạo FormField mới trong ShopERP              → ✅ Extend FormTypes.cs trong UI.Platform
```

---

## Estimated Effort

| Step | Task | Thời gian |
|---|---|---|
| 0 | Pre-validation build | 30 phút |
| 1 | Extend UI.Platform (FormTypes + VanAnSearchBar) | 2 giờ |
| 2 | AccountingLayout + Navigation | 1 giờ |
| 3 | AccountingIndex (hub page) | 1 giờ |
| 4 | RevenueEntry.razor | 3 giờ |
| 5 | ExpenseEntry.razor | 2 giờ |
| 6 | TransactionHistory.razor | 3 giờ |
| 7 | AccountBalance.razor | 2 giờ |
| 8 | DI + Build validation | 30 phút |
| **Total** | | **~15 giờ** |
