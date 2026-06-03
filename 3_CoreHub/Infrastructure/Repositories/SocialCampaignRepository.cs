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
            return await _context.SocialCampaigns
                .Include(c => c.Shop)
                .FirstOrDefaultAsync(c => c.Id == campaignId, cancellationToken);
        }

        public async Task<IEnumerable<SocialCampaign>> GetByTenantIdAsync(TenantId tenantId, CancellationToken cancellationToken = default)
        {
            return await _context.SocialCampaigns
                .Where(c => c.ShopId == tenantId.Value)
                .ToListAsync(cancellationToken);
        }

        public async Task<IEnumerable<SocialCampaign>> GetActiveByTenantIdAsync(TenantId tenantId, CancellationToken cancellationToken = default)
        {
            return await _context.SocialCampaigns
                .Where(c => c.ShopId == tenantId.Value && c.IsActive)
                .ToListAsync(cancellationToken);
        }

        public async Task<IEnumerable<SocialCampaign>> GetActiveAsync(CancellationToken cancellationToken = default)
        {
            return await _context.SocialCampaigns
                .Where(c => c.IsActive)
                .ToListAsync(cancellationToken);
        }

        public async Task<SocialCampaign> AddAsync(SocialCampaign campaign, CancellationToken cancellationToken = default)
        {
            await _context.SocialCampaigns.AddAsync(campaign, cancellationToken);
            return campaign;
        }

        public async Task<SocialCampaign> UpdateAsync(SocialCampaign campaign, CancellationToken cancellationToken = default)
        {
            _context.SocialCampaigns.Update(campaign);
            return campaign;
        }
    }
}
