# TASK CARD — Phase 2: Bug 2B — VAS Financial Reports Export (DOCX + XLSX)

> **Status:** 🟡 PLANNED — ready to implement
> **Prerequisite:** None (independent phase)
> **Branch:** `feature/tenant-fix-phase2-vas-export`
> **Estimated sessions:** 2 (1 service + DI, 1 UI + tests)
> **Mode:** IMPLEMENT
> **Domain modification:** ❌ NO (feature gap fill, không động Domain)

## Objective
Thêm functionality export DOCX + XLSX cho 4 trang VAS Financial Reports (Balance Sheet, Income Statement, Cash Flow Statement, Trial Balance) — tương tự `HKDBookDetail.razor` đã có cho sổ HKD.

**Business logic:**
- 4 trang VAS reports hiện tại chỉ có nút "🔄 Tạo báo cáo" — KHÔNG có nút export
- `HKDBookDetail.razor:61-66` đã có pattern: 2 nút "📄 Xuất DOCX" + "📊 Xuất XLSX" → gọi `IHKDBookExportService` + JS `vanAn.downloadFile`
- JS `vanAn.downloadFile` đã tồn tại tại `App.razor:30-48` — reuse được
- Cần tạo `IFinancialReportExportService` tương đương `IHKDBookExportService`

## Prerequisites
- [ ] Phase 2 INVESTIGATE: verify 4 VAS report pages current state (no export buttons)
- [ ] Verify `HKDBookExportService.cs` pattern (Open XML SDK + EPPlus)
- [ ] Verify `vanAn.downloadFile` JS function exists (`App.razor:30-48`)
- [ ] Verify 4 report domain types: `BalanceSheet`, `IncomeStatement`, `CashFlowStatement`, `TrialBalance` (grep Domain.cs)
- [ ] Verify `FinancialStatementLine` structure (used by all 4 reports)

## Files to Modify
| File | Changes |
|------|---------|
| `5_WebApps/ShopERP/Services/FinancialReportExportService.cs` (new) | New service: `IFinancialReportExportService` + impl (DOCX + XLSX cho 4 reports) |
| `5_WebApps/ShopERP/Program.cs` | Register `IFinancialReportExportService` in DI |
| `5_WebApps/ShopERP/Components/Pages/Accounting/BalanceSheet.razor` | Add export buttons + inject service + ExportDocx/ExportXlsx methods |
| `5_WebApps/ShopERP/Components/Pages/Accounting/IncomeStatement.razor` | Same as BalanceSheet |
| `5_WebApps/ShopERP/Components/Pages/Accounting/CashFlowStatement.razor` | Same as BalanceSheet |
| `5_WebApps/ShopERP/Components/Pages/Accounting/TrialBalance.razor` | Same as BalanceSheet |
| `6_Testing/e2e-tests/vas-export.spec.ts` (new) | E2E test Gate 4 compliance |

## Detailed Task List

### P2-T1: INVESTIGATE — Verify report domain types + FinancialStatementLine structure
**Before coding:** grep Domain.cs để confirm 4 report types + `FinancialStatementLine` structure.

```powershell
# Verify 4 report types exist
grep -n "class BalanceSheet|class IncomeStatement|class CashFlowStatement|class TrialBalance" 1_Shared/Domain.cs
# Verify FinancialStatementLine
grep -n "class FinancialStatementLine|record FinancialStatementLine" 1_Shared/Domain.cs
```

**Expected:** 4 report classes + `FinancialStatementLine` với properties: `ReportItemCode`, `ReportItemName`, `EndingAmount`, `OpeningAmount`, `Level`, `IsNormalNegative`.

**If drift detected:** Update task card với actual structure trước khi code T2.

### P2-T2: Create `IFinancialReportExportService` + impl
**File mới:** `5_WebApps/ShopERP/Services/FinancialReportExportService.cs`

**Architecture decision (D5):** 1 generic exporter thay vì 8 method riêng — vì 4 reports có cấu trúc tương tự (header + bảng `FinancialStatementLine` list).

**Interface:**
```csharp
namespace VanAn.ShopERP.Services
{
    /// <summary>
    /// Bug 2B fix: Export VAS Financial Reports to DOCX (Open XML SDK) + XLSX (EPPlus).
    /// Pattern mirrors IHKDBookExportService. 4 reports share FinancialStatementLine structure.
    /// </summary>
    public interface IFinancialReportExportService
    {
        /// <summary>Export a financial report (BalanceSheet/IncomeStatement/etc.) to DOCX.</summary>
        /// <param name="title">Report title (vd: "Bảng Cân Đối Kế Toán")</param>
        /// <param name="period">Period string (vd: "Tháng 08/2026")</param>
        /// <param name="sections">Named sections (vd: [("TÀI SẢN", assetsLines), ("NỢ PHẢI TRẢ", liabilitiesLines)])</param>
        Task<byte[]> ExportToDocxAsync(string title, string period, IReadOnlyList<(string SectionName, IReadOnlyList<FinancialStatementLine> Lines)> sections);

        /// <summary>Export same report to XLSX.</summary>
        Task<byte[]> ExportToXlsxAsync(string title, string period, IReadOnlyList<(string SectionName, IReadOnlyList<FinancialStatementLine> Lines)> sections);
    }
}
```

**Implementation:** Mirror `HKDBookExportService.cs` pattern:
- DOCX: `WordprocessingDocument` + `Body` + header paragraphs + table per section + footer
- XLSX: `ExcelPackage` + worksheet + header rows + data rows per section

**Reference:** `5_WebApps/ShopERP/Services/HKDBookExportService.cs` (lines 26-43 cho DOCX pattern).

### P2-T3: Register DI
**File:** `5_WebApps/ShopERP/Program.cs`

**Find existing registration (line 250):**
```csharp
services.AddScoped<IHKDBookExportService, HKDBookExportService>();
```

**Add after:**
```csharp
services.AddScoped<IFinancialReportExportService, FinancialReportExportService>();
```

### P2-T4: Add export UI to BalanceSheet.razor
**File:** `5_WebApps/ShopERP/Components/Pages/Accounting/BalanceSheet.razor`

**Add inject (after line 15):**
```razor
@inject VanAn.ShopERP.Services.IFinancialReportExportService ExportService
@inject IJSRuntime JSRuntime
```

**Add export buttons to "Kỳ báo cáo" card (after line 49 — sau nút "Áp dụng"):**
```razor
<VanAButton Variant="success" Size="small" OnClick="ExportDocx" Disabled="@exporting">
    📄 Xuất DOCX
</VanAButton>
<VanAButton Variant="success" Size="small" OnClick="ExportXlsx" Disabled="@exporting">
    📊 Xuất XLSX
</VanAButton>
```

**Add fields + methods to `@code` block:**
```csharp
private bool exporting = false;

private async Task ExportDocx()
{
    if (report == null) return;
    await DownloadFile(async () =>
    {
        var sections = new List<(string, IReadOnlyList<FinancialStatementLine>)>
        {
            ("TÀI SẢN", report.Assets),
            ("NỢ PHẢI TRẢ", report.Liabilities),
            ("VỐN CHỦ SỞ HỮU", report.Equity)
        };
        return await ExportService.ExportToDocxAsync("Bảng Cân Đối Kế Toán", $"Tháng {selectedMonth:D2}/{selectedYear}", sections);
    }, $"BalanceSheet_{selectedYear}_{selectedMonth:D2}.docx");
}

private async Task ExportXlsx()
{
    if (report == null) return;
    await DownloadFile(async () =>
    {
        var sections = new List<(string, IReadOnlyList<FinancialStatementLine>)>
        {
            ("TÀI SẢN", report.Assets),
            ("NỢ PHẢI TRẢ", report.Liabilities),
            ("VỐN CHỦ SỞ HỮU", report.Equity)
        };
        return await ExportService.ExportToXlsxAsync("Bảng Cân Đối Kế Toán", $"Tháng {selectedMonth:D2}/{selectedYear}", sections);
    }, $"BalanceSheet_{selectedYear}_{selectedMonth:D2}.xlsx");
}

private async Task DownloadFile(Func<Task<byte[]>> produce, string fileName)
{
    exporting = true;
    try
    {
        byte[] bytes = await produce();
        string base64 = Convert.ToBase64String(bytes);
        string mime = fileName.EndsWith(".docx") ? "application/vnd.openxmlformats-officedocument.wordprocessingml.document" : "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";
        await JSRuntime.InvokeVoidAsync("vanAn.downloadFile", base64, mime, fileName);
    }
    catch (Exception ex)
    {
        Logger.LogError(ex, "Error exporting {FileName}", fileName);
        errorMessage = $"Không thể xuất file: {ex.Message}";
    }
    finally
    {
        exporting = false;
    }
}
```

**Note:** `DownloadFile` method identical to `HKDBookDetail.razor:180-199`. Có thể extract to shared utility sau (tech debt), nhưng hiện tại duplicate cho simplicity.

### P2-T5: Add export UI to IncomeStatement.razor
**File:** `5_WebApps/ShopERP/Components/Pages/Accounting/IncomeStatement.razor`

Same pattern as P2-T4. INVESTIGATE first: read file to confirm `report` variable name + section structure (IncomeStatement có thể có 1 section "Kết Quả Hoạt Động Kinh Doanh" thay vì 3 sections như BalanceSheet).

**Sections for IncomeStatement (TBD — verify in INVESTIGATE):**
```csharp
var sections = new List<(string, IReadOnlyList<FinancialStatementLine>)>
{
    ("KẾT QUẢ HOẠT ĐỘNG KINH DOANH", report.Lines)  // verify property name
};
```

### P2-T6: Add export UI to CashFlowStatement.razor
**File:** `5_WebApps/ShopERP/Components/Pages/Accounting/CashFlowStatement.razor`

Same pattern. INVESTIGATE section structure.

### P2-T7: Add export UI to TrialBalance.razor
**File:** `5_WebApps/ShopERP/Components/Pages/Accounting/TrialBalance.razor`

Same pattern. INVESTIGATE section structure.

### P2-T8: E2E test (Gate 4 compliance)
**File mới:** `6_Testing/e2e-tests/vas-export.spec.ts`

```typescript
import { test, expect } from '@playwright/test';

test.describe('Bug 2B — VAS Financial Reports export', () => {
  test.beforeEach(async ({ page }) => {
    // Login as Company tenant owner (VAS reports only for Enterprise)
  });

  test('Balance Sheet exports DOCX', async ({ page }) => {
    // Navigate to /accounting/balance-sheet
    // Click "📄 Xuất DOCX"
    // Assert: download event triggered with .docx filename
  });

  test('Balance Sheet exports XLSX', async ({ page }) => {
    // Similar — click "📊 Xuất XLSX"
  });

  // Repeat for IncomeStatement, CashFlowStatement, TrialBalance (4 reports × 2 formats = 8 tests)
});
```

**Note:** Cần INVESTIGATE existing E2E login pattern + Playwright download event handling.

### P2-T9: Build + guard + tests
- `dotnet build VanAn.sln` Release — 0 errors
- `guard-check.ps1` — ALL CHECKS PASSED
- E2E tests pass (nếu local infra có Playwright)
- Commit: `[TENANT-FIX P2] add VAS reports export (DOCX + XLSX)`

## Verification
- [ ] `IFinancialReportExportService` created with DOCX + XLSX methods
- [ ] DI registered in Program.cs
- [ ] 4 VAS report pages have export buttons (DOCX + XLSX)
- [ ] Export triggers browser download (vanAn.downloadFile JS)
- [ ] E2E tests cover 4 reports × 2 formats = 8 scenarios
- [ ] Build 0 errors
- [ ] Guard pass
- [ ] Commit on feature branch

## Rollback
- Git revert commit
- 4 VAS report pages sẽ lại không có export buttons (pre-fix state)
- Service file + DI registration removed

## Impact Assessment
- **User-facing:** Tenant Company có thể export 4 VAS reports ra DOCX/XLSX (feature mới)
- **Performance:** Export là on-demand, không impact runtime
- **Security:** Không đổi (export trong OwnerOnly policy)
- **Data:** Không đổi (read-only export)
- **Dependencies:** Reuse Open XML SDK + EPPlus (đã có trong project)
