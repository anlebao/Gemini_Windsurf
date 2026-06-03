using Microsoft.Extensions.Logging;
using VanAn.Shared.Domain;
using VanAn.CoreHub.Repositories;

namespace VanAn.CoreHub.Services;

public class SocialCampaignService : ISocialCampaignService
{
    private readonly ISocialCampaignRepository _repository;
    private readonly ILogger<SocialCampaignService> _logger;

    public SocialCampaignService(ISocialCampaignRepository repository, ILogger<SocialCampaignService> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    public async Task<SocialCampaign> CreateCampaignAsync(SocialCampaign campaign)
    {
        await _repository.AddAsync(campaign);
        
        _logger.LogInformation("Created social campaign {CampaignId} for shop {ShopId}", campaign.Id, campaign.ShopId);
        return campaign;
    }

    public async Task<SocialCampaign?> GetCampaignByIdAsync(Guid campaignId)
    {
        return await _repository.GetByIdAsync(campaignId);
    }

    public async Task<List<SocialCampaign>> GetCampaignsByShopAsync(Guid shopId)
    {
        var campaigns = await _repository.GetByTenantIdAsync(new TenantId(shopId));
        return campaigns.Where(c => c.IsActive).OrderByDescending(c => c.CreatedAt).ToList();
    }

    public async Task<string> GenerateTrackingUrlAsync(Guid campaignId)
    {
        var campaign = await GetCampaignByIdAsync(campaignId);
        if (campaign == null)
            throw new InvalidOperationException($"Campaign {campaignId} not found");

        // Generate tracking URL format: khachlink.com/c/{TrackingCode}
        var baseUrl = "https://khachlink.com/c";
        var trackingUrl = $"{baseUrl}/{campaign.TrackingCode}";
        
        _logger.LogInformation("Generated tracking URL for campaign {CampaignId}: {TrackingUrl}", campaignId, trackingUrl);
        return trackingUrl;
    }

    public async Task<bool> RecordClickAsync(string trackingCode)
    {
        // Need to add GetByTrackingCodeAsync to repository
        var campaigns = await _repository.GetActiveAsync();
        var campaign = campaigns.FirstOrDefault(c => c.TrackingCode == trackingCode);
        
        if (campaign == null)
        {
            _logger.LogWarning("Campaign not found for tracking code: {TrackingCode}", trackingCode);
            return false;
        }

        campaign.IncrementClicks();
        
        await _repository.UpdateAsync(campaign);
        
        _logger.LogInformation("Recorded click for campaign {CampaignId}, total clicks: {TotalClicks}", 
            campaign.Id, campaign.TotalClicks);
        return true;
    }

    public async Task<SocialCampaign?> GetCampaignByTrackingCodeAsync(string trackingCode)
    {
        var campaigns = await _repository.GetActiveAsync();
        return campaigns.FirstOrDefault(c => c.TrackingCode == trackingCode);
    }

    public async Task<bool> IncrementConvertedOrdersAsync(Guid campaignId)
    {
        var campaign = await GetCampaignByIdAsync(campaignId);
        if (campaign == null)
        {
            _logger.LogWarning("Campaign not found: {CampaignId}", campaignId);
            return false;
        }

        campaign.IncrementConvertedOrders();
        
        await _repository.UpdateAsync(campaign);
        
        _logger.LogInformation("Incremented converted orders for campaign {CampaignId}, total: {TotalConverted}", 
            campaign.Id, campaign.ConvertedOrders);
        return true;
    }

    public async Task<SocialCampaign> UpdateCampaignAsync(SocialCampaign campaign)
    {
        var existing = await GetCampaignByIdAsync(campaign.Id);
        if (existing == null)
            throw new InvalidOperationException($"Campaign {campaign.Id} not found");

        existing.UpdateCampaignDetails(campaign.CampaignName, campaign.UtmSource, campaign.IsActive);

        await _repository.UpdateAsync(existing);
        
        _logger.LogInformation("Updated social campaign {CampaignId}", campaign.Id);
        return existing;
    }

    public async Task<bool> DeleteCampaignAsync(Guid campaignId)
    {
        var campaign = await GetCampaignByIdAsync(campaignId);
        if (campaign == null)
            return false;

        campaign.UpdateCampaignDetails(campaign.CampaignName, campaign.UtmSource, false);
        
        await _repository.UpdateAsync(campaign);
        
        _logger.LogInformation("Deactivated social campaign {CampaignId}", campaignId);
        return true;
    }

    public async Task<IEnumerable<SocialCampaign>> GetAllCampaignsAsync()
    {
        return await _repository.GetActiveAsync();
    }
}
