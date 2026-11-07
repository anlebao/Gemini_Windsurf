using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using VanAn.TenantAi.Api.Services;
using VanAn.TenantAi.Contracts;
using VanAn.TenantAi.Domain;
using VanAn.TenantAi.Infra;
using Xunit;

namespace VanAn.TenantAi.Tests;

/// <summary>task_ai_p6b — admin API §6.3 + retention cleanup + tenant purge (InMemory WAF).</summary>
public sealed class AdminApiTests : IDisposable
{
    private static readonly Guid TenantA = TenantAiApiFactory.TenantA;
    private static readonly Guid TenantB = TenantAiApiFactory.TenantB;

    private readonly TenantAiApiFactory _factory;
    private readonly HttpClient _client;

    public AdminApiTests()
    {
        _factory = TenantAiApiFactory.WithServices(s => { });
        _client = _factory.CreateClient();
    }

    public void Dispose() => _factory.Dispose();

    // ---------- helpers ----------

    private static (string Ctx, string Sig) SignCtx(Guid tenantId, string callerId)
        => TenantContextSigner.Sign(
            TenantContextSigner.CreatePayload(tenantId, callerId, "req-" + Guid.NewGuid().ToString("N")[..8]),
            TenantAiApiFactory.SigningSecret);

    private HttpRequestMessage Req(HttpMethod method, string path, object? body = null,
        string caller = "shoperp-a", Guid? ctxTenant = null)
    {
        var r = new HttpRequestMessage(method, path);
        r.Headers.Add("X-Caller-Id", caller);
        r.Headers.Add("X-Ai-Key", caller == "gateway" ? TenantAiApiFactory.GatewayKey : TenantAiApiFactory.ShopKey);
        var (ctx, sig) = SignCtx(ctxTenant ?? TenantA, caller);
        r.Headers.Add("X-Tenant-Ctx", ctx);
        r.Headers.Add("X-Tenant-Ctx-Sig", sig);
        if (body != null) r.Content = JsonContent.Create(body);
        return r;
    }

    private string AdminPath(Guid? tenant = null, string suffix = "")
        => $"/v1/admin/tenants/{tenant ?? TenantA}{suffix}";

    private async Task<T> Scoped<T>(Func<TenantAiDbContext, Task<T>> f)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        return await f(scope.ServiceProvider.GetRequiredService<TenantAiDbContext>());
    }

    private async Task Scoped(Func<TenantAiDbContext, Task> f)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        await f(scope.ServiceProvider.GetRequiredService<TenantAiDbContext>());
    }

    private async Task PutProfileAsync(string name)
        => (await _client.SendAsync(Req(HttpMethod.Put, AdminPath(suffix: "/profile"),
            new PutProfileRequest(name, null, null)))).StatusCode.Should().Be(HttpStatusCode.OK);

    // ---------- profile / lifecycle [G11] ----------

    [Fact]
    public async Task PutProfile_CreatesAppendOnlyDraftVersions()
    {
        await PutProfileAsync("Bé Chay");
        await PutProfileAsync("Bé Chay v2");

        var res = await _client.SendAsync(Req(HttpMethod.Get, AdminPath(suffix: "/profile")));
        res.StatusCode.Should().Be(HttpStatusCode.OK);
        var profile = await res.Content.ReadFromJsonAsync<ProfileResponse>();
        profile!.Draft!.Version.Should().Be(2);
        profile.Draft.AiName.Should().Be("Bé Chay v2");
        profile.Published.Should().BeNull();

        // append-only: v1 row still exists untouched
        var versions = await Scoped(db => db.TenantAiConfigs
            .Where(c => c.TenantId == TenantA).Select(c => c.Version).ToListAsync());
        versions.Should().BeEquivalentTo([1, 2]);
    }

    [Fact]
    public async Task PutProfile_Validates()
    {
        var empty = await _client.SendAsync(Req(HttpMethod.Put, AdminPath(suffix: "/profile"),
            new PutProfileRequest("  ", null, null)));
        empty.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var badJson = await _client.SendAsync(Req(HttpMethod.Put, AdminPath(suffix: "/profile"),
            new PutProfileRequest("Bé", "{not-json", null)));
        badJson.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Publish_DraftToPublished_ArchivesPrevious()
    {
        await PutProfileAsync("v1");
        (await _client.SendAsync(Req(HttpMethod.Post, AdminPath(suffix: "/publish"))))
            .StatusCode.Should().Be(HttpStatusCode.OK);

        await PutProfileAsync("v2");
        var res = await _client.SendAsync(Req(HttpMethod.Post, AdminPath(suffix: "/publish")));
        var pub = await res.Content.ReadFromJsonAsync<PublishResponse>();
        pub!.Version.Should().Be(2);
        pub.PublishedBy.Should().Be("shoperp-a");

        var profile = await (await _client.SendAsync(
            Req(HttpMethod.Get, AdminPath(suffix: "/profile"))))
            .Content.ReadFromJsonAsync<ProfileResponse>();
        profile!.Published!.Version.Should().Be(2);
        profile.Draft.Should().BeNull();
    }

    [Fact]
    public async Task Publish_NoDraft_409()
    {
        var res = await _client.SendAsync(Req(HttpMethod.Post, AdminPath(suffix: "/publish")));
        res.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Rollback_CreatesNewPublishedVersionWithOldContent()
    {
        await PutProfileAsync("Tên Cũ");
        await _client.SendAsync(Req(HttpMethod.Post, AdminPath(suffix: "/publish")));
        await PutProfileAsync("Tên Mới");
        await _client.SendAsync(Req(HttpMethod.Post, AdminPath(suffix: "/publish")));

        var res = await _client.SendAsync(Req(HttpMethod.Post, AdminPath(suffix: "/rollback"),
            new RollbackRequest(1)));
        res.StatusCode.Should().Be(HttpStatusCode.OK);

        var profile = await (await _client.SendAsync(
            Req(HttpMethod.Get, AdminPath(suffix: "/profile"))))
            .Content.ReadFromJsonAsync<ProfileResponse>();
        profile!.Published!.AiName.Should().Be("Tên Cũ");
        profile.Published.Version.Should().Be(3); // rollback = new version, append-only [G11]
    }

    [Fact]
    public async Task Rollback_UnknownVersion_404()
    {
        var res = await _client.SendAsync(Req(HttpMethod.Post, AdminPath(suffix: "/rollback"),
            new RollbackRequest(99)));
        res.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // ---------- policy ----------

    [Fact]
    public async Task Policy_GetDefault_Put_ExternalRejected()
    {
        await PutProfileAsync("Bé"); // policy needs a config row (gắn configVersion)

        var def = await (await _client.SendAsync(
            Req(HttpMethod.Get, AdminPath(suffix: "/policy"))))
            .Content.ReadFromJsonAsync<PolicyResponse>();
        def!.ConfigVersion.Should().Be(1);
        def.IntentScopesJson.Should().Be("{}");

        var put = await _client.SendAsync(Req(HttpMethod.Put, AdminPath(suffix: "/policy"),
            new PutPolicyRequest("{\"MenuQuery\":false}", null, "[\"giá sỉ\"]")));
        put.StatusCode.Should().Be(HttpStatusCode.OK);

        var got = await (await _client.SendAsync(
            Req(HttpMethod.Get, AdminPath(suffix: "/policy"))))
            .Content.ReadFromJsonAsync<PolicyResponse>();
        got!.IntentScopesJson.Should().Contain("MenuQuery");
        got.RefusedTopicsJson.Should().Contain("giá sỉ");

        var ext = await _client.SendAsync(Req(HttpMethod.Put, AdminPath(suffix: "/policy"),
            new PutPolicyRequest(null, "[\"wikipedia.org\"]", null)));
        ext.StatusCode.Should().Be(HttpStatusCode.BadRequest); // external_p3
    }

    [Fact]
    public async Task Policy_Put_WithoutConfig_409()
    {
        var res = await _client.SendAsync(Req(HttpMethod.Put, AdminPath(suffix: "/policy"),
            new PutPolicyRequest("{}", null, null)));
        res.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    // ---------- conversations + usage ----------

    private async Task SeedConversationAsync(Guid tenant, string sessionId, string channel,
        int assistantMessages = 1, bool refused = false, bool degraded = false)
    {
        await Scoped(db =>
        {
            db.ConversationSessions.Add(ConversationSession.Create(tenant, sessionId, channel));
            for (var i = 0; i < assistantMessages; i++)
            {
                var m = ConversationMessage.Create(tenant, sessionId, $"m{i}:reply",
                    MessageRole.Assistant, $"answer {i}", DateTime.UtcNow.AddDays(90));
                m.SetAnswerMeta("MenuQuery", null, null, 0.9, refused, null, degraded, 100, 50);
                db.ConversationMessages.Add(m);
            }
            return db.SaveChangesAsync();
        });
    }

    [Fact]
    public async Task Conversations_Paged_SessionDrill_WrongTenant403()
    {
        await SeedConversationAsync(TenantA, "s-aaa", "public", assistantMessages: 2, refused: true);
        await SeedConversationAsync(TenantA, "s-bbb", "public");

        var list = await (await _client.SendAsync(
            Req(HttpMethod.Get, AdminPath(suffix: "/conversations?page=1&size=10"))))
            .Content.ReadFromJsonAsync<ConversationListResponse>();
        list!.Total.Should().Be(2);
        list.Items.Should().HaveCount(2);
        var s1 = list.Items.First(i => i.SessionId == "s-aaa");
        s1.MessageCount.Should().Be(2);
        s1.RefusedCount.Should().Be(2);

        var detail = await (await _client.SendAsync(
            Req(HttpMethod.Get, AdminPath(suffix: "/conversations?sessionId=s-aaa"))))
            .Content.ReadFromJsonAsync<ConversationDetailResponse>();
        detail!.Messages.Should().HaveCount(2);
        detail.Messages[0].Role.Should().Be("assistant");
        detail.Messages[0].Refused.Should().BeTrue();

        var missing = await _client.SendAsync(
            Req(HttpMethod.Get, AdminPath(suffix: "/conversations?sessionId=nope")));
        missing.StatusCode.Should().Be(HttpStatusCode.NotFound);

        // route tenant ≠ ctx tenant → 403 (admin path of T1)
        var wrong = await _client.SendAsync(
            Req(HttpMethod.Get, AdminPath(TenantB, "/conversations")));
        wrong.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Usage_AggregatesTokensRefusedDegraded()
    {
        await SeedConversationAsync(TenantA, "s1", "public", assistantMessages: 2, refused: true);
        await SeedConversationAsync(TenantA, "s2", "public", assistantMessages: 1, degraded: true);

        var usage = await (await _client.SendAsync(
            Req(HttpMethod.Get, AdminPath(suffix: "/usage?days=30"))))
            .Content.ReadFromJsonAsync<UsageResponse>();
        usage!.Total.Messages.Should().Be(3);
        usage.Total.Refused.Should().Be(2);
        usage.Total.Degraded.Should().Be(1);
        usage.Total.PromptTokens.Should().Be(300);   // 3 × 100
        usage.Total.CompletionTokens.Should().Be(150); // 3 × 50
    }

    // ---------- retention cleanup [G10] ----------

    [Fact]
    public async Task RetentionCleanup_DeletesExpiredAndOrphanSessions()
    {
        await Scoped(async db =>
        {
            // orphan-to-be: session with only expired messages
            db.ConversationSessions.Add(ConversationSession.Create(TenantA, "s-old", "public"));
            db.ConversationMessages.Add(ConversationMessage.Create(
                TenantA, "s-old", "m1", MessageRole.User, "old",
                DateTime.UtcNow.AddDays(-1)));
            // live: session with a valid message
            db.ConversationSessions.Add(ConversationSession.Create(TenantA, "s-live", "public"));
            db.ConversationMessages.Add(ConversationMessage.Create(
                TenantA, "s-live", "m1", MessageRole.User, "fresh",
                DateTime.UtcNow.AddDays(30)));
            await db.SaveChangesAsync();
        });

        var cleanup = _factory.Services.GetServices<IHostedService>()
            .OfType<RetentionCleanupService>().Single();
        var (msgs, sessions) = await cleanup.CleanupPassAsync(default);
        msgs.Should().Be(1);
        sessions.Should().Be(1);

        var remaining = await Scoped(db => Task.FromResult(new
        {
            Messages = db.ConversationMessages.Count(m => m.TenantId == TenantA),
            Sessions = db.ConversationSessions.Count(s => s.TenantId == TenantA)
        }));
        remaining.Messages.Should().Be(1);
        remaining.Sessions.Should().Be(1);
    }

    // ---------- purge [NFR-7] ----------

    [Fact]
    public async Task Purge_RequiresConfirm_WipesOnlyOwnTenant()
    {
        await PutProfileAsync("Sắp xóa");
        await SeedConversationAsync(TenantA, "s1", "public");
        await Scoped(async db =>
        {
            db.ProductKnowledge.Add(ProductKnowledge.Create(TenantA, Guid.NewGuid(), "Món A", 10, "v1"));
            db.ProductKnowledge.Add(ProductKnowledge.Create(TenantB, Guid.NewGuid(), "Món B", 20, "v1"));
            db.IngestLogs.Add(IngestLog.Create(TenantA, "products", "k1", 1, IngestStatus.Succeeded));
            db.IntentRoutes.Add(IntentRoute.Create(TenantA, "MenuQuery",
                KnowledgeSourceFlags.ProductKnowledge, true, null));
            await db.SaveChangesAsync();
        });

        // missing X-Confirm-Purge → 400
        var noConfirm = await _client.SendAsync(Req(HttpMethod.Delete, AdminPath()));
        noConfirm.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var req = Req(HttpMethod.Delete, AdminPath());
        req.Headers.Add("X-Confirm-Purge", TenantA.ToString());
        var res = await _client.SendAsync(req);
        res.StatusCode.Should().Be(HttpStatusCode.OK);
        var purge = await res.Content.ReadFromJsonAsync<PurgeResponse>();
        purge!.Messages.Should().BeGreaterThan(0);
        purge.Configs.Should().Be(1);
        purge.Products.Should().Be(1);

        var remaining = await Scoped(db => Task.FromResult(new
        {
            A = db.ConversationMessages.Count(m => m.TenantId == TenantA),
            B = db.ProductKnowledge.Count(p => p.TenantId == TenantB)
        }));
        remaining.A.Should().Be(0);
        remaining.B.Should().Be(1); // tenant B untouched
    }

    // ---------- task_ai_p10: docs list + profile version history ----------

    private async Task<Guid> SeedDocAsync(string title, string content)
    {
        var res = await _client.SendAsync(Req(HttpMethod.Post, "/v1/knowledge/documents",
            new DocumentIngestRequest("faq", title, content, "public")));
        res.StatusCode.Should().Be(HttpStatusCode.OK);
        var created = await res.Content.ReadFromJsonAsync<DocumentIngestResponse>();
        return created!.Id;
    }

    [Fact]
    public async Task Documents_List_FilterByStatus_IncludesContent()
    {
        var keep = await SeedDocAsync("FAQ giờ mở cửa", "Quán mở 9h-21h hằng ngày.");
        var other = await SeedDocAsync("FAQ wifi", "Wifi miễn phí cho khách.");

        var pending = await (await _client.SendAsync(
            Req(HttpMethod.Get, "/v1/admin/knowledge/documents?status=pending_review")))
            .Content.ReadFromJsonAsync<DocumentListResponse>();
        pending!.Total.Should().Be(2);
        pending.Items.Should().HaveCount(2);
        pending.Items.Select(i => i.Content).Should().Contain("Quán mở 9h-21h hằng ngày.");

        // approve one → filter "approved" sees only it; "rejected" empty
        (await _client.SendAsync(Req(HttpMethod.Post, $"/v1/admin/knowledge/{keep}/approve")))
            .StatusCode.Should().Be(HttpStatusCode.OK);

        var approved = await (await _client.SendAsync(
            Req(HttpMethod.Get, "/v1/admin/knowledge/documents?status=approved")))
            .Content.ReadFromJsonAsync<DocumentListResponse>();
        approved!.Total.Should().Be(1);
        approved.Items[0].Title.Should().Be("FAQ giờ mở cửa");
        approved.Items[0].ApprovalStatus.Should().Be("approved");

        var rejected = await (await _client.SendAsync(
            Req(HttpMethod.Get, "/v1/admin/knowledge/documents?status=rejected")))
            .Content.ReadFromJsonAsync<DocumentListResponse>();
        rejected!.Total.Should().Be(0);

        // invalid status → fail-closed empty page
        var bogus = await (await _client.SendAsync(
            Req(HttpMethod.Get, "/v1/admin/knowledge/documents?status=nonsense")))
            .Content.ReadFromJsonAsync<DocumentListResponse>();
        bogus!.Total.Should().Be(0);

        // no filter → all active docs
        var all = await (await _client.SendAsync(
            Req(HttpMethod.Get, "/v1/admin/knowledge/documents")))
            .Content.ReadFromJsonAsync<DocumentListResponse>();
        all!.Total.Should().Be(2);
        _ = other;
    }

    [Fact]
    public async Task Profile_IncludesRecentVersions_ForRollbackPicker()
    {
        await PutProfileAsync("v1");
        await _client.SendAsync(Req(HttpMethod.Post, AdminPath(suffix: "/publish")));
        await PutProfileAsync("v2");

        var profile = await (await _client.SendAsync(
            Req(HttpMethod.Get, AdminPath(suffix: "/profile"))))
            .Content.ReadFromJsonAsync<ProfileResponse>();
        profile!.RecentVersions.Select(v => v.Version).Should().Equal(2, 1);
        profile.RecentVersions[0].Status.Should().Be("draft");
        profile.RecentVersions[1].Status.Should().Be("published");
    }

    // ---------- regression: widget-config reads published persona ----------

    [Fact]
    public async Task WidgetConfig_AfterPublish_ReturnsPersona()
    {
        var persona = "{\"greeting\":\"Chào bạn đến An Tâm!\",\"suggestions\":[\"Menu hôm nay\",\"Món ít calo\"]}";
        (await _client.SendAsync(Req(HttpMethod.Put, AdminPath(suffix: "/profile"),
            new PutProfileRequest("Bé An Tâm", persona, null))))
            .StatusCode.Should().Be(HttpStatusCode.OK);
        await _client.SendAsync(Req(HttpMethod.Post, AdminPath(suffix: "/publish")));

        var widget = await (await _client.SendAsync(
            Req(HttpMethod.Get, "/v1/config/widget", caller: "gateway")))
            .Content.ReadFromJsonAsync<WidgetConfigResponse>();
        widget!.AiName.Should().Be("Bé An Tâm");
        widget.Greeting.Should().Be("Chào bạn đến An Tâm!");
        widget.Suggestions.Should().Contain("Menu hôm nay");
    }
}
