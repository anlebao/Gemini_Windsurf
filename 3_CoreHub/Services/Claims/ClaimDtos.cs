using VanAn.CoreHub.Services.Onboarding;

namespace VanAn.CoreHub.Services.Claims
{
    /// <summary>
    /// Crawl-to-Onboard (2026-08-25): Owner submits claim for Pending tenant.
    /// All fields required except ClaimantEmail (optional).
    /// </summary>
    public record SubmitClaimRequest(
        string ClaimantName,        // Owner name (required)
        string ClaimantPhone,       // Owner phone (required — consented per M3, sets ContactPhone after Verify)
        string? ClaimantEmail,      // Owner email (optional)
        string GpkdImageUrl,        // URL to uploaded GPKD image (Cloudinary — from Phase 6 Claim form)
        string TaxCodeSubmitted);   // Tax code entered by claimant (cross-checked vs Settings.TaxCode)

    /// <summary>
    /// Crawl-to-Onboard (2026-08-25): Claim request DTO for SysAdmin queue + detail view.
    /// </summary>
    public record ClaimDto(
        Guid Id,
        Guid TenantId,
        string TenantName,          // For display in queue
        string ClaimantName,
        string ClaimantPhone,
        string? ClaimantEmail,
        string GpkdImageUrl,
        string TaxCodeSubmitted,
        string Status,              // Submitted/Approved/Rejected
        DateTime SubmittedAt,
        Guid? ReviewedByUserId,
        DateTime? ReviewedAt,
        string? RejectionReason);
}
