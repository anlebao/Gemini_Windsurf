using VanAn.Shared.Domain;

namespace VanAn.CoreHub.Infrastructure.Repositories;

/// <summary>
/// TenantProviderConfigurationService - Stub implementation for multi-tenant provider configuration
/// TODO: Implement with actual database repository
/// </summary>
public class TenantProviderConfigurationService : ITenantProviderConfigurationService
{
    public Task<ProviderId?> GetActiveProviderAsync(TenantId tenantId, CancellationToken cancellationToken = default)
    {
        // Stub: Return Viettel as default provider
        return Task.FromResult<ProviderId?>(new ProviderId("viettel"));
    }

    public Task<IEnumerable<ProviderId>> GetFallbackProvidersAsync(TenantId tenantId, CancellationToken cancellationToken = default)
    {
        // Stub: Return MISA and BKAV as fallback providers
        var fallbackProviders = new List<ProviderId>
        {
            new ProviderId("misa"),
            new ProviderId("bkav")
        };
        return Task.FromResult<IEnumerable<ProviderId>>(fallbackProviders);
    }

    public Task<ProviderConfiguration?> GetProviderConfigurationAsync(
        TenantId tenantId,
        ProviderId providerId,
        CancellationToken cancellationToken = default)
    {
        // Stub: Return default configuration
        var config = new ProviderConfiguration(
            tenantId,
            providerId,
            providerId.Value.ToUpper(),
            true,
            1,
            "{}"
        );
        return Task.FromResult<ProviderConfiguration?>(config);
    }

    public Task UpdateProviderStatusAsync(
        TenantId tenantId,
        ProviderId providerId,
        ProviderStatus status,
        CancellationToken cancellationToken = default)
    {
        // Stub: Update status in database
        return Task.CompletedTask;
    }

    public Task UpdateProviderConfigurationAsync(
        TenantId tenantId,
        ProviderId providerId,
        string configurationData,
        CancellationToken cancellationToken = default)
    {
        // Stub: Update configuration in database
        return Task.CompletedTask;
    }
}
