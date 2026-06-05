using VanAn.Shared.Domain.Common;

namespace VanAn.Integration.Tests.Infrastructure;

/// <summary>
/// Test Tenant Provider for Integration Tests
/// Provides tenant context for testing scenarios
/// </summary>
public class TestTenantProvider : ITenantProvider
{
    private readonly TenantId _tenantId;
    private string? _currentUser;

    public TestTenantProvider()
    {
        _tenantId = new TenantId(Guid.Parse("12345678-1234-1234-1234-123456789abc"));
    }

    public Guid TenantId => _tenantId.Value;
    
    public string? CurrentUser => _currentUser;
    
    public bool HasTenant => true;

    public void SetTenant(Guid tenantId)
    {
        // For testing, we don't allow changing the tenant
        // This could be enhanced if needed for specific test scenarios
    }

    public TenantId GetCurrentTenantId()
    {
        return _tenantId;
    }

    public string GetCurrentTenantIdAsString()
    {
        return _tenantId.Value.ToString();
    }
}
