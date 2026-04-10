using Microsoft.Extensions.Logging;
using VanAn.Shared.Domain;
using VanAn.Shared.Models;

namespace VanAn.CoreHub.Services;

public interface IShopConfigService
{
    Task<ShopConfig?> GetShopConfigAsync(Guid shopId);
    Task<ShopConfig> CreateShopConfigAsync(ShopConfig config);
    Task<ShopConfig> UpdateShopConfigAsync(ShopConfig config);
    Task<bool> DeleteShopConfigAsync(Guid shopId);
}

public class ShopConfigService : IShopConfigService
{
    private readonly ILogger<ShopConfigService> _logger;

    public ShopConfigService(ILogger<ShopConfigService> logger)
    {
        _logger = logger;
    }

    public async Task<ShopConfig?> GetShopConfigAsync(Guid shopId)
    {
        await Task.Delay(10);
        return new ShopConfig
        {
            ShopId = shopId,
            ShopName = "Vạn An Group",
            PrimaryColor = "#8B4513",
            SecondaryColor = "#D2691E",
            LogoUrl = new Uri("/images/vanan-default-logo.png", UriKind.Relative),
            Address = "123 Nguyễn Huệ, Q1, TP.HCM",
            Phone = "1900-1234",
            Email = "info@vanan.vn",
            SocialLinksFb = "https://facebook.com/vanan",
            SocialLinksTiktok = "https://tiktok.com/@vanan",
            Theme = ThemeType.Classic
        };
    }

    public async Task<ShopConfig> CreateShopConfigAsync(ShopConfig config)
    {
        await Task.Delay(10);
        return config;
    }

    public async Task<ShopConfig> UpdateShopConfigAsync(ShopConfig config)
    {
        await Task.Delay(10);
        return config;
    }

    public async Task<bool> DeleteShopConfigAsync(Guid shopId)
    {
        await Task.Delay(10);
        return true;
    }
}
