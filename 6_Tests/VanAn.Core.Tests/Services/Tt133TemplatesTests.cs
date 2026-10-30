using VanAn.CoreHub.Services.Data;
using VanAn.Shared.Domain;
using Xunit;

namespace VanAn.Core.Tests.Services;

/// <summary>
/// TT 133/2016/TT-BTC — P1 tests: cấu trúc mã số B01a-DNN / B02-DNN / B03-DNN theo Phụ lục 2.
/// Spec: docs/AI/tasks/tt133_tt58_compliance/spec/tt133-pl2-bctc-forms.txt
/// </summary>
public class Tt133TemplatesTests
{
    [Fact]
    public void B01aDnn_HasCorrectFormCodeAndStandard()
    {
        var t = Tt133Templates.BalanceSheetTt133;
        Assert.Equal(AccountingStandard.TT133_2016, t.Standard);
        Assert.Equal("B01a-DNN", t.ReportForm);
    }

    [Theory]
    [InlineData("110", "I. Tiền và các khoản tương đương tiền")]
    [InlineData("120", "II. Đầu tư tài chính")]
    [InlineData("130", "III. Các khoản phải thu")]
    [InlineData("140", "IV. Hàng tồn kho")]
    [InlineData("141", "1. Hàng tồn kho")]
    [InlineData("150", "V. Tài sản cố định")]
    [InlineData("170", "VII. Xây dựng cơ bản dở dang")]
    [InlineData("200", "TỔNG CỘNG TÀI SẢN (200=110+120+130+140+150+160+170+180)")]
    [InlineData("300", "I. Nợ phải trả")]
    [InlineData("311", "1. Phải trả người bán")]
    [InlineData("400", "II. Vốn chủ sở hữu")]
    [InlineData("417", "7. Lợi nhuận sau thuế chưa phân phối")]
    [InlineData("500", "TỔNG CỘNG NGUỒN VỐN (500=300+400)")]
    public void B01aDnn_ContainsKeyLines(string code, string expectedName)
    {
        var line = Tt133Templates.BalanceSheetTt133.Lines.First(l => l.ReportItemCode == code);
        Assert.Equal(expectedName, line.ReportItemName);
    }

    [Fact]
    public void B01aDnn_TotalsAreCalculated()
    {
        var lines = Tt133Templates.BalanceSheetTt133.Lines;
        foreach (var code in new[] { "120", "130", "140", "150", "160", "180", "200", "300", "400", "500" })
            Assert.True(lines.First(l => l.ReportItemCode == code).IsCalculated, $"{code} must be calculated");
    }

    [Fact]
    public void B01aDnn_NoDuplicateCodes()
    {
        var codes = Tt133Templates.BalanceSheetTt133.Lines.Select(l => l.ReportItemCode).ToList();
        Assert.Equal(codes.Count, codes.Distinct().Count());
    }

    [Theory]
    [InlineData("01", "511")]   // Doanh thu → 511
    [InlineData("11", "632")]   // Giá vốn → 632
    [InlineData("21", "515")]   // DT tài chính → 515
    [InlineData("22", "635")]   // CP tài chính → 635
    [InlineData("24", "642")]   // CP QLKD gộp → 642 (KHÔNG tách 641)
    [InlineData("31", "711")]   // Thu nhập khác → 711
    [InlineData("32", "811")]   // Chi phí khác → 811
    [InlineData("51", "821")]   // CP thuế TNDN gộp → 821 (KHÔNG tách 821/822)
    public void B02Dnn_MapsCorrectAccounts(string code, string expectedAccount)
    {
        var line = Tt133Templates.IncomeStatementTt133.Lines.First(l => l.ReportItemCode == code);
        Assert.Contains(expectedAccount, line.AccountCodes);
    }

    [Theory]
    [InlineData("10")] [InlineData("20")] [InlineData("30")]
    [InlineData("40")] [InlineData("50")] [InlineData("60")]
    public void B02Dnn_FormulaLinesAreCalculated(string code)
    {
        var line = Tt133Templates.IncomeStatementTt133.Lines.First(l => l.ReportItemCode == code);
        Assert.True(line.IsCalculated);
    }

    [Fact]
    public void B03Dnn_DirectHasAllSubtotals()
    {
        var codes = Tt133Templates.CashFlowDirectTt133.Lines.Select(l => l.ReportItemCode).ToList();
        foreach (var code in new[] { "20", "30", "40", "50", "60", "61", "70" })
            Assert.Contains(code, codes);
    }

    [Fact]
    public void B03Dnn_IndirectAdjustmentsExist()
    {
        var t = Tt133Templates.CashFlowIndirectAdjustmentsTt133;
        Assert.Equal(AccountingStandard.TT133_2016, t.Standard);
        Assert.Contains(t.Lines, l => l.ReportItemCode == "01");
        Assert.Contains(t.Lines, l => l.ReportItemCode == "20");
    }
}
