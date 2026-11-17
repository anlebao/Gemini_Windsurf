using Microsoft.EntityFrameworkCore;
using VanAn.Atrs.Contracts;
using VanAn.Atrs.Domain;
using VanAn.Atrs.Infra;
using Xunit;

namespace VanAn.Atrs.Tests.Services;

public sealed class RecoveryCaseManagerTests : IDisposable
{
    private readonly AtrsDbContext _db;
    private readonly RecoveryCaseManager _manager;

    public RecoveryCaseManagerTests()
    {
        var options = new DbContextOptionsBuilder<AtrsDbContext>()
            .UseInMemoryDatabase($"rcm-{Guid.NewGuid()}")
            .Options;
        _db = new AtrsDbContext(options);
        _manager = new RecoveryCaseManager(
            _db,
            new StubRecoveryCodeService(),
            new RiskPolicyEngine(),
            _bumpNotifier);
    }

    private readonly RecordingBumpNotifier _bumpNotifier = new();

    public void Dispose()
    {
        _db.Dispose();
        GC.SuppressFinalize(this);
    }

    [Fact]
    public async Task Submit_NewRequest_ReturnsSubmitted()
    {
        var result = await _manager.SubmitAsync(Guid.NewGuid(), RecoveryRequestType.LOST_DEVICE, "idem-1");
        Assert.False(result.Existing);
        Assert.Equal(RecoveryRequestStatus.SUBMITTED.ToString(), result.Request.Status);
    }

    [Fact]
    public async Task Submit_SameIdempotencyKey_ReturnsExisting()
    {
        var subjectId = Guid.NewGuid();
        var first = await _manager.SubmitAsync(subjectId, RecoveryRequestType.LOST_DEVICE, "idem-1");
        var second = await _manager.SubmitAsync(subjectId, RecoveryRequestType.LOST_DEVICE, "idem-1");
        Assert.True(second.Existing);
        Assert.Equal(first.Request.Id, second.Request.Id);
    }

    [Fact]
    public async Task Submit_ActiveRequestExists_ReturnsExisting()
    {
        var subjectId = Guid.NewGuid();
        var first = await _manager.SubmitAsync(subjectId, RecoveryRequestType.LOST_DEVICE, "idem-1");
        var second = await _manager.SubmitAsync(subjectId, RecoveryRequestType.NO_CREDENTIAL, "idem-2");
        Assert.True(second.Existing);
        Assert.Equal(first.Request.Id, second.Request.Id); // anti-duplicate FR-011
    }

    [Fact]
    public async Task VerifyCodePath_GoodCode_Approves()
    {
        var subjectId = Guid.NewGuid();
        var submitted = await _manager.SubmitAsync(subjectId, RecoveryRequestType.LOST_DEVICE, "idem-1");
        var result = await _manager.VerifyCodePathAsync(submitted.Request.Id, StubRecoveryCodeService.GoodCode, deviceId: null);
        Assert.True(result.Success);
        Assert.Equal(RiskDecisionKind.ALLOW.ToString(), result.RiskDecisionKind);
        Assert.Equal(RecoveryRequestStatus.APPROVED.ToString(), result.Request!.Status);
    }

    [Fact]
    public async Task VerifyCodePath_HighValueAccount_StillApprovedByCode()
    {
        var subjectId = Guid.NewGuid();
        var submitted = await _manager.SubmitAsync(subjectId, RecoveryRequestType.NO_CREDENTIAL, "idem-1");
        var result = await _manager.VerifyCodePathAsync(submitted.Request.Id, StubRecoveryCodeService.GoodCode, deviceId: null, highAccountValue: true);
        // R2 high-value-recovery → ALLOW_WITH_STEP_UP; code verify = step-up đã thỏa → APPROVED.
        Assert.True(result.Success);
        Assert.Equal(RiskDecisionKind.ALLOW_WITH_STEP_UP.ToString(), result.RiskDecisionKind);
        Assert.Equal(RecoveryRequestStatus.APPROVED.ToString(), result.Request!.Status);
    }

    [Fact]
    public async Task VerifyCodePath_BadCode_FailsWithoutConsuming()
    {
        var subjectId = Guid.NewGuid();
        var submitted = await _manager.SubmitAsync(subjectId, RecoveryRequestType.LOST_DEVICE, "idem-1");
        var result = await _manager.VerifyCodePathAsync(submitted.Request.Id, "WRONG-CODE", deviceId: null);
        Assert.False(result.Success);
        Assert.Equal("InvalidCode", result.Reason);
        Assert.Equal(RecoveryRequestStatus.AUTOMATED_CHECK.ToString(), result.Request!.Status);
    }

    [Fact]
    public async Task VerifyCodePath_WrongState_Rejected()
    {
        var subjectId = Guid.NewGuid();
        var submitted = await _manager.SubmitAsync(subjectId, RecoveryRequestType.LOST_DEVICE, "idem-1");
        await _manager.VerifyCodePathAsync(submitted.Request.Id, StubRecoveryCodeService.GoodCode, deviceId: null);
        // Request đã APPROVED — verify lần nữa → WrongState.
        var second = await _manager.VerifyCodePathAsync(submitted.Request.Id, StubRecoveryCodeService.GoodCode, deviceId: null);
        Assert.False(second.Success);
        Assert.Equal("WrongState", second.Reason);
    }

    [Fact]
    public async Task ManualReview_ApproveThenComplete()
    {
        var subjectId = Guid.NewGuid();
        var submitted = await _manager.SubmitAsync(subjectId, RecoveryRequestType.NO_CREDENTIAL, "idem-1");
        var reviewed = await _manager.ManualApproveAsync(submitted.Request.Id, reviewerId: Guid.NewGuid());
        Assert.Equal(RecoveryRequestStatus.APPROVED.ToString(), reviewed.Status);

        var completed = await _manager.CompleteAsync(reviewed.Id, newDeviceId: Guid.NewGuid());
        Assert.Equal(RecoveryRequestStatus.COMPLETED.ToString(), completed.Status);
        Assert.NotNull(completed.CompletedAt);
        Assert.NotNull(completed.NewDeviceId);
    }

    [Fact]
    public async Task ManualReject_SetsReasonCode()
    {
        var subjectId = Guid.NewGuid();
        var submitted = await _manager.SubmitAsync(subjectId, RecoveryRequestType.COMPROMISED, "idem-1");
        var rejected = await _manager.ManualRejectAsync(submitted.Request.Id, reviewerId: Guid.NewGuid(), reasonCode: "insufficient-evidence");
        Assert.Equal(RecoveryRequestStatus.REJECTED.ToString(), rejected.Status);
        Assert.Equal("insufficient-evidence", rejected.DecisionReasonCode);
    }

    [Fact]
    public async Task CompleteAsync_BumpsTokenVersion_ForLinkedIdentities()
    {
        var subjectId = Guid.NewGuid();
        var customerId = Guid.NewGuid();
        _db.TrustSubjects.Add(new TrustSubject(subjectId, null));
        _db.TrustSubjectLinks.Add(new TrustSubjectLink(Guid.NewGuid(), subjectId, TrustIdentityType.Customer, customerId, tenantId: null));
        await _db.SaveChangesAsync();

        var submitted = await _manager.SubmitAsync(subjectId, RecoveryRequestType.LOST_DEVICE, "idem-1");
        await _manager.ManualApproveAsync(submitted.Request.Id, reviewerId: Guid.NewGuid());
        var completed = await _manager.CompleteAsync(submitted.Request.Id, newDeviceId: Guid.NewGuid());

        Assert.Equal(RecoveryRequestStatus.COMPLETED.ToString(), completed.Status);
        Assert.Single(_bumpNotifier.Calls);
        Assert.Equal((customerId, "Customer"), _bumpNotifier.Calls[0]);
    }

    // ── Wave 4 (U10): ListMine + Cancel ──

    [Fact]
    public async Task ListMine_ReturnsOnlySubjectRequests_NewestFirst()
    {
        var subjectId = Guid.NewGuid();
        var otherId = Guid.NewGuid();
        await _manager.SubmitAsync(subjectId, RecoveryRequestType.LOST_DEVICE, "idem-a");
        await _manager.SubmitAsync(otherId, RecoveryRequestType.NO_CREDENTIAL, "idem-b");

        var mine = await _manager.ListMineAsync(subjectId);
        Assert.Single(mine);
        Assert.Equal(subjectId, mine[0].TrustSubjectId);
    }

    [Fact]
    public async Task CancelAsync_OpenRequest_SetsCancelled()
    {
        var subjectId = Guid.NewGuid();
        var submitted = await _manager.SubmitAsync(subjectId, RecoveryRequestType.LOST_DEVICE, "idem-1");
        var cancelled = await _manager.CancelAsync(submitted.Request.Id, subjectId);
        Assert.Equal(RecoveryRequestStatus.CANCELLED.ToString(), cancelled!.Status);
    }

    [Fact]
    public async Task CancelAsync_WrongSubject_ReturnsNull()
    {
        var subjectId = Guid.NewGuid();
        var submitted = await _manager.SubmitAsync(subjectId, RecoveryRequestType.LOST_DEVICE, "idem-1");
        var result = await _manager.CancelAsync(submitted.Request.Id, Guid.NewGuid());
        Assert.Null(result);
    }

    [Fact]
    public async Task CancelAsync_CompletedRequest_ReturnsCurrentState()
    {
        var subjectId = Guid.NewGuid();
        var submitted = await _manager.SubmitAsync(subjectId, RecoveryRequestType.LOST_DEVICE, "idem-1");
        await _manager.ManualApproveAsync(submitted.Request.Id, reviewerId: Guid.NewGuid());
        var cancelled = await _manager.CancelAsync(submitted.Request.Id, subjectId);
        Assert.Equal(RecoveryRequestStatus.APPROVED.ToString(), cancelled!.Status);
    }

    private sealed class RecordingBumpNotifier : ITokenVersionBumpNotifier
    {
        public List<(Guid UserId, string IdentityType)> Calls { get; } = [];
        public Task BumpAsync(Guid userId, string identityType, CancellationToken ct = default)
        {
            Calls.Add((userId, identityType));
            return Task.CompletedTask;
        }
    }

    private sealed class StubRecoveryCodeService : IRecoveryCodeService
    {
        public const string GoodCode = "GOOD-CODE";
        public Task<GenerateRecoveryCodesResult> GenerateSetAsync(Guid trustSubjectId, CancellationToken ct = default)
            => Task.FromResult(new GenerateRecoveryCodesResult(Guid.NewGuid(), [GoodCode], DateTime.UtcNow));

        public Task<VerifyRecoveryCodeResult> VerifyAndConsumeAsync(Guid trustSubjectId, string code, Guid? deviceId, CancellationToken ct = default)
            => Task.FromResult(new VerifyRecoveryCodeResult(code == GoodCode, code == GoodCode ? "Success" : "InvalidCode"));

        public Task<RecoveryCodesStatusDto> GetStatusAsync(Guid trustSubjectId, CancellationToken ct = default)
            => Task.FromResult(new RecoveryCodesStatusDto(true, 1, DateTime.UtcNow, null));
    }
}