using System.IdentityModel.Tokens.Jwt;
using Microsoft.Extensions.Configuration;
using VanAn.CoreHub.Services;
using VanAn.Shared.Domain;
using Xunit;
using DemoUser = VanAn.Shared.Domain.Aggregates.UserAggregate.DemoUser;
using UserRole = VanAn.Shared.Domain.Aggregates.UserAggregate.UserRole;

namespace VanAn.Core.Tests.Services;

/// <summary>
/// ATRS-3 (D3): token version — domain bump + JWT claim `tv` (revocation primitive).
/// </summary>
public class AtrsTokenVersionTests
{
    // ── Domain: DemoUser ──────────────────────────────────────────────

    [Fact]
    public void DemoUser_BumpTokenVersion_Increments()
    {
        var user = DemoUser.Create(
            new TenantId(Guid.NewGuid()), "owner", BCrypt.Net.BCrypt.HashPassword("x"), "Owner", UserRole.Owner);
        Assert.Equal(0, user.TokenVersion);

        user.BumpTokenVersion();
        Assert.Equal(1, user.TokenVersion);

        user.BumpTokenVersion();
        Assert.Equal(2, user.TokenVersion);
    }

    // ── Domain: Customer ──────────────────────────────────────────────

    [Fact]
    public void Customer_BumpTokenVersion_Increments()
    {
        var customer = new Customer(new TenantId(Guid.NewGuid()), "Khách hàng", "0900000000");
        Assert.Equal(0, customer.TokenVersion);

        customer.BumpTokenVersion();
        Assert.Equal(1, customer.TokenVersion);
    }

    // ── JwtTokenService: claim tv ─────────────────────────────────────

    [Fact]
    public void GenerateToken_WithTokenVersion_EmitsTvClaim()
    {
        var svc = BuildService();
        var token = svc.GenerateToken(
            userId: Guid.NewGuid(),
            email: "a@b.vn",
            role: UserRole.Owner,
            tenantId: Guid.NewGuid(),
            tokenVersion: 3);

        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(token);
        var tv = jwt.Claims.FirstOrDefault(c => c.Type == "tv")?.Value;
        Assert.Equal("3", tv);
    }

    [Fact]
    public void GenerateToken_WithoutTokenVersion_HasNoTvClaim()
    {
        var svc = BuildService();
        var token = svc.GenerateToken(
            userId: Guid.NewGuid(),
            email: "a@b.vn",
            role: UserRole.Owner,
            tenantId: Guid.NewGuid());

        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(token);
        Assert.DoesNotContain(jwt.Claims, c => c.Type == "tv");
    }

    [Fact]
    public void GenerateToken_StringRoleOverload_EmitsTvClaim()
    {
        var svc = BuildService();
        var token = svc.GenerateToken(
            userId: Guid.NewGuid(),
            email: "sys@b.vn",
            role: "SystemAdmin",
            tenantId: Guid.Empty,
            tokenVersion: 5);

        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(token);
        Assert.Equal("5", jwt.Claims.FirstOrDefault(c => c.Type == "tv")?.Value);
    }

    private static JwtTokenService BuildService()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Jwt:Secret"] = "test-secret-key-32-chars-minimum-length!!",
                ["Jwt:Issuer"] = "VanAn",
                ["Jwt:Audience"] = "VanAnApps"
            })
            .Build();
        return new JwtTokenService(config);
    }
}