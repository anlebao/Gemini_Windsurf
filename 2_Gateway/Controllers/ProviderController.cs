using Microsoft.AspNetCore.Mvc;
using VanAn.CoreHub.Services;
using VanAn.Shared.Domain;

namespace VanAn.Gateway.Controllers;

/// <summary>
/// ProviderController - REST API for provider management
/// Phase 1: TenantId now from JWT claim, not query string
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class ProviderController(IProviderManager providerManager) : ControllerBase
{
    private readonly IProviderManager _providerManager = providerManager;

    /// <summary>
    /// Get TenantId from JWT claim (Phase 1: fail-fast security)
    /// </summary>
    private Guid GetTenantIdFromClaim()
    {
        string? tenantClaim = User.FindFirst("TenantId")?.Value
            ?? User.FindFirst("tenant_id")?.Value
            ?? User.FindFirst("tenantId")?.Value;
        return Guid.TryParse(tenantClaim, out Guid tenantId) ? tenantId : Guid.Empty;
    }

    /// <summary>
    /// List all providers for tenant
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> ListProviders(CancellationToken cancellationToken)
    {
        Guid tenantGuid = GetTenantIdFromClaim();
        if (tenantGuid == Guid.Empty)
        {
            return Unauthorized(new { error = "Tenant ID required in JWT claim" });
        }

        var tenant = new TenantId(tenantGuid);
        var activeProvider = _providerManager.GetActiveProvider(tenant);
        var fallbackProviders = _providerManager.GetFallbackProviders(tenant);

        return Ok(new
        {
            ActiveProvider = activeProvider?.Value,
            FallbackProviders = fallbackProviders.Select(p => p.Value)
        });
    }

    /// <summary>
    /// Get provider configuration
    /// </summary>
    [HttpGet("{providerId}")]
    public async Task<IActionResult> GetProvider(string providerId, CancellationToken cancellationToken)
    {
        Guid tenantGuid = GetTenantIdFromClaim();
        if (tenantGuid == Guid.Empty)
        {
            return Unauthorized(new { error = "Tenant ID required in JWT claim" });
        }

        var tenant = new TenantId(tenantGuid);
        var provider = new ProviderId(providerId);
        var config = await _providerManager.GetProviderConfigurationAsync(tenant, provider, cancellationToken);

        if (config == null)
            return NotFound();

        return Ok(config);
    }

    /// <summary>
    /// Health check for provider
    /// </summary>
    [HttpGet("{providerId}/health")]
    public async Task<IActionResult> HealthCheck(string providerId, CancellationToken cancellationToken)
    {
        Guid tenantGuid = GetTenantIdFromClaim();
        if (tenantGuid == Guid.Empty)
        {
            return Unauthorized(new { error = "Tenant ID required in JWT claim" });
        }

        var tenant = new TenantId(tenantGuid);
        var provider = new ProviderId(providerId);
        var isHealthy = await _providerManager.CheckProviderHealthAsync(tenant, provider, cancellationToken);

        return Ok(new { ProviderId = providerId, IsHealthy = isHealthy });
    }
}
