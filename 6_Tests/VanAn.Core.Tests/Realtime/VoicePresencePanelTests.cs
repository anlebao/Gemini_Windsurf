using Bunit;
using Bunit.JSInterop;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using VanAn.UI.Platform.Adapters;
using VanAn.UI.Platform.Components.Realtime;
using VanAn.UI.Platform.Core.Interfaces;
using Xunit;

namespace VanAn.Core.Tests.Realtime;

/// <summary>
/// VOICE PRESENCE (2026-11-21): global incoming-call overlay + mini call-bar —
/// chỉ render khi chat surface KHÔNG active (tránh double UI với VoiceCallPanel).
/// </summary>
public class VoicePresencePanelTests : TestContext
{
    private readonly VoiceCallCoordinator _coordinator = new();

    public VoicePresencePanelTests()
    {
        Services.AddSingleton(_coordinator);
        Services.AddSingleton<ICssAdapter, BootstrapAdapter>(); // VanAnButton call controls
        JSInterop.Mode = JSRuntimeMode.Loose; // vananVoiceChat.* → default
    }

    private IRenderedComponent<VoicePresencePanel> Render(bool enabled = true)
        => RenderComponent<VoicePresencePanel>(p => p
            .Add(c => c.Enabled, enabled)
            .Add(c => c.Token, "test-token")
            .Add(c => c.CurrentUserId, Guid.NewGuid())
            .Add(c => c.VoiceHubUrl, "/hubs/voice")
            .Add(c => c.VoiceServiceUrl, "http://localhost"));

    [Fact]
    public void Disabled_RendersNothing()
    {
        var cut = Render(enabled: false);
        cut.Markup.Should().NotContain("voice-presence-incoming");
        cut.Markup.Should().NotContain("voice-presence-callbar");
    }

    [Fact]
    public void IncomingCall_WhenChatIdle_ShowsOverlayWithCallerName()
    {
        var cut = Render();
        _coordinator.PublishIncomingInfo("Nguyễn Văn A", "Shop ABC");
        _coordinator.PublishCallState("ringing", "{}");

        cut.WaitForAssertion(() =>
        {
            var overlay = cut.Find("[data-testid='voice-presence-incoming']");
            overlay.TextContent.Should().Contain("Nguyễn Văn A");
            overlay.TextContent.Should().Contain("Shop ABC");
            cut.Find("[data-testid='voice-presence-accept']").Should().NotBeNull();
            cut.Find("[data-testid='voice-presence-reject']").Should().NotBeNull();
        });
    }

    [Fact]
    public void IncomingCall_WhenChatSurfaceActive_HidesOverlay()
    {
        var cut = Render();
        _coordinator.SetChatSurfaceActive(true);
        _coordinator.PublishCallState("ringing", "{}");

        cut.WaitForAssertion(() =>
            cut.Markup.Should().NotContain("voice-presence-incoming"));
    }

    [Fact]
    public void ActiveCall_ShowsMiniCallBar()
    {
        var cut = Render();
        _coordinator.PublishCallState("connected", "{}");

        cut.WaitForAssertion(() =>
        {
            cut.Find("[data-testid='voice-presence-callbar']").Should().NotBeNull();
            cut.Find("[data-testid='voice-presence-end']").Should().NotBeNull();
            cut.Find("[data-testid='voice-presence-mute']").Should().NotBeNull();
        });
    }

    [Fact]
    public void ActiveCall_WhenChatSurfaceActive_HidesCallBar()
    {
        var cut = Render();
        _coordinator.SetChatSurfaceActive(true);
        _coordinator.PublishCallState("connected", "{}");

        cut.WaitForAssertion(() =>
            cut.Markup.Should().NotContain("voice-presence-callbar"));
    }

    [Fact]
    public void IdleState_RendersNothing()
    {
        var cut = Render();
        _coordinator.PublishCallState("idle", "{}");

        cut.WaitForAssertion(() =>
        {
            cut.Markup.Should().NotContain("voice-presence-incoming");
            cut.Markup.Should().NotContain("voice-presence-callbar");
        });
    }
}
