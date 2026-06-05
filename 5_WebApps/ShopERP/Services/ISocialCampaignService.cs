using VanAn.Shared.Domain;

namespace VanAn.ShopERP.Services
{
    public interface ISocialCampaignService
    {
        Task<SocialCampaign> CreateCampaignAsync(SocialCampaign campaign);
        Task<SocialCampaign?> GetCampaignByIdAsync(Guid campaignId);
        Task<List<SocialCampaign>> GetCampaignsByShopAsync(Guid shopId);
        Task<string> GenerateTrackingUrlAsync(Guid campaignId);
        Task<bool> RecordClickAsync(string trackingCode);
        Task<SocialCampaign?> GetCampaignByTrackingCodeAsync(string trackingCode);
        Task<bool> IncrementConvertedOrdersAsync(Guid campaignId);
        Task<SocialCampaign> UpdateCampaignAsync(SocialCampaign campaign);
        Task<bool> DeleteCampaignAsync(Guid campaignId);
        Task<IEnumerable<SocialCampaign>> GetAllCampaignsAsync();
    }
}
