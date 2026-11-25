using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using VanAn.TenantAi.Ai;
using VanAn.TenantAi.Api.Services;
using VanAn.TenantAi.Contracts;
using Xunit;

namespace VanAn.TenantAi.Tests;

/// <summary>W3a — KnowledgeExtractor: raw text → structured candidates (never throws,
/// always returns a response the UI can render).</summary>
public sealed class KnowledgeExtractorTests
{
    private static KnowledgeExtractor Extractor(FakeLlmChatClient? llm = null)
        => new(llm ?? new FakeLlmChatClient(), NullLogger<KnowledgeExtractor>.Instance);

    private static FakeLlmChatClient LlmReturning(string json)
        => new() { Responder = (_, _) => new LlmChatResult(json, 50, 10) };

    [Fact]
    public async Task Extract_ParsesStructuredJson()
    {
        var llm = LlmReturning("""
            {"summary":"Menu spa","items":[
              {"kind":"service","title":"Aqua Peel","content":"Aqua Peel 590k, 60 phút"},
              {"kind":"faq","title":"Giờ mở cửa","content":"8h-21h hàng ngày"}
            ]}
            """);
        var result = await Extractor(llm).ExtractAsync(new KnowledgeExtractRequest("menu day du"), default);

        result.Summary.Should().Be("Menu spa");
        result.Items.Should().HaveCount(2);
        result.Items[0].Kind.Should().Be("service");
        result.Items[0].Title.Should().Be("Aqua Peel");
    }

    [Fact]
    public async Task Extract_InvalidJson_ReturnsEmptyItems_NoThrow()
    {
        var llm = LlmReturning("đây không phải json");
        var result = await Extractor(llm).ExtractAsync(new KnowledgeExtractRequest("abc"), default);

        result.Items.Should().BeEmpty();
        result.Summary.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task Extract_LlmDisabled_ReturnsGracefulEmpty()
    {
        var llm = new FakeLlmChatClient { Enabled = false };
        var result = await Extractor(llm).ExtractAsync(new KnowledgeExtractRequest("abc"), default);

        result.Items.Should().BeEmpty();
        llm.Calls.Should().Be(0);
    }

    [Fact]
    public async Task Extract_LlmNull_ReturnsGracefulEmpty()
    {
        var llm = new FakeLlmChatClient { Responder = (_, _) => null };
        var result = await Extractor(llm).ExtractAsync(new KnowledgeExtractRequest("abc"), default);

        result.Items.Should().BeEmpty();
    }

    [Fact]
    public async Task Extract_CapsItems_AndNormalizesKind()
    {
        var items = string.Join(',', Enumerable.Range(0, 80)
            .Select(i => $$"""{"kind":"weird{{i}}","title":"Item {{i}}","content":"c"}"""));
        var llm = LlmReturning($$"""{"summary":"s","items":[{{items}}]}""");
        var result = await Extractor(llm).ExtractAsync(new KnowledgeExtractRequest("abc"), default);

        result.Items.Count.Should().BeLessThanOrEqualTo(KnowledgeExtractor.MaxItems);
        result.Items.Should().OnlyContain(i => i.Kind == "other");
    }

    [Fact]
    public async Task Extract_PromptContainsSourceText()
    {
        var llm = new FakeLlmChatClient();
        await Extractor(llm).ExtractAsync(
            new KnowledgeExtractRequest("BẢNG GIÁ DOCTOR SPAN: Aqua Peel 590k"), default);

        llm.Prompts.Should().ContainSingle(p => p.Contains("Aqua Peel 590k"));
    }

    [Fact]
    public async Task Extract_SkipsBlankTitleOrContent()
    {
        var llm = LlmReturning("""
            {"summary":"s","items":[
              {"kind":"faq","title":"","content":"c"},
              {"kind":"faq","title":"ok","content":"  "},
              {"kind":"faq","title":"Giữ lại","content":"nội dung"}
            ]}
            """);
        var result = await Extractor(llm).ExtractAsync(new KnowledgeExtractRequest("abc"), default);

        result.Items.Should().ContainSingle(i => i.Title == "Giữ lại");
    }

    // ── Pending-Review T3 (2026-11-25): price field optional ──────────────

    [Fact]
    public async Task Extract_PriceNumber_ParsesDecimal()
    {
        var llm = LlmReturning("""
            {"summary":"Menu","items":[
              {"kind":"product","title":"Cà phê sữa đá","content":"20k","price":20000},
              {"kind":"service","title":"Aqua Peel","content":"590k","price":590000.0}
            ]}
            """);
        var result = await Extractor(llm).ExtractAsync(new KnowledgeExtractRequest("menu"), default);

        result.Items[0].Price.Should().Be(20000m);
        result.Items[1].Price.Should().Be(590000m);
    }

    [Fact]
    public async Task Extract_NoPriceField_PriceNull()
    {
        var llm = LlmReturning("""
            {"summary":"s","items":[{"kind":"faq","title":"Giờ mở cửa","content":"8h-21h"}]}
            """);
        var result = await Extractor(llm).ExtractAsync(new KnowledgeExtractRequest("abc"), default);

        result.Items.Should().ContainSingle();
        result.Items[0].Price.Should().BeNull();
    }

    [Fact]
    public async Task Extract_PriceNonNumericOrNegative_PriceNull()
    {
        var llm = LlmReturning("""
            {"summary":"s","items":[
              {"kind":"product","title":"A","content":"c","price":"abc"},
              {"kind":"product","title":"B","content":"c","price":-500},
              {"kind":"product","title":"C","content":"c","price":15000}
            ]}
            """);
        var result = await Extractor(llm).ExtractAsync(new KnowledgeExtractRequest("abc"), default);

        result.Items[0].Price.Should().BeNull();
        result.Items[1].Price.Should().BeNull();
        result.Items[2].Price.Should().Be(15000m);
    }

    [Fact]
    public async Task Extract_ImageUrl_ParsesValidHttp_SkipsInvalid()
    {
        var llm = LlmReturning("""
            {"summary":"s","items":[
              {"kind":"product","title":"Trà sữa","content":"25k","price":25000,
               "imageUrl":"https://shop.com/trasua.jpg"},
              {"kind":"product","title":"Cà phê","content":"20k",
               "imageUrl":"javascript:alert(1)"},
              {"kind":"product","title":"Bánh","content":"10k"}
            ]}
            """);
        var result = await Extractor(llm).ExtractAsync(new KnowledgeExtractRequest("menu"), default);

        result.Items[0].ImageUrl.Should().Be("https://shop.com/trasua.jpg");
        result.Items[1].ImageUrl.Should().BeNull();
        result.Items[2].ImageUrl.Should().BeNull();
    }
}
