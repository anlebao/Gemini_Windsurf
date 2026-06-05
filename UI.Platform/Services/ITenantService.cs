namespace VanAn.UI.Platform.Services
{
    /// <summary>
    /// Frontend Tenant Service - UI Platform
    /// Retrieves tenant information from authentication state
    /// </summary>
    public interface ITenantService
    {
        Guid GetCurrentTenantId();
        void SetCurrentTenant(Guid tenantId);
        TenantConfig GetTenantConfig();
    }

    public class TenantConfig
    {
        public string TenantName { get; set; } = string.Empty;
        public string Theme { get; set; } = "default";
        public string Logo { get; set; } = string.Empty;
        public Dictionary<string, object> Settings { get; set; } = [];
    }
}
