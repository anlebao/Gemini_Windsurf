using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging.Abstractions;
using VanAn.TenantAi.Ai;
using VanAn.TenantAi.Api.Services;
using VanAn.TenantAi.Domain;
using VanAn.TenantAi.Infra;
using Xunit;

namespace VanAn.TenantAi.Tests;

/// <summary>
/// W3f — chat learning suggest integration tests (InMemory host + FakeLlm):
/// gom câu hỏi NoData → cluster → FAQ draft → KnowledgeDocument PendingReview
/// với SourceRef "chat-learn:". Human-in-the-loop: không bao giờ auto-approve.
/// </summary>
public sealed class ChatLearningSuggestServiceTests : IDisposable
{
    private static readonly Guid TenantA = TenantAiApiFactory.TenantA;
    private static readonly Guid TenantB = TenantAiApiFactory.TenantB;

    private readonly FakeLlmChatClient _llm;
    private readonly TenantAiApiFactory _factory;

    public ChatLearningSuggestServiceTests()
    {
        _llm = new FakeLlmChatClient
        {
            // Echo câu hỏi (dòng 3 của prompt) vào title — fake đọc được prompt để
            // mỗi tenant nhận đúng draft của câu hỏi riêng mình.
            Responder = (prompt, _) =>
            {
                var lines = prompt.Split('\n', StringSplitOptions.RemoveEmptyEntries);
                var question = lines.Length >= 3 ? lines[2].Trim() : "Câu hỏi?";
                return new LlmChatResult(
                    $$"""{"title":"{{question}}","content":"Câu trả lời cần chủ shop xác nhận."}""",
                    10, 4);
            }
        };
        _factory = TenantAiApiFactory.WithServices(s =>
        {
            s.RemoveAll<ILlmChatClient>();
            s.AddSingleton<ILlmChatClient>(_llm);
            s.RemoveAll<IEmbeddingProvider>();
            s.AddSingleton<IEmbeddingProvider>(new FakeEmbeddingProvider());
        });
        _ = _factory.CreateClient(); // force host build (InMemory DB shared by name)
    }

    public void Dispose() => _factory.Dispose();

    // ---------- helpers ----------

    private ChatLearningSuggestService Service(Dictionary<string, string?>? settings = null)
    {
        var configValues = new Dictionary<string, string?>
        {
            ["Learning:Enabled"] = "true",
            ["Learning:MinClusterSize"] = "3",
            ["Learning:SimilarityThreshold"] = "0.85",
            ["Learning:MaxSuggestionsPerTenant"] = "5",
            ["Learning:MaxPendingPerTenant"] = "10",
            ["Learning:LookbackDays"] = "7"
        };
        if (settings != null)
            foreach (var kv in settings)
                configValues[kv.Key] = kv.Value;
        var config = new ConfigurationBuilder().AddInMemoryCollection(configValues).Build();
        return new ChatLearningSuggestService(_factory.Services, config,
            NullLogger<ChatLearningSuggestService>.Instance);
    }

    private async Task Scoped(Func<TenantAiDbContext, Task> f)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        await f(scope.ServiceProvider.GetRequiredService<TenantAiDbContext>());
    }

    private async Task<T> Scoped<T>(Func<TenantAiDbContext, Task<T>> f)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        return await f(scope.ServiceProvider.GetRequiredService<TenantAiDbContext>());
    }

    private async Task SeedNoDataTurnAsync(Guid tenant, string sessionId, string question,
        string intent = ChatIntents.Faq, string channel = "khachlink_storefront",
        bool refused = false)
        => await Scoped(db =>
        {
            db.ConversationSessions.Add(ConversationSession.Create(tenant, sessionId, channel));
            var msgId = "m-" + Guid.NewGuid().ToString("N")[..8];
            var expires = DateTime.UtcNow.AddDays(90);
            db.ConversationMessages.Add(ConversationMessage.Create(
                tenant, sessionId, msgId, MessageRole.User, question, expires));
            var assistant = ConversationMessage.Create(tenant, sessionId, msgId + ":reply",
                MessageRole.Assistant, ChatTemplates.NoData, expires);
            assistant.SetAnswerMeta(intent, null, null, 0, refused, refused ? "out_of_scope" : null, false, 10, 4);
            db.ConversationMessages.Add(assistant);
            return db.SaveChangesAsync();
        });

    private async Task SeedPendingChatLearnDocAsync(Guid tenant, string suffix)
        => await Scoped(db =>
        {
            db.KnowledgeDocuments.Add(KnowledgeDocument.Create(tenant, "faq", "T" + suffix, "C",
                DocumentClassification.Internal, sourceRef: $"{ChatLearningSuggestService.SourceRefPrefix}{tenant:N}:{suffix}"));
            return db.SaveChangesAsync();
        });

    private Task<List<KnowledgeDocument>> ChatLearnDocsAsync(Guid tenant)
        => Scoped(db => db.KnowledgeDocuments.AsNoTracking()
            .Where(d => d.TenantId == tenant
                && d.SourceRef != null && d.SourceRef.StartsWith(ChatLearningSuggestService.SourceRefPrefix))
            .ToListAsync());

    private Task<int> ChatLearnDocCountAsync(Guid tenant)
        => Scoped(db => db.KnowledgeDocuments.AsNoTracking()
            .CountAsync(d => d.TenantId == tenant
                && d.SourceRef != null && d.SourceRef.StartsWith(ChatLearningSuggestService.SourceRefPrefix)));

    // ---------- core behavior ----------

    [Fact]
    public async Task Run_CreatesPendingDoc_ForFrequentUnansweredQuestion()
    {
        await SeedNoDataTurnAsync(TenantA, "s1", "Quán mở cửa mấy giờ?");
        await SeedNoDataTurnAsync(TenantA, "s2", "quán mở cửa mấy giờ");
        await SeedNoDataTurnAsync(TenantA, "s3", "quán mở cửa mấy giờ ạ");

        var result = await Service().RunAsync(TenantA);

        result.SuggestedCreated.Should().Be(1);
        var docs = await ChatLearnDocsAsync(TenantA);
        var doc = docs.Should().ContainSingle().Subject;
        doc.Type.Should().Be("faq");
        doc.Classification.Should().Be(DocumentClassification.Internal); // owner picks at approve
        doc.ApprovalStatus.Should().Be(ApprovalStatus.PendingReview);   // never auto-approve
        doc.SourceRef!.Should().StartWith(ChatLearningSuggestService.SourceRefPrefix + TenantA.ToString("N") + ":");
        doc.Title.Should().Be("Quán mở cửa mấy giờ?");
        doc.Content.Should().Contain("chủ shop xác nhận");
    }

    [Fact]
    public async Task Run_BelowMinClusterSize_NoDoc()
    {
        await SeedNoDataTurnAsync(TenantA, "s1", "Quán mở cửa mấy giờ?");
        await SeedNoDataTurnAsync(TenantA, "s2", "quán mở cửa mấy giờ");

        var result = await Service().RunAsync(TenantA);

        result.SuggestedCreated.Should().Be(0);
        (await ChatLearnDocCountAsync(TenantA)).Should().Be(0);
    }

    [Fact]
    public async Task Run_Idempotent_SecondRun_NoDuplicate()
    {
        await SeedNoDataTurnAsync(TenantA, "s1", "Quán mở cửa mấy giờ?");
        await SeedNoDataTurnAsync(TenantA, "s2", "quán mở cửa mấy giờ");
        await SeedNoDataTurnAsync(TenantA, "s3", "quán mở cửa mấy giờ ạ");

        var svc = Service();
        await svc.RunAsync(TenantA);
        await svc.RunAsync(TenantA);

        (await ChatLearnDocCountAsync(TenantA)).Should().Be(1);
    }

    [Fact]
    public async Task Run_RespectsMaxSuggestionsPerTenant()
    {
        await SeedNoDataTurnAsync(TenantA, "s1", "Quán mở cửa mấy giờ?");
        await SeedNoDataTurnAsync(TenantA, "s2", "quán mở cửa mấy giờ");
        await SeedNoDataTurnAsync(TenantA, "s3", "quán mở cửa mấy giờ ạ");
        await SeedNoDataTurnAsync(TenantA, "s4", "Quán có giao hàng không?");
        await SeedNoDataTurnAsync(TenantA, "s5", "quán có giao hàng không");
        await SeedNoDataTurnAsync(TenantA, "s6", "quán có giao hàng không ạ");
        await SeedNoDataTurnAsync(TenantA, "s7", "Giá phở chay bao nhiêu?");
        await SeedNoDataTurnAsync(TenantA, "s8", "giá phở chay bao nhiêu");
        await SeedNoDataTurnAsync(TenantA, "s9", "giá phở chay bao nhiêu ạ");

        var result = await Service(new Dictionary<string, string?> { ["Learning:MaxSuggestionsPerTenant"] = "2" })
            .RunAsync(TenantA);

        result.SuggestedCreated.Should().Be(2);
        (await ChatLearnDocCountAsync(TenantA)).Should().Be(2);
    }

    [Fact]
    public async Task Run_RespectsMaxPendingPerTenant()
    {
        for (var i = 0; i < 10; i++)
            await SeedPendingChatLearnDocAsync(TenantA, "p" + i);
        await SeedNoDataTurnAsync(TenantA, "s1", "Quán mở cửa mấy giờ?");
        await SeedNoDataTurnAsync(TenantA, "s2", "quán mở cửa mấy giờ");
        await SeedNoDataTurnAsync(TenantA, "s3", "quán mở cửa mấy giờ ạ");

        var result = await Service().RunAsync(TenantA);

        result.SuggestedCreated.Should().Be(0);
        (await ChatLearnDocCountAsync(TenantA)).Should().Be(10);
    }

    [Fact]
    public async Task Run_LlmDisabled_NoDocs_NoCrash()
    {
        _llm.Enabled = false;
        await SeedNoDataTurnAsync(TenantA, "s1", "Quán mở cửa mấy giờ?");
        await SeedNoDataTurnAsync(TenantA, "s2", "quán mở cửa mấy giờ");
        await SeedNoDataTurnAsync(TenantA, "s3", "quán mở cửa mấy giờ ạ");

        var result = await Service().RunAsync(TenantA);

        result.SkippedNoLlm.Should().BeGreaterThan(0);
        (await ChatLearnDocCountAsync(TenantA)).Should().Be(0);
    }

    [Fact]
    public async Task Run_LlmFailure_NoCrash_Skips()
    {
        _llm.Responder = (_, _) => null; // provider failed → no draft
        await SeedNoDataTurnAsync(TenantA, "s1", "Quán mở cửa mấy giờ?");
        await SeedNoDataTurnAsync(TenantA, "s2", "quán mở cửa mấy giờ");
        await SeedNoDataTurnAsync(TenantA, "s3", "quán mở cửa mấy giờ ạ");

        var result = await Service().RunAsync(TenantA);

        result.SuggestedCreated.Should().Be(0);
        (await ChatLearnDocCountAsync(TenantA)).Should().Be(0);
    }

    [Fact]
    public async Task Run_ExcludesChitChat_AndRefusedTurns()
    {
        await SeedNoDataTurnAsync(TenantA, "s1", "xin chào", intent: ChatIntents.ChitChat);
        await SeedNoDataTurnAsync(TenantA, "s2", "xin chào", intent: ChatIntents.ChitChat);
        await SeedNoDataTurnAsync(TenantA, "s3", "xin chào", intent: ChatIntents.ChitChat);
        await SeedNoDataTurnAsync(TenantA, "s4", "Quán mở cửa mấy giờ?", refused: true);
        await SeedNoDataTurnAsync(TenantA, "s5", "quán mở cửa mấy giờ", refused: true);
        await SeedNoDataTurnAsync(TenantA, "s6", "quán mở cửa mấy giờ ạ", refused: true);

        var result = await Service().RunAsync(TenantA);

        result.SuggestedCreated.Should().Be(0);
        (await ChatLearnDocCountAsync(TenantA)).Should().Be(0);
    }

    [Fact]
    public async Task Run_ExcludesPreviewChannel()
    {
        await SeedNoDataTurnAsync(TenantA, "s1", "Quán mở cửa mấy giờ?", channel: "preview");
        await SeedNoDataTurnAsync(TenantA, "s2", "quán mở cửa mấy giờ", channel: "preview");
        await SeedNoDataTurnAsync(TenantA, "s3", "quán mở cửa mấy giờ ạ", channel: "preview");

        var result = await Service().RunAsync(TenantA);

        result.SuggestedCreated.Should().Be(0);
        (await ChatLearnDocCountAsync(TenantA)).Should().Be(0);
    }

    [Fact]
    public async Task Run_FiltersGarbageQuestions()
    {
        await SeedNoDataTurnAsync(TenantA, "s1", "abc");
        await SeedNoDataTurnAsync(TenantA, "s2", "abc");
        await SeedNoDataTurnAsync(TenantA, "s3", "abc");

        var result = await Service().RunAsync(TenantA);

        result.SuggestedCreated.Should().Be(0);
        (await ChatLearnDocCountAsync(TenantA)).Should().Be(0);
    }

    [Fact]
    public async Task Run_PerTenantIsolation_BothGetOwnDocs()
    {
        await SeedNoDataTurnAsync(TenantA, "s1", "Quán mở cửa mấy giờ?");
        await SeedNoDataTurnAsync(TenantA, "s2", "quán mở cửa mấy giờ");
        await SeedNoDataTurnAsync(TenantA, "s3", "quán mở cửa mấy giờ ạ");
        await SeedNoDataTurnAsync(TenantB, "s1", "Quán có giao hàng không?");
        await SeedNoDataTurnAsync(TenantB, "s2", "quán có giao hàng không");
        await SeedNoDataTurnAsync(TenantB, "s3", "quán có giao hàng không ạ");

        var result = await Service().RunAsync(tenantId: null);

        result.TenantsScanned.Should().Be(2);
        var docsA = await ChatLearnDocsAsync(TenantA);
        var docsB = await ChatLearnDocsAsync(TenantB);
        docsA.Should().ContainSingle();
        docsB.Should().ContainSingle();
        docsA[0].TenantId.Should().Be(TenantA);
        docsB[0].TenantId.Should().Be(TenantB);
        docsA[0].Title.Should().Contain("mở cửa");
        docsB[0].Title.Should().Contain("giao hàng");
    }

    [Fact]
    public async Task Run_Disabled_ReturnsZero()
    {
        await SeedNoDataTurnAsync(TenantA, "s1", "Quán mở cửa mấy giờ?");
        await SeedNoDataTurnAsync(TenantA, "s2", "quán mở cửa mấy giờ");
        await SeedNoDataTurnAsync(TenantA, "s3", "quán mở cửa mấy giờ ạ");

        var result = await Service(new Dictionary<string, string?> { ["Learning:Enabled"] = "false" })
            .RunAsync(TenantA);

        result.SuggestedCreated.Should().Be(0);
        (await ChatLearnDocCountAsync(TenantA)).Should().Be(0);
    }
}