using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Moq.Protected;
using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using VanAn.Gateway.Controllers;
using VanAn.Shared.Domain;
using VanAn.Shared.Domain.Common;
using VanAn.Shared.Services;
using Xunit;

namespace VanAn.Tests.Services;

/// <summary>
/// Loyalty Alliance Phase 3B — tests for LoyaltyController.GetWallet (Gateway).
/// Verifies the wallet endpoint resolves customer token via ShopERP /api/loyalty/my-identity,
/// then queries PG AllianceWallet + AllianceTransactions via IAllianceWalletService.
/// Uses mocked IHttpClientFactory (for identity resolution HTTP call) + mocked IAllianceWalletService.
/// Spec: docs/specs/loyalty-alliance-spec.md v1.0.
/// </summary>
public class LoyaltyWalletControllerTests
{
    private static readonly Guid TestDeviceId = Guid.NewGuid();
    private static readonly Guid TestWalletId = Guid.NewGuid();
    private static readonly Guid TestTenantId = Guid.Parse("00000000-0000-0000-0000-000000000001");

    /// <summary>
    /// Build a LoyaltyController with mocked IHttpClientFactory + IAllianceWalletService.
    /// The HTTP mock returns the identity response (deviceId) for the /api/loyalty/my-identity call.
    /// </summary>
    private static (LoyaltyController controller, Mock<IAllianceWalletService> walletMock)
        BuildController(
            HttpStatusCode identityStatus = HttpStatusCode.OK,
            Guid? identityDeviceId = null, // null → use TestDeviceId; Guid.Empty → send null in JSON
            string? identityPhoneNumber = "0901234567",
            AllianceWallet? wallet = null,
            IReadOnlyList<AllianceTransaction>? transactions = null)
    {
        // Guid.Empty is sentinel for "send null deviceId in JSON response"
        Guid effectiveDeviceId = identityDeviceId.HasValue && identityDeviceId.Value == Guid.Empty
            ? Guid.Empty // will be serialized as null below
            : (identityDeviceId ?? TestDeviceId);
        bool sendNullDeviceId = identityDeviceId.HasValue && identityDeviceId.Value == Guid.Empty;

        // Mock HttpMessageHandler to intercept the ShopERP /api/loyalty/my-identity call
        var httpHandlerMock = new Mock<HttpMessageHandler>(MockBehavior.Strict);
        httpHandlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.Is<HttpRequestMessage>(r => r.RequestUri!.ToString().Contains("/api/loyalty/my-identity")),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage(identityStatus)
            {
                Content = new StringContent(
                    JsonSerializer.Serialize(new
                    {
                        customerId = Guid.NewGuid(),
                        deviceId = sendNullDeviceId ? (Guid?)null : effectiveDeviceId,
                        phoneNumber = identityPhoneNumber
                    }),
                    Encoding.UTF8,
                    "application/json")
            });

        var httpClient = new HttpClient(httpHandlerMock.Object) { BaseAddress = new Uri("http://localhost:5003/") };
        var httpClientFactoryMock = new Mock<IHttpClientFactory>();
        httpClientFactoryMock.Setup(f => f.CreateClient("shoperp")).Returns(httpClient);

        var walletMock = new Mock<IAllianceWalletService>();
        walletMock.Setup(w => w.GetWalletByDeviceIdAsync(It.IsAny<Guid>())).ReturnsAsync(wallet);
        walletMock.Setup(w => w.GetTransactionsAsync(It.IsAny<Guid>(), It.IsAny<int>()))
            .ReturnsAsync(transactions ?? new List<AllianceTransaction>());

        var controller = new LoyaltyController(
            httpClientFactoryMock.Object,
            walletMock.Object,
            NullLogger<LoyaltyController>.Instance);

        // Set up X-Customer-Token header
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext()
        };
        controller.HttpContext.Request.Headers["X-Customer-Token"] = "valid-token";

        return (controller, walletMock);
    }

    private static AllianceWallet CreateWallet(int balance = 500, bool active = true)
    {
        var wallet = new AllianceWallet(TestDeviceId, "0901234567");
        if (balance > 0) wallet.AddPoints(balance);
        if (!active) wallet.Freeze();
        typeof(BaseEntity).GetProperty(nameof(BaseEntity.Id))!.SetValue(wallet, TestWalletId);
        return wallet;
    }

    private static AllianceTransaction CreateTransaction(
        AllianceTransactionType type, int points, int balanceAfter, Guid tenantId, string reason = "Test")
    {
        var tx = new AllianceTransaction(TestWalletId, tenantId, type, points, balanceAfter, reason);
        return tx;
    }

    // ──────────────────────────────────────────────────────────
    // Test 1: Valid token + wallet exists → returns wallet with balance + transactions
    // ──────────────────────────────────────────────────────────

    [Fact(DisplayName = "LA-W-1: GetWallet — valid token + wallet exists returns balance + transactions")]
    public async Task GetWallet_ValidToken_WalletExists_ReturnsWallet()
    {
        var wallet = CreateWallet(balance: 500);
        var transactions = new List<AllianceTransaction>
        {
            CreateTransaction(AllianceTransactionType.EARN, 200, 200, TestTenantId, "Order #1"),
            CreateTransaction(AllianceTransactionType.EARN, 300, 500, TestTenantId, "Order #2")
        };

        var (controller, walletMock) = BuildController(wallet: wallet, transactions: transactions);

        var result = await controller.GetWallet();

        var ok = Assert.IsType<OkObjectResult>(result);
        var walletDto = Assert.IsType<WalletResponse>(ok.Value);
        Assert.Equal(500, walletDto.TotalPointBalance);
        Assert.True(walletDto.IsActive);
        Assert.Equal(2, walletDto.RecentTransactions.Count);
        Assert.Equal("EARN", walletDto.RecentTransactions[0].Type);

        // Breakdown should group by tenant
        Assert.Single(walletDto.Breakdown);
        Assert.Equal(500, walletDto.Breakdown[0].Points);

        // Verify AllianceWalletService was called with correct deviceId
        walletMock.Verify(w => w.GetWalletByDeviceIdAsync(TestDeviceId), Times.Once);
        walletMock.Verify(w => w.GetTransactionsAsync(TestWalletId, 20), Times.Once);
    }

    // ──────────────────────────────────────────────────────────
    // Test 2: Valid token + no wallet → returns empty wallet (balance 0)
    // ──────────────────────────────────────────────────────────

    [Fact(DisplayName = "LA-W-2: GetWallet — valid token + no wallet returns empty balance")]
    public async Task GetWallet_ValidToken_NoWallet_ReturnsEmpty()
    {
        var (controller, walletMock) = BuildController(wallet: null);

        var result = await controller.GetWallet();

        var ok = Assert.IsType<OkObjectResult>(result);
        var walletDto = Assert.IsType<WalletResponse>(ok.Value);
        Assert.Equal(0, walletDto.TotalPointBalance);
        Assert.False(walletDto.IsActive);
        Assert.Empty(walletDto.RecentTransactions);

        walletMock.Verify(w => w.GetWalletByDeviceIdAsync(TestDeviceId), Times.Once);
        walletMock.Verify(w => w.GetTransactionsAsync(It.IsAny<Guid>(), It.IsAny<int>()), Times.Never);
    }

    // ──────────────────────────────────────────────────────────
    // Test 3: No X-Customer-Token → 401
    // ──────────────────────────────────────────────────────────

    [Fact(DisplayName = "LA-W-3: GetWallet — missing token returns 401")]
    public async Task GetWallet_MissingToken_Returns401()
    {
        var httpHandlerMock = new Mock<HttpMessageHandler>(MockBehavior.Strict);
        var httpClient = new HttpClient(httpHandlerMock.Object) { BaseAddress = new Uri("http://localhost:5003/") };
        var httpClientFactoryMock = new Mock<IHttpClientFactory>();
        httpClientFactoryMock.Setup(f => f.CreateClient("shoperp")).Returns(httpClient);

        var walletMock = new Mock<IAllianceWalletService>();
        var controller = new LoyaltyController(
            httpClientFactoryMock.Object,
            walletMock.Object,
            NullLogger<LoyaltyController>.Instance);
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext()
        };
        // No X-Customer-Token header set

        var result = await controller.GetWallet();

        var unauthorized = Assert.IsType<UnauthorizedObjectResult>(result);
        Assert.NotNull(unauthorized.Value);

        walletMock.Verify(w => w.GetWalletByDeviceIdAsync(It.IsAny<Guid>()), Times.Never);
    }

    // ──────────────────────────────────────────────────────────
    // Test 4: Identity service returns 401 → forwarded as 401
    // ──────────────────────────────────────────────────────────

    [Fact(DisplayName = "LA-W-4: GetWallet — identity service 401 returns 401")]
    public async Task GetWallet_IdentityService401_Returns401()
    {
        var (controller, walletMock) = BuildController(identityStatus: HttpStatusCode.Unauthorized);

        var result = await controller.GetWallet();

        var contentResult = Assert.IsType<ContentResult>(result);
        Assert.Equal(401, contentResult.StatusCode);

        walletMock.Verify(w => w.GetWalletByDeviceIdAsync(It.IsAny<Guid>()), Times.Never);
    }

    // ──────────────────────────────────────────────────────────
    // Test 5: DeviceId is null → 404 (customer not in alliance)
    // ──────────────────────────────────────────────────────────

    [Fact(DisplayName = "LA-W-5: GetWallet — null deviceId returns 404 (not in alliance)")]
    public async Task GetWallet_NullDeviceId_Returns404()
    {
        var (controller, walletMock) = BuildController(identityDeviceId: Guid.Empty);

        var result = await controller.GetWallet();

        var notFound = Assert.IsType<NotFoundObjectResult>(result);
        Assert.NotNull(notFound.Value);

        walletMock.Verify(w => w.GetWalletByDeviceIdAsync(It.IsAny<Guid>()), Times.Never);
    }

    // ──────────────────────────────────────────────────────────
    // Test 6: Breakdown groups transactions by tenant correctly
    // ──────────────────────────────────────────────────────────

    [Fact(DisplayName = "LA-W-6: GetWallet — breakdown groups by tenant")]
    public async Task GetWallet_Breakdown_GroupsByTenant()
    {
        var wallet = CreateWallet(balance: 600);
        var tenantA = Guid.Parse("00000000-0000-0000-0000-000000000001");
        var tenantB = Guid.Parse("00000000-0000-0000-0000-000000000002");
        var transactions = new List<AllianceTransaction>
        {
            CreateTransaction(AllianceTransactionType.EARN, 200, 200, tenantA, "Order A1"),
            CreateTransaction(AllianceTransactionType.EARN, 100, 300, tenantA, "Order A2"),
            CreateTransaction(AllianceTransactionType.EARN, 300, 600, tenantB, "Order B1")
        };

        var (controller, _) = BuildController(wallet: wallet, transactions: transactions);

        var result = await controller.GetWallet();

        var ok = Assert.IsType<OkObjectResult>(result);
        var walletDto = Assert.IsType<WalletResponse>(ok.Value);
        Assert.Equal(2, walletDto.Breakdown.Count);

        var breakdownA = walletDto.Breakdown.First(b => b.TenantId == tenantA);
        Assert.Equal(300, breakdownA.Points); // 200 + 100

        var breakdownB = walletDto.Breakdown.First(b => b.TenantId == tenantB);
        Assert.Equal(300, breakdownB.Points);
    }
}
