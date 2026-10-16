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
}