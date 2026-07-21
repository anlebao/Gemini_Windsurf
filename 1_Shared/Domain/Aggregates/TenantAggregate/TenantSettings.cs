namespace VanAn.Shared.Domain.Aggregates.TenantAggregate
{
    /// <summary>
    /// Value object for tenant configuration settings — Wave 5
    /// Owned entity — stored in Tenants table (no separate table needed)
    /// </summary>
    public class TenantSettings
    {
        public string? ContactEmail { get; private set; }
        public string? ContactPhone { get; private set; }
        public string? Address { get; private set; }
        public string? LogoUrl { get; private set; }
        public string? TaxCode { get; private set; }

        // Store Finder — geographic coordinates (migrated from Shop entity, 2026-07-21)
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
            string? brandStory = null)
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
        }

        public TenantSettings WithContactEmail(string email)
            => new(email, ContactPhone, Address, LogoUrl, TaxCode, Latitude, Longitude, Slug, SocialLinksFb, SocialLinksTiktok, BrandStory);

        public TenantSettings WithContactPhone(string phone)
            => new(ContactEmail, phone, Address, LogoUrl, TaxCode, Latitude, Longitude, Slug, SocialLinksFb, SocialLinksTiktok, BrandStory);

        public TenantSettings WithAddress(string address)
            => new(ContactEmail, ContactPhone, address, LogoUrl, TaxCode, Latitude, Longitude, Slug, SocialLinksFb, SocialLinksTiktok, BrandStory);

        public TenantSettings WithTaxCode(string taxCode)
            => new(ContactEmail, ContactPhone, Address, LogoUrl, taxCode, Latitude, Longitude, Slug, SocialLinksFb, SocialLinksTiktok, BrandStory);

        public TenantSettings WithCoordinates(double latitude, double longitude)
            => new(ContactEmail, ContactPhone, Address, LogoUrl, TaxCode, latitude, longitude, Slug, SocialLinksFb, SocialLinksTiktok, BrandStory);

        public TenantSettings WithSlug(string? slug)
            => new(ContactEmail, ContactPhone, Address, LogoUrl, TaxCode, Latitude, Longitude, slug, SocialLinksFb, SocialLinksTiktok, BrandStory);

        public TenantSettings WithSocialLinks(string? fb, string? tiktok)
            => new(ContactEmail, ContactPhone, Address, LogoUrl, TaxCode, Latitude, Longitude, Slug, fb, tiktok, BrandStory);

        public TenantSettings WithBrandStory(string? story)
            => new(ContactEmail, ContactPhone, Address, LogoUrl, TaxCode, Latitude, Longitude, Slug, SocialLinksFb, SocialLinksTiktok, story);

        public static TenantSettings Empty() => new(null, null, null);
    }
}
