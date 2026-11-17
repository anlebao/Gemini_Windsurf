using Microsoft.EntityFrameworkCore;
using VanAn.Atrs.Contracts;
using VanAn.Atrs.Domain;
using VanAn.Atrs.Infra;
using Xunit;

namespace VanAn.Atrs.Tests.Services;

public sealed class AuditQueryServiceTests : IDisposable
{
    private readonly AtrsDbContext _db;
    private readonly AuditQueryService _service;
    private readonly Guid _subjectA = Guid.NewGuid();
    private readonly Guid _subjectB = Guid.NewGuid();

    public AuditQueryServiceTests()
    {
        var options = new DbContextOptionsBuilder<AtrsDbContext>()
            .UseInMemoryDatabase($"audit-{Guid.NewGuid()}")
            .Options;
        _db = new AtrsDbContext(options);
        _service = new AuditQueryService(_db);

        _db.AccountSecurityEvents.AddRange(
            AccountSecurityEvent.Create(Guid.NewGuid(), _subjectA, "recovery.approved", SecurityEventActorType.Staff,
                "staff-1", "vanan-atrs", null, null, SecurityEventOutcome.Success, null,
                minimalMetadataJson: "{\"requestId\":\"secret-internal-metadata\"}",
                occurredAt: DateTime.UtcNow.AddHours(-2)),
            AccountSecurityEvent.Create(Guid.NewGuid(), _subjectA, "login.failed", SecurityEventActorType.User,
                null, "khachlink", null, null, SecurityEventOutcome.Failure, "bad-password",
                minimalMetadataJson: "{\"secret\":\"hacker\"}",
                occurredAt: DateTime.UtcNow.AddHours(-1)),
            AccountSecurityEvent.Create(Guid.NewGuid(), _subjectB, "recovery.rejected", SecurityEventActorType.Staff,
                "staff-2", "vanan-atrs", null, null, SecurityEventOutcome.Blocked, "policy-deny",
                occurredAt: DateTime.UtcNow));
        _db.SaveChanges();
    }

    public void Dispose()
    {
        _db.Dispose();
        GC.SuppressFinalize(this);
    }

    [Fact]
    public async Task Query_NoFilters_ReturnsAllDesc()
    {
        var page = await _service.QueryAsync(new AuditQuery(Take: 50));
        Assert.Equal(3, page.Total);
        Assert.Equal(3, page.Items.Count);
        Assert.True(page.Items[0].OccurredAt >= page.Items[1].OccurredAt);
    }

    [Fact]
    public async Task Query_FilterEventTypeContains()
    {
        var page = await _service.QueryAsync(new AuditQuery(EventType: "recovery", Take: 50));
        Assert.Equal(2, page.Total);
        Assert.All(page.Items, i => Assert.Contains("recovery", i.EventType));
    }

    [Fact]
    public async Task Query_FilterOutcomeAndActorType()
    {
        var failed = await _service.QueryAsync(new AuditQuery(Outcome: "Failure", Take: 50));
        Assert.Single(failed.Items);
        Assert.Equal("login.failed", failed.Items[0].EventType);

        var staff = await _service.QueryAsync(new AuditQuery(ActorType: "Staff", Take: 50));
        Assert.Equal(2, staff.Total);
    }

    [Fact]
    public async Task Query_FilterSubjectAndDateRange()
    {
        var bySubject = await _service.QueryAsync(new AuditQuery(SubjectId: _subjectA, Take: 50));
        Assert.Equal(2, bySubject.Total);
        Assert.All(bySubject.Items, i => Assert.Equal(_subjectA, i.TrustSubjectId));

        var from = DateTime.UtcNow.AddHours(-3);
        var to = DateTime.UtcNow.AddHours(-1).AddSeconds(-1);
        var ranged = await _service.QueryAsync(new AuditQuery(From: from, To: to, Take: 50));
        Assert.Single(ranged.Items);
        Assert.Equal("recovery.approved", ranged.Items[0].EventType);
    }

    [Fact]
    public async Task Query_PagingReturnsTotal()
    {
        var page1 = await _service.QueryAsync(new AuditQuery(Skip: 0, Take: 2));
        var page2 = await _service.QueryAsync(new AuditQuery(Skip: 2, Take: 2));
        Assert.Equal(2, page1.Items.Count);
        Assert.Equal(1, page2.Items.Count);
        Assert.Equal(3, page1.Total);
        Assert.Equal(3, page2.Total);
    }

    [Fact]
    public async Task Query_ResponseNeverExposesPayloadJson()
    {
        var page = await _service.QueryAsync(new AuditQuery(Take: 50));
        foreach (var item in page.Items)
        {
            var json = System.Text.Json.JsonSerializer.Serialize(item);
            Assert.DoesNotContain("secret-internal-metadata", json);
            Assert.DoesNotContain("hacker", json);
            Assert.DoesNotContain("MinimalMetadataJson", json);
            Assert.DoesNotContain("EvidenceReferencesJson", json);
        }
    }
}