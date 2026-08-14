using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using VanAn.CoreHub.Infrastructure;
using VanAn.Shared.Domain.Aggregates.KhachLinkAggregate;

namespace VanAn.CoreHub.Services
{
    /// <summary>
    /// KhachLink Multi-Profile R1: Service for managing KhachLink instances.
    /// Platform-level CRUD + by-domain lookup. Uses VanAnDbContext directly (follows ShopInstanceService pattern).
    /// </summary>
    public class KhachLinkInstanceService : IKhachLinkInstanceService
    {
        private readonly IVanAnDbContext _dbContext;
        private readonly ILogger<KhachLinkInstanceService>? _logger;

        public KhachLinkInstanceService(IVanAnDbContext dbContext, ILogger<KhachLinkInstanceService>? logger = null)
        {
            _dbContext = dbContext;
            _logger = logger;
        }

        public async Task<KhachLinkInstance?> GetByIdAsync(Guid id, CancellationToken ct = default)
        {
            return await _dbContext.KhachLinkInstances
                .AsNoTracking()
                .FirstOrDefaultAsync(i => i.Id == id, ct);
        }

        public async Task<KhachLinkInstance?> GetByDomainAsync(string customDomain, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(customDomain))
                return null;

            var domain = customDomain.ToLowerInvariant();
            return await _dbContext.KhachLinkInstances
                .AsNoTracking()
                .FirstOrDefaultAsync(i => i.CustomDomain == domain && i.IsActive, ct);
        }

        public async Task<List<KhachLinkInstance>> GetAllAsync(CancellationToken ct = default)
        {
            return await _dbContext.KhachLinkInstances
                .AsNoTracking()
                .OrderByDescending(i => i.CreatedAt)
                .ToListAsync(ct);
        }

        public async Task<KhachLinkInstance> CreateAsync(
            string label,
            KhachLinkProfile profile,
            string customDomain,
            Guid? ownerTenantId = null,
            KhachLinkNavFlags? navFlagsOverride = null,
            CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(label))
                throw new ArgumentException("Label cannot be empty.", nameof(label));
            if (string.IsNullOrWhiteSpace(customDomain))
                throw new ArgumentException("CustomDomain cannot be empty.", nameof(customDomain));

            var normalizedDomain = customDomain.ToLowerInvariant();

            // Unique CustomDomain check
            bool duplicate = await _dbContext.KhachLinkInstances
                .AnyAsync(i => i.CustomDomain == normalizedDomain, ct);
            if (duplicate)
                throw new InvalidOperationException($"A KhachLinkInstance with CustomDomain '{normalizedDomain}' already exists.");

            var instance = new KhachLinkInstance(label, profile, normalizedDomain, ownerTenantId, navFlagsOverride);
            await _dbContext.KhachLinkInstances.AddAsync(instance, ct);
            await _dbContext.SaveChangesAsync(ct);

            _logger?.LogInformation("Created KhachLinkInstance {Id} '{Label}' domain={Domain} profile={Profile}",
                instance.Id, instance.Label, instance.CustomDomain, instance.Profile);
            return instance;
        }

        public async Task<bool> UpdateAsync(
            Guid id,
            KhachLinkProfile profile,
            KhachLinkNavFlags navFlags,
            CancellationToken ct = default)
        {
            if (navFlags is null)
                throw new ArgumentNullException(nameof(navFlags));

            var instance = await _dbContext.KhachLinkInstances
                .FirstOrDefaultAsync(i => i.Id == id, ct);
            if (instance is null)
                return false;

            instance.UpdateProfile(profile, navFlags);
            await _dbContext.SaveChangesAsync(ct);

            _logger?.LogInformation("Updated KhachLinkInstance {Id} profile={Profile}", instance.Id, profile);
            return true;
        }

        public async Task<bool> DeactivateAsync(Guid id, CancellationToken ct = default)
        {
            var instance = await _dbContext.KhachLinkInstances
                .FirstOrDefaultAsync(i => i.Id == id, ct);
            if (instance is null)
                return false;

            instance.Deactivate();
            await _dbContext.SaveChangesAsync(ct);

            _logger?.LogInformation("Deactivated KhachLinkInstance {Id} '{Label}'", instance.Id, instance.Label);
            return true;
        }
    }
}
