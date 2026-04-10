using VanAn.Shared.Domain.Common;

namespace VanAn.CoreHub.Tests.TestInfrastructure;

/// <summary>
/// SINGLE SOURCE OF TRUTH for TestTenantProvider
/// Anti-workaround rule: NO duplicates allowed across solution
/// </summary>
public class TestTenantProvider : ITenantProvider
{
    private Guid _tenantId = Guid.TryParse("12345678-1234-1234-1234-123456789012", out var result) ? result : Guid.NewGuid();

    public Guid TenantId => _tenantId;
    
    public string? CurrentUser => "87654321-4321-4321-4321-210987654321";

    public bool HasTenant => _tenantId != Guid.Empty;

    public void SetTenant(Guid tenantId)
    {
        _tenantId = tenantId;
    }
}
