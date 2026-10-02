using Bunit;
using Xunit;
using FluentAssertions;
using VanAn.ShopERP.Services.Accounting;

namespace VanAn.ShopERP.Tests.Components.Accounting;

/// <summary>
/// TT 71 Phase 4b (S3) — tests mẫu in chứng từ: Phiếu thu 01-TT / Phiếu chi 02-TT
/// (khuôn mẫu Phụ lục II TT 71/2024/TT-BTC) + helper số → chữ.
/// </summary>
public class Tt71VoucherTests : ComponentTestBase
{
    // ── Tt71ReceiptVoucher (Mẫu số 01 - TT) ───────────────────────────────

    [Fact]
    public void ReceiptVoucher_RendersTt71Template_WithEntryData()
    {
        var cut = RenderComponent<ShopERP.Components.Accounting.Tt71ReceiptVoucher>(p => p
            .Add(v => v.SoPhieu, "PT-20261002-1")
            .Add(v => v.Ngay, "02/10/2026")
            .Add(v => v.No, "111")
            .Add(v => v.Co, "511")
            .Add(v => v.NguoiNop, "Nguyễn Văn A")
            .Add(v => v.LyDo, "Thu tiền bán hàng")
            .Add(v => v.SoTien, 1_234_000m)
            .Add(v => v.ChungTuGoc, "HĐ-001"));

        cut.Markup.Should().Contain("PHIẾU THU");
        cut.Markup.Should().Contain("Mẫu số 01 - TT");

        // Dữ liệu entry (TextContent — bỏ qua thẻ span giữa label/value)
        var body = cut.Find(".voucher-body").TextContent;
        body.Should().Contain("Nợ: 111");
        body.Should().Contain("Có: 511");
        body.Should().Contain("Nguyễn Văn A");
        body.Should().Contain("Thu tiền bán hàng");
        body.Should().Contain("HĐ-001");
        body.Should().Contain("1.234.000");

        // Viết bằng chữ (footer + body)
        cut.Markup.Should().Contain("Một triệu hai trăm ba mươi tư nghìn đồng");

        // 5 chữ ký
        cut.Markup.Should().Contain("GIÁM ĐỐC");
        cut.Markup.Should().Contain("KẾ TOÁN TRƯỞNG");
        cut.Markup.Should().Contain("NGƯỜI LẬP PHIẾU");
        cut.Markup.Should().Contain("THỦ QUỸ");
        cut.Markup.Should().Contain("NGƯỜI NỘP TIỀN");
    }

    // ── Tt71PaymentVoucher (Mẫu số 02 - TT) ───────────────────────────────

    [Fact]
    public void PaymentVoucher_RendersTt71Template_WithEntryData()
    {
        var cut = RenderComponent<ShopERP.Components.Accounting.Tt71PaymentVoucher>(p => p
            .Add(v => v.SoPhieu, "PC-20261002-1")
            .Add(v => v.Ngay, "02/10/2026")
            .Add(v => v.No, "642")
            .Add(v => v.Co, "111")
            .Add(v => v.NguoiNhan, "Công ty XYZ")
            .Add(v => v.LyDo, "Chi phí điện nước")
            .Add(v => v.SoTien, 500_000m));

        cut.Markup.Should().Contain("PHIẾU CHI");
        cut.Markup.Should().Contain("Mẫu số 02 - TT");

        // Dữ liệu entry (TextContent — bỏ qua thẻ span giữa label/value)
        var body = cut.Find(".voucher-body").TextContent;
        body.Should().Contain("Nợ: 642");
        body.Should().Contain("Có: 111");
        body.Should().Contain("Công ty XYZ");
        body.Should().Contain("Chi phí điện nước");
        body.Should().Contain("500.000");

        // Viết bằng chữ
        cut.Markup.Should().Contain("Năm trăm nghìn đồng");

        // 5 chữ ký
        cut.Markup.Should().Contain("GIÁM ĐỐC");
        cut.Markup.Should().Contain("KẾ TOÁN TRƯỞNG");
        cut.Markup.Should().Contain("THỦ QUỸ");
        cut.Markup.Should().Contain("NGƯỜI LẬP PHIẾU");
        cut.Markup.Should().Contain("NGƯỜI NHẬN TIỀN");
    }

    // ── VietnameseCurrencyText (số → chữ) ─────────────────────────────────

    [Theory]
    [InlineData(0, "Không đồng")]
    [InlineData(15, "Mười lăm đồng")]
    [InlineData(14, "Mười bốn đồng")]
    [InlineData(21, "Hai mươi mốt đồng")]
    [InlineData(24, "Hai mươi tư đồng")]
    [InlineData(105, "Một trăm linh năm đồng")]
    [InlineData(104, "Một trăm linh tư đồng")]
    [InlineData(1000, "Một nghìn đồng")]
    [InlineData(1_234_567, "Một triệu hai trăm ba mươi tư nghìn năm trăm sáu mươi bảy đồng")]
    [InlineData(1_000_005, "Một triệu không nghìn không trăm linh năm đồng")]
    [InlineData(1_000_000_000, "Một tỷ đồng")]
    public void VietnameseCurrencyText_ConvertsAmountToWords(decimal amount, string expected)
    {
        VietnameseCurrencyText.ToWords(amount).Should().Be(expected);
    }
}
