using VanAn.Shared.Domain;

namespace VanAn.CoreHub.Services.Data;

/// <summary>
/// TT 133/2016/TT-BTC report templates — VERIFIED from Phụ lục 2 (biểu mẫu BCTC).
/// Spec extract: docs/AI/tasks/tt133_tt58_compliance/spec/tt133-pl2-bctc-forms.txt
///   B01a-DNN: Báo cáo tình hình tài chính (mẫu BẮT BUỘC — B01b-DNN tùy chọn, defer)
///   B02-DNN:  Báo cáo kết quả hoạt động kinh doanh
///   B03-DNN:  Báo cáo lưu chuyển tiền tệ (khuyến khích — direct + indirect)
/// Reuses Tt99ReportTemplate/Tt99TemplateLine (Mã số → AccountCodes → hierarchy).
/// Điểm KHÁC TT 99: tổng TS = Mã 200 (không phải 280) · tổng NV = Mã 500 (không phải 440)
/// · NetIncome plug vào Mã 417 (không phải 420) · B02 mã 24 gộp CP QLKD (không tách 641/642)
/// · mã 51 gộp CP thuế TNDN (không tách 821/822).
/// Limitations (không tách số dư theo bên — giống caveat TT 71):
///   - Mã 132 (trả trước người bán) = số dư Nợ chi tiết 331 → hệ thống không tách bên → 0.
///   - Mã 312 (người mua trả trước) = số dư Có chi tiết 131 → 0.
///   - TK 229 dùng chung cho mã 124/136/142 → gán về 136 (phải thu khó đòi — dùng phổ biến nhất).
///   - BĐSĐT hao mòn dùng TK con 2147 — prefix match không trùng 214 của TSCĐ.
/// </summary>
public static class Tt133Templates
{
    // ════════════════════════════════════════════════════════════════════════════
    // B 01a-DNN: Báo cáo tình hình tài chính (VERIFIED from Phụ lục 2 TT 133)
    // ════════════════════════════════════════════════════════════════════════════
    public static Tt99ReportTemplate BalanceSheetTt133 => new(
        AccountingStandard.TT133_2016, "B01a-DNN",
        new List<Tt99TemplateLine>
        {
            // TÀI SẢN
            new("110", "I. Tiền và các khoản tương đương tiền", 1, new[]{"111","112"}, true, false),
            new("120", "II. Đầu tư tài chính", 1, Array.Empty<string>(), true, false),
            new("121", "1. Chứng khoán kinh doanh", 2, new[]{"121"}, false, false),
            new("122", "2. Đầu tư nắm giữ đến ngày đáo hạn", 2, new[]{"128"}, false, false),
            new("123", "3. Đầu tư góp vốn vào đơn vị khác", 2, new[]{"228"}, false, false),
            new("124", "4. Dự phòng tổn thất đầu tư tài chính (*)", 2, Array.Empty<string>(), false, true),
            new("130", "III. Các khoản phải thu", 1, Array.Empty<string>(), true, false),
            new("131", "1. Phải thu của khách hàng", 2, new[]{"131"}, false, false),
            new("132", "2. Trả trước cho người bán", 2, Array.Empty<string>(), false, false),
            new("133", "3. Vốn kinh doanh ở đơn vị trực thuộc", 2, Array.Empty<string>(), false, false),
            new("134", "4. Phải thu khác", 2, new[]{"136","138","141"}, false, false),
            new("135", "5. Tài sản thiếu chờ xử lý", 2, Array.Empty<string>(), false, false),
            new("136", "6. Dự phòng phải thu khó đòi (*)", 2, new[]{"229"}, false, true),
            new("140", "IV. Hàng tồn kho", 1, Array.Empty<string>(), true, false),
            new("141", "1. Hàng tồn kho", 2, new[]{"151","152","153","154","155","156","157"}, false, false),
            new("142", "2. Dự phòng giảm giá hàng tồn kho (*)", 2, Array.Empty<string>(), false, true),
            new("150", "V. Tài sản cố định", 1, Array.Empty<string>(), true, false),
            new("151", "- Nguyên giá", 2, new[]{"211"}, false, false),
            new("152", "- Giá trị hao mòn lũy kế (*)", 2, new[]{"214"}, false, true),
            new("160", "VI. Bất động sản đầu tư", 1, Array.Empty<string>(), true, false),
            new("161", "- Nguyên giá", 2, new[]{"217"}, false, false),
            new("162", "- Giá trị hao mòn lũy kế (*)", 2, new[]{"2147"}, false, true),
            new("170", "VII. Xây dựng cơ bản dở dang", 1, new[]{"241"}, false, false),
            new("180", "VIII. Tài sản khác", 1, Array.Empty<string>(), true, false),
            new("181", "1. Thuế GTGT được khấu trừ", 2, new[]{"133"}, false, false),
            new("182", "2. Tài sản khác", 2, new[]{"242"}, false, false),
            new("200", "TỔNG CỘNG TÀI SẢN (200=110+120+130+140+150+160+170+180)", 0, Array.Empty<string>(), true, false),
            // NGUỒN VỐN
            new("300", "I. Nợ phải trả", 0, Array.Empty<string>(), true, false),
            new("311", "1. Phải trả người bán", 1, new[]{"331"}, false, true),
            new("312", "2. Người mua trả tiền trước", 1, Array.Empty<string>(), false, true),
            new("313", "3. Thuế và các khoản phải nộp Nhà nước", 1, new[]{"333"}, false, true),
            new("314", "4. Phải trả người lao động", 1, new[]{"334"}, false, true),
            new("315", "5. Phải trả khác", 1, new[]{"335","338"}, false, true),
            new("316", "6. Vay và nợ thuê tài chính", 1, new[]{"341"}, false, true),
            new("317", "7. Phải trả nội bộ về vốn kinh doanh", 1, new[]{"336"}, false, true),
            new("318", "8. Dự phòng phải trả", 1, new[]{"352"}, false, true),
            new("319", "9. Quỹ khen thưởng, phúc lợi", 1, new[]{"353"}, false, true),
            new("320", "10. Quỹ phát triển khoa học và công nghệ", 1, new[]{"356"}, false, true),
            new("400", "II. Vốn chủ sở hữu", 0, Array.Empty<string>(), true, false),
            new("411", "1. Vốn góp của chủ sở hữu", 1, new[]{"411"}, false, true),
            new("412", "2. Thặng dư vốn cổ phần", 1, Array.Empty<string>(), false, true),
            new("413", "3. Vốn khác của chủ sở hữu", 1, Array.Empty<string>(), false, true),
            new("414", "4. Cổ phiếu quỹ (*)", 1, new[]{"419"}, false, true),
            new("415", "5. Chênh lệch tỷ giá hối đoái", 1, new[]{"413"}, false, true),
            new("416", "6. Các quỹ thuộc vốn chủ sở hữu", 1, new[]{"418"}, false, true),
            new("417", "7. Lợi nhuận sau thuế chưa phân phối", 1, new[]{"421"}, false, true),
            new("500", "TỔNG CỘNG NGUỒN VỐN (500=300+400)", 0, Array.Empty<string>(), true, false),
        });

    // ════════════════════════════════════════════════════════════════════════════
    // B 02-DNN: Báo cáo kết quả hoạt động kinh doanh (VERIFIED from Phụ lục 2 TT 133)
    // Formulas: 10=01-02 · 20=10-11 · 30=20+21-22-24 · 40=31-32 · 50=30+40 · 60=50-51
    // ════════════════════════════════════════════════════════════════════════════
    public static Tt99ReportTemplate IncomeStatementTt133 => new(
        AccountingStandard.TT133_2016, "B02-DNN",
        new List<Tt99TemplateLine>
        {
            new("01", "1. Doanh thu bán hàng và cung cấp dịch vụ", 1, new[]{"511"}, false, false),
            new("02", "2. Các khoản giảm trừ doanh thu", 1, Array.Empty<string>(), false, false),
            new("10", "3. Doanh thu thuần về bán hàng và CC DV (10 = 01 - 02)", 1, Array.Empty<string>(), true, false),
            new("11", "4. Giá vốn hàng bán", 1, new[]{"632"}, false, false),
            new("20", "5. Lợi nhuận gộp về bán hàng và CC DV (20 = 10 - 11)", 1, Array.Empty<string>(), true, false),
            new("21", "6. Doanh thu hoạt động tài chính", 1, new[]{"515"}, false, false),
            new("22", "7. Chi phí tài chính", 1, new[]{"635"}, false, false),
            new("23", "- Trong đó: Chi phí lãi vay", 2, Array.Empty<string>(), false, false),
            new("24", "8. Chi phí quản lý kinh doanh", 1, new[]{"642"}, false, false),
            new("30", "9. Lợi nhuận thuần từ hoạt động kinh doanh (30 = 20 + 21 - 22 - 24)", 1, Array.Empty<string>(), true, false),
            new("31", "10. Thu nhập khác", 1, new[]{"711"}, false, false),
            new("32", "11. Chi phí khác", 1, new[]{"811"}, false, false),
            new("40", "12. Lợi nhuận khác (40 = 31 - 32)", 1, Array.Empty<string>(), true, false),
            new("50", "13. Tổng lợi nhuận kế toán trước thuế (50 = 30 + 40)", 1, Array.Empty<string>(), true, false),
            new("51", "14. Chi phí thuế TNDN", 1, new[]{"821"}, false, false),
            new("60", "15. Lợi nhuận sau thuế thu nhập doanh nghiệp (60 = 50 - 51)", 1, Array.Empty<string>(), true, false),
        });

    // ════════════════════════════════════════════════════════════════════════════
    // B 03-DNN: Báo cáo lưu chuyển tiền tệ — Direct method (VERIFIED from Phụ lục 2)
    // Mã số giống khung B03-DN TT99 — CashFlowStatementService adds subtotal lines.
    // ════════════════════════════════════════════════════════════════════════════
    public static Tt99ReportTemplate CashFlowDirectTt133 => new(
        AccountingStandard.TT133_2016, "B03-DNN-Direct",
        new List<Tt99TemplateLine>
        {
            new("01", "1. Tiền thu từ bán hàng, CC DV và doanh thu khác", 2, Array.Empty<string>(), false, false),
            new("02", "2. Tiền chi trả cho người cung cấp hàng hóa, DV", 2, Array.Empty<string>(), false, true),
            new("03", "3. Tiền chi trả cho người lao động", 2, Array.Empty<string>(), false, true),
            new("04", "4. Tiền lãi vay đã trả", 2, Array.Empty<string>(), false, true),
            new("05", "5. Thuế TNDN đã nộp", 2, Array.Empty<string>(), false, true),
            new("06", "6. Tiền thu khác từ HĐKD", 2, Array.Empty<string>(), false, false),
            new("07", "7. Tiền chi khác cho HĐKD", 2, Array.Empty<string>(), false, true),
            new("20", "Lưu chuyển tiền thuần từ HĐKD", 1, Array.Empty<string>(), true, false),
            new("21", "1. Tiền chi mua sắm, xây dựng TSCĐ, BĐSĐT và TSDH khác", 2, Array.Empty<string>(), false, true),
            new("22", "2. Tiền thu từ thanh lý, nhượng bán TSCĐ, BĐSĐT và TSDH khác", 2, Array.Empty<string>(), false, false),
            new("23", "3. Tiền chi cho vay, đầu tư góp vốn vào đơn vị khác", 2, Array.Empty<string>(), false, true),
            new("24", "4. Tiền thu hồi cho vay, đầu tư góp vốn vào đơn vị khác", 2, Array.Empty<string>(), false, false),
            new("25", "5. Tiền thu lãi cho vay, cổ tức và lợi nhuận được chia", 2, Array.Empty<string>(), false, false),
            new("30", "Lưu chuyển tiền thuần từ HĐ đầu tư", 1, Array.Empty<string>(), true, false),
            new("31", "1. Tiền thu từ phát hành cổ phiếu, nhận vốn góp của CSH", 2, Array.Empty<string>(), false, false),
            new("32", "2. Tiền trả lại vốn góp, mua lại CP của DN đã phát hành", 2, Array.Empty<string>(), false, true),
            new("33", "3. Tiền thu từ đi vay", 2, Array.Empty<string>(), false, false),
            new("34", "4. Tiền trả nợ gốc vay và nợ thuê tài chính", 2, Array.Empty<string>(), false, true),
            new("35", "5. Cổ tức, lợi nhuận đã trả cho chủ sở hữu", 2, Array.Empty<string>(), false, true),
            new("40", "Lưu chuyển tiền thuần từ HĐ tài chính", 1, Array.Empty<string>(), true, false),
            new("50", "Lưu chuyển tiền thuần trong kỳ (50 = 20 + 30 + 40)", 1, Array.Empty<string>(), true, false),
            new("60", "Tiền và tương đương tiền đầu kỳ", 1, Array.Empty<string>(), false, false),
            new("61", "Ảnh hưởng của thay đổi tỷ giá HĐ quy đổi ngoại tệ", 1, Array.Empty<string>(), false, false),
            new("70", "Tiền và tương đương tiền cuối kỳ (70 = 50 + 60 + 61)", 1, Array.Empty<string>(), true, false),
        });

    // ════════════════════════════════════════════════════════════════════════════
    // B 03-DNN: Indirect method — operating adjustments only
    // (Investing + Financing sections same as direct method)
    // ════════════════════════════════════════════════════════════════════════════
    public static Tt99ReportTemplate CashFlowIndirectAdjustmentsTt133 => new(
        AccountingStandard.TT133_2016, "B03-DNN-Indirect-Adjustments",
        new List<Tt99TemplateLine>
        {
            new("01", "1. Lợi nhuận trước thuế", 1, Array.Empty<string>(), false, false),
            new("03", "- Khấu hao TSCĐ và BĐSĐT", 2, new[]{"214"}, false, false),
            new("04", "- Các khoản dự phòng", 2, Array.Empty<string>(), false, false),
            new("05", "- Lãi, lỗ chênh lệch tỷ giá HĐ do đánh giá lại các khoản mục tiền tệ có gốc ngoại tệ", 2, Array.Empty<string>(), false, false),
            new("06", "- Lãi, lỗ từ hoạt động đầu tư, tài chính", 2, Array.Empty<string>(), false, false),
            new("07", "- Chi phí lãi vay", 2, Array.Empty<string>(), false, false),
            new("08", "- Các khoản điều chỉnh khác", 2, Array.Empty<string>(), false, false),
            new("20", "Lưu chuyển tiền thuần từ HĐKD", 1, Array.Empty<string>(), true, false),
        });
}
