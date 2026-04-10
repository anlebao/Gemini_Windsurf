using Microsoft.AspNetCore.Mvc.RazorPages;
using VanAn.CoreHub.Services;
using VanAn.Shared.Domain;
using VanAn.Shared.Models;
using VanAn.Shared.Services;

namespace VanAn.KhachLink.Pages;

public class IndexModel : PageModel
{
    private readonly ILoyaltyRewardsService _loyaltyRewardsService;
    private readonly IShopConfigService _shopConfigService;
    private readonly ICustomerService _customerService;

    public IndexModel(ILoyaltyRewardsService loyaltyRewardsService, 
                     IShopConfigService shopConfigService,
                     ICustomerService customerService)
    {
        _loyaltyRewardsService = loyaltyRewardsService;
        _shopConfigService = shopConfigService;
        _customerService = customerService;
    }

    public LoyaltyRewards CustomerRewards { get; set; } = new();
    public ShopConfig ShopConfig { get; set; } = new ShopConfig 
    { 
        ShopId = Guid.NewGuid()
    };
    public IReadOnlyCollection<Product> Products { get; private set; } = new List<Product>();

    public async Task OnGetAsync()
    {
        // Get device ID from cookie or generate new one
        var deviceId = Request.Cookies["customer_device_id"];
        
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
        var customer = await _customerService.GetOrCreateCustomerByDeviceIdAsync(parsedDeviceId);

        // Fetch customer rewards using actual customer ID
        CustomerRewards = await _loyaltyRewardsService.GetCustomerRewardsAsync(customer.CustomerId.Value) ?? new LoyaltyRewards
        {
            PointBalance = 0,
            History = "[]"
        };

        // Fetch shop config
        var defaultShopId = Guid.NewGuid(); // Generate shop ID for this session
        ShopConfig = await _shopConfigService.GetShopConfigAsync(defaultShopId) ?? new ShopConfig
        {
            ShopName = "Vạn An Group",
            PrimaryColor = "#8B4513",
            SecondaryColor = "#D2691E",
            Theme = ThemeType.Classic
        };

        // Initialize demo products
        Products = new List<Product>
        {
            new Product { Name = "Trà Sữa Đậu Đỏ", Price = 35000m, Category = "Trà Sữa", Description = "Đậu đỏ tự nhiên, béo ngậy" },
            new Product { Name = "Trà Sữa Truyền Thống", Price = 30000m, Category = "Trà Sữa", Description = "Hương vị cổ điển không thể thiếu" },
            new Product { Name = "Trà Sữa Matcha", Price = 40000m, Category = "Trà Sữa", Description = "Matcha Nhật Bản nguyên chất" }
        };
    }
}
