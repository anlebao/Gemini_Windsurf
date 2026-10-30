using VanAn.Shared.Domain;

namespace VanAn.CoreHub.Services.Data;

/// <summary>
/// TT 58/2026/TT-BTC report templates — VERIFIED from Điều 10 (BCTC doanh nghiệp siêu nhỏ).
/// Spec extract: docs/AI/tasks/tt133_tt58_compliance/spec/tt58-2026-full-text.txt
///   B01-DNSN: Báo cáo tình hình tài chính (14 mã — TS 110..200 / NV 300..500)
///   B02-DNSN: Báo cáo kết quả kinh doanh (5 mã — 01/02/03/10/20)
/// TT 58 KHÔNG yêu cầu B03, B09, F01. DN nộp TNDN % trên DT không bắt buộc NỘP BCTC
/// (vẫn lập được để xem — Điều 10.1b).
/// NetIncome plug vào Mã 420 (LNST chưa phân phối).
/// </summary>
public static class Tt58Templates
{
    // ════════════════════════════════════════════════════════════════════════════
    // B 01-DNSN (Điều 10.2a TT 58/2026)
    // ════════════════════════════════════════════════════════════════════════════
    public static Tt99ReportTemplate BalanceSheetTt58 => new(
        AccountingStandard.TT58_2026, "B01-DNSN",
        new List<Tt99TemplateLine>
        {
            // TÀI SẢN
            new("110", "I. Tiền", 1, new[]{"111","112"}, false, false),
            new("120", "II. Các khoản nợ phải thu", 1, new[]{"131","133","136","138","141"}, false, false),
            new("130", "III. Hàng tồn kho", 1, new[]{"151","152","153","154","155","156","157"}, false, false),
            // Mã 140 = nguyên giá 211 − hao mòn 214 (214 IsNormalCredit → negated trong compute).
            new("140", "IV. Tài sản cố định", 1, new[]{"211","214"}, false, false),
            new("150", "V. Tài sản khác", 1, new[]{"121","128","217","241","242","244"}, false, false),
            new("200", "TỔNG CỘNG TÀI SẢN (200=110+120+130+140+150)", 0, Array.Empty<string>(), true, false),
            // NGUỒN VỐN
            new("300", "I. Nợ phải trả (300=310+320)", 0, Array.Empty<string>(), true, false),
            new("310", "1. Các khoản nợ phải trả", 1, new[]{"331","334","335","336","338","341","352","353"}, false, true),
            new("320", "2. Thuế và các khoản phải nộp Nhà nước", 1, new[]{"333"}, false, true),
            new("400", "II. Vốn chủ sở hữu (400=410+420+430)", 0, Array.Empty<string>(), true, false),
            new("410", "1. Vốn đầu tư của chủ sở hữu", 1, new[]{"411"}, false, true),
            new("420", "2. Lợi nhuận sau thuế chưa phân phối", 1, new[]{"421"}, false, true),
            new("430", "3. Các quỹ thuộc vốn chủ sở hữu", 1, new[]{"418"}, false, true),
            new("500", "TỔNG CỘNG NGUỒN VỐN (500=300+400)", 0, Array.Empty<string>(), true, false),
        });

    // ════════════════════════════════════════════════════════════════════════════
    // B 02-DNSN (Điều 10.2b TT 58/2026)
    // Formulas: 03 = 01 - 02 · 20 = 03 - 10
    // ════════════════════════════════════════════════════════════════════════════
    public static Tt99ReportTemplate IncomeStatementTt58 => new(
        AccountingStandard.TT58_2026, "B02-DNSN",
        new List<Tt99TemplateLine>
        {
            new("01", "1. Doanh thu và thu nhập thuần", 1, new[]{"511","515","711"}, false, false),
            new("02", "2. Các khoản chi phí", 1, new[]{"632","635","642","811"}, false, false),
            new("03", "3. Lợi nhuận thuần trước thuế (03 = 01 - 02)", 1, Array.Empty<string>(), true, false),
            new("10", "4. Chi phí thuế thu nhập doanh nghiệp", 1, new[]{"821"}, false, false),
            new("20", "5. Lợi nhuận sau thuế (20 = 03 - 10)", 1, Array.Empty<string>(), true, false),
        });
}
