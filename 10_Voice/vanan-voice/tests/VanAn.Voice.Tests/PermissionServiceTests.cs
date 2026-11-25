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
        var permission = new PermissionService(harness.Store, harness.Features, harness.ContextVerifier, harness.GuestQuota);
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

    // ---- Zero-barrier guest calling (Device identity + VoiceGuestCalling flag, 2026-11-25) ----

    [Fact]
    public async Task Device_caller_with_verified_shop_context_is_allowed_by_default()
    {
        var (permission, harness) = Create();
        using var _ = harness;
        // 'shop' context: verify resolves the tenant (default flag VoiceGuestCalling=true).
        harness.ContextVerifier.VerifiedTenantId = VoiceIdentities.Tenant;
        harness.ContextVerifier.ResolvedCounterparts = new[] { VoiceIdentities.UserB };

        var result = await permission.CheckStartCallAsync(
            VoiceIdentities.Device(VoiceIdentities.UserA), Guid.Empty, "shop", VoiceIdentities.Tenant.ToString(), DateTime.UtcNow);

        result.Allowed.Should().BeTrue();
        result.TenantId.Should().Be(VoiceIdentities.Tenant);
        result.TargetUserId.Should().Be(VoiceIdentities.UserB);
    }

    [Fact]
    public async Task Device_caller_is_denied_when_guest_calling_flag_off()
    {
        var (permission, harness) = Create();
        using var _ = harness;
        harness.ContextVerifier.VerifiedTenantId = VoiceIdentities.Tenant;
        harness.ContextVerifier.ResolvedCounterparts = new[] { VoiceIdentities.UserB };
        harness.Features.Set(VoiceIdentities.Tenant, new VoiceTenantConfig(VoiceGuestCalling: false));

        var result = await permission.CheckStartCallAsync(
            VoiceIdentities.Device(VoiceIdentities.UserA), Guid.Empty, "shop", VoiceIdentities.Tenant.ToString(), DateTime.UtcNow);

        result.Allowed.Should().BeFalse();
        result.Reason.Should().Be(CallEndReason.GuestCallDisabled);
        result.TenantId.Should().Be(VoiceIdentities.Tenant);
    }

    [Fact]
    public async Task Guest_calling_flag_off_does_not_block_logged_in_customer()
    {
        var (permission, harness) = Create();
        using var _ = harness;
        harness.ContextVerifier.VerifiedTenantId = VoiceIdentities.Tenant;
        harness.Features.Set(VoiceIdentities.Tenant, new VoiceTenantConfig(VoiceGuestCalling: false));

        // Sysadmin tắt guest calling → chỉ chặn Device; customer đã đăng nhập vẫn gọi được.
        var result = await permission.CheckStartCallAsync(
            VoiceIdentities.Customer(VoiceIdentities.UserA), VoiceIdentities.UserB, "chat", "conv-1", DateTime.UtcNow);

        result.Allowed.Should().BeTrue();
    }

    [Fact]
    public async Task Device_caller_without_context_is_denied()
    {
        var (permission, harness) = Create();
        using var _ = harness;

        // A device identity carries no tenant claim and no context → no tenant source.
        var result = await permission.CheckStartCallAsync(
            VoiceIdentities.Device(VoiceIdentities.UserA), VoiceIdentities.UserB, null, null, DateTime.UtcNow);

        result.Allowed.Should().BeFalse();
        result.Reason.Should().Be(CallEndReason.ContextRequired);
    }

    [Fact]
    public async Task Device_caller_via_chat_context_is_allowed_when_flag_on()
    {
        var (permission, harness) = Create();
        using var _ = harness;
        // Guest đã chat trước đó → participant row 'Guest' tồn tại → 'chat' context cũng verify được.
        harness.ContextVerifier.VerifiedTenantId = VoiceIdentities.Tenant;

        var result = await permission.CheckStartCallAsync(
            VoiceIdentities.Device(VoiceIdentities.UserA), VoiceIdentities.UserB, "chat", "Shop:t-1", DateTime.UtcNow);

        result.Allowed.Should().BeTrue();
    }
}
public class GuestCallQuotaTests
{
    private static (PermissionService Service, CallServiceHarness Harness) Create()
    {
        var harness = new CallServiceHarness();
        var permission = new PermissionService(harness.Store, harness.Features, harness.ContextVerifier, harness.GuestQuota);
        return (permission, harness);
    }

    private void SetupShopContext(CallServiceHarness harness)
    {
        harness.ContextVerifier.VerifiedTenantId = VoiceIdentities.Tenant;
        harness.ContextVerifier.ResolvedCounterparts = new[] { VoiceIdentities.UserB };
    }

    [Fact]
    public async Task Device_caller_with_free_call_remaining_is_allowed()
    {
        var (permission, harness) = Create();
        using var _ = harness;
        SetupShopContext(harness);
        harness.GuestQuota.HasFreeCall = true;

        var result = await permission.CheckStartCallAsync(
            VoiceIdentities.Device(VoiceIdentities.UserA), Guid.Empty, "shop", VoiceIdentities.Tenant.ToString(), DateTime.UtcNow);

        result.Allowed.Should().BeTrue();
    }

    [Fact]
    public async Task Device_caller_who_used_free_call_is_denied_with_GUEST_CALL_LIMIT()
    {
        var (permission, harness) = Create();
        using var _ = harness;
        SetupShopContext(harness);
        harness.GuestQuota.HasFreeCall = false;

        var result = await permission.CheckStartCallAsync(
            VoiceIdentities.Device(VoiceIdentities.UserA), Guid.Empty, "shop", VoiceIdentities.Tenant.ToString(), DateTime.UtcNow);

        result.Allowed.Should().BeFalse();
        result.Reason.Should().Be(CallEndReason.GuestCallLimit);
        result.TenantId.Should().Be(VoiceIdentities.Tenant);
    }

    [Fact]
    public async Task Quota_limit_only_applies_to_device_callers()
    {
        var (permission, harness) = Create();
        using var _ = harness;
        harness.ContextVerifier.VerifiedTenantId = VoiceIdentities.Tenant;
        harness.GuestQuota.HasFreeCall = false;

        // Customer đã đăng nhập không bị giới hạn số lần gọi.
        var result = await permission.CheckStartCallAsync(
            VoiceIdentities.Customer(VoiceIdentities.UserA), VoiceIdentities.UserB, "chat", "conv-1", DateTime.UtcNow);

        result.Allowed.Should().BeTrue();
    }

    [Fact]
    public async Task Flag_off_takes_precedence_over_quota()
    {
        var (permission, harness) = Create();
        using var _ = harness;
        SetupShopContext(harness);
        harness.Features.Set(VoiceIdentities.Tenant, new VoiceTenantConfig(VoiceGuestCalling: false));
        harness.GuestQuota.HasFreeCall = false;

        // Tenant cấm hẳn khách vãng lai → GUEST_CALL_DISABLED (không phải LIMIT).
        var result = await permission.CheckStartCallAsync(
            VoiceIdentities.Device(VoiceIdentities.UserA), Guid.Empty, "shop", VoiceIdentities.Tenant.ToString(), DateTime.UtcNow);

        result.Reason.Should().Be(CallEndReason.GuestCallDisabled);
    }

    [Fact]
    public async Task GuestCallQuota_counts_only_real_call_attempts()
    {
        var harness = new CallServiceHarness();
        using var _ = harness;
        var quota = new GuestCallQuota(harness.Db);
        var deviceId = VoiceIdentities.UserA;

        (await quota.HasFreeCallAsync(deviceId)).Should().BeTrue("device chưa gọi lần nào");

        // Denied call (Failed — vd INVALID_CONTEXT) KHÔNG tính là đã dùng lượt.
        var denied = VoiceCall.Create(Guid.NewGuid(), VoiceIdentities.Tenant, deviceId, VoiceIdentities.UserB,
            "test", "shop", "Shop:t", DateTime.UtcNow);
        denied.ForceTerminal(CallStatus.Failed, CallEndReason.InvalidContext, DateTime.UtcNow);
        harness.Db.VoiceCalls.Add(denied);
        await harness.Db.SaveChangesAsync();
        (await quota.HasFreeCallAsync(deviceId)).Should().BeTrue("call bị deny không trừ lượt miễn phí");

        // Call đã đổ chuông (Ringing → Ended) TÍNH là đã dùng lượt.
        var rang = VoiceCall.Create(Guid.NewGuid(), VoiceIdentities.Tenant, deviceId, VoiceIdentities.UserB,
            "test", "shop", "Shop:t", DateTime.UtcNow);
        rang.ForceTerminal(CallStatus.Missed, CallEndReason.RingTimeout, DateTime.UtcNow);
        harness.Db.VoiceCalls.Add(rang);
        await harness.Db.SaveChangesAsync();
        (await quota.HasFreeCallAsync(deviceId)).Should().BeFalse("đã gọi 1 lần → lần 2 phải đăng nhập");
    }
}
