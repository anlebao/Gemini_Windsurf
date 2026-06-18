using System.Security.Claims;
using VanAn.Shared.Domain.Common;

namespace VanAn.ShopERP.Services
{
    /// <summary>
    /// HTTP context-based ITenantProvider for ShopERP.
    /// Resolves TenantId and CurrentUser from JWT claims via IHttpContextAccessor.
    /// </summary>
    public class HttpContextTenantProvider(IHttpContextAccessor httpContextAccessor) : ITenantProvider
    {
        private readonly IHttpContextAccessor _httpContextAccessor = httpContextAccessor;
        private Guid _overrideTenantId = Guid.Empty;

        public Guid TenantId
        {
            get
            {
                if (_overrideTenantId != Guid.Empty)
                    return _overrideTenantId;

                // Wave 1 Phase 2: Standardized claim name "tenant_id" (snake_case, OIDC standard)
                // Support dual-read during migration: "tenant_id" first, then legacy "TenantId"
                string? claim = _httpContextAccessor.HttpContext?.User
                    .FindFirstValue("tenant_id")
                    ?? _httpContextAccessor.HttpContext?.User
                    .FindFirstValue("TenantId");

                return claim != null && Guid.TryParse(claim, out Guid id) ? id : Guid.Empty;
            }
        }

        public string? CurrentUser =>
            _httpContextAccessor.HttpContext?.User.FindFirstValue(ClaimTypes.Name)
            ?? _httpContextAccessor.HttpContext?.User.FindFirstValue(ClaimTypes.Email);

        public bool HasTenant => TenantId != Guid.Empty;

        public void SetTenant(Guid tenantId) => _overrideTenantId = tenantId;
    }
}
