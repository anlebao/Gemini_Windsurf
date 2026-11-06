using System.Net;
using FluentAssertions;
using VanAn.TenantAi.Contracts;
using Xunit;

namespace VanAn.TenantAi.Tests;

/// <summary>P0b gate tests — auth 3 lớp (key → scope → signed ctx), threat T6/T12.</summary>
public sealed class AuthTests(TenantAiApiFactory factory) : IClassFixture<TenantAiApiFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    private static (string Ctx, string Sig) SignCtx(Guid tenantId, string callerId, long? iat = null, long? exp = null)
    {
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var payload = new TenantContextPayload(tenantId, callerId, iat ?? now, exp ?? now + 120, "req-1");
        return TenantContextSigner.Sign(payload, TenantAiApiFactory.SigningSecret);
    }

    private async Task<HttpResponseMessage> Send(HttpMethod method, string path, string caller, string key,
        (string Ctx, string Sig)? ctx = null)
    {
        using var r = new HttpRequestMessage(method, path);
        r.Headers.Add("X-Caller-Id", caller);
        r.Headers.Add("X-Ai-Key", key);
        if (ctx is { } c)
        {
            r.Headers.Add("X-Tenant-Ctx", c.Ctx);
            r.Headers.Add("X-Tenant-Ctx-Sig", c.Sig);
        }
        return await _client.SendAsync(r);
    }

    private async Task<HttpResponseMessage> SendRaw(HttpMethod method, string path, string caller, string key,
        string? ctxHeader, string? sigHeader)
    {
        using var r = new HttpRequestMessage(method, path);
        r.Headers.Add("X-Caller-Id", caller);
        r.Headers.Add("X-Ai-Key", key);
        if (ctxHeader != null) r.Headers.Add("X-Tenant-Ctx", ctxHeader);
        if (sigHeader != null) r.Headers.Add("X-Tenant-Ctx-Sig", sigHeader);
        return await _client.SendAsync(r);
    }

    [Fact]
    public async Task Health_NoAuth_200()
        => (await _client.GetAsync(new Uri("/v1/health", UriKind.Relative))).StatusCode.Should().Be(HttpStatusCode.OK);

    [Fact]
    public async Task NoKey_401()
        => (await _client.GetAsync(new Uri("/v1/auth/check", UriKind.Relative))).StatusCode.Should().Be(HttpStatusCode.Unauthorized);

    [Fact]
    public async Task BadKey_401()
        => (await Send(HttpMethod.Get, "/v1/auth/check", "gateway", "wrong"))
           .StatusCode.Should().Be(HttpStatusCode.Unauthorized);

    [Fact]
    public async Task GoodKey_AuthCheck_200()
    {
        var res = await Send(HttpMethod.Get, "/v1/auth/check", "gateway", TenantAiApiFactory.GatewayKey);
        res.StatusCode.Should().Be(HttpStatusCode.OK);
        (await res.Content.ReadAsStringAsync()).Should().Contain("gateway");
    }

    // T12: scope escalation — gateway (public-chat only) cannot hit ingest/admin routes.
    [Fact]
    public async Task Gateway_Key_IngestRoute_403()
    {
        var ctx = SignCtx(TenantAiApiFactory.TenantA, "gateway");
        var res = await Send(HttpMethod.Post, "/v1/knowledge/products:batch",
            "gateway", TenantAiApiFactory.GatewayKey, ctx);
        res.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Gateway_Key_AdminRoute_403()
    {
        var ctx = SignCtx(TenantAiApiFactory.TenantA, "gateway");
        var res = await Send(HttpMethod.Get,
            $"/v1/admin/tenants/{TenantAiApiFactory.TenantA}/profile",
            "gateway", TenantAiApiFactory.GatewayKey, ctx);
        res.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    // T6: tenant outside caller scope — shoperp-a allowed only TenantA.
    [Fact]
    public async Task ShopCaller_TenantOutOfScope_403()
    {
        var ctx = SignCtx(TenantAiApiFactory.TenantB, "shoperp-a");
        var res = await Send(HttpMethod.Post, "/v1/knowledge/products:batch",
            "shoperp-a", TenantAiApiFactory.ShopKey, ctx);
        res.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task ShopCaller_TenantInScope_PassesAuth_400()
    {
        // P2: route implemented — auth passes, endpoint rejects missing Idempotency-Key.
        var ctx = SignCtx(TenantAiApiFactory.TenantA, "shoperp-a");
        var res = await Send(HttpMethod.Post, "/v1/knowledge/products:batch",
            "shoperp-a", TenantAiApiFactory.ShopKey, ctx);
        res.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    // Signed ctx validation failures → 401.
    [Fact]
    public async Task MissingCtx_OnTenantEndpoint_401()
        => (await Send(HttpMethod.Post, "/v1/chat", "gateway", TenantAiApiFactory.GatewayKey))
           .StatusCode.Should().Be(HttpStatusCode.Unauthorized);

    [Fact]
    public async Task BadSignature_401()
    {
        var ctx = SignCtx(TenantAiApiFactory.TenantA, "gateway");
        var res = await SendRaw(HttpMethod.Post, "/v1/chat", "gateway",
            TenantAiApiFactory.GatewayKey, ctx.Ctx, "AAAA" + ctx.Sig[4..]);
        res.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task ExpiredCtx_401()
    {
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var ctx = SignCtx(TenantAiApiFactory.TenantA, "gateway", iat: now - 7200, exp: now - 3600);
        (await Send(HttpMethod.Post, "/v1/chat", "gateway", TenantAiApiFactory.GatewayKey, ctx))
            .StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task CallerIdMismatch_401()
    {
        var ctx = SignCtx(TenantAiApiFactory.TenantA, "shoperp-a"); // ctx nói shoperp-a
        (await Send(HttpMethod.Post, "/v1/chat", "gateway", TenantAiApiFactory.GatewayKey, ctx))
            .StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task TamperedPayload_401()
    {
        var good = SignCtx(TenantAiApiFactory.TenantA, "gateway");
        var forged = SignCtx(TenantAiApiFactory.TenantB, "gateway"); // ctx khác, sig của good
        var res = await SendRaw(HttpMethod.Post, "/v1/chat", "gateway",
            TenantAiApiFactory.GatewayKey, forged.Ctx, good.Sig);
        res.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // Valid full chain → reaches endpoint (501 stub), TenantId claim set.
    [Fact]
    public async Task ValidCtx_Chat_501()
    {
        var ctx = SignCtx(TenantAiApiFactory.TenantA, "gateway");
        (await Send(HttpMethod.Post, "/v1/chat", "gateway", TenantAiApiFactory.GatewayKey, ctx))
            .StatusCode.Should().Be(HttpStatusCode.NotImplemented);
    }

    [Fact]
    public async Task ValidCtx_TenantCheck_200_ReturnsClaims()
    {
        var ctx = SignCtx(TenantAiApiFactory.TenantA, "gateway");
        var res = await Send(HttpMethod.Get, "/v1/auth/tenant-check",
            "gateway", TenantAiApiFactory.GatewayKey, ctx);
        res.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await res.Content.ReadAsStringAsync();
        body.Should().Contain(TenantAiApiFactory.TenantA.ToString());
        body.Should().Contain("req-1");
    }
}
