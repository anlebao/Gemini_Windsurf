using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using VanAn.CoreHub.Infrastructure;
using VanAn.Shared.Domain;
using VanAn.Shared.Domain.Aggregates.MembershipAggregate;
using VanAn.Shared.Domain.Aggregates.TenantAggregate;

namespace VanAn.CoreHub.Services.Membership
{
    /// <summary>
    /// Membership Infrastructure (2026-09-29): Sổ đăng ký thành viên điện tử (SRS §17).
    ///
    /// Data Integrity Contract (master_plan mục 6):
    /// - Cấm xóa member vật lý — lifecycle bằng status transition (SRS §7).
    /// - GetStatusAsync (public verify) KHÔNG PII — SRS §22.
    /// - ListForCustomerAsync = cross-tenant read CÓ CHỦ ĐÍCH (member là khách của nhiều HTX) —
    ///   authz ở tầng API theo actor đăng nhập.
    /// </summary>
    public class MemberRegistryService(
        IVanAnDbContext dbContext,
        ILogger<MemberRegistryService> logger) : IMemberRegistryService
    {
        public async Task<IReadOnlyList<MemberDto>> ListForHtxAsync(
            Guid htxTenantId,
            string? status = null,
            CancellationToken ct = default)
        {
            var htxTenantIdVo = new TenantId(htxTenantId);
            var query = dbContext.Members
                .IgnoreQueryFilters()
                .AsNoTracking()
                .Where(m => m.TenantId == htxTenantIdVo);

            if (!string.IsNullOrWhiteSpace(status)
                && Enum.TryParse<MemberStatus>(status, ignoreCase: true, out var statusEnum))
            {
                query = query.Where(m => m.Status == statusEnum);
            }

            var members = await query
                .OrderByDescending(m => m.JoinedAt)
                .ToListAsync(ct);

            return members.Select(MapToDto).ToList();
        }

        public async Task<MemberDto?> GetAsync(Guid memberId, CancellationToken ct = default)
        {
            var member = await dbContext.Members
                .IgnoreQueryFilters()
                .AsNoTracking()
                .FirstOrDefaultAsync(m => m.Id == memberId, ct);
            return member is null ? null : MapToDto(member);
        }

        public async Task<MemberStatusDto?> GetStatusAsync(string memberNumber, CancellationToken ct = default)
        {
            // Public verify (SRS §22): tra theo MemberNumber (token từ QR). Cross-tenant có chủ đích —
            // bất kỳ ai quét QR đều được xem status, KHÔNG được xem PII.
            var member = await dbContext.Members
                .IgnoreQueryFilters()
                .AsNoTracking()
                .FirstOrDefaultAsync(m => m.MemberNumber == memberNumber, ct);
            if (member is null) return null;

            var tenant = await dbContext.Tenants
                .IgnoreQueryFilters()
                .AsNoTracking()
                .FirstOrDefaultAsync(t => t.Id == member.TenantId, ct);

            return new MemberStatusDto(
                IsActive: member.IsActive(),
                Status: member.Status.ToString(),
                MemberNumber: member.MemberNumber,
                HtxName: tenant?.Name,
                JoinedAt: member.JoinedAt);
        }

        public async Task SuspendAsync(Guid memberId, string reason, CancellationToken ct = default)
        {
            var member = await LoadAsync(memberId, ct);
            member.Suspend(reason);
            await dbContext.SaveChangesAsync(ct);
            logger.LogInformation("Member {MemberId} suspended: {Reason}", memberId, reason);
        }

        public async Task ReactivateAsync(Guid memberId, CancellationToken ct = default)
        {
            var member = await LoadAsync(memberId, ct);
            member.Reactivate();
            await dbContext.SaveChangesAsync(ct);
            logger.LogInformation("Member {MemberId} reactivated", memberId);
        }

        public async Task ResignAsync(Guid memberId, string reason, CancellationToken ct = default)
        {
            var member = await LoadAsync(memberId, ct);
            member.Resign(reason);
            await dbContext.SaveChangesAsync(ct);
            logger.LogInformation("Member {MemberId} resigned: {Reason}", memberId, reason);
        }

        public async Task TerminateAsync(Guid memberId, string reason, CancellationToken ct = default)
        {
            var member = await LoadAsync(memberId, ct);
            member.Terminate(reason);
            await dbContext.SaveChangesAsync(ct);
            logger.LogInformation("Member {MemberId} terminated: {Reason}", memberId, reason);
        }

        /// <inheritdoc />
        /// <summary>Luồng 2 (2026-10-02, user directive): SystemAdmin add tenant làm thành viên HTX.</summary>
        public async Task<Guid> AddMemberAsync(
            Guid htxTenantId,
            Guid memberTenantId,
            MembershipType membershipType,
            decimal? capitalContributionAmount,
            Guid reviewedByUserId,
            CancellationToken ct = default)
        {
            if (memberTenantId == Guid.Empty)
                throw new ArgumentException("Member tenant id cannot be empty.", nameof(memberTenantId));
            if (reviewedByUserId == Guid.Empty)
                throw new ArgumentException("ReviewedByUserId cannot be empty.", nameof(reviewedByUserId));

            // Guard D6: góp vốn theo loại thành viên (Luật HTX 2023 + user directive).
            if (membershipType == MembershipType.LinkedNonCapitalMember && capitalContributionAmount is not null)
                throw new ArgumentException("Linked non-capital member cannot declare a capital contribution.", nameof(capitalContributionAmount));
            if (membershipType != MembershipType.LinkedNonCapitalMember
                && (capitalContributionAmount is null || capitalContributionAmount.Value <= 0))
                throw new ArgumentException("Official and linked-capital members require a capital contribution amount (> 0).", nameof(capitalContributionAmount));

            var htxTenantIdVo = new TenantId(htxTenantId);

            // Guard: HTX phải đã kích hoạt Membership (HtxProfile tồn tại).
            var htxExists = await dbContext.HtxProfiles
                .IgnoreQueryFilters()
                .AsNoTracking()
                .AnyAsync(p => p.TenantId == htxTenantIdVo, ct);
            if (!htxExists)
                throw new InvalidOperationException($"Tenant {htxTenantId} is not an HTX (no HtxProfile).");

            // Guard D7: member tenant phải đã verify (Active).
            var memberTenant = await dbContext.Tenants
                .IgnoreQueryFilters()
                .AsNoTracking()
                .FirstOrDefaultAsync(t => t.Id == new TenantId(memberTenantId), ct);
            if (memberTenant is null || memberTenant.Status != TenantStatus.Active)
                throw new InvalidOperationException(
                    $"Tenant {memberTenantId} must be verified (Active) before joining an HTX as member.");
            if (memberTenantId == htxTenantId)
                throw new InvalidOperationException("A tenant cannot be a member of its own HTX (self-membership).");

            // Duplicate guard: (htx, memberTenant) — unique index UX_Members_HtxTenant.
            var exists = await dbContext.Members
                .IgnoreQueryFilters()
                .AsNoTracking()
                .AnyAsync(m => m.TenantId == htxTenantIdVo && m.MemberTenantId == new TenantId(memberTenantId), ct);
            if (exists)
                throw new InvalidOperationException($"Tenant {memberTenantId} is already a member of HTX {htxTenantId}.");

            var memberNumber = await GenerateMemberNumberAsync(htxTenantIdVo, ct);
            var member = Member.CreateActive(
                htxTenantIdVo,
                memberNumber,
                membershipType,
                memberCustomerId: null,
                memberTenantId: new TenantId(memberTenantId),
                capitalContributionAmount: capitalContributionAmount);

            dbContext.Members.Add(member);
            await dbContext.SaveChangesAsync(ct);

            logger.LogInformation(
                "Member {MemberId} ({MemberNumber}) added to HTX {HtxId} by SystemAdmin {AdminId} — tenant {MemberTenantId}, type {Type}, capital {Capital}",
                member.Id, memberNumber, htxTenantId, reviewedByUserId, memberTenantId, membershipType, capitalContributionAmount);
            return member.Id;
        }

        public async Task<IReadOnlyList<MemberDto>> ListForCustomerAsync(Guid customerId, CancellationToken ct = default)
        {
            // "HTX của tôi" — member xem các HTX mình đang tham gia (multi-HTX — SRS §19).
            // Cross-tenant read CÓ CHỦ ĐÍCH (mục 7) — authz theo actor ở API, KHÔNG theo tenant context.
            var members = await dbContext.Members
                .IgnoreQueryFilters()
                .AsNoTracking()
                .Where(m => m.MemberCustomerId == customerId)
                .OrderByDescending(m => m.JoinedAt)
                .ToListAsync(ct);

            return members.Select(MapToDto).ToList();
        }

        // ── Helpers ──────────────────────────────────────────────────────────

        /// <summary>Member number MEM-{htxSlug}-{seq:D6} (SRS §18) — unique index bảo vệ race.</summary>
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

        private async Task<Member> LoadAsync(Guid memberId, CancellationToken ct)
        {
            return await dbContext.Members
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(m => m.Id == memberId, ct)
                ?? throw new KeyNotFoundException($"Member {memberId} not found.");
        }

        private static MemberDto MapToDto(Member m) => new(
            Id: m.Id,
            HtxTenantId: m.TenantId.Value,
            MemberCustomerId: m.MemberCustomerId,
            MemberTenantId: m.MemberTenantId?.Value,
            MembershipType: m.MembershipType.ToString(),
            MemberNumber: m.MemberNumber,
            Status: m.Status.ToString(),
            JoinedAt: m.JoinedAt,
            ApprovedAt: m.ApprovedAt,
            EffectiveAt: m.EffectiveAt,
            TerminatedAt: m.TerminatedAt,
            StatusReason: m.StatusReason,
            CapitalContributionAmount: m.CapitalContributionAmount);
    }
}
