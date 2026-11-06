using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging.Abstractions;
using VanAn.TenantAi.Ai;
using VanAn.TenantAi.Api.Services;
using VanAn.TenantAi.Contracts;
using VanAn.TenantAi.Domain;
using VanAn.TenantAi.Infra;
using Xunit;

namespace VanAn.TenantAi.Tests;

/// <summary>task_ai_p5 — QuotaGate policy matrix [G7] PO-1 (unit, no HTTP).</summary>
public sealed class QuotaGateUnitTests
{
    private static readonly Guid Tenant = Guid.Parse("aaaaaaaa-0000-0000-0000-000000000009");
    private static readonly IConfiguration Config = new ConfigurationBuilder().Build();

    private static QuotaGate Gate(FakeMeteringService m, IConfiguration? cfg = null)
        => new(m, cfg ?? Config, NullLogger<QuotaGate>.Instance);

    [Fact]
    public async Task Unconfigured_Allows_FailOpen()
    {
        var m = new FakeMeteringService { IsConfigured = false };
        (await Gate(m).EvaluateAsync(Tenant)).Should().Be(QuotaOutcome.Allow);
        m.CheckCalls.Should().Be(0); // no outbound call when metering isn't deployed
    }

    [Fact]
    public async Task Deny_Refuses_AndCaches()
    {
        var m = new FakeMeteringService
        {
            CheckResponder = (_, _) => new MeteringCheckResult(0m, 1m, Deny: true, "depleted")
        };
        (await Gate(m).EvaluateAsync(Tenant)).Should().Be(QuotaOutcome.Refuse);
        m.CheckCalls.Should().Be(1);
    }

    [Fact]
    public async Task CheckFail_FreshCachedAllow_Allows()
    {
        var m = new FakeMeteringService
        {
            CheckResponder = (_, _) => new MeteringCheckResult(10m, 1m, Deny: false, null)
        };
        var gate = Gate(m);
        (await gate.EvaluateAsync(Tenant)).Should().Be(QuotaOutcome.Allow);
        m.CheckResponder = (_, _) => null; // metering down
        (await gate.EvaluateAsync(Tenant)).Should().Be(QuotaOutcome.Allow); // cached verdict wins
    }

    [Fact]
    public async Task CheckFail_FreshCachedDeny_Refuses()
    {
        var m = new FakeMeteringService
        {
            CheckResponder = (_, _) => new MeteringCheckResult(0m, 1m, Deny: true, null)
        };
        var gate = Gate(m);
        (await gate.EvaluateAsync(Tenant)).Should().Be(QuotaOutcome.Refuse);
        m.CheckResponder = (_, _) => null;
        (await gate.EvaluateAsync(Tenant)).Should().Be(QuotaOutcome.Refuse);
    }

    [Fact]
    public async Task CheckFail_NoCache_Degrades()
    {
        var m = new FakeMeteringService { CheckResponder = (_, _) => null };
        (await Gate(m).EvaluateAsync(Tenant)).Should().Be(QuotaOutcome.Degrade);
    }

    [Fact]
    public async Task CheckFail_ExpiredCache_Degrades()
    {
        var cfg = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["Metering:CheckCacheSeconds"] = "0" })
            .Build();
        var m = new FakeMeteringService
        {
            CheckResponder = (_, _) => new MeteringCheckResult(10m, 1m, Deny: false, null)
        };
        var gate = Gate(m, cfg);
        (await gate.EvaluateAsync(Tenant)).Should().Be(QuotaOutcome.Allow); // cached with TTL=0 → already stale
        m.CheckResponder = (_, _) => null;
        (await gate.EvaluateAsync(Tenant)).Should().Be(QuotaOutcome.Degrade);
    }
}

/// <summary>task_ai_p5 — end-to-end metering gate on the InMemory host (T8 core).</summary>
public sealed class MeteringGateApiTests : IDisposable
{
    private static readonly Guid TenantA = TenantAiApiFactory.TenantA;

    private readonly FakeLlmChatClient _llm = new();
    private readonly FakeMeteringService _metering = new();
    private readonly RecordingIngestQueue _ingest = new();
    private readonly TenantAiApiFactory _factory;
    private readonly HttpClient _client;

    public MeteringGateApiTests()
    {
        _factory = TenantAiApiFactory.WithServices(s =>
        {
            s.RemoveAll<ILlmChatClient>();
            s.AddSingleton<ILlmChatClient>(_llm);
            s.RemoveAll<IMeteringService>();
            s.AddSingleton<IMeteringService>(_metering);
            s.RemoveAll<IMeteringIngestQueue>();
            s.AddSingleton<IMeteringIngestQueue>(_ingest);
        });
        _client = _factory.CreateClient();
        _metering.CheckResponder = (_, _) => new MeteringCheckResult(100m, 1m, Deny: false, null);
    }

    public void Dispose() => _factory.Dispose();

    // ---------- helpers ----------

    private static (string Ctx, string Sig) SignCtx(Guid tenantId, string callerId)
        => TenantContextSigner.Sign(
            TenantContextSigner.CreatePayload(tenantId, callerId, "req-" + Guid.NewGuid().ToString("N")[..8]),
            TenantAiApiFactory.SigningSecret);

    private HttpRequestMessage Req(HttpMethod method, string path, Guid? tenant = null)
    {
        var r = new HttpRequestMessage(method, path);
        r.Headers.Add("X-Caller-Id", "gateway");
        r.Headers.Add("X-Ai-Key", TenantAiApiFactory.GatewayKey);
        var (ctx, sig) = SignCtx(tenant ?? TenantA, "gateway");
        r.Headers.Add("X-Tenant-Ctx", ctx);
        r.Headers.Add("X-Tenant-Ctx-Sig", sig);
        return r;
    }

    private HttpRequestMessage ChatReq(string text, string sessionId = "s1", string? messageId = null, Guid? tenant = null)
    {
        var r = Req(HttpMethod.Post, "/v1/chat", tenant);
        r.Content = JsonContent.Create(new ChatRequest(
            sessionId, messageId ?? "m-" + Guid.NewGuid().ToString("N")[..8], text));
        return r;
    }

    private async Task<ChatResponse> Chat(HttpRequestMessage req)
    {
        var res = await _client.SendAsync(req);
        res.StatusCode.Should().Be(HttpStatusCode.OK);
        return (await res.Content.ReadFromJsonAsync<ChatResponse>())!;
    }

    private async Task SeedProductAsync(string name, decimal price)
        => await SeedProductAsync(TenantA, name, price);

    private async Task SeedProductAsync(Guid tenant, string name, decimal price)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<TenantAiDbContext>();
        var p = ProductKnowledge.Create(tenant, Guid.NewGuid(), name, price, "v1");
        p.UpdateFromSource(name, null, price, null, "[]", "[]", true, "Món chính", "v1");
        db.ProductKnowledge.Add(p);
        await db.SaveChangesAsync();
    }

    // ---------- card test list ----------

    [Fact]
    public async Task Deny_ChatRefused_ZeroLlmCalls()
    {
        _metering.CheckResponder = (_, _) => new MeteringCheckResult(0m, 1m, Deny: true, "depleted");
        var body = await Chat(ChatReq("Giá phở chay bao nhiêu?"));
        body.Refused.Should().BeTrue();
        body.RefuseReason.Should().Be("quota_denied");
        body.Answer.Should().NotContain("credit").And.NotContain("số dư"); // không lộ ví
        _llm.Calls.Should().Be(0); // T8: 0 LLM khi deny
        // Refused → MeterOnly ingest, không charge unpaid.
        _ingest.Events.Should().ContainSingle().Which.ServiceCode.Should().Be(MeteringServiceCodes.ChatDegraded);
    }

    [Fact]
    public async Task CheckFail_ExpiredCache_Degraded_ZeroLlmCalls()
    {
        await SeedProductAsync("Phở chay", 80_000m);
        _metering.CheckResponder = (_, _) => null; // metering down, no cached verdict
        var body = await Chat(ChatReq("Giá phở chay bao nhiêu?"));
        body.Degraded.Should().BeTrue();
        body.Refused.Should().BeFalse();
        body.Answer.Should().Contain("Phở chay"); // structured-only fallback vẫn trả lời
        _llm.Calls.Should().Be(0); // T8 core: 0 LLM khi quota không xác minh được
        _ingest.Events.Should().ContainSingle().Which.ServiceCode.Should().Be(MeteringServiceCodes.ChatDegraded);
    }

    [Fact]
    public async Task CheckFail_FreshCache_AllowsNormalPath()
    {
        await SeedProductAsync("Phở chay", 80_000m);
        var first = await Chat(ChatReq("Giá phở chay bao nhiêu?", messageId: "c1"));
        first.Degraded.Should().BeFalse();
        _metering.CheckResponder = (_, _) => null; // metering down — cached allow còn 30s
        var second = await Chat(ChatReq("Giá phở chay bao nhiêu?", messageId: "c2"));
        second.Degraded.Should().BeFalse();
        second.Refused.Should().BeFalse();
    }

    [Fact]
    public async Task WalletOk_NormalPath_IngestsOnce_WithChatIdempotencyKey()
    {
        var mid = "m-ingest-1";
        var body = await Chat(ChatReq("Xin chào", messageId: mid));
        body.Refused.Should().BeFalse();
        var evt = _ingest.Events.Should().ContainSingle().Subject;
        evt.ServiceCode.Should().Be(MeteringServiceCodes.ChatMessage);
        evt.Quantity.Should().Be(1m);
        evt.IdempotencyKey.Should().Be($"chat:{TenantA}:{mid}");
    }

    [Fact]
    public async Task IngestThrow_ResponseStillServed()
    {
        _ingest.ThrowOnEnqueue = true;
        var body = await Chat(ChatReq("Xin chào"));
        body.Refused.Should().BeFalse();
    }

    [Fact]
    public async Task LlmDisabled_DegradesEveryRequest()
    {
        await SeedProductAsync("Phở chay", 80_000m);
        _llm.Enabled = false; // TenantAiLlm:ApiKey rỗng tương đương [G6]
        var body = await Chat(ChatReq("Giá phở chay bao nhiêu?"));
        body.Degraded.Should().BeTrue();
        body.Answer.Should().Contain("Phở chay");
        _llm.Calls.Should().Be(0);
    }

    [Fact]
    public async Task WidgetConfig_Deny_AiEnabledFalse()
    {
        _metering.CheckResponder = (_, _) => new MeteringCheckResult(0m, 1m, Deny: true, "depleted");
        var res = await _client.SendAsync(Req(HttpMethod.Get, "/v1/config/widget"));
        res.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = (await res.Content.ReadFromJsonAsync<WidgetConfigResponse>())!;
        body.AiEnabled.Should().BeFalse();
    }

    [Fact]
    public async Task WidgetConfig_Enabled_ReturnsConfigAndDefaults()
    {
        var res = await _client.SendAsync(Req(HttpMethod.Get, "/v1/config/widget"));
        res.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = (await res.Content.ReadFromJsonAsync<WidgetConfigResponse>())!;
        body.AiEnabled.Should().BeTrue();
        body.AiName.Should().NotBeNullOrWhiteSpace();
        body.Greeting.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task WidgetConfig_PublishedPersona_GreetingAndSuggestions()
    {
        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<TenantAiDbContext>();
            var cfg = TenantAiConfig.CreateDraft(TenantA, 1, "Bé Chay",
                "{\"greeting\":\"Chào bạn đến quán chay!\",\"suggestions\":[\"Menu hôm nay\",\"Món ít calo\"]}");
            cfg.MarkPublished("test");
            db.TenantAiConfigs.Add(cfg);
            await db.SaveChangesAsync();
        }
        var res = await _client.SendAsync(Req(HttpMethod.Get, "/v1/config/widget"));
        var body = (await res.Content.ReadFromJsonAsync<WidgetConfigResponse>())!;
        body.AiEnabled.Should().BeTrue();
        body.AiName.Should().Be("Bé Chay");
        body.Greeting.Should().Be("Chào bạn đến quán chay!");
        body.Suggestions.Should().Equal("Menu hôm nay", "Món ít calo");
    }
}

/// <summary>task_ai_p5 — kill switch TenantAi:Enabled=false (FR-9): refused mọi tenant.</summary>
public sealed class KillSwitchApiTests : IDisposable
{
    private static readonly Guid TenantA = TenantAiApiFactory.TenantA;

    private readonly FakeLlmChatClient _llm = new();
    private readonly FakeMeteringService _metering = new();
    private readonly TenantAiApiFactory _factory;
    private readonly HttpClient _client;

    public KillSwitchApiTests()
    {
        _factory = TenantAiApiFactory.With(
            configure: s =>
            {
                s.RemoveAll<ILlmChatClient>();
                s.AddSingleton<ILlmChatClient>(_llm);
                s.RemoveAll<IMeteringService>();
                s.AddSingleton<IMeteringService>(_metering);
            },
            settings: new Dictionary<string, string?> { ["TenantAi:Enabled"] = "false" });
        _client = _factory.CreateClient();
        _metering.CheckResponder = (_, _) => new MeteringCheckResult(100m, 1m, Deny: false, null);
    }

    public void Dispose() => _factory.Dispose();

    private HttpRequestMessage Req(HttpMethod method, string path)
    {
        var r = new HttpRequestMessage(method, path);
        r.Headers.Add("X-Caller-Id", "gateway");
        r.Headers.Add("X-Ai-Key", TenantAiApiFactory.GatewayKey);
        var (ctx, sig) = TenantContextSigner.Sign(
            TenantContextSigner.CreatePayload(TenantA, "gateway", "req-k1"),
            TenantAiApiFactory.SigningSecret);
        r.Headers.Add("X-Tenant-Ctx", ctx);
        r.Headers.Add("X-Tenant-Ctx-Sig", sig);
        return r;
    }

    [Fact]
    public async Task KillSwitch_ChatRefused_Politely()
    {
        var r = Req(HttpMethod.Post, "/v1/chat");
        r.Content = JsonContent.Create(new ChatRequest("s1", "m1", "Xin chào"));
        var res = await _client.SendAsync(r);
        res.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = (await res.Content.ReadFromJsonAsync<ChatResponse>())!;
        body.Refused.Should().BeTrue();
        body.RefuseReason.Should().Be("ai_disabled");
        _llm.Calls.Should().Be(0);
        _metering.CheckCalls.Should().Be(0); // kill switch trước quota gate
    }

    [Fact]
    public async Task KillSwitch_WidgetConfig_AiEnabledFalse()
    {
        var res = await _client.SendAsync(Req(HttpMethod.Get, "/v1/config/widget"));
        var body = (await res.Content.ReadFromJsonAsync<WidgetConfigResponse>())!;
        body.AiEnabled.Should().BeFalse();
    }
}
