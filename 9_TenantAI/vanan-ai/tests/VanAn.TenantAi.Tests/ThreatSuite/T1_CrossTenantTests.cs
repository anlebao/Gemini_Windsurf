using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using VanAn.TenantAi.Contracts;
using Xunit;

namespace VanAn.TenantAi.Tests.ThreatSuite;

/// <summary>
/// SRS §10 T1 — cross-tenant leak. Tenant A must never see tenant B's data through
/// any retrieval path: structured products, semantic chunks, session history.
/// (Vector-path T1 runs on production RV — pgvector is not exercisable on InMemory.)
/// </summary>
[Trait("Threat", "T1")]
public sealed class T1_CrossTenantTests : ThreatTestBase
{
    [Fact]
    public async Task T1_ProductQuery_TenantBDataInvisibleToA()
    {
        await SeedProductAsync(TenantB, "Bún riêu cua", 99_000m);
        await SeedProductAsync(TenantA, "Phở chay", 80_000m);

        var body = await Chat(ChatReq("Giá bún riêu cua bao nhiêu?"));
        body.Answer.Should().Contain("chưa có dữ liệu");
        body.Products.Should().BeEmpty();
        body.SourceIds.Should().BeEmpty();
    }

    [Fact]
    public async Task T1_SemanticChunks_TenantBDocInvisibleToA()
    {
        // Tenant B: approved + public story doc — must still be invisible to A.
        await SeedDocAsync(TenantB, "story", "Câu chuyện B", "bí mật thương hiệu của tenant B");

        var body = await Chat(ChatReq("Kể câu chuyện thương hiệu quán đi"));
        body.Answer.Should().Contain("chưa có dữ liệu");
        body.Answer.Should().NotContain("tenant B");
        body.SourceIds.Should().BeEmpty();
    }

    [Fact]
    public async Task T1_MenuQuery_TenantAOnlyOwnProducts()
    {
        var a = await SeedProductAsync(TenantA, "Phở chay", 80_000m);
        await SeedProductAsync(TenantB, "Lẩu đặc biệt", 250_000m);
        await SeedProductAsync(TenantB, "Cơm gà", 60_000m);

        var body = await Chat(ChatReq("Cho mình xem menu"));
        body.Products.Select(p => p.ProductId).Should().Equal(a);
    }

    [Fact]
    public async Task T1_SessionHistory_CrossTenantReturnsEmpty()
    {
        await Chat(ChatReq("Xin chào", sessionId: "t1-sess", messageId: "t1-m1"));

        // Tenant B reads A's session id → tenant-filtered → empty (not 403/404).
        var res = await Client.SendAsync(
            Req(HttpMethod.Get, "/v1/chat/sessions/t1-sess/messages", "gateway", TenantB));
        res.StatusCode.Should().Be(HttpStatusCode.OK);
        var messages = await res.Content.ReadFromJsonAsync<SessionMessagesResponse>();
        messages!.Messages.Should().BeEmpty();
    }
}
