using Microsoft.AspNetCore.Components.Authorization;
using System.Security.Claims;

namespace VanAn.ShopERP.Tests.Components;

/// <summary>
/// Mock AuthenticationStateProvider for testing
/// </summary>
public class TestAuthenticationStateProvider : AuthenticationStateProvider
{
    private string[] _roles = [];

    /// <summary>
    /// W4 (P1.2): gán role claims cho test (vd. "Owner", "DebtAccountant") —
    /// cần cho UI role-gate assertions (nút Mở Lại Kỳ ẩn/hiện theo quyền).
    /// </summary>
    public void SetRoles(params string[] roles) => _roles = roles;

    public override Task<AuthenticationState> GetAuthenticationStateAsync()
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.Name, "Test User"),
            new(ClaimTypes.NameIdentifier, "test-user-id"),
            new("TenantId", Guid.NewGuid().ToString())
        };
        claims.AddRange(_roles.Select(r => new Claim(ClaimTypes.Role, r)));

        var identity = new ClaimsIdentity(claims, "TestAuthentication");

        var user = new ClaimsPrincipal(identity);
        return Task.FromResult(new AuthenticationState(user));
    }
}
