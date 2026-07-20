using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using NATS.Client;
using VanAn.ShopERP.Services;

namespace VanAn.Integration.Tests;

/// <summary>
/// Phase 4 tests: OrderSyncSubscriber must subscribe to routed NATS subjects
/// (vanan.cloud.order.created.{shopInstanceId}) instead of wildcard, and fail
/// fast if SHOP_INSTANCE_ID is not configured.
/// </summary>
public class OrderSyncSubscriberRoutingTests
{
    private static readonly Guid LocalShopInstanceId = new("00000000-0000-0000-0000-000000000001");

    /// <summary>
    /// Testable subclass that exposes the protected ExecuteAsync and overrides
    /// NATS connection creation so no real NATS server is required.
    /// </summary>
    private sealed class TestableOrderSyncSubscriber : OrderSyncSubscriber
    {
        private readonly IConnection _connection;
        public List<string> SubscribedSubjects { get; } = new();

        public TestableOrderSyncSubscriber(
            IServiceProvider serviceProvider,
            IConfiguration configuration,
            IConnection connection)
            : base(serviceProvider, configuration, NullLogger<OrderSyncSubscriber>.Instance)
        {
            _connection = connection;
        }

        protected override IConnection CreateSubscriptionConnection(string url)
        {
            // Capture the URL but return the injected mock connection.
            return _connection;
        }

        protected override void RecordSubscription(string subject)
        {
            SubscribedSubjects.Add(subject);
        }

        public Task InvokeExecuteAsync(CancellationToken ct) => ExecuteAsync(ct);
    }

    private static IConfiguration BuildConfig(string? shopInstanceId)
    {
        var dict = new Dictionary<string, string?>
        {
            ["Nats:Url"] = "nats://localhost:4222"
        };
        if (shopInstanceId is not null)
        {
            dict["ShopInstance:Id"] = shopInstanceId;
        }
        return new ConfigurationBuilder()
            .AddInMemoryCollection(dict)
            .Build();
    }

    private static (Mock<IConnection> conn, IAsyncSubscription fakeSub) BuildMockConnection()
    {
        var fakeSub = new Mock<IAsyncSubscription>().Object;
        var conn = new Mock<IConnection>();
        conn.Setup(c => c.SubscribeAsync(It.IsAny<string>(), It.IsAny<EventHandler<MsgHandlerEventArgs>>()))
            .Returns(fakeSub)
            .Callback<string, EventHandler<MsgHandlerEventArgs>>((s, _) => { /* captured via RecordSubscription */ });
        return (conn, fakeSub);
    }

    [Fact]
    public async Task ExecuteAsync_WithoutShopInstanceId_ThrowsInvalidOperationException()
    {
        var services = new ServiceCollection().BuildServiceProvider();
        var config = BuildConfig(shopInstanceId: null);
        var (conn, _) = BuildMockConnection();
        var sut = new TestableOrderSyncSubscriber(services, config, conn.Object);

        Func<Task> act = () => sut.InvokeExecuteAsync(CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*SHOP_INSTANCE_ID*");
    }

    [Fact]
    public async Task ExecuteAsync_WithEmptyShopInstanceId_ThrowsInvalidOperationException()
    {
        var services = new ServiceCollection().BuildServiceProvider();
        var config = BuildConfig(shopInstanceId: "");
        var (conn, _) = BuildMockConnection();
        var sut = new TestableOrderSyncSubscriber(services, config, conn.Object);

        Func<Task> act = () => sut.InvokeExecuteAsync(CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*SHOP_INSTANCE_ID*");
    }

    [Fact]
    public async Task ExecuteAsync_WithInvalidGuidShopInstanceId_ThrowsInvalidOperationException()
    {
        var services = new ServiceCollection().BuildServiceProvider();
        var config = BuildConfig(shopInstanceId: "not-a-guid");
        var (conn, _) = BuildMockConnection();
        var sut = new TestableOrderSyncSubscriber(services, config, conn.Object);

        Func<Task> act = () => sut.InvokeExecuteAsync(CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*SHOP_INSTANCE_ID*");
    }

    [Fact]
    public async Task ExecuteAsync_WithShopInstanceIdConfigured_SubscribesToRoutedSubject()
    {
        var services = new ServiceCollection().BuildServiceProvider();
        var config = BuildConfig(shopInstanceId: LocalShopInstanceId.ToString());
        var (conn, _) = BuildMockConnection();
        var sut = new TestableOrderSyncSubscriber(services, config, conn.Object);

        await sut.InvokeExecuteAsync(CancellationToken.None);

        sut.SubscribedSubjects.Should().Contain(
            $"vanan.cloud.order.created.{LocalShopInstanceId}");
        sut.SubscribedSubjects.Should().Contain(
            $"vanan.cloud.order.status.changed.{LocalShopInstanceId}");
        // Must NOT subscribe to wildcard/bare subjects (would cause cross-VPS data leak).
        sut.SubscribedSubjects.Should().NotContain("vanan.cloud.order.created");
        sut.SubscribedSubjects.Should().NotContain("vanan.cloud.order.created.>");
    }

    [Fact]
    public async Task ExecuteAsync_WithShopInstanceIdConfigured_DoesNotSubscribeToWildcard()
    {
        var services = new ServiceCollection().BuildServiceProvider();
        var config = BuildConfig(shopInstanceId: LocalShopInstanceId.ToString());
        var (conn, _) = BuildMockConnection();
        var sut = new TestableOrderSyncSubscriber(services, config, conn.Object);

        await sut.InvokeExecuteAsync(CancellationToken.None);

        // No wildcard subscriptions — every subject must include the ShopInstanceId.
        sut.SubscribedSubjects.Should().AllSatisfy(s =>
            s.Should().Contain(LocalShopInstanceId.ToString(),
                "every subscription must be routed to this ShopInstanceId"));
    }
}
