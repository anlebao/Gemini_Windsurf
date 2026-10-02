using Microsoft.Extensions.Logging;
using VanAn.Shared.Domain;

namespace VanAn.CoreHub.Services;

/// <summary>
/// TT 99/2025/TT-BTC — Financial Statement Notes service implementation (Mẫu B 09-DN).
/// Generates textual notes per Phụ lục IV TT 99 structure:
///   Phần I: Đặc điểm hoạt động (9 sub-items) — pulls tenant info from TenantSettings
///   Phần II: Kỳ kế toán, đơn vị tiền tệ (2 sub-items)
///   Phần III: Chuẩn mực và chế độ kế toán (2 sub-items)
///   Phần IV: Chính sách kế toán (29 sub-items — TT 99 standard template)
///   Phần X: Sửa đổi, bổ sung (nếu có)
/// NOTE: Per official B 09-DN, NO need to inject BalanceSheet/Income/CashFlow services
/// — Phần IV is TEXT template, not data-driven from other reports.
/// </summary>
public class FinancialStatementNotesService : IFinancialStatementNotesService
{
    private readonly ITenantManagementService _tenantService;
    private readonly ILogger<FinancialStatementNotesService> _logger;

    public FinancialStatementNotesService(
        ITenantManagementService tenantService,
        ILogger<FinancialStatementNotesService> logger)
    {
        _tenantService = tenantService;
        _logger = logger;
    }

    public async Task<FinancialStatementNotes> GenerateAsync(
        TenantId tenantId, AccountingPeriod period, AccountingStandard standard, CancellationToken ct = default)
    {
        _logger.LogInformation("Generating Financial Statement Notes for tenant {TenantId} period {Period} standard {Standard}", tenantId, period, standard);

        // TT 71/2024 (HTX) — B09-HTX: cấu trúc riêng theo Phụ lục IV TT 71 (Mục I.3).
        if (standard == AccountingStandard.TT71_2024)
        {
            return await GenerateTt71NotesAsync(tenantId, period, ct).ConfigureAwait(false);
        }

        var tenant = await _tenantService.GetTenantByIdAsync(tenantId, ct);
        var settings = tenant?.Settings;

        // PHẦN I: Đặc điểm hoạt động của doanh nghiệp (9 sub-items)
        var phanI = new NoteSection("I", "Đặc điểm hoạt động của doanh nghiệp", 1, "", new[]
        {
            new NoteSection("I.1", "Hình thức sở hữu vốn", 2, settings?.LegalForm ?? "Chưa thiết lập", null),
            new NoteSection("I.2", "Lĩnh vực kinh doanh", 2, settings?.BusinessField ?? "Chưa thiết lập", null),
            new NoteSection("I.3", "Ngành nghề kinh doanh", 2, tenant?.DefaultIndustrySector?.ToString() ?? "Chưa thiết lập", null),
            new NoteSection("I.4", "Chu kỳ sản xuất, kinh doanh thông thường", 2, "01/01 - 31/12", null),
            new NoteSection("I.5", "Đặc điểm hoạt động trong năm tài chính", 2, "Bình thường", null),
            new NoteSection("I.6", "Cấu trúc doanh nghiệp", 2, "Đơn vị độc lập", null),
            new NoteSection("I.7", "Số lượng người lao động", 2, "Chưa cập nhật", null),
            new NoteSection("I.8", "Khả năng so sánh thông tin trên BCTC", 2, "Có so sánh được", null),
            new NoteSection("I.9", "Thuyết minh các thông tin khác theo quy định pháp luật", 2, "Không có", null),
        });

        // PHẦN II: Kỳ kế toán, đơn vị tiền tệ (2 sub-items)
        var phanII = new NoteSection("II", "Kỳ kế toán, đơn vị tiền tệ sử dụng trong kế toán", 1, "", new[]
        {
            new NoteSection("II.1", "Kỳ kế toán năm", 2, $"01/01/{period.Year} - 31/12/{period.Year}", null),
            new NoteSection("II.2", "Đơn vị tiền tệ sử dụng trong kế toán", 2, "VNĐ", null),
        });

        // PHẦN III: Chuẩn mực và Chế độ kế toán áp dụng (2 sub-items)
        var standardName = standard switch
        {
            AccountingStandard.TT99_2025 => "Thông tư 99/2025/TT-BTC",
            AccountingStandard.TT133_2016 => "Thông tư 133/2016/TT-BTC",
            AccountingStandard.TT58_2026 => "Thông tư 58/2026/TT-BTC",
            AccountingStandard.TT71_2024 => "Thông tư 71/2024/TT-BTC (Chế độ kế toán HTX)",
            _ => standard.ToString()
        };
        var phanIII = new NoteSection("III", "Chuẩn mực và Chế độ kế toán áp dụng", 1, "", new[]
        {
            new NoteSection("III.1", "Chế độ kế toán áp dụng", 2, standardName, null),
            new NoteSection("III.2", "Tuyên bố về việc tuân thủ Chuẩn mực kế toán VN và Chế độ kế toán", 2, "Tuân thủ đầy đủ Chuẩn mực kế toán Việt Nam và Chế độ kế toán", null),
        });

        // PHẦN IV: Các chính sách kế toán (29 sub-items — full TT 99 template)
        var phanIV = new NoteSection("IV", "Các chính sách kế toán, ước tính kế toán và các quy định pháp luật có liên quan áp dụng", 1, "", new[]
        {
            new NoteSection("IV.1", "Nguyên tắc chuyển đổi BCTC lập bằng ngoại tệ sang VNĐ", 2, "Không áp dụng (đồng tiền ghi sổ = VNĐ)", null),
            new NoteSection("IV.2", "Các loại tỷ giá hối đoái áp dụng", 2, "Tỷ giá giao dịch thực tế", null),
            new NoteSection("IV.3", "Nguyên tắc xác định lãi suất thực tế", 2, "Lãi suất hiệu lực", null),
            new NoteSection("IV.4", "Nguyên tắc ghi nhận các khoản tiền và tương đương tiền", 2, "Theo TT 99/2025/TT-BTC", null),
            new NoteSection("IV.5", "Nguyên tắc kế toán các khoản đầu tư tài chính", 2, "Theo TT 99/2025/TT-BTC", null),
            new NoteSection("IV.6", "Nguyên tắc kế toán nợ phải thu", 2, "Theo TT 99/2025/TT-BTC", null),
            new NoteSection("IV.7", "Nguyên tắc kế toán hàng tồn kho", 2, "FIFO", null),
            new NoteSection("IV.8", "Nguyên tắc kế toán và khấu hao TSCĐ (bao gồm BĐS đầu tư)", 2, "Đường thẳng", null),
            new NoteSection("IV.9", "Nguyên tắc kế toán tài sản sinh học", 2, "Theo TT 99 Phụ lục IV", null),
            new NoteSection("IV.10", "Nguyên tắc kế toán các loại hợp đồng hợp tác kinh doanh", 2, "Không áp dụng", null),
            new NoteSection("IV.11", "Nguyên tắc kế toán chi phí chờ phân bổ", 2, "Theo TT 99/2025/TT-BTC", null),
            new NoteSection("IV.12", "Nguyên tắc kế toán phải trả người bán", 2, "Theo TT 99/2025/TT-BTC", null),
            new NoteSection("IV.13", "Nguyên tắc kế toán phải trả cổ tức, lợi nhuận", 2, "Theo TT 99/2025/TT-BTC (TK 332)", null),
            new NoteSection("IV.14", "Nguyên tắc ghi nhận chi phí phải trả", 2, "Theo TT 99/2025/TT-BTC", null),
            new NoteSection("IV.15", "Nguyên tắc ghi nhận doanh thu chờ phân bổ", 2, "Theo TT 99/2025/TT-BTC", null),
            new NoteSection("IV.16", "Nguyên tắc kế toán các khoản dự phòng phải trả", 2, "Theo TT 99/2025/TT-BTC", null),
            new NoteSection("IV.17", "Nguyên tắc kế toán thuế TNDN hoãn lại", 2, "Theo TT 99/2025/TT-BTC", null),
            new NoteSection("IV.18", "Nguyên tắc ghi nhận vay và nợ thuê tài chính", 2, "Theo TT 99/2025/TT-BTC", null),
            new NoteSection("IV.19", "Nguyên tắc ghi nhận và vốn hóa các khoản chi phí đi vay", 2, "Theo TT 99/2025/TT-BTC", null),
            new NoteSection("IV.20", "Nguyên tắc ghi nhận trái phiếu chuyển đổi", 2, "Không áp dụng", null),
            new NoteSection("IV.21", "Nguyên tắc ghi nhận vốn chủ sở hữu", 2, settings?.CharterCapital.HasValue == true
                ? $"Vốn điều lệ: {settings.CharterCapital.Value:N0} VNĐ"
                : "Theo TT 99/2025/TT-BTC", null),
            new NoteSection("IV.22", "Nguyên tắc và phương pháp ghi nhận doanh thu, thu nhập khác (bao gồm Doanh thu bán BĐSĐT)", 2, "Theo TT 99/2025/TT-BTC", null),
            new NoteSection("IV.23", "Nguyên tắc kế toán các khoản giảm trừ doanh thu", 2, "Theo TT 99/2025/TT-BTC", null),
            new NoteSection("IV.24", "Nguyên tắc kế toán giá vốn hàng bán", 2, "Theo TT 99/2025/TT-BTC", null),
            new NoteSection("IV.25", "Nguyên tắc kế toán chi phí tài chính", 2, "Theo TT 99/2025/TT-BTC", null),
            new NoteSection("IV.26", "Nguyên tắc kế toán chi phí bán hàng, chi phí quản lý doanh nghiệp", 2, "Theo TT 99/2025/TT-BTC", null),
            new NoteSection("IV.27", "Nguyên tắc kế toán bán, thanh lý TSCĐ, BĐS đầu tư", 2, "Theo TT 99/2025/TT-BTC", null),
            new NoteSection("IV.28", "Nguyên tắc và phương pháp ghi nhận chi phí thuế TNDN hiện hành (bao gồm thuế tối thiểu toàn cầu), TNDN hoãn lại", 2, "Theo TT 99/2025/TT-BTC", null),
            new NoteSection("IV.29", "Các nguyên tắc và phương pháp kế toán khác", 2, "Không có", null),
        });

        // PHẦN X: Sửa đổi, bổ sung (nếu có)
        var phanX = new NoteSection("X", "Những nội dung sửa đổi, bổ sung biểu mẫu, tên và nội dung các chỉ tiêu của BCTC so với biểu mẫu BCTC được Bộ Tài chính quy định (nếu có)", 1, "Không có sửa đổi, bổ sung", null);

        return new FinancialStatementNotes(tenantId, period, DateTime.UtcNow, standard,
            new[] { phanI, phanII, phanIII, phanIV, phanX });
    }

    /// <summary>
    /// TT 71/2024/TT-BTC — B09-HTX (Bản thuyết minh BCTC của HTX) theo Phụ lục IV Mục I.3:
    ///   I. Đặc điểm hoạt động của HTX · II. Kỳ kế toán, đơn vị tiền tệ · III. Chế độ kế toán áp dụng
    ///   IV. Thông tin bổ sung cho B01-HTX (13 mục) · V. Thông tin bổ sung cho B02-HTX
    ///   VI. Những thông tin khác mà HTX cần thuyết minh
    /// Cấu trúc TEXT template (như B09-DN) — chi tiết bảng IV/V theo dõi riêng theo yêu cầu quản lý.
    /// </summary>
    private async Task<FinancialStatementNotes> GenerateTt71NotesAsync(
        TenantId tenantId, AccountingPeriod period, CancellationToken ct)
    {
        var tenant = await _tenantService.GetTenantByIdAsync(tenantId, ct);
        var settings = tenant?.Settings;

        // PHẦN I: Đặc điểm hoạt động của HTX (2 mục)
        var phanI = new NoteSection("I", "Đặc điểm hoạt động của HTX", 1, "", new[]
        {
            new NoteSection("I.1", "Lĩnh vực kinh doanh", 2, settings?.BusinessField ?? "Chưa thiết lập", null),
            new NoteSection("I.2", "Ngành nghề kinh doanh", 2, tenant?.DefaultIndustrySector?.ToString() ?? "Chưa thiết lập", null),
        });

        // PHẦN II: Kỳ kế toán, đơn vị tiền tệ (2 mục)
        var phanII = new NoteSection("II", "Kỳ kế toán, đơn vị tiền tệ sử dụng trong kế toán", 1, "", new[]
        {
            new NoteSection("II.1", "Kỳ kế toán năm (ngày bắt đầu - ngày kết thúc)", 2, $"01/01/{period.Year} - 31/12/{period.Year}", null),
            new NoteSection("II.2", "Đơn vị tiền tệ sử dụng trong kế toán", 2, "VNĐ (Đồng Việt Nam)", null),
        });

        // PHẦN III: Chế độ kế toán áp dụng (1 mục)
        var phanIII = new NoteSection("III", "Chế độ kế toán áp dụng", 1,
            "Chế độ kế toán HTX ban hành kèm theo Thông tư 71/2024/TT-BTC ngày 07/10/2024 của Bộ Tài chính", null);

        // PHẦN IV: Thông tin bổ sung cho các khoản mục B01-HTX (13 mục — text template)
        var phanIV = new NoteSection("IV", "Thông tin bổ sung cho các khoản mục trình bày trong Báo cáo tình hình tài chính", 1, "", new[]
        {
            new NoteSection("IV.1", "Tiền", 2, "Chi tiết theo dõi riêng theo yêu cầu quản lý", null),
            new NoteSection("IV.2", "Các khoản đầu tư tài chính", 2, "Chi tiết theo dõi riêng theo yêu cầu quản lý", null),
            new NoteSection("IV.3", "Các khoản phải thu", 2, "Chi tiết theo dõi riêng theo yêu cầu quản lý", null),
            new NoteSection("IV.4", "Phải thu của hoạt động cho vay nội bộ", 2, "Chi tiết theo dõi riêng theo yêu cầu quản lý", null),
            new NoteSection("IV.5", "Hàng tồn kho", 2, "Chi tiết theo dõi riêng theo yêu cầu quản lý", null),
            new NoteSection("IV.6", "Tài sản cố định", 2, "Chi tiết theo dõi riêng theo yêu cầu quản lý", null),
            new NoteSection("IV.7", "Tài sản chung không chia", 2, "Chi tiết theo dõi riêng theo yêu cầu quản lý", null),
            new NoteSection("IV.8", "Phải trả của hoạt động tín dụng nội bộ", 2, "Chỉ áp dụng cho HTX còn hợp đồng TDNB ký trước 01/9/2023 còn hiệu lực", null),
            new NoteSection("IV.9", "Thuế và các khoản phải nộp Nhà nước", 2, "Chi tiết theo từng loại thuế", null),
            new NoteSection("IV.10", "Phải trả khác", 2, "Chi tiết theo yêu cầu quản lý", null),
            new NoteSection("IV.11", "Quỹ chung không chia", 2, "Chi tiết theo từng loại quỹ chung không chia và nguồn hình thành tài sản chung không chia", null),
            new NoteSection("IV.12", "Thuyết minh thông tin về các TK ngoài bảng (loại 0)", 2, "001-008 theo Phụ lục I TT 71", null),
            new NoteSection("IV.13", "Các thông tin khác do HTX tự thuyết minh, giải trình", 2, "Không có", null),
        });

        // PHẦN V: Thông tin bổ sung cho các khoản mục B02-HTX (4 mục)
        var phanV = new NoteSection("V", "Thông tin bổ sung cho các khoản mục trình bày trong Báo cáo kết quả hoạt động kinh doanh", 1, "", new[]
        {
            new NoteSection("V.1", "Doanh thu của hoạt động sản xuất kinh doanh (giao dịch nội bộ/ngoài)", 2, "Chi tiết theo dõi riêng theo yêu cầu quản lý", null),
            new NoteSection("V.2", "Giá vốn hàng bán (giao dịch nội bộ/ngoài)", 2, "Chi tiết theo dõi riêng theo yêu cầu quản lý", null),
            new NoteSection("V.3", "Các khoản giảm trừ doanh thu", 2, "Chi tiết theo dõi riêng theo yêu cầu quản lý", null),
            new NoteSection("V.4", "Chi phí quản lý kinh doanh", 2, "Chi tiết theo dõi riêng theo yêu cầu quản lý", null),
        });

        // PHẦN VI: Những thông tin khác mà HTX cần thuyết minh
        var phanVI = new NoteSection("VI", "Những thông tin khác mà HTX cần thuyết minh", 1, "Không có", null);

        return new FinancialStatementNotes(tenantId, period, DateTime.UtcNow, AccountingStandard.TT71_2024,
            new[] { phanI, phanII, phanIII, phanIV, phanV, phanVI });
    }
}
