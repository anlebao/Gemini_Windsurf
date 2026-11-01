using VanAn.Shared.Domain;
using VanAn.CoreHub.Services.Journal;

namespace VanAn.ShopERP.Tests.Components.Accounting;

/// <summary>
/// Task card 2026-11-01 (P2): trang Phiếu hạch toán CTGS — render · thêm/xóa dòng ·
/// badge lệch→cân · Lưu disabled khi lệch · lưu thành công → số phiếu + nút in.
/// </summary>
public class JournalVoucherPageTests : ComponentTestBase
{
    private Mock<IManualJournalVoucherService> RegisterVoucherService()
    {
        var mock = new Mock<IManualJournalVoucherService>();
        mock.Setup(s => s.CreateAsync(
                It.IsAny<TenantId>(), It.IsAny<DateTime>(), It.IsAny<string>(),
                It.IsAny<IReadOnlyList<JournalVoucherLine>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new JournalVoucherResult(Guid.NewGuid(), Guid.NewGuid(), "CTGS-2026-00001", 2));
        Services.AddSingleton(mock.Object);
        return mock;
    }

    /// <summary>Điền 1 dòng Nợ + 1 dòng Có cùng số tiền → cân đối.
    /// Re-query mỗi lần — mỗi Change trigger re-render → element cũ stale.</summary>
    private static void FillBalancedLines(Bunit.IRenderedFragment cut, decimal amount = 100_000m)
    {
        var amt = amount.ToString(System.Globalization.CultureInfo.InvariantCulture);
        cut.FindAll("[data-testid='jv-account']").First().Change("111");
        cut.FindAll("[data-testid='jv-debit']").First().Change(amt);
        cut.FindAll("[data-testid='jv-account']").Last().Change("642");
        cut.FindAll("[data-testid='jv-credit']").Last().Change(amt);
    }

    [Fact]
    public void JournalVoucher_ShouldRender_WithTwoLinesAndBalanceBadge()
    {
        // Arrange
        _ = RegisterVoucherService();

        // Act
        var cut = RenderComponent<ShopERP.Components.Pages.Accounting.JournalVoucher>();

        // Assert
        cut.WaitForAssertion(() => cut.Markup.Should().Contain("Phiếu Hạch Toán"));
        cut.FindAll("[data-testid='jv-line']").Should().HaveCount(2);
        cut.Find("[data-testid='jv-balance-badge']").Should().NotBeNull();
        cut.Find("[data-testid='jv-save']").HasAttribute("disabled").Should().BeTrue();
    }

    [Fact]
    public void JournalVoucher_AddAndRemoveLine_ChangesRowCount()
    {
        // Arrange
        _ = RegisterVoucherService();
        var cut = RenderComponent<ShopERP.Components.Pages.Accounting.JournalVoucher>();
        cut.WaitForAssertion(() => cut.FindAll("[data-testid='jv-line']").Should().HaveCount(2));

        // Act — thêm dòng
        cut.Find("[data-testid='jv-add-line']").Click();
        cut.FindAll("[data-testid='jv-line']").Should().HaveCount(3);

        // Act — xóa dòng đầu → về 2
        cut.FindAll("[data-testid='jv-remove-line']").First(b => !b.HasAttribute("disabled")).Click();
        cut.FindAll("[data-testid='jv-line']").Should().HaveCount(2);
    }

    [Fact]
    public void JournalVoucher_BalanceBadge_LechThenCan()
    {
        // Arrange
        _ = RegisterVoucherService();
        var cut = RenderComponent<ShopERP.Components.Pages.Accounting.JournalVoucher>();
        cut.WaitForAssertion(() => cut.Markup.Should().Contain("Lệch"));

        // Act — điền Nợ 100.000 chưa có Có → vẫn lệch
        FillBalancedLines(cut);
        cut.Find("[data-testid='jv-balance-badge']").TextContent.Should().Contain("Cân đối");
    }

    [Fact]
    public void JournalVoucher_SaveDisabled_WhenUnbalanced()
    {
        // Arrange
        _ = RegisterVoucherService();
        var cut = RenderComponent<ShopERP.Components.Pages.Accounting.JournalVoucher>();
        cut.WaitForAssertion(() => cut.FindAll("[data-testid='jv-debit']").Should().HaveCount(2));

        // Act — chỉ Nợ, không Có → lệch → disabled
        cut.FindAll("[data-testid='jv-debit']").First().Change("100000");
        cut.Find("[data-testid='jv-save']").HasAttribute("disabled").Should().BeTrue();
    }

    [Fact]
    public void JournalVoucher_Save_CallsService_AndShowsVoucherNumber()
    {
        // Arrange
        var mock = RegisterVoucherService();
        var cut = RenderComponent<ShopERP.Components.Pages.Accounting.JournalVoucher>();
        cut.WaitForAssertion(() => cut.FindAll("[data-testid='jv-debit']").Should().HaveCount(2));

        // Act — diễn giải + 2 dòng cân → Lưu enabled → click
        cut.Find("[data-testid='jv-description']").Change("Bút toán test");
        FillBalancedLines(cut);
        cut.Find("[data-testid='jv-save']").HasAttribute("disabled").Should().BeFalse();
        cut.Find("[data-testid='jv-save']").Click();

        // Assert — service gọi đúng 1 lần + success alert có số CTGS + nút in hiện
        cut.WaitForAssertion(() => cut.Markup.Should().Contain("CTGS-2026-00001"));
        mock.Verify(s => s.CreateAsync(
            It.IsAny<TenantId>(), It.IsAny<DateTime>(), "Bút toán test",
            It.Is<IReadOnlyList<JournalVoucherLine>>(l => l.Count == 2 && l.Sum(x => x.Debit) == l.Sum(x => x.Credit)),
            It.IsAny<CancellationToken>()), Times.Once);
        cut.Markup.Should().Contain("In phiếu (CTGS)");
    }
}
