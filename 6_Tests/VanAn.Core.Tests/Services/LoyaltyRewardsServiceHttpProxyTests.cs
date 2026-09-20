using System.Net;
using System.Text;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using VanAn.CoreHub.Domain.Repositories;
using VanAn.CoreHub.Repositories;
using VanAn.CoreHub.Services;
using VanAn.Core.Tests.Helpers;
using VanAn.ShopERP.Services;
using VanAn.Shared.Domain;
using VanAn.Shared.Services;
using Xunit;

namespace VanAn.Core.Tests.Services;

/// <summary>
/// Loyalty Points Integrity (Batch 2, T1.4) — ShopERP POS proxy for ILoyaltyRewardsService.
///   - writes route through the Gateway PG ledger (internal API)
///   - Gateway unreachable → award/refund return true (D5 skip), spend returns false — NO throw
///   - IdentityLevel gate still enforced locally before forwarding a spend
/// </summary>
public class LoyaltyRewardsServiceHttpProxyTests
{
    private static readonly Guid TestCustomerId = Guid.NewGuid();
    private static readonly Guid TestTenantId = Guid.Parse("00000000-0000-0000-0000-000000000001");

    private static LoyaltyRewardsServiceHttpProxy BuildProxy(
        MockHttpMessageHandler handler,
        Customer? customer = null,
        Mock<ILoyaltyRewardsRepository>? repoMock = null,
        Mock<IShopFeatureSettingsService>? settingsMock = null)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddHttpClient("GatewayInternal", client => client.BaseAddress = new Uri("http://gateway:8080"))
            .ConfigurePrimaryHttpMessageHandler(() => handler);

        var provider = services.BuildServiceProvider();
        var httpFactory = provider.GetRequiredService<IHttpClientFactory>();

        var repo = repoMock ?? new Mock<ILoyaltyRewardsRepository>();
        var customerRepo = new Mock<ICustomerRepository>();
        customerRepo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>())).ReturnsAsync(customer);

        return new LoyaltyRewardsServiceHttpProxy(
            repo.Object,
            customerRepo.Object,
            settingsMock?.Object,
            httpFactory,
            NullLogger<LoyaltyRewardsServiceHttpProxy>.Instance);
    }

    private static MockHttpMessageHandler GatewayDownHandler()
        => new ThrowingHandler();

    private sealed class ThrowingHandler : MockHttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => throw new HttpRequestException("Connection refused");
    }

    // ──────────────────────────────────────────────────────────
    // AddPointsAsync (award via Gateway ledger)
    // ──────────────────────────────────────────────────────────

    [Fact(DisplayName = "PROXY-1: AddPointsAsync happy path → forwards to internal award → true")]
    public async Task AddPoints_GatewayOk_ReturnsTrue()
    {
        var handler = new MockHttpMessageHandler();
        handler.AddResponse("api/internal/loyalty/award", HttpMethod.Post, new { success = true, newBalance = 500 });

        var proxy = BuildProxy(handler);

        bool result = await proxy.AddPointsAsync(TestCustomerId, TestTenantId, 100, "Order #o1");

        result.Should().BeTrue();
    }

    [Fact(DisplayName = "PROXY-2: AddPointsAsync Gateway down → returns true, does NOT throw (D5 skip)")]
    public async Task AddPoints_GatewayDown_ReturnsTrueNoThrow()
    {
        var proxy = BuildProxy(GatewayDownHandler());

        bool result = await proxy.AddPointsAsync(TestCustomerId, TestTenantId, 100, "Order #o1");

        result.Should().BeTrue("award must not fail the order when the Gateway is unreachable");
    }

    [Fact(DisplayName = "PROXY-3: AddPointsAsync skipped by ledger (budget/guard) → true (not an error)")]
    public async Task AddPoints_LedgerSkipped_ReturnsTrue()
    {
        var handler = new MockHttpMessageHandler();
        handler.AddResponse("api/internal/loyalty/award", HttpMethod.Post,
            new { success = true, skipped = true, message = "Budget exhausted for this tenant", newBalance = 0 });

        var proxy = BuildProxy(handler);

        bool result = await proxy.AddPointsAsync(TestCustomerId, TestTenantId, 100, "Order #o1");

        result.Should().BeTrue();
    }

    // ──────────────────────────────────────────────────────────
    // SubtractPointsAsync (spend via Gateway ledger)
    // ──────────────────────────────────────────────────────────

    [Fact(DisplayName = "PROXY-4: SubtractPointsAsync happy path → forwards to internal spend → true")]
    public async Task SubtractPoints_GatewayOk_ReturnsTrue()
    {
        var customer = new Customer(new TenantId(TestTenantId), "Test", "0900000001");
        typeof(VanAn.Shared.Domain.Common.BaseEntity).GetProperty(nameof(VanAn.Shared.Domain.Common.BaseEntity.Id))!.SetValue(customer, TestCustomerId);
        customer.UpgradeIdentityLevel(IdentityLevel.Verified);

        var handler = new MockHttpMessageHandler();
        handler.AddResponse("api/internal/loyalty/spend", HttpMethod.Post, new { success = true, newBalance = 70 });

        var proxy = BuildProxy(handler, customer);

        bool result = await proxy.SubtractPointsAsync(TestCustomerId, TestTenantId, 30, "Redeem: X");

        result.Should().BeTrue();
    }

    [Fact(DisplayName = "PROXY-5: SubtractPointsAsync Gateway down → returns false, does NOT throw")]
    public async Task SubtractPoints_GatewayDown_ReturnsFalseNoThrow()
    {
        var customer = new Customer(new TenantId(TestTenantId), "Test", "0900000001");
        typeof(VanAn.Shared.Domain.Common.BaseEntity).GetProperty(nameof(VanAn.Shared.Domain.Common.BaseEntity.Id))!.SetValue(customer, TestCustomerId);
        customer.UpgradeIdentityLevel(IdentityLevel.Verified);

        var proxy = BuildProxy(GatewayDownHandler(), customer);

        bool result = await proxy.SubtractPointsAsync(TestCustomerId, TestTenantId, 30, "Redeem: X");

        result.Should().BeFalse("a redemption must not succeed without a ledger deduction");
    }

    [Fact(DisplayName = "PROXY-6: SubtractPointsAsync unverified customer → IdentityLevelNotSufficientException (local gate)")]
    public async Task SubtractPoints_UnverifiedCustomer_ThrowsGate()
    {
        var customer = new Customer(new TenantId(TestTenantId), "Test", "0900000001");
        typeof(VanAn.Shared.Domain.Common.BaseEntity).GetProperty(nameof(VanAn.Shared.Domain.Common.BaseEntity.Id))!.SetValue(customer, TestCustomerId);
        // IdentityLevel = Social (below Verified) — default gate requires Verified.

        var proxy = BuildProxy(new MockHttpMessageHandler(), customer);

        var act = () => proxy.SubtractPointsAsync(TestCustomerId, TestTenantId, 30, "Redeem: X");

        await act.Should().ThrowAsync<IdentityLevelNotSufficientException>();
    }

    [Fact(DisplayName = "PROXY-7: SubtractPointsAsync rejected by ledger (insufficient) → false")]
    public async Task SubtractPoints_LedgerRejected_ReturnsFalse()
    {
        var customer = new Customer(new TenantId(TestTenantId), "Test", "0900000001");
        typeof(VanAn.Shared.Domain.Common.BaseEntity).GetProperty(nameof(VanAn.Shared.Domain.Common.BaseEntity.Id))!.SetValue(customer, TestCustomerId);
        customer.UpgradeIdentityLevel(IdentityLevel.Verified);

        var handler = new MockHttpMessageHandler();
        handler.AddRawResponse("api/internal/loyalty/spend", HttpMethod.Post, HttpStatusCode.Conflict,
            "{\"success\":false,\"error\":\"Không đủ điểm — điểm chỉ dùng được tại tenant đã tặng\"}");

        var proxy = BuildProxy(handler, customer);

        bool result = await proxy.SubtractPointsAsync(TestCustomerId, TestTenantId, 30, "Redeem: X");

        result.Should().BeFalse();
    }
}
