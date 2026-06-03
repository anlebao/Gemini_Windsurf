using Microsoft.AspNetCore.Components.Authorization;

namespace VanAn.UI.Platform.Services
{
    /// <summary>
    /// Frontend Tenant Service Implementation - UI Platform
    /// Retrieves tenant info from authentication state only
    /// </summary>
    public class TenantService(AuthenticationStateProvider authStateProvider) : ITenantService
    {
        private readonly AuthenticationStateProvider _authStateProvider = authStateProvider;
        private Guid _currentTenantId;

        public Guid GetCurrentTenantId()
        {
            if (_currentTenantId == Guid.Empty)
            {
                AuthenticationState authState = _authStateProvider.GetAuthenticationStateAsync().Result;
                System.Security.Claims.ClaimsPrincipal user = authState.User;

                string? tenantClaim = user.FindFirst("TenantId")?.Value;
                if (!string.IsNullOrEmpty(tenantClaim) && Guid.TryParse(tenantClaim, out Guid tenantId))
                {
                    _currentTenantId = tenantId;
                }
                else
                {
                    _currentTenantId = Guid.Empty; // Default fallback
                }
            }

            return _currentTenantId;
        }

        public void SetCurrentTenant(Guid tenantId)
        {
            _currentTenantId = tenantId;
        }

        public TenantConfig GetTenantConfig()
        {
            return new TenantConfig
            {
                TenantName = $"Tenant {_currentTenantId}",
                Theme = "default",
                Logo = "/images/default-logo.png"
            };
        }
    }
}
