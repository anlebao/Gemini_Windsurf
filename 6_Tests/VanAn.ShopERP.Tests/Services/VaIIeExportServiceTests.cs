using FluentAssertions;
using OfficeOpenXml;
using VanAn.CoreHub.Services.InventoryIntelligence;
using VanAn.CoreHub.Services.InventoryIntelligence.Dtos;
using VanAn.ShopERP.Services;
using VanAn.Shared.Domain;
using AlertSeverity = VanAn.Shared.Domain.AlertSeverity;

namespace VanAn.ShopERP.Tests.Services;

/// <summary>
/// VA-IIE Phase 4 (2026-10-05): Export Excel — Shift Report + Forecast + Profitability (SRS §5.2, §8.1/8.6).
/// Assert bytes là file xlsx hợp lệ (PK zip header + sheet names đúng).
/// </summary>
public class VaIIeExportServiceTests
{
    private static readonly Guid ShiftId = Guid.Parse("11111111-1111-1111-1111-111111111111");

    [Fact]
    public async Task ExportShiftReportExcel_ProducesValidWorkbook()
    {
        var report = new ShiftReportDto
        {
            Shift = new ShiftDto(ShiftId, ShiftType.Full, ShiftStatus.Submitted,
                DateTime.UtcNow.AddHours(-4), DateTime.UtcNow, Guid.NewGuid(), null, null, "Bàn giao OK"),
            OpeningCounts = [new InventoryCountDto(Guid.NewGuid(), "Cà phê bột", "g", CountType.Opening, 1000m, null)],
            ClosingCounts = [new InventoryCountDto(Guid.NewGuid(), "Cà phê bột", "g", CountType.Closing, 800m, null)],
            Consumptions =
            [
                new ConsumptionDto(Guid.NewGuid(), "Cà phê bột", "g", 150m, 200m, 50m, 33.33m, VarianceClassification.Loss)
            ],
            Cash = new CashSectionDto(2_000_000m, 1_950_000m, 50_000m, false),
            Alerts = [new ShiftAlertDto(Guid.NewGuid(), "CASH_MISMATCH", AlertSeverity.Critical, "Tiền mặt chênh 50.000", null, 50_000m, null, false)],
            Orders = [new OrderSummaryDto(Guid.NewGuid(), "DINEIN", 100_000m, DateTime.UtcNow, 2)]
        };

        byte[] bytes = await VaIIeExportService.ExportShiftReportExcelAsync(report);

        bytes.Should().NotBeEmpty();
        bytes[0].Should().Be((byte)'P'); // PK zip
        bytes[1].Should().Be((byte)'K');
        using var package = new ExcelPackage(new MemoryStream(bytes));
        package.Workbook.Worksheets.Should().ContainSingle().Which.Name.Should().Be("Báo cáo ca");
    }

    [Fact]
    public async Task ExportForecastExcel_HasRestockAndStockoutSheets()
    {
        var report = new ForecastReport(
            DateTime.UtcNow, 14, 2, 1,
            [
                new ForecastItem(Guid.NewGuid(), "Cà phê bột", "g", 200m, 75m, 2, 3, 25m, ForecastStatus.Critical)
            ],
            [
                new ForecastItem(Guid.NewGuid(), "Sữa đặc", "lon", 10m, 3m, 3, 3, 0m, ForecastStatus.Critical)
            ],
            new Dictionary<Guid, IReadOnlyList<DailyConsumption>>());

        byte[] bytes = await VaIIeExportService.ExportForecastExcelAsync(report);

        using var package = new ExcelPackage(new MemoryStream(bytes));
        var names = package.Workbook.Worksheets.Select(w => w.Name).ToList();
        names.Should().Contain("Restock");
        names.Should().Contain("Stockout");
    }

    [Fact]
    public async Task ExportProfitabilityExcel_HasSummaryAndItems()
    {
        var report = new ProfitabilityReport(
            ShiftId, 2_000_000m, 1_450_000m, 550_000m, 27.5m,
            [new ProfitabilityPerItem(Guid.NewGuid(), "Cà phê sữa đá", 100, 1_200_000m, 522_500m, 677_500m, 56.46m)]);

        byte[] bytes = await VaIIeExportService.ExportProfitabilityExcelAsync(report);

        using var package = new ExcelPackage(new MemoryStream(bytes));
        package.Workbook.Worksheets.Should().ContainSingle().Which.Name.Should().Be("Lợi nhuận ca");
        // Cell chứa doanh số ca (FormatVnd — invariant culture, comma separator)
        var sheet = package.Workbook.Worksheets[0];
        sheet.Cells[4, 2].Text.Should().Contain("2,000,000");
    }
}
