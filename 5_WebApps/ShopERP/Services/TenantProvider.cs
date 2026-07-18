using System.Security.Claims;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Extensions.DependencyInjection;
using VanAn.Shared.Domain.Common;

namespace VanAn.ShopERP.Services
{
    /// <summary>
    /// HTTP context-based ITenantProvider for ShopERP.
    /// Resolves TenantId and CurrentUser from JWT claims via IHttpContextAccessor.
    /// In Blazor Server interactive sessions, HttpContext is null — falls back to
    /// AuthenticationStateProvider to read claims from the circuit's auth state.
    /// </summary>
    public class HttpContextTenantProvider(
        IHttpContextAccessor httpContextAccessor,
        IServiceProvider? serviceProvider = null) : ITenantProvider
    {
        private readonly IHttpContextAccessor _httpContextAccessor = httpContextAccessor;
        private readonly IServiceProvider? _serviceProvider = serviceProvider;
        private Guid _overrideTenantId = Guid.Empty;
        private Guid _cachedTenantId = Guid.Empty;
        private bool _tenantIdCached = false;

        public Guid TenantId
        {
            get
            {
                if (_overrideTenantId != Guid.Empty)
                    return _overrideTenantId;

                if (_tenantIdCached)
                    return _cachedTenantId;

                // Path 1: HTTP context (API controllers + Blazor prerender)
                string? claim = _httpContextAccessor.HttpContext?.User
                    .FindFirstValue("tenant_id")
                    ?? _httpContextAccessor.HttpContext?.User
                    .FindFirstValue("tenantId");

                if (claim != null && Guid.TryParse(claim, out Guid id))
                {
                    _cachedTenantId = id;
                    _tenantIdCached = true;
                    return id;
                }

                // Path 2: Blazor Server interactive session (HttpContext is null)
                // Use AuthenticationStateProvider to read claims from the circuit
                if (_serviceProvider != null && _httpContextAccessor.HttpContext == null)
                {
                    try
                    {
                        var authStateProvider = _serviceProvider.GetService<AuthenticationStateProvider>();
                        if (authStateProvider != null)
                        {
                            var authState = authStateProvider.GetAuthenticationStateAsync()
                                .GetAwaiter().GetResult();
                            var blazorClaim = authState.User.FindFirst("tenant_id")
                                ?? authState.User.FindFirst("tenantId");
                            if (blazorClaim != null && Guid.TryParse(blazorClaim.Value, out Guid blazorId))
                            {
                                _cachedTenantId = blazorId;
                                _tenantIdCached = true;
                                return blazorId;
                            }
                        }
                    }
                    catch
                    {
                        // Fall through to Guid.Empty
                    }
                }

                _cachedTenantId = Guid.Empty;
                _tenantIdCached = true;
                return Guid.Empty;
            }
        }

        public string? CurrentUser =>
            _httpContextAccessor.HttpContext?.User.FindFirstValue(ClaimTypes.Name)
            ?? _httpContextAccessor.HttpContext?.User.FindFirstValue(ClaimTypes.Email);

        public bool HasTenant => TenantId != Guid.Empty;

        public void SetTenant(Guid tenantId) => _overrideTenantId = tenantId;
    }
}
