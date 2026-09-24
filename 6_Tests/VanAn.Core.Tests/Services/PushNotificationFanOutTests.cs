using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using System.Text.Json;
using VanAn.CoreHub.Domain.Repositories;
using VanAn.CoreHub.Services;
using Xunit;

namespace VanAn.Core.Tests.Services
{
    /// <summary>
    /// NF-4: Role fan-out tests for PushNotificationBackgroundService.HandleEventAsync.
    /// Covers buyer push, salesman-on-completed, assigned-shipper-on-cancelled,
    /// rolesOnly republish (Gateway-resolved recipients), buyer/role dedup, and
    /// strict rejection of malformed payloads (no TryParse stub — corrupt ids
    /// must throw, not silently become "no recipient").
    /// </summary>
    public class PushNotificationFanOutTests
    {
        private static readonly Guid OrderId = Guid.NewGuid();
        private static readonly Guid BuyerId = Guid.NewGuid();
        private static readonly Guid SalesmanId = Guid.NewGuid();
        private static readonly Guid ShipperId = Guid.NewGuid();

        private static (PushNotificationBackgroundService Sut, Mock<PushNotificationService> Push) CreateSut()
        {
            var pushConfig = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["PushNotifications:VapidPrivateKey"] = "test-vapid-private-key",
                    ["PushNotifications:VapidPublicKey"] = "test-vapid-public-key",
                    ["PushNotifications:VapidSubject"] = "mailto:test@vanan.com"
                })
                .Build();

            var pushMock = new Mock<PushNotificationService>(
                pushConfig, NullLogger<PushNotificationService>.Instance,
                Mock.Of<IPushSubscriptionRepository>(), null, null);
            pushMock.Setup(p => p.SendOrderStatusNotificationAsync(
                    It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string?>()))
                .ReturnsAsync(1);
            pushMock.Setup(p => p.SendBulkNotificationAsync(
                    It.IsAny<IReadOnlyList<Guid>>(), It.IsAny<string>(), It.IsAny<string>(),
                    It.IsAny<string?>(), It.IsAny<Guid?>()))
                .ReturnsAsync((1, 0));

            var services = new ServiceCollection()
                .AddSingleton(pushMock.Object)
                .BuildServiceProvider();

            var config = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?> { ["Nats:Url"] = "nats://localhost:4222" })
                .Build();

            var sut = new PushNotificationBackgroundService(
                services, config, NullLogger<PushNotificationBackgroundService>.Instance);
            return (sut, pushMock);
        }

        private static byte[] Payload(object o) => JsonSerializer.SerializeToUtf8Bytes(o);

        [Fact(DisplayName = "NF-4 T1: buyer only — status event pushes customer, no role fan-out")]
        public async Task BuyerOnly_SendsBuyerPush_NoRoleFanOut()
        {
            var (sut, push) = CreateSut();

            await sut.HandleEventAsync(Payload(new
            {
                orderId = OrderId,
                customerId = BuyerId,
                newStatus = "confirmed"
            }), CancellationToken.None);

            push.Verify(p => p.SendOrderStatusNotificationAsync(BuyerId, OrderId, "confirmed", null), Times.Once);
            push.Verify(p => p.SendBulkNotificationAsync(
                It.IsAny<IReadOnlyList<Guid>>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<string?>(), It.IsAny<Guid?>()), Times.Never);
        }

        [Fact(DisplayName = "NF-4 T2: completed + salesmanId → buyer push AND salesman push to sales-dashboard")]
        public async Task Completed_WithSalesman_PushesBuyerAndSalesman()
        {
            var (sut, push) = CreateSut();

            await sut.HandleEventAsync(Payload(new
            {
                orderId = OrderId,
                customerId = BuyerId,
                salesmanId = SalesmanId,
                newStatus = "completed"
            }), CancellationToken.None);

            push.Verify(p => p.SendOrderStatusNotificationAsync(BuyerId, OrderId, "completed", null), Times.Once);
            push.Verify(p => p.SendBulkNotificationAsync(
                It.Is<IReadOnlyList<Guid>>(l => l.Count == 1 && l[0] == SalesmanId),
                "Vạn An",
                It.Is<string>(b => b.Contains("hoàn thành")),
                "/community/sales-dashboard",
                null), Times.Once);
        }

        [Fact(DisplayName = "NF-4 T3: cancelled + assignedShipperId → buyer push AND shipper push to active-deliveries")]
        public async Task Cancelled_WithShipper_PushesBuyerAndShipper()
        {
            var (sut, push) = CreateSut();

            await sut.HandleEventAsync(Payload(new
            {
                orderId = OrderId,
                customerId = BuyerId,
                assignedShipperId = ShipperId,
                newStatus = "cancelled"
            }), CancellationToken.None);

            push.Verify(p => p.SendOrderStatusNotificationAsync(BuyerId, OrderId, "cancelled", null), Times.Once);
            push.Verify(p => p.SendBulkNotificationAsync(
                It.Is<IReadOnlyList<Guid>>(l => l.Count == 1 && l[0] == ShipperId),
                "Vạn An",
                It.Is<string>(b => b.Contains("bị hủy")),
                "/community/active-deliveries",
                null), Times.Once);
        }

        [Fact(DisplayName = "NF-4 T4: rolesOnly republish skips buyer, still pushes role recipient")]
        public async Task RolesOnly_SkipsBuyer_StillPushesRole()
        {
            var (sut, push) = CreateSut();

            await sut.HandleEventAsync(Payload(new
            {
                orderId = OrderId,
                customerId = (Guid?)null,
                salesmanId = SalesmanId,
                newStatus = "completed",
                rolesOnly = true
            }), CancellationToken.None);

            push.Verify(p => p.SendOrderStatusNotificationAsync(
                It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string?>()), Times.Never);
            push.Verify(p => p.SendBulkNotificationAsync(
                It.Is<IReadOnlyList<Guid>>(l => l.Count == 1 && l[0] == SalesmanId),
                It.IsAny<string>(), It.IsAny<string>(), "/community/sales-dashboard", null), Times.Once);
        }

        [Fact(DisplayName = "NF-4 T5: salesmanId == buyer customerId → no duplicate role push")]
        public async Task SalesmanEqualsBuyer_NoDuplicatePush()
        {
            var (sut, push) = CreateSut();

            await sut.HandleEventAsync(Payload(new
            {
                orderId = OrderId,
                customerId = BuyerId,
                salesmanId = BuyerId,
                newStatus = "completed"
            }), CancellationToken.None);

            push.Verify(p => p.SendOrderStatusNotificationAsync(BuyerId, OrderId, "completed", null), Times.Once);
            push.Verify(p => p.SendBulkNotificationAsync(
                It.IsAny<IReadOnlyList<Guid>>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<string?>(), It.IsAny<Guid?>()), Times.Never);
        }

        [Fact(DisplayName = "NF-4 T6: no recipients → nothing sent")]
        public async Task NoRecipients_NothingSent()
        {
            var (sut, push) = CreateSut();

            await sut.HandleEventAsync(Payload(new
            {
                orderId = OrderId,
                newStatus = "completed"
            }), CancellationToken.None);

            push.VerifyNoOtherCalls();
        }

        [Fact(DisplayName = "NF-4 T7: malformed recipient id → event rejected, nothing sent (strict parse, no stub fallback)")]
        public async Task MalformedRecipientId_EventRejected_NothingSent()
        {
            var (sut, push) = CreateSut();

            await sut.HandleEventAsync(Payload(new
            {
                orderId = OrderId,
                customerId = BuyerId,
                salesmanId = "not-a-guid",
                newStatus = "completed"
            }), CancellationToken.None);

            push.Verify(p => p.SendOrderStatusNotificationAsync(
                It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string?>()), Times.Never);
            push.Verify(p => p.SendBulkNotificationAsync(
                It.IsAny<IReadOnlyList<Guid>>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<string?>(), It.IsAny<Guid?>()), Times.Never);
        }

        [Fact(DisplayName = "NF-4 T8: role push does NOT fire on non-matching status (delivering ≠ completed)")]
        public async Task NonMatchingStatus_NoRolePush()
        {
            var (sut, push) = CreateSut();

            await sut.HandleEventAsync(Payload(new
            {
                orderId = OrderId,
                customerId = BuyerId,
                salesmanId = SalesmanId,
                assignedShipperId = ShipperId,
                newStatus = "delivering"
            }), CancellationToken.None);

            push.Verify(p => p.SendOrderStatusNotificationAsync(BuyerId, OrderId, "delivering", null), Times.Once);
            push.Verify(p => p.SendBulkNotificationAsync(
                It.IsAny<IReadOnlyList<Guid>>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<string?>(), It.IsAny<Guid?>()), Times.Never);
        }

        // ---------- E16: DeserializePushSubscription (nested vs flat shape) ----------

        [Fact(DisplayName = "E16 T1: nested NotificationsController shape {Endpoint,Keys:{P256dh,Auth}} deserializes")]
        public void Deserialize_NestedShape_ReturnsSubscription()
        {
            var json = """{"Endpoint":"https://push.example.com/sub/1","Keys":{"P256dh":"BP4x","Auth":"abc123"}}""";

            var sub = PushNotificationService.DeserializePushSubscription(json);

            Assert.NotNull(sub);
            Assert.Equal("https://push.example.com/sub/1", sub!.Endpoint);
            Assert.Equal("BP4x", sub.P256DH);
            Assert.Equal("abc123", sub.Auth);
        }

        [Fact(DisplayName = "E16 T2: flat WebPush shape {endpoint,p256dh,auth} deserializes")]
        public void Deserialize_FlatShape_ReturnsSubscription()
        {
            var json = """{"endpoint":"https://push.example.com/sub/2","p256dh":"BP4x","auth":"abc123"}""";

            var sub = PushNotificationService.DeserializePushSubscription(json);

            Assert.NotNull(sub);
            Assert.Equal("https://push.example.com/sub/2", sub!.Endpoint);
            Assert.Equal("BP4x", sub.P256DH);
        }

        [Fact(DisplayName = "E16 T3: missing keys → null (no send, no throw)")]
        public void Deserialize_MissingKeys_ReturnsNull()
        {
            var json = """{"Endpoint":"https://push.example.com/sub/3"}""";

            Assert.Null(PushNotificationService.DeserializePushSubscription(json));
        }

        [Fact(DisplayName = "E16 T4: invalid JSON → null (no send, no throw)")]
        public void Deserialize_InvalidJson_ReturnsNull()
        {
            Assert.Null(PushNotificationService.DeserializePushSubscription("not-json"));
        }
    }
}
