using Microsoft.AspNetCore.Components.Authorization;
using System.Security.Claims;

namespace VanAn.ShopERP.Services
{
    /// <summary>
    /// Bridges Razor Pages authentication to Blazor components.
    /// Uses HttpContext to retrieve authentication state from cookie-based auth.
    /// </summary>
    public class HttpContextAuthenticationStateProvider(IHttpContextAccessor httpContextAccessor) : AuthenticationStateProvider
    {
        private readonly IHttpContextAccessor _httpContextAccessor = httpContextAccessor;

        public override async Task<AuthenticationState> GetAuthenticationStateAsync()
        {
            HttpContext? httpContext = _httpContextAccessor.HttpContext;

            if (httpContext == null)
            {
                return new AuthenticationState(new ClaimsPrincipal(new ClaimsIdentity()));
            }

            ClaimsPrincipal user = httpContext.User;

            return new AuthenticationState(user);
        }
    }
}
