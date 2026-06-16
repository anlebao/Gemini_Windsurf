using Microsoft.EntityFrameworkCore;
using VanAn.CoreHub.Infrastructure;
using VanAn.CoreHub.Infrastructure.Messaging;
using VanAn.Integration.Tests.Infrastructure;
using VanAn.Shared.Domain;
using Xunit;
using FluentAssertions;

namespace VanAn.Integration.Tests.Services;

public class OutboxIntegrationTests : IntegrationTestBase
{
    private readonly OutboxRepository _sut;
    private readonly TenantId _tenantId = TestTenantId;

    public OutboxIntegrationTests()
    {
        _sut = new OutboxRepository(_dbContext);
    }

    private OutboxEvent CreateEvent(string eventType = "InvoiceSubmitted") =>
        new(_tenantId, new ElectronicInvoiceId(Guid.NewGuid()), eventType, "{\"amount\":100000}");

    private async Task EnqueueAndSaveAsync(OutboxEvent e)
    {
        await _sut.EnqueueAsync(e);
        await _dbContext.SaveChangesAsync();
    }

    [Fact]
    public async Task EnqueueAsync_ThenSave_ThenGetPending_ShouldReturnSavedEvent()
    {
        var e = CreateEvent();

        await EnqueueAndSaveAsync(e);
        var pending = await _sut.GetPendingEventsAsync();

        pending.Should().HaveCount(1);
        pending[0].EventType.Should().Be("InvoiceSubmitted");
        pending[0].Status.Should().Be(EventStatus.Pending);
    }

    [Fact]
    public async Task EnqueueAsync_MultipleEvents_GetPending_ShouldReturnAll()
    {
        await EnqueueAndSaveAsync(CreateEvent());
        await EnqueueAndSaveAsync(CreateEvent());
        await EnqueueAndSaveAsync(CreateEvent());

        var pending = await _sut.GetPendingEventsAsync(batchSize: 10);

        pending.Should().HaveCount(3);
    }

    [Fact]
    public async Task GetPendingEventsAsync_RespectsMaxBatchSize()
    {
        await EnqueueAndSaveAsync(CreateEvent());
        await EnqueueAndSaveAsync(CreateEvent());
        await EnqueueAndSaveAsync(CreateEvent());
        await EnqueueAndSaveAsync(CreateEvent());
        await EnqueueAndSaveAsync(CreateEvent());

        var pending = await _sut.GetPendingEventsAsync(batchSize: 2);

        pending.Should().HaveCount(2);
    }

    [Fact]
    public async Task MarkAsProcessedAsync_ShouldUpdateStatus()
    {
        var e = CreateEvent();
        await EnqueueAndSaveAsync(e);

        await _sut.MarkAsProcessedAsync(e.OutboxEventId);

        var result = await _sut.GetByIdAsync(e.OutboxEventId);
        result.Should().NotBeNull();
        result!.Status.Should().Be(EventStatus.Processed);
        result.ProcessedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task MarkAsFailedAsync_ShouldUpdateStatusAndIncrementRetry()
    {
        var e = CreateEvent();
        await EnqueueAndSaveAsync(e);

        await _sut.MarkAsFailedAsync(e.OutboxEventId, "Connection refused");

        var result = await _sut.GetByIdAsync(e.OutboxEventId);
        result.Should().NotBeNull();
        result!.Status.Should().Be(EventStatus.Failed);
        result.RetryCount.Should().Be(1);
        result.ErrorDetails.Should().Be("Connection refused");
    }

    [Fact]
    public async Task MarkAsFailedAsync_CalledTwice_ShouldIncrementRetryCountTwice()
    {
        var e = CreateEvent();
        await EnqueueAndSaveAsync(e);

        await _sut.MarkAsFailedAsync(e.OutboxEventId, "Error 1");
        await _sut.MarkAsFailedAsync(e.OutboxEventId, "Error 2");

        var result = await _sut.GetByIdAsync(e.OutboxEventId);
        result!.RetryCount.Should().Be(2);
        result.ErrorDetails.Should().Be("Error 2");
    }

    [Fact]
    public async Task GetPendingEventsAsync_AfterMarkProcessed_ShouldExcludeProcessed()
    {
        var e1 = CreateEvent("InvoiceSubmitted");
        var e2 = CreateEvent("InvoiceConfirmed");
        await EnqueueAndSaveAsync(e1);
        await EnqueueAndSaveAsync(e2);

        await _sut.MarkAsProcessedAsync(e1.OutboxEventId);
        var pending = await _sut.GetPendingEventsAsync();

        pending.Should().HaveCount(1);
        pending[0].EventType.Should().Be("InvoiceConfirmed");
    }

    [Fact]
    public async Task GetByIdAsync_ExistingEvent_ShouldReturnCorrectEvent()
    {
        var e = CreateEvent("InvoiceConfirmed");
        await EnqueueAndSaveAsync(e);

        var result = await _sut.GetByIdAsync(e.OutboxEventId);

        result.Should().NotBeNull();
        result!.EventType.Should().Be("InvoiceConfirmed");
        result.TenantId.Should().Be(_tenantId);
    }

    [Fact]
    public async Task GetByIdAsync_NonExistingId_ShouldReturnNull()
    {
        var result = await _sut.GetByIdAsync(Guid.NewGuid());

        result.Should().BeNull();
    }

    [Fact]
    public async Task AtomicTransaction_WhenExceptionAfterInvoiceAdd_NeitherInvoiceNorOutboxPersisted()
    {
        var tenantId = TestTenantId;
        var invoiceId = new ElectronicInvoiceId(Guid.NewGuid());

        var invoice = new ElectronicInvoice(
            tenantId,
            new OrderId(Guid.NewGuid()),
            new InvoiceIdempotencyKey("IDEM-ATOMIC-TEST"),
            InvoiceType.Goods,
            100_000m, 10_000m, 110_000m,
            "Test Customer", "0123456789", "123 Test St");

        var outboxEvent = new OutboxEvent(
            tenantId, invoice.InvoiceId, "InvoiceCreated", "{\"test\":true}");

        // Act: begin transaction, add both, then ROLLBACK (simulating failure)
        using var transaction = await _dbContext.Database.BeginTransactionAsync();
        try
        {
            await _dbContext.ElectronicInvoices.AddAsync(invoice);
            await _sut.EnqueueAsync(outboxEvent);
            await _dbContext.SaveChangesAsync();

            // Simulate failure AFTER flush but BEFORE commit
            throw new InvalidOperationException("Simulated infrastructure failure");
        }
        catch
        {
            await transaction.RollbackAsync();
        }

        // Assert: NEITHER invoice NOR outbox event persisted
        // Need fresh context to verify (detach tracked entities)
        _dbContext.ChangeTracker.Clear();

        var invoices = await _dbContext.ElectronicInvoices.CountAsync();
        var outboxMessages = await _dbContext.OutboxMessages.CountAsync();

        invoices.Should().Be(0, "invoice must be rolled back on transaction failure");
        outboxMessages.Should().Be(0, "outbox event must be rolled back on transaction failure");
    }
}
