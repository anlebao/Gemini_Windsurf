using VanAn.Shared.Domain.Common;

namespace VanAn.Shared.Domain.Aggregates.KhachLinkAggregate
{
    /// <summary>
    /// KhachLink instance — 1 deployment entry point với profile + nav flags riêng.
    /// Platform-level routing entity (follows ShopInstance pattern):
    /// - TenantId = Guid.Empty (platform sentinel, excluded from multi-tenancy query filter)
    /// - No business key VO (Single-Identity Pattern, Id = PK only)
    /// - Not AggregateRoot (no domain events — routing config entity)
    ///
    /// OwnerTenantId:
    /// - null = platform-level instance (Type 1 directory, Type 2 logistics, Type 3 jobs)
    /// - non-null = tenant-owned instance (Type 4 full commerce, Type 5 reseller)
    ///
    /// Resolved at runtime by CustomDomain (nginx Host header → GET /api/v1/khachlink-instances/by-domain/{domain}).
    /// </summary>
    public class KhachLinkInstance : BaseEntity
    {
        /// <summary>Human-readable label — "Danh bạ Vạn An", "KhachLink Shop A"</summary>
        public string Label { get; private set; } = string.Empty;

        /// <summary>Profile type — defines feature set + default nav flags</summary>
        public KhachLinkProfile Profile { get; private set; } = KhachLinkProfile.FullCommerce;

        /// <summary>
        /// Custom domain for this instance — "diemthuong2.khachvip.online", "shopA.khachvip.online".
        /// nginx routes by Host header. Must be unique across all instances.
        /// Stored as hostname only (lowercase, no scheme/path/port) — canonicalized on create.
        /// </summary>
        public string CustomDomain { get; private set; } = string.Empty;

        /// <summary>
        /// Owner tenant — null for platform-level instances (Type 1, 2, 3).
        /// Non-null for tenant-owned instances (Type 4, 5).
        /// When non-null, KhachLink uses this tenant as default context (instead of LastInteractionService).
        /// Note: This is distinct from BaseEntity.TenantId (which is always Guid.Empty — platform sentinel).
        /// </summary>
        public Guid? OwnerTenantId { get; private set; }

        /// <summary>Nav item visibility — owned entity, stored in same table (15 flattened bool columns)</summary>
        public KhachLinkNavFlags NavFlags { get; private set; } = new();

        /// <summary>Whether this instance is active and serving traffic.</summary>
        public bool IsActive { get; private set; } = true;

        // ── Issue #143: Style override fields ──────────────────────────────────
        // When non-null, these override the tenant ShopConfig style in KhachLinkLayout.
        // Platform-level instances (OwnerTenantId=null) use these as the sole style source.
        // Tenant-owned instances use these to override the tenant's default branding.

        /// <summary>Theme name (Classic/Modern/Teen/Lady/Premium). Null = inherit from tenant ShopConfig.</summary>
        public string? Theme { get; private set; }

        /// <summary>Logo URL for KhachLink header. Null = inherit from tenant ShopConfig (or default icon).</summary>
        public string? LogoUrl { get; private set; }

        /// <summary>Nav (sidebar) color override. Null = inherit from tenant ShopConfig.</summary>
        public string? NavColor { get; private set; }

        /// <summary>Header (top bar) color override. Null = inherit from tenant ShopConfig.</summary>
        public string? HeaderColor { get; private set; }

        /// <summary>Footer color override. Null = inherit from tenant ShopConfig.</summary>
        public string? FooterColor { get; private set; }

        // EF Core materialization
        private KhachLinkInstance() { }

        /// <summary>
        /// Factory: create a new KhachLinkInstance.
        /// TenantId is always Guid.Empty (platform sentinel) — this entity is NOT tenant-scoped.
        /// NavFlags defaults to ForProfile(profile) if not overridden.
        /// </summary>
        public KhachLinkInstance(
            string label,
            KhachLinkProfile profile,
            string customDomain,
            Guid? ownerTenantId = null,
            KhachLinkNavFlags? navFlagsOverride = null)
            : base(new TenantId(Guid.Empty)) // platform-level entity, not tenant-scoped
        {
            if (string.IsNullOrWhiteSpace(label))
                throw new ArgumentException("Label cannot be empty.", nameof(label));
            if (string.IsNullOrWhiteSpace(customDomain))
                throw new ArgumentException("CustomDomain cannot be empty.", nameof(customDomain));

            Label = label;
            Profile = profile;
            CustomDomain = CanonicalizeDomain(customDomain);
            OwnerTenantId = ownerTenantId;
            NavFlags = navFlagsOverride ?? KhachLinkNavFlags.ForProfile(profile);
            IsActive = true;
        }

        /// <summary>
        /// Canonicalize domain input: strip scheme, path, port, trailing slash.
        /// Accepts: "sanjob.com", "https://sanjob.com", "sanjob.com/", "SANJOB.COM"
        /// Returns: "sanjob.com" (hostname only, lowercase, no trailing slash)
        /// </summary>
        private static string CanonicalizeDomain(string input)
        {
            var trimmed = input.Trim().ToLowerInvariant();

            // Strip scheme if admin included it
            if (trimmed.StartsWith("https://"))
                trimmed = trimmed["https://".Length..];
            else if (trimmed.StartsWith("http://"))
                trimmed = trimmed["http://".Length..];

            // Parse as URI to extract hostname (handles path, port, trailing slash)
            // If no scheme, prepend "https://" for Uri.TryParse to work
            if (!trimmed.Contains("://"))
                trimmed = "https://" + trimmed;

            if (Uri.TryCreate(trimmed, UriKind.Absolute, out var uri) && !string.IsNullOrEmpty(uri.Host))
                return uri.Host;  // hostname only, lowercase, no path/port/slash

            // Fallback: strip path manually if Uri parse fails
            var slashIdx = trimmed.IndexOf('/');
            if (slashIdx > 0)
                trimmed = trimmed[..slashIdx];

            return trimmed.TrimEnd('/');
        }

        /// <summary>
        /// Update profile + reset nav flags to preset (or override).
        /// </summary>
        public void UpdateProfile(KhachLinkProfile profile, KhachLinkNavFlags? navFlagsOverride = null)
        {
            Profile = profile;
            NavFlags = navFlagsOverride ?? KhachLinkNavFlags.ForProfile(profile);
            UpdateAudit();
        }

        /// <summary>
        /// Override individual nav flags (without changing profile).
        /// </summary>
        public void UpdateNavFlags(KhachLinkNavFlags flags)
        {
            NavFlags = flags ?? throw new ArgumentNullException(nameof(flags));
            UpdateAudit();
        }

        /// <summary>Activate this instance (serve traffic).</summary>
        public void Activate()
        {
            IsActive = true;
            UpdateAudit();
        }

        /// <summary>Deactivate this instance (soft delete — stop serving traffic, keep record).</summary>
        public void Deactivate()
        {
            IsActive = false;
            UpdateAudit();
        }

        /// <summary>Update the display label.</summary>
        public void UpdateLabel(string label)
        {
            if (string.IsNullOrWhiteSpace(label))
                throw new ArgumentException("Label cannot be empty.", nameof(label));
            Label = label;
            UpdateAudit();
        }

        /// <summary>
        /// Issue #143: Update style override fields (Theme, LogoUrl, NavColor, HeaderColor, FooterColor).
        /// Pass null to clear an override (inherit from tenant ShopConfig).
        /// Empty string is treated as null (no override).
        /// </summary>
        public void UpdateStyle(string? theme, string? logoUrl, string? navColor, string? headerColor, string? footerColor)
        {
            Theme = string.IsNullOrWhiteSpace(theme) ? null : theme;
            LogoUrl = string.IsNullOrWhiteSpace(logoUrl) ? null : logoUrl;
            NavColor = string.IsNullOrWhiteSpace(navColor) ? null : navColor;
            HeaderColor = string.IsNullOrWhiteSpace(headerColor) ? null : headerColor;
            FooterColor = string.IsNullOrWhiteSpace(footerColor) ? null : footerColor;
            UpdateAudit();
        }
    }
}
