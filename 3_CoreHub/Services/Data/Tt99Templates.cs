using VanAn.Shared.Domain;

namespace VanAn.CoreHub.Services.Data;

/// <summary>
/// TT 99/2025/TT-BTC report templates — VERIFIED from official Phụ lục IV.
/// Sources:
///   B 01-DN: https://vplsdms.vn/bao-cao-tinh-hinh-tai-chinh-nam-cua-doanh-nghiep-dap-ung-gia-dinh-hoat-dong-lien-tuc
///   B 02-DN: https://vplsdms.vn/bao-cao-ket-qua-hoat-dong-kinh-doanh-nam
///   B 03-DN: https://vplsdms.vn/bao-cao-luu-chuyen-tien-te-nam
/// These templates define the hierarchical Mã số structure (100/110/111...) that groups
/// account codes into TT 99 report indicators. Services use these to generate reports
/// with the correct structure instead of flat account lists.
/// </summary>
public static class Tt99Templates
{
    // ════════════════════════════════════════════════════════════════════════════
    // B 01-DN: Báo cáo tình hình tài chính (VERIFIED from Phụ lục IV TT 99)
    // ════════════════════════════════════════════════════════════════════════════
    public static Tt99ReportTemplate BalanceSheetTt99 => new(
        AccountingStandard.TT99_2025, "B01-DN",
        new List<Tt99TemplateLine>
        {
            // A — TÀI SẢN NGẮN HẠN
            new("100", "A - TÀI SẢN NGẮN HẠN", 1, Array.Empty<string>(), true, false),
            new("110", "I. Tiền và các khoản tương đương tiền", 2, Array.Empty<string>(), true, false),
            new("111", "1. Tiền", 3, new[]{"111","112","113"}, false, false),
            new("112", "2. Các khoản tương đương tiền", 3, Array.Empty<string>(), false, false),
            new("120", "II. Đầu tư tài chính ngắn hạn", 2, Array.Empty<string>(), true, false),
            new("121", "1. Chứng khoán kinh doanh", 3, new[]{"121"}, false, false),
            new("122", "2. Dự phòng giảm giá CK kinh doanh (*)", 3, Array.Empty<string>(), false, true),
            new("123", "3. Đầu tư nắm giữ đến ngày đáo hạn ngắn hạn", 3, new[]{"128"}, false, false),
            new("124", "4. Dự phòng đầu tư nắm giữ NTN ngắn hạn (*)", 3, Array.Empty<string>(), false, true),
            new("125", "5. Đầu tư ngắn hạn khác", 3, Array.Empty<string>(), false, false),
            new("126", "6. Dự phòng tổn thất đầu tư ngắn hạn khác (*)", 3, Array.Empty<string>(), false, true),
            new("130", "III. Các khoản phải thu ngắn hạn", 2, Array.Empty<string>(), true, false),
            new("131", "1. Phải thu ngắn hạn của khách hàng", 3, new[]{"131"}, false, false),
            new("132", "2. Trả trước cho người bán ngắn hạn", 3, Array.Empty<string>(), false, false),
            new("133", "3. Phải thu nội bộ ngắn hạn", 3, new[]{"136"}, false, false),
            new("134", "4. Phải thu theo tiến độ HĐXD", 3, Array.Empty<string>(), false, false),
            new("135", "5. Phải thu ngắn hạn khác", 3, new[]{"138"}, false, false),
            new("136", "6. Dự phòng phải thu ngắn hạn khó đòi (*)", 3, Array.Empty<string>(), false, true),
            new("137", "7. Tài sản thiếu chờ xử lý", 3, Array.Empty<string>(), false, false),
            new("140", "IV. Hàng tồn kho", 2, Array.Empty<string>(), true, false),
            new("141", "1. Hàng tồn kho", 3, new[]{"152","153","155","156","157"}, false, false),
            new("142", "2. Dự phòng giảm giá hàng tồn kho (*)", 3, Array.Empty<string>(), false, true),
            new("150", "V. Tài sản sinh học ngắn hạn", 2, Array.Empty<string>(), true, false),
            new("151", "1. Súc vật nuôi lấy sản phẩm một lần ngắn hạn", 3, new[]{"215"}, false, false),
            new("152", "2. Cây trồng theo mùa vụ hoặc lấy SP 1 lần ngắn hạn", 3, new[]{"215"}, false, false),
            new("153", "3. Dự phòng tổn thất tài sản sinh học ngắn hạn (*)", 3, Array.Empty<string>(), false, true),
            new("160", "VI. Tài sản ngắn hạn khác", 2, Array.Empty<string>(), true, false),
            new("161", "1. Chi phí chờ phân bổ ngắn hạn", 3, new[]{"242"}, false, false),
            new("162", "2. Thuế GTGT được khấu trừ", 3, new[]{"133"}, false, false),
            new("163", "3. Thuế và các khoản khác phải thu NSNN", 3, new[]{"333"}, false, false),
            new("164", "4. Giao dịch mua bán lại TPCP", 3, new[]{"171"}, false, false),
            new("165", "5. Tài sản ngắn hạn khác", 3, new[]{"141","151","154","158"}, false, false),
            // B — TÀI SẢN DÀI HẠN
            new("200", "B - TÀI SẢN DÀI HẠN", 1, Array.Empty<string>(), true, false),
            new("210", "I. Các khoản phải thu dài hạn", 2, Array.Empty<string>(), true, false),
            new("211", "1. Phải thu dài hạn của khách hàng", 3, Array.Empty<string>(), false, false),
            new("212", "2. Trả trước cho người bán dài hạn", 3, Array.Empty<string>(), false, false),
            new("213", "3. Vốn kinh doanh ở đơn vị trực thuộc", 3, Array.Empty<string>(), false, false),
            new("214", "4. Phải thu nội bộ dài hạn", 3, Array.Empty<string>(), false, false),
            new("215", "5. Phải thu dài hạn khác", 3, Array.Empty<string>(), false, false),
            new("216", "6. Dự phòng phải thu dài hạn khó đòi (*)", 3, Array.Empty<string>(), false, true),
            new("220", "II. Tài sản cố định", 2, Array.Empty<string>(), true, false),
            new("221", "1. TSCĐ hữu hình", 3, new[]{"211"}, false, false),
            new("222", "- Nguyên giá", 4, Array.Empty<string>(), false, false),
            new("223", "- Giá trị hao mòn lũy kế (*)", 4, new[]{"214"}, false, true),
            new("224", "2. TSCĐ thuê tài chính", 3, new[]{"212"}, false, false),
            new("225", "- Nguyên giá", 4, Array.Empty<string>(), false, false),
            new("226", "- Giá trị hao mòn lũy kế (*)", 4, new[]{"214"}, false, true),
            new("227", "3. TSCĐ vô hình", 3, new[]{"213"}, false, false),
            new("228", "- Nguyên giá", 4, Array.Empty<string>(), false, false),
            new("229", "- Giá trị hao mòn lũy kế (*)", 4, new[]{"214"}, false, true),
            new("230", "III. Tài sản sinh học dài hạn", 2, Array.Empty<string>(), true, false),
            new("231", "1. Súc vật nuôi cho sản phẩm định kỳ", 3, new[]{"215"}, false, false),
            new("232", "a) SV nuôi cho SP định kỳ chưa đến giai đoạn trưởng thành", 4, new[]{"215"}, false, false),
            new("233", "b) SV nuôi cho SP định kỳ đến giai đoạn trưởng thành", 4, new[]{"215"}, false, false),
            new("234", "- Nguyên giá", 4, Array.Empty<string>(), false, false),
            new("235", "- Giá trị khấu hao lũy kế (*)", 4, Array.Empty<string>(), false, true),
            new("236", "2. Súc vật nuôi lấy sản phẩm một lần dài hạn", 3, new[]{"215"}, false, false),
            new("237", "3. Cây trồng theo mùa vụ hoặc lấy SP 1 lần dài hạn", 3, new[]{"215"}, false, false),
            new("238", "4. Dự phòng tổn thất tài sản sinh học dài hạn (*)", 3, Array.Empty<string>(), false, true),
            new("240", "IV. Bất động sản đầu tư", 2, Array.Empty<string>(), true, false),
            new("241", "- Nguyên giá", 3, new[]{"217"}, false, false),
            new("242", "- Giá trị hao mòn lũy kế (*)", 3, Array.Empty<string>(), false, true),
            new("250", "V. Tài sản dở dang dài hạn", 2, Array.Empty<string>(), true, false),
            new("251", "1. Chi phí SXKD dở dang dài hạn", 3, new[]{"154"}, false, false),
            new("252", "2. Chi phí xây dựng cơ bản dở dang", 3, new[]{"241"}, false, false),
            new("260", "VI. Đầu tư tài chính dài hạn", 2, Array.Empty<string>(), true, false),
            new("261", "1. Đầu tư vào công ty con", 3, new[]{"221"}, false, false),
            new("262", "2. Đầu tư vào công ty liên doanh, liên kết", 3, new[]{"222"}, false, false),
            new("263", "3. Đầu tư góp vốn vào đơn vị khác", 3, new[]{"228"}, false, false),
            new("264", "4. Dự phòng tổn thất đầu tư vào đơn vị khác dài hạn (*)", 3, new[]{"229"}, false, true),
            new("265", "5. Đầu tư nắm giữ đến ngày đáo hạn dài hạn", 3, new[]{"128"}, false, false),
            new("266", "6. Dự phòng đầu tư nắm giữ NTN dài hạn (*)", 3, Array.Empty<string>(), false, true),
            new("270", "VII. Tài sản dài hạn khác", 2, Array.Empty<string>(), true, false),
            new("271", "1. Chi phí chờ phân bổ dài hạn", 3, new[]{"242"}, false, false),
            new("272", "2. Tài sản thuế thu nhập hoãn lại", 3, new[]{"243"}, false, false),
            new("273", "3. Thiết bị, vật tư, phụ tùng thay thế dài hạn", 3, Array.Empty<string>(), false, false),
            new("274", "4. Tài sản dài hạn khác", 3, new[]{"244"}, false, false),
            new("280", "TỔNG CỘNG TÀI SẢN (280 = 100 + 200)", 1, Array.Empty<string>(), true, false),
            // C — NỢ PHẢI TRẢ
            new("300", "C - NỢ PHẢI TRẢ", 1, Array.Empty<string>(), true, false),
            new("310", "I. Nợ ngắn hạn", 2, Array.Empty<string>(), true, false),
            new("311", "1. Phải trả người bán ngắn hạn", 3, new[]{"331"}, false, true),
            new("312", "2. Người mua trả tiền trước ngắn hạn", 3, Array.Empty<string>(), false, true),
            new("313", "3. Phải trả cổ tức, lợi nhuận", 3, new[]{"332"}, false, true),
            new("314", "4. Thuế và các khoản phải nộp NSNN ngắn hạn", 3, new[]{"333"}, false, true),
            new("315", "5. Phải trả người lao động", 3, new[]{"334"}, false, true),
            new("316", "6. Chi phí phải trả ngắn hạn", 3, new[]{"335"}, false, true),
            new("317", "7. Phải trả nội bộ ngắn hạn", 3, new[]{"336"}, false, true),
            new("318", "8. Phải trả theo tiến độ HĐXD ngắn hạn", 3, new[]{"337"}, false, true),
            new("319", "9. Doanh thu chờ phân bổ ngắn hạn", 3, Array.Empty<string>(), false, true),
            new("320", "10. Phải trả ngắn hạn khác", 3, new[]{"338"}, false, true),
            new("321", "11. Vay và nợ thuê tài chính ngắn hạn", 3, new[]{"341"}, false, true),
            new("322", "12. Dự phòng phải trả ngắn hạn", 3, new[]{"352"}, false, true),
            new("323", "13. Quỹ khen thưởng, phúc lợi", 3, new[]{"353"}, false, true),
            new("324", "14. Quỹ bình ổn giá", 3, new[]{"357"}, false, true),
            new("325", "15. Giao dịch mua bán lại TPCP", 3, Array.Empty<string>(), false, true),
            new("330", "II. Nợ dài hạn", 2, Array.Empty<string>(), true, false),
            new("331", "1. Phải trả người bán dài hạn", 3, new[]{"331"}, false, true),
            new("332", "2. Người mua trả tiền trước dài hạn", 3, Array.Empty<string>(), false, true),
            new("333", "3. Thuế và các khoản phải nộp NSNN dài hạn", 3, new[]{"333"}, false, true),
            new("334", "4. Chi phí phải trả dài hạn", 3, new[]{"335"}, false, true),
            new("335", "5. Phải trả nội bộ về vốn kinh doanh", 3, Array.Empty<string>(), false, true),
            new("336", "6. Phải trả nội bộ dài hạn", 3, new[]{"336"}, false, true),
            new("337", "7. Doanh thu chờ phân bổ dài hạn", 3, Array.Empty<string>(), false, true),
            new("338", "8. Phải trả dài hạn khác", 3, new[]{"338"}, false, true),
            new("339", "9. Vay và nợ thuê tài chính dài hạn", 3, new[]{"341"}, false, true),
            new("340", "10. Trái phiếu chuyển đổi", 3, new[]{"343"}, false, true),
            new("341", "11. Cổ phiếu ưu đãi", 3, Array.Empty<string>(), false, true),
            new("342", "12. Thuế TNDN hoãn lại phải trả", 3, new[]{"347"}, false, true),
            new("343", "13. Dự phòng phải trả dài hạn", 3, new[]{"352"}, false, true),
            new("344", "14. Quỹ phát triển KH&CN", 3, new[]{"356"}, false, true),
            // D — VỐN CHỦ SỞ HỮU
            new("400", "D - VỐN CHỦ SỞ HỮU", 1, Array.Empty<string>(), true, false),
            new("411", "1. Vốn góp của chủ sở hữu", 2, new[]{"411"}, false, true),
            new("412", "2. Thặng dư vốn", 2, new[]{"412"}, false, true),
            new("413", "3. Quyền chọn chuyển đổi trái phiếu", 2, Array.Empty<string>(), false, true),
            new("414", "4. Vốn khác của chủ sở hữu", 2, new[]{"418"}, false, true),
            new("415", "5. Cổ phiếu mua lại của chính mình (*)", 2, new[]{"419"}, false, true),
            new("416", "6. Chênh lệch đánh giá lại tài sản", 2, new[]{"412"}, false, true),
            new("417", "7. Chênh lệch tỷ giá hối đoái", 2, new[]{"413"}, false, true),
            new("418", "8. Quỹ đầu tư phát triển", 2, new[]{"414"}, false, true),
            new("419", "9. Quỹ khác thuộc vốn chủ sở hữu", 2, new[]{"418"}, false, true),
            new("420", "10. Lợi nhuận sau thuế chưa phân phối", 2, new[]{"421"}, false, true),
            new("440", "TỔNG CỘNG NGUỒN VỐN (440 = 300 + 400)", 1, Array.Empty<string>(), true, false),
        });

    // ════════════════════════════════════════════════════════════════════════════
    // B 02-DN: Báo cáo kết quả hoạt động kinh doanh (VERIFIED from Phụ lục IV TT 99)
    // ════════════════════════════════════════════════════════════════════════════
    public static Tt99ReportTemplate IncomeStatementTt99 => new(
        AccountingStandard.TT99_2025, "B02-DN",
        new List<Tt99TemplateLine>
        {
            new("01", "1. Doanh thu bán hàng và cung cấp dịch vụ", 1, new[]{"511"}, false, false),
            new("02", "2. Các khoản giảm trừ doanh thu", 1, new[]{"521"}, false, false),
            new("10", "3. Doanh thu thuần về bán hàng và CC DV (10 = 01 - 02)", 1, Array.Empty<string>(), true, false),
            new("11", "4. Giá vốn hàng bán", 1, new[]{"632"}, false, false),
            new("20", "5. Lợi nhuận gộp về bán hàng và CC DV (20 = 10 - 11)", 1, Array.Empty<string>(), true, false),
            new("21", "6. Lãi/lỗ của hoạt động bán, thanh lý BĐS đầu tư", 1, new[]{"5117","6327"}, false, false),
            new("22", "7. Doanh thu hoạt động tài chính", 1, new[]{"515"}, false, false),
            new("23", "8. Chi phí tài chính", 1, new[]{"635"}, false, false),
            new("24", "- Trong đó: Chi phí đi vay", 2, Array.Empty<string>(), false, false),
            new("25", "9. Chi phí bán hàng", 1, new[]{"641"}, false, false),
            new("26", "10. Chi phí quản lý doanh nghiệp", 1, new[]{"642"}, false, false),
            new("30", "11. Lợi nhuận thuần từ HĐKD {30 = 20 + 21 + 22 - (23 + 25 + 26)}", 1, Array.Empty<string>(), true, false),
            new("31", "12. Thu nhập khác", 1, new[]{"711"}, false, false),
            new("32", "13. Chi phí khác", 1, new[]{"811"}, false, false),
            new("40", "14. Lợi nhuận khác (40 = 31 - 32)", 1, Array.Empty<string>(), true, false),
            new("50", "15. Tổng lợi nhuận kế toán trước thuế (50 = 30 + 40)", 1, Array.Empty<string>(), true, false),
            new("51", "16. Chi phí thuế TNDN hiện hành", 1, new[]{"821"}, false, false),
            new("52", "17. Chi phí thuế TNDN hoãn lại", 1, new[]{"822"}, false, false),
            new("60", "18. Lợi nhuận sau thuế TNDN (60 = 50 - 51 - 52)", 1, Array.Empty<string>(), true, false),
        });

    // ════════════════════════════════════════════════════════════════════════════
    // B 03-DN: Báo cáo lưu chuyển tiền tệ — Direct method (VERIFIED from Phụ lục IV TT 99)
    // ════════════════════════════════════════════════════════════════════════════
    public static Tt99ReportTemplate CashFlowDirectTt99 => new(
        AccountingStandard.TT99_2025, "B03-DN-Direct",
        new List<Tt99TemplateLine>
        {
            // I. HĐ kinh doanh
            new("01", "1. Tiền thu từ bán hàng, CC DV và doanh thu khác", 2, Array.Empty<string>(), false, false),
            new("02", "2. Tiền chi trả cho người cung cấp hàng hóa và DV", 2, Array.Empty<string>(), false, true),
            new("03", "3. Tiền chi trả cho người lao động", 2, Array.Empty<string>(), false, true),
            new("04", "4. Chi phí đi vay đã trả", 2, Array.Empty<string>(), false, true),
            new("05", "5. Thuế thu nhập doanh nghiệp đã nộp", 2, Array.Empty<string>(), false, true),
            new("06", "6. Tiền thu khác từ hoạt động kinh doanh", 2, Array.Empty<string>(), false, false),
            new("07", "7. Tiền chi khác cho hoạt động kinh doanh", 2, Array.Empty<string>(), false, true),
            new("20", "Lưu chuyển tiền thuần từ HĐKD", 1, Array.Empty<string>(), true, false),
            // II. HĐ đầu tư
            new("21", "1. Tiền chi mua sắm, xây dựng TSCĐ và TSDH khác", 2, Array.Empty<string>(), false, true),
            new("22", "2. Tiền thu từ thanh lý, nhượng bán TSCĐ và TSDH khác", 2, Array.Empty<string>(), false, false),
            new("23", "3. Tiền chi cho vay, mua công cụ nợ của đơn vị khác", 2, Array.Empty<string>(), false, true),
            new("24", "4. Tiền thu hồi cho vay, bán lại công cụ nợ của đơn vị khác", 2, Array.Empty<string>(), false, false),
            new("25", "5. Tiền chi đầu tư góp vốn vào đơn vị khác", 2, Array.Empty<string>(), false, true),
            new("26", "6. Tiền thu hồi đầu tư góp vốn vào đơn vị khác", 2, Array.Empty<string>(), false, false),
            new("27", "7. Tiền thu lãi cho vay, cổ tức và LNST được chia", 2, Array.Empty<string>(), false, false),
            new("30", "Lưu chuyển tiền thuần từ HĐ đầu tư", 1, Array.Empty<string>(), true, false),
            // III. HĐ tài chính
            new("31", "1. Tiền thu từ phát hành cổ phiếu, nhận vốn góp của CSH", 2, Array.Empty<string>(), false, false),
            new("32", "2. Tiền trả lại vốn góp, mua lại cổ phiếu đã phát hành", 2, Array.Empty<string>(), false, true),
            new("33", "3. Tiền thu từ đi vay", 2, Array.Empty<string>(), false, false),
            new("34", "4. Tiền trả nợ gốc vay", 2, Array.Empty<string>(), false, true),
            new("35", "5. Tiền trả nợ gốc thuê tài chính", 2, Array.Empty<string>(), false, true),
            new("36", "6. Cổ tức, lợi nhuận đã trả cho chủ sở hữu", 2, new[]{"332"}, false, true),
            new("40", "Lưu chuyển tiền thuần từ HĐ tài chính", 1, Array.Empty<string>(), true, false),
            // Tổng
            new("50", "Lưu chuyển tiền thuần trong kỳ (50 = 20 + 30 + 40)", 1, Array.Empty<string>(), true, false),
            new("60", "Tiền và tương đương tiền đầu kỳ", 1, Array.Empty<string>(), false, false),
            new("61", "Ảnh hưởng của thay đổi tỷ giá hối đoái quy đổi ngoại tệ", 1, Array.Empty<string>(), false, false),
            new("70", "Tiền và tương đương tiền cuối kỳ (70 = 50 + 60 + 61)", 1, Array.Empty<string>(), true, false),
        });

    // ════════════════════════════════════════════════════════════════════════════
    // B 03-DN: Indirect method — operating adjustments only
    // (Investing + Financing sections are same as direct method)
    // ════════════════════════════════════════════════════════════════════════════
    public static Tt99ReportTemplate CashFlowIndirectAdjustmentsTt99 => new(
        AccountingStandard.TT99_2025, "B03-DN-Indirect-Adjustments",
        new List<Tt99TemplateLine>
        {
            new("01", "1. Lợi nhuận trước thuế", 1, Array.Empty<string>(), false, false),
            new("02", "- Khấu hao TSCĐ và BĐSĐT", 2, new[]{"214"}, false, false),
            new("03", "- Các khoản dự phòng", 2, Array.Empty<string>(), false, false),
            new("04", "- Lãi, lỗ chênh lệch tỷ giá hối đoái do đánh giá lại các khoản mục tiền tệ có gốc ngoại tệ", 2, Array.Empty<string>(), false, false),
            new("05", "- Lãi, lỗ từ hoạt động đầu tư, tài chính", 2, Array.Empty<string>(), false, false),
            new("06", "- Chi phí đi vay", 2, Array.Empty<string>(), false, false),
            new("07", "- Các khoản điều chỉnh khác", 2, Array.Empty<string>(), false, false),
            new("08", "3. Lợi nhuận từ HĐKD trước thay đổi vốn lưu động", 1, Array.Empty<string>(), true, false),
            new("09", "- Tăng, giảm các khoản phải thu", 2, new[]{"131"}, false, false),
            new("10", "- Tăng, giảm hàng tồn kho", 2, new[]{"152","155","156"}, false, false),
            new("11", "- Tăng, giảm các khoản phải trả (Không kể lãi vay phải trả, TNDN phải nộp)", 2, new[]{"331"}, false, false),
            new("12", "- Tăng, giảm chi phí chờ phân bổ", 2, new[]{"242"}, false, false),
            new("13", "- Tăng, giảm chứng khoán kinh doanh", 2, new[]{"121"}, false, false),
            new("14", "- Chi phí đi vay đã trả", 2, Array.Empty<string>(), false, false),
            new("15", "- Thuế thu nhập doanh nghiệp đã nộp", 2, Array.Empty<string>(), false, false),
            new("16", "- Tiền thu khác từ hoạt động kinh doanh", 2, Array.Empty<string>(), false, false),
            new("17", "- Tiền chi khác cho hoạt động kinh doanh", 2, Array.Empty<string>(), false, true),
            new("20", "Lưu chuyển tiền thuần từ HĐKD", 1, Array.Empty<string>(), true, false),
        });
}
