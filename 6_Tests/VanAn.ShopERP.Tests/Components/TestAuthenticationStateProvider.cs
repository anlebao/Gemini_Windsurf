using Microsoft.AspNetCore.Components.Authorization;
using System.Security.Claims;

namespace VanAn.ShopERP.Tests.Components;

/// <summary>
/// Mock AuthenticationStateProvider for testing
/// </summary>
public class TestAuthenticationStateProvider : AuthenticationStateProvider
{
    public override Task<AuthenticationState> GetAuthenticationStateAsync()
    {
        var identity = new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.Name, "Test User"),
            new Claim(ClaimTypes.NameIdentifier, "test-user-id"),
            new Claim("TenantId", Guid.NewGuid().ToString())
        }, "TestAuthentication");

        var user = new ClaimsPrincipal(identity);
        return Task.FromResult(new AuthenticationState(user));
    }
}
