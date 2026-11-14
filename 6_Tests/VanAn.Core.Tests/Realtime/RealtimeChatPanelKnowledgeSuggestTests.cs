using Bunit;
using Bunit.JSInterop;
using FluentAssertions;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using VanAn.UI.Platform.Adapters;
using VanAn.UI.Platform.Components.Realtime;
using VanAn.UI.Platform.Core.Interfaces;
using VanAn.UI.Platform.Realtime;
using Xunit;

namespace VanAn.Core.Tests.Realtime;

/// <summary>
/// AI-P11 — "Đề xuất AI" button on RealtimeChatPanel: renders only for own messages
/// when opted in, enabled only with a preceding customer message, fires
/// OnKnowledgeSuggest with the verbatim paired Q/A.
/// </summary>
public class RealtimeChatPanelKnowledgeSuggestTests : TestContext
{
    private static readonly Guid SubjectId = Guid.NewGuid();
    private static readonly Guid OwnerId = Guid.NewGuid();
    private static readonly Guid CustomerId = Guid.NewGuid();

    public RealtimeChatPanelKnowledgeSuggestTests()
    {
        Services.AddSingleton<ICssAdapter, BootstrapAdapter>();
        Services.AddSingleton<IRealtimeChatClient>(new FakeChatClient());
        Services.AddSingleton<IRealtimeEndpointProvider>(new StubEndpoints());
        JSInterop.Mode = JSRuntimeMode.Loose;
    }

    private IRenderedComponent<RealtimeChatPanel> Render(bool enabled, Action<RealtimeChatPanel.KnowledgeSuggestion>? onSuggest = null)
        => RenderComponent<RealtimeChatPanel>(p => p
            .Add(c => c.SubjectType, "Shop")
            .Add(c => c.SubjectId, SubjectId)
            .Add(c => c.CurrentUserId, OwnerId)
            .Add(c => c.StaffToken, "staff-jwt")
            .Add(c => c.KnowledgeSuggestEnabled, enabled)
            .Add(c => c.OnKnowledgeSuggest, onSuggest is null
                ? EventCallback<RealtimeChatPanel.KnowledgeSuggestion>.Empty
                : EventCallback.Factory.Create<RealtimeChatPanel.KnowledgeSuggestion>(this, onSuggest)));

    [Fact]
    public void SuggestButton_RendersOnlyOnOwnMessage_WhenEnabled()
    {
        var cut = Render(true);
        cut.WaitForAssertion(() => cut.FindAll("button").Should().NotBeEmpty());

        var buttons = cut.FindAll("button[aria-label='Đề xuất vào tri thức AI']");
        buttons.Should().HaveCount(1); // only the owner's message, not the customer's
    }

    [Fact]
    public void SuggestButton_NotRendered_WhenDisabled()
    {
        var cut = Render(false);
        cut.WaitForAssertion(() => cut.Markup.Should().NotBeNullOrEmpty());
        cut.FindAll("button[aria-label='Đề xuất vào tri thức AI']").Should().BeEmpty();
    }

    [Fact]
    public void SuggestButton_Click_FiresCallback_WithVerbatimPair()
    {
        RealtimeChatPanel.KnowledgeSuggestion? got = null;
        var cut = Render(true, s => got = s);

        cut.WaitForAssertion(() =>
            cut.FindAll("button[aria-label='Đề xuất vào tri thức AI']").Should().HaveCount(1));
        var btn = cut.Find("button[aria-label='Đề xuất vào tri thức AI']");
        System.IO.File.WriteAllText(@"C:\VibeCoding\Gemini_Windsurf\.tmp-rv\suggest-btn.html", btn.OuterHtml);
        btn.Click();

        got.Should().NotBeNull();
        got!.Question.Should().Be("câu hỏi của khách");
        got.Answer.Should().Be("câu trả lời của chủ quán");
        got.MessageId.Should().Be(RealtimeChatPanelKnowledgeSuggestFixture.OwnerMessageId);
    }

    private static class RealtimeChatPanelKnowledgeSuggestFixture
    {
        public static readonly Guid OwnerMessageId = Guid.NewGuid();

        public static RealtimeChatHistoryResult History() => new()
        {
            Success = true,
            ConversationId = Guid.NewGuid(),
            Messages = new List<RealtimeChatMessage>
            {
                new() { Id = Guid.NewGuid(), SenderId = CustomerId, Content = "câu hỏi của khách", SentAt = DateTime.UtcNow.AddMinutes(-2) },
                new() { Id = OwnerMessageId, SenderId = OwnerId, Content = "câu trả lời của chủ quán", SentAt = DateTime.UtcNow.AddMinutes(-1) },
            }
        };
    }

    private sealed class FakeChatClient : IRealtimeChatClient
    {
        public Task<RealtimeChatHistoryResult> GetHistoryAsync(string subjectType, Guid subjectId,
            string? customerToken, Guid? customerDeviceId, int take = 100, string? staffToken = null,
            Guid? peerId = null, CancellationToken ct = default)
            => Task.FromResult(RealtimeChatPanelKnowledgeSuggestFixture.History());

        public Task<RealtimeSendResult> SendMessageAsync(string subjectType, Guid subjectId, string content,
            string? customerToken, Guid? customerDeviceId, string? staffToken = null,
            Guid? peerId = null, CancellationToken ct = default)
            => Task.FromResult(new RealtimeSendResult { Success = true });
    }

    private sealed class StubEndpoints : IRealtimeEndpointProvider
    {
        public string GetGatewayBaseUrl() => "http://localhost";
        public string GetMessagingHubUrl() => "http://localhost/hubs/messaging";
        public string GetTrackingHubUrl() => "http://localhost/hubs/tracking";
    }
}
