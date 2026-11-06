using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using VanAn.TenantAi.Ai;
using VanAn.TenantAi.Contracts;
using VanAn.TenantAi.Domain;
using VanAn.TenantAi.Infra;
using Xunit;

namespace VanAn.TenantAi.Tests.ThreatSuite;

/// <summary>
/// task_ai_p6 — shared WAF harness for the SRS §10 threat suite. Same pattern as
/// ChatApiTests (InMemory host, deterministic fakes) so every threat test runs the
/// real HTTP + auth + orchestrator pipeline.
/// </summary>
public abstract class ThreatTestBase : IDisposable
{
    protected static readonly Guid TenantA = TenantAiApiFactory.TenantA;
    protected static readonly Guid TenantB = TenantAiApiFactory.TenantB;

    protected readonly FakeLlmChatClient Llm = new();
    protected readonly FakeEmbeddingProvider Embed = new();
    protected readonly TenantAiApiFactory Factory;
    protected readonly HttpClient Client;

    protected ThreatTestBase(Action<IServiceCollection>? extra = null)
    {
        Factory = TenantAiApiFactory.WithServices(s =>
        {
            s.RemoveAll<IEmbeddingProvider>();
            s.AddSingleton<IEmbeddingProvider>(Embed);
            s.RemoveAll<ILlmChatClient>();
            s.AddSingleton<ILlmChatClient>(Llm);
            extra?.Invoke(s);
        });
        Client = Factory.CreateClient();
    }

    public void Dispose() => Factory.Dispose();

    protected static (string Ctx, string Sig) SignCtx(Guid tenantId, string callerId)
        => TenantContextSigner.Sign(
            TenantContextSigner.CreatePayload(tenantId, callerId, "req-" + Guid.NewGuid().ToString("N")[..8]),
            TenantAiApiFactory.SigningSecret);

    protected HttpRequestMessage Req(HttpMethod method, string path, string caller = "gateway",
        Guid? tenant = null, object? body = null, string? idem = null)
    {
        var r = new HttpRequestMessage(method, path);
        r.Headers.Add("X-Caller-Id", caller);
        r.Headers.Add("X-Ai-Key", caller == "gateway" ? TenantAiApiFactory.GatewayKey : TenantAiApiFactory.ShopKey);
        var (ctx, sig) = SignCtx(tenant ?? TenantA, caller);
        r.Headers.Add("X-Tenant-Ctx", ctx);
        r.Headers.Add("X-Tenant-Ctx-Sig", sig);
        if (idem != null) r.Headers.Add("Idempotency-Key", idem);
        if (body != null) r.Content = JsonContent.Create(body);
        return r;
    }

    protected HttpRequestMessage ChatReq(string text, Guid? tenant = null,
        string sessionId = "ts", string? messageId = null)
        => Req(HttpMethod.Post, "/v1/chat", "gateway", tenant,
            new ChatRequest(sessionId, messageId ?? "m-" + Guid.NewGuid().ToString("N")[..8], text));

    protected async Task<ChatResponse> Chat(HttpRequestMessage req)
    {
        var res = await Client.SendAsync(req);
        res.StatusCode.Should().Be(HttpStatusCode.OK);
        return (await res.Content.ReadFromJsonAsync<ChatResponse>())!;
    }

    protected async Task<T> Scoped<T>(Func<TenantAiDbContext, Task<T>> f)
    {
        await using var scope = Factory.Services.CreateAsyncScope();
        return await f(scope.ServiceProvider.GetRequiredService<TenantAiDbContext>());
    }

    protected async Task Scoped(Func<TenantAiDbContext, Task> f)
    {
        await using var scope = Factory.Services.CreateAsyncScope();
        await f(scope.ServiceProvider.GetRequiredService<TenantAiDbContext>());
    }

    protected async Task<Guid> SeedProductAsync(Guid tenant, string name, decimal price,
        int? calories = null, bool available = true)
    {
        var productId = Guid.NewGuid();
        await Scoped(db =>
        {
            var p = ProductKnowledge.Create(tenant, productId, name, price, "v1");
            p.UpdateFromSource(name, null, price, calories, "[]", "[]", available, "Món chính", "v1");
            db.ProductKnowledge.Add(p);
            return db.SaveChangesAsync();
        });
        return productId;
    }

    protected async Task<Guid> SeedDocAsync(Guid tenant, string type, string title, string content,
        DocumentClassification classification = DocumentClassification.Public, bool approve = true)
    {
        var doc = KnowledgeDocument.Create(tenant, type, title, content, classification);
        if (approve) doc.Approve("threat-suite");
        var id = doc.Id;
        await Scoped(async db =>
        {
            db.KnowledgeDocuments.Add(doc);
            if (approve)
                db.KnowledgeChunks.Add(KnowledgeChunk.Create(tenant, id, 0, content, null, 0));
            await db.SaveChangesAsync();
        });
        return id;
    }
}
