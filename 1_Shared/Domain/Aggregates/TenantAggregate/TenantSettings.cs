namespace VanAn.Shared.Domain.Aggregates.TenantAggregate
{
    /// <summary>
    /// Value object for tenant configuration settings â€” Wave 5
    /// Owned entity â€” stored in Tenants table (no separate table needed)
    /// </summary>
    public class TenantSettings
    {
        public string? ContactEmail { get; private set; }
        public string? ContactPhone { get; private set; }
        public string? Address { get; private set; }
        public string? LogoUrl { get; private set; }
        public string? TaxCode { get; private set; }

        // Store Finder â€” geographic coordinates (migrated from Shop entity, 2026-07-21)
        public double? Latitude { get; private set; }
        public double? Longitude { get; private set; }

        // Tenant Profile Page (2026-07-21): URL slug for /store/{slug} route.
        // Unique across tenants. Lowercase, alphanumeric + hyphens only. Max 100 chars.
        // Null = no public profile page (use GUID fallback).
        public string? Slug { get; private set; }

        // Tenant Profile Page (2026-07-21): Social media links for /store/{slug} Social Hub section.
        // Full URL to Facebook page + TikTok profile/video. Null = section hidden.
        public string? SocialLinksFb { get; private set; }
        public string? SocialLinksTiktok { get; private set; }

        // Tenant Profile Page (2026-07-21): Short brand story shown in hero section of /store/{slug}.
        // Max 500 chars. Null = no story section.
        public string? BrandStory { get; private set; }

        // Theme Customization (2026-07-22): KhachLink UI theme selected by SysAdmin.
        // Stored as int in DB (ThemeType enum). Default Classic = 0.
        public ThemeType Theme { get; private set; } = ThemeType.Classic;

        // EF Core requires parameterless constructor
        private TenantSettings() { }

        public TenantSettings(
            string? contactEmail,
            string? contactPhone,
            string? address,
            string? logoUrl = null,
            string? taxCode = null,
            double? latitude = null,
            double? longitude = null,
            string? slug = null,
            string? socialLinksFb = null,
            string? socialLinksTiktok = null,
            string? brandStory = null,
            ThemeType theme = ThemeType.Classic)
        {
            ContactEmail = contactEmail;
            ContactPhone = contactPhone;
            Address = address;
            LogoUrl = logoUrl;
            TaxCode = taxCode;
            Latitude = latitude;
            Longitude = longitude;
            Slug = slug;
            SocialLinksFb = socialLinksFb;
            SocialLinksTiktok = socialLinksTiktok;
            BrandStory = brandStory;
            Theme = theme;
        }

        public TenantSettings WithContactEmail(string email)
            => new(email, ContactPhone, Address, LogoUrl, TaxCode, Latitude, Longitude, Slug, SocialLinksFb, SocialLinksTiktok, BrandStory, Theme);

        public TenantSettings WithContactPhone(string phone)
            => new(ContactEmail, phone, Address, LogoUrl, TaxCode, Latitude, Longitude, Slug, SocialLinksFb, SocialLinksTiktok, BrandStory, Theme);

        public TenantSettings WithAddress(string address)
            => new(ContactEmail, ContactPhone, address, LogoUrl, TaxCode, Latitude, Longitude, Slug, SocialLinksFb, SocialLinksTiktok, BrandStory, Theme);

        public TenantSettings WithTaxCode(string taxCode)
            => new(ContactEmail, ContactPhone, Address, LogoUrl, taxCode, Latitude, Longitude, Slug, SocialLinksFb, SocialLinksTiktok, BrandStory, Theme);

        public TenantSettings WithCoordinates(double latitude, double longitude)
            => new(ContactEmail, ContactPhone, Address, LogoUrl, TaxCode, latitude, longitude, Slug, SocialLinksFb, SocialLinksTiktok, BrandStory, Theme);

        public TenantSettings WithSlug(string? slug)
            => new(ContactEmail, ContactPhone, Address, LogoUrl, TaxCode, Latitude, Longitude, slug, SocialLinksFb, SocialLinksTiktok, BrandStory, Theme);

        public TenantSettings WithSocialLinks(string? fb, string? tiktok)
            => new(ContactEmail, ContactPhone, Address, LogoUrl, TaxCode, Latitude, Longitude, Slug, fb, tiktok, BrandStory, Theme);

        public TenantSettings WithBrandStory(string? story)
            => new(ContactEmail, ContactPhone, Address, LogoUrl, TaxCode, Latitude, Longitude, Slug, SocialLinksFb, SocialLinksTiktok, story, Theme);

        public TenantSettings WithTheme(ThemeType theme)
            => new(ContactEmail, ContactPhone, Address, LogoUrl, TaxCode, Latitude, Longitude, Slug, SocialLinksFb, SocialLinksTiktok, BrandStory, theme);

        public static TenantSettings Empty() => new(null, null, null);
    }
}
