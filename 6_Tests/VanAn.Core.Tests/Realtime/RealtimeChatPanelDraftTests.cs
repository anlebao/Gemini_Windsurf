using Bunit;
using Bunit.JSInterop;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using VanAn.UI.Platform.Adapters;
using VanAn.UI.Platform.Components.Realtime;
using VanAn.UI.Platform.Core.Interfaces;
using VanAn.UI.Platform.Realtime;
using Xunit;

namespace VanAn.Core.Tests.Realtime;

/// <summary>
/// Task AI-P8 — RealtimeChatPanel.InitialDraft: one-shot prefill for AI→human escalation.
/// Applied once, never overwrites user typing on re-render; null = unchanged behavior.
/// </summary>
public class RealtimeChatPanelDraftTests : TestContext
{
    private static readonly Guid SubjectId = Guid.NewGuid();

    public RealtimeChatPanelDraftTests()
    {
        Services.AddSingleton<ICssAdapter, BootstrapAdapter>();
        Services.AddSingleton<IRealtimeChatClient>(new FakeRealtimeChatClient());
        Services.AddSingleton<IRealtimeEndpointProvider>(new StubEndpoints());
        JSInterop.Mode = JSRuntimeMode.Loose; // scrollToBottom → default
    }

    private IRenderedComponent<RealtimeChatPanel> Render(string? draft)
        => RenderComponent<RealtimeChatPanel>(p => p
            .Add(c => c.SubjectType, "Shop")
            .Add(c => c.SubjectId, SubjectId)
            .Add(c => c.InitialDraft, draft));

    [Fact]
    public void InitialDraft_PrefillsInput_Once()
    {
        var cut = Render("Câu hỏi AI không trả lời được");
        cut.WaitForAssertion(() =>
            cut.Find(".chat-panel input").GetAttribute("value")
                .Should().Be("Câu hỏi AI không trả lời được"));
    }

    [Fact]
    public void InitialDraft_Rerender_DoesNotOverwriteUserTyping()
    {
        var cut = Render("draft gốc");
        cut.WaitForAssertion(() =>
            cut.Find(".chat-panel input").GetAttribute("value").Should().Be("draft gốc"));

        cut.Find(".chat-panel input").Input("khách đang gõ dở");
        cut.SetParametersAndRender(p => p
            .Add(c => c.SubjectType, "Shop")
            .Add(c => c.SubjectId, SubjectId)
            .Add(c => c.InitialDraft, "draft gốc"));

        cut.Find(".chat-panel input").GetAttribute("value").Should().Be("khách đang gõ dở");
    }

    [Fact]
    public void InitialDraft_Null_InputStaysEmpty()
    {
        var cut = Render(null);
        cut.WaitForAssertion(() =>
            cut.Find(".chat-panel input").GetAttribute("value").Should().BeNullOrEmpty());
    }

    [Fact]
    public void HeaderTitle_OverridesDefault()
    {
        var cut = RenderComponent<RealtimeChatPanel>(p => p
            .Add(c => c.SubjectType, "Shop")
            .Add(c => c.SubjectId, SubjectId)
            .Add(c => c.HeaderTitle, "Chủ quán"));
        cut.WaitForAssertion(() =>
        {
            cut.Markup.Should().Contain("Chủ quán");
            cut.Markup.Should().NotContain("Chat với khách hàng");
        });
    }

    private sealed class FakeRealtimeChatClient : IRealtimeChatClient
    {
        public Task<RealtimeChatHistoryResult> GetHistoryAsync(string subjectType, Guid subjectId,
            string? customerToken, Guid? customerDeviceId, int take = 100, string? staffToken = null,
            CancellationToken ct = default)
            => Task.FromResult(new RealtimeChatHistoryResult { Success = true });

        public Task<RealtimeSendResult> SendMessageAsync(string subjectType, Guid subjectId, string content,
            string? customerToken, Guid? customerDeviceId, string? staffToken = null,
            CancellationToken ct = default)
            => Task.FromResult(new RealtimeSendResult { Success = true });
    }

    private sealed class StubEndpoints : IRealtimeEndpointProvider
    {
        public string GetGatewayBaseUrl() => "http://localhost";
        public string GetMessagingHubUrl() => "http://localhost/hubs/messaging";
        public string GetTrackingHubUrl() => "http://localhost/hubs/tracking";
    }
}
