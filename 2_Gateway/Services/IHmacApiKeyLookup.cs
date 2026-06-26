namespace VanAn.Gateway.Services
{
    /// <summary>
    /// Wave 14: Minimal interface the HMAC middleware needs from the API Key store.
    /// Gateway does NOT take a direct dependency on CoreHub repositories — instead,
    /// this interface is satisfied by an adapter registered in Gateway's DI.
    /// </summary>
    public interface IHmacApiKeyLookup
    {
        /// <summary>
        /// Lookup an active, non-expired API key by its raw ID.
        /// Returns null if not found, revoked, or expired.
        /// </summary>
        Task<ApiKeyRecord?> FindActiveKeyAsync(Guid keyId, CancellationToken ct = default);

        /// <summary>Records a successful use of the key (updates LastUsedAt).</summary>
        Task RecordUsageAsync(Guid keyId, CancellationToken ct = default);
    }

    /// <summary>Lightweight DTO — avoids coupling Gateway to domain entities.</summary>
    public sealed record ApiKeyRecord(
        Guid Id,
        Guid TenantId,
        string Name,
        /// <summary>BCrypt hash of the shared HMAC secret.</summary>
        string SecretHash);
}
