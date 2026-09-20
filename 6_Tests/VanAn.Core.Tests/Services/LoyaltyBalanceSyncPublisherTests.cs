using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using VanAn.CoreHub.Infrastructure.Messaging;
using VanAn.CoreHub.Services;
using VanAn.Shared.Domain;
using Xunit;

namespace VanAn.Tests.Services;

/// <summary>
/// Loyalty Points Integrity (Batch 1) — tests for LoyaltyBalanceSyncPublisher.
/// Verifies subject "vanan.cloud.loyalty.changed.{deviceId}" + extended payload + Outbox routing key,
/// so ShopERP LoyaltySyncSubscriber can mirror the PG balance for BOTH Silo and Alliance earn/spend.
/// </summary>
public class LoyaltyBalanceSyncPublisherTests
{
    private static readonly Guid TestCustomerId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid TestTenantId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly Guid TestDeviceId = Guid.Parse("33333333-3333-3333-3333-333333333333");
    private static readonly Guid TestOrderId = Guid.Parse("44444444-4444-4444-4444-444444444444");

    private readonly Mock<INatsEventPublisher> _natsMock = new();
    private readonly Mock<IOutboxRepository> _outboxMock = new();

    private LoyaltyBalanceSyncPublisher BuildPublisher() =>
        new(_natsMock.Object, _outboxMock.Object, NullLogger<LoyaltyBalanceSyncPublisher>.Instance);

    [Fact(DisplayName = "LPI-B1-1: publishes to vanan.cloud.loyalty.changed.{deviceId} with extended payload")]
    public async Task PublishAsync_UsesDeviceSubject_WithExtendedPayload()
    {
        string? capturedSubject = null;
        byte[]? capturedPayload = null;
        _natsMock
            .Setup(n => n.PublishAsync(It.IsAny<string>(), It.IsAny<byte[]>(), It.IsAny<CancellationToken>()))
            .Callback<string, byte[], CancellationToken>((s, p, _) => { capturedSubject = s; capturedPayload = p; })
            .Returns(Task.CompletedTask);

        await BuildPublisher().PublishAsync(
            customerId: TestCustomerId,
            tenantId: TestTenantId,
            pointBalance: 500,
            type: "EARN",
            points: 150,
            reason: "Hoàn tiền từ chiến dịch X - Đơn hàng #abc",
            customerDeviceId: TestDeviceId,
            sourceOrderId: TestOrderId);

        Assert.Equal($"vanan.cloud.loyalty.changed.{TestDeviceId}", capturedSubject);

        using var doc = JsonDocument.Parse(Encoding.UTF8.GetString(capturedPayload!));
        JsonElement root = doc.RootElement;
        Assert.Equal(TestDeviceId.ToString(), root.GetProperty("customerDeviceId").GetString());
        Assert.Equal(TestCustomerId.ToString(), root.GetProperty("customerId").GetString());
        Assert.Equal(TestTenantId.ToString(), root.GetProperty("tenantId").GetString());
        Assert.Equal(500, root.GetProperty("pointBalance").GetInt32());
        Assert.Equal("EARN", root.GetProperty("type").GetString());
        Assert.Equal(150, root.GetProperty("points").GetInt32());
        Assert.Contains("Đơn hàng #abc", root.GetProperty("reason").GetString()!);
        Assert.False(string.IsNullOrEmpty(root.GetProperty("updatedAt").GetString()));
    }

    [Fact(DisplayName = "LPI-B1-2: enqueues Outbox event LoyaltyChanged with RoutingKey = deviceId")]
    public async Task PublishAsync_EnqueuesOutbox_WithRoutingKey()
    {
        OutboxEvent? captured = null;
        _outboxMock
            .Setup(o => o.EnqueueAsync(It.IsAny<OutboxEvent>(), It.IsAny<CancellationToken>()))
            .Callback<OutboxEvent, CancellationToken>((e, _) => captured = e)
            .Returns(Task.CompletedTask);

        await BuildPublisher().PublishAsync(
            customerId: TestCustomerId,
            tenantId: TestTenantId,
            pointBalance: 500,
            type: "EARN",
            points: 150,
            reason: "reason",
            customerDeviceId: TestDeviceId,
            sourceOrderId: TestOrderId);

        Assert.NotNull(captured);
        Assert.Equal(EventTypes.LoyaltyChanged, captured!.EventType);
        Assert.Equal(TestDeviceId.ToString(), captured.RoutingKey);
        Assert.Equal(TestOrderId, captured.CorrelationId);
        Assert.Equal(TestTenantId, captured.TenantId.Value);
        // EventData is the same extended payload (NatsSyncWorker publishes it verbatim)
        using var doc = JsonDocument.Parse(captured.EventData);
        Assert.Equal("EARN", doc.RootElement.GetProperty("type").GetString());
    }

    [Fact(DisplayName = "LPI-B1-3: subject suffix falls back to customerId when no deviceId")]
    public async Task PublishAsync_NoDeviceId_FallsBackToCustomerIdSuffix()
    {
        string? capturedSubject = null;
        _natsMock
            .Setup(n => n.PublishAsync(It.IsAny<string>(), It.IsAny<byte[]>(), It.IsAny<CancellationToken>()))
            .Callback<string, byte[], CancellationToken>((s, _, _) => capturedSubject = s)
            .Returns(Task.CompletedTask);

        await BuildPublisher().PublishAsync(
            customerId: TestCustomerId,
            tenantId: TestTenantId,
            pointBalance: 100,
            type: "EARN",
            points: 10,
            reason: "r");

        Assert.Equal($"vanan.cloud.loyalty.changed.{TestCustomerId}", capturedSubject);
    }

    [Fact(DisplayName = "LPI-B1-4: NATS publish failure does not block Outbox enqueue")]
    public async Task PublishAsync_NatsFailure_OutboxStillEnqueued()
    {
        _natsMock
            .Setup(n => n.PublishAsync(It.IsAny<string>(), It.IsAny<byte[]>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("NATS down"));

        OutboxEvent? captured = null;
        _outboxMock
            .Setup(o => o.EnqueueAsync(It.IsAny<OutboxEvent>(), It.IsAny<CancellationToken>()))
            .Callback<OutboxEvent, CancellationToken>((e, _) => captured = e)
            .Returns(Task.CompletedTask);

        await BuildPublisher().PublishAsync(
            customerId: TestCustomerId,
            tenantId: TestTenantId,
            pointBalance: 100,
            type: "EARN",
            points: 10,
            reason: "r",
            customerDeviceId: TestDeviceId);

        Assert.NotNull(captured); // Outbox fallback still enqueued
        Assert.Equal(EventTypes.LoyaltyChanged, captured!.EventType);
    }
}
