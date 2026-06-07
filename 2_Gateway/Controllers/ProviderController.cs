using Microsoft.AspNetCore.Mvc;
using VanAn.CoreHub.Services;
using VanAn.Shared.Domain;

namespace VanAn.Gateway.Controllers;

/// <summary>
/// ProviderController - REST API for provider management
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class ProviderController : ControllerBase
{
    private readonly IProviderManager _providerManager;

    public ProviderController(IProviderManager providerManager)
    {
        _providerManager = providerManager;
    }

    /// <summary>
    /// List all providers for tenant
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> ListProviders([FromQuery] Guid tenantId, CancellationToken cancellationToken)
    {
        var tenant = new TenantId(tenantId);
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
    public async Task<IActionResult> GetProvider([FromQuery] Guid tenantId, string providerId, CancellationToken cancellationToken)
    {
        var tenant = new TenantId(tenantId);
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
    public async Task<IActionResult> HealthCheck([FromQuery] Guid tenantId, string providerId, CancellationToken cancellationToken)
    {
        var tenant = new TenantId(tenantId);
        var provider = new ProviderId(providerId);
        var isHealthy = await _providerManager.CheckProviderHealthAsync(tenant, provider, cancellationToken);
        
        return Ok(new { ProviderId = providerId, IsHealthy = isHealthy });
    }
}
