using Microsoft.AspNetCore.Components.Authorization;
using System.Security.Claims;

namespace VanAn.ShopERP.Services
{
    /// <summary>
    /// Bridges Razor Pages authentication to Blazor components.
    /// Uses HttpContext to retrieve authentication state from cookie-based auth during SSR.
    /// In interactive circuit mode (HttpContext null), uses a cached state that the framework
    /// sets via <see cref="IHostEnvironmentAuthenticationStateProvider.SetAuthenticationState"/>.
    /// </summary>
    public class HttpContextAuthenticationStateProvider(IHttpContextAccessor httpContextAccessor)
        : AuthenticationStateProvider, IHostEnvironmentAuthenticationStateProvider
    {
        private readonly IHttpContextAccessor _httpContextAccessor = httpContextAccessor;
        private Task<AuthenticationState> _circuitAuthState =
            Task.FromResult(new AuthenticationState(new ClaimsPrincipal(new ClaimsIdentity())));

        public override Task<AuthenticationState> GetAuthenticationStateAsync()
        {
            HttpContext? httpContext = _httpContextAccessor.HttpContext;

            if (httpContext != null)
            {
                return Task.FromResult(new AuthenticationState(httpContext.User));
            }

            // Interactive circuit: HttpContext is null, use state set by the framework
            return _circuitAuthState;
        }

        void IHostEnvironmentAuthenticationStateProvider.SetAuthenticationState(Task<AuthenticationState> authenticationStateTask)
        {
            _circuitAuthState = authenticationStateTask ?? throw new ArgumentNullException(nameof(authenticationStateTask));
            NotifyAuthenticationStateChanged(_circuitAuthState);
        }
    }
}
