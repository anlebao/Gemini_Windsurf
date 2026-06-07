using VanAn.Shared.Domain;

namespace VanAn.CoreHub.Services;

/// <summary>
/// IProviderManager - Multi-tenant provider management
/// Manages provider selection, health checking, and configuration
/// </summary>
public interface IProviderManager
{
    /// <summary>
    /// Get active provider for tenant (primary provider)
    /// </summary>
    ProviderId? GetActiveProvider(TenantId tenantId);

    /// <summary>
    /// Get fallback providers for tenant (ordered by priority)
    /// </summary>
    IEnumerable<ProviderId> GetFallbackProviders(TenantId tenantId);

    /// <summary>
    /// Check provider health status
    /// </summary>
    Task<bool> CheckProviderHealthAsync(
        TenantId tenantId,
        ProviderId providerId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Update provider status
    /// </summary>
    Task UpdateProviderStatusAsync(
        TenantId tenantId,
        ProviderId providerId,
        ProviderStatus status,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Get provider configuration
    /// </summary>
    Task<ProviderConfiguration?> GetProviderConfigurationAsync(
        TenantId tenantId,
        ProviderId providerId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Update provider configuration
    /// </summary>
    Task UpdateProviderConfigurationAsync(
        TenantId tenantId,
        ProviderId providerId,
        string configurationData,
        CancellationToken cancellationToken = default);
}
