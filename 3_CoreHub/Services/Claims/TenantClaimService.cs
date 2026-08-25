using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using VanAn.CoreHub.Infrastructure;
using VanAn.CoreHub.Services.Onboarding;
using VanAn.Shared.Domain;
using VanAn.Shared.Domain.Aggregates.TenantAggregate;

namespace VanAn.CoreHub.Services.Claims
{
    /// <summary>
    /// Crawl-to-Onboard Pipeline (2026-08-25): Claim request lifecycle management.
    /// Orchestrates: owner submit claim → SysAdmin review → Approve (Verify) or Reject.
    /// </summary>
    public class TenantClaimService(
        IVanAnDbContext dbContext,
        ITenantOnboardingService onboardingService,
        ILogger<TenantClaimService> logger) : ITenantClaimService
    {
        public async Task<Guid> SubmitClaimAsync(
            Guid tenantId,
            SubmitClaimRequest req,
            CancellationToken ct = default)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(req.ClaimantName);
            ArgumentException.ThrowIfNullOrWhiteSpace(req.ClaimantPhone);
            ArgumentException.ThrowIfNullOrWhiteSpace(req.GpkdImageUrl);
            ArgumentException.ThrowIfNullOrWhiteSpace(req.TaxCodeSubmitted);

            var tenantIdVo = new TenantId(tenantId);
            var tenant = await dbContext.Tenants
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(t => t.Id == tenantIdVo, ct)
                ?? throw new KeyNotFoundException($"Tenant {tenantId} not found.");

            if (tenant.Status == TenantStatus.Active)
                throw new InvalidOperationException("Tenant is already verified — cannot submit claim.");

            if (tenant.Status != TenantStatus.Pending)
                throw new InvalidOperationException($"Cannot submit claim for tenant in status {tenant.Status}. Only Pending tenants accept claims.");

            var claim = TenantClaimRequest.Create(
                tenantIdVo,
                req.ClaimantName,
                req.ClaimantPhone,
                req.GpkdImageUrl,
                req.TaxCodeSubmitted,
                req.ClaimantEmail);

            dbContext.TenantClaimRequests.Add(claim);
            await dbContext.SaveChangesAsync(ct);

            logger.LogInformation(
                "Claim submitted for tenant {TenantId} by {ClaimantName} — claim {ClaimId}",
                tenantId, req.ClaimantName, claim.Id);

            return claim.Id;
        }

        public async Task<VanAn.CoreHub.Services.Onboarding.VerifyResult> ApproveClaimAsync(
            Guid claimRequestId,
            VerifyTenantRequest adminConfig,
            Guid sysAdminUserId,
            CancellationToken ct = default)
        {
            var claim = await dbContext.TenantClaimRequests
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(c => c.Id == claimRequestId, ct)
                ?? throw new KeyNotFoundException($"Claim request {claimRequestId} not found.");

            if (!claim.IsSubmitted())
                throw new InvalidOperationException($"Cannot approve claim in status {claim.Status}. Only Submitted claims can be approved.");

            // Set ApprovedByUserId on the verify config
            adminConfig = adminConfig with { ApprovedByUserId = sysAdminUserId };

            // Reuse VerifyAsync (DRY — creates owner user + groups + Activate + outbox publish)
            var result = await onboardingService.VerifyAsync(claim.TenantId.Value, adminConfig, ct);

            // Mark claim as Approved (with owner user ID from Verify result)
            claim.Approve(sysAdminUserId, result.OwnerUserId);
            await dbContext.SaveChangesAsync(ct);

            logger.LogInformation(
                "Claim {ClaimId} approved by SysAdmin {SysAdminUserId} — tenant {TenantId} verified, owner user {OwnerUserId}",
                claimRequestId, sysAdminUserId, result.TenantId, result.OwnerUserId);

            return result;
        }

        public async Task RejectClaimAsync(
            Guid claimRequestId,
            string reason,
            Guid sysAdminUserId,
            CancellationToken ct = default)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(reason);

            var claim = await dbContext.TenantClaimRequests
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(c => c.Id == claimRequestId, ct)
                ?? throw new KeyNotFoundException($"Claim request {claimRequestId} not found.");

            claim.Reject(sysAdminUserId, reason);
            await dbContext.SaveChangesAsync(ct);

            logger.LogInformation(
                "Claim {ClaimId} rejected by SysAdmin {SysAdminUserId} — reason: {Reason}",
                claimRequestId, sysAdminUserId, reason);
        }

        public async Task<IReadOnlyList<ClaimDto>> ListPendingClaimsAsync(CancellationToken ct = default)
        {
            var claims = await dbContext.TenantClaimRequests
                .IgnoreQueryFilters()
                .Where(c => c.Status == TenantClaimRequest.ClaimStatus.Submitted)
                .OrderBy(c => c.SubmittedAt)
                .ToListAsync(ct);

            // Load tenant names for display (batch query)
            var tenantIds = claims.Select(c => c.TenantId).Distinct().ToList();
            var tenants = await dbContext.Tenants
                .IgnoreQueryFilters()
                .Where(t => tenantIds.Contains(t.Id))
                .ToDictionaryAsync(t => t.Id, t => t.Name, ct);

            return claims.Select(c => MapToDto(c, tenants.GetValueOrDefault(c.Id))).ToList();
        }

        public async Task<ClaimDto?> GetClaimAsync(Guid claimRequestId, CancellationToken ct = default)
        {
            var claim = await dbContext.TenantClaimRequests
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(c => c.Id == claimRequestId, ct);
            if (claim is null) return null;

            var tenant = await dbContext.Tenants
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(t => t.Id == claim.TenantId, ct);

            return MapToDto(claim, tenant?.Name);
        }

        private static ClaimDto MapToDto(TenantClaimRequest claim, string? tenantName)
            => new(
                Id: claim.Id,
                TenantId: claim.TenantId.Value,
                TenantName: tenantName ?? "(unknown)",
                ClaimantName: claim.ClaimantName,
                ClaimantPhone: claim.ClaimantPhone,
                ClaimantEmail: claim.ClaimantEmail,
                GpkdImageUrl: claim.GpkdImageUrl,
                TaxCodeSubmitted: claim.TaxCodeSubmitted,
                Status: claim.Status.ToString(),
                SubmittedAt: claim.SubmittedAt,
                ReviewedByUserId: claim.ReviewedByUserId,
                ReviewedAt: claim.ReviewedAt,
                RejectionReason: claim.RejectionReason);
    }
}
