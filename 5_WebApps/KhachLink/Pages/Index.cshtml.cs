using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using VanAn.CoreHub.Services;
using VanAn.Shared.Domain;

namespace VanAn.KhachLink.Pages
{
    // TECH DEBT: Loyalty and Customer data temporarily disabled.
    // KhachLink must not access DB directly (VA-KHACHLINK-004).
    // Fix: Create GatewayCustomerService + GatewayLoyaltyRewardsService
    // that call /api/customers via HttpClient("gateway").
    // Tracked: docs/AI/phase-next-order-accounting-improvements.md §6
    public class IndexModel(IShopConfigService shopConfigService) : PageModel
    {
        private readonly IShopConfigService _shopConfigService = shopConfigService;

        public LoyaltyRewards? CustomerRewards { get; set; }
        public ShopConfig ShopConfig { get; set; } = new ShopConfig();
        public IReadOnlyCollection<Product> Products { get; private set; } = new List<Product>();
        public IReadOnlyCollection<Product> FeaturedProducts { get; private set; } = new List<Product>();

        [FromQuery(Name = "shopId")]
        public Guid? ShopId { get; set; }

        public async Task OnGetAsync()
        {
            // Wave 3 Phase 3: Resolve tenant/shop from URL query param ?shopId=xxx
            // Customer data is scoped to the shop they are visiting — not random demo data.
            Guid resolvedShopId = ShopId ?? Guid.Empty;

            if (resolvedShopId == Guid.Empty)
            {
                // No shopId in URL — return empty/default config (no demo data)
                ShopConfig = new ShopConfig
                {
                    ShopName = "Vạn An Group",
                    PrimaryColor = "#8B4513",
                    SecondaryColor = "#D2691E",
                    Theme = ThemeType.Classic
                };
                return;
            }

            // Fetch real shop config from service (via Gateway in production)
            ShopConfig = await _shopConfigService.GetShopConfigAsync(resolvedShopId) ?? new ShopConfig
            {
                ShopId = resolvedShopId,
                ShopName = "Vạn An Group",
                PrimaryColor = "#8B4513",
                SecondaryColor = "#D2691E",
                Theme = ThemeType.Classic
            };

            // Featured products are loaded from Gateway API (not seeded here).
            // FeaturedProducts stays empty until a real /api/products?shopId=... call is added.
        }
    }
}
