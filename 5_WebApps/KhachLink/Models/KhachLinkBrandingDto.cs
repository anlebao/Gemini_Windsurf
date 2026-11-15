namespace VanAn.KhachLink.Models;

/// <summary>
/// W3-3 Option A: branding info cho desktop sidebar (NavMenu) — cascade từ MainLayout.
/// ShopName từ ShopConfig (fallback "Vạn An KhachLink"), LogoUrl ưu tiên instance override.
/// </summary>
public sealed class KhachLinkBrandingDto
{
    public string ShopName { get; set; } = "Vạn An KhachLink";
    public string? LogoUrl { get; set; }
}
