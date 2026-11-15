using VanAn.Atrs.Domain;
using Xunit;

namespace VanAn.Atrs.Tests.Domain;

public class TrustSubjectTests
{
    [Fact]
    public void Create_WithValidId_InitializesPendingState()
    {
        var id = Guid.NewGuid();
        var subject = new TrustSubject(id, "hash");

        Assert.Equal(id, subject.Id);
        Assert.Equal("hash", subject.PrimaryContactHash);
        Assert.True(subject.CreatedAt <= DateTime.UtcNow);
    }

    [Fact]
    public void Create_EmptyId_Throws()
    {
        Assert.Throws<ArgumentException>(() => new TrustSubject(Guid.Empty, null));
    }

    [Fact]
    public void SetPrimaryContactHash_UpdatesTimestamp()
    {
        var subject = new TrustSubject(Guid.NewGuid(), null);
        subject.SetPrimaryContactHash("new-hash");
        Assert.Equal("new-hash", subject.PrimaryContactHash);
        Assert.NotNull(subject.UpdatedAt);
    }

    [Fact]
    public void SetPrimaryContactHash_Empty_Throws()
    {
        var subject = new TrustSubject(Guid.NewGuid(), null);
        Assert.Throws<ArgumentException>(() => subject.SetPrimaryContactHash("  "));
    }
}

public class AccountTrustProfileTests
{
    [Fact]
    public void Create_StartsPendingSetupBasic()
    {
        var profile = new AccountTrustProfile(Guid.NewGuid(), Guid.NewGuid());
        Assert.Equal(TrustProfileStatus.PENDING_SETUP, profile.Status);
        Assert.Equal(AssuranceState.BASIC, profile.AssuranceState);
    }

    [Fact]
    public void ElevateAssurance_OnlyIncreases()
    {
        var profile = new AccountTrustProfile(Guid.NewGuid(), Guid.NewGuid());
        profile.ElevateAssurance(AssuranceState.RECOVERY_READY);
        Assert.Equal(AssuranceState.RECOVERY_READY, profile.AssuranceState);

        // Downgrade attempt = no-op (không tự downgrade)
        profile.ElevateAssurance(AssuranceState.BASIC);
        Assert.Equal(AssuranceState.RECOVERY_READY, profile.AssuranceState);
    }

    [Fact]
    public void SetStatus_UpdatesTimestamp()
    {
        var profile = new AccountTrustProfile(Guid.NewGuid(), Guid.NewGuid());
        profile.SetStatus(TrustProfileStatus.ACTIVE);
        Assert.Equal(TrustProfileStatus.ACTIVE, profile.Status);
        Assert.NotNull(profile.UpdatedAt);
    }
}

public class AccountSecurityEventTests
{
    [Fact]
    public void Create_SetsAllFields()
    {
        var evt = AccountSecurityEvent.Create(
            id: Guid.NewGuid(),
            trustSubjectId: Guid.NewGuid(),
            eventType: "device.registered",
            actorType: SecurityEventActorType.User,
            actorId: "actor-1",
            sourceApplication: "KhachLink",
            deviceId: Guid.NewGuid(),
            sessionCorrelationId: "corr-1",
            outcome: SecurityEventOutcome.Success,
            reasonCode: "OK");

        Assert.Equal("device.registered", evt.EventType);
        Assert.Equal(SecurityEventActorType.User, evt.ActorType);
        Assert.Equal(1, evt.SchemaVersion);
        Assert.True(evt.ReceivedAt >= evt.OccurredAt);
    }

    [Fact]
    public void Create_InvalidArgs_Throw()
    {
        Assert.Throws<ArgumentException>(() => AccountSecurityEvent.Create(
            Guid.Empty, Guid.NewGuid(), "x", SecurityEventActorType.System, null, null, null, null,
            SecurityEventOutcome.Success, null));

        Assert.Throws<ArgumentException>(() => AccountSecurityEvent.Create(
            Guid.NewGuid(), Guid.Empty, "x", SecurityEventActorType.System, null, null, null, null,
            SecurityEventOutcome.Success, null));

        Assert.Throws<ArgumentException>(() => AccountSecurityEvent.Create(
            Guid.NewGuid(), Guid.NewGuid(), "", SecurityEventActorType.System, null, null, null, null,
            SecurityEventOutcome.Success, null, schemaVersion: 0));
    }
}

public class ConsentRecordTests
{
    [Fact]
    public void Create_ThenWithdraw_IsIdempotent()
    {
        var consent = new ConsentRecord(Guid.NewGuid(), Guid.NewGuid(), "recovery-evidence", "2026-11-15");
        Assert.Null(consent.WithdrawnAt);

        consent.Withdraw();
        var first = consent.WithdrawnAt;
        Assert.NotNull(first);

        consent.Withdraw(); // idempotent — không đổi timestamp
        Assert.Equal(first, consent.WithdrawnAt);
    }
}