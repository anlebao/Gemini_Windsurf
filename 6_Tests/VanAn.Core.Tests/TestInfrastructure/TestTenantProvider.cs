using VanAn.Shared.Domain.Common;

namespace VanAn.CoreHub.Tests.TestInfrastructure
{
    /// <summary>
    /// SINGLE SOURCE OF TRUTH for TestTenantProvider
    /// Anti-workaround rule: NO duplicates allowed across solution
    /// </summary>
    public class TestTenantProvider : ITenantProvider
    {
        public Guid TenantId { get; private set; } = Guid.TryParse("12345678-1234-1234-1234-123456789012", out Guid result) ? result : Guid.NewGuid();

        public string? CurrentUser => "87654321-4321-4321-4321-210987654321";

        public bool HasTenant => TenantId != Guid.Empty;

        public void SetTenant(Guid tenantId)
        {
            TenantId = tenantId;
        }
    }
}
