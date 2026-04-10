using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc;
using VanAn.CoreHub.Services;
using VanAn.Shared.Domain;
using VanAn.Shared.Models;

namespace VanAn.KhachLink.Pages;

public class CampaignModel : PageModel
{
    private readonly ISocialCampaignService _socialCampaignService;
    private readonly IShopConfigService _shopConfigService;

    public CampaignModel(ISocialCampaignService socialCampaignService, IShopConfigService shopConfigService)
    {
        _socialCampaignService = socialCampaignService;
        _shopConfigService = shopConfigService;
    }

    public SocialCampaign Campaign { get; set; } = new();
    public string Code { get; set; } = string.Empty;
    public string TrackingCode { get; set; } = string.Empty;
    public string Keyframes { get; set; } = "fade-in";
    public ShopConfig ShopConfig { get; set; } = new ShopConfig 
    { 
        ShopId = Guid.NewGuid()
    };
    public List<Product> Products { get; set; } = new();

    public async Task<IActionResult> OnGetAsync(string trackingCode)
    {
        TrackingCode = trackingCode ?? string.Empty;
        Code = TrackingCode; // For backward compatibility

        if (!string.IsNullOrEmpty(TrackingCode))
        {
            Campaign = await _socialCampaignService.GetCampaignByTrackingCodeAsync(TrackingCode) ?? new SocialCampaign
            {
                Id = Guid.NewGuid(),
                ShopId = Guid.NewGuid(),
                CampaignName = "Mùa Hè Sôi Động",
                TrackingCode = TrackingCode,
                TotalClicks = 1234,
                ConvertedOrders = 56,
                IsActive = true
            };
        }
        else
        {
            // Return 404 if no tracking code provided
            return NotFound();
        }

        // Fetch shop config
        var defaultShopId = Guid.NewGuid(); // Generate shop ID for this session
        ShopConfig = await _shopConfigService.GetShopConfigAsync(defaultShopId) ?? new ShopConfig
        {
            ShopName = "Vạn An Group",
            PrimaryColor = "#8B4513",
            SecondaryColor = "#D2691E",
            Theme = ThemeType.Classic
        };

        // Record click for analytics
        var deviceId = Request.Cookies["customer_device_id"];
        if (string.IsNullOrEmpty(deviceId))
        {
            deviceId = "device_" + Guid.NewGuid().ToString("N")[..8];
            Response.Cookies.Append("customer_device_id", deviceId, new CookieOptions
            {
                Expires = DateTime.UtcNow.AddYears(1)
            });
        }

        await _socialCampaignService.RecordClickAsync(TrackingCode);

        // Initialize demo products with campaign pricing
        Products = new List<Product>
        {
            new Product { Name = "Trà Sữa Đậu Đỏ", Price = 28000m, Category = "Trà Sữa", Description = "Đậu đỏ tự nhiên, béo ngậy" },
            new Product { Name = "Trà Sữa Truyền Thống", Price = 25500m, Category = "Trà Sữa", Description = "Hương vị cổ điển không thể thiếu" },
            new Product { Name = "Trà Sữa Matcha", Price = 30000m, Category = "Trà Sữa", Description = "Matcha Nhật Bản nguyên chất" }
        };

        return Page();
    }
}
