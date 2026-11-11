using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;
using VanAn.Voice.Api;

namespace VanAn.Voice.Tests;

/// <summary>GET /v1/missed-calls/{userId} — authenticated, own events only (FR-014, SEC-002).</summary>
public class MissedCallsEndpointTests
{
    private static WebApplicationFactory<VoiceProgram> CreateFactory()
    {
        var dbPath = Path.Combine(Path.GetTempPath(), $"voice-missed-{Guid.NewGuid():N}.db");
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
        });
    }

    [Fact]
    public async Task Requires_authentication()
    {
        var factory = CreateFactory();
        await using var _ = factory;

        var response = await factory.CreateClient().GetAsync($"/v1/missed-calls/{VoiceIdentities.UserA}");

        response.StatusCode.Should().Be(System.Net.HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Rejects_reading_another_users_missed_calls()
    {
        var factory = CreateFactory();
        await using var _ = factory;
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer",
                TestJwt.Mint(VoiceIdentities.UserA, VoiceIdentities.Tenant));

        var response = await client.GetAsync($"/v1/missed-calls/{VoiceIdentities.UserB}");

        response.StatusCode.Should().Be(System.Net.HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Returns_own_missed_calls()
    {
        var factory = CreateFactory();
        await using var _ = factory;
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer",
                TestJwt.Mint(VoiceIdentities.UserA, VoiceIdentities.Tenant));

        var response = await client.GetAsync($"/v1/missed-calls/{VoiceIdentities.UserA}");

        response.StatusCode.Should().Be(System.Net.HttpStatusCode.OK);
        var body = await response.Content.ReadAsStringAsync();
        body.Should().Be("[]"); // no missed calls yet
    }
}