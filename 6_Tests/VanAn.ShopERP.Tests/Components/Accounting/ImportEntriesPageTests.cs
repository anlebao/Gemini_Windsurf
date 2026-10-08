using Moq;
using VanAn.CoreHub.Services.Import;
using VanAn.Shared.Domain;
using VanAn.ShopERP.Components.Pages.Accounting;

namespace VanAn.ShopERP.Tests.Components.Accounting;

/// <summary>
/// NHẬP LIỆU & SỔ SÁCH P4 (#1 Import Excel, 2026-10-08): trang /accounting/import —
/// render mẫu 8 cột + 2 nút tải mẫu (xlsx/csv qua vanAn.downloadFile) + InputFile upload + bảng lỗi.
/// </summary>
public class ImportEntriesPageTests : ComponentTestBase
{
    private void RegisterImportService(ImportPreviewResult? preview = null, ImportResult? importResult = null)
    {
        var mock = new Mock<IImportService>();
        mock.Setup(s => s.GenerateTemplateAsync(ImportFileFormat.Xlsx, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new byte[] { 0x50, 0x4B, 0x03, 0x04 }); // PK header
        mock.Setup(s => s.GenerateTemplateAsync(ImportFileFormat.Csv, It.IsAny<CancellationToken>()))
            .ReturnsAsync("Ngày,Loại phiếu"u8.ToArray());
        mock.Setup(s => s.PreviewAsync(It.IsAny<TenantId>(), It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(preview ?? new ImportPreviewResult(0, 0, 0, [], "Sẵn sàng lưu: 0 dòng."));
        mock.Setup(s => s.ImportAsync(It.IsAny<TenantId>(), It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(importResult ?? new ImportResult(0, 0, 0, [], "Đã lưu 0/0 dòng."));
        Services.AddSingleton(mock.Object);
    }

    [Fact]
    public void ImportEntries_ShouldRender_TemplateButtonsAndUpload()
    {
        // Arrange
        RegisterImportService();

        // Act
        var cut = RenderComponent<ImportEntries>();

        // Assert
        cut.WaitForAssertion(() => cut.Markup.Should().Contain("Import Dữ Liệu Từ Excel"));
        cut.Markup.Should().Contain("Tải mẫu Excel (.xlsx)");
        cut.Markup.Should().Contain("Tải mẫu CSV (.csv)");
        cut.Markup.Should().Contain("Ngày");
        cut.Markup.Should().Contain("Loại phiếu");
        cut.Markup.Should().Contain("Số chứng từ");
        cut.FindAll("[data-testid='import-file-input']").Should().HaveCount(1);
    }

    [Fact]
    public void ImportEntries_DownloadXlsxTemplate_InvokesDownloadFile()
    {
        // Arrange — Loose mode ghi invocation vào JSInterop.Invocations (assert trực tiếp — VerifyInvoke
        // trên SetupVoid handler không thấy invocation khi Loose route qua auto-handler)
        RegisterImportService();
        var cut = RenderComponent<ImportEntries>();

        // Act
        cut.FindAll("button").First(b => b.TextContent!.Contains("Tải mẫu Excel (.xlsx)")).Click();

        // Assert — gọi vanAn.downloadFile với base64 + mime xlsx + tên file
        cut.WaitForAssertion(() => JSInterop.Invocations.Should().Contain(i => i.Identifier == "vanAn.downloadFile"));
        var args = JSInterop.Invocations.First(i => i.Identifier == "vanAn.downloadFile").Arguments;
        args[1].Should().Be("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet");
        args[2].ToString().Should().EndWith(".xlsx");
    }

    [Fact]
    public void ImportEntries_DownloadCsvTemplate_InvokesDownloadFile()
    {
        // Arrange
        RegisterImportService();
        var cut = RenderComponent<ImportEntries>();

        // Act
        cut.FindAll("button").First(b => b.TextContent!.Contains("Tải mẫu CSV (.csv)")).Click();

        // Assert
        cut.WaitForAssertion(() => JSInterop.Invocations.Should().Contain(i => i.Identifier == "vanAn.downloadFile"));
        var args = JSInterop.Invocations.First(i => i.Identifier == "vanAn.downloadFile").Arguments;
        args[1].Should().Be("text/csv");
        args[2].ToString().Should().EndWith(".csv");
    }
}
