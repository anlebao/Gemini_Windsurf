using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using VanAn.CoreHub.Infrastructure;
using VanAn.Shared.Domain;
using VanAn.Shared.Domain.Aggregates.MembershipAggregate;

namespace VanAn.CoreHub.Services.Membership
{
    /// <summary>
    /// Membership Infrastructure (2026-09-29): Hồ sơ HTX (A1 — HTX = Tenant + HtxProfile).
    /// </summary>
    public class HtxProfileService(
        IVanAnDbContext dbContext,
        ILogger<HtxProfileService> logger) : IHtxProfileService
    {
        public async Task<HtxProfileDto?> GetAsync(Guid htxTenantId, CancellationToken ct = default)
        {
            var profile = await dbContext.HtxProfiles
                .IgnoreQueryFilters()
                .AsNoTracking()
                .FirstOrDefaultAsync(p => p.TenantId == new TenantId(htxTenantId), ct);
            return profile is null ? null : MapToDto(profile);
        }

        public async Task<HtxProfileDto> GetOrCreateAsync(
            Guid htxTenantId,
            string charterVersion,
            string termsVersion,
            CancellationToken ct = default)
        {
            var htxTenantIdVo = new TenantId(htxTenantId);
            var existing = await dbContext.HtxProfiles
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(p => p.TenantId == htxTenantIdVo, ct);
            if (existing is not null)
                return MapToDto(existing);

            var profile = HtxProfile.Create(htxTenantIdVo, charterVersion, termsVersion);
            dbContext.HtxProfiles.Add(profile);
            await dbContext.SaveChangesAsync(ct);

            logger.LogInformation("HtxProfile created for tenant {HtxId} (charter {CharterVersion}, terms {TermsVersion})",
                htxTenantId, charterVersion, termsVersion);
            return MapToDto(profile);
        }

        public async Task UpdateCharterAsync(
            Guid htxTenantId,
            string charterVersion,
            string? charterUrl,
            CancellationToken ct = default)
        {
            var profile = await LoadAsync(htxTenantId, ct);
            profile.UpdateCharter(charterVersion, charterUrl);
            await dbContext.SaveChangesAsync(ct);
            logger.LogInformation("HtxProfile {HtxId} charter updated → {CharterVersion}", htxTenantId, charterVersion);
        }

        public async Task UpdateTermsAsync(
            Guid htxTenantId,
            string termsVersion,
            CancellationToken ct = default)
        {
            var profile = await LoadAsync(htxTenantId, ct);
            profile.UpdateTerms(termsVersion);
            await dbContext.SaveChangesAsync(ct);
            logger.LogInformation("HtxProfile {HtxId} terms updated → {TermsVersion}", htxTenantId, termsVersion);
        }

        // ── Helpers ──────────────────────────────────────────────────────────

        private async Task<HtxProfile> LoadAsync(Guid htxTenantId, CancellationToken ct)
        {
            return await dbContext.HtxProfiles
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(p => p.TenantId == new TenantId(htxTenantId), ct)
                ?? throw new KeyNotFoundException($"HtxProfile for tenant {htxTenantId} not found. Tenant chưa đăng ký Membership Infrastructure.");
        }

        private static HtxProfileDto MapToDto(HtxProfile p) => new(
            Id: p.Id,
            HtxTenantId: p.TenantId.Value,
            CharterVersion: p.CharterVersion,
            TermsVersion: p.TermsVersion,
            CharterUrl: p.CharterUrl);
    }
}
