using VanAn.Shared.Domain;

namespace VanAn.Shared.Services
{
    public interface ISocialCampaignService
    {
        Task<SocialCampaign> CreateCampaignAsync(SocialCampaign campaign);
        Task<SocialCampaign?> GetCampaignByIdAsync(Guid campaignId);
        Task<List<SocialCampaign>> GetCampaignsByShopAsync(Guid shopId);
        Task<List<SocialCampaign>> GetCampaignsByTenantAsync(Guid tenantId);
        Task<string> GenerateTrackingUrlAsync(Guid campaignId);
        Task<bool> RecordClickAsync(string trackingCode);
        Task<SocialCampaign?> GetCampaignByTrackingCodeAsync(string trackingCode);
        Task<bool> IncrementConvertedOrdersAsync(Guid campaignId);
        Task<SocialCampaign> UpdateCampaignAsync(SocialCampaign campaign);
        Task<bool> DeleteCampaignAsync(Guid campaignId);
        Task<IEnumerable<SocialCampaign>> GetAllCampaignsAsync();
    }
}
