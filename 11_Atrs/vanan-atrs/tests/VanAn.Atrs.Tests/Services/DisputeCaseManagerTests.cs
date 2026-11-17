using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using VanAn.Atrs.Contracts;
using VanAn.Atrs.Domain;
using VanAn.Atrs.Infra;
using Xunit;

namespace VanAn.Atrs.Tests.Services;

/// <summary>
/// ATRS-9/ATRS-10 (Wave 3): dispute lifecycle, maker-checker, idempotent adjust,
/// evidence access classification, user-safe projection, staff permission matrix.
/// </summary>
public sealed class DisputeCaseManagerTests : IDisposable
{
    private readonly AtrsDbContext _db;
    private readonly StubLoyaltyGatewayClient _gateway = new();
    private readonly DisputeCaseManager _manager;

    public DisputeCaseManagerTests()
    {
        var options = new DbContextOptionsBuilder<AtrsDbContext>()
            .UseInMemoryDatabase($"dispute-{Guid.NewGuid()}")
            .Options;
        _db = new AtrsDbContext(options);
        _manager = new DisputeCaseManager(_db, _gateway,
            new SecurityPolicyService(_db, new Microsoft.Extensions.Caching.Memory.MemoryCache(new Microsoft.Extensions.Caching.Memory.MemoryCacheOptions()), TimeProvider.System),
            new StubNotificationWriter(), new AtrsMetrics(),
            NullLogger<DisputeCaseManager>.Instance);
    }

    public void Dispose()
    {
        _db.Dispose();
        GC.SuppressFinalize(this);
    }

    private async Task<(Guid SubjectId, Guid CustomerId, Guid TenantId)> SeedSubjectAsync()
    {
        var subjectId = Guid.NewGuid();
        var customerId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();
        _db.TrustSubjectLinks.Add(new TrustSubjectLink(Guid.NewGuid(), subjectId, TrustIdentityType.Customer, customerId, tenantId));
        await _db.SaveChangesAsync();
        return (subjectId, customerId, tenantId);
    }

    private static SubmitDisputeRequest NewRequest(Guid? tenantId = null, string? txRef = null, string? idem = null)
        => new("POINTS_MISSING", "Tôi bị trừ điểm nhưng không nhận được gì.", tenantId,
            txRef is null ? null : [txRef], null, idem);

    private async Task<DisputeCase> GetCaseAsync(Guid caseId)
        => await _db.DisputeCases.FirstAsync(c => c.Id == caseId);

    // ── Submit / list / duplicate ──

    [Fact]
    public async Task Submit_CreatesOpenCase()
    {
        var (subjectId, _, tenantId) = await SeedSubjectAsync();
        var result = await _manager.SubmitAsync(subjectId, NewRequest(tenantId));
        Assert.False(result.Existing);
        Assert.Equal("OPEN", result.Case.Status);
    }

    [Fact]
    public async Task Submit_SameIdempotencyKey_ReturnsExisting()
    {
        var (subjectId, _, tenantId) = await SeedSubjectAsync();
        var first = await _manager.SubmitAsync(subjectId, NewRequest(tenantId, idem: "k1"));
        var second = await _manager.SubmitAsync(subjectId, NewRequest(tenantId, idem: "k1"));
        Assert.True(second.Existing);
        Assert.Equal(first.Case.Id, second.Case.Id);
    }

    [Fact]
    public async Task Submit_DuplicateActiveTransactionRef_ReturnsExisting()
    {
        var (subjectId, _, tenantId) = await SeedSubjectAsync();
        var first = await _manager.SubmitAsync(subjectId, NewRequest(tenantId, txRef: "order-123"));
        var second = await _manager.SubmitAsync(subjectId, NewRequest(tenantId, txRef: "order-123", idem: "other-key"));
        Assert.True(second.Existing);
        Assert.Equal(first.Case.Id, second.Case.Id);
    }

    [Fact]
    public async Task Submit_InvalidCaseType_Throws()
    {
        var (subjectId, _, _) = await SeedSubjectAsync();
        await Assert.ThrowsAsync<ArgumentException>(() =>
            _manager.SubmitAsync(subjectId, new SubmitDisputeRequest("NOT_A_TYPE", "issue", null, null, null, null)));
    }

    // ── Evidence + access classification ──

    [Fact]
    public async Task AddEvidence_UserSubmit_AlwaysUserVisible_EvenIfRestrictedRequested()
    {
        var (subjectId, _, tenantId) = await SeedSubjectAsync();
        var submitted = await _manager.SubmitAsync(subjectId, NewRequest(tenantId));
        var detail = await _manager.AddEvidenceAsync(submitted.Case.Id, subjectId,
            new AddDisputeEvidenceRequest("SCREENSHOT", "khachlink", "ref-1",
                new string('A', 64), "ảnh chụp", null, "RESTRICTED"));
        Assert.NotNull(detail);
        var item = Assert.Single(detail!.Evidence);
        // User DTO không lộ AccessClassification; staff view phải thấy USER_VISIBLE.
        var staff = await _manager.GetStaffDetailAsync(submitted.Case.Id);
        Assert.Equal("USER_VISIBLE", staff!.Evidence.Single().AccessClassification);
    }

    [Fact]
    public async Task GetMine_OtherUsersCase_ReturnsNull()
    {
        var (subjectId, _, tenantId) = await SeedSubjectAsync();
        var submitted = await _manager.SubmitAsync(subjectId, NewRequest(tenantId));
        var other = await _manager.GetMineAsync(submitted.Case.Id, Guid.NewGuid());
        Assert.Null(other);
    }

    [Fact]
    public async Task Withdraw_OpenCase_Closes_AndBlocksFurtherEvidence()
    {
        var (subjectId, _, tenantId) = await SeedSubjectAsync();
        var submitted = await _manager.SubmitAsync(subjectId, NewRequest(tenantId));
        Assert.True(await _manager.WithdrawAsync(submitted.Case.Id, subjectId));
        Assert.Equal("CLOSED", (await GetCaseAsync(submitted.Case.Id)).Status.ToString());
        var detail = await _manager.AddEvidenceAsync(submitted.Case.Id, subjectId,
            new AddDisputeEvidenceRequest("OTHER", "khachlink", null, null, null, null, null));
        Assert.Null(detail); // terminal → không thêm evidence
    }

    // ── Lifecycle transitions ──

    [Fact]
    public async Task Transition_InvalidJump_Throws()
    {
        var (subjectId, _, tenantId) = await SeedSubjectAsync();
        var submitted = await _manager.SubmitAsync(subjectId, NewRequest(tenantId));
        // OPEN → UNDER_REVIEW không hợp lệ (phải qua TRIAGE).
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _manager.TransitionAsync(submitted.Case.Id, Guid.NewGuid(), DisputeCaseStatus.UNDER_REVIEW, null));
    }

    [Fact]
    public async Task Assign_OpenCase_MovesToTriage()
    {
        var (subjectId, _, tenantId) = await SeedSubjectAsync();
        var staffId = Guid.NewGuid();
        var submitted = await _manager.SubmitAsync(subjectId, NewRequest(tenantId));
        var item = await _manager.AssignAsync(submitted.Case.Id, staffId);
        Assert.NotNull(item);
        Assert.Equal("TRIAGE", item!.Status);
        Assert.Equal(staffId, item.AssignedReviewerId);
    }

    [Fact]
    public async Task StaffDetail_RecordsSecurityEvent()
    {
        var (subjectId, _, tenantId) = await SeedSubjectAsync();
        var staffId = Guid.NewGuid();
        var submitted = await _manager.SubmitAsync(subjectId, NewRequest(tenantId));
        await _manager.GetStaffDetailAsync(submitted.Case.Id, staffId);
        var evt = await _db.AccountSecurityEvents
            .Where(e => e.EventType == "dispute.staff_view")
            .ToListAsync();
        Assert.Single(evt);
        Assert.Equal(SecurityEventActorType.Staff, evt[0].ActorType);
        Assert.Equal(staffId.ToString(), evt[0].ActorId);
    }

    // ── Maker-checker + adjustment ──

    private async Task<(Guid CaseId, Guid SubjectId, Guid TenantId)> CaseReadyForReviewAsync(int? adjustPoints = null)
    {
        var (subjectId, _, tenantId) = await SeedSubjectAsync();
        var submitted = await _manager.SubmitAsync(subjectId, NewRequest(tenantId));
        await _manager.AssignAsync(submitted.Case.Id, Guid.NewGuid());
        await _manager.TransitionAsync(submitted.Case.Id, Guid.NewGuid(), DisputeCaseStatus.UNDER_REVIEW, null);
        return (submitted.Case.Id, subjectId, tenantId);
    }

    private static ProposeDisputeDecisionRequest ProposeAdjust(int points = 100)
        => new("PARTIALLY_APPROVED", "Sai sót hệ thống", "Chúng tôi đã ghi nhận.", "internal note",
            "atrs-dispute-v1", points, null, "Điều chỉnh sai lệch");

    [Fact]
    public async Task MakerChecker_SelfApproval_Rejected()
    {
        var (caseId, _, _) = await CaseReadyForReviewAsync();
        var proposer = Guid.NewGuid();
        var decision = await _manager.ProposeDecisionAsync(caseId, proposer, ProposeAdjust());
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _manager.ApproveDecisionAsync(decision.Id, proposer));
        Assert.Contains("cannot be the proposer", ex.Message);
        Assert.Equal(0, _gateway.AdjustCalls.Count); // không adjust khi self-approve
    }

    [Fact]
    public async Task MakerChecker_DisabledByPolicy_AllowsSelfApproval()
    {
        // A21: policy dispute.maker_checker_required=false → self-approve được phép (wired qua DisputeCaseManager).
        var policyService = new SecurityPolicyService(
            _db,
            new Microsoft.Extensions.Caching.Memory.MemoryCache(new Microsoft.Extensions.Caching.Memory.MemoryCacheOptions()),
            TimeProvider.System);
        await policyService.ProposeAsync(SecurityPolicyKeys.DisputeMakerCheckerRequired,
            SecurityPolicyGroups.MakerChecker, "{\"required\":false}", Guid.NewGuid(), "tắt maker-checker (test)");

        var (caseId, _, _) = await CaseReadyForReviewAsync();
        var proposer = Guid.NewGuid();
        var decision = await _manager.ProposeDecisionAsync(caseId, proposer, ProposeAdjust(50));
        var approved = await _manager.ApproveDecisionAsync(decision.Id, proposer); // cùng người — KHÔNG throw

        Assert.Equal("EXECUTED", approved.Status);
        Assert.Single(_gateway.AdjustCalls);
        Assert.Equal(50, _gateway.AdjustCalls[0].Points);
    }

    [Fact]
    public async Task Approve_WithAdjustment_ExecutesOnce_ResolvesCase()
    {
        var (caseId, _, tenantId) = await CaseReadyForReviewAsync();
        var decision = await _manager.ProposeDecisionAsync(caseId, Guid.NewGuid(), ProposeAdjust(100));
        var approved = await _manager.ApproveDecisionAsync(decision.Id, Guid.NewGuid());

        Assert.Equal("EXECUTED", approved.Status);
        Assert.NotNull(approved.ResolutionReference);
        Assert.Equal("RESOLVED", (await GetCaseAsync(caseId)).Status.ToString());
        var call = Assert.Single(_gateway.AdjustCalls);
        Assert.Equal(100, call.Points);
        Assert.Equal(tenantId, call.TenantId);
        Assert.StartsWith($"dispute:{caseId:N}:adjust:", call.IdempotencyKey);
    }

    [Fact]
    public async Task Approve_LedgerFails_CaseNotResolved_CanRepropose()
    {
        _gateway.AdjustResult = new LoyaltyAdjustResult(false, null, "Insufficient balance");
        var (caseId, _, _) = await CaseReadyForReviewAsync();
        var d1 = await _manager.ProposeDecisionAsync(caseId, Guid.NewGuid(), ProposeAdjust());
        var failed = await _manager.ApproveDecisionAsync(d1.Id, Guid.NewGuid());

        Assert.Equal("FAILED", failed.Status);
        Assert.Equal("UNDER_REVIEW", (await GetCaseAsync(caseId)).Status.ToString());

        // Investigator re-propose → decision mới, idempotency key khác.
        _gateway.AdjustResult = new LoyaltyAdjustResult(true, "tx-123", null);
        var d2 = await _manager.ProposeDecisionAsync(caseId, Guid.NewGuid(), ProposeAdjust());
        var ok = await _manager.ApproveDecisionAsync(d2.Id, Guid.NewGuid());
        Assert.Equal("EXECUTED", ok.Status);
        Assert.Equal("RESOLVED", (await GetCaseAsync(caseId)).Status.ToString());
        Assert.Equal(2, _gateway.AdjustCalls.Count); // mỗi decision 1 lần — không double-adjust cùng key
        Assert.NotEqual(_gateway.AdjustCalls[0].IdempotencyKey, _gateway.AdjustCalls[1].IdempotencyKey);
    }

    [Fact]
    public async Task Approve_NoAdjustmentDecision_ResolvesWithoutGatewayCall()
    {
        var (caseId, _, _) = await CaseReadyForReviewAsync();
        var d = await _manager.ProposeDecisionAsync(caseId, Guid.NewGuid(),
            new ProposeDisputeDecisionRequest("REJECTED", "Không đủ bằng chứng", "Rất tiếc.", null, "v1", null, null, null));
        var approved = await _manager.ApproveDecisionAsync(d.Id, Guid.NewGuid());
        Assert.Equal("EXECUTED", approved.Status); // approve xong, không cần ledger
        Assert.Equal("REJECTED", (await GetCaseAsync(caseId)).Status.ToString());
        Assert.Empty(_gateway.AdjustCalls);
    }

    [Fact]
    public async Task RejectDecision_ProposalRejected_CaseBackToReview()
    {
        var (caseId, _, _) = await CaseReadyForReviewAsync();
        var d = await _manager.ProposeDecisionAsync(caseId, Guid.NewGuid(), ProposeAdjust());
        var rejected = await _manager.RejectDecisionAsync(d.Id, Guid.NewGuid(), "Cần thêm bằng chứng");
        Assert.Equal("REJECTED", rejected.Status);
        Assert.Equal("UNDER_REVIEW", (await GetCaseAsync(caseId)).Status.ToString());
        Assert.Empty(_gateway.AdjustCalls);
    }

    [Fact]
    public async Task Propose_SecondPendingDecision_Rejected()
    {
        var (caseId, _, _) = await CaseReadyForReviewAsync();
        await _manager.ProposeDecisionAsync(caseId, Guid.NewGuid(), ProposeAdjust());
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _manager.ProposeDecisionAsync(caseId, Guid.NewGuid(), ProposeAdjust()));
    }

    [Fact]
    public async Task Decisions_AreAppendOnly_MultipleRowsPreserved()
    {
        _gateway.AdjustResult = new LoyaltyAdjustResult(false, null, "fail");
        var (caseId, _, _) = await CaseReadyForReviewAsync();
        var d1 = await _manager.ProposeDecisionAsync(caseId, Guid.NewGuid(), ProposeAdjust());
        await _manager.ApproveDecisionAsync(d1.Id, Guid.NewGuid()); // FAILED
        _gateway.AdjustResult = new LoyaltyAdjustResult(true, "tx", null);
        var d2 = await _manager.ProposeDecisionAsync(caseId, Guid.NewGuid(), ProposeAdjust());
        await _manager.ApproveDecisionAsync(d2.Id, Guid.NewGuid()); // EXECUTED

        var decisions = await _db.DisputeDecisions.Where(d => d.DisputeCaseId == caseId).ToListAsync();
        Assert.Equal(2, decisions.Count);
        Assert.Contains(decisions, d => d.Status == DisputeDecisionStatus.FAILED);
        Assert.Contains(decisions, d => d.Status == DisputeDecisionStatus.EXECUTED);
    }

    // ── Transactions read ──

    [Fact]
    public async Task GetTransactions_MergesEntries_FromAllCustomerLinks()
    {
        var (subjectId, customerId, tenantId) = await SeedSubjectAsync();
        _gateway.Entries.Add(new LoyaltyLedgerEntryDto(Guid.NewGuid().ToString(), "EARN", 50, null, "order", null, DateTime.UtcNow, tenantId, "SILO_ISSUANCE"));
        var entries = await _manager.GetTransactionsAsync(subjectId, tenantId);
        Assert.Single(entries);
        Assert.Equal(50, entries[0].Points);
    }
}

/// <summary>Stub ledger gateway — records adjust calls, configurable result/entries.</summary>
internal sealed class StubLoyaltyGatewayClient : ILoyaltyGatewayClient
{
    public List<LoyaltyAdjustResult> AdjustResults { get; } = [];
    public List<(Guid CustomerId, Guid TenantId, int Points, string Reason, string IdempotencyKey)> AdjustCalls { get; } = [];
    public List<LoyaltyLedgerEntryDto> Entries { get; } = [];
    public LoyaltyAdjustResult AdjustResult { get; set; } = new(true, "tx-ref", null);

    public Task<IReadOnlyList<LoyaltyLedgerEntryDto>> GetEntriesAsync(
        Guid customerId, Guid tenantId, DateTime? fromUtc = null, DateTime? toUtc = null,
        int limit = 100, CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<LoyaltyLedgerEntryDto>>(Entries);

    public Task<LoyaltyAdjustResult> AdjustPointsAsync(
        Guid customerId, Guid tenantId, int points, string reason,
        string idempotencyKey, CancellationToken ct = default)
    {
        AdjustCalls.Add((customerId, tenantId, points, reason, idempotencyKey));
        return Task.FromResult(AdjustResult);
    }
}

/// <summary>ATRS-10: permission grant matrix + scope + revoke/expire.</summary>
public sealed class AtrsAuthorizationServiceTests : IDisposable
{
    private readonly AtrsDbContext _db;
    private readonly AtrsAuthorizationService _authz;

    public AtrsAuthorizationServiceTests()
    {
        var options = new DbContextOptionsBuilder<AtrsDbContext>()
            .UseInMemoryDatabase($"authz-{Guid.NewGuid()}")
            .Options;
        _db = new AtrsDbContext(options);
        _authz = new AtrsAuthorizationService(_db, new ConfigurationBuilder().Build());
    }

    public void Dispose()
    {
        _db.Dispose();
        GC.SuppressFinalize(this);
    }

    [Fact]
    public async Task Has_NoGrant_ReturnsFalse()
        => Assert.False(await _authz.HasAsync(Guid.NewGuid(), AtrsPermissions.DisputeView));

    [Fact]
    public async Task Has_ActiveGrant_ReturnsTrue()
    {
        var userId = Guid.NewGuid();
        await _authz.GrantAsync(userId, AtrsPermissions.DisputeView, "admin");
        Assert.True(await _authz.HasAsync(userId, AtrsPermissions.DisputeView));
    }

    [Fact]
    public async Task Has_RevokedGrant_ReturnsFalse()
    {
        var userId = Guid.NewGuid();
        var grant = await _authz.GrantAsync(userId, AtrsPermissions.DisputeView, "admin");
        await _authz.RevokeAsync(grant.Id);
        Assert.False(await _authz.HasAsync(userId, AtrsPermissions.DisputeView));
    }

    [Fact]
    public async Task Has_ExpiredGrant_ReturnsFalse()
    {
        var userId = Guid.NewGuid();
        await _authz.GrantAsync(userId, AtrsPermissions.DisputeView, "admin", expiresAt: DateTime.UtcNow.AddMinutes(-1));
        Assert.False(await _authz.HasAsync(userId, AtrsPermissions.DisputeView));
    }

    [Fact]
    public async Task Has_TenantScopedGrant_WrongTenant_ReturnsFalse()
    {
        var userId = Guid.NewGuid();
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();
        await _authz.GrantAsync(userId, AtrsPermissions.DisputeView, "admin", scopeTenantId: tenantA);
        Assert.True(await _authz.HasAsync(userId, AtrsPermissions.DisputeView, tenantA));
        Assert.False(await _authz.HasAsync(userId, AtrsPermissions.DisputeView, tenantB));
        // scopeTenantId null (account-level request) → grant có scope vẫn áp dụng? Rule hiện tại:
        // grant scoped chỉ match khi request cùng tenant → null request bị chặn (fail-closed).
        Assert.False(await _authz.HasAsync(userId, AtrsPermissions.DisputeView, null));
    }

    [Fact]
    public async Task Has_SystemAdminRole_BreakGlassTrue()
    {
        Assert.True(await _authz.HasAsync(Guid.NewGuid(), ["SystemAdmin"], AtrsPermissions.DisputeApproveAdjustment));
    }

    [Fact]
    public async Task Has_OwnerRole_LegacyRecoveryPerms_True_DisputePerms_False()
    {
        var userId = Guid.NewGuid();
        Assert.True(await _authz.HasAsync(userId, ["Owner"], AtrsPermissions.RecoveryApprove));
        Assert.False(await _authz.HasAsync(userId, ["Owner"], AtrsPermissions.DisputeApproveAdjustment));
    }

    [Fact]
    public async Task Grant_DuplicateActive_ReturnsExisting()
    {
        var userId = Guid.NewGuid();
        var g1 = await _authz.GrantAsync(userId, AtrsPermissions.DisputeView, "admin");
        var g2 = await _authz.GrantAsync(userId, AtrsPermissions.DisputeView, "admin");
        Assert.Equal(g1.Id, g2.Id);
        Assert.Equal(1, _db.StaffPermissionGrants.Count());
    }
}
