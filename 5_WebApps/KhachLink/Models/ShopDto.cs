namespace VanAn.KhachLink.Models;

/// <summary>
/// DTO for Shop data received from ShopERP via Gateway (GET /api/shops/by-tenant/{tenantId}).
/// Mirrors the subset of Shop entity fields needed to build a ShopConfig.
/// Branding fields (PrimaryColor, SecondaryColor, Theme) are NOT stored on Shop —
/// they remain at their defaults in ShopConfig.
/// </summary>
public class ShopDto
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Address { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }
}
