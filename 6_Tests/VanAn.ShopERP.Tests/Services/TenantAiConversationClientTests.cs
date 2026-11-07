using System.Net;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using VanAn.ShopERP.Services;
using Xunit;

namespace VanAn.ShopERP.Tests.Services;

/// <summary>
/// Task AI-P9 — TenantAiApiClient conversation-review methods (ShopInbox "AI trả lời khách" tab):
/// field mapping, page/size + sessionId query, fail-safe null (never throws), disabled → zero HTTP.
/// </summary>
public sealed class TenantAiConversationClientTests
{
    private static readonly Guid Tenant = Guid.Parse("aaaaaaaa-0000-0000-0000-000000000001");

    private sealed class StubHandler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
    {
        public int Calls { get; private set; }
        public HttpRequestMessage? LastRequest { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Calls++;
            LastRequest = request;
            return Task.FromResult(respond(request));
        }
    }

    private static TenantAiApiClient BuildClient(HttpMessageHandler handler, bool configured = true)
    {
        var http = new HttpClient(handler) { BaseAddress = new Uri("https://vanan-ai.test/") };
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["TenantAi:CallerId"] = configured ? "shoperp" : null,
            ["TenantAi:CallerKey"] = configured ? "key" : null,
            ["TenantAi:CtxSigningSecret"] = configured ? "secret" : null,
        }).Build();
        return new TenantAiApiClient(http, config, NullLogger<TenantAiApiClient>.Instance);
    }

    private static HttpResponseMessage Json(string body, HttpStatusCode code = HttpStatusCode.OK)
        => new(code) { Content = new StringContent(body, System.Text.Encoding.UTF8, "application/json") };

    [Fact]
    public async Task GetAiConversations_MapsFields_AndSendsPageSize()
    {
        var handler = new StubHandler(_ => Json("""
            {"page":2,"size":20,"total":41,"items":[
              {"sessionId":"sess-abc","channel":"public","lastActiveAtUtc":"2026-11-07T03:00:00Z",
               "messageCount":12,"refusedCount":2,"degradedCount":1}]}
            """));
        var client = BuildClient(handler);

        var list = await client.GetAiConversationsAsync(Tenant, page: 2, size: 20);

        list.Should().NotBeNull();
        list!.Total.Should().Be(41);
        list.Items.Should().HaveCount(1);
        var s = list.Items[0];
        s.SessionId.Should().Be("sess-abc");
        s.Channel.Should().Be("public");
        s.MessageCount.Should().Be(12);
        s.RefusedCount.Should().Be(2);
        s.DegradedCount.Should().Be(1);
        handler.LastRequest!.RequestUri!.ToString()
            .Should().Be($"https://vanan-ai.test/v1/admin/tenants/{Tenant}/conversations?page=2&size=20");
        handler.LastRequest.Headers.Contains("X-Tenant-Ctx").Should().BeTrue();
    }

    [Fact]
    public async Task GetAiConversationMessages_MapsDetail_AndSendsSessionId()
    {
        var handler = new StubHandler(_ => Json("""
            {"sessionId":"sess-abc","messages":[
              {"role":"user","content":"Có phở chay không?","intent":null,"confidence":null,
               "refused":false,"degraded":false,"createdAtUtc":"2026-11-07T03:00:00Z"},
              {"role":"assistant","content":"Dạ có ạ.","intent":"menu_query","confidence":0.92,
               "refused":false,"degraded":false,"createdAtUtc":"2026-11-07T03:00:01Z"}]}
            """));
        var client = BuildClient(handler);

        var detail = await client.GetAiConversationMessagesAsync(Tenant, "sess-abc");

        detail.Should().NotBeNull();
        detail!.SessionId.Should().Be("sess-abc");
        detail.Messages.Should().HaveCount(2);
        detail.Messages[0].Role.Should().Be("user");
        detail.Messages[1].Intent.Should().Be("menu_query");
        detail.Messages[1].Confidence.Should().Be(0.92);
        handler.LastRequest!.RequestUri!.ToString()
            .Should().Be($"https://vanan-ai.test/v1/admin/tenants/{Tenant}/conversations?sessionId=sess-abc");
    }

    [Fact]
    public async Task GetAiConversations_ServerError_ReturnsNull_NotThrows()
    {
        var handler = new StubHandler(_ => Json("{}", HttpStatusCode.InternalServerError));
        var client = BuildClient(handler);

        var list = await client.GetAiConversationsAsync(Tenant, 1, 20);

        list.Should().BeNull();
    }

    [Fact]
    public async Task GetAiConversationMessages_NotFound_ReturnsNull()
    {
        var handler = new StubHandler(_ => Json(
            """{"error":{"code":"not_found","message":"Session not found.","retryable":false}}""",
            HttpStatusCode.NotFound));
        var client = BuildClient(handler);

        var detail = await client.GetAiConversationMessagesAsync(Tenant, "missing");

        detail.Should().BeNull();
    }

    [Fact]
    public async Task ClientDisabled_MakesZeroHttpCalls()
    {
        var handler = new StubHandler(_ => Json("{}"));
        var client = BuildClient(handler, configured: false);

        var list = await client.GetAiConversationsAsync(Tenant, 1, 20);
        var detail = await client.GetAiConversationMessagesAsync(Tenant, "s1");

        list.Should().BeNull();
        detail.Should().BeNull();
        handler.Calls.Should().Be(0);
    }
}
