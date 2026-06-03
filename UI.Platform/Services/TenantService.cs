using Microsoft.AspNetCore.Components.Authorization;
using System.Security.Claims;

namespace VanAn.UI.Platform.Services
{
    /// <summary>
    /// Frontend Tenant Service Implementation - UI Platform
    /// Retrieves tenant info from authentication state only
    /// </summary>
    public class TenantService : ITenantService
    {
        private readonly AuthenticationStateProvider _authStateProvider;
        private Guid _currentTenantId;

        public TenantService(AuthenticationStateProvider authStateProvider)
        {
            _authStateProvider = authStateProvider;
        }

        public Guid GetCurrentTenantId()
        {
            if (_currentTenantId == Guid.Empty)
            {
                var authState = _authStateProvider.GetAuthenticationStateAsync().Result;
                var user = authState.User;
                
                var tenantClaim = user.FindFirst("TenantId")?.Value;
                if (!string.IsNullOrEmpty(tenantClaim) && Guid.TryParse(tenantClaim, out var tenantId))
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
