using Bunit;
using Bunit.JSInterop;
using FluentAssertions;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using VanAn.UI.Platform.Adapters;
using VanAn.UI.Platform.Components.Ai;
using VanAn.UI.Platform.Components.Realtime;
using VanAn.UI.Platform.Core.Interfaces;
using VanAn.UI.Platform.Realtime;
using Xunit;

namespace VanAn.Core.Tests.TenantAi;

/// <summary>
/// Task AI-P4 — AiChatWidget bUnit tests: render gate (aiEnabled), product card →
/// cart callback, fallback khi AI down.
/// </summary>
public class AiChatWidgetTests : TestContext
{
    private static readonly Guid TenantId = Guid.NewGuid();

    public AiChatWidgetTests()
    {
        Services.AddSingleton<ICssAdapter, BootstrapAdapter>();
        JSInterop.Mode = JSRuntimeMode.Loose; // localStorage + scrollToBottom → default
    }

    private sealed class FakeAiChatClient : IAiChatClient
    {
        public AiWidgetConfig? Config { get; set; } =
            new(true, "Bé AI", "Chào bạn!", ["Menu hôm nay?"]);
        public AiChatResult? ChatResult { get; set; }
        public int SendCalls { get; private set; }
        public string? LastSessionId { get; private set; }
        public string? LastMessageId { get; private set; }
        public string? LastText { get; private set; }

        public Task<AiWidgetConfig?> GetWidgetConfigAsync(Guid tenantId, CancellationToken ct = default)
            => Task.FromResult(Config);

        public Task<IReadOnlyList<AiSessionMessage>?> GetSessionMessagesAsync(Guid tenantId,
            string sessionId, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<AiSessionMessage>?>([]);

        public Task<AiChatResult?> SendChatAsync(Guid tenantId, string sessionId, string messageId,
            string text, string? locale = null, CancellationToken ct = default)
        {
            SendCalls++;
            LastSessionId = sessionId;
            LastMessageId = messageId;
            LastText = text;
            return Task.FromResult(ChatResult);
        }

        public Task<AiChatResult?> SearchAiAsync(Guid tenantId, string query, CancellationToken ct = default)
            => Task.FromResult<AiChatResult?>(null);
    }

    private IRenderedComponent<AiChatWidget> Render(FakeAiChatClient client, Action<AiProductCardItem>? onAddToCart = null)
    {
        Services.AddSingleton<IAiChatClient>(client);
        return RenderComponent<AiChatWidget>(p => p
            .Add(w => w.TenantId, TenantId)
            .Add(w => w.OnAddToCart, onAddToCart ?? (_ => { })));
    }

    [Fact]
    public void Widget_RendersBubble_WhenAiEnabled()
    {
        var cut = Render(new FakeAiChatClient());
        cut.WaitForAssertion(() =>
            cut.Find("button.vanan-ai-bubble-btn").Should().NotBeNull());
    }

    [Fact]
    public void Widget_RendersNothing_WhenAiDisabled()
    {
        var cut = Render(new FakeAiChatClient { Config = new AiWidgetConfig(false, null, null, null) });
        cut.WaitForAssertion(() =>
            cut.Markup.Should().NotContain("vanan-ai-bubble-btn"));
    }

    [Fact]
    public void Widget_RendersNothing_WhenServiceDown_NullConfig()
    {
        var cut = Render(new FakeAiChatClient { Config = null });
        cut.WaitForAssertion(() =>
            cut.Markup.Should().NotContain("vanan-ai-bubble-btn"));
    }

    [Fact]
    public void Open_ShowsPanel_GreetingAndSuggestions()
    {
        var cut = Render(new FakeAiChatClient());
        cut.WaitForAssertion(() => cut.Find("button.vanan-ai-bubble-btn").Click());
        cut.WaitForAssertion(() =>
        {
            cut.Markup.Should().Contain("Bé AI");
            cut.Markup.Should().Contain("Chào bạn!");
            cut.Markup.Should().Contain("Menu hôm nay?");
        });
    }

    [Fact]
    public void Send_AppendsUserAndAssistantMessages()
    {
        var client = new FakeAiChatClient
        {
            ChatResult = new AiChatResult("Quán có phở chay 80k.", [], [], 0.9, false, null, false, false)
        };
        var cut = Render(client);
        cut.WaitForAssertion(() => cut.Find("button.vanan-ai-bubble-btn").Click());
        cut.WaitForAssertion(() => cut.Find(".vanan-ai-panel"));

        cut.Find(".vanan-ai-panel input").Input("Quán có phở chay không?");
        cut.Find("button.vanan-ai-send").Click();

        cut.WaitForAssertion(() =>
        {
            cut.Markup.Should().Contain("Quán có phở chay không?");
            cut.Markup.Should().Contain("Quán có phở chay 80k.");
        });
        client.SendCalls.Should().Be(1);
        client.LastText.Should().Be("Quán có phở chay không?");
        client.LastSessionId.Should().NotBeNullOrEmpty();
        client.LastMessageId.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void ProductCard_AddToCart_InvokesCallbackWithSnapshot()
    {
        var product = new AiProductCardItem(Guid.NewGuid(), "Phở chay", 80_000m, null);
        var client = new FakeAiChatClient
        {
            ChatResult = new AiChatResult("Gợi ý:", [product], ["product:1"], 0.9, false, null, false, false)
        };
        AiProductCardItem? added = null;
        var cut = Render(client, p => added = p);
        cut.WaitForAssertion(() => cut.Find("button.vanan-ai-bubble-btn").Click());
        cut.WaitForAssertion(() => cut.Find(".vanan-ai-panel"));

        cut.Find(".vanan-ai-panel input").Input("Có món chay không?");
        cut.Find("button.vanan-ai-send").Click();

        cut.WaitForAssertion(() => cut.Find(".vanan-ai-product-card"));
        cut.Markup.Should().Contain("Phở chay");
        cut.Markup.Should().Contain("80.000");

        cut.Find(".vanan-ai-product-card button").Click();
        cut.WaitForAssertion(() => added.Should().NotBeNull());
        added!.ProductId.Should().Be(product.ProductId);
        added.Name.Should().Be("Phở chay");
        added.Price.Should().Be(80_000m);
        cut.Markup.Should().Contain("Đã thêm");
    }

    [Fact]
    public void Send_ClientError_ShowsFallbackMessage()
    {
        var client = new FakeAiChatClient { ChatResult = null }; // transport error → null
        var cut = Render(client);
        cut.WaitForAssertion(() => cut.Find("button.vanan-ai-bubble-btn").Click());
        cut.WaitForAssertion(() => cut.Find(".vanan-ai-panel"));

        cut.Find(".vanan-ai-panel input").Input("test");
        cut.Find("button.vanan-ai-send").Click();

        cut.WaitForAssertion(() =>
            cut.Markup.Should().Contain("Trợ lý AI tạm nghỉ"));
    }

    // === Task AI-P8 — unified widget: AI-first + human handoff ===

    private IRenderedComponent<AiChatWidget> RenderUnified(
        FakeAiChatClient client,
        Action<string?>? onDraft = null,
        bool humanAvailable = true,
        RenderFragment? humanContent = null)
    {
        Services.AddSingleton<IAiChatClient>(client);
        return RenderComponent<AiChatWidget>(p => p
            .Add(w => w.TenantId, TenantId)
            .Add(w => w.HumanChatAvailable, humanAvailable)
            .Add(w => w.OnDraftRequest, onDraft ?? (_ => { }))
            .Add(w => w.HumanContent, humanContent ?? (builder => builder.AddContent(0, "HUMAN-PANEL"))));
    }

    [Fact]
    public void Handoff_ButtonShown_SwitchesToHumanMode()
    {
        var cut = RenderUnified(new FakeAiChatClient());
        cut.WaitForAssertion(() => cut.Find("button.vanan-ai-bubble-btn").Click());
        cut.WaitForAssertion(() => cut.Find(".vanan-ai-panel"));

        cut.Find("button.vanan-ai-human-btn").Click();

        cut.WaitForAssertion(() =>
        {
            cut.Markup.Should().Contain("Chủ quán");
            cut.Markup.Should().Contain("HUMAN-PANEL");
            cut.Markup.Should().NotContain("vanan-ai-human-btn");
            cut.Find("button.vanan-ai-back-btn").Should().NotBeNull();
        });
    }

    [Fact]
    public void Handoff_BackToAi_KeepsAiHistory()
    {
        var client = new FakeAiChatClient
        {
            ChatResult = new AiChatResult("Câu trả lời AI.", [], [], 0.9, false, null, false, false)
        };
        var cut = RenderUnified(client);
        cut.WaitForAssertion(() => cut.Find("button.vanan-ai-bubble-btn").Click());
        cut.WaitForAssertion(() => cut.Find(".vanan-ai-panel"));
        cut.Find(".vanan-ai-panel input").Input("Có phở chay không?");
        cut.Find("button.vanan-ai-send").Click();
        cut.WaitForAssertion(() => cut.Markup.Should().Contain("Câu trả lời AI."));

        cut.Find("button.vanan-ai-human-btn").Click();
        cut.WaitForAssertion(() => cut.Find("button.vanan-ai-back-btn"));
        cut.Find("button.vanan-ai-back-btn").Click();

        cut.WaitForAssertion(() =>
        {
            cut.Markup.Should().Contain("Câu trả lời AI.");
            cut.Markup.Should().Contain("Có phở chay không?");
        });
    }

    [Fact]
    public void Handoff_RefusedMessage_ChipEscalatesWithUserText()
    {
        var client = new FakeAiChatClient
        {
            ChatResult = new AiChatResult("Xin lỗi, mình không biết.", [], [], 0.1, true, "out_of_scope", false, false)
        };
        string? draft = "UNSET";
        var cut = RenderUnified(client, d => draft = d);
        cut.WaitForAssertion(() => cut.Find("button.vanan-ai-bubble-btn").Click());
        cut.WaitForAssertion(() => cut.Find(".vanan-ai-panel"));

        cut.Find(".vanan-ai-panel input").Input("Quán có nhận order 200 phần không?");
        cut.Find("button.vanan-ai-send").Click();

        cut.WaitForAssertion(() => cut.Find("button.vanan-ai-escalate"));
        cut.Find("button.vanan-ai-escalate").Click();

        cut.WaitForAssertion(() =>
        {
            cut.Markup.Should().Contain("HUMAN-PANEL");
            cut.Markup.Should().Contain("Chủ quán");
        });
        draft.Should().Be("Quán có nhận order 200 phần không?");
    }

    [Fact]
    public void Handoff_RefusedChip_DraftTruncatedTo500()
    {
        var client = new FakeAiChatClient
        {
            ChatResult = new AiChatResult("Từ chối.", [], [], 0.1, true, "out_of_scope", false, false)
        };
        string? draft = null;
        var cut = RenderUnified(client, d => draft = d);
        cut.WaitForAssertion(() => cut.Find("button.vanan-ai-bubble-btn").Click());
        cut.WaitForAssertion(() => cut.Find(".vanan-ai-panel"));

        var longText = new string('x', 800);
        cut.Find(".vanan-ai-panel input").Input(longText);
        cut.Find("button.vanan-ai-send").Click();

        cut.WaitForAssertion(() => cut.Find("button.vanan-ai-escalate").Click());
        cut.WaitForAssertion(() => draft.Should().HaveLength(500));
    }

    [Fact]
    public void Handoff_Disabled_NoButtonsNoChips()
    {
        var client = new FakeAiChatClient
        {
            ChatResult = new AiChatResult("Từ chối.", [], [], 0.1, true, "out_of_scope", false, false)
        };
        var cut = RenderUnified(client, humanAvailable: false);
        cut.WaitForAssertion(() => cut.Find("button.vanan-ai-bubble-btn").Click());
        cut.WaitForAssertion(() => cut.Find(".vanan-ai-panel"));

        cut.Find(".vanan-ai-panel input").Input("test refuse");
        cut.Find("button.vanan-ai-send").Click();

        cut.WaitForAssertion(() =>
        {
            cut.Markup.Should().Contain("Từ chối.");
            cut.Markup.Should().NotContain("vanan-ai-human-btn");
            cut.Markup.Should().NotContain("vanan-ai-escalate");
            cut.Markup.Should().NotContain("HUMAN-PANEL");
        });
    }

    [Fact]
    public void Handoff_AiDisabled_OpensStraightToHumanMode()
    {
        var client = new FakeAiChatClient { Config = new AiWidgetConfig(false, null, null, null) };
        var cut = RenderUnified(client);

        cut.WaitForAssertion(() => cut.Find("button.vanan-ai-bubble-btn").Click());
        cut.WaitForAssertion(() =>
        {
            cut.Markup.Should().Contain("Chủ quán");
            cut.Markup.Should().Contain("HUMAN-PANEL");
            cut.Markup.Should().NotContain("vanan-ai-back-btn"); // no AI to go back to
        });
    }

    [Fact]
    public void Handoff_AiDown_NullConfig_StillShowsHumanBubble()
    {
        var client = new FakeAiChatClient { Config = null }; // service down
        var cut = RenderUnified(client);

        cut.WaitForAssertion(() => cut.Find("button.vanan-ai-bubble-btn").Click());
        cut.WaitForAssertion(() => cut.Markup.Should().Contain("HUMAN-PANEL"));
    }

    [Fact]
    public void Handoff_EscalateToRealtimePanel_PrefillsDraft()
    {
        // Store-level flow: refuse CTA → OnDraftRequest → host passes draft to
        // RealtimeChatPanel.InitialDraft → input prefilled for the customer.
        Services.AddSingleton<IRealtimeChatClient>(new FakeRealtimeChatClient());
        Services.AddSingleton<IRealtimeEndpointProvider>(new StubEndpoints());
        var subjectId = Guid.NewGuid();
        string? draft = null;

        RenderFragment humanContent = builder =>
        {
            builder.OpenComponent<RealtimeChatPanel>(0);
            builder.AddAttribute(1, "SubjectType", "Shop");
            builder.AddAttribute(2, "SubjectId", subjectId);
            builder.AddAttribute(3, "InitialDraft", draft);
            builder.CloseComponent();
        };

        var client = new FakeAiChatClient
        {
            ChatResult = new AiChatResult("Mình không trả lời được câu này.", [], [], 0.1, true, "out_of_scope", false, false)
        };
        var cut = RenderUnified(client, d => draft = d, humanContent: humanContent);
        cut.WaitForAssertion(() => cut.Find("button.vanan-ai-bubble-btn").Click());
        cut.WaitForAssertion(() => cut.Find(".vanan-ai-panel"));

        cut.Find(".vanan-ai-panel input").Input("Có giao hàng tận nơi không?");
        cut.Find("button.vanan-ai-send").Click();
        cut.WaitForAssertion(() => cut.Find("button.vanan-ai-escalate").Click());

        cut.WaitForAssertion(() =>
        {
            cut.Markup.Should().Contain("chat-panel");
            draft.Should().Be("Có giao hàng tận nơi không?");
            cut.Find(".chat-panel input").GetAttribute("value")
                .Should().Be("Có giao hàng tận nơi không?");
        });
    }

    // === SF-W1 — PendingQuestion / unread badge / OnHumanOpened ===

    private IRenderedComponent<AiChatWidget> RenderW1(
        FakeAiChatClient client,
        string? pendingQuestion = null,
        Action<string?>? onPendingChanged = null,
        int unread = 0,
        Action? onHumanOpened = null)
    {
        Services.AddSingleton<IAiChatClient>(client);
        return RenderComponent<AiChatWidget>(p => p
            .Add(w => w.TenantId, TenantId)
            .Add(w => w.PendingQuestion, pendingQuestion)
            .Add(w => w.PendingQuestionChanged, onPendingChanged ?? (_ => { }))
            .Add(w => w.HumanUnreadCount, unread)
            .Add(w => w.OnHumanOpened, onHumanOpened ?? (() => { })));
    }

    [Fact]
    public void PendingQuestion_OpensPanel_AutoSends_Once()
    {
        var client = new FakeAiChatClient
        {
            ChatResult = new AiChatResult("Còn slot 3h chiều.", [], [], 0.9, false, null, false, false)
        };
        string? bound = "UNSET";
        var cut = RenderW1(client, "Còn lịch trống chiều nay?", v => bound = v);

        cut.WaitForAssertion(() =>
        {
            client.SendCalls.Should().Be(1);
            client.LastText.Should().Be("Còn lịch trống chiều nay?");
            bound.Should().BeNull(); // PendingQuestionChanged fired → host clears
            cut.Markup.Should().Contain("vanan-ai-panel");
            cut.Markup.Should().Contain("Còn lịch trống chiều nay?");
        });
    }

    [Fact]
    public void PendingQuestion_AiDisabled_NeverSends()
    {
        var client = new FakeAiChatClient { Config = new AiWidgetConfig(false, null, null, null) };
        var cut = RenderW1(client, "test question");
        // Give the component a beat — must NOT send (no session in human-only mode).
        cut.WaitForAssertion(() => cut.Markup.Should().BeEmpty());
        client.SendCalls.Should().Be(0);
    }

    [Fact]
    public void UnreadBadge_Renders_WhenPositive()
    {
        var cut = RenderW1(new FakeAiChatClient(), unread: 3);
        cut.WaitForAssertion(() =>
            cut.Find(".vanan-ai-unread").TextContent.Trim().Should().Be("3"));
    }

    [Fact]
    public void UnreadBadge_Hidden_WhenZero()
    {
        var cut = RenderW1(new FakeAiChatClient(), unread: 0);
        cut.WaitForAssertion(() => cut.Find("button.vanan-ai-bubble-btn").Should().NotBeNull());
        cut.FindAll(".vanan-ai-unread").Should().BeEmpty();
    }

    [Fact]
    public void UnreadBadge_Capped9Plus()
    {
        var cut = RenderW1(new FakeAiChatClient(), unread: 12);
        cut.WaitForAssertion(() =>
            cut.Find(".vanan-ai-unread").TextContent.Trim().Should().Be("9+"));
    }

    [Fact]
    public void OnHumanOpened_Fired_WhenSwitchingToHuman()
    {
        var calls = 0;
        Services.AddSingleton<IAiChatClient>(new FakeAiChatClient());
        var cut = RenderComponent<AiChatWidget>(p => p
            .Add(w => w.TenantId, TenantId)
            .Add(w => w.HumanChatAvailable, true)
            .Add(w => w.OnHumanOpened, () => calls++)
            .Add(w => w.HumanContent, builder => builder.AddContent(0, "HUMAN")));

        cut.WaitForAssertion(() => cut.Find("button.vanan-ai-bubble-btn").Click());
        cut.WaitForAssertion(() => cut.Find("button.vanan-ai-human-btn").Click());
        cut.WaitForAssertion(() => calls.Should().Be(1));
    }

    // === SF-W3 — voice input (nói thay vì gõ) ===

    private IRenderedComponent<AiChatWidget> RenderVoice(
        FakeAiChatClient client, bool voiceEnabled, bool speechSupported)
    {
        Services.AddSingleton<IAiChatClient>(client);
        if (speechSupported)
            JSInterop.Setup<bool>("vananVoiceInput.isSupported").SetResult(true);
        return RenderComponent<AiChatWidget>(p => p
            .Add(w => w.TenantId, TenantId)
            .Add(w => w.VoiceEnabled, voiceEnabled)
            .Add(w => w.OnAddToCart, _ => { }));
    }

    [Fact]
    public void VoiceMic_Hidden_WhenVoiceDisabled()
    {
        var cut = RenderVoice(new FakeAiChatClient(), voiceEnabled: false, speechSupported: true);
        cut.WaitForAssertion(() => cut.Find("button.vanan-ai-bubble-btn").Click());
        cut.WaitForAssertion(() => cut.Find(".vanan-ai-panel"));

        cut.FindAll(".vanan-ai-mic").Should().BeEmpty("tenant flag off → mic hidden");
    }

    [Fact]
    public void VoiceMic_Hidden_WhenBrowserUnsupported()
    {
        // Loose JSInterop → isSupported returns default(false) — mic stays hidden.
        var cut = RenderVoice(new FakeAiChatClient(), voiceEnabled: true, speechSupported: false);
        cut.WaitForAssertion(() => cut.Find("button.vanan-ai-bubble-btn").Click());
        cut.WaitForAssertion(() => cut.Find(".vanan-ai-panel"));

        cut.FindAll(".vanan-ai-mic").Should().BeEmpty();
    }

    [Fact]
    public void VoiceMic_Renders_WhenEnabledAndSupported()
    {
        var cut = RenderVoice(new FakeAiChatClient(), voiceEnabled: true, speechSupported: true);
        cut.WaitForAssertion(() => cut.Find("button.vanan-ai-bubble-btn").Click());

        cut.WaitForAssertion(() => cut.Find("button.vanan-ai-mic").Should().NotBeNull());
    }

    [Fact]
    public async Task VoiceFinal_Transcript_AutoSends()
    {
        var client = new FakeAiChatClient
        {
            ChatResult = new AiChatResult("Dạ có ạ.", [], [], 0.9, false, null, false, false)
        };
        var cut = RenderVoice(client, voiceEnabled: true, speechSupported: true);
        cut.WaitForAssertion(() => cut.Find("button.vanan-ai-bubble-btn").Click());
        cut.WaitForAssertion(() => cut.Find(".vanan-ai-panel"));

        await cut.InvokeAsync(() => cut.Instance.OnVoiceFinal("phở chay bao nhiêu"));

        cut.WaitForAssertion(() => client.SendCalls.Should().Be(1));
        client.LastText.Should().Be("phở chay bao nhiêu");
        cut.WaitForAssertion(() => cut.Markup.Should().Contain("Dạ có ạ."));
    }

    [Fact]
    public async Task VoiceInterim_FillsInput_WithoutSending()
    {
        var client = new FakeAiChatClient();
        var cut = RenderVoice(client, voiceEnabled: true, speechSupported: true);
        cut.WaitForAssertion(() => cut.Find("button.vanan-ai-bubble-btn").Click());
        cut.WaitForAssertion(() => cut.Find(".vanan-ai-panel"));

        await cut.InvokeAsync(() => cut.Instance.OnVoiceInterim("phở"));

        cut.Find(".vanan-ai-panel input").GetAttribute("value").Should().Be("phở");
        client.SendCalls.Should().Be(0, "interim text must not send");
    }

    [Fact]
    public async Task Dispose_StopsActiveVoiceSession()
    {
        JSInterop.Setup<bool>("vananVoiceInput.start", _ => true).SetResult(true);
        JSInterop.SetupVoid("vananVoiceInput.stop", _ => true).SetVoidResult();
        var cut = RenderVoice(new FakeAiChatClient(), voiceEnabled: true, speechSupported: true);
        cut.WaitForAssertion(() => cut.Find("button.vanan-ai-bubble-btn").Click());
        cut.WaitForAssertion(() => cut.Find("button.vanan-ai-mic"));

        cut.Find("button.vanan-ai-mic").Click();
        cut.WaitForAssertion(() =>
            cut.Find("button.vanan-ai-mic").ClassList.Should().Contain("listening"));

        await cut.Instance.DisposeAsync();

        JSInterop.VerifyInvoke("vananVoiceInput.stop");
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
