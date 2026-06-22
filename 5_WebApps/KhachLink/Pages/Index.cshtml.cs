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
        public ShopConfig ShopConfig { get; set; } = new ShopConfig
        {
            ShopId = Guid.NewGuid()
        };
        public IReadOnlyCollection<Product> Products { get; private set; } = new List<Product>();
        public IReadOnlyCollection<Product> FeaturedProducts { get; private set; } = new List<Product>();

        public async Task OnGetAsync()
        {
            // Fetch shop config
            Guid defaultShopId = Guid.NewGuid();
            ShopConfig = await _shopConfigService.GetShopConfigAsync(defaultShopId) ?? new ShopConfig
            {
                ShopName = "Vạn An Group",
                PrimaryColor = "#8B4513",
                SecondaryColor = "#D2691E",
                Theme = ThemeType.Classic
            };

            // Initialize demo products
            TenantId tenantId = new(Guid.NewGuid());
            FeaturedProducts = new List<Product>
            {
                new(tenantId, "Trà Sua Dau Do", "Dau do tu nhiên, béo ngây", 35000m, "Trà Sua", true, null, 0.10m),
                new(tenantId, "Trà Sua Truyen Thong", "Huong vi co dien không the thieu", 30000m, "Trà Sua", true, null, 0.10m),
                new(tenantId, "Trà Sua Matcha", "Matcha Nhat Ban nguyên chât", 40000m, "Trà Sua", true, null, 0.10m)
            };
        }
    }
}
