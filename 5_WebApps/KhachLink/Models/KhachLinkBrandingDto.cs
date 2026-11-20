namespace VanAn.KhachLink.Models;

/// <summary>
/// W3-3 Option A: branding info cho desktop sidebar (NavMenu) — cascade từ MainLayout.
/// ShopName từ ShopConfig (fallback "Shop AI"), LogoUrl ưu tiên instance override.
/// </summary>
public sealed class KhachLinkBrandingDto
{
    public string ShopName { get; set; } = "Shop AI";
    public string? LogoUrl { get; set; }
}
