using VanAn.Shared.Domain.Aggregates.ApiKeyAggregate;

namespace VanAn.CoreHub.Domain.Repositories
{
    /// <summary>
    /// Wave 14: Repository interface for ApiKey aggregate.
    /// CRUD + lookup operations required by ApiKeyManagementService and Gateway middleware.
    /// </summary>
    public interface IApiKeyRepository
    {
        Task<ApiKey?> GetByIdAsync(Guid id, CancellationToken ct = default);
        Task<IReadOnlyList<ApiKey>> GetByTenantAsync(Guid tenantId, CancellationToken ct = default);
        Task AddAsync(ApiKey key, CancellationToken ct = default);
        Task UpdateAsync(ApiKey key, CancellationToken ct = default);
        Task SaveChangesAsync(CancellationToken ct = default);
    }
}
