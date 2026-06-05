using Microsoft.AspNetCore.Mvc.RazorPages;
using VanAn.CoreHub.Services;
using VanAn.Shared.Domain;
using VanAn.Shared.Services;

namespace VanAn.KhachLink.Pages
{
    public class IndexModel(ILoyaltyRewardsService loyaltyRewardsService,
                     IShopConfigService shopConfigService,
                     ICustomerService customerService) : PageModel
    {
        private readonly ILoyaltyRewardsService _loyaltyRewardsService = loyaltyRewardsService;
        private readonly IShopConfigService _shopConfigService = shopConfigService;
        private readonly ICustomerService _customerService = customerService;

        public LoyaltyRewards CustomerRewards { get; set; } = null!;
        public ShopConfig ShopConfig { get; set; } = new ShopConfig
        {
            ShopId = Guid.NewGuid()
        };
        public IReadOnlyCollection<Product> Products { get; private set; } = new List<Product>();
        public IReadOnlyCollection<Product> FeaturedProducts { get; private set; } = new List<Product>();

        public async Task OnGetAsync()
        {
            // Get device ID from cookie or generate new one
            string? deviceId = Request.Cookies["customer_device_id"];

            // Handle old poisoned cookie format (e.g., "device_a1b2...")
            if (string.IsNullOrEmpty(deviceId) || !Guid.TryParse(deviceId, out Guid parsedDeviceId))
            {
                // Generate fresh GUID and overwrite old cookie
                parsedDeviceId = Guid.NewGuid();
                deviceId = parsedDeviceId.ToString();
                Response.Cookies.Append("customer_device_id", deviceId, new CookieOptions
                {
                    Expires = DateTime.UtcNow.AddYears(1)
                });
            }

            // Get or create customer by device ID
            Customer customer = await _customerService.GetOrCreateCustomerByDeviceIdAsync(parsedDeviceId);

            // Fetch customer rewards using actual customer ID
            CustomerRewards = await _loyaltyRewardsService.GetCustomerRewardsAsync(customer.CustomerId.Value);

            // Fetch shop config
            Guid defaultShopId = Guid.NewGuid(); // Generate shop ID for this session
            ShopConfig = await _shopConfigService.GetShopConfigAsync(defaultShopId) ?? new ShopConfig
            {
                ShopName = "Vạn An Group",
                PrimaryColor = "#8B4513",
                SecondaryColor = "#D2691E",
                Theme = ThemeType.Classic
            };

            // Initialize demo products using service
            TenantId tenantId = new(Guid.NewGuid()); // Demo tenant
            FeaturedProducts = new List<Product>
            {
                new(tenantId, "Trà Sua Dau Do", "Dau do tu nhiên, béo ngây", 35000m, "Trà Sua", true, null, 0.10m),
                new(tenantId, "Trà Sua Truyen Thong", "Huong vi co dien không the thieu", 30000m, "Trà Sua", true, null, 0.10m),
                new(tenantId, "Trà Sua Matcha", "Matcha Nhat Ban nguyên chât", 40000m, "Trà Sua", true, null, 0.10m)
            };
        }
    }
}
