using Microsoft.EntityFrameworkCore;
using VanAn.CoreHub.Infrastructure;
using Microsoft.Extensions.Logging;
using VanAn.Shared.Domain;

namespace VanAn.CoreHub.Services;

public class SocialCampaignService : ISocialCampaignService
{
    private readonly VanAnDbContext _context;
    private readonly ILogger<SocialCampaignService> _logger;

    public SocialCampaignService(VanAnDbContext context, ILogger<SocialCampaignService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<SocialCampaign> CreateCampaignAsync(SocialCampaign campaign)
    {
        await _context.SocialCampaigns.AddAsync(campaign);
        await _context.SaveChangesAsync();
        
        _logger.LogInformation("Created social campaign {CampaignId} for shop {ShopId}", campaign.Id, campaign.ShopId);
        return campaign;
    }

    public async Task<SocialCampaign?> GetCampaignByIdAsync(Guid campaignId)
    {
        return await _context.SocialCampaigns
            .Include(c => c.Shop)
            .FirstOrDefaultAsync(c => c.Id == campaignId);
    }

    public async Task<List<SocialCampaign>> GetCampaignsByShopAsync(Guid shopId)
    {
        return await _context.SocialCampaigns
            .Where(c => c.ShopId == shopId && c.IsActive)
            .OrderByDescending(c => c.CreatedAt)
            .ToListAsync();
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
        var campaign = await GetCampaignByTrackingCodeAsync(trackingCode);
        if (campaign == null)
        {
            _logger.LogWarning("Campaign not found for tracking code: {TrackingCode}", trackingCode);
            return false;
        }

        campaign.TotalClicks++;
        campaign.UpdatedAt = DateTime.UtcNow;
        
        await _context.SaveChangesAsync();
        
        _logger.LogInformation("Recorded click for campaign {CampaignId}, total clicks: {TotalClicks}", 
            campaign.Id, campaign.TotalClicks);
        return true;
    }

    public async Task<SocialCampaign?> GetCampaignByTrackingCodeAsync(string trackingCode)
    {
        return await _context.SocialCampaigns
            .FirstOrDefaultAsync(c => c.TrackingCode == trackingCode && c.IsActive);
    }

    public async Task<bool> IncrementConvertedOrdersAsync(Guid campaignId)
    {
        var campaign = await GetCampaignByIdAsync(campaignId);
        if (campaign == null)
        {
            _logger.LogWarning("Campaign not found: {CampaignId}", campaignId);
            return false;
        }

        campaign.ConvertedOrders++;
        campaign.UpdatedAt = DateTime.UtcNow;
        
        await _context.SaveChangesAsync();
        
        _logger.LogInformation("Incremented converted orders for campaign {CampaignId}, total: {TotalConverted}", 
            campaign.Id, campaign.ConvertedOrders);
        return true;
    }

    public async Task<SocialCampaign> UpdateCampaignAsync(SocialCampaign campaign)
    {
        var existing = await GetCampaignByIdAsync(campaign.Id);
        if (existing == null)
            throw new InvalidOperationException($"Campaign {campaign.Id} not found");

        existing.CampaignName = campaign.CampaignName;
        existing.UtmSource = campaign.UtmSource;
        existing.IsActive = campaign.IsActive;
        existing.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();
        
        _logger.LogInformation("Updated social campaign {CampaignId}", campaign.Id);
        return existing;
    }

    public async Task<bool> DeleteCampaignAsync(Guid campaignId)
    {
        var campaign = await GetCampaignByIdAsync(campaignId);
        if (campaign == null)
            return false;

        campaign.IsActive = false;
        campaign.UpdatedAt = DateTime.UtcNow;
        
        await _context.SaveChangesAsync();
        
        _logger.LogInformation("Deactivated social campaign {CampaignId}", campaignId);
        return true;
    }

    public async Task<IEnumerable<SocialCampaign>> GetAllCampaignsAsync()
    {
        return await _context.SocialCampaigns
            .Include(c => c.Shop)
            .Where(c => c.IsActive)
            .OrderByDescending(c => c.CreatedAt)
            .ToListAsync();
    }
}
