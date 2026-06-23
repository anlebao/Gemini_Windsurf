using System.IdentityModel.Tokens.Jwt;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using VanAn.CoreHub.Services;
using VanAn.Shared.Domain;
using Xunit;
using UserRole = VanAn.Shared.Domain.Aggregates.UserAggregate.UserRole;

namespace VanAn.Tests.Services;

/// <summary>
/// Wave 0 — W0-T6: Unit tests for JwtTokenService.
/// Covers: generation, claims presence, expiry, tampered signature, wrong secret.
/// </summary>
public class JwtTokenServiceTests
{
    private const string ValidSecret = "VanAn-Test-Secret-Key-2026-@#$%^&*()";
    private const string ValidIssuer = "VanAnTest";
    private const string ValidAudience = "VanAnApiTest";

    private static JwtTokenService CreateService(string? secret = null, string? issuer = null, string? audience = null)
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Jwt:Secret"] = secret ?? ValidSecret,
                ["Jwt:Issuer"] = issuer ?? ValidIssuer,
                ["Jwt:Audience"] = audience ?? ValidAudience,
            })
            .Build();
        return new JwtTokenService(config);
    }

    [Fact]
    public void GenerateToken_ValidInput_ShouldReturnNonEmptyJwtString()
    {
        // Arrange
        var svc = CreateService();
        var userId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();

        // Act
        var token = svc.GenerateToken(userId, "admin@vanan.vn", UserRole.Owner, tenantId);

        // Assert
        token.Should().NotBeNullOrEmpty();
        // JWT has 3 parts separated by dots
        token.Split('.').Should().HaveCount(3);
    }

    [Fact]
    public void GenerateToken_ShouldContainRequiredClaims()
    {
        // Arrange
        var svc = CreateService();
        var userId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();

        // Act
        var token = svc.GenerateToken(userId, "admin@vanan.vn", UserRole.Owner, tenantId);

        // Assert: decode without validation to inspect claims
        var handler = new JwtSecurityTokenHandler();
        var jwt = handler.ReadJwtToken(token);

        jwt.Claims.Should().Contain(c => c.Type == JwtRegisteredClaimNames.Sub && c.Value == userId.ToString());
        jwt.Claims.Should().Contain(c => c.Type == JwtRegisteredClaimNames.Email && c.Value == "admin@vanan.vn");
        jwt.Claims.Should().Contain(c => c.Type == "tenant_id" && c.Value == tenantId.ToString());
        jwt.ValidTo.Should().BeAfter(DateTime.UtcNow);
    }

    [Fact]
    public void GenerateToken_ShouldExpireAfter8Hours()
    {
        // Arrange
        var svc = CreateService();
        var userId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();

        // Act
        var token = svc.GenerateToken(userId, "test@vanan.vn", UserRole.Staff, tenantId);

        // Assert: exp claim should be ~8 hours from now
        var handler = new JwtSecurityTokenHandler();
        var jwt = handler.ReadJwtToken(token);

        var expectedExpiry = DateTime.UtcNow.AddHours(8);
        jwt.ValidTo.Should().BeCloseTo(expectedExpiry, TimeSpan.FromMinutes(1));
    }

    [Fact]
    public void ValidateToken_TamperedSignature_ShouldThrow()
    {
        // Arrange
        var svc = CreateService();
        var token = svc.GenerateToken(Guid.NewGuid(), "test@vanan.vn", UserRole.Owner, Guid.NewGuid());

        // Tamper: replace the entire signature segment with a clearly invalid one
        // (single-char flip is unreliable with base64url padding on different runtimes)
        var parts = token.Split('.');
        parts[2] = "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA";
        var tamperedToken = string.Join('.', parts);

        // Act & Assert
        var act = () => svc.ValidateToken(tamperedToken);
        act.Should().Throw<Exception>("because tampered signature must fail validation");
    }

    [Fact]
    public void ValidateToken_WrongSecret_ShouldThrow()
    {
        // Arrange: generate token with one service, validate with another (different secret)
        var issuerSvc = CreateService(secret: "VanAn-Test-Secret-Key-2026-@#$%^&*()");
        var token = issuerSvc.GenerateToken(Guid.NewGuid(), "test@vanan.vn", UserRole.Owner, Guid.NewGuid());

        var validatorSvc = CreateService(secret: "DifferentSecret-Key-2026-@#$%^&*()!");

        // Act & Assert
        var act = () => validatorSvc.ValidateToken(token);
        act.Should().Throw<Exception>("because token signed with a different secret must fail validation");
    }

    [Fact]
    public void JwtTokenService_Constructor_ShouldThrow_IfSecretTooShort()
    {
        // Arrange: secret less than 32 chars
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Jwt:Secret"] = "short",
                ["Jwt:Issuer"] = ValidIssuer,
                ["Jwt:Audience"] = ValidAudience,
            })
            .Build();

        // Act & Assert
        var act = () => new JwtTokenService(config);
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*32 characters*");
    }
}
