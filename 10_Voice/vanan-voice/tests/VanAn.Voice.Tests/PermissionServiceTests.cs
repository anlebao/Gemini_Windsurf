using FluentAssertions;
using Xunit;
using VanAn.Voice.Api.Services;
using VanAn.Voice.Domain;

namespace VanAn.Voice.Tests;

public class PermissionServiceTests
{
    private static (PermissionService Service, CallServiceHarness Harness) Create()
    {
        var harness = new CallServiceHarness();
        var permission = new PermissionService(harness.Store, harness.Features, harness.ContextVerifier);
        return (permission, harness);
    }

    [Fact]
    public async Task Self_call_is_denied()
    {
        var (permission, harness) = Create();
        using var _ = harness;

        var result = await permission.CheckStartCallAsync(
            VoiceIdentities.Staff(VoiceIdentities.UserA), VoiceIdentities.UserA, null, null, DateTime.UtcNow);

        result.Allowed.Should().BeFalse();
        result.Reason.Should().Be(CallEndReason.SelfCall);
    }

    [Fact]
    public async Task Caller_already_in_call_is_denied()
    {
        var (permission, harness) = Create();
        using var _keep = harness;
        harness.Store.AddConnection(VoiceIdentities.UserA, "conn-a");

        var call = VoiceCall.Create(Guid.NewGuid(), VoiceIdentities.Tenant, VoiceIdentities.UserA, VoiceIdentities.UserB, "test", null, null, DateTime.UtcNow);
        harness.Store.TryAddActive(new VoiceStateStore.ActiveCall { Call = call, CallerConnectionId = "conn-a", RingStartedAt = DateTime.UtcNow }, out _);

        var result = await permission.CheckStartCallAsync(
            VoiceIdentities.Staff(VoiceIdentities.UserA), VoiceIdentities.UserC, null, null, DateTime.UtcNow);

        result.Allowed.Should().BeFalse();
        result.Reason.Should().Be(CallEndReason.CallerBusy);
    }

    [Fact]
    public async Task Kill_switch_off_denies_regardless_of_wallet()
    {
        var (permission, harness) = Create();
        using var _ = harness;
        harness.ContextVerifier.VerifiedTenantId = VoiceIdentities.Tenant;
        harness.Features.Set(VoiceIdentities.Tenant, new VoiceTenantConfig(VoiceFeatureEnabled: false));

        var result = await permission.CheckStartCallAsync(
            VoiceIdentities.Customer(VoiceIdentities.UserA), VoiceIdentities.UserB, "chat", "conv-1", DateTime.UtcNow);

        result.Allowed.Should().BeFalse();
        result.Reason.Should().Be(CallEndReason.FeatureDisabled);
    }

    [Fact]
    public async Task Verified_context_allows_customer_and_resolves_tenant()
    {
        var (permission, harness) = Create();
        using var _ = harness;
        harness.ContextVerifier.VerifiedTenantId = VoiceIdentities.Tenant;

        var result = await permission.CheckStartCallAsync(
            VoiceIdentities.Customer(VoiceIdentities.UserA), VoiceIdentities.UserB, "chat", "conv-1", DateTime.UtcNow);

        result.Allowed.Should().BeTrue();
        result.TenantId.Should().Be(VoiceIdentities.Tenant);
    }

    [Fact]
    public async Task Unverifiable_context_is_denied()
    {
        var (permission, harness) = Create();
        using var _ = harness;
        harness.ContextVerifier.Fail = true;

        var result = await permission.CheckStartCallAsync(
            VoiceIdentities.Customer(VoiceIdentities.UserA), VoiceIdentities.UserB, "chat", "conv-1", DateTime.UtcNow);

        result.Allowed.Should().BeFalse();
        result.Reason.Should().Be(CallEndReason.InvalidContext);
    }

    [Fact]
    public async Task Guest_only_counterpart_is_denied_guest_peer()
    {
        var (permission, harness) = Create();
        using var _ = harness;
        // 2026-11-26: staff calls a thread whose only counterpart is a guest —
        // candidates resolve empty but the guest probe says "guest-only thread"
        // → GUEST_PEER so the UI can explain "khách chưa đăng nhập".
        harness.ContextVerifier.ResolvedCounterparts = Array.Empty<Guid>();
        harness.ContextVerifier.HasGuestCounterpart = true;

        var result = await permission.CheckStartCallAsync(
            VoiceIdentities.Staff(VoiceIdentities.UserA), Guid.Empty, "chat", "conv-g", DateTime.UtcNow);

        result.Allowed.Should().BeFalse();
        result.Reason.Should().Be(CallEndReason.GuestPeer);
    }

    [Fact]
    public async Task Empty_counterpart_without_guest_stays_invalid_context()
    {
        var (permission, harness) = Create();
        using var _ = harness;
        harness.ContextVerifier.ResolvedCounterparts = Array.Empty<Guid>();
        harness.ContextVerifier.HasGuestCounterpart = false;

        var result = await permission.CheckStartCallAsync(
            VoiceIdentities.Staff(VoiceIdentities.UserA), Guid.Empty, "chat", "conv-x", DateTime.UtcNow);

        result.Allowed.Should().BeFalse();
        result.Reason.Should().Be(CallEndReason.InvalidContext);
    }

    [Fact]
    public async Task No_context_customer_is_denied_no_tenant_source()
    {
        var (permission, harness) = Create();
        using var _ = harness;

        var result = await permission.CheckStartCallAsync(
            VoiceIdentities.Customer(VoiceIdentities.UserA), VoiceIdentities.UserB, null, null, DateTime.UtcNow);

        result.Allowed.Should().BeFalse();
        result.Reason.Should().Be(CallEndReason.ContextRequired);
    }

    [Fact]
    public async Task No_context_requires_open_calling_flag()
    {
        var (permission, harness) = Create();
        using var _keep = harness;
        harness.Features.Set(VoiceIdentities.Tenant, new VoiceTenantConfig(VoiceOpenCalling: false)); // reset harness default

        // Staff with tenant, open-calling off → denied.
        var denied = await permission.CheckStartCallAsync(
            VoiceIdentities.Staff(VoiceIdentities.UserA), VoiceIdentities.UserB, null, null, DateTime.UtcNow);
        denied.Allowed.Should().BeFalse();
        denied.Reason.Should().Be(CallEndReason.ContextRequired);

        // Tenant opts in → allowed.
        harness.Features.Set(VoiceIdentities.Tenant, new VoiceTenantConfig(VoiceOpenCalling: true));
        var allowed = await permission.CheckStartCallAsync(
            VoiceIdentities.Staff(VoiceIdentities.UserA), VoiceIdentities.UserB, null, null, DateTime.UtcNow);
        allowed.Allowed.Should().BeTrue();
        allowed.TenantId.Should().Be(VoiceIdentities.Tenant);
    }

    // ---- R9 (voice_presence Phase C): round-robin counterpart pick ----

    private static async Task<PermissionResult> CallWithCandidatesAsync(
        PermissionService permission, CallServiceHarness harness, params Guid[] candidates)
    {
        harness.ContextVerifier.VerifiedTenantId = VoiceIdentities.Tenant;
        harness.ContextVerifier.ResolvedCounterparts = candidates;
        return await permission.CheckStartCallAsync(
            VoiceIdentities.Customer(VoiceIdentities.UserA), Guid.Empty, "chat", "conv-rr", DateTime.UtcNow);
    }

    [Fact]
    public async Task Reachable_candidates_rotate_round_robin()
    {
        var (permission, harness) = Create();
        using var _ = harness;
        harness.Store.AddConnection(VoiceIdentities.UserB, "conn-b");
        harness.Store.AddConnection(VoiceIdentities.UserC, "conn-c");

        var first = await CallWithCandidatesAsync(permission, harness, VoiceIdentities.UserB, VoiceIdentities.UserC);
        var second = await CallWithCandidatesAsync(permission, harness, VoiceIdentities.UserB, VoiceIdentities.UserC);
        var third = await CallWithCandidatesAsync(permission, harness, VoiceIdentities.UserB, VoiceIdentities.UserC);

        first.TargetUserId.Should().Be(VoiceIdentities.UserB);
        second.TargetUserId.Should().Be(VoiceIdentities.UserC);
        third.TargetUserId.Should().Be(VoiceIdentities.UserB);
    }

    [Fact]
    public async Task Busy_candidate_is_skipped_for_reachable_one()
    {
        var (permission, harness) = Create();
        using var keep = harness;
        harness.Store.AddConnection(VoiceIdentities.UserB, "conn-b");
        harness.Store.AddConnection(VoiceIdentities.UserC, "conn-c");
        // UserB đang trong call → chỉ UserC reachable.
        var call = VoiceCall.Create(Guid.NewGuid(), VoiceIdentities.Tenant, VoiceIdentities.UserB, VoiceIdentities.UserA, "test", null, null, DateTime.UtcNow);
        harness.Store.TryAddActive(new VoiceStateStore.ActiveCall { Call = call, CallerConnectionId = "conn-x", RingStartedAt = DateTime.UtcNow }, out _);

        var result = await CallWithCandidatesAsync(permission, harness, VoiceIdentities.UserB, VoiceIdentities.UserC);

        result.TargetUserId.Should().Be(VoiceIdentities.UserC);
    }

    [Fact]
    public async Task No_reachable_falls_back_to_first_candidate()
    {
        var (permission, harness) = Create();
        using var _ = harness;
        // Không ai có connection → fallback candidates[0] (sẽ nhận OFFLINE trung thực).
        var result = await CallWithCandidatesAsync(permission, harness, VoiceIdentities.UserB, VoiceIdentities.UserC);

        result.TargetUserId.Should().Be(VoiceIdentities.UserB);
    }
}