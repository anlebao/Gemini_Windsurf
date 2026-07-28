using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using VanAn.CoreHub.Infrastructure;
using VanAn.CoreHub.Infrastructure.Messaging;
using VanAn.Shared.Domain;
using VanAn.Shared.Domain.Common;
using Xunit;
using FluentAssertions;
using CoreOutboxMessage = VanAn.CoreHub.Infrastructure.OutboxMessage;

namespace VanAn.Core.Tests.Services;

/// <summary>
/// EVIDENCE-GATHERING TEST: reproduces production scenario with file-based SQLite (WAL mode),
/// NOT in-memory. Verifies:
///   Evidence 4: ToDomain reflection correctly preserves OutboxEventId == DB Id
///   Evidence 1: ExecuteUpdateAsync returns rowsAffected > 0 and persists to disk
/// </summary>
public class OutboxFileBasedEvidenceTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly VanAnDbContext _dbContext;
    private readonly OutboxRepository _sut;
    private readonly string _dbPath;

    public OutboxFileBasedEvidenceTests()
    {
        // File-based SQLite in WAL mode — mimics production
        _dbPath = Path.Combine(Path.GetTempPath(), $"vanan-outbox-evidence-{Guid.NewGuid():N}.db");
        _connection = new SqliteConnection($"Data Source={_dbPath}");
        _connection.Open();

        // Enable WAL mode (same as production)
        using (var cmd = _connection.CreateCommand())
        {
            cmd.CommandText = "PRAGMA journal_mode=WAL;";
            cmd.ExecuteNonQuery();
        }

        var options = new DbContextOptionsBuilder<VanAnDbContext>()
            .UseSqlite(_connection)
            .Options;

        _dbContext = new VanAnDbContext(options, new StubTenantProvider());
        _dbContext.Database.EnsureCreated();

        _sut = new OutboxRepository(_dbContext);
    }

    [Fact(DisplayName = "EVIDENCE 4: ToDomain reflection preserves OutboxEventId == DB Id")]
    public async Task ToDomain_Reflection_Preserves_OutboxEventId()
    {
        // Arrange — insert OutboxMessage with a known Id
        var knownId = Guid.NewGuid();
        var tenantId = new TenantId(Guid.NewGuid());
        var message = new CoreOutboxMessage
        {
            Id = knownId,
            EventType = "OrderStatusChanged",
            EventData = "{\"id\":\"test\"}",
            CreatedAt = DateTime.UtcNow,
            TenantId = tenantId,
            Status = OutboxMessageStatus.Pending,
            RetryCount = 0,
            NextRetryAt = DateTime.UtcNow
        };
        _dbContext.OutboxMessages.Add(message);
        await _dbContext.SaveChangesAsync();

        // Act — GetPendingEventsAsync calls ToDomain which uses reflection
        var pending = await _sut.GetPendingEventsAsync(batchSize: 10);

        // Assert — EVIDENCE 4: OutboxEventId must match DB Id
        pending.Should().HaveCount(1);
        var ev = pending[0];
        ev.OutboxEventId.Should().Be(knownId,
            "because ToDomain reflection must set OutboxEventId = m.Id. " +
            "If this fails, reflection is broken and MarkAsProcessedAsync will UPDATE 0 rows.");
    }

    [Fact(DisplayName = "EVIDENCE 1: ExecuteUpdateAsync returns rowsAffected > 0 and persists")]
    public async Task ExecuteUpdateAsync_ReturnsPositiveRowsAffected_AndPersists()
    {
        // Arrange — insert OutboxMessage
        var knownId = Guid.NewGuid();
        var tenantId = new TenantId(Guid.NewGuid());
        var message = new CoreOutboxMessage
        {
            Id = knownId,
            EventType = "OrderStatusChanged",
            EventData = "{\"id\":\"test\"}",
            CreatedAt = DateTime.UtcNow,
            TenantId = tenantId,
            Status = OutboxMessageStatus.Pending,
            RetryCount = 0,
            NextRetryAt = DateTime.UtcNow
        };
        _dbContext.OutboxMessages.Add(message);
        await _dbContext.SaveChangesAsync();

        // Act — call MarkAsProcessedAsync (uses ExecuteUpdateAsync internally)
        await _sut.MarkAsProcessedAsync(knownId);

        // Assert — re-read from a FRESH DbContext (new scope, like NatsSyncWorker)
        await using var freshConnection = new SqliteConnection($"Data Source={_dbPath}");
        await freshConnection.OpenAsync();
        var freshOptions = new DbContextOptionsBuilder<VanAnDbContext>()
            .UseSqlite(freshConnection)
            .Options;
        await using var freshCtx = new VanAnDbContext(freshOptions, new StubTenantProvider());

        var updated = await freshCtx.OutboxMessages
            .IgnoreQueryFilters()
            .AsNoTracking()
            .FirstOrDefaultAsync(m => m.Id == knownId);

        updated.Should().NotBeNull();
        updated!.Status.Should().Be(OutboxMessageStatus.Processed,
            "because ExecuteUpdateAsync must persist Status change to disk. " +
            "If this fails, the UPDATE is rolling back or 0 rows affected.");
        updated.ProcessedAt.Should().NotBeNull();
    }

    [Fact(DisplayName = "FULL ROUND-TRIP: GetPending → MarkAsProcessed → GetPending returns empty")]
    public async Task FullRoundTrip_GetPending_MarkProcessed_GetPending_Empty()
    {
        // Arrange
        var knownId = Guid.NewGuid();
        var tenantId = new TenantId(Guid.NewGuid());
        _dbContext.OutboxMessages.Add(new CoreOutboxMessage
        {
            Id = knownId,
            EventType = "OrderCreated",
            EventData = "{\"id\":\"test\"}",
            CreatedAt = DateTime.UtcNow,
            TenantId = tenantId,
            Status = OutboxMessageStatus.Pending,
            RetryCount = 0,
            NextRetryAt = DateTime.UtcNow
        });
        await _dbContext.SaveChangesAsync();

        // Act 1 — GetPending returns the event
        var pending1 = await _sut.GetPendingEventsAsync();
        pending1.Should().HaveCount(1);
        var ev = pending1[0];

        // Act 2 — MarkAsProcessed using the OutboxEventId from ToDomain
        await _sut.MarkAsProcessedAsync(ev.OutboxEventId);

        // Act 3 — GetPending again (fresh scope, like next poll cycle)
        using var scope2 = new ServiceScopeSimulator(_dbPath);
        var pending2 = await scope2.Repository.GetPendingEventsAsync();

        // Assert — event should be gone from pending list
        pending2.Should().BeEmpty(
            "because after MarkAsProcessed the event should no longer appear in GetPending. " +
            "If this fails, the UPDATE is not persisting — this is the production bug.");
    }

    [Fact(DisplayName = "ROOT CAUSE FIX: MarkAsProcessed works with lowercase Id in DB (COLLATE NOCASE)")]
    public async Task MarkAsProcessed_LowercaseIdInDb_StillUpdates()
    {
        // Arrange — insert row with LOWERCASE Id (simulates NATS sync / JSON deserialization)
        var knownId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        var tenantId = new TenantId(Guid.NewGuid());

        // Use raw SQL to insert lowercase Id (EF Core would store UPPERCASE)
        await _dbContext.Database.ExecuteSqlRawAsync(
            "INSERT INTO OutboxMessages (Id, EventType, EventData, CreatedAt, TenantId, Status, RetryCount, NextRetryAt) " +
            "VALUES ({0}, {1}, {2}, {3}, {4}, 0, 0, {5})",
            knownId.ToString("D").ToLowerInvariant(), // explicit lowercase
            "OrderStatusChanged",
            "{\"id\":\"test\"}",
            DateTime.UtcNow.ToString("o"),
            tenantId.Value.ToString(),
            DateTime.UtcNow.ToString("o"));

        // Act — MarkAsProcessed with Guid (EF Core would send UPPERCASE, but COLLATE NOCASE handles it)
        await _sut.MarkAsProcessedAsync(knownId);

        // Assert — re-read from fresh DbContext
        await using var freshConnection = new SqliteConnection($"Data Source={_dbPath}");
        await freshConnection.OpenAsync();
        var freshOptions = new DbContextOptionsBuilder<VanAnDbContext>()
            .UseSqlite(freshConnection)
            .Options;
        await using var freshCtx = new VanAnDbContext(freshOptions, new StubTenantProvider());

        var updated = await freshCtx.Set<CoreOutboxMessage>()
            .FromSqlRaw("SELECT * FROM OutboxMessages WHERE Id COLLATE NOCASE = {0}", knownId.ToString("D").ToUpperInvariant())
            .AsNoTracking()
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync();

        updated.Should().NotBeNull();
        updated!.Status.Should().Be(OutboxMessageStatus.Processed,
            "because COLLATE NOCASE must match lowercase DB row with UPPERCASE parameter. " +
            "This is the ROOT CAUSE fix for the production outbox stuck loop.");
    }

    public void Dispose()
    {
        _dbContext.Dispose();
        _connection.Dispose();
        try { File.Delete(_dbPath); } catch { }
        try { File.Delete(_dbPath + "-wal"); } catch { }
        try { File.Delete(_dbPath + "-shm"); } catch { }
    }

    private sealed class StubTenantProvider : ITenantProvider
    {
        public Guid TenantId => Guid.Empty;
        public string? CurrentUser => null;
        public bool HasTenant => false;
        public void SetTenant(Guid tenantId) { }
    }

    /// <summary>
    /// Simulates a DI scope with a fresh DbContext (like NatsSyncWorker creates per poll cycle).
    /// </summary>
    private sealed class ServiceScopeSimulator : IDisposable
    {
        private readonly SqliteConnection _conn;
        private readonly VanAnDbContext _ctx;
        public OutboxRepository Repository { get; }

        public ServiceScopeSimulator(string dbPath)
        {
            _conn = new SqliteConnection($"Data Source={dbPath}");
            _conn.Open();
            var options = new DbContextOptionsBuilder<VanAnDbContext>()
                .UseSqlite(_conn)
                .Options;
            _ctx = new VanAnDbContext(options, new StubTenantProvider());
            Repository = new OutboxRepository(_ctx);
        }

        public void Dispose()
        {
            _ctx.Dispose();
            _conn.Dispose();
        }
    }
}
