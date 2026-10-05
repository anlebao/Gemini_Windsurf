using VanAn.Shared.Domain;
// Namespace "Booking" xung đột type Booking (lesson P1 — CS0118) → alias cho domain entity.
using BookingEntity = VanAn.Shared.Domain.Booking;

namespace VanAn.CoreHub.Services.Booking;

/// <summary>1 dòng add-on trong create booking (SRS §8.3 — non-scheduling).</summary>
public sealed record AddOnLine(Guid AddOnId, int Quantity);

/// <summary>
/// Command create booking (SRS §21.1). CustomerId/CustomerDeviceId — zero-friction identity (§4.1);
/// StaffId null = "Bất kỳ ai" (tenant assign sau). AttributionId = immutable snapshot từ QR (§7.6 — P3).
/// </summary>
public sealed record CreateBookingCommand(
    Guid OfferingId,
    DateTime StartAt,
    Guid? StaffId,
    Guid? CustomerId,
    string? CustomerDeviceId,
    string? CustomerNote,
    IReadOnlyList<AddOnLine> AddOns,
    Guid? AttributionId = null);

/// <summary>DTO status polling (SRS §10.3 — ETag-ready qua Version; public token = PublicBookingCode §25).</summary>
public sealed record BookingStatusDto(
    Guid BookingId,
    string PublicBookingCode,
    BookingStatus Status,
    BookingSubState SubState,
    Guid? StaffId,
    string OfferingNameSnapshot,
    DateTime StartAt,
    DateTime EndAt,
    decimal EstimatedTotal,
    PaymentStatus PaymentStatus,
    BookingInvoiceStatus InvoiceStatus,
    int Version,
    DateTime? CompletedAt,
    Guid? OrderId);

/// <summary>
/// IBookingService — create (Idempotency-Key §21.1) · state machine 9-state (§9.3, backend authoritative §37.2)
/// · confirm/reject/cancel idempotent (§21.2) · assign/change staff idempotent + re-check conflict (§21.3, §13)
/// · check-in/start/complete · no-show · status polling DTO (§10) · BookingEvent mọi transition (§23-24)
/// · double-booking prevention (SRS §12, AC-C04) — atomic conflict transaction PG (SELECT FOR UPDATE,
/// precedent WalletService HR-SCALE-3). KHÔNG dựa UI lock.
/// </summary>
public interface IBookingService
{
    /// <summary>Create booking — idempotent theo Idempotency-Key (retry không tạo booking trùng).</summary>
    Task<BookingEntity> CreateBookingAsync(TenantId tenantId, CreateBookingCommand command, string idempotencyKey, CancellationToken ct = default);

    // ── State machine transitions (idempotent) ─────────────────────────────
    Task<BookingEntity> ConfirmAsync(TenantId tenantId, Guid bookingId, Guid? actorId = null, CancellationToken ct = default);
    Task<BookingEntity> RejectAsync(TenantId tenantId, Guid bookingId, string? reason, Guid? actorId = null, CancellationToken ct = default);
    Task<BookingEntity> CancelAsync(TenantId tenantId, Guid bookingId, string? reason, Guid? actorId = null, CancellationToken ct = default);
    Task<BookingEntity> AssignStaffAsync(TenantId tenantId, Guid bookingId, Guid staffId, Guid? actorId = null, CancellationToken ct = default);
    Task<BookingEntity> ChangeStaffAsync(TenantId tenantId, Guid bookingId, Guid staffId, Guid? actorId = null, CancellationToken ct = default);
    Task<BookingEntity> CheckInAsync(TenantId tenantId, Guid bookingId, Guid? actorId = null, CancellationToken ct = default);
    Task<BookingEntity> StartServiceAsync(TenantId tenantId, Guid bookingId, Guid? actorId = null, CancellationToken ct = default);
    Task<BookingEntity> CompleteAsync(TenantId tenantId, Guid bookingId, decimal actualTotal, Guid? actorId = null, CancellationToken ct = default);
    Task<BookingEntity> MarkNoShowAsync(TenantId tenantId, Guid bookingId, string? reason, Guid? actorId = null, CancellationToken ct = default);

    // ── Reads ───────────────────────────────────────────────────────────────
    /// <summary>Status polling theo public booking token (opaque, non-sequential §25).</summary>
    Task<BookingStatusDto?> GetPublicStatusAsync(string publicBookingCode, CancellationToken ct = default);

    /// <summary>Queue cho tenant backend (P5): lọc theo status + khoảng thời gian, sắp theo StartAt.</summary>
    Task<IReadOnlyList<BookingEntity>> GetQueueAsync(TenantId tenantId, BookingStatus? status = null, DateTime? from = null, DateTime? to = null, CancellationToken ct = default);

    Task<BookingEntity?> GetBookingAsync(TenantId tenantId, Guid bookingId, CancellationToken ct = default);

    // ── BookingTenantConfig (Q5 feature-flag + deposit policy §16.1) ────────
    Task<BookingTenantConfig> GetConfigAsync(TenantId tenantId, CancellationToken ct = default);
    Task<BookingTenantConfig> UpdateConfigAsync(
        TenantId tenantId, bool isEnabled, DepositPolicy depositPolicy,
        decimal? depositFixedAmount, decimal? depositPercentage,
        string? cancelReschedulePolicy, string? einvoiceMode, CancellationToken ct = default);
}
