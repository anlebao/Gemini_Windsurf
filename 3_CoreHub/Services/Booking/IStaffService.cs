using VanAn.Shared.Domain;
// Domain junction entity "StaffService" (Staff ↔ Offering) trùng tên service class → alias.
using StaffServiceEntity = VanAn.Shared.Domain.StaffService;

namespace VanAn.CoreHub.Services.Booking;

/// <summary>
/// IStaffService — CRUD Staff + skills (StaffService junction) + working schedule + exact-date override
/// (SRS §11.1-11.3). MỌI query filter theo TenantId (bài học RV a21f97f2 — KHÔNG leak cross-tenant).
/// Booking data sống ở Gateway PG (D1). Namespace "Booking" giữ nguyên — xung đột type Booking xử lý
/// bằng alias (lesson P1).
/// </summary>
public interface IStaffService
{
    // ── Staff ──────────────────────────────────────────────────────────────
    Task<Staff> CreateStaffAsync(TenantId tenantId, string displayName, string? role = null, Guid? staffUserId = null, CancellationToken ct = default);

    Task<Staff> UpdateStaffAsync(
        TenantId tenantId, Guid staffId, string displayName, string? role,
        string? avatarUrl, Guid? staffUserId, bool isActive, CancellationToken ct = default);

    Task<Staff?> GetStaffAsync(TenantId tenantId, Guid staffId, CancellationToken ct = default);

    /// <summary>List staff của tenant (activeOnly=true → chỉ staff active — dùng cho customer-facing).</summary>
    Task<IReadOnlyList<Staff>> ListStaffAsync(TenantId tenantId, bool activeOnly = false, CancellationToken ct = default);

    /// <summary>Staff có skill phù hợp offering (SRS §11.2 — StaffService junction).</summary>
    Task<IReadOnlyList<Staff>> ListEligibleStaffAsync(TenantId tenantId, Guid offeringId, bool activeOnly = true, CancellationToken ct = default);

    // ── Skills (StaffService junction) ─────────────────────────────────────
    Task<IReadOnlyList<StaffServiceEntity>> ListStaffServicesAsync(TenantId tenantId, Guid staffId, CancellationToken ct = default);

    /// <summary>Thay thế toàn bộ danh sách offering mà staff được phép phục vụ (idempotent replace).</summary>
    Task SetStaffServicesAsync(TenantId tenantId, Guid staffId, IReadOnlyCollection<Guid> offeringIds, CancellationToken ct = default);

    // ── Working schedule (weekday recurring + break, SRS §11.3) ────────────
    /// <summary>Upsert 1 weekday schedule — unique (StaffId, Weekday); update nếu đã tồn tại.</summary>
    Task<StaffWorkingSchedule> UpsertWorkingScheduleAsync(
        TenantId tenantId, Guid staffId, int weekday, TimeSpan startTime, TimeSpan endTime,
        TimeSpan? breakStart = null, TimeSpan? breakEnd = null, CancellationToken ct = default);

    Task<IReadOnlyList<StaffWorkingSchedule>> ListWorkingSchedulesAsync(TenantId tenantId, Guid staffId, CancellationToken ct = default);

    Task DeleteWorkingScheduleAsync(TenantId tenantId, Guid scheduleId, CancellationToken ct = default);

    // ── Exact-date override (WORKING | LEAVE | UNAVAILABLE | BREAK) ────────
    Task<StaffScheduleOverride> AddOverrideAsync(
        TenantId tenantId, Guid staffId, DateTime date, StaffScheduleOverrideType overrideType,
        TimeSpan? startTime = null, TimeSpan? endTime = null, string? note = null, CancellationToken ct = default);

    Task<IReadOnlyList<StaffScheduleOverride>> ListOverridesAsync(TenantId tenantId, Guid staffId, DateTime from, DateTime to, CancellationToken ct = default);

    Task DeleteOverrideAsync(TenantId tenantId, Guid overrideId, CancellationToken ct = default);
}
