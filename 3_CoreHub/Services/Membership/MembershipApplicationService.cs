using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using VanAn.CoreHub.Infrastructure;
using VanAn.Shared.Domain;
using VanAn.Shared.Domain.Aggregates.MembershipAggregate;

namespace VanAn.CoreHub.Services.Membership
{
    /// <summary>
    /// Membership Infrastructure (2026-09-29): Lifecycle của hồ sơ xin gia nhập HTX.
    /// Template: TenantClaimService (load với IgnoreQueryFilters + guard status + domain transitions).
    ///
    /// Data Integrity Contract (master_plan mục 6):
    /// - Duplicate guard: 1 (htx, applicant) chỉ 1 application active (Draft/Submitted/NeedInfo).
    /// - ApproveAsync tạo Member trong CÙNG unit of work — polymorphic party từ application
    ///   (BusinessTenantId null → member là customer; ngược lại member là business tenant).
    /// - Không xóa application vật lý (status transition only).
    /// </summary>
    public class MembershipApplicationService(
        IVanAnDbContext dbContext,
        ILogger<MembershipApplicationService> logger) : IMembershipApplicationService
    {
        public async Task<Guid> CreateApplicationAsync(
            CreateMembershipApplicationRequest request,
            CancellationToken ct = default)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(request.FullName);
            ArgumentException.ThrowIfNullOrWhiteSpace(request.PhoneNumber);
            if (request.ApplicantCustomerId == Guid.Empty)
                throw new ArgumentException("ApplicantCustomerId cannot be empty.", nameof(request.ApplicantCustomerId));

            var htxTenantId = new TenantId(request.HtxTenantId);

            // Duplicate guard (Data Integrity Contract mục 5): chỉ 1 hồ sơ active review / (htx, applicant).
            // Applicant bị từ chối có thể nộp lại (hồ sơ mới) — anti-gaming SRS §37.
            var activeExists = await dbContext.MembershipApplications
                .IgnoreQueryFilters()
                .AnyAsync(a => a.TenantId == htxTenantId
                    && a.ApplicantCustomerId == request.ApplicantCustomerId
                    && a.IsActiveReview(), ct);
            if (activeExists)
                throw new InvalidOperationException(
                    $"Applicant {request.ApplicantCustomerId} already has an active application for HTX {request.HtxTenantId}.");

            var application = MembershipApplication.Create(
                htxTenantId,
                request.ApplicantCustomerId,
                request.MembershipType,
                request.FullName,
                request.PhoneNumber,
                request.Email,
                request.Region,
                request.ExpectedRole,
                request.BusinessTenantId is null || request.BusinessTenantId == Guid.Empty
                    ? null : new TenantId(request.BusinessTenantId.Value),
                request.CharterVersion,
                request.ConsentVersion);

            dbContext.MembershipApplications.Add(application);
            await dbContext.SaveChangesAsync(ct);

            logger.LogInformation(
                "Membership application {ApplicationId} created (Draft) for customer {CustomerId} at HTX {HtxId}",
                application.Id, request.ApplicantCustomerId, request.HtxTenantId);
            return application.Id;
        }

        public async Task SubmitAsync(Guid applicationId, CancellationToken ct = default)
        {
            var application = await LoadAsync(applicationId, ct);
            application.Submit();
            await dbContext.SaveChangesAsync(ct);
            logger.LogInformation("Membership application {ApplicationId} submitted", applicationId);
        }

        public async Task ResubmitAsync(Guid applicationId, CancellationToken ct = default)
        {
            var application = await LoadAsync(applicationId, ct);
            application.Resubmit();
            await dbContext.SaveChangesAsync(ct);
            logger.LogInformation("Membership application {ApplicationId} resubmitted (NeedInfo → Submitted)", applicationId);
        }

        public async Task RequestMoreInfoAsync(
            Guid applicationId,
            RequestMoreInfoRequest request,
            CancellationToken ct = default)
        {
            var application = await LoadAsync(applicationId, ct);
            application.RequestMoreInfo(request.ReviewedByUserId, request.Reason);
            await dbContext.SaveChangesAsync(ct);
            logger.LogInformation(
                "Membership application {ApplicationId} → NeedInfo by reviewer {ReviewerId}: {Reason}",
                applicationId, request.ReviewedByUserId, request.Reason);
        }

        public async Task<Guid> ApproveAsync(
            Guid applicationId,
            Guid reviewedByUserId,
            CancellationToken ct = default)
        {
            var application = await LoadAsync(applicationId, ct);

            // 1. HTX duyệt (domain guard: chỉ Submitted → Approved) + raise MembershipApplicationApprovedEvent
            application.Approve(reviewedByUserId);

            // 2. Tạo Member Active trong CÙNG unit of work (D2) — polymorphic party (Data Integrity Contract mục 1)
            var memberNumber = await GenerateMemberNumberAsync(application.TenantId, ct);
            var member = Member.CreateActive(
                application.TenantId,
                memberNumber,
                application.MembershipType,
                memberCustomerId: application.BusinessTenantId is null ? application.ApplicantCustomerId : null,
                memberTenantId: application.BusinessTenantId);

            dbContext.Members.Add(member);
            await dbContext.SaveChangesAsync(ct);

            logger.LogInformation(
                "Membership application {ApplicationId} APPROVED by {ReviewerId} — member {MemberId} ({MemberNumber}) activated",
                applicationId, reviewedByUserId, member.Id, memberNumber);
            return member.Id;
        }

        public async Task RejectAsync(
            Guid applicationId,
            RejectApplicationRequest request,
            CancellationToken ct = default)
        {
            var application = await LoadAsync(applicationId, ct);
            application.Reject(request.ReviewedByUserId, request.Reason);
            await dbContext.SaveChangesAsync(ct);
            logger.LogInformation(
                "Membership application {ApplicationId} REJECTED by {ReviewerId}: {Reason}",
                applicationId, request.ReviewedByUserId, request.Reason);
        }

        public async Task<IReadOnlyList<MembershipApplicationDto>> ListForHtxAsync(
            Guid htxTenantId,
            string? status = null,
            CancellationToken ct = default)
        {
            var htxTenantIdVo = new TenantId(htxTenantId);
            var query = dbContext.MembershipApplications
                .IgnoreQueryFilters()
                .AsNoTracking()
                .Where(a => a.TenantId == htxTenantIdVo);

            if (!string.IsNullOrWhiteSpace(status)
                && Enum.TryParse<ApplicationStatus>(status, ignoreCase: true, out var statusEnum))
            {
                query = query.Where(a => a.Status == statusEnum);
            }

            var applications = await query
                .OrderByDescending(a => a.CreatedAt)
                .ToListAsync(ct);

            return applications.Select(MapToDto).ToList();
        }

        public async Task<MembershipApplicationDto?> GetAsync(Guid applicationId, CancellationToken ct = default)
        {
            var application = await dbContext.MembershipApplications
                .IgnoreQueryFilters()
                .AsNoTracking()
                .FirstOrDefaultAsync(a => a.Id == applicationId, ct);
            return application is null ? null : MapToDto(application);
        }

        public async Task<IReadOnlyList<MembershipApplicationDto>> ListForApplicantAsync(
            Guid applicantCustomerId,
            CancellationToken ct = default)
        {
            // Cross-tenant read CÓ CHỦ ĐÍCH (Data Integrity Contract mục 7): applicant xem hồ sơ
            // của chính mình trên MỌI HTX — phải bỏ query filter, authz ở tầng API theo actor.
            var applications = await dbContext.MembershipApplications
                .IgnoreQueryFilters()
                .AsNoTracking()
                .Where(a => a.ApplicantCustomerId == applicantCustomerId)
                .OrderByDescending(a => a.CreatedAt)
                .ToListAsync(ct);

            return applications.Select(MapToDto).ToList();
        }

        // ── Helpers ──────────────────────────────────────────────────────────

        private async Task<MembershipApplication> LoadAsync(Guid applicationId, CancellationToken ct)
        {
            return await dbContext.MembershipApplications
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(a => a.Id == applicationId, ct)
                ?? throw new KeyNotFoundException($"Membership application {applicationId} not found.");
        }

        /// <summary>
        /// Member number format MEM-{htxSlug}-{seq:D6} (SRS §18). Unique index UX_Members_MemberNumber
        /// bảo vệ race — nếu trùng, DbUpdateException nổi lên và caller retry (MVP: không tự retry).
        /// </summary>
        private async Task<string> GenerateMemberNumberAsync(TenantId htxTenantId, CancellationToken ct)
        {
            var tenant = await dbContext.Tenants
                .IgnoreQueryFilters()
                .AsNoTracking()
                .FirstOrDefaultAsync(t => t.Id == htxTenantId, ct);
            var slug = tenant?.Settings?.Slug ?? "HTX";

            var count = await dbContext.Members
                .IgnoreQueryFilters()
                .CountAsync(m => m.TenantId == htxTenantId, ct);

            return $"MEM-{slug}-{count + 1:D6}";
        }

        private static MembershipApplicationDto MapToDto(MembershipApplication a) => new(
            Id: a.Id,
            HtxTenantId: a.TenantId.Value,
            ApplicantCustomerId: a.ApplicantCustomerId,
            BusinessTenantId: a.BusinessTenantId?.Value,
            MembershipType: a.MembershipType.ToString(),
            Status: a.Status.ToString(),
            FullName: a.FullName,
            PhoneNumber: a.PhoneNumber,
            Email: a.Email,
            Region: a.Region,
            ExpectedRole: a.ExpectedRole.ToString(),
            IdentityVerificationLevel: a.IdentityVerificationLevel.ToString(),
            ConsentVersion: a.ConsentVersion,
            CharterVersion: a.CharterVersion,
            CapitalFeeStatus: a.CapitalFeeStatus.ToString(),
            SubmittedAt: a.SubmittedAt,
            ReviewedByUserId: a.ReviewedByUserId,
            ReviewedAt: a.ReviewedAt,
            RejectionReason: a.RejectionReason,
            NeedInfoReason: a.NeedInfoReason,
            CreatedAt: a.CreatedAt);
    }
}
