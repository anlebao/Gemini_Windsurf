using VanAn.Shared.Domain;

namespace VanAn.CoreHub.Services.Orchestration;

/// <summary>
/// FallbackService - Fallback logic implementation
/// Focused service: ONLY fallback logic
/// </summary>
public class FallbackService : IFallbackService
{
    private readonly IProviderManager _providerManager;

    public FallbackService(IProviderManager providerManager)
    {
        _providerManager = providerManager;
    }

    public async Task<ProviderId?> SelectFallbackProviderAsync(
        TenantId tenantId,
        ProviderId failedProvider,
        CancellationToken cancellationToken = default)
    {
        // Get fallback providers (ordered by priority)
        var fallbackProviders = _providerManager.GetFallbackProviders(tenantId);
        
        // Select first fallback provider that is not the failed one
        return fallbackProviders.FirstOrDefault(p => p != failedProvider);
    }

    public async Task<bool> IsFallbackAvailableAsync(
        TenantId tenantId,
        CancellationToken cancellationToken = default)
    {
        var fallbackProviders = _providerManager.GetFallbackProviders(tenantId);
        return fallbackProviders.Any();
    }
}
