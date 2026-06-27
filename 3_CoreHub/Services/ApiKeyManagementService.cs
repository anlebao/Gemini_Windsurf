using Microsoft.Extensions.Logging;
using VanAn.Shared.Repositories;
using VanAn.Shared.Domain;

namespace VanAn.CoreHub.Services
{
    /// <summary>
    /// Wave 14: API Key lifecycle management implementation.
    ///
    /// Secret storage strategy (per SRS decision table):
    ///   - Raw secret is generated once and returned to the caller (like GitHub PAT).
    ///   - For HMAC validation the middleware needs the raw secret as the HMAC key.
    ///     BCrypt is one-way only — unsuitable here.
    ///   - We store the secret in plaintext in DB (protected by DB-level access controls).
    ///     Production upgrade path: wrap with IDataProtectionProvider or use KMS.
    ///   - SecretHash field name is kept for forward-compatibility when KMS/wrapping is added.
    /// </summary>
    public class ApiKeyManagementService(
        IApiKeyRepository repository,
        ILogger<ApiKeyManagementService> logger) : IApiKeyManagementService
    {
        private readonly IApiKeyRepository _repository = repository;
        private readonly ILogger<ApiKeyManagementService> _logger = logger;

        public async Task<(ApiKey Key, string RawSecret)> CreateKeyAsync(
            Guid tenantId, string name, int expirationDays = 90, CancellationToken ct = default)
        {
            // Generate a cryptographically-random 32-byte secret (256 bits)
            byte[] secretBytes = new byte[32];
            System.Security.Cryptography.RandomNumberGenerator.Fill(secretBytes);
            string rawSecret = Convert.ToBase64String(secretBytes);

            var key = new ApiKey(tenantId, name, secretHash: rawSecret, expirationDays);
            await _repository.AddAsync(key, ct);
            await _repository.SaveChangesAsync(ct);

            _logger.LogInformation(
                "API Key created: Id={KeyId}, Tenant={TenantId}, Name={Name}, Expires={Expires}",
                key.Id, tenantId, name, key.ExpiresAt);

            return (key, rawSecret);
        }

        public Task<IReadOnlyList<ApiKey>> ListKeysAsync(Guid tenantId, CancellationToken ct = default)
            => _repository.GetByTenantAsync(tenantId, ct);

        public async Task<ApiKey> RevokeKeyAsync(Guid keyId, Guid tenantId, CancellationToken ct = default)
        {
            var key = await _repository.GetByIdAsync(keyId, ct)
                ?? throw new KeyNotFoundException($"API key {keyId} not found");

            if (key.TenantId != tenantId)
                throw new UnauthorizedAccessException($"API key {keyId} does not belong to tenant {tenantId}");

            key.Revoke();
            await _repository.UpdateAsync(key, ct);
            await _repository.SaveChangesAsync(ct);

            _logger.LogInformation("API Key revoked: Id={KeyId}, Tenant={TenantId}", keyId, tenantId);
            return key;
        }

        public async Task<ApiKey?> FindActiveKeyAsync(Guid keyId, CancellationToken ct = default)
        {
            var key = await _repository.GetByIdAsync(keyId, ct);
            return key?.IsValid() == true ? key : null;
        }

        public async Task RecordKeyUsageAsync(Guid keyId, CancellationToken ct = default)
        {
            var key = await _repository.GetByIdAsync(keyId, ct);
            if (key is null) return;
            key.RecordUsage();
            await _repository.UpdateAsync(key, ct);
            await _repository.SaveChangesAsync(ct);
        }
    }
}
