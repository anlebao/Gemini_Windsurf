using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using VanAn.Shared.Domain;
using VanAn.CoreHub.Infrastructure;

namespace VanAn.CoreHub.Repositories
{
    /// <summary>
    /// VA-FI-MVP2 (2026-08-21): Repository implementation for BusinessProfile entities.
    /// Pattern follows OrderRepository — IVanAnDbContext + try/catch with logger.
    /// TenantId filter uses direct equality (Pattern #1 — EF Core applies TenantIdConverter).
    /// </summary>
    public class BusinessProfileRepository(IVanAnDbContext context, ILogger<BusinessProfileRepository> logger) : IBusinessProfileRepository
    {
        private readonly IVanAnDbContext _context = context;
        private readonly ILogger<BusinessProfileRepository> _logger = logger;

        public async Task<BusinessProfile?> GetByTenantAsync(TenantId tenantId, CancellationToken cancellationToken = default)
        {
            try
            {
                return await _context.BusinessProfiles
                    .AsNoTracking()
                    .FirstOrDefaultAsync(p => p.TenantId == tenantId, cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting BusinessProfile for tenant {TenantId}", tenantId.Value);
                return null;
            }
        }

        public async Task<BusinessProfile> AddAsync(BusinessProfile profile, CancellationToken cancellationToken = default)
        {
            try
            {
                _ = await _context.BusinessProfiles.AddAsync(profile, cancellationToken).ConfigureAwait(false);
                _ = await _context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
                return profile;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error adding BusinessProfile for tenant {TenantId}", profile.TenantId.Value);
                throw;
            }
        }

        public async Task<BusinessProfile> UpdateAsync(BusinessProfile profile, CancellationToken cancellationToken = default)
        {
            try
            {
                // GetByTenantAsync uses AsNoTracking → entity is detached → use DbSet.Update to attach + mark Modified.
                _context.BusinessProfiles.Update(profile);
                _ = await _context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
                return profile;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating BusinessProfile for tenant {TenantId}", profile.TenantId.Value);
                throw;
            }
        }

        public async Task SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            _ = await _context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
    }
}
