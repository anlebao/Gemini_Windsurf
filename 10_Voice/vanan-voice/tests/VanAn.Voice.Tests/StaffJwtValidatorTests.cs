using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using VanAn.Voice.Api.Auth;
using VanAn.Voice.Domain;

namespace VanAn.Voice.Tests;

public class StaffJwtValidatorTests
{
    private static StaffJwtValidator CreateValidator()
    {
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Jwt:Secret"] = TestJwt.Secret,
            ["Jwt:Issuer"] = TestJwt.Issuer,
            ["Jwt:Audience"] = TestJwt.Audience
        }).Build();
        return new StaffJwtValidator(config, NullLogger<StaffJwtValidator>.Instance);
    }

    [Fact]
    public async Task Valid_staff_jwt_resolves_identity()
    {
        var jwt = TestJwt.Mint(VoiceIdentities.UserA, VoiceIdentities.Tenant);
        var context = new DefaultHttpContext();
        context.Request.QueryString = new QueryString($"?access_token={jwt}");

        var identity = await CreateValidator().ValidateAsync(context);

        identity.Should().NotBeNull();
        identity!.UserId.Should().Be(VoiceIdentities.UserA);
        identity.Kind.Should().Be(VoiceIdentityKind.Staff);
        identity.TenantId.Should().Be(VoiceIdentities.Tenant);
    }

    [Fact]
    public async Task Valid_jwt_via_authorization_header()
    {
        var jwt = TestJwt.Mint(VoiceIdentities.UserA, VoiceIdentities.Tenant);
        var context = new DefaultHttpContext();
        context.Request.Headers.Authorization = $"Bearer {jwt}";

        var identity = await CreateValidator().ValidateAsync(context);

        identity.Should().NotBeNull();
        identity!.UserId.Should().Be(VoiceIdentities.UserA);
    }

    [Fact]
    public async Task Invalid_token_returns_null()
    {
        var context = new DefaultHttpContext();
        context.Request.QueryString = new QueryString("?access_token=not-a-real-jwt");

        (await CreateValidator().ValidateAsync(context)).Should().BeNull();
    }

    [Fact]
    public async Task Missing_credential_returns_null()
    {
        (await CreateValidator().ValidateAsync(new DefaultHttpContext())).Should().BeNull();
    }
}