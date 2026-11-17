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

    // ── Wave 5 (A14/A15/A16): staff ops — assign / note / escalate / queue page / detail / dashboard ──

    [Fact]
    public async Task Assign_SetsAssignedReviewer()
    {
        var subjectId = Guid.NewGuid();
        var staffId = Guid.NewGuid();
        var submitted = await _manager.SubmitAsync(subjectId, RecoveryRequestType.LOST_DEVICE, "idem-a1");
        var item = await _manager.AssignAsync(submitted.Request.Id, staffId);
        Assert.NotNull(item);
        Assert.Equal(staffId, item.AssignedReviewerId);
        Assert.NotNull(item.AssignedAt);
    }

    [Fact]
    public async Task Assign_TerminalRequest_ReturnsNull()
    {
        var subjectId = Guid.NewGuid();
        var submitted = await _manager.SubmitAsync(subjectId, RecoveryRequestType.LOST_DEVICE, "idem-a2");
        await _manager.ManualApproveAsync(submitted.Request.Id, reviewerId: Guid.NewGuid());
        var item = await _manager.AssignAsync(submitted.Request.Id, Guid.NewGuid());
        Assert.Null(item);
    }

    [Fact]
    public async Task AddNote_StoresNoteAndAudits()
    {
        var subjectId = Guid.NewGuid();
        var staffId = Guid.NewGuid();
        var submitted = await _manager.SubmitAsync(subjectId, RecoveryRequestType.LOST_DEVICE, "idem-n1");
        var note = await _manager.AddNoteAsync(submitted.Request.Id, staffId, "Kiểm tra giấy tờ tuỳ thân", RecoveryNoteType.RECOMMEND_APPROVE);

        Assert.Equal(staffId, note.AuthorId);
        Assert.Equal(RecoveryNoteType.RECOMMEND_APPROVE.ToString(), note.NoteType);
        Assert.True(await _db.AccountSecurityEvents.AnyAsync(e =>
            e.EventType == "recovery.note_added" && e.ActorId == staffId.ToString() && e.TrustSubjectId == subjectId));
    }

    [Fact]
    public async Task Escalate_ThenReturnToReview()
    {
        var subjectId = Guid.NewGuid();
        var submitted = await _manager.SubmitAsync(subjectId, RecoveryRequestType.COMPROMISED, "idem-e1");
        // Bad code → AUTOMATED_CHECK → request-info → PENDING_USER_ACTION → escalate hợp lệ.
        await _manager.VerifyCodePathAsync(submitted.Request.Id, "WRONG-CODE", deviceId: null);
        await _manager.RequestMoreInfoAsync(submitted.Request.Id, Guid.NewGuid());

        var escalated = await _manager.EscalateAsync(submitted.Request.Id, Guid.NewGuid());
        Assert.Equal(RecoveryRequestStatus.ESCALATED.ToString(), escalated.Status);

        var returned = await _manager.ReturnToReviewAsync(submitted.Request.Id, Guid.NewGuid());
        Assert.Equal(RecoveryRequestStatus.PENDING_REVIEW.ToString(), returned.Status);
    }

    [Fact]
    public async Task Escalate_FromSubmitted_Throws()
    {
        var subjectId = Guid.NewGuid();
        var submitted = await _manager.SubmitAsync(subjectId, RecoveryRequestType.LOST_DEVICE, "idem-e2");
        await Assert.ThrowsAsync<InvalidOperationException>(() => _manager.EscalateAsync(submitted.Request.Id, Guid.NewGuid()));
    }

    [Fact]
    public async Task QueuePage_ReturnsTotalAndSlaFlag()
    {
        var subjectId = Guid.NewGuid();
        await _manager.SubmitAsync(subjectId, RecoveryRequestType.LOST_DEVICE, "idem-q1");

        var page = await _manager.ListQueuePageAsync(new RecoveryQueueQuery(Take: 50));
        Assert.Single(page.Items);
        Assert.Equal(1, page.Total);
        // Request vừa tạo → age < 24h → không SLA breach.
        Assert.False(page.Items[0].SlaBreached);
        Assert.True(page.Items[0].AgeHours >= 0);
    }

    [Fact]
    public async Task QueuePage_FiltersByStatusAndSubject()
    {
        var subjectA = Guid.NewGuid();
        var subjectB = Guid.NewGuid();
        await _manager.SubmitAsync(subjectA, RecoveryRequestType.LOST_DEVICE, "idem-q2");
        await _manager.SubmitAsync(subjectB, RecoveryRequestType.NO_CREDENTIAL, "idem-q3");

        var bySubject = await _manager.ListQueuePageAsync(new RecoveryQueueQuery(SubjectId: subjectA, Take: 50));
        Assert.Single(bySubject.Items);
        Assert.Equal(subjectA, bySubject.Items[0].TrustSubjectId);

        var byType = await _manager.ListQueuePageAsync(new RecoveryQueueQuery(RequestType: "NO_CREDENTIAL", Take: 50));
        Assert.Single(byType.Items);
        Assert.Equal(subjectB, byType.Items[0].TrustSubjectId);
    }

    [Fact]
    public async Task StaffDetail_IncludesNotesDevicesLinksAndAuditsView()
    {
        var subjectId = Guid.NewGuid();
        var staffId = Guid.NewGuid();
        _db.TrustSubjects.Add(new TrustSubject(subjectId, null));
        _db.TrustSubjectLinks.Add(new TrustSubjectLink(Guid.NewGuid(), subjectId, TrustIdentityType.Customer, Guid.NewGuid(), tenantId: Guid.NewGuid()));
        _db.UserDevices.Add(new UserDevice(Guid.NewGuid(), subjectId, "dev-1", RegistrationMethod.INSTALLATION_ID, friendlyName: "iPhone của khách"));
        await _db.SaveChangesAsync();

        var submitted = await _manager.SubmitAsync(subjectId, RecoveryRequestType.LOST_DEVICE, "idem-d1");
        await _manager.AddNoteAsync(submitted.Request.Id, staffId, "note nội bộ", RecoveryNoteType.INTERNAL_NOTE);
        await _manager.VerifyCodePathAsync(submitted.Request.Id, StubRecoveryCodeService.GoodCode, deviceId: null);

        var detail = await _manager.GetStaffDetailAsync(submitted.Request.Id, staffId);
        Assert.NotNull(detail);
        Assert.Single(detail.Notes);
        Assert.Single(detail.SubjectLinks);
        Assert.Single(detail.Devices);
        Assert.Equal("iPhone của khách", detail.Devices[0].FriendlyName);
        Assert.NotNull(detail.Risk);
        Assert.Equal(RiskPolicyEngine.PolicyVersion, detail.Risk.PolicyVersion);
        Assert.Contains("routine", detail.Risk.ReasonCodes);
        // Audit staff_view đã ghi.
        Assert.True(await _db.AccountSecurityEvents.AnyAsync(e =>
            e.EventType == "recovery.staff_view" && e.ActorId == staffId.ToString()));
    }

    [Fact]
    public async Task StaffDetail_DoesNotExposeSecretsOrEvidencePayloads()
    {
        var subjectId = Guid.NewGuid();
        var submitted = await _manager.SubmitAsync(subjectId, RecoveryRequestType.LOST_DEVICE, "idem-d2",
            evidenceSummaryJson: "{\"secretAnswer\":\"hacker-secret\",\"recoveryCode\":\"XXXXX-XXXXX\"}");

        var detail = await _manager.GetStaffDetailAsync(submitted.Request.Id, Guid.NewGuid());
        Assert.NotNull(detail);
        // Evidence payload KHÔNG được expose qua staff DTO (compile-time guarantee).
        Assert.Null(typeof(RecoveryStaffDetailDto).GetProperty("EvidenceSummaryJson"));
        // RecentEvents sanitized — chỉ field an toàn, không payload JSON.
        foreach (var evt in detail.RecentEvents)
        {
            Assert.NotNull(evt.EventType);
            Assert.NotNull(evt.Outcome);
        }
    }

    [Fact]
    public async Task StaffActions_WriteAuditEvents()
    {
        var subjectId = Guid.NewGuid();
        var staffId = Guid.NewGuid();
        var submitted = await _manager.SubmitAsync(subjectId, RecoveryRequestType.LOST_DEVICE, "idem-aud1");
        await _manager.ManualApproveAsync(submitted.Request.Id, staffId);

        Assert.True(await _db.AccountSecurityEvents.AnyAsync(e => e.EventType == "recovery.approved" && e.ActorId == staffId.ToString()));

        var submitted2 = await _manager.SubmitAsync(subjectId, RecoveryRequestType.NO_CREDENTIAL, "idem-aud2");
        await _manager.ManualRejectAsync(submitted2.Request.Id, staffId, "insufficient-evidence");
        Assert.True(await _db.AccountSecurityEvents.AnyAsync(e => e.EventType == "recovery.rejected"));

        var submitted3 = await _manager.SubmitAsync(subjectId, RecoveryRequestType.COMPROMISED, "idem-aud3");
        await _manager.VerifyCodePathAsync(submitted3.Request.Id, "WRONG-CODE", deviceId: null); // → AUTOMATED_CHECK
        await _manager.RequestMoreInfoAsync(submitted3.Request.Id, staffId);
        Assert.True(await _db.AccountSecurityEvents.AnyAsync(e => e.EventType == "recovery.info_requested"));
    }

    [Fact]
    public async Task Dashboard_CountsRealData()
    {
        var subjectId = Guid.NewGuid();
        await _manager.SubmitAsync(subjectId, RecoveryRequestType.LOST_DEVICE, "idem-dash1"); // SUBMITTED → new24h + awaiting? (SUBMITTED không tính awaiting)

        var dash = await _manager.GetDashboardAsync();
        Assert.True(dash.NewRecovery24h >= 1);
        Assert.Equal(0, dash.AwaitingReview); // chưa có PENDING_REVIEW
        Assert.True(dash.SlaOverdue == 0);
        Assert.True(dash.RecentSecurityIncidents24h >= 0);
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