using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using VanAn.CoreHub.Infrastructure;
using VanAn.Gateway.Controllers;
using VanAn.Gateway.Services;
using VanAn.Shared.Domain;
using VanAn.Shared.Domain.Aggregates.KhachLinkAggregate;
using VanAn.Shared.Domain.Aggregates.TenantAggregate;
using VanAn.Shared.Services;
using Xunit;

namespace VanAn.Core.Tests.TenantAi;

/// <summary>
/// Task AI-P4 — gateway surface tests: server-side tenant resolution (T6),
/// signed ctx, session cap 20/giờ, graceful degradation.
/// </summary>
public class PublicTenantAiControllerTests
{
    private static readonly Guid TenantA = Guid.NewGuid();
    private static readonly Guid TenantB = Guid.NewGuid();
    private const string SigningSecret = "test-signing-secret-0123456789";
    private const string HostA = "shopa.khachvip.online";

    // ── Helpers ──────────────────────────────────────────────────────────

    private sealed class StubHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> send) : HttpMessageHandler
    {
        public int Requests { get; private set; }
        public HttpRequestMessage? LastRequest { get; private set; }
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests++;
            LastRequest = request;
            return send(request, cancellationToken);
        }
    }

    private sealed class FakeFeatureSettings(bool aiEnabled) : IShopFeatureSettingsService
    {
        public Task<ShopFeatureSettingsDto> GetSettingsAsync(Guid tenantId, CancellationToken ct = default)
            => Task.FromResult(new ShopFeatureSettingsDto { AIChat_Enabled = aiEnabled });
        public Task<ShopFeatureSettingsDto> UpdateSettingsAsync(Guid tenantId, ShopFeatureSettingsDto settings, CancellationToken ct = default)
            => Task.FromResult(settings);
        public Task<bool> IsEnabledAsync(Guid tenantId, string toggleName, CancellationToken ct = default)
            => Task.FromResult(aiEnabled);
    }

    private static (VanAnDbContext Ctx, SqliteConnection Conn) NewDb()
    {
        var conn = new SqliteConnection($"DataSource=test_{Guid.NewGuid()};Mode=Memory;Cache=Shared");
        conn.Open();
        var efProvider = new ServiceCollection().AddEntityFrameworkSqlite().BuildServiceProvider();
        var options = new DbContextOptionsBuilder<VanAnDbContext>()
            .UseInternalServiceProvider(efProvider).UseSqlite(conn)
            .ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        var ctx = new VanAnDbContext(options);
        ctx.Database.EnsureCreated();
        return (ctx, conn);
    }

    private static async Task SeedTenantAsync(VanAnDbContext ctx, Guid tenantId)
    {
        ctx.Tenants.Add(VanAn.Shared.Domain.Aggregates.TenantAggregate.Tenant.CreateCompany(new TenantId(tenantId), "Test Store"));
        await ctx.SaveChangesAsync();
    }

    private static async Task SeedBoundInstanceAsync(VanAnDbContext ctx, string domain, Guid ownerTenantId)
    {
        ctx.KhachLinkInstances.Add(new KhachLinkInstance("Shop A", KhachLinkProfile.FullCommerce, domain, ownerTenantId));
        await ctx.SaveChangesAsync();
    }

    private static IConfiguration Config(int? sessionCap = null) => new ConfigurationBuilder()
        .AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["TenantAi:BaseUrl"] = "http://vanan-ai.test/",
            ["TenantAi:CtxSigningSecret"] = SigningSecret,
            ["TenantAi:CallerId"] = "gateway",
            ["TenantAi:GatewayCallerKey"] = "caller-key",
            ["TenantAi:SessionCap"] = sessionCap?.ToString() ?? "20"
        })
        .Build();

    private static TenantAiForwarder NewForwarder(StubHandler handler, IConfiguration? config = null)
        => new(new HttpClient(handler) { BaseAddress = new Uri("http://vanan-ai.test/") },
            config ?? Config(), NullLogger<TenantAiForwarder>.Instance);

    private static PublicTenantAiController NewController(
        VanAnDbContext ctx, TenantAiForwarder forwarder, bool aiEnabled = true,
        string? host = null, int? sessionCap = null)
    {
        var http = new DefaultHttpContext();
        if (host is not null) http.Request.Host = new HostString(host);
        return new PublicTenantAiController(
            ctx,
            new FakeFeatureSettings(aiEnabled),
            forwarder,
            new ServiceCollection().AddMemoryCache().BuildServiceProvider().GetRequiredService<IMemoryCache>(),
            Config(sessionCap),
            NullLogger<PublicTenantAiController>.Instance)
        { ControllerContext = new ControllerContext { HttpContext = http } };
    }

    private static PublicAiChatRequest ChatReq(string sessionId = "s1", string? messageId = null, Guid? tenantId = null)
        => new()
        {
            TenantId = tenantId,
            SessionId = sessionId,
            MessageId = messageId ?? Guid.NewGuid().ToString("N"),
            Text = "Quán có phở chay không?"
        };

    /// <summary>Decode the X-Tenant-Ctx payload the forwarder sent (base64url JSON).</summary>
    private static JsonElement DecodeCtx(HttpRequestMessage req)
    {
        var ctx = req.Headers.GetValues("X-Tenant-Ctx").Single();
        var padded = ctx.Replace('-', '+').Replace('_', '/');
        padded = padded.PadRight(padded.Length + (4 - padded.Length % 4) % 4, '=');
        return JsonSerializer.Deserialize<JsonElement>(Convert.FromBase64String(padded));
    }

    private static HttpResponseMessage OkJson(string body)
        => new(HttpStatusCode.OK) { Content = new StringContent(body, Encoding.UTF8, "application/json") };

    private const string ChatOkJson = "{\"answer\":\"Quán có phở chay 80k.\",\"products\":[],\"sourceIds\":[],\"confidence\":0.9,\"refused\":false,\"refuseReason\":null,\"staleData\":false,\"degraded\":false}";

    // ── T6: tenant resolution ────────────────────────────────────────────

    [Fact]
    public async Task Chat_InstanceBoundHost_IgnoresClientTenantId_T6()
    {
        var (ctx, conn) = NewDb();
        await using var _ = ctx; await using var __ = conn;
        await SeedTenantAsync(ctx, TenantA);
        await SeedTenantAsync(ctx, TenantB);
        await SeedBoundInstanceAsync(ctx, HostA, TenantA);

        var handler = new StubHandler((_, _) => Task.FromResult(OkJson(ChatOkJson)));
        var ctrl = NewController(ctx, NewForwarder(handler), host: HostA);

        // Client spoofs tenant B — server must sign ctx for the BOUND tenant A.
        var result = await ctrl.Chat(ChatReq(tenantId: TenantB));

        result.Should().BeOfType<ContentResult>();
        handler.LastRequest.Should().NotBeNull();
        DecodeCtx(handler.LastRequest!).GetProperty("tenantId").GetGuid().Should().Be(TenantA);
    }

    [Fact]
    public async Task Chat_UnboundHost_ValidatedClientTenantId_Forwards()
    {
        var (ctx, conn) = NewDb();
        await using var _ = ctx; await using var __ = conn;
        await SeedTenantAsync(ctx, TenantA);

        var handler = new StubHandler((_, _) => Task.FromResult(OkJson(ChatOkJson)));
        var ctrl = NewController(ctx, NewForwarder(handler), host: "diemthuong2.khachvip.online");

        var result = await ctrl.Chat(ChatReq(tenantId: TenantA));

        result.Should().BeOfType<ContentResult>();
        DecodeCtx(handler.LastRequest!).GetProperty("tenantId").GetGuid().Should().Be(TenantA);
    }

    [Fact]
    public async Task Chat_UnknownTenant_NotFound_NoForward()
    {
        var (ctx, conn) = NewDb();
        await using var _ = ctx; await using var __ = conn;

        var handler = new StubHandler((_, _) => Task.FromResult(OkJson(ChatOkJson)));
        var ctrl = NewController(ctx, NewForwarder(handler), host: "diemthuong2.khachvip.online");

        var result = await ctrl.Chat(ChatReq(tenantId: Guid.NewGuid()));

        result.Should().BeOfType<NotFoundObjectResult>();
        handler.Requests.Should().Be(0, "unresolvable tenant must never reach vanan-ai");
    }

    // ── Session cap [G8 PO-3] ────────────────────────────────────────────

    [Fact]
    public async Task Chat_21stMessageInSession_Returns429()
    {
        var (ctx, conn) = NewDb();
        await using var _ = ctx; await using var __ = conn;
        await SeedTenantAsync(ctx, TenantA);

        var handler = new StubHandler((_, _) => Task.FromResult(OkJson(ChatOkJson)));
        var ctrl = NewController(ctx, NewForwarder(handler), host: "marketplace.test");

        for (var i = 0; i < 20; i++)
            (await ctrl.Chat(ChatReq(sessionId: "cap-s", tenantId: TenantA)))
                .Should().BeOfType<ContentResult>();

        var blocked = await ctrl.Chat(ChatReq(sessionId: "cap-s", tenantId: TenantA));
        blocked.Should().BeOfType<ObjectResult>()
            .Which.StatusCode.Should().Be(StatusCodes.Status429TooManyRequests);
        handler.Requests.Should().Be(20, "the capped message must not be forwarded");

        // A different session on the same tenant is unaffected.
        (await ctrl.Chat(ChatReq(sessionId: "other", tenantId: TenantA)))
            .Should().BeOfType<ContentResult>();
    }

    // ── Graceful degradation (NFR-3) ─────────────────────────────────────

    [Fact]
    public async Task Chat_Upstream5xx_Returns503Graceful()
    {
        var (ctx, conn) = NewDb();
        await using var _ = ctx; await using var __ = conn;
        await SeedTenantAsync(ctx, TenantA);

        var handler = new StubHandler((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.InternalServerError)));
        var ctrl = NewController(ctx, NewForwarder(handler), host: "marketplace.test");

        var result = await ctrl.Chat(ChatReq(tenantId: TenantA));

        result.Should().BeOfType<ObjectResult>()
            .Which.StatusCode.Should().Be(StatusCodes.Status503ServiceUnavailable);
    }

    [Fact]
    public async Task Chat_FeatureFlagOff_Returns503_NoForward()
    {
        var (ctx, conn) = NewDb();
        await using var _ = ctx; await using var __ = conn;
        await SeedTenantAsync(ctx, TenantA);

        var handler = new StubHandler((_, _) => Task.FromResult(OkJson(ChatOkJson)));
        var ctrl = NewController(ctx, NewForwarder(handler), aiEnabled: false, host: "marketplace.test");

        var result = await ctrl.Chat(ChatReq(tenantId: TenantA));

        result.Should().BeOfType<ObjectResult>()
            .Which.StatusCode.Should().Be(StatusCodes.Status503ServiceUnavailable);
        handler.Requests.Should().Be(0);
    }

    [Fact]
    public async Task WidgetConfig_ForwarderNotConfigured_ReturnsAiDisabled()
    {
        var (ctx, conn) = NewDb();
        await using var _ = ctx; await using var __ = conn;
        await SeedTenantAsync(ctx, TenantA);

        // No TenantAi:BaseUrl + no signing secret → forwarder self-disables.
        var emptyConfig = new ConfigurationBuilder().Build();
        var forwarder = new TenantAiForwarder(new HttpClient(new StubHandler((_, _) =>
            Task.FromResult(OkJson("{}")))), emptyConfig, NullLogger<TenantAiForwarder>.Instance);
        forwarder.IsEnabled.Should().BeFalse();

        var ctrl = NewController(ctx, forwarder, host: "marketplace.test");
        var result = await ctrl.GetWidgetConfig(TenantA);

        result.Should().BeOfType<OkObjectResult>()
            .Which.Value.Should().BeEquivalentTo(new { aiEnabled = false });
    }

    [Fact]
    public async Task WidgetConfig_UpstreamError_GracefulDisabled()
    {
        var (ctx, conn) = NewDb();
        await using var _ = ctx; await using var __ = conn;
        await SeedTenantAsync(ctx, TenantA);

        // Upstream 501 (P5 stub) → aiEnabled:false (200 — feature simply off).
        var handler = new StubHandler((_, _) => Task.FromResult(
            new HttpResponseMessage(HttpStatusCode.NotImplemented)));
        var ctrl = NewController(ctx, NewForwarder(handler), host: "marketplace.test");

        var result = await ctrl.GetWidgetConfig(TenantA);
        result.Should().BeOfType<OkObjectResult>()
            .Which.Value.Should().BeEquivalentTo(new { aiEnabled = false });
    }

    [Fact]
    public async Task WidgetConfig_UpstreamOk_PassesBodyThrough()
    {
        var (ctx, conn) = NewDb();
        await using var _ = ctx; await using var __ = conn;
        await SeedTenantAsync(ctx, TenantA);

        var handler = new StubHandler((_, _) => Task.FromResult(
            OkJson("{\"aiEnabled\":true,\"aiName\":\"Bé AI\",\"greeting\":\"Chào bạn!\",\"suggestions\":[\"Menu hôm nay?\"]}")));
        var ctrl = NewController(ctx, NewForwarder(handler), host: "marketplace.test");

        var result = await ctrl.GetWidgetConfig(TenantA);
        result.Should().BeOfType<ContentResult>()
            .Which.Content.Should().Contain("\"aiEnabled\":true");
    }

    [Fact]
    public async Task SessionMessages_UpstreamOk_PassesBodyThrough()
    {
        var (ctx, conn) = NewDb();
        await using var _ = ctx; await using var __ = conn;
        await SeedTenantAsync(ctx, TenantA);

        var handler = new StubHandler((req, _) =>
        {
            req.RequestUri!.AbsolutePath.Should().Be("/v1/chat/sessions/sess-1/messages");
            return Task.FromResult(OkJson("{\"sessionId\":\"sess-1\",\"messages\":[]}"));
        });
        var ctrl = NewController(ctx, NewForwarder(handler), host: "marketplace.test");

        var result = await ctrl.GetSessionMessages("sess-1", TenantA);
        result.Should().BeOfType<ContentResult>()
            .Which.Content.Should().Contain("\"sessionId\":\"sess-1\"");
    }

    // ── Signing (test vector cố định — T6/D7) ────────────────────────────

    [Fact]
    public void Signer_DeterministicVector()
    {
        var payload = new TenantAiContextPayload(TenantA, "gateway", 1_700_000_000, 1_700_000_120, "req-1");
        var (ctx, sig) = TenantAiContextSigner.Sign(payload, SigningSecret);

        // Deterministic: same input → same output.
        var again = TenantAiContextSigner.Sign(payload, SigningSecret);
        again.Should().Be((ctx, sig));

        // ctx decodes to the payload.
        var padded = ctx.Replace('-', '+').Replace('_', '/');
        padded = padded.PadRight(padded.Length + (4 - padded.Length % 4) % 4, '=');
        var decoded = JsonSerializer.Deserialize<JsonElement>(Convert.FromBase64String(padded));
        decoded.GetProperty("tenantId").GetGuid().Should().Be(TenantA);
        decoded.GetProperty("callerId").GetString().Should().Be("gateway");
        decoded.GetProperty("requestId").GetString().Should().Be("req-1");

        // sig = base64url(HMAC-SHA256(secret, ctx)) — computed independently.
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(SigningSecret));
        var expected = Convert.ToBase64String(hmac.ComputeHash(Encoding.UTF8.GetBytes(ctx)))
            .TrimEnd('=').Replace('+', '-').Replace('/', '_');
        sig.Should().Be(expected);
    }
}
