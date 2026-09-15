using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.DependencyInjection;
using VanAn.ShopERP.Services;
using Xunit;

namespace VanAn.Core.Tests.Services;

/// <summary>
/// CC-S4 (Issue #4): Unit tests for CustomerTokenService.CreateLongLivedToken.
/// Verifies long-lived tokens (365-day TTL for dev-token endpoint) are created and
/// validated correctly. Replaces the X-Dev-OTP bypass which was an unconditional
/// security hole.
/// </summary>
public class CustomerTokenServiceDevTokenTests
{
    private static ICustomerTokenService CreateService()
    {
        var services = new ServiceCollection();
        services.AddDataProtection();
        var provider = services.BuildServiceProvider();
        return new CustomerTokenService(provider.GetRequiredService<IDataProtectionProvider>());
    }

    [Fact]
    public void CreateLongLivedToken_Returns_NonEmptyToken()
    {
        var service = CreateService();
        var customerId = Guid.NewGuid();

        var token = service.CreateLongLivedToken(customerId, 365);

        Assert.False(string.IsNullOrEmpty(token));
        Assert.NotEqual(customerId.ToString(), token); // must be protected, not raw GUID
    }

    [Fact]
    public void CreateLongLivedToken_Validates_ToCorrectCustomerId()
    {
        var service = CreateService();
        var customerId = Guid.NewGuid();

        var token = service.CreateLongLivedToken(customerId, 365);
        var validatedId = service.ValidateToken(token);

        Assert.Equal(customerId, validatedId);
    }

    [Fact]
    public void CreateLongLivedToken_With1Day_Validates_BeforeExpiry()
    {
        var service = CreateService();
        var customerId = Guid.NewGuid();

        var token = service.CreateLongLivedToken(customerId, 1);
        var validatedId = service.ValidateToken(token);

        Assert.Equal(customerId, validatedId);
    }

    [Fact]
    public void CreateLongLivedToken_DiffersFrom_CreateToken()
    {
        // Long-lived token should have different expiry than standard 30-day token.
        // Both validate to same customerId, but the protected payloads differ.
        var service = CreateService();
        var customerId = Guid.NewGuid();

        var standardToken = service.CreateToken(customerId);
        var longLivedToken = service.CreateLongLivedToken(customerId, 365);

        Assert.NotEqual(standardToken, longLivedToken);
        Assert.Equal(customerId, service.ValidateToken(standardToken));
        Assert.Equal(customerId, service.ValidateToken(longLivedToken));
    }

    [Fact]
    public void ValidateToken_ReturnsNull_ForGarbageInput()
    {
        var service = CreateService();

        var result = service.ValidateToken("not-a-valid-token");

        Assert.Null(result);
    }

    [Fact]
    public void ValidateToken_ReturnsNull_ForEmptyInput()
    {
        var service = CreateService();

        var result = service.ValidateToken("");

        Assert.Null(result);
    }
}
