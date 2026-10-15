using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using VanAn.CoreHub.Infrastructure;
using VanAn.CoreHub.Infrastructure.Messaging;
using VanAn.Shared.Domain;
using VanAn.Shared.Domain.Aggregates.MembershipAggregate;

namespace VanAn.CoreHub.Services.Membership
{
    /// <summary>
    /// Membership Infrastructure (2026-09-29): Hồ sơ HTX (A1 — HTX = Tenant + HtxProfile).
    /// 2026-10-15: publish "vanan.cloud.tenant.type.changed" khi MarkAsHtx / RevertAsync
    /// → ShopERP TenantSyncSubscriber cập nhật SQLite mirror (fix báo cáo HTX đọc mirror cũ).
    /// </summary>
    public class HtxProfileService(
        IVanAnDbContext dbContext,
        ILogger<HtxProfileService> logger,
        INatsEventPublisher? natsEventPublisher = null) : IHtxProfileService
    {
        private const string TenantTypeChangedSubject = "vanan.cloud.tenant.type.changed";
        public async Task<HtxProfileDto?> GetAsync(Guid htxTenantId, CancellationToken ct = default)
        {
            var profile = await dbContext.HtxProfiles
                .IgnoreQueryFilters()
                .AsNoTracking()
                .FirstOrDefaultAsync(p => p.TenantId == new TenantId(htxTenantId), ct);
            return profile is null ? null : MapToDto(profile);
        }

        public async Task<IReadOnlyList<HtxProfileSummaryDto>> ListAsync(CancellationToken ct = default)
        {
            var profiles = await dbContext.HtxProfiles
                .IgnoreQueryFilters()
                .AsNoTracking()
                .ToListAsync(ct);

            // Tenant name cho dropdown review (SystemAdmin) — global lookup theo Id.
            // Pattern #8: dùng VO list cho Contains (t.Id.Value không translate trong Where).
            var tenantIdVos = profiles.Select(p => p.TenantId).Distinct().ToList();
            var tenantNames = await dbContext.Tenants
                .IgnoreQueryFilters()
                .AsNoTracking()
                .Where(t => tenantIdVos.Contains(t.Id))
                .Select(t => new { t.Id.Value, t.Name })
                .ToDictionaryAsync(t => t.Value, t => t.Name, ct);

            return profiles
                .OrderBy(p => tenantNames.GetValueOrDefault(p.TenantId.Value) ?? string.Empty)
                .Select(p => new HtxProfileSummaryDto(
                    HtxTenantId: p.TenantId.Value,
                    TenantName: tenantNames.GetValueOrDefault(p.TenantId.Value) ?? "HTX",
                    CharterVersion: p.CharterVersion))
                .ToList();
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

            // TT 71/2024 (2026-10-01): HtxProfile là marker chính thức "tenant là HTX" (A1)
            // → đồng thời phân loại tenant: Type=HTX + AccountingStandard=TT71_2024.
            // IgnoreQueryFilters: lookup theo Id tường minh, tenant là dữ liệu global.
            var tenant = await dbContext.Tenants
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(t => t.Id == new TenantId(htxTenantId), ct);
            if (tenant is not null)
            {
                tenant.MarkAsHtx();
            }
            else
            {
                logger.LogWarning("HtxProfile created for tenant {HtxId} but tenant row NOT found — Type không được set", htxTenantId);
            }

            await dbContext.SaveChangesAsync(ct);

            logger.LogInformation("HtxProfile created for tenant {HtxId} (charter {CharterVersion}, terms {TermsVersion}) — marked HTX/TT71",
                htxTenantId, charterVersion, termsVersion);
            await PublishTenantTypeChangedAsync(htxTenantId, tenant?.Type, tenant?.AccountingStandard);
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

        /// <inheritdoc />
        public async Task RevertAsync(
            Guid htxTenantId,
            TenantType targetType,
            AccountingStandard? targetStandard,
            CancellationToken ct = default)
        {
            var htxTenantIdVo = new TenantId(htxTenantId);
            var profile = await dbContext.HtxProfiles
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(p => p.TenantId == htxTenantIdVo, ct)
                ?? throw new KeyNotFoundException($"HtxProfile for tenant {htxTenantId} not found. Tenant không phải HTX.");

            // Q3 (2026-10-15, user directive): CHẶN revert khi còn thành viên — bảo toàn dữ liệu
            // (phải tự xóa/terminate thành viên trước).
            bool hasMembers = await dbContext.Members
                .IgnoreQueryFilters()
                .AnyAsync(m => m.TenantId == htxTenantIdVo, ct);
            if (hasMembers)
                throw new InvalidOperationException(
                    "Không thể chuyển ngược HTX → Công ty/HKD khi còn thành viên. Hãy xóa/terminate toàn bộ thành viên trước.");

            var tenant = await dbContext.Tenants
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(t => t.Id == htxTenantIdVo, ct)
                ?? throw new InvalidOperationException($"Tenant {htxTenantId} not found in PostgreSQL.");

            tenant.RevertTenantType(targetType, targetStandard);
            dbContext.HtxProfiles.Remove(profile);
            await dbContext.SaveChangesAsync(ct);

            logger.LogInformation("HtxProfileService: tenant {HtxId} reverted HTX → {TargetType} (standard {Standard}) — HtxProfile deleted",
                htxTenantId, targetType, targetStandard?.ToString() ?? "null");
            await PublishTenantTypeChangedAsync(htxTenantId, tenant.Type, tenant.AccountingStandard);
        }

        // ── Helpers ──────────────────────────────────────────────────────────

        /// <summary>
        /// Publish "vanan.cloud.tenant.type.changed" → ShopERP TenantSyncSubscriber cập nhật
        /// SQLite mirror Type/AccountingStandard. Fire-and-forget (giống LoyaltyConfigController):
        /// miss event chỉ trễ mirror — P0 backfill + subscriber idempotent đảm bảo nhất quán.
        /// </summary>
        private async Task PublishTenantTypeChangedAsync(Guid tenantId, TenantType? type, AccountingStandard? standard)
        {
            if (natsEventPublisher is null) return;
            try
            {
                var payload = new
                {
                    TenantId = tenantId,
                    Type = type.HasValue ? (int)type.Value : (int?)null,
                    AccountingStandard = standard.HasValue ? (int)standard.Value : (int?)null,
                    UpdatedAt = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss.fffffffZ")
                };
                byte[] bytes = System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(payload);
                await natsEventPublisher.PublishAsync(TenantTypeChangedSubject, bytes);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "HtxProfileService: failed to publish tenant type change for {TenantId} — SQLite mirror may be stale", tenantId);
            }
        }

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
