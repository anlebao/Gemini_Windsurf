using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using VanAn.CoreHub.Infrastructure;
using VanAn.CoreHub.Infrastructure.Seed;
using VanAn.CoreHub.Services;
using VanAn.CoreHub.Services.Data;
using VanAn.CoreHub.Tests.TestInfrastructure;
using VanAn.Shared.Domain;
using Xunit;
using TenantAggregate = VanAn.Shared.Domain.Aggregates.TenantAggregate.Tenant;

namespace VanAn.Core.Tests.Services;

/// <summary>
/// TT 71/2024 (HTX) — Phase 3 tests (T2): template structure B01-HTX/B02-HTX theo Phụ lục IV
/// + B09-HTX render test qua FinancialStatementNotesService.
/// </summary>
public class Tt71TemplatesTests
{
    // ── T2: B02-HTX template structure ────────────────────────────────────

    [Fact]
    public void B02Htx_HasFullStructure_WithInternalExternalSplit()
    {
        var template = Tt71Templates.IncomeStatementTt71;

        Assert.Equal(AccountingStandard.TT71_2024, template.Standard);
        Assert.Equal("B02-HTX", template.ReportForm);

        string[] expectedCodes =
        {
            "01", "01a", "01b", "02", "02a", "02b", "10", "10a", "10b",
            "11", "11a", "11b", "12", "12a", "12b", "20", "20a", "20b",
            "31", "32", "40", "50", "51", "60"
        };
        Assert.Equal(expectedCodes, template.Lines.Select(l => l.ReportItemCode).ToArray());

        // Tách nội bộ/ngoài theo spec: 511 vs 512 · 611 vs 612
        Assert.Equal(new[] { "511" }, Line(template, "01a").AccountCodes);
        Assert.Equal(new[] { "512" }, Line(template, "01b").AccountCodes);
        Assert.Equal(new[] { "521" }, Line(template, "02a").AccountCodes);
        Assert.Equal(new[] { "611" }, Line(template, "11a").AccountCodes);
        Assert.Equal(new[] { "612" }, Line(template, "11b").AccountCodes);
        Assert.Equal(new[] { "642" }, Line(template, "12").AccountCodes);
        Assert.Equal(new[] { "558" }, Line(template, "31").AccountCodes);
        Assert.Equal(new[] { "658" }, Line(template, "32").AccountCodes);
        Assert.Equal(new[] { "659" }, Line(template, "51").AccountCodes);

        // Các chỉ tiêu công thức (IsCalculated)
        foreach (string code in new[] { "01", "02", "10", "10a", "10b", "11", "20", "20a", "20b", "40", "50", "60" })
        {
            Assert.True(Line(template, code).IsCalculated, $"Mã {code} phải là chỉ tiêu công thức (IsCalculated)");
        }

        // Chỉ tiêu không có dữ liệu phân bổ (02b/12a/12b) = direct với AccountCodes rỗng → 0
        Assert.Empty(Line(template, "02b").AccountCodes);
        Assert.Empty(Line(template, "12a").AccountCodes);
        Assert.Empty(Line(template, "12b").AccountCodes);
    }

    // ── T2: B01-HTX template structure ────────────────────────────────────

    [Fact]
    public void B01Htx_HasFullStructure_WithContraLines()
    {
        var template = Tt71Templates.BalanceSheetTt71;

        Assert.Equal(AccountingStandard.TT71_2024, template.Standard);
        Assert.Equal("B01-HTX", template.ReportForm);

        string[] expectedCodes =
        {
            "110", "120", "130", "137", "140", "150", "151", "152", "160", "161", "162",
            "170", "180", "200",
            "300", "310", "320", "330", "340", "350", "360", "370", "380",
            "400", "410", "420", "430", "440", "500"
        };
        Assert.Equal(expectedCodes, template.Lines.Select(l => l.ReportItemCode).ToArray());

        // Account mapping theo spec PL IV Mục I.1
        Assert.Equal(new[] { "111", "112" }, Line(template, "110").AccountCodes);
        Assert.Equal(new[] { "121" }, Line(template, "120").AccountCodes);
        Assert.Equal(new[] { "132" }, Line(template, "137").AccountCodes);
        Assert.Equal(new[] { "151", "152", "154", "156", "157" }, Line(template, "140").AccountCodes);
        Assert.Equal(new[] { "211" }, Line(template, "151").AccountCodes);
        Assert.Equal(new[] { "212" }, Line(template, "161").AccountCodes);
        Assert.Equal(new[] { "2142" }, Line(template, "162").AccountCodes);
        Assert.Equal(new[] { "442" }, Line(template, "440").AccountCodes);

        // Các dòng contra (*) → IsNormalNegative
        Assert.True(Line(template, "152").IsNormalNegative, "152 (Hao mòn TSCĐ) phải là (*)");
        Assert.True(Line(template, "162").IsNormalNegative, "162 (Hao mòn tài sản chung không chia) phải là (*)");
        Assert.True(Line(template, "180").IsNormalNegative, "180 (Dự phòng tổn thất tài sản) phải là (*)");

        // Tổng hợp (IsCalculated): 150/160/200/300/400/500; 137 "Trong đó" không tính vào tổng
        foreach (string code in new[] { "150", "160", "200", "300", "400", "500" })
        {
            Assert.True(Line(template, code).IsCalculated, $"Mã {code} phải là chỉ tiêu công thức (IsCalculated)");
        }
        Assert.False(Line(template, "137").IsCalculated, "137 (Trong đó) là dòng thuyết minh, không phải công thức");
    }

    // ── T2: B09-HTX render test (qua NotesService) ────────────────────────

    [Fact]
    public async Task B09Htx_NotesService_ReturnsHtxSections()
    {
        var tenantMock = new Mock<ITenantManagementService>();
        tenantMock.Setup(s => s.GetTenantByIdAsync(It.IsAny<TenantId>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((TenantAggregate)null!);
        var svc = new FinancialStatementNotesService(tenantMock.Object, NullLogger<FinancialStatementNotesService>.Instance);

        var notes = await svc.GenerateAsync(
            Tt71SampleDataSeeder.Tt71TenantId,
            new AccountingPeriod(2026, 5),
            AccountingStandard.TT71_2024);

        Assert.Equal(AccountingStandard.TT71_2024, notes.Standard);
        Assert.Equal(6, notes.Sections.Count());

        // I-VI theo B09-HTX spec
        Assert.Equal("I", notes.Sections.ElementAt(0).SectionCode);
        Assert.Contains("Đặc điểm hoạt động của HTX", notes.Sections.ElementAt(0).SectionTitle);
        Assert.Equal("II", notes.Sections.ElementAt(1).SectionCode);
        Assert.Equal("III", notes.Sections.ElementAt(2).SectionCode);
        Assert.Contains("Chế độ kế toán HTX ban hành kèm theo Thông tư 71/2024/TT-BTC", notes.Sections.ElementAt(2).Content);
        Assert.Equal("IV", notes.Sections.ElementAt(3).SectionCode);
        Assert.Equal(13, notes.Sections.ElementAt(3).SubSections!.Count());
        Assert.Equal("V", notes.Sections.ElementAt(4).SectionCode);
        Assert.Equal(4, notes.Sections.ElementAt(4).SubSections!.Count());
        Assert.Equal("VI", notes.Sections.ElementAt(5).SectionCode);
    }

    private static Tt99TemplateLine Line(Tt99ReportTemplate template, string code)
    {
        var line = template.Lines.FirstOrDefault(l => l.ReportItemCode == code);
        Assert.NotNull(line);
        return line!;
    }
}
