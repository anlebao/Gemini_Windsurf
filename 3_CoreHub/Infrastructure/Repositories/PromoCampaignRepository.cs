using Microsoft.EntityFrameworkCore;
using VanAn.CoreHub.Domain.Repositories;
using VanAn.CoreHub.Infrastructure;
using VanAn.Shared.Domain;

namespace VanAn.CoreHub.Infrastructure.Repositories
{
    /// <summary>
    /// WS-2: PromoCampaign repository implementation (ShopERP SQLite, tenant-scoped).
    /// </summary>
    public class PromoCampaignRepository : IPromoCampaignRepository
    {
        private readonly IVanAnDbContext _context;

        public PromoCampaignRepository(IVanAnDbContext context)
        {
            _context = context;
        }

        private DbSet<PromoCampaign> Campaigns => _context.PromoCampaigns;
        private DbSet<PromoCampaignRecipient> Recipients => _context.PromoCampaignRecipients;

        public Task<PromoCampaign?> GetByIdAsync(Guid id)
            => Campaigns.FirstOrDefaultAsync(c => c.Id == id && !c.IsDeleted);

        public async Task<IReadOnlyList<PromoCampaign>> GetAllAsync(int page, int pageSize)
        {
            return await Campaigns
                .Where(c => !c.IsDeleted)
                .OrderByDescending(c => c.CreatedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();
        }

        public async Task<IReadOnlyList<PromoCampaign>> GetPendingCampaignsAsync()
        {
            return await Campaigns
                .Where(c => !c.IsDeleted && c.Status == "Pending")
                .OrderBy(c => c.CreatedAt)
                .ToListAsync();
        }

        public async Task<PromoCampaign> AddAsync(PromoCampaign campaign)
        {
            _ = await Campaigns.AddAsync(campaign);
            _ = await _context.SaveChangesAsync();
            return campaign;
        }

        public async Task<PromoCampaign> UpdateAsync(PromoCampaign campaign)
        {
            _ = Campaigns.Update(campaign);
            _ = await _context.SaveChangesAsync();
            return campaign;
        }

        public async Task<IReadOnlyList<PromoCampaignRecipient>> GetPendingRecipientsAsync(Guid campaignId, int batchSize)
        {
            return await Recipients
                .Where(r => !r.IsDeleted && r.PromoCampaignId == campaignId && r.Status == "Pending")
                .OrderBy(r => r.CreatedAt)
                .Take(batchSize)
                .ToListAsync();
        }

        public async Task<IReadOnlyList<PromoCampaignRecipient>> GetRecipientsAsync(Guid campaignId, int page, int pageSize)
        {
            return await Recipients
                .Where(r => !r.IsDeleted && r.PromoCampaignId == campaignId)
                .OrderByDescending(r => r.CreatedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();
        }

        public async Task<int> GetRecipientCountByStatusAsync(Guid campaignId, string status)
        {
            return await Recipients
                .CountAsync(r => !r.IsDeleted && r.PromoCampaignId == campaignId && r.Status == status);
        }

        public async Task AddRecipientsAsync(IEnumerable<PromoCampaignRecipient> recipients)
        {
            await Recipients.AddRangeAsync(recipients);
            _ = await _context.SaveChangesAsync();
        }

        public async Task UpdateRecipientAsync(PromoCampaignRecipient recipient)
        {
            _ = Recipients.Update(recipient);
            _ = await _context.SaveChangesAsync();
        }
    }
}
