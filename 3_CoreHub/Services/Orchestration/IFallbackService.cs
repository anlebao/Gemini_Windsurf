using VanAn.Shared.Domain;

namespace VanAn.CoreHub.Services.Orchestration;

/// <summary>
/// IFallbackService - Fallback logic for provider failover
/// Focused service: ONLY fallback logic
/// </summary>
public interface IFallbackService
{
    /// <summary>
    /// Select fallback provider after primary failure
    /// </summary>
    Task<ProviderId?> SelectFallbackProviderAsync(
        TenantId tenantId,
        ProviderId failedProvider,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Check if fallback is available
    /// </summary>
    Task<bool> IsFallbackAvailableAsync(
        TenantId tenantId,
        CancellationToken cancellationToken = default);
}
