using System.Security.Cryptography;
using System.Text;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using VanAn.Voice.Api.Services;

namespace VanAn.Voice.Tests;

public class TurnCredentialServiceTests
{
    private static TurnCredentialService Create(Dictionary<string, string?>? overrides = null)
    {
        var values = new Dictionary<string, string?>
        {
            ["TURN:PublicHost"] = "turn.example.com",
            ["TURN:UdpPort"] = "3478",
            ["TURN:TcpPort"] = "3478",
            ["TURN:TlsPort"] = "5349",
            ["TURN:Secret"] = "turn-secret",
            ["TURN:Realm"] = "khachvip.online",
            ["TURN:CredentialTtlSeconds"] = "1800"
        };
        if (overrides != null)
            foreach (var (k, v) in overrides) values[k] = v;

        var config = new ConfigurationBuilder().AddInMemoryCollection(values).Build();
        return new TurnCredentialService(config, NullLogger<TurnCredentialService>.Instance);
    }

    [Fact]
    public void Not_configured_returns_null()
    {
        var service = Create(new Dictionary<string, string?> { ["TURN:Secret"] = "" });

        var result = service.Issue(Guid.NewGuid(), DateTime.UtcNow);

        result.Should().BeNull();
        service.IsConfigured.Should().BeFalse();
    }

    [Fact]
    public void Issues_valid_turn_rest_credentials()
    {
        var service = Create();
        var callerId = Guid.NewGuid();
        var now = new DateTime(2026, 11, 11, 10, 0, 0, DateTimeKind.Utc);

        var result = service.Issue(callerId, now);

        result.Should().NotBeNull();
        result!.TtlSeconds.Should().Be(1800);
        result.ExpiresAtUtc.Should().Be(now.AddSeconds(1800));

        // username = "{unix}:{callerId}" (Coturn TURN REST scheme).
        var unix = new DateTimeOffset(now).ToUnixTimeSeconds();
        result.Username.Should().Be($"{unix}:{callerId:N}");

        // credential = base64(HMAC-SHA1(secret, username)) — recompute to verify the signature.
        using var hmac = new HMACSHA1(Encoding.UTF8.GetBytes("turn-secret"));
        var expected = Convert.ToBase64String(hmac.ComputeHash(Encoding.UTF8.GetBytes(result.Username)));
        result.Credential.Should().Be(expected);

        // URLs: UDP + TCP relay, TLS when configured.
        result.Urls.Should().Contain("turn:turn.example.com:3478?transport=udp");
        result.Urls.Should().Contain("turn:turn.example.com:3478?transport=tcp");
        result.Urls.Should().Contain("turns:turn.example.com:5349?transport=tcp");
    }

    [Fact]
    public void Ttl_is_configurable()
    {
        var service = Create(new Dictionary<string, string?> { ["TURN:CredentialTtlSeconds"] = "900" });

        var result = service.Issue(Guid.NewGuid(), DateTime.UtcNow);

        result!.TtlSeconds.Should().Be(900);
        result.ExpiresAtUtc.Should().BeCloseTo(DateTime.UtcNow.AddSeconds(900), TimeSpan.FromSeconds(2));
    }

    [Fact]
    public void No_tls_url_when_tls_port_disabled()
    {
        var service = Create(new Dictionary<string, string?> { ["TURN:TlsPort"] = "0" });

        var result = service.Issue(Guid.NewGuid(), DateTime.UtcNow);

        result!.Urls.Should().NotContain(u => u.StartsWith("turns:"));
    }
}