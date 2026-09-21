using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using System.Reflection;
using VanAn.CoreHub.Services;
using VanAn.Gateway.Controllers;
using VanAn.Shared.Domain;
using VanAn.Shared.Domain.Common;
using VanAn.Shared.DTOs;
using VanAn.Shared.Services;
using Xunit;

namespace VanAn.Tests.Services;

/// <summary>
/// Loyalty Points Integrity (Batch 4, Phase 4, T4.2) — the order tracking banner reads the
/// REAL awarded points from the PG ledger (decision D4), never a recomputed estimate.
/// Spec: docs/plans/loyalty-integrity-detail-coding-plan.md §Phase 4 (T4.2).
/// </summary>
public class PublicOrdersControllerTests
{
    private static readonly Guid TestTenantGuid = Guid.Parse("00000000-0000-0000-0000-00000000000a");

    private static PublicOrdersController BuildController(
        Mock<ILoyaltyPointLedgerService> ledgerMock,
        Mock<IShopFeatureSettingsService>? settingsMock = null,
        Mock<IOrderService>? orderServiceMock = null)
    {
        var orderService = orderServiceMock ?? new Mock<IOrderService>();
        var socialCampaign = new Mock<ISocialCampaignService>();
        var tenantProvider = new Mock<ITenantProvider>();

        var settings = settingsMock ?? new Mock<IShopFeatureSettingsService>();
        if (settingsMock is null)
        {
            settings
                .Setup(s => s.GetSettingsAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new ShopFeatureSettingsDto { Loyalty_Program_Enabled = true });
        }

        var controller = new PublicOrdersController(
            orderService.Object,
            socialCampaign.Object,
            tenantProvider.Object,
            dbContext: null,
            settings.Object,
            salesmanService: null,
            ledgerMock.Object,
            NullLogger<PublicOrdersController>.Instance);

        // Controllers resolve HttpContext.RequestAborted during GetPublicOrder.
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext()
        };

        return controller;
    }

    private static Order BuildOrder(OrderStatusId status)
    {
        var order = Order.Create(Guid.NewGuid(), new TenantId(TestTenantGuid), customerId: null, new List<OrderItem>());
        typeof(Order).GetProperty(nameof(Order.Status))!.SetValue(order, status);
        return order;
    }

    private static async Task<(PublicOrderTrackingDto? Dto, Mock<ILoyaltyPointLedgerService> Ledger)> GetPublicOrderAsync(
        Order order, Mock<ILoyaltyPointLedgerService>? ledgerMock = null)
    {
        var ledger = ledgerMock ?? new Mock<ILoyaltyPointLedgerService>();
        if (ledgerMock is null)
        {
            ledger.Setup(l => l.GetAwardedPointsAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((int?)250);
        }

        var orderService = new Mock<IOrderService>();
        orderService
            .Setup(s => s.GetOrderByIdForPublicTrackingAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(order);

        var controller = BuildController(ledger, orderServiceMock: orderService);

        var actionResult = await controller.GetPublicOrder(order.Id);
        // ActionResult<T> wraps Ok() as ObjectResult with StatusCode 200.
        var ok = Assert.IsAssignableFrom<ObjectResult>(actionResult.Result);
        Assert.Equal(200, ok.StatusCode);
        return (Assert.IsType<PublicOrderTrackingDto>(ok.Value), ledger);
    }

    // ──────────────────────────────────────────────────────────
    // T4.2 — banner reads REAL points from the ledger
    // ──────────────────────────────────────────────────────────

    [Fact(DisplayName = "BANNER-1: completed order → banner shows the REAL awarded points from the ledger")]
    public async Task GetPublicOrder_CompletedOrder_ReturnsRealAwardedPoints()
    {
        var (dto, ledger) = await GetPublicOrderAsync(BuildOrder(new OrderStatusId("completed")));

        Assert.Equal(250, dto!.PointsAwarded);
        Assert.True(dto.LoyaltyEnabled);
        ledger.Verify(l => l.GetAwardedPointsAsync(It.IsAny<Guid>(), TestTenantGuid, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact(DisplayName = "BANNER-2: delivered order → banner shows the REAL awarded points from the ledger")]
    public async Task GetPublicOrder_DeliveredOrder_ReturnsRealAwardedPoints()
    {
        var (dto, _) = await GetPublicOrderAsync(BuildOrder(new OrderStatusId("delivered")));

        Assert.Equal(250, dto!.PointsAwarded);
    }

    [Fact(DisplayName = "BANNER-3: order never awarded (ledger null) → banner shows null, not a recompute")]
    public async Task GetPublicOrder_NotAwarded_ReturnsNull()
    {
        var ledger = new Mock<ILoyaltyPointLedgerService>();
        ledger
            .Setup(l => l.GetAwardedPointsAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((int?)null);

        var (dto, _) = await GetPublicOrderAsync(BuildOrder(new OrderStatusId("completed")), ledger);

        Assert.Null(dto!.PointsAwarded);
    }

    [Fact(DisplayName = "BANNER-4: order still pending → no points, ledger NOT queried")]
    public async Task GetPublicOrder_PendingStatus_LedgerNotQueried()
    {
        var ledger = new Mock<ILoyaltyPointLedgerService>();
        var (dto, _) = await GetPublicOrderAsync(BuildOrder(new OrderStatusId("preparing")), ledger);

        Assert.Null(dto!.PointsAwarded);
        ledger.Verify(
            l => l.GetAwardedPointsAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact(DisplayName = "BANNER-5: tenant loyalty disabled → LoyaltyEnabled=false (banner hidden)")]
    public async Task GetPublicOrder_TenantLoyaltyDisabled_LoyaltyEnabledFalse()
    {
        var settings = new Mock<IShopFeatureSettingsService>();
        settings
            .Setup(s => s.GetSettingsAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ShopFeatureSettingsDto { Loyalty_Program_Enabled = false });

        var ledger = new Mock<ILoyaltyPointLedgerService>();
        ledger
            .Setup(l => l.GetAwardedPointsAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((int?)null);

        var order = BuildOrder(new OrderStatusId("completed"));
        var orderService = new Mock<IOrderService>();
        orderService
            .Setup(s => s.GetOrderByIdForPublicTrackingAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(order);

        var controller = BuildController(ledger, settingsMock: settings, orderServiceMock: orderService);

        var actionResult = await controller.GetPublicOrder(order.Id);
        var ok = Assert.IsAssignableFrom<ObjectResult>(actionResult.Result);
        Assert.Equal(200, ok.StatusCode);
        var dto = Assert.IsType<PublicOrderTrackingDto>(ok.Value);

        Assert.Null(dto.PointsAwarded);
        Assert.False(dto.LoyaltyEnabled);
    }
}
