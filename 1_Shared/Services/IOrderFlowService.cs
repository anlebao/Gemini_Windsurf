using VanAn.Shared.Domain;

namespace VanAn.Shared.Services
{
    /// <summary>
    /// Issue #195 (2026-10-19, approved Q1/Q4): một step trong luồng trạng thái đơn —
    /// resolved view cho consumer (workflow validation + timeline UI + admin UI).
    /// </summary>
    public sealed record OrderFlowStepDto(
        string Code,
        string DisplayName,
        int SortOrder,
        bool IsTerminal,
        List<string> AllowedNextCodes);

    /// <summary>
    /// Issue #195: flow đã resolve cho (tenant, sourceType). Source = "database" (profile
    /// từ PG) | "builtin" (fallback hardcode — chưa có profile nào match).
    /// </summary>
    public sealed class OrderFlowResolution
    {
        public Guid? ProfileId { get; init; }
        public string ProfileName { get; init; } = string.Empty;
        public string Source { get; init; } = "builtin";
        public List<OrderFlowStepDto> Steps { get; init; } = new();

        public OrderFlowStepDto? GetStep(string? code) =>
            code == null ? null : Steps.FirstOrDefault(s => s.Code == code);

        public bool IsAllowed(string fromStatus, string toStatus) =>
            Steps.Any(s => s.Code == fromStatus && s.AllowedNextCodes.Contains(toStatus));

        public IReadOnlyList<string> NextOf(string? currentStatus) =>
            GetStep(currentStatus)?.AllowedNextCodes ?? (IReadOnlyList<string>)[];
    }

    /// <summary>Input step khi SystemAdmin upsert profile (code + label + thứ tự + transitions).</summary>
    public sealed record OrderFlowStepInput(
        string Code,
        string DisplayName,
        int SortOrder,
        bool IsTerminal,
        List<string> AllowedNextCodes);

    /// <summary>Profile view cho SystemAdmin UI.</summary>
    public sealed class OrderFlowProfileDto
    {
        public Guid Id { get; set; }
        public Guid TenantId { get; set; }
        public string Scope { get; set; } = string.Empty;
        public string? SourceType { get; set; }
        public string? CategoryKey { get; set; }
        public string Name { get; set; } = string.Empty;
        public bool IsActive { get; set; }
        public List<OrderFlowStepDto> Steps { get; set; } = new();
    }

    /// <summary>
    /// Issue #195: đọc + resolve flow đơn. 2 implementations:
    /// - CoreHub OrderFlowService (Gateway, EF → PG, memory-cache 2 phút)
    /// - ShopERP OrderFlowApiClient (HTTP → GET /api/v1/orderflow/resolve, cache local)
    /// </summary>
    public interface IOrderFlowService
    {
        /// <summary>Resolve profile theo precedence: tenant+source → tenant → system+source → system → builtin.</summary>
        Task<OrderFlowResolution> ResolveFlowAsync(Guid tenantId, string? sourceType, CancellationToken ct = default);

        Task<bool> IsTransitionAllowedAsync(Guid tenantId, string? sourceType, string fromStatus, string toStatus, CancellationToken ct = default);

        Task<IReadOnlyList<string>> GetNextStatusesAsync(Guid tenantId, string? sourceType, string currentStatus, CancellationToken ct = default);
    }

    /// <summary>
    /// Issue #195: SystemAdmin CRUD order-flow profiles — chỉ Gateway implement
    /// (ShopERP HTTP client không cần; gọi sẽ throw NotSupportedException).
    /// </summary>
    public interface IOrderFlowAdminService
    {
        /// <summary>Liệt kê profiles; tenantId=null → tất cả (system + mọi tenant).</summary>
        Task<List<OrderFlowProfileDto>> GetProfilesAsync(Guid? tenantId, CancellationToken ct = default);

        /// <summary>
        /// Tạo/cập nhật profile cho (tenantId, sourceType, categoryKey). tenantId null/Empty →
        /// System scope. Một (tenant, scope, sourceType, categoryKey) chỉ 1 active profile —
        /// upsert thay thế toàn bộ steps.
        /// </summary>
        Task<OrderFlowProfileDto> UpsertProfileAsync(
            Guid? tenantId, string? sourceType, string? categoryKey, string name,
            IReadOnlyList<OrderFlowStepInput> steps, string? actor, CancellationToken ct = default);

        Task DeactivateProfileAsync(Guid profileId, string? actor, CancellationToken ct = default);

        /// <summary>Seed system defaults (mặc định bếp + booking) nếu chưa có — idempotent.</summary>
        Task EnsureSystemDefaultsAsync(CancellationToken ct = default);
    }
}
