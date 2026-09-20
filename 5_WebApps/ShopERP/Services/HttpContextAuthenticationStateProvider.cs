using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Server;
using System.Security.Claims;

namespace VanAn.ShopERP.Services
{
    /// <summary>
    /// Bridges Razor Pages authentication to Blazor components.
    /// Uses HttpContext to retrieve authentication state from cookie-based auth during SSR.
    /// Falls back to <see cref="ServerAuthenticationStateProvider"/> base when HttpContext is null
    /// (interactive circuit mode) — the framework sets the auth state via
    /// <see cref="IHostEnvironmentAuthenticationStateProvider"/> when the circuit is established.
    /// </summary>
    public class HttpContextAuthenticationStateProvider(IHttpContextAccessor httpContextAccessor) : ServerAuthenticationStateProvider
    {
        private readonly IHttpContextAccessor _httpContextAccessor = httpContextAccessor;

        public override async Task<AuthenticationState> GetAuthenticationStateAsync()
        {
            HttpContext? httpContext = _httpContextAccessor.HttpContext;

            if (httpContext != null)
            {
                return new AuthenticationState(httpContext.User);
            }

            try
            {
                // Interactive circuit: HttpContext is null, fall back to base
                // (ServerAuthenticationStateProvider gets state from the circuit connection)
                return await base.GetAuthenticationStateAsync();
            }
            catch (InvalidOperationException)
            {
                // No HttpContext AND no active circuit — i.e. called from a background service
                // (hosted job) outside any Razor component scope. There is no authenticated user
                // here; return an anonymous principal instead of throwing so callers that mint
                // system-level JWTs (GatewayAdminApiClientBase) fall back to their system defaults.
                return new AuthenticationState(new ClaimsPrincipal(new ClaimsIdentity()));
            }
        }
    }
}
