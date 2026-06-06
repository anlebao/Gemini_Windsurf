using VanAn.Shared.Domain;

namespace VanAn.CoreHub.Infrastructure.Repositories;

/// <summary>
/// ITenantProviderConfigurationService - Multi-tenant provider configuration service
/// Manages provider configuration per tenant
/// </summary>
public interface ITenantProviderConfigurationService
{
    /// <summary>
    /// Get active provider for tenant
    /// </summary>
    Task<ProviderId?> GetActiveProviderAsync(TenantId tenantId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get fallback providers for tenant (ordered by priority)
    /// </summary>
    Task<IEnumerable<ProviderId>> GetFallbackProvidersAsync(TenantId tenantId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get provider configuration
    /// </summary>
    Task<ProviderConfiguration?> GetProviderConfigurationAsync(
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
    /// Update provider configuration
    /// </summary>
    Task UpdateProviderConfigurationAsync(
        TenantId tenantId,
        ProviderId providerId,
        string configurationData,
        CancellationToken cancellationToken = default);
}
