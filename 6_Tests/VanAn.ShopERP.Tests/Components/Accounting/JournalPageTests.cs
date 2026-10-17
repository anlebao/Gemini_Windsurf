using Moq;
using VanAn.CoreHub.Services.GeneralLedger;
using VanAn.Shared.Domain;

namespace VanAn.ShopERP.Tests.Components.Accounting;

/// <summary>
/// TUÂN THỦ PHÁP LÝ P0.2-Đ1 (2026-10-16): trang Sổ Nhật Ký Chung —
/// render header + dòng bút toán (Nợ/Có) · empty state.
/// </summary>
public class JournalPageTests : ComponentTestBase
{
    private void RegisterJournalService(IReadOnlyList<JournalEntryDto> entries)
    {
        var mock = new Mock<IGeneralLedgerService>();
        mock.Setup(s => s.GetJournalAsync(It.IsAny<TenantId>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(entries);
        Services.AddSingleton(mock.Object);
    }

    [Fact]
    public void Journal_Renders_HeaderAndRows()
    {
        // Arrange
        RegisterJournalService(new List<JournalEntryDto>
        {
            new(Guid.NewGuid(), "J20261015-ABC12345", new DateTime(2026, 10, 15), "Thu tiền", null, false, null,
                new List<JournalEntryLineDto>
                {
                    new("111", 1_000_000, 0, null),
                    new("511", 0, 1_000_000, null)
                })
        });

        // Act
        var cut = RenderComponent<ShopERP.Components.Pages.Accounting.JournalReport>();

        // Assert
        cut.WaitForAssertion(() => cut.Markup.Should().Contain("Sổ Nhật Ký Chung"));
        cut.Markup.Should().Contain("J20261015-ABC12345");
        cut.Markup.Should().Contain("Thu tiền");
        cut.Markup.Should().Contain("111");
        cut.Markup.Should().Contain("511");
    }

    [Fact]
    public void Journal_Shows_EmptyState_WhenNoEntries()
    {
        // Arrange
        RegisterJournalService(new List<JournalEntryDto>());

        // Act
        var cut = RenderComponent<ShopERP.Components.Pages.Accounting.JournalReport>();

        // Assert
        cut.WaitForAssertion(() => cut.Markup.Should().Contain("Không có bút toán trong kỳ"));
    }

    [Fact]
    public void Journal_ExportExcel_DownloadsXlsx_WhenClicked()
    {
        // Arrange — nút Xuất Excel → vanAn.downloadFile với mime xlsx (Mẫu S03b-DN).
        var mock = new Mock<IGeneralLedgerService>();
        mock.Setup(s => s.GetJournalAsync(It.IsAny<TenantId>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<JournalEntryDto>
            {
                new(Guid.NewGuid(), "J20261015-ABC12345", new DateTime(2026, 10, 15), "Thu tiền", null, false, null,
                    new List<JournalEntryLineDto>
                    {
                        new("111", 1_000_000, 0, null),
                        new("511", 0, 1_000_000, null)
                    })
            });
        mock.Setup(s => s.GetTenantInfoAsync(It.IsAny<TenantId>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TenantInfoDto("Vạn An Test", "123 Lê Lợi, Q.1"));
        Services.AddSingleton(mock.Object);

        var cut = RenderComponent<ShopERP.Components.Pages.Accounting.JournalReport>();
        cut.WaitForAssertion(() => cut.Markup.Should().Contain("Xuất Excel"));

        // Act
        cut.FindAll("button").First(b => b.TextContent.Contains("Xuất Excel")).Click();

        // Assert — gọi vanAn.downloadFile với base64 + mime xlsx + tên file .xlsx
        cut.WaitForAssertion(() => JSInterop.Invocations.Should().Contain(i => i.Identifier == "vanAn.downloadFile"));
        var args = JSInterop.Invocations.First(i => i.Identifier == "vanAn.downloadFile").Arguments;
        args[1].Should().Be("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet");
        args[2].ToString()!.Should().EndWith(".xlsx");
    }
}