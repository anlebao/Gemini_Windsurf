using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using VanAn.CoreHub.Domain.Repositories;
using VanAn.CoreHub.Services;
using VanAn.Shared.Domain;
using VanAn.Shared.Domain.Audit;
using VanAn.Shared.Domain.Common;
using Xunit;

namespace VanAn.Core.Tests.Audit;

/// <summary>
/// Sprint 3 EXPANDED (2026-09-09): Audit toggle gating + hybrid async persist + queue/writer flush tests.
/// Toggle semantics: no flag setting → defaultWhenMissing (audit flags default ON, VALCN default OFF).
/// Hybrid persist: AccountingEntry/PeriodClosing → SYNC (repository direct); Security/KhachLink/other → ASYNC (queue).
/// </summary>
public class AuditToggleAndQueueTests
{
    // ---- Stubs (pattern: StubShopFeatureSettingsService precedent) ----

    /// <summary>Stub IFeatureFlagService — explicit values override; no setting → defaultWhenMissing.</summary>
    private sealed class StubFeatureFlagService : IFeatureFlagService
    {
        private readonly Dictionary<string, bool> _flags = new();

        public void SetFlag(string name, bool enabled) => _flags[name] = enabled;

        public Task<bool> IsEnabledAsync(string featureName, bool defaultWhenMissing = false, CancellationToken ct = default)
            => Task.FromResult(_flags.TryGetValue(featureName, out var v) ? v : defaultWhenMissing);

        public Task<IReadOnlyList<FeatureFlagDto>> GetAllAsync(CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<FeatureFlagDto>>([]);

        public Task SetEnabledAsync(string featureName, bool enabled, Guid updatedBy, CancellationToken ct = default)
        {
            _flags[featureName] = enabled;
            return Task.CompletedTask;
        }
    }

    /// <summary>Stub IAuditLogRepository — counts AddAsync calls (other methods unused).</summary>
    private sealed class StubAuditLogRepository : IAuditLogRepository
    {
        public int AddCount;
        public List<AuditLog> AddedLogs = new();

        public Task<AuditLog> AddAsync(AuditLog auditLog)
        {
            AddCount++;
            AddedLogs.Add(auditLog);
            return Task.FromResult(auditLog);
        }

        public Task<IReadOnlyList<AuditLog>> AddRangeAsync(IEnumerable<AuditLog> auditLogs)
        {
            var list = auditLogs.ToList();
            AddCount += list.Count;
            AddedLogs.AddRange(list);
            return Task.FromResult<IReadOnlyList<AuditLog>>(list);
        }

        public Task<AuditLog?> GetByIdAsync(Guid id) => Task.FromResult<AuditLog?>(null);
        public Task<AuditLogPagedResult> GetByQueryAsync(AuditLogQuery query) => Task.FromResult(new AuditLogPagedResult());
        public Task<AuditLogPagedResult> GetByQueryCrossTenantAsync(AuditLogQuery query) => Task.FromResult(new AuditLogPagedResult());
        public Task<IReadOnlyList<AuditLog>> GetByEntityAsync(AuditableEntityType entityType, Guid entityId, int maxResults = 100)
            => Task.FromResult<IReadOnlyList<AuditLog>>([]);
        public Task<IReadOnlyList<AuditLog>> GetByCorrelationIdAsync(string correlationId)
            => Task.FromResult<IReadOnlyList<AuditLog>>([]);
        public Task<IReadOnlyList<AuditLog>> GetRecentAsync(int count = 50)
            => Task.FromResult<IReadOnlyList<AuditLog>>([]);
        public Task<int> GetCountAsync(DateTime? fromDate = null, DateTime? toDate = null) => Task.FromResult(0);
    }

    private static AuditTrailService CreateService(
        StubFeatureFlagService flags,
        StubAuditLogRepository repo,
        AuditLogQueue queue)
    {
        var tenantProvider = new Mock<ITenantProvider>();
        tenantProvider.SetupGet(t => t.TenantId).Returns(Guid.Empty);
        tenantProvider.SetupGet(t => t.HasTenant).Returns(false);

        var httpAccessor = new Mock<IHttpContextAccessor>();

        return new AuditTrailService(
            repo,
            tenantProvider.Object,
            NullLogger<AuditTrailService>.Instance,
            httpAccessor.Object,
            flags,
            queue);
    }

    // ---- 1. Toggle gating ----

    [Fact]
    public async Task LogCreateAsync_MasterOff_ReturnsNull_NoRepositoryCall()
    {
        var flags = new StubFeatureFlagService();
        flags.SetFlag("Audit_Enabled", false);  // master OFF
        var repo = new StubAuditLogRepository();
        var queue = new AuditLogQueue();
        var sut = CreateService(flags, repo, queue);

        var result = await sut.LogCreateAsync(AuditableEntityType.AccountingEntry, Guid.NewGuid(), "{}");

        Assert.Null(result);
        Assert.Equal(0, repo.AddCount);
        Assert.Equal(0, queue.Count);
    }

    [Fact]
    public async Task LogCreateAsync_AccountingGroupOff_AccountingSuppressed()
    {
        var flags = new StubFeatureFlagService();
        flags.SetFlag("Audit_Accounting", false);  // accounting group OFF (master stays default ON)
        var repo = new StubAuditLogRepository();
        var queue = new AuditLogQueue();
        var sut = CreateService(flags, repo, queue);

        var result = await sut.LogCreateAsync(AuditableEntityType.AccountingEntry, Guid.NewGuid(), "{}");

        Assert.Null(result);
        Assert.Equal(0, repo.AddCount);
    }

    [Fact]
    public async Task LogCreateAsync_AccountingGroupOff_KhachLinkStillLogged()
    {
        var flags = new StubFeatureFlagService();
        flags.SetFlag("Audit_Accounting", false);  // accounting group OFF — khác group không ảnh hưởng
        var repo = new StubAuditLogRepository();
        var queue = new AuditLogQueue();
        var sut = CreateService(flags, repo, queue);

        var result = await sut.LogCreateAsync(AuditableEntityType.KhachLinkInstance, Guid.NewGuid(), "{}");

        Assert.NotNull(result);  // KhachLink → enqueue async
        Assert.Equal(1, queue.Count);
        Assert.Equal(0, repo.AddCount);
    }

    [Fact]
    public async Task LogCreateAsync_NoSetting_DefaultsOn_LogWritten()
    {
        var flags = new StubFeatureFlagService();  // no flags set → defaultWhenMissing semantics (audit ON)
        var repo = new StubAuditLogRepository();
        var queue = new AuditLogQueue();
        var sut = CreateService(flags, repo, queue);

        var result = await sut.LogCreateAsync(AuditableEntityType.AccountingEntry, Guid.NewGuid(), "{}");

        Assert.NotNull(result);  // AccountingEntry → sync write
        Assert.Equal(1, repo.AddCount);
        Assert.Equal(0, queue.Count);
    }

    [Fact]
    public async Task LogSecurityEventAsync_SecurityGroupOff_ReturnsNull()
    {
        var flags = new StubFeatureFlagService();
        flags.SetFlag("Audit_Security", false);
        var repo = new StubAuditLogRepository();
        var queue = new AuditLogQueue();
        var sut = CreateService(flags, repo, queue);

        var result = await sut.LogSecurityEventAsync(AuditActionType.FailedLogin, "test");

        Assert.Null(result);
        Assert.Equal(0, queue.Count);
        Assert.Equal(0, repo.AddCount);
    }

    // ---- 2. Hybrid persist ----

    [Fact]
    public async Task LogCreateAsync_AccountingEntry_WritesSyncDirect()
    {
        var flags = new StubFeatureFlagService();
        var repo = new StubAuditLogRepository();
        var queue = new AuditLogQueue();
        var sut = CreateService(flags, repo, queue);

        var result = await sut.LogCreateAsync(AuditableEntityType.AccountingEntry, Guid.NewGuid(), "{}");

        Assert.NotNull(result);
        Assert.Equal(1, repo.AddCount);  // SYNC — direct repository write
        Assert.Equal(0, queue.Count);
    }

    [Fact]
    public async Task LogCreateAsync_KhachLinkInstance_EnqueuesAsync()
    {
        var flags = new StubFeatureFlagService();
        var repo = new StubAuditLogRepository();
        var queue = new AuditLogQueue();
        var sut = CreateService(flags, repo, queue);

        var result = await sut.LogCreateAsync(AuditableEntityType.KhachLinkInstance, Guid.NewGuid(), "{}");

        Assert.NotNull(result);
        Assert.Equal(1, queue.Count);  // ASYNC — enqueued
        Assert.Equal(0, repo.AddCount);
    }

    [Fact]
    public async Task LogSecurityEventAsync_EnqueuesAsync()
    {
        var flags = new StubFeatureFlagService();
        var repo = new StubAuditLogRepository();
        var queue = new AuditLogQueue();
        var sut = CreateService(flags, repo, queue);

        var result = await sut.LogSecurityEventAsync(
            AuditActionType.FailedLogin, "Failed login: test", ipAddress: "1.2.3.4", userAgent: "test-agent");

        Assert.NotNull(result);
        Assert.Equal(1, queue.Count);
        Assert.Equal(0, repo.AddCount);
        // Structured fields populated on the enqueued log
        Assert.True(queue.TryDequeue(out var enqueued));
        Assert.Equal(AuditableEntityType.SecurityEvent, enqueued!.EntityType);
        Assert.Equal(AuditActionType.FailedLogin, enqueued.Action);
        Assert.Equal("1.2.3.4", enqueued.IpAddress);
        Assert.Equal("test-agent", enqueued.UserAgent);
        Assert.Equal(Guid.Empty, enqueued.EntityId);
    }

    [Fact]
    public async Task LogPeriodCloseAsync_WritesSyncDirect()
    {
        var flags = new StubFeatureFlagService();
        var repo = new StubAuditLogRepository();
        var queue = new AuditLogQueue();
        var sut = CreateService(flags, repo, queue);

        var result = await sut.LogPeriodCloseAsync(new AccountingPeriod(2026, 9), "year-end close");

        Assert.NotNull(result);
        Assert.Equal(1, repo.AddCount);  // PeriodClosing → SYNC path
        Assert.Equal(0, queue.Count);
    }

    // ---- 3. Queue + writer flush ----

    [Fact]
    public void AuditLogQueue_TryEnqueueDequeue_Roundtrip()
    {
        var queue = new AuditLogQueue();
        var ids = new List<Guid>();
        for (int i = 0; i < 5; i++)
        {
            var id = Guid.NewGuid();
            ids.Add(id);
            Assert.True(queue.TryEnqueue(AuditLog.ForCreate(
                new TenantId(Guid.Empty), AuditableEntityType.Customer, id, "{}", "test")));
        }

        Assert.Equal(5, queue.Count);

        // Dequeue FIFO — đúng thứ tự enqueue
        foreach (var expectedId in ids)
        {
            Assert.True(queue.TryDequeue(out var log));
            Assert.Equal(expectedId, log!.EntityId);
        }
        Assert.Equal(0, queue.Count);
        Assert.False(queue.TryDequeue(out _));
    }

    [Fact]
    public async Task AuditLogBackgroundWriter_FlushAsync_WritesBatchToRepo()
    {
        var queue = new AuditLogQueue();
        var repo = new StubAuditLogRepository();
        var services = new ServiceCollection();
        services.AddScoped<IAuditLogRepository>(_ => repo);
        using var provider = services.BuildServiceProvider();
        var writer = new AuditLogBackgroundWriter(
            queue,
            provider.GetRequiredService<IServiceScopeFactory>(),
            NullLogger<AuditLogBackgroundWriter>.Instance);

        for (int i = 0; i < 3; i++)
        {
            queue.TryEnqueue(AuditLog.ForSecurityEvent(
                new TenantId(Guid.Empty), AuditActionType.FailedLogin, $"test-{i}", "system"));
        }
        Assert.Equal(3, queue.Count);

        await writer.FlushAsync();

        Assert.Equal(3, repo.AddCount);  // batch INSERT qua repository
        Assert.Equal(0, queue.Count);    // queue drained
    }

    [Fact]
    public async Task AuditLogBackgroundWriter_FlushAsync_EmptyQueue_NoCall()
    {
        var queue = new AuditLogQueue();
        var repo = new StubAuditLogRepository();
        var services = new ServiceCollection();
        services.AddScoped<IAuditLogRepository>(_ => repo);
        using var provider = services.BuildServiceProvider();
        var writer = new AuditLogBackgroundWriter(
            queue,
            provider.GetRequiredService<IServiceScopeFactory>(),
            NullLogger<AuditLogBackgroundWriter>.Instance);

        await writer.FlushAsync();

        Assert.Equal(0, repo.AddCount);  // queue rỗng → repository không bị call
    }
}
