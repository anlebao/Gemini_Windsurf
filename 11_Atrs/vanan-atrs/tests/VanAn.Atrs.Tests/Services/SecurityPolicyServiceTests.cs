using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using VanAn.Atrs.Domain;
using VanAn.Atrs.Infra;
using Xunit;

namespace VanAn.Atrs.Tests.Services;

public sealed class SecurityPolicyServiceTests : IDisposable
{
    private readonly AtrsDbContext _db;
    private readonly SecurityPolicyService _service;

    public SecurityPolicyServiceTests()
    {
        var options = new DbContextOptionsBuilder<AtrsDbContext>()
            .UseInMemoryDatabase($"policy-{Guid.NewGuid()}")
            .Options;
        _db = new AtrsDbContext(options);
        _service = new SecurityPolicyService(_db, new MemoryCache(new MemoryCacheOptions()), TimeProvider.System);
    }

    public void Dispose()
    {
        _db.Dispose();
        GC.SuppressFinalize(this);
    }

    [Fact]
    public async Task Propose_FirstVersion_IsEffective()
    {
        var dto = await _service.ProposeAsync("recovery.sla_hours", SecurityPolicyGroups.Sla,
            "{\"hours\":24}", Guid.NewGuid(), "seed default");
        Assert.Equal(1, dto.Version);
        Assert.Equal(SecurityPolicyStatus.EFFECTIVE.ToString(), dto.Status);
        Assert.Equal(24, await _service.GetEffectiveNumberAsync("recovery.sla_hours", "hours", 999));
    }

    [Fact]
    public async Task Propose_SecondVersion_WaitsApproval()
    {
        var proposer = Guid.NewGuid();
        await _service.ProposeAsync("recovery.sla_hours", SecurityPolicyGroups.Sla, "{\"hours\":24}", proposer, "v1");
        var proposed = await _service.ProposeAsync("recovery.sla_hours", SecurityPolicyGroups.Sla, "{\"hours\":48}", proposer, "tăng SLA");
        Assert.Equal(2, proposed.Version);
        Assert.Equal(SecurityPolicyStatus.PROPOSED.ToString(), proposed.Status);
        // Effective vẫn v1 cho tới khi approve.
        Assert.Equal(24, await _service.GetEffectiveNumberAsync("recovery.sla_hours", "hours", 999));
    }

    [Fact]
    public async Task Propose_InvalidJson_Rejected()
    {
        await Assert.ThrowsAnyAsync<System.Text.Json.JsonException>(() =>
            _service.ProposeAsync("recovery.sla_hours", SecurityPolicyGroups.Sla, "not-json", Guid.NewGuid(), null));
    }

    [Fact]
    public async Task Propose_PendingExists_Throws()
    {
        var proposer = Guid.NewGuid();
        await _service.ProposeAsync("recovery.sla_hours", SecurityPolicyGroups.Sla, "{\"hours\":24}", proposer, "v1");
        await _service.ProposeAsync("recovery.sla_hours", SecurityPolicyGroups.Sla, "{\"hours\":48}", proposer, "proposal 1");
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _service.ProposeAsync("recovery.sla_hours", SecurityPolicyGroups.Sla, "{\"hours\":72}", proposer, "proposal 2"));
    }

    [Fact]
    public async Task Approve_SelfApproval_Rejected()
    {
        var proposer = Guid.NewGuid();
        await _service.ProposeAsync("recovery.sla_hours", SecurityPolicyGroups.Sla, "{\"hours\":24}", proposer, "v1");
        var proposed = await _service.ProposeAsync("recovery.sla_hours", SecurityPolicyGroups.Sla, "{\"hours\":48}", proposer, "v2");
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => _service.ApproveAsync(proposed.Id, proposer));
        Assert.Contains("cannot be the proposer", ex.Message);
    }

    [Fact]
    public async Task Approve_OtherStaff_SupersedesOldAndBecomesEffective()
    {
        var proposer = Guid.NewGuid();
        var approver = Guid.NewGuid();
        await _service.ProposeAsync("recovery.sla_hours", SecurityPolicyGroups.Sla, "{\"hours\":24}", proposer, "v1");
        var proposed = await _service.ProposeAsync("recovery.sla_hours", SecurityPolicyGroups.Sla, "{\"hours\":48}", proposer, "v2");

        var approved = await _service.ApproveAsync(proposed.Id, approver);
        Assert.Equal(SecurityPolicyStatus.EFFECTIVE.ToString(), approved.Status);
        Assert.Equal(approver, approved.ApprovedBy);
        Assert.Equal(48, await _service.GetEffectiveNumberAsync("recovery.sla_hours", "hours", 999));

        var rows = await _service.ListAsync();
        var v1 = rows.First(r => r.Version == 1);
        Assert.Equal(SecurityPolicyStatus.SUPERSEDED.ToString(), v1.Status);
    }

    [Fact]
    public async Task Reject_KeepsRowForAudit_AndEffectiveUnchanged()
    {
        var proposer = Guid.NewGuid();
        var approver = Guid.NewGuid();
        await _service.ProposeAsync("recovery.sla_hours", SecurityPolicyGroups.Sla, "{\"hours\":24}", proposer, "v1");
        var proposed = await _service.ProposeAsync("recovery.sla_hours", SecurityPolicyGroups.Sla, "{\"hours\":48}", proposer, "v2");

        var rejected = await _service.RejectAsync(proposed.Id, approver);
        Assert.Equal(SecurityPolicyStatus.REJECTED.ToString(), rejected.Status);
        Assert.NotNull(rejected.RejectedAt);
        Assert.Equal(24, await _service.GetEffectiveNumberAsync("recovery.sla_hours", "hours", 999));
    }

    [Fact]
    public async Task EffectiveNumber_BadJsonOrMissingField_FallsBack()
    {
        Assert.Equal(999, await _service.GetEffectiveNumberAsync("missing.key", "hours", 999));
        await _service.ProposeAsync("recovery.sla_hours", SecurityPolicyGroups.Sla, "{\"other\":1}", Guid.NewGuid(), "bad field");
        Assert.Equal(999, await _service.GetEffectiveNumberAsync("recovery.sla_hours", "hours", 999));
    }

    [Fact]
    public async Task EffectiveBool_Works()
    {
        var proposer = Guid.NewGuid();
        await _service.ProposeAsync("dispute.maker_checker_required", SecurityPolicyGroups.MakerChecker,
            "{\"required\":false}", proposer, "tắt maker-checker");
        Assert.False(await _service.GetEffectiveBoolAsync("dispute.maker_checker_required", "required", true));
        Assert.True(await _service.GetEffectiveBoolAsync("missing.key", "required", true));
    }
}