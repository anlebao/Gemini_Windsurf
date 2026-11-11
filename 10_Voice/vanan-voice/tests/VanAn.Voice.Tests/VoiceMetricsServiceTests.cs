using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;
using VanAn.Voice.Api.Services;
using VanAn.Voice.Domain;

namespace VanAn.Voice.Tests;

public class VoiceMetricsServiceTests
{
    [Fact]
    public async Task Summary_aggregates_call_statistics()
    {
        using var h = new CallServiceHarness();
        var now = DateTime.UtcNow;
        var callId = Guid.NewGuid();

        var connected = VoiceCall.Create(callId, VoiceIdentities.Tenant, VoiceIdentities.UserA, VoiceIdentities.UserB, "test", null, null, now);
        connected.Transition(CallStatus.Ringing, null, now);
        connected.Transition(CallStatus.Accepted, null, now);
        connected.Transition(CallStatus.Negotiating, null, now);
        connected.Transition(CallStatus.Connected, null, now);
        connected.SetConnectionType("TURN");
        connected.Transition(CallStatus.Ended, CallEndReason.UserEnded, now.AddSeconds(90));
        h.Db.VoiceCalls.Add(connected);

        var denied = VoiceCall.Create(Guid.NewGuid(), VoiceIdentities.Tenant, VoiceIdentities.UserA, VoiceIdentities.UserC, "test", null, null, now);
        denied.ForceTerminal(CallStatus.Failed, CallEndReason.MeteringDenied, now);
        h.Db.VoiceCalls.Add(denied);
        await h.Db.SaveChangesAsync();

        var metrics = await new VoiceMetricsService(h.Db).SummaryAsync(now);

        metrics.TotalCalls.Should().Be(2);
        metrics.TodayCalls.Should().Be(2);
        metrics.ConnectedCalls.Should().Be(1);
        metrics.FailedCalls.Should().Be(1);
        metrics.MeteringDenied.Should().Be(1);
        metrics.TurnRelayed.Should().Be(1);
        metrics.AvgDurationSeconds.Should().Be(90);
        metrics.TurnPercentage.Should().Be(100); // 1 TURN / 1 typed call
    }

    [Fact]
    public async Task Empty_db_returns_zeroes()
    {
        using var h = new CallServiceHarness();

        var metrics = await new VoiceMetricsService(h.Db).SummaryAsync(DateTime.UtcNow);

        metrics.TotalCalls.Should().Be(0);
        metrics.TurnPercentage.Should().Be(0);
        metrics.AvgDurationSeconds.Should().Be(0);
    }
}