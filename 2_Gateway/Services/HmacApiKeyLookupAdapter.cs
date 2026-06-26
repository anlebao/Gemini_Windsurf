using VanAn.CoreHub.Services;

namespace VanAn.Gateway.Services
{
    /// <summary>
    /// Wave 14: Adapts IApiKeyManagementService (CoreHub) to IHmacApiKeyLookup (Gateway).
    /// Registered as Scoped in Gateway DI — Gateway already references CoreHub.
    /// </summary>
    public class HmacApiKeyLookupAdapter(IApiKeyManagementService apiKeyService) : IHmacApiKeyLookup
    {
        private readonly IApiKeyManagementService _apiKeyService = apiKeyService;

        public async Task<ApiKeyRecord?> FindActiveKeyAsync(Guid keyId, CancellationToken ct = default)
        {
            var key = await _apiKeyService.FindActiveKeyAsync(keyId, ct);
            if (key is null) return null;
            return new ApiKeyRecord(key.Id, key.TenantId, key.Name, key.SecretHash);
        }

        public Task RecordUsageAsync(Guid keyId, CancellationToken ct = default)
            => _apiKeyService.RecordKeyUsageAsync(keyId, ct);
    }
}
