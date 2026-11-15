using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using VanAn.CoreHub.Infrastructure;
using VanAn.Gateway.Services;
using VanAn.Shared.Domain;
using VanAn.Shared.Domain.Aggregates.KhachLinkAggregate;
using VanAn.Shared.Domain.Aggregates.MembershipAggregate;
using VanAn.Shared.Domain.Aggregates.TenantAggregate;
using Tenant = VanAn.Shared.Domain.Aggregates.TenantAggregate.Tenant;

namespace VanAn.Gateway.Controllers
{
    /// <summary>
    /// Entry Points Retention W2-1 (2026-11-14): Public cooperative (HTX) directory endpoints.
    /// Reuses Membership Infrastructure as source of truth (user-approved direction change):
    ///   - HTX = Tenant có HtxProfile (Status Active)
    ///   - Member shops = Member rows (MemberTenantId != null, Status = Active) của HTX đó
    /// Anonymous read-only surface cho hoptacxa.timlathay.com portal + timlathay.com.
    /// Admin CRUD tái sử dụng /api/admin/membership/* (MembershipAdminController) +
    /// /admin/membership/members (MembershipMembers.razor) — KHÔNG tạo entity song song.
    /// </summary>
    [ApiController]
    [Route("api/cooperatives")]
    [EnableRateLimiting("cooperatives-public")]
    public class CooperativesController(
        IVanAnDbContext dbContext,
        ITenantSearchService tenantSearch,
        ILogger<CooperativesController> logger) : ControllerBase
    {
        private readonly IVanAnDbContext _dbContext = dbContext;
        private readonly ITenantSearchService _tenantSearch = tenantSearch;
        private readonly ILogger<CooperativesController> _logger = logger;

        /// <summary>List HTX đang active + member-shop count (portal landing).</summary>
        [HttpGet]
        [AllowAnonymous]
        public async Task<ActionResult<List<CooperativeDto>>> List(CancellationToken ct = default)
            => Ok(await GetAllAsync(ct));

        /// <summary>Member shops của 1 HTX (chỉ tenant-member Active, tenant Status Active).</summary>
        [HttpGet("{htxTenantId:guid}/members")]
        [AllowAnonymous]
        public async Task<ActionResult<List<CooperativeMemberDto>>> Members(Guid htxTenantId, CancellationToken ct = default)
        {
            var htxVo = new TenantId(htxTenantId);
            var isHtx = await _dbContext.HtxProfiles
                .IgnoreQueryFilters().AsNoTracking()
                .AnyAsync(p => p.TenantId == htxVo, ct);
            if (!isHtx)
                return NotFound();

            var members = await _dbContext.Members
                .IgnoreQueryFilters().AsNoTracking()
                .Where(m => m.TenantId == htxVo
                    && m.MemberTenantId != null
                    && m.Status == MemberStatus.Active)
                .ToListAsync(ct);
            if (members.Count == 0)
                return Ok(new List<CooperativeMemberDto>());

            // TenantId là record (ref type): MemberTenantId! .Value → Guid; MemberTenantId! → TenantId VO.
            var memberTenantIdVos = members
                .Select(m => m.MemberTenantId!)
                .Distinct()
                .ToList();
            var memberTenantIdGuids = memberTenantIdVos.Select(x => x.Value).ToList();

            var tenants = await _dbContext.Tenants
                .IgnoreQueryFilters().AsNoTracking()
                .Where(t => memberTenantIdVos.Contains(t.Id) && t.Status == TenantStatus.Active)
                .Select(t => new { t.Id.Value, t.Name, Slug = t.Settings.Slug, LogoUrl = t.Settings.LogoUrl })
                .ToDictionaryAsync(t => t.Value, ct);

            var domainMap = await _tenantSearch.GetKhachLinkDomainMapAsync(memberTenantIdGuids, ct);
            // W3-1 (FR-V8): flagship toggles per member tenant — batch 1 query.
            var featureFlags = await _tenantSearch.GetFeatureFlagMapAsync(memberTenantIdGuids, ct);

            return Ok(members
                .Where(m => tenants.ContainsKey(m.MemberTenantId!.Value))
                .Select(m =>
                {
                    var t = tenants[m.MemberTenantId!.Value];
                    featureFlags.TryGetValue(m.MemberTenantId!.Value, out var flags);
                    return new CooperativeMemberDto(
                        MemberId: m.Id,
                        MemberNumber: m.MemberNumber,
                        TenantId: m.MemberTenantId!.Value,
                        Name: t.Name,
                        Slug: t.Slug,
                        LogoUrl: t.LogoUrl,
                        KhachLinkDomain: domainMap.GetValueOrDefault(m.MemberTenantId!.Value),
                        JoinedAt: m.JoinedAt,
                        AiChatEnabled: flags.AiChatEnabled,
                        VoiceCallEnabled: flags.VoiceCallEnabled);
                })
                .OrderBy(m => m.Name)
                .ToList());
        }

        /// <summary>
        /// Resolve portal domain → cooperative scope.
        /// Instance có OwnerTenantId là HTX → trả đúng 1 HTX đó.
        /// Portal-level instance (OwnerTenantId=null, vd hoptacxa) hoặc domain lạ → trả toàn bộ HTX active.
        /// </summary>
        [HttpGet("by-domain/{domain}")]
        [AllowAnonymous]
        public async Task<ActionResult<List<CooperativeDto>>> ByDomain(string domain, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(domain))
                return BadRequest();

            var normalized = domain.Trim().ToLowerInvariant();
            var instance = await _dbContext.KhachLinkInstances
                .IgnoreQueryFilters().AsNoTracking()
                .FirstOrDefaultAsync(i => i.CustomDomain == normalized, ct);
            if (instance is null)
                return NotFound();

            var allCoops = await GetAllAsync(ct);
            if (instance.OwnerTenantId is { } ownerId
                && allCoops.Any(c => c.TenantId == ownerId))
            {
                return Ok(allCoops.Where(c => c.TenantId == ownerId).ToList());
            }

            return Ok(allCoops);
        }

        private async Task<List<CooperativeDto>> GetAllAsync(CancellationToken ct)
        {
            var htxRows = await LoadActiveHtxAsync(ct);
            if (htxRows.Count == 0)
                return new List<CooperativeDto>();

            var htxIdVos = htxRows.Select(h => h.HtxTenantIdVo).ToList();
            var memberCounts = await GetMemberShopCountsAsync(htxIdVos, ct);
            var domainMap = await _tenantSearch.GetKhachLinkDomainMapAsync(htxRows.Select(h => h.TenantId).ToList(), ct);
            // W3-2b: HTX tenant tự nó cũng là storefront (vd Vạn An Trading → commienphi) —
            // cần flagship flags để portal render Hỏi AI/Gọi trên self card.
            var featureFlags = await _tenantSearch.GetFeatureFlagMapAsync(htxRows.Select(h => h.TenantId).ToList(), ct);

            return htxRows.Select(h =>
            {
                featureFlags.TryGetValue(h.TenantId, out var flags);
                return new CooperativeDto(
                    TenantId: h.TenantId,
                    Name: h.Name,
                    Slug: h.Slug,
                    LogoUrl: h.LogoUrl,
                    MemberCount: memberCounts.GetValueOrDefault(h.TenantId),
                    KhachLinkDomain: domainMap.GetValueOrDefault(h.TenantId),
                    AiChatEnabled: flags.AiChatEnabled,
                    VoiceCallEnabled: flags.VoiceCallEnabled);
            }).ToList();
        }

        // ── Helpers ──────────────────────────────────────────────────────────

        private sealed record HtxRow(Guid TenantId, TenantId HtxTenantIdVo, string Name, string? Slug, string? LogoUrl);

        /// <summary>HTX active = HtxProfile join Tenant Status=Active (tenant suspended → ẩn khỏi portal).</summary>
        private async Task<List<HtxRow>> LoadActiveHtxAsync(CancellationToken ct)
        {
            var profiles = await _dbContext.HtxProfiles
                .IgnoreQueryFilters().AsNoTracking()
                .ToListAsync(ct);
            if (profiles.Count == 0)
                return new List<HtxRow>();

            var idVos = profiles.Select(p => p.TenantId).ToList();
            var tenants = await _dbContext.Tenants
                .IgnoreQueryFilters().AsNoTracking()
                .Where(t => idVos.Contains(t.Id) && t.Status == TenantStatus.Active)
                .Select(t => new { t.Id.Value, t.Name, Slug = t.Settings.Slug, LogoUrl = t.Settings.LogoUrl })
                .ToDictionaryAsync(t => t.Value, ct);

            return profiles
                .Where(p => tenants.ContainsKey(p.TenantId.Value))
                .Select(p =>
                {
                    var t = tenants[p.TenantId.Value];
                    return new HtxRow(p.TenantId.Value, p.TenantId, t.Name, t.Slug, t.LogoUrl);
                })
                .OrderBy(h => h.Name)
                .ToList();
        }

        /// <summary>Count member-shop (MemberTenantId != null) Active per HTX.</summary>
        private async Task<Dictionary<Guid, int>> GetMemberShopCountsAsync(List<TenantId> htxIdVos, CancellationToken ct)
        {
            var rows = await _dbContext.Members
                .IgnoreQueryFilters().AsNoTracking()
                .Where(m => htxIdVos.Contains(m.TenantId)
                    && m.MemberTenantId != null
                    && m.Status == MemberStatus.Active)
                .Select(m => m.TenantId.Value)
                .ToListAsync(ct);
            return rows.GroupBy(x => x).ToDictionary(g => g.Key, g => g.Count());
        }

    }

    // ── DTOs ──────────────────────────────────────────────────────────────

    /// <summary>HTX card trên portal — tenant-backed (Membership Infrastructure).</summary>
    public sealed record CooperativeDto(
        Guid TenantId,
        string Name,
        string? Slug,
        string? LogoUrl,
        int MemberCount,
        string? KhachLinkDomain,
        // W3-2b: flagship toggles của CHÍNH tenant HTX — portal render self card
        // (storefront/Hỏi AI/Gọi) khi HTX tenant có KhachLink storefront.
        bool AiChatEnabled = false,
        bool VoiceCallEnabled = false);

    /// <summary>Member shop card — tenant-member Active của HTX.</summary>
    public sealed record CooperativeMemberDto(
        Guid MemberId,
        string MemberNumber,
        Guid TenantId,
        string Name,
        string? Slug,
        string? LogoUrl,
        string? KhachLinkDomain,
        DateTime JoinedAt,
        // W3-1 (FR-V8): flagship toggles — portal render "Hỏi AI"/"Gọi" per member.
        bool AiChatEnabled = false,
        bool VoiceCallEnabled = false);
}
