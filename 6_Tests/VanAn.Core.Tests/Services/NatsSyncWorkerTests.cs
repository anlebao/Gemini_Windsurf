using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using VanAn.CoreHub.Infrastructure.Messaging;
using VanAn.CoreHub.Services;
using VanAn.Shared.Domain;
using Xunit;

namespace VanAn.Core.Tests.Services;

// ──────────────────────────────────────────────────────────────────────────────
// Fake publisher — records publish calls, no real NATS needed
// ──────────────────────────────────────────────────────────────────────────────

internal sealed class FakeNatsEventPublisher : INatsEventPublisher
{
    public bool IsConnected { get; set; } = true;
    public List<(string Subject, byte[] Payload)> Published { get; } = [];
    public bool ThrowOnPublish { get; set; } = false;

    public Task PublishAsync(string subject, byte[] payload, CancellationToken cancellationToken = default)
    {
        if (ThrowOnPublish)
            throw new InvalidOperationException("Simulated NATS publish failure");

        Published.Add((subject, payload));
        return Task.CompletedTask;
    }

    public void Dispose() { }
}

// ──────────────────────────────────────────────────────────────────────────────
// NatsSyncWorker unit tests
// ──────────────────────────────────────────────────────────────────────────────

public class NatsSyncWorkerTests
{
    private static IConfiguration BuildConfig(int pollMs = 50) =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Sync__PollIntervalMs"] = pollMs.ToString(),
                ["Sync__BatchSize"] = "50"
            })
            .Build();

    private static OutboxEvent BuildOutboxEvent(string eventType = "order.created") =>
        new(new TenantId(Guid.NewGuid()),
            new ElectronicInvoiceId(Guid.NewGuid()),
            eventType,
            $"{{\"id\":\"{Guid.NewGuid()}\"}}");

    private static NatsSyncWorker BuildWorker(
        Mock<IOutboxRepository> outboxMock,
        INatsEventPublisher publisher,
        int pollMs = 50)
    {
        var serviceCollection = new ServiceCollection();
        serviceCollection.AddScoped<IOutboxRepository>(_ => outboxMock.Object);
        var sp = serviceCollection.BuildServiceProvider();

        return new NatsSyncWorker(
            sp,
            publisher,
            new Mock<ILogger<NatsSyncWorker>>().Object,
            BuildConfig(pollMs));
    }

    // ──────────────────────────────────────────────────────────
    // Test 1: No pending events → no publish calls
    // ──────────────────────────────────────────────────────────

    [Fact(DisplayName = "ExecuteAsync: no pending events → publisher not called")]
    public async Task ExecuteAsync_NoPendingEvents_DoesNotCallPublish()
    {
        var outboxMock = new Mock<IOutboxRepository>();
        outboxMock
            .Setup(o => o.GetPendingEventsAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        var publisher = new FakeNatsEventPublisher();
        var worker = BuildWorker(outboxMock, publisher);

        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(150));
        await worker.StartAsync(cts.Token);
        await Task.Delay(120, CancellationToken.None);
        await worker.StopAsync(CancellationToken.None);

        Assert.Empty(publisher.Published);
    }

    // ──────────────────────────────────────────────────────────
    // Test 2: 2 pending events → published + marked processed
    // ──────────────────────────────────────────────────────────

    [Fact(DisplayName = "ExecuteAsync: 2 pending events → published twice, both marked processed")]
    public async Task ExecuteAsync_WithPendingEvents_PublishesAndMarksProcessed()
    {
        var ev1 = BuildOutboxEvent("order.created");
        var ev2 = BuildOutboxEvent("loyalty.earned");

        var outboxMock = new Mock<IOutboxRepository>();
        // First poll returns 2 events; subsequent polls return empty
        outboxMock
            .SetupSequence(o => o.GetPendingEventsAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([ev1, ev2])
            .ReturnsAsync([]);

        var publisher = new FakeNatsEventPublisher();
        var worker = BuildWorker(outboxMock, publisher);

        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(200));
        await worker.StartAsync(cts.Token);
        await Task.Delay(120, CancellationToken.None);
        await worker.StopAsync(CancellationToken.None);

        // 2 publishes with correct subjects
        Assert.Equal(2, publisher.Published.Count);
        Assert.Contains(publisher.Published, p => p.Subject == "vanan.shoperp.order.created");
        Assert.Contains(publisher.Published, p => p.Subject == "vanan.shoperp.loyalty.earned");

        // Both marked as processed
        outboxMock.Verify(o => o.MarkAsProcessedAsync(ev1.OutboxEventId, It.IsAny<CancellationToken>()), Times.Once);
        outboxMock.Verify(o => o.MarkAsProcessedAsync(ev2.OutboxEventId, It.IsAny<CancellationToken>()), Times.Once);
    }

    // ──────────────────────────────────────────────────────────
    // Test 3: PublishAsync throws → event marked as failed, worker continues
    // ──────────────────────────────────────────────────────────

    [Fact(DisplayName = "ExecuteAsync: publish failure → event marked as failed, worker does NOT crash")]
    public async Task ExecuteAsync_PublishFails_MarksEventAsFailed_WorkerContinues()
    {
        var failingEvent = BuildOutboxEvent("order.created");

        var outboxMock = new Mock<IOutboxRepository>();
        outboxMock
            .SetupSequence(o => o.GetPendingEventsAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([failingEvent])
            .ReturnsAsync([]);

        var publisher = new FakeNatsEventPublisher { ThrowOnPublish = true };
        var worker = BuildWorker(outboxMock, publisher);

        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(200));

        // Act — must not propagate exception
        await worker.StartAsync(cts.Token);
        await Task.Delay(120, CancellationToken.None);
        await worker.StopAsync(CancellationToken.None);

        // Assert: MarkAsFailed called for the failing event
        outboxMock.Verify(
            o => o.MarkAsFailedAsync(failingEvent.OutboxEventId, It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Once);

        // ProcessedAsync must NOT be called
        outboxMock.Verify(
            o => o.MarkAsProcessedAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    // ──────────────────────────────────────────────────────────
    // Test 4: CancellationToken cancelled → exits gracefully
    // ──────────────────────────────────────────────────────────

    [Fact(DisplayName = "ExecuteAsync: cancellation → worker exits gracefully without exception")]
    public async Task ExecuteAsync_WhenCancelled_ExitsGracefully()
    {
        var outboxMock = new Mock<IOutboxRepository>();
        outboxMock
            .Setup(o => o.GetPendingEventsAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        var publisher = new FakeNatsEventPublisher();
        var worker = BuildWorker(outboxMock, publisher, pollMs: 10);

        using var cts = new CancellationTokenSource();
        await worker.StartAsync(cts.Token);

        // Cancel immediately
        cts.Cancel();

        // StopAsync must complete without throwing
        var ex = await Record.ExceptionAsync(() => worker.StopAsync(CancellationToken.None));
        Assert.Null(ex);
    }
}
