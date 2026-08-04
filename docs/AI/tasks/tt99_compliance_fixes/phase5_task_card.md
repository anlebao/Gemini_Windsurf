# TASK CARD — Phase 5: B 09-DN Bản Thuyết Minh BCTC

> **Status:** ✅ COMPLETE — Wave 4b commit `51738298`
> **Priority:** P1 — Missing mandatory report
> **Branch:** `feature/tt99-fix-phase5-b09dn-thuyet-minh`
> **Estimated sessions:** 2-3
> **Mode:** IMPLEMENT
> **Domain modification:** YES — new `FinancialStatementNotes` record

## Objective
B 09-DN (Bản thuyết minh BCTC) là 1 trong 4 báo cáo bắt buộc theo TT 99/2025/TT-BTC. Codebase hiện THIẾU hoàn toàn. Cần tạo domain record + service + UI page + export.

**Đặc thù B 09-DN:** Khác 3 báo cáo kia (dạng bảng số liệu), B 09-DN là **báo cáo dạng văn bản thuyết minh** — gồm các sections文字 mô tả chính sách kế toán, tình hình tài chính, giải thích chỉ tiêu BCTC.

## Cấu trúc B 09-DN theo TT 99 (Phụ lục IV — VERIFIED)
> **Source:** `REFERENCE_B09DN_official.md` — https://vplsdms.vn/ban-thuyet-minh-bao-cao-tai-chinh-nam-cua-doanh-nghiep-dap-ung-gia-dinh-hoat-dong-lien-tuc
> **CRITICAL:** Original task card's 5-phần structure (I-V) was WRONG. Official TT 99 has 5 sections: I, II, III, IV, X — different content.

```
PHẦN I: ĐẶC ĐIỂM HOẠT ĐỘNG CỦA DOANH NGHIỆP (9 sub-items)
  1. Hình thức sở hữu vốn
  2. Lĩnh vực kinh doanh
  3. Ngành nghề kinh doanh
  4. Chu kỳ sản xuất, kinh doanh thông thường
  5. Đặc điểm hoạt động của doanh nghiệp trong năm tài chính có ảnh hưởng đến BCTC
  6. Cấu trúc doanh nghiệp (công ty con, liên doanh, đơn vị trực thuộc)
  7. Số lượng người lao động
  8. Tuyên bố về khả năng so sánh thông tin trên BCTC
  9. Thuyết minh các thông tin khác theo quy định pháp luật

PHẦN II: KỲ KẾ TOÁN, ĐƠN VỊ TIỀN TỆ SỬ DỤNG TRONG KẾ TOÁN
  1. Kỳ kế toán năm (bắt đầu.../... kết thúc.../...)
  2. Đơn vị tiền tệ sử dụng trong kế toán

PHẦN III: CHUẨN MỰC VÀ CHẾ ĐỘ KẾ TOÁN ÁP DỤNG
  1. Chế độ kế toán áp dụng
  2. Tuyên bố về việc tuân thủ Chuẩn mực kế toán VN và Chế độ kế toán

PHẦN IV: CÁC CHÍNH SÁCH KẾ TOÁN, ƯỚC TÍNH KẾ TOÁN VÀ CÁC QUY ĐỊNH PHÁP LUẬT CÓ LIÊN QUAN ÁP DỤNG (29 sub-items)
  1. Nguyên tắc chuyển đổi BCTC lập bằng ngoại tệ sang VNĐ
  2. Các loại tỷ giá hối đoái áp dụng (NEW TT 99)
  3. Nguyên tắc xác định lãi suất thực tế
  4. Nguyên tắc ghi nhận các khoản tiền và tương đương tiền
  5. Nguyên tắc kế toán các khoản đầu tư tài chính (a-e)
  6. Nguyên tắc kế toán nợ phải thu
  7. Nguyên tắc kế toán hàng tồn kho
  8. Nguyên tắc kế toán và khấu hao TSCĐ (bao gồm BĐS đầu tư)
  9. Nguyên tắc kế toán tài sản sinh học (NEW TT 99)
  10. Nguyên tắc kế toán các loại hợp đồng hợp tác kinh doanh
  11. Nguyên tắc kế toán chi phí chờ phân bổ
  12. Nguyên tắc kế toán phải trả người bán
  13. Nguyên tắc kế toán phải trả cổ tức, lợi nhuận (NEW TT 99 — TK 332)
  14. Nguyên tắc ghi nhận chi phí phải trả
  15. Nguyên tắc ghi nhận doanh thu chờ phân bổ
  16. Nguyên tắc kế toán các khoản dự phòng phải trả
  17. Nguyên tắc kế toán thuế TNDN hoãn lại
  18. Nguyên tắc ghi nhận vay và nợ thuê tài chính
  19. Nguyên tắc ghi nhận và vốn hóa các khoản chi phí đi vay
  20. Nguyên tắc ghi nhận trái phiếu chuyển đổi
  21. Nguyên tắc ghi nhận vốn chủ sở hữu
  22. Nguyên tắc và phương pháp ghi nhận doanh thu, thu nhập khác (bao gồm Doanh thu bán BĐSĐT — NEW TT 99)
  23. Nguyên tắc kế toán các khoản giảm trừ doanh thu
  24. Nguyên tắc kế toán giá vốn hàng bán
  25. Nguyên tắc kế toán chi phí tài chính
  26. Nguyên tắc kế toán chi phí bán hàng, chi phí quản lý doanh nghiệp
  27. Nguyên tắc kế toán bán, thanh lý TSCĐ, BĐS đầu tư
  28. Nguyên tắc và phương pháp ghi nhận chi phí thuế TNDN hiện hành (bao gồm thuế tối thiểu toàn cầu), TNDN hoãn lại
  29. Các nguyên tắc và phương pháp kế toán khác

PHẦN X: NHỮNG NỘI DUNG SỬA ĐỔI, BỔ SUNGBIỂU MẪU, TÊN VÀ NỘI DUNG CÁC CHỈ TIÊU CỦA BCTC SO VỚI BIỂU MẪU BCTC ĐƯỢC BỘ TÀI CHÍNH QUY ĐỊNH (NẾU CÓ)
  - Tên các chỉ tiêu có sửa đổi, bổ sung
  - Nội dung các chỉ tiêu có sửa đổi, bổ sung
  - Lý do thay đổi
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
    private readonly ILogger<FinancialStatementNotesService> _logger;

    // NOTE: Per official B 09-DN, NO need to inject BalanceSheet/Income/CashFlow services
    // — Phần IV (chính sách KT) is TEXT, not data-driven from other reports
    // Original task card's "Phần III: Tình hình TC" + "Phần IV: Giải thích chỉ tiêu" were WRONG
    // — these are NOT in official B 09-DN structure

    public async Task<FinancialStatementNotes> GenerateAsync(...)
    {
        var tenant = await _tenantService.GetTenantByIdAsync(tenantId, ct);

        // PHẦN I: Đặc điểm hoạt động (9 sub-items)
        var phanI = new NoteSection("I", "Đặc điểm hoạt động của doanh nghiệp", 1, "", new[]
        {
            new NoteSection("I.1", "Hình thức sở hữu vốn", 2, tenant.Settings.LegalForm ?? "Chưa thiết lập", null),
            new NoteSection("I.2", "Lĩnh vực kinh doanh", 2, tenant.Settings.BusinessField ?? "Chưa thiết lập", null),
            new NoteSection("I.3", "Ngành nghề kinh doanh", 2, tenant.DefaultIndustrySector?.ToString() ?? "Chưa thiết lập", null),
            new NoteSection("I.4", "Chu kỳ sản xuất, kinh doanh thông thường", 2, "01/01 - 31/12", null),
            new NoteSection("I.5", "Đặc điểm hoạt động trong năm tài chính", 2, "Bình thường", null),
            new NoteSection("I.6", "Cấu trúc doanh nghiệp", 2, "Đơn vị độc lập", null),
            new NoteSection("I.7", "Số lượng người lao động", 2, "Chưa cập nhật", null),
            new NoteSection("I.8", "Khả năng so sánh thông tin trên BCTC", 2, "Có so sánh được", null),
            new NoteSection("I.9", "Thuyết minh các thông tin khác", 2, "Không có", null),
        });

        // PHẦN II: Kỳ kế toán, đơn vị tiền tệ (2 sub-items)
        var phanII = new NoteSection("II", "Kỳ kế toán, đơn vị tiền tệ sử dụng trong kế toán", 1, "", new[]
        {
            new NoteSection("II.1", "Kỳ kế toán năm", 2, "01/01 - 31/12", null),
            new NoteSection("II.2", "Đơn vị tiền tệ sử dụng trong kế toán", 2, "VNĐ", null),
        });

        // PHẦN III: Chuẩn mực và Chế độ kế toán áp dụng (2 sub-items)
        var phanIII = new NoteSection("III", "Chuẩn mực và Chế độ kế toán áp dụng", 1, "", new[]
        {
            new NoteSection("III.1", "Chế độ kế toán áp dụng", 2, "Thông tư 99/2025/TT-BTC", null),
            new NoteSection("III.2", "Tuyên bố tuân thủ", 2, "Tuân thủ đầy đủ Chuẩn mực kế toán VN và Chế độ kế toán", null),
        });

        // PHẦN IV: Các chính sách kế toán (29 sub-items — full TT 99 template)
        var phanIV = new NoteSection("IV", "Các chính sách kế toán, ước tính kế toán và các quy định pháp luật có liên quan áp dụng", 1, "", new[]
        {
            new NoteSection("IV.1", "Nguyên tắc chuyển đổi BCTC lập bằng ngoại tệ sang VNĐ", 2, "Không áp dụng (đồng tiền ghi sổ = VNĐ)", null),
            new NoteSection("IV.2", "Các loại tỷ giá hối đoái áp dụng", 2, "Tỷ giá giao dịch thực tế", null),
            new NoteSection("IV.3", "Nguyên tắc xác định lãi suất thực tế", 2, "Lãi suất hiệu lực", null),
            new NoteSection("IV.4", "Nguyên tắc ghi nhận tiền và tương đương tiền", 2, "Theo TT 99/2025/TT-BTC", null),
            new NoteSection("IV.5", "Nguyên tắc kế toán đầu tư tài chính", 2, "Theo TT 99/2025/TT-BTC", null),
            new NoteSection("IV.6", "Nguyên tắc kế toán nợ phải thu", 2, "Theo TT 99/2025/TT-BTC", null),
            new NoteSection("IV.7", "Nguyên tắc kế toán hàng tồn kho", 2, "FIFO", null),
            new NoteSection("IV.8", "Nguyên tắc kế toán và khấu hao TSCĐ, BĐS đầu tư", 2, "Đường thẳng", null),
            new NoteSection("IV.9", "Nguyên tắc kế toán tài sản sinh học", 2, "Theo TT 99 Phụ lục IV", null),
            new NoteSection("IV.10", "Nguyên tắc kế toán hợp đồng Hợp tác kinh doanh", 2, "Không áp dụng", null),
            new NoteSection("IV.11", "Nguyên tắc kế toán chi phí chờ phân bổ", 2, "Theo TT 99/2025/TT-BTC", null),
            new NoteSection("IV.12", "Nguyên tắc kế toán phải trả người bán", 2, "Theo TT 99/2025/TT-BTC", null),
            new NoteSection("IV.13", "Nguyên tắc kế toán phải trả cổ tức, lợi nhuận", 2, "Theo TT 99/2025/TT-BTC (TK 332)", null),
            new NoteSection("IV.14", "Nguyên tắc ghi nhận chi phí phải trả", 2, "Theo TT 99/2025/TT-BTC", null),
            new NoteSection("IV.15", "Nguyên tắc ghi nhận doanh thu chờ phân bổ", 2, "Theo TT 99/2025/TT-BTC", null),
            new NoteSection("IV.16", "Nguyên tắc kế toán dự phòng phải trả", 2, "Theo TT 99/2025/TT-BTC", null),
            new NoteSection("IV.17", "Nguyên tắc kế toán thuế TNDN hoãn lại", 2, "Theo TT 99/2025/TT-BTC", null),
            new NoteSection("IV.18", "Nguyên tắc ghi nhận vay và nợ thuê tài chính", 2, "Theo TT 99/2025/TT-BTC", null),
            new NoteSection("IV.19", "Nguyên tắc ghi nhận và vốn hóa chi phí đi vay", 2, "Theo TT 99/2025/TT-BTC", null),
            new NoteSection("IV.20", "Nguyên tắc ghi nhận trái phiếu chuyển đổi", 2, "Không áp dụng", null),
            new NoteSection("IV.21", "Nguyên tắc ghi nhận vốn chủ sở hữu", 2, "Theo TT 99/2025/TT-BTC", null),
            new NoteSection("IV.22", "Nguyên tắc ghi nhận doanh thu, thu nhập khác", 2, "Theo TT 99/2025/TT-BTC (bao gồm BĐSĐT)", null),
            new NoteSection("IV.23", "Nguyên tắc kế toán giảm trừ doanh thu", 2, "Theo TT 99/2025/TT-BTC", null),
            new NoteSection("IV.24", "Nguyên tắc kế toán giá vốn hàng bán", 2, "Theo TT 99/2025/TT-BTC", null),
            new NoteSection("IV.25", "Nguyên tắc kế toán chi phí tài chính", 2, "Theo TT 99/2025/TT-BTC", null),
            new NoteSection("IV.26", "Nguyên tắc kế toán CP bán hàng, CP quản lý DN", 2, "Theo TT 99/2025/TT-BTC", null),
            new NoteSection("IV.27", "Nguyên tắc kế toán bán, thanh lý TSCĐ, BĐS đầu tư", 2, "Theo TT 99/2025/TT-BTC", null),
            new NoteSection("IV.28", "Nguyên tắc ghi nhận chi phí thuế TNDN", 2, "Theo TT 99/2025/TT-BTC (bao gồm thuế tối thiểu toàn cầu)", null),
            new NoteSection("IV.29", "Các nguyên tắc và phương pháp kế toán khác", 2, "Không có", null),
        });

        // PHẦN X: Sửa đổi, bổ sung (nếu có)
        var phanX = new NoteSection("X", "Những nội dung sửa đổi, bổ sung biểu mẫu, tên và nội dung các chỉ tiêu của BCTC so với biểu mẫu BCTC được Bộ Tài chính quy định (nếu có)", 1, "Không có sửa đổi, bổ sung", null);

        return new FinancialStatementNotes(tenantId, period, DateTime.UtcNow, standard,
            new[] { phanI, phanII, phanIII, phanIV, phanX });
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

---

## ANALYZE UPDATE (2026-08-03)

### Verified Accurate
- ✅ `FinancialStatementNotes` record does NOT exist (0 matches in 1_Shared/)
- ✅ `NoteSection` record does NOT exist
- ✅ `FinancialStatementNotesService` does NOT exist (0 matches in 3_CoreHub/)
- ✅ `FinancialStatementNotes.razor` does NOT exist (13 .razor files, none match)
- ✅ `ITenantManagementService.GetTenantByIdAsync` exists, returns `Task<Tenant?>`
- ✅ `FinancialReportExportService` uses per-report methods (Docx+Xlsx pairs) — need new `ExportNotesToDocxAsync` + `ExportNotesToXlsxAsync`
- ✅ Program.cs DI pattern: `AddScoped<I, Impl>()`
- ✅ `AccountingLayout.razor` NO menu change needed (hub already exists at `/accounting/financial-reports`)
- ✅ `FinancialReports.razor` is the hub — just add card

### 🔴 BLOCKER: Tenant Missing 3 Fields for Phần I
Task card lines 136-138 reference `tenant.LegalForm`, `tenant.BusinessField`, `tenant.CharterCapital` — **these DO NOT EXIST** on Tenant entity.

**Current Tenant properties:** Name, BusinessType, HKDGroup?, IndustrySector?, Type?, Status, Settings
**Current TenantSettings:** ContactEmail, ContactPhone, Address, TaxCode, Slug, BrandStory, etc.

**Fix: Add 3 properties to TenantSettings (Option B — no migration)**
```csharp
// TenantSettings — add
public string? LegalForm { get; set; }         // "Công ty TNHH"
public string? BusinessField { get; set; }     // "F&B", "Thương mại"
public decimal? CharterCapital { get; set; }   // VND
```

**Updated code snippet (Phần I):**
```csharp
new NoteSection("I.1", "Hình thức pháp lý", 2, tenant.Settings.LegalForm ?? "Chưa thiết lập", null),
new NoteSection("I.2", "Lĩnh vực kinh doanh", 2, tenant.Settings.BusinessField ?? "Chưa thiết lập", null),
new NoteSection("I.3", "Vốn điều lệ", 2, tenant.Settings.CharterCapital.HasValue ? $"{tenant.Settings.CharterCapital.Value:N0} VNĐ" : "Chưa thiết lập", null),
```

**Prerequisite:** Complete Phase 5a (TenantSettings extension) BEFORE Phase 5.
