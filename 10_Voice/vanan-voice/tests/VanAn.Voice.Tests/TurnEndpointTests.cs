using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using System.Net.Http.Json;
using Xunit;
using VanAn.Voice.Api;

namespace VanAn.Voice.Tests;

/// <summary>GET /v1/turn/credentials — auth + issuance shape (SRS §8.2, SEC-004).</summary>
public class TurnEndpointTests
{
    private static WebApplicationFactory<VoiceProgram> CreateFactory(bool turnConfigured)
    {
        var dbPath = Path.Combine(Path.GetTempPath(), $"voice-turn-{Guid.NewGuid():N}.db");
        return new WebApplicationFactory<VoiceProgram>().WithWebHostBuilder(b =>
        {
            b.UseSetting("Jwt:Secret", TestJwt.Secret);
            b.UseSetting("Jwt:Issuer", TestJwt.Issuer);
            b.UseSetting("Jwt:Audience", TestJwt.Audience);
            b.UseSetting("Voice:DbPath", dbPath);
            b.UseSetting("Voice:GatewayDb", "");
            b.UseSetting("Voice:ShopErpBaseUrl", "");
            b.UseSetting("Metering:BaseUrl", "");
            b.UseSetting("Metering:CallerId", "");
            b.UseSetting("Metering:CallerKey", "");
            b.UseSetting("TURN:PublicHost", turnConfigured ? "turn.example.com" : "");
            b.UseSetting("TURN:Secret", turnConfigured ? "turn-secret" : "");
        });
    }

    [Fact]
    public async Task Requires_authentication()
    {
        var factory = CreateFactory(turnConfigured: true);
        await using var _ = factory;

        var response = await factory.CreateClient().GetAsync("/v1/turn/credentials");

        response.StatusCode.Should().Be(System.Net.HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Returns_credentials_for_authenticated_user()
    {
        var factory = CreateFactory(turnConfigured: true);
        await using var _ = factory;
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer",
                TestJwt.Mint(VoiceIdentities.UserA, VoiceIdentities.Tenant));

        var response = await client.GetAsync("/v1/turn/credentials");

        response.StatusCode.Should().Be(System.Net.HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<TurnCredentialsBody>();
        body.Should().NotBeNull();
        body!.Urls.Should().Contain("turn:turn.example.com:3478?transport=udp");
        body.Username.Should().Contain(":");
        body.Credential.Should().NotBeNullOrWhiteSpace();
        body.TtlSeconds.Should().Be(1800);
    }

    [Fact]
    public async Task Service_unavailable_when_turn_not_configured()
    {
        var factory = CreateFactory(turnConfigured: false);
        await using var _ = factory;
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer",
                TestJwt.Mint(VoiceIdentities.UserA, VoiceIdentities.Tenant));

        var response = await client.GetAsync("/v1/turn/credentials");

        response.StatusCode.Should().Be(System.Net.HttpStatusCode.ServiceUnavailable);
    }

    private sealed class TurnCredentialsBody
    {
        public List<string> Urls { get; set; } = [];
        public string Username { get; set; } = string.Empty;
        public string Credential { get; set; } = string.Empty;
        public int TtlSeconds { get; set; }
    }
}