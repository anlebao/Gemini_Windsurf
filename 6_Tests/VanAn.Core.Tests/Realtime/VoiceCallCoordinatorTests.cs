using VanAn.UI.Platform.Components.Realtime;
using Xunit;

namespace VanAn.Core.Tests.Realtime;

/// <summary>VOICE PRESENCE (2026-11-21): state bus giữa voice JS owner và observers.</summary>
public class VoiceCallCoordinatorTests
{
    [Fact]
    public void PublishCallState_UpdatesState_AndNotifies()
    {
        var c = new VoiceCallCoordinator();
        var notified = 0;
        c.Changed += () => notified++;

        c.PublishCallState("connected", "{}");
        Assert.Equal("connected", c.CallState);
        Assert.Equal(1, notified);
    }

    [Fact]
    public void IncomingActive_TrueOnlyWhenRinging()
    {
        var c = new VoiceCallCoordinator();
        Assert.False(c.IncomingActive);
        c.PublishCallState("ringing", "{}");
        Assert.True(c.IncomingActive);
        c.PublishCallState("connected", "{}");
        Assert.False(c.IncomingActive);
    }

    [Fact]
    public void ParseIncomingInfo_ExtractsCallerNameAndLabel()
    {
        var json = """{"callId":"x","callerUserId":"y","callerDisplayName":"Nguyễn Văn A","verifiedContextLabel":"Shop ABC","timeoutConfig":{}}""";
        var (name, label) = VoiceCallCoordinator.ParseIncomingInfo(json);
        Assert.Equal("Nguyễn Văn A", name);
        Assert.Equal("Shop ABC", label);
    }

    [Fact]
    public void ParseIncomingInfo_InvalidJson_ReturnsNulls()
    {
        var (name, label) = VoiceCallCoordinator.ParseIncomingInfo("not-json");
        Assert.Null(name);
        Assert.Null(label);
    }

    [Fact]
    public void SetChatSurfaceActive_SuppressesDuplicates()
    {
        var c = new VoiceCallCoordinator();
        var notified = 0;
        c.Changed += () => notified++;

        c.SetChatSurfaceActive(true);
        c.SetChatSurfaceActive(true); // no-op — không notify lại
        Assert.True(c.ChatSurfaceActive);
        Assert.Equal(1, notified);

        c.SetChatSurfaceActive(false);
        Assert.False(c.ChatSurfaceActive);
        Assert.Equal(2, notified);
    }

    [Fact]
    public void Reset_ClearsAllState()
    {
        var c = new VoiceCallCoordinator();
        c.PublishHubState("connected");
        c.PublishCallState("ringing", "{}");
        c.PublishIncomingInfo("A", "B");
        c.SetChatSurfaceActive(true);

        c.Reset();
        Assert.Equal("connecting", c.HubState);
        Assert.Equal("idle", c.CallState);
        Assert.False(c.IncomingActive);
        Assert.False(c.ChatSurfaceActive);
        Assert.Null(c.IncomingCallerName);
    }
}
