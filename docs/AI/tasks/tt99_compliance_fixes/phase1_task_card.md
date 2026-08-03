# TASK CARD — Phase 1: Rename B 01-DN "Bảng Cân Đối Kế Toán" → "Báo Cáo Tình Hình Tài Chính"

> **Status:** 🟡 PLANNED
> **Priority:** P0 — Quick fix
> **Branch:** `feature/tt99-fix-phase1-rename-b01dn`
> **Estimated sessions:** 1 (30 phút)
> **Mode:** IMPLEMENT
> **Domain modification:** NO (comment + UI text only)

## Objective
TT 99/2025/TT-BTC đổi tên "Bảng cân đối kế toán" → "Báo cáo tình hình tài chính". Codebase hiện vẫn dùng tên cũ tại 4 vị trí hiển thị. Cần đổi tên để tuân thủ TT 99.

## Prerequisites
- [ ] Verify `BalanceSheet.razor` line 21 hiện `<h1>Bảng Cân Đối Kế Toán</h1>`
- [ ] Verify `FinancialReports.razor` line 19 hiện `📊 Bảng Cân Đối Kế Toán`
- [ ] Verify `Domain.cs` line 3329 comment `// BẢNG CÂN ĐỐI KẾ TOÁN (Mẫu B01-DN)`
- [ ] Verify `FinancialReportExportService.cs` export title `"Bảng Cân Đối Kế Toán"`

## Files to Modify
| File | Line | Current | New |
|------|------|---------|-----|
| `5_WebApps/ShopERP/Components/Pages/Accounting/BalanceSheet.razor` | 21 | `<h1>Bảng Cân Đối Kế Toán</h1>` | `<h1>Báo Cáo Tình Hình Tài Chính (B 01-DN)</h1>` |
| `5_WebApps/ShopERP/Components/Pages/Accounting/BalanceSheet.razor` | 60 | `Đang tạo Bảng Cân Đối Kế Toán` | `Đang tạo Báo Cáo Tình Hình Tài Chính` |
| `5_WebApps/ShopERP/Components/Pages/Accounting/BalanceSheet.razor` | 200 | `errorMessage = "Không thể tạo Bảng Cân Đối Kế Toán..."` | `errorMessage = "Không thể tạo Báo Cáo Tình Hình Tài Chính..."` |
| `5_WebApps/ShopERP/Components/Pages/Accounting/BalanceSheet.razor` | Export methods | `"Bảng Cân Đối Kế Toán"` | `"Báo Cáo Tình Hình Tài Chính (B 01-DN)"` |
| `5_WebApps/ShopERP/Components/Pages/Accounting/FinancialReports.razor` | 19 | `📊 Bảng Cân Đối Kế Toán` | `📊 Báo Cáo Tình Hình Tài Chính (B 01-DN)` |
| `1_Shared/Domain.cs` | 3329 | `// ── 1. BẢNG CÂN ĐỐI KẾ TOÁN (Mẫu B01-DN / B01-DNN) ──` | `// ── 1. BÁO CÁO TÌNH HÌNH TÀI CHÍNH (Mẫu B01-DN / B01-DNN) ──` |
| `5_WebApps/ShopERP/Services/FinancialReportExportService.cs` | ExportStatementToDocx title | `"Bảng Cân Đối Kế Toán"` | `"Báo Cáo Tình Hình Tài Chính (B 01-DN)"` |

## Detailed Changes

### Change 1: BalanceSheet.razor — UI heading
```razor
<!-- OLD -->
<h1>Bảng Cân Đối Kế Toán</h1>
<!-- NEW -->
<h1>Báo Cáo Tình Hình Tài Chính (B 01-DN)</h1>
```

### Change 2: BalanceSheet.razor — loading message
```razor
<!-- OLD -->
<p>Đang tạo Bảng Cân Đối Kế Toán cho kỳ @selectedMonth/@selectedYear...</p>
<!-- NEW -->
<p>Đang tạo Báo Cáo Tình Hình Tài Chính cho kỳ @selectedMonth/@selectedYear...</p>
```

### Change 3: BalanceSheet.razor — error message
```csharp
// OLD
errorMessage = "Không thể tạo Bảng Cân Đối Kế Toán. Vui lòng thử lại.";
// NEW
errorMessage = "Không thể tạo Báo Cáo Tình Hình Tài Chính. Vui lòng thử lại.";
```

### Change 4: BalanceSheet.razor — export title (ExportDocx + ExportXlsx)
```csharp
// OLD
return await ExportService.ExportStatementToDocxAsync("Bảng Cân Đối Kế Toán", ...);
// NEW
return await ExportService.ExportStatementToDocxAsync("Báo Cáo Tình Hình Tài Chính (B 01-DN)", ...);
```

### Change 5: FinancialReports.razor — hub card
```razor
<!-- OLD -->
<a href="/accounting/balance-sheet" class="report-link-card">
    📊 Bảng Cân Đối Kế Toán
</a>
<!-- NEW -->
<a href="/accounting/balance-sheet" class="report-link-card">
    📊 Báo Cáo Tình Hình Tài Chính (B 01-DN)
</a>
```

### Change 6: Domain.cs — comment only
```csharp
// OLD
// ── 1. BẢNG CÂN ĐỐI KẾ TOÁN (Mẫu B01-DN / B01-DNN) ──
// NEW
// ── 1. BÁO CÁO TÌNH HÌNH TÀI CHÍNH (Mẫu B01-DN / B01-DNN) ──
```

## Verification
- [ ] `dotnet build VanAn.sln` Release — 0 errors
- [ ] `guard-check.ps1` — PASS
- [ ] UI: `/accounting/balance-sheet` hiển thị "Báo Cáo Tình Hình Tài Chính (B 01-DN)"
- [ ] UI: `/accounting/financial-reports` hub card hiển thị tên mới
- [ ] Export: DOCX/XLSX title là "Báo Cáo Tình Hình Tài Chính (B 01-DN)"

## Rollback
`git revert <commit>` — chỉ đổi text, không phá logic.
