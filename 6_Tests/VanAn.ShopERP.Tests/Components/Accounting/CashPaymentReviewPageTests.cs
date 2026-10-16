using Moq;
using VanAn.CoreHub.Services.Payment;
using VanAn.Shared.Domain;

namespace VanAn.ShopERP.Tests.Components.Accounting;

/// <summary>
/// TUÂN THỦ PHÁP LÝ P0.1 (2026-10-16): trang Rà Soát Thanh Toán Tiền Mặt ≥ 20 Triệu —
/// render 2 khối (đơn hàng tiền mặt + gộp cùng ngày cùng NCC) · empty state success.
/// </summary>
public class CashPaymentReviewPageTests : ComponentTestBase
{
    private void RegisterReviewService(CashPaymentReviewDto report)
    {
        var mock = new Mock<IPaymentComplianceService>();
        mock.SetupGet(s => s.Threshold).Returns(20_000_000m);
        mock.Setup(s => s.GetCashPaymentReviewAsync(It.IsAny<TenantId>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(report);
        Services.AddSingleton(mock.Object);
    }

    [Fact]
    public void Review_Renders_OrderAndAggregateBlocks()
    {
        // Arrange
        RegisterReviewService(new CashPaymentReviewDto(
            2026,
            7,
            20_000_000m,
            new List<CashPaymentReviewItemDto>
            {
                new(Guid.NewGuid(), "DH-001", new DateTime(2026, 7, 10), 25_000_000m, "CASH")
            },
            new List<SameDayAggregateDto>
            {
                new("Cty ABC", new DateTime(2026, 7, 15), 24_000_000m, 2)
            }));

        // Act
        var cut = RenderComponent<ShopERP.Components.Pages.Accounting.CashPaymentReview>();

        // Assert
        cut.WaitForAssertion(() => cut.Markup.Should().Contain("Rà Soát Thanh Toán Tiền Mặt"));
        cut.Markup.Should().Contain("DH-001");
        cut.Markup.Should().Contain("Cty ABC");
    }

    [Fact]
    public void Review_Shows_SuccessMessage_WhenClean()
    {
        // Arrange
        RegisterReviewService(new CashPaymentReviewDto(2026, 7, 20_000_000m, [], []));

        // Act
        var cut = RenderComponent<ShopERP.Components.Pages.Accounting.CashPaymentReview>();

        // Assert
        cut.WaitForAssertion(() => cut.Markup.Should().Contain("Không có giao dịch thanh toán tiền mặt ≥ 20 triệu"));
    }
}