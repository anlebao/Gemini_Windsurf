using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc;
using VanAn.CoreHub.Services;
using VanAn.Shared.Domain;

namespace VanAn.KhachLink.Pages
{
    public class CampaignModel(ISocialCampaignService socialCampaignService, IShopConfigService shopConfigService) : PageModel
    {
        private readonly ISocialCampaignService _socialCampaignService = socialCampaignService;
        private readonly IShopConfigService _shopConfigService = shopConfigService;

        public SocialCampaign Campaign { get; set; } = null!;
        public string Code { get; set; } = string.Empty;
        public string TrackingCode { get; set; } = string.Empty;
        public string Keyframes { get; set; } = "fade-in";
        public ShopConfig ShopConfig { get; set; } = new ShopConfig
        {
            ShopId = Guid.NewGuid()
        };
        public List<Product> Products { get; set; } = [];

        public async Task<IActionResult> OnGetAsync(string trackingCode)
        {
            TrackingCode = trackingCode ?? string.Empty;
            Code = TrackingCode; // For backward compatibility

            if (!string.IsNullOrEmpty(TrackingCode))
            {
                TenantId campaignTenantId = new(Guid.NewGuid()); // Demo tenant
                Campaign = await _socialCampaignService.GetCampaignByTrackingCodeAsync(TrackingCode) ??
                    new SocialCampaign(campaignTenantId, Guid.NewGuid(), "default", "Mùa Hè Sôi Ðông", TrackingCode);
            }
            else
            {
                // Return 404 if no tracking code provided
                return NotFound();
            }

            // Fetch shop config
            Guid defaultShopId = Guid.NewGuid(); // Generate shop ID for this session
            ShopConfig = await _shopConfigService.GetShopConfigAsync(defaultShopId) ?? new ShopConfig
            {
                ShopName = "Vạn An Group",
                PrimaryColor = "#8B4513",
                SecondaryColor = "#D2691E",
                Theme = ThemeType.Classic
            };

            // Record click for analytics
            string? deviceId = Request.Cookies["customer_device_id"];
            if (string.IsNullOrEmpty(deviceId))
            {
                deviceId = "device_" + Guid.NewGuid().ToString("N")[..8];
                Response.Cookies.Append("customer_device_id", deviceId, new CookieOptions
                {
                    Expires = DateTime.UtcNow.AddYears(1)
                });
            }

            await _socialCampaignService.RecordClickAsync(TrackingCode);

            // Initialize demo products with campaign pricing using constructor
            TenantId tenantId = new(Guid.NewGuid()); // Demo tenant
            Products =
            [
                new Product(tenantId, "Trà Sua Dau Do", "Dau do tu nhiên, béo ngây", 28000m, "Trà Sua", true, null, 0.10m),
                new Product(tenantId, "Trà Sua Truyen Thong", "Huong vi co dien không the thieu", 25500m, "Trà Sua", true, null, 0.10m),
                new Product(tenantId, "Trà Sua Matcha", "Matcha Nhat Ban nguyên chât", 30000m, "Trà Sua", true, null, 0.10m)
            ];

            return Page();
        }
    }
}
