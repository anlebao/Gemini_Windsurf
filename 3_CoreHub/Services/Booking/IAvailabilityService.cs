using VanAn.Shared.Domain;

namespace VanAn.CoreHub.Services.Booking;

/// <summary>1 slot khả dụng cho customer (SRS §11.5 — chỉ trả available, không trả lý do).</summary>
public sealed record AvailableSlot(DateTime StartAt, DateTime EndAt, Guid StaffId, string StaffName);

/// <summary>1 slot × staff trong matrix admin (who-is-available §14) — kèm lý do unavailable tối thiểu.</summary>
public sealed record StaffAvailabilityStatus(
    Guid StaffId, string StaffName, DateTime SlotStartAt, DateTime SlotEndAt, bool IsAvailable, string? UnavailableReason);

/// <summary>Kết quả validate 1 slot cụ thể (server-side re-check — SRS §12, §13 manual override).</summary>
public sealed record AvailabilityCheckResult(bool IsAvailable, string? UnavailableReason);

/// <summary>
/// IAvailabilityService — MVP availability rule (SRS §11.4): active ∧ skill match ∧ working interval
/// ∧ không break/leave/unavailable ∧ không booking conflict. Slot step 30 phút (MVP constant).
/// Customer API chỉ nhận danh sách available (Risk 3); tenant admin xem lý do tối thiểu (§11.5).
/// </summary>
public interface IAvailabilityService
{
    /// <summary>Slot available trong ngày cho offering (staffId? filter staff cụ thể). Chỉ trả available.</summary>
    Task<IReadOnlyList<AvailableSlot>> GetAvailableSlotsAsync(
        TenantId tenantId, Guid offeringId, DateOnly date, Guid? staffId = null, CancellationToken ct = default);

    /// <summary>Matrix staff × slot trong ngày kèm lý do unavailable — tenant admin (who-is-available §14).</summary>
    Task<IReadOnlyList<StaffAvailabilityStatus>> GetStaffAvailabilityMatrixAsync(
        TenantId tenantId, DateOnly date, Guid? offeringId = null, CancellationToken ct = default);

    /// <summary>Server-side re-check 1 slot (dùng trong create/assign/change-staff — SRS §12).</summary>
    Task<AvailabilityCheckResult> ValidateSlotAsync(
        TenantId tenantId, Guid offeringId, Guid staffId, DateTime startAt, CancellationToken ct = default);
}
