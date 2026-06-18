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
        public ShopConfig ShopConfig { get; set; } = new ShopConfig();
        public List<Product> Products { get; set; } = [];

        [FromQuery(Name = "shopId")]
        public Guid? ShopId { get; set; }

        public async Task<IActionResult> OnGetAsync(string trackingCode)
        {
            TrackingCode = trackingCode ?? string.Empty;
            Code = TrackingCode; // For backward compatibility

            if (string.IsNullOrEmpty(TrackingCode))
            {
                // Return 404 if no tracking code provided
                return NotFound();
            }

            // Wave 3 Phase 3: Campaign tenant comes from URL ?shopId=xxx, not random Guid.NewGuid()
            Guid resolvedShopId = ShopId ?? Guid.Empty;

            Campaign = await _socialCampaignService.GetCampaignByTrackingCodeAsync(TrackingCode)
                ?? (resolvedShopId != Guid.Empty
                    ? new SocialCampaign(new TenantId(resolvedShopId), resolvedShopId, "default", "Mùa Hè Sôi Ðộng", TrackingCode)
                    : null!);

            if (Campaign == null)
            {
                return NotFound();
            }

            // Fetch real shop config from service — no random ShopId
            ShopConfig = resolvedShopId != Guid.Empty
                ? (await _shopConfigService.GetShopConfigAsync(resolvedShopId) ?? new ShopConfig
                {
                    ShopId = resolvedShopId,
                    ShopName = "Vạn An Group",
                    PrimaryColor = "#8B4513",
                    SecondaryColor = "#D2691E",
                    Theme = ThemeType.Classic
                })
                : new ShopConfig
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

            _ = await _socialCampaignService.RecordClickAsync(TrackingCode);

            // Products are loaded from Gateway API (not seeded here).
            // Products stays empty until a real /api/products?shopId=... call is added.

            return Page();
        }
    }
}
