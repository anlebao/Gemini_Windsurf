using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using VanAn.CoreHub.Infrastructure;
using VanAn.CoreHub.Infrastructure.Entities;
using VanAn.CoreHub.Repositories;
using VanAn.CoreHub.Services;
using VanAn.Integration.Tests.Infrastructure;
using VanAn.Shared.Domain;
using Xunit;
using CoreAccountingEntry = VanAn.Shared.Domain.AccountingEntry;

namespace VanAn.Integration.Tests.Accounting;

/// <summary>
/// W5: Integration tests for <see cref="PeriodClosingService"/> DB persistence.
/// Uses SQLite in-memory (via <see cref="TestDatabaseFixture"/>) for realistic schema + converter behavior
/// without heavy CI pipeline cost. Mocks only the non-persistence dependencies (repository, reversal, audit).
///
/// Verifies the 4 key persistence invariants:
/// <list type="number">
/// <item>Close period → DB record exists with Status=Closed</item>
/// <item>App restart (fresh DbContext) → status still Closed (survives restart)</item>
/// <item>Reopen → Status=Open + ReopenReason persisted</item>
/// <item>Multi-tenant isolation: tenant A's closed period ≠ tenant B's same period</item>
/// </list>
/// </summary>
public class PeriodClosingPersistenceTests : IClassFixture<TestDatabaseFixture>
{
    private readonly TestDatabaseFixture _fixture;

    public PeriodClosingPersistenceTests(TestDatabaseFixture fixture)
    {
        _fixture = fixture;
    }

    /// <summary>
    /// Create a PeriodClosingService with a real IVanAnDbContext (from fixture) + mocked dependencies.
    /// The mock repository returns a single revenue entry with non-zero amount (passes validation).
    /// </summary>
    private static PeriodClosingService CreateService(
        IAccountingDbContext dbContext,
        TenantId tenantId,
        AccountingPeriod period,
        List<CoreAccountingEntry>? entries = null)
    {
        entries ??=
        [
            CoreAccountingEntry.CreateRevenue(tenantId, period, new Money(1_000_000m, "VND"), "Test revenue")
        ];

        Mock<IAccountingEntryRepository> mockRepo = new();
        _ = mockRepo
            .Setup(r => r.GetByTenantAndPeriodAsync(tenantId, period, It.IsAny<CancellationToken>()))
            .ReturnsAsync(entries);

        Mock<IReversalService> mockReversal = new();
        _ = mockReversal
            .Setup(r => r.CreateReversalEntryAsync(
                It.IsAny<AccountingEntryId>(), It.IsAny<TenantId>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(CoreAccountingEntry.CreateRevenue(tenantId, period, new Money(1_000_000m, "VND"), "Reversal"));

        Mock<IAuditTrailService> mockAudit = new();
        _ = mockAudit
            .Setup(a => a.LogPeriodCloseAsync(It.IsAny<AccountingPeriod>(), It.IsAny<string>(),
                It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(VanAn.Shared.Domain.Audit.AuditLog.ForCreate(
                tenantId, VanAn.Shared.Domain.Audit.AuditableEntityType.AccountingEntry,
                Guid.NewGuid(), "{}", "test-user"));
        _ = mockAudit
            .Setup(a => a.LogPeriodReopenAsync(It.IsAny<AccountingPeriod>(), It.IsAny<string>(),
                It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(VanAn.Shared.Domain.Audit.AuditLog.ForCreate(
                tenantId, VanAn.Shared.Domain.Audit.AuditableEntityType.AccountingEntry,
                Guid.NewGuid(), "{}", "test-user"));

        return new PeriodClosingService(
            mockRepo.Object,
            mockReversal.Object,
            mockAudit.Object,
            dbContext,
            NullLogger<PeriodClosingService>.Instance);
    }

    [Fact]
    public async Task ClosePeriod_PersistsClosedStatusToDatabase()
    {
        // Arrange
        TenantId tenantId = new(Guid.NewGuid());
        AccountingPeriod period = new(2026, 6);
        _fixture.SetCurrentTenant(tenantId);
        VanAnDbContext dbContext = _fixture.CreateFreshDbContext();

        PeriodClosingService service = CreateService(dbContext, tenantId, period);

        // Act
        ClosingEntry result = await service.ClosePeriodAsync(period, tenantId, Guid.NewGuid());

        // Assert — service returned a valid closing entry
        Assert.NotNull(result);
        Assert.Equal(period, result.Period);

        // Assert — DB record exists with Status=Closed
        PeriodClosingStatusEntity? dbRecord = await dbContext.PeriodClosingStatuses
            .FirstOrDefaultAsync(e => e.TenantId == tenantId && e.PeriodYear == 2026 && e.PeriodMonth == 6);
        Assert.NotNull(dbRecord);
        Assert.Equal(PeriodClosingStatus.Closed, dbRecord!.Status);
        Assert.NotNull(dbRecord.ClosedAt);
        Assert.NotNull(dbRecord.ClosedBy);

        await dbContext.DisposeAsync();
    }

    [Fact]
    public async Task GetPeriodStatus_SurvivesRestart_FreshDbContext_StillReturnsClosed()
    {
        // Arrange — close period with first context
        TenantId tenantId = new(Guid.NewGuid());
        AccountingPeriod period = new(2026, 7);
        _fixture.SetCurrentTenant(tenantId);

        VanAnDbContext context1 = _fixture.CreateFreshDbContext();
        PeriodClosingService service1 = CreateService(context1, tenantId, period);
        _ = await service1.ClosePeriodAsync(period, tenantId, Guid.NewGuid());
        await context1.DisposeAsync(); // simulate app restart (context disposed)

        // Act — query with a FRESH context (simulates app restart)
        VanAnDbContext context2 = _fixture.CreateFreshDbContext();
        PeriodClosingService service2 = CreateService(context2, tenantId, period);
        PeriodClosingStatus status = await service2.GetPeriodStatusAsync(period, tenantId);

        // Assert — status survived restart (persisted to DB, not in-memory)
        Assert.Equal(PeriodClosingStatus.Closed, status);

        await context2.DisposeAsync();
    }

    [Fact]
    public async Task ReopenPeriod_UpdatesStatusToOpen_AndPersistsReopenReason()
    {
        // Arrange — close first
        TenantId tenantId = new(Guid.NewGuid());
        AccountingPeriod period = new(2026, 8);
        _fixture.SetCurrentTenant(tenantId);

        VanAnDbContext dbContext = _fixture.CreateFreshDbContext();
        PeriodClosingService service = CreateService(dbContext, tenantId, period);
        _ = await service.ClosePeriodAsync(period, tenantId, Guid.NewGuid());

        // Act — reopen with a reason
        string reopenReason = "Correction needed for VAT adjustment";
        await service.ReopenPeriodAsync(period, tenantId, Guid.NewGuid(), reopenReason);

        // Assert — DB record shows Open + ReopenReason persisted
        PeriodClosingStatusEntity? dbRecord = await dbContext.PeriodClosingStatuses
            .FirstOrDefaultAsync(e => e.TenantId == tenantId && e.PeriodYear == 2026 && e.PeriodMonth == 8);
        Assert.NotNull(dbRecord);
        Assert.Equal(PeriodClosingStatus.Open, dbRecord!.Status);
        Assert.Equal(reopenReason, dbRecord.ReopenReason);

        // Assert — GetPeriodStatus confirms Open
        PeriodClosingStatus status = await service.GetPeriodStatusAsync(period, tenantId);
        Assert.Equal(PeriodClosingStatus.Open, status);

        await dbContext.DisposeAsync();
    }

    [Fact]
    public async Task MultiTenantIsolation_TenantA_Close_DoesNotAffectTenantB()
    {
        // Arrange — two tenants, same period
        TenantId tenantA = new(Guid.NewGuid());
        TenantId tenantB = new(Guid.NewGuid());
        AccountingPeriod period = new(2026, 9);

        // Act — tenant A closes the period
        _fixture.SetCurrentTenant(tenantA);
        VanAnDbContext contextA = _fixture.CreateFreshDbContext();
        PeriodClosingService serviceA = CreateService(contextA, tenantA, period);
        _ = await serviceA.ClosePeriodAsync(period, tenantA, Guid.NewGuid());
        await contextA.DisposeAsync();

        // Assert — tenant B's same period is still Open (no DB record)
        _fixture.SetCurrentTenant(tenantB);
        VanAnDbContext contextB = _fixture.CreateFreshDbContext();
        PeriodClosingService serviceB = CreateService(contextB, tenantB, period);
        PeriodClosingStatus tenantBStatus = await serviceB.GetPeriodStatusAsync(period, tenantB);
        Assert.Equal(PeriodClosingStatus.Open, tenantBStatus);

        // Assert — tenant B can close its own period (no conflict with tenant A)
        _ = await serviceB.ClosePeriodAsync(period, tenantB, Guid.NewGuid());
        PeriodClosingStatus tenantBStatusAfterClose = await serviceB.GetPeriodStatusAsync(period, tenantB);
        Assert.Equal(PeriodClosingStatus.Closed, tenantBStatusAfterClose);

        // Assert — tenant A is still Closed (tenant B's close didn't affect it)
        _fixture.SetCurrentTenant(tenantA);
        VanAnDbContext contextA2 = _fixture.CreateFreshDbContext();
        PeriodClosingService serviceA2 = CreateService(contextA2, tenantA, period);
        PeriodClosingStatus tenantAStatus = await serviceA2.GetPeriodStatusAsync(period, tenantA);
        Assert.Equal(PeriodClosingStatus.Closed, tenantAStatus);

        await contextB.DisposeAsync();
        await contextA2.DisposeAsync();
    }
}
