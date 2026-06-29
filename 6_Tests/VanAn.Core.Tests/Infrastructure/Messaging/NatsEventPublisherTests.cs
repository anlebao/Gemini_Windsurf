using Microsoft.Extensions.Logging;
using Moq;
using NATS.Client;
using VanAn.CoreHub.Infrastructure.Messaging;
using Xunit;

namespace VanAn.Core.Tests.Infrastructure.Messaging;

/// <summary>
/// Unit tests for NatsEventPublisher.
/// Uses internal constructor to inject a mock IConnection — no real NATS server needed.
/// </summary>
public class NatsEventPublisherTests
{
    private readonly Mock<ILogger<NatsEventPublisher>> _loggerMock = new();

    // ──────────────────────────────────────────────────────────
    // Test 1: Connected → publishes payload
    // ──────────────────────────────────────────────────────────

    [Fact(DisplayName = "PublishAsync: when connected, calls IConnection.Publish with correct subject and payload")]
    public async Task PublishAsync_WhenConnected_CallsNatsPublish()
    {
        // Arrange
        var connectionMock = new Mock<IConnection>();
        connectionMock.Setup(c => c.State).Returns(ConnState.CONNECTED);

        var publisher = new NatsEventPublisher(connectionMock.Object, _loggerMock.Object);
        var payload = "test-payload"u8.ToArray();

        // Act
        await publisher.PublishAsync("vanan.shoperp.order.created", payload);

        // Assert
        connectionMock.Verify(c => c.Publish("vanan.shoperp.order.created", payload), Times.Once);
    }

    // ──────────────────────────────────────────────────────────
    // Test 2: Not connected → skips publish, no exception
    // ──────────────────────────────────────────────────────────

    [Fact(DisplayName = "PublishAsync: when not connected, logs warning and does NOT throw")]
    public async Task PublishAsync_WhenDisconnected_LogsWarning_DoesNotThrow()
    {
        // Arrange
        var connectionMock = new Mock<IConnection>();
        connectionMock.Setup(c => c.State).Returns(ConnState.CLOSED);

        var publisher = new NatsEventPublisher(connectionMock.Object, _loggerMock.Object);

        // Act — must not throw
        await publisher.PublishAsync("vanan.shoperp.order.created", "data"u8.ToArray());

        // Assert: Publish was NOT called
        connectionMock.Verify(c => c.Publish(It.IsAny<string>(), It.IsAny<byte[]>()), Times.Never);
    }

    // ──────────────────────────────────────────────────────────
    // Test 3: Null connection (NATS offline at startup) → IsConnected false, no exception
    // ──────────────────────────────────────────────────────────

    [Fact(DisplayName = "PublishAsync: when connection is null (NATS unavailable), does NOT throw")]
    public async Task PublishAsync_WhenConnectionNull_DoesNotThrow()
    {
        // Arrange — simulates constructor failing to connect (NATS offline)
        var publisher = new NatsEventPublisher(connection: null, _loggerMock.Object);

        // Act
        await publisher.PublishAsync("vanan.shoperp.order.created", "data"u8.ToArray());

        // Assert: IsConnected false, no exception thrown
        Assert.False(publisher.IsConnected);
    }

    // ──────────────────────────────────────────────────────────
    // Test 4: Dispose → calls Drain then Dispose on connection
    // ──────────────────────────────────────────────────────────

    [Fact(DisplayName = "Dispose: calls Drain() before Dispose() to flush in-flight messages")]
    public void Dispose_CallsDrainFirst()
    {
        // Arrange
        var callOrder = new List<string>();
        var connectionMock = new Mock<IConnection>();
        connectionMock.Setup(c => c.State).Returns(ConnState.CONNECTED);
        connectionMock.Setup(c => c.Drain()).Callback(() => callOrder.Add("Drain"));
        connectionMock.Setup(c => c.Dispose()).Callback(() => callOrder.Add("Dispose"));

        var publisher = new NatsEventPublisher(connectionMock.Object, _loggerMock.Object);

        // Act
        publisher.Dispose();

        // Assert: Drain called before Dispose
        Assert.Equal(["Drain", "Dispose"], callOrder);
    }

    // ──────────────────────────────────────────────────────────
    // Test 5: Dispose idempotent — second call is safe
    // ──────────────────────────────────────────────────────────

    [Fact(DisplayName = "Dispose: second call is safe (idempotent)")]
    public void Dispose_CalledTwice_IsIdempotent()
    {
        var connectionMock = new Mock<IConnection>();
        connectionMock.Setup(c => c.State).Returns(ConnState.CONNECTED);

        var publisher = new NatsEventPublisher(connectionMock.Object, _loggerMock.Object);

        publisher.Dispose();
        publisher.Dispose(); // must not throw or double-drain

        connectionMock.Verify(c => c.Drain(), Times.Once);
    }
}
