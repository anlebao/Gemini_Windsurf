using System.Net;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using VanAn.ShopERP.Services;
using Xunit;

namespace VanAn.ShopERP.Tests.Services;

/// <summary>
/// Task AI-P10 — TenantAiApiClient admin methods (SRS §6.3): endpoint paths, request
/// payloads, field mapping, fail-safe null (never throws), disabled → zero HTTP.
/// </summary>
public sealed class TenantAiAdminClientTests
{
    private static readonly Guid Tenant = Guid.Parse("aaaaaaaa-0000-0000-0000-000000000001");

    private sealed class StubHandler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
    {
        public int Calls { get; private set; }
        public HttpRequestMessage? LastRequest { get; private set; }
        public string? LastBody { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Calls++;
            LastRequest = request;
            LastBody = request.Content == null ? null : await request.Content.ReadAsStringAsync(ct);
            return respond(request);
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

    // ---------- profile / lifecycle ----------

    [Fact]
    public async Task GetProfile_MapsDraftPublished_AndRecentVersions()
    {
        var handler = new StubHandler(_ => Json("""
            {"draft":{"version":3,"status":"draft","aiName":"Bé Mới","personaJson":"{\"greeting\":\"Hi\"}",
              "businessProfileJson":"{}","publishedAtUtc":null,"publishedBy":null},
             "published":{"version":2,"status":"published","aiName":"Bé Cũ","personaJson":"{}",
              "businessProfileJson":"{}","publishedAtUtc":"2026-11-06T10:00:00Z","publishedBy":"ops"},
             "recentVersions":[
              {"version":3,"status":"draft","aiName":"Bé Mới","personaJson":"{}","businessProfileJson":"{}","publishedAtUtc":null,"publishedBy":null},
              {"version":2,"status":"published","aiName":"Bé Cũ","personaJson":"{}","businessProfileJson":"{}","publishedAtUtc":"2026-11-06T10:00:00Z","publishedBy":"ops"}]}
            """));
        var client = BuildClient(handler);

        var profile = await client.GetProfileAsync(Tenant);

        profile.Should().NotBeNull();
        profile!.Draft!.Version.Should().Be(3);
        profile.Draft.AiName.Should().Be("Bé Mới");
        profile.Published!.Version.Should().Be(2);
        profile.RecentVersions.Should().HaveCount(2);
        handler.LastRequest!.RequestUri!.ToString()
            .Should().Be($"https://vanan-ai.test/v1/admin/tenants/{Tenant}/profile");
        handler.LastRequest.Headers.Contains("X-Tenant-Ctx").Should().BeTrue();
    }

    [Fact]
    public async Task PutProfile_SendsPayload_AndMapsVersion()
    {
        var handler = new StubHandler(_ => Json("""{"version":4,"status":"draft"}"""));
        var client = BuildClient(handler);

        var result = await client.PutProfileAsync(Tenant, "Be An Tam", "{\"greeting\":\"Chao\"}", "{}");

        result.Should().NotBeNull();
        result!.Version.Should().Be(4);
        handler.LastRequest!.Method.Should().Be(HttpMethod.Put);
        handler.LastBody.Should().Contain("\"aiName\":\"Be An Tam\"");
        handler.LastBody.Should().Contain("personaJson");
        handler.LastBody.Should().Contain("businessProfileJson");
    }

    [Fact]
    public async Task Publish_PostsToPublishEndpoint()
    {
        var handler = new StubHandler(_ => Json(
            """{"version":4,"status":"published","publishedAtUtc":"2026-11-07T01:00:00Z","publishedBy":"shoperp"}"""));
        var client = BuildClient(handler);

        var result = await client.PublishConfigAsync(Tenant);

        result.Should().NotBeNull();
        result!.Version.Should().Be(4);
        result.Status.Should().Be("published");
        handler.LastRequest!.Method.Should().Be(HttpMethod.Post);
        handler.LastRequest.RequestUri!.ToString()
            .Should().Be($"https://vanan-ai.test/v1/admin/tenants/{Tenant}/publish");
    }

    [Fact]
    public async Task Rollback_SendsVersion_InBody()
    {
        var handler = new StubHandler(_ => Json(
            """{"version":5,"status":"published","publishedAtUtc":"2026-11-07T01:00:00Z","publishedBy":"shoperp"}"""));
        var client = BuildClient(handler);

        var result = await client.RollbackConfigAsync(Tenant, 2);

        result.Should().NotBeNull();
        handler.LastRequest!.RequestUri!.ToString()
            .Should().Be($"https://vanan-ai.test/v1/admin/tenants/{Tenant}/rollback");
        handler.LastBody.Should().Contain("\"version\":2");
    }

    // ---------- policy ----------

    [Fact]
    public async Task GetPolicy_MapsJsonFields()
    {
        var handler = new StubHandler(_ => Json("""
            {"configVersion":2,"intentScopesJson":"{\"MenuQuery\":false}",
             "externalAllowJson":"[]","refusedTopicsJson":"[\"giá sỉ\"]"}
            """));
        var client = BuildClient(handler);

        var policy = await client.GetPolicyAsync(Tenant);

        policy.Should().NotBeNull();
        policy!.ConfigVersion.Should().Be(2);
        policy.IntentScopesJson.Should().Contain("MenuQuery");
        policy.RefusedTopicsJson.Should().Contain("giá sỉ");
    }

    [Fact]
    public async Task PutPolicy_SendsThreeJsonFields()
    {
        var handler = new StubHandler(_ => Json(
            """{"configVersion":2,"intentScopesJson":"{}","externalAllowJson":"[]","refusedTopicsJson":"[]"}"""));
        var client = BuildClient(handler);

        var result = await client.PutPolicyAsync(Tenant, "{\"Faq\":false}", "[]", "[\"x\"]");

        result.Should().NotBeNull();
        handler.LastRequest!.Method.Should().Be(HttpMethod.Put);
        handler.LastBody.Should().Contain("intentScopesJson");
        handler.LastBody.Should().Contain("refusedTopicsJson");
        handler.LastBody.Should().Contain("externalAllowJson");
    }

    // ---------- preview chat ----------

    [Fact]
    public async Task PreviewChat_PostsChatRequest_AndMapsAnswer()
    {
        var handler = new StubHandler(_ => Json("""
            {"answer":"Quán mở 9h-21h ạ.","products":[],"sourceIds":["doc-1"],
             "confidence":0.91,"refused":false,"refuseReason":null,"staleData":false,"degraded":false}
            """));
        var client = BuildClient(handler);

        var result = await client.PreviewChatAsync(Tenant, "sess-1", "msg-1", "Mấy giờ mở cửa?");

        result.Should().NotBeNull();
        result!.Answer.Should().Contain("9h-21h");
        result.Confidence.Should().Be(0.91);
        handler.LastRequest!.RequestUri!.ToString()
            .Should().Be($"https://vanan-ai.test/v1/admin/tenants/{Tenant}/preview-chat");
        handler.LastBody.Should().Contain("\"sessionId\":\"sess-1\"");
        handler.LastBody.Should().Contain("\"messageId\":\"msg-1\"");
    }

    // ---------- documents list + delete + reject payload ----------

    [Fact]
    public async Task GetDocuments_SendsStatusPageSize_AndMapsContent()
    {
        var handler = new StubHandler(_ => Json("""
            {"page":1,"size":20,"total":1,"items":[
             {"id":"11111111-1111-1111-1111-111111111111","type":"faq","title":"Giờ mở cửa",
              "classification":"public","approvalStatus":"pending_review","approvedBy":null,
              "approvedAtUtc":null,"rejectionReason":null,"createdAtUtc":"2026-11-07T00:00:00Z",
              "sanitized":false,"content":"Mở 9h-21h"}]}
            """));
        var client = BuildClient(handler);

        var list = await client.GetDocumentsAsync(Tenant, "pending_review", 1, 20);

        list.Should().NotBeNull();
        list!.Total.Should().Be(1);
        list.Items[0].Content.Should().Be("Mở 9h-21h");
        list.Items[0].ApprovalStatus.Should().Be("pending_review");
        handler.LastRequest!.RequestUri!.ToString()
            .Should().Be("https://vanan-ai.test/v1/admin/knowledge/documents?page=1&size=20&status=pending_review");
    }

    [Fact]
    public async Task GetDocuments_NoStatus_OmitsFilterParam()
    {
        var handler = new StubHandler(_ => Json("""{"page":1,"size":20,"total":0,"items":[]}"""));
        var client = BuildClient(handler);

        await client.GetDocumentsAsync(Tenant, null, 1, 20);

        handler.LastRequest!.RequestUri!.ToString()
            .Should().Be("https://vanan-ai.test/v1/admin/knowledge/documents?page=1&size=20");
    }

    [Fact]
    public async Task DeleteDocument_SendsDelete_ReturnsBool()
    {
        var handler = new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.NoContent));
        var client = BuildClient(handler);
        var docId = Guid.NewGuid();

        var ok = await client.DeleteDocumentAsync(Tenant, docId);

        ok.Should().BeTrue();
        handler.LastRequest!.Method.Should().Be(HttpMethod.Delete);
        handler.LastRequest.RequestUri!.ToString()
            .Should().Be($"https://vanan-ai.test/v1/knowledge/documents/{docId}");
    }

    [Fact]
    public async Task RejectDocument_SendsReasonPayload()
    {
        var handler = new StubHandler(_ => Json(
            """{"id":"11111111-1111-1111-1111-111111111111","type":"faq","title":"t","classification":"public","approvalStatus":"rejected","approvedBy":"shoperp","approvedAtUtc":"2026-11-07T00:00:00Z","rejectionReason":"sai thong tin","createdAtUtc":"2026-11-06T00:00:00Z"}"""));
        var client = BuildClient(handler);
        var docId = Guid.NewGuid();

        var ok = await client.RejectDocumentAsync(Tenant, docId, "sai thong tin");

        ok.Should().BeTrue();
        handler.LastBody.Should().Contain("\"reason\":\"sai thong tin\"");
        handler.LastRequest!.RequestUri!.ToString()
            .Should().Be($"https://vanan-ai.test/v1/admin/knowledge/{docId}/reject");
    }

    // ---------- usage ----------

    [Fact]
    public async Task GetUsage_SendsDays_AndMapsTotals()
    {
        var handler = new StubHandler(_ => Json("""
            {"days":30,"perDay":[{"date":"2026-11-06","messages":5,"refused":1,"degraded":0,
              "promptTokens":1000,"completionTokens":400}],
             "total":{"date":"total","messages":5,"refused":1,"degraded":0,
              "promptTokens":1000,"completionTokens":400}}
            """));
        var client = BuildClient(handler);

        var usage = await client.GetUsageAsync(Tenant, 30);

        usage.Should().NotBeNull();
        usage!.Total.Messages.Should().Be(5);
        usage.Total.PromptTokens.Should().Be(1000);
        usage.PerDay.Should().HaveCount(1);
        handler.LastRequest!.RequestUri!.ToString()
            .Should().Be($"https://vanan-ai.test/v1/admin/tenants/{Tenant}/usage?days=30");
    }

    // ---------- fail-safe ----------

    [Fact]
    public async Task ServerError_ReturnsNull_NotThrows()
    {
        var handler = new StubHandler(_ => Json("{}", HttpStatusCode.InternalServerError));
        var client = BuildClient(handler);

        (await client.GetProfileAsync(Tenant)).Should().BeNull();
        (await client.GetPolicyAsync(Tenant)).Should().BeNull();
        (await client.GetUsageAsync(Tenant, 30)).Should().BeNull();
        (await client.GetDocumentsAsync(Tenant, null, 1, 20)).Should().BeNull();
        (await client.PreviewChatAsync(Tenant, "s", "m", "t")).Should().BeNull();
        (await client.PublishConfigAsync(Tenant)).Should().BeNull();
        (await client.DeleteDocumentAsync(Tenant, Guid.NewGuid())).Should().BeFalse();
    }

    [Fact]
    public async Task ClientDisabled_MakesZeroHttpCalls()
    {
        var handler = new StubHandler(_ => Json("{}"));
        var client = BuildClient(handler, configured: false);

        (await client.GetProfileAsync(Tenant)).Should().BeNull();
        (await client.PutProfileAsync(Tenant, "n", null, null)).Should().BeNull();
        (await client.PublishConfigAsync(Tenant)).Should().BeNull();
        (await client.GetPolicyAsync(Tenant)).Should().BeNull();
        (await client.PreviewChatAsync(Tenant, "s", "m", "t")).Should().BeNull();
        (await client.GetDocumentsAsync(Tenant, null, 1, 20)).Should().BeNull();
        (await client.DeleteDocumentAsync(Tenant, Guid.NewGuid())).Should().BeFalse();
        (await client.GetUsageAsync(Tenant, 30)).Should().BeNull();

        handler.Calls.Should().Be(0);
    }
}
