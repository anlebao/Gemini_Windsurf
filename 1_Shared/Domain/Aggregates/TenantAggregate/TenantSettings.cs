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

        // Sprint 7 — Commerce Mode override for this tenant.
        // Inherit (-1): use GlobalCommerceMode (default). Marketplace (0): ép Marketplace. Reseller (1): ép Reseller.
        public CommerceMode CommerceModeOverride { get; private set; } = CommerceMode.Inherit;

        // TT 99 B 09-DN Phần I — Enterprise profile fields for Bản thuyết minh BCTC (Phase 5a)
        // LegalForm: Hình thức sở hữu vốn (VD: "Công ty TNHH", "Công ty Cổ phần")
        // BusinessField: Lĩnh vực kinh doanh (VD: "F&B", "Thương mại", "Sản xuất")
        // CharterCapital: Vốn điều lệ (VND) — used in Phần IV.21 (Vốn CSH)
        public string? LegalForm { get; private set; }
        public string? BusinessField { get; private set; }
        public decimal? CharterCapital { get; private set; }

        // #93 — KhachLink style customization: SystemAdmin can override nav/header/footer colors + logo.
        // Null = use default (hardcoded in KhachLinkLayout/NavMenu CSS).
        // Format: hex color string (VD: "#1a1a2e", "#8B4513").
        public string? NavColor { get; private set; }
        public string? HeaderColor { get; private set; }
        public string? FooterColor { get; private set; }

        // Crawl-to-Onboard Pipeline (2026-08-25, M3 resolved):
        // Raw phone number crawled from trangvangvietnam (supplement source). INTERNAL USE ONLY:
        // - NOT displayed on Pending profile (hide SĐT section per Luật 91/2025 Điều 16)
        // - Used by SysAdmin for owner identity verification during Claim approval
        // - After owner Verify: ContactPhone = owner-provided from Claim form (consented)
        // - CrawledPhone should be deleted post-Verify (data minimization per Luật 91/2025)
        // Note: stored raw (not hashed) — gray area legal risk, user-approved with mitigation.
        public string? CrawledPhone { get; private set; }

        // TT 58/2026/TT-BTC (P2 — DNSN): phương pháp nộp thuế của DN siêu nhỏ.
        // Quyết định danh mục sổ kế toán bắt buộc (Điều 5-8). Null = mặc định VatPercent_TndnPercent.
        // Chỉ đổi từ đầu kỳ kế toán năm (Điều 4.3).
        public DnsnTaxMethod? DnsnTaxMethod { get; private set; }

        // Storefront 2-ảnh background (2026-11-24, user approved):
        // (1) StorefrontBannerUrl — ảnh biển hiệu → hero background /store/{slug}.
        // (2) StorefrontBackdropUrl — ảnh chủ quán/sản phẩm → nền body chìm mờ.
        // Giá trị lưu = R2 object key (upload qua wizard, resolve presigned URL mỗi lần
        // đọc store-info — presigned TTL 7 ngày nên KHÔNG lưu URL trực tiếp) HOẶC
        // absolute http(s) URL ngoài (admin dán tay). Null = fallback gradient CSS.
        public string? StorefrontBannerUrl { get; private set; }
        public string? StorefrontBackdropUrl { get; private set; }

        // Storefront content vertical (2026-11-24, Phương án A): ngành nghề →
        // preset labels trên /store/{slug} (section title, CTA, AI chips, search).
        // Trước đây hardcode F&B — sai với tenant dịch vụ. General = trung lập.
        public StorefrontVertical StorefrontVertical { get; private set; } = StorefrontVertical.General;

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
            ThemeType theme = ThemeType.Classic,
            CommerceMode commerceModeOverride = CommerceMode.Inherit,
            string? navColor = null,
            string? headerColor = null,
            string? footerColor = null,
            string? crawledPhone = null,
            DnsnTaxMethod? dnsnTaxMethod = null,
            string? storefrontBannerUrl = null,
            string? storefrontBackdropUrl = null,
            StorefrontVertical storefrontVertical = StorefrontVertical.General)
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
            CommerceModeOverride = commerceModeOverride;
            NavColor = navColor;
            HeaderColor = headerColor;
            FooterColor = footerColor;
            CrawledPhone = crawledPhone;
            DnsnTaxMethod = dnsnTaxMethod;
            StorefrontBannerUrl = storefrontBannerUrl;
            StorefrontBackdropUrl = storefrontBackdropUrl;
            StorefrontVertical = storefrontVertical;
        }

        public TenantSettings WithContactEmail(string email)
            => new(email, ContactPhone, Address, LogoUrl, TaxCode, Latitude, Longitude, Slug, SocialLinksFb, SocialLinksTiktok, BrandStory, Theme, CommerceModeOverride, NavColor, HeaderColor, FooterColor, CrawledPhone, DnsnTaxMethod, StorefrontBannerUrl, StorefrontBackdropUrl, StorefrontVertical);

        public TenantSettings WithContactPhone(string phone)
            => new(ContactEmail, phone, Address, LogoUrl, TaxCode, Latitude, Longitude, Slug, SocialLinksFb, SocialLinksTiktok, BrandStory, Theme, CommerceModeOverride, NavColor, HeaderColor, FooterColor, CrawledPhone, DnsnTaxMethod, StorefrontBannerUrl, StorefrontBackdropUrl, StorefrontVertical);

        public TenantSettings WithAddress(string address)
            => new(ContactEmail, ContactPhone, address, LogoUrl, TaxCode, Latitude, Longitude, Slug, SocialLinksFb, SocialLinksTiktok, BrandStory, Theme, CommerceModeOverride, NavColor, HeaderColor, FooterColor, CrawledPhone, DnsnTaxMethod, StorefrontBannerUrl, StorefrontBackdropUrl, StorefrontVertical);

        public TenantSettings WithTaxCode(string taxCode)
            => new(ContactEmail, ContactPhone, Address, LogoUrl, taxCode, Latitude, Longitude, Slug, SocialLinksFb, SocialLinksTiktok, BrandStory, Theme, CommerceModeOverride, NavColor, HeaderColor, FooterColor, CrawledPhone, DnsnTaxMethod, StorefrontBannerUrl, StorefrontBackdropUrl, StorefrontVertical);

        public TenantSettings WithCoordinates(double latitude, double longitude)
            => new(ContactEmail, ContactPhone, Address, LogoUrl, TaxCode, latitude, longitude, Slug, SocialLinksFb, SocialLinksTiktok, BrandStory, Theme, CommerceModeOverride, NavColor, HeaderColor, FooterColor, CrawledPhone, DnsnTaxMethod, StorefrontBannerUrl, StorefrontBackdropUrl, StorefrontVertical);

        public TenantSettings WithSlug(string? slug)
            => new(ContactEmail, ContactPhone, Address, LogoUrl, TaxCode, Latitude, Longitude, slug, SocialLinksFb, SocialLinksTiktok, BrandStory, Theme, CommerceModeOverride, NavColor, HeaderColor, FooterColor, CrawledPhone, DnsnTaxMethod, StorefrontBannerUrl, StorefrontBackdropUrl, StorefrontVertical);

        public TenantSettings WithSocialLinks(string? fb, string? tiktok)
            => new(ContactEmail, ContactPhone, Address, LogoUrl, TaxCode, Latitude, Longitude, Slug, fb, tiktok, BrandStory, Theme, CommerceModeOverride, NavColor, HeaderColor, FooterColor, CrawledPhone, DnsnTaxMethod, StorefrontBannerUrl, StorefrontBackdropUrl, StorefrontVertical);

        public TenantSettings WithBrandStory(string? story)
            => new(ContactEmail, ContactPhone, Address, LogoUrl, TaxCode, Latitude, Longitude, Slug, SocialLinksFb, SocialLinksTiktok, story, Theme, CommerceModeOverride, NavColor, HeaderColor, FooterColor, CrawledPhone, DnsnTaxMethod, StorefrontBannerUrl, StorefrontBackdropUrl, StorefrontVertical);

        public TenantSettings WithTheme(ThemeType theme)
            => new(ContactEmail, ContactPhone, Address, LogoUrl, TaxCode, Latitude, Longitude, Slug, SocialLinksFb, SocialLinksTiktok, BrandStory, theme, CommerceModeOverride, NavColor, HeaderColor, FooterColor, CrawledPhone, DnsnTaxMethod, StorefrontBannerUrl, StorefrontBackdropUrl, StorefrontVertical);

        /// <summary>
        /// Sprint 7 — Set commerce mode override for this tenant.
        /// Inherit = use global setting. Marketplace/Reseller = ép mode cho tenant này.
        /// </summary>
        public TenantSettings WithCommerceModeOverride(CommerceMode mode)
            => new(ContactEmail, ContactPhone, Address, LogoUrl, TaxCode, Latitude, Longitude, Slug, SocialLinksFb, SocialLinksTiktok, BrandStory, Theme, mode, NavColor, HeaderColor, FooterColor, CrawledPhone, DnsnTaxMethod, StorefrontBannerUrl, StorefrontBackdropUrl, StorefrontVertical);

        /// <summary>
        /// #93 — Set KhachLink style customization (nav/header/footer colors).
        /// Null = use default hardcoded colors.
        /// </summary>
        public TenantSettings WithStyleColors(string? navColor, string? headerColor, string? footerColor)
            => new(ContactEmail, ContactPhone, Address, LogoUrl, TaxCode, Latitude, Longitude, Slug, SocialLinksFb, SocialLinksTiktok, BrandStory, Theme, CommerceModeOverride, navColor, headerColor, footerColor, CrawledPhone, DnsnTaxMethod, StorefrontBannerUrl, StorefrontBackdropUrl, StorefrontVertical);

        /// <summary>
        /// #93 — Set logo URL for KhachLink header.
        /// </summary>
        public TenantSettings WithLogoUrl(string? logoUrl)
            => new(ContactEmail, ContactPhone, Address, logoUrl, TaxCode, Latitude, Longitude, Slug, SocialLinksFb, SocialLinksTiktok, BrandStory, Theme, CommerceModeOverride, NavColor, HeaderColor, FooterColor, CrawledPhone, DnsnTaxMethod, StorefrontBannerUrl, StorefrontBackdropUrl, StorefrontVertical);

        /// <summary>
        /// Crawl-to-Onboard Pipeline (2026-08-25, M3 resolved): Set raw crawled phone number.
        /// INTERNAL USE ONLY — NOT displayed on Pending profile (hide SĐT section per Luật 91/2025 Điều 16).
        /// SysAdmin uses for owner identity verification during Claim approval.
        /// After owner Verify: ContactPhone = owner-provided (consented); CrawledPhone should be cleared
        /// (data minimization per Luật 91/2025).
        /// </summary>
        public TenantSettings WithCrawledPhone(string? crawledPhone)
            => new(ContactEmail, ContactPhone, Address, LogoUrl, TaxCode, Latitude, Longitude, Slug, SocialLinksFb, SocialLinksTiktok, BrandStory, Theme, CommerceModeOverride, NavColor, HeaderColor, FooterColor, crawledPhone, DnsnTaxMethod, StorefrontBannerUrl, StorefrontBackdropUrl, StorefrontVertical);

        /// <summary>
        /// TT 58/2026 (P2 — DNSN): set phương pháp nộp thuế DN siêu nhỏ (Điều 5-8).
        /// Null = mặc định VatPercent_TndnPercent. Chỉ đổi từ đầu kỳ kế toán năm (Điều 4.3).
        /// </summary>
        public TenantSettings WithDnsnTaxMethod(DnsnTaxMethod? method)
            => new(ContactEmail, ContactPhone, Address, LogoUrl, TaxCode, Latitude, Longitude, Slug, SocialLinksFb, SocialLinksTiktok, BrandStory, Theme, CommerceModeOverride, NavColor, HeaderColor, FooterColor, CrawledPhone, method, StorefrontBannerUrl, StorefrontBackdropUrl, StorefrontVertical);

        /// <summary>
        /// Storefront 2-ảnh background (2026-11-24): set banner (hero) + backdrop (nền chìm)
        /// cho /store/{slug}. Giá trị = R2 key HOẶC absolute URL ngoài; null = fallback CSS.
        /// </summary>
        public TenantSettings WithStorefrontImages(string? bannerUrl, string? backdropUrl)
            => new(ContactEmail, ContactPhone, Address, LogoUrl, TaxCode, Latitude, Longitude, Slug, SocialLinksFb, SocialLinksTiktok, BrandStory, Theme, CommerceModeOverride, NavColor, HeaderColor, FooterColor, CrawledPhone, DnsnTaxMethod, bannerUrl, backdropUrl);

        /// <summary>
        /// Storefront vertical (2026-11-24, Phương án A): set ngành nghề → preset labels
        /// trên /store/{slug}. General = labels trung lập; null không áp dụng (enum non-null).
        /// </summary>
        public TenantSettings WithStorefrontVertical(StorefrontVertical vertical)
            => new(ContactEmail, ContactPhone, Address, LogoUrl, TaxCode, Latitude, Longitude, Slug, SocialLinksFb, SocialLinksTiktok, BrandStory, Theme, CommerceModeOverride, NavColor, HeaderColor, FooterColor, CrawledPhone, DnsnTaxMethod, StorefrontBannerUrl, StorefrontBackdropUrl, vertical);

        public static TenantSettings Empty() => new(null, null, null);
    }
}
