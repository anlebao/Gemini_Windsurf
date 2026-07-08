using System.IdentityModel.Tokens.Jwt;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using VanAn.CoreHub.Infrastructure;
using VanAn.CoreHub.Infrastructure.Entities;
using VanAn.CoreHub.Services;
using VanAn.CoreHub.Tests.TestInfrastructure;
using VanAn.Shared.Domain.Common;
using Xunit;

namespace VanAn.Tests.Services;

/// <summary>
/// F3: Unit tests for PlatformUserLoginService.
/// Replaces the deleted PlatformUserLoginServiceTests.cs (commit 2a9313a) which used Moq
/// and could not mock IgnoreQueryFilters. This version uses SQLite in-memory via
/// VanAnDbContextTestFactory — same pattern as UserManagementServiceTests — so the
/// real EF Core query pipeline (including IgnoreQueryFilters) is exercised.
/// </summary>
public class PlatformUserLoginServiceTests : IDisposable
{
    private const string ValidSecret = "VanAn-Test-Secret-Key-2026-@#$%^&*()";
    private const string ValidIssuer = "VanAnTest";
    private const string ValidAudience = "VanAnApiTest";
    private const string TestUsername = "sysadmin@vanan.vn";
    private const string TestPassword = "VanAn@2026";

    private readonly TestContextScope _scope;
    private readonly VanAnDbContext _db;
    private readonly JwtTokenService _jwt;
    private readonly PlatformUserLoginService _sut;

    public PlatformUserLoginServiceTests()
    {
        _scope = VanAnDbContextTestFactory.Create();
        _db = _scope.Context;
        _jwt = CreateJwtService();
        _sut = new PlatformUserLoginService(_db, _jwt);
    }

    public void Dispose() => _scope.Dispose();

    private static JwtTokenService CreateJwtService()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Jwt:Secret"] = ValidSecret,
                ["Jwt:Issuer"] = ValidIssuer,
                ["Jwt:Audience"] = ValidAudience,
            })
            .Build();
        return new JwtTokenService(config);
    }

    private async Task<PlatformUser> SeedUserAsync(string username = TestUsername, bool active = true)
    {
        var hash = BCrypt.Net.BCrypt.HashPassword(TestPassword, 12);
        var user = new PlatformUser(username, hash, "System Admin", username);
        if (!active)
        {
            // PlatformUser.IsActive has private setter — use reflection to flip for the inactive test.
            // No public API to deactivate because platform users are managed via DB tooling (per task card Q3).
            typeof(PlatformUser)
                .GetProperty(nameof(PlatformUser.IsActive))!
                .SetValue(user, false);
        }
        _ = await _db.PlatformUsers.AddAsync(user);
        _ = await _db.SaveChangesAsync();
        _db.Entry(user).State = EntityState.Detached; // avoid tracker returning stale IsActive
        return user;
    }

    [Fact(DisplayName = "F3-S1: LoginAsync with valid credentials returns token and correct claims")]
    public async Task LoginAsync_ValidCredentials_ReturnsToken()
    {
        // Arrange
        await SeedUserAsync();

        // Act
        var result = await _sut.LoginAsync(TestUsername, TestPassword);

        // Assert
        result.Should().NotBeNull();
        result!.Email.Should().Be(TestUsername);
        result.Role.Should().Be(PlatformRole.SystemAdmin.ToString());
        result.Token.Should().NotBeEmpty();

        var handler = new JwtSecurityTokenHandler();
        var jwt = handler.ReadJwtToken(result.Token);
        // JwtTokenService uses ClaimTypes.Role (URI form), not the short "role" claim.
        jwt.Claims.Should().Contain(c => c.Type == System.Security.Claims.ClaimTypes.Role && c.Value == "SystemAdmin");
    }

    [Fact(DisplayName = "F3-S2: LoginAsync with wrong password returns null")]
    public async Task LoginAsync_WrongPassword_ReturnsNull()
    {
        // Arrange
        await SeedUserAsync();

        // Act
        var result = await _sut.LoginAsync(TestUsername, "WrongPassword");

        // Assert
        result.Should().BeNull();
    }

    [Fact(DisplayName = "F3-S3: LoginAsync with non-existent user returns null")]
    public async Task LoginAsync_NonExistentUser_ReturnsNull()
    {
        // Arrange — no seed

        // Act
        var result = await _sut.LoginAsync("nobody@vanan.vn", TestPassword);

        // Assert
        result.Should().BeNull();
    }

    [Fact(DisplayName = "F3-S4: LoginAsync with inactive user returns null")]
    public async Task LoginAsync_InactiveUser_ReturnsNull()
    {
        // Arrange
        await SeedUserAsync(active: false);

        // Act
        var result = await _sut.LoginAsync(TestUsername, TestPassword);

        // Assert
        result.Should().BeNull();
    }

    [Fact(DisplayName = "F3-S5: LoginAsync with empty username returns null")]
    public async Task LoginAsync_EmptyUsername_ReturnsNull()
    {
        // Arrange — no seed needed; query returns null regardless

        // Act
        var result = await _sut.LoginAsync("", TestPassword);

        // Assert
        result.Should().BeNull();
    }
}
