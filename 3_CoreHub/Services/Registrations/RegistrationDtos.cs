namespace VanAn.CoreHub.Services.Registrations
{
    /// <summary>
    /// GTM Drill Machine W2 (2026-09-08): Request DTO for merchant registration submission.
    /// Submitted by merchant via KhachLink Register form (route /claim).
    /// </summary>
    public record SubmitRegistrationRequest(
        string ShopName,           // Shop/business name (required — from demo prefill or direct input)
        string ContactName,        // Contact person name (required)
        string ContactPhone,       // Contact phone (required — admin uses for follow-up)
        string Source,              // Registration source: "demo" | "audit" | "direct"
        string? TurnstileToken,     // Cloudflare Turnstile token (server-side verified)
        string? Industry = null,   // Optional industry from demo
        string? LogoUrl = null,    // Optional logo URL from demo upload
        string? ContactEmail = null, // Optional contact email
        string? HoneypotWebsite = null); // Honeypot field — bot fills → silent reject

    /// <summary>
    /// Result DTO for registration submission — returned to KhachLink client.
    /// </summary>
    public record RegistrationSubmitResult(Guid RegistrationId, string Message);

    /// <summary>
    /// Admin queue DTO for SysAdmin review — mirrors TenantClaimRequest.ClaimDto pattern.
    /// </summary>
    public record RegistrationDto(
        Guid Id,
        string ShopName,
        string? Industry,
        string? LogoUrl,
        string ContactName,
        string ContactPhone,
        string? ContactEmail,
        string Source,
        bool TurnstileVerified,
        string Status,              // Submitted/Contacted/Onboarded/Rejected
        DateTime SubmittedAt,
        Guid? ReviewedByUserId,
        DateTime? ReviewedAt,
        string? RejectionReason,
        Guid? OnboardedTenantId);
}
