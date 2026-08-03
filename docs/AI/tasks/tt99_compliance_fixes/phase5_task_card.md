# TASK CARD — Phase 5: B 09-DN Bản Thuyết Minh BCTC

> **Status:** 🟡 PLANNED
> **Priority:** P1 — Missing mandatory report
> **Branch:** `feature/tt99-fix-phase5-b09dn-thuyet-minh`
> **Estimated sessions:** 2-3
> **Mode:** IMPLEMENT
> **Domain modification:** YES — new `FinancialStatementNotes` record

## Objective
B 09-DN (Bản thuyết minh BCTC) là 1 trong 4 báo cáo bắt buộc theo TT 99/2025/TT-BTC. Codebase hiện THIẾU hoàn toàn. Cần tạo domain record + service + UI page + export.

**Đặc thù B 09-DN:** Khác 3 báo cáo kia (dạng bảng số liệu), B 09-DN là **báo cáo dạng văn bản thuyết minh** — gồm các sections文字 mô tả chính sách kế toán, tình hình tài chính, giải thích chỉ tiêu BCTC.

## Cấu trúc B 09-DN theo TT 99 (Phụ lục IV)
```
PHẦN I: ĐẶC ĐIỂM HOẠT ĐỘNG
  1. Hình thức pháp lý
  2. Lĩnh vực kinh doanh
  3. Vốn điều lệ
  4. Đặc điểm hoạt động

PHẦN II: CHÍNH SÁCH KẾ TOÁN ÁP DỤNG
  1. Chế độ kế toán áp dụng (TT 99/2025/TT-BTC)
  2. Năm tài chính
  3. Đơn vị tiền tệ
  4. Nguyên tắc ghi nhận: tài sản, nợ, doanh thu, chi phí
  5. Phương pháp khấu hao TSCĐ
  6. Phương pháp tính giá trị hàng tồn kho
  7. Nguyên tắc kế toán tài sản sinh học (NEW TT 99)
  8. Các loại tỷ giá áp dụng (NEW TT 99)
  9. Cơ sở xác định giá trị (NEW TT 99)

PHẦN III: TÌNH HÌNH TÀI CHÍNH VÀ KẾT QUẢ KINH DOANH
  1. Tình hình tài chính (phân tích ngắn gọn)
  2. Kết quả kinh doanh
  3. Lưu chuyển tiền tệ

PHẦN IV: GIẢI THÍCH CHỈ TIÊU BÁO CÁO TÀI CHÍNH
  1. Báo cáo tình hình tài chính (chi tiết từng chỉ tiêu B 01-DN)
  2. Báo cáo kết quả HĐKD (chi tiết B 02-DN)
  3. Báo cáo lưu chuyển tiền tệ (chi tiết B 03-DN)

PHẦN V: THÔNG TIN BỔ SUNG
  1. Tài sản thế chấp
  2. Cam kết tài chính
  3. Phạt tiền bồi thường
  4. Giao dịch với các bên liên quan
  5. Sai sót kỳ trước
```

## Prerequisites
- [ ] Verify `FinancialStatementNotes` record KHÔNG tồn tại (grep → 0 match)
- [ ] Verify `FinancialStatementNotesService` KHÔNG tồn tại
- [ ] Verify `FinancialStatementNotes.razor` KHÔNG tồn tại
- [ ] Verify `FinancialReports.razor` hiện 3 thẻ BCTC (sau Phase 2 tách TrialBalance)
- [ ] Verify `FinancialReportExportService` có pattern để thêm export method mới
- [ ] **Phase 1 + Phase 2 nên COMPLETE** (naming + hub structure)

## Files to Create/Modify
| File | Action | Description |
|------|--------|-------------|
| `1_Shared/Domain.cs` | MODIFY | Add `FinancialStatementNotes` record + `NoteSection` record |
| `3_CoreHub/Services/IFinancialStatementNotesService.cs` | NEW | Service interface |
| `3_CoreHub/Services/FinancialStatementNotesService.cs` | NEW | Service implementation |
| `5_WebApps/ShopERP/Components/Pages/Accounting/FinancialStatementNotes.razor` | NEW | UI page |
| `5_WebApps/ShopERP/Services/FinancialReportExportService.cs` | MODIFY | Add export DOCX/XLSX |
| `5_WebApps/ShopERP/Components/Pages/Accounting/FinancialReports.razor` | MODIFY | Add B 09-DN card |
| `5_WebApps/ShopERP/Program.cs` | MODIFY | DI register `FinancialStatementNotesService` |

## Detailed Changes

### Change 1: Domain — FinancialStatementNotes record
```csharp
// Domain.cs — add after CashFlowStatement (line 3356)
// ── 4. BẢN THUYẾT MINH BCTC (Mẫu B09-DN) ──────────────────────────────────────────
/// <summary>
/// B 09-DN: Bản thuyết minh BCTC — báo cáo dạng văn bản, không phải bảng số liệu.
/// Gồm 5 phần: Đặc điểm HĐ, Chính sách KT, Tình hình TC, Giải thích chỉ tiêu, TT bổ sung.
/// </summary>
public record NoteSection(
    string SectionCode,         // "I", "II", "III", "IV", "V"
    string SectionTitle,        // "ĐẶC ĐIỂM HOẠT ĐỘNG"
    int Level,                  // 1 = phần, 2 = mục con
    string Content,             // Nội dung thuyết minh (text)
    IEnumerable<NoteSection>? SubSections  // Mục con (VD: II.1, II.2...)
);

public record FinancialStatementNotes(
    TenantId TenantId,
    AccountingPeriod Period,
    DateTime GeneratedAt,
    AccountingStandard Standard,
    IEnumerable<NoteSection> Sections
);
```

### Change 2: Service interface
```csharp
// IFinancialStatementNotesService.cs (NEW)
using VanAn.Shared.Domain;

namespace VanAn.CoreHub.Services;

/// <summary>
/// VAS Wave — Financial Statement Notes service (Mẫu B09-DN).
/// Generates textual notes explaining BCTC indicators + accounting policies.
/// </summary>
public interface IFinancialStatementNotesService
{
    Task<FinancialStatementNotes> GenerateAsync(
        TenantId tenantId, AccountingPeriod period, AccountingStandard standard, CancellationToken ct = default);
}
```

### Change 3: Service implementation
```csharp
// FinancialStatementNotesService.cs (NEW)
public class FinancialStatementNotesService : IFinancialStatementNotesService
{
    private readonly IAccountingDbContext _dbContext;
    private readonly ITenantManagementService _tenantService;
    private readonly IBalanceSheetService _balanceSheetService;
    private readonly IIncomeStatementService _incomeStatementService;
    private readonly ICashFlowStatementService _cashFlowService;
    private readonly ILogger<FinancialStatementNotesService> _logger;

    // Inject all 3 report services to pull data for Phần IV (giải thích chỉ tiêu)

    public async Task<FinancialStatementNotes> GenerateAsync(...)
    {
        // Phần I: Đặc điểm HĐ — từ Tenant info (hình thức pháp lý, lĩnh vực, vốn điều lệ)
        var tenant = await _tenantService.GetTenantByIdAsync(tenantId, ct);
        var phanI = new NoteSection("I", "ĐẶC ĐIỂM HOẠT ĐỘNG", 1, "", new[]
        {
            new NoteSection("I.1", "Hình thức pháp lý", 2, tenant.LegalForm, null),
            new NoteSection("I.2", "Lĩnh vực kinh doanh", 2, tenant.BusinessField, null),
            new NoteSection("I.3", "Vốn điều lệ", 2, $"{tenant.CharterCapital:N0} VNĐ", null),
        });

        // Phần II: Chính sách KT — static template + tenant-specific overrides
        var phanII = new NoteSection("II", "CHÍNH SÁCH KẾ TOÁN ÁP DỤNG", 1, "", new[]
        {
            new NoteSection("II.1", "Chế độ kế toán", 2, "Thông tư 99/2025/TT-BTC", null),
            new NoteSection("II.2", "Năm tài chính", 2, "01/01 - 31/12", null),
            new NoteSection("II.3", "Đơn vị tiền tệ", 2, "VNĐ", null),
            new NoteSection("II.4", "Nguyên tắc ghi nhận", 2, "Theo TT 99/2025/TT-BTC", null),
            new NoteSection("II.5", "Phương pháp khấu hao TSCĐ", 2, "Đường thẳng", null),
            new NoteSection("II.6", "Phương pháp tính GTHTK", 2, "FIFO", null),
            new NoteSection("II.7", "Nguyên tắc KT tài sản sinh học", 2, "Theo TT 99 Phụ lục IV", null),
            new NoteSection("II.8", "Tỷ giá áp dụng", 2, "Tỷ giá giao dịch thực tế", null),
        });

        // Phần III: Tình hình TC — pull từ BalanceSheet + IncomeStatement + CashFlow
        var bs = await _balanceSheetService.GenerateAsync(tenantId, period, standard, ct);
        var is_ = await _incomeStatementService.GenerateAsync(tenantId, period, standard, ct);
        var cfs = await _cashFlowService.GenerateAsync(tenantId, period, standard, ct);
        var phanIII = new NoteSection("III", "TÌNH HÌNH TC & KẾT QUẢ KD", 1, "", new[]
        {
            new NoteSection("III.1", "Tình hình tài chính", 2,
                $"Tổng tài sản: {bs.TotalAssetsEnding:N0} VNĐ. Nợ phải trả + VCSH: {bs.TotalLiabilitiesAndEquityEnding:N0} VNĐ.", null),
            new NoteSection("III.2", "Kết quả kinh doanh", 2,
                $"Doanh thu: {is_.TotalRevenueEnding:N0} VNĐ. Lợi nhuận ròng: {is_.NetProfitEnding:N0} VNĐ.", null),
            new NoteSection("III.3", "Lưu chuyển tiền tệ", 2,
                $"Tiền đầu kỳ: {cfs.OpeningCash:N0} VNĐ. Tiền cuối kỳ: {cfs.ClosingCash:N0} VNĐ.", null),
        });

        // Phần IV: Giải thích chỉ tiêu — chi tiết từng chỉ tiêu B 01-DN, B 02-DN, B 03-DN
        var phanIV = new NoteSection("IV", "GIẢI THÍCH CHỈ TIÊU BCTC", 1, "", BuildSectionIV(bs, is_, cfs));

        // Phần V: TT bổ sung — từ commitments, contingencies (cần thêm domain entities nếu chưa có)
        var phanV = new NoteSection("V", "THÔNG TIN BỔ SUNG", 1, "", new[]
        {
            new NoteSection("V.1", "Tài sản thế chấp", 2, "Không có", null),
            new NoteSection("V.2", "Cam kết tài chính", 2, "Không có", null),
            new NoteSection("V.3", "Giao dịch với bên liên quan", 2, "Không có", null),
        });

        return new FinancialStatementNotes(tenantId, period, DateTime.UtcNow, standard,
            new[] { phanI, phanII, phanIII, phanIV, phanV });
    }
}
```

### Change 4: UI page
```razor
@page "/accounting/financial-statement-notes"
@rendermode InteractiveServer
@layout AccountingLayout
@attribute [Authorize(Policy = "OwnerOnly")]
@inject VanAn.CoreHub.Services.IFinancialStatementNotesService NotesService
@inject VanAn.ShopERP.Services.IFinancialReportExportService ExportService
@inject IThemeProvider ThemeProvider
@inject ITenantProvider TenantProvider

<div class="report-page @ThemeProvider.CurrentTheme">
    <header class="page-header">
        <h1>Bản Thuyết Minh BCTC (B 09-DN)</h1>
        <div class="header-actions">
            <VanAButton Variant="secondary" Size="small" OnClick="GenerateReport">🔄 Tạo thuyết minh</VanAButton>
            <VanAButton Variant="secondary" Size="small" OnClick="ExportDocx">📄 Xuất DOCX</VanAButton>
            <VanAButton Variant="secondary" Size="small" OnClick="ExportXlsx">📊 Xuất XLSX</VanAButton>
        </div>
    </header>

    @if (report != null)
    {
        @foreach (var section in report.Sections)
        {
            <VanACard Header="@($"{section.SectionCode}. {section.SectionTitle}")">
                @if (!string.IsNullOrEmpty(section.Content))
                {
                    <p>@section.Content</p>
                }
                @if (section.SubSections != null)
                {
                    <ul>
                        @foreach (var sub in section.SubSections)
                        {
                            <li><strong>@sub.SectionCode. @sub.SectionTitle:</strong> @sub.Content</li>
                        }
                    </ul>
                }
            </VanACard>
        }
    }
</div>
```

### Change 5: FinancialReports.razor — add B 09-DN card
```razor
<!-- Add after CashFlowStatement card -->
<a href="/accounting/financial-statement-notes" class="report-link-card">
    📝 Bản Thuyết Minh BCTC (B 09-DN)
</a>
```

### Change 6: DI registration
```csharp
// Program.cs
builder.Services.AddScoped<IFinancialStatementNotesService, FinancialStatementNotesService>();
```

## Verification
- [ ] `dotnet build VanAn.sln` Release — 0 errors
- [ ] UI `/accounting/financial-statement-notes` hiển thị 5 phần (I-V)
- [ ] Phần I hiển thị thông tin tenant (hình thức pháp lý, lĩnh vực, vốn)
- [ ] Phần III hiển thị tổng tài sản, doanh thu, lợi nhuận (pull từ 3 báo cáo)
- [ ] Export DOCX/XLSX hoạt động
- [ ] `FinancialReports.razor` có 4 thẻ BCTC (B 01 + B 02 + B 03 + B 09)

## Rollback
`git revert <commit>` — new report, không ảnh hưởng existing.

## Notes
- **INVESTIGATE:** Tenant entity hiện có những field nào? (LegalForm, BusinessField, CharterCapital) — có thể cần thêm field.
- **INVESTIGATE:** Phần V (TT bổ sung) cần entities cho tài sản thế chấp, cam kết — hiện có thể chưa có. Có thể hiển thị "Không có" mặc định cho MVP.
- **Phase 4 (template structure) nên COMPLETE trước** để Phần IV giải thích chỉ tiêu đúng cấu trúc TT 99.
