using FluentAssertions;
using Xunit;
using VanAn.Voice.Domain;

namespace VanAn.Voice.Tests;

public class CallStateMachineTests
{
    [Theory]
    [InlineData(CallStatus.Created, CallStatus.Ringing, true)]
    [InlineData(CallStatus.Ringing, CallStatus.Accepted, true)]
    [InlineData(CallStatus.Accepted, CallStatus.Negotiating, true)]
    [InlineData(CallStatus.Negotiating, CallStatus.Connected, true)]
    [InlineData(CallStatus.Connected, CallStatus.Reconnecting, true)]
    [InlineData(CallStatus.Reconnecting, CallStatus.Connected, true)]
    [InlineData(CallStatus.Connected, CallStatus.Ended, true)]
    [InlineData(CallStatus.Ringing, CallStatus.Rejected, true)]
    [InlineData(CallStatus.Ringing, CallStatus.Cancelled, true)]
    [InlineData(CallStatus.Ringing, CallStatus.Missed, true)]
    public void Allowed_transitions(CallStatus from, CallStatus to, bool expected)
        => CallStateMachine.CanTransition(from, to).Should().Be(expected);

    [Theory]
    [InlineData(CallStatus.Ended, CallStatus.Connected)]     // terminal → active is forbidden
    [InlineData(CallStatus.Rejected, CallStatus.Ringing)]
    [InlineData(CallStatus.Created, CallStatus.Connected)]   // cannot skip negotiation
    [InlineData(CallStatus.Ringing, CallStatus.Connected)]
    [InlineData(CallStatus.Accepted, CallStatus.Ringing)]    // no backward transitions
    [InlineData(CallStatus.Connected, CallStatus.Accepted)]
    [InlineData(CallStatus.Reconnecting, CallStatus.Ended)]  // reconnect must succeed or FAIL
    public void Forbidden_transitions(CallStatus from, CallStatus to)
        => CallStateMachine.CanTransition(from, to).Should().BeFalse();

    [Theory]
    [InlineData(CallStatus.Ended, true)]
    [InlineData(CallStatus.Rejected, true)]
    [InlineData(CallStatus.Cancelled, true)]
    [InlineData(CallStatus.Missed, true)]
    [InlineData(CallStatus.Busy, true)]
    [InlineData(CallStatus.Offline, true)]
    [InlineData(CallStatus.CallCollision, true)]
    [InlineData(CallStatus.Failed, true)]
    [InlineData(CallStatus.Ringing, false)]
    [InlineData(CallStatus.Connected, false)]
    [InlineData(CallStatus.Reconnecting, false)]
    public void Terminal_states(CallStatus status, bool expected)
        => CallStateMachine.IsTerminal(status).Should().Be(expected);

    [Fact]
    public void Transition_is_idempotent()
    {
        var call = VoiceCall.Create(Guid.NewGuid(), VoiceIdentities.Tenant, VoiceIdentities.UserA, VoiceIdentities.UserB, "test", null, null, DateTime.UtcNow);
        call.Transition(CallStatus.Ringing, null, DateTime.UtcNow).Should().BeTrue();
        call.Transition(CallStatus.Ringing, null, DateTime.UtcNow).Should().BeTrue(); // no-op
        call.Status.Should().Be(CallStatus.Ringing);
    }

    [Fact]
    public void Invalid_transition_returns_false_and_keeps_state()
    {
        var call = VoiceCall.Create(Guid.NewGuid(), VoiceIdentities.Tenant, VoiceIdentities.UserA, VoiceIdentities.UserB, "test", null, null, DateTime.UtcNow);
        call.Transition(CallStatus.Connected, null, DateTime.UtcNow).Should().BeFalse();
        call.Status.Should().Be(CallStatus.Created);
    }

    [Fact]
    public void Terminal_bookkeeping_stamps_ended_at_reason_duration()
    {
        var start = new DateTime(2026, 11, 11, 10, 0, 0, DateTimeKind.Utc);
        var call = VoiceCall.Create(Guid.NewGuid(), VoiceIdentities.Tenant, VoiceIdentities.UserA, VoiceIdentities.UserB, "test", null, null, start);
        call.Transition(CallStatus.Ringing, null, start.AddSeconds(1));
        call.Transition(CallStatus.Accepted, null, start.AddSeconds(2));
        call.Transition(CallStatus.Negotiating, null, start.AddSeconds(3));
        call.Transition(CallStatus.Connected, null, start.AddSeconds(4));
        call.Transition(CallStatus.Ended, CallEndReason.UserEnded, start.AddSeconds(65));

        call.IsTerminal().Should().BeTrue();
        call.EndedAt.Should().Be(start.AddSeconds(65));
        call.EndReason.Should().Be(CallEndReason.UserEnded);
        call.DurationSeconds.Should().Be(65);
    }

    [Fact]
    public void ForceTerminal_never_rewrites_history()
    {
        var call = VoiceCall.Create(Guid.NewGuid(), VoiceIdentities.Tenant, VoiceIdentities.UserA, VoiceIdentities.UserB, "test", null, null, DateTime.UtcNow);
        call.ForceTerminal(CallStatus.Failed, CallEndReason.MeteringDenied, DateTime.UtcNow);
        var firstEndedAt = call.EndedAt;
        call.ForceTerminal(CallStatus.Ended, CallEndReason.UserEnded, DateTime.UtcNow.AddMinutes(1));

        call.Status.Should().Be(CallStatus.Failed);
        call.EndedAt.Should().Be(firstEndedAt);
        call.EndReason.Should().Be(CallEndReason.MeteringDenied);
    }
}