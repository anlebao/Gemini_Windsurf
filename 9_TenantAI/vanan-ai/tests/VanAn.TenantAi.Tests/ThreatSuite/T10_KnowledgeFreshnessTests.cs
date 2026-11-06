using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using VanAn.TenantAi.Ai;
using VanAn.TenantAi.Contracts;
using VanAn.TenantAi.Domain;
using Xunit;

namespace VanAn.TenantAi.Tests.ThreatSuite;

/// <summary>
/// SRS §10 T10 — stale knowledge. [G16] freshness: after a re-sync the chat must
/// answer with the NEW source data (đổi giá → giá mới); a stale product gets its
/// volatile claims stripped + StaleData flag; /v1/knowledge/status reports
/// source_version + synced_at for drift/reconcile checks.
/// </summary>
[Trait("Threat", "T10")]
public sealed class T10_KnowledgeFreshnessTests : ThreatTestBase
{
    private static ProductsBatchRequest Batch(string version, Guid id, string name, decimal price)
        => new(FullSync: true, SourceVersion: version,
            Items: [new ProductKnowledgeItem(id, name, null, price, null, null, null, true, "Món chính")]);

    private void LlmClaimsWrongPrice(Guid productId)
        => Llm.Responder = (_, _) => new LlmChatResult(
            "{\"answer\":\"Giá món này là 50k bạn nhé.\",\"productRefs\":[\"" + productId
            + "\"],\"sourceIds\":[],\"confidence\":0.9}", 100, 20);

    [Fact]
    public async Task T10_PriceChange_AfterResync_ChatAnswersNewPrice()
    {
        var pid = Guid.NewGuid();
        (await Client.SendAsync(Req(HttpMethod.Post, "/v1/knowledge/products:batch", "shoperp-a",
            body: Batch("v1", pid, "Phở chay", 80_000m), idem: "t10-b1")))
            .StatusCode.Should().Be(HttpStatusCode.OK);

        LlmClaimsWrongPrice(pid);
        var first = await Chat(ChatReq("Giá phở chay bao nhiêu?", sessionId: "t10-a", messageId: "t10-a1"));
        first.Answer.Should().Contain("80").And.NotContain("50k");

        // Re-sync: same productId, new price + new source_version.
        (await Client.SendAsync(Req(HttpMethod.Post, "/v1/knowledge/products:batch", "shoperp-a",
            body: Batch("v2", pid, "Phở chay", 95_000m), idem: "t10-b2")))
            .StatusCode.Should().Be(HttpStatusCode.OK);

        var second = await Chat(ChatReq("Giá phở chay bao nhiêu?", sessionId: "t10-b", messageId: "t10-b1"));
        second.Answer.Should().Contain("95").And.NotContain("80").And.NotContain("50k");
    }

    [Fact]
    public async Task T10_KnowledgeStatus_ReportsSourceVersionAndSyncedAt()
    {
        var pid = Guid.NewGuid();
        (await Client.SendAsync(Req(HttpMethod.Post, "/v1/knowledge/products:batch", "shoperp-a",
            body: Batch("src-t10", pid, "Bún chay", 45_000m), idem: "t10-st")))
            .StatusCode.Should().Be(HttpStatusCode.OK);

        var res = await Client.SendAsync(Req(HttpMethod.Get, "/v1/knowledge/status", "shoperp-a"));
        res.StatusCode.Should().Be(HttpStatusCode.OK);
        var status = (await res.Content.ReadFromJsonAsync<KnowledgeStatusResponse>())!;
        status.Products.Should().Be(1);
        status.LastProductSourceVersion.Should().Be("src-t10");
        status.LastProductSyncedAtUtc.Should().NotBeNull();
        status.LastProductSyncedAtUtc!.Value.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromMinutes(2));
        status.ProductsStale.Should().BeFalse();
    }

    [Fact]
    public async Task T10_StaleProduct_VolatileClaimsStripped_FlagSet()
    {
        var pid = await SeedProductAsync(TenantA, "Phở chay", 80_000m);
        await Scoped(async db =>
        {
            var p = await db.ProductKnowledge.SingleAsync(x => x.ProductId == pid);
            typeof(ProductKnowledge).GetProperty(nameof(ProductKnowledge.SyncedAtUtc))!
                .SetValue(p, DateTime.UtcNow.AddHours(-48));
            await db.SaveChangesAsync();
        });

        var body = await Chat(ChatReq("Giá phở chay bao nhiêu?", sessionId: "t10-c", messageId: "t10-c1"));
        body.StaleData.Should().BeTrue();
        body.Answer.Should().NotContain("80.000").And.NotContain("80,000");
        body.Products.Should().BeEmpty(); // stale product never suggested to cart [FR-5]

        var status = await (await Client.SendAsync(
            Req(HttpMethod.Get, "/v1/knowledge/status", "shoperp-a")))
            .Content.ReadFromJsonAsync<KnowledgeStatusResponse>();
        status!.ProductsStale.Should().BeTrue();
    }
}
