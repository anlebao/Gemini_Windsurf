using Microsoft.EntityFrameworkCore;
using VanAn.CoreHub.Repositories;
using VanAn.Shared.Domain;

namespace VanAn.CoreHub.Infrastructure.Repositories
{
    public class SocialCampaignRepository(IVanAnDbContext context) : ISocialCampaignRepository
    {
        private readonly IVanAnDbContext _context = context;

        public async Task<SocialCampaign?> GetByIdAsync(Guid campaignId, CancellationToken cancellationToken = default)
        {
            // IgnoreQueryFilters: SystemAdmin (tenant_id=Empty) needs to fetch campaigns
            // from any tenant for admin operations (update/delete)
            return await _context.SocialCampaigns
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(c => c.Id == campaignId, cancellationToken);
        }

        public async Task<IEnumerable<SocialCampaign>> GetByTenantIdAsync(TenantId tenantId, CancellationToken cancellationToken = default)
        {
            return await _context.SocialCampaigns
                .IgnoreQueryFilters()
                .Where(c => c.TenantId == tenantId)
                .ToListAsync(cancellationToken);
        }

        public async Task<IEnumerable<SocialCampaign>> GetActiveByTenantIdAsync(TenantId tenantId, CancellationToken cancellationToken = default)
        {
            return await _context.SocialCampaigns
                .IgnoreQueryFilters()
                .Where(c => c.TenantId == tenantId && c.IsActive)
                .ToListAsync(cancellationToken);
        }

        public async Task<IEnumerable<SocialCampaign>> GetActiveAsync(CancellationToken cancellationToken = default)
        {
            // IgnoreQueryFilters: SystemAdmin (tenant_id=Empty) needs to see all campaigns
            return await _context.SocialCampaigns
                .IgnoreQueryFilters()
                .Where(c => c.IsActive)
                .ToListAsync(cancellationToken);
        }

        /// <summary>
        /// Get active campaigns by TenantId (not ShopId). Used by Home page personalization.
        /// IgnoreQueryFilters: allows cross-tenant query for SystemAdmin / public endpoints.
        /// </summary>
        public async Task<IEnumerable<SocialCampaign>> GetActiveByTenantIdValueAsync(Guid tenantId, CancellationToken cancellationToken = default)
        {
            // Use direct TenantId comparison (Known Error Pattern #1: EF Core applies
            // TenantIdConverter automatically — never use EF.Property<Guid> or .Value)
            var tid = new TenantId(tenantId);
            return await _context.SocialCampaigns
                .IgnoreQueryFilters()
                .Where(c => c.TenantId == tid && c.IsActive)
                .ToListAsync(cancellationToken);
        }

        public async Task<SocialCampaign> AddAsync(SocialCampaign campaign, CancellationToken cancellationToken = default)
        {
            _ = await _context.SocialCampaigns.AddAsync(campaign, cancellationToken);
            return campaign;
        }

        public async Task<SocialCampaign> UpdateAsync(SocialCampaign campaign, CancellationToken cancellationToken = default)
        {
            _ = _context.SocialCampaigns.Update(campaign);
            return campaign;
        }
    }
}
