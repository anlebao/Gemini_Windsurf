using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using VanAn.CoreHub.Infrastructure;
using VanAn.CoreHub.Infrastructure.Entities;
using VanAn.Integration.Tests.Infrastructure;
using VanAn.ShopERP.Infrastructure;
using Xunit;

namespace VanAn.Integration.Tests;

/// <summary>
/// Platform SystemAdmin — Integration tests for PlatformUserLoginController.
/// Verifies the full flow: login request → service → BCrypt verify → JWT mint → response.
/// Uses CustomWebApplicationFactory (SQLite in-memory, EnsureCreated schema).
/// </summary>
[Trait("Category", "Integration")]
public class PlatformUserLoginEndpointTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public PlatformUserLoginEndpointTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    // F2: idempotent — Program.cs seed (L435-451) already runs inside WebApplicationFactory<Program>,
    // so the same sysadmin@vanan.vn row exists. Insert again → UNIQUE constraint fail (Deviation #2).
    // Fix: check existing first, reuse if present. Tests don't rely on a specific password hash
    // because the test uses the actual Program.cs-seeded user (password "VanAn@2026").
    private async Task SeedPlatformUserAsync()
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ShopERPDbContext>();
        var existing = await db.PlatformUsers.IgnoreQueryFilters()
            .FirstOrDefaultAsync(u => u.Username == "sysadmin@vanan.vn");
        if (existing != null)
        {
            return; // Program.cs seed already inserted the user
        }
        var passwordHash = BCrypt.Net.BCrypt.HashPassword("VanAn@2026", 12);
        var user = new PlatformUser("sysadmin@vanan.vn", passwordHash, "System Admin", "sysadmin@vanan.vn");
        _ = db.PlatformUsers.Add(user);
        _ = await db.SaveChangesAsync();
    }

    [Fact(DisplayName = "Platform login: correct credentials returns 200 OK with token")]
    public async Task Login_CorrectCredentials_Returns200OkWithToken()
    {
        // Arrange
        await SeedPlatformUserAsync();

        var request = new
        {
            Username = "sysadmin@vanan.vn",
            Password = "VanAn@2026"
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/platform/login", request);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<LoginResponse>();
        Assert.NotNull(result);
        Assert.True(result!.Success);
        Assert.Equal("SystemAdmin", result.Role);
        Assert.Equal("sysadmin@vanan.vn", result.Email);
        Assert.NotEmpty(result.Token);
    }

    [Fact(DisplayName = "Platform login: wrong password returns 401 Unauthorized")]
    public async Task Login_WrongPassword_Returns401Unauthorized()
    {
        // Arrange
        await SeedPlatformUserAsync();

        var request = new
        {
            Username = "sysadmin@vanan.vn",
            Password = "WrongPassword"
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/platform/login", request);

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<ErrorResponse>();
        Assert.NotNull(result);
        Assert.False(result!.Success);
        Assert.Equal("Invalid credentials", result.Message);
    }

    [Fact(DisplayName = "Platform login: nonexistent user returns 401 Unauthorized")]
    public async Task Login_NonexistentUser_Returns401Unauthorized()
    {
        // Arrange
        var request = new
        {
            Username = "nonexistent@vanan.vn",
            Password = "VanAn@2026"
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/platform/login", request);

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<ErrorResponse>();
        Assert.NotNull(result);
        Assert.False(result!.Success);
        Assert.Equal("Invalid credentials", result.Message);
    }

    private record LoginResponse(bool Success, string Email, string Role, string Token, string Message);
    private record ErrorResponse(bool Success, string Message);
}
