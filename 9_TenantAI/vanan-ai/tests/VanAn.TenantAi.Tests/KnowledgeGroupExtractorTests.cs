using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using VanAn.TenantAi.Ai;
using VanAn.TenantAi.Api.Services;
using VanAn.TenantAi.Contracts;
using Xunit;

namespace VanAn.TenantAi.Tests;

/// <summary>W3c-multi — KnowledgeExtractor.ExtractGroupsAsync: 1 text → 4 nhóm
/// (menu/about/policy/promotion) chạy song song, per-group never-throw.</summary>
public sealed class KnowledgeGroupExtractorTests
{
    private static KnowledgeExtractor Extractor(FakeLlmChatClient? llm = null)
        => new(llm ?? new FakeLlmChatClient(), NullLogger<KnowledgeExtractor>.Instance);

    private static readonly string AllGroupsJson = """
        {"summary":"s","items":[{"kind":"product","title":"Item","content":"c"}]}
        """;

    [Fact]
    public async Task ExtractGroups_ReturnsAllFourGroups()
    {
        var llm = new FakeLlmChatClient { Responder = (_, _) => new LlmChatResult(AllGroupsJson, 10, 5) };
        var result = await Extractor(llm).ExtractGroupsAsync(new KnowledgeExtractRequest("full text"), default);

        result.Groups.Select(g => g.Key).Should().Equal("menu", "about", "policy", "promotion");
        result.Groups.Should().OnlyContain(g => g.Items.Count == 1);
        llm.Calls.Should().Be(4); // fan-out song song — 1 call/nhóm
    }

    [Fact]
    public async Task ExtractGroups_OneGroupFails_OthersStillReturn()
    {
        // Responder: nhóm promotion nhận prompt chứa "khuyến mãi" → trả null (LLM fail).
        var llm = new FakeLlmChatClient
        {
            // Chỉ prompt nhóm promotion có cụm này trong dòng "CHỈ trích:" —
            // các nhóm khác nhắc "khuyến mãi" trong mệnh đề loại trừ.
            Responder = (prompt, _) => prompt.Contains("chương trình khuyến mãi", StringComparison.OrdinalIgnoreCase)
                ? null
                : new LlmChatResult(AllGroupsJson, 10, 5)
        };
        var result = await Extractor(llm).ExtractGroupsAsync(new KnowledgeExtractRequest("text"), default);

        var promo = result.Groups.Single(g => g.Key == "promotion");
        promo.Items.Should().BeEmpty();
        promo.Warning.Should().NotBeNullOrWhiteSpace();
        result.Groups.Where(g => g.Key != "promotion").Should().OnlyContain(g => g.Items.Count == 1);
    }

    [Fact]
    public async Task ExtractGroups_BlankText_ReturnsEmpty_NoLlmCall()
    {
        var llm = new FakeLlmChatClient();
        var result = await Extractor(llm).ExtractGroupsAsync(new KnowledgeExtractRequest("   "), default);

        result.Groups.Should().HaveCount(4);
        result.Groups.Should().OnlyContain(g => g.Items.Count == 0);
        llm.Calls.Should().Be(0);
    }

    [Fact]
    public async Task ExtractGroups_LlmDisabled_AllGroupsGraceful()
    {
        var llm = new FakeLlmChatClient { Enabled = false };
        var result = await Extractor(llm).ExtractGroupsAsync(new KnowledgeExtractRequest("abc"), default);

        result.Groups.Should().HaveCount(4);
        result.Groups.Should().OnlyContain(g => g.Items.Count == 0 && g.Warning != null);
        llm.Calls.Should().Be(0);
    }

    [Fact]
    public async Task ExtractGroups_EachPromptScopedToItsGroup()
    {
        var llm = new FakeLlmChatClient { Responder = (_, _) => new LlmChatResult(AllGroupsJson, 10, 5) };
        await Extractor(llm).ExtractGroupsAsync(new KnowledgeExtractRequest("source text"), default);

        // 4 prompts khác nhau — mỗi prompt phải chứa chỉ dẫn riêng của nhóm.
        llm.Prompts.Should().HaveCount(4);
        llm.Prompts.Should().Contain(p => p.Contains("menu", StringComparison.OrdinalIgnoreCase));
        llm.Prompts.Should().Contain(p => p.Contains("giới thiệu", StringComparison.OrdinalIgnoreCase));
        llm.Prompts.Should().Contain(p => p.Contains("chính sách", StringComparison.OrdinalIgnoreCase));
        llm.Prompts.Should().Contain(p => p.Contains("khuyến mãi", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ExtractGroups_CapsItemsPerGroup()
    {
        var items = string.Join(',', Enumerable.Range(0, 80)
            .Select(i => $$"""{"kind":"product","title":"I{{i}}","content":"c"}"""));
        var llm = new FakeLlmChatClient { Responder = (_, _) => new LlmChatResult($$"""{"summary":"s","items":[{{items}}]}""", 10, 5) };
        var result = await Extractor(llm).ExtractGroupsAsync(new KnowledgeExtractRequest("abc"), default);

        result.Groups.Should().OnlyContain(g => g.Items.Count <= KnowledgeExtractor.MaxItems);
    }
}
