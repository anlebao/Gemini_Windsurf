using VanAn.KhachLink.Models;
using VanAn.Shared.Domain;

namespace VanAn.KhachLink.Pages;

/// <summary>
/// GTM Drill Machine W2 (2026-09-08): In-memory demo storefront state.
/// Session-only — KHÔNG persist DB/localStorage. Refresh = reset demo.
/// Merchant enters shop name + industry → mock storefront with editable products, hours, theme.
/// </summary>
public class DemoStoreState
{
    public string ShopName { get; set; } = string.Empty;
    public string Industry { get; set; } = string.Empty;
    public string? LogoUrl { get; set; }
    public string OpeningHours { get; set; } = "7:00 - 22:00";
    public ThemeType Theme { get; set; } = ThemeType.Classic;
    public List<DemoProduct> Products { get; set; } = new();

    /// <summary>Seed sample products for known industries, or generic fallback for others.</summary>
    public void SeedProductsForIndustry(string industry)
    {
        Industry = industry;
        Products = GetSampleProducts(industry);
    }

    /// <summary>
    /// Hardcoded 3-5 sample products for common industries. Generic fallback for unsupported.
    /// Pattern: name + price (VND) — editable in UI.
    /// </summary>
    private static List<DemoProduct> GetSampleProducts(string industry) => industry.ToLowerInvariant() switch
    {
        "cà phê" or "cafe" or "cà phê sữa" => new()
        {
            new("Cà phê sữa đá", 25000),
            new("Bạc xỉu", 28000),
            new("Cà phê đen", 20000),
            new("Trà đào cam sả", 45000),
            new("Trà sữa trân châu", 50000)
        },
        "phở" or "pho" => new()
        {
            new("Phở bò tái", 55000),
            new("Phở bò chín", 55000),
            new("Phở gà", 50000),
            new("Phở xào", 60000)
        },
        "tạp hóa" or "tạp hoá" or "cửa hàng tạp hóa" => new()
        {
            new("Mì gói (vỉ 5)", 15000),
            new("Nước suối 500ml", 8000),
            new("Gạo 1kg", 25000),
            new("Đường 1kg", 28000)
        },
        "salon" or "tiệm nail" or "nail" => new()
        {
            new("Làm móng tay cơ bản", 80000),
            new("Sơn gel", 120000),
            new("Đắp móng", 150000),
            new("Vẽ móng nghệ thuật", 200000)
        },
        "ăn vặt" or "quán ăn vặt" => new()
        {
            new("Khoai tây chiên", 35000),
            new("Gà rán (3 miếng)", 55000),
            new("Trà sữa", 40000),
            new("Xúc xích nướng", 30000)
        },
        _ => new() // Generic fallback for unsupported industries
        {
            new("Sản phẩm 1", 50000),
            new("Sản phẩm 2", 75000),
            new("Sản phẩm 3", 100000)
        }
    };
}

/// <summary>Editable demo product — display only, no cart behavior.</summary>
public class DemoProduct
{
    public Guid Id { get; } = Guid.NewGuid();
    public string Name { get; set; }
    public decimal Price { get; set; }

    public DemoProduct(string name, decimal price)
    {
        Name = name;
        Price = price;
    }
}
