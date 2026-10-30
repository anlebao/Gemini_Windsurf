using VanAn.CoreHub.Services.Data;
using VanAn.CoreHub.Services.Template;
using VanAn.Shared.Domain;
using Xunit;

namespace VanAn.Core.Tests.Services;

/// <summary>
/// TT 58/2026/TT-BTC — P2 tests: cấu trúc B01/B02-DNSN + danh mục sổ theo DnsnTaxMethod (Điều 5-8).
/// Spec: docs/AI/tasks/tt133_tt58_compliance/spec/tt58-2026-full-text.txt
/// </summary>
public class Tt58DnsnTests
{
    // ── BCTC structure ──────────────────────────────────────────────────────

    [Fact]
    public void B01Dnsn_HasCorrectFormCodeAndStandard()
    {
        var t = Tt58Templates.BalanceSheetTt58;
        Assert.Equal(AccountingStandard.TT58_2026, t.Standard);
        Assert.Equal("B01-DNSN", t.ReportForm);
    }

    [Theory]
    [InlineData("110", "111")]
    [InlineData("110", "112")]
    [InlineData("120", "131")]
    [InlineData("130", "156")]
    [InlineData("150", "242")]
    [InlineData("310", "331")]
    [InlineData("320", "333")]
    [InlineData("410", "411")]
    [InlineData("420", "421")]
    [InlineData("430", "418")]
    public void B01Dnsn_MapsCorrectAccounts(string code, string expectedAccount)
    {
        var line = Tt58Templates.BalanceSheetTt58.Lines.First(l => l.ReportItemCode == code);
        Assert.Contains(expectedAccount, line.AccountCodes);
    }

    [Theory]
    [InlineData("200")] [InlineData("300")]
    [InlineData("400")] [InlineData("500")]
    public void B01Dnsn_TotalsAreCalculated(string code)
    {
        var line = Tt58Templates.BalanceSheetTt58.Lines.First(l => l.ReportItemCode == code);
        Assert.True(line.IsCalculated);
    }

    [Fact]
    public void B01Dnsn_Has14Lines_NoDuplicateCodes()
    {
        var lines = Tt58Templates.BalanceSheetTt58.Lines;
        Assert.Equal(14, lines.Count);
        Assert.Equal(lines.Count, lines.Select(l => l.ReportItemCode).Distinct().Count());
    }

    [Theory]
    [InlineData("01", "511")] [InlineData("01", "711")]
    [InlineData("02", "632")] [InlineData("02", "642")]
    [InlineData("10", "821")]
    public void B02Dnsn_MapsCorrectAccounts(string code, string expectedAccount)
    {
        var line = Tt58Templates.IncomeStatementTt58.Lines.First(l => l.ReportItemCode == code);
        Assert.Contains(expectedAccount, line.AccountCodes);
    }

    [Theory]
    [InlineData("03")] [InlineData("20")]
    public void B02Dnsn_FormulaLinesAreCalculated(string code)
    {
        var line = Tt58Templates.IncomeStatementTt58.Lines.First(l => l.ReportItemCode == code);
        Assert.True(line.IsCalculated);
    }

    // ── Danh mục sổ theo DnsnTaxMethod (Điều 5-8 — T2-T5) ─────────────────────

    [Fact]
    public void BookCodes_Dieu5_OnlyS1()
    {
        var codes = TemplateFactory.GetDnsnBookCodes(DnsnTaxMethod.VatPercent_TndnPercent, includeOptional: false);
        Assert.Equal(new[] { "S1_DNSN" }, codes);
    }

    [Fact]
    public void BookCodes_Dieu6_S2aS2bS2cS2d()
    {
        var codes = TemplateFactory.GetDnsnBookCodes(DnsnTaxMethod.VatPercent_TndnIncome, includeOptional: false);
        Assert.Equal(new[] { "S2a_DNSN", "S2b_DNSN", "S2c_DNSN", "S2d_DNSN" }, codes);
    }

    [Fact]
    public void BookCodes_Dieu7_S3aS3b()
    {
        var codes = TemplateFactory.GetDnsnBookCodes(DnsnTaxMethod.VatDeduct_TndnPercent, includeOptional: false);
        Assert.Equal(new[] { "S3a_DNSN", "S3b_DNSN" }, codes);
    }

    [Fact]
    public void BookCodes_Dieu8_S2bS2cS2dS3b()
    {
        var codes = TemplateFactory.GetDnsnBookCodes(DnsnTaxMethod.VatDeduct_TndnIncome, includeOptional: false);
        Assert.Equal(new[] { "S2b_DNSN", "S2c_DNSN", "S2d_DNSN", "S3b_DNSN" }, codes);
    }

    [Fact]
    public void BookCodes_IncludeOptional_AddsS4a()
    {
        var codes = TemplateFactory.GetDnsnBookCodes(DnsnTaxMethod.VatPercent_TndnPercent, includeOptional: true);
        Assert.Contains("S4a_DNSN", codes);
    }

    // ── BookType enum values ────────────────────────────────────────────────

    [Theory]
    [InlineData(AccountingBookType.S1_DNSN, 12)]
    [InlineData(AccountingBookType.S4a_DNSN, 19)]
    public void DnsnBookTypes_AppendedAfterHkd(AccountingBookType type, int expected)
    {
        Assert.Equal(expected, (int)type);
    }
}
