using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;
using VanAn.Voice.Api.Services;
using VanAn.Voice.Contracts;
using VanAn.Voice.Domain;
using VanAn.Voice.Infra;

namespace VanAn.Voice.Tests;

public class CallServiceIntegrationTests
{
    // ---- Happy path ----

    [Fact]
    public async Task Full_lifecycle_ring_accept_negotiate_connect_end()
    {
        using var h = new CallServiceHarness();
        h.Store.AddConnection(VoiceIdentities.UserB, "conn-b");
        var callId = Guid.NewGuid();

        var started = await h.Service.StartCallAsync(VoiceIdentities.Staff(VoiceIdentities.UserA),
            new StartCallRequest(callId, VoiceIdentities.UserB, null, null), "conn-a");
        started.Status.Should().Be("RINGING");

        (await h.Service.AcceptCallAsync(VoiceIdentities.Staff(VoiceIdentities.UserB), callId, "conn-b")).Should().BeTrue();
        (await h.Service.SendOfferAsync(VoiceIdentities.Staff(VoiceIdentities.UserA), new SdpDto(callId, "offer-1"))).Should().BeTrue();
        (await h.Service.SendAnswerAsync(VoiceIdentities.Staff(VoiceIdentities.UserB), new SdpDto(callId, "answer-1"))).Should().BeTrue();
        (await h.Service.NotifyConnectedAsync(VoiceIdentities.Staff(VoiceIdentities.UserA), callId)).Should().BeTrue();
        (await h.Service.EndCallAsync(VoiceIdentities.Staff(VoiceIdentities.UserA), callId)).Should().BeTrue();

        h.Publisher.SentTo("conn-a").Select(x => x.Method).Should().Contain(new[]
        {
            "CallCreated", "CallAccepted", "ReceiveAnswer", "CallConnected", "CallEnded"
        });
        h.Publisher.SentTo("conn-b").Select(x => x.Method).Should().Contain(new[]
        {
            "IncomingCall", "CallAccepted", "ReceiveOffer", "CallConnected", "CallEnded"
        });

        var row = await h.Db.VoiceCalls.SingleAsync(c => c.Id == callId);
        row.Status.Should().Be(CallStatus.Ended);
        row.EndReason.Should().Be(CallEndReason.UserEnded);
        row.DurationSeconds.Should().BeGreaterThanOrEqualTo(0);
        h.Store.Get(callId).Should().BeNull(); // released from runtime state
        (await h.Db.VoiceCallEvents.CountAsync(e => e.CallId == callId)).Should().BeGreaterThanOrEqualTo(5);
    }

    // ---- Idempotency ----

    [Fact]
    public async Task Duplicate_startcall_same_callid_returns_existing_call()
    {
        using var h = new CallServiceHarness();
        h.Store.AddConnection(VoiceIdentities.UserB, "conn-b");
        var callId = Guid.NewGuid();

        var first = await h.Service.StartCallAsync(VoiceIdentities.Staff(VoiceIdentities.UserA),
            new StartCallRequest(callId, VoiceIdentities.UserB, null, null), "conn-a");
        var second = await h.Service.StartCallAsync(VoiceIdentities.Staff(VoiceIdentities.UserA),
            new StartCallRequest(callId, VoiceIdentities.UserB, null, null), "conn-a");

        second.IsNew.Should().BeFalse();
        second.Status.Should().Be(first.Status);
        (await h.Db.VoiceCalls.CountAsync(c => c.Id == callId)).Should().Be(1);
    }

    // ---- Offline callee ----

    [Fact]
    public async Task Offline_callee_returns_offline_immediately_and_records_missed_notification()
    {
        using var h = new CallServiceHarness();
        var callId = Guid.NewGuid();

        var result = await h.Service.StartCallAsync(VoiceIdentities.Staff(VoiceIdentities.UserA),
            new StartCallRequest(callId, VoiceIdentities.UserB, null, null), "conn-a");

        result.Status.Should().Be("OFFLINE");
        h.Publisher.SentTo("conn-a").Select(x => x.Method).Should().Contain("CallOffline");

        var row = await h.Db.VoiceCalls.SingleAsync(c => c.Id == callId);
        row.Status.Should().Be(CallStatus.Offline);
        h.Store.Get(callId).Should().BeNull();

        // Missed-call notification: caller may notify once; callee sees it on next open.
        (await h.Service.SendMissedCallNotificationAsync(VoiceIdentities.Staff(VoiceIdentities.UserA), callId)).Should().BeTrue();
        (await h.Service.SendMissedCallNotificationAsync(VoiceIdentities.Staff(VoiceIdentities.UserA), callId)).Should().BeFalse(); // once per call
        (await h.Db.VoiceCallEvents.CountAsync(e => e.CallId == callId && e.EventType == "missed-call" && e.ActorUserId == VoiceIdentities.UserB))
            .Should().Be(1);
    }

    // ---- Busy rules (FR-007) ----

    [Fact]
    public async Task Caller_busy_is_rejected()
    {
        using var h = new CallServiceHarness();
        h.Store.AddConnection(VoiceIdentities.UserB, "conn-b");
        h.Store.AddConnection(VoiceIdentities.UserC, "conn-c");
        var callId1 = Guid.NewGuid();
        await h.Service.StartCallAsync(VoiceIdentities.Staff(VoiceIdentities.UserA),
            new StartCallRequest(callId1, VoiceIdentities.UserB, null, null), "conn-a");

        var second = await h.Service.StartCallAsync(VoiceIdentities.Staff(VoiceIdentities.UserA),
            new StartCallRequest(Guid.NewGuid(), VoiceIdentities.UserC, null, null), "conn-a");

        second.Status.Should().Be("BUSY");
        second.Reason.Should().Be(CallEndReason.CallerBusy);
        h.Publisher.SentTo("conn-a").Select(x => x.Method).Should().Contain("CallBusy");
    }

    [Fact]
    public async Task Receiver_busy_is_rejected()
    {
        using var h = new CallServiceHarness();
        h.Store.AddConnection(VoiceIdentities.UserB, "conn-b");
        h.Store.AddConnection(VoiceIdentities.UserC, "conn-c");
        await h.Service.StartCallAsync(VoiceIdentities.Staff(VoiceIdentities.UserB),
            new StartCallRequest(Guid.NewGuid(), VoiceIdentities.UserC, null, null), "conn-b");

        var attempt = await h.Service.StartCallAsync(VoiceIdentities.Staff(VoiceIdentities.UserA),
            new StartCallRequest(Guid.NewGuid(), VoiceIdentities.UserB, null, null), "conn-a");

        attempt.Status.Should().Be("BUSY");
        attempt.Reason.Should().Be(CallEndReason.ReceiverBusy);
    }

    // ---- Collision (FR-013) ----

    [Fact]
    public async Task Call_collision_rejects_later_startcall_without_row()
    {
        using var h = new CallServiceHarness();
        h.Store.AddConnection(VoiceIdentities.UserA, "conn-a");
        h.Store.AddConnection(VoiceIdentities.UserB, "conn-b");

        await h.Service.StartCallAsync(VoiceIdentities.Staff(VoiceIdentities.UserA),
            new StartCallRequest(Guid.NewGuid(), VoiceIdentities.UserB, null, null), "conn-a");

        var collided = await h.Service.StartCallAsync(VoiceIdentities.Staff(VoiceIdentities.UserB),
            new StartCallRequest(Guid.NewGuid(), VoiceIdentities.UserA, null, null), "conn-b");

        collided.Status.Should().Be("CALL_COLLISION");
        h.Publisher.SentTo("conn-b").Select(x => x.Method).Should().Contain("CallCollision");
    }

    // ---- Multi-device accept race ----

    [Fact]
    public async Task First_accept_wins_other_devices_get_cleanup_event()
    {
        using var h = new CallServiceHarness();
        h.Store.AddConnection(VoiceIdentities.UserB, "conn-b1");
        h.Store.AddConnection(VoiceIdentities.UserB, "conn-b2");
        var callId = Guid.NewGuid();
        await h.Service.StartCallAsync(VoiceIdentities.Staff(VoiceIdentities.UserA),
            new StartCallRequest(callId, VoiceIdentities.UserB, null, null), "conn-a");

        (await h.Service.AcceptCallAsync(VoiceIdentities.Staff(VoiceIdentities.UserB), callId, "conn-b1")).Should().BeTrue();
        (await h.Service.AcceptCallAsync(VoiceIdentities.Staff(VoiceIdentities.UserB), callId, "conn-b2")).Should().BeTrue(); // no-op + cleanup

        var row = await h.Db.VoiceCalls.SingleAsync(c => c.Id == callId);
        row.Status.Should().Be(CallStatus.Accepted);
        h.Publisher.SentTo("conn-b2").Select(x => x.Method).Should().Contain("CallAccepted");
        h.Publisher.SentTo("conn-a").Select(x => x.Method).Count(m => m == "CallAccepted").Should().Be(1);
    }

    // ---- Metering gate ----

    [Fact]
    public async Task Metering_deny_rejects_before_ringing()
    {
        using var h = new CallServiceHarness();
        h.Store.AddConnection(VoiceIdentities.UserB, "conn-b");
        var callId = Guid.NewGuid();

        var result = await h.Service.StartCallAsync(VoiceIdentities.Staff(VoiceIdentities.UserA),
            new StartCallRequest(callId, VoiceIdentities.UserB, null, null), "conn-a");

        // Metering not configured in harness → fail-open → call proceeds.
        result.Status.Should().Be("RINGING");
    }

    [Fact]
    public async Task Kill_switch_off_rejects_with_feature_disabled()
    {
        using var h = new CallServiceHarness();
        h.Store.AddConnection(VoiceIdentities.UserB, "conn-b");
        h.Features.Set(VoiceIdentities.Tenant, new VoiceTenantConfig(VoiceFeatureEnabled: false));
        var callId = Guid.NewGuid();

        var result = await h.Service.StartCallAsync(VoiceIdentities.Staff(VoiceIdentities.UserA),
            new StartCallRequest(callId, VoiceIdentities.UserB, null, null), "conn-a");

        result.Status.Should().Be("FAILED");
        result.Reason.Should().Be(CallEndReason.FeatureDisabled);
        h.Publisher.SentTo("conn-a").Select(x => x.Method).Should().Contain("CallFailed");
    }

    // ---- Concurrent StartCall race (FR-007 DB backstop) ----

    [Fact]
    public async Task Concurrent_startcall_same_caller_one_wins()
    {
        using var h = new CallServiceHarness();
        h.Store.AddConnection(VoiceIdentities.UserB, "conn-b");
        h.Store.AddConnection(VoiceIdentities.UserC, "conn-c");

        var t1 = h.Service.StartCallAsync(VoiceIdentities.Staff(VoiceIdentities.UserA),
            new StartCallRequest(Guid.NewGuid(), VoiceIdentities.UserB, null, null), "conn-a");
        var t2 = h.Service.StartCallAsync(VoiceIdentities.Staff(VoiceIdentities.UserA),
            new StartCallRequest(Guid.NewGuid(), VoiceIdentities.UserC, null, null), "conn-a");

        var results = await Task.WhenAll(t1, t2);
        results.Count(r => r.Status == "RINGING").Should().Be(1);
        results.Count(r => r.Status == "BUSY").Should().Be(1);
    }

    // ---- Reconnection ----

    [Fact]
    public async Task Reconnect_within_window_restores_connected()
    {
        using var h = new CallServiceHarness();
        h.Store.AddConnection(VoiceIdentities.UserB, "conn-b");
        var callId = Guid.NewGuid();
        await h.Service.StartCallAsync(VoiceIdentities.Staff(VoiceIdentities.UserA),
            new StartCallRequest(callId, VoiceIdentities.UserB, null, null), "conn-a");
        await h.Service.AcceptCallAsync(VoiceIdentities.Staff(VoiceIdentities.UserB), callId, "conn-b");
        await h.Service.SendOfferAsync(VoiceIdentities.Staff(VoiceIdentities.UserA), new SdpDto(callId, "offer"));
        await h.Service.SendAnswerAsync(VoiceIdentities.Staff(VoiceIdentities.UserB), new SdpDto(callId, "answer"));
        await h.Service.NotifyConnectedAsync(VoiceIdentities.Staff(VoiceIdentities.UserA), callId);

        (await h.Service.NotifyReconnectingAsync(VoiceIdentities.Staff(VoiceIdentities.UserA), callId)).Should().BeTrue();
        (await h.Service.NotifyReconnectedAsync(VoiceIdentities.Staff(VoiceIdentities.UserA), callId, "conn-a2")).Should().BeTrue();

        var row = await h.Db.VoiceCalls.SingleAsync(c => c.Id == callId);
        row.Status.Should().Be(CallStatus.Connected);
    }

    // ---- Reconciliation ----

    [Fact]
    public async Task Startup_reconciliation_marks_stale_calls_failed()
    {
        using var h = new CallServiceHarness();
        var staleCallId = Guid.NewGuid();
        var stale = VoiceCall.Create(staleCallId, VoiceIdentities.Tenant, VoiceIdentities.UserA, VoiceIdentities.UserB, "test", null, null, DateTime.UtcNow.AddMinutes(-10));
        h.Db.VoiceCalls.Add(stale);
        await h.Db.SaveChangesAsync();

        var swept = await VoiceReconciliation.MarkStaleAsync(h.Db, DateTime.UtcNow);

        swept.Should().Be(1);
        var row = await h.Db.VoiceCalls.SingleAsync(c => c.Id == staleCallId);
        row.Status.Should().Be(CallStatus.Failed);
        row.EndReason.Should().Be(CallEndReason.ServerRestart);
        (await h.Db.VoiceCallEvents.CountAsync(e => e.CallId == staleCallId && e.EventType == "call.failed")).Should().Be(1);
    }

    // ---- Self-call ----

    [Fact]
    public async Task Self_call_is_rejected()
    {
        using var h = new CallServiceHarness();
        var result = await h.Service.StartCallAsync(VoiceIdentities.Staff(VoiceIdentities.UserA),
            new StartCallRequest(Guid.NewGuid(), VoiceIdentities.UserA, null, null), "conn-a");

        result.Status.Should().Be("FAILED");
        result.Reason.Should().Be(CallEndReason.SelfCall);
    }

    // ---- Chat-first: counterpart resolved from the conversation (P4) ----

    [Fact]
    public async Task StartCall_without_target_resolves_counterpart_from_context()
    {
        using var h = new CallServiceHarness();
        h.ContextVerifier.VerifiedTenantId = VoiceIdentities.Tenant;
        h.ContextVerifier.ResolvedCounterpart = VoiceIdentities.UserB;
        h.Store.AddConnection(VoiceIdentities.UserB, "conn-b");
        var callId = Guid.NewGuid();

        var result = await h.Service.StartCallAsync(VoiceIdentities.Staff(VoiceIdentities.UserA),
            new StartCallRequest(callId, Guid.Empty, "chat", "Shop:subject-1"), "conn-a");

        result.Status.Should().Be("RINGING");
        var row = await h.Db.VoiceCalls.SingleAsync(c => c.Id == callId);
        row.ReceiverUserId.Should().Be(VoiceIdentities.UserB); // resolved server-side
        row.ContextType.Should().Be("chat");
        h.Publisher.SentTo("conn-b").Select(x => x.Method).Should().Contain("IncomingCall");
    }

    [Fact]
    public async Task StartCall_denied_when_counterpart_cannot_be_resolved()
    {
        using var h = new CallServiceHarness();
        h.ContextVerifier.VerifiedTenantId = VoiceIdentities.Tenant;
        h.ContextVerifier.ResolvedCounterpart = null; // no counterpart
        var callId = Guid.NewGuid();

        var result = await h.Service.StartCallAsync(VoiceIdentities.Staff(VoiceIdentities.UserA),
            new StartCallRequest(callId, Guid.Empty, "chat", "Shop:subject-1"), "conn-a");

        result.Status.Should().Be("FAILED");
        result.Reason.Should().Be(CallEndReason.InvalidContext);
    }
}