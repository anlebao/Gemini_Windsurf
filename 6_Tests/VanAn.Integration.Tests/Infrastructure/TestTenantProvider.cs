using VanAn.Shared.Domain.Common;

namespace VanAn.Integration.Tests.Infrastructure;

/// <summary>
/// Test Tenant Provider for Integration Tests
/// Provides tenant context for testing scenarios
/// </summary>
public class TestTenantProvider : ITenantProvider
{
    private TenantId _tenantId;
    private string? _currentUser;

    public TestTenantProvider()
    {
        // Default tenant ID for backward compatibility
        _tenantId = new TenantId(Guid.Parse("12345678-1234-1234-1234-123456789abc"));
    }

    public TestTenantProvider(TenantId tenantId)
    {
        _tenantId = tenantId;
    }

    public Guid TenantId => _tenantId.Value;
    
    public string? CurrentUser => _currentUser;
    
    public bool HasTenant => true;

    public void SetTenant(Guid tenantId)
    {
        _tenantId = new TenantId(tenantId);
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
