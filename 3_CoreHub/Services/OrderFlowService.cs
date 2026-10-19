using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using VanAn.CoreHub.Infrastructure;
using VanAn.Shared.Domain;
using VanAn.Shared.Services;

namespace VanAn.CoreHub.Services
{
    /// <summary>
    /// Issue #195 (2026-10-19, approved Q1/Q4): resolve order-flow profiles từ PG
    /// (Gateway source of truth) + SystemAdmin CRUD.
    ///
    /// Resolution precedence (approved): (tenant + sourceType) → (tenant, null) →
    /// (system + sourceType) → (system, null) → built-in defaults. Category scope
    /// (CategoryKey) được hỗ trợ trong match khi caller truyền categoryKey — hiện
    /// OrderWorkflowService chưa truyền (deferred, cần product-category mapping).
    ///
    /// Multi-tenancy: OrderFlowProfile nằm dưới global tenant filter; service query bằng
    /// IgnoreQueryFilters + predicate TƯỜNG MINH (tenant hiện tại HOẶC system=Empty) —
    /// không bao giờ trả profile của tenant khác.
    ///
    /// Cache: IMemoryCache 60s theo (generation, tenant, source). Generation là số
    /// static tăng mỗi lần write → invalidate toàn bộ flow cache trong process này
    /// (profile thay đổi hiếm; staleness tối đa = TTL).
    /// </summary>
    public class OrderFlowService(
        IVanAnDbContext context,
        IMemoryCache cache,
        ILogger<OrderFlowService> logger) : IOrderFlowService, IOrderFlowAdminService
    {
        private readonly IVanAnDbContext _context = context;
        private readonly IMemoryCache _cache = cache;
        private readonly ILogger<OrderFlowService> _logger = logger;

        private static long _generation;
        private static readonly TimeSpan CacheTtl = TimeSpan.FromSeconds(60);

        // ==================== RESOLUTION (IOrderFlowService) ====================

        public async Task<OrderFlowResolution> ResolveFlowAsync(Guid tenantId, string? sourceType, CancellationToken ct = default)
        {
            string? src = Normalize(sourceType);
            string cacheKey = $"orderflow:{Interlocked.Read(ref _generation)}:{tenantId}:{src ?? "*"}";
            if (_cache.TryGetValue(cacheKey, out OrderFlowResolution? cached) && cached != null)
                return cached;

            OrderFlowResolution resolution = await ResolveFromDbAsync(tenantId, src, null, ct)
                ?? BuildBuiltin(src);

            _cache.Set(cacheKey, resolution, CacheTtl);
            return resolution;
        }

        public async Task<bool> IsTransitionAllowedAsync(Guid tenantId, string? sourceType, string fromStatus, string toStatus, CancellationToken ct = default)
        {
            OrderFlowResolution flow = await ResolveFlowAsync(tenantId, sourceType, ct);
            return flow.IsAllowed(NormalizeCode(fromStatus), NormalizeCode(toStatus));
        }

        public async Task<IReadOnlyList<string>> GetNextStatusesAsync(Guid tenantId, string? sourceType, string currentStatus, CancellationToken ct = default)
        {
            OrderFlowResolution flow = await ResolveFlowAsync(tenantId, sourceType, ct);
            return flow.NextOf(NormalizeCode(currentStatus));
        }

        private async Task<OrderFlowResolution?> ResolveFromDbAsync(Guid tenantId, string? sourceType, string? categoryKey, CancellationToken ct)
        {
            var tid = new TenantId(tenantId);
            var sysId = new TenantId(Guid.Empty);

            List<OrderFlowProfile> candidates;
            try
            {
                // Explicit predicate (tenant OR system) — global filter thay bằng IgnoreQueryFilters
                // vì system profiles (TenantId=Empty) phải visible cho mọi tenant.
                candidates = await _context.OrderFlowProfiles
                    .IgnoreQueryFilters()
                    .Include(p => p.Steps)
                    .Where(p => p.IsActive && !p.IsDeleted
                        && (p.TenantId == tid || p.TenantId == sysId))
                    .ToListAsync(ct);
            }
            catch (Exception ex)
            {
                // Fail-open → builtin: lỗi đọc config không được chặn workflow đơn.
                _logger.LogWarning(ex, "OrderFlowService: profile query failed for tenant {TenantId} — builtin fallback", tenantId);
                return null;
            }

            // In-memory match: SourceType/CategoryKey null = wildcard (khớp mọi giá trị).
            static int Score(OrderFlowProfile p, TenantId tid, string? src, string? cat)
            {
                int s = 0;
                if (p.Scope == OrderFlowScope.Category && p.CategoryKey != null) s += 8;
                if (p.TenantId == tid) s += 4;
                if (p.SourceType == src) s += 2;
                else s += 1; // wildcard source — thấp hơn exact match
                return s;
            }

            OrderFlowProfile? best = candidates
                .Where(p => (p.SourceType == null || p.SourceType == sourceType)
                         && (p.CategoryKey == null || p.CategoryKey == categoryKey))
                .OrderByDescending(p => Score(p, tid, sourceType, categoryKey))
                .ThenByDescending(p => p.CreatedAt)
                .FirstOrDefault();

            if (best == null) return null;

            return new OrderFlowResolution
            {
                ProfileId = best.Id,
                ProfileName = best.Name,
                Source = "database",
                Steps = best.Steps
                    .OrderBy(s => s.SortOrder)
                    .Select(s => new OrderFlowStepDto(s.Code, s.DisplayName, s.SortOrder, s.IsTerminal, s.AllowedNextCodes.ToList()))
                    .ToList()
            };
        }

        // ==================== BUILT-IN DEFAULTS ====================

        /// <summary>
        /// Built-in fallback — mirror của map hardcode trong OrderWorkflowService (kitchen ON).
        /// Booking (issue #195 0.2): chỉ cần "Thanh toán" + "Hoàn thành".
        /// </summary>
        public static OrderFlowResolution BuildBuiltin(string? sourceType) => new()
        {
            ProfileId = null,
            Source = "builtin",
            ProfileName = sourceType == "booking" ? "Đặt lịch (mặc định)" : "Luồng mặc định (bếp)",
            Steps = sourceType == "booking" ? BookingSteps() : DefaultSteps()
        };

        private static List<OrderFlowStepDto> DefaultSteps() =>
        [
            new("pending",    "Chờ xác nhận",  10, false, ["confirmed", "preparing", "completed", "cancelled"]),
            new("confirmed",  "Đã xác nhận",   20, false, ["preparing", "delivering", "completed", "cancelled"]),
            new("preparing",  "Đang chuẩn bị", 30, false, ["ready", "completed", "cancelled"]),
            new("ready",      "Sẵn sàng",      40, false, ["delivering", "delivered", "completed", "cancelled"]),
            new("delivering", "Đang giao",     50, false, ["delivered", "completed", "cancelled"]),
            new("delivered",  "Đã giao",       60, false, ["completed", "cancelled"]),
            new("completed",  "Hoàn thành",    70, true,  []),
            new("cancelled",  "Đã hủy",        80, true,  []),
        ];

        private static List<OrderFlowStepDto> BookingSteps() =>
        [
            new("pending",   "Chờ thanh toán", 10, false, ["confirmed", "cancelled"]),
            new("confirmed", "Đã thanh toán",  20, false, ["completed", "cancelled"]),
            new("completed", "Hoàn thành",     30, true,  []),
            new("cancelled", "Đã hủy",         40, true,  []),
        ];

        // ==================== ADMIN CRUD (IOrderFlowAdminService) ====================

        public async Task<List<OrderFlowProfileDto>> GetProfilesAsync(Guid? tenantId, CancellationToken ct = default)
        {
            IQueryable<OrderFlowProfile> q = _context.OrderFlowProfiles
                .IgnoreQueryFilters()
                .Include(p => p.Steps);

            if (tenantId.HasValue)
            {
                var tid = new TenantId(tenantId.Value);
                q = q.Where(p => p.TenantId == tid);
            }

            List<OrderFlowProfile> profiles = await q
                .OrderBy(p => p.TenantId).ThenBy(p => p.Name)
                .ToListAsync(ct);
            return profiles.Select(MapToDto).ToList();
        }

        public async Task<OrderFlowProfileDto> UpsertProfileAsync(
            Guid? tenantId, string? sourceType, string? categoryKey, string name,
            IReadOnlyList<OrderFlowStepInput> steps, string? actor, CancellationToken ct = default)
        {
            ValidateSteps(steps);

            string? src = Normalize(sourceType);
            string? cat = string.IsNullOrWhiteSpace(categoryKey) ? null : categoryKey.Trim();
            var scope = cat != null ? OrderFlowScope.Category
                : tenantId.HasValue && tenantId.Value != Guid.Empty ? OrderFlowScope.Tenant
                : OrderFlowScope.System;
            var tid = new TenantId(scope == OrderFlowScope.System ? Guid.Empty : tenantId!.Value);

            OrderFlowProfile result = null!;
            List<OrderFlowStep> newSteps = null!;
            await _context.ExecuteAtomicAsync(async () =>
            {
                OrderFlowProfile? existing = await _context.OrderFlowProfiles
                    .IgnoreQueryFilters()
                    .Include(p => p.Steps)
                    .FirstOrDefaultAsync(p => p.TenantId == tid
                        && p.SourceType == src
                        && p.CategoryKey == cat
                        && p.IsActive && !p.IsDeleted, ct);

                if (existing == null)
                {
                    existing = new OrderFlowProfile(tid, scope, name, src, cat);
                    _ = _context.OrderFlowProfiles.Add(existing);
                }
                else
                {
                    existing.UpdateDetails(name, src, cat);
                }

                // Replace steps — step là child rows của profile, replace toàn bộ.
                _context.OrderFlowSteps.RemoveRange(existing.Steps.ToList());
                newSteps = steps.Select(s => new OrderFlowStep(
                    tid, existing.Id, s.Code, s.DisplayName, s.SortOrder, s.IsTerminal, s.AllowedNextCodes)).ToList();
                // DbSet.Add bắt buộc (không add qua nav collection): OrderFlowStep.Id được set
                // client-side (Guid + ValueGeneratedOnAdd) → nav-add + DetectChanges đánh state
                // Modified → UPDATE vào row chưa tồn tại → DbUpdateConcurrencyException.
                _context.OrderFlowSteps.AddRange(newSteps);

                _ = await _context.SaveChangesAsync(ct);
                result = existing;
            });

            // Refresh nav cho MapToDto — old steps đã bị delete/detach sau SaveChanges.
            result.Steps.Clear();
            foreach (OrderFlowStep s in newSteps) result.Steps.Add(s);

            _ = Interlocked.Increment(ref _generation); // invalidate resolution cache
            _logger.LogInformation("OrderFlow profile upserted: {ProfileId} scope={Scope} tenant={TenantId} source={Source} steps={Steps} by={Actor}",
                result.Id, scope, tid.Value, src ?? "*", steps.Count, actor ?? "system");
            return MapToDto(result);
        }

        public async Task DeactivateProfileAsync(Guid profileId, string? actor, CancellationToken ct = default)
        {
            OrderFlowProfile? profile = await _context.OrderFlowProfiles
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(p => p.Id == profileId && !p.IsDeleted, ct)
                ?? throw new InvalidOperationException($"OrderFlowProfile {profileId} not found.");

            profile.Deactivate();
            _ = await _context.SaveChangesAsync(ct);
            _ = Interlocked.Increment(ref _generation);
            _logger.LogInformation("OrderFlow profile deactivated: {ProfileId} by={Actor}", profileId, actor ?? "system");
        }

        public async Task EnsureSystemDefaultsAsync(CancellationToken ct = default)
        {
            var sysId = new TenantId(Guid.Empty);

            List<string?> existingSources = await _context.OrderFlowProfiles
                .IgnoreQueryFilters()
                .Where(p => p.TenantId == sysId && p.Scope == OrderFlowScope.System && p.IsActive && !p.IsDeleted)
                .Select(p => p.SourceType)
                .ToListAsync(ct);

            var added = false;
            if (!existingSources.Contains(null))
            {
                var profile = new OrderFlowProfile(sysId, OrderFlowScope.System, "Luồng mặc định (bếp)");
                foreach (OrderFlowStepDto s in DefaultSteps())
                    profile.Steps.Add(new OrderFlowStep(sysId, profile.Id, s.Code, s.DisplayName, s.SortOrder, s.IsTerminal, s.AllowedNextCodes));
                _ = _context.OrderFlowProfiles.Add(profile);
                added = true;
            }
            if (!existingSources.Contains("booking"))
            {
                var profile = new OrderFlowProfile(sysId, OrderFlowScope.System, "Đặt lịch → Thanh toán → Hoàn thành", "booking");
                foreach (OrderFlowStepDto s in BookingSteps())
                    profile.Steps.Add(new OrderFlowStep(sysId, profile.Id, s.Code, s.DisplayName, s.SortOrder, s.IsTerminal, s.AllowedNextCodes));
                _ = _context.OrderFlowProfiles.Add(profile);
                added = true;
            }

            if (added)
            {
                _ = await _context.SaveChangesAsync(ct);
                _ = Interlocked.Increment(ref _generation);
                _logger.LogInformation("OrderFlow system default profiles seeded");
            }
        }

        // ==================== HELPERS ====================

        private static void ValidateSteps(IReadOnlyList<OrderFlowStepInput> steps)
        {
            if (steps.Count == 0)
                throw new ArgumentException("Flow profile requires at least one step.", nameof(steps));

            var codes = steps.Select(s => NormalizeCode(s.Code)).ToList();
            if (codes.Count != codes.Distinct().Count())
                throw new ArgumentException("Duplicate step codes.", nameof(steps));
            if (!codes.Contains("pending"))
                throw new ArgumentException("Flow must contain the 'pending' entry step.", nameof(steps));
            if (!steps.Any(s => s.IsTerminal))
                throw new ArgumentException("Flow requires at least one terminal step (completed/cancelled).", nameof(steps));

            foreach (OrderFlowStepInput s in steps)
            {
                foreach (string next in s.AllowedNextCodes)
                {
                    if (!codes.Contains(NormalizeCode(next)))
                        throw new ArgumentException($"Step '{s.Code}' allows unknown next status '{next}'.", nameof(steps));
                }
            }
        }

        private static OrderFlowProfileDto MapToDto(OrderFlowProfile p) => new()
        {
            Id = p.Id,
            TenantId = p.TenantId.Value,
            Scope = p.Scope.ToString(),
            SourceType = p.SourceType,
            CategoryKey = p.CategoryKey,
            Name = p.Name,
            IsActive = p.IsActive,
            Steps = p.Steps.OrderBy(s => s.SortOrder)
                .Select(s => new OrderFlowStepDto(s.Code, s.DisplayName, s.SortOrder, s.IsTerminal, s.AllowedNextCodes.ToList()))
                .ToList()
        };

        private static string? Normalize(string? v) =>
            string.IsNullOrWhiteSpace(v) ? null : v.Trim().ToLowerInvariant();

        private static string NormalizeCode(string v) => v.Trim().ToLowerInvariant();
    }
}
