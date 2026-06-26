using VanAn.Shared.Domain.Aggregates.ApiKeyAggregate;

namespace VanAn.CoreHub.Services
{
    /// <summary>
    /// Wave 14: API Key lifecycle management — create, list, revoke.
    /// Implemented by ApiKeyManagementService in CoreHub.
    /// </summary>
    public interface IApiKeyManagementService
    {
        /// <summary>
        /// Creates a new API Key for the tenant.
        /// Returns the ApiKey entity and the raw (one-time) secret.
        /// </summary>
        Task<(ApiKey Key, string RawSecret)> CreateKeyAsync(Guid tenantId, string name, int expirationDays = 90, CancellationToken ct = default);

        Task<IReadOnlyList<ApiKey>> ListKeysAsync(Guid tenantId, CancellationToken ct = default);

        Task<ApiKey> RevokeKeyAsync(Guid keyId, Guid tenantId, CancellationToken ct = default);

        /// <summary>
        /// Lookup an active, non-expired key by raw ID.
        /// Used by Gateway middleware adapter (IHmacApiKeyLookup).
        /// </summary>
        Task<ApiKey?> FindActiveKeyAsync(Guid keyId, CancellationToken ct = default);

        Task RecordKeyUsageAsync(Guid keyId, CancellationToken ct = default);
    }
}
