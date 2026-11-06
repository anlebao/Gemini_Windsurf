using FluentAssertions;
using VanAn.TenantAi.Ai;
using Xunit;

namespace VanAn.TenantAi.Tests.ThreatSuite;

/// <summary>
/// SRS §10 T4 — hallucinated product/price. The validator strips productRefs and
/// sourceIds that were never in the retrieved FACTS, and a question about a
/// non-existent product must answer "chưa có" — never an invented item.
/// </summary>
[Trait("Threat", "T4")]
public sealed class T4_HallucinationTests : ThreatTestBase
{
    [Fact]
    public async Task T4_LlmInventsProductRef_StrippedFromResponse()
    {
        var real = await SeedProductAsync(TenantA, "Phở chay", 80_000m);
        var fakeRef = Guid.NewGuid(); // never retrieved as a fact
        Llm.Responder = (_, _) => new LlmChatResult(
            "{\"answer\":\"Theo dữ liệu quán.\",\"productRefs\":[\"" + real + "\",\"" + fakeRef
            + "\"],\"sourceIds\":[\"product:" + real + "\",\"product:" + fakeRef
            + "\",\"chunk:dddddddd-0000-0000-0000-000000000000\"],\"confidence\":0.9}", 100, 20);

        var body = await Chat(ChatReq("Giá phở chay bao nhiêu?"));
        body.Products.Select(p => p.ProductId).Should().Equal(real);
        body.SourceIds.Should().Equal($"product:{real}");
    }

    [Fact]
    public async Task T4_UnknownProduct_NeverInvented_ZeroLlmCalls()
    {
        await SeedProductAsync(TenantA, "Phở chay", 80_000m);
        var body = await Chat(ChatReq("Giá burger phô mai bao nhiêu?"));
        body.Answer.Should().Contain("chưa có dữ liệu");
        body.Products.Should().BeEmpty();
        Llm.Calls.Should().Be(0);
    }

    [Fact]
    public async Task T4_LlmInventsPrice_NotInFacts_FallsBack()
    {
        await SeedProductAsync(TenantA, "Phở chay", 80_000m);
        // Invented calories where the fact has none → ungrounded → fallback answer.
        Llm.Responder = (_, _) => new LlmChatResult(
            "{\"answer\":\"Món này 350 kcal bạn nhé.\",\"productRefs\":[],\"sourceIds\":[],\"confidence\":0.9}", 100, 20);
        var body = await Chat(ChatReq("Giá phở chay bao nhiêu?"));
        body.Answer.Should().NotContain("350");
    }
}
