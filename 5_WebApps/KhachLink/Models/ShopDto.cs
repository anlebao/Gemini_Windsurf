namespace VanAn.KhachLink.Models;

using System.Text.Json.Serialization;
using VanAn.Shared.Domain;

/// <summary>
/// DTO for Shop data received from ShopERP via Gateway (GET /api/shops/by-tenant/{tenantId}).
/// Mirrors the subset of Shop entity fields needed to build a ShopConfig.
/// Branding fields (PrimaryColor, SecondaryColor, Theme) are NOT stored on Shop â€”
/// they remain at their defaults in ShopConfig.
/// </summary>
public class ShopDto
{
    public Guid Id { get; set; }

    // ShopERP returns TenantId as nested object {"value":"guid"} because TenantId is a ValueObject.
    // Use TenantIdValue for deserialization, then expose as Guid via TenantId property.
    [JsonPropertyName("tenantId")]
    public TenantIdJson TenantIdWrapper { get; set; } = new();

    [JsonIgnore]
    public Guid TenantId => TenantIdWrapper?.Value ?? Guid.Empty;

    public string Name { get; set; } = string.Empty;
    public string Address { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }
    /// <summary>Tenant Profile Page (2026-07-21): URL slug for /store/{slug}. Null if not set.</summary>
    public string? Slug { get; set; }
    public string? SocialLinksFb { get; set; }
    public string? SocialLinksTiktok { get; set; }
    public string? BrandStory { get; set; }
    public string? LogoUrl { get; set; }
    public ThemeType Theme { get; set; } = ThemeType.Classic;
    // #93 — KhachLink style customization colors
    public string? NavColor { get; set; }
    public string? HeaderColor { get; set; }
    public string? FooterColor { get; set; }
}

/// <summary>
/// Wrapper for TenantId ValueObject serialized as {"value":"guid"}
/// </summary>
public class TenantIdJson
{
    public Guid Value { get; set; }
}
