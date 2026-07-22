using Microsoft.AspNetCore.Components.Authorization;
using System.Security.Claims;

namespace VanAn.KhachLink.Services
{
    /// <summary>
    /// Anonymous AuthenticationStateProvider for Blazor WebAssembly.
    /// KhachLink is a customer-facing PWA with no server-side auth — tenant context
    /// comes from QR scan / LastInteractionService (localStorage), not auth claims.
    /// This stub satisfies TenantService's AuthenticationStateProvider dependency
    /// (required by UI.Platform.TenantService) without pulling in full auth infrastructure.
    /// GetAuthenticationStateAsync returns an anonymous user with no TenantId claim,
    /// so TenantService.GetCurrentTenantId() returns Guid.Empty — callers fall back
    /// to LastInteractionService for tenant context.
    /// </summary>
    public class AnonymousAuthenticationStateProvider : AuthenticationStateProvider
    {
        private static readonly AuthenticationState _anonymousState =
            new(new ClaimsPrincipal(new ClaimsIdentity()));

        public override Task<AuthenticationState> GetAuthenticationStateAsync()
            => Task.FromResult(_anonymousState);
    }
}
