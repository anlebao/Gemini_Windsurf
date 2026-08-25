using VanAn.Shared.Domain;

namespace VanAn.CoreHub.Services.Onboarding
{
    // ── Crawl-to-Onboard Pipeline DTOs (2026-08-25) ──────────────────────────

    /// <summary>
    /// Crawl-to-Onboard (2026-08-25): Business listing data crawled from external sources.
    /// Used by OnboardUnverifiedAsync to create Pending tenants.
    ///
    /// Sources:
    /// - doanhnghiep.vn API (primary — legal business registration data per Luật Doanh nghiệp 2020)
    /// - trangvangvietnam HTML (supplement — SĐT only, internal use per M3)
    /// - xinvoice.vn API (verify — MST cross-check)
    /// </summary>
    public record CrawlListingDto(
        string Name,                    // Business name (from doanhnghiep.vn name_vi)
        string? TaxCode,                // MST (from doanhnghiep.vn mst)
        string? Address,                // Business address (from doanhnghiep.vn address_full)
        string? CrawledPhone,           // SĐT from trangvangvietnam (M3: internal use, NOT displayed on Pending profile)
        string? ContactName,            // Legal rep name (from doanhnghiep.vn legal_rep_name)
        string? IndustryCode,           // Industry code (from doanhnghiep.vn industry_main_code)
        string SourceSite,              // Source site name (e.g., "doanhnghiep.vn")
        string SourceUrl,               // Full URL of crawled listing
        DateTime CrawledAt,             // Crawl timestamp
        double? Lat = null,             // Latitude (if available from source)
        double? Lng = null);            // Longitude (if available from source)

    /// <summary>
    /// Crawl-to-Onboard (2026-08-25): Request to verify a Pending tenant → Active.
    /// Used by VerifyAsync (called directly by SysAdmin or via ApproveClaimAsync).
    ///
    /// M3: OwnerPhone is the SĐT from owner Claim form (consented) — NOT from CrawledPhone.
    /// After Verify, ContactPhone = OwnerPhone (consented), CrawledPhone kept internal (or cleared per data minimization).
    /// </summary>
    public record VerifyTenantRequest(
        string OwnerUsername,           // Login username for new admin user
        string OwnerPassword,           // Initial password (shown once to SysAdmin)
        string OwnerDisplayName,        // Display name for admin user
        string? OwnerPhone = null,      // SĐT from owner Claim form (consented per M3) — sets ContactPhone
        string? OwnerEmail = null,      // Email from owner Claim form (optional)
        Guid? ShopInstanceId = null,    // ShopERP hosting instance (Multi-VPS routing)
        string? Slug = null,            // Clean slug (null = auto-generate from name)
        Guid ApprovedByUserId = default); // SysAdmin who approved (for TenantVerifiedEvent)

    /// <summary>
    /// Crawl-to-Onboard (2026-08-25): Result of VerifyAsync.
    /// </summary>
    public record VerifyResult(
        Guid TenantId,
        Guid OwnerUserId,
        int PermissionGroupsCreated,
        string PublishedSlug);
}
