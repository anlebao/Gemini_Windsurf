namespace VanAn.HRApp.Application
{
    public class TenantContext
    {
        public string TenantId { get; set; } = string.Empty;
        public string TenantName { get; set; } = string.Empty;
        public bool IsAuthenticated { get; set; } = false;
        public string UserId { get; set; } = string.Empty;
        public string Username { get; set; } = string.Empty;
    }

    public static class TenantExtensions
    {
        public static void SetTenantHeader(this HttpClient http, TenantContext tenant)
        {
            if (!string.IsNullOrEmpty(tenant.TenantId))
            {
                http.DefaultRequestHeaders.Remove("X-Tenant-Id");
                http.DefaultRequestHeaders.Add("X-Tenant-Id", tenant.TenantId);
            }
        }
    }
}
