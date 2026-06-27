using Microsoft.EntityFrameworkCore;
using VanAn.Shared.Repositories;
using VanAn.Shared.Domain;

namespace VanAn.CoreHub.Infrastructure.Repositories
{
    /// <summary>
    /// Wave 14: EF Core implementation of IApiKeyRepository.
    /// No global query filter — ApiKey is looked up by Id before TenantId is resolved.
    /// </summary>
    public class ApiKeyRepository(IVanAnDbContext db) : IApiKeyRepository
    {
        private readonly IVanAnDbContext _db = db;

        public Task<ApiKey?> GetByIdAsync(Guid id, CancellationToken ct = default)
            => _db.ApiKeys.FirstOrDefaultAsync(k => k.Id == id, ct);

        public async Task<IReadOnlyList<ApiKey>> GetByTenantAsync(Guid tenantId, CancellationToken ct = default)
            => await _db.ApiKeys
                .Where(k => k.TenantId == tenantId)
                .OrderByDescending(k => k.CreatedAt)
                .ToListAsync(ct);

        public async Task AddAsync(ApiKey key, CancellationToken ct = default)
            => await _db.ApiKeys.AddAsync(key, ct);

        public Task UpdateAsync(ApiKey key, CancellationToken ct = default)
        {
            _db.ApiKeys.Update(key);
            return Task.CompletedTask;
        }

        public Task SaveChangesAsync(CancellationToken ct = default)
            => _db.SaveChangesAsync(ct);
    }
}
