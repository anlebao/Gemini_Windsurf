using VanAn.Shared.Domain.Common;

namespace VanAn.Shared.Domain.Aggregates.TenantAggregate
{
    /// <summary>
    /// Raised when a new Tenant is created â€” triggers welcome email â€” Wave 5
    /// </summary>
    public sealed record TenantCreatedEvent(
        Guid TenantId,
        string TenantName,
        string? ContactEmail,
        DateTime OccurredAt) : IDomainEvent
    {
        public Guid EventId { get; } = Guid.NewGuid();
    }

    /// <summary>
    /// Raised when a Tenant is suspended â€” Wave 5
    /// </summary>
    public sealed record TenantSuspendedEvent(
        Guid TenantId,
        string Reason,
        DateTime OccurredAt) : IDomainEvent
    {
        public Guid EventId { get; } = Guid.NewGuid();
    }

    /// <summary>
    /// Raised when a Tenant is permanently deactivated â€” Wave 5
    /// </summary>
    public sealed record TenantDeactivatedEvent(
        Guid TenantId,
        string Reason,
        DateTime OccurredAt) : IDomainEvent
    {
        public Guid EventId { get; } = Guid.NewGuid();
    }

    /// <summary>
    /// Raised when an HKD tenant is converted to a DN tenant (D9 Option B).
    /// Wave 2: Domain event only â€” outbox handler/consumer is W8 scope.
    /// </summary>
    public sealed record TenantConvertedEvent(
        Guid TenantId,           // HKD tenant being converted
        Guid SuccessorTenantId,  // New DN tenant created from conversion
        DateTime OccurredAt) : IDomainEvent
    {
        public Guid EventId { get; } = Guid.NewGuid();
    }

    /// <summary>
    /// Bug 1 fix (approved 2026-08-03): Raised when SystemAdmin changes tenant BusinessType.
    /// Audit trail for type correction (Company ↔ HouseholdBusiness).
    /// </summary>
    public sealed record TenantBusinessTypeChangedEvent(
        Guid TenantId,
        BusinessType NewBusinessType,
        HKDGroup? NewHkdGroup,
        string Reason,
        DateTime OccurredAt) : IDomainEvent
    {
        public Guid EventId { get; } = Guid.NewGuid();
    }

    // ── Crawl-to-Onboard Pipeline events (2026-08-25) ───────────────────────

    /// <summary>
    /// Crawl-to-Onboard (2026-08-25): Raised when a Pending tenant is created from crawled
    /// business listing. NOT TenantCreatedEvent — no welcome email (tenant not yet verified,
    /// no owner user yet). SourceUrl is the crawl source (doanhnghiep.vn/trangvangvietnam).
    /// </summary>
    public sealed record TenantPendingEvent(
        Guid TenantId,
        string TenantName,
        string? TaxCode,
        string? SourceUrl,
        DateTime OccurredAt) : IDomainEvent
    {
        public Guid EventId { get; } = Guid.NewGuid();
    }

    /// <summary>
    /// Crawl-to-Onboard (2026-08-25): Raised when a Pending tenant is Verified → Active.
    /// Trigger: owner Claim + SysAdmin Approve (or direct SysAdmin verify bypass).
    /// Option A: This event is published to NATS (subject vanan.cloud.tenant.verified)
    /// → TenantSyncSubscriber (ShopERP) upserts Tenant row in SQLite with same Guid tenantId
    /// → ensures tenant identity consistency PG↔SQLite (avoids accounting split).
    /// ApprovedByUserId: SysAdmin who approved the claim (set by service layer).
    /// </summary>
    public sealed record TenantVerifiedEvent(
        Guid TenantId,
        Guid ApprovedByUserId,
        DateTime OccurredAt) : IDomainEvent
    {
        public Guid EventId { get; } = Guid.NewGuid();
    }

    /// <summary>
    /// Crawl-to-Onboard (2026-08-25): Raised when business owner submits a Claim request
    /// for a Pending tenant (GPKD upload + claimant info). Goes to SysAdmin queue for review.
    /// </summary>
    public sealed record TenantClaimRequestedEvent(
        Guid TenantId,
        Guid ClaimRequestId,
        string ClaimantName,
        DateTime OccurredAt) : IDomainEvent
    {
        public Guid EventId { get; } = Guid.NewGuid();
    }

    /// <summary>
    /// Crawl-to-Onboard (2026-08-25): Raised when SysAdmin approves a Claim request.
    /// Tenant transitions Pending → Active (TenantVerifiedEvent also raised by Verify()).
    /// OwnerUserId: new admin user created for the tenant during Verify.
    /// </summary>
    public sealed record TenantClaimApprovedEvent(
        Guid TenantId,
        Guid ClaimRequestId,
        Guid OwnerUserId,
        Guid ApprovedByUserId,
        DateTime OccurredAt) : IDomainEvent
    {
        public Guid EventId { get; } = Guid.NewGuid();
    }

    /// <summary>
    /// Crawl-to-Onboard (2026-08-25, Option A — H7): Raised when SystemAdmin updates an
    /// Active tenant's profile (name, settings — slug, contactPhone, address, etc.).
    /// Published to NATS (subject vanan.cloud.tenant.profile.updated)
    /// → TenantSyncSubscriber (ShopERP) updates SQLite row → keeps SQLite in sync with PG.
    /// Without this event, SQLite tenant row becomes stale after admin profile update
    /// → ProductsController.cs:146 / UserManagement.razor.cs:45 show wrong tenant info.
    ///
    /// TenantSettingsSnapshot: lightweight snapshot of key Settings fields for SQLite upsert
    /// (slug, contactPhone, address, taxCode, brandStory, logoUrl, lat, lng, theme,
    /// navColor, headerColor, footerColor, socialLinksFb, socialLinksTiktok).
    /// Note: CrawledPhone NOT included in snapshot (internal field, not synced to SQLite).
    /// </summary>
    public sealed record TenantProfileUpdatedEvent(
        Guid TenantId,
        string NewName,
        TenantSettingsSnapshot Settings,
        DateTime OccurredAt) : IDomainEvent
    {
        public Guid EventId { get; } = Guid.NewGuid();
    }

    /// <summary>
    /// Crawl-to-Onboard (2026-08-25, Option A — H7): Lightweight snapshot of TenantSettings
    /// for NATS sync. Only includes fields that need to be synced to ShopERP SQLite
    /// (used by ProductsController tenant name lookup + UserManagement dropdown).
    /// Excludes CrawledPhone (internal, not synced) + LegalForm/BusinessField/CharterCapital
    /// (financial reporting fields, PG-only — FinancialStatementNotesService reads from PG).
    /// </summary>
    public sealed record TenantSettingsSnapshot(
        string? ContactEmail,
        string? ContactPhone,
        string? Address,
        string? LogoUrl,
        string? TaxCode,
        double? Latitude,
        double? Longitude,
        string? Slug,
        string? SocialLinksFb,
        string? SocialLinksTiktok,
        string? BrandStory,
        int Theme,  // ThemeType enum as int for serialization simplicity
        int CommerceModeOverride,  // CommerceMode enum as int
        string? NavColor,
        string? HeaderColor,
        string? FooterColor)
    {
        /// <summary>Factory to create snapshot from TenantSettings.</summary>
        public static TenantSettingsSnapshot From(TenantSettings s) => new(
            s.ContactEmail, s.ContactPhone, s.Address, s.LogoUrl, s.TaxCode,
            s.Latitude, s.Longitude, s.Slug, s.SocialLinksFb, s.SocialLinksTiktok,
            s.BrandStory, (int)s.Theme, (int)s.CommerceModeOverride,
            s.NavColor, s.HeaderColor, s.FooterColor);
    }
}
