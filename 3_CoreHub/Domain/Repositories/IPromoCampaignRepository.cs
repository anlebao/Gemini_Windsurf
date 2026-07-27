using VanAn.Shared.Domain;

namespace VanAn.CoreHub.Domain.Repositories
{
    /// <summary>
    /// WS-2: Repository for PromoCampaign + PromoCampaignRecipient entities.
    /// ShopERP SQLite (tenant-scoped).
    /// </summary>
    public interface IPromoCampaignRepository
    {
        Task<PromoCampaign?> GetByIdAsync(Guid id);
        Task<IReadOnlyList<PromoCampaign>> GetAllAsync(int page, int pageSize);
        Task<IReadOnlyList<PromoCampaign>> GetPendingCampaignsAsync();
        Task<PromoCampaign> AddAsync(PromoCampaign campaign);
        Task<PromoCampaign> UpdateAsync(PromoCampaign campaign);

        Task<IReadOnlyList<PromoCampaignRecipient>> GetPendingRecipientsAsync(Guid campaignId, int batchSize);
        Task<IReadOnlyList<PromoCampaignRecipient>> GetRecipientsAsync(Guid campaignId, int page, int pageSize);
        Task<int> GetRecipientCountByStatusAsync(Guid campaignId, string status);
        Task AddRecipientsAsync(IEnumerable<PromoCampaignRecipient> recipients);
        Task UpdateRecipientAsync(PromoCampaignRecipient recipient);
    }
}
