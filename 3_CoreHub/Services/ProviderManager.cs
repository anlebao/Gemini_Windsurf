using Microsoft.Extensions.Caching.Memory;
using VanAn.CoreHub.Infrastructure.Repositories;
using VanAn.Shared.Domain;

namespace VanAn.CoreHub.Services;

/// <summary>
/// ProviderManager - Multi-tenant provider management implementation
/// Configuration caching (NOT instances) for performance
/// </summary>
public class ProviderManager : IProviderManager
{
    private readonly ITenantProviderConfigurationService _configurationService;
    private readonly IMemoryCache _cache;
    private readonly TimeSpan _cacheDuration = TimeSpan.FromMinutes(5);

    public ProviderManager(
        ITenantProviderConfigurationService configurationService,
        IMemoryCache cache)
    {
        _configurationService = configurationService;
        _cache = cache;
    }

    public ProviderId? GetActiveProvider(TenantId tenantId)
    {
        var cacheKey = $"active_provider_{tenantId}";
        return _cache.GetOrCreate(cacheKey, entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = _cacheDuration;
            return _configurationService.GetActiveProviderAsync(tenantId).GetAwaiter().GetResult();
        });
    }

    public IEnumerable<ProviderId> GetFallbackProviders(TenantId tenantId)
    {
        var cacheKey = $"fallback_providers_{tenantId}";
        return _cache.GetOrCreate(cacheKey, entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = _cacheDuration;
            return _configurationService.GetFallbackProvidersAsync(tenantId).GetAwaiter().GetResult();
        }) ?? Enumerable.Empty<ProviderId>();
    }

    public async Task<bool> CheckProviderHealthAsync(
        TenantId tenantId,
        ProviderId providerId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var config = await _configurationService.GetProviderConfigurationAsync(tenantId, providerId, cancellationToken);
            if (config == null) return false;

            // Health check logic would be implemented by provider-specific service
            // For now, return true if provider is active
            return config.Status == ProviderStatus.Active;
        }
        catch
        {
            return false;
        }
    }

    public async Task UpdateProviderStatusAsync(
        TenantId tenantId,
        ProviderId providerId,
        ProviderStatus status,
        CancellationToken cancellationToken = default)
    {
        await _configurationService.UpdateProviderStatusAsync(tenantId, providerId, status, cancellationToken);
        
        // Invalidate cache
        var cacheKey = $"active_provider_{tenantId}";
        _cache.Remove(cacheKey);
    }

    public async Task<ProviderConfiguration?> GetProviderConfigurationAsync(
        TenantId tenantId,
        ProviderId providerId,
        CancellationToken cancellationToken = default)
    {
        return await _configurationService.GetProviderConfigurationAsync(tenantId, providerId, cancellationToken);
    }

    public async Task UpdateProviderConfigurationAsync(
        TenantId tenantId,
        ProviderId providerId,
        string configurationData,
        CancellationToken cancellationToken = default)
    {
        await _configurationService.UpdateProviderConfigurationAsync(tenantId, providerId, configurationData, cancellationToken);
        
        // Invalidate cache
        var cacheKey = $"active_provider_{tenantId}";
        _cache.Remove(cacheKey);
    }
}
