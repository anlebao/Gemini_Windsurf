using VanAn.CoreHub.Services.Onboarding;

namespace VanAn.CoreHub.Services.Claims
{
    /// <summary>
    /// Crawl-to-Onboard Pipeline (2026-08-25): Claim request lifecycle management.
    /// Business owner submits claim for Pending tenant → SysAdmin reviews → Approve (Verify) or Reject.
    /// </summary>
    public interface ITenantClaimService
    {
        /// <summary>
        /// Owner submits a claim for a Pending tenant (GPKD upload + claimant info).
        /// Rejects if tenant is Active ("already verified") or not found.
        /// Rate-limited at API layer (3 req/IP/day — Phase 4).
        /// </summary>
        Task<Guid> SubmitClaimAsync(Guid tenantId, SubmitClaimRequest req, CancellationToken ct = default);

        /// <summary>
        /// SysAdmin approves a claim → calls VerifyAsync (creates owner user + groups + Activate)
        /// + marks claim as Approved. Returns VerifyResult with credentials (shown once).
        /// </summary>
        Task<VanAn.CoreHub.Services.Onboarding.VerifyResult> ApproveClaimAsync(Guid claimRequestId, VerifyTenantRequest adminConfig, Guid sysAdminUserId, CancellationToken ct = default);

        /// <summary>
        /// SysAdmin rejects a claim with reason.
        /// </summary>
        Task RejectClaimAsync(Guid claimRequestId, string reason, Guid sysAdminUserId, CancellationToken ct = default);

        /// <summary>
        /// Lists all Submitted claims (SysAdmin queue).
        /// </summary>
        Task<IReadOnlyList<ClaimDto>> ListPendingClaimsAsync(CancellationToken ct = default);

        /// <summary>
        /// Gets a single claim by ID.
        /// </summary>
        Task<ClaimDto?> GetClaimAsync(Guid claimRequestId, CancellationToken ct = default);
    }
}
